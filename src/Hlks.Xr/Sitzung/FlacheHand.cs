// Quest App P5b (Modul XR, «Ergänzungen» ins Interface-Labor, von Christian übernommen am 23.10.2026): Schnitt mit der flachen Hand am Sitzungstisch.
//   Auftrag P5b: «Am Sitzungstisch folgt der Schnitt der waagrecht gehaltenen flachen Hand (Handfläche nach unten, über dem Modell, kurz ruhig). Senkrecht
//   gehalten gibt sie einen Vertikalschnitt. Loslassen bzw. Hand weg behält den Schnitt; «Zurücksetzen» in der App «Tisch». Nutzt den vorhandenen Schnitt-Shader.»
// Rein, ohne StereoKit. Die App liest die Hände im Raum der Brille, ruft je Hand und Bild Schritt auf und zeichnet den Quader aus Kombiniert mit dem Shader
// «schnitt» (Assets/schnitt.hlsl, SchnittQuader.ShaderWerte).
//
// Erkennen (je Hand, je Bild). ANNAHMEN P5b, im Brillentest zu bestätigen; Ruhezeit und Winkel sind Stellschrauben («sitzung.schnitt_ruhe_s»,
// «sitzung.schnitt_toleranz_grad»), die übrigen Werte benannte Konstanten unten:
//   - flach: Finger gestreckt (XrTabletLogik.FaustVerhaeltnis über OffenAb 1.65; solange der Schnitt folgt, genügt OffenHaltenAb 1.4), kein Pinch;
//   - waagrecht: die Normale der Handfläche (XrTabletLogik.HandflaechenNormale, aus der Handfläche heraus) liegt höchstens ToleranzGrad (Vorgabe 25°) neben
//     senkrecht nach unten (Handfläche nach unten);
//     senkrecht: sie liegt höchstens ToleranzGrad neben der Waagrechten und zeigt nicht zum Gesicht (mindestens ZumKopfGrad = 45° neben der Richtung zum Kopf –
//     die linke Handfläche zum Gesicht holt die Apps); dazwischen zählt die Hand nicht. Solange der Schnitt folgt, gilt die Toleranz plus HystereseGrad (10°);
//   - über dem Modell: die Mitte der Handfläche liegt im Grundriss des Modells auf dem Tisch (Rechteck der Ansicht in den Achsen des Gebäudes bzw. die
//     Hüllbox, plus RandM = 5 cm) und höchstens UeberM = 30 cm über seiner Oberkante (im Raum der Brille gemessen);
//   - kurz ruhig: so erkannt mindestens RuheS (Vorgabe 0.3 s) und dabei die Mitte der Handfläche im Umkreis RuheRadiusM = 2 cm;
//   - nicht kurz nach einem Griff: SperreNachGriffS = 1 s nach dem Loslassen eines Griffs zählt die Hand nicht (die App gibt dann keine Gelenke).
// Dann folgt der Schnitt der Hand in jedem Bild, solange sie flach, in derselben Lage und über dem Modell bleibt; danach bleibt er stehen («Behalten»).
//
// Schnitt (Koordinaten der Szene, also vor der Verkleinerung auf den Tisch):
//   - waagrecht: alles über der Höhe der Handfläche fällt weg (wie der Etagenschnitt, aber frei in der Höhe);
//   - senkrecht: eine senkrechte Ebene durch die Mitte der Handfläche, parallel zu ihr; weg fällt die Seite zum Kopf hin – man blickt auf die Schnittfläche.
//     Liegt ihre Normale höchstens FangGrad = 10° neben einer Achse des Gebäudes (Rechteck der Ansicht), rastet sie dort ein.
// Zusammen mit dem Schnitt der Ansicht (Etagenschnitt bzw. Ausschnitt «Raum zeigen (VR)») gibt das einen Quader für den Shader (HandSchnitt.Kombiniert):
// waagrecht die kleinere Oberkante; senkrecht eine weitere Grenze. Hat die Ansicht schon waagrechte Grenzen (Ausschnitt), liegt die Ebene in der nächsten
// ihrer Achsen (der Shader kennt nur zwei waagrechte Achsen; die App ruft Schritt dann mit achsenPflicht).

