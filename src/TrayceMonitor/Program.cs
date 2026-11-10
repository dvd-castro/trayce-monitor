namespace TrayceMonitor;

internal static class Program
{
    private const string MutexName = @"Local\TrayceMonitor.SingleInstance";

    /// <summary>
    /// Opcoes: --selftest (abre cartao e painel sem mouse, registra em trayce.log e sai; codigo 0 = ok),
    /// --quit (fecha a instancia em execucao).
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--quit"))
        {
            nint h = Native.FindWindow(App.TrayClass, null);
            if (h != 0) Native.PostMessage(h, Native.WM_CLOSE, 0, 0);
            return h != 0 ? 0 : 1;
        }

        nint mutex = Native.CreateMutex(0, 1, MutexName);
        if (mutex == 0 || System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == Native.ERROR_ALREADY_EXISTS)
        {
            if (mutex != 0) Native.CloseHandle(mutex);
            return 3; // ja ha uma instancia rodando
        }
        try { return new App(args.Contains("--selftest")).Run(); }
        catch (Exception ex) { Log.Line("ERRO fatal: " + ex); return 1; }
        finally { Native.CloseHandle(mutex); }
    }
}
