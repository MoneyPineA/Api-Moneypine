using System;

namespace ApiEjemplo.Helpers
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo MexicoZone =
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)");

        public static DateTime GetMexicoTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MexicoZone);
        }

        public static DateTime ConvertToMexicoTime(DateTime utcDate)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDate, MexicoZone);
        }
    }
}