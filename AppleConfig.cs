using System;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jannesen.PushNotification.Internal;

namespace Jannesen.PushNotification
{
    public class AppleConfig: ServiceConfig
    {
        public                      bool                                Development                 { get; private set; }
        public                      X509Certificate2                    ClientCertificate           { get; private set; }
        public                      int                                 FeedbackInterval            { get; private set; }
        public                      int                                 RecyleCount                 { get; private set; }
        public                      int                                 RecyleTimout                { get; private set; }

        public                                                          AppleConfig(bool development, string certificateFilename, string certificatePasswd, int feedbackInterval = (6*60*60*1000), int recyleCount=128, int recyleTimout=5000)
        {
            Development = development;

            try {
                ClientCertificate = string.IsNullOrEmpty(certificatePasswd)? new X509Certificate2(certificateFilename): new X509Certificate2(certificateFilename, certificatePasswd);

                if (!ClientCertificate.HasPrivateKey)
                    throw new InvalidOperationException("Private key missing.");

                if (ClientCertificate.NotBefore > DateTime.UtcNow)
                    throw new InvalidOperationException("Certificate is not valid.");

                if (ClientCertificate.NotAfter < DateTime.UtcNow)
                    throw new InvalidOperationException("Certificate is expired.");
            }
            catch(Exception err) {
                throw new PushNotificationConfigException("Failed to load client certificate '" + certificateFilename + "'.", err);
            }

            FeedbackInterval = feedbackInterval;
            RecyleCount      = recyleCount;
            RecyleTimout     = recyleTimout;
        }
        public                                                          AppleConfig(bool development, byte[] certificateData, string certificatePasswd, int feedbackInterval = (6*60*60*1000), int recyleCount=128, int recyleTimout=5000)
        {
            Development = development;

            try {
                ClientCertificate = string.IsNullOrEmpty(certificatePasswd)? new X509Certificate2(certificateData): new X509Certificate2(certificateData, certificatePasswd);

                if (!ClientCertificate.HasPrivateKey)
                    throw new InvalidOperationException("Private key missing.");

                if (ClientCertificate.NotBefore > DateTime.UtcNow)
                    throw new InvalidOperationException("Certificate is not valid.");

                if (ClientCertificate.NotAfter < DateTime.UtcNow)
                    throw new InvalidOperationException("Certificate is expired.");
            }
            catch(Exception err) {
                throw new PushNotificationConfigException("Failed to load client certificate.", err);
            }

            FeedbackInterval = feedbackInterval;
            RecyleCount      = recyleCount;
            RecyleTimout     = recyleTimout;
        }
        public                                                          AppleConfig(XmlElement config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            try {
                Development      = config.GetAttributeBool("development", false);
                ClientCertificate = _loadCertificate(config.GetAttributeString("certificate"));
                FeedbackInterval = config.GetAttributeInt("feedback-interval", 0,   24) * 3600000;
                RecyleCount      = config.GetAttributeInt("recyle-count", 1, 1024, 128);
                RecyleTimout     = config.GetAttributeInt("recyle-timout", 1,   30,   5) * 1000;
            }
            catch(Exception err) {
                throw new ConfigurationErrorsException("Parsing Apple configuration failed.", err);
            }
        }

        internal    override async  Task<Internal.ServiceConnection>    GetNewConnection(PushService service)
        {
            try {
                var connection = new Internal.APNSPushConnection(service, this);

                await connection.ConnectAsync();

                return  connection;
            }
            catch(Exception err) {
                throw new PushNotificationConnectionException("Appl.GetNewConnection failed.", err);
            }
        }

        public                      PushService                         PushService()
        {
            return new PushService(this);
        }
        public               async  Task<List<AppleFeedback>>           Feedback()
        {
            Task<List<AppleFeedback>>   readTask;

            using (var connection = new Internal.APNSConnection()) {
                await connection.Connect(Development ? "feedback.sandbox.push.apple.com" : "feedback.push.apple.com", 2196, ClientCertificate, 30 * 1000);

                readTask = _feedbackReadResponse(connection);

                using (CancellationTokenSource cts = new CancellationTokenSource(15000))
                    await Task.WhenAny(readTask, Task.Delay(-1, cts.Token));
            }

            await readTask;

            return readTask.Result;
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
                    throw new ConfigurationErrorsException("Failed to open X509Store", err);
                }

                foreach (var cert in store.Certificates) {
                    if (cert.NotBefore < DateTime.UtcNow && cert.NotAfter > DateTime.UtcNow.AddHours(-12) && cert.Subject == certificateName) {
                        if (foundCert == null || foundCert.NotAfter < cert.NotAfter) {
                            if (!cert.HasPrivateKey)
                                throw new ConfigurationErrorsException("Certificate '"+ certificateName + "' has no private key.");

                            foundCert = cert;
                        }
                    }
                }
            }

            if (foundCert != null)
                return foundCert;

            throw new ConfigurationErrorsException("Certificate '" + certificateName + "' not found in store.");
        }
        private     static  async   Task<List<AppleFeedback>>           _feedbackReadResponse(Internal.APNSConnection connection)
        {
            var rtn = new List<AppleFeedback>();

            byte[]      msg;

            while ((msg = await connection.Receive(6, true)) != null) {
                var timestamp   = (msg[0] << 24) | (msg[1] << 16) | (msg[2] << 8) | (msg[3]);
                var deviceToken = await connection.Receive((msg[4] << 8) | (msg[5]), false);

                rtn.Add(new AppleFeedback(timestamp, deviceToken));
            }

            return rtn;
        }
    }
}
