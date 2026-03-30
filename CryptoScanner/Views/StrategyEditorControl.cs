using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CryptoScanner.Models;

namespace CryptoScanner.Views;

public class StrategyEditorControl : Control
{
    public event Action<StrategyBlock>? BlockDoubleClicked;

    public static readonly StyledProperty<TradingStrategy?> StrategyProperty =
        AvaloniaProperty.Register<StrategyEditorControl, TradingStrategy?>(nameof(Strategy));

    public static readonly StyledProperty<Guid?> SelectedBlockIdProperty =
        AvaloniaProperty.Register<StrategyEditorControl, Guid?>(nameof(SelectedBlockId));

    public TradingStrategy? Strategy
    {
        get => GetValue(StrategyProperty);
        set => SetValue(StrategyProperty, value);
    }

    public Guid? SelectedBlockId
    {
        get => GetValue(SelectedBlockIdProperty);
        set => SetValue(SelectedBlockIdProperty, value);
    }

    // Interaction state
    private StrategyBlock? _dragBlock;
    private Point _dragOffset;
    private StrategyBlock? _connectFrom;
    private string _connectPort = "";
    private Point _connectMouse;
    private bool _isConnecting;
    private StrategyBlock? _hoverBlock;
    private (StrategyBlock block, string port)? _hoverPin;

    // Pan & Zoom
    private double _zoom = 1.0;
    private double _panX, _panY;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartX, _panStartY;

    const double BlockW = 190, BlockH = 74, PinR = 8, PinRHover = 11, TitleH = 26;
    const double ZoomMin = 0.25, ZoomMax = 3.0;
    const double GridSnap = 20;

    static readonly Color BgColor = Color.Parse("#0F0F23");
    static readonly Color GridThin = Color.Parse("#1E1E3A");
    static readonly Color GridThick = Color.Parse("#252545");
    static readonly Color NodeBody = Color.Parse("#2D2D44");
    static readonly Color NodeBodyHover = Color.Parse("#363658");
    static readonly Color NodeSelected = Color.Parse("#404068");
    static readonly Color SelectGlow = Color.Parse("#00D4AA");
    static readonly Color TitleStart = Color.Parse("#8E8EA0");
    static readonly Color TitleCondition = Color.Parse("#E67E22");
    static readonly Color TitleBuy = Color.Parse("#27AE60");
    static readonly Color TitleSell = Color.Parse("#E74C3C");
    static readonly Color TitleHold = Color.Parse("#F1C40F");
    static readonly Color TitleAlarm = Color.Parse("#9B59B6");
    static readonly Color TextGray = Color.Parse("#9E9EBA");
    static readonly Color PinDefault = Color.Parse("#8E8EA0");
    static readonly Color PinBorder = Color.Parse("#50506A");
    static readonly Color WireJa = Color.Parse("#2ECC71");
    static readonly Color WireNein = Color.Parse("#E74C3C");
    static readonly Color WireOut = Color.Parse("#8E8EA0");
    static readonly Color WireGlowJa = Color.Parse("#302ECC71");
    static readonly Color WireGlowNein = Color.Parse("#30E74C3C");

    static StrategyEditorControl()
    {
        AffectsRender<StrategyEditorControl>(StrategyProperty, SelectedBlockIdProperty);
        BoundsProperty.Changed.AddClassHandler<StrategyEditorControl>((c, _) => c.InvalidateVisual());
    }

    public StrategyEditorControl()
    {
        Focusable = true;
        ClipToBounds = true;
        // Hit-testing works because Render draws a filled rectangle covering the entire bounds.
        // No Background property needed — Control doesn't have one, and Panel.Render is sealed.
    }

    // ── Public API ──────────────────────────────────────────────

    public void DeleteBlock(Guid id)
    {
        var strat = Strategy; if (strat == null) return;
        var block = strat.Blocks.FirstOrDefault(b => b.Id == id);
        if (block == null || block.Type == BlockType.Start) return;
        strat.Connections.RemoveAll(c => c.FromBlockId == id || c.ToBlockId == id);
        strat.Blocks.Remove(block);
        if (SelectedBlockId == id) SelectedBlockId = null;
        InvalidateVisual();
    }

