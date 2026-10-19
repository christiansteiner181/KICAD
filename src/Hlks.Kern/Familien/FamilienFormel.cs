// Familien (Modul 2, B1): Formelsprache wie Revit (entwurf/modul-2.md, «Formeln»). Eigener Zerleger und Auswerter, keine
// Fremdbibliothek. Werte sind Zahlen (Längen mm, Winkel Grad, ja_nein 1/0) oder Texte (nur mit = und <> vergleichbar).
//
// Grammatik (von schwach nach stark bindend):
//   vergleich := summe [ ( < | > | <= | >= | = | <> ) summe ]      – nicht verkettbar (a < b < c ist ein Fehler)
//   summe     := produkt { ( + | - ) produkt }
//   produkt   := vorzeichen { ( * | / ) vorzeichen }
//   vorzeichen:= ( - | + ) vorzeichen | potenz
//   potenz    := grundwert [ ^ vorzeichen ]                         – rechtsbündig: 2^3^2 = 2^9
//   grundwert := Zahl | "Text" | Name | Funktion ( [ vergleich { , vergleich } ] ) | ( vergleich )
// ANNAHMEN (Prüfliste): «^» bindet stärker als das Vorzeichen (−2^2 = −4, wie in der Mathematik); Funktionsnamen sind
// gross/klein egal, Parameternamen nicht (Spezifikation); Gleichheit von Zahlen mit relativer Toleranz 1e-9;
// if() wertet nur den gewählten Zweig aus (if(x <> 0, 1/x, 0) ist erlaubt); round() rundet .5 von null weg,
// roundup/rounddown runden auf die nächste ganze Zahl auf- bzw. abwärts (Revit: Ganzzahl).

using System.Globalization;
using System.Text;

namespace Hlks.Kern;

