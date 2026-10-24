// Quest App P5b (Modul XR, «Ergänzungen» ins Interface-Labor, von Christian übernommen am 23.10.2026): Röntgen-Hand beim Begehen.
//   modul-xr.md: «Wohin die offene Hand zeigt, werden Wände und Decken durchsichtig, dahinter erscheinen die Leitungen.» Auftrag P5b: «Die offene Hand
//   (Handfläche nach vorn, Finger gestreckt) macht Wände und Decken in einem Kegel um ihre Zeigerichtung durchscheinend (wie der Durchgang, nur auf Distanz), so
//   dass dahinter liegende Leitungen und Räume sichtbar werden. Stellschrauben roentgen.winkel_grad, roentgen.reichweite_m.»
// Rein, ohne StereoKit. Die App ruft je Hand und Bild Schritt (Geste) und mit dem Kegel RoentgenVerfolger.Pruefen auf und gibt die Elemente zusammen mit denen
// des Durchgangs (NetzDurchgang) an die Darstellung: dieselben durchscheinenden Netze, dieselbe Deckkraft (Durchgang.DurchgangDeckkraft).
//
// Geste (ANNAHMEN P5b, benannte Konstanten unten): offene Hand (XrTabletLogik.FaustVerhaeltnis über OffenAb 1.65, solange aktiv über OffenHaltenAb 1.45),
// kein Pinch, die Normale der Handfläche (aus ihr heraus) höchstens VornAnGrad = 50° neben der Richtung vom Kopf zur Hand (Handfläche nach vorn, vom Körper weg –
// die «Halt»-Hand auf Brusthöhe liegt bei etwa 40°; solange aktiv bis VornHaltenGrad = 65°). Durch die Decke zeigt man mit der erhobenen Hand, Handfläche nach
// oben. Ein nach AnS = 0.2 s Halten, aus nach AusS = 0.25 s ohne Geste – kurzes Zittern schaltet nicht. Hängende Hände (Handfläche zur Seite bzw. nach hinten),
// die Handfläche zum Gesicht (Apps holen) und der Pinch (Springen) zählen nicht.
// Kegel: Spitze in der Mitte der Handfläche, Achse = Normale der Handfläche (die Zeigerichtung der offenen Hand), Öffnungswinkel roentgen.winkel_grad (ganz,
// Vorgabe 40°, also ±20°), Länge roentgen.reichweite_m (Vorgabe 6 m).
// Welche Bauteile: dieselben wie im Durchgang (ModellSzene.DurchgangsBauteile: Wände und Decken, in der IFC-Unterlage IfcWall, IfcSlab …), ohne den Boden unter
// den Füssen (Bauteile ganz unter Standboden + BodenM = 5 cm; sonst würde der Boden vor einem durchscheinend, sobald der Kegel ihn streift). Ein Bauteil gilt als
// im Kegel, wenn eine Kette von Kugeln entlang der Achse es trifft (Abstand SchrittM = 0.25 m, Radius so, dass jede Kugel den Kegel bis zur nächsten deckt) –
// eine Näherung, die eher etwas zu viel als zu wenig durchscheinen lässt; vorher fällt alles weg, dessen Hüllkugel den Kegel nicht berührt.

namespace Hlks.Xr.Modell;

/// <summary>Stellschrauben der Röntgen-Hand (Annahmen P5b).</summary>
public sealed record RoentgenEinstellungen
{
    /// <summary>«roentgen.winkel_grad»: Öffnungswinkel des Kegels, ganz (Grad). Vorgabe 40° (±20° um die Zeigerichtung).</summary>
    public double WinkelGrad { get; init; } = 40;
    /// <summary>«roentgen.reichweite_m»: so weit reicht der Kegel von der Hand (m). Vorgabe 6 m – etwa ein bis zwei Wände weit.</summary>
    public double ReichweiteM { get; init; } = 6;

    public static readonly RoentgenEinstellungen Vorgabe = new();
}

/// <summary>Kegel der Röntgen-Hand in der Szene: Spitze, Achse (Einheit), halber Öffnungswinkel (rad) und Länge (m).</summary>
public sealed record RoentgenKegel(Vec3 Spitze, Vec3 Achse, double HalbwinkelRad, double ReichweiteM)
{
    /// <summary>Liegt der Punkt im Kegel (zwischen Spitze und Reichweite, innerhalb des Winkels)?</summary>
    public bool Enthaelt(Vec3 p)
    {
        var v = p - Spitze;
        var s = v.Skalar(Achse);
        if (s < 0 || s > ReichweiteM) return false;
        return (v - Achse * s).Laenge <= s * Math.Tan(HalbwinkelRad) + 1e-12;
    }

