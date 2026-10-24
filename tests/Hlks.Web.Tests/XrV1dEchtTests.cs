using System.Diagnostics;
using System.Globalization;
using Hlks.Kern;
using Hlks.Xr;
using Hlks.Xr.Modell;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App V1d am realen Projekt Test_praxis (nur die lokale, schreibgeschützte Kopie %USERPROFILE%\HLKS_CAD_testdaten\test_praxis; nichts davon ins Repository):
/// Erkundung – Dreiecke nach Grösse der Elemente.
/// </summary>
public class XrV1dEchtTests(ITestOutputHelper aus)
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static string Ordner => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", "test_praxis");

    [Fact]
    public void TestPraxis_Erkundung_Groessen()
    {
        if (!File.Exists(Path.Combine(Ordner, "projekt.json"))) { aus.WriteLine($"Echte Daten fehlen: {Ordner} – Test ohne Prüfung beendet."); return; }
        var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), "xr-v1d-echt", "pc", "mensch");
        var s = XrModell.Stand(XrModell.Erfassen(p));
        var teile = new List<(byte[], string, string, string)>();
        foreach (var g in s.Geschosse)
            foreach (var teil in XrModell.Teile)
            {
                if (teil == XrModell.TeilUnterlage && g.SchluesselUnterlage is null) continue;
                teile.Add((XrModell.Erzeugen(s, g.Geschoss.Id, teil).Daten, g.Geschoss.Id, teil, g.Geschoss.Kennzeichen + " " + teil));
            }
        var sz = ModellSzene.AusGlb(teile);
        foreach (var zelle in new[] { 0.01, 0.02, 0.03, 0.04, 0.06, 0.08 })
        {
            var uhr = Stopwatch.StartNew();
            long vorher = 0, nachher = 0;
            foreach (var n in sz.Netze.Where(x => x.Daten.Gruppe != ModellSzene.GruppeRaum))
            {
                var v = TischStufe.Vereinfachen(n.Daten.Positionen, n.Daten.Normalen, n.Daten.Indizes, zelle);
                vorher += n.Daten.Dreiecke;
                nachher += v.Dreiecke;
            }
            aus.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Zelle {zelle * 100:0} cm: {vorher} → {nachher} Dreiecke in {uhr.ElapsedMilliseconds} ms"));
        }
    }
}
