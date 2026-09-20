using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SDMonitor
{
    internal sealed class DashboardConfig
    {
        public const string DefaultFileName = "dashboard-tiles.json";

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

        internal static DashboardConfig Load(string? path, int? refreshOverrideMilliseconds, string defaultConfigDirectory)
        {
            DashboardTilesFile file = LoadFile(path, defaultConfigDirectory);
            List<DashboardTile> tiles = new();
            HashSet<int> positions = new();

            foreach (DashboardTileJson tile in file.Tiles)
            {
                DashboardTile dashboardTile = tile.ToDashboardTile(refreshOverrideMilliseconds);
                if (!positions.Add(dashboardTile.Position))
                {
                    throw new InvalidOperationException($"Duplicate dashboard tile position: {dashboardTile.Position}.");
                }

                tiles.Add(dashboardTile);
            }

            return new DashboardConfig(tiles.OrderBy(tile => tile.Position).ToArray());
        }

        internal static string GetDefaultConfigDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
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

        private static DashboardTilesFile LoadFile(string? path, string? defaultConfigDirectory = null)
        {
            string resolvedPath = ResolvePath(path, defaultConfigDirectory);
            if (!File.Exists(resolvedPath))
            {
                CreateDefaultFile(resolvedPath);
            }

            string json = File.ReadAllText(resolvedPath);
            DashboardTilesFile? file = JsonSerializer.Deserialize<DashboardTilesFile>(json, s_jsonOptions);
            if (file is null || file.Tiles.Count == 0)
            {
                throw new InvalidOperationException($"Dashboard config '{resolvedPath}' must contain at least one tile.");
            }

            return file;
        }

        private static string ResolvePath(string? path, string? defaultConfigDirectory)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Dashboard config file not found: {path}", path);
                }

                return path;
            }

            return Path.Combine(defaultConfigDirectory ?? GetDefaultConfigDirectory(), DefaultFileName);
        }

        private static void CreateDefaultFile(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(DashboardTilesFile.Default(), s_jsonOptions);
            File.WriteAllText(path, json);
        }
    }

    internal sealed record DashboardTile(
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

            DashboardThreshold? threshold = Thresholds.FirstOrDefault(value => value.Matches(percent.Value));
            return threshold is null ? Style : threshold.Apply(Style);
        }
    }

    internal sealed record DashboardThreshold(
        double? MinPercent,
        double? MaxPercent,
        int? TitleSize,
        int? ValueSize,
        RgbColor? BorderColor,
        RgbColor? BackgroundColor,
        RgbColor? FontColor,
        RgbColor? TitleColor,
        RgbColor? ValueColor)
    {
        public bool Matches(double percent)
        {
            return (!MinPercent.HasValue || percent >= MinPercent.Value) &&
                (!MaxPercent.HasValue || percent <= MaxPercent.Value);
        }

        public TileRenderStyle Apply(TileRenderStyle style)
        {
            RgbColor? fontColor = FontColor;
            return style with
            {
                TitleSize = TitleSize ?? style.TitleSize,
                ValueSize = ValueSize ?? style.ValueSize,
                BorderColor = BorderColor ?? style.BorderColor,
                BackgroundColor = BackgroundColor ?? style.BackgroundColor,
                TitleColor = TitleColor ?? fontColor ?? style.TitleColor,
                ValueColor = ValueColor ?? fontColor ?? style.ValueColor
            };
        }
    }

    internal sealed class DashboardTilesFile
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

    internal sealed class DashboardTileJson
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
            ProgressColor = accent
        };

        public DashboardTile ToDashboardTile(int? refreshOverrideMilliseconds)
        {
            string normalizedMetric = Metric.Trim().ToLowerInvariant();
            if (!s_metrics.Contains(normalizedMetric))
            {
                throw new InvalidOperationException($"Invalid dashboard tile metric: {Metric}.");
            }

            if (!Font.Equals("5x7", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported dashboard tile font: {Font}.");
            }

            int position = RequireRange(nameof(Position), Position, 1, MonitorMiniConstants.KeyCount);
            int titleSize = RequireRange(nameof(TitleSize), TitleSize, 1, 4);
            int valueSize = RequireRange(nameof(ValueSize), ValueSize, 1, 5);
            int refreshMilliseconds = refreshOverrideMilliseconds ?? RequireRange(nameof(RefreshMilliseconds), RefreshMilliseconds, 250, 60000);
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
            IReadOnlyList<DashboardThreshold> thresholds = ThresholdsEnabled
                ? Thresholds.Select(threshold => threshold.ToDashboardThreshold()).ToArray()
                : [];

            return new DashboardTile(normalizedMetric, Title.Trim(), position, refreshMilliseconds, style, thresholds);
        }

        internal static int RequireRange(string name, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                throw new InvalidOperationException($"Invalid dashboard tile {name}: {value}. Expected integer from {min} to {max}.");
            }

            return value;
        }

        internal static RgbColor ParseColor(string value)
        {
            string color = value.Trim();
            if (color.StartsWith('#'))
            {
                color = color[1..];
            }

            if (color.Length != 6 ||
                !byte.TryParse(color[..2], System.Globalization.NumberStyles.HexNumber, null, out byte red) ||
                !byte.TryParse(color.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte green) ||
                !byte.TryParse(color.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte blue))
            {
                throw new InvalidOperationException($"Invalid RGB color: {value}.");
            }

            return new RgbColor(red, green, blue);
        }
    }

    internal sealed class DashboardThresholdJson
    {
        public double? MinPercent { get; init; }

        public double? MaxPercent { get; init; }

        public int? TitleSize { get; init; }

        public int? ValueSize { get; init; }

        public string? BorderColor { get; init; }

        public string? BackgroundColor { get; init; }

        public string? FontColor { get; init; }

        public string? TitleColor { get; init; }

        public string? ValueColor { get; init; }

        public DashboardThreshold ToDashboardThreshold()
        {
            double? minPercent = RequirePercent(nameof(MinPercent), MinPercent);
            double? maxPercent = RequirePercent(nameof(MaxPercent), MaxPercent);
            if (!minPercent.HasValue && !maxPercent.HasValue)
            {
                throw new InvalidOperationException("Dashboard tile threshold must define MinPercent or MaxPercent.");
            }

            if (minPercent.HasValue && maxPercent.HasValue && minPercent.Value > maxPercent.Value)
            {
                throw new InvalidOperationException("Dashboard tile threshold MinPercent cannot be greater than MaxPercent.");
            }

            return new DashboardThreshold(
                minPercent,
                maxPercent,
                RequireRange(nameof(TitleSize), TitleSize, 1, 4),
                RequireRange(nameof(ValueSize), ValueSize, 1, 5),
                ParseOptionalColor(BorderColor),
                ParseOptionalColor(BackgroundColor),
                ParseOptionalColor(FontColor),
                ParseOptionalColor(TitleColor),
                ParseOptionalColor(ValueColor));
        }

        private static double? RequirePercent(string name, double? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value < 0 || value.Value > 100)
            {
                throw new InvalidOperationException($"Invalid dashboard tile threshold {name}: {value.Value}. Expected number from 0 to 100.");
            }

            return value.Value;
        }

        private static int? RequireRange(string name, int? value, int min, int max)
        {
            return value.HasValue ? DashboardTileJson.RequireRange(name, value.Value, min, max) : null;
        }

        private static RgbColor? ParseOptionalColor(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : DashboardTileJson.ParseColor(value);
        }
    }
}
