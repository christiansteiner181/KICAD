// Grafik der ansichtsgebundenen Beschriftung (Detaillinie, Füllbereich, Textnotiz, Bemassung, Kote, Symbol) und das
// Schnittsymbol der Schnitt-/Ansichtslinien in Grundrissen (Modul 1a, Teil B3, siehe entwurf/modul-1a.md).
//
// Aufteilung: diese Datei = Verteiler, Detaillinie, Füllbereich, Textnotiz; BeschriftungMass.cs = Bemassung und Kote;
// BeschriftungSymbole.cs = Nordpfeil und Schnittsymbol; BeschriftungHilfen.cs = Vektor- und Primitivhilfen.
//
// Koordinaten: Modell-mm der Ansicht (Grundriss x/y, Schnitt/Ansicht u/v). Strichstärken und Schriftgrössen in
// Papier-mm; Papiermasse für Abstände rechnet ctx.AufModell(mm) um. Alle Masse, die nicht aus SIA 400:2000 stammen,
// sind im Code mit «Annahme» gekennzeichnet (Prüfliste Christian).

namespace Hlks.Kern;

public sealed class BeschriftungGrafik : IGrafikquelle
{
    public string Name => "beschriftung";

    /// <summary>Vorgabe-Schrifthöhe der Textnotiz in Papier-mm (Katalog: Vorgabe 2.5).</summary>
    public const double TextGroesse = 2.5;
    /// <summary>Annahme: Vorgabe-Grau eines Füllbereichs «voll» ohne Farbe (Katalog: «Vorgabe grau»).</summary>
    public const string VorgabeGrau = "#c8c8c8";

    public bool Zustaendig(Katalog k, Element e) =>
        (k.Klassen.TryGetValue(e.Klasse, out var d) && d.BrauchtAnsicht) || e.Klasse == "Ansicht";

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e)
    {
        if (e.Geometrie is null || e.Geometrie.Punkte.Count == 0) return [];
        return e.Klasse switch
        {
            "Detaillinie" => Detaillinie(e),
            "Fuellbereich" => Fuellbereich(e),
            "Textnotiz" => Textnotiz(e),
            "Bemassung" => BeschriftungMass.Bemassung(ctx, e),
            "Kote" => BeschriftungMass.Kote(ctx, e),
            "Symbol" => BeschriftungSymbole.Symbol(ctx, e),
            // Schnittsymbol: nur in Grundrissen und nie in der Ansicht selbst
            "Ansicht" when ctx.Art == "grundriss" && e.Id != ctx.Ansicht.Id => BeschriftungSymbole.Schnittsymbol(ctx, e),
            _ => [],
        };
    }

    /// <summary>Beschriftung ist 2D.</summary>
    public Koerper? Koerper(Projekt p, Element e) => null;

    // ------------------------------------------------------------------------------------------ Detaillinie

    static IEnumerable<Grafikteil> Detaillinie(Element e)
    {
        var pts = BeschriftungHilfen.Punkte2D(e.Geometrie!);
        if (pts.Count < 2) return [];
        var stil = new Stil { Stift = e.Text("stift") ?? Stifte.Duenn, Linie = e.Text("linienart") ?? "voll" };
        return [new Grafikteil
        {
            Id = e.Id, Ebene = "beschriftung",
            Primitive = [new Primitiv { Art = "polylinie", Punkte = pts, Stil = stil }],
            Fangpunkte = pts.Select(p => (double[])p.Clone()).ToList(),
        }];
    }

    // ------------------------------------------------------------------------------------------ Füllbereich

    static IEnumerable<Grafikteil> Fuellbereich(Element e)
    {
        var pts = BeschriftungHilfen.Punkte2D(e.Geometrie!);
        if (pts.Count > 3 && pts[0][0] == pts[^1][0] && pts[0][1] == pts[^1][1]) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) return [];
        var muster = e.Text("muster");
        var stil = new Stil { Stift = Grafik.Bool(e, "rand") ? Stifte.Duenn : null };
        if (muster is null or "voll" || Schraffuren.Definition(muster) is null)
            stil.Fuellung = e.Text("farbe") is { Length: > 0 } f ? f : VorgabeGrau;
        else
            stil.Muster = muster;
        return [new Grafikteil
        {
            Id = e.Id, Ebene = "fuellung",   // unter der übrigen Beschriftung, damit volle Füllungen keine Linien verdecken
            Primitive = [new Primitiv { Art = "polygon", Punkte = pts, Stil = stil }],
            Fangpunkte = pts.Select(p => (double[])p.Clone()).ToList(),
        }];
    }

    // ------------------------------------------------------------------------------------------ Textnotiz

    static IEnumerable<Grafikteil> Textnotiz(Element e)
    {
        var inhalt = e.Text("inhalt");
        if (string.IsNullOrEmpty(inhalt)) return [];
        var p = e.Geometrie!.Punkte[0];
        var text = BeschriftungHilfen.Text([p[0], p[1]], inhalt, e.Zahl("groesse") is double g and > 0 ? g : TextGroesse,
            e.Text("ausrichtung") ?? "links", e.Zahl("winkel") ?? 0);
        // N9 (Annahme): eine gedrehte Textnotiz gilt als an einem Bauteil ausgerichtet und dreht mit der Ansicht; Winkel 0 bleibt waagrecht
        if ((e.Zahl("winkel") ?? 0) != 0) text.Mitdrehend();
        return [new Grafikteil
        {
            Id = e.Id, Ebene = "beschriftung",
            Primitive = [text],
            Fangpunkte = [[p[0], p[1]]],
        }];
    }
}
