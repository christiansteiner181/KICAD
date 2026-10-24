// Quest App V1a, Vorgabe Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste. auch da nutzen wir die wischbewegung von rechts nach links.»
// EINE Geste «Wegwischen» für die ganze App: rechte Hand offen, Handfläche nach links, Wisch von rechts nach links (quer zum Blick). Sie schliesst die offene Glas-App
// und die Karte einer Prüfaufgabe (= Abbruch; Christian 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem Wegwischen geschlossen; Pinch + Wisch nach vorn
// entfällt»), schiebt die App-Symbole nach links aus dem Bild (ersetzt das beidhändige Wegschieben aus der Mitte) und – V1c – den PC-Bildschirm. Rein, ohne
// StereoKit; je Bild ein Schritt.
//   - Erkennung (Wegwischen.Schritt): die rechte Hand offen, ohne Pinch, Handfläche höchstens «wegwischen.handflaeche_grad» neben «nach links» (waagrecht quer
//     zum Blick); seit V1d auch die linke Hand (Christian 24.10.2026: «wisch mit der linken hand von rechts nach links soll in wieder raus schieben»), deren Handfläche
//     seitlich in einer der beiden Richtungen zeigen darf – die Richtung des Wischs entscheidet. Jede Hand hat ihre Spur, die Sperre gilt für beide (zwei Hände
//     zugleich lösen einmal aus). Geht die andere Hand dabei mehr als GegenM gegenläufig nach rechts (Wegschieben mit zwei Händen auseinander, Zusammenführen), zählt
//     der Strich nicht (V1d, Befund Test «Auseinander» des Gestenlabors mit der linken Hand).
//     Gemessen wird der Weg nach links vom am weitesten rechts liegenden Punkt im Zeitfenster «wegwischen.zeit_s» bis jetzt – Ausholen nach rechts löst
//     nichts aus, es verlängert nur den Anlauf. Die Bewegung weicht höchstens «wegwischen.richtung_grad» von «nach links» ab (nicht nach oben, unten, vorn).
//     Ab «wegwischen.weg_m» nach links: das Ereignis (einmal), danach eine Sperre. Während der Geste der Weg nach links, damit das Ziel mitläuft.
//   - Abgrenzung: ohne Pinch löst sie weder Pinch + Wisch (Etage, Blättern, Drehen) noch den Sprung aus – die brauchen den Pinch; die Hebegeste braucht beide Hände
//     mit den Handflächen oben (Tests).
//   - Ziel (Wegwischen.Ziel), wenn mehreres sichtbar ist: was die Hand beim Wischen überstreicht bzw. vor dem sie wischt (im Blick vom Auge, Glas.AufEbene), bei
//     mehreren das mit den meisten Punkten des Wegs; sonst das zuletzt Eingeblendete – so geht zuerst das Vorderste (die App bzw. die Karte), beim nächsten Wischen das
//     Symbolfeld, ebenso der PC-Bildschirm (V1c) nach seinem Einblendzeitpunkt. Die App hängt ihre Ziele über einen Einhängepunkt ein (IWegwischbar).
// Alle Schwellen sind Annahmen V1a (Stellschrauben wegwischen.*).

namespace Hlks.Xr.Bedienung;

/// <summary>Schwellen des Wegwischens (Stellschrauben «wegwischen.*», Annahmen V1a).</summary>
public sealed record WegwischEinstellungen
{
    /// <summary>«wegwischen.weg_m»: so weit muss die Hand nach links (Annahme V1a 0.18 m, etwas mehr als der Mindestweg eines Wischs).</summary>
    public double WegM { get; init; } = 0.18;
    /// <summary>«wegwischen.zeit_s»: in höchstens so viel Zeit (Annahme V1a 0.6 s – ein Wischen, kein Schieben).</summary>
    public double ZeitS { get; init; } = 0.6;
    /// <summary>«wegwischen.handflaeche_grad»: die Handfläche zeigt höchstens so weit neben waagrecht nach links (Annahme V1a 50°).</summary>
    public double HandflaecheGrad { get; init; } = 50;
    /// <summary>«wegwischen.richtung_grad»: die Bewegung weicht höchstens so weit von «nach links» ab (Annahme V1a 35°).</summary>
    public double RichtungGrad { get; init; } = 35;

