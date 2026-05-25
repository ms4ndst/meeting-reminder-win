using H.NotifyIcon;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MeetingReminder.App.Services;

public sealed class NotificationService
{
    private readonly TaskbarIcon? _tray;
    private bool _toastBroken;

    public NotificationService(TaskbarIcon? tray)
    {
        _tray = tray;
    }

    public void NotifyMeetingSoon(string title, int minutes)
        => Notify($"🐱 {title} in {minutes} min", "Your cat is flying!");

    private void Notify(string title, string body)
    {
        if (!_toastBroken)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(body)
                    .Show();
                return;
            }
            catch
            {
                _toastBroken = true;
            }
        }

        try
        {
            _tray?.ShowNotification(title: title, message: body);
        }
        catch { }
    }
}
