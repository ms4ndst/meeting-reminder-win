using MeetingReminder.Core.Models;

namespace MeetingReminder.Core;

/// <summary>
/// Polls the calendar every 60 seconds and fires <see cref="MeetingSoon"/>
/// when an event falls within the alert window. Deduplicates by event ID.
/// </summary>
public sealed class CalendarPoller : IDisposable
{
    private readonly ICalendarService _service;
    private Timer? _timer;
    private readonly HashSet<string> _notifiedIds = new();

    /// <summary>How many minutes before a meeting to fire the alert.</summary>
    public int AlertMinutesBefore { get; set; } = AppConfig.DefaultAlertMinutes;

    /// <summary>Fired when a meeting is within the alert window (±1 minute).</summary>
    public event EventHandler<(CalendarEvent Event, int MinutesUntil)>? MeetingSoon;

    public CalendarPoller(ICalendarService service)
    {
        _service = service;
    }

    public void Start()
    {
        Stop();
        _ = PollAsync();
        _timer = new Timer(_ => _ = PollAsync(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Clear the dedup set so already-alerted events can fire again (useful for testing).</summary>
    public void ResetNotified() => _notifiedIds.Clear();

    private async Task PollAsync()
    {
        try
        {
            if (!_service.HasAccess) return;

            var events = await _service.FetchUpcomingEventsAsync();
            var now = DateTimeOffset.Now;
            var windowLow = AlertMinutesBefore - 1;
            var windowHigh = AlertMinutesBefore + 1;

            foreach (var evt in events)
            {
                var minutes = (int)(evt.StartTime - now).TotalMinutes;
                if (minutes < windowLow || minutes > windowHigh) continue;
                if (!_notifiedIds.Add(evt.Id)) continue;

                MeetingSoon?.Invoke(this, (evt, minutes));
            }
        }
        catch
        {
            // Polling must never crash. Errors are logged at the service layer.
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
