using System;
using System.Xml;

namespace Jannesen.PushNotification.Library
{
    static class XmlHelper
    {
        public  static      string      GetAttributeString(this XmlElement elm, string attrName)
        {
            string  s = elm.GetAttribute(attrName);

            if (string.IsNullOrEmpty(s))
                throw new FormatException("Missing attribute '" + attrName + "'.");

            return s;
        }
        public  static      bool        GetAttributeBool(this XmlElement elm, string attrName, bool defaultValue)
        {
            string  s = elm.GetAttribute(attrName);

            if (string.IsNullOrEmpty(s))
                return defaultValue;

            switch(s.ToLowerInvariant()) {
            case "0":
            case "false":
                return false;

            case "1":
            case "true":
                return true;

            default:
                throw new FormatException("Invalid bool value '" + s + "' in attribute '" + attrName + "'.");
            }
        }
        public  static      int         GetAttributeInt(this XmlElement elm, string attrName, int minValue, int maxValue)
        {
            string  s = elm.GetAttributeString(attrName);

            if (!int.TryParse(s, out var rtn))
                throw new FormatException("Invalid int value '" + s + "' in attribute '" + attrName + "'.");

            if (minValue > rtn || rtn > maxValue)
                throw new FormatException("Invalid int value '" + s + "' in attribute '" + attrName + "'.");

            return rtn;
        }
        public  static      int         GetAttributeInt(this XmlElement elm, string attrName, int minValue, int maxValue, int defaultValue)
        {
            string  s = elm.GetAttribute(attrName);

            if (string.IsNullOrEmpty(s))
                return defaultValue;

            if (!int.TryParse(s, out var rtn))
                throw new FormatException("Invalid int value '" + s + "' in attribute '" + attrName + "'.");

            if (minValue > rtn || rtn > maxValue)
                throw new FormatException("int value '" + s + "' in attribute '" + attrName + "' out of range.");

            return rtn;
        }
    }
}
