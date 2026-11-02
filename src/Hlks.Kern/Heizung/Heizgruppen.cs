// Modul 5 Teil A-D (entwurf/modul-5.md, Punkte 1–3): Heizgruppe, Systempaar Vor-/Rücklauf und die Ableitung der Glieder aus dem Netz von Modul 3.
//
// Heizgruppe (Klasse Heizgruppe, Kürzel HG): Gruppierung wie der Strang, kein Netzelement. teilVon = Anlage; bezuege.vorlauf/ruecklauf = Systempaar;
// bezuege.verteiler + abgangVl/abgangRl = Abgang (Zentrale bzw. Element und Anschlüsse), an dem die Gruppe beginnt. Gespeichert werden nur diese
// Angaben und die Eingaben (art, verbraucherart, vlTemp, rlTemp); alles andere ist abgeleitet und wird nie gespeichert.
//
// Ableitung (deterministisch, Reihenfolge der Anschlüsse am Element):
//   Vorlaufweg  ab dem Anschluss abgangVl durch das Netz im Medium dieses Anschlusses (Breitensuche ungerichtet, das Startelement wird nie wieder
//               betreten): durch Leitungsbauteile (Leitung, Formteil, Armatur) und Verteiler (Unterverteiler, z. B. FBH-Verteiler je Geschoss);
//               Verbraucher beenden den Weg (sie zählen), ebenso andere Zentralen (Speicher, Erzeuger, Wärmetauscher …) und Verteiler, an deren
//               Abgang eine andere Heizgruppe beginnt (Grenzen). Ungerichtet, weil die Fliessrichtung in fremden IFC (Revit-Pumpenfamilien)
//               verkehrt sein kann – das Medium trennt Vor- und Rücklauf; ein Mischer-Bypass (Anschluss b im Rücklaufmedium) ist damit eine erlaubte
//               Masche, die die Ableitung nicht abbricht.
//   Rücklaufweg ebenso ab abgangRl im Rücklaufmedium (ohne abgangRl: das Gegenstück des Vorlaufanschlusses, vl_2 → rl_2, vl_aus → rl_ein).
//   Glieder     Armaturen (Ventil, Mischventil, Pumpe, Fühler, Zähler) auf diesen Wegen, Vorlauf in Fliessrichtung (Tiefe ab dem Abgang),
//               Rücklauf von den Verbrauchern zum Abgang (Tiefe absteigend), je mit Weg (vl | rl).
//   Stamm/Äste  Vom Abgang bis zur ersten Verzweigung mit mehr als einem Ast, der zu Verbrauchern bzw. Unterverteilern führt, ist der Stamm (dort
//               liegen Mischer, Pumpe, Fühler der Gruppe); danach je Ast die Unterverteiler und Verbraucher (z. B. Strang A und B der Bernstrasse).
//               Abzweige zu Fühlern, Entleerungen o. Ä. sind keine Äste.
//
// Vorschlag (heizgruppen_vorschlagen) für Altbestand und IFC: je Gruppenpumpe (Pumpe im Heiz- bzw. Kältemedium, die nicht schon in einer Gruppe liegt)
// die Seite mit Verbrauchern (nur Verbraucher, keine andere Zentrale, keine andere Pumpe – Verteiler werden dabei durchlaufen) und die Speiseseite;
// Abgang = die erste Zentrale bzw. Verzweigung auf der Speiseseite. Danach je Abgang eines Verteilers, der in keiner Gruppe liegt und Verbraucher
// erreicht, eine Gruppe ohne Pumpe (Befund P34). art (gemischt mit Mischventil, sonst ungemischt) und verbraucherart werden aus Gliedern und
// Verbrauchern abgeleitet, Name «<Verbraucherart> <art>» (z. B. «FBH gemischt»). Begründung «je Gruppenpumpe» statt nur «je Verteiler-Abgang»:
// in der Bernstrasse ist der Speicher die Verteilung und die FBH-Verteiler je Geschoss sind Unterverteiler (sonst 45 Gruppen statt einer).
//
// Zwischengespeichert je Modellstand (Modell.Aenderungszaehler) wie SanitaerBelastung.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ein Glied einer Heizgruppe (Armatur auf dem Vor- bzw. Rücklaufweg).</summary>
/// <param name="Weg">vl | rl</param>
/// <param name="Reihe">Laufende Nummer in Fliessreihenfolge (Vorlauf zuerst, dann Rücklauf), ab 1.</param>
/// <param name="Tiefe">Schritte ab dem Abgang.</param>
/// <param name="Ast">null = Stamm (vor der ersten Verzweigung), sonst Nummer des Asts.</param>
/// <param name="Eintritt">Anschluss, über den der Weg das Glied erreicht.</param>
public sealed record HeizGlied(Element Element, string Weg, int Reihe, int Tiefe, int? Ast, string? Eintritt);

/// <summary>Ein Ast nach der ersten Verzweigung der Gruppe (z. B. ein Steigstrang mit seinen Unterverteilern).</summary>
public sealed class HeizAst
{
    public required int Nummer { get; init; }
    /// <summary>Erstes Element des Asts nach dem Abzweig.</summary>
    public required Element Anfang { get; init; }
    public List<Element> Unterverteiler { get; } = [];
    public List<Element> Verbraucher { get; } = [];
    public List<Element> Glieder { get; } = [];
}

/// <summary>Knoten einer Breitensuche durch das Netz einer Gruppe.</summary>
/// <param name="Typ">leitung | verbraucher | unterverteiler | grenze | stopp | sonst</param>
public sealed record HeizKnoten(Element Element, HeizKnoten? Eltern, string? Eintritt, int Tiefe, string Typ);

/// <summary>Ergebnis einer Breitensuche (Baum) ab einem Anschluss.</summary>
public sealed class HeizBaum
{
    public List<HeizKnoten> Reihe { get; } = [];
    public Dictionary<string, HeizKnoten> Knoten { get; } = new(StringComparer.Ordinal);
    /// <summary>Treffpunkte von Maschen (Element-Id), die ein zweites Mal erreicht wurden.</summary>
    public HashSet<string> Maschen { get; } = new(StringComparer.Ordinal);
    public IEnumerable<HeizKnoten> Kinder(HeizKnoten k) => Reihe.Where(x => x.Eltern == k);
}

