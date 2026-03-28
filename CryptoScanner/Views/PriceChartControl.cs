using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CryptoScanner.Models;

namespace CryptoScanner.Views;

public class PriceChartControl : Control
{
    // ── Data properties ─────────────────────────────────────────
    public static readonly StyledProperty<List<OhlcCandle>?> CandlesProperty =
        AvaloniaProperty.Register<PriceChartControl, List<OhlcCandle>?>(nameof(Candles));
    public static readonly StyledProperty<decimal> Sma20Property =
        AvaloniaProperty.Register<PriceChartControl, decimal>(nameof(Sma20));
    public static readonly StyledProperty<decimal> Sma50Property =
        AvaloniaProperty.Register<PriceChartControl, decimal>(nameof(Sma50));

    // ── Currency symbol for labels ─────────────────────────────
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<PriceChartControl, string>(nameof(CurrencySymbol), "$");
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    // ── Toggles ─────────────────────────────────────────────────
    public static readonly StyledProperty<bool> ShowCandlesticksProperty =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowCandlesticks), false);
    public static readonly StyledProperty<bool> ShowSma20Property =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowSma20), true);
    public static readonly StyledProperty<bool> ShowSma50Property =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowSma50), true);
    public static readonly StyledProperty<bool> ShowEma12Property =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowEma12), false);
    public static readonly StyledProperty<bool> ShowEma26Property =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowEma26), false);
    public static readonly StyledProperty<bool> ShowBollingerBandsProperty =
        AvaloniaProperty.Register<PriceChartControl, bool>(nameof(ShowBollingerBands), false);

    public List<OhlcCandle>? Candles { get => GetValue(CandlesProperty); set => SetValue(CandlesProperty, value); }
    public decimal Sma20 { get => GetValue(Sma20Property); set => SetValue(Sma20Property, value); }
    public decimal Sma50 { get => GetValue(Sma50Property); set => SetValue(Sma50Property, value); }
    public bool ShowCandlesticks { get => GetValue(ShowCandlesticksProperty); set => SetValue(ShowCandlesticksProperty, value); }
    public bool ShowSma20 { get => GetValue(ShowSma20Property); set => SetValue(ShowSma20Property, value); }
    public bool ShowSma50 { get => GetValue(ShowSma50Property); set => SetValue(ShowSma50Property, value); }
    public bool ShowEma12 { get => GetValue(ShowEma12Property); set => SetValue(ShowEma12Property, value); }
    public bool ShowEma26 { get => GetValue(ShowEma26Property); set => SetValue(ShowEma26Property, value); }
    public bool ShowBollingerBands { get => GetValue(ShowBollingerBandsProperty); set => SetValue(ShowBollingerBandsProperty, value); }

    // Zoom/Pan state
    private double _chartZoom = 1.0;
    private int _chartOffset;
    private bool _isDragging;
    private double _dragStartX;
    private int _dragStartOffset;

    // Crosshair state
    private double _mouseX, _mouseY;
    private bool _isMouseOver;

    static readonly Color BullColor = Color.Parse("#00D4AA");
    static readonly Color BearColor = Color.Parse("#FF4757");
    static readonly Color Sma20Color = Color.Parse("#3B82F6");
    static readonly Color Sma50Color = Color.Parse("#F59E0B");
    static readonly Color Ema12Color = Color.Parse("#06B6D4");
    static readonly Color Ema26Color = Color.Parse("#D946EF");
    static readonly Color BollingerColor = Color.Parse("#A78BFA");
    static readonly Color BollingerFill = Color.Parse("#18A78BFA");
    static readonly Color GridColor = Color.Parse("#1C2333");
    static readonly Color LabelColor = Color.Parse("#8899A6");
    static readonly Color AreaFillTop = Color.Parse("#1A00D4AA");
    static readonly Color AreaFillBot = Color.Parse("#0000D4AA");

    static PriceChartControl()
    {
        AffectsRender<PriceChartControl>(CandlesProperty, Sma20Property, Sma50Property, CurrencySymbolProperty,
            ShowCandlesticksProperty, ShowSma20Property, ShowSma50Property,
            ShowEma12Property, ShowEma26Property, ShowBollingerBandsProperty);
        BoundsProperty.Changed.AddClassHandler<PriceChartControl>((c, _) => c.InvalidateVisual());
    }

    public PriceChartControl()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
    }

    // ── Mouse: zoom (scroll) + pan (drag) + reset (dblclick) ────

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var candles = Candles;
        if (candles == null || candles.Count < 2) return;

        double delta = e.Delta.Y > 0 ? 1.08 : 0.93;
        double oldZoom = _chartZoom;
        _chartZoom = Math.Clamp(_chartZoom * delta, 1.0, Math.Max(1.0, candles.Count / 10.0));

        // Adjust offset to keep the view centered around current position
        int visible = VisibleCount(candles.Count);
        _chartOffset = Math.Clamp(_chartOffset, 0, Math.Max(0, candles.Count - visible));

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            if (e.ClickCount >= 2)
            {
                _chartZoom = 1.0; _chartOffset = 0;
                InvalidateVisual(); e.Handled = true; return;
            }
            _isDragging = true;
            _dragStartX = e.GetPosition(this).X;
            _dragStartOffset = _chartOffset;
            e.Handled = true;
        }
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
        _mouseX = pos.X; _mouseY = pos.Y;

        if (_isMouseOver && !_isDragging) { InvalidateVisual(); return; }
        if (!_isDragging) return;
        var candles = Candles;
        if (candles == null || candles.Count < 2) return;

        double dx = e.GetPosition(this).X - _dragStartX;
        int visible = VisibleCount(candles.Count);
        double pixelsPerCandle = (Bounds.Width - 60) / Math.Max(1, visible);
        int candleShift = (int)(-dx / Math.Max(1, pixelsPerCandle));

        _chartOffset = Math.Clamp(_dragStartOffset + candleShift, 0, Math.Max(0, candles.Count - visible));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDragging = false;
    }

    private int VisibleCount(int total) => Math.Max(10, (int)(total / _chartZoom));

    // ── Rendering ───────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        var allCandles = Candles;
        if (allCandles == null || allCandles.Count < 2) return;

        double w = Bounds.Width, h = Bounds.Height;
        if (w < 10 || h < 10) return;

        const double padL = 4, padR = 60, padT = 10, padB = 32;
        var cW = w - padL - padR; var cH = h - padT - padB;
        if (cW < 10 || cH < 10) return;

        // Determine visible slice
        int total = allCandles.Count;
        int visible = VisibleCount(total);
        int startIdx = Math.Clamp(_chartOffset, 0, Math.Max(0, total - visible));
        int endIdx = Math.Min(startIdx + visible, total);
        var candles = allCandles.GetRange(startIdx, endIdx - startIdx);
        int n = candles.Count;
        if (n < 2) return;

        var closes = candles.Select(c => (double)c.Close).ToList();
        var opens = candles.Select(c => (double)c.Open).ToList();
        var highs = candles.Select(c => (double)c.High).ToList();
        var lows = candles.Select(c => (double)c.Low).ToList();

        var minP = lows.Min() * 0.998; var maxP = highs.Max() * 1.002;
        var range = maxP - minP;
        if (range <= 0) return;

        double ToX(int i) => padL + (i / (double)(n - 1)) * cW;
        double ToY(double p) => padT + (1.0 - (p - minP) / range) * cH;

        // Background
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#0D1117")), null, new Rect(0, 0, w, h), 6, 6);

        // Grid
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        var lblBr = new SolidColorBrush(LabelColor);
        for (int i = 0; i <= 5; i++)
        {
            var p = minP + range * i / 5; var y = ToY(p);
            ctx.DrawLine(gridPen, new Point(padL, y), new Point(padL + cW, y));
            var ft = new FormattedText(FmtPrice(p, CurrencySymbol), System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, lblBr);
            ctx.DrawText(ft, new Point(padL + cW + 4, y - ft.Height / 2));
        }

        // Y-axis title
        {
            var yTitle = $"Preis ({CurrencySymbol})";
            var ytf = new FormattedText(yTitle, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 8, new SolidColorBrush(Color.Parse("#50506A")));
            ctx.DrawText(ytf, new Point(padL + cW + 4, padT - 2));
        }

        // ── X-axis date/time labels ──────────────────────────────
        {
            var xGridPen = new Pen(new SolidColorBrush(Color.Parse("#15FFFFFF")), 1);
            var tickPen = new Pen(new SolidColorBrush(LabelColor), 1);
            // Determine format: show time for short views, date only for long
            bool showTime = n < 100;
            string dateFmt = showTime ? "dd.MM HH:mm" : "dd.MM";
            int labelCount = Math.Clamp((int)(cW / 90), 3, 10);
            int step = Math.Max(1, n / labelCount);

            for (int li = 0; li < n; li += step)
            {
                var candle = candles[li];
                var dt = DateTimeOffset.FromUnixTimeSeconds(candle.Timestamp).ToLocalTime();
                var label = dt.ToString(dateFmt);
                var dft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 9, lblBr);
                double lx = ToX(li);
                // Tick mark
                ctx.DrawLine(tickPen, new Point(lx, padT + cH), new Point(lx, padT + cH + 4));
                // Vertical grid line (very subtle)
                ctx.DrawLine(xGridPen, new Point(lx, padT), new Point(lx, padT + cH));
                // Label
                ctx.DrawText(dft, new Point(lx - dft.Width / 2, padT + cH + 5));
            }
        }

        // Use full data for SMA/EMA/BB computation, then slice the visible portion
        var allCloses = allCandles.Select(c => (double)c.Close).ToList();

        // Bollinger
        if (ShowBollingerBands && allCloses.Count >= 20)
        {
            var sma = SMA(allCloses, 20);
            var (up, lo) = BB(allCloses, sma, 20, 2.0);
            DrawSlicedBand(ctx, up, lo, allCloses.Count, 20, startIdx, n, ToX, ToY);
        }

        if (ShowCandlesticks)
        {
            double candleStep = cW / n;
            double bodyW = Math.Max(1, candleStep * 0.65);
            for (int i = 0; i < n; i++)
            {
                double x = ToX(i);
                bool bull = closes[i] >= opens[i];
                var brush = new SolidColorBrush(bull ? BullColor : BearColor);
                var wickPen = new Pen(brush, Math.Max(1, bodyW * 0.15));
                ctx.DrawLine(wickPen, new Point(x, ToY(highs[i])), new Point(x, ToY(lows[i])));
                double bt = ToY(Math.Max(opens[i], closes[i]));
                double bb = ToY(Math.Min(opens[i], closes[i]));
                ctx.DrawRectangle(brush, null, new Rect(x - bodyW / 2, bt, bodyW, Math.Max(1, bb - bt)));
            }
        }
        else
        {
            var ag = new StreamGeometry();
            using (var sg = ag.Open())
            {
                sg.BeginFigure(new Point(ToX(0), ToY(closes[0])), true);
                for (int i = 1; i < n; i++) sg.LineTo(new Point(ToX(i), ToY(closes[i])));
                sg.LineTo(new Point(ToX(n - 1), padT + cH));
                sg.LineTo(new Point(ToX(0), padT + cH));
                sg.EndFigure(true);
            }
            var af = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(AreaFillTop, 0), new GradientStop(AreaFillBot, 1) }
            };
            ctx.DrawGeometry(af, null, ag);
            DrawLine(ctx, closes, ToX, ToY, new Pen(new SolidColorBrush(BullColor), 2));
        }

        // Overlays (computed from full data, drawn for visible slice)
        if (ShowSma20 && allCloses.Count >= 20)
            DrawSlicedSeries(ctx, SMA(allCloses, 20), allCloses.Count, 20, startIdx, n, ToX, ToY,
                new Pen(new SolidColorBrush(Sma20Color), 1.5, DashStyle.Dash));
        if (ShowSma50 && allCloses.Count >= 50)
            DrawSlicedSeries(ctx, SMA(allCloses, 50), allCloses.Count, 50, startIdx, n, ToX, ToY,
                new Pen(new SolidColorBrush(Sma50Color), 1.5, DashStyle.Dash));
        if (ShowEma12 && allCloses.Count >= 12)
            DrawSlicedSeries(ctx, EMA(allCloses, 12), allCloses.Count, 12, startIdx, n, ToX, ToY,
                new Pen(new SolidColorBrush(Ema12Color), 1.5));
        if (ShowEma26 && allCloses.Count >= 26)
            DrawSlicedSeries(ctx, EMA(allCloses, 26), allCloses.Count, 26, startIdx, n, ToX, ToY,
                new Pen(new SolidColorBrush(Ema26Color), 1.5));

        // Current price dot
        var ly = ToY(closes[^1]);
        ctx.DrawEllipse(new SolidColorBrush(BullColor), null, new Point(ToX(n - 1), ly), 4, 4);
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#3300D4AA")), 1, DashStyle.Dot),
            new Point(padL, ly), new Point(padL + cW, ly));

        // Legend
        DrawLegend(ctx, padL + 6, padT + 4);

        // Zoom indicator (bottom-left)
        if (_chartZoom > 1.01)
        {
            var zi = new FormattedText($"Zoom {_chartZoom:N1}x  |  {n}/{total} Kerzen",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(Color.Parse("#50506A")));
            ctx.DrawText(zi, new Point(padL + 4, h - zi.Height - 2));
        }

        // ── Crosshair / Fadenkreuz ──────────────────────────────
        if (_isMouseOver && _mouseX >= padL && _mouseX <= padL + cW && _mouseY >= padT && _mouseY <= padT + cH)
        {
            var chPen = new Pen(new SolidColorBrush(Color.Parse("#80FFFFFF")), 1, DashStyle.Dash);

            // Vertical line
            ctx.DrawLine(chPen, new Point(_mouseX, padT), new Point(_mouseX, padT + cH));
            // Horizontal line
            ctx.DrawLine(chPen, new Point(padL, _mouseY), new Point(padL + cW, _mouseY));

            // Price label on right edge
            double priceAtY = minP + (1.0 - (_mouseY - padT) / cH) * range;
            var priceLbl = FmtPrice(priceAtY, CurrencySymbol);
            var pft = new FormattedText(priceLbl, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10, new SolidColorBrush(Color.Parse("#F0F6FC")));
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#161B22")), null,
                new Rect(padL + cW + 1, _mouseY - pft.Height / 2 - 2, padR - 2, pft.Height + 4), 3, 3);
            ctx.DrawText(pft, new Point(padL + cW + 4, _mouseY - pft.Height / 2));

            // Date label on bottom edge — find nearest candle
            double candleIdx = (_mouseX - padL) / cW * (n - 1);
            int ci = Math.Clamp((int)Math.Round(candleIdx), 0, n - 1);
            var candle = candles[ci];
            var dtOffset = DateTimeOffset.FromUnixTimeSeconds(candle.Timestamp);
            var dateLbl = dtOffset.ToLocalTime().ToString("dd.MM HH:mm");
            var dft = new FormattedText(dateLbl, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(Color.Parse("#F0F6FC")));
            double dftX = Math.Clamp(_mouseX - dft.Width / 2, padL, padL + cW - dft.Width);
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#161B22")), null,
                new Rect(dftX - 3, padT + cH + 2, dft.Width + 6, dft.Height + 4), 3, 3);
            ctx.DrawText(dft, new Point(dftX, padT + cH + 4));

            // OHLC tooltip for hovered candle
            bool isBull = candle.Close >= candle.Open;
            var ohlcColor = isBull ? BullColor : BearColor;
            var cs = CurrencySymbol;
            var ohlcText = $"O:{FmtPrice((double)candle.Open, cs)} H:{FmtPrice((double)candle.High, cs)}\nL:{FmtPrice((double)candle.Low, cs)} C:{FmtPrice((double)candle.Close, cs)}";
            var oft = new FormattedText(ohlcText, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(ohlcColor));
            double tooltipX = Math.Min(_mouseX + 14, padL + cW - oft.Width - 10);
            double tooltipY = Math.Max(_mouseY - oft.Height - 10, padT);
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#E0161B22")), null,
                new Rect(tooltipX - 4, tooltipY - 3, oft.Width + 8, oft.Height + 6), 4, 4);
            ctx.DrawText(oft, new Point(tooltipX, tooltipY));
        }
    }

    // ── Sliced overlay drawing ──────────────────────────────────

    private static void DrawSlicedSeries(DrawingContext ctx, List<double> fullSeries, int totalCandles, int period,
        int startIdx, int visibleCount, Func<int, double> toX, Func<double, double> toY, Pen pen)
    {
        int seriesOffset = totalCandles - fullSeries.Count; // index in candle space where series starts
        for (int vi = 1; vi < visibleCount; vi++)
        {
            int ci = startIdx + vi;     // candle index
            int si = ci - seriesOffset; // series index
            int siPrev = si - 1;
            if (siPrev < 0 || si >= fullSeries.Count) continue;
            ctx.DrawLine(pen, new Point(toX(vi - 1), toY(fullSeries[siPrev])), new Point(toX(vi), toY(fullSeries[si])));
        }
    }

    private static void DrawSlicedBand(DrawingContext ctx, List<double> upper, List<double> lower,
        int totalCandles, int period, int startIdx, int visibleCount,
        Func<int, double> toX, Func<double, double> toY)
    {
        int seriesOffset = totalCandles - upper.Count;
        var upPts = new List<(int vi, double val)>();
        var loPts = new List<(int vi, double val)>();

        for (int vi = 0; vi < visibleCount; vi++)
        {
            int si = startIdx + vi - seriesOffset;
            if (si < 0 || si >= upper.Count) continue;
            upPts.Add((vi, upper[si]));
            loPts.Add((vi, lower[si]));
        }

        if (upPts.Count < 2) return;

        var fg = new StreamGeometry();
        using (var sg = fg.Open())
        {
            sg.BeginFigure(new Point(toX(upPts[0].vi), toY(upPts[0].val)), true);
            for (int i = 1; i < upPts.Count; i++) sg.LineTo(new Point(toX(upPts[i].vi), toY(upPts[i].val)));
            for (int i = loPts.Count - 1; i >= 0; i--) sg.LineTo(new Point(toX(loPts[i].vi), toY(loPts[i].val)));
            sg.EndFigure(true);
        }
        ctx.DrawGeometry(new SolidColorBrush(BollingerFill), null, fg);

        var bbPen = new Pen(new SolidColorBrush(BollingerColor), 1, DashStyle.Dash);
        for (int i = 1; i < upPts.Count; i++)
        {
            ctx.DrawLine(bbPen, new Point(toX(upPts[i - 1].vi), toY(upPts[i - 1].val)),
                                new Point(toX(upPts[i].vi), toY(upPts[i].val)));
            ctx.DrawLine(bbPen, new Point(toX(loPts[i - 1].vi), toY(loPts[i - 1].val)),
                                new Point(toX(loPts[i].vi), toY(loPts[i].val)));
        }
    }

    // ── Legend + line helpers ────────────────────────────────────

    private void DrawLegend(DrawingContext ctx, double x, double y)
    {
        var items = new List<(string l, Color c)>();
        items.Add(ShowCandlesticks ? ("Kerzen", BullColor) : ("Preis", BullColor));
        if (ShowSma20) items.Add(("SMA20", Sma20Color));
        if (ShowSma50) items.Add(("SMA50", Sma50Color));
        if (ShowEma12) items.Add(("EMA12", Ema12Color));
        if (ShowEma26) items.Add(("EMA26", Ema26Color));
        if (ShowBollingerBands) items.Add(("BB", BollingerColor));

        double ox = x;
        foreach (var (l, c) in items)
        {
            ctx.DrawLine(new Pen(new SolidColorBrush(c), 2), new Point(ox, y + 6), new Point(ox + 12, y + 6));
            var ft = new FormattedText(l, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 8, new SolidColorBrush(LabelColor));
            ctx.DrawText(ft, new Point(ox + 15, y));
            ox += 15 + ft.Width + 8;
        }
    }

    static void DrawLine(DrawingContext ctx, List<double> v, Func<int, double> tx, Func<double, double> ty, Pen p)
    {
        for (int i = 1; i < v.Count; i++)
            ctx.DrawLine(p, new Point(tx(i - 1), ty(v[i - 1])), new Point(tx(i), ty(v[i])));
    }

    // ── Computation helpers ─────────────────────────────────────

    static List<double> SMA(List<double> p, int per)
    {
        var r = new List<double>(); if (p.Count < per) return r;
        double s = 0; for (int i = 0; i < per; i++) s += p[i]; r.Add(s / per);
        for (int i = per; i < p.Count; i++) { s += p[i] - p[i - per]; r.Add(s / per); }
        return r;
    }

    static List<double> EMA(List<double> p, int per)
    {
        var r = new List<double>(); if (p.Count < per) return r;
        double m = 2.0 / (per + 1), e = 0;
        for (int i = 0; i < per; i++) e += p[i]; e /= per; r.Add(e);
        for (int i = per; i < p.Count; i++) { e = (p[i] - e) * m + e; r.Add(e); }
        return r;
    }

    static (List<double> U, List<double> L) BB(List<double> prices, List<double> sma, int per, double nsd)
    {
        var u = new List<double>(); var l = new List<double>();
        int off = prices.Count - sma.Count;
        for (int i = 0; i < sma.Count; i++)
        {
            double sum = 0; int st = Math.Max(0, off + i - per + 1); int cnt = 0;
            for (int j = st; j <= off + i && j < prices.Count; j++) { var d = prices[j] - sma[i]; sum += d * d; cnt++; }
            var sd = cnt > 0 ? Math.Sqrt(sum / cnt) : 0;
            u.Add(sma[i] + nsd * sd); l.Add(sma[i] - nsd * sd);
        }
        return (u, l);
    }

    static string FmtPrice(double p, string sym = "") => sym + p switch
    { >= 10000 => p.ToString("N0"), >= 1000 => p.ToString("N1"), >= 1 => p.ToString("N2"), >= 0.01 => p.ToString("N4"), _ => p.ToString("N6") };
}
