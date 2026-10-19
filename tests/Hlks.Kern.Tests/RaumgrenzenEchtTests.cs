using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Raumgrenzen mit der echten Datei BKW Hauptsitz Architektur (Archicad 28, IFC4; %USERPROFILE%\HLKS_CAD_testdaten,
/// nie im Repo). Fehlt die Datei, kehrt der Test ohne Prüfung zurück. Ziel (Auftrag B1): alle Raumgrenzen unter 10 s.
/// Dauer und Summen gehen in die Ausgabe und nach %TEMP%\HLKS_CAD\b1c_raumgrenzen_bkw.json für den Bericht.</summary>
public class RaumgrenzenEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bkw_AlleRaumgrenzen_Dauer_Summen()
    {
        var datei = Echt(@"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc");
        if (datei is null) { aus.WriteLine("Echte Datei fehlt: %USERPROFILE%\\HLKS_CAD_testdaten\\bkw_hauptsitz\\BKW-HS_1_ARCH_ARC.ifc"); return; }
        var p = Leer();
        var sw = Stopwatch.StartNew();
        var uebernahme = Ok(Uebernehmen(p, datei));
        double import = sw.Elapsed.TotalSeconds;

        sw.Restart();
        var erg = Raumgrenzen.Rechnen(p.Modell, p.Katalog);
        double rechnen = sw.Elapsed.TotalSeconds;
        sw.Restart();
        var liste = RaumgrenzenAbfragen.HuellbauteileAus(p.Modell, p.Katalog, erg, null);
        double huelle = sw.Elapsed.TotalSeconds;
        sw.Restart();
        var wb = WaermebrueckenVorschlagen.Vorschlaege(p.Modell, p.Katalog, null, []);
        double waermebruecken = sw.Elapsed.TotalSeconds;

        // Verlängerung der Wände mit Raster gegen alle Paare: gleiche Werte, Zeit vorher/nachher
        var m = p.Modell; var k = p.Katalog;
        var jeGeschoss = m.Elemente.Where(e => k.IstArt(e, "Wand")).GroupBy(e => e.Geschoss)
            .Select(g => g.Select(e => Wandgeometrie.Lage(m, e)).OfType<Wandlage>().ToList()).ToList();
        var kopie = jeGeschoss.Select(l => l.Select(w => Wandgeometrie.Lage(m, w.Wand)!).ToList()).ToList();
        sw.Restart();
        foreach (var l in jeGeschoss) Wandgeometrie.Verlaengern(l, raster: false);
        double allePaare = sw.Elapsed.TotalSeconds;
        sw.Restart();
        foreach (var l in kopie) Wandgeometrie.Verlaengern(l);
        double mitRaster = sw.Elapsed.TotalSeconds;
        int abweichend = jeGeschoss.SelectMany(x => x).Zip(kopie.SelectMany(x => x)).Count(x => x.First.VerlAnfang != x.Second.VerlAnfang || x.First.VerlEnde != x.Second.VerlEnde);

        // Räume, die sich im selben Geschoss überlappen (mehr als 0.01 m²)
        int ueberlappend = 0;
        foreach (var g in m.DerKlasse("Raum").Where(r => r.Geometrie is { Punkte.Count: >= 3 }).GroupBy(r => r.Geschoss))
        {
            var l = g.Select(r => (Pfad: new Clipper2Lib.PathsD { Polygone.Pfad(r.Geometrie!.Punkte) }, Box: ArchitekturHilfe.Box(r.Geometrie!.Punkte))).ToList();
            for (int i = 0; i < l.Count; i++)
                for (int j = i + 1; j < l.Count; j++)
                {
                    var (a, b) = (l[i].Box, l[j].Box);
                    if (a[0] >= b[2] || b[0] >= a[2] || a[1] >= b[3] || b[1] >= a[3]) continue;
                    var s = Clipper2Lib.Clipper.Intersect(l[i].Pfad, l[j].Pfad, Clipper2Lib.FillRule.NonZero, 3);
                    if (Math.Abs(Clipper2Lib.Clipper.Area(s)) > Raumgrenzen.KleinsteFlaeche) ueberlappend++;
                }
        }

        // Räume aus Wänden je Geschoss mit und ohne Höhenprüfung (Entscheid 4): Anzahl und Fläche
        var hoehenpruefung = new JsonObject();
        foreach (var g in m.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote")))
        {
            double kote = g.Zahl("kote") ?? 0;
            List<Wandlage> Lagen(Func<Wandlage, bool> wahl)
            {
                var l = m.Elemente.Where(e => k.IstArt(e, "Wand") && e.Geschoss == g.Id).Select(e => Wandgeometrie.Lage(m, e)).OfType<Wandlage>()
                    .Where(w => w.Dicke > 0 && wahl(w)).ToList();
                Wandgeometrie.Verlaengern(l);
                return l;
            }
            double h = g.Zahl("hoehe") ?? 0;
            static string Text(string name, List<List<double[]>> f) => $"{name} {f.Count}/{Math.Round(f.Sum(r => Polygone.Flaecheninhalt(r)) / 1e6)}";
            var ohne = RaeumeAusWaenden.Flaechen(Lagen(_ => true));
            var vorgabe = RaeumeAusWaenden.Flaechen(Lagen(w => w.Hoehe is not double || h <= 0 || Math.Min(w.Oberkante, kote + h) - Math.Max(w.Unterkante, kote) > h / 2));
            var gewaehlt = RaeumeAusWaenden.Zusammenfuehren(RaeumeAusWaenden.Flaechen(Lagen(w => RaeumeAusWaenden.ImGeschoss(w, kote))), ohne, out _);
            hoehenpruefung[g.Kennzeichen] = $"{Text("ohne", ohne)} | {Text("Vorgabe Überdeckung > H/2", vorgabe)} | {Text("gewählt", gewaehlt)} (Räume/m²)";
        }

        int raeume = p.Modell.DerKlasse("Raum").Count();
        var z = new JsonObject
        {
            ["datei"] = Path.GetFileName(datei),
            ["importSekunden"] = Math.Round(import, 1),
            ["raumgrenzenSekunden"] = Math.Round(rechnen, 2),
            ["huellbauteileSekunden"] = Math.Round(huelle, 2),
            ["waermebrueckenSekunden"] = Math.Round(waermebruecken, 2),
            ["zeiten"] = new JsonObject(erg.Zeiten.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)Math.Round(x.Value, 2)))),
            ["verlaengernAllePaareSekunden"] = Math.Round(allePaare, 2),
            ["verlaengernRasterSekunden"] = Math.Round(mitRaster, 2),
            ["verlaengernAbweichend"] = abweichend,
            ["raeumeUeberlappend"] = ueberlappend,
            ["waende"] = p.Modell.DerKlasse("Wand").Count(),
            ["raeume"] = raeume,
            ["raeumeGerechnet"] = erg.Raeume.Count,
            ["raeumeJeGeschoss"] = new JsonObject(m.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote"))
                .Select(g => KeyValuePair.Create(g.Kennzeichen, (JsonNode?)$"{m.DerKlasse("Raum").Count(r => r.Geschoss == g.Id)} Räume, {Math.Round(m.DerKlasse("Raum").Where(r => r.Geschoss == g.Id).Sum(r => r.Geometrie!.Flaeche()) / 1e6)} m², Kote {g.Zahl("kote")}, Höhe {g.Zahl("hoehe")}"))),
            ["hoehenpruefung"] = hoehenpruefung,
            ["hinweiseRaeume"] = new JsonArray(uebernahme.Hinweise.Where(h => h.StartsWith("Räume")).Select(h => (JsonNode)h).ToArray()),
            ["raumgrenzen"] = erg.Grenzen.Count,
            ["jeAngrenzung_m2"] = new JsonObject(erg.Grenzen.GroupBy(g => g.Angrenzung).OrderBy(g => g.Key)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)Math.Round(g.Sum(x => x.Flaeche), 1)))),
            ["jeArt_m2"] = new JsonObject(erg.Grenzen.GroupBy(g => g.Art).OrderBy(g => g.Key)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)Math.Round(g.Sum(x => x.Flaeche), 1)))),
            // Boden, Decke und Dach je Angrenzung (Wandband-Regel 18.10.2026: Vergleich vorher/nachher)
            ["waagrechtJeArtUndAngrenzung_m2"] = new JsonObject(erg.Grenzen.Where(g => g.Art is "boden" or "decke" or "dach")
                .GroupBy(g => g.Art + " " + g.Angrenzung).OrderBy(g => g.Key)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)Math.Round(g.Sum(x => x.Flaeche), 1)))),
            ["offen_m2"] = Math.Round(erg.Grenzen.Where(g => g.Offen).Sum(g => g.Flaeche), 1),
            ["offenAnteilSeiten"] = Math.Round(erg.Grenzen.Where(g => g.Offen).Sum(g => g.Flaeche)
                / Math.Max(1e-9, erg.Grenzen.Where(g => g.Art is "wand" or "fenster" or "tuer").Sum(g => g.Flaeche)), 3),
            ["bodenOhnePlatte_m2"] = Math.Round(erg.Grenzen.Where(g => g.Art == "boden" && g.Bauteil is null).Sum(g => g.Flaeche), 1),
            ["deckeOhnePlatte_m2"] = Math.Round(erg.Grenzen.Where(g => g.Art is "decke" or "dach" && g.Bauteil is null).Sum(g => g.Flaeche), 1),
            ["raumflaeche_m2"] = Math.Round(erg.Raeume.Sum(r => r.FlaecheM2), 1),
            ["huellbauteile"] = liste.Count,
            ["huelleInnen_m2"] = Math.Round(liste.Sum(x => x.FlaecheInnen), 1),
            ["huelleAussen_m2"] = Math.Round(liste.Sum(x => x.FlaecheAussen ?? 0), 1),
            ["deckenauflager"] = wb.Count(v => v.Art == "deckenauflager"),
            ["deckenauflager_m"] = Math.Round(wb.Where(v => v.Art == "deckenauflager").Sum(v => v.LaengeM), 1),
            ["fensteranschlaege"] = wb.Count(v => v.Art == "fensteranschlag"),
            ["fensteranschlaege_m"] = Math.Round(wb.Where(v => v.Art == "fensteranschlag").Sum(v => v.LaengeM), 1),
            ["hinweise"] = new JsonArray(erg.Hinweise.Select(h => (JsonNode)h).ToArray()),
        };
        aus.WriteLine(z.ToJsonString(Json.Eingerueckt));
        var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b1c_raumgrenzen_bkw.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        File.WriteAllText(ziel, z.ToJsonString(Json.Eingerueckt));

        Assert.True(raeume > 0);
        Assert.True(erg.Raeume.Count >= raeume * 0.9, "fast alle Räume gerechnet");
        // Boden und Decke schliessen je Raum auf die Raumfläche (Splitter eingerechnet)
        foreach (var rm in erg.Raeume)
        {
            var g = erg.Grenzen.Where(x => x.Raum.Id == rm.Raum.Id).ToList();
            Assert.True(Math.Abs(rm.FlaecheM2 - g.Where(x => x.Art == "boden").Sum(x => x.Flaeche)) < 1e-3 * Math.Max(1, rm.FlaecheM2), $"{rm.Raum.Kennzeichen} Boden");
        }
        Assert.True(rechnen < 10, $"Raumgrenzen dauern {rechnen:0.0} s (Ziel unter 10 s)");
        Assert.Equal(0, abweichend);     // Raster rechnet genau wie alle Paare
        Assert.Equal(0, ueberlappend);   // Inseln abgezogen (vorher 28 Paare)
    }
}
