using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo;
/// fehlen sie, kehrt der Test sofort zurück): H+K-IFC (Revit) mit Netzdatei übernehmen – die HLKS-Elemente werden Elemente, die
/// verknüpfte Revit-Architektur (deckungsgleich) wird Hintergrund. Prüflauf kollision_pruefen HLKS ↔ Architektur und HLKS ↔ HLKS
/// über die Tür, ohne Projektvorgaben (Christians Werte fehlen noch: keine weiche Prüfung, jede Durchdringung). Zahlen je Art, Typ
/// und Paar, Zeiten; zweiter Lauf ohne Änderung: alle Status bleiben, keine neue, keine behoben; ein von Hand gesetzter Status bleibt;
/// B2b: der zweite Lauf schreibt höchstens die Projektinformation (pruefung nennt weiter Lauf 1).
/// Zeitgrenzen grosszügig (parallele Bauläufe). Zahlen nach %TEMP%\HLKS_CAD\b2_kollision_echt.json.</summary>
public sealed class KollisionspruefungEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_HlksGegenRevitArchitektur_ZweiLaeufe_StatusBleiben()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Dateien Bernstrasse 42 fehlen auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        try
        {
            var uhr = Stopwatch.StartNew();
            Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
            double sUebertrag = uhr.Elapsed.TotalSeconds;
            var ul = p.Modell.DerKlasse("Unterlage").Single();
            var par = new JsonObject { ["unterlagen"] = new JsonArray(ul.Kennzeichen) };

            // Lauf 1
            uhr.Restart();
            var l1 = Ok(p.Ausfuehren("kollision_pruefen", par.DeepClone().AsObject()));
            double s1 = uhr.Elapsed.TotalSeconds;
            var liste1 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            var status1 = BcfFormat.Kollisionen(p.Modell, p.Katalog).ToDictionary(e => e.Id, e => (e.Text("status"), e.Text("kennung")));
            int n1 = status1.Count;
            // drei von Hand bestätigen (bleibt im zweiten Lauf)
            var bestaetigt = BcfFormat.Kollisionen(p.Modell, p.Katalog).Take(3).Select(e => e.Kennzeichen).ToArray();
            if (bestaetigt.Length > 0)
                Ok(p.Ausfuehren("kollision_status", new JsonObject { ["kollisionen"] = new JsonArray(bestaetigt.Select(x => (JsonNode)x).ToArray()), ["status"] = "bestaetigt" }));

            // Lauf 2 ohne Änderung
            uhr.Restart();
            var l2 = Ok(p.Ausfuehren("kollision_pruefen", par.DeepClone().AsObject()));
            double s2 = uhr.Elapsed.TotalSeconds;
            var nach2 = BcfFormat.Kollisionen(p.Modell, p.Katalog).ToList();
            var liste2 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            // B2b: Operationen des zweiten Laufs (vorher ~3400: jede Kollision neu geschrieben)
            var ops2 = l2.PaketId is null ? new List<Operation>() : p.Verlauf.Single(x => x.Id == l2.PaketId).Operationen;
            var ops1 = p.Verlauf.Single(x => x.Id == l1.PaketId).Operationen.Count;

            // Nachtrag B3c: Lauf 3 mit dem Paar hlks-bekleidung (Bekleidungen nur auf Wunsch), danach wieder die Vorgabe – die
            // Kollisionen mit Bekleidungen bleiben dann unverändert (ausserhalb des Laufs)
            uhr.Restart();
            var l3 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject
            {
                ["unterlagen"] = new JsonArray(ul.Kennzeichen), ["paare"] = new JsonArray("hlks-architektur", "hlks-bekleidung", "hlks-hlks"),
            }));
            double s3 = uhr.Elapsed.TotalSeconds;
            var liste3 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            var l4 = Ok(p.Ausfuehren("kollision_pruefen", par.DeepClone().AsObject()));
            var liste4 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            int bekleidung3 = liste3["zusammenfassung"]!["jePaar"]!["hlks-bekleidung"]?.GetValue<int>() ?? 0;

            // Mass-Verteilung der harten (Grundlage für toleranz_hart, Christians Entscheid)
            var hart = nach2.Where(e => e.Text("art") == "hart").Select(e => e.Zahl("mass") ?? 0).ToList();
            var zahlen = new JsonObject
            {
                ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
                ["uebertragSekunden"] = Math.Round(sUebertrag, 1),
                ["lauf1"] = new JsonObject { ["sekunden"] = Math.Round(s1, 1), ["kollisionen"] = n1, ["operationen"] = ops1, ["zusammenfassung"] = liste1["zusammenfassung"]!.DeepClone(), ["hinweise"] = new JsonArray(l1.Hinweise.Select(h => (JsonNode)h).ToArray()) },
                ["lauf2"] = new JsonObject
                {
                    ["sekunden"] = Math.Round(s2, 1), ["kollisionen"] = nach2.Count, ["operationen"] = ops2.Count,
                    ["operationenKlassen"] = string.Join(", ", ops2.GroupBy(o => o.Element?.Klasse ?? o.Art).Select(g => $"{g.Key} {g.Count()}")),
                    ["kollisionslauf"] = liste2["kollisionslauf"]?.DeepClone(),
                    ["zusammenfassung"] = liste2["zusammenfassung"]!.DeepClone(), ["hinweise"] = new JsonArray(l2.Hinweise.Select(h => (JsonNode)h).ToArray()),
                },
                ["lauf3MitBekleidung"] = new JsonObject
                {
                    ["sekunden"] = Math.Round(s3, 1), ["kollisionen"] = liste3["anzahl"]!.GetValue<int>(), ["bekleidung"] = bekleidung3,
                    ["jePaar"] = liste3["zusammenfassung"]!["jePaar"]!.DeepClone(),
                    ["bekleidungJeTyp"] = string.Join(", ", ((JsonArray)liste3["zeilen"]!).Where(z => z!["paar"]!.GetValue<string>() == "hlks-bekleidung")
                        .GroupBy(z => z!["typ"]!.GetValue<string>()).Select(g => $"{g.Key} {g.Count()}")),
                    ["hinweise"] = new JsonArray(l3.Hinweise.Take(6).Select(h => (JsonNode)h).ToArray()),
                },
                ["lauf4Vorgabe"] = new JsonObject
                {
                    ["jePaar"] = liste4["zusammenfassung"]!["jePaar"]!.DeepClone(), ["jeStatus"] = liste4["zusammenfassung"]!["jeStatus"]!.DeepClone(),
                    ["hinweise"] = new JsonArray(l4.Hinweise.Where(h => h.StartsWith("Status:") || h.StartsWith("Davon")).Select(h => (JsonNode)h).ToArray()),
                },
                ["durchdringungRohbau"] = string.Join(", ", ((JsonArray)liste2["zeilen"]!).Where(z => z!["typ"]!.GetValue<string>() == "durchdringung")
                    .GroupBy(z => z!["rohbau"]?.GetValue<string>() ?? "?").Select(g => $"{g.Key} {g.Count()}")),
                ["massHart"] = new JsonObject
                {
                    ["bis1mm"] = hart.Count(x => x <= 1), ["bis5mm"] = hart.Count(x => x > 1 && x <= 5), ["bis20mm"] = hart.Count(x => x > 5 && x <= 20),
                    ["bis100mm"] = hart.Count(x => x > 20 && x <= 100), ["ueber100mm"] = hart.Count(x => x > 100),
                },
                ["durchdringungJeBauteilEntitaet"] = string.Join(", ", ((JsonArray)liste2["zeilen"]!).Where(z => z!["typ"]!.GetValue<string>() == "durchdringung")
                    .GroupBy(z => (z!["bauteil"]?.GetValue<string>() ?? "?").Split(' ')[0]).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                ["beispiele"] = new JsonArray(((JsonArray)liste2["zeilen"]!).Take(5).Select(z => (JsonNode)$"{z!["kollision"]} {z["art"]}/{z["typ"]} {z["mass_mm"]} mm: {z["beteiligte"]}").ToArray()),
                ["rechner"] = new JsonObject { ["prozessoren"] = Environment.ProcessorCount, ["hinweis"] = "Zeiten bei paralleler Last anderer Bauläufe" },
            };
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b2_kollision_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllText(ziel, zahlen.ToJsonString(Json.Eingerueckt));

            // Prüfungen: es gibt Durchdringungen der verknüpften Architektur; Lauf 2 = Lauf 1 (Status bleiben, keine neu, keine behoben)
            Assert.True(n1 > 100, $"Kollisionen {n1}");
            Assert.True(liste1["zusammenfassung"]!["jeTyp"]!["durchdringung"]?.GetValue<int>() > 50, "Durchdringungen");
            Assert.Equal(n1, nach2.Count);
            foreach (var e in nach2)
            {
                var (st, kn) = status1[e.Id];
                Assert.Equal(kn, e.Text("kennung"));
                Assert.Equal(bestaetigt.Contains(e.Kennzeichen) ? "bestaetigt" : st, e.Text("status"));
                Assert.Contains(" · Lauf 1 · ", e.Text("pruefung"));               // B2b: zuerst gefunden in Lauf 1, nicht nachgeführt
                Assert.DoesNotContain("Lauf 2", e.Text("pruefung"));
            }
            // B2b: der zweite Lauf ohne Änderung schreibt höchstens die Projektinformation (Zähler kollisionslauf)
            Assert.True(ops2.Count <= 1, $"Lauf 2: {ops2.Count} Operationen");
            Assert.All(ops2, o => Assert.Equal("Projektinformation", o.Element?.Klasse));
            Assert.Contains(" · Lauf 2 · ", liste2["kollisionslauf"]!.GetValue<string>());
            Assert.Contains("0 neu,", l2.Hinweise.Single(h => h.StartsWith("Status:")));
            Assert.Contains(" 0 nicht mehr gefunden", l2.Hinweise.Single(h => h.StartsWith("Status:")));
            Assert.True(s1 < 600 && s2 < 600, $"Läufe {s1:0.0} s und {s2:0.0} s");
            // Nachtrag B3c: die Vorgabe meldet keine Bekleidung; mit dem Paar kommen sie dazu, ohne es bleiben sie unverändert
            Assert.Null(liste1["zusammenfassung"]!["jePaar"]!["hlks-bekleidung"]);
            Assert.True(bekleidung3 > 0, "keine Kollision mit Bekleidungen im Lauf mit hlks-bekleidung");
            Assert.Equal(bekleidung3, liste4["zusammenfassung"]!["jePaar"]!["hlks-bekleidung"]!.GetValue<int>());
            Assert.DoesNotContain(BcfFormat.Kollisionen(p.Modell, p.Katalog), e => e.Text("status") == "behoben");
            Assert.Contains(l4.Hinweise, h => h.StartsWith($"Davon {bekleidung3} mit Bekleidungen"));
            Assert.DoesNotContain(l1.Hinweise, h => h.Contains("Weiche Prüfung entfällt"));
        }
        finally
        {
            try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
