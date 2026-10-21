// Leitungen (Modul 3, B1): Anschlusslagen für ALLE Elemente (entwurf/modul-3.md, Grundsatzentscheid 4) – eine zentrale Funktion
// für verbinden, die Prüfungen P13–P15, die Abfrage «anschlusslagen» und das Routing (B2).
//   - Leitung (Rohr, Kanal): anfang = erster Punkt, Richtung vom zweiten zum ersten Punkt (nach aussen); ende = letzter Punkt,
//     Richtung vom vorletzten zum letzten. Grösse aus den Eigenschaften (Rohr dn; Rohr einer Abwasser-Serie nach Aussendurchmesser
//     durchmesser = «d 110», Nachtrag entwurf/abwasser-serie.md; Kanal durchmesser bzw. breite und hoehe).
//   - Element mit Familie: Lage und Richtung aus der Familie in der Einbaulage (Einfügepunkt, ausrichtung bzw. drehung),
//     Grösse aus dem Familienanschluss.
//   - Element ohne Familie und nicht platziertes Element (ohne Geometrie): Lage und Richtung null – nur logisch verbindbar.
// ANNAHMEN (Prüfliste): Elemente ohne Familie (ausser Leitungen) haben auch keine Grösse – Formteile ohne Familie tragen nur die
//   grösste Nennweite (Katalog «dn, Nennweite (grösste)»), ihre Anschlussgrösse ist nicht verlässlich; eine Leitung, deren
//   Endpunkte zusammenfallen, hat dort keine Richtung (und damit keine Lage für die Prüfung); ein Kanal ohne Serie nimmt
//   durchmesser, sonst breite und hoehe.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ein Anschluss mit Lage (Welt, mm) und Richtung (Welt, Einheitsvektor, aus dem Element heraus) – beide null, wenn das
/// Element keine Lage dafür hat – und Grösse (leer = unbekannt). Quelle: leitung | familie | ohne_familie | nicht_platziert |
/// familie_ungueltig.</summary>
public sealed record AnschlussStelle(string Name, string Medium, string Fluss, double[]? Lage, double[]? Richtung, Leitungsgroesse Groesse,
    string? Art, string? Form, string? Gewinde, string Quelle)
{
    public bool HatLage => Lage is not null && Richtung is not null;
    public bool HatGroesse => !Groesse.Leer;
}

public static class Anschlusslagen
{
    /// <summary>Zwei verbundene Anschlüsse liegen höchstens so weit auseinander (mm, Grundsatzentscheid 4).</summary>
    public const double AbstandMax = 1.0;
    /// <summary>Höchste Abweichung der Richtungen von gegenläufig (Grad).</summary>
    public const double WinkelMax = 1.0;

    /// <summary>Alle Anschlüsse eines Elements (Reihenfolge wie am Element). Scheitert nie.</summary>
    public static List<AnschlussStelle> Von(Modell m, Katalog k, Element e)
    {
        if (k.IstArt(e, "Leitung")) return Leitung(m, k, e);
        if (Familien.FamilieVon(m, e) is { } f) return Familie(m, k, e, f);
        var quelle = e.Bezuege.ContainsKey(Familien.Bezug) ? "familie_ungueltig" : e.Geometrie is null ? "nicht_platziert" : "ohne_familie";
        return e.Anschluesse.Select(a => Ohne(m, e, a, quelle)).ToList();
    }

    /// <summary>Ein Anschluss eines Elements (Gross/Kleinschreibung egal) oder null.</summary>
    public static AnschlussStelle? Stelle(Modell m, Katalog k, Element e, string anschluss) =>
        Von(m, k, e).FirstOrDefault(s => s.Name.Equals(anschluss, StringComparison.OrdinalIgnoreCase));

    static AnschlussStelle Ohne(Modell m, Element e, Anschluss a, string quelle) =>
        new(a.Name, Netz.Medium(m, e, a), a.Fluss, null, null, default, null, null, a.Gewinde, quelle);

    /// <summary>Grösse einer Leitung aus ihren Eigenschaften (Rohr dn; Rohr einer Abwasser-Serie nach Aussendurchmesser durchmesser «d 110»,
    /// die DN des Herstellers zählt dort nicht; Rohr ohne dn mit durchmesser ebenso; Kanal nach Serie bzw. durchmesser, sonst breite × hoehe).</summary>
    public static Leitungsgroesse LeitungGroesse(Modell m, Katalog k, Element e)
    {
        if (!k.IstArt(e, "Kanal"))
        {
            if (NachAussendurchmesser(m, k, e)) return e.Zahl("durchmesser") is double da ? Leitungsgroesse.Aussen(da) : default;
            if (e.Zahl("dn") is double dn) return Leitungsgroesse.Rohr(dn);
            return e.Zahl("durchmesser") is double d0 ? Leitungsgroesse.Aussen(d0) : default;
        }
        var rechteck = Leitungsserie.VonLeitung(m, k, e)?.Querschnitt == "rechteck";
        if (!rechteck && e.Zahl("durchmesser") is double d) return Leitungsgroesse.Rund(d);
        if (e.Zahl("breite") is double b && e.Zahl("hoehe") is double h) return Leitungsgroesse.Rechteck(b, h);
        return default;
    }

    /// <summary>Die Leitung gehört zu einer gültigen Abwasser-Serie nach Aussendurchmesser (leitung.einsatz abwasser, Grösse d).</summary>
    public static bool NachAussendurchmesser(Modell m, Katalog k, Element leitung) =>
        Familien.FamilieVon(m, leitung) is { } f && Familien.Definition(f, k) is { Gueltig: true, Leitung.NachAussendurchmesser: true };

