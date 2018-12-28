using System;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Jannesen.FileFormat.Json;

// https://firebase.google.com/docs/cloud-messaging/http-server-ref

namespace Jannesen.PushNotification.Internal
{
    internal class FirebaseConnection: ServiceConnection
    {
        public      readonly        PushService                 Service;
        public      readonly        AndroidConfig               Config;

        public      override        bool                        isAvailable
        {
            get {
                return true;
            }
        }

        public                                                  FirebaseConnection(PushService service, AndroidConfig config)
        {
            Service = service;
            Config  = config;
        }
        public      override        void                        Dispose(bool disposing)
        {
        }

        public      override async  Task                        SendNotificationAsync(Notification notification)
        {
            try {
                byte[]      body;

                var timeToLive = (int)((notification.ExpireTime - DateTime.UtcNow).Ticks / TimeSpan.TicksPerSecond);
                if (timeToLive < 60)
                    throw new Exception("Notification expired.");

                using (StringWriter stringWriter = new StringWriter())
                {
                    using (JsonWriter jsonWriter = new JsonWriter(stringWriter))
                    {
                        jsonWriter.WriteStartObject();
                            jsonWriter.WriteNameValue("to", notification.DeviceAddress);

                            jsonWriter.WriteNameValue("time_to_live", timeToLive);

                            if (notification.HighPriority)
                                jsonWriter.WriteNameValue("priority", "high");

                            jsonWriter.WriteStartObject("data");
                                foreach(var p in notification.Payload)
                                    jsonWriter.WriteNameValue(p.Key, p.Value);
                            jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndObject();
                    }

                    var sbody = stringWriter.ToString();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("https://fcm.googleapis.com/fcm/send: POST " + sbody);
#endif
                    body = System.Text.Encoding.UTF8.GetBytes(sbody);
                }

                HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create("https://fcm.googleapis.com/fcm/send");
                webreq.Method = "POST";
                webreq.ContentType = "application/json; charset=UTF-8";
                webreq.Headers.Add("Authorization: key=" + Config.AuthorizationKey);
                webreq.Headers.Add("Sender: id="         + Config.SenderId);
                webreq.ContentLength = body.Length;

                string response;

                using (Stream dataStream = await webreq.GetRequestStreamAsync())
                {
                    dataStream.Write(body, 0, body.Length);

                    using (HttpWebResponse webresp = (HttpWebResponse)await webreq.GetResponseAsync())
                    {
                        if (webresp.StatusCode != HttpStatusCode.OK)
                            throw new Exception("FCM server returns: " + webresp.StatusCode);

                        if (!webresp.ContentType.StartsWith("application/json;"))
                            throw new Exception("FCM server returns invalid contenttype: " + webresp.ContentType);

                        using (Stream responseReader = webresp.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(responseReader, System.Text.Encoding.GetEncoding(webresp.CharacterSet)))
                            {
                                response = await reader.ReadToEndAsync();
                            }
                        }
                    }
                }

                JsonObject jsonResponse = (JsonObject)JsonReader.Parse(response);

                if (jsonResponse.GetValueInt("failure") != 0 || jsonResponse.GetValueInt("canonical_ids") != 0) {
                    var results = jsonResponse["results"];
                    if (results is JsonArray && ((JsonArray)results).Count == 1) {
                        var result = ((JsonArray)results)[0];
                        if (result is JsonObject) {
                            var error = ((JsonObject)result)["error"];

                            if (error is string) {
                                Service.Error((string)error == "NotRegistered" || (string)error == "MismatchSenderId"
                                                ? new PushNotificationInvalidDeviceException(notification)
                                                : new PushNotificationException(notification, "Submit notification to '" + notification.DeviceAddress + "' failed error '" + error + "'."));
                                return;
                            }
                        }
                    }

                    Service.Error(new PushNotificationException(notification, "Submit notification to '" + notification.DeviceAddress + "' failed error '" + response + "'."));
                }
            }
            catch(Exception err) {
                Service.Error(new PushNotificationException(notification, "Failed to submit notification to '" + notification.DeviceAddress + "'.", err));
            }
        }
    }
}
