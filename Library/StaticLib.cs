using System;
using System.Text;

namespace Jannesen.PushNotification.Library
{
    static class StaticLib
    {
        public  static      DateTime        UnixEPoch   = new DateTime(1970, 1, 1, 0, 0, 0);

        public  static      int             ToUnixTimeSeconds(this DateTime dt)
        {
            return (int)((dt.Ticks - UnixEPoch.Ticks) / TimeSpan.TicksPerSecond);
        }
    }
}
