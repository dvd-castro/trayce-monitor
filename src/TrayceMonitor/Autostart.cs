using Microsoft.Win32;

namespace TrayceMonitor;

/// <summary>"Iniciar com o Windows" em HKCU\Software\Microsoft\Windows\CurrentVersion\Run (sem admin).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "TrayceMonitor";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public static bool IsEnabled()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(ValueName) is string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    public static bool Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (on) { if (k.GetValue(ValueName) as string != Command) k.SetValue(ValueName, Command, RegistryValueKind.String); }
            else k.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Line("iniciar com o Windows: " + ex.Message);
            return false;
        }
    }
}
