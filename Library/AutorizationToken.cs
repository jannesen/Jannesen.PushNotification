using System;

namespace Jannesen.PushNotification.Library
{
    public class AutorizationToken
    {
        public  readonly    string      Token;
        public  readonly    DateTime    Expires;

        public                          AutorizationToken(string token, DateTime expires)
        {
            Token   = token;
            Expires = expires;
        }
    }
}
