// Kollisionsprüfung (Modul 3b, B1): Durchgänge einer Leitungsachse durch ein Bauteil – Grundlage der Aussparungen (B3) –,
// Projektion des Leitungsquerschnitts auf die Bauteilebene und die Prüfung, ob ein Bauteil an einer Stelle offen ist
// (Grundsatzentscheid 8: Abgleich mit einer neuen Architekten-Fassung).
//
// Durchgang: ein zusammenhängendes Stück der Achse im Bauteil (geschlossene Teile seines Netzes, Strahltest wie
//   Pruefnetz.InnenAbschnitte; innere Trennflächen, z. B. zwischen Wandschichten, trennen nicht). Ein- und Austrittspunkt mit
//   Parameter t und Flächennormale (nach aussen: beim Eintritt gegen die Achsrichtung, beim Austritt mit ihr). Tiefe = Länge
//   der Achse im Bauteil; Dicke = Tiefe · |cos| zur Eintrittsnormalen (Bauteildicke senkrecht zur Eintrittsfläche, gleich
//   der Bauteildicke bei parallelen Flächen); Winkel = Winkel zwischen Achse und Eintrittsfläche (90° = senkrecht).
// Projektion: Ebene durch die Mitte des Durchgangs mit der Eintrittsnormalen. Der Querschnitt (rund d bzw. rechteckig b × h,
//   wahlweise mit Zuschlag ringsum, z. B. Dämmung oder Spiel) wird längs der Achse auf die Ebene projiziert: rund schräg =
//   Ellipse (Halbachsen R/cos θ längs der Achse in der Ebene und R quer), rechteckig = Parallelogramm. Ausgegeben werden das
//   umschliessende Rechteck in den Ebenenachsen U, V und der umschliessende Kreis – einmal für den Querschnitt in der Fläche
//   und einmal für den ganzen Durchbruch (Querschnitt über die Tiefe verschoben: Hülle von Ein- und Austritt, senkrecht auf die
//   Ebene gelegt), wie ihn eine rechtwinklig zum Bauteil ausgeführte Aussparung umfassen muss.
//   Ebenenachsen ohne Vorgabe: steile Flächen (|n_z| < 0.5, Wände) U waagrecht, V nach oben; flache Flächen (Decken) U und V
//   aus der Projektion von x und y. ANNAHME (Prüfliste): nicht parallele Ein- und Austrittsflächen werden wie parallele zur
//   Eintrittsfläche behandelt.
// Öffnung: exakt, ob der Zylinder bzw. Quader der Öffnung (Länge entlang der Achse, Querschnitt quer dazu, um den Rand
//   verkleinert) das Netz des Bauteils berührt; dazu als Mass der Anteil freier Strahlen eines Bündels entlang der Achse
//   (rund 1 + 8 + 16 + 24 Strahlen auf drei Ringen, rechteckig 7 × 7).

namespace Hlks.Kern;

/// <summary>Ein Durchgang einer Achse a–b durch ein Bauteil. Teilstück = Index des Leitungsteilstücks (0 bei einer Strecke).
/// Normalen sind null, wenn die Achse im Bauteil beginnt bzw. endet.</summary>
public sealed record Durchgang(
    int Teilstueck, double T0, double T1, V3 Eintritt, V3 Austritt, V3? NormaleEin, V3? NormaleAus,
    bool BeginntInnen, bool EndetInnen, double Tiefe, double Dicke, double Winkel, V3 Mitte, V3 Richtung);

/// <summary>Projektion eines Leitungsquerschnitts auf die Bauteilebene (mm). AchseU/AchseV spannen die Ebene durch die Mitte
/// auf (Normale = Eintrittsnormale). Breite/Hoehe: umschliessendes Rechteck des Querschnitts in der Fläche entlang U bzw. V;
/// Kreis: Durchmesser des umschliessenden Kreises; Rund: runder Querschnitt und senkrechte Querung (Kreis = Querschnitt).
/// HalbachseA/B, RichtungA: Ellipse (nur rund; A längs der Achsprojektion). Durchbruch*: dasselbe für die Hülle über die
/// ganze Tiefe.</summary>
public sealed record Querschnittsprojektion(
    V3 Mitte, V3 Normale, V3 AchseU, V3 AchseV, double Breite, double Hoehe, double Kreis, bool Rund,
    double HalbachseA, double HalbachseB, V3 RichtungA, double DurchbruchBreite, double DurchbruchHoehe, double DurchbruchKreis);

