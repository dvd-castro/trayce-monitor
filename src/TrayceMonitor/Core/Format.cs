using System.Globalization;

namespace TrayceMonitor.Core;

/// <summary>Formatacao dos numeros na interface (pt-BR: virgula decimal). "—" quando nao ha leitura.</summary>
public static class Format
{
    public const string None = "—";
    private static readonly NumberFormatInfo Nfi = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static string Percent(double v) => double.IsNaN(v) ? None : $"{Math.Clamp(Math.Round(v), 0, 100).ToString("F0", Nfi)}%";

    /// <summary>Taxa em bytes por segundo: B/s, KB/s (inteiro), MB/s e GB/s (1 casa). Base 1024.</summary>
    public static string Rate(double bytesPerSec)
    {
        if (double.IsNaN(bytesPerSec) || bytesPerSec < 0) return None;
        if (bytesPerSec < 1024) return $"{bytesPerSec.ToString("F0", Nfi)} B/s";
        if (bytesPerSec < 1024 * 1024) return $"{(bytesPerSec / 1024).ToString("F0", Nfi)} KB/s";
        if (bytesPerSec < 1024.0 * 1024 * 1024) return $"{(bytesPerSec / 1048576).ToString("F1", Nfi)} MB/s";
        return $"{(bytesPerSec / 1073741824).ToString("F1", Nfi)} GB/s";
    }

    /// <summary>"7,9 / 15,9 GB".</summary>
    public static string Memory(double usedGb, double totalGb)
    {
        if (double.IsNaN(usedGb) || double.IsNaN(totalGb) || totalGb <= 0) return None;
        return $"{usedGb.ToString("F1", Nfi)} / {totalGb.ToString("F1", Nfi)} GB";
    }

    public static string Temperature(double celsius) => double.IsNaN(celsius) || celsius <= 0 ? None : $"{Math.Round(celsius).ToString("F0", Nfi)} °C";

    public static string Latency(double ms)
    {
        if (double.IsNaN(ms) || ms < 0) return None;
        return ms < 1 ? "<1 ms" : $"{Math.Round(ms).ToString("F0", Nfi)} ms";
    }

    /// <summary>Tooltip de reserva da bandeja (szTip tem 128 posicoes, com o terminador: maximo 127).</summary>
    public const int TipMax = 127;
    public static string Tooltip(double cpu, double mem, double gpu, double latencyMs, bool latencyOn)
    {
        string s = $"Trayce Monitor\nCPU {Percent(cpu)} · RAM {Percent(mem)} · GPU {Percent(gpu)}";
        if (latencyOn) s += $"\nLatência {Latency(latencyMs)}";
        return Clip(s, TipMax);
    }

    public static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
