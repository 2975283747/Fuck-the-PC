using System.Windows.Threading;

namespace TrayTemps;

internal sealed class FanWindowHost : IDisposable
{
    private readonly object _sync = new();
    private readonly FanControlService _fanControl;

    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private FanControlWindow? _window;
    private bool _disposed;

    public FanWindowHost(FanControlService fanControl)
    {
        _fanControl = fanControl;
    }

    public void Show()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_dispatcher is not null && _thread is { IsAlive: true })
            {
                _dispatcher.BeginInvoke(
                    () =>
                    {
                        if (_window is null)
                        {
                            return;
                        }

                        _window.Show();
                        _window.WindowState = System.Windows.WindowState.Normal;
                        _window.Activate();
                        _window.Focus();
                    },
                    DispatcherPriority.Normal);
                return;
            }

            _thread = new Thread(WindowThreadMain)
            {
                IsBackground = true,
                Name = "TrayTemps.FanControlUI"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    private void WindowThreadMain()
    {
        try
        {
            var window = new FanControlWindow(_fanControl);
            _window = window;
            _dispatcher = Dispatcher.CurrentDispatcher;
            window.Closed += (_, _) => Dispatcher.CurrentDispatcher.InvokeShutdown();
            window.Show();
            Dispatcher.Run();
        }
        finally
        {
            _window = null;
            _dispatcher = null;
        }
    }

    public void Shutdown()
    {
        Dispatcher? dispatcher;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            dispatcher = _dispatcher;
        }

        if (dispatcher is null)
        {
            return;
        }

        try
        {
            dispatcher.Invoke(
                () =>
                {
                    _window?.AllowClose();
                    _window?.Close();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                },
                DispatcherPriority.Normal);
        }
        catch
        {
            // Shutdown is best-effort.
        }
    }

    public void Dispose()
    {
        Shutdown();
    }
}
