// Quest App P5a: Geisterhände (Ergänzung «Ins Interface-Labor», Christian 23.10.2026: «Zögert man, zeigen Geisterhände die passende Geste»).
// Zögern: die Fingerspitze liegt ZeitS lang ruhig (Umkreis RuheM) über demselben Ziel (Kachel, Eintrag, offene App, Griff am Tisch), ohne dass etwas ausgelöst
// wird. Dann führt eine halbdurchsichtige Hand die passende Geste vor – zweimal hintereinander, aus der Form der eigenen Hand: Antippen (Zeigefinger stösst
// in die Kachel), Blättern (Pinch + Wisch nach links), Wegwischen (Pinch + Wisch nach vorn), Greifen (Daumen und Zeigefinger schliessen sich am Griff).
// Dieselbe Geste über demselben Ziel frühestens nach WiederholenS wieder. Rein, ohne StereoKit; die App zeichnet Gelenke und Knochen.
// Alle Zeiten und Wege sind Annahmen P5a (Labor).

namespace Hlks.Xr.Bedienung;

/// <summary>Geste, die eine Geisterhand vorführt.</summary>
public enum GeisterGeste { Antippen, Blaettern, Wegwischen, Greifen }

/// <summary>Zustand der Zögern-Erkennung einer Hand.</summary>
public sealed class ZoegernZustand
{
    internal string? Ziel;
    internal double Seit;
    internal Vec3 Ort;
    internal string? Gemeldet;
    internal double GemeldetT = double.NegativeInfinity;
}

/// <summary>Bild einer Geisterhand: Gelenke (Namen wie WebXR) und Deckkraft (0 … 1).</summary>
public sealed record GeisterBild(IReadOnlyDictionary<string, Vec3> Gelenke, double Deckkraft);

public static class Geisterhand
{
    /// <summary>So lange ruhig über demselben Ziel gilt als Zögern (s), Annahme P5a.</summary>
    public const double ZoegernS = 1.5;
    /// <summary>Umkreis der Ruhe (m), Annahme P5a.</summary>
    public const double RuheM = 0.03;
    /// <summary>Dieselbe Vorführung über demselben Ziel frühestens wieder nach (s), Annahme P5a.</summary>
    public const double WiederholenS = 6;
    /// <summary>Dauer einer Vorführung (zwei Durchgänge), s.</summary>
    public const double DauerS = 1.8;
    /// <summary>Höchste Deckkraft der Geisterhand.</summary>
    public const double DeckkraftMax = 0.45;
    /// <summary>Antippen: die Spitze startet so weit vor der Kachel (m).</summary>
    public const double AnlaufM = 0.08;
    /// <summary>Blättern und Wegwischen: Weg der Hand (m), über dem Mindestweg eines Wischs.</summary>
    public const double WegM = 0.22;
    /// <summary>Blättern und Wegwischen laufen so weit vor der Fläche (m).</summary>
    public const double VorFlaecheM = 0.07;

    /// <summary>
    /// Ein Schritt je Bild und Hand: ort = Fingerspitze, ziel = Schlüssel des Ziels unter der Hand (null = keines), aktion = in diesem Bild ausgelöst oder Pinch.
    /// Gibt das Ziel zurück, wenn jetzt gezögert wird (einmal je Ruhe; dasselbe Ziel frühestens nach <see cref="WiederholenS"/> wieder), sonst null.
    /// </summary>
    public static string? Zoegern(ZoegernZustand z, double t, Vec3? ort, string? ziel, bool aktion)
    {
        if (ort is not { } o || ziel is null || aktion)
        {
            z.Ziel = null;
            if (aktion) z.Gemeldet = null;   // wer handelt, braucht keine Hilfe mehr
            return null;
        }
        if (ziel != z.Ziel || z.Ort.Abstand(o) > RuheM) { z.Ziel = ziel; z.Seit = t; z.Ort = o; return null; }
        if (t - z.Seit < ZoegernS - 1e-9) return null;
        if (z.Gemeldet == ziel && t - z.GemeldetT < WiederholenS) return null;
        z.Gemeldet = ziel;
        z.GemeldetT = t;
        z.Seit = t;
        return ziel;
    }

    /// <summary>Knochen der Hand als Paare von Gelenknamen (WebXR) zum Zeichnen: Handgelenk zu den Mittelhandknochen, dann jeder Finger bis zur Spitze.</summary>
    public static readonly IReadOnlyList<(string A, string B)> Knochen = BaueKnochen();

