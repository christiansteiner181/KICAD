// «Quest verbinden» (Modul XR) über REST – dünne Hülle über QuestDienst; in Program.cs hängt nur «app.MapQuest(dienst);» sie ein. Reine Rechner-Funktion:
// es ist kein Kern-Befehl und keine Kern-Abfrage (das Modell wird nicht berührt), wie «Ordner im Explorer zeigen» (POST /api/ordner/zeigen). Für die KI nicht
// freigegeben (KiEinordnung.RechnerFunktionen, kein MCP-Werkzeug). Der Herkunftsschutz gilt (nur localhost, POST nur mit application/json, kein fremder Ursprung).
// Der Port der Weiterleitung ist der Port, auf dem dieser Server die Anfrage bekam (Host-Kopf der geprüften Anfrage) – nie ein Wert aus dem Körper.
//
//   GET  /api/quest            Zustand: { adbGefunden, adbPfad?, adbQuelle? (einstellung|pfad|ort), adbHinweis?, geraete: [{ seriennummer, zustand, wlan }],
//                              questVerbunden, freigegeben, nichtFreigegeben, seriennummer?, wlan, ip?, weiterleitung, port, url, fehler? }
//   POST /api/quest/verbinden  Weiterleitung setzen (adb reverse tcp:<Port> tcp:<Port>)            → Zustand + meldung
//   POST /api/quest/wlan       Quest per USB auf adb über WLAN umstellen (tcpip 5555, connect) und Weiterleitung neu setzen → Zustand + meldung
//   POST /api/quest/ip         { ip }  über WLAN verbinden (IPv4, optional :Port) und Weiterleitung setzen → Zustand + meldung
//   POST /api/quest/trennen    Weiterleitung entfernen                                              → Zustand + meldung
//   POST /api/quest/automatik  { anlass: takt | start | klick | ansicht3d }  EIN Schritt von «Brille automatisch verbinden» (idempotent, unter der Sperre) →
//                              { automatisch, status: aus | kein_adb | bereit | sucht | nicht_gefunden | nicht_freigegeben | fehler, meldung?, hinweis?, ping: nein | gelaufen | gesperrt | keine_netze,
//                              ip?, seriennummer?, brille?, port, url }. «takt» liest nur adb devices und die Nachbartabelle; die anderen dürfen einen Ping-Durchlauf machen
//                              (im eigenen /24-Netz, höchstens einmal je 2 Minuten). Ist quest.automatisch aus, geschieht nichts (ausser bei «klick»).
// GET /api/quest liefert zusätzlich automatisch (Einstellung quest.automatisch) und brillen (Einstellung quest.brillen: [{ seriennummer, mac, letzteIp, zuletzt }]).
// Den Pfad zur adb.exe speichert die Oberfläche als Benutzereinstellung «quest.adb» (POST /api/benutzer/einstellungen). Fehler: 400 { fehler, vorschlag }.

using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static class QuestEndpunkte
{
    public static WebApplication MapQuest(this WebApplication app, ProjektDienst dienst)
    {
        var einst = app.Services.GetService<QuestEinstellungen>() ?? QuestEinstellungen.Vorgabe();
        var netz = app.Services.GetService<IQuestNetz>() ?? IQuestNetz.Vorgabe();
        var quest = new QuestDienst(dienst.Benutzer, einst, netz);
        var g = app.MapGroup("/api/quest");

        g.MapGet("", (HttpRequest r) => Gesichert(async () => Antwort(await quest.Zustand(Port(r)))));
        g.MapPost("/verbinden", (HttpRequest r) => Gesichert(async () => Antwort(await quest.Verbinden(Port(r)))));
        g.MapPost("/wlan", (HttpRequest r) => Gesichert(async () => Antwort(await quest.AufWlanUmstellen(Port(r)))));
        g.MapPost("/ip", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(await quest.MitIpVerbinden(Param.Text(p, "ip", true), Port(r)));
        }));
        g.MapPost("/trennen", (HttpRequest r) => Gesichert(async () => Antwort(await quest.Trennen(Port(r)))));
        g.MapPost("/automatik", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(await quest.Automatik(Port(r), Param.Text(p, "anlass")));
        }));
        return app;
    }

    /// <summary>Der Port dieses Servers aus dem Host-Kopf (der Herkunftsschutz hat den Host-Namen schon als localhost geprüft); ohne Angabe 80.</summary>
    static int Port(HttpRequest r) => r.Host.Port ?? 80;
}
