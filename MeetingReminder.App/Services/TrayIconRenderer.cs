using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MeetingReminder.App.Services;

/// <summary>
/// Renders the tray icon procedurally with a Catppuccin-coloured airplane glyph.
/// Returns a System.Drawing.Icon for H.NotifyIcon.
/// </summary>
public static class TrayIconRenderer
{
    public const int Size = 32;

    public static Icon Render(Color color, Color backdrop)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Rounded square backdrop.
            using (var bg = new SolidBrush(backdrop))
            {
                var rect = new Rectangle(1, 1, Size - 2, Size - 2);
                using var path = RoundedRect(rect, 6);
                g.FillPath(bg, path);
            }

            // Airplane glyph (simplified paper plane shape).
            using var pen = new Pen(color, 2.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            // Fuselage
            g.DrawLine(pen, 6, 16, 26, 16);
            // Nose
            g.DrawLine(pen, 22, 16, 26, 14);
            g.DrawLine(pen, 22, 16, 26, 18);
            // Wing top
            g.DrawLine(pen, 12, 16, 16, 8);
            g.DrawLine(pen, 16, 8, 20, 8);
            // Wing bottom
            g.DrawLine(pen, 12, 16, 16, 24);
            g.DrawLine(pen, 16, 24, 20, 24);
            // Tail
            g.DrawLine(pen, 8, 16, 6, 11);
            g.DrawLine(pen, 8, 16, 6, 21);
        }

        return BitmapToIcon(bmp);
    }

    private static Icon BitmapToIcon(Bitmap bmp)
    {
        var ms = new MemoryStream();
        try
        {
            using var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write((byte)Size);
            bw.Write((byte)Size);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);

            using var pngStream = new MemoryStream();
            bmp.Save(pngStream, ImageFormat.Png);
            byte[] pngData = pngStream.ToArray();

            bw.Write(pngData.Length);
            bw.Write(22);
            bw.Write(pngData);
            bw.Flush();

            ms.Position = 0;
            var icon = new Icon(ms);
            var cloned = (Icon)icon.Clone();
            icon.Dispose();
            return cloned;
        }
        finally
        {
            ms.Dispose();
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, radius, radius, 180, 90);
        path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
        path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Catppuccin Mocha blink cycle
    public static readonly Color[] MochaSyncCycle =
    {
        Color.FromArgb(0xCB, 0xA6, 0xF7), // Mauve
        Color.FromArgb(0x89, 0xB4, 0xFA), // Blue
        Color.FromArgb(0xF5, 0xC2, 0xE7), // Pink
        Color.FromArgb(0x89, 0xDC, 0xEB), // Sky
        Color.FromArgb(0xB4, 0xBE, 0xFE), // Lavender
    };

    public static readonly Color[] LatteSyncCycle =
    {
        Color.FromArgb(0x88, 0x39, 0xEF), // Mauve
        Color.FromArgb(0x1E, 0x66, 0xF5), // Blue
        Color.FromArgb(0xEA, 0x76, 0xCB), // Pink
        Color.FromArgb(0x04, 0xA5, 0xE5), // Sky
        Color.FromArgb(0x72, 0x87, 0xFD), // Lavender
    };

    public static readonly Color MochaIdle = Color.FromArgb(0xCB, 0xA6, 0xF7);     // Mauve
    public static readonly Color MochaBackdrop = Color.FromArgb(0x1E, 0x1E, 0x2E); // Base

    public static readonly Color LatteIdle = Color.FromArgb(0x1E, 0x66, 0xF5);     // Blue
    public static readonly Color LatteBackdrop = Color.FromArgb(0xEF, 0xF1, 0xF5); // Base
}
