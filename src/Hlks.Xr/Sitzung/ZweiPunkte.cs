// Quest App V1e (Brillentest, Christian 24.10.2026): «modell auf tisch soll einen verschieben punkt erhalten, und einen punkt für drehen und vergrössern diese bewegung
// dreht immer um das zentrum. beide punkte sollen farblich unterschiedlich sein, und ein kleines symbol beim greiffen erhalten.» Ersetzt als Vorgabe den einen Griff von
// V1d (Variante «griffe_tisch» = «zwei_punkte» | «ein_griff»). Rein, ohne StereoKit; alles Raum-lokal (XrSitzungLogik).
//   - Punkt «Verschieben»: Mitte der vorderen Kante des sichtbar gezeichneten Gebäudes (wie der Griff von V1d, EinGriff.GriffPunkt). Gefasst (Pinch höchstens GriffFangM
//     vom Punkt) folgt das Modell der Hand in allen Richtungen, auch in der Höhe – ohne Drehung und ohne Änderung des Massstabs.
//   - Punkt «Drehen und Vergrössern»: an der rechten vorderen Ecke des sichtbaren Rechtecks (gut erreichbar für die rechte Hand), schräg nach aussen. Das Modell dreht
//     IMMER um sein Zentrum (Mitte des sichtbaren Rechtecks auf dem Tisch, beim Fassen festgehalten), nie um den Griff: der Winkel der Hand um das Zentrum in der
//     Tischebene ergibt die Drehung, das Verhältnis der Abstände Hand ↔ Zentrum (jetzt zu beim Fassen, waagrecht) den Massstab (grösser = Modell grösser; Grenzen
//     «sitzung.zoom_min/max», 1:5 … 1:200). Kleine Ausschläge um den Startpunkt zählen nicht (weich abgezogene Totzonen «sitzung.dreh_totzone_grad» und
//     «sitzung.zoom_totzone»), damit Drehen nicht ungewollt zoomt und umgekehrt. Die Höhe bleibt.
//   - Es wirkt immer nur ein Punkt (der zuerst gefasste); eine Hand fasst nie beide. Nach dem Loslassen sitzt jeder Punkt wieder an seinem Platz am Modell.
// Jede Lage wird aus dem Anfang des Griffs gerechnet (kein Aufsummieren). Alle Zahlen sind Annahmen V1e, keine Normwerte.
// V1f (Vorschlag zum Auftrag «Pistolengeste statt Pinch»): mit der Variante «ausloeser» = «pistole» fasst die FAUST – mit der ganzen Hand zugreifen (die App meldet die
// geschlossene Hand als «Pinch» der EinGriffHand, Punkt = Innenseite der Faust); weil die Hand grösser ist als die Fingerspitzen, fängt sie im Umkreis FaustFangM (Annahme V1f).
// V1g (Christian 24.10.2026: «durch oben unten des roten punkte soll das modell auch rotieren» – Kippen): am Punkt «Drehen und Vergrössern» kippt die Höhe der Hand das
// Modell um eine waagrechte Achse durch das Zentrum, quer zur Blickrichtung beim Greifen (vom Kopf zum Zentrum): die Neigung folgt dem Winkel der Hand über der Unterkante
// (Höhe gegen den waagrechten Abstand zum Zentrum beim Fassen – nach aussen bzw. innen zoomt nur), mit der Totzone «sitzung.dreh_totzone_grad» und höchstens «sitzung.kipp_max_grad». Links/rechts dreht weiter, aussen/innen
// zoomt. Ist das Modell schon gekippt, bleiben Achse und Drehpunkt. Ein Doppeltipp am Punkt (zweimal kurz fassen) stellt die Neigung auf 0. Kippen.cs hebt das Modell,
// damit es nicht in den Tisch taucht. Annahmen V1g.

namespace Hlks.Xr.Sitzung;

