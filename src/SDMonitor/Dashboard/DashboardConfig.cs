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
            AllowTrailingCommas = true
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
            DashboardTilesFile file = LoadFile(path);
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

        private static DashboardTilesFile LoadFile(string? path)
        {
            string? resolvedPath = ResolvePath(path);
            if (resolvedPath is null)
            {
                return DashboardTilesFile.Default();
            }

            string json = File.ReadAllText(resolvedPath);
            DashboardTilesFile? file = JsonSerializer.Deserialize<DashboardTilesFile>(json, s_jsonOptions);
            if (file is null || file.Tiles.Count == 0)
            {
                throw new InvalidOperationException($"Dashboard config '{resolvedPath}' must contain at least one tile.");
            }

            return file;
        }

        private static string? ResolvePath(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Dashboard config file not found: {path}", path);
                }

                return path;
            }

            string currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, DefaultFileName);
            if (File.Exists(currentDirectoryPath))
            {
                return currentDirectoryPath;
            }

            string executableDirectoryPath = Path.Combine(AppContext.BaseDirectory, DefaultFileName);
            return File.Exists(executableDirectoryPath) ? executableDirectoryPath : null;
        }
    }

    internal sealed record DashboardTile(
        string Metric,
        string Title,
        int Position,
        int RefreshMilliseconds,
        TileRenderStyle Style);

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

            return new DashboardTile(normalizedMetric, Title.Trim(), position, refreshMilliseconds, style);
        }

        private static int RequireRange(string name, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                throw new InvalidOperationException($"Invalid dashboard tile {name}: {value}. Expected integer from {min} to {max}.");
            }

            return value;
        }

        private static RgbColor ParseColor(string value)
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
}
