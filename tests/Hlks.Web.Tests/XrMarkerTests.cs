using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using HkXr.Logik.Marker;
using HkXr.Logik.Netz;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App V1a (Modul XR, XR-E): Problem-Marker aus der Brille (XrMarkerEndpunkte.cs) mit dem Beispielprojekt «MFH Musterweg» – POST und GET /api/xr/marker,
/// Ablage als Element ProblemMarker im Projekt (Paket, Rückgängig, Liste im Modell), doppelt gesendete Kennung, Prüfung der Eingaben, ETag/304, Zugang im Büronetz
/// nur mit Schlüssel (Benutzer = Name der Kopplung), die App (HkXr.Logik.Marker, ModellHttp) sendet und liest, Änderung am PC mit element_aendern, BCF-Export, KI-Einordnung.
/// </summary>
public class XrMarkerTests
{
    static readonly DateTimeOffset Zeit = new(2026, 10, 24, 14, 5, 0, TimeSpan.FromHours(2));

    static JsonObject Neu(string kennung = "k-1", string? notiz = null, object? element = null) => JsonNode.Parse(Json.Text(new
    {
        kennung, ort = new[] { 2000.0, 1500, 1200 }, blick = new { kopf = new[] { 2000.0, -500, 1700 }, richtung = new[] { 0.0, 1, 0 }, modus = "begehen", massstab = 1 },
        element, geschoss = (string?)null, zeit = "2026-10-24T14:05:00+02:00", notiz,
    }))!.AsObject();

