// Befehle der Planblätter: Planblatt anlegen, Ansicht platzieren (Ansichtsfenster). Beide legen Elemente über
// ElementAnlegen an, damit Katalogprüfung, Kennzeichen, Herkunft und Rückgängig dieselben sind wie überall.
// Die PDF-Ausgabe steht in Planausgabe.cs (Abfrage plan_pdf).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class PlanBefehle
{
    public static void Registrieren(Befehlsregister r)
    {
        r.Hinzufuegen(new PlanblattAnlegen());
        r.Hinzufuegen(new AnsichtPlatzieren());
        r.Hinzufuegen(new EinlageplaeneAnlegen());   // Modul 3, B10 (EinlagePlaene.cs)
        r.Hinzufuegen(new RevisionHinzufuegen());    // Modul 7 Teil A: Revision eines Planblatts (Revisionen.cs)
        r.Hinzufuegen(new PlansatzRevisionHinzufuegen());   // … für alle gewählten Blätter eines Plansatzes
        PlansatzBefehle.Registrieren(r);             // Serien-PDF: plansatz_setzen, planblatt_gewerk_setzen (PlansatzBefehle.cs)
    }
}

/// <summary>Abfrage plannummer_vorschlag (Modul 7 Teil A): die Plannummer, die planblatt_anlegen ohne Nummer vergeben würde – ändert nichts.</summary>
public static class PlanAbfragen
{
    public const string Text = "Schlägt eine Plannummer nach dem H+K-Schema vor «<Projektnummer> <Fach><NN>» (Fach H Heizung, L Lüftung, S Sanitär, K Koordination; NN je Geschoss von unten, bei S ab 00, sonst ab 01; " +
        "Sonderblätter FBH H13 ff., Steigzonen H21, Grundleitungen/Einlagen/Kanalisation S06–S10) samt Begründung, Titelvorschlag und Hinweisen (z. B. Nummer schon vergeben → nächste freie). Ändert nichts.";
    public const string ParGewerk = "heizung | lueftung | sanitaer | koordination (Kälte gehört zu heizung; elektro hat keinen Fachbuchstaben); fehlt es, aus den Gewerken der Ansicht";
    public const string ParGeschoss = "Geschoss (Kennzeichen); fehlt es, das der Ansicht";
    public const string ParAnsicht = "Ansicht, für deren Blatt die Nummer gedacht ist (liefert Geschoss, Gewerk und Art)";
    public const string ParArt = "geschoss (Vorgabe) | fbh | steigzonen | grundleitungen | einlagen | kanalisation";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("plannummer_vorschlag", Text,
        [
            new("gewerk", "text", false, ParGewerk), new("geschoss", "text", false, ParGeschoss), new("ansicht", "text", false, ParAnsicht), new("art", "text", false, ParArt),
        ]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "plannummer_vorschlag"
        ? Plannummernvorschlag.Berechnen(p, Param.Text(par, "gewerk"), Param.Text(par, "geschoss"), Param.Text(par, "ansicht"), Param.Text(par, "art")).Json()
        : null;
}

public static class PlanBefehleTexte
{
    public const string Anlegen = PlanblattAnlegen.Text;
}

public sealed class PlanblattAnlegen : IBefehl
{
    public string Name => "planblatt_anlegen";
    public const string Text = "Legt ein Planblatt mit H+K-Plankopf an (Format A4 bis A0, Vorgabe quer). Das Kennzeichen (PB-1, PB-2 …) wird gebildet; Ansichten kommen danach mit ansicht_platzieren auf das Blatt. " +
        "Ohne nummer wird sie nach dem H+K-Schema vorgeschlagen (Abfrage plannummer_vorschlag: Projektnummer, Fach nach Gewerk, Nummer nach Geschoss; angegeben werden dafür gewerk, geschoss bzw. ansicht und art), ohne titel der Titelvorschlag («Heizung Erdgeschoss»).";
    public string Beschreibung => PlanBefehleTexte.Anlegen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("nummer", "text", false, "Plannummer im Plankopf, im Projekt eindeutig, z. B. 2317-101; fehlt sie, gilt der Vorschlag nach dem H+K-Schema (gewerk, geschoss, ansicht, art)"),
        new("titel", "text", false, "Planinhalt (Titel im Plankopf), z. B. Grundriss Erdgeschoss Sanitär; fehlt er, gilt der Titelvorschlag aus Gewerk und Geschoss"),
        new("format", "text", false, "A4 | A3 | A2 | A1 | A0 (Vorgabe A3)"),
        new("hochformat", "bool", false, "true = Hochformat, sonst quer"),
        new("gewerk", "text", false, "Gewerk des Plans (heizung (auch Kälte) | lueftung | sanitaer | elektro | koordination | allgemein): setzt Planblatt.gewerk und bestimmt den Fachbuchstaben des Vorschlags"),
        new("geschoss", "text", false, "Geschoss (Kennzeichen) für den Vorschlag von Nummer und Titel"),
        new("ansicht", "text", false, "Ansicht, aus der Geschoss, Gewerk und Art des Vorschlags kommen (die Ansicht wird nicht platziert)"),
        new("art", "text", false, PlanAbfragen.ParArt),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var nummer = Param.Text(p, "nummer")?.Trim() ?? "";
        var titel = Param.Text(p, "titel")?.Trim() ?? "";
        var gewerk = Param.Text(p, "gewerk") is { Length: > 0 } gt ? PlanGewerk.Hole(gt) : null;
        KaelteAlias.HinweisFuerBefehl(k, Param.Text(p, "gewerk"));
        string? vorschlagText = null;
        if (nummer.Length == 0 || titel.Length == 0)
        {
            var v = Plannummernvorschlag.Berechnen(k.Projekt, gewerk?.Schluessel, Param.Text(p, "geschoss"), Param.Text(p, "ansicht"), Param.Text(p, "art"));
            if (nummer.Length == 0)
            {
                nummer = v.Nummer ?? throw new HlksFehler("Die Plannummer fehlt und kann nicht vorgeschlagen werden: " + v.Begruendung, "Eine Plannummer angeben, z. B. 1-0768 H03, oder gewerk und geschoss bzw. ansicht angeben (Abfrage plannummer_vorschlag).");
                vorschlagText = $"Plannummer {nummer} vorgeschlagen ({v.Begruendung})";
                k.Hinweise.AddRange(v.Hinweise);
            }
            if (titel.Length == 0)
                titel = v.Titel ?? throw new HlksFehler("Der Titel fehlt und kann nicht vorgeschlagen werden.", "Einen Titel angeben, z. B. «Heizung Erdgeschoss», oder gewerk und geschoss angeben.");
        }
        if (k.Arbeit.DerKlasse("Planblatt").FirstOrDefault(b => string.Equals(b.Text("nummer"), nummer, StringComparison.OrdinalIgnoreCase)) is { } doppelt)
            throw new HlksFehler($"Die Plannummer «{nummer}» hat schon {doppelt.Kennzeichen}.", "Eine andere Plannummer wählen oder das bestehende Blatt mit element_aendern ändern.");

