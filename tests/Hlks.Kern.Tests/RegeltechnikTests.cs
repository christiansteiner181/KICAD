using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Herstellerbibliothek H1 (entwurf/herstellerbibliothek.md): Abschnitt regeltechnik, antriebe und anbau der Familie, Abfrage
/// regeltechnik. Alle Werte FIKTIV («Musterantrieb AG») – keine Herstellerdaten.</summary>
public class RegeltechnikTests
{
    const string Antrieb = """
        { "format": 1, "name": "Musterantrieb (fiktiv)", "kategorie": "Antrieb", "hersteller": "Musterantrieb AG",
          "parameter": [
            { "name": "u", "art": "typ", "datentyp": "zahl", "einheit": "V", "wert": 24 },
            { "name": "p_betrieb", "art": "typ", "datentyp": "zahl", "einheit": "W" },
            { "name": "laufzeit", "art": "typ", "datentyp": "zahl", "einheit": "s" },
            { "name": "kraft", "art": "typ", "datentyp": "zahl", "einheit": "N", "wert": 500 },
            { "name": "schutzart", "art": "typ", "datentyp": "text", "wert": "IP 54" } ],
          "typen": [ { "name": "MA-30", "werte": { "p_betrieb": 2.5, "laufzeit": 30 } }, { "name": "MA-150", "werte": { "p_betrieb": 1.5, "laufzeit": 150 } } ],
          "koerper": [ { "id": "g", "art": "quader", "von": [-40, -40, 0], "bis": [40, 40, 120] } ], "anschluesse": [],
          "regeltechnik": {
            "elektrisch": { "speisung": [ { "art": "AC/DC", "spannung": "u" } ], "leistung": "p_betrieb", "schutzart": "schutzart" },
            "stellen": { "signale": [ { "art": "0-10V" }, { "art": "3-punkt" } ], "rueckmeldung": [ { "art": "2-10V" } ], "laufzeit": "laufzeit", "stellkraft": "kraft" },
            "kommunikation": [ { "protokoll": "modbus-rtu" } ],
            "datenpunkte": [ { "name": "Stellsignal", "signal": "AO", "art": "stellung", "einheit": "%", "weg": "0-10V" },
                             { "name": "Stellung", "signal": "AI", "art": "messung", "einheit": "%", "weg": "2-10V" } ] } }
        """;

    const string Ventil = """
        { "format": 1, "name": "Musterventil (fiktiv)", "kategorie": "Ventil",
          "parameter": [ { "name": "dn", "art": "typ", "datentyp": "dn", "ziel": "dn" } ],
          "typen": [ { "name": "DN 15", "werte": { "dn": 15 } }, { "name": "DN 20", "werte": { "dn": 20 } } ],
          "koerper": [ { "id": "k", "art": "quader", "von": [-40, -20, -20], "bis": [40, 20, 20] } ],
          "anschluesse": [ { "name": "ein", "medium": "*", "fluss": "beide", "art": "rohr", "dn": "dn", "lage": [-40, 0, 0], "richtung": [-1, 0, 0] },
                           { "name": "aus", "medium": "*", "fluss": "beide", "art": "rohr", "dn": "dn", "lage": [40, 0, 0], "richtung": [1, 0, 0] } ],
          "antriebe": [ { "familie": "Musterantrieb (fiktiv)", "typen": [ "MA-30" ], "fuer": [ "DN 15" ] } ],
          "anbau": { "lage": [0, 0, 20], "richtung": [0, 0, 1] } }
        """;

    static FamilieDef Def(string json, Action<JsonObject>? aendern = null)
    {
        var d = FamilieTestHilfe.JsonAus(json);
        aendern?.Invoke(d);
        return FamilieDef.Lesen(d, Kat);
    }

