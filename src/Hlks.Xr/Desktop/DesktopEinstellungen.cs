// Quest App V1c (Modul XR, XR-I «Desktop in der Brille»): Stellschrauben der Geste und des virtuellen Bildschirms. Rein, ohne StereoKit.
//
// Christian, 24.10.2026: «ein wisch mit der rechten hand mit der handfläche nach links gerichtet, zieht den verbundenen Desktop in das bild. vr bleibt starr im
// hintergrund.» – Berichtigung 24.10.2026: «bei der handgeste ist die wischbewegung natürlich von links nach rechts. heraus schieben von rechts nach links.» –
// Ergänzung 24.10.2026: «wir vereinheitlichen die wegwisch geste. auch da nutzen wir die wischbewegung von rechts nach links» (gemeinsame Geste der App, V1a).
//
// Schlüssel (Katalog kataloge/xr_stellschrauben.json, Gruppen «gesten» und «desktop»):
//   gesten.hineinziehen.weg_m | .tempo_ms | .handflaeche_grad   Hineinziehen (rechte Hand offen, Handfläche nach links, Wisch links → rechts)
//   gesten.wegwischen.weg_m   | .tempo_ms | .handflaeche_grad   Wegwischen (dieselbe Haltung, Wisch rechts → links; gemeinsame Geste der App, Namen wie V1a)
//   desktop.breite_m, desktop.abstand_m, desktop.radius_m, desktop.bildrate                      Bildschirm und Übertragung
// Alle Startwerte sind ANNAHMEN V1c (keine Norm betroffen): Weg 0.20 m (etwas mehr als der Wisch 0.15 m, «deutlich»), Tempo 0.45 m/s im Mittel, Handfläche 45°
// neben «nach links»; Bildschirm 1.4 m breit in 1.1 m Abstand (Auftrag V1c), Bogen mit 2.0 m Radius («leicht gebogen»), 12 Bilder/s (Ziel 10–15).
// Weitere Werte (Zeitfenster, andere Hand, Sperre, Höhe, grösste Breite, Mipmaps, Trennen, Hochladen) sind benannte Konstanten bzw. Felder ohne Stellschraube:
// Der Katalog führt höchstens 56 Stellschrauben (XrLaborKatalog.cs, seit V1c; vorher 48), und der Kanal merkt höchstens 64 Werte mit Varianten und Aufgabe.

using System.Text.Json;

namespace Hlks.Xr.Desktop;

/// <summary>Schwellen eines Seitenwischs mit der rechten Hand (Handfläche nach links): Weg, Tempo, Lage der Handfläche; dazu feste Werte.</summary>
public sealed record SeitenWischEinstellungen
{
    /// <summary>So weit muss die Hand in Wischrichtung gehen (m, Sehne vom tiefsten Punkt im Zeitfenster). Annahme V1c 0.20.</summary>
    public double WegM { get; init; } = 0.20;
    /// <summary>Mittleres Tempo vom Beginn der Bewegung bis jetzt (m/s). Annahme V1c 0.45 (ein langsames Ausholen löst nicht aus).</summary>
    public double TempoMs { get; init; } = 0.45;
    /// <summary>Die Normale der Handfläche liegt höchstens so weit neben waagrecht nach links (Grad). Annahme V1c 45° (Röntgen-Hand: Handfläche nach vorn, 50°).</summary>
    public double HandflaecheGrad { get; init; } = 45;
    /// <summary>Die ganze Bewegung höchstens so lange (s): älteres zählt nicht. Annahme V1c 0.7 s.</summary>
    public double ZeitS { get; init; } = 0.7;
    /// <summary>Die andere (linke) Hand bewegt sich währenddessen höchstens so weit (m) – sonst ist es eine Geste mit zwei Händen. Annahme V1c 0.08 m.</summary>
    public double AndereHandM { get; init; } = 0.08;
    /// <summary>Nach einer Auslösung zählt keine der beiden Gesten so lange (s): das Zurückholen der Hand löst die Gegengeste nicht aus. Annahme V1c 1.0 s.</summary>
    public double SperreS { get; init; } = 1.0;

