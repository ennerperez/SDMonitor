using System;
using System.IO;
using Xunit;

namespace SDMonitor.Tests
{
    public sealed class DashboardRunnerTests
    {
        [Fact]
        public void RunConsoleOnlyPrintsOneFrameAndStops()
        {
            DashboardConfig config = OneTileConfig();
            using StringWriter output = new();
            TextWriter originalOutput = Console.Out;

            try
            {
                Console.SetOut(output);

                DashboardRunner.RunConsoleOnly(config, frames: 1);
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            string text = output.ToString();
            Assert.Contains("Dashboard running without device.", text, StringComparison.Ordinal);
            Assert.Contains("Console dashboard stopped.", text, StringComparison.Ordinal);
            Assert.Contains("CPU:", text, StringComparison.Ordinal);
        }

        [Fact]
        public void RunRendersOneFrameAndClearsDeck()
        {
            FakeTransport transport = new();
            using MonitorMini deck = new(transport);
            DashboardConfig config = OneTileConfig();
            using StringWriter output = new();
            TextWriter originalOutput = Console.Out;

            try
            {
                Console.SetOut(output);

                DashboardRunner.Run(deck, config, frames: 1);
            }
            finally
            {
                Console.SetOut(originalOutput);
            }

            string text = output.ToString();
            Assert.Contains("Dashboard running.", text, StringComparison.Ordinal);
            Assert.Contains("Dashboard stopped.", text, StringComparison.Ordinal);
            Assert.Contains("CPU:", text, StringComparison.Ordinal);
            Assert.True(transport.OutputReportCount > 0);
        }

        private static DashboardConfig OneTileConfig()
        {
            DashboardTile tile = new(
                "cpu",
                "CPU",
                1,
                250,
                TileRenderStyle.Default(new RgbColor(0, 200, 255)),
                []);

            return new DashboardConfig([tile]);
        }

        private sealed class FakeTransport : IMonitorTransport
        {
            public MonitorDeviceInfo DeviceInfo { get; } = new("Product", "Maker", "Serial", 0x0FD9, 0x0063, "/dev/fake");

            public int OutputReportCount { get; private set; }

            public void WriteOutputReport(ReadOnlySpan<byte> report)
            {
                OutputReportCount++;
            }

            public void SendFeatureReport(ReadOnlySpan<byte> report)
            {
            }

            public byte[] GetFeatureReport(byte reportId, int length)
            {
                return new byte[length];
            }

            public int ReadInputReport(Span<byte> buffer, int timeoutMilliseconds)
            {
                return 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
