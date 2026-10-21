// Schnitthöhe des Grundrisses nach SIA 400:2000 Ziff. C.3.1.1 (Nachbearbeitung N21, Christian 21.10.2026: «schnitthöhe plan gem. sia»).
// Quelle: Der Grundriss ist ein Horizontalschnitt durch ein Geschoss; die Höhenlage des Schnittes «ist nicht genau fixiert … Sie ist in der
// Regel so zu wählen, dass durch alle Tür- und Fensteröffnungen geschnitten wird». Einen festen Wert nennt die Norm nicht.
//
// Entscheid des Hauptlaufs (nachbearbeitung.md N21); Zahlen in mm über OK Boden des Geschosses (Geschosskote):
//   - Massgebend sind die Höhenbereiche aller Türen und Fenster der Wände des Geschosses: Tür UK … UK + hoehe (UK = OK Boden, Annahme: Türen haben
//     keine Schwelle), Fenster bruestung … bruestung + hoehe. Ein Bereich ist halboffen [von, bis) wie Oeffnungslage.Schneidet; ohne hoehe nach oben offen.
//   - Gemeinsamer Bereich nicht leer: der Wert darin, der am nächsten bei 1000 mm liegt (bisherige Vorgabe; Annahme, am Freitag bestätigen).
//   - Sonst: die Höhe, die die meisten Öffnungen schneidet; bei Gleichstand die nächste bei 1000 mm. Ein Hinweis nennt die nicht geschnittenen Öffnungen.
//   - Ohne Türen und Fenster: 1000 mm mit Hinweis.
//   - Auf 10 mm gerundet, so dass der gerundete Wert im Bereich bleibt (ist der Bereich enger als 10 mm, gilt seine Untergrenze unverändert).
// Ansichtseigenschaft schnitthoeheNachSia (Vorgabe ja, auch für Ansichten ohne Wert); bei nein gilt schnitthoehe von Hand wie bisher.
// Zwischengespeichert je Geschoss und Stand des Modells (Aenderungszaehler), damit nicht jede Grafik neu rechnet.

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ergebnis von <see cref="SchnitthoeheSia.SchnitthoeheNachSia"/>.</summary>
/// <param name="Wert">Schnitthöhe in mm über OK Boden des Geschosses.</param>
/// <param name="Herkunft">Text mit «SIA 400:2000 Ziff. C.3.1.1», dem Bereich und den massgebenden Öffnungen.</param>
/// <param name="Hinweise">z. B. nicht geschnittene Öffnungen oder «keine Öffnungen».</param>
/// <param name="Massgebend">Öffnungen, die den Bereich begrenzen (Kennzeichen mit Grenze), unten zuerst.</param>
/// <param name="Oeffnungen">Anzahl der Türen und Fenster des Geschosses.</param>
/// <param name="Geschnitten">Davon vom Wert geschnitten.</param>
/// <param name="Von">Untergrenze des Bereichs, in dem der Wert liegt (mm über OK Boden), null ohne Öffnungen.</param>
/// <param name="Bis">Obergrenze (ausschliesslich), null = nach oben offen oder ohne Öffnungen.</param>
public sealed record SchnitthoeheSiaErgebnis(double Wert, string Herkunft, IReadOnlyList<string> Hinweise, IReadOnlyList<string> Massgebend,
    int Oeffnungen, int Geschnitten, double? Von, double? Bis);

public static class SchnitthoeheSia
{
    public const string Norm = "SIA 400:2000 Ziff. C.3.1.1";
    public const string Eigenschaft = "schnitthoeheNachSia";
    /// <summary>Bisherige Vorgabe der Ansicht und Zielwert, wenn der Bereich ihn enthält (mm über OK Boden).</summary>
    public const double Vorgabe = 1000;
    const double Raster = 10;

    /// <summary>Gilt für die Ansicht die Schnitthöhe nach SIA? Ohne Wert ja (bestehende Ansichten schneiden damit nach SIA).</summary>
    public static bool Aktiv(Element ansicht) => Grafik.Bool(ansicht, Eigenschaft, true);

