// Quest App V1a (Modul XR, XR-C «Messwerkzeug mit Fang», entwurf/modul-xr.md): Fang auf der Szene der App. Die App hat die Körper des Kerns (/api/koerper) nicht,
// nur die GLB je Geschoss (P2) mit der Zuordnung Dreieck → Element (mesh.extras.kicad.elemente). Deshalb rechnet dieser Fang die Fangdaten aus den Dreiecken:
//   - Flächen: die Dreiecke selbst (Strahl: erster Treffer, beidseitig – die Unterlage aus IFC hat keine verlässliche Umlaufrichtung; Finger: Fusspunkt im Dreieck).
//   - Kanten: Kanten zwischen zwei Dreiecken, deren Flächennormalen um mehr als KanteGrad abweichen, und offene Ränder; Ecken der Dreiecke werden dazu auf
//     SchweissenM zusammengefasst (gleiche Lage = gleiche Ecke), gerade Ketten von Kanten zu einer Strecke verbunden (so liegt die «Mitte» in der Mitte der ganzen Kante).
//   - Ecken: Punkte, an denen Kanten mit mehr als EckeGrad abknicken (ein Rohr mit 24 Strecken je Kreis knickt am Rand um 15° – keine Ecken), dazu Enden offener Ketten.
//   - Leitungsachsen «soweit möglich» (Auftrag V1a): für Elemente der Ebenen leitung, formteil und armatur des Modells, deren Ecken auf einem Zylinder liegen
//     (Abweichung höchstens AchseAnteil des Radius bzw. AchseMinM), die Achse mit Radius und als Anschlüsse ihre Enden; für Kanäle (leitung als Quader, 8 Ecken)
//     die Mittellinie der längsten Richtung. Gekrümmte Formteile, Armaturen mit Griff usw. bekommen keine Achse, nur Flächen, Kanten und Ecken.
// Rangfolge und Fangradien wie der Prototyp (xr-mess-logik.js fangenPunkt/fangenStrahl, hier XrMessLogik): Anschluss > Ecke > Mitte einer Kante > Kante/Achse >
// Fläche > frei; Finger 3 cm, Strahl 3 cm + 3 cm je Meter. Am Sitzungstisch rechnet die App in der Szene (Modellkoordinaten) und gibt die Radien im Massstab mit.
// ANNAHMEN V1a (Brillentest): KanteGrad 30°, EckeGrad 45°, SchweissenM 0.5 mm, Achse 8 % bzw. 2 mm, höchstens KantenMaxDreiecke Dreiecke je Element für Kanten
// und Ecken (grössere Elemente fangen nur auf Flächen), Stücke zu StueckDreiecke Dreiecken mit eigener Hüllbox für den Strahl.
// V1d (Befund Brillentest V1 24.10.2026: 4 057 Abfragen, im Mittel 12 ms, einmal 1 013 ms auf der Quest): räumlicher Index statt linearer Suche. Bis V1a ging jede
// Abfrage alle Elemente durch und rechnete Kanten und Ecken eines Elements erst, wenn der Fang es zum ersten Mal brauchte – im Zwischenspeicher von höchstens 400
// Elementen. Am Tisch reicht der Fangradius im Massstab (1:50: 1.5 m + 3 % des Wegs in der Szene) über viele Elemente; der Zwischenspeicher lief über, die Kanten
// wurden immer wieder neu gerechnet (die Spitzen). Jetzt rechnet Aufbauen einmal je Modell im Hintergrund die Fangdaten aller Elemente und legt Stücke (Dreiecke),
// Punkte und Strecken in je einen BoxBaum (BVH). Punkt und Strahl suchen nur noch im Baum; Ergebnis, Rangfolge und Gleichstände wie bis V1a (feste Nummer je Eintrag
// in der Reihenfolge der Elemente nach Schlüssel; Test gegen den Fang bis V1a, tests/Hlks.Xr.Tests/FangReferenz.cs).
// Rein, ohne StereoKit; Szene in Metern (y oben), Ergebnisse wie XrMessLogik (FangErgebnis, Id = Schlüssel des Elements in der Szene, z. B. «modell:<Id>»).

using System.Diagnostics;
using Hlks.Xr.Modell;

namespace Hlks.Xr.Messen;

/// <summary>Kanten, Ecken, Achse und Dreiecksstücke eines Elements (einmal gerechnet, im Index des Fangs).</summary>
public sealed class ElementFang
{
    public required SzenenElement Element { get; init; }
    public List<FangPunkt> Punkte { get; } = [];
    public List<FangStrecke> Strecken { get; } = [];
    /// <summary>Radius der Achse (m), 0 ohne Achse.</summary>
    public double Radius { get; internal set; }
    /// <summary>Achse erkannt (Zylinder bzw. Kanal).</summary>
    public FangStrecke? Achse { get; internal set; }
    /// <summary>Dreiecke in Stücken mit Hüllbox (Netz, erstes Dreieck, Anzahl).</summary>
    public List<(int Netz, int Ab, int Anzahl, Box3 Box)> Stuecke { get; } = [];
    /// <summary>Kanten und Ecken nicht gerechnet (zu viele Dreiecke).</summary>
    public bool OhneKanten { get; internal set; }
}

