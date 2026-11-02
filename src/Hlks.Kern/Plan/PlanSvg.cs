// BIM to field (entwurf/bim-to-field.md, Stufe 1): Plangrafik einer Ansicht als SVG für das Export-Paket der Baustelle. Gleiche Primitive wie
// Bildschirm und PDF (Grafiktypen.cs), gezeichnet wie PdfZeichner: Ebenen von unten nach oben, Strichstärken und Schriften in Papier-mm mal
// Massstab, Füllung «schnitt» in der Linienfarbe, «papier» weiss, Primitive «NurBildschirm» weggelassen.
// Vereinfachungen gegenüber dem PDF: keine Schraffurmuster (nur die Füllung) und keine Rasterbilder von Unterlagen (Art «bild»).
// Jedes Grafikteil mit Element-Id wird eine Gruppe mit data-id – der Betrachter zeigt beim Antippen die Elementinfo.
// Koordinaten: SVG-Einheit = Modell-mm der Ansicht (Schema: Papier-mm), y gespiegelt; eine gedrehte Ansicht (N9) dreht die ganze Gruppe.

using System.Globalization;
using System.Text;

namespace Hlks.Kern;

public static class PlanSvg
{
    static string Z(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    public static string Svg(Plangrafik g)
    {
        var b = g.Begrenzung ?? [0, 0, 1000, 1000];
        var rand = Math.Max(b[2] - b[0], b[3] - b[1]) * 0.02;
        double x0 = b[0] - rand, y0 = b[1] - rand, w = b[2] - b[0] + 2 * rand, h = b[3] - b[1] + 2 * rand;
        var m = g.Massstab <= 0 ? 1 : g.Massstab;
        var sb = new StringBuilder();
        // y nach oben: viewBox über −y, alle Punkte mit −y
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{Z(x0)} {Z(-(y0 + h))} {Z(w)} {Z(h)}\" data-ansicht=\"{Esc(g.Ansicht)}\" data-art=\"{g.Art}\" data-massstab=\"{Z(m)}\">");
        var mitte = $"{Z(x0 + w / 2)} {Z(-(y0 + h / 2))}";
        sb.Append(g.Drehung != 0 ? $"<g transform=\"rotate({Z(-g.Drehung)} {mitte})\">" : "<g>");
        foreach (var t in g.Teile.OrderBy(t => Ebenen.Rang(t.Ebene)))
        {
            var prims = t.Primitive.Where(p => !p.Stil.NurBildschirm && p.Art != "bild").ToList();
            if (prims.Count == 0) continue;
            sb.Append(t.Id is { } id ? $"<g data-id=\"{Esc(id)}\" data-ebene=\"{t.Ebene}\">" : $"<g data-ebene=\"{t.Ebene}\">");
            foreach (var p in prims) Primitiv(sb, p, m);
            sb.Append("</g>");
        }
        sb.Append("</g></svg>");
        return sb.ToString();
    }

    static string Pkt(IEnumerable<double[]> pkt) => string.Join(" ", pkt.Select(q => $"{Z(q[0])},{Z(-q[1])}"));

    static string Strich(Stil s, double m)
    {
        if (s.Stift is null) return " stroke=\"none\"";
        var farbe = s.Farbe ?? "#000000";
        var dash = s.Linie switch
        {
            "strich" => $" stroke-dasharray=\"{Z(2 * m)} {Z(1 * m)}\"",
            "strichpunkt" => $" stroke-dasharray=\"{Z(3 * m)} {Z(0.8 * m)} {Z(0.3 * m)} {Z(0.8 * m)}\"",
            "punkt" => $" stroke-dasharray=\"{Z(0.3 * m)} {Z(0.8 * m)}\"",
            _ => "",
        };
        var deck = s.Fuellung is null && s.Deckkraft is double d ? $" stroke-opacity=\"{Z(d)}\"" : "";
        return $" stroke=\"{farbe}\" stroke-width=\"{Z(Stifte.Mm(s.Stift) * m)}\"{dash}{deck}";
    }

