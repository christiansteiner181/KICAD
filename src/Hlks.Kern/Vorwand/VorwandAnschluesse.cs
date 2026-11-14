// Anschlüsse der Montageelemente (Entscheid Christian 13.11.2026, Frage 30: «Montageelemente als Familien mit eigenen Anschlüssen»; Klarstellung
// 14.11.2026: «die anschlüsse sind im vorwand element»). Lage aus dem Katalog des Elementtyps (kataloge/vorwand_geberit.json, anschluesse mit
// Höhe z über OKFF und seitlichem Versatz s, je mit Quelle bzw. Annahme): s = Mitte des Elements + Versatz, q = Vorderseite der Vorwand + 5 mm
// (Armaturenanschluss 5 mm über die Beplankung, Planungshandbuch S. 163 – wie der Körper in VorwandGrafik), Richtung zum Raum (Apparateseite).
// Grösse: WC-Abgang d 90 (Planungshandbuch S. 178, wie die Darstellung); die übrigen ohne Grösse (Katalog ohne Mass – Annahme: keine Prüfung).
// Damit lassen sich Leitungen an Montageelemente räumlich anschliessen (vorher nur logisch, Gesamtprüfung fachwerkzeuge Befund 2).

namespace Hlks.Kern;

public static class VorwandAnschluesse
{
    /// <summary>Anschlussstellen eines Montageelements in seiner Vorwand; null, wenn Vorwand oder Typ fehlen (dann ohne Lage wie bisher).</summary>
    public static List<AnschlussStelle>? Von(Modell m, Katalog k, Element me)
    {
        if (me.TeilVon is not { } vid || m.Finde(vid) is not { } vorwand || VorwandGestell.Ableiten(m, k, vorwand) is not { } auf) return null;
        var el = auf.Elemente.FirstOrDefault(x => x.Element.Id == me.Id);
        if (el?.Typ is not { } typ || typ.Anschluesse.Count == 0) return null;
        double mitte = (el.S0 + el.S1) / 2, q = auf.Tiefe + 5;
        var p0 = auf.Punkt(mitte, auf.Tiefe); var p1 = auf.Punkt(mitte, auf.Tiefe + 1);
        double[] richtung = [p1[0] - p0[0], p1[1] - p0[1], 0];
        var aus = new List<AnschlussStelle>();
        foreach (var a in typ.Anschluesse)
        {
            var xy = auf.Punkt(mitte + a.S, q);
            var g = a.Medium == "SW" && typ.Apparate.Contains("WC") ? Leitungsgroesse.Aussen(90) : default;
            var gesetzt = me.Anschluesse.FirstOrDefault(x => x.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase));
            aus.Add(new AnschlussStelle(a.Name, gesetzt is null ? a.Medium : Netz.Medium(m, me, gesetzt), a.Fluss, [xy[0], xy[1], auf.Okff + a.Z], richtung, g,
                null, null, gesetzt?.Gewinde, "vorwand_katalog"));
        }
        return aus;
    }
}