    static async Task<(HttpStatusCode Code, JsonObject? Json, HttpResponseMessage A)> Post(HttpClient c, JsonObject k)
    {
        var a = await c.PostAsync("/api/xr/marker", new StringContent(k.ToJsonString(), Encoding.UTF8, "application/json"));
        var t = await a.Content.ReadAsStringAsync();
        return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t) as JsonObject, a);
    }

    [Fact]
    public async Task Marker_wird_Element_im_Projekt_mit_Paket_und_steht_in_der_Liste()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        var (code, j, _) = await Post(s.Lokal, Neu(notiz: "Kollision\u0007 mit Lüftung"));
        Assert.Equal(HttpStatusCode.Created, code);
        var m = j!["marker"]!;
        Assert.Equal("PM-1", (string)m["kennzeichen"]!);
        Assert.Equal("offen", (string)m["status"]!);
        Assert.Equal("Kollision mit Lüftung", (string)m["notiz"]!);
        Assert.Equal(1200, (double)m["ort"]![2]!);
        Assert.Equal("begehen", (string)m["blick"]!["modus"]!);
        // im Modell: Element ProblemMarker, Quelle Brille, mit Paket (Rückgängig über die Tür des Kerns)
        var modell = JsonNode.Parse(await s.Lokal.GetStringAsync("/api/modell"))!.ToJsonString();
        Assert.Contains("\"klasse\":\"ProblemMarker\"", modell);
        // zweimal dieselbe Kennung (nach einer Trennung nochmals gesendet): kein zweiter Marker
        var (code2, j2, _) = await Post(s.Lokal, Neu());
        Assert.Equal(HttpStatusCode.OK, code2);
        Assert.True((bool)j2!["schon"]!);
        Assert.Equal("PM-1", (string)j2["marker"]!["kennzeichen"]!);
        // ein zweiter ohne Notiz, mit betroffenem Element (GlobalId der Unterlage)
        var (c3, j3, _) = await Post(s.Lokal, Neu("k-2", element: new { id = "3vB2YO$MX4xv5uCqZZG05x", teil = "unterlage", art = "IfcWall" }));
        Assert.Equal(HttpStatusCode.Created, c3);
        Assert.Null(j3!["marker"]!["notiz"]);
        Assert.Equal("unterlage", (string)j3["marker"]!["element"]!["teil"]!);
        // Liste mit ETag und 304
        var a = await s.Lokal.GetAsync("/api/xr/marker");
        var liste = JsonNode.Parse(await a.Content.ReadAsStringAsync())!;
        Assert.Equal(["PM-1", "PM-2"], liste["marker"]!.AsArray().Select(x => (string)x!["kennzeichen"]!));
        var etag = a.Headers.ETag!.ToString();
        var r = new HttpRequestMessage(HttpMethod.Get, "/api/xr/marker");
        r.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await s.Lokal.SendAsync(r)).StatusCode);
        // am PC: Status, Zuständig, Frist mit dem normalen Befehl → neue Liste (anderer ETag)
        var (cb, jb) = await s.LokalPost("/api/befehl/element_aendern", new { element = "PM-1", eigenschaften = new { status = "erledigt", zustaendig = "Muster Sanitär AG", frist = "2026-11-02" } });
        Assert.True(cb == HttpStatusCode.OK && (bool)jb!["angenommen"]!, jb?.ToJsonString());
        var a2 = await s.Lokal.GetAsync("/api/xr/marker");
        Assert.NotEqual(etag, a2.Headers.ETag!.ToString());
        var pm1 = JsonNode.Parse(await a2.Content.ReadAsStringAsync())!["marker"]![0]!;
        Assert.Equal("erledigt", (string)pm1["status"]!);
        Assert.Equal("Muster Sanitär AG", (string)pm1["zustaendig"]!);
        // Rückgängig nimmt die Änderung zurück
        Assert.Equal(HttpStatusCode.OK, (await s.LokalPost("/api/befehl/rueckgaengig", new { })).Code);
        Assert.Equal("offen", (string)JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/marker"))!["marker"]![0]!["status"]!);
    }

    [Fact]
    public async Task Ungueltige_Eingaben_400_ohne_Projekt_409()
    {
        await using var s = new XrServer(zugang: false);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Lokal.GetAsync("/api/xr/marker")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(s.Lokal, Neu())).Code);
        await s.ProjektAnlegen();
        var schlecht = new List<JsonObject>();
        var a = Neu(); a.Remove("kennung"); schlecht.Add(a);
        var b = Neu("mit leerzeichen"); schlecht.Add(b);
        var c = Neu(); c["ort"] = new JsonArray(1, 2); schlecht.Add(c);
        var d = Neu(); d["ort"] = new JsonArray(1, 2, 1e9); schlecht.Add(d);
        var e = Neu(); e.Remove("blick"); schlecht.Add(e);
        var f = Neu(); f["blick"]!["modus"] = "fliegen"; schlecht.Add(f);
        var g = Neu(); g["blick"]!["richtung"] = new JsonArray(0, 0, 0); schlecht.Add(g);
        var h = Neu(); h["notiz"] = new string('x', 501); schlecht.Add(h);
        var i = Neu(); i["blick"]!["massstab"] = 0; schlecht.Add(i);
        foreach (var x in schlecht)
        {
            var (code, j, _) = await Post(s.Lokal, x);
            Assert.True(code == HttpStatusCode.BadRequest, $"{x.ToJsonString()} → {(int)code} {j?.ToJsonString()}");
            Assert.False(string.IsNullOrEmpty((string?)j!["fehler"]));
        }
        // ein unbekanntes Modell-Element zählt nicht als betroffen, der Marker kommt trotzdem
        var (ok, jo, _) = await Post(s.Lokal, Neu("k-9", element: new { id = "gibtsnicht", teil = "modell", art = "wand" }));
        Assert.Equal(HttpStatusCode.Created, ok);
        Assert.Null(jo!["marker"]!["element"]);
        Assert.Single(JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/marker"))!["marker"]!.AsArray());   // die ungültigen legten nichts an
    }

    [Fact]
    public async Task Im_Bueronetz_nur_mit_Schluessel_Benutzer_ist_die_Kopplung_und_die_App_sendet_und_liest()
    {
        await using var s = new XrServer();
        await s.ProjektAnlegen();
        var (port, _, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST3-MARKER", "Quest Christian");
        using var ohne = XrServer.Netz(port, fp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ohne.GetAsync("/api/xr/marker")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(ohne, Neu())).Code);
        // die App: HkXr.Logik.Marker baut den Marker, ModellHttp (über den Netzzugang mit Schlüssel) sendet ihn
        using var mit = XrServer.Netz(port, fp, XrServer.Token(paket));
        var quelle = new ModellHttp(mit, "netz");
        var neu = ProblemMarker.Neu("k-app", new HkXr.Logik.Vec3(1, 1.2, -2), new HkXr.Logik.Vec3(1, 1.7, 0), new HkXr.Logik.Vec3(0, 0, -1), [0, 0, 0], ProblemMarker.ModusBegehen, 1,
            null, null, null, Zeit, "Prüfen");
        var antwort = await quelle.MarkerSenden(neu.ZuJson().ToJsonString(), CancellationToken.None);
        Assert.Equal(201, antwort.Status);
        var m = ProblemMarker.EintragLesen(System.Text.Json.JsonDocument.Parse(antwort.Text!).RootElement.GetProperty("marker"))!;
        Assert.Equal("Quest Christian", m.Benutzer);
        Assert.Equal([1000, 2000, 1200], m.OrtMm);
        Assert.Equal("Prüfen", m.Notiz);
        // die Liste über den Netzzugang mit ETag
        var l = await quelle.MarkerListe(null, CancellationToken.None);
        Assert.Equal(200, l.Status);
        var liste = ProblemMarker.ListeLesen(l.Text!);
        var e = Assert.Single(liste);
        Assert.Equal("k-app", e.Kennung);
        Assert.Equal(new HkXr.Logik.Vec3(1, 1.2, -2), ProblemMarker.OrtSzene(e, [0, 0, 0]));
        Assert.Equal(304, (await quelle.MarkerListe(l.ETag, CancellationToken.None)).Status);
        // im Modell: brille = Kennung der Brille
        var modell = await s.Lokal.GetStringAsync("/api/modell");
        Assert.Contains("QUEST3-MARKER", modell);
    }

    [Fact]
    public async Task BCF_Export_der_Marker_über_die_Abfrage()
    {
        await using var s = new XrServer(zugang: false);
        await s.ProjektAnlegen();
        await Post(s.Lokal, Neu(notiz: "Kollision"));
        var datei = Path.Combine(s.Ordner, "marker.bcf");
        var (code, j) = await s.LokalPost("/api/abfrage/bcf_export", new { datei, klasse = "ProblemMarker" });
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        Assert.Equal(1, (int)j!["topics"]!);
        Assert.StartsWith("bestanden", (string)j["schemapruefung"]!);
        Assert.True(File.Exists(datei));
    }

    [Fact]
    public void KI_Einordnung_xr_marker_verboten_kein_Werkzeug()
    {
        Assert.Equal("verboten", KiEinordnung.Von("xr_marker"));
        Assert.Contains("/api/xr/marker", KiEinordnung.RechnerFunktionen.Single(x => x.Name == "xr_marker").Grund);
        Assert.DoesNotContain("xr_marker", KiEinordnung.Erlaubt);
        Assert.DoesNotContain("xr_marker", Hlks.Mcp.Werkzeugauswahl.Alle());
    }
}
