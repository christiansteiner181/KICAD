// Quest App V1a, Vorgabe Christian 24.10.2026 (nach den Prüfbildern 01): frei schwebende App-Symbole – «die symbole müssen in griff weite sein. angewählt werden sie
// durch berührung. finger zeigen den pointer mit tooltip»; «symbole nicht in einer leiste sondern verteilt». Rein, ohne StereoKit; die Gesten in HebeGeste.cs, gezeichnet
// in src/Hlks.Quest/SymbolApps.cs.
//   - Lage der Symbole (AppSymbole.Plaetze): locker verteilt vor dem Körper in Griffweite, verschiedene Höhen, leicht gestaffelte Tiefe, keine gemeinsame Grundlinie;
//     je App immer derselbe Platz (Muskelgedächtnis). Mindestabstand: Symbole und Tooltips überlappen nie (Mitten weit über 60 pt, Apple HIG «Buttons»: «at least
//     60 pts apart»). Variante «symbole» = «verteilt» (Vorgabe) | «bogen» (zum Vergleich im Labor). Im Raum fest, nicht am Kopf.
//   - Zeiger mit Tooltip (Zeiger): ein gestreckter Zeigefinger nahe an einem Symbol bzw. Knopf zeigt einen Punkt darauf und eine feine Linie von der Fingerspitze;
//     nach einer Verweilzeit der Name als Tooltip unter dem Ziel. Apple HIG «Buttons» (visionOS): «the system displays a tooltip after people hover over a button for
//     a moment»; «Eyes»: «like when a tooltip appears below a button, a slightly longer delay can work well» (abgerufen 24.10.2026).
// Alle Zahlen sind Annahmen V1a (Stellschrauben symbole.*, zeiger.tooltip_s), keine Normwerte.

namespace Hlks.Xr.Bedienung;

/// <summary>Ein App-Symbol: Id der App, Lage (Kreis, Normale zum Auge).</summary>
public sealed record SymbolPlatz(string Id, TabletPose Lage);

public static class AppSymbole
{
    /// <summary>Durchmesser eines Symbols (pt), Annahme V1a: grosse runde Ziele für den Finger (visionOS-Startseite; Fingerziele grösser als 60 pt).</summary>
    public const double DurchmesserPt = 72;
    /// <summary>So weit unter ihrem Platz beginnen die Symbole beim Hineinheben (m).</summary>
    public const double HubM = 0.28;
    /// <summary>Tooltip: Höhe und Lücke unter dem Symbol (pt).</summary>
    public const double TooltipHoehePt = 30, TooltipLueckePt = 8;
    /// <summary>Ausblenden (s).</summary>
    public const double AusblendenS = 0.25;

    /// <summary>
    /// Plätze «verteilt» in m bei Streuung 1 (seitlich nach rechts, hoch, zum Auge hin) um die Mitte; je App fest. «ort» (Begehen) und «tisch» (Sitzungsraum) teilen
    /// sich einen Platz. Kleinster Mittenabstand 0.14 m (Durchmesser 6.5 cm, Tooltip darunter).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Vec3> Verteilt = new Dictionary<string, Vec3>(StringComparer.Ordinal)
    {
        ["status"] = new(-0.21, 0.07, 0.03),
        ["etagen"] = new(-0.08, 0.15, 0.0),
        ["messen"] = new(0.07, 0.08, -0.03),
        ["marker"] = new(0.20, 0.14, 0.02),
        ["ort"] = new(-0.13, -0.08, -0.02),
        ["tisch"] = new(-0.13, -0.08, -0.02),
        ["ebenen"] = new(0.14, -0.06, 0.01),
    };

    /// <summary>Reserveplätze für Apps ohne festen Platz (in dieser Reihenfolge).</summary>
    static readonly Vec3[] Reserve = [new(0.0, -0.02, 0.04), new(0.29, 0.03, -0.02), new(-0.30, -0.04, 0.0)];

    /// <summary>Durchmesser in m.</summary>
    public static double Durchmesser(GlasMasse m) => m.M(DurchmesserPt);

