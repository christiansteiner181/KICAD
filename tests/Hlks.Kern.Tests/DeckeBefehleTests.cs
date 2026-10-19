using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>decke_erzeugen (Modul 1c, Auftrag B3): Umriss aus den Wänden (Masse von Hand), Höhen und Geschoss je Art,
/// Ablehnung doppelt, Trockenlauf, Rückgängig, Darstellung in Grundriss, Schnitt und 3D, Nachführung «folgt nicht»,
/// Beispielprojekt «MFH Musterweg».</summary>
public class DeckeBefehleTests
{
    // ---------------------------------------------------------------- Hilfen

    static BefehlsErgebnis Decke(Projekt p, string art = "decke", double dicke = 200, string geschoss = "EG", JsonObject? mehr = null, bool trocken = false)
    {
        var par = new JsonObject { ["geschoss"] = geschoss, ["art"] = art, ["dicke"] = dicke };
        foreach (var (k, v) in mehr ?? []) par[k] = v?.DeepClone();
        return p.Ausfuehren("decke_erzeugen", par, trocken);
    }

    static List<Element> Decken(Projekt p) =>
        p.Modell.Elemente.Where(e => e.Klasse is "Decke" or "Dach").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();

    static double Flaeche(Element e) => Math.Abs(Polygone.Flaecheninhalt(e.Geometrie!.Punkte));

