namespace PerformanceLogger.Models
{
    public class HddInfoModel
    {
        public string Name { get; set; }
        public string FileSystem { get; set; }
        public int UsedSpace { get; set; }
        public int TotalSpace { get; set; }
        public int HddUtilization { get; set; }
    }
}
