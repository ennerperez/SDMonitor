using System;

namespace SDMonitor.Control
{
    public interface ISdMonitorTransport : IDisposable
    {
        SdMonitorDeviceInfo DeviceInfo { get; }

        void WriteOutputReport(ReadOnlySpan<byte> report);

        void SendFeatureReport(ReadOnlySpan<byte> report);

        byte[] GetFeatureReport(byte reportId, int length);

        int ReadInputReport(Span<byte> buffer, int timeoutMilliseconds);
    }
}
