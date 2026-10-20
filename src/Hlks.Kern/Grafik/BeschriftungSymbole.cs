// Symbole der Plangrafik: Nordpfeil (Symbol) und Schnittsymbol der Schnitt-/Ansichtslinien in Grundrissen.
// Quelle Schnittsymbol: SIA 400:2000 Ziff. B.6.1 (Pfeil in Blickrichtung, dünnste Linie; Schnittlinie strichpunktiert,
// an den Enden dick nach Tab. 4 in B.3.3; Bezeichnung = Kennzeichen der Schnittansicht). Seit Modul 7 Teil B1: «<Titel> / <Plankürzel>» mit dem Blatt,
// auf dem die Ansicht liegt (Blattverweis.cs; «—», solange sie auf keinem Blatt liegt).
// ANNAHMEN (nicht aus der Norm, Prüfliste Christian), in Papier-mm:
//   dicke Endstücke der Schnittlinie je 10 mm; dazwischen dünn strichpunktiert durchgehend
//   Pfeil: Schaft 6 mm, Spitze 2.5 mm lang und 1.6 mm breit (ausgefüllt); Bezeichnung 2.5 mm Schrift, 1 mm vom Pfeil
//   Blickrichtung ohne Angabe: links der Linie Anfang → Ende
//   Nordpfeil: Gestalt wie in js/ansichten/grundriss.js (Pfeil Spitze–Fuss 40 Einheiten = groesse, Vorgabe 15 mm),
//   «N» darunter (wie dort) mit 3.5 mm Schrift bei 15 mm Grösse (skaliert mit groesse); Einfügepunkt = Mitte des Pfeils

namespace Hlks.Kern;

internal static class BeschriftungSymbole
{
    const double EndstueckLaenge = 10.0;
    const double PfeilSchaft = 6.0;
    const double PfeilSpitzeLaenge = 2.5;
    const double PfeilSpitzeBreite = 1.6;
    const double BezeichnungSchrift = 2.5;
    const double BezeichnungAbstand = 1.0;
    const double NordVorgabe = 15.0;
    const double NordSchrift = 3.5;

    // ------------------------------------------------------------------------------------------ Nordpfeil

    public static IEnumerable<Grafikteil> Symbol(GrafikKontext ctx, Element e)
    {
        if ((e.Text("art") ?? "nordpfeil") != "nordpfeil") return [];
        var p = e.Geometrie!.Punkte[0];
        double groesse = e.Zahl("groesse") is double g and > 0 ? g : NordVorgabe;
        double winkel = e.Zahl("winkel") ?? 0;
        double k = groesse / 40;   // Papier-mm je Einheit des SVG (Pfeil von y = 4 bis y = 44)

        // lokale SVG-Einheit (x rechts, y unten, Mitte des Pfeils bei 20/24) → Plan (Papier-mm, y oben), drehen, auf Modell
        double[] Plan(double ux, double uy)
        {
            var lokal = BeschriftungHilfen.Drehen([(ux - 20) * k, -(uy - 24) * k], winkel);
            return [p[0] + ctx.AufModell(lokal[0]), p[1] + ctx.AufModell(lokal[1])];
        }

        // ganzer Pfeil (Umriss) und linke Hälfte gefüllt – wie die zwei Pfade im SVG
        var umriss = BeschriftungHilfen.Polygon([Plan(20, 4), Plan(31, 44), Plan(20, 36), Plan(9, 44)], Stifte.Duenn);
        var haelfte = BeschriftungHilfen.Polygon([Plan(20, 4), Plan(20, 36), Plan(9, 44)], null, "schnitt");
        double nSchrift = NordSchrift * groesse / NordVorgabe;
        var nPos = BeschriftungHilfen.Drehen([0, -(44 - 24) * k - 1.0 - 0.7 * nSchrift], winkel);
        var n = BeschriftungHilfen.Text([p[0] + ctx.AufModell(nPos[0]), p[1] + ctx.AufModell(nPos[1])], "N", nSchrift, "mitte", winkel, fett: true);

        return [new Grafikteil
        {
            Id = e.Id, Ebene = "symbol", Primitive = [haelfte, umriss, n],
            Fangpunkte = [[p[0], p[1]]],
        }];
    }

    // ------------------------------------------------------------------------------------------ Schnittsymbol

