// Das Tablet beruhigen (Rückmeldung nach dem ersten Brillentest, 21.10.2026: «tablet ist etwas nervös, sollte etwas stabilisiert werden»).
// Quelle: wwwroot/js/ansichten/xr-greifen-logik.js, Abschnitt «Beruhigen des Tablets» (Node-Test xr-tablet-ruhe.test.mjs): One-Euro-Filter, Glättung der
// Handflächen-Normale, Totzone, Nachführen. Alle Werte stellt Christian nach dem nächsten Brillentest nach (in der App als Stellschrauben).
//
// Konventionen: Szene in Metern (Y oben); Winkel in Grad; Zeiten in Sekunden. Der Zustand gehört zu einer Sitzung und wird beim Öffnen des Tablets neu angelegt.

namespace Hlks.Xr;

/// <summary>Parameter eines One-Euro-Filters: kleinste Grenzfrequenz (Hz), Zuwachs je Geschwindigkeit (beta) und Grenzfrequenz der Geschwindigkeit (Hz).</summary>
public sealed record EinEuroParameter(double MinCut, double Beta, double DCut);

/// <summary>Zustand eines One-Euro-Filters für Vektoren: gefilterter Wert, letzter Rohwert, gefilterte Geschwindigkeit.</summary>
public sealed class EinEuroZustand
{
    public Vec3? X { get; set; }
    public Vec3 Roh { get; set; }
    public Vec3 Dx { get; set; }
}

/// <summary>Zustand der Beruhigung (xr-greifen-logik.js neuerTabletZustand): Filter von Mitte, Normale und Handflächen-Normale; Folgt = das Tablet führt gerade nach.</summary>
public sealed class TabletZustand
{
    public EinEuroZustand? Mitte { get; set; }
    public EinEuroZustand? Normale { get; set; }
    public EinEuroZustand? Palm { get; set; }
    public bool Folgt { get; set; }
}

public static class XrTabletRuhe
{
    /// <summary>xr-greifen-logik.js TABLET_TOTZONE_M: weicht die Mitte weniger ab, bleibt das Tablet stehen (m).</summary>
    public const double TotzoneM = 0.015;
    /// <summary>xr-greifen-logik.js TABLET_TOTZONE_GRAD: dasselbe für die Normale des Tablets (Grad).</summary>
    public const double TotzoneGrad = 4;
    /// <summary>xr-greifen-logik.js TABLET_RUHE_ANTEIL: das Nachführen endet, sobald die Abweichung unter diesem Anteil der Totzone liegt.</summary>
    public const double RuheAnteil = 0.3;
    /// <summary>xr-greifen-logik.js TABLET_FOLGE_ZEIT: Zeitkonstante (s), mit der das Tablet beim Nachführen das gefilterte Ziel einholt.</summary>
    public const double FolgeZeit = 0.04;
    /// <summary>xr-greifen-logik.js TABLET_FILTER_MITTE: One-Euro-Filter der Tablet-Mitte. Langsam bei kleinen, zügig bei grossen Bewegungen.</summary>
    public static readonly EinEuroParameter FilterMitte = new(1.2, 40, 3);
    /// <summary>xr-greifen-logik.js TABLET_FILTER_NORMALE: One-Euro-Filter der Tablet-Normale (Einheitsvektor).</summary>
    public static readonly EinEuroParameter FilterNormale = new(1.2, 8, 3);
    /// <summary>xr-greifen-logik.js TABLET_FILTER_PALM: One-Euro-Filter der Handflächen-Normale (Hauptquelle des Zitterns: Hebel TabletAbstand).</summary>
    public static readonly EinEuroParameter FilterPalm = new(0.8, 8, 3);

    /// <summary>xr-greifen-logik.js neuerTabletZustand: neuer, leerer Zustand der Beruhigung.</summary>
    public static TabletZustand NeuerTabletZustand() => new();

