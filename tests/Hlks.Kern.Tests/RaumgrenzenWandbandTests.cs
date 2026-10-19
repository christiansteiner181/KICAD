using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Wandband-Regel der Raumgrenzen (Modul 1c, Nachtrag zu B1/B5; Entscheid Hauptlauf 18.10.2026): Ein Rest von Boden bzw.
/// Decke, der in einem Wandband des Geschosses darunter bzw. darüber liegt, wird an der Wandachse geteilt; jede Hälfte geht an
/// den Raum auf ihrer Seite (Nachbar, Angrenzung dieses Raums; Art bleibt boden bzw. decke). Ohne Raum auf der Seite und
/// ausserhalb jedes Bands gilt die bisherige Regel (erdreich bzw. aussen). Jeder Erwartungswert ist von Hand aus der Geometrie
/// gerechnet (Rechenweg im Kommentar), nie durch Aufruf einer Produktfunktion bestimmt. Lichte Höhe 2.60 m (Testwert).
///
/// Grundhaus wie Referenz1cTests: Aussenmass 10.00 × 8.00 m, Aussenwände 300 mm → Achsen (150, 150) … (9850, 7850), innen
/// x 300 … 9700 (9.40 m), y 300 … 7700 (7.40 m). EG Kote 0, OG Kote 3000, je 3000 hoch; Terrain ±0.</summary>
public class RaumgrenzenWandbandTests
{
    const double Hoehe = 2600;   // lichte Höhe in mm (Testwert)

