using Newtonsoft.Json;
using PerformanceLogger.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace PerformanceLogger
{
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = nameof(PerformanceLogger);

            //var _timer = new Timer(TimerCallback, null, 5000, 1000);
            while (true)
            {
                TimerCallback();
                Thread.Sleep(900);

                //Console.ReadLine();
            }
        }

        private static DateTime _lastPerformanceUpdate = DateTime.MinValue;
        private static DateTime _lastProcessesUpdate = DateTime.MinValue;
        private static DateTime _lastTick = DateTime.Now;
        private static void TimerCallback()
        {
            PerformanceModel performance = GetPerformance();
            ICollection<HddInfoModel> hddInfo = GetHddInfo();

            UpdateConsole(performance, hddInfo);

            var now = DateTime.Now;
            SettingsModel settings = GetSettings();
            if(now - _lastPerformanceUpdate > settings.PerformanceLogPeriod)
            {
                _lastPerformanceUpdate = now;
                LogPerformance(performance, hddInfo);
            }

            if(now - _lastProcessesUpdate > settings.ProcessesLogPeriod)
            {
                _lastProcessesUpdate = now;
                ICollection<ProcessModel> processes = GetProcessesLog();
                LogProcesses(processes);
            }

            if (now.Day > _lastTick.Day)
            {
                ZipFiles();
                DeleteLogFiles();
            }

            _lastTick = now;
        }

        private static void UpdateConsole(PerformanceModel performance, ICollection<HddInfoModel> hddInfo)
        {
            Console.SetCursorPosition(1, 1);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"CPU: {performance.CpuPercent,2}%");

            Console.SetCursorPosition(10, 1);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write($"RAM: {performance.Ram.UsedRam,5}MB ({performance.Ram.RamUtilization,2}%)");

            Console.SetCursorPosition(29, 1);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("HDD:");
            foreach (HddInfoModel disk in hddInfo)
            {
                Console.Write($" {disk.Name} {disk.UsedSpace}GB ({disk.HddUtilization}%)");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.SetCursorPosition(1, 2);
        }

        //private static void ShowProcesses(ICollection<ProcessModel> processes)
        //{
        //    Console.WriteLine($"{"PID",-6}|{"Name",-35}|TotalProcTime|Threads|Memory(Mb)");

        //    foreach (var proc in processes)
        //    {
        //        Console.WriteLine($"{proc.Pid,-6}|{proc.Name,-35}|{proc.ProcessedTime:hh\\:mm\\:ss, -13}|{proc.ThreadsCount,-7}|{proc.WorkingSet}");
        //    }
        //}

        private static ICollection<ProcessModel> GetProcessesLog()
        {
            var res = new List<ProcessModel>();

            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.Id == 0) continue;

                TimeSpan totalProcTime = TimeSpan.Zero;
                float cpu = 0;

                try
                {
                    totalProcTime = proc.TotalProcessorTime;
                    var counter = new PerformanceCounter("Process", "% Processor Time", proc.ProcessName, true);
                    cpu = counter.NextValue();
                    cpu = counter.NextValue();
                }
                catch { /* ignored */ }

                res.Add(new ProcessModel
                {
                    Cpu = (int)(cpu/Environment.ProcessorCount),
                    Pid = proc.Id,
                    Name = proc.ProcessName,
                    ProcessedTime = totalProcTime,
                    ThreadsCount = proc.Threads.Count,
                    WorkingSet = (int)(proc.WorkingSet64 / 1024 / 1024)
                });
            }

            return res;
        }

        private static ulong GetTotalMemoryInBytes()
        {
            return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        }

        private static PerformanceModel GetPerformance()
        {
            float cpu = 0, ram = 0;
            try
            {
                var totalCpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                var availableRam = new PerformanceCounter("Memory", "Available MBytes");
                cpu = totalCpu.NextValue();
                Thread.Sleep(100);
                cpu = totalCpu.NextValue();
                ram = availableRam.NextValue();
            }
            catch { /*ignored*/ }

            ulong memKb = GetTotalMemoryInBytes() / 1024;

            int totalRam = (int)(memKb / 1024);
            int usedRam = totalRam - (int)ram;
            int ramUtilization = (int)((float)usedRam / totalRam * 100);

            return new PerformanceModel
            {
                CpuPercent = (int)cpu,
                Ram = new RamPerformance
                {
                    FreeRam = (int)ram,
                    TotalRam = totalRam,
                    UsedRam = usedRam,
                    RamUtilization = ramUtilization
                }
            };
        }

        private static ICollection<HddInfoModel> GetHddInfo()
        {
            var res = new List<HddInfoModel>();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    long totalSpaceGb = drive.TotalSize / 1024 / 1024 / 1024;
                    long usedSpaceGb = (drive.TotalSize - drive.TotalFreeSpace) / 1024 / 1024 / 1024;
                    int spaceUtilization = (int)((float)usedSpaceGb / totalSpaceGb * 100);

                    res.Add(new HddInfoModel
                    {
                        FileSystem = drive.DriveFormat,
                        Name = drive.Name,
                        UsedSpace = (int)usedSpaceGb,
                        TotalSpace = (int)totalSpaceGb,
                        HddUtilization = spaceUtilization
                    });
                }
            }

            return res;
        }

        private static SettingsModel GetSettings()
        {
            string processLogPeriodSetting = ConfigurationManager.AppSettings["processLogPeriod"];
            string performanceLogPeriodSetting = ConfigurationManager.AppSettings["performanceLogPeriod"];
            string logsFolder = ConfigurationManager.AppSettings["logsPath"];

            if (!TimeSpan.TryParse(processLogPeriodSetting, out TimeSpan processLogPeriod)) processLogPeriod = TimeSpan.FromSeconds(30);
            if (!TimeSpan.TryParse(performanceLogPeriodSetting, out TimeSpan performanceLogPeriod)) performanceLogPeriod = TimeSpan.FromSeconds(30);
            logsFolder = string.IsNullOrEmpty(logsFolder) ? "~/logs/" : logsFolder.Trim();

            return new SettingsModel {
                LogsFolder = logsFolder,
                PerformanceLogPeriod = performanceLogPeriod,
                ProcessesLogPeriod = processLogPeriod
            };
        }

        private static void WriteToFile(string path, string data)
        {
            path = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(path);
            if(!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            File.AppendAllText(path, data);
        }

        private const string PerformanceLogFilename = "performance.txt";
        private const string ProcessesLogFilename = "processes.txt";
        private static void LogPerformance(PerformanceModel data, ICollection<HddInfoModel> hdds)
        {
            string path = GetSettings().LogsFolder + PerformanceLogFilename;
            string json = JsonConvert.SerializeObject(new {
                now = DateTime.Now,
                cpu = data.CpuPercent,
                ram = data.Ram.RamUtilization,
                ramUsage = data.Ram.UsedRam,
                hdds = hdds.Select(x => new {
                    name = x.Name,
                    usage = x.HddUtilization,
                    free = x.TotalSpace - x.UsedSpace
                    })
                });
            WriteToFile(path, json + ",");
        }
        private static void LogProcesses(ICollection<ProcessModel> processes)
        {
            string path = GetSettings().LogsFolder + ProcessesLogFilename;
            string json = JsonConvert.SerializeObject(new { now = DateTime.Now, processes = processes.OrderByDescending(x=>x.WorkingSet) });
            WriteToFile(path, json + ",");
        }

        //private static void ReadLog()
        //{
        //    string path = Path.GetFullPath("logs/log.txt");

        //    if(!File.Exists(path)) return;

        //    string json = "{\"items\": [" + File.ReadAllText(path) + "]}";
        //}

        private static void ZipFiles()
        {
            string path = Path.GetFullPath(GetSettings().LogsFolder);

            string performanceLog = Path.Combine(path, PerformanceLogFilename);
            string processesLog = Path.Combine(path, ProcessesLogFilename);

            if(!File.Exists(performanceLog) || !File.Exists(processesLog)) return;

            string zipPath = Path.Combine(path, $"{DateTime.Now:dd_MM_yyyy}.zip");
            if (File.Exists(zipPath))
            {
                zipPath = Path.Combine(path, $"{DateTime.Now:dd_MM_yyyy_HHmmss}.zip");
            }

            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(performanceLog, PerformanceLogFilename, CompressionLevel.Optimal);
                zip.CreateEntryFromFile(processesLog, ProcessesLogFilename, CompressionLevel.Optimal);
            }
        }
        private static void DeleteLogFiles()
        {
            string path = Path.GetFullPath(GetSettings().LogsFolder);

            string performanceLog = Path.Combine(path, PerformanceLogFilename);
            string processesLog = Path.Combine(path, ProcessesLogFilename);

            if (File.Exists(performanceLog))
            {
                File.Delete(performanceLog);
            }

            if (File.Exists(processesLog))
            {
                File.Delete(processesLog);
            }
        }
    }
}
