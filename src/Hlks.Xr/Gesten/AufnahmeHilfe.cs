// Quest App V1b-1 (Modul XR, Gestenlabor): Hilfe beim Vorzeigen einer Geste. Start und Ende einer vorgezeigten Geste aus der Bewegungsenergie (Ruhe vorher und
// nachher), Zuschneiden einer Rohaufnahme, Rückmeldung «zu kurz», «zu lang», «Hand verloren» usw. Rein, ohne StereoKit.
//
// Bewegungsenergie je Hand und Bild (m/s): die Rohwerte (RohFolge) werden über GlaettungS gemittelt (gleitendes Mittel, nur verfolgte Bilder); die Energie im Bild i
// ist die Änderung zwischen dem Mittel um T(i) − GlaettungS/2 und dem um T(i) + GlaettungS/2, geteilt durch die Zeit dazwischen:
//   Tempo der Handflächenmitte + EnergieKruemmung × mittlere Änderung der Krümmung + EnergieDrehung × Drehwinkel der Normale + EnergiePinch × Pinch-Änderung.
// So bewegt auch eine Faust, die sich an Ort schliesst, und das Zittern einer ruhenden Hand (Rauschen ~5 mm) bleibt unter EnergieAn.
// Händigkeit: links bzw. rechts die Energie dieser Hand; beliebig und beidhändig das Grössere beider Hände.
//
// Zuschneiden: Bewegung = Energie über EnergieAn; Abschnitte mit Ruhe unter LueckeS dazwischen zählen zusammen; es gilt der Abschnitt mit der grössten Energie
// (Summe Energie × Zeit). Schnitt = Abschnitt ± RandS. Rückmeldung (die erste zutreffende): keine Bewegung, Hand verloren (eine nötige Hand fehlt in einem Bild des
// Schnitts), zu lang (> MaxDauerS), zu kurz (< MinDauerS), keine Ruhe vorher bzw. nachher (die Bewegung beginnt bzw. endet weniger als RandS vom ersten bzw.
// letzten Bild mit bestimmbarer Energie). Bestimmbar ist die Energie nur, wo beide Mittelungsfenster ganz in den Daten liegen (nicht in der ersten und letzten
// GlaettungS): am Rand gäbe ein einzelnes verrauschtes Bild, geteilt durch eine kurze Zeit, sonst eine Scheinbewegung. Start und Ende des Schnitts sind Bildzeiten.
// GestenRekorder macht dasselbe schrittweise (Bild für Bild): wartet auf die Bewegung, endet nach RuheS ruhiger Hände und schneidet dann zu.

namespace Hlks.Xr.Gesten;

/// <summary>Rückmeldung zu einer vorgezeigten Geste.</summary>
public enum AufnahmeRueckmeldung { Ok, KeineBewegung, HandVerloren, ZuLang, ZuKurz, KeineRuheVorher, KeineRuheNachher }

/// <summary>Ergebnis des Zuschneidens: Rückmeldung, die zugeschnittene Aufnahme (auch bei einer Rückmeldung, sofern es eine Bewegung gab), Start und Ende (s),
/// die bewegte Hand (beliebig: die aktivere; beidhändig null).</summary>
public sealed record Zuschnitt(AufnahmeRueckmeldung Rueckmeldung, Aufnahme? Aufnahme, double StartS, double EndeS, HandSeite? AktiveHand)
{
    public bool Ok => Rueckmeldung == AufnahmeRueckmeldung.Ok;
    public double DauerS => EndeS - StartS;
}

public static class AufnahmeHilfe
{
    /// <summary>Text der Rückmeldung für die Brille.</summary>
    public static string Text(AufnahmeRueckmeldung r, GestenEinstellungen? e = null)
    {
        var es = e ?? GestenEinstellungen.Vorgabe;
        return r switch
        {
            AufnahmeRueckmeldung.Ok => "Aufgenommen.",
            AufnahmeRueckmeldung.KeineBewegung => "Keine Bewegung erkannt – die Geste deutlicher vorzeigen.",
            AufnahmeRueckmeldung.HandVerloren => "Hand verloren – die Hände im Blick der Brille halten.",
            AufnahmeRueckmeldung.ZuLang => $"Zu lang – höchstens {es.MaxDauerS:0.#} s, danach die Hände kurz ruhig halten.",
            AufnahmeRueckmeldung.ZuKurz => $"Zu kurz – mindestens {es.MinDauerS:0.##} s, die Geste vollständig vorzeigen.",
            AufnahmeRueckmeldung.KeineRuheVorher => "Zu früh begonnen – erst nach dem Ton bewegen.",
            _ => "Nicht zur Ruhe gekommen – am Ende die Hände kurz ruhig halten.",
        };
    }