/// <summary>Ergebnis der Öffnungsprüfung: Offen = der Körper der Öffnung berührt das Bauteil nicht (exakt); AnteilFrei = Anteil
/// der Strahlen des Bündels, die das Bauteil nicht treffen; Treffer = ein Punkt des störenden Bauteils (sonst null).</summary>
public sealed record Oeffnungsbefund(bool Offen, double AnteilFrei, int Strahlen, V3? Treffer);

public static class Durchgaenge
{
    const double Tol = Geometrie3.Toleranz;

    /// <summary>Durchgänge der Strecke a–b durch das Netz eines Bauteils (nur geschlossene Teile).</summary>
    public static List<Durchgang> Finden(Pruefnetz bauteil, V3 a, V3 b, int teilstueck = 0)
    {
        var aus = new List<Durchgang>();
        var r = b - a;
        double l = r.Laenge;
        if (l <= 1e-9) return aus;
        var d = r * (1 / l);
        var stuecke = bauteil.InnenAbschnitte(a, b, out var gruppen);
        foreach (var (t0, t1) in stuecke)
        {
            bool beginnt = t0 <= 1e-12, endet = t1 >= 1 - 1e-12;
            V3? nein = beginnt ? null : Normale(gruppen, t0, l, d, eintritt: true);
            V3? naus = endet ? null : Normale(gruppen, t1, l, d, eintritt: false);
            double tiefe = (t1 - t0) * l;
            double cos = nein is V3 ne ? Math.Abs(ne.Mal(d)) : naus is V3 na ? Math.Abs(na.Mal(d)) : 1;
            double winkel = Math.Asin(Math.Clamp(cos, 0, 1)) * 180 / Math.PI;
            aus.Add(new Durchgang(teilstueck, t0, t1, a + r * t0, a + r * t1, nein, naus, beginnt, endet, tiefe, tiefe * cos, winkel,
                a + r * ((t0 + t1) / 2), d));
        }
        return aus;
    }

    /// <summary>Durchgänge einer Leitung (alle Teilstücke ihrer Achse) durch ein Bauteil mit Netz.</summary>
    public static List<Durchgang> Finden(Pruefkoerper leitung, Pruefkoerper bauteil)
    {
        var aus = new List<Durchgang>();
        if (bauteil.Netz is not { } n || leitung.Achse.Count < 2) return aus;
        for (int i = 1; i < leitung.Achse.Count; i++)
        {
            var a = leitung.Achse[i - 1]; var b = leitung.Achse[i];
            if (!Huelle3.Aus(a, b).Schneidet(n.Huelle, Tol)) continue;
            aus.AddRange(Finden(n, a, b, i - 1));
        }
        return aus;
    }

    /// <summary>Gemittelte, nach aussen gerichtete Normale der Durchstosspunkte an der Grenze t (Eintritt: n·d &lt; 0).</summary>
    static V3? Normale(List<List<Treffer3>> gruppen, double t, double l, V3 d, bool eintritt)
    {
        List<Treffer3>? g = null; double best = double.MaxValue;
        foreach (var x in gruppen)
        {
            double tm = x.Average(y => y.T);
            if (Math.Abs(tm - t) < best) { best = Math.Abs(tm - t); g = x; }
        }
        if (g is null || best * l > 2 * Tol) return null;
        V3 summe = default;
        foreach (var x in g)
        {
            var n = x.Normale;
            double s = n.Mal(d);
            if ((eintritt && s > 0) || (!eintritt && s < 0)) n = -n;
            summe += n;
        }
        return summe.Laenge > 1e-12 ? summe.Einheit() : g[0].Normale;
    }

    // ------------------------------------------------------------------ Projektion

