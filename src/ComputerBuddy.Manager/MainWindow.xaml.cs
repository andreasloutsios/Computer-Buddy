using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ComputerBuddy.Contracts;

namespace ComputerBuddy.Manager;

public partial class MainWindow : Window
{
    private readonly BuddyIpcClient _client = new();
    private UserSettingsDto? _current;
    private static readonly Option[] HotkeyKeys = BuildHotkeyKeys();
    private DateTimeOffset? _lastConnectedAt;
    private const string UpdatesUrl = "https://drive.google.com/drive/folders/1JGNk0OqQwaXtBdpk6D7F9sY5XCiYiQB-";

    public MainWindow()
    {
        InitializeComponent();

        CharacterModeComboBox.ItemsSource = new[]
        {
            new Option("minifigure", "Minifigure builder")
        };
        CharacterModeComboBox.DisplayMemberPath = nameof(Option.Name);
        CharacterModeComboBox.SelectedValuePath = nameof(Option.Id);

        PaletteTargetComboBox.ItemsSource = new[]
        {
            new Option("skin", "Skin color"),
            new Option("shirt", "Shirt color"),
            new Option("pants", "Pants color"),
            new Option("hat", "Hat color"),
            new Option("accent", "Accent color")
        };
        PaletteTargetComboBox.DisplayMemberPath = nameof(Option.Name);
        PaletteTargetComboBox.SelectedValuePath = nameof(Option.Id);
        PaletteTargetComboBox.SelectedValue = "skin";

        HeadComboBox.ItemsSource = new[] { new Option("smile", "Smile"), new Option("visor", "Visor") };
        HeadComboBox.DisplayMemberPath = nameof(Option.Name);
        HeadComboBox.SelectedValuePath = nameof(Option.Id);

        BodyComboBox.ItemsSource = new[] { new Option("classic", "Classic"), new Option("hoodie", "Hoodie") };
        BodyComboBox.DisplayMemberPath = nameof(Option.Name);
        BodyComboBox.SelectedValuePath = nameof(Option.Id);

        HatComboBox.ItemsSource = new[] { new Option("none", "None"), new Option("cap", "Cap"), new Option("helmet", "Helmet") };
        HatComboBox.DisplayMemberPath = nameof(Option.Name);
        HatComboBox.SelectedValuePath = nameof(Option.Id);

        AccessoryComboBox.ItemsSource = new[] { new Option("none", "None"), new Option("backpack", "Backpack") };
        AccessoryComboBox.DisplayMemberPath = nameof(Option.Name);
        AccessoryComboBox.SelectedValuePath = nameof(Option.Id);

        HotkeyKeyComboBox.ItemsSource = HotkeyKeys;
        HotkeyKeyComboBox.DisplayMemberPath = nameof(Option.Name);
        HotkeyKeyComboBox.SelectedValuePath = nameof(Option.Id);

        ScaleSlider.ValueChanged += (_, _) => ScaleValue.Text = $"{ScaleSlider.Value:0}";
        SpeedSlider.ValueChanged += (_, _) => SpeedValue.Text = $"{SpeedSlider.Value:0}";
        ScaleValue.Text = $"{ScaleSlider.Value:0}";
        SpeedValue.Text = $"{SpeedSlider.Value:0}";

        StatusText.Text = "Not connected.";
    }

