// Die Tür: Abfragen und Befehle über ihren Namen und ein JSON-Parameterobjekt aufrufen. Oberfläche (REST) und
// Skripte benutzen diesen einen Einstieg; der MCP-Server bildet dieselben Namen als Werkzeuge ab.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Tuer
{
    /// <summary>Führt eine Abfrage aus Abfragen.Liste aus. Rückgabe: JsonNode oder Text (Textansichten).
    /// Fachliche Fehler kommen als HlksFehler.</summary>
    public static object Abfrage(Projekt p, string name, JsonObject? par = null)
    {
        par ??= new JsonObject();
        var a = new Abfragen(p);
        string? T(string n, bool pflicht = false) => Param.Text(par, n, pflicht);
        int I(string n, int vorgabe) => Param.Zahl(par, n) is double d ? (int)d : vorgabe;
        return name switch
        {
            "faehigkeiten" => a.Faehigkeiten(),
            "projekt_ueberblick" => a.ProjektUeberblick(),
            "element_lesen" => a.ElementLesen(T("element", true)!),
            "elemente_suchen" => a.ElementeSuchen(T("klasse"), T("geschoss"), T("raum"), T("system"), T("text"), I("seite", 1), I("proSeite", 50)),
            "system_lesen" => a.SystemLesen(T("system", true)!),
            "strang_text" => a.StrangText(T("strang", true)!),
            "netz_folgen" => a.NetzFolgen(T("von", true)!, T("richtung", true)!, T("medium"), T("bisKlasse")),
            "raum_inhalt" => a.RaumInhalt(T("raum", true)!),
            "mengen" => Ableitungen.Mengen(p, T("system"), T("geschoss")),
            "raumliste" => Ableitungen.Raumliste(p, T("geschoss")),
            "strangliste" => Ableitungen.Strangliste(p, T("system")),
            "pruefen" => a.Pruefen(T("schwere")),
            "warum" => a.Warum(T("element", true)!, T("eigenschaft", true)!),
            "aenderungen" => a.Aenderungen(T("element"), I("anzahl", 20)),
            "katalog" => a.Katalog(T("klasse")),
            "datenpunkte" => a.Datenpunkte(T("element")),
            "zeitreihe" => a.Zeitreihe(T("datenpunkt", true)!, T("von", true)!, T("bis", true)!, Param.Zahl(par, "aufloesungMinuten") ?? 60),
            "plan_pdf" => Planausgabe.PdfDatei(p, T("planblatt", true)!, T("datei")),
            "ifc_pruefen" => IfcImport.Pruefen(p, T("datei", true)!),
            "aufbau_rechnen" => HuelleAbfragen.AufbauRechnen(p, T("aufbau", true)!),
            "materialien" => HuelleAbfragen.Materialien(p),
            "huelle_pruefen" => HuelleAbfragen.HuellePruefen(p),
            "raumgrenzen" => RaumgrenzenAbfragen.Raumgrenzen(p, T("raum"), T("geschoss"), Param.Bool(par, "geometrie")),
            "huellbauteile" => RaumgrenzenAbfragen.Huellbauteile(p, T("geschoss")),
            "hkap_verbindung" or "hkap_abgleich" => HkapAbfragen.Tuer(p, name, par)!,   // Modul 1d
            _ => FamilienAbfragen.Tuer(p, name, par) ?? UnterlagenAbfragen.Tuer(p, name, par)   // Modul 2
                 ?? LeitungenAbfragen.Tuer(p, name, par) ?? SchemaAbfragen.Tuer(p, name, par)   // Modul 3
                 ?? KollisionAbfragen.Tuer(p, name, par) ?? AussparungAbfragen.Tuer(p, name, par)   // Modul 3b
                 ?? PlansatzAbfragen.Tuer(p, name, par)   // Serien-PDF
                 ?? PlanAbfragen.Tuer(p, name, par)   // Modul 7 Teil A: plannummer_vorschlag
                 ?? NormenAbfragen.Tuer(p, name, par)   // Modul 3c: Normensuche
                 ?? SanitaerAbfragen.Tuer(p, name, par)   // Modul 4 Teil A
                 ?? IfcExportAbfragen.Tuer(p, name, par)   // Modul 7 Teil B2: ifc_export
                 ?? PlanwinkelAbfragen.Tuer(p, name, par)   // N9: planwinkel
                 ?? RaumBegehenAbfragen.Tuer(p, name, par)   // N15: raum_begehen
                 ?? throw new HlksFehler($"Abfrage «{name}» gibt es nicht.", "Abfragen: " + string.Join(", ", Abfragen.Liste.Select(x => x.Name))),
        };
    }

    /// <summary>Abfrage in zwei Schritten (Nachtrag 21.10.2026, Abfrage leitungsserie): Aufruf unter der Sperre des Projekts; das Ergebnis
    /// rechnet danach ohne Sperre auf einem festen Lesestand und bricht mit dem CancellationToken ab (HTTP-Anfrage bzw. MCP-Aufruf). Für
    /// alle übrigen Abfragen rechnet schon dieser Aufruf (wie Abfrage), das Ergebnis gibt die Antwort nur zurück.</summary>
    public static Func<CancellationToken, object> AbfrageVorbereiten(Projekt p, string name, JsonObject? par = null)
    {
        par ??= new JsonObject();
        if (LeitungenAbfragen.Vorbereiten(p, name, par) is { } spaeter) return spaeter;
        var ergebnis = Abfrage(p, name, par);
        return _ => ergebnis;
    }

    /// <summary>Führt einen Befehl aus (inkl. rueckgaengig, wiederholen, synchronisieren und paket_zuruecknehmen {paket, akteurArt?}).</summary>
    public static object Befehl(Projekt p, string name, JsonObject? par = null, bool trockenlauf = false) => name switch
    {
        "rueckgaengig" => p.Rueckgaengig(trockenlauf),
        "wiederholen" => p.Wiederholen(trockenlauf),
        "synchronisieren" => p.Synchronisieren(),
        // Modul 3c: ein bestimmtes Paket zurücknehmen (KI-Palette «Zurücknehmen»); nicht als MCP-Werkzeug – die KI nimmt eigene Änderungen mit rueckgaengig zurück
        "paket_zuruecknehmen" => p.PaketZuruecknehmen(Param.Text(par ?? new JsonObject(), "paket", true)!, trockenlauf, Param.Text(par ?? new JsonObject(), "akteurArt")),
        _ => p.Ausfuehren(name, par, trockenlauf),
    };
}