        var eig = new JsonObject
        {
            ["nummer"] = nummer,
            ["titel"] = titel,
            ["format"] = Param.Text(p, "format") ?? Blattformate.Vorgabe,
            ["hochformat"] = Param.Bool(p, "hochformat"),
        };
        if (gewerk is not null) eig["gewerk"] = gewerk.Schluessel;
        var anlegen = new JsonObject { ["klasse"] = "Planblatt", ["eigenschaften"] = eig };
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var blatt = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        var (b, h) = Blattformate.Groesse(blatt.Text("format"), Planausgabe.Hoch(blatt));
        k.Hinweise.Add($"Planblatt {blatt.Kennzeichen} (Nr. {nummer}) angelegt: {blatt.Text("format")} {(Planausgabe.Hoch(blatt) ? "hoch" : "quer")}, {b:0} × {h:0} mm.");
        if (vorschlagText is not null) k.Hinweise.Add(vorschlagText + ".");
    }
}

public sealed class AnsichtPlatzieren : IBefehl
{
    public string Name => "ansicht_platzieren";
    public const string Text = "Setzt eine Ansicht (Grundriss, Schnitt, Ansicht, Schema oder Liste) als Ansichtsfenster auf ein Planblatt. Eine Ansicht steht nur auf einem Blatt. Massstab und Ausschnitt kommen von der Ansicht; ein Schema hat keinen Massstab (Papier-mm, 1:1) und zeigt darum im Titel und im Plankopf keinen. Eine Liste (Raumliste, Strangliste, Mengen, Materialliste, Kollisionen, Aussparungen, Sanitär-Zusammenstellung …) wird im PDF als Tabelle gezeichnet (Papier-mm, ohne Massstab). Passt sie nicht in die Höhe des freien Bereichs, geht sie in Blöcken rechts daneben weiter (umbruch = spalten, Vorgabe; Kopfzeile in jedem Block, Summen nur am Ende); reicht auch die Blattbreite nicht, meldet der Befund P28 «Liste passt nicht aufs Blatt» mit Vorschlag. umbruch = aus: eine Spalte mit höchstens 45 Zeilen (Befund P28, wenn sie nicht passt).";
    public string Beschreibung => Text;
    public const string ParAnsicht = "Ansicht (Kennzeichen oder Id): Grundriss, Schnitt, Ansicht, Schema oder Liste";
    public const string ParUmbruch = "Nur für Listen: spalten (Vorgabe) = passt die Liste nicht in die Höhe, geht sie in Blöcken rechts daneben weiter | aus = eine Spalte mit höchstens 45 Zeilen. Eigenschaft umbruch des Ansichtsfensters";
    public const string ParSchrift = "Nur für Listen: Schriftgrösse der Tabelle in Papier-mm (Vorgabe 2.2; 1.5 bis 4). Eigenschaft schrift des Ansichtsfensters";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("planblatt", "text", true, "Planblatt (Kennzeichen, Id oder Plannummer)"),
        new("ansicht", "text", true, ParAnsicht),
        new("lage", "liste", false, "[x, y] Mitte des Fensters auf dem Blatt in Papier-mm, Ursprung unten links; fehlt sie, kommt die Ansicht in die Mitte des freien Bereichs"),
        new("umbruch", "text", false, ParUmbruch),
        new("schrift", "zahl", false, ParSchrift),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var blatt = Planausgabe.Blatt(k.Arbeit, Param.Text(p, "planblatt", true));
        var ansicht = k.Arbeit.HoleDerKlasse(Param.Text(p, "ansicht", true), "Ansicht");

