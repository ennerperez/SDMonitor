using System;
using System.Collections.Generic;
using Xunit;

namespace SDMonitor.Control.Tests
{
    public sealed class MiniProtocolTests
    {
        [Fact]
        public void BrightnessReportMatchesMiniFeatureReportShape()
        {
            byte[] report = MiniProtocol.BrightnessReport(30);

            Assert.Equal(SdMonitorMiniConstants.FeatureReportLength, report.Length);
            Assert.Equal(0x05, report[0x00]);
            Assert.Equal(0x55, report[0x01]);
            Assert.Equal(0xAA, report[0x02]);
            Assert.Equal(0xD1, report[0x03]);
            Assert.Equal(0x01, report[0x04]);
            Assert.Equal(30, report[0x05]);
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
        public void ParseKeyStatesReadsFirstSixPayloadBytes()
        {
            byte[] input = new byte[SdMonitorMiniConstants.InputReportLength];
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
        public void ImageUploadReportsUsesMiniOutputReportHeaders()
        {
            byte[] bmp = MiniProtocol.SolidBmpKeyImage(0x11, 0x22, 0x33);

            IReadOnlyList<byte[]> reports = MiniProtocol.ImageUploadReports(2, bmp, showImage: true);

            Assert.All(reports, report => Assert.Equal(SdMonitorMiniConstants.OutputReportLength, report.Length));
            Assert.Equal((byte)'B', reports[0][SdMonitorMiniConstants.ImagePayloadOffset]);
            Assert.Equal(0x02, reports[0][0x00]);
            Assert.Equal(0x01, reports[0][0x01]);
            Assert.Equal(0x00, reports[0][0x02]);
            Assert.Equal(0x01, reports[0][0x04]);
            Assert.Equal(0x03, reports[0][0x05]);
            Assert.Equal(1, reports[1][0x02]);
        }
    }
}
