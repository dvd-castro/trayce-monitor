using System.Runtime.InteropServices;

namespace TrayceMonitor;

/// <summary>
/// Direct2D e DirectWrite pela vtable (sem CsWin32, sem wrappers COM do .NET): so os metodos que o cartao e o painel usam.
/// Indices conferidos contra d2d1.h / dwrite.h (ordem de declaracao).
/// </summary>
internal static unsafe class D2D
{
    [StructLayout(LayoutKind.Sequential)] public struct ColorF { public float r, g, b, a; public ColorF(uint argb, float alpha = 1) { r = ((argb >> 16) & 0xFF) / 255f; g = ((argb >> 8) & 0xFF) / 255f; b = (argb & 0xFF) / 255f; a = alpha; } }
    [StructLayout(LayoutKind.Sequential)] public struct RectF { public float l, t, r, b; public RectF(float l, float t, float r, float b) { this.l = l; this.t = t; this.r = r; this.b = b; } }
    [StructLayout(LayoutKind.Sequential)] public struct Point2F { public float x, y; public Point2F(float x, float y) { this.x = x; this.y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SizeF { public float w, h; }
    [StructLayout(LayoutKind.Sequential)] public struct SizeU { public uint w, h; }
    [StructLayout(LayoutKind.Sequential)] public struct Ellipse { public Point2F p; public float rx, ry; }
    [StructLayout(LayoutKind.Sequential)] public struct RoundedRect { public RectF rect; public float rx, ry; }
    [StructLayout(LayoutKind.Sequential)] public struct ArcSegment { public Point2F point; public SizeF size; public float rotation; public int sweepDirection, arcSize; }
    [StructLayout(LayoutKind.Sequential)] public struct StrokeStyleProps { public int startCap, endCap, dashCap, lineJoin; public float miterLimit; public int dashStyle; public float dashOffset; }
    [StructLayout(LayoutKind.Sequential)] public struct RenderTargetProps { public int type, format, alphaMode; public float dpiX, dpiY; public int usage, minLevel; }
    [StructLayout(LayoutKind.Sequential)] public struct HwndRenderTargetProps { public nint hwnd; public SizeU pixelSize; public int presentOptions; }

    public const int RT_DEFAULT = 0, RT_SOFTWARE = 1, RT_HARDWARE = 2;
    public const int ALPHA_UNKNOWN = 0, ALPHA_PREMULTIPLIED = 1, ALPHA_IGNORE = 3;
    public const int CAP_ROUND = 2;

    private static void** V(void* o) => *(void***)o;

    public static uint Release(void* o) => o == null ? 0 : ((delegate* unmanaged<void*, uint>)V(o)[2])(o);

    public static void* CreateFactory()
    {
        Guid iid = new("06152247-6f50-465a-9245-118bfd3b6007");
        void* f; int hr = Native.D2D1CreateFactory(0 /*single threaded*/, &iid, null, &f);
        if (hr < 0) throw new InvalidOperationException($"D2D1CreateFactory 0x{hr:X8}");
        return f;
    }

    public static void* CreateDWriteFactory()
    {
        Guid iid = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
        void* f; int hr = Native.DWriteCreateFactory(0 /*shared*/, &iid, &f);
        if (hr < 0) throw new InvalidOperationException($"DWriteCreateFactory 0x{hr:X8}");
        return f;
    }

    // ID2D1Factory
    public static void* CreateHwndRenderTarget(void* factory, RenderTargetProps* rtp, HwndRenderTargetProps* hp)
    {
        void* rt; int hr = ((delegate* unmanaged<void*, RenderTargetProps*, HwndRenderTargetProps*, void**, int>)V(factory)[14])(factory, rtp, hp, &rt);
        if (hr < 0) throw new InvalidOperationException($"CreateHwndRenderTarget 0x{hr:X8}");
        return rt;
    }
    public static void* CreatePathGeometry(void* factory)
    {
        void* g; int hr = ((delegate* unmanaged<void*, void**, int>)V(factory)[10])(factory, &g);
        return hr < 0 ? null : g;
    }
    public static void* CreateStrokeStyle(void* factory, StrokeStyleProps* p)
    {
        void* s; int hr = ((delegate* unmanaged<void*, StrokeStyleProps*, float*, uint, void**, int>)V(factory)[11])(factory, p, null, 0, &s);
        return hr < 0 ? null : s;
    }

    // ID2D1RenderTarget
    public static void* CreateBrush(void* rt, ColorF c)
    {
        void* b; int hr = ((delegate* unmanaged<void*, ColorF*, void*, void**, int>)V(rt)[8])(rt, &c, null, &b);
        if (hr < 0) throw new InvalidOperationException($"CreateSolidColorBrush 0x{hr:X8}");
        return b;
    }
    public static void SetColor(void* brush, ColorF c) => ((delegate* unmanaged<void*, ColorF*, void>)V(brush)[8])(brush, &c);
    public static void DrawLine(void* rt, Point2F a, Point2F b, void* brush, float w, void* stroke) => ((delegate* unmanaged<void*, Point2F, Point2F, void*, float, void*, void>)V(rt)[15])(rt, a, b, brush, w, stroke);
    public static void FillRectangle(void* rt, RectF r, void* brush) => ((delegate* unmanaged<void*, RectF*, void*, void>)V(rt)[17])(rt, &r, brush);
    public static void FillRoundedRectangle(void* rt, RoundedRect r, void* brush) => ((delegate* unmanaged<void*, RoundedRect*, void*, void>)V(rt)[19])(rt, &r, brush);
    public static void DrawEllipse(void* rt, Ellipse e, void* brush, float w, void* stroke) => ((delegate* unmanaged<void*, Ellipse*, void*, float, void*, void>)V(rt)[20])(rt, &e, brush, w, stroke);
    public static void FillEllipse(void* rt, Ellipse e, void* brush) => ((delegate* unmanaged<void*, Ellipse*, void*, void>)V(rt)[21])(rt, &e, brush);
    public static void DrawRoundedRectangle(void* rt, RoundedRect r, void* brush, float w) => ((delegate* unmanaged<void*, RoundedRect*, void*, float, void*, void>)V(rt)[18])(rt, &r, brush, w, null);
    public static void FillGeometry(void* rt, void* g, void* brush) => ((delegate* unmanaged<void*, void*, void*, void*, void>)V(rt)[23])(rt, g, brush, null);
    public static void DrawGeometry(void* rt, void* g, void* brush, float w, void* stroke) => ((delegate* unmanaged<void*, void*, void*, float, void*, void>)V(rt)[22])(rt, g, brush, w, stroke);
    public static void DrawText(void* rt, string s, void* format, RectF r, void* brush)
    {
        fixed (char* p = s) ((delegate* unmanaged<void*, char*, uint, void*, RectF*, void*, int, int, void>)V(rt)[27])(rt, p, (uint)s.Length, format, &r, brush, 0, 0);
    }
    public static void SetAntialiasMode(void* rt, int mode) => ((delegate* unmanaged<void*, int, void>)V(rt)[32])(rt, mode);
    public static void Clear(void* rt, ColorF c) => ((delegate* unmanaged<void*, ColorF*, void>)V(rt)[47])(rt, &c);
    public static void BeginDraw(void* rt) => ((delegate* unmanaged<void*, void>)V(rt)[48])(rt);
    public static int EndDraw(void* rt) { ulong a, b; return ((delegate* unmanaged<void*, ulong*, ulong*, int>)V(rt)[49])(rt, &a, &b); }
    public static int Resize(void* hwndRt, SizeU s) => ((delegate* unmanaged<void*, SizeU*, int>)V(hwndRt)[58])(hwndRt, &s);

    // ID2D1PathGeometry / ID2D1GeometrySink
    public static void* Open(void* path) { void* s; int hr = ((delegate* unmanaged<void*, void**, int>)V(path)[17])(path, &s); return hr < 0 ? null : s; }
    public static void BeginFigure(void* sink, Point2F p, int hollow) => ((delegate* unmanaged<void*, Point2F, int, void>)V(sink)[5])(sink, p, hollow);
    public static void AddLines(void* sink, Point2F* pts, uint n) => ((delegate* unmanaged<void*, Point2F*, uint, void>)V(sink)[6])(sink, pts, n);
    public static void EndFigure(void* sink, int closed) => ((delegate* unmanaged<void*, int, void>)V(sink)[8])(sink, closed);
    public static int CloseSink(void* sink) => ((delegate* unmanaged<void*, int>)V(sink)[9])(sink);
    public static void AddArc(void* sink, ArcSegment a) => ((delegate* unmanaged<void*, ArcSegment*, void>)V(sink)[14])(sink, &a);

    // IDWriteFactory / IDWriteTextFormat
    public static void* CreateTextFormat(void* dw, string family, int weight, float size)
    {
        void* f;
        fixed (char* fam = family) fixed (char* loc = "pt-br")
        {
            int hr = ((delegate* unmanaged<void*, char*, void*, int, int, int, float, char*, void**, int>)V(dw)[15])(dw, fam, null, weight, 0, 5, size, loc, &f);
            if (hr < 0) throw new InvalidOperationException($"CreateTextFormat 0x{hr:X8}");
        }
        return f;
    }
    public static void SetTextAlignment(void* tf, int a) => ((delegate* unmanaged<void*, int, int>)V(tf)[3])(tf, a);
    public static void SetParagraphAlignment(void* tf, int a) => ((delegate* unmanaged<void*, int, int>)V(tf)[4])(tf, a);
    public static void SetWordWrapping(void* tf, int w) => ((delegate* unmanaged<void*, int, int>)V(tf)[5])(tf, w);
}
