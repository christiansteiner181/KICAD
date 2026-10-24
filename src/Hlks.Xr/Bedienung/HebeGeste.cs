// Quest App V1a, Gesten der App-Symbole (Christian 24.10.2026):
//   - Hebegeste: «wird mit beiden handflächen nach oben eine deutliches nach obenstreichen gezeigt, soll dies frei schwebende Appsymbole von unten her einblenden. so wie
//     ich sie von unten hinein heben würde.» Beide Hände offen, Handflächen nach oben, beide deutlich und etwa gleichzeitig nach oben → «Heben». Während der Geste ein
//     Fortschritt 0 … 1, damit die Symbole der Höhe der Hände folgen. Ersetzt «linke Handfläche zum Gesicht» (P5a).
//   - Wegschieben: «aus der mitte heraus seitwärtswischen mit beiden armen und nach aussen gedrehten händen schiebt die symbole beidseitig aus dem bild.» Beide Hände
//     beginnen nahe beieinander vor dem Körper, die Handflächen nach aussen gedreht (links nach links, rechts nach rechts), beide wischen gleichzeitig deutlich nach
//     aussen → «Wegschieben». Während der Geste der Weg jeder Hand nach aussen, damit die Symbole mitlaufen (links der Mitte nach links, rechts nach rechts).
// Beide Gesten ohne Pinch: weder Pinch + Wisch (Etage, Blättern, Drehen) noch der Sprung lösen aus – sie brauchen den Pinch; umgekehrt lösen ein einhändiger Wisch, die
// Hebegeste bzw. das Wegschieben die jeweils andere nicht aus (Tests). Rein, ohne StereoKit. Alle Schwellen sind Annahmen V1a (Stellschrauben hebe.*, schieben.*).

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen der Hebegeste (Stellschrauben «hebe.*», Annahmen V1a).</summary>
public sealed record HebeEinstellungen
{
    /// <summary>«hebe.weg_m»: so weit muss jede Hand nach oben – «deutlich» (Annahme V1a 0.15 m, gleich wie der Mindestweg eines Wischs).</summary>
    public double WegM { get; init; } = 0.15;
    /// <summary>«hebe.zeit_s»: in höchstens so viel Zeit (Annahme V1a 0.9 s).</summary>
    public double ZeitS { get; init; } = 0.9;
    /// <summary>«hebe.handflaeche_grad»: die Handfläche zeigt höchstens so weit neben senkrecht nach oben, Annahme V1a 45°.</summary>
    public double HandflaecheGrad { get; init; } = 45;
    /// <summary>«hebe.gleichzeitig_s»: die beiden Hände beginnen höchstens so weit auseinander (Annahme V1a 0.35 s).</summary>
    public double GleichzeitigS { get; init; } = 0.35;

    public static readonly HebeEinstellungen Vorgabe = new();
}

/// <summary>Schwellen des Wegschiebens (Stellschrauben «schieben.*», Annahmen V1a).</summary>
public sealed record SchiebeEinstellungen
{
    /// <summary>«schieben.startabstand_m»: am Anfang liegen die Hände höchstens so weit auseinander (quer zum Blick) – «aus der Mitte heraus» (Annahme V1a 0.3 m).</summary>
    public double StartabstandM { get; init; } = 0.3;
    /// <summary>«schieben.weg_m»: so weit muss jede Hand nach aussen (Annahme V1a 0.15 m).</summary>
    public double WegM { get; init; } = 0.15;
    /// <summary>«schieben.zeit_s»: in höchstens so viel Zeit (Annahme V1a 0.8 s).</summary>
    public double ZeitS { get; init; } = 0.8;
    /// <summary>«schieben.handflaeche_grad»: die Handfläche zeigt höchstens so weit neben waagrecht nach aussen (Annahme V1a 50°).</summary>
    public double HandflaecheGrad { get; init; } = 50;
    /// <summary>«schieben.gleichzeitig_s»: die beiden Hände beginnen höchstens so weit auseinander (Annahme V1a 0.35 s).</summary>
    public double GleichzeitigS { get; init; } = 0.35;

    public static readonly SchiebeEinstellungen Vorgabe = new();
}

