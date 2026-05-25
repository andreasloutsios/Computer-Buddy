using System.Windows;
using System.Windows.Threading;

namespace ComputerBuddy.App.Windows;

public partial class SystemInfoWindow : Window
{
    private readonly DispatcherTimer _timer;

    public SystemInfoWindow()
    {
        InitializeComponent();

        MachineNameText.Text = $"Machine: {Environment.MachineName}";
        OsText.Text = $"OS: {Environment.OSVersion}";

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            Refresh();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var ps = System.Windows.Forms.SystemInformation.PowerStatus;
        var percent = (int)Math.Round(ps.BatteryLifePercent * 100);

        BatteryText.Text = $"Battery: {percent}% ({ps.PowerLineStatus})";
        TimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

