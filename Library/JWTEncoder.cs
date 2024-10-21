using System;
using System.Security.Cryptography;
using System.Text;
using Jannesen.FileFormat.Json;

namespace Jannesen.PushNotification.Library
{
    internal sealed class JWTEncoder: IDisposable
    {
        private struct JwtHeader: IJsonSerializer
        {
            public          string      alg;
            public          string      kid;

                readonly    void        IJsonSerializer.WriteTo(JsonWriter jsonWriter)
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WriteNameValue("alg", alg);
                jsonWriter.WriteNameValue("kid", kid);
                jsonWriter.WriteNameValue("typ", "JWT");
                jsonWriter.WriteEndObject();
            }
        }

        private readonly        string                      _keyid;
        private readonly        RSA                         _key;

        public                                              JWTEncoder(string keyid, string pkcs8PrivateKey)
        {
            _keyid = keyid;
            _key   = RSA.Create(Pkcs8.DecodeRsaParameters(pkcs8PrivateKey));
        }
        public                  void                        Dispose()
        {
            _key.Dispose();
        }

        public                  string                      CreateJwtToken(IJsonSerializer payload)
        {
            var jwt = _jwtEncode(JsonWriter.Serialize(new JwtHeader() { alg = "RS256", kid = _keyid} ))
                    + "." + _jwtEncode(JsonWriter.Serialize(payload));

            return jwt
                 + "." + _jwtEncode(_getJwtSignatute(Encoding.ASCII.GetBytes(jwt)));
        }

        private                 byte[]                      _getJwtSignatute(byte[] data)
        {
            return _key.SignHash(SHA256.HashData(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        private     static      string                      _jwtEncode(string data)
        {
            return _jwtEncode(Encoding.UTF8.GetBytes(data));
        }
        private     static      string                      _jwtEncode(byte[] data)
        {
            return Convert.ToBase64String(data).Replace("=", String.Empty).Replace('+', '-').Replace('/', '_');
        }
    }
}
