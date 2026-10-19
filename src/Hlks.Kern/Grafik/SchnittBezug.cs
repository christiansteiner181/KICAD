// Schnitt und Ansicht (Modul 1a, C1) – Bezugslinien: Geschosskoten und Achsen.
// Aus der Spezifikation (entwurf/modul-1a.md): Geschosskoten am Rand; Achsen, welche die Linie kreuzen, als senkrechte
// Achslinie mit Bezeichnung; Achse strichpunktiert dünn mit Kreis Ø 8 mm Papier (wie im Grundriss); Koten in m, Format
// «+2.85», «±0.00», «−2.60» (SIA 400:2000 Ziff. B.5.4, BeschriftungMass.KoteText).
// ANNAHMEN (Gestaltung, Prüfliste Christian), alle in Papier-mm:
//   Geschoss: Strich-Linie dünn auf der Kote über die ganze Breite der Modellgrafik, links 25 mm und rechts 5 mm darüber
//   hinaus; links Kotensymbol (Dreieck 3 × 2.5 mm, Spitze auf der Linie, wie die Kote der Beschriftung) und Text
//   «±0.00 EG» (Kote, dann Kennzeichen des Geschosses), Schrift 2.0 mm, über der Linie.
//   Achse: Linie über die ganze Höhe der Modellgrafik und aller Geschosskoten, unten und oben 5 mm darüber hinaus; Kreis
//   oben, Bezeichnung 3.5 mm in der Kreismitte (wie im Grundriss).
//   Eine Achse erscheint, wenn sie die (unbegrenzte) Schnittlinie kreuzt – wie die Modellgrafik, die entlang u nicht
//   begrenzt ist. Ohne Modellgrafik reicht die Breite über die Schnittlinie, die Höhe über die Geschosse bzw. 0…3000 mm.
// Sichtbarkeit sinngemäss wie Grafik.Sichtbar: ausgeblendete Klassen und Gewerke-Filter der Ansicht gelten.

namespace Hlks.Kern;

internal static class Schnittbezug
{
    const double AchsKreis = 8.0;          // Spezifikation (wie Grundriss)
    const double AchsSchrift = 3.5;        // Annahme wie Grundriss (ArchitekturGrafik)
    const double AchsUeberstand = 5.0;     // Annahme
    const double LinksUeberstand = 25.0;   // Annahme
    const double RechtsUeberstand = 5.0;   // Annahme
    const double KoteBreite = 3.0;         // Annahme wie BeschriftungMass.Kote
    const double KoteHoehe = 2.5;          // Annahme wie BeschriftungMass.Kote
    const double KoteSchrift = 2.0;        // Annahme wie BeschriftungMass.Kote

    public static IEnumerable<Grafikteil> Erzeugen(GrafikKontext ctx, Schnittraum raum, IReadOnlyList<Grafikteil> modell)
    {
        var k = ctx.Katalog;
        var geschosse = ctx.Modell.Elemente
            .Where(e => k.IstArt(e, "Geschoss") && Gezeigt(ctx, e) && e.Zahl("kote") is not null)
            .OrderBy(e => e.Zahl("kote")).ThenBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var achsen = new List<(Element E, double U)>();
        foreach (var e in ctx.Modell.Elemente)
            if (k.IstArt(e, "Achse") && Grafik.Sichtbar(ctx, e) && Kreuzung(raum, e) is double u) achsen.Add((e, u));
        if (geschosse.Count == 0 && achsen.Count == 0) return [];

        double u0, u1, v0, v1;
        if (Grafik.Begrenzung(modell) is { } b) (u0, v0, u1, v1) = (b[0], b[1], b[2], b[3]);
        else
        {
            (u0, u1) = (0, raum.Laenge);
            if (geschosse.Count > 0)
            {
                v0 = geschosse.Min(g => g.Zahl("kote")!.Value);
                v1 = geschosse.Max(g => g.Zahl("kote")!.Value + (g.Zahl("hoehe") ?? 3000));
            }
            else (v0, v1) = (0, 3000);
        }
        foreach (var (_, u) in achsen) { u0 = Math.Min(u0, u); u1 = Math.Max(u1, u); }
        foreach (var g in geschosse) { v0 = Math.Min(v0, g.Zahl("kote")!.Value); v1 = Math.Max(v1, g.Zahl("kote")!.Value); }

        var teile = new List<Grafikteil>();
        foreach (var g in geschosse) teile.Add(Geschoss(ctx, g, u0, u1));
        foreach (var (e, u) in achsen) teile.Add(Achse(ctx, e, u, v0, v1));
        return teile;
    }