public sealed class SzenenFang
{
    /// <summary>Kante: die Normalen der angrenzenden Dreiecke weichen um mehr als so viel ab (Grad), ANNAHME V1a.</summary>
    public const double KanteGrad = 30;
    /// <summary>Ecke: zwei Kanten knicken um mehr als so viel ab (Grad), ANNAHME V1a.</summary>
    public const double EckeGrad = 45;
    /// <summary>Ecken der Dreiecke näher als so viel (m) gelten als dieselbe Ecke.</summary>
    public const double SchweissenM = 0.0005;
    /// <summary>Gerade Kette: Kanten weichen höchstens so viel ab (Grad).</summary>
    public const double GeradeGrad = 2;
    /// <summary>Zylinder: Abweichung der Ecken vom mittleren Radius höchstens dieser Anteil bzw. mindestens AchseMinM.</summary>
    public const double AchseAnteil = 0.08, AchseMinM = 0.002;
    /// <summary>Kanten und Ecken nur bis zu so vielen Dreiecken je Element.</summary>
    public const int KantenMaxDreiecke = 60_000;
    /// <summary>Dreiecke je Stück (eigene Hüllbox für den Strahl).</summary>
    public const int StueckDreiecke = 64;
    /// <summary>Ebenen des Modells, deren Elemente eine Achse bekommen können (Zylinder); Kanal (Quader) nur bei «leitung».</summary>
    public static readonly IReadOnlySet<string> AchsenEbenen = new HashSet<string>(StringComparer.Ordinal) { "leitung", "formteil", "armatur" };

    readonly ModellSzene szene;
    readonly SzenenElement[] elemente;
    readonly object sperre = new();
    // V1d: eine Abfrage zur Zeit (Maske, Stempel; die App fragt im Hintergrund, die Prüfung im Bild)
    readonly object abfrageSperre = new();
    volatile Index? index;

    /// <summary>Der räumliche Index (V1d): Fangdaten je Element, Stücke, Punkte und Strecken mit fester Nummer, je ein BoxBaum.</summary>
    sealed class Index
    {
        public required ElementFang[] Fang;
        public required Dictionary<string, int> Nummer;
        public required (int Element, int Netz, int Ab, int Anzahl)[] Stuecke;
        public required (int Element, FangPunkt P)[] Punkte;
        public required (int Element, FangStrecke S)[] Strecken;
        /// <summary>Punkte nach Art getrennt (Anschluss vor Ecke): Nummern in <see cref="Punkte"/>, je ein Baum.</summary>
        public required int[] Anschluesse, Ecken;
        public required BoxBaum BaumStuecke, BaumAnschluesse, BaumEcken, BaumStrecken;
        public double GroessterRadius;
    }

    /// <summary>Welche Elemente zählen (z. B. nur das Geschoss am Stand, ohne IFC-Unterlage). Räume zählen nie. Gerechnet wird je Element einmal, wenn sich der
    /// Filter (die Referenz) ändert – die App setzt ihn darum nur bei einer Änderung neu.</summary>
    public Func<SzenenElement, bool>? Filter { get; set; }
    /// <summary>Welche Punkte sichtbar sind (z. B. im Schnittquader am Tisch); null = alle.</summary>
    public Func<Vec3, bool>? Sichtbar { get; set; }
    /// <summary>Wie viele Elemente bisher aufbereitet wurden (Protokoll; nach dem Aufbau alle).</summary>
    public int Aufbereitet { get; private set; }
    /// <summary>V1d: ist der Index gebaut (die App fragt erst dann, damit nichts im Bild wartet)?</summary>
    public bool Bereit => index is not null;
    /// <summary>V1d: Dauer des Aufbaus (ms) und die Zahl der Stücke, Punkte und Strecken im Index (Protokoll).</summary>
    public double AufbauMs { get; private set; }
    public (int Stuecke, int Punkte, int Strecken) Umfang => index is { } i ? (i.Stuecke.Length, i.Punkte.Length, i.Strecken.Length) : (0, 0, 0);

    // Filter je Element (bool je Nummer), neu bei einem anderen Filter
    Func<SzenenElement, bool>? maskeFuer;
    bool maskeGesetzt;
    bool[] maske = [];
    // je Abfrage: Eintritt des Strahls in die (gedehnte) Box eines Elements, mit Stempel
    double[] eintritt = [];
    int[] stempel = [];
    int abfrage;

    public SzenenFang(ModellSzene szene)
    {
        this.szene = szene;
        elemente = [.. szene.Elemente.Values.Where(e => e.Gruppe != ModellSzene.GruppeRaum && !e.Box.IstLeer).OrderBy(e => e.Schluessel, StringComparer.Ordinal)];
    }

    public ModellSzene Szene => szene;

