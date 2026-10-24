// Quest App V1a: Prüfaufgabe «messen-wand-wand» (Katalog kataloge/xr_pruefaufgaben.json; «Miss den Abstand zwischen den beiden markierten Wänden»). Die App markiert
// zwei gegenüberliegende Wände eines Raums und kennt ihren Abstand (Sollmass); eine Messung gelingt, wenn sie von der einen zur anderen Wand geht und höchstens
// ToleranzMm vom Sollmass abweicht (Katalog: «höchstens 2 cm», Annahme P6). Rein, ohne StereoKit; Szene in Metern.
//
// Wahl des Paars: der Raum am Stand (sonst der, in dem ein Punkt 2 m vor dem Blick liegt, sonst der nächste im Geschoss); im Grundriss des Raums zwei parallele
// Randkanten (±ParallelGrad), die sich gegenüberliegen (die Mitte zwischen ihnen liegt im Raum), mindestens AbstandMinM auseinander und mindestens UeberlappMinM
// nebeneinander. Bevorzugt das Paar, über das man blickt (Normale nahe der Blickrichtung), dann die längere Überlappung. Sollmass: der Raumgrundriss liegt nicht
// immer genau auf den Wandflächen (Putz, Toleranz der Erkennung, eine Wand im Raum) – deshalb sucht die App je Kante mit Strahlen aus der Mitte des Raums die
// Fläche einer Wand (Ebene «wand» bzw. IfcWall… der Unterlage), die man von dort sieht (parallel ±ParallelGrad, höchstens FlaecheSucheM hinter der Kante); ohne
// Wandfläche gilt die Kante.
// ANNAHMEN V1a (Brillentest): ParallelGrad 3°, AbstandMinM 0.8 m, UeberlappMinM 0.5 m, FlaecheSucheM 0.25 m, Punkte höchstens PunktWandM = 5 cm neben der Wand,
// Markierung 2.2 m hoch.

using Hlks.Xr.Messen;
using Hlks.Xr.Modell;

namespace Hlks.Xr.Labor;

/// <summary>Eine markierte Wand: Punkt der Wandfläche, Normale in den Raum (waagrecht), Anfang und Ende der Markierung am Boden, Boden und Höhe.</summary>
public sealed record WandMarke(Vec3 Punkt, Vec3 Normale, Vec3 Von, Vec3 Bis, double Boden, double Hoehe)
{
    /// <summary>Abstand eines Punkts von der Wandfläche (m, mit Vorzeichen: positiv im Raum).</summary>
    public double Abstand(Vec3 p) => (p - Punkt).Skalar(Normale);

    /// <summary>Liegt p (höchstens rand neben der Fläche) auf der markierten Wand – entlang der Markierung (± spiel) und in der Höhe?</summary>
    public bool Auf(Vec3 p, double rand, double spiel = 0.3)
    {
        if (Math.Abs(Abstand(p)) > rand) return false;
        var d = Bis - Von;
        var l = d.Laenge;
        var s = l < 1e-9 ? 0 : (p - Von).Skalar(d * (1 / l));
        return s >= -spiel && s <= l + spiel && p.Y >= Boden - 0.1 && p.Y <= Boden + Hoehe + 1.5;
    }
}

/// <summary>Zwei gegenüberliegende Wände eines Raums und ihr Abstand (Sollmass, mm).</summary>
public sealed record WandPaar(string Raum, WandMarke A, WandMarke B, double SollMm);

public static class WandPaare
{
    public const double ParallelGrad = 3, AbstandMinM = 0.8, AbstandMaxM = 20, UeberlappMinM = 0.5, FlaecheSucheM = 0.25, PunktWandM = 0.05, MarkeHoeheM = 2.2;
    /// <summary>Erfolgskriterium (Katalog «höchstens 2 cm vom Sollmass», Annahme P6).</summary>
    public const double ToleranzMm = 20;

