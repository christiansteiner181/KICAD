// Ableitungen: Listen, die der Kern deterministisch aus dem Modell erzeugt. Gleiches Modell → gleiche Liste.
// Jede Ableitung trägt den Modellstand, damit erkennbar ist, ob sie veraltet ist. Keine Normwerte in Modul 0a.
// Modul 3 (B4): Leitungslängen je Serie und Grösse (Leitungslaengen), Mengen und Strangliste mit «laengen», Materialliste.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Ableitungen
{
    static double R(double x, int stellen = 2) => Math.Round(x, stellen, MidpointRounding.AwayFromZero);

    public static JsonObject Kopf(Projekt p, string art) => new()
    {
        ["ableitung"] = art,
        ["projekt"] = p.Kopf.Name,
        ["modellStand"] = new JsonObject { ["pruefsumme"] = p.Modell.Pruefsumme(), ["pakete"] = p.BekanntePakete.Count },
        ["erzeugt"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
        ["quelle"] = "Kern, aus dem Modell abgeleitet",
    };

    /// <summary>Modul 3 (B4): Materialliste je Leitungsserie und Grösse (Längen) und je Formteil-Familie und Typ (Stück).
    /// Einhängepunkt vom Hauptlauf; Abfrage «materialliste» in LeitungenAbfragen.</summary>
    public const string MateriallisteText = "Materialliste: Leitungslängen je Serie und Grösse (m) und Formteile je Familie, Typ und Winkel (Stück; Bogenwinkel, bei T-Stücken der Abzweigwinkel), wahlweise für ein System, einen Strang oder ein Geschoss.";

    /// <summary>Länge der Leitungen einer Serie und Grösse in m (2 Stellen). <see cref="Serie"/> = Name der Leitungsserie (Familie) oder
    /// «ohne Serie»; <see cref="Groesse"/> = Rohrgrösse nach <see cref="Rohrgroessen.Von"/> (Modul 3, B9b: «NW 20» bzw. «DN 20»; Abwasser «110» = Aussendurchmesser),
    /// bei Kanälen der Typname der Serie bzw. ohne Serie «b×h» oder «Ø d».
    /// Grundlage der Mengen, der Strangliste, der Materialliste und der Stränge in HKAP (Modul 3, B5).</summary>
    public sealed record Leitungslaenge(string Serie, string Groesse, double LaengeM);

    /// <summary>Leitungslängen je Serie und Grösse (Achslänge, Polylinie: Summe der Teilstücke), optional gefiltert auf einen Strang
    /// (Leitungen mit teilVon = Strang), ein System (eigenes System oder über Verbindungen wie bei den Mengen) und ein Geschoss.
    /// Die Filter sind Kennzeichen oder Ids; Unbekanntes oder Falsches wirft HlksFehler mit Vorschlag. Sortiert: Serie (ohne Serie
    /// zuletzt), dann Grösse nach Mass. Nur Leitungen (Klasse Rohr, Kanal und was von Leitung erbt) zählen.</summary>
    public static List<Leitungslaenge> Leitungslaengen(Projekt p, string? strangId = null, string? systemId = null, string? geschossId = null)
    {
        var f = Filter(p.Modell, systemId, strangId, geschossId);
        return Laengen(p, p.Modell.Elemente.Where(e => p.Katalog.IstArt(e, "Leitung") && f(e)));
    }

    static List<Leitungslaenge> Laengen(Projekt p, IEnumerable<Element> leitungen) =>
        leitungen.Select(l => (B: HlksMasse.Bezeichnung(p, l), Laenge: l.Geometrie?.Laenge() ?? 0))
            .GroupBy(x => (x.B.Serie, x.B.Groesse, x.B.Mass))
            .OrderBy(g => g.Key.Serie == HlksMasse.OhneSerie).ThenBy(g => g.Key.Serie, Natuerlich).ThenBy(g => g.Key.Mass).ThenBy(g => g.Key.Groesse, Natuerlich)
            .Select(g => new Leitungslaenge(g.Key.Serie, g.Key.Groesse, R(g.Sum(x => x.Laenge) / 1000)))
            .ToList();

    static JsonArray LaengenJson(IEnumerable<Leitungslaenge> l) =>
        new(l.Select(x => (JsonNode)new JsonObject { ["serie"] = x.Serie, ["groesse"] = x.Groesse, ["laenge_m"] = x.LaengeM }).ToArray());

    /// <summary>Filter system / strang / geschoss (Kennzeichen oder Id; null = alle).</summary>
    static Func<Element, bool> Filter(Modell m, string? system, string? strang, string? geschoss)
    {
        var sFilter = system is null ? null : m.HoleDerKlasse(system, "System").Id;
        var stFilter = strang is null ? null : m.HoleDerKlasse(strang, "Strang").Id;
        var gFilter = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        return e => (sFilter is null || e.System == sFilter || Netz.SystemeVon(m, e).Any(s => s.Id == sFilter))
                    && (stFilter is null || e.TeilVon == stFilter)
                    && (gFilter is null || e.Geschoss == gFilter);
    }

    /// <summary>Natürliche Ordnung: Zahlen im Text nach Wert («DN 20» vor «DN 100»), sonst ohne Rücksicht auf Gross/Klein.</summary>
    static readonly IComparer<string> Natuerlich = Comparer<string>.Create((a, b) =>
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int i0 = i, j0 = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var x = a[i0..i].TrimStart('0'); var y = b[j0..j].TrimStart('0');
                int c = x.Length != y.Length ? x.Length.CompareTo(y.Length) : string.CompareOrdinal(x, y);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++; j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    });

    /// <summary>Winkel eines Formteils für die Materialliste (Grad, auf 3 Stellen): beim Bogen sein Umlenkwinkel (mit Familie aus der
    /// Richtung von b, sonst Eigenschaft bzw. Exemplarwert «winkel»); beim T-Stück der Abzweigwinkel (Korrektur 30a: bei Silent-db20 sind
    /// 45° und 88.5° verschiedene Artikel), aber nur, wenn die Familie einen Winkelparameter (ziel oder Name «winkel», Modul 3 B7) hat –
    /// ein T-Stück ohne diesen Parameter hat nur den einen Abzweig und bleibt ohne Winkel. Andere Formteile: null.</summary>
    static double? Formteilwinkel(Projekt p, Element e)
    {
        var m = p.Modell; var k = p.Katalog;
        double? w = null;
        if (k.IstArt(e, "Bogen"))
            w = Leitungspruefungen.Bogenwinkel(m, k, e) ?? e.Zahl("winkel") ?? Json.Zahl(Familien.Exemplarwerte(e)?["winkel"]);
        else if (k.IstArt(e, "TStueck"))
        {
            if (Familien.FamilieVon(m, e) is { } fam && Familien.Definition(fam, k) is { Gueltig: true } def
                && def.Parameter.Any(x => string.Equals(x.Ziel, "winkel", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("winkel", StringComparison.OrdinalIgnoreCase)))
                w = Leitungspruefungen.Abzweigwinkel(m, k, e);
            w ??= e.Zahl("winkel") ?? Json.Zahl(Familien.Exemplarwerte(e)?["winkel"]);
        }
        return w is double x ? Math.Round(x, 3) : null;
    }

    /// <summary>Materialliste (Modul 3, B4): Leitungen je Serie und Grösse (Länge in m, Stück = Anzahl Geraden) und Formteile je
    /// Familie und Typ (Stück; Bögen zusätzlich je Winkel, T-Stücke mit Winkelparameter je Abzweigwinkel), gefiltert nach System, Strang und Geschoss (Kennzeichen). Formteile
    /// ohne Familie (Altbestand) stehen unter «ohne Familie» mit Klasse und DN als Typ. Deterministisch sortiert.</summary>
    public static JsonObject Materialliste(Projekt p, string? system = null, string? strang = null, string? geschoss = null)
    {
        var m = p.Modell; var k = p.Katalog;
        var passt = Filter(m, system, strang, geschoss);
        var leitungen = m.Elemente.Where(e => k.IstArt(e, "Leitung") && passt(e)).ToList();
        var laengen = Laengen(p, leitungen);
        var stueckJe = leitungen.GroupBy(l => { var b = HlksMasse.Bezeichnung(p, l); return (b.Serie, b.Groesse); }).ToDictionary(g => g.Key, g => g.Count());

        const string OhneFamilie = "ohne Familie";
        string Zahl(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var formteile = m.Elemente.Where(e => k.IstArt(e, "Formteil") && passt(e)).Select(e =>
        {
            var fam = Familien.FamilieVon(m, e);
            var typ = Familien.Typ(e);
            double? winkel = Formteilwinkel(p, e);
            if (fam is null || typ is null)
                return (Familie: OhneFamilie, Typ: HlksEinordnung.Dn(e) is double dn ? $"{e.Klasse} DN {Zahl(dn)}" : e.Klasse, Klasse: e.Klasse, Winkel: winkel);
            return (Familie: fam.Name ?? fam.Kennzeichen, Typ: typ, Klasse: e.Klasse, Winkel: winkel);
        }).ToList();
        var formteilZeilen = new JsonArray(formteile.GroupBy(f => f)
            .OrderBy(g => g.Key.Familie == OhneFamilie).ThenBy(g => g.Key.Familie, Natuerlich).ThenBy(g => g.Key.Klasse, Natuerlich)
            .ThenBy(g => g.Key.Typ, Natuerlich).ThenBy(g => g.Key.Winkel ?? -1)
            .Select(g => (JsonNode)new JsonObject
            {
                ["familie"] = g.Key.Familie, ["typ"] = g.Key.Typ, ["klasse"] = g.Key.Klasse,
                ["winkel"] = g.Key.Winkel is double w ? Math.Round(w, 3) : null, ["stueck"] = g.Count(),
            }).ToArray());

        var o = Kopf(p, "Materialliste");
        o["filter"] = new JsonObject { ["system"] = system, ["strang"] = strang, ["geschoss"] = geschoss };
        o["leitungen"] = new JsonArray(laengen.Select(x => (JsonNode)new JsonObject
        {
            ["serie"] = x.Serie, ["groesse"] = x.Groesse, ["stueck"] = stueckJe.GetValueOrDefault((x.Serie, x.Groesse)), ["laenge_m"] = x.LaengeM,
        }).ToArray());
        o["leitungslaengeTotal_m"] = R(leitungen.Sum(l => l.Geometrie?.Laenge() ?? 0) / 1000);
        o["formteile"] = formteilZeilen;
        o["formteileTotal"] = formteile.Count;
        var ohne = leitungen.Count(l => HlksMasse.Bezeichnung(p, l).Serie == HlksMasse.OhneSerie);
        var hinweise = new List<string>();
        if (ohne > 0) hinweise.Add($"{ohne} {(ohne == 1 ? "Leitung hat" : "Leitungen haben")} keine Serie (Prüfung P18): sie stehen unter «{HlksMasse.OhneSerie}» mit der Grösse aus den Eigenschaften.");
        hinweise.AddRange(Rohrgroessen.Hinweise(p, leitungen));
        if (hinweise.Count > 0) o["hinweis"] = string.Join(" ", hinweise);
        return o;
    }

    /// <summary>Bereich der Rohrgrössen für Listen: Abwasser nur die Zahl («110» bzw. «56–125»), übrige mit Art («NW 20» bzw. «NW 20–32»);
    /// verschiedene Arten getrennt durch «; » (Abwasser zuletzt). Null ohne Grössen.</summary>
    static string? GroessenBereich(IReadOnlyList<Rohrgroesse> groessen)
    {
        var teile = groessen.Where(g => g.Wert is not null).GroupBy(g => (g.Abwasser, g.Art)).OrderBy(g => g.Key.Abwasser).ThenBy(g => g.Key.Art)
            .Select(g =>
            {
                double min = g.Min(x => x.Wert!.Value), max = g.Max(x => x.Wert!.Value);
                int st = g.Key.Abwasser ? 1 : 2;
                var z = Rohrgroessen.Zahl(min, st) == Rohrgroessen.Zahl(max, st) ? Rohrgroessen.Zahl(min, st) : Rohrgroessen.Zahl(min, st) + "–" + Rohrgroessen.Zahl(max, st);
                return g.Key.Abwasser ? z : g.Key.Art + " " + z;
            }).ToList();
        return teile.Count == 0 ? null : string.Join("; ", teile);
    }

    static string Kz(Modell m, string? id) => m.Finde(id)?.Kennzeichen ?? "";

    static double GeschossKote(Modell m, string? geschossId) => m.Finde(geschossId)?.Zahl("kote") ?? double.MinValue;

    /// <summary>Gewerk eines Netzelements über den Systemtyp seines Systems, sonst aus dem Katalog.</summary>
    public static string? Gewerk(Modell m, Katalog k, Element e)
    {
        var sys = m.Finde(e.System) ?? Netz.SystemeVon(m, e).FirstOrDefault();
        if (sys?.Text("systemtyp") is { } typ && k.Systemtypen.TryGetValue(typ, out var st)) return st.Gewerk;
        return k.Klassen.TryGetValue(e.Klasse, out var d) ? d.Gewerk : null;
    }

    public static JsonObject Raumliste(Projekt p, string? geschoss = null)
    {
        var m = p.Modell; var k = p.Katalog;
        var gFilter = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        var raeume = m.DerKlasse("Raum").Where(r => gFilter is null || r.Geschoss == gFilter)
            .OrderBy(r => GeschossKote(m, r.Geschoss)).ThenBy(r => r.Text("nummer") ?? r.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var zeilen = new JsonArray();
        foreach (var r in raeume)
        {
            double flaeche = (r.Geometrie?.Flaeche() ?? 0) / 1e6;
            double hoehe = (r.Zahl("lichteHoehe") ?? r.Geometrie?.Hoehe ?? m.Finde(r.Geschoss)?.Zahl("hoehe") ?? 0) / 1000;
            var geraete = m.Elemente.Where(e => e.Raum == r.Id && Netz.IstEndgeraet(k, e)).OrderBy(e => e.Kennzeichen).ToList();
            var systeme = geraete.SelectMany(g => Netz.SystemeVon(m, g)).Select(s => s.Kennzeichen).Distinct().Order().ToList();
            zeilen.Add(new JsonObject
            {
                ["raum"] = r.Kennzeichen,
                ["nummer"] = r.Text("nummer"),
                ["name"] = r.Name,
                ["geschoss"] = Kz(m, r.Geschoss),
                ["nutzung"] = r.Text("nutzung"),
                ["flaeche_m2"] = R(flaeche),
                ["hoehe_m"] = R(hoehe),
                ["volumen_m3"] = R(flaeche * hoehe),
                ["anzahlEndgeraete"] = geraete.Count,
                ["endgeraete"] = new JsonArray(geraete.Select(g => (JsonNode)JsonValue.Create($"{g.Kennzeichen} ({g.Klasse})")).ToArray()),
                ["systeme"] = new JsonArray(systeme.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
            });
        }
        var summen = new JsonArray(raeume.GroupBy(r => Kz(m, r.Geschoss)).Select(g => (JsonNode)new JsonObject
        {
            ["geschoss"] = g.Key,
            ["raeume"] = g.Count(),
            ["flaeche_m2"] = R(g.Sum(r => (r.Geometrie?.Flaeche() ?? 0) / 1e6)),
        }).ToArray());
        var o = Kopf(p, "Raumliste");
        o["anzahl"] = raeume.Count;
        o["flaecheTotal_m2"] = R(raeume.Sum(r => (r.Geometrie?.Flaeche() ?? 0) / 1e6));
        o["summenJeGeschoss"] = summen;
        o["zeilen"] = zeilen;
        return o;
    }

    public static JsonObject Strangliste(Projekt p, string? system = null)
    {
        var m = p.Modell; var k = p.Katalog;
        var sFilter = system is null ? null : m.HoleDerKlasse(system, "System").Id;
        var straenge = m.DerKlasse("Strang").Where(s => sFilter is null || s.TeilVon == sFilter)
            .OrderBy(s => Kz(m, s.TeilVon)).ThenBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var zeilen = new JsonArray();
        var alleLeitungen = new List<Element>();
        foreach (var s in straenge)
        {
            var sys = m.Finde(s.TeilVon);
            var eigene = m.Elemente.Where(e => e.TeilVon == s.Id).ToList();
            var leitungen = eigene.Where(e => k.IstArt(e, "Leitung")).ToList();
            alleLeitungen.AddRange(leitungen);
            var (direkt, gesamt) = Netz.Verbraucher(m, k, s);
            var geschosse = eigene.Concat(direkt).Select(e => e.Geschoss).Where(g => g is not null).Distinct()
                .Select(g => m.Finde(g)!).OrderByDescending(g => g.Zahl("kote") ?? 0).Select(g => g.Kennzeichen).ToList();
            var groessen = leitungen.Where(l => !Leitungsbezeichnung.IstLuft(p, l)).Select(l => Rohrgroessen.Von(p, null, l)).ToList();
            var datenpunkte = m.DerKlasse("Datenpunkt").Where(d => d.Bezuege.TryGetValue("an", out var an) && (an == s.Id || eigene.Any(e => e.Id == an)))
                .Select(d => d.Kennzeichen).Order().ToList();
            zeilen.Add(new JsonObject
            {
                ["strang"] = s.Kennzeichen,
                ["name"] = s.Name,
                ["art"] = s.Text("art"),
                ["system"] = sys?.Kennzeichen,
                ["systemtyp"] = sys?.Text("systemtyp"),
                ["medium"] = sys?.Text("medium"),
                ["geschosse"] = new JsonArray(geschosse.Select(g => (JsonNode)JsonValue.Create(g)).ToArray()),
                ["bauteile"] = eigene.Count,
                ["leitungslaenge_m"] = R(leitungen.Sum(l => l.Geometrie?.Laenge() ?? 0) / 1000),
                ["laengen"] = LaengenJson(Laengen(p, leitungen)),   // Modul 3 (B4): je Serie und Grösse
                ["groesse"] = GroessenBereich(groessen),   // B9b: Abwasser Aussendurchmesser («56–125»), übrige NW/DN («NW 20–32»)
                ["endgeraeteDirekt"] = new JsonArray(direkt.Select(e => (JsonNode)JsonValue.Create($"{e.Kennzeichen} ({e.Klasse}, {Kz(m, e.Raum)})")).ToArray()),
                ["anzahlDirekt"] = direkt.Count,
                ["anzahlDirektJeKlasse"] = new JsonObject(direkt.GroupBy(e => e.Klasse).OrderBy(g => g.Key).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
                ["anzahlGesamt"] = gesamt.Count,
                ["datenpunkte"] = new JsonArray(datenpunkte.Select(d => (JsonNode)JsonValue.Create(d)).ToArray()),
            });
        }
        var o = Kopf(p, "Strangliste");
        o["anzahl"] = straenge.Count;
        if (Rohrgroessen.Hinweise(p, alleLeitungen) is { Count: > 0 } hw) o["hinweis"] = string.Join(" ", hw);
        o["zeilen"] = zeilen;
        return o;
    }

    public static JsonObject Mengen(Projekt p, string? system = null, string? geschoss = null)
    {
        var m = p.Modell; var k = p.Katalog;
        var sFilter = system is null ? null : m.HoleDerKlasse(system, "System").Id;
        var gFilter = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        bool Passt(Element e) => (sFilter is null || e.System == sFilter || Netz.SystemeVon(m, e).Any(s => s.Id == sFilter))
                                 && (gFilter is null || e.Geschoss == gFilter);
        var leitungen = m.Elemente.Where(e => k.IstArt(e, "Leitung") && Passt(e)).ToList();
        var gruppen = new JsonArray(leitungen.Select(l => (L: l, B: HlksMasse.Bezeichnung(p, l)))
            .GroupBy(x => (System: Kz(m, x.L.System), x.L.Klasse, Material: x.L.Text("material") ?? "–", Groesse: x.B.Groesse, Mass: x.B.Mass))
            .OrderBy(g => g.Key.System).ThenBy(g => g.Key.Klasse).ThenBy(g => g.Key.Material).ThenBy(g => g.Key.Mass).ThenBy(g => g.Key.Groesse, Natuerlich)
            .Select(g => (JsonNode)new JsonObject
            {
                ["system"] = g.Key.System,
                ["klasse"] = g.Key.Klasse,
                ["material"] = g.Key.Material,
                ["groesse"] = g.Key.Groesse,   // B9b: Abwasser Aussendurchmesser («110»), übrige NW/DN («NW 20»)
                ["stueck"] = g.Count(),
                ["laenge_m"] = R(g.Sum(x => x.L.Geometrie?.Laenge() ?? 0) / 1000),
            }).ToArray());
        var stueck = new JsonArray(m.Elemente.Where(e => !k.IstArt(e, "Leitung") && k.Klassen.TryGetValue(e.Klasse, out var d) && d.Schicht == "system"
                                                          && e.Klasse is not ("Anlage" or "System" or "Strang") && Passt(e))
            .GroupBy(e => (e.Klasse, Gewerk: Gewerk(m, k, e))).OrderBy(g => g.Key.Klasse).ThenBy(g => g.Key.Gewerk)
            .Select(g => (JsonNode)new JsonObject
            {
                ["klasse"] = g.Key.Klasse, ["gewerk"] = g.Key.Gewerk, ["stueck"] = g.Count(),
                ["bkp"] = k.BkpFuer(g.First(), g.Key.Gewerk), ["bkpHinweis"] = "BKP-Zuordnung vorläufig, zu prüfen",
            }).ToArray());
        var o = Kopf(p, "Mengen");
        o["filter"] = new JsonObject { ["system"] = system, ["geschoss"] = geschoss };
        o["leitungen"] = gruppen;
        o["laengen"] = LaengenJson(Laengen(p, leitungen));   // Modul 3 (B4): Längen je Serie und Grösse
        if (Rohrgroessen.Hinweise(p, leitungen) is { Count: > 0 } hw) o["hinweis"] = string.Join(" ", hw);
        o["leitungslaengeTotal_m"] = R(leitungen.Sum(l => l.Geometrie?.Laenge() ?? 0) / 1000);
        o["bauteile"] = stueck;
        return o;
    }
}
