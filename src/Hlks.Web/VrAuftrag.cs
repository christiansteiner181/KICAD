// «VR-Auftrag» (Modul XR, Christian Steiner 21.10.2026: «ev. ist es einfacher die am pc eingestellten befehle (bsp. in sitzungsraum betrachten, oder in vr begehen)
// vom VR gerät aufzurufen. es bleibt ja immer die gleiche adresse auf localhost.»): Der PC setzt einen Auftrag, die VR-Startseite /vr in der Brille liest ihn alle 2 s
// und führt ihn aus (neuer Auftrag während der Sitzung: sofort, ohne neuen Tipp).
//
//   GET  /api/vr/auftrag   { auftrag: { modus, raum?, geschoss?, ausschnitt, ansicht?, nr, zeit } | null }
//   POST /api/vr/auftrag   { modus: "begehen" | "sitzungsraum", raum?: Id, geschoss?: Id, ausschnitt?: bool, ansicht?: Id, autostart?: bool }  → { auftrag, autostart: gestartet | aus | nicht_angefordert | nicht_verfuegbar | app | app_gestartet };
//                          autostart: true startet nach dem Speichern «Begehung beim Aufsetzen automatisch starten» (QuestStarter.cs), wenn die Einstellung quest.autostart an ist; nr zählt bei jedem Setzen hoch (vom Server vergeben, ein nr im Körper zählt nicht)
//
// Gespeichert im Speicher des Servers und zusätzlich in den BenutzerEinstellungen (Schlüssel vr.auftrag), damit er einen Neustart übersteht; die Ablage ist nur eine
// Zugabe (im Testlauf ohne Umleitung nach %TEMP% wird sie abgelehnt, der Speicher gilt trotzdem). Keine Fachlogik: der Auftrag ist nur eine Anweisung für die Anzeige,
// ändert weder Modell noch Rechner; ob eine Id ein Raum ist, entscheidet die Seite mit der Kern-Abfrage raum_begehen. Hinter dem Herkunftsschutz wie alle /api.
// KI: kein MCP-Werkzeug, siehe KiEinordnung.Oberflaechenfunktionen.
// Quest App (P3): Ein neuer Auftrag geht zusätzlich sofort über den Live-Kanal an die App (Ereignis Gesetzt → XrKanal.AuftragSenden); P7: ist eine App verbunden, meldet die Antwort autostart «app» und KICAD startet keinen Browser in der Brille; die Abfrage alle 2 s
// bleibt für die VR-Startseite im Browser.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;
using static Hlks.Web.EndpunktHilfe;

namespace Hlks.Web;

/// <summary>Der Auftrag. Ausschnitt: «Raum zeigen (VR)» – der Sitzungsraum zeigt nur den Raum plus Rand (Quader wie «Raum im 3D zeigen», N14); Ansicht: eine 3D-Ansicht (Schnittbereich und Filter) auf dem Tisch.</summary>
public sealed record VrAuftragWert(string Modus, string? Raum, string? Geschoss, long Nr, DateTimeOffset Zeit, bool Ausschnitt = false, string? Ansicht = null);

