using System.Linq;
using Xunit;

namespace SDMonitor.Tests
{
    public sealed class MacOSHardwareSamplerTests
    {
        [Fact]
        public void SampleReturnsAllDashboardMetrics()
        {
            MacOSHardwareSampler sampler = new();

            DashboardMetric[] metrics = sampler.Sample();
            DashboardMetric[] secondSample = sampler.Sample();

            Assert.Equal(["cpu", "ram", "gpu", "disk", "upload", "download"], metrics.Select(metric => metric.Metric).ToArray());
            Assert.Equal(["cpu", "ram", "gpu", "disk", "upload", "download"], secondSample.Select(metric => metric.Metric).ToArray());
            Assert.All(secondSample, metric => Assert.False(string.IsNullOrWhiteSpace(metric.Value)));
        }
    }
}
