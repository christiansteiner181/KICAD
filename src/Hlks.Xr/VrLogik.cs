// «In VR begehen» (Nachbearbeitung N23): die Rechenregeln, rein und ohne Zeichnen und ohne Gerät.
// Quelle: wwwroot/js/ansichten/vr-logik.js (Browser-Prototyp, unverändert; Node-Test vr.test.mjs).
//
// Konventionen: Y oben, Meter, Blick -Z; Szene x = Ost, z = -Nord. Die Blickrichtung zählt als Gieren (Bogenmass, gegen den Uhrzeigersinn von oben ab
// Projekt-x; 90 Grad = Projekt-Nord = -z), wie beim Begehen am Bildschirm.
//
// Das «Rig» ist der Ursprung des Referenzraums (local-floor: Ursprung auf dem Boden, y = 0) in der Welt. Es sitzt auf der Bodenhöhe (OK Boden des Raums) und
// ist um <c>Gieren</c> gedreht (Drehung um y, wie three rotation.y bzw. Matrix4x4.CreateRotationY). Ein Punkt p im Referenzraum liegt in der Welt bei
//   welt = rig + R(θ)·p,   R(θ): x' = x·cosθ + z·sinθ,  z' = −x·sinθ + z·cosθ.
// Blickt der Kopf im Referenzraum mit dem Gieren g0, blickt er in der Welt mit g0 + θ.
//
// ANNAHMEN des Prototyps (Christian beim Test mit der Brille zu bestätigen): linker Stick = gehen (1.4 m/s in Blickrichtung des Kopfes, ohne Höhe), Stick
// gedrückt = dreifach schneller; rechter Stick seitlich = Drehen in 30°-Schritten (Snap-Turn); Abzug bzw. Pinch halten = Zielstrahl, Loslassen = Teleport;
// IFC-Unterlage mit Taste A ein/aus; keine Kollision. Die Wände werden nur unter 500 mm zum Kopf durchscheinend (siehe Durchgang).

using System.Numerics;

namespace Hlks.Xr;

/// <summary>Texte der Knöpfe (vr-logik.js VR_TEXT).</summary>
public static class VrText
{
    public const string Knopf = "In VR begehen";
    public const string Beenden = "VR beenden";
    public const string Startet = "VR startet …";
    public const string KeineBrille = "Keine VR-Brille verbunden (Quest Link starten oder KICAD im Browser der Quest öffnen)";
    public const string KeinBrowser = "Dieser Browser kann kein WebXR (Chrome am PC benutzen)";
}

/// <summary>Zustand des Knopfes «In VR begehen» (vr-logik.js knopfZustand).</summary>
public sealed record KnopfZustandWert(string Text, bool Deaktiviert, string Hinweis);

/// <summary>Optionen für die Sitzung (vr-logik.js sitzungsOptionen): Boden als Referenz verlangt, Hand-Tracking wenn vorhanden.</summary>
public sealed record SitzungsOptionen(IReadOnlyList<string> RequiredFeatures, IReadOnlyList<string> OptionalFeatures);

/// <summary>Lage des Rigs in der Welt: Position in Metern und Gieren in Bogenmass.</summary>
public sealed record Rig(double X, double Y, double Z, double Gieren)
{
    /// <summary>Drehung des Rigs als Quaternion (um die Hochachse), für die App (float).</summary>
    public Quaternion ZuQuaternion() => Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Gieren);

    /// <summary>Rig als Matrix Referenzraum → Welt (Zeilenvektor-Schreibweise von System.Numerics: v * M), für die App (float).</summary>
    public Matrix4x4 ZuMatrix() => Matrix4x4.CreateRotationY((float)Gieren) * Matrix4x4.CreateTranslation((float)X, (float)Y, (float)Z);
}

