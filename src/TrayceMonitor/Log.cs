using System.Diagnostics;

namespace TrayceMonitor;

/// <summary>Registro minimo em %LOCALAPPDATA%\TrayceMonitor\trayce.log (inicio, erros e autoteste). Reinicia acima de 256 KB.</summary>
internal static class Log
{
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private static readonly object s_lock = new();
    public static double Ms => s_clock.Elapsed.TotalMilliseconds;
    public static string Dir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrayceMonitor");
    private static string FilePath => Path.Combine(Dir, "trayce.log");

    public static void Line(string s)
    {
        try
        {
            lock (s_lock)
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 256 * 1024) fi.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {s}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
