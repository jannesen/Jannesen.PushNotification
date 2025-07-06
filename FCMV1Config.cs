using System;
using System.Security.Cryptography;
using Jannesen.FileFormat.Json;

/*
 * Create config:
 *
 * get project_id, private_key_id, client_email and token_uri from firebase config file.
 *
 * get private_key from firebase config file split \n to lines and save file to 1.pem
 * openssl rsa -aes256 -in 1.pem -out 2.pem -passout pass:<passpharse>
 * open 2.pem in editor and join lines with \n to 1 line and add to config.
 *
 */

namespace Jannesen.PushNotification
{
    public sealed class FCMV1Config: IDisposable
    {
        public                  string                              ProjectId               { get; private set; }
        public                  string                              PrivateKeyId            { get; private set; }
        public                  RSA                                 PrivateKey              { get; private set; }
        public                  string                              ClientEmail             { get; private set; }
        public                  string                              TokenUri                { get; private set; }

        public                                                      FCMV1Config(JsonObject config, string? passphrase=null)
        {
            ArgumentNullException.ThrowIfNull(config);

            try {
                ProjectId    = config.GetValueString("project_id");
                PrivateKeyId = config.GetValueString("private_key_id");
                PrivateKey   = _loadPrivateKey(config.GetValueString("private_key"), passphrase);
                ClientEmail  = config.GetValueString("client_email");
                TokenUri     = config.GetValueString("token_uri");
            }
            catch(Exception err) {
                throw new PushNotificationConfigException("Parsing Android configuration failed.", err);
            }
        }
        public                  void                                Dispose()
        {
            PrivateKey?.Dispose();
        }

        public      override    string                              ToString()
        {
            return "PushNotifcationConfig.FCMConfig";
        }

        private     static      RSA                                 _loadPrivateKey(string key, string? passphrase)
        {
            var rsa = RSA.Create();
            if (passphrase != null) {
                rsa.ImportFromEncryptedPem(key, passphrase);
            }
            else {
                rsa.ImportFromPem(key);
            }

            return rsa;
        }
    }
}
