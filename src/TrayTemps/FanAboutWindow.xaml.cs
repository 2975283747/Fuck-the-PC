using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace TrayTemps;

internal partial class FanAboutWindow : Window
{
    public FanAboutWindow(string language, string themeMode)
    {
        InitializeComponent();
        ThemeManager.Apply(this, themeMode);
        WindowAnimator.Attach(this);

        string lang = language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "zh-CN";

        Title = lang == "en-US"
            ? "About Fuck the PC"
            : "关于 Fuck the PC";
        TitleText.Text = Strings.Get(lang, "About");
        SubtitleText.Text = lang == "en-US"
            ? "Fan Control"
            : "风扇控制";
        ThanksTitleText.Text = Strings.Get(lang, "SpecialThanks");
        ThanksBodyText.Text = Strings.Get(lang, "ThanksText");
        VersionText.Text = "Version 2.0.0";
        CloseButton.ToolTip = Strings.Get(lang, "Close");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Link_RequestNavigate(
        object sender,
        RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening the project link is best-effort.
        }

        e.Handled = true;
    }
}
