// Quest App P4b-Nachtrag (Brillentest 23.10.2026: nach «Echter Tisch» und zurück blieben die Modelle schwarz): EIN Ort entscheidet, welchen Hintergrund der Renderer hat
// (Löschfarbe und Himmel), und merkt sich den ursprünglichen Zustand genau einmal. Vorher merkten sich der Sitzungsraum und der Passthrough je den Zustand des anderen
// und setzten ihn beim Ausschalten zurück; je nach Reihenfolge konnte so ein fremder Zustand (z. B. «durchsichtig» des Passthrough) als «vorher» zurückkommen.
//
// Regel (rein, ohne StereoKit):
//   Passthrough ein                  → durchsichtig (Löschfarbe 0, 0, 0, 0), kein Himmel – das Kamerabild liegt unter der Szene
//   sonst Sitzungsraum aktiv         → hell (V1a, Umgebung im Stil von visionOS: Löschfarbe = Farbe des Horizonts, Christian 24.10.2026) bzw. dunkel (Variante
//                                      «umgebung» = «tron», die frühere Fassung), kein Himmel
//   sonst (Begehen)                  → der Zustand vor dem ersten Wechsel (Ursprung), danach ist nichts mehr gemerkt
// Der Ursprung wird nur beim Verlassen des neutralen Zustands gemerkt, nie aus einem Zustand, den diese Regel selbst gesetzt hat. Licht (Renderer.SkyLight), Materialien und
// Tönung verstellt sie nicht.

namespace Hlks.Xr.Sitzung;

/// <summary>Hintergrund des Renderers: Löschfarbe (RGBA 0…1) und ob der Himmel gezeichnet wird.</summary>
public readonly record struct Hintergrund(float R, float G, float B, float A, bool Himmel)
{
    /// <summary>Dunkler Raum des Sitzungsraums.</summary>
    public static readonly Hintergrund Dunkel = new(0.012f, 0.016f, 0.024f, 1, false);
    /// <summary>V1a: helle Umgebung des Sitzungsraums (bis V1e die Farbe des Horizonts der hellen Kuppel); V1f: der helle Ersatzhintergrund am Tisch ohne Passthrough (Simulator).</summary>
    public static readonly Hintergrund Hell = new(0.86f, 0.88f, 0.90f, 1, false);
    /// <summary>Passthrough: durchsichtig, damit das Kamerabild unter der Szene erscheint.</summary>
    public static readonly Hintergrund Durchsichtig = new(0, 0, 0, 0, false);
}

/// <summary>Der eine Ort für den Hintergrund (siehe Dateikopf).</summary>
public sealed class HintergrundVerwalter
{
    Hintergrund? ursprung;

    public bool Sitzung { get; private set; }
    public bool Passthrough { get; private set; }
    /// <summary>Gemerkter Zustand vor dem ersten Wechsel (null im neutralen Zustand).</summary>
    public Hintergrund? Ursprung => ursprung;

    /// <summary>Neuer Zustand; aktuell = was der Renderer gerade hat. Rückgabe: was der Renderer jetzt haben soll.</summary>
    public Hintergrund Setzen(Hintergrund aktuell, bool sitzung, bool passthrough, bool hell = false)
    {
        if (ursprung is null && (sitzung || passthrough)) ursprung = aktuell;   // nur beim Verlassen des neutralen Zustands
        Sitzung = sitzung;
        Passthrough = passthrough;
        if (passthrough) return Hintergrund.Durchsichtig;
        if (sitzung) return hell ? Hintergrund.Hell : Hintergrund.Dunkel;
        var zurueck = ursprung ?? aktuell;
        ursprung = null;
        return zurueck;
    }
}
