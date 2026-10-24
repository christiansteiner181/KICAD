// Quest App V1b-1 (Modul XR, Gestenlabor): Merkmale je Bild und die neu abgetastete Merkmalsfolge eines Abschnitts. Rein, ohne StereoKit.
//
// Je Hand und Bild (Rohwerte, RohFolge): Handflächenmitte und -normale (Welt), Krümmung je Finger, Pinch normiert, verfolgt ja/nein; dazu der Gierwinkel des Kopfs.
//   Krümmung eines Fingers = (Bogen − Sehne) / Handgrösse: Bogen = Summe der Knochenlängen vom Ansatz bis zur Spitze (Daumen ab thumb-metacarpal, Finger ab
//     «metacarpal», damit das Grundgelenk mitzählt), Sehne = Ansatz bis Spitze. Gestreckt 0, Faust ≈ 0.6 … 1. Handgrösse wie XrTabletLogik.HandGroesse
//     (Handgelenk – Grundgelenk des Mittelfingers, ≈ 0.09 m). Fehlen Gelenke eines Fingers, zählt er als gestreckt (StereoKit liefert bei verfolgter Hand alle 25).
//   Pinch normiert = Pinch-Abstand / Handgrösse, gekappt bei PinchKappe (offene Hand ≈ 1.4 → 1, Pinch ≈ 0.1).
//
// Folge eines Abschnitts [t0, t1] (Merkmalart: rechte Hand, linke Hand, linke Hand gespiegelt, beide Hände), neu abgetastet auf N Stützpunkte (gleiche Zeitabstände,
// linear zwischen den Bildern):
//   - Körperbezug: Achsen aus dem Gierwinkel des Kopfs im letzten Bild des Abschnitts (rechts, oben, vorn; Nicken zählt nicht): x = rechts, y = oben, z = vorn.
//     Eine Hebegeste nach oben ist so «nach oben», egal wohin man schaut; eine Körperdrehung ändert die Merkmale nicht. (Das letzte Bild, weil alle Kandidaten
//     des Live-Erkenners jetzt enden: die Drehung in den Körperbezug geschieht so einmal je Auswertung für alle Kandidaten.)
//   - Lage = Handflächenmitte relativ zur Mitte im ersten Stützpunkt, im Körperbezug, geteilt durch die Ausdehnung der Bahn (grösster Abstand vom Start), mindestens
//     MindestAusdehnungM – so ist eine grössere oder kleinere Ausführung gleich, das Zittern einer ruhenden Hand bleibt aber klein.
//   - Normale der Handfläche im Körperbezug (Einheitsvektor, zwischen zwei Bildern linear).
//   - Krümmung je Finger (5) und Pinch (1).
//   - Grösse = ln(Ausdehnung / BezugAusdehnungM), in jedem Stützpunkt gleich (schwach gewichtet: grosse und kleine Bewegungen sind verwandt, aber nicht gleich).
//   - Gespiegelt (Händigkeit «beliebig»): x der Lage und der Normale mit umgekehrtem Vorzeichen; so gleicht die linke Hand der rechten.
//   - Beidhändig: beide Hände (je Lage, Normale, Krümmung, Pinch; Lage mit der grösseren Ausdehnung beider normiert), Grösse, Abstand der Handflächen (m) und
//     Richtung von links nach rechts (Körperbezug).
// Jeder Wert ist mit seinem Gewicht (GestenEinstellungen) vervielfacht; der Vergleich (GestenDtw) rechnet dann einfach euklidisch je Stützpunkt.
//
// Rechenweg in zwei Stufen: Zeilen (je Bild und Hand die 12 Rohwerte im Körperbezug, ungewichtet) und FolgeAusZeilen (abtasten, relativ zum Start, normieren,
// gewichten). Offline (Beispiele, Einordnen) rechnet beides je Abschnitt; der Live-Erkenner die Zeilen einmal je Auswertung.

namespace Hlks.Xr.Gesten;

/// <summary>Welche Hand bzw. Hände eine Merkmalsfolge beschreibt.</summary>
public enum Merkmalart { Rechts, Links, LinksGespiegelt, Beide }

/// <summary>Neu abgetastete Merkmalsfolge: Stützpunkte × Dimension Werte (zeilenweise).</summary>
public sealed record MerkmalFolge(Merkmalart Art, int Stuetzpunkte, int Dimension, double[] Werte)
{
    public double Wert(int stuetzpunkt, int k) => Werte[stuetzpunkt * Dimension + k];
}

