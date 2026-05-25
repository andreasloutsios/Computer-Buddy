using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ComputerBuddy.App.Services.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly int _id;
    private readonly HwndSource _source;

    public GlobalHotkeyService(nint hwnd, ModifierKeys modifiers, uint vk)
    {
        _id = GetHashCode();
        _source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Failed to create HwndSource.");
        _source.AddHook(WndProc);

        if (!RegisterHotKey(hwnd, _id, (uint)modifiers, vk))
        {
            throw new InvalidOperationException("Failed to register global hotkey.");
        }

        Hwnd = hwnd;
    }

    public event Action? Triggered;
    public nint Hwnd { get; }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        UnregisterHotKey(Hwnd, _id);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam == _id)
        {
            handled = true;
            Triggered?.Invoke();
        }

        return 0;
    }

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}