    /// <summary>Bewegungsenergie einer Hand je Bild (siehe Dateikopf); 0 in Bildern, in denen die Hand fehlt.</summary>
    public static double[] Energie(Aufnahme a, HandSeite seite, GestenEinstellungen? e = null)
    {
        var es = (e ?? GestenEinstellungen.Vorgabe).Geprueft();
        var r = RohFolge.Aus(a, es);
        var h = seite == HandSeite.Links ? 0 : 1;
        var aus = new double[r.Anzahl];
        for (var i = 0; i < r.Anzahl; i++) aus[i] = EnergieBei(r, h, i, es);
        return aus;
    }

    /// <summary>Mittel der Rohwerte der Hand h über die verfolgten Bilder mit T in [von, bis]; false ohne solches Bild.</summary>
    static bool Mittel(RohFolge r, int h, double von, double bis, out double zeit, out Vec3 mitte, out Vec3 normale, Span<double> kr, out double pinch)
    {
        zeit = 0; mitte = Vec3.Null; normale = Vec3.Null; pinch = 0;
        kr.Clear();
        var anzahl = 0;
        var i = Math.Max(0, r.Suche(von));
        for (; i < r.Anzahl && r.T(i) <= bis + 1e-12; i++)
        {
            if (r.T(i) < von - 1e-12 || !r.Ok(h, i)) continue;
            anzahl++;
            zeit += r.T(i);
            mitte += r.Mitte(h, i);
            normale += r.Normale(h, i);
            pinch += r.Pinch(h, i);
            for (var f = 0; f < 5; f++) kr[f] += r.Kruemmung(h, i, f);
        }
        if (anzahl == 0) return false;
        var q = 1.0 / anzahl;
        zeit *= q; mitte *= q; normale = normale.Einheit(); pinch *= q;
        for (var f = 0; f < 5; f++) kr[f] *= q;
        return true;
    }

    /// <summary>Ist die Energie im Bild i bestimmbar (beide Mittelungsfenster ganz in den Daten)?</summary>
    internal static bool Bestimmbar(RohFolge r, int i, GestenEinstellungen e) =>
        r.Anzahl > 1 && r.T(i) - e.GlaettungS >= r.T(0) - 1e-9 && r.T(i) + e.GlaettungS <= r.T(r.Anzahl - 1) + 1e-9;

    /// <summary>Energie der Hand h im Bild i (siehe Dateikopf); 0, wo sie nicht bestimmbar ist (erste und letzte GlaettungS der Daten).</summary>
    internal static double EnergieBei(RohFolge r, int h, int i, GestenEinstellungen e)
    {
        if (!r.Ok(h, i) || !Bestimmbar(r, i, e)) return 0;
        var halb = e.GlaettungS / 2;
        var ti = r.T(i);
        Span<double> ka = stackalloc double[5];
        Span<double> kb = stackalloc double[5];
        if (!Mittel(r, h, ti - 2 * halb, ti, out var ta, out var ma, out var na, ka, out var pa)) return 0;
        if (!Mittel(r, h, ti, ti + 2 * halb, out var tb, out var mb, out var nb, kb, out var pb)) return 0;
        var dt = tb - ta;
        if (dt < 1e-3) return 0;
        double kd = 0;
        for (var f = 0; f < 5; f++) kd += Math.Abs(kb[f] - ka[f]);
        kd /= 5;
        var winkel = Math.Acos(Math.Clamp(na.Skalar(nb), -1, 1));
        return (ma.Abstand(mb) + e.EnergieKruemmung * kd + e.EnergieDrehung * winkel + e.EnergiePinch * Math.Abs(pb - pa)) / dt;
    }

    /// <summary>Energie je Bild nach Händigkeit (links, rechts bzw. das Grössere beider Hände).</summary>
    internal static double[] EnergieNach(RohFolge r, Haendigkeit h, GestenEinstellungen e, out double[] links, out double[] rechts)
    {
        links = new double[r.Anzahl];
        rechts = new double[r.Anzahl];
        var aus = new double[r.Anzahl];
        for (var i = 0; i < r.Anzahl; i++)
        {
            links[i] = h == Haendigkeit.Rechts ? 0 : EnergieBei(r, 0, i, e);
            rechts[i] = h == Haendigkeit.Links ? 0 : EnergieBei(r, 1, i, e);
            aus[i] = Math.Max(links[i], rechts[i]);
        }
        return aus;
    }

