namespace ComputerBuddy.Contracts;

public static class BuddyIpc
{
    public const string PipeName = "ComputerBuddy";
}

public sealed record IpcRequest(string Type, object? Data = null);

public sealed record IpcResponse(string Type, object? Data = null, string? Error = null);

public sealed record GetSettingsResponse(UserSettingsDto Settings);

public sealed record SetSettingsRequest(UserSettingsDto Settings, bool Save);

public sealed record SimpleOkResponse(bool Ok);

public sealed record ToggleBuddyRequest();

public sealed record ShowBuddyRequest();

public sealed record HideBuddyRequest();

public sealed record ExitBuddyRequest();

public sealed class UserSettingsDto
{
    public double Scale { get; set; }
    public double SpeedDipPerSecond { get; set; }

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
    public bool RemindersEnabled { get; set; }
    public int ReminderMinutes { get; set; }
}
