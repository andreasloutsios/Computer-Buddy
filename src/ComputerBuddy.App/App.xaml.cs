using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using ComputerBuddy.App.Models;
using ComputerBuddy.App.Services;
using ComputerBuddy.App.Services.Hotkeys;
using ComputerBuddy.App.Services.Ipc;
using ComputerBuddy.App.Services.Reminders;
using ComputerBuddy.App.Windows;
using ComputerBuddy.Contracts;

namespace ComputerBuddy.App;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private SettingsService? _settingsService;
    private MainWindow? _buddyWindow;
    private SettingsWindow? _settingsWindow;
    private ChatWindow? _chatWindow;
    private SystemInfoWindow? _systemInfoWindow;
    private ReminderService? _reminderService;
    private GlobalHotkeyService? _hotkeyService;
    private UserSettings? _settings;
    private BuddyIpcServer? _ipcServer;
    private bool _isExiting;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _buddyWindow = new MainWindow(_settingsService, _settings);
        _buddyWindow.SourceInitialized += (_, _) => RegisterHotkey();
        _buddyWindow.Closing += (_, args) => OnWindowClosingHide(_buddyWindow, args, isBuddy: true);
        _buddyWindow.Show();

        _settingsWindow = new SettingsWindow(_settingsService, _settings, _buddyWindow);
        _settingsWindow.Closing += (_, args) => OnWindowClosingHide(_settingsWindow, args, isBuddy: false);
        _chatWindow = new ChatWindow(_settingsService, _settings);
        _chatWindow.Closing += (_, args) => OnWindowClosingHide(_chatWindow, args, isBuddy: false);
        _systemInfoWindow = new SystemInfoWindow();
        _systemInfoWindow.Closing += (_, args) => OnWindowClosingHide(_systemInfoWindow, args, isBuddy: false);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Visible = true,
            Text = "Computer Buddy",
            Icon = System.Drawing.SystemIcons.Application
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show/Hide Buddy", null, (_, _) => ToggleBuddyVisibility());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Control Center", null, (_, _) => OpenControlCenter());
        menu.Items.Add("Chat", null, (_, _) => ShowChat());
        menu.Items.Add("System Info", null, (_, _) => ShowSystemInfo());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;

        _notifyIcon.DoubleClick += (_, _) => ToggleBuddyVisibility();

        _reminderService = new ReminderService(_settings);
        _reminderService.Reminder += msg => OnReminder(msg);
        _reminderService.Start();

        _ipcServer = new BuddyIpcServer(
            getSettings: () => _settings!,
            applySettings: (dto, save) => ApplySettingsFromManager(dto, save),
            toggleBuddy: () => Dispatcher.Invoke(ToggleBuddyVisibility),
            showBuddy: () => Dispatcher.Invoke(ShowBuddy),
            hideBuddy: () => Dispatcher.Invoke(HideBuddy),
            exitBuddy: () => Dispatcher.Invoke(ExitApplication));
        _ipcServer.Start();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _ipcServer?.Dispose();
        _hotkeyService?.Dispose();
        _reminderService?.Dispose();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    public void ToggleBuddyVisibility()
    {
        if (_buddyWindow is null)
        {
            return;
        }

        if (_buddyWindow.Visibility == Visibility.Visible)
        {
            HideBuddy();
        }
        else
        {
            ShowBuddy();
        }
    }

    public void ShowBuddy()
    {
        if (_buddyWindow is null)
        {
            return;
        }

        _buddyWindow.Show();
        _buddyWindow.Activate();
    }

    public void HideBuddy()
    {
        _buddyWindow?.Hide();
    }

    public void ExitApplication()
    {
        _isExiting = true;
        Shutdown();
    }

    public void OpenControlCenter()
    {
        try
        {
            var exe = TryGetManagerExePath();
            if (!string.IsNullOrWhiteSpace(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                    UseShellExecute = true
                });
                return;
            }

            var slnDir = FindSolutionRoot();
            var csproj = Path.Combine(slnDir, "src", "ComputerBuddy.Manager", "ComputerBuddy.Manager.csproj");
            Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{csproj}\"",
                WorkingDirectory = slnDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            _notifyIcon?.ShowBalloonTip(4000, "Computer Buddy", "Failed to start Control Center.", System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    public void ShowSettings() => ShowWindow(_settingsWindow);
    public void ShowChat() => ShowWindow(_chatWindow);
    public void ShowSystemInfo() => ShowWindow(_systemInfoWindow);

    public void OnSettingsChanged()
    {
        _reminderService?.UpdateFromSettings();
    }

    private void ShowWindow(Window? window)
    {
        if (window is null)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OnWindowClosingHide(Window? window, System.ComponentModel.CancelEventArgs args, bool isBuddy)
    {
        if (window is null || _isExiting)
        {
            return;
        }

        args.Cancel = true;
        window.Hide();

        if (isBuddy)
        {
            _notifyIcon?.ShowBalloonTip(2500, "Computer Buddy", "Still running in the system tray.", System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void RegisterHotkey()
    {
        if (_buddyWindow is null || _hotkeyService is not null)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(_buddyWindow).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        UpdateHotkey();
    }

    private void UpdateHotkey()
    {
        if (_buddyWindow is null || _settings is null)
        {
            return;
        }

        _hotkeyService?.Dispose();
        _hotkeyService = null;

        if (!_settings.ToggleHotkeyEnabled)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(_buddyWindow).Handle;
        if (hwnd == nint.Zero)
        {
            return;
        }

        try
        {
            var modifiers = (GlobalHotkeyService.ModifierKeys)_settings.ToggleHotkeyModifiers;
            var vk = (uint)_settings.ToggleHotkeyVirtualKey;
            _hotkeyService = new GlobalHotkeyService(hwnd, modifiers, vk);
            _hotkeyService.Triggered += () => Dispatcher.Invoke(ToggleBuddyVisibility);
        }
        catch
        {
            _hotkeyService?.Dispose();
            _hotkeyService = null;
            _notifyIcon?.ShowBalloonTip(4000, "Computer Buddy", "Failed to register global hotkey.", System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private void OnReminder(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            if (_buddyWindow is not null && _buddyWindow.Visibility == Visibility.Visible)
            {
                _buddyWindow.ShowReminder(msg);
                return;
            }

            _notifyIcon?.ShowBalloonTip(4000, "Computer Buddy", msg, System.Windows.Forms.ToolTipIcon.Info);
        });
    }

    private void ApplySettingsFromManager(UserSettingsDto dto, bool save)
    {
        if (_settings is null)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            _settings.Scale = dto.Scale;
            _settings.SpeedDipPerSecond = dto.SpeedDipPerSecond;
            _settings.ToggleHotkeyEnabled = dto.ToggleHotkeyEnabled;
            _settings.ToggleHotkeyModifiers = dto.ToggleHotkeyModifiers;
            _settings.ToggleHotkeyVirtualKey = dto.ToggleHotkeyVirtualKey;
            _settings.CharacterMode = dto.CharacterMode;
            _settings.HeadId = dto.HeadId;
            _settings.BodyId = dto.BodyId;
            _settings.HatId = dto.HatId;
            _settings.AccessoryId = dto.AccessoryId;
            _settings.SkinColor = dto.SkinColor;
            _settings.ShirtColor = dto.ShirtColor;
            _settings.PantsColor = dto.PantsColor;
            _settings.HatColor = dto.HatColor;
            _settings.AccentColor = dto.AccentColor;
            _settings.SpriteSheetPngPath = dto.SpriteSheetPngPath;
            _settings.SpriteSheetManifestPath = dto.SpriteSheetManifestPath;
            _settings.OpenAiModel = dto.OpenAiModel;
            _settings.RemindersEnabled = dto.RemindersEnabled;
            _settings.ReminderMinutes = dto.ReminderMinutes;

            _buddyWindow?.ReloadSprites();
            OnSettingsChanged();
            UpdateHotkey();

            if (save)
            {
                _settingsService?.Save(_settings);
            }
        });
    }

    private static string? TryGetManagerExePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var direct = Path.Combine(baseDir, "ComputerBuddy.Manager.exe");
        if (File.Exists(direct))
        {
            return direct;
        }

        var slnDir = FindSolutionRoot();
        var projectDir = Path.Combine(slnDir, "src", "ComputerBuddy.Manager");
        var debug = Path.Combine(projectDir, "bin", "Debug", "net8.0-windows", "ComputerBuddy.Manager.exe");
        if (File.Exists(debug))
        {
            return debug;
        }

        var release = Path.Combine(projectDir, "bin", "Release", "net8.0-windows", "ComputerBuddy.Manager.exe");
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
}