/// <summary>Kopfpose: Position (x, y, z) und Blickgieren, im Referenzraum oder in der Welt, je nach Funktion.</summary>
public sealed record KopfPose(double X, double Y, double Z, double Gieren)
{
    /// <summary>Kopf im Nullpunkt, Blick nach -Z (Gieren 90 Grad), die Annahme von rigStart ohne Pose.</summary>
    public static readonly KopfPose Standard = new(0, 0, 0, Math.PI / 2);
}

/// <summary>Stick-Achsen (-1 … 1); y &lt; 0 = nach vorn, wie das Gamepad der Quest sie liefert.</summary>
public readonly record struct Stick(double X, double Y);

/// <summary>Bewegung in der Welt in Metern.</summary>
public readonly record struct Bewegung(double Dx, double Dz);

/// <summary>Zustand des Snap-Turns (vr-logik.js snapPruefen): bereit = der nächste Ausschlag dreht.</summary>
public readonly record struct SnapZustand(bool Bereit);

/// <summary>Ergebnis des Snap-Turns: neuer Zustand und Schritt (+1 links, -1 rechts, 0 keiner).</summary>
public readonly record struct SnapErgebnis(bool Bereit, int Schritt);

/// <summary>Treffer des Zielstrahls auf dem Boden.</summary>
public readonly record struct TeleportTreffer(Vec3 Punkt, double Distanz);

/// <summary>Kamera der 3D-Ansicht: Position und Ziel (three-Meter). Ohne Ziel blickt der Standort nach Norden.</summary>
public sealed record KameraWerte(Vec3 Position, Vec3? Ziel = null);

/// <summary>Gleitender Mittelwert der Zeit je Bild (ms); gibt auch die Bilder pro Sekunde, die diese Zeit zulässt. vr-logik.js bildzeit.</summary>
public sealed class Bildzeit
{
    readonly int _fenster;
    readonly List<double> _werte = [];

    public Bildzeit(int fenster = 90) => _fenster = fenster;

    public void Neu(double ms)
    {
        if (!Num.Ok(ms) || ms < 0) return;
        _werte.Add(ms);
        if (_werte.Count > _fenster) _werte.RemoveAt(0);
    }

    public double Mittel => _werte.Count > 0 ? _werte.Sum() / _werte.Count : 0;
    public double Maximum => _werte.Count > 0 ? _werte.Max() : 0;
    public int Anzahl => _werte.Count;
    public void Leeren() => _werte.Clear();
}

public static class VrLogik
{
    /// <summary>vr-logik.js SNAP_GRAD: Drehung der Schritte beim Snap-Turn (Grad).</summary>
    public const double SnapGrad = 30;
    /// <summary>vr-logik.js SNAP_AN: ab dieser Auslenkung des rechten Sticks (0 … 1) dreht ein Schritt.</summary>
    public const double SnapAn = 0.7;
    /// <summary>vr-logik.js SNAP_LOS: erst unter dieser Auslenkung ist der nächste Schritt möglich.</summary>
    public const double SnapLos = 0.3;
    /// <summary>vr-logik.js STICK_TOTZONE: Totzone des linken Sticks.</summary>
    public const double StickTotzone = 0.15;
    /// <summary>vr-logik.js TELEPORT_REICHWEITE: der Zielstrahl reicht so weit (m).</summary>
    public const double TeleportReichweite = 15;
    /// <summary>vr-logik.js TELEPORT_MIN_STEIGUNG: ein Strahl, der weniger steil nach unten zeigt, trifft den Boden nicht (Sinus des Winkels).</summary>
    public const double TeleportMinSteigung = 0.02;
    /// <summary>vr-logik.js IFC_GRENZE_DREIECKE: IFC-Unterlagen mit mehr Dreiecken sind in VR zuerst ausgeblendet.</summary>
    public const double IfcGrenzeDreiecke = 300000;
    /// <summary>vr-logik.js DURCHGANG_MM (aus begehen-logik.js): Wände werden nur unter diesem Abstand (mm) durchscheinend.</summary>
    public const double DurchgangMm = Durchgang.DurchgangMm;

