using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace SDMonitor.UnitTests
{
    public sealed class CliTests
    {
        [Theory]
        [InlineData("--help", 0, "sdmonitor command:")]
        [InlineData("help", 0, "sdmonitor command:")]
        [InlineData("unknown", 2, "Unknown command: unknown")]
        [InlineData("dashboard 250 1 extra", 1, "Invalid dashboard arguments.")]
        [InlineData("dashboard 249", 1, "Invalid interval-ms. Expected integer from 250 to 60000.")]
        public void CommandLineHandlesNonHardwarePaths(string arguments, int exitCode, string expectedOutput)
        {
            using var process = StartCli(arguments);

            Assert.True(process.WaitForExit(5000));
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();

            Assert.Equal(exitCode, process.ExitCode);
            Assert.Contains(expectedOutput, output, StringComparison.Ordinal);
        }

        private static Process StartCli(string arguments)
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "sdmonitor");
            return Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            })!;
        }
    }
}
