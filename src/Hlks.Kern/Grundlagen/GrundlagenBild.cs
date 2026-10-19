// Grafikquelle für PDF- und Bild-Unterlagen (Modul 1b, B3): die Seite bzw. das Bild als ein Primitiv «bild»
// (Punkte = vier Ecken im Modell: unten links, unten rechts, oben rechts, oben links; Quelle = Id der Unterlage).
// Ein PDF wird einmal als PNG gerendert (Ableitung «.seiteN.png», bei anderer Auflösung «.seiteN-<dpi>dpi.png»);
// ein Bild (PNG, JPEG) wird direkt gezeichnet. Bildschirm: GET /api/unterlagen/{id}/bild liefert <see cref="UnterlagenBild.Pfad"/>.
// Plan-PDF: der PdfZeichner zeichnet das Primitiv aus derselben Datei (<see cref="UnterlagenBild.PfadVon"/>).

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Bilddatei einer Unterlage der Art pdf oder bild.</summary>
public static class UnterlagenBild
{
    /// <summary>Standardauflösung beim Rendern eines PDF.</summary>
    public const double VorgabeDpi = 150;

    // Id der Unterlage → Bilddatei, gesetzt von der Grafikquelle; der PdfZeichner kennt das Projekt nicht und liest hier nach.
    static readonly ConcurrentDictionary<string, string> ablage = new();

    /// <summary>Bilddatei der Unterlage, die zuletzt gezeichnet wurde (null, wenn noch nie).</summary>
    public static string? PfadVon(string unterlageId) => ablage.TryGetValue(unterlageId, out var p) ? p : null;

    /// <summary>Zusatz des Dateinamens der gerenderten PDF-Seite.</summary>
    public static string Zusatz(int seite, double dpi) =>
        Math.Round(dpi) == VorgabeDpi ? $".seite{seite}.png" : $".seite{seite}-{Math.Round(dpi):0}dpi.png";

    /// <summary>Pfad der Bilddatei (PNG aus dem PDF bzw. das Bild selbst); erzeugt die Ableitung beim ersten Mal.</summary>
    public static string Pfad(Projekt p, Element unterlage)
    {
        var datei = unterlage.Text("datei") ?? throw new HlksFehler($"Unterlage {unterlage.Kennzeichen} hat keine Datei.");
        var quelle = Unterlagen.Pfad(p, datei);
        string pfad;
        switch (unterlage.Text("art"))
        {
            case "pdf":
            {
                int seite = (int)(unterlage.Zahl("seite") ?? 1);
                double dpi = (unterlage.Eigenschaft("transformation")?.Inhalt as JsonObject)?["dpi"] is { } n ? Json.Zahl(n) ?? VorgabeDpi : VorgabeDpi;
                pfad = Unterlagen.Ableitung(p, datei, Zusatz(seite, dpi), ziel => PdfSeite.Rendern(quelle, seite, dpi, ziel));
                break;
            }
            case "bild": pfad = quelle; break;
            default: throw new HlksFehler($"Unterlage {unterlage.Kennzeichen} ist keine PDF- oder Bild-Unterlage.");
        }
        ablage[unterlage.Id] = pfad;
        return pfad;
    }

    /// <summary>Medientyp der Bilddatei für REST («image/png», «image/jpeg»).</summary>
    public static string Medientyp(string pfad) => BildDatei.Lesen(pfad).Format == "jpg" ? "image/jpeg" : "image/png";
}

public sealed class GrundlagenBild : IGrundlagenArt
{
    public bool Fuer(string art) => art is "pdf" or "bild";

    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element unterlage)
    {
        BildInfo info;
        try { info = BildDatei.Lesen(UnterlagenBild.Pfad(ctx.Projekt, unterlage)); }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { return []; }   // fehlende Datei: Grundriss bleibt zeichenbar
        var lage = UnterlagenLage.Lesen(unterlage);
        double b = info.Breite, h = info.Hoehe;
        double[] P((double X, double Y) q) => [q.X, q.Y];
        var bild = new Primitiv
        {
            Art = "bild",
            Quelle = unterlage.Id,
            Punkte = [P(lage.AufPixel(0, h)), P(lage.AufPixel(b, h)), P(lage.AufPixel(b, 0)), P(lage.AufPixel(0, 0))],
            Stil = new Stil { Stift = null, Deckkraft = unterlage.Zahl("deckkraft") ?? 0.5 },
        };
        return [new Grafikteil { Id = unterlage.Id, Ebene = "grundlage", Primitive = [bild] }];
    }

    public Koerper? Koerper(Projekt p, Element unterlage) => null;
}
