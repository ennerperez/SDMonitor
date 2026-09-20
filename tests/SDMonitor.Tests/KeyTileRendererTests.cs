using Xunit;

namespace SDMonitor.Tests
{
    public sealed class KeyTileRendererTests
    {
        [Fact]
        public void RenderTileReturnsMiniSizedBmpForPercentAndNoPercentTiles()
        {
            byte[] percentTile = KeyTileRenderer.RenderTile("CPU", "55%", 55, new RgbColor(0, 200, 255));
            byte[] noPercentTile = KeyTileRenderer.RenderTile("GPU?", "N/A", null, TileRenderStyle.Default(new RgbColor(170, 130, 255)));

            Assert.Equal((byte)'B', percentTile[0]);
            Assert.Equal((byte)'M', percentTile[1]);
            Assert.Equal(54 + MonitorMiniConstants.KeyImageWidth * MonitorMiniConstants.KeyImageHeight * 3, percentTile.Length);
            Assert.Equal(percentTile.Length, noPercentTile.Length);
            Assert.NotEqual(percentTile, noPercentTile);
        }

        [Fact]
        public void RenderTileClampsPercentAndFitsLongText()
        {
            TileRenderStyle style = new(
                "5x7",
                4,
                5,
                new RgbColor(1, 2, 3),
                new RgbColor(4, 5, 6),
                new RgbColor(7, 8, 9),
                50,
                30,
                30,
                new RgbColor(10, 11, 12),
                new RgbColor(13, 14, 15));

            byte[] low = KeyTileRenderer.RenderTile("long_title_123", "very-long-value", -10, style);
            byte[] high = KeyTileRenderer.RenderTile("long_title_123", "very-long-value", 110, style);

            Assert.Equal(low.Length, high.Length);
            Assert.NotEqual(low, high);
        }

        [Fact]
        public void RenderLabelTileReturnsMiniSizedBmp()
        {
            byte[] tile = KeyTileRenderer.RenderLabelTile("down", new RgbColor(80, 170, 255));

            Assert.Equal((byte)'B', tile[0]);
            Assert.Equal((byte)'M', tile[1]);
            Assert.Equal(54 + MonitorMiniConstants.KeyImageWidth * MonitorMiniConstants.KeyImageHeight * 3, tile.Length);
        }
    }
}
