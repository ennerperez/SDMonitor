using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SDMonitor.Control;

namespace SDMonitor.Cli
{
    internal sealed class LinuxHardwareSampler
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
            double? diskPercent = ReadRootDiskPercent();
            (double uploadBytesPerSecond, double downloadBytesPerSecond) = ReadNetworkBytesPerSecond();

            return
            [
                PercentMetric("CPU", cpuPercent, new RgbColor(0, 200, 255)),
                PercentMetric("RAM", ramPercent, new RgbColor(120, 220, 80)),
                PercentMetric("GPU", gpuPercent, new RgbColor(170, 130, 255)),
                PercentMetric("HDD", diskPercent, new RgbColor(255, 190, 70)),
                RateMetric("UP", uploadBytesPerSecond, ref _maxUploadBytesPerSecond, new RgbColor(255, 100, 100)),
                RateMetric("DOWN", downloadBytesPerSecond, ref _maxDownloadBytesPerSecond, new RgbColor(80, 170, 255))
            ];
        }

        private double? ReadCpuPercent()
        {
            if (!File.Exists("/proc/stat"))
            {
                return null;
            }

            string? line = File.ReadLines("/proc/stat").FirstOrDefault();
            if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                return null;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return null;
            }

            ulong[] values = parts.Skip(1).Select(value => ulong.TryParse(value, out ulong parsed) ? parsed : 0).ToArray();
            ulong idle = values.ElementAtOrDefault(3) + values.ElementAtOrDefault(4);
            ulong total = 0;
            foreach (ulong value in values)
            {
                total += value;
            }

            CpuSample current = new(total, idle);
            CpuSample? previous = _previousCpu;
            _previousCpu = current;

            if (previous is null)
            {
                return 0;
            }

            ulong totalDelta = current.Total - previous.Total;
            ulong idleDelta = current.Idle - previous.Idle;
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

            Dictionary<string, ulong> values = File.ReadLines("/proc/meminfo")
                .Select(ParseMemInfoLine)
                .Where(pair => pair.HasValue)
                .ToDictionary(pair => pair!.Value.Key, pair => pair!.Value.Value);

            if (!values.TryGetValue("MemTotal", out ulong total) ||
                !values.TryGetValue("MemAvailable", out ulong available) ||
                total == 0)
            {
                return null;
            }

            return Math.Clamp((total - available) / (double)total * 100, 0, 100);
        }

        private static KeyValuePair<string, ulong>? ParseMemInfoLine(string line)
        {
            string[] parts = line.Split([':', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !ulong.TryParse(parts[1], out ulong value))
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

            if (!File.Exists("/proc/net/dev"))
            {
                return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
            }

            foreach (string line in File.ReadLines("/proc/net/dev").Skip(2))
            {
                string[] nameAndValues = line.Split(':', 2);
                if (nameAndValues.Length != 2)
                {
                    continue;
                }

                string interfaceName = nameAndValues[0].Trim();
                if (interfaceName == "lo")
                {
                    continue;
                }

                string[] values = nameAndValues[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length < 16)
                {
                    continue;
                }

                if (ulong.TryParse(values[0], out ulong rx))
                {
                    receive += rx;
                }

                if (ulong.TryParse(values[8], out ulong tx))
                {
                    transmit += tx;
                }
            }

            return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
        }

        private static DashboardMetric PercentMetric(string title, double? percent, RgbColor accent)
        {
            double? rounded = percent.HasValue ? Math.Round(percent.Value / 5, MidpointRounding.AwayFromZero) * 5 : null;
            return new DashboardMetric(title, FormatPercent(rounded), rounded, accent);
        }

        private static DashboardMetric RateMetric(string title, double bytesPerSecond, ref double maxBytesPerSecond, RgbColor accent)
        {
            double roundedBytesPerSecond = RoundRate(bytesPerSecond);
            if (roundedBytesPerSecond > maxBytesPerSecond)
            {
                maxBytesPerSecond = roundedBytesPerSecond;
            }

            double percent = maxBytesPerSecond <= 0
                ? 0
                : Math.Clamp(roundedBytesPerSecond / maxBytesPerSecond * 100, 0, 100);

            return new DashboardMetric(title, FormatRate(roundedBytesPerSecond), percent, accent);
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
