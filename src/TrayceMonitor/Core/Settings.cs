using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayceMonitor.Core;

/// <summary>Configuracao persistida em %LOCALAPPDATA%\TrayceMonitor\settings.json.</summary>
public sealed class Settings
{
    public const int DefaultIntervalMs = 1000, MinIntervalMs = 1000, MaxIntervalMs = 5000;

    /// <summary>Latencia ICMP: desligada por padrao.</summary>
    public bool LatencyEnabled { get; set; }
    public bool StartWithWindows { get; set; }
    public int IntervalMs { get; set; } = DefaultIntervalMs;

    /// <summary>Corrige valores fora da faixa. Devolve true se algo foi corrigido.</summary>
    public bool Normalize()
    {
        int fixedInterval = Math.Clamp(IntervalMs, MinIntervalMs, MaxIntervalMs);
        bool changed = fixedInterval != IntervalMs;
        IntervalMs = fixedInterval;
        return changed;
    }

    public Settings Clone() => new() { LatencyEnabled = LatencyEnabled, StartWithWindows = StartWithWindows, IntervalMs = IntervalMs };

    /// <summary>Le o JSON; vazio, invalido ou de outro tipo = padroes. Nunca lanca.</summary>
    public static Settings Parse(string? json)
    {
        Settings? s = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try { s = JsonSerializer.Deserialize(json, SettingsJson.Default.Settings); }
            catch (JsonException) { s = null; }
            catch (NotSupportedException) { s = null; }
        }
        s ??= new Settings();
        s.Normalize();
        return s;
    }

    public string ToJson() => JsonSerializer.Serialize(this, SettingsJson.Default.Settings);

    /// <summary>Gravacao atomica: arquivo temporario no mesmo diretorio, flush, e troca por MoveFileEx(REPLACE_EXISTING).</summary>
    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new StreamWriter(fs))
        {
            w.Write(ToJson());
            w.Flush();
            fs.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    public static Settings Load(string path)
    {
        try { return Parse(File.Exists(path) ? File.ReadAllText(path) : null); }
        catch (IOException) { return Parse(null); }
        catch (UnauthorizedAccessException) { return Parse(null); }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
