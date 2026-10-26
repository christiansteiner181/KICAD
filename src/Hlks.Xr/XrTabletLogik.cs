// Modul XR, Teil XR-C (Tablet an der linken Hand): Greif- und Pinch-Erkennung aus Handgelenken (Hand-Tracking), Handfläche zum Kopf, Lage des Tablets über der Hand,
// Treffer auf dem Tablet und das Tablet-Layout. Das Beruhigen des Tablets steht in XrTabletRuhe, die Wischgeste in XrWischLogik.
// Quelle: wwwroot/js/ansichten/xr-greifen-logik.js (Browser-Prototyp, unverändert; Node-Test xr-b.test.mjs). Die Teile für das Greifen selbst (XR-B: Reichweite,
// Querverschiebung in 1-cm-Schritten, Heranholen per Strahl) sind noch nicht übertragen; sie kommen mit XR-B.
//
// Konventionen: Szene in Metern (Y oben). Gelenke: Namen der WebXR-Hand-API ("wrist", "index-finger-tip" …) → Vec3 in der Welt; StereoKit liefert dieselben
// 25 Gelenke und lässt sich auf diese Namen abbilden.
//
// ANNAHMEN (am Brillentest zu bestätigen; alle Schwellen sind benannt):
//   Faust = Mittel der Entfernungen der vier Fingerspitzen zum Handgelenk, geteilt durch die Handgrösse (Handgelenk → Grundgelenk des Mittelfingers, ≈ 0.09 m).
//     Offene Hand ≈ 1.9, Faust ≈ 1.0. Greifen ab < 1.3, Loslassen ab > 1.55 (Hysterese).
//   Pinch = Daumen- und Zeigefingerspitze unter 2.5 cm (los ab 4 cm).
//   Tablet: Hand offen (Verhältnis > 1.65) und die Handflächen-Normale zeigt höchstens 60° neben den Kopf. Schliessen: Faust oder Wegdrehen (> 80°, Hysterese).
//   Berühren: Fingerspitze höchstens 1.5 cm vor der Tablet-Ebene bzw. dahinter (bis 4 cm), im Rechteck; ein Druck zählt beim Eintritt.

namespace Hlks.Xr;

/// <summary>Lage des Tablets im Raum: Mitte, Normale (zum Kopf), rechts und oben in der Ebene, Breite und Höhe in m.</summary>
public sealed record TabletPose(Vec3 Mitte, Vec3 Normale, Vec3 Rechts, Vec3 Oben, double B, double H);

/// <summary>Punkt in Ebenenkoordinaten des Tablets: U, V in 0 … 1 (links oben = 0/0), Tiefe vor der Ebene &gt; 0, Innen = im Rechteck.</summary>
public readonly record struct TabletKoord(double U, double V, double Tiefe, bool Innen);

/// <summary>Treffer eines Strahls auf dem Tablet: U, V und Weg T entlang des Strahls.</summary>
public readonly record struct TabletStrahlTreffer(double U, double V, double T);

/// <summary>Berührung der Fingerspitze: U, V und Tiefe zur Ebene.</summary>
public readonly record struct TabletBeruehrungsTreffer(double U, double V, double Tiefe);

/// <summary>Taste des Tablets in Bildpunkten (Textur 480 × 640, hochkant).</summary>
public sealed record Taste(string Id, string Text, double X, double Y, double W, double H, bool Umschalter = false, bool Platzhalter = false);

/// <summary>Beschreibung einer Taste im Raster (Spalte, Zeile).</summary>
public sealed record TabletTasteDef(string Id, string Text, int Spalte, int Zeile, bool Umschalter = false, bool Platzhalter = false);

/// <summary>Element für den Kopf des Tablets (lesend): Kennzeichen, Klasse, System, Geschoss, Raum und Werte in der Reihenfolge des Kerns.</summary>
public sealed record ElementInfo(string? Kennzeichen, string? Klasse, string? System, string? Geschoss, string? Raum, IReadOnlyList<KeyValuePair<string, object?>>? Werte = null);

public static class XrTabletLogik
{
    public const double FaustAn = 1.3;
    public const double FaustLos = 1.55;
    public const double OffenAb = 1.65;
    public const double PinchAn = 0.025;
    public const double PinchLos = 0.04;
    public const double PalmAnGrad = 60;
    public const double PalmAusGrad = 80;

