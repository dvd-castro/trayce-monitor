using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>
/// Icone da bandeja, maquina de estados do cartao, menu e ciclo de vida. Tudo na thread da interface; o amostrador
/// e a latencia rodam em threads proprias e so entregam valores.
/// </summary>
internal sealed unsafe class App : IWindow
{
    public const string TrayClass = "TrayceMonitor.Tray";
    private const uint CallbackMsg = Native.WM_APP + 1, MsgSample = Native.WM_APP + 2, MsgSelfTest = Native.WM_APP + 3;
    private const nuint TimerHover = 2;
    private const double OpenDelayMs = 400, CloseGraceMs = 250, ReleaseAfterMs = 3000, ReopenGuardMs = 350;
    private const uint IconId = 1;
    private const int CmdPanel = 1, CmdLatency = 2, CmdAutostart = 3, CmdAbout = 4, CmdExit = 5;

    private readonly bool _selfTest;
    private readonly string _settingsPath = Path.Combine(Log.Dir, "settings.json");
    private Settings _settings = new();
    private readonly Model _model = new();
    private nint _hwnd, _trayIcon, _appIcon;
    private uint _taskbarCreated;
    private Card _card = null!;
    private Panel _panel = null!;
    private readonly Pinger _pinger = new();
    private Metrics? _metrics;
    private Thread? _sampler;
    private volatile bool _stopSampler;
    private readonly object _snapLock = new();
    private Snapshot _pending = Snapshot.Empty;
    private readonly BandTracker _band = new();
    private int _iconSize = 16, _lastLevel = -2; private Band _lastBand = Band.NoData; private bool _lightBar, _iconAdded;
    private string _version = "1.0.0";

    private enum St { Idle, Pending, Open }
    private St _st;
    private double _t0, _leaveStart, _releaseAt = double.MaxValue, _panelClosedAt = -1e9;
    private bool _suppress, _hoverTimer, _forced;
    private Native.RECT _iconRect;
    public int SelfTestFailures;

    public App(bool selfTest) { _selfTest = selfTest; }

    public int Run()
    {
        var v = typeof(App).Assembly.GetName().Version;
        if (v != null) _version = $"{v.Major}.{v.Minor}.{v.Build}";
        _settings = Settings.Load(_settingsPath);
        bool reg = Autostart.IsEnabled();
        if (reg) Autostart.Set(true); // atualiza o caminho se o exe mudou de lugar
        if (_settings.StartWithWindows != reg) { _settings.StartWithWindows = reg; SaveSettings(); }

        _model.Dark = !(ReadDword("AppsUseLightTheme", 1) == 1);
        _model.LatencyOn = _settings.LatencyEnabled;
        _appIcon = LoadAppIcon();
        _hwnd = Windowing.Create(TrayClass, AppInfo.Name, Native.WS_OVERLAPPED, Native.WS_EX_TOOLWINDOW, _appIcon);
        Windowing.Register(_hwnd, this);
        _card = new Card(_model) { Clicked = () => { CloseCard(); OpenPanel(activate: true); } };
        _panel = new Panel(_model, _version) { Closed = () => _panelClosedAt = Log.Ms };
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        Native.WTSRegisterSessionNotification(_hwnd, 0);
        ReadTheme();
        AddIcon();
        if (_settings.LatencyEnabled) _pinger.Start();
        StartSampler();
        Log.Line($"inicio: {AppInfo.Name} {_version}, icone={(_iconAdded ? "ok" : "FALHOU")}, latencia={_settings.LatencyEnabled}, intervalo={_settings.IntervalMs} ms");
        if (_selfTest) StartSelfTest();

        Native.MSG msg;
        while (Native.GetMessage(&msg, 0, 0, 0) > 0) { Native.TranslateMessage(&msg); Native.DispatchMessage(&msg); }
        Shutdown();
        return SelfTestFailures == 0 ? 0 : 2;
    }

    private void StartSampler()
    {
        int interval = _settings.IntervalMs;
        _sampler = new Thread(() =>
        {
            try { _metrics = new Metrics(); }
            catch (Exception ex) { Log.Line("amostrador: " + ex.Message); return; }
            long next = Environment.TickCount64;
            while (!_stopSampler)
            {
                var s = _metrics.Sample();
                lock (_snapLock) _pending = s;
                Native.PostMessage(_hwnd, MsgSample, 0, 0);
                next += interval;
                long wait = next - Environment.TickCount64;
                if (wait < 0) { next = Environment.TickCount64; wait = interval; }
                for (; wait > 0 && !_stopSampler; wait -= 100) Thread.Sleep((int)Math.Min(100, wait));
            }
            _metrics.Dispose();
        }) { IsBackground = true, Name = "amostrador", Priority = ThreadPriority.BelowNormal };
        _sampler.Start();
    }

