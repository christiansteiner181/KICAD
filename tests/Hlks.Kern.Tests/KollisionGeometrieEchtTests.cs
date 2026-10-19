using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im
/// Repo; fehlen sie, kehrt der Test sofort zurück): Architektur-IFC (Archicad) und H+K-IFC (Revit) übernehmen, Prüfkörper
/// bauen, Index bauen, alle Kandidatenpaare HLKS ↔ Architektur bestimmen, die Schmalphase (hart) über alle Paare rechnen und
/// die Durchgänge der Leitungen bestimmen – Zeiten messen und ausgeben (Ziel nach entwurf/modul-3b.md: «in Sekunden»).
/// Die H+K-Datei wird mit Netzdatei übernommen: ihre verknüpfte Architektur (Revit-Link, gleiches Koordinatensystem wie die
/// Leitungen) wird so zu Hintergrund-Prüfkörpern. Die Archicad-Datei liegt nach der Übernahme rund 31 m daneben (Lagewarnung
/// der Übernahme, ausrichtung = projekt) – die Paare werden darum je Quelle getrennt gezählt. Zeitgrenzen grosszügig (die
/// Maschine ist nachts mit parallelen Bauläufen belastet). Zahlen nach %TEMP%\HLKS_CAD\b1_kollision_echt.json.</summary>
public sealed class KollisionGeometrieEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_PruefkoerperIndexKandidatenpaareSchmalphase_Zeiten()
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
            Ok(Uebernehmen(p, arch));                                   // Architektur samt Netzdatei (exakte Netze, Hintergrund)
            double sArch = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            var par = new JsonObject { ["bereiche"] = new JsonArray("hlks") };   // mit Netzdatei: verknüpfte Architektur als Hintergrund
            var e = Uebernehmen(p, hlks, par);
            if (!e.Angenommen && (e.Fehler ?? "").Contains("ACHTUNG"))
            {
                aus.WriteLine("HLKS-Übertrag mit Lagewarnung abgelehnt, wiederholt mit ausrichtung = projekt: " + e.Fehler);
                par["ausrichtung"] = "projekt";
                e = Uebernehmen(p, hlks, par);
            }
            Ok(e);
            double sHlks = uhr.Elapsed.TotalSeconds;
            var ulArch = p.Modell.DerKlasse("Unterlage").Single(u => (u.Text("original") ?? "").EndsWith("architekt_3d.ifc", StringComparison.OrdinalIgnoreCase));
            var ulHlks = p.Modell.DerKlasse("Unterlage").Single(u => u.Id != ulArch.Id);

            // 1. Prüfkörper
            uhr.Restart();
            var menge = Pruefkoerperbau.Erzeugen(p);
            double sKoerper = uhr.Elapsed.TotalSeconds;
            var hk = menge.Koerper.Where(k => k.Bereich == "hlks" && k.ElementId is not null).ToList();   // HLKS-Elemente
            var ak = menge.ImBereich("architektur").ToList();
            long Dreiecke(IEnumerable<Pruefkoerper> l) => l.Sum(k => (long)(k.Netz?.AnzahlDreiecke ?? 0));
            var akArchicad = ak.Where(k => k.UnterlageId == ulArch.Id).ToList();
            var akRevit = ak.Where(k => k.UnterlageId == ulHlks.Id).ToList();

            // 2. Index (Breitphase)
            uhr.Restart();
            var ia = new Raumindex<Pruefkoerper>(ak, k => k.Huelle);
            var ih = new Raumindex<Pruefkoerper>(hk, k => k.Huelle);
            double sIndex = uhr.Elapsed.TotalSeconds;

            // 3. Kandidatenpaare HLKS ↔ Architektur (Hüllquader berühren sich), dazu HLKS ↔ HLKS
            uhr.Restart();
            var paare = ih.Paare(ia);
            double sPaare = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            var paareHH = ih.Paare();
            double sPaareHH = uhr.Elapsed.TotalSeconds;

            // 4. Schmalphase hart über alle Paare HLKS ↔ Architektur, ein Faden: erster Lauf baut die Dreiecksbäume, zweiter
            //    Lauf misst die reine Prüfung
            var langsam = new List<(double Ms, Pruefkoerper H, Pruefkoerper A, Kontakt K)>();
            uhr.Restart();
            foreach (var (h, a) in paare)
            {
                var t0 = uhr.Elapsed.TotalMilliseconds;
                var k = Schmalphase.Pruefen(h, a);
                langsam.Add((uhr.Elapsed.TotalMilliseconds - t0, h, a, k));
            }
            double sSchmal = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            foreach (var (h, a) in paare) Schmalphase.Pruefen(h, a);
            double sSchmal2 = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            int hartParallel = 0;
            Parallel.ForEach(paare, x => { if (Schmalphase.Pruefen(x.A, x.B).Hart) Interlocked.Increment(ref hartParallel); });
            double sSchmalParallel = uhr.Elapsed.TotalSeconds;
            int Zahl(Func<(double, Pruefkoerper H, Pruefkoerper A, Kontakt K), bool> f) => langsam.Count(x => f(x));

            // 5. Durchgänge der Leitungen durch die Bauteile (Grundlage Aussparungen) über alle Paare Leitung × Bauteil
            uhr.Restart();
            int durchgaenge = 0;
            foreach (var (h, a) in paare)
                if (h.IstLeitung && a.Netz is not null) durchgaenge += Durchgaenge.Finden(h, a).Count;
            double sDurch = uhr.Elapsed.TotalSeconds;

            // Hintergrund-Teile (IfcBuildingElementPart) der Archicad-Datei, deren Hüllquader in dem eines übernommenen Bauteils liegt
            // (Hinweis auf doppelte Geometrie: Bauteil mit eigenem Körper und zusätzlich seine Teile in der Netzdatei)
            var uebernommeneArchicad = new Raumindex<Pruefkoerper>(akArchicad.Where(k => k.ElementId is not null).ToList(), k => k.Huelle);
            int teileInBauteil = akArchicad.Count(k => k.Entitaet == "IfcBuildingElementPart"
                && uebernommeneArchicad.Kandidaten(k.Huelle).Any(b => b.Huelle.Umfasst(k.Huelle, 1)));

            string Name(Pruefkoerper k) => $"{k.Kennzeichen ?? k.Entitaet} ({k.Klasse ?? k.Entitaet}, {k.Quelle}, {(k.IstLeitung ? k.Achsen[0].ToString() : k.Netz!.AnzahlDreiecke + " Dreiecke")})";
            var zahlen = new JsonObject
            {
                ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
                ["uebertragSekunden"] = new JsonObject { ["architektur"] = Math.Round(sArch, 1), ["hlksMitNetzdatei"] = Math.Round(sHlks, 1) },
                ["pruefkoerper"] = new JsonObject
                {
                    ["alle"] = menge.Koerper.Count, ["hlksElemente"] = hk.Count, ["leitungen"] = hk.Count(k => k.IstLeitung),
                    ["hlksExakt"] = hk.Count(k => k.Quelle == "netzdatei"), ["hlksKicadKoerper"] = hk.Count(k => k.Quelle == "koerper"),
                    ["dreieckeHlksNetze"] = Dreiecke(hk),
                    ["architekturArchicad"] = akArchicad.Count, ["dreieckeArchicad"] = Dreiecke(akArchicad),
                    ["archicadExakt"] = akArchicad.Count(k => k.Quelle == "netzdatei"), ["archicadHintergrund"] = akArchicad.Count(k => k.Quelle == "hintergrund"),
                    ["archicadKicadKoerper"] = akArchicad.Count(k => k.Quelle == "koerper"),
                    ["archicadKicadKlassen"] = string.Join(", ", akArchicad.Where(k => k.Quelle == "koerper").GroupBy(k => k.Klasse).Select(g => $"{g.Key} {g.Count()}")),
                    ["archicadHintergrundEntitaeten"] = string.Join(", ", menge.Koerper.Where(k => k.UnterlageId == ulArch.Id && k.Quelle == "hintergrund")
                        .GroupBy(k => k.Entitaet).OrderByDescending(g => g.Count()).Take(12).Select(g => $"{g.Key} {g.Count()}")),
                    ["archicadGeteilteStuecke"] = akArchicad.Sum(k => k.WeitereElemente.Count),
                    ["archicadHintergrundTeileInUebernommenemBauteil"] = teileInBauteil,
                    ["architekturRevitLink"] = akRevit.Count, ["dreieckeRevitLink"] = Dreiecke(akRevit),
                    ["sonst"] = menge.Koerper.Count(k => k.Bereich == "sonst"), ["hinweise"] = menge.Hinweise.Count,
                },
                ["paare"] = new JsonObject
                {
                    ["hlksArchitektur"] = paare.Count, ["davonArchicad"] = paare.Count(x => x.B.UnterlageId == ulArch.Id),
                    ["davonRevitLink"] = paare.Count(x => x.B.UnterlageId == ulHlks.Id), ["hlksHlks"] = paareHH.Count,
                },
                ["schmalphase"] = new JsonObject
                {
                    ["hart"] = Zahl(x => x.K.Hart), ["enthalten"] = Zahl(x => x.K.Art == Kontaktart.Enthalten), ["beruehrung"] = Zahl(x => x.K.Art == Kontaktart.Beruehrung),
                    ["hartLeitungen"] = Zahl(x => x.K.Hart && x.H.IstLeitung), ["hartAmEnde"] = Zahl(x => x.K.Hart && x.K.AmEnde),
                    ["tiefeHart"] = new JsonObject
                    {
                        ["bis1mm"] = Zahl(x => x.K.Hart && x.K.Eindringtiefe <= 1), ["bis5mm"] = Zahl(x => x.K.Hart && x.K.Eindringtiefe > 1 && x.K.Eindringtiefe <= 5),
                        ["bis20mm"] = Zahl(x => x.K.Hart && x.K.Eindringtiefe > 5 && x.K.Eindringtiefe <= 20),
                        ["bis100mm"] = Zahl(x => x.K.Hart && x.K.Eindringtiefe > 20 && x.K.Eindringtiefe <= 100), ["ueber100mm"] = Zahl(x => x.K.Hart && x.K.Eindringtiefe > 100),
                    },
                    ["hartJeEntitaet"] = string.Join(", ", langsam.Where(x => x.K.Hart).GroupBy(x => x.A.Entitaet ?? x.A.Klasse).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                    ["durchgaenge"] = durchgaenge,
                },
                ["sekunden"] = new JsonObject
                {
                    ["pruefkoerper"] = Math.Round(sKoerper, 2), ["index"] = Math.Round(sIndex, 3), ["paareHlksArchitektur"] = Math.Round(sPaare, 3),
                    ["paareHlksHlks"] = Math.Round(sPaareHH, 3), ["schmalphaseHartErsterLauf"] = Math.Round(sSchmal, 2),
                    ["schmalphaseHartZweiterLauf"] = Math.Round(sSchmal2, 2), ["schmalphaseHartParallel"] = Math.Round(sSchmalParallel, 2),
                    ["durchgaenge"] = Math.Round(sDurch, 2),
                },
                ["langsamstePaareMs"] = new JsonArray(langsam.OrderByDescending(x => x.Ms).Take(5)
                    .Select(x => (JsonNode)$"{x.Ms:0} ms: {Name(x.H)} ↔ {Name(x.A)} → {x.K.Art}").ToArray()),
                ["rechner"] = new JsonObject { ["prozessoren"] = Environment.ProcessorCount },
                ["hinweiseBeispiele"] = new JsonArray(menge.Hinweise.Take(10).Select(x => (JsonNode)x).ToArray()),
            };
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b1_kollision_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllText(ziel, zahlen.ToJsonString(Json.Eingerueckt));

            // Mengen plausibel: ≈ 4000 HLKS-Elemente, Architektur mit Netzen, Leitungen queren die verknüpfte Architektur
            Assert.True(hk.Count >= 3000, $"HLKS-Prüfkörper {hk.Count}");
            Assert.True(hk.Count(k => k.IstLeitung) >= 1900, "Leitungen");
            Assert.True(akArchicad.Count >= 300 && Dreiecke(akArchicad) >= 10000, $"Archicad {akArchicad.Count} Prüfkörper");
            Assert.True(akRevit.Count >= 300, $"Revit-Link {akRevit.Count} Prüfkörper");
            Assert.True(paare.Count(x => x.B.UnterlageId == ulHlks.Id) > 1000, "Paare mit der verknüpften Architektur");
            Assert.True(durchgaenge > 50, $"Durchgänge {durchgaenge}");
            Assert.Equal(Zahl(x => x.K.Hart), hartParallel);   // parallel dasselbe Ergebnis (Prüfnetze fadensicher)
            // Zeitgrenzen grosszügig (Ziel: Sekunden)
            Assert.True(sKoerper < 180, $"Prüfkörper {sKoerper:0.0} s");
            Assert.True(sIndex + sPaare < 30, $"Index und Paare {sIndex + sPaare:0.00} s");
            Assert.True(sSchmal < 600, $"Schmalphase {sSchmal:0.0} s");
        }
        finally
        {
            try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