/// <summary>Wert einer Formel: Zahl oder Text.</summary>
public readonly struct FWert
{
    public double Zahl { get; }
    public string? Text { get; }
    public bool IstText => Text is not null;
    FWert(double zahl, string? text) { Zahl = zahl; Text = text; }
    public static FWert Z(double zahl) => new(zahl, null);
    public static FWert T(string text) => new(0, text);
    public static FWert Wahr(bool b) => new(b ? 1 : 0, null);
    public override string ToString() => IstText ? Text! : Zahl.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>Fehler in einer Formel mit Stelle (1-basierte Zeichenposition; 0 = ganze Formel) und Weg zur Lösung.</summary>
public sealed class FormelFehler(string meldung, int stelle, string? vorschlag = null) : Exception(meldung)
{
    public int Stelle { get; } = stelle;
    public string? Vorschlag { get; } = vorschlag;
    /// <summary>«Stelle 12: unbekannter Name «hoehee»» bzw. ohne Stelle nur die Meldung.</summary>
    public string MitStelle => Stelle > 0 ? $"Stelle {Stelle}: {Message}" : Message;
}

/// <summary>Woher eine Formel die Werte ihrer Namen bekommt.</summary>
public interface IFormelUmgebung
{
    /// <summary>Wert des Parameters <paramref name="name"/>; wirft <see cref="FormelFehler"/> (unbekannt, ohne Wert, Zyklus).</summary>
    FWert Wert(string name, int stelle);
}

/// <summary>Ein zerlegter Ausdruck (Baum). Auswerten ist ohne Seiteneffekte und darf parallel laufen.</summary>
public abstract class Ausdruck
{
    public int Stelle { get; init; }
    public abstract FWert Werten(IFormelUmgebung u);
    /// <summary>Alle Parameternamen mit Stelle (für Prüfungen ohne Auswertung: unbekannte Namen, Typregel, Zyklen).</summary>
    public abstract void NamenSammeln(List<(string Name, int Stelle)> namen);

    public List<(string Name, int Stelle)> Namen()
    {
        var l = new List<(string, int)>();
        NamenSammeln(l);
        return l;
    }

    internal static double Zahl(FWert w, int stelle, string wofuer)
    {
        if (w.IstText) throw new FormelFehler($"{wofuer} braucht eine Zahl, nicht den Text «{w.Text}»", stelle,
            "Texte lassen sich nur mit = und <> vergleichen; mit Zahlen rechnen.");
        return w.Zahl;
    }

    internal static FWert Endlich(double z, int stelle)
    {
        if (double.IsNaN(z) || double.IsInfinity(z))
            throw new FormelFehler("Ergebnis ist keine endliche Zahl", stelle, "Werte und Formel prüfen (z. B. Potenz einer negativen Zahl mit Bruch).");
        return FWert.Z(z);
    }
}

sealed class Zahlknoten(double zahl) : Ausdruck
{
    public double Wert { get; } = zahl;
    public override FWert Werten(IFormelUmgebung u) => FWert.Z(Wert);
    public override void NamenSammeln(List<(string, int)> namen) { }
}

sealed class Textknoten(string text) : Ausdruck
{
    public override FWert Werten(IFormelUmgebung u) => FWert.T(text);
    public override void NamenSammeln(List<(string, int)> namen) { }
}

sealed class Nameknoten(string name) : Ausdruck
{
    public string Name { get; } = name;
    public override FWert Werten(IFormelUmgebung u) => u.Wert(Name, Stelle);
    public override void NamenSammeln(List<(string, int)> namen) => namen.Add((Name, Stelle));
}

sealed class Vorzeichenknoten(Ausdruck a) : Ausdruck
{
    public override FWert Werten(IFormelUmgebung u) => FWert.Z(-Zahl(a.Werten(u), Stelle, "Das Vorzeichen −"));
    public override void NamenSammeln(List<(string, int)> namen) => a.NamenSammeln(namen);
}

sealed class Binaerknoten(string op, Ausdruck a, Ausdruck b) : Ausdruck
{
    public string Op { get; } = op;

    public override FWert Werten(IFormelUmgebung u)
    {
        var x = a.Werten(u);
        var y = b.Werten(u);
        if (Op is "=" or "<>")
        {
            if (x.IstText != y.IstText)
                throw new FormelFehler($"«{Op}» vergleicht Text mit Zahl ({x} {Op} {y})", Stelle, "Beide Seiten müssen Texte oder beide Zahlen sein.");
            bool gleich = x.IstText ? string.Equals(x.Text, y.Text, StringComparison.Ordinal) : Gleich(x.Zahl, y.Zahl);
            return FWert.Wahr(Op == "=" ? gleich : !gleich);
        }
        string was = $"Der Operator «{Op}»";
        double p = Zahl(x, Stelle, was), q = Zahl(y, Stelle, was);
        switch (Op)
        {
            case "+": return Endlich(p + q, Stelle);
            case "-": return Endlich(p - q, Stelle);
            case "*": return Endlich(p * q, Stelle);
            case "/":
                if (q == 0) throw new FormelFehler("Division durch 0", Stelle, "Nenner prüfen oder mit if(nenner <> 0, …, …) absichern.");
                return Endlich(p / q, Stelle);
            case "^":
                if (p == 0 && q < 0) throw new FormelFehler("Division durch 0 (0 hoch negative Zahl)", Stelle, "Basis oder Exponent prüfen.");
                return Endlich(Math.Pow(p, q), Stelle);
            case "<": return FWert.Wahr(p < q && !Gleich(p, q));
            case ">": return FWert.Wahr(p > q && !Gleich(p, q));
            case "<=": return FWert.Wahr(p < q || Gleich(p, q));
            case ">=": return FWert.Wahr(p > q || Gleich(p, q));
            default: throw new FormelFehler($"Unbekannter Operator «{Op}»", Stelle);
        }
    }

    /// <summary>Gleichheit mit relativer Toleranz 1e-9 (Annahme, Dateikopf): 0.1 + 0.2 = 0.3 gilt.</summary>
    public static bool Gleich(double p, double q) => Math.Abs(p - q) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(p), Math.Abs(q)));

    public override void NamenSammeln(List<(string, int)> namen) { a.NamenSammeln(namen); b.NamenSammeln(namen); }
}

sealed class Funktionsknoten(string name, List<Ausdruck> args) : Ausdruck
{
    public string Name { get; } = name;
    public IReadOnlyList<Ausdruck> Argumente => args;

    double Z(IFormelUmgebung u, int i) => Zahl(args[i].Werten(u), args[i].Stelle, $"{Name}()");

