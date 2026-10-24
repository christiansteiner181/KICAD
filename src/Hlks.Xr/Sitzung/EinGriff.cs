// Quest App V1d (Brillentest V1, Christian 24.10.2026): «das modell soll nur mit einem griff bewegt und gezoomt werden.» Ersetzt am Sitzungstisch die zwei Griffe an
// den vorderen Gebäudeecken (P4b-Nachtrag, XrSitzungLogik.GestenSchritt bleibt als Regel des Prototyps). Rein, ohne StereoKit; alles Raum-lokal (XrSitzungLogik).
//   - Der Griff: eine Kugel in der Mitte der vorderen Kante des sichtbar gezeichneten Gebäudes (Hüllenlogik von P4b: SichtbarRahmen in den Achsen des Gebäudes),
//     GriffAbstandM nach aussen, GriffHoeheM über der Unterkante des Modells. «Vorne» ist die Seite, deren Aussenrichtung in der Lage des Modells (mit seiner Drehung)
//     am meisten zum Platz am Tisch (+z) zeigt – nach einer Drehung kommt der Griff so wieder auf die Seite zum Menschen.
//   - Fassen: ein Pinch, der höchstens GriffFangM vom Griff beginnt. Solange er hält, ist das Modell an die Hand gebunden: es folgt ihr in allen Richtungen (auch der
//     Höhe) und dreht mit dem Gierwinkel der Hand (Handgelenk → Grundgelenk des Mittelfingers; kleines Zittern unter DrehTotzoneGrad zählt nicht). Der Punkt des
//     Modells unter der Hand bleibt unter der Hand (auch beim Zoomen).
//   - Zoomen (Variante «zoom_ein_griff»):
//       «zweite_hand» (Vorgabe): hält eine Hand den Griff, zoomt ein Pinch der anderen Hand über den Abstand der Hände (auseinander = grösser), um die Griffhand.
//       «drehknopf»: Rollen des Handgelenks am Griff (um die Achse der Hand); im Uhrzeigersinn aus Sicht des Menschen vergrössert, «sitzung.drehknopf_grad» je
//       Verdoppelung (Annahme V1d 60°); unter RollTotzoneGrad zählt nicht.
//     Massstab in den Grenzen «sitzung.zoom_min/max» (1:5 … 1:200), die Mitte bleibt auf der Platte (XrSitzungLogik.LageBegrenzen).
//   - Jede Lage wird aus dem Anfang des Griffs gerechnet (kein Aufsummieren), ein Wechsel (zweite Hand dazu bzw. weg) setzt einen neuen Bezug, damit nichts springt.
// Alle Zahlen sind Annahmen V1d (Brillentest), keine Normwerte.

namespace Hlks.Xr.Sitzung;

/// <summary>Stellschrauben des einen Griffs (V1d).</summary>
public sealed record EinGriffEinstellungen
{
    /// <summary>Variante «zoom_ein_griff»: <see cref="Bedienung.Varianten.ZoomZweiteHand"/> (Vorgabe) | <see cref="Bedienung.Varianten.ZoomDrehknopf"/>.</summary>
    public string Zoom { get; init; } = Bedienung.Varianten.ZoomZweiteHand;
    /// <summary>«sitzung.drehknopf_grad»: so weit (Grad) das Handgelenk rollen für den doppelten bzw. halben Massstab (Annahme V1d 60°).</summary>
    public double DrehknopfGrad { get; init; } = 60;

    public static readonly EinGriffEinstellungen Vorgabe = new();
}

/// <summary>Eine Hand für den Griff in einem Bild (Raum-lokal): Pinch, Pinch-Punkt, Gierwinkel der Hand (null = unbekannt), Rollen (rad, null = unbekannt), frei (nicht an den Apps).</summary>
public sealed record EinGriffHand(bool Pinch, Vec3 Punkt, double? Gieren, double? Rollen, bool Frei = true);

/// <summary>Zustand des einen Griffs.</summary>
public sealed class EinGriffZustand
{
    public SitzungLage Lage { get; internal set; }
    /// <summary>Welche Hand hält den Griff («links», «rechts»), sonst null.</summary>
    public string? Hand { get; internal set; }
    /// <summary>Zoomt gerade die zweite Hand (Variante «zweite_hand»)?</summary>
    public bool ZweiteHand { get; internal set; }
    public bool Gehalten => Hand is not null;
    internal SitzungLage Lage0 = null!;
    internal Vec3 H0;
    internal double? G0, R0;
    internal Xz Q;            // Punkt des Modells unter der Hand, gegen die Drehung gedreht, im Modellmass (× Massstab)
    internal double N0;       // Massstab beim Bezug
    internal double D0;       // Abstand der Hände beim Beginn des Zoomens
    internal bool PinchL, PinchR;