public static class GestenMerkmale
{
    /// <summary>Werte je Stützpunkt für eine Hand: Lage 3, Normale 3, Krümmung 5, Pinch 1, Grösse 1.</summary>
    public const int DimEineHand = 13;
    /// <summary>Werte je Stützpunkt beidhändig: 2 × 12, Grösse 1, Abstand 1, Richtung 3.</summary>
    public const int DimBeide = 29;

    /// <summary>Spalten innerhalb des Blocks einer Hand.</summary>
    public const int SpalteLage = 0, SpalteNormale = 3, SpalteKruemmung = 6, SpaltePinch = 11;

    /// <summary>Rohwerte je Bild und Hand in den Zeilen: Lage 3, Normale 3, Krümmung 5, Pinch 1.</summary>
    internal const int ZeileBreite = 12;

    public static int Dimension(Merkmalart art) => art == Merkmalart.Beide ? DimBeide : DimEineHand;

    /// <summary>Hand einer Merkmalart (null = beide).</summary>
    public static HandSeite? Seite(Merkmalart art) => art switch
    {
        Merkmalart.Rechts => HandSeite.Rechts,
        Merkmalart.Links or Merkmalart.LinksGespiegelt => HandSeite.Links,
        _ => null,
    };

    /// <summary>Die Merkmalarten, mit denen der Erkenner eine Geste dieser Händigkeit im Strom sucht.</summary>
    public static IReadOnlyList<Merkmalart> Arten(Haendigkeit h) => h switch
    {
        Haendigkeit.Rechts => [Merkmalart.Rechts],
        Haendigkeit.Links => [Merkmalart.Links],
        Haendigkeit.Beliebig => [Merkmalart.Rechts, Merkmalart.LinksGespiegelt],
        _ => [Merkmalart.Beide],
    };

    /// <summary>Gelenkketten (Indizes in HandGelenke.WebXr) vom Ansatz bis zur Spitze: Daumen, Zeige-, Mittel-, Ring-, kleiner Finger.</summary>
    static readonly int[][] Ketten = [[1, 2, 3, 4], [5, 6, 7, 8, 9], [10, 11, 12, 13, 14], [15, 16, 17, 18, 19], [20, 21, 22, 23, 24]];
    const int Handgelenk = 0, MittelfingerGrund = 11;

    /// <summary>Handgrösse (m): Handgelenk bis Grundgelenk des Mittelfingers; fehlen sie, <see cref="GestenEinstellungen.HandGroesseVorgabeM"/>.</summary>
    public static double HandGroesse(HandBild h, GestenEinstellungen? e = null)
    {
        var vorgabe = (e ?? GestenEinstellungen.Vorgabe).HandGroesseVorgabeM;
        if (h.Gelenke.Count < HandBild.GelenkAnzahl) return vorgabe;
        var a = h.Gelenke[Handgelenk];
        var b = h.Gelenke[MittelfingerGrund];
        if (!a.IstEndlich || !b.IstEndlich) return vorgabe;
        var g = a.Abstand(b);
        return g > 0.02 && g < 0.3 ? g : vorgabe;
    }

    /// <summary>Krümmung der fünf Finger ((Bogen − Sehne) / Handgrösse; Daumen zuerst), siehe Dateikopf.</summary>
    public static double[] Kruemmung(HandBild h, GestenEinstellungen? e = null)
    {
        var k = new double[5];
        Kruemmung(h, HandGroesse(h, e), k);
        return k;
    }

    internal static void Kruemmung(HandBild h, double groesse, Span<double> ziel)
    {
        ziel[..5].Clear();
        if (h.Gelenke.Count < HandBild.GelenkAnzahl) return;
        ReadOnlySpan<Vec3> g = h.Gelenke is Vec3[] feld ? feld : [.. h.Gelenke];
        for (var f = 0; f < 5; f++)
        {
            var kette = Ketten[f];
            double bogen = 0;
            var ok = true;
            for (var j = 0; j + 1 < kette.Length && ok; j++)
            {
                Vec3 a = g[kette[j]], b = g[kette[j + 1]];
                if (!a.IstEndlich || !b.IstEndlich) ok = false;
                else bogen += a.Abstand(b);
            }
            if (!ok) continue;
            var sehne = g[kette[0]].Abstand(g[kette[^1]]);
            ziel[f] = Math.Max(0, bogen - sehne) / groesse;
        }
    }

