// Quest App P5a (Modul XR, Interface-Labor): feste Gestenbedeutungen (entwurf/modul-xr.md, «Ergänzungen», von Christian übernommen am 23.10.2026):
// «Jede Geste bedeutet in allen Apps dasselbe: Wisch links/rechts blättert, hoch/runter wechselt die Etage, Wegwischen schliesst, Pinch wählt, Faust greift,
// zwei Hände ändern den Massstab.» Hier stehen die Regeln dazu, rein und ohne StereoKit:
//   - Wisch in allen Richtungen (XrWischLogik erkennt im Prototyp nur waagrechte Wische) mit den Schwellen als Stellschrauben «wisch.*»;
//   - Einordnen der Richtung zum Blick: links, rechts, vor, zurück, hoch, runter (vor und zurück lösen seit V1a nichts aus; Wegwischen ist eine eigene Geste mit
//     offener Hand, Bedienung/Wegwischen.cs – Christian 24.10.2026);
//   - Wisch mit gehaltenem Pinch (PinchWisch) mit Richtungssperre: Die Gegenrichtung zählt kurz nach einem Wisch nicht, so löst das Zurückholen der Hand
//     nichts aus (Regel aus dem Drehen, Christian 23.10.2026: «schnellaufeinanderfolgende drehungen nur in eine richtung zulassen»);
//   - die Bedeutung eines Wischs im Zusammenhang (Gestenbedeutung.Wisch) und die Tabelle der Gesten (für die Hilfe in der Brille und OBERFLAECHE.md).
//
// ENTSCHEID P5a (Annahme bis zum Brillentest, im Labor zu vergleichen): Gewischt wird überall gleich, mit gehaltenem Pinch (Daumen und Zeigefinger zusammen,
// schnell ziehen) – so wie das Drehen, das Christian am 23.10.2026 abgenommen hat («Das neue Drehen passt»). Ein Wisch mit offener Hand (Prototyp, Masslinien)
// löste beim Senken des Arms leicht ungewollt einen Etagenwechsel aus.
//
// Konventionen: Szene bzw. Raum der Brille in Metern (Y oben); Gieren wie in VrLogik (Bogenmass, gegen den Uhrzeigersinn von oben, ab +x; Blick = (cos, 0, −sin)).

using System.Text.Json;

namespace Hlks.Xr;

/// <summary>Richtung eines Wischs, bezogen auf den Blick: links, rechts, vor (vom Körper weg), zurück (zum Körper), hoch, runter.</summary>
public enum WischRichtung { Links, Rechts, Vor, Zurueck, Hoch, Runter }

/// <summary>Schwellen der Wischgeste (Stellschrauben «wisch.mindestweg_m», «wisch.mindesttempo_ms», «wisch.richtung_toleranz_grad»).</summary>
public sealed record WischEinstellungen
{
    /// <summary>Kürzester Weg (Sehne) eines Wischs (m). Vorgabe <see cref="XrWischLogik.WischWegM"/> (0.15, Annahme Brillentest 21.10.2026).</summary>
    public double MindestwegM { get; init; } = XrWischLogik.WischWegM;
    /// <summary>Mindesttempo (Sehne / Zeit) in m/s. Vorgabe <see cref="XrWischLogik.WischVMs"/> (0.8).</summary>
    public double MindesttempoMs { get; init; } = XrWischLogik.WischVMs;
    /// <summary>Höchste Abweichung von der Hauptachse (waagrecht bzw. senkrecht; beim waagrechten Wisch auch von vorn/hinten) in Grad. Vorgabe ≈ 36.87° aus
    /// <see cref="XrWischLogik.WischWaagrecht"/> (Anteil 0.8 waagrecht, wie im Prototyp).</summary>
    public double RichtungToleranzGrad { get; init; } = ToleranzVorgabe;

