// Familien (Modul 2, B4): Befehle konflikt_entscheiden, familie_freigeben, familie_speichern (entwurf/modul-2.md, «Grundsätze»:
// Freigabe nur durch einen Menschen, Widersprüche nie still auflösen). Registriert in FamilienBefehle.Registrieren.
//
// konflikt_entscheiden  nur Akteur-Art mensch: setzt den Wert in die Definition (Typwert bzw. Vorgabewert) und den Entscheid
//                       {wert, akteur, zeit, begruendung} an den Konflikt; Status entwurf, Version + 1.
// wert_bestaetigen      nur mensch (Nachtrag Hauptlauf 18.10.2026): nimmt einen Wert ohne Quelle bzw. «ungeprüft-bild» ausdrücklich
//                       an – abdeckung.<schlüssel>.bestaetigt {wert, akteur, zeit, begruendung}, Status mensch; fällt weg, sobald
//                       sich der Wert ändert. Status entwurf, Version + 1.
// familie_freigeben     nur mensch; nur ohne Fehler (Definition, Typen, Quellen), ohne offene Konflikte, ohne fehlende
//                       Pflichtquellen und ohne Masse ausserhalb der Toleranz. Ergebnis: Status freigegeben, freigabe {akteur,
//                       zeit, version, …}; Werte aus Bildern bleiben erlaubt, die Freigabe nennt ihre Anzahl.
// familie_speichern     nur mensch. Nachtrag B7: ohne datei = familie_in_bibliothek (Bürobibliothek, jeder Status). Mit datei
//                       (Ausfuhr, nur freigegebene Familien): Definition nach vollem Pfad, mit unterlagen als Verweis (Datei,
//                       Prüfsumme), ohne Unterlagen und Befunde. Eine vorhandene Datei derselben Familie nur mit höherer
//                       Version überschreiben, sonst Hinweis. Entscheid Hauptlauf 19.10.2026 (C3): eine RELATIVE datei wird
//                       abgelehnt – die mitgelieferte Bibliothek kataloge/familien ist nur lesbar (kommt mit der Software).
// ANNAHMEN (Prüfliste): «nur mensch» heisst Herkunft art mensch (nicht ki, beispiel, import); auch familie_speichern ist der KI
//   gesperrt (schreibt in die Bibliothek der Software); begruendung ist freiwillig (Hinweis, wenn sie fehlt); ein Entscheid darf
//   einen Wert ausserhalb der Quellen setzen (Hinweis), er gilt dann als Wert eines Menschen; die Freigabe ändert die Version
//   nicht; Dateiname aus dem Familiennamen ohne Umlaute und Sonderzeichen; rückgängig nimmt bibliothek zurück, nicht die Datei.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienFreigabe
{
    /// <summary>Lehnt jeden Akteur ausser einem Menschen ab – mit klarer Meldung für die KI.</summary>
    public static void NurMensch(Kontext k, string was)
    {
        if (k.Herkunft.Art == "mensch") return;
        throw new HlksFehler(k.Herkunft.Art == "ki"
                ? $"{was} darf nur ein Mensch – der Akteur «{k.Herkunft.Akteur}» ist eine KI (Herkunft ki) und wird abgelehnt."
                : $"{was} darf nur ein Mensch – der Akteur «{k.Herkunft.Akteur}» hat die Herkunft «{k.Herkunft.Art}».",
            "Den Punkt im Bericht nennen; Christian entscheidet bzw. gibt frei (Oberfläche «Familie aus Unterlagen» oder als Mensch angemeldet).");
    }

    internal static Wert W(Kontext k, JsonNode? inhalt) => new() { Inhalt = inhalt, Herkunft = k.Herkunft };

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Stand einer gespeicherten Familie für die Freigabe: Quellenprüfung auf Abruf, Massvergleich, Sperrgründe. Fehler = Definition,
    /// Typen, Quellen und nicht umschlüsselbare Ids; Offen = offene Konflikte, Pflichtwerte ohne Quelle, Masse ausserhalb der Toleranz;
    /// Sperren = beides. Nachfuehrung = Leserversion der Quellen (Nachtrag 21.10.2026, in der Definition schon umgeschlüsselt).</summary>
    public sealed record Stand(FamilieDef Familie, JsonObject Definition, QuellenErgebnis? Quellen, List<MassZeile> Masse, List<FamFehler> Sperren)
    {
        public QuellenNachfuehrung? Nachfuehrung { get; init; }
        /// <summary>Fehler der Definition bzw. der Auswertung der Typen.</summary>
        public List<FamFehler> DefinitionFehler { get; init; } = [];
        public List<FamFehler> Fehler { get; init; } = [];
        public List<FamFehler> Offen { get; init; } = [];
    }

    public static Stand Pruefen(Projekt p, Element fam, Katalog kat, string akteurArt)
    {
        var fehler = new List<FamFehler>();
        var offen = new List<FamFehler>();
        var wert = fam.Eigenschaft("definition");
        var def = (wert?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? [];
        var f = FamilieDef.Lesen(def, kat);
        if (!f.Gueltig)
        {
            fehler.AddRange(f.Fehler);
            return new Stand(f, def, null, [], [.. fehler]) { Fehler = fehler, DefinitionFehler = [.. fehler] };
        }
        foreach (var t in f.Typen)
        {
            var a = FamilienAuswertung.Aufloesen(f, t.Name, null);
            foreach (var x in a.Fehler) fehler.Add(new FamFehler($"typ «{t.Name}»: {x.Stelle}", x.Meldung, x.Vorschlag));
        }
        var definitionFehler = fehler.ToList();
        var q = new BefundQuelle(p);
        // Nachtrag 21.10.2026: Quellen einer älteren Leserversion im Speicher umschlüsseln (die Freigabe speichert die Definition so)
        var n = FamilienQuellenVersion.Nachfuehren(p, def, null, q);
        foreach (var x in n.NichtZuordenbar)
            fehler.Add(new FamFehler(x.Stelle, $"Quelle «{x.Id}» (Befund-Id nach Leser {x.Von}) lässt sich nicht eindeutig auf Leser {x.Nach} umschlüsseln: {x.Grund}",
                "Den Befund in der aktuellen Fassung suchen (Abfrage befunde der Unterlage) und die Quelle mit seiner Id neu nennen (familie_aendern)."));
        var e = FamilienQuellenAnalyse.Rechnen(p, def, f, def, wert?.Herkunft.Art, true, akteurArt, q);
        fehler.AddRange(e.Fehler);
        foreach (var k in e.Konflikte.OfType<JsonObject>().Where(k => k["entscheid"] is null))
            offen.Add(new FamFehler($"Konflikt {T(k["id"])}", $"«{T(k["parameter"])}»{(T(k["typ"]) is { } ty ? $" (Typ {ty})" : "")} ist offen: {string.Join(" / ", (k["werte"] as JsonArray ?? []).Select(w => w?["wert"]?.ToJsonString()))} (Vorschlag {k["vorschlag"]?.ToJsonString()})",
                $"konflikt_entscheiden {{ familie, konflikt: «{T(k["id"])}», wert, begruendung }} (nur ein Mensch)."));
        foreach (var s in e.FehlendePflicht)
            offen.Add(new FamFehler($"abdeckung «{s}»", "Pflichtwert ohne Quelle (weder aus einer Unterlage noch von einem Menschen gesetzt)",
                "Quelle (Befund-Id) nennen oder den Wert als Mensch setzen (familie_aendern)."));
        if (FamilienQuellenAnalyse.AusUnterlagen(def)) FamilienQuellenAnalyse.Anwenden(def, e);
        var masse = f.Masse.Count > 0 ? FamilienMasse.Vergleichen(FamilieDef.Lesen(def, kat), def, q, null) : [];
        foreach (var z in masse.Where(z => z.Bestanden == false))
            offen.Add(new FamFehler($"mass «{z.Mass}» Typ «{z.Typ}»", $"ausserhalb der Toleranz: Soll {QuellVergleiche.F(z.Soll!.Value)} mm ({z.SollQuelle}), Ist {QuellVergleiche.F(z.Ist!.Value)} mm, Toleranz ±{QuellVergleiche.F(z.Toleranz!.Value)} mm",
                "Körper, Massbezug (von/bis) oder Typwert korrigieren; Abweichungsliste: familie_masse, Kontrollbild: familie_vergleichen."));
        return new Stand(f, def, e, masse, [.. fehler, .. offen]) { Nachfuehrung = n, Fehler = fehler, Offen = offen, DefinitionFehler = definitionFehler };
    }

    /// <summary>Dateiname der Bibliothek aus dem Familiennamen: ohne Umlaute und Sonderzeichen.</summary>
    public static string Dateiname(string name)
    {
        var t = name.Trim().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss").Replace('×', 'x');
        var sb = new StringBuilder();
        foreach (var c in t) sb.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' ? c : '_');
        var s = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_', '.');
        return (s.Length == 0 ? "familie" : s) + ".familie.json";
    }

    static readonly JsonSerializerOptions Schreiben = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static byte[] Bytes(JsonObject def) => new UTF8Encoding(false).GetBytes(def.ToJsonString(Schreiben));
}

public sealed class KonfliktEntscheiden : IBefehl
{
    public string Name => "konflikt_entscheiden";
    public const string Text = "Entscheidet einen Konflikt einer Familie (nur ein Mensch – die KI wird abgelehnt): wert wird in die Definition gesetzt (Typwert bzw. Vorgabewert des Parameters), " +
        "der Entscheid { wert, akteur, zeit, begruendung } bleibt am Konflikt, solange sich die Werte der Quellen nicht ändern. Danach Status entwurf (Version + 1); freigeben mit familie_freigeben. " +
        "Konflikte und ihre Ids: Abfrage familie (definition.konflikte).";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("konflikt", "text", true, "Id des Konflikts (k-…) aus definition.konflikte"),
        new("wert", "text", true, "Gewählter Wert (Zahl in der Einheit des Parameters bzw. Text), meist einer der Werte des Konflikts"),
        new("begruendung", "text", false, "Begründung des Entscheids (z. B. «Massbild DXF ist massgebend, Rücksprache Hersteller»)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        FamilienFreigabe.NurMensch(k, "Konflikte entscheiden");
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        var id = Param.Text(p, "konflikt", true)!.Trim();
        var def = (fam.Eigenschaft("definition")?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? [];
        var konflikte = (def["konflikte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var konflikt = konflikte.FirstOrDefault(x => string.Equals(x["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Konflikt «{id}» gibt es in {fam.Kennzeichen} «{fam.Name}» nicht.",
                konflikte.Count > 0 ? "Konflikte: " + string.Join(", ", konflikte.Select(x => $"{x["id"]} ({x["parameter"]}{(x["typ"] is { } t ? ", Typ " + t : "")}{(x["entscheid"] is null ? ", offen" : ", entschieden")})"))
                                    : "Die Familie hat keine Konflikte (Abfrage familie, definition.konflikte).");
        var f = Familien.Definition(fam, k.Katalog);
        var pn = konflikt["parameter"]?.ToString() ?? "";
        var typ = konflikt["typ"]?.ToString();
        if (!f.NachName.TryGetValue(pn, out var par))
            throw new HlksFehler($"Der Parameter «{pn}» des Konflikts gibt es nicht mehr.", "Die Familie neu prüfen lassen (familie_aendern oder familie_entwurf); der Konflikt verschwindet dann.");
        var text = Param.Text(p, "wert", true)!.Trim();
        JsonNode wert;
        if (par.IstText) wert = JsonValue.Create(text);
        else
        {
            var z = p["wert"] is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? Json.Zahl(v) : QuellZahlen.Lesen(text, bruch: false)?.Wert;
            if (z is not double d) throw new HlksFehler($"wert «{text}» ist keine Zahl – «{pn}» ist {par.Datentyp}.", $"Zahl in {QuellVergleiche.ParameterEinheit(par) ?? "der Einheit des Parameters"} angeben, z. B. {konflikt["vorschlag"]?.ToJsonString()}.");
            wert = JsonValue.Create(d);
        }
        var quellenWerte = (konflikt["werte"] as JsonArray ?? []).Select(w => Json.Zahl(w?["wert"])).Where(x => x is not null).Select(x => x!.Value).ToList();
        if (!par.IstText && Json.Zahl(wert) is double gew && !quellenWerte.Any(x => Math.Abs(x - gew) < 1e-6))
            k.Hinweise.Add($"Der Wert {QuellVergleiche.F(gew)} ist keiner der Quellenwerte ({string.Join(" / ", quellenWerte.Select(QuellVergleiche.F))}); er gilt als Wert eines Menschen.");
        var begruendung = Param.Text(p, "begruendung")?.Trim();
        if (string.IsNullOrEmpty(begruendung)) k.Hinweise.Add("Ohne Begründung entschieden – für die Nachvollziehbarkeit eine Begründung angeben.");
        konflikt["entscheid"] = new JsonObject
        {
            ["wert"] = wert.DeepClone(), ["akteur"] = k.Herkunft.Akteur, ["zeit"] = k.Herkunft.Zeit.ToString("o", CultureInfo.InvariantCulture),
            ["begruendung"] = string.IsNullOrEmpty(begruendung) ? null : begruendung,
        };
        // Wert in die Definition
        if (typ is null)
        {
            var po = (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => o["name"]?.ToString().Trim() == pn);
            if (po is not null) po["wert"] = wert.DeepClone();
        }
        else
        {
            var to = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => o["name"]?.ToString().Trim() == typ)
                ?? throw new HlksFehler($"Den Typ «{typ}» des Konflikts gibt es nicht mehr.", "Die Familie neu prüfen lassen; der Konflikt verschwindet dann.");
            if (to["werte"] is not JsonObject w) to["werte"] = w = [];
            w[pn] = wert.DeepClone();
        }
        FamilienBefehle.Speichern(k, fam, def, Name, "entwurf", FamilienBefehle.Version(fam) + 1, null);
        k.Hinweise.Add($"Konflikt {konflikt["id"]} entschieden: «{pn}»{(typ is null ? "" : $" (Typ {typ})")} = {wert.ToJsonString()} von {k.Herkunft.Akteur}. Status entwurf – freigeben mit familie_freigeben.");
    }
}

public sealed class WertBestaetigen : IBefehl
{
    public string Name => "wert_bestaetigen";
    public const string Text = "Ein Mensch nimmt einen Wert ohne Quelle oder einen aus einem Bild gelesenen, ungeprüften Wert ausdrücklich an (nur ein Mensch – die KI wird abgelehnt): " +
        "in der Abdeckung wird der Status mensch mit bestaetigt { wert, akteur, zeit, begruendung }. Der Wert zählt dann für die Freigabe als belegt; ändert er sich, fällt die Bestätigung weg. " +
        "Mit typ der Typwert («Typ/Parameter»), ohne typ der Vorgabewert des Parameters. Danach Status entwurf (Version + 1). Geprüfte Werte brauchen keine Bestätigung, Konflikte entscheidet konflikt_entscheiden.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("parameter", "text", true, "Parameter, dessen Wert bestätigt wird"),
        new("typ", "text", false, "Typ, dessen Wert bestätigt wird; leer = Vorgabewert des Parameters"),
        new("begruendung", "text", false, "Begründung (z. B. «aus dem Massbild gelesen und am Muster nachgemessen»)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        FamilienFreigabe.NurMensch(k, "Werte bestätigen");
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        var f = Familien.Definition(fam, k.Katalog);
        if (!f.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", f.Fehler);
        var pn = Param.Text(p, "parameter", true)!.Trim();
        if (!f.NachName.TryGetValue(pn, out var par))
            throw new HlksFehler($"Parameter «{pn}» gibt es in {fam.Kennzeichen} nicht.", "Parameter: " + string.Join(", ", f.Parameter.Select(x => x.Name)));
        if (par.HatFormel)
            throw new HlksFehler($"«{pn}» hat eine Formel – sein Wert folgt aus anderen Parametern und wird nicht einzeln bestätigt.", "Die Parameter der Formel bestätigen bzw. belegen.");
        var typ = Param.Text(p, "typ")?.Trim();
        if (string.IsNullOrEmpty(typ)) typ = null;
        if (typ is not null && f.Typ(typ) is null)
            throw new HlksFehler($"Typ «{typ}» gibt es in {fam.Kennzeichen} nicht.", "Typen: " + string.Join(", ", f.Typen.Select(t => t.Name)));
        var schluessel = typ is null ? pn : $"{typ}/{pn}";

        // Stand der Abdeckung (auf Abruf, aus der gespeicherten, vertrauten Definition)
        var wert = fam.Eigenschaft("definition");
        var def = (wert?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? [];
        var q = new BefundQuelle(k.Projekt);
        var n = FamilienQuellenVersion.Nachfuehren(k.Projekt, def, null, q);   // Nachtrag 21.10.2026: ältere Leserversion umschlüsseln
        var e = FamilienQuellenAnalyse.Rechnen(k.Projekt, def, f, def, wert?.Herkunft.Art, true, k.Herkunft.Art, q);
        var fehler = n.NichtZuordenbar.Select(x => new FamFehler(x.Stelle, $"Quelle «{x.Id}» (Befund-Id nach Leser {x.Von}) lässt sich nicht eindeutig auf Leser {x.Nach} umschlüsseln: {x.Grund}",
            "Den Befund in der aktuellen Fassung suchen (Abfrage befunde der Unterlage) und die Quelle mit seiner Id neu nennen (familie_aendern).")).Concat(e.Fehler).ToList();
        if (fehler.Count > 0) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", fehler);
        if (e.Abdeckung[schluessel] is not JsonObject eintrag)
            throw new HlksFehler($"«{schluessel}» hat keinen eigenen Wert, der sich bestätigen liesse.",
                typ is not null && par.Wert is not null ? $"Der Typ übernimmt den Vorgabewert: ohne typ bestätigen (parameter «{pn}»)."
                                                         : "Zuerst einen Wert setzen (familie_aendern); setzt ihn ein Mensch, gilt er ohnehin als «mensch».");
        var status = eintrag["status"]?.ToString();
        if (eintrag["konflikt"] is not null)
        {
            if (status == "konflikt")
                throw new HlksFehler($"«{schluessel}» hat einen offenen Konflikt ({eintrag["konflikt"]}).", $"konflikt_entscheiden {{ familie, konflikt: «{eintrag["konflikt"]}», wert, begruendung }}.");
            k.Hinweise.Add($"«{schluessel}» ist durch einen Konfliktentscheid schon von einem Menschen festgelegt – keine Bestätigung nötig.");
            return;
        }
        if (eintrag["wert"] is null)
            throw new HlksFehler($"«{schluessel}» hat keinen Wert.", "Zuerst den Wert setzen (familie_aendern) oder eine Quelle nennen.");
        if (status == "geprüft")
        {
            k.Hinweise.Add($"«{schluessel}» ist schon gegen seine Quelle geprüft – keine Bestätigung nötig.");
            return;
        }
        if (eintrag["bestaetigt"] is not null)
        {
            k.Hinweise.Add($"«{schluessel}» = {eintrag["wert"]!.ToJsonString()} ist schon bestätigt ({eintrag["bestaetigt"]!["akteur"]}).");
            return;
        }
        FamilienQuellenAnalyse.Anwenden(def, e);
        var begruendung = Param.Text(p, "begruendung")?.Trim();
        if (string.IsNullOrEmpty(begruendung)) k.Hinweise.Add("Ohne Begründung bestätigt – für die Nachvollziehbarkeit eine Begründung angeben.");
        var neu = (JsonObject)def["abdeckung"]![schluessel]!;
        neu["bestaetigt"] = new JsonObject
        {
            ["wert"] = eintrag["wert"]!.DeepClone(), ["akteur"] = k.Herkunft.Akteur, ["zeit"] = k.Herkunft.Zeit.ToString("o", CultureInfo.InvariantCulture),
            ["begruendung"] = string.IsNullOrEmpty(begruendung) ? null : begruendung,
        };
        neu["status"] = "mensch";
        FamilienBefehle.Speichern(k, fam, def, Name, "entwurf", FamilienBefehle.Version(fam) + 1, null);
        k.Hinweise.Add($"«{schluessel}» = {eintrag["wert"]!.ToJsonString()} von {k.Herkunft.Akteur} bestätigt (vorher {status}{(eintrag["vermerk"] is { } v ? $": {v}" : "")}). Status entwurf – freigeben mit familie_freigeben.");
    }
}

public sealed class FamilieFreigeben : IBefehl
{
    public string Name => "familie_freigeben";
    public const string Text = "Gibt eine Familie frei (nur ein Mensch – die KI wird abgelehnt): nur ohne Fehler (Definition, Typen, Quellen), ohne offene Konflikte, ohne Pflichtwerte ohne Quelle und " +
        "ohne Masse ausserhalb der Toleranz (Abweichungsliste familie_masse). Ergebnis: Status freigegeben und freigabe { akteur, zeit, version, geprueft, ausBild, mensch, konflikteEntschieden }. " +
        "Werte aus Bildern (ungeprüft) bleiben erlaubt; die Freigabe nennt ihre Anzahl. Jede spätere Änderung setzt die Familie wieder auf entwurf.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("familie", "text", true, "Familie (Kennzeichen, Name oder id)")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        FamilienFreigabe.NurMensch(k, "Familien freigeben");
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        if (fam.Text("status") == "freigegeben" && fam.Eigenschaft("freigabe") is not null)
        {
            k.Hinweise.Add($"{fam.Kennzeichen} «{fam.Name}» ist schon freigegeben (Version {FamilienBefehle.Version(fam)}).");
            return;
        }
        var s = FamilienFreigabe.Pruefen(k.Projekt, fam, k.Katalog, k.Herkunft.Art);
        if (s.Sperren.Count > 0)
        {
            var f = FamilienPruefung.Fehlermeldung($"{fam.Kennzeichen} «{fam.Name}» lässt sich nicht freigeben", s.Sperren);
            throw new HlksFehler(f.Message, f.Vorschlag);
        }
        int version = FamilienBefehle.Version(fam);
        var def = s.Definition;
        def["status"] = "freigegeben";
        def["version"] = version;
        var e = s.Quellen!;
        int entschieden = e.Konflikte.OfType<JsonObject>().Count(x => x["entscheid"] is not null);
        var freigabe = new JsonObject
        {
            ["akteur"] = k.Herkunft.Akteur, ["zeit"] = k.Herkunft.Zeit.ToString("o", CultureInfo.InvariantCulture), ["version"] = version,
            ["geprueft"] = e.Geprueft, ["ausBild"] = e.AusBild, ["mensch"] = e.Mensch, ["bestaetigt"] = e.Bestaetigt, ["konflikteEntschieden"] = entschieden,
            ["masseBestanden"] = s.Masse.Count(z => z.Bestanden == true),
        };
        var el = fam.Kopie();
        el.Eigenschaften["status"] = FamilienFreigabe.W(k, JsonValue.Create("freigegeben"));
        el.Eigenschaften["definition"] = FamilienFreigabe.W(k, def);
        el.Eigenschaften["freigabe"] = FamilienFreigabe.W(k, freigabe);
        k.Setzen(el);
        k.Hinweise.Add($"{fam.Kennzeichen} «{fam.Name}» freigegeben (Version {version}) von {k.Herkunft.Akteur}: {e.Geprueft} Wert(e) gegen die Quelle geprüft, " +
            $"{e.AusBild} aus Bildern gelesen (ungeprüft), {e.Mensch} von einem Menschen (davon {e.Bestaetigt} bestätigt), {entschieden} Konflikt(e) entschieden, {s.Masse.Count(z => z.Bestanden == true)} Masse je Typ bestanden.");
    }
}

public sealed class FamilieSpeichern : IBefehl
{
    public string Name => "familie_speichern";
    public const string Text = "Schreibt eine Familie in die Familienbibliothek (nur ein Mensch; ohne datei derselbe Weg wie familie_in_bibliothek – Bürobibliothek, jeder Status, " +
        "gleicher Inhalt = keine neue Version). Mit datei: Ausfuhr einer freigegebenen Familie als Datei <name>.familie.json an einen vollen Pfad (ein relativer Name wird abgelehnt: die mitgelieferte Bibliothek kataloge/familien ist nur lesbar), " +
        "die Definition mit unterlagen als Verweis (Datei, Prüfsumme) – ohne die Unterlagen selbst und ohne Befunde; eine vorhandene Datei derselben Familie wird nur mit höherer " +
        "Version überschrieben (sonst Hinweis), bibliothek = { datei, pruefsumme }; rückgängig nimmt das zurück, die Datei bleibt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie (Kennzeichen, Name oder id)"),
        new("datei", "text", false, "Nur für die Ausfuhr als Datei: voller Pfad (ein relativer Name wird abgelehnt – kataloge/familien ist nur lesbar); leer = in die Familienbibliothek (wie familie_in_bibliothek)"),
    ];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        FamilienFreigabe.NurMensch(k, "Familien in die Bibliothek speichern");
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        if (string.IsNullOrWhiteSpace(Param.Text(p, "datei")))
        {
            // Nachtrag B7: ohne datei derselbe Weg wie familie_in_bibliothek (Bürobibliothek, auch Entwürfe)
            FamilieInBibliothek.Schreiben(k, [fam], false, Name);
            return;
        }
        // Entscheid Hauptlauf 19.10.2026: die mitgelieferte Bibliothek (kataloge/familien) ist nur lesbar – dorthin schreibt kein Befehl mehr
        var gewuenscht = Param.Text(p, "datei")!.Trim();
        if (!Path.IsPathRooted(gewuenscht))
            throw new HlksFehler($"datei «{gewuenscht}» ist ein relativer Name: die mitgelieferte Bibliothek kataloge/familien ist nur lesbar und wird nicht beschrieben.",
                $"Für die Familienbibliothek des Büros datei weglassen (derselbe Weg wie familie_in_bibliothek); für eine Ausfuhr als Datei den vollen Pfad angeben, z. B. C:\\Ablage\\{FamilienFreigabe.Dateiname(fam.Name)}.familie.json.");
        if (fam.Text("status") != "freigegeben")
            throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» ist nicht freigegeben (Status {fam.Text("status") ?? "unbekannt"}) – in die Bibliothek kommen nur freigegebene Familien.",
                "Zuerst familie_freigeben (nur ein Mensch).");
        var def = (fam.Eigenschaft("definition")?.Inhalt as JsonObject)?.DeepClone() as JsonObject ?? [];
        int version = FamilienBefehle.Version(fam);
        def["status"] = "freigegeben";
        def["version"] = version;
        // unterlagen als Verweis: alle Unterlagen der Quellen (die Prüfung führt die Liste; fehlt sie, hier ergänzen)
        if (def["unterlagen"] is not JsonArray { Count: > 0 } && FamilienQuellenAnalyse.AusUnterlagen(def))
        {
            var q = new BefundQuelle(k.Projekt);
            var liste = new JsonArray();
            foreach (var px in FamilienQuellenAnalyse.AlleQuellen(def).SelectMany(x => x.Ids).Select(BefundQuelle.Praefix).Where(x => x is not null).Distinct().Order(StringComparer.Ordinal))
                if (q.Unterlage(px!) is { } u)
                    liste.Add(new JsonObject { ["unterlage"] = u.Id, ["datei"] = u.Original, ["pruefsumme"] = BefundQuelle.Pruefsumme(u.Pfad), ["art"] = u.Art });
            if (liste.Count > 0) def["unterlagen"] = liste;
        }

        var ordner = Path.Combine(k.Katalog.Ordner, "familien");
        var pfad = Path.GetFullPath(gewuenscht.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? gewuenscht : gewuenscht + ".familie.json");
        if (File.Exists(pfad))
        {
            JsonObject? alt = null;
            try { alt = JsonNode.Parse(File.ReadAllBytes(pfad)) as JsonObject; } catch (JsonException) { }
            var altId = T(alt?["id"]);
            if (altId is not null && altId != T(def["id"]))
                throw new HlksFehler($"Die Datei «{pfad}» gehört zu einer anderen Familie (id {altId}, «{T(alt?["name"])}»).",
                    "Einen anderen Dateinamen wählen (Parameter datei).");
            var altVersion = Json.Zahl(alt?["version"]) ?? 0;
            if (altVersion >= version)
            {
                k.Hinweise.Add($"Nicht gespeichert: «{pfad}» hat schon Version {altVersion:0}, {fam.Kennzeichen} hat Version {version} – überschrieben wird nur mit höherer Version " +
                    "(die Familie ändern, freigeben, dann speichern).");
                return;
            }
        }
        var bytes = FamilienFreigabe.Bytes(def);
        if (!k.Trockenlauf)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
                var temp = Path.Combine(Path.GetDirectoryName(pfad)!, $".{Path.GetFileName(pfad)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllBytes(temp, bytes);
                    File.Move(temp, pfad, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new HlksFehler($"«{pfad}» lässt sich nicht schreiben: {ex.Message}", "Ordner und Zugriffsrechte prüfen oder einen anderen Pfad (datei) wählen.");
            }
        }
        var pruefsumme = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var relativ = pfad.StartsWith(Path.GetFullPath(ordner) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(ordner, pfad).Replace('\\', '/') : pfad;
        var el = fam.Kopie();
        el.Eigenschaften["bibliothek"] = FamilienFreigabe.W(k, new JsonObject { ["datei"] = relativ, ["pruefsumme"] = pruefsumme });
        k.Setzen(el);
        int unterlagen = (def["unterlagen"] as JsonArray)?.Count ?? 0;
        k.Hinweise.Add($"{fam.Kennzeichen} «{fam.Name}» {(k.Trockenlauf ? "würde gespeichert" : "gespeichert")}: {pfad} (Version {version}, {unterlagen} Unterlage(n) als Verweis, ohne Befunde). " +
            "Rückgängig ändert die Datei nicht.");
    }
}