    /// <summary>Wände (Ebene «wand» des Modells, IfcWall… der Unterlage) – ihre Flächen geben das Sollmass.</summary>
    public static bool IstWand(SzenenElement e) =>
        e.Teil == Netz.GlbZwischenspeicher.TeilModell ? e.Art == "wand" : e.Art is { } a && a.StartsWith("IfcWall", StringComparison.OrdinalIgnoreCase);

    /// <summary>Das Wandpaar vor der Person (siehe Dateikopf); null, wenn es keinen Raum mit zwei gegenüberliegenden Kanten gibt.</summary>
    public static WandPaar? Waehlen(ModellSzene szene, string? geschossId, double boden, Vec3 kopf, double blickGieren)
    {
        var blick = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        var raum = RaumStandort.RaumBei(szene, geschossId, new Xz(kopf.X, kopf.Z), boden)
            ?? RaumStandort.RaumBei(szene, geschossId, new Xz(kopf.X + blick.X * 2, kopf.Z + blick.Z * 2), boden)
            ?? szene.Raeume.Where(r => geschossId is null || r.GeschossId == geschossId)
                .Select(r => (r, g: RaumStandort.Grundriss([.. szene.Ecken(r)])))
                .Where(x => x.g is not null && Math.Abs(x.g.Boden - boden) <= 0.5)
                .OrderBy(x => RaumStandort.Abstand(x.g!, new Xz(kopf.X, kopf.Z))).Select(x => x.r.Id).FirstOrDefault();
        if (raum is null || szene.Finde(raum) is not { } re) return null;
        var g = RaumStandort.Grundriss([.. szene.Ecken(re)]);
        if (g is null) return null;
        var kanten = Kanten(g.Rand);
        var cosPar = Math.Cos(ParallelGrad * Math.PI / 180);
        (int I, int J, double Wert)? best = null;
        for (int i = 0; i < kanten.Count; i++)
            for (int j = i + 1; j < kanten.Count; j++)
            {
                var (a0, a1) = kanten[i];
                var (b0, b1) = kanten[j];
                var da = Richtung(a0, a1);
                var db = Richtung(b0, b1);
                if (Math.Abs(da.X * db.X + da.Z * db.Z) < cosPar) continue;
                var n = new Xz(-da.Z, da.X);
                var abstand = (b0.X - a0.X) * n.X + (b0.Z - a0.Z) * n.Z;
                if (Math.Abs(abstand) < AbstandMinM || Math.Abs(abstand) > AbstandMaxM) continue;
                // Überlappung entlang der Kante
                double S(Xz p) => (p.X - a0.X) * da.X + (p.Z - a0.Z) * da.Z;
                double lo = Math.Max(Math.Min(S(a0), S(a1)), Math.Min(S(b0), S(b1))), hi = Math.Min(Math.Max(S(a0), S(a1)), Math.Max(S(b0), S(b1)));
                if (hi - lo < UeberlappMinM) continue;
                var mitteS = (lo + hi) / 2;
                var mitte = new Xz(a0.X + da.X * mitteS + n.X * abstand / 2, a0.Z + da.Z * mitteS + n.Z * abstand / 2);
                if (!RaumStandort.Innen(g, mitte)) continue;
                var wert = Math.Abs(n.X * blick.X + n.Z * blick.Z) * 10 + (hi - lo);
                if (best is null || wert > best.Value.Wert + 1e-9) best = (i, j, wert);
            }
        if (best is not { } b) return null;
        var (k0, k1) = kanten[b.I];
        var (m0, m1) = kanten[b.J];
        var d0 = Richtung(k0, k1);
        double T(Xz p) => (p.X - k0.X) * d0.X + (p.Z - k0.Z) * d0.Z;
        double ulo = Math.Max(Math.Min(T(k0), T(k1)), Math.Min(T(m0), T(m1))), uhi = Math.Min(Math.Max(T(k0), T(k1)), Math.Max(T(m0), T(m1)));
        var nn = new Xz(-d0.Z, d0.X);
        var dist = (m0.X - k0.X) * nn.X + (m0.Z - k0.Z) * nn.Z;
        var nA = dist > 0 ? new Vec3(nn.X, 0, nn.Z) : new Vec3(-nn.X, 0, -nn.Z);   // von A in den Raum (zu B)
        Vec3 Auf(Xz basis, double s, double versatz, Vec3 normale) => new(basis.X + d0.X * s + normale.X * versatz, g.Boden, basis.Z + d0.Z * s + normale.Z * versatz);
        var aVon = Auf(k0, ulo, 0, nA);
        var aBis = Auf(k0, uhi, 0, nA);
        var bBasis = new Xz(k0.X + nn.X * dist, k0.Z + nn.Z * dist);
        var bVon = Auf(bBasis, ulo, 0, nA);
        var bBis = Auf(bBasis, uhi, 0, nA);
        var halb = Math.Abs(dist) / 2;
        var wA = Flaeche(szene, geschossId, aVon, aBis, nA, g.Boden, halb);
        var wB = Flaeche(szene, geschossId, bVon, bBis, -nA, g.Boden, halb);
        var a = new WandMarke(aVon + nA * wA, nA, aVon + nA * wA, aBis + nA * wA, g.Boden, MarkeHoeheM);
        var bm = new WandMarke(bVon - nA * wB, -nA, bVon - nA * wB, bBis - nA * wB, g.Boden, MarkeHoeheM);
        var soll = Math.Abs((bm.Punkt - a.Punkt).Skalar(nA)) * 1000;
        return new WandPaar(raum, a, bm, Math.Round(soll, 1));
    }

