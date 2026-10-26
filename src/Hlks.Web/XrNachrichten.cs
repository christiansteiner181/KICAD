// Nachrichten des Live-Kanals (Modul XR, Quest App P3): GET /api/xr/kanal (WebSocket, Textnachrichten, je eine JSON-Nachricht).
// Jede Nachricht hat «typ» und «fassung» (heute 1). Beschreibung für Menschen: src/Hlks.Web/OBERFLAECHE.md, Abschnitt «Modul XR: Zugang im Büronetz».
//
// Brille → PC:   hallo { name, geraet, app? }   tschuess {}   pose { kopf, links?, rechts?, modus?, massstab? }   zeigen { element?, punkt? }
//                messprotokoll { aufgabe, variante?, varianten?{ name: option }, zeitMs, fehlgriffe, abbrueche, erfolg?, stellschrauben?, notiz? }   (Auswertung: XrLaborAuswertung.cs)
// PC → Brille:   willkommen { id, teilnehmer[], auftrag?, stellschrauben{} }   hallo { id, name, geraet }   tschuess { id }
//                pose { von, … , zeit }   zeigen { von, element?, punkt?, zeit }   auftrag { auftrag }   stellschraube { schluessel, wert }
//                quittung { bezug, nr }   fehler { meldung, bezug? }
//
// Masse: Positionen in mm im Koordinatensystem des KICAD-Modells (x, y waagrecht, z nach oben; wie alle Masse im Kern), Drehungen als Quaternion
// [x, y, z, w] im selben System. Die App rechnet aus ihrer Welt (StereoKit: Meter, y oben) um; der Server prüft nur Form und Grenzen.
// Der Server reicht nie den Rohtext einer Brille weiter: er liest die Felder, prüft sie und baut die Nachricht neu (XrNachrichten.Pose …).

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Lage im Raum: Position [x, y, z] in mm, Drehung als Quaternion [x, y, z, w].</summary>
public sealed record XrLage(double[] Pos, double[] Rot);

/// <summary>Eine Hand: Lage des Handgelenks, Spitze des Zeigefingers [x, y, z] in mm, Pinch (Daumen an Zeigefinger) und optional seine Stärke 0…1.</summary>
public sealed record XrHand(XrLage Gelenk, double[] Zeigefinger, bool Pinch, double? PinchStaerke);

/// <summary>Kopf und Hände eines Teilnehmers. Modus: «begehen» oder «sitzungsraum»; Massstab: 1 = 1:1 (Sitzungsraum z. B. 0.02).</summary>
public sealed record XrPose(XrLage Kopf, XrHand? Links, XrHand? Rechts, string? Modus, double? Massstab);

/// <summary>Worauf die Brille zeigt: Element-Id bzw. Kennzeichen (null = nichts) und optional der Punkt in mm.</summary>
public sealed record XrZeigen(string? Element, double[]? Punkt);

/// <summary>Messung einer Prüfaufgabe im Interface-Labor.</summary>
public sealed record XrMessprotokoll(string Aufgabe, string? Variante, double ZeitMs, int Fehlgriffe, int Abbrueche, bool? Erfolg, JsonObject? Stellschrauben, string? Notiz, JsonObject? Varianten = null);

/// <summary>Anmeldung eines Teilnehmers.</summary>
public sealed record XrHallo(string Name, string Geraet, string? App);

/// <summary>Ein Teilnehmer, wie die anderen ihn sehen.</summary>
public sealed record XrTeilnehmerInfo(string Id, string Name, string Geraet);

/// <summary>Ungültige Nachricht (Meldung für die Brille).</summary>
public sealed class XrNachrichtFehler(string meldung) : Exception(meldung);

public static class XrNachrichten
{
    public const int Fassung = 1;
    public static readonly IReadOnlyList<string> VonBrille = ["hallo", "tschuess", "pose", "zeigen", "messprotokoll"];
    public static readonly IReadOnlyList<string> VomPc = ["willkommen", "hallo", "tschuess", "pose", "zeigen", "auftrag", "stellschraube", "quittung", "fehler"];
    public static readonly IReadOnlyList<string> Modi = ["begehen", "sitzungsraum"];

