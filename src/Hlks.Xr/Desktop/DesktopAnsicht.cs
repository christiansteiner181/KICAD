// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): der virtuelle Bildschirm im Raum – Form, Platz, Ein- und Ausblenden, Griff und Knöpfe. Rein, ohne
// StereoKit; alles im Raum der Brille (Referenzraum, Boden y = 0), nicht in der Szene: der Bildschirm gehört zum Körper, die VR-Szene bleibt starr.
//
// Form (DesktopFlaeche): ein Ausschnitt eines senkrechten Zylinders, Radius desktop.radius_m (Annahme 2.0 m), Bogenlänge desktop.breite_m (1.4 m), Höhe nach dem
// Seitenverhältnis des Monitors. Lokaler Raum: x rechts, y oben, z zum Betrachter, Mitte des Bildschirms im Ursprung; die Achse des Zylinders liegt bei
// (0, ·, R) – vor dem Bildschirm, hinter dem Kopf (R > Abstand): «leicht gebogen» (Christian, Desktop-Ansicht von Meta).
// Platz: frontal zum Blick beim Hineinziehen (nur Gieren, Nicken zählt nicht), desktop.abstand_m (1.1 m) vor den Augen, Mitte HoeheM (−0.10 m) unter Augenhöhe.
// Danach im Raum verankert (folgt dem Kopf nicht); am Griff verschiebbar – dann dreht er sich zum Kopf (wie Fenster in visionOS).
// Ein- und Ausblenden (DesktopAnsicht.Schritt): Der Bildschirm kommt von LINKS (Drehung um die Hochachse durch die Augen, AusGrad = 95° ganz draussen) und folgt
// dem Fortschritt der Hand (Hineinziehen), nach der Auslösung setzt er sich weich (SetzenS); Wegwischen schiebt ihn nach links hinaus (RausS). Bricht die Hand
// ab, gleitet er zurück. Alles Annahmen V1c.
// Bedienen: Kapsel-Griff unter dem Bildschirm (Pinch fasst, verschiebt), links daneben ein runder Schliessen-Knopf (Fingerspitze), rechts – nur bei mehreren
// Monitoren – ein runder Knopf für den nächsten Monitor (Fensterteile wie V1a nach den HIG von visionOS).
// Wegwischbares Ziel (für V1a's gemeinsames Wegwischen): DesktopAnsicht.Ziel gibt Lage (Fläche), Zeit des Einblendens und Id «desktop».

namespace Hlks.Xr.Desktop;

/// <summary>Masse des Bildschirms: Bogenlänge, Höhe, Radius (m).</summary>
public readonly record struct DesktopMasse(double BreiteM, double HoeheM, double RadiusM)
{
    /// <summary>Seitenverhältnis 16:9, solange noch kein Bild da ist.</summary>
    public static DesktopMasse Aus(DesktopEinstellungen e, int breitePx = 1920, int hoehePx = 1080) =>
        new(e.BreiteM, e.BreiteM * Math.Max(1, hoehePx) / Math.Max(1, breitePx), Math.Max(e.RadiusM, e.BreiteM / Math.PI));

    /// <summary>Öffnungswinkel des Bogens (Bogenmass).</summary>
    public double Winkel => BreiteM / RadiusM;
    /// <summary>Sehne (gerader Abstand der beiden Ränder, m).</summary>
    public double Sehne => 2 * RadiusM * Math.Sin(Winkel / 2);
    /// <summary>So weit kommen die Ränder zum Betrachter (m).</summary>
    public double Tiefe => RadiusM * (1 - Math.Cos(Winkel / 2));
}

/// <summary>Netz eines Bildschirmteils: Lagen (lokal), Normalen, Texturkoordinaten (U, V; V = 0 oben), Dreiecke.</summary>
public sealed record DesktopNetz(Vec3[] Positionen, Vec3[] Normalen, (double U, double V)[] Uv, int[] Indizes);

