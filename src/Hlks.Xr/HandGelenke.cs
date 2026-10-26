// Quest App P4a (Modul XR): die Gelenknamen der WebXR-Hand-API, mit denen die Regeln in Hlks.Xr rechnen (XrTabletLogik, XrWischLogik, HandBegehen), auf
// die Hände von StereoKit abgebildet.
//
// WebXR (W3C, «WebXR Hand Input Module – Level 1», Abschnitt «Skeleton Joints»): 25 Gelenke – wrist; thumb-metacarpal, thumb-phalanx-proximal,
// thumb-phalanx-distal, thumb-tip; je Finger (index-finger, middle-finger, ring-finger, pinky-finger) metacarpal, phalanx-proximal, phalanx-intermediate,
// phalanx-distal, tip.
// StereoKit 0.3.11 (StereoKit.xml, Hand, FingerId, JointId): Hand[finger, gelenk] mit 5 × 5 Gelenken – Finger 0 Daumen … 4 kleiner Finger; Gelenk
// 0 Root (Mittelhand, «metacarpal»), 1 KnuckleMajor (Grundgelenk, «phalanx-proximal»), 2 KnuckleMid, 3 KnuckleMinor, 4 Tip. Der Daumen hat nur vier
// echte Gelenke, bei ihm sind Root und KnuckleMajor gleich; seine WebXR-Gelenke liegen deshalb auf 1 … 4. Das Handgelenk ist Hand.wrist.

namespace Hlks.Xr;

public static class HandGelenke
{
    public const string Handgelenk = "wrist";

    static readonly string[] Finger = ["index-finger", "middle-finger", "ring-finger", "pinky-finger"];
    static readonly string[] Glieder = ["metacarpal", "phalanx-proximal", "phalanx-intermediate", "phalanx-distal", "tip"];

    /// <summary>Die 25 WebXR-Namen in der Reihenfolge der Spezifikation.</summary>
    public static readonly IReadOnlyList<string> WebXr =
    [
        Handgelenk, "thumb-metacarpal", "thumb-phalanx-proximal", "thumb-phalanx-distal", "thumb-tip",
        .. Finger.SelectMany(f => Glieder.Select(g => $"{f}-{g}")),
    ];

    /// <summary>WebXR-Name → (Finger, Gelenk) für StereoKit Hand[finger, gelenk]; ohne «wrist» (StereoKit: Hand.wrist).</summary>
    public static readonly IReadOnlyDictionary<string, (int Finger, int Gelenk)> StereoKit = Abbildung();

    static Dictionary<string, (int, int)> Abbildung()
    {
        var d = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["thumb-metacarpal"] = (0, 1), ["thumb-phalanx-proximal"] = (0, 2), ["thumb-phalanx-distal"] = (0, 3), ["thumb-tip"] = (0, 4),
        };
        for (int f = 0; f < Finger.Length; f++)
            for (int g = 0; g < Glieder.Length; g++) d[$"{Finger[f]}-{Glieder[g]}"] = (f + 1, g);
        return d;
    }

    /// <summary>
    /// Die Gelenke einer Hand mit WebXR-Namen. gelenk(finger, glied) liefert die Lage eines StereoKit-Gelenks, handgelenk die von Hand.wrist; beide in
    /// derselben Welt (die App rechnet sie vorher in die Szene um). Gelenke mit nicht endlichen Werten fehlen im Ergebnis.
    /// </summary>
    public static Dictionary<string, Vec3> AusStereoKit(Func<int, int, Vec3> gelenk, Vec3 handgelenk)
    {
        var aus = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        if (handgelenk.IstEndlich) aus[Handgelenk] = handgelenk;
        foreach (var (name, (f, g)) in StereoKit)
        {
            var p = gelenk(f, g);
            if (p.IstEndlich) aus[name] = p;
        }
        return aus;
    }
}
