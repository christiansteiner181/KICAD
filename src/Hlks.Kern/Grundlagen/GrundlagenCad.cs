// Grafikquelle für DWG/DXF-Unterlagen (Modul 1b, B3): die gelesene Zeichnung (CadZeichnung, Ableitung «.dwg.json»)
// als Plangrafik-Primitive im Modell. Alles Stift dünn, Farbe grau #8a8a8a, Ebene «grundlage» (ganz unten), Deckkraft
// der Unterlage; Layer aus «ebenenAus» fehlen. Textgrössen: Zeichnungshöhe × Faktor = Modell-mm, durch den Massstab
// der Ansicht = Papier-mm.

namespace Hlks.Kern;

public sealed class GrundlagenCad : IGrundlagenArt
{
    public const string Grau = "#8a8a8a";

    public bool Fuer(string art) => art is "dwg" or "dxf";

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage)
    {
        CadZeichnung z;
        try { z = CadZeichnung.Laden(ctx.Projekt, unterlage); }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { return []; }   // fehlende oder unlesbare Datei: Grundriss bleibt zeichenbar
        return [new Grafikteil { Id = unterlage.Id, Ebene = "grundlage", Primitive = Primitive(z, UnterlagenLage.Lesen(unterlage), ctx.Massstab, unterlage) }];
    }

    public Koerper? Koerper(Projekt p, Element unterlage) => null;

    /// <summary>Die Primitive der Zeichnung im Modell (ohne ausgeblendete Layer).</summary>
    public static List<Primitiv> Primitive(CadZeichnung z, UnterlagenLage lage, double massstab, Element unterlage)
    {
        var aus = Grafik.Liste(unterlage, "ebenenAus").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var verborgen = z.Layer.Select(l => aus.Contains(l)).ToArray();
        var stil = new Stil { Stift = Stifte.Duenn, Farbe = Grau, Deckkraft = unterlage.Zahl("deckkraft") ?? 0.5 };
        var liste = new List<Primitiv>(z.Primitive.Count);
        double f = lage.Faktor;
        foreach (var p in z.Primitive)
        {
            if (verborgen[p.Layer]) continue;
            var w = p.W;
            switch (p.Art)
            {
                case 'l' or 'p' or 'g':
                {
                    var punkte = new List<double[]>(w.Length / 2 + 1);
                    for (int i = 0; i + 1 < w.Length; i += 2) { var (x, y) = lage.Auf(w[i], w[i + 1]); punkte.Add([x, y]); }
                    if (p.Art == 'g') punkte.Add(punkte[0]);
                    liste.Add(new Primitiv { Art = p.Art == 'l' ? "linie" : "polylinie", Punkte = punkte, Stil = stil });
                    break;
                }
                case 'b':
                {
                    var (x, y) = lage.Auf(w[0], w[1]);
                    liste.Add(new Primitiv { Art = "bogen", Punkte = [[x, y]], Radius = w[2] * f, Von = Winkel(w[3] + lage.Drehung), Bis = Winkel(w[4] + lage.Drehung), Stil = stil });
                    break;
                }
                case 'k':
                {
                    var (x, y) = lage.Auf(w[0], w[1]);
                    liste.Add(new Primitiv { Art = "kreis", Punkte = [[x, y]], Radius = w[2] * f, Stil = stil });
                    break;
                }
                case 't':
                {
                    var (x, y) = lage.Auf(w[0], w[1]);
                    double papierMm = w[2] * f / massstab;
                    liste.Add(new Primitiv
                    {
                        Art = "text", Punkte = [[x, y]], Text = p.Text, Groesse = papierMm,
                        Winkel = Winkel(w[3] + lage.Drehung) is var wk && wk != 0 ? wk : null,
                        Ausrichtung = w[4] switch { 1 => "mitte", 2 => "rechts", _ => "links" }, Stil = stil,
                        Mitdrehen = true,   // N9: Text der Unterlage dreht mit der Ansicht
                    });
                    break;
                }
            }
        }
        return liste;
    }

    static double Winkel(double grad)
    {
        grad %= 360;
        return grad < 0 ? grad + 360 : grad;
    }
}
