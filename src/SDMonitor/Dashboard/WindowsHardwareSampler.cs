using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SDMonitor
{
    internal sealed class WindowsHardwareSampler : IHardwareSampler
    {
        private CpuSample? _previousCpu;
        private NetworkSample? _previousNetwork;
        private double _maxUploadBytesPerSecond;
        private double _maxDownloadBytesPerSecond;

        public DashboardMetric[] Sample()
        {
            double? cpuPercent = ReadCpuPercent();
            double? ramPercent = ReadRamPercent();
            double? gpuPercent = ReadNvidiaGpuPercent();
            double? diskPercent = ReadSystemDiskPercent();
            (double uploadBytesPerSecond, double downloadBytesPerSecond) = ReadNetworkBytesPerSecond();

            return
            [
                PercentMetric("cpu", "CPU", cpuPercent, new RgbColor(0, 200, 255)),
                PercentMetric("ram", "RAM", ramPercent, new RgbColor(120, 220, 80)),
                PercentMetric("gpu", "GPU", gpuPercent, new RgbColor(170, 130, 255)),
                PercentMetric("disk", "HDD", diskPercent, new RgbColor(255, 190, 70)),
                RateMetric("upload", "UP", uploadBytesPerSecond, ref _maxUploadBytesPerSecond, new RgbColor(255, 100, 100)),
                RateMetric("download", "DOWN", downloadBytesPerSecond, ref _maxDownloadBytesPerSecond, new RgbColor(80, 170, 255))
            ];
        }

        private double? ReadCpuPercent()
        {
            CpuSample current = new(DateTimeOffset.UtcNow, ReadProcessCpuTicks());
            CpuSample? previous = _previousCpu;
            _previousCpu = current;

            if (previous is null)
            {
                return 0;
            }

            double elapsedTicks = (current.Timestamp - previous.Timestamp).TotalSeconds *
                Environment.ProcessorCount *
                TimeSpan.TicksPerSecond;
            if (elapsedTicks <= 0)
            {
                return 0;
            }

            long cpuTicks = current.ProcessCpuTicks - previous.ProcessCpuTicks;
            return Math.Clamp(cpuTicks / elapsedTicks * 100, 0, 100);
        }

        private static long ReadProcessCpuTicks()
        {
            long ticks = 0;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    ticks += process.TotalProcessorTime.Ticks;
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            return ticks;
        }

        private static double? ReadRamPercent()
        {
            try
            {
                MemoryStatusEx status = new()
                {
                    Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
                };

                if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
                {
                    return null;
                }

                return Math.Clamp((status.TotalPhys - status.AvailPhys) / (double)status.TotalPhys * 100, 0, 100);
            }
            catch
            {
                return null;
            }
        }

        private static double? ReadNvidiaGpuPercent()
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        ArgumentList = { "--query-gpu=utilization.gpu", "--format=csv,noheader,nounits" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(500) || process.ExitCode != 0)
                {
                    return null;
                }

                string? firstValue = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return double.TryParse(firstValue, out double value) ? Math.Clamp(value, 0, 100) : null;
            }
            catch
            {
                return null;
            }
        }

        private static double? ReadSystemDiskPercent()
        {
            try
            {
                string? rootPath = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    rootPath = Path.GetPathRoot(Environment.SystemDirectory);
                }

                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return null;
                }

                DriveInfo root = new(rootPath);
                if (!root.IsReady || root.TotalSize <= 0)
                {
                    return null;
                }

                return Math.Clamp((root.TotalSize - root.AvailableFreeSpace) / (double)root.TotalSize * 100, 0, 100);
            }
            catch
            {
                return null;
            }
        }

        private (double UploadBytesPerSecond, double DownloadBytesPerSecond) ReadNetworkBytesPerSecond()
        {
            NetworkSample current = ReadNetworkSample();
            NetworkSample? previous = _previousNetwork;
            _previousNetwork = current;

            if (previous is null)
            {
                return (0, 0);
            }

            double seconds = Math.Max((current.Timestamp - previous.Timestamp).TotalSeconds, 0.001);
            return (
                (current.TransmitBytes - previous.TransmitBytes) / seconds,
                (current.ReceiveBytes - previous.ReceiveBytes) / seconds);
        }

        private static NetworkSample ReadNetworkSample()
        {
            ulong receive = 0;
            ulong transmit = 0;

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                    receive += ToUnsigned(statistics.BytesReceived);
                    transmit += ToUnsigned(statistics.BytesSent);
                }
                catch
                {
                }
            }

            return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
        }

        private static ulong ToUnsigned(long value)
        {
            return value > 0 ? (ulong)value : 0;
        }

        private static DashboardMetric PercentMetric(string metric, string title, double? percent, RgbColor accent)
        {
            double? rounded = percent.HasValue ? Math.Round(percent.Value / 5, MidpointRounding.AwayFromZero) * 5 : null;
            return new DashboardMetric(metric, title, FormatPercent(rounded), rounded, accent);
        }

        private static DashboardMetric RateMetric(string metric, string title, double bytesPerSecond, ref double maxBytesPerSecond, RgbColor accent)
        {
            double roundedBytesPerSecond = RoundRate(bytesPerSecond);
            if (roundedBytesPerSecond > maxBytesPerSecond)
            {
                maxBytesPerSecond = roundedBytesPerSecond;
            }

            double percent = maxBytesPerSecond <= 0
                ? 0
                : Math.Clamp(roundedBytesPerSecond / maxBytesPerSecond * 100, 0, 100);

            return new DashboardMetric(metric, title, FormatRate(roundedBytesPerSecond), percent, accent);
        }

        private static double RoundRate(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return Math.Round(bytesPerSecond / 1024 / 1024, 1, MidpointRounding.AwayFromZero) * 1024 * 1024;
            }

            if (bytesPerSecond >= 1024)
            {
                return Math.Round(bytesPerSecond / 1024 / 5, MidpointRounding.AwayFromZero) * 5 * 1024;
            }

            return Math.Round(bytesPerSecond / 100, MidpointRounding.AwayFromZero) * 100;
        }

        private static string FormatPercent(double? percent)
        {
            return percent.HasValue ? $"{percent.Value:0}%" : "N/A";
        }

        private static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{bytesPerSecond / 1024 / 1024:0.0}M";
            }

            if (bytesPerSecond >= 1024)
            {
                return $"{bytesPerSecond / 1024:0}K";
            }

            return $"{bytesPerSecond:0}B";
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        private sealed record CpuSample(DateTimeOffset Timestamp, long ProcessCpuTicks);

        private sealed record NetworkSample(DateTimeOffset Timestamp, ulong ReceiveBytes, ulong TransmitBytes);
    }
}
