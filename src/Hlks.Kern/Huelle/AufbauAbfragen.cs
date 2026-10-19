// Abfragen der Gebäudehülle (Modul 1c, B2): aufbau_rechnen, materialien, huelle_pruefen. Sie lesen nur; die Rechnung
// steckt in HuelleRechnung/FensterRechnung. Ergebnis ist JSON für Liste und KI; fehlende Angaben stehen im Klartext.

using System.Text.Json.Nodes;
using static Hlks.Kern.HuelleHilfe;

namespace Hlks.Kern;

public static class HuelleAbfragen
{
    public const string AufbauRechnenText = "Rechnet einen Aufbau durch: Schichten mit Dicke, λ und R je Schicht, Rsi, Rse, R und U (gerechnet oder ausdrücklich) mit Quelle, dazu was für die Rechnung fehlt.";
    public const string MaterialienText = "Materialien des Projekts mit Kennwerten und Quellen (und was fehlt) sowie die Einträge der Bürobibliothek (kataloge/materialien.json).";
    public const string HuellePruefenText = "Prüft die Hülle für Liste und KI: Bauteile ohne Aufbau, Aufbauten ohne U-Wert, Fenster und Türen ohne Kennwerte, Materialien ohne λ, fehlende Rsi/Rse, unpassende Aufbauarten.";

