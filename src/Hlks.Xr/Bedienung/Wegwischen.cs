// Quest App V1a, Vorgabe Christian 24.10.2026: «wir vereinheitlichen die wegwisch geste. auch da nutzen wir die wischbewegung von rechts nach links.»
// EINE Geste «Wegwischen» für die ganze App: rechte Hand offen, Handfläche nach links, Wisch von rechts nach links (quer zum Blick). Sie schliesst die offene Glas-App
// und die Karte einer Prüfaufgabe (= Abbruch; Christian 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem Wegwischen geschlossen; Pinch + Wisch nach vorn
// entfällt»), schiebt die App-Symbole nach links aus dem Bild (ersetzt das beidhändige Wegschieben aus der Mitte) und – V1c – den PC-Bildschirm. Rein, ohne
// StereoKit; je Bild ein Schritt.
//   - Erkennung (Wegwischen.Schritt): nur die rechte Hand; offen, ohne Pinch, Handfläche höchstens «wegwischen.handflaeche_grad» neben «nach links» (waagrecht quer
//     zum Blick). Gemessen wird der Weg nach links vom am weitesten rechts liegenden Punkt im Zeitfenster «wegwischen.zeit_s» bis jetzt – Ausholen nach rechts löst
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

/// <summary>Zustand des Wegwischens (Punkte der rechten Hand, solange sie in der Haltung ist).</summary>
public sealed class WegwischZustand
{
    internal readonly List<(double T, Vec3 P)> Punkte = [];
    internal double SperreBis = double.NegativeInfinity;
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
    public static WegwischStand Schritt(WegwischZustand z, double t, HandBeobachtung? rechts, double blickGieren, WegwischEinstellungen? e = null)
    {
        e ??= WegwischEinstellungen.Vorgabe;
        var links = Links(blickGieren);
        var passt = rechts is { Offen: true, Pinch: false } h && h.Normale.Skalar(links) >= Math.Cos(e.HandflaecheGrad * Math.PI / 180);
        if (!passt) { z.Punkte.Clear(); return new WegwischStand(false, 0, false, []); }
        z.Punkte.Add((t, rechts!.Value.Mitte));
        while (z.Punkte.Count > 0 && z.Punkte[0].T < t - e.ZeitS - 1e-9) z.Punkte.RemoveAt(0);
        if (t < z.SperreBis) { z.Punkte.Clear(); return new WegwischStand(false, 0, false, []); }   // in der Sperre beginnt nichts
        // Anlauf: der am weitesten rechts liegende Punkt im Fenster (der letzte dort, 1 cm Spiel); Ausholen nach rechts verschiebt nur den Anlauf
        var jetzt = z.Punkte[^1].P;
        var rechtest = z.Punkte.Min(x => x.P.Skalar(links));
        var anlauf = z.Punkte.FindLastIndex(x => x.P.Skalar(links) <= rechtest + 0.01);
        var start = z.Punkte[anlauf].P;
        var d = jetzt - start;
        var weg = Math.Max(0, jetzt.Skalar(links) - rechtest);
        var richtungOk = d.Laenge < 1e-6 || d.Skalar(links) >= Math.Cos(e.RichtungGrad * Math.PI / 180) * d.Laenge;
        var pfad = z.Punkte.Skip(anlauf).Select(x => x.P).ToList();
        if (weg >= e.WegM && richtungOk)
        {
            z.SperreBis = t + SperreS;
            z.Punkte.Clear();
            return new WegwischStand(true, weg, false, pfad);
        }
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
