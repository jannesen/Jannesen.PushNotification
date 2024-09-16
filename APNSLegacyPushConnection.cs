using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jannesen.FileFormat.Json;
using Jannesen.PushNotification.Library;

// https://developer.apple.com/library/content/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/BinaryProviderAPI.html#//apple_ref/doc/uid/TP40008194-CH13-SW1

namespace Jannesen.PushNotification
{
    internal sealed class APNSLegacyPushConnection: IDisposable
    {
        public  readonly            APNSLegacyService           Service;
        public  readonly            APNSLegacyConfig            Config;

        public                      bool                        isAvailable     => _isAvailable;
        public                      bool                        needsRecyle     => _notificationIdentifier >= Config.RecyleCount;

        private                     int                         _notificationIdentifier;
        private     volatile        bool                        _isAvailable;
        private                     APNSLegacyConnection        _connection;
        private                     Task                        _receiveTask;
        private                     List<PushMessage>          _notifications;
        private                     Timer                       _connectionTimer;
        private readonly            object                      _lockObject;

        public                                                  APNSLegacyPushConnection(APNSLegacyService service, APNSLegacyConfig config)
        {
            Service = service;
            Config  = config;

            _notificationIdentifier = 0;
            _isAvailable            = false;
            _lockObject             = new object();
        }
        public                      void                        Dispose()
        {
            _close();
        }

        public               async  Task                        ConnectAsync()
        {
            _connection = new APNSLegacyConnection();
            string hostname = Config.Development ? "gateway.sandbox.push.apple.com" : "gateway.push.apple.com";

            try {
                await _connection.ConnectAsync(hostname, 2195, Config.ClientCertificate, 30 * 1000);
            }
            catch(Exception err) {
                throw new PushNotificationConnectionException("connect(" + hostname + "): failed.", err);
            }

            lock(_lockObject) {
                _isAvailable   = _connection.Connected;
                _notifications = new List<PushMessage>(Config.RecyleCount);

                if (_isAvailable) {
                    _receiveTask      = Task.Run(_receiveResponceAsync);
                    _connectionTimer = new Timer(_connectionTimer_callback, null, Config.RecyleTimout, Timeout.Infinite);
                }
            }
        }
        public               async  Task                        SendNotificationAsync(PushMessage notification)
        {
            byte[]      msg = new byte[2102];

            int     pos = 1 + 4;

            try {
                // 3 NotificationIdentifier
                var notificationIdentifier = _notificationIdentifier;
                {
                    msg[pos++] = 0x03;
                    msg[pos++] = 0;
                    msg[pos++] = 4;
                    msg[pos++] = (byte)((notificationIdentifier >> 24) & 0xFF);
                    msg[pos++] = (byte)((notificationIdentifier >> 16) & 0xFF);
                    msg[pos++] = (byte)((notificationIdentifier >>  8) & 0xFF);
                    msg[pos++] = (byte)((notificationIdentifier      ) & 0xFF);
                }

                if (notification != null) {
                    // 1 DeviceToken
                    {
                        var deviceToken = notification.DeviceToken;
                        if (deviceToken.Length != 64)
                            throw new FormatException("Invalid DeviceToken.");

                        msg[pos++] = 0x01;
                        msg[pos++] = 0;
                        msg[pos++] = 32;

                        for (int i = 0 ; i < deviceToken.Length ; i += 2)
                            msg[pos++] = (byte)(StaticLib.HexToNibble(deviceToken[i]) << 4 | StaticLib.HexToNibble(deviceToken[i + 1]));
                    }

                    // 2 Payload
                    {
                        byte[]      bpayload;

                        using (var x = new StringWriter()) {
                            (new JsonWriter(x)).WriteValue(notification.Payload);
                            bpayload = Encoding.UTF8.GetBytes(x.ToString());
                        }

                        if (bpayload.Length > 2048)
                            throw new FormatException("Payload to big.");

                        msg[pos++] = 0x02;
                        msg[pos++] = (byte)((bpayload.Length >>  8) & 0xFF);
                        msg[pos++] = (byte)((bpayload.Length      ) & 0xFF);

                        Array.Copy(bpayload, 0, msg, pos, bpayload.Length);
                        pos += bpayload.Length;
                    }

                    // 4 ExpirationDate
                    {
                        var expireTime = notification.ExpireTime;
                        if (expireTime.Ticks < DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute)
                            throw new PushNotificationExpiredException(notification);

                        var     v = (Int32)((expireTime - StaticLib.UnixEPoch).Ticks / TimeSpan.TicksPerSecond);

                        msg[pos++] = 0x04;
                        msg[pos++] = 0;
                        msg[pos++] = 4;
                        msg[pos++] = (byte)((v >> 24) & 0xFF);
                        msg[pos++] = (byte)((v >> 16) & 0xFF);
                        msg[pos++] = (byte)((v >>  8) & 0xFF);
                        msg[pos++] = (byte)((v      ) & 0xFF);
                    }

                    // 5 Priority
                    if (notification.HighPriority) {
                        msg[pos++] = 0x05;
                        msg[pos++] = 0;
                        msg[pos++] = 1;
                        msg[pos++] = 0x05;
                    }
                }

                int sz = pos - 5;

                msg[0] = 0x02;
                msg[1] = (byte)((sz >> 24) & 0xFF);
                msg[2] = (byte)((sz >> 16) & 0xFF);
                msg[3] = (byte)((sz >>  8) & 0xFF);
                msg[4] = (byte)((sz      ) & 0xFF);

                var queued = false;

                lock(_lockObject) {
                    if (_notifications != null) {
                        _notifications.Insert(_notificationIdentifier++, notification);
                        queued = true;
                    }
                }

                if (!queued) {
                    if (notification != null)
                        Service.SendNotification(notification);
                    return ;
                }
            }
            catch(Exception err) {
                if (notification != null)
                    await Service.Error(new PushNotificationException(notification, "Notification to '" + notification.DeviceToken + "' failed. Invalid notification format.", err));
            }

            try {
                await _connection.SendAsync(msg, pos);
            }
            catch(Exception err) {
                Dispose();
                await Service.Error(new PushNotificationServiceException("Sending request to APSN failed.", err));
            }
        }
        public               async  Task                        CloseAsync()
        {
            _isAvailable = false;

            lock (_lockObject) {
                if (_receiveTask == null) {
                    _close();
                    return;
                }

                if (_connectionTimer != null) {
                    _connectionTimer.Dispose();
                    _connectionTimer = null;
                }
            }

            await SendNotificationAsync(null);

            try {
                using (CancellationTokenSource cts = new CancellationTokenSource(15000)) {
                    await Task.WhenAny(_receiveTask, Task.Delay(-1, cts.Token));
                }
            }
            catch(TaskCanceledException) {
            }

            lock(_lockObject) {
                if (_connection == null) {
                    return;
                }
            }

            _close();
#if DEBUG
            await Service.Error(new PushNotificationServiceException("Shutdown of APSN connection failed."));
#endif
        }

