using System;

namespace BalrogLauncher.Models
{
    public class CountdownEvent
    {
        public string Name { get; set; } = string.Empty;
        public DateTime EndTime { get; set; }
        public long TimestampMs { get; set; }

        public string FormattedRemainingTime
            => (EndTime - DateTime.Now).TotalSeconds <= 0
                ? "00:00:00"
                : (EndTime - DateTime.Now).TotalDays >= 1
                    ? $"{(int)(EndTime - DateTime.Now).TotalDays}d {(EndTime - DateTime.Now).Hours:D2}:{(EndTime - DateTime.Now).Minutes:D2}:{(EndTime - DateTime.Now).Seconds:D2}"
                    : $"{(EndTime - DateTime.Now).Hours:D2}:{(EndTime - DateTime.Now).Minutes:D2}:{(EndTime - DateTime.Now).Seconds:D2}";
    }
}