    public override FWert Werten(IFormelUmgebung u)
    {
        const double Grad = Math.PI / 180;
        switch (Name)
        {
            case "if":
            {
                var b = args[0].Werten(u);
                if (b.IstText) throw new FormelFehler($"if() braucht als Bedingung einen Vergleich oder ja_nein, nicht den Text «{b.Text}»", args[0].Stelle,
                    "Bedingung z. B. als hoehe > 600 oder mit_abdeckung schreiben.");
                return b.Zahl != 0 ? args[1].Werten(u) : args[2].Werten(u);
            }
            case "and":
            {
                bool r = true;
                for (int i = 0; i < args.Count; i++) r &= Z(u, i) != 0;
                return FWert.Wahr(r);
            }
            case "or":
            {
                bool r = false;
                for (int i = 0; i < args.Count; i++) r |= Z(u, i) != 0;
                return FWert.Wahr(r);
            }
            case "not": return FWert.Wahr(Z(u, 0) == 0);
            case "min": return FWert.Z(Enumerable.Range(0, args.Count).Select(i => Z(u, i)).Min());
            case "max": return FWert.Z(Enumerable.Range(0, args.Count).Select(i => Z(u, i)).Max());
            case "abs": return FWert.Z(Math.Abs(Z(u, 0)));
            case "sqrt":
            {
                var x = Z(u, 0);
                if (x < 0) throw new FormelFehler($"sqrt() einer negativen Zahl ({Fmt(x)})", Stelle, "Wert prüfen oder abs() benutzen.");
                return FWert.Z(Math.Sqrt(x));
            }
            case "round": return FWert.Z(Math.Round(Z(u, 0), MidpointRounding.AwayFromZero));
            case "roundup": return FWert.Z(Math.Ceiling(Z(u, 0)));
            case "rounddown": return FWert.Z(Math.Floor(Z(u, 0)));
            case "sin": return FWert.Z(Math.Sin(Z(u, 0) * Grad));
            case "cos": return FWert.Z(Math.Cos(Z(u, 0) * Grad));
            case "tan":
            {
                var w = Z(u, 0);
                var rest = ((w % 180) + 180) % 180;
                if (Math.Abs(rest - 90) < 1e-9) throw new FormelFehler($"tan({Fmt(w)}°) ist nicht definiert", Stelle, "Winkel prüfen (tan von 90° ± k·180° gibt es nicht).");
                return Endlich(Math.Tan(w * Grad), Stelle);
            }
            case "asin" or "acos":
            {
                var x = Z(u, 0);
                if (x < -1 - 1e-12 || x > 1 + 1e-12) throw new FormelFehler($"{Name}() braucht einen Wert zwischen −1 und 1, nicht {Fmt(x)}", Stelle, "Verhältnis prüfen.");
                x = Math.Clamp(x, -1, 1);
                return FWert.Z((Name == "asin" ? Math.Asin(x) : Math.Acos(x)) / Grad);
            }
            case "atan": return FWert.Z(Math.Atan(Z(u, 0)) / Grad);
            case "pi": return FWert.Z(Math.PI);
            default: throw new FormelFehler($"Unbekannte Funktion «{Name}»", Stelle, "Funktionen: " + string.Join(", ", Formel.Funktionen.Keys));
        }
    }

    static string Fmt(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    public override void NamenSammeln(List<(string, int)> namen) { foreach (var a in args) a.NamenSammeln(namen); }
}

/// <summary>Zerlegt Formeln. <see cref="Parsen"/> wirft <see cref="FormelFehler"/> mit Stelle.</summary>
public static class Formel
{
    /// <summary>Funktionen mit kleinster und grösster Anzahl Argumente (−1 = beliebig viele).</summary>
    public static readonly IReadOnlyDictionary<string, (int Min, int Max)> Funktionen = new Dictionary<string, (int, int)>
    {
        ["if"] = (3, 3), ["and"] = (1, -1), ["or"] = (1, -1), ["not"] = (1, 1), ["min"] = (1, -1), ["max"] = (1, -1),
        ["abs"] = (1, 1), ["sqrt"] = (1, 1), ["round"] = (1, 1), ["roundup"] = (1, 1), ["rounddown"] = (1, 1),
        ["sin"] = (1, 1), ["cos"] = (1, 1), ["tan"] = (1, 1), ["asin"] = (1, 1), ["acos"] = (1, 1), ["atan"] = (1, 1), ["pi"] = (0, 0),
    };

    static readonly string[] Vergleiche = ["<=", ">=", "<>", "<", ">", "="];

    public static Ausdruck Parsen(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormelFehler("Formel ist leer", 0, "Eine Zahl oder einen Ausdruck wie «hoehe - 2 * 30» angeben.");
        var z = new Zerleger(text);
        var a = z.Vergleich();
        z.Ende();
        return a;
    }

