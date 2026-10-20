using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Ableitungen (Raumliste, Strangliste, Mengen): richtige Werte, deterministisch, mit Modellstand.</summary>
public class AbleitungenTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly Projekt P = Beispielprojekt.Erzeugen(Kat, null);

    static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException($"Keine Zahl: {n?.ToJsonString() ?? "null"}");
    static string Text(JsonNode? n) => n?.GetValue<string>() ?? "";
    static JsonArray Liste(JsonNode? n) => n as JsonArray ?? throw new Xunit.Sdk.XunitException("Keine Liste");
    static JsonNode Zeile(JsonObject o, string schluessel, string wert) =>
        Liste(o["zeilen"]).Single(z => Text(z![schluessel]) == wert)!;

    // ---------------------------------------------------------------- Raumliste

    [Fact]
    public void Raumliste_EG()
    {
        var o = Ableitungen.Raumliste(P, "EG");
        Assert.Equal("Raumliste", Text(o["ableitung"]));
        Assert.Equal(5, (int)Zahl(o["anzahl"]));
        Assert.Equal(120.0, Zahl(o["flaecheTotal_m2"]), 2);
        Assert.Equal(5, Liste(o["zeilen"]).Count);
        Assert.All(Liste(o["zeilen"]), z => Assert.Equal("EG", Text(z!["geschoss"])));

        var bad = Zeile(o, "raum", "E.02");
        Assert.Equal(12.0, Zahl(bad["flaeche_m2"]), 2);
        Assert.Equal("Bad", Text(bad["name"]));
        Assert.Equal(2.5, Zahl(bad["hoehe_m"]), 2);
        Assert.Equal(30.0, Zahl(bad["volumen_m3"]), 2);
        Assert.Equal(3, (int)Zahl(bad["anzahlEndgeraete"]));
        Assert.Equal(["KW", "SW"], Liste(bad["systeme"]).Select(Text).ToArray());
        Assert.Equal(["BW-E.02 (Badewanne)", "WC-E.02 (WC)", "WT-E.02 (Waschtisch)"], Liste(bad["endgeraete"]).Select(Text).ToArray());

        var summe = Liste(o["summenJeGeschoss"]).Single()!;
        Assert.Equal("EG", Text(summe["geschoss"]));
        Assert.Equal(5, (int)Zahl(summe["raeume"]));
    }

    [Fact]
    public void Raumliste_Gesamt_NachGeschossUndNummerGeordnet()
    {
        var o = Ableitungen.Raumliste(P);
        Assert.Equal(13, (int)Zahl(o["anzahl"]));
        Assert.Equal(360.0, Zahl(o["flaecheTotal_m2"]), 2);
        var nummern = Liste(o["zeilen"]).Select(z => Text(z!["raum"])).ToList();
        // UG (tiefste Kote) zuerst, dann EG, dann 1.OG; innerhalb nach Nummer
        Assert.Equal(["U.01", "U.02", "U.03", "E.01", "E.02", "E.03", "E.04", "E.05", "1.01", "1.02", "1.03", "1.04", "1.05"], nummern);
        var summen = Liste(o["summenJeGeschoss"]);
        Assert.Equal(["UG", "EG", "1.OG"], summen.Select(s => Text(s!["geschoss"])).ToArray());
        Assert.All(summen, s => Assert.Equal(120.0, Zahl(s!["flaeche_m2"]), 2));
        // Die Summe der Zeilen stimmt mit dem Total überein
        Assert.Equal(Zahl(o["flaecheTotal_m2"]), Liste(o["zeilen"]).Sum(z => Zahl(z!["flaeche_m2"])), 2);
        // Endgeräte: 16 Apparate stehen in Räumen
        Assert.Equal(16, Liste(o["zeilen"]).Sum(z => (int)Zahl(z!["anzahlEndgeraete"])));
    }

    [Fact]
    public void Raumliste_UnbekanntesGeschoss_Fehler()
    {
        var f = Assert.Throws<HlksFehler>(() => Ableitungen.Raumliste(P, "5.OG"));
        Assert.Contains("5.OG", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    // ---------------------------------------------------------------- Strangliste

    [Fact]
    public void Strangliste_Werte()
    {
        var o = Ableitungen.Strangliste(P);
        Assert.Equal("Strangliste", Text(o["ableitung"]));
        Assert.Equal(9, (int)Zahl(o["anzahl"]));   // 7 Sanitär-Stränge + 2 Kälte-Stränge (KAE-V1, KAE-R1; Entscheid Christian 20.10.2026)

        void Pruefe(string strang, int direkt, int gesamt)
        {
            var z = Zeile(o, "strang", strang);
            Assert.Equal(direkt, (int)Zahl(z["anzahlDirekt"]));
            Assert.Equal(direkt, Liste(z["endgeraeteDirekt"]).Count);
            Assert.Equal(gesamt, (int)Zahl(z["anzahlGesamt"]));
        }
        Pruefe("SW-F1", 8, 8);
        Pruefe("SW-F2", 6, 6);
        Pruefe("SW-S1", 0, 14);
        Pruefe("SW-G1", 2, 16);
        Pruefe("KW-S1", 8, 8);
        Pruefe("KW-S2", 6, 6);
        Pruefe("KW-V1", 1, 15);
    }

    [Fact]
    public void Strangliste_Zeile_SchmutzwasserFallstrang1()
    {
        var o = Ableitungen.Strangliste(P);
        var z = Zeile(o, "strang", "SW-F1");
        Assert.Equal("SW", Text(z["system"]));
        Assert.Equal("Schmutzwasser", Text(z["systemtyp"]));
        Assert.Equal("SW", Text(z["medium"]));
        Assert.Equal("fallstrang", Text(z["art"]));
        Assert.Equal(["1.OG", "EG"], Liste(z["geschosse"]).Select(Text).ToArray());
        // B9b (Entscheid Christian 19.10.2026): Abwasser wird mit dem Aussendurchmesser bezeichnet; das Beispielprojekt nennt nur DN → keine Grösse, kein Ersatz aus DN
        Assert.Null(z["groesse"]);
        Assert.Contains("Abwasserleitung", Text(o["hinweis"]));
        Assert.Contains("WC-1.02 (WC, 1.02)", Liste(z["endgeraeteDirekt"]).Select(Text));
        var jeKlasse = (JsonObject)z["anzahlDirektJeKlasse"]!;
        Assert.Equal(2, (int)Zahl(jeKlasse["WC"]));
        Assert.Equal(2, (int)Zahl(jeKlasse["Waschtisch"]));
        Assert.Equal(2, (int)Zahl(jeKlasse["Badewanne"]));
        Assert.Equal(2, (int)Zahl(jeKlasse["Spueltisch"]));
        Assert.True(Zahl(z["leitungslaenge_m"]) > 10);
        Assert.Empty(Liste(z["datenpunkte"]));
    }

    [Fact]
    public void Strangliste_Datenpunkte_UndSystemfilter()
    {
        var kw = Ableitungen.Strangliste(P, "KW");
        Assert.Equal(3, (int)Zahl(kw["anzahl"]));
        Assert.Equal(["KW-S1", "KW-S2", "KW-V1"], Liste(kw["zeilen"]).Select(z => Text(z!["strang"])).ToArray());
        Assert.Equal(["DP-MV1", "DP-WZ1-DF", "DP-WZ1-VOL"], Liste(Zeile(kw, "strang", "KW-V1")["datenpunkte"]).Select(Text).ToArray());
        var sw = Ableitungen.Strangliste(P, "SW");
        Assert.Equal(4, (int)Zahl(sw["anzahl"]));
        Assert.Equal(["SW-F1", "SW-F2", "SW-G1", "SW-S1"], Liste(sw["zeilen"]).Select(z => Text(z!["strang"])).ToArray());
    }

    [Fact]
    public void Filter_MitElementDerFalschenKlasse_IstEinFehler()
    {
        // wie system_lesen («ist kein System»): ein Filter mit dem Kennzeichen eines WC darf nicht still eine leere Liste liefern
        var f1 = Assert.Throws<HlksFehler>(() => Ableitungen.Strangliste(P, "WC-E.02"));
        Assert.Contains("kein System", f1.Message);
    }

    [Fact]
    public void Filter_MitElementDerFalschenKlasse_Mengen()
    {
        Assert.Throws<HlksFehler>(() => Ableitungen.Mengen(P, "WC-E.02"));
        Assert.Throws<HlksFehler>(() => Ableitungen.Mengen(P, null, "WC-E.02"));
    }

    [Fact]
    public void Filter_MitElementDerFalschenKlasse_Raumliste()
    {
        Assert.Throws<HlksFehler>(() => Ableitungen.Raumliste(P, "WC-E.02"));
    }

    // ---------------------------------------------------------------- Mengen

    [Fact]
    public void Mengen_Leitungen_GesamtlaengeUndGruppen()
    {
        var o = Ableitungen.Mengen(P);
        Assert.Equal("Mengen", Text(o["ableitung"]));
        Assert.Equal(123.77, Zahl(o["leitungslaengeTotal_m"]), 2);   // nach den Gefälle-Korrekturen (vorher 120.19) = 119.77 Sanitär + 4.00 Kälte (KAE-V1-1, KAE-R1-1)

        var gruppen = Liste(o["leitungen"]);
        Assert.NotEmpty(gruppen);
        double summe = gruppen.Sum(g => Zahl(g!["laenge_m"]));
        // jede Gruppe ist auf 2 Stellen gerundet: Abweichung höchstens 0.005 je Gruppe
        Assert.InRange(Math.Abs(summe - Zahl(o["leitungslaengeTotal_m"])), 0, 0.005 * gruppen.Count + 1e-9);
        // Stückzahlen: 138 Verbindungen gehören zu Leitungen, die Gruppen zählen alle Leitungen
        int leitungen = P.Modell.Elemente.Count(e => Kat.IstArt(e, "Leitung"));
        Assert.Equal(leitungen, gruppen.Sum(g => (int)Zahl(g!["stueck"])));
        // unabhängig nachgerechnet
        double direkt = P.Modell.Elemente.Where(e => Kat.IstArt(e, "Leitung")).Sum(e => e.Geometrie!.Laenge()) / 1000;
        Assert.Equal(direkt, Zahl(o["leitungslaengeTotal_m"]), 2);
        // Gruppen sind eindeutig und sortiert
        var schluessel = gruppen.Select(g => $"{Text(g!["system"])}|{Text(g["klasse"])}|{Text(g["material"])}|{g["groesse"]}").ToList();
        Assert.Equal(schluessel.Count, schluessel.Distinct().Count());
        Assert.All(gruppen, g => Assert.Equal("Rohr", Text(g!["klasse"])));
        Assert.Contains(gruppen, g => Text(g!["system"]) == "SW" && Text(g["material"]) == "PE");
        Assert.Contains(gruppen, g => Text(g!["system"]) == "KW" && Text(g["material"]) == "Mehrschichtverbund");
    }

    [Fact]
    public void Mengen_SummeDerSystemeUndGeschosseIstGesamtlaenge()
    {
        double total = Zahl(Ableitungen.Mengen(P)["leitungslaengeTotal_m"]);
        double sw = Zahl(Ableitungen.Mengen(P, "SW")["leitungslaengeTotal_m"]);
        double kw = Zahl(Ableitungen.Mengen(P, "KW")["leitungslaengeTotal_m"]);
        double kae = Zahl(Ableitungen.Mengen(P, "KAE-VL")["leitungslaengeTotal_m"]) + Zahl(Ableitungen.Mengen(P, "KAE-RL")["leitungslaengeTotal_m"]);   // Kältekreis (Gewerk Heizung)
        Assert.Equal(4.0, kae, 2);
        Assert.Equal(total, sw + kw + kae, 1);
        Assert.True(sw > 0 && kw > 0);
        double jeGeschoss = new[] { "UG", "EG", "1.OG" }.Sum(g => Zahl(Ableitungen.Mengen(P, null, g)["leitungslaengeTotal_m"]));
        Assert.Equal(total, jeGeschoss, 1);
        // Filter erscheinen im Ergebnis
        var m = Ableitungen.Mengen(P, "SW", "EG");
        Assert.Equal("SW", Text(m["filter"]!["system"]));
        Assert.Equal("EG", Text(m["filter"]!["geschoss"]));
        Assert.All(Liste(m["leitungen"]), g => Assert.Equal("SW", Text(g!["system"])));
    }

    [Fact]
    public void Mengen_Bauteile_16Apparate()
    {
        var o = Ableitungen.Mengen(P);
        var stueck = Liste(o["bauteile"]);
        int apparate = stueck.Where(s => Kat.Klassen[Text(s!["klasse"])].IstArt("Apparat")).Sum(s => (int)Zahl(s!["stueck"]));
        Assert.Equal(16, apparate);
        var wc = stueck.Single(s => Text(s!["klasse"]) == "WC")!;
        Assert.Equal(4, (int)Zahl(wc["stueck"]));
        Assert.Equal("sanitaer", Text(wc["gewerk"]));
        Assert.Equal("251", Text(wc["bkp"]));
        Assert.Contains("vorläufig", Text(wc["bkpHinweis"]));
        // Leitungen und Gliederungselemente zählen nicht als Bauteile
        Assert.DoesNotContain(stueck, s => Text(s!["klasse"]) is "Rohr" or "System" or "Strang" or "Anlage");
        // Hausanschluss, Kontrollschacht, Wasserzähler, Ventil je 1
        foreach (var k in new[] { "Hausanschluss", "Kontrollschacht", "Wasserzaehler", "Ventil" })
            Assert.Equal(1, (int)Zahl(stueck.Single(s => Text(s!["klasse"]) == k && Text(s["gewerk"]) == "sanitaer")!["stueck"]));   // (Kältekreis: Pumpe und drei Ventile im Gewerk heizung)
    }

    // ---------------------------------------------------------------- Determinismus

    static string Zeilen(JsonObject o) => o["zeilen"]!.ToJsonString();

    [Fact]
    public void Determinismus_ZweiGetrenntErzeugteBeispieleLiefernGleicheZeilen()
    {
        var a = Beispielprojekt.Erzeugen(Kat, null);
        var b = Beispielprojekt.Erzeugen(Kat, null);
        // Ids und Prüfsumme unterscheiden sich …
        Assert.NotEqual(a.Modell.Finde("WC-E.02")!.Id, b.Modell.Finde("WC-E.02")!.Id);
        Assert.NotEqual(a.Modell.Pruefsumme(), b.Modell.Pruefsumme());
        Assert.NotEqual(a.Kopf.Id, b.Kopf.Id);
        // … die Zeilen nicht
        Assert.Equal(Zeilen(Ableitungen.Raumliste(a)), Zeilen(Ableitungen.Raumliste(b)));
        Assert.Equal(Zeilen(Ableitungen.Raumliste(a, "EG")), Zeilen(Ableitungen.Raumliste(b, "EG")));
        Assert.Equal(Zeilen(Ableitungen.Strangliste(a)), Zeilen(Ableitungen.Strangliste(b)));
        Assert.Equal(Zeilen(Ableitungen.Strangliste(a, "KW")), Zeilen(Ableitungen.Strangliste(b, "KW")));
        var ma = Ableitungen.Mengen(a); var mb = Ableitungen.Mengen(b);
        Assert.Equal(ma["leitungen"]!.ToJsonString(), mb["leitungen"]!.ToJsonString());
        Assert.Equal(ma["bauteile"]!.ToJsonString(), mb["bauteile"]!.ToJsonString());
        Assert.Equal(ma["leitungslaengeTotal_m"]!.ToJsonString(), mb["leitungslaengeTotal_m"]!.ToJsonString());
        // und der Modellstand unterscheidet sich
        Assert.NotEqual(Text(Ableitungen.Raumliste(a)["modellStand"]!["pruefsumme"]), Text(Ableitungen.Raumliste(b)["modellStand"]!["pruefsumme"]));
    }

    [Fact]
    public void Determinismus_WiederholterAufrufLiefertGleichesErgebnis()
    {
        Assert.Equal(Zeilen(Ableitungen.Raumliste(P)), Zeilen(Ableitungen.Raumliste(P)));
        Assert.Equal(Zeilen(Ableitungen.Strangliste(P)), Zeilen(Ableitungen.Strangliste(P)));
        Assert.Equal(Ableitungen.Mengen(P)["leitungen"]!.ToJsonString(), Ableitungen.Mengen(P)["leitungen"]!.ToJsonString());
    }

    // ---------------------------------------------------------------- Modellstand

    [Fact]
    public void JedeAbleitungTraegtModellstand()
    {
        foreach (var (o, art) in new[]
        {
            (Ableitungen.Raumliste(P), "Raumliste"),
            (Ableitungen.Strangliste(P), "Strangliste"),
            (Ableitungen.Mengen(P), "Mengen"),
        })
        {
            Assert.Equal(art, Text(o["ableitung"]));
            var stand = (JsonObject)o["modellStand"]!;
            Assert.Equal(P.Modell.Pruefsumme(), Text(stand["pruefsumme"]));
            Assert.Equal(P.BekanntePakete.Count, (int)Zahl(stand["pakete"]));
            Assert.Equal(Beispielprojekt.Name, Text(o["projekt"]));
            Assert.False(string.IsNullOrWhiteSpace(Text(o["erzeugt"])));
            Assert.False(string.IsNullOrWhiteSpace(Text(o["quelle"])));
        }
    }

    [Fact]
    public void Modellstand_VeraendertSichMitDemModell()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vorher = Ableitungen.Raumliste(p);
        Assert.True(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-2" }).Angenommen);
        var nachher = Ableitungen.Raumliste(p);
        Assert.NotEqual(Text(vorher["modellStand"]!["pruefsumme"]), Text(nachher["modellStand"]!["pruefsumme"]));
        Assert.Equal((int)Zahl(vorher["modellStand"]!["pakete"]) + 1, (int)Zahl(nachher["modellStand"]!["pakete"]));
        Assert.Equal(p.Modell.Pruefsumme(), Text(nachher["modellStand"]!["pruefsumme"]));
    }

    [Fact]
    public void Raumliste_FolgtDemModell_NeuerApparatImRaum()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var e = p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ausgussbecken", ["kennzeichen"] = "AB-1",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(8000, 7000, 0)) },
        });
        Assert.True(e.Angenommen, e.Fehler);
        var z = Zeile(Ableitungen.Raumliste(p, "EG"), "raum", "E.02");
        Assert.Equal(4, (int)Zahl(z["anzahlEndgeraete"]));
        Assert.Equal(17, Liste(Ableitungen.Raumliste(p)["zeilen"]).Sum(r => (int)Zahl(r!["anzahlEndgeraete"])));
    }
}
