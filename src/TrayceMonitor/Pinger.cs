using System.Runtime.InteropServices;

namespace TrayceMonitor;

/// <summary>
/// Latencia por ICMP sem admin (IcmpSendEcho2 com evento, thread propria). A cada 2 s mede 1.1.1.1 e o gateway
/// padrao, um alvo por vez (um eco em voo), payload fixo de 32 B, timeout de 1 s. Em falha o alvo recua
/// (2, 4, 8, 16 s). Pausa enquanto a sessao esta bloqueada. Desligado por padrao: so roda se o usuario ligar.
/// </summary>
internal sealed unsafe partial class Pinger : IDisposable
{
    public const int IntervalMs = 2000, MaxBackoffTicks = 8, PayloadBytes = 32;
    private const uint Cloudflare = 0x01010101; // 1.1.1.1

    private volatile float _internetMs = float.NaN, _gatewayMs = float.NaN;
    private volatile bool _stop, _paused;
    private Thread? _thread;
    private readonly AutoResetEvent _wake = new(false);

    public double InternetMs => _internetMs;
    public double GatewayMs => _gatewayMs;
    public bool Running => _thread != null;

    [LibraryImport("iphlpapi.dll")] private static partial nint IcmpCreateFile();
    [LibraryImport("iphlpapi.dll")] private static partial int IcmpCloseHandle(nint h);
    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    private static partial uint IcmpSendEcho2(nint h, nint evt, nint apc, nint apcCtx, uint dest, void* data, ushort size, void* opts, void* reply, uint replySize, uint timeout);
    [LibraryImport("iphlpapi.dll")] private static partial uint IcmpParseReplies(void* reply, uint size);
    [LibraryImport("iphlpapi.dll")] private static partial uint GetBestRoute2(void* luid, uint ifIndex, void* src, void* dst, uint opts, void* row, void* bestSrc);
    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW")] private static partial nint CreateEvent(nint sa, int manual, int initial, nint name);
    [LibraryImport("kernel32.dll")] private static partial uint WaitForSingleObject(nint h, uint ms);
    [LibraryImport("kernel32.dll")] private static partial int ResetEvent(nint h);
    [LibraryImport("kernel32.dll")] private static partial int CloseHandle(nint h);

    public void Start()
    {
        if (_thread != null) return;
        _stop = false;
        _thread = new Thread(Loop) { IsBackground = true, Name = "latencia", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public void Stop()
    {
        if (_thread == null) return;
        _stop = true; _wake.Set();
        _thread.Join(3000);
        _thread = null;
        _internetMs = _gatewayMs = float.NaN;
    }

    /// <summary>Sessao bloqueada: nao envia nada ate desbloquear.</summary>
    public bool Paused
    {
        get => _paused;
        set { _paused = value; if (!value) _wake.Set(); if (value) _internetMs = _gatewayMs = float.NaN; }
    }

    private static uint Gateway()
    {
        byte* dst = stackalloc byte[28]; new Span<byte>(dst, 28).Clear();
        *(ushort*)dst = 2; *(uint*)(dst + 4) = Cloudflare;
        byte* row = stackalloc byte[256]; byte* src = stackalloc byte[28];
        new Span<byte>(row, 256).Clear();
        uint st = GetBestRoute2(null, 0, null, dst, 0, row, src);
        ushort fam = *(ushort*)(row + 44);
        uint gw = *(uint*)(row + 48);
        return st == 0 && fam == 2 ? gw : 0;
    }

    private void Loop()
    {
        nint h = IcmpCreateFile();
        nint evt = CreateEvent(0, 1, 0, 0);
        if (h == -1 || evt == 0) { Log.Line("latencia: IcmpCreateFile falhou"); return; }
        byte* data = stackalloc byte[PayloadBytes]; new Span<byte>(data, PayloadBytes).Fill(0x54);
        byte* reply = stackalloc byte[256];
        var cf = new Backoff(); var gwB = new Backoff();
        uint gw = 0; int tick = 0;
        try
        {
            while (!_stop)
            {
                if (_paused) { _wake.WaitOne(); continue; }
                if (tick % 15 == 0 || gw == 0) gw = Gateway();
                if (cf.Due()) { float ms = Echo(h, evt, Cloudflare, data, reply); _internetMs = ms; cf.Result(!float.IsNaN(ms)); }
                if (_stop || _paused) continue;
                if (gw == 0) _gatewayMs = float.NaN;
                else if (gwB.Due()) { float ms = Echo(h, evt, gw, data, reply); _gatewayMs = ms; gwB.Result(!float.IsNaN(ms)); if (float.IsNaN(ms)) gw = 0; }
                tick++;
                _wake.WaitOne(IntervalMs);
            }
        }
        finally { IcmpCloseHandle(h); CloseHandle(evt); }
    }

    /// <summary>Recuo por alvo: depois de k falhas seguidas espera min(2^k, 8) ticks.</summary>
    private sealed class Backoff
    {
        private int _fails, _skip;
        public bool Due() { if (_skip > 0) { _skip--; return false; } return true; }
        public void Result(bool ok)
        {
            if (ok) { _fails = 0; _skip = 0; return; }
            _fails = Math.Min(_fails + 1, 3);
            _skip = Math.Min(1 << _fails, MaxBackoffTicks) - 1;
        }
    }

    private static float Echo(nint h, nint evt, uint dest, byte* data, byte* reply)
    {
        ResetEvent(evt);
        uint r = IcmpSendEcho2(h, evt, 0, 0, dest, data, PayloadBytes, null, reply, 256, 1000);
        int err = r == 0 ? Marshal.GetLastPInvokeError() : 0;
        if (r == 0 && err != 997 /*ERROR_IO_PENDING*/) return float.NaN;
        if (WaitForSingleObject(evt, 1500) != 0) return float.NaN;
        uint count = IcmpParseReplies(reply, 256);
        uint status = *(uint*)(reply + 4), rtt = *(uint*)(reply + 8);
        return count > 0 && status == 0 ? rtt : float.NaN;
    }

    public void Dispose() { Stop(); _wake.Dispose(); }
}
