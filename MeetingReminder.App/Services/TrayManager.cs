using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MeetingReminder.App.ViewModels;
using H.NotifyIcon;

namespace MeetingReminder.App.Services;

public sealed class TrayManager : IDisposable
{
    private readonly MainViewModel _vm;
    private TaskbarIcon? _tray;
    private DispatcherTimer? _blinkTimer;
    private int _blinkIndex;
    private System.Drawing.Icon? _currentIcon;

    public TrayManager(MainViewModel vm) => _vm = vm;

    public void Initialize()
    {
        try
        {
            _tray = new TaskbarIcon
            {
                ToolTipText = "MeetingReminder",
                ContextMenu = BuildMenu(),
                NoLeftClickDelay = true,
            };

            _tray.TrayLeftMouseDown += (_, _) => ShowMainWindow();
            _tray.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

            _vm.PropertyChanged += OnVmPropertyChanged;
            ThemeManager.ThemeChanged += (_, _) => RefreshIcon();

            RefreshIcon();
            _tray.ForceCreate(enablesEfficiencyMode: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize system tray icon:\n\n{ex.Message}",
                "Tray Icon Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(NewItem("Open MeetingReminder", () => ShowMainWindow(), isHeader: true));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewItem("Test airplane", () => _vm.TestAirplane()));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewItem("Settings…", OpenSettingsTab));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewItem("Exit", ExitApp));
        return menu;
    }

    private MenuItem NewItem(string header, Action onClick, bool isHeader = false)
    {
        var item = new MenuItem { Header = header };
        if (isHeader) item.FontWeight = FontWeights.SemiBold;
        item.Click += (_, _) => onClick();
        return item;
    }

    private void ShowMainWindow()
    {
        var window = Application.Current.Windows
            .OfType<MainWindow>().FirstOrDefault();
        window?.ShowAndActivate();
    }

    private void OpenSettingsTab()
    {
        var window = Application.Current.Windows
            .OfType<MainWindow>().FirstOrDefault();
        if (window is null) return;
        window.ShowAndActivate();

        var tabs = FindChild<TabControl>(window);
        if (tabs is not null && tabs.Items.Count > 1)
            tabs.SelectedIndex = 1;
    }

    private void ExitApp()
    {
        try { _tray?.Dispose(); } catch { }
        Application.Current.Shutdown(0);
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsFlying))
        {
            if (_vm.IsFlying) StartBlinking();
            else StopBlinking();
        }
    }

    private void StartBlinking()
    {
        StopBlinking();
        _blinkIndex = 0;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _blinkTimer.Tick += (_, _) =>
        {
            var cycle = ThemeManager.CurrentTheme == "Latte"
                ? TrayIconRenderer.LatteSyncCycle
                : TrayIconRenderer.MochaSyncCycle;
            var backdrop = ThemeManager.CurrentTheme == "Latte"
                ? TrayIconRenderer.LatteBackdrop
                : TrayIconRenderer.MochaBackdrop;
            var color = cycle[_blinkIndex % cycle.Length];
            _blinkIndex++;
            SetTrayIcon(TrayIconRenderer.Render(color, backdrop));
        };
        _blinkTimer.Start();
    }

    private void StopBlinking()
    {
        _blinkTimer?.Stop();
        _blinkTimer = null;
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        bool latte = ThemeManager.CurrentTheme == "Latte";
        var backdrop = latte ? TrayIconRenderer.LatteBackdrop : TrayIconRenderer.MochaBackdrop;
        var idleColor = latte ? TrayIconRenderer.LatteIdle : TrayIconRenderer.MochaIdle;
        SetTrayIcon(TrayIconRenderer.Render(idleColor, backdrop));
    }

    private void SetTrayIcon(System.Drawing.Icon icon)
    {
        if (_tray is null) return;
        _currentIcon?.Dispose();
        _currentIcon = icon;
        _tray.Icon = icon;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is null) return null;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var deeper = FindChild<T>(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    public void Dispose()
    {
        try { _blinkTimer?.Stop(); } catch { }
        try { _currentIcon?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
    }
}
