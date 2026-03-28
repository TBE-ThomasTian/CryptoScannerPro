using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CryptoScanner.Models;

namespace CryptoScanner.Views;

public class PortfolioChartControl : Control
{
    public static readonly StyledProperty<List<PortfolioSnapshot>?> SnapshotsProperty =
        AvaloniaProperty.Register<PortfolioChartControl, List<PortfolioSnapshot>?>(nameof(Snapshots));

    public static readonly StyledProperty<decimal> InitialBalanceProperty =
        AvaloniaProperty.Register<PortfolioChartControl, decimal>(nameof(InitialBalance), 10000m);

    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<PortfolioChartControl, string>(nameof(CurrencySymbol), "$");

    public List<PortfolioSnapshot>? Snapshots { get => GetValue(SnapshotsProperty); set => SetValue(SnapshotsProperty, value); }
    public decimal InitialBalance { get => GetValue(InitialBalanceProperty); set => SetValue(InitialBalanceProperty, value); }
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    // Crosshair state
    private double _mouseX, _mouseY;
    private bool _isMouseOver;

    static readonly Color GainColor = Color.Parse("#00D4AA");
    static readonly Color LossColor = Color.Parse("#FF4757");
    static readonly Color GainFillTop = Color.Parse("#2200D4AA");
    static readonly Color GainFillBot = Color.Parse("#0000D4AA");
    static readonly Color LossFillTop = Color.Parse("#22FF4757");
    static readonly Color LossFillBot = Color.Parse("#00FF4757");
    static readonly Color GridColor = Color.Parse("#1C2333");
    static readonly Color LabelColor = Color.Parse("#8899A6");
    static readonly Color BaselineColor = Color.Parse("#30363D");

    static PortfolioChartControl()
    {
        AffectsRender<PortfolioChartControl>(SnapshotsProperty, InitialBalanceProperty, CurrencySymbolProperty);
        BoundsProperty.Changed.AddClassHandler<PortfolioChartControl>((c, _) => c.InvalidateVisual());
    }

