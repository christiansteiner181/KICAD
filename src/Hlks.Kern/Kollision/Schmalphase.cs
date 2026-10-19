// Kollisionsprüfung (Modul 3b, B1): Schmalphase – exakte Prüfung zweier Prüfkörper, deren Hüllquader sich nahe kommen
// (Grundsatzentscheid 2). Ergebnis ist ein Kontakt mit Art, Abstand, Eindringtiefe und den beiden massgebenden Punkten.
//
//   Achskörper (Kapsel/Quader) gegen Netz: Kapsel über den Abstand Achse–Dreieck (minus Radius, exakt); Quader über die
//     Trennachsen (Quader um die Toleranz verkleinert = Durchdringung, vergrössert = Berührung), Abstand exakt über die
//     zwölf Quaderdreiecke. Enthaltensein: Abschnitte der Achse in geschlossenen Teilen des Netzes (Strahltest) – eine
//     Leitung ganz in einer Wand ist eine harte Kollision. Umgekehrt: alle Ecken des Netzes im Achskörper.
//   Netz gegen Netz: Dreieckspaare aus den beiden Bäumen, Dreieck–Dreieck nach Möller; ohne Durchdringung Enthaltensein über
//     den Strahltest einer Ecke, sonst kleinster Abstand (Verzweigen und Begrenzen über beide Bäume).
//   Kapsel gegen Kapsel: Abstand der Achsen minus Radien (exakt); Quader gegen Kapsel/Quader über das Quadernetz.
//
// Eindringtiefe (NÄHERUNG, Prüfliste): Sie soll zeigen, wie tief ein Körper im anderen steckt, und ist keine kleinste
// Trennverschiebung.
//   - Achskörper, Achse ausserhalb: grösster Wert von Ausdehnung(n) − Abstand(Achse, Dreieck) über die nahen Dreiecke
//     (Kapsel: r − Abstand, exakt für eine Ebene).
//   - Achskörper, Achse (teilweise) innen: Ausdehnung + grösster Abstand der inneren Achsstücke zur Oberfläche, abgetastet an
//     einer ungeraden Zahl Punkte je Stück (eine je angefangene 100 mm, höchstens neun, die Mitte immer dabei; dazu Achsenden,
//     die innen liegen) – Rohr senkrecht durch eine Wand der Dicke d: r + d/2.
//   - Netz gegen Netz: grösster Abstand zur Oberfläche des anderen Netzes über Ecken, die in ihm liegen, und über die
//     Mitten der inneren Stücke durchstossender Kanten (höchstens 256 Ecken bzw. 64 Dreieckspaare je Richtung); ohne
//     geschlossene Netze 0.
//   - Umschliessen (das Netz ganz im Achskörper): grösster Wert von Ausdehnung − Abstand einer Netzecke zur Achse.
// Abstände werden nur bis maxAbstand genau bestimmt; was weiter liegt, meldet Abstand ∞ (Art Getrennt).

namespace Hlks.Kern;

/// <summary>Wie zwei Prüfkörper zueinander liegen.</summary>
public enum Kontaktart
{
    /// <summary>Weiter auseinander als die Toleranz (Abstand gemessen bis maxAbstand, sonst ∞).</summary>
    Getrennt,
    /// <summary>Oberflächen näher als die Toleranz, ohne Durchdringung.</summary>
    Beruehrung,
    /// <summary>Die Körper durchdringen sich (harte Kollision).</summary>
    Schnitt,
    /// <summary>A liegt ganz in B (harte Kollision, z. B. Leitung in der Wand).</summary>
    Enthalten,
    /// <summary>B liegt ganz in A (harte Kollision).</summary>
    Umschliesst,
}

