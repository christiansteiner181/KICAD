// Herstellerbibliothek H1 (entwurf/herstellerbibliothek.md, Entscheide Christian 02.11.2026): Abschnitt «regeltechnik» der Familie –
// elektrische Daten, Stellen und Rückmelden, Kommunikation, Datenpunkte als Vorlage für die Klasse Datenpunkt (Modul 8) – sowie bei
// Armaturen «antriebe» (Kombinationstabelle des Herstellers: welche Antriebsfamilie/-typen auf welche Ventiltypen passen) und «anbau»
// (Anbaupunkt des Antriebs). Am platzierten Ventil zeigt bezuege.antrieb auf die Antriebsfamilie, Eigenschaft antriebstyp nennt den Typ.
//
// Grundsatz: Jeder Einzelwert (Spannung, Leistung, Laufzeit …) ist ein VERWEIS auf einen Parameter der Familie. Damit gelten für
// Regeltechnik-Werte dieselben Quellen, Wertprüfung, Konflikte und Abdeckung wie für alle Parameter (FamilienKonflikte). Listeneinträge
// (Speisung, Signale, Protokolle, Datenpunkte) tragen eigene quellen; ihre Befunde müssen existieren, der Text wird nicht verglichen
// (Bezeichnungen wie «DC 0…10 V» sind je Hersteller zu verschieden). Ohne quellen: Hinweis «unbelegt».

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Einzelwert der Regeltechnik: Stelle im Abschnitt (z. B. «elektrisch.leistung») und Parameter der Familie.</summary>
public sealed record FamRtVerweis(string Stelle, string Parameter);

/// <summary>Gelesener Abschnitt «regeltechnik» bzw. «antriebe»/«anbau» einer Familie.</summary>
public sealed class FamRegeltechnik
{
    public static readonly string[] Signalarten = ["0-10V", "2-10V", "0-20mA", "4-20mA", "3-punkt", "auf-zu", "pwm", "bus"];
    public static readonly string[] Rueckmeldearten = ["0-10V", "2-10V", "0-20mA", "4-20mA", "kontakt", "bus"];
    public static readonly string[] Protokolle = ["modbus-rtu", "modbus-tcp", "bacnet-mstp", "bacnet-ip", "mp-bus", "knx", "lon", "mbus", "profinet", "herstellerbus"];
    public static readonly string[] Speisearten = ["AC", "DC", "AC/DC"];

    /// <summary>Einzelwerte je Unterabschnitt: Feld → (Grösse der Einheit nach QuellEinheiten bzw. Datentyp, Text erlaubt).</summary>
    static readonly Dictionary<string, (string? Einheit, string? Datentyp, bool Text)> ElektrischFelder = new()
    {
        ["leistung"] = ("W", null, false), ["leistung_ruhe"] = ("W", null, false), ["leistung_bemessung"] = ("VA", null, false),
        ["strom"] = ("A", null, false), ["schutzart"] = (null, null, true), ["schutzklasse"] = (null, null, true),
    };
    static readonly Dictionary<string, (string? Einheit, string? Datentyp, bool Text)> StellenFelder = new()
    {
        ["laufzeit"] = ("s", null, false), ["stellkraft"] = ("N", null, false), ["drehmoment"] = ("Nm", null, false),
        ["hub"] = (null, "laenge", false), ["drehwinkel"] = (null, "winkel", false), ["notstellung"] = (null, null, true),
    };
    static readonly Dictionary<string, (string? Einheit, string? Datentyp, bool Text)> SpeisungFelder = new()
    {
        ["spannung"] = ("V", null, true), ["frequenz"] = ("Hz", null, false),
    };

    public List<FamRtVerweis> Verweise { get; } = [];
    /// <summary>Kombinationstabelle (nur Armaturen): je Eintrag Antriebsfamilie, Antriebstypen (leer = alle), Ventiltypen (leer = alle).</summary>
    public List<(string Familie, List<string> Typen, List<string> Fuer)> Antriebe { get; } = [];
    /// <summary>Nur Verteiler (Entscheid Christian 02.11.2026): passende Verteilerkästen je Typ (Abgangszahl), gleicher Aufbau wie Antriebe.</summary>
    public List<(string Familie, List<string> Typen, List<string> Fuer)> Kaesten { get; } = [];
    public FamZahl[]? AnbauLage { get; set; }
    public FamZahl[]? AnbauRichtung { get; set; }

