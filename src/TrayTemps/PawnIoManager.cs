using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace TrayTemps;

internal static class PawnIoManager
{
    private const string ResourceName = "TrayTemps.Resources.PawnIO_setup.exe";
    private const string DevicePath = @"\\?\GLOBALROOT\Device\PawnIO";
    private const string InstallerFileName = "PawnIO_setup_2.2.0.exe";
    private const string InstallerSha256 = "1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032";

    private const uint OpenExisting = 3;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;

    public static string LastError { get; private set; } = string.Empty;

    public static PawnIoResult EnsureInstalled()
    {
        LastError = string.Empty;

        if (IsDeviceAvailable())
        {
            return new PawnIoResult(true, false);
        }

        if (!Program.IsProcessElevated())
        {
            LastError = "PawnIO 驱动缺失，且当前程序没有管理员权限。";
            return new PawnIoResult(false, false);
        }

        try
        {
            string installerPath = ExtractInstaller();
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? Path.GetTempPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("-install");
            startInfo.ArgumentList.Add("-silent");

            using var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(120_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The installer may already have exited.
                }

                LastError = "PawnIO 驱动安装超时。";
                return new PawnIoResult(false, true);
            }

            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (IsDeviceAvailable())
                {
                    return new PawnIoResult(true, true);
                }

                Thread.Sleep(250);
            }

            LastError = FirstNonEmpty(error, output);
            if (string.IsNullOrWhiteSpace(LastError))
            {
                LastError = $"PawnIO 安装程序返回代码 {process.ExitCode}，但驱动设备未出现。";
            }

            return new PawnIoResult(false, true);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return new PawnIoResult(false, true);
        }
    }

    public static bool IsDeviceAvailable()
    {
        try
        {
            using SafeFileHandle handle = CreateFile(
                DevicePath,
                0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            return !handle.IsInvalid;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractInstaller()
    {
        string installerPath = Path.Combine(Path.GetTempPath(), InstallerFileName);

        if (File.Exists(installerPath) &&
            string.Equals(
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(installerPath))),
                InstallerSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return installerPath;
        }

        Assembly assembly = typeof(PawnIoManager).Assembly;
        using Stream? resource = assembly.GetManifestResourceStream(ResourceName);

        if (resource is null)
        {
            throw new InvalidOperationException("exe 内缺少 PawnIO 安装组件。");
        }

        string temporaryPath = installerPath + ".tmp";
        using (FileStream output = File.Create(temporaryPath))
        {
            resource.CopyTo(output);
        }

        File.Move(temporaryPath, installerPath, overwrite: true);
        return installerPath;
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second.Trim() : first.Trim();
    }

    [System.Runtime.InteropServices.DllImport(
        "kernel32.dll",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    internal readonly record struct PawnIoResult(bool Success, bool WasInstalled);
}