/// <summary>Lage der App-Symbole und Zeiten (Stellschrauben «symbole.*», «zeiger.tooltip_s», Variante «symbole»; Annahmen V1a).</summary>
public sealed record SymbolEinstellungen
{
    /// <summary>«symbole.abstand_m»: Mitte der Symbole so weit vor den Augen (waagrecht), in Griffweite (Annahme V1a 0.42 m).</summary>
    public double AbstandM { get; init; } = 0.42;
    /// <summary>«symbole.hoehe_m»: Mitte der Symbole so weit unter den Augen – Brust bis Schulter (Annahme V1a 0.32 m).</summary>
    public double HoeheM { get; init; } = 0.32;
    /// <summary>«symbole.streuung»: Faktor der Verteilung (1 = wie vorgesehen; grösser = weiter auseinander).</summary>
    public double Streuung { get; init; } = 1;
    /// <summary>«symbole.wartezeit_s»: ohne Wahl gehen die Symbole nach so vielen Sekunden (Annahme V1a 10 s).</summary>
    public double WartezeitS { get; init; } = 10;
    /// <summary>«zeiger.tooltip_s»: der Name erscheint nach so viel Verweilen am Ziel (Annahme V1a 0.6 s; HIG: «after … a moment», «slightly longer delay»).</summary>
    public double TooltipS { get; init; } = 0.6;
    /// <summary>Variante «symbole»: <see cref="Varianten.SymboleVerteilt"/> (Vorgabe) oder <see cref="Varianten.SymboleBogen"/>.</summary>
    public string Verteilung { get; init; } = Varianten.SymboleVerteilt;
}

/// <summary>Was eine Hand für die Gesten zeigt: Mitte und Normale der Handfläche (Normale aus der Handfläche heraus), offen, Pinch.</summary>
public readonly record struct HandBeobachtung(Vec3 Mitte, Vec3 Normale, bool Offen, bool Pinch)
{
    /// <summary>Aus den Gelenken (WebXR-Namen); seite «left» bzw. «right». null, wenn Gelenke fehlen.</summary>
    public static HandBeobachtung? Aus(IReadOnlyDictionary<string, Vec3>? g, string seite, PinchEinstellungen? pinch = null)
    {
        if (XrTabletLogik.HandflaechenMitte(g) is not { } m || XrTabletLogik.HandflaechenNormale(g, seite) is not { } n) return null;
        return new HandBeobachtung(m, n, XrTabletLogik.IstOffen(g), XrTabletLogik.IstPinch(g, false, pinch));
    }
}

/// <summary>Ereignis der Gesten.</summary>
public enum HebeArt { Heben, Wegschieben }

/// <summary>
/// Stand eines Bildes: Ereignis (oder null); Fortschritt des Hebens 0 … 1 (beide Handflächen oben) und mittlere Höhe der Handflächen; gezögert (Hebegeste);
/// Weg der linken bzw. rechten Hand nach aussen (m), solange beide in der Haltung des Wegschiebens sind (sonst 0) – die Symbole laufen damit mit.
/// </summary>
public sealed record HebeStand(HebeArt? Ereignis, double Fortschritt, double Hoehe, bool Zoegert, double SchiebenLinks = 0, double SchiebenRechts = 0)
{
    /// <summary>Beide Hände in der Haltung des Wegschiebens (die App wertet dann keinen einhändigen Wisch über eine Messlinie aus).</summary>
    public bool SchiebenLaeuft { get; init; }
}

/// <summary>Zustand der Gesten (je Hand die Punkte, solange die Hand passt).</summary>
public sealed class HebeZustand
{
    internal readonly List<(double T, Vec3 P)>[] Hoch = [[], []], Aussen = [[], []];
    internal double SperreBis = double.NegativeInfinity;
    internal bool ZoegernGemeldet;
}

public static class HebeGeste
{
    /// <summary>Quer zur Richtung der Geste höchstens so viel im Verhältnis zum Weg (sonst ist es ein Greifen oder Zeigen), Annahme V1a.</summary>
    public const double SeitlichAnteil = 0.6;
    /// <summary>Nach einem Ereignis zählt keine Geste so lange (s).</summary>
    public const double SperreS = 0.8;
    /// <summary>Beide Handflächen oben, aber ruhig (Umkreis RuheM) so lange: gezögert – Geisterhände zeigen das Anheben (Annahme V1a wie P5a).</summary>
    public const double ZoegernS = 1.5, RuheM = 0.04;

