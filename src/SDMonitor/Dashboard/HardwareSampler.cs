using System;

namespace SDMonitor.Dashboard
{
    public static class HardwareSampler
    {
        public static IHardwareSampler Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsHardwareSampler();
            }

            if (OperatingSystem.IsMacOS())
            {
                return new OsxHardwareSampler();
            }

            return new LinuxHardwareSampler();
        }
    }
}
