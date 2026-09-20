namespace SDMonitor.Devices
{
    public sealed record MonitorDeviceInfo(
        string ProductName,
        string Manufacturer,
        string SerialNumber,
        int VendorId,
        int ProductId,
        string DevicePath);
}
