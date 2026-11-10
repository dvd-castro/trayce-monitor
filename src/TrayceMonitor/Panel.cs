using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>
/// Painel do clique esquerdo: janela Win32 + Direct2D no mesmo processo (sem WinUI: abre em milissegundos e nao
/// carrega o runtime XAML). Seis blocos com valor grande e sparkline de 60 s, rodape com versao. Fecha com Esc,
/// com o X ou ao perder o foco; a janela e destruida ao fechar (nada fica residente).
/// </summary>
internal sealed unsafe class Panel : IWindow
{
    public const float WidthDip = 456, HeaderDip = 52, TileH = 124, Gap = 12, FooterDip = 40;
    public static float HeightDip => HeaderDip + 3 * TileH + 2 * Gap + FooterDip;
    public nint Hwnd { get; private set; }
    private const uint MsgClose = Native.WM_APP + 1;
    public Action? Closed;
    private readonly Model _m;
    private readonly string _version;
    private Surface? _s;
    private Native.RECT _rect;
    private uint _dpi = 96;

    public Panel(Model m, string version) { _m = m; _version = version; }

    public bool IsOpen => Hwnd != 0;

    public void Open(Native.RECT icon, nint appIcon, bool activate)
    {
        if (Hwnd == 0)
        {
            Hwnd = Windowing.Create("TrayceMonitor.Panel", AppInfo.Name, Native.WS_POPUP | Native.WS_SYSMENU, 0, appIcon);
            Windowing.Register(Hwnd, this);
            _s = new Surface(Hwnd);
            if (appIcon != 0) { Native.SendMessage(Hwnd, Native.WM_SETICON, 1, appIcon); Native.SendMessage(Hwnd, Native.WM_SETICON, 0, appIcon); }
        }
        Windowing.Style(Hwnd, _m.Dark);
        _rect = Windowing.PlaceNear(icon, WidthDip, HeightDip, out _dpi);
        Native.SetWindowPos(Hwnd, 0, _rect.Left, _rect.Top, _rect.Width, _rect.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        _s!.Ensure(_rect.Width, _rect.Height, _dpi);
        Native.ShowWindow(Hwnd, activate ? Native.SW_SHOW : Native.SW_SHOWNOACTIVATE);
        if (activate) Native.SetForegroundWindow(Hwnd);
        Native.InvalidateRect(Hwnd, null, 0);
    }

    public void Close()
    {
        if (Hwnd == 0) return;
        nint h = Hwnd;
        Hwnd = 0;
        _s?.Release(); _s = null;
        Windowing.Unregister(h);
        Native.DestroyWindow(h);
        Closed?.Invoke();
    }

    public void Refresh() { if (Hwnd != 0) Native.InvalidateRect(Hwnd, null, 0); }
    public void ApplyTheme() { if (Hwnd != 0) { Windowing.Style(Hwnd, _m.Dark); Refresh(); } }

    public nint Proc(nint h, uint m, nint w, nint l)
    {
        switch (m)
        {
            case Native.WM_ERASEBKGND: return 1;
            case Native.WM_PAINT:
            {
                Native.PAINTSTRUCT ps; Native.BeginPaint(h, &ps);
                Paint();
                Native.EndPaint(h, &ps);
                return 0;
            }
            case Native.WM_ACTIVATE:
                if (Native.LoWord(w) == Native.WA_INACTIVE) Native.PostMessage(h, MsgClose, 0, 0);
                return 0;
            case MsgClose: Close(); return 0;
            case Native.WM_KEYDOWN:
                if ((int)w == Native.VK_ESCAPE) Close();
                return 0;
            case Native.WM_LBUTTONUP:
            {
                float x = Native.SLoWord(l) * 96f / _dpi, y = Native.SHiWord(l) * 96f / _dpi;
                if (x >= WidthDip - 48 && y <= HeaderDip) Close();
                return 0;
            }
            case Native.WM_CLOSE: Close(); return 0;
        }
        return Native.DefWindowProc(h, m, w, l);
    }

    private void Paint()
    {
        if (_s == null) return;
        if (!_s.Ready) _s.Ensure(_rect.Width, _rect.Height, _dpi);
        var p = _m.Pal; var d = _m.Now;
        _s.Begin(p.WindowBg);
        const float pad = 16;
        // cabecalho: anel da marca + nome + fechar
        _s.Ring(pad + 11, 26, 9, 3.5f, 72, p.Accent, p.Track);
        _s.Text(AppInfo.Name, 15, 600, p.Text, pad + 30, 15, 260, 22);
        _s.Text("✕", 13, 400, p.Text2, WidthDip - 44, 16, 28, 20, Surface.Align.Center);

        float tw = (WidthDip - 2 * pad - Gap) / 2, x0 = pad, x1 = pad + tw + Gap, y = HeaderDip;
        Tile(x0, y, tw, "CPU", Format.Percent(d.Cpu), "Uso do processador", _m.Cpu, 100, Bands.Of(d.Cpu));
        Tile(x1, y, tw, "MEMÓRIA", Format.Percent(d.MemPercent), Format.Memory(d.MemUsedGb, d.MemTotalGb), _m.Mem, 100, Bands.Of(d.MemPercent));
        y += TileH + Gap;
        string temp = Format.Temperature(d.GpuTempC);
        Tile(x0, y, tw, "GPU", Format.Percent(d.Gpu), temp == Format.None ? "Temperatura: sem leitura" : $"Temperatura {temp}", _m.Gpu, 100, Bands.Of(d.Gpu));
        Tile(x1, y, tw, "DISCO", Format.Rate(Model.Sum(d.DiskReadBps, d.DiskWriteBps)),
            $"Leitura {Format.Rate(d.DiskReadBps)} · Escrita {Format.Rate(d.DiskWriteBps)}", _m.Disk, 0, Band.NoData);
        y += TileH + Gap;
        Tile(x0, y, tw, "REDE", Format.Rate(Model.Sum(d.NetRxBps, d.NetTxBps)),
            $"↓ {Format.Rate(d.NetRxBps)} · ↑ {Format.Rate(d.NetTxBps)}", _m.Net, 0, Band.NoData);
        if (_m.LatencyOn)
            Tile(x1, y, tw, "LATÊNCIA", Format.Latency(_m.InternetMs), $"1.1.1.1 · gateway {Format.Latency(_m.GatewayMs)}", _m.Latency, 0, Band.NoData);
        else
            Tile(x1, y, tw, "LATÊNCIA", "Desligada", "Ligue no menu do ícone", null, 0, Band.NoData);
        y += TileH;

        _s.Line(pad, y + 12, WidthDip - pad, y + 12, p.Track);
        _s.Text($"{AppInfo.Name} {_version} · Licença {AppInfo.License}", 11, 400, p.Text3, pad, y + 18, 240, 16);
        _s.Text("github.com/dvd-castro/trayce-monitor", 11, 400, p.Text3, WidthDip - pad - 220, y + 18, 220, 16, Surface.Align.Right);
        if (!_s.End()) Refresh();
    }

    private void Tile(float x, float y, float w, string label, string value, string detail, History? h, double max, Band band)
    {
        var p = _m.Pal;
        uint color = band == Band.NoData ? p.Accent : p.ForBand(band);
        _s!.RoundRect(x, y, w, TileH, 8, p.Card, p.Dark() ? 0xFF3A3A3A : 0xFFE5E5E5);
        _s.Text(label, 11, 600, p.Text2, x + 14, y + 10, w - 28, 16);
        _s.Text(value, 26, 600, h == null ? p.Text3 : p.Text, x + 14, y + 24, w - 28, 36);
        _s.Text(detail, 11, 400, p.Text2, x + 14, y + 60, w - 28, 16);
        if (h != null) _s.Sparkline(h, x + 14, y + 82, w - 28, 30, color, p.Track, max);
    }
}

internal static class PaletteExt
{
    public static bool Dark(this Palette p) => ReferenceEquals(p, Palette.Dark);
}
