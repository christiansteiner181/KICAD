using System.Diagnostics;
using System.Text.Json.Nodes;
using Clipper2Lib;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Anschluss Wand–Decke (Modul 1c, Auftrag B6, Entscheid «Decke läuft durch»): Im Schnitt wird die Schnittfläche einer
/// Wand um die Schnittflächen der Decken und Dächer verkleinert, im 3D endet der Wandkörper an der Unterkante einer Decke, die
/// über ihm liegt; die Wand-Geometrie bleibt. Nachführung: Geschosskote oder -höhe geändert → Hinweis «liegt nicht mehr auf
/// der Geschosskote».</summary>
public class AnschlussWandDeckeTests(ITestOutputHelper aus)
{
    // ---------------------------------------------------------------- Hilfen

    /// <summary>Fläche einer Menge von Polygonen (Löcher abgezogen) in mm².</summary>
    static double Flaeche(PathsD p) => Math.Abs(Clipper.Area(p));

    static PathsD Pfade(IEnumerable<Primitiv> polygone)
    {
        var liste = new PathsD();
        foreach (var x in polygone)
        {
            liste.Add(Polygone.Ccw(Polygone.Pfad(x.Punkte)));
            foreach (var l in x.Loecher ?? [])
            {
                var r = Polygone.Pfad(l);
                if (Clipper.Area(r) > 0) r.Reverse();
                liste.Add(r);
            }
        }
        return liste;
    }

    static PathsD Schnitt(PathsD a, PathsD b) => Clipper.Intersect(a, b, FillRule.NonZero, Polygone.Praezision);

    /// <summary>Schnittflächen (Polygone mit dicker Kontur) der Wände bzw. der Decken und Dächer einer Plangrafik.</summary>
    static (PathsD Waende, PathsD Decken, double SummeWaende, double SummeDecken) Schnittflaechen(Projekt p, Plangrafik g)
    {
        var w = new PathsD(); var d = new PathsD(); double sw = 0, sd = 0;
        foreach (var t in g.Teile)
        {
            if (t.Id is null || p.Modell.Finde(t.Id) is not { } e) continue;
            var poly = t.Primitive.Where(x => x.Art == "polygon" && x.Stil.Stift == Stifte.Dick).ToList();
            if (poly.Count == 0) continue;
            var pf = Pfade(poly);
            if (p.Katalog.IstArt(e, "Wand")) { w.AddRange(pf); sw += poly.Sum(PolyFlaeche); }
            else if (p.Katalog.IstArt(e, "Decke") || p.Katalog.IstArt(e, "Dach")) { d.AddRange(pf); sd += poly.Sum(PolyFlaeche); }
        }
        return (w, d, sw, sd);
    }

    /// <summary>Keine Doppelfüllung: Wand ∩ Decke = 0 und Summe der einzelnen Flächen = Fläche der Vereinigung (je in mm²).</summary>
    static void OhneDoppelfuellung(Projekt p, Plangrafik g)
    {
        var (w, d, sw, sd) = Schnittflaechen(p, g);
        Assert.True(sw > 0 && sd > 0, "Schnitt trifft Wände und Decken");
        double ueber = Flaeche(Schnitt(Polygone.Vereinigen(w), Polygone.Vereinigen(d)));
        Assert.True(ueber < 1, $"Wand ∩ Decke = {ueber:0.###} mm²");
        double vereinigt = Flaeche(Polygone.Vereinigen(new PathsD([.. w, .. d])));
        Assert.True(Math.Abs(vereinigt - (sw + sd)) < 1, $"Summe {sw + sd:0.###} mm², Vereinigung {vereinigt:0.###} mm²");
    }

    static BefehlsErgebnis Decke(Projekt p, string art = "decke", double dicke = 200, string geschoss = "EG", JsonObject? mehr = null, bool trocken = false)
    {
        var par = new JsonObject { ["geschoss"] = geschoss, ["art"] = art, ["dicke"] = dicke };
        foreach (var (k, v) in mehr ?? []) par[k] = v?.DeepClone();
        return p.Ausfuehren("decke_erzeugen", par, trocken);
    }

