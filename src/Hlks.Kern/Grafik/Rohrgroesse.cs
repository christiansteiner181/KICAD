// Grösse eines Rohrs – die eine gemeinsame Funktion für Beschriftung und Exporte (Modul 3, B9b).
// Entscheide Christian Steiner 19.10.2026:
//   Abwasser:  «Praxisorientiert werden Ablaufrohre immer mit dem Aussendurchmesser definiert. Diese müssten so beschrieben werden
//              (PE-S 110, 90, …). Dies ist auch bei den Exporten so zu übernehmen. DA = 110 → 110.»
//   Übrige:    «Alle anderen Rohre können mit ihrer NW oder DN definiert werden.» (Wasser, Heizung, Kälte; NW bzw. DN nach der Einstellung
//              rohrbezeichnung der Ansicht, sonst des Projekts, Vorgabe NW; ersetzt «CNS Ø22» aus B9: jetzt «CNS NW 20»)
//
//   Abwasser    Beschriftung «{kuerzel} {da}» («PE-S 110»), ohne Kürzel «110»; Grösse in Listen und Exporten «110» (mm, ohne Ø, ohne NW/DN)
//   übrige      Beschriftung «{kuerzel} {NW|DN} {dn}» («CNS NW 20»), ohne Kürzel «NW 20»; Grösse in Listen und Exporten «NW 20»
//
// Abwasser = Leitung in einem System, dessen Medium zu den Systemtypen der Schema-Vorlage «abwasser» gehört (Schmutzwasser, Regenwasser
// – SchemaBefehle.Vorlagen, Katalog systemtypen.json). Das Medium kommt aus System.medium bzw. dem Systemtyp (SchemaBefehle.MediumVon),
// bei Leitungen ohne eigenes System aus dem System des Strangs bzw. der verbundenen Bauteile.
//
// Woher die Zahl kommt (nie umgerechnet – Normwerte):
//   da:  1. Typ der Leitungsserie (Aussendurchmesser der Grösse), 2. Eigenschaft Leitung.da (Katalog; IFC-Import mit Herkunft).
//   dn:  1. Typ der Leitungsserie (Grössenschlüssel dn), 2. Eigenschaft Leitung.dn.
//   Nie da aus DN gerechnet und nie DN aus da; fehlt die Zahl, gibt es keine Grösse (Beschriftung null) und einen Hinweis. Nie auf Ø ausweichen.
// Kürzel: Block leitung.kuerzel der Serie. Rohre ohne Serie (Fremddatei): bei Abwasser das Material, wenn die Datei es nennt (Herkunft ifc) und
// es selbst ein Kürzel ist (kurz, Grossbuchstaben, z. B. «PE-S») – sonst keines.
// Kanäle und Lüftungsleitungen sind keine Rohre (Leitungsbezeichnung: «400×200», «Ø 250»).

using System.Globalization;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Grösse eines Rohrs. <see cref="Wert"/> null = keine Grösse (siehe <see cref="Hinweis"/>).</summary>
public sealed record Rohrgroesse(bool Abwasser, string Art, double? Wert, string? Kuerzel, string Quelle, string? Hinweis)
{
    /// <summary>Grösse für Listen und Exporte: Abwasser «110» (Aussendurchmesser in mm), sonst «NW 20» bzw. «DN 20»; null ohne Zahl.</summary>
    public string? Groesse => Wert is double v ? (Abwasser ? Rohrgroessen.Zahl(v, 1) : $"{Art} {Rohrgroessen.Zahl(v, 2)}") : null;

    /// <summary>Beschriftung im Plan: «{kuerzel} {Grösse}», ohne Kürzel nur die Grösse; null ohne Zahl.</summary>
    public string? Beschriftung => Groesse is { } g ? (Kuerzel is { Length: > 0 } k ? $"{k} {g}" : g) : null;
}

/// <summary>Die gemeinsame Funktion (<see cref="Von"/>) und ihre Hilfen.</summary>
public static class Rohrgroessen
{
    // Kürzel, das die Datei selbst als Material nennt: 2 bis 6 Zeichen, nur Grossbuchstaben und Ziffern, optional ein Bindestrich
    static readonly Regex KuerzelMuster = new(@"^[A-Z][A-Z0-9]{1,4}(-[A-Z0-9]{1,3})?$", RegexOptions.Compiled);

    /// <summary>Zahl für die Anzeige: ganzzahlig ohne Nachkommastellen, sonst bis <paramref name="stellen"/> Stellen mit Punkt.</summary>
    public static string Zahl(double v, int stellen) =>
        Math.Round(v, stellen, MidpointRounding.AwayFromZero).ToString(stellen == 1 ? "0.#" : "0.##", CultureInfo.InvariantCulture);

