// MCP-Werkzeuge der Leitungen (Modul 3): Abfragen leitungsserie (B1; seit 21.10.2026 ohne Sperre gerechnet und abbrechbar), abzweigwinkel
// (Nachtrag 21.10.2026), leitungszug (B2) und verlegung (B8c), Befehle des Routings (B2:
// leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie). Dünne Hülle über dem Kern wie in
// Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern (LeitungenAbfragen.Definitionen, Leitungsserie.AbfrageText,
// Leitungszug.AbfrageText, RoutingBefehle.Par*, LeitungZeichnen.Text usw.).

using System.ComponentModel;
using System.Text.Json;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    // ---------- Abfragen ----------

    // Nachtrag 21.10.2026: leitungsserie rechnet ohne Sperre auf einem Lesestand und bricht ab, wenn der Client den Aufruf abbricht
    [McpServerTool(Name = "leitungsserie"), Description(Leitungsserie.AbfrageText)]
    public string LeitungsserieLesen([Description("Produktlinie: Familie der Kategorie Rohr bzw. Kanal (Kennzeichen, Name oder id)")] string familie,
        CancellationToken abbruch = default) =>
        LaufAbgesetzt(() => { var r = Leitungsserie.AbfrageVorbereiten(projekt, familie); return a => r(a); }, abbruch);

    [McpServerTool(Name = "abzweigwinkel"), Description(Leitungsserie.AbzweigwinkelAbfrageText)]
    public string AbzweigwinkelLesen(
        [Description(Leitungsserie.ParAbzweigLeitung)] string? leitung = null,
        [Description(Leitungsserie.ParAbzweigFamilie)] string? familie = null,
        [Description(Leitungsserie.ParAbzweigGroesse)] JsonElement? groesse = null) =>
        Lauf(() => Antwort(Leitungsserie.AbzweigwinkelAbfrage(projekt, leitung, familie, Knoten(groesse))));

    [McpServerTool(Name = "leitungszug"), Description(Leitungszug.AbfrageText)]
    public string LeitungszugLesen([Description(Leitungszug.ParLeitung)] string leitung) =>
        Lauf(() => Antwort(Leitungszug.Abfrage(projekt, leitung)));

    [McpServerTool(Name = "verlegung"), Description(Verlegung.AbfrageText)]
    public string VerlegungLesen(
        [Description(Verlegung.ParGeschoss)] string? geschoss = null,
        [Description(Verlegung.ParArt)] string? art = null) =>
        Lauf(() => Antwort(Verlegung.Abfrage(projekt, geschoss, art)));

    // ---------- Befehle (B2: Routing) ----------

    [McpServerTool(Name = "leitung_zeichnen"), Description(LeitungZeichnen.Text)]
    public string LeitungZeichnenWerkzeug(
        [Description(RoutingBefehle.ParSerie)] string serie,
        [Description(RoutingBefehle.ParGroesse)] JsonElement groesse,
        [Description(RoutingBefehle.ParPunkte)] JsonElement punkte,
        [Description(RoutingBefehle.ParVon)] JsonElement? von = null,
        [Description(RoutingBefehle.ParNach)] JsonElement? nach = null,
        [Description(RoutingBefehle.ParGefaelle)] double? gefaelle = null,
        [Description(RoutingBefehle.ParSystem)] string? system = null,
        [Description(RoutingBefehle.ParStrang)] string? strang = null,
        [Description(RoutingBefehle.ParMedium)] string? medium = null,
        [Description(RoutingBefehle.ParGeschoss)] string? geschoss = null,
        [Description(RoutingBefehle.ParPraefix)] string? praefix = null,
        [Description(RoutingBefehle.ParEigenschaften)] JsonElement? eigenschaften = null,
        [Description(RoutingBefehle.ParUebergang)] JsonElement? uebergang = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_zeichnen", Parameter(("serie", serie), ("groesse", groesse), ("punkte", punkte), ("von", von), ("nach", nach), ("gefaelle", gefaelle),
            ("system", system), ("strang", strang), ("medium", medium), ("geschoss", geschoss), ("praefix", praefix), ("eigenschaften", eigenschaften), ("uebergang", uebergang)), trockenlauf);

    [McpServerTool(Name = "fallleitung_platzieren"), Description(FallleitungPlatzieren.Text)]   // 06.11.2026
    public string FallleitungPlatzierenWerkzeug(
        [Description(FallleitungPlatzieren.ParPunkt)] JsonElement punkt,
        [Description(RoutingBefehle.ParSerie)] string serie,
        [Description(RoutingBefehle.ParGroesse)] JsonElement groesse,
        [Description(RoutingBefehle.ParSystem)] string? system = null,
        [Description(RoutingBefehle.ParStrang)] string? strang = null,
        [Description(FallleitungPlatzieren.ParVon)] string? von_geschoss = null,
        [Description(FallleitungPlatzieren.ParBis)] string? bis_geschoss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("fallleitung_platzieren", Parameter(("punkt", punkt), ("serie", serie), ("groesse", groesse), ("system", system), ("strang", strang), ("von_geschoss", von_geschoss), ("bis_geschoss", bis_geschoss)), trockenlauf);

    [McpServerTool(Name = "steigleitung_platzieren"), Description(SteigleitungPlatzieren.Text)]   // 06.11.2026
    public string SteigleitungPlatzierenWerkzeug(
        [Description(SteigleitungPlatzieren.ParPunkt)] JsonElement punkt,
        [Description(SteigleitungPlatzieren.ParLeitungen)] JsonElement leitungen,
        [Description(SteigleitungPlatzieren.ParAbstand)] double? abstand = null,
        [Description(SteigleitungPlatzieren.ParZoneBreite)] double? steigzone_breite = null,
        [Description(SteigleitungPlatzieren.ParZoneTiefe)] double? steigzone_tiefe = null,
        [Description(SteigleitungPlatzieren.ParRichtung)] JsonElement? richtung = null,
        [Description(FallleitungPlatzieren.ParVon)] string? von_geschoss = null,
        [Description(FallleitungPlatzieren.ParBis)] string? bis_geschoss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("steigleitung_platzieren", Parameter(("punkt", punkt), ("leitungen", leitungen), ("abstand", abstand), ("steigzone_breite", steigzone_breite), ("steigzone_tiefe", steigzone_tiefe), ("richtung", richtung), ("von_geschoss", von_geschoss), ("bis_geschoss", bis_geschoss)), trockenlauf);

    [McpServerTool(Name = "leitung_abzweigen"), Description(LeitungAbzweigen.Text)]
    public string LeitungAbzweigenWerkzeug(
        [Description(LeitungAbzweigen.ParHaupt)] string leitung,
        [Description(LeitungAbzweigen.ParPunkt)] JsonElement punkt,
        [Description(LeitungAbzweigen.ParWinkel)] double? winkel = null,
        [Description(LeitungAbzweigen.ParSeite)] JsonElement? seite = null,
        [Description(LeitungAbzweigen.ParRichtung)] JsonElement? richtung = null,
        [Description(LeitungAbzweigen.ParLaenge)] double? laenge = null,
        [Description(LeitungAbzweigen.ParAbzweigPunkte)] JsonElement? punkte = null,
        [Description(LeitungAbzweigen.ParAbzweigGroesse)] JsonElement? groesse = null,
        [Description(LeitungAbzweigen.ParAbzweigSerie)] string? serie = null,
        [Description(LeitungAbzweigen.ParEnde)] JsonElement? ende = null,
        [Description(RoutingBefehle.ParGefaelle)] double? gefaelle = null,
        [Description(LeitungAbzweigen.ParSystemAbzweig)] string? system = null,
        [Description(LeitungAbzweigen.ParStrangAbzweig)] string? strang = null,
        [Description(RoutingBefehle.ParMedium)] string? medium = null,
        [Description(RoutingBefehle.ParGeschoss)] string? geschoss = null,
        [Description(RoutingBefehle.ParPraefix)] string? praefix = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_abzweigen", Parameter(("leitung", leitung), ("punkt", punkt), ("winkel", winkel), ("seite", seite), ("richtung", richtung), ("laenge", laenge), ("punkte", punkte),
            ("groesse", groesse), ("serie", serie), ("ende", ende), ("gefaelle", gefaelle), ("system", system), ("strang", strang), ("medium", medium), ("geschoss", geschoss),
            ("praefix", praefix)), trockenlauf);

    [McpServerTool(Name = "leitung_aufteilen"), Description(LeitungAufteilen.Text)]
    public string LeitungAufteilenWerkzeug(
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(LeitungAufteilen.ParSerie)] string? serie = null,
        [Description(LeitungAufteilen.ParGroesse)] JsonElement? groesse = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_aufteilen", Parameter(("leitung", leitung), ("leitungen", leitungen), ("serie", serie), ("groesse", groesse)), trockenlauf);

    [McpServerTool(Name = "leitung_teilen"), Description(LeitungTeilen.Text)]   // Bearbeitungswerkzeuge A3
    public string LeitungTeilenWerkzeug(
        [Description(LeitungTeilung.ParLeitung)] string leitung,
        [Description(LeitungTeilung.ParPunkt)] JsonElement punkt,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_teilen", Parameter(("leitung", leitung), ("punkt", punkt)), trockenlauf);

    [McpServerTool(Name = "leitung_einsetzen"), Description(LeitungEinsetzen.Text)]   // Bearbeitungswerkzeuge A3
    public string LeitungEinsetzenWerkzeug(
        [Description(LeitungTeilung.ParLeitung)] string leitung,
        [Description(LeitungTeilung.ParPunkt)] JsonElement punkt,
        [Description(LeitungEinsetzen.ParFamilie)] string familie,
        [Description(LeitungEinsetzen.ParTyp)] string? typ = null,
        [Description("Exemplarwerte { name: wert }")] JsonElement? parameter = null,
        [Description("Kennzeichen des eingesetzten Elements")] string? kennzeichen = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_einsetzen", Parameter(("leitung", leitung), ("punkt", punkt), ("familie", familie), ("typ", typ), ("parameter", parameter),
            ("kennzeichen", kennzeichen)), trockenlauf);

    [McpServerTool(Name = "ecke_bilden"), Description(EckeBilden.Text)]   // Bearbeitungswerkzeuge A4
    public string EckeBildenWerkzeug(
        [Description("Erste Gerade (Kennzeichen oder Id)")] string a,
        [Description("Zweite Gerade (Kennzeichen oder Id)")] string b,
        [Description(Stutzen.ParBleibt)] JsonElement? bleibt_a = null,
        [Description(Stutzen.ParBleibt)] JsonElement? bleibt_b = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("ecke_bilden", Parameter(("a", a), ("b", b), ("bleibt_a", bleibt_a), ("bleibt_b", bleibt_b)), trockenlauf);

    [McpServerTool(Name = "dehnen_bis"), Description(DehnenBis.Text)]   // Bearbeitungswerkzeuge A4
    public string DehnenBisWerkzeug(
        [Description("Gerade, die gestutzt bzw. gedehnt wird (Kennzeichen oder Id)")] string element,
        [Description("Bezugsgerade (Kennzeichen oder Id)")] string bis,
        [Description(Stutzen.ParBleibt)] JsonElement? bleibt = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("dehnen_bis", Parameter(("element", element), ("bis", bis), ("bleibt", bleibt)), trockenlauf);

    [McpServerTool(Name = "leitung_gefaelle"), Description(LeitungGefaelle.Text)]   // Bearbeitungswerkzeuge A5
    public string LeitungGefaelleWerkzeug(
        [Description(LeitungGefaelle.ParGefaelle)] double gefaelle,
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(LeitungGefaelle.ParFix)] string? fix = null,
        [Description(LeitungGefaelle.ParZug)] bool zug = true,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_gefaelle", Parameter(("leitung", leitung), ("leitungen", leitungen), ("gefaelle", gefaelle), ("fix", fix), ("zug", zug)), trockenlauf);

    [McpServerTool(Name = "leitung_hoehe"), Description(LeitungHoehe.Text)]   // Bearbeitungswerkzeuge A5
    public string LeitungHoeheWerkzeug(
        [Description(RoutingBefehle.ParLeitung)] string leitung,
        [Description("Höhe der Achse über OK Geschoss in mm (am Anfang der Geraden)")] double? hoehe = null,
        [Description("Höhenänderung in mm (statt hoehe)")] double? dz = null,
        [Description("false: nur diese Gerade (Vorgabe true: der Leitungszug)")] bool zug = true,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("leitung_hoehe", Parameter(("leitung", leitung), ("hoehe", hoehe), ("dz", dz), ("zug", zug)), trockenlauf);

    [McpServerTool(Name = "parallele_leitungen"), Description(ParalleleLeitungen.Text)]   // Bearbeitungswerkzeuge B2
    public string ParalleleLeitungenWerkzeug(
        [Description(RoutingBefehle.ParLeitung)] string leitung,
        [Description("Achsabstand in mm (> 0)")] double abstand,
        [Description("Anzahl paralleler Züge (Vorgabe 1)")] double? anzahl = null,
        [Description(ParalleleLeitungen.ParLage)] string? lage = null,
        [Description(ParalleleLeitungen.ParSeite)] JsonElement? seite = null,
        [Description(ParalleleLeitungen.ParSysteme)] JsonElement? systeme = null,
        [Description("false: nur die Gerade (Vorgabe true: der Leitungszug)")] bool zug = true,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("parallele_leitungen", Parameter(("leitung", leitung), ("abstand", abstand), ("anzahl", anzahl), ("lage", lage), ("seite", seite), ("systeme", systeme), ("zug", zug)), trockenlauf);

    [McpServerTool(Name = "bogen_zu_tstueck"), Description(BogenZuTstueck.Text)]   // Bearbeitungswerkzeuge B3
    public string BogenZuTstueckWerkzeug(
        [Description("Bogen (Kennzeichen oder Id) zwischen zwei Geraden")] string bogen,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("bogen_zu_tstueck", Parameter(("bogen", bogen)), trockenlauf);

    [McpServerTool(Name = "tstueck_zu_bogen"), Description(TstueckZuBogen.Text)]   // Bearbeitungswerkzeuge B3
    public string TstueckZuBogenWerkzeug(
        [Description("T-Stück (Kennzeichen oder Id)")] string tstueck,
        [Description("Anschluss, der entfällt (a, b oder c); Vorgabe: der offene. An ihm darf höchstens eine Gerade mit freiem Ende hängen (sie entfällt mit)")] string? anschluss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("tstueck_zu_bogen", Parameter(("tstueck", tstueck), ("anschluss", anschluss)), trockenlauf);

    [McpServerTool(Name = "leitung_groesse"), Description(LeitungGroesseAendern.Text)]
    public string LeitungGroesseWerkzeug(
        [Description(LeitungGroesseAendern.ParGroesse)] JsonElement groesse,
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(RoutingBefehle.ParZug)] bool zug = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("leitung", leitung), ("leitungen", leitungen), ("groesse", groesse));
        if (zug) par["zug"] = true;
        return Befehl("leitung_groesse", par, trockenlauf);
    }

    [McpServerTool(Name = "leitung_serie"), Description(LeitungSerieAendern.Text)]
    public string LeitungSerieWerkzeug(
        [Description(LeitungSerieAendern.ParSerie)] string serie,
        [Description(RoutingBefehle.ParLeitung)] string? leitung = null,
        [Description(RoutingBefehle.ParLeitungen)] JsonElement? leitungen = null,
        [Description(RoutingBefehle.ParZug)] bool zug = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("leitung", leitung), ("leitungen", leitungen), ("serie", serie));
        if (zug) par["zug"] = true;
        return Befehl("leitung_serie", par, trockenlauf);
    }

    [McpServerTool(Name = "fliessrichtung_aus_quelle"), Description(FliessrichtungAusQuelle.Text)]
    public string FliessrichtungWerkzeug(
        [Description(FliessrichtungAusQuelle.ParQuelle)] string quelle,
        [Description(FliessrichtungAusQuelle.ParAnschluss)] string? anschluss = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
        => Befehl("fliessrichtung_aus_quelle", Parameter(("quelle", quelle), ("anschluss", anschluss)), trockenlauf);

    [McpServerTool(Name = "fliessrichtung_umkehren"), Description(FliessrichtungUmkehren.Text)]   // Abläufe, 09.11.2026
    public string FliessrichtungUmkehrenWerkzeug(
        [Description("Eine Gerade des Zugs (Kennzeichen oder Id)")] string leitung,
        [Description("false: nur diese Gerade umkehren (Vorgabe true: der ganze Leitungszug samt Gefälle)")] bool zug = true,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
        => Befehl("fliessrichtung_umkehren", Parameter(("leitung", leitung), ("zug", zug)), trockenlauf);
}