    /// <summary>Projektion des Querschnitts (Form und Masse des Achskörpers, ringsum + zuschlag) auf die Ebene des Durchgangs.
    /// achseU: gewünschte Richtung U in der Ebene (wird in die Ebene projiziert); ohne Vorgabe siehe Dateikopf.</summary>
    public static Querschnittsprojektion Projektion(Durchgang g, Achskoerper querschnitt, double zuschlag = 0, V3? achseU = null)
    {
        var n = g.NormaleEin ?? (g.NormaleAus is V3 na ? -na : -g.Richtung);
        var d = g.Richtung;
        var (u, v) = Ebenenachsen(n, achseU);
        double cos = Math.Max(Math.Abs(d.Mal(n)), 1e-6);
        var schiebung = (d - n * d.Mal(n)) * (g.Tiefe / 2);   // Verschiebung über die halbe Tiefe in der Ebene
        double hu, hv, kreis, ha = 0, hb = 0; V3 ra = u; bool rund;
        if (querschnitt.Form == Querschnittsform.Rund)
        {
            double r = querschnitt.Radius + zuschlag;
            var inEbene = d - n * d.Mal(n);
            if (inEbene.Laenge < 1e-9) { ra = u; ha = hb = r; }
            else { ra = inEbene.Einheit(); ha = r / cos; hb = r; }
            var rb = n.Kreuz(ra);
            double Halb(V3 e) => Math.Sqrt(Math.Pow(ha * ra.Mal(e), 2) + Math.Pow(hb * rb.Mal(e), 2));
            hu = Halb(u); hv = Halb(v); kreis = 2 * ha;
            rund = ha - hb <= Tol;
        }
        else
        {
            double bb = querschnitt.Breite + 2 * zuschlag, hh = querschnitt.Hoehe + 2 * zuschlag;
            double dn = d.Mal(n);
            if (Math.Abs(dn) < 1e-6) dn = dn < 0 ? -1e-6 : 1e-6;
            V3 P(V3 w) => w - d * (w.Mal(n) / dn);   // schräge Projektion längs der Achse auf die Ebene
            var pb = P(querschnitt.RichtungB) * (bb / 2); var ph = P(querschnitt.RichtungH) * (hh / 2);
            double Halb(V3 e) => Math.Abs(pb.Mal(e)) + Math.Abs(ph.Mal(e));
            hu = Halb(u); hv = Halb(v);
            kreis = 2 * Math.Max((pb + ph).Laenge, (pb - ph).Laenge);
            rund = false;
        }
        return new Querschnittsprojektion(g.Mitte, n, u, v, 2 * hu, 2 * hv, kreis, rund, ha, hb, ra,
            2 * (hu + Math.Abs(schiebung.Mal(u))), 2 * (hv + Math.Abs(schiebung.Mal(v))), kreis + 2 * schiebung.Laenge);
    }

    /// <summary>Achsen U, V der Ebene mit Normale n (V = n × U bzw. nach oben gerichtet), unabhängig davon, von welcher Seite
    /// die Leitung eintritt.</summary>
    public static (V3 U, V3 V) Ebenenachsen(V3 n, V3? vorgabe = null)
    {
        n = n.Einheit();
        if (vorgabe is V3 w)
        {
            var u0 = w - n * w.Mal(n);
            if (u0.Laenge > 1e-9) { var uu = u0.Einheit(); return (uu, n.Kreuz(uu)); }
        }
        if (Math.Abs(n.Z) < 0.5)
        {
            var u = new V3(0, 0, 1).Kreuz(n).Einheit();
            if (u.X < -1e-9 || (Math.Abs(u.X) <= 1e-9 && u.Y < 0)) u = -u;
            var v = n.Kreuz(u);
            if (v.Z < 0) v = -v;
            return (u, v.Einheit());
        }
        var ux = (new V3(1, 0, 0) - n * n.X).Einheit();
        var vy = new V3(0, 1, 0) - n * n.Y; vy = (vy - ux * vy.Mal(ux)).Einheit();
        return (ux, vy);
    }

    // ------------------------------------------------------------------ Öffnung prüfen

