using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Quest App (Modul XR, P2) am realen Projekt Test_praxis (Architekten-IFC mit rund 843 000 Dreiecken): liest nur die schreibgeschützte
/// lokale Kopie %USERPROFILE%\HLKS_CAD_testdaten\test_praxis (nie W:, nichts davon ins Repo) und kehrt ohne Prüfung zurück, wenn sie fehlt.
/// Misst je Geschoss und Teil Dreiecke, Netze, Grösse und Erzeugungszeit (Ausgabe und %TEMP%\HLKS_CAD\xr_p2_test_praxis.json, nur Zahlen) und
/// prüft die Grenzen von Meta für die Quest 3 (höchstens 1.5 Mio. Dreiecke und 200 Zeichenaufrufe je Bild; entwurf/modul-xr.md).</summary>
public class XrModellEchtTests(ITestOutputHelper aus)
{
    static string Ordner => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", "test_praxis");

    static Dictionary<string, (long, DateTime)> Dateien(string ordner) =>
        Directory.EnumerateFiles(ordner, "*", SearchOption.AllDirectories).ToDictionary(f => f, f => (new FileInfo(f).Length, File.GetLastWriteTimeUtc(f)));

    [Fact]
    public void TestPraxis_GlbJeGeschoss_GueltigUndImBudget_Messwerte()
    {
        if (!File.Exists(Path.Combine(Ordner, "projekt.json"))) { aus.WriteLine($"Echte Daten fehlen: {Ordner} – Test ohne Prüfung beendet."); return; }
        var davor = Dateien(Ordner);
        var sw = Stopwatch.StartNew();
        var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), "xr-p2-echt", "pc", "mensch");
        long oeffnen = sw.ElapsedMilliseconds;
        sw.Restart();
        var e = XrModell.Erfassen(p);
        long erfassen = sw.ElapsedMilliseconds;
        sw.Restart();
        var s = XrModell.Stand(e);
        long stand = sw.ElapsedMilliseconds;
        aus.WriteLine($"Test_praxis: Öffnen {oeffnen} ms, Erfassen {erfassen} ms, Stand {stand} ms, Bezugspunkt [{string.Join(", ", s.Bezugspunkt)}] mm, {s.Geschosse.Count} Geschosse");
        aus.WriteLine($"{"Geschoss",-9} {"Teil",-9} {"Netze",5} {"Dreiecke",9} {"Ecken",9} {"Elemente",8} {"MB",7} {"ms",6}");

        var zeilen = new JsonArray();
        int netze = 0;
        long dreiecke = 0, bytes = 0, unterlageDreiecke = 0;
        foreach (var g in s.Geschosse)
        {
            int netzeGeschoss = 0;
            foreach (var teil in XrModell.Teile)
            {
                if (teil == XrModell.TeilUnterlage && g.SchluesselUnterlage is null) continue;
                sw.Restart();
                var r = XrModell.Erzeugen(s, g.Geschoss.Id, teil);
                long ms = sw.ElapsedMilliseconds;
                var glb = GlbPruefer.Pruefen(r.Daten);
                Assert.Equal(r.Netze, glb.Meshes.Count);
                Assert.Equal(r.Dreiecke, (long)glb.SzeneExtras["dreiecke"]!);
                netzeGeschoss += r.Netze;
                dreiecke += r.Dreiecke;
                bytes += r.Daten.Length;
                if (teil == XrModell.TeilUnterlage) unterlageDreiecke += r.Dreiecke;
                double mb = r.Daten.Length / 1048576.0;
                aus.WriteLine($"{g.Geschoss.Kennzeichen,-9} {teil,-9} {r.Netze,5} {r.Dreiecke,9} {r.Ecken,9} {r.Elemente,8} {mb,7:F2} {ms,6}");
                zeilen.Add(new JsonObject
                {
                    ["geschoss"] = g.Geschoss.Kennzeichen, ["koteMm"] = g.Geschoss.KoteMm, ["teil"] = teil, ["netze"] = r.Netze, ["dreiecke"] = r.Dreiecke,
                    ["ecken"] = r.Ecken, ["elemente"] = r.Elemente, ["mb"] = Math.Round(mb, 3), ["ms"] = ms,
                });
            }
            Assert.True(netzeGeschoss < 50, $"{g.Geschoss.Kennzeichen}: {netzeGeschoss} Netze (Ziel unter 50 je Geschoss)");
            netze += netzeGeschoss;
        }
        aus.WriteLine($"Summe: {netze} Netze, {dreiecke} Dreiecke, {bytes / 1048576.0:F2} MB");

        // Budget Quest 3 (Meta): ganze Szene unter 200 Zeichenaufrufe und 1.5 Mio. Dreiecke
        Assert.True(netze < 200, $"{netze} Netze");
        Assert.True(dreiecke < 1_500_000, $"{dreiecke} Dreiecke");
        // die Unterlage ist vollständig: Hintergrund aller IFC-Unterlagen bis auf entartete Dreiecke (unter 0.1 %)
        long imKopf = 0;
        foreach (var u in p.Modell.DerKlasse("Unterlage").Where(u => u.Text("art") == "ifc" && u.Text("netz") is { Length: > 0 } && Grafik.Bool(u, "anzeigen", true)))
            imKopf += IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, u.Text("netz")!)).Produkte.Where(x => !x.Uebernommen).Sum(x => (long)x.Dreiecke);
        aus.WriteLine($"Unterlage: {unterlageDreiecke} von {imKopf} Dreiecken des Hintergrunds ({imKopf - unterlageDreiecke} entartet)");
        Assert.InRange(unterlageDreiecke, (long)(imKopf * 0.999), imKopf);
        // jedes Element mit Körper steht in genau einem Geschoss
        Assert.Equal(Grafik.Koerper(p).Count(k => k.Teile.Any(t => t.Art != "netz")), s.Geschosse.Sum(g => g.Elemente));

        // gleiche Daten, zweites Öffnen: gleiche Schlüssel (der Schlüssel hängt am Inhalt)
        var p2 = Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), "xr-p2-echt", "pc", "mensch");
        var s2 = XrModell.Stand(p2);
        Assert.Equal(s.Geschosse.Select(g => (g.SchluesselModell, g.SchluesselUnterlage)), s2.Geschosse.Select(g => (g.SchluesselModell, g.SchluesselUnterlage)));

        // nur gelesen: kein Ordner, keine Datei der Kopie geändert oder dazugekommen
        Assert.Equal(davor, Dateien(Ordner));

        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_p2_test_praxis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllText(datei, new JsonObject
        {
            ["oeffnenMs"] = oeffnen, ["erfassenMs"] = erfassen, ["standMs"] = stand, ["netze"] = netze, ["dreiecke"] = dreiecke,
            ["mb"] = Math.Round(bytes / 1048576.0, 3), ["unterlageDreiecke"] = unterlageDreiecke, ["hintergrundImKopf"] = imKopf, ["zeilen"] = zeilen,
        }.ToJsonString(Json.Eingerueckt));
    }
}