    /// <summary>
    /// Vorgabe der Richtungstoleranz: arccos(0.8) = 36.8699° in Grad, auf 0.01° gerundet – so führt sie der Katalog des Labors genau (P5b: Vorgabe 36.87, Schritt
    /// 0.01; die Seite «XR-Labor» zeigt und nimmt nur Werte im Schritt). Für genau diesen Wert gilt weiter die Regel «mindestens 80 % waagrecht» des Prototyps
    /// (<see cref="CosToleranz"/> = 0.8).
    /// </summary>
    public const double ToleranzVorgabe = 36.87;

    public static readonly WischEinstellungen Vorgabe = new();

    /// <summary>Kosinus der Toleranz (Anteil der Hauptachse an der Sehne).</summary>
    public double CosToleranz => RichtungToleranzGrad == ToleranzVorgabe ? XrWischLogik.WischWaagrecht : Math.Cos(RichtungToleranzGrad * Num.Grad);
}

/// <summary>Pinch-Schwellen und Fangradius an einer Stelle (Stellschrauben «pinch.schwelle_an_m», «pinch.schwelle_aus_m», «fang.radius_m»).</summary>
public sealed record PinchEinstellungen
{
    /// <summary>Daumen- und Zeigefingerspitze näher als dies = Pinch (m). Vorgabe <see cref="XrTabletLogik.PinchAn"/> (2.5 cm).</summary>
    public double AnM { get; init; } = XrTabletLogik.PinchAn;
    /// <summary>Erst ab diesem Abstand ist der Pinch gelöst (m, Hysterese). Vorgabe <see cref="XrTabletLogik.PinchLos"/> (4 cm).</summary>
    public double AusM { get; init; } = XrTabletLogik.PinchLos;
    /// <summary>Fangradius am Finger (m): so weit neben einer Kachel rastet die Fingerspitze noch an ihr ein. Vorgabe 3 cm wie der Fang am Finger beim Messen
    /// (Annahme Brillentest 21.10.2026, xr-mess-logik.js).</summary>
    public double FangRadiusM { get; init; } = 0.03;

    public static readonly PinchEinstellungen Vorgabe = new();
}

/// <summary>Was ein Wisch mit Pinch im Zusammenhang auslöst (Gestenbedeutung.Wisch). Schliessen und Abbrechen gehen seit V1a nur mit dem Wegwischen (Wegwischen.cs).</summary>
public enum GestenAktion { Nichts, Drehen, SeiteVor, SeiteZurueck, EtageHoch, EtageRunter }

/// <summary>Zusammenhang einer Geste: Sitzungsraum oder Begehen, eine App (Liste) offen, die Apps sichtbar, Etage per Wisch erlaubt (Variante «etage»);
/// P5b: eine Prüfaufgabe läuft (ihre Karte steht vor einem).</summary>
public sealed record GestenKontext(bool Sitzungsraum, bool AppOffen, bool AppsSichtbar, bool EtageMitWisch = true, bool AufgabeOffen = false);

/// <summary>Zustand von <see cref="XrGesten.PinchWischSchritt"/> einer Hand: Wischerkennung und letzte Richtung (Sperre der Gegenrichtung).</summary>
public sealed class PinchWischZustand
{
    public Wischer Wischer { get; } = XrWischLogik.NeuerWischer();
    /// <summary>Zeit und Richtung des letzten anerkannten Wischs dieser Hand (bleiben über das Ende des Pinch hinaus).</summary>
    public double? LetzteT { get; internal set; }
    public WischRichtung? Letzte { get; internal set; }
}

public static class XrGesten
{
    /// <summary>Waagrechter Blickvektor zum Gieren (Länge 1).</summary>
    public static Vec3 Vorn(double blickGieren) => new(Math.Cos(blickGieren), 0, -Math.Sin(blickGieren));

    /// <summary>Waagrechter Rechts-Vektor zum Gieren (wie HandBegehen.Rechts).</summary>
    public static Vec3 Rechts(double blickGieren) => new(Math.Sin(blickGieren), 0, Math.Cos(blickGieren));