    /// <summary>Gültiger Name (Parameter): Buchstabe oder _, dann Buchstaben, Ziffern, _ (Umlaute erlaubt).</summary>
    public static bool IstName(string s) =>
        s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');

    sealed class Zerleger(string s)
    {
        int i;

        void Leer() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        int Stelle => i + 1;

        bool Ist(string t)
        {
            Leer();
            return string.CompareOrdinal(s, i, t, 0, t.Length) == 0;
        }

        public void Ende()
        {
            Leer();
            if (i >= s.Length) return;
            if (s[i] == ')') throw new FormelFehler("«)» ohne öffnende Klammer", Stelle, "Klammern zählen.");
            if (Vergleiche.Any(Ist)) throw new FormelFehler("Vergleiche lassen sich nicht verketten", Stelle, "and(a < b, b < c) schreiben.");
            if (s[i] == ',') throw new FormelFehler("«,» ausserhalb einer Funktion", Stelle, "Argumente stehen in Klammern hinter dem Funktionsnamen, z. B. max(a, b).");
            throw new FormelFehler($"Unerwartetes «{s[i]}»", Stelle, "Operator (+ - * / ^) oder Klammer prüfen.");
        }

        public Ausdruck Vergleich()
        {
            var a = Summe();
            foreach (var op in Vergleiche)
                if (Ist(op))
                {
                    int st = Stelle;
                    i += op.Length;
                    var b = Summe();
                    return new Binaerknoten(op, a, b) { Stelle = st };
                }
            return a;
        }

        Ausdruck Summe()
        {
            var a = Produkt();
            while (true)
            {
                Leer();
                if (i < s.Length && s[i] is '+' or '-')
                {
                    int st = Stelle; var op = s[i++].ToString();
                    a = new Binaerknoten(op, a, Produkt()) { Stelle = st };
                }
                else return a;
            }
        }

        Ausdruck Produkt()
        {
            var a = Vorzeichen();
            while (true)
            {
                Leer();
                if (i < s.Length && s[i] is '*' or '/')
                {
                    int st = Stelle; var op = s[i++].ToString();
                    a = new Binaerknoten(op, a, Vorzeichen()) { Stelle = st };
                }
                else return a;
            }
        }

        Ausdruck Vorzeichen()
        {
            Leer();
            if (i < s.Length && s[i] == '-') { int st = Stelle; i++; return new Vorzeichenknoten(Vorzeichen()) { Stelle = st }; }
            if (i < s.Length && s[i] == '+') { i++; return Vorzeichen(); }
            return Potenz();
        }

        Ausdruck Potenz()
        {
            var a = Grundwert();
            Leer();
            if (i < s.Length && s[i] == '^')
            {
                int st = Stelle; i++;
                return new Binaerknoten("^", a, Vorzeichen()) { Stelle = st };   // rechtsbündig über Vorzeichen → Potenz
            }
            return a;
        }