    // ---------------------------------------------------------------------------------------------------- Knopf

    /// <summary>
    /// vr-logik.js knopfZustand. unterstuetzt: null = noch nicht geprüft. Ohne Brille ist der Knopf ausgegraut und trägt den Hinweis.
    /// </summary>
    public static KnopfZustandWert KnopfZustand(bool xrVorhanden = false, bool? unterstuetzt = null, bool aktiv = false, bool startet = false)
    {
        if (aktiv) return new(VrText.Beenden, false, "");
        if (startet) return new(VrText.Startet, true, "");
        if (!xrVorhanden) return new(VrText.Knopf, true, VrText.KeinBrowser);
        if (unterstuetzt != true) return new(VrText.Knopf, true, VrText.KeineBrille);
        return new(VrText.Knopf, false, "");
    }

    /// <summary>vr-logik.js sitzungsOptionen: Optionen für requestSession('immersive-vr', …).</summary>
    public static SitzungsOptionen Sitzungsoptionen() => new(["local-floor"], ["hand-tracking"]);

    // ---------------------------------------------------------------------------------------------------- Rig

    /// <summary>vr-logik.js drehe: dreht einen Punkt (x, z) im Referenzraum mit dem Rig-Gieren θ in die Welt (ohne Verschiebung).</summary>
    public static Xz Drehe(Xz p, double theta)
    {
        double c = Math.Cos(theta), s = Math.Sin(theta);
        return new Xz(p.X * c + p.Z * s, -p.X * s + p.Z * c);
    }

    /// <summary>vr-logik.js gierenVonRichtung: Gieren (Bogenmass, Zählung des Begehens) einer Blickrichtung (x, z) in three-Koordinaten.</summary>
    public static double GierenVonRichtung(Xz r) => Math.Atan2(-r.Z, r.X);

    /// <summary>vr-logik.js kopfWelt: Weltposition des Kopfes bei Rig und Kopf im Referenzraum.</summary>
    public static Vec3 KopfWelt(Rig rig, Vec3 kopf)
    {
        var d = Drehe(new Xz(kopf.X, kopf.Z), rig.Gieren);
        return new Vec3(rig.X + d.X, rig.Y + kopf.Y, rig.Z + d.Z);
    }

    /// <summary>vr-logik.js blickGierenWelt: Blickgieren des Kopfes in der Welt.</summary>
    public static double BlickGierenWelt(Rig rig, double kopfGieren) => kopfGieren + rig.Gieren;

    /// <summary>
    /// vr-logik.js kopfAusWelt: Umkehrung von <see cref="KopfWelt"/>. Kopf in der Welt → Referenzraum des Rigs. Befund Brillentest 21.10.2026: Liefert die
    /// Laufzeit die Kamera in Weltkoordinaten, aber <see cref="KopfWelt"/> erwartet den Referenzraum, sitzt der Kopf doppelt um das Rig versetzt.
    /// </summary>
    public static KopfPose KopfAusWelt(Rig rig, KopfPose w)
    {
        var d = Drehe(new Xz(w.X - rig.X, w.Z - rig.Z), -rig.Gieren);
        return new KopfPose(d.X, w.Y - rig.Y, d.Z, w.Gieren - rig.Gieren);
    }

    /// <summary>
    /// vr-logik.js rigStart: Rig so, dass der Kopf an der Stelle des Begehens steht und in dessen Blickrichtung schaut. kopf = Kopfpose im Referenzraum
    /// (die Brille sitzt nicht genau im Nullpunkt und blickt nicht genau nach -z); ohne Angabe <see cref="KopfPose.Standard"/>. Die Höhe des Rigs ist OK Boden.
    /// </summary>
    public static Rig RigStart(BegehenZustand zustand, KopfPose? kopf = null)
    {
        kopf ??= KopfPose.Standard;
        var theta = zustand.Gieren - kopf.Gieren;
        var d = Drehe(new Xz(kopf.X, kopf.Z), theta);
        return new Rig(zustand.X - d.X, zustand.Boden, zustand.Z - d.Z, theta);
    }

