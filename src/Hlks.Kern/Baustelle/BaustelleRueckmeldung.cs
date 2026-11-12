// BIM to field Stufe 1, Rückkanal als Datei (entwurf/bim-to-field.md): die Baustellen-Webseite sammelt Meldungen offline und speichert sie als
// JSON-Datei (Format unten); KICAD liest sie (Vorschau) und übernimmt sie auf Befehl des Menschen – nichts kommt automatisch ins Modell.
//
//   Datei   { format: "kicad-baustelle-rueckmeldung", fassung: 1, projektId, projekt, modellPruefsumme, exportErstellt, person, gespeichert,
//             meldungen: [{ id, zeit, art: status | mangel | frage | abweichung, element (Id), kz, status?, text?, mass?: { dx, dy, dz } mm,
//             fotos?: [data:image/jpeg;base64,…] }] }
//   Status      → Eigenschaft montagestatus des Elements (nur Bauteile im Netz), Herkunft Art «baustelle», Akteur = meldende Person, Zeit der Meldung.
//   Mangel, Frage, Abweichung → ProblemMarker (quelle baustelle, art, notiz, beteiligte, benutzer, zeit, kennung = Meldungs-Id, mass, fotos) an der
//               Lage des Elements; ein zweimal eingelesener Marker (gleiche Kennung) wird nicht zweimal angelegt. Fotos als JPEG in
//               <Projekt>\baustelle\fotos\<Kennung>-<n>.jpg (ohne Projektablage und im Trockenlauf nicht geschrieben).
// Eine Abweichung ändert die Geometrie nicht (As-built ist Stufe 3).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class BaustelleRueckmeldung
{
    public const string Format = "kicad-baustelle-rueckmeldung";
    public static readonly IReadOnlyList<string> Arten = ["status", "mangel", "frage", "abweichung"];

    public const string LesenText = "Rückmeldung von der Baustelle (BIM to field) lesen, ohne zu übernehmen: Absender, Exportstand (ob das Modell seither " +
        "geändert wurde), je Meldung Art, Element, Status bzw. Text, Mass, Anzahl Fotos und ob sie schon übernommen ist.";
    public const string UebernehmenText = "Rückmeldung von der Baustelle übernehmen (BIM to field): Status an die Elemente (montagestatus, Herkunft baustelle), " +
        "Mängel, Fragen und Abweichungen als Problem-Marker mit Fotos; schon übernommene Meldungen werden übersprungen. Wahlweise nur die genannten Meldungen. " +
        "Ein Paket, zurücknehmbar (Fotodateien bleiben).";
    public const string ParDatei = "Rückmeldedatei (.json) von der Baustellen-Webseite";
    public const string ParInhalt = "Inhalt der Rückmeldedatei als Objekt (statt datei, z. B. aus der Oberfläche hochgeladen)";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
        [new("rueckmeldung_lesen", LesenText, [new("datei", "text", false, ParDatei), new("inhalt", "objekt", false, ParInhalt)])];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "rueckmeldung_lesen" ? Lesen(p, Laden(p, par)) : null;

    public static void Registrieren(Befehlsregister r) => r.Hinzufuegen(new RueckmeldungUebernehmen());

    /// <summary>Rückmeldung aus «inhalt» oder aus der Datei. Die KI (rueckmeldung_lesen ist für sie erlaubt) nennt Dateien nur unter dem Projektordner und den
    /// Wurzeln aus kataloge/buero.json – geprüft vor dem ersten Dateizugriff (KiPfade, Sicherheitsprüfung 3c B3a/B4; Gesamtprüfung 13.11.2026).</summary>
    public static JsonObject Laden(Projekt p, JsonObject par)
    {
        if (par["inhalt"] is JsonObject o) return o;
        var datei = Param.Text(par, "datei")?.Trim().Trim('"');
        if (string.IsNullOrEmpty(datei)) throw new HlksFehler("Rückmeldung fehlt.", "datei (Pfad der .json) oder inhalt angeben.");
        datei = KiPfade.Pruefen(p, datei, "rueckmeldung_lesen");
        if (!File.Exists(datei)) throw new HlksFehler($"Die Datei «{datei}» gibt es nicht.", "Vollständigen Pfad der Rückmeldedatei angeben.");
        try { return JsonNode.Parse(File.ReadAllText(datei)) as JsonObject ?? throw new HlksFehler("Die Datei ist keine Rückmeldung."); }
        catch (System.Text.Json.JsonException ex) { throw new HlksFehler($"Die Datei ist kein gültiges JSON: {ex.Message}"); }
    }

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;

    /// <summary>Prüft Format und Projekt; Rückgabe: die Meldungen mit ihrem Element (null, wenn nicht gefunden).</summary>
    public static List<(JsonObject Meldung, Element? Element)> Pruefen(Projekt p, JsonObject r)
    {
        if (T(r["format"]) != Format) throw new HlksFehler("Das ist keine Rückmeldung der Baustellen-Webseite.", $"Datei mit format «{Format}» wählen.");
        if (T(r["projektId"]) is { } pid && pid != p.Kopf.Id)
            throw new HlksFehler($"Die Rückmeldung gehört zu einem anderen Projekt ({T(r["projekt"]) ?? pid}).", "Das Projekt öffnen, aus dem die Webseite stammt.");
        var m = p.Modell;
        return (r["meldungen"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(x => (x, m.Finde(T(x["element"])) ?? m.Finde(T(x["kz"])))).ToList();
    }

    static bool Uebernommen(Modell m, JsonObject x, Element? e) =>
        T(x["art"]) == "status" ? e is not null && e.Text("montagestatus") == T(x["status"])
        : m.DerKlasse("ProblemMarker").Any(pm => pm.Text("kennung") == T(x["id"]));

    public static JsonObject Lesen(Projekt p, JsonObject r)
    {
        var liste = Pruefen(p, r);
        var stand = T(r["modellPruefsumme"]);
        return new JsonObject
        {
            ["projekt"] = T(r["projekt"]), ["person"] = T(r["person"]), ["exportErstellt"] = T(r["exportErstellt"]), ["gespeichert"] = T(r["gespeichert"]),
            ["modellGeaendert"] = stand is not null && !p.Stand.Pruefsumme.StartsWith(stand, StringComparison.Ordinal),
            ["meldungen"] = new JsonArray(liste.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = T(t.Meldung["id"]), ["zeit"] = T(t.Meldung["zeit"]), ["art"] = T(t.Meldung["art"]), ["element"] = t.Element?.Kennzeichen ?? T(t.Meldung["kz"]),
                ["gefunden"] = t.Element is not null, ["status"] = T(t.Meldung["status"]), ["text"] = T(t.Meldung["text"]), ["mass"] = t.Meldung["mass"]?.DeepClone(),
                ["fotos"] = (t.Meldung["fotos"] as JsonArray)?.Count ?? 0, ["uebernommen"] = Uebernommen(p.Modell, t.Meldung, t.Element),
            }).ToArray()),
        };
    }

    /// <summary>Lage eines Problem-Markers am Element: Mitte der Punkte (Leitung: Mitte der Achse).</summary>
    public static double[]? Lage(Element e) =>
        e.Geometrie?.Punkte is { Count: > 0 } pk && pk.All(q => q.Length >= 3)
            ? [pk.Average(q => q[0]), pk.Average(q => q[1]), pk.Average(q => q[2])] : null;
}