    public static readonly WegwischEinstellungen Vorgabe = new();
}

/// <summary>Stand eines Bildes: Ereignis (einmal), Weg nach links (m) seit dem Anlauf, Geste läuft (Hand in der Haltung und unterwegs nach links), Weg der Hand.</summary>
public sealed record WegwischStand(bool Ereignis, double Weg, bool Laeuft, IReadOnlyList<Vec3> Pfad);

/// <summary>Zustand des Wegwischens (Punkte der rechten bzw. – V1d – linken Hand, solange sie in der Haltung ist; eine Sperre für beide).</summary>
public sealed class WegwischZustand
{
    internal readonly List<(double T, Vec3 P)> Punkte = [];
    internal readonly List<(double T, Vec3 P)> PunkteLinks = [];
    // V1d: Weg beider Hände im Zeitfenster, gleich in welcher Haltung (gegenläufige zweite Hand erkennen)
    internal readonly List<(double T, Vec3 P)> VerlaufRechts = [];
    internal readonly List<(double T, Vec3 P)> VerlaufLinks = [];
    internal double SperreBis = double.NegativeInfinity;
    /// <summary>V1d: welche Hand zuletzt ausgelöst hat bzw. gerade wischt («right», «left»), sonst null.</summary>
    public string? Hand { get; internal set; }
}

/// <summary>Ein mögliches Ziel des Wegwischens: Id, Fläche im Raum (zum Auge gedreht bzw. Hülle im Blick), eingeblendet seit (s).</summary>
public sealed record WegwischKandidat(string Id, TabletPose Flaeche, double EingeblendetSeit);

/// <summary>Zustand der Wahl des Ziels über eine Geste (<see cref="Wegwischen.ZielFuehren"/>).</summary>
public sealed class WegwischZielZustand
{
    internal string? Ziel;
}

public static class Wegwischen
{
    /// <summary>Nach einem Ereignis zählt keine Geste so lange (s).</summary>
    public const double SperreS = 0.8;
    /// <summary>Ab so viel Weg nach links (m) «läuft» die Geste (das Ziel läuft mit; ein Wisch über eine Messlinie zählt dann nicht).</summary>
    public const double LaeuftAbM = 0.03;

    /// <summary>Waagrecht nach links zum Blick (Gieren wie VrLogik: 90° = Blick nach −z).</summary>
    public static Vec3 Links(double blickGieren) => HebeGeste.Rechts(blickGieren) * -1;

    /// <summary>Ein Schritt je Bild mit der rechten Hand (null = nicht erkannt). Siehe Dateikopf.</summary>
    public static WegwischStand Schritt(WegwischZustand z, double t, HandBeobachtung? rechts, double blickGieren, WegwischEinstellungen? e = null) =>
        Schritt(z, t, rechts, null, blickGieren, e);

