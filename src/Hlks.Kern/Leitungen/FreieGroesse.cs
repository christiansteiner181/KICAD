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
}
