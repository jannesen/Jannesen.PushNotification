﻿using System;
using System.Security.Cryptography.X509Certificates;
using Jannesen.FileFormat.Json;
using Jannesen.PushNotification.Library;

/*
 * Convert apple client pkcs to key cert
 * 
 * openssl pkcs12 -passin pass:<pkcs passphare> -passout pass:<passphare>  -in <pfxfile> -out 1.pem
 * 
 * open pem file in editor.
 *  - remove all lines outside -----BEGIN en ---END
 *  - join BEGIN ENCRYPTED PRIVATE KEY lines with \n and place in key
 *  - join BEGIN CERTIFICATE lines with \n and place in cert
 */
namespace Jannesen.PushNotification
{
    public sealed class APNSLegacyConfig: IDisposable
    {
        public                      bool                                Development                 { get; private set; }
        public                      X509Certificate2                    ClientCertificate           { get; private set; }
        public                      int                                 FeedbackInterval            { get; private set; }
        public                      int                                 RecyleCount                 { get; private set; }
        public                      int                                 RecyleTimout                { get; private set; }

        public                                                          APNSLegacyConfig(JsonObject config, string? passphrase=null)
        {
            ArgumentNullException.ThrowIfNull(config);

            try {
                Development       = config.GetValueBoolean("development", false);
                ClientCertificate =  _loadCertificate(config.GetValueObjectRequired("client-certificate"), passphrase);
                RecyleCount       = config.GetValueInt("recyle-count",      1, 1024, 128);
                RecyleTimout      = config.GetValueInt("recyle-timout",     1,   30,   5) * 1000;
            }
            catch(Exception err) {
                ClientCertificate?.Dispose();
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

        private     static          X509Certificate2                    _loadCertificate(JsonObject clientCertificate, string? passphrase)
        {
            using (var cert = passphrase != null
                                ? X509Certificate2.CreateFromEncryptedPem(clientCertificate.GetValueString("cert"), clientCertificate.GetValueString("key"), passphrase)
                                : X509Certificate2.CreateFromPem(clientCertificate.GetValueString("cert"), clientCertificate.GetValueString("key"))) {
            // Work around "ephemeral keys" error
            // https://stackoverflow.com/questions/72096812/loading-x509certificate2-from-pem-file-results-in-no-credentials-are-available
                return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12), null);
            }
        }
    }
}