    /// <summary>Die bewegtere Hand einer Aufnahme (Summe der Energie; gleich viel bzw. keine Bewegung: rechts).</summary>
    public static HandSeite AktiveHand(Aufnahme a, GestenEinstellungen? e = null)
    {
        var es = (e ?? GestenEinstellungen.Vorgabe).Geprueft();
        var r = RohFolge.Aus(a, es);
        EnergieNach(r, Haendigkeit.Beliebig, es, out var l, out var re);
        return l.Sum() > re.Sum() ? HandSeite.Links : HandSeite.Rechts;
    }

    /// <summary>Schneidet eine Rohaufnahme auf die vorgezeigte Geste zu (siehe Dateikopf).</summary>
    public static Zuschnitt Zuschneiden(Aufnahme roh, Haendigkeit haendigkeit, GestenEinstellungen? e = null)
    {
        var es = (e ?? GestenEinstellungen.Vorgabe).Geprueft();
        if (!roh.ZeitenGueltig()) return new Zuschnitt(AufnahmeRueckmeldung.KeineBewegung, null, roh.Start, roh.Start, null);
        var r = RohFolge.Aus(roh, es);
        var en = EnergieNach(r, haendigkeit, es, out var el, out var er);
        var n = r.Anzahl;

        // Abschnitte mit Bewegung, kurze Ruhe dazwischen überbrückt
        var abschnitte = new List<(int Von, int Bis)>();
        for (var i = 0; i < n; i++)
        {
            if (en[i] <= es.EnergieAn) continue;
            var j = i;
            while (j + 1 < n && en[j + 1] > es.EnergieAn) j++;
            if (abschnitte.Count > 0 && r.T(i) - r.T(abschnitte[^1].Bis) < es.LueckeS) abschnitte[^1] = (abschnitte[^1].Von, j);
            else abschnitte.Add((i, j));
            i = j;
        }
        if (abschnitte.Count == 0) return new Zuschnitt(AufnahmeRueckmeldung.KeineBewegung, null, roh.Start, roh.Start, null);
        double Arbeit((int Von, int Bis) a)
        {
            double s = 0;
            for (var i = a.Von; i <= a.Bis; i++) s += en[i] * (i + 1 < n ? r.T(i + 1) - r.T(i) : 0);
            return s;
        }
        var haupt = abschnitte.OrderByDescending(Arbeit).First();
        var start = Math.Max(r.T(0), r.T(haupt.Von) - es.RandS);
        var ende = Math.Min(r.T(n - 1), r.T(haupt.Bis) + es.RandS);

        HandSeite? aktiv = haendigkeit switch
        {
            Haendigkeit.Links => HandSeite.Links,
            Haendigkeit.Rechts => HandSeite.Rechts,
            Haendigkeit.Beliebig => Summe(el, haupt) > Summe(er, haupt) ? HandSeite.Links : HandSeite.Rechts,
            _ => null,
        };
        var schnitt = roh.Teil(start, ende);
        if (schnitt.Bilder.Count > 0) { start = schnitt.Start; ende = schnitt.Ende; }
        bool links = haendigkeit == Haendigkeit.Beidhaendig || aktiv == HandSeite.Links;
        bool rechts = haendigkeit == Haendigkeit.Beidhaendig || aktiv == HandSeite.Rechts;
        // Ruhe vorher/nachher: zwischen dem ersten (letzten) Bild mit bestimmbarer Energie und der Bewegung mindestens RandS
        var erstes = 0;
        while (erstes < n - 1 && !Bestimmbar(r, erstes, es)) erstes++;
        var letztes = n - 1;
        while (letztes > 0 && !Bestimmbar(r, letztes, es)) letztes--;
        var urteil = AufnahmeRueckmeldung.Ok;
        if (schnitt.Bilder.Any(b => links && !b.Links.Brauchbar || rechts && !b.Rechts.Brauchbar)) urteil = AufnahmeRueckmeldung.HandVerloren;
        else if (ende - start > es.MaxDauerS) urteil = AufnahmeRueckmeldung.ZuLang;
        else if (ende - start < es.MinDauerS) urteil = AufnahmeRueckmeldung.ZuKurz;
        else if (r.T(haupt.Von) - r.T(erstes) < es.RandS) urteil = AufnahmeRueckmeldung.KeineRuheVorher;
        else if (r.T(letztes) - r.T(haupt.Bis) < es.RandS) urteil = AufnahmeRueckmeldung.KeineRuheNachher;
        return new Zuschnitt(urteil, schnitt, start, ende, aktiv);
    }

    static double Summe(double[] w, (int Von, int Bis) a)
    {
        double s = 0;
        for (var i = a.Von; i <= a.Bis; i++) s += w[i];
        return s;
    }
}

/// <summary>Zustand des <see cref="GestenRekorder"/>.</summary>
public enum RekorderZustand { Wartet, Laeuft, Fertig }

