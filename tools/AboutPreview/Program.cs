using System.Drawing;
using System.Windows.Forms;
using TrayTemps;

namespace AboutPreview;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var icon = new Icon(Path.Combine(AppContext.BaseDirectory, "App.ico"));
        Application.Run(new AboutForm(icon));
    }
}
