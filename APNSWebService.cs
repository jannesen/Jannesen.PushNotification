using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Jannesen.FileFormat.Json;
using Jannesen.PushNotification.Library;

// https://developer.apple.com/documentation/usernotifications/sending-notification-requests-to-apns

namespace Jannesen.PushNotification
{
    public sealed class APNSWebService: PushWebService
    {
        public  readonly            APNSConfig                  Config;
        private readonly            HttpClientHandler           _httpClientHandler;
        private readonly            HttpClient                  _httpClient;

        public                                                  APNSWebService(APNSConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            Config             = config;
            _httpClientHandler = new HttpClientHandler() {
                                     AutomaticDecompression         = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                                     CheckCertificateRevocationList = true,
                                     AllowAutoRedirect              = false
                                 };
            _httpClientHandler.ClientCertificates.Add(config.ClientCertificate);
            _httpClient         = new HttpClient(_httpClientHandler) {
                                      Timeout = new TimeSpan(15 * TimeSpan.TicksPerSecond)
                                  };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }
        protected   override        void                        Dispose(bool disposing)
        {
            if (disposing) {
                _httpClient.Dispose();
                _httpClientHandler.Dispose();
            }
        }

        public  override            Task                        InitAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
        public  override async      Task                        SendNotificationAsync(PushMessage notification, CancellationToken ct)
        {
            var path    = $"/3/device/" + notification.DeviceToken;
            var payload = JsonWriter.Stringify(notification.Payload);
            var retry   = 0;
retry:
            using (var req = new HttpRequestMessage(HttpMethod.Post, Config.APNSServer + path)) {
                req.Version = new Version(2, 0);
                req.Headers.TryAddWithoutValidation(":method",    "POST");
                req.Headers.TryAddWithoutValidation(":path",      path);
                req.Headers.Add("apns-push-type",  "background");
                req.Headers.Add("apns-expiration", notification.ExpireTime.ToUnixTimeSeconds().ToString());
                req.Headers.Add("apns-priority",   "5");
                req.Headers.Add("apns-topic",      Config.BundleId);

                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using(var resp = await _httpClient.SendAsync(req, ct)) {
                    var body = await resp.Content.ReadAsByteArrayAsync(ct);

                    switch(resp.StatusCode) {
                    case HttpStatusCode.OK: // 200
                        return;

                    case HttpStatusCode.Gone:                   // 410
                        throw new PushNotificationException("Unknown device.", PushNotificationErrorReason.DeviceNotFound, notification);

                    //case HttpStatusCode.BadRequest:             // 400
                    //case HttpStatusCode.Forbidden:              // 403
                    //case HttpStatusCode.NotFound:               // 404
                    //case HttpStatusCode.RequestEntityTooLarge:  // 413
                    //case HttpStatusCode.TooManyRequests:        // 429

                    case HttpStatusCode.InternalServerError:    // 500
                    case HttpStatusCode.BadGateway:             // 502
                    case HttpStatusCode.ServiceUnavailable:     // 503
                        if (retry < 3) {
                            try {
                                await Task.Delay(5000 + (++retry * 2500), ct);
                                goto retry;
                            }
                            catch(Exception) {
                            }
                        }
                        break;
                    }

                    throw new PushNotificationException("ASPN.SendPushNotification failed: " + _getResponseErrorMessage(resp.StatusCode, body) + ".", PushNotificationErrorReason.ServiceError, notification);
                }
            }
        }

        private static              string                      _getResponseErrorMessage(HttpStatusCode statusCode, byte[] body)
        {
            var rtn = "status=" + statusCode;

            try {
                var sbody = Encoding.ASCII.GetString(body);
                var reason = (string?)null;

                try {
                    reason = (JsonReader.ParseString(sbody) as JsonObject)?.GetValueStringNullable("reason");
                }
                catch(Exception) {
                }

                if (reason != null) {
                    rtn += " reason=" + reason;
                }
                else {
                    rtn += " body=" + sbody;
                }
            }
            catch(Exception) {
            }

            return rtn;
        }

        public      override        string                      ToString()
        {
            return "PushNotifcation.APNSWebService";
        }
    }
}