    /// <summary>Schnitthöhe der Ansicht über OK Geschoss (mm): nach SIA gerechnet oder der Handwert der Ansicht (Vorgabe 1000).</summary>
    public static double Hoehe(Modell m, Katalog k, Element ansicht, Element? geschoss) =>
        geschoss is not null && Aktiv(ansicht) ? SchnitthoeheNachSia(m, k, geschoss).Wert : ansicht.Zahl("schnitthoehe") ?? Vorgabe;

    // ------------------------------------------------------------------ Zwischenspeicher

    sealed class Speicher
    {
        public long Zaehler = -1;
        public readonly Dictionary<string, SchnitthoeheSiaErgebnis> Je = new(StringComparer.Ordinal);
    }

    static readonly ConditionalWeakTable<Modell, Speicher> zwischen = new();
    static readonly object sperre = new();

    /// <summary>Schnitthöhe des Geschosses nach SIA 400:2000 Ziff. C.3.1.1 (Regeln im Dateikopf), zwischengespeichert je Stand des Modells.</summary>
    public static SchnitthoeheSiaErgebnis SchnitthoeheNachSia(Modell m, Katalog k, Element geschoss)
    {
        var s = zwischen.GetValue(m, _ => new Speicher());
        lock (sperre)
        {
            if (s.Zaehler != m.Aenderungszaehler) { s.Je.Clear(); s.Zaehler = m.Aenderungszaehler; }
            if (!s.Je.TryGetValue(geschoss.Id, out var e)) s.Je[geschoss.Id] = e = Rechnen(m, k, geschoss);
            return e;
        }
    }

    public static SchnitthoeheSiaErgebnis SchnitthoeheNachSia(Projekt p, Element geschoss) => SchnitthoeheNachSia(p.Modell, p.Katalog, geschoss);

    // ------------------------------------------------------------------ Rechnen

    /// <summary>Höhenbereich einer Öffnung über OK Boden: [Von, Bis), Bis = +∞ ohne Höhe.</summary>
    sealed record Bereich(Element Element, bool Tuer, double Von, double Bis);

