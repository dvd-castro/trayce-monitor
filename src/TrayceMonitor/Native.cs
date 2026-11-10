using System.Runtime.InteropServices;

namespace TrayceMonitor;

/// <summary>P/Invoke de Win32 usado pela PoC. Tudo blitavel ou com LibraryImport (compativel com NativeAOT).</summary>
internal static unsafe partial class Native
{
    // ---------- mensagens e constantes ----------
    public const uint WM_CREATE = 0x0001, WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_CLOSE = 0x0010,
        WM_ERASEBKGND = 0x0014, WM_SETTINGCHANGE = 0x001A, WM_MOUSEACTIVATE = 0x0021, WM_TIMER = 0x0113,
        WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B, WM_DPICHANGED = 0x02E0, WM_USER = 0x0400,
        WM_APP = 0x8000, WM_NCHITTEST = 0x0084, WM_DISPLAYCHANGE = 0x007E;
    public const uint NIN_SELECT = WM_USER + 0, NIN_KEYSELECT = WM_USER + 1, NIN_BALLOONSHOW = WM_USER + 2,
        NIN_BALLOONHIDE = WM_USER + 3, NIN_BALLOONTIMEOUT = WM_USER + 4, NIN_BALLOONUSERCLICK = WM_USER + 5,
        NIN_POPUPOPEN = WM_USER + 6, NIN_POPUPCLOSE = WM_USER + 7;
    public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETFOCUS = 3, NIM_SETVERSION = 4;
    public const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_STATE = 0x8, NIF_INFO = 0x10,
        NIF_GUID = 0x20, NIF_REALTIME = 0x40, NIF_SHOWTIP = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;
    public const int MA_NOACTIVATE = 3;
    public const uint WS_POPUP = 0x80000000, WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80,
        WS_EX_NOACTIVATE = 0x08000000, WS_OVERLAPPED = 0;
    public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_SHOWNA = 8;
    public const uint SWP_NOACTIVATE = 0x10, SWP_NOZORDER = 0x4, SWP_SHOWWINDOW = 0x40;
    public const int SM_CXSMICON = 49, SM_CYSMICON = 50, SM_CXSCREEN = 0, SM_CYSCREEN = 1,
        SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint GR_GDIOBJECTS = 0, GR_USEROBJECTS = 1;
    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_MOVE = 0x1, MOUSEEVENTF_LEFTDOWN = 0x2, MOUSEEVENTF_LEFTUP = 0x4,
        MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000;
    public const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public readonly bool Contains(POINT p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
        public override readonly string ToString() => $"({Left},{Top},{Right},{Bottom} {Width}x{Height})";
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public POINT pt; public uint lPrivate; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public char* lpszMenuName, lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState, dwStateMask;
        public fixed char szInfo[256];
        public uint uTimeoutOrVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NOTIFYICONIDENTIFIER { public uint cbSize; public nint hWnd; public uint uID; public Guid guidItem; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public nint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public MOUSEINPUT mi; }

    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize, dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS_EX
    {
        public uint cb, PageFaultCount;
        public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
            QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO { public int fIcon; public uint xHotspot, yHotspot; public nint hbmMask, hbmColor; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT { public nint hdc; public int fErase; public RECT rcPaint; public int fRestore, fIncUpdate; public fixed byte rgbReserved[32]; }

    // ---------- user32 ----------
    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW")] public static partial ushort RegisterClassEx(WNDCLASSEXW* wc);
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW")]
    public static partial nint CreateWindowEx(uint exStyle, char* cls, char* name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")] public static partial nint DefWindowProc(nint h, uint m, nint w, nint l);
    [LibraryImport("user32.dll")] public static partial int DestroyWindow(nint h);
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")] public static partial int GetMessage(MSG* m, nint h, uint min, uint max);
    [LibraryImport("user32.dll")] public static partial int TranslateMessage(MSG* m);
    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")] public static partial nint DispatchMessage(MSG* m);
    [LibraryImport("user32.dll")] public static partial void PostQuitMessage(int code);
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")] public static partial int PostMessage(nint h, uint m, nint w, nint l);
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)] public static partial uint RegisterWindowMessage(string s);
    [LibraryImport("user32.dll")] public static partial nuint SetTimer(nint h, nuint id, uint ms, nint proc);
    [LibraryImport("user32.dll")] public static partial int KillTimer(nint h, nuint id);
    [LibraryImport("user32.dll")] public static partial int GetCursorPos(POINT* p);
    [LibraryImport("user32.dll")] public static partial int SetCursorPos(int x, int y);
    [LibraryImport("user32.dll")] public static partial int ShowWindow(nint h, int cmd);
    [LibraryImport("user32.dll")] public static partial int SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);
    [LibraryImport("user32.dll")] public static partial int GetWindowRect(nint h, RECT* r);
    [LibraryImport("user32.dll")] public static partial int IsWindowVisible(nint h);
    [LibraryImport("user32.dll")] public static partial int InvalidateRect(nint h, RECT* r, int erase);
    [LibraryImport("user32.dll")] public static partial nint BeginPaint(nint h, PAINTSTRUCT* ps);
    [LibraryImport("user32.dll")] public static partial int EndPaint(nint h, PAINTSTRUCT* ps);
    [LibraryImport("user32.dll")] public static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")] public static partial nint MonitorFromRect(RECT* r, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")] public static partial int GetMonitorInfo(nint mon, MONITORINFO* mi);
    [LibraryImport("user32.dll")] public static partial int GetSystemMetrics(int i);
    [LibraryImport("user32.dll")] public static partial int GetSystemMetricsForDpi(int i, uint dpi);
    [LibraryImport("user32.dll")] public static partial uint GetDpiForWindow(nint h);
    [LibraryImport("user32.dll")] public static partial uint GetGuiResources(nint proc, uint flags);
    [LibraryImport("user32.dll")] public static partial uint SendInput(uint n, INPUT* inputs, int size);
    [LibraryImport("user32.dll")] public static partial int GetLastInputInfo(LASTINPUTINFO* li);
    [LibraryImport("user32.dll")] public static partial nint CreateIconIndirect(ICONINFO* info);
    [LibraryImport("user32.dll")] public static partial int DestroyIcon(nint icon);
    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")] public static partial nint LoadCursor(nint inst, nint id);
    [LibraryImport("user32.dll")] public static partial nint GetDC(nint h);
    [LibraryImport("user32.dll")] public static partial int ReleaseDC(nint h, nint dc);
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)] public static partial nint FindWindow(string? cls, string? name);
    [LibraryImport("user32.dll")] public static partial nint WindowFromPoint(POINT p);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")] public static partial int GetClassName(nint h, char* buf, int max);
    [LibraryImport("user32.dll")] public static partial nint GetAncestor(nint h, uint flags);

    // ---------- shell32 ----------
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")] public static partial int Shell_NotifyIcon(uint msg, NOTIFYICONDATAW* data);
    [LibraryImport("shell32.dll")] public static partial int Shell_NotifyIconGetRect(NOTIFYICONIDENTIFIER* id, RECT* r);

    // ---------- shcore / dwm ----------
    [LibraryImport("shcore.dll")] public static partial int GetDpiForMonitor(nint mon, int type, uint* dpiX, uint* dpiY);
    [LibraryImport("dwmapi.dll")] public static partial int DwmSetWindowAttribute(nint h, uint attr, void* value, uint size);
    [LibraryImport("dwmapi.dll")] public static partial int DwmFlush();
    public const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34,
        DWMWA_SYSTEMBACKDROP_TYPE = 38;
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int l, r, t, b; }
    [LibraryImport("dwmapi.dll")] public static partial int DwmExtendFrameIntoClientArea(nint h, MARGINS* m);

    // ---------- gdi32 ----------
    [LibraryImport("gdi32.dll")] public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* info, uint usage, void** bits, nint section, uint offset);
    [LibraryImport("gdi32.dll")] public static partial nint CreateBitmap(int w, int h, uint planes, uint bpp, void* bits);
    [LibraryImport("gdi32.dll")] public static partial int DeleteObject(nint o);
    [LibraryImport("gdi32.dll")] public static partial nint CreateCompatibleDC(nint dc);
    [LibraryImport("gdi32.dll")] public static partial int DeleteDC(nint dc);
    [LibraryImport("gdi32.dll")] public static partial nint SelectObject(nint dc, nint o);
    [LibraryImport("gdi32.dll")] public static partial int BitBlt(nint dst, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);

    // D3DKMT (gdi32, modo usuario, sem driver)
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ADAPTERINFO { public uint hAdapter; public uint LuidLow; public int LuidHigh; public uint NumOfSources; public int bPrecisePresentRegionsPreferred; }
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ENUMADAPTERS2 { public uint NumAdapters; public D3DKMT_ADAPTERINFO* pAdapters; }
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_QUERYADAPTERINFO { public uint hAdapter; public int Type; public void* pPrivateDriverData; public uint PrivateDriverDataSize; }
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ADAPTER_PERFDATA
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency, MaxMemoryFrequency, MaxMemoryFrequencyOC, MemoryBandwidth, PCIEBandwidth;
        public uint FanRPM, Power, Temperature;
        public byte PowerStateOverride;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_ADAPTER_PERFDATACAPS
    {
        public uint PhysicalAdapterIndex;
        public ulong MaxMemoryBandwidth, MaxPCIEBandwidth;
        public uint MaxFanRPM, TemperatureMax, TemperatureWarning;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct D3DKMT_CLOSEADAPTER { public uint hAdapter; }
    public const int KMTQAITYPE_ADAPTERPERFDATA = 62, KMTQAITYPE_ADAPTERPERFDATA_CAPS = 63;
    [LibraryImport("gdi32.dll")] public static partial int D3DKMTEnumAdapters2(D3DKMT_ENUMADAPTERS2* e);
    [LibraryImport("gdi32.dll")] public static partial int D3DKMTQueryAdapterInfo(D3DKMT_QUERYADAPTERINFO* q);
    [LibraryImport("gdi32.dll")] public static partial int D3DKMTCloseAdapter(D3DKMT_CLOSEADAPTER* c);

    // ---------- kernel32 / psapi ----------
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")] public static partial nint GetModuleHandle(char* name);
    [LibraryImport("kernel32.dll")] public static partial nint GetCurrentProcess();
    [LibraryImport("kernel32.dll")] public static partial int GetProcessTimes(nint p, long* create, long* exit, long* kernel, long* user);
    [LibraryImport("kernel32.dll")] public static partial int GetSystemTimes(long* idle, long* kernel, long* user);
    [LibraryImport("kernel32.dll")] public static partial int GlobalMemoryStatusEx(MEMORYSTATUSEX* m);
    [LibraryImport("kernel32.dll")] public static partial int GetProcessHandleCount(nint p, uint* count);
    [LibraryImport("kernel32.dll")] public static partial uint GetTickCount();
    [LibraryImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")] public static partial int GetProcessMemoryInfo(nint p, PROCESS_MEMORY_COUNTERS_EX* c, uint cb);
    [LibraryImport("kernel32.dll", EntryPoint = "K32QueryWorkingSet")] public static partial int QueryWorkingSet(nint p, void* buf, uint cb);
    [LibraryImport("kernel32.dll")] public static partial void* VirtualAlloc(void* addr, nuint size, uint type, uint protect);

    // ---------- pdh ----------
    [StructLayout(LayoutKind.Sequential)] public struct PDH_FMT_COUNTERVALUE { public uint CStatus; public uint pad; public double doubleValue; }
    [StructLayout(LayoutKind.Sequential)] public struct PDH_FMT_COUNTERVALUE_ITEM_W { public char* szName; public PDH_FMT_COUNTERVALUE FmtValue; }
    public const uint PDH_FMT_DOUBLE = 0x200, PDH_FMT_NOCAP100 = 0x8000;
    public const uint PDH_MORE_DATA = 0x800007D2;
    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW")] public static partial uint PdhOpenQuery(char* src, nint user, nint* query);
    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)] public static partial uint PdhAddEnglishCounter(nint q, string path, nint user, nint* counter);
    [LibraryImport("pdh.dll")] public static partial uint PdhCollectQueryData(nint q);
    [LibraryImport("pdh.dll")] public static partial uint PdhGetFormattedCounterValue(nint c, uint fmt, uint* type, PDH_FMT_COUNTERVALUE* v);
    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")] public static partial uint PdhGetFormattedCounterArray(nint c, uint fmt, uint* size, uint* count, void* items);
    [LibraryImport("pdh.dll")] public static partial uint PdhCloseQuery(nint q);

    // ---------- d2d / dwrite ----------
    [LibraryImport("d2d1.dll")] public static partial int D2D1CreateFactory(int type, Guid* riid, void* options, void** factory);
    [LibraryImport("dwrite.dll")] public static partial int DWriteCreateFactory(int type, Guid* riid, void** factory);

    // ---------- adicionados no app v1 ----------
    public const uint WM_ACTIVATE = 0x0006, WM_KEYDOWN = 0x0100, WM_COMMAND = 0x0111, WM_NULL = 0x0000,
        WM_SETICON = 0x0080, WM_WTSSESSION_CHANGE = 0x02B1;
    public const int WA_INACTIVE = 0, VK_ESCAPE = 0x1B;
    public const int WTS_SESSION_LOCK = 7, WTS_SESSION_UNLOCK = 8;
    public const uint MF_STRING = 0x0, MF_CHECKED = 0x8, MF_SEPARATOR = 0x800;
    public const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100, TPM_NONOTIFY = 0x80;
    public const uint MB_YESNO = 0x4, MB_ICONINFORMATION = 0x40, MB_SETFOREGROUND = 0x10000;
    public const int IDYES = 6;
    public const uint IMAGE_ICON = 1;
    public const int SW_SHOW = 5;
    public const uint WS_SYSMENU = 0x00080000;
    [LibraryImport("kernel32.dll")] public static partial int CloseHandle(nint h);
    public const int ERROR_ALREADY_EXISTS = 183;
    public const int SM_CXICON = 11;

    [LibraryImport("user32.dll")] public static partial int UpdateWindow(nint h);
    [LibraryImport("user32.dll")] public static partial nint CreatePopupMenu();
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)] public static partial int AppendMenu(nint menu, uint flags, nuint id, string? text);
    [LibraryImport("user32.dll")] public static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);
    [LibraryImport("user32.dll")] public static partial int DestroyMenu(nint menu);
    [LibraryImport("user32.dll")] public static partial int SetForegroundWindow(nint h);
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)] public static partial int MessageBox(nint h, string text, string caption, uint type);
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW")] public static partial nint LoadImage(nint inst, nint name, uint type, int cx, int cy, uint flags);
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")] public static partial nint SendMessage(nint h, uint m, nint w, nint l);
    [LibraryImport("wtsapi32.dll")] public static partial int WTSRegisterSessionNotification(nint h, uint flags);
    [LibraryImport("wtsapi32.dll")] public static partial int WTSUnRegisterSessionNotification(nint h);
    [LibraryImport("shell32.dll", EntryPoint = "ShellExecuteW", StringMarshalling = StringMarshalling.Utf16)] public static partial nint ShellExecute(nint h, string? op, string file, string? args, string? dir, int show);
    [LibraryImport("kernel32.dll", EntryPoint = "CreateMutexW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)] public static partial nint CreateMutex(nint sa, int initialOwner, string name);

    // GetIfTable2 (iphlpapi): MIB_IF_TABLE2 = ULONG NumEntries (+4 de alinhamento) + MIB_IF_ROW2[NumEntries] de 1352 B
    public const int IfRowSize = 1352, IfRowType = 1128, IfRowFlags = 1152, IfRowOperStatus = 1156, IfRowInOctets = 1208, IfRowOutOctets = 1280;
    [LibraryImport("iphlpapi.dll")] public static partial uint GetIfTable2(void** table);
    [LibraryImport("iphlpapi.dll")] public static partial void FreeMibTable(void* table);

    public static int LoWord(nint v) => (int)((long)v & 0xFFFF);
    public static int HiWord(nint v) => (int)(((long)v >> 16) & 0xFFFF);
    public static short SLoWord(nint v) => (short)((long)v & 0xFFFF);
    public static short SHiWord(nint v) => (short)(((long)v >> 16) & 0xFFFF);
}
