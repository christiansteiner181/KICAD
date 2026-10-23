// Modul XR, Teil XR-H «Sitzungsraum mit Modell auf dem Tisch»: die Rechenregeln, rein und ohne Zeichnen und ohne Gerät.
// Einpassen und Massstab, Grenzen, Griffe, Drehen und Verschieben mit einer Hand, Zoomen und Drehen mit zwei Händen um den Mittelpunkt, Etagenliste und
// Schnitthöhe aus den Geschossdaten, Ausschnitt («Raum zeigen (VR)»), Tablet-Seite des Sitzungsraums.
// Quelle: wwwroot/js/ansichten/xr-sitzung-logik.js (Browser-Prototyp, unverändert; Node-Test xr-sitzung.test.mjs).
//
// Konventionen: Raum-lokal, Meter ({x, y, z}, y oben). Ursprung = Boden unter dem Kopf beim Betreten, der Blick geht nach -z. Das Modell liegt auf dem Tisch; seine Lage
// ist SitzungLage { Massstab (Nenner n, 1:n), Drehung (rad um die Senkrechte, gegen den Uhrzeigersinn von oben), X, Z (Mitte des Modells relativ zur Tischmitte, m) }.
// Modellkoordinaten sind die der 3D-Ansicht (m: x Ost, y Höhe, z = -Nord); Geschossdaten (Kote, Höhe, Dicke) in mm wie im Kern.
//
// ANNAHMEN (am Brillentest zu bestätigen; alle Werte sind benannt):
//   Tisch 1.6 × 1.0 m, Plattenoberkante 0.75 m, Vorderkante 0.4 m vor dem Kopf (Mitte also 0.9 m vor dem Kopf); nutzbare Platte mit 0.1 m Rand.
//   Massstab zu Beginn 1:50 (grösser, wenn das Modell sonst nicht auf die Platte passt, höchstens 1:200); Zoom von 1:200 bis 1:5 (MassstabMin; Prototyp 1:20,
//   Brillentest Christian 23.10.2026: «die modellansichten sollten >1:20 gezoomt werden können»).
//   Griffe (Christian 21.10.2026 nach dem Brillentest: «das ist nicht praktikabel. es braucht griffe. halten nur mit daumen und zeigefinger.»): zwei sichtbare Griffe links und
//   rechts des Modells (GriffAbstandM ausserhalb der Modellbreite, GriffHoeheM über der Platte), sie wandern mit Lage, Drehung und Massstab des Modells. Gegriffen wird nur
//   mit einem Pinch (Daumen- und Zeigefingerspitze) bzw. dem Abzug, der höchstens GriffFangM vom Griff entfernt beginnt (Controller auch mit dem Zielstrahl), und bis zum
//   Loslassen. Bewegt wird das Modell nur, solange BEIDE Griffe gehalten sind (verschieben, drehen, zoomen um den Mittelpunkt der beiden Pinch-Punkte); ein Griff allein: Ruhe.
//   Die Faust greift nichts. Zoom: der Abstand der Hände, Drehung: die Richtung der Verbindungslinie, Verschieben: der Mittelpunkt.
//   Schnitt: unter der Decke des gewählten Geschosses = Unterkante der Decke (Oberkante der Decke minus Dicke), «Decke zeigen» = Oberkante der Decke. Oberkante der Decke
//   über einem Geschoss = Kote des Geschosses darüber, sonst Kote + Geschosshöhe (Regel von decke_erzeugen, Kern). Gibt es keine Decke im Modell, gilt deren Oberkante.

namespace Hlks.Xr;

/// <summary>Lage des Modells auf dem Tisch: Massstab (Nenner), Drehung (rad), Mitte relativ zur Tischmitte (m).</summary>
/// <summary>Lage des Modells auf dem Tisch: Massstab (Nenner), Drehung (rad), Verschiebung X, Z (m) und – Brillentest Christian 23.10.2026 – Höhe über der Platte (m).</summary>
public sealed record SitzungLage(double Massstab, double Drehung, double X, double Z, double Hoehe = 0);

/// <summary>Startlage mit der Angabe, ob das Modell so auf die Platte passt (xr-sitzung-logik.js einpassen).</summary>
public sealed record Einpassung(SitzungLage Lage, bool Passt);

/// <summary>Grundfläche (b × t) und Höhe h des Modells bzw. des Ausschnitts in Metern (unverkleinert).</summary>
public sealed record ModellGroesse(double B, double T, double H = 0);

/// <summary>Einer der beiden Griffe (index 0 = links, 1 = rechts), Raum-lokal.</summary>
public sealed record GriffPunkt(int Index, string Seite, double X, double Y, double Z);

/// <summary>Zielstrahl des Controllers: Ursprung und Richtung, Raum-lokal.</summary>
public sealed record Strahl3(Vec3 U, Vec3 R);

/// <summary>Hand für die Richtung: Gelenke (Raum-lokal) und Richtung des Zielstrahls.</summary>
public sealed record HandInfo(IReadOnlyDictionary<string, Vec3>? Gelenke = null, Vec3? StrahlRichtung = null);

