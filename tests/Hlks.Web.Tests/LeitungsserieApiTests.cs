using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Nachtrag Modul 3, 21.10.2026 (hlks-kern): /api/abfrage/leitungsserie rechnet nach einem kurzen Lesestand ohne die Sperre des Projekts –
/// andere Anfragen warten nicht auf sie; bricht der Browser ab, endet die Rechnung (RequestAborted, Kern-Test LeitungsserieNachtraegeTests). FIKTIVE
/// Testkanalserie mit 400 Rastertypen (tests/daten/familien/test_kanalfrei, Typen b, h = 150 … 1100) samt Formteilen mit Exemplar-Grössen.
/// Zeitabhängig: scheitert der Test unter fremder Last, einzeln wiederholen.</summary>
public class LeitungsserieApiTests
{
    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_serie_" + Guid.NewGuid().ToString("N")[..10]);
        public string Ordner => Path.Combine(Wurzel, "c_Pläne und Modelle", "e_KICAD");

        public Server()
        {
            var e = new Einstellungen("Test", "testpc", Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
            Http.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, JsonNode? koerper = null)
        {
            var a = await Http.PostAsync(pfad, new StringContent((koerper ?? new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json"));
            var text = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        public async Task Befehl(string name, JsonNode p)
        {
            var (code, j) = await Post($"/api/befehl/{name}", p);
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        }

        public void Dispose()
        {
            try { Http.PostAsync("/api/projekt/schliessen", new StringContent("{}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult(); } catch { /* egal */ }
            Http.Dispose();
            fabrik.Dispose();
            basis.Dispose();
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    static string Testdatei(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            foreach (var p in new[] { Path.Combine(d.FullName, "tests", "daten", name), Path.Combine(d.FullName, "HLKS_CAD", "tests", "daten", name) })
                if (File.Exists(p)) return p;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "tests", "daten", name);
    }

    [Fact]
    public async Task Leitungsserie_GrosseFreieSerie_AndereAnfragenWartenNicht()
    {
        using var s = new Server();
        var (code, _) = await s.Post("/api/projekt/anlegen", new JsonObject { ["ordner"] = s.Ordner, ["name"] = "Serie Test", ["beispiel"] = true });
        Assert.Equal(HttpStatusCode.OK, code);
        foreach (var f in new[] { "test_kanalbogen_frei", "test_kanal_t_frei", "test_kanalreduktion_frei" })
            await s.Befehl("familie_laden", new JsonObject { ["datei"] = Testdatei(Path.Combine("familien", f + ".familie.json")) });
        var d = (JsonObject)JsonNode.Parse(File.ReadAllText(Testdatei(Path.Combine("familien", "test_kanalfrei.familie.json"))))!;
        d["id"] = "fam-test-frei-400"; d["name"] = "Testkanal frei 400 Typen (fiktiv)";
        var masse = Enumerable.Range(0, 20).Select(i => 150.0 + 50 * i).ToList();
        d["typen"] = new JsonArray(masse.SelectMany(b => masse.Select(h => (JsonNode)new JsonObject
        {
            ["name"] = $"{b}x{h}", ["werte"] = new JsonObject { ["b"] = b, ["h"] = h },
        })).ToArray());
        await s.Befehl("familie_entwurf", new JsonObject { ["definition"] = d });

        var uhr = Stopwatch.StartNew();
        var gross = s.Post("/api/abfrage/leitungsserie", new JsonObject { ["familie"] = "fam-test-frei-400" });
        await Task.Delay(400);
        var (kleinCode, klein) = await s.Post("/api/abfrage/element_lesen", new JsonObject { ["element"] = "SW" });
        var kleinFertig = uhr.Elapsed;
        Assert.Equal(HttpStatusCode.OK, kleinCode);
        Assert.Equal("SW", klein!["kennzeichen"]!.GetValue<string>());
        Assert.False(gross.IsCompleted, $"leitungsserie war schon nach {kleinFertig.TotalSeconds:0.0} s fertig – zu kurz, um das Warten zu prüfen");   // die kleine Anfrage wartete nicht
        var (grossCode, serie) = await gross;
        Assert.Equal(HttpStatusCode.OK, grossCode);
        Assert.Equal("gezaehlt", serie!["kontrolle"]!["reduktionen"]!["ermittlung"]!.GetValue<string>());
        Assert.Equal(400, serie["groessen"]!.AsArray().Count);
    }
}
