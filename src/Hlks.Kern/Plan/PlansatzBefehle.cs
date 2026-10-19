// Befehle und Abfragen der Plansätze (Serien-PDF, entwurf/serien-pdf.md):
//   plansatz_setzen          Plansatz anlegen bzw. ändern (Name, Gewerk, automatisch, Blätter in Reihenfolge, abgewählte, Ausgabe-Einstellung)
//   planblatt_gewerk_setzen  Gewerk von Planblättern von Hand setzen (Vorschläge bestätigen)
//   Abfrage plansaetze       alle Plansätze, automatische je Gewerk angeboten bzw. nachgeführt (ändert nichts)
//   Abfrage plansatz_pdf     Sammel-PDF und auf Wunsch Einzel-PDFs schreiben (Plansatzausgabe.cs)
// Beide Befehle ändern über ElementAnlegen/ElementAendern: Katalogprüfung, Kennzeichen, Herkunft, Trockenlauf und Rückgängig wie überall.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Texte der Befehle und Abfragen – dieselben stehen an den MCP-Werkzeugen (WerkzeugePlan.cs).</summary>
public static class PlansatzTexte
{
    public const string Setzen =
        "Legt einen Plansatz für das Serien-PDF an oder ändert ihn. Automatischer Plansatz (automatisch): alle Planblätter eines Gewerks, neue kommen von selbst dazu, " +
        "Reihenfolge nach Plannummer, von Hand verschobene bleibt; «auto:<gewerk>» als plansatz legt den angebotenen Satz des Gewerks an. Plansatz von Hand: " +
        "nur die genannten Blätter. blaetter = Reihenfolge, abgewaehlt = beim Export nicht dabei (bleibt gespeichert), ausgabe = letzte Einstellung { anlass, ordner, einzeln }. " +
        "Trockenlauf zeigt, was entstünde.";
    public const string ParPlansatz = "Plansatz (Kennzeichen, Id oder Name) bzw. «auto:<gewerk>» für den automatischen Satz eines Gewerks; fehlt er, wird ein neuer Plansatz angelegt";
    public const string ParName = "Name, z. B. «Plansatz Sanitär»; beim Anlegen nötig, bei einem automatischen Satz mit Gewerk «Plansatz <Gewerk>»";
    public const string ParGewerk = "Gewerk: heizung | lueftung | sanitaer | kaelte | elektro | koordination | allgemein";
    public const string ParAutomatisch = "true = automatischer Plansatz des Gewerks (braucht gewerk); ohne Angabe bleibt der Wert, beim Anlegen nein";
    public const string ParBlaetter = "Planblätter (Kennzeichen oder Plannummer) in der Reihenfolge des PDF; bei einem automatischen Satz müssen sie zum Gewerk gehören";
    public const string ParAbgewaehlt = "Planblätter, die beim Export nicht dabei sind (die gewählte Auswahl bleibt für den nächsten Export gespeichert)";
    public const string ParAusgabe = "Letzte Ausgabe-Einstellung { anlass, ordner (voller Pfad; null = Vorschlag), einzeln (zusätzlich je Plan ein PDF), zeit }; angegebene Felder überschreiben";

    public const string GewerkSetzen =
        "Setzt das Gewerk von Planblättern von Hand (bestätigt auch einen Vorschlag aus Plannummer oder Ansichten). gewerk «vorschlag» nimmt die Angabe zurück. " +
        "Das Gewerk bestimmt, in welchem automatischen Plansatz ein Blatt steht.";
    public const string ParPlanblaetter = "Planblätter (Kennzeichen oder Plannummer)";
    public const string ParGewerkBlatt = "heizung | lueftung | sanitaer | kaelte | elektro | koordination | allgemein, oder «vorschlag» (Angabe zurücknehmen)";

