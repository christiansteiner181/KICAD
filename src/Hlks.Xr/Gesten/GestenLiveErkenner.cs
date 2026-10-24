// Quest App V1b-1 (Modul XR, Gestenlabor): Live-Erkennung der eigenen Gesten im Strom der Bilder. Bilder hinein, Ereignisse heraus. Rein, ohne StereoKit.
//
// Ablauf je Bild (GestenLiveErkenner.Bild):
//   1. Rohwerte des Bilds in den Ringpuffer (PufferS, ~2.5 s; RohFolge, keine Zuteilung je Bild). Eine Lücke über BildLueckeS gilt als Verlust der Hände.
//      Bewegungstor: je Hand die letzte Zeit, zu der sie sich merklich bewegt hat (TorLageM, TorKruemmung, TorPinch, TorDrehungGrad).
//   2. Höchstens alle SchrittS (nicht in jedem Bild) und nicht in der Sperrzeit: Kandidaten-Abschnitte, die jetzt enden. Ihre Längen bilden ein festes Raster
//      (Verhältnis LaengenSchritt) vom kürzesten Beispiel aller Gesten / (1 + LaengenRand) bis zum längsten × (1 + LaengenRand); jede Geste prüft nur die Längen
//      in ihrem eigenen Bereich. Abschnitte ohne Bewegung (seit ihrem Beginn keine Bewegung) fallen weg.
//      Die Rohwerte im Körperbezug (GestenMerkmale.Zeilen, Gierwinkel des jetzigen Bilds) entstehen einmal je Auswertung; die Merkmalsfolge einer Länge und
//      Merkmalart einmal für alle Gesten. Je Geste zuerst die untere Schranke gegen die gemeinsame Hülle ihrer Beispiele, dann je Beispiel, und nur darunter das DTW
//      mit vorzeitigem Abbruch; Grenze = Schwelle der Geste bzw. ihr bester Abstand in dieser Auswertung.
//   3. Eine Geste unter ihrer Schwelle ist ein offener Treffer. Er gilt erst, wenn das beste Verhältnis seit WarteS nicht mehr besser wurde – so zählt das Ende der
//      ganzen Geste und nicht schon ein Teil. Dann Entscheid wie GestenErkenner.Entscheiden (erkannt bzw. unklar) als Ereignis.
//   4. Sperrzeit nach einem Ereignis: bis Ende der Geste + SperreS wird nicht geprüft, und ein neuer Abschnitt muss nach der Sperrzeit beginnen (wie
//      XrWischLogik: Bewegungen in der Sperrzeit zählen nicht).

namespace Hlks.Xr.Gesten;

/// <summary>Ereignis des Live-Erkenners: Zeit des Entscheids, erkannt bzw. unklar, beste (und zweitbeste) Geste, Start und Ende des erkannten Abschnitts.</summary>
public sealed record GestenEreignis(double T, ErkennungsArt Art, GestenTreffer Beste, GestenTreffer? Zweite, double StartS, double EndeS);

/// <summary>Live-Erkennung (siehe Dateikopf). Nicht threadsicher: ein Erkenner je Strom.</summary>
public sealed class GestenLiveErkenner
{
    /// <summary>Höchste Bildrate, für die der Ringpuffer bemessen ist (Bilder je s); schnellere Ströme kürzen den Puffer.</summary>
    public const double BildrateMax = 200;

