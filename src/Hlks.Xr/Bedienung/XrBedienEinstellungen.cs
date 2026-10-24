// Quest App P5a (Modul XR, Interface-Labor): die Stellschrauben der Bedienung an einer Stelle – sie kommen live vom PC (Kanal «stellschraube», Seite «XR-Labor»
// in KICAD, P6) und beim Verbinden mit «willkommen» (die gemerkten Werte). Schlüssel genau wie im Katalog kataloge/xr_stellschrauben.json (P6):
//   anzeige.skala, apps.groesse_m, apps.abstand_m, apps.anordnung, wisch.mindestweg_m, wisch.mindesttempo_ms, wisch.richtung_toleranz_grad,
//   pinch.schwelle_an_m, pinch.schwelle_aus_m, fang.radius_m, komfort.tunnelblick, komfort.tunnelblick_staerke, komfort.ton, begehen.drehwinkel_grad,
//   sitzung.zoom_min, sitzung.zoom_max, labor.variante.bedienung (apps | tablet), labor.variante.etage (wisch_modell | app).
// P5b: roentgen.winkel_grad, roentgen.reichweite_m (Röntgen-Hand), sitzung.schnitt_ruhe_s, sitzung.schnitt_toleranz_grad (Schnitt mit flacher Hand),
//   labor.variante.teleport (zeigen_pinch | stick), labor.variante.messen (messband | zwei_punkte; seit V1a wirksam).
// V1a: apps.seitlich_grad, anzeige.glas_deckkraft, anzeige.glas_helligkeit, labor.variante.umgebung (hell | tron), hinweis.dauer_s, hebe.* (Hebegeste),
//   symbole.* und zeiger.tooltip_s (App-Symbole: Feld frontal mit Abstand, Mitte, Höhe und Reihen; Berührung mit der ganzen Hand; Zeiger),
//   labor.variante.symbole (verteilt | bogen), labor.variante.symbolwahl (stehen | ausblenden), labor.variante.feld_bei_app (seitlich | dahinter),
//   symbole.tisch_abstand_m (am Sitzungstisch: Luft unter der untersten Symbolreihe).
// V1d: symbole.hoehe_sitzend_m, symbole.hoehe_tisch_m, symbole.wahl_frei_s (Symbolfeld höher bzw. am Tisch tiefer, Sperre nach der Wahl), tafel.* (Tafel mittig,
//   träges Nachführen, Blättern auf der Tafel), labor.variante.tafel_nachfuehren (traege | fest), sitzung.drehknopf_grad und labor.variante.zoom_ein_griff
//   (zweite_hand | drehknopf; ein Griff am Tisch), sitzung.vereinfachen_mm und sitzung.dreiecke_max (vereinfachte Unterlage am Tisch, Hlks.Xr.Modell.TischStufe).
// V1e: gesten.anstoss_weg_m, gesten.anstoss_tempo_ms (Anstoss statt ganzer Bewegung), etagen.zeilen_max (Etagenliste auf einer Seite), labor.variante.griffe_tisch
//   (zwei_punkte | ein_griff), sitzung.dreh_totzone_grad und sitzung.zoom_totzone (zwei Punkte am Tisch), symbole.abstand_tafel_m und symbole.radius_seitlich_m (Feld
//   neben der Tafel), anzeige.glanz (Glanz der Apps).
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
    /// <summary>«labor.variante.etage»: Etage per Wisch hoch/runter bzw. nur in der App «Etagen» (Vorgabe seit dem Brillentest 24.10.2026: Geste unpraktisch).</summary>
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
    /// <summary>V1a, «labor.variante.feld_bei_app»: ist eine App offen, weicht das Symbolfeld seitlich neben das Fenster aus (Vorgabe; Christian 24.10.2026: «Das
    /// Symbolfeld weicht bei offener App seitlich neben das Fenster aus.») bzw. zum Vergleich dahinter zurück (die frühere Auslegung).</summary>
    public const string FeldSeitlich = "seitlich", FeldDahinter = "dahinter";
    /// <summary>V1d, «labor.variante.tafel_nachfuehren»: die Tafel (Glas-App, PC-Bildschirm) rückt träge in die Blickmitte nach (Vorgabe) bzw. bleibt nach dem Öffnen
    /// fest im Raum (Christian 24.10.2026: «die tafel … sollte immer zentrisch sein»; Annahme: «immer» = beim Öffnen und träge nachgeführt).</summary>
    public const string TafelTraege = "traege", TafelFest = "fest";
    /// <summary>V1d, «labor.variante.zoom_ein_griff»: am einen Griff zoomt ein Pinch der zweiten Hand über den Abstand der Hände (Vorgabe) bzw. das Rollen des
    /// Handgelenks (Christian 24.10.2026: «das modell soll nur mit einem griff bewegt und gezoomt werden»).</summary>
    public const string ZoomZweiteHand = "zweite_hand", ZoomDrehknopf = "drehknopf";
    /// <summary>V1e, «labor.variante.griffe_tisch»: am Sitzungstisch zwei Punkte – Verschieben und Drehen/Vergrössern um das Zentrum (Vorgabe; Christian 24.10.2026:
    /// «modell auf tisch soll einen verschieben punkt erhalten, und einen punkt für drehen und vergrössern») bzw. der eine Griff von V1d.</summary>
    public const string GriffeZweiPunkte = "zwei_punkte", GriffeEinGriff = "ein_griff";

    /// <summary>Die Varianten, die die App kennt: Name → Optionen (die erste ist die Vorgabe), wie kataloge/xr_varianten.json.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bekannt = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["bedienung"] = [Apps, Tablet], ["teleport"] = [ZeigenPinch, Stick], ["etage"] = [EtageApp, EtageWisch], ["messen"] = [Messband, ZweiPunkte],
        ["umgebung"] = [UmgebungHell, UmgebungTron], ["symbole"] = [SymboleVerteilt, SymboleBogen], ["symbolwahl"] = [SymbolwahlStehen, SymbolwahlAusblenden],
        ["feld_bei_app"] = [FeldSeitlich, FeldDahinter], ["tafel_nachfuehren"] = [TafelTraege, TafelFest], ["zoom_ein_griff"] = [ZoomZweiteHand, ZoomDrehknopf],
        ["griffe_tisch"] = [GriffeZweiPunkte, GriffeEinGriff],
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
    /// <summary>V1e, «etagen.zeilen_max»: bis zu so vielen Geschossen zeigt die App «Etagen» alle auf einer Seite (die Tafel wird höher); darüber rollt die Liste
    /// (Christian 24.10.2026: «die etagen auswahl geht über mehrere seiten da wäre besser die liste zu vergrössern»; Annahme V1e 12).</summary>
    public int EtagenZeilenMax { get; init; } = 12;
    /// <summary>V1e, «anzeige.glanz»: Glanz der Glas-Apps, App-Symbole und Aufgabenkarte, 0 = wie bis V1d, 1 = kräftig (Christian 24.10.2026: «apps sollen etwas
    /// mehr glanz erhalten»; Annahme V1e 0.6).</summary>
    public double Glanz { get; init; } = 0.6;
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
    /// <summary>«labor.variante.etage»: <see cref="Varianten.EtageApp"/> (Vorgabe) oder <see cref="Varianten.EtageWisch"/>.</summary>
    public string Etage { get; init; } = Varianten.EtageApp;
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
    /// <summary>V1d: die Tafel – Platz mittig, träges Nachführen, Blättern auf der Tafel («tafel.*», Variante «tafel_nachfuehren»).</summary>
    public TafelEinstellungen Tafel { get; init; } = TafelEinstellungen.Vorgabe;
    /// <summary>V1d: der eine Griff am Sitzungstisch («sitzung.drehknopf_grad», Variante «zoom_ein_griff»).</summary>
    public EinGriffEinstellungen Griff { get; init; } = EinGriffEinstellungen.Vorgabe;
    /// <summary>V1d: die vereinfachte Unterlage am Tisch («sitzung.vereinfachen_mm», «sitzung.dreiecke_max»).</summary>
    public TischStufeEinstellungen Tisch { get; init; } = TischStufeEinstellungen.Vorgabe;
    /// <summary>V1e: die zwei Punkte am Sitzungstisch (Variante «griffe_tisch», «sitzung.dreh_totzone_grad», «sitzung.zoom_totzone»).</summary>
    public ZweiPunkteEinstellungen Punkte { get; init; } = ZweiPunkteEinstellungen.Vorgabe;
    /// <summary>V1e: der Anstoss der Gesten («gesten.anstoss_*»; gilt für Hebegeste, Wegwischen und – über DesktopEinstellungen.HineinMit – das Hineinziehen).</summary>
    public AnstossEinstellungen Anstoss => Wegwischen.Anstoss;

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
        "symbolwahl" => Symbole.NachWahl, "feld_bei_app" => Symbole.FeldBeiApp, "tafel_nachfuehren" => Tafel.Nachfuehren, "zoom_ein_griff" => Griff.Zoom, "griffe_tisch" => Punkte.Variante, _ => null,
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
        ["symbole.tisch_abstand_m"] = (0, 0.3),
        ["symbole.streuung"] = (0.6, 1.6),
        ["symbole.wartezeit_s"] = (0, 120),
        ["zeiger.tooltip_s"] = (0, 3),
        ["wegwischen.weg_m"] = (0.05, 0.6),
        ["wegwischen.zeit_s"] = (0.2, 2),
        ["wegwischen.handflaeche_grad"] = (10, 80),
        ["wegwischen.richtung_grad"] = (5, 80),
        // V1d
        ["symbole.hoehe_sitzend_m"] = (0, 0.4),
        ["symbole.hoehe_tisch_m"] = (0, 1.0),
        ["symbole.wahl_frei_s"] = (0, 2),
        ["tafel.hoehe_m"] = (0, 0.6),
        ["tafel.nachfuehren_grad"] = (5, 90),
        ["tafel.blaettern_abstand_m"] = (0.01, 0.2),
        ["tafel.blaettern_weg_m"] = (0.03, 0.3),
        ["sitzung.drehknopf_grad"] = (20, 180),
        ["sitzung.vereinfachen_mm"] = (0, 5),
        ["sitzung.dreiecke_max"] = (50_000, 2_000_000),
        // V1e
        ["gesten.anstoss_weg_m"] = (0.02, 0.2),
        ["gesten.anstoss_tempo_ms"] = (0.2, 3),
        ["etagen.zeilen_max"] = (4, 30),
        ["sitzung.dreh_totzone_grad"] = (0, 20),
        ["sitzung.zoom_totzone"] = (0, 0.3),
        ["symbole.abstand_tafel_m"] = (0.02, 0.4),
        ["symbole.radius_seitlich_m"] = (0.3, 1.0),
        ["anzeige.glanz"] = (0, 1),
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
        "symbole.tisch_abstand_m", "labor.variante.feld_bei_app",
        // V1d
        "symbole.hoehe_sitzend_m", "symbole.hoehe_tisch_m", "symbole.wahl_frei_s", "tafel.hoehe_m", "tafel.nachfuehren_grad", "tafel.blaettern_abstand_m",
        "tafel.blaettern_weg_m", "labor.variante.tafel_nachfuehren", "sitzung.drehknopf_grad", "labor.variante.zoom_ein_griff", "sitzung.vereinfachen_mm",
        "sitzung.dreiecke_max",
        // V1e
        "gesten.anstoss_weg_m", "gesten.anstoss_tempo_ms", "etagen.zeilen_max", "labor.variante.griffe_tisch", "sitzung.dreh_totzone_grad", "sitzung.zoom_totzone",
        "symbole.abstand_tafel_m", "symbole.radius_seitlich_m", "anzeige.glanz",
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
        XrBedienEinstellungen MitAnstoss(AnstossEinstellungen a) => this with { Hebe = Hebe with { Anstoss = a }, Wegwischen = Wegwischen with { Anstoss = a } };
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
            case "labor.variante.feld_bei_app":
                return zurueck ? this with { Symbole = Symbole with { FeldBeiApp = v.Symbole.FeldBeiApp } }
                    : Wahl(Varianten.FeldSeitlich, Varianten.FeldDahinter) is { } s12 ? this with { Symbole = Symbole with { FeldBeiApp = s12 } } : null;
            case "symbole.tisch_abstand_m":
                return zurueck ? this with { Symbole = Symbole with { TischAbstandM = v.Symbole.TischAbstandM } }
                    : Zahl() is { } s13 ? this with { Symbole = Symbole with { TischAbstandM = s13 } } : null;
            // V1d
            case "symbole.hoehe_sitzend_m":
                return zurueck ? this with { Symbole = Symbole with { HoeheSitzendM = v.Symbole.HoeheSitzendM } }
                    : Zahl() is { } d1 ? this with { Symbole = Symbole with { HoeheSitzendM = d1 } } : null;
            case "symbole.hoehe_tisch_m":
                return zurueck ? this with { Symbole = Symbole with { HoeheTischM = v.Symbole.HoeheTischM } }
                    : Zahl() is { } d2 ? this with { Symbole = Symbole with { HoeheTischM = d2 } } : null;
            case "symbole.wahl_frei_s":
                return zurueck ? this with { Symbole = Symbole with { WahlFreiS = v.Symbole.WahlFreiS } }
                    : Zahl() is { } d3 ? this with { Symbole = Symbole with { WahlFreiS = d3 } } : null;
            case "tafel.hoehe_m":
                return zurueck ? this with { Tafel = Tafel with { HoeheM = v.Tafel.HoeheM } } : Zahl() is { } d4 ? this with { Tafel = Tafel with { HoeheM = d4 } } : null;
            case "tafel.nachfuehren_grad":
                return zurueck ? this with { Tafel = Tafel with { NachfuehrenGrad = v.Tafel.NachfuehrenGrad } }
                    : Zahl() is { } d5 ? this with { Tafel = Tafel with { NachfuehrenGrad = d5 } } : null;
            case "tafel.blaettern_abstand_m":
                return zurueck ? this with { Tafel = Tafel with { BlaetternAbstandM = v.Tafel.BlaetternAbstandM } }
                    : Zahl() is { } d6 ? this with { Tafel = Tafel with { BlaetternAbstandM = d6 } } : null;
            case "tafel.blaettern_weg_m":
                return zurueck ? this with { Tafel = Tafel with { BlaetternWegM = v.Tafel.BlaetternWegM } }
                    : Zahl() is { } d7 ? this with { Tafel = Tafel with { BlaetternWegM = d7 } } : null;
            case "labor.variante.tafel_nachfuehren":
                return zurueck ? this with { Tafel = Tafel with { Nachfuehren = v.Tafel.Nachfuehren } }
                    : Wahl(Varianten.TafelTraege, Varianten.TafelFest) is { } d8 ? this with { Tafel = Tafel with { Nachfuehren = d8 } } : null;
            case "sitzung.drehknopf_grad":
                return zurueck ? this with { Griff = Griff with { DrehknopfGrad = v.Griff.DrehknopfGrad } }
                    : Zahl() is { } d9 ? this with { Griff = Griff with { DrehknopfGrad = d9 } } : null;
            case "labor.variante.zoom_ein_griff":
                return zurueck ? this with { Griff = Griff with { Zoom = v.Griff.Zoom } }
                    : Wahl(Varianten.ZoomZweiteHand, Varianten.ZoomDrehknopf) is { } d10 ? this with { Griff = Griff with { Zoom = d10 } } : null;
            case "sitzung.vereinfachen_mm":
                return zurueck ? this with { Tisch = Tisch with { VereinfachenMm = v.Tisch.VereinfachenMm } }
                    : Zahl() is { } d11 ? this with { Tisch = Tisch with { VereinfachenMm = d11 } } : null;
            case "sitzung.dreiecke_max":
                return zurueck ? this with { Tisch = Tisch with { DreieckeMax = v.Tisch.DreieckeMax } }
                    : Zahl() is { } d12 ? this with { Tisch = Tisch with { DreieckeMax = d12 } } : null;
            // V1e: der Anstoss gilt für Hebegeste und Wegwischen (und über HineinMit für das Hineinziehen)
            case "gesten.anstoss_weg_m":
                return (zurueck ? v.Anstoss.WegM : Zahl()) is { } e1 ? MitAnstoss(Anstoss with { WegM = e1 }) : null;
            case "gesten.anstoss_tempo_ms":
                return (zurueck ? v.Anstoss.TempoMs : Zahl()) is { } e2 ? MitAnstoss(Anstoss with { TempoMs = e2 }) : null;
            case "etagen.zeilen_max":
                return zurueck ? this with { Apps = Apps with { EtagenZeilenMax = v.Apps.EtagenZeilenMax } }
                    : Zahl() is { } e3 && Math.Abs(e3 - Math.Round(e3)) < 1e-9 ? this with { Apps = Apps with { EtagenZeilenMax = (int)Math.Round(e3) } } : null;
            case "labor.variante.griffe_tisch":
                return zurueck ? this with { Punkte = Punkte with { Variante = v.Punkte.Variante } }
                    : Wahl(Varianten.GriffeZweiPunkte, Varianten.GriffeEinGriff) is { } e4 ? this with { Punkte = Punkte with { Variante = e4 } } : null;
            case "sitzung.dreh_totzone_grad":
                return zurueck ? this with { Punkte = Punkte with { DrehTotzoneGrad = v.Punkte.DrehTotzoneGrad } }
                    : Zahl() is { } e5 ? this with { Punkte = Punkte with { DrehTotzoneGrad = e5 } } : null;
            case "sitzung.zoom_totzone":
                return zurueck ? this with { Punkte = Punkte with { ZoomTotzone = v.Punkte.ZoomTotzone } }
                    : Zahl() is { } e6 ? this with { Punkte = Punkte with { ZoomTotzone = e6 } } : null;
            case "symbole.abstand_tafel_m":
                return zurueck ? this with { Symbole = Symbole with { AbstandTafelM = v.Symbole.AbstandTafelM } }
                    : Zahl() is { } e7 ? this with { Symbole = Symbole with { AbstandTafelM = e7 } } : null;
            case "symbole.radius_seitlich_m":
                return zurueck ? this with { Symbole = Symbole with { RadiusSeitlichM = v.Symbole.RadiusSeitlichM } }
                    : Zahl() is { } e8 ? this with { Symbole = Symbole with { RadiusSeitlichM = e8 } } : null;
            case "anzeige.glanz":
                return zurueck ? this with { Apps = Apps with { Glanz = v.Apps.Glanz } } : Zahl() is { } e9 ? this with { Apps = Apps with { Glanz = e9 } } : null;
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
