using System.Diagnostics;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abnahme Modul 1c (Gebäudehülle): Beispielprojekt mit erzeugten Decken, einem Testaufbau und Raumgrenzen samt
/// Bodenbilanz; dazu die echten Architekten-IFC von BKW und Bernstrasse (Hülle aus IFC, Raumgrenzen, Hüllbauteile,
/// Wärmebrücken-Vorschläge, Zeiten). Läuft nur mit HLKS_ABNAHME=1, schreibt %TEMP%\HLKS_CAD\abnahme1c.json.
/// Alle Kennwerte hier sind **Testwerte** (Quelle «Testwert Abnahme 1c»), keine Materialdaten.</summary>
public class Abnahme1cTests
{
    static bool Aktiv => Environment.GetEnvironmentVariable("HLKS_ABNAHME") == "1";
    static readonly string Bericht = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "abnahme1c.json");
    const string Testquelle = "Testwert Abnahme 1c (kein Materialkennwert)";

    /// <summary>Summen der Raumgrenzen je Angrenzung und Art, Bodenbilanz, offene Kanten, Rechenzeit.</summary>
    static JsonObject Raumgrenzen(Projekt p)
    {
        var sw = Stopwatch.StartNew();
        var r = Hlks.Kern.Raumgrenzen.Rechnen(p.Modell, p.Katalog);
        var s = sw.Elapsed.TotalSeconds;
        var jeAngrenzung = new JsonObject();
        foreach (var g in r.Grenzen.GroupBy(x => x.Angrenzung).OrderBy(g => g.Key))
            jeAngrenzung[g.Key] = Math.Round(g.Sum(x => x.Flaeche), 1);
        var jeArt = new JsonObject();
        foreach (var g in r.Grenzen.GroupBy(x => x.Art).OrderBy(g => g.Key))
            jeArt[g.Key] = Math.Round(g.Sum(x => x.Flaeche), 1);
        double boden = r.Grenzen.Where(x => x.Art == "boden").Sum(x => x.Flaeche), raum = r.Raeume.Sum(x => x.FlaecheM2);
        double seiten = r.Grenzen.Where(x => x.Art is "wand" or "fenster" or "tuer").Sum(x => x.Flaeche);
        double offen = r.Grenzen.Where(x => x.Offen).Sum(x => x.Flaeche);
        var mitU = r.Grenzen.Where(x => x.UA is not null).ToList();
        return new JsonObject
        {
            ["raeume"] = r.Raeume.Count, ["raumgrenzen"] = r.Grenzen.Count, ["sekunden"] = Math.Round(s, 1),
            ["jeAngrenzungM2"] = jeAngrenzung, ["jeArtM2"] = jeArt,
            ["raumflaecheM2"] = Math.Round(raum, 1), ["bodenM2"] = Math.Round(boden, 1),
            ["bodenbilanzAbweichungM2"] = Math.Round(boden - raum, 2),
            ["offenM2"] = Math.Round(offen, 1), ["offenAnteil"] = seiten > 0 ? Math.Round(offen / seiten, 3) : 0,
            ["mitU"] = mitU.Count, ["uaSummeWK"] = Math.Round(mitU.Where(x => x.Angrenzung is "aussen" or "erdreich").Sum(x => x.UA!.Value), 1),
            ["hinweise"] = new JsonArray(r.Hinweise.Take(10).Select(h => (JsonNode)h).ToArray()),
        };
    }

    static int Zahl(Projekt p, string klasse) => p.Modell.DerKlasse(klasse).Count();

    static JsonObject Pruefung(Projekt p) => new()
    {
        ["P12"] = Pruefungen.Alle(p.Modell, p.Katalog).Count(b => b.Code == "P12"),
    };

    static JsonObject Waermebruecken(Projekt p)
    {
        var e = p.Ausfuehren("waermebruecken_vorschlagen", new JsonObject(), trockenlauf: true);
        return new JsonObject { ["angenommen"] = e.Angenommen, ["aenderungen"] = e.Aenderungen.Count, ["hinweis"] = e.Hinweise.FirstOrDefault() };
    }

    [Fact]
    public void Abnahme_Huelle_ZahlenUndDauer()
    {
        if (!Aktiv) return;
        var bericht = new JsonObject { ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm") };

        // ---- Beispielprojekt: Decken erzeugen, Testaufbau, Raumgrenzen
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var geschosse = p.Modell.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote")).ToList();
        var decken = new JsonArray();
        for (int i = 0; i < geschosse.Count; i++)
        {
            var kz = geschosse[i].Kennzeichen;
            if (i == 0) decken.Add(Erzeugen(p, kz, "boden"));
            decken.Add(Erzeugen(p, kz, i == geschosse.Count - 1 ? "dach" : "decke"));
        }
        // Testaufbau mit Testwerten und Quelle – zeigt die Kette Material → Aufbau → U → Raumgrenze
        Ok(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "Testmaterial A", ["werte"] = new JsonObject { ["lambda"] = new JsonObject { ["wert"] = 0.8, ["quelle"] = Testquelle } } }));
        Ok(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "Testmaterial B", ["werte"] = new JsonObject { ["lambda"] = new JsonObject { ["wert"] = 0.04, ["quelle"] = Testquelle } } }));
        Ok(p.Ausfuehren("aufbau_anlegen", new JsonObject
        {
            ["name"] = "Testwand", ["art"] = "wand",
            ["schichten"] = new JsonArray(new JsonObject { ["material"] = "Testmaterial A", ["dicke"] = 200 }, new JsonObject { ["material"] = "Testmaterial B", ["dicke"] = 160 }),
        }));
        Ok(p.Ausfuehren("uebergang_setzen", new JsonObject
        {
            ["quelle"] = Testquelle,
            ["horizontal"] = new JsonObject { ["rsi"] = 0.13, ["rse"] = 0.04 },
            ["aufwaerts"] = new JsonObject { ["rsi"] = 0.10, ["rse"] = 0.04 },
            ["abwaerts"] = new JsonObject { ["rsi"] = 0.17, ["rse"] = 0.04 },
        }));
        var waende = p.Modell.DerKlasse("Wand").Select(w => (JsonNode)w.Kennzeichen).ToArray();
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray(waende), ["aufbau"] = "Testwand" }));
        var aufbau = p.Modell.DerKlasse("Aufbau").Single(a => a.Name == "Testwand");
        bericht["beispielprojekt"] = new JsonObject
        {
            ["decken"] = decken,
            ["testaufbau"] = new JsonObject { ["rWert"] = aufbau.Zahl("rWert"), ["uWert"] = aufbau.Zahl("uWert"), ["uQuelle"] = aufbau.Eigenschaft("uWert")?.Herkunft.Quelle },
            ["raumgrenzen"] = Raumgrenzen(p),
            ["waermebruecken"] = Waermebruecken(p),
            ["pruefung"] = Pruefung(p),
        };

        // ---- Echte Architekten-IFC
        foreach (var (name, rel) in new[] { ("bkwHauptsitz", @"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc"), ("bernstrasse42", @"bernstrasse42\architekt_3d.ifc") })
        {
            if (Echt(rel) is not { } datei) continue;
            var q = Leer();
            var sw = Stopwatch.StartNew();
            var e = Ok(Uebernehmen(q, datei, new JsonObject { ["bereiche"] = new JsonArray("architektur", "raeume"), ["grundlage"] = false }));
            var import = sw.Elapsed.TotalSeconds;
            var hb = (JsonObject)Tuer.Abfrage(q, "huellbauteile", new JsonObject());
            sw.Restart();
            int koerper = Grafik.Koerper(q).Count;
            bericht[name] = new JsonObject
            {
                ["importSekunden"] = Math.Round(import, 1),
                ["huelleHinweis"] = e.Hinweise.FirstOrDefault(h => h.StartsWith("Hülle")),
                ["aufbauten"] = Zahl(q, "Aufbau"), ["materialien"] = Zahl(q, "Material"),
                ["materialienMitLambda"] = q.Modell.DerKlasse("Material").Count(m => m.Zahl("lambda") is not null),
                ["aufbautenMitU"] = q.Modell.DerKlasse("Aufbau").Count(a => a.Zahl("uWert") is not null),
                ["nordwinkel"] = q.Modell.DerKlasse("Projektinformation").FirstOrDefault()?.Zahl("nordwinkel"),
                ["raumgrenzen"] = Raumgrenzen(q),
                ["huellbauteile"] = hb["zeilen"] is JsonArray z ? z.Count : (hb["anzahl"]?.DeepClone() ?? hb.Count),
                ["waermebruecken"] = Waermebruecken(q),
                ["koerper"] = koerper, ["koerperSekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 1),
                ["pruefung"] = Pruefung(q),
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Bericht)!);
        File.WriteAllText(Bericht, bericht.ToJsonString(Json.Eingerueckt));
    }

    static JsonObject Erzeugen(Projekt p, string geschoss, string art)
    {
        var e = p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = geschoss, ["art"] = art, ["dicke"] = 250 });   // Dicke: Testwert
        return new JsonObject
        {
            ["geschoss"] = geschoss, ["art"] = art, ["angenommen"] = e.Angenommen, ["fehler"] = e.Fehler,
            ["hinweis"] = e.Hinweise.FirstOrDefault(),
        };
    }
}