        private                     void                        _close()
        {
            lock(_lockObject) {
                _isAvailable = false;

                if (_connection != null) {
                    _connection.Dispose();
                    _connection = null;
                }

                if (_connectionTimer != null) {
                    _connectionTimer.Dispose();
                    _connectionTimer = null;
                }
            }
        }
        private     async           Task                        _receiveResponceAsync()
        {
            byte[]                  msg = null;
            List<PushMessage>      notifications;

            try {
                msg = await _connection.ReceiveAsync(6, true);

                lock(_lockObject) {
                    notifications = _notifications;
                    _notifications = null;
                }

                _close();
            }
            catch(Exception err) {
                bool    connected;

                lock(_lockObject) {
                    notifications = _notifications;
                    _notifications = null;
                    connected = _connection != null;
                    _close();
                }

                if (connected)
                    await Service.Error(new PushNotificationServiceException("ReceiveAsync response from APNS failed.", err));
            }

            if (msg != null && msg[0] == 0x08) {
                var ni = (msg[2] << 24) | (msg[3] << 16) | (msg[4] << 8) | (msg[5]);

                if (ni >= 0 && ni == _notificationIdentifier - 1 && notifications[ni] == null)
                    notifications = null;
                else {
                    if (ni >= 0 && ni < _notificationIdentifier) {
                        var n = notifications[ni];

                        if (n != null)
                            await Service.Error((msg[1] == 0x08)
                                                ? new PushNotificationInvalidDeviceException(n)
                                                : new PushNotificationException(n, "Submit notification to '" + n.DeviceToken + "' failed error #" + msg[1] + "."));
                    }

                    for (int i = 0 ; i <= ni && i < _notificationIdentifier ; ++i)
                        notifications[i] = null;
                }

                Service.ConnectionClosed(this, notifications);
            }
            else {
                await Service.NotificationDropped(notifications, new Exception("Connection dropped by APSN."));
                Service.ConnectionClosed(this);
            }
        }
        private                     void                        _connectionTimer_callback(object state)
        {
            Service.RecyleConnection(this);
        }
    }
}
