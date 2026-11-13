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

/// <summary>fliessrichtung_umkehren (Christian 09.11.2026: «bei abläufen muss die fluss und gefällsrichtung angezeigt und invertiert werden können»):
/// kehrt die Fliessrichtung des Leitungszugs um (Geraden über Bögen und Reduktionen bis zum nächsten T-Stück bzw. Gerät) und legt ein vorhandenes Gefälle
/// in die neue Fliessrichtung (leitung_gefaelle mit demselben Gefälle; ist ein Ende an ein Element mit Lage gebunden, bleibt dessen Höhe).</summary>
public sealed class FliessrichtungUmkehren : IBefehl
{
    public const string Text = "Kehrt Fliess- und Gefällsrichtung eines Leitungszugs um (Abläufe, Christian 09.11.2026): Punkte, Grössen und Verbindungen von anfang und ende " +
        "jeder Geraden werden getauscht (Lage bleibt); hatte der Zug ein Gefälle, wird es mit demselben Wert in die neue Fliessrichtung gelegt (Bögen wandern mit; " +
        "ein an ein Element mit Lage gebundenes Ende behält seine Höhe). zug = false: nur die genannte Gerade, ohne Gefälle. Ein Paket.";
    public string Name => "fliessrichtung_umkehren";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, "Eine Gerade des Zugs (Kennzeichen oder Id)"),
        new("zug", "bool", false, "false: nur diese Gerade umkehren (Vorgabe true: der ganze Leitungszug samt Gefälle)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var e = m.Hole(Param.Text(p, "leitung", true));
        if (!k.Katalog.IstArt(e, "Leitung")) throw new HlksFehler($"{e.Kennzeichen} ist keine Leitung.", "Eine Gerade (Rohr) angeben.");
        bool zug = p["zug"] is null || Param.Bool(p, "zug");
        var glieder = zug ? Leitungszug.Finden(m, k.Katalog, e).Glieder.Select(g => g.Element).Where(x => k.Katalog.IstArt(x, "Leitung")).ToList() : [e];
        // Gefälle vor dem Umkehren: Eigenschaft gefaelle, sonst gemessen am ersten flachen Stück (Annahme: bis 15 % Neigung gilt als Gefälle)
        double? gef = glieder.Select(x => x.Zahl("gefaelle")).FirstOrDefault(x => x > 0);
        if (gef is null)
            foreach (var x in glieder)
            {
                var pts = x.Geometrie?.Punkte;
                if (pts is not { Count: >= 2 }) continue;
                for (int i = 1; i < pts.Count && gef is null; i++)
                {
                    double h = Math.Sqrt(Math.Pow(pts[i][0] - pts[i - 1][0], 2) + Math.Pow(pts[i][1] - pts[i - 1][1], 2));
                    double dz = Math.Abs((pts[i].Length > 2 ? pts[i][2] : 0) - (pts[i - 1].Length > 2 ? pts[i - 1][2] : 0));
                    if (h > 1 && dz > 0.5 && dz <= 0.15 * h) gef = Math.Round(dz / h * 100, 2);
                }
                if (gef is not null) break;
            }
        double ZMax() => glieder.SelectMany(x => m.Finde(x.Id)?.Geometrie?.Punkte ?? []).Select(q => q.Length > 2 ? q[2] : 0).DefaultIfEmpty(0).Max();
        double zHochVorher = ZMax();
        foreach (var x in glieder) FliessrichtungAusQuelle.Umkehren(k, m.Finde(x.Id)!);
        k.Hinweise.Add($"Fliessrichtung umgekehrt: {string.Join(", ", glieder.Select(x => x.Kennzeichen))}.");
        if (zug && gef is double g && g > 0)
        {
            new LeitungGefaelle().Ausfuehren(k, new JsonObject { ["leitung"] = e.Id, ["gefaelle"] = g, ["zug"] = true });
            k.Hinweise.Add($"Gefälle {g:0.##} % in die neue Fliessrichtung gelegt.");
            // Hoch- und Tiefpunkt tauschen (Entscheid Christian 13.11.2026, Frage 2): der neue Anfang liegt auf der Höhe des alten
            // Hochpunkts, die Leitung kippt statt abzusinken. Geht das nicht (gebundene Enden), bleibt die Lage mit Hinweis.
            double dz = zHochVorher - ZMax();
            if (Math.Abs(dz) > Anschlusslagen.AbstandMax)
                try
                {
                    new LeitungHoehe().Ausfuehren(k, new JsonObject { ["leitung"] = e.Id, ["dz"] = Math.Round(dz, 3), ["zug"] = true });
                    k.Hinweise.Add($"Hoch- und Tiefpunkt getauscht: Hochpunkt wieder auf z = {zHochVorher:0.#} mm.");
                }
                catch (HlksFehler f)
                {
                    k.Hinweise.Add($"Hoch- und Tiefpunkt nicht getauscht ({f.Message}) – der Zug liegt {dz:0.#} mm tiefer; Höhe mit leitung_hoehe bzw. Gefälle neu setzen.");
                }
        }
    }
}