    /// <summary>Abstand eines Punkts zur Mantelfläche des unendlichen Kegels (≤ 0 innen).</summary>
    public double AbstandMantel(Vec3 p)
    {
        var v = p - Spitze;
        var s = v.Skalar(Achse);
        var rho = (v - Achse * s).Laenge;
        double c = Math.Cos(HalbwinkelRad), si = Math.Sin(HalbwinkelRad);
        return s * c + rho * si < 0 ? v.Laenge : rho * c - s * si;
    }
}

/// <summary>Zustand einer Hand für <see cref="RoentgenHand.Schritt"/>.</summary>
public sealed class RoentgenZustand
{
    public bool Aktiv { get; internal set; }
    public RoentgenKegel? Kegel { get; internal set; }
    internal double? GesteSeit, OhneSeit;
}

public static class RoentgenHand
{
    /// <summary>Solange die Röntgen-Hand aktiv ist, gilt die Hand bis zu diesem Verhältnis als offen (Hysterese zu XrTabletLogik.OffenAb).</summary>
    public const double OffenHaltenAb = 1.45;
    /// <summary>Handfläche nach vorn: höchstens so weit (Grad) neben der Richtung vom Kopf zur Hand; solange aktiv <see cref="VornHaltenGrad"/>.</summary>
    public const double VornAnGrad = 50, VornHaltenGrad = 65;
    /// <summary>Ein nach so langem Halten der Geste, aus nach so langer Zeit ohne (s).</summary>
    public const double AnS = 0.2, AusS = 0.25;
    /// <summary>Abstand der Kugeln entlang der Achse (m).</summary>
    public const double SchrittM = 0.25;

    /// <summary>Zeigt die Hand die Geste (offen, Handfläche nach vorn)? halten = schon aktiv (Hysterese). Gelenke und Kopf in derselben Welt.</summary>
    public static bool Geste(IReadOnlyDictionary<string, Vec3>? g, string seite, Vec3 kopf, bool halten)
    {
        if (XrTabletLogik.FaustVerhaeltnis(g) is not { } v || v < (halten ? OffenHaltenAb : XrTabletLogik.OffenAb)) return false;
        if (XrTabletLogik.IstPinch(g)) return false;
        if (XrTabletLogik.HandflaechenNormale(g, seite) is not { } n || n.Laenge < 0.5 || XrTabletLogik.HandflaechenMitte(g) is not { } m) return false;
        var vor = (m - kopf).Einheit();
        if (vor.Laenge < 0.5) return false;
        return Math.Acos(Math.Clamp(n.Skalar(vor), -1, 1)) * 180 / Math.PI <= (halten ? VornHaltenGrad : VornAnGrad);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: Geste erkennen (mit Ein- und Ausschaltzeit) und den Kegel bilden. gelenke und kopf in der Szene (null = Hand nicht erkannt).
    /// Gibt den Kegel zurück, solange die Röntgen-Hand aktiv ist, sonst null.
    /// </summary>
    public static RoentgenKegel? Schritt(RoentgenZustand z, double t, IReadOnlyDictionary<string, Vec3>? gelenke, string seite, Vec3 kopf, RoentgenEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? RoentgenEinstellungen.Vorgabe;
        var geste = Geste(gelenke, seite, kopf, z.Aktiv);
        if (geste)
        {
            z.OhneSeit = null;
            z.GesteSeit ??= t;
            if (!z.Aktiv && t - z.GesteSeit.Value >= AnS - 1e-9) z.Aktiv = true;
        }
        else
        {
            z.GesteSeit = null;
            if (z.Aktiv)
            {
                z.OhneSeit ??= t;
                if (t - z.OhneSeit.Value >= AusS - 1e-9) { z.Aktiv = false; z.OhneSeit = null; }
            }
        }
        if (!z.Aktiv) { z.Kegel = null; return null; }
        // aktiv: der Kegel folgt der Hand; ohne Geste (kurz) bleibt der letzte
        if (geste && XrTabletLogik.HandflaechenMitte(gelenke) is { } m && XrTabletLogik.HandflaechenNormale(gelenke, seite) is { } n)
            z.Kegel = new RoentgenKegel(m, n.Einheit(), Math.Clamp(e.WinkelGrad, 1, 179) / 2 * Math.PI / 180, Math.Max(0.1, e.ReichweiteM));
        return z.Kegel;
    }

    /// <summary>Radius der Kugel bei Weg s entlang der Achse: deckt den Kegel bis zur nächsten Kugel (s + SchrittM/2).</summary>
    public static double KugelRadius(RoentgenKegel k, double s) => Num.Hypot(SchrittM / 2, (s + SchrittM / 2) * Math.Tan(k.HalbwinkelRad));

    /// <summary>Berührt die Hüllkugel der Box den Kegel (Vorauswahl)?</summary>
    public static bool Moeglich(RoentgenKegel k, Box3 box)
    {
        if (box.IstLeer) return false;
        var c = Vec3.Mitte(box.Min, box.Max);
        var r = box.Max.Abstand(box.Min) / 2;
        var s = (c - k.Spitze).Skalar(k.Achse);
        if (s < -r || s > k.ReichweiteM + r) return false;
        return k.AbstandMantel(c) <= r;
    }

    /// <summary>Trifft der Kegel das Bauteil (Form in der Szene, Hüllbox)? Kette von Kugeln entlang der Achse (siehe Dateikopf).</summary>
    public static bool Trifft(RoentgenKegel k, Form form, Box3 box)
    {
        if (!Moeglich(k, box)) return false;
        for (var s = SchrittM / 2; s < k.ReichweiteM + 1e-9; s += SchrittM)
        {
            var p = k.Spitze + k.Achse * s;
            var r = KugelRadius(k, s);
            if (box.Abstand(p) > r) continue;
            if (Durchgang.AbstandZurForm(p, form) <= r) return true;
        }
        return false;
    }
}

/// <summary>
/// Welche Bauteile die Röntgen-Hand gerade durchscheinend macht (wie <see cref="DurchgangVerfolger"/>, mit dem Kegel statt der Kopfposition). Neu gerechnet wird
/// nur, wenn sich der Kegel merklich bewegt hat (Spitze <see cref="BewegungM"/>, Achse <see cref="DrehungGrad"/>, Winkel, Reichweite, Geschoss).
/// </summary>
public sealed class RoentgenVerfolger(ModellSzene szene)
{
    public const double BewegungM = 0.03, DrehungGrad = 2;
    /// <summary>Bauteile ganz unter Standboden + so viel (m) bleiben (der Boden unter den Füssen).</summary>
    public const double BodenM = 0.05;
    readonly Dictionary<string, DreieckForm?> formen = new(StringComparer.Ordinal);
    HashSet<string> nah = new(StringComparer.Ordinal);
    RoentgenKegel? letzter;
    string? letztesGeschoss;
    double? letzterBoden;