    static Projekt Leer()
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        return p;
    }

    /// <summary>Raum als Polygon (Punkte [x, y] in mm), lichte Höhe 2.60 m.</summary>
    static string Raum(Projekt p, string kz, string geschoss, double[][] punkte, bool beheizt = true)
    {
        var eig = new JsonObject { ["lichteHoehe"] = Hoehe };
        if (!beheizt) eig["beheizt"] = false;
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = kz, ["geschoss"] = geschoss, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts(punkte) },
        }));
        return kz;
    }

    static double[][] Rechteck(double x0, double y0, double x1, double y1) => [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];

    static string Decke(Projekt p, string geschoss) =>
        RaumgrenzenHilfe.Ok(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = geschoss, ["art"] = "decke", ["dicke"] = 250 }))
            .Aenderungen.Single(a => a.Klasse == "Decke" && a.Art == "angelegt").Kennzeichen;

    static List<Raumgrenze> Von(RaumgrenzenErgebnis e, string raum, params string[] arten) =>
        e.Grenzen.Where(g => g.Raum.Kennzeichen == raum && arten.Contains(g.Art)).ToList();

    static void Nahe(double erwartet, double ist, string text, double tol = 1e-6) => RaumgrenzenHilfe.Nahe(erwartet, ist, tol, text);

    static Raumgrenze Gegen(List<Raumgrenze> g, string nachbar) => g.Single(x => x.NachbarRaum?.Kennzeichen == nachbar);

    /// <summary>Ring der Darstellung schliesst auf die Fläche, Box des Rings wie erwartet (mm).</summary>
    static void RingWie(Raumgrenze g, double x0, double y0, double x1, double y1)
    {
        Assert.NotNull(g.Ring);
        Nahe(g.Flaeche * 1e6, Math.Abs(Polygone.Flaecheninhalt(g.Ring!)), "Ring = Fläche", 1e-3);
        Nahe(x0, g.Ring!.Min(q => q[0]), "Ring x0", 1e-6);
        Nahe(y0, g.Ring.Min(q => q[1]), "Ring y0", 1e-6);
        Nahe(x1, g.Ring.Max(q => q[0]), "Ring x1", 1e-6);
        Nahe(y1, g.Ring.Max(q => q[1]), "Ring y1", 1e-6);
    }

    // ------------------------------------------------------------------ Aufbauten der Fälle

    /// <summary>(a) Referenzfall B5: EG Innenwand 150 mm, Achse x = 4000 (Flächen 3925 … 4075) zwischen R-A (beheizt) und R-B
    /// (unbeheizt); OG ein Raum über der ganzen Innenfläche. ohneRB: R-B fehlt (Fläche ohne Raum darunter).</summary>
    static Projekt FallA(bool ohneRB = false, Func<double[], double[]>? lage = null)
    {
        lage ??= q => q;
        double[][] L(double[][] pkt) => pkt.Select(lage).ToArray();
        var p = Leer();
        RaumgrenzenHilfe.Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = RaumgrenzenHilfe.Pts(L(Rechteck(150, 150, 9850, 7850))), ["dicke"] = 300, ["geschlossen"] = true,
        }));
        RaumgrenzenHilfe.Wand(p, "EG", lage([4000, 300]), lage([4000, 7700]), 150);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "OG", ["punkte"] = RaumgrenzenHilfe.Pts(L(Rechteck(150, 150, 9850, 7850))), ["dicke"] = 300, ["geschlossen"] = true,
        }));
        Raum(p, "R-A", "EG", L(Rechteck(300, 300, 3925, 7700)));
        if (!ohneRB) Raum(p, "R-B", "EG", L(Rechteck(4075, 300, 9700, 7700)), beheizt: false);
        Raum(p, "R-OG", "OG", L(Rechteck(300, 300, 9700, 7700)));
        Decke(p, "EG");
        return p;
    }

    /// <summary>(b) Spiegelfall: EG ein Raum R-EG über der ganzen Innenfläche; OG Innenwand 150 mm, Achse x = 4000, zwischen R-OA
    /// (beheizt, x 300 … 3925) und R-OB (unbeheizt, x 4075 … 9700).</summary>
    static Projekt FallB()
    {
        var p = Leer();
        RaumgrenzenHilfe.Waende(p, "EG", 150, 150, 9850, 7850, 300);
        RaumgrenzenHilfe.Waende(p, "OG", 150, 150, 9850, 7850, 300);
        RaumgrenzenHilfe.Wand(p, "OG", [4000, 300], [4000, 7700], 150);
        Raum(p, "R-EG", "EG", Rechteck(300, 300, 9700, 7700));
        Raum(p, "R-OA", "OG", Rechteck(300, 300, 3925, 7700));
        Raum(p, "R-OB", "OG", Rechteck(4075, 300, 9700, 7700), beheizt: false);
        Decke(p, "EG");
        return p;
    }

    /// <summary>(c) Wandkreuzung im EG: Wand V 150 mm, Achse x = 4000 (Flächen 3925 … 4075), Wand H 200 mm, Achse y = 4000
    /// (Flächen 3900 … 4100). Vier Räume: SW x 300 … 3925 / y 300 … 3900, SO x 4075 … 9700 / y 300 … 3900, NW x 300 … 3925 /
    /// y 4100 … 7700, NO x 4075 … 9700 / y 4100 … 7700 (NO unbeheizt). OG ein Raum über der ganzen Innenfläche.</summary>
    static Projekt FallC()
    {
        var p = Leer();
        RaumgrenzenHilfe.Waende(p, "EG", 150, 150, 9850, 7850, 300);
        RaumgrenzenHilfe.Wand(p, "EG", [4000, 300], [4000, 7700], 150);
        RaumgrenzenHilfe.Wand(p, "EG", [300, 4000], [9700, 4000], 200);
        RaumgrenzenHilfe.Waende(p, "OG", 150, 150, 9850, 7850, 300);
        Raum(p, "R-SW", "EG", Rechteck(300, 300, 3925, 3900));
        Raum(p, "R-SO", "EG", Rechteck(4075, 300, 9700, 3900));
        Raum(p, "R-NW", "EG", Rechteck(300, 4100, 3925, 7700));
        Raum(p, "R-NO", "EG", Rechteck(4075, 4100, 9700, 7700), beheizt: false);
        Raum(p, "R-OG", "OG", Rechteck(300, 300, 9700, 7700));
        Decke(p, "EG");
        return p;
    }

    /// <summary>(d) Dicke EG-Aussenwände 400 mm um die Achsen (200, 200) … (9800, 7800): Flächen aussen 0 … 10000 / 0 … 8000,
    /// innen 400 … 9600 / 400 … 7600; R-EG = Innenfläche 9.20 × 7.20 m. OG-Aussenwände 200 mm, R-OG x 300 … 11600 (ragt im Osten
    /// 1.60 m über die Aussenseite der EG-Wand hinaus, im Westen 0.10 m über deren Innenseite), y 400 … 7600 bzw. mit ecke bis
    /// y 9000 (ragt auch im Norden 1.00 m hinaus). Keine Platten.</summary>
    static Projekt FallD(bool ecke = false)
    {
        var p = Leer();
        RaumgrenzenHilfe.Waende(p, "EG", 200, 200, 9800, 7800, 400);
        double yo = ecke ? 9000 : 7600;
        RaumgrenzenHilfe.Waende(p, "OG", 200, 300, 11700, yo + 100, 200);
        Raum(p, "R-EG", "EG", Rechteck(400, 400, 9600, 7600));
        Raum(p, "R-OG", "OG", Rechteck(300, 400, 11600, yo));
        return p;
    }

    /// <summary>(f) Anschluss wie bei BKW (W-1249 an W-1346): im OG läuft die Stichwand V 200 mm, Achse x = 5000, von der Südwand bis
    /// an die Querwand H (Achse y = 6000); H besteht aus zwei Wänden 200 mm, H1 x 300 … 5000 und H2 x 5000 … 9700 (je kürzer als V,
    /// V wird also zuerst geteilt). OG-Räume SW x 300 … 4900 / y 300 … 5900, SO x 5100 … 9700 / y 300 … 5900, N x 300 … 9700 /
    /// y 6100 … 7700; EG ein Raum R-EG über der ganzen Innenfläche. schief: H1 endet 1 mm höher (y 6001), wie W-1346 bei BKW.</summary>
    static Projekt FallF(bool schief = false)
    {
        var p = Leer();
        RaumgrenzenHilfe.Waende(p, "EG", 150, 150, 9850, 7850, 300);
        RaumgrenzenHilfe.Waende(p, "OG", 150, 150, 9850, 7850, 300);
        RaumgrenzenHilfe.Wand(p, "OG", [5000, 300], [5000, 6000], 200);
        RaumgrenzenHilfe.Wand(p, "OG", [300, 6000], [5000, schief ? 6001 : 6000], 200);
        RaumgrenzenHilfe.Wand(p, "OG", [5000, schief ? 6001 : 6000], [9700, 6000], 200);
        Raum(p, "R-EG", "EG", Rechteck(300, 300, 9700, 7700));
        Raum(p, "R-SW", "OG", Rechteck(300, 300, 4900, 5900));
        Raum(p, "R-SO", "OG", Rechteck(5100, 300, 9700, 5900));
        Raum(p, "R-N", "OG", Rechteck(300, 6100, 9700, 7700));
        return p;
    }

    // ------------------------------------------------------------------ (a) Boden über einer Innenwand des Geschosses darunter

    [Fact]
    public void A_BodenUeberInnenwandDarunter_AnDerAchseGeteilt_JeHaelfteZumRaumDaneben()
    {
        var p = FallA();
        var e = RaumgrenzenHilfe.Rechnen(p);
        var b = Von(e, "R-OG", "boden");
        // R-A = 3.625 × 7.40 = 26.825 m², R-B = 5.625 × 7.40 = 41.625 m²; Streifen über der Innenwand x 3925 … 4075 = 0.15 × 7.40
        // = 1.11 m², an der Achse x = 4000 geteilt: je 0.075 × 7.40 = 0.555 m².
        // Gegen R-A: x 300 … 4000 = 3.70 × 7.40 = 27.38 m² (= 26.825 + 0.555), beheizt; gegen R-B: x 4000 … 9700 = 5.70 × 7.40
        // = 42.18 m² (= 41.625 + 0.555), unbeheizt. Kein Boden gegen aussen.
        Assert.Equal(2, b.Count);
        var a = Gegen(b, "R-A");
        var rb = Gegen(b, "R-B");
        Nahe(3.70 * 7.40, a.Flaeche, "gegen R-A");
        Nahe(5.70 * 7.40, rb.Flaeche, "gegen R-B");
        Assert.Equal("beheizt", a.Angrenzung);
        Assert.Equal("unbeheizt", rb.Angrenzung);
        Assert.All(b, x => { Assert.Equal("boden", x.Art); Assert.Equal(180, x.Neigung); Assert.Equal(3000, x.Unten); });
        Assert.DoesNotContain(b, x => x.Angrenzung == "aussen");
        // Bauteil bleibt die Decke über EG (überdeckt auch den Streifen)
        var decke = p.Modell.DerKlasse("Decke").Single().Kennzeichen;
        Assert.All(b, x => Assert.Equal(decke, x.Bauteil?.Kennzeichen));
        // je Nachbar eine zusammenhängende Fläche: Ring x 300 … 4000 bzw. 4000 … 9700, y 300 … 7700
        RingWie(a, 300, 300, 4000, 7700);
        RingWie(rb, 4000, 300, 9700, 7700);
        // die Decken der EG-Räume bleiben ganz gegen R-OG (kein Rest): 26.825 bzw. 41.625 m², beheizt
        Nahe(26.825, Gegen(Von(e, "R-A", "decke"), "R-OG").Flaeche, "Decke R-A");
        Nahe(41.625, Gegen(Von(e, "R-B", "decke"), "R-OG").Flaeche, "Decke R-B");
        // Hinweis: zwei Stücke zu je 0.555 m², zusammen 1.11 m²
        Assert.Contains(e.Hinweise, h => h.Contains("an der Wandachse geteilt") && h.Contains("2 Stücke, 1.11 m²"));
    }

    [Fact]
    public void A_OhneRaumAufEinerSeite_DieseHaelfteNachDerBisherigenRegel()
    {
        // R-B fehlt: West-Hälfte des Streifens (0.555 m²) geht zu R-A → 27.38 m²; Ost-Hälfte 0.555 m² und die Fläche ohne Raum
        // darunter x 4075 … 9700 (41.625 m²) bleiben gegen aussen (OG nicht unterstes Geschoss, über Terrain): 5.70 × 7.40 = 42.18 m².
        var e = RaumgrenzenHilfe.Rechnen(FallA(ohneRB: true));
        var b = Von(e, "R-OG", "boden");
        Nahe(3.70 * 7.40, Gegen(b, "R-A").Flaeche, "gegen R-A");
        var aussen = b.Single(x => x.NachbarRaum is null);
        Assert.Equal("aussen", aussen.Angrenzung);
        Assert.Equal("aussen", aussen.Nachbar);
        Nahe(5.70 * 7.40, aussen.Flaeche, "aussen");
        RingWie(aussen, 4000, 300, 9700, 7700);
        Assert.Contains(e.Hinweise, h => h.Contains("an der Wandachse geteilt") && h.Contains("1 Stück, 0.56 m²"));
    }

    // ------------------------------------------------------------------ (b) Spiegelfall: Decke unter einer Innenwand darüber

    [Fact]
    public void B_DeckeUnterInnenwandDarueber_AnDerAchseGeteilt()
    {
        var p = FallB();
        var e = RaumgrenzenHilfe.Rechnen(p);
        var d = Von(e, "R-EG", "decke", "dach");
        // R-EG = 9.40 × 7.40 = 69.56 m²; darüber R-OA 3.625 × 7.40 = 26.825 und R-OB 5.625 × 7.40 = 41.625 m²; Streifen unter der
        // OG-Innenwand 0.15 × 7.40 = 1.11 m², an der Achse x = 4000 geteilt.
        // Gegen R-OA: 3.70 × 7.40 = 27.38 m² beheizt; gegen R-OB: 5.70 × 7.40 = 42.18 m² unbeheizt; Art decke, kein Dach.
        Assert.Equal(2, d.Count);
        var oa = Gegen(d, "R-OA");
        var ob = Gegen(d, "R-OB");
        Nahe(3.70 * 7.40, oa.Flaeche, "gegen R-OA");
        Nahe(5.70 * 7.40, ob.Flaeche, "gegen R-OB");
        Nahe(26.825 + 0.555, oa.Flaeche, "Zahlenprobe R-OA");
        Nahe(41.625 + 0.555, ob.Flaeche, "Zahlenprobe R-OB");
        Assert.Equal("beheizt", oa.Angrenzung);
        Assert.Equal("unbeheizt", ob.Angrenzung);
        Assert.All(d, x => { Assert.Equal("decke", x.Art); Assert.Equal(0, x.Neigung); Assert.Equal(Hoehe, x.Unten); });
        var decke = p.Modell.DerKlasse("Decke").Single().Kennzeichen;
        Assert.All(d, x => Assert.Equal(decke, x.Bauteil?.Kennzeichen));
        RingWie(oa, 300, 300, 4000, 7700);
        RingWie(ob, 4000, 300, 9700, 7700);
        // die Böden der OG-Räume liegen ganz über R-EG: 26.825 bzw. 41.625 m²
        Nahe(26.825, Gegen(Von(e, "R-OA", "boden"), "R-EG").Flaeche, "Boden R-OA");
        Nahe(41.625, Gegen(Von(e, "R-OB", "boden"), "R-EG").Flaeche, "Boden R-OB");
    }

    // ------------------------------------------------------------------ (c) Wandkreuzung

    [Fact]
    public void C_Wandkreuzung_JedeTeilflaecheZumNaechstenRaum()
    {
        var e = RaumgrenzenHilfe.Rechnen(FallC());
        var b = Von(e, "R-OG", "boden");
        // Räume: SW 3.625 × 3.60 = 13.05, SO 5.625 × 3.60 = 20.25, NW 13.05, NO 20.25 → 66.60 m²; Rest = Kreuz aus V-Streifen
        // 0.15 × 7.40 = 1.11 und H-Streifen 0.20 × 9.40 = 1.88 minus Kreuzungsfeld 0.15 × 0.20 = 0.03 → 2.96 m² (69.56 − 66.60).
        // Geteilt an den Achsen x = 4000 und y = 4000, das Kreuzungsfeld in vier Viertel:
        //   SW: x 300 … 4000 × y 300 … 4000 = 3.70 × 3.70 = 13.69 (= 13.05 + H-Hälfte 3.70 × 0.10 + V-Hälfte 0.075 × 3.60 = 0.37 + 0.27)
        //   SO: x 4000 … 9700 × y 300 … 4000 = 5.70 × 3.70 = 21.09 (= 20.25 + 0.57 + 0.27)
        //   NW: 3.70 × 3.70 = 13.69;  NO: 5.70 × 3.70 = 21.09 (unbeheizt)
        Assert.Equal(4, b.Count);
        Nahe(3.70 * 3.70, Gegen(b, "R-SW").Flaeche, "SW");
        Nahe(5.70 * 3.70, Gegen(b, "R-SO").Flaeche, "SO");
        Nahe(3.70 * 3.70, Gegen(b, "R-NW").Flaeche, "NW");
        Nahe(5.70 * 3.70, Gegen(b, "R-NO").Flaeche, "NO");
        Nahe(13.05 + 0.37 + 0.27, Gegen(b, "R-SW").Flaeche, "Zahlenprobe SW");
        Nahe(20.25 + 0.57 + 0.27, Gegen(b, "R-SO").Flaeche, "Zahlenprobe SO");
        Assert.Equal("unbeheizt", Gegen(b, "R-NO").Angrenzung);
        Assert.All(b.Where(x => x.NachbarRaum?.Kennzeichen != "R-NO"), x => Assert.Equal("beheizt", x.Angrenzung));
        Assert.DoesNotContain(b, x => x.NachbarRaum is null);
        RingWie(Gegen(b, "R-SW"), 300, 300, 4000, 4000);
        RingWie(Gegen(b, "R-SO"), 4000, 300, 9700, 4000);
        RingWie(Gegen(b, "R-NW"), 300, 4000, 4000, 7700);
        RingWie(Gegen(b, "R-NO"), 4000, 4000, 9700, 7700);
        // Summen je Angrenzung: beheizt 13.69 + 21.09 + 13.69 = 48.47, unbeheizt 21.09
        Nahe(48.47, RaumgrenzenHilfe.Summe(b, x => x.Angrenzung == "beheizt"), "beheizt");
        Nahe(21.09, RaumgrenzenHilfe.Summe(b, x => x.Angrenzung == "unbeheizt"), "unbeheizt");
    }

    // ------------------------------------------------------------------ (d) Auskragung über eine dicke EG-Aussenwand

    [Fact]
    public void D_AuskragungUeberDickeAussenwand_InnenhaelfteZumRaum_AussenhaelfteAussen()
    {
        var e = RaumgrenzenHilfe.Rechnen(FallD());
        var b = Von(e, "R-OG", "boden");
        // R-OG x 300 … 11600 × y 400 … 7600 = 11.30 × 7.20 = 81.36 m²; R-EG x 400 … 9600 = 9.20 × 7.20 = 66.24 m².
        // West: Streifen x 300 … 400 liegt in der Innenhälfte der Westwand (Achse x = 200) → R-EG: 0.10 × 7.20 = 0.72 m².
        // Ost: Ostwand Achse x = 9800, Band 9600 … 10000: Innenhälfte 9600 … 9800 → R-EG 0.20 × 7.20 = 1.44 m²; Aussenhälfte
        // 9800 … 10000 (1.44 m²) und Auskragung 10000 … 11600 (1.60 × 7.20 = 11.52 m²) → aussen.
        // Gegen R-EG: x 300 … 9800 = 9.50 × 7.20 = 68.40 m² (= 66.24 + 0.72 + 1.44); aussen: x 9800 … 11600 = 1.80 × 7.20 = 12.96 m².
        Assert.Equal(2, b.Count);
        var r = Gegen(b, "R-EG");
        var a = b.Single(x => x.NachbarRaum is null);
        Nahe(9.50 * 7.20, r.Flaeche, "gegen R-EG");
        Nahe(66.24 + 0.72 + 1.44, r.Flaeche, "Zahlenprobe R-EG");
        Assert.Equal("beheizt", r.Angrenzung);
        Nahe(1.80 * 7.20, a.Flaeche, "aussen");
        Nahe(1.44 + 11.52, a.Flaeche, "Zahlenprobe aussen");
        Assert.Equal("aussen", a.Angrenzung);
        RingWie(r, 300, 400, 9800, 7600);
        RingWie(a, 9800, 400, 11600, 7600);
    }

    [Fact]
    public void D_AuskragungUeberEcke_InneresViertelZumRaum()
    {
        var e = RaumgrenzenHilfe.Rechnen(FallD(ecke: true));
        var b = Von(e, "R-OG", "boden");
        // R-OG x 300 … 11600 × y 400 … 9000 = 11.30 × 8.60 = 97.18 m². Nordwand EG Achse y = 7800 (Band 7600 … 8000), Ostwand Achse
        // x = 9800. Zu R-EG gehören die Innenhälften und an der Ecke NO nur das innere Viertel x 9600 … 9800 × y 7600 … 7800, an der
        // Ecke NW das Viertel x 300 … 400 × y 7600 … 7800 (Innenhälften beider Wände):
        // gegen R-EG x 300 … 9800 × y 400 … 7800 = 9.50 × 7.40 = 70.30 m²; aussen 97.18 − 70.30 = 26.88 m²
        // (Probe: Ost 1.80 × 7.40 = 13.32 + Nord 11.30 × 1.20 = 13.56 → 26.88).
        Nahe(9.50 * 7.40, Gegen(b, "R-EG").Flaeche, "gegen R-EG");
        RingWie(Gegen(b, "R-EG"), 300, 400, 9800, 7800);
        var a = b.Where(x => x.NachbarRaum is null).ToList();
        Assert.All(a, x => Assert.Equal("aussen", x.Angrenzung));
        Nahe(97.18 - 70.30, a.Sum(x => x.Flaeche), "aussen");
        Nahe(1.80 * 7.40 + 11.30 * 1.20, a.Sum(x => x.Flaeche), "Zahlenprobe aussen");
    }

    // ------------------------------------------------------------------ (f) Anschluss am Ende der Suchlinie (Befund BKW)

    [Fact]
    public void F_StichwandEndetAnQuerwand_AnschlussfeldAnDerAchseDerQuerwandGeteilt()
    {
        var e = RaumgrenzenHilfe.Rechnen(FallF());
        var d = Von(e, "R-EG", "decke", "dach");
        // R-EG 9.40 × 7.40 = 69.56 m²; darüber SW 4.60 × 5.60 = 25.76, SO 25.76, N 9.40 × 1.60 = 15.04 → 66.56 m²; Rest 3.00 m² =
        // V-Streifen x 4900 … 5100 × y 300 … 5900 (0.20 × 5.60 = 1.12) + H-Streifen y 5900 … 6100 × x 300 … 9700 (0.20 × 9.40 = 1.88).
        // Geteilt an den Achsen x = 5000 und y = 6000, das Anschlussfeld x 4900 … 5100 × y 5900 … 6100 in vier Viertel:
        //   SW: x 300 … 5000 × y 300 … 6000 = 4.70 × 5.70 = 26.79 (= 25.76 + V-Hälfte 0.10 × 5.60 + H-Hälfte 4.70 × 0.10 = 0.56 + 0.47)
        //   SO: x 5000 … 9700 × y 300 … 6000 = 26.79;  N: x 300 … 9700 × y 6000 … 7700 = 9.40 × 1.70 = 15.98 (= 15.04 + 0.94)
        // V (Band im Rest y 300 … 6100, rund 1.17 m²) wird vor H1 und H2 (je x-Bereich 4.82 m, rund 0.96 m²) geteilt; ihre
        // Suchlinie endete ohne Verlängerung im Band von H, das obere Viertel des Anschlussfelds (y 6000 … 6100) ginge dann an SW
        // bzw. SO statt an N (je 0.12 × 0.10 m mit der Toleranz).
        Assert.Equal(3, d.Count);
        Nahe(4.70 * 5.70, Gegen(d, "R-SW").Flaeche, "SW");
        Nahe(4.70 * 5.70, Gegen(d, "R-SO").Flaeche, "SO");
        Nahe(9.40 * 1.70, Gegen(d, "R-N").Flaeche, "N");
        Nahe(25.76 + 0.56 + 0.47, Gegen(d, "R-SW").Flaeche, "Zahlenprobe SW");
        Nahe(15.04 + 0.94, Gegen(d, "R-N").Flaeche, "Zahlenprobe N");
        RingWie(Gegen(d, "R-SW"), 300, 300, 5000, 6000);
        RingWie(Gegen(d, "R-N"), 300, 6000, 9700, 7700);
    }

    [Fact]
    public void F_QuerwandLeichtSchief_KeineSplitterGegenAussen()
    {
        // Wie oben, H1 aber um 1 mm auf 4.70 m schief (BKW W-1346): Raumkante und Bandrand liegen um Bruchteile eines mm
        // auseinander. Diese Splitter dürfen die Querwand nicht zur Hälfte gegen «kein Raum» teilen: keine Decke gegen aussen,
        // die Flächen weichen nur um die Schiefe ab (höchstens 4.70 m × 1 mm = 0.0047 m²).
        var d = Von(RaumgrenzenHilfe.Rechnen(FallF(schief: true)), "R-EG", "decke", "dach");
        Assert.DoesNotContain(d, x => x.NachbarRaum is null);
        Assert.Equal(3, d.Count);
        Nahe(4.70 * 5.70, Gegen(d, "R-SW").Flaeche, "SW", 0.005);
        Nahe(4.70 * 5.70, Gegen(d, "R-SO").Flaeche, "SO", 0.005);
        Nahe(9.40 * 1.70, Gegen(d, "R-N").Flaeche, "N", 0.005);
        Nahe(69.56, d.Sum(x => x.Flaeche), "Summe", 1e-5);
    }

    // ------------------------------------------------------------------ (e) Bodenbilanz unverändert

    public static TheoryData<string> Faelle => ["A", "A ohne R-B", "B", "C", "D", "D Ecke", "F", "F schief", "A gedreht 30°"];

    static Projekt Fall(string name) => name switch
    {
        "A" => FallA(),
        "A ohne R-B" => FallA(ohneRB: true),
        "B" => FallB(),
        "C" => FallC(),
        "D" => FallD(),
        "D Ecke" => FallD(ecke: true),
        "F" => FallF(),
        "F schief" => FallF(schief: true),
        "A gedreht 30°" => FallA(lage: Gedreht),
        _ => throw new ArgumentException(name),
    };

    /// <summary>Drehung um (0, 0) um 30° im Gegenuhrzeigersinn (schräge Wände: Band und Suchlinie nicht achsparallel).</summary>
    static double[] Gedreht(double[] q)
    {
        double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
        return [q[0] * c - q[1] * s, q[0] * s + q[1] * c];
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void E_Bodenbilanz_SummeBodenGleichSummeRaumflaechen_JeRaumBodenUndDecke(string fall)
    {
        var e = RaumgrenzenHilfe.Rechnen(Fall(fall));
        double tol = fall.Contains("gedreht") || fall.Contains("schief") ? 1e-4 : 1e-6;   // schräge Kanten: Rundung auf 0.001 mm
        // Summe Boden = Summe Raumflächen; je Raum Boden = Raumfläche und Decke + Dach = Raumfläche – die Regel ändert nur die Zuordnung
        Nahe(e.Raeume.Sum(r => r.FlaecheM2), RaumgrenzenHilfe.Summe(e.Grenzen, x => x.Art == "boden"), $"{fall}: Summe Boden", tol);
        foreach (var r in e.Raeume)
        {
            var g = e.Grenzen.Where(x => x.Raum.Id == r.Raum.Id).ToList();
            Nahe(r.FlaecheM2, RaumgrenzenHilfe.Summe(g, x => x.Art == "boden"), $"{fall}: {r.Raum.Kennzeichen} Boden", tol);
            Nahe(r.FlaecheM2, RaumgrenzenHilfe.Summe(g, x => x.Art is "decke" or "dach"), $"{fall}: {r.Raum.Kennzeichen} Decke/Dach", tol);
        }
    }

    [Fact]
    public void E_Gedreht30_GleicheZuordnungWieAchsparallel()
    {
        // Fall (a) um 30° gedreht: Flächen wie achsparallel (27.38 / 42.18 m²), kein Boden gegen aussen im OG
        var b = Von(RaumgrenzenHilfe.Rechnen(FallA(lage: Gedreht)), "R-OG", "boden");
        Nahe(3.70 * 7.40, RaumgrenzenHilfe.Summe(b, x => x.NachbarRaum?.Kennzeichen == "R-A"), "gegen R-A", 1e-4);
        Nahe(5.70 * 7.40, RaumgrenzenHilfe.Summe(b, x => x.NachbarRaum?.Kennzeichen == "R-B"), "gegen R-B", 1e-4);
        Assert.DoesNotContain(b, x => x.Angrenzung == "aussen");
    }
}