        Ausdruck Grundwert()
        {
            Leer();
            if (i >= s.Length) throw new FormelFehler("Formel endet, hier fehlt ein Wert", Stelle, "Ausdruck vervollständigen.");
            int st = Stelle;
            char c = s[i];
            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])))
            {
                int a = i;
                while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
                if (i < s.Length && s[i] == '.') { i++; while (i < s.Length && char.IsAsciiDigit(s[i])) i++; }
                if (i < s.Length && s[i] is 'e' or 'E')
                {
                    int e = i + 1;
                    if (e < s.Length && s[e] is '+' or '-') e++;
                    if (e < s.Length && char.IsAsciiDigit(s[e])) { i = e; while (i < s.Length && char.IsAsciiDigit(s[i])) i++; }
                }
                if (i < s.Length && (char.IsLetter(s[i]) || s[i] == '_'))
                    throw new FormelFehler($"Zahl und Name ohne Operator («{s[a..i]}{s[i]}…»)", st, "Einheiten gibt es in Formeln nicht (Längen in mm); sonst * einfügen.");
                return new Zahlknoten(double.Parse(s[a..i], NumberStyles.Float, CultureInfo.InvariantCulture)) { Stelle = st };
            }
            if (c == '"')
            {
                int ende = s.IndexOf('"', i + 1);
                if (ende < 0) throw new FormelFehler("Text ohne schliessendes Anführungszeichen", st, "Text in \"…\" setzen.");
                var t = s[(i + 1)..ende];
                i = ende + 1;
                return new Textknoten(t) { Stelle = st };
            }
            if (char.IsLetter(c) || c == '_')
            {
                int a = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                var name = s[a..i];
                if (Ist("(")) return Funktion(name, st);
                return new Nameknoten(name) { Stelle = st };
            }
            if (c == '(')
            {
                i++;
                var a = Vergleich();
                if (!Ist(")")) throw new FormelFehler("«)» fehlt", Stelle, "Zu jeder «(» gehört eine «)».");
                i++;
                return a;
            }
            if (c == ')') throw new FormelFehler("Hier fehlt ein Wert vor «)»", st, "Ausdruck in der Klammer vervollständigen.");
            if (c == ',') throw new FormelFehler("Hier fehlt ein Wert vor «,»", st, "Argument angeben.");
            if (c is '*' or '/' or '^' or '<' or '>' or '=')
                throw new FormelFehler($"Hier fehlt ein Wert vor «{c}»", st, "Operator braucht links einen Wert.");
            throw new FormelFehler($"Unerlaubtes Zeichen «{c}»", st, "Erlaubt: Zahlen (Punkt als Dezimalzeichen), Namen, + - * / ^, Klammern, Vergleiche < > <= >= = <>, Funktionen, \"Text\".");
        }

        Ausdruck Funktion(string name, int st)
        {
            var klein = name.ToLowerInvariant();
            if (!Funktionen.TryGetValue(klein, out var anzahl))
                throw new FormelFehler($"Unbekannte Funktion «{name}»", st, "Funktionen: " + string.Join(", ", Funktionen.Keys) + Aehnlich(klein));
            i++;   // (
            var args = new List<Ausdruck>();
            if (!Ist(")"))
            {
                while (true)
                {
                    args.Add(Vergleich());
                    if (Ist(",")) { i++; continue; }
                    if (Ist(")")) break;
                    if (i >= s.Length) throw new FormelFehler($"«)» fehlt nach den Argumenten von {klein}()", Stelle, "Funktionsaufruf schliessen.");
                    throw new FormelFehler($"In {klein}() wird «,» oder «)» erwartet", Stelle, "Argumente mit Komma trennen.");
                }
            }
            i++;   // )
            if (args.Count < anzahl.Min || (anzahl.Max >= 0 && args.Count > anzahl.Max))
            {
                var soll = anzahl.Max < 0 ? $"mindestens {anzahl.Min}" : anzahl.Min == anzahl.Max ? $"{anzahl.Min}" : $"{anzahl.Min} bis {anzahl.Max}";
                throw new FormelFehler($"{klein}() braucht {soll} Argument(e), nicht {args.Count}", st,
                    klein switch { "if" => "if(bedingung, dann, sonst)", "pi" => "pi() ohne Argument", _ => $"{klein}(wert)" });
            }
            return new Funktionsknoten(klein, args) { Stelle = st };
        }

        static string Aehnlich(string name)
        {
            var t = Formel.Funktionen.Keys.Where(f => Abstand(f, name) <= 2).ToList();
            return t.Count > 0 ? $" – gemeint: {string.Join(" oder ", t)}?" : "";
        }
    }

    /// <summary>Editierabstand (Levenshtein) für Vorschläge bei Tippfehlern.</summary>
    public static int Abstand(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int x = 0; x <= a.Length; x++) d[x, 0] = x;
        for (int y = 0; y <= b.Length; y++) d[0, y] = y;
        for (int x = 1; x <= a.Length; x++)
            for (int y = 1; y <= b.Length; y++)
                d[x, y] = Math.Min(Math.Min(d[x - 1, y] + 1, d[x, y - 1] + 1), d[x - 1, y - 1] + (a[x - 1] == b[y - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    /// <summary>Ähnliche Namen für den Vorschlag bei einem unbekannten Namen.</summary>
    public static string NamensVorschlag(string name, IEnumerable<string> bekannte)
    {
        var liste = bekannte.ToList();
        var nah = liste.Where(n => Abstand(n.ToLowerInvariant(), name.ToLowerInvariant()) <= Math.Max(1, name.Length / 3)).Take(3).ToList();
        var sb = new StringBuilder();
        if (nah.Count > 0) sb.Append("Gemeint: ").Append(string.Join(" oder ", nah)).Append("? ");
        sb.Append(liste.Count == 0 ? "Die Familie hat keine Parameter." : "Parameter: " + string.Join(", ", liste));
        return sb.ToString();
    }
}
