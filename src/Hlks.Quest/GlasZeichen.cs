using Hlks.Xr;
using Hlks.Xr.Bedienung;
using XVec3 = Hlks.Xr.Vec3;

namespace Hlks.Quest;

/// <summary>
/// Zeichen, die die App selbst aus Punkten, Linien und Ringen zeichnet (Namen in Hlks.Xr.Bedienung.GlasSymbole.Gezeichnet, Vorsatz «@»), wo es in KICAD (symbole.js) kein
/// passendes Symbol als Textur gibt: H+K XR E1 «Werkzeuge» (Raster aus vier Punkten), XR-Labor S1 die Platzhalter-Apps der Spielwiese (Notizen, Bilder, Uhr, Liste,
/// Dokument, Einstellungen). Strichstärke und Masse im Verhältnis zum Radius r des Symbols (Annahme S1, im Stil der Strich-Symbole von KICAD: dunkle Linien auf dem
/// Pastellton). Neue Texturen gingen über werkzeuge/quest_symbole.mjs (offene Frage S1).
/// </summary>
static class GlasZeichen
{
    /// <summary>Zeichnet das Zeichen <paramref name="bild"/> in die runde Fläche lage (Radius r); false, wenn es kein selbst gezeichnetes Zeichen ist.</summary>
    public static bool Zeichnen(GlasZeichner z, string? bild, TabletPose lage, double r, GlasFarbe farbe)
    {
        if (bild is null || !GlasSymbole.Gezeichnet.Contains(bild)) return false;
        XVec3 P(double x, double y) => lage.Mitte + lage.Rechts * (x * r) + lage.Oben * (y * r) + lage.Normale * 0.003;
        var d = r * 0.085;   // Strich
        void L(double x0, double y0, double x1, double y1) => z.Linie(P(x0, y0), P(x1, y1), d, farbe);
        void Zug(params (double X, double Y)[] p) { for (int i = 1; i < p.Length; i++) L(p[i - 1].X, p[i - 1].Y, p[i].X, p[i].Y); }
        void Pkt(double x, double y, double durchmesser) => z.Punkt(P(x, y), durchmesser * r, farbe);
        switch (bild)
        {
            case GlasSymbole.ZeichenWerkzeuge:   // E1: Raster aus vier Punkten (wie eine Startseite)
                foreach (var (dx, dy) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) }) Pkt(dx * 0.22, dy * 0.22, 0.26);
                break;
            case GlasSymbole.ZeichenNotizen:     // Blatt mit Zeilen, die letzte kürzer
                foreach (var (y, bis) in new[] { (0.3, 0.38), (0.1, 0.38), (-0.1, 0.38), (-0.3, 0.05) }) L(-0.38, y, bis, y);
                break;
            case GlasSymbole.ZeichenBilder:      // Rahmen, Berge, Sonne
                Zug((-0.45, -0.33), (0.45, -0.33), (0.45, 0.33), (-0.45, 0.33), (-0.45, -0.33));
                Zug((-0.36, -0.24), (-0.1, 0.06), (0.1, -0.12), (0.2, -0.03), (0.36, -0.24));
                Pkt(0.22, 0.17, 0.2);
                break;
            case GlasSymbole.ZeichenUhr:         // Zifferblatt mit zwei Zeigern
                z.Ring(lage with { Mitte = P(0, 0) }, r * 0.5, d, farbe);
                L(0, 0, 0, 0.32);
                L(0, 0, 0.22, 0);
                Pkt(0, 0, 0.12);
                break;
            case GlasSymbole.ZeichenListe:       // drei Zeilen mit Aufzählungspunkt
                foreach (var y in new[] { 0.28, 0.0, -0.28 }) { Pkt(-0.32, y, 0.14); L(-0.14, y, 0.38, y); }
                break;
            case GlasSymbole.ZeichenDokument:    // Seite mit Eselsohr und Zeilen
                Zug((-0.32, -0.42), (0.32, -0.42), (0.32, 0.22), (0.12, 0.42), (-0.32, 0.42), (-0.32, -0.42));
                Zug((0.12, 0.42), (0.12, 0.22), (0.32, 0.22));
                foreach (var y in new[] { 0.04, -0.12, -0.28 }) L(-0.2, y, 0.2, y);
                break;
            case GlasSymbole.ZeichenEinstellungen:   // drei Regler
                foreach (var (y, x) in new[] { (0.28, -0.15), (0.0, 0.2), (-0.28, -0.04) }) { L(-0.4, y, 0.4, y); Pkt(x, y, 0.22); }
                break;
        }
        return true;
    }
}