    public PortfolioChartControl()
    {
        ClipToBounds = true;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isMouseOver = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isMouseOver = false;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        _mouseX = pos.X;
        _mouseY = pos.Y;
        if (_isMouseOver) InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var snapshots = Snapshots;
        if (snapshots == null || snapshots.Count < 2)
        {
            DrawEmptyState(ctx);
            return;
        }

        double w = Bounds.Width, h = Bounds.Height;
        if (w < 10 || h < 10) return;

        const double padL = 4, padR = 70, padT = 10, padB = 32;
        double cW = w - padL - padR, cH = h - padT - padB;
        if (cW < 10 || cH < 10) return;

        int n = snapshots.Count;
        var values = snapshots.Select(s => (double)s.TotalValue).ToList();
        double initBal = (double)InitialBalance;

        double minV = Math.Min(values.Min(), initBal) * 0.995;
        double maxV = Math.Max(values.Max(), initBal) * 1.005;
        double range = maxV - minV;
        if (range <= 0) return;

        double ToX(int i) => padL + (i / (double)(n - 1)) * cW;
        double ToY(double v) => padT + (1.0 - (v - minV) / range) * cH;

        // Background
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#0D1117")), null, new Rect(0, 0, w, h), 6, 6);

        // Grid lines + price labels
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        var lblBr = new SolidColorBrush(LabelColor);
        for (int i = 0; i <= 5; i++)
        {
            double v = minV + range * i / 5;
            double y = ToY(v);
            ctx.DrawLine(gridPen, new Point(padL, y), new Point(padL + cW, y));
            var ft = new FormattedText($"{CurrencySymbol}{v:N0}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, lblBr);
            ctx.DrawText(ft, new Point(padL + cW + 4, y - ft.Height / 2));
        }

        // Baseline (initial balance)
        double baseY = ToY(initBal);
        var basePen = new Pen(new SolidColorBrush(BaselineColor), 1, DashStyle.Dash);
        ctx.DrawLine(basePen, new Point(padL, baseY), new Point(padL + cW, baseY));
        var baseFt = new FormattedText($"Start {CurrencySymbol}{initBal:N0}", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(BaselineColor));
        ctx.DrawText(baseFt, new Point(padL + cW + 4, baseY - baseFt.Height - 2));

        // X-axis date labels
        int labelCount = Math.Clamp((int)(cW / 100), 3, 10);
        int step = Math.Max(1, n / labelCount);
        var tickPen = new Pen(new SolidColorBrush(LabelColor), 1);
        for (int li = 0; li < n; li += step)
        {
            var ts = snapshots[li].Timestamp;
            string label = n > 48 ? ts.ToString("dd.MM") : ts.ToString("dd.MM HH:mm");
            var dft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, lblBr);
            double lx = ToX(li);
            ctx.DrawLine(tickPen, new Point(lx, padT + cH), new Point(lx, padT + cH + 4));
            ctx.DrawText(dft, new Point(lx - dft.Width / 2, padT + cH + 5));
        }

        // Determine if overall gain or loss
        bool isGain = values[^1] >= initBal;
        var lineColor = isGain ? GainColor : LossColor;

        // Area fill (gradient from line to baseline)
        var areaGeo = new StreamGeometry();
        using (var sg = areaGeo.Open())
        {
            sg.BeginFigure(new Point(ToX(0), ToY(values[0])), true);
            for (int i = 1; i < n; i++)
                sg.LineTo(new Point(ToX(i), ToY(values[i])));
            sg.LineTo(new Point(ToX(n - 1), baseY));
            sg.LineTo(new Point(ToX(0), baseY));
            sg.EndFigure(true);
        }

        var fillTop = isGain ? GainFillTop : LossFillTop;
        var fillBot = isGain ? GainFillBot : LossFillBot;
        var areaFill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(fillTop, 0), new GradientStop(fillBot, 1) }
        };
        ctx.DrawGeometry(areaFill, null, areaGeo);

        // Line
        var linePen = new Pen(new SolidColorBrush(lineColor), 2);
        for (int i = 1; i < n; i++)
            ctx.DrawLine(linePen, new Point(ToX(i - 1), ToY(values[i - 1])), new Point(ToX(i), ToY(values[i])));

        // Current value dot
        double lastY = ToY(values[^1]);
        ctx.DrawEllipse(new SolidColorBrush(lineColor), null, new Point(ToX(n - 1), lastY), 4, 4);

        // Legend
        double pnl = values[^1] - initBal;
        double pnlPct = initBal > 0 ? (pnl / initBal) * 100 : 0;
        string legendText = $"Depotwert  {(pnl >= 0 ? "+" : "")}{CurrencySymbol}{pnl:N2} ({(pnlPct >= 0 ? "+" : "")}{pnlPct:N2}%)";
        var legendFt = new FormattedText(legendText, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(lineColor));
        ctx.DrawLine(new Pen(new SolidColorBrush(lineColor), 2), new Point(padL + 6, padT + 10), new Point(padL + 18, padT + 10));
        ctx.DrawText(legendFt, new Point(padL + 22, padT + 3));

        // Crosshair
        if (_isMouseOver && _mouseX >= padL && _mouseX <= padL + cW && _mouseY >= padT && _mouseY <= padT + cH)
        {
            var chPen = new Pen(new SolidColorBrush(Color.Parse("#80FFFFFF")), 1, DashStyle.Dash);
            ctx.DrawLine(chPen, new Point(_mouseX, padT), new Point(_mouseX, padT + cH));
            ctx.DrawLine(chPen, new Point(padL, _mouseY), new Point(padL + cW, _mouseY));

            // Value label on right edge
            double valAtY = minV + (1.0 - (_mouseY - padT) / cH) * range;
            var valLbl = $"{CurrencySymbol}{valAtY:N2}";
            var vft = new FormattedText(valLbl, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#F0F6FC")));
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#161B22")), null,
                new Rect(padL + cW + 1, _mouseY - vft.Height / 2 - 2, padR - 2, vft.Height + 4), 3, 3);
            ctx.DrawText(vft, new Point(padL + cW + 4, _mouseY - vft.Height / 2));

            // Date + value tooltip
            double idx = (_mouseX - padL) / cW * (n - 1);
            int ci = Math.Clamp((int)Math.Round(idx), 0, n - 1);
            var snap = snapshots[ci];
            double snapPnl = (double)snap.TotalValue - initBal;
            double snapPct = initBal > 0 ? (snapPnl / initBal) * 100 : 0;
            bool snapGain = snapPnl >= 0;

            var dateLbl = snap.Timestamp.ToString("dd.MM.yyyy HH:mm");
            var dft2 = new FormattedText(dateLbl, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(Color.Parse("#F0F6FC")));
            double dftX = Math.Clamp(_mouseX - dft2.Width / 2, padL, padL + cW - dft2.Width);
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#161B22")), null,
                new Rect(dftX - 3, padT + cH + 2, dft2.Width + 6, dft2.Height + 4), 3, 3);
            ctx.DrawText(dft2, new Point(dftX, padT + cH + 4));

            // Tooltip with value + P/L
            var tipColor = snapGain ? GainColor : LossColor;
            var tipText = $"{CurrencySymbol}{snap.TotalValue:N2}\n{(snapGain ? "+" : "")}{snapPct:N2}%";
            var tft = new FormattedText(tipText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(tipColor));
            double tipX = Math.Min(_mouseX + 14, padL + cW - tft.Width - 10);
            double tipY = Math.Max(_mouseY - tft.Height - 10, padT);
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#E0161B22")), null,
                new Rect(tipX - 4, tipY - 3, tft.Width + 8, tft.Height + 6), 4, 4);
            ctx.DrawText(tft, new Point(tipX, tipY));
        }
    }

    private void DrawEmptyState(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#0D1117")), null, new Rect(0, 0, w, h), 6, 6);
        var ft = new FormattedText("Noch keine Daten — Diagramm erscheint nach dem ersten Scan",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, new SolidColorBrush(Color.Parse("#484F58")));
        ctx.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
    }
}
