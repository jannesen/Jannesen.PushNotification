using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jannesen.FileFormat.Json;

// https://developer.apple.com/library/content/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/BinaryProviderAPI.html#//apple_ref/doc/uid/TP40008194-CH13-SW1

namespace Jannesen.PushNotification.Internal
{
    internal sealed class APNSConnection: IDisposable
    {
        private                     string                      _name;
        private                     TcpClient                   _tcpClient;
        private                     SslStream                   _sslStream;
        private                     object                      _lockObject;

        public                      bool                        Connected
        {
            get {
                return _tcpClient.Connected;
            }
        }

        public                                                  APNSConnection()
        {
            _tcpClient  = new TcpClient();
            _lockObject = new object();
        }
        public                      void                        Dispose()
        {
            lock(_lockObject)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(_name + ": Dispose");
#endif
                try {
                    if (_sslStream != null)
                        _sslStream.Dispose();

                    if (_tcpClient != null)
                        _tcpClient.Dispose();
                }
                catch(Exception) {
                }
            }
        }

        public              async   Task                        Connect(string hostname, int port, X509Certificate2 clientCertificate, int connectTimeout)
        {
            _name = hostname + ":" + port.ToString();

            Exception       timeoutError = null;

            try {
                using (new Timer((object state) =>
                                    {
                                        lock(_lockObject)
                                        {
                                            if (_tcpClient != null) {
                                                timeoutError = new TimeoutException("Timeout");
                                                Dispose();
                                            }
                                        }
                                    },
                                    null, connectTimeout, Timeout.Infinite))
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(_name + ": Connecting");
#endif
                    await _tcpClient.ConnectAsync(hostname, port);

                    lock(_lockObject)
                    {
                        _sslStream = new SslStream(_tcpClient.GetStream(),
                                                   false,
                                                   (sender, certificate, chain, policyErrors)                                    => (policyErrors == SslPolicyErrors.None),
                                                   (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => (clientCertificate) );
                    }

                    try {
                        await _sslStream.AuthenticateAsClientAsync(hostname, new X509CertificateCollection { clientCertificate }, System.Security.Authentication.SslProtocols.Tls, false);
                    }
                    catch (System.Security.Authentication.AuthenticationException err2) {
                        throw new Exception("SSL Stream Failed to Authenticate as Client", err2);
                    }

                    if (!_sslStream.IsMutuallyAuthenticated)
                        throw new Exception("SSL Stream Failed to Authenticate");

                    if (!_sslStream.CanWrite)
                        throw new Exception("SSL Stream is not Writable");
                }
            }
            catch(Exception err) {
                Dispose();

                if (err is ObjectDisposedException || err is NullReferenceException)
                    err = (timeoutError != null) ? timeoutError : new Exception("Connect aborted.");

#if DEBUG
                System.Diagnostics.Debug.WriteLine(_name + ": Connect failed " + err.Message);
#endif
                throw err;
            }

            if (timeoutError != null)
                throw timeoutError;

#if DEBUG
            System.Diagnostics.Debug.WriteLine(_name + ": Connected");
#endif
        }
        public              async   Task<byte[]>                Receive(int length, bool allowEof)
        {
            byte[]  msg = new byte[length];
            int     sz  = 0;

            do {
                int r = await _sslStream.ReadAsync(msg, sz, msg.Length - sz);

                if (r <= 0)
                    break;

                sz += r;
            }
            while (sz < length);

#if DEBUG
            System.Diagnostics.Debug.WriteLine(_name + ": RECV " + (sz > 0 ? Library.HexDump(msg, sz) : "EOF"));
#endif

            if (sz == length)
                return msg;
            if (sz == 0 && allowEof)
                return null;

            throw new SystemException("Incomplete data received.");
        }
        public                      Task                        Send(byte[] msg, int length)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(_name + ": SEND " + Library.HexDump(msg, length));
#endif
            return _sslStream.WriteAsync(msg, 0, length);
        }
    }
}
