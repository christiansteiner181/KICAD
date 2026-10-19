// Text aus DWG/DXF bereinigen (Modul 1b, B3): MText-Formatcodes (\A1; \f…; \W…; \H…x; \S…^…; \P …) und
// %%-Zeichen (%%c Ø, %%d °, %%p ±) in lesbaren Klartext umsetzen. Die Plangrafik kennt nur einfachen Text.

using System.Globalization;
using System.Text;

namespace Hlks.Kern;

public static class CadText
{
    /// <summary>MText in Zeilen zerlegen (Formatcodes entfernt).</summary>
    public static List<string> MText(string? roh)
    {
        var zeilen = new List<string>();
        if (string.IsNullOrEmpty(roh)) return zeilen;
        var sb = new StringBuilder();
        for (int i = 0; i < roh.Length; i++)
        {
            char c = roh[i];
            if (c == '\\' && i + 1 < roh.Length)
            {
                char d = roh[i + 1];
                switch (d)
                {
                    case 'P' or 'N': zeilen.Add(sb.ToString()); sb.Clear(); i++; break;
                    case '\\' or '{' or '}': sb.Append(d); i++; break;
                    case '~': sb.Append(' '); i++; break;
                    case 'L' or 'l' or 'O' or 'o' or 'K' or 'k': i++; break;
                    case 'U' when i + 6 < roh.Length && roh[i + 2] == '+'
                                  && int.TryParse(roh.AsSpan(i + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                        sb.Append((char)code); i += 6; break;
                    case 'S':
                    {
                        int ende = roh.IndexOf(';', i + 2);
                        if (ende < 0) { i++; break; }
                        sb.Append(Gestapelt(roh.Substring(i + 2, ende - i - 2)));
                        i = ende;
                        break;
                    }
                    default:
                    {
                        // \A1;  \pxql;  \fArial|b0|i0;  \W1.0;  \H2x;  \C1;  \Q15;  \T1;  – Parameter bis zum Semikolon
                        int ende = roh.IndexOf(';', i + 2);
                        i = ende < 0 ? i + 1 : ende;
                        break;
                    }
                }
            }
            else if (c is '{' or '}' or '\r') { }
            else if (c == '\n') { zeilen.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        zeilen.Add(sb.ToString());
        return zeilen.Select(Prozentzeichen).ToList();
    }

    /// <summary>Einzeiliger Text (TEXT, Attribute): nur die %%-Codes.</summary>
    public static string Einfach(string? roh) => Prozentzeichen(roh ?? "");

    /// <summary>%%c Ø · %%d ° · %%p ± · %%u/%%o/%%k (Schalter, entfällt) · %%%% % · %%nnn Zeichencode.</summary>
    static string Prozentzeichen(string s)
    {
        if (!s.Contains("%%")) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length && s[i + 1] == '%')
            {
                char k = char.ToLowerInvariant(s[i + 2]);
                if (k == 'c') { sb.Append('Ø'); i += 2; continue; }
                if (k == 'd') { sb.Append('°'); i += 2; continue; }
                if (k == 'p') { sb.Append('±'); i += 2; continue; }
                if (k is 'u' or 'o' or 'k') { i += 2; continue; }
                if (k == '%') { sb.Append('%'); i += 2; continue; }
                if (i + 4 < s.Length && int.TryParse(s.AsSpan(i + 2, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code)) { sb.Append((char)code); i += 4; continue; }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>Gestapelter Text «zähler^nenner» (hoch-/tiefgestellt) bzw. «zähler/nenner», «zähler#nenner».</summary>
    static string Gestapelt(string inhalt)
    {
        int i = inhalt.IndexOf('^');
        if (i >= 0)
        {
            var oben = inhalt[..i].Trim();
            var unten = inhalt[(i + 1)..].Trim();
            if (unten.Length == 0) return Hoch(oben);
            return oben.Length == 0 ? unten : oben + unten;
        }
        return inhalt.Replace('#', '/');
    }

    static string Hoch(string s) => s switch { "1" => "¹", "2" => "²", "3" => "³", _ => s };
}
