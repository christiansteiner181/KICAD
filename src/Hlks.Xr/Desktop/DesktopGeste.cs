// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): die Gesten zum Bildschirm des PCs. Rein, ohne StereoKit; Hände und Kopf im Raum der Brille.
//
// Christian, 24.10.2026 (mit Berichtigung und Ergänzung desselben Tages):
//   - Hineinziehen: rechte Hand offen, Handfläche nach links, Wisch von LINKS nach RECHTS. Die Handfläche «zieht» den Bildschirm mit: er kommt von links ins Bild,
//     folgt der Hand und setzt sich frontal.
//   - Hinausschieben = die gemeinsame Wegwisch-Geste der ganzen App: dieselbe Haltung, Wisch von RECHTS nach LINKS. Die Erkennung legt V1a als gemeinsame Geste in
//     src/Hlks.Xr/Bedienung/Wegwischen.cs an; hier ist sie vorerst hinter IWegwischErkennung gekapselt (DesktopWegwischen). EINHÄNGEPUNKT für die Zusammenführung:
//     DesktopGesten(IWegwischErkennung? weg) – dort V1a's Wegwischen als IWegwischErkennung übergeben, sonst nichts ändern.
//   - Die VR-Szene bleibt in beiden Fällen starr (kein Drehen, Blättern, Springen, Etagenwechsel; das sperrt die App, solange der Bildschirm da ist).
//
// Regel (SeitenWisch.Schritt, je Bild):
//   Haltung: rechte Hand erkannt und offen (XrTabletLogik.IstOffen), kein Pinch (mit Hysterese), Normale der Handfläche höchstens HandflaecheGrad neben waagrecht
//   nach links (zum Blick); dazu «erlaubt» (die Hand ist nicht an Apps oder am Griff). Passt die Haltung nicht, beginnt alles neu.
//   Bewegung in Richtung r (Hineinziehen: nach rechts, Wegwischen: nach links), im Zeitfenster ZeitS: Weg = Lage jetzt minus tiefster Stand in r; Beginn = der
//   letzte Punkt am tiefsten Stand (1 cm Spiel); quer dazu höchstens 0.6 × Weg (wie V1a, HebeGeste.SeitlichAnteil).
//   Ausgelöst: Weg ≥ WegM, mittleres Tempo seit dem Beginn ≥ TempoMs (oder über die letzten WegM: halb ziehen, kurz halten, zügig weiterziehen zählt), die linke
//   Hand seit dem Beginn höchstens AndereHandM bewegt (oder nicht erkannt), nicht in der Sperre. Danach SperreS lang nichts (beide Gesten, DesktopGesten).
//   Fortschritt (für das Folgen der Hand): (Weg − 20 % WegM) / (80 % WegM), 0 … 1; Läuft, sobald 20 % erreicht sind.
// Warum Hebegeste, Wegschieben mit zwei Händen, Pinch + Wisch, Röntgen-Hand nicht auslösen (Tests DesktopGesteTests): Hebegeste – Handflächen oben; zwei Hände –
// die linke bewegt sich mit; Pinch + Wisch – Pinch; Röntgen-Hand – Handfläche nach vorn. Ausholen vor dem Wisch: zu kurz bzw. zu langsam, und Hineinziehen
// und Wegwischen schauen in entgegengesetzte Richtungen; das Zurückholen der Hand nach der Geste fällt in die Sperre.

namespace Hlks.Xr.Desktop;

/// <summary>Stand eines Seitenwischs in einem Bild: ausgelöst (nur in diesem Bild), Fortschritt 0 … 1, läuft (Hand folgt), Weg in m.</summary>
public readonly record struct SeitenWischStand(bool Ausgeloest, double Fortschritt, bool Laeuft, double WegM)
{
    public static readonly SeitenWischStand Nichts = new(false, 0, false, 0);
}

/// <summary>Zustand eines Seitenwischs (Punkte der rechten Hand, solange die Haltung passt; Spur der linken Hand; Pinch vorher; Sperre).</summary>
public sealed class SeitenWischZustand
{
    internal readonly List<(double T, Vec3 P)> Punkte = [];
    internal readonly List<(double T, Vec3 P)> Andere = [];
    internal bool PinchVorher;
    internal double SperreBis = double.NegativeInfinity;

    /// <summary>Sperre von aussen (Gegengeste ausgelöst): bis t nichts, und die laufende Bewegung zählt nicht weiter.</summary>
    public void Sperren(double bis)
    {
        SperreBis = Math.Max(SperreBis, bis);
        Punkte.Clear();
    }
}

