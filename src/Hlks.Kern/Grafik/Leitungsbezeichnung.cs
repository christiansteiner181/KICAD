// Bezeichnung und Querschnitt der Leitungen (Christian Steiner, 17.10.2026): Rohre in NW oder DN, Kanäle und
// Lüftungsrohre in mm. Einstellbar wie die Masseinheit: Ansicht «rohrbezeichnung» › Projektinformation
// «rohrbezeichnung» › Vorgabe NW (Christian 17.10.2026: «NW als Vorgabe für Rohre, DN nur auf Einstellung»).
//   Rechteckkanal:                   «400×200»   (Breite × Höhe in mm)
//   Rundkanal, Lüftungsrohr:         «Ø 250»     (Durchmesser in mm; beim Lüftungsrohr aus dn, wenn kein Durchmesser)
//   Rohr (nicht Abwasser):           «NW 20» bzw. «DN 20»; mit Kürzel der Serie «CNS NW 20» (Entscheid Christian Steiner 19.10.2026, B9b:
//                                    «Alle anderen Rohre können mit ihrer NW oder DN definiert werden»; ersetzt «CNS Ø22» aus B9). Die
//                                    Zahl kommt aus dem Typ der Serie bzw. Leitung.dn; fehlt sie, gibt es keine Beschriftung.
//   Abwasser (Schmutz-, Regenwasser): «PE-S 110», ohne Kürzel «110»: Aussendurchmesser in mm, ohne Ø, ohne NW/DN (Entscheid 19.10.2026, B9b);
//                                    die Einstellung rohrbezeichnung gilt für Abwasser nicht. Fehlt da, gibt es keine Beschriftung.
//   Entscheidung und Zahlen: Rohrgroesse.cs (die eine gemeinsame Funktion für Pläne, Schema, Listen und Exporte)
// Lüftung = Klasse Kanal oder Medium des Systems mit Gewerk lueftung. Kanäle und Lüftungsrohre erscheinen in
// Grundriss und 3D in ihrer echten Grösse (Querschnitt), Rohre wie bisher nach DN.

using System.Globalization;

namespace Hlks.Kern;

/// <summary>Querschnitt einer Leitung in mm: rund (Durchmesser) oder rechteckig (Breite × Höhe).</summary>
public sealed record Querschnitt(double? Durchmesser, double? Breite, double? Hoehe)
{
    public bool Rechteck => Breite is > 0 && Hoehe is > 0;
    /// <summary>Breite im Grundriss in mm.</summary>
    public double Grundrissbreite => Rechteck ? Breite!.Value : Durchmesser ?? 0;
}

public static class Leitungsbezeichnung
{
    public static readonly IReadOnlyList<string> Arten = ["NW", "DN"];
    /// <summary>Vorgabe, wenn weder Ansicht noch Projekt etwas sagen: NW (Christian Steiner, 17.10.2026).</summary>
    public const string Vorgabe = "NW";

    /// <summary>Gehört die Leitung zur Lüftung (Kanal oder Luftmedium)?</summary>
    public static bool IstLuft(Projekt p, Element e) =>
        e.Klasse == "Kanal" || HlksEinordnung.Medium(p, e)?.Gewerk == "lueftung";

    /// <summary>Querschnitt einer Lüftungsleitung; bei Rohren der übrigen Gewerke null (Darstellung nach DN).</summary>
    public static Querschnitt? QuerschnittVon(Projekt p, Element e)
    {
        if (!IstLuft(p, e)) return null;
        double? Z(string n) => e.Zahl(n) is double v && v > 0 ? v : null;
        if (Z("breite") is double b && Z("hoehe") is double h) return new(null, b, h);
        return (Z("durchmesser") ?? HlksEinordnung.Dn(e)) is double d ? new(d, null, null) : null;
    }

    /// <summary>NW oder DN für Rohre in dieser Ansicht und woher es kommt (ansicht | projekt | vorgabe).</summary>
    public static (string Art, string Herkunft) Art(Modell m, Element? ansicht)
    {
        if (Gueltig(ansicht?.Text("rohrbezeichnung")) is { } a) return (a, "ansicht");
        if (Gueltig(m.DerKlasse("Projektinformation").FirstOrDefault()?.Text("rohrbezeichnung")) is { } p) return (p, "projekt");
        return (Vorgabe, "vorgabe");
    }

    /// <summary>Beschriftung der Leitung, z. B. «DN 50», «NW 50», «400×200», «Ø 250»; null ohne Angaben.</summary>
    public static string? Text(Projekt p, Element? ansicht, Element e)
    {
        if (QuerschnittVon(p, e) is { } q)
            return q.Rechteck ? $"{Mm(q.Breite!.Value)}×{Mm(q.Hoehe!.Value)}" : $"Ø {Mm(q.Durchmesser!.Value)}";
        if (IstLuft(p, e)) return null;
        // Modul 3 (B9b): Rohre – Abwasser mit Aussendurchmesser («PE-S 110»), alle übrigen mit NW/DN («CNS NW 20»); ohne Zahl keine Beschriftung
        return Rohrgroessen.Von(p, ansicht, e).Beschriftung;
    }

    /// <summary>Höhe einer Lüftungsleitung zur UK roh Decke (Christian 09.11.2026: «Lüftungen beziehen sich nicht auf lagen. diese werden für Rohre
    /// immer als Achse = -xy UKD und bei kanälen immer OK = -xy UKD angegeben und gezeichnet», Bezeichnung «Achse = -XYmm UKD» bzw. «OK = -XYmm UKD»):
    /// runde Leitung ab Achse, eckige ab OK, gemessen am ersten waagrechten Stück. null ohne Querschnitt, senkrecht oder ohne UK roh Decke.</summary>
    public static string? HoeheUkd(Projekt p, Element e)
    {
        if (QuerschnittVon(p, e) is not { } q || e.Geometrie?.Punkte is not { Count: >= 2 } pts || e.Geschoss is null || p.Modell.Finde(e.Geschoss) is not { } g) return null;
        double? z = null;
        for (int i = 1; i < pts.Count && z is null; i++)
            if (Math.Abs(pts[i][0] - pts[i - 1][0]) + Math.Abs(pts[i][1] - pts[i - 1][1]) > 1 && pts[i - 1].Length > 2) z = pts[i - 1][2];
        if (z is null || Lagen.UkRohdecke(p, g).Wert is not double ukd) return null;
        double hoehe = z.Value - (g.Zahl("kote") ?? 0) + (q.Rechteck ? q.Hoehe!.Value / 2 : 0);   // Achse bzw. OK über der Kote
        double d = Math.Round(ukd - hoehe);
        string wert = d == 0 ? "±0" : d > 0 ? "−" + Mm(d) : "+" + Mm(-d);
        return $"{(q.Rechteck ? "OK" : "Achse")} = {wert}mm UKD";
    }

    /// <summary>Aussendurchmesser in mm für die Beschriftung: ganzzahlig ohne Nachkommastellen («22»), sonst auf eine Stelle gerundet
    /// mit Punkt («22.5»). Für die Bausteine im Einlageplan («KW: B.n.o Ø16»).</summary>
    public static string Aussendurchmesser(double da) => Math.Round(da, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    static string Mm(double v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);

    static string? Gueltig(string? s) => s is not null && Arten.Contains(s.ToUpperInvariant()) ? s.ToUpperInvariant() : null;
}