    /// <summary>Gegenrichtung eines Wischs.</summary>
    public static WischRichtung Gegenteil(WischRichtung r) => r switch
    {
        WischRichtung.Links => WischRichtung.Rechts,
        WischRichtung.Rechts => WischRichtung.Links,
        WischRichtung.Vor => WischRichtung.Zurueck,
        WischRichtung.Zurueck => WischRichtung.Vor,
        WischRichtung.Hoch => WischRichtung.Runter,
        _ => WischRichtung.Hoch,
    };

    /// <summary>
    /// Wisch in allen Richtungen im Verlauf einer Hand (aufsteigend in t, Ende = jetzt), wie XrWischLogik.WischErkennen: gesucht ist ein Anfang höchstens
    /// <see cref="XrWischLogik.WischZeitS"/> (0.4 s) und mindestens <see cref="XrWischLogik.WischMinZeitS"/> vor dem Ende, von dem aus die Sehne mit mindestens
    /// MindesttempoMs durchlaufen wurde und überwiegend waagrecht oder überwiegend senkrecht verläuft: die grössere der beiden Komponenten mindestens MindestwegM
    /// lang und mindestens cos(Toleranz) der ganzen Sehne. Richtung im Ergebnis: waagrecht (x, 0, z) bzw. (0, ±1, 0), Länge 1.
    /// </summary>
    public static WischErgebnis? WischErkennen(IReadOnlyList<WischProbe>? verlauf, WischEinstellungen? einstellungen = null)
    {
        var e = einstellungen ?? WischEinstellungen.Vorgabe;
        if (verlauf is null || verlauf.Count < 2) return null;
        var ende = verlauf[^1];
        var cos = e.CosToleranz;
        for (var i = 0; i < verlauf.Count - 1; i++)
        {
            var dt = ende.T - verlauf[i].T;
            if (dt > XrWischLogik.WischZeitS + 1e-9) continue;   // zu alt
            if (dt < XrWischLogik.WischMinZeitS) break;          // alle weiteren sind noch kürzer
            var d = ende.P - verlauf[i].P;
            double gesamt = d.Laenge, waagrecht = Num.Hypot(d.X, d.Z), senkrecht = Math.Abs(d.Y);
            if (gesamt / dt < e.MindesttempoMs) continue;
            var haupt = Math.Max(waagrecht, senkrecht);
            if (haupt < e.MindestwegM || haupt < cos * gesamt) continue;
            var richtung = waagrecht >= senkrecht ? new Vec3(d.X / waagrecht, 0, d.Z / waagrecht) : new Vec3(0, Math.Sign(d.Y), 0);
            return new WischErgebnis(verlauf.Skip(i).Select(s => s.P).ToList(), gesamt, dt, gesamt / dt, richtung);
        }
        return null;
    }

    /// <summary>
    /// Richtung eines Wischs (Ergebnis von <see cref="WischErkennen"/>) zum Blick: senkrecht = hoch/runter; waagrecht innerhalb der Toleranz um den Blick = vor
    /// (vom Körper weg) bzw. zurück; jeder andere waagrechte Wisch zählt seitlich (links/rechts nach dem Vorzeichen) – so dreht ein schräger Seitenwisch weiter wie
    /// in P4a. null bei einer Richtung ohne Länge.
    /// </summary>
    public static WischRichtung? Einordnen(Vec3 richtung, double blickGieren, double toleranzGrad)
    {
        if (!richtung.IstEndlich || richtung.Laenge < 1e-9) return null;
        var r = richtung.Einheit();
        var waagrecht = Num.Hypot(r.X, r.Z);
        if (Math.Abs(r.Y) > waagrecht) return r.Y > 0 ? WischRichtung.Hoch : WischRichtung.Runter;
        var w = new Vec3(r.X / waagrecht, 0, r.Z / waagrecht);
        var cos = Math.Cos(toleranzGrad * Num.Grad);
        var f = w.Skalar(Vorn(blickGieren));
        if (f >= cos) return WischRichtung.Vor;
        if (-f >= cos) return WischRichtung.Zurueck;
        return w.Skalar(Rechts(blickGieren)) > 0 ? WischRichtung.Rechts : WischRichtung.Links;
    }