public static class SeitenWisch
{
    /// <summary>Quer zur Richtung höchstens so viel im Verhältnis zum Weg (wie V1a HebeGeste.SeitlichAnteil).</summary>
    public const double SeitlichAnteil = 0.6;
    /// <summary>Ab diesem Anteil des Wegs folgt der Bildschirm der Hand (darunter ruht er: kleines Zittern zeigt nichts).</summary>
    public const double AnsatzAnteil = 0.2;
    /// <summary>Spiel beim Beginn: der letzte Punkt höchstens so nah (m) am tiefsten Stand.</summary>
    public const double BeginnSpielM = 0.01;

    /// <summary>
    /// Ein Schritt je Bild (Regel im Dateikopf). rechts/links = Gelenke der rechten bzw. linken Hand (WebXR-Namen, Raum der Brille; null = nicht erkannt),
    /// blickGieren wie VrLogik, nachRechts = Hineinziehen (sonst Wegwischen), erlaubt = die Hand darf (nicht an Apps, nicht am Griff, nicht in einer Gegensperre).
    /// </summary>
    public static SeitenWischStand Schritt(SeitenWischZustand z, double t, IReadOnlyDictionary<string, Vec3>? rechts, IReadOnlyDictionary<string, Vec3>? links,
        double blickGieren, bool nachRechts, SeitenWischEinstellungen? e = null, PinchEinstellungen? pinch = null, bool erlaubt = true)
    {
        e ??= SeitenWischEinstellungen.Vorgabe;
        var behalten = e.ZeitS + 0.2;
        // linke Hand: Spur der Handfläche, solange erkannt (für «die andere Hand ruhig»)
        if (XrTabletLogik.HandflaechenMitte(links) is { } lm) z.Andere.Add((t, lm));
        else z.Andere.Clear();
        while (z.Andere.Count > 0 && z.Andere[0].T < t - behalten) z.Andere.RemoveAt(0);

        var r = XrGesten.Rechts(blickGieren);
        if (!nachRechts) r = r * -1;
        var linksRichtung = XrGesten.Rechts(blickGieren) * -1;
        var pinchJetzt = rechts is not null && XrTabletLogik.IstPinch(rechts, z.PinchVorher, pinch);
        z.PinchVorher = pinchJetzt;
        var mitte = XrTabletLogik.HandflaechenMitte(rechts);
        var normale = XrTabletLogik.HandflaechenNormale(rechts, "right");
        var haltung = erlaubt && mitte is not null && normale is not null && !pinchJetzt && XrTabletLogik.IstOffen(rechts)
            && normale.Value.Skalar(linksRichtung) >= Math.Cos(e.HandflaecheGrad * Num.Grad);
        if (!haltung) { z.Punkte.Clear(); return SeitenWischStand.Nichts; }
        z.Punkte.Add((t, mitte!.Value));
        while (z.Punkte.Count > 0 && z.Punkte[0].T < t - behalten) z.Punkte.RemoveAt(0);
        if (t < z.SperreBis)
        {
            // in der Sperre beginnt nichts: eine Bewegung, die hier anfängt, zählt nach der Sperre nicht (sonst löste sie verspätet aus)
            z.Punkte.Clear();
            return SeitenWischStand.Nichts;
        }
        var fenster = z.Punkte.Where(x => x.T >= t - e.ZeitS - 1e-9).ToList();
        var jetzt = fenster[^1].P;
        var kleinst = fenster.Min(x => x.P.Skalar(r));
        var beginn = fenster.Last(x => x.P.Skalar(r) <= kleinst + BeginnSpielM);
        var weg = jetzt.Skalar(r) - kleinst;
        var d = jetzt - beginn.P;
        var quer = (d - r * d.Skalar(r)).Laenge;
        var ruhig = AndereRuhig(z, beginn.T, e.AndereHandM);
        var ansatz = AnsatzAnteil * e.WegM;
        var fortschritt = ruhig ? Math.Clamp((weg - ansatz) / (e.WegM - ansatz), 0, 1) : 0;
        var laeuft = ruhig && weg >= ansatz;
        // Tempo: im Mittel seit dem Beginn – oder über die letzten WegM (wer halb zieht, kurz hält und dann zügig weiterzieht, löst aus; ein langsamer Weg nicht)
        var tempo = weg / Math.Max(1e-3, t - beginn.T);
        if (tempo < e.TempoMs && weg >= e.WegM)
            for (int i = fenster.Count - 2; i >= 0; i--)
                if ((jetzt - fenster[i].P).Skalar(r) >= e.WegM) { tempo = Math.Max(tempo, e.WegM / Math.Max(1e-3, t - fenster[i].T)); break; }
        if (weg >= e.WegM && tempo >= e.TempoMs && quer <= SeitlichAnteil * weg && ruhig)
        {
            z.SperreBis = t + e.SperreS;
            z.Punkte.Clear();
            return new SeitenWischStand(true, 1, false, weg);
        }
        return new SeitenWischStand(false, fortschritt, laeuft, weg);
    }