    static void Schnittlinie(Projekt p, string kz, double[] a, double[] b) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(a, b) },
        }));

    /// <summary>Schnitt quer durch das Rechteck bei x: Linie von Süd nach Nord, u = y + 1000, v = z; Blick nach Westen.</summary>
    static Plangrafik Quer(Projekt p, double x = 3000, string kz = "S-1")
    {
        Schnittlinie(p, kz, [x, -1000], [x, 5000]);
        return Grafik.Plan(p, kz);
    }

    static List<Primitiv> Flaechen(Plangrafik g, Projekt p, string kz) =>
        g.Teile.Single(t => t.Id == Hole(p, kz).Id).Primitive.Where(x => x.Art == "polygon").ToList();

    static List<Koerperteil> Quader(Projekt p, string kz) =>
        Grafik.Koerper(p, [Hole(p, kz).Id]).Single().Teile.OrderBy(t => t.Mitte![0]).ThenBy(t => t.Mitte![1]).ThenBy(t => t.Mitte![2]).ToList();

    static double Oben(Koerperteil t) => t.Mitte![2] + t.Masse![2] / 2;
    static double Unten(Koerperteil t) => t.Mitte![2] - t.Masse![2] / 2;

    static void Geschoss(Projekt p, string kz, double kote, double hoehe, string gebaeude = "GEB") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = gebaeude,
            ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe },
        }));

    static BefehlsErgebnis Geschossaendern(Projekt p, string kz, JsonObject eig, bool trocken = false) =>
        p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }, trocken);

    const string NichtAufKote = "liegt nicht mehr auf der Geschosskote";

    // ---------------------------------------------------------------- Schnitt

    [Fact]
    public void Schnitt_RechteckMitDecke_WandEndetAnDerUnterkante_KeineDoppelfuellung()
    {
        var p = Rechteck();                                             // Wände 3000 hoch (Geschosshöhe EG), Dicke 200
        Ok(Decke(p));                                                   // D-1: Oberkante 3000, Unterkante 2800, −100 … 6100 × −100 … 4100
        var g = Quer(p);
        // Hand: Wände y −100 … 100 bzw. 3900 … 4100 → u 900 … 1100 bzw. 4900 … 5100, z 0 … 2800 (statt 3000)
        var w1 = Assert.Single(Flaechen(g, p, "W-1"));
        Nahe([900, 0, 1100, 2800], Box(w1.Punkte));
        Assert.Equal(200.0 * 2800, PolyFlaeche(w1), 3);
        Assert.Equal(Stifte.Dick, w1.Stil.Stift);
        Nahe([4900, 0, 5100, 2800], Box(Assert.Single(Flaechen(g, p, "W-3")).Punkte));
        // die Decke bleibt ganz: u 900 … 5100, z 2800 … 3000
        var d = Assert.Single(Flaechen(g, p, "D-1"));
        Nahe([900, 2800, 5100, 3000], Box(d.Punkte));
        Assert.Equal(4200.0 * 200, PolyFlaeche(d), 3);
        // Summe 2 × 200 × 2800 + 4200 × 200 = 1.96 m² = Vereinigung
        OhneDoppelfuellung(p, g);
        var (_, _, sw, sd) = Schnittflaechen(p, g);
        Assert.Equal(1.96e6, sw + sd, 1);
        // Geometrie der Wand unverändert
        Assert.Equal(3000, Hole(p, "W-1").Geometrie!.Hoehe);
    }

    [Fact]
    public void Schnitt_DeckeUeberdecktNurTeilDerWanddicke_WandMinusDecke()
    {
        var p = Rechteck();
        // Decke ab y = 50: überdeckt von W-1 (y −100 … 100) nur den Streifen 50 … 100; die Achse y = 0 liegt ausserhalb,
        // der Körper von W-1 bleibt also voll (3000) – der Schnitt zieht die Decke trotzdem ab
        Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([-100, 50], [6100, 50], [6100, 4100], [-100, 4100]) }));
        Assert.Equal(3000, Oben(Assert.Single(Quader(p, "W-1"))), 6);
        var g = Quer(p);
        var w1 = Assert.Single(Flaechen(g, p, "W-1"));
        // Hand: u 900 … 1100 × z 0 … 3000 minus u 1050 … 1100 × z 2800 … 3000 → L-Form, 600000 − 10000 = 590000 mm²
        Nahe([900, 0, 1100, 3000], Box(w1.Punkte));
        Assert.Equal(6, w1.Punkte.Count);
        Assert.Equal(200.0 * 3000 - 50.0 * 200, PolyFlaeche(w1), 3);
        Assert.Contains(w1.Punkte, q => Math.Abs(q[0] - 1050) < 1e-3 && Math.Abs(q[1] - 2800) < 1e-3);
        OhneDoppelfuellung(p, g);
    }

    [Fact]
    public void Schnitt_AusserhalbDerDecke_WandBleibtVoll()
    {
        var p = Rechteck();
        Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([-100, -100], [3000, -100], [3000, 4100], [-100, 4100]) }));   // nur die Westhälfte
        var aussen = Quer(p, 4500, "S-AUSSEN");                         // ausserhalb der Decke: volle Wandhöhe
        Nahe([900, 0, 1100, 3000], Box(Assert.Single(Flaechen(aussen, p, "W-1")).Punkte));
        Assert.DoesNotContain(aussen.Teile, t => t.Id == Hole(p, "D-1").Id && t.Primitive.Any(x => x.Art == "polygon"));
        var innen = Quer(p, 1500, "S-INNEN");                           // unter der Decke: bis Unterkante
        Nahe([900, 0, 1100, 2800], Box(Assert.Single(Flaechen(innen, p, "W-1")).Punkte));
        OhneDoppelfuellung(p, innen);
    }

    [Fact]
    public void Schnitt_MaterialBeton_AuchGekuerzt()
    {
        var p = Rechteck("beton");
        Ok(Decke(p));
        var f = Assert.Single(Flaechen(Quer(p), p, "W-1"));
        Assert.Equal("beton", f.Stil.Muster);
        Nahe([900, 0, 1100, 2800], Box(f.Punkte));
    }

    [Fact]
    public void Schnitt_OhneDecke_Unveraendert()
    {
        var p = Rechteck();
        var w1 = Assert.Single(Flaechen(Quer(p), p, "W-1"));
        Nahe([900, 0, 1100, 3000], Box(w1.Punkte));
    }

    // ---------------------------------------------------------------- 3D

    [Fact]
    public void Koerper_WandUnterDecke_EndetAufUnterkante_GeometrieBleibt()
    {
        var p = Rechteck();
        Assert.Equal(3000, Oben(Assert.Single(Quader(p, "W-1"))), 6);   // ohne Decke: Geschosshöhe
        Ok(Decke(p));
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var q = Assert.Single(Quader(p, kz));
            Assert.Equal(0, Unten(q), 6);
            Assert.Equal(2800, Oben(q), 6);                             // UK Decke = 3000 − 200
            Assert.Equal(3000, Hole(p, kz).Geometrie!.Hoehe);           // Element unverändert
        }
        Nahe([6200, 200, 2800], Quader(p, "W-1")[0].Masse!);           // Länge samt Eckverlängerung bleibt
        // die Decke selbst: Prisma 2800 … 3000
        var d = Assert.Single(Grafik.Koerper(p, [Hole(p, "D-1").Id]).Single().Teile);
        Assert.Equal(2800, d.Unten);
        Assert.Equal(3000, d.Oben);
    }

    [Fact]
    public void Koerper_WandTeilweiseAusserhalb_NurDerUeberdeckteTeilGekuerzt()
    {
        var p = Rechteck();
        Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([-100, -100], [3000, -100], [3000, 4100], [-100, 4100]) }));   // nur die Westhälfte
        // W-1 (Achse y = 0, −100 … 6100): bis x = 3000 unter der Decke, danach frei (wie eine Attika)
        var q = Quader(p, "W-1");
        Assert.Equal(2, q.Count);
        Assert.Equal(3100, q[0].Masse![0], 6);                          // −100 … 3000
        Assert.Equal(2800, Oben(q[0]), 6);
        Assert.Equal(3100, q[1].Masse![0], 6);                          // 3000 … 6100
        Assert.Equal(3000, Oben(q[1]), 6);
        Assert.Equal(1450, q[0].Mitte![0], 6);
        Assert.Equal(4550, q[1].Mitte![0], 6);
        Assert.Equal(3000, Oben(Assert.Single(Quader(p, "W-2"))), 6);   // Ostwand ganz ausserhalb: voll
        Assert.Equal(2800, Oben(Assert.Single(Quader(p, "W-4"))), 6);   // Westwand ganz darunter
    }

    [Fact]
    public void Koerper_AchseGenauAufDemPlattenrand_BleibtVoll_SchnittZiehtDieHaelfteAb()
    {
        var p = Rechteck();
        Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([-100, 0], [6100, 0], [6100, 4100], [-100, 4100]) }));   // Rand auf der Achse von W-1
        Assert.Equal(3000, Oben(Assert.Single(Quader(p, "W-1"))), 6);   // im Zweifel nichts wegnehmen
        Assert.Equal(2800, Oben(Assert.Single(Quader(p, "W-3"))), 6);
        // Schnitt: u 900 … 1100 × 0 … 3000 minus u 1000 … 1100 × 2800 … 3000
        var w1 = Assert.Single(Flaechen(Quer(p), p, "W-1"));
        Assert.Equal(200.0 * 3000 - 100.0 * 200, PolyFlaeche(w1), 3);
    }

    [Fact]
    public void Koerper_WandMitTuerUndFenster_StuerzeEndenAnDerDecke()
    {
        var p = Rechteck();
        Ok(Tuer(p));                                                    // W-1, x 2550 … 3450, 2100 hoch
        Ok(Fenster(p, wand: "W-3"));                                    // W-3, Brüstung 900, Sturz 2100
        Ok(Decke(p));
        foreach (var kz in new[] { "W-1", "W-3" })
        {
            var q = Quader(p, kz);
            Assert.All(q, t => Assert.True(Oben(t) <= 2800 + 1e-6, $"{kz}: Oberkante {Oben(t)}"));
            Assert.Contains(q, t => Math.Abs(Unten(t) - 2100) < 1e-6 && Math.Abs(Oben(t) - 2800) < 1e-6);   // Sturz bis UK Decke
        }
        Assert.Equal(3, Quader(p, "W-1").Count);                        // links, Sturz, rechts
        Assert.Equal(4, Quader(p, "W-3").Count);                        // links, Brüstung, Sturz, rechts
    }

    [Fact]
    public void Koerper_DeckeInHalberWandhoehe_UnterbrichtDieWand()
    {
        var p = Neu();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["hoehe"] = 6000, ["geschlossen"] = true,
        }));
        Ok(Decke(p));                                                   // kein Geschoss darüber: Oberkante 0 + 3000, UK 2800
        var q = Quader(p, "W-1");
        Assert.Equal(2, q.Count);
        Assert.Equal(0, Unten(q[0]), 6);
        Assert.Equal(2800, Oben(q[0]), 6);
        Assert.Equal(3000, Unten(q[1]), 6);
        Assert.Equal(6000, Oben(q[1]), 6);
    }

    [Fact]
    public void Koerper_PlatteUnterOderUeberDerWand_KeineKuerzung()
    {
        var p = Rechteck();
        Ok(Decke(p, "boden", 300));                                     // Oberkante 0 = Wandunterkante: Wand steht darauf
        Ok(p.Ausfuehren("element_anlegen", new JsonObject              // Platte weit über der Wand
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-HOCH", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([-100, -100, 6000], [6100, -100, 6000], [6100, 4100, 6000], [-100, 4100, 6000]), ["dicke"] = 250 },
        }));
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var q = Assert.Single(Quader(p, kz));
            Assert.Equal(0, Unten(q), 6);
            Assert.Equal(3000, Oben(q), 6);
        }
    }

    [Fact]
    public void Koerper_PlatteInDerWandUnterkante_KuerztVonUnten()
    {
        var p = Rechteck();
        // Platte 0 … 250 über dem ganzen Grundriss (die Wand steht in ihr): «Wand minus Decke» wie im Schnitt
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-UNTEN", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([-100, -100, 250], [6100, -100, 250], [6100, 4100, 250], [-100, 4100, 250]), ["dicke"] = 250 },
        }));
        var q = Assert.Single(Quader(p, "W-1"));
        Assert.Equal(250, Unten(q), 6);
        Assert.Equal(3000, Oben(q), 6);
    }

    static JsonObject Platte(string klasse, string kz, double ok, double dicke) => new()
    {
        ["klasse"] = klasse, ["kennzeichen"] = kz, ["geschoss"] = "EG",
        ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([-100, -100, ok], [6100, -100, ok], [6100, 4100, ok], [-100, 4100, ok]), ["dicke"] = dicke },
    };

    [Fact]
    public void Koerper_WandGanzInEinerPlatte_BleibtGanz_SchnittAuch()
    {
        var p = Rechteck();
        // geneigtes IFC-Dach als Kasten über die ganze Dachhöhe (−500 … 3500): die Wände 0 … 3000 liegen ganz darin – kein
        // Anschluss «Decke über Wand», sondern ein Konflikt im Modell; die Wand bleibt ganz
        Ok(p.Ausfuehren("element_anlegen", Platte("Dach", "DA-KASTEN", 3500, 4000)));
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var q = Assert.Single(Quader(p, kz));
            Assert.Equal(0, Unten(q), 6);
            Assert.Equal(3000, Oben(q), 6);
        }
        var w1 = Assert.Single(Flaechen(Quer(p), p, "W-1"));
        Nahe([900, 0, 1100, 3000], Box(w1.Punkte));
    }

    [Fact]
    public void Koerper_WandGanzInPlattenpaket_BleibtGanz()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", Platte("Decke", "D-A", 1500, 1800)));   // −300 … 1500
        Ok(p.Ausfuehren("element_anlegen", Platte("Decke", "D-B", 3300, 1800)));   // 1500 … 3300
        var q = Assert.Single(Quader(p, "W-1"));
        Assert.Equal(0, Unten(q), 6);
        Assert.Equal(3000, Oben(q), 6);
        // ragt die Wand oben aus dem Paket heraus, bleibt nur dieser Teil
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-1", ["geometrie"] = new JsonObject
        {
            ["art"] = "linie", ["punkte"] = Pts([0, 0, 0], [6000, 0, 0]), ["dicke"] = 200, ["hoehe"] = 3600,
        } }));
        var oben = Assert.Single(Quader(p, "W-1"));
        Assert.Equal(3300, Unten(oben), 6);
        Assert.Equal(3600, Oben(oben), 6);
    }

    [Fact]
    public void Koerper_MitZwischenspeicher_GleichWieEinzeln()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([-100, -100], [3000, -100], [3000, 4100], [-100, 4100]) }));
        var alle = Grafik.Koerper(p).ToDictionary(k => k.Id);
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var einzeln = new ArchitekturGrafik().Koerper(p, Hole(p, kz))!;   // ohne Lauf: Decken jedes Mal neu gesammelt
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(einzeln), System.Text.Json.JsonSerializer.Serialize(alle[Hole(p, kz).Id]));
        }
    }

    [Fact]
    public void Koerper_VielePlatten_RasterGleichWieOhne()
    {
        var p = Rechteck();
        // 24 Streifen à 250 mm (x −100 … 5900), Oberkante 3000, abwechselnd 200 und 300 dick – ab 16 Platten rechnet der Lauf
        // mit dem Raster
        for (int i = 0; i < 24; i++)
        {
            double x0 = -100 + i * 250;
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Decke", ["kennzeichen"] = $"D-S{i}", ["geschoss"] = "EG",
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([x0, -100, 3000], [x0 + 250, -100, 3000], [x0 + 250, 4100, 3000], [x0, 4100, 3000]), ["dicke"] = i % 2 == 0 ? 200 : 300 },
            }));
        }
        var alle = Grafik.Koerper(p).ToDictionary(k => k.Id);
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var einzeln = new ArchitekturGrafik().Koerper(p, Hole(p, kz))!;
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(einzeln), System.Text.Json.JsonSerializer.Serialize(alle[Hole(p, kz).Id]));
        }
        // W-1: 24 Streifen unter den Platten (bis 2800 bzw. 2700) und das freie Stück 5900 … 6100 (voll)
        var q = Quader(p, "W-1");
        Assert.Equal(25, q.Count);
        Assert.Equal(2800, Oben(q[0]), 6);
        Assert.Equal(2700, Oben(q[1]), 6);
        Assert.Equal(3000, Oben(q[^1]), 6);
        Assert.Equal(200, q[^1].Masse![0], 6);
    }

    // ---------------------------------------------------------------- Grundriss

    [Fact]
    public void Grundriss_MitDecke_Unveraendert()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        string Waende() => System.Text.Json.JsonSerializer.Serialize(Grafik.Plan(p, "AN-EG").Teile.Where(t => t.Ebene == "wand").ToList());
        var vorher = Waende();
        Ok(Decke(p));
        Assert.Equal(vorher, Waende());
    }

    // ---------------------------------------------------------------- Beispielprojekt

    [Fact]
    public void Beispielprojekt_DeckenAllerGeschosse_SchnittOhneDoppelfuellung()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Decke(p, "boden", 300, "UG"));                               // D-1: −3000 … −2700
        Ok(Decke(p, "decke", 250, "UG"));                               // D-2: −250 … 0
        Ok(Decke(p, "decke", 250, "EG"));                               // D-3: 2650 … 2900
        Ok(Decke(p, "dach", 300, "1.OG"));                              // DA-1: 5500 … 5800
        Schnittlinie(p, "S-BSP", [6000, -2000], [6000, 12000]);         // Nord–Süd, u = y + 2000
        var g = Grafik.Plan(p, "S-BSP");
        OhneDoppelfuellung(p, g);
        // Hand: Südwände Achse y = 0, Dicke 350 → u 1825 … 2175; jede endet an der Unterkante der Decke darüber
        Nahe([1825, -2700, 2175, -250], Box(Assert.Single(Flaechen(g, p, "W-UG-S")).Punkte));
        Nahe([1825, 0, 2175, 2650], Box(Assert.Single(Flaechen(g, p, "W-EG-S")).Punkte));
        Nahe([1825, 2900, 2175, 5500], Box(Assert.Single(Flaechen(g, p, "W-1.OG-S")).Punkte));
        // Decken ganz: Aussenkontur y −175 … 10175 → u 1825 … 12175
        Nahe([1825, 2650, 12175, 2900], Box(Assert.Single(Flaechen(g, p, "D-3")).Punkte));
        // 3D: alle Wandkörper enden unter der Decke darüber
        foreach (var (kz, oben) in new[] { ("W-UG-S", -250.0), ("W-EG-N", 2650.0), ("W-1.OG-O", 5500.0) })
            Assert.Equal(oben, Quader(p, kz).Max(Oben), 6);
    }

    // ---------------------------------------------------------------- Nachführung Geschosskote

    /// <summary>EG (0, 3000) und OG (3000, 3000) mit je einem Rechteck; D-1 = Decke über EG (gehört zu OG, Oberkante 3000),
    /// D-2 = Boden EG (0), DA-1 = Dach OG (6000).</summary>
    static Projekt ZweiGeschosse()
    {
        var p = Rechteck();
        Geschoss(p, "OG", 3000, 3000);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["geschlossen"] = true }));
        Ok(Decke(p));
        Ok(Decke(p, "boden", 300));
        Ok(Decke(p, "dach", 300, "OG"));
        return p;
    }

    [Fact]
    public void Nachfuehrung_KoteDesGeschossesDarueber_HinweisDeckeBleibt()
    {
        var p = ZweiGeschosse();
        var stand = Json.Text(Hole(p, "D-1").Geometrie!.Punkte);
        var e = Ok(Geschossaendern(p, "OG", new JsonObject { ["kote"] = 3200 }));
        var h = Assert.Single(e.Hinweise, x => x.Contains(NichtAufKote) && x.StartsWith("Decke D-1 "));
        Assert.Contains("Oberkante 3000 mm", h);
        Assert.Contains("Kote von OG jetzt 3200 mm", h);
        Assert.Contains("neu erzeugen oder Höhe anpassen", h);
        Assert.Contains("element_loeschen D-1", h);
        Assert.Contains("elemente_verschieben D-1 mit dz = 200", h);
        // keine automatische Änderung
        Assert.DoesNotContain(e.Aenderungen, a => a.Kennzeichen is "D-1" or "D-2" or "DA-1");
        Assert.Equal(stand, Json.Text(Hole(p, "D-1").Geometrie!.Punkte));
        // Boden EG und Dach OG (Kote + Höhe von OG = 6200) – das Dach meldet sich ebenfalls
        Assert.DoesNotContain(e.Hinweise, x => x.Contains("D-2 " + NichtAufKote));
        Assert.Contains(e.Hinweise, x => x.StartsWith("Dach DA-1 " + NichtAufKote) && x.Contains("Kote + Geschosshöhe von OG jetzt 6200 mm"));
        // dem Vorschlag folgen: Decke um dz anheben → kein Hinweis mehr bei der nächsten Änderung
        var weiter = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("D-1"), ["dz"] = 200 }));
        Assert.DoesNotContain(weiter.Hinweise, x => x.Contains(NichtAufKote));
        Assert.All(Hole(p, "D-1").Geometrie!.Punkte, q => Assert.Equal(3200, q[2]));
    }

    [Fact]
    public void Nachfuehrung_KoteDesUmrissGeschosses_BodenMeldet_DeckeDarueberNicht()
    {
        var p = ZweiGeschosse();
        var e = Ok(Geschossaendern(p, "EG", new JsonObject { ["kote"] = 100 }));
        Assert.Contains(e.Hinweise, x => x.StartsWith("Decke D-2 " + NichtAufKote) && x.Contains("Oberkante 0 mm") && x.Contains("Kote von EG jetzt 100 mm"));
        Assert.DoesNotContain(e.Hinweise, x => x.Contains("D-1 " + NichtAufKote));    // liegt weiter auf der Kote von OG
        Assert.DoesNotContain(e.Hinweise, x => x.Contains("DA-1 " + NichtAufKote));
    }

    [Fact]
    public void Nachfuehrung_HoeheGeaendert_NurWoSieDasZielBestimmt()
    {
        var p = ZweiGeschosse();
        // Höhe EG: die Decke über EG liegt auf der Kote von OG, nicht auf Kote + Höhe von EG → still
        var eg = Ok(Geschossaendern(p, "EG", new JsonObject { ["hoehe"] = 3100 }));
        Assert.DoesNotContain(eg.Hinweise, x => x.Contains(NichtAufKote));
        // Höhe OG: das Dach liegt auf Kote + Höhe von OG → Hinweis
        var og = Ok(Geschossaendern(p, "OG", new JsonObject { ["hoehe"] = 2800 }));
        var h = Assert.Single(og.Hinweise, x => x.Contains(NichtAufKote));
        Assert.StartsWith("Dach DA-1 ", h);
        Assert.Contains("Oberkante 6000 mm", h);
        Assert.Contains("Kote + Geschosshöhe von OG jetzt 5800 mm", h);
        Assert.Contains("dz = -200", h);
        // anderes am Geschoss (Name) ändert nichts
        var name = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "OG", ["name"] = "Obergeschoss" }));
        Assert.DoesNotContain(name.Hinweise, x => x.Contains(NichtAufKote));
    }

    [Fact]
    public void Nachfuehrung_DeckeOhneUmrissAus_AufDerKote_UndAnderesGebaeude()
    {
        var p = Rechteck();
        Geschoss(p, "OG", 3000, 3000);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-2" }));
        Geschoss(p, "EG-2", 0, 3000, "GEB-2");
        Geschoss(p, "OG-2", 3000, 3000, "GEB-2");
        JsonObject Platte(string kz, string geschoss, double x0) => new()
        {
            ["klasse"] = "Decke", ["kennzeichen"] = kz, ["geschoss"] = geschoss,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([x0, 0, 3000], [x0 + 5000, 0, 3000], [x0 + 5000, 4000, 3000], [x0, 4000, 3000]), ["dicke"] = 250 },
        };
        Ok(p.Ausfuehren("element_anlegen", Platte("D-IFC", "OG", 0)));        // wie eine IFC-Decke, ohne umrissAus
        Ok(p.Ausfuehren("element_anlegen", Platte("D-NACHBAR", "OG-2", 20000)));
        var e = Ok(Geschossaendern(p, "OG", new JsonObject { ["kote"] = 3150 }));
        Assert.Contains(e.Hinweise, x => x.StartsWith("Decke D-IFC " + NichtAufKote) && x.Contains("Kote von OG jetzt 3150 mm") && x.Contains("dz = 150"));
        Assert.DoesNotContain(e.Hinweise, x => x.Contains("D-NACHBAR"));    // anderes Gebäude, eigenes OG unverändert
        // Rückgängig nimmt die Kote zurück; der Trockenlauf meldet dasselbe, ohne zu ändern
        Ok(p.Rueckgaengig());
        Assert.Equal(3000, Hole(p, "OG").Zahl("kote"));
        var trocken = Ok(Geschossaendern(p, "OG", new JsonObject { ["kote"] = 3150 }, trocken: true));
        Assert.Contains(trocken.Hinweise, x => x.StartsWith("Decke D-IFC " + NichtAufKote));
        Assert.Equal(3000, Hole(p, "OG").Zahl("kote"));
    }

    [Fact]
    public void Nachfuehrung_Beispielprojekt_GeschosskoteAendern_Hinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(Decke(p, "boden", 300, "UG"));
        Ok(Decke(p, "decke", 250, "UG"));
        Ok(Decke(p, "decke", 250, "EG"));
        Ok(Decke(p, "dach", 300, "1.OG"));
        var e = Ok(Geschossaendern(p, "1.OG", new JsonObject { ["kote"] = 3000 }));
        // D-3 (Decke über EG) lag auf der Kote von 1.OG, das Dach auf Kote + Höhe von 1.OG
        Assert.Contains(e.Hinweise, x => x.StartsWith("Decke D-3 " + NichtAufKote) && x.Contains("Kote von 1.OG jetzt 3000 mm"));
        Assert.Contains(e.Hinweise, x => x.StartsWith("Dach DA-1 " + NichtAufKote) && x.Contains("jetzt 5900 mm"));
        Assert.DoesNotContain(e.Hinweise, x => x.StartsWith("Decke D-1 ") || x.StartsWith("Decke D-2 "));
    }

    // ---------------------------------------------------------------- BKW: Dauer vorher/nachher

    /// <summary>BKW Hauptsitz Architektur (2412 Wände, 753 Decken): Dauer von Grafik.Koerper (alle Elemente) und zweier
    /// Schnitte quer durchs Gebäude, Überlappung der Schnittflächen Wand ∩ Decke. Läuft nur mit HLKS_ABNAHME=1 (Import rund
    /// 100 s) und wenn die Datei in %USERPROFILE%\HLKS_CAD_testdaten liegt; Zahlen nach %TEMP%\HLKS_CAD\b6_bkw.json.</summary>
    [Fact]
    public void Bkw_KoerperUndSchnitt_DauerUndUeberlappung()
    {
        if (Environment.GetEnvironmentVariable("HLKS_ABNAHME") != "1") return;
        var datei = IfcHilfe.Echt(@"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc");
        if (datei is null) { aus.WriteLine("Echte Datei fehlt: %USERPROFILE%\\HLKS_CAD_testdaten\\bkw_hauptsitz\\BKW-HS_1_ARCH_ARC.ifc"); return; }
        var p = IfcHilfe.Leer();
        var uhr = Stopwatch.StartNew();
        Ok(IfcHilfe.Uebernehmen(p, datei));
        double import = uhr.Elapsed.TotalSeconds;

        // Körper: erster Lauf (kalt) und drei weitere, Median
        var koerperZeiten = new List<double>();
        List<Koerper> koerper = [];
        for (int i = 0; i < 4; i++)
        {
            uhr.Restart();
            koerper = Grafik.Koerper(p);
            koerperZeiten.Add(uhr.Elapsed.TotalSeconds);
        }
        double median = koerperZeiten.Skip(1).Order().ElementAt(1);

        // Wände, deren Körper unter der Oberkante der Geometrie endet (gekürzt), und Wände ganz ohne Körper (ganz in einer Platte)
        var lagen = p.Modell.DerKlasse("Wand").Select(w => Wandgeometrie.Lage(p.Modell, w)).OfType<Wandlage>().ToDictionary(w => w.Id);
        int gekuerzt = koerper.Count(k => lagen.TryGetValue(k.Id, out var w) && w.Hoehe is not null && k.Teile.Count > 0
            && k.Teile.Max(t => t.Mitte![2] + t.Masse![2] / 2) < w.Oberkante - 1);
        var mitKoerper = koerper.Select(k => k.Id).ToHashSet();
        var platten = p.Modell.Elemente.Where(e => e.Klasse is "Decke" or "Dach" && e.Geometrie is { Art: "polygon", Dicke: > 0 } g && g.Punkte.Count >= 3)
            .Select(e => (E: e, Ok: e.Geometrie!.Punkte.Max(q => q[2]), Dicke: e.Geometrie.Dicke!.Value)).ToList();
        var ohne = new JsonArray();
        foreach (var w in lagen.Values.Where(w => w.Dicke > 0 && w.Hoehe > 0 && !mitKoerper.Contains(w.Id)).OrderBy(w => w.Wand.Kennzeichen).Take(25))
        {
            double mx = (w.Ax + w.Bx) / 2, my = (w.Ay + w.By) / 2;
            var in_ = platten.Where(x => x.Ok - x.Dicke < w.Oberkante && x.Ok > w.Unterkante && Polygone.Innen(x.E.Geometrie!.Punkte, mx, my))
                .Select(x => $"{x.E.Kennzeichen} {x.E.Name} {x.Ok - x.Dicke:0}…{x.Ok:0}");
            ohne.Add($"{w.Wand.Kennzeichen} «{w.Wand.Name}» {w.Unterkante:0}…{w.Oberkante:0} d {w.Dicke:0} l {w.Laenge:0}: {string.Join("; ", in_)}");
        }
        int ohneKoerper = lagen.Values.Count(w => w.Dicke > 0 && w.Hoehe > 0 && !mitKoerper.Contains(w.Id));

        // Vergleich im selben Lauf, abwechselnd (die Last des Rechners schwankt): mit Anschluss und mit allen Platten 1000 m
        // nach oben verschoben, also ohne gekürzte Wand
        var plattenKz = new JsonArray(platten.Select(x => (JsonNode)x.E.Kennzeichen).ToArray());
        var mitAnschluss = new List<double>(); var ohneAnschluss = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            uhr.Restart();
            Grafik.Koerper(p);
            mitAnschluss.Add(uhr.Elapsed.TotalSeconds);
            Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = plattenKz.DeepClone(), ["dz"] = 1e6 }));
            uhr.Restart();
            Grafik.Koerper(p);
            ohneAnschluss.Add(uhr.Elapsed.TotalSeconds);
            Ok(p.Rueckgaengig());
        }

        // Zwei Schnitte durch die Mitte der Wände (Nord–Süd, Ost–West)
        var pts = p.Modell.DerKlasse("Wand").SelectMany(w => w.Geometrie?.Punkte ?? []).ToList();
        double x0 = pts.Min(q => q[0]), x1 = pts.Max(q => q[0]), y0 = pts.Min(q => q[1]), y1 = pts.Max(q => q[1]);
        double xm = (x0 + x1) / 2, ym = (y0 + y1) / 2;
        var schnitte = new JsonObject();
        foreach (var (kz, a, b) in new[] { ("S-NS", new[] { xm, y0 - 5000 }, new[] { xm, y1 + 5000 }), ("S-OW", new[] { x0 - 5000, ym }, new[] { x1 + 5000, ym }) })
        {
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = kz,
                ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 100 },
                ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(a, b) },
            }));
            uhr.Restart();
            var g = Grafik.Plan(p, kz);
            double s = uhr.Elapsed.TotalSeconds;
            var (w, d, _, _) = Schnittflaechen(p, g);
            var du = Polygone.Vereinigen(d);
            double ueber = Flaeche(Schnitt(Polygone.Vereinigen(w), du));
            // je Wand: ganz in Platten (Konflikt im Modell, bleibt mit Doppelfüllung) oder angeschlossen (darf nicht überlappen)
            double ueberAngeschlossen = 0; var ganzDrin = new JsonArray();
            foreach (var t in g.Teile)
            {
                if (t.Id is null || p.Modell.Finde(t.Id) is not { } e || !p.Katalog.IstArt(e, "Wand")) continue;
                var pf = Polygone.Vereinigen(Pfade(t.Primitive.Where(x => x.Art == "polygon" && x.Stil.Stift == Stifte.Dick)));
                if (pf.Count == 0) continue;
                if (Polygone.Abziehen(pf, du).Count == 0) ganzDrin.Add(e.Kennzeichen);
                else ueberAngeschlossen += Flaeche(Schnitt(pf, du));
            }
            schnitte[kz] = new JsonObject
            {
                ["sekunden"] = Math.Round(s, 2), ["teile"] = g.Teile.Count,
                ["wandflaecheM2"] = Math.Round(Flaeche(Polygone.Vereinigen(w)) / 1e6, 2), ["deckenflaecheM2"] = Math.Round(Flaeche(du) / 1e6, 2),
                ["ueberlappungM2"] = Math.Round(ueber / 1e6, 3),
                ["ueberlappungAngeschlossenM2"] = Math.Round(ueberAngeschlossen / 1e6, 3),
                ["waendeGanzInPlatte"] = ganzDrin,
            };
        }

        var z = new JsonObject
        {
            ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
            ["importSekunden"] = Math.Round(import, 1),
            ["waende"] = p.Modell.DerKlasse("Wand").Count(),
            ["decken"] = p.Modell.DerKlasse("Decke").Count() + p.Modell.DerKlasse("Dach").Count(),
            ["koerper"] = koerper.Count,
            ["koerperSekunden"] = new JsonArray(koerperZeiten.Select(x => (JsonNode)Math.Round(x, 2)).ToArray()),
            ["koerperMedianSekunden"] = Math.Round(median, 2),
            ["abwechselndMitSekunden"] = new JsonArray(mitAnschluss.Select(x => (JsonNode)Math.Round(x, 3)).ToArray()),
            ["abwechselndOhneSekunden"] = new JsonArray(ohneAnschluss.Select(x => (JsonNode)Math.Round(x, 3)).ToArray()),
            ["abwechselndMitMedian"] = Math.Round(mitAnschluss.Order().ElementAt(2), 3),
            ["abwechselndOhneMedian"] = Math.Round(ohneAnschluss.Order().ElementAt(2), 3),
            ["waendeGekuerzt"] = gekuerzt,
            ["waendeOhneKoerper"] = ohneKoerper,
            ["waendeOhneKoerperBeispiele"] = ohne,
            ["schnitte"] = schnitte,
        };
        aus.WriteLine(z.ToJsonString(Json.Eingerueckt));
        var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b6_bkw.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        File.AppendAllText(ziel, z.ToJsonString(Json.Eingerueckt) + Environment.NewLine);

        // Entscheid «Decke läuft durch»: im Schnitt keine Doppelfüllung an den Anschlüssen (0.01 m² Spielraum für Rundung);
        // Wände ganz in einer Platte bleiben bewusst stehen; im 3D verschwindet keine Wand
        foreach (var (kz, s) in schnitte)
            Assert.True(s!["ueberlappungAngeschlossenM2"]!.GetValue<double>() < 0.01, $"{kz}: Wand ∩ Decke = {s["ueberlappungAngeschlossenM2"]} m²");
        Assert.Equal(0, ohneKoerper);
    }
}