    /// <summary>
    /// Ein Schritt je Bild und Hand: Wisch mit gehaltenem Pinch. punkt = Pinch-Punkt (null = Hand nicht erkannt), pinch = Pinch gehalten und Wischen erlaubt.
    /// Gibt die Richtung im ersten Bild eines Wischs zurück, sonst null. Die Gegenrichtung des letzten Wischs dieser Hand zählt innerhalb sperreS nicht
    /// (Stellschraube «begehen.drehen_sperre_s», gilt für alle Wische); Wische in dieselbe Richtung zählen und verlängern die Sperre.
    /// </summary>
    public static WischRichtung? PinchWischSchritt(PinchWischZustand z, double t, Vec3? punkt, bool pinch, double blickGieren, WischEinstellungen? einstellungen,
        double sperreS)
    {
        var e = einstellungen ?? WischEinstellungen.Vorgabe;
        var w = XrWischLogik.WischSchritt(z.Wischer, t, punkt, pinch && punkt is not null, e);
        if (w is not { Neu: true }) return null;
        if (Einordnen(w.Richtung, blickGieren, e.RichtungToleranzGrad) is not { } r) return null;
        if (z.LetzteT is { } lt && z.Letzte is { } lr && t - lt < sperreS && r == Gegenteil(lr)) return null;
        z.LetzteT = t;
        z.Letzte = r;
        return r;
    }
}

/// <summary>Die feste Bedeutung jeder Geste (Ergänzung «Ins Interface-Labor», Christian 23.10.2026) – Regel und Tabelle an einer Stelle.</summary>
public static class Gestenbedeutung
{
    /// <summary>
    /// Bedeutung eines Wischs (mit gehaltenem Pinch): links/rechts blättert in einer offenen App (nach links = nächste Seite, der Inhalt folgt der Hand), sonst dreht
    /// er beim Begehen (wenn der Schalter «Drehen» wirkt, HandBegehen); hoch/runter wechselt die Etage (Begehen und Sitzungstisch; nicht in der Variante
    /// «etage» = «app»); vor und zurück: nichts. V1a (Christian 24.10.2026: «Ja, auch offene Glas-Apps werden mit dem Wegwischen geschlossen; Pinch + Wisch nach vorn
    /// entfällt»): geschlossen bzw. abgebrochen wird nur noch mit dem Wegwischen (offene rechte Hand, von rechts nach links; Hlks.Xr.Bedienung.Wegwischen) –
    /// die offene App, die Karte einer Prüfaufgabe (Abbruch), die App-Symbole.
    /// </summary>
    public static GestenAktion Wisch(WischRichtung r, GestenKontext k) => r switch
    {
        WischRichtung.Links => k.AppOffen ? GestenAktion.SeiteVor : k.Sitzungsraum ? GestenAktion.Nichts : GestenAktion.Drehen,
        WischRichtung.Rechts => k.AppOffen ? GestenAktion.SeiteZurueck : k.Sitzungsraum ? GestenAktion.Nichts : GestenAktion.Drehen,
        WischRichtung.Hoch => k.EtageMitWisch ? GestenAktion.EtageHoch : GestenAktion.Nichts,
        WischRichtung.Runter => k.EtageMitWisch ? GestenAktion.EtageRunter : GestenAktion.Nichts,
        _ => GestenAktion.Nichts,
    };