    static readonly string[] Spitzen = ["index-finger-tip", "middle-finger-tip", "ring-finger-tip", "pinky-finger-tip"];
    static readonly string[] Noetig = ["wrist", "middle-finger-phalanx-proximal", .. Spitzen, "thumb-tip", "index-finger-metacarpal", "pinky-finger-metacarpal"];

    static bool Hat(IReadOnlyDictionary<string, Vec3>? g, string name) => g is not null && g.TryGetValue(name, out var v) && v.IstEndlich;

    /// <summary>xr-greifen-logik.js gelenkeVollstaendig: sind alle Gelenke da, die die Erkennung braucht?</summary>
    public static bool GelenkeVollstaendig(IReadOnlyDictionary<string, Vec3>? g) => g is not null && Noetig.All(n => Hat(g, n));

    /// <summary>xr-greifen-logik.js handGroesse: Handgelenk bis Grundgelenk des Mittelfingers (m).</summary>
    public static double HandGroesse(IReadOnlyDictionary<string, Vec3> g) => g["wrist"].Abstand(g["middle-finger-phalanx-proximal"]);

    /// <summary>xr-greifen-logik.js faustVerhaeltnis: mittlere Entfernung der Fingerspitzen zum Handgelenk, in Handgrössen (offen ≈ 1.9, Faust ≈ 1.0); null, wenn Gelenke fehlen.</summary>
    public static double? FaustVerhaeltnis(IReadOnlyDictionary<string, Vec3>? g)
    {
        if (!GelenkeVollstaendig(g)) return null;
        var h = HandGroesse(g!);
        if (!(h > 1e-4)) return null;
        return Spitzen.Sum(n => g![n].Abstand(g["wrist"])) / Spitzen.Length / h;
    }

    /// <summary>xr-greifen-logik.js istFaust: Faust mit Hysterese; vorher = war schon gegriffen.</summary>
    public static bool IstFaust(IReadOnlyDictionary<string, Vec3>? g, bool vorher = false)
    {
        var v = FaustVerhaeltnis(g);
        if (v is null) return false;
        return vorher ? v < FaustLos : v < FaustAn;
    }

    /// <summary>xr-greifen-logik.js istOffen.</summary>
    public static bool IstOffen(IReadOnlyDictionary<string, Vec3>? g) => FaustVerhaeltnis(g) is { } v && v > OffenAb;

    /// <summary>xr-greifen-logik.js istPinch: Pinch (Daumen- und Zeigefingerspitze) mit Hysterese. P5a: Schwellen aus <paramref name="schwellen"/> (Stellschrauben
    /// «pinch.schwelle_an_m», «pinch.schwelle_aus_m», an einer Stelle geführt), ohne Angabe <see cref="PinchAn"/> / <see cref="PinchLos"/>.</summary>
    public static bool IstPinch(IReadOnlyDictionary<string, Vec3>? g, bool vorher = false, PinchEinstellungen? schwellen = null)
    {
        if (!Hat(g, "thumb-tip") || !Hat(g, "index-finger-tip")) return false;
        var d = g!["thumb-tip"].Abstand(g["index-finger-tip"]);
        return vorher ? d < (schwellen?.AusM ?? PinchLos) : d < (schwellen?.AnM ?? PinchAn);
    }

    /// <summary>xr-greifen-logik.js handflaechenMitte.</summary>
    public static Vec3? HandflaechenMitte(IReadOnlyDictionary<string, Vec3>? g)
        => Hat(g, "wrist") && Hat(g, "middle-finger-phalanx-proximal") ? (g!["wrist"] + g["middle-finger-phalanx-proximal"]) * 0.5 : null;

    /// <summary>xr-greifen-logik.js handflaechenNormale: Normale der Handfläche (zeigt aus der Handfläche heraus); seite "left" oder "right".</summary>
    public static Vec3? HandflaechenNormale(IReadOnlyDictionary<string, Vec3>? g, string seite)
    {
        if (!GelenkeVollstaendig(g)) return null;
        var a = g!["index-finger-metacarpal"] - g["wrist"];
        var b = g["pinky-finger-metacarpal"] - g["wrist"];
        var n = a.Kreuz(b).Einheit();
        return seite == "left" ? n * -1 : n;   // linke Hand: cross(b, a)
    }

    /// <summary>xr-greifen-logik.js palmWinkelZumKopf: Winkel (Grad) zwischen der Handflächen-Normale und der Richtung zum Kopf; null, wenn nicht bestimmbar.</summary>
    public static double? PalmWinkelZumKopf(IReadOnlyDictionary<string, Vec3>? g, string seite, Vec3? kopf)
    {
        var n = HandflaechenNormale(g, seite);
        var m = HandflaechenMitte(g);
        if (n is null || m is null || kopf is null) return null;
        var z = (kopf.Value - m.Value).Einheit();
        return Math.Acos(Math.Min(1, Math.Max(-1, n.Value.Skalar(z)))) / Num.Grad;
    }

