// Quest App P5a (Modul XR, Interface-Labor): die Stellschrauben der Bedienung an einer Stelle – sie kommen live vom PC (Kanal «stellschraube», Seite «XR-Labor»
// in KICAD, P6) und beim Verbinden mit «willkommen» (die gemerkten Werte). Schlüssel genau wie im Katalog kataloge/xr_stellschrauben.json (P6):
//   anzeige.skala, apps.groesse_m, apps.abstand_m, apps.anordnung, wisch.mindestweg_m, wisch.mindesttempo_ms, wisch.richtung_toleranz_grad,
//   pinch.schwelle_an_m, pinch.schwelle_aus_m, fang.radius_m, komfort.tunnelblick, komfort.tunnelblick_staerke, komfort.ton, begehen.drehwinkel_grad,
//   sitzung.zoom_min, sitzung.zoom_max, labor.variante.bedienung (apps | tablet), labor.variante.etage (wisch_modell | app).
// P5b: roentgen.winkel_grad, roentgen.reichweite_m (Röntgen-Hand), sitzung.schnitt_ruhe_s, sitzung.schnitt_toleranz_grad (Schnitt mit flacher Hand),
//   labor.variante.teleport (zeigen_pinch | stick), labor.variante.messen (messband | zwei_punkte; Messen folgt mit V1, die App merkt die Wahl nur).
//   Hat die App den Katalog, gelten seine Grenzen (Parameter «grenzen» von MitStellschraube, Hlks.Xr.Labor.LaborWerte); sonst die Grenzen hier (gleich wie im
//   Katalog; tests/Hlks.Web.Tests/XrLaborTests.cs vergleicht beide).
// Die Schlüssel begehen.ziel_* / begehen.drehen_sperre_s und haltung.* führen HandBegehenEinstellungen und HaltungEinstellungen (P4a).
// Wert null setzt auf die Vorgabe zurück. Ein Wert ausserhalb der Grenzen, vom falschen Typ oder einer, der eine Paarbedingung verletzt (Pinch an < aus,
// Zoom min < max), wird abgewiesen (MitStellschraube gibt null zurück, die App meldet «ungültig»).
// Keine Normwerte: alles sind Einstellungen der Bedienung; Vorgaben aus dem Prototyp, aus P4a/P4b bzw. Annahmen P5a, die Christian im Brillentest festlegt.
// Grenzen wie im Katalog von P6 (min/max), damit die Seite «XR-Labor» und die App dasselbe annehmen.

using System.Text.Json;
using Hlks.Xr.Modell;
using Hlks.Xr.Sitzung;

namespace Hlks.Xr.Bedienung;

/// <summary>Anordnung der schwebenden Apps (Stellschraube «apps.anordnung»).</summary>
public static class Anordnungen
{
    /// <summary>Im Bogen um den Körper auf Brusthöhe (Vorgabe).</summary>
    public const string Bogen = "bogen";
    /// <summary>In einer geraden Reihe vor dem Körper.</summary>
    public const string Reihe = "reihe";
    /// <summary>Klein über der linken Handfläche, solange sie zum Gesicht zeigt (wie das Tablet des Prototyps).</summary>
    public const string AnHand = "an_hand";

    public static readonly IReadOnlyList<string> Alle = [Bogen, Reihe, AnHand];
}

/// <summary>Varianten des Labors (Stellschrauben «labor.variante.&lt;name&gt;», Katalog kataloge/xr_varianten.json).</summary>
public static class Varianten
{
    /// <summary>«labor.variante.bedienung»: schwebende Apps (Vorgabe) bzw. das Tablet – in der App die Box vor dem Blick (P4).</summary>
    public const string Apps = "apps", Tablet = "tablet";
    /// <summary>«labor.variante.etage»: Etage per Wisch hoch/runter (Vorgabe) bzw. nur in der App «Etagen».</summary>
    public const string EtageWisch = "wisch_modell", EtageApp = "app";
    /// <summary>P5b, «labor.variante.teleport»: springen mit Zeigen und Pinch (Vorgabe) bzw. mit dem Stick eines Controllers.</summary>
    public const string ZeigenPinch = "zeigen_pinch", Stick = "stick";
    /// <summary>P5b, «labor.variante.messen»: Messband aufziehen (Vorgabe) bzw. zwei Punkte setzen – Messen folgt mit Version 1, die App merkt die Wahl nur.</summary>
    public const string Messband = "messband", ZweiPunkte = "zwei_punkte";