/// <summary>Stellschrauben der zwei Punkte (V1e).</summary>
public sealed record ZweiPunkteEinstellungen
{
    /// <summary>Variante «griffe_tisch»: <see cref="Bedienung.Varianten.GriffeZweiPunkte"/> (Vorgabe) | <see cref="Bedienung.Varianten.GriffeEinGriff"/> (V1d).</summary>
    public string Variante { get; init; } = Bedienung.Varianten.GriffeZweiPunkte;
    /// <summary>«sitzung.dreh_totzone_grad»: so viel Drehung um das Zentrum zählt nicht (Annahme V1e 3°).</summary>
    public double DrehTotzoneGrad { get; init; } = 3;
    /// <summary>«sitzung.zoom_totzone»: so viel Änderung des Abstands zum Zentrum (Anteil) zählt nicht (Annahme V1e 0.05 = 5 %).</summary>
    public double ZoomTotzone { get; init; } = 0.05;
    /// <summary>V1f: die Punkte fasst die Faust statt des Pinch (die App setzt es aus der Variante «ausloeser»).</summary>
    public bool Faust { get; init; }
    /// <summary>V1g, «sitzung.kipp_max_grad»: höchstens so weit (Grad) kippt das Modell nach vorn bzw. hinten (Annahme V1g 60°).</summary>
    public double KippMaxGrad { get; init; } = 60;

    public bool MitZweiPunkten => Variante != Bedienung.Varianten.GriffeEinGriff;

    public static readonly ZweiPunkteEinstellungen Vorgabe = new();
}

/// <summary>Die beiden Punkte und das Zentrum (Raum-lokal); V1g: das Zentrum in der Szene (x, z; Drehpunkt der Kippung).</summary>
public readonly record struct ZweiPunkteLage(Vec3 Verschieben, Vec3 Drehen, Vec3 Zentrum, Xz ZentrumSzene = default);

/// <summary>Welcher Punkt.</summary>
public enum TischPunkt { Keiner, Verschieben, Drehen }

/// <summary>Zustand der zwei Punkte.</summary>
public sealed class ZweiPunkteZustand
{
    public SitzungLage Lage { get; internal set; }
    /// <summary>Gefasster Punkt (Keiner = frei).</summary>
    public TischPunkt Punkt { get; internal set; }
    /// <summary>Welche Hand ihn hält («links», «rechts»), sonst null.</summary>
    public string? Hand { get; internal set; }
    public bool Gehalten => Hand is not null;
    /// <summary>Drehung (rad) und Faktor des Abstands seit dem Fassen (nach den Totzonen; Prüfung, Anzeige).</summary>
    public double Drehung { get; internal set; }
    public double Faktor { get; internal set; } = 1;
    internal SitzungLage Lage0 = null!;
    internal Vec3 H0;
    internal Xz C;            // Zentrum beim Fassen
    internal Xz Q;            // Mitte des Modells relativ zum Zentrum, gegen die Drehung gedreht, im Modellmass (× Massstab)
    internal double Phi0, R0;
    internal bool PinchL, PinchR;
    // V1g: Kippen (Winkel der Hand beim Fassen, Unterkante, Neigung beim Fassen, Achse, Drehpunkt) und Doppeltipp
    internal double A0, Yb, Kipp0;
    internal Xz KippAchse = new(-1, 0), KippPunkt;
    internal double GefasstT = double.NaN, LosT = double.NaN;
    internal Vec3 LetzterOrt, LosOrt;
    internal bool KurzGehalten;
    /// <summary>V1g: Neigung des Modells (rad; Prüfung, Anzeige).</summary>
    public double Neigung => Lage.Kippung?.Winkel ?? 0;

    public ZweiPunkteZustand(SitzungLage lage) { Lage = lage; }
}

/// <summary>Ereignis der zwei Punkte in einem Bild.</summary>
public enum ZweiPunkteEreignis { Nichts, Gefasst, Losgelassen, NeigungZurueck }

