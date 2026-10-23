// Modul XR (XR-C Messwerkzeug mit Fang, XR-B Masse zu Decke und Wand): Fang und Abstandsrechnung im Raum, rein und ohne Zeichnen.
// Quelle: wwwroot/js/ansichten/xr-mess-logik.js (Browser-Prototyp, unverändert; Node-Test xr-b.test.mjs).
// Beide Teile benutzen dieselbe Fanglogik: das Messwerkzeug setzt Punkte (Finger, Strahl), das Greifen einer Leitung zeigt die Masse zu Decke und Wand.
//
// Konventionen: Szene wie die 3D-Ansicht (x = Ost, y = oben, z = -Nord), Meter. Die Körper kommen vom Kern (/api/koerper) in Modell-mm (x Ost, y Nord, z oben) und
// werden hier umgerechnet. Ausgaben für den Menschen sind Modell-Masse: dx = Ost, dy = Nord, dz = oben, in mm.
//
// Fangdaten je Körper: Punkte (Ecken; bei Rohren, Formteilen, Armaturen, Apparaten die Enden der Achse als «anschluss»), Strecken (Kanten bzw. Achsen), Flächen
// (ebene Vielecke mit Normale nach aussen, auch mit Löchern). ANNAHMEN (Brillentest): Fangradius am Finger 3 cm, am Strahl 3 cm + 3 cm je Meter Abstand;
// Rangfolge Anschluss > Ecke > Mitte einer Kante > Kante/Achse > Fläche > frei; Anschlüsse von Formteilen sind die Enden ihrer Zylinderachse (der Kern liefert in den
// Körpern keine benannten Anschlüsse).

namespace Hlks.Xr;

/// <summary>Die Arten eines Fangs (xr-mess-logik.js FANGARTEN_XR).</summary>
public static class Fangart
{
    public const string Anschluss = "anschluss";
    public const string Ecke = "ecke";
    public const string Mitte = "mitte";
    public const string Achse = "achse";
    public const string Kante = "kante";
    public const string Flaeche = "flaeche";
    public const string Frei = "frei";

    /// <summary>xr-mess-logik.js FANGARTEN_XR: in der Rangfolge.</summary>
    public static readonly IReadOnlyList<string> Alle = [Anschluss, Ecke, Mitte, Achse, Kante, Flaeche, Frei];

    /// <summary>xr-mess-logik.js FANGNAMEN_XR: Anzeigenamen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Namen = new Dictionary<string, string>
    {
        [Anschluss] = "Anschluss", [Ecke] = "Ecke", [Mitte] = "Mitte", [Achse] = "Leitungsachse", [Kante] = "Kante", [Flaeche] = "Fläche", [Frei] = "frei",
    };
}

public sealed record FangPunkt(Vec3 P, string Art);
/// <summary>Strecke zum Fangen (Kante oder Leitungsachse); Radius nur bei der Achse (Rohroberfläche).</summary>
public sealed record FangStrecke(Vec3 A, Vec3 B, string Art, double? Radius = null);
/// <summary>Ebenes Vieleck mit Normale N nach aussen, einem Punkt P der Ebene und Löchern.</summary>
public sealed record FangFlaeche(Vec3[] Ring, Vec3 N, Vec3 P, Vec3[][] Loecher);
/// <summary>Ebene (Normale und ein Punkt), z. B. die Fläche, auf die ein Fang traf; Bezug für das Lotmass.</summary>
public sealed record Ebene3(Vec3 N, Vec3 P);

/// <summary>Fangdaten eines Körpers (xr-mess-logik.js fangdatenAusKoerper): Box, Punkte, Strecken, Flächen und der grösste Rohrradius.</summary>
public sealed class KoerperFangdaten
{
    public required string Id { get; init; }
    public string Ebene { get; init; } = "sonst";
    public Box3? Box { get; set; }
    public List<FangPunkt> Punkte { get; } = [];
    public List<FangStrecke> Strecken { get; } = [];
    public List<FangFlaeche> Flaechen { get; } = [];
    public double Radius { get; set; }
}