public sealed class RueckmeldungUebernehmen : IBefehl
{
    public string Name => "rueckmeldung_uebernehmen";
    public string Beschreibung => BaustelleRueckmeldung.UebernehmenText;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("datei", "text", false, BaustelleRueckmeldung.ParDatei),
        new("inhalt", "objekt", false, BaustelleRueckmeldung.ParInhalt),
        new("meldungen", "liste", false, "Nur diese Meldungen (Ids); ohne: alle noch nicht übernommenen"),
    ];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        // Herkunft des Aufrufs, nicht Art des Projekts: eine KI über HTTP meldet sich je Aufruf (X-Hlks-Akteur-Art: ki, Rang 4)
        if (k.Herkunft.Art == "ki") throw new HlksFehler("Rückmeldungen der Baustelle übernimmt der Mensch.", "Den Projektleiter bitten.");
        var r = BaustelleRueckmeldung.Laden(k.Projekt, p);
        var liste = BaustelleRueckmeldung.Pruefen(k.Projekt, r);
        var nur = p["meldungen"] is JsonArray a ? a.Select(x => x?.GetValue<string>()).OfType<string>().ToHashSet() : null;
        var person = T(r["person"]) ?? "Baustelle";
        var m = k.Arbeit;
        int status = 0, marker = 0, fotos = 0, uebersprungen = 0;
        var fehlt = new List<string>();
        foreach (var (x, e0) in liste)
        {
            var id = T(x["id"]) ?? Guid.NewGuid().ToString("N");
            if (nur is not null && !nur.Contains(id)) continue;
            var e = e0 is null ? null : m.Finde(e0.Id);
            if (e is null) { fehlt.Add(T(x["kz"]) ?? id); continue; }
            var art = T(x["art"]);
            var zeit = DateTimeOffset.TryParse(T(x["zeit"]), out var z) ? z : k.Herkunft.Zeit;
            var herkunft = k.Herkunft with { Art = "baustelle", Akteur = person, Zeit = zeit, Quelle = $"Rückmeldung Baustelle {person}, übernommen von {k.Herkunft.Akteur}" };
            if (art == "status")
            {
                var st = T(x["status"]);
                if (st is null || !BaustelleDaten.Status.Contains(st)) throw new HlksFehler($"Meldung {id}: Status «{st}» gibt es nicht.");
                if (!BaustelleDaten.IstBauteil(k.Katalog, e) || e.Text("montagestatus") == st) { uebersprungen++; continue; }
                var neu = e.Kopie();
                neu.Eigenschaften["montagestatus"] = new Wert { Inhalt = st, Herkunft = herkunft };
                k.Setzen(neu);
                status++;
            }
            else if (art is "mangel" or "frage" or "abweichung")
            {
                if (m.DerKlasse("ProblemMarker").Any(pm => pm.Text("kennung") == id)) { uebersprungen++; continue; }
                var dateien = Fotos(k, id, x["fotos"] as JsonArray);
                fotos += dateien.Count;
                var eig = new JsonObject
                {
                    ["status"] = "offen", ["art"] = art, ["notiz"] = T(x["text"]) ?? art, ["beteiligte"] = new JsonArray(JsonValue.Create(e.Id)),
                    ["benutzer"] = person, ["zeit"] = zeit.ToString("o"), ["kennung"] = id, ["quelle"] = "baustelle",
                };
                if (x["mass"] is JsonObject mass) eig["mass"] = mass.DeepClone();
                if (dateien.Count > 0) eig["fotos"] = new JsonArray(dateien.Select(d => (JsonNode)JsonValue.Create(d)).ToArray());
                var anlegen = new JsonObject
                {
                    ["klasse"] = "ProblemMarker", ["name"] = $"{art switch { "mangel" => "Mangel", "frage" => "Frage", _ => "Abweichung" }} {e.Kennzeichen}",
                    ["eigenschaften"] = eig,
                };
                if (e.Geschoss is not null) anlegen["geschoss"] = e.Geschoss;
                if (BaustelleRueckmeldung.Lage(e) is { } lage)
                    anlegen["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(lage[0], lage[1], lage[2])) };
                new ElementAnlegen().Ausfuehren(k, anlegen);
                marker++;
            }
            else throw new HlksFehler($"Meldung {id}: Art «{art}» gibt es nicht.", "Arten: " + string.Join(", ", BaustelleRueckmeldung.Arten));
        }
        k.Hinweise.Add($"Rückmeldung von {person}: {status} Status, {marker} Marker ({fotos} Fotos) übernommen" + (uebersprungen > 0 ? $", {uebersprungen} schon übernommen" : "") + ".");
        if (fehlt.Count > 0) k.Hinweise.Add($"{fehlt.Count} Meldung(en) ohne Element im Modell (gelöscht?): {string.Join(", ", fehlt.Take(10))}.");
    }

    /// <summary>Fotos (data:image/…;base64) als Dateien im Projektordner; Rückgabe: relative Pfade. Ohne Ablage bzw. im Trockenlauf nur gezählt.</summary>
    static List<string> Fotos(Kontext k, string id, JsonArray? liste)
    {
        var aus = new List<string>();
        if (liste is null) return aus;
        var i = 0;
        foreach (var f in liste)
        {
            i++;
            var s = T(f);
            var komma = s?.IndexOf(',') ?? -1;
            if (s is null || !s.StartsWith("data:image/", StringComparison.Ordinal) || komma < 0) continue;
            var endung = s.StartsWith("data:image/png", StringComparison.Ordinal) ? "png" : "jpg";
            var sicher = string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
            var rel = $"baustelle/fotos/{sicher}-{i}.{endung}";
            aus.Add(rel);
            if (k.Trockenlauf || k.Projekt.Speicher is null) continue;
            var pfad = Path.Combine(k.Projekt.Speicher.Ort, "baustelle", "fotos", $"{sicher}-{i}.{endung}");
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.WriteAllBytes(pfad, Convert.FromBase64String(s[(komma + 1)..]));
        }
        return aus;
    }
}
