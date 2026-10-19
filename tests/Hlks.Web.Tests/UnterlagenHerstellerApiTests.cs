using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Hlks.Web.Tests;

/// <summary>REST für Herstellerunterlagen (Modul 2, B2): Hochladen der neuen Endungen (csv, stl, obj, txt, xlsx, step …), Einfügen mit zweck «familie»,
/// unterlagen_lesen, befunde und unterlage_bild (PNG als Base64) über /api/abfrage und /api/befehl.</summary>
public class UnterlagenHerstellerApiTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_herst_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, object? koerper = null)
        {
            var text = koerper switch { null => "{}", JsonNode n => n.ToJsonString(), _ => Hlks.Kern.Json.Text(koerper) };
            var a = await Http.PostAsync(pfad, new StringContent(text, Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Hochladen(string name, byte[] bytes)
        {
            var inhalt = new ByteArrayContent(bytes);
            inhalt.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var a = await Http.PostAsync("/api/unterlagen?name=" + Uri.EscapeDataString(name), inhalt);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public void Dispose()
        {
            Http.Dispose(); fabrik.Dispose(); basis.Dispose();
            try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    static byte[] Pdf()
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(300); s.Height = XUnit.FromPoint(200);
        using (var g = XGraphics.FromPdfPage(s)) g.DrawRectangle(new XPen(XColors.Black, 2), 30, 30, 120, 80);
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    const string Csv = "Typ;DN;L [mm]\r\nV-15;15;67\r\nV-20;20;78\r\n";
    const string Stl = "solid d\nfacet normal 0 0 1\n outer loop\n  vertex 0 0 0\n  vertex 10 0 0\n  vertex 0 20 0\n endloop\nendfacet\nendsolid d\n";
    const string Typkatalog = ",Laenge##LENGTH##MILLIMETERS,DN##NUMBER##\r\nV-15,67,15\r\n";

    [Fact]
    public async Task Hochladen_Einfuegen_Lesen_Befunde_und_Bild_ueber_REST()
    {
        using var s = new Server();
        var (code, _) = await s.Post("/api/projekt/anlegen", new { ordner = s.Ordner, name = "Hersteller-Test", beispiel = true });
        Assert.Equal(HttpStatusCode.OK, code);

        // die neuen Endungen lassen sich hochladen (keine Beschränkung auf die Arten der Grundlagen)
        var namen = new Dictionary<string, byte[]>
        {
            ["typen.csv"] = Encoding.UTF8.GetBytes(Csv), ["dreieck.stl"] = Encoding.UTF8.GetBytes(Stl), ["ventil.txt"] = Encoding.UTF8.GetBytes(Typkatalog),
            ["modell.obj"] = Encoding.UTF8.GetBytes("v 0 0 0\nv 10 0 0\nv 0 10 0\nf 1 2 3\n"), ["modell.step"] = Encoding.UTF8.GetBytes("ISO-10303-21;\nEND-ISO-10303-21;\n"),
            ["blatt.pdf"] = Pdf(),
        };
        var abgelegt = new Dictionary<string, string>();
        foreach (var (name, bytes) in namen)
        {
            var (c, j) = await s.Hochladen(name, bytes);
            Assert.Equal(HttpStatusCode.OK, c);
            Assert.EndsWith("_" + name, j!["name"]!.GetValue<string>());
            abgelegt[name] = j["name"]!.GetValue<string>();
        }
        // Einfügen als Herstellerunterlage ohne Geschoss und Lage
        foreach (var name in namen.Keys)
        {
            var par = new JsonObject { ["datei"] = abgelegt[name], ["zweck"] = "familie" };
            if (name == "ventil.txt") par["art"] = "typkatalog";
            var (c, j) = await s.Post("/api/befehl/unterlage_einfuegen", par);
            Assert.Equal(HttpStatusCode.OK, c);
            Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        }
        // ohne zweck: die Herstellerart wird mit Vorschlag abgelehnt (400 nie, fachlicher Fehler als Antwort)
        var (cn, jn) = await s.Post("/api/befehl/unterlage_einfuegen", new { datei = abgelegt["dreieck.stl"], geschoss = "EG" });
        Assert.Equal(HttpStatusCode.OK, cn);
        Assert.False(jn!["angenommen"]!.GetValue<bool>());
        Assert.Contains("zweck", jn["vorschlag"]!.GetValue<string>());

        // unterlagen_lesen: mehrere Arten in einer Antwort, ein Fehler je Datei bricht nicht ab
        var (cl, lesen) = await s.Post("/api/abfrage/unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("typen.csv", "dreieck.stl", "ventil.txt", "modell.obj", "gibts-nicht.pdf", "modell.step") });
        Assert.Equal(HttpStatusCode.OK, cl);
        var us = lesen!["unterlagen"]!.AsArray();
        Assert.Equal(["csv", "stl", "typkatalog", "obj"], us.Take(4).Select(x => x!["art"]!.GetValue<string>()).ToArray());
        Assert.Equal(2, us[0]!["tabellen"]![0]!["zeilen"]!.GetValue<int>());
        Assert.Equal(1, us[1]!["form3d"]![0]!["anzahlDreiecke"]!.GetValue<int>());
        Assert.Equal(1, us[2]!["jeArt"]!["typ"]!.GetValue<int>());
        Assert.NotNull(us[4]!["fehler"]);                     // gibt es nicht
        Assert.True(us[5]!["fehler"] is not null || us[5]!["hinweise"] is not null);   // STEP: Fehler oder Hinweis dieser Datei, nie ein Abbruch

        // befunde: gefiltert
        var (cb, b) = await s.Post("/api/abfrage/befunde", new JsonObject { ["unterlage"] = "typen.csv", ["art"] = "zelle", ["text"] = "78" });
        Assert.Equal(HttpStatusCode.OK, cb);
        Assert.Equal(1, b!["treffer"]!.GetValue<int>());
        Assert.Equal(78, b["befunde"]![0]!["wert"]!.GetValue<double>());
        Assert.Equal("mm", b["befunde"]![0]!["einheit"]!.GetValue<string>());

        // unterlage_bild: PNG als Base64
        var (ci, bild) = await s.Post("/api/abfrage/unterlage_bild", new JsonObject { ["unterlage"] = "blatt.pdf", ["breite"] = 300 });
        Assert.Equal(HttpStatusCode.OK, ci);
        Assert.Equal("image/png", bild!["mime"]!.GetValue<string>());
        var png = Convert.FromBase64String(bild["base64"]!.GetValue<string>());
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Equal(300, bild["breite"]!.GetValue<int>());
        Assert.Equal(200, bild["hoehe"]!.GetValue<int>());

        // fachlicher Fehler: 400 mit Text, nie 500
        var (cf, f) = await s.Post("/api/abfrage/unterlage_bild", new JsonObject { ["unterlage"] = "typen.csv" });
        Assert.Equal(HttpStatusCode.BadRequest, cf);
        Assert.Contains("befunde", f!["vorschlag"]!.GetValue<string>());
    }
}
