// Hilfen für die Tests der Quest App P5b: eine Hand (WebXR-Gelenknamen) in beliebiger Lage – Handgelenk, Richtung der Finger und Normale der Handfläche
// (aus der Handfläche heraus, wie XrTabletLogik.HandflaechenNormale sie liefert), offen bzw. als Faust.

namespace Hlks.Xr.Tests;

internal static class HandHilfen
{
    /// <summary>
    /// Hand der Seite «left» bzw. «right»: Handgelenk w, Finger in Richtung f, Normale der Handfläche n (f und n senkrecht zueinander, Länge 1). offen: Fingerspitzen
    /// 18 cm vor dem Handgelenk; sonst Faust (Spitzen zur Handfläche gekrümmt). Die Mitte der Handfläche liegt 4.5 cm vor dem Handgelenk.
    /// </summary>
    public static Dictionary<string, Vec3> Hand(string seite, Vec3 w, Vec3 f, Vec3 n, bool offen = true)
    {
        f = f.Einheit(); n = n.Einheit();
        // seitlich zum Zeigefinger hin: so, dass (Zeigefinger − Handgelenk) × (kleiner Finger − Handgelenk) die Normale ergibt (rechte Hand) bzw. ihr Gegenteil (linke)
        var q = seite == "right" ? f.Kreuz(n) : n.Kreuz(f);
        var g = new Dictionary<string, Vec3>
        {
            ["wrist"] = w,
            ["middle-finger-phalanx-proximal"] = w + f * 0.09,
            ["index-finger-phalanx-proximal"] = w + f * 0.09 + q * 0.02,
            ["index-finger-metacarpal"] = w + f * 0.03 + q * 0.02,
            ["pinky-finger-metacarpal"] = w + f * 0.03 - q * 0.02,
            ["thumb-tip"] = w + f * 0.06 + q * 0.07,
        };
        var seitlich = new Dictionary<string, double> { ["index-finger-tip"] = 0.03, ["middle-finger-tip"] = 0.005, ["ring-finger-tip"] = -0.02, ["pinky-finger-tip"] = -0.04 };
        foreach (var (name, s) in seitlich)
            g[name] = offen ? w + f * 0.18 + q * s : w + f * 0.06 + q * (s * 0.5) + n * 0.045;
        return g;
    }

    /// <summary>Mitte der Handfläche einer Hand aus <see cref="Hand"/>: Handgelenk + 4.5 cm in Richtung der Finger.</summary>
    public static Vec3 Mitte(Vec3 w, Vec3 f) => w + f.Einheit() * 0.045;
}