    /// <summary>Alle Quellenlisten der drei Abschnitte mit Stelle (für Existenzprüfung und Format, FamilienQuellenAnalyse.AlleQuellen).</summary>
    public static IEnumerable<(string Stelle, JsonNode? Liste)> Quellenlisten(JsonObject def)
    {
        if (def["regeltechnik"] is JsonObject rt)
        {
            if (rt["elektrisch"] is JsonObject el)
            {
                int i = 0;
                foreach (var s in el["speisung"] as JsonArray ?? []) { i++; if (s is JsonObject o) yield return ($"regeltechnik.elektrisch.speisung[{i}].quellen", o["quellen"]); }
            }
            if (rt["stellen"] is JsonObject st)
                foreach (var feld in new[] { "signale", "rueckmeldung" })
                {
                    int i = 0;
                    foreach (var s in st[feld] as JsonArray ?? []) { i++; if (s is JsonObject o) yield return ($"regeltechnik.stellen.{feld}[{i}].quellen", o["quellen"]); }
                }
            foreach (var feld in new[] { "kommunikation", "datenpunkte" })
            {
                int i = 0;
                foreach (var s in rt[feld] as JsonArray ?? []) { i++; if (s is JsonObject o) yield return ($"regeltechnik.{feld}[{i}].quellen", o["quellen"]); }
            }
        }
        {
            int i = 0;
            foreach (var s in def["antriebe"] as JsonArray ?? []) { i++; if (s is JsonObject o) yield return ($"antriebe[{i}].quellen", o["quellen"]); }
        }
        {
            int i = 0;
            foreach (var s in def["kaesten"] as JsonArray ?? []) { i++; if (s is JsonObject o) yield return ($"kaesten[{i}].quellen", o["quellen"]); }
        }
        if (def["anbau"] is JsonObject an) yield return ("anbau.quellen", an["quellen"]);
    }

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Liest und prüft die Abschnitte. fehl/hinweis melden mit Stelle; punkt liest einen Punkt wie in den Körpern.</summary>
    internal static FamRegeltechnik? Lesen(JsonObject def, FamilieDef f, Katalog? kat, KlassenDef? klasse,
        Action<string, string, string?> fehl, Action<string, string, string?> hinweis, Func<JsonNode?, string, int, FamZahl[]?> punkt)
    {
        var rt = def["regeltechnik"];
        var an = def["antriebe"];
        var ab = def["anbau"];
        var ka = def["kaesten"];
        if (rt is null && an is null && ab is null && ka is null) return null;
        var r = new FamRegeltechnik();
        if (rt is not null) r.RegeltechnikLesen(rt, f, kat, fehl, hinweis);
        bool verteiler = klasse is null || klasse.IstArt("Verteiler");
        bool armatur = klasse is null || klasse.IstArt("Armatur") || verteiler;   // Verteiler: Stellantriebe je Abgang (FBH)
        if (ka is not null)
        {
            if (!verteiler) fehl("kaesten", $"kaesten gibt es nur bei Verteilern, nicht bei {klasse!.Name}", "Abschnitt entfernen.");
            else r.ListeLesen(ka, "kaesten", "Verteilerkasten", r.Kaesten, f, fehl, hinweis);
        }
        if ((an is not null || ab is not null) && !armatur)
            fehl(an is not null ? "antriebe" : "anbau", $"antriebe und anbau gibt es nur bei Armaturen (Ventil, Klappe) und Verteilern, nicht bei {klasse!.Name}",
                "Abschnitt entfernen; ein Antrieb ist eine eigene Familie der Kategorie Antrieb.");
        else
        {
            if (an is not null) r.ListeLesen(an, "antriebe", "Antrieb", r.Antriebe, f, fehl, hinweis);
            if (ab is not null)
            {
                if (ab is not JsonObject o) fehl("anbau", "anbau muss ein Objekt sein", "{ lage: [x, y, z], richtung: [dx, dy, dz], quellen }");
                else
                {
                    foreach (var (feld, _) in o)
                        if (feld is not ("lage" or "richtung" or "quellen")) fehl("anbau", $"Unbekanntes Feld «{feld}»", "Erlaubt: lage, richtung, quellen");
                    r.AnbauLage = punkt(o["lage"], "anbau.lage", 3);
                    r.AnbauRichtung = punkt(o["richtung"], "anbau.richtung", 3);
                }
            }
            if (an is not null && ab is null && !(klasse?.IstArt("Verteiler") ?? false))
                hinweis("anbau", "Die Familie nennt Antriebe, aber keinen Anbaupunkt – der Antrieb lässt sich nicht darstellen", "anbau { lage, richtung } aus dem Massbild angeben.");
        }
        return r;
    }

