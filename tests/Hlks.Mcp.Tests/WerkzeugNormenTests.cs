using System.Diagnostics;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Tests.Gemeinsam;
using ModelContextProtocol.Protocol;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Modul 3c, Teil K: MCP-Werkzeuge normen_suchen, norm_lesen (über HLKS_NORMEN – darum allein) mit den eigenen Test-PDF
/// (tests/daten/normen, erfundener Text). Nie W:.</summary>
[CollectionDefinition("NormenUmgebungMcp", DisableParallelization = true)]
public sealed class NormenUmgebungMcpSammlung { }

[Collection("NormenUmgebungMcp")]
public sealed class WerkzeugNormenTests : IDisposable
{
    readonly string wurzel = NormenTestdaten.Kopie();
    readonly TestUmgebung umgebung;

    public WerkzeugNormenTests() { umgebung = TestUmgebung.Setzen(("HLKS_NORMEN", wurzel)); }

    public void Dispose()
    {
        umgebung.Dispose();
        NormenTestdaten.Loeschen(wurzel);
    }

    [Fact]
    public void NormenSuchen_TrefferMitNormAusgabeDateiSeiteZifferAuszug()
    {
        var w = new Werkzeuge(Beispiel());
        var j = Json(w.NormenSuchen("Mindestgefälle Prüfleitung"));
        Assert.Equal(wurzel, j["quelle"]!.GetValue<string>());
        var t = j["treffer"]!.AsArray()[0]!;
        Assert.Equal("SIA 9999:2026", t["norm"]!.GetValue<string>());
        Assert.Equal("2026", t["ausgabe"]!.GetValue<string>());
        Assert.Equal(Path.Combine("SIA_Normen", "9999_2026_d.pdf"), t["datei"]!.GetValue<string>());
        Assert.Equal(3, t["seite"]!.GetValue<int>());
        Assert.Equal("4.2.1", t["ziffer"]!.GetValue<string>());
        var auszug = t["auszug"]!.GetValue<string>();
        Assert.Contains("7 Promille", auszug);
        Assert.True(auszug.Length <= 600);
        Assert.All(j["treffer"]!.AsArray(), x => Assert.True(x!["auszug"]!.GetValue<string>().Length <= 600));
        Assert.Contains("norm_lesen", j["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void NormenSuchen_Reihe_UndLeereSuche()
    {
        var w = new Werkzeuge(Beispiel());
        var j = Json(w.NormenSuchen("Spülung Prüfleitung", reihe: "Merkblatt"));
        Assert.All(j["treffer"]!.AsArray(), t => Assert.Equal("Merkblatt", t!["reihe"]!.GetValue<string>()));
        Assert.NotEmpty(j["treffer"]!.AsArray());
        Assert.True(Json(w.NormenSuchen(" ")).AsObject().ContainsKey("fehler"));
    }

    [Fact]
    public void NormLesen_TextDerSeiten_HoechstensDrei_UnbekanntIstFehler()
    {
        var w = new Werkzeuge(Beispiel());
        var text = w.NormLesen(Path.Combine("SIA_Normen", "9999_2026_d.pdf"), 3);
        Assert.Contains("=== Seite 3 ===", text);
        Assert.Contains("Mindestgefälle der Prüfleitung", text);
        Assert.Contains("lizenziert", text);
        var viel = w.NormLesen("SIA 9999:2026", 1, 4);
        Assert.DoesNotContain("=== Seite 4 ===", viel);
        Assert.Contains("höchstens 3 Seiten", viel);
        Assert.True(Json(w.NormLesen(@"C:\Windows\win.ini", 1)).AsObject().ContainsKey("fehler"));
        Assert.True(Json(w.NormLesen("SIA 9999:2026", 99)).AsObject().ContainsKey("fehler"));
    }
}

/// <summary>Modul 3c, Teil K: kontext_bild (Bild der Ansicht zur Frage) – in der Werkzeugklasse und über das echte MCP-Protokoll mit
/// --kontext-bild und --normen (ohne Umgebungsvariable).</summary>
public sealed class KontextBildTests : IDisposable
{
    // 1×1-PNG (gültig) und der Kopf eines JPEG (für die Erkennung genügt er)
    public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];
    readonly string ordner = Path.Combine(Path.GetTempPath(), "hlks_kbild_" + Guid.NewGuid().ToString("N")[..8]);

    public KontextBildTests() { Directory.CreateDirectory(ordner); }
    public void Dispose() => Aufraeumen(ordner);

    string Datei(string name, byte[] inhalt) { var p = Path.Combine(ordner, name); File.WriteAllBytes(p, inhalt); return p; }

    [Fact]
    public void MitPng_LiefertBildinhalt()
    {
        var r = new SitzungWerkzeuge(new KontextBild(Datei("a.png", Png))).KontextBildZeigen();
        Assert.NotEqual(true, r.IsError);
        var bild = Assert.Single(r.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", bild.MimeType);
        Assert.Contains("Bild der aktiven Ansicht", r.Content.OfType<TextContentBlock>().First().Text);
    }

    [Fact]
    public void MitJpeg_LiefertBildinhalt()
    {
        var r = new SitzungWerkzeuge(new KontextBild(Datei("a.jpg", Jpeg))).KontextBildZeigen();
        Assert.Equal("image/jpeg", Assert.Single(r.Content.OfType<ImageContentBlock>()).MimeType);
    }

    [Fact]
    public void OhneBild_Hinweis_KeinFehler()
    {
        foreach (var b in new[] { new KontextBild(null), new KontextBild(Path.Combine(ordner, "fehlt.png")) })
        {
            var r = new SitzungWerkzeuge(b).KontextBildZeigen();
            Assert.NotEqual(true, r.IsError);
            Assert.Empty(r.Content.OfType<ImageContentBlock>());
            Assert.StartsWith("Kein Bild zur Frage", r.Content.OfType<TextContentBlock>().Single().Text);
        }
    }

    [Fact]
    public void OhneProjektkontext_MitInternet_NieEinBild()
    {
        // Modul 3c, Entscheid Hauptlauf 20.10.2026: mit Internet kein Projektkontext (MCP-Server mit --ohne-projektkontext)
        var r = new SitzungWerkzeuge(new KontextBild(Datei("a.png", Png), OhneProjektkontext: true)).KontextBildZeigen();
        Assert.NotEqual(true, r.IsError);
        Assert.Empty(r.Content.OfType<ImageContentBlock>());
        Assert.StartsWith("Kein Bild – mit Internet ohne Projektkontext", r.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    public void KeinBildformat_Fehler()
    {
        var r = new SitzungWerkzeuge(new KontextBild(Datei("a.png", "kein Bild"u8.ToArray()))).KontextBildZeigen();
        Assert.True(r.IsError);
        Assert.Contains("weder PNG noch JPEG", r.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    public void Protokoll_KontextBildUndNormenUeberStartargumente()
    {
        var (wurzel, projekt) = TempProjektOrdner();
        var normen = NormenTestdaten.Kopie();
        Process? server = null;
        try
        {
            Assert.Equal(0, Lauf("--beispiel", projekt).Code);
            var bild = Datei("ansicht.png", Png);
            server = ServerStarten("--projekt", projekt, "--akteur", "KI", "--art", "ki", "--geraet", "test", "--kontext-bild", bild, "--normen", normen);
            _ = server.StandardError.ReadToEndAsync();
            var nr = 0;
            JsonObject Anfrage(string methode, JsonObject par)
            {
                var id = ++nr;
                server.StandardInput.WriteLine(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = methode, ["params"] = par }.ToJsonString());
                server.StandardInput.Flush();
                while (true)
                {
                    var lesen = server.StandardOutput.ReadLineAsync();
                    if (!lesen.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("Keine Antwort auf " + methode);
                    var o = JsonNode.Parse(lesen.Result ?? throw new EndOfStreamException())!.AsObject();
                    if (o["id"]?.GetValue<int>() == id) return o;
                }
            }
            Anfrage("initialize", new JsonObject { ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(), ["clientInfo"] = new JsonObject { ["name"] = "t", ["version"] = "1" } });
            server.StandardInput.WriteLine(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }.ToJsonString());
            server.StandardInput.Flush();

            var namen = Anfrage("tools/list", new JsonObject())["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
            Assert.Contains("kontext_bild", namen);
            Assert.Contains("normen_suchen", namen);
            Assert.Contains("norm_lesen", namen);

            var k = Anfrage("tools/call", new JsonObject { ["name"] = "kontext_bild", ["arguments"] = new JsonObject() })["result"]!;
            var img = k["content"]!.AsArray().OfType<JsonObject>().Single(c => c["type"]!.GetValue<string>() == "image");
            Assert.Equal("image/png", img["mimeType"]!.GetValue<string>());
            Assert.Equal(Png, Convert.FromBase64String(img["data"]!.GetValue<string>()));

            var n = Anfrage("tools/call", new JsonObject { ["name"] = "normen_suchen", ["arguments"] = new JsonObject { ["frage"] = "Mindestgefälle Prüfleitung" } })["result"]!;
            var antwort = JsonNode.Parse(n["content"]!.AsArray()[0]!["text"]!.GetValue<string>())!;
            Assert.Equal(normen, antwort["quelle"]!.GetValue<string>());
            Assert.Equal("SIA 9999:2026", antwort["treffer"]![0]!["norm"]!.GetValue<string>());
        }
        finally
        {
            if (server is not null) { try { server.StandardInput.Close(); if (!server.WaitForExit(10000)) server.Kill(true); } catch { } server.Dispose(); }
            Aufraeumen(wurzel);
            NormenTestdaten.Loeschen(normen);
        }
    }
}

/// <summary>Kopie der Test-PDF (im Ausgabeordner unter normen\, aus tests/daten/normen) in einen eigenen Ordner unter %TEMP%.</summary>
static class NormenTestdaten
{
    public static string Kopie()
    {
        var quelle = Path.Combine(AppContext.BaseDirectory, "normen");
        if (!Directory.Exists(quelle)) throw new DirectoryNotFoundException("Test-PDF fehlen: " + quelle);
        var ziel = Path.Combine(Path.GetTempPath(), "hlks_normen_" + Guid.NewGuid().ToString("N")[..10]);
        foreach (var f in Directory.GetFiles(quelle, "*.pdf", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(quelle, f);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(ziel, rel))!);
            File.Copy(f, Path.Combine(ziel, rel));
        }
        return ziel;
    }

    public static void Loeschen(string ordner) => Aufraeumen(ordner);

    /// <summary>Für die Abdeckung aller Abfragen (WerkzeugTests): HLKS_NORMEN nur während des Aufrufs auf eine Kopie der Test-PDF. Unschädlich
    /// im parallelen Lauf: nur die Normensuche liest die Variable, und deren eigene Tests laufen in einer Sammlung für sich.</summary>
    public static string Mit(Func<string> aufruf)
    {
        var ordner = Kopie();
        try
        {
            using var u = TestUmgebung.Setzen(("HLKS_NORMEN", ordner));
            return aufruf();
        }
        finally { Loeschen(ordner); }
    }
}
