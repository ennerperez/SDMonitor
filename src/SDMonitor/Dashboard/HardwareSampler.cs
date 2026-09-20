using System;

namespace SDMonitor
{
    internal static class HardwareSampler
    {
        public static IHardwareSampler Create()
        {
            return OperatingSystem.IsWindows()
                ? new WindowsHardwareSampler()
                : new LinuxHardwareSampler();
        }
    }
}
