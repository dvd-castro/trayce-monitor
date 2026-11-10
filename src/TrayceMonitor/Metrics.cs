using System.Runtime.InteropServices;
using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>Uma leitura do sistema. NaN = sem leitura.</summary>
internal struct Snapshot
{
    public double Cpu, MemPercent, MemUsedGb, MemTotalGb, Gpu, GpuTempC, DiskReadBps, DiskWriteBps, NetRxBps, NetTxBps;
    public static Snapshot Empty => new()
    {
        Cpu = double.NaN, MemPercent = double.NaN, MemUsedGb = double.NaN, MemTotalGb = double.NaN, Gpu = double.NaN,
        GpuTempC = double.NaN, DiskReadBps = double.NaN, DiskWriteBps = double.NaN, NetRxBps = double.NaN, NetTxBps = double.NaN,
    };
}

/// <summary>
/// Amostrador sem driver e sem admin: PDH (CPU utility, disco leitura/escrita, GPU Engine), GlobalMemoryStatusEx,
/// GetIfTable2 (octetos das placas fisicas ligadas) e D3DKMT (temperatura da GPU). Roda na thread do amostrador.
/// </summary>
internal sealed unsafe class Metrics : IDisposable
{
    private nint _q, _qGpu, _cCpu, _cDiskRead, _cDiskWrite, _cGpu;
    private byte* _buf; private uint _bufSize;
    private readonly Dictionary<int, double> _gpuAgg = new(64);
    private readonly List<uint> _adapters = new();
    private readonly CounterRate _rx = new(), _tx = new();

    public Metrics()
    {
        nint q;
        if (Native.PdhOpenQuery(null, 0, &q) == 0)
        {
            _q = q;
            _cCpu = Add(_q, @"\Processor Information(_Total)\% Processor Utility");
            _cDiskRead = Add(_q, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
            _cDiskWrite = Add(_q, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
            Native.PdhCollectQueryData(_q);
        }
        if (Native.PdhOpenQuery(null, 0, &q) == 0)
        {
            _qGpu = q;
            _cGpu = Add(_qGpu, @"\GPU Engine(*)\Utilization Percentage");
            Native.PdhCollectQueryData(_qGpu);
        }
        OpenAdapters();
        ReadNet(out _, out _);
    }

    private static nint Add(nint q, string path)
    {
        nint c; uint st = Native.PdhAddEnglishCounter(q, path, 0, &c);
        if (st != 0) { Log.Line($"PDH: contador indisponivel {path} (0x{st:X8})"); return 0; }
        return c;
    }

    private void OpenAdapters()
    {
        var e = new Native.D3DKMT_ENUMADAPTERS2();
        if (Native.D3DKMTEnumAdapters2(&e) != 0 || e.NumAdapters == 0) return;
        var arr = new Native.D3DKMT_ADAPTERINFO[e.NumAdapters];
        fixed (Native.D3DKMT_ADAPTERINFO* p = arr)
        {
            e.pAdapters = p;
            if (Native.D3DKMTEnumAdapters2(&e) != 0) return;
        }
        for (int n = 0; n < e.NumAdapters; n++) _adapters.Add(arr[n].hAdapter);
    }

    public Snapshot Sample()
    {
        var s = Snapshot.Empty;
        if (_q != 0)
        {
            Native.PdhCollectQueryData(_q);
            s.Cpu = Math.Min(100, Value(_cCpu));
            s.DiskReadBps = Value(_cDiskRead);
            s.DiskWriteBps = Value(_cDiskWrite);
        }
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)sizeof(Native.MEMORYSTATUSEX) };
        if (Native.GlobalMemoryStatusEx(&m) != 0 && m.ullTotalPhys > 0)
        {
            s.MemTotalGb = m.ullTotalPhys / 1073741824.0;
            s.MemUsedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
            s.MemPercent = 100.0 * (m.ullTotalPhys - m.ullAvailPhys) / m.ullTotalPhys;
        }
        ReadNet(out s.NetRxBps, out s.NetTxBps);
        s.Gpu = GpuEngineMax();
        foreach (uint h in _adapters)
        {
            var pd = new Native.D3DKMT_ADAPTER_PERFDATA();
            var qp = new Native.D3DKMT_QUERYADAPTERINFO { hAdapter = h, Type = Native.KMTQAITYPE_ADAPTERPERFDATA, pPrivateDriverData = &pd, PrivateDriverDataSize = (uint)sizeof(Native.D3DKMT_ADAPTER_PERFDATA) };
            if (Native.D3DKMTQueryAdapterInfo(&qp) == 0 && pd.Temperature > 0)
                s.GpuTempC = double.IsNaN(s.GpuTempC) ? pd.Temperature / 10.0 : Math.Max(s.GpuTempC, pd.Temperature / 10.0);
        }
        return s;
    }

