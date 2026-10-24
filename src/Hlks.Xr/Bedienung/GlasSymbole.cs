// Quest App V1a: eigene einfache Symbole für die Apps im Stil von visionOS (keine SF Symbols – deren Lizenz gilt nur für Apple-Plattformen). Jedes Symbol ist eine
// Liste von Linienzügen im Einheitsquadrat (−0.5 … 0.5, y nach oben); die App zeichnet sie weiss auf dem Knopf (Strichstärke nach der Grösse). Rein, ohne StereoKit.

namespace Hlks.Xr.Bedienung;

public static class GlasSymbole
{
    public const string Status = "status", Etagen = "etagen", Messen = "messen", Marker = "marker", Ort = "ort", Tisch = "tisch", Begehen = "begehen",
        Ebenen = "ebenen", Schliessen = "schliessen", Loeschen = "loeschen", Lot = "lot", Drehen = "drehen", Haken = "haken", Plus = "plus", Gesten = "gesten";

    static (double X, double Y)[] Kreis(double cx, double cy, double r, int n = 20, double von = 0, double bis = 2 * Math.PI) =>
        [.. Enumerable.Range(0, n + 1).Select(i => { var a = von + (bis - von) * i / n; return (cx + r * Math.Cos(a), cy + r * Math.Sin(a)); })];

    static (double X, double Y)[] Raute(double cy, double b, double h) => [(-b, cy), (0, cy + h), (b, cy), (0, cy - h), (-b, cy)];

    /// <summary>Die Linienzüge eines Symbols (unbekannt: leer).</summary>
    public static IReadOnlyList<(double X, double Y)[]> Linien(string? name) => name switch
    {
        Status => [Kreis(0, 0, 0.36), Kreis(0, 0, 0.08, 10)],
        Etagen => [Raute(0.22, 0.38, 0.13), Raute(0, 0.38, 0.13), Raute(-0.22, 0.38, 0.13)],
        Messen => [[(-0.42, -0.12), (0.42, -0.12), (0.42, 0.12), (-0.42, 0.12), (-0.42, -0.12)], [(-0.24, 0.12), (-0.24, 0.0)], [(-0.06, 0.12), (-0.06, 0.03)],
            [(0.12, 0.12), (0.12, 0.0)], [(0.30, 0.12), (0.30, 0.03)]],
        Marker => [[.. Kreis(0, 0.14, 0.24, 18, -0.25 * Math.PI, 1.25 * Math.PI), (0, -0.42), (0.17, -0.03)], Kreis(0, 0.14, 0.07, 8)],
        Ort or Begehen => [Kreis(0, 0.30, 0.1, 10), [(0, 0.18), (0, -0.12)], [(-0.2, 0.06), (0.2, 0.06)], [(0, -0.12), (-0.15, -0.42)], [(0, -0.12), (0.15, -0.42)]],
        Tisch => [[(-0.42, 0.05), (0.42, 0.05)], [(-0.3, 0.05), (-0.3, -0.38)], [(0.3, 0.05), (0.3, -0.38)], [(-0.15, 0.05), (-0.15, 0.3), (0.15, 0.3), (0.15, 0.05)]],
        Ebenen => [Raute(0.1, 0.36, 0.14), [(-0.36, -0.08), (0, -0.22), (0.36, -0.08)]],
        Schliessen => [[(-0.25, -0.25), (0.25, 0.25)], [(-0.25, 0.25), (0.25, -0.25)]],
        Loeschen => [[(-0.3, 0.25), (0.3, 0.25)], [(-0.1, 0.25), (-0.1, 0.35), (0.1, 0.35), (0.1, 0.25)], [(-0.22, 0.25), (-0.18, -0.38), (0.18, -0.38), (0.22, 0.25)]],
        Lot => [[(-0.4, -0.3), (0.4, -0.3)], [(0, -0.3), (0, 0.4)], [(0, -0.18), (0.12, -0.18), (0.12, -0.3)]],
        Drehen => [[.. Kreis(0, 0, 0.32, 18, 0.3, 1.8 * Math.PI)], [(0.3, 0.32), (0.28, 0.1), (0.07, 0.15)]],
        Haken => [[(-0.3, 0), (-0.08, -0.22), (0.32, 0.25)]],
        Plus => [[(-0.3, 0), (0.3, 0)], [(0, -0.3), (0, 0.3)]],
        Gesten => [[(-0.2, -0.4), (-0.2, 0.15)], [(-0.05, -0.4), (-0.05, 0.38)], [(0.1, -0.4), (0.1, 0.3)], [(0.25, -0.4), (0.25, 0.15)], [(-0.2, 0.15), (-0.38, 0.0)]],
        _ => [],
    };

    /// <summary>Alle Namen (für Tests und die Anleitung).</summary>
    public static readonly IReadOnlyList<string> Alle = [Status, Etagen, Messen, Marker, Ort, Tisch, Begehen, Ebenen, Schliessen, Loeschen, Lot, Drehen, Haken, Plus, Gesten];
}
