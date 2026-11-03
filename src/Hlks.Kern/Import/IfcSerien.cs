// Standardkomponenten aus dem IFC mit unseren Serien nachzeichnen (Christian 03.11.2026: «ist ein ablaufrohr gezeichnet, so wird dies beim
// import mit unserer silent familie nachgezeichnet»; «Serien-Zuordnung»). Beim HLKS-Import:
//   1. Leitungen: Regel IFC-Typ (Name bzw. ObjectType, regulärer Ausdruck, wahlweise nur für bestimmte Medien) → Serie der Bürobibliothek; die
//      Serie samt ihren Formteil-Familien (und mischbaren Serien) wird ins Projekt geladen, die Leitung bekommt Serie und Typ nach ihrem
//      Aussendurchmesser (Rohr) bzw. Durchmesser oder Breite × Höhe (Kanal). Regeln: Projektinformation.ifcSerien zuerst, dann die Vorgabe.
//   2. Formteile (Bogen, T-Stück, Reduktion), deren Anschlüsse alle an Leitungen derselben Serie hängen: das Formteil der Serie mit Lage im
//      Schnittpunkt der Leitungsachsen und Ausrichtung aus deren Richtungen – der Verlauf bleibt wie im IFC. Ein Knick, den die Serie nicht hat,
//      nimmt den nächsten erlaubten Winkel (Abwasser-Serien bis 5°, sonst 2°; Revit zeichnet 90°, Geberit Silent hat 88.5° – Christian: «die bogen sind 88.5° zum
//      kompensieren des gefälles von der vertikalen in die Horizontale»), mit Hinweis.
// ANNAHMEN (Vorgabe-Regeln, an Bernstrasse 42 H+K-Revit-Typen geprüft; wählbar): Mapress ohne «Edelstahl» im Namen: Trinkwasser (KW, WW, ZIR)
//   Edelstahl, sonst C-Stahl; «Stahl» = Stahlrohr geschweisst; Rund-Kanäle = Wickelfalzrohr rund. Entscheide Christian 03.11.2026: Flexkanäle =
//   Depping Aludec 112 («flexkanäle von depping ag»), Uponor-Metallverbundrohr = Uponor Uni Pipe PLUS FBH («uponor FBH Rohre in Standard»;
//   ohne Formteile, ab Rolle, Biegeradius 100 mm).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

static class IfcSerien
{
    public sealed record Regel(string Muster, string? Serie, string[]? Medien = null);

    public static readonly Regel[] Vorgabe =
    [
        new("Silent-db20|Silent db20", "Geberit Silent-db20"),
        new(@"Geberit[ _]PE|PE[ _-]Rohr|_PE\b", "Geberit PE"),
        new(@"Mapress.*Edelstahl|1\.4401", "Geberit Mapress Edelstahl"),
        new("Mapress", "Geberit Mapress Edelstahl", ["KW", "WW", "ZIR"]),
        new("Mapress", "Geberit Mapress C-Stahl"),
        new("Sani[Pp]ex", "JRG Sanipex MT"),
        new("Flexkanal", "Flexkanal Depping Aludec 112"),
        new("Uponor", "Uponor Uni Pipe PLUS FBH"),
        new("Stahl", "Stahlrohr geschweisst"),
        new("^Rechteck", "Rechteckkanal"),
        new("^Rund", "Wickelfalzrohr rund"),
    ];

