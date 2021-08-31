namespace PerformanceLogger.Models
{
    public class PerformanceModel
    {
        public int CpuPercent { get; set; }
        public RamPerformance Ram { get; set; }
    }

    public class RamPerformance
    {
        public int FreeRam { get; set; }
        public int TotalRam { get; set; }
        public int UsedRam { get; set; }
        public int RamUtilization { get; set; }
    }
}
