// «Quest App» im Dialog «Quest verbinden» (Modul XR, Quest App P7) über REST – dünne Hülle über QuestDienst (QuestApp.cs, QuestAppNetz.cs); in Program.cs hängt nur
// «app.MapQuestApp(questStarter.Quest);» sie ein. Reine Rechner-Funktionen: kein Kern-Befehl, das Modell wird nicht berührt; für die KI verboten
// (KiEinordnung.RechnerFunktionen «quest_app», «quest_app_netz»; kein MCP-Werkzeug). Der Herkunftsschutz gilt (nur localhost, POST nur mit application/json).
//
//   GET  /api/quest/app             Zustand: { paket, ablage{ ordner, quelle (einstellung | vorgabe), vorhanden, version?{ datei, commit?, zeit?, groesse, sha256, versionName?, versionCode? }, apkPfad?, fehler? },
//                                   adbGefunden, adbHinweis?, brille?{ seriennummer, wlan, modell?, installiert?{ installiert, versionName?, versionCode?, aktualisiert?, erstinstalliert? } },
//                                   vergleich: nicht_installiert | andere_nummer | gleiche_nummer | unbekannt }
//   GET  /api/quest/app/netz        Netzprofil und Firewall (nur lesen; 30 s gemerkt, ?neu=1 liest neu): { gelesen, fehler?, zugang{ an, aktiv, port, suchPort, ips[], fingerabdruckKurz? },
//                                   schnittstellen[{ name, ip, profil? }], profile[], regeln[], hinweise[{ stufe: ok | info | warnung, text }], skript }
//   POST /api/quest/app/installieren { weg: wlan | kabel, name? }  App per adb installieren, Brille koppeln (Weg A), App starten
//                                   → { ok, schritte[{ id, text, ok }], meldung?, fehler?, vorschlag?, weg, brille?, name? } (Status 200 auch bei ok = false; nie ein Schlüssel)
// Den Ordner der Ablage speichert die Oberfläche als Benutzereinstellung «quest.app_ablage» (POST /api/benutzer/einstellungen). Netzzugang ein/aus, gekoppelte Brillen, Ersatzkopplung:
// die Endpunkte /api/xr/… (XrEndpunkte.cs).

using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class QuestAppEndpunkte
{
    public static WebApplication MapQuestApp(this WebApplication app, QuestDienst quest)
    {
        var netz = app.Services.GetRequiredService<XrNetzDienst>();
        var kopplungen = app.Services.GetRequiredService<XrKopplungen>();
        var system = app.Services.GetService<IQuestAppSystem>() ?? IQuestAppSystem.Vorgabe();
        var lan = app.Services.GetService<IQuestNetz>() ?? IQuestNetz.Vorgabe();
        var g = app.MapGroup("/api/quest/app");

        var cacheSperre = new SemaphoreSlim(1, 1);
        (DateTimeOffset Zeit, NetzAuskunft Auskunft)? cache = null;
        async Task<NetzAuskunft> Auskunft(bool neu, CancellationToken abbruch)
        {
            await cacheSperre.WaitAsync(abbruch);
            try
            {
                if (!neu && cache is { } c && DateTimeOffset.UtcNow - c.Zeit < TimeSpan.FromSeconds(30)) return c.Auskunft;
                var a = await system.NetzLesen(abbruch);
                cache = (DateTimeOffset.UtcNow, a);
                return a;
            }
            finally { cacheSperre.Release(); }
        }

        g.MapGet("", () => Gesichert(async () => Antwort(await quest.AppZustand())));

        g.MapGet("/netz", (HttpRequest r) => Gesichert(async () =>
        {
            var a = await Auskunft(r.Query["neu"] == "1", r.HttpContext.RequestAborted);
            var info = netz.Info();
            var echte = lan.Schnittstellen().Where(QuestDienst.EchteSchnittstelle).ToList();
            var hinweise = QuestAppNetz.Bewerten(a, echte, info.Port, info.SuchPort, netz.Aktiv);
            var zustand = netz.Zustand();
            return Antwort(new
            {
                gelesen = a.Gelesen, fehler = a.Fehler,
                zugang = new { an = (bool)zustand["an"]!, aktiv = netz.Aktiv, port = info.Port, suchPort = info.SuchPort, ips = info.Ips, fingerabdruckKurz = (string?)zustand["fingerabdruckKurz"] },
                schnittstellen = echte.Select(s => new { name = s.Name, ip = s.Ip, profil = a.Profile.FirstOrDefault(p => string.Equals(p.Schnittstelle, s.Name, StringComparison.OrdinalIgnoreCase))?.Kategorie }),
                profile = a.Profile, regeln = a.Regeln, hinweise, skript = QuestAppNetz.FirewallSkript,
            });
        }));

        g.MapPost("/installieren", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var e = await quest.AppInstallieren(Param.Text(p, "weg", true), Param.Text(p, "name"), netz, kopplungen);
            r.HttpContext.Response.Headers.CacheControl = "no-store";
            return Antwort(e);
        }));
        return app;
    }
}