/// <summary>Ergebnis der Schmalphase. Abstand ≥ 0 (0 bei Durchdringung und Enthaltensein, ∞ wenn weiter als maxAbstand);
/// Eindringtiefe ≥ 0 (Näherung, siehe Dateikopf); PunktA auf/in A, PunktB auf/in B (bei Abstand: die nächsten Punkte, bei
/// Durchdringung: tiefster Punkt von A und nächster Oberflächenpunkt von B). AmEnde: der massgebende Punkt liegt hinter dem
/// Ende einer Leitungsachse (in der Halbkugel der Kapsel bzw. an der Endfläche des Quaders) – z. B. eine Leitung, die an
/// einem Gerät endet.</summary>
public sealed record Kontakt(Kontaktart Art, double Abstand, double Eindringtiefe, V3 PunktA, V3 PunktB, bool AmEnde = false)
{
    public static readonly Kontakt Fern = new(Kontaktart.Getrennt, double.PositiveInfinity, 0, default, default);

    /// <summary>Harte Kollision (Durchdringung oder Enthaltensein).</summary>
    public bool Hart => Art is Kontaktart.Schnitt or Kontaktart.Enthalten or Kontaktart.Umschliesst;

    /// <summary>Lage des Befunds (Mitte der beiden Punkte).</summary>
    public V3 Ort => (PunktA + PunktB) * 0.5;

    /// <summary>Derselbe Kontakt aus Sicht von B.</summary>
    public Kontakt Gespiegelt() => this with
    {
        Art = Art switch { Kontaktart.Enthalten => Kontaktart.Umschliesst, Kontaktart.Umschliesst => Kontaktart.Enthalten, _ => Art },
        PunktA = PunktB, PunktB = PunktA,
    };
}

public static class Schmalphase
{
    const double Tol = Geometrie3.Toleranz;
    const int EckenJeRichtung = 256;
    const int PaareFuerTiefe = 64;
    const int ProbenJeStueck = 9;

    // ------------------------------------------------------------------ Prüfkörper gegen Prüfkörper

    /// <summary>Prüft zwei Prüfkörper. maxAbstand: bis zu welchem Abstand er genau bestimmt wird (weiche Prüfung); 0 = nur
    /// Durchdringung und Berührung. mitDaemmungA/B: die zweite Hülle der Leitung (daemmdicke) statt des Rohrs.</summary>
    public static Kontakt Pruefen(Pruefkoerper a, Pruefkoerper b, double maxAbstand = 0, bool mitDaemmungA = false, bool mitDaemmungB = false)
    {
        var ha = mitDaemmungA ? a.Huelle : a.HuelleOhneDaemmung;
        var hb = mitDaemmungB ? b.Huelle : b.HuelleOhneDaemmung;
        if (!ha.Schneidet(hb, maxAbstand + Tol)) return Kontakt.Fern;
        if (a.IstLeitung && b.IstLeitung)
        {
            var kb = b.Huellkoerper(mitDaemmungB);
            return Leitung(a.Huellkoerper(mitDaemmungA).Select(x => Kombinieren(kb.Select(y => Pruefen(x, y, maxAbstand)), true)));
        }
        if (a.IstLeitung && b.Netz is { } nb) return Leitung(a.Huellkoerper(mitDaemmungA).Select(x => Pruefen(x, nb, maxAbstand)));
        if (b.IstLeitung && a.Netz is { } na) return Leitung(b.Huellkoerper(mitDaemmungB).Select(y => Pruefen(y, na, maxAbstand))).Gespiegelt();
        if (a.Netz is { } x1 && b.Netz is { } x2) return Pruefen(x1, x2, maxAbstand);
        return Kontakt.Fern;
    }

    /// <summary>Ergebnis einer Leitung aus den Ergebnissen ihrer Teilstücke: Enthalten nur, wenn alle Teilstücke enthalten sind;
    /// sonst der schwerste Befund (Umschliesst, Schnitt, Berührung, Getrennt), Abstand das Minimum, Tiefe das Maximum.</summary>
    static Kontakt Leitung(IEnumerable<Kontakt> teile) => Kombinieren(teile, false);

