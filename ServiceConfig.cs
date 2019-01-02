using System;
using System.Threading.Tasks;

namespace Jannesen.PushNotification
{
    public abstract class ServiceConfig
    {
        internal    abstract    Task<Internal.ServiceConnection>    GetNewConnection(PushService service);
    }
}