    public EinGriffZustand(SitzungLage lage) { Lage = lage; }
}

/// <summary>Ereignis des einen Griffs in einem Bild.</summary>
public enum EinGriffEreignis { Nichts, Gefasst, Losgelassen, ZoomBeginnt, ZoomEndet }

public static class EinGriff
{
    /// <summary>Gierwinkel der Hand: kleiner als so viel (Grad) zählt nicht (Zittern beim Verschieben), Annahme V1d.</summary>
    public const double DrehTotzoneGrad = 2;
    /// <summary>Rollen (Drehknopf): kleiner als so viel (Grad) zählt nicht, Annahme V1d.</summary>
    public const double RollTotzoneGrad = 3;

    /// <summary>Weich abgezogene Totzone: |x| − tz mit dem Vorzeichen von x, darunter 0.</summary>
    static double Totzone(double x, double tz) => Math.Abs(x) <= tz ? 0 : x - Math.Sign(x) * tz;

    /// <summary>
    /// Lage des Griffs (Raum-lokal, siehe Dateikopf): Mitte der vorderen Seite des sichtbaren Rechtecks (ohne Rechteck die Box des Bezugs), GriffAbstandM nach aussen,
    /// GriffHoeheM über der Unterkante des Modells.
    /// </summary>
    public static Vec3 GriffPunkt(SitzungLage lage, SitzungBezug bezug, SichtbarRahmen? sichtbar)
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
        var a = r.Punkt(best.U0, best.V0);
        var b = r.Punkt(best.U1, best.V1);
        var mitte = SitzungRaum.ModellZuRaum(new Vec3((a.X + b.X) / 2, bezug.Unten, (a.Z + b.Z) / 2), lage, bezug);
        var n = XrSitzungLogik.DreheXZ(best.N, lage.Drehung);
        return new Vec3(mitte.X + n.X * XrSitzungLogik.GriffAbstandM, XrSitzungLogik.TischHoehe + lage.Hoehe + XrSitzungLogik.GriffHoeheM, mitte.Z + n.Z * XrSitzungLogik.GriffAbstandM);
    }

    /// <summary>
    /// Rollen der Hand (rad): Winkel der Handflächennormale um die Achse der Hand (Handgelenk → Grundgelenk des Mittelfingers) gegen «oben»; positiv = im Uhrzeigersinn
    /// aus Sicht des Menschen (von hinten entlang der Achse). null, wenn Gelenke fehlen oder die Achse fast senkrecht steht.
    /// </summary>
    public static double? Rollen(IReadOnlyDictionary<string, Vec3>? g, string seite)
    {
        if (g is null || !g.TryGetValue("wrist", out var w) || !g.TryGetValue("middle-finger-phalanx-proximal", out var m)) return null;
        if (XrTabletLogik.HandflaechenNormale(g, seite) is not { } n) return null;
        var achse = m - w;
        if (achse.Laenge < 1e-6) return null;
        achse = achse.Einheit();
        var oben = new Vec3(0, 1, 0);
        var bezug = oben - achse * oben.Skalar(achse);
        if (bezug.Laenge < 0.2) return null;   // Achse fast senkrecht
        bezug = bezug.Einheit();
        var nq = n - achse * n.Skalar(achse);
        if (nq.Laenge < 1e-6) return null;
        nq = nq.Einheit();
        return Math.Atan2(achse.Skalar(bezug.Kreuz(nq)), bezug.Skalar(nq));
    }

    /// <summary>Neuer Zustand (Startlage, Zurücksetzen): nichts gehalten.</summary>
    public static EinGriffZustand Neu(SitzungLage lage) => new(lage);

    static void Bezug(EinGriffZustand z, EinGriffHand h)
    {
        z.Lage0 = z.Lage;
        z.H0 = h.Punkt;
        z.G0 = h.Gieren;
        z.R0 = h.Rollen;
        z.N0 = z.Lage.Massstab;
        var m0 = XrSitzungLogik.ModellMitte(z.Lage);
        var q = XrSitzungLogik.DreheXZ(new Xz(m0.X - h.Punkt.X, m0.Z - h.Punkt.Z), -z.Lage.Drehung);
        z.Q = new Xz(q.X * z.N0, q.Z * z.N0);
    }

    /// <summary>
    /// Ein Schritt je Bild: links/rechts = die Hände (null = nicht erkannt), griff = Lage des Griffs (Raum-lokal, <see cref="GriffPunkt"/>), zoom = Grenzen. Neue Lage in
    /// z.Lage; das Ereignis dieses Bilds.
    /// </summary>
    public static EinGriffEreignis Schritt(EinGriffZustand z, EinGriffHand? links, EinGriffHand? rechts, Vec3 griff, EinGriffEinstellungen? e = null, ZoomGrenzen? zoom = null)
    {
        e ??= EinGriffEinstellungen.Vorgabe;
        var ereignis = EinGriffEreignis.Nichts;
        bool neuL = links is { Pinch: true } && !z.PinchL, neuR = rechts is { Pinch: true } && !z.PinchR;
        z.PinchL = links?.Pinch == true;
        z.PinchR = rechts?.Pinch == true;
        // fassen: ein neuer Pinch am Griff (die nähere Hand, links vor rechts bei Gleichstand)
        if (z.Hand is null)
        {
            double dl = neuL && links!.Frei ? links.Punkt.Abstand(griff) : double.PositiveInfinity, dr = neuR && rechts!.Frei ? rechts.Punkt.Abstand(griff) : double.PositiveInfinity;
            var wer = Math.Min(dl, dr) <= XrSitzungLogik.GriffFangM ? (dl <= dr ? "links" : "rechts") : null;
            if (wer is null) return ereignis;
            z.Hand = wer;
            z.ZweiteHand = false;
            Bezug(z, wer == "links" ? links! : rechts!);
            return EinGriffEreignis.Gefasst;
        }
        var halt = z.Hand == "links" ? links : rechts;
        var andere = z.Hand == "links" ? rechts : links;
        var andereNeu = z.Hand == "links" ? neuR : neuL;
        if (halt is not { Pinch: true })
        {
            z.Hand = null;
            z.ZweiteHand = false;
            return EinGriffEreignis.Losgelassen;
        }
        // Zoom mit der zweiten Hand: Beginn (neuer Pinch der anderen Hand, nicht an den Apps) bzw. Ende (sie lässt los) setzt einen neuen Bezug
        if (e.Zoom == Bedienung.Varianten.ZoomZweiteHand)
        {
            if (!z.ZweiteHand && andereNeu && andere is { Frei: true } a0)
            {
                Bezug(z, halt);
                z.ZweiteHand = true;
                z.D0 = Math.Max(XrSitzungLogik.MinHandabstand, a0.Punkt.Abstand(halt.Punkt));
                ereignis = EinGriffEreignis.ZoomBeginnt;
            }
            else if (z.ZweiteHand && andere is not { Pinch: true })
            {
                z.ZweiteHand = false;
                Bezug(z, halt);
                ereignis = EinGriffEreignis.ZoomEndet;
            }
        }
        else z.ZweiteHand = false;
        // Massstab
        var n = z.N0;
        if (z.ZweiteHand && andere is { } a)
            n = z.N0 * z.D0 / Math.Max(XrSitzungLogik.MinHandabstand, a.Punkt.Abstand(halt.Punkt));
        else if (e.Zoom == Bedienung.Varianten.ZoomDrehknopf && z.R0 is { } r0 && halt.Rollen is { } r1)
        {
            var rollen = Totzone(XrSitzungLogik.WinkelDiff(r1, r0), RollTotzoneGrad * Num.Grad);
            n = z.N0 * Math.Pow(2, -rollen / (Math.Max(5, e.DrehknopfGrad) * Num.Grad));
        }
        n = XrSitzungLogik.MassstabBegrenzen(n, zoom);
        // Drehung mit dem Gierwinkel der Hand, Lage: der Punkt des Modells unter der Hand bleibt unter ihr, Höhe folgt der Hand
        var dphi = z.G0 is { } g0 && halt.Gieren is { } g1 ? Totzone(XrSitzungLogik.WinkelDiff(g1, g0), DrehTotzoneGrad * Num.Grad) : 0;
        var theta = z.Lage0.Drehung + dphi;
        var rel = XrSitzungLogik.DreheXZ(new Xz(z.Q.X / n, z.Q.Z / n), theta);
        var mitte = new Xz(halt.Punkt.X + rel.X, halt.Punkt.Z + rel.Z);
        var hoehe = z.Lage0.Hoehe + (halt.Punkt.Y - z.H0.Y);
        z.Lage = XrSitzungLogik.LageBegrenzen(new SitzungLage(n, theta, mitte.X - XrSitzungLogik.TischMitte.X, mitte.Z - XrSitzungLogik.TischMitte.Z, hoehe), zoom);
        return ereignis;
    }

    /// <summary>Gehört diese Hand («links», «rechts») gerade zum Griff (hält ihn oder zoomt als zweite Hand)?</summary>
    public static bool HandAmGriff(EinGriffZustand z, string hand) => z.Hand == hand || (z.ZweiteHand && z.Hand is not null && z.Hand != hand);
}