    static Kontakt Kombinieren(IEnumerable<Kontakt> teile, bool enthaltenGenuegtEins)
    {
        Kontakt? best = null;
        int enthalten = 0, anzahl = 0;
        double tiefe = 0;
        foreach (var k in teile)
        {
            anzahl++;
            if (k.Art == Kontaktart.Enthalten) enthalten++;
            if (k.Hart) tiefe = Math.Max(tiefe, k.Eindringtiefe);
            if (best is null || Rang(k) > Rang(best) || (Rang(k) == Rang(best) && k.Abstand < best.Abstand)) best = k;
        }
        if (best is null) return Kontakt.Fern;
        if (enthalten > 0)
        {
            bool alle = enthaltenGenuegtEins || enthalten == anzahl;
            if (alle && best.Art != Kontaktart.Umschliesst) return best with { Art = Kontaktart.Enthalten, Abstand = 0, Eindringtiefe = tiefe };
            if (!alle && best.Art == Kontaktart.Enthalten) return best with { Art = Kontaktart.Schnitt, Eindringtiefe = tiefe };
        }
        return best.Hart ? best with { Eindringtiefe = tiefe } : best;

        static int Rang(Kontakt k) => k.Art switch
        {
            Kontaktart.Umschliesst => 4, Kontaktart.Enthalten => 3, Kontaktart.Schnitt => 3, Kontaktart.Beruehrung => 1, _ => 0,
        };
    }

    // ------------------------------------------------------------------ Achskörper gegen Achskörper

    /// <summary>Leitung gegen Leitung (je ein Teilstück). Kapsel–Kapsel exakt über den Abstand der Achsen minus die Radien;
    /// mit einem Quader über dessen Netz.</summary>
    public static Kontakt Pruefen(Achskoerper a, Achskoerper b, double maxAbstand = 0)
    {
        if (!a.Huelle.Schneidet(b.Huelle, maxAbstand + Tol)) return Kontakt.Fern;
        if (b.Form == Querschnittsform.Rechteck) return Pruefen(a, b.AlsNetz(), maxAbstand);
        if (a.Form == Querschnittsform.Rechteck) return Pruefen(b, a.AlsNetz(), maxAbstand).Gespiegelt();
        double d = Geometrie3.SegmentSegment(a.A, a.B, b.A, b.B, out var s, out var t, out var ca, out var cb);
        double r = a.Radius + b.Radius;
        var n = d > 1e-12 ? (cb - ca) * (1 / d) : Quer(a.Richtung);
        var pa = ca + n * a.Radius; var pb = cb - n * b.Radius;
        bool amEnde = AmEnde(a, s, cb) || AmEnde(b, t, ca);
        // Enthaltensein: beide Enden der einen Achse näher als (R − r) an der anderen (der Abstand ist längs einer Strecke konvex)
        if (a.Radius <= b.Radius && Geometrie3.AbstandPunktSegment(a.A, b.A, b.B) <= b.Radius - a.Radius
            && Geometrie3.AbstandPunktSegment(a.B, b.A, b.B) <= b.Radius - a.Radius)
            return new Kontakt(Kontaktart.Enthalten, 0, r - d, pa, pb, amEnde);
        if (b.Radius <= a.Radius && Geometrie3.AbstandPunktSegment(b.A, a.A, a.B) <= a.Radius - b.Radius
            && Geometrie3.AbstandPunktSegment(b.B, a.A, a.B) <= a.Radius - b.Radius)
            return new Kontakt(Kontaktart.Umschliesst, 0, r - d, pa, pb, amEnde);
        if (d < r - Tol) return new Kontakt(Kontaktart.Schnitt, 0, r - d, pa, pb, amEnde);
        if (d <= r + Tol) return new Kontakt(Kontaktart.Beruehrung, Math.Max(0, d - r), Math.Max(0, r - d), pa, pb, amEnde);
        if (d - r > maxAbstand) return Kontakt.Fern;
        return new Kontakt(Kontaktart.Getrennt, d - r, 0, pa, pb, amEnde);
    }

    static V3 Quer(V3 d) => d.Senkrechte().E1;

    /// <summary>Liegt der Gegenpunkt hinter einem Achsende (Parameter am Ende und jenseits der Endebene)?</summary>
    static bool AmEnde(Achskoerper k, double s, V3 gegen) =>
        (s <= 1e-9 && (gegen - k.A).Mal(k.Richtung) < -Tol) || (s >= 1 - 1e-9 && (gegen - k.B).Mal(k.Richtung) > Tol);

