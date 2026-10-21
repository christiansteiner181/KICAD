using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N21 an ganzen Projekten: Beispielprojekt (hat Wände, aber ab Werk keine Türen und Fenster; die Tests setzen welche) und
/// die echte Bernstrasse 42 (Archicad-IFC; nur mit HLKS_ABNAHME=1 und wenn die Datei in %USERPROFILE%\HLKS_CAD_testdaten liegt).</summary>
public class SchnitthoeheSiaProjektTests(ITestOutputHelper ausgabe)
{
    /// <summary>Prüft alle Grundrisse: Schnitthöhe der Grafik = Geschosskote + gerechneter Wert, und jede Tür und jedes Fenster der Wände des
    /// Geschosses wird auf dieser Höhe geschnitten. Gibt die Anzahl geprüfter Öffnungen zurück.</summary>
    int AlleGeschnitten(Projekt p, bool schnittErzwingen = true)
    {
        var k = p.Katalog; var m = p.Modell;
        int pruefungen = 0;
        foreach (var an in m.DerKlasse("Ansicht").Where(a => a.Text("art") == "grundriss").OrderBy(a => a.Kennzeichen, StringComparer.Ordinal))
        {
            var geschoss = m.Finde(an.Geschoss)!;
            var ctx = Grafik.Kontext(p, an);
            var e = SchnitthoeheSia.SchnitthoeheNachSia(p, geschoss);
            ausgabe.WriteLine($"{an.Kennzeichen} (Geschoss {geschoss.Kennzeichen}): {e.Wert} mm über OK Boden, {e.Geschnitten} von {e.Oeffnungen} Öffnungen; {string.Join("; ", e.Massgebend)}");
            foreach (var h in e.Hinweise) ausgabe.WriteLine("   Hinweis: " + h);
            Assert.Equal((geschoss.Zahl("kote") ?? 0) + e.Wert, ctx.Schnitthoehe);
            foreach (var wand in m.Elemente.Where(x => k.IstArt(x, "Wand") && x.Geschoss == geschoss.Id))
            {
                if (Wandgeometrie.Lage(m, wand) is not { } w) continue;
                if (w.Dicke <= 0) continue;
                foreach (var o in Wandgeometrie.Oeffnungen(k, w, Wandgeometrie.OeffnungenVon(m, k, wand.Id)))
                {
                    if (schnittErzwingen)
                        Assert.True(o.Schneidet(ctx.Schnitthoehe - w.Unterkante), $"{o.Element.Kennzeichen} in {wand.Kennzeichen} wird im {an.Kennzeichen} bei {ctx.Schnitthoehe} nicht geschnitten");
                    pruefungen++;
                }
            }
            if (schnittErzwingen) Assert.Equal(e.Oeffnungen, e.Geschnitten);
        }
        return pruefungen;
    }

    static void Oeffnung(Projekt p, string klasse, string wand, double abstand, double hoehe, double? bruestung = null)
    {
        var par = new JsonObject { ["klasse"] = klasse, ["wand"] = wand, ["abstand"] = abstand, ["breite"] = 1000, ["hoehe"] = hoehe };
        if (bruestung is double b) par["bruestung"] = b;
        Ok(p.Ausfuehren("oeffnung_setzen", par));
    }

    [Fact]
    public void Beispielprojekt_OhneOeffnungen_TausendMitHinweis_JedeAnsicht()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var grundrisse = p.Modell.DerKlasse("Ansicht").Where(a => a.Text("art") == "grundriss").ToList();
        Assert.NotEmpty(grundrisse);
        foreach (var an in grundrisse)
        {
            var geschoss = p.Modell.Finde(an.Geschoss)!;
            var e = SchnitthoeheSia.SchnitthoeheNachSia(p, geschoss);
            Assert.Equal(0, e.Oeffnungen);
            Assert.Equal(1000, e.Wert);
            Assert.NotEmpty(e.Hinweise);
            Assert.Equal((geschoss.Zahl("kote") ?? 0) + 1000, Grafik.Kontext(p, an).Schnitthoehe);   // wie bisher: die Beispielansichten blieben unverändert
        }
    }

    [Fact]
    public void Beispielprojekt_MitTuerenUndFenstern_JederGrundrissSchneidetAlle()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        // je Geschoss Türen und Fenster mit unterschiedlichen Höhen; im EG sitzt die Brüstung über 1000 (Bereich beginnt bei 1100)
        Oeffnung(p, "Tuer", "W-UG-S", 3000, 2100);
        Oeffnung(p, "Fenster", "W-UG-S", 7000, 1100, 1350);       // Kellerfenster 1350 … 2450
        Oeffnung(p, "Tuer", "W-EG-S", 3000, 2100);
        Oeffnung(p, "Fenster", "W-EG-S", 7000, 1350, 1100);       // 1100 … 2450
        Oeffnung(p, "Fenster", "W-EG-O", 5000, 1200, 900);        // 900 … 2100
        Oeffnung(p, "Tuer", "W-1.OG-S", 3000, 2200);
        Oeffnung(p, "Fenster", "W-1.OG-N", 5000, 1000, 800);      // 800 … 1800
        int n = AlleGeschnitten(p);
        Assert.Equal(9, n);                                       // 7 Öffnungen; die zwei UG-Grundrisse (Sanitär, Heizung/Kälte) zählen die UG-Öffnungen je einmal
        var eg = SchnitthoeheSia.SchnitthoeheNachSia(p, p.Modell.Finde("EG")!);
        Assert.Equal(1100, eg.Wert);                              // gemeinsam 1100 … 2100
        Assert.Equal(3, eg.Oeffnungen);
        var ug = SchnitthoeheSia.SchnitthoeheNachSia(p, p.Modell.Finde("UG")!);
        Assert.Equal(1350, ug.Wert);                              // gemeinsam 1350 … 2100
        var og = SchnitthoeheSia.SchnitthoeheNachSia(p, p.Modell.Finde("1.OG")!);
        Assert.Equal(1000, og.Wert);                              // gemeinsam 800 … 1800 → 1000
    }

    [Fact]
    public void Bernstrasse42_Echt_AlleOeffnungenJedesGeschossesGeschnitten()
    {
        if (Environment.GetEnvironmentVariable("HLKS_ABNAHME") != "1") { ausgabe.WriteLine("Nur mit HLKS_ABNAHME=1 (der IFC-Import dauert bis zu zwei Minuten)."); return; }
        if (IfcHilfe.Echt(@"bernstrasse42\architekt_3d.ifc") is not { } datei) { ausgabe.WriteLine(@"Echte Datei fehlt: %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\architekt_3d.ifc"); return; }
        var p = IfcHilfe.Leer();
        IfcHilfe.Ok(IfcHilfe.Uebernehmen(p, datei));
        foreach (var g in p.Modell.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote") ?? 0))
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = "GR-" + g.Kennzeichen, ["geschoss"] = g.Kennzeichen,
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },   // wie eine Ansicht von vor N21
            }));
        // Probelauf 21.10.2026: in allen sieben Geschossen gibt es einen gemeinsamen Bereich (1000 mm), 72 Öffnungen in Wänden mit Dicke, alle geschnitten
        int n = AlleGeschnitten(p);
        ausgabe.WriteLine($"{n} Öffnungen in Wänden mit Dicke");
        Assert.True(n > 0);
    }
}