    /// <summary>Regeln: Projektinformation.ifcSerien [{ muster, serie, medien }] (serie leer = keine Serie), danach die Vorgabe.</summary>
    public static List<Regel> Regeln(Modell m)
    {
        var aus = new List<Regel>();
        if (m.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcSerien")?.Inhalt is JsonArray a)
            foreach (var r in a.OfType<JsonObject>())
                if (Wert(r["muster"]) is { Length: > 0 } mu)
                    aus.Add(new Regel(mu, Wert(r["serie"]) is { Length: > 0 } s ? s : null,
                        r["medien"] is JsonArray me ? me.Select(x => Wert(x)).OfType<string>().ToArray() : null));
        aus.AddRange(Vorgabe);
        return aus;
    }

    public static IEnumerable<string> Zuordnen(IfcLauf lauf, IEnumerable<(IIfcProduct X, Element E)> leitungen, IEnumerable<(IIfcProduct X, Element E)> formteile)
    {
        var k = lauf.Kontext;
        var kat = lauf.Katalog;
        var regeln = Regeln(k.Arbeit);
        var ort = FamilienBibliothekDienst.Ort(kat);
        var hinweise = new List<string>();
        var ohne = new Dictionary<string, int>();
        var fehler = new Dictionary<string, int>();
        var serien = new Dictionary<string, Leitungsserie?>(StringComparer.OrdinalIgnoreCase);
        var vonLeitung = new Dictionary<string, (Leitungsserie S, Leitungsgroesse G)>();
        int zugeordnet = 0;

        foreach (var (x, e) in leitungen)
        {
            var typname = Typname(x) ?? e.Name ?? "";
            var medium = e.Anschluesse.Select(a => a.Medium).FirstOrDefault(md => md is { Length: > 0 } && md != "*") ?? Netz.ElementMedium(k.Arbeit, e);
            var regel = regeln.FirstOrDefault(r => Regex.IsMatch(typname, r.Muster, RegexOptions.IgnoreCase) && (r.Medien is null || medium is not null && r.Medien.Contains(medium, StringComparer.OrdinalIgnoreCase)));
            if (regel?.Serie is not { } name) { Zaehlen(ohne, typname); continue; }
            if (!serien.TryGetValue(name, out var s)) serien[name] = s = Laden(k, kat, ort, name, fehler);
            if (s is null) continue;
            var g = Groesse(s, e);
            if (g is null) { Zaehlen(fehler, $"{name}: Grösse {Kurz(e)} nicht in der Serie"); continue; }
            e.Bezuege[Familien.Bezug] = s.Familie.Id;
            lauf.Wert(e, "familientyp", g.Typ, $"Serie {s.Familie.Name} nach IFC-Typ «{typname}» (Regel «{regel.Muster}»)");
            vonLeitung[e.Id] = (s, g.Groesse);
            zugeordnet++;
        }

        // Formteile zwischen Leitungen derselben Serie
        var anschluss = new Dictionary<(string, string), (string Id, string Name)>();
        foreach (var v in lauf.Verbindungen) { anschluss[(v.VonRef, v.VonAnschluss)] = (v.NachRef, v.NachAnschluss); anschluss[(v.NachRef, v.NachAnschluss)] = (v.VonRef, v.VonAnschluss); }
        int formteileOk = 0, winkelAngepasst = 0;
        foreach (var (_, e) in formteile)
        {
            string? art = kat.IstArt(e, "Bogen") ? "bogen" : kat.IstArt(e, "TStueck") ? "tstueck" : kat.IstArt(e, "Reduktion") ? "reduktion" : null;
            if (art is null) continue;
            var enden = new Dictionary<string, (V3 Punkt, V3 Raus, Leitungsserie S, Leitungsgroesse G)>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in e.Anschluesse)
            {
                if (!anschluss.TryGetValue((e.Id, a.Name), out var gg) || !vonLeitung.TryGetValue(gg.Id, out var sl) || !lauf.Geplant.TryGetValue(gg.Id, out var l)) break;
                var p = l.Geometrie?.Punkte;
                if (p is not { Count: >= 2 }) break;
                bool amAnfang = gg.Name.Equals("anfang", StringComparison.OrdinalIgnoreCase);
                var ende = V3.Aus(amAnfang ? p[0] : p[^1]); var weiter = V3.Aus(amAnfang ? p[1] : p[^2]);
                enden[a.Name] = (ende, (weiter - ende).Einheit(), sl.S, sl.G);
            }
            if (enden.Count != e.Anschluesse.Count || enden.Values.Select(v => v.S.Familie.Id).Distinct().Count() != 1) continue;
            var s = enden.Values.First().S;
            try
            {
                var (wahl, ursprung, x, z, angepasst) = Formteil(k.Arbeit, kat, s, art, enden);
                if (wahl is null) { Zaehlen(fehler, $"{Leitungsserie.Artname(art)} in {s.Familie.Name}: kein passendes Formteil"); continue; }
                e.Bezuege[Familien.Bezug] = wahl.Familie!.Id;
                lauf.Wert(e, "familientyp", wahl.Typ, $"Formteil der Serie {s.Familie.Name}");
                if (wahl.Exemplar is { Count: > 0 } ex) lauf.Wert(e, "parameter", ex.DeepClone(), $"Formteil der Serie {s.Familie.Name}");
                lauf.Wert(e, "ausrichtung", Familien.AusrichtungJson(x, z), "Richtungen der angeschlossenen Leitungen");
                e.Geometrie = new Geometrie { Art = "punkt", Punkte = [[Math.Round(ursprung.X, 1), Math.Round(ursprung.Y, 1), Math.Round(ursprung.Z, 1)]] };
                formteileOk++;
                if (angepasst) winkelAngepasst++;
            }
            catch (HlksFehler f) { Zaehlen(fehler, f.Message.Length > 80 ? f.Message[..80] + "…" : f.Message); }
        }

        if (zugeordnet > 0) hinweise.Add($"Serien aus dem Standard: {zugeordnet} Leitungen und {formteileOk} Formteile nachgezeichnet ({string.Join(", ", serien.Where(s => s.Value is not null).Select(s => s.Key))})"
            + (winkelAngepasst > 0 ? $"; {winkelAngepasst} Bögen auf den nächsten Winkel der Serie (Spielraum Abwasser 5°, sonst 2°; z. B. 90° → 88.5° für das Gefälle)" : "") + ".");
        if (ohne.Count > 0) hinweise.Add("Ohne Serie (keine Regel, Projektinformation.ifcSerien): " + string.Join(", ", ohne.OrderByDescending(o => o.Value).Take(8).Select(o => $"«{o.Key}» {o.Value}×")) + ".");
        foreach (var (grund, n) in fehler) hinweise.Add($"Serien aus dem Standard: {n}× {grund}.");
        return hinweise;
    }

