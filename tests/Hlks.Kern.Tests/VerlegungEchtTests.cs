using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8c, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo;
/// fehlen sie, kehrt der Test sofort zurück): H+K-IFC (Revit) übernehmen – die HLKS-Leitungen werden Elemente, die verknüpfte
/// Architektur bleibt Hintergrund (Netzdatei). Typenliste nach Entscheid Christian 19.10.2026: «5.0_Geschossdecke Tragkonstruktion
/// 16/1.5 cm 175» = beton. Erwartet werden die Einlagen der Pläne Christians S07 «Einlagen D.ü.2.UG» (zwei PE-S 110: eine von der
/// Fassade zum BWA Varino horizontal, eine kurze beim Treppenhaus), S08 «Einlagen Bodenplatte 2.UG» (PE-S 56 zum BWA Varino vertikal),
/// S09 «Einlagen D.ü.1.UG» (PE-S 110/90/63/56, SaniPex KW/WW) und die Grundleitungen aus S06 «Grundleitungen 2.UG».
/// Zahlen und Laufzeiten nach %TEMP%\HLKS_CAD\b8c_verlegung_echt.json.</summary>
public sealed class VerlegungEchtTests(ITestOutputHelper aus)
{
    const string Typ = "5.0_Geschossdecke Tragkonstruktion 16/1.5 cm 175";
    const string Christian = "Christian Steiner, H+K Planungs AG, 19.10.2026";

    [Fact]
    public void Bernstrasse42_Einlagen_Grundleitungen_WieS06BisS09()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Datei Bernstrasse 42 fehlt auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
        Ok(p.Ausfuehren("typmaterial_setzen", new JsonObject
        {
            ["typen"] = new JsonArray(new JsonObject { ["typname"] = Typ, ["kategorie"] = "beton" }), ["quelle"] = Christian,
        }));

