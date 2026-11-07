using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PowerpalTray;

internal static class TrayIconRenderer
{
    private const int SuperSample = 4;

    private static readonly (int Dx, int Dy)[] OutlineOffsets =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (1, -1), (-1, 1), (1, 1),
    };

    private static readonly StringFormat TightFormat = (StringFormat)StringFormat.GenericTypographic.Clone();

    public static Icon CreateIcon(string text, Color color)
    {
        var targetSize = SystemInformation.SmallIconSize;
        var width = Math.Max(targetSize.Width, 16);
        var height = Math.Max(targetSize.Height, 16);

        using var big = RenderContent(text, color, width * SuperSample, height * SuperSample);
        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(big, 0, 0, width, height);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Bitmap RenderContent(string text, Color color, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        var dotIndex = text.IndexOf('.');
        var hasDot = dotIndex >= 0;
        var beforeDot = hasDot ? text[..dotIndex] : text;
        var afterDot = hasDot ? text[(dotIndex + 1)..] : string.Empty;
        var digitsOnly = beforeDot + afterDot;

        using var font = GetFittingFont(g, digitsOnly, width, height);
        var beforeSize = g.MeasureString(beforeDot, font, PointF.Empty, TightFormat);
        var afterSize = hasDot ? g.MeasureString(afterDot, font, PointF.Empty, TightFormat) : SizeF.Empty;
        var dotDiameter = Math.Max(1f, height / 9f);
        var dotGap = hasDot ? dotDiameter * 1.6f : 0f;

        var totalWidth = beforeSize.Width + dotGap + afterSize.Width;
        var startX = (width - totalWidth) / 2f;
        var y = (height - beforeSize.Height) / 2f;
        var outlineWidth = Math.Max(1f, height / 16f);

        void DrawAll(Brush brush, float ox, float oy)
        {
            var x = startX + ox;
            g.DrawString(beforeDot, font, brush, x, y + oy, TightFormat);
            x += beforeSize.Width;

            if (hasDot)
            {
                var dotY = y + oy + beforeSize.Height - dotDiameter * 1.3f;
                g.FillEllipse(brush, x + (dotGap - dotDiameter) / 2f, dotY, dotDiameter, dotDiameter);
                x += dotGap;
                g.DrawString(afterDot, font, brush, x, y + oy, TightFormat);
            }
        }

        using (var outlineBrush = new SolidBrush(Color.Black))
        {
            foreach (var (dx, dy) in OutlineOffsets)
                DrawAll(outlineBrush, dx * outlineWidth, dy * outlineWidth);
        }

        using var brush = new SolidBrush(color);
        DrawAll(brush, 0, 0);

        return bitmap;
    }

    private static Font GetFittingFont(Graphics g, string text, int width, int height)
    {
        var margin = Math.Max(1f, width / 16f);
        var maxWidth = width - margin;
        var maxHeight = height - margin;

        for (var size = height + 8f; size >= 6f; size -= 0.5f)
        {
            var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = g.MeasureString(text, font, PointF.Empty, TightFormat);
            if (measured.Width <= maxWidth && measured.Height <= maxHeight)
                return font;

            font.Dispose();
        }

        return new Font("Segoe UI", 6f, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