    readonly GestenEinstellungen e;
    readonly IReadOnlyList<VorbereiteteGeste> gesten;
    readonly RohFolge roh;
    readonly double[] zeilen;
    readonly double[] laengen;
    readonly (int Geste, Merkmalart Art)[][] jeLaenge;
    readonly bool brauchtLinks, brauchtRechts;
    readonly double[][] puffer = new double[4][];
    readonly bool[] berechnet = new bool[4], verfuegbar = new bool[4];
    readonly double[] jetztAbstand, jetztStart;
    readonly HandSeite?[] jetztSeite;
    readonly double[] offenQ, offenAbstand, offenStart, offenEnde;
    readonly HandSeite?[] offenSeite;
    readonly double cosTor;
    bool offen;
    double offenBestQ, offenBestT;
    double letzteT, letzteAuswertung, startAb, sperreBis, verfolgtL, verfolgtR, letzteBewegung;
    readonly bool[] anker = new bool[2];
    readonly Vec3[] ankerLage = new Vec3[2], ankerNormale = new Vec3[2];
    readonly double[] ankerKruemmung = new double[2], ankerPinch = new double[2];

    public GestenLiveErkenner(IEnumerable<EigeneGeste> gesten, GestenEinstellungen? einstellungen = null) : this(new GestenErkenner(gesten, einstellungen)) { }

    public GestenLiveErkenner(GestenErkenner erkenner)
    {
        Erkenner = erkenner;
        e = erkenner.Einstellungen;
        gesten = erkenner.Gueltige;
        roh = new RohFolge((int)Math.Ceiling(e.PufferS * BildrateMax) + 8);
        zeilen = new double[roh.Kapazitaet * 2 * GestenMerkmale.ZeileBreite];
        foreach (var art in Enum.GetValues<Merkmalart>()) puffer[(int)art] = new double[e.Stuetzpunkte * GestenMerkmale.Dimension(art)];
        int z = gesten.Count;
        jetztAbstand = new double[z]; jetztStart = new double[z]; jetztSeite = new HandSeite?[z];
        offenQ = new double[z]; offenAbstand = new double[z]; offenStart = new double[z]; offenEnde = new double[z]; offenSeite = new HandSeite?[z];
        cosTor = Math.Cos(e.TorDrehungGrad * Math.PI / 180);
        foreach (var g in gesten)
            foreach (var a in g.Arten)
            {
                var (l, r) = GestenMerkmale.Haende(a);
                brauchtLinks |= l;
                brauchtRechts |= r;
            }

        // Raster der Kandidatenlängen und je Länge die Gesten (mit ihren Merkmalarten), die sie prüfen
        var bereiche = gesten.Select(g => (Lo: Math.Max(g.DauerMinS / (1 + e.LaengenRand), 0.05), Hi: Math.Min(g.DauerMaxS * (1 + e.LaengenRand), e.PufferS))).ToList();
        var liste = new List<double>();
        if (bereiche.Count > 0)
        {
            var lo = bereiche.Min(b => b.Lo);
            var hi = bereiche.Max(b => b.Hi);
            for (var x = lo; x <= hi * 1.0001; x *= e.LaengenSchritt) liste.Add(x);
            if (liste[^1] < hi * 0.999) liste.Add(hi);
        }
        laengen = [.. liste];
        var rand = Math.Sqrt(e.LaengenSchritt);
        jeLaenge = new (int, Merkmalart)[laengen.Length][];
        for (var k = 0; k < laengen.Length; k++)
        {
            var l = laengen[k];
            jeLaenge[k] = [.. Enumerable.Range(0, z).Where(i => l >= bereiche[i].Lo / rand && l <= bereiche[i].Hi * rand)
                .SelectMany(i => gesten[i].Arten.Select(a => (i, a)))];
        }
        Zuruecksetzen();
    }

    public GestenErkenner Erkenner { get; }
    /// <summary>Raster der Kandidatenlängen (s).</summary>
    public IReadOnlyList<double> Laengen => laengen;
    /// <summary>Zähler (für die Messung im Labor): Auswertungen, Merkmalsfolgen, berechnete Schranken, berechnete DTW.</summary>
    public long Auswertungen { get; private set; }
    public long Folgen { get; private set; }
    public long Schranken { get; private set; }
    public long Vergleiche { get; private set; }