public static class ZweiPunkte
{
    /// <summary>V1f: so nah (m) an einem Punkt muss die Faust beim Zugreifen sein (Annahme V1f; der Pinch: XrSitzungLogik.GriffFangM 0.05 m).</summary>
    public const double FaustFangM = 0.08;
    /// <summary>V1g, Doppeltipp: ein kurzes Fassen (höchstens TippMaxS, die Hand höchstens TippMaxM bewegt), dann innert DoppeltippS wieder fassen (höchstens DoppeltippM
    /// daneben) stellt die Neigung auf 0. Annahmen V1g.</summary>
    public const double TippMaxS = 0.4, TippMaxM = 0.03, DoppeltippS = 0.5, DoppeltippM = 0.06;

    /// <summary>
    /// V1f: hält die Hand eine Faust zum Zugreifen? vorher = sie hielt schon (Hysterese XrTabletLogik.FaustAn / FaustLos). Die Pistolengeste (Zeigefinger gestreckt) ist keine
    /// neue Faust – sie zielt.
    /// </summary>
    public static bool Faust(IReadOnlyDictionary<string, Vec3>? g, bool vorher) => XrTabletLogik.IstFaust(g, vorher) && (vorher || !Bedienung.Pistole.Haltung(g));

    /// <summary>V1f: der Punkt der Faust – die Mitte zwischen Handfläche und Mittelgelenk des Mittelfingers (innen in der geschlossenen Hand); null ohne Gelenke.</summary>
    public static Vec3? FaustPunkt(IReadOnlyDictionary<string, Vec3>? g) =>
        XrTabletLogik.HandflaechenMitte(g) is { } m && g!.TryGetValue("middle-finger-phalanx-intermediate", out var p) && p.IstEndlich ? Vec3.Mitte(m, p) : null;

    /// <summary>Weich abgezogene Totzone: |x| − tz mit dem Vorzeichen von x, darunter 0.</summary>
    static double Totzone(double x, double tz) => Math.Abs(x) <= tz ? 0 : x - Math.Sign(x) * tz;

    /// <summary>Neuer Zustand (Startlage, Zurücksetzen): nichts gehalten.</summary>
    public static ZweiPunkteZustand Neu(SitzungLage lage) => new(lage);

