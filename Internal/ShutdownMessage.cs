using System;

namespace Jannesen.PushNotification.Internal
{
    internal sealed class ShutdownMessage
    {
        public      readonly        ServiceConnection       Connection;

        public                                              ShutdownMessage(ServiceConnection connection)
        {
            Connection = connection;
        }
    }
}
