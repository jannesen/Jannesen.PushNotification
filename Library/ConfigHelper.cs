using System;
using Jannesen.FileFormat.Json;

namespace Jannesen.PushNotification.Library
{
    static class ConfigHelper
    {
        public  static      bool        GetValueBoolean(this JsonObject json, string propertyName, bool defaultValue)
        {
            return json.GetValueBooleanNullable(propertyName) ?? defaultValue;
        }
        public  static      int         GetValueInt(this JsonObject json, string propertyName, int minValue, int maxValue)
        {
            var  rtn = json.GetValueInt(propertyName);

            if (minValue > rtn || rtn > maxValue)
                throw new FormatException("Invalid int value '" + rtn + "' in property '" + propertyName + "'.");

            return rtn;
        }
        public  static      int         GetValueInt(this JsonObject json, string propertyName, int minValue, int maxValue, int defaultValue)
        {
            var  rtn = json.GetValueIntNullable(propertyName) ?? defaultValue;

            if (minValue > rtn || rtn > maxValue)
                throw new FormatException("Invalid int value '" + rtn + "' in property '" + propertyName + "'.");

            return rtn;
        }
    }
}