    /// <summary>
    /// V1d (Christian 24.10.2026: «wisch mit der linken hand … von rechts nach links soll in wieder raus schieben»): ein Schritt je Bild mit beiden Händen (null = nicht
    /// erkannt). Rechts wie bis V1d (Handfläche nach links); links die Handfläche seitlich in einer der beiden Richtungen – die Richtung des Wischs (rechts nach links)
    /// entscheidet. Jede Hand hat ihre eigene Spur; es gilt eine Sperre für beide, so dass zwei Hände zugleich nur einmal auslösen. Bei zwei laufenden Wischen zählt der
    /// weitere.
    /// </summary>
    public static WegwischStand Schritt(WegwischZustand z, double t, HandBeobachtung? rechts, HandBeobachtung? links, double blickGieren, WegwischEinstellungen? e = null)
    {
        e ??= WegwischEinstellungen.Vorgabe;
        var nachLinks = Links(blickGieren);
        var cos = Math.Cos(e.HandflaecheGrad * Math.PI / 180);
        var r = Spur(z.Punkte, t, rechts is { Offen: true, Pinch: false } hr && hr.Normale.Skalar(nachLinks) >= cos ? hr.Mitte : null, nachLinks, e, z.SperreBis);
        var l = Spur(z.PunkteLinks, t, links is { Offen: true, Pinch: false } hl && Math.Abs(hl.Normale.Skalar(nachLinks)) >= cos ? hl.Mitte : null, nachLinks, e, z.SperreBis);
        // V1d: bewegt sich die andere Hand zugleich gegenläufig (nach rechts), ist es kein Wegwischen – z. B. das Wegschieben mit zwei Händen (auseinander) bzw. das
        // Zusammenführen; die Spur dieser Hand beginnt neu
        Verlauf(z.VerlaufRechts, t, rechts?.Mitte, e);
        Verlauf(z.VerlaufLinks, t, links?.Mitte, e);
        if (r.Laeuft || r.Ereignis) r = Gegenlaeufig(z.VerlaufLinks, nachLinks, r, z.Punkte);
        if (l.Laeuft || l.Ereignis) l = Gegenlaeufig(z.VerlaufRechts, nachLinks, l, z.PunkteLinks);
        foreach (var (s, hand) in new[] { (r, "right"), (l, "left") })
        {
            if (!s.Ereignis) continue;
            z.SperreBis = t + SperreS;
            z.Punkte.Clear();
            z.PunkteLinks.Clear();
            z.Hand = hand;
            return s;
        }
        var lauf = l.Weg > r.Weg ? (l, "left") : (r, "right");
        z.Hand = lauf.Item1.Laeuft ? lauf.Item2 : null;
        return lauf.Item1;
    }

    /// <summary>Spur einer Hand (Punkte im Zeitfenster): Weg nach links vom Anlauf (am weitesten rechts), Richtung, Ereignis ab «wegwischen.weg_m»; mitte null = Hand nicht in der Haltung.</summary>
    /// <summary>V1d: so weit (m) darf sich die andere Hand während des Wischs gegenläufig bewegen (nach rechts), sonst ist es kein Wegwischen (Annahme V1d).</summary>
    public const double GegenM = 0.08;

    static void Verlauf(List<(double T, Vec3 P)> v, double t, Vec3? p, WegwischEinstellungen e)
    {
        if (p is not { } q) { v.Clear(); return; }
        v.Add((t, q));
        while (v.Count > 0 && v[0].T < t - e.ZeitS - 1e-9) v.RemoveAt(0);
    }

    /// <summary>Ist die andere Hand im Zeitfenster mehr als GegenM nach rechts gegangen (vom am weitesten links liegenden Punkt bis jetzt), zählt der Strich nicht.</summary>
    static WegwischStand Gegenlaeufig(List<(double T, Vec3 P)> andere, Vec3 links, WegwischStand s, List<(double T, Vec3 P)> spur)
    {
        if (andere.Count < 2) return s;
        var nachRechts = andere.Max(x => x.P.Skalar(links)) - andere[^1].P.Skalar(links);
        if (nachRechts <= GegenM) return s;
        spur.Clear();
        return new WegwischStand(false, 0, false, []);
    }

