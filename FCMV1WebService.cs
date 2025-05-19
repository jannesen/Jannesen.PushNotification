using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jannesen.FileFormat.Json;
using Jannesen.PushNotification.Library;

namespace Jannesen.PushNotification
{
    public sealed class FCMV1WebService: PushWebService
    {
        private struct JWTPayload: IJsonSerializer
        {

            public          string[]        scopes;
            public          bool            email_verified;
            public          string          iss;
            public          string          aud;
            public          int             exp;
            public          int             iat;

                readonly    void            IJsonSerializer.WriteTo(JsonWriter jsonWriter)
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteNameValue("scope",              String.Join(" ", scopes));
                jsonWriter.WriteNameValue("email_verified",     email_verified);
                jsonWriter.WriteNameValue("iss",                iss);
                jsonWriter.WriteNameValue("aud",                aud);
                jsonWriter.WriteNameValue("exp",                exp);
                jsonWriter.WriteNameValue("iat",                iat);
                jsonWriter.WriteEndObject();
            }
        }

        private static  readonly    string[]                    JWTScopes = [
                                                                                "https://www.googleapis.com/auth/firebase",
                                                                                //"https://www.googleapis.com/auth/userinfo.email",
                                                                                //"https://www.googleapis.com/auth/identitytoolkit",
                                                                                //"https://www.googleapis.com/auth/devstorage.full_control",
                                                                                "https://www.googleapis.com/auth/cloud-platform",
                                                                                //"https://www.googleapis.com/auth/datastore"
                                                                            ];
        public  readonly            FCMV1Config                 Config;
        private readonly            HttpClientHandler           _httpClientHandler;
        private readonly            HttpClient                  _httpClient;
        private readonly            JWTEncoder                  _jwtEncoder;
        private                     Task<AutorizationToken>?    _autorization;
        private readonly            object                      _lockObject;

        public                                                  FCMV1WebService(FCMV1Config config)
        {
            ArgumentNullException.ThrowIfNull(config);

            Config             = config;
            _jwtEncoder        = new JWTEncoder(config.PrivateKeyId, config.PrivateKey);
            _httpClientHandler = new HttpClientHandler() {
                                     AutomaticDecompression         = DecompressionMethods.GZip,
                                     CheckCertificateRevocationList = true,
                                     AllowAutoRedirect              = false
                                 };
            _httpClient          = new HttpClient(_httpClientHandler) {
                                       Timeout = new TimeSpan(15 * TimeSpan.TicksPerSecond)
                                   };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            _lockObject = new object();
        }
        protected   override        void                        Dispose(bool disposing)
        {
            if (disposing) {
                _httpClient.Dispose();
                _httpClientHandler.Dispose();
            }
        }

