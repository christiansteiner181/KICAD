using System.Text.Json.Nodes;

namespace Hlks.Web.Tests;

/// <summary>
/// Abbruchprotokoll der Gesten (Modul G, GestenProtokoll.cs): nur kleine Einträge { geste, dauerMs, grund, werte { Name: Zahl } }, nie Punkte, Bilder oder lange Texte;
/// Ablage als Zeile mit Zeit. Schreibt nur in einen eigenen Ordner unter %TEMP%.
/// </summary>
public class GestenProtokollTests : IDisposable
{
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_gestenprot_" + Guid.NewGuid().ToString("N")[..10]);

    public void Dispose() { try { Directory.Delete(ordner, true); } catch { /* egal */ } }

    [Fact]
    public void Gueltiger_Eintrag_wird_mit_Zeit_angehaengt()
    {
        var (e, f) = GestenProtokoll.Pruefen(JsonNode.Parse("""{ "geste": "drehen", "dauerMs": 1432.6, "grund": "hand öffnet sich", "werte": { "faust": 1.234567, "luecke": null } }"""));
        Assert.Null(f);
        GestenProtokoll.Anhaengen(e!, ordner, new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.FromHours(1)));
        GestenProtokoll.Anhaengen(e!, ordner);
        var zeilen = File.ReadAllLines(Path.Combine(ordner, "abbrueche.jsonl"));
        Assert.Equal(2, zeilen.Length);
        var z = JsonNode.Parse(zeilen[0])!.AsObject();
        Assert.StartsWith("2026-10-26T14:00:00", (string)z["zeit"]!);
        Assert.Equal("drehen", (string)z["geste"]!);
        Assert.Equal(1433, (double)z["dauerMs"]!);
        Assert.Equal(1.2346, (double)z["werte"]!["faust"]!);
    }

    [Theory]
    [InlineData("""[1, 2]""")]
    [InlineData("""{ "geste": "drehen", "bild": "data:image/png;base64,AAAA" }""")]
    [InlineData("""{ "grund": "x", "werte": { "punkte": [[0.1, 0.2]] } }""")]
    [InlineData("""{ "grund": "data:image/png;base64,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" }""")]
    [InlineData("""{ "dauerMs": -5 }""")]
    [InlineData("""{ "werte": { "a": 1, "b": 2, "c": 3, "d": 4, "e": 5, "f": 6, "g": 7, "h": 8, "i": 9, "j": 10, "k": 11, "l": 12, "m": 13 } }""")]
    public void Alles_andere_wird_abgelehnt(string json)
    {
        var (e, f) = GestenProtokoll.Pruefen(JsonNode.Parse(json));
        Assert.Null(e);
        Assert.False(string.IsNullOrEmpty(f));
    }
}
