using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MeetingReminder.App.Services;

namespace MeetingReminder.App.ViewModels;

public sealed partial class LogViewModel : ViewModelBase
{
    private const int MaxLines = 2000;

    [ObservableProperty] private bool _autoScroll = true;

    public ObservableCollection<string> Lines { get; } = new();

    public LogViewModel(string logsDir)
    {
        try
        {
            var today = Path.Combine(logsDir, $"meetingreminder-{DateTime.Now:yyyy-MM-dd}.log");
            if (File.Exists(today))
            {
                using var fs = new FileStream(today, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var rd = new StreamReader(fs);
                while (rd.ReadLine() is { } line)
                {
                    Lines.Add(line);
                    if (Lines.Count > MaxLines) Lines.RemoveAt(0);
                }
            }
        }
        catch { }

        RollingFileLoggerProvider.LogLine += OnLogLine;
    }

    private void OnLogLine(object? sender, string line)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.BeginInvoke(
                (Action)(() => OnLogLine(sender, line)));
            return;
        }

        Lines.Add(line);
        while (Lines.Count > MaxLines) Lines.RemoveAt(0);
    }
}
