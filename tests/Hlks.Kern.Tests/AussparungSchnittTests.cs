using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B4c (angeglichen in Nachtrag B4d an SIA 400:2000 B.10 / SIA 410/2): Aussparungen in Schnitt und Ansicht als
/// Sinnbild über der Grafik (Sichtraum, Form, Kennfarbe, Ergänzung, Status nur am Bildschirm, Beschriftung, Plan-PDF).
/// Alle Werte FIKTIV (wie AussparungGrafikTests: Spiel 20; Rohr DN 50: da 55). Handrechnungen in mm, Massstab 1:50.
/// Schnittlinie A → B, Blick nach links der Linie: u = Abstand ab A entlang der Linie, v = z, w = Abstand vor der Schnittebene.
///   RO-1  Rohr quer durch W-1 bei x = 3000, z = 1000: Aussparung von Hand rund Ø 95 → ganze cm 100 (aussparung_form, Nachtrag B3b, B4e),
///         Tiefe 200, Achse [0,1,0].
///   KA-1  Kanal 400 × 200 quer durch W-1 bei x = 2000, z = 2000: Aussparung 440 × 240, Tiefe 200, Achse [0,1,0].
///   S-1   Schnitt (3000, −1000) → (3000, 5000): Blick nach −x; u = y + 1000, w = 3000 − x. RO-1: u 900…1100, v 950…1050, w −50…50.
///         KA-1: u 900…1100, v 1880…2120, w 780…1220.
///   A-1   Ansicht (0, −3000) → (6000, −3000): Blick nach +y; u = x, w = y + 3000. Beide Aussparungen liegen bei w = 3000 (Achse parallel zur Blickrichtung).</summary>
public class AussparungSchnittTests
{
    /// <summary>option: Ansicht-Eigenschaft beschriftungAmSymbol an AN-EG (AussparungGrafikTests.WandprojektMitOption); an weiteren Ansichten einschalten
    /// mit AussparungGrafikTests.Option.</summary>
    static Projekt Aufbau(bool option = false)
    {
        var p = option ? AussparungGrafikTests.WandprojektMitOption() : Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-1");   // B3b: Kernbohrung nur von Hand
        Assert.Equal(2, Aussparungen(p).Count);
        return p;
    }

