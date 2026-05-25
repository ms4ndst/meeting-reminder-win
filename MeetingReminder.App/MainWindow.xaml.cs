using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using MeetingReminder.App.Services;
using MeetingReminder.App.ViewModels;

namespace MeetingReminder.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ThemeManager.ApplyImmersiveDarkMode(this, ThemeManager.CurrentTheme == "Mocha");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Log.Lines.CollectionChanged += LogLinesChanged;
        }
    }

    private void LogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!vm.Log.AutoScroll) return;
        if (LogList.Items.Count == 0) return;

        Dispatcher.BeginInvoke((Action)(() =>
        {
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }));
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Closing the window minimises to tray.
        e.Cancel = true;
        Hide();
        ShowInTaskbar = false;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            ShowInTaskbar = false;
        }
    }

    public void ShowAndActivate()
    {
        Show();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }
}
