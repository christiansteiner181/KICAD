// Netzlogik: dem Systemgraphen in Fliessrichtung folgen. Die Richtung steht an den Anschlüssen (rein/raus);
// Formteile haben ungerichtete Anschlüsse («beide») und übernehmen die Richtung der angeschlossenen Leitungen.

namespace Hlks.Kern;

public sealed record NetzSchritt(Element Element, int Tiefe, string? Vorgaenger, string? Eintritt, double LaengeBisHier);

public static class Netz
{
    /// <summary>Wirksames Medium eines Anschlusses: «*» übernimmt das Medium des Systems.</summary>
    public static string Medium(Modell m, Element e, Anschluss a)
    {
        if (a.Medium != "*") return a.Medium;
        var sys = m.Finde(e.System);
        return sys?.Text("medium") ?? "*";
    }

    /// <summary>Medium eines Systems (aus der Eigenschaft «medium»).</summary>
    public static string? SystemMedium(Modell m, string? systemId) => m.Finde(systemId)?.Text("medium");

    static bool Ausgang(string fluss, string richtung) => richtung switch
    {
        "stromab" => fluss is "raus" or "beide",
        "stromauf" => fluss is "rein" or "beide",
        _ => true,
    };

    static bool Eingang(string fluss, string richtung) => richtung switch
    {
        "stromab" => fluss is "rein" or "beide",
        "stromauf" => fluss is "raus" or "beide",
        _ => true,
    };

