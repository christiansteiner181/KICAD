using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, Nachtrag B3e, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im
/// Repo; fehlen sie, kehrt der Test sofort zurück). Muster TypmaterialEchtTests (B3d); Vorgaben FIKTIV wie dort (Spiel 20, Zusammenfassen
/// 100 mm), damit die Zahlen vergleichbar sind.
///   1. Prüflauf mit hlks-architektur, hlks-vorwand, hlks-hlks und Vorschlag ohne Typenliste (Wortliste B3e: Unterdecke, Bodenaufbau,
///      Luftschicht).
///   2. Typenliste wie B3d (die zwei Typen vom 19.10.2026) – Vorschlag und Prüflauf mit der Vorgabe: Wirkung der Wortliste B3e allein.
///   3. Typenliste nach Christians Entscheiden zu B3d (19.10.2026, Quelle «Christian Steiner, H+K Planungs AG, 19.10.2026»): dazu die drei
///      UB-Typen = bodenaufbau und die Tragkonstruktion = beton – Vorschlag und Prüflauf mit der Vorgabe.
/// Die Typnamen kommen aus der Abfrage typmaterial (gesucht über ihren Anfang, je genau ein Treffer), nicht abgetippt.
/// Je Stufe: Aussparungen, «Material unbekannt», unbekannte Typen (Top 10), Kollisionen im Vorgabelauf, P20 und P20 je Entität bzw. bei
/// Teilen (IfcBuildingElementPart) je Typname. Zahlen nach %TEMP%\HLKS_CAD\b3e_typmaterial_echt.json.</summary>
public sealed class TypmaterialB3eEchtTests(ITestOutputHelper aus)
{
    const string Christian = "Christian Steiner, H+K Planungs AG, 19.10.2026";

    /// <summary>Typname wie im Auftrag (nur zum Finden und Prüfen – gesetzt wird der Typname aus der Abfrage) und Kategorie nach Christians
    /// Entscheiden; Stufe 2 = schon in B3d. Gesucht wird über den Anfang bis zum ersten Leerschlag nach der Typnummer; unter den Treffern
    /// muss genau einer denselben Schlüssel haben (Bernstrasse: neben «_IW GIS 1.8/3.2 cm 50» gibt es «_IW GIS 1.8/3.2 cm 50 - 50»).</summary>
    static readonly (string Typname, string Kategorie, int Stufe)[] Entscheide =
    [
        ("2.3_IW 1.5/8/1.5 cm 110", "leichtbau", 2),
        ("_IW GIS 1.8/3.2 cm 50", "installationsvorwand", 2),
        ("5.1_Geschossdecke UB 10/3/6/1.5 cm 205", "bodenaufbau", 3),
        ("5.2_UB EG 10/2/5.5/1.5 cm 190", "bodenaufbau", 3),
        ("5.1.1_Geschossdecke UB ohne Dämmung 105", "bodenaufbau", 3),
        ("5.0_Geschossdecke Tragkonstruktion 16/1.5 cm 175", "beton", 3),
    ];

    static bool Unbekannt(Element a) => a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue v && v.GetValue<bool>();

    static JsonArray Oben(JsonObject typmaterial, int n = 10) => new(typmaterial["unbekannt"]!.AsArray().Take(n).Select(z => (JsonNode)new JsonObject
    {
        ["schluessel"] = z!["schluessel"]!.GetValue<string>(), ["bauteile"] = z["bauteile"]!.GetValue<int>(),
        ["aussparungen"] = z["aussparungen"]!.GetValue<int>(), ["durchgaenge"] = z["durchgaenge"]!.GetValue<int>(),
        ["entitaeten"] = z["entitaeten"]!.GetValue<string>(), ["material"] = z["material"]!.GetValue<string>(),
    }).ToArray());

    static string? Hinweis(BefehlsErgebnis e, string teil) => e.Hinweise.FirstOrDefault(h => h.Contains(teil));

    static int Zahl(string? text, string muster) =>
        text is not null && Regex.Match(text, muster) is { Success: true } m ? int.Parse(m.Groups[1].Value) : -1;