    /// <summary>
    /// V1d: baut den Index (Fangdaten aller Elemente, drei BoxBäume) – einmal; die App ruft es nach dem Laden des Modells im Hintergrund auf. Punkt, Strahl und Fang
    /// bauen ihn selbst, wenn er noch fehlt (dann im Thread des Aufrufs).
    /// </summary>
    public void Aufbauen()
    {
        if (index is not null) return;
        lock (sperre)
        {
            if (index is not null) return;
            var uhr = Stopwatch.StartNew();
            var fang = new ElementFang[elemente.Length];
            var nummer = new Dictionary<string, int>(StringComparer.Ordinal);
            var stuecke = new List<(int, int, int, int)>();
            var stueckBoxen = new List<Box3>();
            var punkte = new List<(int, FangPunkt)>();
            var punktBoxen = new List<Box3>();
            var strecken = new List<(int, FangStrecke)>();
            var streckenBoxen = new List<Box3>();
            double radius = 0;
            for (int i = 0; i < elemente.Length; i++)
            {
                var f = Aufbereiten(szene, elemente[i]);
                fang[i] = f;
                nummer[elemente[i].Schluessel] = i;
                foreach (var (nr, ab, anzahl, box) in f.Stuecke) { stuecke.Add((i, nr, ab, anzahl)); stueckBoxen.Add(box); }
                foreach (var q in f.Punkte) { punkte.Add((i, q)); punktBoxen.Add(new Box3(q.P, q.P)); }
                foreach (var s in f.Strecken)
                {
                    var r = s.Radius ?? 0;
                    radius = Math.Max(radius, r);
                    strecken.Add((i, s));
                    streckenBoxen.Add(Box3.Leer.Plus(s.A).Plus(s.B) is var b && r > 0 ? new Box3(b.Min - new Vec3(r, r, r), b.Max + new Vec3(r, r, r)) : Box3.Leer.Plus(s.A).Plus(s.B));
                }
            }
            int[] anschluesse = [.. Enumerable.Range(0, punkte.Count).Where(k => punkte[k].Item2.Art == Fangart.Anschluss)];
            int[] ecken = [.. Enumerable.Range(0, punkte.Count).Where(k => punkte[k].Item2.Art != Fangart.Anschluss)];
            var neu = new Index
            {
                Fang = fang, Nummer = nummer, Stuecke = [.. stuecke], Punkte = [.. punkte], Strecken = [.. strecken], Anschluesse = anschluesse, Ecken = ecken,
                BaumStuecke = new BoxBaum(stueckBoxen), BaumAnschluesse = new BoxBaum([.. anschluesse.Select(k => punktBoxen[k])]),
                BaumEcken = new BoxBaum([.. ecken.Select(k => punktBoxen[k])]), BaumStrecken = new BoxBaum(streckenBoxen), GroessterRadius = radius,
            };
            eintritt = new double[elemente.Length];
            stempel = new int[elemente.Length];
            Aufbereitet = elemente.Length;
            AufbauMs = uhr.Elapsed.TotalMilliseconds;
            index = neu;
        }
    }

    Index Sicher()
    {
        if (index is null) Aufbauen();
        return index!;
    }

    bool[] Maske(Index ix)
    {
        var filter = Filter;   // einmal lesen: die App kann ihn im Bild neu setzen, während der Hintergrund fragt
        if (maskeGesetzt && ReferenceEquals(maskeFuer, filter) && maske.Length == elemente.Length) return maske;
        if (maske.Length != elemente.Length) maske = new bool[elemente.Length];
        for (int i = 0; i < elemente.Length; i++) maske[i] = filter?.Invoke(elemente[i]) ?? true;
        maskeFuer = filter;
        maskeGesetzt = true;
        return maske;
    }

    bool Zeigt(Vec3 p) => Sichtbar?.Invoke(p) ?? true;

    // ------------------------------------------------------------------------------------------------ Fangen mit dem Finger

    /// <summary>
    /// Fängt einen Punkt (Fingerspitze bzw. Pinch-Punkt) wie XrMessLogik.FangenPunkt: Anschluss/Ecke im Radius, Mitte einer Kante im halben Radius, Kante/Achse
    /// (an der Rohroberfläche), Fusspunkt auf einem Dreieck, sonst frei mit dem Eingangspunkt.
    /// </summary>
    public FangErgebnis Punkt(Vec3 p, double radius = XrMessLogik.FangRadiusFinger)
    {
        var ix = Sicher();
        lock (abfrageSperre) return Punkt(ix, p, radius);
    }