    /// <summary>vr-logik.js rigDrehen: Drehung um den Kopf (der Kopf bleibt an seiner Weltstelle); dGrad &gt; 0 gegen den Uhrzeigersinn, &lt; 0 nach rechts.</summary>
    public static Rig RigDrehen(Rig rig, Vec3 kopf, double dGrad)
    {
        var vorher = Drehe(new Xz(kopf.X, kopf.Z), rig.Gieren);
        var theta = rig.Gieren + dGrad * Num.Grad;
        var nachher = Drehe(new Xz(kopf.X, kopf.Z), theta);
        return rig with { X = rig.X + vorher.X - nachher.X, Z = rig.Z + vorher.Z - nachher.Z, Gieren = theta };
    }

    /// <summary>vr-logik.js rigAufZiel: Rig so, dass der Kopf über (ziel.X, ziel.Z) steht; die Höhe des Rigs wird <paramref name="boden"/> (ohne Angabe bleibt sie).</summary>
    public static Rig RigAufZiel(Rig rig, Vec3 kopf, Xz ziel, double? boden = null)
    {
        var d = Drehe(new Xz(kopf.X, kopf.Z), rig.Gieren);
        return rig with { X = ziel.X - d.X, Z = ziel.Z - d.Z, Y = boden ?? rig.Y };
    }

    /// <summary>vr-logik.js rigVersetzen: Rig um (dx, dz) in der Welt verschoben (Höhe bleibt).</summary>
    public static Rig RigVersetzen(Rig rig, double dx, double dz) => rig with { X = rig.X + dx, Z = rig.Z + dz };

    // ---------------------------------------------------------------------------------------------------- Sticks

    static double MitTotzone(double v, double tot = StickTotzone)
    {
        var a = Math.Abs(v);
        if (!Num.Ok(v) || a <= tot) return 0;
        return Math.Sign(v) * Math.Min(1, (a - tot) / (1 - tot));
    }

    /// <summary>
    /// vr-logik.js stickBewegung: Bewegung des linken Sticks in der Welt (m) für dt Sekunden. blickGieren = Blickrichtung des Kopfes in der Welt (rad).
    /// Nur waagrecht, die Höhe ändert sich nie; diagonal nicht schneller als geradeaus.
    /// </summary>
    public static Bewegung StickBewegung(Stick? stick, double blickGieren, double dt, bool schnell = false)
    {
        var vor = -MitTotzone(stick?.Y ?? double.NaN);
        var seit = MitTotzone(stick?.X ?? double.NaN);
        if (vor == 0 && seit == 0) return new Bewegung(0, 0);
        var v = Begehen.Geschwindigkeit * (schnell ? Begehen.Schnell : 1) * Math.Max(0, dt);
        double fx = Math.Cos(blickGieren), fz = -Math.Sin(blickGieren);    // vorwärts
        double rx = Math.Sin(blickGieren), rz = Math.Cos(blickGieren);     // rechts
        var laenge = Num.Hypot(vor, seit);
        var f = laenge > 1 ? 1 / laenge : 1;
        return new Bewegung(v * f * (fx * vor + rx * seit), v * f * (fz * vor + rz * seit));
    }

