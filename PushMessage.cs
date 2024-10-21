
using System;
using Jannesen.FileFormat.Json;

namespace Jannesen.PushNotification
{
    public class PushMessage
    {
        public      readonly    string          DeviceToken;
        public      readonly    JsonObject      Payload;
        public      readonly    bool            HighPriority;
        public      readonly    DateTime        ExpireTime;

        public                                  PushMessage(string deviceToken, JsonObject payload, bool highPriority, DateTime expireTime)
        {
            if (string.IsNullOrEmpty(deviceToken))
                throw new ArgumentException("Invalid deviceToken.");

            if (!(payload is JsonObject))
                throw new ArgumentException("Invalid payload.");

            if (expireTime.Ticks < DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute)
                throw new ArgumentException("Invalid expireTime.");

            DeviceToken  = deviceToken;
            Payload      = payload;
            HighPriority = highPriority;
            ExpireTime   = expireTime;
        }
        public                                  PushMessage(string deviceToken, JsonObject payload, bool highPriority, TimeSpan timeToLive)
        {
            if (string.IsNullOrEmpty(deviceToken))
                throw new ArgumentException("Invalid deviceToken.");

            if (!(payload is JsonObject))
                throw new ArgumentException("Invalid payload.");

            if (timeToLive.Ticks < TimeSpan.TicksPerMinute)
                throw new ArgumentException("Invalid timeToLive.");

            DeviceToken  = deviceToken;
            Payload      = payload;
            HighPriority = highPriority;
            ExpireTime   = DateTime.UtcNow + timeToLive;
        }
    }
}
