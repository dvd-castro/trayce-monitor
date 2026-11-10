namespace TrayceMonitor.Core;

/// <summary>
/// Cores da interface. Superficies e texto espelham assets/brand/tokens.json (app.light/app.dark e brand.*);
/// um teste confere que os valores batem com o arquivo. Estados do anel (ok/warn/crit) vem da spec v2.2, 4.2.
/// ARGB.
/// </summary>
public sealed record Palette(
    uint WindowBg, uint Card, uint CardAlt, uint Text, uint Text2, uint Text3, uint Accent,
    uint Ok, uint Warn, uint Crit, uint Track, uint Mute)
{
    public static readonly Palette Light = new(
        WindowBg: 0xFFF3F3F3, Card: 0xFFFFFFFF, CardAlt: 0xFFF7F7F7, Text: 0xFF1A1A1A, Text2: 0xFF5C5C5C, Text3: 0xFF6B6B6B,
        Accent: 0xFF005FB8, Ok: 0xFF13803F, Warn: 0xFF9A5B00, Crit: 0xFFC42B31, Track: 0xFFCFCFCF, Mute: 0xFF767676);

    public static readonly Palette Dark = new(
        WindowBg: 0xFF202020, Card: 0xFF2C2C2C, CardAlt: 0xFF323232, Text: 0xFFFFFFFF, Text2: 0xFFC8C8C8, Text3: 0xFFA6A6A6,
        Accent: 0xFF60CDFF, Ok: 0xFF4CD27A, Warn: 0xFFFFC53D, Crit: 0xFFFF6B63, Track: 0xFF444444, Mute: 0xFF8E8E8E);

    public uint ForBand(Band b) => b switch { Band.Critical => Crit, Band.High => Warn, Band.Normal => Ok, _ => Mute };
}

/// <summary>Paleta do anel conforme a cor da BARRA (nao a dos apps).</summary>
public readonly record struct RingPalette(uint Ok, uint Warn, uint Crit, uint Track, uint Mute)
{
    public static readonly RingPalette LightBar = new(0xFF13803F, 0xFF9A5B00, 0xFFC42B31, 0xFFCFCFCF, 0xFF767676);
    public static readonly RingPalette DarkBar = new(0xFF4CD27A, 0xFFFFC53D, 0xFFFF6B63, 0xFF444444, 0xFF8E8E8E);
}

public static class AppInfo
{
    public const string Name = "Trayce Monitor";
    public const string Repo = "https://github.com/dvd-castro/trayce-monitor";
    public const string License = "MIT";
}
