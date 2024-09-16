using System;
using Jannesen.FileFormat.Json;

namespace Jannesen.PushNotification
{
    public class FCMV1Config
    {
        public                  string                              ProjectId               { get; private set; }
        public                  string                              PrivateKeyId            { get; private set; }
        public                  string                              PrivateKey              { get; private set; }
        public                  string                              ClientEmail             { get; private set; }
        public                  string                              TokenUri                { get; private set; }

        public                                                      FCMV1Config(JsonObject config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            try {
                ProjectId    = config.GetValueString("project_id");
                PrivateKeyId = config.GetValueString("private_key_id");
                PrivateKey   = config.GetValueString("private_key");
                ClientEmail  = config.GetValueString("client_email");
                TokenUri     = config.GetValueString("token_uri");
            }
            catch(Exception err) {
                throw new PushNotificationConfigException("Parsing Android configuration failed.", err);
            }
        }

        public      override    string                              ToString()
        {
            return "PushNotifcation.Android";
        }
    }
}
