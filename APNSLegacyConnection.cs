using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA5398 // Avoid hardcoding SslProtocols 'Tls12' to ensure your application remains secure in the future. Use 'None' to let the Operating System choose a version.
#pragma warning disable CA2201 // Do not raise reserved exception types

// https://developer.apple.com/library/content/documentation/NetworkingInternet/Conceptual/RemoteNotificationsPG/BinaryProviderAPI.html#//apple_ref/doc/uid/TP40008194-CH13-SW1

namespace Jannesen.PushNotification
{
    internal sealed class APNSLegacyConnection: IDisposable
    {
        private readonly            TcpClient                   _tcpClient;
        private                     SslStream                   _sslStream;
        private readonly            object                      _lockObject;

        public                      bool                        Connected
        {
            get {
                return _tcpClient.Connected;
            }
        }

        public                                                  APNSLegacyConnection()
        {
            _tcpClient  = new TcpClient();
            _lockObject = new object();
        }
        public                      void                        Dispose()
        {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: DISPOSE");
#endif
            lock(_lockObject) {
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

        public              async   Task                        ConnectAsync(string hostname, int port, X509Certificate2 clientCertificate, int connectTimeout)
        {
            Exception       timeoutError = null;

            try {
                using (new Timer((object state) => {
                                     lock(_lockObject) {
                                         if (_tcpClient != null) {
                                             timeoutError = new TimeoutException("Timeout");
                                             Dispose();
                                         }
                                     }
                                 },
                                 null, connectTimeout, Timeout.Infinite))
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: CONNECT");
#endif
                    await _tcpClient.ConnectAsync(hostname, port);

                    lock(_lockObject) {
                        _sslStream = new SslStream(_tcpClient.GetStream(),
                                                   false,
                                                   (sender, certificate, chain, policyErrors)                                    => (policyErrors == SslPolicyErrors.None),
                                                   (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => (clientCertificate) );
                    }

                    try {
                        await _sslStream.AuthenticateAsClientAsync(hostname, new X509CertificateCollection { clientCertificate }, System.Security.Authentication.SslProtocols.Tls12, false);
                    }
                    catch (System.Security.Authentication.AuthenticationException err2) {
                        throw new Exception("SSL Stream Failed to Authenticate as Client", err2);
                    }

                    if (!_sslStream.IsMutuallyAuthenticated)
                        throw new Exception("SSL Stream Failed to Authenticate");

                    if (!_sslStream.CanWrite)
                        throw new Exception("SSL Stream is not Writable");
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: CONNECTED");
#endif
                }
            }
            catch(Exception err) {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: ERROR " + err.Message);
#endif
                Dispose();

                if (err is ObjectDisposedException || err is NullReferenceException)
                    err = timeoutError ?? new Exception("ConnectAsync aborted.");

                throw err;
            }

            if (timeoutError != null)
                throw timeoutError;
        }
        public              async   Task<byte[]>                ReceiveAsync(int length, bool allowEof)
        {
            byte[]  msg = new byte[length];
            int     sz  = 0;

            do {
                int r = await _sslStream.ReadAsync(new Memory<byte>(msg, sz, msg.Length - sz));

                if (r <= 0)
                    break;

                sz += r;
            }
            while (sz < length);

#if DEBUG
            System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: RECV " + _hexDump(msg, length));
#endif

            if (sz == length)
                return msg;
            if (sz == 0 && allowEof)
                return null;

            throw new SystemException("Incomplete data received.");
        }
        public                      Task                        SendAsync(byte[] msg, int length)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("APNSLegacyConnection: SEND " + _hexDump(msg, length));
#endif
            return _sslStream.WriteAsync(msg, 0, length);
        }

#if DEBUG
        private static readonly     char[]                      _hexTable = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
        private static              string                      _hexDump(byte[] msg, int length)
        {
            var s = new StringBuilder();

            for (int i = 0 ; i < length ; ++i) {
                var b = msg[i];

                if (i > 0) {
                    s.Append(' ');
                }

                s.Append(_hexTable[(b >> 4) & 0xf]);
                s.Append(_hexTable[(b     ) & 0xf]);
            }

            return s.ToString();
        }
#endif
    }
}
