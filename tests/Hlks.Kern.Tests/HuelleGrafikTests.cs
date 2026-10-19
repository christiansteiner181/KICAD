using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Darstellung «Hülle» im Grundriss (Modul 1c, Auftrag C1b) und Raumumriss mit Insel ohne Steg (Befund B1).
/// Alle Masse sind Handrechnungen: Raum innen 6.0 × 4.0 m, Wände Dicke 200, Massstab 1:50 (2 Papier-mm = 100 mm Modell).</summary>
public class HuelleGrafikTests
{
    /// <summary>EG (Kote 0, Höhe 3000), Wände um die Achsen (−100, −100) … (6100, 4100), Raum R innen (0, 0) … (6000, 4000), Grundriss AN-EG 1:50.</summary>
    static Projekt Raum(bool huelle = true)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000, 2600, "0.01");
        Ansicht(p, huelle ? "huelle" : null);
        return p;
    }

    static void Ansicht(Projekt p, string? darstellung)
    {
        var eig = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 };
        if (darstellung is not null) eig["darstellung"] = darstellung;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-EG", ["geschoss"] = "EG", ["eigenschaften"] = eig }));
    }

    static List<Primitiv> HuellePrim(Plangrafik g) => g.Teile.Where(t => t.Ebene == "huelle").SelectMany(t => t.Primitive).ToList();

    static List<Primitiv> Streifen(Plangrafik g, string farbe) =>
        HuellePrim(g).Where(x => x.Art == "polygon" && x.Stil.Fuellung == farbe && x.Stil.Deckkraft != HuelleGrafik.BodenDeckkraft).ToList();

    static double Inhalt(Primitiv x) => Math.Abs(Polygone.Flaecheninhalt(x.Punkte));

    // ------------------------------------------------------------------ Streifen

    [Fact]
    public void Rechteckraum_VierStreifenAussen_AufDerRaumseiteDerKante()
    {
        var p = Raum();
        var g = Grafik.Plan(p, "AN-EG");
        var s = Streifen(g, HuelleGrafik.FarbeAussen);
        Assert.Equal(4, s.Count);
        // 2 Papier-mm = 100 mm Modell, Streifenmitte 1 Papier-mm in den Raum → er liegt an der Wandfläche an (0 … 100 mm)
        var boxen = s.Select(x => Box(x.Punkte)).ToList();
        foreach (var erwartet in new[] { new[] { 0.0, 0, 6000, 100 }, new[] { 5900.0, 0, 6000, 4000 }, new[] { 0.0, 3900, 6000, 4000 }, new[] { 0.0, 0, 100, 4000 } })
            Assert.Contains(boxen, b => b.Zip(erwartet, (a, e) => Math.Abs(a - e) < 0.01).All(x => x));
        Assert.All(s, x => Assert.True(Math.Abs(Inhalt(x) - (x.Punkte.Max(q => q[0]) - x.Punkte.Min(q => q[0])) * (x.Punkte.Max(q => q[1]) - x.Punkte.Min(q => q[1]))) < 0.01));
        Assert.All(s, x => Assert.True(x.Stil.NurBildschirm));
        Assert.All(s, x => Assert.Null(x.Stil.Stift));
        // Farbe aussen als Konstante der Quelle
        Assert.Equal("#e0482f", HuelleGrafik.FarbeAussen);
    }

    [Fact]
    public void Rechteckraum_BodenGegenErdreich_DezenteFlaeche()
    {
        var g = Grafik.Plan(Raum(), "AN-EG");
        var boden = HuellePrim(g).Where(x => x.Stil.Deckkraft == HuelleGrafik.BodenDeckkraft).ToList();
        Assert.Single(boden);
        Assert.Equal(HuelleGrafik.FarbeErdreich, boden[0].Stil.Fuellung);   // unterstes Geschoss → Erdreich
        Assert.Equal(24e6, Inhalt(boden[0]), 1);
        Assert.True(boden[0].Stil.NurBildschirm);
    }

    [Fact]
    public void Innenwand_ZweiRaeume_BeheiztDezentAufJederSeite()
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 10100, 4100, 200);
        RaumgrenzenHilfe.Wand(p, "EG", [5000, -100], [5000, 4100], 200);
        RaumgrenzenHilfe.Raum(p, "EG", 2500, 2000, 2600, "0.01");
        RaumgrenzenHilfe.Raum(p, "EG", 7500, 2000, 2600, "0.02");
        Ansicht(p, "huelle");
        var g = Grafik.Plan(p, "AN-EG");
        var beheizt = Streifen(g, HuelleGrafik.FarbeBeheizt);
        Assert.Equal(2, beheizt.Count);   // je Raum eine Seite der Innenwand
        // 1 Papier-mm = 50 mm breit, 4000 lang; links von x = 4900 … 4900-50, rechts ab 5100
        var boxen = beheizt.Select(x => Box(x.Punkte)).OrderBy(b => b[0]).ToList();
        Nahe([4850, 0, 4900, 4000], boxen[0], 0.01);
        Nahe([5100, 0, 5150, 4000], boxen[1], 0.01);
        Assert.All(beheizt, x => Assert.Equal(HuelleGrafik.BeheiztDeckkraft, x.Stil.Deckkraft));
        // Aussenwände: 6 Streifen aussen (je Raum Süd, Nord, und aussen Ost bzw. West)
        Assert.Equal(6, Streifen(g, HuelleGrafik.FarbeAussen).Count);
        Assert.Empty(Streifen(g, HuelleGrafik.FarbeUnbeheizt));
    }

    [Fact]
    public void UnbeheizterNachbar_FarbeUnbeheizt()
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 10100, 4100, 200);
        RaumgrenzenHilfe.Wand(p, "EG", [5000, -100], [5000, 4100], 200);
        RaumgrenzenHilfe.Raum(p, "EG", 2500, 2000, 2600, "0.01");
        var nachbar = RaumgrenzenHilfe.Raum(p, "EG", 7500, 2000, 2600, "0.02");
        RaumgrenzenHilfe.Eigenschaften(p, nachbar, new JsonObject { ["beheizt"] = false });
        Ansicht(p, "huelle");
        var s = Streifen(Grafik.Plan(p, "AN-EG"), HuelleGrafik.FarbeUnbeheizt);
        Assert.Single(s);   // nur die Seite des beheizten Raums gegen den unbeheizten (die andere Seite ist beheizt gegen unbeheizt: Raum 2 sieht beheizt)
        Assert.True(Box(s[0].Punkte)[2] <= 4900.01);
    }

    [Fact]
    public void Fenster_WandstreifenUnterbrochen_FensterHellerUndGestrichelt()
    {
        var p = Raum();
        RaumgrenzenHilfe.Fenster(p, "W-1", 3100, 1200, 1200, 900);   // Süd, Mitte x = 3000 (Abstand ab Wandanfang bei x = −100), 1.2 m breit
        var g = Grafik.Plan(p, "AN-EG");
        var aussen = Streifen(g, HuelleGrafik.FarbeAussen);
        var fenster = aussen.Where(x => x.Stil.Linie == "strich").ToList();
        Assert.Single(fenster);
        Assert.Equal(HuelleGrafik.OeffnungDeckkraft, fenster[0].Stil.Deckkraft);
        Assert.Equal(HuelleGrafik.FarbeAussen, fenster[0].Stil.Farbe);
        Assert.Equal(Stifte.Duenn, fenster[0].Stil.Stift);
        Nahe([2400, 0, 3600, 100], Box(fenster[0].Punkte), 0.01);
        // Wandstreifen Süd: zwei Teile, zusammen 6000 − 1200 lang; die anderen drei Wände bleiben je ein Streifen
        var sued = aussen.Where(x => x.Stil.Linie != "strich" && Box(x.Punkte)[3] <= 100.01 && Box(x.Punkte)[2] - Box(x.Punkte)[0] > 150).ToList();
        Assert.Equal(2, sued.Count);
        Assert.Equal(4800, sued.Sum(x => Box(x.Punkte)[2] - Box(x.Punkte)[0]), 0.01);
        Assert.Equal(6, aussen.Count);
    }

    // ------------------------------------------------------------------ Ebene, Ein/Aus, nur Bildschirm

    [Fact]
    public void Ebene_Huelle_UeberRaeumenUnterWaenden()
    {
        var r = Ebenen.Reihenfolge;
        Assert.True(r.ToList().IndexOf("raum") < r.ToList().IndexOf("huelle"));
        Assert.True(r.ToList().IndexOf("huelle") < r.ToList().IndexOf("wand"));
        Assert.True(Ebenen.Rang("huelle") > Ebenen.Rang("raum") && Ebenen.Rang("huelle") < Ebenen.Rang("wand"));
        var g = Grafik.Plan(Raum(), "AN-EG");
        Assert.Contains(g.Teile, t => t.Ebene == "huelle" && t.Id is null);
    }

    [Fact]
    public void QuelleHuelle_StehtVorHlksGrafik_UndIstFuerKeinElementZustaendig()
    {
        var namen = Grafik.Quellen.Select(q => q.Name).ToList();
        Assert.True(namen.IndexOf("huelle") >= 0 && namen.IndexOf("huelle") < namen.IndexOf("hlks"));
        var p = Raum();
        var q = Grafik.Quellen.Single(x => x.Name == "huelle");
        Assert.DoesNotContain(p.Modell.Elemente, e => q.Zustaendig(p.Katalog, e));
    }

    [Fact]
    public void DarstellungStandard_OderOhneAngabe_KeineHuelle()
    {
        Assert.Empty(HuellePrim(Grafik.Plan(Raum(false), "AN-EG")));
        var p = Raum();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = "standard" } }));
        Assert.Empty(HuellePrim(Grafik.Plan(p, "AN-EG")));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = "huelle" } }));
        Assert.NotEmpty(HuellePrim(Grafik.Plan(p, "AN-EG")));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = null } }));
        Assert.Empty(HuellePrim(Grafik.Plan(p, "AN-EG")));
    }

    [Fact]
    public void GewerkeOhneArchitektur_KeineHuelle()
    {
        var p = Raum();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["gewerke"] = new JsonArray("sanitaer") } }));
        Assert.Empty(HuellePrim(Grafik.Plan(p, "AN-EG")));
    }

    [Fact]
    public void NurBildschirm_AllePrimitive_UndNichtImPlanPdf()
    {
        var g = Grafik.Plan(Raum(), "AN-EG");
        Assert.All(HuellePrim(g), x => Assert.True(x.Stil.NurBildschirm));
        // Plan-PDF: PdfZeichner lässt NurBildschirm weg – die Hülle darf nur Primitive dieser Art enthalten
        Assert.DoesNotContain(g.Teile.Where(t => t.Ebene == "huelle").SelectMany(t => t.Primitive), x => !x.Stil.NurBildschirm);
    }

    // ------------------------------------------------------------------ Raum mit Insel ohne Steg

    /// <summary>Raum 10 × 8 m mit Insel 2 × 2 m (4000 … 6000 × 3000 … 5000): Ring mit Steg der Breite 0 wie aus IfcRaeume/RaeumeAusWaenden.</summary>
    static (Projekt P, List<double[]> Ring) MitInsel(double aufmass = 0)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        var aussen = new List<double[]> { new[] { 0.0, 0 }, new[] { 10000.0, 0 }, new[] { 10000.0, 8000 }, new[] { 0.0, 8000 } };
        var insel = new List<double[]> { new[] { 4000.0 - aufmass, 3000 - aufmass }, new[] { 6000.0 + aufmass, 3000 - aufmass }, new[] { 6000.0 + aufmass, 5000 + aufmass }, new[] { 4000.0 - aufmass, 5000 + aufmass } };
        var ring = RaeumeAusWaenden.MitInseln(aussen, [insel], out int ohneSteg);
        Assert.Equal(0, ohneSteg);
        Assert.Equal(4 + 4 + 1 + 2, ring.Count);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-I", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["nummer"] = "0.01", ["lichteHoehe"] = 2600 },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts(ring.ToArray()) },
        }));
        Ansicht(p, null);
        return (p, ring);
    }

    [Fact]
    public void RaumMitInsel_UmrissOhneSteg_AussenringUndLoch()
    {
        var (p, _) = MitInsel();
        var g = Grafik.Plan(p, "AN-EG");
        var teil = Teil(g, p, "R-I");
        var polys = teil.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, polys.Count);   // Füllung, Umriss
        Assert.All(polys, x =>
        {
            Assert.Equal(4, x.Punkte.Count);                       // Aussenring ohne Steg
            Assert.NotNull(x.Loecher);
            Assert.Single(x.Loecher!);
            Assert.Equal(4, x.Loecher![0].Count);                  // Loch = Insel
            Assert.Equal(80e6 - 4e6, PolyFlaeche(x), 1);
        });
        Assert.Equal(polys[0].Punkte.Count + polys[0].Loecher![0].Count, teil.Fangpunkte!.Count);   // keine Doppelpunkte des Stegs
        // kein Fangkantenpaar a → b und b → a (so erschiene ein Steg)
        string Schl(double[] a) => $"{Math.Round(a[0], 1)}|{Math.Round(a[1], 1)}";
        var kanten = teil.Fangkanten!.Select(k => (Schl(k[0]), Schl(k[1]))).ToList();
        Assert.Equal(8, kanten.Count);
        Assert.DoesNotContain(kanten, k => kanten.Contains((k.Item2, k.Item1)));
        // Stempel: Fläche ohne Steg gerechnet, 76.00 m²
        Assert.Contains(teil.Primitive, x => x.Art == "text" && x.Text == "76.00 m²");
    }

    [Fact]
    public void RaumMitSpitzeDerBreiteNull_SpitzeEntfernt_KeineHaarlinie()
    {
        // BKW: Räume aus Wänden mit einer Spitze (hin und zurück auf derselben Linie, 100 mm) – Punkte kommen doppelt vor, es gibt aber kein Loch
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        var ring = new[] { new[] { 0.0, 0 }, [10000.0, 0], [10000.0, 2000], [10100.0, 2000], [10000.0, 2000], [10000.0, 4000], [0.0, 4000] };
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-S", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["nummer"] = "0.02", ["lichteHoehe"] = 2600 },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts(ring) },
        }));
        Ansicht(p, null);
        var teil = Teil(Grafik.Plan(p, "AN-EG"), p, "R-S");
        var polys = teil.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, polys.Count);
        Assert.All(polys, x => { Assert.Null(x.Loecher); Assert.Equal(4, x.Punkte.Count); Assert.Equal(40e6, PolyFlaeche(x), 1); });
        string Schl(double[] a) => $"{Math.Round(a[0], 1)}|{Math.Round(a[1], 1)}";
        var kanten = teil.Fangkanten!.Select(k => (Schl(k[0]), Schl(k[1]))).ToList();
        Assert.DoesNotContain(kanten, k => kanten.Contains((k.Item2, k.Item1)));
    }

    [Fact]
    public void RaumOhneInsel_UmrissUnveraendert()
    {
        var p = Raum(false);
        var g = Grafik.Plan(p, "AN-EG");
        var raum = g.Teile.Single(t => t.Ebene == "raum");
        var polys = raum.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, polys.Count);
        Assert.All(polys, x => { Assert.Null(x.Loecher); Assert.Equal(4, x.Punkte.Count); });
    }

    [Fact]
    public void RaumMitInsel_Koerper_PrismaMitLoch()
    {
        var (p, _) = MitInsel();
        var k = Grafik.Koerper(p).Single(x => x.Id == Hole(p, "R-I").Id);
        var t = Assert.Single(k.Teile);
        Assert.Equal("prisma", t.Art);
        Assert.Equal(4, t.Grundriss!.Count);
        Assert.Single(t.Loecher!);
        Assert.Equal(4, t.Loecher![0].Count);
        Assert.Equal(2600, t.Oben - t.Unten);
    }

    [Fact]
    public void RaumMitInsel_Huelle_StreifenUmDieInselAufDerRaumseite()
    {
        var (p, _) = MitInsel(200);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 10100, 8100, 200);
        RaumgrenzenHilfe.Waende(p, "EG", 3900, 2900, 6100, 5100, 200);   // Inselwände, Achsen 100 innerhalb des Lochs
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = "huelle" } }));
        var g = Grafik.Plan(p, "AN-EG");
        // 4 Aussenkanten + 4 Inselkanten, alle gegen den Raum R-I (Insel hat keinen Raum): aussen
        var s = Streifen(g, HuelleGrafik.FarbeAussen);
        Assert.Equal(8, s.Count);
        // Inselstreifen liegen ausserhalb der Insel (Raumseite): an der Südkante y = 2800 ein Streifen 2700 … 2800
        Assert.Contains(s, x => Box(x.Punkte) is var b && Math.Abs(b[1] - 2700) < 0.01 && Math.Abs(b[3] - 2800) < 0.01);
    }
}
