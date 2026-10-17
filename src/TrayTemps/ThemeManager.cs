using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;

namespace TrayTemps;

internal static class ThemeManager
{
    public static event Action? ThemeChanged;

    public static string Resolve(string mode)
    {
        if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            return "Dark";
        }

        if (mode.Equals("Light", StringComparison.OrdinalIgnoreCase))
        {
            return "Light";
        }

        return IsWindowsDark() ? "Dark" : "Light";
    }

    public static void Apply(Window window, string mode, bool animate = false)
    {
        bool dark = Resolve(mode) == "Dark";

        Set(window, "AppBackground", dark ? "#0D0F13" : "#F5F5F7");
        Set(window, "TitleBarBackground", dark ? "#15181E" : "#FBFBFD");
        Set(window, "SidebarBackground", dark ? "#181C23" : "#F0F0F3");
        Set(window, "CardBackground", dark ? "#242A33" : "#FFFFFF");
        Set(window, "CardDisabledBackground", dark ? "#303742" : "#F0F0F3");
        Set(window, "TextPrimary", dark ? "#F4F6FA" : "#1D1D1F");
        Set(window, "TextSecondary", dark ? "#B6BFCC" : "#6E6E73");
        Set(window, "BorderBrush", dark ? "#414A57" : "#DEDEE3");
        Set(window, "DividerBrush", dark ? "#39414D" : "#E3E3E8");
        Set(window, "ControlBackground", dark ? "#2C333D" : "#ECECF0");
        Set(window, "ControlHover", dark ? "#38414D" : "#E1E1E6");
        Set(window, "ControlPressed", dark ? "#465160" : "#D6D6DC");
        Set(window, "WindowHover", dark ? "#38414D" : "#E8E8ED");
        Set(window, "ToggleOff", dark ? "#596575" : "#D1D5DB");
        Set(window, "Accent", dark ? "#0A84FF" : "#0071E3");
        Set(window, "AccentSoft", dark ? "#21364D" : "#E8F2FF");
        Set(window, "Danger", dark ? "#FF6B63" : "#FF3B30");
        Set(window, "ShadowColor", "#000000");
        Set(window, "FanFrameBrush", dark ? "#FFFFFF" : "#0A0A0A");
        Set(window, "FanInnerBrush", dark ? "#242A33" : "#FFFFFF");
        Set(window, "FanHoleBrush", dark ? "#0D0F13" : "#F5F5F7");
        Set(window, "FanBladeBrush", dark ? "#FFFFFF" : "#0A0A0A");
        Set(window, "FanHubBrush", dark ? "#FFFFFF" : "#0A0A0A");
        Set(window, "FanEdgeBrush", dark ? "#D4D9E1" : "#000000");
        ThemeChanged?.Invoke();

        if (animate && window.IsLoaded && window.Content is FrameworkElement root)
        {
            root.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation
                {
                    From = 0.62,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
                },
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private static bool IsWindowsDark()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                writable: false);
            object? value = key?.GetValue("AppsUseLightTheme");
            return value is int integer && integer == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Set(Window window, string key, string hex)
    {
        Color color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        window.Resources[key] = new SolidColorBrush(color);
    }
}