    public static IEnumerable<Grafikteil> Schnittsymbol(GrafikKontext ctx, Element e)
    {
        if (e.Text("art") is not ("schnitt" or "ansicht")) return [];
        var pts = BeschriftungHilfen.Punkte2D(e.Geometrie!);
        if (pts.Count < 2) return [];

        double gesamt = Weglaenge(pts);
        if (gesamt < 1e-9) return [];
        double ende = ctx.AufModell(EndstueckLaenge);
        var prim = new List<Primitiv>();
        if (gesamt <= 2 * ende)
            prim.Add(BeschriftungHilfen.Polylinie(pts, Stifte.Dick, "strichpunkt"));
        else
        {
            prim.Add(BeschriftungHilfen.Polylinie(Teilweg(pts, 0, ende), Stifte.Dick, "strichpunkt"));
            prim.Add(BeschriftungHilfen.Polylinie(Teilweg(pts, ende, gesamt - ende), Stifte.Duenn, "strichpunkt"));
            prim.Add(BeschriftungHilfen.Polylinie(Teilweg(pts, gesamt - ende, gesamt), Stifte.Dick, "strichpunkt"));
        }

        // Pfeile an beiden Enden in Blickrichtung; Bezeichnung neben der Pfeilspitze: «<Titel> / <Plankürzel>» (Modul 7 Teil B1, Blattverweis.cs)
        var verweis = Blattverweis.Text(ctx.Modell, e);
        bool links = (e.Text("blick") ?? "links") != "rechts";
        var dAnfang = BeschriftungHilfen.Einheit(BeschriftungHilfen.Minus(pts[1], pts[0]));
        var dEnde = BeschriftungHilfen.Einheit(BeschriftungHilfen.Minus(pts[^1], pts[^2]));
        foreach (var (punkt, d) in new[] { (pts[0], dAnfang), (pts[^1], dEnde) })
        {
            var blick = links ? BeschriftungHilfen.Links(d) : BeschriftungHilfen.Mal(BeschriftungHilfen.Links(d), -1);
            Pfeil(ctx, prim, punkt, blick);
            var spitze = BeschriftungHilfen.Plus(punkt, BeschriftungHilfen.Mal(blick, ctx.AufModell(PfeilSchaft)));
            var mitte = BeschriftungHilfen.Plus(spitze, BeschriftungHilfen.Mal(blick,
                ctx.AufModell(BezeichnungAbstand + BezeichnungSchrift * 0.5)));
            // Text mittig zur Mitte setzen: Grundlinie liegt um etwa 0.35 der Schrifthöhe tiefer
            var pos = new[] { mitte[0], mitte[1] - ctx.AufModell(BezeichnungSchrift * 0.35) };
            string ausrichtung = "mitte";
            if (Math.Abs(blick[0]) >= 0.5)
            {
                // Blick waagrecht: der lange Text «Titel / Plankürzel» steht neben der Spitze, vom Pfeil weg (nicht quer über die Schnittlinie)
                ausrichtung = blick[0] > 0 ? "links" : "rechts";
                pos = [spitze[0] + Math.Sign(blick[0]) * ctx.AufModell(BezeichnungAbstand), spitze[1] - ctx.AufModell(BezeichnungSchrift * 0.35)];
            }
            prim.Add(BeschriftungHilfen.Text(pos, verweis, BezeichnungSchrift, ausrichtung));
        }

        return [new Grafikteil
        {
            Id = e.Id, Ebene = "symbol", Primitive = prim,
            Fangpunkte = [[pts[0][0], pts[0][1]], [pts[^1][0], pts[^1][1]]],
        }];
    }

    /// <summary>Pfeil (Schaft dünn, ausgefüllte Spitze) vom Punkt in Richtung <paramref name="blick"/>.</summary>
    static void Pfeil(GrafikKontext ctx, List<Primitiv> prim, double[] von, double[] blick)
    {
        var quer = BeschriftungHilfen.Links(blick);
        var spitze = BeschriftungHilfen.Plus(von, BeschriftungHilfen.Mal(blick, ctx.AufModell(PfeilSchaft)));
        var fuss = BeschriftungHilfen.Minus(spitze, BeschriftungHilfen.Mal(blick, ctx.AufModell(PfeilSpitzeLaenge)));
        double halb = ctx.AufModell(PfeilSpitzeBreite) / 2;
        prim.Add(BeschriftungHilfen.Linie(von, fuss, Stifte.Duenn));
        prim.Add(BeschriftungHilfen.Polygon([spitze,
            BeschriftungHilfen.Plus(fuss, BeschriftungHilfen.Mal(quer, halb)),
            BeschriftungHilfen.Minus(fuss, BeschriftungHilfen.Mal(quer, halb))], Stifte.Duenn, "schnitt"));
    }

    static double Weglaenge(List<double[]> pts)
    {
        double l = 0;
        for (int i = 1; i < pts.Count; i++) l += BeschriftungHilfen.Laenge(BeschriftungHilfen.Minus(pts[i], pts[i - 1]));
        return l;
    }

    /// <summary>Der Teil des Linienzugs zwischen den Weglängen <paramref name="von"/> und <paramref name="bis"/> (ab Anfang).</summary>
    static List<double[]> Teilweg(List<double[]> pts, double von, double bis)
    {
        var aus = new List<double[]>();
        double pos = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var b = pts[i];
            double l = BeschriftungHilfen.Laenge(BeschriftungHilfen.Minus(b, a));
            double s0 = pos, s1 = pos + l;
            pos = s1;
            if (l < 1e-9 || s1 <= von || s0 >= bis) continue;
            double u0 = Math.Max(von, s0), u1 = Math.Min(bis, s1);
            var p0 = BeschriftungHilfen.Plus(a, BeschriftungHilfen.Mal(BeschriftungHilfen.Minus(b, a), (u0 - s0) / l));
            var p1 = BeschriftungHilfen.Plus(a, BeschriftungHilfen.Mal(BeschriftungHilfen.Minus(b, a), (u1 - s0) / l));
            if (aus.Count == 0) aus.Add(p0);
            aus.Add(p1);
        }
        return aus;
    }
}
