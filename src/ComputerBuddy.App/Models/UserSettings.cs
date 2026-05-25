namespace ComputerBuddy.App.Models;

public sealed class UserSettings
{
    public double Scale { get; set; } = 4.0;
    public double SpeedDipPerSecond { get; set; } = 120.0;
    public bool ToggleHotkeyEnabled { get; set; } = true;
    public int ToggleHotkeyModifiers { get; set; } = 3;
    public int ToggleHotkeyVirtualKey { get; set; } = 0x42;
    public string CharacterMode { get; set; } = "minifigure";
    public string HeadId { get; set; } = "smile";
    public string BodyId { get; set; } = "classic";
    public string HatId { get; set; } = "none";
    public string AccessoryId { get; set; } = "none";
    public string SkinColor { get; set; } = "#F2C28B";
    public string ShirtColor { get; set; } = "#2A9D8F";
    public string PantsColor { get; set; } = "#264653";
    public string HatColor { get; set; } = "#E76F51";
    public string AccentColor { get; set; } = "#E9C46A";
    public string? SpriteSheetPngPath { get; set; }
    public string? SpriteSheetManifestPath { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string? EncryptedOpenAiApiKey { get; set; }
    public bool RemindersEnabled { get; set; } = true;
    public int ReminderMinutes { get; set; } = 30;
}

