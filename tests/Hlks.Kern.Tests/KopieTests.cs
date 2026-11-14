using System.Text.Json;
namespace Hlks.Kern.Tests;

/// <summary>Frage 46 (14.11.2026): Element.Kopie ohne JSON-Umweg muss genau dasselbe liefern wie Serialisieren und Lesen – sonst gingen Felder beim
/// Kopieren der Arbeitskopie je Befehl verloren.</summary>
public class KopieTests
{
    [Fact]
    public void Kopie_GleichWieJsonUmweg_UndUnabhaengig()
    {
        var p = Beispielprojekt.Erzeugen(ArchitekturHilfe.Kat, null);
        Assert.NotEmpty(p.Modell.Elemente);
        foreach (var e in p.Modell.Elemente)
            Assert.Equal(JsonSerializer.Serialize(e.KopieUeberJson(), Json.Optionen), JsonSerializer.Serialize(e.Kopie(), Json.Optionen));
        var w = p.Modell.Elemente.First(e => e.Eigenschaften.Count > 0 && e.Geometrie is { Punkte.Count: > 0 });
        var k = w.Kopie();
        k.Geometrie!.Punkte[0][0] += 1;
        k.Eigenschaften.First().Value.Inhalt = null;
        k.Bezuege["x"] = "y";
        Assert.NotEqual(JsonSerializer.Serialize(w, Json.Optionen), JsonSerializer.Serialize(k, Json.Optionen));   // Original unberührt
    }
}
