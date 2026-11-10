namespace TrayceMonitor.Core;

/// <summary>Ultimos 60 pontos (1 por segundo) para as sparklines. Sem alocacao por tick.</summary>
public sealed class History
{
    public const int Capacity = 60;
    private readonly double[] _v = new double[Capacity];
    private int _next;
    public int Count { get; private set; }

    public void Add(double v)
    {
        _v[_next] = v;
        _next = (_next + 1) % Capacity;
        if (Count < Capacity) Count++;
    }

    /// <summary>i = 0 e o mais antigo dos <see cref="Count"/> pontos.</summary>
    public double this[int i] => _v[(_next - Count + i + Capacity) % Capacity];

    /// <summary>Maior valor valido (para escala automatica de taxas); 0 se nao houver.</summary>
    public double Max()
    {
        double m = 0;
        for (int i = 0; i < Count; i++) { double v = this[i]; if (!double.IsNaN(v) && v > m) m = v; }
        return m;
    }

    public void Clear() { Count = 0; _next = 0; }
}

/// <summary>Taxa a partir de contadores acumulados (octetos). Reinicio ou volta do contador = sem leitura.</summary>
public sealed class CounterRate
{
    private ulong _last; private long _lastMs = -1;

    public double Update(ulong value, long nowMs)
    {
        double rate = double.NaN;
        if (_lastMs >= 0 && nowMs > _lastMs && value >= _last)
            rate = (value - _last) * 1000.0 / (nowMs - _lastMs);
        _last = value; _lastMs = nowMs;
        return rate;
    }
}
