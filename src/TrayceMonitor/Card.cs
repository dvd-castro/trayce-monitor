using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>
/// Cartao de pouso: nunca ativa (WS_EX_NOACTIVATE + TOOLWINDOW + TOPMOST, MA_NOACTIVATE, SW_SHOWNOACTIVATE),
/// cantos do Windows 11, fundo solido, Direct2D em software. Recursos graficos soltos 3 s depois de fechar.
/// </summary>
internal sealed unsafe class Card : IWindow
{
    public const float WidthDip = 360, HeaderDip = 40, RowDip = 52, BottomDip = 10;
    public nint Hwnd { get; }
    public bool Visible { get; private set; }
    public Native.RECT Rect;
    public Action? Clicked;
    private readonly Model _m;
    private readonly Surface _s;
    private uint _dpi = 96;

    public Card(Model m)
    {
        _m = m;
        Hwnd = Windowing.Create("TrayceMonitor.Card", "Trayce Monitor", Native.WS_POPUP,
            Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST);
        Windowing.Register(Hwnd, this);
        Windowing.Style(Hwnd, m.Dark);
        _s = new Surface(Hwnd);
    }

    private int Rows => _m.LatencyOn ? 6 : 5;
    public float HeightDip => HeaderDip + Rows * RowDip + BottomDip;

    public void Place(Native.RECT icon)
    {
        Rect = Windowing.PlaceNear(icon, WidthDip, HeightDip, out _dpi);
        Native.SetWindowPos(Hwnd, -1 /*HWND_TOPMOST*/, Rect.Left, Rect.Top, Rect.Width, Rect.Height, Native.SWP_NOACTIVATE);
        _s.Ensure(Rect.Width, Rect.Height, _dpi);
    }

    public void Show()
    {
        Native.ShowWindow(Hwnd, Native.SW_SHOWNOACTIVATE);
        Visible = true;
        Native.InvalidateRect(Hwnd, null, 0);
    }

    public void Hide() { Native.ShowWindow(Hwnd, Native.SW_HIDE); Visible = false; }
    public bool HasResources => _s.Ready;
    public void ReleaseResources() => _s.Release();
    public void ApplyTheme() { Windowing.Style(Hwnd, _m.Dark); if (Visible) Native.InvalidateRect(Hwnd, null, 0); }
    public void Refresh() { if (Visible) Native.InvalidateRect(Hwnd, null, 0); }

    public nint Proc(nint h, uint m, nint w, nint l)
    {
        switch (m)
        {
            case Native.WM_MOUSEACTIVATE: return Native.MA_NOACTIVATE;
            case Native.WM_ERASEBKGND: return 1;
            case Native.WM_PAINT:
            {
                Native.PAINTSTRUCT ps; Native.BeginPaint(h, &ps);
                Paint();
                Native.EndPaint(h, &ps);
                return 0;
            }
            case Native.WM_LBUTTONUP: Clicked?.Invoke(); return 0;
        }
        return Native.DefWindowProc(h, m, w, l);
    }

    private void Paint()
    {
        if (!_s.Ready) _s.Ensure(Rect.Width, Rect.Height, _dpi);
        var p = _m.Pal; var d = _m.Now;
        _s.Begin(p.WindowBg);
        const float pad = 16;
        _s.Text(AppInfo.Name, 13, 600, p.Text, pad, 12, 200, 18);
        _s.Text("Clique para abrir o painel", 11, 400, p.Text3, WidthDip - pad - 170, 14, 170, 16, Surface.Align.Right);
        float y = HeaderDip;
        Row(ref y, "CPU", d.Cpu, Format.Percent(d.Cpu), "", _m.Cpu, 100);
        Row(ref y, "MEMÓRIA", d.MemPercent, Format.Percent(d.MemPercent), Format.Memory(d.MemUsedGb, d.MemTotalGb), _m.Mem, 100);
        Row(ref y, "GPU", d.Gpu, Format.Percent(d.Gpu), Format.Temperature(d.GpuTempC), _m.Gpu, 100);
        Row(ref y, "DISCO", double.NaN, Format.Rate(Model.Sum(d.DiskReadBps, d.DiskWriteBps)),
            $"L {Format.Rate(d.DiskReadBps)} · E {Format.Rate(d.DiskWriteBps)}", _m.Disk, 0);
        Row(ref y, "REDE", double.NaN, Format.Rate(Model.Sum(d.NetRxBps, d.NetTxBps)),
            $"↓ {Format.Rate(d.NetRxBps)} · ↑ {Format.Rate(d.NetTxBps)}", _m.Net, 0);
        if (_m.LatencyOn)
            Row(ref y, "LATÊNCIA", double.NaN, Format.Latency(_m.InternetMs), $"gateway {Format.Latency(_m.GatewayMs)}", _m.Latency, 0);
        if (!_s.End()) Native.InvalidateRect(Hwnd, null, 0);
    }

    private void Row(ref float y, string label, double pct, string value, string detail, History h, double max)
    {
        var p = _m.Pal;
        const float pad = 16, ringX = pad + 14, textX = 58, textW = 210, sparkX = 280, sparkW = WidthDip - sparkX - pad;
        bool isPct = !double.IsNaN(pct) || max == 100;
        uint color = isPct ? p.ForBand(Bands.Of(pct)) : p.Accent;
        if (isPct) _s.Ring(ringX, y + 26, 11, 4, pct, color, p.Track);
        else
        {
            _s.Ring(ringX, y + 26, 11, 4, double.NaN, color, p.Track);
            _s.RoundRect(ringX - 3, y + 23, 6, 6, 3, p.Accent);
        }
        _s.Text(label, 11, 600, p.Text2, textX, y + 7, 90, 16);
        if (detail.Length > 0) _s.Text(detail, 11, 400, p.Text2, textX + 60, y + 7, textW - 60, 16, Surface.Align.Right);
        _s.Text(value, 20, 600, p.Text, textX, y + 20, textW, 28);
        _s.Sparkline(h, sparkX, y + 14, sparkW, 26, isPct ? color : p.Accent, p.Track, max);
        y += RowDip;
    }
}
