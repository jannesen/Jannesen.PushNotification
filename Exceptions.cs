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

    public class PushNotificationException: Exception
    {
        public          PushMessage         Notification             { get; private set; }

        public                              PushNotificationException(PushMessage notification, string message): base(message)
        {
            this.Notification = notification;
        }
        public                              PushNotificationException(PushMessage notification, string message, Exception innerException): base(message, innerException)
        {
            this.Notification = notification;
        }
    }

    public class PushNotificationInvalidDeviceException: PushNotificationException
    {
        public                              PushNotificationInvalidDeviceException(PushMessage notification): base(notification, "Invalid device-token '" + notification.DeviceToken + "'.")
        {
        }
    }

    public class PushNotificationExpiredException: PushNotificationException
    {
        public                              PushNotificationExpiredException(PushMessage notification): base(notification, "Notification expired device-token '" + notification.DeviceToken + "'.")
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
}
