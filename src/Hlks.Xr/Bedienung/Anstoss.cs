// Quest App V1e (Brillentest, Christian 24.10.2026): «alle ein und aus wisch gesten sollen nur einen anstoss brauchen. momentan brechen einblenden von apps und monitor,
// sowie der apps oft ab. weil die instinktive bewegung zu kurz ist.» Rein, ohne StereoKit.
// Ein ANSTOSS ist ein kurzer, deutlicher Schub in die Richtung der Geste, in der Haltung der Geste (die prüft der Aufrufer; die Spur enthält nur Punkte in der Haltung):
//   - Weg mindestens «gesten.anstoss_weg_m» innert ZeitS, mittleres Tempo über diesen Weg mindestens «gesten.anstoss_tempo_ms» (vom Anlauf – dem am weitesten
//     entgegen liegenden Punkt im Zeitfenster, der letzte dort mit SpielM – bis jetzt), quer höchstens QuerAnteil des Wegs (und höchstens, was die Geste erlaubt);
//   - die Haltung stand schon HaltungVorS vor dem Anlauf (erst die Hand stellen, dann der Schub), die Handfläche dreht sich dabei höchstens DrehMaxGrad (ein Schub,
//     kein Schwenk), die Hand ist deutlich offen (FaustVerhaeltnis ab OffenAb).
// Er gilt für die Hebegeste (beide Hände), das Hineinziehen des PC-Bildschirms und das Wegwischen; die ganze Bewegung wie bis V1d löst weiter aus. Ist er erkannt,
// gilt die Geste als ausgelöst – die Bewegung (Symbole steigen auf, Bildschirm gleitet herein, Fenster gleitet hinaus) läuft in der App mit Schwung zu Ende; vorher
// folgt sie der Hand. Gegen Fehlauslösungen bleiben die Sperren, das Ausholen (ein gemächliches Ausholen erreicht das Tempo nicht), die gegenläufige bzw. ruhige zweite
// Hand und die Abgrenzung über den Pinch. Die festen Werte (Haltung vorher, Drehung, Offenheit, quer) sind so gewählt, dass die Fehlauslösungen über Zufallsbewegungen,
// ruhige Hände und die Testgesten des Labors nicht zunehmen (tests/Hlks.Xr.Tests/V1eTests.cs). Alle Werte sind Annahmen V1e.
// V1f (Christian 24.10.2026: «wisch geste soll auch gehen wenn die finger nicht voll gestrekt sind.»): die LOCKERE HAND zählt – für die Haltung der Wischgesten (Wegwischen,
// Hineinziehen, Hebegeste) und für den Anstoss gilt «gesten.offen_ab» (FaustVerhaeltnis, Annahme V1f 1.40; bis V1e Haltung 1.65, Anstoss 1.75). Nicht zählen: die Faust (unter
// XrTabletLogik.FaustAn), die Pistolengeste (Pistole.Haltung) und der Pinch (LockereHand). Die Fehlauslösungen vorher/nachher misst tests/Hlks.Xr.Tests/V1fTests.cs. Gibt der
// Anstoss nicht aus, nennt <see cref="AnstossStand.Grund"/> den Grund (für das Protokoll der Hebegeste).
// V1h (Brillentest Christian 25.10.2026: «Ja, das Wegwischen geht jetzt gut, übernimm die Werte als Vorgabe.»): Weg 0.045 m, Tempo 0.35 m/s, für alle drei Gesten. Dazu
// Christians Regeln für Striche nach aussen (am Rand des Blicks bzw. auf der eigenen Körperseite der Hand: AussenGesperrt) und – gegen die Fehlauslösungen, die mit den
// lockereren Werten zunehmen – «unter dem Blick» (UntenGesperrt: die am Körper ruhende Hand wischt nicht). Vorher/nachher: tests/Hlks.Xr.Tests/V1hTests.cs.

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen des Anstosses (Stellschrauben «gesten.anstoss_*», «gesten.offen_ab»; die übrigen feste Annahmen V1e).</summary>
public sealed record AnstossEinstellungen
{
    /// <summary>«gesten.anstoss_weg_m»: so weit (m) muss die Hand in Richtung der Geste (Annahme V1e 0.06 m; Christian: «nur einen anstoss»; V1h 0.045 m, Brillentest
    /// 25.10.2026: «Ja, das Wegwischen geht jetzt gut, übernimm die Werte als Vorgabe.»).</summary>
    public double WegM { get; init; } = 0.045;
    /// <summary>«gesten.anstoss_tempo_ms»: mittleres Tempo (m/s) über den Anstoss (Annahme V1e 0.5 m/s: 6 cm in höchstens 0.12 s bzw. 10 cm in 0.2 s – ein gemächliches
    /// Ausholen bleibt darunter; V1h 0.35 m/s, Brillentest 25.10.2026).</summary>
    public double TempoMs { get; init; } = 0.35;
    /// <summary>Die Haltung der Geste steht schon so lange (s) vor dem Anlauf (Annahme V1e 0.1 s).</summary>
    public double HaltungVorS { get; init; } = 0.1;
    /// <summary>Quer zur Richtung höchstens so viel im Verhältnis zum Weg (Annahme V1e 0.4; die ganze Bewegung erlaubt 0.6 bzw. 35°).</summary>
    public double QuerAnteil { get; init; } = 0.4;
    /// <summary>Die Handfläche dreht sich während des Anstosses höchstens so viel (Grad; Annahme V1e 25°).</summary>
    public double DrehMaxGrad { get; init; } = 25;
    /// <summary>«gesten.offen_ab»: die Hand ist beim Anstoss mindestens so weit offen (XrTabletLogik.FaustVerhaeltnis; flache Hand rund 1.9, Faust rund 1.0). V1e 1.75; V1f
    /// (lockere Hand, Christian 24.10.2026) Annahme 1.40 – halb gekrümmte Finger (rund 1.45) zählen.</summary>
    public double OffenAb { get; init; } = 1.40;
    /// <summary>V1f, «gesten.offen_ab»: ab so viel gilt die Hand in der Haltung der Wischgesten als offen (V1e XrTabletLogik.OffenAb 1.65; V1f wie <see cref="OffenAb"/>).</summary>
    public double HaltungOffenAb { get; init; } = 1.40;
    /// <summary>V1f: die Pistolengeste zählt nicht als Haltung der Wischgesten (bis V1e gab es sie nicht).</summary>
    public bool OhnePistole { get; init; } = true;
    /// <summary>V1f: so viel darf sich die Form der Hand (FaustVerhaeltnis, höchster minus tiefster Wert) während der Geste ändern – eine Wischgeste macht man mit gleich
    /// bleibender Hand; eine Bewegung, bei der sich die Finger öffnen oder schliessen, ist keine (Annahme V1f 0.15; bis V1e ohne Grenze). Gegen Fehlauslösungen mit der
    /// lockeren Hand (tests/Hlks.Xr.Tests/V1fTests.cs).</summary>
    public double FormSpiel { get; init; } = 0.15;
    /// <summary>V1f: eine lockere Hand (weniger offen als XrTabletLogik.OffenAb) hat die vier Finger etwa gleich gekrümmt – die Streckung (Pistole.Streckung) liegt höchstens so
    /// weit auseinander (Annahme V1f 0.25; ein gestreckter Finger bei eingerollten übrigen ist Zeigen, keine Wischgeste).</summary>
    public double SpreizungMax { get; init; } = 0.25;
    /// <summary>V1h, «gesten.randwinkel_grad»: die linke Hand wischt am linken bzw. die rechte am rechten Rand nicht nach aussen – beginnt der Strich mehr als so viel (Grad)
    /// neben der Blickrichtung auf der Seite der Hand, zählt er nicht (Annahme V1h 25°; Christian 25.10.2026). Gilt für Wegwischen (linke Hand) und Hineinziehen (rechte
    /// Hand); 90 = ohne.</summary>
    public double RandGrad { get; init; } = 25;
    /// <summary>V1h, «gesten.mitte_toleranz_m»: ein Strich nach aussen (linke Hand nach links, rechte nach rechts) zählt nur, wenn er an bzw. jenseits der Körpermitte beginnt –
    /// höchstens so viel (m) auf der eigenen Seite der Hand (Christian 25.10.2026: «oder im normbereich der jeweiligen hand»; Annahme V1h 0.05 m); unendlich = ohne.</summary>
    public double MitteToleranzM { get; init; } = 0.05;
    /// <summary>V1h, «gesten.unten_grad»: ein Wisch quer zum Blick (Wegwischen, Hineinziehen) zählt nicht, wenn er mehr als so viel (Grad) unter der Blickrichtung beginnt –
    /// die Hand ruht dann am Körper bzw. liegt unter dem Bild (Annahme V1h 45°; die ruhende Hand der Tests liegt rund 51° unter dem waagrechten Blick, die Wische rund 30°).
    /// Gegen die Fehlauslösungen mit den lockereren Werten des Brillentests 25.10.2026; 90 = ohne. Nicht für die Hebegeste (sie beginnt unten).</summary>
    public double UntenGrad { get; init; } = 45;