    static void Ansicht(Projekt p, string kz, string art, double[] a, double[] b, double? tiefe = null, string? blick = null)
    {
        var eig = new JsonObject { ["art"] = art, ["massstab"] = 50 };
        if (tiefe is double t) eig["tiefe"] = t;
        if (blick is not null) eig["blick"] = blick;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(a, b) },
        }));
    }

    static void S1(Projekt p, double? tiefe = null, string? blick = null) => Ansicht(p, "S-1", "schnitt", [3000, -1000], [3000, 5000], tiefe, blick);
    static void A1(Projekt p, double? tiefe = null, string? blick = null) => Ansicht(p, "A-1", "ansicht", [0, -3000], [6000, -3000], tiefe, blick);

    static void Darstellung(Projekt p, string ansicht, string? wert) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { ["darstellung"] = wert } }));

    /// <summary>Eine Aussparung ausserhalb des Gebäudes (leerer Raum): rund (d) oder rechteckig (b × h), beliebige Achse.</summary>
    static Element Frei(Projekt p, string kz, double[] mitte, double[]? achse, double tiefe, double? d = null, double? b = null, double? h = null)
    {
        var eig = new JsonObject { ["form"] = d is null ? "rechteckig" : "rund", ["tiefe"] = tiefe, ["status"] = "vorgeschlagen" };
        if (d is double dd) eig["d"] = dd;
        if (b is double bb) eig["b"] = bb;
        if (h is double hh) eig["h"] = hh;
        if (achse is not null) eig["achse"] = new JsonArray(achse[0], achse[1], achse[2]);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(mitte) },
        }));
        return Hole(p, kz);
    }

    static Grafikteil Symbol(Plangrafik g, Element a) => Assert.Single(g.Teile, t => t.Id == a.Id);
    static Primitiv Umriss(Grafikteil t) => AussparungGrafikTests.Umriss(t);
    static bool Hat(Plangrafik g, Element a) => g.Teile.Any(t => t.Id == a.Id);
    static List<Primitiv> Texte(Plangrafik g) => g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text").ToList();
    static void Nummerieren(Projekt p) => Ok(p.Ausfuehren("aussparungen_nummerieren", new JsonObject()));

    // ------------------------------------------------------------------ Schnitt quer zur Wand

    [Fact]
    public void Schnitt_QuerZurWand_RundeAussparung_RechteckTiefeMalDurchmesser_OhneKreuz_Braun()
    {
        var p = Aufbau();
        S1(p);
        var a = VonLeitung(p, "RO-1");
        var teil = Symbol(Grafik.Plan(p, "S-1"), a);
        Assert.Equal("oeffnung", teil.Ebene);
        // Achse y liegt in der Bildebene (u): Tiefe 200 → u 900…1100, Ø 100 → v 950…1050
        var umriss = Umriss(teil);
        Assert.Equal(4, umriss.Punkte.Count);
        Nahe([900, 950, 1100, 1050], Box(umriss.Punkte));
        Assert.Equal(Stifte.Duenn, umriss.Stil.Stift);
        Assert.Null(umriss.Stil.Farbe);
        // Kennfarbe braun (Wand), Strich auf der längsten Kante unten (Einzug min(25, 100/4) = 25, ganze Länge), kein Kreuz, kein Kreis
        Assert.Equal(AussparungGrafik.Braun, Assert.Single(AussparungGrafikTests.Kennfarben(teil)).Stil.Fuellung);
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Assert.Equal(Stifte.Duenn, l.Stil.Stift);
        Nahe([900, 975], l.Punkte[0]);
        Nahe([1100, 975], l.Punkte[1]);
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");
        Assert.Contains(teil.Fangpunkte!, q => Math.Abs(q[0] - 1000) < 1e-6 && Math.Abs(q[1] - 1000) < 1e-6);
        // nur der Statusrahmen ist nur am Bildschirm
        Assert.Equal(AussparungGrafik.FarbeVorgeschlagen, AussparungGrafikTests.Statusrahmen(teil).Stil.Farbe);
    }

    [Fact]
    public void Schnitt_QuerZurWand_RechteckigeAussparung_RechteckTiefeMalHoehe()
    {
        var p = Aufbau();
        S1(p);
        var teil = Symbol(Grafik.Plan(p, "S-1"), VonLeitung(p, "KA-1"));
        // KA-1 liegt bei x = 2000, also w = 1000 vor der Schnittebene (780…1220), ohne Sichttiefe sichtbar: u 900…1100, v 1880…2120
        var umriss = Umriss(teil);
        Nahe([900, 1880, 1100, 2120], Box(umriss.Punkte));
        Assert.DoesNotContain(teil.Primitive, x => x.Art == "linie");
    }

    [Fact]
    public void Schnitt_SymbolLiegtUeberSchnittflaechenUndSichtkanten()
    {
        var p = Aufbau();
        S1(p);
        var g = Grafik.Plan(p, "S-1");
        int wand = g.Teile.FindIndex(t => t.Id == Hole(p, "W-1").Id);
        Assert.True(wand >= 0);
        Assert.True(g.Teile.FindIndex(t => t.Id == VonLeitung(p, "RO-1").Id) > wand);
        Assert.True(g.Teile.FindIndex(t => t.Id == VonLeitung(p, "KA-1").Id) > wand);
        // die Schnittfläche der Wand bleibt unverändert: Aussparungen werden nicht ausgespart (Körper NichtSchneiden)
        Assert.Contains(g.Teile.Single(t => t.Id == Hole(p, "W-1").Id).Primitive, x => x.Art == "polygon" && x.Stil.Fuellung != AussparungGrafik.Braun);
    }

    // ------------------------------------------------------------------ Ansicht parallel zur Wand

    [Fact]
    public void Ansicht_ParallelZurWand_RundeAussparung_KreisBraunMitHalbbogenUntenRechts()
    {
        var p = Aufbau();
        A1(p);
        var teil = Symbol(Grafik.Plan(p, "A-1"), VonLeitung(p, "RO-1"));
        var kreise = teil.Primitive.Where(x => x.Art == "kreis" && !x.Stil.NurBildschirm).ToList();
        Assert.Equal(2, kreise.Count);                                    // Kennfarbe und Umriss
        Assert.All(kreise, k => { Assert.Equal(50, k.Radius!.Value, 1e-9); Nahe([3000, 1000], k.Punkte[0]); });
        var kf = Assert.Single(kreise, k => k.Stil.Fuellung is not null);
        Assert.Equal(AussparungGrafik.Braun, kf.Stil.Fuellung);
        Assert.Equal(AussparungGrafik.KennDeckkraft, kf.Stil.Deckkraft);
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "polygon" or "linie");   // kein Mittelkreuz
        // Ergänzung: dünner Halbbogen unten rechts 225° … 45°, Radius 50 − min(25, 50/2 = 25) = 25
        var b = Assert.Single(teil.Primitive, x => x.Art == "bogen");
        Assert.Equal((225.0, 45.0), (b.Von!.Value, b.Bis!.Value));
        Assert.Equal(25, b.Radius!.Value, 1e-9);
        Assert.Equal(Stifte.Duenn, b.Stil.Stift);
    }

    [Fact]
    public void Ansicht_ParallelZurWand_RechteckigeAussparung_RechteckBMalH_OhneKreuz()
    {
        var p = Aufbau();
        A1(p);
        var teil = Symbol(Grafik.Plan(p, "A-1"), VonLeitung(p, "KA-1"));
        var umriss = Umriss(teil);
        Assert.Equal(4, umriss.Punkte.Count);
        Nahe([1780, 1880, 2220, 2120], Box(umriss.Punkte));      // b = 440 entlang der Wand, h = 240
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");
        // Strich auf der längsten Kante (440, unten), Einzug 25: 1780 … 2220 bei v = 1905
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Nahe([1780, 1905], l.Punkte[0]);
        Nahe([2220, 1905], l.Punkte[1]);
    }

    // ------------------------------------------------------------------ Sichtraum

    [Fact]
    public void Sichttiefe_HinterDerTiefeKeinSymbol_DavorSchon()
    {
        var p = Aufbau();
        S1(p, tiefe: 700);                                       // KA-1 beginnt bei w = 780, RO-1 bei w = −50
        var g = Grafik.Plan(p, "S-1");
        Assert.True(Hat(g, VonLeitung(p, "RO-1")));
        Assert.False(Hat(g, VonLeitung(p, "KA-1")));
        Ansicht(p, "S-2", "schnitt", [3000, -1000], [3000, 5000], tiefe: 800);   // 800 ≥ 780: der Körper reicht in den Sichtraum
        Assert.True(Hat(Grafik.Plan(p, "S-2"), VonLeitung(p, "KA-1")));
        A1(p, tiefe: 2800);                                      // beide reichen von w = 2900 bis 3100: 2800 reicht nicht
        Assert.False(Hat(Grafik.Plan(p, "A-1"), VonLeitung(p, "KA-1")));
        Assert.False(Hat(Grafik.Plan(p, "A-1"), VonLeitung(p, "RO-1")));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "A-1", ["eigenschaften"] = new JsonObject { ["tiefe"] = 2950 } }));
        var g2 = Grafik.Plan(p, "A-1");
        Assert.True(Hat(g2, VonLeitung(p, "KA-1")));             // Körper 2900…3100
        Assert.True(Hat(g2, VonLeitung(p, "RO-1")));             // Zylinder ebenso (Achse parallel, Tiefe 200)
    }

    [Fact]
    public void Sichtraum_HinterDerSchnittebene_KeinSymbol()
    {
        var p = Aufbau();
        A1(p, blick: "rechts");                                  // Blick nach −y: die Wand liegt hinter dem Betrachter
        var g = Grafik.Plan(p, "A-1");
        Assert.False(Hat(g, VonLeitung(p, "RO-1")));
        Assert.False(Hat(g, VonLeitung(p, "KA-1")));
        S1(p, blick: "rechts");                                  // Blick nach +x: KA-1 (x = 2000) liegt dahinter, RO-1 wird noch geschnitten
        var s = Grafik.Plan(p, "S-1");
        Assert.True(Hat(s, VonLeitung(p, "RO-1")));
        Assert.False(Hat(s, VonLeitung(p, "KA-1")));
    }

    [Fact]
    public void Sichtraum_SeitlichUnbegrenzt_WieDerSchnittraum()
    {
        var p = Aufbau();
        // kurze Ansichtslinie links der Aussparungen: der Schnittraum ist entlang u unbegrenzt, RO-1 (u = 3000) erscheint trotzdem
        Ansicht(p, "A-K", "ansicht", [0, -3000], [1000, -3000]);
        var teil = Symbol(Grafik.Plan(p, "A-K"), VonLeitung(p, "RO-1"));
        Nahe([3000, 1000], Assert.Single(teil.Primitive, x => x.Art == "kreis" && x.Stil.Fuellung is null && !x.Stil.NurBildschirm).Punkte[0]);
    }

    [Fact]
    public void Sichtraum_AussparungOhneLage_KleineMarkeNurImSichtraum()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-X", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rund", ["d"] = 100 },   // achse und tiefe fehlen
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 0, 1000]) },
        }));
        S1(p);
        var feld = Umriss(Symbol(Grafik.Plan(p, "S-1"), Hole(p, "AUS-X")));
        Nahe([925, 925, 1075, 1075], Box(feld.Punkte));          // 3 mm Papier = 150 mm um (u 1000, v 1000)
        Ansicht(p, "A-2", "ansicht", [0, -3000], [6000, -3000], tiefe: 1000);   // Punkt bei w = 3000
        Assert.False(Hat(Grafik.Plan(p, "A-2"), Hole(p, "AUS-X")));
    }

    // ------------------------------------------------------------------ Schräge Achse

    [Fact]
    public void SchraegeAchse_RechteckigeAussparung_UmrissDerProjektionAlsPolygon()
    {
        var p = Aufbau();
        // b = 100 entlang U = (1,0,−1)/√2, h = 80 entlang y (Blickrichtung), Tiefe 50 entlang (1,0,1)/√2; Mitte (8000, 8000, 1500)
        var a = Frei(p, "AUS-S", [8000, 8000, 1500], [1, 0, 1], 50, b: 100, h: 80);
        Ansicht(p, "A-S", "ansicht", [7000, 5000], [9000, 5000]);    // Blick nach +y, u = x − 7000, w = y − 5000 = 3000
        var teil = Symbol(Grafik.Plan(p, "A-S"), a);
        var umriss = Umriss(teil);
        Assert.Equal(4, umriss.Punkte.Count);
        // gedrehtes Rechteck 100 × 50 um (1000, 1500): Ecken ±50·(s, −s) ± 25·(s, s), s = √½
        double s = Math.Sqrt(0.5);
        foreach (var (du, dv) in new[] { (75 * s, -25 * s), (25 * s, -75 * s), (-25 * s, 75 * s), (-75 * s, 25 * s) })
            Assert.Contains(umriss.Punkte, q => Math.Abs(q[0] - (1000 + du)) < 1e-3 && Math.Abs(q[1] - (1500 + dv)) < 1e-3);
        Assert.Equal(100.0 * 50, PolyFlaeche(umriss), 0.1);
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");
        // ohne Bauteil: Achse (1, 0, 1)/√2 steil (|z| ≥ 0.5) → horizontal → gelb, Ergänzung an der Ecke oben links
        Assert.Equal(AussparungGrafik.Gelb, Assert.Single(AussparungGrafikTests.Kennfarben(teil)).Stil.Fuellung);
        Assert.Single(teil.Primitive, x => x.Art == "polylinie");
    }

    [Fact]
    public void SchraegeAchse_RundeAussparung_UmschliessendesPolygonStattKreis()
    {
        var p = Aufbau();
        var a = Frei(p, "AUS-R", [8000, 8000, 1500], [1, 0, 1], 200, d: 100);
        Ansicht(p, "A-S", "ansicht", [7000, 5000], [9000, 5000]);
        var teil = Symbol(Grafik.Plan(p, "A-S"), a);
        var umriss = Umriss(teil);
        Assert.Equal(20000, PolyFlaeche(umriss), 0.1);           // Ø 100 × Tiefe 200, um 45° gedreht (Quader um den Zylinder)
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "kreis" or "linie");
    }

    [Theory]
    [InlineData(0.01, true)]       // 0.57° zur Blickrichtung
    [InlineData(0.03, false)]      // 1.72°
    public void Winkelgrenze_EinGradZurBlickrichtung(double quer, bool kreis)
    {
        var p = Aufbau();
        var a = Frei(p, "AUS-W", [8000, 8000, 1500], [quer, 1, 0], 200, d: 100);
        Ansicht(p, "A-W", "ansicht", [7000, 5000], [9000, 5000]);
        var teil = Symbol(Grafik.Plan(p, "A-W"), a);
        Assert.Equal(kreis, teil.Primitive.Any(x => x.Art == "kreis"));
        Assert.Equal(!kreis, teil.Primitive.Any(x => x.Art == "polygon"));
    }

    // ------------------------------------------------------------------ Stil, Ausblenden, Beschriftung

    [Theory]
    [InlineData("freigegeben", AussparungGrafik.FarbeAbgegeben)]
    [InlineData("angekommen", AussparungGrafik.FarbeAngekommen)]
    [InlineData("fehlt", AussparungGrafik.FarbeFehlt)]
    public void Statusfarbe_NurAmBildschirm_WieImGrundriss(string status, string farbe)
    {
        var p = Aufbau();
        S1(p);
        var a = VonLeitung(p, "RO-1");
        string[] weg = status switch { "freigegeben" => ["freigegeben"], "angekommen" => ["freigegeben", "versandt", "angekommen"], _ => ["freigegeben", "versandt", "fehlt"] };
        foreach (var schritt in weg) Ok(Status(p, schritt, a.Kennzeichen));
        var teil = Symbol(Grafik.Plan(p, "S-1"), Hole(p, a.Kennzeichen));
        Assert.Equal(farbe, AussparungGrafikTests.Statusrahmen(teil).Stil.Farbe);
        Assert.All(teil.Primitive.Where(x => !x.Stil.NurBildschirm), x => { Assert.NotEqual(farbe, x.Stil.Farbe); Assert.NotEqual(farbe, x.Stil.Fuellung); });
    }

    [Fact]
    public void KlasseAusgeblendet_KeinSymbol_AuchNichtInDerAnsicht()
    {
        var p = Aufbau();
        S1(p);
        A1(p);
        Assert.True(Hat(Grafik.Plan(p, "S-1"), VonLeitung(p, "RO-1")));
        foreach (var kz in new[] { "S-1", "A-1" })
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = new JsonObject { ["klassenAusblenden"] = new JsonArray("Aussparung") } }));
        foreach (var kz in new[] { "S-1", "A-1" })
        {
            var g = Grafik.Plan(p, kz);
            Assert.DoesNotContain(g.Teile, t => AussparungenIds(p).Contains(t.Id!));
            Assert.Contains(g.Teile, t => t.Id == Hole(p, "W-1").Id);        // übrige Grafik unverändert
        }
    }

    static HashSet<string> AussparungenIds(Projekt p) => Aussparungen(p).Select(a => a.Id).ToHashSet();

    /// <summary>Vorgabe (H+K-Vorlage, ohne Beschriftung am Symbol): in der Darstellung «aussparungen» nur die Nummer, nur am Bildschirm;
    /// eine Zeile mittig am Sinnbild: 1000 − 0.35 × 100.</summary>
    [Fact]
    public void Beschriftung_OhneOption_NurNummerAmBildschirm()
    {
        var p = Aufbau();
        Nummerieren(p);
        S1(p);
        Assert.DoesNotContain(Texte(Grafik.Plan(p, "S-1")), t => t.Text!.Contains("A-EG-"));
        Darstellung(p, "S-1", "aussparungen");
        var alle = Texte(Grafik.Plan(p, "S-1"));
        Assert.DoesNotContain(alle, t => t.Text!.Contains("WD") || t.Text.Contains("OK +") || t.Text.Contains("Achse +"));
        var texte = alle.Where(t => t.Text!.StartsWith("A-EG-", StringComparison.Ordinal)).ToList();
        Assert.Equal(["A-EG-001", "A-EG-002"], texte.Select(t => t.Text).Order());
        Assert.All(texte, t => Assert.True(t.Stil.NurBildschirm));
        var kopf = texte.Single(t => t.Text == "A-EG-002");
        Assert.Equal(1100 + 50, kopf.Punkte[0][0], 1e-6);
        Assert.Equal(1000 - 2.0 * 50 * 0.35, kopf.Punkte[0][1], 1e-6);
    }

    [Fact]
    public void Beschriftung_MitOption_NurInDerDarstellungAussparungen()
    {
        var p = Aufbau(option: true);
        Nummerieren(p);
        S1(p);
        AussparungGrafikTests.Option(p, "S-1");
        // normale Darstellung: auch mit vergebenen Nummern kein Text (anders als im Grundriss)
        var normal = Grafik.Plan(p, "S-1");
        Assert.DoesNotContain(Texte(normal), t => t.Text!.Contains("A-EG-") || t.Text.Contains("OK"));
        Assert.All(AussparungenIds(p), id => Assert.Single(normal.Teile, t => t.Id == id));   // nur das Symbol, kein zweites Teil

        Darstellung(p, "S-1", "aussparungen");
        var g = Grafik.Plan(p, "S-1");
        var texte = Texte(g);
        // nach Mitte x: KA-1 (x = 2000) → 001, RO-1 (x = 3000) → 002; Text rechts neben dem Sinnbild (RO-1: u bis 1100 + 1 mm Papier)
        // Beschriftung wie im Aussparungsplan (SIA 400 B.10.3 Tabelle 6, Einheit cm): «S WD Ø 10», Nummer, Achse ab Fertigkote EG (0)
        var kopf = texte.Single(t => t.Text == "S WD Ø 10");
        Assert.True(kopf.Fett);
        Assert.Equal(2.0, kopf.Groesse);
        Assert.Equal(1100 + 50, kopf.Punkte[0][0], 1e-6);
        // Block aus Kopf und zwei Zeilen, mittig am Sinnbild (v = 1000), Zeilenabstand 1.2 × 2.0 mm: 1000 + 2 × 120 / 2 − 35
        Assert.Equal(1000 + 2 * 120 / 2.0 - 2.0 * 50 * 0.35, kopf.Punkte[0][1], 1e-6);
        var einzel = texte.Single(t => t.Text!.StartsWith("A-EG-002"));
        Assert.Equal(["A-EG-002", "Achse +1.00"], einzel.Text!.Split('\n'));
        Assert.Equal(kopf.Punkte[0][1] - 120, einzel.Punkte[0][1], 1e-6);
        Assert.Contains(texte, t => t.Text == "L WD 44/24" && t.Fett == true);
        var ka = texte.Single(t => t.Text!.StartsWith("A-EG-001"));
        Assert.Equal(["A-EG-001", "OK +2.12"], ka.Text!.Split('\n'));
        // Beschriftungsteile ohne Id auf der Ebene beschriftung
        Assert.All(g.Teile.Where(t => t.Primitive.Any(x => x.Art == "text" && x.Text!.StartsWith("A-EG-"))), t => { Assert.Null(t.Id); Assert.Equal("beschriftung", t.Ebene); });
    }

    [Fact]
    public void Beschriftung_OhneNummerStehtDasKennzeichen_AuchInDerAnsicht()
    {
        var p = Aufbau();
        A1(p);
        Darstellung(p, "A-1", "aussparungen");
        var ka = VonLeitung(p, "KA-1");
        var texte = Texte(Grafik.Plan(p, "A-1"));
        Assert.Contains(texte, t => t.Text == ka.Kennzeichen && t.Fett == true && t.Stil.NurBildschirm);
    }

    [Fact]
    public void Aussparungsplan_KeineLegendeUndKeineDaempfungInSchnittUndAnsicht()
    {
        var p = Aufbau();
        S1(p);
        Darstellung(p, "S-1", "aussparungen");
        var g = Grafik.Plan(p, "S-1");
        Assert.DoesNotContain(g.Teile, t => t.Primitive.Any(x => x.Text?.StartsWith("Aussparungen nach SIA") == true));
        Assert.Contains(g.Teile, t => t.Id == Hole(p, "RO-1").Id);   // im Schnitt bleibt die Gebäudetechnik
    }

    // ------------------------------------------------------------------ Beschriftungen freistellen

    /// <summary>Textblock der Beschriftung als [minx, miny, maxx, maxy] nach der Annahme der Freistellung (Zeichenbreite 0.55 × Schrift,
    /// Oberlänge 0.8, Unterlänge 0.25, Zeilenabstand 1.2; Schrift 2.0 mm bei 1:50 = 100 mm).</summary>
    static double[] Block(Grafikteil t)
    {
        var kopf = t.Primitive.First(x => x.Art == "text");
        var zeilen = t.Primitive.Where(x => x.Art == "text").SelectMany(x => x.Text!.Split('\n')).ToList();
        double x0 = kopf.Punkte[0][0], y0 = kopf.Punkte[0][1], s = 100, lh = 1.2 * s;
        return [x0, y0 - (zeilen.Count - 1) * lh - 0.25 * s, x0 + zeilen.Max(z => z.Length) * 0.55 * s, y0 + 0.8 * s];
    }

    static bool Ueberlappen(double[] a, double[] b) => a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3];

    static List<Grafikteil> Beschriftungen(Plangrafik g) =>
        g.Teile.Where(t => t.Id is null && t.Ebene == "beschriftung" && t.Primitive.Any(x => x.Art == "text" && x.Text!.StartsWith("A-EG-"))).ToList();

    static void FuenfDichte(Projekt p, double y, double[] achse)
    {
        for (int i = 0; i < 5; i++) Frei(p, $"AUS-D{i + 1}", [1000 + 300 * i, y, 2000], achse, 200, b: 100, h: 100);
        Nummerieren(p);
    }

    [Fact]
    public void Freistellung_FuenfAussparungenImAbstand300_Grundriss_KeineUeberlappendenTextbloecke()
    {
        var p = Aufbau();
        FuenfDichte(p, 2000, [0, 0, 1]);
        Darstellung(p, "AN-EG", "aussparungen");
        var g = Grafik.Plan(p, "AN-EG");
        var bloecke = Beschriftungen(g);
        Assert.Equal(7, bloecke.Count);                           // 5 neue + RO-1 + KA-1
        for (int i = 0; i < bloecke.Count; i++)
            for (int j = i + 1; j < bloecke.Count; j++)
                Assert.False(Ueberlappen(Block(bloecke[i]), Block(bloecke[j])), $"Blöcke {i} und {j} überlappen");
        // die dichten Aussparungen brauchen Führungslinien (mindestens eine Beschriftung ist abgerückt), je eine dünne Linie in der
        // Planfarbe – wie die Beschriftung nur am Bildschirm (Vorgabe ohne Beschriftung am Symbol)
        var linien = bloecke.SelectMany(t => t.Primitive).Where(x => x.Art == "linie").ToList();
        Assert.NotEmpty(linien);
        Assert.All(linien, x => { Assert.Equal(Stifte.Duenn, x.Stil.Stift); Assert.Null(x.Stil.Farbe); Assert.True(x.Stil.NurBildschirm); });
        // möglichst nicht über einem Sinnbild (gedruckt, ohne Statusrahmen): kein Block deckt ein Sinnbild einer anderen Aussparung
        foreach (var b in bloecke.Select(Block))
            foreach (var sym in g.Teile.Where(t => AussparungenIds(p).Contains(t.Id!)).Select(t => Grafik.Begrenzung([new Grafikteil { Primitive = t.Primitive.Where(x => !x.Stil.NurBildschirm).ToList() }])!))
                Assert.False(Ueberlappen(b, sym));
    }

    [Fact]
    public void Freistellung_FuenfAussparungenImAbstand300_Ansicht_KeineUeberlappendenTextbloecke()
    {
        var p = Wandprojekt();
        FuenfDichte(p, 8000, [0, 1, 0]);                          // Achse in Blickrichtung: Rechtecke 100 × 100 bei u = x, v = 2000
        Ansicht(p, "A-D", "ansicht", [0, 5000], [6000, 5000]);
        Darstellung(p, "A-D", "aussparungen");
        var bloecke = Beschriftungen(Grafik.Plan(p, "A-D"));
        Assert.Equal(5, bloecke.Count);
        for (int i = 0; i < bloecke.Count; i++)
            for (int j = i + 1; j < bloecke.Count; j++)
                Assert.False(Ueberlappen(Block(bloecke[i]), Block(bloecke[j])), $"Blöcke {i} und {j} überlappen");
        Assert.Contains(bloecke.SelectMany(t => t.Primitive), x => x.Art == "linie");
    }

    [Fact]
    public void Freistellung_Deterministisch_GleichesModellGleicheLagen()
    {
        var p = Wandprojekt();
        FuenfDichte(p, 2000, [0, 0, 1]);
        Darstellung(p, "AN-EG", "aussparungen");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(Grafik.Plan(p, "AN-EG").Teile), System.Text.Json.JsonSerializer.Serialize(Grafik.Plan(p, "AN-EG").Teile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Freistellung_AlleinstehendeAussparung_BehaeltLageRechtsOhneFuehrungslinie(bool plan)
    {
        var p = Wandprojekt();
        var a = Frei(p, "AUS-E", [3000, 2000, 2000], [0, 0, 1], 200, b: 100, h: 100);
        Nummerieren(p);
        if (plan) Darstellung(p, "AN-EG", "aussparungen");
        var g = Grafik.Plan(p, "AN-EG");
        var kopf = Assert.Single(Beschriftungen(g));
        Assert.DoesNotContain(kopf.Primitive, x => x.Art == "linie");
        var t = kopf.Primitive.First(x => x.Art == "text");
        Assert.Equal(3050 + 50, t.Punkte[0][0], 1e-6);            // Symbol bis x = 3050, 1 mm Papier Abstand
        // Vorgabe ohne Beschriftung am Symbol: Kopf = Nummer (nur Bildschirm); im Plan darunter «kein Fixpunkt» (Wände ohne Material)
        int zeilen = plan ? 2 : 1;
        Assert.Equal(2000 + (zeilen - 1) * 120 / 2.0 - 2.0 * 50 * 0.35, t.Punkte[0][1], 1e-6);   // mittig am Symbol wie bisher
        Assert.Equal(Hole(p, a.Kennzeichen).Text("nummer"), t.Text);
        Assert.Equal(zeilen, kopf.Primitive.Where(x => x.Art == "text").SelectMany(x => x.Text!.Split('\n')).Count());
        if (plan) Assert.Equal(AussparungMassketten.KeinFixpunkt, kopf.Primitive.Where(x => x.Art == "text").ElementAt(1).Text);
    }

    [Fact]
    public void Freistellung_PlanPdfMitDichtenAussparungen_EntstehtOhneNummern()
    {
        var p = Aufbau();
        FuenfDichte(p, 2000, [0, 0, 1]);
        Darstellung(p, "AN-EG", "aussparungen");
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-202", ["titel"] = "Aussparungsplan dicht", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new JsonArray(210, 150) }));
        var pdf = Planausgabe.Pdf(p, "PB-1", unkomprimiert: true);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var s = Encoding.Latin1.GetString(pdf);
        foreach (var n in Aussparungen(p).Select(a => a.Text("nummer"))) Assert.DoesNotContain(n!, s);   // Nummern nur am Bildschirm
        Assert.DoesNotContain("kein Fixpunkt", s);
    }

    // ------------------------------------------------------------------ Bestehendes bleibt

    [Fact]
    public void Raeume_ErscheinenWeiterhinNichtImSchnitt()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([100, 100], [5900, 100], [5900, 3900], [100, 3900]) },
        }));
        S1(p);
        var g = Grafik.Plan(p, "S-1");
        Assert.DoesNotContain(g.Teile, t => t.Id == Hole(p, "R-1").Id);
        Assert.True(Hat(g, VonLeitung(p, "RO-1")));
    }

    // ------------------------------------------------------------------ Plan-PDF

    [Fact]
    public void PlanPdf_SchnittZeigtDasSymbolUndDieBeschriftung_AusgeblendetNicht()
    {
        var p = Aufbau();
        Nummerieren(p);
        S1(p);
        Darstellung(p, "S-1", "aussparungen");
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-201", ["titel"] = "Schnitt mit Aussparungen", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "S-1", ["lage"] = new JsonArray(210, 150) }));

        var pdf = Planausgabe.Pdf(p, "PB-1", unkomprimiert: true);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var s = Encoding.Latin1.GetString(pdf);
        Assert.DoesNotContain("A-EG-001", s);                 // Vorgabe: Nummer nur am Bildschirm
        // Kennfarbe braun (#7f3f00 = 127/63/0) im Inhaltsstrom, die Statusfarbe «vorgeschlagen» (#e08a1e = 224/138/30) nicht
        var farbe = new Regex(@"0\.49\d* 0\.24\d* 0 (rg|RG)");
        Assert.Matches(farbe, s);
        Assert.DoesNotMatch(new Regex(@"0\.87\d* 0\.54\d* 0\.11\d* (rg|RG)"), s);

        // Klasse ausgeblendet: keine Kennfarbe
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "S-1", ["eigenschaften"] = new JsonObject { ["klassenAusblenden"] = new JsonArray("Aussparung") } }));
        var s2 = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.DoesNotMatch(farbe, s2);
    }

    // ------------------------------------------------------------------ Kollisionsmarke bleibt am Bildschirm im Grundriss

    [Fact]
    public void Kollisionsmarke_ErscheintWeiterhinNichtInSchnitt()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = "KOL-1", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "hart", ["typ"] = "kontakt", ["status"] = "neu", ["mass"] = 12 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 0, 1000]) },
        }));
        S1(p);
        Assert.DoesNotContain(Grafik.Plan(p, "S-1").Teile, t => t.Id == Hole(p, "KOL-1").Id);
    }
}