    public static readonly SeitenWischEinstellungen Vorgabe = new();
}

/// <summary>Stellschrauben von XR-I (siehe Dateikopf): die beiden Gesten, der Bildschirm im Raum und die Übertragung.</summary>
public sealed record DesktopEinstellungen
{
    public const string HineinVorsatz = "gesten.hineinziehen.", WegVorsatz = "gesten.wegwischen.";

    /// <summary>Hineinziehen: Wisch von links nach rechts.</summary>
    public SeitenWischEinstellungen Hinein { get; init; } = SeitenWischEinstellungen.Vorgabe;
    /// <summary>Wegwischen: Wisch von rechts nach links (gemeinsame Geste der App; Namen gleich wie V1a).</summary>
    public SeitenWischEinstellungen Weg { get; init; } = SeitenWischEinstellungen.Vorgabe;

    /// <summary>«desktop.breite_m»: Breite des Bildschirms (Bogenlänge, m). Auftrag V1c: etwa 1.4 m.</summary>
    public double BreiteM { get; init; } = 1.4;
    /// <summary>«desktop.abstand_m»: Mitte des Bildschirms so weit vor den Augen (waagrecht, m). Auftrag V1c: etwa 1.1 m.</summary>
    public double AbstandM { get; init; } = 1.1;
    /// <summary>«desktop.radius_m»: Radius des Bogens (m); grösser = flacher. Annahme V1c 2.0 m («leicht gebogen»: der Mittelpunkt liegt hinter dem Kopf).</summary>
    public double RadiusM { get; init; } = 2.0;
    /// <summary>«desktop.bildrate»: Ziel der Bilder je Sekunde (der PC sendet nur Geänderte und höchstens so oft). Auftrag V1c: 10–15, Annahme 12.</summary>
    public double Bildrate { get; init; } = 12;

    /// <summary>Mitte des Bildschirms gegenüber der Augenhöhe (m, negativ = tiefer). Annahme V1c −0.10 (leicht unter dem Blick, bequem sitzend und stehend).</summary>
    public double HoeheM { get; init; } = -0.10;
    /// <summary>Breitere Bildschirme verkleinert der PC auf so viele Bildpunkte (scharf ohne Mipmaps; 4K wäre auf 1.4 m mehr als zweifach verkleinert). Annahme V1c 1920.</summary>
    public int MaxBreitePx { get; init; } = 1920;
    /// <summary>Mipmaps der Texturen. Annahme V1c: aus – auf 1.4 m in 1.1 m wird ein 1920 breites Bild nur etwa 1.2-fach verkleinert; Mipmaps machten Schrift weicher.</summary>
    public bool Mipmaps { get; init; }
    /// <summary>So lange nach dem Ausblenden bleibt die Verbindung pausiert, dann trennt die App (s). Annahme V1c 60 s.</summary>
    public double TrennenNachS { get; init; } = 60;
    /// <summary>Höchstens so viele Felder (Texturen, je 1 MB) lädt die App je Bild hoch (Bildzeit). Annahme V1c 2 (im Simulator etwa 3–4 ms je Feld).</summary>
    public int HochladenJeBild { get; init; } = 2;

    public static readonly DesktopEinstellungen Vorgabe = new();

    /// <summary>Alle Schlüssel (für LaborWerte und den Katalog).</summary>
    public static readonly IReadOnlyList<string> Schluessel =
    [
        HineinVorsatz + "weg_m", HineinVorsatz + "tempo_ms", HineinVorsatz + "handflaeche_grad",
        WegVorsatz + "weg_m", WegVorsatz + "tempo_ms", WegVorsatz + "handflaeche_grad",
        "desktop.breite_m", "desktop.abstand_m", "desktop.radius_m", "desktop.bildrate",
    ];