    /// <summary>
    /// Plätze der Symbole: Mitte <see cref="SymbolEinstellungen.AbstandM"/> vor dem Auge (waagrecht in Blickrichtung) und <see cref="SymbolEinstellungen.HoeheM"/>
    /// darunter; «verteilt» nach der Tabelle oben (× Streuung), «bogen» in einer Reihe im Bogen um das Auge (Mitten 0.15 m × Streuung). Jedes Symbol ist zum Auge gedreht.
    /// </summary>
    public static List<SymbolPlatz> Plaetze(IReadOnlyList<string> ids, Vec3 auge, double blickGieren, SymbolEinstellungen e, GlasMasse m)
    {
        var vor = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        var rechts = HebeGeste.Rechts(blickGieren);   // rechts vom Blick (Blick −z → rechts +x)
        var mitte = auge + vor * e.AbstandM + new Vec3(0, -e.HoeheM, 0);
        var d = Durchmesser(m);
        var aus = new List<SymbolPlatz>();
        int reserve = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            Vec3 p;
            if (e.Verteilung == Varianten.SymboleBogen)
            {
                var w = (i - (ids.Count - 1) / 2.0) * 0.15 * e.Streuung / Math.Max(0.1, e.AbstandM);
                var r = new Vec3(Math.Cos(blickGieren - w), 0, -Math.Sin(blickGieren - w));
                p = auge + r * e.AbstandM + new Vec3(0, -e.HoeheM, 0);
            }
            else
            {
                var v = Verteilt.TryGetValue(ids[i], out var f) ? f : Reserve[Math.Min(reserve++, Reserve.Length - 1)];
                p = mitte + (rechts * v.X + new Vec3(0, v.Y, 0) - vor * v.Z) * e.Streuung;
            }
            aus.Add(new SymbolPlatz(ids[i], XrTabletLogik.TabletLageAus(p, auge - p) with { B = d, H = d }));
        }
        return aus;
    }

    /// <summary>Mitte der Plätze (dort öffnet sich die gewählte App).</summary>
    public static Vec3 Mitte(IReadOnlyList<SymbolPlatz> p) =>
        p.Count == 0 ? Vec3.Null : p.Aggregate(Vec3.Null, (s, x) => s + x.Lage.Mitte) * (1.0 / p.Count);

    /// <summary>
    /// Hineinheben: Fortschritt f (0 … 1) der Geste bzw. der Bewegung danach → Versatz nach unten (m, positiv = tiefer) und Deckkraft je Symbol; jedes steigt etwas
    /// nach dem vorigen (Reihenfolge index), weich.
    /// </summary>
    public static (double Unten, double Deckkraft) Hineinheben(double f, int index, int anzahl)
    {
        var versatz = 0.06 * index;
        var k = GlasStil.Weich((Math.Clamp(f, 0, 1) * (1 + 0.06 * Math.Max(0, anzahl - 1)) - versatz));
        return ((1 - k) * HubM, k);
    }

    /// <summary>Beim Wegschieben laufen die Symbole mit so viel des Wegs der Hand zur Seite (mehr als die Hand: sie «fliegen» aus dem Bild), Annahme V1a.</summary>
    public const double SchiebenFaktor = 1.4;
    /// <summary>Nach dem Wegschieben gleiten sie noch so weit (m) nach aussen; ab so viel (m) seitlich sind sie ganz verblasst.</summary>
    public const double WegschiebenM = 0.6, VerblasstM = 0.7;

    /// <summary>
    /// Versatz eines Symbols zur Seite beim Wegschieben (m, positiv = nach rechts): links der Mitte (x &lt; 0) mit dem Weg der linken Hand nach links, rechts der Mitte
    /// mit dem der rechten nach rechts; nach dem Ereignis (danach 0 … 1) gleiten sie zusätzlich <see cref="WegschiebenM"/> weiter.
    /// </summary>
    public static double Seitlich(double x, double wegLinks, double wegRechts, double danach) =>
        x < 0 ? -(wegLinks * SchiebenFaktor + WegschiebenM * danach) : wegRechts * SchiebenFaktor + WegschiebenM * danach;

    /// <summary>Deckkraft nach dem Versatz zur Seite (1 = am Platz, 0 = ganz aus dem Bild geschoben).</summary>
    public static double SeitlichDeckkraft(double seitlich) => 1 - Math.Clamp(Math.Abs(seitlich) / VerblasstM, 0, 1);

    /// <summary>Breite eines Tooltips (m) für den Text (Schrift «klein», Rand je Seite 12 pt).</summary>
    public static double TooltipBreite(string text, GlasMasse m) => text.Length * m.Schrift(GlasStil.KleinPt) * 0.6 + m.M(24);

    /// <summary>Lage des Tooltips unter dem Ziel (HIG «Eyes»: «a tooltip appears below a button»), 4 mm davor.</summary>
    public static TabletPose TooltipLage(TabletPose ziel, string text, GlasMasse m)
    {
        double h = m.M(TooltipHoehePt), b = TooltipBreite(text, m);
        return ziel with { Mitte = ziel.Mitte - ziel.Oben * (ziel.H / 2 + m.M(TooltipLueckePt) + h / 2) + ziel.Normale * 0.004, B = b, H = h };
    }
}

