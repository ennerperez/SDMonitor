using SDMonitor.Control;

namespace SDMonitor.Cli
{
    internal sealed record DashboardMetric(
        string Title,
        string Value,
        double? Percent,
        RgbColor Accent);
}