    FangErgebnis Punkt(Index ix, Vec3 p, double radius)
    {
        var zaehlt = Maske(ix);
        var R = radius;
        // Punkte: ein Anschluss geht jeder Ecke vor (der nähere; bei Gleichstand der frühere) – gibt es einen, ist er das Ergebnis; sonst die nächste Ecke (der Umkreis
        // schrumpft mit dem besten Treffer). Was ein Punkt ist, geht allem anderen vor (Rangfolge wie bis V1a): dann bleiben Strecken und Flächen ungefragt.
        int bP = -1; var bPd = R;
        double Punkte(BoxBaum baum, int[] nummern)
        {
            baum.Nahe(p, R, j =>
            {
                var k = nummern[j];
                var (e, q) = ix.Punkte[k];
                if (!zaehlt[e]) return double.PositiveInfinity;
                var d = p.Abstand(q.P);
                if (d <= R && (bP < 0 || d < bPd || (d == bPd && k < bP)) && Zeigt(q.P)) { bP = k; bPd = d; }
                return bP >= 0 ? bPd : double.PositiveInfinity;
            });
            return bPd;
        }
        Punkte(ix.BaumAnschluesse, ix.Anschluesse);
        if (bP < 0) Punkte(ix.BaumEcken, ix.Ecken);
        if (bP >= 0) return Ergebnis(ix.Punkte[bP].P.Art, ix.Punkte[bP].P.P, elemente[ix.Punkte[bP].Element].Schluessel);
        // Strecken: Mitte einer Kante (zählt nur bis zum halben Radius) und der nächste Punkt (bei Gleichstand der spätere, wie bis V1a); der Umkreis schrumpft
        int bM = -1; var bMd = R * 0.5; Vec3 bMp = default;
        int bS = -1; var bSd = R; Vec3 bSp = default;
        ix.BaumStrecken.Nahe(p, R, k =>
        {
            var (e, s) = ix.Strecken[k];
            if (!zaehlt[e]) return double.PositiveInfinity;
            var m = Vec3.Mitte(s.A, s.B);
            var dm = p.Abstand(m);
            if (s.Art == Fangart.Kante && (dm < bMd || (dm == bMd && k > bM)) && Zeigt(m)) { bM = k; bMd = dm; bMp = m; }
            var n = XrMessLogik.NaechsterAufStrecke3(p, s.A, s.B);
            var dEff = Math.Max(0, p.Abstand(n.P) - (s.Radius ?? 0));
            if ((dEff < bSd || (dEff == bSd && k > bS)) && Zeigt(n.P)) { bS = k; bSd = dEff; bSp = n.P; }
            return Math.Max(bMd, bSd);
        });
        if (bM >= 0) return Ergebnis(Fangart.Mitte, bMp, elemente[ix.Strecken[bM].Element].Schluessel);
        if (bS >= 0) return Ergebnis(ix.Strecken[bS].S.Art, bSp, elemente[ix.Strecken[bS].Element].Schluessel);
        // Flächen: Fusspunkt im Dreieck (bei Gleichstand der spätere)
        long bF = -1; var bFd = R; Vec3 bFp = default, bFn = default; var bFe = -1;
        ix.BaumStuecke.Nahe(p, R, k =>
        {
            var (e, nr, ab, anzahl) = ix.Stuecke[k];
            if (!zaehlt[e]) return double.PositiveInfinity;
            var d = szene.Netze[nr].Daten;
            for (int t = ab; t < ab + anzahl; t++)
            {
                var (a, b, c) = Dreieck(d, t);
                long nrT = (long)k * StueckDreiecke + (t - ab);
                if (Fusspunkt(p, a, b, c) is { } fp && (fp.Abstand < bFd || (fp.Abstand == bFd && nrT > bF)) && Zeigt(fp.P)) { bF = nrT; bFd = fp.Abstand; bFp = fp.P; bFn = fp.N; bFe = e; }
            }
            return bFd;
        });
        if (bF >= 0)
        {
            var n = (p - bFp).Skalar(bFn) < 0 ? -bFn : bFn;   // Normale zur Seite des Fingers
            return Ergebnis(Fangart.Flaeche, bFp, elemente[bFe].Schluessel, n, new Ebene3(n, bFp));
        }
        return Ergebnis(Fangart.Frei, p, null);
    }

    // ------------------------------------------------------------------------------------------------ Fangen mit dem Strahl

    /// <summary>
    /// Fängt mit dem Zielstrahl wie XrMessLogik.FangenStrahl: Anschluss/Ecke/Kante/Achse nahe am Strahl (Radius radius0 + jeMeter × Abstand), nichts hinter der ersten
    /// Fläche (5 cm Spiel), sonst der erste Treffer auf einem Dreieck (Normale gegen den Strahl), sonst frei ohne Punkt. r muss nicht die Länge 1 haben.
    /// </summary>
    public FangErgebnis Strahl(Vec3 u, Vec3 r0, double radius0 = XrMessLogik.FangRadiusStrahl, double jeMeter = XrMessLogik.FangRadiusJeMeter, double reichweite = 30)
    {
        var r = r0.Einheit();
        if (r.Laenge < 0.5) return Ergebnis(Fangart.Frei, null, null);
        var ix = Sicher();
        lock (abfrageSperre) return Strahl(ix, u, r, radius0, jeMeter, reichweite);
    }