    /// <summary>Geschoss: ausgeblendete Klassen und Gewerke-Filter wie Grafik.Sichtbar (Geschosse haben keine Geometrie).</summary>
    static bool Gezeigt(GrafikKontext ctx, Element e)
    {
        if (!ctx.Katalog.Klassen.TryGetValue(e.Klasse, out var d)) return false;
        if (ctx.KlassenAus.Contains(e.Klasse) || d.Linie.Any(ctx.KlassenAus.Contains)) return false;
        return ctx.Gewerke is null || d.Gewerk is not { } gw || gw == "allgemein" || ctx.Gewerke.Contains(gw);
    }

    /// <summary>u, wo die Achse (Strecke im Grundriss) die Schnittlinie kreuzt; null, wenn sie sie nicht kreuzt oder parallel liegt.</summary>
    static double? Kreuzung(Schnittraum raum, Element achse)
    {
        var g = achse.Geometrie;
        if (g is null || g.Punkte.Count < 2) return null;
        var a = g.Punkte[0]; var b = g.Punkte[^1];
        double wa = raum.W(a[0], a[1]), wb = raum.W(b[0], b[1]);
        if (Math.Abs(wa - wb) < 1e-9 || (wa > 0 && wb > 0) || (wa < 0 && wb < 0)) return null;
        double t = wa / (wa - wb), ua = raum.U(a[0], a[1]), ub = raum.U(b[0], b[1]);
        return ua + t * (ub - ua);
    }

    static Grafikteil Geschoss(GrafikKontext ctx, Element g, double u0, double u1)
    {
        double v = g.Zahl("kote")!.Value;
        double[] anfang = [u0 - ctx.AufModell(LinksUeberstand), v], ende = [u1 + ctx.AufModell(RechtsUeberstand), v];
        double b = ctx.AufModell(KoteBreite) / 2, h = ctx.AufModell(KoteHoehe);
        double[] spitze = [anfang[0] + b, v];
        return new Grafikteil
        {
            Id = g.Id, Ebene = "achse",
            Primitive =
            [
                BeschriftungHilfen.Linie(anfang, ende, Stifte.Duenn, "strich"),
                BeschriftungHilfen.Polygon([spitze, [spitze[0] + b, v + h], [spitze[0] - b, v + h]], Stifte.Duenn),
                BeschriftungHilfen.Text([spitze[0] + b + ctx.AufModell(0.8), v + h - ctx.AufModell(KoteSchrift * 0.7)],
                    BeschriftungMass.KoteText(v) + " " + g.Kennzeichen, KoteSchrift, "links"),
            ],
            Fangpunkte = [anfang, ende],
            Fangkanten = [[anfang, ende]],
        };
    }

    static Grafikteil Achse(GrafikKontext ctx, Element e, double u, double v0, double v1)
    {
        double ue = ctx.AufModell(AchsUeberstand), r = ctx.AufModell(AchsKreis / 2), fs = ctx.AufModell(AchsSchrift);
        double[] unten = [u, v0 - ue], oben = [u, v1 + ue], mitte = [u, v1 + ue + r];
        return new Grafikteil
        {
            Id = e.Id, Ebene = "achse",
            Primitive =
            [
                BeschriftungHilfen.Linie(unten, oben, Stifte.Duenn, "strichpunkt"),
                new Primitiv { Art = "kreis", Punkte = [mitte], Radius = r, Stil = new Stil { Stift = Stifte.Duenn } },
                BeschriftungHilfen.Text([u, mitte[1] - 0.35 * fs], e.Text("bezeichnung") ?? e.Kennzeichen, AchsSchrift, "mitte"),
            ],
            Fangpunkte = [unten, oben],
            Fangkanten = [[unten, oben]],
        };
    }
}