    static List<(string, string)> BaueKnochen()
    {
        var k = new List<(string, string)>();
        string[] daumen = ["thumb-metacarpal", "thumb-phalanx-proximal", "thumb-phalanx-distal", "thumb-tip"];
        k.Add(("wrist", daumen[0]));
        for (int i = 0; i + 1 < daumen.Length; i++) k.Add((daumen[i], daumen[i + 1]));
        foreach (var f in new[] { "index", "middle", "ring", "pinky" })
        {
            string[] g = [$"{f}-finger-metacarpal", $"{f}-finger-phalanx-proximal", $"{f}-finger-phalanx-intermediate", $"{f}-finger-phalanx-distal", $"{f}-finger-tip"];
            k.Add(("wrist", g[0]));
            for (int i = 0; i + 1 < g.Length; i++) k.Add((g[i], g[i + 1]));
        }
        return k;
    }

    static double Glatt(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>
    /// Die Geisterhand zur Zeit t (s seit Beginn der Vorführung) aus der Form der eigenen Hand (Gelenke im Raum der Brille) für das Ziel (Kachel bzw. offene App;
    /// beim Greifen die Lage des Griffs als Mitte). null ausserhalb der Vorführung oder wenn der Hand die Zeigefinger- bzw. Daumenspitze fehlt.
    /// </summary>
    public static GeisterBild? Bild(GeisterGeste geste, double t, IReadOnlyDictionary<string, Vec3> hand, TabletPose ziel)
    {
        if (t < 0 || t > DauerS) return null;
        if (!hand.TryGetValue("index-finger-tip", out var spitze) || !hand.TryGetValue("thumb-tip", out var daumen)) return null;
        var deck = DeckkraftMax * Math.Min(1, Math.Min(t / 0.15, (DauerS - t) / 0.2));
        var zyklus = DauerS / 2;
        var u = (t % zyklus) / zyklus;   // 0 … 1 je Durchgang
        var pinchPunkt = Vec3.Mitte(spitze, daumen);
        Dictionary<string, Vec3> Verschoben(Vec3 bezug, Vec3 soll, bool zu)
        {
            var d = soll - bezug;
            var neu = hand.ToDictionary(x => x.Key, x => x.Value + d, StringComparer.Ordinal);
            if (zu)
            {
                // Pinch: Daumenspitze an die Zeigefingerspitze, das letzte Daumenglied halb dorthin
                neu["thumb-tip"] = neu["index-finger-tip"] + (neu["thumb-tip"] - neu["index-finger-tip"]) * 0.15;
                if (neu.TryGetValue("thumb-phalanx-distal", out var dd)) neu["thumb-phalanx-distal"] = Vec3.Mitte(dd, neu["thumb-tip"]);
            }
            return neu;
        }
        switch (geste)
        {
            case GeisterGeste.Antippen:
            {
                // hin (erste Hälfte) und zurück (zweite Hälfte): von AnlaufM vor der Kachel bis knapp hinein
                var s = u < 0.5 ? Glatt(u / 0.5) : 1 - Glatt((u - 0.5) / 0.5);
                var soll = ziel.Mitte + ziel.Normale * (AnlaufM * (1 - s) - 0.004 * s);
                return new GeisterBild(Verschoben(spitze, soll, false), deck);
            }
            case GeisterGeste.Blaettern:
            {
                // Pinch vor der Fläche, schnell von rechts nach links (nächste Seite), dann kurz stehen
                var s = Glatt(u / 0.55);
                var soll = ziel.Mitte + ziel.Normale * VorFlaecheM + ziel.Rechts * (WegM / 2 - WegM * s);
                return new GeisterBild(Verschoben(pinchPunkt, soll, true), deck);
            }
            case GeisterGeste.Wegwischen:
            {
                // Pinch vor der Fläche, schnell vom Körper weg (gegen die Normale der Fläche, die zum Menschen zeigt)
                var s = Glatt(u / 0.55);
                var soll = ziel.Mitte + ziel.Normale * (VorFlaecheM + WegM * (1 - s));
                return new GeisterBild(Verschoben(pinchPunkt, soll, true), deck * (1 - 0.6 * s));
            }
            default:
            {
                // Greifen: die Hand an den Griff, Daumen und Zeigefinger schliessen sich (erste Hälfte), öffnen sich wieder (zweite Hälfte)
                var zu = u < 0.5 ? Glatt(u / 0.5) : 1 - Glatt((u - 0.5) / 0.5);
                var bild = Verschoben(pinchPunkt, ziel.Mitte, false);
                var zs = bild["index-finger-tip"];
                bild["thumb-tip"] = bild["thumb-tip"] + (zs - bild["thumb-tip"]) * (0.85 * zu);
                return new GeisterBild(bild, deck);
            }
        }
    }
}
