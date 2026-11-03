// Verbindungstabelle (Christian 03.11.2026: «es braucht eine art verbindungstabelle. dass die unterschiedlichen masssysteme keine
// konflikte verursachen.» – Entscheide: Übergang automatisch setzen, bei mehreren Möglichkeiten eine Auswahl vorschlagen, Zuordnungen aus
// den Familien). Keine Werte aus dem Gedächtnis: DN, Aussendurchmesser und Zoll stehen belegt in den Typen der Leitungsserien bzw. an den
// Anschlüssen der Familien; diese Datei vergleicht nur.
//
//  - Verbindungstechnik: Anschluss.verbindung und leitung.verbindung (Arten unten). Fehlt eine Seite, bleibt es beim bisherigen Verhalten
//    (Grösse vergleichen, Reduktion).
//  - Direkt verbindbar (Büroregel, ANNAHME zur Bestätigung durch Christian): Innen- mit Aussengewinde, sonst gleiche Technik (Flansch an
//    Flansch, Press an Press …).
//  - Gleiche Grösse: beide Gewinde → gleiche Zollangabe (Text normalisiert, z. B. «Rp 1 1/4» = «G 1 1/4» = «1 1/4"»); sonst DN gleich,
//    sonst Durchmesser d gleich.
//  - Übergang: Familien der Kategorie Uebergang aus leitung.uebergaenge; Anschluss a = Seite der Serie (Grösse der Leitung, im Ursprung wie
//    bei der Reduktion), b = Geräteseite (Verbindung direkt verbindbar mit dem Gerät, gleiche Grösse).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ein möglicher Übergang: Familie, Typ, Grösse auf der Seite der Serie und die Formteilwahl für das Routing.</summary>
public sealed record UebergangKandidat(Element Familie, string Typ, Leitungsgroesse SerienSeite, string GeraeteSeite, FormteilWahl Wahl);

/// <summary>Ergebnis eines Vergleichs Gerät ↔ Leitungsserie: direkt | uebergang | kein_uebergang | unbekannt.</summary>
public sealed record VerbindungsErgebnis(string Status, string Text, IReadOnlyList<UebergangKandidat> Kandidaten);

public static class Verbindungen
{
    public static readonly string[] Arten =
        ["gewinde-innen", "gewinde-aussen", "verschraubung", "flansch", "press", "schweissen", "loeten", "kleben", "steck", "klemm", "muffe"];

    /// <summary>Lassen sich zwei Verbindungsarten direkt verbinden? null = eine Seite ohne Angabe (unbekannt).</summary>
    public static bool? Direkt(string? a, string? b)
    {
        if (a is null || b is null) return null;
        if ((a, b) is ("gewinde-innen", "gewinde-aussen") or ("gewinde-aussen", "gewinde-innen")) return true;
        if (a.StartsWith("gewinde", StringComparison.Ordinal) && b.StartsWith("gewinde", StringComparison.Ordinal)) return false;   // innen/innen, aussen/aussen
        return a == b;
    }

    static readonly Regex ZollMuster = new(@"(\d+/\d+)|(\d+)(?:[ \-+]+(\d+/\d+))?", RegexOptions.CultureInvariant);

