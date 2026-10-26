// Interface-Labor der Quest App (Modul XR, P6): Katalog für die App, Auswertung der Messprotokolle, Fragebogen, Varianten und Prüfaufgaben schalten. Dünne Hülle über
// XrLaborKatalog.cs, XrLaborAuswertung.cs und XrFragebogen.cs; in Program.cs hängt nur «app.MapXrLabor(dienst);» sie ein. Reine Rechner-Funktionen: kein Kern-Befehl, das Modell
// wird nicht berührt; für die KI verboten (KiEinordnung.RechnerFunktionen «xr_labor»; kein MCP-Werkzeug).
//
// Für die Brille (Marke XrImNetz: im Netzzugang nur über HTTPS mit Schlüssel):
//   GET  /api/xr/labor/katalog     Stellschrauben, Aufgaben und Varianten zusammen (Aufbau: XrLaborKatalog.Zusammen), ETag = "<schluessel>", If-None-Match → 304.
//                                  «profil» ist das Profil, das der Benutzer als «gilt für die App» markiert hat (Benutzereinstellung labor.profil_aktiv), sonst null.
// Nur am PC (localhost):
//   GET  /api/xr/labor/auswertung  Messprotokolle zusammengefasst (XrLaborAuswertung.Lesen): aufgaben[{ aufgabe, titel, status, laeufe, …, varianten[], fassungen[] }], fassungen[], fragebogen[]
//   GET  /api/xr/labor/fragebogen  Die Fragen (Komfort mit Wortlaut; SUS und NASA-TLX mit Platzhalter, solange kein Wortlaut mit Quelle da ist: wortlautVorhanden)
//   POST /api/xr/labor/fragebogen  { name, sitzung?, app?, varianten?, komfort?{ uebelkeit, augen, natuerlich, text }, sus?[10], tlx?{ mental … frustration }, notiz? } → gespeicherter Fragebogen mit Punkten
//   POST /api/xr/labor/variante    { name, option } an alle Brillen (als Stellschraube labor.variante.<name>, gemerkt); option null = Vorgabe → { gesendet, stellschrauben }
//   POST /api/xr/labor/aufgabe     { aufgabe } startet eine Prüfaufgabe in den verbundenen Brillen (Stellschraube labor.aufgabe, NICHT gemerkt); aufgabe null beendet → { gesendet, aufgabe }
// Fehler: { fehler, vorschlag } (400 Eingabe oder fehlerhafter Katalog, 503 Ablage).
//
// XR-Labor L1 (entwurf/labor-app.md, Zuschnitt L1): dieselben Endpunkte laufen auch im Labor-Server (src/Hlks.Labor) – ohne Kopie des Codes. Was in KICAD fest ist, nimmt das Labor
// über <see cref="XrLaborQuelle"/>: Katalogordner, die Quelle des Profils «gilt für die App» (Labor: labor\profile statt Benutzereinstellungen), die Ablage der Fragebogen
// (Labor: labor\fragebogen mit Rechnername) und – nur Labor – die Marke der Brillen-App, an die Varianten und Prüfaufgaben gehen («xrlabor»; ohne solche Brille an alle, mit «hinweis»).
// Die Messprotokolle legt der Kanal ab (XrKanal, XrMessAblage); die Auswertung liest alle Dateien des Ordners. KICAD ruft die Überladung mit dem ProjektDienst: Verhalten wie vor L1.

using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

/// <summary>
/// Woher die Labor-Endpunkte ihre Ablage nehmen (siehe Dateikopf). Alle Funktionen werden je Anfrage aufgerufen.
/// </summary>
public sealed class XrLaborQuelle
{
    /// <summary>Ordner mit den vier Katalogdateien (xr_stellschrauben.json …).</summary>
    public required Func<string> KatalogOrdner { get; init; }
    /// <summary>Das Profil «gilt für die App»: Kennung und Inhalt { name, werte }; (null, null) = keines. Darf nicht werfen ausser bei Ablagefehlern (IOException, UnauthorizedAccessException, HlksFehler → gilt als keines).</summary>
    public required Func<(string? Kennung, JsonNode? Profil)> AktivesProfil { get; init; }
    /// <summary>Speicher der Fragebogen (eine neue Instanz oder dieselbe je Aufruf).</summary>
    public required Func<XrFragebogenSpeicher> Fragebogen { get; init; }
    /// <summary>Id der Brillen-App (<c>hallo.marke</c>), an die Varianten und Prüfaufgaben gehen, wenn eine solche Brille verbunden ist; null = immer an alle (KICAD).</summary>
    public string? NurMarke { get; init; }
    /// <summary>Name der App für den Hinweis («XR-Labor»).</summary>
    public string MarkeName { get; init; } = "";
}