    void RegeltechnikLesen(JsonNode rt, FamilieDef f, Katalog? kat, Action<string, string, string?> fehl, Action<string, string, string?> hinweis)
    {
        if (rt is not JsonObject o) { fehl("regeltechnik", "regeltechnik muss ein Objekt sein", "{ elektrisch, stellen, kommunikation, datenpunkte }"); return; }
        foreach (var (feld, _) in o)
            if (feld is not ("elektrisch" or "stellen" or "kommunikation" or "datenpunkte"))
                fehl("regeltechnik", $"Unbekanntes Feld «{feld}»", "Erlaubt: elektrisch, stellen, kommunikation, datenpunkte");

        if (o["elektrisch"] is { } eln)
        {
            if (eln is not JsonObject el) fehl("regeltechnik.elektrisch", "elektrisch muss ein Objekt sein", null);
            else
            {
                foreach (var (feld, wert) in el)
                {
                    if (feld == "speisung") continue;
                    if (ElektrischFelder.TryGetValue(feld, out var art)) Verweis($"regeltechnik.elektrisch.{feld}", wert, art, f, fehl);
                    else fehl("regeltechnik.elektrisch", $"Unbekanntes Feld «{feld}»", "Erlaubt: speisung, " + string.Join(", ", ElektrischFelder.Keys));
                }
                if (el["speisung"] is { } spn)
                {
                    if (spn is not JsonArray sp) fehl("regeltechnik.elektrisch.speisung", "speisung muss eine Liste sein", "[{ art: \"AC\", spannung: \"<Parameter>\", frequenz: \"<Parameter>\", quellen }]");
                    else
                    {
                        int i = 0;
                        foreach (var x in sp)
                        {
                            i++;
                            var ort = $"regeltechnik.elektrisch.speisung[{i}]";
                            if (x is not JsonObject so) { fehl(ort, "Eintrag muss ein Objekt sein", null); continue; }
                            foreach (var (feld, wert) in so)
                            {
                                if (feld is "art" or "phasen" or "quellen") continue;
                                if (SpeisungFelder.TryGetValue(feld, out var art)) Verweis($"{ort}.{feld}", wert, art, f, fehl);
                                else fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: art, spannung, frequenz, phasen, quellen");
                            }
                            Auswahl(so["art"], $"{ort}.art", Speisearten, true, fehl);
                            if (so["phasen"] is { } ph && Json.Zahl(ph) is not (1 or 3)) fehl($"{ort}.phasen", "phasen muss 1 oder 3 sein", null);
                            if (so["spannung"] is null) fehl(ort, "spannung fehlt", "Parameter der Nennspannung nennen.");
                            Unbelegt(so, ort, hinweis);
                        }
                    }
                }
            }
        }

        if (o["stellen"] is { } stn)
        {
            if (stn is not JsonObject st) fehl("regeltechnik.stellen", "stellen muss ein Objekt sein", null);
            else
                foreach (var (feld, wert) in st)
                {
                    if (feld is "signale" or "rueckmeldung")
                        Arten(wert, $"regeltechnik.stellen.{feld}", feld == "signale" ? Signalarten : Rueckmeldearten, fehl, hinweis);
                    else if (StellenFelder.TryGetValue(feld, out var art)) Verweis($"regeltechnik.stellen.{feld}", wert, art, f, fehl);
                    else fehl("regeltechnik.stellen", $"Unbekanntes Feld «{feld}»", "Erlaubt: signale, rueckmeldung, " + string.Join(", ", StellenFelder.Keys));
                }
        }

        if (o["kommunikation"] is { } kon)
        {
            if (kon is not JsonArray ka) fehl("regeltechnik.kommunikation", "kommunikation muss eine Liste sein", "[{ protokoll: \"modbus-rtu\", zusatz: \"<Busmodul>\", quellen }]");
            else
            {
                int i = 0;
                foreach (var x in ka)
                {
                    i++;
                    var ort = $"regeltechnik.kommunikation[{i}]";
                    if (x is not JsonObject ko) { fehl(ort, "Eintrag muss ein Objekt sein", null); continue; }
                    foreach (var (feld, _) in ko)
                        if (feld is not ("protokoll" or "zusatz" or "quellen")) fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: protokoll, zusatz, quellen");
                    Auswahl(ko["protokoll"], $"{ort}.protokoll", Protokolle, true, fehl);
                    Unbelegt(ko, ort, hinweis);
                }
            }
        }

        if (o["datenpunkte"] is { } dpn)
        {
            if (dpn is not JsonArray da) { fehl("regeltechnik.datenpunkte", "datenpunkte muss eine Liste sein", "[{ name, signal, art, messgroesse, einheit, weg, objekt, optional, quellen }]"); return; }
            var dp = kat?.Klassen.GetValueOrDefault("Datenpunkt");
            string[] signale = dp?.Eigenschaften.FirstOrDefault(x => x.Name == "signal")?.Auswahl?.ToArray() ?? ["AI", "AO", "DI", "DO", "Zaehler", "Wert"];
            string[] arten = dp?.Eigenschaften.FirstOrDefault(x => x.Name == "art")?.Auswahl?.ToArray() ?? ["messung", "stellung", "meldung", "schaltung", "zaehlung", "sollwert"];
            string[] wege = ["kontakt", .. Signalarten.Where(s => s != "bus"), .. Protokolle];
            var namen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (var x in da)
            {
                i++;
                var ort = $"regeltechnik.datenpunkte[{i}]";
                if (x is not JsonObject d) { fehl(ort, "Eintrag muss ein Objekt sein", null); continue; }
                foreach (var (feld, _) in d)
                    if (feld is not ("name" or "signal" or "art" or "messgroesse" or "einheit" or "weg" or "objekt" or "optional" or "quellen"))
                        fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: name, signal, art, messgroesse, einheit, weg, objekt, optional, quellen");
                var name = T(d["name"])?.Trim();
                if (string.IsNullOrEmpty(name)) fehl($"{ort}.name", "name fehlt", "Kurzname des Datenpunkts, z. B. «Stoerung».");
                else if (!namen.Add(name)) fehl($"{ort}.name", $"Datenpunkt «{name}» gibt es zweimal", "Namen eindeutig wählen.");
                Auswahl(d["signal"], $"{ort}.signal", signale, true, fehl);
                Auswahl(d["art"], $"{ort}.art", arten, true, fehl);
                Auswahl(d["weg"], $"{ort}.weg", wege, false, fehl);
                if (d["optional"] is { } op && !(op is JsonValue ov && ov.TryGetValue<bool>(out _))) fehl($"{ort}.optional", "optional muss true oder false sein", null);
                Unbelegt(d, ort, hinweis);
            }
        }
    }