        var art = ansicht.Text("art");
        if (art is not ("grundriss" or "schnitt" or "ansicht" or "schema" or "liste"))
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} ist eine {art}-Ansicht; auf Planblättern stehen Grundrisse, Schnitte, Ansichten, Schemas und Listen.",
                "Eine Ansicht der Art grundriss, schnitt, ansicht, schema oder liste wählen (projekt_ueberblick zeigt die Ansichten).");

        if (k.Arbeit.DerKlasse("Ansichtsfenster").FirstOrDefault(f => f.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id) is { } vorhanden)
        {
            var wo = k.Arbeit.Finde(vorhanden.TeilVon)?.Kennzeichen ?? "einem Planblatt";
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} steht schon auf {wo} (Ansichtsfenster {vorhanden.Kennzeichen}); eine Ansicht steht nur auf einem Blatt.",
                $"Das Fenster verschieben (element_aendern {vorhanden.Kennzeichen} mit geometrie), mit element_loeschen entfernen und neu setzen, oder eine zweite Ansicht anlegen (element_anlegen, Klasse Ansicht, mit ansichtsvorlage {ansicht.Kennzeichen}).");
        }

        // Optionen der Liste (Modul 7 Teil B3): gelten nur für Listen
        var umbruch = Param.Text(p, "umbruch")?.Trim();
        var schrift = Param.Zahl(p, "schrift");
        if ((umbruch is { Length: > 0 } || schrift is not null) && art != "liste")
            throw new HlksFehler($"umbruch und schrift gelten nur für Listen; {ansicht.Kennzeichen} ist eine {art}-Ansicht.", "umbruch und schrift weglassen.");
        if (umbruch is { Length: > 0 } && !umbruch.Equals(Listentabelle.UmbruchSpalten, StringComparison.OrdinalIgnoreCase) && !umbruch.Equals(Listentabelle.UmbruchAus, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Umbruch «{umbruch}» gibt es nicht.", "Erlaubt: spalten (Vorgabe), aus.");
        if (schrift is { } sg && (!double.IsFinite(sg) || sg < Listentabelle.SchriftMin || sg > Listentabelle.SchriftMax))
            throw new HlksFehler($"Die Schriftgrösse {sg:0.##} mm ist nicht möglich.", $"Zwischen {Listentabelle.SchriftMin:0.#} und {Listentabelle.SchriftMax:0.#} mm wählen (Vorgabe {Listentabelle.SchriftVorgabe:0.#}).");
        var platz = art == "liste" ? new Listentabelle.Platzierung(blatt, Listentabelle.Umbruchwert(umbruch), Listentabelle.SchriftWert(schrift)) : null;

        var (bw, bh) = Blattformate.Groesse(blatt.Text("format"), Planausgabe.Hoch(blatt));
        double[] lage;
        if (p["lage"] is null) lage = Planausgabe.FreieLage(k.Projekt, k.Arbeit, blatt, ansicht, k.Hinweise, platz);
        else
        {
            if (p["lage"] is not JsonArray a || a.Count != 2 || Json.Zahl(a[0]) is not double x || Json.Zahl(a[1]) is not double y)
                throw new HlksFehler("Parameter «lage» muss [x, y] in Papier-mm sein.", "Beispiel: [210, 150] – Mitte des Fensters, Ursprung unten links.");
            if (x < 0 || y < 0 || x > bw || y > bh)
                throw new HlksFehler($"Die Lage [{x:0.#}, {y:0.#}] liegt ausserhalb des Blatts {blatt.Kennzeichen} ({blatt.Text("format")}, {bw:0} × {bh:0} mm).",
                    $"x zwischen 0 und {bw:0}, y zwischen 0 und {bh:0} wählen – oder lage weglassen.");
            lage = [x, y];
        }

        var fensterPar = new JsonObject
        {
            ["klasse"] = "Ansichtsfenster",
            ["teilVon"] = blatt.Id,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(lage[0], lage[1])) },
            ["bezuege"] = new JsonObject { ["ansicht"] = ansicht.Id },
        };
        var eig = new JsonObject();
        if (umbruch is { Length: > 0 }) eig["umbruch"] = Listentabelle.Umbruchwert(umbruch);
        if (schrift is not null) eig["schrift"] = schrift;
        if (eig.Count > 0) fensterPar["eigenschaften"] = eig;
        new ElementAnlegen().Ausfuehren(k, fensterPar);
        var fenster = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        k.Hinweise.Add($"Ansichtsfenster {fenster.Kennzeichen}: {ansicht.Kennzeichen} auf {blatt.Kennzeichen} bei [{lage[0]:0.#}, {lage[1]:0.#}] mm.");
    }
}
