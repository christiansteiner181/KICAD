// Kataloge des Interface-Labors (Modul XR, Quest App P6): der Vertrag zwischen KICAD (Dialog «XR-Labor») und der Quest App (P5).
//
// Vier Dateien in kataloge/ (Ordner Einstellungen.KatalogOrdner), beim Laden streng geprüft (Fehler → HlksFehler mit Datei und Stelle, nie stilles Weglassen):
//   xr_stellschrauben.json  Stellschrauben mit Gruppe, Titel, Beschreibung, Einheit, min, max, Schritt, Vorgabe, Herkunft, Status (app | folgt_p5);
//                           dazu «muster» (labor.variante.<name>, labor.aufgabe) für Schlüssel, die nicht einzeln aufgeführt sind
//   xr_pruefaufgaben.json   Prüfaufgaben: Id, Titel, Anleitung (kurz, für die Brille), Messgrössen, Erfolgskriterium, Fehlgriff (optional, P5b: was die App zählt),
//                           Status (p5 | folgt_v1)
//   xr_varianten.json       Varianten (A/B) je Bedienung: Name, Optionen (Id, Titel), Vorgabe
//   xr_fragebogen.json      Fragen: Komfort (eigene Wortlaute), SUS und NASA-TLX (Wortlaut nur mit Quelle, heute Platzhalter)
//
// Die App holt Stellschrauben, Aufgaben und Varianten zusammen mit GET /api/xr/labor/katalog (XrLaborEndpunkte.cs, mit ETag); der Fragebogen bleibt am PC.
// Reihenfolge der Werte in der App: Vorgabe aus dem Katalog › Profil «gilt für die App» (Feld «profil» der Antwort) › Stellschrauben live (willkommen.stellschrauben, Nachricht stellschraube).
// Keine Normwerte: alle Werte sind Einstellungen der Bedienung (Annahmen, im Katalog je Eintrag mit Herkunft).

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hlks.Kern;

namespace Hlks.Web;

/// <summary>Eine geprüfte Stellschraube des Katalogs (Roh = der Eintrag der Datei, für die Antwort).</summary>
public sealed class XrStellschraubeDef
{
    public required string Schluessel { get; init; }
    public required string Gruppe { get; init; }
    /// <summary>zahl | ja_nein | auswahl</summary>
    public required string Art { get; init; }
    public required string Status { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Schritt { get; init; }
    public IReadOnlyList<string> Auswahl { get; init; } = [];
    public required JsonNode Vorgabe { get; init; }
    public required JsonObject Roh { get; init; }
}

public sealed class XrLaborKatalog
{
    public const int Fassung = 1;
    public const string DateiStellschrauben = "xr_stellschrauben.json";
    public const string DateiAufgaben = "xr_pruefaufgaben.json";
    public const string DateiVarianten = "xr_varianten.json";
    public const string DateiFragebogen = "xr_fragebogen.json";

    /// <summary>Schlüssel der Stellschraube für die gewählte Variante: labor.variante.&lt;name&gt;.</summary>
    public const string VarianteVorsatz = "labor.variante.";
    /// <summary>Schlüssel der Stellschraube, die eine Prüfaufgabe startet.</summary>
    public const string AufgabeSchluessel = "labor.aufgabe";