/// <summary>
/// Sammlung der Fangdaten (xr-mess-logik.js erzeugeFangdaten). Die Reihenfolge der Körper bleibt die des ersten Setzens (wie eine JavaScript-Map);
/// bei gleichem Abstand gewinnt der frühere.
/// </summary>
public sealed class Fangdaten
{
    readonly Dictionary<string, KoerperFangdaten> _karte = [];
    readonly List<string> _reihenfolge = [];

    public void Setzen(Koerper k)
    {
        var z = XrMessLogik.FangdatenAusKoerper(k);
        if (z.Box is not null)
        {
            if (!_karte.ContainsKey(k.Id)) _reihenfolge.Add(k.Id);
            _karte[k.Id] = z;
        }
        else Entfernen(k.Id);
    }

    public void Entfernen(string id)
    {
        if (_karte.Remove(id)) _reihenfolge.Remove(id);
    }

    public void Leeren()
    {
        _karte.Clear();
        _reihenfolge.Clear();
    }

    public int Anzahl => _karte.Count;
    public KoerperFangdaten? Get(string id) => _karte.GetValueOrDefault(id);
    public IReadOnlyList<KoerperFangdaten> Alle() => _reihenfolge.Select(id => _karte[id]).ToList();
}

/// <summary>Optionen für das Fangen: Radius (m), Ids, die nie gefangen werden (das gegriffene Element), Reichweite des Strahls (m, Vorgabe 30).</summary>
public sealed record FangOptionen(double? Radius = null, IReadOnlySet<string>? Ausser = null, double? Reichweite = null);

/// <summary>
/// Ergebnis eines Fangs. P ist bei «frei» der Eingangspunkt (Finger) bzw. null (Strahl ohne Treffer). N und Flaeche nur bei einem Fang auf einer Fläche;
/// T (Weg entlang des Strahls) nur beim Strahl.
/// </summary>
public sealed record FangErgebnis(string Art, string Name, Vec3? P, string? Id, Vec3? N = null, Ebene3? Flaeche = null, double? T = null);

/// <summary>Treffer eines Strahls auf einer Fläche der Fangdaten.</summary>
public sealed record StrahlTreffer(double T, Vec3 P, string Id, FangFlaeche F);

/// <summary>Nächste Punkte von Strahl und Strecke.</summary>
public readonly record struct StrahlStreckeErgebnis(double TStrahl, Vec3 PunktStrecke, double Abstand);

/// <summary>Abstand und Unterschiede von a nach b in mm (dx Ost, dy Nord, dz oben).</summary>
public readonly record struct Mass(double AbstandMm, double Dx, double Dy, double Dz);

/// <summary>Lotrechtes Mass zu einer Fläche: Abstand, Vorzeichen (+1 auf der Seite der Normale) und Fusspunkt auf der Ebene.</summary>
public sealed record LotErgebnis(double AbstandMm, int Vorzeichen, Vec3 Fuss);

/// <summary>Mass von einer Stelle zur Decke bzw. Wand: Abstand, Abstand minus Radius (zum Rand des Rohrs), Treffpunkt und Id des Bauteils.</summary>
public sealed record MassStelle(double AbstandMm, double AbstandRandMm, Vec3 Punkt, string Id);

/// <summary>Masse einer Stelle zur nächsten Decke darüber und zur nächsten Wand je Seite.</summary>
public sealed record DeckeWaendeErgebnis(MassStelle? Decke, MassStelle? WandA, MassStelle? WandB);

public static class XrMessLogik
{
    const double M = 1.0 / 1000;
    const double EPS = 1e-9;