    /// <summary>Pinch-Abstand in Handgrössen, gekappt bei <see cref="GestenEinstellungen.PinchKappe"/>; ohne Abstand die Kappe (offen).</summary>
    public static double PinchNormiert(HandBild h, GestenEinstellungen? e = null)
    {
        var es = e ?? GestenEinstellungen.Vorgabe;
        return PinchNormiert(h, HandGroesse(h, es), es);
    }

    internal static double PinchNormiert(HandBild h, double groesse, GestenEinstellungen e) =>
        double.IsFinite(h.PinchAbstand) && h.PinchAbstand >= 0 ? Math.Min(h.PinchAbstand / groesse, e.PinchKappe) : e.PinchKappe;

    /// <summary>Vektor im Körperbezug zum Gierwinkel: (rechts, oben, vorn).</summary>
    public static Vec3 Koerperbezug(Vec3 v, double gieren) => new(v.Skalar(XrGesten.Rechts(gieren)), v.Y, v.Skalar(XrGesten.Vorn(gieren)));

    /// <summary>Merkmalsfolge einer ganzen Aufnahme (siehe Dateikopf); null, wenn eine nötige Hand in einem Bild fehlt oder die Aufnahme zu kurz ist.</summary>
    public static MerkmalFolge? Folge(Aufnahme a, Merkmalart art, GestenEinstellungen? e = null)
    {
        var es = (e ?? GestenEinstellungen.Vorgabe).Geprueft();
        if (!a.ZeitenGueltig()) return null;
        var r = RohFolge.Aus(a, es);
        var werte = new double[es.Stuetzpunkte * Dimension(art)];
        return Folge(r, a.Start, a.Ende, art, es, werte) ? new MerkmalFolge(art, es.Stuetzpunkte, Dimension(art), werte) : null;
    }

    /// <summary>Braucht die Merkmalart die linke bzw. rechte Hand?</summary>
    internal static (bool Links, bool Rechts) Haende(Merkmalart art) => (art != Merkmalart.Rechts, art is Merkmalart.Rechts or Merkmalart.Beide);

    /// <summary>
    /// Merkmalsfolge des Abschnitts [t0, t1] aus den Rohwerten (siehe Dateikopf) in ziel (Stützpunkte × Dimension). false, wenn der Abschnitt nicht in den Rohwerten
    /// liegt oder eine nötige Hand in einem Bild des Abschnitts nicht verfolgt ist.
    /// </summary>
    internal static bool Folge(RohFolge r, double t0, double t1, Merkmalart art, GestenEinstellungen e, Span<double> ziel)
    {
        if (r.Anzahl < 2 || !(t1 > t0) || t0 < r.T(0) - 1e-9 || t1 > r.T(r.Anzahl - 1) + 1e-9) return false;
        var i0 = Math.Max(0, r.Suche(t0));
        var iEnde = Math.Max(0, r.Suche(t1));
        var i1 = r.T(iEnde) < t1 - 1e-9 && iEnde + 1 < r.Anzahl ? iEnde + 1 : iEnde;
        var (links, rechts) = Haende(art);
        for (var i = i0; i <= i1; i++)
            if (links && !r.Ok(0, i) || rechts && !r.Ok(1, i)) return false;
        var zeilen = new double[(i1 - i0 + 1) * 2 * ZeileBreite];
        Zeilen(r, i0, i1, r.Gieren(iEnde), links, rechts, zeilen);
        FolgeAusZeilen(r, i0, i1, zeilen, t0, t1, art, e, ziel);
        return true;
    }

    /// <summary>
    /// Rohwerte der Bilder von … bis (logische Indizes) im Körperbezug zum Gierwinkel, ungewichtet: je Bild j und Hand h ab ((j − von) · 2 + h) · 12 Lage (absolut,
    /// gedreht), Normale, Krümmung (5), Pinch. Nur die verlangten Hände.
    /// </summary>
    internal static void Zeilen(RohFolge r, int von, int bis, double gieren, bool links, bool rechts, double[] ziel)
    {
        Vec3 re = XrGesten.Rechts(gieren), vo = XrGesten.Vorn(gieren);
        for (var i = von; i <= bis; i++)
            for (var h = 0; h < 2; h++)
            {
                if (h == 0 ? !links : !rechts) continue;
                var o = ((i - von) * 2 + h) * ZeileBreite;
                var m = r.Mitte(h, i);
                var n = r.Normale(h, i);
                ziel[o] = m.Skalar(re);
                ziel[o + 1] = m.Y;
                ziel[o + 2] = m.Skalar(vo);
                ziel[o + 3] = n.Skalar(re);
                ziel[o + 4] = n.Y;
                ziel[o + 5] = n.Skalar(vo);
                for (var f = 0; f < 5; f++) ziel[o + 6 + f] = r.Kruemmung(h, i, f);
                ziel[o + 11] = r.Pinch(h, i);
            }
    }