/// <summary>Eine Hand beim Greifen mit einer Hand: Ort (Raum-lokal) und Richtung der Hand um die Senkrechte (null = unbekannt).</summary>
public sealed record HandXz(double X, double Z, double? Gieren = null);

/// <summary>Bezug für das Greifen mit einer Hand (xr-sitzung-logik.js einhandStart).</summary>
public sealed record EinhandGriff(SitzungLage Lage0, Xz H0, double? G0);

/// <summary>Bezug für das Greifen mit zwei Händen (xr-sitzung-logik.js zweihandStart).</summary>
public sealed record ZweihandGriff(SitzungLage Lage0, double Abstand0, double? Phi0, Xz Q0, double Y0 = 0);

/// <summary>Eine Hand in der Gestenerkennung: Griff = Pinch bzw. Abzug gesetzt (die Faust zählt nicht), Nah = Index des Griffs in Fangreichweite, X, Z = Pinch-Punkt.</summary>
public sealed record GestenHand(bool Griff, int? Nah, double X, double Z, double Y = 0);

/// <summary>Beide Hände (null = Hand nicht verfolgt).</summary>
public sealed record GestenHaende(GestenHand? Links, GestenHand? Rechts);

/// <summary>Welchen Griff (0 | 1) eine Hand hält, sonst null.</summary>
public readonly record struct GriffBesitz(int? Links, int? Rechts);

/// <summary>War der Pinch im letzten Schritt gesetzt (Flanke)?</summary>
public readonly record struct GriffVorher(bool Links, bool Rechts);

/// <summary>Zustand der Gestenerkennung. Modus: null (Ruhe), "wartet" (ein Griff gehalten) oder "zwei" (beide Griffe: das Modell bewegt sich).</summary>
public sealed record GestenZustand(SitzungLage Lage, string? Modus, ZweihandGriff? Griff, GriffBesitz Besitzt, GriffVorher Vorher);

/// <summary>Bezug des Sitzungsraums für einen Ausschnitt: Grösse, Anker (Mitte x, z) und tiefster Punkt (Welt, m).</summary>
public sealed record AusschnittBezug(ModellGroesse Groesse, Xz Anker, double Unten);

/// <summary>Clipping-Ebene des Ausschnitts: Abstand = Normale · p + Konstante (≥ 0 = sichtbar).</summary>
public sealed record Clipebene(double[] Normale, double Konstante);

/// <summary>Geschoss aus dem Kern (mm): Kennzeichen, Name, Kote (OK fertig Boden), Höhe und das Gebäude, zu dem es gehört (TeilVon).</summary>
public sealed record SitzungGeschossRoh(string Kennzeichen, string? Name, double? Kote, double? Hoehe, string? TeilVon = null);

/// <summary>Decke aus dem Modell (nur Klasse Decke): Oberkante und Dicke in mm.</summary>
public sealed record DeckeRoh(double? Oberkante, double? Dicke);

/// <summary>Aufbereitetes Geschoss: Oberkante der Decke (DeckeOK), Dicke (null = keine Decke gefunden), Unterkante (DeckeUK), OhneDecke.</summary>
public sealed record SitzungGeschoss(string Kennzeichen, string Name, double Kote, double Hoehe, double DeckeOK, double? Dicke, double DeckeUK, bool OhneDecke);

/// <summary>Wahl auf dem Tablet: Etage (Kennzeichen), «Decke zeigen» und ein Ausschnitt (Text) – der Ausschnitt ersetzt die Etage.</summary>
public sealed record SitzungWahl(string? Geschoss, bool DeckeZeigen, string? Ausschnitt = null);

public static class XrSitzungLogik
{
    // ------------------------------------------------------------------------------------------------------ Masse (xr-sitzung-logik.js TISCH, RAUM, MASSSTAB)
    public const double TischHoehe = 0.75;
    public const double TischBreite = 1.6;
    public const double TischTiefe = 1.0;
    public const double TischVorderkante = 0.4;
    public const double TischRand = 0.1;
    /// <summary>xr-sitzung-logik.js TISCH.halt: Abstand der Modellmitte zum Rand der Platte.</summary>
    public const double TischHalt = 0.05;
    /// <summary>xr-sitzung-logik.js TISCH_MITTE: Mitte der Tischplatte (Oberkante) im Raum.</summary>
    public static readonly Vec3 TischMitte = new(0, TischHoehe, -(TischVorderkante + TischTiefe / 2));
    public const double RaumBreite = 8;
    public const double RaumTiefe = 6;
    public const double RaumHoehe = 3.2;
    public const double RaumHinten = 1.5;
    public const double MassstabStart = 50;
    /// <summary>Feinster Massstab beim Zoomen (Nenner), die eine Stelle dafür. Prototyp 1:20; Brillentest Christian 23.10.2026: «die modellansichten sollten &gt;1:20 gezoomt werden
    /// können» → 1:5 (Hauptlauf). Stellschraube im Labor P5.</summary>
    public const double MassstabMin = 5;
    public const double MassstabMax = 200;

