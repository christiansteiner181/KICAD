// XR-Labor Spielwiese S1: GET /api/xr/labor/spielwiese im Labor-Server – der Katalog aus dem Ordner der Kataloge unverändert, ETag/304, eine Änderung der Datei gilt
// ohne Neustart (neuer ETag), ungültig bzw. fehlend 409 mit Stelle; über den Netzzugang der Labor-Brille nur mit Schlüssel, wie der Katalog des Labors.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Web;
using Hlks.Web.Tests;
using Hlks.Xr.Labor;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

public class SpielwieseEndpunktTests
{
    static LaborServer Server(string katalogOrdner, bool zugang = false) => new(zugang, dienste: s =>
    {
        var alt = (Einstellungen)s.Last(d => d.ServiceType == typeof(Einstellungen)).ImplementationInstance!;
        s.AddSingleton(alt with { KatalogOrdner = katalogOrdner });
    });

    static async Task<HttpResponseMessage> Holen(HttpClient c, string? etag = null)
    {
        using var r = new HttpRequestMessage(HttpMethod.Get, LaborSpielwiese.Pfad);
        if (etag is not null) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return await c.SendAsync(r);
    }

    [Fact]
    public async Task Katalog_unveraendert_mit_ETag_304_und_neuer_Fassung_ohne_Neustart()
    {
        var ordner = TestraumHilfe.Kopie();
        try
        {
            await using var s = Server(ordner);
            var datei = Path.Combine(ordner, SpielwieseKatalog.Datei);
            var a = await Holen(s.Lokal);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal("application/json", a.Content.Headers.ContentType?.MediaType);
            var text = await a.Content.ReadAsStringAsync();
            Assert.Equal(File.ReadAllText(datei, Encoding.UTF8), text);   // unverändert: die App liest denselben Text
            var k = SpielwieseKatalog.Lesen(text);
            Assert.Equal($"\"{k.Schluessel}\"", a.Headers.ETag?.ToString());
            Assert.Equal("no-cache", a.Headers.CacheControl?.ToString());
            Assert.Equal(6, k.Apps.Count);
            foreach (var etag in new[] { $"\"{k.Schluessel}\"", $"W/\"{k.Schluessel}\"", "*" })
            {
                var b = await Holen(s.Lokal, etag);
                Assert.Equal(HttpStatusCode.NotModified, b.StatusCode);
                Assert.Empty(await b.Content.ReadAsByteArrayAsync());
            }
            // Claude ändert eine App in der Datei: der nächste Abruf liefert sie mit neuem ETag, das alte ETag gilt nicht mehr
            var o = JsonNode.Parse(text)!.AsObject();
            o["apps"]![0]!["name"] = "Merkzettel";
            File.WriteAllText(datei, o.ToJsonString(), new UTF8Encoding(false));
            var c = await Holen(s.Lokal, $"\"{k.Schluessel}\"");
            Assert.Equal(HttpStatusCode.OK, c.StatusCode);
            var k2 = SpielwieseKatalog.Lesen(await c.Content.ReadAsStringAsync());
            Assert.Equal("Merkzettel", k2.Apps[0].Name);
            Assert.NotEqual(k.Schluessel, k2.Schluessel);
            Assert.Equal($"\"{k2.Schluessel}\"", c.Headers.ETag?.ToString());
            // nur GET
            Assert.NotEqual(HttpStatusCode.OK, (await s.Lokal.PostAsync(LaborSpielwiese.Pfad, new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        }
        finally { await LaborServer.Loeschen(ordner); }
    }

    [Fact]
    public async Task Ungueltig_oder_fehlend_409_mit_Stelle_und_Vorschlag()
    {
        var ordner = TestraumHilfe.Kopie();
        try
        {
            await using var s = Server(ordner);
            var datei = Path.Combine(ordner, SpielwieseKatalog.Datei);
            var o = JsonNode.Parse(File.ReadAllText(datei, Encoding.UTF8))!.AsObject();
            // eine Farbe, die es in xr_farben.json nicht gibt (der Labor-PC prüft gegen den Farbkatalog im selben Ordner)
            o["apps"]![2]!["farbe"] = "gibt-es-nicht";
            File.WriteAllText(datei, o.ToJsonString(), new UTF8Encoding(false));
            var (c1, j1) = await s.Get(LaborSpielwiese.Pfad);
            Assert.Equal(HttpStatusCode.Conflict, c1);
            Assert.Contains("apps[uhr].farbe", (string)j1!["fehler"]!);
            Assert.Contains("labor_spielwiese.json", (string)j1["vorschlag"]!);
            File.WriteAllText(datei, "{ kaputt", new UTF8Encoding(false));
            var (c2, j2) = await s.Get(LaborSpielwiese.Pfad);
            Assert.Equal(HttpStatusCode.Conflict, c2);
            Assert.Contains("kein gültiges JSON", (string)j2!["fehler"]!);
            File.Delete(datei);
            var (c3, j3) = await s.Get(LaborSpielwiese.Pfad);
            Assert.Equal(HttpStatusCode.Conflict, c3);
            Assert.Contains("fehlt", (string)j3!["fehler"]!);
        }
        finally { await LaborServer.Loeschen(ordner); }
    }

    [Fact]
    public async Task Ueber_den_Netzzugang_der_Labor_Brille_nur_mit_Schluessel()
    {
        await using var s = Server(TestraumHilfe.KatalogOrdner(), zugang: true);
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST3-SPIELWIESE");
        using (var ohne = XrServer.Netz(port, fp))
            Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync(LaborSpielwiese.Pfad)).StatusCode);
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        var a = await Holen(mit);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        var k = SpielwieseKatalog.Lesen(await a.Content.ReadAsStringAsync());
        Assert.Equal(["notizen", "bilder", "uhr", "liste", "dokument", "einstellungen"], k.Apps.Select(x => x.Kennung).ToArray());
        Assert.Equal(HttpStatusCode.NotModified, (await Holen(mit, $"\"{k.Schluessel}\"")).StatusCode);
    }
}