    /// <summary>Kombinationstabelle «antriebe» bzw. «kaesten»: je Eintrag Familie (Kategorie <paramref name="kategorie"/>), deren Typen und die
    /// Typen dieser Familie, für die sie passen.</summary>
    void ListeLesen(JsonNode an, string name, string kategorie, List<(string Familie, List<string> Typen, List<string> Fuer)> ziel, FamilieDef f,
        Action<string, string, string?> fehl, Action<string, string, string?> hinweis)
    {
        if (an is not JsonArray a) { fehl(name, $"{name} muss eine Liste sein", $"[{{ familie: \"<Familie der Kategorie {kategorie}>\", typen: [ … ], fuer: [ <Typen dieser Familie> ], quellen }}]"); return; }
        int i = 0;
        foreach (var x in a)
        {
            i++;
            var ort = $"{name}[{i}]";
            if (x is not JsonObject o) { fehl(ort, "Eintrag muss ein Objekt sein", null); continue; }
            foreach (var (feld, _) in o)
                if (feld is not ("familie" or "typen" or "fuer" or "quellen")) fehl(ort, $"Unbekanntes Feld «{feld}»", "Erlaubt: familie, typen, fuer, quellen");
            var fam = T(o["familie"])?.Trim();
            if (string.IsNullOrEmpty(fam)) { fehl($"{ort}.familie", "familie fehlt", $"Name oder id der Familie (Kategorie {kategorie}) angeben."); continue; }
            var typen = Texte(o["typen"], $"{ort}.typen", fehl);
            var fuer = Texte(o["fuer"], $"{ort}.fuer", fehl);
            foreach (var t in fuer)
                if (f.Typ(t) is null) fehl($"{ort}.fuer", $"Typ «{t}» gibt es in dieser Familie nicht", "Erlaubt: " + string.Join(", ", f.Typen.Select(x => x.Name).Take(20)));
            Unbelegt(o, ort, hinweis);
            ziel.Add((fam, typen, fuer));
        }
    }