    public const string Aufzaehlen =
        "Alle Plansätze für das Serien-PDF. Automatische Plansätze je Gewerk, für das es Planblätter gibt: angelegte nachgeführt, noch nicht angelegte angeboten " +
        "(plansatz null, schluessel «auto:<gewerk>»); dazu die von Hand. Je Satz die Blätter in Reihenfolge (nach Plannummer, von Hand verschobene bleibt) mit Nummer, Titel, " +
        "Format, Gewerk (gesetzt oder Vorschlag mit Quelle), dabei/abgewählt, Einzeldatei; dazu der Vorschlag für Anlass, Ausgabeordner " +
        "(<Objekt>\\c_Pläne und Modelle\\d_Ausgang\\b_Pläne\\<TT.MM.JJJJ>_<Anlass>\\<Gewerk>) und die Sammeldatei. Ändert nichts.";
    public const string ParPlansatzFilter = "Nur dieser Plansatz (Kennzeichen, Name oder «auto:<gewerk>»)";
    public const string ParAnlassVorschlag = "Anlass für den Ordnervorschlag, z. B. Revisionspläne; leer = der zuletzt gespeicherte";
    public const string ParDatum = "Datum der Ausgabe JJJJ-MM-TT; leer = heute";

    public const string Pdf =
        "Schreibt einen Plansatz als Serien-PDF: ein Sammel-PDF mit allen gewählten Planblättern nacheinander (jede Seite im Format ihres Blatts, ein Lesezeichen je Plan «S01 Sanitär 1.UG») " +
        "und auf Wunsch je Plan ein PDF mit H+K-Namen («1-0768-S01-Sanitär 1.Untergeschoss-.pdf»). Legt den Ordner an (Vorschlag <Objekt>\\c_Pläne und Modelle\\d_Ausgang\\b_Pläne\\<TT.MM.JJJJ>_<Anlass>\\<Gewerk>). " +
        "Überschreibt keine vorhandene Datei ohne ueberschreiben = true (sonst Fehler mit Vorschlag). Speichert Auswahl, Reihenfolge und Einstellung im Plansatz (legt einen angebotenen automatischen Satz dabei an). " +
        "Antwort: Dateien, Seiten, Grösse, Dauer, Hinweise.";
    public const string ParPdfPlansatz = "Plansatz (Kennzeichen, Name oder «auto:<gewerk>», auch ein Gewerkname wie sanitaer)";
    public const string ParOrdner = "Zielordner (voller Pfad); leer = der gespeicherte, sonst der Vorschlag";
    public const string ParAnlass = "Anlass für den Ordnernamen, z. B. Revisionspläne; leer = der gespeicherte";
    public const string ParEinzeln = "true = zusätzlich je Plan ein PDF (Vorgabe nein)";
    public const string ParPdfBlaetter = "Planblätter in der Reihenfolge des PDF; ohne abgewaehlt sind nur diese dabei; fehlt der Parameter, gelten die gespeicherte Reihenfolge und Auswahl";
    public const string ParPdfAbgewaehlt = "Planblätter, die diesmal nicht dabei sind (mit blaetter: die Liste enthält dann alle Blätter, auch die abgewählten)";
    public const string ParUeberschreiben = "true = vorhandene Dateien überschreiben (Vorgabe nein)";
    public const string ParPruefen = "true = nichts schreiben, nur zeigen, was geschrieben würde und welche Dateien es schon gibt";
}

public static class PlansatzBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new PlansatzSetzen());
        r.Hinzufuegen(new PlanblattGewerkSetzen());
    }
}

public static class PlansatzAbfragen
{
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("plansaetze", PlansatzTexte.Aufzaehlen,
        [
            new("plansatz", "text", false, PlansatzTexte.ParPlansatzFilter), new("anlass", "text", false, PlansatzTexte.ParAnlassVorschlag), new("datum", "text", false, PlansatzTexte.ParDatum),
        ]),
        new("plansatz_pdf", PlansatzTexte.Pdf,
        [
            new("plansatz", "text", true, PlansatzTexte.ParPdfPlansatz), new("ordner", "text", false, PlansatzTexte.ParOrdner), new("anlass", "text", false, PlansatzTexte.ParAnlass),
            new("einzeln", "bool", false, PlansatzTexte.ParEinzeln), new("blaetter", "liste", false, PlansatzTexte.ParPdfBlaetter), new("abgewaehlt", "liste", false, PlansatzTexte.ParPdfAbgewaehlt),
            new("ueberschreiben", "bool", false, PlansatzTexte.ParUeberschreiben), new("datum", "text", false, PlansatzTexte.ParDatum), new("pruefen", "bool", false, PlansatzTexte.ParPruefen),
        ]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "plansaetze" => Aufzaehlen(p, par),
        "plansatz_pdf" => Plansatzausgabe.Schreiben(p, par),
        _ => null,
    };