    private void PaletteColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b)
        {
            return;
        }

        var hex = b.Tag as string;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        var target = PaletteTargetComboBox.SelectedValue as string;
        var tb = target switch
        {
            "skin" => SkinColorTextBox,
            "shirt" => ShirtColorTextBox,
            "pants" => PantsColorTextBox,
            "hat" => HatColorTextBox,
            "accent" => AccentColorTextBox,
            _ => SkinColorTextBox
        };

        tb.Text = hex.Trim();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await ConnectAsync();
    }

    private async void StartBuddyButton_Click(object sender, RoutedEventArgs e)
    {
        await StartBuddyAndConnectAsync();
    }

    private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UpdatesUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusText.Text = "Failed to open updates link.";
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplySettingsAsync(save: false);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplySettingsAsync(save: true);
    }

    private async void ExportPackButton_Click(object sender, RoutedEventArgs e)
    {
        var dto = BuildDtoFromUi();
        if (dto is null)
        {
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Computer Buddy Pack (*.zip)|*.zip",
            FileName = "computer-buddy-pack.zip",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        var exportDto = Clone(dto);
        using var fs = File.Create(dlg.FileName);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        if (string.Equals(exportDto.CharacterMode, "sprite_sheet", StringComparison.OrdinalIgnoreCase))
        {
            exportDto.SpriteSheetPngPath = AddAssetIfExists(zip, exportDto.SpriteSheetPngPath);
            exportDto.SpriteSheetManifestPath = AddAssetIfExists(zip, exportDto.SpriteSheetManifestPath);
        }

        var settingsEntry = zip.CreateEntry("settings.json");
        await using (var entryStream = settingsEntry.Open())
        await using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
        {
            var json = JsonSerializer.Serialize(exportDto, BuddyIpcJson.Options);
            await writer.WriteAsync(json);
        }

        StatusText.Text = $"Exported pack: {dlg.FileName}";
    }

    private async void ImportPackButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Computer Buddy Pack (*.zip)|*.zip",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        UserSettingsDto? dto;
        using (var zip = ZipFile.OpenRead(dlg.FileName))
        {
            var settingsEntry = zip.GetEntry("settings.json");
            if (settingsEntry is null)
            {
                StatusText.Text = "Pack missing settings.json";
                return;
            }

            await using (var entryStream = settingsEntry.Open())
            using (var reader = new StreamReader(entryStream, Encoding.UTF8))
            {
                var json = await reader.ReadToEndAsync();
                dto = JsonSerializer.Deserialize<UserSettingsDto>(json, BuddyIpcJson.Options);
            }

            if (dto is null)
            {
                StatusText.Text = "Failed to read pack settings.";
                return;
            }

            var packFolder = GetPackFolder(dlg.FileName);
            Directory.CreateDirectory(packFolder);

            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = Path.Combine(packFolder, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }

            dto.SpriteSheetPngPath = ResolveAssetPath(packFolder, dto.SpriteSheetPngPath);
            dto.SpriteSheetManifestPath = ResolveAssetPath(packFolder, dto.SpriteSheetManifestPath);
        }

        _current = dto;
        ApplyDtoToUi(dto);
        StatusText.Text = $"Imported pack: {Path.GetFileName(dlg.FileName)}";

        await ApplySettingsAsync(save: true);
    }

    private async Task ApplySettingsAsync(bool save)
    {
        var dto = BuildDtoFromUi();
        if (dto is null)
        {
            return;
        }

        var req = new IpcRequest("set_settings", new SetSettingsRequest(dto, save));
        var resp = await _client.SendAsync(req, CancellationToken.None);
        if (resp.Error is not null)
        {
            StatusText.Text = $"Error: {resp.Error}";
            return;
        }

        StatusText.Text = save ? "Saved + applied to buddy." : "Applied to buddy.";
        _current = dto;
    }

    private async Task RefreshFromBuddyAsync()
    {
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        StatusText.Text = "Connecting...";

        IpcResponse? lastResp = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(250);
            }

            lastResp = await _client.SendAsync(new IpcRequest("get_settings"), CancellationToken.None);
            if (lastResp.Error is null)
            {
                break;
            }
        }

        if (lastResp is null || lastResp.Error is not null)
        {
            StatusText.Text = $"Not connected: {lastResp?.Error ?? "Buddy is not running."}";
            return;
        }

        var json = JsonSerializer.Serialize(lastResp.Data, BuddyIpcJson.Options);
        var data = JsonSerializer.Deserialize<GetSettingsResponse>(json, BuddyIpcJson.Options);
        if (data is null)
        {
            StatusText.Text = "Invalid response.";
            return;
        }

        _current = data.Settings;
        ApplyDtoToUi(_current);
        _lastConnectedAt = DateTimeOffset.Now;
        StatusText.Text = $"Connected ({_lastConnectedAt:HH:mm:ss}).";
    }

    private async Task StartBuddyAndConnectAsync()
    {
        StatusText.Text = "Starting buddy...";
        try
        {
            var slnDir = FindSolutionRoot();
            var buddyExe = TryGetBuddyExePath(slnDir);

            ProcessStartInfo psi;
            if (!string.IsNullOrWhiteSpace(buddyExe))
            {
                psi = new ProcessStartInfo
                {
                    FileName = buddyExe,
                    WorkingDirectory = Path.GetDirectoryName(buddyExe)!,
                    UseShellExecute = true
                };
            }
            else
            {
                var csproj = Path.Combine(slnDir, "src", "ComputerBuddy.App", "ComputerBuddy.App.csproj");
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{csproj}\"",
                    WorkingDirectory = slnDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to start buddy: {ex.Message}";
            return;
        }

        for (var i = 0; i < 12; i++)
        {
            await Task.Delay(300);
            var resp = await _client.SendAsync(new IpcRequest("get_settings"), CancellationToken.None);
            if (resp.Error is null)
            {
                var json = JsonSerializer.Serialize(resp.Data, BuddyIpcJson.Options);
                var data = JsonSerializer.Deserialize<GetSettingsResponse>(json, BuddyIpcJson.Options);
                if (data is null)
                {
                    StatusText.Text = "Invalid response.";
                    return;
                }

                _current = data.Settings;
                ApplyDtoToUi(_current);
                StatusText.Text = "Connected.";
                return;
            }
        }

        StatusText.Text = "Not connected: Buddy did not start or did not respond.";
    }

    private static string? TryGetBuddyExePath(string slnDir)
    {
        var baseDir = AppContext.BaseDirectory;
        var direct = Path.Combine(baseDir, "ComputerBuddy.App.exe");
        if (File.Exists(direct))
        {
            return direct;
        }

        var projectDir = Path.Combine(slnDir, "src", "ComputerBuddy.App");
        var debug = Path.Combine(projectDir, "bin", "Debug", "net8.0-windows", "ComputerBuddy.App.exe");
        if (File.Exists(debug))
        {
            return debug;
        }

        var release = Path.Combine(projectDir, "bin", "Release", "net8.0-windows", "ComputerBuddy.App.exe");
        if (File.Exists(release))
        {
            return release;
        }

        return null;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sln = Path.Combine(dir.FullName, "ComputerBuddy.sln");
            if (File.Exists(sln))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private async Task SendSimpleAsync(string type, object data)
    {
        var resp = await _client.SendAsync(new IpcRequest(type, data), CancellationToken.None);
        StatusText.Text = resp.Error is null ? "OK" : $"Error: {resp.Error}";
    }

    private void ApplyDtoToUi(UserSettingsDto dto)
    {
        CharacterModeComboBox.SelectedValue = dto.CharacterMode;
        ScaleSlider.Value = dto.Scale;
        SpeedSlider.Value = dto.SpeedDipPerSecond;

        HotkeyEnabledCheckBox.IsChecked = dto.ToggleHotkeyEnabled;
        HotkeyCtrlCheckBox.IsChecked = (dto.ToggleHotkeyModifiers & 0x0002) != 0;
        HotkeyAltCheckBox.IsChecked = (dto.ToggleHotkeyModifiers & 0x0001) != 0;
        HotkeyShiftCheckBox.IsChecked = (dto.ToggleHotkeyModifiers & 0x0004) != 0;
        HotkeyWinCheckBox.IsChecked = (dto.ToggleHotkeyModifiers & 0x0008) != 0;
        HotkeyKeyComboBox.SelectedValue = dto.ToggleHotkeyVirtualKey.ToString();

        HeadComboBox.SelectedValue = dto.HeadId;
        BodyComboBox.SelectedValue = dto.BodyId;
        HatComboBox.SelectedValue = dto.HatId;
        AccessoryComboBox.SelectedValue = dto.AccessoryId;

        SkinColorTextBox.Text = dto.SkinColor;
        ShirtColorTextBox.Text = dto.ShirtColor;
        PantsColorTextBox.Text = dto.PantsColor;
        HatColorTextBox.Text = dto.HatColor;
        AccentColorTextBox.Text = dto.AccentColor;
    }

    private UserSettingsDto? BuildDtoFromUi()
    {
        var baseDto = _current is null ? new UserSettingsDto() : Clone(_current);

        baseDto.CharacterMode = (CharacterModeComboBox.SelectedValue as string) ?? baseDto.CharacterMode;
        baseDto.Scale = Math.Max(1, Math.Round(ScaleSlider.Value));
        baseDto.SpeedDipPerSecond = Math.Max(1, SpeedSlider.Value);

        baseDto.ToggleHotkeyEnabled = HotkeyEnabledCheckBox.IsChecked == true;
        baseDto.ToggleHotkeyModifiers = BuildHotkeyModifiers();
        var hotkeyValue = (HotkeyKeyComboBox.SelectedValue as string) ?? baseDto.ToggleHotkeyVirtualKey.ToString();
        if (int.TryParse(hotkeyValue, out var vk))
        {
            baseDto.ToggleHotkeyVirtualKey = vk;
        }

        baseDto.HeadId = (HeadComboBox.SelectedValue as string) ?? baseDto.HeadId;
        baseDto.BodyId = (BodyComboBox.SelectedValue as string) ?? baseDto.BodyId;
        baseDto.HatId = (HatComboBox.SelectedValue as string) ?? baseDto.HatId;
        baseDto.AccessoryId = (AccessoryComboBox.SelectedValue as string) ?? baseDto.AccessoryId;

        baseDto.SkinColor = NormalizeHex(SkinColorTextBox.Text, baseDto.SkinColor);
        baseDto.ShirtColor = NormalizeHex(ShirtColorTextBox.Text, baseDto.ShirtColor);
        baseDto.PantsColor = NormalizeHex(PantsColorTextBox.Text, baseDto.PantsColor);
        baseDto.HatColor = NormalizeHex(HatColorTextBox.Text, baseDto.HatColor);
        baseDto.AccentColor = NormalizeHex(AccentColorTextBox.Text, baseDto.AccentColor);
        baseDto.SpriteSheetPngPath = null;
        baseDto.SpriteSheetManifestPath = null;

        return baseDto;
    }

    private int BuildHotkeyModifiers()
    {
        var m = 0;
        if (HotkeyAltCheckBox.IsChecked == true) m |= 0x0001;
        if (HotkeyCtrlCheckBox.IsChecked == true) m |= 0x0002;
        if (HotkeyShiftCheckBox.IsChecked == true) m |= 0x0004;
        if (HotkeyWinCheckBox.IsChecked == true) m |= 0x0008;
        return m;
    }

    private static Option[] BuildHotkeyKeys()
    {
        var list = new List<Option>();
        for (var c = 'A'; c <= 'Z'; c++)
        {
            list.Add(new Option(((int)c).ToString(), c.ToString()));
        }

        for (var f = 1; f <= 12; f++)
        {
            list.Add(new Option((0x70 + (f - 1)).ToString(), $"F{f}"));
        }

        return list.ToArray();
    }

    private static string NormalizeHex(string? text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var s = text.Trim();
        if (!s.StartsWith("#", StringComparison.Ordinal))
        {
            s = "#" + s;
        }

        return s;
    }

    private static string? NormalizeOptional(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }

    private static string? AddAssetIfExists(ZipArchive zip, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim();
        if (!File.Exists(p))
        {
            return path;
        }

        var fileName = Path.GetFileName(p);
        var entryName = "assets/" + fileName;
        zip.CreateEntryFromFile(p, entryName);
        return entryName;
    }

    private static string GetPackFolder(string zipPath)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "ComputerBuddy", "packs");
        var name = Path.GetFileNameWithoutExtension(zipPath);
        return Path.Combine(baseDir, name);
    }

    private static string? ResolveAssetPath(string packFolder, string? maybeRelative)
    {
        if (string.IsNullOrWhiteSpace(maybeRelative))
        {
            return null;
        }

        var p = maybeRelative.Trim();
        if (Path.IsPathRooted(p))
        {
            return p;
        }

        var full = Path.Combine(packFolder, p.Replace('/', Path.DirectorySeparatorChar));
        return full;
    }

    private static UserSettingsDto Clone(UserSettingsDto dto)
    {
        var json = JsonSerializer.Serialize(dto, BuddyIpcJson.Options);
        return JsonSerializer.Deserialize<UserSettingsDto>(json, BuddyIpcJson.Options) ?? new UserSettingsDto();
    }

    private sealed record Option(string Id, string Name);
}

