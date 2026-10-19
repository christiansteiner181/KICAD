using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1, Nachtrag (Entscheide Hauptlauf 18.10.2026): Kanalanschlüsse mit d bzw. b × h, Konturen aus zwei Punkten
/// mit Wölbung, Zusammenfassung der Definition in Darstellung.Element, Ablehnung beim Löschen einer verwendeten Familie.
/// Alle Masse frei gewählt (fiktiv).</summary>
public class FamilieNachtragTests
{
    /// <summary>Volumenstromregler (fiktiv) mit rundem Eintritt d 125 und rechteckigem Austritt 400 × 200, dazu ein Rohranschluss
    /// ohne dn und ein runder Kanal ohne d (Lücken → Hinweise).</summary>
    static JsonObject Kanalfamilie() => JsonAus("""
        { "format": 1, "name": "Testregler Kanal (fiktiv)", "kategorie": "Volumenstromregler",
          "parameter": [{ "name": "d", "art": "typ", "datentyp": "laenge" }, { "name": "laenge", "art": "typ", "datentyp": "laenge", "wert": 300 }],
          "typen": [{ "name": "125", "werte": { "d": 125 } }, { "name": "160", "werte": { "d": 160 } }],
          "koerper": [{ "id": "rohr", "art": "zylinder", "von": [0, 0, 0], "nach": [0, "laenge", 0], "r": "d / 2" }],
          "anschluesse": [
            { "name": "ein", "medium": "ZUL", "fluss": "rein", "art": "kanal", "form": "rund", "d": "d", "lage": [0, 0, 0], "richtung": [0, -1, 0] },
            { "name": "aus", "medium": "ZUL", "fluss": "raus", "art": "kanal", "form": "rechteck", "b": 400, "h": 200, "lage": [0, "laenge", 0], "richtung": [0, 1, 0] },
            { "name": "mess", "medium": "*", "fluss": "beide", "art": "rohr", "lage": [0, 150, 0], "richtung": [0, 0, 1] },
            { "name": "leer", "medium": "ZUL", "fluss": "beide", "art": "kanal", "form": "rund", "lage": [0, 150, 0], "richtung": [1, 0, 0] }] }
        """);

    // ------------------------------------------------------------------ 1. Kanalanschlüsse

    [Fact]
    public void Kanal_DefinitionGibtHinweiseStattFehler()
    {
        var f = Def(Kanalfamilie());
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.Contains(f.Hinweise, h => h.Stelle == "anschluss «mess»" && h.Meldung.Contains("ohne dn"));
        Assert.Contains(f.Hinweise, h => h.Stelle == "anschluss «leer»" && h.Meldung.Contains("ohne d "));
        Assert.DoesNotContain(f.Hinweise, h => h.Stelle is "anschluss «ein»" or "anschluss «aus»");
        var r = Def(Mini(anschluesse: """
            [{ "name": "a", "medium": "ZUL", "fluss": "rein", "art": "kanal", "form": "rechteck", "b": 400, "lage": [0, 0, 0], "richtung": [0, 0, 1] },
             { "name": "b", "medium": "ZUL", "fluss": "rein", "art": "kanal", "lage": [0, 0, 0], "richtung": [0, 0, 1] },
             { "name": "c", "medium": "HZ-VL", "fluss": "rein", "art": "rohr", "dn": 15, "d": 20, "lage": [0, 0, 0], "richtung": [0, 0, 1] }]
            """));
        Assert.True(r.Gueltig);
        Assert.Contains(r.Hinweise, h => h.Stelle == "anschluss «a»" && h.Meldung.Contains("ohne b und h"));
        Assert.Contains(r.Hinweise, h => h.Stelle == "anschluss «b»" && h.Meldung.Contains("ohne form"));
        Assert.Contains(r.Hinweise, h => h.Stelle == "anschluss «c»" && h.Meldung.Contains("nur für Kanäle"));
        Assert.Empty(Def(Testheizkoerper()).Hinweise);   // Rohranschlüsse mit dn: nichts zu bemerken
    }