/// <summary>
/// Nimmt eine vorgezeigte Geste Bild für Bild auf (Labor-App «Gesten»: Start per Knopf mit Countdown und Ton, dann Bilder hineingeben): wartet auf die Bewegung
/// (höchstens WarteMaxS), endet, wenn die Hände RuheS ruhig sind (bzw. die Bewegung länger als MaxDauerS + RuheS dauert), und schneidet dann zu
/// (<see cref="AufnahmeHilfe.Zuschneiden"/>). Ergebnis in <see cref="Ergebnis"/>.
/// </summary>
public sealed class GestenRekorder
{
    readonly Haendigkeit haendigkeit;
    readonly GestenEinstellungen e;
    readonly List<GestenBild> bilder = [];
    readonly RohFolge roh;
    double letzteBewegung = double.NegativeInfinity;
    int geprueft = -1;

    /// <summary>Höchstzahl Bilder einer Aufnahme (Schutz bei sehr hoher Bildrate).</summary>
    public const int BilderMax = 4000;

    public GestenRekorder(Haendigkeit haendigkeit, GestenEinstellungen? einstellungen = null)
    {
        this.haendigkeit = haendigkeit;
        e = (einstellungen ?? GestenEinstellungen.Vorgabe).Geprueft();
        roh = new RohFolge(BilderMax);
    }

    public RekorderZustand Zustand { get; private set; } = RekorderZustand.Wartet;
    /// <summary>Zeit des ersten Bilds mit Bewegung (null = noch keine).</summary>
    public double? BewegungSeit { get; private set; }
    /// <summary>Das Ergebnis, sobald <see cref="Zustand"/> Fertig ist.</summary>
    public Zuschnitt? Ergebnis { get; private set; }

    /// <summary>Ein Bild hinein; gibt den Zustand danach zurück. Bilder mit nicht aufsteigender Zeit fallen weg.</summary>
    public RekorderZustand Bild(GestenBild b)
    {
        if (Zustand == RekorderZustand.Fertig) return Zustand;
        if (!double.IsFinite(b.T) || !double.IsFinite(b.Gieren) || bilder.Count > 0 && !(b.T > bilder[^1].T)) return Zustand;
        bilder.Add(b);
        roh.Hinzu(b, e);
        var t = b.T;
        var t0 = bilder[0].T;
        // Energie ist erst GlaettungS nach einem Bild bestimmbar (Mittel danach)
        while (geprueft + 1 < roh.Anzahl && roh.T(geprueft + 1) <= t - e.GlaettungS)
        {
            geprueft++;
            double en = haendigkeit switch
            {
                Haendigkeit.Links => AufnahmeHilfe.EnergieBei(roh, 0, geprueft, e),
                Haendigkeit.Rechts => AufnahmeHilfe.EnergieBei(roh, 1, geprueft, e),
                _ => Math.Max(AufnahmeHilfe.EnergieBei(roh, 0, geprueft, e), AufnahmeHilfe.EnergieBei(roh, 1, geprueft, e)),
            };
            if (en > e.EnergieAn)
            {
                letzteBewegung = roh.T(geprueft);
                if (Zustand == RekorderZustand.Wartet)
                {
                    Zustand = RekorderZustand.Laeuft;
                    BewegungSeit = roh.T(geprueft);
                }
            }
        }
        if (Zustand == RekorderZustand.Wartet && t - t0 > e.WarteMaxS)
            return Beenden(new Zuschnitt(AufnahmeRueckmeldung.KeineBewegung, null, t0, t0, null));
        if (Zustand == RekorderZustand.Laeuft)
        {
            var ruhig = geprueft >= 0 && roh.T(geprueft) - letzteBewegung >= e.RuheS;
            var zuLange = BewegungSeit is { } s && t - s > e.MaxDauerS + e.RuheS + e.GlaettungS;
            if (ruhig || zuLange || bilder.Count >= BilderMax)
            {
                var z = AufnahmeHilfe.Zuschneiden(new Aufnahme(bilder.ToList()), haendigkeit, e);
                // nicht zur Ruhe gekommen: zu lang (ausser die Hand ging verloren bzw. es gab gar keine Bewegung)
                if (!ruhig && z.Rueckmeldung is not (AufnahmeRueckmeldung.HandVerloren or AufnahmeRueckmeldung.KeineBewegung))
                    z = z with { Rueckmeldung = AufnahmeRueckmeldung.ZuLang };
                return Beenden(z);
            }
        }
        return Zustand;
    }

    RekorderZustand Beenden(Zuschnitt z)
    {
        Ergebnis = z;
        Zustand = RekorderZustand.Fertig;
        return Zustand;
    }
}