    /// <summary>
    /// xr-greifen-logik.js tabletGeste: Tablet-Geste der linken Hand: offene Hand, Handfläche zum Kopf. vorher = Tablet war offen (Hysterese: das Wegdrehen zählt
    /// erst ab <see cref="PalmAusGrad"/>, das Schliessen bei Faust oder Verhältnis unter <see cref="OffenAb"/> − 0.25).
    /// </summary>
    public static bool TabletGeste(IReadOnlyDictionary<string, Vec3>? g, string seite, Vec3? kopf, bool vorher = false)
    {
        var v = FaustVerhaeltnis(g);
        var w = PalmWinkelZumKopf(g, seite, kopf);
        if (v is null || w is null) return false;
        return vorher ? (v > OffenAb - 0.25 && w < PalmAusGrad) : (v > OffenAb && w < PalmAnGrad);
    }

    // ---------------------------------------------------------------------------------------------------- Tablet
    // Christian 21.10.2026 beim Brillentest: «tablett soll auf handdistanz sein. 15" hochformat». 15 Zoll = 381 mm Diagonale, Seitenverhältnis 3:4 → 0.229 × 0.305 m, hochkant.

    /// <summary>xr-greifen-logik.js TABLET_DIAGONALE_M: 15 Zoll.</summary>
    public const double TabletDiagonaleM = 0.381;
    /// <summary>xr-greifen-logik.js TABLET_B: Breite = 3/5 der Diagonale (3-4-5-Dreieck, 3:4 hochkant): 0.2286 m.</summary>
    public static readonly double TabletB = Num.Round(TabletDiagonaleM * 0.6 * 1e4) / 1e4;
    /// <summary>xr-greifen-logik.js TABLET_H: Höhe 0.3048 m.</summary>
    public static readonly double TabletH = Num.Round(TabletDiagonaleM * 0.8 * 1e4) / 1e4;
    /// <summary>xr-greifen-logik.js TABLET_ABSTAND: m über der Handfläche; das Tablet liegt wie in der Hand gehalten.</summary>
    public const double TabletAbstand = 0.025;
    /// <summary>xr-greifen-logik.js TABLET_VERSATZ_OBEN: die Mitte liegt so weit über der Handmitte (in Richtung «oben» des Tablets), damit die Hand den unteren Teil trägt.</summary>
    public const double TabletVersatzOben = 0.08;
    /// <summary>xr-greifen-logik.js TABLET_PX: Breite der Textur in Bildpunkten (3:4 hochkant, 1 Bildpunkt ≈ 0.48 mm).</summary>
    public const double TabletPxB = 480;
    /// <summary>xr-greifen-logik.js TABLET_PX: Höhe der Textur in Bildpunkten.</summary>
    public const double TabletPxH = 640;
    /// <summary>xr-greifen-logik.js BERUEHR_VOR (m).</summary>
    public const double BeruehrVor = 0.015;
    /// <summary>xr-greifen-logik.js BERUEHR_HINTER (m).</summary>
    public const double BeruehrHinter = 0.04;

    /// <summary>xr-greifen-logik.js tabletLageAus: Lage aus Mitte und (Blick-)Normale des Tablets: aufrecht, rechts und oben daraus abgeleitet.</summary>
    public static TabletPose TabletLageAus(Vec3 mitte, Vec3 normale)
    {
        var n = normale.Einheit();
        var hoch = new Vec3(0, 1, 0);
        var rechts = hoch.Kreuz(n);
        if (rechts.Laenge < 1e-6) rechts = new Vec3(1, 0, 0);
        rechts = rechts.Einheit();
        var oben = n.Kreuz(rechts).Einheit();
        return new TabletPose(mitte, n, rechts, oben, TabletB, TabletH);
    }

    /// <summary>
    /// xr-greifen-logik.js tabletLage: Lage des Tablets: <see cref="TabletAbstand"/> über der Handfläche (in Richtung ihrer Normale) und
    /// <see cref="TabletVersatzOben"/> nach oben versetzt (die Hand trägt den unteren Teil), dem Kopf zugewandt, aufrecht.
    /// </summary>
    public static TabletPose TabletLage(Vec3 mitte, Vec3 normale, Vec3 kopf)
    {
        var m = mitte + normale.Einheit() * TabletAbstand;
        var z = TabletLageAus(m, kopf - m);
        return TabletLageAus(m + z.Oben * TabletVersatzOben, z.Normale);
    }

