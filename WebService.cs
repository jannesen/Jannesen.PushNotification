using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jannesen.PushNotification
{
    public abstract class WebService: IDisposable
    {
                                                                ~WebService()
        {
            Dispose(false);
        }
        public                      void                        Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected   abstract        void                        Dispose(bool disposing);

        public      abstract        Task                        InitAsync(CancellationToken ct);
        public      abstract        Task                        SendNotificationAsync(PushMessage notification, CancellationToken ct);
    }
}
