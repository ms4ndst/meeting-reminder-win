using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Xunit;

namespace MeetingReminder.Tests;

public class CalendarPollerTests
{
    [Fact]
    public void Default_AlertMinutesBefore_Is_Five()
    {
        var poller = new CalendarPoller(new FakeCalendarService());
        Assert.Equal(5, poller.AlertMinutesBefore);
    }

    [Fact]
    public void AlertMinutesBefore_Can_Be_Changed()
    {
        var poller = new CalendarPoller(new FakeCalendarService());
        poller.AlertMinutesBefore = 10;
        Assert.Equal(10, poller.AlertMinutesBefore);
    }

    [Fact]
    public void ResetNotified_Clears_State()
    {
        var poller = new CalendarPoller(new FakeCalendarService());
        poller.ResetNotified();
        // Should not throw — just verifies it's callable.
    }

    private sealed class FakeCalendarService : ICalendarService
    {
        public bool HasAccess => true;
        public Task<bool> RequestAccessAsync() => Task.FromResult(true);
        public Task<IReadOnlyList<CalendarEvent>> FetchUpcomingEventsAsync()
            => Task.FromResult<IReadOnlyList<CalendarEvent>>(Array.Empty<CalendarEvent>());
    }
}