    /// <summary>xr-greifen-logik.js tabletKoordinaten: Punkt (Welt) → Ebenenkoordinaten des Tablets.</summary>
    public static TabletKoord TabletKoordinaten(TabletPose lage, Vec3 p)
    {
        var w = p - lage.Mitte;
        double x = w.Skalar(lage.Rechts), y = w.Skalar(lage.Oben), tiefe = w.Skalar(lage.Normale);
        double u = x / lage.B + 0.5, v = 0.5 - y / lage.H;
        return new TabletKoord(u, v, tiefe, u >= 0 && u <= 1 && v >= 0 && v <= 1);
    }

    /// <summary>xr-greifen-logik.js tabletStrahl: Schnitt eines Strahls mit der Tablet-Ebene; null bei parallel, dahinter oder ausserhalb.</summary>
    public static TabletStrahlTreffer? TabletStrahl(TabletPose lage, Vec3 ursprung, Vec3 richtung)
    {
        var nr = lage.Normale.Skalar(richtung);
        if (Math.Abs(nr) < 1e-6) return null;
        var t = (lage.Mitte - ursprung).Skalar(lage.Normale) / nr;
        if (!(t > 0)) return null;
        var k = TabletKoordinaten(lage, ursprung + richtung * t);
        return k.Innen ? new TabletStrahlTreffer(k.U, k.V, t) : null;
    }

    /// <summary>xr-greifen-logik.js tabletBeruehrung: berührt die Fingerspitze das Tablet? (Eintritt in die Zone von <see cref="BeruehrVor"/> bis <see cref="BeruehrHinter"/>)</summary>
    public static TabletBeruehrungsTreffer? TabletBeruehrung(TabletPose lage, Vec3 spitze)
    {
        var k = TabletKoordinaten(lage, spitze);
        if (!k.Innen) return null;
        return k.Tiefe <= BeruehrVor && k.Tiefe >= -BeruehrHinter ? new TabletBeruehrungsTreffer(k.U, k.V, k.Tiefe) : null;
    }

    // ---------------------------------------------------------------------------------------------------- Tasten

    /// <summary>xr-greifen-logik.js TABLET_TASTEN: Tasten des Tablets im Raster 2 × 6 unter dem Kopf mit den Eigenschaften.</summary>
    public static readonly IReadOnlyList<TabletTasteDef> TabletTasten =
    [
        new("rueckgaengig", "Rückgängig", 0, 0),
        new("wiederholen", "Wiederholen", 1, 0),
        new("messen", "Messen", 0, 1, Umschalter: true),
        new("lot", "Lot zur Fläche", 1, 1, Umschalter: true),
        new("hoeher", "Augenhöhe +", 0, 2),
        new("tiefer", "Augenhöhe −", 1, 2),
        new("messungenLoeschen", "Messungen löschen", 0, 3),
        new("schnitt", "Schnittbereich", 1, 3, Umschalter: true),
        new("ifc", "IFC-Unterlage", 0, 4, Umschalter: true),
        new("raum", "Sitzungsraum", 1, 4),   // XR-H: wechselt in den Sitzungsraum; dort heisst die Taste «Begehen»
        new("familie", "Familie platzieren", 0, 5, Platzhalter: true),
        new("problem", "Problem markieren", 1, 5, Platzhalter: true),
    ];

    /// <summary>xr-greifen-logik.js TABLET_KOPF_H: Bildpunkte für Titel und Eigenschaften (Titel plus bis zu drei Zeilen).</summary>
    public const double TabletKopfH = 118;
    /// <summary>xr-greifen-logik.js TABLET_ENDE: Beschreibung der Taste «VR beenden» (Lage setzt das Layout).</summary>
    public static readonly (string Id, string Text) TabletEnde = ("ende", "VR beenden");
    /// <summary>xr-greifen-logik.js TABLET_SPALTEN: Tastenspalten (hochkant).</summary>
    public const int TabletSpalten = 2;

