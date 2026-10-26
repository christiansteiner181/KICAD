// Quest App P5a: die schwebenden Apps mit dem Zeigefinger berühren (Poke) bzw. mit Pinch wählen – grosse Ziele, Fang zum nächsten Ziel, Leuchten beim Annähern
// (Leitlinie Bedienung, Christian 23.10.2026: «Grosse Ziele und ein Fang zum nächsten Ziel machen Genauigkeit unnötig», «Ton und Licht melden jeden Fang …
// und jede Berührung»). Rein, je Bild und Hand ein Schritt; die App zeichnet, spielt Ton und Licht und führt die Kachel aus.
//
// Regeln (Annahmen P5a, Werte unten benannt, Fangradius als Stellschraube «fang.radius_m»):
//   - Ziel (Fang): die Kachel, deren Rechteck (um den Fangradius vergrössert) die Fingerspitze in der Projektion enthält und vor der die Spitze höchstens
//     NaeheM liegt (bis HinterM dahinter); bei mehreren die nächste (Abstand zum Rechteck, dann Tiefe), die bisherige mit 5 mm Vorzug (kein Flackern).
//   - Leuchten: Nähe = 1 − Tiefe / NaeheM (0 … 1).
//   - Antippen: die Spitze geht von vorn durch die Druckebene (DruckTiefeM vor der Fläche) – im letzten Bild davor, jetzt darauf oder dahinter –, ohne Pinch.
//     Wer schon dahinter ist oder von der Seite hineinfährt, löst nichts aus. Ein Sprung des Trackings (mehr als SprungM in einem Bild) zählt nicht.
//   - Pinch wählt: beginnt ein Pinch an einer Kachel und endet er dort, ohne dass die Hand weiter als PinchWegM wanderte, gilt das wie Antippen (beim Loslassen,
//     damit ein Pinch + Wisch über einer Liste blättert statt zu wählen).
//   - Nach dem Auslösen ist die Hand gesperrt, bis sie zurückgezogen ist (LoslassTiefeM vor der Fläche oder weg von den Kacheln) und kein Pinch hält – so löst
//     eine App, die sich unter dem Finger öffnet, nicht gleich ihren ersten Eintrag aus.
//   - P5b, daneben: geht die Spitze von vorn durch die Druckebene einer Kachel, liegt aber ausserhalb des Fangs (höchstens DanebenM weiter), meldet der Schritt
//     «Daneben» – ein Fehlgriff der Prüfaufgaben.

namespace Hlks.Xr.Bedienung;

/// <summary>Zustand einer Hand an den Apps.</summary>
public sealed class BeruehrHand
{
    /// <summary>Kachel im Fang (Index), sonst null.</summary>
    public int? Hover { get; internal set; }
    internal Vec3? LetzteSpitze;
    internal bool Gesperrt;
    internal bool PinchVorher;
    internal int? PinchKachel;
    internal Vec3? PinchStart;
}

/// <summary>Ergebnis eines Schritts: Kachel im Fang, Nähe (0 … 1), ausgelöste Kachel, neu im Fang (Ton), durch Pinch gewählt; P5b: ein Antippen daneben (Fehlgriff
/// in den Prüfaufgaben).</summary>
public readonly record struct BeruehrErgebnis(int? Hover, double Naehe, int? Ausgeloest, bool HoverNeu, bool PerPinch, bool Daneben = false);

public static class AppBeruehrung
{
    /// <summary>Ab diesem Abstand vor der Kachel leuchtet sie (m), Annahme P5a.</summary>
    public const double NaeheM = 0.10;
    /// <summary>Die Mitte der Fingerspitze erreicht die Kachel, wenn sie so nah vor der Fläche ist (m; ≈ Radius der Spitze), Annahme P5a.</summary>
    public const double DruckTiefeM = 0.005;
    /// <summary>Nach dem Auslösen ist die Hand erst wieder bereit, wenn die Spitze so weit vor der Fläche ist (m), Annahme P5a.</summary>
    public const double LoslassTiefeM = 0.025;
    /// <summary>So weit hinter der Fläche zählt die Spitze noch zur Kachel (m), wie das Tablet des Prototyps (BeruehrHinter 4 cm) plus Spiel.</summary>
    public const double HinterM = 0.06;
    /// <summary>Mehr Weg der Spitze in einem Bild ist ein Sprung des Trackings (m), kein Antippen.</summary>
    public const double SprungM = 0.15;
    /// <summary>Ein Pinch, der weiter wandert, wählt nicht (er wischt), m.</summary>
    public const double PinchWegM = 0.04;
    /// <summary>Vorzug der bisherigen Kachel im Fang (m), gegen Flackern zwischen Nachbarn.</summary>
    public const double HoverVorzugM = 0.005;
    /// <summary>
    /// P5b, «Antippen daneben»: geht die Spitze von vorn durch die Druckebene einer Kachel, liegt dabei aber ausserhalb des Fangs und höchstens so weit (m) neben ihm,
    /// zählt das als Fehlgriff der Prüfaufgaben (Annahme P5b; weiter weg ist es keine Absicht, die Kachel zu treffen).
    /// </summary>
    public const double DanebenM = 0.06;