    static List<string> Texte(JsonNode? n, string ort, Action<string, string, string?> fehl)
    {
        if (n is null) return [];
        if (n is not JsonArray a) { fehl(ort, "muss eine Liste von Texten sein", null); return []; }
        var l = new List<string>();
        foreach (var x in a) if (T(x)?.Trim() is { Length: > 0 } s) l.Add(s); else fehl(ort, "Eintrag muss ein Text sein", null);
        return l;
    }

    void Verweis(string stelle, JsonNode? wert, (string? Einheit, string? Datentyp, bool Text) art, FamilieDef f, Action<string, string, string?> fehl)
    {
        var name = T(wert)?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            fehl(stelle, "muss der Name eines Parameters der Familie sein (Text)",
                "Wert als Parameter (Typparameter mit Wert je Typ und quellen) führen und hier seinen Namen nennen, z. B. \"p1_max\".");
            return;
        }
        if (!f.NachName.TryGetValue(name, out var p)) { fehl(stelle, $"Parameter «{name}» gibt es nicht", Formel.NamensVorschlag(name, f.NachName.Keys)); return; }
        if (p.IstText)
        {
            if (!art.Text) fehl(stelle, $"«{name}» ist ein Text – hier ist eine Zahl verlangt", art.Einheit is null ? $"Datentyp {art.Datentyp}" : $"Datentyp zahl mit Einheit {art.Einheit}");
        }
        else if (p.Datentyp == "ja_nein") fehl(stelle, $"«{name}» ist ja_nein – hier ist {(art.Text ? "ein Text oder eine Zahl" : "eine Zahl")} verlangt", null);
        else if (art.Datentyp is not null)
        {
            if (p.Datentyp != art.Datentyp) fehl(stelle, $"«{name}» ist {p.Datentyp}, verlangt ist {art.Datentyp}", $"Datentyp {art.Datentyp} wählen.");
        }
        else if (art.Einheit is not null && (p.Datentyp != "zahl" || p.Einheit is null || QuellEinheiten.Faktor(p.Einheit, art.Einheit) is null))
            fehl(stelle, $"«{name}» hat {(p.Datentyp != "zahl" ? "den Datentyp " + p.Datentyp : "die Einheit " + (p.Einheit ?? "keine"))}, verlangt ist eine Zahl in {art.Einheit}",
                $"Datentyp zahl, einheit {art.Einheit} (oder eine umrechenbare Einheit).");
        Verweise.Add(new FamRtVerweis(stelle, name));
    }

    static void Auswahl(JsonNode? n, string ort, string[] erlaubt, bool pflicht, Action<string, string, string?> fehl)
    {
        if (n is null) { if (pflicht) fehl(ort, "fehlt", "Erlaubt: " + string.Join(", ", erlaubt)); return; }
        var s = T(n);
        if (s is null || !erlaubt.Contains(s, StringComparer.Ordinal)) fehl(ort, $"«{s ?? n.ToJsonString()}» gibt es nicht", "Erlaubt: " + string.Join(", ", erlaubt));
    }

    static void Arten(JsonNode? n, string ort, string[] erlaubt, Action<string, string, string?> fehl, Action<string, string, string?> hinweis)
    {
        if (n is not JsonArray a) { fehl(ort, "muss eine Liste sein", "[{ art: \"0-10V\", quellen }]"); return; }
        int i = 0;
        foreach (var x in a)
        {
            i++;
            if (x is not JsonObject o) { fehl($"{ort}[{i}]", "Eintrag muss ein Objekt sein", null); continue; }
            foreach (var (feld, _) in o)
                if (feld is not ("art" or "quellen")) fehl($"{ort}[{i}]", $"Unbekanntes Feld «{feld}»", "Erlaubt: art, quellen");
            Auswahl(o["art"], $"{ort}[{i}].art", erlaubt, true, fehl);
            Unbelegt(o, $"{ort}[{i}]", hinweis);
        }
    }

    static void Unbelegt(JsonObject o, string ort, Action<string, string, string?> hinweis)
    {
        if (o["quellen"] is not JsonArray { Count: > 0 })
            hinweis(ort, "unbelegt: ohne quellen", "Befund der Herstellerunterlage nennen (Datenblatt bzw. VDI-3805-Feld als Zweitbeleg).");
    }

    // ------------------------------------------------------------------ Auflösen je Typ

    /// <summary>Regeltechnik für einen Typ: der Abschnitt wie definiert, jeder Parameterverweis ersetzt durch
    /// { parameter, wert, einheit } (Wert aus Typ und Exemplarwerten). Null = die Familie hat keinen Abschnitt.</summary>
    public static JsonObject? Aufloesen(FamilieDef f, string? typ, JsonObject? exemplar)
    {
        if (f.Quelle["regeltechnik"] is not JsonObject rt || f.Regeltechnik is null) return null;
        var a = FamilienAuswertung.Aufloesen(f, typ, exemplar);
        var aus = (JsonObject)rt.DeepClone();
        foreach (var v in f.Regeltechnik.Verweise)
        {
            var (eltern, feld) = Stelle(aus, v.Stelle);
            if (eltern is null) continue;
            var p = f.NachName[v.Parameter];
            var w = a.Wert(v.Parameter);
            eltern[feld] = new JsonObject
            {
                ["parameter"] = v.Parameter,
                ["wert"] = w is { } x ? (x.IstText ? JsonValue.Create(x.Text) : JsonValue.Create(Math.Round(x.Zahl, 6))) : null,
                ["einheit"] = p.Datentyp switch { "laenge" => "mm", "winkel" => "°", _ => p.Einheit },
            };
        }
        return aus;
    }

    /// <summary>Eltern-Objekt und Feldname zu einer Stelle «regeltechnik.a.b[2].c» innerhalb des Abschnitts.</summary>
    static (JsonObject? Eltern, string Feld) Stelle(JsonObject rt, string stelle)
    {
        var teile = stelle.Split('.').Skip(1).ToArray();   // ohne «regeltechnik»
        JsonNode? n = rt;
        for (int i = 0; i < teile.Length - 1; i++)
        {
            var t = teile[i];
            int k = t.IndexOf('[');
            if (k < 0) { n = n?[t]; continue; }
            n = n?[t[..k]];
            n = n is JsonArray arr && int.TryParse(t[(k + 1)..^1], out var nr) && nr >= 1 && nr <= arr.Count ? arr[nr - 1] : null;
        }
        return (n as JsonObject, teile[^1]);
    }
}

