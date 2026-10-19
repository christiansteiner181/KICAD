using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, Nachtrag B3d, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im
/// Repo; fehlen sie, kehrt der Test sofort zurück): H+K-IFC (Revit) übernehmen – die verknüpfte Revit-Architektur wird Hintergrund
/// (Netzdatei Fassung 3 mit Material und Typname). Vorgaben FIKTIV wie AussparungEchtTests (Spiel 20, Zusammenfassen 100 mm), damit die
/// Zahlen mit B3c vergleichbar sind.
///   1. Prüflauf mit hlks-architektur, hlks-vorwand, hlks-hlks (zählt die Kollisionen mit Installationsvorwänden), Vorschlag ohne Typenliste.
///   2. Typenliste nach Christians Entscheid vom 19.10.2026 über typmaterial_setzen (zuerst Trockenlauf): «2.3_IW 1.5/8/1.5 cm 110» =
///      leichtbau, «_IW GIS 1.8/3.2 cm 50» = installationsvorwand.
///   3. Vorschlag und Prüflauf mit der Vorgabe (ohne hlks-vorwand) – wie viele Aussparungen bleiben, wie viele «Material unbekannt»,
///      welche unbekannten Typen übrig sind (Top 10) und wie viele Kollisionen mit der Vorwand wegfallen.
/// Zahlen nach %TEMP%\HLKS_CAD\b3d_typmaterial_echt.json.</summary>
public sealed class TypmaterialEchtTests(ITestOutputHelper aus)
{
    const string T110 = "2.3_IW 1.5/8/1.5 cm 110";
    const string TGis = "_IW GIS 1.8/3.2 cm 50";
    const string Christian = "Entscheid Christian 19.10.2026 (entwurf/modul-3b.md, Entscheide Christian 19.10.2026 Material)";

    static bool Unbekannt(Element a) => a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue v && v.GetValue<bool>();

    static int P20(Projekt p) => Pruefungen.Alle(p.Modell, p.Katalog).Count(b => b.Code == "P20");

    static JsonArray Oben(JsonObject typmaterial, int n = 10) => new(typmaterial["unbekannt"]!.AsArray().Take(n).Select(z => (JsonNode)new JsonObject
    {
        ["schluessel"] = z!["schluessel"]!.GetValue<string>(), ["bauteile"] = z["bauteile"]!.GetValue<int>(),
        ["aussparungen"] = z["aussparungen"]!.GetValue<int>(), ["durchgaenge"] = z["durchgaenge"]!.GetValue<int>(),
        ["entitaeten"] = z["entitaeten"]!.GetValue<string>(), ["material"] = z["material"]!.GetValue<string>(),
    }).ToArray());