        // ---- Laufzeiten: erster Lauf (mit Bauteilkontext), gleicher Stand (Zwischenspeicher), nach einer Änderung an einer Leitung
        var uhr = Stopwatch.StartNew();
        var vs = Verlegung.Von(p);
        double sErster = uhr.Elapsed.TotalSeconds;
        uhr.Restart();
        Assert.Same(vs, Verlegung.Von(p));
        double msZweiter = uhr.Elapsed.TotalMilliseconds;
        var eine = vs.JeElement.Values.Where(b => b.IstEinlage && b.Laenge > 0).OrderBy(b => b.Kennzeichen, Aussparungsbezug.Natuerlich).First();
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = eine.Kennzeichen, ["eigenschaften"] = new JsonObject { ["verlegung"] = new JsonObject { ["wert"] = "frei", ["quelle"] = "Test B8c" } },
        }));
        uhr.Restart();
        var vs3 = Verlegung.Von(p);
        double msNachAenderung = uhr.Elapsed.TotalMilliseconds;
        Assert.Same(vs.Kontext, vs3.Kontext);
        Assert.Equal(("frei", "hand", "einlage"), (vs3.Befund(eine.ElementId)!.Verlegung, vs3.Befund(eine.ElementId)!.Herkunft, vs3.Befund(eine.ElementId)!.Erkannt));
        Assert.True(p.Rueckgaengig().Angenommen);

        // ---- Pläne
        var plaene = vs.Plaene(p.Modell);
        Verlegungsplan Plan(string s) => plaene.SingleOrDefault(x => x.Schluessel == s) ?? throw new Xunit.Sdk.XunitException($"Plan «{s}» fehlt: {string.Join(", ", plaene.Select(x => x.Schluessel))}");
        int Leitungen(Verlegungsplan x) => x.Elemente.Count(b => b.Laenge > 0);
        string Sys(Verlegungsbefund b) => p.Modell.Finde(p.Modell.Finde(b.ElementId)?.System)?.Kennzeichen ?? "?";
        var g = Plan("Grundleitungen 2.UG");
        var bp = Plan("Einlagen Bodenplatte 2.UG");
        var d2 = Plan("Einlagen D.ü.2.UG");
        var d1 = Plan("Einlagen D.ü.1.UG");
        Assert.InRange(Leitungen(g), 30, 60);
        Assert.InRange(g.Laenge / 1000, 30, 50);
        Assert.InRange(Leitungen(bp), 1, 4);
        Assert.InRange(bp.Laenge / 1000, 1.0, 2.5);
        Assert.InRange(Leitungen(d2), 5, 10);
        Assert.InRange(d2.Laenge / 1000, 8, 11);   // S07: lange Leitung ab Fassade ≈ 8.7 m und kurze beim Treppenhaus ≈ 0.6 m
        // im H+K-Modell: die lange Leitung im System S-WASR (Regenabwasser), die kurze in S-WASH
        Assert.All(d2.Elemente.Where(b => b.Laenge > 0), b => Assert.Contains(Sys(b), new[] { "S-WASH", "S-WASR" }));
        Assert.InRange(Leitungen(d1), 60, 120);
        Assert.InRange(d1.Laenge / 1000, 60, 110);
        var d1Systeme = d1.Elemente.Where(b => b.Laenge > 0).GroupBy(Sys).ToDictionary(x => x.Key, x => x.Count());
        foreach (var s in new[] { "S-WASH", "S-KA-red", "S-WW" }) Assert.True(d1Systeme.ContainsKey(s), $"D.ü.1.UG ohne {s}");
        Assert.All(d1.Elemente, b => Assert.Equal(("decke", "1.UG"), (b.Bauteilart, b.GeschossKennzeichen)));
        Assert.True(sErster < 30, $"erster Lauf {sErster:0.0} s");
        Assert.True(msZweiter < 50, $"zweiter Lauf {msZweiter:0} ms");

        // ---- Abfrage und Zahlen
        uhr.Restart();
        var o = (JsonObject)Tuer.Abfrage(p, "verlegung");
        double msAbfrage = uhr.Elapsed.TotalMilliseconds;
        JsonObject Zeile(Verlegungsplan x) => new()
        {
            ["leitungen"] = Leitungen(x), ["formteile"] = x.Elemente.Count(b => b.Laenge == 0), ["laenge_m"] = Math.Round(x.Laenge / 1000, 2),
            ["systeme"] = new JsonObject(x.Elemente.Where(b => b.Laenge > 0).GroupBy(Sys).OrderBy(z => z.Key, StringComparer.Ordinal)
                .Select(z => KeyValuePair.Create(z.Key, (JsonNode?)JsonValue.Create(Math.Round(z.Sum(b => b.Laenge) / 1000, 2))))),
            ["bauteile"] = new JsonArray(x.Elemente.Select(b => b.Typname ?? b.BauteilName).OfType<string>().Distinct().Order(StringComparer.Ordinal).Select(t => (JsonNode)JsonValue.Create(t)).ToArray()),
        };
        var zahlen = new JsonObject
        {
            ["typenliste"] = $"{Typ} = beton ({Christian})",
            ["laufzeit"] = new JsonObject
            {
                ["ersterLauf_s"] = Math.Round(sErster, 2), ["davonBauteilkontext_s"] = Math.Round(vs.KontextMs / 1000.0, 2),
                ["gleicherStand_ms"] = Math.Round(msZweiter, 2), ["nachAenderungEinerLeitung_ms"] = Math.Round(msNachAenderung, 1), ["abfrage_ms"] = Math.Round(msAbfrage, 1),
            },
            ["zaehlung"] = o["zaehlung"]!.DeepClone(),
            ["plaene"] = new JsonObject(plaene.Select(x => KeyValuePair.Create(x.Schluessel, (JsonNode?)Zeile(x)))),
            ["hinweise"] = o["hinweise"]!.DeepClone(),
        };
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b8c_verlegung_echt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
        aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
    }
}
