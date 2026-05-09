using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CLS_II
{
    class RegexMatch
    {
        public static bool isFloatingNum(string value)
        {
            return Regex.IsMatch(value, @"^[-]?[0-9]+(\.[0-9]+)?$");
        }

        public static bool isInteger(string value)
        {
            return Regex.IsMatch(value, @"^[-]?[0-9]+$");
        }

        public static bool isPositiveInteger(string value)
        {
            return Regex.IsMatch(value, @"^[0-9]+$");
        }

        public static bool isIP(string value)
        {
            return Regex.IsMatch(value, @"^(\d{1,3}\.){3}\d{1,3}$");
        }

        public static bool isAmsNetID(string value)
        {
            return Regex.IsMatch(value, @"^(\d{1,3}\.){3}\d{1,3}\.1\.1$");
        }

        public static bool isSameName(string value1, string value2)
        {
            if (value2 == value1 || Regex.IsMatch(value2, @"^" + value1 + @"\(\d+\)$"))
                return true;
            else
                return false;
        }

        public static string StringDeleteBlank(string value)
        {
            return Regex.Replace(value, @"\s", "");
        }
    }
}
