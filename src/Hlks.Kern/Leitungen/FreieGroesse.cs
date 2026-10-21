// Leitungen (Nachtrag Modul 3, entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026): freie Grösse einer Kanalserie an der
// Leitung. Eine Leitung mit freier Grösse trägt bezuege.familie = Serie, familientyp = Vorlage (leitung.frei.vorlage) und die
// tatsächliche Grösse in breite/hoehe (Kanal rechteckig) bzw. durchmesser (Kanal rund) – Herkunft familie, Quelle «<Familie> Typ
// <Vorlage>, frei <b × h>». Ausgewertet wird sie aus der Vorlage mit ersetzten Grössenparametern (FamilienAuswertung.Aufloesen mit
// typwerte): Nachführung (FamilienNachfuehrung), Masse für Grafik, Mengen und IFC (HlksMasse), Routing (Leitungsserie.GroesseVon).
//
// Regel (Von): familientyp = Vorlage einer Serie mit leitung.frei und die Grösse steht in den Eigenschaften; ausgenommen
//   - Werte, die aus dem Typ selbst stammen (Herkunft familie, Quelle «<Familie> Typ <Vorlage>») – sie folgen dem Typ, wenn dieser
//     sich ändert;
//   - eine Grösse, die einem Typ der Serie entspricht (auch der Vorlage): der Rastertyp hat Vorrang – so wird beim Umstellen von
//     familientyp auf die Vorlage nicht still die alte Rastergrösse zur freien Grösse.
// ANNAHMEN (Prüfliste): ein Wert von Hand (element_aendern breite) an einer Leitung mit familientyp = Vorlage gilt als freie Grösse
//   (P29 prüft Grenzen und Schritt; P14 meldet nicht passende Formteile); die Formteil-Grösse eines Formteils mit freier Grösse ist die
//   Folge seiner Anschlussgrössen («430 × 270/250 × 200»), wenn seine Exemplarwerte einen Grössenparameter eines Anschlusses setzen.
//
// Formteile freier Serien tragen ihre Grösse selbst (Nachtrag 21.10.2026, Auftrag «Nachträge zu Leitungsserien» Punkt 2; Befund: ein Formteil in
// der Vorgabegrösse der Familie, z. B. Bogen 400 × 200 bei Vorlage 400 × 200, speicherte keine Exemplar-Grössen und änderte sich mit der Vorgabe):
//   - neue Formteile: die Formteilwahl setzt die Grössen immer (Leitungsserie.Formteil);
//   - bestehende Formteile, beim nächsten Ändern im selben Paket (FormteilGroessenNachfuehren, vor FamilienNachfuehren): ändert ein Befehl die
//     Definition der Familie, bekommt ein Formteil ohne eigene Grösse die Grösse, die es VORHER hatte (alte Definition) – es bleibt gleich;
//     ändert ein Befehl das Formteil selbst, wird die Grösse festgehalten, die es danach hat (Typ und Exemplarwerte des Befehls gelten). Nie ohne
//     Paket: beim Laden wird nichts umgeschrieben;
//   - Prüfung P45 (hinweis) meldet Formteile ohne eigene Grösse mit Vorschlag (element_aendern mit allen Werten; bei Bögen auch leitung_groesse).
//   «Formteil einer freien Serie» = seine Familie steht in leitung.formteile einer Serie mit leitung.frei im Projekt; «Grössenparameter» =
//   Exemplarparameter ohne Formel, die direkt die Grösse (d, b, h) eines Anschlusses sind (wie FreiExemplar).

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FreieGroesse
{
    /// <summary>Ersetzte Typwerte einer Grösse: Grössenparameter der Serie (leitung.groesse) → Wert.</summary>
    public static Dictionary<string, double> Typwerte(FamLeitung l, Leitungsgroesse g)
    {
        var d = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (schl, pn) in l.Groesse)
            if (g.Wert(schl) is double x) d[pn] = x;
        return d;
    }

    /// <summary>Eigenschaften der Leitung, die die Grösse tragen: rund durchmesser, rechteckig breite und hoehe.</summary>
    public static string[] Eigenschaften(FamLeitung l) => l.Groesse.ContainsKey("d") ? ["durchmesser"] : ["breite", "hoehe"];

    /// <summary>Grösse aus den Eigenschaften einer Kanal-Leitung (Kanal rund durchmesser, rechteckig breite × hoehe) oder null.</summary>
    public static Leitungsgroesse? AusEigenschaften(FamLeitung l, Element e)
    {
        if (l.Groesse.ContainsKey("d")) return e.Zahl("durchmesser") is double d && d > 0 && double.IsFinite(d) ? Leitungsgroesse.Rund(d) : null;
        if (l.Groesse.ContainsKey("b"))
            return e.Zahl("breite") is double b && b > 0 && double.IsFinite(b) && e.Zahl("hoehe") is double h && h > 0 && double.IsFinite(h) ? Leitungsgroesse.Rechteck(b, h) : null;
        return null;
    }

    /// <summary>Quelle der Grösse aus einem Typ («<Familie> Typ <typ>», wie die Nachführung von Modul 3).</summary>
    public static string RasterQuelle(string familie, string typ) => $"{familie} Typ {typ}";

    /// <summary>Quelle einer freien Grösse: «<Familie> Typ <Vorlage>, frei 430 × 270».</summary>
    public static string FreiQuelle(string familie, string vorlage, Leitungsgroesse g) => $"{familie} Typ {vorlage}, frei {g.Kurz}";

    static readonly ConditionalWeakTable<FamilieDef, List<Leitungsgroesse>> raster = new();

    /// <summary>Grössen der Typen einer Serie (auswertbare Typen), zwischengespeichert je Definitionsstand.</summary>
    public static IReadOnlyList<Leitungsgroesse> Raster(FamilieDef def) => raster.GetValue(def, d => d.Typen
        .Select(t => FamilienAuswertung.Aufloesen(d, t.Name, null)).Where(a => a.Gueltig && a.Leitung is not null)
        .Select(a => Leitungsserie.AusSchluesseln(a.Leitung!.Groesse)).ToList());

    /// <summary>Die freie Grösse einer Leitung (siehe Dateikopf) oder null (keine freie Serie, anderer Typ, Grösse fehlt, Werte aus dem
    /// Typ, Rastergrösse). familie = Name der Serie für die Quelle (Familie.Name bzw. Name der Definition).</summary>
    public static Leitungsgroesse? Von(FamilieDef def, Element leitung, string familie)
    {
        if (def.Leitung is not { Frei: { } fr } l || Familien.Typ(leitung) != fr.Vorlage) return null;
        if (AusEigenschaften(l, leitung) is not { } g) return null;
        var rq = RasterQuelle(familie, fr.Vorlage);
        foreach (var n in Eigenschaften(l))
            if (leitung.Eigenschaft(n) is { Herkunft: { Art: "familie" } h } && h.Quelle == rq) return null;
        if (Raster(def).Any(r => r.Gleich(g))) return null;
        return g;
    }

    /// <summary>Ersetzte Typwerte einer Leitung mit freier Grösse oder null.</summary>
    public static Dictionary<string, double>? TypwerteVon(FamilieDef def, Element leitung, string familie) =>
        Von(def, leitung, familie) is { } g ? Typwerte(def.Leitung!, g) : null;

    /// <summary>Grösse eines Formteils mit freier Grösse für Listen und IFC: die Anschlussgrössen in der Reihenfolge der Familie
    /// («430 × 270», «430 × 270/250 × 200»), wenn seine Exemplarwerte einen Grössenparameter (d, b, h) eines Anschlusses setzen;
    /// sonst null (Typname gilt).</summary>
    public static string? FormteilText(FamilieDef def, JsonObject? exemplar, FamAufloesung a)
    {
        if (exemplar is null || exemplar.Count == 0 || !a.Gueltig) return null;
        bool setzt = def.Anschluesse.Any(an => new[] { an.D, an.B, an.H }.Any(z => z?.Text?.Trim() is { } t && exemplar.ContainsKey(t)
            && def.NachName.TryGetValue(t, out var p) && p.Art == "exemplar"));
        if (!setzt) return null;
        var gr = a.Anschluesse.Where(x => x.Sichtbar).Select(Leitungsgroesse.Von).Where(g => !g.Leer).Select(g => g.Kurz).Distinct().ToList();
        return gr.Count == 0 ? null : string.Join("/", gr);
    }

    /// <summary>Grösse eines Elements mit freier Grösse für Listen und das IFC-Typobjekt: Leitung «430 × 270», Formteil «430 × 270/250 × 200»;
    /// null = keine freie Grösse (dann gilt der Typname).</summary>
    public static string? Text(Projekt p, Element e)
    {
        var m = p.Modell; var k = p.Katalog;
        if (k.IstArt(e, "Leitung")) return HlksMasse.Serie(p, e) is { Frei: true } s ? s.Typ : null;
        if (Familien.FamilieVon(m, e) is not { } f || Familien.Definition(f, k) is not { Gueltig: true } def || !k.IstArt(e, def.Kategorie)) return null;
        var ex = Familien.Exemplarwerte(e);
        if (ex is null || ex.Count == 0) return null;
        return FormteilText(def, ex, FamilienAuswertung.Aufloesen(def, Familien.Typ(e), ex));
    }

    // ------------------------------------------------------------------ Formteile freier Serien tragen ihre Grösse selbst (Nachtrag 21.10.2026)

    /// <summary>Grössenparameter einer Formteil-Familie: Exemplarparameter ohne Formel, die direkt die Grösse (d, b, h) eines Anschlusses sind –
    /// in der Reihenfolge der Anschlüsse, ohne Doppel.</summary>
    public static List<string> FormteilGroessenparameter(FamilieDef def)
    {
        var l = new List<string>();
        foreach (var an in def.Anschluesse)
            foreach (var z in new[] { an.D, an.B, an.H })
                if (z?.Konstante is null && z?.Text?.Trim() is { } pn && def.NachName.TryGetValue(pn, out var p) && p.Art == "exemplar" && !p.HatFormel && !l.Contains(pn))
                    l.Add(pn);
        return l;
    }

    /// <summary>Definitions-ids der Formteil-Familien, die eine freie Serie (leitung.frei) des Modells in leitung.formteile nennt, je mit der
    /// ersten solchen Serie (für Meldungen).</summary>
    public static Dictionary<string, Element> FormteilFamilienFreierSerien(Modell m, Katalog k)
    {
        var d = new Dictionary<string, Element>(StringComparer.Ordinal);
        foreach (var f in m.DerKlasse(Familien.Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            if (Familien.Definition(f, k) is { Gueltig: true, Leitung: { Frei: not null } l })
                foreach (var w in l.Formteile.Values) d.TryAdd(w.Familie, f);
        return d;
    }

    /// <summary>Grössenparameter, die ein Formteil nicht selbst trägt, mit dem Wert aus der Auswertung (def, typ, Exemplarwerte) – leer, wenn es
    /// alle trägt bzw. die Auswertung nicht gelingt.</summary>
    public static Dictionary<string, double> FehlendeGroessen(FamilieDef def, string? typ, JsonObject? exemplar)
    {
        var fehlend = new Dictionary<string, double>(StringComparer.Ordinal);
        var namen = FormteilGroessenparameter(def).Where(n => exemplar?[n] is null).ToList();
        if (namen.Count == 0 || typ is null || def.Typ(typ) is null) return fehlend;
        var a = FamilienAuswertung.Aufloesen(def, typ, exemplar);
        if (!a.Gueltig) return fehlend;
        foreach (var n in namen)
            if (a.Wert(n) is { IstText: false } w && double.IsFinite(w.Zahl)) fehlend[n] = w.Zahl;
        return fehlend;
    }

    /// <summary>Exemplarwerte mit den ergänzten Grössen (Reihenfolge: bisherige, dann die Grössen).</summary>
    public static JsonObject MitGroessen(JsonObject? exemplar, IReadOnlyDictionary<string, double> groessen)
    {
        var ex = exemplar?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (n, w) in groessen) ex[n] = w;
        return ex;
    }

    static string Zahl(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>P45 (hinweis, Nachtrag 21.10.2026): Formteil einer freien Serie, das seine Grösse nicht selbst trägt (Grössenparameter fehlen in
    /// seinen Exemplarwerten – die Grösse kommt aus Typ bzw. Vorgabe der Familie und änderte sich mit ihr). Vorschlag: element_aendern mit allen
    /// Werten (ändert Lage und Form nicht); bei einem Bogen zwischen zwei Leitungen auch leitung_groesse (wählt den Bogen neu).</summary>
    public static void PruefenFormteile(Modell m, Katalog k, List<Befund> b)
    {
        var frei = FormteilFamilienFreierSerien(m, k);
        if (frei.Count == 0) return;
        foreach (var e in m.Elemente.Where(e => e.Klasse != Familien.Klasse && !k.IstArt(e, "Leitung")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (Familien.FamilieVon(m, e) is not { } f || Familien.DefinitionsId(f) is not { } id || !frei.TryGetValue(id, out var serie)) continue;
            if (Familien.Definition(f, k) is not { Gueltig: true } def || !k.IstArt(e, def.Kategorie)) continue;
            var ex = Familien.Exemplarwerte(e);
            var fehlend = FehlendeGroessen(def, Familien.Typ(e), ex);
            if (fehlend.Count == 0) continue;
            var werte = string.Join(", ", fehlend.Select(x => $"{x.Key} = {Zahl(x.Value)}"));
            var parameter = MitGroessen(ex, fehlend).ToJsonString();
            var vorschlag = $"Grösse festhalten (ändert Lage und Form nicht): element_aendern {{ \"element\": \"{e.Kennzeichen}\", \"eigenschaften\": {{ \"parameter\": {parameter} }} }}";
            if (k.IstArt(e, "Bogen"))
            {
                var leitungen = e.Anschluesse.Select(a => m.VerbindungAn(e.Id, a.Name)?.Gegenueber(e.Id, a.Name)).Where(g => g is not null)
                    .Select(g => m.Finde(g!.Element)).Where(x => x is not null && k.IstArt(x, "Leitung")).Select(x => x!).ToList();
                if (leitungen.Count > 0 && Anschlusslagen.LeitungGroesse(m, k, leitungen[0]) is { Leer: false } lg)
                    vorschlag += $" – oder den Bogen neu wählen lassen: leitung_groesse {{ \"leitungen\": [{string.Join(", ", leitungen.Select(x => $"\"{x.Kennzeichen}\""))}], \"groesse\": \"{lg.Kurz}\" }}";
            }
            b.Add(new("P45", "hinweis", e.Kennzeichen,
                $"{e.Klasse} der freien Serie {serie.Kennzeichen} «{serie.Name}» trägt seine Grösse nicht selbst ({werte} aus Typ «{Familien.Typ(e)}» bzw. der Vorgabe von {f.Kennzeichen}) – ändert sich die Familie, ändert sich das Formteil mit",
                vorschlag));
        }
    }
}

