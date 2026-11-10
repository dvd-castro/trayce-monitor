using System.Text.Json;
using TrayceMonitor.Core;

namespace TrayceMonitor.Tests;

[TestClass]
public sealed class BandTests
{
    [TestMethod]
    [DataRow(0.0, Band.Normal)]
    [DataRow(69.9, Band.Normal)]
    [DataRow(70.0, Band.High)]
    [DataRow(89.9, Band.High)]
    [DataRow(90.0, Band.Critical)]
    [DataRow(100.0, Band.Critical)]
    [DataRow(double.NaN, Band.NoData)]
    public void Of_uses_entry_thresholds(double v, Band expected) => Assert.AreEqual(expected, Bands.Of(v));

    [TestMethod]
    public void Hysteresis_holds_band_until_five_points_below()
    {
        var t = new BandTracker();
        Assert.AreEqual(Band.Normal, t.Update(50));
        Assert.AreEqual(Band.High, t.Update(72));
        Assert.AreEqual(Band.High, t.Update(66));     // ainda acima de 65
        Assert.AreEqual(Band.Normal, t.Update(64.9)); // saiu
        Assert.AreEqual(Band.Critical, t.Update(95));
        Assert.AreEqual(Band.Critical, t.Update(86));
        Assert.AreEqual(Band.High, t.Update(84));
        Assert.AreEqual(Band.Normal, t.Update(10));
    }

    [TestMethod]
    public void Hysteresis_jumps_straight_up_and_handles_missing_data()
    {
        var t = new BandTracker();
        Assert.AreEqual(Band.Critical, t.Update(99));
        Assert.AreEqual(Band.NoData, t.Update(double.NaN));
        Assert.AreEqual(Band.Normal, t.Update(80 - 15)); // sem memoria depois de NoData
        Assert.AreEqual(Band.High, t.Update(70));
    }

    [TestMethod]
    [DataRow(double.NaN, -1)]
    [DataRow(0.0, 0)]
    [DataRow(6.9, 0)]
    [DataRow(7.0, 2)]
    [DataRow(50.0, 12)]
    [DataRow(97.9, 23)]
    [DataRow(98.0, 24)]
    public void RingLevel_quantizes_in_24_steps(double v, int level) => Assert.AreEqual(level, Bands.RingLevel(v));
}

[TestClass]
public sealed class FormatTests
{
    [TestMethod]
    public void Percent_rounds_clamps_and_shows_dash()
    {
        Assert.AreEqual("12%", Format.Percent(12.4));
        Assert.AreEqual("100%", Format.Percent(130));
        Assert.AreEqual("0%", Format.Percent(-3));
        Assert.AreEqual("—", Format.Percent(double.NaN));
    }

    [TestMethod]
    [DataRow(0.0, "0 B/s")]
    [DataRow(1023.0, "1023 B/s")]
    [DataRow(1024.0, "1 KB/s")]
    [DataRow(512.0 * 1024, "512 KB/s")]
    [DataRow(1.3 * 1048576, "1,3 MB/s")]
    [DataRow(2.0 * 1073741824, "2,0 GB/s")]
    [DataRow(double.NaN, "—")]
    [DataRow(-1.0, "—")]
    public void Rate_picks_unit_with_comma_decimal(double v, string s) => Assert.AreEqual(s, Format.Rate(v));

    [TestMethod]
    public void Memory_temperature_latency()
    {
        Assert.AreEqual("7,9 / 15,9 GB", Format.Memory(7.91, 15.94));
        Assert.AreEqual("—", Format.Memory(1, 0));
        Assert.AreEqual("54 °C", Format.Temperature(54.4));
        Assert.AreEqual("—", Format.Temperature(0));
        Assert.AreEqual("18 ms", Format.Latency(17.6));
        Assert.AreEqual("<1 ms", Format.Latency(0));
        Assert.AreEqual("—", Format.Latency(double.NaN));
    }

