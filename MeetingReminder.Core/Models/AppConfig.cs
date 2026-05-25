namespace MeetingReminder.Core.Models;

/// <summary>
/// Persisted user configuration. Lives at %LOCALAPPDATA%\MeetingReminder\config.json.
/// </summary>
public sealed record AppConfig(
    int AlertMinutesBefore,
    double FlightDurationSeconds,
    bool StartWithWindows,
    bool StartMinimized,
    string Theme,                       // "Mocha" | "Latte"
    string Accent,                      // "Mauve", "Blue", "Lavender", ...
    string GoogleClientId,              // OAuth2 Client ID from Google Cloud Console
    string GoogleClientSecret)          // OAuth2 Client Secret
{
    public const int MinAlertMinutes = 1;
    public const int MaxAlertMinutes = 30;
    public const int DefaultAlertMinutes = 5;

    public const double SlowSpeed = 22.0;
    public const double NormalSpeed = 14.0;
    public const double FastSpeed = 8.0;

    public const string DefaultTheme = "Mocha";
    public const string DefaultAccent = "Mauve";

    /// <summary>Brand new config with sensible defaults.</summary>
    public static AppConfig Default() => new(
        AlertMinutesBefore: DefaultAlertMinutes,
        FlightDurationSeconds: NormalSpeed,
        StartWithWindows: false,
        StartMinimized: false,
        Theme: DefaultTheme,
        Accent: DefaultAccent,
        GoogleClientId: "",
        GoogleClientSecret: "");

    /// <summary>
    /// Clamp / normalise values that may have come from a hand-edited config.json.
    /// </summary>
    public AppConfig Normalised()
    {
        var alert = Math.Clamp(
            AlertMinutesBefore <= 0 ? DefaultAlertMinutes : AlertMinutesBefore,
            MinAlertMinutes,
            MaxAlertMinutes);

        var speed = FlightDurationSeconds;
        if (speed <= 0) speed = NormalSpeed;

        var theme = string.IsNullOrWhiteSpace(Theme) ? DefaultTheme : Theme;
        var accent = string.IsNullOrWhiteSpace(Accent) ? DefaultAccent : Accent;

        return this with
        {
            AlertMinutesBefore = alert,
            FlightDurationSeconds = speed,
            Theme = theme,
            Accent = accent,
            GoogleClientId = GoogleClientId ?? "",
            GoogleClientSecret = GoogleClientSecret ?? "",
        };
    }
}