    FangErgebnis Strahl(Index ix, Vec3 u, Vec3 r, double radius0, double jeMeter, double reichweite)
    {
        var zaehlt = Maske(ix);
        // 1. erster Treffer auf einem Dreieck (bei Gleichstand der frühere)
        double? tTreffer = null; Vec3 nTreffer = default; var eTreffer = -1; long nrTreffer = long.MaxValue;
        ix.BaumStuecke.Strahl(u, r, reichweite, 0, (k, _) =>
        {
            var (e, nr, ab, anzahl) = ix.Stuecke[k];
            if (!zaehlt[e]) return double.PositiveInfinity;
            var d = szene.Netze[nr].Daten;
            for (int t = ab; t < ab + anzahl; t++)
            {
                var (a, b, c) = Dreieck(d, t);
                long nrT = (long)k * StueckDreiecke + (t - ab);
                if (StrahlDreieck(u, r, a, b, c) is { } tt && tt <= reichweite && (tTreffer is null || tt < tTreffer || (tt == tTreffer && nrT < nrTreffer)) && Zeigt(u + r * tt))
                {
                    tTreffer = tt;
                    nTreffer = (b - a).Kreuz(c - a).Einheit();
                    eTreffer = e;
                    nrTreffer = nrT;
                }
            }
            return tTreffer ?? double.PositiveInfinity;
        });
        // 2. Punkte und Kanten nahe am Strahl, nicht hinter der ersten Fläche; nur Elemente, deren gedehnte Box der Strahl bis dorthin erreicht (wie bis V1a)
        var bis = Math.Min(reichweite, (tTreffer ?? reichweite) + 0.05);
        var dehnung = radius0 + jeMeter * bis + 0.01;
        abfrage++;
        bool Erreicht(int e)
        {
            if (stempel[e] != abfrage) { stempel[e] = abfrage; eintritt[e] = BoxStrahl(elemente[e].Box, u, r, dehnung) ?? double.PositiveInfinity; }
            return eintritt[e] <= bis;
        }
        // Punkte nahe am Strahl: ein Anschluss vor jeder Ecke (der nähere, bei Gleichstand der frühere); ein Punkt geht den Kanten vor – dann bleiben sie ungefragt
        int bP = -1; double bPt = 0; var bPd = double.PositiveInfinity;
        // Gesucht wird stufenweise von innen nach aussen (Dehnung radius0, doppelt, … bis zur vollen): der beste Treffer (kleinster Abstand zum Strahl) liegt innerhalb
        // jeder Dehnung, die mindestens so gross ist wie sein Abstand – findet eine enge Stufe einen gültigen, ist es derselbe wie mit der vollen Dehnung, nur ohne die
        // vielen Kandidaten im weiten Kegel (am Tisch 1.5 m + 3 % des Wegs in der Szene). Innerhalb einer Stufe schrumpft die Dehnung auf den besten Abstand.
        void Stufenweise(Action<double> suche, Func<double, bool> gefunden, double voll)
        {
            for (var d = Math.Min(voll, radius0 + 0.01); ; d = Math.Min(voll, d * 2))
            {
                suche(d);
                if (gefunden(d) || d >= voll) return;   // gefunden heisst: der beste liegt höchstens d vom Strahl (dann ist er sicher der beste)
            }
        }
        void Punkte(BoxBaum baum, int[] nummern) => Stufenweise(dd => baum.StrahlNahe(u, r, bis, dd, j =>
        {
            var k = nummern[j];
            var (e, q) = ix.Punkte[k];
            if (!zaehlt[e] || !Erreicht(e)) return double.PositiveInfinity;
            var w = q.P - u;
            var t = w.Skalar(r);
            if (t <= 0 || t > bis) return double.PositiveInfinity;
            var d = (w - r * t).Laenge;
            var tol = radius0 + jeMeter * t;
            if (d <= tol && (bP < 0 || d < bPd || (d == bPd && k < bP)) && Zeigt(q.P)) { bP = k; bPt = t; bPd = d; }
            return bP >= 0 ? bPd : double.PositiveInfinity;
        }, radius0 + 1e-9, jeMeter), d => bP >= 0 && bPd <= d, dehnung);
        Punkte(ix.BaumAnschluesse, ix.Anschluesse);
        if (bP < 0) Punkte(ix.BaumEcken, ix.Ecken);
        if (bP >= 0) return Ergebnis(ix.Punkte[bP].P.Art, ix.Punkte[bP].P.P, elemente[ix.Punkte[bP].Element].Schluessel, t: bPt);
        int bS = -1; Vec3 bSp = default; double bSt = 0; var bSd = double.PositiveInfinity;
        Stufenweise(dd => ix.BaumStrecken.StrahlNahe(u, r, bis + ix.GroessterRadius, dd, k =>
        {
            var (e, s) = ix.Strecken[k];
            if (!zaehlt[e] || !Erreicht(e)) return double.PositiveInfinity;
            var h = XrMessLogik.StrahlStrecke(u, r, s.A, s.B);
            if (h.TStrahl <= 0 || h.TStrahl > bis + (s.Radius ?? 0)) return double.PositiveInfinity;   // die Achse liegt um den Radius hinter der eigenen Oberfläche
            var tol = radius0 + jeMeter * h.TStrahl + (s.Radius ?? 0);
            if (h.Abstand <= tol && (h.Abstand < bSd || (h.Abstand == bSd && k < bS)) && Zeigt(h.PunktStrecke)) { bS = k; bSp = h.PunktStrecke; bSt = h.TStrahl; bSd = h.Abstand; }
            return bS >= 0 ? bSd : double.PositiveInfinity;   // weiter weg vom Strahl ist keine Strecke besser
        }, radius0 + 1e-9, jeMeter), d => bS >= 0 && bSd <= d, dehnung + jeMeter * ix.GroessterRadius);
        if (bS >= 0) return Ergebnis(ix.Strecken[bS].S.Art, bSp, elemente[ix.Strecken[bS].Element].Schluessel, t: bSt);
        if (tTreffer is { } th)
        {
            var n = nTreffer.Skalar(r) > 0 ? -nTreffer : nTreffer;   // zur Seite, von der der Strahl kommt
            var p = u + r * th;
            return Ergebnis(Fangart.Flaeche, p, elemente[eTreffer].Schluessel, n, new Ebene3(n, p), th);
        }
        return Ergebnis(Fangart.Frei, null, null);
    }