/// <summary>Zustand des Zeigers einer Hand.</summary>
public sealed class ZeigerZustand
{
    internal int? Ziel;
    internal double Seit;
}

/// <summary>Bild des Zeigers: Ziel (Index), Punkt auf dem Ziel, Anfang der Linie (Fingerspitze), Nähe 0 … 1, Deckkraft des Tooltips 0 … 1.</summary>
public sealed record ZeigerBild(int Index, Vec3 Punkt, Vec3 Von, double Naehe, double Tooltip);

public static class Zeiger
{
    /// <summary>Ab so nah (m, vor der Fläche) zeigt der Zeiger, Annahme V1a.</summary>
    public const double NaeheM = 0.12;
    /// <summary>Seitlich darf die Spitze so weit neben dem Ziel sein (m).</summary>
    public const double RandM = 0.015;
    /// <summary>Einblenden des Tooltips (s).</summary>
    public const double EinblendenS = 0.15;
    /// <summary>Gestreckt: die Glieder des Zeigefingers weichen höchstens so weit voneinander ab (Grad), Annahme V1a.</summary>
    public const double GestrecktGrad = 35;

    /// <summary>Ist der Zeigefinger gestreckt (Grundglied und Spitze in einer Richtung)?</summary>
    public static bool ZeigefingerGestreckt(IReadOnlyDictionary<string, Vec3>? g)
    {
        if (g is null || !g.TryGetValue("index-finger-phalanx-proximal", out var a) || !g.TryGetValue("index-finger-phalanx-intermediate", out var b)
            || !g.TryGetValue("index-finger-phalanx-distal", out var c) || !g.TryGetValue("index-finger-tip", out var d)) return false;
        var u = b - a; var w = d - c;
        if (u.Laenge < 1e-6 || w.Laenge < 1e-6) return false;
        return u.Einheit().Skalar(w.Einheit()) >= Math.Cos(GestrecktGrad * Math.PI / 180);
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: spitze = Zeigefingerspitze (null = keine Hand), gestreckt = Zeigefinger gestreckt, ziele = Symbole bzw. Knöpfe; strahl = Zielstrahl
    /// der Hand (wenn der Finger nicht nah ist). Das nächste Ziel, vor dessen Fläche die Spitze höchstens <see cref="NaeheM"/> liegt (seitlich höchstens RandM daneben),
    /// sonst das erste, das der Strahl trifft. Der Tooltip erscheint nach tooltipS am selben Ziel.
    /// </summary>
    public static ZeigerBild? Schritt(ZeigerZustand z, double t, Vec3? spitze, bool gestreckt, IReadOnlyList<TabletPose> ziele, double tooltipS, (Vec3 U, Vec3 R)? strahl = null)
    {
        int? best = null;
        Vec3 punkt = default, von = default;
        double naehe = 0, bestD = double.PositiveInfinity;
        if (spitze is { } s && gestreckt)
            for (int i = 0; i < ziele.Count; i++)
            {
                var p = ziele[i];
                var d = (s - p.Mitte).Skalar(p.Normale);
                if (d < -0.02 || d > NaeheM) continue;
                var x = (s - p.Mitte).Skalar(p.Rechts);
                var y = (s - p.Mitte).Skalar(p.Oben);
                if (Math.Abs(x) > p.B / 2 + RandM || Math.Abs(y) > p.H / 2 + RandM) continue;
                if (d >= bestD) continue;
                bestD = d;
                best = i;
                punkt = p.Mitte + p.Rechts * Math.Clamp(x, -p.B / 2, p.B / 2) + p.Oben * Math.Clamp(y, -p.H / 2, p.H / 2) + p.Normale * 0.003;
                von = s;
                naehe = 1 - Math.Clamp(Math.Max(0, d) / NaeheM, 0, 1);
            }
        if (best is null && strahl is { } st && Glas.Strahl(ziele, st.U, st.R, 0.005) is { } tr)
        {
            best = tr.Index;
            punkt = st.U + st.R * tr.T;   // T in Einheiten von R (Glas.Strahl)
            von = st.U;
            naehe = 1;
        }
        if (best != z.Ziel) { z.Ziel = best; z.Seit = t; }
        if (best is not int b) return null;
        return new ZeigerBild(b, punkt, von, naehe, GlasStil.Weich((t - z.Seit - tooltipS) / EinblendenS));
    }
}
