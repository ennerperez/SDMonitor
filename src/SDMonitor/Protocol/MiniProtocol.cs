using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SDMonitor.Devices;
using SDMonitor.Rendering;

namespace SDMonitor.Protocol
{
    public static class MiniProtocol
    {
        public static byte[] BrightnessReport(byte percent)
        {
            if (percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "Brightness must be between 0 and 100.");
            }

            var report = NewFeatureReport(0x05, 0x55);
            report[0x02] = 0xAA;
            report[0x03] = 0xD1;
            report[0x04] = 0x01;
            report[0x05] = percent;
            return report;
        }

        public static byte[] ShowLogoReport()
        {
            var report = NewFeatureReport(0x0B, 0x63);
            report[0x02] = 0x00;
            return report;
        }

        public static byte[] SleepDurationReport(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Sleep duration cannot be negative.");
            }

            if (duration.TotalSeconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Sleep duration is too large.");
            }

            var report = NewFeatureReport(0x0B, 0xA2);
            BinaryPrimitives.WriteInt32LittleEndian(report.AsSpan(0x02, sizeof(int)), (int)duration.TotalSeconds);
            return report;
        }

        public static byte[] FirmwareRequestReport(byte reportId)
        {
            if (reportId is not (0xA0 or 0xA1 or 0xA2))
            {
                throw new ArgumentOutOfRangeException(nameof(reportId), reportId, "Firmware report ID must be 0xA0, 0xA1, or 0xA2.");
            }

            return NewFeatureReport(reportId);
        }

        public static byte[] SerialNumberRequestReport()
        {
            return NewFeatureReport(0x03);
        }

        public static string ReadAsciiReportString(ReadOnlySpan<byte> report, int offset)
        {
            var length = report[offset..].IndexOf((byte)0x00);
            var value = length >= 0 ? report.Slice(offset, length) : report[offset..];
            return Encoding.ASCII.GetString(value).Trim();
        }

        public static IReadOnlyList<MiniKeyState> ParseKeyStates(ReadOnlySpan<byte> inputReport)
        {
            if (inputReport.Length < MonitorMiniConstants.InputReportLength)
            {
                throw new ArgumentException("Input report must be at least 65 bytes.", nameof(inputReport));
            }

            if (inputReport[0] != 0x01)
            {
                throw new ArgumentException("Input report ID must be 0x01.", nameof(inputReport));
            }

            var states = new MiniKeyState[MonitorMiniConstants.KeyCount];
            for (var key = 0; key < states.Length; key++)
            {
                states[key] = new MiniKeyState(key, inputReport[key + 1] != 0x00);
            }

            return states;
        }

        public static byte[] SolidBmpKeyImage(byte red, byte green, byte blue)
        {
            var pixels = new RgbColor[MonitorMiniConstants.KeyImageWidth * MonitorMiniConstants.KeyImageHeight];
            Array.Fill(pixels, new RgbColor(red, green, blue));
            return BmpFromPixels(pixels, MonitorMiniConstants.KeyImageWidth, MonitorMiniConstants.KeyImageHeight);
        }

        public static byte[] BmpFromPixels(ReadOnlySpan<RgbColor> pixels, int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
            }

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel count must equal width multiplied by height.", nameof(pixels));
            }

            const int bytesPerPixel = 3;
            const int headerLength = 54;
            var rowStride = width * bytesPerPixel;
            var paddedRowStride = (rowStride + 3) & ~3;
            var pixelDataLength = paddedRowStride * height;
            var bmp = new byte[headerLength + pixelDataLength];

            bmp[0] = (byte)'B';
            bmp[1] = (byte)'M';
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x02, 4), bmp.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x0A, 4), headerLength);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x0E, 4), 40);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x12, 4), width);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x16, 4), -height);
            BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(0x1A, 2), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(0x1C, 2), 24);
            BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(0x22, 4), pixelDataLength);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    var offset = headerLength + y * paddedRowStride + x * bytesPerPixel;
                    bmp[offset + 0] = pixel.Blue;
                    bmp[offset + 1] = pixel.Green;
                    bmp[offset + 2] = pixel.Red;
                }
            }

            return bmp;
        }

        public static IReadOnlyList<byte[]> ImageUploadReports(int keyIndex, ReadOnlySpan<byte> bmpBytes, bool showImage)
        {
            if (keyIndex is < 0 or >= MonitorMiniConstants.KeyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, "Key index must be between 0 and 5.");
            }

            if (bmpBytes.IsEmpty)
            {
                throw new ArgumentException("BMP image data cannot be empty.", nameof(bmpBytes));
            }

            var chunkCount = (bmpBytes.Length + MonitorMiniConstants.ImageChunkPayloadLength - 1) /
                             MonitorMiniConstants.ImageChunkPayloadLength;
            var reports = new byte[chunkCount][];

            for (var index = 0; index < reports.Length; index++)
            {
                var report = new byte[MonitorMiniConstants.OutputReportLength];
                report[0x00] = 0x02;
                report[0x01] = 0x01;
                report[0x02] = checked((byte)index);
                report[0x03] = 0x00;
                report[0x04] = showImage ? (byte)0x01 : (byte)0x00;
                report[0x05] = checked((byte)(keyIndex + 1));

                var sourceOffset = index * MonitorMiniConstants.ImageChunkPayloadLength;
                var sourceLength = Math.Min(MonitorMiniConstants.ImageChunkPayloadLength, bmpBytes.Length - sourceOffset);
                bmpBytes.Slice(sourceOffset, sourceLength).CopyTo(report.AsSpan(MonitorMiniConstants.ImagePayloadOffset));
                reports[index] = report;
            }

            return reports;
        }

        private static byte[] NewFeatureReport(byte reportId, byte command = 0x00)
        {
            var report = new byte[MonitorMiniConstants.FeatureReportLength];
            report[0] = reportId;
            report[1] = command;
            return report;
        }
    }
}
