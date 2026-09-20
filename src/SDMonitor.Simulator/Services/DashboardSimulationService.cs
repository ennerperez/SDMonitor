using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using SDMonitor.Dashboard;
using SDMonitor.Devices;
using SDMonitor.Rendering;
using SDMonitor.Simulator.ViewModels;

namespace SDMonitor.Simulator.Services
{
    public sealed class DashboardSimulationService
    {
        private readonly DashboardConfig _config;
        private readonly IHardwareSampler _sampler;
        private readonly DateTimeOffset[] _lastUpdated = new DateTimeOffset[MonitorMiniConstants.KeyCount];

        public DashboardSimulationService()
        {
            _config = DashboardConfig.Load(string.Empty, null);
            _sampler = HardwareSampler.Create();
            RefreshIntervalMilliseconds = _config.RefreshIntervalMilliseconds;
        }

        public int RefreshIntervalMilliseconds { get; }

        public IReadOnlyList<StreamDeckKeyViewModel> CreateKeys()
        {
            var keys = Enumerable.Range(0, MonitorMiniConstants.KeyCount)
                .Select(index => new StreamDeckKeyViewModel
                {
                    Title = $"KEY {index + 1}",
                    Value = "N/A",
                    Percent = 0,
                    BorderBrush = StreamDeckKeyViewModel.Brush("#222832"),
                    ProgressBrush = StreamDeckKeyViewModel.Brush("#303846")
                })
                .ToArray();

            foreach (DashboardTile tile in _config.Tiles)
            {
                var key = keys[tile.Position - 1];
                ApplyStyle(key, tile.Style);
                key.Title = string.IsNullOrWhiteSpace(tile.Title) ? tile.Metric.ToUpperInvariant() : tile.Title;
            }

            return keys;
        }

        public DashboardUpdate Update(IReadOnlyList<StreamDeckKeyViewModel> keys)
        {
            var now = DateTimeOffset.UtcNow;
            DashboardMetric[] metrics = _sampler.Sample();
            Dictionary<string, DashboardMetric> metricsByName = metrics.ToDictionary(
                metric => metric.Metric,
                StringComparer.OrdinalIgnoreCase);

            foreach (DashboardTile tile in _config.Tiles)
            {
                var keyIndex = tile.Position - 1;
                if (now - _lastUpdated[keyIndex] < TimeSpan.FromMilliseconds(tile.RefreshMilliseconds))
                {
                    continue;
                }

                if (!metricsByName.TryGetValue(tile.Metric, out DashboardMetric? metric))
                {
                    continue;
                }

                var key = keys[keyIndex];
                ApplyStyle(key, tile.Style);
                key.Title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                key.Value = metric.Value;
                key.Percent = Math.Clamp(metric.Percent ?? 0, 0, 100);
                _lastUpdated[keyIndex] = now;
            }

            return new DashboardUpdate(
                $"Mirroring {metrics.Length} live metrics",
                $"{DateTimeOffset.Now:HH:mm:ss}");
        }

        private static void ApplyStyle(StreamDeckKeyViewModel key, TileRenderStyle style)
        {
            key.TitleFontSize = ScaleToFontSize(style.TitleSize, baseSize: 13);
            key.ValueFontSize = ScaleToFontSize(style.ValueSize, baseSize: 12);
            key.ProgressHeight = Math.Max(3, style.ProgressBarHeightPercent / 100.0 * 80.0);
            key.BackgroundBrush = Brush(style.BackgroundColor);
            key.BorderBrush = Brush(style.BorderColor);
            key.ProgressBrush = Brush(style.ProgressColor);
            key.TitleBrush = Brush(style.TitleColor);
            key.ValueBrush = Brush(style.ValueColor);
        }

        private static double ScaleToFontSize(int scale, int baseSize)
        {
            return baseSize + (scale - 1) * 8;
        }

        private static SolidColorBrush Brush(RgbColor color)
        {
            return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        }
    }

    public sealed record DashboardUpdate(string StatusText, string RefreshText);
}
