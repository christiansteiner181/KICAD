// «VR-Auftrag» (Modul XR, Christian Steiner 21.10.2026: «ev. ist es einfacher die am pc eingestellten befehle (bsp. in sitzungsraum betrachten, oder in vr begehen)
// vom VR gerät aufzurufen. es bleibt ja immer die gleiche adresse auf localhost.»): Der PC setzt einen Auftrag, die VR-Startseite /vr in der Brille liest ihn alle 2 s
// und führt ihn aus (neuer Auftrag während der Sitzung: sofort, ohne neuen Tipp).
//
//   GET  /api/vr/auftrag   { auftrag: { modus, raum?, geschoss?, nr, zeit } | null }
//   POST /api/vr/auftrag   { modus: "begehen" | "sitzungsraum", raum?: Id, geschoss?: Id, autostart?: bool }  → { auftrag, autostart: gestartet | aus | nicht_angefordert | nicht_verfuegbar };
//                          autostart: true startet nach dem Speichern «Begehung beim Aufsetzen automatisch starten» (QuestStarter.cs), wenn die Einstellung quest.autostart an ist; nr zählt bei jedem Setzen hoch (vom Server vergeben, ein nr im Körper zählt nicht)
//
// Gespeichert im Speicher des Servers und zusätzlich in den BenutzerEinstellungen (Schlüssel vr.auftrag), damit er einen Neustart übersteht; die Ablage ist nur eine
// Zugabe (im Testlauf ohne Umleitung nach %TEMP% wird sie abgelehnt, der Speicher gilt trotzdem). Keine Fachlogik: der Auftrag ist nur eine Anweisung für die Anzeige,
// ändert weder Modell noch Rechner; ob eine Id ein Raum ist, entscheidet die Seite mit der Kern-Abfrage raum_begehen. Hinter dem Herkunftsschutz wie alle /api.
// KI: kein MCP-Werkzeug, siehe KiEinordnung.Oberflaechenfunktionen.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

public sealed record VrAuftragWert(string Modus, string? Raum, string? Geschoss, long Nr, DateTimeOffset Zeit);

public sealed class VrAuftragSpeicher
{
    public const string Schluessel = "vr.auftrag";
    public static readonly string[] Modi = ["begehen", "sitzungsraum"];
    static readonly Regex IdMuster = new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant);
    static readonly Regex KennzeichenMuster = new(@"^[A-Za-z0-9][A-Za-z0-9._\-]{0,39}$", RegexOptions.CultureInvariant);

    readonly BenutzerEinstellungen benutzer;
    readonly object sperre = new();
    VrAuftragWert? aktuell;
    bool geladen;
    public Func<DateTimeOffset> Uhr { get; set; } = () => DateTimeOffset.UtcNow;

    public VrAuftragSpeicher(BenutzerEinstellungen benutzer) { this.benutzer = benutzer; }

    /// <summary>Id (Guid) oder Kennzeichen eines Elements; sonst Fehler.</summary>
    public static bool IdGueltig(string? t) => t is not null && (IdMuster.IsMatch(t) || KennzeichenMuster.IsMatch(t));

    /// <summary>Prüft Körper und baut den neuen Auftrag (ohne Nr und Zeit zu vergeben). Wirft HlksFehler mit Vorschlag.</summary>
    public static (string Modus, string? Raum, string? Geschoss) Pruefen(JsonObject p)
    {
        var modus = Param.Text(p, "modus", true)!;
        if (!Modi.Contains(modus, StringComparer.Ordinal))
            throw new HlksFehler($"Der Modus «{modus}» ist unbekannt.", "Erlaubt sind «begehen» und «sitzungsraum».");
        var raum = Param.Text(p, "raum");
        var geschoss = Param.Text(p, "geschoss");
        if (raum is not null && !IdGueltig(raum)) throw new HlksFehler("Der Raum hat keine gültige Id.", "Die Id (oder das Kennzeichen) eines Raums angeben.");
        if (geschoss is not null && !IdGueltig(geschoss)) throw new HlksFehler("Das Geschoss hat keine gültige Id.", "Die Id (oder das Kennzeichen) eines Geschosses angeben.");
        return (modus, raum, geschoss);
    }

    void Laden()
    {
        if (geladen) return;
        geladen = true;
        try
        {
            if (benutzer.Hole(Schluessel) is JsonObject o && o["modus"]?.GetValue<string>() is { } m && Modi.Contains(m)
                && (o["nr"] is JsonValue nv && nv.TryGetValue<long>(out var nr)))
                aktuell = new VrAuftragWert(m, o["raum"]?.GetValue<string>(), o["geschoss"]?.GetValue<string>(), nr,
                    DateTimeOffset.TryParse(o["zeit"]?.GetValue<string>(), out var z) ? z : Uhr());
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or IOException or UnauthorizedAccessException) { /* gespeicherter Wert unbrauchbar: leer beginnen */ }
    }

    public VrAuftragWert? Lesen() { lock (sperre) { Laden(); return aktuell; } }

    public VrAuftragWert Setzen(JsonObject p)
    {
        var (modus, raum, geschoss) = Pruefen(p);
        lock (sperre)
        {
            Laden();
            var neu = new VrAuftragWert(modus, raum, geschoss, (aktuell?.Nr ?? 0) + 1, Uhr());
            aktuell = neu;
            try { benutzer.Setzen(Schluessel, Zuobjekt(neu)); }
            catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { /* nur eine Zugabe: der Speicher des Servers gilt */ }
            return neu;
        }
    }

    public static JsonObject Zuobjekt(VrAuftragWert a) => new()
    {
        ["modus"] = a.Modus, ["raum"] = a.Raum, ["geschoss"] = a.Geschoss, ["nr"] = a.Nr, ["zeit"] = a.Zeit.ToString("o"),
    };
}

public static class VrAuftragEndpunkte
{
    public static WebApplication MapVrAuftrag(this WebApplication app, ProjektDienst dienst, QuestStarter? starter = null)
    {
        var speicher = new VrAuftragSpeicher(dienst.Benutzer);
        var g = app.MapGroup("/api/vr");
        g.MapGet("/auftrag", () => Gesichert(() => Task.FromResult(Antwort(new { auftrag = speicher.Lesen() is { } a ? VrAuftragSpeicher.Zuobjekt(a) : null }))));
        g.MapPost("/auftrag", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var a = speicher.Setzen(p);
            // Autostart (QuestStarter.cs): nur auf Wunsch des Menschen (autostart: true im Körper) und nur mit eingeschalteter Einstellung quest.autostart. Der Port ist der des Servers.
            var gewuenscht = p["autostart"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            var autostart = !gewuenscht ? "nicht_angefordert" : starter is null ? "nicht_verfuegbar" : starter.Starten(r.Host.Port ?? 80, a.Nr) ? "gestartet" : "aus";
            return Antwort(new { auftrag = VrAuftragSpeicher.Zuobjekt(a), autostart });
        }));
        return app;
    }
}