    /// <summary>Lage eines Punkts zur Kachel: Abstand zum Rechteck in der Ebene (0 = darüber) und Tiefe vor der Fläche (&gt; 0 = davor).</summary>
    public static (double Aussen, double Tiefe) Lage(TabletPose k, Vec3 p)
    {
        var w = p - k.Mitte;
        double x = w.Skalar(k.Rechts), y = w.Skalar(k.Oben), tiefe = w.Skalar(k.Normale);
        return (Num.Hypot(Math.Max(0, Math.Abs(x) - k.B / 2), Math.Max(0, Math.Abs(y) - k.H / 2)), tiefe);
    }

    /// <summary>Die Kachel im Fang für diese Fingerspitze (siehe Dateikopf), mit Abstand zum Rechteck und Tiefe; null, wenn keine.</summary>
    public static (int Index, double Aussen, double Tiefe)? Naechste(IReadOnlyList<TabletPose> ziele, Vec3 spitze, double fang, int? bisher = null)
    {
        (int Index, double Aussen, double Tiefe)? beste = null;
        double besteWert = double.PositiveInfinity;
        for (int i = 0; i < ziele.Count; i++)
        {
            var (aussen, tiefe) = Lage(ziele[i], spitze);
            if (aussen > fang || tiefe > NaeheM || tiefe < -HinterM) continue;
            var wert = aussen + Math.Max(0, tiefe) * 1e-3 - (i == bisher ? HoverVorzugM : 0);
            if (wert < besteWert) { besteWert = wert; beste = (i, aussen, tiefe); }
        }
        return beste;
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand. spitze = Spitze des Zeigefingers im Raum der Brille (null = Hand nicht erkannt), pinch = Pinch gehalten (mit den Schwellen
    /// der Stellschrauben), ziele = Lagen der bedienbaren Kacheln bzw. Einträge in diesem Bild, p = Fangradius.
    /// </summary>
    public static BeruehrErgebnis Schritt(BeruehrHand z, Vec3? spitze, bool pinch, IReadOnlyList<TabletPose> ziele, PinchEinstellungen? p = null)
    {
        var fang = (p ?? PinchEinstellungen.Vorgabe).FangRadiusM;
        if (spitze is not { } s || !s.IstEndlich)
        {
            z.Hover = null; z.LetzteSpitze = null; z.Gesperrt = false; z.PinchVorher = false; z.PinchKachel = null; z.PinchStart = null;
            return default;
        }
        var n = Naechste(ziele, s, fang, z.Hover);
        int? ausgeloest = null;
        var perPinch = false;
        if (n is { } k && !z.Gesperrt && !pinch && !z.PinchVorher && z.LetzteSpitze is { } alt && alt.Abstand(s) <= SprungM
            && Lage(ziele[k.Index], alt).Tiefe > DruckTiefeM && k.Tiefe <= DruckTiefeM)
            ausgeloest = k.Index;   // Antippen
        if (pinch && !z.PinchVorher)
        {
            z.PinchKachel = z.Gesperrt ? null : n?.Index;
            z.PinchStart = s;
        }
        else if (!pinch && z.PinchVorher)
        {
            // Pinch losgelassen: wählt, wenn er an derselben Kachel begann und endet und die Hand dabei ruhig blieb
            if (z.PinchKachel is { } pk && n?.Index == pk && z.PinchStart is { } ps && ps.Abstand(s) <= PinchWegM && !z.Gesperrt) { ausgeloest = pk; perPinch = true; }
            z.PinchKachel = null;
            z.PinchStart = null;
        }
        else if (pinch && z.PinchStart is { } start && start.Abstand(s) > PinchWegM) z.PinchKachel = null;   // gewandert: wischt, wählt nicht
        // P5b: Antippen daneben – durch die Druckebene einer Kachel, aber knapp neben ihrem Fang (siehe DanebenM)
        var daneben = false;
        if (n is null && !z.Gesperrt && !pinch && !z.PinchVorher && z.LetzteSpitze is { } vor && vor.Abstand(s) <= SprungM)
            for (int i = 0; i < ziele.Count && !daneben; i++)
            {
                var (aussen, tiefe) = Lage(ziele[i], s);
                if (aussen <= fang || aussen > fang + DanebenM || tiefe > DruckTiefeM || tiefe < -HinterM) continue;
                daneben = Lage(ziele[i], vor).Tiefe > DruckTiefeM;
            }
        if (ausgeloest is not null) z.Gesperrt = true;
        else if (z.Gesperrt && !pinch && (n is null || n.Value.Tiefe > LoslassTiefeM)) z.Gesperrt = false;
        var hoverNeu = n is not null && n.Value.Index != z.Hover;
        z.Hover = n?.Index;
        z.LetzteSpitze = s;
        z.PinchVorher = pinch;
        var naehe = n is { } q ? Math.Clamp(1 - Math.Max(0, q.Tiefe) / NaeheM, 0, 1) : 0;
        return new BeruehrErgebnis(n?.Index, naehe, ausgeloest, hoverNeu, perPinch, daneben);
    }
}
