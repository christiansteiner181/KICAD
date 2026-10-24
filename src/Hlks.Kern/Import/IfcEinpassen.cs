// IFC einpassen (entwurf/ifc-einpassen.md; Auftrag Christian 24.10.2026: «automatische Ausrichtung über Wände und Raumkanten, mit
// Trockenlauf und Lagebericht»). Gesucht sind Drehung um z und Verschiebung in x/y, mit der die Wandflächen und Raumkanten einer
// IFC-Datei – gerechnet in der Lage «Gebäudeursprung» (IfcDatei.BezugVorschlag) – auf den Wänden und Räumen des Projekts liegen.
// Die Höhe kommt wie bei «gebaeude» aus den Geschosskoten der Datei (z des BezugVorschlags).
//
// Aufbau: Rechnen arbeitet nur auf 2D-Strecken je Geschoss (synthetisch prüfbar); ProjektMerkmale und DateiMerkmale gewinnen die
// Strecken; Ausfuehren verbindet beides, rechnet den Bezug und schreibt den Lagebericht (JSON-Feldnamen fest, siehe Spezifikation).
//
// Verfahren (Spezifikation «Verfahren», erprobt am Fall Bernstrasse 42; Abweichungen sind dort erlaubt, wenn die Tests sie tragen):
// 1. Hauptrichtung je Seite: längengewichtetes Histogramm der Kantenrichtungen modulo 90° (0.1°, geglättet), Spitze, gewichtetes
//    Mittel innert ±1°. Kandidaten θ = αProjekt − αDatei + k·90°, k = 0…3. Ohne klare Spitze (Anteil < 30 %) auf einer Seite: die
//    drei stärksten Spitzen je Seite kombiniert. Abweichung: Auch bei klarer Spitze zählen weitere Spitzen mit mindestens halbem
//    Gewicht der stärksten mit (zwei Gebäudeflügel unter einem Winkel: sonst bestimmte der zufällig stärkere Flügel jeder Seite θ).
// 2. Verschiebung je θ: Abstimmung über Eckpunkte (Kantenendpunkte, Datei gedreht, gleiches Geschoss), Raster 100 mm, Summe über
//    3 × 3 Zellen, die 5 besten je θ (mindestens 500 mm auseinander). Eckpunkte je Seite höchstens MaxEcken (gleichmässige, feste
//    Stichprobe).
// 3. Bewertung: Proben alle 200 mm auf den Kanten der Datei (höchstens MaxProben, feste Stichprobe; für die Vorauswahl der Kandidaten
//    höchstens MaxProbenGrob), Abstand zur nächsten Kante des Projekts im gleichen Geschoss über ein Rasterverzeichnis (Zellen 1 m).
//    Wert = Anteil der Proben innert 150 mm.
// 4. Verfeinern: ICP Punkt–Strecke mit fallender Grenze (400, 300, 200, 150, 100, 100, 80, 80, 60, 60 mm), je Schritt 2D-Kabsch.
//    Abweichungen (getragen von IfcEinpassenTests): (a) vor dem ICP eine Feinsuche ±300 mm im Raster 50 mm (meiste Treffer innert
//    150 mm) – die Stimmen liegen bei Raumkanten auf Wandflächen bis über eine halbe Wanddicke neben der Lage, und ICP spränge von dort
//    auf die andere Fläche derselben Wand; (b) jeder Kandidat wird mit der groben Stichprobe verfeinert, die besten sechs verschiedenen
//    Lagen (mehr als 1 m oder 2° auseinander) danach mit allen Proben – sonst stünde der verfeinerte Sieger gegen unverfeinerte
//    Nächstbeste und eine symmetrische Lage sähe eindeutiger aus, als sie ist; (c) nach dem Plan weiter mit 60 mm bis zur Ruhe (höchstens 40
//    Schritte) – entlang langer Wände gleitet Punkt–Punkt-ICP nur langsam, verlangt sind im synthetischen Fall 5 mm / 0.01°;
//    (d) Feinlage des Siegers auf das kleinste Mittel der besseren Hälfte der Abstände (Begründung bei Feinlage, Echt-Test).
// 5. Eindeutig, wenn Anteil(150 mm) ≥ 50 %, mindestens 30 Treffer und die beste Lage, die sich um mehr als 1 m oder 2° vom Sieger
//    unterscheidet, höchstens 70 % seines Anteils erreicht. Der Abstand zweier Lagen wird am Schwerpunkt der Proben gemessen (die
//    Verschiebung allein hängt am Ursprung der Datei, der weit vom Gebäude liegen kann).
// 6. Bezug: Modell = DrehungZ(−d)·(Welt − B). Mit B0/d0 = BezugVorschlag und Projekt = R(θ)·(Lage mit B0) + t gilt d = d0 − θ und
//    B = B0 − R(d)·t, z = B0.z (Herleitung bei Bezug).
// 7. Gegenprobe: IfcAusrichtung.Inhalt mit dem neuen Bezug (Überdeckung) in den Bericht.
//
// Geschosse: Jedes Merkmal gehört zum Projektgeschoss mit der grössten Kote ≤ Unterkante + 1000 mm (darunter: zum untersten).
// Verglichen wird je Geschoss, wenn die Geschosse mit mindestens MinKantenJeGeschoss Kanten auf beiden Seiten mindestens die Hälfte
// der Kantenlänge der Datei tragen (Annahme, die Spezifikation sagt nur «zu wenig Merkmale je Geschoss»); sonst alles zusammen.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Kante in der Ebene (mm): Wandfläche (Wand = true) oder Raumkante. Geschoss = Index in der aufsteigenden Geschossliste des
/// Projekts (−1 = das Projekt hat keine Geschosse).</summary>
public readonly record struct EinpassKante(double Ax, double Ay, double Bx, double By, int Geschoss, bool Wand)
{
    public double Laenge => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
}

/// <summary>Merkmale einer Seite: Kanten und Anzahl der Wände bzw. Räume, aus denen sie stammen.</summary>
public sealed class EinpassMerkmale
{
    public List<EinpassKante> Kanten { get; } = [];
    public int Waende { get; set; }
    public int Raeume { get; set; }
    /// <summary>Hinweis zur Gewinnung (z. B. Stichprobe bei grossen Dateien) oder null.</summary>
    public string? Hinweis { get; set; }
    public int Wandflaechen => Kanten.Count(k => k.Wand);
    public int Raumkanten => Kanten.Count(k => !k.Wand);

    /// <summary>Zwei Wandflächen (Achse ± halbe Dicke) einer geraden Wand; ohne Dicke die Achse.</summary>
    public void Wand(double ax, double ay, double bx, double by, double dicke, int geschoss)
    {
        double l = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        if (l < 1e-6) return;
        Waende++;
        if (dicke <= 0) { Kanten.Add(new(ax, ay, bx, by, geschoss, true)); return; }
        double nx = -(by - ay) / l * dicke / 2, ny = (bx - ax) / l * dicke / 2;
        Kanten.Add(new(ax + nx, ay + ny, bx + nx, by + ny, geschoss, true));
        Kanten.Add(new(ax - nx, ay - ny, bx - nx, by - ny, geschoss, true));
    }

    /// <summary>Kanten eines Raumpolygons (geschlossen).</summary>
    public void Raum(IReadOnlyList<double[]> ring, int geschoss)
    {
        if (ring.Count < 3) return;
        Raeume++;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            if (Math.Abs(b[0] - a[0]) + Math.Abs(b[1] - a[1]) > 1e-6) Kanten.Add(new(a[0], a[1], b[0], b[1], geschoss, false));
        }
    }
}