/// <summary>Abgeleitete Sicht einer Heizgruppe (nie gespeichert).</summary>
public sealed class HeizgruppenWeg
{
    public required Element Gruppe { get; init; }
    public Element? Abgang { get; init; }
    public string? AbgangVl { get; init; }
    public string? AbgangRl { get; set; }
    /// <summary>Woher abgangRl kommt: eigenschaft | gegenstueck (aus dem Namen von abgangVl) | null.</summary>
    public string? AbgangRlWoher { get; set; }
    public string? MediumVl { get; set; }
    public string? MediumRl { get; set; }
    public Element? Vorlauf { get; init; }
    public Element? Ruecklauf { get; init; }
    /// <summary>Vorlauf- und Rücklaufsystem sind gegenseitig Gegenstück.</summary>
    public bool Systempaar { get; init; }
    public HeizBaum? VlBaum { get; set; }
    public HeizBaum? RlBaum { get; set; }
    public List<HeizGlied> Glieder { get; } = [];
    public List<Element> Verbraucher { get; } = [];
    public List<Element> Unterverteiler { get; } = [];
    /// <summary>Verbraucher (Id) → nächster Unterverteiler auf seinem Weg (Vorlauf, sonst Rücklauf).</summary>
    public Dictionary<string, Element> UnterverteilerJeVerbraucher { get; } = new(StringComparer.Ordinal);
    public List<Element> Grenzen { get; } = [];
    /// <summary>Kreise eines Unterverteilers: die Verbraucher, deren nächster Unterverteiler er ist.</summary>
    public List<Element> KreiseVon(Element unterverteiler) => Verbraucher.Where(v => UnterverteilerJeVerbraucher.TryGetValue(v.Id, out var u) && u.Id == unterverteiler.Id).ToList();
    public Element? Abzweig { get; set; }
    public List<HeizAst> Aeste { get; } = [];
    /// <summary>Alle Elemente der Wege (Vorlauf und Rücklauf, ohne den Abgang).</summary>
    public HashSet<string> Elemente { get; } = new(StringComparer.Ordinal);
    public List<string> Hinweise { get; } = [];

    public IEnumerable<Element> Pumpen => Glieder.Where(g => g.Element.Klasse == "Pumpe").Select(g => g.Element);
    public IEnumerable<Element> Mischer => Glieder.Where(g => g.Element.Klasse == "Mischventil" || (g.Element.Klasse == "Ventil" && g.Element.Text("art") == "mischventil")).Select(g => g.Element);
    /// <summary>Kurztext des Abgangs, z. B. «VT-1.vl_2/rl_2».</summary>
    public string? AbgangText => Abgang is null ? null : $"{Abgang.Kennzeichen}.{AbgangVl ?? "?"}{(AbgangRl is null ? "" : "/" + AbgangRl)}";
}

/// <summary>Vorschlag einer Heizgruppe (heizgruppen_vorschlagen).</summary>
public sealed record HeizgruppenVorschlag(Element Abgang, string AbgangVl, string? AbgangRl, Element? Pumpe, Element? Vorlauf, Element? Ruecklauf,
    string? Art, string? Verbraucherart, string Name, HeizgruppenWeg Weg);

/// <summary>Ergebnis der Ableitung aller Heizgruppen eines Modellstands (zwischengespeichert).</summary>
public sealed class HeizungStand
{
    public required Modell Modell { get; init; }
    public required Katalog Katalog { get; init; }
    public Dictionary<string, HeizgruppenWeg> Gruppen { get; } = new(StringComparer.Ordinal);
    /// <summary>Element-Id → Heizgruppen (Ids), in deren Wegen es liegt.</summary>
    public Dictionary<string, List<string>> GruppenJeElement { get; } = new(StringComparer.Ordinal);
    public IEnumerable<HeizgruppenWeg> In(string elementId) =>
        GruppenJeElement.TryGetValue(elementId, out var l) ? l.Select(id => Gruppen[id]) : [];
}

public static class Heizgruppen
{
    // ------------------------------------------------------------------ Medien und Systempaar

    /// <summary>Medium mit Paar (Heizung, Kälte, auch Glykol): Rolle vorlauf | ruecklauf, sonst null.</summary>
    public static string? Rolle(Katalog k, string? medium) => k.PaarRolle(medium);

    public static bool IstHeizmedium(Katalog k, string? medium) => Rolle(k, medium) is not null;

    /// <summary>Hat das Element einen Anschluss in einem Heiz- bzw. Kältemedium (Medium mit Gegenstück)?</summary>
    public static bool HatHeizmedium(Modell m, Katalog k, Element e) => e.Anschluesse.Any(a => IstHeizmedium(k, Netz.Medium(m, e, a)));

    /// <summary>Gegenstück eines Systems (bezuege.gegenstueck) oder null.</summary>
    public static Element? Gegenstueck(Modell m, Element system) =>
        system.Bezuege.TryGetValue(Bezug.Gegenstueck, out var id) && m.Finde(id) is { Klasse: "System" } g ? g : null;

    /// <summary>Zwei Systeme sind gegenseitig Gegenstück.</summary>
    public static bool IstPaar(Modell m, Element? vl, Element? rl) =>
        vl is not null && rl is not null && Gegenstueck(m, vl)?.Id == rl.Id && Gegenstueck(m, rl)?.Id == vl.Id;

    /// <summary>Namen von Bezügen der Heizung.</summary>
    public static class Bezug
    {
        public const string Gegenstueck = "gegenstueck";
        public const string Vorlauf = "vorlauf";
        public const string Ruecklauf = "ruecklauf";
        public const string Verteiler = "verteiler";
        public const string Strangschema = "strangschema";
        /// <summary>Fühler ohne Verbindung (Anlege- bzw. Tauchfühler ohne Formteil): Leitung, an der er sitzt.</summary>
        public const string An = "an";
    }

