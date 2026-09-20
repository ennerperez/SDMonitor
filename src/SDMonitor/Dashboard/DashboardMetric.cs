using SDMonitor.Rendering;

namespace SDMonitor.Dashboard
{
    public record DashboardMetric(
        string Metric,
        string Title,
        string Value,
        double? Percent,
        RgbColor Accent);
}
