// Luftmengen im Kanalnetz (Christian 02.11.2026: «lüftung: aus- einlässe bestimmen luftmenge. Flussrichtung aus dem Lüftungsgerät»; Muster wie
// Heizung und Sanitär: Eigenschaften vom Verbraucher her übergeben und summiert).
//   Durchlass  Volumenstrom = Angabe «volumenstrom» am Zu- bzw. Abluftdurchlass; sonst Zu- bzw. Abluft des Raums gleich auf seine Durchlässe
//              ohne Angabe verteilt (wie die Leistung der Heizung).
//   je Kanal   ab jedem Durchlass wird dem Netz zum Gerät gefolgt – Zuluft stromauf (der Kanal versorgt den Durchlass), Abluft stromab – durch
//              Leitungsbauteile (Kanäle, Formteile, Klappen); jeder erreichte Kanal bekommt den Volumenstrom, jeder Durchlass zählt je Kanal einmal.
//              Am Lüftungsgerät bzw. einem anderen Gerät endet der Weg.
// Die Fliessrichtung kommt aus den Anschlüssen; mit fliessrichtung_aus_quelle richtet der Mensch sie vom Lüftungsgerät her aus.
// Zwischengespeichert je Modellstand.

using System.Runtime.CompilerServices;

namespace Hlks.Kern;

public sealed class LueftungNetz
{
    /// <summary>Durchlass (Id) → Volumenstrom in m³/h (null = keine Angabe) und Herkunft (angabe | raum).</summary>
    public Dictionary<string, (double? M3h, string? Woher)> Durchlaesse { get; } = new(StringComparer.Ordinal);
    /// <summary>Kanal (Id) → Durchlässe (Ids), die er versorgt bzw. entlüftet.</summary>
    public Dictionary<string, HashSet<string>> JeKanal { get; } = new(StringComparer.Ordinal);

    public (double M3h, int Anzahl, List<string> OhneAngabe)? Kanal(string id)
    {
        if (!JeKanal.TryGetValue(id, out var d)) return null;
        double s = 0; var ohne = new List<string>();
        foreach (var x in d) if (Durchlaesse.GetValueOrDefault(x).M3h is double v) s += v; else ohne.Add(x);
        return (Math.Round(s, 2), d.Count, ohne);
    }
}

public static class LueftungMengen
{
    sealed class Gespeichert(long zaehler, Katalog katalog, LueftungNetz netz)
    {
        public long Zaehler { get; } = zaehler;
        public Katalog Katalog { get; } = katalog;
        public LueftungNetz Netz { get; } = netz;
    }

    static readonly ConditionalWeakTable<Modell, Gespeichert> zwischen = new();

    public static LueftungNetz Ergebnis(Modell m, Katalog k)
    {
        var z = m.Aenderungszaehler;
        if (zwischen.TryGetValue(m, out var g) && g.Zaehler == z && ReferenceEquals(g.Katalog, k)) return g.Netz;
        var n = Rechnen(m, k);
        zwischen.AddOrUpdate(m, new Gespeichert(z, k, n));
        return n;
    }

    static readonly string[] Klassen = ["Zuluftdurchlass", "Abluftdurchlass"];

    static LueftungNetz Rechnen(Modell m, Katalog k)
    {
        var n = new LueftungNetz();
        var durchlaesse = m.Elemente.Where(e => Klassen.Any(c => k.IstArt(e, c))).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();
        // Volumenstrom: Angabe, sonst Raum gleich verteilt (Zuluft auf Zuluftdurchlässe, Abluft auf Abluftdurchlässe)
        foreach (var gr in durchlaesse.GroupBy(e => (e.Raum ?? "", k.IstArt(e, "Zuluftdurchlass"))))
        {
            double? Angabe(Element e) => e.Zahl("volumenstrom");
            var mit = gr.Where(e => Angabe(e) is not null).ToList();
            foreach (var e in mit) n.Durchlaesse[e.Id] = (Angabe(e), "angabe");
            var ohne = gr.Where(e => Angabe(e) is null).ToList();
            var raum = gr.Key.Item1.Length == 0 ? null : m.Finde(gr.Key.Item1);
            double? rest = raum?.Zahl(gr.Key.Item2 ? "zuluft" : "abluft") is double q ? Math.Max(0, q - mit.Sum(e => Angabe(e)!.Value)) : null;
            foreach (var e in ohne) n.Durchlaesse[e.Id] = rest is double r ? (Math.Round(r / ohne.Count, 2), "raum") : (null, null);
        }
        foreach (var d in durchlaesse)
        {
            // Zuluft kommt zum Durchlass hin (stromauf zum Gerät), Abluft geht von ihm weg (stromab zum Gerät); am Gerät endet der Weg
            var richtung = k.IstArt(d, "Zuluftdurchlass") ? "stromauf" : "stromab";
            foreach (var s in Netz.Folgen(m, k, d, richtung, stopp: e => !Netz.IstLeitungsbauteil(k, e)))
                if (k.IstArt(s.Element, "Leitung"))
                {
                    if (!n.JeKanal.TryGetValue(s.Element.Id, out var menge)) n.JeKanal[s.Element.Id] = menge = new(StringComparer.Ordinal);
                    menge.Add(d.Id);
                }
        }
        return n;
    }

    /// <summary>element_lesen: Volumenstrom am Durchlass (mit Herkunft) bzw. Summe am Kanal (abgeleitet).</summary>
    public static void ElementErgaenzen(Projekt p, Element e, System.Text.Json.Nodes.JsonObject o)
    {
        var k = p.Katalog;
        bool durchlass = Klassen.Any(c => k.IstArt(e, c));
        if (!durchlass && !k.IstArt(e, "Leitung")) return;
        var n = Ergebnis(p.Modell, k);
        System.Text.Json.Nodes.JsonObject Abg() => o["abgeleitet"] as System.Text.Json.Nodes.JsonObject ?? (System.Text.Json.Nodes.JsonObject)(o["abgeleitet"] = new System.Text.Json.Nodes.JsonObject());
        if (durchlass && n.Durchlaesse.TryGetValue(e.Id, out var v) && v.M3h is double q)
        {
            Abg()["volumenstrom_m3h"] = q;
            Abg()["volumenstromWoher"] = v.Woher;
        }
        else if (n.Kanal(e.Id) is { } s)
        {
            Abg()["volumenstrom_m3h"] = s.M3h;
            Abg()["durchlaesse"] = s.Anzahl;
            if (s.OhneAngabe.Count > 0) Abg()["durchlaesseOhneAngabe"] = s.OhneAngabe.Count;
        }
    }
}
