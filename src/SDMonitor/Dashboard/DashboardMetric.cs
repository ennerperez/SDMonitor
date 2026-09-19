namespace SDMonitor
{
    internal sealed record DashboardMetric(
        string Title,
        string Value,
        double? Percent,
        RgbColor Accent);
}
