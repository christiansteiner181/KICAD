// Rechnung für Fenster (Modul 1c, B2): Uw = (Ag·Ug + Af·Uf + lg·ψg)/(Ag + Af).
//   A  = breite · hoehe (lichte Masse in m²)
//   Af = rahmenanteil · A,  Ag = A − Af
//   lg = Umfang der Verglasung = 2·(b + h)·√(1 − rahmenanteil)
// ANNAHME: Die Verglasung ist dem Fenster geometrisch ähnlich (gleiches Seitenverhältnis, Rahmen umlaufend gleich
// breit). Bei Sprossen, Pfosten oder ungleichen Rahmenbreiten weicht der Glasrandverbund ab; dann Uw ausdrücklich
// aus dem Datenblatt setzen (fenster_kennwerte uw mit Quelle) – das geht vor der Rechnung.
// Ug, Uf, ψg und Rahmenanteil ohne Quelle gelten als fehlend. Ein ausdrückliches uw (Herkunft nicht «berechnung»,
// z. B. Datenblatt oder IFC) wird nie überschrieben.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public sealed class FensterErgebnis
{
    public required Element Fenster { get; init; }
    public double? A { get; set; }
    public double? Af { get; set; }
    public double? Ag { get; set; }
    public double? Lg { get; set; }
    public double? UwGerechnet { get; set; }
    public string? UwQuelle { get; set; }
    public double? UwAusdruecklich { get; set; }
    public string? UwAusdruecklichQuelle { get; set; }
    public double? Uw => UwAusdruecklich ?? UwGerechnet;
    /// <summary>Was für die Rechnung von Uw fehlt.</summary>
    public List<string> Fehlt { get; } = [];
}

public static class FensterRechnung
{
    /// <summary>Eingaben der Uw-Rechnung (Eigenschaften des Fensters).</summary>
    public static readonly string[] Eingaben = ["ug", "uf", "psiG", "rahmenanteil"];

    public static FensterErgebnis Rechnen(Element f)
    {
        var erg = new FensterErgebnis { Fenster = f };
        var uw = f.Eigenschaft("uw");
        if (uw?.AlsZahl() is double ue && uw.Herkunft.Art != "berechnung")
        {
            erg.UwAusdruecklich = ue;
            erg.UwAusdruecklichQuelle = Quelle(uw);
        }

        double? breite = f.Zahl("breite"), hoehe = f.Zahl("hoehe");
        if (breite is not > 0) erg.Fehlt.Add("breite fehlt.");
        if (hoehe is not > 0) erg.Fehlt.Add("hoehe fehlt.");
        double? Eingabe(string name, string text)
        {
            var (w, hat) = ZahlMitQuelle(f, name);
            if (w is null) { erg.Fehlt.Add($"{text} fehlt (fenster_kennwerte mit {name} und Quelle)."); return null; }
            if (!hat) { erg.Fehlt.Add($"{text} hat keine Quelle und gilt nicht (fenster_kennwerte mit {name} und Quelle)."); return null; }
            return w;
        }
        var ug = Eingabe("ug", "Ug");
        var uf = Eingabe("uf", "Uf");
        var psi = Eingabe("psiG", "ψg");
        var rahmen = Eingabe("rahmenanteil", "Rahmenanteil");
        if (rahmen is double rr && (rr < 0 || rr > 1))
        {
            erg.Fehlt.Add("Rahmenanteil liegt nicht zwischen 0 und 1.");
            rahmen = null;
        }
        if (breite is > 0 && hoehe is > 0 && ug is double g && uf is double r && psi is double p && rahmen is double ra)
        {
            double b = breite.Value / 1000.0, h = hoehe.Value / 1000.0;
            double a = b * h, af = ra * a, ag = a - af;
            double lg = 2 * (b + h) * Math.Sqrt(1 - ra);
            erg.A = a; erg.Af = af; erg.Ag = ag; erg.Lg = lg;
            erg.UwGerechnet = (ag * g + af * r + lg * p) / (ag + af);
            erg.UwQuelle = $"Uw = (Ag·Ug + Af·Uf + lg·ψg)/(Ag + Af) = ({Z(ag)} m² · {Z(g)} + {Z(af)} m² · {Z(r)} + {Z(lg)} m · {Z(p)})/({Z(ag)} + {Z(af)}) = {Z(erg.UwGerechnet.Value)} W/(m²·K); " +
                $"A = {Z(b)} m · {Z(h)} m, Af = Rahmenanteil {Z(ra)} · A, lg = 2·(b + h)·√(1 − Rahmenanteil); Annahme: Verglasung dem Fenster ähnlich; " +
                $"Ug: {Quelle(f.Eigenschaft("ug"))}; Uf: {Quelle(f.Eigenschaft("uf"))}; ψg: {Quelle(f.Eigenschaft("psiG"))}; Rahmenanteil: {Quelle(f.Eigenschaft("rahmenanteil"))}";
        }
        return erg;
    }
}