    /// <summary>
    /// Merkmalsfolge des Abschnitts [t0, t1] aus den Zeilen der Bilder von … bis (siehe <see cref="Zeilen"/>; der Abschnitt muss darin liegen). Die Verfolgung der
    /// Hände prüft der Aufrufer.
    /// </summary>
    internal static void FolgeAusZeilen(RohFolge r, int von, int bis, double[] z, double t0, double t1, Merkmalart art, GestenEinstellungen e, Span<double> ziel)
    {
        int n = e.Stuetzpunkte, d = Dimension(art);
        var spiegel = art == Merkmalart.LinksGespiegelt;
        var beide = art == Merkmalart.Beide;
        var hand = art == Merkmalart.Rechts ? 1 : 0;
        double wN = e.GewichtNormale, wK = e.GewichtKruemmung, wP = e.GewichtPinch;
        Span<double> start = stackalloc double[6];
        Span<double> jetzt = stackalloc double[6];
        Span<double> w = stackalloc double[ZeileBreite];
        Span<double> gewicht = [1, 1, 1, wN, wN, wN, wK, wK, wK, wK, wK, wP];
        double ausdehnung = 0;
        var k = Math.Clamp(r.Suche(t0), von, bis);
        double tk = r.T(k), tk2 = k < bis ? r.T(k + 1) : tk;
        var haende = beide ? 2 : 1;
        for (var s = 0; s < n; s++)
        {
            var ts = s == n - 1 ? t1 : t0 + (t1 - t0) * s / (n - 1);
            while (k < bis && tk2 <= ts)
            {
                k++;
                tk = tk2;
                tk2 = k < bis ? r.T(k + 1) : tk;
            }
            var k2 = k < bis ? k + 1 : k;
            var dt = tk2 - tk;
            var a = dt > 0 ? Math.Clamp((ts - tk) / dt, 0, 1) : 0;
            var zeile = ziel.Slice(s * d, d);
            for (var b = 0; b < haende; b++)
            {
                var h = beide ? b : hand;
                ReadOnlySpan<double> z1 = z.AsSpan(((k - von) * 2 + h) * ZeileBreite, ZeileBreite);
                ReadOnlySpan<double> z2 = z.AsSpan(((k2 - von) * 2 + h) * ZeileBreite, ZeileBreite);
                for (var c = 0; c < ZeileBreite; c++) w[c] = z1[c] + (z2[c] - z1[c]) * a;
                if (spiegel) { w[0] = -w[0]; w[3] = -w[3]; }
                if (s == 0) { start[3 * b] = w[0]; start[3 * b + 1] = w[1]; start[3 * b + 2] = w[2]; }
                jetzt[3 * b] = w[0]; jetzt[3 * b + 1] = w[1]; jetzt[3 * b + 2] = w[2];
                w[0] -= start[3 * b];
                w[1] -= start[3 * b + 1];
                w[2] -= start[3 * b + 2];
                var l = Math.Sqrt(w[0] * w[0] + w[1] * w[1] + w[2] * w[2]);
                if (l > ausdehnung) ausdehnung = l;
                var block = zeile.Slice(b * 12, 12);
                for (var c = 0; c < ZeileBreite; c++) block[c] = gewicht[c] * w[c];
            }
            if (beide)
            {
                double vx = jetzt[3] - jetzt[0], vy = jetzt[4] - jetzt[1], vz = jetzt[5] - jetzt[2];
                var l = Math.Sqrt(vx * vx + vy * vy + vz * vz);
                zeile[25] = e.GewichtHandabstand * l;
                var q = l > 1e-9 ? e.GewichtHandrichtung / l : 0;
                zeile[26] = q * vx;
                zeile[27] = q * vy;
                zeile[28] = q * vz;
            }
        }
        var massstab = Math.Max(ausdehnung, e.MindestAusdehnungM);
        var fl = e.GewichtLage / massstab;
        var groesse = e.GewichtGroesse * Math.Log(massstab / e.BezugAusdehnungM);
        for (var s = 0; s < n; s++)
        {
            var o = s * d;
            for (var j = 0; j < 3; j++) ziel[o + SpalteLage + j] *= fl;
            if (beide)
            {
                for (var j = 0; j < 3; j++) ziel[o + 12 + SpalteLage + j] *= fl;
                ziel[o + 24] = groesse;
            }
            else ziel[o + 12] = groesse;
        }
    }
}

