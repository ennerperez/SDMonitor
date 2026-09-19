using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SDMonitor.Control;

namespace SDMonitor.Cli
{
    internal static class DashboardRunner
    {
        public static void Run(SdMonitorMini deck, int intervalMilliseconds, int? frames)
        {
            LinuxHardwareSampler sampler = new();
            Console.WriteLine("Hardware dashboard running. Press Ctrl+C to stop.");

            using CancellationTokenSource cancellation = new();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            sampler.Sample();
            if (cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds))
            {
                return;
            }

            byte[][] lastImages = new byte[SdMonitorMiniConstants.KeyCount][];
            try
            {
                int renderedFrames = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    DashboardMetric[] metrics = sampler.Sample();
                    for (int key = 0; key < metrics.Length; key++)
                    {
                        DashboardMetric metric = metrics[key];
                        byte[] image = KeyTileRenderer.RenderTile(metric.Title, metric.Value, metric.Percent, metric.Accent);
                        if (lastImages[key] is not null && image.AsSpan().SequenceEqual(lastImages[key]))
                        {
                            continue;
                        }

                        deck.SetKeyImage(key, image);
                        lastImages[key] = image;
                        Thread.Sleep(15);
                    }

                    PrintConsole(metrics);
                    renderedFrames++;

                    if (frames.HasValue && renderedFrames >= frames.Value)
                    {
                        Console.WriteLine();
                        break;
                    }

                    if (cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds))
                    {
                        break;
                    }
                }
            }
            finally
            {
                deck.ClearKeys();
                Console.WriteLine();
                Console.WriteLine("Dashboard stopped. Keys cleared.");
            }
        }

        private static void PrintConsole(IReadOnlyList<DashboardMetric> metrics)
        {
            Console.Write("\r");
            Console.Write(string.Join("  ", metrics.Select(metric => $"{metric.Title}:{metric.Value}")).PadRight(80));
        }
    }
}
