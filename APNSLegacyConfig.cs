using System;
using System.Security.Cryptography.X509Certificates;
using Jannesen.FileFormat.Json;
using Jannesen.PushNotification.Library;

namespace Jannesen.PushNotification
{
    public sealed class APNSLegacyConfig: IDisposable
    {
        public                      bool                                Development                 { get; private set; }
        public                      X509Certificate2                    ClientCertificate           { get; private set; }
        public                      int                                 FeedbackInterval            { get; private set; }
        public                      int                                 RecyleCount                 { get; private set; }
        public                      int                                 RecyleTimout                { get; private set; }

        public                                                          APNSLegacyConfig(JsonObject config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            try {
                Development      = config.GetValueBoolean("development", false);
                ClientCertificate = _loadCertificate(config.GetValueString("certificate"));
                FeedbackInterval = config.GetValueInt("feedback-interval", 0,   24,   0) * 3600000;
                RecyleCount      = config.GetValueInt("recyle-count",      1, 1024, 128);
                RecyleTimout     = config.GetValueInt("recyle-timout",     1,   30,   5) * 1000;
            }
            catch(Exception err) {
                throw new PushNotificationConfigException("Parsing Apple configuration failed.", err);
            }
        }
        public                      void                                Dispose()
        {
            ClientCertificate?.Dispose();
        }

        public      override        string                              ToString()
        {
            return "PushNotifcation.Apple";
        }

        private     static          X509Certificate2                    _loadCertificate(string certificateName)
        {
            X509Certificate2 foundCert = null;

            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine)) {
                try {
                    store.Open(OpenFlags.ReadOnly);
                }
                catch(Exception err) {
                    throw new PushNotificationConfigException("Failed to open X509Store", err);
                }

                foreach (var cert in store.Certificates) {
                    if (cert.NotBefore < DateTime.UtcNow && cert.NotAfter > DateTime.UtcNow.AddHours(-12) && cert.Subject == certificateName) {
                        if (foundCert == null || foundCert.NotAfter < cert.NotAfter) {
                            if (!cert.HasPrivateKey)
                                throw new PushNotificationConfigException("Certificate '"+ certificateName + "' has no private key.");

                            try {
                                var _ = cert.PrivateKey;
                            }
                            catch(Exception) {
                                throw new PushNotificationConfigException("Certificate '"+ certificateName + "' no read-access to private key.");
                            }

                            foundCert = cert;
                        }
                    }
                }
            }

            if (foundCert != null)
                return foundCert;

            throw new PushNotificationConfigException("Certificate '" + certificateName + "' not found in store.");
        }
    }
}
