// Modul 4 Teil A-K (entwurf/modul-4.md, Punkte 2 und 3): Apparateart der Sanitärapparate und Belastungssummen im Netz aus Modul 3.
//
// Apparateart (Eigenschaft sanitaerArt, Klasse Apparat): in dieser Reihenfolge
//   1. Eigenschaft sanitaerArt am Element (von Hand bzw. über element_aendern; aus der Familie, wenn ein Parameter ziel = sanitaerArt hat –
//      dann Herkunft «familie», FamilienNachfuehren setzt sie),
//   2. Familienparameter namens «sanitaerArt» (Typ- oder Exemplarwert) ohne ziel,
//   3. Vorgabe der Klasse aus dem Katalog (vorgabeJeKlasse; Bodenablauf hat keine → Befund P21 «Art wählen»).
// Die Werte LU KW, LU WW, DU stehen nur im Katalog (kataloge/sanitaer_belastungswerte.json) und werden nie gespeichert (abgeleitet).
//
// Belastungssummen: je Apparateanschluss mit Medium KW, WW oder SW wird dem Netz gefolgt – Trinkwasser stromauf (die Leitungen, die den
// Apparat versorgen, haben ihn stromab), Schmutzwasser stromab (die Leitungen, in die er entwässert, haben ihn stromauf). Jede erreichte
// Leitung bekommt den Wert des Anschlusses (LU KW, LU WW bzw. DU der Art). Gefolgt wird nur über Anschlüsse der Medien des Netzes
// (Trinkwasser: KW und WW, Schmutzwasser: SW; Zirkulation ZIR und Regenwasser RW sind ausgenommen) und nur durch Leitungsbauteile und
// Zentralen (Wassererwärmer: das Kaltwasser vor dem Wassererwärmer trägt dessen Warmwasser-Apparate mit, wie die Gesamtsumme ΣKW + ΣWW
// am Hausanschluss); an Apparaten, Quellen und Senken endet der Weg.
// Masche (Ringleitung): erreicht der Weg ab einem Apparat ein Element ein zweites Mal, gibt es zwei Wege – der Apparat zählt je Leitung
// trotzdem höchstens einmal (je Apparat und Medium), das Element wird als Befund P24 gemeldet.
// KEINE Normrechnung je Teilstrecke (kein Spitzendurchfluss, keine DN, kein Q_WW je Leitung) – das ist Teil B (Normen fehlen).
//
// Zwischengespeichert je Modellstand (Modell.Aenderungszaehler): Prüfungen, element_lesen und Abfragen teilen eine Rechnung.

using System.Runtime.CompilerServices;

namespace Hlks.Kern;

/// <summary>Apparateart eines Apparats mit Herkunft.</summary>
/// <param name="Woher">eigenschaft | familie | vorgabe | keine | unbekannt (Schlüssel nicht im Katalog)</param>
/// <param name="Herkunft">Lesbare Herkunft der Art (Akteur und Zeit, Familie und Typ bzw. Vorgabe der Klasse).</param>
public sealed record SanitaerZuordnung(SanitaerArt? Art, string? Schluessel, string Woher, string Herkunft);

/// <summary>Beitrag eines Apparateanschlusses zu einer Leitung (je Apparat und Medium höchstens einmal). Wert null = die Art hat in dieser
/// Spalte keinen Wert oder der Apparat hat keine Art.</summary>
public sealed record SanitaerBeitrag(Element Apparat, string Anschluss, string Medium, SanitaerZuordnung Zuordnung, double? Wert)
{
    public string Schluessel => Apparat.Id + "|" + Medium.ToUpperInvariant();
}

/// <summary>Summen über eine Menge von Beiträgen (Leitung, Strang, System, Anlage).</summary>
public sealed record SanitaerSumme(double SummeLuKw, double SummeLuWw, double GroessterLu, double SummeDu, IReadOnlyList<SanitaerBeitrag> Beitraege, bool Masche)
{
    /// <summary>Anzahl verschiedener Apparate mit Beitrag.</summary>
    public int Apparate => Beitraege.Select(b => b.Apparat.Id).Distinct().Count();
    /// <summary>Apparate, die beitragen sollten, aber keinen Wert haben (ohne Art oder Art ohne Wert dieser Spalte).</summary>
    public IReadOnlyList<SanitaerBeitrag> OhneWert => Beitraege.Where(b => b.Wert is null).ToList();
}