    static JsonObject Aufzaehlen(Projekt p, JsonObject par)
    {
        var datum = Plaene.Datum(Param.Text(par, "datum"));
        var anlass = Param.Text(par, "anlass");
        var alle = Plaene.Alle(p.Modell);
        var gewaehlt = Param.Text(par, "plansatz") is { Length: > 0 } f ? [Plaene.Finde(p.Modell, f)] : alle;
        return new JsonObject
        {
            ["projektnummer"] = Plaene.Projektnummer(p.Modell),
            ["objektordner"] = Plaene.Objektordner(p),
            ["ausgabewurzel"] = Plaene.Ausgabewurzel(p),
            ["datum"] = datum.ToString("yyyy-MM-dd"),
            ["gewerke"] = new JsonArray(PlanGewerk.Alle.Select(g => (JsonNode)new JsonObject { ["gewerk"] = g.Schluessel, ["name"] = g.Name }).ToArray()),
            ["plansaetze"] = new JsonArray(gewaehlt.Select(s => (JsonNode)Plaene.Auskunft(p, s, anlass, datum)).ToArray()),
            ["ohneBlaetter"] = Plaene.Blaetter(p.Modell).Any() ? null : "Es gibt noch keine Planblätter (planblatt_anlegen).",
        };
    }
}