    /// <summary>Waagrecht nach rechts zum Blick (Gieren wie VrLogik: 90° = Blick nach −z).</summary>
    public static Vec3 Rechts(double blickGieren) => new(Math.Sin(blickGieren), 0, Math.Cos(blickGieren));

    /// <summary>
    /// Ein Schritt je Bild: links/rechts = Beobachtung der Hand (null = nicht erkannt), blickGieren für «aussen». Eine Hand zählt nur offen und ohne Pinch.
    /// Heben: Handfläche höchstens <see cref="HebeEinstellungen.HandflaecheGrad"/> neben oben; beide Hände in höchstens ZeitS um WegM gestiegen (quer höchstens
    /// <see cref="SeitlichAnteil"/> davon), ihre tiefsten Punkte höchstens GleichzeitigS auseinander. Wegschieben: Handfläche höchstens
    /// <see cref="SchiebeEinstellungen.HandflaecheGrad"/> neben aussen (links nach links, rechts nach rechts); beide in höchstens ZeitS um WegM nach aussen (quer
    /// höchstens SeitlichAnteil davon), gleichzeitig begonnen, und am Anfang höchstens StartabstandM auseinander.
    /// </summary>
    public static HebeStand Schritt(HebeZustand z, double t, HandBeobachtung? links, HandBeobachtung? rechts, double blickGieren, HebeEinstellungen? e = null,
        SchiebeEinstellungen? s = null)
    {
        e ??= HebeEinstellungen.Vorgabe;
        s ??= SchiebeEinstellungen.Vorgabe;
        var cosHeben = Math.Cos(e.HandflaecheGrad * Math.PI / 180);
        var cosSchieben = Math.Cos(s.HandflaecheGrad * Math.PI / 180);
        var behalten = Math.Max(Math.Max(e.ZeitS, s.ZeitS), ZoegernS) + 0.1;
        var oben = new Vec3(0, 1, 0);
        var r = Rechts(blickGieren);
        Vec3[] aussen = [r * -1, r];
        HandBeobachtung?[] haende = [links, rechts];
        for (int i = 0; i < 2; i++)
        {
            var h = haende[i];
            var frei = h is { Offen: true, Pinch: false };
            Fuehren(z.Hoch[i], frei && h!.Value.Normale.Skalar(oben) >= cosHeben, h, t, behalten);
            Fuehren(z.Aussen[i], frei && h!.Value.Normale.Skalar(aussen[i]) >= cosSchieben, h, t, behalten);
        }
        // Heben: Fortschritt (der kleinere Anstieg der beiden im Verhältnis zum Weg) und Höhe
        double fortschritt = 0, hoehe = 0;
        var beideOben = z.Hoch[0].Count > 0 && z.Hoch[1].Count > 0;
        if (beideOben)
        {
            fortschritt = Math.Clamp(Math.Min(Weg(z.Hoch[0], t, e.ZeitS, oben).Weg, Weg(z.Hoch[1], t, e.ZeitS, oben).Weg) / e.WegM, 0, 1);
            hoehe = (z.Hoch[0][^1].P.Y + z.Hoch[1][^1].P.Y) / 2;
        }
        // Wegschieben: der Weg jeder Hand nach aussen, solange beide in der Haltung sind
        double schiebenL = 0, schiebenR = 0;
        var beideAussen = z.Aussen[0].Count > 0 && z.Aussen[1].Count > 0;
        if (beideAussen)
        {
            schiebenL = Weg(z.Aussen[0], t, s.ZeitS, aussen[0]).Weg;
            schiebenR = Weg(z.Aussen[1], t, s.ZeitS, aussen[1]).Weg;
        }
        // gezögert: beide Handflächen oben, lange genug ruhig
        var zoegert = false;
        if (beideOben && Ruhig(z.Hoch[0], t) && Ruhig(z.Hoch[1], t)) { zoegert = !z.ZoegernGemeldet; z.ZoegernGemeldet = true; }
        else if (!beideOben) z.ZoegernGemeldet = false;
        HebeStand Stand(HebeArt? art, double f, bool zg) => new(art, f, hoehe, zg, schiebenL, schiebenR) { SchiebenLaeuft = beideAussen };
        if (t < z.SperreBis)
        {
            // in der Sperre beginnt nichts: eine Bewegung, die hier anfängt, zählt nach der Sperre nicht (sonst löste sie verspätet ein zweites Mal aus)
            foreach (var l in z.Hoch.Concat(z.Aussen)) l.Clear();
            schiebenL = schiebenR = 0;
            return Stand(null, 0, false);
        }
        if (beideOben)
        {
            var a = Weg(z.Hoch[0], t, e.ZeitS, oben);
            var b = Weg(z.Hoch[1], t, e.ZeitS, oben);
            if (a.Weg >= e.WegM && b.Weg >= e.WegM && a.Quer <= SeitlichAnteil * a.Weg && b.Quer <= SeitlichAnteil * b.Weg && Math.Abs(a.Beginn - b.Beginn) <= e.GleichzeitigS + 1e-9)
                return Ausgeloest(z, t, Stand(HebeArt.Heben, 1, false));
        }
        if (beideAussen)
        {
            var a = Weg(z.Aussen[0], t, s.ZeitS, aussen[0]);
            var b = Weg(z.Aussen[1], t, s.ZeitS, aussen[1]);
            var start = Math.Abs((b.Start - a.Start).Skalar(r));
            if (a.Weg >= s.WegM && b.Weg >= s.WegM && a.Quer <= SeitlichAnteil * a.Weg && b.Quer <= SeitlichAnteil * b.Weg
                && Math.Abs(a.Beginn - b.Beginn) <= s.GleichzeitigS + 1e-9 && start <= s.StartabstandM + 1e-9)
                return Ausgeloest(z, t, Stand(HebeArt.Wegschieben, 0, false));
        }
        return Stand(null, fortschritt, zoegert);
    }