    /// <summary>xr-mess-logik.js FANG_RADIUS_FINGER (m).</summary>
    public const double FangRadiusFinger = 0.03;
    /// <summary>xr-mess-logik.js FANG_RADIUS_STRAHL (m).</summary>
    public const double FangRadiusStrahl = 0.03;
    /// <summary>xr-mess-logik.js FANG_RADIUS_JE_METER: Zuwachs des Fangradius am Strahl je Meter Abstand.</summary>
    public const double FangRadiusJeMeter = 0.03;

    static readonly HashSet<string> AnschlussEbenen = ["leitung", "formteil", "armatur", "apparat"];
    static readonly HashSet<string> DeckenEbenen = ["decke"];
    static readonly HashSet<string> WandEbenen = ["wand"];

    // ---------------------------------------------------------------------------------------------------- Modell

    /// <summary>xr-mess-logik.js ausModell: Modellpunkt [x, y, z] in mm → Szenenpunkt in m.</summary>
    public static Vec3 AusModell(double[] p) => new(p[0] * M, (p.Length > 2 ? p[2] : 0) * M, -p[1] * M);

    // ---------------------------------------------------------------------------------------------------- Masse

    /// <summary>xr-mess-logik.js masse: Abstand und Unterschiede von a nach b: Abstand und dx (Ost), dy (Nord), dz (oben) in mm.</summary>
    public static Mass Masse(Vec3 a, Vec3 b)
    {
        var d = b - a;
        return new Mass(d.Laenge * 1000, d.X * 1000, -d.Z * 1000, d.Y * 1000);
    }

    /// <summary>xr-mess-logik.js massText: Länge für die Anzeige: ganze Millimeter, dazu Zentimeter mit einer Stelle («1234 mm · 123.4 cm»).</summary>
    public static string MassText(double mm)
    {
        var mmT = Math.Abs(mm) < 100 ? Num.ToFixed(Num.Round(mm * 10) / 10, 1) : Num.Text(Num.Round(mm));
        var text = $"{mmT} mm · {Num.ToFixed(Num.Round(mm) / 10, 1)} cm";
        var i = text.IndexOf("-0.0", StringComparison.Ordinal);   // JavaScript replace ersetzt nur das erste Vorkommen
        return i < 0 ? text : text[..i] + "0.0" + text[(i + 4)..];
    }

    /// <summary>xr-mess-logik.js massKurz: Kurzform ohne Zentimeter.</summary>
    public static string MassKurz(double mm)
        => $"{(Math.Abs(mm) < 100 ? Num.ToFixed(Num.Round(mm * 10) / 10, 1) : Num.Text(Num.Round(mm)))} mm";

    /// <summary>xr-mess-logik.js lotMass: lotrechtes Mass zu einer Fläche (Decke, Wand, Boden): Abstand von b zur Ebene, Fusspunkt auf der Ebene.</summary>
    public static LotErgebnis LotMass(Ebene3 flaeche, Vec3 b)
    {
        var n = flaeche.N.Einheit();
        var d = (b - flaeche.P).Skalar(n);
        return new LotErgebnis(Math.Abs(d) * 1000, d >= 0 ? 1 : -1, b - n * d);
    }

    // ---------------------------------------------------------------------------------------------------- Körper → Fangdaten

    static FangFlaeche FlaecheVon(Vec3[] ring, Vec3 n, Vec3[][]? loecher = null) => new(ring, n.Einheit(), ring[0], loecher ?? []);

