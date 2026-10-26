using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using HkXr.Logik.Ebenen;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App P4b (Modul XR, Schnittstelle für Fachmodule): GET /api/xr/ebenen und GET /api/xr/ebene/{name} (XrEbenenEndpunkte.cs) mit dem Beispielprojekt «MFH Musterweg»:
/// Liste, Format der Probe-Ebene «fliessrichtung» (gegen die Leitungen des Modells gerechnet), ETag und 304, neuer Schlüssel nach einer Änderung, Fehler, Zugang im
/// Büronetz mit Schlüssel, Lesen mit dem Parser der App (HkXr.Logik.Ebenen) und KI-Einordnung.
/// </summary>
public class XrEbenenApiTests
{
    static async Task<JsonObject> Json(HttpResponseMessage a)
    {
        var t = await a.Content.ReadAsStringAsync();
        Assert.True(a.StatusCode == HttpStatusCode.OK, $"{(int)a.StatusCode}: {t}");
        return JsonNode.Parse(t)!.AsObject();
    }

    static async Task<HttpResponseMessage> Holen(HttpClient c, string pfad, string? etag = null)
    {
        var r = new HttpRequestMessage(HttpMethod.Get, pfad);
        if (etag is not null) r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        return await c.SendAsync(r);
    }

    [Fact]
    public async Task Liste_nennt_die_Probe_Ebene_mit_Adresse_Schluessel_und_Umfang()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        var l = await Json(await s.Lokal.GetAsync("/api/xr/ebenen"));
        Assert.Equal(1, (int)l["fassung"]!);
        Assert.False(string.IsNullOrEmpty((string?)l["projekt"]));
        var e = Assert.Single(l["ebenen"]!.AsArray())!;
        Assert.Equal("fliessrichtung", (string?)e["name"]);
        Assert.Equal("Fliessrichtung", (string?)e["titel"]);
        Assert.Equal("/api/xr/ebene/fliessrichtung", (string?)e["url"]);
        Assert.Matches("^[0-9a-f]{24}$", (string?)e["schluessel"]);
        Assert.True((int)e["elemente"]! > 0);
        // die App liest die Liste
        var liste = EbenenListe.Lesen(l.ToJsonString());
        Assert.Equal("fliessrichtung", Assert.Single(liste.Ebenen).Name);
    }

    [Fact]
    public async Task Fliessrichtung_Pfeile_und_Partikel_entlang_der_Leitungen_in_der_Farbe_des_Mediums()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        var e = await Json(await s.Lokal.GetAsync("/api/xr/ebene/fliessrichtung"));
        Assert.Equal("hlks-xr-ebene", (string?)e["art"]);
        Assert.Equal(1, (int)e["fassung"]!);
        Assert.Equal("mm", (string?)e["einheit"]);
        Assert.Contains("Fliessrichtung von anfang nach ende", (string?)e["quelle"]);
        var elemente = e["elemente"]!.AsArray().Select(x => x!.AsObject()).ToList();
        var geschosse = (await Json(await s.Lokal.GetAsync("/api/xr/geschosse")))["geschosse"]!.AsArray().Select(g => (string)g!["id"]!).ToHashSet();
        var dienst = s.Dienste.GetRequiredService<ProjektDienst>();
        var leitungen = await dienst.Mit(p => p.Modell.Elemente.Where(x => HlksEinordnung.Art(p.Katalog, x) == HlksArt.Leitung && HlksEinordnung.Punkte(x).Count >= 2)
            .ToDictionary(x => x.Id, x => (Punkte: HlksEinordnung.Punkte(x), Farbe: HlksEinordnung.Farbe(HlksEinordnung.Medium(p, x)), Radius: XrEbenen.Radius(x))));
        Assert.NotEmpty(leitungen);
        // je Leitung genau ein Partikel-Element, entlang ihrer Punkte (versetzt)
        Assert.Equal(leitungen.Count, elemente.Count(x => (string?)x["typ"] == "partikel"));
        int pfeile = 0;
        foreach (var el in elemente)
        {
            var id = (string)el["element"]!;
            Assert.True(leitungen.ContainsKey(id), $"Element {id} ist keine Leitung");
            Assert.Contains((string)el["geschoss"]!, geschosse);
            Assert.Matches("^#[0-9a-f]{6}$", (string?)el["farbe"]);
            var l = leitungen[id];
            if (l.Farbe is { Length: 7 } f) Assert.Equal(f.ToLowerInvariant(), (string?)el["farbe"]);
            var pk = el["punkte"]!.AsArray().Select(q => q!.AsArray().Select(c => (double)c!).ToArray()).ToList();
            if ((string?)el["typ"] != "pfeil") continue;
            pfeile++;
            Assert.Equal(2, pk.Count);
            // Richtung des Pfeils = Richtung einer Strecke der Leitung von anfang nach ende (gleiche Richtung, Skalarprodukt der Einheitsvektoren ≈ 1)
            double[] d = [pk[1][0] - pk[0][0], pk[1][1] - pk[0][1], pk[1][2] - pk[0][2]];
            var dl = Math.Sqrt(d.Sum(x => x * x));
            Assert.InRange(dl, 50, XrEbenen.PfeilMaxMm + 0.2);
            var passt = false;
            for (int i = 1; i < l.Punkte.Count; i++)
            {
                double[] sd = [l.Punkte[i][0] - l.Punkte[i - 1][0], l.Punkte[i][1] - l.Punkte[i - 1][1], l.Punkte[i][2] - l.Punkte[i - 1][2]];
                var sl = Math.Sqrt(sd.Sum(x => x * x));
                if (sl > 0 && Math.Abs((d[0] * sd[0] + d[1] * sd[1] + d[2] * sd[2]) / dl / sl - 1) < 1e-3) passt = true;
            }
            Assert.True(passt, $"Pfeil von {id} läuft nicht entlang einer Strecke in Fliessrichtung");
            Assert.Equal(XrEbenen.PfeilBreiteMm, (double)el["breite"]!);
        }
        Assert.True(pfeile > 0);
        // die App liest die Ebene ohne Verluste
        var app = XrEbene.Lesen(e.ToJsonString());
        Assert.Equal(elemente.Count, app.Elemente.Count);
        Assert.DoesNotContain(app.Hinweise, h => h.Contains("übergangen"));
        var szene = EbenenSzene.Bauen(app, [0, 0, 0]);
        Assert.Equal(pfeile * 3, szene.Teile.Where(t => t.Typ == "pfeil").Sum(t => t.Striche.Count));
    }

    [Fact]
    public async Task Pfeile_liegen_ueber_der_Leitung_senkrechte_seitlich()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        var e = await Json(await s.Lokal.GetAsync("/api/xr/ebene/fliessrichtung"));
        var dienst = s.Dienste.GetRequiredService<ProjektDienst>();
        var leitungen = await dienst.Mit(p => p.Modell.Elemente.Where(x => HlksEinordnung.Art(p.Katalog, x) == HlksArt.Leitung)
            .ToDictionary(x => x.Id, x => (Punkte: HlksEinordnung.Punkte(x), Radius: XrEbenen.Radius(x))));
        foreach (var el in e["elemente"]!.AsArray().Select(x => x!.AsObject()).Where(x => (string?)x["typ"] == "partikel"))
        {
            var l = leitungen[(string)el["element"]!];
            var pk = el["punkte"]!.AsArray().Select(q => q!.AsArray().Select(c => (double)c!).ToArray()).ToList();
            Assert.Equal(l.Punkte.Count, pk.Count);
            var abstand = l.Radius + XrEbenen.AbstandUeberMm;
            var senkrecht = Enumerable.Range(1, l.Punkte.Count - 1).All(i =>
            {
                double dx = l.Punkte[i][0] - l.Punkte[i - 1][0], dy = l.Punkte[i][1] - l.Punkte[i - 1][1], dz = l.Punkte[i][2] - l.Punkte[i - 1][2];
                return Math.Abs(dz) >= 0.7 * Math.Sqrt(dx * dx + dy * dy + dz * dz);
            });
            for (int i = 0; i < pk.Count; i++)
            {
                Assert.Equal(l.Punkte[i][0] + (senkrecht ? abstand : 0), pk[i][0], 0.06);
                Assert.Equal(l.Punkte[i][1], pk[i][1], 0.06);
                Assert.Equal(l.Punkte[i][2] + (senkrecht ? 0 : abstand), pk[i][2], 0.06);
            }
        }
    }

    [Fact]
    public async Task Ohne_Leitungen_feste_Probe_um_die_Raeume_jedes_Geschosses()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        var dienst = s.Dienste.GetRequiredService<ProjektDienst>();
        var leitungen = await dienst.Mit(p => p.Modell.Elemente.Where(x => HlksEinordnung.Art(p.Katalog, x) == HlksArt.Leitung).Select(x => x.Kennzeichen).ToList());
        foreach (var kz in leitungen)
        {
            var (code, r) = await s.LokalPost("/api/befehl/element_loeschen", new { element = kz });
            Assert.True(code == HttpStatusCode.OK && (bool)r!["angenommen"]!, r?.ToJsonString());
        }
        var e = await Json(await s.Lokal.GetAsync("/api/xr/ebene/fliessrichtung"));
        Assert.Contains(e["hinweise"]!.AsArray(), h => ((string)h!).Contains("Keine Leitungen im Modell"));
        Assert.Contains("feste Probe", (string?)e["quelle"]);
        var geschosse = await dienst.Mit(p => p.Modell.DerKlasse("Geschoss")
            .Where(g => p.Modell.Elemente.Any(x => x.Geschoss == g.Id && p.Katalog.IstArt(x, "Raum") && x.Geometrie is { Punkte.Count: >= 3 }))
            .ToDictionary(g => g.Id, g => g.Zahl("kote") ?? 0));
        var elemente = e["elemente"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(geschosse.Count * 5, elemente.Count);   // je Geschoss 4 Pfeile und ein Umlauf
        foreach (var el in elemente)
        {
            var g = (string)el["geschoss"]!;
            Assert.True(geschosse.ContainsKey(g));
            Assert.Null(el["element"]);
            Assert.All(el["punkte"]!.AsArray(), q => Assert.Equal(geschosse[g] + XrEbenen.ProbeHoeheMm, (double)q![2]!, 0.06));
        }
        var app = XrEbene.Lesen(e.ToJsonString());
        Assert.Equal(elemente.Count, app.Elemente.Count);
    }

    [Fact]
    public async Task Etag_und_304_neuer_Schluessel_nach_einer_Aenderung_an_einer_Leitung()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        using var a = await Holen(s.Lokal, "/api/xr/ebene/fliessrichtung");
        var e = await Json(a);
        var schluessel = (string)e["schluessel"]!;
        Assert.Equal($"\"{schluessel}\"", a.Headers.ETag?.ToString());
        Assert.True(a.Headers.CacheControl?.NoCache);
        foreach (var etag in new[] { $"\"{schluessel}\"", $"W/\"{schluessel}\"", "*" })
        {
            using var b = await Holen(s.Lokal, "/api/xr/ebene/fliessrichtung", etag);
            Assert.Equal(HttpStatusCode.NotModified, b.StatusCode);
        }
        // Dämmung einer Leitung ändern: der Abstand der Pfeile ändert, also der Schlüssel; der alte ETag gibt die neue Ebene
        var leitung = (string)e["elemente"]!.AsArray()[0]!["element"]!;
        var kz = await s.Dienste.GetRequiredService<ProjektDienst>().Mit(p => p.Modell.Finde(leitung)!.Kennzeichen);
        var (code, r) = await s.LokalPost("/api/befehl/element_aendern", new { element = kz, eigenschaften = new { daemmdicke = 40 } });
        Assert.True(code == HttpStatusCode.OK && (bool)r!["angenommen"]!, r?.ToJsonString());
        using var c = await Holen(s.Lokal, "/api/xr/ebene/fliessrichtung", $"\"{schluessel}\"");
        Assert.Equal(HttpStatusCode.OK, c.StatusCode);
        Assert.NotEqual($"\"{schluessel}\"", c.Headers.ETag?.ToString());
        var liste = await Json(await s.Lokal.GetAsync("/api/xr/ebenen"));
        Assert.Equal(c.Headers.ETag?.ToString(), $"\"{(string)liste["ebenen"]![0]!["schluessel"]!}\"");
    }

    [Fact]
    public async Task Fehler_unbekannte_Ebene_404_ohne_Projekt_409_nur_GET()
    {
        await using var s = new XrServer(zugang: false);
        using (var a = await s.Lokal.GetAsync("/api/xr/ebenen")) Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        using (var a = await s.Lokal.GetAsync("/api/xr/ebene/fliessrichtung")) Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        await s.ProjektAnlegen();
        using (var a = await s.Lokal.GetAsync("/api/xr/ebene/gibtsnicht"))
        {
            Assert.Equal(HttpStatusCode.NotFound, a.StatusCode);
            Assert.Contains("gibtsnicht", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        }
        using (var a = await s.Lokal.PostAsync("/api/xr/ebenen", new StringContent("{}", Encoding.UTF8, "application/json")))
            Assert.Equal(HttpStatusCode.MethodNotAllowed, a.StatusCode);
    }

    [Fact]
    public async Task Im_Bueronetz_nur_mit_Schluessel()
    {
        await using var s = new XrServer();
        await s.ProjektAnlegen();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket();
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        var l = await Json(await mit.GetAsync("/api/xr/ebenen"));
        Assert.Equal("fliessrichtung", (string?)l["ebenen"]![0]!["name"]);
        var e = await Json(await mit.GetAsync("/api/xr/ebene/fliessrichtung"));
        Assert.Equal("hlks-xr-ebene", (string?)e["art"]);
        using var ohne = XrServer.Netz(port, fp);
        using (var a = await ohne.GetAsync("/api/xr/ebenen")) Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
        using (var a = await ohne.GetAsync("/api/xr/ebene/fliessrichtung")) Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
    }

    [Fact]
    public async Task App_laedt_die_Ebenen_ueber_den_Netzzugang_mit_ETag()
    {
        await using var s = new XrServer();
        await s.ProjektAnlegen();
        var (port, _, fp) = await s.Aktiv();
        var paket = HkXr.Logik.Netz.Kopplungspaket.Lesen((await s.Paket()).ToJsonString());
        using var zugang = new HkXr.Logik.Netz.PcZugang(new HkXr.Logik.Netz.PcAdresse("127.0.0.1", port, "testpc", "test"), paket);
        Assert.Equal(fp, paket.Fingerabdruck);
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_xr_ebenen_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new HkXr.Logik.Netz.EbenenAblage(ordner);
            var st = await HkXr.Logik.Netz.EbenenLader.VomPc(zugang.Ebenen, "P", ablage);
            var e = Assert.Single(st.Ebenen);
            Assert.Equal("fliessrichtung", e.Name);
            Assert.NotEmpty(e.Elemente);
            var st2 = await HkXr.Logik.Netz.EbenenLader.VomPc(zugang.Ebenen, "P", ablage);   // 304 → aus der Ablage
            Assert.Equal(e.Schluessel, Assert.Single(st2.Ebenen).Schluessel);
        }
        finally { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); }
    }

    [Fact]
    public void KiEinordnung_XrEbenen_ist_Rechnerfunktion_kein_Werkzeug()
    {
        var f = Assert.Single(KiEinordnung.RechnerFunktionen, x => x.Name == "xr_ebenen");
        Assert.True(f.Grund.Length > 40);
        Assert.Contains("/api/xr/ebenen", f.Grund);
        Assert.Equal("verboten", KiEinordnung.Von("xr_ebenen"));
        Assert.DoesNotContain("xr_ebenen", KiEinordnung.Erlaubt);
        Assert.DoesNotContain(Hlks.Mcp.Werkzeugauswahl.Alle(), w => w.Contains("ebene", StringComparison.OrdinalIgnoreCase) && w.StartsWith("xr", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("xr_ebenen", GespraechDienst.McpWerkzeuge(new GespraechEinstellungen(), false));
        Assert.DoesNotContain("xr_ebenen", AuftragDienst.ErlaubteWerkzeuge);
    }

    [Fact]
    public void Radius_aus_der_groessten_Angabe_mit_Daemmung_sonst_100_mm()
    {
        var e = new Element { Id = "x", Klasse = "Rohr" };
        Assert.Equal(XrEbenen.GroesseOhneAngabeMm / 2, XrEbenen.Radius(e));
        e.Eigenschaften["dn"] = new Wert { Inhalt = JsonValue.Create(50) };
        e.Eigenschaften["da"] = new Wert { Inhalt = JsonValue.Create(63) };
        e.Eigenschaften["daemmdicke"] = new Wert { Inhalt = JsonValue.Create(20) };
        Assert.Equal(63 / 2.0 + 20, XrEbenen.Radius(e));
    }
}
