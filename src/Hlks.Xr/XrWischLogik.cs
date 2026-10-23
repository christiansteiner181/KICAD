// Wischgeste (XR-C, Masslinien wegwischen). Rückmeldung Brillentest 21.10.2026: «masslinien nach einem mass nach wischgeste löschen».
// Quelle: wwwroot/js/ansichten/xr-greifen-logik.js, Abschnitt «Wischgeste» (Node-Test xr-wisch.test.mjs).
// Offene Hand, schnelle, überwiegend waagrechte Bewegung der Handflächenmitte; wo der Weg der Hand eine stehende Masslinie streift, verschwindet diese Messung
// («wie an der Wandtafel»). Ein Wisch ins Leere tut nichts. Alle Schwellen sind Annahmen: Christian stellt sie nach dem nächsten Brillentest nach
// (in der App als Stellschrauben: Mindestweg, Mindesttempo, Richtungstoleranz).
//
// Konventionen: Szene in Metern (Y oben); Zeiten in Sekunden.

namespace Hlks.Xr;

/// <summary>Messpunkt im Verlauf einer Hand: Zeit (s) und Ort (Handflächenmitte).</summary>
public readonly record struct WischProbe(double T, Vec3 P);

/// <summary>Erkannter Wisch: Weg (vom Anfang bis jetzt), Sehne, Dauer, Tempo, waagrechte Richtung; Neu = erstes Bild dieses Wischs.</summary>
public sealed record WischErgebnis(IReadOnlyList<Vec3> Weg, double Strecke, double Dauer, double V, Vec3 Richtung, bool Neu = false);

/// <summary>Masslinie als Strecke von A nach B (Szene, m).</summary>
public readonly record struct Masslinie(Vec3 A, Vec3 B);

/// <summary>Zustand der Wischerkennung einer Hand (xr-greifen-logik.js neuerWischer).</summary>
public sealed class Wischer
{
    public List<WischProbe> Verlauf { get; } = [];
    public bool Aktiv { get; set; }
    public double SperreBis { get; set; } = double.NegativeInfinity;
}

public static class XrWischLogik
{
    /// <summary>xr-greifen-logik.js WISCH_WEG_M: Mindestweg (Sehne) innerhalb von <see cref="WischZeitS"/> (m).</summary>
    public const double WischWegM = 0.15;
    /// <summary>xr-greifen-logik.js WISCH_ZEIT_S: längstes Fenster, in dem der Weg zurückgelegt sein muss (s).</summary>
    public const double WischZeitS = 0.4;
    /// <summary>xr-greifen-logik.js WISCH_MIN_ZEIT_S: kürzeres Fenster zählt nicht (Ausreisser des Trackings) (s).</summary>
    public const double WischMinZeitS = 0.08;
    /// <summary>xr-greifen-logik.js WISCH_V_MS: Mindestgeschwindigkeit (Sehne / Zeit) in m/s.</summary>
    public const double WischVMs = 0.8;
    /// <summary>xr-greifen-logik.js WISCH_WAAGRECHT: Anteil des Wegs, der waagrecht verlaufen muss (0.8 ≈ höchstens 37° gegen die Waagrechte).</summary>
    public const double WischWaagrecht = 0.8;
    /// <summary>xr-greifen-logik.js WISCH_VERLAUF_S: so lange wird der Verlauf der Handflächenmitte je Hand gehalten (s).</summary>
    public const double WischVerlaufS = 0.5;
    /// <summary>xr-greifen-logik.js WISCH_SPERRE_S: Sperre nach einem Wisch, keine Doppelauslösung (s).</summary>
    public const double WischSperreS = 0.5;
    /// <summary>xr-greifen-logik.js WISCH_TREFFER_M: Abstand des Handwegs zur Masslinie, unter dem sie getroffen ist (m).</summary>
    public const double WischTrefferM = 0.10;
    /// <summary>xr-greifen-logik.js WISCH_TABLET_NAEHE_M: bei offenem Tablet wischt die rechte Hand nur ausserhalb dieses Abstands zur Tablet-Mitte (m).</summary>
    public const double WischTabletNaeheM = 0.30;
    /// <summary>xr-greifen-logik.js WISCH_AUSBLENDEN_S: Aufleuchten und Ausblenden der gelöschten Linie (s).</summary>
    public const double WischAusblendenS = 0.2;
    /// <summary>xr-greifen-logik.js WISCH_SPRUNG_MS: schneller als jede Armbewegung (m/s) = Sprung des Trackings, kein Wisch (der Verlauf beginnt neu).</summary>
    public const double WischSprungMs = 8;