    static void QuaderTeil(KoerperTeil t, KoerperFangdaten ziel)
    {
        if (t.Mitte is null || t.Mitte.Length < 2 || t.Masse is null || t.Masse.Length < 3) return;
        var c = AusModell(t.Mitte);
        double lx = t.Masse[0] * M / 2, ly = t.Masse[1] * M / 2, lz = t.Masse[2] * M / 2;
        var th = (t.Drehung ?? 0) * Num.Grad;
        double co = Math.Cos(th), si = Math.Sin(th);
        Vec3 ex = new(co, 0, -si), ey = new(-si, 0, -co), ez = new(0, 1, 0);
        Vec3 Ecke(int a, int b, int d) => c + ((ex * (a * lx) + ey * (b * ly)) + ez * (d * lz));
        var e = new List<Vec3>();
        foreach (var a in new[] { -1, 1 })
            foreach (var b in new[] { -1, 1 })
                foreach (var d in new[] { -1, 1 })
                    e.Add(Ecke(a, b, d));
        static int Idx(int a, int b, int d) => (a > 0 ? 4 : 0) + (b > 0 ? 2 : 0) + (d > 0 ? 1 : 0);
        foreach (var p in e) ziel.Punkte.Add(new FangPunkt(p, Fangart.Ecke));
        for (var i = 0; i < 8; i++)
            foreach (var bit in new[] { 1, 2, 4 })
                if ((i & bit) == 0) ziel.Strecken.Add(new FangStrecke(e[i], e[i | bit], Fangart.Kante));
        // Seiten: (Achse 0 = ex, 1 = ey, 2 = ez; Vorzeichen)
        foreach (var (achse, vz) in new[] { (0, 1), (0, -1), (1, 1), (1, -1), (2, 1), (2, -1) })
        {
            var n = (achse == 0 ? ex : achse == 1 ? ey : ez) * vz;
            (int, int, int)[] ring = achse == 0
                ? [(vz, -1, -1), (vz, 1, -1), (vz, 1, 1), (vz, -1, 1)]
                : achse == 1
                    ? [(-1, vz, -1), (1, vz, -1), (1, vz, 1), (-1, vz, 1)]
                    : [(-1, -1, vz), (1, -1, vz), (1, 1, vz), (-1, 1, vz)];
            ziel.Flaechen.Add(FlaecheVon(ring.Select(r => e[Idx(r.Item1, r.Item2, r.Item3)]).ToArray(), n));
        }
    }

    static Vec3[] FlaecheRing(IEnumerable<double[]> r, double y) => r.Select(p => new Vec3(p[0] * M, y, -p[1] * M)).ToArray();

    static void PrismaTeil(KoerperTeil t, KoerperFangdaten ziel)
    {
        var ring0 = (t.Grundriss ?? []).Where(p => p is { Length: >= 2 }).ToArray();
        if (ring0.Length < 3 || !Num.Ok(t.Unten) || !Num.Ok(t.Oben)) return;
        double yu = t.Unten!.Value * M, yo = t.Oben!.Value * M;
        var unten = FlaecheRing(ring0, yu);
        var oben = FlaecheRing(ring0, yo);
        var loecher = (t.Loecher ?? []).Select(l => FlaecheRing(l, 0)).ToArray();
        ziel.Flaechen.Add(FlaecheVon(oben, new Vec3(0, 1, 0), loecher));
        ziel.Flaechen.Add(FlaecheVon(unten, new Vec3(0, -1, 0), loecher));
        double a2 = 0;
        for (var i = 0; i < ring0.Length; i++)
        {
            var p = ring0[i];
            var q = ring0[(i + 1) % ring0.Length];
            a2 += p[0] * q[1] - q[0] * p[1];
        }
        var vz = a2 >= 0 ? 1 : -1;   // gegen den Uhrzeigersinn (Modell): Aussennormale einer Kante (dx, dy) ist (dy, −dx)
        for (var i = 0; i < ring0.Length; i++)
        {
            var p = ring0[i];
            var q = ring0[(i + 1) % ring0.Length];
            double dx = q[0] - p[0], dy = q[1] - p[1];
            if (Num.Hypot(dx, dy) < 1e-6) continue;
            double nMx = dy * vz, nMy = -dx * vz;   // Modell
            var n = new Vec3(nMx, 0, -nMy).Einheit();
            var j = (i + 1) % ring0.Length;
            ziel.Flaechen.Add(FlaecheVon([unten[i], unten[j], oben[j], oben[i]], n));
            ziel.Strecken.Add(new FangStrecke(unten[i], oben[i], Fangart.Kante));
            ziel.Strecken.Add(new FangStrecke(oben[i], oben[j], Fangart.Kante));
            ziel.Strecken.Add(new FangStrecke(unten[i], unten[j], Fangart.Kante));
            ziel.Punkte.Add(new FangPunkt(oben[i], Fangart.Ecke));
            ziel.Punkte.Add(new FangPunkt(unten[i], Fangart.Ecke));
        }
    }