    [TestMethod]
    public void Tooltip_fits_szTip_and_shows_latency_only_when_on()
    {
        string off = Format.Tooltip(12, 40, double.NaN, 20, latencyOn: false);
        Assert.AreEqual("Trayce Monitor\nCPU 12% · RAM 40% · GPU —", off);
        StringAssert.Contains(Format.Tooltip(12, 40, 3, 20, latencyOn: true), "Latência 20 ms");
        Assert.IsTrue(Format.Clip(new string('x', 400), Format.TipMax).Length <= Format.TipMax);
    }
}

[TestClass]
public sealed class HistoryTests
{
    [TestMethod]
    public void Keeps_last_60_in_order()
    {
        var h = new History();
        for (int i = 0; i < 75; i++) h.Add(i);
        Assert.AreEqual(60, h.Count);
        Assert.AreEqual(15, h[0]);
        Assert.AreEqual(74, h[59]);
        Assert.AreEqual(74, h.Max());
    }

    [TestMethod]
    public void CounterRate_needs_two_points_and_ignores_resets()
    {
        var r = new CounterRate();
        Assert.IsTrue(double.IsNaN(r.Update(1000, 0)));
        Assert.AreEqual(2048, r.Update(3048, 1000), 1e-9);
        Assert.IsTrue(double.IsNaN(r.Update(10, 2000))); // contador reiniciou
    }
}

[TestClass]
public sealed class SettingsTests
{
    [TestMethod]
    public void Defaults_latency_off_interval_1s()
    {
        var s = Settings.Parse(null);
        Assert.IsFalse(s.LatencyEnabled);
        Assert.IsFalse(s.StartWithWindows);
        Assert.AreEqual(1000, s.IntervalMs);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{not json")]
    [DataRow("[1,2]")]
    [DataRow("null")]
    [DataRow("{\"intervalMs\":\"abc\"}")]
    public void Invalid_json_falls_back_to_defaults(string json)
    {
        var s = Settings.Parse(json);
        Assert.IsFalse(s.LatencyEnabled);
        Assert.AreEqual(1000, s.IntervalMs);
    }

    [TestMethod]
    [DataRow(10, 1000)]
    [DataRow(1500, 1500)]
    [DataRow(999999, 5000)]
    public void Interval_is_clamped(int given, int expected)
        => Assert.AreEqual(expected, Settings.Parse($"{{\"intervalMs\":{given}}}").IntervalMs);

    [TestMethod]
    public void Roundtrip_and_atomic_save_leave_no_temp_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "trayce-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            new Settings { LatencyEnabled = true, StartWithWindows = true }.Save(path);
            new Settings { LatencyEnabled = true, StartWithWindows = false, IntervalMs = 2000 }.Save(path); // sobrescreve
            var back = Settings.Load(path);
            Assert.IsTrue(back.LatencyEnabled);
            Assert.IsFalse(back.StartWithWindows);
            Assert.AreEqual(2000, back.IntervalMs);
            Assert.IsFalse(File.Exists(path + ".tmp"));
            StringAssert.Contains(File.ReadAllText(path), "\"latencyEnabled\": true");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

[TestClass]
public sealed class PaletteTests
{
    private static uint Hex(JsonElement e) => 0xFF000000 | Convert.ToUInt32(e.GetString()!.TrimStart('#'), 16);

    [TestMethod]
    public void Palette_matches_brand_tokens()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tokens.json")));
        var app = doc.RootElement.GetProperty("app");
        var brand = doc.RootElement.GetProperty("brand");
        foreach (var (name, p) in new[] { ("light", Palette.Light), ("dark", Palette.Dark) })
        {
            var t = app.GetProperty(name);
            Assert.AreEqual(Hex(t.GetProperty("windowBg")), p.WindowBg, name + " windowBg");
            Assert.AreEqual(Hex(t.GetProperty("card")), p.Card, name + " card");
            Assert.AreEqual(Hex(t.GetProperty("cardAlt")), p.CardAlt, name + " cardAlt");
            Assert.AreEqual(Hex(t.GetProperty("text")), p.Text, name + " text");
            Assert.AreEqual(Hex(t.GetProperty("text2")), p.Text2, name + " text2");
            Assert.AreEqual(Hex(t.GetProperty("text3")), p.Text3, name + " text3");
            Assert.AreEqual(Hex(brand.GetProperty(name).GetProperty("accent").GetProperty("hex")), p.Accent, name + " accent");
        }
    }
}
