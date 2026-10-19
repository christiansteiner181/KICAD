using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Räume aus IFC (Modul 1b, B1): aus IfcSpace (Nummer, Name, lichte Höhe, Kennzeichen) und – wenn die Datei keine
/// hat (Archicad) – automatisch aus den übernommenen Wänden je Geschoss.</summary>
public class IfcRaeumeTests
{
    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void IfcSpace_NummerNameHoeheKennzeichenUmriss(string schema)
    {
        var (p, _) = IfcArchitekturTests.Uebernommen(schema, mitRaum: true);
        var r = Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.Equal("1.01", r.Kennzeichen);
        Assert.Equal("1.01", r.Text("nummer"));
        Assert.Equal("Wohnen", r.Name);
        Assert.Equal("Wohnen", r.Text("nutzung"));
        Assert.Contains("ObjectType", r.Eigenschaft("nutzung")!.Herkunft.Quelle);
        Assert.Equal(2500, r.Zahl("lichteHoehe"));
        Assert.Equal("ifc", r.Eigenschaft("lichteHoehe")!.Herkunft.Art);
        Assert.Equal(5600.0 * 3600, r.Geometrie!.Flaeche(), 0);
        Assert.All(r.Geometrie.Punkte, q => Assert.Equal(0, q[2]));
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG").Id, r.Geschoss);
        Assert.Equal(22, r.Kennungen!["ifc"].Length);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void OhneIfcSpace_RaumAusDenWaenden_OhneNummer_MitHinweis(string schema)
    {
        var (p, e) = IfcArchitekturTests.Uebernommen(schema);
        var r = Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.Equal("R-1", r.Kennzeichen);
        Assert.Null(r.Text("nummer"));
        Assert.Null(r.Name);
        Assert.Equal(5600.0 * 3600, r.Geometrie!.Flaeche(), 0);   // Innenfläche der vier Wände
        Assert.All(r.Geometrie.Punkte, q => Assert.Equal(0, q[2]));
        Assert.StartsWith("raum:", r.Kennungen!["ifc"]);
        Assert.EndsWith(":3000,2000", r.Kennungen["ifc"]);           // Schwerpunkt auf 100 mm
        Assert.Equal(p.Modell.DerKlasse("Unterlage").Single().Id, r.Kennungen["quelle"]);
        Assert.Equal("berechnung", r.Angelegt!.Art);
        Assert.Contains(e.Hinweise, h => h.Contains("Nummer und Name ergänzen"));
        // HLKS-Elemente bekommen ihren Raum aus der Lage: der Raum enthält die Mitte
        Assert.True(r.Geometrie.Enthaelt(3000, 2000));
    }

    [Fact]
    public void RaeumeAusWaenden_NeueFassung_GleicherRaum_NummerBleibt()
    {
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei("IFC2X3", false);
        Ok(Uebernehmen(p, datei));
        var r = p.Modell.DerKlasse("Raum").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = r.Kennzeichen, ["eigenschaften"] = new JsonObject { ["nummer"] = "0.01" } }));
        var zweites = Ok(Uebernehmen(p, datei));
        Assert.Null(zweites.PaketId);   // nichts geändert: der Raum gleicht über seine Kennung ab
        var nachher = p.Modell.DerKlasse("Raum").Single();
        Assert.Equal(r.Id, nachher.Id);
        Assert.Equal("0.01", nachher.Text("nummer"));
    }

    [Fact]
    public void RaeumeAusWaenden_FremderRaumAnDerStelle_KeinZweiter()
    {
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei("IFC4", false);
        Ok(Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("architektur") }));
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = new JsonArray(1500, 1500), ["nummer"] = "0.01" }));
        var e = Ok(Uebernehmen(p, datei));
        var r = Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.Equal("0.01", r.Text("nummer"));
        Assert.Contains(e.Hinweise, h => h.Contains("schon ein Raum"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void IfcSpaceNachRaumAusWaenden_UebernimmtIhn_IdBleibt(string schema)
    {
        // Abnahme 1b: erst das Architekten-IFC ohne Räume (Raum aus Wänden), dann eine Datei mit IfcSpace am selben Ort –
        // es darf kein zweiter Raum entstehen; der echte Raum übernimmt Id und Bezüge des abgeleiteten
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei(schema, false)));
        var abgeleitet = Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.StartsWith("raum:", abgeleitet.Kennungen!["ifc"]);
        var mitRaum = IfcArchitekturTestdatei.Datei(schema, true);
        Assert.NotEqual(Path.GetFileName(IfcArchitekturTestdatei.Datei(schema, false)), Path.GetFileName(mitRaum));
        var e = Ok(Uebernehmen(p, mitRaum, new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        var r = Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.Equal(abgeleitet.Id, r.Id);
        Assert.Equal("1.01", r.Kennzeichen);
        Assert.Equal("1.01", r.Text("nummer"));
        Assert.Equal("Wohnen", r.Name);
        Assert.Equal(22, r.Kennungen!["ifc"].Length);
        Assert.Contains(e.Hinweise, h => h.Contains("1 aus Wänden gebildete Räume durch Räume aus dem IFC ersetzt"));
        // dieselbe Datei nochmals: nichts mehr zu tun
        Assert.Null(Ok(Uebernehmen(p, mitRaum, new JsonObject { ["bereiche"] = new JsonArray("raeume") })).PaketId);
        // und das Architekten-IFC erneut: kein Raum aus Wänden mehr an dieser Stelle
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei(schema, false)));
        Assert.Equal(r.Id, Assert.Single(p.Modell.DerKlasse("Raum")).Id);
    }

    [Fact]
    public void IfcSpace_AbgeleiteterRaumInKicadGeaendert_BleibtUnangetastet()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var abgeleitet = p.Modell.DerKlasse("Raum").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = abgeleitet.Kennzeichen, ["eigenschaften"] = new JsonObject { ["nummer"] = "0.01" } }));
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", true), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        Assert.Equal("0.01", p.Modell.Finde(abgeleitet.Id)!.Text("nummer"));
        Assert.Equal(2, p.Modell.DerKlasse("Raum").Count());
    }

    [Fact]
    public void NurBereichRaeume_WaendeSchonUebernommen_RaeumeAusDiesenWaenden()
    {
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei("IFC4", false);
        Ok(Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("architektur") }));
        Assert.Empty(p.Modell.DerKlasse("Raum"));
        var e = Ok(Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        Assert.Single(p.Modell.DerKlasse("Raum"));
        Assert.Contains(e.Hinweise, h => h.Contains("schon übernommenen Wänden"));
        Assert.Equal(6, p.Modell.DerKlasse("Wand").Count());   // Wände bleiben (andere Klassen werden nicht gelöscht)
    }

    [Fact]
    public void NurBereichRaeume_OhneWaende_Hinweis()
    {
        var p = Leer();
        var e = Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC2X3", false), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        Assert.Empty(p.Modell.DerKlasse("Raum"));
        Assert.Contains(e.Hinweise, h => h.Contains("keine Räume (IfcSpace)") && h.Contains("raum_erkennen"));
    }

    [Fact]
    public void MinimalDatei_RaumAusIfcSpace_UndWand()
    {
        // Testdatei der Phase A (minimal_ifc4/2x3): Raum 1.01 «Wohnen» 4 × 3 m, Wand 5000 × 200
        foreach (var datei in new[] { "minimal_ifc4.ifc", "minimal_ifc2x3.ifc" })
        {
            var p = Leer();
            Ok(Uebernehmen(p, Testdaten(datei)));
            var r = Assert.Single(p.Modell.DerKlasse("Raum"));
            Assert.Equal("1.01", r.Kennzeichen);
            Assert.Equal("Wohnen", r.Name);
            Assert.Equal(12e6, r.Geometrie!.Flaeche(), 0);
            Assert.Equal(2500, r.Zahl("lichteHoehe"));
            var w = Assert.Single(p.Modell.DerKlasse("Wand"));
            Assert.Equal(200, w.Geometrie!.Dicke);
            Assert.Equal(2600, w.Geometrie.Hoehe);
            Assert.Equal(0, w.Geometrie.Punkte[0][1], 3);   // Körper symmetrisch zur Achse
            Assert.Equal(5000, Math.Abs(w.Geometrie.Punkte[1][0] - w.Geometrie.Punkte[0][0]), 3);
        }
    }
}