    /// <summary>Beginnt neu (leerer Puffer, keine Sperre).</summary>
    public void Zuruecksetzen()
    {
        roh.Leeren();
        letzteT = letzteAuswertung = startAb = sperreBis = letzteBewegung = double.NegativeInfinity;
        verfolgtL = verfolgtR = double.PositiveInfinity;
        anker[0] = anker[1] = false;
        OffenLeeren();
    }

    void OffenLeeren()
    {
        offen = false;
        offenBestQ = double.PositiveInfinity;
        offenBestT = double.NegativeInfinity;
        Array.Fill(offenQ, double.PositiveInfinity);
    }

    /// <summary>
    /// Ein Bild hinein (aufsteigende Zeit; ein Bild mit gleicher oder kleinerer Zeit fällt weg, ein Sprung um mehr als 1 s zurück beginnt neu). Gibt ein Ereignis
    /// zurück, wenn in diesem Bild eine Geste entschieden ist (erkannt oder unklar), sonst null.
    /// </summary>
    public GestenEreignis? Bild(GestenBild b)
    {
        if (!double.IsFinite(b.T) || !double.IsFinite(b.Gieren)) return null;
        if (b.T <= letzteT)
        {
            if (b.T < letzteT - 1) Zuruecksetzen();
            else return null;
        }
        if (b.T - letzteT > e.BildLueckeS) startAb = Math.Max(startAb, b.T);
        roh.Hinzu(b, e);
        letzteT = b.T;
        while (roh.Anzahl > 2 && roh.T(1) <= b.T - e.PufferS) roh.Entfernen(1);
        verfolgtL = b.Links.Brauchbar ? Math.Min(verfolgtL, b.T) : double.PositiveInfinity;
        verfolgtR = b.Rechts.Brauchbar ? Math.Min(verfolgtR, b.T) : double.PositiveInfinity;
        Tor(0, b.T);
        Tor(1, b.T);
        if (gesten.Count == 0) return null;
        if (b.T >= sperreBis && b.T - letzteAuswertung >= e.SchrittS - 1e-9)
        {
            letzteAuswertung = b.T;
            Auswerten(b.T);
        }
        return Entscheid(b.T);
    }

    /// <summary>Bewegungstor der Hand h (siehe Dateikopf) mit dem neuesten Bild.</summary>
    void Tor(int h, double t)
    {
        var i = roh.Anzahl - 1;
        if (!roh.Ok(h, i)) { anker[h] = false; return; }
        var m = roh.Mitte(h, i);
        var n = roh.Normale(h, i);
        double k = 0;
        for (var f = 0; f < 5; f++) k += roh.Kruemmung(h, i, f);
        k /= 5;
        var p = roh.Pinch(h, i);
        var bewegt = anker[h] && (m.Abstand(ankerLage[h]) > e.TorLageM || Math.Abs(k - ankerKruemmung[h]) > e.TorKruemmung
            || Math.Abs(p - ankerPinch[h]) > e.TorPinch || n.Skalar(ankerNormale[h]) < cosTor);
        if (bewegt) letzteBewegung = t;
        if (bewegt || !anker[h])
        {
            anker[h] = true;
            ankerLage[h] = m;
            ankerNormale[h] = n;
            ankerKruemmung[h] = k;
            ankerPinch[h] = p;
        }
    }

    bool Verfolgt(Merkmalart art, double t0) => art switch
    {
        Merkmalart.Rechts => verfolgtR <= t0 + 1e-9,
        Merkmalart.Beide => verfolgtL <= t0 + 1e-9 && verfolgtR <= t0 + 1e-9,
        _ => verfolgtL <= t0 + 1e-9,
    };

