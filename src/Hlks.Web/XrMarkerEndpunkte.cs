// Quest App V1a (Modul XR, XR-E «Problem-Marker mit Nachverfolgung»): Marker aus der Brille in KICAD und die Liste für die App. In Program.cs hängt nur
// «app.MapXrMarker(dienst);» sie ein.
//
// Ablage: im PROJEKT, nicht unter %LOCALAPPDATA% – ein Marker ist ein Element der Klasse «ProblemMarker» (kataloge/klassen.json), angelegt mit dem normalen Befehl
// element_anlegen des Kerns. Damit gilt das Muster aller Projektdaten («Eine Tür», entwurf/modul-xr.md Rahmen: «Jede Änderung in VR ist ein normaler Befehl des Kerns,
// mit Paket, Rückgängig und Abgleich wie am PC»): ein Paket in aenderungen/, Rückgängig, Abgleich über die Projektablage – alle KICAD-Plätze sehen ihn; Status,
// Zuständig, Frist und Notiz ändert die Palette «Problem-Marker» am PC mit element_aendern. Die KI liest Marker wie jedes Element (elemente_suchen klasse ProblemMarker).
//
// Für die Brille (Marke XrImNetz: im Netzzugang nur über HTTPS mit Schlüssel; auf localhost wie alle /api):
//   GET  /api/xr/marker   → { projekt, marker: [{ id, kennzeichen, kennung, ort: [x, y, z] mm, blick, element: { id, teil, art } | null, geschoss, zeit, benutzer,
//                           brille, notiz, status, zustaendig, frist }] }, ETag über den Inhalt, If-None-Match → 304; 409 ohne offenes Projekt
//   POST /api/xr/marker   { kennung, ort, blick: { kopf, richtung, modus, massstab }, element?, geschoss?, zeit?, notiz? } (Format Hlks.Xr.Marker.MarkerNeu)
//                         → 201 { marker } bzw. 200 { marker, schon: true }, wenn ein Marker mit dieser Kennung schon angelegt ist (die App sendet nach einer
//                           Trennung nochmals); 400 bei ungültigen Werten. benutzer = Name der Kopplung (bzw. der Bearbeiter am PC), brille = Kennung der Brille.
// Einordnung: Rechner-Funktion «xr_marker» (KiEinordnung) – eine Schnittstelle für das Gerät des Menschen, kein MCP-Werkzeug; die Marker selbst sind Projektdaten.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using Microsoft.Net.Http.Headers;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public static partial class XrMarker
{
    public const string Klasse = "ProblemMarker";
    public const int NotizMax = 500, KennungMax = 64, IdMax = 120, ZeitMax = 40;
    public static readonly IReadOnlyList<string> Modi = ["begehen", "sitzungsraum"];

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex KennungMuster();

    static HlksFehler F(string m, string? v = null) => new(m, v ?? "Format: { kennung, ort: [x, y, z] mm, blick: { kopf, richtung, modus, massstab }, element?, geschoss?, zeit?, notiz? }");

    static double[] Punkt(JsonNode? n, string name, double grenze = 1e8)
    {
        if (n is not JsonArray a || a.Count != 3) throw F($"«{name}» muss [x, y, z] sein.");
        var w = new double[3];
        for (int i = 0; i < 3; i++)
            w[i] = a[i] is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) && Math.Abs(d) <= grenze ? d : throw F($"«{name}» enthält keine gültige Zahl.");
        return w;
    }

    static string? Text(JsonObject o, string name, int max)
    {
        if (o[name] is null) return null;
        if (o[name] is not JsonValue v || !v.TryGetValue<string>(out var s)) throw F($"«{name}» muss ein Text sein.");
        s = new string([.. s.Where(c => c == '\n' || !char.IsControl(c))]).Trim();
        if (s.Length > max) throw F($"«{name}» ist zu lang (höchstens {max} Zeichen).");
        return s.Length == 0 ? null : s;
    }

    /// <summary>
    /// Parameter für element_anlegen aus dem Körper der Brille (geprüft, siehe Dateikopf); benutzer und brille aus der Kopplung. Das betroffene Element kommt als
    /// «beteiligte» (Element-Id bzw. GlobalId) – kein Bezug, damit es sich weiter löschen lässt.
    /// </summary>
    public static JsonObject BefehlAus(JsonObject k, Modell m, string benutzer, string? brille, DateTimeOffset jetzt)
    {
        var kennung = Text(k, "kennung", KennungMax) ?? throw F("«kennung» fehlt.");
        if (!KennungMuster().IsMatch(kennung)) throw F("«kennung» darf nur Buchstaben, Ziffern, - und _ enthalten (höchstens 64).");
        var ort = Punkt(k["ort"], "ort");
        if (k["blick"] is not JsonObject b) throw F("«blick» fehlt.");
        var kopf = Punkt(b["kopf"], "blick.kopf");
        var r = Punkt(b["richtung"], "blick.richtung", 10);
        var l = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
        if (l < 1e-6) throw F("«blick.richtung» darf nicht null sein.");
        var modus = Text(b, "modus", 20) ?? "begehen";
        if (!Modi.Contains(modus)) throw F($"«blick.modus» muss {string.Join(" oder ", Modi)} sein.");
        var massstab = b["massstab"] is null ? 1 : b["massstab"] is JsonValue mv && mv.TryGetValue<double>(out var ms) && double.IsFinite(ms) && ms >= 1 && ms <= 10000 ? ms
            : throw F("«blick.massstab» muss eine Zahl von 1 bis 10000 sein.");
        var eig = new JsonObject
        {
            ["status"] = "offen", ["kennung"] = kennung, ["benutzer"] = benutzer, ["quelle"] = "brille",
            ["blickpunkt"] = new JsonObject
            {
                ["kopf"] = new JsonArray(kopf.Select(x => (JsonNode)Math.Round(x, 1)).ToArray()),
                ["richtung"] = new JsonArray(r.Select(x => (JsonNode)Math.Round(x / l, 5)).ToArray()), ["modus"] = modus, ["massstab"] = massstab,
            },
        };
        if (brille is not null) eig["brille"] = brille;
        if (Text(k, "notiz", NotizMax) is { } notiz) eig["notiz"] = notiz;
        var zeit = Text(k, "zeit", ZeitMax);
        eig["zeit"] = zeit is not null && DateTimeOffset.TryParse(zeit, CultureInfo.InvariantCulture, DateTimeStyles.None, out var z)
            ? z.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture) : jetzt.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);
        if (k["element"] is JsonObject el && Text(el, "id", IdMax) is { } eid)
        {
            var teil = Text(el, "teil", 20) ?? "modell";
            // Modell: nur ein Element, das es gibt; Unterlage: die GlobalId der IFC (BcfAufloesung versteht sie)
            if (teil == "modell" ? m.Finde(eid) is not null : teil == "unterlage" && BcfFormat.IstIfcGuid(eid)) eig["beteiligte"] = new JsonArray(eid);
        }
        var par = new JsonObject
        {
            ["klasse"] = Klasse, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(ort.Select(x => (JsonNode)Math.Round(x, 1)).ToArray())) },
        };
        if (Text(k, "geschoss", IdMax) is { } g && m.Finde(g) is { Klasse: "Geschoss" } ge) par["geschoss"] = ge.Id;
        return par;
    }

    /// <summary>Ein Marker für die App (siehe Dateikopf).</summary>
    public static JsonObject Eintrag(Projekt p, Element e)
    {
        var pt = e.Geometrie?.Punkte.FirstOrDefault() is { Length: >= 3 } q ? q : [0, 0, 0];
        JsonObject? element = null;
        if (e.Eigenschaft("beteiligte")?.Inhalt is JsonArray bt && bt.FirstOrDefault() is JsonValue bv && bv.TryGetValue<string>(out var bid))
            element = p.Modell.Finde(bid) is { } be ? new JsonObject { ["id"] = be.Id, ["teil"] = "modell", ["art"] = be.Klasse } : new JsonObject { ["id"] = bid, ["teil"] = "unterlage", ["art"] = null };
        return new JsonObject
        {
            ["id"] = e.Id, ["kennzeichen"] = e.Kennzeichen, ["kennung"] = e.Text("kennung"),
            ["ort"] = new JsonArray(pt.Take(3).Select(x => (JsonNode)x).ToArray()),
            ["blick"] = e.Eigenschaft("blickpunkt")?.Inhalt?.DeepClone(), ["element"] = element, ["geschoss"] = e.Geschoss,
            ["zeit"] = e.Text("zeit") ?? e.Angelegt?.Zeit.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
            ["benutzer"] = e.Text("benutzer") ?? e.Angelegt?.Akteur, ["brille"] = e.Text("brille"), ["notiz"] = e.Text("notiz"),
            ["status"] = e.Text("status") ?? "offen", ["zustaendig"] = e.Text("zustaendig"), ["frist"] = e.Text("frist"),
        };
    }

    /// <summary>Alle Marker des Projekts (Kennzeichen natürlich sortiert).</summary>
    public static JsonObject Liste(Projekt p) => new()
    {
        ["projekt"] = p.Kopf.Id,
        ["marker"] = new JsonArray([.. p.Modell.Elemente.Where(e => p.Katalog.IstArt(e, Klasse)).OrderBy(e => e.Kennzeichen, BcfFormat.Natuerlich)
            .ThenBy(e => e.Id, StringComparer.Ordinal).Select(e => (JsonNode)Eintrag(p, e))]),
    };

    /// <summary>Marker mit dieser Kennung, sonst null.</summary>
    public static Element? MitKennung(Projekt p, string kennung) =>
        p.Modell.Elemente.FirstOrDefault(e => p.Katalog.IstArt(e, Klasse) && e.Text("kennung") == kennung);

    /// <summary>Legt den Marker an (bzw. findet ihn wieder): (Eintrag, schon da).</summary>
    public static (JsonObject Marker, bool Schon) Anlegen(Projekt p, JsonObject koerper, string benutzer, string? brille, DateTimeOffset jetzt)
    {
        var par = BefehlAus(koerper, p.Modell, benutzer, brille, jetzt);
        var kennung = (string)par["eigenschaften"]!["kennung"]!;
        if (MitKennung(p, kennung) is { } da) return (Eintrag(p, da), true);
        var r = p.Ausfuehren("element_anlegen", par);
        if (!r.Angenommen) throw new HlksFehler(r.Fehler ?? "Der Marker wurde nicht angelegt.", r.Vorschlag);
        return (Eintrag(p, MitKennung(p, kennung) ?? throw new HlksFehler("Der Marker fehlt nach dem Anlegen.")), false);
    }
}