public static class SanitaerArten
{
    public static bool IstApparat(Katalog k, Element e) => k.IstArt(e, "Apparat");

    /// <summary>Apparateart eines Apparats (siehe Dateikopf: Eigenschaft › Familienparameter › Vorgabe der Klasse).</summary>
    public static SanitaerZuordnung Von(Modell m, Katalog k, Element e)
    {
        var kat = k.Sanitaer;
        if (kat is null) return new(null, null, "keine", $"Katalog {SanitaerKatalog.Datei} fehlt");
        if (e.Eigenschaft("sanitaerArt") is { } w && w.AlsText() is { Length: > 0 } s)
        {
            var h = HerkunftText(w.Herkunft);
            return kat.Art(s) is { } a ? new(a, a.Schluessel, w.Herkunft.Art == "familie" ? "familie" : "eigenschaft", h) : new(null, s, "unbekannt", h);
        }
        if (FamilienArt(m, k, e) is { } f)
            return kat.Art(f.Schluessel) is { } a ? new(a, a.Schluessel, "familie", f.Quelle) : new(null, f.Schluessel, "unbekannt", f.Quelle);
        if (kat.Vorgabe(k, e.Klasse, out var vk) is { } v)
            return new(v, v.Schluessel, "vorgabe", $"Vorgabe der Klasse {vk} (vorgabeJeKlasse, Katalog {SanitaerKatalog.Datei})");
        return new(null, null, "keine", $"keine Vorgabe für {e.Klasse} im Katalog {SanitaerKatalog.Datei}");
    }

    static string HerkunftText(Herkunft h) =>
        $"{h.Art}, {h.Akteur}, {h.Zeit:yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(h.Quelle) ? "" : ", " + h.Quelle)}";

    /// <summary>Familienparameter «sanitaerArt» (ohne ziel; mit ziel steht die Art schon als Eigenschaft am Element).</summary>
    static (string Schluessel, string Quelle)? FamilienArt(Modell m, Katalog k, Element e)
    {
        if (Familien.FamilieVon(m, e) is not { } f) return null;
        var def = Familien.Definition(f, k);
        if (!def.Gueltig || !def.NachName.ContainsKey("sanitaerArt")) return null;
        var a = FamilienAuswertung.Aufloesen(def, Familien.Typ(e), Familien.Exemplarwerte(e));
        if (!a.Gueltig || a.Wert("sanitaerArt") is not { } w || string.IsNullOrWhiteSpace(w.Text)) return null;
        return (w.Text.Trim(), $"familie, Parameter sanitaerArt der Familie {f.Kennzeichen} «{f.Name ?? def.Name}» Typ {Familien.Typ(e)}");
    }
}

