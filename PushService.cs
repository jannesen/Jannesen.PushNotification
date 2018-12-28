using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jannesen.PushNotification.Internal;

namespace Jannesen.PushNotification
{
    public sealed class PushService: IDisposable
    {
        public      delegate    void                        ErrorCallback(Exception err);
        public      delegate    void                        SendCallback(Notification notification);

        public                  ServiceConfig               Config                  { get; private set; }
        public                  ErrorCallback               OnError;
        public                  SendCallback                OnSend;

        private                 List<object>                _queue;
        private                 int                         _queueSendPos;
        private                 ServiceConnection           _connection;
        private                 bool                        _workerActive;
        private                 bool                        _shutdown;
        private                 Task                        _activeWorker;
        private                 object                      _lockObject;

        public                                              PushService(ServiceConfig config)
        {
            Config = config;

            _queue         = new List<object>();
            _queueSendPos  = 0;
            _connection    = null;
            _workerActive  = false;
            _activeWorker  = null;
            _lockObject    = new object();
        }
        public                  void                        Dispose()
        {
            ServiceConnection   connection;

            lock(_lockObject)
            {
                connection = _connection;
                _shutdown   = false;
                _connection = null;
                _queue.Clear();
            }

            if (connection != null) {
                connection.Dispose();
            }
        }

        public                  void                        SendNotification(Notification notification)
        {
            lock(_lockObject)
            {
                if (!_shutdown) {
                    _queue.Add(notification);
                    _startWorker();
                }
            }
        }
        public          async   Task                        ShutdownAsync()
        {
            List<Notification>      dropped = null;

            lock(_lockObject)
            {
                _shutdown = true;
                dropped = _dropQueue();
            }

            NotificationDropped(dropped, new Exception("Shutdown"));

            if (_activeWorker != null)
                await _activeWorker;

            await _closeConnection();
        }

        private         async   Task                        _workerTaskAsync()
        {
            try {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(Config.ToString() + ": WorkerTask started.");
#endif
                object  msg;

                while ((msg = _getNextMessage()) != null) {
                    try {
                        if (msg is Notification) {
                            if (_connection == null || !_connection.isAvailable)
                                _connection = await Config.GetNewConnection(this);

                            if (_connection.isAvailable) {
                                _send((Notification)msg);

                                await _connection.SendNotificationAsync((Notification)msg);

                                if (_connection.needsRecyle)
                                    await _closeConnection();
                            }
                        }

                        if (msg is ShutdownMessage) {
                            var sc = ((ShutdownMessage)msg).Connection;
                            if (sc == null || _connection == sc)
                                await _closeConnection();
                        }
                    }
                    catch(Exception err) {
                        Error(err);
                    }
                }
#if DEBUG
                System.Diagnostics.Debug.WriteLine(Config.ToString() + ": WorkerTask stopped.");
#endif
            }
            catch(Exception err) {
                Error(new PushNotificationServiceException(this, "WorkerTask crashed!", err));
            }
        }
        private                 object                      _getNextMessage()
        {
            lock (_lockObject)
            {
                while (_queueSendPos < _queue.Count) {
                    if (_queueSendPos > 128) {
                        _queue.RemoveRange(0, 128);
                        _queueSendPos -= 128;
                    }

                    var msg = _queue[_queueSendPos++];
                    if (msg != null)
                        return msg;
                }

                _workerActive = false;

                _queue.Clear();
                _queueSendPos = 0;
                if (_queue.Capacity > 64)
                    _queue.Capacity = 64;

                return null;
            }
        }
        private                 void                        _send(Notification notification)
        {
            try {
                OnSend?.Invoke(notification);
            }
            catch(Exception err) {
                System.Diagnostics.Debug.WriteLine("SEND CALLBACK FAILED: " + err.Message);
            }
        }
        private                 List<Notification>          _dropQueue()
        {
            List<Notification>      dropped = new List<Notification>();

            while (_queueSendPos < _queue.Count) {
                var o = _queue[_queueSendPos++];

                if (o is Notification)
                    dropped.Add((Notification)o);
            }

            _queue.Clear();
            _queueSendPos = 0;

            return dropped;
        }
        private                 void                        _startWorker()
        {
            if (!_workerActive) {
                _workerActive = true;
                _activeWorker = Task.Run(_workerTaskAsync);
            }
        }
        private         async   Task                        _closeConnection()
        {
            ServiceConnection       connection;

            lock(_lockObject)
            {
                connection = _connection;
                _connection = null;
            }

            if (connection != null) {
                try {
                    await connection.CloseAsync();
                }
                catch(Exception err) {
                    Error(new PushNotificationServiceException(this, "Connection close failed.", err));
                }
                finally {
                    connection.Dispose();
                }
            }
        }

        internal                void                        Error(Exception error)
        {
#if DEBUG
            {
                string  msg = Config.ToString() + ": ERROR:";

                for (Exception e = error; e != null ; e = e.InnerException)
                    msg += " " + e.Message;

                System.Diagnostics.Debug.WriteLine(msg);
            }
#endif
            try {
                OnError?.Invoke(error);
            }
            catch(Exception err) {
                System.Diagnostics.Debug.WriteLine("ONERROR CALLBACK FAILED: " + err.Message);
            }
        }
        internal                void                        ConnectionClosed(ServiceConnection connection, List<Notification> requeueNotifications = null)
        {
            lock(_lockObject)
            {
                if (_connection == connection)
                    _connection = null;

                if (requeueNotifications != null) {
                    foreach(var n in requeueNotifications) {
                        if (n is Notification)
                            _queue.Add(n);
                    }

                    if (!_shutdown && _queue.Count > 0)
                        _startWorker();
                }
            }
        }
        internal                void                        RecyleConnection(ServiceConnection connection)
        {
            lock(_lockObject)
            {
                if (_connection == connection && !_shutdown) {
                    _queue.Insert(_queueSendPos, new Internal.ShutdownMessage(connection));
                    _startWorker();
                }
            }
        }
        internal                void                        NotificationDropped(List<Notification> notifications, Exception err)
        {
            if (notifications != null) {
                foreach(var n in notifications) {
                    if (n != null)
                        Error(new PushNotificationException(n, "Notification to '" + n.DeviceAddress + "' dropped.", err));
                }
            }
        }
    }
}