    /// <summary>Nachbarn eines Elements in einer Richtung (stromab | stromauf | beide).</summary>
    public static IEnumerable<(Element Nachbar, string Eintritt, string Austritt)> Nachbarn(Modell m, Element e, string? eintritt, string richtung, string? medium)
    {
        foreach (var a in e.Anschluesse)
        {
            if (eintritt is not null && a.Name.Equals(eintritt, StringComparison.OrdinalIgnoreCase)) continue;
            if (medium is not null && Medium(m, e, a) is var med && med != "*" && !med.Equals(medium, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Ausgang(a.Fluss, richtung)) continue;
            var v = m.VerbindungAn(e.Id, a.Name);
            if (v is null) continue;
            var g = v.Gegenueber(e.Id, a.Name)!;
            var n = m.Finde(g.Element);
            var na = n?.AnschlussNamens(g.Anschluss);
            if (n is null || na is null) continue;
            if (!Eingang(na.Fluss, richtung)) continue;
            yield return (n, na.Name, a.Name);
        }
    }

    /// <summary>Breitensuche ab einem Element. bisKlasse beendet den Weg an Elementen dieser Klasse (inklusive).</summary>
    public static List<NetzSchritt> Folgen(Modell m, Katalog k, Element start, string richtung, string? medium = null,
        string? bisKlasse = null, int maxSchritte = 2000, Func<Element, bool>? stopp = null)
    {
        var ergebnis = new List<NetzSchritt>();
        var besucht = new HashSet<string> { start.Id };
        var schlange = new Queue<(Element E, int Tiefe, string? Eintritt, double Laenge)>();
        schlange.Enqueue((start, 0, null, 0));
        while (schlange.Count > 0 && ergebnis.Count < maxSchritte)
        {
            var (e, tiefe, eintritt, laenge) = schlange.Dequeue();
            if (tiefe > 0 && bisKlasse is not null && k.IstArt(e, bisKlasse)) continue;
            if (tiefe > 0 && stopp?.Invoke(e) == true) continue;
            foreach (var (n, nEin, _) in Nachbarn(m, e, eintritt, richtung, medium))
            {
                if (!besucht.Add(n.Id)) continue;
                double l = laenge + (n.Geometrie is { Art: "linie" or "polylinie" } g ? g.Laenge() : 0);
                ergebnis.Add(new NetzSchritt(n, tiefe + 1, e.Kennzeichen, nEin, l));
                schlange.Enqueue((n, tiefe + 1, nEin, l));
            }
        }
        return ergebnis;
    }

    /// <summary>Leitungsbauteile (Leitung, Formteil, Armatur) im Gegensatz zu Endgeräten.</summary>
    public static bool IstLeitungsbauteil(Katalog k, Element e) =>
        k.IstArt(e, "Leitung") || k.IstArt(e, "Formteil") || k.IstArt(e, "Armatur");

    /// <summary>Endgerät = Verbraucher (Apparat, Heizkörper, Luftdurchlass …), nicht Quelle, Senke oder Zentrale.</summary>
    public static bool IstEndgeraet(Katalog k, Element e) => Rolle(k, e) == "verbraucher";

    /// <summary>Rolle im Netz laut Katalog: verbraucher | quelle | senke | zentrale | leitung | null.</summary>
    public static string? Rolle(Katalog k, Element e) => k.Klassen.TryGetValue(e.Klasse, out var d) ? d.Rolle : null;

    /// <summary>Netzbauteil, das kein Leitungsbauteil ist (Verbraucher, Quelle, Senke, Zentrale).</summary>
    public static bool IstNetzgeraet(Katalog k, Element e) => Rolle(k, e) is "verbraucher" or "quelle" or "senke" or "zentrale";

    /// <summary>Richtung, in der die Verbraucher eines Systems liegen (aus dem Systemtyp).</summary>
    public static string VerbraucherRichtung(Modell m, Katalog k, Element system)
    {
        var typ = system.Text("systemtyp");
        return typ is not null && k.Systemtypen.TryGetValue(typ, out var st) ? st.VerbraucherRichtung : "stromab";
    }

    /// <summary>Endgeräte an einem Strang. direkt = ohne den Weg durch einen anderen Strang.</summary>
    public static (List<Element> Direkt, List<Element> Gesamt) Verbraucher(Modell m, Katalog k, Element strang)
    {
        var system = m.Finde(strang.TeilVon) ?? m.Finde(strang.System);
        if (system is null) return ([], []);
        var richtung = VerbraucherRichtung(m, k, system);
        var medium = system.Text("medium");
        var eigene = m.Elemente.Where(e => e.TeilVon == strang.Id).ToList();
        var direkt = new Dictionary<string, Element>();
        var gesamt = new Dictionary<string, Element>();
        foreach (var start in eigene)
        {
            foreach (var s in Folgen(m, k, start, richtung, medium))
                if (IstEndgeraet(k, s.Element)) gesamt[s.Element.Id] = s.Element;
            foreach (var s in Folgen(m, k, start, richtung, medium,
                         stopp: e => e.TeilVon is not null && e.TeilVon != strang.Id && m.Finde(e.TeilVon)?.Klasse == "Strang"))
                if (IstEndgeraet(k, s.Element) && !(s.Element.TeilVon is not null && s.Element.TeilVon != strang.Id && m.Finde(s.Element.TeilVon)?.Klasse == "Strang"))
                    direkt[s.Element.Id] = s.Element;
        }
        return (direkt.Values.OrderBy(e => e.Kennzeichen).ToList(), gesamt.Values.OrderBy(e => e.Kennzeichen).ToList());
    }

    /// <summary>Systeme, an die ein Endgerät angeschlossen ist (über die Systeme der direkt verbundenen Leitungsbauteile).</summary>
    public static List<Element> SystemeVon(Modell m, Element e)
    {
        var ids = new HashSet<string>();
        if (e.System is not null) ids.Add(e.System);
        foreach (var v in m.VerbindungenVon(e.Id))
        {
            var andere = m.Finde(v.Von.Element == e.Id ? v.Nach.Element : v.Von.Element);
            if (andere?.System is not null) ids.Add(andere.System);
        }
        return ids.Select(i => m.Finde(i)).Where(x => x is not null).Select(x => x!).OrderBy(x => x.Kennzeichen).ToList();
    }
}
