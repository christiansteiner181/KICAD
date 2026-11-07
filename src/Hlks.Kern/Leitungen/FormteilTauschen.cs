// Bearbeitungswerkzeuge B3 (entwurf/bearbeitungswerkzeuge.md, Christian 03.11.2026): Formteil tauschen wie Revit Plus/Minus.
//   bogen_zu_tstueck (Plus): Bogen entfernt, die zulaufende Gerade wird vorübergehend durch den Achsschnittpunkt verlängert, die abgehende mit
//     dehnen_bis an ihr angeschlossen (T-Stück der Serie, Abzweigwinkel = bisheriger Umlenkwinkel); das Stück nach dem T-Stück entfällt wieder –
//     der Durchgang bleibt offen (wie Revit, zum Weiterzeichnen).
//   tstueck_zu_bogen (Minus): der offene Anschluss (bzw. der angegebene, an dem höchstens eine Gerade mit freiem Ende hängt) entfällt; bleibt
//     der Durchgang, werden die beiden Geraden direkt verbunden, sonst entsteht ein Bogen (ecke_bilden).
// ANNAHMEN (Prüfliste): Hilfsverlängerung 3 × Aussendurchmesser, mindestens 300 mm; Kreuze kennt keine Serie.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class BogenZuTstueck : IBefehl
{
    public string Name => "bogen_zu_tstueck";
    public const string Text = "Macht aus einem Bogen ein T-Stück der Produktlinie (Revit «Plus» am Formteil): die zulaufende Gerade endet am Durchgang des T-Stücks, die abgehende wird der Abzweig (wie dehnen_bis; der Umlenkwinkel muss ein Abzweigwinkel der Produktlinie sein); der andere Durchgang bleibt offen zum Weiterzeichnen. Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } = [new("bogen", "text", true, "Bogen (Kennzeichen oder Id) zwischen zwei Geraden")];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var bogen = m.Hole(Param.Text(p, "bogen", true));
        if (!kat.IstArt(bogen, "Bogen")) throw new HlksFehler($"{bogen.Kennzeichen} ist kein Bogen ({bogen.Klasse}).", "Einen Bogen wählen.");
        var seiten = bogen.Anschluesse.Select(a => (A: a.Name, G: m.VerbindungAn(bogen.Id, a.Name)?.Gegenueber(bogen.Id, a.Name))).ToList();
        if (seiten.Count != 2 || seiten.Any(s => s.G is null || m.Finde(s.G.Element) is not { } l || !kat.IstArt(l, "Leitung") || l.Geometrie is not { Punkte.Count: 2 }))
            throw new HlksFehler($"{bogen.Kennzeichen} hängt nicht zwischen zwei Geraden.", "Einen Bogen wählen, an dem beidseitig eine gerade Leitung angeschlossen ist.");
        // zulaufend = die Gerade, deren Ende am Bogen hängt
        var zu = seiten.FirstOrDefault(s => s.G!.Anschluss.Equals("ende", StringComparison.OrdinalIgnoreCase));
        var weg = seiten.FirstOrDefault(s => s.G!.Anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase));
        if (zu.G is null || weg.G is null)
            throw new HlksFehler($"Die Fliessrichtungen an {bogen.Kennzeichen} widersprechen sich.", "Fliessrichtung prüfen (Kontextregister «Fliessrichtung ab hier»).");
        var x = m.Finde(zu.G!.Element)!; var y = m.Finde(weg.G!.Element)!;
        var punkt = V3.Aus(Familien.Einfuegepunkt(bogen) ?? throw new HlksFehler($"{bogen.Kennzeichen} hat keine Lage.", "Bogen platzieren."));
        var xs = x.Geometrie!.Punkte;
        var d = (V3.Aus(xs[1]) - V3.Aus(xs[0])).Einheit();
        double aussen = Leitungsserie.VonLeitung(m, kat, x)?.GroesseVon(x)?.Aussen ?? 0;
        double stummel = Math.Max(300, 3 * aussen);

        new ElementLoeschen().Ausfuehren(k, new JsonObject { ["element"] = bogen.Id });
        var xn = m.Finde(x.Id)!.Kopie();
        xn.Geometrie!.Punkte[1] = (punkt + d * stummel).Feld();
        k.Setzen(xn);
        int vorher = k.Operationen.Count;
        try { new DehnenBis().Ausfuehren(k, new JsonObject { ["element"] = y.Id, ["bis"] = x.Id }); }
        catch (HlksFehler f) { throw new HlksFehler($"{bogen.Kennzeichen} zu T-Stück: {f.Message}", f.Vorschlag); }
        // das Hilfsstück nach dem T-Stück (zweites Teilstück der Verlängerung) entfällt – der Durchgang bleibt offen
        var neu = k.Operationen.Skip(vorher).Where(o => o.Art == "element.setzen" && o.ElementVorher is null).Select(o => m.Finde(o.Id)).OfType<Element>().ToList();
        var hilf = neu.Single(e => kat.IstArt(e, "Leitung"));
        var ts = neu.First(e => kat.IstArt(e, "TStueck"));
        var offen = m.VerbindungAn(hilf.Id, "anfang")?.Gegenueber(hilf.Id, "anfang")?.Anschluss;
        new ElementLoeschen().Ausfuehren(k, new JsonObject { ["element"] = hilf.Id });
        k.Hinweise.Insert(0, $"{bogen.Kennzeichen} durch {ts.Kennzeichen} ersetzt: {x.Kennzeichen} endet am Durchgang, {y.Kennzeichen} ist der Abzweig, {ts.Kennzeichen}.{offen} ist offen.");
    }
}

