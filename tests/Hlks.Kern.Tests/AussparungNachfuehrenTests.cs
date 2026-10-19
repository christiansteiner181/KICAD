using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B3: Nachführen bestehender Aussparungen – Vorschläge wiedererkennen und nachführen, feste (ab freigegeben)
/// nicht ändern, sondern veraltet setzen und neu vorschlagen; aufgegangene Vorschläge löschen; Bezüge auf gelöschte Leitungen;
/// Auswahl nach Bauteilen, Leitungen und Geschoss. Werte FIKTIV (siehe AussparungHilfe).</summary>
public class AussparungNachfuehrenTests
{
    [Fact]
    public void Vorgeschlagen_LeitungVerschoben_GeometrieNachgefuehrt_KennzeichenUndNummerBleiben()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        var vorher = Assert.Single(Aussparungen(p));
        Verschieben(p, "RO-1", [3500, -500, 1200], [3500, 500, 1200]);
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(vorher.Id, a.Id);
        Assert.Equal("AUS-1", a.Kennzeichen);
        Assert.Equal("A-EG-001", a.Text("nummer"));
        Assert.Equal("vorgeschlagen", a.Text("status"));
        Assert.Equal([3500, 0, 1200], Mitte(a));
        Etwa(1200 - 50, a.Zahl("kote_uk")); Etwa(1200 + 50, a.Zahl("kote_ok"));   // 95 × 95 → ganze cm 100 × 100 (B3b, B4e)
        Assert.Contains(e.Aenderungen, x => x.Art == "geaendert" && x.Kennzeichen == "AUS-1");
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-1 (A-EG-001) nachgeführt"));
        // Grösse ändert sich mit der Leitung: DN 20 (da 25) → 25 + 40 = 65 → ganze cm 70 × 70
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "RO-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" } }));
        Ok(Vorschlagen(p));
        Etwa(70, Assert.Single(Aussparungen(p)).Zahl("b")); Etwa(70, Assert.Single(Aussparungen(p)).Zahl("h"));
    }

    [Fact]
    public void Freigegeben_KleineVerschiebungImSpiel_GiltWeiter()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        Verschieben(p, "RO-1", [3010, -500, 1000], [3010, 500, 1000]);   // 10 + 27.5 ≤ 50 (Aussparung 100 breit): passt noch
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("freigegeben", a.Text("status"));
        Assert.Equal([3000, 0, 1000], Mitte(a));   // nicht verändert
        Assert.Empty(e.Aenderungen);
        Assert.Contains(e.Hinweise, h => h.Contains("1 feste gelten weiter"));
    }

    [Fact]
    public void Freigegeben_LeitungPasstNichtMehr_Veraltet_UndNeuerVorschlag()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Ok(Status(p, "freigegeben", "AUS-1"));
        Ok(Status(p, "versandt", "AUS-1"));
        Verschieben(p, "RO-1", [3030, -500, 1000], [3030, 500, 1000]);   // 30 + 27.5 = 57.5 > 50: Querschnitt ausserhalb
        var e = Ok(Vorschlagen(p));
        var alt = Hole(p, "AUS-1");
        Assert.Equal("veraltet", alt.Text("status"));
        Assert.Equal([3000, 0, 1000], Mitte(alt));   // Geometrie bleibt
        Assert.Contains("RO-1 passt nicht mehr hindurch", alt.Eigenschaft("status")!.Herkunft.Quelle);
        var neu = VonLeitung(p, "RO-1");
        Assert.Equal("AUS-2", neu.Kennzeichen);
        Assert.Equal("vorgeschlagen", neu.Text("status"));
        Assert.Equal([3030, 0, 1000], Mitte(neu));
        Assert.Null(neu.Text("nummer"));
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-1 (A-EG-001) (versandt → veraltet): RO-1 passt nicht mehr hindurch (Querschnitt ausserhalb)"));
        Assert.Contains(e.NeueBefunde, b => b.Code == "P19" && b.Element == "AUS-1");
        // nächster Lauf: der veraltete bleibt veraltet, der neue unverändert
        var z = Ok(Vorschlagen(p));
        Assert.Empty(z.Aenderungen);
    }

    [Fact]
    public void Freigegeben_LeitungQuertNichtMehr_Veraltet_OhneNeuenVorschlag()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        Verschieben(p, "RO-1", [3000, 500, 1000], [3000, 1500, 1000]);   // ganz im Raum
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("veraltet", a.Text("status"));
        Assert.Contains("RO-1 quert das Bauteil nicht mehr", a.Eigenschaft("status")!.Herkunft.Quelle);
    }

    [Fact]
    public void Vorgeschlagen_LeitungQuertNichtMehr_Veraltet()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        Verschieben(p, "RO-1", [3000, 500, 1000], [3000, 1500, 1000]);
        Ok(Vorschlagen(p));
        Assert.Equal("veraltet", Assert.Single(Aussparungen(p)).Text("status"));
    }

    [Fact]
    public void Freigegeben_WeitereLeitungGehtGanzHindurch_BezugErgaenzt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);   // Sammelaussparung 250 × 100 (95 → ganze cm), x 952.5 … 1202.5
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        // DN 15 (da 20, FIKTIV) mitten zwischen beiden: Querschnitt x 1067.5 … 1087.5, z 990 … 1010 liegt in der Sammelaussparung
        Ok(Modul3Hilfe.Rohr(p, "RO-C", "DN 15", [1077.5, -500, 1000], [1077.5, 500, 1000]));
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("freigegeben", a.Text("status"));
        Etwa(250, a.Zahl("b"));
        Assert.Equal(["RO-A", "RO-B", "RO-C"], LeitungKz(p, a));
        Assert.Contains(e.Hinweise, h => h.Contains("RO-C geht ganz hindurch – als Leitung ergänzt"));
    }

    [Fact]
    public void ZweiVorschlaege_RueckenZusammen_EinerNachgefuehrt_DerAndereAufgegangen()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1300);   // Randabstand 205: getrennt
        Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        Verschieben(p, "RO-B", [1155, -500, 1000], [1155, 500, 1000]);   // Randabstand 60: zusammen
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("AUS-1", a.Kennzeichen);   // näher an der neuen Mitte (1077.5) als AUS-2 (1300)
        Assert.Equal("rechteckig", a.Text("form"));
        Assert.Equal(["RO-A", "RO-B"], LeitungKz(p, a));
        Assert.Contains(e.Aenderungen, x => x.Art == "geloescht" && x.Kennzeichen == "AUS-2");
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-2 gelöscht: aufgegangen in AUS-1"));
    }

    [Fact]
    public void Sammel_Vorgeschlagen_EineLeitungWeg_WirdEinzeln_DieAndereNeu()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);
        Ok(Vorschlagen(p));
        Assert.Equal("rechteckig", Assert.Single(Aussparungen(p)).Text("form"));
        Verschieben(p, "RO-B", [3000, -500, 1000], [3000, 500, 1000]);
        Ok(Vorschlagen(p));
        var a = Hole(p, "AUS-1");   // Mitte 1077.5: näher bei RO-A (1000) als bei RO-B (3000)
        Assert.Equal("rechteckig", a.Text("form"));   // B3b: auch die Einzelaussparung bleibt rechteckig
        Etwa(100, a.Zahl("b")); Etwa(100, a.Zahl("h"));   // 95 → ganze cm (B4e)
        Assert.Null(a.Zahl("d"));
        Etwa(950, a.Zahl("kote_uk")); Etwa(1050, a.Zahl("kote_ok"));
        Assert.Equal([1000, 0, 1000], Mitte(a));
        Assert.Equal(["RO-A"], LeitungKz(p, a));
        Assert.Equal("AUS-2", VonLeitung(p, "RO-B").Kennzeichen);
    }

    [Fact]
    public void Sammel_Freigegeben_EineLeitungWeg_Veraltet_BeideNeuVorgeschlagen()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        RohrDurchW1(p, "RO-B", 1155);
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        Verschieben(p, "RO-B", [3000, -500, 1000], [3000, 500, 1000]);
        Ok(Vorschlagen(p));
        var alt = Hole(p, "AUS-1");
        Assert.Equal("veraltet", alt.Text("status"));
        Assert.Equal("RO-B passt nicht mehr hindurch (Querschnitt ausserhalb)", alt.Eigenschaft("status")!.Herkunft.Quelle!.Split(": ", 2)[1]);
        Assert.Equal(["RO-A", "RO-B"], LeitungKz(p, alt));   // Bezüge bleiben
        Assert.Equal([1000, 0, 1000], Mitte(VonLeitung(p, "RO-A")));   // RO-A passte noch, wird aber mit neu vorgeschlagen
        Assert.Equal([3000, 0, 1000], Mitte(VonLeitung(p, "RO-B")));
        Assert.Equal(3, Aussparungen(p).Count);
    }

    [Fact]
    public void Freigegeben_UndNeueLeitungDaneben_KeinZusammenfassenMitFesten()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-A", 1000);
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        RohrDurchW1(p, "RO-B", 1155);   // Randabstand 60 zur festen – diese bleibt, wie sie ist
        Ok(Vorschlagen(p));
        Assert.Equal("freigegeben", Hole(p, "AUS-1").Text("status"));
        Etwa(100, Hole(p, "AUS-1").Zahl("b"));   // 95 → ganze cm (B4e)
        var b = VonLeitung(p, "RO-B");
        Assert.Equal("AUS-2", b.Kennzeichen);
        Assert.Equal("rechteckig", b.Text("form"));
        Assert.Equal(["RO-B"], LeitungKz(p, b));
    }

    [Fact]
    public void LeitungGeloescht_FremdesPaket_VorschlagVeraltet_BezugEntfernt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(Vorschlagen(p));
        // element_loeschen löst den Bezug selbst und setzt veraltet (Hauptlauf 19.10.2026, AussparungLoeschenTests) – hier nur im Trockenlauf
        var trocken = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "RO-1" }, trockenlauf: true));
        Assert.Contains(trocken.Hinweise, h => h.StartsWith("Aussparung AUS-1") && h.Contains("Status veraltet"));
        Assert.Equal("vorgeschlagen", Hole(p, "AUS-1").Text("status"));
        // gelöscht über ein fremdes Paket (z. B. ein anderer Rechner, Synchronisieren): der Bezug zeigt ins Leere (P08)
        var ro = Hole(p, "RO-1");
        p.Modell.Anwenden([new Operation { Art = "element.loeschen", Id = ro.Id, Basis = ro.Version, ElementVorher = ro.Kopie() }], "99999999T000000000Z_x_x_000001");
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08" && b.Element == "AUS-1");
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal("veraltet", a.Text("status"));
        Assert.Empty(Aussparungsbezug.Leitungen(a));
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-1: Bezug auf gelöschte Leitung(en) entfernt"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08" && b.Element == "AUS-1");
    }

    [Fact]
    public void VonHandAngelegt_OhneAchse_Freigegeben_BleibtUnberuehrt()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 3000);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = "AUS-H", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rund", ["d"] = 120, ["status"] = "freigegeben" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 0, 1000]) },
            ["bezuege"] = new JsonObject { ["bauteil"] = "W-1", ["leitung1"] = "RO-1" },
        }));
        var e = Ok(Vorschlagen(p));
        Assert.Equal("freigegeben", Hole(p, "AUS-H").Text("status"));
        Assert.Contains(e.Hinweise, h => h.Contains("AUS-H: Lage, Achse oder Masse fehlen"));
        Assert.Equal(2, Aussparungen(p).Count);   // der Bedarf wird neu vorgeschlagen
    }

    [Fact]
    public void Auswahl_Bauteile_Leitungen_Geschoss()
    {
        var p = Wandprojekt();
        Decke(p);
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 4000);
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);
        // nur die Decke
        Ok(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray("D-1") }));
        Assert.Equal(["KA-1"], Aussparungen(p).SelectMany(a => LeitungKz(p, a)));
        // nur Geschoss OG (die Decke gehört zum OG) – nichts Neues
        Assert.Empty(Ok(Vorschlagen(p, new JsonObject { ["geschoss"] = "OG" })).Aenderungen);
        // nur RO-1: das Bauteil W-1 wird ganz behandelt (auch RO-2)
        var e = Ok(Vorschlagen(p, new JsonObject { ["leitungen"] = new JsonArray("RO-1") }));
        Assert.Equal(3, Aussparungen(p).Count);
        Assert.Equal(2, e.Aenderungen.Count);
        // Fehler: Bauteil, das keine Aussparungen bekommt; unbekannte Leitung
        Assert.Contains("bekommt keine Aussparungen", Nein(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray("RO-1") })).Fehler);
        Assert.Contains("gibt es nicht", Nein(Vorschlagen(p, new JsonObject { ["leitungen"] = new JsonArray("RO-99") })).Fehler);
        Assert.Contains("keine Leitung", Nein(Vorschlagen(p, new JsonObject { ["leitungen"] = new JsonArray("W-1") })).Fehler);
    }

    [Fact]
    public void Auswahl_AndereBauteileBleibenUnberuehrt()
    {
        var p = Wandprojekt();
        Decke(p);
        RohrDurchW1(p, "RO-1", 1000);
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);
        Ok(Vorschlagen(p));
        Assert.Equal(2, Aussparungen(p).Count);
        // RO-1 verschieben, aber nur die Decke behandeln: die Wand-Aussparung bleibt unverändert
        Verschieben(p, "RO-1", [2000, -500, 1000], [2000, 500, 1000]);
        Assert.Empty(Ok(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray("D-1") })).Aenderungen);
        Assert.Equal([1000, 0, 1000], Mitte(VonLeitung(p, "RO-1")));
        Ok(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray("W-1") }));
        Assert.Equal([2000, 0, 1000], Mitte(VonLeitung(p, "RO-1")));
    }
}