public static class DesktopFlaeche
{
    /// <summary>So fein wird der Bogen unterteilt (Segmente je Meter; 1.4 m = 34 Segmente, eine Sehne weicht bei R = 2 m weniger als 0.2 mm ab).</summary>
    public const double SegmenteJeMeter = 24;

    /// <summary>Punkt der Fläche (lokal) zu Bogen s (m ab der Mitte, rechts positiv) und Höhe v (m ab der Mitte).</summary>
    public static Vec3 Punkt(double s, double v, double radius)
    {
        var phi = s / radius;
        return new Vec3(radius * Math.Sin(phi), v, radius * (1 - Math.Cos(phi)));
    }

    /// <summary>Normale der Fläche (lokal, zum Betrachter bzw. zur Achse hin).</summary>
    public static Vec3 Normale(double s, double radius)
    {
        var phi = s / radius;
        return new Vec3(-Math.Sin(phi), 0, Math.Cos(phi));
    }

    /// <summary>Lokaler Punkt → Bogen s, Höhe v und Tiefe vor der Fläche (m, &gt; 0 = zum Betrachter hin).</summary>
    public static (double S, double V, double Tiefe) AufFlaeche(Vec3 lokal, double radius)
    {
        double dx = lokal.X, dz = lokal.Z - radius;
        var phi = Math.Atan2(dx, -dz);
        return (phi * radius, lokal.Y, radius - Math.Sqrt(dx * dx + dz * dz));
    }

    /// <summary>Lokal → Raum der Brille für einen Bildschirm mit Mitte und Gieren (Blickrichtung vom Betrachter zum Bildschirm).</summary>
    public static Vec3 Welt(Vec3 lokal, Vec3 mitte, double gieren) =>
        mitte + XrGesten.Rechts(gieren) * lokal.X + Vec3.Hoch * lokal.Y + XrGesten.Vorn(gieren) * -lokal.Z;

    /// <summary>Raum der Brille → lokal.</summary>
    public static Vec3 Lokal(Vec3 welt, Vec3 mitte, double gieren)
    {
        var d = welt - mitte;
        return new Vec3(d.Skalar(XrGesten.Rechts(gieren)), d.Y, -d.Skalar(XrGesten.Vorn(gieren)));
    }

    /// <summary>
    /// Netz eines Felds (Textur) des Rasters: ein Streifen des Bogens mit so vielen Segmenten wie nötig, zwei Zeilen von Ecken (die Fläche ist senkrecht gerade).
    /// U, V gehen über das ganze Feld von 0 bis 1.
    /// </summary>
    public static DesktopNetz Feld(DesktopRaster r, int feld, DesktopMasse m)
    {
        var (x0, y0, fb, fh) = r.FeldRechteck(feld);
        double MeterX(double px) => (px / r.Breite - 0.5) * m.BreiteM;
        double MeterY(double py) => (0.5 - py / r.Hoehe) * m.HoeheM;
        double s0 = MeterX(x0), s1 = MeterX(x0 + fb), v0 = MeterY(y0), v1 = MeterY(y0 + fh);
        var k = Math.Max(1, (int)Math.Ceiling((s1 - s0) * SegmenteJeMeter));
        var pos = new Vec3[2 * (k + 1)];
        var nor = new Vec3[pos.Length];
        var uv = new (double, double)[pos.Length];
        for (int i = 0; i <= k; i++)
        {
            var u = (double)i / k;
            var s = s0 + (s1 - s0) * u;
            pos[i] = Punkt(s, v0, m.RadiusM);
            pos[k + 1 + i] = Punkt(s, v1, m.RadiusM);
            nor[i] = nor[k + 1 + i] = Normale(s, m.RadiusM);
            uv[i] = (u, 0);
            uv[k + 1 + i] = (u, 1);
        }
        var ind = new List<int>(k * 6);
        for (int i = 0; i < k; i++)
        {
            int a = i, b = i + 1, c = k + 1 + i, d = k + 2 + i;   // a b oben, c d unten
            ind.AddRange([a, c, d, a, d, b]);                     // gegen den Uhrzeigersinn von vorn (+z)
        }
        return new DesktopNetz(pos, nor, uv, [.. ind]);
    }

