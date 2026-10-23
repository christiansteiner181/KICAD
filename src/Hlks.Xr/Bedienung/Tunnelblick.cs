// Quest App P5a: Komfort – Tunnelblick beim Springen, Drehen und beim Etagenwechsel (Ergänzung Christian 23.10.2026, entwurf/modul-xr.md «Komfort-Stellschrauben»:
// «Beim Gehen engt sich das Blickfeld leicht ein (Stärke einstellbar)»). Die App bewegt sich nur in Sprüngen und Drehschritten (kein stetiges Gehen); dort engt
// sich das Blickfeld kurz und weich ein: Einblenden AnS, Halten HaltenS, Ausblenden AusS. Stellschrauben «komfort.tunnelblick» (an/aus) und
// «komfort.tunnelblick_staerke» (0 … 1: wie eng). Rein, ohne StereoKit; die App zeichnet einen Ring um den Blick (innen frei, aussen dunkel).
//
// ENTSCHEID P5a: Mit Tunnelblick ersetzt er beim Drehen das kurze Abblenden aus P4a; beim Springen bleibt das Abblenden (es verdeckt den Schnitt, Christian:
// «Klappt gut»), der Tunnel klingt danach weich aus. Ohne Tunnelblick gilt P4a unverändert.
// Werte (Zeiten, Öffnung 60° … 15° um die Blickachse, weicher Rand 18°, Deckkraft 0.95) sind Annahmen P5a, keine Norm.

namespace Hlks.Xr.Bedienung;

/// <summary>Zustand des Tunnelblicks: Beginn des letzten Ereignisses und seine Stärke.</summary>
public sealed class TunnelZustand
{
    public double Start { get; internal set; } = double.NegativeInfinity;
    public double Spitze { get; internal set; }
}

/// <summary>Form des Tunnels: frei bis InnenGrad um die Blickachse, dunkel ab AussenGrad, Deckkraft des dunklen Teils.</summary>
public readonly record struct TunnelForm(double InnenGrad, double AussenGrad, double Deckkraft);

public static class Tunnelblick
{
    public const double AnS = 0.08, HaltenS = 0.12, AusS = 0.35;
    /// <summary>Freier Blick ohne Einengen bzw. bei voller Stärke (Grad um die Blickachse) und Breite des weichen Rands.</summary>
    public const double OffenGrad = 60, EngGrad = 15, RandGrad = 18;
    public const double DeckkraftMax = 0.95;

    public static double Dauer => AnS + HaltenS + AusS;

    /// <summary>Ein Sprung, eine Drehung oder ein Etagenwechsel: der Tunnel beginnt (nur wenn eingeschaltet und Stärke &gt; 0).</summary>
    public static void Ausloesen(TunnelZustand z, double t, KomfortEinstellungen k)
    {
        if (!k.Tunnelblick || !(k.Staerke > 0)) return;
        z.Start = t;
        z.Spitze = Math.Clamp(k.Staerke, 0, 1);
    }

    static double Glatt(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>Verlauf 0 … 1 (weich ein, halten, weich aus) zur Zeit t.</summary>
    public static double Verlauf(TunnelZustand z, double t)
    {
        var d = t - z.Start;
        if (!(d >= 0) || d > Dauer) return 0;
        if (d < AnS) return Glatt(d / AnS);
        if (d < AnS + HaltenS) return 1;
        return 1 - Glatt((d - AnS - HaltenS) / AusS);
    }

    /// <summary>Form zur Zeit t; null, wenn kein Tunnel zu zeichnen ist. Je stärker (und je mitten im Verlauf), desto enger und dunkler.</summary>
    public static TunnelForm? Form(TunnelZustand z, double t)
    {
        var h = Verlauf(z, t);
        var s = z.Spitze * h;
        if (!(s > 1e-4)) return null;
        var innen = OffenGrad - (OffenGrad - EngGrad) * s;
        return new TunnelForm(innen, innen + RandGrad, DeckkraftMax * h);
    }
}