/// <summary>Abfrage «regeltechnik»: Regeltechnik eines platzierten Elements (Familie + Typ; bei einem Ventil mit Antrieb die des
/// Antriebs) bzw. einer Familie und eines Typs.</summary>
public static class RegeltechnikAbfrage
{
    public const string Name = "regeltechnik";
    public const string Text = "Regeltechnische Angaben (Herstellerbibliothek): elektrische Daten (Speisung, Leistung W/VA, Strom, Schutzart), Stellen " +
        "(Signale, Rückmeldung, Laufzeit, Stellkraft/Drehmoment, Hub), Kommunikation (Modbus, BACnet, MP-Bus, KNX …) und Datenpunkte als Vorlage für die " +
        "Klasse Datenpunkt – je Wert mit Parameter, Wert und Einheit. Für ein Element aus seiner Familie und seinem Typ; trägt das Element einen Antrieb " +
        "(bezuege.antrieb, Eigenschaft antriebstyp), zusätzlich die Angaben des Antriebs (gilt). Alternativ familie + typ.";

    public static readonly IReadOnlyList<ParameterDef> Parameter =
    [
        new("element", "text", false, "Element (Kennzeichen oder Id)"),
        new("familie", "text", false, "Familie (Kennzeichen, Name oder id) – statt element"),
        new("typ", "text", false, "Typ der Familie (mit familie)"),
    ];

