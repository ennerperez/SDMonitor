using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SDMonitor
{
    internal static class DashboardRunner
    {
        public static void Run(MonitorMini deck, DashboardConfig config, int? frames)
        {
            IHardwareSampler sampler = HardwareSampler.Create();
            Console.WriteLine("Dashboard running.");

            using CancellationTokenSource cancellation = new();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            sampler.Sample();
            if (cancellation.Token.WaitHandle.WaitOne(config.RefreshIntervalMilliseconds))
            {
                return;
            }

            byte[][] lastImages = new byte[MonitorMiniConstants.KeyCount][];
            DateTimeOffset[] lastRendered = new DateTimeOffset[MonitorMiniConstants.KeyCount];
            try
            {
                int renderedFrames = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    DashboardMetric[] metrics = sampler.Sample();
                    Dictionary<string, DashboardMetric> metricsByName = metrics.ToDictionary(
                        metric => metric.Metric,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (DashboardTile tile in config.Tiles)
                    {
                        int key = tile.Position - 1;
                        if (lastRendered[key] != default &&
                            now - lastRendered[key] < TimeSpan.FromMilliseconds(tile.RefreshMilliseconds))
                        {
                            continue;
                        }

                        if (!metricsByName.TryGetValue(tile.Metric, out DashboardMetric? metric))
                        {
                            continue;
                        }

                        string title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                        byte[] image = KeyTileRenderer.RenderTile(title, metric.Value, metric.Percent, tile.Style);
                        if (lastImages[key] is not null && image.AsSpan().SequenceEqual(lastImages[key]))
                        {
                            lastRendered[key] = now;
                            continue;
                        }

                        deck.SetKeyImage(key, image);
                        lastImages[key] = image;
                        lastRendered[key] = now;
                        Thread.Sleep(15);
                    }

                    PrintConsole(config, metricsByName);
                    renderedFrames++;

                    if (frames.HasValue && renderedFrames >= frames.Value)
                    {
                        Console.WriteLine();
                        break;
                    }

                    if (cancellation.Token.WaitHandle.WaitOne(config.RefreshIntervalMilliseconds))
                    {
                        break;
                    }
                }
            }
            finally
            {
                deck.ClearKeys();
                Console.WriteLine();
                Console.WriteLine("Dashboard stopped.");
            }
        }

        public static void RunConsoleOnly(DashboardConfig config, int? frames)
        {
            IHardwareSampler sampler = HardwareSampler.Create();
            Console.WriteLine("Dashboard running without device.");

            using CancellationTokenSource cancellation = new();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            sampler.Sample();
            if (cancellation.Token.WaitHandle.WaitOne(config.RefreshIntervalMilliseconds))
            {
                return;
            }

            int renderedFrames = 0;
            while (!cancellation.IsCancellationRequested)
            {
                DashboardMetric[] metrics = sampler.Sample();
                Dictionary<string, DashboardMetric> metricsByName = metrics.ToDictionary(
                    metric => metric.Metric,
                    StringComparer.OrdinalIgnoreCase);
                PrintConsole(config, metricsByName);
                renderedFrames++;

                if (frames.HasValue && renderedFrames >= frames.Value)
                {
                    Console.WriteLine();
                    break;
                }

                if (cancellation.Token.WaitHandle.WaitOne(config.RefreshIntervalMilliseconds))
                {
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Console dashboard stopped.");
        }

        private static void PrintConsole(DashboardConfig config, Dictionary<string, DashboardMetric> metricsByName)
        {
            Console.Write("\r");
            Console.Write(string.Join("  ", config.Tiles.Select(tile =>
            {
                if (!metricsByName.TryGetValue(tile.Metric, out DashboardMetric? metric))
                {
                    return $"{tile.Title}:N/A";
                }

                string title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                return $"{title}:{metric.Value}";
            })).PadRight(80));
        }
    }
}
