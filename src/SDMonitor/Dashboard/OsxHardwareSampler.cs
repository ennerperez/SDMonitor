using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using SDMonitor.Rendering;

namespace SDMonitor.Dashboard
{
    public class OsxHardwareSampler : IHardwareSampler
    {
        private NetworkSample? _previousNetwork;
        private double _maxUploadBytesPerSecond;
        private double _maxDownloadBytesPerSecond;

        public DashboardMetric[] Sample()
        {
            var cpuPercent = ReadCpuPercent();
            var ramPercent = ReadRamPercent();
            var gpuPercent = ReadGpuPercent();
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

        private static double? ReadCpuPercent()
        {
            var output = RunCommand("top", 1_000, "-l", "1", "-n", "0", "-s", "0");
            var cpuLine = output?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("CPU usage:", StringComparison.Ordinal));

            var idle = ReadPercentageBefore(cpuLine, " idle");
            return idle.HasValue ? Math.Clamp(100 - idle.Value, 0, 100) : null;
        }

        private static double? ReadRamPercent()
        {
            var totalOutput = RunCommand("sysctl", 500, "-n", "hw.memsize");
            if (!ulong.TryParse(totalOutput?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var totalBytes) ||
                totalBytes == 0)
            {
                return null;
            }

            var vmStat = RunCommand("vm_stat", 500);
            var pageSize = ReadPageSize(vmStat);
            if (pageSize == 0)
            {
                return null;
            }

            var freePages = ReadPageCount(vmStat, "Pages free");
            var speculativePages = ReadPageCount(vmStat, "Pages speculative");
            var availableBytes = (freePages + speculativePages) * pageSize;
            if (availableBytes > totalBytes)
            {
                availableBytes = totalBytes;
            }

            return Math.Clamp((totalBytes - availableBytes) / (double)totalBytes * 100, 0, 100);
        }

        private static double? ReadGpuPercent()
        {
            var output = RunCommand("ioreg", 750, "-r", "-d", "1", "-w", "0", "-c", "AGXAccelerator");
            return ReadNamedNumber(output, "Device Utilization %");
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
                ByteDelta(previous.TransmitBytes, current.TransmitBytes) / seconds,
                ByteDelta(previous.ReceiveBytes, current.ReceiveBytes) / seconds);
        }

        private static NetworkSample ReadNetworkSample()
        {
            ulong receive = 0;
            ulong transmit = 0;

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    var statistics = networkInterface.GetIPv4Statistics();
                    receive += ToUInt64(statistics.BytesReceived);
                    transmit += ToUInt64(statistics.BytesSent);
                }
                catch (NetworkInformationException)
                {
                }
            }

            return new NetworkSample(DateTimeOffset.UtcNow, receive, transmit);
        }

        private static string? RunCommand(string fileName, int timeoutMilliseconds, params string[] arguments)
        {
            try
            {
                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    }
                };

                foreach (var argument in arguments)
                {
                    process.StartInfo.ArgumentList.Add(argument);
                }

                process.Start();
                if (process.WaitForExit(timeoutMilliseconds))
                {
                    return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd() : null;
                }

                process.Kill(entireProcessTree: true);
                return null;

            }
            catch
            {
                return null;
            }
        }

        private static ulong ReadPageSize(string? vmStat)
        {
            var firstLine = vmStat?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (firstLine is null)
            {
                return 0;
            }

            const string marker = "page size of ";
            var start = firstLine.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return 0;
            }

            start += marker.Length;
            var end = firstLine.IndexOf(' ', start);
            var pageSizeText = end < 0 ? firstLine[start..] : firstLine[start..end];
            return ulong.TryParse(pageSizeText, NumberStyles.None, CultureInfo.InvariantCulture, out var pageSize)
                ? pageSize
                : 0;
        }

        private static ulong ReadPageCount(string? vmStat, string name)
        {
            var line = vmStat?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(value => value.StartsWith(name + ":", StringComparison.Ordinal));
            if (line is null)
            {
                return 0;
            }

            var valueText = line.Split(':', 2)[1].Trim().TrimEnd('.').Replace(",", string.Empty, StringComparison.Ordinal);
            return ulong.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private static double? ReadPercentageBefore(string? text, string token)
        {
            if (text is null)
            {
                return null;
            }

            var end = text.IndexOf(token, StringComparison.Ordinal);
            if (end <= 0)
            {
                return null;
            }

            var start = end - 1;
            while (start >= 0 && (char.IsDigit(text[start]) || text[start] == '.'))
            {
                start--;
            }

            var valueText = text[(start + 1)..end];
            return double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static double? ReadNamedNumber(string? text, string name)
        {
            if (text is null)
            {
                return null;
            }

            var nameIndex = text.IndexOf(name, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return null;
            }

            var valueStart = text.IndexOf('=', nameIndex);
            if (valueStart < 0)
            {
                return null;
            }

            valueStart++;
            while (valueStart < text.Length && !char.IsDigit(text[valueStart]))
            {
                valueStart++;
            }

            var valueEnd = valueStart;
            while (valueEnd < text.Length && (char.IsDigit(text[valueEnd]) || text[valueEnd] == '.'))
            {
                valueEnd++;
            }

            return double.TryParse(text[valueStart..valueEnd], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, 0, 100)
                : null;
        }

        private static ulong ByteDelta(ulong previous, ulong current)
        {
            return current >= previous ? current - previous : 0;
        }

        private static ulong ToUInt64(long value)
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

        private sealed record NetworkSample(DateTimeOffset Timestamp, ulong ReceiveBytes, ulong TransmitBytes);
    }
}
