using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MeetingReminder.App.Services;

/// <summary>
/// Runtime theme + accent swapping. All controls use DynamicResource so changes
/// repaint live without a restart.
/// </summary>
public static class ThemeManager
{
    public static readonly string[] AvailableThemes = { "Mocha", "Latte" };

    public static readonly string[] AvailableAccents =
    {
        "Rosewater", "Flamingo", "Pink", "Mauve", "Red", "Maroon", "Peach",
        "Yellow", "Green", "Teal", "Sky", "Sapphire", "Blue", "Lavender"
    };

    public static string CurrentTheme { get; private set; } = "Mocha";
    public static string CurrentAccent { get; private set; } = "Mauve";

    public static event EventHandler? ThemeChanged;

    public static void ApplyTheme(string flavor)
    {
        if (!AvailableThemes.Contains(flavor))
            flavor = "Mocha";

        var app = Application.Current;
        if (app is null) return;

        var merged = app.Resources.MergedDictionaries;

        var existing = merged.FirstOrDefault(d =>
            d.Source != null &&
            (d.Source.OriginalString.EndsWith("Mocha.xaml", StringComparison.OrdinalIgnoreCase) ||
             d.Source.OriginalString.EndsWith("Latte.xaml", StringComparison.OrdinalIgnoreCase)));

        var fresh = new ResourceDictionary
        {
            Source = new Uri($"Themes/{flavor}.xaml", UriKind.Relative)
        };

        if (existing is not null)
        {
            var idx = merged.IndexOf(existing);
            merged[idx] = fresh;
        }
        else
        {
            merged.Insert(0, fresh);
        }

        CurrentTheme = flavor;
        ApplyAccent(CurrentAccent);

        foreach (Window w in app.Windows)
            ApplyImmersiveDarkMode(w, flavor == "Mocha");

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void ApplyAccent(string accentName)
    {
        if (!AvailableAccents.Contains(accentName))
            accentName = CurrentTheme == "Latte" ? "Blue" : "Mauve";

        var app = Application.Current;
        if (app is null) return;

        var brushKey = accentName + "Brush";
        if (app.TryFindResource(brushKey) is SolidColorBrush sourceBrush)
        {
            app.Resources["AccentBrush"] = new SolidColorBrush(sourceBrush.Color);
        }

        var focusKey = "LavenderBrush";
        if (app.TryFindResource(focusKey) is SolidColorBrush focusBrush)
            app.Resources["FocusBrush"] = new SolidColorBrush(focusBrush.Color);

        CurrentAccent = accentName;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    // ----- DWM dark mode (Windows 10 1809+) ---------------------------------

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyImmersiveDarkMode(Window window, bool useDark)
    {
        if (window is null) return;

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => ApplyImmersiveDarkMode(window, useDark);
            return;
        }

        var flag = useDark ? 1 : 0;

        if (DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref flag, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE_20H1,
                ref flag, sizeof(int));
        }
    }
}
