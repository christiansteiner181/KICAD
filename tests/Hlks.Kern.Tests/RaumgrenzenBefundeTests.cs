using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern.Tests;

/// <summary>Folgearbeiten aus den Raumgrenzen von BKW (Modul 1c, B1; Entscheide Hauptlauf 18.10.2026): Verlängerung der Wände
/// über ein Raster (gleiches Ergebnis wie alle Paare), Räume aus Wänden mit Höhenprüfung und Inseln ohne Überlappung.</summary>
public class RaumgrenzenBefundeTests
{
    static Wandlage Lage(string kz, double ax, double ay, double bx, double by, double dicke, double unten = 0, double? hoehe = 3000)
    {
        double dx = bx - ax, dy = by - ay, l = Math.Sqrt(dx * dx + dy * dy);
        return new Wandlage
        {
            Wand = new Element { Id = kz, Kennzeichen = kz, Klasse = "Wand" },
            Ax = ax, Ay = ay, Bx = bx, By = by, Laenge = l, Ux = dx / l, Uy = dy / l, Dicke = dicke, Unterkante = unten, Hoehe = hoehe,
        };
    }

    /// <summary>Geschlossener Wandzug um die Achsen (x0, y0) … (x1, y1).</summary>
    static IEnumerable<Wandlage> Rechteck(string kz, double x0, double y0, double x1, double y1, double dicke, double unten = 0, double? hoehe = 3000) =>
    [
        Lage(kz + "s", x0, y0, x1, y0, dicke, unten, hoehe), Lage(kz + "o", x1, y0, x1, y1, dicke, unten, hoehe),
        Lage(kz + "n", x1, y1, x0, y1, dicke, unten, hoehe), Lage(kz + "w", x0, y1, x0, y0, dicke, unten, hoehe),
    ];

    // ------------------------------------------------------------------ 6) Verlängerung über ein Raster

    [Fact]
    public void Verlaengern_MitRaster_GenauWieAllePaare()
    {
        // Raster aus 12 × 10 Räumen (Wände 100 … 400 dick), dazu schräge und zufällige Wände: über 64 Wände, also mit Raster
        var zufall = new Random(1810);
        var liste = new List<Wandlage>();
        for (int i = 0; i <= 12; i++) liste.Add(Lage($"x{i}", i * 4000, 0, i * 4000, 40000, 100 + 25 * (i % 13)));
        for (int j = 0; j <= 10; j++) liste.Add(Lage($"y{j}", 0, j * 4000, 48000, j * 4000, 150 + 20 * (j % 11)));
        for (int k = 0; k < 150; k++)
        {
            double ax = zufall.NextDouble() * 48000, ay = zufall.NextDouble() * 40000, w = zufall.NextDouble() * Math.PI, l = 500 + zufall.NextDouble() * 6000;
            liste.Add(Lage($"z{k}", ax, ay, ax + Math.Cos(w) * l, ay + Math.Sin(w) * l, zufall.Next(4) == 0 ? 0 : 50 + zufall.NextDouble() * 450));
        }
        // Enden genau auf Achsen und Flächen anderer Wände (Grenzfälle der Toleranz Dicke/2 + 1 mm)
        liste.Add(Lage("t1", 6000, 2000, 8000 - 50.5, 2000, 200));   // endet 50.5 mm vor der Achse von x2 (Dicke 150): verlängert
        liste.Add(Lage("t2", 10000, 6000, 12000 - 175 / 2.0 - 1.5, 6000, 200));   // 0.5 mm zu weit von x3 (Dicke 175): nicht
        Assert.True(liste.Count >= 64);
        var mitRaster = liste.Select(w => Lage(w.Id, w.Ax, w.Ay, w.Bx, w.By, w.Dicke)).ToList();
        Wandgeometrie.Verlaengern(liste, raster: false);
        Wandgeometrie.Verlaengern(mitRaster);
        for (int i = 0; i < liste.Count; i++)
        {
            Assert.True(liste[i].VerlAnfang == mitRaster[i].VerlAnfang, $"{liste[i].Id} Anfang: {liste[i].VerlAnfang} / {mitRaster[i].VerlAnfang}");
            Assert.True(liste[i].VerlEnde == mitRaster[i].VerlEnde, $"{liste[i].Id} Ende: {liste[i].VerlEnde} / {mitRaster[i].VerlEnde}");
        }
        Assert.True(liste.Count(w => w.VerlAnfang > 0 || w.VerlEnde > 0) >= 24, "der Test prüft echte Verlängerungen");
    }

    // ------------------------------------------------------------------ 4) Räume aus Wänden: Höhenprüfung