    /// <summary>
    /// Lage der beiden Punkte und des Zentrums (Raum-lokal): Verschieben in der Mitte der vorderen Seite des sichtbaren Rechtecks (ohne Rechteck die Box des Bezugs),
    /// Drehen an ihrer rechten Ecke, je <see cref="XrSitzungLogik.GriffAbstandM"/> nach aussen (an der Ecke schräg), <see cref="XrSitzungLogik.GriffHoeheM"/> über der
    /// Unterkante; das Zentrum ist die Mitte des Rechtecks auf der Unterkante. V1g: gerechnet ohne Kippung, dann mit dem Modell gekippt.
    /// </summary>
    public static ZweiPunkteLage Punkte(SitzungLage lage, SitzungBezug bezug, SichtbarRahmen? sichtbar)
    {
        var r = sichtbar is { Gueltig: true } s ? s
            : new SichtbarRahmen(1, 0, bezug.Anker.X - bezug.Groesse.B / 2, bezug.Anker.X + bezug.Groesse.B / 2, bezug.Anker.Z - bezug.Groesse.T / 2, bezug.Anker.Z + bezug.Groesse.T / 2);
        var seiten = new (Xz N, double U0, double V0, double U1, double V1)[]
        {
            (new Xz(-r.Uz, r.Ux), r.UMin, r.VMax, r.UMax, r.VMax),
            (new Xz(r.Ux, r.Uz), r.UMax, r.VMin, r.UMax, r.VMax),
            (new Xz(-r.Ux, -r.Uz), r.UMin, r.VMin, r.UMin, r.VMax),
            (new Xz(r.Uz, -r.Ux), r.UMin, r.VMin, r.UMax, r.VMin),
        };
        var best = seiten[0];
        var bestZ = XrSitzungLogik.DreheXZ(best.N, lage.Drehung).Z;
        foreach (var x in seiten.Skip(1))
        {
            var z = XrSitzungLogik.DreheXZ(x.N, lage.Drehung).Z;
            if (z > bestZ + 1e-9) { best = x; bestZ = z; }
        }
        Vec3 Raum(Xz p) => SitzungRaum.ModellZuRaumOhneKippung(new Vec3(p.X, bezug.Unten, p.Z), lage, bezug);
        var a = Raum(r.Punkt(best.U0, best.V0));
        var b = Raum(r.Punkt(best.U1, best.V1));
        var mitte = new Vec3((a.X + b.X) / 2, a.Y, (a.Z + b.Z) / 2);
        var zentrumSzene = r.Punkt((r.UMin + r.UMax) / 2, (r.VMin + r.VMax) / 2);
        var zentrum = Raum(zentrumSzene);
        var n = XrSitzungLogik.DreheXZ(best.N, lage.Drehung);
        var ecke = a.X >= b.X ? a : b;   // die rechte Ecke (im Raum, vom Platz am Tisch aus)
        var entlang = new Xz(ecke.X - mitte.X, ecke.Z - mitte.Z);
        var el = Math.Sqrt(entlang.X * entlang.X + entlang.Z * entlang.Z);
        var schraeg = el > 1e-9 ? new Xz((n.X + entlang.X / el) / Math.Sqrt(2), (n.Z + entlang.Z / el) / Math.Sqrt(2)) : n;
        var y = XrSitzungLogik.TischHoehe + lage.Hoehe + XrSitzungLogik.GriffHoeheM;
        var g = XrSitzungLogik.GriffAbstandM;
        Vec3 K(Vec3 q) => Kippen.Anwenden(q, lage, bezug);   // V1g: die Punkte kippen mit dem Modell
        return new ZweiPunkteLage(K(new Vec3(mitte.X + n.X * g, y, mitte.Z + n.Z * g)), K(new Vec3(ecke.X + schraeg.X * g, y, ecke.Z + schraeg.Z * g)),
            K(new Vec3(zentrum.X, XrSitzungLogik.TischHoehe + lage.Hoehe, zentrum.Z)), zentrumSzene);
    }

