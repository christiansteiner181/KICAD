// Quest App P4b (Modul XR, XR-H mit Passthrough, Entscheid Christian 23.10.2026 «Beides, Passthrough prüfen»; Interface-Labor: «Die Tischhöhe wird durch einmaliges
// Antippen des Tisches mit der Hand festgelegt»): die Höhe des echten Tisches aus der Spitze des Zeigefingers. Rein, je Bild ein Schritt.
//
// Regel (ANNAHMEN P4b, alle Werte in TischAntippenEinstellungen, im Labor P5 nachzustellen):
//   - Die Spitze des Zeigefingers bewegt sich nach unten (im Fenster vor der Ruhe mindestens AbwaertsM höher als in der Ruhe) und bleibt dann RuheS lang
//     ruhig (alle Punkte der Ruhe im Umkreis RuheRadiusM um den letzten) – so tippt man auf eine Platte; ein ruhig in der Luft gehaltener Finger zählt nicht.
//   - Kein Pinch (Daumen und Zeigefinger zusammen ist Greifen, nicht Tippen).
//   - Höhe über dem Boden zwischen HoeheMinM und HoeheMaxM (Tische, Pulte, Stehtische), waagrecht höchstens ReichweiteM vom Kopf.
//   - Die Höhe der Platte ist der Median der Ruhe minus dem Radius der Fingerspitze (das Gelenk liegt in der Mitte der Kuppe, StereoKit HandJoint.radius).
//   - Nach einem Treffer beginnt alles neu (einmaliges Antippen; die App schaltet das Antippen danach ab).
// Koordinaten: Referenzraum der Brille, Boden y = 0 (OriginMode.Floor), Meter.

namespace Hlks.Xr.Sitzung;

/// <summary>Schwellen des Antippens (Annahmen P4b).</summary>
public sealed record TischAntippenEinstellungen
{
    /// <summary>So lange muss die Fingerspitze ruhig liegen (s).</summary>
    public double RuheS { get; init; } = 0.3;
    /// <summary>Umkreis der Ruhe (m).</summary>
    public double RuheRadiusM { get; init; } = 0.012;
    /// <summary>So viel höher muss die Fingerspitze vor der Ruhe gewesen sein (m): die Bewegung nach unten auf die Platte.</summary>
    public double AbwaertsM { get; init; } = 0.02;
    /// <summary>Fenster vor der Ruhe, in dem die Bewegung nach unten liegen muss (s).</summary>
    public double FensterS { get; init; } = 1.0;
    /// <summary>Tiefste und höchste Platte über dem Boden (m).</summary>
    public double HoeheMinM { get; init; } = 0.4;
    public double HoeheMaxM { get; init; } = 1.3;
    /// <summary>Waagrechter Abstand der Fingerspitze vom Kopf höchstens (m), Reichweite des Arms.</summary>
    public double ReichweiteM { get; init; } = 1.0;

    public static readonly TischAntippenEinstellungen Vorgabe = new();
}

/// <summary>Angetippter Punkt auf der Platte (Referenzraum) und Höhe der Platte über dem Boden (m).</summary>
public sealed record TischTreffer(Vec3 Punkt, double Hoehe);

/// <summary>Verlauf einer Hand für <see cref="TischAntippen.Schritt"/>.</summary>
public sealed class TischAntippenZustand
{
    internal readonly List<(double T, Vec3 P)> Verlauf = [];
    /// <summary>Punkte im Verlauf (Prüfung).</summary>
    public int Punkte => Verlauf.Count;
    public void Zuruecksetzen() => Verlauf.Clear();
}

public static class TischAntippen
{
    /// <summary>Radius der Fingerspitze, wenn die Laufzeit keinen liefert (m), Annahme.</summary>
    public const double FingerRadiusVorgabe = 0.008;

    /// <summary>
    /// Ein Schritt: t = Zeit (s), finger = Spitze des Zeigefingers (null = Hand nicht erkannt), kopf = Kopf, pinch = Daumen und Zeigefinger zusammen.
    /// Gibt beim Antippen die Platte zurück, sonst null.
    /// </summary>
    public static TischTreffer? Schritt(TischAntippenZustand z, double t, Vec3? finger, Vec3 kopf, bool pinch, double fingerRadius = FingerRadiusVorgabe,
        TischAntippenEinstellungen? e = null)
    {
        e ??= TischAntippenEinstellungen.Vorgabe;
        if (finger is not { IstEndlich: true } f || pinch || !double.IsFinite(t))
        {
            z.Verlauf.Clear();
            return null;
        }
        if (z.Verlauf.Count > 0 && t < z.Verlauf[^1].T) z.Verlauf.Clear();   // Zeit lief rückwärts: neu beginnen
        z.Verlauf.Add((t, f));
        var grenze = t - e.RuheS - e.FensterS;
        var weg = z.Verlauf.FindIndex(x => x.T >= grenze);
        if (weg > 0) z.Verlauf.RemoveRange(0, weg);

        // genug Verlauf: ein Punkt liegt mindestens RuheS zurück
        var ruheAb = t - e.RuheS;
        if (z.Verlauf[0].T > ruheAb) return null;
        var ruhe = new List<Vec3>();
        double vorherMax = double.NegativeInfinity;
        foreach (var (tt, p) in z.Verlauf)
        {
            if (tt >= ruheAb)
            {
                if (p.Abstand(f) > e.RuheRadiusM) return null;   // nicht ruhig
                ruhe.Add(p);
            }
            else vorherMax = Math.Max(vorherMax, p.Y);
        }
        if (ruhe.Count == 0) return null;
        // Median je Achse: die letzten Millimeter des Absenkens (noch im Umkreis der Ruhe) verschieben ihn nicht
        static double Median(IEnumerable<double> w) { var s = w.Order().ToList(); return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2; }
        var mitte = new Vec3(Median(ruhe.Select(p => p.X)), Median(ruhe.Select(p => p.Y)), Median(ruhe.Select(p => p.Z)));
        if (!(vorherMax - mitte.Y >= e.AbwaertsM)) return null;   // keine Bewegung nach unten: in der Luft gehalten
        var hoehe = mitte.Y - Math.Max(0, double.IsFinite(fingerRadius) ? fingerRadius : FingerRadiusVorgabe);
        if (hoehe < e.HoeheMinM || hoehe > e.HoeheMaxM) return null;
        if (Math.Sqrt((mitte.X - kopf.X) * (mitte.X - kopf.X) + (mitte.Z - kopf.Z) * (mitte.Z - kopf.Z)) > e.ReichweiteM) return null;
        z.Verlauf.Clear();
        return new TischTreffer(new Vec3(mitte.X, hoehe, mitte.Z), hoehe);
    }
}
