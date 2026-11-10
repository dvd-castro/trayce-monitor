using TrayceMonitor.Core;

namespace TrayceMonitor;

/// <summary>
/// Anel vetorial (grade de 32 x 32 unidades) rasterizado por cobertura analitica com supersampling 8 x 8 no tamanho
/// EXATO pedido (SM_CXSMICON do DPI da barra). Normal = anel liso; Alta = anel com ponto; Critica = disco cheio
/// (com "!" vazado a partir de 24 px); sem dado = trilho tracejado. Saida BGRA com alfa direto, linha 0 em cima.
/// </summary>
internal static class RingIcon
{
    private const float C = 16f, RTrack = 11.5f, Half = 2.5f;

    public static uint[] Render(int size, int level, Band band, RingPalette pal)
    {
        var px = new uint[size * size];
        const int ss = 8;
        float scale = 32f / size;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float a = 0, r = 0, g = 0, b = 0;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
            {
                float u = (x + (sx + 0.5f) / ss) * scale, v = (y + (sy + 0.5f) / ss) * scale;
                uint c = Sample(u, v, size, level, band, pal);
                float ca = (c >> 24) / 255f;
                if (ca <= 0) continue;
                a += ca; r += ((c >> 16) & 0xFF) * ca; g += ((c >> 8) & 0xFF) * ca; b += (c & 0xFF) * ca;
            }
            if (a <= 0) continue;
            uint A = (uint)MathF.Round(a / (ss * ss) * 255f);
            uint R = (uint)MathF.Round(r / a), G = (uint)MathF.Round(g / a), B = (uint)MathF.Round(b / a);
            px[y * size + x] = (A << 24) | (R << 16) | (G << 8) | B;
        }
        return px;
    }

    private static uint Sample(float u, float v, int size, int level, Band band, RingPalette pal)
    {
        float dx = u - C, dy = v - C, d = MathF.Sqrt(dx * dx + dy * dy);
        if (band == Band.Critical)
        {
            if (d > 14f) return 0;
            if (size >= 24 && (Capsule(u, v, C, 11f, C, 16f, 1.4f) || Dist(u, v, C, 19.6f) <= 1.4f)) return 0;
            return pal.Crit;
        }
        bool inTrack = MathF.Abs(d - RTrack) <= Half;
        float ang = MathF.Atan2(dx, -dy) * (180f / MathF.PI);
        if (ang < 0) ang += 360f;
        if (band == Band.NoData) return inTrack && (ang % 30f) < 20f ? pal.Mute : 0;
        uint arc = band == Band.High ? pal.Warn : pal.Ok;
        if (band == Band.High && d <= 3.6f) return pal.Warn;
        if (level >= 0)
        {
            float sweep = level * (360f / Bands.RingSteps);
            if (level >= Bands.RingSteps) { if (inTrack) return arc; }
            else
            {
                if (inTrack && ang <= sweep) return arc;
                if (Dist(u, v, C, C - RTrack) <= Half) return arc;
                float er = sweep * MathF.PI / 180f;
                if (Dist(u, v, C + RTrack * MathF.Sin(er), C - RTrack * MathF.Cos(er)) <= Half) return arc;
            }
        }
        return inTrack ? pal.Track : 0;
    }

    private static float Dist(float u, float v, float x, float y) => MathF.Sqrt((u - x) * (u - x) + (v - y) * (v - y));

    private static bool Capsule(float u, float v, float x0, float y0, float x1, float y1, float r)
    {
        float vx = x1 - x0, vy = y1 - y0, t = Math.Clamp(((u - x0) * vx + (v - y0) * vy) / (vx * vx + vy * vy), 0f, 1f);
        return Dist(u, v, x0 + t * vx, y0 + t * vy) <= r;
    }

    /// <summary>HICON 32 bpp com alfa. Quem chama destroi com DestroyIcon.</summary>
    public static unsafe nint ToHIcon(uint[] pixels, int size)
    {
        var header = new Native.BITMAPINFOHEADER { biSize = (uint)sizeof(Native.BITMAPINFOHEADER), biWidth = size, biHeight = -size, biPlanes = 1, biBitCount = 32 };
        void* bits;
        nint color = Native.CreateDIBSection(0, &header, 0, &bits, 0, 0);
        if (color == 0) return 0;
        pixels.AsSpan().CopyTo(new Span<uint>(bits, pixels.Length));
        nint mask = Native.CreateBitmap(size, size, 1, 1, null);
        var info = new Native.ICONINFO { fIcon = 1, hbmColor = color, hbmMask = mask };
        nint icon = Native.CreateIconIndirect(&info);
        Native.DeleteObject(color);
        Native.DeleteObject(mask);
        return icon;
    }
}