    /// <summary>Grenzen im Code (min, max); der Katalog führt dieselben (Test in Hlks.Web.Tests).</summary>
    public static readonly IReadOnlyDictionary<string, (double Min, double Max)> Grenzen = new Dictionary<string, (double, double)>(StringComparer.Ordinal)
    {
        [HineinVorsatz + "weg_m"] = (0.05, 0.6), [HineinVorsatz + "tempo_ms"] = (0.1, 3), [HineinVorsatz + "handflaeche_grad"] = (10, 80),
        [WegVorsatz + "weg_m"] = (0.05, 0.6), [WegVorsatz + "tempo_ms"] = (0.1, 3), [WegVorsatz + "handflaeche_grad"] = (10, 80),
        ["desktop.breite_m"] = (0.5, 4), ["desktop.abstand_m"] = (0.4, 4), ["desktop.radius_m"] = (0.8, 20), ["desktop.bildrate"] = (1, 30),
    };

    public static bool Kennt(string schluessel) => Grenzen.ContainsKey(schluessel);

    /// <summary>
    /// Stellschraube vom PC (wie HaltungEinstellungen.MitStellschraube): neue Fassung oder null, wenn der Schlüssel nicht hierher gehört bzw. der Wert ungültig ist.
    /// Wert null setzt zurück. grenzen = min/max aus dem Katalog des Labors (gelten vor den Grenzen hier).
    /// </summary>
    public DesktopEinstellungen? MitStellschraube(string schluessel, JsonElement wert, IReadOnlyDictionary<string, (double Min, double Max)>? grenzen = null)
    {
        if (!Grenzen.TryGetValue(schluessel, out var g)) return null;
        if (grenzen is not null && grenzen.TryGetValue(schluessel, out var k)) g = k;
        var zurueck = wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        double z;
        if (zurueck) z = Lesen(Vorgabe, schluessel);
        else if (wert.ValueKind == JsonValueKind.Number && wert.TryGetDouble(out var d) && double.IsFinite(d) && d >= g.Min && d <= g.Max) z = d;
        else return null;
        return Mit(schluessel, z);
    }

    /// <summary>Wert eines Schlüssels in dieser Fassung.</summary>
    public static double Lesen(DesktopEinstellungen e, string schluessel) => schluessel switch
    {
        HineinVorsatz + "weg_m" => e.Hinein.WegM,
        HineinVorsatz + "tempo_ms" => e.Hinein.TempoMs,
        HineinVorsatz + "handflaeche_grad" => e.Hinein.HandflaecheGrad,
        WegVorsatz + "weg_m" => e.Weg.WegM,
        WegVorsatz + "tempo_ms" => e.Weg.TempoMs,
        WegVorsatz + "handflaeche_grad" => e.Weg.HandflaecheGrad,
        "desktop.breite_m" => e.BreiteM,
        "desktop.abstand_m" => e.AbstandM,
        "desktop.radius_m" => e.RadiusM,
        "desktop.bildrate" => e.Bildrate,
        _ => throw new ArgumentException($"Unbekannte Stellschraube «{schluessel}».", nameof(schluessel)),
    };

    DesktopEinstellungen Mit(string schluessel, double z) => schluessel switch
    {
        HineinVorsatz + "weg_m" => this with { Hinein = Hinein with { WegM = z } },
        HineinVorsatz + "tempo_ms" => this with { Hinein = Hinein with { TempoMs = z } },
        HineinVorsatz + "handflaeche_grad" => this with { Hinein = Hinein with { HandflaecheGrad = z } },
        WegVorsatz + "weg_m" => this with { Weg = Weg with { WegM = z } },
        WegVorsatz + "tempo_ms" => this with { Weg = Weg with { TempoMs = z } },
        WegVorsatz + "handflaeche_grad" => this with { Weg = Weg with { HandflaecheGrad = z } },
        "desktop.breite_m" => this with { BreiteM = z },
        "desktop.abstand_m" => this with { AbstandM = z },
        "desktop.radius_m" => this with { RadiusM = z },
        _ => this with { Bildrate = z },
    };
}
