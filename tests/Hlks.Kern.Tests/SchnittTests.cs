using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Schnitt und Ansicht aus den Körpern (Modul 1a, C1): Schnittflächen, Sichtkanten mit Verdeckung, Blick, Sichttiefe,
/// Ansicht ohne Schnittflächen, Geschosskoten, Achsen, Leistung am Beispielprojekt. Bezug: SIA 400:2000 Ziff. B.3.3
/// (dick = Schnittkanten, dünn = Sichtkanten).</summary>
public class SchnittTests(ITestOutputHelper aus)
{
    // ---------------------------------------------------------------- Hilfen

    /// <summary>Vier Wände um 6000 × 4000 (Achsen), Dicke 200, Höhe 2600: W-1 Süd, W-2 Ost, W-3 Nord, W-4 West.</summary>
    static Projekt Rechteck2600(string? material = null)
    {
        var p = Neu();
        var par = new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["hoehe"] = 2600, ["geschlossen"] = true,
        };
        if (material is not null) par["material"] = material;
        Ok(p.Ausfuehren("wand_zeichnen", par));
        return p;
    }

    static void Ansicht(Projekt p, string kz, double[] a, double[] b, string art = "schnitt", string? blick = null, double? tiefe = null, JsonObject? mehr = null)
    {
        var eig = new JsonObject { ["art"] = art, ["massstab"] = 50 };
        if (blick is not null) eig["blick"] = blick;
        if (tiefe is not null) eig["tiefe"] = tiefe;
        foreach (var (k, v) in mehr ?? []) eig[k] = v?.DeepClone();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(a, b) },
        }));
    }

    /// <summary>Schnitt quer durch das Rechteck: Linie x = 3000 von Süd nach Nord, u = y + 1000; Blick links = nach Westen.</summary>
    static Plangrafik Quer(Projekt p, string? blick = "links", double? tiefe = null, string art = "schnitt", string kz = "S-1")
    {
        Ansicht(p, kz, [3000, -1000], [3000, 5000], art, blick, tiefe);
        return Grafik.Plan(p, kz);
    }

    static void SystemKw(Projekt p) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));

    static void Rohr(Projekt p, string kz, params double[][] punkte) =>
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject { ["system"] = "KW", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["dn"] = 100, ["punkte"] = Pts(punkte) }));

    static Grafikteil? TeilVon(Plangrafik g, Projekt p, string kz) => g.Teile.SingleOrDefault(t => t.Id == Hole(p, kz).Id);
    static List<Primitiv> Flaechen(Grafikteil t) => t.Primitive.Where(x => x.Art == "polygon").ToList();
    static List<Primitiv> Kanten(Grafikteil t) => t.Primitive.Where(x => x.Art is "linie" or "polylinie").ToList();
    static List<(double[] A, double[] B)> Segmente(Grafikteil t) => Kanten(t).SelectMany(x => x.Punkte.Zip(x.Punkte.Skip(1))).ToList();
    static bool Gleich(double[] a, double[] b, double tol = 1e-3) => Math.Abs(a[0] - b[0]) <= tol && Math.Abs(a[1] - b[1]) <= tol;
    static double Abstand(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

    /// <summary>Waagrechte Sichtkanten auf Höhe v.</summary>
    static List<(double Von, double Bis)> Waagrecht(Grafikteil t, double v) =>
        Segmente(t).Where(s => Math.Abs(s.A[1] - v) < 1e-3 && Math.Abs(s.B[1] - v) < 1e-3)
            .Select(s => (Math.Min(s.A[0], s.B[0]), Math.Max(s.A[0], s.B[0]))).OrderBy(s => s.Item1).ToList();

    /// <summary>Senkrechte Sichtkanten bei u.</summary>
    static List<(double Von, double Bis)> Senkrecht(Grafikteil t, double u, double tol = 1e-3) =>
        Segmente(t).Where(s => Math.Abs(s.A[0] - u) < tol && Math.Abs(s.B[0] - u) < tol)
            .Select(s => (Math.Min(s.A[1], s.B[1]), Math.Max(s.A[1], s.B[1]))).OrderBy(s => s.Item1).ToList();

    // ---------------------------------------------------------------- Wände

    [Fact]
    public void Schnitt_QuerDurchRechteck_ZweiSchnittflaechenRueckwandAlsSichtkante()
    {
        var p = Rechteck2600();
        var g = Quer(p);
        Assert.Equal("schnitt", g.Art);
        Assert.NotNull(g.Begrenzung);

        // genau zwei Schnittflächen (Kontur dick), 200 breit, 2600 hoch, an den richtigen u-Lagen
        var dick = g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "polygon" && x.Stil.Stift == Stifte.Dick).ToList();
        Assert.Equal(2, dick.Count);
        var w1 = TeilVon(g, p, "W-1")!;
        Assert.Equal("wand", w1.Ebene);
        var f1 = Assert.Single(Flaechen(w1));
        Nahe([900, 0, 1100, 2600], Box(f1.Punkte));
        Assert.Equal(200.0 * 2600, PolyFlaeche(f1), 3);
        Assert.Equal("schnitt", f1.Stil.Fuellung);                    // Material unbestimmt wie im Grundriss
        Assert.Equal(0.25, f1.Stil.Deckkraft);
        Nahe([4900, 0, 5100, 2600], Box(Assert.Single(Flaechen(TeilVon(g, p, "W-3")!)).Punkte));

        // Rückwand W-4 (parallel zur Linie, 2900 dahinter): nur Sichtkanten dünn, Ober- und Unterkante zwischen den Schnittflächen
        var w4 = TeilVon(g, p, "W-4")!;
        Assert.Empty(Flaechen(w4));
        Assert.All(Kanten(w4), x => Assert.Equal(Stifte.Duenn, x.Stil.Stift));
        foreach (var v in new[] { 0.0, 2600.0 })
        {
            var (von, bis) = Assert.Single(Waagrecht(w4, v));
            Assert.Equal(1100, von, 0.5);
            Assert.Equal(4900, bis, 0.5);
        }
        Assert.Empty(Senkrecht(w4, 900));                               // Kanten hinter den Schnittflächen verdeckt
        // Wand hinter dem Betrachter (Ost) fehlt
        Assert.Null(TeilVon(g, p, "W-2"));

        // Fangpunkte = Ecken, Fangkanten = Kanten der Schnittfläche
        foreach (var e in new double[][] { [900, 0], [1100, 0], [1100, 2600], [900, 2600] })
            Assert.Contains(w1.Fangpunkte!, f => Gleich(f, e));
        Assert.Contains(w1.Fangkanten!, k => (Gleich(k[0], [900, 0]) && Gleich(k[1], [1100, 0])) || (Gleich(k[1], [900, 0]) && Gleich(k[0], [1100, 0])));
        Assert.Equal(4, w1.Fangkanten!.Count);
        // gerade Sichtkanten sind ebenfalls fangbar
        Assert.Contains(w4.Fangkanten!, k => Math.Abs(k[0][1] - 2600) < 1e-3 && Math.Abs(k[1][1] - 2600) < 1e-3);
    }

    [Fact]
    public void Schnitt_MaterialBeton_SchraffurWieImGrundriss()
    {
        var p = Rechteck2600("beton");
        var f = Assert.Single(Flaechen(TeilVon(Quer(p), p, "W-1")!));
        Assert.Equal("beton", f.Stil.Muster);
        Assert.Equal("papier", f.Stil.Fuellung);
        Assert.Equal(Stifte.Dick, f.Stil.Stift);
    }

    [Fact]
    public void Schnitt_TiefeKleinerAlsAbstandZurRueckwand_RueckwandFehlt()
    {
        var p = Rechteck2600();
        var kurz = Quer(p, tiefe: 2500);
        Assert.Null(TeilVon(kurz, p, "W-4"));
        Assert.Equal(2, kurz.Teile.SelectMany(t => t.Primitive).Count(x => x.Art == "polygon" && x.Stil.Stift == Stifte.Dick));
        Assert.NotNull(TeilVon(Quer(p, tiefe: 3500, kz: "S-2"), p, "W-4"));
    }

    [Fact]
    public void Schnitt_BlickLinksRechts_NichtGespiegeltVorneHintenWechselt()
    {
        var p = Rechteck2600();
        var links = Quer(p, "links");
        var rechts = Quer(p, "rechts", kz: "S-2");
        var ohne = Quer(p, null, kz: "S-3");
        // Blick links: u ab A (Süd); Blick rechts: u ab B (Nord) – so sieht der Betrachter es, nicht seitenverkehrt
        Nahe([900, 0, 1100, 2600], Box(Assert.Single(Flaechen(TeilVon(links, p, "W-1")!)).Punkte));
        Nahe([4900, 0, 5100, 2600], Box(Assert.Single(Flaechen(TeilVon(links, p, "W-3")!)).Punkte));
        Nahe([4900, 0, 5100, 2600], Box(Assert.Single(Flaechen(TeilVon(rechts, p, "W-1")!)).Punkte));
        Nahe([900, 0, 1100, 2600], Box(Assert.Single(Flaechen(TeilVon(rechts, p, "W-3")!)).Punkte));
        Assert.NotNull(TeilVon(links, p, "W-4"));
        Assert.Null(TeilVon(links, p, "W-2"));
        Assert.NotNull(TeilVon(rechts, p, "W-2"));
        Assert.Null(TeilVon(rechts, p, "W-4"));
        var (von, bis) = Assert.Single(Waagrecht(TeilVon(rechts, p, "W-2")!, 2600));
        Assert.Equal(1100, von, 0.5);
        Assert.Equal(4900, bis, 0.5);
        Assert.NotNull(TeilVon(ohne, p, "W-4"));                      // ohne blick: links
        Assert.Null(TeilVon(ohne, p, "W-2"));
    }

    [Fact]
    public void Schnitt_DurchTuer_SturzGeschnittenUntenFreiLeibungSichtbar()
    {
        var p = Rechteck2600();
        Ok(Tuer(p));                                                    // W-1, Mitte x = 3000, 900 breit, 2100 hoch
        var g = Quer(p);
        var w1 = TeilVon(g, p, "W-1")!;
        var sturz = Assert.Single(Flaechen(w1));                        // nur der Sturz ist Wand, unten frei
        Nahe([900, 2100, 1100, 2600], Box(sturz.Punkte));
        Assert.Equal(Stifte.Dick, sturz.Stil.Stift);
        // Türblatt geschnitten: Ebene oeffnung, kein Hauptbauteil (nicht dick)
        var tuer = TeilVon(g, p, "T-1")!;
        Assert.Equal("oeffnung", tuer.Ebene);
        var blatt = Assert.Single(Flaechen(tuer));
        Nahe([980, 0, 1020, 2100], Box(blatt.Punkte));
        Assert.NotEqual(Stifte.Dick, blatt.Stil.Stift);
        // Leibung (Wandstück links der Tür, 450 hinter der Ebene) als Sichtkante bis unter den Sturz
        var (von, bis) = Assert.Single(Senkrecht(w1, 900));
        Assert.Equal(0, von, 0.5);
        Assert.Equal(2100, bis, 0.5);
        Assert.All(Kanten(w1), x => Assert.Equal(Stifte.Duenn, x.Stil.Stift));
    }

    [Fact]
    public void Schnitt_DurchFenster_BruestungUndSturzGeschnitten()
    {
        var p = Rechteck2600();
        Ok(Fenster(p));                                                 // W-1, Mitte x = 3000, 1200 breit, 1200 hoch, Brüstung 900
        var g = Quer(p);
        var f = Flaechen(TeilVon(g, p, "W-1")!).OrderBy(x => Box(x.Punkte)[1]).ToList();
        Assert.Equal(2, f.Count);
        Nahe([900, 0, 1100, 900], Box(f[0].Punkte));                     // Brüstung
        Nahe([900, 2100, 1100, 2600], Box(f[1].Punkte));                 // Sturz
        var glas = Assert.Single(Flaechen(TeilVon(g, p, "F-1")!));
        Nahe([980, 900, 1020, 2100], Box(glas.Punkte));
        Assert.Equal("glas", glas.Stil.Muster);
    }

    [Fact]
    public void Ansicht_KeineDickenSchnittkonturen_KoerperAnDerLinieDuennUmrissen()
    {
        var p = Rechteck2600();
        Ok(Tuer(p));
        var g = Quer(p, art: "ansicht");
        Assert.Equal("ansicht", g.Art);
        var alle = g.Teile.SelectMany(t => t.Primitive).ToList();
        Assert.DoesNotContain(alle, x => x.Stil.Stift == Stifte.Dick);
        Assert.DoesNotContain(alle, x => x.Art == "polygon" && x.Stil.Fuellung is not null);   // keine Schnittflächen
        var w1 = TeilVon(g, p, "W-1")!;
        Assert.Empty(Flaechen(w1));
        var (von, bis) = Assert.Single(Waagrecht(w1, 2600));           // Oberkante des Sturzes an der Linie
        Assert.Equal(900, von, 0.5);
        Assert.Equal(1100, bis, 0.5);
        Assert.NotNull(TeilVon(g, p, "W-4"));
        Assert.Null(TeilVon(g, p, "W-2"));
    }

    [Fact]
    public void Ansicht_FassadeOhneNaehte_FensterleibungNurAnDerOeffnung()
    {
        var p = Rechteck2600();
        Ok(Fenster(p));                                                 // W-1, x 2400…3600, Brüstung 900, Sturz 2100
        Ansicht(p, "A-S", [-1000, -2000], [7000, -2000], art: "ansicht"); // Südansicht, Blick nach Norden, u = x + 1000
        var g = Grafik.Plan(p, "A-S");
        var w1 = TeilVon(g, p, "W-1")!;
        // Wandstücke neben, unter und über dem Fenster liegen in einer Ebene: senkrechte Kanten nur an der Öffnung
        var (von, bis) = Assert.Single(Senkrecht(w1, 3400));
        Assert.Equal(900, von, 0.5);
        Assert.Equal(2100, bis, 0.5);
        // Ecken: die Stirnseiten der Ost- und Westwand liegen in der Fassadenebene, ihre Innenkanten sind Nähte
        var alle = g.Teile.Where(t => t.Ebene == "wand").ToList();
        Assert.DoesNotContain(alle, t => Senkrecht(t, 1100).Count > 0 || Senkrecht(t, 6900).Count > 0);
        Assert.Single(alle.SelectMany(t => Senkrecht(t, 900)));          // Aussenkante nur einmal
        // Unter- und Oberkante durchgehend (Stücke ohne Lücke von 900 bis 7100)
        foreach (var v in new[] { 0.0, 2600.0 })
        {
            var stuecke = alle.SelectMany(t => Waagrecht(t, v)).OrderBy(s => s.Von).ToList();
            Assert.Equal(900, stuecke[0].Von, 0.5);
            double ende = stuecke[0].Bis;
            foreach (var s in stuecke.Skip(1)) { Assert.True(s.Von <= ende + 0.5, $"Lücke bei u = {ende} auf v = {v}"); ende = Math.Max(ende, s.Bis); }
            Assert.Equal(7100, ende, 0.5);
        }
        // Das Glas (in der Wandachse) liegt mit seinem Umriss genau hinter den Leibungskanten: verdeckt, die Öffnung zeichnet die Wand
    }

    [Fact]
    public void Schnitt_RaumWirdWederGeschnittenNochGezeigt_FilterDerAnsichtGelten()
    {
        var p = Rechteck2600();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nummer"] = "0.01" }));
        var g = Quer(p);
        Assert.Null(TeilVon(g, p, "R-1"));
        Ansicht(p, "S-X", [3000, -1000], [3000, 5000], mehr: new JsonObject { ["klassenAusblenden"] = new JsonArray("Wand") });
        Assert.DoesNotContain(Grafik.Plan(p, "S-X").Teile, t => t.Ebene == "wand");
        Ansicht(p, "S-G", [3000, -1000], [3000, 5000], mehr: new JsonObject { ["gewerke"] = new JsonArray("sanitaer") });
        Assert.DoesNotContain(Grafik.Plan(p, "S-G").Teile, t => t.Ebene == "wand");
    }

    [Fact]
    public void Schnitt_LinieOhneLaenge_FehlerMitVorschlag()
    {
        var p = Rechteck2600();
        Ansicht(p, "S-0", [1000, 0], [1000, 0]);
        var f = Assert.Throws<HlksFehler>(() => Grafik.Plan(p, "S-0"));
        Assert.Contains("Länge 0", f.Message);
    }

    // ---------------------------------------------------------------- Leitungen

    [Fact]
    public void Schnitt_RohrQuerZurEbene_KreisSchraegEllipseParallelSichtkante()
    {
        var p = Neu();
        SystemKw(p);
        Rohr(p, "RO-1", [1000, -2000, 1500], [1000, 2000, 1500]);       // senkrecht zur Ebene → Kreis
        Rohr(p, "RO-2", [0, -2000, 1500], [4000, 2000, 1500]);          // 45° zur Ebene → Ellipse
        Rohr(p, "RO-3", [0, 1000, 500], [4000, 1000, 500]);             // parallel, 1000 hinter der Ebene → Sichtkante
        Ansicht(p, "S-R", [0, 0], [5000, 0]);                            // Blick links = nach Norden, u = x
        var g = Grafik.Plan(p, "S-R");

        var t1 = TeilVon(g, p, "RO-1")!;
        Assert.Equal("leitung", t1.Ebene);
        var kreis = Assert.Single(Flaechen(t1));
        Assert.All(kreis.Punkte, q => Assert.Equal(50, Abstand(q, [1000, 1500]), 0.01));   // DN 100 → Radius 50
        Assert.Equal(Stifte.Dick, kreis.Stil.Stift);
        Assert.NotNull(kreis.Stil.Fuellung);

        var ellipse = Assert.Single(Flaechen(TeilVon(g, p, "RO-2")!));
        var b = Box(ellipse.Punkte);
        Assert.Equal(100 * Math.Sqrt(2), b[2] - b[0], 0.01);           // quer: Durchmesser / sin 45°
        Assert.Equal(100, b[3] - b[1], 0.01);
        Assert.Equal(2000, (b[0] + b[2]) / 2, 0.01);

        var t3 = TeilVon(g, p, "RO-3")!;
        Assert.Empty(Flaechen(t3));
        Assert.All(Kanten(t3), x => Assert.Equal(Stifte.Duenn, x.Stil.Stift));
        foreach (var v in new[] { 450.0, 550.0 })
        {
            var (von, bis) = Assert.Single(Waagrecht(t3, v));
            Assert.Equal(0, von, 0.01);
            Assert.Equal(4000, bis, 0.01);
        }
        Assert.Single(Senkrecht(t3, 0));                                // Rohrende
    }

    [Fact]
    public void Verdeckung_RohrHinterWandFehlt_RohrVorWandUnterbrichtIhreKanten()
    {
        var p = Rechteck2600();
        SystemKw(p);
        Rohr(p, "RO-H", [-1000, 500, 1000], [-1000, 3500, 1000]);       // ausserhalb hinter der Westwand
        Rohr(p, "RO-V", [1000, 2000, -500], [1000, 2000, 3000]);        // senkrecht im Raum vor der Westwand, u = 3000
        var g = Quer(p);
        Assert.Null(TeilVon(g, p, "RO-H"));
        var w4 = TeilVon(g, p, "W-4")!;
        var oben = Waagrecht(w4, 2600);
        Assert.Equal(2, oben.Count);                                    // vom Rohr unterbrochen
        Assert.Equal(2950, oben[0].Bis, 0.5);
        Assert.Equal(3050, oben[1].Von, 0.5);
        var rohr = TeilVon(g, p, "RO-V")!;
        Assert.Single(Senkrecht(rohr, 2950, 0.01));                     // Mantellinien
        Assert.Single(Senkrecht(rohr, 3050, 0.01));
    }

    [Fact]
    public void Verdeckung_RohrSchraegDurchWand_TeilHinterDerWandVerdeckt()
    {
        var p = Rechteck2600();
        SystemKw(p);
        Rohr(p, "RO-S", [2000, -500, 1200], [3500, 3500, 1200]);        // schräg durch die Südwand nach aussen
        Ansicht(p, "S-W", [-1000, 2000], [7000, 2000], blick: "rechts"); // Blick nach Süden, u ab B: u = 7000 − x
        var t = TeilVon(Grafik.Plan(p, "S-W"), p, "RO-S")!;
        // Achse kreuzt die Ebene y = 2000 bei x = 2937.5 (Schnittellipse), die Innenfläche der Wand (y = 100) bei x = 2225:
        // dahinter (in der Wand und draussen) ist das Rohr verdeckt
        Assert.NotNull(Assert.Single(Flaechen(t)));
        foreach (var v in new[] { 1150.0, 1250.0 })
        {
            var (von, bis) = Assert.Single(Waagrecht(t, v));
            Assert.Equal(4062.5, von, 2.5);                             // berührt die Ellipse tangential, endet in ihrer Kontur
            Assert.Equal(4775, bis, 1.0);
        }
    }

    [Fact]
    public void Verdeckung_RohrknickRundOhneNaht()
    {
        var p = Neu();
        SystemKw(p);
        Rohr(p, "RO-K", [0, 1000, 1000], [2000, 1000, 1000], [2000, 1000, 2500]);
        Ansicht(p, "S-K", [0, 0], [5000, 0]);
        var t = TeilVon(Grafik.Plan(p, "S-K"), p, "RO-K")!;
        double[] knick = [2000, 1000];
        // keine Kante durch das Innere des Knicks (Stirnkanten der Rohrstücke sind Nähte)
        Assert.All(Segmente(t), s => Assert.True(Polygone.AbstandZurStrecke(knick[0], knick[1], s.A[0], s.A[1], s.B[0], s.B[1]) > 45,
            $"Kante ({s.A[0]}, {s.A[1]}) – ({s.B[0]}, {s.B[1]}) läuft durch den Knick"));
        // Aussenecke rund: Bogen der Knickkugel
        Assert.Contains(t.Primitive, x => x.Art == "polylinie" && x.Punkte.Count > 3 && x.Punkte.All(q => Math.Abs(Abstand(q, knick) - 50) < 0.2));
        // Innenecke: obere Mantellinie des waagrechten Stücks endet an der linken des senkrechten
        Assert.Equal(1950, Waagrecht(t, 1050).Max(s => s.Bis), 0.5);
        Assert.Equal(1050, Senkrecht(t, 1950, 0.01).Min(s => s.Von), 0.5);
    }

    // ---------------------------------------------------------------- Geschosse und Achsen

    [Fact]
    public void Schnitt_GeschosskotenUndAchsen()
    {
        var p = Rechteck2600();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB",
            ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Achse", ["kennzeichen"] = "AX-B", ["eigenschaften"] = new JsonObject { ["bezeichnung"] = "B" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([-1000, 2000], [7000, 2000]) },   // kreuzt bei y = 2000
        }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Achse", ["kennzeichen"] = "AX-1", ["eigenschaften"] = new JsonObject { ["bezeichnung"] = "1" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([1000, -2000], [1000, 6000]) },   // parallel
        }));
        var g = Quer(p);

        var eg = TeilVon(g, p, "EG")!;
        Assert.Equal("achse", eg.Ebene);
        Assert.Contains(eg.Primitive, x => x.Art == "text" && x.Text == "±0.00 EG");
        var linie = eg.Primitive.Single(x => x.Art == "linie");
        Assert.Equal("strich", linie.Stil.Linie);
        Assert.Equal(Stifte.Duenn, linie.Stil.Stift);
        Assert.All(linie.Punkte, q => Assert.Equal(0, q[1], 6));
        Assert.True(linie.Punkte.Min(q => q[0]) < 900 && linie.Punkte.Max(q => q[0]) > 5100);   // über die ganze Breite
        Assert.Contains(TeilVon(g, p, "OG")!.Primitive, x => x.Art == "text" && x.Text == "+3.00 OG");

        var ax = TeilVon(g, p, "AX-B")!;
        Assert.Equal("achse", ax.Ebene);
        var al = ax.Primitive.Single(x => x.Art == "linie");
        Assert.Equal("strichpunkt", al.Stil.Linie);
        Assert.All(al.Punkte, q => Assert.Equal(3000, q[0], 6));        // senkrecht bei u = y + 1000
        Assert.True(al.Punkte.Max(q => q[1]) > 3000 && al.Punkte.Min(q => q[1]) < 0);
        var kreis = ax.Primitive.Single(x => x.Art == "kreis");
        Assert.Equal(4.0 * 50, kreis.Radius);                            // Ø 8 mm Papier bei 1:50
        Assert.Contains(ax.Primitive, x => x.Art == "text" && x.Text == "B");
        Assert.Null(TeilVon(g, p, "AX-1"));
    }

    // ---------------------------------------------------------------- Beispielprojekt

    /// <summary>Median der Laufzeit in ms nach einem Aufwärmlauf (JIT).</summary>
    static (double Median, double[] Laeufe) Laufzeit(Action lauf, int anzahl = 5)
    {
        lauf();
        var zeiten = new double[anzahl];
        for (int i = 0; i < anzahl; i++)
        {
            var uhr = Stopwatch.StartNew();
            lauf();
            zeiten[i] = uhr.Elapsed.TotalMilliseconds;
        }
        var sortiert = zeiten.Order().ToArray();
        return (sortiert[anzahl / 2], zeiten);
    }

    [Fact]
    public void Beispielprojekt_SchnittQuerDurchsGebaeude_NichtLeerUndSchnell()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ansicht(p, "S-BSP", [6000, -2000], [6000, 12000]);              // Nord-Süd durch die Mitte, Blick nach Westen
        Ansicht(p, "A-SUED", [-1000, -3000], [13000, -3000], art: "ansicht");   // Südansicht, Blick nach Norden
        var g = Grafik.Plan(p, "S-BSP");
        var a = Grafik.Plan(p, "A-SUED");
        var (ms, laeufe) = Laufzeit(() => Grafik.Plan(p, "S-BSP"));
        var (msA, laeufeA) = Laufzeit(() => Grafik.Plan(p, "A-SUED"));
        aus.WriteLine($"Beispielprojekt ({p.Modell.Elemente.Count} Elemente): Schnitt Median {ms:F1} ms [{string.Join(", ", laeufe.Select(x => x.ToString("F1")))}], " +
                      $"{g.Teile.Count} Teile, {g.Teile.Sum(t => t.Primitive.Count)} Primitive; Ansicht Median {msA:F1} ms " +
                      $"[{string.Join(", ", laeufeA.Select(x => x.ToString("F1")))}], {a.Teile.Count} Teile");
        // Ziel 200 ms (Auftrag C1); die Testgrenze ist grosszügig, weil parallele Bauläufe die Maschine belasten
        Assert.True(ms < 1000, $"Schnitt dauerte im Median {ms:F0} ms");
        Assert.True(msA < 1000, $"Ansicht dauerte im Median {msA:F0} ms");

        Assert.NotEmpty(g.Teile);
        foreach (var kz in new[] { "W-UG-S", "W-UG-N", "W-EG-S", "W-EG-N", "W-1.OG-S", "W-1.OG-N" })
        {
            var f = Assert.Single(Flaechen(TeilVon(g, p, kz)!));
            Assert.Equal(Stifte.Dick, f.Stil.Stift);
            Assert.Equal(350, Box(f.Punkte)[2] - Box(f.Punkte)[0], 3);
        }
        Assert.Contains(g.Teile, t => t.Ebene == "leitung");
        Assert.Contains(g.Teile, t => t.Ebene == "achse" && t.Primitive.Any(x => x.Text == "±0.00 EG"));
        Assert.NotEmpty(a.Teile);
        Assert.DoesNotContain(a.Teile.SelectMany(t => t.Primitive), x => x.Stil.Stift == Stifte.Dick);
    }
}
