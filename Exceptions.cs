using System;

namespace Jannesen.PushNotification
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class PushNotificationConfigException: Exception
    {
        public                              PushNotificationConfigException(string message): base(message)
        {
        }
        public                              PushNotificationConfigException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class PushNotificationConnectionException: Exception
    {
        public                              PushNotificationConnectionException(string message): base(message)
        {
        }
        public                              PushNotificationConnectionException(string message, Exception innerException): base(message, innerException)
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class PushNotificationException: Exception
    {
        public          Notification        Notification             { get; private set; }

        public                              PushNotificationException(Notification notification, string message): base(message)
        {
            this.Notification = notification;
        }
        public                              PushNotificationException(Notification notification, string message, Exception innerException): base(message, innerException)
        {
            this.Notification = notification;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class PushNotificationInvalidDeviceException: PushNotificationException
    {
        public                              PushNotificationInvalidDeviceException(Notification notification): base(notification, "Invalid device-token '" + notification.DeviceAddress + "'.")
        {
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class PushNotificationServiceException: Exception
    {
        public          PushService Service      { get; private set; }

        public                                  PushNotificationServiceException(PushService service, string message): base(message)
        {
            this.Service = service;
        }
        public                                  PushNotificationServiceException(PushService service, string message, Exception innerException): base(message, innerException)
        {
            this.Service = service;
        }
    }
}
