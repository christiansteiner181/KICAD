// Quest App P5b: Zeilenumbruch für Texte in der Brille (Karte einer Prüfaufgabe, Hinweiszeile). Befund P5b am Prüfbild: Text.Add von StereoKit 0.3.11 mit
// TextFit.Wrap | TextFit.Squeeze bricht lange Texte ohne Zeilenwechsel nicht um, sondern verkleinert sie auf eine Zeile – die Anleitung wurde unlesbar klein.
// Die App bricht deshalb selbst an Wortgrenzen um; Squeeze verkleinert danach nur noch, wenn die Zeilen nicht in die Höhe passen. Rein, ohne StereoKit.

namespace Hlks.Xr.Bedienung;

public static class Textumbruch
{
    /// <summary>Mittlere Breite eines Zeichens der Standardschrift im Verhältnis zur Zeichenhöhe (Annahme P5b, am Prüfbild abgeschätzt).</summary>
    public const double ZeichenBreiteAnteil = 0.55;

    /// <summary>Zeichen je Zeile für eine Breite (m) bei dieser Zeichenhöhe (m); mindestens 8.</summary>
    public static int ZeichenJeZeile(double breiteM, double zeichenHoeheM) =>
        zeichenHoeheM <= 0 || !double.IsFinite(breiteM) ? 8 : Math.Max(8, (int)Math.Floor(breiteM / (zeichenHoeheM * ZeichenBreiteAnteil)));

    /// <summary>
    /// Bricht den Text an Leerzeichen um, so dass keine Zeile länger als max Zeichen ist (vorhandene Zeilenwechsel bleiben); ein Wort, das allein länger ist, wird
    /// hart geteilt.
    /// </summary>
    public static string Umbrechen(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || max < 1) return text ?? "";
        var aus = new List<string>();
        foreach (var absatz in text.Replace("\r\n", "\n").Split('\n'))
        {
            var zeile = "";
            foreach (var roh in absatz.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var wort = roh;
                while (wort.Length > max)
                {
                    if (zeile.Length > 0) { aus.Add(zeile); zeile = ""; }
                    aus.Add(wort[..max]);
                    wort = wort[max..];
                }
                if (zeile.Length == 0) zeile = wort;
                else if (zeile.Length + 1 + wort.Length <= max) zeile += " " + wort;
                else { aus.Add(zeile); zeile = wort; }
            }
            aus.Add(zeile);
        }
        return string.Join("\n", aus);
    }
}
