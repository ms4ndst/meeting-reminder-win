namespace MeetingReminder.Core.Models;

/// <summary>
/// A single calendar event, normalised from whatever calendar source provides it.
/// </summary>
public sealed record CalendarEvent(
    string Id,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
