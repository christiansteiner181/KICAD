using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1, Nachtrag aus dem Abnahmelauf Belimo (Hauptlauf 18.10.2026): gewinde eines Anschlusses darf der Name
/// eines Textparameters sein (Wert je Typ, unverändert übernommen); sonst bleibt der Text. Am Element, in anschlusslagen,
/// element_lesen und familie_ansicht; Typwechsel führt nach. Werte fiktiv.</summary>
public class FamilieGewindeTests
{
    static JsonObject Gewindefamilie() => JsonAus("""
        { "format": 1, "name": "Testkugelhahn (fiktiv)", "kategorie": "Heizkoerper",
          "parameter": [{ "name": "rp", "art": "typ", "datentyp": "text" }, { "name": "dn", "art": "typ", "datentyp": "dn", "wert": 15 }],
          "typen": [{ "name": "A", "werte": { "rp": "1" } }, { "name": "B", "werte": { "rp": "Rp 3/4" } }, { "name": "C", "werte": {} }],
          "koerper": [{ "id": "k", "art": "quader", "von": [0, 0, 0], "bis": [100, 50, 50] }],
          "anschluesse": [
            { "name": "ein", "medium": "HZ-VL", "fluss": "rein", "art": "rohr", "dn": "dn", "gewinde": " rp ", "lage": [0, 25, 25], "richtung": [-1, 0, 0] },
            { "name": "aus", "medium": "HZ-RL", "fluss": "raus", "art": "rohr", "dn": "dn", "gewinde": "G 1/2", "lage": [100, 25, 25], "richtung": [1, 0, 0] },
            { "name": "mess", "medium": "*", "fluss": "beide", "art": "rohr", "dn": 8, "gewinde": "dn", "lage": [50, 25, 50], "richtung": [0, 0, 1] }] }
        """);

    static Projekt MitHahn(string typ = "A")
    {
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Gewindefamilie() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = typ, ["punkt"] = new JsonArray(0, 0, 0), ["kennzeichen"] = "HK-1" }));
        return p;
    }

    static Dictionary<string, JsonObject> Lagen(Projekt p) =>
        ((JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "HK-1" }))["anschluesse"]!.AsArray()
            .Select(x => x!.AsObject()).ToDictionary(x => x["name"]!.GetValue<string>());

    [Fact]
    public void Gewinde_TextparameterGibtDenWertDesTyps_FreierTextBleibt()
    {
        var p = MitHahn("A");
        var hk = Hole(p, "HK-1");
        Assert.Equal("1", hk.AnschlussNamens("ein")!.Gewinde);          // Wert unverändert, wie im Typ
        Assert.Equal("G 1/2", hk.AnschlussNamens("aus")!.Gewinde);      // freier Text
        Assert.Equal("dn", hk.AnschlussNamens("mess")!.Gewinde);        // Name eines Zahlparameters: kein Textparameter → Text bleibt
        var l = Lagen(p);
        Assert.Equal("1", l["ein"]["gewinde"]!.GetValue<string>());
        Assert.Equal("G 1/2", l["aus"]["gewinde"]!.GetValue<string>());
        var lesen = (JsonObject)Tuer.Abfrage(p, "element_lesen", new JsonObject { ["element"] = "HK-1" });
        Assert.Equal("1", lesen["anschluesse"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "ein")!["gewinde"]!.GetValue<string>());
        var ansicht = (JsonObject)Tuer.Abfrage(p, "familie_ansicht", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "B", ["ansicht"] = "3d", ["detailgrad"] = "mittel" });
        Assert.Equal("Rp 3/4", ansicht["anschluesse"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "ein")!["gewinde"]!.GetValue<string>());
    }

    [Fact]
    public void Gewinde_TypwechselFuehrtNach_OhneWertKeinGewinde()
    {
        var p = MitHahn("A");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "B" } }));
        Assert.Equal("Rp 3/4", Hole(p, "HK-1").AnschlussNamens("ein")!.Gewinde);
        Assert.Equal("Rp 3/4", Lagen(p)["ein"]["gewinde"]!.GetValue<string>());
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "C" } }));
        Assert.Null(Hole(p, "HK-1").AnschlussNamens("ein")!.Gewinde);   // Typ C hat kein rp
        Assert.Equal("G 1/2", Hole(p, "HK-1").AnschlussNamens("aus")!.Gewinde);
    }

    [Fact]
    public void Gewinde_AmAnschlussMitKopieUndJsonUndBeimUmbenennen()
    {
        var a = new Anschluss { Name = "ein", Gewinde = "Rp 1/2" };
        Assert.Equal("Rp 1/2", a.Kopie().Gewinde);
        Assert.Equal("Rp 1/2", JsonSerializer.Deserialize<Anschluss>(Json.Text(a), Json.Optionen)!.Gewinde);
        Assert.DoesNotContain("gewinde", Json.Text(new Anschluss { Name = "x" }));
        var p = MitHahn("B");
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = "rp", ["neu"] = "rp_zoll" }),
        }));
        var def = (JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!;
        Assert.Equal("rp_zoll", def["anschluesse"]![0]!["gewinde"]!.GetValue<string>());
        Assert.Equal("Rp 3/4", Hole(p, "HK-1").AnschlussNamens("ein")!.Gewinde);
    }
}
