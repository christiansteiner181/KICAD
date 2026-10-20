// MCP-Werkzeuge der Planblätter (Modul 1a, Teil B4): Planblatt anlegen, Ansicht platzieren, Plan als PDF; Modul 3, B10: Einlage- und
// Grundleitungspläne anlegen (einlageplaene_anlegen).
// Dünne Hülle über dem Kern wie in Werkzeuge.cs; Name, Beschreibung und Parameter stehen genau so im Kern
// (PlanBefehle.cs, Abfragen.Liste).

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;

namespace Hlks.Mcp;

public sealed partial class Werkzeuge
{
    [McpServerTool(Name = "planblatt_anlegen"), Description(PlanBefehleTexte.Anlegen)]
    public string PlanblattAnlegen(
        [Description("Plannummer im Plankopf, im Projekt eindeutig, z. B. 2317-101; fehlt sie, gilt der Vorschlag nach dem H+K-Schema (gewerk, geschoss, ansicht, art)")] string? nummer = null,
        [Description("Planinhalt (Titel im Plankopf), z. B. Grundriss Erdgeschoss Sanitär; fehlt er, gilt der Titelvorschlag aus Gewerk und Geschoss")] string? titel = null,
        [Description("A4 | A3 | A2 | A1 | A0 (Vorgabe A3)")] string? format = null,
        [Description("true = Hochformat, sonst quer")] bool hochformat = false,
        [Description("Gewerk des Plans (heizung (auch Kälte) | lueftung | sanitaer | elektro | koordination | allgemein): setzt Planblatt.gewerk und bestimmt den Fachbuchstaben des Vorschlags")] string? gewerk = null,
        [Description("Geschoss (Kennzeichen) für den Vorschlag von Nummer und Titel")] string? geschoss = null,
        [Description("Ansicht, aus der Geschoss, Gewerk und Art des Vorschlags kommen (die Ansicht wird nicht platziert)")] string? ansicht = null,
        [Description(PlanAbfragen.ParArt)] string? art = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("nummer", nummer), ("titel", titel), ("format", format), ("gewerk", gewerk), ("geschoss", geschoss), ("ansicht", ansicht), ("art", art));
        par["hochformat"] = hochformat;
        return Befehl("planblatt_anlegen", par, trockenlauf);
    }

    [McpServerTool(Name = "plannummer_vorschlag"), Description(PlanAbfragen.Text)]
    public string PlannummerVorschlag(
        [Description(PlanAbfragen.ParGewerk)] string? gewerk = null,
        [Description(PlanAbfragen.ParGeschoss)] string? geschoss = null,
        [Description(PlanAbfragen.ParAnsicht)] string? ansicht = null,
        [Description(PlanAbfragen.ParArt)] string? art = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "plannummer_vorschlag", Parameter(("gewerk", gewerk), ("geschoss", geschoss), ("ansicht", ansicht), ("art", art)))));

    [McpServerTool(Name = "revision_hinzufuegen"), Description(RevisionTexte.Hinzufuegen)]
    public string RevisionHinzufuegenWerkzeug(
        [Description(RevisionTexte.ParPlanblatt)] string planblatt,
        [Description(RevisionTexte.ParBeschreibung)] string beschreibung,
        [Description(RevisionTexte.ParDatum)] string? datum = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("revision_hinzufuegen", Parameter(("planblatt", planblatt), ("beschreibung", beschreibung), ("datum", datum)), trockenlauf);

    [McpServerTool(Name = "plansatz_revision_hinzufuegen"), Description(RevisionTexte.PlansatzHinzufuegen)]
    public string PlansatzRevisionHinzufuegenWerkzeug(
        [Description(RevisionTexte.ParPlansatz)] string plansatz,
        [Description(RevisionTexte.ParBeschreibung)] string beschreibung,
        [Description(RevisionTexte.ParDatum)] string? datum = null,
        [Description(RevisionTexte.ParBlaetter)] JsonElement? blaetter = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("plansatz_revision_hinzufuegen", Parameter(("plansatz", plansatz), ("beschreibung", beschreibung), ("datum", datum), ("blaetter", blaetter)), trockenlauf);
    [McpServerTool(Name = "ansicht_platzieren"), Description(Hlks.Kern.AnsichtPlatzieren.Text)]
    public string AnsichtPlatzieren(
        [Description("Planblatt (Kennzeichen, Id oder Plannummer)")] string planblatt,
        [Description(Hlks.Kern.AnsichtPlatzieren.ParAnsicht)] string ansicht,
        [Description("[x, y] Mitte des Fensters auf dem Blatt in Papier-mm, Ursprung unten links; fehlt sie, kommt die Ansicht in die Mitte des freien Bereichs")] JsonElement? lage = null,
        [Description(Hlks.Kern.AnsichtPlatzieren.ParUmbruch)] string? umbruch = null,
        [Description(Hlks.Kern.AnsichtPlatzieren.ParSchrift)] double? schrift = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("ansicht_platzieren", Parameter(("planblatt", planblatt), ("ansicht", ansicht), ("lage", lage), ("umbruch", umbruch), ("schrift", schrift)), trockenlauf);

    [McpServerTool(Name = "einlageplaene_anlegen"), Description(EinlageplaeneAnlegen.Text)]
    public string EinlageplaeneAnlegenWerkzeug(
        [Description(EinlageplaeneAnlegen.ParMassstab)] double? massstab = null,
        [Description(EinlageplaeneAnlegen.ParFormat)] string? format = null,
        [Description(Verlegung.ParGeschoss)] string? geschoss = null,
        [Description(Verlegung.ParArt)] string? art = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("einlageplaene_anlegen", Parameter(("massstab", massstab), ("format", format), ("geschoss", geschoss), ("art", art)), trockenlauf);

    [McpServerTool(Name = "plan_pdf"), Description("Schreibt ein Planblatt mit seinen Ansichtsfenstern und dem H+K-Plankopf als Vektor-PDF. Ohne datei nach %TEMP%\\HLKS_CAD\\<Plannummer>.pdf. Antwort: datei, seiten, groesseBytes.")]
    public string PlanPdf(
        [Description("Planblatt (Kennzeichen, Id oder Plannummer)")] string planblatt,
        [Description("Zieldatei (.pdf); leer = Temp-Ordner")] string? datei = null) =>
        Lauf(() => Antwort(Planausgabe.PdfDatei(projekt, planblatt, datei)));

    // ---------- Serien-PDF und Plansätze (entwurf/serien-pdf.md) ----------

    [McpServerTool(Name = "plansaetze"), Description(PlansatzTexte.Aufzaehlen)]
    public string PlansaetzeLesen(
        [Description(PlansatzTexte.ParPlansatzFilter)] string? plansatz = null,
        [Description(PlansatzTexte.ParAnlassVorschlag)] string? anlass = null,
        [Description(PlansatzTexte.ParDatum)] string? datum = null) =>
        Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "plansaetze", Parameter(("plansatz", plansatz), ("anlass", anlass), ("datum", datum)))));

    [McpServerTool(Name = "plansatz_pdf"), Description(PlansatzTexte.Pdf)]
    public string PlansatzPdfSchreiben(
        [Description(PlansatzTexte.ParPdfPlansatz)] string plansatz,
        [Description(PlansatzTexte.ParOrdner)] string? ordner = null,
        [Description(PlansatzTexte.ParAnlass)] string? anlass = null,
        [Description(PlansatzTexte.ParEinzeln)] bool einzeln = false,
        [Description(PlansatzTexte.ParPdfBlaetter)] JsonElement? blaetter = null,
        [Description(PlansatzTexte.ParPdfAbgewaehlt)] JsonElement? abgewaehlt = null,
        [Description(PlansatzTexte.ParUeberschreiben)] bool ueberschreiben = false,
        [Description(PlansatzTexte.ParDatum)] string? datum = null,
        [Description(PlansatzTexte.ParPruefen)] bool pruefen = false,
        [Description(PlansatzTexte.ParStempel)] string? stempel = null)
    {
        var par = Parameter(("plansatz", plansatz), ("ordner", ordner), ("anlass", anlass), ("blaetter", blaetter), ("abgewaehlt", abgewaehlt), ("datum", datum), ("stempel", stempel));
        par["einzeln"] = einzeln; par["ueberschreiben"] = ueberschreiben; par["pruefen"] = pruefen;
        return Lauf(() => Antwort((JsonNode)Tuer.Abfrage(projekt, "plansatz_pdf", par)));
    }

    [McpServerTool(Name = "plansatz_setzen"), Description(PlansatzTexte.Setzen)]
    public string PlansatzSetzenWerkzeug(
        [Description(PlansatzTexte.ParPlansatz)] string? plansatz = null,
        [Description(PlansatzTexte.ParName)] string? name = null,
        [Description(PlansatzTexte.ParGewerk)] string? gewerk = null,
        [Description(PlansatzTexte.ParAutomatisch)] bool automatisch = false,
        [Description(PlansatzTexte.ParBlaetter)] JsonElement? blaetter = null,
        [Description(PlansatzTexte.ParAbgewaehlt)] JsonElement? abgewaehlt = null,
        [Description(PlansatzTexte.ParAusgabe)] JsonElement? ausgabe = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("plansatz", plansatz), ("name", name), ("gewerk", gewerk), ("blaetter", blaetter), ("abgewaehlt", abgewaehlt), ("ausgabe", ausgabe));
        if (automatisch) par["automatisch"] = true;   // false = unverändert lassen; ein Satz von Hand entsteht ohne automatisch
        return Befehl("plansatz_setzen", par, trockenlauf);
    }

    [McpServerTool(Name = "planblatt_gewerk_setzen"), Description(PlansatzTexte.GewerkSetzen)]
    public string PlanblattGewerkSetzenWerkzeug(
        [Description(PlansatzTexte.ParPlanblaetter)] JsonElement planblaetter,
        [Description(PlansatzTexte.ParGewerkBlatt)] string gewerk,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("planblatt_gewerk_setzen", Parameter(("planblaetter", planblaetter), ("gewerk", gewerk)), trockenlauf);
}