using System.Numerics;

namespace Hlks.Xr.Sitzung;

/// <summary>Stellschrauben des Schnitts mit der flachen Hand (Annahmen P5b).</summary>
public sealed record FlacheHandEinstellungen
{
    /// <summary>«sitzung.schnitt_ruhe_s»: so lange muss die flache Hand ruhig über dem Modell liegen, bevor der Schnitt ihr folgt (s). Vorgabe 0.3 s.</summary>
    public double RuheS { get; init; } = 0.3;
    /// <summary>«sitzung.schnitt_toleranz_grad»: höchste Abweichung der Handfläche von waagrecht bzw. senkrecht (Grad). Vorgabe 25°.</summary>
    public double ToleranzGrad { get; init; } = 25;

    public static readonly FlacheHandEinstellungen Vorgabe = new();
}

/// <summary>Lage der flachen Hand: waagrecht (Handfläche nach unten) bzw. senkrecht.</summary>
public enum HandSchnittArt { Waagrecht, Senkrecht }

/// <summary>
/// Schnitt der flachen Hand in der Szene: waagrecht – weg fällt alles über <see cref="Hoehe"/> (y, m); senkrecht – Ebene durch <see cref="Punkt"/> (x, z) mit der
/// waagrechten Einheitsnormale <see cref="Normale"/> (zum Kopf hin); weg fällt alles mit Normale·(p − Punkt) &gt; 0.
/// </summary>
public sealed record HandSchnitt(HandSchnittArt Art, double Hoehe, Xz Punkt, Xz Normale)
{
    const double W = SchnittQuader.Weit;

    /// <summary>Quader für den Shader: der Schnitt der Ansicht (null = keiner) zusammen mit diesem Schnitt (siehe Dateikopf).</summary>
    public SchnittQuader Kombiniert(SchnittQuader? basis)
    {
        var b = basis ?? new SchnittQuader(1, 0, -W, W, 0, 1, -W, W, -W, W);
        if (Art == HandSchnittArt.Waagrecht) return b with { YMax = Math.Min(b.YMax, Hoehe) };
        var frei = b.UMin <= -W / 2 && b.UMax >= W / 2 && b.VMin <= -W / 2 && b.VMax >= W / 2;
        if (frei)
            return new SchnittQuader(Normale.X, Normale.Z, -W, Normale.X * Punkt.X + Normale.Z * Punkt.Z, -Normale.Z, Normale.X, -W, W, b.YMin, b.YMax);
        // Achsen der Ansicht: die Ebene liegt in der nächsten (U bzw. V, Vorzeichen nach der Normale)
        double du = Normale.X * b.Ux + Normale.Z * b.Uz, dv = Normale.X * b.Vx + Normale.Z * b.Vz;
        if (Math.Abs(du) >= Math.Abs(dv))
        {
            var u0 = Punkt.X * b.Ux + Punkt.Z * b.Uz;
            return du > 0 ? b with { UMax = Math.Min(b.UMax, u0) } : b with { UMin = Math.Max(b.UMin, u0) };
        }
        var v0 = Punkt.X * b.Vx + Punkt.Z * b.Vz;
        return dv > 0 ? b with { VMax = Math.Min(b.VMax, v0) } : b with { VMin = Math.Max(b.VMin, v0) };
    }