    /// <summary>
    /// xr-greifen-logik.js wischErkennen: erkennt einen Wisch im Verlauf einer Hand (aufsteigend in t, Ende = jetzt). Gesucht ist ein Anfang höchstens
    /// <see cref="WischZeitS"/> vor dem Ende, von dem aus die Sehne mindestens <see cref="WischWegM"/> lang ist, zu mindestens <see cref="WischWaagrecht"/> waagrecht verläuft
    /// und mit mindestens <see cref="WischVMs"/> durchlaufen wurde.
    /// </summary>
    public static WischErgebnis? WischErkennen(IReadOnlyList<WischProbe>? verlauf)
    {
        if (verlauf is null || verlauf.Count < 2) return null;
        var e = verlauf[^1];
        for (var i = 0; i < verlauf.Count - 1; i++)
        {
            var dt = e.T - verlauf[i].T;
            if (dt > WischZeitS + 1e-9) continue;   // zu alt
            if (dt < WischMinZeitS) break;          // alle weiteren sind noch kürzer
            var d = e.P - verlauf[i].P;
            double gesamt = d.Laenge, waagrecht = Num.Hypot(d.X, d.Z);
            if (waagrecht < WischWegM || waagrecht < WischWaagrecht * gesamt) continue;
            var v = gesamt / dt;
            if (v < WischVMs) continue;
            return new WischErgebnis(verlauf.Skip(i).Select(s => s.P).ToList(), gesamt, dt, v, new Vec3(d.X / waagrecht, 0, d.Z / waagrecht));
        }
        return null;
    }

    /// <summary>xr-greifen-logik.js streckenAbstand: kleinster Abstand zweier Strecken p1–q1 und p2–q2 (m).</summary>
    public static double StreckenAbstand(Vec3 p1, Vec3 q1, Vec3 p2, Vec3 q2)
    {
        static double Klemm(double x) => Math.Min(1, Math.Max(0, x));
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;
        double a = d1.Skalar(d1), e = d2.Skalar(d2), f = d2.Skalar(r);
        const double EPS = 1e-12;
        double s, t;
        if (a <= EPS && e <= EPS) return p1.Abstand(p2);
        if (a <= EPS) { s = 0; t = Klemm(f / e); }
        else
        {
            var c = d1.Skalar(r);
            if (e <= EPS) { t = 0; s = Klemm(-c / a); }
            else
            {
                var b = d1.Skalar(d2);
                var den = a * e - b * b;
                s = den > EPS ? Klemm((b * f - c * e) / den) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Klemm(-c / a); }
                else if (t > 1) { t = 1; s = Klemm((b - c) / a); }
            }
        }
        return (p1 + d1 * s).Abstand(p2 + d2 * t);
    }

    /// <summary>xr-greifen-logik.js wischTreffer: welche Masslinien streift der Handweg (Streckenzug) im Abstand unter radius? Rückgabe: Indizes (aufsteigend).</summary>
    public static IReadOnlyList<int> WischTreffer(IReadOnlyList<Vec3>? weg, IReadOnlyList<Masslinie> linien, double radius = WischTrefferM)
    {
        var treffer = new List<int>();
        if (weg is null || weg.Count == 0) return treffer;
        for (var i = 0; i < linien.Count; i++)
        {
            for (var k = 0; k < weg.Count; k++)
            {
                var q = weg[Math.Min(k + 1, weg.Count - 1)];
                if (StreckenAbstand(weg[k], q, linien[i].A, linien[i].B) < radius) { treffer.Add(i); break; }
            }
        }
        return treffer;
    }

    /// <summary>xr-greifen-logik.js neuerWischer: neuer Zustand der Wischerkennung einer Hand.</summary>
    public static Wischer NeuerWischer() => new();

    /// <summary>
    /// xr-greifen-logik.js wischSchritt: ein Schritt je Bild und Hand. t = Zeit (s), ort = Handflächenmitte, erlaubt = Hand offen, nicht am Greifen/Pinchen/Tablet.
    /// Der Wisch gilt, solange die Bewegung die Schwellen erfüllt (so trifft er auch eine Linie am Ende des Strichs); danach gilt die Sperre <see cref="WischSperreS"/>.
    /// Rückgabe null oder der Wisch (<see cref="WischErgebnis.Neu"/> = erstes Bild dieses Wischs).
    /// P5a: mit <paramref name="alle"/> erkennt der Schritt Wische in allen Richtungen mit diesen Schwellen (XrGesten.WischErkennen, Stellschrauben «wisch.*»);
    /// ohne (null) wie im Prototyp nur waagrechte (<see cref="WischErkennen"/>).
    /// </summary>
    public static WischErgebnis? WischSchritt(Wischer z, double t, Vec3? ort, bool erlaubt, WischEinstellungen? alle = null)
    {
        WischErgebnis? Beenden()
        {
            if (z.Aktiv) { z.Aktiv = false; z.SperreBis = t + WischSperreS; }
            z.Verlauf.Clear();
            return null;
        }
        if (!erlaubt || ort is not { } o) return Beenden();
        if (t < z.SperreBis) { z.Verlauf.Clear(); return null; }
        if (z.Verlauf.Count > 0)
        {
            var letzter = z.Verlauf[^1];
            if (t > letzter.T && letzter.P.Abstand(o) / (t - letzter.T) > WischSprungMs)   // Sprung des Trackings
            {
                if (z.Aktiv) Beenden();
                z.Verlauf.Clear();
            }
        }
        z.Verlauf.Add(new WischProbe(t, o));
        while (z.Verlauf.Count > 2 && t - z.Verlauf[0].T > WischVerlaufS) z.Verlauf.RemoveAt(0);
        var w = alle is null ? WischErkennen(z.Verlauf) : XrGesten.WischErkennen(z.Verlauf, alle);
        if (w is null) return z.Aktiv ? Beenden() : null;   // noch kein Wisch: Verlauf weitersammeln
        var neu = !z.Aktiv;
        z.Aktiv = true;
        return w with { Neu = neu };
    }
}
