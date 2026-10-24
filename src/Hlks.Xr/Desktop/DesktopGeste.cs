// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): das Hineinziehen des Bildschirms des PCs. Rein, ohne StereoKit; Hände und Kopf im Raum der Brille.
//
// Christian, 24.10.2026 (mit Berichtigung und Ergänzung desselben Tages):
//   - Hineinziehen: rechte Hand offen, Handfläche nach links, Wisch von LINKS nach RECHTS. Die Handfläche «zieht» den Bildschirm mit: er kommt von links ins Bild,
//     folgt der Hand und setzt sich frontal.
//   - Hinausschieben = die gemeinsame Wegwisch-Geste der ganzen App (V1a, src/Hlks.Xr/Bedienung/Wegwischen.cs): dieselbe Haltung, Wisch von RECHTS nach LINKS.
//     Der Bildschirm meldet sich dort als wegwischbares Ziel an (Id «desktop», DesktopAnsicht.Kandidat); hier steht nur die Gegensperre (DesktopGesten).
//   - Die VR-Szene bleibt starr (kein Drehen, Blättern, Springen, Etagenwechsel; das sperrt die App, solange der Bildschirm da ist).
//
// Regel (SeitenWisch.Schritt, je Bild):
//   Haltung: rechte Hand erkannt und offen (XrTabletLogik.IstOffen), kein Pinch (mit Hysterese), Normale der Handfläche höchstens HandflaecheGrad neben waagrecht
//   nach links (zum Blick; gleich wie «wegwischen.handflaeche_grad»); dazu «erlaubt» (die Hand ist nicht an Apps oder am Griff). Passt die Haltung nicht, beginnt
//   alles neu.
//   Bewegung nach rechts im Zeitfenster ZeitS: Weg = Lage jetzt minus am weitesten links; Beginn = der letzte Punkt dort (1 cm Spiel); quer dazu höchstens
//   0.6 × Weg (wie V1a, HebeGeste.SeitlichAnteil).
//   Ausgelöst: Weg ≥ WegM, mittleres Tempo seit dem Beginn ≥ TempoMs (oder über die letzten WegM: halb ziehen, kurz halten, zügig weiterziehen zählt), die linke
//   Hand seit dem Beginn höchstens AndereHandM bewegt (oder nicht erkannt), nicht in der Sperre.
//   Fortschritt (für das Folgen der Hand): (Weg − 20 % WegM) / (80 % WegM), 0 … 1; Läuft, sobald 20 % erreicht sind.
// Gegensperre (DesktopGesten): nach dem Hineinziehen ruht das Wegwischen SperreS lang (WegwischenRuht: das Zurückholen der Hand schiebt den Bildschirm nicht gleich
// wieder hinaus und wischt auch nichts anderes weg); nach jedem Wegwischen (NachWegwischen) ruht das Hineinziehen ebenso lang.
// V1d (Christian 24.10.2026: «wisch mit der linken hand von links nach rechts soll auch den monitor reinziehen. und wisch mit der linken hand von rechts nach links
// soll in wieder raus schieben»): dieselbe Regel auch für die linke Hand (eigene Spur; Handfläche seitlich in einer der beiden Richtungen, die Richtung des Wischs
// entscheidet; die rechte Hand muss dabei ruhen). Löst eine Hand aus, sperrt sie die andere mit – zwei Hände zugleich lösen höchstens einmal aus.
// Warum Hebegeste, Pinch + Wisch, Röntgen-Hand, zwei Hände nicht auslösen (Tests DesktopGesteTests): Hebegeste – Handflächen oben; zwei Hände – die andere bewegt
// sich mit; Pinch + Wisch – Pinch; Röntgen-Hand – Handfläche nach vorn. Ausholen vor dem Wisch: zu kurz bzw. zu langsam, und Hineinziehen und Wegwischen schauen in
// entgegengesetzte Richtungen; das Zurückholen der Hand nach der Geste fällt in die Gegensperre.

using Hlks.Xr.Bedienung;

namespace Hlks.Xr.Desktop;

/// <summary>Stand des Hineinziehens in einem Bild: ausgelöst (nur in diesem Bild), Fortschritt 0 … 1, läuft (Bildschirm folgt der Hand), Weg in m.</summary>
public readonly record struct SeitenWischStand(bool Ausgeloest, double Fortschritt, bool Laeuft, double WegM)
{
    public static readonly SeitenWischStand Nichts = new(false, 0, false, 0);
}

