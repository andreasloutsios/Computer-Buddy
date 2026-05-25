using System.Windows.Media.Imaging;

namespace ComputerBuddy.App.Services.Sprites;

public sealed class SpriteSet
{
    public required int BaseWidth { get; init; }
    public required int BaseHeight { get; init; }
    public required Dictionary<string, SpriteAnimation> Animations { get; init; }

    public SpriteAnimation GetAnimation(string name)
    {
        if (!Animations.TryGetValue(name, out var anim))
        {
            throw new KeyNotFoundException($"Missing animation '{name}'.");
        }

        return anim;
    }
}

public sealed class SpriteAnimation
{
    public required IReadOnlyList<BitmapSource> Frames { get; init; }
    public required int Fps { get; init; }
}