    static readonly Regex VlWort = new(@"(?<![a-z])vl(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex VorlaufWort = new(@"vorlauf", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Name des Gegenstücks eines Vorlauf-Namens: «H VL NT» → «H RL NT», «Heizung Vorlauf» → «Heizung Rücklauf»; null, wenn der Name kein
    /// Vorlauf-Kennwort enthält (eindeutig: genau ein Vorkommen).</summary>
    public static string? RuecklaufName(string? vorlaufName)
    {
        if (string.IsNullOrWhiteSpace(vorlaufName)) return null;
        var n = vorlaufName.Trim();
        int vl = VlWort.Matches(n).Count, vor = VorlaufWort.Matches(n).Count;
        if (vl + vor != 1) return null;
        return vl == 1 ? VlWort.Replace(n, m => m.Value == "VL" ? "RL" : m.Value == "vl" ? "rl" : "Rl")
                       : VorlaufWort.Replace(n, m => char.IsUpper(m.Value[0]) ? "Rücklauf" : "rücklauf");
    }

    static string Normiert(string s) => Regex.Replace(s.ToLowerInvariant().Replace("ü", "ue"), @"[\s_\-]+", " ").Trim();

    /// <summary>Passen die Namen eines Vorlauf- und eines Rücklaufsystems zusammen («H VL NT» ↔ «H RL NT», Gross/klein und Trenner egal)?</summary>
    public static bool NamenPassen(string? vorlauf, string? ruecklauf) =>
        RuecklaufName(vorlauf) is { } r && ruecklauf is not null && Normiert(r) == Normiert(ruecklauf);

    /// <summary>Anschluss-Gegenstück am selben Element: vl_2 → rl_2, vl → rl, vl_aus → rl_ein, vl_ein → rl_aus, p_vl → p_rl, s_vl → s_rl (und umgekehrt).
    /// null, wenn der Name kein vl/rl enthält.</summary>
    public static string? PartnerAnschluss(string name)
    {
        var n = name.ToLowerInvariant();
        string? t = null;
        if (Regex.IsMatch(n, @"(^|_)vl(_|$)")) t = Regex.Replace(n, @"(^|_)vl(_|$)", "$1rl$2");
        else if (Regex.IsMatch(n, @"(^|_)rl(_|$)")) t = Regex.Replace(n, @"(^|_)rl(_|$)", "$1vl$2");
        if (t is null) return null;
        if (t.EndsWith("_aus")) t = t[..^4] + "_ein";
        else if (t.EndsWith("_ein")) t = t[..^4] + "_aus";
        return t;
    }

    // ------------------------------------------------------------------ Breitensuche

    static bool MediumPasst(Modell m, Element e, Anschluss? a, string medium)
    {
        if (a is null) return false;
        var med = Netz.Medium(m, e, a);
        return med == "*" || med.Equals(medium, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Breitensuche ungerichtet im Medium ab einem Anschluss eines Elements. Das Startelement wird nie wieder betreten. Leitungsbauteile werden
    /// durchlaufen (ausser stopp), Verteiler als Unterverteiler (ausser grenze), Verbraucher, andere Zentralen, Quellen und Senken beenden den Weg.</summary>
    public static HeizBaum Folgen(Modell m, Katalog k, Element start, string anschluss, string medium, Func<Element, bool>? grenze = null, Func<Element, bool>? stopp = null)
    {
        var baum = new HeizBaum();
        var an = start.AnschlussNamens(anschluss);
        if (an is null || !MediumPasst(m, start, an, medium)) return baum;
        var v = m.VerbindungAn(start.Id, an.Name);
        var g = v?.Gegenueber(start.Id, an.Name);
        var erster = m.Finde(g?.Element);
        var ea = erster?.AnschlussNamens(g!.Anschluss);
        if (erster is null || ea is null || erster.Id == start.Id || !MediumPasst(m, erster, ea, medium)) return baum;
        var schlange = new Queue<HeizKnoten>();
        void Dazu(Element e, HeizKnoten? eltern, string eintritt, int tiefe)
        {
            var typ = Typ(k, e, grenze, stopp);
            var kn = new HeizKnoten(e, eltern, eintritt, tiefe, typ);
            baum.Knoten[e.Id] = kn;
            baum.Reihe.Add(kn);
            if (typ is "leitung" or "unterverteiler") schlange.Enqueue(kn);
        }
        Dazu(erster, null, ea.Name, 1);
        while (schlange.Count > 0)
        {
            var kn = schlange.Dequeue();
            var e = kn.Element;
            foreach (var a in e.Anschluesse)
            {
                if (a.Name.Equals(kn.Eintritt, StringComparison.OrdinalIgnoreCase)) continue;
                if (!MediumPasst(m, e, a, medium)) continue;
                var vb = m.VerbindungAn(e.Id, a.Name);
                var gg = vb?.Gegenueber(e.Id, a.Name);
                var n = m.Finde(gg?.Element);
                var na = n?.AnschlussNamens(gg!.Anschluss);
                if (n is null || na is null || n.Id == start.Id || n.Id == e.Id) continue;
                if (!MediumPasst(m, n, na, medium)) continue;
                if (baum.Knoten.ContainsKey(n.Id))
                {
                    if (kn.Eltern?.Element.Id != n.Id) baum.Maschen.Add(n.Id);   // zweiter Weg zum selben Element
                    continue;
                }
                Dazu(n, kn, na.Name, kn.Tiefe + 1);
            }
        }
        return baum;
    }

    static string Typ(Katalog k, Element e, Func<Element, bool>? grenze, Func<Element, bool>? stopp)
    {
        var rolle = Netz.Rolle(k, e);
        if (rolle == "verbraucher") return "verbraucher";
        if (rolle == "zentrale") return k.IstArt(e, "Verteiler") && !(grenze?.Invoke(e) ?? false) ? "unterverteiler" : "grenze";
        if (rolle is "quelle" or "senke") return "grenze";
        if (Netz.IstLeitungsbauteil(k, e)) return stopp?.Invoke(e) == true ? "stopp" : "leitung";
        return "sonst";
    }

    // ------------------------------------------------------------------ Ableitung je Heizgruppe

    sealed class Gespeichert(long zaehler, Katalog katalog, HeizungStand stand)
    {
        public long Zaehler { get; } = zaehler;
        public Katalog Katalog { get; } = katalog;
        public HeizungStand Stand { get; } = stand;
    }

    static readonly ConditionalWeakTable<Modell, Gespeichert> zwischen = new();

    /// <summary>Ableitung aller Heizgruppen des Modellstands (zwischengespeichert, solange sich das Modell nicht ändert).</summary>
    public static HeizungStand Ergebnis(Modell m, Katalog k)
    {
        var z = m.Aenderungszaehler;
        if (zwischen.TryGetValue(m, out var g) && g.Zaehler == z && ReferenceEquals(g.Katalog, k)) return g.Stand;
        var s = new HeizungStand { Modell = m, Katalog = k };
        var gruppen = m.DerKlasse("Heizgruppe").OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var abgaenge = Abgaenge(gruppen);
        foreach (var hg in gruppen)
        {
            var w = Ableiten(m, k, hg, abgaenge);
            s.Gruppen[hg.Id] = w;
            foreach (var id in w.Elemente.Concat(w.Verbraucher.Select(x => x.Id)).Distinct())
            {
                if (!s.GruppenJeElement.TryGetValue(id, out var l)) s.GruppenJeElement[id] = l = [];
                l.Add(hg.Id);
            }
        }
        zwischen.AddOrUpdate(m, new Gespeichert(z, k, s));
        return s;
    }

    /// <summary>Abgänge der Heizgruppen: Element-Id → Heizgruppen-Ids (für die Grenze «an diesem Verteiler beginnt eine andere Gruppe»).</summary>
    static Dictionary<string, List<string>> Abgaenge(IEnumerable<Element> gruppen)
    {
        var d = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var hg in gruppen)
            if (hg.Bezuege.TryGetValue(Bezug.Verteiler, out var v))
            {
                if (!d.TryGetValue(v, out var l)) d[v] = l = [];
                l.Add(hg.Id);
            }
        return d;
    }

    /// <summary>Leitet Wege, Glieder, Verbraucher und Äste einer Heizgruppe ab.</summary>
    public static HeizgruppenWeg Ableiten(Modell m, Katalog k, Element hg, IReadOnlyDictionary<string, List<string>>? abgaenge = null)
    {
        abgaenge ??= Abgaenge(m.DerKlasse("Heizgruppe"));
        var vl = hg.Bezuege.TryGetValue(Bezug.Vorlauf, out var vid) ? m.Finde(vid) : null;
        var rl = hg.Bezuege.TryGetValue(Bezug.Ruecklauf, out var rid) ? m.Finde(rid) : null;
        var abgang = hg.Bezuege.TryGetValue(Bezug.Verteiler, out var aid) ? m.Finde(aid) : null;
        var w = new HeizgruppenWeg
        {
            Gruppe = hg, Abgang = abgang, AbgangVl = hg.Text("abgangVl"), AbgangRl = hg.Text("abgangRl"),
            Vorlauf = vl, Ruecklauf = rl, Systempaar = IstPaar(m, vl, rl),
        };
        if (w.AbgangRl is not null) w.AbgangRlWoher = "eigenschaft";
        // Grenze: ein Verteiler, an dessen Abgang eine andere Heizgruppe beginnt
        bool Grenze(Element e) => abgaenge.TryGetValue(e.Id, out var l) && l.Any(id => id != hg.Id);
        WegeAbleiten(m, k, w, Grenze, null);
        return w;
    }

    /// <summary>Wege ab dem Abgang (Vorlauf, Rücklauf) und daraus Glieder, Verbraucher, Unterverteiler und Äste.</summary>
    static void WegeAbleiten(Modell m, Katalog k, HeizgruppenWeg w, Func<Element, bool> grenze, Func<Element, bool>? stopp)
    {
        if (w.Abgang is null || w.AbgangVl is null)
        {
            w.Hinweise.Add("Ohne Abgang (bezuege.verteiler mit abgangVl) lassen sich Glieder und Verbraucher nicht ableiten.");
            return;
        }
        var anVl = w.Abgang.AnschlussNamens(w.AbgangVl);
        if (anVl is null) { w.Hinweise.Add($"{w.Abgang.Kennzeichen} hat keinen Anschluss «{w.AbgangVl}»."); return; }
        w.MediumVl = Netz.Medium(m, w.Abgang, anVl) is var mv && mv != "*" ? mv : w.Vorlauf?.Text("medium");
        if (w.MediumVl is null) { w.Hinweise.Add($"Medium am Abgang {w.Abgang.Kennzeichen}.{w.AbgangVl} unbekannt (kein System)."); return; }
        w.VlBaum = Folgen(m, k, w.Abgang, anVl.Name, w.MediumVl, grenze, stopp);
        if (w.VlBaum.Reihe.Count == 0) w.Hinweise.Add($"{w.Abgang.Kennzeichen}.{w.AbgangVl} ist nicht verbunden.");

        // Rücklauf: abgangRl, sonst das Gegenstück des Vorlaufanschlusses
        if (w.AbgangRl is null && PartnerAnschluss(anVl.Name) is { } p && w.Abgang.AnschlussNamens(p) is { } pa)
        {
            w.AbgangRl = pa.Name;
            w.AbgangRlWoher = "gegenstueck";
        }
        w.MediumRl = k.GegenstueckMedium(w.MediumVl);
        if (w.AbgangRl is not null && w.Abgang.AnschlussNamens(w.AbgangRl) is { } anRl)
        {
            var mr = Netz.Medium(m, w.Abgang, anRl);
            if (mr != "*") w.MediumRl = mr;
            if (w.MediumRl is not null) w.RlBaum = Folgen(m, k, w.Abgang, anRl.Name, w.MediumRl, grenze, stopp);
        }
        else if (w.AbgangRl is not null) w.Hinweise.Add($"{w.Abgang.Kennzeichen} hat keinen Anschluss «{w.AbgangRl}».");
        else w.Hinweise.Add($"Rücklauf-Abgang unbekannt (abgangRl fehlt, {w.Abgang.Kennzeichen}.{anVl.Name} hat kein Gegenstück nach dem Namen).");

        // Stamm und Äste im Vorlauf
        var vlb = w.VlBaum;
        var kinder = vlb.Reihe.Where(x => x.Eltern is not null).GroupBy(x => x.Eltern!.Element.Id).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var relevant = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool Relevant(HeizKnoten kn)
        {
            if (relevant.TryGetValue(kn.Element.Id, out var r)) return r;
            r = kn.Typ is "verbraucher" or "unterverteiler"
                || (kinder.TryGetValue(kn.Element.Id, out var l) && l.Any(Relevant));
            return relevant[kn.Element.Id] = r;
        }
        var astVon = new Dictionary<string, int>(StringComparer.Ordinal);
        if (vlb.Reihe.Count > 0)
        {
            var cur = vlb.Reihe[0];
            for (int schritt = 0; schritt < 100000; schritt++)
            {
                if (cur.Typ is "verbraucher" or "unterverteiler") break;
                var rel = (kinder.GetValueOrDefault(cur.Element.Id) ?? []).Where(Relevant).ToList();
                if (rel.Count == 1) { cur = rel[0]; continue; }
                if (rel.Count >= 2)
                {
                    w.Abzweig = cur.Element;
                    int n = 0;
                    foreach (var kind in rel)
                    {
                        var ast = new HeizAst { Nummer = ++n, Anfang = kind.Element };
                        w.Aeste.Add(ast);
                        var stapel = new Stack<HeizKnoten>([kind]);
                        while (stapel.Count > 0)
                        {
                            var x = stapel.Pop();
                            astVon[x.Element.Id] = ast.Nummer;
                            foreach (var y in kinder.GetValueOrDefault(x.Element.Id) ?? []) stapel.Push(y);
                        }
                    }
                }
                break;
            }
        }

        // Anlegefühler: Fühler ohne Verbindung mit bezuege.an auf eine Leitung (Tauchhülse ohne Formteil, Anlegefühler) zählen dort als Glied
        var anlege = m.DerKlasse("Fuehler").Where(f => f.Bezuege.ContainsKey(Bezug.An)).ToLookup(f => f.Bezuege[Bezug.An], StringComparer.Ordinal);
        static Element? UvVorfahr(HeizKnoten kn)
        {
            for (var y = kn.Eltern; y is not null; y = y.Eltern) if (y.Typ == "unterverteiler") return y.Element;
            return null;
        }

        // Vorlauf: Verbraucher, Unterverteiler, Grenzen, Glieder
        var vlGlieder = new List<(HeizGlied Glied, int Index)>();
        int index = 0;
        foreach (var kn in vlb.Reihe)
        {
            index++;
            w.Elemente.Add(kn.Element.Id);
            int? ast = astVon.TryGetValue(kn.Element.Id, out var an) ? an : null;
            switch (kn.Typ)
            {
                case "verbraucher":
                    w.Verbraucher.Add(kn.Element);
                    if (UvVorfahr(kn) is { } uv) w.UnterverteilerJeVerbraucher[kn.Element.Id] = uv;
                    break;
                case "unterverteiler": w.Unterverteiler.Add(kn.Element); break;
                case "grenze": w.Grenzen.Add(kn.Element); w.Elemente.Remove(kn.Element.Id); break;
                default:
                    if (k.IstArt(kn.Element, "Armatur")) vlGlieder.Add((new HeizGlied(kn.Element, "vl", 0, kn.Tiefe, ast, kn.Eintritt), index));
                    break;
            }
            foreach (var f in anlege[kn.Element.Id].Where(f => !vlb.Knoten.ContainsKey(f.Id)))
            {
                vlGlieder.Add((new HeizGlied(f, "vl", 0, kn.Tiefe, ast, Bezug.An), index));
                w.Elemente.Add(f.Id);
            }
        }

        // Rücklauf: Verbraucher nur im Rücklauf (Vorlauf nicht angeschlossen) zählen auch; ihr Unterverteiler aus dem Rücklaufbaum
        var rlGlieder = new List<(HeizGlied Glied, int Index)>();
        if (w.RlBaum is { } rlb)
        {
            index = 0;
            foreach (var kn in rlb.Reihe)
            {
                index++;
                switch (kn.Typ)
                {
                    case "verbraucher":
                        w.Elemente.Add(kn.Element.Id);
                        if (!w.Verbraucher.Contains(kn.Element)) w.Verbraucher.Add(kn.Element);
                        if (!w.UnterverteilerJeVerbraucher.ContainsKey(kn.Element.Id) && UvVorfahr(kn) is { } uv) w.UnterverteilerJeVerbraucher[kn.Element.Id] = uv;
                        break;
                    case "unterverteiler":
                        w.Elemente.Add(kn.Element.Id);
                        if (!w.Unterverteiler.Contains(kn.Element)) w.Unterverteiler.Add(kn.Element);
                        break;
                    case "grenze":
                        if (!w.Grenzen.Contains(kn.Element)) w.Grenzen.Add(kn.Element);
                        break;
                    default:
                        w.Elemente.Add(kn.Element.Id);
                        // ein Mischventil liegt mit b auch im Rücklauf (Bypass): es ist Glied des Vorlaufs, nicht doppelt
                        if (k.IstArt(kn.Element, "Armatur") && !vlGlieder.Any(x => x.Glied.Element.Id == kn.Element.Id))
                            rlGlieder.Add((new HeizGlied(kn.Element, "rl", 0, kn.Tiefe, null, kn.Eintritt), index));
                        break;
                }
                foreach (var f in anlege[kn.Element.Id].Where(f => !rlb.Knoten.ContainsKey(f.Id) && !w.Elemente.Contains(f.Id)))
                {
                    rlGlieder.Add((new HeizGlied(f, "rl", 0, kn.Tiefe, null, Bezug.An), index));
                    w.Elemente.Add(f.Id);
                }
            }
        }

        // Äste: Verbraucher und Unterverteiler über den Vorlauf, sonst über ihren Unterverteiler; Rücklauf-Glieder über ihre Nachfahren
        int? AstVon(Element e) => astVon.TryGetValue(e.Id, out var a) ? a
            : w.UnterverteilerJeVerbraucher.TryGetValue(e.Id, out var uv) && astVon.TryGetValue(uv.Id, out var b) ? b : null;
        if (w.Aeste.Count > 0)
        {
            foreach (var u in w.Unterverteiler) if (AstVon(u) is int a) w.Aeste[a - 1].Unterverteiler.Add(u);
            foreach (var v in w.Verbraucher) if (AstVon(v) is int a) w.Aeste[a - 1].Verbraucher.Add(v);
            if (w.RlBaum is { } rb)
            {
                var kinderRl = rb.Reihe.Where(x => x.Eltern is not null).GroupBy(x => x.Eltern!.Element.Id).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
                var astMenge = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
                HashSet<int> Menge(HeizKnoten kn)
                {
                    if (astMenge.TryGetValue(kn.Element.Id, out var s)) return s;
                    s = [];
                    if (kn.Typ is "verbraucher" or "unterverteiler" && AstVon(kn.Element) is int a) s.Add(a);
                    foreach (var c in kinderRl.GetValueOrDefault(kn.Element.Id) ?? []) s.UnionWith(Menge(c));
                    return astMenge[kn.Element.Id] = s;
                }
                for (int i = 0; i < rlGlieder.Count; i++)
                    if (rb.Knoten.TryGetValue(rlGlieder[i].Glied.Element.Id, out var kn) || (rlGlieder[i].Glied.Eintritt == Bezug.An
                        && rb.Knoten.TryGetValue(rlGlieder[i].Glied.Element.Bezuege[Bezug.An], out kn)))
                        if (Menge(kn) is { Count: 1 } einzig) rlGlieder[i] = (rlGlieder[i].Glied with { Ast = einzig.First() }, rlGlieder[i].Index);
            }
        }

        // Glieder in Fliessreihenfolge: Vorlauf ab dem Abgang, Rücklauf von den Verbrauchern zum Abgang
        int reihe = 0;
        foreach (var (g, _) in vlGlieder.OrderBy(x => x.Glied.Tiefe).ThenBy(x => x.Index))
        {
            w.Glieder.Add(g with { Reihe = ++reihe });
            if (g.Ast is int a) w.Aeste[a - 1].Glieder.Add(g.Element);
        }
        foreach (var (g, _) in rlGlieder.OrderByDescending(x => x.Glied.Tiefe).ThenByDescending(x => x.Index))
        {
            w.Glieder.Add(g with { Reihe = ++reihe });
            if (g.Ast is int a && w.Aeste.Count >= a) w.Aeste[a - 1].Glieder.Add(g.Element);
        }
        w.Elemente.Remove(w.Abgang.Id);
    }

    // ------------------------------------------------------------------ abgeleitete Werte

    /// <summary>Spreizung in K: |vlTemp − rlTemp| (Heizung: vl − rl, Kälte: rl − vl); null, wenn eine Temperatur fehlt.</summary>
    public static double? Spreizung(Element hg)
    {
        if (hg.Zahl("vlTemp") is not double v || hg.Zahl("rlTemp") is not double r) return null;
        return Math.Round(Math.Abs(v - r), 3);
    }

    /// <summary>Ist die Gruppe eine Kältegruppe (Medium Kälte bzw. verbraucherart kaelte)?</summary>
    public static bool IstKaelte(Modell m, HeizgruppenWeg w) =>
        (w.MediumVl ?? w.Vorlauf?.Text("medium"))?.StartsWith("KAE", StringComparison.OrdinalIgnoreCase) == true || w.Gruppe.Text("verbraucherart") == "kaelte";

    /// <summary>art aus den Gliedern: gemischt, wenn ein Mischventil (bzw. Ventil art mischventil, 3weg/4weg) dabei ist, sonst ungemischt.</summary>
    public static string ArtAbleiten(HeizgruppenWeg w) =>
        w.Glieder.Any(g => (g.Element.Klasse == "Mischventil" && g.Element.Text("art") != "umschalt") || (g.Element.Klasse == "Ventil" && g.Element.Text("art") == "mischventil"))
            ? "gemischt" : "ungemischt";

    /// <summary>verbraucherart aus den Klassen der Verbraucher (fbh, heizkoerper, kaelte, gemischt); null ohne Verbraucher.</summary>
    public static string? VerbraucherartAbleiten(Modell m, HeizgruppenWeg w)
    {
        if (IstKaelte(m, w)) return "kaelte";
        var arten = w.Verbraucher.Select(v => v.Klasse switch { "Fussbodenheizkreis" => "fbh", "Heizkoerper" => "heizkoerper", _ => "gemischt" }).Distinct().ToList();
        return arten.Count switch { 0 => null, 1 => arten[0], _ => "gemischt" };
    }

    /// <summary>Anzeigename einer verbraucherart für den Gruppennamen.</summary>
    public static string VerbraucherartKurz(string? art) => art switch
    {
        "fbh" => "FBH", "heizkoerper" => "Heizkörper", "lufterhitzer" => "Lufterhitzer", "kaelte" => "Kälte", "gemischt" => "Gemischt", _ => "Heizgruppe",
    };

    /// <summary>Systeme der Wege: das häufigste System der Leitungsbauteile im Vorlauf- bzw. Rücklaufweg (für Vorschlag und Anlegen).</summary>
    public static (Element? Vorlauf, Element? Ruecklauf) SystemeAusWegen(Modell m, Katalog k, HeizgruppenWeg w)
    {
        Element? Haeufigstes(HeizBaum? b) => b?.Reihe.Where(x => Netz.IstLeitungsbauteil(k, x.Element) && x.Element.System is not null)
            .GroupBy(x => x.Element.System!).OrderByDescending(g => g.Count()).ThenBy(g => g.Min(x => x.Tiefe)).Select(g => m.Finde(g.Key)).FirstOrDefault(x => x is { Klasse: "System" });
        return (Haeufigstes(w.VlBaum), Haeufigstes(w.RlBaum));
    }

    // ------------------------------------------------------------------ Vorschlag

    /// <summary>Probe-Weg einer möglichen Gruppe ab einem Abgang (ohne Heizgruppe im Modell).</summary>
    public static HeizgruppenWeg Probe(Modell m, Katalog k, Element abgang, string abgangVl, string? abgangRl, Func<Element, bool> grenze)
    {
        var w = new HeizgruppenWeg
        {
            Gruppe = new Element { Id = "vorschlag", Kennzeichen = "vorschlag", Klasse = "Heizgruppe" },
            Abgang = abgang, AbgangVl = abgangVl, AbgangRl = abgangRl,
        };
        if (abgangRl is not null) w.AbgangRlWoher = "eigenschaft";
        WegeAbleiten(m, k, w, grenze, null);
        return w;
    }

    /// <summary>Eine Seite einer Pumpe (Breitensuche bis Verbraucher, Zentralen und andere Pumpen; Verteiler durchlaufen).</summary>
    sealed record PumpenSeite(string Anschluss, HeizBaum Baum, int Verbraucher, int Zentralen, int Pumpen);

    static PumpenSeite Seite(Modell m, Katalog k, Element pumpe, Anschluss a, string medium)
    {
        var b = Folgen(m, k, pumpe, a.Name, medium, grenze: null, stopp: e => e.Klasse == "Pumpe");
        return new PumpenSeite(a.Name, b, b.Reihe.Count(x => x.Typ == "verbraucher"), b.Reihe.Count(x => x.Typ == "grenze"), b.Reihe.Count(x => x.Typ == "stopp"));
    }

    /// <summary>Abgang auf der Speiseseite einer Pumpe: dem Weg folgen bis zur ersten Zentrale (Abgang = ihr Anschluss) bzw. bis zur ersten Verzweigung
    /// mit mehr als einem Ast, der zu einer Zentrale, einem Verbraucher oder einer Pumpe führt (Abgang = das Element und der Anschluss Richtung Pumpe).</summary>
    static (Element Element, string Anschluss)? AbgangAufSpeiseseite(Modell m, Katalog k, Element pumpe, string anschluss, string medium)
    {
        var baum = Folgen(m, k, pumpe, anschluss, medium, grenze: e => true, stopp: e => e.Klasse == "Pumpe");
        if (baum.Reihe.Count == 0) return null;
        var kinder = baum.Reihe.Where(x => x.Eltern is not null).GroupBy(x => x.Eltern!.Element.Id).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var relevant = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool Relevant(HeizKnoten kn)
        {
            if (relevant.TryGetValue(kn.Element.Id, out var r)) return r;
            r = kn.Typ is "verbraucher" or "unterverteiler" or "grenze" or "stopp" || (kinder.TryGetValue(kn.Element.Id, out var l) && l.Any(Relevant));
            return relevant[kn.Element.Id] = r;
        }
        var cur = baum.Reihe[0];
        for (int i = 0; i < 100000; i++)
        {
            if (cur.Typ is "grenze" or "unterverteiler") return (cur.Element, cur.Eintritt!);
            if (cur.Typ is "verbraucher" or "stopp") return null;
            var rel = (kinder.GetValueOrDefault(cur.Element.Id) ?? []).Where(Relevant).ToList();
            if (rel.Count == 1) { cur = rel[0]; continue; }
            if (rel.Count >= 2) return (cur.Element, cur.Eintritt!);
            return null;
        }
        return null;
    }

    /// <summary>Abgang einer Gruppe aus ihrer Pumpe (siehe Dateikopf): Seite mit Verbrauchern, Speiseseite, erste Zentrale bzw. Verzweigung dort.
    /// Liegt die Pumpe im Rücklauf, ist der gefundene Anschluss der Rücklauf-Abgang und der Vorlauf sein Gegenstück nach dem Namen. null mit Grund.</summary>
    public static (Element Element, string Vl, string? Rl)? AbgangAusPumpe(Modell m, Katalog k, Element p, out string? grund)
    {
        grund = null;
        var anschluesse = p.Anschluesse.Where(a => IstHeizmedium(k, Netz.Medium(m, p, a)) && m.VerbindungAn(p.Id, a.Name) is not null).ToList();
        if (anschluesse.Count < 2) { grund = $"Pumpe {p.Kennzeichen}: nicht beidseitig im Heiz- bzw. Kältenetz verbunden – kein Vorschlag."; return null; }
        var medium = Netz.Medium(m, p, anschluesse[0]);
        var seiten = anschluesse.Select(a => Seite(m, k, p, a, medium)).ToList();
        var verbraucherSeiten = seiten.Where(s => s.Verbraucher > 0 && s.Zentralen == 0 && s.Pumpen == 0).ToList();
        if (verbraucherSeiten.Count != 1)
        {
            grund = verbraucherSeiten.Count == 0
                ? $"Pumpe {p.Kennzeichen}: keine Seite führt nur zu Verbrauchern (Erzeugerkreis bzw. Netz unvollständig) – kein Vorschlag."
                : $"Pumpe {p.Kennzeichen}: beide Seiten führen zu Verbrauchern – Speiseseite unklar, kein Vorschlag.";
            return null;
        }
        var speise = seiten.First(s => s != verbraucherSeiten[0]);
        if (AbgangAufSpeiseseite(m, k, p, speise.Anschluss, medium) is not { } ab)
        {
            grund = $"Pumpe {p.Kennzeichen}: auf der Speiseseite ({speise.Anschluss}) keine Zentrale bzw. Verzweigung gefunden – kein Vorschlag.";
            return null;
        }
        if (Rolle(k, medium) != "ruecklauf") return (ab.Element, ab.Anschluss, null);
        if (PartnerAnschluss(ab.Anschluss) is not { } pv || ab.Element.AnschlussNamens(pv) is not { } pva)
        {
            grund = $"Pumpe {p.Kennzeichen} im Rücklauf: zum Abgang {ab.Element.Kennzeichen}.{ab.Anschluss} gibt es keinen Vorlaufanschluss nach dem Namen – kein Vorschlag.";
            return null;
        }
        return (ab.Element, pva.Name, ab.Anschluss);
    }

    /// <summary>Vorschläge für Heizgruppen (siehe Dateikopf). anlage = nur diese Anlage (Id), null = alle.</summary>
    public static List<HeizgruppenVorschlag> Vorschlaege(Modell m, Katalog k, string? anlageId, List<string> hinweise)
    {
        var stand = Ergebnis(m, k);
        var abgedeckt = new HashSet<string>(stand.GruppenJeElement.Keys, StringComparer.Ordinal);
        var belegteAbgaenge = new HashSet<string>(m.DerKlasse("Heizgruppe").Where(h => h.Bezuege.ContainsKey(Bezug.Verteiler) && h.Text("abgangVl") is not null)
            .Select(h => $"{h.Bezuege[Bezug.Verteiler]}|{h.Text("abgangVl")!.ToLowerInvariant()}"), StringComparer.Ordinal);
        var vorschlaege = new List<HeizgruppenVorschlag>();
        var verteilerAbgaenge = new HashSet<string>(belegteAbgaenge.Select(x => x.Split('|')[0]), StringComparer.Ordinal);
        bool Grenze(Element e) => verteilerAbgaenge.Contains(e.Id);

        bool InAnlage(Element e) => anlageId is null || SystemeVon(m, e).Any(s => s.TeilVon == anlageId);

        HeizgruppenVorschlag? Bilden(Element abgang, string anVl, string? anRl, Element? pumpe)
        {
            var w = Probe(m, k, abgang, anVl, anRl, Grenze);
            if (w.Verbraucher.Count == 0) return null;
            var (vl, rl) = SystemeAusWegen(m, k, w);
            var art = ArtAbleiten(w);
            var vart = VerbraucherartAbleiten(m, w);
            return new HeizgruppenVorschlag(abgang, anVl, w.AbgangRl, pumpe, vl, rl, art, vart, $"{VerbraucherartKurz(vart)} {art}", w);
        }

        void Aufnehmen(HeizgruppenVorschlag v)
        {
            vorschlaege.Add(v);
            belegteAbgaenge.Add($"{v.Abgang.Id}|{v.AbgangVl.ToLowerInvariant()}");
            verteilerAbgaenge.Add(v.Abgang.Id);
            foreach (var id in v.Weg.Elemente) abgedeckt.Add(id);
            foreach (var x in v.Weg.Verbraucher) abgedeckt.Add(x.Id);
        }

        // 1. je Gruppenpumpe
        foreach (var p in m.DerKlasse("Pumpe").Where(p => HatHeizmedium(m, k, p) && InAnlage(p)).OrderBy(p => p.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (abgedeckt.Contains(p.Id)) continue;
            if (AbgangAusPumpe(m, k, p, out var grund) is not { } ab) { hinweise.Add(grund!); continue; }
            if (belegteAbgaenge.Contains($"{ab.Element.Id}|{ab.Vl.ToLowerInvariant()}")) continue;
            if (Bilden(ab.Element, ab.Vl, ab.Rl, p) is { } v) Aufnehmen(v);
            else hinweise.Add($"Pumpe {p.Kennzeichen}: ab {ab.Element.Kennzeichen}.{ab.Vl} keine Verbraucher erreicht – kein Vorschlag.");
        }

        // 2. je Abgang eines Verteilers, der in keiner Gruppe liegt (Gruppe ohne Pumpe)
        foreach (var vt in m.Elemente.Where(e => k.IstArt(e, "Verteiler") && !abgedeckt.Contains(e.Id) && InAnlage(e)).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList())
            foreach (var a in vt.Anschluesse.Where(a => Rolle(k, Netz.Medium(m, vt, a)) == "vorlauf" && a.Fluss != "rein" && !a.Name.Equals("vl_ein", StringComparison.OrdinalIgnoreCase)))
            {
                if (m.VerbindungAn(vt.Id, a.Name) is null || belegteAbgaenge.Contains($"{vt.Id}|{a.Name.ToLowerInvariant()}")) continue;
                var probe = Probe(m, k, vt, a.Name, null, Grenze);
                if (probe.Pumpen.Any(x => !abgedeckt.Contains(x.Id)) || probe.Verbraucher.Count == 0 || probe.Verbraucher.All(x => abgedeckt.Contains(x.Id))) continue;
                if (Bilden(vt, a.Name, null, null) is { } v) Aufnehmen(v);
            }
        return vorschlaege;
    }

    /// <summary>Heizgruppen, in deren Wegen Leitungsbauteile der genannten Stränge liegen (Modul 1d: «verknüpfte Heizgruppe» eines HKAP-Strangs).</summary>
    public static List<Element> FuerStraenge(Modell m, Katalog k, IEnumerable<Element> straenge)
    {
        var ids = straenge.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0 || !m.DerKlasse("Heizgruppe").Any()) return [];
        var stand = Ergebnis(m, k);
        return m.Elemente.Where(e => e.TeilVon is not null && ids.Contains(e.TeilVon)).SelectMany(e => stand.In(e.Id)).Select(w => w.Gruppe)
            .DistinctBy(g => g.Id).OrderBy(g => g.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Systeme eines Elements: eigenes System bzw. die der angeschlossenen Leitungsbauteile.</summary>
    public static List<Element> SystemeVon(Modell m, Element e) => Netz.SystemeVon(m, e).Where(s => s.Klasse == "System").ToList();

    /// <summary>Anlage(n) eines Elements über seine Systeme.</summary>
    public static List<Element> AnlagenVon(Modell m, Katalog k, Element e) =>
        SystemeVon(m, e).Select(s => m.Finde(s.TeilVon)).Where(a => a is not null && k.IstArt(a, "Anlage")).Select(a => a!).DistinctBy(a => a.Id).ToList();
}