    static readonly Regex IdMuster = new(@"^[a-z][a-z0-9\-]{0,39}$", RegexOptions.CultureInvariant);
    static readonly Regex NameMuster = new(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant);
    static readonly Regex OptionMuster = new(@"^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);
    static readonly string[] Arten = ["zahl", "ja_nein", "auswahl"];
    static readonly string[] StellschraubeStatus = ["app", "folgt_p5"];
    static readonly string[] AufgabeStatus = ["p5", "folgt_v1"];
    static readonly string[] MessgroessenIds = ["zeit", "fehlgriffe", "abbrueche"];

    public string Ordner { get; private init; } = "";
    public IReadOnlyList<XrStellschraubeDef> Stellschrauben { get; private set; } = [];
    JsonArray gruppen = [], muster = [], aufgaben = [], varianten = [], messgroessen = [];
    Dictionary<string, XrStellschraubeDef> nachSchluessel = new(StringComparer.Ordinal);
    Dictionary<string, JsonObject> aufgabeNachId = new(StringComparer.Ordinal);
    Dictionary<string, (JsonObject Roh, HashSet<string> Optionen, string Vorgabe)> varianteNachName = new(StringComparer.Ordinal);
    public XrFragebogenKatalog Fragebogen { get; private set; } = null!;

    public IReadOnlyCollection<string> AufgabenIds => aufgabeNachId.Keys;
    public IReadOnlyCollection<string> VariantenNamen => varianteNachName.Keys;
    public XrStellschraubeDef? Stellschraube(string schluessel) => nachSchluessel.GetValueOrDefault(schluessel);
    public bool AufgabeBekannt(string id) => aufgabeNachId.ContainsKey(id);
    public string? AufgabeStatusVon(string id) => aufgabeNachId.TryGetValue(id, out var a) ? (string?)a["status"] : null;
    public string? AufgabeTitel(string id) => aufgabeNachId.TryGetValue(id, out var a) ? (string?)a["titel"] : null;
    public bool VarianteBekannt(string name, string option) => varianteNachName.TryGetValue(name, out var v) && v.Optionen.Contains(option);
    public string? VarianteVorgabe(string name) => varianteNachName.TryGetValue(name, out var v) ? v.Vorgabe : null;

    // ---------------------------------------------------------------- Laden

    /// <summary>Lädt und prüft alle vier Dateien. Wirft HlksFehler (mit Datei und Stelle) bei jedem Fehler.</summary>
    public static XrLaborKatalog Laden(string ordner)
    {
        var k = new XrLaborKatalog { Ordner = ordner };
        k.StellschraubenLaden(Lesen(ordner, DateiStellschrauben, "hlks-xr-stellschrauben"));
        k.AufgabenLaden(Lesen(ordner, DateiAufgaben, "hlks-xr-pruefaufgaben"));
        k.VariantenLaden(Lesen(ordner, DateiVarianten, "hlks-xr-varianten"));
        k.Fragebogen = XrFragebogenKatalog.Laden(Lesen(ordner, DateiFragebogen, "hlks-xr-fragebogen"));
        k.QuerPruefen();
        return k;
    }

    public static HlksFehler Fehler(string datei, string text) =>
        new($"{datei}: {text}", $"Die Datei kataloge/{datei} korrigieren (Aufbau siehe src/Hlks.Web/OBERFLAECHE.md, Abschnitt «Modul XR: XR-Labor»).");

    static JsonObject Lesen(string ordner, string datei, string art)
    {
        var pfad = Path.Combine(ordner, datei);
        if (!File.Exists(pfad)) throw Fehler(datei, $"Die Datei fehlt im Ordner {ordner}.");
        JsonNode? n;
        try { n = JsonNode.Parse(File.ReadAllText(pfad, Encoding.UTF8), documentOptions: new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow }); }
        catch (JsonException ex) { throw Fehler(datei, "Kein gültiges JSON: " + ex.Message); }
        if (n is not JsonObject o) throw Fehler(datei, "Die Datei muss ein JSON-Objekt sein.");
        if ((string?)o["art"] != art) throw Fehler(datei, $"«art» muss «{art}» sein.");
        if (o["fassung"] is not JsonValue f || !f.TryGetValue<int>(out var fz) || fz != Fassung) throw Fehler(datei, $"«fassung» muss {Fassung} sein.");
        return o;
    }

