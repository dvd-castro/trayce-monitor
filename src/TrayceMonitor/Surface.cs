using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>
/// Alvo de desenho Direct2D (software: sem dispositivo de GPU, menos memoria) de uma janela, com as primitivas
/// que o cartao e o painel usam: texto, anel de progresso, sparkline e retangulo arredondado. Coordenadas em DIP.
/// </summary>
internal sealed unsafe class Surface
{
    private readonly nint _hwnd;
    private void* _factory, _dw, _rt, _brush, _round;
    private readonly Dictionary<(int weight, float size), nint> _formats = new();
    public uint Dpi { get; private set; } = 96;
    public bool Ready => _rt != null;

    public Surface(nint hwnd) { _hwnd = hwnd; }

    public void Ensure(int pxW, int pxH, uint dpi)
    {
        if (_rt != null && dpi == Dpi) { D2D.Resize(_rt, new D2D.SizeU { w = (uint)Math.Max(1, pxW), h = (uint)Math.Max(1, pxH) }); return; }
        Release();
        Dpi = dpi;
        _factory = D2D.CreateFactory();
        _dw = D2D.CreateDWriteFactory();
        var rtp = new D2D.RenderTargetProps { type = D2D.RT_SOFTWARE, alphaMode = D2D.ALPHA_UNKNOWN, dpiX = dpi, dpiY = dpi };
        var hp = new D2D.HwndRenderTargetProps { hwnd = _hwnd, pixelSize = new D2D.SizeU { w = (uint)Math.Max(1, pxW), h = (uint)Math.Max(1, pxH) } };
        _rt = D2D.CreateHwndRenderTarget(_factory, &rtp, &hp);
        D2D.SetAntialiasMode(_rt, 0);
        _brush = D2D.CreateBrush(_rt, new D2D.ColorF(0xFF000000));
        var sp = new D2D.StrokeStyleProps { startCap = D2D.CAP_ROUND, endCap = D2D.CAP_ROUND, dashCap = D2D.CAP_ROUND, lineJoin = 2, miterLimit = 10 };
        _round = D2D.CreateStrokeStyle(_factory, &sp);
    }

    public void Release()
    {
        foreach (var f in _formats.Values) D2D.Release((void*)f);
        _formats.Clear();
        foreach (var p in new nint[] { (nint)_round, (nint)_brush, (nint)_rt, (nint)_dw, (nint)_factory }) D2D.Release((void*)p);
        _round = _brush = _rt = _dw = _factory = null;
    }

    public void Begin(uint bg) { D2D.BeginDraw(_rt); D2D.Clear(_rt, new D2D.ColorF(bg)); }

    /// <summary>Devolve false se o alvo precisa ser recriado (D2DERR_RECREATE_TARGET).</summary>
    public bool End()
    {
        int hr = D2D.EndDraw(_rt);
        if (hr < 0) { Log.Line($"desenho: EndDraw 0x{hr:X8}; recriando"); Release(); return false; }
        return true;
    }

    private void* Fmt(int weight, float size)
    {
        if (_formats.TryGetValue((weight, size), out var f)) return (void*)f;
        void* tf = D2D.CreateTextFormat(_dw, size >= 20 ? "Segoe UI Variable Display" : "Segoe UI Variable Text", weight, size);
        D2D.SetWordWrapping(tf, 1);
        _formats[(weight, size)] = (nint)tf;
        return tf;
    }

    public enum Align { Left = 0, Right = 1, Center = 2 }

    public void Text(string s, float size, int weight, uint color, float x, float y, float w, float h, Align align = Align.Left)
    {
        void* tf = Fmt(weight, size);
        D2D.SetTextAlignment(tf, (int)align);
        D2D.SetColor(_brush, new D2D.ColorF(color));
        D2D.DrawText(_rt, s, tf, new D2D.RectF(x, y, x + w, y + h), _brush);
    }

    public void RoundRect(float x, float y, float w, float h, float r, uint fill, uint stroke = 0)
    {
        var rr = new D2D.RoundedRect { rect = new D2D.RectF(x, y, x + w, y + h), rx = r, ry = r };
        D2D.SetColor(_brush, new D2D.ColorF(fill));
        D2D.FillRoundedRectangle(_rt, rr, _brush);
        if (stroke != 0)
        {
            D2D.SetColor(_brush, new D2D.ColorF(stroke));
            D2D.DrawRoundedRectangle(_rt, rr, _brush, 1);
        }
    }