    /// <summary>Medien der Abwasser-Systemtypen (Schema-Vorlage «abwasser», Katalog systemtypen.json).</summary>
    public static HashSet<string> AbwasserMedien(Katalog k)
    {
        var r = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in SchemaBefehle.Vorlagen["abwasser"])
            if (k.Systemtypen.TryGetValue(t, out var st) && !string.IsNullOrEmpty(st.Medium)) r.Add(st.Medium);
        return r;
    }

    /// <summary>System der Leitung: eigenes, sonst das des Strangs, sonst das erste der verbundenen Bauteile.</summary>
    static Element? SystemVon(Modell m, Element e) =>
        m.Finde(e.System) ?? (m.Finde(e.TeilVon) is { } st ? (m.Finde(st.System) ?? m.Finde(st.TeilVon)) : null) ?? Netz.SystemeVon(m, e).FirstOrDefault();

    /// <summary>Leitung der Entwässerung (Medium Schmutz- oder Regenwasser)?</summary>
    public static bool IstAbwasser(Projekt p, Element e)
    {
        if (SystemVon(p.Modell, e) is not { Klasse: "System" } sys) return false;
        return SchemaBefehle.MediumVon(p.Katalog, sys) is { } medium && AbwasserMedien(p.Katalog).Contains(medium);
    }

    /// <summary>Die gemeinsame da-Quelle: Aussendurchmesser (mm) aus dem Typ der Leitungsserie, sonst aus Leitung.da, mit Quelle; null, wenn
    /// beides fehlt. Nie aus DN gerechnet. Für Abwasser die Grösse, für die Bausteine im Einlageplan («KW: B.n.o Ø16») jedes Rohr.</summary>
    public static (double Da, string Quelle)? Aussendurchmesser(Projekt p, Element e)
    {
        if (HlksMasse.Serie(p, e) is { Kategorie: "Rohr" } s && s.Werte.Aussen is double sa && sa > 0)
            return (sa, $"Serie {s.SerieName}, Typ {s.Typ}");
        return e.Zahl("da") is double da && da > 0 ? (da, "Leitung.da") : null;
    }

    /// <summary>Beschriftung aus den Einzelteilen (für die Abfrage einer Leitungsserie, die ihr Medium nicht kennt).</summary>
    public static string? Beschriftung(bool abwasser, string art, string? kuerzel, double? wert) => new Rohrgroesse(abwasser, art, wert, kuerzel, "–", null).Beschriftung;

    /// <summary>Kürzel eines Rohrs ohne Serie aus dem Material, wenn die Fremddatei es nennt (Herkunft ifc) und es selbst ein Kürzel ist
    /// («PE-S»); ein von Hand eingetragenes Material («PE») ist keine Angabe der Datei und zählt nicht. Sonst null.</summary>
    public static string? MaterialKuerzel(Element e) =>
        e.Eigenschaft("material") is { Herkunft.Art: "ifc" } && e.Text("material")?.Trim() is { } t && KuerzelMuster.IsMatch(t) ? t : null;

    /// <summary>Die Grösse eines Rohrs (Entscheid 19.10.2026, siehe Dateikopf). <paramref name="ansicht"/> bestimmt NW oder DN (null:
    /// Projekt, Vorgabe NW). Für Kanäle und Lüftungsleitungen nicht gedacht (<see cref="Leitungsbezeichnung.IstLuft"/>).</summary>
    public static Rohrgroesse Von(Projekt p, Element? ansicht, Element e)
    {
        var serie = HlksMasse.Serie(p, e) is { Kategorie: "Rohr" } s ? s : null;
        if (IstAbwasser(p, e))
        {
            var kuerzel = serie is not null ? serie.Kuerzel : MaterialKuerzel(e);
            if (Aussendurchmesser(p, e) is var (da, quelle))
                return new(true, "da", da, kuerzel, quelle, null);
            return new(true, "da", null, kuerzel, "–", "Abwasserleitung ohne Aussendurchmesser (da): weder die Serie noch die Leitung nennt ihn – keine Grösse, kein DN als Ersatz.");
        }
        var art = Leitungsbezeichnung.Art(p.Modell, ansicht).Art;
        var kz = serie?.Kuerzel;
        if (serie is not null && serie.Werte.Groesse.TryGetValue("dn", out var sd) && sd > 0)
            return new(false, art, sd, kz, $"Serie {serie.SerieName}, Typ {serie.Typ}", null);
        if (HlksEinordnung.Dn(e) is double dn)
            return new(false, art, dn, kz, "Leitung.dn", null);
        return new(false, art, null, kz, "–", "Rohr ohne Nennweite (DN): weder die Serie noch die Leitung nennt sie – keine Grösse, kein Ø als Ersatz.");
    }

    /// <summary>Hinweise zu Leitungen ohne Grösse, zusammengefasst («3 Abwasserleitungen ohne Aussendurchmesser (da) …»). Kanäle und
    /// Lüftungsleitungen zählen nicht. Leer, wenn alle Rohre eine Grösse haben.</summary>
    public static List<string> Hinweise(Projekt p, IEnumerable<Element> leitungen)
    {
        int ohneDa = 0, ohneDn = 0;
        foreach (var l in leitungen)
        {
            if (Leitungsbezeichnung.IstLuft(p, l)) continue;
            var g = Von(p, null, l);
            if (g.Wert is not null) continue;
            if (g.Abwasser) ohneDa++; else ohneDn++;
        }
        var r = new List<string>();
        if (ohneDa > 0)
            r.Add($"{ohneDa} Abwasserleitung{(ohneDa == 1 ? "" : "en")} ohne Aussendurchmesser (da): keine Grösse – da in der Serie bzw. an der Leitung nachtragen (nie aus DN gerechnet).");
        if (ohneDn > 0)
            r.Add($"{ohneDn} Rohr{(ohneDn == 1 ? "" : "e")} ohne Nennweite (DN): keine Grösse – DN in der Serie bzw. an der Leitung nachtragen (nie aus dem Aussendurchmesser gerechnet).");
        return r;
    }
}
