// Darstellung «Hülle» im Grundriss (Modul 1c, Auftrag C1b; Spezifikation entwurf/modul-1c.md, Abschnitt «Oberfläche»).
// Eine Grafikquelle ohne eigene Elemente: Ist die Ansicht ein Grundriss mit darstellung = huelle, zeichnet sie aus den
// Raumgrenzen des Geschosses (Raumgrenzen.JeGeschoss, nur gelesen) einen kräftigen Streifen auf der Raumseite jeder Wand-,
// Fenster- und Türkante, gefärbt nach Angrenzung (aussen, erdreich, unbeheizt, beheizt). Böden gegen Erdreich oder aussen
// erscheinen als dezente Fläche im Raum. Alles nur am Bildschirm (Stil.NurBildschirm), nicht im Plan-PDF; Ebene «huelle»
// (über den Räumen, unter den Wänden). Keine Normwerte, keine Rechenregeln: Angrenzung und Geometrie liefert der Rechenkern.
//
// Entscheide dieses Auftrags (alle Annahmen, Farben ohne Quelle):
// - Streifen 2 Papier-mm breit, die Streifenmitte 1 Papier-mm im Raum: er liegt damit an der Wandfläche an und wächst mit dem
//   Massstab nicht in die Wand hinein. An Ecken enden die Streifen stumpf an Von/Bis.
// - Beheizt (Innenwand zwischen beheizten Räumen) nur dezent: 1 Papier-mm, Deckkraft 0.4. Die Hülle soll auffallen; Innenwände
//   sind die Mehrheit der Raumgrenzen und würden sie sonst übertönen.
// - Fenster, Türen und offene Kantenstücke (Raumgrenze ohne Wand): derselbe Streifen heller (Deckkraft 0.45) mit gestrichelter
//   Kontur. Der Wandstreifen spart die Öffnungen aus, damit nichts übereinanderliegt.
// - Liegen mehrere Streifen auf demselben Kantenstück (Wand teils unter Terrain), gewinnt der später gezeichnete:
//   aussen vor erdreich vor unbeheizt vor beheizt.
// - Nur Bildschirm: Der Plan (PDF) bleibt ein Plan; die Hülle ist eine Prüfansicht. Vorschlag, im Bericht genannt.

namespace Hlks.Kern;

public sealed class HuelleGrafik : IGrafikquelle
{
    public string Name => "huelle";

    // Annahme Farbe: Die Farben sind frei gewählt (keine Normvorgabe). Sie gelten für Grundriss und 3D und stehen in
    // wwwroot/js/ansichten/huelle-darstellung.js als Spiegel (der Node-Test vergleicht beide).
    public const string FarbeAussen = "#e0482f";      // Annahme Farbe: rotorange (Wärme geht nach draussen)
    public const string FarbeErdreich = "#8a6a3b";    // Annahme Farbe: braun
    public const string FarbeUnbeheizt = "#3f7fd1";   // Annahme Farbe: blau
    public const string FarbeBeheizt = "#5aa469";     // Annahme Farbe: grün, nur dezent gezeichnet

    /// <summary>Breite des Streifens in Papier-mm (Annahme).</summary>
    public const double StreifenBreite = 2.0;
    /// <summary>Abstand der Streifenmitte von der Raumkante in Papier-mm (Annahme).</summary>
    public const double StreifenMitte = 1.0;
    /// <summary>Beheizt: schmaler (Papier-mm) und durchscheinend (Annahmen).</summary>
    public const double BeheiztBreite = 1.0;
    public const double BeheiztDeckkraft = 0.4;
    public const double OeffnungDeckkraft = 0.45;
    public const double BodenDeckkraft = 0.22;

    public static string Farbe(string angrenzung) => angrenzung switch
    {
        "aussen" => FarbeAussen,
        "erdreich" => FarbeErdreich,
        "unbeheizt" => FarbeUnbeheizt,
        _ => FarbeBeheizt,
    };

    /// <summary>Zeichenrang: kleiner = früher (liegt unten).</summary>
    static int Rang(string angrenzung) => angrenzung switch { "beheizt" => 0, "unbeheizt" => 1, "erdreich" => 2, _ => 3 };

    public bool Zustaendig(Katalog k, Element e) => false;
    public IEnumerable<Grafikteil> Plan(GrafikKontext ctx, Element e) => [];
    public Koerper? Koerper(Projekt p, Element e) => null;

    /// <summary>Ist die Darstellung «Hülle» für diese Ansicht eingeschaltet (Grundriss mit Geschoss, darstellung = huelle)?</summary>
    public static bool Aktiv(GrafikKontext ctx) =>
        ctx.Art == "grundriss" && ctx.Geschoss is not null && ctx.Ansicht.Text("darstellung") == "huelle";