    [Fact]
    public void ImGeschoss_WandUnterDemFussbodenTrenntNicht()
    {
        // Kote 0: Fundamentwand −1000 … −180 nein, Sockel unter dem Boden −860 … +20 nein (Toleranz 20 mm), Sockel −70 … 455 ja,
        // Wand −60 … 2700 ja, versetzter Gebäudeteil (BKW 1.OG Kote 3000, Wand 4761 … 8431) ja, Wand ohne Höhe ja
        Assert.False(RaeumeAusWaenden.ImGeschoss(Lage("f", 0, 0, 1000, 0, 200, -1000, 820), 0));
        Assert.False(RaeumeAusWaenden.ImGeschoss(Lage("s", 0, 0, 1000, 0, 200, -860, 880), 0));
        Assert.True(RaeumeAusWaenden.ImGeschoss(Lage("k", 0, 0, 1000, 0, 200, -70, 525), 0));
        Assert.True(RaeumeAusWaenden.ImGeschoss(Lage("w", 0, 0, 1000, 0, 200, -60, 2760), 0));
        Assert.True(RaeumeAusWaenden.ImGeschoss(Lage("v", 0, 0, 1000, 0, 200, 4761, 3670), 3000));
        Assert.True(RaeumeAusWaenden.ImGeschoss(Lage("o", 0, 0, 1000, 0, 200, 0, null), 0));
    }

    [Fact]
    public void Zusammenfuehren_FundamentwandTeiltNicht_RaumOhneSieOffenBleibt()
    {
        // Rechteck 10 × 8 m (Achsen), Wände 0 … 3000; quer durch eine Fundamentwand (−1000 … −180) bei x = 5000:
        // gefiltert ein Raum 9.8 × 7.8 m statt zweier; die beiden Räume aus allen Wänden liegen darin und fallen weg
        double Flaeche(List<List<double[]>> f) => f.Sum(r => Polygone.Flaecheninhalt(r));
        var alle = Rechteck("a", 0, 0, 10000, 8000, 200).Append(Lage("f", 5000, 0, 5000, 8000, 300, -1000, 820)).ToList();
        var gefiltert = Rechteck("a", 0, 0, 10000, 8000, 200).ToList();
        Wandgeometrie.Verlaengern(alle); Wandgeometrie.Verlaengern(gefiltert);
        var f = RaeumeAusWaenden.Zusammenfuehren(RaeumeAusWaenden.Flaechen(gefiltert), RaeumeAusWaenden.Flaechen(alle), out int behalten);
        Assert.Single(f);
        Assert.Equal(0, behalten);
        Assert.Equal(9.8 * 7.8 * 1e6, Flaeche(f), 1);
        // Raum, dessen eine Seite nur eine Sockelwand unter dem Boden hat (Fassade fehlt): gefiltert offen → aus allen Wänden
        var offen = new List<Wandlage>
        {
            Lage("s", 0, 0, 10000, 0, 200, -1000, 820),   // Sockel unter dem Boden
            Lage("o", 10000, 0, 10000, 8000, 200), Lage("n", 10000, 8000, 0, 8000, 200), Lage("w", 0, 8000, 0, 0, 200),
        };
        var ohneSockel = offen.Skip(1).Select(w => Lage(w.Id, w.Ax, w.Ay, w.Bx, w.By, w.Dicke)).ToList();
        Wandgeometrie.Verlaengern(offen); Wandgeometrie.Verlaengern(ohneSockel);
        Assert.Empty(RaeumeAusWaenden.Flaechen(ohneSockel));
        f = RaeumeAusWaenden.Zusammenfuehren(RaeumeAusWaenden.Flaechen(ohneSockel), RaeumeAusWaenden.Flaechen(offen), out behalten);
        Assert.Single(f);
        Assert.Equal(1, behalten);
    }

    // ------------------------------------------------------------------ 5) Räume aus Wänden: Inseln

    /// <summary>Aussenwände um (0, 0) … (10000, 8000), Dicke 200; Insel = geschlossener Wandzug um (4000, 3000) … (6000, 5000).
    /// Aussenraum innen 9.8 × 7.8 m minus Insel 2.2 × 2.2 m = 71.6 m²; Inselraum 1.8 × 1.8 m = 3.24 m².</summary>
    static List<List<double[]>> MitInsel(out List<string> hinweise)
    {
        var lagen = Rechteck("a", 0, 0, 10000, 8000, 200).Concat(Rechteck("i", 4000, 3000, 6000, 5000, 200)).ToList();
        Wandgeometrie.Verlaengern(lagen);
        var h = new List<string>();
        var f = RaeumeAusWaenden.Flaechen(lagen, h.Add);
        hinweise = h;
        return f;
    }