/// <summary>Zustand des Hineinziehens (Punkte der rechten Hand, solange die Haltung passt; Spur der linken Hand; Pinch vorher; Sperre).</summary>
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
    /// Ein Schritt je Bild (Regel im Dateikopf): Wisch der rechten Hand nach rechts. rechts/links = Gelenke der rechten bzw. linken Hand (WebXR-Namen, Raum der
    /// Brille; null = nicht erkannt), blickGieren wie VrLogik, erlaubt = die Hand darf (nicht an Apps, nicht am Griff, nicht in einer Gegensperre).
    /// </summary>
    /// V1d (Christian 24.10.2026: «wisch mit der linken hand von links nach rechts soll auch den monitor reinziehen»): seite = «left» – dann ist rechts die LINKE Hand
    /// (die wischende) und links die rechte (die andere); ihre Handfläche darf seitlich in einer der beiden Richtungen zeigen, die Richtung des Wischs entscheidet.
    public static SeitenWischStand Schritt(SeitenWischZustand z, double t, IReadOnlyDictionary<string, Vec3>? rechts, IReadOnlyDictionary<string, Vec3>? links,
        double blickGieren, SeitenWischEinstellungen? e = null, PinchEinstellungen? pinch = null, bool erlaubt = true, string seite = "right")
    {
        e ??= SeitenWischEinstellungen.Vorgabe;
        var behalten = e.ZeitS + 0.2;
        // die andere Hand: Spur der Handfläche, solange erkannt (für «die andere Hand ruhig»)
        if (XrTabletLogik.HandflaechenMitte(links) is { } lm) z.Andere.Add((t, lm));
        else z.Andere.Clear();
        while (z.Andere.Count > 0 && z.Andere[0].T < t - behalten) z.Andere.RemoveAt(0);

        var r = XrGesten.Rechts(blickGieren);
        var linksRichtung = r * -1;
        var pinchJetzt = rechts is not null && XrTabletLogik.IstPinch(rechts, z.PinchVorher, pinch);
        z.PinchVorher = pinchJetzt;
        var mitte = XrTabletLogik.HandflaechenMitte(rechts);
        var normale = XrTabletLogik.HandflaechenNormale(rechts, seite);
        var seitlich = normale is { } nn ? (seite == "left" ? Math.Abs(nn.Skalar(linksRichtung)) : nn.Skalar(linksRichtung)) : -1;
        var haltung = erlaubt && mitte is not null && normale is not null && !pinchJetzt && XrTabletLogik.IstOffen(rechts)
            && seitlich >= Math.Cos(e.HandflaecheGrad * Num.Grad);
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
/// Das Hineinziehen mit der Gegensperre zum gemeinsamen Wegwischen (V1a): nach dem Hineinziehen ruht das Wegwischen SperreS lang (<see cref="WegwischenRuht"/>),
/// nach jedem Wegwischen das Hineinziehen (<see cref="NachWegwischen"/>). Die Lage der Handfläche nimmt es vom Wegwischen (dieselbe Haltung).
/// </summary>
public sealed class DesktopGesten(Func<DesktopEinstellungen> einstellungen, Func<PinchEinstellungen?>? pinch = null, Func<WegwischEinstellungen?>? wegwischen = null)
{
    readonly SeitenWischZustand hinein = new(), hineinLinks = new();
    double wegGesperrtBis = double.NegativeInfinity;

    /// <summary>V1d: welche Hand zuletzt hereingezogen hat bzw. gerade zieht («right», «left»), sonst null.</summary>
    public string? Hand { get; private set; }

    /// <summary>
    /// Ein Bild; erlaubt = der Bildschirm ist aus bzw. kommt, und die rechte Hand ist frei; erlaubtLinks (V1d) = die linke Hand ist frei (ohne Angabe wie erlaubt).
    /// V1d: beide Hände ziehen herein (je eine Spur, die andere Hand ruhig); löst eine aus, sperrt sie die andere mit – zwei Hände zugleich lösen einmal aus.
    /// </summary>
    public SeitenWischStand Schritt(DesktopHaende h, bool erlaubt, bool? erlaubtLinks = null)
    {
        var e = einstellungen();
        var ein = e.HineinMit(wegwischen?.Invoke());
        var r = SeitenWisch.Schritt(hinein, h.T, h.Rechts, h.Links, h.BlickGieren, ein, pinch?.Invoke(), erlaubt);
        var l = SeitenWisch.Schritt(hineinLinks, h.T, h.Links, h.Rechts, h.BlickGieren, ein, pinch?.Invoke(), erlaubtLinks ?? erlaubt, "left");
        var wer = r.Ausgeloest ? "right" : l.Ausgeloest ? "left" : l.Fortschritt > r.Fortschritt ? "left" : "right";
        var s = wer == "left" ? l : r;
        Hand = s.Laeuft || s.Ausgeloest ? wer : null;
        if (r.Ausgeloest || l.Ausgeloest)
        {
            wegGesperrtBis = h.T + e.Hinein.SperreS;
            (r.Ausgeloest ? hineinLinks : hinein).Sperren(h.T + e.Hinein.SperreS);   // die andere Hand löst nicht gleich noch einmal aus
        }
        return s;
    }

    /// <summary>Ruht das gemeinsame Wegwischen gerade (kurz nach dem Hineinziehen)?</summary>
    public bool WegwischenRuht(double t) => t < wegGesperrtBis;

    /// <summary>Ein Wegwischen hat ausgelöst (gleich welches Ziel, gleich welche Hand): das Hineinziehen ruht SperreS lang, eine laufende Bewegung zählt nicht weiter.</summary>
    public void NachWegwischen(double t)
    {
        hinein.Sperren(t + einstellungen().Hinein.SperreS);
        hineinLinks.Sperren(t + einstellungen().Hinein.SperreS);
    }
}
