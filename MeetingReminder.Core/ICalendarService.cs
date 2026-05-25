using MeetingReminder.Core.Models;

namespace MeetingReminder.Core;

/// <summary>
/// Abstracts calendar access so the poller doesn't know whether events come from
/// Windows Calendar, Outlook COM, or a mock.
/// </summary>
public interface ICalendarService
{
    /// <summary>Whether the user has granted calendar access.</summary>
    bool HasAccess { get; }

    /// <summary>Request access from the user. Returns true if granted.</summary>
    Task<bool> RequestAccessAsync();

    /// <summary>Fetch events starting in the next hour.</summary>
    Task<IReadOnlyList<CalendarEvent>> FetchUpcomingEventsAsync();
}