    public static readonly AnstossEinstellungen Vorgabe = new();
    /// <summary>Kein Anstoss (nur die ganze Bewegung wie bis V1d; für Vergleiche).</summary>
    public static readonly AnstossEinstellungen Aus = new() { WegM = double.PositiveInfinity };
    /// <summary>V1f: der Satz von V1e (deutlich offene Hand: Haltung ab 1.65, Anstoss ab 1.75; für den Vergleich der Fehlauslösungen).</summary>
    public static readonly AnstossEinstellungen V1e = new() { WegM = 0.06, TempoMs = 0.5, OffenAb = 1.75, HaltungOffenAb = XrTabletLogik.OffenAb, OhnePistole = false, FormSpiel = double.PositiveInfinity, SpreizungMax = double.PositiveInfinity,
        RandGrad = 90, MitteToleranzM = double.PositiveInfinity, UntenGrad = 90 };
}

/// <summary>
/// Stand des Anstosses: ausgelöst, Weg (m) und Tempo (m/s) seit dem Anlauf im Zeitfenster, Zeit des Anlaufs, Weg quer zur Richtung; V1f: der Grund, wenn er nicht ausgelöst
/// hat (kurz, für das Protokoll), sonst null.
/// </summary>
public readonly record struct AnstossStand(bool Ausgeloest, double Weg, double Tempo, double Beginn, double Quer, string? Grund = null)
{
    public static readonly AnstossStand Nichts = new(false, 0, 0, double.NaN, 0);
}

