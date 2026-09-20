using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SDMonitor.Devices;
using SDMonitor.Rendering;

namespace SDMonitor.Dashboard
{
    public class DashboardConfig
    {
        public const string DefaultFileName = "preferences.json";

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        public DashboardConfig(IReadOnlyList<DashboardTile> tiles)
        {
            Tiles = tiles;
            RefreshIntervalMilliseconds = tiles.Count == 0
                ? 1500
                : tiles.Min(tile => tile.RefreshMilliseconds);
        }

        public IReadOnlyList<DashboardTile> Tiles { get; }

        public int RefreshIntervalMilliseconds { get; }

        public static DashboardConfig Load(string? path, int? refreshOverrideMilliseconds)
        {
            return Load(path, refreshOverrideMilliseconds, GetDefaultConfigDirectory());
        }

        public static DashboardConfig Load(string? path, int? refreshOverrideMilliseconds, string defaultConfigDirectory)
        {
            var file = LoadFile(path, defaultConfigDirectory);
            List<DashboardTile> tiles = new();
            HashSet<int> positions = new();

            foreach (var tile in file.Tiles)
            {
                var dashboardTile = tile.ToDashboardTile(refreshOverrideMilliseconds);
                if (!positions.Add(dashboardTile.Position))
                {
                    throw new InvalidOperationException($"Duplicate dashboard tile position: {dashboardTile.Position}.");
                }

                tiles.Add(dashboardTile);
            }

            return new DashboardConfig(tiles.OrderBy(tile => tile.Position).ToArray());
        }

        public static string GetDefaultConfigDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Environment.CurrentDirectory;
            }

