using System.Linq;
using SDMonitor.Dashboard;
using Xunit;

namespace SDMonitor.UnitTests
{
    public sealed class OsxHardwareSamplerTests
    {
        [Fact]
        public void SampleReturnsAllDashboardMetrics()
        {
            OsxHardwareSampler sampler = new();

            DashboardMetric[] metrics = sampler.Sample();
            DashboardMetric[] secondSample = sampler.Sample();

            Assert.Equal(["cpu", "ram", "gpu", "disk", "upload", "download"], metrics.Select(metric => metric.Metric).ToArray());
            Assert.Equal(["cpu", "ram", "gpu", "disk", "upload", "download"], secondSample.Select(metric => metric.Metric).ToArray());
            Assert.All(secondSample, metric => Assert.False(string.IsNullOrWhiteSpace(metric.Value)));
        }
    }
}
