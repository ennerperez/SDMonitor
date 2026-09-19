namespace SDMonitor.Control
{
    public sealed record SdMonitorDeviceInfo(
        string ProductName,
        string Manufacturer,
        string SerialNumber,
        int VendorId,
        int ProductId,
        string DevicePath);
}