    [Fact]
    public void Antriebsfamilie_GueltigUndWerteJeTyp()
    {
        var f = Def(Antrieb);
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler));
        // Listeneinträge ohne quellen: Hinweis «unbelegt», kein Fehler
        Assert.Contains(f.Hinweise, h => h.Stelle == "regeltechnik.stellen.signale[1]" && h.Meldung.Contains("unbelegt"));

        var rt = FamRegeltechnik.Aufloesen(f, "MA-150", null)!;
        Assert.Equal(150, rt["stellen"]!["laufzeit"]!["wert"]!.GetValue<double>());
        Assert.Equal("s", rt["stellen"]!["laufzeit"]!["einheit"]!.GetValue<string>());
        Assert.Equal(1.5, rt["elektrisch"]!["leistung"]!["wert"]!.GetValue<double>());
        Assert.Equal(24, rt["elektrisch"]!["speisung"]![0]!["spannung"]!["wert"]!.GetValue<double>());
        Assert.Equal("IP 54", rt["elektrisch"]!["schutzart"]!["wert"]!.GetValue<string>());
        Assert.Equal("AC/DC", rt["elektrisch"]!["speisung"]![0]!["art"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("leistung", "kraft", "regeltechnik.elektrisch.leistung")]             // N statt W
    [InlineData("leistung", "schutzart", "regeltechnik.elektrisch.leistung")]         // Text statt Zahl
    [InlineData("leistung", "gibtsnicht", "regeltechnik.elektrisch.leistung")]        // kein Parameter
    [InlineData("leistung", 12, "regeltechnik.elektrisch.leistung")]                  // Wert statt Verweis
    public void Einzelwert_MussPassenderParameterSein(string feld, object wert, string stelle)
    {
        var f = Def(Antrieb, d => d["regeltechnik"]!["elektrisch"]![feld] = JsonValue.Create(wert));
        Assert.Contains(f.Fehler, x => x.Stelle == stelle);
    }

    [Fact]
    public void Auswahlwerte_UndDatenpunkteGegenKatalog()
    {
        var f = Def(Antrieb, d =>
        {
            d["regeltechnik"]!["stellen"]!["signale"]![0]!["art"] = "0..10 V";
            d["regeltechnik"]!["kommunikation"]![0]!["protokoll"] = "profibus-xy";
            d["regeltechnik"]!["datenpunkte"]![0]!["signal"] = "AX";
            d["regeltechnik"]!["datenpunkte"]![1]!["name"] = "Stellsignal";
        });
        Assert.Contains(f.Fehler, x => x.Stelle == "regeltechnik.stellen.signale[1].art");
        Assert.Contains(f.Fehler, x => x.Stelle == "regeltechnik.kommunikation[1].protokoll");
        Assert.Contains(f.Fehler, x => x.Stelle == "regeltechnik.datenpunkte[1].signal");
        Assert.Contains(f.Fehler, x => x.Stelle == "regeltechnik.datenpunkte[2].name");
    }

    [Fact]
    public void AntriebeUndAnbau_NurBeiArmaturenUndMitBekanntenTypen()
    {
        Assert.True(Def(Ventil).Gueltig, string.Join("; ", Def(Ventil).Fehler));
        var falsch = Def(Ventil, d => d["antriebe"]![0]!["fuer"] = new JsonArray("DN 99"));
        Assert.Contains(falsch.Fehler, x => x.Stelle == "antriebe[1].fuer");
        var heizkoerper = FamilieDef.Lesen(FamilieTestHilfe.Mini(mehr: """, "antriebe": [ { "familie": "X" } ]"""), Kat);
        Assert.Contains(heizkoerper.Fehler, x => x.Stelle == "antriebe");
    }

    [Fact]
    public void Quellen_MuessenBefundIdsSein()
    {
        var f = Def(Antrieb, d => d["regeltechnik"]!["kommunikation"]![0]!["quellen"] = new JsonArray("Seite 3"));
        Assert.Contains(f.Fehler, x => x.Stelle == "regeltechnik.kommunikation[1].quellen");
    }

    [Fact]
    public void Abfrage_VentilMitAntrieb_LiefertRegeltechnikDesAntriebs()
    {
        var p = Modul3Hilfe.MitSerie(false);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus(Antrieb) }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus(Ventil) }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = "Musterventil (fiktiv)", ["typ"] = "DN 15", ["punkt"] = new JsonArray(0, 0, 0), ["kennzeichen"] = "VE-1", ["system"] = "KW",
        }));
        var antrieb = p.Modell.DerKlasse(Familien.Klasse).Single(f => f.Name == "Musterantrieb (fiktiv)");
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "VE-1", ["bezuege"] = new JsonObject { ["antrieb"] = antrieb.Kennzeichen }, ["eigenschaften"] = new JsonObject { ["antriebstyp"] = "MA-30" },
        }));

        var o = (JsonObject)Tuer.Abfrage(p, "regeltechnik", new JsonObject { ["element"] = "VE-1" });
        Assert.Equal("antrieb", o["gilt"]!.GetValue<string>());
        Assert.True(o["antrieb"]!["passt"]!.GetValue<bool>());
        Assert.Equal(30, o["antrieb"]!["regeltechnik"]!["stellen"]!["laufzeit"]!["wert"]!.GetValue<double>());

        // MA-150 steht nicht in der Kombinationstabelle des Ventils
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "VE-1", ["eigenschaften"] = new JsonObject { ["antriebstyp"] = "MA-150" } }));
        o = (JsonObject)Tuer.Abfrage(p, "regeltechnik", new JsonObject { ["element"] = "VE-1" });
        Assert.False(o["antrieb"]!["passt"]!.GetValue<bool>());
    }
}
