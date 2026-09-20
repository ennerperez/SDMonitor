using System;
using System.Collections.Generic;
using Xunit;

namespace SDMonitor.Tests
{
    public sealed class MiniProtocolTests
    {
        [Fact]
        public void BrightnessReportMatchesMiniFeatureReportShape()
        {
            byte[] report = MiniProtocol.BrightnessReport(30);

            Assert.Equal(MonitorMiniConstants.FeatureReportLength, report.Length);
            Assert.Equal(0x05, report[0x00]);
            Assert.Equal(0x55, report[0x01]);
            Assert.Equal(0xAA, report[0x02]);
            Assert.Equal(0xD1, report[0x03]);
            Assert.Equal(0x01, report[0x04]);
            Assert.Equal(30, report[0x05]);
        }

        [Fact]
        public void BrightnessReportRejectsValuesOverOneHundred()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.BrightnessReport(101));
        }

        [Fact]
        public void SleepDurationReportWritesSecondsLittleEndian()
        {
            byte[] report = MiniProtocol.SleepDurationReport(TimeSpan.FromSeconds(300));

            Assert.Equal(0x0B, report[0x00]);
            Assert.Equal(0xA2, report[0x01]);
            Assert.Equal(0x2C, report[0x02]);
            Assert.Equal(0x01, report[0x03]);
            Assert.Equal(0x00, report[0x04]);
            Assert.Equal(0x00, report[0x05]);
        }

        [Fact]
        public void SleepDurationReportRejectsNegativeAndTooLargeDurations()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.SleepDurationReport(TimeSpan.FromSeconds(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.SleepDurationReport(TimeSpan.FromSeconds((double)int.MaxValue + 1)));
        }

        [Fact]
        public void FirmwareAndSerialReportsUseExpectedIds()
        {
            Assert.Equal(0xA0, MiniProtocol.FirmwareRequestReport(0xA0)[0]);
            Assert.Equal(0xA1, MiniProtocol.FirmwareRequestReport(0xA1)[0]);
            Assert.Equal(0xA2, MiniProtocol.FirmwareRequestReport(0xA2)[0]);
            Assert.Equal(0x03, MiniProtocol.SerialNumberRequestReport()[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.FirmwareRequestReport(0xA3));
        }

        [Fact]
        public void ReadAsciiReportStringStopsAtNullAndTrimsWhitespace()
        {
            byte[] report = [0, 0, 0, 0, 0, (byte)' ', (byte)'S', (byte)'N', (byte)'1', (byte)' ', 0, (byte)'X'];

            string value = MiniProtocol.ReadAsciiReportString(report, 5);

            Assert.Equal("SN1", value);
            Assert.Equal("ABC", MiniProtocol.ReadAsciiReportString([(byte)'A', (byte)'B', (byte)'C'], 0));
        }

        [Fact]
        public void ParseKeyStatesReadsFirstSixPayloadBytes()
        {
            byte[] input = new byte[MonitorMiniConstants.InputReportLength];
            input[0] = 0x01;
            input[1] = 0x01;
            input[4] = 0x01;

            IReadOnlyList<MiniKeyState> states = MiniProtocol.ParseKeyStates(input);

            Assert.True(states[0].IsPressed);
            Assert.False(states[1].IsPressed);
            Assert.False(states[2].IsPressed);
            Assert.True(states[3].IsPressed);
            Assert.False(states[4].IsPressed);
            Assert.False(states[5].IsPressed);
        }

        [Fact]
        public void ParseKeyStatesRejectsShortReportsAndWrongReportId()
        {
            Assert.Throws<ArgumentException>(() => MiniProtocol.ParseKeyStates(new byte[64]));

            byte[] input = new byte[MonitorMiniConstants.InputReportLength];
            input[0] = 0x02;
            Assert.Throws<ArgumentException>(() => MiniProtocol.ParseKeyStates(input));
        }

        [Fact]
        public void SolidBmpKeyImageCreatesEightyByEightyTwentyFourBitBmp()
        {
            byte[] bmp = MiniProtocol.SolidBmpKeyImage(0x11, 0x22, 0x33);

            Assert.Equal((byte)'B', bmp[0]);
            Assert.Equal((byte)'M', bmp[1]);
            Assert.Equal(54 + 80 * 80 * 3, bmp.Length);
            Assert.Equal(0x33, bmp[54]);
            Assert.Equal(0x22, bmp[55]);
            Assert.Equal(0x11, bmp[56]);
        }

        [Fact]
        public void BmpFromPixelsAddsRowPaddingAndWritesTopDownPixels()
        {
            RgbColor[] pixels =
            [
                new(0x10, 0x20, 0x30),
                new(0x40, 0x50, 0x60)
            ];

            byte[] bmp = MiniProtocol.BmpFromPixels(pixels, width: 1, height: 2);

            Assert.Equal(54 + 8, bmp.Length);
            Assert.Equal(0x30, bmp[54]);
            Assert.Equal(0x20, bmp[55]);
            Assert.Equal(0x10, bmp[56]);
            Assert.Equal(0x60, bmp[58]);
            Assert.Equal(0x50, bmp[59]);
            Assert.Equal(0x40, bmp[60]);
        }

        [Fact]
        public void BmpFromPixelsRejectsInvalidDimensionsAndPixelCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.BmpFromPixels([], width: 0, height: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.BmpFromPixels([], width: 1, height: 0));
            Assert.Throws<ArgumentException>(() => MiniProtocol.BmpFromPixels([], width: 1, height: 1));
        }

        [Fact]
        public void ImageUploadReportsUsesMiniOutputReportHeaders()
        {
            byte[] bmp = MiniProtocol.SolidBmpKeyImage(0x11, 0x22, 0x33);

            IReadOnlyList<byte[]> reports = MiniProtocol.ImageUploadReports(2, bmp, showImage: true);

            Assert.All(reports, report => Assert.Equal(MonitorMiniConstants.OutputReportLength, report.Length));
            Assert.Equal((byte)'B', reports[0][MonitorMiniConstants.ImagePayloadOffset]);
            Assert.Equal(0x02, reports[0][0x00]);
            Assert.Equal(0x01, reports[0][0x01]);
            Assert.Equal(0x00, reports[0][0x02]);
            Assert.Equal(0x01, reports[0][0x04]);
            Assert.Equal(0x03, reports[0][0x05]);
            Assert.Equal(1, reports[1][0x02]);
        }

        [Fact]
        public void ImageUploadReportsHandlesSingleChunkAndHiddenImageFlag()
        {
            byte[] bmp = [0x10, 0x20, 0x30];

            IReadOnlyList<byte[]> reports = MiniProtocol.ImageUploadReports(0, bmp, showImage: false);

            Assert.Single(reports);
            Assert.Equal(0x00, reports[0][0x04]);
            Assert.Equal(0x01, reports[0][0x05]);
            Assert.Equal(bmp, reports[0][MonitorMiniConstants.ImagePayloadOffset..(MonitorMiniConstants.ImagePayloadOffset + bmp.Length)]);
        }

        [Fact]
        public void ImageUploadReportsRejectsInvalidKeyAndEmptyImage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.ImageUploadReports(-1, [1], showImage: true));
            Assert.Throws<ArgumentOutOfRangeException>(() => MiniProtocol.ImageUploadReports(MonitorMiniConstants.KeyCount, [1], showImage: true));
            Assert.Throws<ArgumentException>(() => MiniProtocol.ImageUploadReports(0, [], showImage: true));
        }
    }
}