    static SchnitthoeheSiaErgebnis Rechnen(Modell m, Katalog k, Element geschoss)
    {
        double kote = geschoss.Zahl("kote") ?? 0;
        var jeWand = Wandgeometrie.OeffnungenJeWand(m, k);
        var liste = new List<Bereich>();
        foreach (var wand in m.Elemente.Where(e => k.IstArt(e, "Wand") && e.Geschoss == geschoss.Id))
        {
            // Wände ohne Dicke zeichnet der Grundriss nur als Achse (keine Öffnung): sie zählen nicht
            if (Wandgeometrie.Lage(m, wand) is not { Dicke: > 0 } w) continue;
            foreach (var o in Wandgeometrie.Oeffnungen(k, w, jeWand.GetValueOrDefault(wand.Id) ?? []))
            {
                // 0.01 mm: Rechenrauschen darf keine Scheinbereiche erzeugen
                double von = Math.Round(w.Unterkante + o.Unten - kote, 2);
                double bis = o.Oben is double ob ? Math.Round(w.Unterkante + ob - kote, 2) : double.PositiveInfinity;
                liste.Add(new Bereich(o.Element, o.IstTuer, von, bis));
            }
        }
        string g = geschoss.Kennzeichen;
        if (liste.Count == 0)
            return new SchnitthoeheSiaErgebnis(Vorgabe,
                $"{Norm}: im Geschoss {g} gibt es keine Türen und Fenster, durch die zu schneiden wäre – Vorgabe {Mm(Vorgabe)} mm über OK Boden.",
                [$"Geschoss {g} hat keine Türen und Fenster: Schnitthöhe {Mm(Vorgabe)} mm als Vorgabe, nicht aus Öffnungen gerechnet."], [], 0, 0, null, null);

        // Abschnitte [p_i, p_i+1) zwischen allen Grenzen; die Anzahl geschnittener Öffnungen ist in jedem Abschnitt konstant.
        var punkte = liste.SelectMany(b => new[] { b.Von, b.Bis }).Where(double.IsFinite).Distinct().OrderBy(x => x).ToList();
        int bestes = -1;
        var abschnitte = new List<(double A, double B, int N)>();
        for (int i = 0; i < punkte.Count; i++)
        {
            double a = punkte[i], b = i + 1 < punkte.Count ? punkte[i + 1] : double.PositiveInfinity;
            int n = liste.Count(x => x.Von <= a && a < x.Bis);
            abschnitte.Add((a, b, n));
            bestes = Math.Max(bestes, n);
        }
        // Unter den besten Abschnitten der Wert, der am nächsten bei 1000 mm liegt (bei Gleichstand der tiefere)
        var (wa, wb, _) = abschnitte.Where(x => x.N == bestes)
            .OrderBy(x => Math.Abs(Waehle(x.A, x.B) - Vorgabe)).ThenBy(x => Waehle(x.A, x.B)).First();
        double wert = Waehle(wa, wb);

        var geschnitten = liste.Where(x => x.Von <= wa && wa < x.Bis).ToList();
        var unten = geschnitten.MaxBy(x => x.Von)!;
        var oben = geschnitten.MinBy(x => x.Bis)!;
        var massgebend = new List<string> { $"{unten.Element.Kennzeichen} (untere Grenze {Mm(unten.Von)} mm, {Art(unten)})" };
        massgebend.Add(double.IsFinite(oben.Bis)
            ? $"{oben.Element.Kennzeichen} (obere Grenze {Mm(oben.Bis)} mm, {Art(oben)})"
            : "nach oben offen (alle geschnittenen Öffnungen ohne Höhe)");
        string bereich = double.IsFinite(wb) ? $"{Mm(wa)} … {Mm(wb)} mm" : $"ab {Mm(wa)} mm";
        var hinweise = new List<string>();
        string herkunft;
        if (bestes == liste.Count)
        {
            herkunft = $"{Norm}: Schnitt durch alle {liste.Count} Türen und Fenster des Geschosses {g}; gemeinsamer Bereich {bereich} über OK Boden, " +
                       $"davon der Wert am nächsten bei {Mm(Vorgabe)} mm, auf {Mm(Raster)} mm gerundet. Massgebend: {string.Join("; ", massgebend)}.";
        }
        else
        {
            var fehlen = liste.Where(x => !(x.Von <= wa && wa < x.Bis)).OrderBy(x => x.Element.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            herkunft = $"{Norm}: kein gemeinsamer Bereich aller {liste.Count} Türen und Fenster des Geschosses {g}; die Höhe schneidet {bestes} davon " +
                       $"(Bereich {bereich} über OK Boden, nächster Wert bei {Mm(Vorgabe)} mm). Massgebend: {string.Join("; ", massgebend)}.";
            hinweise.Add($"Nicht geschnitten bei {Mm(wert)} mm: " + string.Join(", ", fehlen.Select(x => $"{x.Element.Kennzeichen} ({Art(x)}, {Mm(x.Von)} … {(double.IsFinite(x.Bis) ? Mm(x.Bis) : "offen")} mm)")) +
                         ". Norm: «so zu wählen, dass durch alle Tür- und Fensteröffnungen geschnitten wird» – hier nicht erfüllbar.");
        }
        return new SchnitthoeheSiaErgebnis(wert, herkunft, hinweise, massgebend, liste.Count, bestes, wa, double.IsFinite(wb) ? wb : null);
    }

    /// <summary>Wert im Abschnitt [a, b), der am nächsten bei 1000 mm liegt, auf 10 mm gerundet und im Abschnitt geblieben. Ist der Abschnitt
    /// enger als 10 mm und enthält kein Vielfaches von 10, gilt seine Untergrenze.</summary>
    static double Waehle(double a, double b)
    {
        double tief = Math.Ceiling(a / Raster - 1e-9) * Raster;                          // kleinstes Vielfaches ≥ a
        double hoch = double.IsFinite(b) ? (Math.Ceiling(b / Raster - 1e-9) - 1) * Raster : double.PositiveInfinity;   // grösstes Vielfaches < b
        if (tief > hoch) return a;
        return Math.Clamp(Math.Round(Vorgabe / Raster) * Raster, tief, hoch);
    }

    static string Art(Bereich b) => b.Tuer ? "Tür" : "Fenster";

    static string Mm(double x) => Wandgeometrie.Mm(x);
}

/// <summary>Abfrage schnitthoehe_sia: gerechnete Schnitthöhe eines Geschosses bzw. einer Ansicht mit Herkunft (für die Eigenschaftenpalette).</summary>
public static class SchnitthoeheAbfragen
{
    public const string Text = "Schnitthöhe des Grundrisses nach SIA 400:2000 Ziff. C.3.1.1 (N21): aus den Höhenbereichen aller Türen und Fenster des Geschosses " +
        "(Tür von OK Boden, Fenster ab Brüstung) der Wert, der durch alle Öffnungen schneidet und am nächsten bei 1000 mm liegt; gibt es keinen " +
        "gemeinsamen Bereich, die Höhe mit den meisten geschnittenen Öffnungen samt Hinweis auf die übrigen. Mit ansicht zusätzlich, ob die Ansicht " +
        "nach SIA schneidet (schnitthoeheNachSia, Vorgabe ja), ihr Handwert (schnitthoehe) und die Höhe, die gilt. Masse in mm über OK Boden. Ändert nichts.";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("schnitthoehe_sia", Text, [new("ansicht", "text", false, "Grundriss (Kennzeichen): Wert, Schalter und geltende Höhe"), new("geschoss", "text", false, "Geschoss (Kennzeichen), wenn keine Ansicht genannt ist")]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "schnitthoehe_sia" ? Abfrage(p, Param.Text(par, "ansicht"), Param.Text(par, "geschoss")) : null;

    public static JsonObject Abfrage(Projekt p, string? ansicht, string? geschoss)
    {
        var m = p.Modell;
        Element? a = ansicht is null ? null : m.HoleDerKlasse(ansicht, "Ansicht");
        Element? g;
        if (a is not null)
        {
            g = m.Finde(a.Geschoss);
            if (a.Text("art") is { } art && art != "grundriss")
                throw new HlksFehler($"Ansicht {a.Kennzeichen} ist eine {art}-Ansicht; die Schnitthöhe gibt es nur im Grundriss.", "Einen Grundriss nennen.");
            if (g is null) throw new HlksFehler($"Grundriss {a.Kennzeichen} hat kein Geschoss.", "Geschoss der Ansicht setzen (element_aendern … geschoss).");
        }
        else if (geschoss is not null) g = m.HoleDerKlasse(geschoss, "Geschoss");
        else throw new HlksFehler("Weder Ansicht noch Geschoss genannt.", "ansicht (Grundriss) oder geschoss angeben.");

        var e = SchnitthoeheSia.SchnitthoeheNachSia(p, g);
        var o = new JsonObject
        {
            ["geschoss"] = g.Kennzeichen, ["norm"] = SchnitthoeheSia.Norm, ["wert"] = e.Wert, ["herkunft"] = e.Herkunft,
            ["hinweise"] = new JsonArray(e.Hinweise.Select(x => (JsonNode?)x).ToArray()), ["massgebend"] = new JsonArray(e.Massgebend.Select(x => (JsonNode?)x).ToArray()),
            ["oeffnungen"] = e.Oeffnungen, ["geschnitten"] = e.Geschnitten, ["von"] = e.Von, ["bis"] = e.Bis,
        };
        if (a is not null)
        {
            bool nachSia = SchnitthoeheSia.Aktiv(a);
            double hand = a.Zahl("schnitthoehe") ?? SchnitthoeheSia.Vorgabe;
            o["ansicht"] = a.Kennzeichen; o["nachSia"] = nachSia; o["handwert"] = hand; o["gilt"] = nachSia ? e.Wert : hand;
            o["geltendHerkunft"] = nachSia ? e.Herkunft : "von Hand gesetzt (schnitthoeheNachSia aus)";
        }
        return o;
    }
}
