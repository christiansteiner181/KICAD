using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1b, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im
/// Repo; fehlen sie, kehrt der Test sofort zurück): Architektur-IFC (Archicad) und H+K-IFC (Revit, verknüpfte Architektur als
/// Hintergrund) übernehmen – die Netzdateien entstehen in Fassung 2. Gezählt werden Öffnungen (gefüllt/leer), Teile und Ganze;
/// verglichen werden die harten Befunde der Schmalphase HLKS ↔ Architektur «vorher» (dieselben Produkte als Netzdatei Fassung 1
/// mit der Lage-Heuristik aus B1 – so rechnete B1) und «nachher» (Fassung 2: Teile mit eigenem Ganzen nicht doppelt). Dazu,
/// als Ausblick für B2, wie viele harte Befunde von Leitungen ganz in einer Öffnung ihres Bauteils liegen. Zeiten grosszügig
/// begrenzt (Maschine nachts mit parallelen Bauläufen belastet). Zahlen nach %TEMP%\HLKS_CAD\b1b_netz2_echt.json.</summary>
public sealed class KollisionNetzFassung2EchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_Fassung2_OeffnungenTeile_HarteBefundeVorherNachher_Zeiten()
    {
        if (Echt(@"bernstrasse42\architekt_3d.ifc") is not { } arch || Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Dateien Bernstrasse 42 fehlen auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        try
        {
            var uhr = Stopwatch.StartNew();
            Ok(Uebernehmen(p, arch));
            double sArch = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            var par = new JsonObject { ["bereiche"] = new JsonArray("hlks") };
            var e = Uebernehmen(p, hlks, par);
            if (!e.Angenommen && (e.Fehler ?? "").Contains("ACHTUNG")) { par["ausrichtung"] = "projekt"; e = Uebernehmen(p, hlks, par); }
            Ok(e);
            double sHlks = uhr.Elapsed.TotalSeconds;
            var ulArch = p.Modell.DerKlasse("Unterlage").Single(u => (u.Text("original") ?? "").EndsWith("architekt_3d.ifc", StringComparison.OrdinalIgnoreCase));
            var ulHlks = p.Modell.DerKlasse("Unterlage").Single(u => u.Id != ulArch.Id);

            // 1. Netzdateien Fassung 2: Produkte, Teile, Öffnungen
            var dateien = new JsonObject();
            var netze = new Dictionary<string, IfcNetzdatei>();
            foreach (var (ul, name) in new[] { (ulArch, "architektur"), (ulHlks, "hlksMitRevitLink") })
            {
                var nd = netze[ul.Id] = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
                var teile = nd.Produkte.Where(x => x.Rolle == IfcNetzRolle.Teil).ToList();
                var mitNetz = nd.Produkte.Where(x => x.Dreiecke > 0).Select(x => x.GlobalId).ToHashSet();
                dateien[name] = new JsonObject
                {
                    ["fassung"] = nd.Fassung, ["megabyte"] = Math.Round(new FileInfo(nd.Pfad).Length / 1e6, 1),
                    ["produkte"] = nd.Produkte.Count, ["uebernommen"] = nd.Produkte.Count(x => x.Uebernommen),
                    ["teile"] = teile.Count, ["teileHintergrund"] = teile.Count(t => !t.Uebernommen),
                    ["teileMitGanzemImNetz"] = teile.Count(t => mitNetz.Contains(t.Bezug!)), ["ganzeMitTeilen"] = teile.Select(t => t.Bezug).Distinct().Count(),
                    ["teileJeEntitaet"] = string.Join(", ", teile.GroupBy(t => t.Entitaet).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                    ["oeffnungen"] = nd.Oeffnungen.Count, ["oeffnungenGefuellt"] = nd.Oeffnungen.Count(o => o.Gefuellt),
                    ["oeffnungenOhneWirtInDerDatei"] = nd.Oeffnungen.Count(o => o.Bezug is null),
                    ["oeffnungenWirtEntitaet"] = string.Join(", ", nd.Oeffnungen.GroupBy(o => o.Bezug is { } bz ? nd.Produkt(bz)?.Entitaet ?? "?" : "ohne Wirt")
                        .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                    ["dreiecke"] = nd.Produkte.Sum(x => (long)x.Dreiecke), ["dreieckeOeffnungen"] = nd.Oeffnungen.Sum(x => (long)x.Dreiecke),
                };
            }

            // 2. Prüfkörper nachher (Fassung 2)
            uhr.Restart();
            var nachher = Pruefkoerperbau.Erzeugen(p);
            double sNachher = uhr.Elapsed.TotalSeconds;

            // 3. Prüfkörper vorher: dieselben Produkte als Netzdatei Fassung 1 (so rechnete B1, Heuristik nach Lage) – nur im Test
            //    direkt im Modell umgehängt, danach zurück
            var original = new Dictionary<string, string>();
            foreach (var (id, nd) in netze)
            {
                var name1 = Path.GetFileName(nd.Pfad) + ".fassung1-test.bin";
                NetzdateiTestschreiber.AlsFassung1(nd, Path.Combine(Unterlagen.Ordner(p), name1));
                var ul = p.Modell.Finde(id)!;
                original[id] = ul.Text("netz")!;
                ul.Eigenschaften["netz"] = new Wert { Inhalt = name1 };
            }
            uhr.Restart();
            var vorher = Pruefkoerperbau.Erzeugen(p);
            double sVorher = uhr.Elapsed.TotalSeconds;
            var roh = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { DoppelteTeileWeglassen = false });
            foreach (var (id, n) in original) p.Modell.Finde(id)!.Eigenschaften["netz"] = new Wert { Inhalt = n };

            // 4. Schmalphase hart HLKS ↔ Architektur für alle drei Mengen (parallel, wie B1 gemessen)
            (int Hart, int Paare, int HartTeile, string JeEntitaet, double Sekunden, List<(Pruefkoerper H, Pruefkoerper A)> HartListe) Hart(Pruefkoerpermenge m)
            {
                var hk = m.Koerper.Where(k => k.Bereich == "hlks" && k.ElementId is not null).ToList();
                var ak = m.ImBereich("architektur").ToList();
                var paare = new Raumindex<Pruefkoerper>(hk, k => k.Huelle).Paare(new Raumindex<Pruefkoerper>(ak, k => k.Huelle));
                var t = Stopwatch.StartNew();
                var hart = new System.Collections.Concurrent.ConcurrentBag<(Pruefkoerper, Pruefkoerper)>();
                Parallel.ForEach(paare, x => { if (Schmalphase.Pruefen(x.A, x.B).Hart) hart.Add((x.A, x.B)); });
                var l = hart.ToList();
                return (l.Count, paare.Count, l.Count(x => x.Item2.Entitaet == Pruefkoerperbau.Teilentitaet),
                    string.Join(", ", l.GroupBy(x => x.Item2.Entitaet ?? x.Item2.Klasse).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                    t.Elapsed.TotalSeconds, l);
            }
            var hv = Hart(vorher);
            var hn = Hart(nachher);
            var hr = Hart(roh);

            // 5. Ausblick B2: harte Befunde von Leitungen, deren Durchgänge alle in einer Öffnung des Bauteils liegen
            uhr.Restart();
            int mitDurchgang = 0, ganzInOeffnung = 0, bauteileMitOeffnung = 0;
            foreach (var (h, a) in hn.HartListe)
            {
                if (!h.IstLeitung || a.Netz is null) continue;
                var oe = nachher.OeffnungenVon(a);
                if (oe.Count > 0) bauteileMitOeffnung++;
                var gs = Durchgaenge.Finden(h, a);
                if (gs.Count == 0) continue;
                mitDurchgang++;
                if (gs.All(g => oe.Any(o => o.Huelle.Erweitert(1).Enthaelt(g.Mitte) && o.Enthaelt(g.Mitte)))) ganzInOeffnung++;
            }
            double sOeffnungen = uhr.Elapsed.TotalSeconds;

            // 6. Zeit des Netzdatei-Erzeugers allein (Fassung 2, Architektur-IFC frisch geöffnet, alle Netze neu gerechnet – in der
            //    Übernahme sind sie aus dem Übertrag schon im Speicher)
            double sOeffnen, sErzeugen;
            uhr.Restart();
            using (var d = IfcDatei.Oeffnen(arch))
            {
                sOeffnen = uhr.Elapsed.TotalSeconds;
                var ueb = netze[ulArch.Id].Produkte.Where(x => x.Uebernommen).Select(x => x.EntityLabel).ToHashSet();
                var mitGeo = d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcProduct>().Where(x => x.Representation is not null
                    && x is not (Xbim.Ifc4.Interfaces.IIfcSpatialStructureElement or Xbim.Ifc4.Interfaces.IIfcDistributionPort or Xbim.Ifc4.Interfaces.IIfcOpeningElement
                        or Xbim.Ifc4.Interfaces.IIfcVirtualElement or Xbim.Ifc4.Interfaces.IIfcAnnotation)).ToList();
                var probe = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b1b_zeit_{Guid.NewGuid():N}.bin");
                uhr.Restart();
                new IfcNetzErzeuger().Erzeugen(d, mitGeo, ueb, probe);
                sErzeugen = uhr.Elapsed.TotalSeconds;
                File.Delete(probe);
            }

            int Kicad(Pruefkoerpermenge m, string ul, string klasse) => m.Koerper.Count(k => k.UnterlageId == ul && k.Quelle == "koerper" && k.Klasse == klasse);
            int Teile(Pruefkoerpermenge m) => m.Koerper.Count(k => k.Entitaet == Pruefkoerperbau.Teilentitaet);
            var oeffnungen = nachher.Oeffnungen;
            var zahlen = new JsonObject
            {
                ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
                ["uebertragSekunden"] = new JsonObject { ["architekturMitNetzdatei"] = Math.Round(sArch, 1), ["hlksMitNetzdatei"] = Math.Round(sHlks, 1) },
                ["netzdateien"] = dateien,
                ["pruefkoerper"] = new JsonObject
                {
                    ["vorherFassung1Heuristik"] = vorher.Koerper.Count, ["vorherOhneHeuristik"] = roh.Koerper.Count, ["nachherFassung2"] = nachher.Koerper.Count,
                    ["teilePruefkoerperVorher"] = Teile(vorher), ["teilePruefkoerperOhneHeuristik"] = Teile(roh), ["teilePruefkoerperNachher"] = Teile(nachher),
                    ["archicadDeckenKicadKoerperVorher"] = Kicad(vorher, ulArch.Id, "Decke"), ["archicadDeckenKicadKoerperNachher"] = Kicad(nachher, ulArch.Id, "Decke"),
                    ["archicadExaktNachher"] = nachher.Koerper.Count(k => k.UnterlageId == ulArch.Id && k.Quelle == "netzdatei"),
                    ["hinweiseNachher"] = new JsonArray(nachher.Hinweise.Take(8).Select(x => (JsonNode)x).ToArray()),
                    ["hinweiseVorher"] = new JsonArray(vorher.Hinweise.Take(8).Select(x => (JsonNode)x).ToArray()),
                },
                ["oeffnungen"] = new JsonObject
                {
                    ["alle"] = oeffnungen.Count, ["gefuellt"] = oeffnungen.Count(o => o.Gefuellt), ["leer"] = oeffnungen.Count(o => !o.Gefuellt),
                    ["mitPruefkoerperDesWirts"] = oeffnungen.Count(o => o.WirtKennungen.Count > 0),
                    ["wirtAusLage"] = oeffnungen.Count(o => o.WirtAusLage), ["ohneWirt"] = oeffnungen.Count(o => o.WirtKennungen.Count == 0),
                    ["wirtIstTeil"] = oeffnungen.Count(o => o.WirtGlobalId != o.BauteilGlobalId),
                    ["jeUnterlage"] = string.Join(", ", oeffnungen.GroupBy(o => o.UnterlageId == ulArch.Id ? "architektur" : "hlksMitRevitLink").Select(g => $"{g.Key} {g.Count()}")),
                },
                ["schmalphaseHart"] = new JsonObject
                {
                    ["vorher"] = new JsonObject { ["paare"] = hv.Paare, ["hart"] = hv.Hart, ["hartTeile"] = hv.HartTeile, ["jeEntitaet"] = hv.JeEntitaet, ["sekunden"] = Math.Round(hv.Sekunden, 2) },
                    ["ohneHeuristik"] = new JsonObject { ["paare"] = hr.Paare, ["hart"] = hr.Hart, ["hartTeile"] = hr.HartTeile, ["jeEntitaet"] = hr.JeEntitaet },
                    ["nachher"] = new JsonObject { ["paare"] = hn.Paare, ["hart"] = hn.Hart, ["hartTeile"] = hn.HartTeile, ["jeEntitaet"] = hn.JeEntitaet, ["sekunden"] = Math.Round(hn.Sekunden, 2) },
                    ["b1Referenz"] = "B1-Lauf 19.10.2026 00:46: 4843 Paare, hart 2888, davon IfcBuildingElementPart 283",
                },
                ["ausblickB2"] = new JsonObject
                {
                    ["hartLeitungMitDurchgang"] = mitDurchgang, ["davonDurchgaengeGanzInOeffnung"] = ganzInOeffnung, ["hartBauteilMitOeffnungen"] = bauteileMitOeffnung,
                    ["sekunden"] = Math.Round(sOeffnungen, 2),
                },
                ["sekunden"] = new JsonObject
                {
                    ["pruefkoerperNachher"] = Math.Round(sNachher, 2), ["pruefkoerperVorher"] = Math.Round(sVorher, 2),
                    ["architekturIfcOeffnen"] = Math.Round(sOeffnen, 1), ["netzdateiFassung2ErzeugenArchitektur"] = Math.Round(sErzeugen, 1),
                },
                ["last"] = new JsonObject { ["prozessoren"] = Environment.ProcessorCount, ["hinweis"] = "Zeiten bei paralleler Last anderer Bauläufe" },
            };
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b1b_netz2_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllText(ziel, zahlen.ToJsonString(Json.Eingerueckt));

            // Prüfungen
            Assert.All(netze.Values, nd => Assert.Equal(IfcNetzErzeuger.Fassung, nd.Fassung));   // Fassung 3 seit Nachtrag B3c (Material); Öffnungen und Teile wie Fassung 2
            Assert.True(netze[ulArch.Id].Oeffnungen.Count >= 100, $"Öffnungen Archicad {netze[ulArch.Id].Oeffnungen.Count}");
            Assert.True(netze[ulArch.Id].Oeffnungen.Count(o => o.Gefuellt) >= 50, "Öffnungen mit Tür/Fenster");
            var oeArch = oeffnungen.Where(o => o.UnterlageId == ulArch.Id).ToList();
            Assert.True(oeArch.Count(o => o.WirtKennungen.Count > 0 && !o.WirtAusLage) >= oeArch.Count * 9 / 10, "Archicad-Öffnungen mit Prüfkörper des Wirts (IfcRelVoidsElement)");
            // Archicad (Zerlegung in der Datei): keine Teile als eigene Prüfkörper, Decken aus Teilen exakt (B1: 33 mit KICAD-Körper)
            Assert.Equal(0, nachher.Koerper.Count(k => k.UnterlageId == ulArch.Id && k.Entitaet == Pruefkoerperbau.Teilentitaet));
            Assert.Equal(0, Kicad(nachher, ulArch.Id, "Decke"));
            // Revit-Verknüpfung (keine Zerlegung in der Datei): Lage-Heuristik wie B1 – nicht schlechter
            Assert.True(hn.HartTeile <= hv.HartTeile, $"harte Befunde mit Teilen nachher {hn.HartTeile} ≤ vorher {hv.HartTeile}");
            Assert.True(hn.Hart <= hv.Hart, $"hart nachher {hn.Hart} ≤ vorher {hv.Hart}");
            Assert.True(sNachher < 180, $"Prüfkörper {sNachher:0.0} s");
            Assert.True(sErzeugen < 300, $"Netzdatei erzeugen {sErzeugen:0.0} s");
            Assert.True(hn.Sekunden < 120, $"Schmalphase {hn.Sekunden:0.0} s");
        }
        finally
        {
            try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