    public void FitToView()
    {
        var strat = Strategy;
        if (strat == null || strat.Blocks.Count == 0) { _zoom = 1; _panX = 0; _panY = 0; InvalidateVisual(); return; }
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var b in strat.Blocks)
        {
            if (b.X < minX) minX = b.X; if (b.Y < minY) minY = b.Y;
            if (b.X + BlockW > maxX) maxX = b.X + BlockW; if (b.Y + BlockH > maxY) maxY = b.Y + BlockH;
        }
        double cW = maxX - minX, cH = maxY - minY;
        if (cW < 1 || cH < 1) { _zoom = 1; _panX = 0; _panY = 0; InvalidateVisual(); return; }
        double vW = Bounds.Width, vH = Bounds.Height;
        if (vW < 10 || vH < 10) return;
        _zoom = Math.Clamp(Math.Min((vW - 200) / cW, (vH - 200) / cH), ZoomMin, 0.85);
        _panX = vW / 2 - (minX + maxX) / 2 * _zoom;
        _panY = vH / 2 - (minY + maxY) / 2 * _zoom;
        InvalidateVisual();
    }

    // ── Coordinate transforms ───────────────────────────────────

    private Point ScreenToCanvas(Point screen) =>
        new((screen.X - _panX) / _zoom, (screen.Y - _panY) / _zoom);

    static double SnapToGrid(double value) => Math.Round(value / GridSnap) * GridSnap;

    static Point InPin(StrategyBlock b) => new(b.X - 2, b.Y + BlockH / 2);
    static Point OutPin(StrategyBlock b, string port)
    {
        if (b.IsCondition)
        {
            double yJa = b.Y + TitleH + (BlockH - TitleH) * 0.30;
            double yNein = b.Y + TitleH + (BlockH - TitleH) * 0.78;
            return port == "Ja" ? new(b.X + BlockW + 2, yJa) : new(b.X + BlockW + 2, yNein);
        }
        return new(b.X + BlockW + 2, b.Y + BlockH / 2);
    }
    static Color TitleColor(StrategyBlock b) => b.Type switch
    {
        BlockType.Start => TitleStart, BlockType.Condition => TitleCondition,
        BlockType.ActionBuy => TitleBuy, BlockType.ActionSell => TitleSell,
        BlockType.ActionHold => TitleHold, BlockType.ActionAlarm => TitleAlarm, _ => PinDefault
    };

    // ── Keyboard ────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.Key == Key.Delete || e.Key == Key.Back) && SelectedBlockId.HasValue)
        {
            DeleteBlock(SelectedBlockId.Value);
            e.Handled = true;
        }
    }

    // ── Mouse: Zoom ─────────────────────────────────────────────

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var sp = e.GetPosition(this);
        var cb = ScreenToCanvas(sp);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.06 : 0.94), ZoomMin, ZoomMax);
        _panX = sp.X - cb.X * _zoom;
        _panY = sp.Y - cb.Y * _zoom;
        InvalidateVisual();
        e.Handled = true;
    }

    // ── Mouse: Press ────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();  // Take focus for keyboard events
        var strat = Strategy; if (strat == null) return;
        var sp = e.GetPosition(this);
        var pos = ScreenToCanvas(sp);
        var props = e.GetCurrentPoint(this).Properties;

        // Middle button or Ctrl+Left = pan
        if (props.IsMiddleButtonPressed ||
            (props.IsLeftButtonPressed && (e.KeyModifiers & KeyModifiers.Control) != 0))
        {
            _isPanning = true; _panStart = sp; _panStartX = _panX; _panStartY = _panY;
            e.Handled = true; return;
        }

        // Right click = delete
        if (props.IsRightButtonPressed)
        {
            var hitB = HitBlock(strat, pos);
            if (hitB != null && hitB.Type != BlockType.Start)
            {
                strat.Connections.RemoveAll(c => c.FromBlockId == hitB.Id || c.ToBlockId == hitB.Id);
                strat.Blocks.Remove(hitB);
                if (SelectedBlockId == hitB.Id) SelectedBlockId = null;
                InvalidateVisual(); e.Handled = true; return;
            }
            var hitC = HitConnection(strat, pos);
            if (hitC != null) { strat.Connections.Remove(hitC); InvalidateVisual(); e.Handled = true; return; }
        }

        // Left click
        if (props.IsLeftButtonPressed)
        {
            // Check delete button on hovered block
            if (_hoverBlock != null && _hoverBlock.Type != BlockType.Start)
            {
                var delCenter = new Point(_hoverBlock.X + BlockW - 14, _hoverBlock.Y + 6);
                if (Dist(pos, delCenter) < 10)
                {
                    DeleteBlock(_hoverBlock.Id);
                    _hoverBlock = null;
                    e.Handled = true; return;
                }
            }

            // Check output pins for connection creation
            foreach (var b in strat.Blocks)
            {
                var ports = b.IsCondition ? new[] { "Ja", "Nein" } : new[] { "Out" };
                foreach (var port in ports)
                {
                    if (Dist(pos, OutPin(b, port)) < PinR + 6)
                    {
                        _isConnecting = true; _connectFrom = b; _connectPort = port; _connectMouse = pos;
                        e.Handled = true; return;
                    }
                }
            }

            // Check block hit for drag or selection
            var block = HitBlock(strat, pos);
            if (block != null)
            {
                SelectedBlockId = block.Id;
                if (e.ClickCount >= 2)
                {
                    BlockDoubleClicked?.Invoke(block);
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                _dragBlock = block;
                _dragOffset = new Point(pos.X - block.X, pos.Y - block.Y);
                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                SelectedBlockId = null;
                if (e.ClickCount >= 2) { FitToView(); e.Handled = true; }
                else InvalidateVisual();
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var sp = e.GetPosition(this);

        if (_isPanning)
        {
            _panX = _panStartX + (sp.X - _panStart.X);
            _panY = _panStartY + (sp.Y - _panStart.Y);
            InvalidateVisual(); return;
        }

        var pos = ScreenToCanvas(sp);

        if (_dragBlock != null)
        {
            _dragBlock.X = SnapToGrid(pos.X - _dragOffset.X);
            _dragBlock.Y = SnapToGrid(pos.Y - _dragOffset.Y);
            InvalidateVisual();
        }
        else if (_isConnecting)
        {
            _connectMouse = pos;
            InvalidateVisual();
        }
        else
        {
            var strat = Strategy;
            if (strat != null)
            {
                var prev = _hoverBlock;
                _hoverBlock = HitBlock(strat, pos);
                _hoverPin = null;
                foreach (var b in strat.Blocks)
                {
                    if (b.HasInput && Dist(pos, InPin(b)) < PinR + 5) _hoverPin = (b, "In");
                    var ports = b.IsCondition ? new[] { "Ja", "Nein" } : new[] { "Out" };
                    foreach (var p in ports)
                        if (Dist(pos, OutPin(b, p)) < PinR + 5) _hoverPin = (b, p);
                }
                if (_hoverBlock != prev || _hoverPin != null) InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning) { _isPanning = false; return; }

        var strat = Strategy;
        if (_isConnecting && _connectFrom != null && strat != null)
        {
            var pos = ScreenToCanvas(e.GetPosition(this));
            foreach (var b in strat.Blocks)
            {
                if (!b.HasInput || b.Id == _connectFrom.Id) continue;
                if (Dist(pos, InPin(b)) < PinR + 10)
                {
                    strat.Connections.RemoveAll(c => c.FromBlockId == _connectFrom.Id && c.OutputPort == _connectPort);
                    strat.Connections.Add(new StrategyConnection
                    { FromBlockId = _connectFrom.Id, ToBlockId = b.Id, OutputPort = _connectPort });
                    break;
                }
            }
        }
        if (_dragBlock != null)
        {
            _dragBlock.X = SnapToGrid(_dragBlock.X);
            _dragBlock.Y = SnapToGrid(_dragBlock.Y);
        }
        _dragBlock = null; _isConnecting = false; _connectFrom = null;
        InvalidateVisual();
    }

    // ── Rendering ───────────────────────────────────────────────

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        double w = Bounds.Width, h = Bounds.Height;

        // Background
        ctx.DrawRectangle(new SolidColorBrush(BgColor), null, new Rect(0, 0, w, h));

        // Grid (screen space)
        var thinPen = new Pen(new SolidColorBrush(GridThin), 0.5);
        var thickPen = new Pen(new SolidColorBrush(GridThick), 1);
        double gs = 20 * _zoom, gsb = 100 * _zoom;
        double sx = _panX % gs, sy = _panY % gs;
        for (double x = sx; x < w; x += gs)
            ctx.DrawLine(Math.Abs((x - _panX) % gsb) < 1 ? thickPen : thinPen, new Point(x, 0), new Point(x, h));
        for (double y = sy; y < h; y += gs)
            ctx.DrawLine(Math.Abs((y - _panY) % gsb) < 1 ? thickPen : thinPen, new Point(0, y), new Point(w, y));

        var strat = Strategy;
        if (strat == null || strat.Blocks.Count == 0)
        {
            var hint = MkText("Bloecke mit der Toolbar hinzufuegen. Rechtsklick = Loeschen.", 14, Color.Parse("#30305A"));
            ctx.DrawText(hint, new Point(w / 2 - hint.Width / 2, h / 2));
            return;
        }

        // Push canvas transform
        using var _t = ctx.PushTransform(Matrix.CreateTranslation(_panX, _panY) * Matrix.CreateScale(_zoom, _zoom));

        // Wires
        foreach (var conn in strat.Connections)
        {
            var from = strat.Blocks.FirstOrDefault(b => b.Id == conn.FromBlockId);
            var to = strat.Blocks.FirstOrDefault(b => b.Id == conn.ToBlockId);
            if (from == null || to == null) continue;
            DrawWire(ctx, OutPin(from, conn.OutputPort), InPin(to), conn.OutputPort);
        }
        if (_isConnecting && _connectFrom != null)
            DrawWire(ctx, OutPin(_connectFrom, _connectPort), _connectMouse, _connectPort);

        // Blocks
        foreach (var block in strat.Blocks)
            DrawNode(ctx, block);

        // Pop transform (using scope)

        // HUD (screen space)
        var zf = MkText($"{_zoom * 100:N0}%", 11, Color.Parse("#50506A"));
        ctx.DrawText(zf, new Point(8, h - zf.Height - 6));
    }

    private void DrawNode(DrawingContext ctx, StrategyBlock b)
    {
        bool isHover = _hoverBlock == b;
        bool isSel = SelectedBlockId == b.Id;
        var titleC = TitleColor(b);
        var bodyC = isSel ? NodeSelected : isHover ? NodeBodyHover : NodeBody;

        // Selection glow
        if (isSel)
        {
            var selPen = new Pen(new SolidColorBrush(SelectGlow), 2.5);
            ctx.DrawRectangle(null, selPen, new Rect(b.X - 3, b.Y - 3, BlockW + 6, BlockH + 6), 11, 11);
        }

        var glowPen = new Pen(new SolidColorBrush(titleC), 1.5);
        ctx.DrawRectangle(null, glowPen, new Rect(b.X - 1, b.Y - 1, BlockW + 2, BlockH + 2), 9, 9);
        ctx.DrawRectangle(new SolidColorBrush(bodyC), null, new Rect(b.X, b.Y, BlockW, BlockH), 8, 8);
        ctx.DrawRectangle(new SolidColorBrush(titleC), null, new Rect(b.X, b.Y, BlockW, TitleH), 8, 8);
        ctx.DrawRectangle(new SolidColorBrush(titleC), null, new Rect(b.X, b.Y + TitleH - 6, BlockW, 6));

        var tf = new FormattedText(b.Title, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            11, new SolidColorBrush(BgColor));
        ctx.DrawText(tf, new Point(b.X + 12, b.Y + (TitleH - tf.Height) / 2));

        var df = MkText(b.Description, 10, TextGray);
        ctx.DrawText(df, new Point(b.X + 12, b.Y + TitleH + 6));

        if (b.HasInput) DrawPin(ctx, InPin(b), PinDefault, IsHP(b, "In"));
        if (b.IsCondition)
        {
            var jaP = OutPin(b, "Ja"); var neinP = OutPin(b, "Nein");
            DrawPin(ctx, jaP, WireJa, IsHP(b, "Ja"));
            DrawPin(ctx, neinP, WireNein, IsHP(b, "Nein"));
            var jaF = MkText("Ja", 9, WireJa); ctx.DrawText(jaF, new Point(jaP.X - PinR - jaF.Width - 4, jaP.Y - jaF.Height / 2));
            var nF = MkText("Nein", 9, WireNein); ctx.DrawText(nF, new Point(neinP.X - PinR - nF.Width - 4, neinP.Y - nF.Height / 2));
        }
        else DrawPin(ctx, OutPin(b, "Out"), PinDefault, IsHP(b, "Out"));

        // Delete button — red X circle in top-right, only on hover, not on Start block
        if (isHover && b.Type != BlockType.Start)
        {
            var delX = b.X + BlockW - 14;
            var delY = b.Y + 6;
            ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E74C3C")), null, new Point(delX, delY), 8, 8);
            var xTxt = MkText("X", 9, Color.Parse("#FFFFFF"));
            ctx.DrawText(xTxt, new Point(delX - xTxt.Width / 2, delY - xTxt.Height / 2));
        }
    }

    bool IsHP(StrategyBlock b, string port) =>
        _hoverPin.HasValue && _hoverPin.Value.block.Id == b.Id && _hoverPin.Value.port == port;

    static void DrawPin(DrawingContext ctx, Point p, Color fill, bool hover)
    {
        double r = hover ? PinRHover : PinR;
        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(PinBorder), 2), p, r + 2, r + 2);
        ctx.DrawEllipse(new SolidColorBrush(fill), null, p, r, r);
        ctx.DrawEllipse(new SolidColorBrush(BgColor), null, p, r * 0.4, r * 0.4);
    }

    static void DrawWire(DrawingContext ctx, Point from, Point to, string port)
    {
        var mc = port == "Nein" ? WireNein : port == "Ja" ? WireJa : WireOut;
        var gc = port == "Nein" ? WireGlowNein : WireGlowJa;
        DrawBezier(ctx, from, to, new Pen(new SolidColorBrush(gc), 6));
        DrawBezier(ctx, from, to, new Pen(new SolidColorBrush(mc), 2.5));
    }

    static void DrawBezier(DrawingContext ctx, Point from, Point to, Pen pen)
    {
        double dx = Math.Max(60, Math.Abs(to.X - from.X) * 0.55);
        var cp1 = new Point(from.X + dx, from.Y); var cp2 = new Point(to.X - dx, to.Y);
        var prev = from;
        for (int i = 1; i <= 24; i++)
        {
            double t = i / 24.0, it = 1 - t;
            double x = it * it * it * from.X + 3 * it * it * t * cp1.X + 3 * it * t * t * cp2.X + t * t * t * to.X;
            double y = it * it * it * from.Y + 3 * it * it * t * cp1.Y + 3 * it * t * t * cp2.Y + t * t * t * to.Y;
            ctx.DrawLine(pen, prev, new Point(x, y)); prev = new Point(x, y);
        }
    }

    static FormattedText MkText(string t, double size, Color c) =>
        new(t, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, size, new SolidColorBrush(c));

    // ── Hit testing (canvas space) ──────────────────────────────

    static StrategyBlock? HitBlock(TradingStrategy s, Point p)
    {
        for (int i = s.Blocks.Count - 1; i >= 0; i--)
        {
            var b = s.Blocks[i];
            if (p.X >= b.X && p.X <= b.X + BlockW && p.Y >= b.Y && p.Y <= b.Y + BlockH) return b;
        }
        return null;
    }

    static StrategyConnection? HitConnection(TradingStrategy s, Point p)
    {
        foreach (var c in s.Connections)
        {
            var from = s.Blocks.FirstOrDefault(b => b.Id == c.FromBlockId);
            var to = s.Blocks.FirstOrDefault(b => b.Id == c.ToBlockId);
            if (from == null || to == null) continue;
            var mid = new Point((OutPin(from, c.OutputPort).X + InPin(to).X) / 2,
                                (OutPin(from, c.OutputPort).Y + InPin(to).Y) / 2);
            if (Dist(p, mid) < 18) return c;
        }
        return null;
    }

    static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