/// <summary>Ergebnis der Netzrechnung eines Modellstands (unveränderlich, zwischengespeichert).</summary>
public sealed class SanitaerNetz
{
    public required Modell Modell { get; init; }
    public required Katalog Katalog { get; init; }
    /// <summary>Alle Apparate (Klasse Apparat) mit Art.</summary>
    public Dictionary<string, SanitaerZuordnung> Apparate { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Beiträge der Apparate (je Apparat und Medium einmal), nur Leitungen der Medien KW, WW, SW.</summary>
    public Dictionary<string, List<SanitaerBeitrag>> JeLeitung { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Medium der Leitung (KW, WW, SW).</summary>
    public Dictionary<string, string> LeitungMedium { get; } = new(StringComparer.Ordinal);
    /// <summary>Element (Id), an dem sich zwei Wege ab einem Apparat treffen → Apparate (Ids).</summary>
    public Dictionary<string, SortedSet<string>> Maschen { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitungen (Ids), deren Summe Apparate einer Masche enthält.</summary>
    public HashSet<string> MaschenLeitungen { get; } = new(StringComparer.Ordinal);
    /// <summary>Apparat (Id) → Anlagen (Ids), an deren Systeme er angeschlossen ist.</summary>
    public Dictionary<string, List<string>> AnlagenJeApparat { get; } = new(StringComparer.Ordinal);

    public static bool IstTrinkwasser(string medium) => SanitaerKatalog.Trinkwasser.Contains(medium);
    public static bool IstAbwasser(string medium) => SanitaerKatalog.Abwasser.Contains(medium);

    /// <summary>Summe über Beiträge; jeder Apparat zählt je Medium einmal.</summary>
    public static SanitaerSumme Summe(IEnumerable<SanitaerBeitrag> beitraege, bool masche)
    {
        var eindeutig = new Dictionary<string, SanitaerBeitrag>(StringComparer.Ordinal);
        foreach (var b in beitraege) eindeutig.TryAdd(b.Schluessel, b);
        var liste = eindeutig.Values.OrderBy(b => b.Apparat.Kennzeichen, StringComparer.OrdinalIgnoreCase).ThenBy(b => b.Medium, StringComparer.Ordinal).ToList();
        double kw = 0, ww = 0, du = 0, max = 0;
        foreach (var b in liste)
        {
            if (b.Wert is not double w) continue;
            switch (SanitaerKatalog.SpalteFuer(b.Medium))
            {
                case "luKw": kw += w; max = Math.Max(max, w); break;
                case "luWw": ww += w; max = Math.Max(max, w); break;
                case "du": du += w; break;
            }
        }
        return new SanitaerSumme(kw, ww, max, du, liste, masche);
    }

    /// <summary>Summe einer Leitung (null = keine Leitung des Trinkwasser- bzw. Abwassernetzes).</summary>
    public SanitaerSumme? Leitung(string id) =>
        LeitungMedium.ContainsKey(id) ? Summe(JeLeitung.GetValueOrDefault(id) ?? [], MaschenLeitungen.Contains(id)) : null;

    /// <summary>Summe über mehrere Leitungen (Strang, System, Anlage): Vereinigung der Apparate.</summary>
    public SanitaerSumme Ueber(IEnumerable<string> leitungen)
    {
        var ids = leitungen.Where(LeitungMedium.ContainsKey).ToList();
        return Summe(ids.SelectMany(i => JeLeitung.GetValueOrDefault(i) ?? []), ids.Any(MaschenLeitungen.Contains));
    }
}

public static class SanitaerBelastung
{
    sealed class Gespeichert(long zaehler, Katalog katalog, SanitaerNetz netz)
    {
        public long Zaehler { get; } = zaehler;
        public Katalog Katalog { get; } = katalog;
        public SanitaerNetz Netz { get; } = netz;
    }

    static readonly ConditionalWeakTable<Modell, Gespeichert> zwischen = new();

    /// <summary>Netzrechnung des Modellstands (zwischengespeichert, solange sich das Modell nicht ändert).</summary>
    public static SanitaerNetz Ergebnis(Modell m, Katalog k)
    {
        var zaehler = m.Aenderungszaehler;
        if (zwischen.TryGetValue(m, out var g) && g.Zaehler == zaehler && ReferenceEquals(g.Katalog, k)) return g.Netz;
        var netz = Rechnen(m, k);
        zwischen.AddOrUpdate(m, new Gespeichert(zaehler, k, netz));
        return netz;
    }

    static SanitaerNetz Rechnen(Modell m, Katalog k)
    {
        var n = new SanitaerNetz { Modell = m, Katalog = k };
        foreach (var e in m.Elemente)
            if (k.IstArt(e, "Leitung") && LeitungsMedium(m, e) is { } med) { n.LeitungMedium[e.Id] = med; n.JeLeitung[e.Id] = []; }

        var maschenApparate = new HashSet<string>(StringComparer.Ordinal);
        var gezaehlt = new HashSet<string>(StringComparer.Ordinal);   // «Leitung|Apparat|Medium»: je Leitung jeder Apparat je Medium einmal
        foreach (var a in m.Elemente.Where(e => SanitaerArten.IstApparat(k, e)).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var z = SanitaerArten.Von(m, k, a);
            n.Apparate[a.Id] = z;
            n.AnlagenJeApparat[a.Id] = Netz.SystemeVon(m, a).Select(s => m.Finde(s.TeilVon)).Where(x => x is not null && k.IstArt(x, "Anlage"))
                .Select(x => x!.Id).Distinct().ToList();
            foreach (var an in a.Anschluesse)
            {
                var medium = Netz.Medium(m, a, an);
                if (SanitaerKatalog.SpalteFuer(medium) is null) continue;
                var beitrag = new SanitaerBeitrag(a, an.Name, medium.ToUpperInvariant(), z, z.Art?.WertFuer(medium));
                Folgen(m, k, a, an, medium,
                    erreicht: x => { if (n.JeLeitung.TryGetValue(x.Id, out var l) && gezaehlt.Add(x.Id + "|" + beitrag.Schluessel)) l.Add(beitrag); },
                    masche: x =>
                    {
                        if (!n.Maschen.TryGetValue(x.Id, out var s)) n.Maschen[x.Id] = s = new(StringComparer.Ordinal);
                        s.Add(a.Id);
                        maschenApparate.Add(a.Id);
                    });
            }
        }
        foreach (var (id, l) in n.JeLeitung)
            if (l.Any(b => maschenApparate.Contains(b.Apparat.Id))) n.MaschenLeitungen.Add(id);
        return n;
    }

    /// <summary>Medium einer Leitung (aus dem System, sonst aus einem Anschluss), nur KW, WW, SW; sonst null (ausgenommen).</summary>
    public static string? LeitungsMedium(Modell m, Element leitung)
    {
        var med = Netz.SystemMedium(m, leitung.System);
        if (string.IsNullOrEmpty(med) || med == "*")
            med = leitung.Anschluesse.Select(a => Netz.Medium(m, leitung, a)).FirstOrDefault(x => x != "*");
        return med is not null && SanitaerKatalog.SpalteFuer(med) is not null ? med.ToUpperInvariant() : null;
    }

    static bool Ausgang(string fluss, string richtung) => richtung == "stromab" ? fluss is "raus" or "beide" : fluss is "rein" or "beide";
    static bool Eingang(string fluss, string richtung) => richtung == "stromab" ? fluss is "rein" or "beide" : fluss is "raus" or "beide";

    static bool MediumPasst(Modell m, Element e, Anschluss? a, IReadOnlySet<string> medien)
    {
        if (a is null) return false;
        var med = Netz.Medium(m, e, a);
        return med == "*" || medien.Contains(med);
    }

    /// <summary>Durch Leitungsbauteile (Leitung, Formteil, Armatur) und Zentralen (Wassererwärmer) geht der Weg weiter; Apparate, Quellen
    /// und Senken beenden ihn.</summary>
    static bool Durchgang(Katalog k, Element e) => Netz.IstLeitungsbauteil(k, e) || Netz.Rolle(k, e) == "zentrale";

    /// <summary>Folgt dem Netz ab einem Apparateanschluss: Trinkwasser stromauf, Schmutzwasser stromab, nur über Anschlüsse der Medien des
    /// Netzes. Ein zweites Erreichen eines Elements ist eine Masche.</summary>
    static void Folgen(Modell m, Katalog k, Element apparat, Anschluss start, string medium, Action<Element> erreicht, Action<Element> masche)
    {
        bool trink = SanitaerNetz.IstTrinkwasser(medium);
        var richtung = trink ? "stromauf" : "stromab";
        var medien = trink ? SanitaerKatalog.Trinkwasser : SanitaerKatalog.Abwasser;
        if (!Ausgang(start.Fluss, richtung)) return;
        var v = m.VerbindungAn(apparat.Id, start.Name);
        var g = v?.Gegenueber(apparat.Id, start.Name);
        var erster = m.Finde(g?.Element);
        var ea = erster?.AnschlussNamens(g!.Anschluss);
        if (erster is null || ea is null || !Eingang(ea.Fluss, richtung) || !Durchgang(k, erster) || !MediumPasst(m, erster, ea, medien)) return;
        var besucht = new HashSet<string>(StringComparer.Ordinal) { apparat.Id, erster.Id };
        var schlange = new Queue<(Element E, string Eintritt)>();
        schlange.Enqueue((erster, ea.Name));
        erreicht(erster);
        while (schlange.Count > 0)
        {
            var (e, ein) = schlange.Dequeue();
            foreach (var (nb, nEin, aus) in Netz.Nachbarn(m, e, ein, richtung, null))
            {
                if (!MediumPasst(m, e, e.AnschlussNamens(aus), medien) || !MediumPasst(m, nb, nb.AnschlussNamens(nEin), medien)) continue;
                if (!Durchgang(k, nb)) continue;
                if (!besucht.Add(nb.Id)) { masche(nb); continue; }
                erreicht(nb);
                schlange.Enqueue((nb, nEin));
            }
        }
    }
}