public static class XrMarkerEndpunkte
{
    public static WebApplication MapXrMarker(this WebApplication app, ProjektDienst dienst)
    {
        var kopplungen = app.Services.GetRequiredService<XrKopplungen>();
        var einstellungen = app.Services.GetRequiredService<XrEinstellungen>();
        var g = app.MapGroup("/api/xr");

        g.MapGet("/marker", (HttpRequest r) => Gesichert(async () =>
        {
            var liste = await dienst.Mit(XrMarker.Liste);
            var text = Json.Text(liste);
            var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32].ToLowerInvariant()}\"");
            var ctx = r.HttpContext;
            ctx.Response.Headers.ETag = etag.ToString();
            ctx.Response.Headers.CacheControl = "no-cache";
            if (r.Headers.IfNoneMatch.Count > 0 && EntityTagHeaderValue.TryParseList(r.Headers.IfNoneMatch, out var l) && l.Any(x => x.Compare(etag, false)))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            return Results.Text(text, "application/json; charset=utf-8");
        })).XrImNetz();

        g.MapPost("/marker", (HttpRequest r) => Gesichert(async () =>
        {
            var k = await KoerperLesen(r);
            var kopplung = XrPforte.Anmeldung(r.HttpContext, kopplungen);
            var benutzer = kopplung?.Name is { Length: > 0 } n ? n : dienst.Einstellungen.Akteur;
            var (marker, schon) = await dienst.Mit(p => XrMarker.Anlegen(p, k, benutzer, kopplung?.Brille, einstellungen.Jetzt()));
            return Antwort(schon ? new JsonObject { ["marker"] = marker, ["schon"] = true } : new JsonObject { ["marker"] = marker }, schon ? 200 : 201);
        })).XrImNetz();
        return app;
    }
}
