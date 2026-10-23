// Quest App P4b (Modul XR, Teil XR-H «Sitzungsraum mit Modell auf dem Tisch»): wo der Sitzungsraum im Raum der Brille liegt und wie das Modell auf den Tisch kommt.
// Rein, ohne StereoKit. Die Regeln des Tisches selbst (Masse, Massstab, Griffe, Gesten, Schnitthöhe, Ausschnitt) stehen in XrSitzungLogik (aus xr-sitzung-logik.js);
// hier steht nur, was die App zusätzlich braucht:
//
//   Raum-lokal (XrSitzungLogik): Meter, y oben, Ursprung = Boden unter dem Kopf beim Betreten, der Blick geht nach −z; der Tisch steht bei TischMitte.
//   Referenzraum der Brille (StereoKit, OriginMode.Floor): Meter, y oben, Boden y = 0.
//   SitzungRaumLage bildet Raum-lokal in den Referenzraum ab: zuerst um die Hochachse drehen (Drehung, gegen den Uhrzeigersinn von oben, wie
//   XrSitzungLogik.DreheXZ), dann um den Ursprung verschieben.
//
//   Virtueller Raum: Ursprung = Boden unter dem Kopf, −z = waagrechte Blickrichtung (wie der Prototyp beim Betreten).
//   Echter Tisch (Passthrough, P4b): der Mensch tippt die Tischplatte an (TischAntippen); die Lage wird so gewählt, dass die Mitte der Platte (TischMitte)
//   genau auf dem angetippten Punkt liegt und −z vom Kopf zum Punkt zeigt. Die Plattenhöhe TischHoehe fällt damit auf die angetippte Höhe; der Boden des
//   Raum-lokalen Systems liegt dann TischHoehe darunter (nur gedacht, der virtuelle Raum wird mit Passthrough nicht gezeichnet).
//
//   Modell auf dem Tisch: Szene (m, wie die GLB) → Raum-lokal:  p_raum = TischMitte + (X, 0, Z) + DreheXZ((p − Anker)/n, Drehung), y: TischHoehe + (p.y − Unten)/n
//   mit SitzungLage (Massstab n, Drehung, X, Z) aus XrSitzungLogik und dem Bezug (Anker = Mitte der Grundfläche, Unten = tiefster Punkt) des ganzen Modells bzw.
//   des Ausschnitts. Das ist dieselbe Rechnung wie in xr-sitzung.js (Halter) und passt zu XrSitzungLogik.ZweihandStart/ZweihandSchritt.

using System.Numerics;

namespace Hlks.Xr.Sitzung;

/// <summary>Lage des Raum-lokalen Systems im Referenzraum der Brille: Ursprung und Drehung um die Hochachse (rad, wie <see cref="XrSitzungLogik.DreheXZ"/>).</summary>
public sealed record SitzungRaumLage(Vec3 Ursprung, double Drehung)
{
    /// <summary>Raum-lokal → Referenzraum.</summary>
    public Vec3 ZuRef(Vec3 lokal)
    {
        var d = XrSitzungLogik.DreheXZ(new Xz(lokal.X, lokal.Z), Drehung);
        return new Vec3(Ursprung.X + d.X, Ursprung.Y + lokal.Y, Ursprung.Z + d.Z);
    }

    /// <summary>Referenzraum → Raum-lokal.</summary>
    public Vec3 AusRef(Vec3 r)
    {
        var d = XrSitzungLogik.DreheXZ(new Xz(r.X - Ursprung.X, r.Z - Ursprung.Z), -Drehung);
        return new Vec3(d.X, r.Y - Ursprung.Y, d.Z);
    }

    /// <summary>Matrix Raum-lokal → Referenzraum (Zeilenvektoren wie System.Numerics und StereoKit).</summary>
    public Matrix4x4 Matrix => Matrix4x4.CreateRotationY((float)Drehung) * Matrix4x4.CreateTranslation((float)Ursprung.X, (float)Ursprung.Y, (float)Ursprung.Z);
}

/// <summary>Bezug des Modells auf dem Tisch: Anker (Mitte der Grundfläche, Szene), tiefster Punkt (Szene, m) und Grösse (unverkleinert) für das Einpassen.</summary>
public sealed record SitzungBezug(Xz Anker, double Unten, ModellGroesse Groesse);

public static class SitzungRaum
{
    /// <summary>Unter so viel waagrechtem Abstand zwischen Kopf und angetipptem Punkt gilt die Blickrichtung statt der Richtung zum Punkt (m).</summary>
    public const double RichtungMinM = 0.15;

