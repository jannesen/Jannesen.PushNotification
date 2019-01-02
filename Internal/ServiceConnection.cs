using System;
using System.Threading.Tasks;

namespace Jannesen.PushNotification.Internal
{
    internal abstract class ServiceConnection: IDisposable
    {
        public      abstract        bool        isAvailable             { get; }
        public      virtual         bool        needsRecyle             { get { return false; } }

                                                ~ServiceConnection()
        {
            Dispose(false);
        }
        public                      void        Dispose()
        {
            Dispose(true);
        }
        public      abstract        void        Dispose(bool disposing);
        public      abstract        Task        SendNotificationAsync(Notification notification);
        public      virtual         Task        CloseAsync()
        {
            return Task.CompletedTask;
        }
    }
}
