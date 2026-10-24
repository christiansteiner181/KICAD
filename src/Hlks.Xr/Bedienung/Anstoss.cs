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

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen des Anstosses (Stellschrauben «gesten.anstoss_*»; die übrigen feste Annahmen V1e).</summary>
public sealed record AnstossEinstellungen
{
    /// <summary>«gesten.anstoss_weg_m»: so weit (m) muss die Hand in Richtung der Geste (Annahme V1e 0.06 m; Christian: «nur einen anstoss»).</summary>
    public double WegM { get; init; } = 0.06;
    /// <summary>«gesten.anstoss_tempo_ms»: mittleres Tempo (m/s) über den Anstoss (Annahme V1e 0.5 m/s: 6 cm in höchstens 0.12 s bzw. 10 cm in 0.2 s – ein gemächliches
    /// Ausholen bleibt darunter).</summary>
    public double TempoMs { get; init; } = 0.5;
    /// <summary>Die Haltung der Geste steht schon so lange (s) vor dem Anlauf (Annahme V1e 0.1 s).</summary>
    public double HaltungVorS { get; init; } = 0.1;
    /// <summary>Quer zur Richtung höchstens so viel im Verhältnis zum Weg (Annahme V1e 0.4; die ganze Bewegung erlaubt 0.6 bzw. 35°).</summary>
    public double QuerAnteil { get; init; } = 0.4;
    /// <summary>Die Handfläche dreht sich während des Anstosses höchstens so viel (Grad; Annahme V1e 25°).</summary>
    public double DrehMaxGrad { get; init; } = 25;
    /// <summary>Die Hand ist beim Anstoss mindestens so weit offen (XrTabletLogik.FaustVerhaeltnis; «offen» der Gesten ab 1.65, eine flache Hand rund 1.9; Annahme V1e 1.75).</summary>
    public double OffenAb { get; init; } = 1.75;

    public static readonly AnstossEinstellungen Vorgabe = new();
    /// <summary>Kein Anstoss (nur die ganze Bewegung wie bis V1d; für Vergleiche).</summary>
    public static readonly AnstossEinstellungen Aus = new() { WegM = double.PositiveInfinity };
}

/// <summary>Stand des Anstosses: ausgelöst, Weg (m) und Tempo (m/s) seit dem Anlauf im Zeitfenster, Zeit des Anlaufs, Weg quer zur Richtung.</summary>
public readonly record struct AnstossStand(bool Ausgeloest, double Weg, double Tempo, double Beginn, double Quer)
{
    public static readonly AnstossStand Nichts = new(false, 0, 0, double.NaN, 0);
}

public static class Anstoss
{
    /// <summary>Der Anstoss muss innert so viel Zeit (s) liegen (Auftrag V1e: «innert rund 0,2 s»).</summary>
    public const double ZeitS = 0.2;
    /// <summary>Spiel beim Anlauf (m): der letzte Punkt höchstens so nah am am weitesten entgegen liegenden (Zittern der ruhenden Hand).</summary>
    public const double SpielM = 0.003;

    /// <summary>
    /// Prüft den Anstoss auf einer Spur (Zeit, Mitte und Normale der Handfläche; nur Punkte in der Haltung, älteste zuerst) zur Zeit t in Richtung r (Einheit). querAnteil:
    /// Grenze der Geste quer zum Weg; offenheit: FaustVerhaeltnis der Hand jetzt.
    /// </summary>
    public static AnstossStand Pruefen(IReadOnlyList<(double T, Vec3 P, Vec3 N)> spur, double t, Vec3 r, double querAnteil, AnstossEinstellungen? e = null,
        double offenheit = double.PositiveInfinity)
    {
        e ??= AnstossEinstellungen.Vorgabe;
        if (spur.Count < 2 || double.IsInfinity(e.WegM) || offenheit < e.OffenAb) return AnstossStand.Nichts;
        var ab = spur.Count - 1;
        while (ab > 0 && spur[ab - 1].T >= t - ZeitS - 1e-9) ab--;
        if (ab >= spur.Count - 1) return AnstossStand.Nichts;
        var jetzt = spur[^1].P;
        var kleinst = double.PositiveInfinity;
        for (int i = ab; i < spur.Count; i++) kleinst = Math.Min(kleinst, spur[i].P.Skalar(r));
        var anlauf = spur.Count - 1;
        for (int i = spur.Count - 1; i >= ab; i--) if (spur[i].P.Skalar(r) <= kleinst + SpielM) { anlauf = i; break; }
        var weg = jetzt.Skalar(r) - kleinst;
        var dauer = t - spur[anlauf].T;
        if (weg <= 0 || dauer <= 1e-6) return new AnstossStand(false, Math.Max(0, weg), 0, spur[anlauf].T, 0);
        var d = jetzt - spur[anlauf].P;
        var quer = (d - r * d.Skalar(r)).Laenge;
        var tempo = weg / dauer;
        // erst die Hand stellen, dann der Schub
        var gehalten = spur[0].T <= spur[anlauf].T - e.HaltungVorS + 1e-9;
        // die Handfläche bleibt beim Schub in ihrer Lage: im ganzen Zeitfenster höchstens DrehMaxGrad gegen die Lage jetzt (der Anlauf allein ist beim Zittern unsicher)
        var cosDreh = Math.Cos(Math.Min(180, e.DrehMaxGrad) * Math.PI / 180);
        var ruhig = true;
        var nj = spur[^1].N;
        for (int i = ab; i < spur.Count && ruhig; i++) ruhig = spur[i].N.Skalar(nj) >= cosDreh - 1e-9;
        var aus = weg >= e.WegM && tempo >= e.TempoMs && quer <= Math.Min(querAnteil, e.QuerAnteil) * weg && gehalten && ruhig;
        return new AnstossStand(aus, weg, tempo, spur[anlauf].T, quer);
    }
}
