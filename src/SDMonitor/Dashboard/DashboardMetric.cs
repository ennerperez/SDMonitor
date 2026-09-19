namespace SDMonitor
{
    internal sealed record DashboardMetric(
        string Metric,
        string Title,
        string Value,
        double? Percent,
        RgbColor Accent);
}
