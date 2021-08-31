using System;

namespace PerformanceLogger.Models
{
    public class ProcessModel
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        public TimeSpan ProcessedTime { get; set; }
        public int ThreadsCount { get; set; }
        public int WorkingSet { get; set; }
        public int Cpu { get; set; }
    }
}