    public const double GriffAbstandM = 0.06;      // m: Abstand eines Griffs zur Modellbreite (aussen, in der Lage des Modells gemessen)
    public const double GriffHoeheM = 0.05;        // m: Höhe der Griffe über der Tischplatte
    public const double GriffDurchmesserM = 0.025; // m: Grösse der Kugel eines Griffs (Anzeige)
    public const double GriffFangM = 0.05;         // m: so nah am Griff muss ein Pinch beginnen, damit er den Griff fasst
    public const double GriffHoverM = 0.05;        // m: so nah an einem freien Griff leuchtet er heller (Annäherung)
    public const double GriffMinHalbM = 0.02;      // m: kleinster Abstand der Griffe von der Modellmitte (winziges Modell)
    public const double HoeheMinM = -0.6;         // m: tiefste Lage des Modells unter der Platte (Brillentest 23.10.2026: «in der höhe … verschoben»), ANNAHME
    public const double HoeheMaxM = 0.9;          // m: höchste Lage über der Platte, ANNAHME
    public const double MinHandabstand = 0.03;     // m: kleinster Abstand zweier Hände, der noch zählt (gegen Teilen durch fast null)
    public const double MinRichtung = 0.03;        // m: kürzere Vektoren (Hand, Verbindungslinie) liefern keine Richtung
    public const double SpielMm = 0.5;             // mm: Flächen genau auf der Schnittebene bleiben/entfallen sicher
    public const double DeckenToleranzMm = 2;      // mm: so genau muss die Oberkante einer Decke auf der Kote liegen, damit sie zum Geschoss zählt
    public const int MaxEtagen = 8;                // so viele Geschosse (plus «Alle Etagen») passen auf das Tablet

    /// <summary>
    /// Der Schnitt «unter der Decke» liegt so viel tiefer als die Unterkante der Decke (mm). Christian 21.10.2026 nach dem Brillentest: «etagenwahl zeigt decke.
    /// ev. 5cm weiter unten schneiden.» (Die Decken kommen oft nur aus der IFC-Unterlage; KICAD kennt dann ihre Dicke nicht.) Nach dem nächsten Brillentest nachstellen.
    /// </summary>
    public const double SchnittTieferMm = 50;

    /// <summary>xr-sitzung-logik.js GESCHOSS_TEXT_MAX: so lang darf die Beschriftung einer Etagentaste sein.</summary>
    public const int GeschossTextMax = 26;
    /// <summary>xr-sitzung-logik.js SITZUNG_TASTE_H_MAX: Bildpunkte; höher werden die Tasten der Sitzungsraum-Seite bei wenigen Etagen nicht.</summary>
    public const double SitzungTasteHMax = 72;

    static double Klemme(double x, double a, double b) => Math.Min(b, Math.Max(a, x));

    /// <summary>xr-sitzung-logik.js dreheXZ: Drehung (x, z) um die Senkrechte; theta &gt; 0 = gegen den Uhrzeigersinn von oben (wie three rotation.y).</summary>
    public static Xz DreheXZ(Xz p, double theta) => new(p.X * Math.Cos(theta) + p.Z * Math.Sin(theta), -p.X * Math.Sin(theta) + p.Z * Math.Cos(theta));

    /// <summary>xr-sitzung-logik.js gierenVon: Richtung (x, z) → Winkel, gleiche Zählung wie <see cref="DreheXZ"/> (Gieren, atan2(−z, x)).</summary>
    public static double GierenVon(Xz d) => Math.Atan2(-d.Z, d.X);