public static class XrLaborEndpunkte
{
    static readonly object Sperre = new();
    static (string Ordner, string Stand, XrLaborKatalog Katalog)? gemerkt;

    /// <summary>Der geprüfte Katalog; wird neu gelesen, sobald sich eine der vier Dateien ändert (Zeit und Grösse).</summary>
    public static XrLaborKatalog KatalogHolen(string ordner)
    {
        var stand = string.Join('|', new[] { XrLaborKatalog.DateiStellschrauben, XrLaborKatalog.DateiAufgaben, XrLaborKatalog.DateiVarianten, XrLaborKatalog.DateiFragebogen }.Select(n =>
        {
            var fi = new FileInfo(Path.Combine(ordner, n));
            return fi.Exists ? $"{fi.LastWriteTimeUtc.Ticks}:{fi.Length}" : "fehlt";
        }));
        lock (Sperre)
        {
            if (gemerkt is { } g && g.Ordner == ordner && g.Stand == stand) return g.Katalog;
            var k = XrLaborKatalog.Laden(ordner);
            gemerkt = (ordner, stand, k);
            return k;
        }
    }

    /// <summary>KICAD: Katalog aus den Einstellungen, Profil und Fragebogen wie vor L1 (Benutzereinstellungen, %LOCALAPPDATA%\HLKS_CAD\xr\fragebogen).</summary>
    public static WebApplication MapXrLabor(this WebApplication app, ProjektDienst dienst)
    {
        var einstellungen = app.Services.GetRequiredService<XrEinstellungen>();
        return app.MapXrLabor(new XrLaborQuelle
        {
            KatalogOrdner = () => dienst.Einstellungen.KatalogOrdner,
            AktivesProfil = () =>
            {
                if (dienst.Benutzer.Hole(XrLaborProfil.AktivSchluessel) is JsonValue v && v.TryGetValue<string>(out var kn) && XrLaborProfil.KennungGueltig(kn))
                    return (kn, dienst.Benutzer.Hole(XrLaborProfil.Vorsatz + kn));
                return (null, null);
            },
            Fragebogen = () => new XrFragebogenSpeicher(Path.Combine(einstellungen.OrdnerPfad(), "fragebogen"), einstellungen.Jetzt),
        });
    }

    /// <summary>Die Labor-Endpunkte mit einer gegebenen Quelle (KICAD: Überladung oben; Labor: src/Hlks.Labor/LaborDienste.cs).</summary>
    public static WebApplication MapXrLabor(this WebApplication app, XrLaborQuelle quelle)
    {
        var kanal = app.Services.GetRequiredService<XrKanal>();
        var g = app.MapGroup("/api/xr/labor");
        XrLaborKatalog Katalog() => KatalogHolen(quelle.KatalogOrdner());

        g.MapGet("/katalog", (HttpRequest r) => Gesichert(() =>
        {
            var k = Katalog();
            string? kennung = null;
            JsonNode? profil = null;
            try { (kennung, profil) = quelle.AktivesProfil(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HlksFehler) { kennung = null; profil = null; }   // ohne lesbare Einstellungen gilt die Vorgabe
            var ctx = r.HttpContext;   // (HttpContext) als Parameter wäre ein RequestDelegate: der zurückgegebene IResult würde nie geschrieben
            var antwort = k.Zusammen(kennung, profil);
            var etag = new EntityTagHeaderValue($"\"{(string)antwort["schluessel"]!}\"");
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (Passt(ctx.Request, etag)) return Task.FromResult(Results.StatusCode(StatusCodes.Status304NotModified));
            return Task.FromResult(Antwort(antwort));
        })).XrImNetz();

        g.MapGet("/auswertung", () => Gesichert(() =>
        {
            var k = Katalog();
            var fragebogen = quelle.Fragebogen().Liste();
            return Task.FromResult(Antwort(XrLaborAuswertung.Lesen(kanal.ProtokollOrdner, k, fragebogen)));
        }));

        g.MapGet("/fragebogen", () => Gesichert(() => Task.FromResult(Antwort(Katalog().Fragebogen.Antwort()))));

        g.MapPost("/fragebogen", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            return Antwort(quelle.Fragebogen().Speichern(p, Katalog()));
        }));

