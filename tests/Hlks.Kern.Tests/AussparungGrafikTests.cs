using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B4a und Nachtrag B4d: Grafik der Aussparungen nach H+K-Vorlage Aussparungsplan Bernstrasse 1.UG (24.09.2024) und
/// SIA 400:2000 B.10 / SIA 410/2 (Grundriss, Aussparungsplan, Beschriftung mit und ohne Option «beschriftungAmSymbol», Legende, gedämpfte
/// Gebäudetechnik, 3D), Kollisionsmarken und Plan-PDF. Alle Werte FIKTIV (Vorgaben Spiel 20,
/// Zusammenfassen 100 mm; Testrohrserie DN 50: da 55). Handrechnungen in mm, Massstab 1:50 (1 Papier-mm = 50 mm Modell).
///   RO-1  Rohr DN 50 (System KW, sanitaer) quer durch W-1 bei x = 3000, z = 1000: vorgeschlagen 95 × 95 → ganze cm 100 × 100
///         (Nachtrag B4e), von Hand rund (aussparung_form, Nachtrag B3b) Ø 55 + 2 × 20 = 95 → 100, Tiefe 200, Achse [0,1,0], Kote 1000.
///         Kurzzeichen «S WD».
///   KA-1  Kanal 400 × 200 (System ZUL, lueftung) quer durch W-1 bei x = 2000, z = 2000: Aussparung 440 × 240, UK 1880, OK 2120,
///         Tiefe 200. Kurzzeichen «L WD».
///   KA-2  Kanal 400 × 200 senkrecht durch die Decke D-1 (OG, Kote 3000, Dicke 250) bei (4000, 2000): 440 × 240, Tiefe 250, UK 2750,
///         OK 3000. Geschoss EG (Deckenaussparungen im Geschoss UNTER der Decke: UK 2750 − 1 im Band des EG 0 … 3000). «L DD».
/// Nummern im EG nach x: KA-1 (2000) A-EG-001, RO-1 (3000) A-EG-002, KA-2 (4000) A-EG-003.
/// Sinnbild (AussparungSinnbild, Annahmen aus der Vorlage): Winkel (Decke) Ecke 1.0 mm Papier = 50 mm innen, Schenkel bis 0.25 mm = 12.5 mm
/// vor die Gegenkanten; Wandstrich 0.5 mm = 25 mm innen; je höchstens ¼ der kleinsten Breite. Striche dünn.</summary>
public class AussparungGrafikTests
{
    const double Winkel = 1.0 * 50, Ende = 0.25 * 50, Strich = 0.5 * 50;   // mm Papier × 50

    // ------------------------------------------------------------------ Option «beschriftungAmSymbol» (Katalog, Hauptlauf B4d)

    /// <summary>AussparungHilfe.Wandprojekt (EG, AN-EG 1:50, vier Wände, OG, Testserie, Vorgaben) mit beschriftungAmSymbol an AN-EG.</summary>
    internal static Projekt WandprojektMitOption()
    {
        var p = Wandprojekt();
        Option(p, "AN-EG");
        return p;
    }

