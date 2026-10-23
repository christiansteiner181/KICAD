// Natürliche Sortierung von Kennzeichen. Ersetzt in den JavaScript-Dateien <c>localeCompare(b, 'de', { numeric: true })</c> (vr-start-logik.js natuerlich)
// und <c>localeCompare</c> (xr-sitzung-logik.js geschosseAufbereiten).
//
// ABWEICHUNG: Die Sortierung hängt nicht von ICU oder der Gebietsschemaeinstellung des Geräts ab (Quest, Windows und Linux ordnen sonst verschieden).
// Eigene Regel: Satzzeichen vor Ziffern vor Buchstaben; Ziffernfolgen nach ihrem Zahlenwert; Buchstaben ohne Rücksicht auf Gross- und Kleinschreibung,
// bei sonst gleichen Texten steht Kleingeschrieben vor Grossgeschrieben (wie ICU). Umlaute und Sonderzeichen ordnen nach ihrem Zeichenwert (ICU stellt «ä» zu «a»).
// Für Kennzeichen wie R-1, R-2, R-10, 1.OG, EG, UG liefert beides dasselbe.

namespace Hlks.Xr;

internal sealed class NatuerlicherVergleich : IComparer<string?>
{
    public static readonly NatuerlicherVergleich Numerisch = new(true);
    public static readonly NatuerlicherVergleich Einfach = new(false);

    readonly bool _numerisch;
    NatuerlicherVergleich(bool numerisch) => _numerisch = numerisch;

    static int Klasse(char c) => char.IsAsciiDigit(c) ? 1 : char.IsLetter(c) ? 2 : 0;

    public int Compare(string? x, string? y)
    {
        var a = x ?? "";
        var b = y ?? "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            char ca = a[i], cb = b[j];
            if (_numerisch && char.IsAsciiDigit(ca) && char.IsAsciiDigit(cb))
            {
                int ei = i, ej = j;
                while (ei < a.Length && char.IsAsciiDigit(a[ei])) ei++;
                while (ej < b.Length && char.IsAsciiDigit(b[ej])) ej++;
                var za = a[i..ei].TrimStart('0');
                var zb = b[j..ej].TrimStart('0');
                if (za.Length != zb.Length) return za.Length < zb.Length ? -1 : 1;
                var c = string.CompareOrdinal(za, zb);
                if (c != 0) return c < 0 ? -1 : 1;
                i = ei;
                j = ej;
                continue;
            }
            int ka = Klasse(ca), kb = Klasse(cb);
            if (ka != kb) return ka < kb ? -1 : 1;
            char fa = char.ToLowerInvariant(ca), fb = char.ToLowerInvariant(cb);
            if (fa != fb) return fa < fb ? -1 : 1;
            i++;
            j++;
        }
        if (i < a.Length) return 1;
        if (j < b.Length) return -1;
        // nur noch Gross-/Kleinschreibung (oder führende Nullen) unterscheiden: Kleingeschrieben zuerst
        return -Math.Sign(string.CompareOrdinal(a, b));
    }
}