    static string Fuellung(Stil s)
    {
        var f = s.Fuellung switch { null => "none", "schnitt" => s.Farbe ?? "#000000", "papier" => "#ffffff", var x => x };
        return f == "none" ? " fill=\"none\"" : $" fill=\"{f}\"" + (s.Deckkraft is double d ? $" fill-opacity=\"{Z(d)}\"" : "");
    }

    static void Primitiv(StringBuilder sb, Primitiv p, double m)
    {
        var s = p.Stil;
        switch (p.Art)
        {
            case "linie" or "polylinie" when p.Punkte.Count >= 2:
                sb.Append($"<polyline points=\"{Pkt(p.Punkte)}\" fill=\"none\"{Strich(s, m)} stroke-linejoin=\"miter\"/>");
                break;
            case "polygon" when p.Punkte.Count >= 3:
                if (p.Loecher is { Count: > 0 } l)
                {
                    var d = string.Concat(new[] { p.Punkte }.Concat(l).Select(r => "M" + Pkt(r).Replace(" ", " L") + "Z"));
                    sb.Append($"<path d=\"{d}\" fill-rule=\"evenodd\"{Fuellung(s)}{Strich(s, m)}/>");
                }
                else sb.Append($"<polygon points=\"{Pkt(p.Punkte)}\"{Fuellung(s)}{Strich(s, m)}/>");
                break;
            case "kreis" when p.Punkte.Count >= 1 && p.Radius is double r:
                sb.Append($"<circle cx=\"{Z(p.Punkte[0][0])}\" cy=\"{Z(-p.Punkte[0][1])}\" r=\"{Z(r)}\"{Fuellung(s)}{Strich(s, m)}/>");
                break;
            case "bogen" when p.Punkte.Count >= 1 && p.Radius is double rb && p.Von is double von && p.Bis is double bis:
            {
                var c = p.Punkte[0];
                double a0 = von * Math.PI / 180, a1 = bis * Math.PI / 180;
                var sweep = ((bis - von) % 360 + 360) % 360;
                // Gegenuhrzeigersinn im Modell = Uhrzeigersinn im gespiegelten SVG (sweep-flag 0)
                sb.Append($"<path d=\"M{Z(c[0] + rb * Math.Cos(a0))},{Z(-(c[1] + rb * Math.Sin(a0)))} A{Z(rb)},{Z(rb)} 0 {(sweep > 180 ? 1 : 0)} 0 " +
                          $"{Z(c[0] + rb * Math.Cos(a1))},{Z(-(c[1] + rb * Math.Sin(a1)))}\"{Fuellung(s)}{Strich(s, m)}/>");
                break;
            }
            case "text" when p.Punkte.Count >= 1 && !string.IsNullOrEmpty(p.Text):
            {
                var q = p.Punkte[0];
                var gr = (p.Groesse ?? 2.5) * m;
                var anker = p.Ausrichtung switch { "mitte" => "middle", "rechts" => "end", _ => "start" };
                var dreh = p.Winkel is double wk && wk != 0 ? $" transform=\"rotate({Z(-wk)} {Z(q[0])} {Z(-q[1])})\"" : "";
                var zeilen = p.Text.Split('\n');
                sb.Append($"<text x=\"{Z(q[0])}\" y=\"{Z(-q[1])}\" font-family=\"Arial, sans-serif\" font-size=\"{Z(gr)}\" text-anchor=\"{anker}\"" +
                          $"{(p.Fett == true ? " font-weight=\"bold\"" : "")} fill=\"{s.Farbe ?? "#000000"}\"{dreh}>");
                for (var i = 0; i < zeilen.Length; i++)
                    sb.Append(i == 0 ? Esc(zeilen[i]) : $"<tspan x=\"{Z(q[0])}\" dy=\"{Z(gr * 1.2)}\">{Esc(zeilen[i])}</tspan>");
                sb.Append("</text>");
                break;
            }
        }
    }
}
