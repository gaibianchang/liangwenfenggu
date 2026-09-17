using System;
using System.Globalization;

namespace LiangWenFengGu
{
    public static class ClockService
    {
        private static TimeZoneInfo _tz;

        public static TimeZoneInfo Zone
        {
            get
            {
                if (_tz == null)
                {
                    string[] ids = new string[] { "China Standard Time", "Asia/Shanghai", "\u4E2D\u56FD\u6807\u51C6\u65F6\u95F4" };
                    for (int i = 0; i < ids.Length; i++)
                    {
                        try { _tz = TimeZoneInfo.FindSystemTimeZoneById(ids[i]); break; }
                        catch { }
                    }
                    if (_tz == null) _tz = TimeZoneInfo.Local;
                }
                return _tz;
            }
        }

        public static DateTime Now()
        {
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, Zone);
        }

        public static bool IsFeng(DateTime t)
        {
            int weekday = (int)t.DayOfWeek;
            bool workday = weekday >= 1 && weekday <= 5;
            if (!workday) return false;
            bool morning = t.Hour >= 9 && t.Hour < 12;
            bool afternoon = t.Hour >= 14 && t.Hour < 18;
            return morning || afternoon;
        }

        public static string WeekdayCn(DateTime t)
        {
            string[] names = new string[] { "\u661F\u671F\u65E5", "\u661F\u671F\u4E00", "\u661F\u671F\u4E8C", "\u661F\u671F\u4E09", "\u661F\u671F\u56DB", "\u661F\u671F\u4E94", "\u661F\u671F\u516D" };
            return names[(int)t.DayOfWeek];
        }

        public static DateTime NextSwitch(DateTime now)
        {
            int[] hours = new int[] { 9, 12, 14, 18 };
            bool current = IsFeng(now);
            for (int offset = 0; offset <= 8; offset++)
            {
                DateTime day = now.Date.AddDays(offset);
                for (int i = 0; i < hours.Length; i++)
                {
                    DateTime candidate = day.AddHours(hours[i]);
                    if (candidate <= now) continue;
                    if (IsFeng(candidate) != current) return candidate;
                }
            }
            return now.AddHours(24);
        }

        public static string FormatCountdown(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            int totalHours = (int)span.TotalHours;
            int days = totalHours / 24;
            int hours = totalHours % 24;
            string core = hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                span.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                span.Seconds.ToString("00", CultureInfo.InvariantCulture);
            if (days > 0) return days.ToString(CultureInfo.InvariantCulture) + "\u5929" + core;
            return core;
        }
        public static string PeriodHint(DateTime t)
        {
            if (IsFeng(t)) return "\u5DE5\u4F5C\u65F6\u6BB5";
            int weekday = (int)t.DayOfWeek;
            bool workday = weekday >= 1 && weekday <= 5;
            if (!workday) return "\u4F11\u606F\u65E5";
            if (t.Hour < 9) return "\u5C1A\u672A\u5F00\u5DE5";
            if (t.Hour < 12) return "\u5DE5\u4F5C\u65F6\u6BB5";
            if (t.Hour < 14) return "\u5348\u4F11\u65F6\u95F4";
            if (t.Hour < 18) return "\u5DE5\u4F5C\u65F6\u6BB5";
            return "\u4E0B\u73ED\u65F6\u95F4";
        }
    }
}
