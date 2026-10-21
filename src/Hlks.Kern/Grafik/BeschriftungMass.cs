// Bemassung (Masskette) und Kote der Plangrafik. Quelle: SIA 400:2000 Ziff. B.5.3 (Masslinien) und B.5.4 (Koten),
// siehe entwurf/modul-1a.md «Darstellung – Regeln mit Quelle».
//
// Aus der Norm: Masslinie dünnste Linie; Massstriche doppelt so dick (Schrägstrich 45°); Masszahl über der Linie, von
// unten bzw. rechts lesbar; Koten in m, Format «+2.85», «±0.00», «−2.60».
// ANNAHMEN (nicht aus der Norm, Prüfliste Christian), alle in Papier-mm:
//   Hilfslinie: Abstand zum Messpunkt 1 mm, Überstand über die Masslinie 2 mm
//   Massstrich: Länge 2.5 mm; Masszahl: Schrift 2.0 mm, Abstand Grundlinie–Masslinie 0.8 mm
//   Vorgabe-Versatz der Masslinie ohne Angabe: 10 mm
//   Masslinie von horizontal/vertikal liegt auf der Linie durch den ersten Messpunkt (plus versatz)
//   Kotensymbol: gleichschenkliges Dreieck, Spitze auf dem Punkt, Basis oben 3 mm breit, 2.5 mm hoch; Schrift 2.0 mm

using System.Globalization;

namespace Hlks.Kern;

internal static class BeschriftungMass
{
    // Annahmen siehe Dateikopf
    const double HilfAbstand = 1.0;
    const double HilfUeberstand = 2.0;
    const double StrichLaenge = 2.5;
    const double ZahlGroesse = 2.0;
    const double ZahlAbstand = 0.8;
    const double VorgabeVersatz = 10.0;
    const double KoteBreite = 3.0;
    const double KoteHoehe = 2.5;
    const double KoteSchrift = 2.0;

    // ------------------------------------------------------------------------------------------ Bemassung

    public static IEnumerable<Grafikteil> Bemassung(GrafikKontext ctx, Element e)
    {
        var pts = BeschriftungHilfen.Punkte2D(e.Geometrie!);
        if (pts.Count < 2) return [];
        var p0 = pts[0];

        // Messrichtung d und Normale n (links der Messrichtung)
        var ziel = pts[^1];
        if (BeschriftungHilfen.Laenge(BeschriftungHilfen.Minus(ziel, p0)) < 1e-9)
            ziel = pts.MaxBy(p => BeschriftungHilfen.Laenge(BeschriftungHilfen.Minus(p, p0)))!;
        var v = BeschriftungHilfen.Minus(ziel, p0);
        // N9: horizontal/vertikal in den Achsen der Ansicht (gedrehter Grundriss); ohne Drehung genau wie bisher
        double[] Achse(double grad)
        {
            var a = BeschriftungHilfen.Drehen([1, 0], grad - ctx.Drehung);
            return BeschriftungHilfen.Punktprodukt(v, a) >= 0 ? a : [-a[0], -a[1]];
        }
        double[] d = (e.Text("richtung") ?? "ausgerichtet") switch
        {
            "horizontal" => ctx.Drehung == 0 ? [v[0] >= 0 ? 1 : -1, 0] : Achse(0),
            "vertikal" => ctx.Drehung == 0 ? [0, v[1] >= 0 ? 1 : -1] : Achse(90),
            _ => BeschriftungHilfen.Einheit(v),
        };
        var n = BeschriftungHilfen.Links(d);

        double versatz = e.Zahl("versatz") ?? ctx.AufModell(VorgabeVersatz);
        double abstand = ctx.AufModell(HilfAbstand), ueberstand = ctx.AufModell(HilfUeberstand);

        // Lage jedes Messpunkts: s entlang der Messrichtung, t quer dazu (beides ab dem ersten Messpunkt)
        var s = pts.Select(p => BeschriftungHilfen.Punktprodukt(BeschriftungHilfen.Minus(p, p0), d)).ToList();
        var t = pts.Select(p => BeschriftungHilfen.Punktprodukt(BeschriftungHilfen.Minus(p, p0), n)).ToList();
        double[] AufLinie(double si) => BeschriftungHilfen.Plus(BeschriftungHilfen.Plus(p0, BeschriftungHilfen.Mal(d, si)), BeschriftungHilfen.Mal(n, versatz));

        var prim = new List<Primitiv>();

        // Hilfslinien dünn: von knapp neben dem Messpunkt bis knapp über die Masslinie
        for (int i = 0; i < pts.Count; i++)
        {
            double luecke = versatz - t[i];
            if (Math.Abs(luecke) <= abstand) continue;   // Masslinie (fast) auf dem Messpunkt: keine Hilfslinie
            double vz = Math.Sign(luecke);
            var von = BeschriftungHilfen.Plus(pts[i], BeschriftungHilfen.Mal(n, vz * abstand));
            var bis = BeschriftungHilfen.Plus(AufLinie(s[i]), BeschriftungHilfen.Mal(n, vz * ueberstand));
            prim.Add(BeschriftungHilfen.Linie(von, bis, Stifte.Duenn));
        }

        // Masslinie dünn von der ersten bis zur letzten Lage
        double smin = s.Min(), smax = s.Max();
        prim.Add(BeschriftungHilfen.Linie(AufLinie(smin), AufLinie(smax), Stifte.Duenn));

        // Massstriche: Schrägstrich 45° zur Masslinie, doppelt so dick wie die Masslinie (mittel), je Messpunkt
        var strichRichtung = BeschriftungHilfen.Drehen(d, 45);
        double halb = ctx.AufModell(StrichLaenge) / 2;
        foreach (var si in s)
        {
            var m = AufLinie(si);
            prim.Add(BeschriftungHilfen.Linie(
                BeschriftungHilfen.Minus(m, BeschriftungHilfen.Mal(strichRichtung, halb)),
                BeschriftungHilfen.Plus(m, BeschriftungHilfen.Mal(strichRichtung, halb)), Stifte.Mittel));
        }

        // Masszahlen: eine je Abschnitt, mittig über der Masslinie, von unten bzw. rechts lesbar
        double winkel = BeschriftungHilfen.LesbarerWinkel(Math.Atan2(d[1], d[0]) * 180 / Math.PI);
        var oben = BeschriftungHilfen.Drehen([0, 1], winkel);
        string einheit = Bemassungseinheit.Fuer(ctx.Modell, ctx.Ansicht, e).Einheit;   // Masskette › Ansicht › Projekt › Gewerk
        var ersatz = e.Text("text");
        for (int i = 1; i < pts.Count; i++)
        {
            double laenge = Math.Abs(s[i] - s[i - 1]);
            if (laenge < 1e-6) continue;
            string text = pts.Count == 2 && !string.IsNullOrEmpty(ersatz) ? ersatz : MassText(laenge, einheit);
            var mitte = BeschriftungHilfen.Mal(BeschriftungHilfen.Plus(AufLinie(s[i - 1]), AufLinie(s[i])), 0.5);
            var pos = BeschriftungHilfen.Plus(mitte, BeschriftungHilfen.Mal(oben, ctx.AufModell(ZahlAbstand)));
            prim.Add(BeschriftungHilfen.Text(pos, text, ZahlGroesse, "mitte", winkel).Mitdrehend());   // N9: folgt der Masslinie
        }

        return [new Grafikteil
        {
            Id = e.Id, Ebene = "beschriftung", Primitive = prim,
            Fangpunkte = pts.Select(p => (double[])p.Clone()).ToList(),
        }];
    }