/// <summary>Geschoss des Projekts (Kennzeichen, Kote in mm).</summary>
public sealed record EinpassGeschoss(string Name, double KoteMm);

/// <summary>Eine Lage der Datei im Projekt: Projekt = R(Drehung)·Datei + (Tx, Ty), Grad und mm.</summary>
public sealed record EinpassLage(double Drehung, double Tx, double Ty, double Anteil150);

/// <summary>Zahlen eines Geschosses (Geschoss = Index in der Geschossliste des Projekts).</summary>
public sealed record EinpassGeschossWert(int Geschoss, int Proben, double Anteil150, double MedianMm);

/// <summary>Ergebnis der Rechnung (Rechnen).</summary>
public sealed class EinpassErgebnis
{
    /// <summary>eindeutig | unsicher | keine Merkmale</summary>
    public string Ergebnis { get; init; } = IfcEinpassen.KeineMerkmale;
    /// <summary>Warum unsicher bzw. keine Merkmale (sonst null).</summary>
    public string? Grund { get; init; }
    /// <summary>θ in Grad: Projekt = R(θ)·(Lage «Gebäudeursprung») + t.</summary>
    public double Drehung { get; init; }
    public double Tx { get; init; }
    public double Ty { get; init; }
    public double Anteil50 { get; init; }
    public double Anteil100 { get; init; }
    public double Anteil150 { get; init; }
    public double MedianMm { get; init; }
    public int Proben { get; init; }
    public int Treffer { get; init; }
    public EinpassLage? Zweitbester { get; init; }
    /// <summary>Alle Geschosse zusammen verglichen (keine Geschosse oder zu wenig Merkmale je Geschoss).</summary>
    public bool Gemeinsam { get; init; }
    /// <summary>«je Geschoss» oder «alle Geschosse zusammen (Grund)».</summary>
    public string Vergleich { get; init; } = "";
    public List<EinpassGeschossWert> JeGeschoss { get; init; } = [];
    public int Kandidaten { get; init; }
    public long RechenMs { get; init; }
}

public static class IfcEinpassen
{
    public const string Eindeutig = "eindeutig";
    public const string Unsicher = "unsicher";
    public const string KeineMerkmale = "keine Merkmale";

    // ---- Kennwerte des Verfahrens (Spezifikation «Verfahren»; Annahmen gekennzeichnet)
    /// <summary>Treffer-Grenze der Bewertung (mm).</summary>
    public const double Grenze = 150;
    /// <summary>Eindeutig ab diesem Anteil der Proben innert 150 mm.</summary>
    public const double MindestAnteil = 0.5;
    /// <summary>Eindeutig erst ab so vielen Treffern.</summary>
    public const int MindestTreffer = 30;
    /// <summary>Die nächstbeste andere Lage darf höchstens diesen Teil des Anteils des Siegers erreichen.</summary>
    public const double HoechstensZweiter = 0.7;
    /// <summary>Andere Lage: mehr als 1 m (am Schwerpunkt der Proben) oder mehr als 2° vom Sieger.</summary>
    public const double AndereLageMm = 1000, AndereLageGrad = 2;
    /// <summary>Klare Hauptrichtung ab diesem Anteil der Kantenlänge innert ±1° der Spitze.</summary>
    public const double KlareSpitze = 0.3;
    /// <summary>Abstand der Proben auf den Kanten der Datei (mm).</summary>
    public const double Probenabstand = 200;
    /// <summary>Annahme: Obergrenzen der festen Stichproben (Laufzeit grosser Modelle, BKW).</summary>
    public const int MaxProben = 6000, MaxProbenGrob = 1500, MaxProbenFeinsuche = 400, MaxEcken = 1000;
    /// <summary>Annahme: Je Geschoss verglichen wird nur mit so vielen Kanten auf beiden Seiten (ein Rechteckraum hat vier).</summary>
    public const int MinKantenJeGeschoss = 4;
    /// <summary>Merkmal gehört zum Geschoss mit der grössten Kote ≤ Unterkante + 1000 mm.</summary>
    public const double GeschossSpielraum = 1000;
    /// <summary>Annahme: Wände bzw. Räume der Datei, die höchstens gerechnet werden (gleichmässige Stichprobe nach EntityLabel).</summary>
    public const int MaxWaende = 1500, MaxRaeume = 1500;

    static readonly double[] IcpGrenzen = [400, 300, 200, 150, 100, 100, 80, 80, 60, 60];
    const double Raster = 100;          // Abstimmung der Verschiebung (mm)
    const int BesteJeDrehung = 5;
    const int Verfeinert = 6;
    const double KurzeKante = 1;        // kürzere Kanten zählen nicht (mm)
    const double FeinlageAnteil = 0.7;  // Feinlage: Mittel der besseren 70 % der Abstände (Annahme, siehe Feinlage)

    // ------------------------------------------------------------------ Geschosse

    /// <summary>Index des Projektgeschosses zu einer Unterkante: grösste Kote ≤ z + 1000 mm, darunter das unterste; −1 ohne Geschosse.</summary>
    public static int GeschossIndex(IReadOnlyList<EinpassGeschoss> geschosse, double z)
    {
        if (geschosse.Count == 0) return -1;
        int aus = 0;
        for (int i = 0; i < geschosse.Count; i++) if (geschosse[i].KoteMm <= z + GeschossSpielraum) aus = i;
        return aus;
    }

    // ------------------------------------------------------------------ Bezug

    /// <summary>Bezug aus der gefundenen Abbildung. Lage mit B0: L = R(−d0)·(W − B0); gefunden P = R(θ)·L + t. Gesucht P = R(−d)·(W − B):
    /// P = R(θ − d0)·(W − B0) + t; mit d = d0 − θ ist R(−d) = R(θ − d0), also P = R(−d)·(W − B0 + R(d)·t) = R(−d)·(W − (B0 − R(d)·t)).</summary>
    public static IfcBezug Bezug(IfcBezug b0, double theta, double tx, double ty)
    {
        double d = Winkel(b0.Drehung - theta), w = d * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return new IfcBezug(b0.X - (c * tx - s * ty), b0.Y - (s * tx + c * ty), b0.Z, d);
    }

    /// <summary>Winkel nach (−180°, 180°].</summary>
    public static double Winkel(double w) { w %= 360; if (w > 180) w -= 360; if (w <= -180) w += 360; return w; }

    // ------------------------------------------------------------------ Rechnung

    readonly record struct Probe(double X, double Y, int G, int Herkunft);
    readonly record struct Ecke(double X, double Y);
    sealed record Kandidat(double Theta, double Tx, double Ty, double Anteil, bool Fein);

