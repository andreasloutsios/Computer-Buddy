using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services.Sprites;

namespace ComputerBuddy.App.Services.Minifigure;

public sealed class MinifigureSpriteSetFactory
{
    public SpriteSet Create(UserSettings settings)
    {
        var baseW = 24;
        var baseH = 24;

        var palette = MinifigurePalette.FromSettings(settings);

        var idleRight = new[]
        {
            RenderFrame(baseW, baseH, settings, palette, 0),
            RenderFrame(baseW, baseH, settings, palette, 1)
        };

        var walkRight = new[]
        {
            RenderFrame(baseW, baseH, settings, palette, 2),
            RenderFrame(baseW, baseH, settings, palette, 3),
            RenderFrame(baseW, baseH, settings, palette, 4),
            RenderFrame(baseW, baseH, settings, palette, 5)
        };

        var idleLeft = idleRight.Select(f => Mirror(f, baseW, baseH)).ToArray();
        var walkLeft = walkRight.Select(f => Mirror(f, baseW, baseH)).ToArray();

        return new SpriteSet
        {
            BaseWidth = baseW,
            BaseHeight = baseH,
            Animations = new Dictionary<string, SpriteAnimation>
            {
                ["idle_right"] = new SpriteAnimation { Frames = idleRight, Fps = 6 },
                ["walk_right"] = new SpriteAnimation { Frames = walkRight, Fps = 12 },
                ["idle_left"] = new SpriteAnimation { Frames = idleLeft, Fps = 6 },
                ["walk_left"] = new SpriteAnimation { Frames = walkLeft, Fps = 12 }
            }
        };
    }

    private static BitmapSource Mirror(BitmapSource src, int w, int h)
    {
        var mirrored = new TransformedBitmap(src, new ScaleTransform(-1, 1, w / 2.0, h / 2.0));
        mirrored.Freeze();
        return mirrored;
    }

    private static BitmapSource RenderFrame(int w, int h, UserSettings settings, MinifigurePalette palette, int variant)
    {
        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new int[w * h];

        var bg = unchecked((int)0x00000000);
        Array.Fill(pixels, bg);

        void Set(int x, int y, int c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
            pixels[y * w + x] = c;
        }

        var legShift = variant switch
        {
            2 => -1,
            3 => 1,
            4 => -1,
            5 => 1,
            _ => 0
        };

        DrawAccessory(settings.AccessoryId, Set, palette, legShift);
        DrawBody(settings.BodyId, Set, palette, legShift);
        DrawHands(Set, palette, legShift);
        DrawHead(settings.HeadId, Set, palette);
        DrawHat(settings.HatId, Set, palette);

        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        wb.Freeze();
        return wb;
    }

    private static void DrawHead(string headId, Action<int, int, int> set, MinifigurePalette p)
    {
        for (var y = 2; y < 9; y++)
        {
            for (var x = 8; x < 16; x++)
            {
                set(x, y, p.Skin);
            }
        }

        for (var y = 2; y < 9; y++)
        {
            set(8, y, p.Outline);
            set(15, y, p.Outline);
        }

        for (var x = 8; x < 16; x++)
        {
            set(x, 2, p.Outline);
            set(x, 8, p.Outline);
        }

        if (string.Equals(headId, "visor", StringComparison.OrdinalIgnoreCase))
        {
            for (var x = 9; x < 15; x++)
            {
                set(x, 5, p.Accent);
                set(x, 6, p.Accent);
            }
            return;
        }

        set(10, 5, p.Eyes);
        set(13, 5, p.Eyes);

        set(11, 7, p.Eyes);
        set(12, 7, p.Eyes);
    }

    private static void DrawHands(Action<int, int, int> set, MinifigurePalette p, int legShift)
    {
        var armShift = -legShift;

        DrawArm(4 + armShift, set, p);
        DrawArm(17 - armShift, set, p);
    }

    private static void DrawArm(int x0, Action<int, int, int> set, MinifigurePalette p)
    {
        if (x0 < 0 || x0 + 2 >= 24)
        {
            return;
        }

        for (var x = x0; x <= x0 + 2; x++)
        {
            set(x, 10, p.Outline);
            set(x, 13, p.Outline);
        }

        for (var y = 11; y <= 12; y++)
        {
            set(x0, y, p.Outline);
            set(x0 + 1, y, p.Shirt);
            set(x0 + 2, y, p.Outline);
        }

        for (var y = 14; y <= 15; y++)
        {
            set(x0, y, p.Outline);
            set(x0 + 1, y, p.Skin);
            set(x0 + 2, y, p.Outline);
        }

        set(x0 + 1, 16, p.Outline);
    }