    static string Text(string datei, JsonNode? n, string wo, int max = 600)
    {
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s)) throw Fehler(datei, $"{wo}: Text fehlt.");
        if (s.Length > max || s.Any(char.IsControl)) throw Fehler(datei, $"{wo}: zu lang (höchstens {max} Zeichen) oder Steuerzeichen.");
        return s;
    }

    static double? Zahl(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? Json.Zahl(v) : null;

    static JsonArray Liste(string datei, JsonObject o, string name)
    {
        if (o[name] is not JsonArray a || a.Count == 0) throw Fehler(datei, $"«{name}» muss eine nicht leere Liste sein.");
        return a;
    }

    static JsonObject Eintrag(string datei, JsonNode? n, string wo) => n as JsonObject ?? throw Fehler(datei, $"{wo}: muss ein Objekt sein.");

    // ---------------------------------------------------------------- Stellschrauben

    void StellschraubenLaden(JsonObject o)
    {
        const string d = DateiStellschrauben;
        var gruppenIds = new HashSet<string>(StringComparer.Ordinal);
        gruppen = [];
        foreach (var gn in Liste(d, o, "gruppen"))
        {
            var g = Eintrag(d, gn, "gruppen");
            var id = Text(d, g["id"], "gruppen.id", 40);
            if (!NameMuster.IsMatch(id)) throw Fehler(d, $"gruppen.id «{id}»: nur Kleinbuchstaben, Ziffern und Unterstrich.");
            if (!gruppenIds.Add(id)) throw Fehler(d, $"gruppen.id «{id}» kommt doppelt vor.");
            Text(d, g["titel"], $"gruppen.{id}.titel", 80);
            gruppen.Add(g.DeepClone());
        }
        muster = [];
        foreach (var mn in Liste(d, o, "muster"))
        {
            var m = Eintrag(d, mn, "muster");
            var name = Text(d, m["muster"], "muster.muster", 64);
            Text(d, m["beschreibung"], $"muster.{name}.beschreibung");
            Text(d, m["quelle"], $"muster.{name}.quelle", 80);
            if (Text(d, m["status"], $"muster.{name}.status", 20) is var st && !StellschraubeStatus.Contains(st)) throw Fehler(d, $"muster.{name}.status «{st}»: erlaubt sind app und folgt_p5.");
            muster.Add(m.DeepClone());
        }

        var liste = new List<XrStellschraubeDef>();
        var schluessel = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sn in Liste(d, o, "stellschrauben"))
        {
            var s = Eintrag(d, sn, "stellschrauben");
            var key = Text(d, s["schluessel"], "stellschrauben.schluessel", 64);
            var wo = $"stellschrauben.{key}";
            if (!XrNachrichten.StellschraubeSchluesselGueltig(key)) throw Fehler(d, $"{wo}: ungültiger Schlüssel (Kleinbuchstaben, Ziffern, . _ -; bis 64 Zeichen).");
            if (key.StartsWith(VarianteVorsatz, StringComparison.Ordinal) || key == AufgabeSchluessel) throw Fehler(d, $"{wo}: «{VarianteVorsatz}…» und «{AufgabeSchluessel}» gehören nicht in diese Liste (siehe «muster»).");
            if (!schluessel.Add(key)) throw Fehler(d, $"{wo}: kommt doppelt vor.");
            var gruppe = Text(d, s["gruppe"], $"{wo}.gruppe", 40);
            if (!gruppenIds.Contains(gruppe)) throw Fehler(d, $"{wo}.gruppe «{gruppe}» steht nicht in «gruppen».");
            Text(d, s["titel"], $"{wo}.titel", 100);
            Text(d, s["beschreibung"], $"{wo}.beschreibung");
            Text(d, s["herkunft"], $"{wo}.herkunft");
            var status = Text(d, s["status"], $"{wo}.status", 20);
            if (!StellschraubeStatus.Contains(status)) throw Fehler(d, $"{wo}.status «{status}»: erlaubt sind app und folgt_p5.");
            var art = Text(d, s["art"], $"{wo}.art", 20);
            if (!Arten.Contains(art)) throw Fehler(d, $"{wo}.art «{art}»: erlaubt sind zahl, ja_nein und auswahl.");
            var vorgabe = s["vorgabe"] ?? throw Fehler(d, $"{wo}.vorgabe fehlt.");
            double min = 0, max = 0, schritt = 0;
            IReadOnlyList<string> auswahl = [];
            switch (art)
            {
                case "zahl":
                    if (Zahl(s["min"]) is not { } mi || Zahl(s["max"]) is not { } ma || !double.IsFinite(mi) || !double.IsFinite(ma)) throw Fehler(d, $"{wo}: min und max müssen Zahlen sein.");
                    if (!(mi < ma)) throw Fehler(d, $"{wo}: min muss kleiner als max sein.");
                    if (Zahl(s["schritt"]) is not { } sc || !(sc > 0) || sc > ma - mi) throw Fehler(d, $"{wo}: schritt muss grösser als 0 und höchstens max − min sein.");
                    if (s["einheit"] is not JsonValue ev || !ev.TryGetValue<string>(out var einheit) || einheit.Length > 12) throw Fehler(d, $"{wo}: einheit fehlt (Text, auch leer erlaubt, höchstens 12 Zeichen).");
                    if (Zahl(vorgabe) is not { } vg || vg < mi || vg > ma) throw Fehler(d, $"{wo}: vorgabe muss eine Zahl zwischen min und max sein.");
                    (min, max, schritt) = (mi, ma, sc);
                    break;
                case "ja_nein":
                    if (vorgabe is not JsonValue bv || !bv.TryGetValue<bool>(out _)) throw Fehler(d, $"{wo}: vorgabe muss true oder false sein.");
                    break;
                default:
                {
                    var werte = new List<string>();
                    foreach (var en in Liste(d, s, "auswahl"))
                    {
                        var e = Eintrag(d, en, $"{wo}.auswahl");
                        var w = Text(d, e["wert"], $"{wo}.auswahl.wert", 40);
                        if (!OptionMuster.IsMatch(w)) throw Fehler(d, $"{wo}.auswahl.wert «{w}»: nur Kleinbuchstaben, Ziffern und Unterstrich.");
                        Text(d, e["titel"], $"{wo}.auswahl.{w}.titel", 80);
                        if (werte.Contains(w)) throw Fehler(d, $"{wo}.auswahl.wert «{w}» kommt doppelt vor.");
                        werte.Add(w);
                    }
                    if (werte.Count < 2) throw Fehler(d, $"{wo}.auswahl braucht mindestens zwei Einträge.");
                    if (vorgabe is not JsonValue sv || !sv.TryGetValue<string>(out var vs) || !werte.Contains(vs)) throw Fehler(d, $"{wo}: vorgabe muss einer der Werte der Auswahl sein.");
                    auswahl = werte;
                    break;
                }
            }
            liste.Add(new XrStellschraubeDef { Schluessel = key, Gruppe = gruppe, Art = art, Status = status, Min = min, Max = max, Schritt = schritt, Auswahl = auswahl, Vorgabe = vorgabe.DeepClone(), Roh = (JsonObject)s.DeepClone() });
        }
        if (liste.Count > 48) throw Fehler(d, "Höchstens 48 Stellschrauben (der Kanal merkt höchstens 64 Werte, dazu kommen Varianten und Aufgabe).");
        // jede Gruppe wird gebraucht (eine leere Gruppe wäre ein Tippfehler)
        foreach (var g in gruppenIds)
            if (liste.All(x => x.Gruppe != g)) throw Fehler(d, $"gruppen.id «{g}» hat keine Stellschraube.");
        Stellschrauben = liste;
        nachSchluessel = liste.ToDictionary(x => x.Schluessel, StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------- Aufgaben

    void AufgabenLaden(JsonObject o)
    {
        const string d = DateiAufgaben;
        messgroessen = [];
        var mIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mn in Liste(d, o, "messgroessen"))
        {
            var m = Eintrag(d, mn, "messgroessen");
            var id = Text(d, m["id"], "messgroessen.id", 20);
            if (!MessgroessenIds.Contains(id)) throw Fehler(d, $"messgroessen.id «{id}»: erlaubt sind zeit, fehlgriffe und abbrueche (die Felder des Messprotokolls).");
            if (!mIds.Add(id)) throw Fehler(d, $"messgroessen.id «{id}» kommt doppelt vor.");
            Text(d, m["titel"], $"messgroessen.{id}.titel", 40);
            Text(d, m["beschreibung"], $"messgroessen.{id}.beschreibung");
            messgroessen.Add(m.DeepClone());
        }
        aufgaben = [];
        aufgabeNachId = new(StringComparer.Ordinal);
        foreach (var an in Liste(d, o, "aufgaben"))
        {
            var a = Eintrag(d, an, "aufgaben");
            var id = Text(d, a["id"], "aufgaben.id", 40);
            var wo = $"aufgaben.{id}";
            if (!IdMuster.IsMatch(id)) throw Fehler(d, $"{wo}: ungültige Id (Kleinbuchstaben, Ziffern, Bindestrich; mit einem Buchstaben am Anfang).");
            if (aufgabeNachId.ContainsKey(id)) throw Fehler(d, $"{wo}: kommt doppelt vor.");
            Text(d, a["titel"], $"{wo}.titel", 80);
            Text(d, a["anleitung"], $"{wo}.anleitung", 300);   // kurz: sie steht in der Brille
            Text(d, a["erfolgskriterium"], $"{wo}.erfolgskriterium");
            if (a["fehlgriff"] is not null) Text(d, a["fehlgriff"], $"{wo}.fehlgriff", 300);   // P5b, optional: was die App als Fehlgriff zählt
            Text(d, a["ort"], $"{wo}.ort", 120);
            Text(d, a["herkunft"], $"{wo}.herkunft");
            var st = Text(d, a["status"], $"{wo}.status", 20);
            if (!AufgabeStatus.Contains(st)) throw Fehler(d, $"{wo}.status «{st}»: erlaubt sind p5 und folgt_v1.");
            if (a["messgroessen"] is not JsonArray mg || mg.Count == 0) throw Fehler(d, $"{wo}.messgroessen fehlt.");
            foreach (var m in mg)
                if (m is not JsonValue mv || !mv.TryGetValue<string>(out var ms) || !mIds.Contains(ms)) throw Fehler(d, $"{wo}.messgroessen: «{m?.ToJsonString()}» steht nicht in der Liste «messgroessen».");
            if (a["varianten"] is not JsonArray va) throw Fehler(d, $"{wo}.varianten muss eine Liste sein (auch leer).");
            foreach (var v in va)
                if (v is not JsonValue vv || !vv.TryGetValue<string>(out var vn) || !NameMuster.IsMatch(vn)) throw Fehler(d, $"{wo}.varianten: ungültiger Name.");
            aufgaben.Add(a.DeepClone());
            aufgabeNachId[id] = a;
        }
        if (aufgabeNachId.Count > 32) throw Fehler(d, "Höchstens 32 Aufgaben.");
    }

    // ---------------------------------------------------------------- Varianten

    void VariantenLaden(JsonObject o)
    {
        const string d = DateiVarianten;
        varianten = [];
        varianteNachName = new(StringComparer.Ordinal);
        foreach (var vn in Liste(d, o, "varianten"))
        {
            var v = Eintrag(d, vn, "varianten");
            var name = Text(d, v["name"], "varianten.name", 32);
            var wo = $"varianten.{name}";
            if (!NameMuster.IsMatch(name)) throw Fehler(d, $"{wo}: ungültiger Name (Kleinbuchstaben, Ziffern, Unterstrich).");
            if (!XrNachrichten.StellschraubeSchluesselGueltig(VarianteVorsatz + name)) throw Fehler(d, $"{wo}: der Schlüssel «{VarianteVorsatz}{name}» ist zu lang.");
            if (varianteNachName.ContainsKey(name)) throw Fehler(d, $"{wo}: kommt doppelt vor.");
            Text(d, v["titel"], $"{wo}.titel", 60);
            Text(d, v["beschreibung"], $"{wo}.beschreibung");
            Text(d, v["herkunft"], $"{wo}.herkunft");
            var opt = new HashSet<string>(StringComparer.Ordinal);
            foreach (var on in Liste(d, v, "optionen"))
            {
                var e = Eintrag(d, on, $"{wo}.optionen");
                var id = Text(d, e["id"], $"{wo}.optionen.id", 40);
                if (!OptionMuster.IsMatch(id)) throw Fehler(d, $"{wo}.optionen.id «{id}»: nur Kleinbuchstaben, Ziffern und Unterstrich.");
                if (!opt.Add(id)) throw Fehler(d, $"{wo}.optionen.id «{id}» kommt doppelt vor.");
                Text(d, e["titel"], $"{wo}.optionen.{id}.titel", 60);
            }
            if (opt.Count < 2) throw Fehler(d, $"{wo}: mindestens zwei Optionen (A/B).");
            var vorgabe = Text(d, v["vorgabe"], $"{wo}.vorgabe", 40);
            if (!opt.Contains(vorgabe)) throw Fehler(d, $"{wo}.vorgabe «{vorgabe}» ist keine der Optionen.");
            varianten.Add(v.DeepClone());
            varianteNachName[name] = (v, opt, vorgabe);
        }
        if (varianteNachName.Count > 12) throw Fehler(d, "Höchstens 12 Varianten.");
    }

    void QuerPruefen()
    {
        foreach (var a in aufgabeNachId.Values)
            foreach (var v in (JsonArray)a["varianten"]!)
                if (!varianteNachName.ContainsKey((string)v!)) throw Fehler(DateiAufgaben, $"aufgaben.{(string?)a["id"]}.varianten: die Variante «{(string)v!}» steht nicht in {DateiVarianten}.");
        if (Stellschrauben.Count + varianteNachName.Count + 1 > 64) throw Fehler(DateiStellschrauben, "Zusammen mit Varianten und Aufgabe mehr als 64 Stellschrauben (Grenze des Kanals).");
    }

    // ---------------------------------------------------------------- Werte prüfen

    /// <summary>Prüft den Wert für eine Stellschraube des Katalogs; null = in Ordnung, sonst die Meldung. Ein Wert null (zurücksetzen) ist immer gültig.</summary>
    public static string? WertFehler(XrStellschraubeDef s, JsonNode? wert)
    {
        if (wert is null) return null;
        if (wert is not JsonValue v) return "Der Wert muss eine Zahl, true/false oder ein Text sein.";
        switch (s.Art)
        {
            case "zahl":
                if (v.GetValueKind() != JsonValueKind.Number || Json.Zahl(v) is not { } d || !double.IsFinite(d)) return "Der Wert muss eine Zahl sein.";
                return d < s.Min || d > s.Max ? $"Der Wert muss von {Num(s.Min)} bis {Num(s.Max)} sein." : null;
            case "ja_nein":
                return v.TryGetValue<bool>(out _) ? null : "Der Wert muss true oder false sein.";
            default:
                return v.TryGetValue<string>(out var t) && s.Auswahl.Contains(t) ? null : $"Erlaubt sind: {string.Join(", ", s.Auswahl)}.";
        }
    }

    static string Num(double d) => d.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Bereinigt ein gespeichertes Profil für die App: nur Schlüssel, die der Katalog kennt, mit gültigem Wert. Gibt { name, werte } zurück
    /// (nie null; ohne Gültiges bleiben die Werte leer). Unbekanntes wird weggelassen, nicht gemeldet – ein altes Profil darf neuere/ältere Kataloge überleben.
    /// </summary>
    public JsonObject ProfilBereinigen(JsonNode? profil)
    {
        var ergebnis = new JsonObject { ["name"] = profil is JsonObject po && po["name"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n : "", ["werte"] = new JsonObject() };
        if (profil is not JsonObject p || p["werte"] is not JsonObject w) return ergebnis;
        var ziel = (JsonObject)ergebnis["werte"]!;
        foreach (var (k, wert) in w)
        {
            if (nachSchluessel.TryGetValue(k, out var s)) { if (wert is not null && WertFehler(s, wert) is null) ziel[k] = wert.DeepClone(); }
            else if (k.StartsWith(VarianteVorsatz, StringComparison.Ordinal) && wert is JsonValue vv && vv.TryGetValue<string>(out var o) && VarianteBekannt(k[VarianteVorsatz.Length..], o)) ziel[k] = o;
        }
        return ergebnis;
    }

    // ---------------------------------------------------------------- Antwort der App

    /// <summary>
    /// Der Katalog für die App (GET /api/xr/labor/katalog): { art, fassung, schluessel (SHA-256 über den Inhalt, ETag), gruppen, stellschrauben, muster, messgroessen,
    /// aufgaben, varianten, profil: { kennung, name, werte } | null }. Das Profil ist das als «gilt für die App» markierte (bereinigt).
    /// </summary>
    public JsonObject Zusammen(string? profilKennung, JsonNode? profil)
    {
        var o = new JsonObject
        {
            ["art"] = "hlks-xr-labor-katalog", ["fassung"] = Fassung,
            ["gruppen"] = gruppen.DeepClone(), ["stellschrauben"] = new JsonArray([.. Stellschrauben.Select(s => (JsonNode)s.Roh.DeepClone())]),
            ["muster"] = muster.DeepClone(), ["messgroessen"] = messgroessen.DeepClone(), ["aufgaben"] = aufgaben.DeepClone(), ["varianten"] = varianten.DeepClone(),
        };
        if (profilKennung is not null && profil is not null)
        {
            var b = ProfilBereinigen(profil);
            o["profil"] = new JsonObject { ["kennung"] = profilKennung, ["name"] = b["name"]!.DeepClone(), ["werte"] = b["werte"]!.DeepClone() };
        }
        else o["profil"] = null;
        var schluessel = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(o.ToJsonString())))[..32];
        var ergebnis = new JsonObject { ["art"] = o["art"]!.DeepClone(), ["fassung"] = Fassung, ["schluessel"] = schluessel };
        foreach (var (k, w) in o) if (k is not ("art" or "fassung")) ergebnis[k] = w?.DeepClone();
        return ergebnis;
    }
}

