using System.Windows;
using MeetingReminder.App.Services;
using MeetingReminder.App.ViewModels;
using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.App;

public partial class App : Application
{
    public static ConfigService Config { get; private set; } = default!;
    public static MainViewModel MainVm { get; private set; } = default!;
    public static ILoggerFactory LoggerFactory { get; private set; } = default!;

    private TrayManager? _tray;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            LoggerFactory?.CreateLogger<App>()?.LogError(e.Exception, "Unhandled UI exception");
        }
        catch { }

        MessageBox.Show(
            $"Unhandled exception: {e.Exception}",
            "MeetingReminder Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            LoggerFactory?.CreateLogger<App>()?.LogError(
                e.ExceptionObject as Exception, "Fatal unhandled exception");
        }
        catch { }

        MessageBox.Show(
            $"Fatal unhandled exception: {e.ExceptionObject}",
            "MeetingReminder Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ---- bootstrap services --------------------------------------------------
            Config = new ConfigService();
            LoggerFactory = BuildLoggerFactory(Config.LogsDirectory);

            var cfg = Config.Load();

            // Sync StartWithWindows with reality.
            var registryHasEntry = StartupManager.IsEnabled();
            if (cfg.StartWithWindows != registryHasEntry)
            {
                cfg = cfg with { StartWithWindows = registryHasEntry };
                Config.Save(cfg);
            }

            // Apply theme + accent before any window is shown.
            ThemeManager.ApplyTheme(cfg.Theme);
            ThemeManager.ApplyAccent(cfg.Accent);

            // ---- handle CLI flags ----------------------------------------------------
            var args = e.Args;
            var startMinimized = HasFlag(args, "--minimized") || cfg.StartMinimized;

            // ---- calendar services ---------------------------------------------------
            var windowsCalendar = new WindowsCalendarService(
                LoggerFactory.CreateLogger<WindowsCalendarService>());
            var googleCalendar = new GoogleCalendarService(
                LoggerFactory.CreateLogger<GoogleCalendarService>(), Config.ConfigDirectory);

            // ---- normal UI startup ---------------------------------------------------
            MainVm = new MainViewModel(Config, windowsCalendar, googleCalendar, LoggerFactory, cfg);

            _tray = new TrayManager(MainVm);
            _tray.Initialize();

            var window = new MainWindow { DataContext = MainVm };
            MainVm.AttachWindow(window);

            if (startMinimized)
            {
                window.WindowState = WindowState.Minimized;
                window.ShowInTaskbar = false;
            }
            else
            {
                window.Show();
            }

            MainVm.StartPolling();

            // Try to silently restore a previous Google Calendar session.
            _ = MainVm.TryRestoreGoogleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start MeetingReminder:\n\n{ex.Message}\n\nSee logs for details.",
                "MeetingReminder Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _tray?.Dispose(); } catch { }
        try { (LoggerFactory as IDisposable)?.Dispose(); } catch { }
        base.OnExit(e);
    }

    private static bool HasFlag(IReadOnlyList<string> args, string flag)
    {
        for (var i = 0; i < args.Count; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static ILoggerFactory BuildLoggerFactory(string logDir)
    {
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new RollingFileLoggerProvider(logDir));
        });
    }
}
