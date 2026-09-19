using System;
using System.Collections.Generic;
using System.Threading;

namespace SDMonitor.Control
{
    public sealed class SdMonitorMini : IDisposable
    {
        private const int ReportWriteDelayMilliseconds = 2;

        private readonly ISdMonitorTransport _transport;

        public SdMonitorMini(ISdMonitorTransport transport)
        {
            _transport = transport;
        }

        public SdMonitorDeviceInfo DeviceInfo => _transport.DeviceInfo;

        public static IReadOnlyList<SdMonitorDeviceInfo> ListDevices()
        {
            return HidSharpSdMonitorTransport.ListDevices();
        }

        public static SdMonitorMini OpenFirst()
        {
            return new SdMonitorMini(HidSharpSdMonitorTransport.OpenFirst());
        }

        public void SetBrightness(byte percent)
        {
            _transport.SendFeatureReport(MiniProtocol.BrightnessReport(percent));
        }

        public void ShowLogo()
        {
            _transport.SendFeatureReport(MiniProtocol.ShowLogoReport());
        }

        public void SetSleepDuration(TimeSpan duration)
        {
            _transport.SendFeatureReport(MiniProtocol.SleepDurationReport(duration));
        }

        public string GetSerialNumber()
        {
            byte[] report = _transport.GetFeatureReport(0x03, SdMonitorMiniConstants.FeatureReportLength);
            return MiniProtocol.ReadAsciiReportString(report, 0x05);
        }

        public string GetFirmwareVersion(byte reportId = 0xA1)
        {
            byte[] request = MiniProtocol.FirmwareRequestReport(reportId);
            byte[] report = _transport.GetFeatureReport(request[0], SdMonitorMiniConstants.FeatureReportLength);
            return MiniProtocol.ReadAsciiReportString(report, 0x05);
        }

        public IReadOnlyList<MiniKeyState>? PollKeys(int timeoutMilliseconds = 50)
        {
            Span<byte> report = stackalloc byte[SdMonitorMiniConstants.InputReportLength];
            int read = _transport.ReadInputReport(report, timeoutMilliseconds);
            return read == 0 ? null : MiniProtocol.ParseKeyStates(report);
        }

        public void SetKeyImage(int keyIndex, ReadOnlySpan<byte> bmpBytes)
        {
            foreach (byte[] report in MiniProtocol.ImageUploadReports(keyIndex, bmpBytes, showImage: true))
            {
                _transport.WriteOutputReport(report);
                Thread.Sleep(ReportWriteDelayMilliseconds);
            }
        }

        public void SetKeyColor(int keyIndex, byte red, byte green, byte blue)
        {
            SetKeyImage(keyIndex, MiniProtocol.SolidBmpKeyImage(red, green, blue));
        }

        public void ClearKeys()
        {
            for (int key = 0; key < SdMonitorMiniConstants.KeyCount; key++)
            {
                SetKeyColor(key, 0, 0, 0);
            }
        }

        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