    /// <summary>
    /// Ein Schritt je Bild: links/rechts = die Hände (null = nicht erkannt; nur Pinch, Punkt und Frei zählen), punkte = Lage der Punkte (<see cref="Punkte"/>, aus der
    /// Lage vor diesem Bild). Neue Lage in z.Lage; das Ereignis dieses Bilds. V1g: kopf = Kopf (Raum-lokal; die Achse der Kippung liegt quer zur Richtung Kopf → Zentrum,
    /// ohne Kopf quer zu −z), bezug und sichtbar für das Anheben (ohne Bezug hebt sich das Modell nicht), t = Zeit (s) für den Doppeltipp (ohne Zeit keiner).
    /// </summary>
    public static ZweiPunkteEreignis Schritt(ZweiPunkteZustand z, EinGriffHand? links, EinGriffHand? rechts, ZweiPunkteLage punkte, ZweiPunkteEinstellungen? e = null,
        ZoomGrenzen? zoom = null, Vec3? kopf = null, SitzungBezug? bezug = null, SichtbarRahmen? sichtbar = null, double t = double.NaN)
    {
        e ??= ZweiPunkteEinstellungen.Vorgabe;
        bool neuL = links is { Pinch: true } && !z.PinchL, neuR = rechts is { Pinch: true } && !z.PinchR;
        z.PinchL = links?.Pinch == true;
        z.PinchR = rechts?.Pinch == true;
        if (z.Hand is null)
        {
            // fassen: ein neuer Pinch an einem der Punkte (der nähere Punkt; bei zwei Händen die nähere Hand, links vor rechts bei Gleichstand)
            (TischPunkt P, double D) Naechster(EinGriffHand h)
            {
                double dv = h.Punkt.Abstand(punkte.Verschieben), dd = h.Punkt.Abstand(punkte.Drehen);
                return dv <= dd ? (TischPunkt.Verschieben, dv) : (TischPunkt.Drehen, dd);
            }
            var l = neuL && links!.Frei ? Naechster(links) : (TischPunkt.Keiner, double.PositiveInfinity);
            var r = neuR && rechts!.Frei ? Naechster(rechts) : (TischPunkt.Keiner, double.PositiveInfinity);
            var (wer, p, d) = l.Item2 <= r.Item2 ? ("links", l.Item1, l.Item2) : ("rechts", r.Item1, r.Item2);
            if (d > (e.Faust ? FaustFangM : XrSitzungLogik.GriffFangM)) return ZweiPunkteEreignis.Nichts;
            var h = wer == "links" ? links! : rechts!;
            z.Hand = wer;
            z.Punkt = p;
            // V1g: Doppeltipp am Punkt «Drehen» (kurz gefasst, gleich wieder gefasst): die Neigung auf 0
            var ereignis = ZweiPunkteEreignis.Gefasst;
            if (p == TischPunkt.Drehen && z.KurzGehalten && double.IsFinite(t) && t - z.LosT <= DoppeltippS + 1e-9 && h.Punkt.Abstand(z.LosOrt) <= DoppeltippM + 1e-9
                && z.Lage.Kippung is not null)
            {
                z.Lage = z.Lage with { Kippung = null };
                ereignis = ZweiPunkteEreignis.NeigungZurueck;
            }
            z.KurzGehalten = false;
            z.GefasstT = t;
            z.LetzterOrt = h.Punkt;
            z.Lage0 = z.Lage;
            z.H0 = h.Punkt;
            z.C = new Xz(punkte.Zentrum.X, punkte.Zentrum.Z);
            var m0 = XrSitzungLogik.ModellMitte(z.Lage);
            var q = XrSitzungLogik.DreheXZ(new Xz(m0.X - z.C.X, m0.Z - z.C.Z), -z.Lage.Drehung);
            z.Q = new Xz(q.X * z.Lage.Massstab, q.Z * z.Lage.Massstab);
            var hc = new Xz(h.Punkt.X - z.C.X, h.Punkt.Z - z.C.Z);
            z.Phi0 = XrSitzungLogik.GierenVon(hc);
            z.R0 = Math.Max(XrSitzungLogik.MinHandabstand, Math.Sqrt(hc.X * hc.X + hc.Z * hc.Z));
            z.Drehung = 0;
            z.Faktor = 1;
            // V1g: Kippen – Winkel der Hand über der Unterkante beim Fassen; die Achse quer zur Richtung Kopf → Zentrum (ist das Modell schon gekippt, bleiben Achse und Punkt)
            z.Yb = XrSitzungLogik.TischHoehe + z.Lage.Hoehe;
            z.A0 = Math.Atan2(h.Punkt.Y - z.Yb, z.R0);
            if (z.Lage.Kippung is { } k0) { z.Kipp0 = k0.Winkel; z.KippAchse = k0.Achse; z.KippPunkt = k0.Punkt; }
            else
            {
                var b = kopf is { } kp ? new Xz(z.C.X - kp.X, z.C.Z - kp.Z) : new Xz(0, -1);
                var bl = Math.Sqrt(b.X * b.X + b.Z * b.Z);
                b = bl > 0.05 ? new Xz(b.X / bl, b.Z / bl) : new Xz(0, -1);
                z.Kipp0 = 0;
                z.KippAchse = new Xz(b.Z, -b.X);   // nach links vom Betrachter: ein positiver Winkel hebt die Seite zu ihm
                z.KippPunkt = punkte.ZentrumSzene;
            }
            return ereignis;
        }
        var halt = z.Hand == "links" ? links : rechts;
        if (halt is not { Pinch: true })
        {
            // V1g: ein kurzes Fassen am Punkt «Drehen» (erster Tipp eines Doppeltipps)?
            var ort = halt?.Punkt ?? z.LetzterOrt;
            z.KurzGehalten = z.Punkt == TischPunkt.Drehen && double.IsFinite(t) && t - z.GefasstT <= TippMaxS + 1e-9 && ort.Abstand(z.H0) <= TippMaxM + 1e-9;
            z.LosT = t;
            z.LosOrt = ort;
            z.Hand = null;
            z.Punkt = TischPunkt.Keiner;
            return ZweiPunkteEreignis.Losgelassen;
        }
        z.LetzterOrt = halt.Punkt;
        if (z.Punkt == TischPunkt.Verschieben)
        {
            // verschieben: in allen Richtungen, auch der Höhe; Drehung und Massstab bleiben
            var d = halt.Punkt - z.H0;
            z.Lage = XrSitzungLogik.LageBegrenzen(z.Lage0 with { X = z.Lage0.X + d.X, Z = z.Lage0.Z + d.Z, Hoehe = z.Lage0.Hoehe + d.Y }, zoom);
            return ZweiPunkteEreignis.Nichts;
        }
        // drehen und vergrössern um das Zentrum: Winkel der Hand um das Zentrum, Verhältnis der Abstände (je mit Totzone)
        var hc1 = new Xz(halt.Punkt.X - z.C.X, halt.Punkt.Z - z.C.Z);
        var r1 = Math.Max(XrSitzungLogik.MinHandabstand, Math.Sqrt(hc1.X * hc1.X + hc1.Z * hc1.Z));
        var dphi = Totzone(XrSitzungLogik.WinkelDiff(XrSitzungLogik.GierenVon(hc1), z.Phi0), e.DrehTotzoneGrad * Num.Grad);
        var lk = Totzone(Math.Log(r1 / z.R0), Math.Log(1 + Math.Max(0, e.ZoomTotzone)));
        var n = XrSitzungLogik.MassstabBegrenzen(z.Lage0.Massstab / Math.Exp(lk), zoom);
        var theta = z.Lage0.Drehung + dphi;
        var rel = XrSitzungLogik.DreheXZ(new Xz(z.Q.X / n, z.Q.Z / n), theta);
        var mitte = new Xz(z.C.X + rel.X, z.C.Z + rel.Z);
        z.Drehung = dphi;
        z.Faktor = z.Lage0.Massstab / n;
        var neu = XrSitzungLogik.LageBegrenzen(new SitzungLage(n, theta, mitte.X - XrSitzungLogik.TischMitte.X, mitte.Z - XrSitzungLogik.TischMitte.Z, z.Lage0.Hoehe), zoom);
        // V1g: kippen – der Winkel der Hand über der Unterkante gegen beim Fassen, im waagrechten Abstand beim Fassen gemessen (nach aussen bzw. innen zoomt nur, kippt nicht);
        // Totzone wie die Drehung, höchstens KippMaxGrad; das Modell hebt sich, wenn nötig
        var dk = Totzone(Math.Atan2(halt.Punkt.Y - z.Yb, z.R0) - z.A0, e.DrehTotzoneGrad * Num.Grad);
        var kmax = Math.Max(0, e.KippMaxGrad) * Num.Grad;
        var winkel = Math.Clamp(z.Kipp0 + dk, -kmax, kmax);
        if (Math.Abs(winkel) > 1e-12)
        {
            neu = neu with { Kippung = new SitzungKippung(winkel, z.KippAchse, z.KippPunkt) };
            if (bezug is not null) neu = neu with { Kippung = neu.Kippung! with { Anheben = Kippen.Anheben(neu, bezug, sichtbar) } };
        }
        z.Lage = neu;
        return ZweiPunkteEreignis.Nichts;
    }

    /// <summary>Hält diese Hand («links», «rechts») gerade einen Punkt?</summary>
    public static bool HandAmPunkt(ZweiPunkteZustand z, string hand) => z.Hand == hand;
}