    internal static void Option(Projekt p, string ansicht, bool ein = true) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { [AussparungGrafik.OptionBeschriftung] = ein } }));

    /// <summary>Projekt mit den drei Aussparungen (vorgeschlagen), Grundrissen AN-EG (Vorgabe) und AN-OG; option: Katalog mit
    /// beschriftungAmSymbol, an AN-EG und AN-OG eingeschaltet.</summary>
    static Projekt Aufbau(bool option = false)
    {
        var p = option ? WandprojektMitOption() : Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-1");   // B3b: Kernbohrung nur von Hand
        Assert.Equal(3, Aussparungen(p).Count);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-OG", ["geschoss"] = "OG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        if (option) Option(p, "AN-OG");
        return p;
    }

    /// <summary>Die vier Wände aus Beton (Fixpunkte der Massketten, Nachtrag B4d).</summary>
    internal static void Beton(Projekt p, params string[] waende)
    {
        foreach (var w in waende.Length > 0 ? waende : ["W-1", "W-2", "W-3", "W-4"])
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = w, ["eigenschaften"] = new JsonObject { ["material"] = "beton" } }));
    }

    static void Darstellung(Projekt p, string ansicht, string? wert) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { ["darstellung"] = wert } }));

    static void Nummerieren(Projekt p) => Ok(p.Ausfuehren("aussparungen_nummerieren", new JsonObject()));

    /// <summary>Das Symbol der Aussparung (das einzige Teil mit ihrer Id).</summary>
    static Grafikteil Symbol(Plangrafik g, Element a) => Assert.Single(g.Teile, t => t.Id == a.Id);

    /// <summary>Der gedruckte Umriss (Polygon ohne Füllung, Stift mittel, nicht nur am Bildschirm).</summary>
    internal static Primitiv Umriss(Grafikteil t) => Assert.Single(t.Primitive, x => x.Art == "polygon" && x.Stil.Fuellung is null && !x.Stil.NurBildschirm);

    /// <summary>Die Kennfarben-Füllungen (Polygone bzw. Kreise mit Füllung, ohne Kontur).</summary>
    internal static List<Primitiv> Kennfarben(Grafikteil t) => t.Primitive.Where(x => x.Stil.Fuellung is not null && x.Stil.Stift is null).ToList();

    /// <summary>Der Statusrahmen (nur Bildschirm).</summary>
    internal static Primitiv Statusrahmen(Grafikteil t) => Assert.Single(t.Primitive, x => x.Stil.NurBildschirm);

    static List<Primitiv> Texte(Plangrafik g) => g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text").ToList();

    static string Json(object o) => JsonSerializer.Serialize(o);

    // ------------------------------------------------------------------ Grundriss: Sinnbild nach SIA 400 B.10.2

    [Fact]
    public void Grundriss_RundeAussparungInWand_RechteckDMalWanddicke_OhneKreuz_Braun_StrichUnten()
    {
        var p = Aufbau();
        var a = VonLeitung(p, "RO-1");
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), a);
        Assert.Equal("oeffnung", teil.Ebene);
        // Projektion der Bohrung in der Wand: d × Wanddicke = 100 × 200 um (3000, 0) – massstäbliches Rechteck ohne Kreuz und ohne Kreis
        var umriss = Umriss(teil);
        Nahe([2950, -100, 3050, 100], Box(umriss.Punkte));
        Assert.Equal(4, umriss.Punkte.Count);
        Assert.Equal(Stifte.Duenn, umriss.Stil.Stift);                  // dünner Rand (Vorlage)
        Assert.Null(umriss.Stil.Farbe);                                 // Planfarbe
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");
        // Kennfarbe braun #7f3f00, voll (vertikales Bauteil, Vorlage)
        var kf = Assert.Single(Kennfarben(teil));
        Assert.Equal("#7f3f00", kf.Stil.Fuellung);
        Assert.Equal(1.0, kf.Stil.Deckkraft);
        // Ergänzung: ein dünner Strich auf der Kante parallel zur Wand (W-1 in x) unten; Einzug min(25, 100/4 = 25) = 25, ganze Länge
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Assert.Equal(Stifte.Duenn, l.Stil.Stift);
        Assert.Equal(2, l.Punkte.Count);
        Nahe([2950, -100 + 25], l.Punkte[0]);
        Nahe([3050, -100 + 25], l.Punkte[1]);
        Assert.Contains(teil.Fangpunkte!, q => Math.Abs(q[0] - 3000) < 1e-6 && Math.Abs(q[1]) < 1e-6);
        // gedruckt: alles ausser dem Statusrahmen
        Assert.Equal(AussparungGrafik.FarbeVorgeschlagen, Statusrahmen(teil).Stil.Farbe);
    }

    [Fact]
    public void Grundriss_RechteckigeAussparungInWand_RechteckBTiefe_StrichUnten()
    {
        var p = Aufbau();
        var a = VonLeitung(p, "KA-1");
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), a);
        var umriss = Umriss(teil);
        Nahe([1780, -100, 2220, 100], Box(umriss.Punkte));   // b = 440 entlang der Wand, Tiefe 200
        Assert.Equal(4, umriss.Punkte.Count);
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");   // kein Diagonalkreuz
        // Strich: Einzug 25 (≤ 200/4 = 50) über die ganze Breite der Aussparung
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Nahe([1780, -100 + Strich], l.Punkte[0]);
        Nahe([2220, -100 + Strich], l.Punkte[1]);
        Assert.Equal(AussparungGrafik.Braun, Assert.Single(Kennfarben(teil)).Stil.Fuellung);
    }

    /// <summary>Wand in y-Richtung (W-2, Achse x = 6000): Rohr quer bei y 2000 → 95 → 100 breit entlang der Wand × 200 Wanddicke; der
    /// Strich liegt auf der Kante parallel zur Wand, die rechts liegt: x = 6100 − min(25, 100/4) = 6075, y 1950 … 2050.</summary>
    [Fact]
    public void Grundriss_AussparungInSenkrechterWand_StrichRechts()
    {
        var p = Wandprojekt();
        Ok(Modul3Hilfe.Rohr(p, "RO-9", "DN 50", [5500, 2000, 1000], [6500, 2000, 1000]));
        Ok(Vorschlagen(p));
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), VonLeitung(p, "RO-9"));
        Nahe([5900, 1950, 6100, 2050], Box(Umriss(teil).Punkte));
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Assert.All(l.Punkte, q => Assert.Equal(6075, q[0], 1e-6));
        Assert.Equal([1950.0, 2050.0], l.Punkte.Select(q => q[1]).Order());
    }

    [Fact]
    public void Grundriss_AussparungInDecke_GelbWinkelObenLinks_ImGeschossUnterDerDecke()
    {
        var p = Aufbau();
        var a = VonLeitung(p, "KA-2");
        Assert.Equal(Hole(p, "EG").Id, a.Geschoss);                       // Geschoss unter der Decke
        var g = Grafik.Plan(p, "AN-EG");
        var teil = Symbol(g, a);
        var umriss = Umriss(teil);
        Nahe([3780, 1880, 4220, 2120], Box(umriss.Punkte));                // b × h = 440 × 240 um (4000, 2000)
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "linie" or "kreis");
        Assert.Equal("#ffc000", Assert.Single(Kennfarben(teil)).Stil.Fuellung);   // gelb, Vorlage
        // Winkel oben und links, dünn: Ecke 50 innen (≤ 240/4 = 60), Schenkel bis 12.5 vor die Gegenkanten:
        // oben rechts → oben links → unten links
        var l = Assert.Single(teil.Primitive, x => x.Art == "polylinie");
        Assert.Equal(Stifte.Duenn, l.Stil.Stift);
        Nahe([4220 - Ende, 2120 - Winkel], l.Punkte[0]);
        Nahe([3780 + Winkel, 2120 - Winkel], l.Punkte[1]);
        Nahe([3780 + Winkel, 1880 + Ende], l.Punkte[2]);
        // im Grundriss OG erscheint sie nicht (die Decke liegt über dem EG)
        Assert.DoesNotContain(Grafik.Plan(p, "AN-OG").Teile, t => t.Id == a.Id);
    }

    [Fact]
    public void Grundriss_RundeAussparungInDecke_KreisMitHalbbogenObenLinks()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-T", ["geschoss"] = "OG",
            ["eigenschaften"] = new JsonObject
            {
                ["form"] = "rund", ["d"] = 100, ["tiefe"] = 250, ["achse"] = new JsonArray(0, 0, 1), ["art"] = "kernbohrung", ["status"] = "vorgeschlagen",
            },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1500, 1500, 5875]) },
        }));
        var teil = Symbol(Grafik.Plan(p, "AN-OG"), Hole(p, "AUS-T"));
        var kreise = teil.Primitive.Where(x => x.Art == "kreis" && !x.Stil.NurBildschirm).ToList();
        Assert.Equal(2, kreise.Count);                                     // Kennfarbe und Umriss
        Assert.All(kreise, k => { Assert.Equal(50, k.Radius!.Value, 1e-9); Nahe([1500, 1500], k.Punkte[0]); });
        Assert.Contains(kreise, k => k.Stil.Fuellung == AussparungGrafik.Gelb);   // ohne Bauteil: Achse steil → horizontal
        // Ergänzung: Halbbogen 45° … 225° (oben links), Radius 50 − min(50, 50/2) = 25, dünn
        var b = Assert.Single(teil.Primitive, x => x.Art == "bogen");
        Assert.Equal((45.0, 225.0), (b.Von!.Value, b.Bis!.Value));
        Assert.Equal(25, b.Radius!.Value, 1e-9);
        Assert.Equal(Stifte.Duenn, b.Stil.Stift);
        Assert.DoesNotContain(teil.Primitive, x => x.Art is "polygon" or "linie");
        // Status: Kreis 0.7 mm Papier = 35 mm grösser, nur am Bildschirm
        Assert.Equal(85, Statusrahmen(teil).Radius!.Value, 1e-9);
    }

    [Fact]
    public void Grundriss_OhneLage_KleineMarkeStattNichts()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-X", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rund", ["d"] = 100 },   // achse und tiefe fehlen
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([500, 500, 1000]) },
        }));
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), Hole(p, "AUS-X"));
        Nahe([425, 425, 575, 575], Box(Umriss(teil).Punkte));   // 3 mm Papier = 150 mm
        Assert.Equal(2, teil.Primitive.Count(x => x.Art == "linie"));
    }

    [Theory]
    [InlineData("vorgeschlagen", AussparungGrafik.FarbeVorgeschlagen)]
    [InlineData("freigegeben", AussparungGrafik.FarbeAbgegeben)]
    [InlineData("versandt", AussparungGrafik.FarbeAbgegeben)]
    [InlineData("angekommen", AussparungGrafik.FarbeAngekommen)]
    [InlineData("ausgefuehrt", AussparungGrafik.FarbeAngekommen)]
    [InlineData("veraltet", AussparungGrafik.FarbeFehlt)]
    [InlineData("fehlt", AussparungGrafik.FarbeFehlt)]
    public void Grundriss_StatusNurAmBildschirm_FarbeFolgtDemStatus_GedrucktesBleibtGleich(string status, string farbe)
    {
        var p = Aufbau();
        var a = VonLeitung(p, "KA-1");
        string Gedruckt() => Json(Symbol(Grafik.Plan(p, "AN-EG"), Hole(p, a.Kennzeichen)).Primitive.Where(x => !x.Stil.NurBildschirm).ToList());
        var vorher = Gedruckt();
        // zulässiger Weg zum Status (Übergänge von Hand), sonst direkt setzen
        foreach (var schritt in Weg(status)) Ok(Status(p, schritt, a.Kennzeichen));
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), Hole(p, a.Kennzeichen));
        Assert.Equal(status, AussparungStatus.Von(Hole(p, a.Kennzeichen)));
        var rahmen = Statusrahmen(teil);
        Assert.Equal(farbe, rahmen.Stil.Farbe);
        Assert.Null(rahmen.Stil.Fuellung);
        Nahe([1780 - 35, -100 - 35, 2220 + 35, 100 + 35], Box(rahmen.Punkte));   // 0.7 mm Papier ausserhalb
        Assert.Equal(farbe, AussparungGrafik.Farbe(status));
        Assert.Equal(vorher, Gedruckt());                                         // die SIA-Darstellung hängt nicht vom Status ab
        Assert.DoesNotContain(teil.Primitive.Where(x => !x.Stil.NurBildschirm), x => x.Stil.Farbe == farbe || x.Stil.Fuellung == farbe);
    }

    static string[] Weg(string status) => status switch
    {
        "vorgeschlagen" => [],
        "freigegeben" => ["freigegeben"],
        "versandt" => ["freigegeben", "versandt"],
        "angekommen" => ["freigegeben", "versandt", "angekommen"],
        "ausgefuehrt" => ["freigegeben", "versandt", "angekommen", "ausgefuehrt"],
        "fehlt" => ["freigegeben", "versandt", "fehlt"],
        _ => [status],
    };

    /// <summary>H+K-Vorlage (Option aus, Vorgabe): am Symbol steht nichts auf dem Plan – die Nummer nur am Bildschirm, kein Kurzzeichen,
    /// keine Kote; ohne Nummer im gewöhnlichen Grundriss gar kein Text.</summary>
    [Fact]
    public void Grundriss_OhneOption_NurNummerAmBildschirm()
    {
        var p = Aufbau();
        Assert.Empty(Texte(Grafik.Plan(p, "AN-EG")));
        Nummerieren(p);
        var g = Grafik.Plan(p, "AN-EG");
        var texte = Texte(g);
        Assert.Equal(["A-EG-001", "A-EG-002", "A-EG-003"], texte.Select(t => t.Text).Order());
        Assert.All(texte, t => Assert.True(t.Stil.NurBildschirm));
        Darstellung(p, "AN-EG", "aussparungen");
        var plan = Texte(Grafik.Plan(p, "AN-EG"));
        Assert.All(plan.Where(t => t.Text!.Contains("A-EG-") || t.Text.Contains("WD") || t.Text.Contains("DD") || t.Text.Contains("OK")), t => Assert.True(t.Stil.NurBildschirm));
        Assert.DoesNotContain(plan, t => t.Text!.Contains("WD") || t.Text.Contains("DD") || t.Text.StartsWith("OK", StringComparison.Ordinal));
    }

    /// <summary>Option beschriftungAmSymbol (SIA-Plan), gewöhnlicher Grundriss: Beschriftung nur mit Nummer – Kurzzeichen (fett) und
    /// darunter die Nummer, gedruckt; keine Masse, keine Koten, keine Massketten. Lage des Blocks: rechts neben dem Sinnbild (KA-1: Umriss
    /// bis x = 2220, + 1 mm Papier = 50), zwei Zeilen mittig am Sinnbild: Grundlinie der Kopfzeile = 0 + (2 − 1) × 120 / 2 − 0.35 × 100 = 25.</summary>
    [Fact]
    public void Grundriss_MitOption_KurzzeichenUndNummerNurWennNummerVorhanden()
    {
        var p = Aufbau(option: true);
        var ohne = Grafik.Plan(p, "AN-EG");
        Assert.Empty(Texte(ohne));                                    // keine Nummer, kein Text
        Nummerieren(p);
        var mit = Grafik.Plan(p, "AN-EG");
        var texte = Texte(mit);
        Assert.Equal(6, texte.Count);                                 // je Aussparung Kurzzeichen und Nummer
        Assert.Equal("A-EG-001", VonLeitung(p, "KA-1").Text("nummer"));
        Assert.Equal("A-EG-003", VonLeitung(p, "KA-2").Text("nummer"));
        foreach (var (kurz, nr) in new[] { ("L WD", "A-EG-001"), ("S WD", "A-EG-002"), ("L DD", "A-EG-003") })
        {
            var kopf = texte.Single(t => t.Text == kurz);
            Assert.True(kopf.Fett);
            var zeile = texte.Single(t => t.Text == nr);
            Assert.NotEqual(true, zeile.Fett);
            Assert.Equal(kopf.Punkte[0][0], zeile.Punkte[0][0], 1e-6);
            Assert.Equal(2.0 * 50 * 1.2, kopf.Punkte[0][1] - zeile.Punkte[0][1], 1e-6);
        }
        var t1 = texte.Single(t => t.Text == "L WD");
        Assert.Equal(2220 + 50, t1.Punkte[0][0], 1e-6);
        Assert.Equal(25, t1.Punkte[0][1], 1e-6);
        Assert.All(mit.Teile.Where(t => t.Primitive.Any(x => x.Art == "text")), t => Assert.Null(t.Id));
        Assert.All(mit.Teile.Where(t => t.Primitive.Any(x => x.Art == "text")), t => Assert.Equal("beschriftung", t.Ebene));
        // kein Mass- und Kotentext, keine Legende im normalen Grundriss; gedruckt
        Assert.DoesNotContain(texte, t => t.Text!.Contains('/') || t.Text.Contains("OK") || t.Text.Contains("SIA"));
        Assert.All(texte, t => Assert.False(t.Stil.NurBildschirm));
    }

    // ------------------------------------------------------------------ Aussparungsplan: Beschriftung nach SIA 400 B.10.3 Tabelle 6

    /// <summary>Option beschriftungAmSymbol (SIA-Plan). Wände aus Beton (Fixpunkte vorhanden). Einheit: Ansicht ohne Angabe und ohne
    /// Gewerk → cm (ANNAHME wie Vorlage und Figur 49).
    ///   KA-1 Wand: «L WD 44/24» (Breite/Höhe 440/240 mm), Nummer, Lage OK ab Fertigkote EG (Kote 0): 2120 − 0 → «OK +2.12».
    ///   RO-1 Wand rund: «S WD Ø 10» (100 mm, auf ganze cm gerundet), Nummer, Achse 1000 − 0 → «Achse +1.00».
    ///   KA-2 Decke: «L DD 44/24» (Breite/Länge = Blatt-x/Blatt-y), Nummer – keine Kote (Tabelle 6, horizontale Bauteile).</summary>
    [Fact]
    public void Aussparungsplan_MitOption_BeschriftungNachTabelle6_KurzzeichenMasseNummerOk()
    {
        var p = Aufbau(option: true);
        Beton(p);
        Nummerieren(p);
        Darstellung(p, "AN-EG", "aussparungen");
        var texte = Texte(Grafik.Plan(p, "AN-EG"));
        void Pruefen(string kopf, string[] zeilen)
        {
            var k = texte.Single(t => t.Text == kopf);
            Assert.True(k.Fett);
            Assert.False(k.Stil.NurBildschirm);   // druckt
            Assert.Equal(2.0, k.Groesse);
            var z = texte.Single(t => t.Text!.StartsWith(zeilen[0], StringComparison.Ordinal));
            Assert.Equal(zeilen, z.Text!.Split('\n'));
            Assert.Equal(k.Punkte[0][0], z.Punkte[0][0], 1e-6);
            Assert.Equal(2.0 * 50 * 1.2, k.Punkte[0][1] - z.Punkte[0][1], 1e-6);   // eine Zeile tiefer
        }
        Pruefen("L WD 44/24", ["A-EG-001", "OK +2.12"]);
        Pruefen("S WD Ø 10", ["A-EG-002", "Achse +1.00"]);
        Pruefen("L DD 44/24", ["A-EG-003"]);
        // die bisherigen Kotenzeilen über ±0.00 und über OKG entfallen
        Assert.DoesNotContain(texte, t => t.Text!.Contains("OKG") || t.Text.Contains("ü. ±0.00"));
    }

    /// <summary>OG: Wand W-5 (Achse y = 6000, z 3000 … 6000), Kanal 400 × 200 bei z 4500 → Aussparung 440 × 240, OK 4620; Fertigkote OG
    /// 3000 → «OK +1.62» (SIA 410/2 Ziff. 3 2: ab der für jedes Geschoss gültigen ±0-Kote). Ohne Nummer steht das Kennzeichen unter dem
    /// Kurzzeichen. Wand W-5 ohne Material: kein Fixpunkt → Zeile «kein Fixpunkt».</summary>
    [Fact]
    public void Aussparungsplan_MitOption_OkAbFertigkoteImOG_UndKennzeichenOhneNummer_KeinFixpunkt()
    {
        var p = Aufbau(option: true);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "OG", ["punkte"] = Pts([0, 6000], [6000, 6000]), ["dicke"] = 200 }));
        Kanal(p, "KA-3", 400, 200, [3000, 5500, 4500], [3000, 6500, 4500]);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "KA-3");
        Assert.Equal(Hole(p, "OG").Id, a.Geschoss);
        Darstellung(p, "AN-OG", "aussparungen");
        var texte = Texte(Grafik.Plan(p, "AN-OG"));
        Assert.Contains(texte, t => t.Text == "L WD 44/24" && t.Fett == true);
        Assert.Contains(texte, t => t.Text == $"{a.Kennzeichen}\nOK +1.62\nkein Fixpunkt");
    }

    [Fact]
    public void Aussparungsplan_MitOption_KotenAusDemElementGehenVorDerRechnung()
    {
        var p = Aufbau(option: true);
        var a = VonLeitung(p, "KA-1");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = a.Kennzeichen, ["eigenschaften"] = new JsonObject { ["kote_uk"] = 1900, ["kote_ok"] = 2150 } }));
        Darstellung(p, "AN-EG", "aussparungen");
        Assert.Contains(Texte(Grafik.Plan(p, "AN-EG")), t => t.Text!.Contains("OK +2.15"));
    }

    [Theory]
    [InlineData("mm", "L WD 440/240", "Ø 100")]
    [InlineData("m", "L WD 0.44/0.24", "Ø 0.10")]   // 100 mm = 0.10 m (zwei Dezimalen wie die Masskette)
    public void Aussparungsplan_MitOption_EinheitDerAnsicht(string einheit, string kopf, string rund)
    {
        var p = Aufbau(option: true);
        Darstellung(p, "AN-EG", "aussparungen");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["bemassungEinheit"] = einheit } }));
        var texte = Texte(Grafik.Plan(p, "AN-EG"));
        Assert.Contains(texte, t => t.Text == kopf);
        Assert.Contains(texte, t => t.Text == $"S WD {rund}");
    }

    [Fact]
    public void Aussparungsplan_MitOption_EinheitDesGewerks_WennDieAnsichtEinGewerkZeigt()
    {
        var p = Aufbau(option: true);
        Darstellung(p, "AN-EG", "aussparungen");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["gewerke"] = new JsonArray("lueftung") } }));
        Assert.Contains(Texte(Grafik.Plan(p, "AN-EG")), t => t.Text == "L WD 440/240");   // Lüftung: mm (Vorgabe Christian 17.10.2026)
    }

    static void Legende(Projekt p, string ansicht, bool ein = true) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { ["legende"] = ein } }));

    /// <summary>Nachtrag B4e: ohne Ansicht-Eigenschaft legende keine Legende im Aussparungsplan (die Vorlage hat keine), mit legende = false
    /// ebenso.</summary>
    [Fact]
    public void Aussparungsplan_OhneLegendeEigenschaft_KeineLegende()
    {
        var p = Aufbau();
        Darstellung(p, "AN-EG", "aussparungen");
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Primitive.Any(x => x.Text?.StartsWith("Aussparungen nach SIA") == true));
        Legende(p, "AN-EG", false);
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Primitive.Any(x => x.Text?.StartsWith("Aussparungen nach SIA") == true));
    }

    [Fact]
    public void Aussparungsplan_Legende_OhneOption_KennfarbenEinheit_StatusNurAmBildschirm()
    {
        var p = Aufbau();
        Darstellung(p, "AN-EG", "aussparungen");
        Legende(p, "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        var legende = Assert.Single(g.Teile, t => t.Id is null && t.Primitive.Any(x => x.Text == "Aussparungen nach SIA 400 B.10 / SIA 410/2"));
        Assert.Equal(["Aussparungen nach SIA 400 B.10 / SIA 410/2", "in Decke, Boden (horizontal)", "in Wand, Stütze (vertikal)",
            "Masse in cm ab Rohbau; Kurzzeichen und Koten siehe Aussparungsliste"],
            legende.Primitive.Where(x => x.Art == "text" && !x.Stil.NurBildschirm).Select(x => x.Text));
        Assert.Equal(5, legende.Primitive.Count(x => x.Art == "text" && x.Stil.NurBildschirm));   // Status nur am Bildschirm
    }

    [Fact]
    public void Aussparungsplan_Legende_MitOption_KennfarbenKurzzeichenEinheit_StatusNurAmBildschirm()
    {
        var p = Aufbau(option: true);
        Darstellung(p, "AN-EG", "aussparungen");
        Legende(p, "AN-EG");
        var g = Grafik.Plan(p, "AN-EG");
        var legende = Assert.Single(g.Teile, t => t.Id is null && t.Primitive.Any(x => x.Text == "Aussparungen nach SIA 400 B.10 / SIA 410/2"));
        var gedruckt = legende.Primitive.Where(x => x.Art == "text" && !x.Stil.NurBildschirm).Select(x => x.Text).ToList();
        Assert.Equal(["Aussparungen nach SIA 400 B.10 / SIA 410/2", "in Decke, Boden (horizontal)", "in Wand, Stütze (vertikal)",
            "L DD", "Lüftung/Klima Deckendurchbruch", "L WD", "Lüftung/Klima Wanddurchbruch", "S WD", "Sanitär Wanddurchbruch",
            "Masse in cm (Breite/Länge bzw. Breite/Höhe); OK = Oberkante ab Fertigkote Geschoss in m"], gedruckt);
        // gedruckte Kästchen: Kennfarben gelb und braun
        Assert.Equal([AussparungGrafik.Gelb, AussparungGrafik.Braun], legende.Primitive.Where(x => x.Art == "polygon" && !x.Stil.NurBildschirm).Select(x => x.Stil.Fuellung));
        // Statusfarben nur am Bildschirm
        var bildschirm = legende.Primitive.Where(x => x.Stil.NurBildschirm).ToList();
        Assert.Equal(["Status (nur Bildschirm)", "vorgeschlagen", "freigegeben / versandt", "angekommen / ausgeführt", "veraltet / fehlt"],
            bildschirm.Where(x => x.Art == "text").Select(x => x.Text));
        Assert.Equal([AussparungGrafik.FarbeVorgeschlagen, AussparungGrafik.FarbeAbgegeben, AussparungGrafik.FarbeAngekommen, AussparungGrafik.FarbeFehlt],
            bildschirm.Where(x => x.Art == "polygon").Select(x => x.Stil.Fuellung));
        // links unter dem Grundriss: Wandachsen liegen bei y = 0 und x ≥ 0 (Leitungen zählen nicht mehr, sie sind im Plan nicht gezeichnet)
        Assert.All(legende.Primitive.SelectMany(x => x.Punkte), q => { Assert.True(q[1] < -300); Assert.True(q[0] >= -1e-6); });
        Assert.Equal("beschriftung", legende.Ebene);
    }

    [Fact]
    public void Aussparungsplan_OhneAussparungKeineLegende_UndNormalerGrundrissHatKeine()
    {
        var p = Aufbau();
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Primitive.Any(x => x.Text?.StartsWith("Aussparungen nach SIA") == true));   // darstellung standard
        var leer = Wandprojekt();
        Darstellung(leer, "AN-EG", "aussparungen");
        Legende(leer, "AN-EG");
        Assert.DoesNotContain(Grafik.Plan(leer, "AN-EG").Teile, t => t.Primitive.Any(x => x.Text?.StartsWith("Aussparungen nach SIA") == true));
    }

    /// <summary>Nachtrag B4e (Entscheid Christian 19.10.2026 «wie Vorlage»): im Aussparungsplan keine Gebäudetechnik – Rohre, Kanäle,
    /// Formteile und Apparate werden nicht gezeichnet; Architektur und Sinnbilder bleiben wie im gewöhnlichen Grundriss.</summary>
    [Fact]
    public void Aussparungsplan_OhneGebaeudetechnik_ArchitekturUnveraendert()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_bogen") }));
        Ok(Modul3Hilfe.Formteil(p, "fam-test-bogen", "DN 25", "BO-1", [1000, 2000, 1000], winkel: 90));
        var normal = Grafik.Plan(p, "AN-EG");
        Darstellung(p, "AN-EG", "aussparungen");
        var plan = Grafik.Plan(p, "AN-EG");
        var rohr = Hole(p, "RO-1").Id;
        string[] hlks = [rohr, Hole(p, "KA-1").Id, Hole(p, "KA-2").Id, Hole(p, "BO-1").Id];

        // gewöhnlicher Grundriss: Rohr, Kanäle und Formteil sind da; im Aussparungsplan nicht
        Assert.All(hlks, id => Assert.Contains(normal.Teile, t => t.Id == id));
        Assert.All(hlks, id => Assert.DoesNotContain(plan.Teile, t => t.Id == id));

        // Architektur (Wände) und die Sinnbilder der Aussparungen bleiben genau wie im normalen Grundriss
        string Arch(Plangrafik g) => Json(g.Teile.Where(t => t.Ebene is "wand" or "oeffnung" or "raum" or "achse" && !(t.Id is not null && t.Id == rohr)).ToList());
        Assert.Equal(Arch(normal), Arch(plan));
        foreach (var kz in new[] { "RO-1", "KA-1", "KA-2" })
        {
            var a = VonLeitung(p, kz);
            Assert.Equal(Json(Symbol(normal, a)), Json(Symbol(plan, a)));
        }
    }

    // ------------------------------------------------------------------ 3D

    static Koerper Koerper(Projekt p, Element a) => Assert.Single(Grafik.Koerper(p, [a.Id]));

    [Fact]
    public void Koerper_RechteckigInWand_QuaderBTiefeHAlsHellerHalbtransparenterKoerper()
    {
        var p = Aufbau();
        var k = Koerper(p, VonLeitung(p, "KA-1"));
        Assert.Equal("oeffnung", k.Ebene);
        var t = Assert.Single(k.Teile);
        Assert.Equal("quader", t.Art);
        Assert.Equal([2000, 0, 2000], t.Mitte);
        Assert.Equal([440, 200, 240], t.Masse);       // b in der Wand, Tiefe quer, h senkrecht
        Assert.Equal(0, t.Drehung!.Value, 1e-9);
        Assert.Equal(AussparungGrafik.KoerperDeckkraft, t.Deckkraft);
        Assert.True(t.NichtSchneiden);
        // 60 % zu Weiss aufgehellt: #e08a1e → 224 → 243, 138 → 208, 30 → 165
        Assert.Equal("#f3d0a5", t.Farbe);
    }

    [Fact]
    public void Koerper_RundInWand_ZylinderUmDieAchseUeberDieBauteildicke()
    {
        var p = Aufbau();
        var t = Assert.Single(Koerper(p, VonLeitung(p, "RO-1")).Teile);
        Assert.Equal("zylinder", t.Art);
        Assert.Equal([3000, -100, 1000], t.Von);
        Assert.Equal([3000, 100, 1000], t.Nach);
        Assert.Equal(50, t.Radius!.Value, 1e-9);   // Ø 100 (95 → ganze cm)
        Assert.True(t.NichtSchneiden);
        Assert.Equal(AussparungGrafik.KoerperDeckkraft, t.Deckkraft);
    }

    [Fact]
    public void Koerper_InDecke_QuaderBHTiefe()
    {
        var p = Aufbau();
        var t = Assert.Single(Koerper(p, VonLeitung(p, "KA-2")).Teile);
        Assert.Equal("quader", t.Art);
        Assert.Equal([4000, 2000, 2875], t.Mitte);
        Assert.Equal([440, 240, 250], t.Masse);
        Assert.Equal(0, t.Drehung!.Value, 1e-9);
    }

    [Fact]
    public void Koerper_FarbeFolgtDemStatus()
    {
        var p = Aufbau();
        var a = VonLeitung(p, "KA-1");
        foreach (var s in new[] { "freigegeben", "versandt", "angekommen" }) Ok(Status(p, s, a.Kennzeichen));
        // grün #2e9e5b → 60 % zu Weiss: 46 → 171, 158 → 216, 91 → 189
        Assert.Equal("#abd8bd", Koerper(p, Hole(p, a.Kennzeichen)).Teile[0].Farbe);
    }

    [Fact]
    public void Koerper_SchraegeWand_QuaderMitDrehungUmZ()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-D", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 300, ["h"] = 200, ["tiefe"] = 250, ["achse"] = new JsonArray(1, 1, 0), ["status"] = "vorgeschlagen" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([8000, 8000, 1500]) },
        }));
        var t = Assert.Single(Koerper(p, Hole(p, "AUS-D")).Teile);
        Assert.Equal("quader", t.Art);
        Assert.Equal([300, 250, 200], t.Masse);
        Assert.Equal(-45, t.Drehung!.Value, 1e-9);      // U = z × n = (−1, 1, 0)/√2, nach +x gerichtet → (1, −1, 0)/√2
        // Grundriss: Umriss b = 300 entlang U, Tiefe 250 entlang der Achse – Hülle der gedrehten Ecken
        var umriss = Umriss(Symbol(Grafik.Plan(p, "AN-EG"), Hole(p, "AUS-D")));
        Assert.Equal(4, umriss.Punkte.Count);
        Assert.Equal(300 * 250, PolyFlaeche(umriss), 0.1);   // Ecken sind auf 0.0001 mm gerundet
    }

    [Fact]
    public void Koerper_SchraegeAchse_GeschlossenesNetzMitRichtigemVolumen()
    {
        var p = Aufbau();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-S", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 100, ["h"] = 80, ["tiefe"] = 50, ["achse"] = new JsonArray(1, 0, 1), ["status"] = "vorgeschlagen" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([8000, 8000, 1500]) },
        }));
        var t = Assert.Single(Koerper(p, Hole(p, "AUS-S")).Teile);
        Assert.Equal("koerper", t.Art);
        Assert.Equal(8, t.Ecken!.Count);
        Assert.Equal(12, t.Dreiecke!.Count);
        // Volumen über die Dreiecke (Normale nach aussen → positiv) = b × h × Tiefe
        double v = 0;
        foreach (var d in t.Dreiecke)
        {
            var a = t.Ecken[d[0]]; var b = t.Ecken[d[1]]; var c = t.Ecken[d[2]];
            v += (a[0] * (b[1] * c[2] - b[2] * c[1]) - a[1] * (b[0] * c[2] - b[2] * c[0]) + a[2] * (b[0] * c[1] - b[1] * c[0])) / 6;
        }
        Assert.Equal(100.0 * 80 * 50, v, 1e-3);
    }

    [Fact]
    public void Schnitt_AussparungWirdNichtGeschnitten_WandBleibt()
    {
        // B4c: Aussparungen erscheinen im Schnitt nur als Sinnbild über der Grafik (AussparungSchnittTests); der Körper schneidet
        // weiterhin nicht – die Wand wird nicht ausgespart.
        var p = Aufbau();
        // Schnitt durch die Wand W-1 und die Bohrung RO-1 (x = 3000, von Süd nach Nord)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "S-1", ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([3000, -1000], [3000, 5000]) },
        }));
        var g = Grafik.Plan(p, "S-1");
        Assert.Contains(g.Teile, t => t.Id == Hole(p, "W-1").Id);
        // das Sinnbild ist die einzige Grafik der Aussparung: keine Schnittfläche, keine Sichtkanten des Körpers
        foreach (var t in g.Teile.Where(t => AussparungenIds(p).Contains(t.Id!)))
            Assert.All(t.Primitive, x =>
            {
                Assert.NotEqual("schnitt", x.Stil.Fuellung);
                Assert.True(x.Stil.NurBildschirm ? x.Stil.Farbe == AussparungGrafik.FarbeVorgeschlagen : x.Stil.Farbe is null);
            });
        Assert.DoesNotContain(g.Teile, t => t.Id == VonLeitung(p, "KA-2").Id);     // KA-2 (x = 4000) liegt hinter der Schnittebene (w < 0)
    }

    static HashSet<string> AussparungenIds(Projekt p) => Aussparungen(p).Select(a => a.Id).ToHashSet();

    // ------------------------------------------------------------------ Kollisionsmarken

    static Element Kollision(Projekt p, string kz, string art = "hart", string status = "neu", double x = 3000, double y = 0, double z = 1000, string geschoss = "EG")
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = kz, ["geschoss"] = geschoss,
            ["eigenschaften"] = new JsonObject { ["art"] = art, ["typ"] = "kontakt", ["status"] = status, ["mass"] = 12 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([x, y, z]) },
        }));
        return Hole(p, kz);
    }

    [Theory]
    [InlineData("hart", "neu", KollisionGrafik.FarbeHart)]
    [InlineData("hart", "bestaetigt", KollisionGrafik.FarbeHart)]
    [InlineData("weich", "neu", KollisionGrafik.FarbeWeich)]
    [InlineData("hart", "behoben", KollisionGrafik.FarbeErledigt)]
    [InlineData("weich", "akzeptiert", KollisionGrafik.FarbeErledigt)]
    [InlineData("hart", "erledigt", KollisionGrafik.FarbeErledigt)]
    public void Kollisionsmarke_KreisMitKreuzNurAmBildschirm_FarbeNachArtUndStatus(string art, string status, string farbe)
    {
        var p = Aufbau();
        var k = Kollision(p, "KOL-1", art, status);
        var teil = Assert.Single(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == k.Id);
        Assert.Equal("symbol", teil.Ebene);
        var kreis = Assert.Single(teil.Primitive, x => x.Art == "kreis");
        Assert.Equal(1.5 * 50, kreis.Radius!.Value, 1e-9);      // Radius 1.5 mm Papier
        Nahe([3000, 0], kreis.Punkte[0]);
        Assert.Equal(farbe, kreis.Stil.Farbe);
        Assert.Equal(2, teil.Primitive.Count(x => x.Art == "linie"));
        Assert.Equal("KOL-1", Assert.Single(teil.Primitive, x => x.Art == "text").Text);
        Assert.All(teil.Primitive, x => Assert.True(x.Stil.NurBildschirm));
        Assert.Equal(farbe, KollisionGrafik.Farbe(art, status));
    }

    [Fact]
    public void Kollisionsmarke_NurImGrundrissDesGeschosses_UndAusblendbar()
    {
        var p = Aufbau();
        var k = Kollision(p, "KOL-1");
        Assert.DoesNotContain(Grafik.Plan(p, "AN-OG").Teile, t => t.Id == k.Id);      // anderes Geschoss
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["klassenAusblenden"] = new JsonArray("Kollision") } }));
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == k.Id);      // Klasse ausgeblendet
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["klassenAusblenden"] = null } }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["gewerke"] = new JsonArray("sanitaer") } }));
        Assert.Contains(Grafik.Plan(p, "AN-EG").Teile, t => t.Id == k.Id);            // Gewerk «allgemein» bleibt sichtbar
    }

    [Fact]
    public void Kollisionsmarke_3DKugelNichtGeschnitten_KeinePlanausgabeUndKeinSchnitt()
    {
        var p = Aufbau();
        var k = Kollision(p, "KOL-1", "weich");
        var kk = Assert.Single(Grafik.Koerper(p, [k.Id]));
        var t = Assert.Single(kk.Teile);
        Assert.Equal("kugel", t.Art);
        Assert.Equal([3000, 0, 1000], t.Mitte);
        Assert.Equal(KollisionGrafik.KugelRadius, t.Radius);
        Assert.Equal(KollisionGrafik.FarbeWeich, t.Farbe);
        Assert.True(t.NichtSchneiden);
        // Schnitt: keine Marke
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "S-1", ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([3000, -1000], [3000, 5000]) },
        }));
        Assert.DoesNotContain(Grafik.Plan(p, "S-1").Teile, tt => tt.Id == k.Id);
    }

    // ------------------------------------------------------------------ Plan-PDF

    /// <summary>Plan-PDF nach H+K-Vorlage (Option aus): Kennfarben, Massketten in Magenta und die Legende stehen darin; Nummern,
    /// Kurzzeichen, Koten, der Status (Farbe des Rahmens, Statuslegende) und die Kollisionsmarken nicht. Farben im Inhaltsstrom als
    /// «r g b rg/RG» in 0…1: braun #7f3f00 = 127/63/0 → 0.498 0.247 0, gelb #ffc000 = 255/192/0 → 1 0.753 0, magenta #ff00ff → 1 0 1,
    /// vorgeschlagen #e08a1e = 224/138/30 → 0.87 0.54 0.11. Masskette KA-1 (Beton): 100 | 1780 | 2220 → «168», «44».</summary>
    [Fact]
    public void PlanPdf_AussparungsplanNachVorlage_OhneBeschriftungOhneStatusUndOhneKollisionsmarken()
    {
        var p = Aufbau();
        Beton(p);
        Nummerieren(p);
        Kollision(p, "KOL-1");
        Darstellung(p, "AN-EG", "aussparungen");
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-101", ["titel"] = "Aussparungsplan EG", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new JsonArray(210, 150) }));

        // unkomprimiert: Text und Zahlen lassen sich im Inhaltsstrom finden
        var pdf = Planausgabe.Pdf(p, "PB-1", unkomprimiert: true);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var s = Encoding.Latin1.GetString(pdf);
        Assert.DoesNotContain("A-EG-001", s);                  // Nummer nur am Bildschirm
        Assert.DoesNotContain("L WD", s);                      // Kurzzeichen in Liste und IFC
        Assert.DoesNotContain("OK +2.12", s);
        Assert.Contains("168", s);                             // Massketten
        Assert.DoesNotContain("Aussparungen nach SIA", s);      // ohne Ansicht-Eigenschaft legende keine Legende (Vorlage, B4e)
        Assert.Matches(new Regex(@"0\.49\d* 0\.24\d* 0 (rg|RG)"), s);          // Kennfarbe braun
        Assert.Matches(new Regex(@"1 0\.75\d* 0 (rg|RG)"), s);                 // Kennfarbe gelb
        Assert.Matches(new Regex(@"1 0 1 (rg|RG)"), s);                        // Massketten magenta
        Assert.DoesNotMatch(new Regex(@"0\.87\d* 0\.54\d* 0\.11\d* (rg|RG)"), s);   // Status «vorgeschlagen» nur am Bildschirm
        Assert.DoesNotContain("Status (nur Bildschirm)", s);
        Assert.DoesNotContain("KOL-1", s);                     // Kollisionsmarken nur am Bildschirm

        // Abfrage plan_pdf schreibt die Datei
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_aussparungsplan_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ziel = Path.Combine(ordner, "aussparungsplan.pdf");
            var a = (JsonObject)Hlks.Kern.Tuer.Abfrage(p, "plan_pdf", new JsonObject { ["planblatt"] = "PB-1", ["datei"] = ziel });
            Assert.Equal(ziel, a["datei"]!.GetValue<string>());
            Assert.Equal(1, a["seiten"]!.GetValue<int>());
            var bytes = File.ReadAllBytes(ziel);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.True(bytes.Length > 5000);
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }

    [Fact]
    public void PlanPdf_MitOption_DruecktKurzzeichenUndNummer()
    {
        var p = Aufbau(option: true);
        Beton(p);
        Nummerieren(p);
        Darstellung(p, "AN-EG", "aussparungen");
        Legende(p, "AN-EG");
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-103", ["titel"] = "Aussparungsplan EG SIA", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG", ["lage"] = new JsonArray(210, 150) }));
        var s = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.Contains("A-EG-001", s);
        Assert.Contains("L WD 44/24", s);
        Assert.Contains("OK +2.12", s);
        Assert.Contains("Wanddurchbruch", s);                  // Kurzzeichen in der Legende
    }

    [Fact]
    public void PlanPdf_OG_OhneAussparungenUndMitNegativerKoteGibtKeinenFehler()
    {
        var p = Aufbau(option: true);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-N", ["geschoss"] = "OG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 300, ["h"] = 200, ["tiefe"] = 200, ["achse"] = new JsonArray(0, 1, 0), ["status"] = "vorgeschlagen" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 6000, 2800]) },   // OK 2900 unter der Fertigkote OG 3000 → «OK −0.10»
        }));
        Nummerieren(p);
        Darstellung(p, "AN-OG", "aussparungen");
        Assert.Contains(Texte(Grafik.Plan(p, "AN-OG")), t => t.Text!.Contains("OK −0.10"));
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-102", ["titel"] = "Aussparungsplan OG", ["format"] = "A3" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-OG", ["lage"] = new JsonArray(210, 150) }));
        var pdf = Planausgabe.Pdf(p, "PB-1");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5000);
    }
}
