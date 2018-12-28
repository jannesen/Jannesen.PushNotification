
using System;
using Jannesen.FileFormat.Json;

namespace Jannesen.PushNotification
{
    public class Notification
    {
        public      readonly    string          DeviceAddress;
        public      readonly    JsonObject      Payload;
        public      readonly    DateTime        ExpireTime;
        public      readonly    bool            HighPriority;

        public                                  Notification(string deviceToken, JsonObject payload, DateTime expireTime, bool highPriority)
        {
            if (string.IsNullOrEmpty(deviceToken))
                throw new ArgumentException("Invalid deviceToken.");

            if (!(payload is JsonObject))
                throw new ArgumentException("Invalid payload.");

            if (expireTime.Ticks < DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute)
                throw new ArgumentException("Invalid expireTime.");

            DeviceAddress  = deviceToken;
            Payload      = payload;
            ExpireTime   = expireTime;
            HighPriority = highPriority;
        }
        public                                  Notification(string deviceToken, JsonObject payload, TimeSpan timeToLive, bool highPriority)
        {
            if (string.IsNullOrEmpty(deviceToken))
                throw new ArgumentException("Invalid deviceToken.");

            if (!(payload is JsonObject))
                throw new ArgumentException("Invalid payload.");

            if (timeToLive.Ticks < TimeSpan.TicksPerMinute)
                throw new ArgumentException("Invalid timeToLive.");

            DeviceAddress  = deviceToken;
            Payload      = payload;
            ExpireTime   = DateTime.UtcNow + timeToLive;
            HighPriority = highPriority;
        }
    }
}