    [Fact]
    public void Inseln_VomUmschliessendenRaumAbgezogen_KeineUeberlappung()
    {
        var f = MitInsel(out var hinweise);
        Assert.Equal(2, f.Count);
        var aussen = f.OrderByDescending(x => Polygone.Flaecheninhalt(x)).First();
        var innen = f.OrderBy(x => Polygone.Flaecheninhalt(x)).First();
        Assert.Equal(71.6e6, Polygone.Flaecheninhalt(aussen), 1);   // Gausssche Formel: Aussen − Insel (Steg der Breite 0)
        Assert.Equal(3.24e6, Polygone.Flaecheninhalt(innen), 1);
        Assert.Equal(4 + 4 + 1 + 2, aussen.Count);                  // Aussenring, Insel, M nochmals, P zweimal
        // keine Überlappung (Vereinigung mit NonZero macht aus dem Steg wieder Aussenring und Loch)
        var schnitt = Clipper.Intersect(new PathsD { Polygone.Pfad(aussen) }, new PathsD { Polygone.Pfad(innen) }, FillRule.NonZero, 3);
        Assert.True(Math.Abs(Clipper.Area(schnitt)) < 1, "Überlappung");
        Assert.Equal(2, Polygone.Vereinigen([Polygone.Pfad(aussen)]).Count);
        // Punkt im Raum: in der Insel nicht, daneben schon
        var raum = new Geometrie { Art = "polygon", Punkte = aussen };
        Assert.False(raum.Enthaelt(5000, 4000));
        Assert.True(raum.Enthaelt(2000, 4000));
        Assert.True(raum.Enthaelt(8000, 4000));
        Assert.Contains(hinweise, h => h.Contains("Inseln"));
    }

    [Fact]
    public void Inseln_RaumgrenzenSehenDenInselraumAlsNachbarn()
    {
        var f = MitInsel(out _);
        var aussen = f.OrderByDescending(x => Polygone.Flaecheninhalt(x)).First();
        var innen = f.OrderBy(x => Polygone.Flaecheninhalt(x)).First();
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", 0, 0, 10000, 8000, 200);
        RaumgrenzenHilfe.Waende(p, "EG", 4000, 3000, 6000, 5000, 200);
        foreach (var (kz, ring) in new[] { ("R-A", aussen), ("R-I", innen) })
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Raum", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts(ring.ToArray()) },
            }));
        var g = RaumgrenzenHilfe.Rechnen(p, "R-A").Grenzen;
        RaumgrenzenHilfe.Nahe(71.6, RaumgrenzenHilfe.Summe(g, x => x.Art == "boden"), 1e-6, "Boden");
        // Lochkanten: 4 × 2.2 m gegen den Inselraum, Höhe 3.0 m (Geschosshöhe); Aussenkanten 2 × (9.8 + 7.8) m aussen
        RaumgrenzenHilfe.Nahe(4 * 2.2 * 3.0, RaumgrenzenHilfe.Summe(g, x => x.NachbarRaum?.Kennzeichen == "R-I"), 1e-6, "gegen Insel");
        RaumgrenzenHilfe.Nahe(2 * (9.8 + 7.8) * 3.0, RaumgrenzenHilfe.Summe(g, x => x.Art == "wand" && x.Angrenzung == "aussen"), 1e-6, "aussen");
        Assert.DoesNotContain(g, x => x.Offen);   // kein Scheinstück auf dem Steg
        Assert.DoesNotContain(RaumgrenzenHilfe.Rechnen(p).Hinweise, h => h.Contains("überlappen"));
    }

    [Fact]
    public void Inseln_FlaecheGanzBelegt_KeinRaum_UndFreistehendeWandAbgezogen()
    {
        // Hof 2.8 × 2.8 m innen, von einem Block aus zwei dicken Wänden (2.7 × 2.7 m) bis auf 0.55 m² ausgefüllt: kein Raum
        var lagen = Rechteck("a", 0, 0, 3000, 3000, 200).ToList();
        lagen.Add(Lage("b1", 150, 800, 2850, 800, 1300));
        lagen.Add(Lage("b2", 150, 2150, 2850, 2150, 1400));
        Wandgeometrie.Verlaengern(lagen);
        var h = new List<string>();
        Assert.Empty(RaeumeAusWaenden.Flaechen(lagen, h.Add));
        Assert.Contains(h, x => x.Contains("ganz von freistehenden Wänden belegt"));
        // eine freistehende Wand 2000 × 200 in einem Raum 9.8 × 7.8 m: ihre Fläche geht ab (0.4 m²)
        var mitWand = Rechteck("a", 0, 0, 10000, 8000, 200).Append(Lage("f", 3000, 4000, 5000, 4000, 200)).ToList();
        Wandgeometrie.Verlaengern(mitWand);
        var f = Assert.Single(RaeumeAusWaenden.Flaechen(mitWand));
        Assert.Equal(9.8 * 7.8 * 1e6 - 2000 * 200, Polygone.Flaecheninhalt(f), 1);
    }
}
