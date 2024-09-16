using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Jannesen.PushNotification
{
    public class PushNotificationConfigException: Exception
    {
        public                              PushNotificationConfigException(string message): base(message)
        {
        }
        public                              PushNotificationConfigException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    public class PushNotificationConnectionException: Exception
    {
        public                              PushNotificationConnectionException(string message): base(message)
        {
        }
        public                              PushNotificationConnectionException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    public class PushNotificationServiceException: Exception
    {
        public                              PushNotificationServiceException(string message): base(message)
        {
        }
        public                              PushNotificationServiceException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    public enum PushNotificationErrorReason
    {
        Unknown                 = 0,
        InvalidDeviceToken      = 1,
        DeviceNotFound          = 2,
        MessageExpired          = 3,
        InvalidMessage          = 4,
        Dropped                 = 5,
        ServiceError            = 10
    }

    public class PushNotificationException: Exception
    {
        public          PushNotificationErrorReason    Reason                  { get; private set; }
        public          PushMessage                    Notification            { get; private set; }

        public                                          PushNotificationException(string message, PushNotificationErrorReason reason, PushMessage notification): base(message)
        {
            this.Reason       = reason;
            this.Notification = notification;
        }
        public                                          PushNotificationException(string message, PushNotificationErrorReason reason, PushMessage notification, Exception innerException): base(message, innerException)
        {
            this.Reason       = reason;
            this.Notification = notification;
        }
    }
}
