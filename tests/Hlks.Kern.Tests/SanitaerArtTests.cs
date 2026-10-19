using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-K, Punkt 2: Apparateart (Eigenschaft sanitaerArt) mit Vorgabe je Klasse, von Hand, aus der Familie; abgeleitete Werte
/// luKw, luWw, du in element_lesen mit «?»-Herkunft (warum: Katalog und Excel-Zeile). Testfamilie FIKTIV (keine Herstellerdaten).</summary>
public class SanitaerArtTests
{
    static SanitaerZuordnung Art(Projekt p, string kz) => SanitaerArten.Von(p.Modell, p.Katalog, p.Modell.Hole(kz));

    [Theory]
    [InlineData("WC", "wc_75")]
    [InlineData("Waschtisch", "waschtisch")]
    [InlineData("Dusche", "dusche")]
    [InlineData("Badewanne", "badewanne")]
    [InlineData("Spueltisch", "spuelbecken")]
    [InlineData("Waschmaschine", "waschmaschine_6")]
    [InlineData("Ausgussbecken", "waschtrog")]
    public void Vorgabe_JeKlasse(string klasse, string art)
    {
        var p = Neu();
        Apparat(p, klasse, "X-1");
        var z = Art(p, "X-1");
        Assert.Equal(art, z.Art!.Schluessel);
        Assert.Equal("vorgabe", z.Woher);
        Assert.Contains($"Vorgabe der Klasse {klasse}", z.Herkunft);
        Assert.Null(p.Modell.Hole("X-1").Eigenschaft("sanitaerArt"));   // abgeleitet, nicht gespeichert
    }

    [Fact]
    public void Bodenablauf_OhneVorgabe_P21_ArtWaehlen()
    {
        var p = Neu();
        var e = Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Bodenablauf", ["kennzeichen"] = "BA-1", ["geschoss"] = "EG" }));
        Assert.Contains(e.NeueBefunde, b => b.Code == "P21" && b.Element == "BA-1" && b.Schwere == "warnung");
        var z = Art(p, "BA-1");
        Assert.Null(z.Art);
        Assert.Equal("keine", z.Woher);
    }