/// <summary>Geprüfte Teile eines Auftrags (ohne Nr und Zeit).</summary>
public sealed record VrAuftragTeile(string Modus, string? Raum, string? Geschoss, bool Ausschnitt, string? Ansicht);

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
    /// <summary>Ein neuer Auftrag ist gesetzt (nach dem Speichern). Der Live-Kanal der Quest App (XrKanal.cs) schickt ihn damit sofort an die Brille.</summary>
    public event Action<VrAuftragWert>? Gesetzt;

    public VrAuftragSpeicher(BenutzerEinstellungen benutzer) { this.benutzer = benutzer; }

    /// <summary>
    /// Quest App V1a (Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»): der Symbolsatz des Benutzers (Benutzereinstellung «oberflaeche.symbole»,
    /// «strich» oder «revit»); der Live-Kanal schickt ihn mit «willkommen», damit die App dieselben Symbole zeigt wie KICAD. Unlesbar oder unbekannt: «strich».
    /// </summary>
    public string Symbolsatz()
    {
        try { return benutzer.Hole("oberflaeche.symbole") is JsonValue v && v.TryGetValue<string>(out var s) && s == "revit" ? "revit" : "strich"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return "strich"; }
    }

    /// <summary>Id (Guid) oder Kennzeichen eines Elements; sonst Fehler.</summary>
    public static bool IdGueltig(string? t) => t is not null && (IdMuster.IsMatch(t) || KennzeichenMuster.IsMatch(t));

    /// <summary>Prüft Körper und baut den neuen Auftrag (ohne Nr und Zeit zu vergeben). Wirft HlksFehler mit Vorschlag.</summary>
    public static VrAuftragTeile Pruefen(JsonObject p)
    {
        var modus = Param.Text(p, "modus", true)!;
        if (!Modi.Contains(modus, StringComparer.Ordinal))
            throw new HlksFehler($"Der Modus «{modus}» ist unbekannt.", "Erlaubt sind «begehen» und «sitzungsraum».");
        var raum = Param.Text(p, "raum");
        var geschoss = Param.Text(p, "geschoss");
        if (raum is not null && !IdGueltig(raum)) throw new HlksFehler("Der Raum hat keine gültige Id.", "Die Id (oder das Kennzeichen) eines Raums angeben.");
        if (geschoss is not null && !IdGueltig(geschoss)) throw new HlksFehler("Das Geschoss hat keine gültige Id.", "Die Id (oder das Kennzeichen) eines Geschosses angeben.");
        bool ausschnitt = false;
        if (p["ausschnitt"] is { } av)
            ausschnitt = av is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new HlksFehler("Der Parameter «ausschnitt» muss true oder false sein.", "ausschnitt: true zeigt im Sitzungsraum nur den Raum plus Rand.");
        var ansicht = Param.Text(p, "ansicht");
        if (ansicht is not null && !IdGueltig(ansicht)) throw new HlksFehler("Die Ansicht hat keine gültige Id.", "Die Id (oder das Kennzeichen) einer 3D-Ansicht angeben.");
        if (ausschnitt && (modus != "sitzungsraum" || raum is null))
            throw new HlksFehler("Ein Ausschnitt braucht den Modus «sitzungsraum» und einen Raum.", "Form: { \"modus\": \"sitzungsraum\", \"raum\": <Id>, \"ausschnitt\": true }.");
        if (ansicht is not null && (modus != "sitzungsraum" || raum is not null || ausschnitt))
            throw new HlksFehler("Eine Ansicht gehört zum Modus «sitzungsraum» und schliesst Raum und Ausschnitt aus.", "Form: { \"modus\": \"sitzungsraum\", \"ansicht\": <Id> }.");
        return new VrAuftragTeile(modus, raum, geschoss, ausschnitt, ansicht);
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
                    DateTimeOffset.TryParse(o["zeit"]?.GetValue<string>(), out var z) ? z : Uhr(),
                    o["ausschnitt"] is JsonValue av && av.TryGetValue<bool>(out var ab) && ab, o["ansicht"]?.GetValue<string>());
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or IOException or UnauthorizedAccessException) { /* gespeicherter Wert unbrauchbar: leer beginnen */ }
    }

    public VrAuftragWert? Lesen() { lock (sperre) { Laden(); return aktuell; } }

    public VrAuftragWert Setzen(JsonObject p, Action<string>? ansichtPruefen = null)
    {
        var t = Pruefen(p);
        if (t.Ansicht is not null) ansichtPruefen?.Invoke(t.Ansicht);   // Klasse Ansicht mit art 3d (nur wenn ein Projekt offen ist)
        VrAuftragWert neu;
        lock (sperre)
        {
            Laden();
            neu = new VrAuftragWert(t.Modus, t.Raum, t.Geschoss, (aktuell?.Nr ?? 0) + 1, Uhr(), t.Ausschnitt, t.Ansicht);
            aktuell = neu;
            try { benutzer.Setzen(Schluessel, Zuobjekt(neu)); }
            catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { /* nur eine Zugabe: der Speicher des Servers gilt */ }
        }
        try { Gesetzt?.Invoke(neu); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { /* Weitergabe an die Brille ist eine Zugabe; der Auftrag gilt */ }
        return neu;
    }

    public static JsonObject Zuobjekt(VrAuftragWert a) => new()
    {
        ["modus"] = a.Modus, ["raum"] = a.Raum, ["geschoss"] = a.Geschoss, ["ausschnitt"] = a.Ausschnitt, ["ansicht"] = a.Ansicht, ["nr"] = a.Nr, ["zeit"] = a.Zeit.ToString("o"),
    };
}

