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
        private readonly IHardwareSampler _sampler;
        private readonly DateTimeOffset[] _lastUpdated = new DateTimeOffset[MonitorMiniConstants.KeyCount];
        private DashboardConfig _config;

        public DashboardSimulationService()
        {
            _sampler = HardwareSampler.Create();
            _config = LoadConfig();
        }

        public int RefreshIntervalMilliseconds => _config.RefreshIntervalMilliseconds;

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

            ApplyConfiguredTiles(keys);
            return keys;
        }

        public DashboardUpdate ReloadPreferences(IReadOnlyList<StreamDeckKeyViewModel> keys)
        {
            DashboardConfig config = LoadConfig();
            _config = config;
            Array.Clear(_lastUpdated);
            ResetKeys(keys);
            ApplyConfiguredTiles(keys);
            return Update(keys, force: true, statusPrefix: "Preferences reloaded");
        }

        public DashboardUpdate Update(IReadOnlyList<StreamDeckKeyViewModel> keys)
        {
            return Update(keys, force: false, statusPrefix: "Mirroring");
        }

        private static DashboardConfig LoadConfig()
        {
            return DashboardConfig.Load(string.Empty, null);
        }

        private void ApplyConfiguredTiles(IReadOnlyList<StreamDeckKeyViewModel> keys)
        {
            foreach (DashboardTile tile in _config.Tiles)
            {
                var key = keys[tile.Position - 1];
                ApplyStyle(key, tile.Style);
                key.Title = string.IsNullOrWhiteSpace(tile.Title) ? tile.Metric.ToUpperInvariant() : tile.Title;
            }
        }

        private static void ResetKeys(IReadOnlyList<StreamDeckKeyViewModel> keys)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                key.Title = $"KEY {index + 1}";
                key.Value = "N/A";
                key.Percent = 0;
                key.TitleFontSize = 13;
                key.ValueFontSize = 24;
                key.ProgressHeight = 7;
                key.BackgroundBrush = StreamDeckKeyViewModel.Brush("#080A0E");
                key.BorderBrush = StreamDeckKeyViewModel.Brush("#222832");
                key.ProgressBrush = StreamDeckKeyViewModel.Brush("#303846");
                key.TitleBrush = StreamDeckKeyViewModel.Brush("#AAB4BE");
                key.ValueBrush = StreamDeckKeyViewModel.Brush("#F5F8FA");
            }
        }

        private DashboardUpdate Update(IReadOnlyList<StreamDeckKeyViewModel> keys, bool force, string statusPrefix)
        {
            var now = DateTimeOffset.UtcNow;
            DashboardMetric[] metrics = _sampler.Sample();
            Dictionary<string, DashboardMetric> metricsByName = metrics.ToDictionary(
                metric => metric.Metric,
                StringComparer.OrdinalIgnoreCase);

            foreach (DashboardTile tile in _config.Tiles)
            {
                var keyIndex = tile.Position - 1;
                if (!force && now - _lastUpdated[keyIndex] < TimeSpan.FromMilliseconds(tile.RefreshMilliseconds))
                {
                    continue;
                }

                if (!metricsByName.TryGetValue(tile.Metric, out DashboardMetric? metric))
                {
                    continue;
                }

                var key = keys[keyIndex];
                ApplyStyle(key, tile.StyleFor(metric.Percent));
                key.Title = string.IsNullOrWhiteSpace(tile.Title) ? metric.Title : tile.Title;
                key.Value = metric.Value;
                key.Percent = Math.Clamp(metric.Percent ?? 0, 0, 100);
                _lastUpdated[keyIndex] = now;
            }

            return new DashboardUpdate(
                $"{statusPrefix} {metrics.Length} live metrics",
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
