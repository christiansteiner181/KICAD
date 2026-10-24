using System.Diagnostics;
using System.Globalization;
using Hlks.Kern;
using Hlks.Xr;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Tests;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App V1d am realen Projekt Test_praxis (nur die lokale, schreibgeschützte Kopie %USERPROFILE%\HLKS_CAD_testdaten\test_praxis; nichts davon ins Repository;
/// ohne die Kopie kehren die Tests ohne Prüfung zurück): die GLB wie der Server (XrModell), die Szene wie die App (ModellSzene).
///   - Tischstufe: Dreiecke der vereinfachten Unterlage je Stufe und die Stufe, die der Tisch bei «Alle Etagen» mit Unterlage bei 1:19, 1:50 und 1:200 zeichnet.
///   - Fang vorher/nachher: der Fang bis V1a (FangReferenz, lineare Suche) gegen den Fang mit Index (SzenenFang) – gleiche Ergebnisse, Zeit je Abfrage am Tisch (1:50)
///     und beim Begehen. Nur Zahlen in der Ausgabe.
/// </summary>
public class XrV1dEchtTests(ITestOutputHelper aus)
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static string Ordner => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", "test_praxis");
    static ModellSzene? szene;

    static ModellSzene? Szene()
    {
        if (!File.Exists(Path.Combine(Ordner, "projekt.json"))) return null;
        if (szene is not null) return szene;
        var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), "xr-v1d-echt", "pc", "mensch");
        var s = XrModell.Stand(XrModell.Erfassen(p));
        var teile = new List<(byte[], string, string, string)>();
        foreach (var g in s.Geschosse)
            foreach (var teil in XrModell.Teile)
            {
                if (teil == XrModell.TeilUnterlage && g.SchluesselUnterlage is null) continue;
                teile.Add((XrModell.Erzeugen(s, g.Geschoss.Id, teil).Daten, g.Geschoss.Id, teil, g.Geschoss.Kennzeichen + " " + teil));
            }
        return szene = ModellSzene.AusGlb(teile);
    }

    static string Z(FormattableString f) => FormattableString.Invariant(f);

    [Fact]
    public void TestPraxis_Tischstufe_Alle_Etagen_unter_dem_Budget()
    {
        if (Szene() is not { } sz) { aus.WriteLine($"Echte Daten fehlen: {Ordner} – Test ohne Prüfung beendet."); return; }
        var netze = sz.Netze.Where(n => n.Daten.Gruppe != ModellSzene.GruppeRaum && n.Daten.Dreiecke > 0).ToList();
        var je = new long[TischStufe.ZellenM.Count];
        var uhr = Stopwatch.StartNew();
        foreach (var n in netze)
            for (int k = 0; k < je.Length; k++)
                je[k] += k == 0 || !TischStufe.Vereinfacht(n.Teil) ? n.Daten.Dreiecke : TischStufe.Vereinfachen(n.Daten.Positionen, n.Daten.Normalen, n.Daten.Indizes, TischStufe.ZellenM[k]).Dreiecke;
        aus.WriteLine(Z($"Alle Etagen mit Unterlage: Dreiecke je Stufe {string.Join(" / ", je)} (Zellen {string.Join(", ", TischStufe.ZellenM.Select(z => z * 100))} cm), gerechnet in {uhr.ElapsedMilliseconds} ms"));
        foreach (var n in new[] { 19.0, 50, 200 })
        {
            var s = TischStufe.StufeWaehlen(je, n);
            aus.WriteLine(Z($"1:{n}: Stufe {s} ({TischStufe.ZellenM[s] * 100} cm, {TischStufe.ZellenM[s] * 1000 / n:0.00} mm auf dem Tisch), {je[s]} Dreiecke"));
            Assert.True(je[s] <= TischStufeEinstellungen.Vorgabe.DreieckeMax, $"1:{n}");
        }
        Assert.True(je[1] < je[0] / 2);
    }

    [Fact]
    public void TestPraxis_Fang_mit_Index_gleich_und_schneller_als_bis_V1a()
    {
        if (Szene() is not { } sz) { aus.WriteLine($"Echte Daten fehlen: {Ordner} – Test ohne Prüfung beendet."); return; }
        var neu = new SzenenFang(sz);
        var uhr = Stopwatch.StartNew();
        neu.Aufbauen();
        var (st, pu, sr) = neu.Umfang;
        aus.WriteLine(Z($"Index: {uhr.ElapsedMilliseconds} ms ({st} Stücke, {pu} Punkte, {sr} Strecken, {neu.Aufbereitet} Elemente)"));
        var alt = new FangReferenz(sz);
        var box = sz.Box;
        var zufall = new Random(17);
        Vec3 Im() => new(box.Min.X + zufall.NextDouble() * (box.Max.X - box.Min.X), box.Min.Y + zufall.NextDouble() * (box.Max.Y - box.Min.Y), box.Min.Z + zufall.NextDouble() * (box.Max.Z - box.Min.Z));
        var mitte = Vec3.Mitte(box.Min, box.Max);
        var groesse = (box.Max - box.Min).Laenge;
        // am Tisch 1:50 (Fangradien im Massstab, Strahl 4 m im Raum = 200 m in der Szene) vom Platz vor dem Modell, und beim Begehen (3 cm, 30 m) aus dem Gebäude
        const double n = 50;
        var auge = mitte + new Vec3(0, groesse * 0.6, groesse * 0.8);
        var faelle = new List<(string Art, Func<SzenenFang, FangErgebnis> Neu, Func<FangReferenz, FangErgebnis> Alt)>();
        for (int i = 0; i < 150; i++)
        {
            var ziel = Im();
            faelle.Add(("Tisch Strahl", f => f.Strahl(auge, ziel - auge, 0.03 * n, 0.03, 4 * n), f => f.Strahl(auge, ziel - auge, 0.03 * n, 0.03, 4 * n)));
            var p = Im();
            faelle.Add(("Tisch Finger", f => f.Punkt(p, 0.03 * n), f => f.Punkt(p, 0.03 * n)));
            var u = Im();
            var r = new Vec3(zufall.NextDouble() - 0.5, (zufall.NextDouble() - 0.5) * 0.3, zufall.NextDouble() - 0.5);
            faelle.Add(("Begehen Strahl", f => f.Strahl(u, r), f => f.Strahl(u, r)));
        }
        var zeiten = faelle.Select(x => x.Art).Distinct().ToDictionary(a => a, _ => (Alt: new List<double>(), Neu: new List<double>()));
        int gleich = 0;
        foreach (var (art, fn, fa) in faelle)
        {
            var u1 = Stopwatch.StartNew();
            var a = fa(alt);
            zeiten[art].Alt.Add(u1.Elapsed.TotalMilliseconds);
            var u2 = Stopwatch.StartNew();
            var b = fn(neu);
            zeiten[art].Neu.Add(u2.Elapsed.TotalMilliseconds);
            Assert.True(a.Art == b.Art && a.Id == b.Id, $"{art}: {a.Art}/{a.Id} ≠ {b.Art}/{b.Id}");
            if (a.P is { } pa && b.P is { } pb) Assert.True(pa.Abstand(pb) < 1e-6, art);
            gleich++;
        }
        static double P(List<double> l, double q) { var s = l.Order().ToList(); return s[Math.Min(s.Count - 1, (int)(q * s.Count))]; }
        foreach (var (art, (za, zn)) in zeiten)
            aus.WriteLine(Z($"{art}: bis V1a ⌀ {za.Average():0.000} ms, 99 % unter {P(za, 0.99):0.00} ms, höchstens {za.Max():0.00} ms; mit Index ⌀ {zn.Average():0.000} ms, 99 % unter {P(zn, 0.99):0.000} ms, höchstens {zn.Max():0.000} ms (die erste {zn[0]:0.000} ms; {zn.Count} Abfragen)"));
        // die Abfragen noch einmal (warm, ohne die ersten Aufrufe): so verteilt sich die Zeit im Betrieb
        foreach (var art in zeiten.Keys)
        {
            var warm = new List<double>();
            foreach (var (a2, fn, _) in faelle.Where(x => x.Art == art)) { var uw = Stopwatch.StartNew(); fn(neu); warm.Add(uw.Elapsed.TotalMilliseconds); }
            aus.WriteLine(Z($"{art} warm: mit Index ⌀ {warm.Average():0.000} ms, 99 % unter {P(warm, 0.99):0.000} ms, höchstens {warm.Max():0.000} ms"));
        }
        aus.WriteLine($"{gleich} Abfragen mit gleichem Ergebnis; bis V1a {alt.Aufbereitet} Elemente aufbereitet (Zwischenspeicher 400)");
        // Zeiten nur im Vergleich prüfen: die absoluten Zahlen hängen von der Last am Rechner ab (sie stehen in der Ausgabe; unbelastet am PC
        // ⌀ unter 0,2 ms und höchstens rund 2 ms je Abfrage am Tisch)
        Assert.True(zeiten.Values.All(z => z.Neu.Average() < z.Alt.Average()), "im Mittel schneller als bis V1a");
        Assert.True(zeiten.Values.All(z => P(z.Neu, 0.99) < P(z.Alt, 0.99)), "auch die Spitzen (99 %) tiefer als bis V1a");
    }
}
