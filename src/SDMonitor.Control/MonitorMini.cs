using System;
using System.Collections.Generic;
using System.Threading;

namespace SDMonitor.Control
{
    public sealed class MonitorMini : IDisposable
    {
        private const int ReportWriteDelayMilliseconds = 2;

        private readonly IMonitorTransport _transport;

        public MonitorMini(IMonitorTransport transport)
        {
            _transport = transport;
        }

        public MonitorDeviceInfo DeviceInfo => _transport.DeviceInfo;

        public static IReadOnlyList<MonitorDeviceInfo> ListDevices()
        {
            return HidSharpMonitorTransport.ListDevices();
        }

        public static MonitorMini OpenFirst()
        {
            return new MonitorMini(HidSharpMonitorTransport.OpenFirst());
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
            byte[] report = _transport.GetFeatureReport(0x03, MonitorMiniConstants.FeatureReportLength);
            return MiniProtocol.ReadAsciiReportString(report, 0x05);
        }

        public string GetFirmwareVersion(byte reportId = 0xA1)
        {
            byte[] request = MiniProtocol.FirmwareRequestReport(reportId);
            byte[] report = _transport.GetFeatureReport(request[0], MonitorMiniConstants.FeatureReportLength);
            return MiniProtocol.ReadAsciiReportString(report, 0x05);
        }

        public IReadOnlyList<MiniKeyState>? PollKeys(int timeoutMilliseconds = 50)
        {
            Span<byte> report = stackalloc byte[MonitorMiniConstants.InputReportLength];
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
            for (int key = 0; key < MonitorMiniConstants.KeyCount; key++)
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
