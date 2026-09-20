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
                  "refreshMilliseconds": 1500
                }
              ],
            }
            """);

            DashboardConfig config = DashboardConfig.Load(path, refreshOverrideMilliseconds: 500);

            Assert.Equal(2, config.Tiles.Count);
            Assert.Equal(500, config.RefreshIntervalMilliseconds);
            Assert.Equal("cpu", config.Tiles[0].Metric);
            Assert.Equal("CPU", config.Tiles[0].Title);
            Assert.Equal("ram", config.Tiles[1].Metric);
            Assert.Equal("Memory", config.Tiles[1].Title);
            Assert.Equal(new RgbColor(0x78, 0xDC, 0x50), config.Tiles[1].Style.BorderColor);
        }

        [Fact]
        public void LoadUsesDefaultTilesWhenNoConfigFileExists()
        {
            string originalDirectory = Environment.CurrentDirectory;
            using TempDirectory temp = new();

            try
            {
                Environment.CurrentDirectory = temp.Path;

                DashboardConfig config = DashboardConfig.Load(null, null);

                Assert.Equal(MonitorMiniConstants.KeyCount, config.Tiles.Count);
                Assert.Equal(1500, config.RefreshIntervalMilliseconds);
                Assert.Equal("cpu", config.Tiles[0].Metric);
                Assert.Equal("download", config.Tiles[5].Metric);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }
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
