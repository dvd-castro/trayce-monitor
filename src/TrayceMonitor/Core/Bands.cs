namespace TrayceMonitor.Core;

/// <summary>Faixa de uso que define a forma e a cor do anel: anel liso, anel com ponto, disco cheio.</summary>
public enum Band { Normal, High, Critical, NoData }

/// <summary>Limiares das faixas com histerese: sobe no limiar, so desce 5 pontos abaixo dele.</summary>
public static class Bands
{
    public const double HighAt = 70, CriticalAt = 90, Hysteresis = 5;

    /// <summary>Faixa sem memoria (para cores de linhas do cartao e do painel).</summary>
    public static Band Of(double percent)
    {
        if (double.IsNaN(percent)) return Band.NoData;
        return percent >= CriticalAt ? Band.Critical : percent >= HighAt ? Band.High : Band.Normal;
    }

    /// <summary>Proxima faixa a partir da atual: entra no limiar, sai so abaixo de limiar - histerese.</summary>
    public static Band Next(Band current, double percent)
    {
        if (double.IsNaN(percent)) return Band.NoData;
        Band up = Of(percent);
        Band down = percent >= CriticalAt - Hysteresis ? Band.Critical : percent >= HighAt - Hysteresis ? Band.High : Band.Normal;
        Band cur = current == Band.NoData ? Band.Normal : current;
        Band held = (Band)Math.Min((int)cur, (int)down);
        return (Band)Math.Max((int)up, (int)held);
    }

    /// <summary>Quantiza 0..100 em 24 passos de 15 graus; abaixo de 7% vira ponto (0), 98%+ fecha o anel (24). NaN = -1.</summary>
    public const int RingSteps = 24;
    public static int RingLevel(double percent)
    {
        if (double.IsNaN(percent)) return -1;
        if (percent >= 98) return RingSteps;
        if (percent < 7) return 0;
        return Math.Clamp((int)Math.Round(percent / 100.0 * RingSteps), 1, RingSteps - 1);
    }
}

/// <summary>Faixa com memoria (histerese) para o icone da bandeja.</summary>
public sealed class BandTracker
{
    public Band Current { get; private set; } = Band.NoData;
    public Band Update(double percent) => Current = Bands.Next(Current, percent);
}