    /// <summary>Definitionen für Abfragen.Liste.</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("aufbau_rechnen", AufbauRechnenText, [new("aufbau", "text", true, "Aufbau (Kennzeichen, Id oder Name)")]),
        new("materialien", MaterialienText, []),
        new("huelle_pruefen", HuellePruefenText, []),
    ];

    const int Grenze = 200;

    static double? Rd(double? x) => x is null ? null : Math.Round(x.Value, 6);
    static JsonArray Arr(IEnumerable<string> s) => new(s.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());

    // ------------------------------------------------------------------ aufbau_rechnen

    public static JsonObject AufbauRechnen(Projekt p, string bezug)
    {
        var a = Hole(p.Modell, p.Katalog, bezug, "Aufbau", "Aufbau");
        var r = HuelleRechnung.Rechnen(p.Modell, a);
        var o = new JsonObject
        {
            ["aufbau"] = a.Kennzeichen, ["name"] = a.Name, ["art"] = r.Art,
            ["waermestrom"] = r.Waermestrom, ["waermestromAusdruecklich"] = r.WaermestromAusdruecklich,
            ["verwendetVon"] = Arr(BauteileMit(p.Modell, a.Id).Select(x => x.Kennzeichen).Order(StringComparer.OrdinalIgnoreCase).Take(Grenze)),
            ["schichten"] = new JsonArray(r.Schichten.Select(s => (JsonNode)new JsonObject
            {
                ["nr"] = s.Nr, ["material"] = s.Material, ["name"] = s.MaterialName, ["dicke_mm"] = s.DickeMm,
                ["lambda"] = s.Lambda, ["lambdaQuelle"] = s.LambdaQuelle, ["r"] = Rd(s.R),
            }).ToArray()),
            ["rsi"] = r.Rsi, ["rse"] = r.Rse, ["uebergangGruppe"] = r.Gruppe, ["uebergangQuelle"] = r.UebergangQuelle,
            ["r"] = Rd(r.R), ["rQuelle"] = r.RQuelle,
            ["u"] = Rd(r.U),
            ["uAusdruecklich"] = r.UAusdruecklich is not null,
            ["uQuelle"] = r.UAusdruecklich is not null ? r.UAusdruecklichQuelle : r.UQuelle,
            ["fehlt"] = Arr(r.Fehlt),
            ["einheiten"] = new JsonObject { ["dicke"] = "mm", ["lambda"] = "W/(m·K)", ["r"] = "m²·K/W", ["rsi"] = "m²·K/W", ["rse"] = "m²·K/W", ["u"] = "W/(m²·K)" },
        };
        if (r.UAusdruecklich is not null && r.UGerechnet is not null)
        {
            o["uGerechnet"] = Rd(r.UGerechnet);
            o["hinweis"] = "Der ausdrückliche U-Wert geht vor; zum Vergleich steht der gerechnete in «uGerechnet».";
        }
        else if (r.UAusdruecklich is not null)
            o["hinweis"] = "Der ausdrückliche U-Wert geht vor." + (r.UAusdruecklichQuelle is null ? " Er hat keine Quelle." : "");
        return o;
    }

    // ------------------------------------------------------------------ materialien

    static readonly string[] Einheitenreihe = ["lambda", "rohdichte", "waermekapazitaet", "emission", "absorption", "mu"];

    static JsonObject WerteVon(Katalog kat, Element m)
    {
        var werte = new JsonObject();
        foreach (var n in Einheitenreihe)
            if (m.Eigenschaft(n) is { } w)
                werte[n] = new JsonObject
                {
                    ["wert"] = w.Inhalt?.DeepClone(), ["einheit"] = w.Einheit, ["quelle"] = Quelle(w),
                    ["herkunft"] = w.Herkunft.Art,
                };
        return werte;
    }

    public static JsonObject Materialien(Projekt p)
    {
        var m = p.Modell;
        var verwendet = AufbautenNachMaterial(m);
        var projekt = m.DerKlasse("Material").OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(x =>
        {
            var fehlt = new List<string>();
            if (x.Eigenschaft("lambda") is null) fehlt.Add("lambda");
            else if (Quelle(x.Eigenschaft("lambda")) is null) fehlt.Add("Quelle von lambda");
            return (JsonNode)new JsonObject
            {
                ["kennzeichen"] = x.Kennzeichen, ["name"] = x.Name, ["kategorie"] = x.Text("kategorie"),
                ["werte"] = WerteVon(p.Katalog, x), ["fehlt"] = Arr(fehlt),
                ["verwendetIn"] = Arr((verwendet.GetValueOrDefault(x.Id) ?? []).Select(a => a.Kennzeichen)),
            };
        }).ToArray();

        var bib = new JsonObject { ["datei"] = MaterialBibliothek.Pfad(p.Katalog) };
        try
        {
            var liste = MaterialBibliothek.Laden(p.Katalog);
            bib["anzahl"] = liste.Count;
            bib["eintraege"] = new JsonArray(liste.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)new JsonObject
            {
                ["name"] = x.Name, ["kategorie"] = x.Kategorie,
                ["werte"] = new JsonObject(x.Werte.Select(w => KeyValuePair.Create(w.Key, (JsonNode?)new JsonObject { ["wert"] = w.Value.Wert, ["quelle"] = w.Value.Quelle }))),
            }).ToArray());
            if (liste.Count == 0)
                bib["hinweis"] = "Die Bürobibliothek ist leer. Kennwerte kommen aus dem IFC, aus Datenblättern oder von Christian, je mit Quelle (material_anlegen).";
        }
        catch (HlksFehler f) { bib["fehler"] = f.Message; bib["vorschlag"] = f.Vorschlag; }
        return new JsonObject { ["projekt"] = new JsonArray(projekt), ["bibliothek"] = bib };
    }

    // ------------------------------------------------------------------ huelle_pruefen

    public static JsonObject HuellePruefen(Projekt p)
    {
        var m = p.Modell; var kat = p.Katalog;
        JsonObject Begrenzt(List<JsonNode> liste) => new() { ["anzahl"] = liste.Count, ["liste"] = new JsonArray(liste.Take(Grenze).ToArray()), ["gekuerzt"] = liste.Count > Grenze };

        // Bauteile ohne Aufbau, Aufbau der falschen Art
        var ohneAufbau = new List<Element>(); var falscheArt = new List<JsonNode>();
        foreach (var b in m.Elemente.Where(e => ErlaubteAufbauArten(kat, e).Length > 0).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var a = b.Bezuege.TryGetValue("aufbau", out var id) ? m.Finde(id) : null;
            if (a is null || !kat.IstArt(a, "Aufbau")) { ohneAufbau.Add(b); continue; }
            if (a.Text("art") is not { } art || !ErlaubteAufbauArten(kat, b).Contains(art))
                falscheArt.Add(new JsonObject { ["bauteil"] = b.Kennzeichen, ["klasse"] = b.Klasse, ["aufbau"] = a.Kennzeichen, ["art"] = a.Text("art"), ["erwartet"] = Arr(ErlaubteAufbauArten(kat, b)) });
        }

        // Aufbauten ohne U, Gruppen der Übergangswiderstände
        var aufbauten = m.DerKlasse("Aufbau").OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var ohneU = new List<JsonNode>(); var gruppen = new SortedSet<string>(StringComparer.Ordinal);
        var ergebnisse = new Dictionary<string, AufbauErgebnis>(StringComparer.Ordinal);
        foreach (var a in aufbauten)
        {
            var r = HuelleRechnung.Rechnen(m, a);
            ergebnisse[a.Id] = r;
            gruppen.Add(r.Gruppe);
            if (r.U is null)
                ohneU.Add(new JsonObject { ["aufbau"] = a.Kennzeichen, ["name"] = a.Name, ["art"] = r.Art, ["rWert"] = Rd(r.R), ["fehlt"] = Arr(r.Fehlt) });
        }
        var uebergang = new List<JsonNode>();
        foreach (var g in gruppen)
        {
            var (rsi, rse, quelle, vorhanden) = HuelleRechnung.Uebergang(m, g);
            string? grund = !vorhanden ? "nicht gesetzt" : rsi is null || rse is null ? "unvollständig" : quelle is null ? "ohne Quelle" : null;
            if (grund is not null)
                uebergang.Add(new JsonObject { ["gruppe"] = g, ["grund"] = grund, ["vorschlag"] = "uebergang_setzen mit rsi, rse und Quelle" });
        }

        // Fenster und Türen
        var fensterOhne = new List<JsonNode>(); var tuerenOhne = new List<JsonNode>();
        foreach (var f in m.Elemente.Where(e => kat.IstArt(e, "Fenster")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var r = FensterRechnung.Rechnen(f);
            var fehlt = (r.Uw is null ? new List<string>(r.Fehlt) : new List<string>());
            if (f.Eigenschaft("g") is null) fehlt.Add("g fehlt (fenster_kennwerte).");
            else if (Quelle(f.Eigenschaft("g")) is null) fehlt.Add("g hat keine Quelle.");
            if (fehlt.Count > 0)
                fensterOhne.Add(new JsonObject { ["fenster"] = f.Kennzeichen, ["uw"] = Rd(r.Uw), ["fehlt"] = Arr(fehlt) });
        }
        foreach (var t in m.Elemente.Where(e => kat.IstArt(e, "Tuer")).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (t.Eigenschaft("ud")?.AlsZahl() is not null) continue;
            var a = t.Bezuege.TryGetValue("aufbau", out var id) ? m.Finde(id) : null;
            if (a is not null && ergebnisse.TryGetValue(a.Id, out var r) && r.U is not null) continue;
            tuerenOhne.Add(new JsonObject { ["tuer"] = t.Kennzeichen, ["fehlt"] = Arr([a is null ? "ud oder ein Aufbau mit U-Wert (aufbau_zuweisen, fenster_kennwerte)" : $"ud, oder U-Wert von {a.Kennzeichen} (siehe aufbautenOhneU)"]) });
        }

        // Materialien ohne λ, soweit Aufbauten sie brauchen
        var verwendet = AufbautenNachMaterial(m).Keys.ToHashSet(StringComparer.Ordinal);
        var matOhne = m.DerKlasse("Material").Where(x => verwendet.Contains(x.Id) && (x.Zahl("lambda") is null || Quelle(x.Eigenschaft("lambda")) is null))
            .OrderBy(x => x.Kennzeichen, StringComparer.OrdinalIgnoreCase)
            .Select(x => (JsonNode)new JsonObject { ["material"] = x.Kennzeichen, ["name"] = x.Name, ["fehlt"] = x.Zahl("lambda") is null ? "lambda" : "Quelle von lambda" }).ToList();

        var nachKlasse = new JsonObject(ohneAufbau.GroupBy(x => x.Klasse).OrderBy(g => g.Key).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
        int offen = ohneAufbau.Count + ohneU.Count + fensterOhne.Count + tuerenOhne.Count + matOhne.Count + uebergang.Count + falscheArt.Count;
        return new JsonObject
        {
            ["zusammenfassung"] = offen == 0
                ? $"Hülle vollständig: {aufbauten.Count} Aufbauten, alle Bauteile mit Aufbau, alle U-Werte vorhanden."
                : $"Offen: {ohneAufbau.Count} Bauteile ohne Aufbau, {ohneU.Count} Aufbauten ohne U-Wert, {fensterOhne.Count} Fenster und {tuerenOhne.Count} Türen ohne Kennwerte, {matOhne.Count} Materialien ohne λ, {uebergang.Count} Gruppen Rsi/Rse fehlen, {falscheArt.Count} Aufbauten der falschen Art.",
            ["bauteileOhneAufbau"] = new JsonObject
            {
                ["anzahl"] = ohneAufbau.Count, ["nachKlasse"] = nachKlasse,
                ["liste"] = Arr(ohneAufbau.Take(Grenze).Select(x => x.Kennzeichen)), ["gekuerzt"] = ohneAufbau.Count > Grenze,
            },
            ["aufbautenOhneU"] = Begrenzt(ohneU),
            ["uebergangFehlt"] = Begrenzt(uebergang),
            ["fensterOhneKennwerte"] = Begrenzt(fensterOhne),
            ["tuerenOhneKennwerte"] = Begrenzt(tuerenOhne),
            ["materialienOhneLambda"] = Begrenzt(matOhne),
            ["aufbauUnpassendeArt"] = Begrenzt(falscheArt),
        };
    }
}
