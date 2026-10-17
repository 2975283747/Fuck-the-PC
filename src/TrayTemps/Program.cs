using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace TrayTemps;

internal static class Program
{
    private const string TaskName = "TrayTemps-TemperatureMonitor";
    private const string MutexName = @"Local\TrayTemps.SingleInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(static arg =>
                string.Equals(arg, "--probe", StringComparison.OrdinalIgnoreCase)))
        {
            RunProbe();
            return;
        }

        if (args.Any(static arg =>
                string.Equals(arg, "--ui-test", StringComparison.OrdinalIgnoreCase)))
        {
            RunUiTest("System");
            return;
        }

        if (args.Any(static arg =>
                string.Equals(arg, "--ui-test-dark", StringComparison.OrdinalIgnoreCase)))
        {
            RunUiTest("Dark");
            return;
        }

        if (args.Any(static arg =>
                string.Equals(arg, "--curve-test", StringComparison.OrdinalIgnoreCase)))
        {
            RunCurveTest();
            return;
        }

        ClosePreviousVersionIfElevated();

        if (TryMigrateToStableExecutable())
        {
            return;
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        PawnIoManager.PawnIoResult pawnIoResult = PawnIoManager.EnsureInstalled();

        using var monitor = new HardwareMonitor();
        using var fanControl = new FanControlService(monitor);
        using var fanWindowHost = new FanWindowHost(fanControl);
        using var trayContext = new TrayApplicationContext(
            monitor,
            fanControl,
            fanWindowHost,
            pawnIoResult);

        Application.Run(trayContext);
    }

    private static void RunProbe()
    {
        using var monitor = new HardwareMonitor();
        HardwareSnapshot snapshot = monitor.ReadSnapshot();
        using var fanControl = new FanControlService(monitor);
        fanControl.Initialize(snapshot);
        fanControl.Poll(snapshot);
        FanControlUiState fanState = fanControl.GetUiState();
        string path = Path.Combine(Path.GetTempPath(), "TrayTemps-fan-probe.txt");
        var lines = new List<string>
        {
            $"TrayTemps 2.0 fan probe: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Error: {snapshot.Error ?? "(none)"}",
            $"Temperatures: {snapshot.Temperatures.Count}",
            $"Fan channels: {snapshot.Fans.Count}",
            $"Speed cards: {fanState.Profiles.Count}",
            $"Profile choices: {fanState.ProfileChoices.Count}",
            $"Config path: {fanControl.ConfigPath}",
            $"Config error: {FanControlService.LastConfigError}",
            string.Empty,
            "Temperature sensors:"
        };

        foreach (TemperatureSensorInfo temperature in snapshot.Temperatures)
        {
            lines.Add(
                $"  [{temperature.HardwareType}] {temperature.DisplayName} = " +
                $"{temperature.Value:0.0} \u00B0C");
        }

        lines.Add(string.Empty);
        lines.Add("Fan channels:");

        foreach (FanChannelInfo fan in snapshot.Fans)
        {
            lines.Add(
                $"  [{fan.HardwareType}] {fan.DisplayName} | " +
                $"FanSensor={fan.FanSensorName} | RPM={fan.Rpm?.ToString("0.0") ?? "--"} | " +
                $"Control={fan.Percent?.ToString("0.0") ?? "--"}% | Mode={fan.ControlMode}");
        }

        lines.Add(string.Empty);
        lines.Add("Speed cards:");

        foreach (ProfileCardState profile in fanState.Profiles)
        {
            lines.Add(
                $"  {profile.Name} | Mode={profile.Mode} | " +
                $"Sensor={profile.TemperatureSensorName} | {profile.Summary}");
        }

        File.WriteAllLines(path, lines);
    }

    private static void RunUiTest(string themeMode)
    {
        using var monitor = new HardwareMonitor();
        using var fanControl = new FanControlService(monitor);
        HardwareSnapshot snapshot = monitor.ReadSnapshot();
        fanControl.Initialize(snapshot);
        fanControl.Poll(snapshot);
        fanControl.SetThemeMode(themeMode);

        var application = new System.Windows.Application();
        application.Run(new FanControlWindow(fanControl));
    }

    private static void RunCurveTest()
    {
        var profile = new SpeedProfileConfig
        {
            Name = "Curve Test",
            Mode = SpeedMode.Curve,
            TemperatureSensorId = "test",
            CurvePoints =
            [
                new CurvePoint { Temperature = 30, Percent = 20 },
                new CurvePoint { Temperature = 55, Percent = 45 },
                new CurvePoint { Temperature = 75, Percent = 78 },
                new CurvePoint { Temperature = 95, Percent = 100 }
            ]
        };

        var temperatures = new[]
        {
            new TemperatureChoice("test", "GPU Core", 50)
        };

        var application = new System.Windows.Application();
        application.Run(new SpeedProfileEditorWindow(
            profile,
            () => temperatures,
            "zh-CN",
            "Light"));
    }

    internal static bool IsProcessElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    internal static bool QueryAutoStart()
    {
        return RunSchTasks(["/Query", "/TN", TaskName]).Success;
    }

    internal static void UpdateAutoStartTarget()
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        RunSchTasks(CreateAutoStartArguments(executablePath));
    }

    internal static SchTasksResult SetAutoStart(bool enabled)
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new SchTasksResult(false, "无法确定程序路径。");
        }

        string[] arguments = enabled
            ? CreateAutoStartArguments(executablePath)
            :
            [
                "/Delete",
                "/TN", TaskName,
                "/F"
            ];

        return RunSchTasks(arguments);
    }

    private static void ClosePreviousVersionIfElevated()
    {
        if (!IsProcessElevated())
        {
            return;
        }

        string[] previousProcessNames =
        [
            "TrayTemps",
            "TrayTemps-CPU-fix",
            "TrayTemps-Icon",
            "TrayTemps-About",
            "TrayTemps-AboutFix"
        ];

        foreach (string processName in previousProcessNames)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                if (process.Id == Environment.ProcessId)
                {
                    process.Dispose();
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit(3_000);
                }
                catch
                {
                    // If the old instance cannot be closed, the mutex below prevents duplicates.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static bool TryMigrateToStableExecutable()
    {
        if (!IsProcessElevated())
        {
            return false;
        }

        string? currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return false;
        }

        string? directory = Path.GetDirectoryName(currentPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        string targetPath = Path.Combine(directory, "TrayTemps.exe");
        if (string.Equals(
                Path.GetFullPath(currentPath),
                Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            File.Copy(currentPath, targetPath, overwrite: true);

            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = directory,
                UseShellExecute = false
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string[] CreateAutoStartArguments(string executablePath)
    {
        return
        [
            "/Create",
            "/TN", TaskName,
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/IT",
            "/TR", $"\"{executablePath}\"",
            "/F"
        ];
    }

    private static SchTasksResult RunSchTasks(string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The process may already have exited.
                }

                return new SchTasksResult(false, "计划任务操作超时。");
            }

            if (process.ExitCode == 0)
            {
                return new SchTasksResult(true, string.Empty);
            }

            string message = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput.Trim()
                : standardError.Trim();

            return new SchTasksResult(
                false,
                string.IsNullOrWhiteSpace(message)
                    ? $"schtasks.exe 返回错误代码 {process.ExitCode}。"
                    : message);
        }
        catch (Exception ex)
        {
            return new SchTasksResult(false, ex.Message);
        }
    }
}

internal readonly record struct SchTasksResult(bool Success, string Message);
