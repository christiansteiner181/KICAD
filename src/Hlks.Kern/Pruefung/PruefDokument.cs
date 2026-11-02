// Prüfansicht (Christian 02.11.2026: «programm integrierte PDF anzeige» – «Pläne prüfen und markieren»): das geprüfte PDF einer Ansicht der Art
// «pruefung» – ein Planblatt (PDF, wie der Kern es ausgibt) oder eine PDF-Unterlage (Modul 1b, Datei im Ordner unterlagen). Der Kern liefert Seitenzahl,
// Seitengrösse in pt und die Seite als PNG (Docnet/PDFium wie die Unterlagen); die Markierungen sind Elemente der Klasse Pruefmarke (Punkte in pt,
// Ursprung unten links, y nach oben) und werden mit element_anlegen bzw. element_aendern bearbeitet – rückgängig, für alle sichtbar, lesbar für die KI.

namespace Hlks.Kern;

public static class PruefDokument
{
    public const double VorgabeDpi = 110, MinDpi = 36, MaxDpi = 300;

    /// <summary>Das PDF eines Dokuments: Planblatt (vom Kern erzeugt, im Speicher) oder PDF-Unterlage (Pfad).</summary>
    static (string? Pfad, byte[]? Bytes, Element Element) Quelle(Projekt p, string dokument)
    {
        var e = p.Modell.Finde(dokument) ?? throw new HlksFehler($"Dokument «{dokument}» gibt es nicht.", "Kennzeichen eines Planblatts oder einer PDF-Unterlage angeben.");
        if (e.Klasse == "Planblatt") return (null, Planausgabe.Pdf(p, e.Kennzeichen), e);
        if (e.Klasse == "Unterlage" && e.Text("art") == "pdf" && e.Text("datei") is { } datei) return (Unterlagen.Pfad(p, datei), null, e);
        throw new HlksFehler($"{e.Kennzeichen} ({e.Klasse}) ist kein PDF.", "Ein Planblatt oder eine Unterlage der Art pdf wählen.");
    }

    /// <summary>Seitenzahl und Grösse jeder Seite in pt.</summary>
    public static (int Seiten, List<(double Breite, double Hoehe)> Masse, string Titel) Info(Projekt p, string dokument)
    {
        var (pfad, bytes, e) = Quelle(p, dokument);
        string? datei = pfad;
        string? temp = null;
        try
        {
            if (bytes is not null) { temp = Path.Combine(Path.GetTempPath(), $"kicad-pruefung-{Guid.NewGuid():N}.pdf"); File.WriteAllBytes(temp, bytes); datei = temp; }
            var (n, _, _) = PdfSeite.Mass(datei!, 1);
            var masse = Enumerable.Range(1, n).Select(s => { var (_, b, h) = PdfSeite.Mass(datei!, s); return (b, h); }).ToList();
            var titel = e.Klasse == "Planblatt" ? string.Join(" ", new[] { e.Text("nummer"), e.Text("titel") }.Where(x => !string.IsNullOrEmpty(x))) : e.Name ?? e.Text("original") ?? e.Kennzeichen;
            return (n, masse, string.IsNullOrWhiteSpace(titel) ? e.Kennzeichen : titel);
        }
        finally { if (temp is not null) File.Delete(temp); }
    }

    /// <summary>Seite (ab 1) als PNG mit <paramref name="dpi"/>.</summary>
    public static byte[] SeitePng(Projekt p, string dokument, int seite, double? dpi)
    {
        var (pfad, bytes, _) = Quelle(p, dokument);
        var d = Math.Clamp(dpi ?? VorgabeDpi, MinDpi, MaxDpi);
        var (w, h, bgra) = PdfSeiteBild.Rendern(pfad, bytes, seite, d / 72.0);
        var ziel = Path.Combine(Path.GetTempPath(), $"kicad-pruefung-{Guid.NewGuid():N}.png");
        try
        {
            PngSchreiber.Schreiben(ziel, w, h, bgra, d);
            return File.ReadAllBytes(ziel);
        }
        finally { File.Delete(ziel); }
    }
}
