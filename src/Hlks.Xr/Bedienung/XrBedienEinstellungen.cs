// Quest App P5a (Modul XR, Interface-Labor): die Stellschrauben der Bedienung an einer Stelle – sie kommen live vom PC (Kanal «stellschraube», Seite «XR-Labor»
// in KICAD, P6) und beim Verbinden mit «willkommen» (die gemerkten Werte). Schlüssel genau wie im Katalog kataloge/xr_stellschrauben.json (P6):
//   anzeige.skala, apps.groesse_m, apps.abstand_m, apps.anordnung, wisch.mindestweg_m, wisch.mindesttempo_ms, wisch.richtung_toleranz_grad,
//   pinch.schwelle_an_m, pinch.schwelle_aus_m, fang.radius_m, komfort.tunnelblick, komfort.tunnelblick_staerke, komfort.ton, begehen.drehwinkel_grad,
//   sitzung.zoom_min, sitzung.zoom_max, labor.variante.bedienung (apps | tablet), labor.variante.etage (wisch_modell | app).
// P5b: roentgen.winkel_grad, roentgen.reichweite_m (Röntgen-Hand), sitzung.schnitt_ruhe_s, sitzung.schnitt_toleranz_grad (Schnitt mit flacher Hand),
//   labor.variante.teleport (zeigen_pinch | stick), labor.variante.messen (messband | zwei_punkte; seit V1a wirksam).
// V1a: apps.seitlich_grad, anzeige.glas_deckkraft, anzeige.glas_helligkeit, labor.variante.umgebung (hell | tron), hinweis.dauer_s, hebe.* (Hebegeste),
//   symbole.* und zeiger.tooltip_s (App-Symbole: Feld frontal mit Abstand, Mitte, Höhe und Reihen; Berührung mit der ganzen Hand; Zeiger),
//   labor.variante.symbole (verteilt | bogen), labor.variante.symbolwahl (stehen | ausblenden).
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
    /// <summary>P5b, «labor.variante.messen»: Messband aufziehen (Vorgabe) bzw. zwei Punkte setzen (V1a: App «Messen»).</summary>
    public const string Messband = "messband", ZweiPunkte = "zwei_punkte";
    /// <summary>V1a, «labor.variante.umgebung»: der Sitzungsraum hell im Stil von visionOS (Vorgabe, Christian 24.10.2026) bzw. die frühere dunkle Fassung zum Vergleich.</summary>
    public const string UmgebungHell = "hell", UmgebungTron = "tron";
    /// <summary>V1a, «labor.variante.symbole»: die App-Symbole locker verteilt (Vorgabe, Christian 24.10.2026: «symbole nicht in einer leiste sondern verteilt») bzw.
    /// im Bogen (zum Vergleich).</summary>
    public const string SymboleVerteilt = "verteilt", SymboleBogen = "bogen";
    /// <summary>V1a, «labor.variante.symbolwahl»: nach der Wahl einer App bleiben die Symbole stehen und weichen hinter die App zurück (Vorgabe; Christian 24.10.2026:
    /// «werden die aps hineingehoben bleiben diese stehen», Auslegung Hauptlauf) bzw. blenden aus (wie zuvor).</summary>
    public const string SymbolwahlStehen = "stehen", SymbolwahlAusblenden = "ausblenden";

    /// <summary>Die Varianten, die die App kennt: Name → Optionen (die erste ist die Vorgabe), wie kataloge/xr_varianten.json.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bekannt = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["bedienung"] = [Apps, Tablet], ["teleport"] = [ZeigenPinch, Stick], ["etage"] = [EtageWisch, EtageApp], ["messen"] = [Messband, ZweiPunkte],
        ["umgebung"] = [UmgebungHell, UmgebungTron], ["symbole"] = [SymboleVerteilt, SymboleBogen], ["symbolwahl"] = [SymbolwahlStehen, SymbolwahlAusblenden],
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
    /// <summary>
    /// V1a, «apps.seitlich_grad»: so weit links neben dem Blick erscheint das Fenster der App beim Holen (Grad). P5a stand die Mitte der Kacheln 50° links (AppLayout.SeitlichGrad);
    /// nach Apple HIG «Spatial layout» («Center important content within the field of view») näher an die Mitte: Annahme V1a 30° – die rechte Hand zeigt weiterhin frei
    /// nach vorn zum Springen. Am Sitzungstisch steht das Fenster neben dem Tisch (Glas.NebenTisch).
    /// </summary>
    public double SeitlichGrad { get; init; } = 30;
    /// <summary>V1a, «anzeige.glas_deckkraft»: Deckkraft der Glasfläche der Apps (0.3 … 1; Annahme V1a 0.75 – durchscheinend, deckt aber Weltobjekte dahinter weitgehend,
    /// Apple HIG «Materials»; Befund Prüfbild 24.10.2026: mit 0.55 schienen Marker und Linien durch das Fenster).</summary>
    public double GlasDeckkraft { get; init; } = 0.75;
    /// <summary>
    /// V1a, «anzeige.glas_helligkeit»: so hell darf das Glas der dunklen Fassung über dem Hintergrund höchstens wirken (sRGB 0.2 … 0.6; Annahme V1a 0.42 – helle Schrift
    /// bleibt lesbar); die helle Fassung wirkt mindestens AppFarben.HellMindestens hell. Die Deckkraft richtet sich danach (AppFarben.Deckkraft; Apple HIG «Materials»:
    /// «Glass is an adaptive material»).
    /// </summary>
    public double GlasHelligkeit { get; init; } = 0.42;
    /// <summary>V1a, «hinweis.dauer_s»: so lange steht ein schwebender Hinweis (s), dann blendet er aus (Annahme V1a 4 s; Christian 24.10.2026: «hinweistexte frei
    /// schwebend»).</summary>
    public double HinweisDauerS { get; init; } = 4;
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
    /// <summary>P5b, «labor.variante.messen»: <see cref="Varianten.Messband"/> (Vorgabe) oder <see cref="Varianten.ZweiPunkte"/> (V1a: App «Messen»).</summary>
    public string Messen { get; init; } = Varianten.Messband;
    /// <summary>V1a, «labor.variante.umgebung»: <see cref="Varianten.UmgebungHell"/> (Vorgabe) oder <see cref="Varianten.UmgebungTron"/>.</summary>
    public string Umgebung { get; init; } = Varianten.UmgebungHell;
    /// <summary>P5b: Röntgen-Hand beim Begehen («roentgen.*»).</summary>
    public RoentgenEinstellungen Roentgen { get; init; } = RoentgenEinstellungen.Vorgabe;
    /// <summary>P5b: Schnitt mit der flachen Hand am Sitzungstisch («sitzung.schnitt_*»).</summary>
    public FlacheHandEinstellungen FlacheHand { get; init; } = FlacheHandEinstellungen.Vorgabe;
    /// <summary>V1a: Hebegeste («hebe.*»).</summary>
    public HebeEinstellungen Hebe { get; init; } = HebeEinstellungen.Vorgabe;
    /// <summary>V1a: Wegwischen (rechte Hand, Handfläche nach links, von rechts nach links; «wegwischen.*»).</summary>
    public WegwischEinstellungen Wegwischen { get; init; } = WegwischEinstellungen.Vorgabe;
    /// <summary>V1a: App-Symbole und Zeiger («symbole.*», «zeiger.tooltip_s», Variante «symbole»).</summary>
    public SymbolEinstellungen Symbole { get; init; } = new();

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
        "bedienung" => Bedienung, "teleport" => Teleport, "etage" => Etage, "messen" => Messen, "umgebung" => Umgebung, "symbole" => Symbole.Verteilung,
        "symbolwahl" => Symbole.NachWahl, _ => null,
    };

    /// <summary>V1a: der Sitzungsraum in der hellen Umgebung (visionOS) statt der früheren dunklen.</summary>
    public bool UmgebungHell => Umgebung != Varianten.UmgebungTron;

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
        ["apps.seitlich_grad"] = (0, 90),
        ["anzeige.glas_deckkraft"] = (0.3, 1),
        ["anzeige.glas_helligkeit"] = (0.2, 0.6),
        ["hinweis.dauer_s"] = (1, 15),
        ["hebe.weg_m"] = (0.05, 0.5),
        ["hebe.zeit_s"] = (0.2, 3),
        ["hebe.handflaeche_grad"] = (10, 80),
        ["hebe.gleichzeitig_s"] = (0.05, 1.5),
        ["symbole.abstand_m"] = (0.3, 1.0),
        ["symbole.hoehe_m"] = (0, 0.8),
        ["symbole.feldhoehe_m"] = (0.2, 1.0),
        ["symbole.reihen"] = (1, 3),
        ["symbole.beruehr_rand_m"] = (0, 0.05),
        ["symbole.beruehr_sperre_s"] = (0, 3),
        ["symbole.streuung"] = (0.6, 1.6),
        ["symbole.wartezeit_s"] = (0, 120),
        ["zeiger.tooltip_s"] = (0, 3),
        ["wegwischen.weg_m"] = (0.05, 0.6),
        ["wegwischen.zeit_s"] = (0.2, 2),
        ["wegwischen.handflaeche_grad"] = (10, 80),
        ["wegwischen.richtung_grad"] = (5, 80),
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
        "apps.seitlich_grad", "anzeige.glas_deckkraft", "labor.variante.umgebung", "anzeige.glas_helligkeit",
        "hinweis.dauer_s", "hebe.weg_m", "hebe.zeit_s", "hebe.handflaeche_grad", "hebe.gleichzeitig_s",
        "symbole.abstand_m", "symbole.hoehe_m", "symbole.streuung", "symbole.wartezeit_s", "zeiger.tooltip_s", "labor.variante.symbole",
        "symbole.feldhoehe_m", "symbole.reihen", "symbole.beruehr_rand_m", "symbole.beruehr_sperre_s", "labor.variante.symbolwahl",
        "wegwischen.weg_m", "wegwischen.zeit_s", "wegwischen.handflaeche_grad", "wegwischen.richtung_grad",
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
            case "apps.seitlich_grad":
                return zurueck ? this with { Apps = Apps with { SeitlichGrad = v.Apps.SeitlichGrad } } : Zahl() is { } y ? this with { Apps = Apps with { SeitlichGrad = y } } : null;
            case "anzeige.glas_deckkraft":
                return zurueck ? this with { Apps = Apps with { GlasDeckkraft = v.Apps.GlasDeckkraft } } : Zahl() is { } z ? this with { Apps = Apps with { GlasDeckkraft = z } } : null;
            case "anzeige.glas_helligkeit":
                return zurueck ? this with { Apps = Apps with { GlasHelligkeit = v.Apps.GlasHelligkeit } } : Zahl() is { } gh ? this with { Apps = Apps with { GlasHelligkeit = gh } } : null;
            case "labor.variante.umgebung":
                return zurueck ? this with { Umgebung = v.Umgebung } : Wahl(Varianten.UmgebungHell, Varianten.UmgebungTron) is { } u2 ? this with { Umgebung = u2 } : null;
            case "hinweis.dauer_s":
                return zurueck ? this with { Apps = Apps with { HinweisDauerS = v.Apps.HinweisDauerS } } : Zahl() is { } hd ? this with { Apps = Apps with { HinweisDauerS = hd } } : null;
            case "hebe.weg_m":
                return zurueck ? this with { Hebe = Hebe with { WegM = v.Hebe.WegM } } : Zahl() is { } h1 ? this with { Hebe = Hebe with { WegM = h1 } } : null;
            case "hebe.zeit_s":
                return zurueck ? this with { Hebe = Hebe with { ZeitS = v.Hebe.ZeitS } } : Zahl() is { } h2 ? this with { Hebe = Hebe with { ZeitS = h2 } } : null;
            case "hebe.handflaeche_grad":
                return zurueck ? this with { Hebe = Hebe with { HandflaecheGrad = v.Hebe.HandflaecheGrad } } : Zahl() is { } h3 ? this with { Hebe = Hebe with { HandflaecheGrad = h3 } } : null;
            case "hebe.gleichzeitig_s":
                return zurueck ? this with { Hebe = Hebe with { GleichzeitigS = v.Hebe.GleichzeitigS } } : Zahl() is { } h4 ? this with { Hebe = Hebe with { GleichzeitigS = h4 } } : null;
            case "symbole.abstand_m":
                return zurueck ? this with { Symbole = Symbole with { AbstandM = v.Symbole.AbstandM } } : Zahl() is { } s1 ? this with { Symbole = Symbole with { AbstandM = s1 } } : null;
            case "symbole.hoehe_m":
                return zurueck ? this with { Symbole = Symbole with { HoeheM = v.Symbole.HoeheM } } : Zahl() is { } s2 ? this with { Symbole = Symbole with { HoeheM = s2 } } : null;
            case "symbole.streuung":
                return zurueck ? this with { Symbole = Symbole with { Streuung = v.Symbole.Streuung } } : Zahl() is { } s3 ? this with { Symbole = Symbole with { Streuung = s3 } } : null;
            case "symbole.wartezeit_s":
                return zurueck ? this with { Symbole = Symbole with { WartezeitS = v.Symbole.WartezeitS } } : Zahl() is { } s4 ? this with { Symbole = Symbole with { WartezeitS = s4 } } : null;
            case "zeiger.tooltip_s":
                return zurueck ? this with { Symbole = Symbole with { TooltipS = v.Symbole.TooltipS } } : Zahl() is { } s5 ? this with { Symbole = Symbole with { TooltipS = s5 } } : null;
            case "symbole.feldhoehe_m":
                return zurueck ? this with { Symbole = Symbole with { FeldHoeheM = v.Symbole.FeldHoeheM } } : Zahl() is { } s7 ? this with { Symbole = Symbole with { FeldHoeheM = s7 } } : null;
            case "symbole.reihen":
                return zurueck ? this with { Symbole = Symbole with { Reihen = v.Symbole.Reihen } }
                    : Zahl() is { } s8 && Math.Abs(s8 - Math.Round(s8)) < 1e-9 ? this with { Symbole = Symbole with { Reihen = (int)Math.Round(s8) } } : null;
            case "symbole.beruehr_rand_m":
                return zurueck ? this with { Symbole = Symbole with { BeruehrRandM = v.Symbole.BeruehrRandM } } : Zahl() is { } s9 ? this with { Symbole = Symbole with { BeruehrRandM = s9 } } : null;
            case "symbole.beruehr_sperre_s":
                return zurueck ? this with { Symbole = Symbole with { BeruehrSperreS = v.Symbole.BeruehrSperreS } } : Zahl() is { } s10 ? this with { Symbole = Symbole with { BeruehrSperreS = s10 } } : null;
            case "wegwischen.weg_m":
                return zurueck ? this with { Wegwischen = Wegwischen with { WegM = v.Wegwischen.WegM } } : Zahl() is { } w2 ? this with { Wegwischen = Wegwischen with { WegM = w2 } } : null;
            case "wegwischen.zeit_s":
                return zurueck ? this with { Wegwischen = Wegwischen with { ZeitS = v.Wegwischen.ZeitS } } : Zahl() is { } w3 ? this with { Wegwischen = Wegwischen with { ZeitS = w3 } } : null;
            case "wegwischen.handflaeche_grad":
                return zurueck ? this with { Wegwischen = Wegwischen with { HandflaecheGrad = v.Wegwischen.HandflaecheGrad } }
                    : Zahl() is { } w4 ? this with { Wegwischen = Wegwischen with { HandflaecheGrad = w4 } } : null;
            case "wegwischen.richtung_grad":
                return zurueck ? this with { Wegwischen = Wegwischen with { RichtungGrad = v.Wegwischen.RichtungGrad } }
                    : Zahl() is { } w5 ? this with { Wegwischen = Wegwischen with { RichtungGrad = w5 } } : null;
            case "labor.variante.symbole":
                return zurueck ? this with { Symbole = Symbole with { Verteilung = v.Symbole.Verteilung } }
                    : Wahl(Varianten.SymboleVerteilt, Varianten.SymboleBogen) is { } s6 ? this with { Symbole = Symbole with { Verteilung = s6 } } : null;
            case "labor.variante.symbolwahl":
                return zurueck ? this with { Symbole = Symbole with { NachWahl = v.Symbole.NachWahl } }
                    : Wahl(Varianten.SymbolwahlStehen, Varianten.SymbolwahlAusblenden) is { } s11 ? this with { Symbole = Symbole with { NachWahl = s11 } } : null;
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