    /// <summary>Ist das Bauteil im Zylinder (Mitte, Richtung, Länge entlang der Achse, Durchmesser quer dazu) offen? Der
    /// Zylinder wird ringsum um <paramref name="rand"/> verkleinert, damit eine gleich grosse Öffnung als offen gilt.</summary>
    public static Oeffnungsbefund OeffnungRund(Pruefnetz bauteil, V3 mitte, V3 richtung, double laenge, double durchmesser, double rand = Tol)
    {
        var d = richtung.Einheit();
        var p0 = mitte - d * (laenge / 2); var p1 = mitte + d * (laenge / 2);
        double r = durchmesser / 2 - rand;
        if (r <= 0) return new Oeffnungsbefund(true, 1, 0, null);
        var kand = new List<int>();
        bauteil.Dreiecke(Huelle3.Aus(p0, p1).Erweitert(r), 0, kand);
        V3? treffer = null;
        foreach (var t in kand)
        {
            bauteil.Dreieck(t, out var a, out var b, out var c);
            if (Geometrie3.ZylinderDreieck(p0, p1, r, a, b, c)) { treffer = Geometrie3.NaechsterPunktDreieck(mitte, a, b, c); break; }
        }
        var (e1, e2) = d.Senkrechte();
        var versatz = new List<V3> { default };
        for (int ring = 1; ring <= 3; ring++)
            for (int k = 0; k < 8 * ring; k++)
            {
                double w = 2 * Math.PI * k / (8 * ring), rr = r * ring / 3;
                versatz.Add(e1 * (rr * Math.Cos(w)) + e2 * (rr * Math.Sin(w)));
            }
        return Buendel(bauteil, p0, p1, versatz, treffer);
    }

    /// <summary>Ist das Bauteil im Quader (Mitte, Richtung, Länge entlang der Achse, Breite entlang richtungB, Höhe quer dazu)
    /// offen? Der Querschnitt wird ringsum um <paramref name="rand"/> verkleinert.</summary>
    public static Oeffnungsbefund OeffnungRechteck(Pruefnetz bauteil, V3 mitte, V3 richtung, double laenge, double breite, double hoehe, V3 richtungB, double rand = Tol)
    {
        var d = richtung.Einheit();
        var eb = (richtungB - d * richtungB.Mal(d));
        eb = eb.Laenge > 1e-9 ? eb.Einheit() : d.Senkrechte().E1;
        var eh = d.Kreuz(eb);
        double hb = breite / 2 - rand, hh = hoehe / 2 - rand;
        if (hb <= 0 || hh <= 0) return new Oeffnungsbefund(true, 1, 0, null);
        var p0 = mitte - d * (laenge / 2); var p1 = mitte + d * (laenge / 2);
        var kand = new List<int>();
        var huelle = Huelle3.Leer;
        foreach (var s in new[] { -1.0, 1.0 })
            foreach (var t in new[] { -1.0, 1.0 })
            {
                var q = eb * (s * hb) + eh * (t * hh);
                huelle = huelle.Mit(p0 + q).Mit(p1 + q);
            }
        bauteil.Dreiecke(huelle, 0, kand);
        V3? treffer = null;
        foreach (var t in kand)
        {
            bauteil.Dreieck(t, out var a, out var b, out var c);
            if (Geometrie3.QuaderDreieck(mitte, d, eb, eh, laenge / 2, hb, hh, a, b, c)) { treffer = Geometrie3.NaechsterPunktDreieck(mitte, a, b, c); break; }
        }
        var versatz = new List<V3>();
        for (int i = 0; i < 7; i++)
            for (int j = 0; j < 7; j++)
                versatz.Add(eb * (hb * (i / 3.0 - 1)) + eh * (hh * (j / 3.0 - 1)));
        return Buendel(bauteil, p0, p1, versatz, treffer);
    }

    /// <summary>Offen nach dem exakten Test; Anteil freier Strahlen als Mass dazu.</summary>
    static Oeffnungsbefund Buendel(Pruefnetz bauteil, V3 p0, V3 p1, List<V3> versatz, V3? exakt)
    {
        int frei = 0;
        V3? strahl = null;
        foreach (var o in versatz)
        {
            var t = bauteil.Treffer(p0 + o, p1 + o);
            if (t.Count == 0) frei++;
            else strahl ??= t[0].Punkt;
        }
        return new Oeffnungsbefund(exakt is null, versatz.Count == 0 ? 1 : (double)frei / versatz.Count, versatz.Count, exakt ?? strahl);
    }
}