public sealed class PlansatzSetzen : IBefehl
{
    public string Name => "plansatz_setzen";
    public string Beschreibung => PlansatzTexte.Setzen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("plansatz", "text", false, PlansatzTexte.ParPlansatz),
        new("name", "text", false, PlansatzTexte.ParName),
        new("gewerk", "text", false, PlansatzTexte.ParGewerk),
        new("automatisch", "bool", false, PlansatzTexte.ParAutomatisch),
        new("blaetter", "liste", false, PlansatzTexte.ParBlaetter),
        new("abgewaehlt", "liste", false, PlansatzTexte.ParAbgewaehlt),
        new("ausgabe", "objekt", false, PlansatzTexte.ParAusgabe),
    ];

    static readonly string[] AusgabeFelder = ["anlass", "ordner", "einzeln", "zeit"];

    /// <summary>Liste von Planblättern → Kennzeichen ohne Doppelte, jedes muss ein Planblatt sein.</summary>
    static JsonArray Blaetter(Kontext k, JsonObject p, string name)
    {
        if (p[name] is not JsonArray a) throw new HlksFehler($"Parameter «{name}» muss eine Liste von Planblättern sein.", "Beispiel: [\"PB-1\", \"PB-3\"] oder Plannummern.");
        var kz = new List<string>();
        foreach (var n in a)
        {
            var bezug = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler($"«{name}» enthält einen Eintrag, der kein Text ist.", "Planblätter als Kennzeichen oder Plannummer angeben.");
            var b = Planausgabe.Blatt(k.Arbeit, bezug);
            if (!kz.Contains(b.Kennzeichen)) kz.Add(b.Kennzeichen);
        }
        return new JsonArray(kz.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    }

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        var bezug = Param.Text(p, "plansatz")?.Trim();
        SatzSicht? bestehend = null;
        if (!string.IsNullOrEmpty(bezug)) bestehend = Plaene.Finde(m, bezug);

        var gewerk = p.ContainsKey("gewerk") && Param.Text(p, "gewerk") is { Length: > 0 } gt ? PlanGewerk.Hole(gt).Schluessel : null;
        var eig = new JsonObject();
        string? name = Param.Text(p, "name")?.Trim();
        if (name is { Length: 0 }) throw new HlksFehler("Der Name ist leer.", "Einen Namen angeben, z. B. «Plansatz Sanitär».");

        bool automatisch = p.ContainsKey("automatisch") ? Param.Bool(p, "automatisch") : bestehend?.Automatisch ?? false;
        var gewerkWirksam = gewerk ?? bestehend?.Gewerk;
        if (automatisch && gewerkWirksam is null)
            throw new HlksFehler("Ein automatischer Plansatz braucht ein Gewerk.", "gewerk angeben (heizung, lueftung, sanitaer, kaelte, elektro, koordination, allgemein) oder automatisch weglassen.");

        if (gewerk is not null) eig["gewerk"] = gewerk;
        if (p.ContainsKey("automatisch")) eig["automatisch"] = automatisch;
        else if (bestehend is null && automatisch) eig["automatisch"] = true;
        // Hat ein Satz ohne Element (angebotener automatischer Satz) gewerk nur über den Schlüssel, steht es schon in gewerkWirksam
        if (bestehend is { Element: null }) { eig["gewerk"] = gewerkWirksam; eig["automatisch"] = true; }

        if (p.ContainsKey("blaetter"))
        {
            var liste = Blaetter(k, p, "blaetter");
            if (automatisch)
            {
                var pn = Plaene.Projektnummer(m);
                foreach (var n in liste)
                {
                    var b = m.Finde(n!.GetValue<string>())!;
                    var g = Plaene.GewerkVon(m, b, pn);
                    if (g.Gewerk != gewerkWirksam)
                        throw new HlksFehler($"{b.Kennzeichen} ({b.Text("nummer")}) gehört zum Gewerk {PlanGewerk.Name(g.Gewerk)} ({g.Quelle}), nicht zu {PlanGewerk.Name(gewerkWirksam!)}.",
                            "Das Gewerk des Blatts mit planblatt_gewerk_setzen ändern oder einen Plansatz von Hand anlegen (automatisch false).");
                }
            }
            eig["blaetter"] = liste;
        }
        if (p.ContainsKey("abgewaehlt")) eig["abgewaehlt"] = Blaetter(k, p, "abgewaehlt");

        if (p.ContainsKey("ausgabe"))
        {
            if (p["ausgabe"] is not JsonObject neu) throw new HlksFehler("Parameter «ausgabe» muss ein Objekt sein.", "Beispiel: { \"anlass\": \"Revisionspläne\", \"einzeln\": true }");
            foreach (var key in neu.Select(x => x.Key).Where(key => !AusgabeFelder.Contains(key)))
                throw new HlksFehler($"ausgabe.{key} gibt es nicht.", "Erlaubt: " + string.Join(", ", AusgabeFelder));
            var summe = bestehend?.Ausgabe is { } alt ? (JsonObject)alt.DeepClone() : new JsonObject();
            foreach (var (key, wert) in neu)
            {
                bool leer = wert is null || (wert is JsonValue sv && sv.TryGetValue<string>(out var st) && st.Length == 0);
                if (leer) { summe.Remove(key); continue; }
                if (key == "einzeln" && !(wert is JsonValue bv && bv.TryGetValue<bool>(out _))) throw new HlksFehler("ausgabe.einzeln muss true oder false sein.");
                if (key != "einzeln" && !(wert is JsonValue tv && tv.TryGetValue<string>(out _))) throw new HlksFehler($"ausgabe.{key} muss Text sein.");
                summe[key] = wert!.DeepClone();
            }
            eig["ausgabe"] = summe;
        }

        if (bestehend?.Element is { } satz)
        {
            var par = new JsonObject { ["element"] = satz.Id };
            if (name is not null && name != satz.Text("name")) { par["name"] = name; eig["name"] = name; }
            if (eig.Count > 0) par["eigenschaften"] = eig;
            if (par.Count == 1) { k.Hinweise.Add("Keine Änderung."); return; }
            new ElementAendern().Ausfuehren(k, par);
            if (k.Operationen.Count > 0) k.Hinweise.Add($"Plansatz {satz.Kennzeichen} «{name ?? SatzName(satz)}» geändert.");
            return;
        }

        // Anlegen: angebotener automatischer Satz oder neuer Satz von Hand
        name ??= automatisch ? "Plansatz " + PlanGewerk.Name(gewerkWirksam!) : null;
        if (name is null) throw new HlksFehler("Der Name fehlt.", "name angeben, z. B. «Plansatz Sanitär», oder ein Gewerk mit automatisch = true.");
        if (m.DerKlasse("Plansatz").Any(s => string.Equals(s.Text("name"), name, StringComparison.OrdinalIgnoreCase)))
            throw new HlksFehler($"Einen Plansatz «{name}» gibt es schon.", "Einen anderen Namen wählen oder den bestehenden mit plansatz ändern.");
        eig["name"] = name;
        new ElementAnlegen().Ausfuehren(k, new JsonObject { ["klasse"] = "Plansatz", ["name"] = name, ["eigenschaften"] = eig });
        var neuSatz = m.Finde(k.Operationen[^1].Id)!;
        var anzahl = Plaene.Alle(m).FirstOrDefault(s => s.Element?.Id == neuSatz.Id)?.Blaetter.Count ?? 0;
        k.Hinweise.Add($"Plansatz {neuSatz.Kennzeichen} «{name}» angelegt ({(automatisch ? "automatisch, Gewerk " + PlanGewerk.Name(gewerkWirksam!) : "von Hand")}, {anzahl} Blätter).");
    }

    static string SatzName(Element satz) => satz.Text("name") ?? satz.Name ?? satz.Kennzeichen;
}