/// <summary>
/// Rohwerte je Bild als Ringpuffer (fest grosse Felder, keine Zuteilung je Bild): Zeit, Gierwinkel, je Hand verfolgt, Mitte, Normale, Krümmung (5), Pinch normiert.
/// Logische Indizes 0 = ältestes Bild. Für eine Aufnahme (offline) wie für den Strom (GestenLiveErkenner).
/// </summary>
internal sealed class RohFolge
{
    readonly int kap;
    int kopf;
    readonly double[] t, gieren, pinch, kr;
    readonly bool[] ok;
    readonly Vec3[] mitte, normale;

    public int Anzahl { get; private set; }
    public int Kapazitaet => kap;

    public RohFolge(int kapazitaet)
    {
        kap = Math.Max(2, kapazitaet);
        t = new double[kap];
        gieren = new double[kap];
        pinch = new double[2 * kap];
        ok = new bool[2 * kap];
        mitte = new Vec3[2 * kap];
        normale = new Vec3[2 * kap];
        kr = new double[2 * kap * 5];
    }

    public static RohFolge Aus(Aufnahme a, GestenEinstellungen e)
    {
        var r = new RohFolge(a.Bilder.Count);
        foreach (var b in a.Bilder) r.Hinzu(b, e);
        return r;
    }

    /// <summary>Physischer Index (ohne Division: logisch 0 … Anzahl − 1 liegt höchstens eine Runde weiter).</summary>
    int P(int i)
    {
        var p = kopf + i;
        return p >= kap ? p - kap : p;
    }

    public double T(int i) => t[P(i)];
    public double Gieren(int i) => gieren[P(i)];
    public bool Ok(int h, int i) => ok[h * kap + P(i)];
    public Vec3 Mitte(int h, int i) => mitte[h * kap + P(i)];
    public Vec3 Normale(int h, int i) => normale[h * kap + P(i)];
    public double Pinch(int h, int i) => pinch[h * kap + P(i)];
    public double Kruemmung(int h, int i, int f) => kr[(h * kap + P(i)) * 5 + f];

    /// <summary>Hängt ein Bild an; ist der Puffer voll, fällt das älteste weg.</summary>
    public void Hinzu(GestenBild b, GestenEinstellungen e)
    {
        if (Anzahl == kap) { kopf = P(1); Anzahl--; }
        var p = P(Anzahl);
        Anzahl++;
        t[p] = b.T;
        gieren[p] = b.Gieren;
        Span<double> k = stackalloc double[5];
        for (var h = 0; h < 2; h++)
        {
            var hand = h == 0 ? b.Links : b.Rechts;
            var q = h * kap + p;
            var brauchbar = hand.Brauchbar;
            ok[q] = brauchbar;
            if (!brauchbar)
            {
                mitte[q] = Vec3.Null;
                normale[q] = Vec3.Hoch;
                pinch[q] = e.PinchKappe;
                for (var f = 0; f < 5; f++) kr[q * 5 + f] = 0;
                continue;
            }
            var groesse = GestenMerkmale.HandGroesse(hand, e);
            mitte[q] = hand.Mitte;
            normale[q] = hand.Normale.Einheit();
            pinch[q] = GestenMerkmale.PinchNormiert(hand, groesse, e);
            GestenMerkmale.Kruemmung(hand, groesse, k);
            for (var f = 0; f < 5; f++) kr[q * 5 + f] = k[f];
        }
    }

    /// <summary>Entfernt die n ältesten Bilder.</summary>
    public void Entfernen(int n)
    {
        n = Math.Clamp(n, 0, Anzahl);
        kopf = (kopf + n) % kap;
        Anzahl -= n;
    }

    public void Leeren() { kopf = 0; Anzahl = 0; }

    /// <summary>Grösster logischer Index mit T ≤ t (−1, wenn keiner).</summary>
    public int Suche(double zeit)
    {
        int lo = 0, hi = Anzahl - 1, r = -1;
        while (lo <= hi)
        {
            var m = (lo + hi) >> 1;
            if (T(m) <= zeit + 1e-12) { r = m; lo = m + 1; }
            else hi = m - 1;
        }
        return r;
    }
}
