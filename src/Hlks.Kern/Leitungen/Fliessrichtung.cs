// Fliessrichtung aus der Quelle (Christian 02.11.2026: «Flussrichtung ist bestimmt durch Pumpeneinbau» bzw. «Flussrichtung aus dem
// Lüftungsgerät»; Entscheid: auf Befehl, nicht still nachgeführt).
// Ab jedem gerichteten Anschluss der Quelle (Pumpe: saug rein, druck raus; Lüftungsgerät: aul/abl rein, zul/fol raus) wird dem Netz durch
// Leitungsbauteile gefolgt, unabhängig von der bisherigen Richtung, im Medium des Anschlusses. Fliesst es vom Anschluss weg, muss jede erreichte
// Leitung an «anfang» betreten werden, fliesst es zu ihm hin, an «ende»; sonst wird sie umgekehrt (Punkte in umgekehrter Folge, Grössen und
// Verbindungen von anfang und ende getauscht – Lage und Anschlüsse der Nachbarn bleiben). Armaturen mit fester Richtung, die gegen den Fluss
// eingebaut sind, und Leitungen, die von zwei Seiten verschieden verlangt werden, meldet der Befehl, ändert sie aber nicht.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class FliessrichtungAusQuelle : IBefehl
{
    public const string Text = "Richtet die Leitungen eines Netzes nach der Quelle aus (Pumpe: saug/druck; Lüftungsgerät: aul, zul, abl, fol; sonst jedes Element mit gerichteten Anschlüssen): ab jedem gerichteten Anschluss der Quelle durch Leitungen, Formteile und Armaturen im Medium des Anschlusses bis zum nächsten Gerät; jede Leitung, die gegen den Fluss gezeichnet ist (Fluss anfang → ende), wird umgekehrt (Punkte, Grössen und Verbindungen von anfang und ende getauscht). Armaturen mit fester Richtung gegen den Fluss und Leitungen mit widersprüchlicher Richtung werden nur gemeldet. Ein Paket.";
    public const string ParQuelle = "Kennzeichen der Quelle (Pumpe, Lüftungsgerät …)";
    public const string ParAnschluss = "Nur ab diesem Anschluss der Quelle (z. B. druck, zul); fehlt = alle gerichteten Anschlüsse";

    public string Name => "fliessrichtung_aus_quelle";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("quelle", "text", true, ParQuelle),
        new("anschluss", "text", false, ParAnschluss),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var quelle = m.Hole(Param.Text(p, "quelle", true));
        var nur = Param.Text(p, "anschluss");
        var anschluesse = quelle.Anschluesse.Where(a => a.Fluss is "rein" or "raus"
            && (nur is null || a.Name.Equals(nur, StringComparison.OrdinalIgnoreCase))).ToList();
        if (anschluesse.Count == 0)
            throw new HlksFehler(nur is null ? $"{quelle.Kennzeichen} hat keinen gerichteten Anschluss." : $"{quelle.Kennzeichen} hat keinen gerichteten Anschluss «{nur}».",
                "Quelle ist ein Element mit Anschlüssen rein/raus, z. B. eine Pumpe (saug, druck) oder ein Lüftungsgerät.");

        var umkehren = new Dictionary<string, bool>(StringComparer.Ordinal);   // Leitung → muss umgekehrt werden
        var widerspruch = new HashSet<string>(StringComparer.Ordinal);
        var gegen = new List<string>();
        var besucht = new HashSet<string>(StringComparer.Ordinal) { quelle.Id };
        foreach (var a in anschluesse)
        {
            bool weg = a.Fluss == "raus";
            var medium = Netz.Medium(m, quelle, a) is var med && med != "*" ? med : null;
            if (m.VerbindungAn(quelle.Id, a.Name)?.Gegenueber(quelle.Id, a.Name) is not { } start) continue;
            var schlange = new Queue<(Element E, string Ein)>();
            if (m.Finde(start.Element) is { } s0 && besucht.Add(s0.Id)) schlange.Enqueue((s0, start.Anschluss));
            while (schlange.Count > 0)
            {
                var (e, ein) = schlange.Dequeue();
                if (!Netz.IstLeitungsbauteil(k.Katalog, e)) continue;   // am nächsten Gerät endet der Weg
                if (k.Katalog.IstArt(e, "Leitung"))
                {
                    bool falsch = !ein.Equals(weg ? "anfang" : "ende", StringComparison.OrdinalIgnoreCase);
                    if (umkehren.TryGetValue(e.Id, out var vorher) && vorher != falsch) widerspruch.Add(e.Id);
                    umkehren[e.Id] = falsch;
                }
                else if (e.AnschlussNamens(ein) is { Fluss: "rein" or "raus" } ea && (ea.Fluss == "rein") != weg)
                    gegen.Add(e.Kennzeichen);
                foreach (var an in e.Anschluesse)
                {
                    if (an.Name.Equals(ein, StringComparison.OrdinalIgnoreCase)) continue;
                    if (medium is not null && Netz.Medium(m, e, an) is var am && am != "*" && !am.Equals(medium, StringComparison.OrdinalIgnoreCase)) continue;
                    if (m.VerbindungAn(e.Id, an.Name)?.Gegenueber(e.Id, an.Name) is not { } g || m.Finde(g.Element) is not { } n) continue;
                    if (besucht.Add(n.Id)) schlange.Enqueue((n, g.Anschluss));
                }
            }
        }

        int n0 = 0;
        foreach (var (id, falsch) in umkehren.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (falsch && !widerspruch.Contains(id)) { Umkehren(k, m.Finde(id)!); n0++; }
        k.Hinweise.Add($"Fliessrichtung ab {quelle.Kennzeichen}: {umkehren.Count} Leitung(en) erreicht, {n0} umgekehrt.");
        if (widerspruch.Count > 0)
            k.Hinweise.Add($"Widersprüchliche Richtung (von zwei Seiten verschieden verlangt, nicht geändert): {string.Join(", ", widerspruch.Select(i => m.Finde(i)!.Kennzeichen).Order().Take(20))}.");
        if (gegen.Count > 0)
            k.Hinweise.Add($"Gegen den Fluss eingebaut (nicht geändert, Armatur drehen): {string.Join(", ", gegen.Distinct().Order().Take(20))}.");
    }

    /// <summary>Kehrt eine Leitung um: Punkte rückwärts, Grössen und Verbindungen von anfang und ende getauscht.</summary>
    public static void Umkehren(Kontext k, Element leitung)
    {
        var neu = leitung.Kopie();
        if (neu.Geometrie is { } g) g.Punkte.Reverse();
        if (neu.AnschlussNamens("anfang") is { } an && neu.AnschlussNamens("ende") is { } en)
        {
            (an.Dn, en.Dn) = (en.Dn, an.Dn);
            (an.D, en.D) = (en.D, an.D);
            (an.B, en.B) = (en.B, an.B);
            (an.H, en.H) = (en.H, an.H);
            (an.Gewinde, en.Gewinde) = (en.Gewinde, an.Gewinde);
        }
        var verb = new[] { "anfang", "ende" }.Select(x => (Name: x, V: k.Arbeit.VerbindungAn(leitung.Id, x))).Where(x => x.V is not null).ToList();
        var neuVerb = verb.Select(x => (Gegen: x.V!.Gegenueber(leitung.Id, x.Name)!, Neu: x.Name == "anfang" ? "ende" : "anfang")).ToList();
        foreach (var (_, v) in verb) k.Trennen(v!);
        k.Setzen(neu);
        foreach (var (gegen, name) in neuVerb) k.Verbinden(new Anschlusspunkt(leitung.Id, name), gegen);
    }
}