    [Fact]
    public void Kanal_PlatzierenFuehrtDUndBxHAnsElementUndInDieAnschlusslagen()
    {
        var p = Neu();
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalfamilie() }));
        Assert.Contains(e.Hinweise, h => h.Contains("anschluss «mess»") && h.Contains("ohne dn"));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));   // Armaturen brauchen ein System
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "125", ["punkt"] = new JsonArray(0, 0, 2500), ["kennzeichen"] = "VVS-1", ["system"] = "ZUL" }));
        var vvs = Hole(p, "VVS-1");
        var ein = vvs.AnschlussNamens("ein")!; var aus = vvs.AnschlussNamens("aus")!;
        Assert.Equal(125, ein.D);
        Assert.Null(ein.Dn); Assert.Null(ein.B);
        Assert.Equal(400, aus.B); Assert.Equal(200, aus.H);
        Assert.Null(aus.D);
        var lagen = (JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "VVS-1" });
        var j = lagen["anschluesse"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["name"]!.GetValue<string>());
        Assert.Equal(125, j["ein"]["d"]!.GetValue<double>());
        Assert.Equal(400, j["aus"]["b"]!.GetValue<double>());
        Assert.Equal(200, j["aus"]["h"]!.GetValue<double>());
        Assert.Null(j["ein"]["dn"]);
        // Typwechsel 160: d per Formel nachgeführt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "VVS-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "160" } }));
        Assert.Equal(160, Hole(p, "VVS-1").AnschlussNamens("ein")!.D);
        // JSON des Anschlusses: d, b, h wie im Modellschema; ohne Wert nicht geschrieben
        var json = Json.Knoten(Hole(p, "VVS-1").AnschlussNamens("aus")!).AsObject();
        Assert.Equal(400, json["b"]!.GetValue<int>());
        Assert.False(json.ContainsKey("d"));
        Assert.Contains(((JsonArray)((JsonObject)Tuer.Abfrage(p, "familie", new JsonObject { ["familie"] = "FAM-1" }))["hinweise"]!), h => h!["stelle"]!.GetValue<string>() == "anschluss «leer»");
    }

    [Fact]
    public void Anschluss_KopieUndJsonMitKanalmassen()
    {
        var a = new Anschluss { Name = "zul", Medium = "ZUL", Fluss = "rein", D = 125, B = 400, H = 200 };
        var k = a.Kopie();
        Assert.Equal((125, 400, 200), (k.D!.Value, k.B!.Value, k.H!.Value));
        var zurueck = System.Text.Json.JsonSerializer.Deserialize<Anschluss>(Json.Text(a), Json.Optionen)!;
        Assert.Equal((125, 400, 200), (zurueck.D!.Value, zurueck.B!.Value, zurueck.H!.Value));
        Assert.DoesNotContain("\"dn\"", Json.Text(new Anschluss { Name = "x", D = 100 }));   // ohne Wert nicht geschrieben
    }

    [Fact]
    public void Kanal_UngueltigeGroesseIstAuswertungsfehler()
    {
        var f = Def(Mini("""[{ "name": "d", "art": "typ", "datentyp": "laenge", "wert": 0 }]""",
            anschluesse: """[{ "name": "a", "medium": "ZUL", "fluss": "rein", "art": "kanal", "form": "rund", "d": "d", "lage": [0, 0, 0], "richtung": [0, 0, 1] }]"""));
        Assert.True(f.Gueltig);
        var x = Assert.Single(FamilienAuswertung.Aufloesen(f, "A", null).Fehler);
        Assert.Equal("anschluss «a».d", x.Stelle);
    }

    // ------------------------------------------------------------------ 2. Kontur aus zwei Punkten mit Wölbung

    [Fact]
    public void Kontur_KreisAusZweiWoelbungspunkten()
    {
        // Scheibe Ø 200 aus [[0, 0, 1], [200, 0, 1]] (zwei Halbkreise), Loch Ø 100 ebenso, Dicke 50:
        // V = (48-Eck(100) − 48-Eck(50)) · 50
        var f = Def(Mini(koerper: """
            [{ "id": "ring", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0, 1], [200, 0, 1]], "loecher": [[[50, 0, 1], [150, 0, 1]]], "von": 0, "bis": 50 },
             { "id": "halb", "art": "extrusion", "ebene": "xz", "kontur": [[0, 0], [100, 0, 1]], "von": 0, "bis": 10 },
             { "id": "torus", "art": "rotation", "ebene": "xy", "kontur": [[150, 0, 1], [250, 0, 1]], "achse": { "punkt": [0, 0], "richtung": "v" } }]
            """));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        var a = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.True(a.Gueltig, string.Join(" | ", a.Fehler));
        var t = a.Teile("fein");
        var ring = FamilienKoerper.AlsNetz(t[0]);
        Assert.Null(ring.GeschlossenFehler());
        Etwa((NEck(100) - NEck(50)) * 50, ring.Volumen(), 1e-3);
        // Halbkreis aus zwei Punkten, Wölbung nur am zweiten (Bogen zurück zum ersten): Halbscheibe r 50 → ½ · 48-Eck(50) · 10
        var halb = FamilienKoerper.AlsNetz(t[1]);
        Assert.Null(halb.GeschlossenFehler());
        Etwa(NEck(50) / 2 * 10, halb.Volumen(), 1e-3);
        Assert.Null(FamilienKoerper.AlsNetz(t[2]).GeschlossenFehler());
    }

    [Fact]
    public void Kontur_ZweiPunkteOhneWoelbungIstFehler_MitFormelBeimTyp()
    {
        var f = Def(Mini(koerper: """[{ "id": "k", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0], [100, 0]], "von": 0, "bis": 10 }]"""));
        var x = Assert.Single(f.Fehler);
        Assert.Equal("koerper «k».kontur", x.Stelle);
        Assert.Contains("keine Fläche", x.Meldung);
        Assert.Contains("Wölbung", x.Vorschlag);
        // Wölbung per Formel: erst die Auswertung weiss, ob eine Fläche entsteht
        var g = Def(Mini("""[{ "name": "w", "art": "typ", "datentyp": "zahl" }]""", """[{ "name": "rund", "werte": { "w": 1 } }, { "name": "flach", "werte": { "w": 0 } }]""",
            """[{ "id": "k", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0, "w"], [100, 0, "w"]], "von": 0, "bis": 10 }]"""));
        Assert.True(g.Gueltig, string.Join(" | ", g.Fehler));
        Assert.True(FamilienAuswertung.Aufloesen(g, "rund", null).Gueltig);
        Assert.Contains("keine Fläche", Assert.Single(FamilienAuswertung.Aufloesen(g, "flach", null).Fehler).Meldung);
    }

    // ------------------------------------------------------------------ 3. Darstellung: Zusammenfassung statt Definition

    [Fact]
    public void Darstellung_FamilieNurZusammenfassung()
    {
        var p = MitFamilie();
        var o = Darstellung.Element(p, Hole(p, "FAM-1"));
        var z = o["werte"]!["definition"]!.AsObject();
        Assert.True(z["zusammenfassung"]!.GetValue<bool>());
        Assert.Equal(HkName, z["name"]!.GetValue<string>());
        Assert.Equal("Heizkoerper", z["kategorie"]!.GetValue<string>());
        Assert.Equal(11, z["parameter"]!.GetValue<int>());
        Assert.Equal(["22-600-1000", "22-600-1400", "22-900-800"], z["typen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(4, z["koerper"]!.GetValue<int>());
        Assert.Equal(2, z["anschluesse"]!.GetValue<int>());
        Assert.Equal("entwurf", z["status"]!.GetValue<string>());
        Assert.Equal(1, z["version"]!.GetValue<double>());
        Assert.True(Json.Text(o).Length < 2000, $"Darstellung {Json.Text(o).Length} Zeichen");
        // die ganze Definition gibt es über die Abfrage familie; das Modell bleibt unverändert
        Assert.Equal(11, ((JsonObject)Tuer.Abfrage(p, "familie", new JsonObject { ["familie"] = "FAM-1" }))["definition"]!["parameter"]!.AsArray().Count);
        Assert.IsType<JsonArray>(((JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!)["parameter"]);
        // andere Elemente unverändert
        Assert.False(Darstellung.Element(p, Hole(p, "EG"))["werte"]!.AsObject().ContainsKey("zusammenfassung"));
    }

    // ------------------------------------------------------------------ 5. Familie löschen

    [Fact]
    public void FamilieLoeschen_VerwendetWirdAbgelehntMitVorschlag()
    {
        var p = MitHeizkoerper();
        var n = Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "FAM-1" }));
        Assert.Contains("HK-1", n.Fehler);
        Assert.Contains("löschen oder umhängen", n.Vorschlag);
        Assert.NotNull(p.Modell.Finde("FAM-1"));
        // unverwendet lässt sie sich löschen
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "HK-1" }));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "FAM-1" }));
        Assert.Null(p.Modell.Finde("FAM-1"));
    }
}
