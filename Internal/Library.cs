using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jannesen.PushNotification.Internal
{
    static class Library
    {
        public      static          DateTime                    UnixEPoch   = new DateTime(1970, 1, 1, 0, 0, 0);

        public      static          string                      HexDump(byte[] msg, int len)
        {
            StringBuilder       rtn = new StringBuilder(msg.Length * 2);

            for (int i = 0 ; i < len ; ++i) {
                rtn.Append(NibbleToHex((msg[i] >> 4) & 0xF));
                rtn.Append(NibbleToHex(msg[i] & 0xF));
            }

            return rtn.ToString();
        }
        public      static          char                        NibbleToHex(int v)
        {
            return (char)((v < 10) ? ('0'+v) : ('A'-10)+v);
        }
        public      static          int                         HexToNibble(char c)
        {
            if ('0' <= c  && c <= '9')      return c - '0';
            if ('A' <= c  && c <= 'F')      return c - ('A'-10);
            if ('a' <= c  && c <= 'f')      return c - ('a'-10);

            throw new FormatException("Invalid char in hex string.");
        }
    }
}
