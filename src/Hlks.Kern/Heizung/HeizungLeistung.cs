// Modul 5 Teil A-D (entwurf/modul-5.md, Punkt 4): Leistungssummen, normunabhängig nach dem Muster von Modul 4 Teil A (SanitaerBelastung).
//
// Summiert wird nur die Angabe «leistung» der Verbraucher (Heizkörper, FBH-Kreis …: Normleistung bzw. Heizleistung in W, Herkunft wie gesetzt).
// Die Summe heisst «Σ Normleistung der Verbraucher (Angabe)» und wird ausdrücklich NICHT auf Auslegungstemperaturen umgerechnet (Teil B, Normen
// fehlen) – kein Volumenstrom, keine Stoffwerte.
//   je Leitung   der Vorlauf trägt die Verbraucher stromab, der Rücklauf die stromauf: ab jedem Verbraucheranschluss im Vorlaufmedium wird dem Netz
//                stromauf gefolgt, im Rücklaufmedium stromab (Fliessrichtung an den Anschlüssen wie Modul 3), durch Leitungsbauteile und Verteiler
//                (Unterverteiler tragen ihre Kreise weiter), bis zur nächsten anderen Zentrale (Speicher, Erzeuger, Wärmetauscher). Jede erreichte
//                Leitung bekommt die Leistung des Verbrauchers; jeder Verbraucher zählt je Leitung und Weg höchstens einmal.
//   je Verteiler die Verbraucher, deren Vorlaufweg durch ihn geht.
//   je Heizgruppe die abgeleiteten Verbraucher der Gruppe (Heizgruppen.cs).
//   je Anlage    die Verbraucher an Systemen der Anlage.
//   je Raum      die Verbraucher im Raum neben Raum.heizleistung (HKAP bzw. Eingabe) – nur Information, kein Befund: Normleistung und Raumheizlast
//                sind nicht vergleichbar, solange Teil B fehlt.
// Masche (Ring, Tichelmann): erreicht der Weg ab einem Verbraucher ein Element ein zweites Mal, zählt er trotzdem einmal; der Treffpunkt wird als
// Befund P39 gemeldet, die Leitungen in der Masche sind gekennzeichnet.
// Zwischengespeichert je Modellstand.

using System.Runtime.CompilerServices;

namespace Hlks.Kern;

/// <summary>Beitrag eines Verbrauchers zu einer Leitung bzw. einem Verteiler.</summary>
/// <param name="Weg">vl | rl</param>
/// <param name="LeistungW">Angabe «leistung» in W; null = keine Angabe.</param>
public sealed record HeizBeitrag(Element Verbraucher, string Anschluss, string Weg, double? LeistungW);

/// <summary>Summe über Beiträge (jeder Verbraucher einmal).</summary>
public sealed record HeizSumme(double SummeW, IReadOnlyList<Element> Verbraucher, IReadOnlyList<Element> OhneAngabe, bool Masche,
    double MassenstromKgh = 0, double VolumenstromLh = 0, IReadOnlyList<Element>? OhneStrom = null)
{
    public int Anzahl => Verbraucher.Count;
}

/// <summary>Massen- und Volumenstrom eines Verbrauchers (Christian 02.11.2026: Leistung → Massenstrom, Temperatur aus Heizgruppe): ṁ = Q / (c_p · ΔT),
/// V = ṁ / ρ, Stoffwerte bei der mittleren Temperatur (stoffwerte.json); Grund, wenn etwas fehlt.</summary>
/// <param name="Basis">bedarf (Heizleistung des Raums, verteilt) | normleistung (Rückfall: Raum ohne Heizleistung) – Christian 13.11.2026, Frage 20.</param>
/// <param name="BasisW">Leistung, aus der der Strom gerechnet ist.</param>
public sealed record HeizStrom(double? Kgh, double? Lh, string? Grund, double? SpreizungK = null, Stoffwert? Stoff = null, string? Basis = null, double? BasisW = null);