    /// <summary>xr-greifen-logik.js tabletLayout: Rechtecke aller Tasten in Bildpunkten (zuzüglich der Taste «VR beenden» unten).</summary>
    public static IReadOnlyList<Taste> TabletLayout()
    {
        const double rand = 14, fussH = 44, luecke = 8;
        const int zeilen = 6;
        var raumH = TabletPxH - TabletKopfH - fussH - rand;
        var bw = (TabletPxB - 2 * rand - (TabletSpalten - 1) * luecke) / TabletSpalten;
        var bh = (raumH - (zeilen - 1) * luecke) / zeilen;
        var tasten = TabletTasten.Select(t => new Taste(t.Id, t.Text, rand + t.Spalte * (bw + luecke), TabletKopfH + t.Zeile * (bh + luecke), bw, bh, t.Umschalter, t.Platzhalter)).ToList();
        tasten.Add(new Taste(TabletEnde.Id, TabletEnde.Text, rand, TabletPxH - fussH - rand / 2, TabletPxB - 2 * rand, fussH - 4));
        return tasten;
    }

    static readonly IReadOnlyList<Taste> Layout = TabletLayout();

    /// <summary>xr-greifen-logik.js tasteBei: Taste an der Stelle (u, v) in 0 … 1; Platzhalter zählen als Treffer (sie tun nichts). Ohne Layout das des Tablets im Begehen.</summary>
    public static Taste? TasteBei(double u, double v, IReadOnlyList<Taste>? layout = null)
    {
        double x = u * TabletPxB, y = v * TabletPxH;
        return (layout ?? Layout).FirstOrDefault(t => x >= t.X && x <= t.X + t.W && y >= t.Y && y <= t.Y + t.H);
    }

    /// <summary>xr-greifen-logik.js eigenschaftenZeilen: Zeilen für den Kopf des Tablets aus dem Element (lesend): Kennzeichen, Klasse, System, Geschoss + wichtige Werte.</summary>
    public static IReadOnlyList<string> EigenschaftenZeilen(ElementInfo? el, int max = 3)
    {
        if (el is null) return ["Kein Element gewählt"];
        var z = new List<string> { $"{el.Kennzeichen ?? ""} · {el.Klasse ?? ""}".Trim() };
        var teil = new[]
        {
            !string.IsNullOrEmpty(el.System) ? $"System {el.System}" : null,
            !string.IsNullOrEmpty(el.Geschoss) ? $"Geschoss {el.Geschoss}" : null,
            !string.IsNullOrEmpty(el.Raum) ? $"Raum {el.Raum}" : null,
        }.Where(t => t is not null).ToList();
        if (teil.Count > 0) z.Add(string.Join(" · ", teil));
        var wichtig = (el.Werte ?? [])
            .Where(kv => (kv.Value is string s && s != "" || kv.Value is bool || IstZahl(kv.Value)) && !System.Text.RegularExpressions.Regex.IsMatch(kv.Key, "^(definition|geometrie|netz)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Take(6)
            .Select(kv => $"{kv.Key} {WertText(kv.Value)}").ToList();
        if (wichtig.Count > 0) z.Add(string.Join(" · ", wichtig.Take(3)));
        return z.Take(max).ToList();
    }

    static bool IstZahl(object? v) => v is double or float or int or long or short or byte or decimal;

    static string WertText(object? v) => v switch
    {
        bool b => b ? "true" : "false",
        string s => s,
        null => "",
        _ => Num.Text(Num.Round(Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) * 100) / 100),
    };

    // ---------------------------------------------------------------------------------------------------- Aufbau und Leuchten

    /// <summary>xr-greifen-logik.js AUFBAU_DAUER: Sekunden, in denen das Tablet beim Öffnen von der Hand aus wächst.</summary>
    public const double AufbauDauer = 0.35;

    /// <summary>xr-greifen-logik.js aufbauAnteil: Grösse, auf die das Tablet beim Öffnen wächst (0 … 1 nach t Sekunden; mit leichtem Überschwingen).</summary>
    public static double AufbauAnteil(double t)
    {
        var x = Math.Min(1, Math.Max(0, t / AufbauDauer));
        var e = 1 - Math.Pow(1 - x, 3);
        return x >= 1 ? 1 : e * (1 + 0.08 * Math.Sin(x * Math.PI));
    }

    /// <summary>xr-greifen-logik.js AUFLEUCHT_DAUER: Sekunden, in denen eine Taste nach dem Antippen von 1 auf 0 abklingt.</summary>
    public const double AufleuchtDauer = 0.35;

    /// <summary>xr-greifen-logik.js aufleuchten: Aufleuchten einer Taste nach dem Antippen: 1 → 0 über <see cref="AufleuchtDauer"/>.</summary>
    public static double Aufleuchten(double t) => Math.Max(0, 1 - t / AufleuchtDauer);
}
