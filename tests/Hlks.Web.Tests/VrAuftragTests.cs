using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>VR-Auftrag vom PC an die Brille (VrAuftrag.cs, /api/vr/auftrag): setzen, lesen, hochzählen, Prüfung, Neustart, Ursprungsprüfung, KI-Einordnung.</summary>
public class VrAuftragTests
{
    const string Id = "3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a";

    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }

        public Server(string benutzerOrdner)
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false, BenutzerOrdner = benutzerOrdner };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(object koerper, Action<HttpRequestMessage>? anpassen = null, string? typ = "application/json")
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/vr/auftrag") { Content = new StringContent(koerper is string s ? s : Hlks.Kern.Json.Text(koerper), Encoding.UTF8, typ ?? "application/json") };
            anpassen?.Invoke(r);
            var a = await Http.SendAsync(r);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<JsonNode?> Auftrag()
        {
            var a = await Http.GetAsync("/api/vr/auftrag");
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            return JsonNode.Parse(await a.Content.ReadAsStringAsync())!["auftrag"];
        }

        public void Dispose() { Http.Dispose(); fabrik.Dispose(); basis.Dispose(); }
    }

    static string NeuerOrdner() => Path.Combine(Path.GetTempPath(), "hlks_vra_" + Guid.NewGuid().ToString("N")[..10]);
    static void Weg(string o) { try { if (Directory.Exists(o)) Directory.Delete(o, true); } catch { /* egal */ } }

    [Fact]
    public async Task OhneAuftrag_IstDerAuftragLeer()
    {
        var o = NeuerOrdner();
        try { using var s = new Server(o); Assert.Null(await s.Auftrag()); }
        finally { Weg(o); }
    }

    [Fact]
    public async Task Setzen_ZaehltNrHoch_UndLiefertZeit()
    {
        var o = NeuerOrdner();
        try
        {
            using var s = new Server(o);
            var (c1, j1) = await s.Post(new { modus = "begehen", raum = Id });
            Assert.Equal(HttpStatusCode.OK, c1);
            Assert.Equal(1, (long)j1!["auftrag"]!["nr"]!);
            var (_, j2) = await s.Post(new { modus = "sitzungsraum", geschoss = "EG", nr = 99 });   // ein nr im Körper zählt nicht
            Assert.Equal(2, (long)j2!["auftrag"]!["nr"]!);
            var a = await s.Auftrag();
            Assert.Equal("sitzungsraum", (string?)a!["modus"]);
            Assert.Equal("EG", (string?)a["geschoss"]);
            Assert.Null(a["raum"]);
            Assert.True(DateTimeOffset.TryParse((string?)a["zeit"], out var z) && Math.Abs((DateTimeOffset.UtcNow - z).TotalMinutes) < 5);
        }
        finally { Weg(o); }
    }

    [Fact]
    public async Task Auftrag_UeberstehtEinenNeustart()
    {
        var o = NeuerOrdner();
        try
        {
            using (var s = new Server(o)) { await s.Post(new { modus = "begehen", raum = "R-1" }); await s.Post(new { modus = "begehen", raum = Id }); }
            using var neu = new Server(o);
            var a = await neu.Auftrag();
            Assert.Equal(2, (long)a!["nr"]!);
            Assert.Equal(Id, (string?)a["raum"]);
            var (_, j) = await neu.Post(new { modus = "sitzungsraum" });
            Assert.Equal(3, (long)j!["auftrag"]!["nr"]!);   // zählt nach dem Neustart weiter
        }
        finally { Weg(o); }
    }

    [Fact]
    public void OhneAblage_WederGelesenNochGeschrieben_MitAblageWieBisher()
    {
        // XR-Labor L0: der Speicher des Labors (ablage: false) sieht KICADs gespeicherten Auftrag nicht und schreibt nie; die Vorgabe (KICAD) speichert wie bisher
        var o = NeuerOrdner();
        try
        {
            Directory.CreateDirectory(o);
            BenutzerEinstellungen Benutzer() => new(new RechnerAblage(o, "testpc", true, "einstellungen.json", "Umleitung"));
            var kicad = new VrAuftragSpeicher(Benutzer());
            var a = kicad.Setzen(new JsonObject { ["modus"] = "begehen", ["raum"] = "R-1" });
            Assert.Equal(1, a.Nr);
            Assert.Equal(1, (long)Benutzer().Hole(VrAuftragSpeicher.Schluessel)!["nr"]!);
            Assert.Equal(1, new VrAuftragSpeicher(Benutzer()).Lesen()!.Nr);   // nach einem Neustart wie bisher

            var labor = new VrAuftragSpeicher(Benutzer(), ablage: false);
            Assert.Null(labor.Lesen());
            VrAuftragWert? gesetzt = null;
            labor.Gesetzt += w => gesetzt = w;
            var b = labor.Setzen(new JsonObject { ["modus"] = "sitzungsraum" });
            Assert.Equal(1, b.Nr);
            Assert.Same(b, labor.Lesen());
            Assert.Same(b, gesetzt);
            Assert.Equal("begehen", (string)Benutzer().Hole(VrAuftragSpeicher.Schluessel)!["modus"]!);   // KICADs Auftrag bleibt unberührt
            Assert.Equal(1, (long)Benutzer().Hole(VrAuftragSpeicher.Schluessel)!["nr"]!);
        }
        finally { Weg(o); }
    }

    [Fact]
    public async Task AusschnittUndAnsicht_WerdenGespeichertUndGelesen()
    {
        var o = NeuerOrdner();
        try
        {
            using var s = new Server(o);
            var (c1, j1) = await s.Post(new { modus = "sitzungsraum", raum = Id, ausschnitt = true });
            Assert.Equal(HttpStatusCode.OK, c1);
            Assert.True((bool)j1!["auftrag"]!["ausschnitt"]!);
            Assert.Equal(Id, (string?)j1["auftrag"]!["raum"]);
            var (c2, _) = await s.Post(new { modus = "sitzungsraum", ansicht = "AN-3D" });
            Assert.Equal(HttpStatusCode.OK, c2);
            var a = await s.Auftrag();
            Assert.Equal("AN-3D", (string?)a!["ansicht"]);
            Assert.False((bool)a["ausschnitt"]!);
            Assert.Null(a["raum"]);
            var (_, j3) = await s.Post(new { modus = "begehen", raum = Id });   // ohne Ausschnitt und Ansicht: beide leer
            Assert.False((bool)j3!["auftrag"]!["ausschnitt"]!); Assert.Null(j3["auftrag"]!["ansicht"]);
        }
        finally { Weg(o); }
    }

    [Fact]
    public async Task AusschnittUndAnsicht_UeberstehenEinenNeustart()
    {
        var o = NeuerOrdner();
        try
        {
            using (var s = new Server(o)) await s.Post(new { modus = "sitzungsraum", raum = "R-1", ausschnitt = true });
            using var neu = new Server(o);
            var a = await neu.Auftrag();
            Assert.True((bool)a!["ausschnitt"]!); Assert.Equal("R-1", (string?)a["raum"]);
            await neu.Post(new { modus = "sitzungsraum", ansicht = Id });
            using var nochmal = new Server(o);
            Assert.Equal(Id, (string?)(await nochmal.Auftrag())!["ansicht"]);
        }
        finally { Weg(o); }
    }

    [Theory]
    [InlineData("{\"modus\":\"sitzungsraum\",\"ansicht\":\"../x\"}")]                                  // ungültiges Muster
    [InlineData("{\"modus\":\"sitzungsraum\",\"ansicht\":\"<script>\"}")]
    [InlineData("{\"modus\":\"begehen\",\"ansicht\":\"AN-3D\"}")]                                      // Ansicht nur im Sitzungsraum
    [InlineData("{\"modus\":\"sitzungsraum\",\"ansicht\":\"AN-3D\",\"raum\":\"R-1\"}")]               // Ansicht und Raum schliessen sich aus
    [InlineData("{\"modus\":\"sitzungsraum\",\"ausschnitt\":true}")]                                   // Ausschnitt braucht einen Raum
    [InlineData("{\"modus\":\"begehen\",\"raum\":\"R-1\",\"ausschnitt\":true}")]                       // und den Modus sitzungsraum
    [InlineData("{\"modus\":\"sitzungsraum\",\"raum\":\"R-1\",\"ausschnitt\":\"ja\"}")]                // ausschnitt muss ein bool sein
    public async Task UngueltigerAusschnittOderAnsicht_WirdAbgelehnt(string koerper)
    {
        var o = NeuerOrdner();
        try
        {
            using var s = new Server(o);
            await s.Post(new { modus = "begehen", raum = Id });
            var (code, j) = await s.Post(koerper);
            Assert.Equal(HttpStatusCode.BadRequest, code);
            Assert.False(string.IsNullOrEmpty((string?)j!["fehler"]));
            Assert.Equal(1, (long)(await s.Auftrag())!["nr"]!);
        }
        finally { Weg(o); }
    }

    [Theory]
    [InlineData("{\"modus\":\"fliegen\"}")]
    [InlineData("{}")]
    [InlineData("{\"modus\":\"begehen\",\"raum\":\"../../etc\"}")]
    [InlineData("{\"modus\":\"begehen\",\"raum\":\"<script>\"}")]
    [InlineData("{\"modus\":\"begehen\",\"raum\":\"R 1\"}")]
    [InlineData("{\"modus\":\"sitzungsraum\",\"geschoss\":\"x;y\"}")]
    [InlineData("{\"modus\":\"begehen\",\"raum\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    public async Task UngueltigerAuftrag_WirdAbgelehnt_UndNichtGespeichert(string koerper)
    {
        var o = NeuerOrdner();
        try
        {
            using var s = new Server(o);
            await s.Post(new { modus = "begehen", raum = Id });
            var (code, j) = await s.Post(koerper);
            Assert.Equal(HttpStatusCode.BadRequest, code);
            Assert.False(string.IsNullOrEmpty((string?)j!["fehler"]));
            var a = await s.Auftrag();
            Assert.Equal(1, (long)a!["nr"]!);   // der gültige von vorher gilt unverändert
        }
        finally { Weg(o); }
    }

    [Fact]
    public async Task Ursprungspruefung_FremderHostUndFremdeSeite_Abgelehnt()
    {
        var o = NeuerOrdner();
        try
        {
            using var s = new Server(o);
            var (c1, _) = await s.Post(new { modus = "begehen" }, r => r.Headers.Host = "evil.example");
            Assert.Equal(HttpStatusCode.Forbidden, c1);
            var (c2, _) = await s.Post(new { modus = "begehen" }, r => r.Headers.Add("Origin", "http://evil.example"));
            Assert.Equal(HttpStatusCode.Forbidden, c2);
            var (c3, _) = await s.Post("modus=begehen", null, "text/plain");
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, c3);
            var g = new HttpRequestMessage(HttpMethod.Get, "/api/vr/auftrag"); g.Headers.Host = "evil.example";
            Assert.Equal(HttpStatusCode.Forbidden, (await s.Http.SendAsync(g)).StatusCode);
            Assert.Null(await s.Auftrag());   // nichts wurde gesetzt
        }
        finally { Weg(o); }
    }

    [Fact]
    public void KiEinordnung_VrAuftragIstEineOberflaechenfunktion_UndKeinWerkzeug()
    {
        Assert.Contains(KiEinordnung.Oberflaechenfunktionen, f => f.Name == "vr_auftrag" && f.Grund.Length > 40);
        Assert.DoesNotContain(KiEinordnung.RechnerFunktionen, f => f.Name == "vr_auftrag");
        Assert.Equal("verboten", KiEinordnung.Von("vr_auftrag"));
        Assert.DoesNotContain(KiEinordnung.Erlaubt, n => n.Contains("vr_auftrag"));
    }
}
