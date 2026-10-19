using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B3, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo;
/// fehlen sie, kehrt der Test sofort zurück): H+K-IFC (Revit) mit den Leitungen übernehmen – die verknüpfte Architektur steht als
/// Hintergrund in der Netzdatei (deckungsgleich, Netze aus Revit ausgeschnitten). Mit FIKTIVEN Vorgaben (Spiel 20,
/// Zusammenfassen 100 mm) Aussparungen vorschlagen – Nachtrag B3b: alle rechteckig, keine automatischen Kernbohrungen –, zweiter
/// Lauf ohne Änderung (Wiedererkennen stabil), eine Kernbohrung von Hand (aussparung_form) bleibt beim dritten Lauf,
/// nummerieren, Liste, Prüfungen. Zeiten grosszügig begrenzt (Maschine mit parallelen Bauläufen belastet). Zahlen nach
/// %TEMP%\HLKS_CAD\b3_aussparungen_echt.json. Nachtrag B3c: nur in Beton und Mauerwerk (Material aus der Netzdatei Fassung 3),
/// durchgehende Aussparungen über Schichten; berichtet werden die Materialnamen und Typnamen der gequerten Bauteile (Grundlage der
/// Wortliste), die Einteilung mit und ohne Typname und die Zahl «Material unbekannt».</summary>
public sealed class AussparungEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_Vorschlagen_ZweiterLaufStabil_Nummern_Liste_Zeiten()
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
            int leitungen = p.Modell.Elemente.Count(e => p.Katalog.IstArt(e, "Leitung"));

            uhr.Restart();
            var e1 = ArchitekturHilfe.Ok(AussparungHilfe.Vorschlagen(p));
            double sVorschlag = uhr.Elapsed.TotalSeconds;
            var alle = AussparungHilfe.Aussparungen(p);
            uhr.Restart();
            var e2 = ArchitekturHilfe.Ok(AussparungHilfe.Vorschlagen(p));
            double sZweiter = uhr.Elapsed.TotalSeconds;
            int automatischRund = alle.Count(a => a.Text("form") == "rund"), automatischKernbohrung = alle.Count(a => a.Text("art") == "kernbohrung");
            // B3b: eine Kernbohrung von Hand (erste Einzelaussparung nach Kennzeichen), dann ein dritter Lauf – die Wahl bleibt
            var einzeln = alle.Where(a => Aussparungsbezug.Leitungen(a).Count == 1).OrderBy(a => a.Kennzeichen, Aussparungsbezug.Natuerlich).First();
            uhr.Restart();
            var ef = ArchitekturHilfe.Ok(AussparungHilfe.Form(p, "rund", einzeln.Kennzeichen));
            double sForm = uhr.Elapsed.TotalSeconds;
            var handRund = p.Modell.Finde(einzeln.Id)!;
            uhr.Restart();
            var e3 = ArchitekturHilfe.Ok(AussparungHilfe.Vorschlagen(p));
            double sDritter = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            var ids = p.Modell.Elemente.Where(e => p.Katalog.IstArt(e, "Leitung") || Aussparungsregeln.Aussparungsfaehig(p.Katalog, e)).Select(e => e.Id).ToList();
            var menge = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Elemente = ids, DoppelteTeileWeglassen = false });
            double sPruefkoerper = uhr.Elapsed.TotalSeconds;
            ArchitekturHilfe.Ok(p.Ausfuehren("aussparungen_nummerieren"));
            alle = AussparungHilfe.Aussparungen(p);
            uhr.Restart();
            var liste = (JsonObject)Tuer.Abfrage(p, "aussparungsliste");
            double sListe = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            var befunde = Pruefungen.Alle(p.Modell, p.Katalog);
            double sPruefung = uhr.Elapsed.TotalSeconds;

            // Nachtrag B3c: Material der gequerten Bauteile (welche Namen das Revit-IFC führt), durchgehende Aussparungen, unbekannt
            var bauteilKoerper = menge.Koerper.Where(x => Aussparungsregeln.Aussparungsfaehig(p.Katalog, p.Modell, x)).ToList();
            var bekleidungen = menge.Koerper.Where(x => x.Bekleidung).ToList();
            var leitKoerper = menge.Koerper.Where(x => x.IstLeitung).ToList();
            var gequert = new HashSet<Pruefkoerper>();
            var bekleidungGequert = new HashSet<Pruefkoerper>();
            foreach (var (liste2, ziel) in new[] { (bauteilKoerper, gequert), (bekleidungen, bekleidungGequert) })
            {
                var idx = new Raumindex<Pruefkoerper>(liste2, b => b.Huelle);
                foreach (var l in leitKoerper)
                    foreach (var b in idx.Kandidaten(l.Huelle))
                        if (!ziel.Contains(b) && Durchgaenge.Finden(l, b).Count > 0) ziel.Add(b);
            }
            var befund = gequert.ToDictionary(b => b, b => Bauteilmaterial.Von(p.Modell, p.Katalog, b));
            var ohneTyp = gequert.ToDictionary(b => b, b => Materialbefund.Einteilen(Bauteilmaterial.AusNetz(b).Where(x => !x.NurWennErkannt)));
            string Gruppe(Materialbefund mb) => mb.Unbekannt ? "unbekannt" : Materialwortliste.Name(mb.Gruppe!.Value);
            JsonObject Zaehlen(IEnumerable<string> werte, int max = 40) => new(werte.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Take(max).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
            var material = new JsonObject
            {
                ["gequerteBauteile"] = gequert.Count,
                ["gequerteBekleidungen"] = bekleidungGequert.Count,
                ["jeGruppe"] = Zaehlen(gequert.Select(b => Gruppe(befund[b]))),
                ["jeGruppeNurMaterialzuordnung"] = Zaehlen(gequert.Select(b => Gruppe(ohneTyp[b]))),
                ["materialnamen"] = Zaehlen(gequert.Select(b => b.Materialien.Count == 0 ? "(keine Zuordnung)" : string.Join(" | ", b.Materialien))),
                ["materialnamenEinzeln"] = Zaehlen(gequert.SelectMany(b => b.Materialien.DefaultIfEmpty("(keine Zuordnung)")).Select(n => $"{n} → {(Materialwortliste.Gruppe(n) is { } g ? Materialwortliste.Name(g) : "unbekannt")}")),
                ["typnamen"] = Zaehlen(gequert.Select(b => $"{b.Entitaet}: {b.Typname ?? "(kein Typname)"} → {Gruppe(befund[b])}"), 80),
                ["entscheidDurchTypname"] = gequert.Count(b => Gruppe(befund[b]) != Gruppe(ohneTyp[b])),
                ["bekleidungMaterialnamen"] = Zaehlen(bekleidungGequert.Select(b => $"{b.Typname ?? "?"}: {string.Join(" | ", b.Materialien)}")),
                ["netzFassung"] = string.Join(", ", gequert.Select(b => b.NetzFassung).Distinct()),
            };

            var zahlen = new JsonObject
            {
                ["leitungen"] = leitungen,
                ["aussparungen"] = alle.Count,
                ["durchgehend"] = alle.Count(a => Aussparungsbezug.BauteilSchluesselAlle(a).Count > 1),
                ["durchgehendBauteile"] = Zaehlen(alle.Where(a => Aussparungsbezug.BauteilSchluesselAlle(a).Count > 1).Select(a => $"{Aussparungsbezug.BauteilSchluesselAlle(a).Count} Bauteile")),
                ["materialUnbekannt"] = alle.Count(a => a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue mu && mu.GetValue<bool>()),
                ["aussparungMaterial"] = Zaehlen(alle.Select(a => a.Text("material") ?? "?"), 30),
                ["material"] = material,
                ["rund"] = alle.Count(a => a.Text("form") == "rund"),
                ["rechteckig"] = alle.Count(a => a.Text("form") == "rechteckig"),
                ["kernbohrungen"] = alle.Count(a => a.Text("art") == "kernbohrung"),
                ["sammel"] = alle.Count(a => Aussparungsbezug.Leitungen(a).Count > 1),
                ["imHintergrund"] = alle.Count(a => a.Bezuege.ContainsKey("unterlage")),
                ["jeEntitaet"] = string.Join(", ", alle.GroupBy(a => a.Kennungen?.GetValueOrDefault("bauteilEntitaet") ?? p.Modell.Finde(a.Bezuege.GetValueOrDefault("bauteil"))?.Klasse ?? "?")
                    .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")),
                ["ohneGeschoss"] = alle.Count(a => a.Geschoss is null),
                ["tiefeMax"] = alle.Select(a => a.Zahl("tiefe") ?? 0).DefaultIfEmpty(0).Max(),
                ["masseMax"] = alle.Select(a => Math.Max(a.Zahl("d") ?? 0, Math.Max(a.Zahl("b") ?? 0, a.Zahl("h") ?? 0))).DefaultIfEmpty(0).Max(),
                ["tiefste"] = new JsonArray(alle.OrderByDescending(a => a.Zahl("tiefe") ?? 0).Take(8).Select(a => (JsonNode)JsonValue.Create(
                    $"{a.Kennzeichen}: {a.Kennungen?.GetValueOrDefault("bauteilEntitaet") ?? a.Klasse} tiefe {a.Zahl("tiefe")} achse {a.Eigenschaft("achse")?.Inhalt?.ToJsonString()} " +
                    $"{(a.Text("form") == "rund" ? $"d {a.Zahl("d")}" : $"{a.Zahl("b")} × {a.Zahl("h")}")} Leitungen {Aussparungsbezug.Leitungen(a).Count} " +
                    $"mitte {string.Join("/", a.Geometrie!.Punkte[0].Select(x => Math.Round(x)))}")).ToArray()),
                ["groesste"] = new JsonArray(alle.OrderByDescending(a => Math.Max(a.Zahl("d") ?? 0, Math.Max(a.Zahl("b") ?? 0, a.Zahl("h") ?? 0))).Take(6).Select(a => (JsonNode)JsonValue.Create(
                    $"{a.Kennzeichen}: {a.Kennungen?.GetValueOrDefault("bauteilEntitaet") ?? a.Klasse} tiefe {a.Zahl("tiefe")} achse {a.Eigenschaft("achse")?.Inhalt?.ToJsonString()} " +
                    $"{(a.Text("form") == "rund" ? $"d {a.Zahl("d")}" : $"{a.Zahl("b")} × {a.Zahl("h")}")} Leitungen {Aussparungsbezug.Leitungen(a).Count} " +
                    $"({string.Join(", ", Aussparungsbezug.Leitungen(a).Take(3).Select(l => p.Modell.Finde(l)?.Kennzeichen))})")).ToArray()),
                ["tiefen"] = string.Join(", ", alle.GroupBy(a => a.Zahl("tiefe")).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} mm: {g.Count()}")),
                // Schichten als eigene Wände (Revit): dieselbe Leitung quert dicht hintereinander mehrere Bauteile
                ["nebenAndererDerselbenLeitung"] = alle.Count(a => alle.Any(b => b != a && Aussparungsbezug.BauteilSchluessel(b) != Aussparungsbezug.BauteilSchluessel(a)
                    && Aussparungsbezug.Leitungen(b).Intersect(Aussparungsbezug.Leitungen(a)).Any()
                    && Geometrie.Abstand(a.Geometrie!.Punkte[0], b.Geometrie!.Punkte[0]) < 500)),
                ["zweiterLaufAenderungen"] = e2.Aenderungen.Count,
                ["automatischRund"] = automatischRund,
                ["automatischKernbohrungen"] = automatischKernbohrung,
                ["vonHandRund"] = $"{handRund.Kennzeichen}: {einzeln.Zahl("b")} × {einzeln.Zahl("h")} → Ø {handRund.Zahl("d")} ({ef.Hinweise.FirstOrDefault()})",
                ["dritterLaufAenderungen"] = e3.Aenderungen.Count,
                ["listeZeilen"] = liste["zeilen"]!.AsArray().Count,
                ["p19"] = befunde.Count(b => b.Code == "P19"),
                ["hinweis"] = e1.Hinweise.FirstOrDefault(),
                ["sekunden"] = new JsonObject
                {
                    ["import"] = Math.Round(sImport, 2), ["vorschlagen"] = Math.Round(sVorschlag, 2), ["zweiterLauf"] = Math.Round(sZweiter, 2),
                    ["formVonHand"] = Math.Round(sForm, 2), ["dritterLauf"] = Math.Round(sDritter, 2),
                    ["davonPruefkoerper"] = Math.Round(sPruefkoerper, 2), ["pruefkoerper"] = menge.Koerper.Count,
                    ["liste"] = Math.Round(sListe, 3), ["pruefungen"] = Math.Round(sPruefung, 3),
                },
            };
            var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b3_aussparungen_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));

            Assert.True(leitungen > 100, $"{leitungen} Leitungen");
            Assert.True(alle.Count > 0, "keine Aussparungen vorgeschlagen");
            // Nachtrag B3c: Netzdatei Fassung 3 mit Material; keine Aussparung in einer Bekleidung; Aussparungen nur in Bauteilen mit Aussparung
            Assert.Equal("3", material["netzFassung"]!.GetValue<string>());
            Assert.All(alle, a => Assert.False(string.IsNullOrEmpty(a.Text("material")), $"{a.Kennzeichen} ohne material"));
            Assert.DoesNotContain(alle, a => a.Kennungen?.GetValueOrDefault("bauteilEntitaet") == "IfcCovering");
            Assert.Equal(alle.Count, liste["zeilen"]!.AsArray().Count);
            Assert.Empty(e2.Aenderungen);                                     // Wiedererkennen stabil: der zweite Lauf ändert nichts
            Assert.Equal(0, automatischRund);                                 // B3b: keine automatischen Kernbohrungen
            Assert.Equal(0, automatischKernbohrung);
            Assert.Equal(("rund", "kernbohrung"), (handRund.Text("form"), handRund.Text("art")));
            Assert.True(AussparungForm.KernbohrungVonHand(handRund));
            Assert.Empty(e3.Aenderungen);                                     // die Wahl von Hand bleibt, nichts wird nachgeführt
            Assert.True(AussparungForm.KernbohrungVonHand(p.Modell.Finde(einzeln.Id)!));
            Assert.Equal(1, alle.Count(a => a.Text("form") == "rund"));      // nach dem Nummerieren neu gelesen: genau die eine von Hand
            Assert.True(sForm < 120, $"aussparung_form {sForm:0.0} s");
            Assert.All(alle.Where(a => a.Geschoss is not null), a => Assert.False(string.IsNullOrEmpty(a.Text("nummer")), $"{a.Kennzeichen} ohne Nummer"));
            Assert.DoesNotContain(befunde, b => b.Code == "P08" && b.Element.StartsWith("AUS-"));
            Assert.True(sVorschlag < 180, $"vorschlagen {sVorschlag:0.0} s");
            Assert.True(sPruefung < 30, $"Prüfungen {sPruefung:0.0} s");
        }
        finally { try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
