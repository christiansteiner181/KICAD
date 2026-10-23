// Standort und Zustand des Begehens. Quelle: wwwroot/js/ansichten/begehen-logik.js (nur, was vr-logik.js, vr-start-logik.js und die Tests brauchen:
// Konstanten, zustandAusStandort, einblendung). Das Begehen am Bildschirm (Tasten, Maus) gehört nicht zur Quest App und fehlt hier bewusst.
//
// Konventionen: Y oben, Meter, Blick -Z; Szene x = Ost, z = -Nord; Standort in Projektmillimetern (x Ost, y Nord, z oben); Gieren gegen den
// Uhrzeigersinn von oben ab +x, im Standort als Azimut in Grad (90 Grad = Projekt-Nord = -z).

namespace Hlks.Xr;

/// <summary>Standpunkt in Projektmillimetern (x Ost, y Nord, z oben). Teil der Antwort der Abfrage raum_begehen.</summary>
public sealed record Standpunkt(double X, double Y, double? Z = null, string? Art = null);

/// <summary>Blickrichtung des Standorts: Azimut in Grad (ab Projekt-x gegen den Uhrzeigersinn) und Nicken über der Waagrechten.</summary>
public sealed record Blick(double AzimutGrad, double NickenGrad = 0);

/// <summary>
/// Standort im Format der Abfrage raum_begehen (Kern): Raum, Geschoss, Standpunkt, Boden, Blick. Alle Texte dürfen fehlen.
/// </summary>
public sealed record Standort
{
    public string? Raum { get; init; }
    public string? Name { get; init; }
    public string? Nummer { get; init; }
    public string? Geschoss { get; init; }
    public string? GeschossName { get; init; }
    public Standpunkt? Standpunkt { get; init; }
    /// <summary>OK Boden in mm.</summary>
    public double? BodenOk { get; init; }
    /// <summary>Augenhöhe in mm (Vorschlag des Kerns); fehlt sie, gilt <see cref="Begehen.AugenhoeheVorgabe"/>.</summary>
    public double? Augenhoehe { get; init; }
    public Blick? Blick { get; init; }
}

/// <summary>
/// Anfangszustand des Begehens (begehen-logik.js zustandAusStandort): x, z in Szenenmetern, Boden = OK Boden in m, Augen = Augenhöhe über Boden in m,
/// Gieren und Nicken im Bogenmass.
/// </summary>
public sealed record BegehenZustand(double X, double Z, double Boden, double Augen, double Gieren, double Nicken);

/// <summary>Zeilen der Einblendung (begehen-logik.js einblendung).</summary>
public sealed record EinblendungZeilen(string Titel, string Geschoss, string Augenhoehe);

public static class Begehen
{
    /// <summary>begehen-logik.js AUGENHOEHE_VORGABE: Augenhöhe über OK Boden in mm, wenn nichts eingestellt ist.</summary>
    public const double AugenhoeheVorgabe = 1700;
    /// <summary>begehen-logik.js AUGENHOEHE_MIN.</summary>
    public const double AugenhoeheMin = 300;
    /// <summary>begehen-logik.js AUGENHOEHE_MAX.</summary>
    public const double AugenhoeheMax = 5000;
    /// <summary>begehen-logik.js GESCHWINDIGKEIT: Gehen in m/s.</summary>
    public const double Geschwindigkeit = 1.4;
    /// <summary>begehen-logik.js SCHNELL: Faktor bei gedrücktem Stick.</summary>
    public const double Schnell = 3;
    /// <summary>begehen-logik.js NICKEN_MAX_GRAD.</summary>
    public const double NickenMaxGrad = 80;

    /// <summary>
    /// begehen-logik.js zustandAusStandort. Null, wenn Standpunkt, Boden oder Blick fehlen oder keine endlichen Zahlen sind.
    /// augenhoeheMm hat Vorrang vor der Augenhöhe des Standorts; beide werden auf 300 … 5000 mm begrenzt.
    /// </summary>
    public static BegehenZustand? ZustandAusStandort(Standort? standort, double? augenhoeheMm = null)
    {
        var s = standort?.Standpunkt;
        var b = standort?.Blick;
        if (s is null || !Num.Ok(s.X) || !Num.Ok(s.Y) || !Num.Ok(standort!.BodenOk) || b is null || !Num.Ok(b.AzimutGrad)) return null;
        var augenMm = Num.Ok(augenhoeheMm) ? augenhoeheMm!.Value : Num.Ok(standort.Augenhoehe) ? standort.Augenhoehe!.Value : AugenhoeheVorgabe;
        return new BegehenZustand(
            X: s.X / 1000, Z: -s.Y / 1000,
            Boden: standort.BodenOk!.Value / 1000,
            Augen: Num.Klemme(augenMm, AugenhoeheMin, AugenhoeheMax) / 1000,
            Gieren: b.AzimutGrad * Num.Grad,
            Nicken: Num.Klemme(Num.Ok(b.NickenGrad) ? b.NickenGrad : 0, -NickenMaxGrad, NickenMaxGrad) * Num.Grad);
    }

    /// <summary>begehen-logik.js einblendung: Titel (Kennzeichen, Nummer, Name), Geschoss und Augenhöhe als Zeilen.</summary>
    public static EinblendungZeilen Einblendung(Standort? standort, BegehenZustand z)
    {
        var kz = standort?.Raum ?? "";
        var name = !string.IsNullOrEmpty(standort?.Name) && standort!.Name != kz ? standort.Name : "";
        var nummer = !string.IsNullOrEmpty(standort?.Nummer) && standort!.Nummer != kz ? standort.Nummer : "";
        var geschoss = !string.IsNullOrEmpty(standort?.GeschossName) ? $"{standort!.Geschoss ?? ""} – {standort.GeschossName}" : standort?.Geschoss ?? "";
        return new EinblendungZeilen(
            Titel: string.Join(" · ", new[] { kz, nummer, name }.Where(t => !string.IsNullOrEmpty(t))),
            Geschoss: geschoss != "" ? $"Geschoss {geschoss}" : "",
            Augenhoehe: $"Augenhöhe {Num.ToFixed(z.Augen, 2)} m über OK Boden");
    }
}