        g.MapPost("/variante", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var k = Katalog();
            var name = Param.Text(p, "name", true)!;
            if (k.VarianteVorgabe(name) is null)
                throw new HlksFehler($"Die Variante «{(name.Length > 20 ? name[..20] : name)}» gibt es nicht.", "Die Namen stehen in kataloge/xr_varianten.json: " + string.Join(", ", k.VariantenNamen));
            var option = p["option"] is null ? null : Param.Text(p, "option");
            if (option is not null && !k.VarianteBekannt(name, option))
                throw new HlksFehler($"Die Option «{(option.Length > 20 ? option[..20] : option)}» gibt es für «{name}» nicht.", "Die Optionen stehen in kataloge/xr_varianten.json.");
            var gesendet = kanal.StellschraubeSetzen(XrLaborKatalog.VarianteVorsatz + name, option is null ? null : JsonValue.Create(option), null, quelle.NurMarke);
            var antwort = new JsonObject { ["gesendet"] = gesendet, ["stellschrauben"] = kanal.Stellschrauben() };
            if (Hinweis(quelle, kanal) is { } hinweis) antwort["hinweis"] = hinweis;
            return Antwort(antwort);
        }));

        g.MapPost("/aufgabe", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var k = Katalog();
            var id = p["aufgabe"] is null ? null : Param.Text(p, "aufgabe");
            if (id is not null)
            {
                if (!k.AufgabeBekannt(id))
                    throw new HlksFehler($"Die Aufgabe «{(id.Length > 40 ? id[..40] : id)}» gibt es nicht.", "Die Ids stehen in kataloge/xr_pruefaufgaben.json: " + string.Join(", ", k.AufgabenIds));
                if (k.AufgabeStatusVon(id) != "p5")
                    throw new HlksFehler($"Die Aufgabe «{id}» ist noch nicht in der App (folgt mit Version 1).", "Eine Aufgabe mit Status «p5» wählen.");
            }
            var alle = kanal.TeilnehmerListe().OfType<JsonObject>().ToList();
            // XR-Labor L1: nur an Brillen der Labor-App, wenn eine solche verbunden ist; sonst an alle (mit Hinweis)
            var ziele = quelle.NurMarke is { } marke && alle.Any(t => (string?)t["marke"] == marke) ? alle.Where(t => (string?)t["marke"] == marke).ToList() : alle;
            var teilnehmer = ziele.Select(t => (string)t["id"]!).ToList();
            if (id is not null && teilnehmer.Count == 0)
                throw new HlksFehler("Es ist keine Brille verbunden.", quelle.NurMarke is null
                    ? "Die App in der Brille starten und mit KICAD verbinden (Dialog «Quest verbinden»)."
                    : $"Die App «{quelle.MarkeName}» in der Brille starten und mit dem Labor verbinden (Register «Brille»).");
            var gesendet = 0;
            foreach (var t in teilnehmer)
            {
                try { gesendet += kanal.StellschraubeSetzen(XrLaborKatalog.AufgabeSchluessel, id is null ? null : JsonValue.Create(id), t); }
                catch (HlksFehler) { /* der Teilnehmer ging inzwischen weg */ }
            }
            var antwort = new JsonObject { ["gesendet"] = gesendet, ["aufgabe"] = id };
            if (Hinweis(quelle, kanal) is { } hinweis) antwort["hinweis"] = hinweis;
            return Antwort(antwort);
        }));
        return app;
    }

    /// <summary>Hinweis, wenn Brillen verbunden sind, aber keine mit der gewünschten Marke: der Auftrag ging an alle. Sonst null (immer null in KICAD).</summary>
    static string? Hinweis(XrLaborQuelle quelle, XrKanal kanal) =>
        quelle.NurMarke is { } marke && kanal.Anzahl > 0 && kanal.AnzahlMarke(marke) == 0
            ? $"Keine Brille mit der App «{quelle.MarkeName}» verbunden: der Auftrag ging an alle {kanal.Anzahl} verbundenen Brillen."
            : null;

    /// <summary>If-None-Match enthält diesen ETag (schwacher Vergleich) oder «*».</summary>
    static bool Passt(HttpRequest r, EntityTagHeaderValue etag)
    {
        var kopf = r.Headers.IfNoneMatch;
        if (kopf.Count == 0) return false;
        if (!EntityTagHeaderValue.TryParseList(kopf, out var liste)) return false;
        return liste.Any(x => x.Equals(EntityTagHeaderValue.Any) || x.Compare(etag, useStrongComparison: false));
    }
}