    /// <summary>Virtueller Raum: Ursprung auf dem Boden (y = 0) unter dem Kopf, −z in der waagrechten Blickrichtung (Gieren wie VrLogik: ab +x gegen den Uhrzeigersinn).</summary>
    public static SitzungRaumLage VorKopf(Vec3 kopf, double blickGieren) => new(new Vec3(kopf.X, 0, kopf.Z), blickGieren - Math.PI / 2);

    /// <summary>
    /// Echter Tisch: die Mitte der Tischplatte (XrSitzungLogik.TischMitte, Höhe TischHoehe) liegt auf dem angetippten Punkt; −z zeigt waagrecht vom Kopf zum Punkt
    /// (ist der Punkt näher als <see cref="RichtungMinM"/>, die Blickrichtung).
    /// </summary>
    public static SitzungRaumLage AufTisch(Vec3 tipp, Vec3 kopf, double blickGieren)
    {
        var d = new Xz(tipp.X - kopf.X, tipp.Z - kopf.Z);
        var gieren = Math.Sqrt(d.X * d.X + d.Z * d.Z) >= RichtungMinM ? XrSitzungLogik.GierenVon(d) : blickGieren;
        var drehung = gieren - Math.PI / 2;
        var m = XrSitzungLogik.DreheXZ(new Xz(XrSitzungLogik.TischMitte.X, XrSitzungLogik.TischMitte.Z), drehung);
        return new SitzungRaumLage(new Vec3(tipp.X - m.X, tipp.Y - XrSitzungLogik.TischHoehe, tipp.Z - m.Z), drehung);
    }

    /// <summary>Bezug für das ganze Modell aus seiner Hüllbox (Szene); null bei leerer Box.</summary>
    public static SitzungBezug? BezugAusBox(Box3 box) => box.IstLeer ? null
        : new SitzungBezug(new Xz((box.Min.X + box.Max.X) / 2, (box.Min.Z + box.Max.Z) / 2), box.Min.Y,
            new ModellGroesse(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z, box.Max.Y - box.Min.Y));

    /// <summary>Bezug für einen Ausschnitt (XrSitzungLogik.AusschnittBezugAus).</summary>
    public static SitzungBezug BezugAusAusschnitt(AusschnittBezug a) => new(a.Anker, a.Unten, a.Groesse);

    /// <summary>Szene → Raum-lokal (siehe Dateikopf).</summary>
    public static Vec3 ModellZuRaum(Vec3 p, SitzungLage lage, SitzungBezug bezug)
    {
        var n = lage.Massstab;
        var d = XrSitzungLogik.DreheXZ(new Xz((p.X - bezug.Anker.X) / n, (p.Z - bezug.Anker.Z) / n), lage.Drehung);
        var m = XrSitzungLogik.ModellMitte(lage);
        return new Vec3(m.X + d.X, XrSitzungLogik.TischHoehe + lage.Hoehe + (p.Y - bezug.Unten) / n, m.Z + d.Z);
    }

    /// <summary>Matrix Szene → Raum-lokal (dieselbe Abbildung wie <see cref="ModellZuRaum"/>).</summary>
    public static Matrix4x4 ModellMatrix(SitzungLage lage, SitzungBezug bezug)
    {
        var m = XrSitzungLogik.ModellMitte(lage);
        return Matrix4x4.CreateTranslation((float)-bezug.Anker.X, (float)-bezug.Unten, (float)-bezug.Anker.Z)
            * Matrix4x4.CreateScale((float)(1 / lage.Massstab))
            * Matrix4x4.CreateRotationY((float)lage.Drehung)
            * Matrix4x4.CreateTranslation((float)m.X, (float)(XrSitzungLogik.TischHoehe + lage.Hoehe), (float)m.Z);
    }

    /// <summary>Matrix Szene → Referenzraum der Brille.</summary>
    public static Matrix4x4 ModellMatrix(SitzungLage lage, SitzungBezug bezug, SitzungRaumLage raum) => ModellMatrix(lage, bezug) * raum.Matrix;
}

/// <summary>Lage einer Box gegen den Schnittquader.</summary>
public enum SchnittLage { Aussen, Innen, Schneidet }

