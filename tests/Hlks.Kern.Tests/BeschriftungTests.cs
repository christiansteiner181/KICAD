using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Plangrafik der Beschriftung (Detaillinie, Füllbereich, Textnotiz, Bemassung, Kote, Symbol) und das
/// Schnittsymbol in Grundrissen (Modul 1a, B3). Bezug: SIA 400:2000 Ziff. B.5.3, B.5.4, B.6.1.</summary>
public class BeschriftungTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    // ---------------------------------------------------------------- Hilfen

    static JsonObject Geo(string art, params double[][] punkte) => new()
    {
        ["art"] = art,
        ["punkte"] = new JsonArray(punkte.Select(p => (JsonNode)new JsonArray(p.Select(v => (JsonNode)JsonValue.Create(v)).ToArray())).ToArray()),
    };

    static Element Anlegen(Projekt p, string klasse, string kz, JsonObject? eig, JsonObject? geo, string? ansicht = null, string? geschoss = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (eig is not null) o["eigenschaften"] = eig.DeepClone();
        if (geo is not null) o["geometrie"] = geo.DeepClone();
        if (ansicht is not null) o["bezuege"] = new JsonObject { ["ansicht"] = ansicht };
        if (geschoss is not null) o["geschoss"] = geschoss;
        var r = p.Ausfuehren("element_anlegen", o);
        Assert.True(r.Angenommen, $"{klasse} {kz} abgelehnt: {r.Fehler} {r.Vorschlag}");
        return p.Modell.Finde(kz)!;
    }

    /// <summary>Projekt mit Geschoss EG, Grundrissen AN-EG (m), AN-CM (cm), AN-MM (mm), AN-OG und Schnitt AN-S (Linie 0/0 → 5000/0).</summary>
    static Projekt Neu()
    {
        var p = Projekt.Anlegen(Kat, null, "Beschriftungstest", "T", "pc");
        Anlegen(p, "Gebaeude", "GEB-1", null, null);
        var eg = new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 };
        var g = p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB-1", ["eigenschaften"] = eg });
        Assert.True(g.Angenommen, g.Fehler);
        Anlegen(p, "Ansicht", "AN-EG", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 }, null, geschoss: "EG");
        Anlegen(p, "Ansicht", "AN-CM", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["bemassungEinheit"] = "cm" }, null, geschoss: "EG");
        Anlegen(p, "Ansicht", "AN-MM", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["bemassungEinheit"] = "mm" }, null, geschoss: "EG");
        Anlegen(p, "Ansicht", "AN-OG", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 }, null, geschoss: "EG");
        Anlegen(p, "Ansicht", "AN-S", new JsonObject { ["art"] = "schnitt", ["massstab"] = 50, ["blick"] = "links", ["tiefe"] = 3000 },
            Geo("linie", [0, 0], [5000, 0]));
        return p;
    }

    static Grafikteil Teil(Plangrafik g, Element e) => g.Teile.Single(t => t.Id == e.Id);

    static Element Bem(Projekt p, string kz, string ansicht, double[][] punkte, string? richtung = null, double? versatz = null, string? text = null)
    {
        var eig = new JsonObject();
        if (richtung is not null) eig["richtung"] = richtung;
        if (versatz is not null) eig["versatz"] = versatz;
        if (text is not null) eig["text"] = text;
        return Anlegen(p, "Bemassung", kz, eig, Geo("polylinie", punkte), ansicht);
    }

    static List<string> Texte(Grafikteil t) => t.Primitive.Where(x => x.Art == "text").Select(x => x.Text!).ToList();
    static List<Primitiv> Linien(Grafikteil t, string stift) => t.Primitive.Where(x => x.Art == "linie" && x.Stil.Stift == stift).ToList();

    // ---------------------------------------------------------------- Bemassung

    [Fact]
    public void Bemassung_ZweiPunkte_ZeigtMeterMitZweiDezimalen()
    {
        var p = Neu();
        var b = Bem(p, "BM-1", "AN-EG", [[0, 0], [3450, 0]], versatz: 1000);
        var t = Teil(Grafik.Plan(p, "AN-EG"), b);
        Assert.Equal("beschriftung", t.Ebene);
        Assert.Equal(["3.45"], Texte(t));
        var z = t.Primitive.Single(x => x.Art == "text");
        Assert.Equal("mitte", z.Ausrichtung);
        Assert.Equal(0, z.Winkel);
        Assert.Equal(2.0, z.Groesse);
        Assert.False(z.Stil.NurBildschirm);
    }

    [Fact]
    public void Bemassung_EinheitCmUndMmAusDerAnsicht()
    {
        var p = Neu();
        var b1 = Bem(p, "BM-CM", "AN-CM", [[0, 0], [3450, 0]]);
        var b2 = Bem(p, "BM-CM2", "AN-CM", [[0, 100], [3455, 100]]);
        var b3 = Bem(p, "BM-MM", "AN-MM", [[0, 0], [3450, 0]]);
        var cm = Grafik.Plan(p, "AN-CM");
        Assert.Equal(["345"], Texte(Teil(cm, b1)));
        Assert.Equal(["345.5"], Texte(Teil(cm, b2)));
        Assert.Equal(["3450"], Texte(Teil(Grafik.Plan(p, "AN-MM"), b3)));
    }

    [Fact]
    public void Bemassung_HilfslinienMasslinieUndZweiMassstriche()
    {
        var p = Neu();
        var b = Bem(p, "BM-1", "AN-EG", [[0, 0], [3450, 0]], versatz: 1000);
        var t = Teil(Grafik.Plan(p, "AN-EG"), b);

        // Masslinie und zwei Hilfslinien dünn (SIA 400 B.5.3, B.3.3 Tab. 4)
        var duenn = Linien(t, Stifte.Duenn);
        Assert.Equal(3, duenn.Count);
        var mass = duenn.Single(l => l.Punkte[0][1] == 1000 && l.Punkte[1][1] == 1000);
        Assert.Equal([0.0, 3450.0], mass.Punkte.Select(q => q[0]).Order().ToArray());
        // Hilfslinien: 1 mm Abstand zum Messpunkt, 2 mm Überstand (Annahme), bei 1:50 = 50 bzw. 100 mm
        var hilf = duenn.Where(l => l != mass).OrderBy(l => l.Punkte[0][0]).ToList();
        Assert.Equal([0.0, 50.0], hilf[0].Punkte[0]);
        Assert.Equal([0.0, 1100.0], hilf[0].Punkte[1]);
        Assert.Equal([3450.0, 50.0], hilf[1].Punkte[0]);

        // Massstriche: doppelt so dick wie die Masslinie, Schrägstrich 45°, 2.5 mm Papier = 125 mm
        Assert.Equal(2 * Stifte.Mm(Stifte.Duenn), Stifte.Mm(Stifte.Mittel), 0.03);
        var striche = Linien(t, Stifte.Mittel);
        Assert.Equal(2, striche.Count);
        foreach (var s in striche)
        {
            double dx = s.Punkte[1][0] - s.Punkte[0][0], dy = s.Punkte[1][1] - s.Punkte[0][1];
            Assert.Equal(Math.Abs(dx), Math.Abs(dy), 6);
            Assert.Equal(125, Math.Sqrt(dx * dx + dy * dy), 6);
        }
        Assert.Equal([[0.0, 0.0], [3450.0, 0.0]], t.Fangpunkte!.Select(f => f).ToArray());
    }

    [Fact]
    public void Bemassung_VersatzNegativLiegtRechtsDerMessrichtung()
    {
        var p = Neu();
        var b = Bem(p, "BM-1", "AN-EG", [[0, 0], [2000, 0]], versatz: -800);
        var t = Teil(Grafik.Plan(p, "AN-EG"), b);
        var mass = Linien(t, Stifte.Duenn).Single(l => l.Punkte[0][1] == -800 && l.Punkte[1][1] == -800);
        Assert.NotNull(mass);
        // Hilfslinien zeigen nach unten
        Assert.Contains(Linien(t, Stifte.Duenn), l => l.Punkte[0][1] == -50 && l.Punkte[1][1] == -900);
    }

    [Fact]
    public void Bemassung_MasskettenmitDreiPunktenHatZweiZahlen()
    {
        var p = Neu();
        var b = Bem(p, "BM-1", "AN-EG", [[0, 0], [1000, 0], [3450, 0]], versatz: 600);
        var t = Teil(Grafik.Plan(p, "AN-EG"), b);
        Assert.Equal(["1.00", "2.45"], Texte(t));
        Assert.Equal(3, Linien(t, Stifte.Mittel).Count);   // ein Massstrich je Messpunkt
    }

    [Fact]
    public void Bemassung_VertikalLiestVonRechts()
    {
        var p = Neu();
        var b = Bem(p, "BM-V", "AN-EG", [[0, 0], [0, 2000]], versatz: 500);
        var t = Teil(Grafik.Plan(p, "AN-EG"), b);
        var z = t.Primitive.Single(x => x.Art == "text");
        Assert.Equal("2.00", z.Text);
        Assert.Equal(90, z.Winkel!.Value, 6);
        // Messrichtung nach Norden, links davon = Westen: Masslinie bei x = -500
        Assert.Contains(Linien(t, Stifte.Duenn), l => l.Punkte[0][0] == -500 && l.Punkte[1][0] == -500);
        // Text sitzt links der Masslinie (über der Linie im Textsinn)
        Assert.True(z.Punkte[0][0] < -500);
        Assert.Equal(1000, z.Punkte[0][1], 6);
    }

    [Fact]
    public void Bemassung_NachUntenGemessenWirdTrotzdemVonRechtsGelesen()
    {
        var p = Neu();
        var b = Bem(p, "BM-V", "AN-EG", [[0, 2000], [0, 0]], versatz: 500);
        var z = Teil(Grafik.Plan(p, "AN-EG"), b).Primitive.Single(x => x.Art == "text");
        Assert.Equal(90, z.Winkel!.Value, 6);
        Assert.Equal("2.00", z.Text);
    }

    [Fact]
    public void Bemassung_AusgerichtetSchraegUndHorizontalMitHoehenunterschied()
    {
        var p = Neu();
        var schraeg = Bem(p, "BM-S", "AN-EG", [[0, 0], [3000, 4000]]);
        var horiz = Bem(p, "BM-H", "AN-EG", [[0, 0], [3000, 4000]], richtung: "horizontal", versatz: 500);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(["5.00"], Texte(Teil(g, schraeg)));
        var zs = Teil(g, schraeg).Primitive.Single(x => x.Art == "text");
        Assert.InRange(zs.Winkel!.Value, -90, 90);
        Assert.Equal(Math.Atan2(4000, 3000) * 180 / Math.PI, zs.Winkel!.Value, 6);
        Assert.Equal(["3.00"], Texte(Teil(g, horiz)));
    }

    [Fact]
    public void Bemassung_TextErsetztDieZahlNurBeiZweiPunkten()
    {
        var p = Neu();
        var zwei = Bem(p, "BM-1", "AN-EG", [[0, 0], [3450, 0]], text: "ca. 3.50");
        var drei = Bem(p, "BM-2", "AN-EG", [[0, 500], [1000, 500], [3450, 500]], text: "x");
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(["ca. 3.50"], Texte(Teil(g, zwei)));
        Assert.Equal(["1.00", "2.45"], Texte(Teil(g, drei)));
    }

    [Fact]
    public void Bemassung_ImSchnittGiltDieselbeRechnung()
    {
        var p = Neu();
        var b = Bem(p, "BM-S", "AN-S", [[500, 0], [500, 2850]], versatz: 400);
        var g = Grafik.Plan(p, "AN-S");
        Assert.Equal(["2.85"], Texte(Teil(g, b)));
    }

    // ---------------------------------------------------------------- Kote

    static string KoteText(Projekt p, string ansicht, Element k) =>
        Teil(Grafik.Plan(p, ansicht), k).Primitive.Single(x => x.Art == "text").Text!;

    [Fact]
    public void Kote_ZeigtHoeheDesPunktsMitVorzeichen()
    {
        var p = Neu();
        var plus = Anlegen(p, "Kote", "KO-1", null, Geo("punkt", [1000, 1000, 2850]), "AN-EG");
        var minus = Anlegen(p, "Kote", "KO-2", null, Geo("punkt", [2000, 1000, -2600]), "AN-EG");
        var null0 = Anlegen(p, "Kote", "KO-3", null, Geo("punkt", [3000, 1000, 0]), "AN-EG");
        Assert.Equal("+2.85", KoteText(p, "AN-EG", plus));
        Assert.Equal("−2.60", KoteText(p, "AN-EG", minus));   // echtes Minuszeichen
        Assert.Equal("±0.00", KoteText(p, "AN-EG", null0));
    }

    [Fact]
    public void Kote_WertZusatzUndSymbol()
    {
        var p = Neu();
        var k = Anlegen(p, "Kote", "KO-1", new JsonObject { ["wert"] = 2900, ["zusatz"] = "OK FB" }, Geo("punkt", [1000, 1000, 0]), "AN-EG");
        var t = Teil(Grafik.Plan(p, "AN-EG"), k);
        Assert.Equal(["OK FB +2.90"], Texte(t));
        // Dreieck mit der Spitze auf dem Punkt, dünn
        var d = t.Primitive.Single(x => x.Art == "polygon");
        Assert.Equal(Stifte.Duenn, d.Stil.Stift);
        Assert.Equal(3, d.Punkte.Count);
        Assert.Equal([1000.0, 1000.0], d.Punkte[0]);
        Assert.Equal(d.Punkte[1][1], d.Punkte[2][1]);
        Assert.Equal(d.Punkte[0][0], (d.Punkte[1][0] + d.Punkte[2][0]) / 2, 6);   // gleichschenklig
        Assert.Equal([1000.0, 1000.0], t.Fangpunkte!.Single());
    }

    [Fact]
    public void Kote_ImSchnittNimmtDieZweiteKoordinate()
    {
        var p = Neu();
        var k1 = Anlegen(p, "Kote", "KO-1", null, Geo("punkt", [1000, 2850]), "AN-S");
        var k2 = Anlegen(p, "Kote", "KO-2", null, Geo("punkt", [2000, -2600]), "AN-S");
        Assert.Equal("+2.85", KoteText(p, "AN-S", k1));
        Assert.Equal("−2.60", KoteText(p, "AN-S", k2));
    }

    // ---------------------------------------------------------------- Detaillinie, Füllbereich, Textnotiz, Symbol

    [Fact]
    public void Detaillinie_VorgabenUndAngaben()
    {
        var p = Neu();
        var a = Anlegen(p, "Detaillinie", "DL-1", null, Geo("polylinie", [0, 0], [1000, 0], [1000, 1000]), "AN-EG");
        var b = Anlegen(p, "Detaillinie", "DL-2", new JsonObject { ["stift"] = "dick", ["linienart"] = "strich" }, Geo("linie", [0, 0], [500, 500]), "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        var pa = Teil(g, a).Primitive.Single();
        Assert.Equal("polylinie", pa.Art);
        Assert.Equal(3, pa.Punkte.Count);
        Assert.Equal(Stifte.Duenn, pa.Stil.Stift);
        Assert.Equal("voll", pa.Stil.Linie);
        var pb = Teil(g, b).Primitive.Single();
        Assert.Equal(Stifte.Dick, pb.Stil.Stift);
        Assert.Equal("strich", pb.Stil.Linie);
        Assert.Equal("beschriftung", Teil(g, a).Ebene);
    }

    [Fact]
    public void Fuellbereich_SchraffurFarbeUndRand()
    {
        var p = Neu();
        var quadrat = Geo("polygon", [0, 0], [1000, 0], [1000, 1000], [0, 1000]);
        var beton = Anlegen(p, "Fuellbereich", "FB-1", new JsonObject { ["muster"] = "beton" }, quadrat, "AN-EG");
        var voll = Anlegen(p, "Fuellbereich", "FB-2", new JsonObject { ["muster"] = "voll", ["farbe"] = "#ff0000", ["rand"] = true }, quadrat, "AN-EG");
        var grau = Anlegen(p, "Fuellbereich", "FB-3", new JsonObject { ["muster"] = "voll" }, quadrat, "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");

        var pb = Teil(g, beton).Primitive.Single();
        Assert.Equal("polygon", pb.Art);
        Assert.Equal("beton", pb.Stil.Muster);
        Assert.Null(pb.Stil.Fuellung);
        Assert.Null(pb.Stil.Stift);          // Kontur nur mit rand

        var pv = Teil(g, voll).Primitive.Single();
        Assert.Equal("#ff0000", pv.Stil.Fuellung);
        Assert.Null(pv.Stil.Muster);
        Assert.Equal(Stifte.Duenn, pv.Stil.Stift);

        var pg = Teil(g, grau).Primitive.Single();
        Assert.Equal(BeschriftungGrafik.VorgabeGrau, pg.Stil.Fuellung);
        Assert.Null(pg.Stil.Stift);
    }

    [Fact]
    public void Textnotiz_InhaltGroesseAusrichtungWinkel()
    {
        var p = Neu();
        var a = Anlegen(p, "Textnotiz", "TX-1", new JsonObject { ["inhalt"] = "Zeile 1\nZeile 2" }, Geo("punkt", [100, 200, 0]), "AN-EG");
        var b = Anlegen(p, "Textnotiz", "TX-2", new JsonObject { ["inhalt"] = "Titel", ["groesse"] = 3.5, ["ausrichtung"] = "rechts", ["winkel"] = 30 },
            Geo("punkt", [300, 400, 0]), "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        var ta = Teil(g, a).Primitive.Single();
        Assert.Equal("Zeile 1\nZeile 2", ta.Text);
        Assert.Equal(2.5, ta.Groesse);
        Assert.Equal("links", ta.Ausrichtung);
        Assert.Equal(0, ta.Winkel);
        Assert.Equal([100.0, 200.0], ta.Punkte[0]);
        var tb = Teil(g, b).Primitive.Single();
        Assert.Equal(3.5, tb.Groesse);
        Assert.Equal("rechts", tb.Ausrichtung);
        Assert.Equal(30, tb.Winkel);
        Assert.False(ta.Stil.NurBildschirm);
    }

    [Fact]
    public void Nordpfeil_MitNGroesseUndWinkel()
    {
        var p = Neu();
        var s = Anlegen(p, "Symbol", "SY-1", new JsonObject { ["art"] = "nordpfeil" }, Geo("punkt", [5000, 5000, 0]), "AN-EG");
        var gedreht = Anlegen(p, "Symbol", "SY-2", new JsonObject { ["art"] = "nordpfeil", ["winkel"] = 90, ["groesse"] = 30 }, Geo("punkt", [8000, 5000, 0]), "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        var t = Teil(g, s);
        Assert.Equal("symbol", t.Ebene);
        Assert.Contains("N", Texte(t));
        var umriss = t.Primitive.Single(x => x.Art == "polygon" && x.Punkte.Count == 4);
        // Spitze oben: Länge Spitze–Fuss 15 mm Papier = 750 mm bei 1:50, Spitze 20 Einheiten über der Mitte
        double k = 15.0 / 40 * 50;
        Assert.Equal(5000, umriss.Punkte[0][0], 6);
        Assert.Equal(5000 + 20 * k, umriss.Punkte[0][1], 6);
        Assert.Equal(5000 - 20 * k, umriss.Punkte[1][1], 6);
        Assert.NotNull(t.Primitive.Single(x => x.Art == "polygon" && x.Punkte.Count == 3).Stil.Fuellung);

        // um 90° gedreht und doppelt so gross: Spitze zeigt nach Westen (Gegenuhrzeigersinn), Länge 20 * 2k
        var u = Teil(g, gedreht).Primitive.Single(x => x.Art == "polygon" && x.Punkte.Count == 4);
        Assert.Equal(8000 - 20 * 2 * k, u.Punkte[0][0], 6);
        Assert.Equal(5000, u.Punkte[0][1], 6);
        Assert.Equal(90, Teil(g, gedreht).Primitive.Single(x => x.Art == "text").Winkel!.Value, 6);
    }

    // ---------------------------------------------------------------- Ansichtsbindung

    [Fact]
    public void Beschriftung_ErscheintNurInIhrerAnsicht()
    {
        var p = Neu();
        var tx = Anlegen(p, "Textnotiz", "TX-1", new JsonObject { ["inhalt"] = "nur EG" }, Geo("punkt", [0, 0, 0]), "AN-EG");
        var kote = Anlegen(p, "Kote", "KO-S", null, Geo("punkt", [100, 200]), "AN-S");
        Assert.Contains(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == tx.Id);
        Assert.DoesNotContain(Grafik.Plan(p, "AN-OG").Teile, t => t.Id == tx.Id);
        Assert.DoesNotContain(Grafik.Plan(p, "AN-S").Teile, t => t.Id == tx.Id);
        Assert.Contains(Grafik.Plan(p, "AN-S").Teile, t => t.Id == kote.Id);   // Schnitt ruft die Beschriftung ebenfalls auf
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == kote.Id);
    }

    [Fact]
    public void Beschriftung_OhneAnsichtWirdAbgelehnt()
    {
        var p = Neu();
        var r = p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Textnotiz", ["eigenschaften"] = new JsonObject { ["inhalt"] = "x" }, ["geometrie"] = Geo("punkt", [0, 0, 0]),
        });
        Assert.False(r.Angenommen);
    }

    [Fact]
    public void Beschriftung_HatKeinenKoerper()
    {
        var p = Neu();
        var tx = Anlegen(p, "Textnotiz", "TX-1", new JsonObject { ["inhalt"] = "x" }, Geo("punkt", [0, 0, 0]), "AN-EG");
        var q = new BeschriftungGrafik();
        Assert.Null(q.Koerper(p, tx));
        Assert.Null(q.Koerper(p, p.Modell.Finde("AN-S")!));
        Assert.DoesNotContain(Grafik.Koerper(p), k => k.Id == tx.Id);
    }

    [Fact]
    public void Beschriftung_AllesDrucktAuchAmPlan()
    {
        var p = Neu();
        Anlegen(p, "Textnotiz", "TX-1", new JsonObject { ["inhalt"] = "x" }, Geo("punkt", [0, 0, 0]), "AN-EG");
        Bem(p, "BM-1", "AN-EG", [[0, 0], [3450, 0]]);
        Anlegen(p, "Kote", "KO-1", null, Geo("punkt", [0, 0, 100]), "AN-EG");
        Anlegen(p, "Symbol", "SY-1", new JsonObject { ["art"] = "nordpfeil" }, Geo("punkt", [0, 0, 0]), "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        Assert.NotEmpty(g.Teile);
        Assert.All(g.Teile.SelectMany(t => t.Primitive), pr => Assert.False(pr.Stil.NurBildschirm));
        Assert.NotNull(g.Begrenzung);
    }

    // ---------------------------------------------------------------- Schnittsymbol

    static List<Primitiv> Pfeilspitzen(Grafikteil t) => t.Primitive.Where(x => x.Art == "polygon").ToList();

    [Fact]
    public void Schnittsymbol_ErscheintImGrundrissMitPfeilNachBlick()
    {
        var p = Neu();
        var s = p.Modell.Finde("AN-S")!;
        var g = Grafik.Plan(p, "AN-EG");
        var t = Teil(g, s);
        Assert.Equal("symbol", t.Ebene);

        // blick links der Linie 0/0 → 5000/0 = nach Norden (+y): beide Pfeilspitzen oberhalb der Linie
        var spitzen = Pfeilspitzen(t);
        Assert.Equal(2, spitzen.Count);
        Assert.All(spitzen, sp => Assert.True(sp.Punkte[0][1] > 0));
        Assert.Contains(spitzen, sp => sp.Punkte[0][0] == 0);
        Assert.Contains(spitzen, sp => sp.Punkte[0][0] == 5000);
        // Pfeil: Schaft 6 mm Papier = 300 mm
        Assert.All(spitzen, sp => Assert.Equal(300, sp.Punkte[0][1], 6));
        // Fangpunkte an den Enden, Bezeichnung = Kennzeichen, dünne Pfeile (B.6.1)
        Assert.Equal([[0.0, 0.0], [5000.0, 0.0]], t.Fangpunkte!.ToArray());
        Assert.Equal(["AN-S", "AN-S"], Texte(t));
        Assert.All(spitzen, sp => Assert.Equal(Stifte.Duenn, sp.Stil.Stift));
    }

    [Fact]
    public void Schnittsymbol_BlickRechtsZeigtNachUnten()
    {
        var p = Neu();
        var s = p.Modell.Finde("AN-S")!;
        var r = p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-S", ["eigenschaften"] = new JsonObject { ["blick"] = "rechts" } });
        Assert.True(r.Angenommen, r.Fehler);
        var t = Teil(Grafik.Plan(p, "AN-EG"), s);
        Assert.All(Pfeilspitzen(t), sp => Assert.True(sp.Punkte[0][1] < 0));
    }

    [Fact]
    public void Schnittsymbol_LinieStrichpunktDickAnDenEndenDuennDazwischen()
    {
        var p = Neu();
        var t = Teil(Grafik.Plan(p, "AN-EG"), p.Modell.Finde("AN-S")!);
        var linien = t.Primitive.Where(x => x.Art == "polylinie").ToList();
        Assert.Equal(3, linien.Count);
        Assert.All(linien, l => Assert.Equal("strichpunkt", l.Stil.Linie));
        Assert.Equal([Stifte.Dick, Stifte.Duenn, Stifte.Dick], linien.Select(l => l.Stil.Stift!).ToArray());
        // Endstücke je 10 mm Papier = 500 mm
        Assert.Equal([0.0, 0.0], linien[0].Punkte[0]);
        Assert.Equal([500.0, 0.0], linien[0].Punkte[^1]);
        Assert.Equal([500.0, 0.0], linien[1].Punkte[0]);
        Assert.Equal([4500.0, 0.0], linien[1].Punkte[^1]);
        Assert.Equal([5000.0, 0.0], linien[2].Punkte[^1]);
    }

    [Fact]
    public void Schnittsymbol_KurzeLinieIstDurchgehendDick()
    {
        var p = Neu();
        Anlegen(p, "Ansicht", "AN-K", new JsonObject { ["art"] = "ansicht", ["massstab"] = 50, ["blick"] = "links" }, Geo("linie", [0, 3000], [800, 3000]));
        var t = Teil(Grafik.Plan(p, "AN-EG"), p.Modell.Finde("AN-K")!);
        var linie = t.Primitive.Single(x => x.Art == "polylinie");
        Assert.Equal(Stifte.Dick, linie.Stil.Stift);
    }

    [Fact]
    public void Schnittsymbol_NurFuerSchnittUndAnsichtNichtInDerEigenenAnsicht()
    {
        var p = Neu();
        var s = p.Modell.Finde("AN-S")!;
        // andere Grundrisse erscheinen nicht als Symbol; der Schnitt zeigt sein eigenes Symbol nicht
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == p.Modell.Finde("AN-OG")!.Id);
        Assert.DoesNotContain(Grafik.Plan(p, "AN-S").Teile, t => t.Id == s.Id);
        // Schnittsymbole sind in allen Grundrissen sichtbar
        Assert.Contains(Grafik.Plan(p, "AN-OG").Teile, t => t.Id == s.Id);
    }
}
