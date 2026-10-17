using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace TrayTemps;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string DegreeCelsius = "\u00B0C";
    private const int ActiveRefreshMilliseconds = 1_000;
    private const int IdleRefreshMilliseconds = 3_000;

    private readonly HardwareMonitor _monitor;
    private readonly FanControlService _fanControl;
    private readonly FanWindowHost _fanWindowHost;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _temperatureItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Icon _applicationIcon;

    private TemperatureReading _lastReading =
        new(null, null, string.Empty, string.Empty, "正在读取...");
    private bool _diagnosticsWritten;
    private bool _fanInventoryWritten;
    private bool _disposed;

    public TrayApplicationContext(
        HardwareMonitor monitor,
        FanControlService fanControl,
        FanWindowHost fanWindowHost,
        PawnIoManager.PawnIoResult pawnIoResult)
    {
        _monitor = monitor;
        _fanControl = fanControl;
        _fanWindowHost = fanWindowHost;

        _temperatureItem = new ToolStripMenuItem("CPU --   GPU --")
        {
            Enabled = false
        };

        var fanControlItem = new ToolStripMenuItem("自定义风扇转速...");
        fanControlItem.Click += (_, _) => _fanWindowHost.Show();

        _autoStartItem = new ToolStripMenuItem("开机自启动")
        {
            CheckOnClick = false
        };
        _autoStartItem.Click += (_, _) => SetAutoStart(!_autoStartItem.Checked);

        var aboutItem = new ToolStripMenuItem("关于");
        var exitItem = new ToolStripMenuItem("退出");

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
        [
            _temperatureItem,
            new ToolStripSeparator(),
            fanControlItem,
            _autoStartItem,
            aboutItem,
            exitItem
        ]);

        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "CPU --   GPU --",
            Visible = true,
            ContextMenuStrip = _menu
        };

        if (pawnIoResult.WasInstalled && pawnIoResult.Success)
        {
            ShowBalloon("CPU 温度读取组件已安装。", ToolTipIcon.Info);
        }
        else if (!pawnIoResult.Success)
        {
            ShowBalloon(
                $"CPU 温度组件安装失败：{PawnIoManager.LastError}",
                ToolTipIcon.Warning);
        }

        aboutItem.Click += (_, _) => ShowAboutDialog();
        exitItem.Click += (_, _) => ExitApplication();

        _timer = new System.Windows.Forms.Timer
        {
            Interval = IdleRefreshMilliseconds
        };
        _timer.Tick += (_, _) => Refresh();

        bool autoStartEnabled = Program.QueryAutoStart();
        if (autoStartEnabled)
        {
            Program.UpdateAutoStartTarget();
        }

        _autoStartItem.Checked = autoStartEnabled;
        Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        HardwareSnapshot snapshot = _monitor.ReadSnapshot();
        _fanControl.Initialize(snapshot);
        _fanControl.Poll(snapshot);

        _lastReading = HardwareMonitor.CreateTemperatureReading(snapshot);
        ApplyReading();

        if (_lastReading.CpuCelsius is null && !_diagnosticsWritten)
        {
            WriteDiagnostics(snapshot);
            _diagnosticsWritten = true;
        }

        if (!_fanInventoryWritten)
        {
            WriteFanInventory(snapshot);
            _fanInventoryWritten = true;
        }

        int desiredInterval = _fanControl.HasActiveControls
            ? ActiveRefreshMilliseconds
            : IdleRefreshMilliseconds;

        if (_timer.Interval != desiredInterval)
        {
            _timer.Interval = desiredInterval;
        }
    }

    private void ApplyReading()
    {
        string cpuText = FormatTemperature("CPU", _lastReading.CpuCelsius);
        string gpuText = FormatTemperature("GPU", _lastReading.GpuCelsius);
        string trayText = $"{cpuText}   {gpuText}";

        _temperatureItem.Text = trayText;
        _notifyIcon.Text = LimitNotifyIconText(trayText);
    }

    private static string FormatTemperature(string label, double? value)
    {
        return value is double temperature
            ? $"{label} {temperature.ToString("0", CultureInfo.InvariantCulture)}{DegreeCelsius}"
            : $"{label} --";
    }

    private static string LimitNotifyIconText(string text)
    {
        const int maximumLength = 63;
        return text.Length <= maximumLength
            ? text
            : text[..(maximumLength - 3)] + "...";
    }

    private void SetAutoStart(bool enabled)
    {
        SchTasksResult result = Program.SetAutoStart(enabled);

        if (!result.Success && enabled)
        {
            MessageBox.Show(
                result.Message,
                "TrayTemps",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        _autoStartItem.Checked = enabled;
        ShowBalloon(
            enabled ? "已开启开机自启动" : "已关闭开机自启动",
            ToolTipIcon.Info);
    }

    private void ShowAboutDialog()
    {
        using var aboutForm = new AboutForm(_applicationIcon);
        aboutForm.ShowDialog();
    }

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipTitle = "Fuck the PC";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(4_000);
    }

    private void ExitApplication()
    {
        if (_disposed)
        {
            return;
        }

        Dispose();
        Application.Exit();
    }

    private static Icon LoadApplicationIcon()
    {
        string? executablePath = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                Icon? icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    return icon;
                }
            }
            catch
            {
                // Fall back to the system icon below.
            }
        }

        return (Icon)SystemIcons.Information.Clone();
    }

    private static void WriteDiagnostics(HardwareSnapshot snapshot)
    {
        try
        {
            string logPath = Path.Combine(Path.GetTempPath(), "TrayTemps-diagnostics.txt");
            var lines = new List<string>
            {
                $"TrayTemps diagnostics: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Elevated: {Program.IsProcessElevated()}",
                $"PawnIO device available: {PawnIoManager.IsDeviceAvailable()}",
                $"PawnIO error: {PawnIoManager.LastError}",
                $"OS: {Environment.OSVersion}",
                string.Empty,
                "Temperature sensors:"
            };

            foreach (TemperatureSensorInfo temperature in snapshot.Temperatures)
            {
                lines.Add(
                    $"  [{temperature.HardwareType}] {temperature.DisplayName}: " +
                    $"{temperature.Value.ToString("0.0", CultureInfo.InvariantCulture)} {DegreeCelsius}");
            }

            lines.Add(string.Empty);
            lines.Add("Fan controls:");

            foreach (FanChannelInfo fan in snapshot.Fans)
            {
                lines.Add(
                    $"  [{fan.HardwareType}] {fan.DisplayName}: " +
                    $"RPM={fan.Rpm?.ToString("0", CultureInfo.InvariantCulture) ?? "--"}, " +
                    $"Percent={fan.Percent?.ToString("0", CultureInfo.InvariantCulture) ?? "--"}, " +
                    $"Mode={fan.ControlMode}");
            }

            File.WriteAllLines(logPath, lines);
        }
        catch
        {
            // Diagnostics are best-effort.
        }
    }

    private void WriteFanInventory(HardwareSnapshot snapshot)
    {
        try
        {
            string logPath = Path.Combine(
                Path.GetTempPath(),
                "TrayTemps-fan-inventory.txt");
            var lines = new List<string>
            {
                $"TrayTemps 2.0 fan inventory: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Config: {_fanControl.ConfigPath}",
                $"Config error: {FanControlService.LastConfigError}",
                $"Temperatures: {snapshot.Temperatures.Count}",
                $"Fan channels: {snapshot.Fans.Count}",
                string.Empty
            };

            foreach (FanChannelInfo fan in snapshot.Fans)
            {
                lines.Add(
                    $"[{fan.HardwareType}] {fan.DisplayName} | " +
                    $"FanSensor={fan.FanSensorName} | " +
                    $"RPM={fan.Rpm?.ToString("0.0", CultureInfo.InvariantCulture) ?? "--"} | " +
                    $"Control={fan.Percent?.ToString("0.0", CultureInfo.InvariantCulture) ?? "--"}% | " +
                    $"Mode={fan.ControlMode}");
            }

            File.WriteAllLines(logPath, lines);
        }
        catch
        {
            // Inventory diagnostics are best-effort.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _fanControl.RestoreAllDefaults();
            _fanWindowHost.Shutdown();
            _applicationIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
