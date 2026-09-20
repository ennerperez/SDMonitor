namespace SDMonitor.Dashboard
{
    public interface IHardwareSampler
    {
        DashboardMetric[] Sample();
    }
}
