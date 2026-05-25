using System.Windows.Threading;
using ComputerBuddy.App.Models;

namespace ComputerBuddy.App.Services.Reminders;

public sealed class ReminderService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly UserSettings _settings;
    private readonly Random _rng = new();

    private readonly string[] _messages =
    [
        "Hydrate check.",
        "Quick stretch break?",
        "Blink check.",
        "Posture check.",
        "Take 10 deep breaths."
    ];

    public ReminderService(UserSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Tick();
    }

    public event Action<string>? Reminder;

    public void Start()
    {
        UpdateFromSettings();
        _timer.Start();
    }

    public void UpdateFromSettings()
    {
        if (!_settings.RemindersEnabled)
        {
            _timer.Stop();
            return;
        }

        var minutes = Math.Clamp(_settings.ReminderMinutes, 1, 24 * 60);
        _timer.Interval = TimeSpan.FromMinutes(minutes);
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private void Tick()
    {
        if (!_settings.RemindersEnabled)
        {
            return;
        }

        var msg = _messages[_rng.Next(_messages.Length)];
        Reminder?.Invoke(msg);
    }
}