    /// <summary>Grenze für Koordinaten (mm): 10 km.</summary>
    public const double PosMax = 10_000_000;
    static readonly Regex SchluesselMuster = new(@"^[a-z][a-z0-9_.\-]{0,63}$", RegexOptions.CultureInvariant);
    /// <summary>So viele Stellschrauben (mit Varianten und Aufgabe) merkt bzw. trägt der Kanal höchstens; V1d 96 statt 64 (mit V1d 65 Stellschrauben, 10 Varianten, Aufgabe);
    /// V1g 128 (83 Stellschrauben, 12 Varianten, Aufgabe = 96; V1h 86 + 12 + 1 = 99).</summary>
    public const int StellschraubenMax = 128;
    const int VariantenMax = 12;
    static readonly Regex VarianteNameMuster = new(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant);
    static readonly Regex VarianteOptionMuster = new(@"^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);

    // ---------------------------------------------------------------- Lesen

    /// <summary>Liest Typ und Inhalt; prüft «fassung». Wirft XrNachrichtFehler.</summary>
    public static (string Typ, JsonObject Inhalt) Lesen(string text)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 16 }); }
        catch (JsonException) { throw new XrNachrichtFehler("Die Nachricht ist kein gültiges JSON."); }
        if (n is not JsonObject o) throw new XrNachrichtFehler("Die Nachricht muss ein JSON-Objekt sein.");
        var typ = o["typ"] is JsonValue tv && tv.TryGetValue<string>(out var t) ? t : throw new XrNachrichtFehler("«typ» fehlt.");
        if (o["fassung"] is not JsonValue fv || !fv.TryGetValue<int>(out var f)) throw new XrNachrichtFehler("«fassung» fehlt.");
        if (f != Fassung) throw new XrNachrichtFehler($"Fassung {f} wird nicht unterstützt (Server: {Fassung}).");
        return (typ, o);
    }

    public static XrHallo Hallo(JsonObject o) =>
        new(Text(o, "name", 40, true)!, Text(o, "geraet", 40, true)!, Text(o, "app", 40, false));

    public static XrPose Pose(JsonObject o)
    {
        var kopf = Lage(o["kopf"], "kopf") ?? throw new XrNachrichtFehler("«kopf» fehlt.");
        var modus = Text(o, "modus", 20, false);
        if (modus is not null && !Modi.Contains(modus)) throw new XrNachrichtFehler("«modus» muss «begehen» oder «sitzungsraum» sein.");
        double? massstab = null;
        if (o["massstab"] is { } m)
        {
            massstab = Zahl(m, "massstab");
            if (massstab is not (> 0 and <= 1000)) throw new XrNachrichtFehler("«massstab» muss grösser als 0 und höchstens 1000 sein.");
        }
        return new XrPose(kopf, Hand(o["links"], "links"), Hand(o["rechts"], "rechts"), modus, massstab);
    }

    public static XrZeigen Zeigen(JsonObject o)
    {
        var element = Text(o, "element", 64, false);
        if (element is not null && !VrAuftragSpeicher.IdGueltig(element)) throw new XrNachrichtFehler("«element» muss eine Id oder ein Kennzeichen sein.");
        var punkt = o["punkt"] is null ? null : Vektor(o["punkt"], 3, PosMax, "punkt");
        return new XrZeigen(element, punkt);
    }

    public static XrMessprotokoll Messprotokoll(JsonObject o)
    {
        var aufgabe = Text(o, "aufgabe", 80, true)!;
        var variante = Text(o, "variante", 80, false);
        var zeit = Zahl(o["zeitMs"] ?? throw new XrNachrichtFehler("«zeitMs» fehlt."), "zeitMs");
        if (zeit is < 0 or > 86_400_000) throw new XrNachrichtFehler("«zeitMs» muss von 0 bis 86400000 (ein Tag) sein.");
        var fehlgriffe = Ganz(o["fehlgriffe"], "fehlgriffe");
        var abbrueche = Ganz(o["abbrueche"], "abbrueche");
        bool? erfolg = o["erfolg"] is null ? null : o["erfolg"] is JsonValue ev && ev.TryGetValue<bool>(out var e) ? e : throw new XrNachrichtFehler("«erfolg» muss true oder false sein.");
        JsonObject? schrauben = null;
        if (o["stellschrauben"] is { } s)
        {
            if (s is not JsonObject so) throw new XrNachrichtFehler("«stellschrauben» muss ein Objekt sein.");
            if (so.Count > StellschraubenMax) throw new XrNachrichtFehler($"Höchstens {StellschraubenMax} Stellschrauben.");
            schrauben = [];
            foreach (var (k, w) in so)
            {
                if (!SchluesselMuster.IsMatch(k)) throw new XrNachrichtFehler($"Stellschraube «{Kuerzen(k)}»: ungültiger Schlüssel.");
                if (StellschraubeFehler(w) is { } fehler) throw new XrNachrichtFehler($"Stellschraube «{k}»: {fehler}");
                schrauben[k] = w?.DeepClone();
            }
        }
        JsonObject? varianten = null;
        if (o["varianten"] is { } vn)
        {
            if (vn is not JsonObject vo) throw new XrNachrichtFehler("«varianten» muss ein Objekt { Name: Option } sein.");
            if (vo.Count > VariantenMax) throw new XrNachrichtFehler($"Höchstens {VariantenMax} Varianten.");
            varianten = [];
            foreach (var (k, w) in vo)
            {
                if (!VarianteNameMuster.IsMatch(k)) throw new XrNachrichtFehler($"Variante «{Kuerzen(k)}»: ungültiger Name.");
                if (w is not JsonValue wv || !wv.TryGetValue<string>(out var option) || !VarianteOptionMuster.IsMatch(option)) throw new XrNachrichtFehler($"Variante «{k}»: ungültige Option.");
                varianten[k] = option;
            }
        }
        return new XrMessprotokoll(aufgabe, variante, zeit, fehlgriffe, abbrueche, erfolg, schrauben, Text(o, "notiz", 500, false), varianten);
    }

    // ---------------------------------------------------------------- Stellschrauben (PC → Brille)

    /// <summary>Schlüssel einer Stellschraube: wie Benutzereinstellungen (Kleinbuchstaben, Ziffern, . _ -; bis 64 Zeichen).</summary>
    public static bool StellschraubeSchluesselGueltig(string? s) => s is not null && SchluesselMuster.IsMatch(s);

    /// <summary>Wert einer Stellschraube: Zahl (endlich), Ja/Nein oder Text bis 200 Zeichen. Meldung oder null.</summary>
    public static string? StellschraubeFehler(JsonNode? w)
    {
        if (w is not JsonValue v) return "Der Wert muss eine Zahl, true/false oder ein Text sein.";
        switch (v.GetValueKind())
        {
            case JsonValueKind.Number: return double.IsFinite(Json.Zahl(v) ?? double.NaN) ? null : "Die Zahl ist ungültig.";
            case JsonValueKind.True or JsonValueKind.False: return null;
            case JsonValueKind.String: return v.GetValue<string>().Length <= 200 ? null : "Der Text ist zu lang (höchstens 200 Zeichen).";
            default: return "Der Wert muss eine Zahl, true/false oder ein Text sein.";
        }
    }

    // ---------------------------------------------------------------- Bauen (PC → Brille)

    public static string Bauen(string typ, JsonObject inhalt)
    {
        var o = new JsonObject { ["typ"] = typ, ["fassung"] = Fassung };
        foreach (var (k, w) in inhalt) o[k] = w?.DeepClone();
        return o.ToJsonString();
    }

    public static JsonObject PoseKnoten(XrPose p)
    {
        var o = new JsonObject { ["kopf"] = LageKnoten(p.Kopf) };
        if (p.Links is not null) o["links"] = HandKnoten(p.Links);
        if (p.Rechts is not null) o["rechts"] = HandKnoten(p.Rechts);
        if (p.Modus is not null) o["modus"] = p.Modus;
        if (p.Massstab is not null) o["massstab"] = p.Massstab;
        return o;
    }

    public static JsonObject ZeigenKnoten(XrZeigen z)
    {
        var o = new JsonObject { ["element"] = z.Element };
        if (z.Punkt is not null) o["punkt"] = Feld(z.Punkt);
        return o;
    }

    public static JsonObject TeilnehmerKnoten(XrTeilnehmerInfo t) => new() { ["id"] = t.Id, ["name"] = t.Name, ["geraet"] = t.Geraet };

    static JsonObject LageKnoten(XrLage l) => new() { ["pos"] = Feld(l.Pos), ["rot"] = Feld(l.Rot) };

    static JsonObject HandKnoten(XrHand h)
    {
        var o = new JsonObject { ["gelenk"] = LageKnoten(h.Gelenk), ["zeigefinger"] = Feld(h.Zeigefinger), ["pinch"] = h.Pinch };
        if (h.PinchStaerke is not null) o["pinchStaerke"] = h.PinchStaerke;
        return o;
    }

    static JsonArray Feld(double[] w) => [.. w.Select(x => (JsonNode)JsonValue.Create(Math.Round(x, 3)))];

    // ---------------------------------------------------------------- Prüfhilfen

    static XrLage? Lage(JsonNode? n, string name)
    {
        if (n is null) return null;
        if (n is not JsonObject o) throw new XrNachrichtFehler($"«{name}» muss {{ pos, rot }} sein.");
        return new XrLage(Vektor(o["pos"], 3, PosMax, $"{name}.pos"), Vektor(o["rot"], 4, 1.5, $"{name}.rot"));
    }

    static XrHand? Hand(JsonNode? n, string name)
    {
        if (n is null) return null;
        if (n is not JsonObject o) throw new XrNachrichtFehler($"«{name}» muss {{ gelenk, zeigefinger, pinch }} sein.");
        var gelenk = Lage(o["gelenk"], $"{name}.gelenk") ?? throw new XrNachrichtFehler($"«{name}.gelenk» fehlt.");
        var zeige = Vektor(o["zeigefinger"], 3, PosMax, $"{name}.zeigefinger");
        var pinch = o["pinch"] is JsonValue pv && pv.TryGetValue<bool>(out var p) ? p : throw new XrNachrichtFehler($"«{name}.pinch» muss true oder false sein.");
        double? staerke = null;
        if (o["pinchStaerke"] is { } s)
        {
            staerke = Zahl(s, $"{name}.pinchStaerke");
            if (staerke is < 0 or > 1) throw new XrNachrichtFehler($"«{name}.pinchStaerke» muss von 0 bis 1 sein.");
        }
        return new XrHand(gelenk, zeige, pinch, staerke);
    }

    static double[] Vektor(JsonNode? n, int laenge, double grenze, string name)
    {
        if (n is not JsonArray a || a.Count != laenge) throw new XrNachrichtFehler($"«{name}» muss eine Liste von {laenge} Zahlen sein.");
        var w = new double[laenge];
        for (int i = 0; i < laenge; i++)
        {
            var z = Zahl(a[i], name);
            if (Math.Abs(z) > grenze) throw new XrNachrichtFehler($"«{name}» liegt ausserhalb von ±{grenze}.");
            w[i] = z;
        }
        return w;
    }

    static double Zahl(JsonNode? n, string name)
    {
        if (n is JsonValue v && v.GetValueKind() == JsonValueKind.Number && Json.Zahl(v) is { } d && double.IsFinite(d)) return d;
        throw new XrNachrichtFehler($"«{name}» muss eine Zahl sein.");
    }

    static int Ganz(JsonNode? n, string name)
    {
        if (n is JsonValue v && v.TryGetValue<int>(out var i) && i is >= 0 and <= 100_000) return i;
        throw new XrNachrichtFehler($"«{name}» muss eine ganze Zahl von 0 bis 100000 sein.");
    }

    static string? Text(JsonObject o, string name, int max, bool pflicht)
    {
        var n = o[name];
        if (n is null) return pflicht ? throw new XrNachrichtFehler($"«{name}» fehlt.") : null;
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) throw new XrNachrichtFehler($"«{name}» muss Text sein.");
        s = s.Trim();
        if (pflicht && s.Length == 0) throw new XrNachrichtFehler($"«{name}» ist leer.");
        if (s.Length > max || s.Any(char.IsControl)) throw new XrNachrichtFehler($"«{name}» ist zu lang (höchstens {max} Zeichen) oder enthält Steuerzeichen.");
        return s;
    }

    static string Kuerzen(string s) => s.Length <= 20 ? s : s[..20] + "…";
}
