using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Befehle der Architektur und die Nachführung der Öffnungen (Trockenlauf, Rückgängig, Fehler mit Vorschlag).</summary>
public class ArchitekturBefehleTests
{
    static double[] Lage(Projekt p, string kz) => Hole(p, kz).Geometrie!.Punkte[0];
    static double[][] Achse(Projekt p, string kz) => Hole(p, kz).Geometrie!.Punkte.ToArray();

    // ---------------------------------------------------------------- wand_zeichnen

    [Fact]
    public void WandZeichnen_GeschlossenEineWandJeStueck()
    {
        var p = Neu();
        var e = Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["geschlossen"] = true, ["material"] = "backstein",
        }));
        Assert.Equal(4, e.Aenderungen.Count(a => a.Art == "angelegt" && a.Klasse == "Wand"));
        Assert.Contains(e.Hinweise, h => h.Contains("W-1, W-2, W-3, W-4"));
        var w1 = Hole(p, "W-1");
        Nahe([0, 0, 0], w1.Geometrie!.Punkte[0]);
        Nahe([6000, 0, 0], w1.Geometrie.Punkte[1]);
        Assert.Equal(200, w1.Geometrie.Dicke);
        Assert.Equal(3000, w1.Geometrie.Hoehe);              // Vorgabe Geschosshöhe
        Assert.Equal("backstein", w1.Text("material"));
        Assert.Equal(Hole(p, "EG").Id, w1.Geschoss);
        Nahe([0, 4000, 0], Achse(p, "W-4")[0]);               // Schlussstück zurück zum Anfang
        Nahe([0, 0, 0], Achse(p, "W-4")[1]);
    }

    [Fact]
    public void WandZeichnen_BezugslinieVerschiebtAchseUmHalbeDicke()
    {
        var p = Neu();
        // Punkte = linke Wandfläche; im Gegenuhrzeigersinn gezeichnet liegt links innen → Achsen 100 mm nach aussen
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["geschlossen"] = true, ["bezugslinie"] = "links",
        }));
        Nahe([-100, -100], Achse(p, "W-1")[0]);
        Nahe([6100, -100], Achse(p, "W-1")[1]);
        Nahe([6100, 4100], Achse(p, "W-2")[1]);
        var loch = Assert.Single(Assert.Single(Umriss(Grafik.Plan(p, "AN-EG"))).Loecher!);
        Nahe([0, 0, 6000, 4000], Box(loch));                  // Innenfläche = gezeichnete Linie

        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 10000], [2000, 10000], [2000, 12000]), ["dicke"] = 100, ["bezugslinie"] = "rechts",
        }));
        Nahe([0, 10050], Achse(p, "W-5")[0]);                 // rechts der Linie liegt die Wand → Achse 50 mm links
        Nahe([1950, 10050], Achse(p, "W-5")[1]);              // Ecke auf dem Schnitt der versetzten Linien
        Nahe([1950, 12000], Achse(p, "W-6")[1]);
    }

    [Fact]
    public void WandZeichnen_FehlerMitVorschlag()
    {
        var p = Neu();
        var ohneDicke = Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [1000, 0]) }));
        Assert.Contains("dicke", ohneDicke.Fehler);
        Assert.DoesNotMatch(@"\d", ohneDicke.Vorschlag!);     // keine Zahl aus dem Gedächtnis
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0]), ["dicke"] = 200 }));
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [0, 0]), ["dicke"] = 200 }));
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [1000, 0]), ["dicke"] = 200, ["bezugslinie"] = "oben" }));
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [1000, 0]), ["dicke"] = 200, ["material"] = "gold" }));
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [1000, 0]), ["dicke"] = 200, ["geschlossen"] = true }));
        Nein(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "GIBTS-NICHT", ["punkte"] = Pts([0, 0], [1000, 0]), ["dicke"] = 200 }));
        Assert.DoesNotContain(p.Modell.Elemente, e => e.Klasse == "Wand");
    }

    // ---------------------------------------------------------------- oeffnung_setzen

    [Fact]
    public void OeffnungSetzen_TuerMitAbstand_LageUndEigenschaften()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var t = Hole(p, "T-1");
        Assert.Equal("Tuer", t.Klasse);
        Assert.Equal(Hole(p, "W-1").Id, t.Bezuege["wand"]);
        Assert.Equal(Hole(p, "EG").Id, t.Geschoss);
        Assert.Equal(3000, t.Zahl("abstand"));
        Assert.Equal(900, t.Zahl("breite"));
        Assert.Equal("links", t.Text("seite"));               // Vorgabe
        Assert.Equal("anfang", t.Text("band"));
        Nahe([3000, 0, 0], t.Geometrie!.Punkte[0]);
    }

    [Fact]
    public void OeffnungSetzen_PunktWirdAufDieAchseProjiziert()
    {
        var p = Rechteck();
        var e = Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject
        {
            ["klasse"] = "Fenster", ["wand"] = "W-2", ["punkt"] = Pt(6300, 1500), ["breite"] = 1000, ["hoehe"] = 1200, ["bruestung"] = 900,
        }));
        var f = Hole(p, "F-1");
        Assert.Equal(1500, f.Zahl("abstand"));
        Nahe([6000, 1500, 0], f.Geometrie!.Punkte[0]);
        Assert.Equal(900, f.Zahl("bruestung"));
        Assert.Contains(e.Hinweise, h => h.Contains("neben der Achse"));
    }

    [Fact]
    public void OeffnungSetzen_ZuBreitUeberlappendZuHoch_AbgelehntMitVorschlag()
    {
        var p = Rechteck();
        var breit = Nein(Tuer(p, breite: 6000));
        Assert.Contains("passt nicht", breit.Fehler);
        Assert.Contains("100 … 5900", breit.Vorschlag);          // freie Länge zwischen den Querwänden
        var ecke = Nein(Tuer(p, abstand: 300));                    // ragte in die Westwand
        Assert.Contains("freie Länge", ecke.Fehler);
        Ok(Tuer(p));
        var ueber = Nein(Tuer(p, abstand: 3500));
        Assert.Contains("überschneidet T-1", ueber.Fehler);
        Assert.Contains("100 … 2550, 3450 … 5900", ueber.Vorschlag);
        Nein(Fenster(p, abstand: 1000, hoehe: 2500, bruestung: 900));   // 3400 > Wandhöhe 3000
        Assert.Null(p.Modell.Finde("T-2"));
        Assert.Null(p.Modell.Finde("F-1"));
    }

    [Fact]
    public void OeffnungSetzen_FehlendeAngabenAbgelehnt()
    {
        var p = Rechteck();
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = "W-1", ["abstand"] = 2000, ["breite"] = 1000, ["hoehe"] = 1200 }));   // Brüstung fehlt
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = "W-1", ["abstand"] = 2000, ["breite"] = 900, ["hoehe"] = 2100, ["bruestung"] = 100 }));
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = "W-1", ["breite"] = 900, ["hoehe"] = 2100 }));            // Lage fehlt
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = "W-1", ["abstand"] = 2000, ["hoehe"] = 2100 }));           // Breite fehlt
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Raum", ["wand"] = "W-1", ["abstand"] = 2000, ["breite"] = 900, ["hoehe"] = 2100 }));
        Nein(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = "EG", ["abstand"] = 2000, ["breite"] = 900, ["hoehe"] = 2100 }));
        Nein(Tuer(p, seite: "oben"));
        Assert.DoesNotContain(p.Modell.Elemente, e => e.Klasse is "Tuer" or "Fenster");
    }

    // ---------------------------------------------------------------- raum_erkennen

    [Fact]
    public void RaumErkennen_InnenGibtPolygon5800x3800()
    {
        var p = Rechteck();
        Ok(Tuer(p));                                            // Türen unterbrechen die Raumgrenze nicht
        var e = Ok(p.Ausfuehren("raum_erkennen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nummer"] = "0.01", ["nutzung"] = "Wohnen", ["name"] = "Wohnen/Essen",
        }));
        var r = Hole(p, "R-1");
        Assert.Equal("polygon", r.Geometrie!.Art);
        Assert.Equal(4, r.Geometrie.Punkte.Count);
        Nahe([100, 100, 5900, 3900], Box(r.Geometrie.Punkte));
        Assert.Equal(22.04, Math.Round(r.Geometrie.Flaeche() / 1e6, 2));
        Assert.Equal("0.01", r.Text("nummer"));
        Assert.Equal("Wohnen", r.Text("nutzung"));
        Assert.Equal("Wohnen/Essen", r.Name);
        Assert.Equal(Hole(p, "EG").Id, r.Geschoss);
        Assert.Contains(e.Hinweise, h => h.Contains("22.04 m²"));
    }

    [Fact]
    public void RaumErkennen_NichtGeschlossenInWandOderSchonBelegt_Abgelehnt()
    {
        var p = Neu();
        Nein(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }));          // keine Wände
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 4000], [0, 0], [6000, 0], [6000, 4000]), ["dicke"] = 200 }));
        var offen = Nein(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }));
        Assert.Contains("umschliessen", offen.Fehler);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([6000, 4000], [0, 4000]), ["dicke"] = 200 }));
        Nein(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 0) }));               // in der Wand
        Nein(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(9000, 2000) }));            // aussen
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }));
        Assert.Equal(22.04, Math.Round(Hole(p, "R-1").Geometrie!.Flaeche() / 1e6, 2));
        var doppelt = Nein(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(1000, 1000) }));
        Assert.Contains("R-1", doppelt.Fehler);
    }

    // ---------------------------------------------------------------- Nachführung

    [Fact]
    public void WandVerschieben_TuerFolgtMitGleichemAbstand()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dy"] = 500, ["dz"] = 50 }));
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "T-1" && a.Art == "geaendert");   // Folgeänderung im selben Paket
        Assert.Equal(3000, Hole(p, "T-1").Zahl("abstand"));
        Nahe([3000, 500, 50], Lage(p, "T-1"));                   // z = Wandunterkante
        Assert.Single(p.Verlauf.Where(x => x.Befehl == "elemente_verschieben"));

        // Wand umdrehen (Anfang ↔ Ende): abstand bleibt führend, der Punkt wandert
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([6000, 500, 50], [1000, 500, 50]), ["dicke"] = 200, ["hoehe"] = 3000 },
        }));
        Nahe([3000, 500, 50], Lage(p, "T-1"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([7000, 500, 0], [1000, 500, 0]), ["dicke"] = 200, ["hoehe"] = 3000 },
        }));
        Nahe([4000, 500, 0], Lage(p, "T-1"));
    }

    [Fact]
    public void WandLoeschen_TuerWeg_RueckgaengigStelltBeidesHer()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(Fenster(p, abstand: 1000, breite: 800));
        var vorher = p.Modell.Pruefsumme();
        int anzahl = p.Modell.Elemente.Count;
        var e = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-1" }));
        Assert.Null(p.Modell.Finde("W-1"));
        Assert.Null(p.Modell.Finde("T-1"));
        Assert.Null(p.Modell.Finde("F-1"));
        Assert.Equal(anzahl - 3, p.Modell.Elemente.Count);
        Assert.Contains(e.Aenderungen, a => a.Art == "geloescht" && a.Kennzeichen == "T-1");
        Assert.Contains(e.Hinweise, h => h.Contains("T-1 mit Wand W-1"));

        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Assert.NotNull(p.Modell.Finde("T-1"));
        Ok(p.Wiederholen());
        Assert.Null(p.Modell.Finde("T-1"));
        // Andere Elemente ohne Öffnungen löschen wie bisher
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-2" }));
    }

    [Fact]
    public void WandLoeschen_OeffnungNochVerwendet_Abgelehnt()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Textnotiz", ["eigenschaften"] = new JsonObject { ["inhalt"] = "Brandschutztür" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 500]) },
            ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG", ["an"] = "T-1" },
        }));
        var e = Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-1" }));
        Assert.Contains("T-1", e.Fehler);
        Assert.NotNull(p.Modell.Finde("W-1"));
    }

    [Fact]
    public void Nachfuehrung_WandGeloeschtLoeschtOeffnungen()
    {
        // Die Regel selbst, unabhängig vom Befehl: gelöschte Wand → ihre Öffnungen im selben Kontext löschen
        var p = Rechteck();
        Ok(Tuer(p));
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "mensch", Akteur = "T", Zeit = DateTimeOffset.Now });
        k.Loeschen(k.Arbeit.Hole("W-1"));
        Nachfuehrungen.Ausfuehren(k);
        Assert.Null(k.Arbeit.Finde("T-1"));
        Assert.Contains(k.Operationen, o => o.Art == "element.loeschen" && o.ElementVorher!.Kennzeichen == "T-1");
        Assert.NotNull(p.Modell.Finde("T-1"));                    // nur die Arbeitskopie
    }

    [Fact]
    public void Nachfuehrung_PunktOderAbstandDirektGeaendert()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        // Punkt direkt geändert → abstand aus der Projektion, Punkt auf die Achse
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "T-1", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([2000, 50, 0]) },
        }));
        Assert.Equal(2000, Hole(p, "T-1").Zahl("abstand"));
        Assert.Equal("berechnung", Hole(p, "T-1").Eigenschaft("abstand")!.Herkunft.Art);
        Nahe([2000, 0, 0], Lage(p, "T-1"));
        Assert.Contains(e.Hinweise, h => h.Contains("neben der Achse"));
        // abstand geändert → Punkt folgt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "T-1", ["eigenschaften"] = new JsonObject { ["abstand"] = 4000 } }));
        Nahe([4000, 0, 0], Lage(p, "T-1"));
        // Tür allein verschieben → wandert entlang der Wand
        Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["dx"] = -500 }));
        Assert.Equal(3500, Hole(p, "T-1").Zahl("abstand"));
        // Breite ändern, bis sie nicht mehr passt → Hinweis
        var zuBreit = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "T-1", ["eigenschaften"] = new JsonObject { ["breite"] = 5000 } }));
        Assert.Contains(zuBreit.Hinweise, h => h.Contains("passt nicht mehr"));
    }

    [Fact]
    public void Nachfuehrung_WandGekuerzt_HinweisPasstNichtMehr()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "W-1", ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0], [3000, 0]), ["dicke"] = 200, ["hoehe"] = 3000 },
        }));
        Assert.Contains(e.Hinweise, h => h.Contains("T-1 passt nicht mehr in W-1"));
        Assert.Equal(3000, Hole(p, "T-1").Zahl("abstand"));
    }

    [Fact]
    public void Nachfuehrung_OeffnungMitNurPunktErhaeltAbstand()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Fenster", ["kennzeichen"] = "F-X", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["breite"] = 1000, ["hoehe"] = 1200, ["bruestung"] = 900 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1500, 20, 0]) },
            ["bezuege"] = new JsonObject { ["wand"] = "W-1" },
        }));
        Assert.Equal(1500, Hole(p, "F-X").Zahl("abstand"));
        Nahe([1500, 0, 0], Lage(p, "F-X"));
    }

    // ---------------------------------------------------------------- Trockenlauf

    [Fact]
    public void Trockenlauf_AendertNichts()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var stand = p.Modell.Pruefsumme();
        int verlauf = p.Verlauf.Count;
        var befehle = new (string Name, JsonObject Par)[]
        {
            ("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 8000], [3000, 8000]), ["dicke"] = 150 }),
            ("oeffnung_setzen", new JsonObject { ["klasse"] = "Fenster", ["wand"] = "W-3", ["abstand"] = 2000, ["breite"] = 1000, ["hoehe"] = 1200, ["bruestung"] = 900 }),
            ("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000) }),
            ("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dy"] = 300 }),
            ("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dy"] = 8000, ["anzahl"] = 2 }),
            ("elemente_drehen", new JsonObject { ["elemente"] = new JsonArray("W-1", "W-2"), ["mitte"] = Pt(0, 0), ["winkel"] = 30 }),
            ("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["achse"] = Pts([0, -1000], [1, -1000]) }),
            ("element_loeschen", new JsonObject { ["element"] = "W-1" }),
        };
        foreach (var (name, par) in befehle)
        {
            var e = Ok(p.Ausfuehren(name, par, trockenlauf: true));
            Assert.True(e.Trockenlauf);
            Assert.NotEmpty(e.Aenderungen);
            Assert.Equal(stand, p.Modell.Pruefsumme());
        }
        Assert.Equal(verlauf, p.Verlauf.Count);
        // Verschieben im Trockenlauf meldet die Folgeänderung an der Tür
        Assert.Contains(p.Ausfuehren("elemente_verschieben", befehle[3].Par, trockenlauf: true).Aenderungen, a => a.Kennzeichen == "T-1");
    }

    // ---------------------------------------------------------------- Kopieren, Drehen, Spiegeln

    [Fact]
    public void Kopieren_WandMitTuer_NeueKennzeichenUndBezuege()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var e = Ok(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dy"] = 10000, ["anzahl"] = 2 }));
        Assert.Equal(4, e.Aenderungen.Count(a => a.Art == "angelegt"));
        foreach (var (w, t, y) in new[] { ("W-5", "T-2", 10000.0), ("W-6", "T-3", 20000.0) })
        {
            Nahe([0, y, 0], Achse(p, w)[0]);
            Nahe([6000, y, 0], Achse(p, w)[1]);
            Assert.Equal(Hole(p, w).Id, Hole(p, t).Bezuege["wand"]);
            Assert.Equal(3000, Hole(p, t).Zahl("abstand"));
            Nahe([3000, y, 0], Lage(p, t));
            Assert.NotEqual(Hole(p, "T-1").Id, Hole(p, t).Id);
        }
        Assert.Equal(Hole(p, "W-1").Id, Hole(p, "T-1").Bezuege["wand"]);   // Original unverändert
        Nein(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dy"] = 1, ["anzahl"] = 0 }));
    }

    [Fact]
    public void Kopieren_VerbindungenNurInnerhalbDerKopie()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geschoss"] = "EG", ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 1000, 0]) },
        }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "KW", ["kennzeichen"] = "RO-KW", ["geschoss"] = "EG", ["punkte"] = Pts([0, 1000, 0], [1000, 1000, 0]), ["nach"] = "WC-1.kw",
        }));
        int verbindungen = p.Modell.Verbindungen.Count;
        Ok(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("RO-KW", "WC-1"), ["dx"] = 3000 }));
        Assert.Equal(verbindungen + 1, p.Modell.Verbindungen.Count);
        var wc2 = Hole(p, "WC-2");
        Nahe([4000, 1000, 0], wc2.Geometrie!.Punkte[0]);
        var v = Assert.Single(p.Modell.VerbindungenVon(wc2.Id));
        var rohrKopie = p.Modell.Finde(v.Gegenueber(wc2.Id, "kw")!.Element)!;
        Assert.NotEqual("RO-KW", rohrKopie.Kennzeichen);
        Assert.Equal(Hole(p, "KW").Id, rohrKopie.System);
        Ok(p.Ausfuehren("elemente_kopieren", new JsonObject { ["elemente"] = new JsonArray("RO-KW"), ["dy"] = 3000 }));
        Assert.Equal(verbindungen + 1, p.Modell.Verbindungen.Count);       // Verbindung zum WC bleibt beim Original
    }

    [Fact]
    public void Drehen_WaendeMitTuer_TuerFolgtTextDrehtMit()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Textnotiz", ["kennzeichen"] = "TX-1", ["eigenschaften"] = new JsonObject { ["inhalt"] = "Notiz", ["winkel"] = 10 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 0]) }, ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG" },
        }));
        Ok(p.Ausfuehren("elemente_drehen", new JsonObject
        {
            ["elemente"] = new JsonArray("W-1", "W-2", "W-3", "W-4", "TX-1"), ["mitte"] = Pt(0, 0), ["winkel"] = 90,
        }));
        Nahe([0, 0, 0], Achse(p, "W-1")[0]);
        Nahe([0, 6000, 0], Achse(p, "W-1")[1]);
        Nahe([0, 3000, 0], Lage(p, "T-1"));
        Assert.Equal(3000, Hole(p, "T-1").Zahl("abstand"));
        Assert.Equal("links", Hole(p, "T-1").Text("seite"));
        Nahe([0, 1000], Lage(p, "TX-1"));
        Assert.Equal(100, Hole(p, "TX-1").Zahl("winkel"));
        Nahe([-4100, -100, 100, 6100], Box(Assert.Single(Umriss(Grafik.Plan(p, "AN-EG"))).Punkte));
        Nein(p.Ausfuehren("elemente_drehen", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["winkel"] = 90 }));   // Drehpunkt fehlt
    }

    [Fact]
    public void Spiegeln_WandMitTuer_SeiteKehrtUm()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(p.Ausfuehren("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["achse"] = Pts([10000, 0], [10000, 1]) }));
        Nahe([20000, 0, 0], Achse(p, "W-1")[0]);
        Nahe([14000, 0, 0], Achse(p, "W-1")[1]);
        var t = Hole(p, "T-1");
        Assert.Equal("rechts", t.Text("seite"));                 // Aufschlag bleibt im Norden, die Wand läuft jetzt nach Westen
        Assert.Equal("anfang", t.Text("band"));
        Nahe([17000, 0, 0], t.Geometrie!.Punkte[0]);
        // Blatt zeigt weiterhin nach Norden (+y)
        var blatt = Teil(Grafik.Plan(p, "AN-EG"), p, "T-1").Primitive.Single(x => x.Art == "linie");
        Assert.True(blatt.Punkte[1][1] > blatt.Punkte[0][1]);
        Nahe([17450, 100], blatt.Punkte[0]);                     // Band zum gespiegelten Wandanfang
    }

    [Fact]
    public void Spiegeln_GanzerGrundriss_TuerSchlaegtInDenGespiegeltenRaum()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(p.Ausfuehren("elemente_spiegeln", new JsonObject
        {
            ["elemente"] = new JsonArray("W-1", "W-2", "W-3", "W-4", "T-1"), ["achse"] = Pts([0, -1000], [1, -1000]),
        }));
        Nahe([0, -2000, 0], Achse(p, "W-1")[0]);
        Nahe([6000, -2000, 0], Achse(p, "W-1")[1]);
        var t = Hole(p, "T-1");
        Assert.Equal("rechts", t.Text("seite"));
        Nahe([3000, -2000, 0], t.Geometrie!.Punkte[0]);
        var blatt = Teil(Grafik.Plan(p, "AN-EG"), p, "T-1").Primitive.Single(x => x.Art == "linie");
        Assert.True(blatt.Punkte[1][1] < blatt.Punkte[0][1]);    // schlägt nach Süden in den gespiegelten Raum
    }

    [Fact]
    public void Spiegeln_TuerAllein_QuerZurWand_BandKehrtUm()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(p.Ausfuehren("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("T-1"), ["achse"] = Pts([3000, -1000], [3000, 1000]) }));
        var t = Hole(p, "T-1");
        Assert.Equal("ende", t.Text("band"));
        Assert.Equal("links", t.Text("seite"));
        Assert.Equal(3000, t.Zahl("abstand"));
    }

    [Fact]
    public void Bearbeiten_ElementeOhneGeometrie()
    {
        var p = Rechteck();
        var e = Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1", "EG"), ["dx"] = 100 }));
        Assert.Contains(e.Hinweise, h => h.Contains("EG"));
        Nein(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("EG"), ["dx"] = 100 }));
        Nein(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray(), ["dx"] = 100 }));
        Nein(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("GIBTS-NICHT"), ["dx"] = 100 }));
        Nein(p.Ausfuehren("elemente_spiegeln", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["achse"] = Pts([0, 0], [0, 0]) }));
    }

    [Fact]
    public void Register_EnthaeltArchitekturbefehleMitBeschreibungUndParametern()
    {
        var p = Neu();
        foreach (var n in new[] { "wand_zeichnen", "oeffnung_setzen", "raum_erkennen", "elemente_verschieben", "elemente_kopieren", "elemente_drehen", "elemente_spiegeln" })
        {
            var b = p.Befehle.Hole(n);
            Assert.False(string.IsNullOrWhiteSpace(b.Beschreibung));
            Assert.NotEmpty(b.Parameter);
            Assert.All(b.Parameter, x => Assert.Contains(x.Typ, new[] { "text", "zahl", "bool", "liste", "objekt" }));
        }
        var loeschen = p.Befehle.Hole("element_loeschen");
        Assert.Equal(new ElementLoeschen().Beschreibung, loeschen.Beschreibung);   // Hülle behält Beschreibung und Parameter
        Assert.Equal(new ElementLoeschen().Parameter.Select(x => x.Name), loeschen.Parameter.Select(x => x.Name));
    }
}