    void Auswerten(double t)
    {
        if (laengen.Length == 0 || roh.Anzahl < 2 || t - letzteBewegung > laengen[^1] + 1e-9) return;   // seit dem längsten Abschnitt keine Bewegung
        var kMax = -1;
        for (var k = 0; k < laengen.Length; k++)
        {
            var t0 = t - laengen[k];
            if (t0 < startAb || t0 < roh.T(0)) break;   // Längen aufsteigend: alle längeren ebenso
            kMax = k;
        }
        if (kMax < 0) return;
        Auswertungen++;
        var bis = roh.Anzahl - 1;
        var von = Math.Max(0, roh.Suche(t - laengen[kMax]));
        GestenMerkmale.Zeilen(roh, von, bis, roh.Gieren(bis), brauchtLinks, brauchtRechts, zeilen);
        Array.Fill(jetztAbstand, double.PositiveInfinity);
        int n = e.Stuetzpunkte, band = e.Band;
        for (var k = 0; k <= kMax; k++)
        {
            var t0 = t - laengen[k];
            if (t0 > letzteBewegung) continue;   // in diesem Abschnitt hat sich keine Hand bewegt
            Array.Clear(berechnet);
            foreach (var (gi, art) in jeLaenge[k])
            {
                var a = (int)art;
                if (!berechnet[a])
                {
                    berechnet[a] = true;
                    verfuegbar[a] = Verfolgt(art, t0);
                    if (verfuegbar[a])
                    {
                        Folgen++;
                        GestenMerkmale.FolgeAusZeilen(roh, von, bis, zeilen, t0, t, art, e, puffer[a]);
                    }
                }
                if (!verfuegbar[a]) continue;
                var g = gesten[gi];
                var p = puffer[a];
                var d = g.Dimension;
                var grenze = Math.Min(g.Schwelle, jetztAbstand[gi]);
                // zuerst gegen die gemeinsame Hülle aller Beispiele der Geste: liegt schon sie zu weit weg, keines der Beispiele
                Schranken++;
                if (GestenDtw.UntereSchranke(p, g.UntenAlle, g.ObenAlle, n, d, grenze) >= grenze) continue;
                foreach (var ex in g.Beispiele)
                {
                    Schranken++;
                    if (GestenDtw.UntereSchranke(p, ex.Unten, ex.Oben, n, d, grenze) >= grenze) continue;
                    Vergleiche++;
                    var w = GestenDtw.Abstand(p, ex.Werte, n, d, band, grenze);
                    if (w < grenze)
                    {
                        grenze = w;
                        jetztAbstand[gi] = w;
                        jetztStart[gi] = t0;
                        jetztSeite[gi] = GestenMerkmale.Seite(art);
                    }
                }
            }
        }
        for (var i = 0; i < gesten.Count; i++)
        {
            if (!(jetztAbstand[i] <= gesten[i].Schwelle)) continue;
            var q = jetztAbstand[i] / gesten[i].Schwelle;
            if (q < offenQ[i])
            {
                offenQ[i] = q;
                offenAbstand[i] = jetztAbstand[i];
                offenStart[i] = jetztStart[i];
                offenEnde[i] = t;
                offenSeite[i] = jetztSeite[i];
            }
            if (q < offenBestQ)
            {
                offenBestQ = q;
                offenBestT = t;
                offen = true;
            }
        }
    }

    GestenEreignis? Entscheid(double t)
    {
        if (!offen || t - offenBestT < e.WarteS - 1e-9) return null;
        var treffer = new List<GestenTreffer>();
        for (var i = 0; i < gesten.Count; i++)
            if (double.IsFinite(offenQ[i])) treffer.Add(gesten[i].Treffer(offenAbstand[i], offenSeite[i]));
        var u = GestenErkenner.Entscheiden(treffer, e);
        if (u.Beste is not { } beste) { OffenLeeren(); return null; }
        var bi = -1;
        for (var i = 0; i < gesten.Count; i++) if (gesten[i].Geste.Id == beste.GestenId) bi = i;
        var ende = offenEnde[bi];
        var ev = new GestenEreignis(t, u.Art, beste, u.Zweite is { Verhaeltnis: <= 1 } z ? z : null, offenStart[bi], ende);
        OffenLeeren();
        sperreBis = ende + e.SperreS;
        startAb = Math.Max(startAb, sperreBis);
        return ev;
    }
}
