using System;
using System.Configuration;
using System.Xml;

namespace Jannesen.PushNotification.Internal
{
    static class XmlHelper
    {
        public  static      string      GetAttributeString(this XmlElement elm, string attrName)
        {
            string  s = elm.GetAttribute(attrName);

            if (string.IsNullOrEmpty(s))
                throw new ConfigurationErrorsException("Missing attribute '" + attrName + "'.", elm);

            return s;
        }
        public  static      bool        GetAttributeBool(this XmlElement elm, string attrName, bool defaultValue)
        {
            string  s = elm.GetAttribute(attrName);

            if (string.IsNullOrEmpty(s))
                return defaultValue;

            switch(s.ToLower()) {
            case "0":
            case "false":
                return false;

            case "1":
            case "true":
                return true;

            default:
                throw new ConfigurationErrorsException("Invalid bool value '" + s + "' in attribute '" + attrName + "'.", elm);
            }
        }
        public  static      int         GetAttributeInt(this XmlElement elm, string attrName, int minValue, int maxValue)
        {
            string  s = elm.GetAttributeString(attrName);
            int     rtn;

            if (!int.TryParse(s, out rtn))
                throw new ConfigurationErrorsException("Invalid int value '" + s + "' in attribute '" + attrName + "'.", elm);

            if (minValue > rtn || rtn > maxValue)
                throw new ConfigurationErrorsException("Invalid int value '" + s + "' in attribute '" + attrName + "'.", elm);

            return rtn;
        }
        public  static      int         GetAttributeInt(this XmlElement elm, string attrName, int minValue, int maxValue, int defaultValue)
        {
            string  s = elm.GetAttribute(attrName);
            int     rtn;

            if (string.IsNullOrEmpty(s))
                return defaultValue;

            if (!int.TryParse(s, out rtn))
                throw new ConfigurationErrorsException("Invalid int value '" + s + "' in attribute '" + attrName + "'.", elm);

            if (minValue > rtn || rtn > maxValue)
                throw new ConfigurationErrorsException("int value '" + s + "' in attribute '" + attrName + "' out of range.", elm);

            return rtn;
        }
    }
}
