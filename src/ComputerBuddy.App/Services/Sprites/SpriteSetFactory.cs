using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ComputerBuddy.App.Models;

namespace ComputerBuddy.App.Services.Sprites;

public sealed class SpriteSetFactory
{
    public SpriteSet CreateDefault()
    {
        var baseW = 24;
        var baseH = 24;

        var idleRight = new[] { CreateDefaultFrame(baseW, baseH, 0), CreateDefaultFrame(baseW, baseH, 1) };
        var walkRight = new[] { CreateDefaultFrame(baseW, baseH, 2), CreateDefaultFrame(baseW, baseH, 3), CreateDefaultFrame(baseW, baseH, 4), CreateDefaultFrame(baseW, baseH, 5) };

        var idleLeft = idleRight.Select(f =>
        {
            var mirrored = new TransformedBitmap(f, new ScaleTransform(-1, 1, baseW / 2.0, baseH / 2.0));
            mirrored.Freeze();
            return (BitmapSource)mirrored;
        }).ToArray();

        var walkLeft = walkRight.Select(f =>
        {
            var mirrored = new TransformedBitmap(f, new ScaleTransform(-1, 1, baseW / 2.0, baseH / 2.0));
            mirrored.Freeze();
            return (BitmapSource)mirrored;
        }).ToArray();

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

    public bool TryCreateFromFiles(string? pngPath, string? manifestPath, out SpriteSet? spriteSet)
    {
        spriteSet = null;

        if (string.IsNullOrWhiteSpace(pngPath) || string.IsNullOrWhiteSpace(manifestPath))
        {
            return false;
        }

        if (!File.Exists(pngPath) || !File.Exists(manifestPath))
        {
            return false;
        }

        SpriteSheetManifest? manifest;
        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<SpriteSheetManifest>(json);
        }
        catch
        {
            return false;
        }

        if (manifest is null || manifest.FrameWidth <= 0 || manifest.FrameHeight <= 0 || manifest.Animations.Count == 0)
        {
            return false;
        }

        BitmapImage bitmap;
        try
        {
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(pngPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
        }
        catch
        {
            return false;
        }

        var animations = new Dictionary<string, SpriteAnimation>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, def) in manifest.Animations)
        {
            if (def.Count <= 0 || def.Fps <= 0 || def.Row < 0 || def.Start < 0)
            {
                continue;
            }

            var frames = new List<BitmapSource>(def.Count);
            for (var i = 0; i < def.Count; i++)
            {
                var x = (def.Start + i) * manifest.FrameWidth;
                var y = def.Row * manifest.FrameHeight;

                if (x + manifest.FrameWidth > bitmap.PixelWidth || y + manifest.FrameHeight > bitmap.PixelHeight)
                {
                    frames.Clear();
                    break;
                }

                var cropped = new CroppedBitmap(bitmap, new Int32Rect(x, y, manifest.FrameWidth, manifest.FrameHeight));
                cropped.Freeze();
                frames.Add(cropped);
            }

            if (frames.Count == 0)
            {
                continue;
            }

            animations[name] = new SpriteAnimation { Frames = frames, Fps = def.Fps };
        }

        if (animations.Count == 0)
        {
            return false;
        }

        spriteSet = new SpriteSet
        {
            BaseWidth = manifest.FrameWidth,
            BaseHeight = manifest.FrameHeight,
            Animations = animations
        };

        return true;
    }

    private static BitmapSource CreateDefaultFrame(int w, int h, int variant)
    {
        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new int[w * h];

        var bg = unchecked((int)0x00000000);
        Array.Fill(pixels, bg);

        var outline = unchecked((int)0xFF1E1E1E);
        var body = unchecked((int)0xFF2A9D8F);
        var accent = unchecked((int)0xFFE9C46A);
        var eyes = unchecked((int)0xFF000000);

        void Set(int x, int y, int c)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
            pixels[y * w + x] = c;
        }

        for (var y = 4; y < 22; y++)
        {
            for (var x = 7; x < 17; x++)
            {
                Set(x, y, body);
            }
        }

        for (var y = 4; y < 22; y++)
        {
            Set(7, y, outline);
            Set(16, y, outline);
        }

        for (var x = 7; x < 17; x++)
        {
            Set(x, 4, outline);
            Set(x, 21, outline);
        }

        Set(10, 9, eyes);
        Set(13, 9, eyes);

        for (var x = 9; x < 15; x++)
        {
            Set(x, 14, accent);
        }

        var legShift = variant switch
        {
            2 => -1,
            3 => 1,
            4 => -1,
            5 => 1,
            _ => 0
        };

        var leftLegX = 9 + legShift;
        var rightLegX = 13 - legShift;

        for (var y = 18; y < 22; y++)
        {
            Set(leftLegX, y, outline);
            Set(leftLegX + 1, y, outline);
            Set(rightLegX, y, outline);
            Set(rightLegX + 1, y, outline);
        }

        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        wb.Freeze();
        return wb;
    }
}