    /// <summary>
    /// vr-logik.js snapPruefen: Snap-Turn, rechter Stick seitlich. Ein Ausschlag dreht genau einmal; erst wenn der Stick wieder in der Mitte ist,
    /// dreht der nächste. Schritt +1 = links (gegen den Uhrzeigersinn), -1 = rechts. Ohne Zustand (null) gilt «bereit».
    /// </summary>
    public static SnapErgebnis SnapPruefen(SnapZustand? zustand, double? x)
    {
        var bereit = zustand?.Bereit ?? true;
        if (!Num.Ok(x)) return new SnapErgebnis(bereit, 0);
        var ax = Math.Abs(x!.Value);
        if (bereit && ax >= SnapAn) return new SnapErgebnis(false, x.Value > 0 ? -1 : 1);   // Stick nach rechts = nach rechts drehen
        if (!bereit && ax <= SnapLos) return new SnapErgebnis(true, 0);
        return new SnapErgebnis(bereit, 0);
    }

    // ---------------------------------------------------------------------------------------------------- Teleport

    /// <summary>
    /// vr-logik.js teleportZiel: Zielstrahl auf den Boden. ursprung/richtung in der Welt; Boden = waagrechte Ebene auf der Höhe <paramref name="boden"/>
    /// (OK Boden des Geschosses). Null, wenn der Strahl nicht genug nach unten zeigt, zu weit weg trifft oder unvollständig ist.
    /// </summary>
    public static TeleportTreffer? TeleportZiel(Vec3? ursprung, Vec3? richtung, double boden, double reichweite = TeleportReichweite)
    {
        if (ursprung is not { } u || richtung is not { } r || !u.IstEndlich || !r.IstEndlich || !Num.Ok(boden)) return null;
        var l = r.Laenge;
        if (l == 0) return null;
        var dy = r.Y / l;
        if (dy > -TeleportMinSteigung) return null;
        var t = (boden - u.Y) / dy;   // Weg entlang des normierten Strahls
        if (!(t > 0) || t > reichweite) return null;
        return new TeleportTreffer(new Vec3(u.X + (r.X / l) * t, boden, u.Z + (r.Z / l) * t), t);
    }

    // ---------------------------------------------------------------------------------------------------- IFC-Unterlage

    /// <summary>vr-logik.js ifcAnfangsSichtbar: ist die IFC-Unterlage in VR anfangs sichtbar? Bei mehr als <see cref="IfcGrenzeDreiecke"/> Dreiecken nicht (Bildrate).</summary>
    public static bool IfcAnfangsSichtbar(double? dreiecke) => !(Num.Ok(dreiecke) && dreiecke!.Value > IfcGrenzeDreiecke);

    // ---------------------------------------------------------------------------------------------------- Standort ohne Raum

    /// <summary>
    /// vr-logik.js standortAusKamera: Standort im Format der Abfrage raum_begehen für den Start ohne Raum: am aktuellen Kamerastandpunkt der 3D-Ansicht,
    /// Blick zum Ziel der Kamera (waagrecht). boden = Höhe OK Boden in m (die Ansicht nimmt die tiefste Stelle des Modells).
    /// </summary>
    public static Standort? StandortAusKamera(KameraWerte? kamera, double boden, double? augenhoeheMm = null)
    {
        if (kamera is null || !kamera.Position.IstEndlich || !Num.Ok(boden)) return null;
        var p = kamera.Position;
        double azimut = 90;   // sonst nach Norden
        if (kamera.Ziel is { } z && Num.Ok(z.X) && Num.Ok(z.Z) && Num.Hypot(z.X - p.X, z.Z - p.Z) > 1e-6)
            azimut = GierenVonRichtung(new Xz(z.X - p.X, z.Z - p.Z)) / Num.Grad;
        static double Mm(double m) => Num.Round(m * 1000);
        return new Standort
        {
            Raum = null, Name = "Aktueller Standpunkt", Nummer = "", Geschoss = "", GeschossName = "",
            Standpunkt = new Standpunkt(Mm(p.X), Mm(-p.Z), Mm(boden), "kamera"),
            BodenOk = Mm(boden),
            Augenhoehe = Num.Ok(augenhoeheMm) ? augenhoeheMm : null,
            Blick = new Blick(Num.Round(azimut * 100) / 100, 0),
        };
    }
}