public static class VrAuftragEndpunkte
{
    public static WebApplication MapVrAuftrag(this WebApplication app, ProjektDienst dienst, QuestStarter? starter = null)
    {
        // derselbe Speicher wie der Live-Kanal der Quest App (XrZugangDienste.AddXrZugang), damit ein neuer Auftrag sofort an die Brille geht
        var speicher = app.Services.GetService<VrAuftragSpeicher>() ?? new VrAuftragSpeicher(dienst.Benutzer);
        var g = app.MapGroup("/api/vr");
        g.MapGet("/auftrag", () => Gesichert(() => Task.FromResult(Antwort(new { auftrag = speicher.Lesen() is { } a ? VrAuftragSpeicher.Zuobjekt(a) : null }))));
        g.MapPost("/auftrag", (HttpRequest r) => Gesichert(async () =>
        {
            var p = await KoerperLesen(r);
            var a = speicher.Setzen(p, bezug =>
            {
                // ist ein Projekt offen, muss die Ansicht darin eine 3D-Ansicht sein; ohne Projekt gilt nur das Muster (die Seite prüft beim Ausführen)
                if (dienst.Projekt is not { } pr) return;
                var e = pr.Modell.Finde(bezug) ?? throw new HlksFehler($"Die Ansicht «{bezug}» gibt es nicht.", "Kennzeichen oder Id einer 3D-Ansicht angeben.");
                if (e.Klasse != "Ansicht" || (e.Text("art") ?? "grundriss") != "3d")
                    throw new HlksFehler($"{e.Kennzeichen} ist keine 3D-Ansicht.", "Eine 3D-Ansicht angeben (Klasse Ansicht, art 3d).");
            });
            // Autostart (QuestStarter.cs): nur auf Wunsch des Menschen (autostart: true im Körper) und nur mit eingeschalteter Einstellung quest.autostart. Der Port ist der des Servers.
            var gewuenscht = p["autostart"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            // Quest App (P7): Ist eine Brille mit der App im Live-Kanal, ging der Auftrag schon über den Kanal an sie (Ereignis Gesetzt). Dann startet KICAD keinen Browser in der Brille
            // (der Autostart würde den Browser über die laufende App legen und dieselbe Begehung ein zweites Mal öffnen). Antwort «app»; ohne App gilt der Weg über den Browser wie bisher.
            var appVerbunden = (app.Services.GetService<XrKanal>()?.Anzahl ?? 0) > 0;
            // Ist die App installiert und gekoppelt, aber gerade nicht verbunden (Brille sleept, App beendet), wird sie gestartet statt des Browsers; der Auftrag kommt über den Kanal,
            // sobald sie verbindet (willkommen.auftrag). Browser-Autostart nur noch, wenn keine App da ist.
            var appGestartet = !appVerbunden && starter is not null && await starter.Quest.AppFuerAuftragStarten();
            var autostart = appVerbunden ? "app" : appGestartet ? "app_gestartet" : !gewuenscht ? "nicht_angefordert" : starter is null ? "nicht_verfuegbar" : starter.Starten(r.Host.Port ?? 80, a.Nr) ? "gestartet" : "aus";
            return Antwort(new { auftrag = VrAuftragSpeicher.Zuobjekt(a), autostart });
        }));
        return app;
    }
}
