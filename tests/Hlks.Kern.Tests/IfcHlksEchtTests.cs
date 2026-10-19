using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abnahme HLKS-Übertrag (Modul 1b, B2) mit den echten Dateien aus %USERPROFILE%\HLKS_CAD_testdaten (nur lokal,
/// nie im Repo): BKW Hauptsitz (Trimble Nova, IFC4: Heizung/Kälte, Sanitär, Lüftung) und Bernstrasse 42 (H+K Revit 2025,
/// IFC2x3) samt Abgleich der Fassung 2024-09 → 2026-02. Fehlt eine Datei, kehrt der Test früh zurück. Nur Bereich «hlks»
/// (die Revit-Datei enthält verknüpfte Architektur) und ohne Hintergrund-Netz, damit die Dauer den HLKS-Teil misst.</summary>
public sealed class IfcHlksEchtTests(ITestOutputHelper aus)
{
    sealed record Zahlen(int Leitungen, int Formteile, int Armaturen, int Apparate, int Verbindungen, int Anschluesse, int Verbunden,
        int Systeme, int IfcSysteme, int IfcSystemeMitTyp, TimeSpan Dauer, Dictionary<string, int> JeKlasse)
    {
        public double Anteil => Anschluesse == 0 ? 0 : (double)Verbunden / Anschluesse;
    }

    static JsonObject NurHlks() => new() { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false };

    (Projekt P, BefehlsErgebnis E, Zahlen Z) Uebertragen(string datei, Projekt? p = null)
    {
        p ??= Leer();
        var uhr = Stopwatch.StartNew();
        var e = Ok(Uebernehmen(p, datei, NurHlks()));
        uhr.Stop();
        var ul = p.Modell.DerKlasse("Unterlage").Single(u => Path.GetFileName(u.Text("original")!) == Path.GetFileName(datei));
        var meine = p.Modell.Elemente.Where(x => x.Kennungen?.GetValueOrDefault("quelle") == ul.Id).ToList();
        int n(string basis) => meine.Count(x => p.Katalog.IstArt(x, basis));
        var mitAnschluss = meine.Where(x => x.Anschluesse.Count > 0).ToList();
        int anschluesse = mitAnschluss.Sum(x => x.Anschluesse.Count);
        int verbunden = mitAnschluss.Sum(x => x.Anschluesse.Count(a => p.Modell.VerbindungAn(x.Id, a.Name) is not null));
        var hlks = e.Hinweise.First(h => h.StartsWith("HLKS:"));
        var sys = Regex.Match(hlks, @"Systeme: (\d+) in der Datei, (\d+) mit Systemtyp");
        var z = new Zahlen(n("Leitung"), n("Formteil"), n("Armatur"), meine.Count(x => Netz.IstNetzgeraet(p.Katalog, x)),
            p.Modell.Verbindungen.Count, anschluesse, verbunden, meine.Count(x => x.Klasse == "System"),
            int.Parse(sys.Groups[1].Value), int.Parse(sys.Groups[2].Value), uhr.Elapsed,
            meine.GroupBy(x => x.Klasse).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()));
        aus.WriteLine($"{Path.GetFileName(datei)}: {z.Leitungen} Leitungen, {z.Formteile} Formteile, {z.Armaturen} Armaturen, {z.Apparate} Apparate/Geräte; " +
                      $"{z.Verbindungen} Verbindungen; Anschlüsse {z.Anschluesse}, verbunden {z.Verbunden} ({z.Anteil:P1}); " +
                      $"IFC-Systeme {z.IfcSysteme}, davon mit Systemtyp {z.IfcSystemeMitTyp} → {z.Systeme} Systeme; Dauer {z.Dauer.TotalSeconds:0.0} s");
        aus.WriteLine("  " + string.Join(", ", z.JeKlasse.Select(k => $"{k.Key} {k.Value}")));
        foreach (var h in e.Hinweise.Take(40)) aus.WriteLine("  - " + h);
        return (p, e, z);
    }

    static void Aufraeumen(Projekt p)
    {
        try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { }
    }

    [Fact]
    public void BkwHauptsitz_HeizungKaelte_Nova()
    {
        if (Echt(@"bkw_hauptsitz\BKW-HS_1_HK-GEBT_NOV.ifc") is not { } datei) return;
        var (p, _, z) = Uebertragen(datei);
        try
        {
            Assert.Equal(1521, z.JeKlasse["Rohr"]);
            Assert.Equal(238, z.JeKlasse["Ventil"]);
            Assert.Equal(32, z.JeKlasse["Pumpe"]);
            Assert.Equal(13, z.JeKlasse["Wasserzaehler"]);
            Assert.True(z.Formteile >= 1890, $"Formteile {z.Formteile}");
            Assert.True(z.Verbindungen >= 3400, $"Verbindungen {z.Verbindungen}");
            Assert.True(z.Anteil >= 0.85, $"Anteil {z.Anteil:P1}");
            Assert.True(z.Systeme >= 4);
            // Nennweite aus Pset_PipeSegmentTypeCommon.NominalDiameter (Nova schreibt sie als IfcReal)
            Assert.All(p.Modell.DerKlasse("Rohr"), r => Assert.Contains("NominalDiameter", r.Eigenschaft("dn")!.Herkunft.Quelle));
            Assert.True(z.Dauer < TimeSpan.FromSeconds(60), $"Dauer {z.Dauer}");
        }
        finally { Aufraeumen(p); }
    }

    [Fact]
    public void BkwHauptsitz_Sanitaer_Nova()
    {
        if (Echt(@"bkw_hauptsitz\BKW-HS_1_SA-GEBT_NOV.ifc") is not { } datei) return;
        var (p, _, z) = Uebertragen(datei);
        try
        {
            Assert.Equal(99, z.JeKlasse["Rohr"]);
            Assert.True(z.Formteile >= 100, $"Formteile {z.Formteile}");
            Assert.True(z.Verbindungen >= 170, $"Verbindungen {z.Verbindungen}");
            Assert.True(z.Systeme >= 5);
            var sw = p.Modell.DerKlasse("System").Single(s => s.Text("systemtyp") == "Schmutzwasser");
            Assert.True(p.Modell.Elemente.Count(x => x.System == sw.Id) >= 120);
        }
        finally { Aufraeumen(p); }
    }

    [Fact]
    public void BkwHauptsitz_Lueftung_Nova_KanalQuerschnittAusDemKoerper()
    {
        if (Echt(@"bkw_hauptsitz\BKW-HS_1_LU-GEBT_NOV.ifc") is not { } datei) return;
        var (p, _, z) = Uebertragen(datei);
        try
        {
            Assert.Equal(75, z.JeKlasse["Kanal"]);
            Assert.All(p.Modell.DerKlasse("Kanal"), k => Assert.True(k.Zahl("durchmesser") is not null || (k.Zahl("breite") is not null && k.Zahl("hoehe") is not null), k.Name));
            // Steigkanal 550 × 150 (Pset nennt NominalDiameterOrWidth 550, NominalHeight 150)
            Assert.Contains(p.Modell.DerKlasse("Kanal"), k => k.Zahl("breite") == 550 && k.Zahl("hoehe") == 150);
            Assert.True(z.Verbindungen >= 120, $"Verbindungen {z.Verbindungen}");
            Assert.Equal(4, z.Systeme);   // Abluft, Fortluft, Aussenluft, Zuluft; «Abgas» hat keinen Systemtyp
        }
        finally { Aufraeumen(p); }
    }

    [Fact]
    public void Bernstrasse42_Revit2025_Ifc2x3()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } datei) return;
        var (p, _, z) = Uebertragen(datei);
        try
        {
            Assert.Equal(1634, z.JeKlasse["Rohr"]);
            Assert.Equal(318, z.JeKlasse["Kanal"]);   // inkl. 9 Flexkanäle (FLEXIBLESEGMENT, importWerte seit 17.10.2026)
            Assert.True(z.Formteile >= 1500, $"Formteile {z.Formteile}");
            Assert.True(z.Verbindungen >= 3400, $"Verbindungen {z.Verbindungen}");
            Assert.True(z.Anteil >= 0.9, $"Anteil {z.Anteil:P1}");
            Assert.Equal(195, z.IfcSystemeMitTyp);
            Assert.Equal(14, z.Systeme);   // Revit-Teile mit gleichem Basisnamen zusammengefasst
            // Revit 2025 schreibt keine Nennweite in Psets: keine geratene DN aus dem Profil
            Assert.All(p.Modell.DerKlasse("Rohr"), r => Assert.Null(r.Eigenschaft("dn")));
            Assert.All(p.Modell.DerKlasse("Kanal"), k => Assert.True(k.Zahl("durchmesser") is not null || k.Zahl("breite") is not null));
            // Architektur-Objekte der verknüpften Datei (DirectShape ohne Ports) werden keine Apparate
            Assert.Empty(p.Modell.DerKlasse("Waschmaschine"));
            Assert.Empty(p.Modell.DerKlasse("Kontrollschacht"));
            Assert.Equal(10, z.JeKlasse["WC"]);
            Assert.True(z.Dauer < TimeSpan.FromSeconds(120), $"Dauer {z.Dauer}");
        }
        finally { Aufraeumen(p); }
    }

    [Fact]
    public void Bernstrasse42_Abgleich_2024_09_nach_2026_02()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2024-09-17.ifc") is not { } alt || Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } neu) return;
        // Neue Fassung derselben Unterlage = gleicher Dateiname
        var o = Path.Combine(Path.GetTempPath(), "hlks_b2_echt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(o, "alt")); Directory.CreateDirectory(Path.Combine(o, "neu"));
        var a = Path.Combine(o, "alt", "hlks_hk.ifc"); var b = Path.Combine(o, "neu", "hlks_hk.ifc");
        File.Copy(alt, a); File.Copy(neu, b);
        Projekt? p = null;
        try
        {
            (p, _, var z1) = Uebertragen(a);
            var (_, e2, z2) = Uebertragen(b, p);
            var m = Regex.Match(e2.Hinweise[0], @"(\d+) neu, (\d+) geändert, (\d+) gelöscht, (\d+) unverändert");
            Assert.True(m.Success, e2.Hinweise[0]);
            int nNeu = int.Parse(m.Groups[1].Value), nGeaendert = int.Parse(m.Groups[2].Value), nGeloescht = int.Parse(m.Groups[3].Value), nGleich = int.Parse(m.Groups[4].Value);
            aus.WriteLine($"Abgleich 2024-09 → 2026-02: {nNeu} neu, {nGeaendert} geändert, {nGeloescht} gelöscht, {nGleich} unverändert");
            Assert.True(nNeu > 1000 && nGeloescht > 100 && nGeaendert > 100 && nGleich > 0);
            Assert.Single(p.Modell.DerKlasse("Unterlage"));
            // Nach dem Abgleich derselbe Elementbestand wie bei einem frischen Übertrag der neuen Fassung
            Assert.Equal(1634, z2.JeKlasse["Rohr"]);
            Assert.Equal(318, z2.JeKlasse["Kanal"]);   // inkl. 9 Flexkanäle (FLEXIBLESEGMENT, importWerte seit 17.10.2026)
            Assert.Equal(14, z2.Systeme);
        }
        finally
        {
            if (p is not null) Aufraeumen(p);
            try { Directory.Delete(o, true); } catch (IOException) { }
        }
    }
}