    public void Line(float x0, float y0, float x1, float y1, uint color, float w = 1)
    {
        D2D.SetColor(_brush, new D2D.ColorF(color));
        D2D.DrawLine(_rt, new D2D.Point2F(x0, y0), new D2D.Point2F(x1, y1), _brush, w, null);
    }

    /// <summary>Anel de progresso (0..100) com trilho; NaN = so o trilho.</summary>
    public void Ring(float cx, float cy, float r, float stroke, double v, uint color, uint track)
    {
        D2D.SetColor(_brush, new D2D.ColorF(track));
        D2D.DrawEllipse(_rt, new D2D.Ellipse { p = new D2D.Point2F(cx, cy), rx = r, ry = r }, _brush, stroke, null);
        if (double.IsNaN(v)) return;
        D2D.SetColor(_brush, new D2D.ColorF(color));
        float frac = (float)Math.Clamp(v / 100.0, 0, 1);
        if (frac >= 0.999f) { D2D.DrawEllipse(_rt, new D2D.Ellipse { p = new D2D.Point2F(cx, cy), rx = r, ry = r }, _brush, stroke, null); return; }
        if (frac < 0.01f) { D2D.FillEllipse(_rt, new D2D.Ellipse { p = new D2D.Point2F(cx, cy - r), rx = stroke / 2, ry = stroke / 2 }, _brush); return; }
        float a = frac * MathF.PI * 2;
        void* path = D2D.CreatePathGeometry(_factory);
        if (path == null) return;
        void* sink = D2D.Open(path);
        D2D.BeginFigure(sink, new D2D.Point2F(cx, cy - r), 1);
        D2D.AddArc(sink, new D2D.ArcSegment { point = new D2D.Point2F(cx + r * MathF.Sin(a), cy - r * MathF.Cos(a)), size = new D2D.SizeF { w = r, h = r }, sweepDirection = 1, arcSize = frac > 0.5f ? 1 : 0 });
        D2D.EndFigure(sink, 0);
        D2D.CloseSink(sink);
        D2D.DrawGeometry(_rt, path, _brush, stroke, _round);
        D2D.Release(sink); D2D.Release(path);
    }

    /// <summary>Sparkline dos ultimos 60 pontos (alinhada a direita), escala fixa (max &gt; 0) ou automatica.</summary>
    public void Sparkline(History h, float x, float y, float w, float hgt, uint color, uint baseline, double max = 0)
    {
        Line(x, y + hgt, x + w, y + hgt, baseline);
        int n = h.Count;
        if (n < 2) return;
        double scale = max > 0 ? max : Math.Max(h.Max() * 1.15, 1);
        var pts = stackalloc D2D.Point2F[History.Capacity + 2];
        for (int i = 0; i < n; i++)
        {
            double v = h[i];
            float fy = double.IsNaN(v) ? 0 : (float)Math.Clamp(v / scale, 0, 1);
            pts[i] = new D2D.Point2F(x + w * (History.Capacity - n + i) / (History.Capacity - 1f), y + hgt - fy * hgt);
        }
        // area translucida
        void* area = D2D.CreatePathGeometry(_factory);
        if (area != null)
        {
            void* sink = D2D.Open(area);
            D2D.BeginFigure(sink, new D2D.Point2F(pts[0].x, y + hgt), 0);
            D2D.AddLines(sink, pts, (uint)n);
            var end = new D2D.Point2F(pts[n - 1].x, y + hgt);
            D2D.AddLines(sink, &end, 1);
            D2D.EndFigure(sink, 1);
            D2D.CloseSink(sink);
            D2D.SetColor(_brush, new D2D.ColorF(color, 0.16f));
            D2D.FillGeometry(_rt, area, _brush);
            D2D.Release(sink); D2D.Release(area);
        }
        void* path = D2D.CreatePathGeometry(_factory);
        if (path == null) return;
        void* s2 = D2D.Open(path);
        D2D.BeginFigure(s2, pts[0], 1);
        D2D.AddLines(s2, pts + 1, (uint)(n - 1));
        D2D.EndFigure(s2, 0);
        D2D.CloseSink(s2);
        D2D.SetColor(_brush, new D2D.ColorF(color));
        D2D.DrawGeometry(_rt, path, _brush, 1.5f, _round);
        D2D.Release(s2); D2D.Release(path);
    }
}