/// <summary>Profile des Labors in den Benutzereinstellungen: «labor.profil.&lt;kennung&gt;» = { name, werte{} }, «labor.profil_aktiv» = Kennung des Profils, das für die App gilt.</summary>
public static class XrLaborProfil
{
    public const string Vorsatz = "labor.profil.";
    public const string AktivSchluessel = "labor.profil_aktiv";
    static readonly Regex KennungMuster = new(@"^[a-z0-9][a-z0-9_]{0,23}$", RegexOptions.CultureInvariant);
    public const int WerteMax = 64;

    public static bool KennungGueltig(string? s) => s is not null && KennungMuster.IsMatch(s);

    /// <summary>Prüfung für BenutzerEinstellungen: Meldung oder null. Gilt für «labor.profil.&lt;kennung&gt;» (Form) und «labor.profil_aktiv» (Kennung).</summary>
    public static string? Pruefen(string schluessel, JsonNode wert)
    {
        if (schluessel == AktivSchluessel)
            return wert is JsonValue v && v.TryGetValue<string>(out var k) && KennungGueltig(k) ? null : "Der Wert muss die Kennung eines Profils sein (Kleinbuchstaben, Ziffern, Unterstrich; bis 24 Zeichen).";
        if (!schluessel.StartsWith(Vorsatz, StringComparison.Ordinal) || !KennungGueltig(schluessel[Vorsatz.Length..]))
            return "Der Schlüssel muss «labor.profil.<kennung>» heissen (Kennung: Kleinbuchstaben, Ziffern, Unterstrich; bis 24 Zeichen).";
        if (wert is not JsonObject p) return "Ein Profil ist ein Objekt { name, werte }.";
        if (p["name"] is not JsonValue nv || !nv.TryGetValue<string>(out var name) || string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
            return "Der Name des Profils fehlt oder ist zu lang (höchstens 40 Zeichen, keine Steuerzeichen).";
        if (p["werte"] is not JsonObject w) return "«werte» muss ein Objekt { Schlüssel: Wert } sein.";
        if (w.Count > WerteMax) return $"Höchstens {WerteMax} Werte je Profil.";
        foreach (var (k, x) in w)
        {
            if (!XrNachrichten.StellschraubeSchluesselGueltig(k)) return $"Wert «{(k.Length > 20 ? k[..20] + "…" : k)}»: ungültiger Schlüssel.";
            if (x is null) return $"Wert «{k}»: fehlt.";
            if (XrNachrichten.StellschraubeFehler(x) is { } f) return $"Wert «{k}»: {f}";
        }
        return null;
    }
}
