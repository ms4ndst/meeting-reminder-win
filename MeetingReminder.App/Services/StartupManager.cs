using Microsoft.Win32;

namespace MeetingReminder.App.Services;

/// <summary>
/// Manage the HKCU Run entry so MeetingReminder launches with Windows.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MeetingReminder";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrEmpty(value);
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);

        if (enable)
        {
            var exe = GetExecutablePath();
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        }
        else
        {
            try { key.DeleteValue(ValueName, throwOnMissingValue: false); }
            catch { }
        }
    }

    private static string GetExecutablePath()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
            return exe!;

        var loc = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        return loc ?? "MeetingReminder.exe";
    }
}
