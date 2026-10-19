using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>waermebruecken_vorschlagen (Modul 1c, B1): Längen von Hand gerechnet, Abgleich ohne Doppel, Trockenlauf, Rückgängig.</summary>
public class RaumgrenzenWaermebrueckenTests
{
    /// <summary>EG Kote 0, Höhe 3000; OG darüber. Wände Dicke 200, Höhe 2800 um die Achsen (0, 0) … (6200, 4200); Decke D-1
    /// über dem EG (OK 3000, 200 dick → UK 2800 = Wandoberkante). Fenster F-1 in W-1 1.2 × 1.2 m, Tür T-1 in W-2 1.0 × 2.1 m.</summary>
    static Projekt Projekt(bool mitDecke = true, bool mitOg = true)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        if (mitOg) RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", 0, 0, 6200, 4200, 200, 2800);
        RaumgrenzenHilfe.Raum(p, "EG", 3100, 2100, 2600);
        RaumgrenzenHilfe.Fenster(p, "W-1", 3100, 1200, 1200, 900);
        RaumgrenzenHilfe.Tuer(p, "W-2", 2100, 1000, 2100);
        if (mitDecke) RaumgrenzenHilfe.Platte(p, "Decke", "D-1", "EG", -100, -100, 6300, 4300, 3000, 200);
        return p;
    }

    static List<Element> Wb(Projekt p) => p.Modell.DerKlasse("Waermebruecke").OrderBy(e => e.Kennzeichen).ToList();
    static Element Von(Projekt p, string art, string bauteil) =>
        Wb(p).Single(e => e.Text("art") == art && e.Kennungen!["wb"] == $"{art}:{RaumgrenzenHilfe.E(p, bauteil).Id}");

    [Fact]
    public void Laengen_DeckenauflagerAnDerAussenseite_FensteranschlagUmfang()
    {
        var p = Projekt();
        var e = RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        var wb = Wb(p);
        Assert.Equal(6, wb.Count);
        // Deckenauflager: Aussenseite samt Verlängerung = Achse + 2 × 100 mm: 6.4 / 4.4 / 6.4 / 4.4 m, zusammen 21.6 m
        Assert.Equal(6.4, Von(p, "deckenauflager", "W-1").Zahl("laenge"));
        Assert.Equal(4.4, Von(p, "deckenauflager", "W-2").Zahl("laenge"));
        Assert.Equal(6.4, Von(p, "deckenauflager", "W-3").Zahl("laenge"));
        Assert.Equal(4.4, Von(p, "deckenauflager", "W-4").Zahl("laenge"));
        RaumgrenzenHilfe.Nahe(21.6, wb.Where(x => x.Text("art") == "deckenauflager").Sum(x => x.Zahl("laenge")!.Value), 1e-9);
        // Lage: W-1 Süd, Aussenseite y = −100, von x = −100 bis 6300, auf der Unterkante der Decke (2800)
        var d1 = Von(p, "deckenauflager", "W-1");
        Assert.Equal("polylinie", d1.Geometrie!.Art);
        ArchitekturHilfe.Nahe([-100, -100, 2800], d1.Geometrie.Punkte[0]);
        ArchitekturHilfe.Nahe([6300, -100, 2800], d1.Geometrie.Punkte[1]);
        Assert.Equal(6400, d1.Geometrie.Laenge(), 6);
        Assert.Equal(RaumgrenzenHilfe.E(p, "W-1").Id, d1.Bezuege["wand"]);
        Assert.Equal(RaumgrenzenHilfe.E(p, "D-1").Id, d1.Bezuege["decke"]);
        Assert.Equal(RaumgrenzenHilfe.E(p, "EG").Id, d1.Geschoss);
        Assert.Equal("berechnung", d1.Eigenschaft("laenge")!.Herkunft.Art);
        Assert.Null(d1.Zahl("psi"));
        // Fensteranschlag: Umfang 2 × (1.2 + 1.2) = 4.8 m; Tür 2 × (1.0 + 2.1) = 6.2 m; Linienzug um die Öffnung
        var f = Von(p, "fensteranschlag", "F-1");
        Assert.Equal(4.8, f.Zahl("laenge"));
        Assert.Equal(4800, f.Geometrie!.Laenge(), 6);
        Assert.Equal(5, f.Geometrie.Punkte.Count);
        Assert.Equal(RaumgrenzenHilfe.E(p, "F-1").Id, f.Bezuege["oeffnung"]);
        Assert.Equal(6.2, Von(p, "fensteranschlag", "T-1").Zahl("laenge"));
        Assert.Contains(e.Hinweise, h => h.Contains("ψ fehlt"));
        Assert.Contains(e.Hinweise, h => h.Contains("6 neu"));
        Assert.DoesNotContain(e.NeueBefunde, b => b.Schwere == "fehler");
    }

    [Fact]
    public void Abgleich_KeineDoppel_NachfuehrenBeiAenderung()
    {
        var p = Projekt();
        RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        var vorher = p.Modell.Pruefsumme();
        var zweiter = RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        Assert.Equal(6, Wb(p).Count);
        Assert.Equal(vorher, p.Modell.Pruefsumme());   // nichts geändert
        Assert.Contains(zweiter.Hinweise, h => h.Contains("0 neu, 0 nachgeführt, 6 unverändert"));
        // Fenster breiter → Fensteranschlag wird nachgeführt, nicht gedoppelt: 2 × (1.5 + 1.2) = 5.4 m
        RaumgrenzenHilfe.Eigenschaften(p, "F-1", new JsonObject { ["breite"] = 1500 });
        var psi = Von(p, "fensteranschlag", "F-1");
        RaumgrenzenHilfe.Eigenschaften(p, psi.Kennzeichen, new JsonObject { ["psi"] = new JsonObject { ["wert"] = 0.1, ["quelle"] = "Testwert, frei gewählt" } });
        var dritter = RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        Assert.Equal(6, Wb(p).Count);
        Assert.Contains(dritter.Hinweise, h => h.Contains("0 neu, 1 nachgeführt"));
        var f = Von(p, "fensteranschlag", "F-1");
        Assert.Equal(5.4, f.Zahl("laenge"));
        Assert.Equal(0.1, f.Zahl("psi"));   // ψ bleibt
    }

    [Fact]
    public void Trockenlauf_AendertNichts_RueckgaengigStelltHer()
    {
        var p = Projekt();
        var start = p.Modell.Pruefsumme();
        var tl = RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject(), trockenlauf: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(6, tl.Aenderungen.Count(a => a.Art == "angelegt" && a.Klasse == "Waermebruecke"));
        Assert.Equal(start, p.Modell.Pruefsumme());
        RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        Assert.Equal(6, Wb(p).Count);
        RaumgrenzenHilfe.Ok(p.Rueckgaengig());
        Assert.Empty(Wb(p));
        Assert.Equal(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void WandLoeschen_VorschlaegeGehenMit_VonHandAngelegteBlockieren()
    {
        // Entscheid Hauptlauf 18.10.2026: Vorschläge (Kennung «wb») gehen mit Wand, Öffnung oder Decke; von Hand angelegte nicht
        var p = Projekt();
        RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        var start = p.Modell.Pruefsumme();
        var e = RaumgrenzenHilfe.Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-1" }));
        Assert.Null(p.Modell.Finde("F-1"));   // Fenster mit seiner Wand
        Assert.Equal(4, Wb(p).Count);         // Deckenauflager W-1 und Fensteranschlag F-1 weg
        Assert.DoesNotContain(Wb(p), x => x.Name is "Deckenauflager W-1" or "Fensteranschlag F-1");
        Assert.Contains(e.Hinweise, h => h.Contains("2 Wärmebrücken-Vorschläge mit ihrem Bauteil gelöscht"));
        Assert.DoesNotContain(e.NeueBefunde, b => b.Code == "P08");
        RaumgrenzenHilfe.Ok(p.Rueckgaengig());
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Equal(6, Wb(p).Count);

        // Tür direkt löschen: ihr Fensteranschlag geht mit; Decke löschen: die Deckenauflager gehen mit
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "T-1" }));
        Assert.Equal(5, Wb(p).Count);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "D-1" }));
        Assert.Single(Wb(p));   // F-1

        // von Hand angelegte Wärmebrücke an W-3 (ohne Kennung «wb») blockiert das Löschen der Wand
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Waermebruecke", ["kennzeichen"] = "WB-HAND", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "ecke" }, ["bezuege"] = new JsonObject { ["wand"] = "W-3" },
        }));
        var nein = ArchitekturHilfe.Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-3" }));
        Assert.Contains("WB-HAND", nein.Fehler);
    }

    [Fact]
    public void OhneDecke_ObersteGeschossKeinDeckenauflager_SonstAufDerWandoberkante()
    {
        // nur EG, keine Decke: oberstes Geschoss → kein Deckenauflager, nur Fensteranschläge
        var p = Projekt(mitDecke: false, mitOg: false);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        Assert.Equal(2, Wb(p).Count);
        Assert.All(Wb(p), x => Assert.Equal("fensteranschlag", x.Text("art")));
        // mit Geschoss darüber, aber ohne Decke im Modell: auf der Wandoberkante (2800) mit Hinweis
        var q = Projekt(mitDecke: false, mitOg: true);
        var e = RaumgrenzenHilfe.Ok(q.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        var d = Von(q, "deckenauflager", "W-3");
        Assert.Equal(2800, d.Geometrie!.Punkte[0][2]);
        Assert.False(d.Bezuege.ContainsKey("decke"));
        Assert.Contains(e.Hinweise, h => h.Contains("keine Decke über der Wand"));
    }

    [Fact]
    public void Innenwand_UndDach_KeinVorschlag_GeschossFilter()
    {
        var p = Projekt(mitDecke: false, mitOg: false);
        // Dach auf den Wänden (Dachrand): kein Deckenauflager; Innenwand mit Tür: kein Fensteranschlag
        RaumgrenzenHilfe.Platte(p, "Dach", "DA-1", "EG", -100, -100, 6300, 4300, 3000, 200);
        RaumgrenzenHilfe.Wand(p, "EG", [3100, 100], [3100, 4100], 100);   // W-5 frei im Raum (das Raumpolygon bleibt)
        var t2 = RaumgrenzenHilfe.Tuer(p, "W-5", 2000, 900, 2100);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject { ["geschoss"] = "EG" }));
        Assert.DoesNotContain(Wb(p), x => x.Text("art") == "deckenauflager");
        Assert.DoesNotContain(Wb(p), x => x.Bezuege.GetValueOrDefault("wand") == RaumgrenzenHilfe.E(p, "W-5").Id);
        Assert.DoesNotContain(Wb(p), x => x.Bezuege.GetValueOrDefault("oeffnung") == RaumgrenzenHilfe.E(p, t2).Id);
        Assert.Equal(2, Wb(p).Count);   // F-1, T-1
        var nein = p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject { ["geschoss"] = "W-1" });
        Assert.False(nein.Angenommen);
    }
}