public sealed class BuddyIpcClient
{
    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        Exception? lastEx = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var client = new NamedPipeClientStream(".", BuddyIpc.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(2000, cancellationToken);

                await using var writer = new StreamWriter(client, new UTF8Encoding(false), 4096, leaveOpen: true)
                {
                    AutoFlush = true
                };
                using var reader = new StreamReader(client, Encoding.UTF8, false, 4096, leaveOpen: true);

                var json = JsonSerializer.Serialize(request, BuddyIpcJson.Options);
                await writer.WriteLineAsync(json);

                var respLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(respLine))
                {
                    return new IpcResponse("error", null, "No response from buddy.");
                }

                try
                {
                    return JsonSerializer.Deserialize<IpcResponse>(respLine, BuddyIpcJson.Options) ?? new IpcResponse("error", null, "Invalid response JSON.");
                }
                catch
                {
                    return new IpcResponse("error", null, "Invalid response JSON.");
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
                await Task.Delay(150, cancellationToken);
            }
        }

        return new IpcResponse("error", null, GetErrorMessage(lastEx));
    }

    private static string GetErrorMessage(Exception? ex)
    {
        if (ex is null)
        {
            return "Buddy is not running.";
        }

        if (ex is TimeoutException)
        {
            return "Buddy did not respond. Start the buddy app first.";
        }

        if (ex is UnauthorizedAccessException)
        {
            return "Permission error. Run both apps as Administrator or both as normal user.";
        }

        return "Buddy is not running.";
    }
}
