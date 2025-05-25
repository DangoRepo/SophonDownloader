using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    internal class Utils
    {
        private static readonly string[] SizeSuffixes = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string FormatSize(double value, int decimalPlaces = 2)
        {
            byte mag = (byte)Math.Log(value, 1000);

            return $"{Math.Round(value / (1L << (mag * 10)), decimalPlaces)} {SizeSuffixes[mag]}";
        }
    }
}