public sealed class HeizungNetz
{
    /// <summary>Verbraucher (Id) → woher die Leistung kommt: angabe (am Verbraucher) | raum (Heizleistung des Raums gleich verteilt).</summary>
    public Dictionary<string, string> LeistungWoher { get; } = new(StringComparer.Ordinal);
    /// <summary>Verbraucher (Id) → Massen- und Volumenstrom.</summary>
    public Dictionary<string, HeizStrom> Strom { get; } = new(StringComparer.Ordinal);
    /// <summary>Verbraucher (Id) → Heizgruppe (aus Heizgruppen.Ableiten); für Leitungen ausserhalb einer Gruppe (Frage 21).</summary>
    public Dictionary<string, Element> GruppeJeVerbraucher { get; } = new(StringComparer.Ordinal);
    public required Modell Modell { get; init; }
    public required Katalog Katalog { get; init; }
    /// <summary>Verbraucher mit Heiz- bzw. Kältemedium → Leistung in W (null = keine Angabe).</summary>
    public Dictionary<string, double?> Verbraucher { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Beiträge; nur Leitungen in einem Heiz- bzw. Kältemedium.</summary>
    public Dictionary<string, List<HeizBeitrag>> JeLeitung { get; } = new(StringComparer.Ordinal);
    /// <summary>Leitung (Id) → Weg (vl | rl).</summary>
    public Dictionary<string, string> LeitungWeg { get; } = new(StringComparer.Ordinal);
    /// <summary>Verteiler (Id) → Beiträge der Verbraucher, deren Vorlaufweg durch ihn geht.</summary>
    public Dictionary<string, List<HeizBeitrag>> JeVerteiler { get; } = new(StringComparer.Ordinal);
    /// <summary>Treffpunkt (Id) → Verbraucher (Ids), die ihn über zwei Wege erreichen.</summary>
    public Dictionary<string, SortedSet<string>> Maschen { get; } = new(StringComparer.Ordinal);
    public HashSet<string> MaschenLeitungen { get; } = new(StringComparer.Ordinal);

    public HeizSumme Summe(IEnumerable<Element> verbraucher, bool masche = false)
    {
        var l = verbraucher.DistinctBy(v => v.Id).OrderBy(v => v.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        double s = 0, kgh = 0, lh = 0;
        var ohne = new List<Element>();
        var ohneStrom = new List<Element>();
        foreach (var v in l)
        {
            var w = Verbraucher.TryGetValue(v.Id, out var x) ? x : HeizungLeistung.LeistungW(Katalog, v);
            if (w is double d) s += d; else ohne.Add(v);
            if (Strom.TryGetValue(v.Id, out var st) && st.Kgh is double a && st.Lh is double b) { kgh += a; lh += b; } else ohneStrom.Add(v);
        }
        return new HeizSumme(Math.Round(s, 3), l, ohne, masche, Math.Round(kgh, 2), Math.Round(lh, 2), ohneStrom);
    }

    public HeizSumme? Leitung(string id) =>
        JeLeitung.TryGetValue(id, out var b) ? Summe(b.Select(x => x.Verbraucher), MaschenLeitungen.Contains(id)) : null;

    public HeizSumme? Verteiler(string id) =>
        JeVerteiler.TryGetValue(id, out var b) ? Summe(b.Select(x => x.Verbraucher)) : null;
}

public static class HeizungLeistung
{
    public const string Beschriftung = "Σ Normleistung der Verbraucher (Angabe)";
    public const string NichtUmgerechnet = "Leistung: Angabe «leistung» am Verbraucher, sonst die Heiz- bzw. Kühlleistung des Raums gleich auf seine Verbraucher ohne Angabe verteilt (Christian 02.11.2026); " +
        "nicht auf andere Auslegungstemperaturen umgerechnet. Massenstrom ṁ = Q / (c_p · ΔT) aus dem Bedarf (Heiz- bzw. Kühlleistung des Raums, auf seine Verbraucher nach " +
        "Normleistung verteilt wie heizkoerper_auslegung; Christian 13.11.2026), ohne Raumwert aus der Normleistung mit Hinweis; Spreizung der Heizgruppe (vlTemp − rlTemp), Stoffwerte " +
        "des Mediums bei der mittleren Temperatur (kataloge/stoffwerte.json), Volumenstrom = ṁ / ρ; je Leitung die Summe der Verbraucher dahinter.";
    public const string HinweisNormleistung = "Massenstrom aus der Normleistung (Raum ohne Heiz- bzw. Kühlleistung) – bei tieferen Betriebstemperaturen zu gross; Heizlast übernehmen.";

    /// <summary>Bedarf je Heizverbraucher (Frage 20, Christian 13.11.2026 «aus dem Bedarf»): Heiz- bzw. Kühlleistung des Raums, auf seine Verbraucher
    /// nach Normleistung verteilt (nicht alle mit Normleistung: gleich), wie heizkoerper_auslegung; Raum ohne Wert → Normleistung (Angabe) als Rückfall.</summary>
    public static Dictionary<string, (double? W, string? Basis)> Bedarf(Modell m, Katalog k, IReadOnlyList<Element> verbraucher)
    {
        var aus = new Dictionary<string, (double? W, string? Basis)>(StringComparer.Ordinal);
        bool Kaelte(Element v) => v.Anschluesse.Any(a => Netz.Medium(m, v, a).StartsWith("KAE", StringComparison.OrdinalIgnoreCase));
        foreach (var gr in verbraucher.GroupBy(v => (v.Raum ?? "", Kaelte(v))))
        {
            var l = gr.ToList();
            var raum = gr.Key.Item1.Length == 0 ? null : m.Finde(gr.Key.Item1);
            if (raum?.Zahl(gr.Key.Item2 ? "kuehlleistung" : "heizleistung") is double q && q > 0)
            {
                bool alleMitN = l.All(v => LeistungW(k, v) is double w && w > 0);
                double summeN = alleMitN ? l.Sum(v => LeistungW(k, v)!.Value) : 0;
                foreach (var v in l) aus[v.Id] = (Math.Round(q * (alleMitN ? LeistungW(k, v)!.Value / summeN : 1.0 / l.Count), 3), "bedarf");
            }
            else foreach (var v in l) aus[v.Id] = LeistungW(k, v) is double w ? (w, "normleistung") : (null, null);
        }
        return aus;
    }

    /// <summary>Leistung je Heizverbraucher mit Herkunft: Angabe am Verbraucher; sonst Heizleistung (Kälte: Kühlleistung) des Raums abzüglich der
    /// Angaben, gleich auf die Verbraucher ohne Angabe im Raum verteilt (Christian 02.11.2026: «Raum, gleich verteilt»).</summary>
    public static Dictionary<string, (double? W, string? Woher)> Leistungen(Modell m, Katalog k, IReadOnlyList<Element> verbraucher)
    {
        var aus = new Dictionary<string, (double? W, string? Woher)>(StringComparer.Ordinal);
        bool Kaelte(Element v) => v.Anschluesse.Any(a => Netz.Medium(m, v, a).StartsWith("KAE", StringComparison.OrdinalIgnoreCase));
        foreach (var gr in verbraucher.GroupBy(v => (v.Raum ?? "", Kaelte(v))))
        {
            var angabe = gr.Where(v => LeistungW(k, v) is not null).ToList();
            foreach (var v in angabe) aus[v.Id] = (LeistungW(k, v), "angabe");
            var ohne = gr.Where(v => LeistungW(k, v) is null).ToList();
            var raum = gr.Key.Item1.Length == 0 ? null : m.Finde(gr.Key.Item1);
            double? rest = raum?.Zahl(gr.Key.Item2 ? "kuehlleistung" : "heizleistung") is double hl ? Math.Max(0, hl - angabe.Sum(v => LeistungW(k, v)!.Value)) : null;
            foreach (var v in ohne) aus[v.Id] = rest is double r ? (Math.Round(r / ohne.Count, 3), "raum") : (null, null);
        }
        return aus;
    }

    /// <summary>Massen- und Volumenstrom je Verbraucher aus Leistung, Spreizung der Heizgruppe und Stoffwerten.</summary>
    static void Stroeme(Modell m, Katalog k, HeizungNetz n, Dictionary<string, (double? W, string? Basis)> bedarf)
    {
        var gruppe = n.GruppeJeVerbraucher;
        foreach (var hg in m.DerKlasse("Heizgruppe"))
        {
            try { foreach (var v in Heizgruppen.Ableiten(m, k, hg).Verbraucher) gruppe.TryAdd(v.Id, hg); }
            catch (HlksFehler) { /* Gruppe ohne gültigen Weg: ihre Verbraucher bleiben ohne Spreizung */ }
        }
        foreach (var id in n.Verbraucher.Keys)
        {
            var v = m.Finde(id)!;
            var (w, basis) = bedarf.GetValueOrDefault(id);
            if (w is not double q) { n.Strom[id] = new HeizStrom(null, null, "keine Leistung (weder Heizleistung des Raums noch Normleistung)"); continue; }
            if (!gruppe.TryGetValue(id, out var hg) || hg.Zahl("vlTemp") is not double vl || hg.Zahl("rlTemp") is not double rl)
            { n.Strom[id] = new HeizStrom(null, null, "Vor- bzw. Rücklauftemperatur fehlt (Heizgruppe vlTemp/rlTemp)"); continue; }
            var dt = Math.Abs(vl - rl);
            if (dt < 0.1) { n.Strom[id] = new HeizStrom(null, null, "Spreizung 0 K (vlTemp = rlTemp)"); continue; }
            var medium = v.Anschluesse.Select(a => Netz.Medium(m, v, a)).FirstOrDefault(x => k.PaarRolle(x) == "vorlauf");
            var tm = (vl + rl) / 2;
            if (k.Stoffwerte?.Wert(medium, tm) is not { } st) { n.Strom[id] = new HeizStrom(null, null, $"Stoffwerte fehlen für {medium ?? "?"} bei {tm:0.#} °C (stoffwerte.json)", dt); continue; }
            var kgh = q / (st.CpKjKgK * 1000 * dt) * 3600;
            n.Strom[id] = new HeizStrom(Math.Round(kgh, 3), Math.Round(kgh / st.RhoKgM3 * 1000, 3), null, dt, st, basis, q);
        }
    }

    /// <summary>Leistung eines Verbrauchers in W aus der Angabe «leistung» (Einheit W, kW wird umgerechnet); null = keine Angabe.</summary>
    public static double? LeistungW(Katalog k, Element e)
    {
        if (e.Eigenschaft("leistung") is not { } w || w.AlsZahl() is not double v) return null;
        var einheit = w.Einheit ?? (k.Klassen.TryGetValue(e.Klasse, out var d) ? d.Eigenschaften.FirstOrDefault(x => x.Name == "leistung")?.Einheit : null);
        return einheit?.Equals("kW", StringComparison.OrdinalIgnoreCase) == true ? v * 1000 : v;
    }

    public static bool IstHeizverbraucher(Modell m, Katalog k, Element e) => Netz.IstEndgeraet(k, e) && Heizgruppen.HatHeizmedium(m, k, e);

    sealed class Gespeichert(long zaehler, Katalog katalog, HeizungNetz netz)
    {
        public long Zaehler { get; } = zaehler;
        public Katalog Katalog { get; } = katalog;
        public HeizungNetz Netz { get; } = netz;
    }

    static readonly ConditionalWeakTable<Modell, Gespeichert> zwischen = new();

    public static HeizungNetz Ergebnis(Modell m, Katalog k)
    {
        var z = m.Aenderungszaehler;
        if (zwischen.TryGetValue(m, out var g) && g.Zaehler == z && ReferenceEquals(g.Katalog, k)) return g.Netz;
        var n = Rechnen(m, k);
        zwischen.AddOrUpdate(m, new Gespeichert(z, k, n));
        return n;
    }

    static HeizungNetz Rechnen(Modell m, Katalog k)
    {
        var n = new HeizungNetz { Modell = m, Katalog = k };
        foreach (var e in m.Elemente)
            if (k.IstArt(e, "Leitung") && e.Anschluesse.Select(a => Netz.Medium(m, e, a)).FirstOrDefault(x => x != "*") is { } med && k.PaarRolle(med) is { } rolle)
            {
                n.JeLeitung[e.Id] = [];
                n.LeitungWeg[e.Id] = rolle == "vorlauf" ? "vl" : "rl";
            }
        var gezaehlt = new HashSet<string>(StringComparer.Ordinal);
        var heizverbraucher = m.Elemente.Where(e => IstHeizverbraucher(m, k, e)).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var leistungen = Leistungen(m, k, heizverbraucher);
        foreach (var v in heizverbraucher)
        {
            var (leistung, woher) = leistungen.GetValueOrDefault(v.Id);
            n.Verbraucher[v.Id] = leistung;
            if (woher is not null) n.LeistungWoher[v.Id] = woher;
            foreach (var an in v.Anschluesse)
            {
                var medium = Netz.Medium(m, v, an);
                var rolle = k.PaarRolle(medium);
                if (rolle is null) continue;
                var weg = rolle == "vorlauf" ? "vl" : "rl";
                var beitrag = new HeizBeitrag(v, an.Name, weg, leistung);
                Folgen(m, k, v, an, medium, rolle == "vorlauf" ? "stromauf" : "stromab",
                    erreicht: x =>
                    {
                        if (n.JeLeitung.TryGetValue(x.Id, out var l) && gezaehlt.Add($"{x.Id}|{v.Id}|{weg}")) l.Add(beitrag);
                        else if (weg == "vl" && k.IstArt(x, "Verteiler") && gezaehlt.Add($"{x.Id}|{v.Id}|vt"))
                        {
                            if (!n.JeVerteiler.TryGetValue(x.Id, out var vl)) n.JeVerteiler[x.Id] = vl = [];
                            vl.Add(beitrag);
                        }
                    },
                    masche: (treffpunkt, elemente) =>
                    {
                        if (!n.Maschen.TryGetValue(treffpunkt.Id, out var s)) n.Maschen[treffpunkt.Id] = s = new(StringComparer.Ordinal);
                        s.Add(v.Id);
                        foreach (var x in elemente) if (n.JeLeitung.ContainsKey(x.Id)) n.MaschenLeitungen.Add(x.Id);
                    });
            }
        }
        Stroeme(m, k, n, Bedarf(m, k, heizverbraucher));
        return n;
    }

    static bool Ausgang(string fluss, string richtung) => richtung == "stromab" ? fluss is "raus" or "beide" : fluss is "rein" or "beide";
    static bool Eingang(string fluss, string richtung) => richtung == "stromab" ? fluss is "rein" or "beide" : fluss is "raus" or "beide";

    static bool MediumPasst(Modell m, Element e, Anschluss a, string medium)
    {
        var md = Netz.Medium(m, e, a);
        return md == "*" || md.Equals(medium, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Durch Leitungsbauteile und Verteiler geht der Weg weiter; Verbraucher und andere Zentralen beenden ihn.</summary>
    static bool Durchgang(Katalog k, Element e) => Netz.IstLeitungsbauteil(k, e) || k.IstArt(e, "Verteiler");

    static (Element Element, Anschluss Anschluss)? Gegenueber(Modell m, Element e, Anschluss a)
    {
        var v = m.VerbindungAn(e.Id, a.Name);
        var g = v?.Gegenueber(e.Id, a.Name);
        var nb = m.Finde(g?.Element);
        var na = nb?.AnschlussNamens(g!.Anschluss);
        return nb is null || na is null ? null : (nb, na);
    }

    /// <summary>Gerichtete Breitensuche ab einem Verbraucheranschluss; ein zweites Erreichen eines Elements ist eine Masche.</summary>
    static void Folgen(Modell m, Katalog k, Element verbraucher, Anschluss start, string medium, string richtung, Action<Element> erreicht,
        Action<Element, IReadOnlyList<Element>> masche)
    {
        if (!Ausgang(start.Fluss, richtung) || Gegenueber(m, verbraucher, start) is not { } g0) return;
        var (erster, ea) = g0;
        if (!Eingang(ea.Fluss, richtung) || !MediumPasst(m, erster, ea, medium) || !Durchgang(k, erster)) return;
        var eltern = new Dictionary<string, Element?>(StringComparer.Ordinal) { [erster.Id] = null };
        var besucht = new HashSet<string>(StringComparer.Ordinal) { verbraucher.Id, erster.Id };
        var schlange = new Queue<(Element E, string Ein)>();
        schlange.Enqueue((erster, ea.Name));
        erreicht(erster);
        List<Element> Pfad(Element x) { var l = new List<Element>(); for (Element? y = x; y is not null; y = eltern[y.Id]) l.Add(y); return l; }
        while (schlange.Count > 0)
        {
            var (e, ein) = schlange.Dequeue();
            foreach (var a in e.Anschluesse)
            {
                if (a.Name.Equals(ein, StringComparison.OrdinalIgnoreCase) || !MediumPasst(m, e, a, medium) || !Ausgang(a.Fluss, richtung)) continue;
                if (Gegenueber(m, e, a) is not { } g) continue;
                var (nb, na) = g;
                if (!Eingang(na.Fluss, richtung) || !MediumPasst(m, nb, na, medium) || !Durchgang(k, nb)) continue;
                if (!besucht.Add(nb.Id))
                {
                    if (!eltern.ContainsKey(nb.Id) || eltern[e.Id]?.Id == nb.Id) continue;
                    var p1 = Pfad(e); var p2 = Pfad(nb);
                    var in2 = p2.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                    var verzweigung = p1.FirstOrDefault(x => in2.Contains(x.Id)) ?? erster;
                    masche(nb, p1.TakeWhile(x => x.Id != verzweigung.Id).Concat(p2.TakeWhile(x => x.Id != verzweigung.Id)).ToList());
                    continue;
                }
                eltern[nb.Id] = e;
                erreicht(nb);
                schlange.Enqueue((nb, na.Name));
            }
        }
    }
}