    private static nint LoadAppIcon()
    {
        int size = Native.GetSystemMetrics(Native.SM_CXICON);
        return Native.LoadImage(Windowing.Instance, 32512, Native.IMAGE_ICON, size, size, 0);
    }

    private static int ReadDword(string name, int def)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue(name) is int v ? v : def;
        }
        catch (System.Security.SecurityException) { return def; }
    }

    private void ReadTheme()
    {
        _lightBar = ReadDword("SystemUsesLightTheme", 0) == 1;
        nint tray = Native.FindWindow("Shell_TrayWnd", null);
        Native.RECT tr; Native.GetWindowRect(tray, &tr);
        nint mon = Native.MonitorFromRect(&tr, Native.MONITOR_DEFAULTTONEAREST);
        uint dx, dy; Native.GetDpiForMonitor(mon, 0, &dx, &dy);
        _iconSize = Native.GetSystemMetricsForDpi(Native.SM_CXSMICON, dx == 0 ? 96 : dx);
        _lastLevel = -2;
    }

    // ---------------- icone ----------------
    private Native.NOTIFYICONDATAW NewNid() => new() { cbSize = (uint)sizeof(Native.NOTIFYICONDATAW), hWnd = _hwnd, uID = IconId };

    private void AddIcon()
    {
        var nid = NewNid();
        nid.uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP;
        nid.uCallbackMessage = CallbackMsg;
        double cpu = _model.Now.Cpu;
        nid.hIcon = BuildIcon(cpu);
        SetTip(ref nid, Format.Tooltip(cpu, _model.Now.MemPercent, _model.Now.Gpu, _model.InternetMs, _model.LatencyOn));
        _iconAdded = Native.Shell_NotifyIcon(Native.NIM_ADD, &nid) != 0;
        nid.uTimeoutOrVersion = Native.NOTIFYICON_VERSION_4;
        Native.Shell_NotifyIcon(Native.NIM_SETVERSION, &nid);
        ReplaceIcon(nid.hIcon);
    }

    private static void SetTip(ref Native.NOTIFYICONDATAW nid, string s)
    {
        fixed (char* p = nid.szTip) { var span = new Span<char>(p, 128); span.Clear(); s.AsSpan(0, Math.Min(Format.TipMax, s.Length)).CopyTo(span); }
    }

    private nint BuildIcon(double cpu)
    {
        _lastBand = _band.Current;
        _lastLevel = Bands.RingLevel(cpu);
        var px = RingIcon.Render(_iconSize, _lastLevel, _lastBand, _lightBar ? RingPalette.LightBar : RingPalette.DarkBar);
        return RingIcon.ToHIcon(px, _iconSize);
    }

    private void ReplaceIcon(nint hicon)
    {
        if (_trayIcon != 0 && _trayIcon != hicon) Native.DestroyIcon(_trayIcon);
        _trayIcon = hicon;
    }

    private void UpdateIcon()
    {
        double cpu = _model.Now.Cpu;
        _band.Update(cpu);
        var nid = NewNid();
        nid.uFlags = Native.NIF_TIP;
        SetTip(ref nid, Format.Tooltip(cpu, _model.Now.MemPercent, _model.Now.Gpu, _model.InternetMs, _model.LatencyOn));
        if (Bands.RingLevel(cpu) != _lastLevel || _band.Current != _lastBand)
        {
            nid.uFlags |= Native.NIF_ICON;
            nid.hIcon = BuildIcon(cpu);
        }
        Native.Shell_NotifyIcon(Native.NIM_MODIFY, &nid);
        if ((nid.uFlags & Native.NIF_ICON) != 0) ReplaceIcon(nid.hIcon);
    }

    private bool IconRect(out Native.RECT r)
    {
        var id = new Native.NOTIFYICONIDENTIFIER { cbSize = (uint)sizeof(Native.NOTIFYICONIDENTIFIER), hWnd = _hwnd, uID = IconId };
        Native.RECT rr; int hr = Native.Shell_NotifyIconGetRect(&id, &rr);
        r = rr;
        return hr == 0;
    }

    private Native.RECT IconRectOrCursor()
    {
        if (IconRect(out var r)) return r;
        Native.POINT p; Native.GetCursorPos(&p);
        return new Native.RECT { Left = p.X - 8, Top = p.Y - 8, Right = p.X + 8, Bottom = p.Y + 8 };
    }

    // ---------------- mensagens ----------------
    public nint Proc(nint h, uint m, nint w, nint l)
    {
        if (m == CallbackMsg) { OnCallback(Native.LoWord(l), Native.SLoWord(w), Native.SHiWord(w)); return 0; }
        if (m == _taskbarCreated && _taskbarCreated != 0) { ReadTheme(); AddIcon(); return 0; }
        switch (m)
        {
            case Native.WM_TIMER:
                if (w == (nint)TimerHover) OnHoverPoll();
                return 0;
            case MsgSample: OnTick(); return 0;
            case MsgSelfTest: SelfTestStep((int)w); return 0;
            case Native.WM_SETTINGCHANGE:
            case Native.WM_DISPLAYCHANGE:
            case Native.WM_DPICHANGED:
                OnThemeOrDisplay(); return 0;
            case Native.WM_WTSSESSION_CHANGE:
                if ((int)w == Native.WTS_SESSION_LOCK) _pinger.Paused = true;
                else if ((int)w == Native.WTS_SESSION_UNLOCK) _pinger.Paused = false;
                return 0;
            case Native.WM_CLOSE:
                Native.PostQuitMessage(0); return 0;
        }
        return Native.DefWindowProc(h, m, w, l);
    }

    private void OnThemeOrDisplay()
    {
        bool dark = !(ReadDword("AppsUseLightTheme", 1) == 1);
        bool themeChanged = dark != _model.Dark;
        _model.Dark = dark;
        int oldSize = _iconSize; bool oldBar = _lightBar;
        ReadTheme();
        if (oldSize != _iconSize || oldBar != _lightBar)
        {
            var nid = NewNid();
            nid.uFlags = Native.NIF_ICON;
            nid.hIcon = BuildIcon(_model.Now.Cpu);
            Native.Shell_NotifyIcon(Native.NIM_MODIFY, &nid);
            ReplaceIcon(nid.hIcon);
        }
        if (themeChanged) { _card.ApplyTheme(); _panel.ApplyTheme(); }
    }

    private void OnCallback(int ev, int x, int y)
    {
        switch ((uint)ev)
        {
            case Native.WM_MOUSEMOVE:
            case Native.NIN_POPUPOPEN:
                OnHoverSignal(x, y); break;
            case Native.NIN_POPUPCLOSE:
            {
                Native.POINT p; Native.GetCursorPos(&p);
                if (_st == St.Open && !_card.Rect.Contains(p) && _leaveStart == 0) _leaveStart = Log.Ms;
                if (_st == St.Pending) Cancel();
                break;
            }
            case Native.NIN_SELECT:
            case Native.NIN_KEYSELECT:
                if (_st != St.Idle) CloseCard(); else Cancel();
                _suppress = true; StartHover();
                if (_panel.IsOpen) _panel.Close();
                else if (Log.Ms - _panelClosedAt > ReopenGuardMs) OpenPanel(activate: true);
                break;
            case Native.WM_CONTEXTMENU:
                if (_st != St.Idle) CloseCard(); else Cancel();
                _suppress = true; StartHover();
                ShowMenu(x, y);
                break;
        }
    }

    private void OnHoverSignal(int x, int y)
    {
        if (_suppress || _forced || _st != St.Idle || _panel.IsOpen) return;
        _st = St.Pending; _t0 = Log.Ms;
        if (!IconRect(out _iconRect))
        {
            int s = _iconSize; _iconRect = new Native.RECT { Left = x - s, Top = y - s, Right = x + s, Bottom = y + s };
        }
        StartHover();
        _card.Place(_iconRect); // pre-aquecimento: posiciona e cria os recursos durante os 400 ms
    }

    private void StartHover() { if (!_hoverTimer) { Native.SetTimer(_hwnd, TimerHover, 50, 0); _hoverTimer = true; } }
    private void StopHover() { if (_hoverTimer) { Native.KillTimer(_hwnd, TimerHover); _hoverTimer = false; } }

    /// <summary>Plano B: confere o ponteiro (GetCursorPos) contra o icone e o cartao a cada 50 ms, so enquanto preciso.</summary>
    private void OnHoverPoll()
    {
        Native.POINT p; Native.GetCursorPos(&p);
        var ir = _iconRect; ir.Left -= 1; ir.Top -= 1; ir.Right += 1; ir.Bottom += 1;
        bool inIcon = ir.Contains(p), inCard = _card.Visible && _card.Rect.Contains(p);
        double now = Log.Ms;
        if (_suppress)
        {
            if (!IconRect(out var r2) || !r2.Contains(p)) { _suppress = false; if (_st == St.Idle) StopHover(); }
            return;
        }
        switch (_st)
        {
            case St.Pending:
                if (!inIcon) Cancel();
                else if (now - _t0 >= OpenDelayMs) OpenCard();
                break;
            case St.Open:
                if (inIcon || inCard) _leaveStart = 0;
                else if (_leaveStart == 0) _leaveStart = now;
                else if (now - _leaveStart >= CloseGraceMs) CloseCard();
                break;
            default: StopHover(); break;
        }
    }

    private void OpenCard()
    {
        _card.Place(_iconRect);
        _card.Show();
        _st = St.Open; _leaveStart = 0; _releaseAt = double.MaxValue;
        StartHover();
    }

    private void CloseCard()
    {
        _card.Hide();
        _st = St.Idle; _leaveStart = 0; _forced = false;
        _releaseAt = Log.Ms + ReleaseAfterMs;
        if (!_suppress) StopHover();
    }

    private void Cancel()
    {
        if (_st != St.Pending) return;
        _st = St.Idle;
        if (!_suppress) StopHover();
    }

    private void OpenPanel(bool activate)
    {
        _panel.Open(IconRectOrCursor(), _appIcon, activate);
    }

    // ---------------- menu ----------------
    private void ShowMenu(int x, int y)
    {
        nint menu = Native.CreatePopupMenu();
        Native.AppendMenu(menu, Native.MF_STRING, CmdPanel, "Abrir painel");
        Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        Native.AppendMenu(menu, Native.MF_STRING | (_settings.LatencyEnabled ? Native.MF_CHECKED : 0), CmdLatency, "Latência da rede");
        Native.AppendMenu(menu, Native.MF_STRING | (_settings.StartWithWindows ? Native.MF_CHECKED : 0), CmdAutostart, "Iniciar com o Windows");
        Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        Native.AppendMenu(menu, Native.MF_STRING, CmdAbout, "Sobre o Trayce Monitor");
        Native.AppendMenu(menu, Native.MF_STRING, CmdExit, "Sair");
        if (x == 0 && y == 0) { Native.POINT p; Native.GetCursorPos(&p); x = p.X; y = p.Y; }
        Native.SetForegroundWindow(_hwnd);
        int cmd = Native.TrackPopupMenuEx(menu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON | Native.TPM_NONOTIFY, x, y, _hwnd, 0);
        Native.PostMessage(_hwnd, Native.WM_NULL, 0, 0);
        Native.DestroyMenu(menu);
        switch (cmd)
        {
            case CmdPanel: OpenPanel(activate: true); break;
            case CmdLatency: SetLatency(!_settings.LatencyEnabled); break;
            case CmdAutostart:
                bool on = !_settings.StartWithWindows;
                if (Autostart.Set(on)) { _settings.StartWithWindows = on; SaveSettings(); }
                break;
            case CmdAbout: ShowAbout(); break;
            case CmdExit: Native.PostMessage(_hwnd, Native.WM_CLOSE, 0, 0); break;
        }
    }

    private void SetLatency(bool on)
    {
        _settings.LatencyEnabled = on;
        _model.LatencyOn = on;
        if (on) _pinger.Start(); else _pinger.Stop();
        _model.Latency.Clear();
        _model.InternetMs = _model.GatewayMs = double.NaN;
        SaveSettings();
        _panel.Refresh();
    }

    private void ShowAbout()
    {
        string text = $"{AppInfo.Name} {_version}\nMonitor de sistema leve para a bandeja do Windows.\n\n" +
                      $"Licença {AppInfo.License}. Sem administrador, sem driver, sem telemetria.\n{AppInfo.Repo}\n\nAbrir a página do projeto no GitHub?";
        int r = Native.MessageBox(_hwnd, text, $"Sobre o {AppInfo.Name}", Native.MB_YESNO | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND);
        if (r == Native.IDYES) Native.ShellExecute(0, "open", AppInfo.Repo, null, null, Native.SW_SHOW);
    }

    private void SaveSettings()
    {
        try { _settings.Save(_settingsPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Line("configuracao: falha ao gravar: " + ex.Message); }
    }

    // ---------------- tick ----------------
    private void OnTick()
    {
        Snapshot s;
        lock (_snapLock) s = _pending;
        _model.Push(s, _model.LatencyOn ? _pinger.InternetMs : double.NaN, _model.LatencyOn ? _pinger.GatewayMs : double.NaN);
        UpdateIcon();
        _card.Refresh();
        _panel.Refresh();
        if (!_card.Visible && _card.HasResources && Log.Ms >= _releaseAt) { _card.ReleaseResources(); _releaseAt = double.MaxValue; }
    }

    // ---------------- autoteste (sem mouse) ----------------
    private void StartSelfTest()
    {
        new Thread(() =>
        {
            int[] delays = { 3000, 2500, 2500, 1000 };
            for (int i = 0; i < delays.Length; i++) { Thread.Sleep(delays[i]); Native.PostMessage(_hwnd, MsgSelfTest, i + 1, 0); }
        }) { IsBackground = true, Name = "autoteste" }.Start();
    }

    private void Check(bool ok, string what)
    {
        if (!ok) SelfTestFailures++;
        Log.Line($"autoteste: {(ok ? "OK" : "FALHOU")} {what}");
    }

    private void SelfTestStep(int step)
    {
        switch (step)
        {
            case 1:
                Check(_iconAdded, $"icone na bandeja (NIM_ADD), {_iconSize}px, rect {(IconRect(out var r) ? r.ToString() : "indisponivel")}");
                Check(_model.Cpu.Count > 0 && !double.IsNaN(_model.Now.Cpu), $"amostras: {_model.Cpu.Count}, CPU {Format.Percent(_model.Now.Cpu)}, RAM {Format.Percent(_model.Now.MemPercent)}, GPU {Format.Percent(_model.Now.Gpu)} {Format.Temperature(_model.Now.GpuTempC)}, disco L {Format.Rate(_model.Now.DiskReadBps)} E {Format.Rate(_model.Now.DiskWriteBps)}, rede ↓ {Format.Rate(_model.Now.NetRxBps)} ↑ {Format.Rate(_model.Now.NetTxBps)}");
                nint fg = Native.GetForegroundWindow();
                _forced = true;
                _iconRect = IconRectOrCursor();
                _card.Place(_iconRect); _card.Show(); _st = St.Open;
                Native.UpdateWindow(_card.Hwnd);
                Check(Native.IsWindowVisible(_card.Hwnd) != 0 && _card.HasResources, $"cartao aberto em {_card.Rect}");
                Check(Native.GetForegroundWindow() == fg, "cartao nao roubou o foco");
                break;
            case 2:
                CloseCard();
                Check(Native.IsWindowVisible(_card.Hwnd) == 0, "cartao fechado");
                OpenPanel(activate: false);
                Native.UpdateWindow(_panel.Hwnd);
                Check(_panel.IsOpen && Native.IsWindowVisible(_panel.Hwnd) != 0, "painel aberto");
                break;
            case 3:
                _panel.Close();
                Check(!_panel.IsOpen, "painel fechado");
                break;
            case 4:
                Log.Line($"autoteste: fim, {SelfTestFailures} falha(s)");
                Native.PostMessage(_hwnd, Native.WM_CLOSE, 0, 0);
                break;
        }
    }

    private void Shutdown()
    {
        _stopSampler = true; _sampler?.Join(3000);
        _pinger.Dispose();
        StopHover();
        Native.WTSUnRegisterSessionNotification(_hwnd);
        var nid = NewNid();
        Native.Shell_NotifyIcon(Native.NIM_DELETE, &nid);
        if (_trayIcon != 0) Native.DestroyIcon(_trayIcon);
        _panel.Close();
        _card.ReleaseResources();
        Native.DestroyWindow(_card.Hwnd);
        Native.DestroyWindow(_hwnd);
        if (_appIcon != 0) Native.DestroyIcon(_appIcon);
        Log.Line("fim");
    }
}