    /// <summary>xr-mess-logik.js fangdatenAusKoerper: Fangdaten eines Körpers des Kerns.</summary>
    public static KoerperFangdaten FangdatenAusKoerper(Koerper k)
    {
        var z = new KoerperFangdaten { Id = k.Id, Ebene = k.Ebene ?? "sonst" };
        foreach (var t in k.Teile)
        {
            if (t.Art == "quader") QuaderTeil(t, z);
            else if (t.Art == "prisma") PrismaTeil(t, z);
            else if (t.Art == "zylinder" && t.Von is not null && t.Nach is not null)
            {
                var a = AusModell(t.Von);
                var b = AusModell(t.Nach);
                var art = AnschlussEbenen.Contains(z.Ebene) ? Fangart.Anschluss : Fangart.Ecke;
                z.Strecken.Add(new FangStrecke(a, b, Fangart.Achse, (t.Radius ?? 0) * M));
                z.Punkte.Add(new FangPunkt(a, art));
                z.Punkte.Add(new FangPunkt(b, art));
                z.Radius = Math.Max(z.Radius, (t.Radius ?? 0) * M);
            }
            else if (t.Art == "koerper" && t.Ecken is not null)
            {
                foreach (var e in t.Ecken.Take(300))
                    if (e is { Length: >= 3 }) z.Punkte.Add(new FangPunkt(AusModell(e), Fangart.Ecke));
            }
        }
        var box = Box3.Leer;
        foreach (var p in z.Punkte) box = box.Plus(p.P);
        foreach (var s in z.Strecken) box = box.Plus(s.A).Plus(s.B);
        foreach (var f in z.Flaechen)
            foreach (var p in f.Ring) box = box.Plus(p);
        if (!box.IstLeer) z.Box = box;
        return z;
    }

    // ---------------------------------------------------------------------------------------------------- Geometrie

    /// <summary>xr-mess-logik.js naechsterAufStrecke3: nächster Punkt auf der Strecke a–b zu p und der Parameter t (0 … 1).</summary>
    public static (Vec3 P, double T) NaechsterAufStrecke3(Vec3 p, Vec3 a, Vec3 b)
    {
        var ab = b - a;
        var l2 = ab.Skalar(ab);
        var t = l2 < EPS ? 0 : Math.Min(1, Math.Max(0, (p - a).Skalar(ab) / l2));
        return (a + ab * t, t);
    }

    /// <summary>xr-mess-logik.js strahlStrecke: nächste Punkte von Strahl (u, r) und Strecke a–b.</summary>
    public static StrahlStreckeErgebnis StrahlStrecke(Vec3 u, Vec3 r, Vec3 a, Vec3 b)
    {
        var d = b - a;
        var w = u - a;
        double A = r.Skalar(r), B = r.Skalar(d), C = d.Skalar(d), D = r.Skalar(w), E = d.Skalar(w);
        var N = A * C - B * B;
        double t;
        if (N < 1e-12) t = C < EPS ? 0 : Math.Min(1, Math.Max(0, E / C));
        else t = Math.Min(1, Math.Max(0, (A * E - B * D) / N));
        var q = a + d * t;
        var s = Math.Max(0, (q - u).Skalar(r) / A);
        var pr = u + r * s;
        return new StrahlStreckeErgebnis(s, q, pr.Abstand(q));
    }