    /// <summary>
    /// Versatz (m, in Richtung der Normale in den Raum) der Wandfläche, die man vom Raum aus sieht und an der man misst: Strahlen aus dem Raum (tiefe m vor der Kante
    /// von–bis, also aus der Mitte zwischen den beiden Kanten) gegen die Normale auf die Dreiecke der Wände des Geschosses (parallel ±ParallelGrad) – an fünf Stellen
    /// entlang der Kante (15 … 85 %) auf 0.8, 1.2 und 1.6 m über dem Boden; der Median der ersten Treffer. Ein Treffer zählt bis FlaecheSucheM hinter der Kante. Ohne
    /// Treffer 0 (die Kante). Befund Prüflauf Test_praxis 24.10.2026: lag der Grundriss auf der Aussenseite einer Wand im Raum, galt vorher die Fläche an der Kante
    /// (hinter der Wand, 250 mm zu weit) statt der sichtbaren.
    /// </summary>
    public static double Flaeche(ModellSzene szene, string? geschossId, Vec3 von, Vec3 bis, Vec3 normale, double boden, double tiefe)
    {
        var d = bis - von;
        var l = d.Laenge;
        if (l < 1e-9 || tiefe <= 0) return 0;
        var dir = d * (1 / l);
        var cosPar = Math.Cos(ParallelGrad * Math.PI / 180);
        // Band, in dem die Treffer liegen können (von FlaecheSucheM hinter der Kante bis zur Mitte), als Hüllbox für die Vorauswahl der Wände
        var band = Box3.Leer;
        foreach (var p in new[] { von, bis })
            foreach (var v in new[] { -FlaecheSucheM, tiefe })
                foreach (var h in new[] { 0.7, 1.7 })
                    band = band.Plus(p + normale * v + new Vec3(0, h, 0));
        var dreiecke = new List<(Vec3 A, Vec3 B, Vec3 C)>();
        foreach (var e in szene.Elemente.Values)
        {
            if (!IstWand(e) || (geschossId is not null && e.GeschossId != geschossId) || !Ueberlappt(e.Box, band)) continue;
            foreach (var (nr, ab, anzahl) in e.Bereiche)
            {
                var nd = szene.Netze[nr].Daten;
                for (int t = ab; t < ab + anzahl; t++)
                {
                    var a = nd.Ecke(nd.Indizes[3 * t]);
                    var b = nd.Ecke(nd.Indizes[3 * t + 1]);
                    var c = nd.Ecke(nd.Indizes[3 * t + 2]);
                    var n = (b - a).Kreuz(c - a);
                    if (n.Laenge < 1e-12 || Math.Abs(n.Einheit().Skalar(normale)) < cosPar) continue;
                    dreiecke.Add((a, b, c));
                }
            }
        }
        if (dreiecke.Count == 0) return 0;
        var treffer = new List<double>();
        var r = normale * -1;
        foreach (var s in new[] { 0.15, 0.325, 0.5, 0.675, 0.85 })
            foreach (var h in new[] { 0.8, 1.2, 1.6 })
            {
                var u = von + dir * (l * s) + normale * tiefe + new Vec3(0, boden + h - von.Y, 0);
                double? erster = null;
                foreach (var (a, b, c) in dreiecke)
                    if (SzenenFang.StrahlDreieck(u, r, a, b, c) is { } t && t <= tiefe + FlaecheSucheM && (erster is null || t < erster)) erster = t;
                if (erster is { } te) treffer.Add(tiefe - te);
            }
        if (treffer.Count == 0) return 0;
        treffer.Sort();
        return treffer[treffer.Count / 2];
    }

