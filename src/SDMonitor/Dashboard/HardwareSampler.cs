using System;

namespace SDMonitor
{
    internal static class HardwareSampler
    {
        public static IHardwareSampler Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsHardwareSampler();
            }

            if (OperatingSystem.IsMacOS())
            {
                return new MacOSHardwareSampler();
            }

            return new LinuxHardwareSampler();
        }
    }
}
