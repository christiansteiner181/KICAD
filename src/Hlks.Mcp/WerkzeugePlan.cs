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
    [McpServerTool(Name = "planblatt_anlegen"), Description("Legt ein Planblatt mit H+K-Plankopf an (Format A4 bis A0, Vorgabe quer). Das Kennzeichen (PB-1, PB-2 …) wird gebildet; Ansichten kommen danach mit ansicht_platzieren auf das Blatt.")]
    public string PlanblattAnlegen(
        [Description("Plannummer im Plankopf, im Projekt eindeutig, z. B. 2317-101")] string nummer,
        [Description("Planinhalt (Titel im Plankopf), z. B. Grundriss Erdgeschoss Sanitär")] string titel,
        [Description("A4 | A3 | A2 | A1 | A0 (Vorgabe A3)")] string? format = null,
        [Description("true = Hochformat, sonst quer")] bool hochformat = false,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false)
    {
        var par = Parameter(("nummer", nummer), ("titel", titel), ("format", format));
        par["hochformat"] = hochformat;
        return Befehl("planblatt_anlegen", par, trockenlauf);
    }

    [McpServerTool(Name = "ansicht_platzieren"), Description("Setzt eine Ansicht (Grundriss, Schnitt, Ansicht, Schema) als Ansichtsfenster auf ein Planblatt. Eine Ansicht steht nur auf einem Blatt. Massstab und Ausschnitt kommen von der Ansicht; ein Schema hat keinen Massstab (Papier-mm, 1:1) und zeigt darum im Titel und im Plankopf keinen.")]
    public string AnsichtPlatzieren(
        [Description("Planblatt (Kennzeichen, Id oder Plannummer)")] string planblatt,
        [Description("Ansicht (Kennzeichen oder Id): Grundriss, Schnitt, Ansicht oder Schema")] string ansicht,
        [Description("[x, y] Mitte des Fensters auf dem Blatt in Papier-mm, Ursprung unten links; fehlt sie, kommt die Ansicht in die Mitte des freien Bereichs")] JsonElement? lage = null,
        [Description("Nur prüfen, nichts ändern")] bool trockenlauf = false) =>
        Befehl("ansicht_platzieren", Parameter(("planblatt", planblatt), ("ansicht", ansicht), ("lage", lage)), trockenlauf);

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
        [Description(PlansatzTexte.ParPruefen)] bool pruefen = false)
    {
        var par = Parameter(("plansatz", plansatz), ("ordner", ordner), ("anlass", anlass), ("blaetter", blaetter), ("abgewaehlt", abgewaehlt), ("datum", datum));
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