    /// <summary>Soma InOctets/OutOctets das interfaces de hardware ligadas (sem filtros nem loopback): sem dupla contagem.</summary>
    private void ReadNet(out double rx, out double tx)
    {
        rx = tx = double.NaN;
        void* table;
        if (Native.GetIfTable2(&table) != 0) return;
        try
        {
            uint n = *(uint*)table;
            byte* row = (byte*)table + 8;
            ulong inSum = 0, outSum = 0;
            for (uint i = 0; i < n; i++, row += Native.IfRowSize)
            {
                byte flags = row[Native.IfRowFlags];
                bool hardware = (flags & 1) != 0, filter = (flags & 2) != 0;
                uint type = *(uint*)(row + Native.IfRowType), oper = *(uint*)(row + Native.IfRowOperStatus);
                if (!hardware || filter || oper != 1 || type == 24) continue;
                inSum += *(ulong*)(row + Native.IfRowInOctets);
                outSum += *(ulong*)(row + Native.IfRowOutOctets);
            }
            long now = Environment.TickCount64;
            rx = _rx.Update(inSum, now);
            tx = _tx.Update(outSum, now);
        }
        finally { Native.FreeMibTable(table); }
    }

    private static double Value(nint c)
    {
        if (c == 0) return double.NaN;
        uint type; Native.PDH_FMT_COUNTERVALUE v;
        uint st = Native.PdhGetFormattedCounterValue(c, Native.PDH_FMT_DOUBLE | Native.PDH_FMT_NOCAP100, &type, &v);
        return st == 0 && (v.CStatus == 0 || v.CStatus == 1) ? v.doubleValue : double.NaN;
    }

    private uint ReadArray(nint c)
    {
        if (c == 0) return 0;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            uint size = _bufSize, count;
            uint st = Native.PdhGetFormattedCounterArray(c, Native.PDH_FMT_DOUBLE | Native.PDH_FMT_NOCAP100, &size, &count, _buf);
            if (st == 0) return count;
            if (st != Native.PDH_MORE_DATA) return 0;
            if (_buf != null) NativeMemory.Free(_buf);
            _bufSize = size + 4096;
            _buf = (byte*)NativeMemory.Alloc(_bufSize);
        }
        return 0;
    }

    /// <summary>GPU = maior soma por (adaptador, tipo de motor), como o Gerenciador de Tarefas.</summary>
    private double GpuEngineMax()
    {
        if (_qGpu == 0) return double.NaN;
        Native.PdhCollectQueryData(_qGpu);
        uint n = ReadArray(_cGpu);
        if (n == 0) return double.NaN;
        var items = (Native.PDH_FMT_COUNTERVALUE_ITEM_W*)_buf;
        _gpuAgg.Clear();
        for (int j = 0; j < n; j++)
        {
            if (items[j].FmtValue.CStatus > 1) continue;
            var name = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(items[j].szName);
            int li = name.IndexOf("luid_"), pi = name.IndexOf("_phys"), ei = name.IndexOf("engtype_");
            if (li < 0 || pi < li || ei < 0) continue;
            int key = HashCode.Combine(string.GetHashCode(name[li..pi]), string.GetHashCode(name[(ei + 8)..]));
            _gpuAgg[key] = _gpuAgg.GetValueOrDefault(key) + items[j].FmtValue.doubleValue;
        }
        double max = 0;
        foreach (var v in _gpuAgg.Values) max = Math.Max(max, v);
        return Math.Min(100, max);
    }

    public void Dispose()
    {
        if (_q != 0) { Native.PdhCloseQuery(_q); _q = 0; }
        if (_qGpu != 0) { Native.PdhCloseQuery(_qGpu); _qGpu = 0; }
        foreach (uint h in _adapters) { var c = new Native.D3DKMT_CLOSEADAPTER { hAdapter = h }; Native.D3DKMTCloseAdapter(&c); }
        _adapters.Clear();
        if (_buf != null) { NativeMemory.Free(_buf); _buf = null; }
    }
}
