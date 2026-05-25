using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReminder.App.Services;
using MeetingReminder.Core;
using MeetingReminder.Core.Models;

namespace MeetingReminder.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly MainViewModel _main;
    private bool _suppressPersist;

    [ObservableProperty] private int _alertMinutesBefore = AppConfig.DefaultAlertMinutes;
    [ObservableProperty] private double _flightDurationSeconds = AppConfig.NormalSpeed;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private string _theme = AppConfig.DefaultTheme;
    [ObservableProperty] private string _accent = AppConfig.DefaultAccent;
    [ObservableProperty] private string _googleClientId = "";
    [ObservableProperty] private string _googleClientSecret = "";

    public IReadOnlyList<string> AvailableThemes => ThemeManager.AvailableThemes;
    public IReadOnlyList<string> AvailableAccents => ThemeManager.AvailableAccents;

    /// <summary>Speed preset labels for the UI picker.</summary>
    public static IReadOnlyList<SpeedOption> SpeedOptions { get; } = new[]
    {
        new SpeedOption("Slow", AppConfig.SlowSpeed),
        new SpeedOption("Normal", AppConfig.NormalSpeed),
        new SpeedOption("Fast", AppConfig.FastSpeed),
    };

    public SettingsViewModel(ConfigService config, MainViewModel main, AppConfig cfg)
    {
        _config = config;
        _main = main;
        LoadFrom(cfg);
    }

    public void LoadFrom(AppConfig cfg)
    {
        _suppressPersist = true;
        try
        {
            AlertMinutesBefore = cfg.AlertMinutesBefore;
            FlightDurationSeconds = cfg.FlightDurationSeconds;
            StartWithWindows = cfg.StartWithWindows;
            StartMinimized = cfg.StartMinimized;
            Theme = cfg.Theme;
            Accent = cfg.Accent;
            GoogleClientId = cfg.GoogleClientId;
            GoogleClientSecret = cfg.GoogleClientSecret;
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    [RelayCommand]
    private void SetSpeed(string seconds)
    {
        if (double.TryParse(seconds, System.Globalization.CultureInfo.InvariantCulture, out var val))
            FlightDurationSeconds = val;
    }

    partial void OnAlertMinutesBeforeChanged(int value)
    {
        _main.UpdatePollerAlert();
        Persist();
    }

    partial void OnFlightDurationSecondsChanged(double value) => Persist();
    partial void OnStartMinimizedChanged(bool value) => Persist();

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_suppressPersist) return;
        try { StartupManager.Set(value); }
        catch { }
        Persist();
    }

    partial void OnThemeChanged(string value)
    {
        if (_suppressPersist) return;
        ThemeManager.ApplyTheme(value);
        if (value == "Latte" && Accent == "Mauve") Accent = "Blue";
        else if (value == "Mocha" && Accent == "Blue") Accent = "Mauve";
        Persist();
    }

    partial void OnAccentChanged(string value)
    {
        if (_suppressPersist) return;
        ThemeManager.ApplyAccent(value);
        Persist();
    }

    partial void OnGoogleClientIdChanged(string value) => Persist();
    partial void OnGoogleClientSecretChanged(string value) => Persist();

    /// <summary>True when both Google credential fields have a value.</summary>
    public bool HasGoogleCredentials =>
        !string.IsNullOrWhiteSpace(GoogleClientId) &&
        !string.IsNullOrWhiteSpace(GoogleClientSecret);

    private void Persist()
    {
        if (_suppressPersist) return;
        _main.SaveCurrent();
    }
}

public sealed record SpeedOption(string Label, double Seconds);
