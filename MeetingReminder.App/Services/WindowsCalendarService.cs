using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.Appointments;

namespace MeetingReminder.App.Services;

/// <summary>
/// Reads from the Windows Calendar app via WinRT AppointmentStore.
/// This is the Windows equivalent of macOS EventKit: it reads from all accounts
/// the user has connected (Outlook.com, Google, iCloud, Exchange).
/// </summary>
public sealed class WindowsCalendarService : ICalendarService
{
    private readonly ILogger<WindowsCalendarService> _logger;
    private AppointmentStore? _store;
    private bool _accessDenied;

    public WindowsCalendarService(ILogger<WindowsCalendarService> logger)
    {
        _logger = logger;
    }

    public bool HasAccess => _store is not null && !_accessDenied;

    public async Task<bool> RequestAccessAsync()
    {
        try
        {
            _store = await AppointmentManager.RequestStoreAsync(
                AppointmentStoreAccessType.AllCalendarsReadOnly);
            _accessDenied = _store is null;
            if (!_accessDenied)
                _logger.LogInformation("Calendar access granted");
            else
                _logger.LogWarning("Calendar access denied by user");
            return !_accessDenied;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request calendar access");
            _accessDenied = true;
            return false;
        }
    }

    public async Task<IReadOnlyList<CalendarEvent>> FetchUpcomingEventsAsync()
    {
        if (_store is null)
            return Array.Empty<CalendarEvent>();

        try
        {
            var now = DateTimeOffset.Now;
            // Fetch events until end of day to get the whole day's events
            var endOfDay = now.Date.AddDays(1).AddSeconds(-1);
            var timeSpan = endOfDay - now;
            var appointments = await _store.FindAppointmentsAsync(
                now, timeSpan);

            return appointments.Select(a => new CalendarEvent(
                Id: a.LocalId ?? Guid.NewGuid().ToString(),
                Title: FormatTitle(a),
                StartTime: a.StartTime,
                EndTime: a.StartTime + a.Duration
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch upcoming events");
            return Array.Empty<CalendarEvent>();
        }
    }

    /// <summary>
    /// Prefer "Meeting with <organizer>" when available; fall back to the appointment subject.
    /// </summary>
    private static string FormatTitle(Appointment a)
    {
        var fallback = string.IsNullOrWhiteSpace(a.Subject) ? "Untitled Meeting" : a.Subject;

        if (a.Invitees is { Count: > 0 })
        {
            var names = a.Invitees
                .Select(i => i.Address)
                .Where(n => !string.IsNullOrEmpty(n))
                .Take(3)
                .ToList();

            if (names.Count > 0)
            {
                return names.Count switch
                {
                    1 => $"Meeting with {names[0]}",
                    2 => $"Meeting with {names[0]} and {names[1]}",
                    _ => $"Meeting with {names[0]}, {names[1]} +{names.Count - 2} more"
                };
            }
        }

        return fallback;
    }
}