    // ------------------------------------------------------------------ Achskörper gegen Netz

    /// <summary>Kapsel bzw. Quader gegen ein Netz.</summary>
    public static Kontakt Pruefen(Achskoerper k, Pruefnetz n, double maxAbstand = 0)
    {
        double reich = maxAbstand + Tol;
        if (!k.Huelle.Schneidet(n.Huelle, reich)) return Kontakt.Fern;
        var kand = new List<int>();
        n.Dreiecke(k.Huelle, reich, kand);

        // Abstand der Achse zu den nahen Dreiecken
        var achsAbstand = new (double D, V3 Pa, V3 Pt, double S)[kand.Count];
        int imin = -1;
        for (int i = 0; i < kand.Count; i++)
        {
            n.Dreieck(kand[i], out var x, out var y, out var z);
            double d = Geometrie3.SegmentDreieck(k.A, k.B, x, y, z, out var pa, out var pt, out var s);
            achsAbstand[i] = (d, pa, pt, s);
            if (imin < 0 || d < achsAbstand[imin].D) imin = i;
        }

        bool schnitt, beruehrt;
        double abstand = double.PositiveInfinity;
        V3 punktA = default, punktB = default; double sA = 0.5;
        if (k.Form == Querschnittsform.Rund)
        {
            if (imin >= 0)
            {
                var (d, pa, pt, s) = achsAbstand[imin];
                var dir = d > 1e-12 ? (pt - pa) * (1 / d) : n.Normale(kand[imin]);
                abstand = d - k.Radius; punktA = pa + dir * k.Radius; punktB = pt; sA = s;
            }
            schnitt = abstand < -Tol;
            beruehrt = abstand <= Tol;
        }
        else
        {
            schnitt = beruehrt = false;
            var m = k.Mitte;
            foreach (var t in kand)
            {
                n.Dreieck(t, out var x, out var y, out var z);
                if (!beruehrt && Geometrie3.QuaderDreieck(m, k.Richtung, k.RichtungB, k.RichtungH, k.Laenge / 2 + Tol, k.Breite / 2 + Tol, k.Hoehe / 2 + Tol, x, y, z)) beruehrt = true;
                if (Geometrie3.QuaderDreieck(m, k.Richtung, k.RichtungB, k.RichtungH, k.Laenge / 2 - Tol, k.Breite / 2 - Tol, k.Hoehe / 2 - Tol, x, y, z)) { schnitt = beruehrt = true; break; }
            }
            if (!beruehrt && kand.Count > 0 && maxAbstand > 0)
            {
                var q = k.AlsNetz();
                foreach (var t in kand)
                {
                    n.Dreieck(t, out var x, out var y, out var z);
                    for (int u = 0; u < q.AnzahlDreiecke; u++)
                    {
                        q.Dreieck(u, out var a0, out var a1, out var a2);
                        double d = Geometrie3.DreieckDreieckAbstand(a0, a1, a2, x, y, z, out var pa, out var pb);
                        if (d < abstand) { abstand = d; punktA = pa; punktB = pb; }
                    }
                }
                sA = Math.Clamp((punktA - k.A).Mal(k.Richtung) / Math.Max(k.Laenge, 1e-12), 0, 1);
            }
            else if (imin >= 0 && beruehrt)
            {
                // Berührung oder Durchdringung: Punkte aus der Achse (der genaue Ort folgt unten aus der Tiefe)
                var (d, pa, pt, s) = achsAbstand[imin];
                var dir = d > 1e-12 ? (pt - pa) * (1 / d) : n.Normale(kand[imin]);
                abstand = 0; punktA = pa + dir * k.Ausdehnung(dir); punktB = pt; sA = s;
            }
            // sonst: getrennt und maxAbstand = 0 – Abstand nicht gefragt (∞)
        }

        // Achse in geschlossenen Teilen des Netzes?
        var innen = n.InnenAbschnitte(k.A, k.B);   // leer ohne geschlossene Teile bzw. wenn die Achse am Netz vorbeigeht
        bool ganzInnen = innen.Count == 1 && innen[0].T0 <= 1e-9 && innen[0].T1 >= 1 - 1e-9;

        if (innen.Count > 0)
        {
            var (tiefe, pa, pb, s) = TiefeInnen(k, n, innen);
            bool ende = AmEnde(k, s, pb);
            if (ganzInnen && !schnitt) return new Kontakt(Kontaktart.Enthalten, 0, tiefe, pa, pb, ende);
            return new Kontakt(Kontaktart.Schnitt, 0, tiefe, pa, pb, ende);
        }

        // Netz ganz im Achskörper?
        if (!schnitt && k.Huelle.Umfasst(n.Huelle) && AlleEckenIn(k, n, out var tiefeU, out var ua, out var ub))
            return new Kontakt(Kontaktart.Umschliesst, 0, tiefeU, ua, ub, false);

        if (schnitt)
        {
            // grösste Ausdehnung − Abstand über die nahen Dreiecke
            double tiefe = 0; V3 pa = punktA, pb = punktB; double s = sA;
            for (int i = 0; i < kand.Count; i++)
            {
                var (d, a, t, si) = achsAbstand[i];
                var dir = d > 1e-12 ? (t - a) * (1 / d) : n.Normale(kand[i]);
                double w = k.Ausdehnung(dir) - d;
                if (w > tiefe) { tiefe = w; pa = a + dir * k.Ausdehnung(dir); pb = t; s = si; }
            }
            return new Kontakt(Kontaktart.Schnitt, 0, tiefe, pa, pb, AmEnde(k, s, pb));
        }
        if (beruehrt) return new Kontakt(Kontaktart.Beruehrung, Math.Max(0, abstand), Math.Max(0, -abstand), punktA, punktB, AmEnde(k, sA, punktB));
        if (double.IsInfinity(abstand) || abstand > maxAbstand) return Kontakt.Fern;
        return new Kontakt(Kontaktart.Getrennt, abstand, 0, punktA, punktB, AmEnde(k, sA, punktB));
    }