    /// <summary>Sucht die Lage der Datei im Projekt (reine Rechnung auf 2D-Strecken). geschosse = Anzahl Projektgeschosse (Kanten tragen
    /// ihren Index; 0 = keine Geschosse).</summary>
    public static EinpassErgebnis Rechnen(EinpassMerkmale projekt, EinpassMerkmale datei, int geschosse)
    {
        var uhr = Stopwatch.StartNew();
        // nach Lage geordnet: Stichproben und Gleichstände hängen so nicht an der Reihenfolge der Elemente (Ids sind zufällig)
        static List<EinpassKante> Geordnet(IEnumerable<EinpassKante> k) => k.Where(x => x.Laenge >= KurzeKante)
            .OrderBy(x => x.Geschoss).ThenBy(x => Math.Round(x.Ax, 1)).ThenBy(x => Math.Round(x.Ay, 1)).ThenBy(x => Math.Round(x.Bx, 1)).ThenBy(x => Math.Round(x.By, 1)).ThenBy(x => x.Wand).ToList();
        var pk = Geordnet(projekt.Kanten);
        var dk = Geordnet(datei.Kanten);
        if (dk.Count == 0 || pk.Count == 0)
            return new EinpassErgebnis
            {
                Ergebnis = KeineMerkmale, RechenMs = uhr.ElapsedMilliseconds,
                Grund = dk.Count == 0 ? "Die Datei hat keine Wände und keine Räume (IfcWall, IfcSpace) mit auswertbarer Geometrie."
                    : "Das Projekt hat keine Wände und keine Räume (ohne die Elemente dieser Unterlage).",
            };

        // ---- Geschosse: je Geschoss oder alles zusammen
        var (gemeinsam, vergleich) = Vergleichsart(pk, dk, geschosse);
        int anzahlG = gemeinsam ? 1 : geschosse;
        // Herkunft der Proben (Geschoss der Datei) bleibt für den Bericht je Geschoss
        var pkG = pk.Select(k => gemeinsam ? k with { Geschoss = 0 } : k).ToList();
        var dkMitHerkunft = dk.Select(k => (Kante: gemeinsam ? k with { Geschoss = 0 } : k, Herkunft: k.Geschoss)).ToList();
        if (!gemeinsam)
        {
            var mitProjekt = pkG.Select(k => k.Geschoss).ToHashSet();
            dkMitHerkunft = dkMitHerkunft.Where(x => mitProjekt.Contains(x.Kante.Geschoss)).ToList();
        }

        var verzeichnis = new Verzeichnis(pkG);
        var proben = Proben(dkMitHerkunft, MaxProben);
        var grob = Stichprobe(proben, MaxProbenGrob);
        double cx = proben.Average(p => p.X), cy = proben.Average(p => p.Y);   // Schwerpunkt der Proben (Datei)

        // ---- 1. Hauptrichtungen → Drehungen
        var drehungen = Drehungen(pkG, dkMitHerkunft.Select(x => x.Kante).ToList());

        // ---- 2. Verschiebungen je Drehung (Eckpunkte), Vorauswahl mit der groben Stichprobe
        var pe = Ecken(pkG, anzahlG, MaxEcken);
        var de = Ecken(dkMitHerkunft.Select(x => x.Kante).ToList(), anzahlG, MaxEcken);
        // ---- 4a. jeden Kandidaten mit der groben Stichprobe verfeinern (die Stimmen liegen bei Raumkanten auf Wandflächen eine halbe
        // Wanddicke neben der Lage; von dort führt ICP nur aus dem richtigen Nachbarn zur Lage)
        var kandidaten = new List<Kandidat>();
        var klein = Stichprobe(grob, MaxProbenFeinsuche);
        foreach (var th in drehungen)
            foreach (var (tx0, ty0) in Verschiebungen(pe, de, th))
            {
                var (tx1, ty1) = Feinsuche(klein, verzeichnis, th, tx0, ty0);
                kandidaten.Add(BesserVerfeinert(grob, verzeichnis, th, tx1, ty1, 10, false));
            }
        kandidaten = kandidaten.OrderByDescending(k => k.Anteil).ToList();

        bool Anders(double t1, double x1, double y1, double t2, double x2, double y2, double mm, double grad)
        {
            if (Math.Abs(Winkel(t1 - t2)) > grad) return true;
            var (ax, ay) = Abbilden(cx, cy, t1, x1, y1); var (bx, by) = Abbilden(cx, cy, t2, x2, y2);
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) > mm;
        }

        // ---- 4b. die besten verschiedenen Lagen (mehr als 1 m oder 2° auseinander; nahe Kandidaten sind nach 4a schon in dieselbe Lage
        // gelaufen) mit allen Proben verfeinern und bewerten – auch die nächstbeste wird so voll verfeinert
        bool AndereLage(Kandidat a, Kandidat b) => Anders(a.Theta, a.Tx, a.Ty, b.Theta, b.Tx, b.Ty, AndereLageMm, AndereLageGrad);
        var auswahl = new List<Kandidat>();
        foreach (var k in kandidaten)
        {
            if (auswahl.Count >= Verfeinert) break;
            if (auswahl.All(a => AndereLage(a, k))) auswahl.Add(k);
        }
        var fein = auswahl.Select(k => BesserVerfeinert(proben, verzeichnis, k.Theta, k.Tx, k.Ty, 40, true)).OrderByDescending(k => k.Anteil).ToList();
        if (fein.Count == 0)
            return new EinpassErgebnis { Ergebnis = KeineMerkmale, Grund = "Keine Lage gefunden (keine Eckpunkte im gleichen Geschoss).", RechenMs = uhr.ElapsedMilliseconds, Vergleich = vergleich, Gemeinsam = gemeinsam };
        var sieger = fein[0];
        // ---- 4c. Feinlage des Siegers (robustes Mass, siehe Feinlage)
        var (fth, ftx, fty) = Feinlage(proben, verzeichnis, sieger.Theta, sieger.Tx, sieger.Ty, cx, cy, Math.Min(FeinlageAnteil, 0.9 * sieger.Anteil));
        sieger = sieger with { Theta = Winkel(fth), Tx = ftx, Ty = fty, Anteil = Bewerten(proben, verzeichnis, fth, ftx, fty).Anteil150 };

        // ---- 5. nächstbeste andere Lage (verfeinert oder aus der Vorauswahl)
        Kandidat? zweiter = fein.Skip(1).Concat(kandidaten).Where(k => AndereLage(sieger, k))
            .OrderByDescending(k => k.Anteil).ThenByDescending(k => k.Fein).FirstOrDefault();

        var b = Bewerten(proben, verzeichnis, sieger.Theta, sieger.Tx, sieger.Ty, mitWerten: true);
        var gruende = new List<string>();
        if (b.Anteil150 < MindestAnteil) gruende.Add($"nur {Prozent(b.Anteil150)} % der Kanten innert {Grenze:0} mm, verlangt {Prozent(MindestAnteil)} %");
        if (b.Treffer < MindestTreffer) gruende.Add($"nur {b.Treffer} Treffer, verlangt {MindestTreffer}");
        if (zweiter is not null && zweiter.Anteil > HoechstensZweiter * b.Anteil150)
            gruende.Add($"nächstbeste Lage {Prozent(zweiter.Anteil)} % gegen {Prozent(b.Anteil150)} % – mehr als {Prozent(HoechstensZweiter)} % der besten");

        // je Geschoss (Herkunft der Proben)
        var jeGeschoss = new List<EinpassGeschossWert>();
        foreach (var g in proben.Select((p, i) => (p.Herkunft, i)).GroupBy(x => x.Herkunft).OrderBy(x => x.Key))
        {
            var werte = g.Select(x => b.Werte![x.i]).Order().ToList();
            jeGeschoss.Add(new EinpassGeschossWert(g.Key, werte.Count, werte.Count(w => w <= Grenze) / (double)werte.Count, werte[werte.Count / 2]));
        }