    /// <summary>Die Gesten und ihre Bedeutung (Geste, Bedeutung) – dieselbe Tabelle steht in OBERFLAECHE.md und in der App «Status» in der Brille.</summary>
    public static readonly IReadOnlyList<(string Geste, string Bedeutung)> Tabelle =
    [
        ("Antippen (Zeigefinger auf einen Knopf)", "wählt bzw. schaltet (Register, Knopf, Eintrag einer Liste im Fenster der Apps)"),
        ("Pinch (Daumen + Zeigefinger)", "wählt: mit dem Strahl auf einen Knopf des Fensters zeigen und Pinch wie Antippen; beim Begehen auf den Boden zeigen und loslassen = springen; am Tisch den Griff fassen"),
        ("Wisch auf der Tafel (ohne Pinch, Hand auf dem Fenster einer App, rechts oder links)", "V1d: blättert – nach links bzw. oben die nächste Seite, nach rechts bzw. unten die vorige; die Seite gleitet mit; im freien Raum bleibt es Wegwischen bzw. Hineinziehen"),
        ("Pinch + Wisch links/rechts", "blättert in der App, wenn die Hand am Fenster ist (links = nächste Seite); beim Begehen sonst drehen um 30° (nur wenn «Drehen» wirkt)"),
        ("Pinch + Wisch hoch/runter", "Etage darüber/darunter (Begehen: über demselben Punkt, sonst im grössten Raum; Sitzungstisch: Etagenschnitt, über dem obersten «Alle Etagen»)"),
        ("Beide Handflächen nach oben, deutlich anheben", "V1a: holt die App-Symbole von unten in ein Feld vor dem Körper; ein Symbol mit der Hand berühren öffnet seine App"),
        ("Wegwischen: rechte Hand offen, Handfläche nach links (V1d auch die linke Hand, Handfläche seitlich), von rechts nach links", "V1a: schliesst, was die Hand überstreicht, sonst das Vorderste – die offene App bzw. die Karte einer Prüfaufgabe (Abbruch), beim nächsten Mal die App-Symbole, ebenso den Bildschirm des PCs; es gleitet nach links aus dem Bild"),
        ("Hineinziehen: rechte Hand offen, Handfläche nach links (V1d auch die linke Hand, Handfläche seitlich), zügig von links nach rechts", "V1c: zieht den Bildschirm des gekoppelten PCs ins Bild (er kommt von links und folgt der Hand); bedient wird mit Maus und Tastatur am PC, die VR-Szene bleibt starr"),
        ("Flache Hand über dem Modell (kurz ruhig)", "Sitzungstisch: Handfläche nach unten = waagrechter Schnitt auf Höhe der Hand, senkrecht gehalten = Vertikalschnitt; Hand weg behält ihn, «Tisch» › «Zurücksetzen» hebt ihn auf"),
        ("Offene Hand, Handfläche nach vorn", "Begehen: Röntgen-Hand – Wände und Decken im Kegel vor der Hand werden durchscheinend"),
        ("Faust", "greift (noch keine Funktion; Greifen kommt mit XR-B)"),
        ("Pinch am Griff an der vorderen Gebäudekante", "V1d, Sitzungstisch: ein Griff – das Modell folgt der Hand in alle Richtungen (auch in der Höhe) und dreht mit ihr; zoomen: Pinch der zweiten Hand (Abstand der Hände) bzw. Variante «Drehknopf»: das Handgelenk am Griff rollen (im Uhrzeigersinn grösser)"),
        ("Zögern am Griff", "eine Geisterhand zeigt die passende Geste"),
        ("Pinch halten, ziehen, loslassen (Messen ein)", "V1a: Messband aufziehen – Anfang beim Pinch, Ende beim Loslassen; Variante «zwei Punkte»: je ein kurzer Pinch"),
        ("Offene Hand schnell quer über eine Messlinie", "V1a: löscht diese Messung (wie an der Wandtafel)"),
        ("Pinch auf eine Stelle (Marker setzen)", "V1a: setzt einen Problem-Marker an die Stelle, auf die man zeigt"),
        ("Pinch am Griff unter dem Fenster", "V1a: das Fenster verschieben; der runde Knopf links daneben schliesst es"),
    ];
}