    static FangErgebnis Ergebnis(string art, Vec3? p, string? id, Vec3? n = null, Ebene3? flaeche = null, double? t = null)
        => new(art, Fangart.Namen[art], p, id, n, flaeche, t);

    // ------------------------------------------------------------------------------------------------ Geometrie

    static (Vec3 A, Vec3 B, Vec3 C) Dreieck(GlbNetzDaten d, int t) => (d.Ecke(d.Indizes[3 * t]), d.Ecke(d.Indizes[3 * t + 1]), d.Ecke(d.Indizes[3 * t + 2]));

    /// <summary>Eintritt des Strahls (u, r; r Einheit) in die um dehnung vergrösserte Box (t ≥ 0), null ohne Schnitt.</summary>
    public static double? BoxStrahl(Box3 b, Vec3 u, Vec3 r, double dehnung)
    {
        if (b.IstLeer) return null;
        double t0 = 0, t1 = double.PositiveInfinity;
        for (int k = 0; k < 3; k++)
        {
            double o = u.Komp(k), d = r.Komp(k), lo = b.Min.Komp(k) - dehnung, hi = b.Max.Komp(k) + dehnung;
            if (Math.Abs(d) < 1e-12)
            {
                if (o < lo || o > hi) return null;
                continue;
            }
            double a = (lo - o) / d, c = (hi - o) / d;
            if (a > c) (a, c) = (c, a);
            t0 = Math.Max(t0, a);
            t1 = Math.Min(t1, c);
            if (t0 > t1) return null;
        }
        return t0;
    }

    /// <summary>Strahl gegen Dreieck (Möller–Trumbore, beidseitig): t &gt; 0 oder null.</summary>
    public static double? StrahlDreieck(Vec3 u, Vec3 r, Vec3 a, Vec3 b, Vec3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = r.Kreuz(e2);
        var det = e1.Skalar(p);
        if (Math.Abs(det) < 1e-14) return null;
        var inv = 1 / det;
        var s = u - a;
        var bu = s.Skalar(p) * inv;
        if (bu < -1e-9 || bu > 1 + 1e-9) return null;
        var q = s.Kreuz(e1);
        var bv = r.Skalar(q) * inv;
        if (bv < -1e-9 || bu + bv > 1 + 1e-9) return null;
        var t = e2.Skalar(q) * inv;
        return t > 1e-9 ? t : null;
    }

    /// <summary>Fusspunkt von p auf der Ebene des Dreiecks, wenn er im Dreieck liegt: Punkt, Abstand, Normale (Einheit).</summary>
    public static (Vec3 P, double Abstand, Vec3 N)? Fusspunkt(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        var n0 = (b - a).Kreuz(c - a);
        var l = n0.Laenge;
        if (l < 1e-14) return null;
        var n = n0 * (1 / l);
        var d = (p - a).Skalar(n);
        var f = p - n * d;
        // baryzentrisch
        Vec3 v0 = b - a, v1 = c - a, v2 = f - a;
        double d00 = v0.Skalar(v0), d01 = v0.Skalar(v1), d11 = v1.Skalar(v1), d20 = v2.Skalar(v0), d21 = v2.Skalar(v1);
        var den = d00 * d11 - d01 * d01;
        if (Math.Abs(den) < 1e-18) return null;
        var v = (d11 * d20 - d01 * d21) / den;
        var w = (d00 * d21 - d01 * d20) / den;
        if (v < -1e-9 || w < -1e-9 || v + w > 1 + 1e-9) return null;
        return (f, Math.Abs(d), n);
    }

    // ------------------------------------------------------------------------------------------------ Fangdaten eines Elements

    /// <summary>Fangdaten eines Elements (aus dem Index; ein Element, das der Fang nicht kennt – Raum, leere Box –, wird einmalig gerechnet).</summary>
    public ElementFang Fang(SzenenElement e)
    {
        var ix = Sicher();
        return ix.Nummer.TryGetValue(e.Schluessel, out var i) ? ix.Fang[i] : Aufbereiten(szene, e);
    }