    static void Geschoss(Projekt p, string kz, double kote, double hoehe) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = "GEB",
            ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe },
        }));

    static void Waende(Projekt p, string geschoss, params double[][] punkte) =>
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = geschoss, ["punkte"] = Pts(punkte), ["dicke"] = 200, ["geschlossen"] = true }));

    static void Raum(Projekt p, double x0, double y0, double x1, double y1) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]) },
        }));

    static void Ansicht(Projekt p, string kz, string? geschoss, JsonObject eig, JsonObject? geometrie = null)
    {
        var o = new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = eig };
        if (geschoss is not null) o["geschoss"] = geschoss;
        if (geometrie is not null) o["geometrie"] = geometrie;
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    static bool HatEcke(Element d, double x, double y) => d.Geometrie!.Punkte.Any(q => Math.Abs(q[0] - x) < 1e-3 && Math.Abs(q[1] - y) < 1e-3);

    const string FolgtNicht = "folgt den Wänden nicht; neu erzeugen";

    // ---------------------------------------------------------------- Umriss

    [Fact]
    public void Rechteck_AussenkonturDerWaende()
    {
        var p = Rechteck();                                             // Achsen 6000 × 4000, Dicke 200, EG Kote 0, Höhe 3000
        var e = Ok(Decke(p));
        var d = Assert.Single(Decken(p));
        Assert.Equal("D-1", d.Kennzeichen);
        Assert.Equal("Decke", d.Klasse);
        Assert.Equal("decke", d.Text("art"));
        Assert.Equal("polygon", d.Geometrie!.Art);
        // Hand: Aussenkontur = Achsen + halbe Dicke ringsum = −100 … 6100 × −100 … 4100 = 6200 × 4200 = 26.04 m²
        Assert.Equal(4, d.Geometrie.Punkte.Count);
        Nahe([-100, -100, 6100, 4100], Box(d.Geometrie.Punkte));
        Assert.Equal(6200.0 * 4200, Flaeche(d), 3);
        Assert.True(Polygone.Flaecheninhalt(d.Geometrie.Punkte) > 0);   // Gegenuhrzeigersinn
        // kein Geschoss darüber: Oberkante = Kote 0 + Geschosshöhe 3000, Dicke nach unten
        Assert.All(d.Geometrie.Punkte, q => Assert.Equal(3000, q[2]));
        Assert.Equal(200, d.Geometrie.Dicke);
        Assert.Equal(Hole(p, "EG").Id, d.Geschoss);
        Assert.Equal(Hole(p, "EG").Id, d.Bezuege[DeckeErzeugen.UmrissAus]);
        Assert.Equal("decke_erzeugen", d.Angelegt!.Befehl);
        Assert.Contains(e.Hinweise, h => h.Contains("D-1 (decke) in EG") && h.Contains("26.04 m²") && h.Contains("Oberkante 3000 mm") && h.Contains("Unterkante 2800 mm"));
        Assert.Contains(e.Hinweise, h => h.Contains("Umriss aus 4 Wänden und 0 Räumen von EG"));
        Assert.Contains(e.Hinweise, h => h.Contains("keine Räume"));   // ohne Räume ist ein Hof nicht erkennbar
    }

    [Fact]
    public void LForm_SechsEckenAussen()
    {
        var p = Neu();
        Waende(p, "EG", [0, 0], [8000, 0], [8000, 3000], [3000, 3000], [3000, 6000], [0, 6000]);
        Ok(Decke(p));
        var d = Assert.Single(Decken(p));
        // Hand: −100 … 8100 × −100 … 3100 (8200 × 3200 = 26.24 m²) + −100 … 3100 × 3100 … 6100 (3200 × 3000 = 9.60 m²) = 35.84 m²
        Assert.Equal(35.84e6, Flaeche(d), 1);
        Assert.Equal(6, d.Geometrie!.Punkte.Count);
        foreach (var (x, y) in new[] { (-100.0, -100.0), (8100.0, -100.0), (8100.0, 3100.0), (3100.0, 3100.0), (3100.0, 6100.0), (-100.0, 6100.0) })
            Assert.True(HatEcke(d, x, y), $"Ecke ({x}, {y}) fehlt");
    }

    [Fact]
    public void ZweiGebaeudeteile_JeEineDeckeInEinemPaket()
    {
        var p = Neu();
        Waende(p, "EG", [0, 0], [6000, 0], [6000, 4000], [0, 4000]);
        Waende(p, "EG", [10000, 0], [14000, 0], [14000, 3000], [10000, 3000]);
        var e = Ok(Decke(p, mehr: new JsonObject { ["name"] = "Decke über EG" }));
        Assert.Equal(2, e.Aenderungen.Count(a => a.Art == "angelegt"));
        var d = Decken(p);
        Assert.Equal(2, d.Count);
        // grössere zuerst: 6200 × 4200 = 26.04 m², dann 4200 × 3200 = 13.44 m²
        Nahe([-100, -100, 6100, 4100], Box(d[0].Geometrie!.Punkte));
        Assert.Equal(26.04e6, Flaeche(d[0]), 1);
        Nahe([9900, -100, 14100, 3100], Box(d[1].Geometrie!.Punkte));
        Assert.Equal(13.44e6, Flaeche(d[1]), 1);
        Assert.Equal("Decke über EG (Teil 1)", d[0].Name);
        Assert.Equal("Decke über EG (Teil 2)", d[1].Name);
        Ok(p.Rueckgaengig());                                           // ein Paket: beide weg
        Assert.Empty(Decken(p));
    }

    [Fact]
    public void Hof_MitRaeumen_GefuelltMitHinweis()
    {
        var p = Neu();
        Waende(p, "EG", [0, 0], [12000, 0], [12000, 12000], [0, 12000]);
        Waende(p, "EG", [4000, 4000], [8000, 4000], [8000, 8000], [4000, 8000]);   // Hofwände
        Raum(p, 0, 0, 12000, 4000);
        Raum(p, 0, 8000, 12000, 12000);
        Raum(p, 0, 4000, 4000, 8000);
        Raum(p, 8000, 4000, 12000, 8000);
        var e = Ok(Decke(p));
        var d = Assert.Single(Decken(p));
        // Hand: aussen −100 … 12100 → 12200² = 148.84 m², der Hof ist gefüllt (Decke ohne Loch)
        Assert.Equal(12200.0 * 12200, Flaeche(d), 1);
        Assert.Equal(4, d.Geometrie!.Punkte.Count);
        // Hof innen 4100 … 7900 → 3800² = 14.44 m²
        Assert.Contains(e.Hinweise, h => h.Contains("Fläche ohne Raum von 14.44 m²") && h.Contains("Hof") && h.Contains("umriss"));
        Assert.Contains(e.Hinweise, h => h.Contains("Umriss aus 8 Wänden und 4 Räumen von EG"));
    }

    [Fact]
    public void FreistehendeWand_Uebergangen()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([10000, 0], [15000, 0]), ["dicke"] = 200 }));   // W-5
        var e = Ok(Decke(p));
        Nahe([-100, -100, 6100, 4100], Box(Assert.Single(Decken(p)).Geometrie!.Punkte));
        Assert.Contains(e.Hinweise, h => h.Contains("übergangen") && h.Contains("W-5"));
    }

    [Fact]
    public void Umriss_Vorgegeben_ErsetztKontur()
    {
        var p = Neu();                                                  // ohne Wände
        var e = Ok(Decke(p, "boden", 300, mehr: new JsonObject
        {
            ["umriss"] = Pts([0, 0, 999], [0, 2000, 999], [5000, 2000, 999], [5000, 0, 999]),   // im Uhrzeigersinn, z wird nicht beachtet
            ["name"] = "Bodenplatte",
        }));
        var d = Assert.Single(Decken(p));
        Assert.Equal("boden", d.Text("art"));
        Assert.Equal("Bodenplatte", d.Name);
        Assert.Equal(10e6, Flaeche(d), 3);
        Assert.True(Polygone.Flaecheninhalt(d.Geometrie!.Punkte) > 0);
        Assert.All(d.Geometrie.Punkte, q => Assert.Equal(0, q[2]));     // boden: Kote EG
        Assert.False(d.Bezuege.ContainsKey(DeckeErzeugen.UmrissAus));
        Assert.Contains(e.Hinweise, h => h.Contains("folgt den Wänden nicht"));
    }

    // ---------------------------------------------------------------- Höhen und Geschoss je Art

    [Fact]
    public void Hoehen_JeArt_GeschossDerNeuenDecke()
    {
        var p = Rechteck();                                             // EG: Kote 0, Höhe 3000
        Geschoss(p, "OG", 3100, 2800);                                  // Kote bewusst ≠ 0 + 3000
        Waende(p, "OG", [0, 0], [6000, 0], [6000, 4000], [0, 4000]);

        Ok(Decke(p, "boden", 250));                                     // boden = Kote EG, im EG
        var boden = Hole(p, "D-1");
        Assert.Equal("boden", boden.Text("art"));
        Assert.Equal(Hole(p, "EG").Id, boden.Geschoss);
        Assert.All(boden.Geometrie!.Punkte, q => Assert.Equal(0, q[2]));

        var e = Ok(Decke(p, "decke", 220));                             // decke = Kote des Geschosses darüber, gehört zu OG
        var decke = Hole(p, "D-2");
        Assert.Equal(Hole(p, "OG").Id, decke.Geschoss);
        Assert.Equal(Hole(p, "EG").Id, decke.Bezuege[DeckeErzeugen.UmrissAus]);
        Assert.All(decke.Geometrie!.Punkte, q => Assert.Equal(3100, q[2]));
        Assert.Contains(e.Hinweise, h => h.Contains("= 3000 mm") && h.Contains("3100 mm"));   // Kote + Höhe ≠ Kote darüber
        Assert.Contains(e.Hinweise, h => h.Contains("D-2 (decke) in OG") && h.Contains("Kote von OG"));

        Ok(Decke(p, "dach", 300, "OG"));                                // dach = Kote + Geschosshöhe, im gewählten Geschoss
        var dach = Hole(p, "DA-1");
        Assert.Equal("Dach", dach.Klasse);
        Assert.Null(dach.Text("art"));
        Assert.Equal(Hole(p, "OG").Id, dach.Geschoss);
        Assert.All(dach.Geometrie!.Punkte, q => Assert.Equal(5900, q[2]));

        // decke über dem obersten Geschoss: Kote + Höhe; liegt auf der Höhe des Dachs → andere Art, nur Hinweis
        var oben = Ok(Decke(p, "decke", 200, "OG"));
        var d3 = Hole(p, "D-3");
        Assert.Equal(Hole(p, "OG").Id, d3.Geschoss);
        Assert.All(d3.Geometrie!.Punkte, q => Assert.Equal(5900, q[2]));
        Assert.Contains(oben.Hinweise, h => h.Contains("Auf gleicher Höhe liegt schon DA-1") && h.Contains("26.04 m²"));

        // dach auf einem Geschoss mit einem darüber: Hinweis (Terrasse?) und Überschneidung mit D-2 (2880 … 3100)
        var dachEg = Ok(Decke(p, "dach", 200, trocken: true));
        Assert.Contains(dachEg.Hinweise, h => h.Contains("Über EG liegt noch OG"));
        Assert.Contains(dachEg.Hinweise, h => h.Contains("Auf gleicher Höhe liegt schon D-2"));

        // Grundriss: die Decke über EG erscheint im Grundriss von OG, wenn die Schnitthöhe sie schneidet
        Ansicht(p, "AN-OG", "OG", new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = -100 });
        var g = Grafik.Plan(p, "AN-OG");
        Assert.Contains(g.Teile, t => t.Id == decke.Id && t.Ebene == "decke");
        Assert.DoesNotContain(g.Teile, t => t.Id == boden.Id);         // Boden gehört zu EG
    }

    // ---------------------------------------------------------------- Doppelt, Fehler

    [Fact]
    public void Doppelt_GleicheArtUndHoehe_Abgelehnt()
    {
        var p = Rechteck();
        Ok(Decke(p));
        var nein = Nein(Decke(p));
        Assert.Contains("D-1", nein.Fehler);
        Assert.Contains("100 %", nein.Fehler);
        Assert.Contains("element_loeschen D-1", nein.Vorschlag);
        Assert.Contains("umriss", nein.Vorschlag);
        Nein(Decke(p, dicke: 300));                                     // andere Dicke, Platten überschneiden sich in der Höhe
        Assert.Single(Decken(p));
        Ok(Decke(p, "boden"));                                          // andere Art und Höhe: geht
        // Teil überdeckt bis 50 %: Hinweis, kein Fehler – neu 6200 × 4200, davon 3000 × 4200 = 12.60 m² unter D-1 (48 %)
        var halb = Ok(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([3100, -100], [9300, -100], [9300, 4100], [3100, 4100]) }));
        Assert.Contains(halb.Hinweise, h => h.Contains("überschneidet D-1 (decke) auf 12.60 m²"));
        // nach dem Löschen geht es wieder
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "D-1" }));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "D-3" }));
        Ok(Decke(p));
    }

    [Fact]
    public void Fehler_MitVorschlag()
    {
        Nein(Decke(Neu()));                                             // weder Wände noch Räume
        var p = Rechteck();
        var ohneDicke = Nein(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "decke" }));
        Assert.Contains("dicke", ohneDicke.Fehler);
        Nein(Decke(p, dicke: 0));
        Nein(Decke(p, "keller"));
        Nein(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["dicke"] = 200 }));   // art fehlt
        Nein(Decke(p, geschoss: "GIBTS-NICHT"));
        Nein(Decke(p, geschoss: "W-1"));                                // kein Geschoss
        Nein(Decke(p, mehr: new JsonObject { ["aufbau"] = "W-1" }));    // kein Aufbau
        Nein(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([0, 0], [1000, 1000], [1000, 0], [0, 1000]) }));   // Achterschleife
        Nein(Decke(p, mehr: new JsonObject { ["umriss"] = Pts([0, 0], [1000, 0]) }));
        Assert.Empty(Decken(p));

        var offen = Neu();                                              // U-förmig, nicht geschlossen
        Ok(offen.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 4000], [0, 0], [6000, 0], [6000, 4000]), ["dicke"] = 200 }));
        var nein = Nein(Decke(offen));
        Assert.Contains("schliessen keine Fläche ein", nein.Fehler);
        Assert.Contains("geschlossen", nein.Vorschlag);
    }

    [Fact]
    public void Aufbau_WirdAlsBezugUebernommen()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Aufbau", ["kennzeichen"] = "AUF-D", ["eigenschaften"] = new JsonObject { ["art"] = "decke" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Aufbau", ["kennzeichen"] = "AUF-W", ["eigenschaften"] = new JsonObject { ["art"] = "wand" } }));
        var e = Ok(Decke(p, mehr: new JsonObject { ["aufbau"] = "AUF-D" }));
        Assert.Equal(Hole(p, "AUF-D").Id, Hole(p, "D-1").Bezuege["aufbau"]);
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("bestimmt"));
        var w = Ok(Decke(p, "boden", mehr: new JsonObject { ["aufbau"] = "AUF-W" }));
        Assert.Equal(Hole(p, "AUF-W").Id, Hole(p, "D-2").Bezuege["aufbau"]);
        Assert.Contains(w.Hinweise, h => h.Contains("AUF-W") && h.Contains("«wand»"));
    }

    // ---------------------------------------------------------------- Trockenlauf, Rückgängig

    [Fact]
    public void Trockenlauf_ZeigtFlaecheUndHoehe_AendertNichts()
    {
        var p = Rechteck();
        var stand = p.Modell.Pruefsumme();
        int verlauf = p.Verlauf.Count;
        var e = Ok(Decke(p, trocken: true));
        Assert.True(e.Trockenlauf);
        Assert.Null(e.PaketId);
        Assert.Single(e.Aenderungen, a => a.Art == "angelegt" && a.Kennzeichen == "D-1");
        Assert.Contains(e.Hinweise, h => h.Contains("26.04 m²") && h.Contains("Oberkante 3000 mm"));
        Assert.Equal(stand, p.Modell.Pruefsumme());
        Assert.Equal(verlauf, p.Verlauf.Count);
        Assert.Empty(Decken(p));
    }

    [Fact]
    public void Rueckgaengig_StelltStandHer_WiederholenErzeugtWieder()
    {
        var p = Rechteck();
        var vorher = p.Modell.Pruefsumme();
        Ok(Decke(p));
        Assert.Single(Decken(p));
        Ok(p.Rueckgaengig());
        Assert.Empty(Decken(p));
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Ok(p.Wiederholen());
        Nahe([-100, -100, 6100, 4100], Box(Assert.Single(Decken(p)).Geometrie!.Punkte));
    }

    // ---------------------------------------------------------------- Darstellung

    [Fact]
    public void Schnitt_ZeigtDieDecke()
    {
        var p = Rechteck();
        Ok(Decke(p));
        // Linie x = 3000 von Süd nach Nord: u = y + 1000, v = z
        Ansicht(p, "S-1", null, new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            new JsonObject { ["art"] = "linie", ["punkte"] = Pts([3000, -1000], [3000, 5000]) });
        var g = Grafik.Plan(p, "S-1");
        var t = g.Teile.Single(x => x.Id == Hole(p, "D-1").Id);
        Assert.Equal("decke", t.Ebene);
        var f = Assert.Single(t.Primitive, x => x.Art == "polygon");
        Nahe([900, 2800, 5100, 3000], Box(f.Punkte));                    // y −100 … 4100, z 2800 … 3000
        Assert.Equal(4200.0 * 200, PolyFlaeche(f), 1);
        Assert.Equal(Stifte.Dick, f.Stil.Stift);
        Assert.Equal("schnitt", f.Stil.Fuellung);
    }

    [Fact]
    public void Grundriss_NurGeschnitten_Und3D()
    {
        var p = Rechteck(schnitthoehe: 2900);                           // AN-EG schneidet die Decke (2800 … 3000)
        Ok(Decke(p));
        var d = Hole(p, "D-1");
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "D-1");
        Assert.Equal("decke", t.Ebene);
        Nahe([-100, -100, 6100, 4100], Box(Assert.Single(t.Primitive).Punkte));
        // übliche Schnitthöhe 1000: die Decke liegt darüber und erscheint nicht (Darstellung «Decke nur geschnitten»)
        Grundriss(p, "AN-EG-1000", 1000);
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG-1000").Teile, x => x.Id == d.Id);
        // 3D: Prisma 2800 … 3000 über dem Umriss
        var k = Assert.Single(Grafik.Koerper(p, [d.Id]));
        Assert.Equal("decke", k.Ebene);
        var teil = Assert.Single(k.Teile);
        Assert.Equal("prisma", teil.Art);
        Assert.Equal(2800, teil.Unten);
        Assert.Equal(3000, teil.Oben);
        Nahe([-100, -100, 6100, 4100], Box(teil.Grundriss!));
    }

    // ---------------------------------------------------------------- Nachführung

    [Fact]
    public void Nachfuehrung_KonturGeaendert_HinweisDeckeBleibt()
    {
        var p = Rechteck();
        Ok(Decke(p));
        var stand = Hole(p, "D-1").Geometrie!.Punkte.Select(q => (double[])q.Clone()).ToList();

        // Material und Höhe einer Aussenwand ändern die Kontur nicht: still
        var mat = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-1", ["eigenschaften"] = new JsonObject { ["material"] = "beton" } }));
        Assert.DoesNotContain(mat.Hinweise, h => h.Contains(FolgtNicht));
        var tuer = Ok(Tuer(p));
        Assert.DoesNotContain(tuer.Hinweise, h => h.Contains(FolgtNicht));

        // Wände verschoben: Hinweis, die Decke selbst bleibt, wie sie ist
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2", "W-3", "W-4"), ["dx"] = 1000 }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Decke D-1 " + FolgtNicht) && h.Contains("element_loeschen D-1"));
        Assert.DoesNotContain(e.Aenderungen, a => a.Kennzeichen == "D-1");
        Assert.Equal(Json.Text(stand), Json.Text(Hole(p, "D-1").Geometrie!.Punkte));
        Ok(p.Rueckgaengig());

        // Wände und Decke zusammen verschoben: die Decke passt weiter
        var mit = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2", "W-3", "W-4", "D-1"), ["dx"] = 1000 }));
        Assert.DoesNotContain(mit.Hinweise, h => h.Contains(FolgtNicht));
        Ok(p.Rueckgaengig());

        // Aussenwand gelöscht: Hinweis
        var weg = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-3" }));
        Assert.Contains(weg.Hinweise, h => h.StartsWith("Decke D-1 " + FolgtNicht));
    }

    [Fact]
    public void Nachfuehrung_DachUndVorgegebenerUmriss()
    {
        var p = Rechteck();
        Ok(Decke(p, "dach"));                                           // DA-1 aus den Wänden
        Ok(Decke(p, "boden", mehr: new JsonObject { ["umriss"] = Pts([-100, -100], [6100, -100], [6100, 4100], [-100, 4100]) }));   // D-1 vorgegeben
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-2"), ["dx"] = 500 }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Dach DA-1 " + FolgtNicht));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("D-1 " + FolgtNicht));   // vorgegebener Umriss folgt ohnehin nicht
    }

    // ---------------------------------------------------------------- Beispielprojekt

    [Fact]
    public void Beispielprojekt_AlleGeschosse_FlaecheWieAussenkontur()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        // Hand: Aussenwände 12000 × 10000 (Achsen), Dicke 350, Ecken geschlossen → Aussenkontur −175 … 12175 × −175 … 10175
        // = 12350 × 10350 = 127.8225 m². Die Räume liegen auf den Achsen, also innerhalb: keine Fläche ohne Raum.
        const double kontur = 12350.0 * 10350;
        var faelle = new (string Geschoss, string Art, double Dicke, string Kz, string Ziel, double Ok)[]
        {
            ("UG", "boden", 300, "D-1", "UG", -2700),                   // unterstes Geschoss: Boden auf Kote UG
            ("UG", "decke", 250, "D-2", "EG", 0),                       // Decke über UG = Boden EG
            ("EG", "decke", 250, "D-3", "1.OG", 2900),
            ("1.OG", "dach", 300, "DA-1", "1.OG", 5800),                // oben: Kote 2900 + Geschosshöhe 2900
        };
        foreach (var f in faelle)
        {
            var e = Ok(Decke(p, f.Art, f.Dicke, f.Geschoss));
            var d = Hole(p, f.Kz);
            Assert.Equal(Hole(p, f.Ziel).Id, d.Geschoss);
            Assert.All(d.Geometrie!.Punkte, q => Assert.Equal(f.Ok, q[2]));
            Assert.Equal(f.Dicke, d.Geometrie.Dicke);
            Assert.Equal(kontur, Flaeche(d), 1);
            Nahe([-175, -175, 12175, 10175], Box(d.Geometrie.Punkte));
            Assert.Contains(e.Hinweise, h => h.Contains($"{f.Kz} ({f.Art}) in {f.Ziel}") && h.Contains("127.82 m²"));
            Assert.DoesNotContain(e.Hinweise, h => h.Contains("ohne Raum") || h.Contains("keine Räume"));
        }
        Assert.Equal(4, Decken(p).Count);
    }
}