    static (int A, int B) DominanteAchsen(Vec3 n)
    {
        double ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
        if (ay >= ax && ay >= az) return (0, 2);
        if (ax >= az) return (1, 2);
        return (0, 1);
    }

    static bool ImVieleck(Vec3 punkt, Vec3[] ring, (int A, int B) achsen)
    {
        var (A, B) = achsen;
        var innen = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var pi = ring[i];
            var pj = ring[j];
            if ((pi.Komp(B) > punkt.Komp(B)) != (pj.Komp(B) > punkt.Komp(B))
                && punkt.Komp(A) < ((pj.Komp(A) - pi.Komp(A)) * (punkt.Komp(B) - pi.Komp(B))) / (pj.Komp(B) - pi.Komp(B)) + pi.Komp(A))
                innen = !innen;
        }
        return innen;
    }

    /// <summary>xr-mess-logik.js imFlaeche: liegt der Punkt (auf der Ebene der Fläche) innerhalb des Vielecks? Löcher zählen als aussen.</summary>
    public static bool ImFlaeche(Vec3 punkt, FangFlaeche f)
    {
        var ax = DominanteAchsen(f.N);
        if (!ImVieleck(punkt, f.Ring, ax)) return false;
        foreach (var l in f.Loecher)
            if (l.Length >= 3 && ImVieleck(punkt, l, ax)) return false;
        return true;
    }

    /// <summary>xr-mess-logik.js strahlFlaeche: Parameter t (≥ 0), bei dem der Strahl die Fläche von vorn trifft (n·r &lt; 0; beidseitig auch von hinten), sonst null.</summary>
    public static double? StrahlFlaeche(Vec3 u, Vec3 r, FangFlaeche f, bool beidseitig = false)
    {
        var nr = f.N.Skalar(r);
        if (Math.Abs(nr) < 1e-9 || (!beidseitig && nr > 0)) return null;
        var t = (f.P - u).Skalar(f.N) / nr;
        if (!(t >= 0)) return null;
        return ImFlaeche(u + r * t, f) ? t : null;
    }

    /// <summary>xr-mess-logik.js fusspunktFlaeche: Fusspunkt von p auf der Ebene der Fläche, wenn er im Vieleck liegt.</summary>
    public static (Vec3 P, double Abstand)? FusspunktFlaeche(Vec3 p, FangFlaeche f)
    {
        var d = (p - f.P).Skalar(f.N);
        var fuss = p - f.N * d;
        return ImFlaeche(fuss, f) ? (fuss, Math.Abs(d)) : null;
    }

    // ---------------------------------------------------------------------------------------------------- Fangen

    static FangErgebnis Ergebnis(string art, Vec3? p, string? id, Vec3? n = null, Ebene3? flaeche = null, double? t = null)
        => new(art, Fangart.Namen[art], p, id, n, flaeche, t);

    /// <summary>
    /// xr-mess-logik.js fangenPunkt: fängt einen Punkt (Finger). Rückgabe mit Art, Name, Punkt, Id und bei einer Fläche Normale und Ebene; bei «frei» ist P der
    /// Eingangspunkt.
    /// </summary>
    public static FangErgebnis FangenPunkt(Fangdaten daten, Vec3 p, FangOptionen? opt = null)
    {
        var R = opt?.Radius ?? FangRadiusFinger;
        var ausser = opt?.Ausser;
        FangPunkt? bP = null; KoerperFangdaten? bPz = null; var bPd = R; var bPrio = 99;
        Vec3? bM = null; KoerperFangdaten? bMz = null; var bMd = R;
        Vec3? bS = null; KoerperFangdaten? bSz = null; var bSd = R; var bSArt = Fangart.Kante;
        FangFlaeche? bF = null; Vec3 bFp = default; KoerperFangdaten? bFz = null; var bFd = R;
        foreach (var z in daten.Alle())
        {
            if ((ausser?.Contains(z.Id) ?? false) || z.Box!.Value.Abstand(p) > R + z.Radius) continue;
            foreach (var q in z.Punkte)
            {
                var d = p.Abstand(q.P);
                var prio = q.Art == Fangart.Anschluss ? 0 : 1;
                if (d <= R && (prio < bPrio || (prio == bPrio && d < bPd))) { bP = q; bPz = z; bPd = d; bPrio = prio; }
            }
            foreach (var s in z.Strecken)
            {
                var m = Vec3.Mitte(s.A, s.B);
                var dm = p.Abstand(m);
                if (dm <= bMd) { bM = m; bMz = z; bMd = dm; }
                var n = NaechsterAufStrecke3(p, s.A, s.B);
                var d = p.Abstand(n.P);
                var dEff = Math.Max(0, d - (s.Radius ?? 0));   // an der Rohroberfläche fängt die Achse
                if (dEff <= bSd) { bS = n.P; bSz = z; bSd = dEff; bSArt = s.Art; }
            }
            foreach (var f in z.Flaechen)
            {
                var h = FusspunktFlaeche(p, f);
                if (h is { } hh && hh.Abstand <= bFd) { bF = f; bFp = hh.P; bFz = z; bFd = hh.Abstand; }
            }
        }
        if (bP is not null) return Ergebnis(bP.Art, bP.P, bPz!.Id);
        if (bM is not null && bMd <= R * 0.5) return Ergebnis(Fangart.Mitte, bM, bMz!.Id);
        if (bS is not null) return Ergebnis(bSArt, bS, bSz!.Id);
        if (bF is not null) return Ergebnis(Fangart.Flaeche, bFp, bFz!.Id, bF.N, new Ebene3(bF.N, bF.P));
        return Ergebnis(Fangart.Frei, p, null);
    }

    /// <summary>xr-mess-logik.js strahlTreffer: erster Treffer des Strahls mit einer Fläche der Fangdaten (Räume zählen nicht); null ohne Treffer.</summary>
    public static StrahlTreffer? StrahlTrefferErster(Fangdaten daten, Vec3 u, Vec3 r, FangOptionen? opt = null)
    {
        StrahlTreffer? best = null;
        foreach (var z in daten.Alle())
        {
            if ((opt?.Ausser?.Contains(z.Id) ?? false) || z.Ebene == "raum") continue;
            foreach (var f in z.Flaechen)
            {
                var t = StrahlFlaeche(u, r, f);
                if (t is { } tt && tt <= (opt?.Reichweite ?? 30) && (best is null || tt < best.T)) best = new StrahlTreffer(tt, u + r * tt, z.Id, f);
            }
        }
        return best;
    }

    /// <summary>
    /// xr-mess-logik.js fangenStrahl: fängt mit dem Zielstrahl: Anschluss/Ecke/Kante/Achse nahe am Strahl (Radius wächst mit dem Abstand), sonst der Treffer auf einer
    /// Fläche. Rückgabe wie <see cref="FangenPunkt"/>, plus T (Weg entlang des Strahls); bei «frei» ohne Treffer sind P und T null.
    /// </summary>
    public static FangErgebnis FangenStrahl(Fangdaten daten, Vec3 u, Vec3 r0, FangOptionen? opt = null)
    {
        var r = r0.Einheit();
        var treffer = StrahlTrefferErster(daten, u, r, opt);
        var reichweite = opt?.Reichweite ?? 30;
        FangPunkt? bP = null; KoerperFangdaten? bPz = null; double bPt = 0; var bPrio = 99; var bPd = double.PositiveInfinity;
        FangStrecke? bS = null; Vec3 bSp = default; KoerperFangdaten? bSz = null; double bSt = 0; var bSd = double.PositiveInfinity;
        foreach (var z in daten.Alle())
        {
            if (opt?.Ausser?.Contains(z.Id) ?? false) continue;
            foreach (var q in z.Punkte)
            {
                var w = q.P - u;
                var t = w.Skalar(r);
                if (t <= 0 || t > reichweite || (treffer is not null && t > treffer.T + 0.05)) continue;   // nichts hinter der ersten Fläche
                var d = (w - r * t).Laenge;
                var tol = FangRadiusStrahl + FangRadiusJeMeter * t;
                var prio = q.Art == Fangart.Anschluss ? 0 : 1;
                if (d <= tol && (prio < bPrio || (prio == bPrio && d < bPd))) { bP = q; bPz = z; bPt = t; bPrio = prio; bPd = d; }
            }
            foreach (var s in z.Strecken)
            {
                var h = StrahlStrecke(u, r, s.A, s.B);
                if (h.TStrahl <= 0 || h.TStrahl > reichweite || (treffer is not null && h.TStrahl > treffer.T + 0.05)) continue;
                var tol = FangRadiusStrahl + FangRadiusJeMeter * h.TStrahl + (s.Radius ?? 0);
                if (h.Abstand <= tol && h.Abstand < bSd) { bS = s; bSp = h.PunktStrecke; bSz = z; bSt = h.TStrahl; bSd = h.Abstand; }
            }
        }
        if (bP is not null) return Ergebnis(bP.Art, bP.P, bPz!.Id, t: bPt);
        if (bS is not null) return Ergebnis(bS.Art, bSp, bSz!.Id, t: bSt);
        if (treffer is not null) return Ergebnis(Fangart.Flaeche, treffer.P, treffer.Id, treffer.F.N, new Ebene3(treffer.F.N, treffer.F.P), treffer.T);
        return Ergebnis(Fangart.Frei, null, null, t: null);
    }

    // ---------------------------------------------------------------------------------------------------- Decke und Wände (XR-B)

    /// <summary>
    /// xr-mess-logik.js deckeUndWaende: Masse einer Stelle p zur nächsten Decke darüber und zur nächsten Wand je Seite. seiten = bis zu zwei waagrechte
    /// Richtungen (fehlt eine, entfällt die Wand dieser Seite). Gerechnet wird gegen die 3D-Körper (Ebenen decke und wand); der Kern hat dafür keine Abfrage
    /// (Stand 21.10.2026). opt: Radius (Rohrradius, für den Abstand zum Rand) und Ausser.
    /// </summary>
    public static DeckeWaendeErgebnis DeckeUndWaende(Fangdaten daten, Vec3 p, IReadOnlyList<Vec3>? seiten, FangOptionen? opt = null)
    {
        var radius = opt?.Radius ?? 0;
        MassStelle? Treffe(Vec3 richtung, HashSet<string> ebenen, Func<Vec3, bool> ok)
        {
            double? bestT = null; string? bestId = null;
            foreach (var z in daten.Alle())
            {
                if ((opt?.Ausser?.Contains(z.Id) ?? false) || !ebenen.Contains(z.Ebene)) continue;
                foreach (var f in z.Flaechen)
                {
                    if (!ok(f.N)) continue;
                    var t = StrahlFlaeche(p, richtung, f);
                    if (t is { } tt && (bestT is null || tt < bestT)) { bestT = tt; bestId = z.Id; }
                }
            }
            return bestT is { } b ? new MassStelle(b * 1000, Math.Max(0, b - radius) * 1000, p + richtung * b, bestId!) : null;
        }
        MassStelle? Wand(int i) => seiten is not null && i < seiten.Count ? Treffe(seiten[i].Einheit(), WandEbenen, n => Math.Abs(n.Y) < 0.3) : null;
        var decke = Treffe(new Vec3(0, 1, 0), DeckenEbenen, n => n.Y < -0.7);
        return new DeckeWaendeErgebnis(decke, Wand(0), Wand(1));
    }
}