    /// <summary>
    /// Glasrahmen hinter dem Bildschirm: dieselbe Krümmung, rand (m) breiter als der Bildschirm, Ecken mit eckRadius gerundet (Ecken des Rasters auf den Kreisbogen
    /// gezogen), tiefe (m) hinter der Fläche. Ein Netz aus spalten × zeilen Zellen.
    /// </summary>
    public static DesktopNetz Rahmen(DesktopMasse m, double rand, double eckRadius, double tiefe, int spalten = 40, int zeilen = 10)
    {
        double b = m.BreiteM + 2 * rand, h = m.HoeheM + 2 * rand;
        var ek = Math.Clamp(eckRadius, 0, Math.Min(b, h) / 2);
        var pos = new Vec3[(spalten + 1) * (zeilen + 1)];
        var nor = new Vec3[pos.Length];
        var uv = new (double, double)[pos.Length];
        for (int j = 0; j <= zeilen; j++)
            for (int i = 0; i <= spalten; i++)
            {
                double s = -b / 2 + b * i / spalten, v = h / 2 - h * j / zeilen;
                // Ecke: liegt der Punkt ausserhalb des Viertelkreises, auf ihn ziehen
                double cx = Math.Clamp(s, -b / 2 + ek, b / 2 - ek), cy = Math.Clamp(v, -h / 2 + ek, h / 2 - ek);
                double dx = s - cx, dy = v - cy, dl = Math.Sqrt(dx * dx + dy * dy);
                if (dl > ek && dl > 1e-12) { s = cx + dx / dl * ek; v = cy + dy / dl * ek; }
                var n = i + j * (spalten + 1);
                pos[n] = Punkt(s, v, m.RadiusM) - Normale(s, m.RadiusM) * tiefe;
                nor[n] = Normale(s, m.RadiusM);
                uv[n] = ((double)i / spalten, (double)j / zeilen);
            }
        var ind = new List<int>(spalten * zeilen * 6);
        for (int j = 0; j < zeilen; j++)
            for (int i = 0; i < spalten; i++)
            {
                int a = i + j * (spalten + 1), bb = a + 1, c = a + spalten + 1, d = c + 1;
                ind.AddRange([a, c, d, a, d, bb]);
            }
        return new DesktopNetz(pos, nor, uv, [.. ind]);
    }
}

/// <summary>Phase des Bildschirms: aus, kommt (folgt der Hand), setzt sich, da, geht (folgt der Hand), hinaus.</summary>
public enum DesktopPhase { Aus, Kommt, Setzt, Da, Geht, Raus }

/// <summary>Verankerung: Augen beim Platzieren (Drehpunkt des Ein- und Ausblendens), Gieren des Bildschirms, Mitte am Platz (Raum der Brille).</summary>
public sealed record DesktopLage(Vec3 Auge, double Gieren, Vec3 Mitte);

/// <summary>Ein wegwischbares Ziel für die gemeinsame Wegwisch-Geste (V1a): Id, Fläche (Mitte, Normale zum Betrachter, Breite der Sehne, Höhe), Zeit des Einblendens.</summary>
public sealed record WegwischZiel(string Id, TabletPose Flaeche, double EingeblendetT);

/// <summary>Was die Hände am Bildschirm in diesem Bild getan haben.</summary>
public readonly record struct DesktopBedienStand(bool GriffNah, bool GriffGehalten, bool SchliessenNah, bool Schliessen, bool MonitorNah, bool MonitorWechseln);

public sealed class DesktopAnsicht
{
    /// <summary>Ganz draussen steht der Bildschirm so weit links (Grad um die Hochachse durch die Augen; ausserhalb des Blickfelds der Quest 3).</summary>
    public const double AusGrad = 95;
    /// <summary>Setzen nach der Auslösung (s), Hinausgleiten (s), Folgen der Hand (Zeitkonstante, s). Annahmen V1c: ruhig und kurz.</summary>
    public const double SetzenS = 0.35, RausS = 0.3, FolgenS = 0.05;
    /// <summary>Griff (Kapsel) unter dem Bildschirm: Breite, Höhe, Abstand zur Unterkante; Knöpfe: Radius, Abstand neben dem Griff; Fang um Griff und Knöpfe (m).</summary>
    public const double GriffB = 0.16, GriffH = 0.022, GriffAbstand = 0.035, KnopfRadius = 0.022, KnopfAbstand = 0.035, FangM = 0.03;