public sealed class PlanblattGewerkSetzen : IBefehl
{
    public string Name => "planblatt_gewerk_setzen";
    public string Beschreibung => PlansatzTexte.GewerkSetzen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("planblaetter", "liste", true, PlansatzTexte.ParPlanblaetter),
        new("gewerk", "text", true, PlansatzTexte.ParGewerkBlatt),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit;
        if (p["planblaetter"] is not JsonArray liste || liste.Count == 0)
            throw new HlksFehler("Parameter «planblaetter» muss eine nicht leere Liste von Planblättern sein.", "Beispiel: [\"PB-1\", \"PB-2\"] oder Plannummern.");
        var roh = Param.Text(p, "gewerk", true)!.Trim();
        bool zurueck = roh.Length == 0 || roh.Equals("vorschlag", StringComparison.OrdinalIgnoreCase);
        var gewerk = zurueck ? null : PlanGewerk.Hole(roh);
        var pn = Plaene.Projektnummer(m);

        var blaetter = new List<Element>();
        foreach (var n in liste)
        {
            var bezug = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler("«planblaetter» enthält einen Eintrag, der kein Text ist.", "Planblätter als Kennzeichen oder Plannummer angeben.");
            var b = Planausgabe.Blatt(m, bezug);
            if (!blaetter.Contains(b)) blaetter.Add(b);
        }
        foreach (var b in blaetter)
        {
            var vorher = Plaene.GewerkVon(m, b, pn);
            new ElementAendern().Ausfuehren(k, new JsonObject
            {
                ["element"] = b.Id,
                ["eigenschaften"] = new JsonObject { ["gewerk"] = gewerk?.Schluessel },
            });
            var nachher = Plaene.GewerkVon(m, m.Finde(b.Id)!, pn);
            var nr = b.Text("nummer") is { Length: > 0 } t ? $" ({t})" : "";
            k.Hinweise.Add(zurueck
                ? $"{b.Kennzeichen}{nr}: Angabe zurückgenommen, Gewerk wieder Vorschlag: {PlanGewerk.Name(nachher.Gewerk)} ({nachher.Quelle})."
                : $"{b.Kennzeichen}{nr}: Gewerk {gewerk!.Name} gesetzt" + (vorher.Gesetzt ? $" (war {PlanGewerk.Name(vorher.Gewerk)})." : vorher.Gewerk == gewerk.Schluessel ? " (Vorschlag bestätigt)." : $" (Vorschlag war {PlanGewerk.Name(vorher.Gewerk)})."));
        }
    }
}