    /// <summary>Die Varianten, die die App kennt: Name → Optionen (die erste ist die Vorgabe), wie kataloge/xr_varianten.json.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bekannt = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["bedienung"] = [Apps, Tablet], ["teleport"] = [ZeigenPinch, Stick], ["etage"] = [EtageWisch, EtageApp], ["messen"] = [Messband, ZweiPunkte],
    };
}

/// <summary>Grösse, Abstand und Anordnung der schwebenden Apps und die Grösse der Schrift.</summary>
public sealed record AppEinstellungen
{
    /// <summary>«anzeige.skala»: Faktor für Box und Schrift (Brillentest 23.10.2026: «text und box eher etwas klein» → 1.35, «Ja, passt»).</summary>
    public double Skala { get; init; } = 1.35;
    /// <summary>«apps.groesse_m»: Kantenlänge einer Kachel (m). Annahme P6/P5a 0.18 m – grosse Ziele (Leitbild Christian 23.10.2026).</summary>
    public double GroesseM { get; init; } = 0.18;
    /// <summary>«apps.abstand_m»: Abstand der Kacheln von der Brust (m), im Griffbereich. Annahme P6/P5a 0.45 m.</summary>
    public double AbstandM { get; init; } = 0.45;
    /// <summary>«apps.anordnung»: <see cref="Anordnungen"/>.</summary>
    public string Anordnung { get; init; } = Anordnungen.Bogen;
}

/// <summary>Komfort: Tunnelblick beim Springen und Drehen, Ton an/aus.</summary>
public sealed record KomfortEinstellungen
{
    /// <summary>«komfort.tunnelblick»: Blickfeld beim Springen, Drehen und Etagenwechsel kurz einengen (Ergänzung Christian 23.10.2026; Vorgabe ein, Annahme P6).</summary>
    public bool Tunnelblick { get; init; } = true;
    /// <summary>«komfort.tunnelblick_staerke»: 0 = kein Einengen, 1 = stark (Vorgabe 0.3, Annahme P6).</summary>
    public double Staerke { get; init; } = 0.3;
    /// <summary>«komfort.ton»: Ton bei Fang, Griff und Berührung (das Licht bleibt).</summary>
    public bool Ton { get; init; } = true;
}

/// <summary>Alle Stellschrauben der Bedienung (P5a) mit Vorgaben und Grenzen; unveränderlich, jede Änderung gibt einen neuen Satz.</summary>
public sealed record XrBedienEinstellungen
{
    public AppEinstellungen Apps { get; init; } = new();
    public WischEinstellungen Wisch { get; init; } = WischEinstellungen.Vorgabe;
    public PinchEinstellungen Pinch { get; init; } = PinchEinstellungen.Vorgabe;
    public KomfortEinstellungen Komfort { get; init; } = new();
    /// <summary>«sitzung.zoom_min» / «sitzung.zoom_max» (Nenner).</summary>
    public ZoomGrenzen Zoom { get; init; } = ZoomGrenzen.Vorgabe;
    /// <summary>«begehen.drehwinkel_grad»: Winkel je Drehschritt (Vorgabe VrLogik.SnapGrad 30°, Brillentest 23.10.2026 «Das neue Drehen passt»).</summary>
    public double DrehwinkelGrad { get; init; } = VrLogik.SnapGrad;
    /// <summary>«labor.variante.bedienung»: <see cref="Varianten.Apps"/> (Vorgabe) oder <see cref="Varianten.Tablet"/>.</summary>
    public string Bedienung { get; init; } = Varianten.Apps;
    /// <summary>«labor.variante.etage»: <see cref="Varianten.EtageWisch"/> (Vorgabe) oder <see cref="Varianten.EtageApp"/>.</summary>
    public string Etage { get; init; } = Varianten.EtageWisch;
    /// <summary>P5b, «labor.variante.teleport»: <see cref="Varianten.ZeigenPinch"/> (Vorgabe) oder <see cref="Varianten.Stick"/>.</summary>
    public string Teleport { get; init; } = Varianten.ZeigenPinch;
    /// <summary>P5b, «labor.variante.messen»: <see cref="Varianten.Messband"/> (Vorgabe) oder <see cref="Varianten.ZweiPunkte"/> (folgt V1, ohne Wirkung).</summary>
    public string Messen { get; init; } = Varianten.Messband;
    /// <summary>P5b: Röntgen-Hand beim Begehen («roentgen.*»).</summary>
    public RoentgenEinstellungen Roentgen { get; init; } = RoentgenEinstellungen.Vorgabe;
    /// <summary>P5b: Schnitt mit der flachen Hand am Sitzungstisch («sitzung.schnitt_*»).</summary>
    public FlacheHandEinstellungen FlacheHand { get; init; } = FlacheHandEinstellungen.Vorgabe;