    /// <summary>Hat sich die linke Hand seit seit (s) höchstens um maxM bewegt? Ohne Spur (nicht erkannt): ja.</summary>
    static bool AndereRuhig(SeitenWischZustand z, double seit, double maxM)
    {
        var teil = z.Andere.Where(x => x.T >= seit - 1e-9).ToList();
        if (teil.Count < 2) return true;
        var start = teil[0].P;
        return teil.All(x => x.P.Abstand(start) <= maxM);
    }
}

/// <summary>Hände und Kopf eines Bildes für die Gesten (Raum der Brille): Zeit, Gelenke der rechten und linken Hand (null = nicht erkannt), Gierwinkel des Blicks.</summary>
public sealed record DesktopHaende(double T, IReadOnlyDictionary<string, Vec3>? Rechts, IReadOnlyDictionary<string, Vec3>? Links, double BlickGieren);

/// <summary>
/// Erkennung des Wegwischens (rechte Hand offen, Handfläche nach links, Wisch rechts → links). Einhängepunkt für die gemeinsame Geste von V1a
/// (src/Hlks.Xr/Bedienung/Wegwischen.cs): beim Zusammenführen dort einen Adapter übergeben (DesktopGesten).
/// </summary>
public interface IWegwischErkennung
{
    /// <summary>Ein Bild; erlaubt = false heisst: die Bewegung zählt nicht (z. B. Sperre nach dem Hineinziehen).</summary>
    SeitenWischStand Schritt(DesktopHaende h, bool erlaubt);
}

/// <summary>Vorläufige eigene Erkennung des Wegwischens (bis V1a's gemeinsame Geste zusammengeführt ist): SeitenWisch nach links mit «gesten.wegwischen.*».</summary>
public sealed class DesktopWegwischen(Func<SeitenWischEinstellungen> einstellungen, Func<PinchEinstellungen?>? pinch = null) : IWegwischErkennung
{
    readonly SeitenWischZustand z = new();
    public SeitenWischZustand Zustand => z;

    public SeitenWischStand Schritt(DesktopHaende h, bool erlaubt) =>
        SeitenWisch.Schritt(z, h.T, h.Rechts, h.Links, h.BlickGieren, nachRechts: false, einstellungen(), pinch?.Invoke(), erlaubt);
}

/// <summary>Stand beider Gesten in einem Bild.</summary>
public readonly record struct DesktopGestenStand(SeitenWischStand Hinein, SeitenWischStand Weg)
{
    /// <summary>Läuft gerade eine der beiden (die App wertet dann keinen anderen einhändigen Wisch aus, z. B. über Masslinien).</summary>
    public bool Laeuft => Hinein.Laeuft || Weg.Laeuft || Hinein.Ausgeloest || Weg.Ausgeloest;
}

/// <summary>
/// Beide Gesten mit gegenseitiger Sperre: nach dem Hineinziehen zählt das Wegwischen SperreS lang nicht (das Zurückholen der Hand schiebt den Bildschirm nicht
/// gleich wieder hinaus) und umgekehrt. hineinErlaubt = der Bildschirm ist aus (bzw. kommt); wegErlaubt = er ist da (bzw. geht).
/// </summary>
public sealed class DesktopGesten
{
    readonly SeitenWischZustand hinein = new();
    readonly IWegwischErkennung weg;
    double hineinGesperrtBis = double.NegativeInfinity, wegGesperrtBis = double.NegativeInfinity;

    /// <param name="weg">Erkennung des Wegwischens; null = die vorläufige eigene (DesktopWegwischen). EINHÄNGEPUNKT für V1a's Wegwischen.</param>
    public DesktopGesten(Func<DesktopEinstellungen> einstellungen, Func<PinchEinstellungen?>? pinch = null, IWegwischErkennung? weg = null)
    {
        Einstellungen = einstellungen;
        Pinch = pinch;
        this.weg = weg ?? new DesktopWegwischen(() => einstellungen().Weg, pinch);
    }

    Func<DesktopEinstellungen> Einstellungen { get; }
    Func<PinchEinstellungen?>? Pinch { get; }
    public IWegwischErkennung Wegwischen => weg;

    public DesktopGestenStand Schritt(DesktopHaende h, bool hineinErlaubt, bool wegErlaubt)
    {
        var e = Einstellungen();
        var hi = SeitenWisch.Schritt(hinein, h.T, h.Rechts, h.Links, h.BlickGieren, nachRechts: true, e.Hinein, Pinch?.Invoke(), hineinErlaubt && h.T >= hineinGesperrtBis);
        var we = weg.Schritt(h, wegErlaubt && h.T >= wegGesperrtBis);
        if (hi.Ausgeloest) wegGesperrtBis = h.T + e.Hinein.SperreS;
        if (we.Ausgeloest) { hineinGesperrtBis = h.T + e.Weg.SperreS; hinein.Sperren(hineinGesperrtBis); }
        return new DesktopGestenStand(hi, we);
    }
}
