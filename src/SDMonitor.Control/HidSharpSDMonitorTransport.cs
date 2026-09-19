using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;

namespace SDMonitor.Control
{
    public sealed class HidSharpSdMonitorTransport : ISdMonitorTransport
    {
        private readonly HidStream _stream;

        private HidSharpSdMonitorTransport(HidDevice device, HidStream stream)
        {
            _stream = stream;
            DeviceInfo = new SdMonitorDeviceInfo(
                device.GetProductName(),
                device.GetManufacturer(),
                device.GetSerialNumber(),
                device.VendorID,
                device.ProductID,
                device.DevicePath);
        }

        public SdMonitorDeviceInfo DeviceInfo { get; }

        public static IReadOnlyList<SdMonitorDeviceInfo> ListDevices()
        {
            return DeviceList.Local
                .GetHidDevices(SdMonitorMiniConstants.VendorId)
                .Where(IsSupportedMini)
                .Select(device => new SdMonitorDeviceInfo(
                    SafeGet(device.GetProductName),
                    SafeGet(device.GetManufacturer),
                    SafeGet(device.GetSerialNumber),
                    device.VendorID,
                    device.ProductID,
                    device.DevicePath))
                .ToArray();
        }

        public static HidSharpSdMonitorTransport OpenFirst()
        {
            HidDevice? device = DeviceList.Local
                .GetHidDevices(SdMonitorMiniConstants.VendorId)
                .Where(IsSupportedMini)
                .FirstOrDefault();

            if (device is null)
            {
                throw new InvalidOperationException("No supported Stream Deck Mini HID device was found.");
            }

            if (!device.TryOpen(out HidStream stream))
            {
                throw new InvalidOperationException("Stream Deck Mini was found, but HID handle could not be opened.");
            }

            stream.ReadTimeout = 50;
            stream.WriteTimeout = 1000;

            return new HidSharpSdMonitorTransport(device, stream);
        }

        public void WriteOutputReport(ReadOnlySpan<byte> report)
        {
            _stream.Write(report.ToArray());
        }

        public void SendFeatureReport(ReadOnlySpan<byte> report)
        {
            _stream.SetFeature(report.ToArray());
        }

        public byte[] GetFeatureReport(byte reportId, int length)
        {
            byte[] report = new byte[length];
            report[0] = reportId;
            _stream.GetFeature(report);
            return report;
        }

        public int ReadInputReport(Span<byte> buffer, int timeoutMilliseconds)
        {
            byte[] local = new byte[buffer.Length];
            _stream.ReadTimeout = timeoutMilliseconds;

            try
            {
                int read = _stream.Read(local);
                local.AsSpan(0, read).CopyTo(buffer);
                return read;
            }
            catch (TimeoutException)
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private static bool IsSupportedMini(HidDevice device)
        {
            return Enumerable.Contains(SdMonitorMiniConstants.SupportedProductIds, device.ProductID);
        }

        private static string SafeGet(Func<string> valueFactory)
        {
            try
            {
                return valueFactory();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
