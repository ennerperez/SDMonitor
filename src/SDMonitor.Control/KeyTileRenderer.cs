using System;
using System.Collections.Generic;
using System.Linq;

namespace SDMonitor.Control
{
    public static class KeyTileRenderer
    {
        private const int Width = MonitorMiniConstants.KeyImageWidth;
        private const int Height = MonitorMiniConstants.KeyImageHeight;

        private static readonly Dictionary<char, string[]> s_font = new()
        {
            [' '] = ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
            ['.'] = ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
            [':'] = ["00000", "01100", "01100", "00000", "01100", "01100", "00000"],
            ['-'] = ["00000", "00000", "00000", "11110", "00000", "00000", "00000"],
            ['/'] = ["00001", "00010", "00100", "01000", "10000", "00000", "00000"],
            ['%'] = ["11001", "11010", "00100", "01000", "10110", "00110", "00000"],
            ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
            ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
            ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
            ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
            ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
            ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
            ['6'] = ["00110", "01000", "10000", "11110", "10001", "10001", "01110"],
            ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
            ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
            ['9'] = ["01110", "10001", "10001", "01111", "00001", "00010", "11100"],
            ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
            ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
            ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
            ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
            ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
            ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
            ['G'] = ["01111", "10000", "10000", "10011", "10001", "10001", "01111"],
            ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
            ['I'] = ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
            ['J'] = ["00111", "00010", "00010", "00010", "00010", "10010", "01100"],
            ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
            ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
            ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
            ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
            ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
            ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
            ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
            ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
            ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
            ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
            ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
            ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
            ['W'] = ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
            ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
            ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
            ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"]
        };

        public static byte[] RenderTile(string title, string value, double? percent, RgbColor accent)
        {
            RgbColor[] pixels = new RgbColor[Width * Height];
            Fill(pixels, new RgbColor(8, 10, 14));
            DrawBorder(pixels, accent);

            DrawTextCentered(pixels, title.ToUpperInvariant(), y: 8, scale: 1, new RgbColor(170, 180, 190));
            DrawTextCentered(pixels, value.ToUpperInvariant(), y: 27, scale: 2, new RgbColor(245, 248, 250));

            DrawBarTrack(pixels, x: 10, y: 66, width: 60, height: 6);
            if (!percent.HasValue)
            {
                return MiniProtocol.BmpFromPixels(RotateCounterClockwise(pixels), Width, Height);
            }

            int fillWidth = (int)Math.Round(Math.Clamp(percent.Value, 0, 100) / 100.0 * 60);
            FillRect(pixels, 10, 66, fillWidth, 6, accent);

            return MiniProtocol.BmpFromPixels(RotateCounterClockwise(pixels), Width, Height);
        }

        public static byte[] RenderLabelTile(string label, RgbColor accent)
        {
            RgbColor[] pixels = new RgbColor[Width * Height];
            Fill(pixels, new RgbColor(8, 10, 14));
            DrawBorder(pixels, accent);
            DrawTextCentered(pixels, label.ToUpperInvariant(), y: 30, scale: 2, new RgbColor(245, 248, 250));
            return MiniProtocol.BmpFromPixels(RotateCounterClockwise(pixels), Width, Height);
        }

        private static void Fill(RgbColor[] pixels, RgbColor color)
        {
            Array.Fill(pixels, color);
        }

        private static void DrawBorder(RgbColor[] pixels, RgbColor color)
        {
            FillRect(pixels, 0, 0, Width, 2, color);
            FillRect(pixels, 0, Height - 2, Width, 2, color);
            FillRect(pixels, 0, 0, 2, Height, color);
            FillRect(pixels, Width - 2, 0, 2, Height, color);
        }

        private static void DrawBarTrack(RgbColor[] pixels, int x, int y, int width, int height)
        {
            FillRect(pixels, x, y, width, height, new RgbColor(35, 40, 48));
            SetPixel(pixels, x - 1, y - 1, new RgbColor(85, 90, 100));
            SetPixel(pixels, x + width, y - 1, new RgbColor(85, 90, 100));
            SetPixel(pixels, x - 1, y + height, new RgbColor(85, 90, 100));
            SetPixel(pixels, x + width, y + height, new RgbColor(85, 90, 100));
        }

        private static void DrawTextCentered(RgbColor[] pixels, string text, int y, int scale, RgbColor color)
        {
            string fitted = FitText(text, scale);
            int width = TextWidth(fitted, scale);
            DrawText(pixels, fitted, Math.Max(0, (Width - width) / 2), y, scale, color);
        }

        private static string FitText(string text, int scale)
        {
            string normalized = new(text.Select(character => s_font.ContainsKey(character) ? character : ' ').ToArray());
            while (normalized.Length > 0 && TextWidth(normalized, scale) > Width - 8)
            {
                normalized = normalized[..^1];
            }

            return normalized;
        }

        private static int TextWidth(string text, int scale)
        {
            if (text.Length == 0)
            {
                return 0;
            }

            return ((text.Length * 5) + (text.Length - 1)) * scale;
        }

        private static void DrawText(RgbColor[] pixels, string text, int x, int y, int scale, RgbColor color)
        {
            int cursor = x;
            foreach (char character in text)
            {
                DrawCharacter(pixels, character, cursor, y, scale, color);
                cursor += 6 * scale;
            }
        }

        private static void DrawCharacter(RgbColor[] pixels, char character, int x, int y, int scale, RgbColor color)
        {
            string[] glyph = s_font.TryGetValue(character, out string[]? lines) ? lines : s_font[' '];

            for (int row = 0; row < glyph.Length; row++)
            {
                for (int col = 0; col < glyph[row].Length; col++)
                {
                    if (glyph[row][col] != '1')
                    {
                        continue;
                    }

                    FillRect(pixels, x + col * scale, y + row * scale, scale, scale, color);
                }
            }
        }

        private static void FillRect(RgbColor[] pixels, int x, int y, int width, int height, RgbColor color)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    SetPixel(pixels, x + col, y + row, color);
                }
            }
        }

        private static void SetPixel(RgbColor[] pixels, int x, int y, RgbColor color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return;
            }

            pixels[y * Width + x] = color;
        }

        private static RgbColor[] RotateCounterClockwise(RgbColor[] source)
        {
            RgbColor[] rotated = new RgbColor[source.Length];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int targetX = y;
                    int targetY = Width - 1 - x;
                    rotated[targetY * Width + targetX] = source[y * Width + x];
                }
            }

            return rotated;
        }
    }
}
