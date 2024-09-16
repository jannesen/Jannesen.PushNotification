using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jannesen.PushNotification.Internal;

namespace Jannesen.PushNotification
{
    public sealed class APNSLegacyService: IDisposable
    {
        public      delegate    Task                        ErrorCallback(Exception err);
        public      delegate    Task                        SendCallback(Notification notification);

        public                  APNSLegacyConfig            Config                  { get; private set; }
        public                  ErrorCallback               OnError;
        public                  SendCallback                OnSend;

        private readonly        List<object>                _queue;
        private                 int                         _queueSendPos;
        private                 APNSLegacyPushConnection    _connection;
        private                 bool                        _shutdown;
        private                 Task                        _activeWorker;
        private readonly        object                      _lockObject;

        public                                              APNSLegacyService(APNSLegacyConfig config)
        {
            Config = config;

            _queue         = new List<object>();
            _queueSendPos  = 0;
            _connection    = null;
            _activeWorker  = null;
            _lockObject    = new object();
        }
        public                  void                        Dispose()
        {
            APNSLegacyPushConnection    connection;

            lock(_lockObject) {
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
            lock(_lockObject) {
                if (!_shutdown) {
                    _queue.Add(notification);
                    _startWorker();
                }
            }
        }
        public          async   Task                        WaitIdle(CancellationToken cancellationToken)
        {
            Task                    activeWorker;

            lock(_lockObject) {
                activeWorker = _activeWorker;
            }

            if (activeWorker != null) { 
                TaskCompletionSource<object>    tcs = new TaskCompletionSource<object>();

                using (var x = cancellationToken.Register(() => { tcs.SetException(new TaskCanceledException()); })) {
                   await Task.WhenAny(activeWorker, tcs.Task);
                }
            }
        }
        public          async   Task                        ShutdownAsync()
        {
            List<Notification>      dropped;
            Task                    activeWorker;

            lock(_lockObject) {
                _shutdown = true;
                dropped = _dropQueue();
                activeWorker = _activeWorker;
            }

            await NotificationDropped(dropped, new Exception("Shutdown"));

            if (activeWorker != null)
                await activeWorker;

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
                            var connection = _connection;
                            if (connection == null || !connection.isAvailable) {
                                if (connection != null) {
                                    connection.Dispose();
                                }

                                try {
                                    connection = new APNSLegacyPushConnection(this, Config);

                                    await connection.ConnectAsync();
                                }
                                catch(Exception err) {
                                    throw new PushNotificationConnectionException("Appl.GetNewConnection failed.", err);
                                }

                                _connection = connection;
                            }

                            if (connection.isAvailable) {
                                await _send((Notification)msg);

                                await connection.SendNotificationAsync((Notification)msg);

                                if (connection.needsRecyle && _connection == connection) { 
                                    await _closeConnection();
                                }
                            }
                        }

                        if (msg is ShutdownMessage) {
                            var sc = ((ShutdownMessage)msg).Connection;
                            if (sc == null || _connection == sc)
                                await _closeConnection();
                        }
                    }
                    catch(Exception err) {
                        await Error(err);
                    }
                }
#if DEBUG
                System.Diagnostics.Debug.WriteLine(Config.ToString() + ": WorkerTask stopped.");
#endif
            }
            catch(Exception err) {
                await Error(new PushNotificationServiceException("WorkerTask crashed!", err));
            }
        }
        private                 object                      _getNextMessage()
        {
            lock (_lockObject) {
                while (_queueSendPos < _queue.Count) {
                    if (_queueSendPos > 128) {
                        _queue.RemoveRange(0, 128);
                        _queueSendPos -= 128;
                    }

                    var msg = _queue[_queueSendPos++];
                    if (msg != null)
                        return msg;
                }

                _activeWorker = null;

                _queue.Clear();
                _queueSendPos = 0;
                if (_queue.Capacity > 64)
                    _queue.Capacity = 64;

                return null;
            }
        }
        private         async   Task                        _send(Notification notification)
        {
            try {
                await OnSend?.Invoke(notification);
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
            if (_activeWorker == null) {
                _activeWorker = Task.Run(_workerTaskAsync);
            }
        }
        private         async   Task                        _closeConnection()
        {
            APNSLegacyPushConnection   connection;

            lock(_lockObject) {
                connection = _connection;
                _connection = null;
            }

            if (connection != null) {
                try {
                    await connection.CloseAsync();
                }
                catch(Exception err) {
                    await Error(new PushNotificationServiceException("Connection close failed.", err));
                }
                finally {
                    connection.Dispose();
                }
            }
        }

        internal        async   Task                        Error(Exception error)
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
                await OnError?.Invoke(error);
            }
            catch(Exception err) {
                System.Diagnostics.Debug.WriteLine("ONERROR CALLBACK FAILED: " + err.Message);
            }
        }
        internal                void                        ConnectionClosed(APNSLegacyPushConnection connection, List<Notification> requeueNotifications = null)
        {
            lock(_lockObject) {
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
        internal                void                        RecyleConnection(APNSLegacyPushConnection connection)
        {
            lock(_lockObject) {
                if (_connection == connection && !_shutdown) {
                    _queue.Insert(_queueSendPos, new Internal.ShutdownMessage(connection));
                    _startWorker();
                }
            }
        }
        internal        async   Task                        NotificationDropped(List<Notification> notifications, Exception err)
        {
            if (notifications != null) {
                foreach(var n in notifications) {
                    if (n != null)
                        await Error(new PushNotificationException(n, "Notification to '" + n.DeviceAddress + "' dropped.", err));
                }
            }
        }
    }
}
