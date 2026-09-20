using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;

namespace SDMonitor
{
    internal sealed class MacOSHardwareSampler : IHardwareSampler
    {
        private NetworkSample? _previousNetwork;
        private double _maxUploadBytesPerSecond;
        private double _maxDownloadBytesPerSecond;

        public DashboardMetric[] Sample()
        {
            double? cpuPercent = ReadCpuPercent();
            double? ramPercent = ReadRamPercent();
            double? gpuPercent = ReadGpuPercent();
            double? diskPercent = ReadRootDiskPercent();
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

        private static double? ReadCpuPercent()
        {
            string? output = RunCommand("top", 1_000, "-l", "1", "-n", "0", "-s", "0");
            string? cpuLine = output?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("CPU usage:", StringComparison.Ordinal));

            double? idle = ReadPercentageBefore(cpuLine, " idle");
            return idle.HasValue ? Math.Clamp(100 - idle.Value, 0, 100) : null;
        }

        private static double? ReadRamPercent()
        {
            string? totalOutput = RunCommand("sysctl", 500, "-n", "hw.memsize");
            if (!ulong.TryParse(totalOutput?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong totalBytes) ||
                totalBytes == 0)
            {
                return null;
            }

            string? vmStat = RunCommand("vm_stat", 500);
            ulong pageSize = ReadPageSize(vmStat);
            if (pageSize == 0)
            {
                return null;
            }

            ulong freePages = ReadPageCount(vmStat, "Pages free");
            ulong speculativePages = ReadPageCount(vmStat, "Pages speculative");
            ulong availableBytes = (freePages + speculativePages) * pageSize;
            if (availableBytes > totalBytes)
            {
                availableBytes = totalBytes;
            }

            return Math.Clamp((totalBytes - availableBytes) / (double)totalBytes * 100, 0, 100);
        }

        private static double? ReadGpuPercent()
        {
            string? output = RunCommand("ioreg", 750, "-r", "-d", "1", "-w", "0", "-c", "AGXAccelerator");
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
            NetworkSample current = ReadNetworkSample();
            NetworkSample? previous = _previousNetwork;
            _previousNetwork = current;

            if (previous is null)
            {
                return (0, 0);
            }

            double seconds = Math.Max((current.Timestamp - previous.Timestamp).TotalSeconds, 0.001);
            return (
                ByteDelta(previous.TransmitBytes, current.TransmitBytes) / seconds,
                ByteDelta(previous.ReceiveBytes, current.ReceiveBytes) / seconds);
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

                foreach (string argument in arguments)
                {
                    process.StartInfo.ArgumentList.Add(argument);
                }

                process.Start();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                    return null;
                }

                return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd() : null;
            }
            catch
            {
                return null;
            }
        }

        private static ulong ReadPageSize(string? vmStat)
        {
            string? firstLine = vmStat?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (firstLine is null)
            {
                return 0;
            }

            const string marker = "page size of ";
            int start = firstLine.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return 0;
            }

            start += marker.Length;
            int end = firstLine.IndexOf(' ', start);
            string pageSizeText = end < 0 ? firstLine[start..] : firstLine[start..end];
            return ulong.TryParse(pageSizeText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong pageSize)
                ? pageSize
                : 0;
        }

        private static ulong ReadPageCount(string? vmStat, string name)
        {
            string? line = vmStat?
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(value => value.StartsWith(name + ":", StringComparison.Ordinal));
            if (line is null)
            {
                return 0;
            }

            string valueText = line.Split(':', 2)[1].Trim().TrimEnd('.').Replace(",", string.Empty, StringComparison.Ordinal);
            return ulong.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) ? value : 0;
        }

        private static double? ReadPercentageBefore(string? text, string token)
        {
            if (text is null)
            {
                return null;
            }

            int end = text.IndexOf(token, StringComparison.Ordinal);
            if (end <= 0)
            {
                return null;
            }

            int start = end - 1;
            while (start >= 0 && (char.IsDigit(text[start]) || text[start] == '.'))
            {
                start--;
            }

            string valueText = text[(start + 1)..end];
            return double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
        }

        private static double? ReadNamedNumber(string? text, string name)
        {
            if (text is null)
            {
                return null;
            }

            int nameIndex = text.IndexOf(name, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return null;
            }

            int valueStart = text.IndexOf('=', nameIndex);
            if (valueStart < 0)
            {
                return null;
            }

            valueStart++;
            while (valueStart < text.Length && !char.IsDigit(text[valueStart]))
            {
                valueStart++;
            }

            int valueEnd = valueStart;
            while (valueEnd < text.Length && (char.IsDigit(text[valueEnd]) || text[valueEnd] == '.'))
            {
                valueEnd++;
            }

            return double.TryParse(text[valueStart..valueEnd], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
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

        private sealed record NetworkSample(DateTimeOffset Timestamp, ulong ReceiveBytes, ulong TransmitBytes);
    }
}
