using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReminder.App.Services;
using MeetingReminder.App.Views;
using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly ICalendarService _windowsCalendar;
    private readonly GoogleCalendarService _googleCalendar;
    private readonly ILogger<MainViewModel> _logger;
    private CalendarPoller? _poller;
    private readonly List<AirplaneOverlayWindow> _overlayWindows = new();
    private MainWindow? _window;

    /// <summary>The currently active calendar source used for polling.</summary>
    private ICalendarService _activeCalendar;

    public SettingsViewModel Settings { get; }
    public LogViewModel Log { get; }
    public ObservableCollection<UpcomingMeetingItem> UpcomingMeetings { get; } = new();

    [ObservableProperty] private bool _hasCalendarAccess;
    [ObservableProperty] private bool _hasGoogleAccess;
    [ObservableProperty] private bool _isFlying;
    [ObservableProperty] private string _statusText = "Idle — connect a calendar to start";

    public MainViewModel(
        ConfigService config,
        ICalendarService windowsCalendar,
        GoogleCalendarService googleCalendar,
        ILoggerFactory loggerFactory,
        AppConfig cfg)
    {
        _config = config;
        _windowsCalendar = windowsCalendar;
        _googleCalendar = googleCalendar;
        _activeCalendar = windowsCalendar;
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        Settings = new SettingsViewModel(config, this, cfg);
        Log = new LogViewModel(config.LogsDirectory);
        HasCalendarAccess = windowsCalendar.HasAccess;
        HasGoogleAccess = googleCalendar.HasAccess;
    }

    public void AttachWindow(MainWindow window) => _window = window;

    // ----- commands ---------------------------------------------------------

    [RelayCommand]
    private async Task RequestCalendarAccess()
    {
        var granted = await _windowsCalendar.RequestAccessAsync();
        HasCalendarAccess = granted;
        if (granted)
        {
            _activeCalendar = _windowsCalendar;
            StatusText = "Windows Calendar connected — monitoring for meetings";
            StartPolling();
            await RefreshUpcomingAsync();
        }
        else
        {
            StatusText = "Windows Calendar access denied";
        }
    }

    [RelayCommand]
    private async Task ConnectGoogle()
    {
        // Pass inline credentials if the user filled them in.
        bool granted;
        if (Settings.HasGoogleCredentials)
        {
            granted = await _googleCalendar.RequestAccessAsync(
                Settings.GoogleClientId.Trim(), Settings.GoogleClientSecret.Trim());
        }
        else
        {
            granted = await _googleCalendar.RequestAccessAsync();
        }

        HasGoogleAccess = granted;
        if (granted)
        {
            _activeCalendar = _googleCalendar;
            HasCalendarAccess = true;
            StatusText = "Google Calendar connected — monitoring for meetings";
            StartPolling();
            await RefreshUpcomingAsync();
        }
        else
        {
            StatusText = "Google Calendar: enter Client ID + Secret in Settings, or place client_secrets.json";
        }
    }

    [RelayCommand]
    private async Task DisconnectGoogle()
    {
        await _googleCalendar.SignOutAsync();
        HasGoogleAccess = false;
        // Fall back to Windows Calendar if it has access.
        if (_windowsCalendar.HasAccess)
        {
            _activeCalendar = _windowsCalendar;
            StartPolling();
        }
        else
        {
            _poller?.Dispose();
            _poller = null;
            HasCalendarAccess = false;
            StatusText = "Google Calendar disconnected";
        }
    }

    /// <summary>Called at startup to silently restore a previous Google session.</summary>
    public async Task TryRestoreGoogleAsync()
    {
        // Prefer inline credentials if configured.
        var ok = Settings.HasGoogleCredentials
            ? await _googleCalendar.TryRestoreSessionAsync(
                  Settings.GoogleClientId.Trim(), Settings.GoogleClientSecret.Trim())
            : await _googleCalendar.TryRestoreSessionAsync();
        if (ok)
        {
            HasGoogleAccess = true;
            HasCalendarAccess = true;
            _activeCalendar = _googleCalendar;
            StatusText = "Google Calendar restored — monitoring for meetings";
            StartPolling();
            await RefreshUpcomingAsync();
        }
    }

    [RelayCommand]
    public void TestAirplane()
    {
        var fake = new CalendarEvent(
            Id: Guid.NewGuid().ToString(),
            Title: "Test Meeting",
            StartTime: DateTimeOffset.Now.AddMinutes(5),
            EndTime: DateTimeOffset.Now.AddMinutes(35));
        ShowAirplane(fake, 5);
    }

    [RelayCommand]
    private async Task RefreshUpcoming()
    {
        await RefreshUpcomingAsync();
    }

    // ----- polling ----------------------------------------------------------

    public void StartPolling()
    {
        if (!_activeCalendar.HasAccess) return;
        HasCalendarAccess = true;

        _poller?.Dispose();
        _poller = new CalendarPoller(_activeCalendar)
        {
            AlertMinutesBefore = Settings.AlertMinutesBefore
        };
        _poller.MeetingSoon += OnMeetingSoon;
        _poller.Start();
        StatusText = "Calendar connected — monitoring for meetings";
        _logger.LogInformation("Polling started (alert {Minutes}min before)", Settings.AlertMinutesBefore);
    }

    public void UpdatePollerAlert()
    {
        if (_poller is not null)
            _poller.AlertMinutesBefore = Settings.AlertMinutesBefore;
    }

    private void OnMeetingSoon(object? sender, (CalendarEvent Event, int MinutesUntil) e)
    {
        // Marshal to UI thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke((Action)(() => OnMeetingSoon(sender, e)));
            return;
        }

        _logger.LogInformation("Meeting soon: {Title} in {Minutes}min", e.Event.Title, e.MinutesUntil);
        ShowAirplane(e.Event, e.MinutesUntil);
    }

    // ----- airplane overlay -------------------------------------------------

    private void ShowAirplane(CalendarEvent evt, int minutesUntil)
    {
        var duration = Settings.FlightDurationSeconds;
        IsFlying = true;

        var overlay = new AirplaneOverlayWindow(evt.Title, minutesUntil, duration);
        overlay.Show();
        _overlayWindows.Add(overlay);

        // Auto-close after animation + fade
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(duration + 1.5)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _overlayWindows.Remove(overlay);
            overlay.Close();
            if (_overlayWindows.Count == 0)
                IsFlying = false;
        };
        timer.Start();
    }

    // ----- upcoming meetings ------------------------------------------------

    private async Task RefreshUpcomingAsync()
    {
        if (!_activeCalendar.HasAccess) return;

        try
        {
            var events = await _activeCalendar.FetchUpcomingEventsAsync();
            UpcomingMeetings.Clear();
            foreach (var e in events.OrderBy(e => e.StartTime))
            {
                UpcomingMeetings.Add(new UpcomingMeetingItem(
                    e.Title,
                    e.StartTime.ToString("HH:mm"),
                    e.EndTime.ToString("HH:mm")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh upcoming meetings");
        }
    }

    // ----- persistence ------------------------------------------------------

    public void SaveCurrent()
    {
        var cfg = new AppConfig(
            AlertMinutesBefore: Settings.AlertMinutesBefore,
            FlightDurationSeconds: Settings.FlightDurationSeconds,
            StartWithWindows: Settings.StartWithWindows,
            StartMinimized: Settings.StartMinimized,
            Theme: Settings.Theme,
            Accent: Settings.Accent,
            GoogleClientId: Settings.GoogleClientId,
            GoogleClientSecret: Settings.GoogleClientSecret);
        _config.Save(cfg);
    }
}

public sealed record UpcomingMeetingItem(string Title, string StartTime, string EndTime);