    /// <summary>Zollangabe eines Gewindetexts normalisiert («Rp 1 1/4», «G1-1/4», «1 1/4"» → «1 1/4»; «Rp 3/4» → «3/4»), null ohne Zahl.</summary>
    public static string? Zoll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = ZollMuster.Match(text);
        if (!m.Success) return null;
        if (m.Groups[1].Success) return m.Groups[1].Value;   // nur Bruch («3/4»)
        return m.Groups[3].Success ? $"{m.Groups[2].Value} {m.Groups[3].Value}" : m.Groups[2].Value;
    }

    /// <summary>Gleiche Grösse zweier Anschlüsse (Gewinde nach Zoll, sonst DN, sonst d).</summary>
    public static bool GleicheGroesse(string? gewinde1, Leitungsgroesse g1, string? gewinde2, Leitungsgroesse g2)
    {
        if (Zoll(gewinde1) is { } z1 && Zoll(gewinde2) is { } z2) return z1 == z2;
        if (g1.Dn is double d1 && g2.Dn is double d2) return Math.Abs(d1 - d2) <= Leitungsgroesse.Toleranz;
        if (g1.D is double x1 && g2.D is double x2) return Math.Abs(x1 - x2) <= Leitungsgroesse.Toleranz;
        return false;
    }

    /// <summary>Vergleicht den Geräteanschluss mit der Serie (Grösse g auf der Seite der Leitung; null = jede Grösse der Serie, für
    /// Vorschläge). Übergänge aus leitung.uebergaenge.</summary>
    public static VerbindungsErgebnis Pruefen(Modell m, Katalog k, Leitungsserie s, Leitungsgroesse? g, AnschlussStelle geraet)
    {
        var direkt = Direkt(s.Block.Verbindung, geraet.Verbindung);
        if (direkt is null)
            return new("unbekannt", $"Verbindung {(s.Block.Verbindung is null ? $"der Serie {s.Bezeichnung}" : $"des Anschlusses {geraet.Name}")} nicht angegeben – nur die Grösse wird verglichen.", []);
        if (direkt == true) return new("direkt", $"{geraet.Verbindung} an {s.Block.Verbindung}: direkt verbindbar.", []);

        var kandidaten = new List<UebergangKandidat>();
        var gruende = new List<string>();
        foreach (var bezug in s.Block.Uebergaenge ?? [])
        {
            var fam = m.DerKlasse(Familien.Klasse).FirstOrDefault(f => Familien.DefinitionsId(f) == bezug || string.Equals(f.Name?.Trim(), bezug, StringComparison.OrdinalIgnoreCase));
            if (fam is null) { gruende.Add($"Übergangs-Familie «{bezug}» ist nicht im Projekt"); continue; }
            var def = Familien.Definition(fam, k);
            if (!def.Gueltig) { gruende.Add($"{fam.Kennzeichen} «{fam.Name}» ist ungültig"); continue; }
            foreach (var t in def.Typen)
            {
                var a = FamilienAuswertung.Aufloesen(def, t.Name, null);
                if (!a.Gueltig) continue;
                var an = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
                var bn = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals("b", StringComparison.OrdinalIgnoreCase));
                if (an is null || bn is null) continue;
                var ga = s.Norm(Leitungsgroesse.Von(an));
                if (g is { } soll && !ga.Gleich(s.Norm(soll))) continue;
                if (Direkt(bn.Verbindung, geraet.Verbindung) != true) continue;
                if (!GleicheGroesse(bn.Gewinde, Leitungsgroesse.Von(bn), geraet.Gewinde, geraet.Groesse)) continue;
                if (Math.Sqrt(an.Lage.Sum(c => c * c)) > Formteilgeometrie.LageToleranz)
                { gruende.Add($"{fam.Kennzeichen} Typ «{t.Name}»: Anschluss a liegt nicht im Ursprung (wie bei der Reduktion verlangt)"); continue; }
                Formteilgeometrie geo;
                try { geo = Formteilgeometrie.Lokal(def, t.Name, null, "uebergang"); }
                catch (HlksFehler f) { gruende.Add($"{fam.Kennzeichen} Typ «{t.Name}»: {f.Message}"); continue; }
                var wahl = new FormteilWahl { Art = "uebergang", Familie = fam, Def = def, Typ = t.Name, Geometrie = geo, Groesse1 = ga, Groesse2 = Leitungsgroesse.Von(bn) };
                kandidaten.Add(new UebergangKandidat(fam, t.Name, ga, $"{bn.Verbindung} {bn.Gewinde ?? Leitungsgroesse.Von(bn).ToString()}", wahl));
            }
        }
        var ziel = $"{geraet.Verbindung} {geraet.Gewinde ?? geraet.Groesse.ToString()} (Anschluss {geraet.Name})";
        if (kandidaten.Count == 0)
            return new("kein_uebergang", $"{s.Block.Verbindung} der Serie {s.Bezeichnung}{(g is { } gx ? " " + gx : "")} passt nicht direkt an {ziel}, und kein Übergang passt" +
                (gruende.Count > 0 ? ": " + string.Join("; ", gruende.Distinct()) : (s.Block.Uebergaenge is null ? " (die Serie nennt keine uebergaenge)" : "")) + ".", []);
        return new("uebergang", $"Übergang nötig: {s.Block.Verbindung} → {ziel}; {kandidaten.Count} Möglichkeit(en).", kandidaten);
    }

    /// <summary>Kandidat für das Routing: genau einer bzw. der gewählte (familie + typ); sonst HlksFehler mit der Auswahl.</summary>
    public static UebergangKandidat? Waehlen(VerbindungsErgebnis e, JsonObject? wahl, string ort)
    {
        if (e.Status is "unbekannt" or "direkt") return null;
        if (e.Status == "kein_uebergang")
            throw new HlksFehler(ort + e.Text, "Eine Übergangs-Familie (Kategorie Uebergang) mit passender Grösse anlegen und in leitung.uebergaenge der Serie nennen, oder eine Serie mit passender Verbindung wählen.");
        if (wahl is not null)
        {
            var fam = wahl["familie"]?.ToString(); var typ = wahl["typ"]?.ToString();
            var k = e.Kandidaten.FirstOrDefault(x => (fam is null || x.Familie.Kennzeichen == fam || x.Familie.Name == fam || Familien.DefinitionsId(x.Familie) == fam) && (typ is null || x.Typ == typ));
            if (k is null) throw new HlksFehler($"{ort}Der gewählte Übergang ({fam} {typ}) passt nicht.", "Möglich: " + Auswahl(e));
            return k;
        }
        if (e.Kandidaten.Count == 1) return e.Kandidaten[0];
        throw new HlksFehler($"{ort}{e.Text} Bitte wählen: {Auswahl(e)}.", "Den Übergang mit uebergang { familie, typ } angeben (Trockenlauf zeigt die Auswahl).");
    }

    static string Auswahl(VerbindungsErgebnis e) =>
        string.Join("; ", e.Kandidaten.Select(x => $"{x.Familie.Kennzeichen} «{x.Familie.Name}» Typ «{x.Typ}» ({x.SerienSeite} → {x.GeraeteSeite})"));

    // ------------------------------------------------------------------ Abfragen

    public const string TabelleName = "verbindungstabelle";
    public const string TabelleText = "Verbindungstabelle aus den Leitungsserien des Projekts (Werte nur aus den Familien): je Grösse DN, Aussendurchmesser, Zoll (bei Gewindeserien " +
        "aus dem Typnamen), Verbindung der Serie und die Übergänge. Grundlage, damit verschiedene Masssysteme (DN, NW, d, Zoll) keine Konflikte verursachen.";
    public const string PruefenName = "verbindung_pruefen";
    public const string PruefenText = "Prüft, wie ein Geräteanschluss an eine Leitungsserie angeschlossen wird: direkt, mit Übergang (Auswahl aller passenden Übergänge mit " +
        "Grösse auf der Seite der Serie) oder kein Übergang (mit Grund). Ohne groesse alle Grössen der Serie als Vorschlag.";

    public static readonly IReadOnlyList<ParameterDef> TabelleParameter = [new("serie", "text", false, "Nur diese Leitungsserie (Kennzeichen, Name oder id)")];
    public static readonly IReadOnlyList<ParameterDef> PruefenParameter =
    [
        new("element", "text", true, "Gerät bzw. Bauteil (Kennzeichen oder Id)"),
        new("anschluss", "text", true, "Name des Anschlusses"),
        new("serie", "text", true, "Leitungsserie (Kennzeichen, Name oder id)"),
        new("groesse", "objekt", false, "Grösse der Leitung, z. B. { \"dn\": 32 }; leer = alle Grössen der Serie"),
    ];

    public static JsonObject Tabelle(Projekt p, JsonObject par)
    {
        var m = p.Modell; var k = p.Katalog;
        var nur = Param.Text(par, "serie");
        var serien = new JsonArray();
        foreach (var f in m.DerKlasse(Familien.Klasse).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (nur is not null && f.Kennzeichen != nur && f.Name != nur && Familien.DefinitionsId(f) != nur) continue;
            Leitungsserie s;
            try { s = Leitungsserie.Von(m, k, f); } catch (HlksFehler) { continue; }
            if (s.Kategorie != "Rohr") continue;
            serien.Add(new JsonObject
            {
                ["serie"] = s.Bezeichnung, ["verbindung"] = s.Block.Verbindung,
                ["uebergaenge"] = s.Block.Uebergaenge is { } u ? new JsonArray(u.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) : null,
                ["groessen"] = new JsonArray(s.Groessen.Where(g => g.Gueltig).Select(g => (JsonNode)new JsonObject
                {
                    ["typ"] = g.Typ, ["dn"] = g.Groesse.Dn ?? s.DnHersteller(g), ["aussen"] = g.Aussen, ["zoll"] = Zoll(g.Typ) is { } z && g.Typ.Contains('"') ? z : null,
                }).ToArray()),
            });
        }
        return new JsonObject { ["serien"] = serien };
    }

    public static JsonObject PruefenAbfrage(Projekt p, JsonObject par)
    {
        var m = p.Modell; var k = p.Katalog;
        var e = m.Hole(Param.Text(par, "element", true), "element");
        var name = Param.Text(par, "anschluss", true)!;
        var st = Anschlusslagen.Von(m, k, e).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"{e.Kennzeichen} hat keinen Anschluss «{name}».", "Anschlüsse mit anschlusslagen abfragen.");
        var s = Leitungsserie.Hole(m, k, Param.Text(par, "serie", true));
        Leitungsgroesse? g = par["groesse"] is { } gn ? Leitungsgroesse.Lesen(gn) : null;
        var r = Pruefen(m, k, s, g, st);
        return new JsonObject
        {
            ["element"] = e.Kennzeichen, ["anschluss"] = st.Name, ["serie"] = s.Bezeichnung, ["status"] = r.Status, ["text"] = r.Text,
            ["auswahl"] = new JsonArray(r.Kandidaten.Select(x => (JsonNode)new JsonObject
            {
                ["familie"] = x.Familie.Kennzeichen, ["name"] = x.Familie.Name, ["typ"] = x.Typ, ["serienseite"] = x.SerienSeite.AlsJson(), ["geraeteseite"] = x.GeraeteSeite,
            }).ToArray()),
        };
    }
}