    /// <summary>Stücke, Kanten, Ecken und Achse eines Elements (siehe Dateikopf).</summary>
    public static ElementFang Aufbereiten(ModellSzene szene, SzenenElement e)
    {
        var f = new ElementFang { Element = e };
        foreach (var (nr, ab, anzahl) in e.Bereiche)
        {
            var d = szene.Netze[nr].Daten;
            for (int s = ab; s < ab + anzahl; s += StueckDreiecke)
            {
                var n = Math.Min(StueckDreiecke, ab + anzahl - s);
                var box = Box3.Leer;
                for (int t = s; t < s + n; t++) { var (a, b, c) = Dreieck(d, t); box = box.Plus(a).Plus(b).Plus(c); }
                f.Stuecke.Add((nr, s, n, box));
            }
        }
        if (e.Dreiecke > KantenMaxDreiecke) { f.OhneKanten = true; return f; }

        // Ecken zusammenfassen (Schlüssel auf SchweissenM gerundet)
        var ecken = new List<Vec3>();
        var nachSchluessel = new Dictionary<(long, long, long), int>();
        int Ecke(Vec3 p)
        {
            var k = ((long)Math.Round(p.X / SchweissenM), (long)Math.Round(p.Y / SchweissenM), (long)Math.Round(p.Z / SchweissenM));
            if (nachSchluessel.TryGetValue(k, out var i)) return i;
            nachSchluessel[k] = ecken.Count;
            ecken.Add(p);
            return ecken.Count - 1;
        }
        var kanten = new Dictionary<(int, int), (Vec3 N, int Anzahl, bool Knick)>();
        var cosKante = Math.Cos(KanteGrad * Math.PI / 180);
        foreach (var (nr, ab, anzahl) in e.Bereiche)
        {
            var d = szene.Netze[nr].Daten;
            for (int t = ab; t < ab + anzahl; t++)
            {
                var (pa, pb, pc) = Dreieck(d, t);
                int a = Ecke(pa), b = Ecke(pb), c = Ecke(pc);
                if (a == b || b == c || a == c) continue;
                var n = (pb - pa).Kreuz(pc - pa);
                if (n.Laenge < 1e-12) continue;
                n = n.Einheit();
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    var k = x < y ? (x, y) : (y, x);
                    if (!kanten.TryGetValue(k, out var alt)) kanten[k] = (n, 1, false);
                    else kanten[k] = (alt.N, alt.Anzahl + 1, alt.Knick || Math.Abs(alt.N.Skalar(n)) < cosKante);
                }
            }
        }
        var merkmal = kanten.Where(k => k.Value.Anzahl == 1 || k.Value.Knick).Select(k => k.Key).ToList();
        // Nachbarn je Ecke über die Merkmalkanten
        var nachbarn = new Dictionary<int, List<int>>();
        foreach (var (a, b) in merkmal)
        {
            if (!nachbarn.TryGetValue(a, out var la)) nachbarn[a] = la = [];
            if (!nachbarn.TryGetValue(b, out var lb)) nachbarn[b] = lb = [];
            la.Add(b);
            lb.Add(a);
        }
        Vec3 Richtung(int von, int nach) => (ecken[nach] - ecken[von]).Einheit();
        var cosGerade = Math.Cos(GeradeGrad * Math.PI / 180);
        bool Durchlauf(int v) => nachbarn.TryGetValue(v, out var l) && l.Count == 2 && Richtung(l[0], v).Skalar(Richtung(v, l[1])) >= cosGerade;
        // gerade Ketten zu Strecken verbinden
        var besucht = new HashSet<(int, int)>();
        foreach (var (a0, b0) in merkmal)
        {
            if (besucht.Contains((a0, b0))) continue;
            besucht.Add((a0, b0));
            int Weiter(int von, int bei)
            {
                while (Durchlauf(bei))
                {
                    var l = nachbarn[bei];
                    var naechste = l[0] == von ? l[1] : l[0];
                    var k = bei < naechste ? (bei, naechste) : (naechste, bei);
                    if (!besucht.Add(k)) break;
                    von = bei;
                    bei = naechste;
                }
                return bei;
            }
            var ende1 = Weiter(a0, b0);
            var ende2 = Weiter(b0, a0);
            if (ecken[ende1].Abstand(ecken[ende2]) > 1e-6) f.Strecken.Add(new FangStrecke(ecken[ende2], ecken[ende1], Fangart.Kante));
        }
        // Ecken: Knick um mehr als EckeGrad bzw. Ende einer offenen Kette bzw. drei und mehr Kanten
        var cosEcke = Math.Cos(EckeGrad * Math.PI / 180);
        foreach (var (v, l) in nachbarn)
        {
            var ecke = l.Count != 2 || Math.Abs(Richtung(v, l[0]).Skalar(Richtung(v, l[1]))) < cosEcke;
            if (ecke) f.Punkte.Add(new FangPunkt(ecken[v], Fangart.Ecke));
        }
        AchseSuchen(f, e, ecken);
        return f;
    }

    // ------------------------------------------------------------------------------------------------ Achse

    /// <summary>Leitungsachse (siehe Dateikopf): Zylinder über die drei Hauptachsen geprüft, sonst Kanal als Quader (nur «leitung»).</summary>
    static void AchseSuchen(ElementFang f, SzenenElement e, List<Vec3> ecken)
    {
        if (e.Teil != Netz.GlbZwischenspeicher.TeilModell || e.Art is null || !AchsenEbenen.Contains(e.Art) || ecken.Count < 8) return;
        var c = Vec3.Null;
        foreach (var p in ecken) c += p;
        c *= 1.0 / ecken.Count;
        var achsen = Hauptachsen(ecken, c);
        (Vec3 A, double R, double Min, double Max, double Fehler)? best = null;
        // Zylinder erst ab 12 Ecken (ein Quader hat 8 gleich weit von seiner Längsachse – das ist ein Kanal, kein Rohr)
        foreach (var a in ecken.Count >= 12 ? achsen : [])
        {
            double sMin = double.PositiveInfinity, sMax = double.NegativeInfinity;
            var radien = new List<double>(ecken.Count);
            foreach (var p in ecken)
            {
                var w = p - c;
                var s = w.Skalar(a);
                sMin = Math.Min(sMin, s); sMax = Math.Max(sMax, s);
                radien.Add((w - a * s).Laenge);
            }
            // ein gerades Rohr hat seine Ecken an den beiden Enden (Deckel ohne Mittelpunkt bzw. Ränder); sonst ist es gekrümmt oder kein Rohr
            var enden = ecken.Count(p => { var s = (p - c).Skalar(a); return s - sMin <= EndeToleranz(sMax - sMin) || sMax - s <= EndeToleranz(sMax - sMin); });
            if (enden < 0.9 * ecken.Count) continue;
            radien.Sort();
            var median = radien[radien.Count / 2];
            if (median < 0.001 || sMax - sMin < 0.001) continue;
            var rand = radien.Where(x => x >= 0.25 * median).ToList();   // Mittelpunkte der Deckel zählen nicht
            var mittel = rand.Average();
            var fehler = rand.Max(x => Math.Abs(x - mittel));
            if (fehler > Math.Max(AchseMinM, AchseAnteil * mittel)) continue;
            if (best is null || fehler / mittel < best.Value.Fehler) best = (a, mittel, sMin, sMax, fehler / mittel);
        }
        if (best is { } z)
        {
            Achse(f, c + z.A * z.Min, c + z.A * z.Max, z.R);
            return;
        }
        if (e.Art == "leitung" && ecken.Count == 8)
        {
            // Kanal (Quader): längste Richtung; Radius = halbe kleinere Seite des Querschnitts (an der Oberfläche fängt die Achse)
            var masse = achsen.Select(a =>
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                foreach (var p in ecken) { var s = (p - c).Skalar(a); lo = Math.Min(lo, s); hi = Math.Max(hi, s); }
                return (A: a, Lo: lo, Hi: hi, L: hi - lo);
            }).OrderByDescending(x => x.L).ToList();
            if (masse[0].L <= 0.001) return;
            Achse(f, c + masse[0].A * masse[0].Lo, c + masse[0].A * masse[0].Hi, Math.Min(masse[1].L, masse[2].L) / 2);
        }
    }

    /// <summary>So nahe (m) an einem Ende zählt eine Ecke zum Rand des Rohrs: 2 mm bzw. 2 % der Länge.</summary>
    static double EndeToleranz(double laenge) => Math.Max(0.002, 0.02 * laenge);

    static void Achse(ElementFang f, Vec3 a, Vec3 b, double radius)
    {
        var s = new FangStrecke(a, b, Fangart.Achse, radius);
        f.Achse = s;
        f.Radius = radius;
        f.Strecken.Add(s);
        f.Punkte.Add(new FangPunkt(a, Fangart.Anschluss));
        f.Punkte.Add(new FangPunkt(b, Fangart.Anschluss));
    }

    /// <summary>Hauptachsen der Punktwolke (Eigenvektoren der Kovarianz, Jacobi-Verfahren), als Einheitsvektoren.</summary>
    public static IReadOnlyList<Vec3> Hauptachsen(IReadOnlyList<Vec3> punkte, Vec3 c)
    {
        var m = new double[3, 3];
        foreach (var p in punkte)
        {
            var w = p - c;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++) m[i, j] += w.Komp(i) * w.Komp(j);
        }
        var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int runde = 0; runde < 50; runde++)
        {
            int p = 0, q = 1;
            var max = Math.Abs(m[0, 1]);
            if (Math.Abs(m[0, 2]) > max) { max = Math.Abs(m[0, 2]); p = 0; q = 2; }
            if (Math.Abs(m[1, 2]) > max) { max = Math.Abs(m[1, 2]); p = 1; q = 2; }
            if (max < 1e-15) break;
            var theta = (m[q, q] - m[p, p]) / (2 * m[p, q]);
            var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
            if (theta == 0) t = 1;
            var cs = 1 / Math.Sqrt(t * t + 1);
            var sn = t * cs;
            for (int k = 0; k < 3; k++)
            {
                double mkp = m[k, p], mkq = m[k, q];
                m[k, p] = cs * mkp - sn * mkq;
                m[k, q] = sn * mkp + cs * mkq;
            }
            for (int k = 0; k < 3; k++)
            {
                double mpk = m[p, k], mqk = m[q, k];
                m[p, k] = cs * mpk - sn * mqk;
                m[q, k] = sn * mpk + cs * mqk;
            }
            for (int k = 0; k < 3; k++)
            {
                double vkp = v[k, p], vkq = v[k, q];
                v[k, p] = cs * vkp - sn * vkq;
                v[k, q] = sn * vkp + cs * vkq;
            }
        }
        return [.. Enumerable.Range(0, 3).Select(j => new Vec3(v[0, j], v[1, j], v[2, j]).Einheit())];
    }
}
