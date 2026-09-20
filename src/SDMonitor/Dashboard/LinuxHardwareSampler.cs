using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SDMonitor.Rendering;

namespace SDMonitor.Dashboard
{
    public class LinuxHardwareSampler : IHardwareSampler
    {
        private CpuSample? _previousCpu;
        private NetworkSample? _previousNetwork;
        private double _maxUploadBytesPerSecond;
        private double _maxDownloadBytesPerSecond;

        public DashboardMetric[] Sample()
        {
            var cpuPercent = ReadCpuPercent();
            var ramPercent = ReadRamPercent();
            var gpuPercent = ReadNvidiaGpuPercent();
            var diskPercent = ReadRootDiskPercent();
            (var uploadBytesPerSecond, var downloadBytesPerSecond) = ReadNetworkBytesPerSecond();

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
            if (!File.Exists("/proc/stat"))
            {
                return null;
            }

            var line = File.ReadLines("/proc/stat").FirstOrDefault();
            if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                return null;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return null;
            }

            var values = parts.Skip(1).Select(value => ulong.TryParse(value, out var parsed) ? parsed : 0).ToArray();
            var idle = values.ElementAtOrDefault(3) + values.ElementAtOrDefault(4);
            ulong total = 0;
            foreach (var value in values)
            {
                total += value;
            }

            CpuSample current = new(total, idle);
            var previous = _previousCpu;
            _previousCpu = current;

            if (previous is null)
            {
                return 0;
            }

            var totalDelta = current.Total - previous.Total;
            var idleDelta = current.Idle - previous.Idle;
            if (totalDelta == 0)
            {
                return 0;
            }

            return Math.Clamp((1 - idleDelta / (double)totalDelta) * 100, 0, 100);
        }

        private static double? ReadRamPercent()
        {
            if (!File.Exists("/proc/meminfo"))
            {
                return null;
            }

            var values = File.ReadLines("/proc/meminfo")
                .Select(ParseMemInfoLine)
                .Where(pair => pair.HasValue)
                .ToDictionary(pair => pair!.Value.Key, pair => pair!.Value.Value);

            if (!values.TryGetValue("MemTotal", out var total) ||
                !values.TryGetValue("MemAvailable", out var available) ||
                total == 0)
            {
                return null;
            }

            return Math.Clamp((total - available) / (double)total * 100, 0, 100);
        }

        private static KeyValuePair<string, ulong>? ParseMemInfoLine(string line)
        {
            var parts = line.Split([':', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !ulong.TryParse(parts[1], out var value))
            {
                return null;
            }

            return new KeyValuePair<string, ulong>(parts[0], value);
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
                var output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(500) || process.ExitCode != 0)
                {
                    return null;
                }

                var firstValue = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return double.TryParse(firstValue, out var value) ? Math.Clamp(value, 0, 100) : null;
            }
            catch
            {
                return null;
            }
        }

        private static double? ReadRootDiskPercent()
        {
            DriveInfo root = new("/");
            if (!root.IsReady || root.TotalSize <= 0)
            {
                return null;
            }

            return Math.Clamp((root.TotalSize - root.AvailableFreeSpace) / (double)root.TotalSize * 100, 0, 100);
        }

        private (double UploadBytesPerSecond, double DownloadBytesPerSecond) ReadNetworkBytesPerSecond()
        {
            var current = ReadNetworkSample();
            var previous = _previousNetwork;
            _previousNetwork = current;

            if (previous is null)
            {
                return (0, 0);
            }

            var seconds = Math.Max((current.Timestamp - previous.Timestamp).TotalSeconds, 0.001);
            return (
                (current.TransmitBytes - previous.TransmitBytes) / seconds,
                (current.ReceiveBytes - previous.ReceiveBytes) / seconds);
        }

        private static NetworkSample ReadNetworkSample()
        {
            ulong receive = 0;
            ulong transmit = 0;

            if (!File.Exists("/proc/net/dev"))
            {
                return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
            }

            foreach (var line in File.ReadLines("/proc/net/dev").Skip(2))
            {
                var nameAndValues = line.Split(':', 2);
                if (nameAndValues.Length != 2)
                {
                    continue;
                }

                var interfaceName = nameAndValues[0].Trim();
                if (interfaceName == "lo")
                {
                    continue;
                }

                var values = nameAndValues[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 16)
                {
                    continue;
                }

                if (ulong.TryParse(values[0], out var rx))
                {
                    receive += rx;
                }

                if (ulong.TryParse(values[8], out var tx))
                {
                    transmit += tx;
                }
            }

            return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
        }

        private static DashboardMetric PercentMetric(string metric, string title, double? percent, RgbColor accent)
        {
            double? rounded = percent.HasValue ? Math.Round(percent.Value / 5, MidpointRounding.AwayFromZero) * 5 : null;
            return new DashboardMetric(metric, title, FormatPercent(rounded), rounded, accent);
        }

        private static DashboardMetric RateMetric(string metric, string title, double bytesPerSecond, ref double maxBytesPerSecond, RgbColor accent)
        {
            var roundedBytesPerSecond = RoundRate(bytesPerSecond);
            if (roundedBytesPerSecond > maxBytesPerSecond)
            {
                maxBytesPerSecond = roundedBytesPerSecond;
            }

            var percent = maxBytesPerSecond <= 0
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

        private sealed record CpuSample(ulong Total, ulong Idle);

        private sealed record NetworkSample(DateTimeOffset Timestamp, ulong ReceiveBytes, ulong TransmitBytes);
    }
}