    /// <summary>Tiefe eines Achskörpers, dessen Achse innen liegt: Ausdehnung + grösster Abstand abgetasteter innerer Achspunkte
    /// zur Oberfläche.</summary>
    static (double Tiefe, V3 PunktA, V3 PunktB, double S) TiefeInnen(Achskoerper k, Pruefnetz n, List<(double T0, double T1)> innen)
    {
        double best = -1; V3 sBest = k.A, qBest = k.A; double tBest = 0;
        foreach (var (t0, t1) in innen)
        {
            double laenge = (t1 - t0) * k.Laenge;
            int proben = Math.Clamp((int)Math.Ceiling(laenge / 100), 1, ProbenJeStueck);
            if (proben % 2 == 0) proben++;   // ungerade: die Mitte des Stücks ist immer dabei
            for (int j = 0; j < proben; j++)
            {
                double t = t0 + (t1 - t0) * (j + 0.5) / proben;
                var s = k.A + (k.B - k.A) * t;
                var (d, q, _) = n.Naechster(s);
                if (d > best) { best = d; sBest = s; qBest = q; tBest = t; }
            }
            foreach (var t in new[] { t0, t1 })
            {
                if (t > 1e-9 && t < 1 - 1e-9) continue;   // Grenzen im Netz liegen auf der Oberfläche
                var s = k.A + (k.B - k.A) * t;
                var (d, q, _) = n.Naechster(s);
                if (d > best) { best = d; sBest = s; qBest = q; tBest = t; }
            }
        }
        var dir = (sBest - qBest).Laenge > 1e-12 ? (sBest - qBest).Einheit() : k.RichtungH;
        double aus = k.Ausdehnung(dir);
        return (Math.Max(0, best) + aus, sBest + dir * aus, qBest, tBest);
    }