    public static readonly XrBedienEinstellungen Vorgabe = new();

    /// <summary>Etagenwechsel per Wisch hoch/runter erlaubt (Variante «etage»).</summary>
    public bool EtageMitWisch => Etage != Varianten.EtageApp;
    /// <summary>Schwebende Apps statt der Box (Variante «bedienung»).</summary>
    public bool MitApps => Bedienung != Varianten.Tablet;
    /// <summary>P5b: springen mit dem Stick statt mit Zeigen und Pinch (Variante «teleport»).</summary>
    public bool MitStick => Teleport == Varianten.Stick;

    /// <summary>P5b: Wert einer Variante nach ihrem Namen (bedienung, teleport, etage, messen); null für eine Variante, die die App nicht kennt.</summary>
    public string? Variante(string name) => name switch
    {
        "bedienung" => Bedienung, "teleport" => Teleport, "etage" => Etage, "messen" => Messen, _ => null,
    };

    /// <summary>Grenzen der Zahlen (min, max) je Schlüssel – wie im Katalog von P6.</summary>
    public static readonly IReadOnlyDictionary<string, (double Min, double Max)> Grenzen = new Dictionary<string, (double, double)>(StringComparer.Ordinal)
    {
        ["anzeige.skala"] = (0.5, 3),
        ["apps.groesse_m"] = (0.08, 0.4),
        ["apps.abstand_m"] = (0.25, 0.9),
        ["wisch.mindestweg_m"] = (0.05, 0.5),
        ["wisch.mindesttempo_ms"] = (0.2, 3),
        ["wisch.richtung_toleranz_grad"] = (5, 80),
        ["pinch.schwelle_an_m"] = (0.01, 0.06),
        ["pinch.schwelle_aus_m"] = (0.015, 0.08),
        ["fang.radius_m"] = (0.005, 0.15),
        ["komfort.tunnelblick_staerke"] = (0, 1),
        ["begehen.drehwinkel_grad"] = (5, 90),
        ["sitzung.zoom_min"] = (5, 100),
        ["sitzung.zoom_max"] = (50, 1000),
        ["roentgen.winkel_grad"] = (10, 120),
        ["roentgen.reichweite_m"] = (1, 20),
        ["sitzung.schnitt_ruhe_s"] = (0, 2),
        ["sitzung.schnitt_toleranz_grad"] = (5, 45),
    };

    /// <summary>Alle Schlüssel, die dieser Satz versteht.</summary>
    public static readonly IReadOnlyList<string> Schluessel =
    [
        "anzeige.skala", "apps.groesse_m", "apps.abstand_m", "apps.anordnung",
        "wisch.mindestweg_m", "wisch.mindesttempo_ms", "wisch.richtung_toleranz_grad",
        "pinch.schwelle_an_m", "pinch.schwelle_aus_m", "fang.radius_m",
        "komfort.tunnelblick", "komfort.tunnelblick_staerke", "komfort.ton",
        "begehen.drehwinkel_grad", "sitzung.zoom_min", "sitzung.zoom_max",
        "labor.variante.bedienung", "labor.variante.etage",
        "roentgen.winkel_grad", "roentgen.reichweite_m", "sitzung.schnitt_ruhe_s", "sitzung.schnitt_toleranz_grad",
        "labor.variante.teleport", "labor.variante.messen",
    ];

    /// <summary>Gehört der Schlüssel zu diesem Satz?</summary>
    public static bool Kennt(string schluessel) => Schluessel.Contains(schluessel, StringComparer.Ordinal);