    public IEnumerable<Grafikteil> Gemeinsam(GrafikKontext ctx)
    {
        if (!Aktiv(ctx)) return [];
        if (ctx.Gewerke is not null && !ctx.Gewerke.Contains("architektur")) return [];   // Hülle gehört zur Architektur
        using var _ = Wandgeometrie.Zwischenspeichern(ctx.Modell, ctx.Katalog);
        var erg = Raumgrenzen.JeGeschoss(ctx.Modell, ctx.Katalog, ctx.Geschoss!.Id);

        var flaechen = new List<(int Rang, Primitiv P)>();
        var streifen = new List<(int Rang, Primitiv P)>();

        foreach (var g in erg.Grenzen.Where(g => g.Art == "boden" && g.Angrenzung is "erdreich" or "aussen" && g.Flaechen is not null))
            foreach (var f in Polygone.Flaechen(g.Flaechen!))
                flaechen.Add((Rang(g.Angrenzung), new Primitiv
                {
                    Art = "polygon", Punkte = f.Aussen, Loecher = f.Loecher.Count > 0 ? f.Loecher : null,
                    Stil = new Stil { Stift = null, Fuellung = Farbe(g.Angrenzung), Deckkraft = BodenDeckkraft, NurBildschirm = true },
                }));

        var seiten = erg.Grenzen.Where(g => g.Art is "wand" or "fenster" or "tuer" && g.Von is not null && g.Bis is not null).ToList();
        var oeffnungen = seiten.Where(g => g.Art is "fenster" or "tuer").ToLookup(g => g.Raum.Id);
        foreach (var g in seiten)
        {
            string farbe = Farbe(g.Angrenzung);
            bool beheizt = g.Angrenzung == "beheizt";
            double breite = ctx.AufModell(beheizt ? BeheiztBreite : StreifenBreite);
            double mitte = ctx.AufModell(StreifenMitte) * (beheizt ? BeheiztBreite / StreifenBreite : 1);
            double a = mitte - breite / 2, b = mitte + breite / 2;
            if (g.Art == "wand" && !g.Offen)
            {
                foreach (var (s0, s1) in OhneOeffnungen(g, oeffnungen[g.Raum.Id]))
                    streifen.Add((Rang(g.Angrenzung), Streifen(g, s0, s1, a, b, new Stil
                    {
                        Stift = null, Fuellung = farbe, Deckkraft = beheizt ? BeheiztDeckkraft : null, NurBildschirm = true,
                    })));
            }
            else
            {
                // Fenster, Tür oder offenes Kantenstück: heller, gestrichelte Kontur
                streifen.Add((Rang(g.Angrenzung), Streifen(g, 0, g.Laenge, a, b, new Stil
                {
                    Stift = Stifte.Duenn, Linie = "strich", Farbe = farbe, Fuellung = farbe,
                    Deckkraft = beheizt ? BeheiztDeckkraft / 2 : OeffnungDeckkraft, NurBildschirm = true,
                })));
            }
        }

        var prim = flaechen.OrderBy(x => x.Rang).Select(x => x.P).Concat(streifen.OrderBy(x => x.Rang).Select(x => x.P)).ToList();
        if (prim.Count == 0) return [];
        return [new Grafikteil { Id = null, Ebene = "huelle", Primitive = prim }];
    }

    /// <summary>Viereck der Strecke s0 … s1 (mm ab Von) der Raumgrenze, a … b mm in den Raum hinein (gegen die Aussennormale).</summary>
    static Primitiv Streifen(Raumgrenze g, double s0, double s1, double a, double b, Stil stil)
    {
        var v = g.Von!; var w = g.Bis!;
        double l = g.Laenge, ux = (w[0] - v[0]) / l, uy = (w[1] - v[1]) / l;
        double nx = g.Normale[0], ny = g.Normale[1];
        double[] P(double s, double q) => [v[0] + ux * s - nx * q, v[1] + uy * s - ny * q];
        return new Primitiv { Art = "polygon", Punkte = [P(s0, a), P(s1, a), P(s1, b), P(s0, b)], Stil = stil };
    }

    /// <summary>Teilstrecken (mm ab Von) der Wand-Raumgrenze, die nicht von Fenstern oder Türen desselben Raums auf derselben
    /// Linie belegt sind. Öffnungsgrenzen liegen auf derselben Kante wie ihr Wandstück (Rundung: 5 mm Abstand erlaubt).</summary>
    static List<(double, double)> OhneOeffnungen(Raumgrenze wand, IEnumerable<Raumgrenze> oeffnungen)
    {
        double l = wand.Laenge;
        var v = wand.Von!; var w = wand.Bis!;
        double ux = (w[0] - v[0]) / l, uy = (w[1] - v[1]) / l;
        var luecken = new List<(double, double)>();
        foreach (var o in oeffnungen)
        {
            double ax = o.Von![0] - v[0], ay = o.Von[1] - v[1], bx = o.Bis![0] - v[0], by = o.Bis[1] - v[1];
            if (Math.Abs(ax * uy - ay * ux) > 5 || Math.Abs(bx * uy - by * ux) > 5) continue;
            double s0 = ax * ux + ay * uy, s1 = bx * ux + by * uy;
            if (s0 > s1) (s0, s1) = (s1, s0);
            s0 = Math.Max(s0, 0); s1 = Math.Min(s1, l);
            if (s1 - s0 > 1) luecken.Add((s0, s1));
        }
        var aus = new List<(double, double)>();
        double z = 0;
        foreach (var (s0, s1) in luecken.OrderBy(x => x.Item1))
        {
            if (s0 - z > 1) aus.Add((z, s0));
            z = Math.Max(z, s1);
        }
        if (l - z > 1) aus.Add((z, l));
        return aus;
    }
}
