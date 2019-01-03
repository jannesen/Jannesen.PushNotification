using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jannesen.PushNotification
{
    public class AppleFeedback
    {
        public      readonly        DateTime            Timestamp;
        public      readonly        string              DeviceToken;

        internal                                        AppleFeedback(int timestamp, byte[] deviceToken)
        {
            Timestamp   = new DateTime(Internal.Library.UnixEPoch.Ticks + timestamp * TimeSpan.TicksPerSecond);
            DeviceToken = Internal.Library.HexDump(deviceToken, deviceToken.Length).ToLower();
        }
    }
}