            return Path.Combine(appData, "SDMonitor");
        }

        private static DashboardTilesFile LoadFile(string? path, string defaultConfigDirectory)
        {
            var resolvedPath = ResolvePath(path, defaultConfigDirectory);
            if (!File.Exists(resolvedPath))
            {
                CreateDefaultFile(resolvedPath);
            }

            var json = File.ReadAllText(resolvedPath);
            var file = JsonSerializer.Deserialize<DashboardTilesFile>(json, s_jsonOptions);
            if (file is null || file.Tiles.Count == 0)
            {
                throw new InvalidOperationException($"Dashboard config '{resolvedPath}' must contain at least one tile.");
            }

            return file;
        }

        private static string ResolvePath(string? path, string defaultConfigDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Path.Combine(defaultConfigDirectory ?? GetDefaultConfigDirectory(), DefaultFileName);
            }

            return !File.Exists(path) ? throw new FileNotFoundException($"Dashboard config file not found: {path}", path) : path;
        }

        private static void CreateDefaultFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(DashboardTilesFile.Default(), s_jsonOptions);
            File.WriteAllText(path, json);
        }
    }

    public record DashboardTile(
        string Metric,
        string Title,
        int Position,
        int RefreshMilliseconds,
        TileRenderStyle Style,
        IReadOnlyList<DashboardThreshold> Thresholds)
    {
        public TileRenderStyle StyleFor(double? percent)
        {
            if (!percent.HasValue)
            {
                return Style;
            }

            foreach (var threshold in Thresholds)
            {
                if (threshold.Matches(percent.Value))
                {
                    return threshold.ApplyTo(Style);
                }
            }

            return Style;
        }
    }

    public record DashboardThreshold(
        double? MinPercent,
        double? MaxPercent,
        int? TitleSize,
        int? ValueSize,
        RgbColor? BorderColor,
        RgbColor? ProgressColor,
        RgbColor? BackgroundColor,
        int? ProgressBarHeightPercent,
        int? MarginPercent,
        int? PaddingPercent,
        RgbColor? TitleColor,
        RgbColor? ValueColor)
    {
        public bool Matches(double percent)
        {
            if (MinPercent.HasValue && percent < MinPercent.Value)
            {
                return false;
            }

            return !MaxPercent.HasValue || percent <= MaxPercent.Value;
        }

        public TileRenderStyle ApplyTo(TileRenderStyle style) => style with
        {
            TitleSize = TitleSize ?? style.TitleSize,
            ValueSize = ValueSize ?? style.ValueSize,
            BorderColor = BorderColor ?? style.BorderColor,
            ProgressColor = ProgressColor ?? style.ProgressColor,
            BackgroundColor = BackgroundColor ?? style.BackgroundColor,
            ProgressBarHeightPercent = ProgressBarHeightPercent ?? style.ProgressBarHeightPercent,
            MarginPercent = MarginPercent ?? style.MarginPercent,
            PaddingPercent = PaddingPercent ?? style.PaddingPercent,
            TitleColor = TitleColor ?? style.TitleColor,
            ValueColor = ValueColor ?? style.ValueColor
        };
    }

    public class DashboardTilesFile
    {
        public List<DashboardTileJson> Tiles { get; init; } = [];

        public static DashboardTilesFile Default() => new()
        {
            Tiles =
            [
                DashboardTileJson.Default("cpu", "CPU", 1, "#00C8FF"),
                DashboardTileJson.Default("ram", "RAM", 2, "#78DC50"),
                DashboardTileJson.Default("gpu", "GPU", 3, "#AA82FF"),
                DashboardTileJson.Default("disk", "HDD", 4, "#FFBE46"),
                DashboardTileJson.Default("upload", "UP", 5, "#FF6464"),
                DashboardTileJson.Default("download", "DOWN", 6, "#50AAFF")
            ]
        };
    }

    public sealed class DashboardTileJson
    {
        private static readonly HashSet<string> s_metrics = new(StringComparer.OrdinalIgnoreCase)
        {
            "cpu",
            "ram",
            "gpu",
            "disk",
            "upload",
            "download"
        };

        public string Metric { get; init; } = "";

        public string Font { get; init; } = "5x7";

        public int TitleSize { get; init; } = 1;

        public int ValueSize { get; init; } = 2;

        public string Title { get; init; } = "";

        public string BorderColor { get; init; } = "#00C8FF";

        public string ProgressColor { get; init; } = "#00C8FF";

        public string BackgroundColor { get; init; } = "#080A0E";

        public int ProgressBarHeightPercent { get; init; } = 8;

        public int MarginPercent { get; init; }

        public int PaddingPercent { get; init; } = 10;

        public int RefreshMilliseconds { get; init; } = 1500;

        public string TitleColor { get; init; } = "#AAB4BE";

        public string ValueColor { get; init; } = "#F5F8FA";

        public int Position { get; init; }

        public bool ThresholdsEnabled { get; init; }

        public List<DashboardThresholdJson> Thresholds { get; init; } = [];

        public static DashboardTileJson Default(string metric, string title, int position, string accent) => new()
        {
            Metric = metric,
            Title = title,
            Position = position,
            BorderColor = accent,
            ProgressColor = accent,
            ThresholdsEnabled = true,
            Thresholds = DefaultThresholds(accent)
        };

        private static List<DashboardThresholdJson> DefaultThresholds(string accent)
        {
            var color = ParseColor(accent);
            return
            [
                DefaultThreshold(color, 0, 49.99, 0.10),
                DefaultThreshold(color, 50, 74.99, 0.18),
                DefaultThreshold(color, 75, 89.99, 0.28),
                DefaultThreshold(color, 90, null, 0.42)
            ];
        }

        private static DashboardThresholdJson DefaultThreshold(RgbColor color, double minPercent, double? maxPercent, double intensity)
        {
            return new DashboardThresholdJson
            {
                MinPercent = minPercent,
                MaxPercent = maxPercent,
                BorderColor = ToHex(Lighten(color, intensity / 2)),
                ProgressColor = ToHex(Lighten(color, intensity / 2)),
                BackgroundColor = ToHex(Darken(color, intensity)),
                FontColor = "#FFFFFF"
            };
        }

        private static RgbColor Darken(RgbColor color, double intensity)
        {
            return new RgbColor(
                checked((byte)Math.Clamp(Math.Round(color.Red * intensity), 0, 255)),
                checked((byte)Math.Clamp(Math.Round(color.Green * intensity), 0, 255)),
                checked((byte)Math.Clamp(Math.Round(color.Blue * intensity), 0, 255)));
        }

        private static RgbColor Lighten(RgbColor color, double intensity)
        {
            return new RgbColor(
                checked((byte)Math.Clamp(Math.Round(color.Red + ((255 - color.Red) * intensity)), 0, 255)),
                checked((byte)Math.Clamp(Math.Round(color.Green + ((255 - color.Green) * intensity)), 0, 255)),
                checked((byte)Math.Clamp(Math.Round(color.Blue + ((255 - color.Blue) * intensity)), 0, 255)));
        }

        private static string ToHex(RgbColor color)
        {
            return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
        }

        public DashboardTile ToDashboardTile(int? refreshOverrideMilliseconds)
        {
            var normalizedMetric = Metric.Trim().ToLowerInvariant();
            if (!s_metrics.Contains(normalizedMetric))
            {
                throw new InvalidOperationException($"Invalid dashboard tile metric: {Metric}.");
            }

            if (!Font.Equals("5x7", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported dashboard tile font: {Font}.");
            }

            var position = RequireRange(nameof(Position), Position, 1, MonitorMiniConstants.KeyCount);
            var titleSize = RequireRange(nameof(TitleSize), TitleSize, 1, 4);
            var valueSize = RequireRange(nameof(ValueSize), ValueSize, 1, 5);
            var refreshMilliseconds = refreshOverrideMilliseconds ?? RequireRange(nameof(RefreshMilliseconds), RefreshMilliseconds, 250, 60000);
            TileRenderStyle style = new(
                Font,
                titleSize,
                valueSize,
                ParseColor(BorderColor),
                ParseColor(ProgressColor),
                ParseColor(BackgroundColor),
                RequireRange(nameof(ProgressBarHeightPercent), ProgressBarHeightPercent, 1, 50),
                RequireRange(nameof(MarginPercent), MarginPercent, 0, 30),
                RequireRange(nameof(PaddingPercent), PaddingPercent, 0, 30),
                ParseColor(TitleColor),
                ParseColor(ValueColor));

            var thresholds = ThresholdsEnabled
                ? Thresholds.Select(threshold => threshold.ToDashboardThreshold()).ToArray()
                : [];

            return new DashboardTile(normalizedMetric, Title.Trim(), position, refreshMilliseconds, style, thresholds);
        }

        public static int RequireRange(string name, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                throw new InvalidOperationException($"Invalid dashboard tile {name}: {value}. Expected integer from {min} to {max}.");
            }

            return value;
        }

        public static int? RequireOptionalRange(string name, int? value, int min, int max)
        {
            return value.HasValue ? RequireRange(name, value.Value, min, max) : null;
        }

        public static RgbColor ParseColor(string value)
        {
            var color = value.Trim();
            if (color.StartsWith('#'))
            {
                color = color[1..];
            }

            if (color.Length != 6 ||
                !byte.TryParse(color[..2], System.Globalization.NumberStyles.HexNumber, null, out var red) ||
                !byte.TryParse(color.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
                !byte.TryParse(color.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
            {
                throw new InvalidOperationException($"Invalid RGB color: {value}.");
            }

            return new RgbColor(red, green, blue);
        }

        public static RgbColor? ParseOptionalColor(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : ParseColor(value);
        }
    }

    public class DashboardThresholdJson
    {
        public double? MinPercent { get; init; }

        public double? MaxPercent { get; init; }

        public int? TitleSize { get; init; }

        public int? ValueSize { get; init; }

        public string? BorderColor { get; init; }

        public string? ProgressColor { get; init; }

        public string? BackgroundColor { get; init; }

        public int? ProgressBarHeightPercent { get; init; }

        public int? MarginPercent { get; init; }

        public int? PaddingPercent { get; init; }

        public string? FontColor { get; init; }

        public string? TitleColor { get; init; }

        public string? ValueColor { get; init; }

        public DashboardThreshold ToDashboardThreshold()
        {
            switch (MinPercent)
            {
                case null when !MaxPercent.HasValue:
                    throw new InvalidOperationException("Dashboard threshold must define minPercent or maxPercent.");
                case < 0 or > 100:
                    throw new InvalidOperationException($"Invalid dashboard threshold {nameof(MinPercent)}: {MinPercent}. Expected number from 0 to 100.");
            }

            if (MaxPercent is < 0 or > 100)
            {
                throw new InvalidOperationException($"Invalid dashboard threshold {nameof(MaxPercent)}: {MaxPercent}. Expected number from 0 to 100.");
            }

            if (MinPercent.HasValue && MaxPercent.HasValue && MinPercent.Value > MaxPercent.Value)
            {
                throw new InvalidOperationException("Dashboard threshold minPercent cannot be greater than maxPercent.");
            }

            var fontColor = DashboardTileJson.ParseOptionalColor(FontColor);
            return new DashboardThreshold(
                MinPercent,
                MaxPercent,
                DashboardTileJson.RequireOptionalRange(nameof(TitleSize), TitleSize, 1, 4),
                DashboardTileJson.RequireOptionalRange(nameof(ValueSize), ValueSize, 1, 5),
                DashboardTileJson.ParseOptionalColor(BorderColor),
                DashboardTileJson.ParseOptionalColor(ProgressColor),
                DashboardTileJson.ParseOptionalColor(BackgroundColor),
                DashboardTileJson.RequireOptionalRange(nameof(ProgressBarHeightPercent), ProgressBarHeightPercent, 1, 50),
                DashboardTileJson.RequireOptionalRange(nameof(MarginPercent), MarginPercent, 0, 30),
                DashboardTileJson.RequireOptionalRange(nameof(PaddingPercent), PaddingPercent, 0, 30),
                DashboardTileJson.ParseOptionalColor(TitleColor) ?? fontColor,
                DashboardTileJson.ParseOptionalColor(ValueColor) ?? fontColor);
        }
    }
}