    /// <summary>Serie (Name oder Kennung) aus dem Projekt bzw. der Bürobibliothek, samt ihren Formteil-Familien und mischbaren Serien.</summary>
    static Leitungsserie? Laden(Kontext k, Katalog kat, BibliotheksOrt ort, string name, Dictionary<string, int> fehler)
    {
        Element? Finden(string bezug) => k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => Familien.DefinitionsId(f) == bezug || string.Equals(f.Name?.Trim(), bezug, StringComparison.OrdinalIgnoreCase));
        Element? Holen(string bezug)
        {
            if (Finden(bezug) is { } da) return da;
            if (!ort.Aktiv) { Zaehlen(fehler, $"«{bezug}»: Familienbibliothek nicht erreichbar"); return null; }
            try
            {
                int n = k.Hinweise.Count;
                FamilieLaden.AusBibliothek(k, ort, bezug, null, null);
                if (k.Hinweise.Count > n) k.Hinweise.RemoveRange(n, k.Hinweise.Count - n);
            }
            catch (HlksFehler f) { Zaehlen(fehler, $"«{bezug}» nicht geladen: {(f.Message.Length > 60 ? f.Message[..60] + "…" : f.Message)}"); return null; }
            return Finden(bezug);
        }
        if (Holen(name) is not { } fam) return null;
        if (fam.Eigenschaft("definition")?.Inhalt is JsonObject def && def["leitung"] is JsonObject l)
        {
            if (l["formteile"] is JsonObject ft) foreach (var (_, f) in ft) if (Wert(f?["familie"]) is { } id) Holen(id);
            if (l["regeln"]?["mischbar"] is JsonArray mi) foreach (var m in mi) if (Wert(m) is { } id) Holen(id);
        }
        try { return Leitungsserie.Von(k.Arbeit, kat, Finden(name)!); }
        catch (HlksFehler f) { Zaehlen(fehler, $"«{name}» ist keine Leitungsserie: {f.Message}"); return null; }
    }

    /// <summary>Typ der Serie für die Grösse der Leitung: Rohr über den Aussendurchmesser (±1 mm), sonst DN; Kanal rund d, eckig b × h
    /// (auch als freie Grösse innerhalb der Grenzen der Serie).</summary>
    static SerienGroesse? Groesse(Leitungsserie s, Element e)
    {
        if (e.Zahl("da") is double da)
            return s.Groessen.Where(g => g.Gueltig && g.Aussen is double a && Math.Abs(a - da) <= 1).MinBy(g => Math.Abs(g.Aussen!.Value - da));
        Leitungsgroesse? lg = e.Zahl("durchmesser") is double d ? Leitungsgroesse.Rund(d)
            : e.Zahl("breite") is double b && e.Zahl("hoehe") is double h ? Leitungsgroesse.Rechteck(b, h)
            : e.Zahl("dn") is double dn ? Leitungsgroesse.Rohr(dn) : null;
        if (lg is not { } g1 || !s.PasstArt(g1)) return null;
        if (s.TypFuer(g1) is { } t) return t;
        return s.Frei is not null && s.Grenzverletzung(g1) is null && s.ImSchritt(g1) ? s.Virtuell(g1) : null;
    }

    static (FormteilWahl? Wahl, V3 Ursprung, V3 X, V3 Z, bool Angepasst) Formteil(Modell m, Katalog kat, Leitungsserie s, string art,
        Dictionary<string, (V3 Punkt, V3 Raus, Leitungsserie S, Leitungsgroesse G)> enden)
    {
        var a = enden["a"]; var b = enden["b"];
        var durch = a.Raus * -1;
        if (art == "reduktion")
        {
            var w = s.Formteil(m, kat, art, a.G, b.G);
            if (!w.Ok || w.Vertauscht) return (null, a.Punkt, default, default, false);
            var (rx, rz) = Formteilgeometrie.Ausrichtung(durch);
            return (w, a.Punkt, rx, rz, false);
        }
        var seite = art == "tstueck" ? enden["c"] : b;
        var ursprung = Schnitt(a.Punkt, a.Raus, seite.Punkt, seite.Raus);
        double winkel = Math.Acos(Math.Clamp(durch.Mal(seite.Raus), -1, 1)) * 180 / Math.PI;
        var kandidaten = new List<(double W, bool Angepasst)> { (winkel, false) };
        // Spielraum (Christian 03.11.2026: «dies ist nicht exakt, und kann nachgearbeitet werden. es braucht also einen gewissen spielraum bei PE und
        // Silent»): Abwasser-Serien bis 5°, die übrigen bis 2°
        double spiel = s.Abwasser ? 5 : 2;
        var erlaubt = art == "tstueck" ? s.Block.Abzweigwinkel ?? [90] : s.Block.Winkel;
        if (erlaubt is not null)
            kandidaten.AddRange(erlaubt.Where(x => Math.Abs(x - winkel) <= spiel && Math.Abs(x - winkel) > 1e-6).OrderBy(x => Math.Abs(x - winkel)).Select(x => (x, true)));
        foreach (var (wi, angepasst) in kandidaten)
        {
            var w = art == "tstueck" ? s.Formteil(m, kat, art, a.G, seite.G, wi) : s.Formteil(m, kat, art, a.G, null, wi);
            if (!w.Ok) continue;
            var (x, z) = Formteilgeometrie.Ausrichtung(durch, seite.Raus);
            return (w, ursprung, x, z, angepasst);
        }
        return (null, ursprung, default, default, false);
    }

    /// <summary>Mitte der kürzesten Verbindung zweier Geraden p + t·u und q + v·w (parallel: Mitte der Punkte).</summary>
    static V3 Schnitt(V3 p, V3 u, V3 q, V3 w)
    {
        var r = p - q;
        double a = u.Mal(u), b = u.Mal(w), c = w.Mal(w), d = u.Mal(r), e = w.Mal(r), n = a * c - b * b;
        if (Math.Abs(n) < 1e-9) return (p + q) * 0.5;
        double t = (b * e - c * d) / n, v = (a * e - b * d) / n;
        return (p + u * t + q + w * v) * 0.5;
    }

    static string Kurz(Element e) => e.Zahl("da") is double da ? $"da {da:0.#}" : e.Zahl("durchmesser") is double d ? $"d {d:0.#}"
        : e.Zahl("breite") is double b ? $"{b:0} × {e.Zahl("hoehe"):0}" : "?";

    static string? Wert(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    static void Zaehlen(Dictionary<string, int> d, string grund) => d[grund] = d.GetValueOrDefault(grund) + 1;

    static string? Typname(IIfcProduct x)
    {
        if (x is IIfcObject o)
        {
            if (o.IsTypedBy.FirstOrDefault()?.RelatingType?.Name is { } tn && IfcLauf.Text(tn) is { Length: > 0 } t) return t;
            if (IfcLauf.Text(o.ObjectType) is { Length: > 0 } ot) return ot;
        }
        return IfcLauf.Text(x.Name);
    }
}
