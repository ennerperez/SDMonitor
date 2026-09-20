using System;
using System.IO;
using Xunit;

namespace SDMonitor.Tests
{
    public sealed class DashboardConfigTests
    {
        [Fact]
        public void LoadReadsJsonOrdersTilesAndAppliesRefreshOverride()
        {
            string path = WriteTempConfig("""
            {
              // comments and trailing commas are allowed
              "tiles": [
                {
                  "metric": "RAM",
                  "title": " Memory ",
                  "position": 2,
                  "refreshMilliseconds": 2000,
                  "borderColor": "78DC50",
                  "progressColor": "#78DC50",
                  "backgroundColor": "#080A0E",
                  "titleColor": "#AAB4BE",
                  "valueColor": "#F5F8FA",
                },
                {
                  "metric": "cpu",
                  "title": "CPU",
                  "position": 1,
                  "refreshMilliseconds": 1500,
                  "thresholdsEnabled": true,
                  "thresholds": [
                    {
                      "minPercent": 80,
                      "titleSize": 2,
                      "valueSize": 3,
                      "borderColor": "#FF0000",
                      "backgroundColor": "#110000",
                      "fontColor": "#FFFFFF",
                      "valueColor": "#FFFF00"
                    }
                  ]
                }
              ],
            }
            """);

            DashboardConfig config = DashboardConfig.Load(path, refreshOverrideMilliseconds: 500);

            Assert.Equal(2, config.Tiles.Count);
            Assert.Equal(500, config.RefreshIntervalMilliseconds);
            Assert.Equal("cpu", config.Tiles[0].Metric);
            Assert.Equal("CPU", config.Tiles[0].Title);
            Assert.Equal(1, config.Tiles[0].Thresholds.Count);
            Assert.Equal("ram", config.Tiles[1].Metric);
            Assert.Equal("Memory", config.Tiles[1].Title);
            Assert.Equal(new RgbColor(0x78, 0xDC, 0x50), config.Tiles[1].Style.BorderColor);
        }

        [Fact]
        public void TileStyleForAppliesFirstMatchingEnabledThreshold()
        {
            DashboardTile tile = new DashboardTileJson
            {
                Metric = "cpu",
                Title = "CPU",
                Position = 1,
                BorderColor = "#00C8FF",
                BackgroundColor = "#080A0E",
                TitleColor = "#AAB4BE",
                ValueColor = "#F5F8FA",
                ThresholdsEnabled = true,
                Thresholds =
                [
                    new()
                    {
                        MinPercent = 80,
                        TitleSize = 2,
                        ValueSize = 3,
                        BorderColor = "#FF0000",
                        BackgroundColor = "#110000",
                        FontColor = "#FFFFFF",
                        ValueColor = "#FFFF00"
                    }
                ]
            }.ToDashboardTile(null);

            TileRenderStyle baseStyle = tile.StyleFor(50);
            TileRenderStyle thresholdStyle = tile.StyleFor(85);

            Assert.Equal(1, baseStyle.TitleSize);
            Assert.Equal(new RgbColor(0x00, 0xC8, 0xFF), baseStyle.BorderColor);
            Assert.Equal(2, thresholdStyle.TitleSize);
            Assert.Equal(3, thresholdStyle.ValueSize);
            Assert.Equal(new RgbColor(0xFF, 0x00, 0x00), thresholdStyle.BorderColor);
            Assert.Equal(new RgbColor(0x11, 0x00, 0x00), thresholdStyle.BackgroundColor);
            Assert.Equal(new RgbColor(0xFF, 0xFF, 0xFF), thresholdStyle.TitleColor);
            Assert.Equal(new RgbColor(0xFF, 0xFF, 0x00), thresholdStyle.ValueColor);
        }

        [Fact]
        public void TileStyleForIgnoresThresholdsWhenDisabled()
        {
            DashboardTile tile = new DashboardTileJson
            {
                Metric = "cpu",
                Position = 1,
                ThresholdsEnabled = false,
                Thresholds =
                [
                    new()
                    {
                        MinPercent = 1,
                        BorderColor = "#FF0000"
                    }
                ]
            }.ToDashboardTile(null);

            Assert.Empty(tile.Thresholds);
            Assert.Equal(new RgbColor(0x00, 0xC8, 0xFF), tile.StyleFor(100).BorderColor);
        }

        [Fact]
        public void LoadCreatesDefaultTilesFileWhenNoConfigFileExists()
        {
            using TempDirectory temp = new();
            string defaultConfigDirectory = Path.Combine(temp.Path, "SDMonitor");

            DashboardConfig config = DashboardConfig.Load(null, null, defaultConfigDirectory);

            string path = Path.Combine(defaultConfigDirectory, DashboardConfig.DefaultFileName);
            Assert.True(Directory.Exists(defaultConfigDirectory));
            Assert.True(File.Exists(path));
            Assert.Equal(MonitorMiniConstants.KeyCount, config.Tiles.Count);
            Assert.Equal(1500, config.RefreshIntervalMilliseconds);
            Assert.Equal("cpu", config.Tiles[0].Metric);
            Assert.Equal("download", config.Tiles[5].Metric);
        }

        [Fact]
        public void DefaultTilesFileCreatesExpectedTiles()
        {
            DashboardTilesFile file = DashboardTilesFile.Default();

            Assert.Equal(MonitorMiniConstants.KeyCount, file.Tiles.Count);
            Assert.Equal(["cpu", "ram", "gpu", "disk", "upload", "download"], file.Tiles.ConvertAll(tile => tile.Metric));
            Assert.Equal([1, 2, 3, 4, 5, 6], file.Tiles.ConvertAll(tile => tile.Position));
            Assert.All(file.Tiles, tile => Assert.Equal(tile.BorderColor, tile.ProgressColor));
        }

        [Fact]
        public void LoadRejectsMissingEmptyAndDuplicatePositionFiles()
        {
            Assert.Throws<FileNotFoundException>(() => DashboardConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"), null));
            Assert.Throws<InvalidOperationException>(() => DashboardConfig.Load(WriteTempConfig("""{"tiles": []}"""), null));
            Assert.Throws<InvalidOperationException>(() => DashboardConfig.Load(WriteTempConfig("""
            {
              "tiles": [
                { "metric": "cpu", "title": "CPU", "position": 1 },
                { "metric": "ram", "title": "RAM", "position": 1 }
              ]
            }
            """), null));
        }

        [Fact]
        public void TileJsonValidationRejectsBadValues()
        {
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "temp", Position = 1 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Font = "8x8", Position = 1 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 0 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, TitleSize = 5 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, ValueSize = 6 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, RefreshMilliseconds = 249 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, ProgressBarHeightPercent = 0 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, MarginPercent = 31 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, PaddingPercent = 31 }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardTileJson { Metric = "cpu", Position = 1, BorderColor = "xyz" }.ToDashboardTile(null));
            Assert.Throws<InvalidOperationException>(() => new DashboardThresholdJson { MinPercent = 101 }.ToDashboardThreshold());
            Assert.Throws<InvalidOperationException>(() => new DashboardThresholdJson { MinPercent = 80, MaxPercent = 70 }.ToDashboardThreshold());
            Assert.Throws<InvalidOperationException>(() => new DashboardThresholdJson().ToDashboardThreshold());
            Assert.Throws<InvalidOperationException>(() => new DashboardThresholdJson { MinPercent = 80, TitleSize = 5 }.ToDashboardThreshold());
        }

        private static string WriteTempConfig(string json)
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            return path;
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
