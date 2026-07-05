using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SagiBlock.Helpers;
using SagiBlock.Models;
using SagiBlock.Services;
using WinForms = System.Windows.Forms;

namespace SagiBlock;

public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _trayIcon;
    private NotificationService? _notifications;
    private ScamGuardService? _guard;
    private DispatcherTimer? _timer;
    private bool _checking;
    private Window? _hiddenWindow;
    private IntPtr _hwnd;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            StartupLog.Write(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                StartupLog.Write(ex, "UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            _hiddenWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            _hiddenWindow.Show();
            _hiddenWindow.Hide();
            _hwnd = new WindowInteropHelper(_hiddenWindow).Handle;

            SetupTray();
            StartupLog.Write("Tray ready");

            ToastAppRegistration.EnsureRegistered();

            try
            {
                _notifications = new NotificationService();
                _notifications.Show(
                    L.Format("StartupTitle", L.Get("AppName")),
                    L.Get("StartupBody"),
                    "startup");
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex, "Notification startup failed");
            }

            _guard = new ScamGuardService();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += async (_, _) => await RunCheckAsync();
            _timer.Start();

            var defer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            defer.Tick += async (_, _) =>
            {
                defer.Stop();
                await RunCheckAsync();
            };
            defer.Start();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "OnStartup failed");
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _trayIcon?.Dispose();
        _hiddenWindow?.Close();
        base.OnExit(e);
    }

    private void SetupTray()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = TrayIconHelper.CreateTrayIcon(),
            Visible = true,
            Text = L.Get("AppName")
        };

        _trayIcon.MouseUp += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Right)
                ShowNativeMenu();
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                ShowNativeMenu();
        };
    }

    private void ShowNativeMenu()
    {
        using var menu = new NativeMenu();
        menu.AddLabel($"{L.Get("AppName")} v{GetVersion()}");
        menu.AddLabel("\u00A9 Studio Tomippe");
        menu.AddSeparator();
        menu.AddItem(L.Get("MenuCheckNow"), () => _ = RunCheckAsync(forceNotify: true));
        menu.AddItem(L.Get("MenuOpenLogFolder"), OpenLogFolder);
        menu.AddSeparator();
        menu.AddItem(L.Get("MenuQuit"), () => Shutdown());
        menu.Show(_hwnd);
    }

    private async Task RunCheckAsync(bool forceNotify = false)
    {
        if (_checking || _guard is null)
            return;

        _checking = true;
        try
        {
            var events = await Task.Run(async () => await _guard.CheckOnceAsync()).ConfigureAwait(true);
            if (_notifications is null)
                return;

            foreach (var item in events)
            {
                var body = item.Detail;
                if (item.Kind == GuardEventKind.ScarewareWindowClosed)
                    body += "\n" + L.Get("ScarewareClosedExtra");
                if (item.Kind == GuardEventKind.ScarewareWindowCloseFailed)
                    body += "\n" + L.Get("ScarewareCloseFailedExtra");
                _notifications.Show(item.Title, body, forceNotify ? Guid.NewGuid().ToString("N") : item.Key);
            }

            if (forceNotify && events.Count == 0)
            {
                _notifications.Show(
                    L.Get("CheckCompleteTitle"),
                    L.Get("CheckCompleteBody"),
                    Guid.NewGuid().ToString("N"));
            }
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex, "RunCheckAsync failed");
        }
        finally
        {
            _checking = false;
        }
    }

    private static void OpenLogFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "SagiBlock");
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true
        });
    }

    private static string GetVersion() =>
        typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
