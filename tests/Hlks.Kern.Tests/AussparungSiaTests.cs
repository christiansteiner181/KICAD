using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Ifc4.Interfaces;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, Nachtrag B4d: Darstellung der Aussparungen nach H+K-Vorlage Aussparungsplan Bernstrasse 1.UG (24.09.2024) und
/// SIA 400:2000 Ziff. B.10 (S. 41–42) / SIA 410/2 – Kurzzeichen (Tabelle 5), Sinnbild «beides» (Kennfarbe geteilt, Winkel, Wandstrich,
/// Diagonale), Massketten ab Fixpunkten (Vorlage: je Richtung ab der näheren Rohbaukante mit dem Aussparungsmass, magenta, gestaffelt;
/// SIA B.10.3), Kurzzeichen in Liste und IFC. Alle Werte FIKTIV (AussparungHilfe: Spiel 20, Zusammenfassen 100; Testrohrserie DN 50:
/// da 55; Wände 200, Decke 250).
/// Wandprojekt: Wände W-1 (Süd, Achse y = 0) bis W-4 (West, Achse x = 0) um 6000 × 4000, Dicke 200; Körper an berührten Enden um die
/// halbe Dicke verlängert (ArchitekturGrafik.WandKoerper) – Innenflächen: W-1 y = 100, W-2 x = 5900, W-3 y = 3900, W-4 x = 100;
/// Aussenecken bei x = −100 / 6100, y = −100 / 4100. EG Kote 0 (Höhe 3000), OG Kote 3000. Grundriss AN-EG 1:50, Schnitthöhe 1000.
/// Massketten: Einheit cm (Ansicht ohne Angabe, ANNAHME wie Vorlage und Figur 49), Masslinie 6 mm Papier = 300 mm neben der Aussparung,
/// Staffel 6 mm Papier = 300 mm.</summary>
public class AussparungSiaTests
{
    static void Darstellung(Projekt p, string ansicht = "AN-EG") =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { ["darstellung"] = "aussparungen" } }));

    static void Material(Projekt p, string wand, string material) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = wand, ["eigenschaften"] = new JsonObject { ["material"] = material } }));

    static Grafikteil Symbol(Plangrafik g, Element a) => Assert.Single(g.Teile, t => t.Id == a.Id);

    static List<Primitiv> Texte(Plangrafik g) => g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text").ToList();

    static readonly Regex Zahl = new(@"^[0-9]+(\.[0-9]+)?$");

    /// <summary>Massketten: gemeinsame Teile ohne Id mit Linien und nur Zahlen als Text.</summary>
    static List<Grafikteil> Ketten(Plangrafik g) =>
        g.Teile.Where(t => t.Id is null && t.Ebene == "beschriftung" && t.Primitive.Count(x => x.Art == "linie") >= 3
                           && t.Primitive.Where(x => x.Art == "text").All(x => Zahl.IsMatch(x.Text!)) && t.Primitive.Any(x => x.Art == "text")).ToList();

    static List<string> Zahlen(Grafikteil kette) => kette.Primitive.Where(x => x.Art == "text").Select(x => x.Text!).ToList();

    /// <summary>Die Masslinie: die längste dünne Linie der Kette.</summary>
    static double[][] Masslinie(Grafikteil kette) => kette.Primitive.Where(x => x.Art == "linie" && x.Stil.Stift == Stifte.Duenn)
        .Select(x => x.Punkte.ToArray()).MaxBy(l => Math.Abs(l[1][0] - l[0][0]) + Math.Abs(l[1][1] - l[0][1]))!;

    // ------------------------------------------------------------------ Kurzzeichen (SIA 400 B.10.2 Tabelle 5)

    [Fact]
    public void Kurzzeichen_SanitaerDeckendurchbruch_SDD()
    {
        var p = Wandprojekt();
        Decke(p);
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [3000, 2000, 2500], [3000, 2000, 3500]));   // System KW: Trinkwasser kalt → sanitaer
        Ok(Vorschlagen(p));
        var k = AussparungKennzeichnung.Von(p.Modell, p.Katalog, VonLeitung(p, "RO-1"))!;
        Assert.Equal(("DD", "S", Bauteillage.Horizontal), (k.Art, k.Gewerk, k.Lage));
        Assert.Equal("S DD", k.Kurzzeichen);
        Assert.Equal("Sanitär Deckendurchbruch", k.Text);
    }

    [Fact]
    public void Kurzzeichen_HeizungWanddurchbruch_HWD()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "HZ", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Heizung Vorlauf" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "RO-H", ["system"] = "HZ", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 50" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([2000, -500, 1000], [2000, 500, 1000]) },
        }));
        Ok(Vorschlagen(p));
        var k = AussparungKennzeichnung.Von(p.Modell, p.Katalog, VonLeitung(p, "RO-H"))!;
        Assert.Equal(("WD", "H", Bauteillage.Vertikal), (k.Art, k.Gewerk, k.Lage));
        Assert.Equal("H WD", k.Kurzzeichen);
    }

    /// <summary>Sammelaussparung in D-1 aus einem Rohr (KW, sanitaer) und einem Kanal (ZUL, lueftung): Rohr 95 × 95 um x 3000 (2952.5 …
    /// 3047.5), Kanal 440 × 240 um x 3300 (3080 … 3520) – Randabstand 32.5 &lt; 100 → eine Aussparung; zwei Gewerke → allgemein «DD».</summary>
    [Fact]
    public void Kurzzeichen_MehrereGewerke_AllgemeinDD()
    {
        var p = Wandprojekt();
        Decke(p);
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [3000, 2000, 2500], [3000, 2000, 3500]));
        Kanal(p, "KA-1", 400, 200, [3300, 2000, 2500], [3300, 2000, 3500]);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(["KA-1", "RO-1"], LeitungKz(p, a).Order());
        var k = AussparungKennzeichnung.Von(p.Modell, p.Katalog, a)!;
        Assert.Null(k.Gewerk);
        Assert.Equal("DD", k.Kurzzeichen);
        Assert.Equal("allgemeiner Deckendurchbruch", k.Text);
    }

    /// <summary>Bodenplatte BP-1 im EG: Oberkante 0, Dicke 250 → z −250 … 0. Rohr senkrecht bei (1000, 1000): Aussparung UK −250;
    /// Unterkante − 1 mm = −251 liegt in keinem Geschossband (EG −1 … 3000, OG 2999 … 6000) → Fundamentdurchbruch «S FD»; Geschoss der
    /// Aussparung = das der Platte (EG, Geschoss-Regel ohne Geschoss darunter).</summary>
    [Fact]
    public void Kurzzeichen_DeckeOhneGeschossDarunter_Fundamentdurchbruch_SFD()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "BP-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 0], [6000, 0, 0], [6000, 4000, 0], [0, 4000, 0]), ["dicke"] = 250 },
        }));
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [1000, 1000, -500], [1000, 1000, 500]));
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal(Hole(p, "EG").Id, a.Geschoss);
        Etwa(-250, a.Zahl("kote_uk"), 1e-6, "UK");
        Assert.True(AussparungKennzeichnung.OhneGeschossDarunter(p.Modell, a));
        Assert.Equal("S FD", AussparungKennzeichnung.Kurzzeichen(p.Modell, p.Katalog, a));
        var zeile = ((JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject()))["zeilen"]![0]!;
        Assert.Equal("S FD", zeile["kurzzeichen"]!.GetValue<string>());
    }

    [Fact]
    public void Kurzzeichen_OhneBauteilAusDerAchse_OhneAchseNull()
    {
        var p = Wandprojekt();
        foreach (var (kz, achse) in new[] { ("AUS-W", new JsonArray(0, 1, 0)), ("AUS-D", new JsonArray(0, 0, 1)) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Aussparung", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 100, ["h"] = 100, ["tiefe"] = 200, ["achse"] = achse },
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 1000, 1000]) },
            }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-X", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["form"] = "rund", ["d"] = 100 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 1000, 1000]) },
        }));
        Assert.Equal("WD", AussparungKennzeichnung.Kurzzeichen(p.Modell, p.Katalog, Hole(p, "AUS-W")));   // Achse waagrecht → Wand
        Assert.Equal("DD", AussparungKennzeichnung.Kurzzeichen(p.Modell, p.Katalog, Hole(p, "AUS-D")));   // Achse senkrecht → Decke
        Assert.Null(AussparungKennzeichnung.Kurzzeichen(p.Modell, p.Katalog, Hole(p, "AUS-X")));          // weder Bauteil noch Achse
    }

    [Theory]
    [InlineData("IfcSlab", Bauteillage.Horizontal)]
    [InlineData("IfcSlabStandardCase", Bauteillage.Horizontal)]
    [InlineData("IfcRoof", Bauteillage.Horizontal)]
    [InlineData("IfcWallStandardCase", Bauteillage.Vertikal)]
    [InlineData("IfcColumn", Bauteillage.Vertikal)]
    [InlineData("IfcBeam", Bauteillage.Vertikal)]       // ANNAHME: Träger vertikal (Durchbruch quer durch den Steg)
    [InlineData("IfcCovering", null)]
    public void Lage_AusDerIfcEntitaet(string entitaet, Bauteillage? lage) =>
        Assert.Equal(lage, AussparungKennzeichnung.LageVonEntitaet(entitaet));

    [Fact]
    public void Gewerk_Kurzzeichen_TabelleNachSia_MsrlAlsElektroAnnahme()
    {
        Assert.Equal(["E", "H", "K", "L", "S"], AussparungKennzeichnung.GewerkTexte.Keys.Order());
        Assert.Equal("S", AussparungKennzeichnung.GewerkKurz["sanitaer"]);
        Assert.Equal("H", AussparungKennzeichnung.GewerkKurz["heizung"]);
        Assert.Equal("L", AussparungKennzeichnung.GewerkKurz["lueftung"]);
        Assert.False(AussparungKennzeichnung.GewerkKurz.ContainsKey("kaelte"));   // Kälte gehört zur Heizung (Entscheid Christian 20.10.2026): Kälteleitungen tragen H
        Assert.Equal("E", AussparungKennzeichnung.GewerkKurz["elektro"]);
        Assert.Equal("E", AussparungKennzeichnung.GewerkKurz["msrl"]);   // ANNAHME
        Assert.Equal(["BD", "DD", "FD", "WD"], AussparungKennzeichnung.ArtTexte.Keys.Order());
    }

    // ------------------------------------------------------------------ Sinnbild «beides» (Tabelle 5, dritte Zeile)

    /// <summary>Aussparung von Hand durch die Decke D-1 (horizontal) und die Wand W-1 (vertikal): 300 × 200 um (2000, 2000), Achse
    /// senkrecht → Kennfarbe diagonal geteilt (gelb oben links, braun unten rechts), Winkel oben/links UND Wandstrich, Diagonale
    /// von unten links (1850, 1900) nach oben rechts (2150, 2100). Haupt-Bauteil D-1 → Art DD.</summary>
    [Fact]
    public void Sinnbild_Beides_KennfarbeGeteilt_ZweiErgaenzungenUndDiagonale()
    {
        var p = Wandprojekt();
        Decke(p);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-B", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 300, ["h"] = 200, ["tiefe"] = 250, ["achse"] = new JsonArray(0, 0, 1), ["status"] = "vorgeschlagen" },
            ["bezuege"] = new JsonObject { ["bauteil"] = "D-1", ["bauteil2"] = "W-1" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([2000, 2000, 2875]) },
        }));
        var a = Hole(p, "AUS-B");
        var k = AussparungKennzeichnung.Von(p.Modell, p.Katalog, a)!;
        Assert.Equal((Bauteillage.Beides, "DD"), (k.Lage, k.Art));
        var teil = Symbol(Grafik.Plan(p, "AN-EG"), a);
        var kf = AussparungGrafikTests.Kennfarben(teil);
        Assert.Equal(2, kf.Count);
        var gelb = Assert.Single(kf, x => x.Stil.Fuellung == AussparungGrafik.Gelb);
        var braun = Assert.Single(kf, x => x.Stil.Fuellung == AussparungGrafik.Braun);
        Assert.Contains(gelb.Punkte, q => Math.Abs(q[0] - 1850) < 1e-6 && Math.Abs(q[1] - 2100) < 1e-6);    // Ecke oben links
        Assert.Contains(braun.Punkte, q => Math.Abs(q[0] - 2150) < 1e-6 && Math.Abs(q[1] - 1900) < 1e-6);   // Ecke unten rechts
        Assert.Equal(3, gelb.Punkte.Count);
        Assert.Equal(3, braun.Punkte.Count);
        Assert.Equal(2, teil.Primitive.Count(x => x.Art == "polylinie" && x.Stil.Stift == Stifte.Duenn));   // Winkel und Wandstrich
        var diag = Assert.Single(teil.Primitive, x => x.Art == "linie");
        Nahe([1850, 1900], diag.Punkte[0]);
        Nahe([2150, 2100], diag.Punkte[1]);
        Assert.Equal(300 * 200, PolyFlaeche(AussparungGrafikTests.Umriss(teil)), 1e-6);
    }

    // ------------------------------------------------------------------ Massketten (H+K-Vorlage Bernstrasse 1.UG, SIA 400 B.10.3)

    /// <summary>Wände aus Beton. Regel der Vorlage: je Richtung EINE Kette von der näheren Rohbaukante zur nächsten Seitenkante, dazu das
    /// Mass der Aussparung (rund: bis zur Achse), magenta, cm.
    /// W-1: KA-1 (440 breit um x 2000: 1780 … 2220): Strahlen entlang W-1 (Achse y 0 und 1 mm ausserhalb der Flächen, y ±101); links
    /// trifft der Strahl y 101 die Innenfläche der Querwand W-4 (x 100; auf der Achse läge erst die Aussenecke x −100 des vereinigten
    /// Umrisses): 1780 − 100 = 1680; rechts W-2 (x 5900): 5900 − 2220 = 3680 → links: 100 | 1780 | 2220 = «168», «44».
    /// RO-1 von Hand rund, Achse x 4000: links 4000 − 100 = 3900, rechts 5900 − 4000 = 1900 → rechts: 4000 | 5900 = «190».
    /// Kettenseite: Wandfläche zum Schwerpunkt der Fixpunkte (Rauminnere, y = +100), Masslinie 100 + 6 mm Papier (300) = 400; die beiden
    /// Ketten überdecken sich nicht (100 … 2220 und 4000 … 5900) → keine Staffel.
    /// D-1 (Geschoss darunter = EG): KA-2 440 × 240 um (4000, 2000). x: links 3780 − 100 = 3680, rechts 5900 − 4220 = 1680 → rechts:
    /// 3780 | 4220 | 5900 = «44», «168», Masslinie unter der Aussparung y = 1880 − 300 = 1580. y: unten 1880 − 100 = 1780, oben
    /// 3900 − 2120 = 1780 → Gleichstand, unten: 100 | 1880 | 2120 = «178», «24», Masslinie links x = 3780 − 300 = 3480.</summary>
    [Fact]
    public void Massketten_NaechsteRohbaukanteUndAussparungsmass_Magenta()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p);
        RohrDurchW1(p, "RO-1", 4000);
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-1");
        Darstellung(p);
        var g = Grafik.Plan(p, "AN-EG");
        var ketten = Ketten(g);
        Assert.Equal(4, ketten.Count);
        var ka1 = Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["168", "44"]));
        var ml = Masslinie(ka1);
        Nahe([100, 400], ml[0]); Nahe([2220, 400], ml[1]);
        var ro1 = Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["190"]));
        ml = Masslinie(ro1);
        Nahe([4000, 400], ml[0]); Nahe([5900, 400], ml[1]);
        var x = Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["44", "168"]));
        ml = Masslinie(x);
        Nahe([3780, 1580], ml[0]); Nahe([5900, 1580], ml[1]);
        var y = Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["178", "24"]));
        ml = Masslinie(y);
        Nahe([3480, 100], ml[0]); Nahe([3480, 2120], ml[1]);
        // Massketten sind Planinhalt (gedruckt), magenta, ohne Element-Id; Massstriche als Schrägstriche (45°); keine Zeile «kein Fixpunkt»
        Assert.All(ketten, k => Assert.All(k.Primitive, pr => { Assert.False(pr.Stil.NurBildschirm); Assert.Equal(AussparungMassketten.Farbe, pr.Stil.Farbe); }));
        Assert.Contains(x.Primitive, pr => pr.Art == "linie" && pr.Stil.Stift == Stifte.Mittel
            && Math.Abs(Math.Abs(pr.Punkte[1][0] - pr.Punkte[0][0]) - Math.Abs(pr.Punkte[1][1] - pr.Punkte[0][1])) < 1e-6);
        Assert.DoesNotContain(Texte(g), t => t.Text!.Contains(AussparungMassketten.KeinFixpunkt));
        // nur im Aussparungsplan: der gewöhnliche Grundriss hat keine Massketten
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = null } }));
        Assert.Empty(Ketten(Grafik.Plan(p, "AN-EG")));
    }

    /// <summary>Staffeln wie die Vorlage: KA-2 (4000, 2000) und KA-5 (5000, 2000) in D-1, Randabstand 4780 − 4220 = 560 ≥ 100 → zwei
    /// Aussparungen. x: KA-2 rechts 3780 | 4220 | 5900 «44», «168»; KA-5 rechts (5900 − 5220 = 680 &lt; 4780 − 100) 4780 | 5220 | 5900
    /// «44», «68». Beide Masslinien lägen bei y 1580 und überdecken sich (3780 … 5900, 4780 … 5900) → KA-2 (kleinere Lage entlang der
    /// Kette) bleibt bei 1580, KA-5 rückt eine Staffel (6 mm Papier = 300) weiter nach aussen: y 1280. Die y-Ketten (x 3480 bzw. 4480)
    /// liegen 1000 auseinander – keine Staffel.</summary>
    [Fact]
    public void Massketten_UeberdeckendeKettenWerdenGestaffelt()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Kanal(p, "KA-5", 400, 200, [5000, 2000, 2500], [5000, 2000, 3500]);
        Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        Darstellung(p);
        var ketten = Ketten(Grafik.Plan(p, "AN-EG"));
        Assert.Equal(1580, Masslinie(Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["44", "168"])))[0][1], 1e-6);
        Assert.Equal(1280, Masslinie(Assert.Single(ketten, k => Zahlen(k).SequenceEqual(["44", "68"])))[0][1], 1e-6);
        var yKetten = ketten.Where(k => Zahlen(k).SequenceEqual(["178", "24"])).Select(k => Masslinie(k)[0][0]).Order().ToList();
        Assert.Equal([3480.0, 4480.0], yKetten);
    }

    /// <summary>W-4 Leichtbau (kein Fixpunkt), übrige Beton. KA-2 in D-1: x-Strahl nach links trifft keinen Fixpunkt (W-1 und W-3
    /// laufen parallel), nach rechts W-2 (5900) → 3780 | 4220 | 5900 = «44», «168». KA-1 in W-1: links ist W-4 kein Fixpunkt, der Strahl
    /// auf der Achse von W-1 endet an deren eigener Stirn (Aussenecke x −100, Rohbau): 1780 + 100 = 1880 &lt; rechts 3680 → −100 | 1780 |
    /// 2220 = «188», «44» – nicht ab der Leichtbauwand (das gäbe «168»).</summary>
    [Fact]
    public void Massketten_LeichtbauwandIstKeinFixpunkt()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p, "W-1", "W-2", "W-3");
        Material(p, "W-4", "leichtbau");
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Ok(Vorschlagen(p));
        Darstellung(p);
        var ketten = Ketten(Grafik.Plan(p, "AN-EG"));
        Assert.Contains(ketten, k => Zahlen(k).SequenceEqual(["44", "168"]));
        Assert.Contains(ketten, k => Zahlen(k).SequenceEqual(["188", "44"]));
        Assert.Contains(ketten, k => Zahlen(k).SequenceEqual(["178", "24"]));
        Assert.DoesNotContain(ketten, k => Zahlen(k).SequenceEqual(["168", "44"]));   // nicht ab der Leichtbauwand (x 100)
    }

    /// <summary>Stütze und tragende Wand (ohne Material) sind Fixpunkte. Stütze 300 × 300 um (3000, 2000): rechte Fläche x 3150 → KA-2
    /// (4000, 2000) links 3780 − 3150 = 630 &lt; rechts 5900 − 4220 = 1680 → 3150 | 3780 | 4220 = «63», «44». KA-3 (5000, 1000): links kein
    /// Fixpunkt (Stütze liegt bei y 1850 … 2150, W-4 ohne Material), rechts W-2 tragend: 5900 − 5220 = 680 → 4780 | 5220 | 5900 = «44»,
    /// «68». y: W-1 und W-3 ohne Material, nicht tragend → «kein Fixpunkt in y» (nur am Bildschirm, Vorgabe ohne Beschriftung am Symbol).</summary>
    [Fact]
    public void Massketten_StuetzeUndTragendeWandSindFixpunkte()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-2", ["eigenschaften"] = new JsonObject { ["tragend"] = true } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Stuetze", ["kennzeichen"] = "STZ-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([2850, 1850, 0], [3150, 1850, 0], [3150, 2150, 0], [2850, 2150, 0]), ["hoehe"] = 2750 },
        }));
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Kanal(p, "KA-3", 400, 200, [5000, 1000, 2500], [5000, 1000, 3500]);
        Ok(Vorschlagen(p));
        Darstellung(p);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Contains(Ketten(g), k => Zahlen(k).SequenceEqual(["63", "44"]));
        Assert.Contains(Ketten(g), k => Zahlen(k).SequenceEqual(["44", "68"]));
        var hinweis = Texte(g).Where(t => t.Text!.EndsWith($"{AussparungMassketten.KeinFixpunkt} in y", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, hinweis.Count);
        Assert.All(hinweis, t => Assert.True(t.Stil.NurBildschirm));
    }

    [Fact]
    public void Massketten_OhneFixpunkt_KeineKette_HinweisAmBildschirm()
    {
        var p = Wandprojekt();   // Wände ohne Material, nicht tragend: kein Fixpunkt
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Darstellung(p);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Empty(Ketten(g));
        Assert.Contains(Texte(g), t => t.Text == "A-EG-001" && t.Stil.NurBildschirm);
        Assert.Contains(Texte(g), t => t.Text == AussparungMassketten.KeinFixpunkt && t.Stil.NurBildschirm);
    }

    [Fact]
    public void Massketten_MitOption_HinweisGedruckt()
    {
        var p = AussparungGrafikTests.WandprojektMitOption();
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Darstellung(p);
        Assert.Contains(Texte(Grafik.Plan(p, "AN-EG")), t => t.Text == "A-EG-001\nOK +2.12\nkein Fixpunkt" && !t.Stil.NurBildschirm);
    }

    [Fact]
    public void Massketten_EinheitDerAnsicht_Millimeter()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [4000, 2000, 2500], [4000, 2000, 3500]);
        Ok(Vorschlagen(p));
        Darstellung(p);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["bemassungEinheit"] = "mm" } }));
        Assert.Contains(Ketten(Grafik.Plan(p, "AN-EG")), k => Zahlen(k).SequenceEqual(["440", "1680"]));
    }

    [Fact]
    public void Massketten_Fixkanten_StrahlTrifftDieNaechsteKreuzendeKante()
    {
        List<double[]> kanten = [[100, -500, 100, 500], [5900, -500, 5900, 500], [0, 50, 6000, 50]];   // die waagrechte ist parallel zum Strahl
        Assert.Equal(1680, AussparungMassketten.Strahl(kanten, 1780, 0, -1, 0)!.Value, 1e-9);
        Assert.Equal(3680, AussparungMassketten.Strahl(kanten, 2220, 0, 1, 0)!.Value, 1e-9);
        Assert.Null(AussparungMassketten.Strahl(kanten, 1780, 1000, -1, 0));   // ausserhalb der Kanten
    }

    // ------------------------------------------------------------------ Nachtrag B4e: ganze cm, kurze Felder, Zwischenspeicher, HLKS-Hintergrund

    /// <summary>Entscheid Christian 19.10.2026: Massketten in ganzen cm, aus den ab dem Fixpunkt gerundeten Lagen (kaufmännisch) – die Kette
    /// geht auf. Fixpunkt 0, Aussparung 853 … 1073: Lagen 85.3 → 85, 107.3 → 107 cm → Felder 85 | 22. Halbe cm: 84.5 → 85, 106.5 → 107.
    /// Bezug rechts (Fixpunkt bei 1073): Lagen −107.3 → −107, −22, 0 → 85 | 22. Vier Felder zu 12.4 cm (0, 124, 248, 372, 496 mm): Lagen
    /// 12.4 → 12, 24.8 → 25, 37.2 → 37, 49.6 → 50 → Felder 12 | 13 | 12 | 13 = 50 = gerundeter Gesamtabstand (Glied für Glied 4 × 12 = 48
    /// ginge nicht auf). Zusammenfallende Punkte geben kein Feld (null).</summary>
    [Fact]
    public void Massketten_GanzeCm_AusGerundetenLagen_KetteGehtAuf()
    {
        Assert.Equal(new double?[] { 850, 220 }, AussparungMassketten.GerundeteFelder([0, 853, 1073], 0));
        Assert.Equal(new double?[] { 850, 220 }, AussparungMassketten.GerundeteFelder([0, 845, 1065], 0));
        Assert.Equal(new double?[] { 850, 220 }, AussparungMassketten.GerundeteFelder([0, 853, 1073], 1073));
        var f = AussparungMassketten.GerundeteFelder([0, 124, 248, 372, 496], 0);
        Assert.Equal(new double?[] { 120, 130, 120, 130 }, f);
        Assert.Equal(500, f.Sum()!.Value);
        Assert.Equal(new double?[] { null, 100 }, AussparungMassketten.GerundeteFelder([0, 0, 100], 0));
    }

    /// <summary>Kurze Felder (Nachtrag B4e): Zahl breiter als das Feld (Breite + Luft &gt; Feldlänge) → aussen. Kette 0 | 20 | 40 | 400 | 410
    /// mit Breite 30 je Zahl, Luft 10: Felder 20 (kurz), 20 (kurz), 360 (innen, Mitte 220), 10 (kurz). Das erste kurze Feld (Index 1) liegt
    /// näher am Anfang → vorne bei 0 − 10 − 15 = −25; das zweite abwechselnd hinten bei 410 + 10 + 15 = 435; das dritte wieder vorne,
    /// nach aussen gereiht: 0 − 10 − 40 − 15 = −65. Belegt: vorne 2 × 40 = 80, hinten 40.</summary>
    [Fact]
    public void Massketten_KurzeFelderAussen_Abwechselnd()
    {
        var lagen = AussparungMassketten.Anordnen([0, 20, 40, 400, 410], ["a", "b", "c", "d"], _ => 30, 10, out var vorne, out var hinten);
        Assert.Equal(new[] { ("c", 220.0, false), ("a", -25.0, true), ("b", 435.0, true), ("d", -65.0, true) },
            lagen.Select(l => (l.Text, l.S, l.Aussen)));
        Assert.Equal((80.0, 40.0), (vorne, hinten));
        // Gleichstand der Seiten (einziges Feld): hinten bei 10 + 10 + 15 = 35
        var eins = AussparungMassketten.Anordnen([0, 10], ["x"], _ => 30, 10, out var v1, out var h1);
        Assert.Equal((35.0, true), (eins[0].S, eins[0].Aussen));
        Assert.Equal((0.0, 40.0), (v1, h1));
    }

    /// <summary>Im Plan: Deckenaussparung 440 × 240 um (340, 2000), linke Kante 120, 20 mm vor der Innenfläche von W-4 (x 100, Beton) →
    /// Kette 100 | 120 | 560 = «2» | «44». «2» (20 mm = 0.4 mm Papier bei 1:50; Zahl 1 × 0.55 × 2 mm = 1.1 mm + 1 mm Luft) passt nicht ins
    /// Feld → aussen vor dem Anfang: Mitte x = 100 − 50 − 27.5 = 22.5, Grundlinie 0.8 mm (40) über der Masslinie y 1880 − 300 = 1580 →
    /// 1620; die Masslinie läuft unter der Zahl bis x = 100 − 105 = −5 weiter.</summary>
    [Fact]
    public void Massketten_KurzesFeldImPlan_ZahlAussen()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p);
        Decke(p);
        Kanal(p, "KA-7", 400, 200, [340, 2000, 2500], [340, 2000, 3500]);
        Ok(Vorschlagen(p));
        Darstellung(p);
        var kette = Assert.Single(Ketten(Grafik.Plan(p, "AN-EG")), k => Zahlen(k).Contains("2"));
        Assert.Equal(["44", "2"], Zahlen(kette));   // innen zuerst, dann aussen
        var zwei = kette.Primitive.Single(x => x.Art == "text" && x.Text == "2");
        Nahe([22.5, 1620], zwei.Punkte[0]);
        Assert.Contains(kette.Primitive, x => x.Art == "linie" && Math.Abs(x.Punkte[0][0] - (-5)) < 1e-6 && Math.Abs(x.Punkte[1][0] - 100) < 1e-6
                                              && Math.Abs(x.Punkte[0][1] - 1580) < 1e-6);
    }

    /// <summary>Zwischenspeicher der Fixpunkte (Nachtrag B4e): der Schlüssel hängt nur an der Architektur – eine Leitungsänderung rechnet
    /// die Fixpunkte nicht neu, eine Wandänderung schon.</summary>
    [Fact]
    public void Fixpunkte_Zwischenspeicher_LeitungsaenderungRechnetNichtNeu()
    {
        var p = Wandprojekt();
        AussparungGrafikTests.Beton(p);
        Kanal(p, "KA-1", 400, 200, [2000, -800, 2000], [2000, 800, 2000]);
        Ok(Vorschlagen(p));
        Darstellung(p);
        var stand = AussparungMassketten.ArchitekturStand(p.Modell, p.Katalog);
        Grafik.Plan(p, "AN-EG");
        long n = AussparungMassketten.BerechnungenVon(p);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KA-1", ["eigenschaften"] = new JsonObject { ["breite"] = 300 } }));
        Ok(Vorschlagen(p));                                                       // Aussparung nachgeführt
        Assert.Equal(stand, AussparungMassketten.ArchitekturStand(p.Modell, p.Katalog));
        Grafik.Plan(p, "AN-EG");
        Assert.Equal(n, AussparungMassketten.BerechnungenVon(p));                       // aus dem Zwischenspeicher
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-2", ["eigenschaften"] = new JsonObject { ["material"] = "leichtbau" } }));
        Assert.NotEqual(stand, AussparungMassketten.ArchitekturStand(p.Modell, p.Katalog));
        Grafik.Plan(p, "AN-EG");
        Assert.True(AussparungMassketten.BerechnungenVon(p) > n);                       // neu gerechnet
    }

    /// <summary>Nachtrag B4e: HLKS-Produkte im Hintergrund einer IFC-Unterlage (synthetische HLKS-IFC, Rohre bei z 500, nur Räume
    /// übernommen) erscheinen im gewöhnlichen Grundriss (Schnitthöhe 500) als graue Schnittlinien, im Aussparungsplan nicht.</summary>
    [Fact]
    public void Aussparungsplan_HlksImIfcHintergrund_NichtGezeichnet()
    {
        var ordner = IfcHlksTestdatei.Ordner();
        var datei = IfcHlksTestdatei.Schreiben("IFC4", Path.Combine(ordner, "hlks.ifc"), new IfcHlksSpec());
        var p = IfcHilfe.Leer();
        try
        {
            Ok(IfcHilfe.Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("raeume") }));   // HLKS bleibt Hintergrund
            var eg = p.Modell.DerKlasse("Geschoss").OrderBy(g => Math.Abs(g.Zahl("kote") ?? 0)).First();
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-H", ["geschoss"] = eg.Kennzeichen,
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 500 - (eg.Zahl("kote") ?? 0), ["schnitthoeheNachSia"] = false },
            }));
            var ul = Assert.Single(p.Modell.DerKlasse("Unterlage"));
            Assert.Contains(Grafik.Plan(p, "AN-H").Teile, t => t.Id == ul.Id && t.Primitive.Count > 0);
            Darstellung(p, "AN-H");
            Assert.DoesNotContain(Grafik.Plan(p, "AN-H").Teile, t => t.Id == ul.Id && t.Primitive.Count > 0);
        }
        finally
        {
            try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (HlksFehler) { }
            try { Directory.Delete(ordner, true); } catch (IOException) { }
        }
    }

    // ------------------------------------------------------------------ IFC: Kurzzeichen im Pset, Geschoss unter der Decke im OG

    /// <summary>D-2 = Decke über dem OG (Oberkante 6000, Dicke 250: z 5750 … 6000). Kanal senkrecht bei (2000, 2000) → Aussparung
    /// 440 × 240, UK 5750, OK 6000; Geschoss = das unter D-2: 5749 im Band des OG (2999 … 6000) → OG. Über OK Geschoss (3000): UK 2750,
    /// OK 3000. Kurzzeichen «L DD» (Zuluft). Dazu RO-1 in W-1 (EG) «S WD» – die Datei führt EG und OG.</summary>
    [Fact]
    public void Ifc_PsetMitKurzzeichen_DeckenaussparungImGeschossDarunter()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-2", ["geschoss"] = "OG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 6000], [6000, 0, 6000], [6000, 4000, 6000], [0, 4000, 6000]), ["dicke"] = 250 },
        }));
        Kanal(p, "KA-9", 400, 200, [2000, 2000, 5500], [2000, 2000, 6500]);
        RohrDurchW1(p, "RO-1", 1500);
        Ok(Vorschlagen(p));
        var ka = VonLeitung(p, "KA-9");
        Assert.Equal(Hole(p, "OG").Id, ka.Geschoss);
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b4d_{Guid.NewGuid().ToString("N")[..8]}");
        try
        {
            var pfad = Path.Combine(ordner, "a.ifc");
            var b = (JsonObject)Tuer.Abfrage(p, "aussparungen_ifc", new JsonObject { ["datei"] = pfad, ["status"] = "vorgeschlagen", ["koordinaten"] = "projekt" });
            Assert.Equal("bestanden", b["rundreise"]!["ergebnis"]!.GetValue<string>());
            using var d = IfcDatei.Oeffnen(pfad);
            Assert.Equal(["EG", "OG"], d.Geschosse().Select(x => x.Name));
            Dictionary<string, IfcWert> Eig(Element a) => d.Eigenschaften(d.Modell.Instances.OfType<IIfcBuildingElementProxy>()
                .Single(x => x.GlobalId.ToString() == AussparungIfcExport.AussparungGlobalId(p.Kopf.Id, a.Id)));
            var e1 = Eig(ka);
            Assert.Equal("L DD", e1[$"{AussparungIfcExport.PsetAussparung}.Kurzzeichen"].Inhalt!.ToString());
            Assert.Equal("OG", e1[$"{AussparungIfcExport.PsetAussparung}.Geschoss"].Inhalt!.ToString());
            Assert.Equal(2750, d.LaengeMm(e1[$"{AussparungIfcExport.PsetAussparung}.KoteUKGeschoss"])!.Value, 0.1);
            Assert.Equal(3000, d.LaengeMm(e1[$"{AussparungIfcExport.PsetAussparung}.KoteOKGeschoss"])!.Value, 0.1);
            Assert.Equal("S WD", Eig(VonLeitung(p, "RO-1"))[$"{AussparungIfcExport.PsetAussparung}.Kurzzeichen"].Inhalt!.ToString());
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }
}