public sealed class TstueckZuBogen : IBefehl
{
    public string Name => "tstueck_zu_bogen";
    public const string Text = "Entfernt einen Abzweig eines T-Stücks (Revit «Minus» am Formteil): der Anschluss (Vorgabe: der offene; angegeben auch einer mit einer Geraden mit freiem Ende, die mit entfällt) entfällt samt T-Stück. Bleibt der Durchgang, werden die beiden Geraden direkt verbunden; bleibt ein Durchgang und der Abzweig, entsteht ein Bogen der Produktlinie (wie ecke_bilden). Ein Paket.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("tstueck", "text", true, "T-Stück (Kennzeichen oder Id)"),
        new("anschluss", "text", false, "Anschluss, der entfällt (a, b oder c); Vorgabe: der offene. An ihm darf höchstens eine Gerade mit freiem Ende hängen (sie entfällt mit)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var t = m.Hole(Param.Text(p, "tstueck", true));
        if (!kat.IstArt(t, "TStueck")) throw new HlksFehler($"{t.Kennzeichen} ist kein T-Stück ({t.Klasse}).", "Ein T-Stück wählen.");
        var seiten = t.Anschluesse.Select(a => (A: a.Name, G: m.VerbindungAn(t.Id, a.Name)?.Gegenueber(t.Id, a.Name))).ToList();
        // Stummel: Gerade, deren anderes Ende frei ist
        Element? Stummel(Anschlusspunkt? g)
        {
            if (g is null || m.Finde(g.Element) is not { } l || !kat.IstArt(l, "Leitung")) return null;
            var anderes = g.Anschluss.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? "ende" : "anfang";
            return m.VerbindungAn(l.Id, anderes) is null ? l : null;
        }
        (string A, Anschlusspunkt? G) weg;
        if (Param.Text(p, "anschluss") is { } an)
        {
            weg = seiten.FirstOrDefault(s => s.A.Equals(an, StringComparison.OrdinalIgnoreCase));
            if (weg.A is null) throw new HlksFehler($"{t.Kennzeichen} hat keinen Anschluss «{an}».", "Anschlüsse: " + string.Join(", ", seiten.Select(s => s.A)));
            if (weg.G is not null && Stummel(weg.G) is null)
                throw new HlksFehler($"An {t.Kennzeichen}.{weg.A} hängt mehr als ein Stummel – entfernt wird nur ein offener Abzweig.", "Zuerst die Leitungen dort löschen bzw. trennen.");
        }
        else
        {
            var offen = seiten.Where(s => s.G is null).ToList();
            if (offen.Count != 1) throw new HlksFehler(offen.Count == 0 ? $"{t.Kennzeichen} hat keinen offenen Anschluss." : $"{t.Kennzeichen} hat mehrere offene Anschlüsse.",
                "anschluss angeben (a, b oder c) – eine Gerade mit freiem Ende daran entfällt mit.");
            weg = offen[0];
        }
        var bleiben = seiten.Where(s => s.A != weg.A).ToList();
        if (bleiben.Any(s => s.G is null || m.Finde(s.G.Element) is not { } l || !kat.IstArt(l, "Leitung") || l.Geometrie is not { Punkte.Count: 2 }))
            throw new HlksFehler($"An {t.Kennzeichen} hängen nicht an beiden übrigen Anschlüssen gerade Leitungen.", "Formteile direkt am T-Stück zuerst durch Geraden ersetzen.");
        var mitte = V3.Aus(Familien.Einfuegepunkt(t) ?? throw new HlksFehler($"{t.Kennzeichen} hat keine Lage.", "T-Stück platzieren."));
        var stummel = weg.G is null ? null : Stummel(weg.G);
        var (l1, n1) = (m.Finde(bleiben[0].G!.Element)!, bleiben[0].G!.Anschluss);
        var (l2, n2) = (m.Finde(bleiben[1].G!.Element)!, bleiben[1].G!.Anschluss);

        if (stummel is not null) new ElementLoeschen().Ausfuehren(k, new JsonObject { ["element"] = stummel.Id });
        new ElementLoeschen().Ausfuehren(k, new JsonObject { ["element"] = t.Id });
        bool durchgang = bleiben.All(s => s.A is "a" or "b");
        if (durchgang)
        {
            foreach (var (l, n) in new[] { (l1, n1), (l2, n2) })
            {
                var neu = m.Finde(l.Id)!.Kopie();
                neu.Geometrie!.Punkte[n.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? 0 : 1] = mitte.Feld();
                k.Setzen(neu);
            }
            Verbinden.Ausfuehren(k, m.Finde(l1.Id)!, n1, m.Finde(l2.Id)!, n2);
            k.Hinweise.Insert(0, $"Abzweig {t.Kennzeichen}.{weg.A} entfernt{(stummel is null ? "" : $" (samt {stummel.Kennzeichen})")}: {l1.Kennzeichen} und {l2.Kennzeichen} direkt verbunden.");
            return;
        }
        double[] Fern(Element l, string n) { var pts = m.Finde(l.Id)!.Geometrie!.Punkte; return n.Equals("anfang", StringComparison.OrdinalIgnoreCase) ? pts[1] : pts[0]; }
        var f1 = Fern(l1, n1); var f2 = Fern(l2, n2);
        try
        {
            new EckeBilden().Ausfuehren(k, new JsonObject
            {
                ["a"] = l1.Id, ["b"] = l2.Id, ["bleibt_a"] = new JsonArray(f1[0], f1[1]), ["bleibt_b"] = new JsonArray(f2[0], f2[1]),
            });
        }
        catch (HlksFehler f) { throw new HlksFehler($"{t.Kennzeichen} zu Bogen: {f.Message}", f.Vorschlag); }
        k.Hinweise.Insert(0, $"Abzweig {t.Kennzeichen}.{weg.A} entfernt{(stummel is null ? "" : $" (samt {stummel.Kennzeichen})")}: Bogen zwischen {l1.Kennzeichen} und {l2.Kennzeichen}.");
    }
}