    public static JsonObject Abfrage(Projekt p, JsonObject par)
    {
        if (Param.Text(par, "element") is { } el)
        {
            var e = p.Modell.Hole(el, "element");
            var fam = Familien.FamilieVon(p.Modell, e)
                ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Familie.", "Regeltechnik steht in der Familie; Element mit einer Familie platzieren.");
            var aus = Teil(p, fam, e.Text("familientyp"), e.Eigenschaft("parameter")?.Inhalt as JsonObject);
            aus["element"] = e.Kennzeichen;
            JsonObject? antrieb = null;
            if (e.Bezuege.TryGetValue("antrieb", out var aid))
            {
                var af = p.Modell.Finde(aid);
                if (af is null || af.Klasse != Familien.Klasse)
                    throw new HlksFehler($"bezuege.antrieb von {e.Kennzeichen} zeigt nicht auf eine Familie.", "Antriebsfamilie (Kategorie Antrieb) als Bezug antrieb setzen.");
                antrieb = Teil(p, af, e.Text("antriebstyp"), null);
                var vdef = Familien.Definition(fam, p.Katalog);
                var adef = Familien.Definition(af, p.Katalog);
                antrieb["passt"] = Passt(vdef, e.Text("familientyp"), adef, e.Text("antriebstyp"));
            }
            aus["antrieb"] = antrieb;
            aus["gilt"] = antrieb is not null && antrieb["regeltechnik"] is not null ? "antrieb" : "familie";
            return aus;
        }
        var fb = Param.Text(par, "familie")
            ?? throw new HlksFehler("element oder familie fehlt.", "element (Kennzeichen) oder familie + typ angeben.");
        return Teil(p, Familien.Hole(p.Modell, fb), Param.Text(par, "typ"), null);
    }

    static JsonObject Teil(Projekt p, Element fam, string? typ, JsonObject? exemplar)
    {
        var def = Familien.Definition(fam, p.Katalog);
        if (typ is not null && def.Typ(typ) is null)
            throw new HlksFehler($"Typ «{typ}» gibt es in {fam.Kennzeichen} «{fam.Name}» nicht.", "Typen: " + string.Join(", ", def.Typen.Select(t => t.Name).Take(20)));
        var q = def.Quelle;
        return new JsonObject
        {
            ["familie"] = fam.Kennzeichen, ["name"] = fam.Name, ["kategorie"] = def.Kategorie,
            ["hersteller"] = q["hersteller"]?.DeepClone(), ["produkt"] = q["produkt"]?.DeepClone(), ["typ"] = typ,
            ["gueltig"] = def.Gueltig,
            ["regeltechnik"] = def.Gueltig ? FamRegeltechnik.Aufloesen(def, typ ?? def.Typen.FirstOrDefault()?.Name, exemplar) : null,
            ["antriebe"] = q["antriebe"]?.DeepClone(),
        };
    }

    /// <summary>Steht der Antrieb (Familie, Typ) in der Kombinationstabelle des Ventils für dessen Typ? null = Ventil ohne Tabelle.</summary>
    static bool? Passt(FamilieDef ventil, string? ventiltyp, FamilieDef antrieb, string? antriebstyp)
    {
        if (ventil.Regeltechnik is not { Antriebe.Count: > 0 } rt) return null;
        return rt.Antriebe.Any(a =>
            (string.Equals(a.Familie, antrieb.Name, StringComparison.OrdinalIgnoreCase) || a.Familie == antrieb.Id)
            && (a.Typen.Count == 0 || (antriebstyp is not null && a.Typen.Contains(antriebstyp)))
            && (a.Fuer.Count == 0 || (ventiltyp is not null && a.Fuer.Contains(ventiltyp))));
    }
}