    [Fact]
    public void VonHand_Aendern_Ungueltig_Entfernen()
    {
        var p = Neu();
        Apparat(p, "WC", "WC-1");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "wc_90" } }));
        var z = Art(p, "WC-1");
        Assert.Equal(("wc_90", "eigenschaft"), (z.Art!.Schluessel, z.Woher));
        Assert.Contains("mensch, T", z.Herkunft);
        var n = Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "wc_99" } }));
        Assert.Contains("wc_75", n.Vorschlag);   // Liste der erlaubten Schlüssel
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = null } }));
        Assert.Equal(("wc_75", "vorgabe"), (Art(p, "WC-1").Art!.Schluessel, Art(p, "WC-1").Woher));
        // Gross-/Kleinschreibung wie jede Auswahl: gespeichert wird der Schlüssel des Katalogs
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "WC_90" } }));
        Assert.Equal("wc_90", p.Modell.Hole("WC-1").Text("sanitaerArt"));
    }

    [Fact]
    public void UnbekannterSchluessel_AusAelteremStand_P21()
    {
        // Ein Schlüssel, den der Katalog nicht (mehr) kennt, kann nur aus einem fremden bzw. älteren Paket kommen: direkt als Operation anwenden
        var p = Neu();
        Apparat(p, "WC", "WC-1");
        var e = p.Modell.Hole("WC-1").Kopie();
        e.Eigenschaften["sanitaerArt"] = new Wert { Inhalt = JsonValue.Create("wc_alt"), Herkunft = new Herkunft { Art = "mensch", Akteur = "Alt" } };
        var m = p.Modell.Kopie();
        m.Anwenden([new Operation { Art = "element.setzen", Id = e.Id, Basis = p.Modell.Hole("WC-1").Version, Element = e }]);
        var z = SanitaerArten.Von(m, p.Katalog, m.Hole("WC-1"));
        Assert.Equal(("unbekannt", "wc_alt"), (z.Woher, z.Schluessel));
        Assert.Contains(Pruefungen.Alle(m, p.Katalog), b => b.Code == "P21" && b.Element == "WC-1" && b.Text.Contains("wc_alt"));
    }

    static JsonObject WcFamilie(bool ziel) => JsonAus($$"""
        { "format": 1, "name": "Test-WC (fiktiv)", "kategorie": "WC",
          "parameter": [{ "name": "sanitaerArt", "art": "typ", "datentyp": "text"{{(ziel ? ", \"ziel\": \"sanitaerArt\"" : "")}} }],
          "typen": [{ "name": "7.5 l", "werte": { "sanitaerArt": "wc_75" } }, { "name": "9 l", "werte": { "sanitaerArt": "wc_90" } }],
          "koerper": [], "anschluesse": [] }
        """);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Familie_GibtDieArtImTypVor(bool ziel)
    {
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = WcFamilie(ziel) }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "9 l", ["punkt"] = new JsonArray(1000, 1000, 0), ["kennzeichen"] = "WC-F" }));
        var z = Art(p, "WC-F");
        Assert.Equal(("wc_90", "familie"), (z.Art!.Schluessel, z.Woher));
        Assert.Contains("Typ 9 l", z.Herkunft);
        if (ziel) Assert.Equal("familie", p.Modell.Hole("WC-F").Eigenschaft("sanitaerArt")!.Herkunft.Art);   // FamilienNachfuehren setzt die Eigenschaft
        else Assert.Null(p.Modell.Hole("WC-F").Eigenschaft("sanitaerArt"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-F", ["eigenschaften"] = new JsonObject { ["familientyp"] = "7.5 l" } }));
        Assert.Equal("wc_75", Art(p, "WC-F").Art!.Schluessel);
        // Die Familie hat keine Anschlüsse: Art mit LU KW, aber kein KW-Anschluss (P22)
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P22" && b.Element == "WC-F" && b.Text.Contains("keinen KW-Anschluss"));
    }

    [Fact]
    public void Familie_UngueltigerSchluesselImZiel_WirdAbgelehnt()
    {
        var d = WcFamilie(true);
        d["typen"]![1]!["werte"]!["sanitaerArt"] = "wc_99";
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "9 l", ["punkt"] = new JsonArray(1000, 1000, 0), ["kennzeichen"] = "WC-F" }));
    }

    [Fact]
    public void ElementLesen_AbgeleiteteWerte_UndWarumMitQuelle()
    {
        var p = Neu();
        Apparat(p, "WC", "WC-1");
        var o = (JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "WC-1" });
        var s = o["sanitaer"]!;
        Assert.Equal("wc_75", s["art"]!.GetValue<string>());
        Assert.Equal("Klosettanlage bis 7.5 Liter Spülwassermenge", s["name"]!.GetValue<string>());
        Assert.Equal("vorgabe", s["woher"]!.GetValue<string>());
        Assert.Equal(31, s["zeile"]!.GetValue<int>());
        Assert.Null(s["luWw"]);
        Assert.Contains("Tabelle1 Zeile 31", s["quelle"]!.GetValue<string>());
        Assert.Equal(1, Zahl(o["abgeleitet"]!["luKw"]));
        Assert.Equal(2, Zahl(o["abgeleitet"]!["du"]));
        Assert.Null(o["abgeleitet"]!["luWw"]);   // das WC hat keinen WW-Anschluss: kein Wert, nicht 0
        foreach (var (wert, einheit, zahl) in new[] { ("luKw", "LU", (double?)1), ("du", "DU", 2), ("luWw", "LU", null) })
        {
            var w = (JsonObject)Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "WC-1", ["eigenschaft"] = wert });
            Assert.Equal(zahl, w["wert"]?.GetValue<double>());
            Assert.Equal(einheit, w["einheit"]!.GetValue<string>());
            Assert.Contains("sanitaer_belastungswerte.json", w["quelle"]!.GetValue<string>());
            Assert.Contains("Zeile 31", w["quelle"]!.GetValue<string>());
            Assert.Contains("Excel", w["rechenweg"]!.GetValue<string>());
        }
        var art = (JsonObject)Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "WC-1", ["eigenschaft"] = "sanitaerArt" });
        Assert.Equal("wc_75", art["wert"]!.GetValue<string>());
        Assert.Contains("Vorgabe der Klasse WC", art["begruendung"]!.GetValue<string>());
        // gesetzte Art: allgemeine Herkunft mit Verlauf
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "wc_90" } }));
        var gesetzt = (JsonObject)Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "WC-1", ["eigenschaft"] = "sanitaerArt" });
        Assert.Equal("wc_90", gesetzt["wert"]!.GetValue<string>());
        Assert.NotNull(gesetzt["herkunft"]);
        Assert.Single(gesetzt["verlauf"]!.AsArray());
        Assert.Equal(2.5, Zahl(((JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "WC-1" }))["abgeleitet"]!["du"]));
    }

    [Fact]
    public void ElementLesen_Leitung_Belastungssummen_WarumMitBeitraegen()
    {
        var p = Netzprojekt(0.5, "KW", "SW");
        Apparat(p, "WC", "WC-1"); Hausanschluss(p); Kontrollschacht(p);
        Rohr(p, "R1", "KW-1", "HA-1.aus", "WC-1.kw");
        Rohr(p, "S1", "SW-1", "WC-1.sw", "KS-1.ein");
        var r = (JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "R1" });
        Assert.Equal(1, Zahl(r["abgeleitet"]!["summeLuKw"]));
        Assert.Equal(0, Zahl(r["abgeleitet"]!["summeLuWw"]));
        Assert.Equal(1, Zahl(r["abgeleitet"]!["groessterLu"]));
        Assert.NotNull(r["abgeleitet"]!["laenge_m"]);   // die bisherigen abgeleiteten Werte bleiben
        Assert.Equal("trinkwasser", r["sanitaer"]!["netz"]!.GetValue<string>());
        var s = (JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "S1" });
        Assert.Equal(2, Zahl(s["abgeleitet"]!["summeDu"]));
        Assert.Null(s["abgeleitet"]!["summeLuKw"]);
        var w = (JsonObject)Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "S1", ["eigenschaft"] = "summeDu" });
        Assert.Equal(2, Zahl(w["wert"]));
        Assert.Equal("WC-1", w["beitraege"]![0]!["apparat"]!.GetValue<string>());
        Assert.Contains("Zeile 31", w["beitraege"]![0]!["quelle"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "S1", ["eigenschaft"] = "summeLuKw" }));
        // Formteile, Räume, Heizkörper: kein Block sanitaer
        Assert.Null(((JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "A-1" }))["sanitaer"]);
    }
}