/// <summary>
/// V1f: die Haltung der Wischgesten mit lockerer Hand (Christian 24.10.2026: «wisch geste soll auch gehen wenn die finger nicht voll gestrekt sind»): keine Faust, kein Pinch,
/// keine Pistolengeste, FaustVerhaeltnis ab <see cref="AnstossEinstellungen.HaltungOffenAb"/>.
/// </summary>
public static class LockereHand
{
    /// <summary>Passt die beobachtete Hand?</summary>
    public static bool Passt(HandBeobachtung h, AnstossEinstellungen e) => Grund(h, e) is null;

    /// <summary>Passen die Gelenke (WebXR-Namen)? pinch = Pinch dieser Hand (mit Hysterese, vom Aufrufer).</summary>
    public static bool Passt(IReadOnlyDictionary<string, Vec3>? g, bool pinch, AnstossEinstellungen e) =>
        !pinch && XrTabletLogik.FaustVerhaeltnis(g) is { } v && v >= XrTabletLogik.FaustAn && v >= e.HaltungOffenAb && !(e.OhnePistole && Pistole.Haltung(g))
        && (v >= XrTabletLogik.OffenAb || Pistole.Spreizung(g) <= e.SpreizungMax + 1e-9);

    /// <summary>Warum die Hand nicht passt (kurz), null wenn sie passt.</summary>
    public static string? Grund(HandBeobachtung h, AnstossEinstellungen e) =>
        !h.Offen ? "Faust"
        : e.OhnePistole && h.Pistole ? "Pistolengeste"
        : h.Pinch ? "Pinch"
        : h.Offenheit < e.HaltungOffenAb ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"zu geschlossen ({h.Offenheit:0.00}, ab {e.HaltungOffenAb:0.00})")
        : h.Offenheit < XrTabletLogik.OffenAb && h.Spreizung > e.SpreizungMax + 1e-9
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Finger ungleich gekrümmt ({h.Spreizung:0.00}, höchstens {e.SpreizungMax:0.00})")
        : null;
}

public static class Anstoss
{
    /// <summary>Der Anstoss muss innert so viel Zeit (s) liegen (Auftrag V1e: «innert rund 0,2 s»).</summary>
    public const double ZeitS = 0.2;
    /// <summary>Spiel beim Anlauf (m): der letzte Punkt höchstens so nah am am weitesten entgegen liegenden (Zittern der ruhenden Hand).</summary>
    public const double SpielM = 0.003;