        return new EinpassErgebnis
        {
            Ergebnis = gruende.Count == 0 ? Eindeutig : Unsicher,
            Grund = gruende.Count == 0 ? null : string.Join("; ", gruende),
            Drehung = sieger.Theta, Tx = sieger.Tx, Ty = sieger.Ty,
            Anteil50 = b.Anteil50, Anteil100 = b.Anteil100, Anteil150 = b.Anteil150, MedianMm = b.Median,
            Proben = proben.Count, Treffer = b.Treffer,
            Zweitbester = zweiter is null ? null : new EinpassLage(zweiter.Theta, zweiter.Tx, zweiter.Ty, zweiter.Anteil),
            Gemeinsam = gemeinsam, Vergleich = vergleich, JeGeschoss = jeGeschoss,
            Kandidaten = kandidaten.Count, RechenMs = uhr.ElapsedMilliseconds,
        };
    }

    static (double X, double Y) Abbilden(double x, double y, double theta, double tx, double ty)
    {
        double w = theta * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return (c * x - s * y + tx, s * x + c * y + ty);
    }

    static string Prozent(double a) => (a * 100).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Je Geschoss oder alles zusammen (siehe Kopf der Datei).</summary>
    static (bool Gemeinsam, string Text) Vergleichsart(List<EinpassKante> pk, List<EinpassKante> dk, int geschosse)
    {
        if (geschosse <= 0) return (true, "alle Geschosse zusammen (das Projekt hat keine Geschosse)");
        var nP = new int[geschosse]; var nD = new int[geschosse]; var lD = new double[geschosse];
        foreach (var k in pk) if (k.Geschoss >= 0 && k.Geschoss < geschosse) nP[k.Geschoss]++;
        foreach (var k in dk) if (k.Geschoss >= 0 && k.Geschoss < geschosse) { nD[k.Geschoss]++; lD[k.Geschoss] += k.Laenge; }
        double gesamt = dk.Sum(k => k.Laenge), brauchbar = 0;
        for (int i = 0; i < geschosse; i++) if (nP[i] >= MinKantenJeGeschoss && nD[i] >= MinKantenJeGeschoss) brauchbar += lD[i];
        if (gesamt <= 0 || brauchbar < 0.5 * gesamt)
            return (true, $"alle Geschosse zusammen (zu wenig Merkmale je Geschoss: {Prozent(gesamt > 0 ? brauchbar / gesamt : 0)} % der Kantenlänge der Datei in Geschossen mit je mindestens {MinKantenJeGeschoss} Kanten auf beiden Seiten)");
        return (false, "je Geschoss");
    }

    // ---- 1. Hauptrichtungen

    /// <summary>Spitzen des Richtungshistogramms modulo 90° (Winkel in Grad, Anteil der Kantenlänge innert ±1°), stärkste zuerst, höchstens drei.</summary>
    public static List<(double Winkel, double Anteil)> Spitzen(IReadOnlyList<EinpassKante> kanten)
    {
        const int n = 900;
        var h = new double[n];
        var richtungen = new List<(double A, double W)>(kanten.Count);
        double gesamt = 0;
        foreach (var k in kanten)
        {
            double l = k.Laenge;
            if (l <= 0) continue;
            double a = Math.Atan2(k.By - k.Ay, k.Bx - k.Ax) * 180 / Math.PI;
            a = ((a % 90) + 90) % 90;
            h[Math.Min(n - 1, (int)(a * 10))] += l;
            richtungen.Add((a, l));
            gesamt += l;
        }
        var aus = new List<(double, double)>();
        if (gesamt <= 0) return aus;
        // glätten: Gauss σ = 0.2°, zyklisch
        var g = new double[n];
        for (int i = 0; i < n; i++)
            for (int j = -6; j <= 6; j++) g[i] += h[(i + j + n) % n] * Math.Exp(-j * j / 8.0);
        var frei = Enumerable.Repeat(true, n).ToArray();
        for (int s = 0; s < 3; s++)
        {
            int best = -1;
            for (int i = 0; i < n; i++) if (frei[i] && (best < 0 || g[i] > g[best])) best = i;
            if (best < 0 || g[best] <= 0) break;
            double spitze = (best + 0.5) / 10, sw = 0, sa = 0;
            foreach (var (a, w) in richtungen)
            {
                double d = a - spitze;
                d -= 90 * Math.Round(d / 90);
                if (Math.Abs(d) <= 1) { sw += w; sa += w * d; }
            }
            for (int j = -30; j <= 30; j++) frei[(best + j + n) % n] = false;   // nächste Spitze mindestens 3° daneben
            if (sw <= 0 || sw < 0.05 * gesamt && aus.Count > 0) continue;
            double mittel = spitze + sa / sw;
            mittel = ((mittel % 90) + 90) % 90;
            aus.Add((mittel, sw / gesamt));
        }
        return aus;
    }

    /// <summary>Kandidaten der Drehung θ = αProjekt − αDatei + k·90° (Grad).</summary>
    static List<double> Drehungen(List<EinpassKante> pk, List<EinpassKante> dk)
    {
        var sp = Spitzen(pk); var sd = Spitzen(dk);
        bool klar = sp.Count > 0 && sd.Count > 0 && sp[0].Anteil >= KlareSpitze && sd[0].Anteil >= KlareSpitze;
        List<double> Wahl(List<(double Winkel, double Anteil)> s) =>
            s.Count == 0 ? [0] : klar ? s.Where((x, i) => i == 0 || x.Anteil >= 0.5 * s[0].Anteil).Select(x => x.Winkel).ToList() : s.Select(x => x.Winkel).ToList();
        var aus = new List<double>();
        foreach (var a in Wahl(sp))
            foreach (var b in Wahl(sd))
                for (int k = 0; k < 4; k++)
                {
                    double th = Winkel(a - b + 90 * k);
                    if (aus.All(x => Math.Abs(Winkel(x - th)) > 0.05)) aus.Add(th);
                }
        return aus;
    }

    // ---- 2. Verschiebungen

    /// <summary>Eckpunkte (Kantenenden) je Geschoss, auf 10 mm zusammengefasst, höchstens max insgesamt (gleichmässige, feste Stichprobe).</summary>
    static List<Ecke>[] Ecken(List<EinpassKante> kanten, int anzahlG, int max)
    {
        var aus = Enumerable.Range(0, anzahlG).Select(_ => new List<Ecke>()).ToArray();
        var gesehen = new HashSet<(int, long, long)>();
        foreach (var k in kanten)
        {
            int g = Math.Clamp(k.Geschoss, 0, anzahlG - 1);
            foreach (var (x, y) in new[] { (k.Ax, k.Ay), (k.Bx, k.By) })
                if (gesehen.Add((g, (long)Math.Round(x / 10), (long)Math.Round(y / 10)))) aus[g].Add(new Ecke(x, y));
        }
        int gesamt = aus.Sum(l => l.Count);
        if (gesamt > max)
        {
            int schritt = (int)Math.Ceiling(gesamt / (double)max);
            for (int g = 0; g < anzahlG; g++) aus[g] = aus[g].Where((_, i) => i % schritt == 0).ToList();
        }
        return aus;
    }

    /// <summary>Die besten Verschiebungen für eine Drehung: Stimmen t = p − R(θ)·q je Geschoss im Raster 100 mm, Summe über 3 × 3
    /// Zellen, gewichtete Mitte; höchstens fünf, mindestens 500 mm auseinander.</summary>
    static List<(double Tx, double Ty)> Verschiebungen(List<Ecke>[] pe, List<Ecke>[] de, double theta)
    {
        double w = theta * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        var stimmen = new Dictionary<long, int>();
        static long Schluessel(long x, long y) => (x << 32) ^ (y & 0xffffffffL);
        for (int g = 0; g < pe.Length; g++)
            foreach (var q in de[g])
            {
                double rx = c * q.X - s * q.Y, ry = s * q.X + c * q.Y;
                foreach (var p in pe[g])
                {
                    long ix = (long)Math.Floor((p.X - rx) / Raster), iy = (long)Math.Floor((p.Y - ry) / Raster);
                    CollectionsMarshal.GetValueRefOrAddDefault(stimmen, Schluessel(ix, iy), out _)++;
                }
            }
        if (stimmen.Count == 0) return [];
        // die stärksten Zellen (Rohzahl), dann 3 × 3 summiert
        var spitzen = new PriorityQueue<long, int>();
        foreach (var (key, n) in stimmen)
        {
            if (spitzen.Count < 300) spitzen.Enqueue(key, n);
            else if (spitzen.TryPeek(out _, out var kleinste) && n > kleinste) { spitzen.Dequeue(); spitzen.Enqueue(key, n); }
        }
        var summen = new List<(long X, long Y, int Summe, double Mx, double My)>();
        while (spitzen.TryDequeue(out var key, out _))
        {
            long ix = key >> 32, iy = (int)(key & 0xffffffffL);
            int summe = 0; double mx = 0, my = 0;
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    if (stimmen.TryGetValue(Schluessel(ix + dx, iy + dy), out var n))
                    {
                        summe += n; mx += n * (ix + dx + 0.5) * Raster; my += n * (iy + dy + 0.5) * Raster;
                    }
            summen.Add((ix, iy, summe, mx / summe, my / summe));
        }
        var aus = new List<(long X, long Y, double Mx, double My)>();
        foreach (var z in summen.OrderByDescending(z => z.Summe).ThenBy(z => z.X).ThenBy(z => z.Y))
        {
            if (aus.Count >= BesteJeDrehung) break;
            if (aus.Any(a => Math.Abs(a.X - z.X) <= 5 && Math.Abs(a.Y - z.Y) <= 5)) continue;
            aus.Add((z.X, z.Y, z.Mx, z.My));
        }
        return aus.Select(a => (a.Mx, a.My)).ToList();
    }

    // ---- 3. Bewertung

    /// <summary>Proben alle 200 mm auf den Kanten der Datei (Mitte der Teilstücke), höchstens max (feste Stichprobe).</summary>
    static List<Probe> Proben(List<(EinpassKante Kante, int Herkunft)> kanten, int max)
    {
        var aus = new List<Probe>();
        foreach (var (k, h) in kanten)
        {
            double l = k.Laenge;
            int n = Math.Max(1, (int)Math.Round(l / Probenabstand));
            for (int i = 0; i < n; i++)
            {
                double f = (i + 0.5) / n;
                aus.Add(new Probe(k.Ax + f * (k.Bx - k.Ax), k.Ay + f * (k.By - k.Ay), k.Geschoss, h));
            }
        }
        return Stichprobe(aus, max);
    }

    static List<T> Stichprobe<T>(List<T> liste, int max)
    {
        if (liste.Count <= max) return liste;
        double schritt = liste.Count / (double)max;
        var aus = new List<T>(max);
        for (int i = 0; i < max; i++) aus.Add(liste[(int)(i * schritt)]);
        return aus;
    }

    sealed record Bewertung(double Anteil50, double Anteil100, double Anteil150, double Median, int Treffer, double[]? Werte);

    static Bewertung Bewerten(List<Probe> proben, Verzeichnis v, double theta, double tx, double ty, bool mitWerten = false)
    {
        double w = theta * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        var werte = new double[proben.Count];
        int a50 = 0, a100 = 0, a150 = 0;
        for (int i = 0; i < proben.Count; i++)
        {
            var p = proben[i];
            double x = c * p.X - s * p.Y + tx, y = s * p.X + c * p.Y + ty;
            double d = v.Naechster(p.G, x, y, Verzeichnis.Reichweite, out _, out _) ?? Verzeichnis.Reichweite;
            werte[i] = d;
            if (d <= 50) a50++;
            if (d <= 100) a100++;
            if (d <= Grenze) a150++;
        }
        int n = Math.Max(1, proben.Count);
        double median = 0;
        if (mitWerten && proben.Count > 0) { var sortiert = (double[])werte.Clone(); Array.Sort(sortiert); median = sortiert[sortiert.Length / 2]; }
        return new Bewertung(a50 / (double)n, a100 / (double)n, a150 / (double)n, median, a150, mitWerten ? werte : null);
    }

    // ---- 4. Verfeinern

    /// <summary>Lokale Feinsuche vor dem ICP: Verschiebungen im Raster 50 mm innert ±300 mm um den Kandidaten; es gewinnt die mit den
    /// meisten Proben innert 150 mm, bei Gleichstand die kleinste Summe der auf 150 mm begrenzten Abstände, dann die nächste zur Mitte.
    /// Abweichung von der Spezifikation: Die Mitte der Stimmen liegt bei Raumkanten auf Wandflächen bis über eine halbe Wanddicke neben
    /// der Lage (Ecken der Räume gegen Enden der Wandflächen); Punkt–Punkt-ICP springt von dort auf die andere Fläche derselben Wand
    /// (synthetischer Test, Dicke 200 mm: +200 mm daneben). Die Summe allein zöge eine halb genaue Lage einer durchgehend 100 mm
    /// entfernten vor (Wandachsen ohne Dicke) – darum zuerst die Treffer.</summary>
    static (double Tx, double Ty) Feinsuche(List<Probe> proben, Verzeichnis v, double theta, double tx, double ty)
    {
        double w = theta * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        int bestTreffer = -1; double bestSumme = double.MaxValue, bx = tx, by = ty;
        foreach (var (i, j) in FeinsucheSchritte)
        {
            double x0 = tx + i * 50, y0 = ty + j * 50, summe = 0; int treffer = 0, rest = proben.Count;
            foreach (var p in proben)
            {
                var d = v.Naechster(p.G, c * p.X - s * p.Y + x0, s * p.X + c * p.Y + y0, Grenze, out _, out _);
                if (d is double dd) { treffer++; summe += dd; } else summe += Grenze;
                if (treffer + --rest < bestTreffer) break;   // kann nicht mehr gewinnen
            }
            if (treffer > bestTreffer || treffer == bestTreffer && summe < bestSumme - 1e-6) { bestTreffer = treffer; bestSumme = summe; bx = x0; by = y0; }
        }
        return (bx, by);
    }

    /// <summary>ICP ab einer Lage und Bewertung des Ergebnisses. Bewusst ohne Vorbehalt «nur wenn der Anteil nicht sinkt»: Wände der Datei
    /// gegen Raumkanten des Projekts erreichen eine halbe Wanddicke neben der Lage mehr Proben innert 150 mm (die Aussenseiten der
    /// Aussenwände rücken in die Grenze; synthetischer Test 85 % statt 71 %) – dort führt erst das ICP zur Lage.</summary>
    static Kandidat BesserVerfeinert(List<Probe> proben, Verzeichnis v, double theta, double tx, double ty, int nachlauf, bool fein)
    {
        var (t, x, y) = Verfeinern(proben, v, theta, tx, ty, nachlauf);
        return new Kandidat(Winkel(t), x, y, Bewerten(proben, v, t, x, y).Anteil150, fein);
    }

    /// <summary>Feinlage: kleinstes Mittel der kleineren Abstände (Anteil anteil der Proben, höchstens 70 %; gestutzte Quadrate wären das
    /// Mass des ICP), Musterverfahren mit Drehung um den Schwerpunkt der Proben (Drehung und Verschiebung entkoppelt), Schritte 0.02° /
    /// 20 mm bis 0.0005° / 0.5 mm. Abweichung von der Spezifikation, getragen vom Echt-Test: Raumkanten aus Revit liegen nur ungefähr auf
    /// den Wandflächen des Architekten (Median rund 20 mm); das ICP mit 60 mm Grenze fand dort eine um 0.11° gedrehte, 120 mm
    /// verschobene Lage mit gleichem Anteil innert 150 mm (87 %). Das Mittel der besseren 50 bzw. 70 % hat über die Drehung ein einziges,
    /// deutliches Minimum, das mit der Lage der Wände aus derselben Revit-Datei (Median 1 mm) übereinstimmt (Bernstrasse 42: 42.68°
    /// gegen 42.675°). 70 % statt 50 %: bei gleichmässigem Rauschen (±20 mm) ist die bessere Hälfte allein zu wenig genau (0.017°).</summary>
    static (double Theta, double Tx, double Ty) Feinlage(List<Probe> proben, Verzeichnis v, double theta, double tx, double ty, double cx, double cy, double anteil)
    {
        var werte = new double[proben.Count];
        int haelfte = Math.Max(1, (int)(proben.Count * Math.Clamp(anteil, 0.3, FeinlageAnteil)));
        double Wert(double th, double mx, double my)
        {
            double w = th * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
            double x0 = mx - (c * cx - s * cy), y0 = my - (s * cx + c * cy);
            for (int i = 0; i < proben.Count; i++)
            {
                var p = proben[i];
                werte[i] = v.Naechster(p.G, c * p.X - s * p.Y + x0, s * p.X + c * p.Y + y0, Verzeichnis.Reichweite, out _, out _) ?? Verzeichnis.Reichweite;
            }
            Array.Sort(werte);
            double summe = 0;
            for (int i = 0; i < haelfte; i++) summe += werte[i];
            return summe / haelfte;
        }
        var (mx, my) = Abbilden(cx, cy, theta, tx, ty);
        double best = Wert(theta, mx, my), sth = 0.02, sm = 20;
        for (int n = 0; n < 500 && sth >= 0.0005; n++)
        {
            bool besser = false;
            foreach (var (a, b, c) in new[] { (sth, 0.0, 0.0), (-sth, 0.0, 0.0), (0.0, sm, 0.0), (0.0, -sm, 0.0), (0.0, 0.0, sm), (0.0, 0.0, -sm) })
            {
                double wert = Wert(theta + a, mx + b, my + c);
                if (wert < best - 1e-9) { best = wert; theta += a; mx += b; my += c; besser = true; break; }
            }
            if (!besser) { sth /= 2; sm /= 2; }
        }
        double wn = theta * Math.PI / 180, cn = Math.Cos(wn), sn = Math.Sin(wn);
        return (theta, mx - (cn * cx - sn * cy), my - (sn * cx + cn * cy));
    }

    static readonly (int I, int J)[] FeinsucheSchritte =
        Enumerable.Range(-6, 13).SelectMany(i => Enumerable.Range(-6, 13).Select(j => (i, j))).OrderBy(x => x.i * x.i + x.j * x.j).ThenBy(x => x.i).ThenBy(x => x.j).ToArray();

    /// <summary>ICP Punkt–Strecke: je Schritt die nächsten Punkte auf den Projektkanten innert der Grenze, dann 2D-Kabsch (Drehung und
    /// Verschiebung, die die Quadratsumme der Abstände klein macht); danach mit 60 mm bis zur Ruhe.</summary>
    static (double Theta, double Tx, double Ty) Verfeinern(List<Probe> proben, Verzeichnis v, double theta, double tx, double ty, int nachlauf = 40)
    {
        bool Schritt(double grenze, ref double th, ref double x0, ref double y0)
        {
            double w = th * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
            double sqx = 0, sqy = 0, spx = 0, spy = 0; int n = 0;
            var paare = new List<(double Qx, double Qy, double Px, double Py)>();
            foreach (var p in proben)
            {
                double x = c * p.X - s * p.Y + x0, y = s * p.X + c * p.Y + y0;
                if (v.Naechster(p.G, x, y, grenze, out var px, out var py) is null) continue;
                paare.Add((p.X, p.Y, px, py));
                sqx += p.X; sqy += p.Y; spx += px; spy += py; n++;
            }
            if (n < 3) return false;
            sqx /= n; sqy /= n; spx /= n; spy /= n;
            double sxx = 0, sxy = 0;
            foreach (var (qx, qy, px, py) in paare)
            {
                double ax = qx - sqx, ay = qy - sqy, bx = px - spx, by = py - spy;
                sxx += ax * bx + ay * by;
                sxy += ax * by - ay * bx;
            }
            double neu = Math.Atan2(sxy, sxx);
            double cn = Math.Cos(neu), sn = Math.Sin(neu);
            th = neu * 180 / Math.PI;
            x0 = spx - (cn * sqx - sn * sqy);
            y0 = spy - (sn * sqx + cn * sqy);
            return true;
        }
        foreach (var g in IcpGrenzen)
            if (!Schritt(g, ref theta, ref tx, ref ty)) return (theta, tx, ty);
        for (int i = 0; i < nachlauf; i++)
        {
            double t0 = theta, x0 = tx, y0 = ty;
            if (!Schritt(IcpGrenzen[^1], ref theta, ref tx, ref ty)) break;
            if (Math.Abs(Winkel(theta - t0)) < 1e-6 && Math.Abs(tx - x0) < 0.01 && Math.Abs(ty - y0) < 0.01) break;
        }
        return (theta, tx, ty);
    }

    /// <summary>Rasterverzeichnis der Projektkanten je Geschoss (Zellen 1 m). Jede Kante steht in den Zellen ihrer Punkte im Abstand
    /// von höchstens 500 mm; gesucht wird in 3 × 3 Zellen – damit ist jeder Abstand bis 750 mm genau.</summary>
    sealed class Verzeichnis
    {
        const double Zelle = 1000;
        public const double Reichweite = 750;
        readonly List<EinpassKante> kanten;
        readonly Dictionary<(int G, long X, long Y), List<int>> zellen = [];

        public Verzeichnis(List<EinpassKante> kanten)
        {
            this.kanten = kanten;
            for (int i = 0; i < kanten.Count; i++)
            {
                var k = kanten[i];
                int n = Math.Max(1, (int)Math.Ceiling(k.Laenge / 500));
                (long, long)? zuletzt = null;
                for (int j = 0; j <= n; j++)
                {
                    double f = j / (double)n;
                    var z = ((long)Math.Floor((k.Ax + f * (k.Bx - k.Ax)) / Zelle), (long)Math.Floor((k.Ay + f * (k.By - k.Ay)) / Zelle));
                    if (z == zuletzt) continue;
                    zuletzt = z;
                    var key = (k.Geschoss, z.Item1, z.Item2);
                    if (!zellen.TryGetValue(key, out var l)) zellen[key] = l = [];
                    if (l.Count == 0 || l[^1] != i) l.Add(i);
                }
            }
        }

        /// <summary>Abstand zur nächsten Kante im Geschoss g und der nächste Punkt darauf, oder null, wenn keine innert grenze.</summary>
        public double? Naechster(int g, double x, double y, double grenze, out double px, out double py)
        {
            px = py = 0;
            long cx = (long)Math.Floor(x / Zelle), cy = (long)Math.Floor(y / Zelle);
            double best = grenze * grenze; bool gefunden = false;
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (!zellen.TryGetValue((g, cx + dx, cy + dy), out var liste)) continue;
                    foreach (var i in liste)
                    {
                        var k = kanten[i];
                        double vx = k.Bx - k.Ax, vy = k.By - k.Ay, ll = vx * vx + vy * vy;
                        double t = ll <= 0 ? 0 : Math.Clamp(((x - k.Ax) * vx + (y - k.Ay) * vy) / ll, 0, 1);
                        double qx = k.Ax + t * vx, qy = k.Ay + t * vy, d2 = (x - qx) * (x - qx) + (y - qy) * (y - qy);
                        if (d2 <= best) { best = d2; px = qx; py = qy; gefunden = true; }
                    }
                }
            return gefunden ? Math.Sqrt(best) : null;
        }
    }

    // ------------------------------------------------------------------ Merkmale

    /// <summary>Geschosse des Projekts (Kennzeichen, Kote), aufsteigend.</summary>
    public static List<EinpassGeschoss> Geschosse(Modell m) =>
        m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is double).Select(g => new EinpassGeschoss(g.Kennzeichen, g.Zahl("kote")!.Value))
            .OrderBy(g => g.KoteMm).ThenBy(g => g.Name, StringComparer.Ordinal).ToList();

    /// <summary>Merkmale des Projekts: Wände (Achse und Dicke → zwei Wandflächen) und Räume (Kanten des Polygons), ohne die Elemente der
    /// Unterlage selbst (Kennung quelle = Id der Unterlage, wie ModellBegrenzung).</summary>
    public static EinpassMerkmale ProjektMerkmale(Modell m, Katalog k, string? ohneUnterlage, IReadOnlyList<EinpassGeschoss> geschosse)
    {
        var aus = new EinpassMerkmale();
        foreach (var e in m.Elemente.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (e.Klasse == "Unterlage" || ohneUnterlage is not null && e.Kennungen?.GetValueOrDefault("quelle") == ohneUnterlage) continue;
            if (k.IstArt(e, "Wand"))
            {
                if (Wandgeometrie.Lage(m, e) is { } w) aus.Wand(w.Ax, w.Ay, w.Bx, w.By, w.Dicke, GeschossIndex(geschosse, w.Unterkante));
            }
            else if (k.IstArt(e, "Raum") && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 } g)
            {
                double z = g.Punkte[0].Length > 2 ? g.Punkte[0][2] : m.Finde(e.Geschoss)?.Zahl("kote") ?? 0;
                aus.Raum(g.Punkte, GeschossIndex(geschosse, z));
            }
        }
        return aus;
    }

    /// <summary>Merkmale der Datei in der Lage «Gebäudeursprung»: Wände und Räume (IfcSpace) mit denselben Übertragern wie beim
    /// Übernehmen (IfcArchitekturLauf.NurWaende, IfcRaeumeLauf.NurIfcSpace), ohne Seiteneffekt auf das Projekt: der Lauf arbeitet auf
    /// einem leeren Modell mit eigenen Geschoss-Kennungen (kein Setzen, keine Kennzeichen, keine Geschosse). Bezug und Zähler der
    /// Geometrie (nicht ausgewertete Entitäten, vereinfachte Körper) werden danach zurückgesetzt.</summary>
    public static EinpassMerkmale DateiMerkmale(IfcDatei d, Projekt p, IReadOnlyList<EinpassGeschoss> geschosse)
    {
        var aus = new EinpassMerkmale();
        var alt = d.Bezug;
        var geo = IfcGeometrie.Von(d);
        var zaehler = geo.ZaehlerSichern();
        d.Bezug = d.BezugVorschlag();
        try
        {
            var kontext = new Kontext(p, new Modell(), new Herkunft { Art = "berechnung", Befehl = "einpassen", Zeit = DateTimeOffset.Now });
            var ids = d.Geschosse().ToDictionary(x => x, x => "einpassen:" + x.GlobalId);
            var lauf = new IfcLauf
            {
                Datei = d, Kontext = kontext, UnterlageId = "einpassen", Quelle = Path.GetFileName(d.Pfad),
                Herkunft = kontext.Herkunft, GeschossIds = ids,
            };
            var waende = d.Modell.Instances.OfType<IIfcWall>().OrderBy(x => x.EntityLabel).ToList();
            var raeume = d.Modell.Instances.OfType<IIfcSpace>().OrderBy(x => x.EntityLabel).ToList();
            var wAuswahl = Stichprobe(waende, MaxWaende).Select(x => x.EntityLabel).ToHashSet();
            var rAuswahl = Stichprobe(raeume, MaxRaeume);
            if (wAuswahl.Count > 0) new IfcArchitekturLauf(lauf).NurWaende(w => wAuswahl.Contains(w.EntityLabel));
            if (rAuswahl.Count > 0) new IfcRaeumeLauf(lauf).NurIfcSpace(rAuswahl);
            foreach (var e in lauf.Geplant.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                if (e.Klasse == "Wand" && e.Geometrie is { Punkte.Count: >= 2 } gw)
                {
                    var a = gw.Punkte[0]; var b = gw.Punkte[^1];
                    aus.Wand(a[0], a[1], b[0], b[1], gw.Dicke ?? 0, GeschossIndex(geschosse, Math.Min(a.Length > 2 ? a[2] : 0, b.Length > 2 ? b[2] : 0)));
                }
                else if (e.Klasse == "Raum" && e.Geometrie is { Art: "polygon", Punkte.Count: >= 3 } gr)
                    aus.Raum(gr.Punkte, GeschossIndex(geschosse, gr.Punkte[0].Length > 2 ? gr.Punkte[0][2] : 0));
            }
            var teile = new List<string>();
            if (waende.Count > MaxWaende) teile.Add($"{wAuswahl.Count} von {waende.Count} Wänden");
            if (raeume.Count > MaxRaeume) teile.Add($"{rAuswahl.Count} von {raeume.Count} Räumen");
            if (teile.Count > 0) aus.Hinweis = "Stichprobe: " + string.Join(", ", teile) + " gerechnet";
        }
        finally
        {
            d.Bezug = alt;
            geo.ZaehlerZuruecksetzen(zaehler);
        }
        return aus;
    }

    // ------------------------------------------------------------------ Einpassen einer Datei

    /// <summary>Einpassen einer Datei: Merkmale beider Seiten, Rechnung, Bezug, Gegenprobe (Überdeckung) und Lagebericht. Bezug = null bei
    /// «keine Merkmale».</summary>
    public static IfcEinpassAusgang Ausfuehren(IfcDatei d, IfcEinpassQuelle q, double[]? bestehend)
    {
        var uhr = Stopwatch.StartNew();
        var geschosse = Geschosse(q.Modell);
        var pm = ProjektMerkmale(q.Modell, q.Projekt.Katalog, q.OhneUnterlage, geschosse);
        var dm = DateiMerkmale(d, q.Projekt, geschosse);
        var e = Rechnen(pm, dm, geschosse.Count);
        IfcBezug? bezug = e.Ergebnis == KeineMerkmale ? null : Bezug(d.BezugVorschlag(), e.Drehung, e.Tx, e.Ty);
        double? ueberdeckung = bezug is not null && bestehend is not null ? Json.Zahl(IfcAusrichtung.Inhalt(d, bezug, bestehend)?["ueberdeckung"]) : null;
        var bericht = Bericht(e, pm, dm, geschosse, bezug, ueberdeckung, uhr.ElapsedMilliseconds);
        return new IfcEinpassAusgang(e, bericht, bezug, ueberdeckung, bericht["text"]!.GetValue<string>());
    }

    /// <summary>Lagebericht «lage.einpassen» (Feldnamen fest, siehe entwurf/ifc-einpassen.md; Felder dürfen dazukommen).</summary>
    public static JsonObject Bericht(EinpassErgebnis e, EinpassMerkmale projekt, EinpassMerkmale datei, IReadOnlyList<EinpassGeschoss> geschosse,
        IfcBezug? bezug, double? ueberdeckung, long dauerMs)
    {
        static JsonObject Zaehler(EinpassMerkmale m) => new()
        {
            ["waende"] = m.Waende, ["raeume"] = m.Raeume, ["kanten"] = m.Kanten.Count, ["wandflaechen"] = m.Wandflaechen, ["raumkanten"] = m.Raumkanten,
        };
        static JsonArray Mm(double x, double y) => new(Math.Round(x, 1), Math.Round(y, 1));
        var o = new JsonObject { ["ergebnis"] = e.Ergebnis };
        if (e.Grund is not null) o["grund"] = e.Grund;
        if (bezug is not null)
        {
            o["bezug"] = bezug.AlsJson();
            o["drehung"] = Math.Round(e.Drehung, 4);
            o["verschiebungMm"] = Mm(e.Tx, e.Ty);
        }
        var mk = new JsonObject { ["datei"] = Zaehler(datei), ["projekt"] = Zaehler(projekt) };
        if (datei.Hinweis is not null) mk["hinweis"] = datei.Hinweis;
        o["merkmale"] = mk;
        o["paarung"] = Paarung(datei, projekt);
        o["vergleich"] = e.Vergleich;
        var gs = new JsonArray();
        foreach (var g in e.JeGeschoss)
            gs.Add(new JsonObject
            {
                ["geschoss"] = g.Geschoss >= 0 && g.Geschoss < geschosse.Count ? geschosse[g.Geschoss].Name : "ohne Geschoss",
                ["kote"] = g.Geschoss >= 0 && g.Geschoss < geschosse.Count ? Math.Round(geschosse[g.Geschoss].KoteMm / 1000, 3) : null,   // m, wie ifc_pruefen.geschosse[].kote
                ["proben"] = g.Proben, ["anteil150"] = Math.Round(g.Anteil150, 3), ["medianMm"] = Math.Round(g.MedianMm, 1),
            });
        o["geschosse"] = gs;
        if (e.Ergebnis != KeineMerkmale)
        {
            o["anteil"] = new JsonObject { ["mm50"] = Math.Round(e.Anteil50, 3), ["mm100"] = Math.Round(e.Anteil100, 3), ["mm150"] = Math.Round(e.Anteil150, 3) };
            o["medianMm"] = Math.Round(e.MedianMm, 1);
            o["proben"] = e.Proben;
            o["treffer"] = e.Treffer;
            o["zweitbester"] = e.Zweitbester is { } z
                ? new JsonObject { ["drehung"] = Math.Round(z.Drehung, 2), ["verschiebungMm"] = Mm(z.Tx, z.Ty), ["anteil150"] = Math.Round(z.Anteil150, 3) }
                : null;
            o["kandidaten"] = e.Kandidaten;
        }
        o["ueberdeckung"] = ueberdeckung is double u ? Math.Round(u, 2) : null;
        o["dauerMs"] = dauerMs;
        o["text"] = Text(e, datei, projekt, bezug, ueberdeckung);
        return o;
    }

    static string Paarung(EinpassMerkmale datei, EinpassMerkmale projekt)
    {
        static string Arten(EinpassMerkmale m) => string.Join(" und ", new[] { m.Wandflaechen > 0 ? "Wandflächen" : null, m.Raumkanten > 0 ? "Raumkanten" : null }.OfType<string>());
        var d = Arten(datei); var p = Arten(projekt);
        return d.Length == 0 || p.Length == 0 ? "keine" : $"{d} der Datei auf {p} des Projekts";
    }

    static string Merkmaltext(EinpassMerkmale m, string seite, bool alsKanten)
    {
        var teile = alsKanten
            ? new[] { m.Wandflaechen > 0 ? $"{m.Wandflaechen} Wandflächen" : null, m.Raumkanten > 0 ? $"{m.Raumkanten} Raumkanten" : null }
            : new[] { m.Waende > 0 ? $"{m.Waende} Wände" : null, m.Raeume > 0 ? $"{m.Raeume} Räume" : null };
        var t = string.Join(" und ", teile.OfType<string>());
        return t.Length == 0 ? $"keine Merkmale {seite}" : $"{t} {seite}";
    }

    static string Text(EinpassErgebnis e, EinpassMerkmale datei, EinpassMerkmale projekt, IfcBezug? bezug, double? ueberdeckung)
    {
        if (e.Ergebnis == KeineMerkmale) return $"Einpassen nicht möglich: {e.Grund}";
        string merkmale = $"{Merkmaltext(datei, "der Datei", true)} auf {Merkmaltext(projekt, "des Projekts", false)}";
        string lage = $"Drehung {e.Drehung:0.000}°, Verschiebung {e.Tx / 1000:0.000} / {e.Ty / 1000:0.000} m";
        string median = e.MedianMm >= Verzeichnis.Reichweite ? $"Median über {Verzeichnis.Reichweite:0} mm" : $"Median {e.MedianMm:0} mm";
        string treffer = $"{Prozent(e.Anteil150)} % der Kanten innert {Grenze:0} mm ({median})";
        string zweiter = e.Zweitbester is { } z ? $"nächstbeste Lage {Prozent(z.Anteil150)} %" : "keine andere Lage";
        string gegen = ueberdeckung is double u ? $" Gegenprobe: {Prozent(u)} % der Bauteile im bestehenden Modell." : "";
        string vergleich = e.Gemeinsam ? $" Verglichen: {e.Vergleich}." : "";
        if (e.Ergebnis == Eindeutig)
            return $"Eingepasst über {merkmale}: {lage}; {treffer}, {zweiter}. Bezug {IfcAusrichtung.Text(bezug!)}.{gegen}{vergleich}";
        return $"Einpassen nicht eindeutig ({e.Grund}). Beste Lage: {lage}; {treffer}, {zweiter}" +
               (e.Zweitbester is { } z2 ? $" (Drehung {z2.Drehung:0.000}°)" : "") + $". Merkmale: {merkmale}.{gegen}{vergleich}";
    }
}

/// <summary>Projektseite und Art des Einpassens für IfcAusrichtung.Bestimmen. Modus: wahl = nur bei ausrichtung = einpassen
/// (ifc_uebernehmen) | pruefen = auch, wenn keine andere Wahl das bestehende Modell trifft (ifc_pruefen) | immer (ifc_pruefen mit
/// einpassen = true).</summary>
public sealed record IfcEinpassQuelle(Projekt Projekt, Modell Modell, string? OhneUnterlage, string Modus = IfcEinpassQuelle.NurWahl)
{
    public const string NurWahl = "wahl";
    public const string Pruefen = "pruefen";
    public const string Immer = "immer";
}

/// <summary>Ergebnis des Einpassens einer Datei: Rechnung, Lagebericht (JSON), Bezug (null bei «keine Merkmale»), Überdeckung, Text.</summary>
public sealed record IfcEinpassAusgang(EinpassErgebnis Ergebnis, JsonObject Bericht, IfcBezug? Bezug, double? Ueberdeckung, string Text)
{
    public bool IstEindeutig => Ergebnis.Ergebnis == IfcEinpassen.Eindeutig;
}