    static WegwischStand Spur(List<(double T, Vec3 P)> punkte, double t, Vec3? mitte, Vec3 links, WegwischEinstellungen e, double sperreBis)
    {
        if (mitte is not { } m) { punkte.Clear(); return new WegwischStand(false, 0, false, []); }
        punkte.Add((t, m));
        while (punkte.Count > 0 && punkte[0].T < t - e.ZeitS - 1e-9) punkte.RemoveAt(0);
        if (t < sperreBis) { punkte.Clear(); return new WegwischStand(false, 0, false, []); }   // in der Sperre beginnt nichts
        // Anlauf: der am weitesten rechts liegende Punkt im Fenster (der letzte dort, 1 cm Spiel); Ausholen nach rechts verschiebt nur den Anlauf
        var jetzt = punkte[^1].P;
        var rechtest = punkte.Min(x => x.P.Skalar(links));
        var anlauf = punkte.FindLastIndex(x => x.P.Skalar(links) <= rechtest + 0.01);
        var start = punkte[anlauf].P;
        var d = jetzt - start;
        var weg = Math.Max(0, jetzt.Skalar(links) - rechtest);
        var richtungOk = d.Laenge < 1e-6 || d.Skalar(links) >= Math.Cos(e.RichtungGrad * Math.PI / 180) * d.Laenge;
        var pfad = punkte.Skip(anlauf).Select(x => x.P).ToList();
        if (weg >= e.WegM && richtungOk) return new WegwischStand(true, weg, false, pfad);
        return new WegwischStand(false, richtungOk ? weg : 0, richtungOk && weg >= LaeuftAbM, pfad);
    }

    /// <summary>
    /// Ziel über die ganze Geste: das Ziel, das gilt, sobald die Geste läuft, bleibt bis zu ihrem Ende (Ereignis bzw. Abbruch) – das Ziel läuft mit der Hand mit und
    /// verliesse sonst den Weg der Hand (Befund Prüflauf 03: die mitlaufende App fiel aus dem Weg, die Symbole wurden weggewischt). Ein Kandidat, der unterwegs verschwindet,
    /// gibt das Ziel frei. Gibt das Ziel dieses Bildes zurück (null, wenn die Geste nicht läuft).
    /// </summary>
    public static string? ZielFuehren(WegwischZielZustand z, WegwischStand stand, IReadOnlyList<WegwischKandidat> sichtbar, Vec3 auge)
    {
        if (!stand.Laeuft && !stand.Ereignis) { z.Ziel = null; return null; }
        if (z.Ziel is null || !sichtbar.Any(k => k.Id == z.Ziel)) z.Ziel = Ziel(sichtbar, stand.Pfad, auge);
        var ziel = z.Ziel;
        if (stand.Ereignis) z.Ziel = null;
        return ziel;
    }

    /// <summary>
    /// Ziel des Wegwischens (siehe Dateikopf): unter den sichtbaren Kandidaten das, über das bzw. vor dem der Weg der Hand im Blick vom Auge liegt (Rand m in seiner
    /// Ebene), bei mehreren das mit den meisten Punkten des Wegs (dann das zuletzt eingeblendete); sonst das zuletzt eingeblendete; null ohne Kandidaten.
    /// </summary>
    public static string? Ziel(IReadOnlyList<WegwischKandidat> sichtbar, IReadOnlyList<Vec3> pfad, Vec3 auge, double rand = 0.03)
    {
        if (sichtbar.Count == 0) return null;
        var treffer = sichtbar
            .Select(k => (k, n: pfad.Count(p => Glas.AufEbene(p, auge, k.Flaeche) is { } q
                && Math.Abs((q - k.Flaeche.Mitte).Skalar(k.Flaeche.Rechts)) <= k.Flaeche.B / 2 + rand
                && Math.Abs((q - k.Flaeche.Mitte).Skalar(k.Flaeche.Oben)) <= k.Flaeche.H / 2 + rand)))
            .Where(x => x.n > 0)
            .OrderByDescending(x => x.n).ThenByDescending(x => x.k.EingeblendetSeit)
            .FirstOrDefault();
        return treffer.k?.Id ?? sichtbar.OrderByDescending(k => k.EingeblendetSeit).First().Id;
    }
}