    static bool AlleEckenIn(Achskoerper k, Pruefnetz n, out double tiefe, out V3 pa, out V3 pb)
    {
        tiefe = 0; pa = pb = k.A;
        bool eine = false;
        for (int t = 0; t < n.AnzahlDreiecke; t++)
        {
            var (i, j, l) = n.Indizes(t);
            foreach (var e in new[] { i, j, l })
            {
                var v = n.Ecke(e);
                if (!k.Enthaelt(v, Tol)) return false;
                var c = Geometrie3.NaechsterPunktSegment(v, k.A, k.B, out _);
                var dir = (v - c).Laenge > 1e-12 ? (v - c).Einheit() : k.RichtungH;
                double w = k.Ausdehnung(dir) - (v - c).Laenge;
                if (w > tiefe || !eine) { tiefe = w; pa = c + dir * k.Ausdehnung(dir); pb = v; eine = true; }
            }
        }
        return eine;
    }

    // ------------------------------------------------------------------ Netz gegen Netz

    /// <summary>Netz gegen Netz: Durchdringung über Dreieckspaare (Möller), sonst Enthaltensein über den Strahltest (geschlossene
    /// Teile), sonst Berührung bzw. kleinster Abstand.</summary>
    public static Kontakt Pruefen(Pruefnetz a, Pruefnetz b, double maxAbstand = 0)
    {
        double reich = maxAbstand + Tol;
        if (!a.Huelle.Schneidet(b.Huelle, reich)) return Kontakt.Fern;
        var paare = a.Index.Paare(b.Index, Tol);
        var schnittPaare = new List<(int A, int B)>();
        V3 summe = default; int punkte = 0;
        bool beruehrt = false; V3 bA = default, bB = default;
        foreach (var (ea, eb) in paare)
        {
            int ta = a.DreieckVon(ea), tb = b.DreieckVon(eb);
            a.Dreieck(ta, out var a0, out var a1, out var a2);
            b.Dreieck(tb, out var b0, out var b1, out var b2);
            var lage = Geometrie3.DreieckDreieck(a0, a1, a2, b0, b1, b2, Tol, out var s0, out var s1);
            if (lage == DreieckLage.Schneidet)
            {
                schnittPaare.Add((ta, tb));
                if (punkte < 64) { summe += (s0 + s1) * 0.5; punkte++; }
            }
            else if (lage == DreieckLage.Beruehrt && !beruehrt) { beruehrt = true; bA = s0; bB = s1; }
        }
        if (schnittPaare.Count > 0)
        {
            var ort = summe * (1.0 / punkte);
            var (tiefe, pa, pb) = TiefeNetze(a, b, schnittPaare);
            return tiefe > 0 ? new Kontakt(Kontaktart.Schnitt, 0, tiefe, pa, pb) : new Kontakt(Kontaktart.Schnitt, 0, 0, ort, ort);
        }
        // ohne Durchdringung: ganz innen oder ganz aussen
        if (b.Huelle.Umfasst(a.Huelle, Tol) && b.HatGeschlosseneTeile && EineEckeInnen(a, b) is { } inB)
            return new Kontakt(Kontaktart.Enthalten, 0, inB.Tiefe, inB.Ecke, inB.Fuss);
        if (a.Huelle.Umfasst(b.Huelle, Tol) && a.HatGeschlosseneTeile && EineEckeInnen(b, a) is { } inA)
            return new Kontakt(Kontaktart.Umschliesst, 0, inA.Tiefe, inA.Fuss, inA.Ecke);
        if (beruehrt) return new Kontakt(Kontaktart.Beruehrung, 0, 0, bA, bB);
        if (maxAbstand <= 0) return Kontakt.Fern;
        var (x, y, w) = a.Index.NaechstesPaar(b.Index, (i, j) =>
        {
            a.Dreieck(a.DreieckVon(i), out var a0, out var a1, out var a2);
            b.Dreieck(b.DreieckVon(j), out var b0, out var b1, out var b2);
            return Geometrie3.DreieckDreieckAbstand(a0, a1, a2, b0, b1, b2, out _, out _);
        }, reich);
        if (x < 0 || w > maxAbstand) return Kontakt.Fern;
        a.Dreieck(a.DreieckVon(x), out var p0, out var p1, out var p2);
        b.Dreieck(b.DreieckVon(y), out var q0, out var q1, out var q2);
        double dd = Geometrie3.DreieckDreieckAbstand(p0, p1, p2, q0, q1, q2, out var na, out var nb);
        return dd <= Tol ? new Kontakt(Kontaktart.Beruehrung, dd, 0, na, nb) : new Kontakt(Kontaktart.Getrennt, dd, 0, na, nb);
    }