    /// <summary>
    /// V1h: Winkel (Grad) eines Punkts gegen die Blickrichtung, waagrecht: positiv rechts, negativ links (Gieren wie VrLogik). Ohne Kopf 0.
    /// </summary>
    public static double SeitenWinkel(Vec3 p, Vec3? kopf, double blickGieren)
    {
        if (kopf is not { } k) return 0;
        var v = p - k;
        var rechts = HebeGeste.Rechts(blickGieren);
        var vorn = new Vec3(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));
        return Math.Atan2(v.Skalar(rechts), v.Skalar(vorn)) * 180 / Math.PI;
    }

    /// <summary>
    /// V1h: zählt ein Strich nach aussen (linke Hand nach links, rechte nach rechts) nicht? Er beginnt am Rand des Blicks (mehr als RandGrad neben dem Blick auf der Seite der
    /// Hand) oder im Bereich der Hand auf ihrer eigenen Körperseite (mehr als MitteToleranzM neben der Körpermitte: senkrechte Ebene durch den Kopf in der Körperrichtung
    /// koerperGieren, ohne Angabe die Blickrichtung). Ohne Kopf nie.
    /// </summary>
    public static bool AussenGesperrt(Vec3 beginn, bool linkeHand, Vec3? kopf, double blickGieren, AnstossEinstellungen e, double? koerperGieren = null)
    {
        if (kopf is not { } k) return false;
        if (e.RandGrad < 90)
        {
            var w = SeitenWinkel(beginn, kopf, blickGieren);
            if (linkeHand ? w < -e.RandGrad : w > e.RandGrad) return true;
        }
        if (double.IsFinite(e.MitteToleranzM))
        {
            var s = (beginn - k).Skalar(HebeGeste.Rechts(koerperGieren ?? blickGieren));
            if (linkeHand ? s < -e.MitteToleranzM : s > e.MitteToleranzM) return true;
        }
        return false;
    }

    /// <summary>
    /// V1h: Winkel (Grad), um den ein Punkt unter der Blickrichtung liegt (blickNicken = Neigung des Blicks, Bogenmass, positiv nach oben; der Punkt gegen die Waagrechte
    /// vom Kopf). Ohne Kopf 0.
    /// </summary>
    public static double UnterBlick(Vec3 p, Vec3? kopf, double blickNicken)
    {
        if (kopf is not { } k) return 0;
        var v = p - k;
        return (blickNicken - Math.Atan2(v.Y, Math.Sqrt(v.X * v.X + v.Z * v.Z))) * 180 / Math.PI;
    }

    /// <summary>V1h: beginnt ein Wisch quer zum Blick mehr als «gesten.unten_grad» unter der Blickrichtung (die Hand ruht am Körper)? Ohne Kopf nie.</summary>
    public static bool UntenGesperrt(Vec3 beginn, Vec3? kopf, double blickNicken, AnstossEinstellungen e) =>
        kopf is not null && e.UntenGrad < 90 && UnterBlick(beginn, kopf, blickNicken) > e.UntenGrad;

    /// <summary>
    /// Prüft den Anstoss auf einer Spur (Zeit, Mitte und Normale der Handfläche, V1f: FaustVerhaeltnis; nur Punkte in der Haltung, älteste zuerst) zur Zeit t in Richtung r
    /// (Einheit). querAnteil: Grenze der Geste quer zum Weg; offenheit: FaustVerhaeltnis der Hand jetzt.
    /// </summary>
    public static AnstossStand Pruefen(IReadOnlyList<(double T, Vec3 P, Vec3 N, double O)> spur, double t, Vec3 r, double querAnteil, AnstossEinstellungen? e = null,
        double offenheit = double.PositiveInfinity)
    {
        e ??= AnstossEinstellungen.Vorgabe;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (double.IsInfinity(e.WegM)) return AnstossStand.Nichts;
        if (offenheit < e.OffenAb) return AnstossStand.Nichts with { Grund = string.Create(ci, $"zu geschlossen ({offenheit:0.00}, ab {e.OffenAb:0.00})") };
        if (spur.Count < 2) return AnstossStand.Nichts with { Grund = "erst in der Haltung" };
        var ab = spur.Count - 1;
        while (ab > 0 && spur[ab - 1].T >= t - ZeitS - 1e-9) ab--;
        if (ab >= spur.Count - 1) return AnstossStand.Nichts with { Grund = "erst in der Haltung" };
        var jetzt = spur[^1].P;
        var kleinst = double.PositiveInfinity;
        for (int i = ab; i < spur.Count; i++) kleinst = Math.Min(kleinst, spur[i].P.Skalar(r));
        var anlauf = spur.Count - 1;
        for (int i = spur.Count - 1; i >= ab; i--) if (spur[i].P.Skalar(r) <= kleinst + SpielM) { anlauf = i; break; }
        var weg = jetzt.Skalar(r) - kleinst;
        var dauer = t - spur[anlauf].T;
        if (weg <= 0 || dauer <= 1e-6) return new AnstossStand(false, Math.Max(0, weg), 0, spur[anlauf].T, 0, "kein Weg in Richtung der Geste");
        var d = jetzt - spur[anlauf].P;
        var quer = (d - r * d.Skalar(r)).Laenge;
        var tempo = weg / dauer;
        // erst die Hand stellen, dann der Schub
        var vorher = spur[anlauf].T - spur[0].T;
        var gehalten = vorher >= e.HaltungVorS - 1e-9;
        // die Handfläche bleibt beim Schub in ihrer Lage: im ganzen Zeitfenster höchstens DrehMaxGrad gegen die Lage jetzt (der Anlauf allein ist beim Zittern unsicher)
        var cosDreh = Math.Cos(Math.Min(180, e.DrehMaxGrad) * Math.PI / 180);
        var nj = spur[^1].N;
        var minCos = 1.0;
        for (int i = ab; i < spur.Count; i++) minCos = Math.Min(minCos, spur[i].N.Skalar(nj));
        var ruhig = minCos >= cosDreh - 1e-9;
        var querOk = quer <= Math.Min(querAnteil, e.QuerAnteil) * weg;
        // V1f: die Form der Hand bleibt (vom Halten vor dem Anlauf bis jetzt)
        var form = FormAenderung(spur, Math.Min(t - ZeitS, spur[anlauf].T - e.HaltungVorS));
        var formOk = form <= e.FormSpiel + 1e-9;
        var aus = weg >= e.WegM && tempo >= e.TempoMs && querOk && gehalten && ruhig && formOk;
        var grund = aus ? null
            : weg < e.WegM ? string.Create(ci, $"Weg {weg * 100:0.0} cm (mind. {e.WegM * 100:0.0})")
            : tempo < e.TempoMs ? string.Create(ci, $"Tempo {tempo:0.00} m/s (mind. {e.TempoMs:0.00})")
            : !querOk ? string.Create(ci, $"quer {quer * 100:0.0} cm")
            : !gehalten ? string.Create(ci, $"Haltung erst {vorher:0.00} s vor dem Schub (mind. {e.HaltungVorS:0.00})")
            : !formOk ? string.Create(ci, $"Hand öffnete bzw. schloss sich ({form:0.00}, höchstens {e.FormSpiel:0.00})")
            : string.Create(ci, $"Handfläche drehte {Math.Acos(Math.Clamp(minCos, -1, 1)) * 180 / Math.PI:0}° (höchstens {e.DrehMaxGrad:0})");
        return new AnstossStand(aus, weg, tempo, spur[anlauf].T, quer, grund);
    }

    /// <summary>V1f: wie stark sich die Form der Hand (FaustVerhaeltnis) auf der Spur ab der Zeit ab geändert hat (höchster minus tiefster Wert).</summary>
    public static double FormAenderung(IReadOnlyList<(double T, Vec3 P, Vec3 N, double O)> spur, double ab)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var x in spur)
            if (x.T >= ab - 1e-9) { min = Math.Min(min, x.O); max = Math.Max(max, x.O); }
        return max >= min ? max - min : 0;
    }
}

/// <summary>
/// V1h: die Richtung des Körpers (Gierwinkel, wie VrLogik) für die Körpermitte der Wischgesten: der Blick, träge nachgeführt – der Kopf dreht sich schneller und öfter als
/// der Körper (Zeitkonstante KoerperS, Annahme V1h 1 s).
/// </summary>
public sealed class KoerperRichtung
{
    public const double KoerperS = 1.0;
    double? gieren;

    /// <summary>Ein Bild: dt (s), Gierwinkel des Blicks; gibt die Körperrichtung zurück.</summary>
    public double Schritt(double dt, double blickGieren)
    {
        if (gieren is not { } g || !double.IsFinite(g)) return (gieren = blickGieren).Value;
        var k = 1 - Math.Exp(-Math.Max(0, dt) / KoerperS);
        gieren = g + XrSitzungLogik.WinkelDiff(blickGieren, g) * k;
        return gieren.Value;
    }

    public double? Gieren => gieren;
}
