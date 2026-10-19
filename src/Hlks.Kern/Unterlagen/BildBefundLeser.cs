// Leser «bild» für PNG und JPEG (Modul 2, Auftrag B2): ein Befund «bild» mit Grösse in Bildpunkten und Auflösung (dpi), soweit
// die Datei sie nennt. Den Inhalt (Masszeichnung, Tabelle, Beschriftung) liest die KI aus dem Bild (unterlage_bild).
// Koordinaten eines Bildes: Bildpunkte, Ursprung oben links, y nach unten (anders als PDF: dort y nach oben).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class BildBefundLeser : IUnterlagenLeser
{
    public string Name => "bild";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["bild"];

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, "bild");
        var info = BildDatei.Lesen(pfad);
        var wert = new JsonObject { ["breite"] = info.Breite, ["hoehe"] = info.Hoehe, ["format"] = info.Format };
        var zusatz = new JsonObject { ["koordinaten"] = "Bildpunkte, Ursprung oben links, y nach unten" };
        if (info.Dpi is double dpi)
        {
            zusatz["dpi"] = Math.Round(dpi, 2);
            zusatz["breiteMm"] = Math.Round(info.Breite * 25.4 / dpi, 1);
            zusatz["hoeheMm"] = Math.Round(info.Hoehe * 25.4 / dpi, 1);
        }
        s.Neu("bild", wert, "px", seite: 1, box: [0, 0, info.Breite, info.Hoehe], zusatz: zusatz);
        s.Hinweis(info.Dpi is null
            ? $"Bild {info.Breite} × {info.Hoehe} Bildpunkte; die Datei nennt keine Auflösung (dpi). Der Inhalt steht nicht als Text vor: mit unterlage_bild ansehen."
            : $"Bild {info.Breite} × {info.Hoehe} Bildpunkte bei {info.Dpi:0.#} dpi. Der Inhalt steht nicht als Text vor: mit unterlage_bild ansehen.");
        return s.Ergebnis;
    }
}
