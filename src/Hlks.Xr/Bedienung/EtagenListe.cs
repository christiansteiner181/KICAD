// Quest App V1e (Brillentest, Christian 24.10.2026): «die etagen auswahl geht über mehrere seiten da wäre besser die liste zu vergrössern.» Rein, ohne StereoKit.
// Die App «Etagen» zeigt alle Geschosse (am Tisch dazu «Alle Etagen») auf EINER Seite: passt die Liste nicht in das gewöhnliche Fenster, wird die Tafel um so viele
// Zeilen höher (GlasMasse.MehrZeilen); die Zeilen bleiben gleich gross und gleich weit auseinander (52 pt hoch, Mitten 60 pt – mit der ganzen Hand bzw. der
// Fingerspitze treffbar). Erst über «etagen.zeilen_max» Einträgen (Annahme V1e 12) rollt die Liste: es stehen so viele Zeilen da, ein Wisch auf der Tafel nach oben
// bzw. unten rollt um eine Zeile weniger als sichtbar (eine bleibt zur Orientierung stehen) – keine Seiten. Beim Öffnen ist das aktuelle Geschoss sichtbar (möglichst
// in der Mitte); hervorgehoben ist es in der Liste selbst (GlasElement.Aktuell).

namespace Hlks.Xr.Bedienung;

/// <summary>Anordnung der Etagenliste: so viele Zeilen mehr als das gewöhnliche Fenster, erste sichtbare Zeile, Zahl der sichtbaren Zeilen, rollt die Liste.</summary>
public readonly record struct EtagenAnordnung(int MehrZeilen, int Ab, int Sichtbar, bool Rollt);

public static class EtagenListe
{
    /// <summary>
    /// anzahl = Einträge, frei = Zeilen im gewöhnlichen Fenster (unter dem Titel), zeilenMax = «etagen.zeilen_max», aktuell = Index des aktuellen Eintrags (−1 = keiner),
    /// ab = erste sichtbare Zeile bisher (null = neu geöffnet: das aktuelle Geschoss sichtbar machen).
    /// </summary>
    public static EtagenAnordnung Anordnen(int anzahl, int frei, int zeilenMax, int aktuell, int? ab)
    {
        frei = Math.Max(1, frei);
        zeilenMax = Math.Max(frei, zeilenMax);
        if (anzahl <= frei) return new EtagenAnordnung(0, 0, Math.Max(0, anzahl), false);
        var sichtbar = Math.Min(anzahl, zeilenMax);
        var mehr = sichtbar - frei;
        if (anzahl <= zeilenMax) return new EtagenAnordnung(mehr, 0, anzahl, false);
        var hoechstens = anzahl - sichtbar;
        var erste = ab ?? (aktuell >= 0 ? aktuell - sichtbar / 2 : 0);
        return new EtagenAnordnung(mehr, Math.Clamp(erste, 0, hoechstens), sichtbar, true);
    }

    /// <summary>Rollen mit einem Wisch auf der Tafel: nach oben (Hoch) weiter in der Liste, nach unten (Runter) zurück, je um eine Zeile weniger als sichtbar.</summary>
    public static int Rollen(EtagenAnordnung a, int anzahl, WischRichtung r)
    {
        if (!a.Rollt) return a.Ab;
        var schritt = Math.Max(1, a.Sichtbar - 1);
        var neu = r switch { WischRichtung.Hoch => a.Ab + schritt, WischRichtung.Runter => a.Ab - schritt, _ => a.Ab };
        return Math.Clamp(neu, 0, Math.Max(0, anzahl - a.Sichtbar));
    }
}