    /// <summary>Schlüssel der Bauteile im Kegel (SzenenElement.Schluessel).</summary>
    public IReadOnlySet<string> Nah => nah;
    /// <summary>Wie viele Bauteile bei der letzten Rechnung genau gemessen wurden (nach der Vorauswahl).</summary>
    public int Gemessen { get; private set; }

    /// <summary>
    /// Prüft den Kegel (null = keine Röntgen-Hand); geschossId = nur Bauteile dieses Geschosses (null = alle); boden = Standboden (Szene y; Bauteile ganz darunter
    /// zählen nicht). true, wenn sich die Menge geändert hat.
    /// </summary>
    public bool Pruefen(RoentgenKegel? kegel, string? geschossId = null, double? boden = null)
    {
        if (kegel is null)
        {
            letzter = null;
            if (nah.Count == 0) return false;
            nah = new(StringComparer.Ordinal);
            return true;
        }
        if (letzter is { } l && geschossId == letztesGeschoss && boden == letzterBoden && l.HalbwinkelRad == kegel.HalbwinkelRad && l.ReichweiteM == kegel.ReichweiteM
            && l.Spitze.Abstand(kegel.Spitze) < BewegungM && l.Achse.Skalar(kegel.Achse) > Math.Cos(DrehungGrad * Math.PI / 180)) return false;
        letzter = kegel;
        letztesGeschoss = geschossId;
        letzterBoden = boden;
        var neu = new HashSet<string>(StringComparer.Ordinal);
        int gemessen = 0;
        foreach (var e in szene.DurchgangsBauteile)
        {
            if (geschossId is not null && e.GeschossId != geschossId) continue;
            if (boden is { } b && e.Box.Max.Y <= b + BodenM) continue;
            if (!RoentgenHand.Moeglich(kegel, e.Box)) continue;
            gemessen++;
            if (!formen.TryGetValue(e.Schluessel, out var f)) formen[e.Schluessel] = f = DreieckForm.Aus([.. szene.Ecken(e)]);
            if (f is not null && RoentgenHand.Trifft(kegel, f, e.Box)) neu.Add(e.Schluessel);
        }
        Gemessen = gemessen;
        if (neu.SetEquals(nah)) return false;
        nah = neu;
        return true;
    }
}