    /// <summary>xr-sitzung-logik.js winkelDiff: Winkelunterschied b − a auf −π … π.</summary>
    public static double WinkelDiff(double b, double a)
    {
        var d = (b - a) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d <= -Math.PI) d += 2 * Math.PI;
        return d;
    }

    // ------------------------------------------------------------------------------------------------------ Massstab und Lage

    /// <summary>xr-sitzung-logik.js massstabBegrenzen: Massstab auf 1:MassstabMin (1:5) … 1:200; ungültige Werte fallen auf 1:50.</summary>
    public static double MassstabBegrenzen(double n) => Num.Ok(n) ? Klemme(n, MassstabMin, MassstabMax) : MassstabStart;

    /// <summary>xr-sitzung-logik.js massstabText.</summary>
    public static string MassstabText(double n) => $"1:{Num.Text(Num.Round(MassstabBegrenzen(n)))}";

    /// <summary>xr-sitzung-logik.js platte: nutzbare Platte (m).</summary>
    public static (double B, double T) Platte() => (TischBreite - 2 * TischRand, TischTiefe - 2 * TischRand);

    /// <summary>
    /// xr-sitzung-logik.js einpassen: Startlage: 1:50 auf der Mitte der Platte; passt das Modell (Grundfläche b × t in m) so nicht auf die nutzbare Platte, wird der
    /// Massstab aufgerundet (höchstens 1:200, dann passt = false). Ungültige Grössen: 1:50.
    /// </summary>
    public static Einpassung Einpassen(ModellGroesse? groesse, double start = MassstabStart)
    {
        var n = start;
        var passt = true;
        if (groesse is not null && groesse.B > 0 && groesse.T > 0)
        {
            var p = Platte();
            var noetig = Math.Max(groesse.B / p.B, groesse.T / p.T);
            if (noetig > n) n = Math.Ceiling(noetig);
            if (n > MassstabMax) { n = MassstabMax; passt = noetig <= n; }
        }
        return new Einpassung(new SitzungLage(MassstabBegrenzen(n), 0, 0, 0), passt);
    }

    /// <summary>xr-sitzung-logik.js lageBegrenzen: Mitte des Modells bleibt auf der Platte (halt = Abstand zum Rand).</summary>
    public static SitzungLage LageBegrenzen(SitzungLage lage)
    {
        double gx = TischBreite / 2 - TischHalt, gz = TischTiefe / 2 - TischHalt;
        return lage with { Massstab = MassstabBegrenzen(lage.Massstab), X = Klemme(lage.X, -gx, gx), Z = Klemme(lage.Z, -gz, gz), Hoehe = Klemme(double.IsFinite(lage.Hoehe) ? lage.Hoehe : 0, HoeheMinM, HoeheMaxM) };
    }

    /// <summary>xr-sitzung-logik.js zuruecksetzen: Zurücksetzen = Startlage (neu eingepasst).</summary>
    public static Einpassung Zuruecksetzen(ModellGroesse? groesse) => Einpassen(groesse);

    /// <summary>xr-sitzung-logik.js modellMitte: Mitte des Modells im Raum (x, z).</summary>
    public static Xz ModellMitte(SitzungLage lage) => new(TischMitte.X + lage.X, TischMitte.Z + lage.Z);

    // ------------------------------------------------------------------------------------------------------ Griffe

    /// <summary>
    /// Lage der beiden Griffe (Raum-lokal, m): an den beiden vorderen Gebäudeecken – Ecken der Modellbox (groesse.B × groesse.T, verkleinert) auf der dem Benutzer
    /// zugewandten Seite (+z in der Lage des Modells), links (Index 0) und rechts (Index 1), je <see cref="GriffAbstandM"/> schräg nach aussen, <see cref="GriffHoeheM"/>
    /// über der Platte, in der Drehung des Modells. Beim Zoomen und Drehen wandern sie mit. Brillentest Christian 23.10.2026: «die beiden greiffpunkte sollten an einer
    /// gebäudeecke sein.» (Prototyp xr-sitzung-logik.js griffPunkte: links und rechts der Modellmitte.)
    /// </summary>
    public static IReadOnlyList<GriffPunkt> GriffPunkte(SitzungLage lage, ModellGroesse? groesse)
    {
        var a = GriffAbstandM / Math.Sqrt(2);   // schräg nach aussen, zusammen GriffAbstandM
        var halbB = Math.Max((groesse is not null && Num.Ok(groesse.B) ? groesse.B : 0) / lage.Massstab / 2, GriffMinHalbM) + a;
        var halbT = Math.Max((groesse is not null && Num.Ok(groesse.T) ? groesse.T : 0) / lage.Massstab / 2, 0) + a;
        var m = ModellMitte(lage);
        return new[] { (Index: 0, Seite: "links", Vz: -1), (Index: 1, Seite: "rechts", Vz: 1) }.Select(g =>
        {
            var d = DreheXZ(new Xz(g.Vz * halbB, halbT), lage.Drehung);
            return new GriffPunkt(g.Index, g.Seite, m.X + d.X, TischHoehe + lage.Hoehe + GriffHoeheM, m.Z + d.Z);
        }).ToList();
    }

    /// <summary>xr-sitzung-logik.js strahlAbstand: Abstand des Punkts p vom Strahl (Ursprung u, Richtung r), nur nach vorn; hinter dem Ursprung zählt der Abstand zum Ursprung.</summary>
    public static double StrahlAbstand(Vec3 u, Vec3 r, Vec3 p)
    {
        var len = r.Laenge;
        if (!(len > 1e-9)) return u.Abstand(p);
        var d = new Vec3(r.X / len, r.Y / len, r.Z / len);
        var w = p - u;
        var t = w.X * d.X + w.Y * d.Y + w.Z * d.Z;
        if (t <= 0) return u.Abstand(p);
        return Num.Hypot(w.X - t * d.X, w.Y - t * d.Y, w.Z - t * d.Z);
    }

    /// <summary>
    /// xr-sitzung-logik.js griffTreffer: welcher Griff liegt in Fangreichweite? punkt = Pinch-Punkt bzw. Position des Controllers (Raum-lokal, mit y), strahl = Zielstrahl
    /// des Controllers oder null (Hände greifen nur in der Nähe). Rückgabe der Index des nächsten Griffs (0 | 1) oder null.
    /// </summary>
    public static int? GriffTreffer(Vec3? punkt, Strahl3? strahl, IReadOnlyList<GriffPunkt> griffe, double fang = GriffFangM)
    {
        if (punkt is not { } pt) return null;
        int? best = null;
        var bd = fang;
        foreach (var g in griffe)
        {
            var gv = new Vec3(g.X, g.Y, g.Z);
            var d = pt.Abstand(gv);
            if (strahl is not null) d = Math.Min(d, StrahlAbstand(strahl.U, strahl.R, gv));
            if (d <= bd) { bd = d; best = g.Index; }
        }
        return best;
    }

    // ------------------------------------------------------------------------------------------------------ Gesten

    /// <summary>
    /// xr-sitzung-logik.js handGieren: Richtung einer Hand um die Senkrechte (rad) aus den Gelenken (Handgelenk → Grundgelenk Mittelfinger, Raum-lokal) bzw. dem
    /// Zielstrahl; null, wenn senkrecht oder unbekannt.
    /// </summary>
    public static double? HandGieren(HandInfo? s)
    {
        var g = s?.Gelenke;
        if (g is not null && g.TryGetValue("wrist", out var w) && g.TryGetValue("middle-finger-phalanx-proximal", out var m))
        {
            var d = new Xz(m.X - w.X, m.Z - w.Z);
            if (Num.Hypot(d.X, d.Z) >= 0.02) return GierenVon(d);
        }
        if (s?.StrahlRichtung is { } r && Num.Hypot(r.X, r.Z) >= 0.3) return GierenVon(new Xz(r.X, r.Z));
        return null;
    }

    /// <summary>xr-sitzung-logik.js einhandStart: eine Hand greift: Modell starr an der Hand.</summary>
    public static EinhandGriff EinhandStart(SitzungLage lage, HandXz hand) => new(lage, new Xz(hand.X, hand.Z), hand.Gieren);

    /// <summary>xr-sitzung-logik.js einhandSchritt. (Bleibt für eine spätere Wahl erhalten; die Gestenerkennung verwendet zwei Hände.)</summary>
    public static SitzungLage EinhandSchritt(EinhandGriff g, HandXz hand)
    {
        var dphi = g.G0 is { } g0 && hand.Gieren is { } hg ? WinkelDiff(hg, g0) : 0;
        var m0 = ModellMitte(g.Lage0);
        var rel = DreheXZ(new Xz(m0.X - g.H0.X, m0.Z - g.H0.Z), dphi);
        var m = new Xz(hand.X + rel.X, hand.Z + rel.Z);
        return LageBegrenzen(g.Lage0 with { Drehung = g.Lage0.Drehung + dphi, X = m.X - TischMitte.X, Z = m.Z - TischMitte.Z });
    }

    /// <summary>xr-sitzung-logik.js zweihandStart: zwei Hände greifen (a, b Raum-lokal); ya, yb = Höhe der Hände (für das Verschieben in der Höhe).</summary>
    public static ZweihandGriff ZweihandStart(SitzungLage lage, Xz a, Xz b, double ya = 0, double yb = 0)
    {
        var mitte = new Xz((a.X + b.X) / 2, (a.Z + b.Z) / 2);
        var m0 = ModellMitte(lage);
        var q = DreheXZ(new Xz(mitte.X - m0.X, mitte.Z - m0.Z), -lage.Drehung);   // Punkt des Modells unter dem Mittelpunkt der Hände (verkleinert)
        var d = new Xz(b.X - a.X, b.Z - a.Z);
        var l = Num.Hypot(d.X, d.Z);
        return new ZweihandGriff(lage, Math.Max(l, MinHandabstand), l >= MinRichtung ? GierenVon(d) : null, new Xz(q.X * lage.Massstab, q.Z * lage.Massstab), (ya + yb) / 2);
    }

    /// <summary>
    /// xr-sitzung-logik.js zweihandSchritt: neue Lage aus dem Abstand (Zoom), der Richtung (Drehung) und dem Mittelpunkt (Verschieben in Breite und Tiefe) der Hände;
    /// dazu die Höhe: das Modell folgt der Höhe der Handmitte (Brillentest Christian 23.10.2026: «durch greiffen der punkte soll das modell auch gedreht, in der
    /// höhe und tiefe verschoben werden können»), begrenzt auf HoeheMinM … HoeheMaxM.
    /// </summary>
    public static SitzungLage ZweihandSchritt(ZweihandGriff g, Xz a, Xz b, double ya = 0, double yb = 0)
    {
        var d = new Xz(b.X - a.X, b.Z - a.Z);
        var l = Num.Hypot(d.X, d.Z);
        var abstand = Math.Max(l, MinHandabstand);
        var n = MassstabBegrenzen(g.Lage0.Massstab * g.Abstand0 / abstand);
        double? phi = l >= MinRichtung ? GierenVon(d) : null;
        var theta = g.Lage0.Drehung + (phi is { } p && g.Phi0 is { } p0 ? WinkelDiff(p, p0) : 0);
        var mitte = new Xz((a.X + b.X) / 2, (a.Z + b.Z) / 2);
        var q = DreheXZ(new Xz(g.Q0.X / n, g.Q0.Z / n), theta);
        var hoehe = g.Lage0.Hoehe + (ya + yb) / 2 - g.Y0;
        return LageBegrenzen(new SitzungLage(n, theta, mitte.X - q.X - TischMitte.X, mitte.Z - q.Z - TischMitte.Z, hoehe));
    }

    /// <summary>xr-sitzung-logik.js gestenNeu: Anfangszustand der Gestenerkennung.</summary>
    public static GestenZustand GestenNeu(SitzungLage lage) => new(lage, null, null, new GriffBesitz(null, null), new GriffVorher(false, false));

    /// <summary>
    /// xr-sitzung-logik.js gestenSchritt: ein Schritt der Gestenerkennung. Eine Hand hält einen Griff, wenn der Pinch in Fangreichweite beginnt (steigende Flanke, der
    /// Griff ist frei) – und bis sie loslässt. Bewegt wird das Modell **nur, solange beide Griffe gehalten sind** (verschieben, drehen, zoomen); hält nur eine Hand, ruht
    /// es (Modus «wartet»). Jeder Wechsel setzt einen neuen Bezug, damit nichts springt.
    /// </summary>
    public static GestenZustand GestenSchritt(GestenZustand g, GestenHaende haende)
    {
        int? bl = null, br = null;
        bool vl, vr;
        // linke Hand zuerst, dann die rechte (sie sieht den Besitz der linken schon)
        var hl = haende?.Links;
        vl = hl?.Griff == true;
        if (vl)
        {
            if (g.Besitzt.Links is not null) bl = g.Besitzt.Links;
            else if (!g.Vorher.Links && hl!.Nah is 0 or 1 && g.Besitzt.Rechts != hl.Nah && br != hl.Nah) bl = hl.Nah;
        }
        var hr = haende?.Rechts;
        vr = hr?.Griff == true;
        if (vr)
        {
            if (g.Besitzt.Rechts is not null) br = g.Besitzt.Rechts;
            else if (!g.Vorher.Rechts && hr!.Nah is 0 or 1 && g.Besitzt.Links != hr.Nah && bl != hr.Nah) br = hr.Nah;
        }
        var besitzt = new GriffBesitz(bl, br);
        var vorher = new GriffVorher(vl, vr);
        var n = (bl is not null ? 1 : 0) + (br is not null ? 1 : 0);
        var gleich = besitzt == g.Besitzt;
        var lage = g.Lage;
        var griff = g.Griff;
        var modus = n == 0 ? null : n == 1 ? "wartet" : "zwei";
        if (n < 2) griff = null;
        else
        {
            // a = die Hand am Griff 0 (links am Modell), b = die Hand am Griff 1: die Richtung a → b ist die Richtung der Verbindungslinie der Griffe
            var a = bl == 0 ? haende!.Links! : haende!.Rechts!;
            var b = bl == 0 ? haende.Rechts! : haende.Links!;
            if (!gleich || griff is null) griff = ZweihandStart(lage, new Xz(a.X, a.Z), new Xz(b.X, b.Z), a.Y, b.Y);
            else lage = ZweihandSchritt(griff, new Xz(a.X, a.Z), new Xz(b.X, b.Z), a.Y, b.Y);
        }
        return new GestenZustand(lage, modus, griff, besitzt, vorher);
    }

    // ------------------------------------------------------------------------------------------------------ Ausschnitt («Raum zeigen (VR)», 3D-Ansicht in VR)

    /// <summary>
    /// xr-sitzung-logik.js ausschnittBezug: Bezug des Sitzungsraums für einen Ausschnitt: der Quader (Ansichtseigenschaft schnittbereich bzw. der Quader von
    /// «Raum im 3D zeigen») wird auf den Tisch gestellt – seine Achsenbox (Welt, m) ist die Grösse, ihr Mittelpunkt (x, z) der Anker, ihre Unterkante der tiefste Punkt.
    /// Null, wenn der Quader fehlt, ungültig oder ausgeschaltet ist.
    /// </summary>
    public static AusschnittBezug? AusschnittBezugAus(SchnittbereichRoh? roh)
    {
        var sb = SchnittbereichLogik.Lesen(roh);
        if (sb is null || !sb.Aktiv) return null;
        var g = SchnittbereichLogik.WeltGrenzen(sb);
        return new AusschnittBezug(
            new ModellGroesse(g.Max.X - g.Min.X, g.Max.Z - g.Min.Z, g.Max.Y - g.Min.Y),
            new Xz((g.Min.X + g.Max.X) / 2, (g.Min.Z + g.Max.Z) / 2), g.Min.Y);
    }

    /// <summary>xr-sitzung-logik.js ausschnittLage: Startlage für einen Ausschnitt: so gross wie möglich (Start MassstabMin, Grenze des Zoomens), gröber nur, wenn er sonst nicht auf die Platte passt.</summary>
    public static Einpassung AusschnittLage(ModellGroesse? groesse) => Einpassen(groesse, MassstabMin);

    /// <summary>xr-sitzung-logik.js ausschnittEbenen: die sechs Clipping-Ebenen des Quaders in den Koordinaten der 3D-Ansicht (m); leer ohne gültigen Quader.</summary>
    public static IReadOnlyList<Clipebene> AusschnittEbenen(SchnittbereichRoh? roh)
    {
        var sb = SchnittbereichLogik.Lesen(roh);
        return sb is null ? [] : SchnittbereichLogik.Ebenen(sb).Select(e => new Clipebene(e.Normale, e.Konstante)).ToList();
    }

    // ------------------------------------------------------------------------------------------------------ Geschosse und Schnitthöhe

    /// <summary>
    /// xr-sitzung-logik.js geschosseAufbereiten: Geschosse für den Sitzungsraum. Nur Geschosse mit Kote und Höhe (wie «Auf Geschoss»). Rückgabe aufsteigend nach Kote.
    /// </summary>
    public static IReadOnlyList<SitzungGeschoss> GeschosseAufbereiten(IEnumerable<SitzungGeschossRoh>? geschosse, IEnumerable<DeckeRoh>? decken = null)
    {
        var gueltig = (geschosse ?? []).Where(g => Num.Ok(g.Kote) && Num.Ok(g.Hoehe))
            .OrderBy(g => g.Kote!.Value).ThenBy(g => g.Kennzeichen, NatuerlicherVergleich.Einfach).ToList();
        var deckenListe = (decken ?? []).ToList();
        var aus = new List<SitzungGeschoss>();
        for (var i = 0; i < gueltig.Count; i++)
        {
            var g = gueltig[i];
            double kote = g.Kote!.Value, hoehe = g.Hoehe!.Value;
            var darueber = gueltig.Where((x, j) => j != i && x.Kote!.Value > kote + 1 && x.TeilVon == g.TeilVon).FirstOrDefault();
            var deckeOK = darueber is not null ? darueber.Kote!.Value : kote + hoehe;
            var passend = deckenListe.Where(d => Num.Ok(d.Oberkante) && Num.Ok(d.Dicke) && d.Dicke!.Value > 0 && Math.Abs(d.Oberkante!.Value - deckeOK) <= DeckenToleranzMm).ToList();
            double? dicke = passend.Count > 0 ? passend.Max(d => d.Dicke!.Value) : null;
            aus.Add(new SitzungGeschoss(g.Kennzeichen, g.Name ?? "", kote, hoehe, deckeOK, dicke, dicke is null ? deckeOK : deckeOK - dicke.Value, dicke is null));
        }
        return aus;
    }

    /// <summary>xr-sitzung-logik.js schnittHoeheMm: Höhe des Schnitts in mm (Modell, z oben): <see cref="SchnittTieferMm"/> unter der Unterkante der Decke bzw. mit deckeZeigen deren Oberkante.</summary>
    public static double SchnittHoeheMm(SitzungGeschoss geschoss, bool deckeZeigen = false) => deckeZeigen ? geschoss.DeckeOK : geschoss.DeckeUK - SchnittTieferMm;

    /// <summary>
    /// xr-sitzung-logik.js schnittEbeneY: Höhe der Clipping-Ebene im Raum (m): Plattenoberkante + (Schnitthöhe − tiefster Punkt des Modells) / Massstab. Das Spiel schiebt die Ebene
    /// um <see cref="SpielMm"/> (im Modell) so, dass Flächen genau auf der Schnitthöhe nicht flackern: unter der Decke entfällt, was in der Ebene liegt; mit «Decke zeigen» bleibt es.
    /// </summary>
    public static double SchnittEbeneY(double schnittMm, bool deckeZeigen, double modellUnten, double massstab, double tischOben = TischHoehe)
    {
        var mm = schnittMm + (deckeZeigen ? SpielMm : -SpielMm);
        return tischOben + (mm / 1000 - modellUnten) / massstab;
    }

    /// <summary>xr-sitzung-logik.js kopfZeilen: Text für den Kopf des Tablets.</summary>
    public static IReadOnlyList<string> KopfZeilen(SitzungLage lage, SitzungWahl wahl, IReadOnlyList<SitzungGeschoss> geschosse)
    {
        var z = new List<string> { $"Sitzungsraum · Massstab {MassstabText(lage.Massstab)}" };
        var g = !string.IsNullOrEmpty(wahl.Geschoss) ? geschosse.FirstOrDefault(x => x.Kennzeichen == wahl.Geschoss) : null;
        if (!string.IsNullOrEmpty(wahl.Ausschnitt))
        {
            z.Add($"Ausschnitt: {wahl.Ausschnitt}");
            z.Add("«Alle Etagen» zeigt wieder das ganze Modell");
        }
        else if (g is null) z.Add("Alle Etagen");
        else
        {
            var hoehe = Num.ToFixed(SchnittHoeheMm(g, wahl.DeckeZeigen) / 1000, 2);
            z.Add($"{g.Kennzeichen} · {(wahl.DeckeZeigen ? "Schnitt über der Decke" : "Schnitt unter der Decke")} ({hoehe} m)");
            if (g.OhneDecke && !wahl.DeckeZeigen) z.Add("Keine Decke im Modell: Schnitt auf Oberkante Geschoss");
        }
        return z;
    }

    // ------------------------------------------------------------------------------------------------------ Tablet-Seite

    /// <summary>xr-sitzung-logik.js geschossText: Beschriftung eines Geschosses auf der Taste (Kennzeichen bis 8 Zeichen, sonst der Name; höchstens <see cref="GeschossTextMax"/> Zeichen).</summary>
    public static string GeschossText(SitzungGeschoss g) => GeschossText(g.Kennzeichen, g.Name);

    /// <summary>Wie <see cref="GeschossText(SitzungGeschoss)"/> aus Kennzeichen und Name.</summary>
    public static string GeschossText(string? kennzeichen, string? name = null)
    {
        var k = kennzeichen ?? "";
        var t = k.Length <= 8 ? k : (!string.IsNullOrEmpty(name) ? name : k);
        return t.Length > GeschossTextMax ? t[..(GeschossTextMax - 1)] + "…" : t;
    }

    /// <summary>
    /// xr-sitzung-logik.js sitzungsLayout: Tasten der Tablet-Seite im Sitzungsraum (Bildpunkte wie <see cref="XrTabletLogik.TabletLayout"/>): oben «Alle Etagen» und die
    /// Geschosse (oberstes zuerst, höchstens <see cref="MaxEtagen"/>), darunter «Decke zeigen», «Zurücksetzen», «IFC-Unterlage», dann «Begehen» und unten «VR beenden».
    /// </summary>
    public static IReadOnlyList<Taste> SitzungsLayout(IReadOnlyList<SitzungGeschoss> geschosse)
    {
        const double rand = 14, luecke = 8, fussH = 44;
        var eintraege = new List<(string Id, string Text)> { ("alle", "Alle Etagen") };
        eintraege.AddRange(geschosse.Reverse().Take(MaxEtagen).Select(g => ($"g:{g.Kennzeichen}", GeschossText(g))));
        var zeilen = eintraege.Count + 2;
        var raumH = XrTabletLogik.TabletPxH - XrTabletLogik.TabletKopfH - fussH - rand;
        var bh = Math.Min(SitzungTasteHMax, (raumH - (zeilen - 1) * luecke) / zeilen);
        var bw = XrTabletLogik.TabletPxB - 2 * rand;
        var bw2 = (bw - luecke) / 2;
        (double X, double Y, double W, double H) Zelle(int spalte, int zeile, bool zwei = false)
            => (rand + (zwei ? spalte * (bw2 + luecke) : 0), XrTabletLogik.TabletKopfH + zeile * (bh + luecke), zwei ? bw2 : bw, bh);
        Taste Mit(string id, string text, (double X, double Y, double W, double H) z, bool umschalter = false) => new(id, text, z.X, z.Y, z.W, z.H, umschalter);
        var tasten = eintraege.Select((e, i) => Mit(e.Id, e.Text, Zelle(0, i), true)).ToList();
        var z0 = eintraege.Count;
        tasten.Add(Mit("decke", "Decke zeigen", Zelle(0, z0, true), true));
        tasten.Add(Mit("zuruecksetzen", "Zurücksetzen", Zelle(1, z0, true)));
        tasten.Add(Mit("ifc", "IFC-Unterlage", Zelle(0, z0 + 1, true), true));
        tasten.Add(Mit("raum", "Begehen", Zelle(1, z0 + 1, true)));
        tasten.Add(new Taste(XrTabletLogik.TabletEnde.Id, XrTabletLogik.TabletEnde.Text, rand, XrTabletLogik.TabletPxH - fussH - rand / 2, XrTabletLogik.TabletPxB - 2 * rand, fussH - 4));
        return tasten;
    }

    /// <summary>xr-sitzung-logik.js sitzungsTasten: Zustand der Umschalter für die Tasten: gewählte Etage («g:Kennzeichen»), «Alle Etagen», «Decke zeigen», IFC.</summary>
    public static IReadOnlyDictionary<string, bool> SitzungsTasten(SitzungWahl wahl, bool ifcAn)
    {
        var z = new Dictionary<string, bool>
        {
            ["alle"] = string.IsNullOrEmpty(wahl.Geschoss) && string.IsNullOrEmpty(wahl.Ausschnitt),
            ["decke"] = wahl.DeckeZeigen,
            ["ifc"] = ifcAn,
        };
        if (!string.IsNullOrEmpty(wahl.Geschoss)) z[$"g:{wahl.Geschoss}"] = true;
        return z;
    }
}
