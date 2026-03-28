using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CryptoScanner.Views;

/// <summary>
/// Draws a stylized "CS" crypto logo with chart bars and upward arrow.
/// Pure Avalonia drawing — no external image file needed.
/// </summary>
public class LogoControl : Control
{
    private static readonly Color Teal1 = Color.Parse("#00D4AA");
    private static readonly Color Teal2 = Color.Parse("#00F5C8");
    private static readonly Color DarkBg = Color.Parse("#0D1117");

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size < 10) return;

        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var r = size / 2;

        // Outer glow circle
        var glowBrush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#3300D4AA"), 0.5),
                new GradientStop(Color.Parse("#0000D4AA"), 1.0)
            }
        };
        ctx.DrawEllipse(glowBrush, null, new Point(cx, cy), r * 1.1, r * 1.1);

        // Main circle background
        var bgBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#161B22"), 0), new GradientStop(Color.Parse("#0D1117"), 1) }
        };
        var borderPen = new Pen(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Teal1, 0), new GradientStop(Color.Parse("#006B55"), 1) }
        }, r * 0.04);

        ctx.DrawEllipse(bgBrush, borderPen, new Point(cx, cy), r * 0.92, r * 0.92);

        // Chart bars (3 ascending bars in the center-left area)
        var barBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#006B55"), 0), new GradientStop(Teal2, 1) }
        };

        double barW = r * 0.13;
        double gap = r * 0.06;
        double baseY = cy + r * 0.28;
        double startX = cx - r * 0.38;

        // Bar 1 (short)
        double h1 = r * 0.25;
        ctx.DrawRectangle(barBrush, null, new Rect(startX, baseY - h1, barW, h1), 2, 2);

        // Bar 2 (medium)
        double h2 = r * 0.42;
        ctx.DrawRectangle(barBrush, null, new Rect(startX + barW + gap, baseY - h2, barW, h2), 2, 2);

        // Bar 3 (tall)
        double h3 = r * 0.60;
        ctx.DrawRectangle(barBrush, null, new Rect(startX + 2 * (barW + gap), baseY - h3, barW, h3), 2, 2);

        // Upward arrow (to the right of bars)
        var arrowPen = new Pen(new SolidColorBrush(Teal2), r * 0.05);
        double arrowX = startX + 3 * (barW + gap) + r * 0.08;
        double arrowTopY = cy - r * 0.38;
        double arrowBotY = baseY;

        // Arrow shaft
        ctx.DrawLine(arrowPen, new Point(arrowX, arrowBotY), new Point(arrowX, arrowTopY));
        // Arrow head
        ctx.DrawLine(arrowPen, new Point(arrowX - r * 0.10, arrowTopY + r * 0.12), new Point(arrowX, arrowTopY));
        ctx.DrawLine(arrowPen, new Point(arrowX + r * 0.10, arrowTopY + r * 0.12), new Point(arrowX, arrowTopY));

        // "CS" text (right side of logo)
        var textBrush = new SolidColorBrush(Color.Parse("#F0F6FC"));
        var ft = new FormattedText("CS", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            r * 0.45, textBrush);
        ctx.DrawText(ft, new Point(cx + r * 0.05, cy - ft.Height / 2 + r * 0.02));
    }
}