/// <summary>Nachführung «Formteile freier Serien tragen ihre Grösse selbst» (Nachtrag 21.10.2026, siehe FreieGroesse.cs) – läuft vor
/// FamilienNachfuehren im selben Paket: ändert ein Befehl die Definition einer Formteil-Familie einer freien Serie, bekommen ihre Formteile ohne
/// eigene Grösse die Grösse, die sie vorher hatten (alte Definition); ändert er ein solches Formteil selbst, wird die Grösse danach festgehalten.</summary>
public sealed class FormteilGroessenNachfuehren : INachfuehrung
{
    public string Name => "formteilgroessen";

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        if (!geaendert.Any(id => m.Finde(id) is { } e && (e.Klasse == Familien.Klasse || e.Bezuege.ContainsKey(Familien.Bezug)))) return;
        var frei = FreieGroesse.FormteilFamilienFreierSerien(m, kat);
        if (frei.Count == 0) return;
        Element? Vorher(string id) => k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;
        // Familien, deren Definition der Befehl geändert hat (Element vor dem Befehl, null = neu)
        var familien = new Dictionary<string, Element?>(StringComparer.Ordinal);
        foreach (var id in geaendert)
            if (m.Finde(id) is { Klasse: Familien.Klasse } f && Familien.DefinitionsId(f) is { } did && frei.ContainsKey(did))
                familien[f.Id] = Vorher(id);
        var festgehalten = new List<string>();
        var kandidaten = familien.Count > 0 ? m.Elemente.ToList() : geaendert.Select(m.Finde).Where(e => e is not null).Select(e => e!).ToList();
        foreach (var e in kandidaten.Where(e => e.Klasse != Familien.Klasse && !kat.IstArt(e, "Leitung")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (!e.Bezuege.TryGetValue(Familien.Bezug, out var fid)) continue;
            bool selbst = geaendert.Contains(e.Id);
            if (!selbst && !familien.ContainsKey(fid)) continue;
            if (m.Finde(fid) is not { Klasse: Familien.Klasse } f || Familien.DefinitionsId(f) is not { } did || !frei.ContainsKey(did)) continue;
            var def = Familien.Definition(f, kat);
            if (!def.Gueltig || !kat.IstArt(e, def.Kategorie)) continue;   // meldet FamilienNachfuehren
            var typ = Familien.Typ(e) ?? (def.Typen.Count == 1 ? def.Typen[0].Name : null);
            var ex = Familien.Exemplarwerte(e);
            // nur die Familie geändert: die Grösse von vorher (alte Definition); das Formteil selbst geändert: die Grösse danach
            var quelle = def;
            if (!selbst && familien.GetValueOrDefault(fid) is { } alt && Familien.Definition(alt, kat) is { Gueltig: true } defAlt) quelle = defAlt;
            var fehlend = FreieGroesse.FehlendeGroessen(quelle, typ, ex)
                .Where(x => def.NachName.TryGetValue(x.Key, out var p) && p.Art == "exemplar" && !p.HatFormel).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            if (fehlend.Count == 0) continue;
            var neu = e.Kopie();
            neu.Eigenschaften["parameter"] = new Wert { Inhalt = FreieGroesse.MitGroessen(ex, fehlend), Herkunft = k.Herkunft };
            k.Setzen(neu);
            festgehalten.Add($"{e.Kennzeichen} ({string.Join(", ", fehlend.Select(x => $"{x.Key} = {x.Value.ToString("0.###", CultureInfo.InvariantCulture)}"))})");
        }
        if (festgehalten.Count > 0)
            k.Hinweise.Add($"Formteile freier Serien tragen ihre Grösse selbst – festgehalten: {string.Join("; ", festgehalten.Take(10))}{(festgehalten.Count > 10 ? $" … ({festgehalten.Count})" : "")}.");
    }
}
