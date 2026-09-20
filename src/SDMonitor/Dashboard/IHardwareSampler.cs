namespace SDMonitor
{
    internal interface IHardwareSampler
    {
        DashboardMetric[] Sample();
    }
}
