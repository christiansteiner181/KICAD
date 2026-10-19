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
        PlansatzBefehle.Registrieren(r);             // Serien-PDF: plansatz_setzen, planblatt_gewerk_setzen (PlansatzBefehle.cs)
    }
}

public sealed class PlanblattAnlegen : IBefehl
{
    public string Name => "planblatt_anlegen";
    public string Beschreibung => "Legt ein Planblatt mit H+K-Plankopf an (Format A4 bis A0, Vorgabe quer). Das Kennzeichen (PB-1, PB-2 …) wird gebildet; Ansichten kommen danach mit ansicht_platzieren auf das Blatt.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("nummer", "text", true, "Plannummer im Plankopf, im Projekt eindeutig, z. B. 2317-101"),
        new("titel", "text", true, "Planinhalt (Titel im Plankopf), z. B. Grundriss Erdgeschoss Sanitär"),
        new("format", "text", false, "A4 | A3 | A2 | A1 | A0 (Vorgabe A3)"),
        new("hochformat", "bool", false, "true = Hochformat, sonst quer"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var nummer = Param.Text(p, "nummer", true)!.Trim();
        var titel = Param.Text(p, "titel", true)!.Trim();
        if (nummer.Length == 0) throw new HlksFehler("Die Plannummer ist leer.", "Eine Plannummer angeben, z. B. 2317-101.");
        if (k.Arbeit.DerKlasse("Planblatt").FirstOrDefault(b => string.Equals(b.Text("nummer"), nummer, StringComparison.OrdinalIgnoreCase)) is { } doppelt)
            throw new HlksFehler($"Die Plannummer «{nummer}» hat schon {doppelt.Kennzeichen}.", "Eine andere Plannummer wählen oder das bestehende Blatt mit element_aendern ändern.");

        var anlegen = new JsonObject
        {
            ["klasse"] = "Planblatt",
            ["eigenschaften"] = new JsonObject
            {
                ["nummer"] = nummer,
                ["titel"] = titel,
                ["format"] = Param.Text(p, "format") ?? Blattformate.Vorgabe,
                ["hochformat"] = Param.Bool(p, "hochformat"),
            },
        };
        new ElementAnlegen().Ausfuehren(k, anlegen);
        var blatt = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        var (b, h) = Blattformate.Groesse(blatt.Text("format"), Planausgabe.Hoch(blatt));
        k.Hinweise.Add($"Planblatt {blatt.Kennzeichen} (Nr. {nummer}) angelegt: {blatt.Text("format")} {(Planausgabe.Hoch(blatt) ? "hoch" : "quer")}, {b:0} × {h:0} mm.");
    }
}

public sealed class AnsichtPlatzieren : IBefehl
{
    public string Name => "ansicht_platzieren";
    public string Beschreibung => "Setzt eine Ansicht (Grundriss, Schnitt, Ansicht, Schema) als Ansichtsfenster auf ein Planblatt. Eine Ansicht steht nur auf einem Blatt. Massstab und Ausschnitt kommen von der Ansicht; ein Schema hat keinen Massstab (Papier-mm, 1:1) und zeigt darum im Titel und im Plankopf keinen.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("planblatt", "text", true, "Planblatt (Kennzeichen, Id oder Plannummer)"),
        new("ansicht", "text", true, "Ansicht (Kennzeichen oder Id): Grundriss, Schnitt, Ansicht oder Schema"),
        new("lage", "liste", false, "[x, y] Mitte des Fensters auf dem Blatt in Papier-mm, Ursprung unten links; fehlt sie, kommt die Ansicht in die Mitte des freien Bereichs"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var blatt = Planausgabe.Blatt(k.Arbeit, Param.Text(p, "planblatt", true));
        var ansicht = k.Arbeit.HoleDerKlasse(Param.Text(p, "ansicht", true), "Ansicht");

        var art = ansicht.Text("art");
        if (art is not ("grundriss" or "schnitt" or "ansicht" or "schema"))
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} ist eine {art}-Ansicht; auf Planblättern stehen Grundrisse, Schnitte, Ansichten und Schemas.",
                "Eine Ansicht der Art grundriss, schnitt, ansicht oder schema wählen (projekt_ueberblick zeigt die Ansichten).");

        if (k.Arbeit.DerKlasse("Ansichtsfenster").FirstOrDefault(f => f.Bezuege.GetValueOrDefault("ansicht") == ansicht.Id) is { } vorhanden)
        {
            var wo = k.Arbeit.Finde(vorhanden.TeilVon)?.Kennzeichen ?? "einem Planblatt";
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} steht schon auf {wo} (Ansichtsfenster {vorhanden.Kennzeichen}); eine Ansicht steht nur auf einem Blatt.",
                $"Das Fenster verschieben (element_aendern {vorhanden.Kennzeichen} mit geometrie), mit element_loeschen entfernen und neu setzen, oder eine zweite Ansicht anlegen (element_anlegen, Klasse Ansicht, mit ansichtsvorlage {ansicht.Kennzeichen}).");
        }

        var (bw, bh) = Blattformate.Groesse(blatt.Text("format"), Planausgabe.Hoch(blatt));
        double[] lage;
        if (p["lage"] is null) lage = Planausgabe.FreieLage(k.Projekt, k.Arbeit, blatt, ansicht, k.Hinweise);
        else
        {
            if (p["lage"] is not JsonArray a || a.Count != 2 || Json.Zahl(a[0]) is not double x || Json.Zahl(a[1]) is not double y)
                throw new HlksFehler("Parameter «lage» muss [x, y] in Papier-mm sein.", "Beispiel: [210, 150] – Mitte des Fensters, Ursprung unten links.");
            if (x < 0 || y < 0 || x > bw || y > bh)
                throw new HlksFehler($"Die Lage [{x:0.#}, {y:0.#}] liegt ausserhalb des Blatts {blatt.Kennzeichen} ({blatt.Text("format")}, {bw:0} × {bh:0} mm).",
                    $"x zwischen 0 und {bw:0}, y zwischen 0 und {bh:0} wählen – oder lage weglassen.");
            lage = [x, y];
        }

        new ElementAnlegen().Ausfuehren(k, new JsonObject
        {
            ["klasse"] = "Ansichtsfenster",
            ["teilVon"] = blatt.Id,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(lage[0], lage[1])) },
            ["bezuege"] = new JsonObject { ["ansicht"] = ansicht.Id },
        });
        var fenster = k.Arbeit.Finde(k.Operationen[^1].Id)!;
        k.Hinweise.Add($"Ansichtsfenster {fenster.Kennzeichen}: {ansicht.Kennzeichen} auf {blatt.Kennzeichen} bei [{lage[0]:0.#}, {lage[1]:0.#}] mm.");
    }
}
