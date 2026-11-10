using System.Runtime.InteropServices;
using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>Janela que trata as proprias mensagens.</summary>
internal interface IWindow { nint Proc(nint h, uint m, nint w, nint l); }

/// <summary>Um unico WndProc nativo que despacha para a janela registrada.</summary>
internal static unsafe class Windowing
{
    private static readonly Dictionary<nint, IWindow> s_map = new();
    public static nint Instance { get; } = Native.GetModuleHandle(null);
    private static readonly HashSet<string> s_classes = new();

    public static void Register(nint h, IWindow w) { if (h != 0) s_map[h] = w; }
    public static void Unregister(nint h) => s_map.Remove(h);

    public static nint Create(string cls, string title, uint style, uint exStyle, nint icon = 0)
    {
        fixed (char* c = cls) fixed (char* t = title)
        {
            if (s_classes.Add(cls))
            {
                var wc = new Native.WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(Native.WNDCLASSEXW), lpfnWndProc = &WndProc, hInstance = Instance, lpszClassName = c,
                    hCursor = Native.LoadCursor(0, 32512), hIcon = icon, hIconSm = icon,
                };
                Native.RegisterClassEx(&wc);
            }
            return Native.CreateWindowEx(exStyle, c, t, style, 0, 0, 10, 10, 0, 0, Instance, 0);
        }
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint h, uint m, nint w, nint l)
    {
        try
        {
            if (s_map.TryGetValue(h, out var win)) return win.Proc(h, m, w, l);
        }
        catch (Exception ex) { Log.Line($"ERRO na mensagem 0x{m:X4}: {ex}"); }
        return Native.DefWindowProc(h, m, w, l);
    }

    /// <summary>
    /// Retangulo (px) para uma janela de wDip x hDip junto do icone: acima/abaixo/ao lado conforme a borda da barra,
    /// preso a area util do monitor do icone.
    /// </summary>
    public static Native.RECT PlaceNear(Native.RECT icon, float wDip, float hDip, out uint dpi)
    {
        nint mon = Native.MonitorFromRect(&icon, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = (uint)sizeof(Native.MONITORINFO) };
        Native.GetMonitorInfo(mon, &mi);
        uint dx, dy; Native.GetDpiForMonitor(mon, 0, &dx, &dy);
        dpi = dx == 0 ? 96 : dx;
        int w = (int)MathF.Round(wDip * dpi / 96f), h = (int)MathF.Round(hDip * dpi / 96f), gap = (int)MathF.Round(8 * dpi / 96f);
        var work = mi.rcWork;
        int cx = (icon.Left + icon.Right) / 2, cy = (icon.Top + icon.Bottom) / 2;
        int x, y;
        if (icon.Bottom <= work.Top) { x = cx - w / 2; y = icon.Bottom + gap; }
        else if (icon.Right <= work.Left) { x = icon.Right + gap; y = cy - h / 2; }
        else if (icon.Left >= work.Right) { x = icon.Left - gap - w; y = cy - h / 2; }
        else { x = cx - w / 2; y = icon.Top - gap - h; } // barra embaixo (ou icone no estouro)
        x = Math.Clamp(x, work.Left + gap, Math.Max(work.Left + gap, work.Right - gap - w));
        y = Math.Clamp(y, work.Top + gap, Math.Max(work.Top + gap, work.Bottom - gap - h));
        return new Native.RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }

    public static void Style(nint h, bool dark)
    {
        int round = 2; // DWMWCP_ROUND
        Native.DwmSetWindowAttribute(h, Native.DWMWA_WINDOW_CORNER_PREFERENCE, &round, 4);
        int d = dark ? 1 : 0;
        Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, &d, 4);
    }
}

/// <summary>Estado compartilhado entre o icone, o cartao e o painel (so a thread da interface escreve).</summary>
internal sealed class Model
{
    public Snapshot Now = Snapshot.Empty;
    public readonly History Cpu = new(), Mem = new(), Gpu = new(), Disk = new(), Net = new(), Latency = new();
    public bool LatencyOn;
    public double InternetMs = double.NaN, GatewayMs = double.NaN;
    public bool Dark;
    public Palette Pal => Dark ? Palette.Dark : Palette.Light;

    public void Push(Snapshot s, double internetMs, double gatewayMs)
    {
        Now = s; InternetMs = internetMs; GatewayMs = gatewayMs;
        Cpu.Add(s.Cpu); Mem.Add(s.MemPercent); Gpu.Add(s.Gpu);
        Disk.Add(Sum(s.DiskReadBps, s.DiskWriteBps)); Net.Add(Sum(s.NetRxBps, s.NetTxBps));
        Latency.Add(LatencyOn ? internetMs : double.NaN);
    }

    public static double Sum(double a, double b) => double.IsNaN(a) && double.IsNaN(b) ? double.NaN : (double.IsNaN(a) ? 0 : a) + (double.IsNaN(b) ? 0 : b);
}
