using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Xml;
using Jannesen.PushNotification.Internal;

namespace Jannesen.PushNotification
{
    public class AndroidConfig: ServiceConfig
    {
        public                  string                              SenderId                { get; private set; }
        public                  string                              AuthorizationKey        { get; private set; }

        public                                                      AndroidConfig(string senderId, string authorizationKey)
        {
            SenderId         = SenderId;
            AuthorizationKey = authorizationKey;
        }
        public                                                      AndroidConfig(XmlElement config)
        {
            try {
                SenderId         = config.GetAttributeString("senderid");
                AuthorizationKey = config.GetAttributeString("authorization-key");
            }
            catch(Exception err) {
                throw new ConfigurationErrorsException("Parsing Android configuration failed.", err);
            }
        }

        internal    override    Task<Internal.ServiceConnection>    GetNewConnection(PushService service)
        {
            return Task.FromResult<Internal.ServiceConnection>(new Internal.FirebaseConnection(service, this));
        }

        public      override    string                              ToString()
        {
            return "PushNotifcation.Android";
        }
    }
}