    /// <summary>
    /// xr-greifen-logik.js einEuroVektor: One-Euro-Filter für Vektoren; liefert den gefilterten Wert. Der erste Wert geht unverändert durch.
    /// dt wird auf 1 ms … 100 ms begrenzt.
    /// </summary>
    public static Vec3 EinEuroVektor(EinEuroZustand z, Vec3 wert, double dt, EinEuroParameter par)
    {
        var d = Math.Min(0.1, Math.Max(1e-3, dt));
        if (z.X is not { } x)
        {
            z.X = wert;
            z.Roh = wert;
            z.Dx = Vec3.Null;
            return wert;
        }
        double Alpha(double fc) => 1 / (1 + 1 / (2 * Math.PI * fc) / d);
        var geschw = (wert - z.Roh) * (1 / d);
        z.Dx += (geschw - z.Dx) * Alpha(par.DCut);
        var fc = par.MinCut + par.Beta * z.Dx.Laenge;
        z.X = x + (wert - x) * Alpha(fc);
        z.Roh = wert;
        return z.X.Value;
    }

    /// <summary>xr-greifen-logik.js tabletPalmGlaetten: Normale der Handfläche vor <see cref="XrTabletLogik.TabletLage"/> glätten (Zustand je Sitzung); liefert einen Einheitsvektor.</summary>
    public static Vec3 TabletPalmGlaetten(TabletZustand zustand, Vec3 normale, double dt)
    {
        zustand.Palm ??= new EinEuroZustand();
        return EinEuroVektor(zustand.Palm, normale.Einheit(), dt, FilterPalm).Einheit();
    }

    /// <summary>xr-greifen-logik.js winkelGrad: Winkel (Grad) zwischen zwei Richtungen.</summary>
    public static double WinkelGrad(Vec3 a, Vec3 b) => Math.Acos(Math.Min(1, Math.Max(-1, a.Einheit().Skalar(b.Einheit())))) / Num.Grad;

    /// <summary>
    /// xr-greifen-logik.js tabletNachfuehren: beruhigt die Lage des Tablets. alt = bisher gezeigte Lage (null: erste Lage, sofort übernommen), neu = Soll aus
    /// <see cref="XrTabletLogik.TabletLage"/>, dt in s. Das Soll wird je Bild gefiltert (One-Euro); das Tablet bleibt stehen, solange das gefilterte Soll weniger als die Totzone
    /// (<see cref="TotzoneM"/>, <see cref="TotzoneGrad"/>) abweicht, folgt darüber weich und bleibt wieder stehen, sobald es angekommen ist (Hysterese <see cref="RuheAnteil"/>).
    /// </summary>
    public static TabletPose TabletNachfuehren(TabletPose? alt, TabletPose neu, double dt, TabletZustand zustand)
    {
        if (alt is null || zustand.Mitte is null)
        {
            zustand.Folgt = false;
            zustand.Mitte = new EinEuroZustand();
            zustand.Normale = new EinEuroZustand();
            EinEuroVektor(zustand.Mitte, neu.Mitte, dt, FilterMitte);
            EinEuroVektor(zustand.Normale, neu.Normale, dt, FilterNormale);
            return neu;
        }
        var d = Math.Min(0.1, Math.Max(1e-3, dt));
        var fm = EinEuroVektor(zustand.Mitte, neu.Mitte, d, FilterMitte);
        var fn = EinEuroVektor(zustand.Normale ??= new EinEuroZustand(), neu.Normale, d, FilterNormale).Einheit();
        var abwM = fm.Abstand(alt.Mitte);
        var abwN = WinkelGrad(fn, alt.Normale);
        var ueber = abwM > TotzoneM || abwN > TotzoneGrad;
        var ruhig = abwM < TotzoneM * RuheAnteil && abwN < TotzoneGrad * RuheAnteil;
        if (ueber) zustand.Folgt = true;
        else if (ruhig) zustand.Folgt = false;
        if (!zustand.Folgt) return alt;
        var k = 1 - Math.Exp(-d / FolgeZeit);
        return XrTabletLogik.TabletLageAus(alt.Mitte + (fm - alt.Mitte) * k, alt.Normale + (fn - alt.Normale) * k);
    }
}
