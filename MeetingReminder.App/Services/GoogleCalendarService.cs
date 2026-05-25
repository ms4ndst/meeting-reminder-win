using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Microsoft.Extensions.Logging;
using CalendarEvent = MeetingReminder.Core.Models.CalendarEvent;

namespace MeetingReminder.App.Services;

/// <summary>
/// Google Calendar integration via OAuth2 browser flow.
/// Users sign in through their browser; tokens are persisted in %LOCALAPPDATA%\MeetingReminder\google-token.
/// 
/// Setup: place a client_secrets.json (from Google Cloud Console → APIs → Credentials → OAuth2 Desktop app)
/// next to the exe or in %LOCALAPPDATA%\MeetingReminder\.
/// </summary>
public sealed class GoogleCalendarService : ICalendarService, IDisposable
{
    private static readonly string[] Scopes = { CalendarService.Scope.CalendarReadonly };
    private const string ApplicationName = "MeetingReminder";

    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly string _configDir;
    private CalendarService? _service;
    private UserCredential? _credential;

    public GoogleCalendarService(ILogger<GoogleCalendarService> logger, string configDir)
    {
        _logger = logger;
        _configDir = configDir;
    }

    public bool HasAccess => _service is not null;

    /// <summary>
    /// Initiates the OAuth2 browser flow. Opens the user's default browser to
    /// Google's consent page. Tokens are cached to disk for subsequent launches.
    /// </summary>
    public async Task<bool> RequestAccessAsync()
    {
        try
        {
            var secrets = LoadSecrets(null, null);
            if (secrets is null) return false;
            return await AuthorizeWithSecretsAsync(secrets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar OAuth failed");
            return false;
        }
    }

    /// <summary>
    /// Initiates OAuth2 using an inline Client ID + Client Secret entered in Settings.
    /// No client_secrets.json file needed.
    /// </summary>
    public async Task<bool> RequestAccessAsync(string clientId, string clientSecret)
    {
        try
        {
            var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
            return await AuthorizeWithSecretsAsync(secrets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar OAuth failed (inline credentials)");
            return false;
        }
    }

    private async Task<bool> AuthorizeWithSecretsAsync(ClientSecrets secrets)
    {
        var tokenDir = Path.Combine(_configDir, "google-token");

        _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(tokenDir, true));

        _service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential,
            ApplicationName = ApplicationName,
        });

        _logger.LogInformation("Google Calendar access granted");
        return true;
    }

    public async Task<IReadOnlyList<CalendarEvent>> FetchUpcomingEventsAsync()
    {
        if (_service is null)
            return Array.Empty<CalendarEvent>();

        try
        {
            var request = _service.Events.List("primary");
            request.TimeMinDateTimeOffset = DateTimeOffset.Now;
            request.TimeMaxDateTimeOffset = DateTimeOffset.Now.AddHours(1);
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.MaxResults = 20;

            var events = await request.ExecuteAsync();

            return (events.Items ?? Enumerable.Empty<Event>())
                .Where(e => e.Start?.DateTimeDateTimeOffset is not null)
                .Select(e => new CalendarEvent(
                    Id: e.Id ?? Guid.NewGuid().ToString(),
                    Title: FormatTitle(e),
                    StartTime: e.Start!.DateTimeDateTimeOffset!.Value,
                    EndTime: e.End?.DateTimeDateTimeOffset ?? e.Start.DateTimeDateTimeOffset!.Value.AddMinutes(30)
                ))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Google Calendar events");
            return Array.Empty<CalendarEvent>();
        }
    }

    /// <summary>
    /// Sign out — revoke tokens and delete the cached credential.
    /// </summary>
    public async Task SignOutAsync()
    {
        if (_credential is not null)
        {
            try { await _credential.RevokeTokenAsync(CancellationToken.None); }
            catch { /* best effort */ }
        }

        _service?.Dispose();
        _service = null;
        _credential = null;

        var tokenDir = Path.Combine(_configDir, "google-token");
        try
        {
            if (Directory.Exists(tokenDir))
                Directory.Delete(tokenDir, recursive: true);
        }
        catch { }

        _logger.LogInformation("Google Calendar signed out");
    }

    /// <summary>
    /// Try to silently restore a previous session from cached tokens (client_secrets.json).
    /// </summary>
    public Task<bool> TryRestoreSessionAsync() => TryRestoreWithSecretsAsync(LoadSecrets(null, null));

    /// <summary>
    /// Try to silently restore a previous session using inline credentials.
    /// </summary>
    public Task<bool> TryRestoreSessionAsync(string clientId, string clientSecret)
        => TryRestoreWithSecretsAsync(new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret });

    private async Task<bool> TryRestoreWithSecretsAsync(ClientSecrets? secrets)
    {
        if (secrets is null) return false;

        var tokenDir = Path.Combine(_configDir, "google-token");
        if (!Directory.Exists(tokenDir)) return false;

        try
        {
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenDir, true));

            if (_credential.Token.IsStale)
                await _credential.RefreshTokenAsync(CancellationToken.None);

            _service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = ApplicationName,
            });

            _logger.LogInformation("Google Calendar session restored from cache");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore Google Calendar session");
            return false;
        }
    }

    /// <summary>
    /// Build ClientSecrets from inline values or from a client_secrets.json file.
    /// Inline credentials (clientId/clientSecret) take priority when provided.
    /// </summary>
    private ClientSecrets? LoadSecrets(string? clientId, string? clientSecret)
    {
        // Prefer inline credentials.
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            return new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        // Fall back to client_secrets.json file.
        var path = FindClientSecretsFile();
        if (path is null)
        {
            _logger.LogWarning(
                "No Google credentials available. Enter Client ID + Secret in Settings, " +
                "or place client_secrets.json in the app folder or {Dir}", _configDir);
            return null;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GoogleClientSecrets.FromStream(stream).Secrets;
    }

    private string? FindClientSecretsFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "client_secrets.json"),
            Path.Combine(_configDir, "client_secrets.json"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string FormatTitle(Event e)
    {
        var fallback = string.IsNullOrWhiteSpace(e.Summary) ? "Untitled Meeting" : e.Summary;

        var attendees = (e.Attendees ?? Enumerable.Empty<EventAttendee>())
            .Where(a => a.Self != true && !string.IsNullOrEmpty(a.DisplayName))
            .Select(a => a.DisplayName!)
            .Take(3)
            .ToList();

        if (attendees.Count == 0) return fallback;

        return attendees.Count switch
        {
            1 => $"Meeting with {attendees[0]}",
            2 => $"Meeting with {attendees[0]} and {attendees[1]}",
            _ => $"Meeting with {attendees[0]}, {attendees[1]} +{attendees.Count - 2} more"
        };
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