    static bool Ueberlappt(Box3 a, Box3 b) =>
        !a.IstLeer && !b.IstLeer && a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    /// <summary>Randkanten des Grundrisses zu geraden Kanten verbunden (Stücke mit gemeinsamem Ende in gleicher Richtung, ±1°; die Richtung der Stücke ist egal).</summary>
    public static List<(Xz A, Xz B)> Kanten(IReadOnlyList<(Xz A, Xz B)> rand)
    {
        var liste = rand.Where(k => Num.Hypot(k.B.X - k.A.X, k.B.Z - k.A.Z) > 1e-6).ToList();
        var cos = Math.Cos(Math.PI / 180);
        static bool Gleich(Xz p, Xz q) => Num.Hypot(p.X - q.X, p.Z - q.Z) <= 1e-6;
        bool geaendert = true;
        while (geaendert)
        {
            geaendert = false;
            for (int i = 0; i < liste.Count && !geaendert; i++)
                for (int j = i + 1; j < liste.Count && !geaendert; j++)
                {
                    var (a0, a1) = liste[i];
                    var (b0, b1) = liste[j];
                    var da = Richtung(a0, a1);
                    var db = Richtung(b0, b1);
                    if (Math.Abs(da.X * db.X + da.Z * db.Z) < cos) continue;
                    (Xz, Xz)? neu = Gleich(a1, b0) ? (a0, b1) : Gleich(a1, b1) ? (a0, b0) : Gleich(a0, b1) ? (b0, a1) : Gleich(a0, b0) ? (b1, a1) : null;
                    if (neu is not { } n) continue;
                    liste[i] = n;
                    liste.RemoveAt(j);
                    geaendert = true;
                }
        }
        return liste;
    }

    static Xz Richtung(Xz a, Xz b)
    {
        var l = Num.Hypot(b.X - a.X, b.Z - a.Z);
        return l < 1e-12 ? new Xz(1, 0) : new Xz((b.X - a.X) / l, (b.Z - a.Z) / l);
    }

    /// <summary>
    /// Prüft eine Messung gegen das Paar: beide Punkte höchstens <see cref="PunktWandM"/> neben je einer der beiden Wände (eine je Wand) und das Mass höchstens
    /// <see cref="ToleranzMm"/> neben dem Sollmass. Rückgabe: null = gelungen, sonst der Grund (Fehlgriff).
    /// </summary>
    public static string? Pruefen(WandPaar paar, Messung m)
    {
        var aA = paar.A.Auf(m.A.P, PunktWandM);
        var aB = paar.B.Auf(m.A.P, PunktWandM);
        var bA = paar.A.Auf(m.B.P, PunktWandM);
        var bB = paar.B.Auf(m.B.P, PunktWandM);
        if (!((aA && bB) || (aB && bA))) return "Messung nicht von Wand zu Wand";
        var abw = Math.Abs(m.Mass.AbstandMm - paar.SollMm);
        return abw <= ToleranzMm + 1e-9 ? null : FormattableString.Invariant($"Mass {abw:0} mm neben dem Sollmass");
    }
}
