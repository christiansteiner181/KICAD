using System.Net;
using System.Text;
using Hlks.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Labor.Tests;

/// <summary>
/// «Netz einrichten …» im Labor (Christian 26.10.2026: ein Knopf in KICAD und im Labor): der Labor-Server hängt dieselben Endpunkte ein (QuestAppEndpunkte.cs) und liefert
/// die Skripte im eigenen Programmordner. Es startet nie ein Programm: der Prozessstart ist durch eine Attrappe ersetzt.
/// </summary>
public class LaborQuestAppNetzTests
{
    sealed class Einrichten : IQuestAppEinrichten
    {
        public List<EinrichtenAuftrag> Auftraege { get; } = [];
        public Task<EinrichtenErgebnis> Starten(EinrichtenAuftrag auftrag, CancellationToken abbruch)
        {
            lock (Auftraege) Auftraege.Add(auftrag);
            return Task.FromResult(new EinrichtenErgebnis(true, null));
        }
    }

    const string Pfad = "/api/quest/app/netz/einrichten";

    [Fact]
    public async Task Das_Labor_startet_das_Skript_aus_seinem_Programmordner_nur_mit_Bestaetigung()
    {
        var e = new Einrichten();
        await using var s = new LaborServer(dienste: d => d.AddSingleton<IQuestAppEinrichten>(e));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.LokalPost(Pfad, new { })).Code);
        Assert.Empty(e.Auftraege);
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True((bool)j!["gestartet"]!);
        var a = Assert.Single(e.Auftraege);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "werkzeuge", "xr_netz_einrichten.ps1"), a.Skript);
        Assert.True(File.Exists(a.Skript));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "werkzeuge", "xr_firewall.ps1")));   // das Skript ruft die Firewall-Regeln aus seinem Ordner
        Assert.EndsWith("-Erhoeht -Ja", a.Argumente);
    }

    [Fact]
    public async Task Das_Labor_nimmt_den_Aufruf_nur_von_localhost()
    {
        var e = new Einrichten();
        await using var s = new LaborServer(dienste: d => d.AddSingleton<IQuestAppEinrichten>(e));
        using var r = new HttpRequestMessage(HttpMethod.Post, Pfad) { Content = new StringContent("{\"bestaetigt\":true}", Encoding.UTF8, "application/json") };
        r.Headers.Host = "boese.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Lokal.SendAsync(r)).StatusCode);
        Assert.Empty(e.Auftraege);
    }

    [Fact]
    public async Task Im_Testlauf_startet_das_Labor_ohne_Attrappe_nichts()
    {
        await using var s = new LaborServer();
        var (code, j) = await s.LokalPost(Pfad, new { bestaetigt = true });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False((bool)j!["gestartet"]!);
    }
}
