using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SDMonitor.Devices;
using SDMonitor.Rendering;

namespace SDMonitor.Dashboard
{
    public static class DashboardRunner
    {
        public static void Run(MonitorMini deck, DashboardConfig config, int? frames)
        {
            var sampler = HardwareSampler.Create();
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

            var lastImages = new byte[MonitorMiniConstants.KeyCount][];
            var lastRendered = new DateTimeOffset[MonitorMiniConstants.KeyCount];
            try
            {
                var renderedFrames = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    var now = DateTimeOffset.UtcNow;
                    var metrics = sampler.Sample();
                    var metricsByName = metrics.ToDictionary(
                        metric => metric.Metric,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var tile in config.Tiles)
                    {
                        var key = tile.Position - 1;
                        if (lastRendered[key] != default &&
                            now - lastRendered[key] < TimeSpan.FromMilliseconds(tile.RefreshMilliseconds))
                        {
                            continue;
                        }

                        if (!metricsByName.TryGetValue(tile.Metric, out var metric))
                        {
                            continue;
                        }

                        var title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                        var image = KeyTileRenderer.RenderTile(title, metric.Value, metric.Percent, tile.StyleFor(metric.Percent));
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
            var sampler = HardwareSampler.Create();
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

            var renderedFrames = 0;
            while (!cancellation.IsCancellationRequested)
            {
                var metrics = sampler.Sample();
                var metricsByName = metrics.ToDictionary(
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
                if (!metricsByName.TryGetValue(tile.Metric, out var metric))
                {
                    return $"{tile.Title}:N/A";
                }

                var title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                return $"{title}:{metric.Value}";
            })).PadRight(80));
        }
    }
}
