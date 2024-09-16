using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Jannesen.PushNotification
{
    [Serializable]
    public class PushNotificationConfigException: Exception
    {
        public                              PushNotificationConfigException(string message): base(message)
        {
        }
        public                              PushNotificationConfigException(string message, Exception innerException): base(message, innerException)
        {
        }

        protected                           PushNotificationConfigException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    [Serializable]
    public class PushNotificationConnectionException: Exception
    {
        public                              PushNotificationConnectionException(string message): base(message)
        {
        }
        public                              PushNotificationConnectionException(string message, Exception innerException): base(message, innerException)
        {
        }

        protected                           PushNotificationConnectionException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    [Serializable]
    public class PushNotificationException: Exception
    {
        public          PushMessage        Notification             { get; private set; }

        public                              PushNotificationException(PushMessage notification, string message): base(message)
        {
            this.Notification = notification;
        }
        public                              PushNotificationException(PushMessage notification, string message, Exception innerException): base(message, innerException)
        {
            this.Notification = notification;
        }

        protected                           PushNotificationException(SerializationInfo info, StreamingContext context): base(info, context)
        {
            Notification = (PushMessage)info.GetValue(nameof(Notification), typeof(PushMessage));
        }
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void                GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Notification), Notification);
        }
    }

    [Serializable]
    public class PushNotificationInvalidDeviceException: PushNotificationException
    {
        public                              PushNotificationInvalidDeviceException(PushMessage notification): base(notification, "Invalid device-token '" + notification.DeviceToken + "'.")
        {
        }

        protected                           PushNotificationInvalidDeviceException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    [Serializable]
    public class PushNotificationExpiredException: PushNotificationException
    {
        public                              PushNotificationExpiredException(PushMessage notification): base(notification, "Notification expired device-token '" + notification.DeviceToken + "'.")
        {
        }

        protected                           PushNotificationExpiredException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }

    [Serializable]
    public class PushNotificationServiceException: Exception
    {
        public                              PushNotificationServiceException(string message): base(message)
        {
        }
        public                              PushNotificationServiceException(string message, Exception innerException): base(message, innerException)
        {
        }

        protected                           PushNotificationServiceException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}
