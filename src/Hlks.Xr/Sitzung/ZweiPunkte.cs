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

    public bool MitZweiPunkten => Variante != Bedienung.Varianten.GriffeEinGriff;

    public static readonly ZweiPunkteEinstellungen Vorgabe = new();
}

/// <summary>Die beiden Punkte und das Zentrum (Raum-lokal).</summary>
public readonly record struct ZweiPunkteLage(Vec3 Verschieben, Vec3 Drehen, Vec3 Zentrum);

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

    public ZweiPunkteZustand(SitzungLage lage) { Lage = lage; }
}

/// <summary>Ereignis der zwei Punkte in einem Bild.</summary>
public enum ZweiPunkteEreignis { Nichts, Gefasst, Losgelassen }

public static class ZweiPunkte
{
    /// <summary>V1f: so nah (m) an einem Punkt muss die Faust beim Zugreifen sein (Annahme V1f; der Pinch: XrSitzungLogik.GriffFangM 0.05 m).</summary>
    public const double FaustFangM = 0.08;

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
    /// Unterkante; das Zentrum ist die Mitte des Rechtecks auf der Unterkante.
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
        Vec3 Raum(Xz p) => SitzungRaum.ModellZuRaum(new Vec3(p.X, bezug.Unten, p.Z), lage, bezug);
        var a = Raum(r.Punkt(best.U0, best.V0));
        var b = Raum(r.Punkt(best.U1, best.V1));
        var mitte = new Vec3((a.X + b.X) / 2, a.Y, (a.Z + b.Z) / 2);
        var zentrum = Raum(r.Punkt((r.UMin + r.UMax) / 2, (r.VMin + r.VMax) / 2));
        var n = XrSitzungLogik.DreheXZ(best.N, lage.Drehung);
        var ecke = a.X >= b.X ? a : b;   // die rechte Ecke (im Raum, vom Platz am Tisch aus)
        var entlang = new Xz(ecke.X - mitte.X, ecke.Z - mitte.Z);
        var el = Math.Sqrt(entlang.X * entlang.X + entlang.Z * entlang.Z);
        var schraeg = el > 1e-9 ? new Xz((n.X + entlang.X / el) / Math.Sqrt(2), (n.Z + entlang.Z / el) / Math.Sqrt(2)) : n;
        var y = XrSitzungLogik.TischHoehe + lage.Hoehe + XrSitzungLogik.GriffHoeheM;
        var g = XrSitzungLogik.GriffAbstandM;
        return new ZweiPunkteLage(new Vec3(mitte.X + n.X * g, y, mitte.Z + n.Z * g), new Vec3(ecke.X + schraeg.X * g, y, ecke.Z + schraeg.Z * g),
            new Vec3(zentrum.X, XrSitzungLogik.TischHoehe + lage.Hoehe, zentrum.Z));
    }

    /// <summary>
    /// Ein Schritt je Bild: links/rechts = die Hände (null = nicht erkannt; nur Pinch, Punkt und Frei zählen), punkte = Lage der Punkte (<see cref="Punkte"/>, aus der
    /// Lage vor diesem Bild). Neue Lage in z.Lage; das Ereignis dieses Bilds.
    /// </summary>
    public static ZweiPunkteEreignis Schritt(ZweiPunkteZustand z, EinGriffHand? links, EinGriffHand? rechts, ZweiPunkteLage punkte, ZweiPunkteEinstellungen? e = null,
        ZoomGrenzen? zoom = null)
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
            return ZweiPunkteEreignis.Gefasst;
        }
        var halt = z.Hand == "links" ? links : rechts;
        if (halt is not { Pinch: true })
        {
            z.Hand = null;
            z.Punkt = TischPunkt.Keiner;
            return ZweiPunkteEreignis.Losgelassen;
        }
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
        z.Lage = XrSitzungLogik.LageBegrenzen(new SitzungLage(n, theta, mitte.X - XrSitzungLogik.TischMitte.X, mitte.Z - XrSitzungLogik.TischMitte.Z, z.Lage0.Hoehe), zoom);
        return ZweiPunkteEreignis.Nichts;
    }

    /// <summary>Hält diese Hand («links», «rechts») gerade einen Punkt?</summary>
    public static bool HandAmPunkt(ZweiPunkteZustand z, string hand) => z.Hand == hand;
}