    [Fact]
    public void Bernstrasse42_Typenliste_ZweiTypen_Aussparungen_UnbekannteTypen_Vorwandkollisionen()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Datei Bernstrasse 42 fehlt auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        try
        {
            var uhr = Stopwatch.StartNew();
            Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
            double sImport = uhr.Elapsed.TotalSeconds;
            AussparungHilfe.Vorgaben(p);
            var ul = p.Modell.DerKlasse("Unterlage").Single();

            // ---- 1. ohne Typenliste: Prüflauf mit dem Paar hlks-vorwand (zählt die Vorwand-Kollisionen), Vorschlag
            uhr.Restart();
            var l1 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject
            {
                ["unterlagen"] = new JsonArray(ul.Kennzeichen), ["paare"] = new JsonArray("hlks-architektur", "hlks-vorwand", "hlks-hlks"),
            }));
            double sLauf1 = uhr.Elapsed.TotalSeconds;
            var liste1 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            int vorwand1 = liste1["zusammenfassung"]!["jePaar"]!["hlks-vorwand"]?.GetValue<int>() ?? 0;
            int p20Lauf1 = P20(p);
            uhr.Restart();
            var v1 = Ok(AussparungHilfe.Vorschlagen(p));
            double sVorschlag1 = uhr.Elapsed.TotalSeconds;
            var a1 = AussparungHilfe.Aussparungen(p);
            int p20Vorschlag1 = P20(p);
            uhr.Restart();
            var tm1 = (JsonObject)Tuer.Abfrage(p, "typmaterial");
            double sAbfrage = uhr.Elapsed.TotalSeconds;

            // ---- 2. Typenliste nach Christians Entscheid (zuerst Trockenlauf)
            var typen = new JsonArray(
                new JsonObject { ["typname"] = T110, ["kategorie"] = "leichtbau" },
                new JsonObject { ["typname"] = TGis, ["kategorie"] = "installationsvorwand" });
            var tl = Ok(p.Ausfuehren("typmaterial_setzen", new JsonObject { ["typen"] = typen.DeepClone(), ["quelle"] = Christian }, true));
            Assert.Empty(p.Modell.DerKlasse("Typmaterial"));
            var gesetzt = Ok(p.Ausfuehren("typmaterial_setzen", new JsonObject { ["typen"] = typen.DeepClone(), ["quelle"] = Christian }));
            Assert.Equal(2, p.Modell.DerKlasse("Typmaterial").Count());

            // ---- 3. Vorschlag und Prüflauf mit der Vorgabe
            uhr.Restart();
            var v2 = Ok(AussparungHilfe.Vorschlagen(p));
            double sVorschlag2 = uhr.Elapsed.TotalSeconds;
            var a2 = AussparungHilfe.Aussparungen(p);
            uhr.Restart();
            var l2 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["unterlagen"] = new JsonArray(ul.Kennzeichen) }));
            double sLauf2 = uhr.Elapsed.TotalSeconds;
            var liste2 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            int p20Lauf2 = P20(p);
            var tm2 = (JsonObject)Tuer.Abfrage(p, "typmaterial");
            string? Hinweis(BefehlsErgebnis e, string teil) => e.Hinweise.FirstOrDefault(h => h.Contains(teil));
            int Zahl(string? text, string muster) =>
                text is not null && System.Text.RegularExpressions.Regex.Match(text, muster) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;
            int ausserhalbVorwand = Zahl(Hinweis(l2, "mit Installationsvorwänden"), @"Davon (\d+) mit Installationsvorwänden");
            var t110 = tm2["typen"]!.AsArray().Single(x => x!["typname"]!.GetValue<string>() == T110)!;
            var tGis = tm2["typen"]!.AsArray().Single(x => x!["typname"]!.GetValue<string>() == TGis)!;
            var kol110 = BcfFormat.Kollisionen(p.Modell, p.Katalog).Where(k => k.Text("material")?.Contains(T110) == true).ToList();

            JsonObject Typzeile(JsonNode t) => new()
            {
                ["element"] = t["element"]!.GetValue<string>(), ["kategorie"] = t["kategorie"]!.GetValue<string>(), ["bauteile"] = t["bauteile"]!.GetValue<int>(),
                ["aussparungen"] = t["aussparungen"]!.GetValue<int>(), ["durchgaenge"] = t["durchgaenge"]!.GetValue<int>(),
                ["typnamen"] = t["typnamen"]!.DeepClone(), ["entitaeten"] = t["entitaeten"]!.GetValue<string>(),
            };
            var zahlen = new JsonObject
            {
                ["vergleichB3c"] = "B3c (19.10.2026, ohne Typname-Entscheid und ohne Typenliste): 112 Aussparungen, 86 «Material unbekannt»",
                ["ohneTypenliste"] = new JsonObject
                {
                    ["aussparungen"] = a1.Count, ["materialUnbekannt"] = a1.Count(Unbekannt),
                    ["kollisionen"] = liste1["anzahl"]!.GetValue<int>(), ["jePaar"] = liste1["zusammenfassung"]!["jePaar"]!.DeepClone(),
                    ["kollisionenMitVorwand"] = vorwand1, ["p20NachLauf"] = p20Lauf1, ["p20NachVorschlag"] = p20Vorschlag1,
                    ["hinweisVorschlag"] = v1.Hinweise.FirstOrDefault(), ["hinweisMaterial"] = v1.Hinweise.ElementAtOrDefault(1),
                    ["unbekannteTypen"] = tm1["zusammenfassung"]!["unbekannteTypen"]!.DeepClone(), ["unbekannteTypenTop10"] = Oben(tm1),
                    ["ausTypname"] = new JsonArray(tm1["ausTypname"]!.AsArray().Take(12).Select(z => (JsonNode)JsonValue.Create(
                        $"{z!["schluessel"]!.GetValue<string>()} → {z["gruppe"]!.GetValue<string>()} ({z["bauteile"]!.GetValue<int>()} Bauteile, " +
                        $"{z["aussparungen"]!.GetValue<int>()} Aussparungen, {z["durchgaenge"]!.GetValue<int>()} Durchgänge)")!).ToArray()),
                    ["zusammenfassung"] = tm1["zusammenfassung"]!.DeepClone(),
                },
                ["typmaterialSetzen"] = new JsonObject
                {
                    ["trockenlaufAenderungen"] = tl.Aenderungen.Count, ["hinweise"] = new JsonArray(gesetzt.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
                },
                ["mitTypenliste"] = new JsonObject
                {
                    ["aussparungen"] = a2.Count, ["materialUnbekannt"] = a2.Count(Unbekannt),
                    ["hinweisVorschlag"] = v2.Hinweise.FirstOrDefault(), ["hinweisMaterial"] = v2.Hinweise.ElementAtOrDefault(1),
                    ["vorschlaegeGeloeschtKeinRohbau"] = Zahl(v2.Hinweise.FirstOrDefault(), @"(\d+) Vorschlag/Vorschläge darin gelöscht"),
                    ["kollisionenListe"] = liste2["anzahl"]!.GetValue<int>(), ["jePaar"] = liste2["zusammenfassung"]!["jePaar"]!.DeepClone(),
                    ["jeStatus"] = liste2["zusammenfassung"]!["jeStatus"]!.DeepClone(),
                    ["hinweisLauf"] = Hinweis(l2, "Prüflauf"), ["hinweisStatus"] = Hinweis(l2, "Status:"),
                    ["vorwandAusserhalbUnveraendert"] = ausserhalbVorwand, ["hinweisVorwand"] = Hinweis(l2, "Installationsvorwänden"),
                    ["hinweisTypenliste"] = Hinweis(l2, "Typenliste ("), ["materialNachgefuehrt"] = Hinweis(l2, "Material und rohbau nachgeführt"),
                    ["kollisionenLeichtbau110"] = kol110.Count, ["kollisionenLeichtbau110Rohbau"] = string.Join(", ", kol110.GroupBy(k => k.Text("rohbau")).Select(g => $"{g.Key} {g.Count()}")),
                    ["p20"] = p20Lauf2,
                    ["typ110"] = Typzeile(t110), ["typGis"] = Typzeile(tGis),
                    ["unbekannteTypen"] = tm2["zusammenfassung"]!["unbekannteTypen"]!.DeepClone(), ["unbekannteTypenTop10"] = Oben(tm2),
                    ["zusammenfassung"] = tm2["zusammenfassung"]!.DeepClone(),
                },
                ["sekunden"] = new JsonObject
                {
                    ["import"] = Math.Round(sImport, 1), ["lauf1"] = Math.Round(sLauf1, 1), ["vorschlag1"] = Math.Round(sVorschlag1, 1),
                    ["abfrageTypmaterial"] = Math.Round(sAbfrage, 2), ["vorschlag2"] = Math.Round(sVorschlag2, 1), ["lauf2"] = Math.Round(sLauf2, 1),
                },
            };
            var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b3d_typmaterial_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));

            // beide Typen kommen vor; danach keine Aussparung mehr in ihren Bauteilen
            Assert.True(t110["bauteile"]!.GetValue<int>() > 0, "«2.3_IW 1.5/8/1.5 cm 110» nicht gefunden");
            Assert.True(tGis["bauteile"]!.GetValue<int>() > 0, "«_IW GIS 1.8/3.2 cm 50» nicht gefunden");
            Assert.Equal(0, t110["aussparungen"]!.GetValue<int>());
            Assert.Equal(a1.Count - a2.Count, Zahl(v2.Hinweise.FirstOrDefault(), @"(\d+) Vorschlag/Vorschläge darin gelöscht"));   // nur die im Leichtbau
            Assert.Equal(0, tGis["aussparungen"]!.GetValue<int>());
            Assert.True(a2.Count(Unbekannt) < a1.Count(Unbekannt) || a1.Count(Unbekannt) == 0, "Material unbekannt nicht weniger");
            // die festgelegten Typen sind nicht mehr unbekannt
            var unbekannt2 = tm2["unbekannt"]!.AsArray().Select(z => z!["schluessel"]!.GetValue<string>()).ToList();
            Assert.DoesNotContain(T110, unbekannt2);
            Assert.DoesNotContain(TGis, unbekannt2);
            // Vorwand: die Kollisionen des ersten Laufs bleiben im zweiten (ohne Paar) unverändert ausserhalb; keine wird neu gemeldet
            Assert.Equal(vorwand1, ausserhalbVorwand);
            Assert.Equal(vorwand1, liste2["zusammenfassung"]!["jePaar"]!["hlks-vorwand"]?.GetValue<int>() ?? 0);
            Assert.All(kol110, k => Assert.Equal("nein", k.Text("rohbau")));          // Leichtbau: Durchdringung ohne P20
            Assert.True(sVorschlag2 < 180, $"vorschlagen {sVorschlag2:0.0} s");
            Assert.True(sAbfrage < 30, $"typmaterial {sAbfrage:0.0} s");
        }
        finally { try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