    private static void DrawBody(string bodyId, Action<int, int, int> set, MinifigurePalette p, int legShift)
    {
        for (var y = 9; y < 16; y++)
        {
            for (var x = 7; x < 17; x++)
            {
                set(x, y, p.Shirt);
            }
        }

        for (var y = 9; y < 16; y++)
        {
            set(7, y, p.Outline);
            set(16, y, p.Outline);
        }

        for (var x = 7; x < 17; x++)
        {
            set(x, 9, p.Outline);
            set(x, 15, p.Outline);
        }

        if (string.Equals(bodyId, "hoodie", StringComparison.OrdinalIgnoreCase))
        {
            for (var x = 9; x < 15; x++)
            {
                set(x, 11, p.Accent);
            }
            set(8, 10, p.Accent);
            set(15, 10, p.Accent);
        }
        else
        {
            for (var x = 10; x < 14; x++)
            {
                set(x, 12, p.Accent);
            }
        }

        for (var y = 16; y < 22; y++)
        {
            for (var x = 7; x < 17; x++)
            {
                set(x, y, p.Pants);
            }
        }

        for (var y = 16; y < 22; y++)
        {
            set(7, y, p.Outline);
            set(16, y, p.Outline);
        }

        for (var x = 7; x < 17; x++)
        {
            set(x, 16, p.Outline);
            set(x, 21, p.Outline);
        }

        var leftLegX = 9 + legShift;
        var rightLegX = 13 - legShift;

        for (var y = 18; y < 22; y++)
        {
            set(leftLegX, y, p.Outline);
            set(leftLegX + 1, y, p.Outline);
            set(rightLegX, y, p.Outline);
            set(rightLegX + 1, y, p.Outline);
        }
    }

    private static void DrawHat(string hatId, Action<int, int, int> set, MinifigurePalette p)
    {
        if (string.Equals(hatId, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(hatId, "helmet", StringComparison.OrdinalIgnoreCase))
        {
            for (var y = 0; y < 4; y++)
            {
                for (var x = 7; x < 17; x++)
                {
                    set(x, y, p.Hat);
                }
            }
            for (var x = 7; x < 17; x++)
            {
                set(x, 0, p.Outline);
                set(x, 3, p.Outline);
            }
            for (var y = 0; y < 4; y++)
            {
                set(7, y, p.Outline);
                set(16, y, p.Outline);
            }
            return;
        }

        for (var y = 1; y < 4; y++)
        {
            for (var x = 8; x < 16; x++)
            {
                set(x, y, p.Hat);
            }
        }

        for (var x = 7; x < 17; x++)
        {
            set(x, 4, p.Outline);
            set(x, 3, p.Hat);
        }
    }

    private static void DrawAccessory(string accessoryId, Action<int, int, int> set, MinifigurePalette p, int legShift)
    {
        if (!string.Equals(accessoryId, "backpack", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (var y = 10; y < 16; y++)
        {
            set(6, y, p.Accent);
            set(17, y, p.Accent);
        }

        set(6, 10, p.Outline);
        set(6, 15, p.Outline);
        set(17, 10, p.Outline);
        set(17, 15, p.Outline);

        var strapY = 11 + (legShift != 0 ? 1 : 0);
        set(8, strapY, p.Outline);
        set(15, strapY, p.Outline);
    }

    private sealed class MinifigurePalette
    {
        public required int Outline { get; init; }
        public required int Eyes { get; init; }
        public required int Skin { get; init; }
        public required int Shirt { get; init; }
        public required int Pants { get; init; }
        public required int Hat { get; init; }
        public required int Accent { get; init; }

        public static MinifigurePalette FromSettings(UserSettings settings)
        {
            return new MinifigurePalette
            {
                Outline = unchecked((int)0xFF1E1E1E),
                Eyes = unchecked((int)0xFF000000),
                Skin = ParseOr(settings.SkinColor, unchecked((int)0xFFF2C28B)),
                Shirt = ParseOr(settings.ShirtColor, unchecked((int)0xFF2A9D8F)),
                Pants = ParseOr(settings.PantsColor, unchecked((int)0xFF264653)),
                Hat = ParseOr(settings.HatColor, unchecked((int)0xFFE76F51)),
                Accent = ParseOr(settings.AccentColor, unchecked((int)0xFFE9C46A))
            };
        }

        private static int ParseOr(string? hex, int fallbackArgb)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallbackArgb;
            }

            var s = hex.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal))
            {
                s = s[1..];
            }

            if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return unchecked((int)(0xFF000000 | (uint)rgb));
            }

            if (s.Length == 8 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                return unchecked((int)(uint)argb);
            }

            return fallbackArgb;
        }
    }
}

