using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MeetingReminder.App.Views;

/// <summary>
/// Transparent, topmost, click-through window that flies an airplane + banner
/// across the screen. WPF equivalent of the macOS AirplaneOverlayWindow (NSPanel).
/// </summary>
public partial class AirplaneOverlayWindow : Window
{
    private readonly double _flightDuration;

    public AirplaneOverlayWindow(string meetingTitle, int minutesUntil, double flightDuration)
    {
        InitializeComponent();

        _flightDuration = flightDuration;
        BannerText.Text = $"{meetingTitle} in {minutesUntil} min";

        // Size to primary screen, position ~65% up.
        var screen = SystemParameters.WorkArea;
        var height = 110.0;
        var yPos = screen.Top + screen.Height * 0.35 - height / 2;

        Left = screen.Left;
        Top = yPos;
        Width = screen.Width;
        Height = height;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Measure the assembly so we know how wide it is.
        AirplaneAssembly.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var assemblyWidth = AirplaneAssembly.DesiredSize.Width;
        if (assemblyWidth < 100) assemblyWidth = 600; // fallback

        var screenWidth = Width;

        // Start off-left, fly to off-right.
        var startX = -assemblyWidth - 50;
        var endX = screenWidth + 50;

        Canvas.SetLeft(AirplaneAssembly, startX);

        // Slide animation
        var slide = new DoubleAnimation
        {
            From = startX,
            To = endX,
            Duration = TimeSpan.FromSeconds(_flightDuration)
        };
        AirplaneAssembly.BeginAnimation(Canvas.LeftProperty, slide);

        // Fade out in the last 0.6 seconds.
        var fade = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(0.6),
            BeginTime = TimeSpan.FromSeconds(_flightDuration - 0.6)
        };
        AirplaneAssembly.BeginAnimation(OpacityProperty, fade);
    }
}
