using System;

namespace Jannesen.PushNotification.Internal
{
    internal sealed class ShutdownMessage
    {
        public      readonly        APNSLegacyPushConnection    Connection;

        public                                                  ShutdownMessage(APNSLegacyPushConnection connection)
        {
            Connection = connection;
        }
    }
}
