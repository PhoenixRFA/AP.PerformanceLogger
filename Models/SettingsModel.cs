using System;

namespace PerformanceLogger.Models
{
    public class SettingsModel
    {
        public TimeSpan PerformanceLogPeriod { get; set; }
        public TimeSpan ProcessesLogPeriod { get; set; }
        public string LogsFolder { get; set; }
    }
}