    static HebeStand Ausgeloest(HebeZustand z, double t, HebeStand stand)
    {
        z.SperreBis = t + SperreS;
        foreach (var l in z.Hoch.Concat(z.Aussen)) l.Clear();
        z.ZoegernGemeldet = true;
        return stand;
    }

    static void Fuehren(List<(double T, Vec3 P)> l, bool passt, HandBeobachtung? h, double t, double behalten)
    {
        if (!passt) { l.Clear(); return; }
        l.Add((t, h!.Value.Mitte));
        while (l.Count > 0 && l[0].T < t - behalten) l.RemoveAt(0);
    }

    /// <summary>
    /// Weg in Richtung r (Einheit) vom kleinsten Stand im Zeitfenster bis jetzt, der Weg quer dazu, die Zeit des Beginns (der letzte Punkt am kleinsten Stand, 1 cm Spiel –
    /// nicht der erste, sonst gälte eine ruhende Hand als «gleichzeitig») und der Punkt dort.
    /// </summary>
    static (double Weg, double Quer, double Beginn, Vec3 Start) Weg(List<(double T, Vec3 P)> l, double t, double fenster, Vec3 r)
    {
        var jetzt = l[^1].P;
        var kleinst = l.Where(x => x.T >= t - fenster - 1e-9).Min(x => x.P.Skalar(r));
        var beginn = l.Last(x => x.T >= t - fenster - 1e-9 && x.P.Skalar(r) <= kleinst + 0.01);
        var d = jetzt - beginn.P;
        var weg = jetzt.Skalar(r) - kleinst;
        var quer = (d - r * d.Skalar(r)).Laenge;
        return (weg, quer, beginn.T, beginn.P);
    }

    static bool Ruhig(List<(double T, Vec3 P)> l, double t)
    {
        if (l.Count == 0 || l[0].T > t - ZoegernS + 1e-9) return false;
        var jetzt = l[^1].P;
        return l.Where(x => x.T >= t - ZoegernS).All(x => x.P.Abstand(jetzt) <= RuheM);
    }
}
