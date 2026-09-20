using System;
using System.Collections.Generic;
using SDMonitor.Devices;
using Xunit;

namespace SDMonitor.UnitTests
{
    public sealed class MonitorMiniTests
    {
        [Fact]
        public void DeviceInfoReturnsTransportDeviceInfo()
        {
            FakeTransport transport = new();
            using MonitorMini deck = new(transport);

            Assert.Same(transport.DeviceInfo, deck.DeviceInfo);
        }

        [Fact]
        public void FeatureCommandsSendExpectedReports()
        {
            FakeTransport transport = new();
            using MonitorMini deck = new(transport);

            deck.SetBrightness(42);
            deck.ShowLogo();
            deck.SetSleepDuration(TimeSpan.FromSeconds(10));

            Assert.Equal(3, transport.FeatureReports.Count);
            Assert.Equal(0x05, transport.FeatureReports[0][0]);
            Assert.Equal(42, transport.FeatureReports[0][5]);
            Assert.Equal(0x0B, transport.FeatureReports[1][0]);
            Assert.Equal(0x63, transport.FeatureReports[1][1]);
            Assert.Equal(0x0B, transport.FeatureReports[2][0]);
            Assert.Equal(0xA2, transport.FeatureReports[2][1]);
        }

        [Fact]
        public void GetSerialAndFirmwareReadAsciiFeatureReports()
        {
            FakeTransport transport = new();
            transport.FeatureResponses[0x03] = FeatureReport(0x03, " SERIAL1 ");
            transport.FeatureResponses[0xA1] = FeatureReport(0xA1, " FW1 ");
            using MonitorMini deck = new(transport);

            var serial = deck.GetSerialNumber();
            var firmware = deck.GetFirmwareVersion();

            Assert.Equal("SERIAL1", serial);
            Assert.Equal("FW1", firmware);
            Assert.Equal([0x03, 0xA1], transport.FeatureRequestIds);
        }

        [Fact]
        public void PollKeysReturnsNullOnTimeoutAndParsesInputReports()
        {
            FakeTransport transport = new();
            transport.InputReads.Enqueue([]);
            transport.InputReads.Enqueue(KeyReport(pressedKey: 2));
            using MonitorMini deck = new(transport);

            Assert.Null(deck.PollKeys());
            var states = deck.PollKeys()!;

            Assert.NotNull(states);
            Assert.False(states[0].IsPressed);
            Assert.False(states[1].IsPressed);
            Assert.True(states[2].IsPressed);
        }

        [Fact]
        public void SetKeyImageSetKeyColorAndClearKeysWriteOutputReports()
        {
            FakeTransport transport = new();
            using MonitorMini deck = new(transport);

            deck.SetKeyImage(0, [0xAA, 0xBB]);
            deck.SetKeyColor(1, 1, 2, 3);
            deck.ClearKeys();

            Assert.True(transport.OutputReports.Count > 1);
            Assert.Equal(0x02, transport.OutputReports[0][0]);
            Assert.Equal(0x01, transport.OutputReports[0][5]);
            Assert.Equal(0xAA, transport.OutputReports[0][MonitorMiniConstants.ImagePayloadOffset]);
            Assert.Equal(0xBB, transport.OutputReports[0][MonitorMiniConstants.ImagePayloadOffset + 1]);
            Assert.Contains(transport.OutputReports, report => report[5] == 0x02);
            Assert.Contains(transport.OutputReports, report => report[5] == 0x06);
        }

        [Fact]
        public void DisposeDisposesTransport()
        {
            FakeTransport transport = new();

            new MonitorMini(transport).Dispose();

            Assert.True(transport.IsDisposed);
        }

        private static byte[] FeatureReport(byte reportId, string value)
        {
            var report = new byte[MonitorMiniConstants.FeatureReportLength];
            report[0] = reportId;
            for (var i = 0; i < value.Length; i++)
            {
                report[5 + i] = (byte)value[i];
            }

            return report;
        }

        private static byte[] KeyReport(int pressedKey)
        {
            var report = new byte[MonitorMiniConstants.InputReportLength];
            report[0] = 0x01;
            report[pressedKey + 1] = 0x01;
            return report;
        }

        private sealed class FakeTransport : IMonitorTransport
        {
            public MonitorDeviceInfo DeviceInfo { get; } = new("Product", "Maker", "Serial", 0x0FD9, 0x0063, "/dev/fake");

            public List<byte[]> OutputReports { get; } = [];

            public List<byte[]> FeatureReports { get; } = [];

            public List<byte> FeatureRequestIds { get; } = [];

            public Dictionary<byte, byte[]> FeatureResponses { get; } = [];

            public Queue<byte[]> InputReads { get; } = [];

            public bool IsDisposed { get; private set; }

            public void WriteOutputReport(ReadOnlySpan<byte> report)
            {
                OutputReports.Add(report.ToArray());
            }

            public void SendFeatureReport(ReadOnlySpan<byte> report)
            {
                FeatureReports.Add(report.ToArray());
            }

            public byte[] GetFeatureReport(byte reportId, int length)
            {
                FeatureRequestIds.Add(reportId);
                return FeatureResponses.TryGetValue(reportId, out var report) ? report : new byte[length];
            }

            public int ReadInputReport(Span<byte> buffer, int timeoutMilliseconds)
            {
                var report = InputReads.Dequeue();
                report.CopyTo(buffer);
                return report.Length;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