        public  override async      Task                        InitAsync(CancellationToken ct)
        {
            await GetAuthorizationTokenAsync(ct);
        }
        public  override async      Task                        SendNotificationAsync(PushMessage notification, CancellationToken ct)
        {
            var retry = 0;
retry:
            var spm = _formatMessage(notification);
            var at  = (await GetAuthorizationTokenAsync(ct)).Token;

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post,
                                                                   "https://fcm.googleapis.com/v1/projects/" + Config.ProjectId + "/messages:send")) {
                httpRequestMessage.Headers.Add("Authorization", at);
                httpRequestMessage.Content = new StringContent(spm, Encoding.UTF8, "application/json");

                using(var httpResponse = await _httpClient.SendAsync(httpRequestMessage, ct)) {
                    var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);

                    var sjson = _getBodyJson(httpResponse, body);

                    if ((httpResponse.StatusCode == HttpStatusCode.OK || httpResponse.StatusCode == HttpStatusCode.NotFound) && sjson != null) {
                        try {
                            if (JsonReader.ParseString(sjson) is JsonObject jsonObject) {
                                switch(httpResponse.StatusCode) {
                                case HttpStatusCode.OK:
                                    return;

                                case HttpStatusCode.NotFound:
                                    if (jsonObject.GetValueObject("error")?.GetValueString("code") == "404") {
                                        throw new PushNotificationException("Unknown device.", PushNotificationErrorReason.DeviceNotFound, notification);
                                    }
                                    break;
                                }
                            }
                        }
                        catch(PushNotificationException) {
                            throw;
                        }
                        catch(Exception) {
                        }
                    }

                    if (httpResponse.StatusCode == HttpStatusCode.InternalServerError ||
                        httpResponse.StatusCode == HttpStatusCode.BadGateway          ||
                        httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable) {
                        if (retry < 3) {
                            try {
                                await Task.Delay(5000 + (++retry * 2500), ct);
                                goto retry;
                            }
                            catch(Exception) {
                            }
                        }
                    }

                    throw new PushNotificationException("FirebaseV1.SendPushNotification failed: " + _getResponseErrorMessage(httpResponse.StatusCode, sjson) + ".", PushNotificationErrorReason.ServiceError, notification);
                }
            }
        }

        public                      Task<AutorizationToken>     GetAuthorizationTokenAsync(CancellationToken ct)
        {
            lock (_lockObject) {
                if (_autorization != null && _autorization.IsCompleted && _autorization.Result.Expires < DateTime.UtcNow.AddMinutes(5)) {
                    _autorization = null;
                }

                if (_autorization == null) {
                    _autorization = _getAuthorizationAsync(ct);
                }
            }

            return _autorization;
        }

        private          async      Task<AutorizationToken>     _getAuthorizationAsync(CancellationToken ct)
        {
            await Task.Yield(); // Release GetAuthorizationTokenAsync lock

            var retry = 0;
retry:
            var dt = (int)((DateTime.UtcNow - StaticLib.UnixEPoch).Ticks / TimeSpan.TicksPerSecond);
            var assertion = _jwtEncoder.CreateJwtToken(new JWTPayload() {
                                                           scopes           = JWTScopes,
                                                           email_verified   = false,
                                                           iss              = Config.ClientEmail,
                                                           aud              = Config.TokenUri,
                                                           exp              = dt + 3600,
                                                           iat              = dt
                                                      });

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, Config.TokenUri)) {
                  httpRequestMessage.Content = new StringContent("assertion=" + assertion
                                                              + "&grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer",
                                                                 Encoding.ASCII, "application/x-www-form-urlencoded");
                using(var httpResponse = await _httpClient.SendAsync(httpRequestMessage, ct)) {
                    var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);       // oops can't cancel sorry

                    var sjson = _getBodyJson(httpResponse, body);

                    if (httpResponse.StatusCode == HttpStatusCode.OK && sjson != null) {
                        try {
                            if (JsonReader.ParseString(sjson) is JsonObject jsonObject) {
                                var token_type = jsonObject.GetValueString("token_type");

                                if (token_type == "Bearer") {
                                    return new AutorizationToken(token_type + " " + jsonObject.GetValueString("access_token"),
                                                                    StaticLib.UnixEPoch.AddTicks((dt + jsonObject.GetValueInt("expires_in")) * TimeSpan.TicksPerSecond));
                                }
                            }
                        }
                        catch(Exception) {
                        }
                    }

                    if (httpResponse.StatusCode == HttpStatusCode.BadGateway ||
                        httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable) {
                        if (retry < 3) {
                            try {
                                await Task.Delay(5000 + (++retry * 2500), ct);
                                goto retry;
                            }
                            catch(Exception) {
                            }
                        }
                    }

                    throw new PushNotificationAuthenticationException("FirebaseV1.GetAccessToken failed: " + _getResponseErrorMessage(httpResponse.StatusCode, sjson) + ".");
                }
            }
        }
        private static              string                      _formatMessage(PushMessage notification)
        {
            var ttl = (notification.ExpireTime.Ticks - DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond/2) / TimeSpan.TicksPerSecond;

            if (ttl < (notification.HighPriority ? 2 : 120)) {
                throw new PushNotificationException("PushMessage expired.", PushNotificationErrorReason.MessageExpired, notification);
            }

            using (var text = new StringWriter()) {
                using (var jsonWriter = new JsonWriter(text, true, true)) {
                    jsonWriter.WriteStartObject();
                        jsonWriter.WriteStartObject("message");
                            jsonWriter.WriteNameValue("token", notification.DeviceToken);
                            jsonWriter.WriteStartObject("data");

                                foreach (var p in notification.Payload) {
                                    if (p.Value != null) {
                                        jsonWriter.WriteNameValue(p.Key, _formatData(p.Value) ?? throw new PushNotificationException("Can't serialize " + p.Value.GetType().FullName + ".", PushNotificationErrorReason.Unknown, notification));
                                    }
                                }

                            jsonWriter.WriteEndObject();

                            jsonWriter.WriteStartObject("android");
                                jsonWriter.WriteNameValue("priority", notification.HighPriority ? "high" : "normal");
                                jsonWriter.WriteNameValue("ttl",     ttl.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s");
                            jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndObject();
                        jsonWriter.WriteNameValue("validate_only", false);
                    jsonWriter.WriteEndObject();
                }
                return text.ToString();
            }
        }
        private static              string?                     _formatData(object value)
        {
            if (value is string valueString) {
                return valueString;
            }

            if (value is IJsonSerializer iserialize) {
                using (var text = new StringWriter()) {
                    using (var jsonWriter = new JsonWriter(text, true, true)) {
                        iserialize.WriteTo(jsonWriter);
                    }
                    return text.ToString();
                }
            }

            return null;
        }
        private static              string?                     _getBodyJson(HttpResponseMessage httpResponse, byte[] body)
        {
            try {
                if (httpResponse.Content.Headers.ContentType?.MediaType == "application/json") {
                    return _charsetEncoding(httpResponse.Content.Headers.ContentType.CharSet).GetString(body);
                }
            }
            catch(Exception) {
            }

            return null;
        }
        private static              Encoding                    _charsetEncoding(string? charset)
        {
            switch(charset?.ToLowerInvariant()) {
            case "utf8":    return Encoding.UTF8;
            case null:      return Encoding.ASCII;
            default:        return Encoding.GetEncoding(charset);
        }
    }
        private static              string                      _getResponseErrorMessage(HttpStatusCode statusCode, string? sjson)
        {
            var rtn = "status=" + statusCode;

            try {
                if (sjson != null) {
                    var o = JsonReader.ParseString(sjson);
                    if (o is JsonObject jsonObject) {
                        var error = jsonObject.GetValueObject("error");
                        if (error != null) {
                            var message = error.GetValueStringNullable("message");
                            if (message != null)
                                return rtn + " message=" + message;
                        }

                        var error_description = jsonObject.GetValueStringNullable("error_description");
                        if (error_description != null)
                            return rtn + " error_description=" + error_description;
                    }
                }
            }
            catch(Exception) {
            }

            return rtn + " body='" + sjson + "'";
        }

        public      override        string                      ToString()
        {
            return "PushNotifcation.FCMV1WebService";
        }
    }
}
