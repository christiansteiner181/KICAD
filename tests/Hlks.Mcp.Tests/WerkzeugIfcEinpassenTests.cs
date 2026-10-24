using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>IFC einpassen über MCP (entwurf/ifc-einpassen.md): ifc_uebernehmen gibt den Lagebericht im Ergebnis aus (Feld lage, auch im
/// Trockenlauf und bei Ablehnung; Nachtrag Hauptlauf 24.10.2026), ausrichtung = einpassen, ifc_pruefen mit einpassen = true.</summary>
public class WerkzeugIfcEinpassenTests
{
    [Fact]
    public void IfcUebernehmen_Trockenlauf_ErgebnisHatDenLagebericht()
    {
        var w = new Werkzeuge(Beispiel());
        var n = Json(w.IfcUebernehmen(Testdaten("minimal_ifc4.ifc"), trockenlauf: true));
        Assert.True(n["angenommen"]!.GetValue<bool>());
        Assert.Equal("erster", n["lage"]!["bewertung"]!.GetValue<string>());
        Assert.Equal("erste Datei", n["lage"]!["ausrichtung"]!.GetValue<string>());
        Assert.NotNull(n["lage"]!["bezug"]);
        Assert.NotNull(n["aenderungen"]);   // bestehende Felder bleiben
    }

    [Fact]
    public void IfcUebernehmen_Einpassen_LageberichtMitEinpassen_AuchBeiAblehnung()
    {
        var w = new Werkzeuge(Beispiel());
        var n = Json(w.IfcUebernehmen(Testdaten("minimal_ifc4.ifc"), ausrichtung: "einpassen", trockenlauf: true));
        var lage = n["lage"] ?? throw new Xunit.Sdk.XunitException("lage fehlt: " + n.ToJsonString());
        Assert.Equal("einpassen", lage["ausrichtung"]!.GetValue<string>());
        var ergebnis = lage["einpassen"]!["ergebnis"]!.GetValue<string>();
        Assert.Contains(ergebnis, new[] { "eindeutig", "unsicher", "keine Merkmale" });
        // angenommen nur bei eindeutiger Lage, sonst abgelehnt mit Vorschlag
        Assert.Equal(ergebnis == "eindeutig", n["angenommen"]!.GetValue<bool>());
        if (ergebnis != "eindeutig") Assert.Contains("bezug", n["vorschlag"]!.GetValue<string>());
    }

    [Fact]
    public void IfcPruefen_EinpassenTrue_ZeigtDenLagebericht()
    {
        var w = new Werkzeuge(Beispiel());
        var ohne = Json(w.IfcPruefen(Testdaten("minimal_ifc4.ifc")));
        var mit = Json(w.IfcPruefen(Testdaten("minimal_ifc4.ifc"), einpassen: true));
        Assert.NotNull(mit["lage"]!["einpassen"]!["ergebnis"]);
        Assert.NotNull(mit["lage"]!["einpassen"]!["merkmale"]!["datei"]);
        Assert.Equal(ohne["datei"]!.GetValue<string>(), mit["datei"]!.GetValue<string>());
    }
}