    /// <summary>
    /// Übernimmt eine Stellschraube vom PC. Gibt den neuen Satz zurück, oder null, wenn der Schlüssel nicht hierher gehört (siehe <see cref="Kennt"/>) bzw. der Wert
    /// ungültig ist (Typ, Grenzen, Paarbedingung). Wert null (bzw. fehlend) setzt auf die Vorgabe zurück – bei Paaren nur, wenn die Bedingung danach gilt.
    /// P5b: grenzen = min/max je Schlüssel aus dem Katalog; sie gelten vor <see cref="Grenzen"/> (Schlüssel, die dort fehlen, nach <see cref="Grenzen"/>).
    /// </summary>
    public XrBedienEinstellungen? MitStellschraube(string schluessel, JsonElement wert, IReadOnlyDictionary<string, (double Min, double Max)>? grenzen = null)
    {
        var zurueck = wert.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        double? Zahl()
        {
            if (wert.ValueKind != JsonValueKind.Number || !wert.TryGetDouble(out var d) || !double.IsFinite(d)) return null;
            var (min, max) = grenzen is not null && grenzen.TryGetValue(schluessel, out var g) ? g : Grenzen[schluessel];
            return d >= min && d <= max ? d : null;
        }
        bool? JaNein() => wert.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
        string? Wahl(params string[] erlaubt) =>
            wert.ValueKind == JsonValueKind.String && wert.GetString()?.Trim().ToLowerInvariant() is { } s && erlaubt.Contains(s) ? s : null;
        var v = Vorgabe;
        XrBedienEinstellungen? MitPinch(PinchEinstellungen p) => p.AnM < p.AusM ? this with { Pinch = p } : null;
        XrBedienEinstellungen? MitZoom(ZoomGrenzen z) => z.Min < z.Max ? this with { Zoom = z } : null;
        switch (schluessel)
        {
            case "anzeige.skala": return zurueck ? this with { Apps = Apps with { Skala = v.Apps.Skala } } : Zahl() is { } a ? this with { Apps = Apps with { Skala = a } } : null;
            case "apps.groesse_m": return zurueck ? this with { Apps = Apps with { GroesseM = v.Apps.GroesseM } } : Zahl() is { } b ? this with { Apps = Apps with { GroesseM = b } } : null;
            case "apps.abstand_m": return zurueck ? this with { Apps = Apps with { AbstandM = v.Apps.AbstandM } } : Zahl() is { } c ? this with { Apps = Apps with { AbstandM = c } } : null;
            case "apps.anordnung":
                return zurueck ? this with { Apps = Apps with { Anordnung = v.Apps.Anordnung } }
                    : Wahl([.. Anordnungen.Alle]) is { } an ? this with { Apps = Apps with { Anordnung = an } } : null;
            case "wisch.mindestweg_m": return zurueck ? this with { Wisch = Wisch with { MindestwegM = v.Wisch.MindestwegM } } : Zahl() is { } d ? this with { Wisch = Wisch with { MindestwegM = d } } : null;
            case "wisch.mindesttempo_ms": return zurueck ? this with { Wisch = Wisch with { MindesttempoMs = v.Wisch.MindesttempoMs } } : Zahl() is { } e ? this with { Wisch = Wisch with { MindesttempoMs = e } } : null;
            case "wisch.richtung_toleranz_grad":
                return zurueck ? this with { Wisch = Wisch with { RichtungToleranzGrad = v.Wisch.RichtungToleranzGrad } }
                    : Zahl() is { } f ? this with { Wisch = Wisch with { RichtungToleranzGrad = f } } : null;
            case "pinch.schwelle_an_m": return zurueck ? MitPinch(Pinch with { AnM = v.Pinch.AnM }) : Zahl() is { } g ? MitPinch(Pinch with { AnM = g }) : null;
            case "pinch.schwelle_aus_m": return zurueck ? MitPinch(Pinch with { AusM = v.Pinch.AusM }) : Zahl() is { } h ? MitPinch(Pinch with { AusM = h }) : null;
            case "fang.radius_m": return zurueck ? this with { Pinch = Pinch with { FangRadiusM = v.Pinch.FangRadiusM } } : Zahl() is { } i ? this with { Pinch = Pinch with { FangRadiusM = i } } : null;
            case "komfort.tunnelblick": return zurueck ? this with { Komfort = Komfort with { Tunnelblick = v.Komfort.Tunnelblick } } : JaNein() is { } j ? this with { Komfort = Komfort with { Tunnelblick = j } } : null;
            case "komfort.tunnelblick_staerke": return zurueck ? this with { Komfort = Komfort with { Staerke = v.Komfort.Staerke } } : Zahl() is { } k ? this with { Komfort = Komfort with { Staerke = k } } : null;
            case "komfort.ton": return zurueck ? this with { Komfort = Komfort with { Ton = v.Komfort.Ton } } : JaNein() is { } l ? this with { Komfort = Komfort with { Ton = l } } : null;
            case "begehen.drehwinkel_grad": return zurueck ? this with { DrehwinkelGrad = v.DrehwinkelGrad } : Zahl() is { } m ? this with { DrehwinkelGrad = m } : null;
            case "sitzung.zoom_min": return zurueck ? MitZoom(Zoom with { Min = v.Zoom.Min }) : Zahl() is { } n ? MitZoom(Zoom with { Min = n }) : null;
            case "sitzung.zoom_max": return zurueck ? MitZoom(Zoom with { Max = v.Zoom.Max }) : Zahl() is { } o ? MitZoom(Zoom with { Max = o }) : null;
            case "labor.variante.bedienung":
                return zurueck ? this with { Bedienung = v.Bedienung } : Wahl(Varianten.Apps, Varianten.Tablet) is { } p ? this with { Bedienung = p } : null;
            case "labor.variante.etage":
                return zurueck ? this with { Etage = v.Etage } : Wahl(Varianten.EtageWisch, Varianten.EtageApp) is { } q ? this with { Etage = q } : null;
            case "labor.variante.teleport":
                return zurueck ? this with { Teleport = v.Teleport } : Wahl(Varianten.ZeigenPinch, Varianten.Stick) is { } r ? this with { Teleport = r } : null;
            case "labor.variante.messen":
                return zurueck ? this with { Messen = v.Messen } : Wahl(Varianten.Messband, Varianten.ZweiPunkte) is { } s ? this with { Messen = s } : null;
            case "roentgen.winkel_grad":
                return zurueck ? this with { Roentgen = Roentgen with { WinkelGrad = v.Roentgen.WinkelGrad } }
                    : Zahl() is { } t ? this with { Roentgen = Roentgen with { WinkelGrad = t } } : null;
            case "roentgen.reichweite_m":
                return zurueck ? this with { Roentgen = Roentgen with { ReichweiteM = v.Roentgen.ReichweiteM } }
                    : Zahl() is { } u ? this with { Roentgen = Roentgen with { ReichweiteM = u } } : null;
            case "sitzung.schnitt_ruhe_s":
                return zurueck ? this with { FlacheHand = FlacheHand with { RuheS = v.FlacheHand.RuheS } }
                    : Zahl() is { } w ? this with { FlacheHand = FlacheHand with { RuheS = w } } : null;
            case "sitzung.schnitt_toleranz_grad":
                return zurueck ? this with { FlacheHand = FlacheHand with { ToleranzGrad = v.FlacheHand.ToleranzGrad } }
                    : Zahl() is { } x ? this with { FlacheHand = FlacheHand with { ToleranzGrad = x } } : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Mehrere Stellschrauben auf einmal (z. B. die gemerkten aus «willkommen»): in zwei Durchgängen, damit Paare (Pinch an/aus, Zoom min/max) in jeder Reihenfolge
    /// gelten. Gibt den neuen Satz und die Schlüssel zurück, die abgewiesen wurden (nur bekannte Schlüssel; fremde werden übergangen).
    /// </summary>
    public (XrBedienEinstellungen Satz, IReadOnlyList<string> Abgewiesen) MitStellschrauben(IEnumerable<KeyValuePair<string, JsonElement>> werte,
        IReadOnlyDictionary<string, (double Min, double Max)>? grenzen = null)
    {
        var offen = werte.Where(w => Kennt(w.Key)).ToList();
        var satz = this;
        for (int durchgang = 0; durchgang < 2 && offen.Count > 0; durchgang++)
        {
            var rest = new List<KeyValuePair<string, JsonElement>>();
            foreach (var w in offen)
                if (satz.MitStellschraube(w.Key, w.Value, grenzen) is { } neu) satz = neu;
                else rest.Add(w);
            offen = rest;
        }
        return (satz, [.. offen.Select(w => w.Key)]);
    }
}
