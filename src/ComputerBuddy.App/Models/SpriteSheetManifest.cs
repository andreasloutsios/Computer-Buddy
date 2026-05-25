namespace ComputerBuddy.App.Models;

public sealed class SpriteSheetManifest
{
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public Dictionary<string, SpriteAnimationDef> Animations { get; set; } = new();
}

public sealed class SpriteAnimationDef
{
    public int Row { get; set; }
    public int Start { get; set; }
    public int Count { get; set; }
    public int Fps { get; set; }
}

