using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern.Tests;

/// <summary>Register «Wie wird gerechnet» (Christian 03.11.2026): jeder Eintrag vollständig, Kennung eindeutig, Code-Stelle und Abfrage gibt es.</summary>
public class RechenregisterTests
{
    [Fact]
    public void Register_ist_vollstaendig_und_verweist_nur_auf_Vorhandenes()
    {
        var k = IfcHilfe.Kat;
        var liste = Rechenregister.Verfahren(k);
        Assert.NotEmpty(liste);
        var ids = liste.Select(v => (string?)v["id"]).ToList();
        Assert.All(ids, id => Assert.Matches("^[a-z0-9_]+\\.[a-z0-9_]+$", id!));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var abfragen = Abfragen.Liste.Select(a => a.Name).ToHashSet();
        var repo = Path.GetFullPath(Path.Combine(k.Ordner, ".."));
        foreach (var v in liste)
        {
            var id = (string)v["id"]!;
            foreach (var feld in new[] { "titel", "gebiet", "zweck" }) Assert.False(string.IsNullOrWhiteSpace((string?)v[feld]), $"{id}: {feld} fehlt");
            Assert.True(v["rechenweg"] is JsonArray { Count: > 0 }, $"{id}: rechenweg leer");
            foreach (var c in (v["code"] as JsonArray ?? []).Select(x => (string)x!))
                Assert.True(File.Exists(Path.Combine(repo, c.Split(' ')[0])), $"{id}: Datei {c} fehlt");
            if ((string?)v["abfrage"] is { Length: > 0 } ab && Regex.Match(ab, "^[a-z_]+") is { Success: true } m)
                Assert.True(abfragen.Contains(m.Value), $"{id}: Abfrage {m.Value} gibt es nicht");
        }
        // Grundlagen (Christian 03.11.2026: «zentrales normenverzeichniss»): jedes Verfahren nennt mindestens eine, nur bekannte; Kennungen eindeutig
        var grundlagen = Grundlagenverzeichnis.Alle(k);
        var gIds = grundlagen.Select(g => (string?)g["id"]).ToList();
        Assert.Equal(gIds.Count, gIds.Distinct().Count());
        Assert.All(grundlagen, g => Assert.Contains((string?)g["art"], Grundlagenverzeichnis.Arten));
        foreach (var v in liste)
        {
            var gl = (v["grundlagen"] as JsonArray ?? []).Select(x => (string?)x).ToList();
            Assert.True(gl.Count > 0, $"{v["id"]}: keine Grundlage");
            Assert.All(gl, x => Assert.True(gIds.Contains(x), $"{v["id"]}: Grundlage {x} fehlt im Verzeichnis"));
        }
        var sn = (JsonObject)Grundlagenverzeichnis.Abfrage(k, new JsonObject { ["id"] = "norm.sn592000" })["grundlage"]!;
        Assert.Contains(sn["verwendetIn"]!.AsArray(), x => (string?)x!["id"] == "sanitaer.abwasser_dimensionierung");
        // die Abfrage liefert Übersicht und Eintrag
        var ein = (JsonObject)Rechenregister.Abfrage(k, new JsonObject { ["id"] = ids[0] })["verfahren"]!;
        Assert.Equal(ids[0], (string?)ein["id"]);
        Assert.Equal(liste.Count, (int)Rechenregister.Abfrage(k, null)["anzahl"]!);
    }
}