    double von, abT;
    bool griff, knopfGesperrt;
    Vec3 griffVersatz;

    public DesktopPhase Phase { get; private set; } = DesktopPhase.Aus;
    /// <summary>0 = ganz draussen (links), 1 = am Platz.</summary>
    public double Anteil { get; private set; }
    public DesktopLage? Lage { get; private set; }
    public double EingeblendetT { get; private set; } = double.NegativeInfinity;
    public double AusgeblendetT { get; private set; } = double.NegativeInfinity;
    /// <summary>Gezeigt (nicht ganz draussen)?</summary>
    public bool Sichtbar => Phase != DesktopPhase.Aus;
    /// <summary>Am Platz und ruhig (Griff und Knöpfe bedienbar, das Wegwischen erlaubt)?</summary>
    public bool Da => Phase is DesktopPhase.Da or DesktopPhase.Geht;
    /// <summary>Darf das Hineinziehen zählen (aus, oder er kommt gerade der Hand folgend)? Sonst bräche die Geste ab, sobald er sichtbar wird.</summary>
    public bool HineinMoeglich => Phase is DesktopPhase.Aus or DesktopPhase.Kommt;
    /// <summary>Wird der Bildschirm gerade am Griff gehalten?</summary>
    public bool GriffGehalten => griff;

    /// <summary>Deckkraft beim Ein- und Ausblenden (draussen durchsichtig, ab einem Drittel des Wegs deckend).</summary>
    public double Deckkraft => Math.Clamp(Anteil * 3, 0, 1);

