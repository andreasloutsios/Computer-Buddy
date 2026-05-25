using System.IO;
using System.Windows;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services;
using ComputerBuddy.App.Services.Minifigure;

namespace ComputerBuddy.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly UserSettings _settings;
    private readonly MainWindow _buddyWindow;

    public SettingsWindow(SettingsService settingsService, UserSettings settings, MainWindow buddyWindow)
    {
        _settingsService = settingsService;
        _settings = settings;
        _buddyWindow = buddyWindow;

        InitializeComponent();

        CharacterModeComboBox.ItemsSource = new[]
        {
            new ModeOption("minifigure", "Minifigure builder"),
            new ModeOption("sprite_sheet", "Sprite sheet")
        };
        CharacterModeComboBox.DisplayMemberPath = nameof(ModeOption.Name);
        CharacterModeComboBox.SelectedValuePath = nameof(ModeOption.Id);

        HeadComboBox.ItemsSource = MinifigureCatalog.Heads;
        HeadComboBox.DisplayMemberPath = nameof(MinifigureCatalog.Option.Name);
        HeadComboBox.SelectedValuePath = nameof(MinifigureCatalog.Option.Id);

        BodyComboBox.ItemsSource = MinifigureCatalog.Bodies;
        BodyComboBox.DisplayMemberPath = nameof(MinifigureCatalog.Option.Name);
        BodyComboBox.SelectedValuePath = nameof(MinifigureCatalog.Option.Id);

        HatComboBox.ItemsSource = MinifigureCatalog.Hats;
        HatComboBox.DisplayMemberPath = nameof(MinifigureCatalog.Option.Name);
        HatComboBox.SelectedValuePath = nameof(MinifigureCatalog.Option.Id);

        AccessoryComboBox.ItemsSource = MinifigureCatalog.Accessories;
        AccessoryComboBox.DisplayMemberPath = nameof(MinifigureCatalog.Option.Name);
        AccessoryComboBox.SelectedValuePath = nameof(MinifigureCatalog.Option.Id);

        ScaleSlider.Value = _settings.Scale;
        SpeedSlider.Value = _settings.SpeedDipPerSecond;
        CharacterModeComboBox.SelectedValue = _settings.CharacterMode;

        HeadComboBox.SelectedValue = _settings.HeadId;
        BodyComboBox.SelectedValue = _settings.BodyId;
        HatComboBox.SelectedValue = _settings.HatId;
        AccessoryComboBox.SelectedValue = _settings.AccessoryId;

        SkinColorTextBox.Text = _settings.SkinColor;
        ShirtColorTextBox.Text = _settings.ShirtColor;
        PantsColorTextBox.Text = _settings.PantsColor;
        HatColorTextBox.Text = _settings.HatColor;
        AccentColorTextBox.Text = _settings.AccentColor;

        PngPathTextBox.Text = _settings.SpriteSheetPngPath ?? "";
        JsonPathTextBox.Text = _settings.SpriteSheetManifestPath ?? "";
        RemindersEnabledCheckBox.IsChecked = _settings.RemindersEnabled;
        ReminderMinutesTextBox.Text = _settings.ReminderMinutes.ToString();
        ModelTextBox.Text = _settings.OpenAiModel;

        ScaleSlider.ValueChanged += (_, _) => ScaleValue.Text = $"{ScaleSlider.Value:0}";
        SpeedSlider.ValueChanged += (_, _) => SpeedValue.Text = $"{SpeedSlider.Value:0}";
        ScaleValue.Text = $"{ScaleSlider.Value:0}";
        SpeedValue.Text = $"{SpeedSlider.Value:0}";

        var hasKey = !string.IsNullOrWhiteSpace(_settingsService.GetOpenAiApiKey(_settings));
        StatusText.Text = hasKey ? "API key saved (hidden). Paste a new one to replace it." : "No API key saved yet.";
    }

    private void BrowsePngButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == true)
        {
            PngPathTextBox.Text = dlg.FileName;
        }
    }

    private void BrowseJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON File (*.json)|*.json",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == true)
        {
            JsonPathTextBox.Text = dlg.FileName;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyToSettings();
        _buddyWindow.ReloadSprites();
        (System.Windows.Application.Current as App)?.OnSettingsChanged();
        StatusText.Text = "Applied.";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyToSettings();
        _settingsService.Save(_settings);
        _buddyWindow.ReloadSprites();
        (System.Windows.Application.Current as App)?.OnSettingsChanged();
        StatusText.Text = "Saved.";
    }

    private void ApplyToSettings()
    {
        _settings.Scale = Math.Max(1, Math.Round(ScaleSlider.Value));
        _settings.SpeedDipPerSecond = Math.Max(1, SpeedSlider.Value);

        _settings.CharacterMode = (CharacterModeComboBox.SelectedValue as string) ?? _settings.CharacterMode;
        _settings.HeadId = (HeadComboBox.SelectedValue as string) ?? _settings.HeadId;
        _settings.BodyId = (BodyComboBox.SelectedValue as string) ?? _settings.BodyId;
        _settings.HatId = (HatComboBox.SelectedValue as string) ?? _settings.HatId;
        _settings.AccessoryId = (AccessoryComboBox.SelectedValue as string) ?? _settings.AccessoryId;

        _settings.SkinColor = NormalizeHex(SkinColorTextBox.Text, _settings.SkinColor);
        _settings.ShirtColor = NormalizeHex(ShirtColorTextBox.Text, _settings.ShirtColor);
        _settings.PantsColor = NormalizeHex(PantsColorTextBox.Text, _settings.PantsColor);
        _settings.HatColor = NormalizeHex(HatColorTextBox.Text, _settings.HatColor);
        _settings.AccentColor = NormalizeHex(AccentColorTextBox.Text, _settings.AccentColor);

        _settings.SpriteSheetPngPath = NormalizePath(PngPathTextBox.Text);
        _settings.SpriteSheetManifestPath = NormalizePath(JsonPathTextBox.Text);

        _settings.RemindersEnabled = RemindersEnabledCheckBox.IsChecked == true;
        if (!int.TryParse(ReminderMinutesTextBox.Text?.Trim(), out var minutes))
        {
            minutes = _settings.ReminderMinutes;
        }
        _settings.ReminderMinutes = Math.Clamp(minutes, 1, 24 * 60);

        _settings.OpenAiModel = string.IsNullOrWhiteSpace(ModelTextBox.Text) ? "gpt-4o-mini" : ModelTextBox.Text.Trim();

        var key = ApiKeyBox.Password;
        if (!string.IsNullOrWhiteSpace(key))
        {
            _settingsService.SetOpenAiApiKey(_settings, key.Trim());
            ApiKeyBox.Password = "";
        }
    }

    private static string? NormalizePath(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return File.Exists(trimmed) ? trimmed : trimmed;
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

    private sealed record ModeOption(string Id, string Name);
}