    static List<AnschlussStelle> Leitung(Modell m, Katalog k, Element e)
    {
        var groesse = LeitungGroesse(m, k, e);
        var pkt = e.Geometrie?.Punkte.Where(p => p.Length >= 2).Select(p => new[] { p[0], p[1], p.Length > 2 ? p[2] : 0 }).ToList();
        var form = k.IstArt(e, "Kanal") ? (groesse.D is not null ? "rund" : groesse.B is not null ? "rechteck" : null) : "rund";
        var art = k.IstArt(e, "Kanal") ? "kanal" : "rohr";
        var l = new List<AnschlussStelle>();
        foreach (var a in e.Anschluesse)
        {
            double[]? lage = null, richtung = null;
            if (pkt is { Count: >= 2 })
            {
                if (a.Name.Equals("anfang", StringComparison.OrdinalIgnoreCase)) (lage, richtung) = (pkt[0], Richtung(pkt[1], pkt[0]));
                else if (a.Name.Equals("ende", StringComparison.OrdinalIgnoreCase)) (lage, richtung) = (pkt[^1], Richtung(pkt[^2], pkt[^1]));
            }
            if (richtung is null) lage = null;
            l.Add(new AnschlussStelle(a.Name, Netz.Medium(m, e, a), a.Fluss, lage, richtung, groesse, art, form, a.Gewinde,
                e.Geometrie is null ? "nicht_platziert" : "leitung"));
        }
        return l;
    }

    /// <summary>Einheitsvektor von a nach b; null, wenn die Punkte zusammenfallen.</summary>
    static double[]? Richtung(double[] a, double[] b)
    {
        var d = new V3(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
        return d.Laenge < 1e-9 ? null : Familien.Runden(d.Einheit().Feld(), 12);
    }

    static List<AnschlussStelle> Familie(Modell m, Katalog k, Element e, Element f)
    {
        var def = Familien.Definition(f, k);
        FamAufloesung? a = null;
        if (def.Gueltig && k.IstArt(e, def.Kategorie))
        {
            var x = FamilienAuswertung.Aufloesen(def, Familien.Typ(e), Familien.Exemplarwerte(e));
            if (x.Gueltig) a = x;
        }
        if (a is null) return e.Anschluesse.Select(x => Ohne(m, e, x, "familie_ungueltig")).ToList();
        var lage = Familien.Einbaulage(e);
        var l = new List<AnschlussStelle>();
        foreach (var an in e.Anschluesse)
        {
            var fa = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals(an.Name, StringComparison.OrdinalIgnoreCase));
            if (fa is null) { l.Add(Ohne(m, e, an, "familie_ungueltig")); continue; }
            l.Add(new AnschlussStelle(an.Name, Netz.Medium(m, e, an), an.Fluss,
                lage is null ? null : Familien.Runden(lage.Welt(fa.Lage)),
                lage is null ? null : Familien.Runden(V3.Aus(lage.Richtung(fa.Richtung)).Einheit().Feld(), 12),
                Leitungsgroesse.Von(fa), fa.Art, fa.Form, fa.Gewinde, lage is null ? "nicht_platziert" : "familie"));
        }
        return l;
    }

    /// <summary>Abstand der Lagen (mm) und Abweichung der Richtungen von gegenläufig (Grad: Winkel zwischen Richtung A und −Richtung B);
    /// null, wenn eine Seite keine Lage hat.</summary>
    public static (double Abstand, double Winkel)? Abweichung(AnschlussStelle a, AnschlussStelle b)
    {
        if (!a.HatLage || !b.HatLage) return null;
        var abstand = Geometrie.Abstand(a.Lage!, b.Lage!);
        var ra = V3.Aus(a.Richtung!).Einheit(); var rb = V3.Aus(b.Richtung!).Einheit();
        var winkel = Math.Acos(Math.Clamp(ra.Mal(-rb), -1, 1)) * 180 / Math.PI;
        return (abstand, winkel);
    }

    /// <summary>Beide haben eine Lage und liegen aufeinander (≤ 1 mm), Richtungen gegenläufig (≤ 1°); bei Grössen auf beiden Seiten
    /// auch gleiche Grösse.</summary>
    public static bool PasstRaeumlich(AnschlussStelle a, AnschlussStelle b) =>
        Abweichung(a, b) is { } x && x.Abstand <= AbstandMax && x.Winkel <= WinkelMax && (!a.HatGroesse || !b.HatGroesse || a.Groesse.Gleich(b.Groesse));

    /// <summary>Anschluss als JSON für Abfragen.</summary>
    public static JsonObject AlsJson(AnschlussStelle s) => new()
    {
        ["name"] = s.Name, ["medium"] = s.Medium, ["fluss"] = s.Fluss, ["art"] = s.Art, ["form"] = s.Form,
        ["dn"] = s.Groesse.Dn, ["d"] = s.Groesse.D, ["b"] = s.Groesse.B, ["h"] = s.Groesse.H, ["gewinde"] = s.Gewinde,
        ["lage"] = s.Lage is null ? null : Json.Knoten(s.Lage), ["richtung"] = s.Richtung is null ? null : Json.Knoten(Familien.Runden(s.Richtung, 9)),
        ["quelle"] = s.Quelle,
    };
}