    /// <summary>Masszahl für eine Länge in mm: m mit zwei Dezimalen, cm ganz oder mit einer Dezimale, mm ganz.</summary>
    public static string MassText(double mm, string einheit)
    {
        switch (einheit)
        {
            case "cm":
                double cm = Math.Round(mm / 10, 1, MidpointRounding.AwayFromZero);
                return Grafik.Format(cm, Math.Abs(cm - Math.Round(cm)) < 1e-9 ? 0 : 1);
            case "mm":
                return Grafik.Format(Math.Round(mm, MidpointRounding.AwayFromZero), 0);
            default:
                return Grafik.Format(mm / 1000, 2);
        }
    }

    // ------------------------------------------------------------------------------------------ Kote

    public static IEnumerable<Grafikteil> Kote(GrafikKontext ctx, Element e)
    {
        var p = e.Geometrie!.Punkte[0];
        // Wert: fester Wert oder Höhe des Punkts (Grundriss z, Schnitt/Ansicht die zweite Koordinate v)
        double hoehe = e.Zahl("wert") ?? (ctx.Art == "grundriss" ? (p.Length > 2 ? p[2] : 0) : p[1]);
        string text = KoteText(hoehe, e.Text("zusatz"));

        double b = ctx.AufModell(KoteBreite) / 2, h = ctx.AufModell(KoteHoehe);
        var spitze = new[] { p[0], p[1] };
        var dreieck = BeschriftungHilfen.Polygon([spitze, [p[0] + b, p[1] + h], [p[0] - b, p[1] + h]], Stifte.Duenn);
        var schrift = BeschriftungHilfen.Text([p[0] + b + ctx.AufModell(0.8), p[1] + h - ctx.AufModell(KoteSchrift * 0.7)],
            text, KoteSchrift, "links");
        return [new Grafikteil
        {
            Id = e.Id, Ebene = "beschriftung", Primitive = [dreieck.Verankert(spitze), schrift.Verankert(spitze)],   // N9: Kote bleibt waagrecht
            Fangpunkte = [[p[0], p[1]]],
        }];
    }

    /// <summary>Kotentext nach SIA 400:2000 Ziff. B.5.4: Höhe in mm → m mit Vorzeichen, «±0.00», «+2.85», «−2.60»
    /// (echtes Minuszeichen U+2212); <paramref name="zusatz"/> davor, durch ein Leerzeichen getrennt.</summary>
    public static string KoteText(double hoeheMm, string? zusatz = null)
    {
        decimal m = Math.Round((decimal)hoeheMm / 1000m, 2, MidpointRounding.AwayFromZero);
        string betrag = Math.Abs(m).ToString("F2", CultureInfo.InvariantCulture);
        string wert = m == 0 ? "±" + betrag : m > 0 ? "+" + betrag : "−" + betrag;
        return string.IsNullOrWhiteSpace(zusatz) ? wert : zusatz.Trim() + " " + wert;
    }
}