/// <summary>
/// Der Teil der Szene, der im Sitzungsraum sichtbar bleibt: ein Quader mit waagrechten Achsen U, V (Einheitsvektoren in x, z der Szene) und Grenzen je Achse sowie
/// Grenzen in der Höhe (m, Szene). Der Etagenschnitt ist ein Quader, der nur oben begrenzt ist; der Ausschnitt («Raum zeigen (VR)») ist der Quader des
/// Schnittbereichs (SchnittbereichLogik). Die App gibt die Werte an den Shader «schnitt» (Assets/schnitt.hlsl), der alles ausserhalb verwirft.
/// </summary>
public sealed record SchnittQuader(double Ux, double Uz, double UMin, double UMax, double Vx, double Vz, double VMin, double VMax, double YMin, double YMax)
{
    /// <summary>«Unbegrenzt» (m) für Achsen ohne Grenze; float im Shader reicht dafür.</summary>
    public const double Weit = 1e6;

    /// <summary>Nur oben begrenzt (Etagenschnitt): alles über yMax (Szene, m) entfällt.</summary>
    public static SchnittQuader Oben(double yMax) => new(1, 0, -Weit, Weit, 0, 1, -Weit, Weit, -Weit, yMax);

    /// <summary>
    /// Aus einem Schnittbereich (Quader in mm wie die Ansichtseigenschaft; hier in den Koordinaten der Szene × 1000, also relativ zum Bezugspunkt). Die Achsen
    /// nach SchnittbereichLogik.Achsen: U = (cos w, −sin w), V = (−sin w, −cos w) in (x, z); u = U·(x, z), v = V·(x, z). Null, wenn der Bereich ungültig ist.
    /// </summary>
    public static SchnittQuader? AusSchnittbereich(SchnittbereichRoh? roh)
    {
        var sb = SchnittbereichLogik.Lesen(roh);
        if (sb is null || !sb.Aktiv) return null;
        var a = SchnittbereichLogik.Achsen(sb.Winkel);
        const double m = 1.0 / 1000;
        return new SchnittQuader(a.U[0], a.U[2], sb.Min[0] * m, sb.Max[0] * m, a.V[0], a.V[2], sb.Min[1] * m, sb.Max[1] * m, sb.Min[2] * m, sb.Max[2] * m);
    }

    public double U(Vec3 p) => p.X * Ux + p.Z * Uz;
    public double V(Vec3 p) => p.X * Vx + p.Z * Vz;

    /// <summary>Liegt der Punkt im Quader (Ränder eingeschlossen)?</summary>
    public bool Enthaelt(Vec3 p)
    {
        double u = U(p), v = V(p);
        return u >= UMin && u <= UMax && v >= VMin && v <= VMax && p.Y >= YMin && p.Y <= YMax;
    }

    /// <summary>
    /// Lage einer achsenparallelen Box (Szene): Innen, wenn alle acht Ecken im Quader liegen (die App zeichnet sie ohne Schnitt), Aussen, wenn alle Ecken jenseits
    /// derselben Grenze liegen (nicht gezeichnet), sonst Schneidet (mit dem Shader «schnitt»). Vorsichtig: eine Box knapp neben einer Ecke des gedrehten Quaders kann
    /// als «Schneidet» gelten, obwohl sie draussen liegt; der Shader verwirft dann alles.
    /// </summary>
    public SchnittLage Lage(Box3 b)
    {
        if (b.IstLeer) return SchnittLage.Aussen;
        double uMin = double.PositiveInfinity, uMax = double.NegativeInfinity, vMin = double.PositiveInfinity, vMax = double.NegativeInfinity;
        foreach (var x in new[] { b.Min.X, b.Max.X })
            foreach (var z in new[] { b.Min.Z, b.Max.Z })
            {
                double u = x * Ux + z * Uz, v = x * Vx + z * Vz;
                uMin = Math.Min(uMin, u); uMax = Math.Max(uMax, u); vMin = Math.Min(vMin, v); vMax = Math.Max(vMax, v);
            }
        if (uMax < UMin || uMin > UMax || vMax < VMin || vMin > VMax || b.Max.Y < YMin || b.Min.Y > YMax) return SchnittLage.Aussen;
        if (uMin >= UMin && uMax <= UMax && vMin >= VMin && vMax <= VMax && b.Min.Y >= YMin && b.Max.Y <= YMax) return SchnittLage.Innen;
        return SchnittLage.Schneidet;
    }

    /// <summary>Werte für den Shader: (Ux, Uz, UMin, UMax), (Vx, Vz, VMin, VMax), (YMin, YMax, 0, 0).</summary>
    public (Vector4 U, Vector4 V, Vector4 Y) ShaderWerte() =>
        (new Vector4((float)Ux, (float)Uz, (float)UMin, (float)UMax), new Vector4((float)Vx, (float)Vz, (float)VMin, (float)VMax),
         new Vector4((float)YMin, (float)YMax, 0, 0));
}