    /// <summary>Die vier Ecken der Schnittfläche innerhalb der Box (Szene), zum Zeichnen eines Rahmens; leer, wenn die Ebene die Box verfehlt.</summary>
    public IReadOnlyList<Vec3> Rahmen(Box3 box)
    {
        if (box.IstLeer) return [];
        if (Art == HandSchnittArt.Waagrecht)
        {
            if (Hoehe < box.Min.Y || Hoehe > box.Max.Y) return [];
            return [new(box.Min.X, Hoehe, box.Min.Z), new(box.Max.X, Hoehe, box.Min.Z), new(box.Max.X, Hoehe, box.Max.Z), new(box.Min.X, Hoehe, box.Max.Z)];
        }
        // Linie p = Punkt + t·e (e = Richtung in der Ebene) im Rechteck der Box beschneiden
        double ex = -Normale.Z, ez = Normale.X, t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
        foreach (var (p, e, lo, hi) in new[] { (Punkt.X, ex, box.Min.X, box.Max.X), (Punkt.Z, ez, box.Min.Z, box.Max.Z) })
        {
            if (Math.Abs(e) < 1e-12) { if (p < lo || p > hi) return []; continue; }
            double a = (lo - p) / e, c = (hi - p) / e;
            t0 = Math.Max(t0, Math.Min(a, c));
            t1 = Math.Min(t1, Math.Max(a, c));
        }
        if (!(t0 <= t1)) return [];
        Vec3 E(double t, double y) => new(Punkt.X + ex * t, y, Punkt.Z + ez * t);
        return [E(t0, box.Min.Y), E(t1, box.Min.Y), E(t1, box.Max.Y), E(t0, box.Max.Y)];
    }
}

/// <summary>Was ein Schritt ergab: nichts, Hand erkannt (wartet auf Ruhe), der Schnitt beginnt zu folgen, folgt, bleibt stehen (Hand weg bzw. nicht mehr flach).</summary>
public enum FlacheHandEreignis { Nichts, Erkannt, Beginnt, Folgt, Behalten }

/// <summary>Zustand einer Hand für <see cref="FlacheHand.Schritt"/>.</summary>
public sealed class FlacheHandZustand
{
    /// <summary>Erkannte Lage (wartet auf Ruhe bzw. folgt), sonst null.</summary>
    public HandSchnittArt? Art { get; internal set; }
    /// <summary>Der Schnitt folgt dieser Hand.</summary>
    public bool Folgt { get; internal set; }
    /// <summary>Letzter Schnitt dieser Hand (bleibt nach dem Loslassen).</summary>
    public HandSchnitt? Schnitt { get; internal set; }
    internal double? Seit;
    internal Vec3? RuhePunkt;

    public void Zuruecksetzen() { Art = null; Folgt = false; Schnitt = null; Seit = null; RuhePunkt = null; }
}

public static class FlacheHand
{
    /// <summary>Solange der Schnitt folgt, gilt die Hand bis zu diesem Verhältnis der Faust-Erkennung als offen (Hysterese zu XrTabletLogik.OffenAb 1.65).</summary>
    public const double OffenHaltenAb = 1.4;
    /// <summary>Hysterese der Winkel, solange der Schnitt folgt (Grad).</summary>
    public const double HystereseGrad = 10;
    /// <summary>Eine senkrechte Hand zählt nur, wenn ihre Normale so weit neben der Richtung zum Kopf liegt (Grad) – die Handfläche zum Gesicht holt die Apps.</summary>
    public const double ZumKopfGrad = 45;
    /// <summary>Rand um den Grundriss des Modells (m im Raum der Brille).</summary>
    public const double RandM = 0.05;
    /// <summary>So hoch über der Oberkante des Modells zählt die Hand noch (m im Raum der Brille).</summary>
    public const double UeberM = 0.30;
    /// <summary>Umkreis, in dem die Hand während der Ruhezeit bleiben muss (m im Raum der Brille).</summary>
    public const double RuheRadiusM = 0.02;
    /// <summary>Fang der senkrechten Ebene an die Achsen des Gebäudes (Grad).</summary>
    public const double FangGrad = 10;
    /// <summary>
    /// So lange nach dem Loslassen eines Griffs zählt diese Hand nicht als flache Hand (s) – wer das Modell gedreht oder gezoomt hat, öffnet die Hand meist flach über
    /// ihm (Annahme P5b; die App gibt der Regel dann keine Gelenke).
    /// </summary>
    public const double SperreNachGriffS = 1.0;