    /// <summary>P20 je Entität des Bauteils und – bei Teilen – je Typname (Kern) mit Material und rohbau der Kollision.</summary>
    static JsonObject P20Auswertung(Projekt p, IfcNetzdatei nd)
    {
        var p20 = Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == "P20").ToList();
        var jeEntitaet = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var teile = new Dictionary<string, (int Anzahl, HashSet<string> Material, HashSet<string> Rohbau)>(StringComparer.Ordinal);
        foreach (var b in p20)
        {
            var kz = Regex.Match(b.Text, @"\(Kollision (KOL-\d+)\)").Groups[1].Value;
            var kol = p.Modell.Finde(kz);
            var bt = kol?.Text("bauteil");
            string ent = "?"; string? typ = null;
            if (bt is not null && bt.IndexOf(':') is var i and > 0 && nd.Produkt(Netzkoepfe.Basis(bt[(i + 1)..])) is { } pr) { ent = pr.Entitaet; typ = pr.Typname; }
            else if (bt is not null && p.Modell.Finde(bt) is { } e) ent = e.Klasse;
            jeEntitaet[ent] = jeEntitaet.GetValueOrDefault(ent) + 1;
            if (ent != Pruefkoerperbau.Teilentitaet) continue;
            var s = Typenliste.Kern(typ ?? "(ohne Typname)");
            if (!teile.TryGetValue(s, out var t)) teile[s] = t = (0, [], []);
            t.Material.Add(kol!.Text("material") ?? "?"); t.Rohbau.Add(kol.Text("rohbau") ?? "?");
            teile[s] = (t.Anzahl + 1, t.Material, t.Rohbau);
        }
        return new JsonObject
        {
            ["p20"] = p20.Count,
            ["jeEntitaet"] = new JsonObject(jeEntitaet.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
            ["teileJeTyp"] = new JsonArray(teile.OrderByDescending(x => x.Value.Anzahl).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
            {
                ["typ"] = x.Key, ["p20"] = x.Value.Anzahl, ["material"] = string.Join(" | ", x.Value.Material.Order(StringComparer.Ordinal)),
                ["rohbau"] = string.Join(", ", x.Value.Rohbau.Order(StringComparer.Ordinal)),
            }).ToArray()),
        };
    }

    /// <summary>Durchdringungen von Teilen (IfcBuildingElementPart) je Typname (Kern), rohbau und Status – dazu, wie viele Teile der
    /// Netzdatei die Wortliste als Luftschicht erkennt (offener Punkt B3c: P20 bei Teilen).</summary>
    static JsonObject TeileAuswertung(Projekt p, IfcNetzdatei nd)
    {
        var je = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var kol in BcfFormat.Kollisionen(p.Modell, p.Katalog).Where(k => k.Text("typ") == "durchdringung"))
        {
            var bt = kol.Text("bauteil") ?? "";
            int i = bt.IndexOf(':');
            if (i <= 0 || nd.Produkt(Netzkoepfe.Basis(bt[(i + 1)..])) is not { } pr || pr.Entitaet != Pruefkoerperbau.Teilentitaet) continue;
            var s = $"{Typenliste.Kern(pr.Typname ?? "(ohne Typname)")} · rohbau {kol.Text("rohbau")} · {kol.Text("status") ?? "neu"}";
            je[s] = je.GetValueOrDefault(s) + 1;
        }
        var teile = nd.Produkte.Where(x => x.Entitaet == Pruefkoerperbau.Teilentitaet).ToList();
        return new JsonObject
        {
            ["durchdringungenJeTyp"] = new JsonObject(je.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
            ["teileInDerNetzdatei"] = teile.Count,
            ["davonLuftschichtNachWortliste"] = teile.Count(x => Materialwortliste.Gruppe(x.Typname) == Materialgruppe.Luftschicht),
            ["luftschichtTypen"] = new JsonArray(teile.Where(x => Materialwortliste.Gruppe(x.Typname) == Materialgruppe.Luftschicht)
                .GroupBy(x => Typenliste.Kern(x.Typname!)).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (JsonNode)JsonValue.Create($"{g.Key} ({g.Count()})")!).ToArray()),
        };
    }

    [Fact]
    public void Bernstrasse42_EntscheideB3e_UnterdeckeLuftschichtBodenaufbau_VorherNachher()
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
            var nd = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));

            // ---- 1. ohne Typenliste
            var l1 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject
            {
                ["unterlagen"] = new JsonArray(ul.Kennzeichen), ["paare"] = new JsonArray("hlks-architektur", "hlks-vorwand", "hlks-hlks"),
            }));
            var liste1 = (JsonObject)Tuer.Abfrage(p, "kollisionen");
            var p20Lauf1 = P20Auswertung(p, nd);
            var v1 = Ok(AussparungHilfe.Vorschlagen(p));
            var a1 = AussparungHilfe.Aussparungen(p);
            var tm1 = (JsonObject)Tuer.Abfrage(p, "typmaterial");

            // Typnamen aus der Abfrage (unbekannt bzw. aus Typname): je Entscheid der Treffer mit demselben Schlüssel
            var schluessel = tm1["unbekannt"]!.AsArray().Concat(tm1["ausTypname"]!.AsArray())
                .Select(z => z!["schluessel"]!.GetValue<string>()).Distinct().ToList();
            var aehnlich = new JsonObject();
            var gefunden = Entscheide.Select(x =>
            {
                var anfang = x.Typname[..x.Typname.IndexOf(' ')];
                var kandidaten = schluessel.Where(s => s.StartsWith(anfang, StringComparison.Ordinal)).ToList();
                aehnlich[x.Typname] = string.Join(" | ", kandidaten);
                var t = kandidaten.Where(s => Typenliste.Schluessel(s) == Typenliste.Schluessel(x.Typname)).ToList();
                Assert.True(t.Count == 1, $"«{x.Typname}»: {t.Count} Treffer in typmaterial (mit gleichem Anfang: {string.Join(" | ", kandidaten)})");
                return (Typname: t[0], x.Kategorie, x.Stufe);
            }).ToList();
            JsonObject Setzen(int stufe) => new()
            {
                ["typen"] = new JsonArray(gefunden.Where(g => g.Stufe == stufe).Select(g => (JsonNode)new JsonObject { ["typname"] = g.Typname, ["kategorie"] = g.Kategorie }).ToArray()),
                ["quelle"] = Christian,
            };

            JsonObject Stufe(int stufe)
            {
                var gesetzt = Ok(p.Ausfuehren("typmaterial_setzen", Setzen(stufe)));
                var u = Stopwatch.StartNew();
                var v = Ok(AussparungHilfe.Vorschlagen(p));
                double sv = u.Elapsed.TotalSeconds;
                var a = AussparungHilfe.Aussparungen(p);
                u.Restart();
                var l = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["unterlagen"] = new JsonArray(ul.Kennzeichen) }));
                double sl = u.Elapsed.TotalSeconds;
                var liste = (JsonObject)Tuer.Abfrage(p, "kollisionen");
                var tm = (JsonObject)Tuer.Abfrage(p, "typmaterial");
                var lauf = Hinweis(l, "Prüflauf");
                return new JsonObject
                {
                    ["typenliste"] = new JsonArray(Typenliste.Lesen(p.Modell).Eintraege.Select(t => (JsonNode)JsonValue.Create($"{t.Kennzeichen} «{t.Typname}» → {t.Kategorie}")!).ToArray()),
                    ["hinweiseSetzen"] = new JsonArray(gesetzt.Hinweise.Where(h => h.StartsWith('«')).Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
                    ["aussparungen"] = a.Count, ["materialUnbekannt"] = a.Count(Unbekannt),
                    ["hinweisVorschlag"] = v.Hinweise.FirstOrDefault(), ["hinweisMaterial"] = v.Hinweise.ElementAtOrDefault(1),
                    ["jeGrund"] = new JsonArray(v.Hinweise.SkipWhile(h => !h.StartsWith("Liste je Grund")).TakeWhile(h => h.StartsWith("Liste je Grund") || h.StartsWith("  "))
                        .Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
                    ["kollisionenVorgabelauf"] = Zahl(lauf, @"; (\d+) Kollisionen"),
                    ["durchdringungenVorgabelauf"] = Zahl(lauf, @"durchdringung (\d+)"), ["kontaktVorgabelauf"] = Zahl(lauf, @"kontakt (\d+)"),
                    ["hinweisLauf"] = lauf, ["hinweisStatus"] = Hinweis(l, "Status:"), ["hinweisTypenliste"] = Hinweis(l, "Typenliste ("),
                    ["hinweisBekleidung"] = Hinweis(l, "Bekleidungen (IfcCovering)"), ["hinweisVorwand"] = Hinweis(l, "mit Installationsvorwänden"),
                    ["hinweisLuftschicht"] = Hinweis(l, "mit Luftschichten"), ["hinweisMaterialNachgefuehrt"] = Hinweis(l, "Material und rohbau nachgeführt"),
                    ["kollisionenListe"] = liste["anzahl"]!.GetValue<int>(), ["jePaar"] = liste["zusammenfassung"]!["jePaar"]!.DeepClone(),
                    ["jeStatus"] = liste["zusammenfassung"]!["jeStatus"]!.DeepClone(),
                    ["p20"] = P20Auswertung(p, nd), ["teile"] = TeileAuswertung(p, nd),
                    ["unbekannteTypen"] = tm["zusammenfassung"]!["unbekannteTypen"]!.DeepClone(), ["unbekannteTypenTop10"] = Oben(tm),
                    ["typen"] = new JsonArray(tm["typen"]!.AsArray().Select(t => (JsonNode)new JsonObject
                    {
                        ["typname"] = t!["typname"]!.GetValue<string>(), ["kategorie"] = t["kategorie"]!.GetValue<string>(), ["bauteile"] = t["bauteile"]!.GetValue<int>(),
                        ["aussparungen"] = t["aussparungen"]!.GetValue<int>(), ["durchgaenge"] = t["durchgaenge"]!.GetValue<int>(), ["entitaeten"] = t["entitaeten"]!.GetValue<string>(),
                    }).ToArray()),
                    ["zusammenfassung"] = tm["zusammenfassung"]!.DeepClone(),
                    ["sekunden"] = new JsonObject { ["vorschlag"] = Math.Round(sv, 1), ["lauf"] = Math.Round(sl, 1) },
                };
            }

            var stufe1 = new JsonObject
            {
                ["aussparungen"] = a1.Count, ["materialUnbekannt"] = a1.Count(Unbekannt),
                ["hinweisVorschlag"] = v1.Hinweise.FirstOrDefault(), ["hinweisMaterial"] = v1.Hinweise.ElementAtOrDefault(1),
                ["kollisionen"] = liste1["anzahl"]!.GetValue<int>(), ["jePaar"] = liste1["zusammenfassung"]!["jePaar"]!.DeepClone(),
                ["hinweisLauf"] = Hinweis(l1, "Prüflauf"), ["hinweisTypenliste"] = Hinweis(l1, "Typenliste ("),
                ["p20NachLauf"] = p20Lauf1, ["p20NachVorschlag"] = P20Auswertung(p, nd), ["teile"] = TeileAuswertung(p, nd),
                ["unbekannteTypen"] = tm1["zusammenfassung"]!["unbekannteTypen"]!.DeepClone(), ["unbekannteTypenTop10"] = Oben(tm1),
                ["ausTypname"] = new JsonArray(tm1["ausTypname"]!.AsArray().Take(16).Select(z => (JsonNode)JsonValue.Create(
                    $"{z!["schluessel"]!.GetValue<string>()} → {z["gruppe"]!.GetValue<string>()} ({z["bauteile"]!.GetValue<int>()} Bauteile, " +
                    $"{z["aussparungen"]!.GetValue<int>()} Aussparungen, {z["durchgaenge"]!.GetValue<int>()} Durchgänge)")!).ToArray()),
                ["zusammenfassung"] = tm1["zusammenfassung"]!.DeepClone(),
            };
            var stufe2 = Stufe(2);
            var stufe3 = Stufe(3);
            var zahlen = new JsonObject
            {
                ["vergleichB3d"] = "B3d (alter Code, 2 Typen): 67 Aussparungen, 41 «Material unbekannt», 31 unbekannte Typen, Vorgabelauf 2722 Kollisionen " +
                                   "(durchdringung 549, kontakt 2173), P20 348",
                ["typnamenAusAbfrage"] = new JsonArray(gefunden.Select(g => (JsonNode)JsonValue.Create($"{g.Typname} → {g.Kategorie}")!).ToArray()),
                ["typenMitGleichemAnfang"] = aehnlich,
                ["ohneTypenliste"] = stufe1, ["typenlisteB3d"] = stufe2, ["typenlisteB3e"] = stufe3,
                ["sekunden"] = new JsonObject { ["import"] = Math.Round(sImport, 1) },
            };
            var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b3e_typmaterial_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));

            // Typenliste B3e: sechs Einträge, die festgelegten Typen nicht mehr unbekannt, UB ohne Aussparung, Tragkonstruktion Beton
            Assert.Equal(6, Typenliste.Lesen(p.Modell).Eintraege.Count);
            var unbekannt3 = stufe3["unbekannteTypenTop10"]!.AsArray().Select(z => z!["schluessel"]!.GetValue<string>()).ToList();
            foreach (var g in gefunden) Assert.DoesNotContain(g.Typname, unbekannt3);
            foreach (var t in stufe3["typen"]!.AsArray())
            {
                var kat = t!["kategorie"]!.GetValue<string>();
                Assert.True(t["bauteile"]!.GetValue<int>() > 0, $"{t["typname"]}: kein Bauteil");
                if (kat is "bodenaufbau" or "leichtbau" or "installationsvorwand") Assert.Equal(0, t["aussparungen"]!.GetValue<int>());
            }
            // Luftschichten: keine Kollision mehr mit «Luft, …»; Unterdecken und Bodenaufbau nicht im Vorgabelauf
            Assert.DoesNotContain(BcfFormat.Kollisionen(p.Modell, p.Katalog), k => k.Text("status") != "behoben" && k.Text("material")?.StartsWith("Luftschicht") == true);
            Assert.True(stufe3["p20"]!["p20"]!.GetValue<int>() <= stufe2["p20"]!["p20"]!.GetValue<int>(), "P20 nicht weniger");
            Assert.True(stufe3["teile"]!["davonLuftschichtNachWortliste"]!.GetValue<int>() > 0, "keine Luftschicht unter den Teilen erkannt");
            Assert.True(stufe3["materialUnbekannt"]!.GetValue<int>() <= stufe2["materialUnbekannt"]!.GetValue<int>(), "Material unbekannt nicht weniger");
        }
        finally { try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