    public static double Weich(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>Platz frontal zum Blick (siehe Dateikopf).</summary>
    public static DesktopLage Platz(Vec3 auge, double blickGieren, DesktopEinstellungen e) =>
        new(auge, blickGieren, auge + XrGesten.Vorn(blickGieren) * e.AbstandM + Vec3.Hoch * e.HoeheM);

    /// <summary>Mitte und Gieren des Bildschirms beim Anteil a: um die Hochachse durch die Augen der Verankerung nach links gedreht um (1 − a) × AusGrad.</summary>
    public static (Vec3 Mitte, double Gieren) Pose(DesktopLage l, double anteil)
    {
        var d = Gedreht(l, (1 - Math.Clamp(anteil, 0, 1)) * AusGrad * Num.Grad);
        return (d.Mitte, d.Gieren);
    }

    /// <summary>Lage um die Hochachse durch die Augen gedreht (Bogenmass, gegen den Uhrzeigersinn von oben = nach links).</summary>
    public static DesktopLage Gedreht(DesktopLage l, double delta)
    {
        var h = l.Mitte - l.Auge;
        var laenge = Num.Hypot(h.X, h.Z);
        var g = Math.Atan2(-h.Z, h.X) + delta;
        return new DesktopLage(l.Auge, l.Gieren + delta, new Vec3(l.Auge.X + laenge * Math.Cos(g), l.Mitte.Y, l.Auge.Z - laenge * Math.Sin(g)));
    }

    /// <summary>Mitte und Gieren im aktuellen Bild (null, solange aus).</summary>
    public (Vec3 Mitte, double Gieren)? Jetzt => Lage is { } l && Sichtbar ? Pose(l, Anteil) : null;

    /// <summary>
    /// Ein Bild: Gesten (DesktopGesten) anwenden, Anteil führen. auge/blickGieren = Kopf jetzt (Platz beim Hineinziehen, Drehpunkt beim Hinausschieben).
    /// Gibt true zurück, wenn der Bildschirm in diesem Bild eingeblendet (ausgelöst) wurde.
    /// </summary>
    public bool Schritt(double t, double dt, Vec3 auge, double blickGieren, DesktopGestenStand g, DesktopEinstellungen e)
    {
        var eingeblendet = false;
        switch (Phase)
        {
            case DesktopPhase.Aus:
                Anteil = 0;
                if (g.Hinein.Ausgeloest || g.Hinein.Laeuft)
                {
                    Lage = Platz(auge, blickGieren, e);
                    Phase = DesktopPhase.Kommt;
                    if (g.Hinein.Ausgeloest) { Setzen(t); eingeblendet = true; }
                    else Folgen(g.Hinein.Fortschritt, dt);
                }
                break;
            case DesktopPhase.Kommt:
                if (g.Hinein.Ausgeloest) { Setzen(t); eingeblendet = true; break; }
                Folgen(g.Hinein.Laeuft ? g.Hinein.Fortschritt : 0, dt);
                if (!g.Hinein.Laeuft && Anteil <= 0.002) { Anteil = 0; Phase = DesktopPhase.Aus; }
                break;
            case DesktopPhase.Setzt:
                Anteil = von + (1 - von) * Weich((t - abT) / SetzenS);
                if (t - abT >= SetzenS) { Anteil = 1; Phase = DesktopPhase.Da; }
                break;
            case DesktopPhase.Da:
            case DesktopPhase.Geht:
                if (griff) { Anteil = 1; break; }
                if (g.Weg.Ausgeloest) { Ausblenden(t, auge); break; }
                if (g.Weg.Laeuft) { Phase = DesktopPhase.Geht; Folgen(1 - g.Weg.Fortschritt, dt); }
                else
                {
                    Folgen(1, dt);
                    if (Anteil >= 0.998) { Anteil = 1; Phase = DesktopPhase.Da; }
                }
                break;
            case DesktopPhase.Raus:
                Anteil = von * (1 - Weich((t - abT) / RausS));
                if (t - abT >= RausS) { Anteil = 0; Phase = DesktopPhase.Aus; }
                break;
        }
        return eingeblendet;
    }

    void Folgen(double ziel, double dt) => Anteil += (Math.Clamp(ziel, 0, 1) - Anteil) * (1 - Math.Exp(-Math.Max(0, dt) / FolgenS));

    void Setzen(double t)
    {
        Phase = DesktopPhase.Setzt;
        von = Anteil;
        abT = t;
        EingeblendetT = t;
    }

    /// <summary>Sofort einblenden (Prüfung bzw. Auftrag): Platz frontal, dann weich an den Platz.</summary>
    public void Einblenden(double t, Vec3 auge, double blickGieren, DesktopEinstellungen e)
    {
        if (Phase is DesktopPhase.Da or DesktopPhase.Setzt or DesktopPhase.Geht) return;
        if (Phase is DesktopPhase.Aus or DesktopPhase.Raus || Lage is null) { Lage = Platz(auge, blickGieren, e); Anteil = 0; }
        Setzen(t);
    }

    /// <summary>Hinausschieben (Wegwischen, Schliessen-Knopf, PC beendet): nach links hinaus. auge = Drehpunkt (Kopf jetzt).</summary>
    public void Ausblenden(double t, Vec3? auge = null)
    {
        if (Phase is DesktopPhase.Aus or DesktopPhase.Raus) return;
        if (auge is { } a && Lage is { } l)
        {
            // Drehpunkt auf den Kopf von jetzt: das Bild steht weiter, wo es ist (beim jetzigen Anteil), und gleitet um den Kopf von jetzt hinaus
            var (m, gi) = Pose(l, Anteil);
            Lage = Gedreht(new DesktopLage(a, gi, m), -(1 - Anteil) * AusGrad * Num.Grad);
        }
        griff = false;
        Phase = DesktopPhase.Raus;
        von = Anteil;
        abT = t;
        AusgeblendetT = t;
    }

    /// <summary>Als wegwischbares Ziel (für das gemeinsame Wegwischen von V1a): nur wenn der Bildschirm da ist.</summary>
    public WegwischZiel? Ziel(DesktopMasse m)
    {
        if (!Da || Jetzt is not { } j) return null;
        var normale = XrGesten.Vorn(j.Gieren) * -1;
        var mitte = j.Mitte + normale * (m.Tiefe / 2);
        return new WegwischZiel("desktop", new TabletPose(mitte, normale, XrGesten.Rechts(j.Gieren), Vec3.Hoch, m.Sehne, m.HoeheM), EingeblendetT);
    }

    /// <summary>Lage von Griff, Schliessen-Knopf und Monitor-Knopf (lokal; unter dem Bildschirm, in der Mitte).</summary>
    public static (Vec3 Griff, Vec3 Schliessen, Vec3 Monitor) Teile(DesktopMasse m)
    {
        var y = -m.HoeheM / 2 - GriffAbstand - GriffH / 2;
        var seit = GriffB / 2 + KnopfAbstand + KnopfRadius;
        return (DesktopFlaeche.Punkt(0, y, m.RadiusM), DesktopFlaeche.Punkt(-seit, y, m.RadiusM), DesktopFlaeche.Punkt(seit, y, m.RadiusM));
    }

    /// <summary>
    /// Griff und Knöpfe (nur wenn da): pinch/pinchPunkt der Hand am Griff fasst und verschiebt (der Bildschirm dreht sich zum Kopf); die Fingerspitze im
    /// Schliessen- bzw. Monitor-Knopf löst einmal aus, bis sie sich wieder entfernt. kopf = Augen jetzt.
    /// </summary>
    public DesktopBedienStand Bedienen(double t, Vec3 kopf, Vec3? pinchPunkt, bool pinch, Vec3? fingerspitze, DesktopMasse m, bool mehrereMonitore)
    {
        if (!Da || Lage is null || Jetzt is not { } j) { griff = false; return default; }
        var (gr, sc, mo) = Teile(m);
        Vec3? L(Vec3? p) => p is { } q ? DesktopFlaeche.Lokal(q, j.Mitte, j.Gieren) : null;
        var pl = L(pinchPunkt);
        var fl = L(fingerspitze);
        bool AmGriff(Vec3 p) => Math.Abs(p.X - gr.X) <= GriffB / 2 + FangM && Math.Abs(p.Y - gr.Y) <= GriffH / 2 + FangM && Math.Abs(p.Z - gr.Z) <= FangM + 0.02;
        var griffNah = (pl is { } a && AmGriff(a)) || (fl is { } b && AmGriff(b));
        if (griff)
        {
            if (!pinch || pinchPunkt is null) griff = false;
            else
            {
                var neu = pinchPunkt.Value + griffVersatz;
                var blick = neu - kopf;
                var gieren = Num.Hypot(blick.X, blick.Z) > 0.05 ? VrLogik.GierenVonRichtung(new Xz(blick.X, blick.Z)) : j.Gieren;
                Lage = new DesktopLage(kopf, gieren, neu);
            }
        }
        else if (pinch && pl is { } p0 && AmGriff(p0))
        {
            griff = true;
            griffVersatz = j.Mitte - pinchPunkt!.Value;
        }
        var schliessenNah = fl is { } f1 && f1.Abstand(sc) <= KnopfRadius + 0.012;
        var monitorNah = mehrereMonitore && fl is { } f2 && f2.Abstand(mo) <= KnopfRadius + 0.012;
        var weg = fl is not { } f3 || (f3.Abstand(sc) > KnopfRadius + FangM && (!mehrereMonitore || f3.Abstand(mo) > KnopfRadius + FangM));
        bool schliessen = false, wechseln = false;
        if (!knopfGesperrt && !griff)
        {
            if (schliessenNah) { schliessen = true; knopfGesperrt = true; }
            else if (monitorNah) { wechseln = true; knopfGesperrt = true; }
        }
        if (weg) knopfGesperrt = false;
        if (schliessen) Ausblenden(t, kopf);
        return new DesktopBedienStand(griffNah, griff, schliessenNah, schliessen, monitorNah, wechseln);
    }
}