    static double Grad(double rad) => rad * 180 / Math.PI;

    /// <summary>
    /// Lage der Hand (Gelenke im Raum der Brille, WebXR-Namen): waagrecht, senkrecht oder null (nicht flach, schräg, Handfläche zum Gesicht). halten = der Schnitt
    /// folgt schon (Hysterese).
    /// </summary>
    public static HandSchnittArt? Lage(IReadOnlyDictionary<string, Vec3>? g, string seite, Vec3 kopf, double toleranzGrad, bool halten)
    {
        if (XrTabletLogik.FaustVerhaeltnis(g) is not { } v || v < (halten ? OffenHaltenAb : XrTabletLogik.OffenAb)) return null;
        if (XrTabletLogik.IstPinch(g)) return null;
        if (XrTabletLogik.HandflaechenNormale(g, seite) is not { } n || n.Laenge < 0.5 || XrTabletLogik.HandflaechenMitte(g) is not { } m) return null;
        var tol = toleranzGrad + (halten ? HystereseGrad : 0);
        var vonUnten = Grad(Math.Acos(Math.Clamp(-n.Y, -1, 1)));
        if (vonUnten <= tol) return HandSchnittArt.Waagrecht;
        var vonWaagrecht = Grad(Math.Asin(Math.Clamp(Math.Abs(n.Y), 0, 1)));
        if (vonWaagrecht > tol) return null;
        var zumKopf = (kopf - m).Einheit();
        if (zumKopf.Laenge > 0 && Grad(Math.Acos(Math.Clamp(n.Skalar(zumKopf), -1, 1))) < ZumKopfGrad) return null;
        return HandSchnittArt.Senkrecht;
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand (siehe Dateikopf). gelenke und kopf im Raum der Brille; modellZuRaum = Szene → Raum der Brille (Sitzungsraum.ModellMatrix, mit
    /// Massstab, Drehung und Lage des Tisches); modellBox = Hüllbox des Modells in der Szene; grundriss = Rechteck der Ansicht in den Achsen des Gebäudes (optional,
    /// für «über dem Modell» und den Fang der senkrechten Ebene); achsenPflicht = die Ebene muss in einer Achse des Grundrisses liegen (Ausschnitt).
    /// Gibt das Ereignis und den Schnitt dieser Hand zurück (bei Beginnt, Folgt, Behalten).
    /// </summary>
    public static (FlacheHandEreignis Ereignis, HandSchnitt? Schnitt) Schritt(FlacheHandZustand z, double t, IReadOnlyDictionary<string, Vec3>? gelenke, string seite,
        Vec3 kopf, Matrix4x4 modellZuRaum, Box3 modellBox, SichtbarRahmen? grundriss = null, bool achsenPflicht = false, FlacheHandEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? FlacheHandEinstellungen.Vorgabe;
        var art = Lage(gelenke, seite, kopf, e.ToleranzGrad, z.Folgt);
        var mitte = XrTabletLogik.HandflaechenMitte(gelenke);
        HandSchnitt? schnitt = null;
        var ueber = false;
        if (art is not null && mitte is { } m && !modellBox.IstLeer && Matrix4x4.Invert(modellZuRaum, out var zuSzene))
        {
            var faktor = ((Vec3)Vector3.TransformNormal(Vector3.UnitX, zuSzene)).Laenge;   // m der Szene je m im Raum der Brille (Massstab)
            var ms = (Vec3)Vector3.Transform((Vector3)m, zuSzene);
            ueber = UeberDemModell(ms, modellBox, grundriss, faktor);
            if (ueber)
            {
                var ns = ((Vec3)Vector3.TransformNormal((Vector3)XrTabletLogik.HandflaechenNormale(gelenke, seite)!.Value, zuSzene)).Einheit();
                var ks = (Vec3)Vector3.Transform((Vector3)kopf, zuSzene);
                schnitt = SchnittAus(art.Value, ms, ns, ks, grundriss, achsenPflicht);
            }
        }
        if (art is null || !ueber || mitte is null || (z.Folgt && art != z.Art))
        {
            if (z.Folgt)
            {
                z.Folgt = false; z.Art = null; z.Seit = null; z.RuhePunkt = null;
                return (FlacheHandEreignis.Behalten, z.Schnitt);
            }
            z.Art = null; z.Seit = null; z.RuhePunkt = null;
            return (FlacheHandEreignis.Nichts, null);
        }
        if (z.Folgt)
        {
            if (schnitt is not null) z.Schnitt = schnitt;
            return (FlacheHandEreignis.Folgt, z.Schnitt);
        }
        if (z.Art != art || z.RuhePunkt is not { } rp || rp.Abstand(mitte.Value) > RuheRadiusM || z.Seit is not { } seit)
        {
            z.Art = art; z.Seit = t; z.RuhePunkt = mitte;
            return (FlacheHandEreignis.Erkannt, null);
        }
        if (t - seit >= e.RuheS - 1e-9 && schnitt is not null)
        {
            z.Folgt = true;
            z.Schnitt = schnitt;
            return (FlacheHandEreignis.Beginnt, schnitt);
        }
        return (FlacheHandEreignis.Erkannt, null);
    }

    /// <summary>Liegt der Punkt (Szene) über dem Modell? faktor = m der Szene je m im Raum der Brille.</summary>
    public static bool UeberDemModell(Vec3 p, Box3 box, SichtbarRahmen? grundriss, double faktor)
    {
        if (box.IstLeer || !p.IstEndlich) return false;
        var rand = RandM * faktor;
        if (p.Y < box.Min.Y - rand || p.Y > box.Max.Y + UeberM * faktor) return false;
        if (grundriss is { Gueltig: true } r)
        {
            double u = r.U(p.X, p.Z), v = r.V(p.X, p.Z);
            return u >= r.UMin - rand && u <= r.UMax + rand && v >= r.VMin - rand && v <= r.VMax + rand;
        }
        return p.X >= box.Min.X - rand && p.X <= box.Max.X + rand && p.Z >= box.Min.Z - rand && p.Z <= box.Max.Z + rand;
    }

    /// <summary>
    /// Schnitt aus der Handfläche in der Szene: Mitte, Normale (Einheit) und Kopf. Senkrecht: Normale waagrecht, zum Kopf hin; Fang an die Achsen des Grundrisses
    /// (höchstens <see cref="FangGrad"/>, mit achsenPflicht immer die nächste). null, wenn die Normale keine waagrechte Richtung hat.
    /// </summary>
    public static HandSchnitt? SchnittAus(HandSchnittArt art, Vec3 mitte, Vec3 normale, Vec3 kopf, SichtbarRahmen? achsen = null, bool achsenPflicht = false)
    {
        var p = new Xz(mitte.X, mitte.Z);
        if (art == HandSchnittArt.Waagrecht) return new HandSchnitt(art, mitte.Y, p, new Xz(0, 0));
        var l = Num.Hypot(normale.X, normale.Z);
        if (l < 1e-6) return null;
        double nx = normale.X / l, nz = normale.Z / l;
        if (nx * (kopf.X - mitte.X) + nz * (kopf.Z - mitte.Z) < 0) { nx = -nx; nz = -nz; }   // zum Kopf hin: diese Seite fällt weg
        if (achsen is not null)
        {
            var kandidaten = new[] { (achsen.Ux, achsen.Uz), (-achsen.Ux, -achsen.Uz), (-achsen.Uz, achsen.Ux), (achsen.Uz, -achsen.Ux) };
            var (bx, bz) = kandidaten.MaxBy(a => a.Item1 * nx + a.Item2 * nz);
            var cos = bx * nx + bz * nz;
            if (achsenPflicht || cos >= Math.Cos(FangGrad * Math.PI / 180)) { var bl = Num.Hypot(bx, bz); (nx, nz) = (bx / bl, bz / bl); }
        }
        return new HandSchnitt(art, mitte.Y, p, new Xz(nx, nz));
    }
}
