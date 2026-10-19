using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B3: aussparungen_nummerieren (stabile Reihenfolge, bestehende Nummern bleiben, alle neu), aussparung_status
/// (zulässige Übergänge), Prüfungen P19/P20 und die Abfrage aussparungsliste. Werte FIKTIV (siehe AussparungHilfe).</summary>
public class AussparungPflegeTests
{
    static string? Nummer(Projekt p, string kz) => Hole(p, kz).Text("nummer");

    /// <summary>Geschoss-Regel (Hauptlauf 19.10.2026, SIA 400:2000 B.10.3 / SIA 410/2 Ziff. 2.3): die Deckenaussparung AUS-2 in D-1
    /// (z 2750 … 3000) gehört ins Geschoss UNTER der Decke: Unterkante 2750 − 1 = 2749 liegt im Band des EG (Kote 0, Höhe 3000:
    /// −1 ≤ 2749 &lt; 3000) → EG. Nummern im EG nach x, dann y: AUS-3 (1000, 0) 001, AUS-4 (3000, 0) 002, AUS-2 (3000, 2000) 003,
    /// AUS-1 (5000, 0) 004.</summary>
    [Fact]
    public void Nummerieren_NachX_BestehendeBleiben_NeueBekommenDieNaechsten_AlleNeu()
    {
        var p = Wandprojekt();
        Decke(p);
        RohrDurchW1(p, "RO-1", 5000);
        Ok(Vorschlagen(p));                 // AUS-1 bei x 5000
        RohrDurchW1(p, "RO-2", 1000);
        RohrDurchW1(p, "RO-3", 3000);
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);
        Ok(Vorschlagen(p));                 // AUS-2 (D-1, Geschoss darunter: EG), AUS-3 (x 1000), AUS-4 (x 3000) – nach Bauteil und Lage angelegt
        Assert.Equal(["KA-1"], LeitungKz(p, Hole(p, "AUS-2")));
        Assert.Equal(["RO-2"], LeitungKz(p, Hole(p, "AUS-3")));
        Assert.Equal(Hole(p, "EG").Id, Hole(p, "AUS-2").Geschoss);
        var e = Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal("A-EG-001", Nummer(p, "AUS-3"));   // x 1000
        Assert.Equal("A-EG-002", Nummer(p, "AUS-4"));   // x 3000, y 0
        Assert.Equal("A-EG-003", Nummer(p, "AUS-2"));   // x 3000, y 2000 (Decke über dem EG)
        Assert.Equal("A-EG-004", Nummer(p, "AUS-1"));   // x 5000
        Assert.Contains(e.Hinweise, h => h == "4 Nummer(n) vergeben.");
        // eine neue links aussen: bekommt die nächste Nummer (höchste 004 + 1), die bestehenden bleiben
        RohrDurchW1(p, "RO-4", 500);
        Ok(Vorschlagen(p));
        var neu = VonLeitung(p, "RO-4");
        Assert.Null(neu.Text("nummer"));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal("A-EG-005", Nummer(p, neu.Kennzeichen));
        Assert.Equal("A-EG-001", Nummer(p, "AUS-3"));
        // nichts mehr zu tun
        var z = Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Empty(z.Aenderungen);
        Assert.Contains(z.Hinweise, h => h == "Keine Aussparung ohne Nummer.");
        // alle neu, nur EG: x 500, 1000, 3000 (y 0), 3000 (y 2000), 5000
        Ok(p.Ausfuehren("aussparungen_nummerieren", new JsonObject { ["geschoss"] = "EG", ["alle"] = true }));
        Assert.Equal("A-EG-001", Nummer(p, neu.Kennzeichen));   // x 500
        Assert.Equal("A-EG-002", Nummer(p, "AUS-3"));
        Assert.Equal("A-EG-003", Nummer(p, "AUS-4"));
        Assert.Equal("A-EG-004", Nummer(p, "AUS-2"));
        Assert.Equal("A-EG-005", Nummer(p, "AUS-1"));
        // Rückgängig nimmt die Nummern als ein Paket zurück
        Ok(p.Rueckgaengig());
        Assert.Equal("A-EG-005", Nummer(p, neu.Kennzeichen));
    }

    [Fact]
    public void Nummerieren_VeralteteBekommenKeine_ZaehlenAberMit()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Ok(Status(p, "veraltet", "AUS-1"));
        RohrDurchW1(p, "RO-2", 3000);
        Ok(Vorschlagen(p));   // RO-1 quert weiter: neuer Vorschlag neben dem von Hand veralteten AUS-1
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal("A-EG-001", Nummer(p, "AUS-1"));
        Assert.Equal("veraltet", Hole(p, "AUS-1").Text("status"));
        Assert.Equal("A-EG-002", Nummer(p, VonLeitung(p, "RO-1").Kennzeichen));   // 001 ist vergeben (veraltet)
        Assert.Equal("A-EG-003", Nummer(p, VonLeitung(p, "RO-2").Kennzeichen));
    }

    [Fact]
    public void Status_Uebergaenge()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        // vorgeschlagen → versandt: übersprungen, abgelehnt mit den zulässigen
        var n = Nein(Status(p, "versandt", "AUS-1"));
        Assert.Contains("AUS-1 ist «vorgeschlagen»", n.Fehler);
        Assert.Contains("vorgeschlagen → freigegeben, veraltet", n.Vorschlag);
        Assert.Equal("vorgeschlagen", Hole(p, "AUS-1").Text("status"));
        // Ablauf vorwärts, auch über die Nummer
        Ok(Status(p, "freigegeben", "A-EG-001", "AUS-2"));
        Ok(Status(p, "versandt", "AUS-1", "AUS-2"));
        Ok(Status(p, "angekommen", "AUS-1"));
        Ok(Status(p, "ausgefuehrt", "AUS-1"));
        Assert.Equal("ausgefuehrt", Hole(p, "AUS-1").Text("status"));
        Assert.Equal("aussparung_status", Hole(p, "AUS-1").Eigenschaft("status")!.Herkunft.Quelle);
        Nein(Status(p, "vorgeschlagen", "AUS-1"));
        // ein unzulässiger Übergang lehnt den ganzen Befehl ab
        Nein(Status(p, "freigegeben", "AUS-1", "AUS-2"));   // AUS-2 versandt → freigegeben ginge, AUS-1 nicht
        Assert.Equal("versandt", Hole(p, "AUS-2").Text("status"));
        // versandt → fehlt → erneut versandt
        Ok(Status(p, "fehlt", "AUS-2"));
        Nein(Status(p, "freigegeben", "AUS-2"));
        Ok(Status(p, "versandt", "AUS-2"));
        // veraltet nur zurück zu vorgeschlagen
        Ok(Status(p, "veraltet", "AUS-1"));
        Assert.Contains("veraltet → vorgeschlagen", Nein(Status(p, "freigegeben", "AUS-1")).Vorschlag);
        Ok(Status(p, "vorgeschlagen", "AUS-1"));
        // gleicher Status: Hinweis, keine Änderung; unbekannter Status bzw. unbekannte Aussparung
        var g = Ok(Status(p, "vorgeschlagen", "AUS-1"));
        Assert.Empty(g.Aenderungen);
        Assert.Contains("Status: vorgeschlagen, freigegeben", Nein(Status(p, "erledigt", "AUS-1")).Vorschlag);
        Assert.Contains("gibt es nicht", Nein(Status(p, "freigegeben", "AUS-99")).Fehler);
        Assert.Contains("keine Aussparung", Nein(Status(p, "freigegeben", "RO-1")).Fehler);
        // Hinweis: Freigabe ohne Nummer
        RohrDurchW1(p, "RO-3", 2500);
        Ok(Vorschlagen(p));
        var f = Ok(Status(p, "freigegeben", VonLeitung(p, "RO-3").Kennzeichen));
        Assert.Contains(f.Hinweise, h => h.Contains("hat noch keine Nummer"));
    }

    [Fact]
    public void P19_VeraltetUndFehlt_Warnung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        Ok(Vorschlagen(p));
        Assert.Empty(Modul3Hilfe.Befunde(p, "P19"));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Ok(Status(p, "veraltet", "AUS-1"));
        Ok(Status(p, "freigegeben", "AUS-2")); Ok(Status(p, "versandt", "AUS-2")); Ok(Status(p, "fehlt", "AUS-2"));
        var b = Modul3Hilfe.Befunde(p, "P19");
        Assert.Equal(2, b.Count);
        Assert.All(b, x => Assert.Equal("warnung", x.Schwere));
        Assert.Contains(b, x => x.Element == "AUS-1" && x.Text.Contains("A-EG-001 ist veraltet") && x.Vorschlag!.Contains("aussparungen_vorschlagen"));
        Assert.Contains(b, x => x.Element == "AUS-2" && x.Text.Contains("fehlt in der Architekten-Fassung") && x.Vorschlag!.Contains("aussparungen_abgleichen"));
    }

    static void Kollision(Projekt p, string kz, JsonObject eig) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = kz, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 0, 1000]) },
        }));

    [Fact]
    public void P20_DurchdringungOhneAussparung_Fehler_BisZurAussparung()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        var w1 = Hole(p, "W-1").Id;
        // Kollision wie B2 (kollision_pruefen): typ durchdringung, bauteil = Element-Id, Beteiligte Leitung und Wand
        Kollision(p, "KOL-1", new JsonObject
        {
            ["art"] = "hart", ["typ"] = "durchdringung", ["status"] = "neu", ["bauteil"] = w1, ["mass"] = 200,
            ["beteiligte"] = new JsonArray(Hole(p, "RO-1").Id, w1),
        });
        var b = Assert.Single(Modul3Hilfe.Befunde(p, "P20"));
        Assert.Equal("fehler", b.Schwere);
        Assert.Equal("RO-1", b.Element);
        Assert.Equal("RO-1 durchdringt W-1 ohne Aussparung (Kollision KOL-1)", b.Text);
        Assert.Contains("aussparungen_vorschlagen (bauteile [\"W-1\"]", b.Vorschlag);
        // die Aussparung deckt sie ab
        var e = Ok(Vorschlagen(p));
        Assert.Contains(e.BehobeneBefunde, x => x.Code == "P20");
        Assert.Empty(Modul3Hilfe.Befunde(p, "P20"));
        // veraltet deckt nicht ab
        Ok(Status(p, "veraltet", "AUS-1"));
        Assert.Single(Modul3Hilfe.Befunde(p, "P20"));
        // akzeptierte Kollision: kein P20
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["status"] = "akzeptiert" } }));
        Assert.Empty(Modul3Hilfe.Befunde(p, "P20"));
    }

    [Fact]
    public void P20_FormateDerBeteiligten_UndNichtBeurteilbar()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        RohrDurchW1(p, "RO-2", 1000);
        // ohne Eigenschaft bauteil: aus den Beteiligten (Kennzeichen, Objekt { element }); ohne Status = neu
        Kollision(p, "KOL-1", new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["beteiligte"] = new JsonArray("RO-1", "W-1") });
        Kollision(p, "KOL-2", new JsonObject
        {
            ["art"] = "hart", ["typ"] = "durchdringung", ["status"] = "bestaetigt", ["bauteil"] = "W-1",
            ["beteiligte"] = new JsonArray(new JsonObject { ["element"] = "RO-2" }, new JsonObject { ["element"] = "W-1" }),
        });
        // kontakt, behoben, ohne Leitung, ohne Bauteil: kein P20
        Kollision(p, "KOL-3", new JsonObject { ["art"] = "hart", ["typ"] = "kontakt", ["beteiligte"] = new JsonArray("RO-1", "W-1") });
        Kollision(p, "KOL-4", new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["status"] = "behoben", ["beteiligte"] = new JsonArray("RO-1", "W-1") });
        Kollision(p, "KOL-5", new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["beteiligte"] = new JsonArray("W-1", "W-2") });
        Kollision(p, "KOL-6", new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["beteiligte"] = new JsonArray("RO-1") });
        var b = Modul3Hilfe.Befunde(p, "P20");
        Assert.Equal(["RO-1", "RO-2"], b.Select(x => x.Element).Order());
        Assert.Contains(b, x => x.Text.Contains("(Kollision KOL-1)"));
        Assert.Contains(b, x => x.Text.Contains("(Kollision KOL-2)"));
        Ok(Vorschlagen(p));
        Assert.Empty(Modul3Hilfe.Befunde(p, "P20"));
        // Aussparung für eine andere Leitung im selben Bauteil deckt nicht ab
        Kollision(p, "KOL-7", new JsonObject { ["art"] = "hart", ["typ"] = "durchdringung", ["beteiligte"] = new JsonArray("RO-1", "W-2") });
        Assert.Equal("RO-1 durchdringt W-2 ohne Aussparung (Kollision KOL-7)", Assert.Single(Modul3Hilfe.Befunde(p, "P20")).Text);
    }

    /// <summary>Geschoss-Regel (Hauptlauf 19.10.2026): die Deckenaussparung (D-1, z 2750 … 3000) gehört ins EG (Unterkante − 1 mm =
    /// 2749 im Band des EG). Nummern im EG nach x: Sammelaussparung RO-A/RO-B (Mitte x ≈ 1077.5) 001, Decke KA-1 (x 3000) 002,
    /// RO-1 (x 4000) 003. Koten der Decke über OK Geschoss (EG Kote 0) = über ±0.00: UK 2750, OK 3000.
    /// Kurzzeichen (Nachtrag B4d, SIA 400 B.10.2 Tabelle 5): Rohre im System KW (Trinkwasser kalt → sanitaer → S) durch die Wand
    /// «S WD», Kanal im System ZUL (Zuluft → lueftung → L) durch die Decke «L DD».</summary>
    [Fact]
    public void Aussparungsliste_Felder_UndGeschossfilter()
    {
        var p = Wandprojekt();
        Decke(p);
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);
        RohrDurchW1(p, "RO-1", 4000);
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        RundVonHand(p, "RO-1");   // B3b: rund nur von Hand – damit die Felder der runden Zeile geprüft bleiben
        var l = (JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject());
        Assert.Equal("Aussparungsliste", l["ableitung"]!.GetValue<string>());
        Assert.Equal(3, l["anzahl"]!.GetValue<int>());
        Assert.Equal(3, l["jeStatus"]!["vorgeschlagen"]!.GetValue<int>());
        var zeilen = l["zeilen"]!.AsArray().Select(z => z!.AsObject()).ToList();
        Assert.Equal(["A-EG-001", "A-EG-002", "A-EG-003"], zeilen.Select(z => z["nummer"]!.GetValue<string>()));
        Assert.Equal(["S WD", "L DD", "S WD"], zeilen.Select(z => z["kurzzeichen"]!.GetValue<string>()));
        var rund = zeilen[2];
        Assert.Equal("EG", rund["geschoss"]!.GetValue<string>());
        Assert.Equal("W-1", rund["bauteil"]!.GetValue<string>());
        Assert.Equal("Wand", rund["bauteilArt"]!.GetValue<string>());
        Assert.Equal("rund", rund["form"]!.GetValue<string>());
        Assert.Equal("Ø 100", rund["masse"]!.GetValue<string>());   // 55 + 2 × 20 = 95 → ganze cm (Nachtrag B4e)
        Assert.Equal(100, rund["d"]!.GetValue<double>());
        Assert.Equal(200, rund["tiefe"]!.GetValue<double>());
        Assert.Equal(1000, rund["koteAchse"]!.GetValue<double>());
        Assert.Equal(1000, rund["koteAchseGeschoss"]!.GetValue<double>());
        Assert.Null(rund["koteUk"]);
        Assert.Equal("kernbohrung", rund["art"]!.GetValue<string>());
        Assert.Equal("RO-1", Assert.Single(rund["leitungen"]!.AsArray())!.GetValue<string>());
        Assert.True(rund["formVonHand"]!.GetValue<bool>());
        Assert.True(rund["formWaehlbar"]!.GetValue<bool>());
        Assert.Null(rund["formSperre"]);
        var sammel = zeilen[0];
        Assert.Equal("250 × 100", sammel["masse"]!.GetValue<string>());   // 155 + 95 = 250; 95 → 100
        Assert.False(sammel["formVonHand"]!.GetValue<bool>());
        Assert.False(sammel["formWaehlbar"]!.GetValue<bool>());
        Assert.Equal("Sammelaussparung (2 Leitungen) – bleibt rechteckig", sammel["formSperre"]!.GetValue<string>());
        Assert.Equal(950, sammel["koteUk"]!.GetValue<double>());           // 1000 ∓ 100/2
        Assert.Equal(1050, sammel["koteOk"]!.GetValue<double>());
        Assert.Equal(["RO-A", "RO-B"], sammel["leitungen"]!.AsArray().Select(x => x!.GetValue<string>()));
        var decke = zeilen[1];
        Assert.Equal("D-1", decke["bauteil"]!.GetValue<string>());
        Assert.Equal("EG", decke["geschoss"]!.GetValue<string>());          // Geschoss unter der Decke
        Assert.Equal(2750, decke["koteUk"]!.GetValue<double>());
        Assert.Equal(2750, decke["koteUkGeschoss"]!.GetValue<double>());   // über OK Geschoss EG (Kote 0): 2750 − 0
        Assert.Equal(3000, decke["koteOkGeschoss"]!.GetValue<double>());   // 3000 − 0
        Assert.Equal("440 × 240", decke["masse"]!.GetValue<string>());
        Assert.False(decke["formVonHand"]!.GetValue<bool>());
        Assert.True(decke["formWaehlbar"]!.GetValue<bool>());
        // freigegeben: nicht wählbar, mit Grund
        var kzDecke = decke["aussparung"]!.GetValue<string>();
        Ok(Status(p, "freigegeben", kzDecke));
        var frei = ((JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject { ["geschoss"] = "EG" }))["zeilen"]!.AsArray()
            .Single(z => z!["aussparung"]!.GetValue<string>() == kzDecke)!;
        Assert.False(frei["formWaehlbar"]!.GetValue<bool>());
        Assert.Equal("Status freigegeben – zuerst aussparung_status vorgeschlagen", frei["formSperre"]!.GetValue<string>());
        Ok(Status(p, "vorgeschlagen", kzDecke));
        // nur EG: alle drei; OG: keine
        var eg = (JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject { ["geschoss"] = "EG" });
        Assert.Equal(3, eg["anzahl"]!.GetValue<int>());
        Assert.Equal("EG", eg["geschoss"]!.GetValue<string>());
        Assert.Equal(0, ((JsonObject)Tuer.Abfrage(p, "aussparungsliste", new JsonObject { ["geschoss"] = "OG" }))["anzahl"]!.GetValue<int>());
        // als Listenansicht: der Katalog erlaubt «aussparungsliste»
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-AUS", ["eigenschaften"] = new JsonObject { ["art"] = "liste", ["liste"] = "aussparungsliste" },
        }));
        Assert.Contains(Abfragen.Liste, a => a.Name == "aussparungsliste" && a.Parameter.Single().Name == "geschoss");
    }
}