    /// <summary>Liegt das Netz x (ohne Durchdringung) in y? Entschieden an der ersten Ecke von x, die nicht auf der Oberfläche
    /// von y liegt (höchstens 16 Ecken). Rückgabe mit Tiefe (Abstand dieser Ecke zur Oberfläche) oder null.</summary>
    static (double Tiefe, V3 Ecke, V3 Fuss)? EineEckeInnen(Pruefnetz x, Pruefnetz y)
    {
        int n = x.AnzahlEcken, schritt = Math.Max(1, n / 16);
        for (int i = 0; i < n; i += schritt)
        {
            var v = x.Ecke(i);
            var lage = y.Lage(v);
            if (lage == Punktlage.Flaeche) continue;
            if (lage == Punktlage.Aussen) return null;
            var (d, q, _) = y.Naechster(v);
            return (d, v, q);
        }
        return null;
    }

    /// <summary>Näherung der Eindringtiefe zweier sich durchdringender Netze (siehe Dateikopf).</summary>
    static (double Tiefe, V3 PunktA, V3 PunktB) TiefeNetze(Pruefnetz a, Pruefnetz b, List<(int A, int B)> schnittPaare)
    {
        double best = 0; V3 pa = default, pb = default;
        var gemeinsam = new Huelle3(
            new V3(Math.Max(a.Huelle.Min.X, b.Huelle.Min.X), Math.Max(a.Huelle.Min.Y, b.Huelle.Min.Y), Math.Max(a.Huelle.Min.Z, b.Huelle.Min.Z)),
            new V3(Math.Min(a.Huelle.Max.X, b.Huelle.Max.X), Math.Min(a.Huelle.Max.Y, b.Huelle.Max.Y), Math.Min(a.Huelle.Max.Z, b.Huelle.Max.Z))).Erweitert(Tol);
        void Ecken(Pruefnetz x, Pruefnetz y, bool xIstA)
        {
            if (!y.HatGeschlosseneTeile) return;
            var liste = new List<int>();
            for (int i = 0; i < x.AnzahlEcken; i++) if (gemeinsam.Enthaelt(x.Ecke(i))) liste.Add(i);
            int schritt = Math.Max(1, liste.Count / EckenJeRichtung);
            for (int i = 0; i < liste.Count; i += schritt)
            {
                var v = x.Ecke(liste[i]);
                if (y.Lage(v) != Punktlage.Innen) continue;
                var (d, q, _) = y.Naechster(v);
                if (d > best) { best = d; (pa, pb) = xIstA ? (v, q) : (q, v); }
            }
        }
        void Kanten(Pruefnetz x, Pruefnetz y, bool xIstA)
        {
            if (!y.HatGeschlosseneTeile) return;
            var gesehen = new HashSet<(int, int)>();
            int zahl = 0;
            foreach (var (ta, tb) in schnittPaare)
            {
                if (zahl++ >= PaareFuerTiefe) break;
                int tx = xIstA ? ta : tb, ty = xIstA ? tb : ta;
                var (i, j, k) = x.Indizes(tx);
                y.Dreieck(ty, out var y0, out var y1, out var y2);
                foreach (var (u, v) in new[] { (i, j), (j, k), (k, i) })
                {
                    if (!gesehen.Add(u < v ? (u, v) : (v, u))) continue;
                    var p = x.Ecke(u); var q = x.Ecke(v);
                    if (!Geometrie3.SegmentDreieckSchnitt(p, q, y0, y1, y2, out _)) continue;
                    foreach (var (t0, t1) in y.InnenAbschnitte(p, q))
                    {
                        var m = p + (q - p) * ((t0 + t1) / 2);
                        var (d, f, _) = y.Naechster(m);
                        if (d > best) { best = d; (pa, pb) = xIstA ? (m, f) : (f, m); }
                    }
                }
            }
        }
        Ecken(a, b, true); Ecken(b, a, false);
        Kanten(a, b, true); Kanten(b, a, false);
        return (best, pa, pb);
    }
}
