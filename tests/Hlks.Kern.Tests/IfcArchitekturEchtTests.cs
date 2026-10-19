using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abnahmefälle B1 mit den echten Dateien (%USERPROFILE%\HLKS_CAD_testdaten, nie im Repo): Bernstrasse 42 Architektur
/// (Archicad 27, IFC2x3, ohne Räume), BKW Hauptsitz Architektur (Archicad 28, IFC4), Bernstrasse H+K-Datei nur Räume (Revit,
/// 37 IfcSpace). Laufen nur mit HLKS_ABNAHME=1 (je bis zu zwei Minuten) und wenn die Datei da ist; sonst kehren sie ohne
/// Prüfung zurück (wie Abnahme1bTests; der dynamische Skip von xUnit 2.9 greift hier nicht).
/// Die Zahlen gehen nach %TEMP%\HLKS_CAD\b1_echt_*.json für den Bericht.</summary>
public class IfcArchitekturEchtTests(ITestOutputHelper aus)
{
    string? Datei(string relativ)
    {
        if (Environment.GetEnvironmentVariable("HLKS_ABNAHME") != "1") { aus.WriteLine("Nur mit HLKS_ABNAHME=1 (dauert Minuten)."); return null; }
        var d = Echt(relativ);
        if (d is null) aus.WriteLine($"Echte Datei fehlt: %USERPROFILE%\\HLKS_CAD_testdaten\\{relativ}");
        return d;
    }

    (Projekt P, BefehlsErgebnis E, double Sekunden, JsonObject Zahlen)? Lauf(string relativ, JsonObject? weitere = null)
    {
        if (Datei(relativ) is not { } datei) return null;
        var p = Leer();
        var sw = Stopwatch.StartNew();
        var e = Ok(Uebernehmen(p, datei, weitere));
        double s = sw.Elapsed.TotalSeconds;
        var z = new JsonObject { ["datei"] = Path.GetFileName(datei), ["dauerSekunden"] = Math.Round(s, 1) };
        foreach (var g in p.Modell.Elemente.GroupBy(x => x.Klasse).OrderBy(x => x.Key)) z[g.Key] = g.Count();
        z["tuerenInWand"] = p.Modell.DerKlasse("Tuer").Count(t => t.Bezuege.ContainsKey("wand"));
        z["fensterInWand"] = p.Modell.DerKlasse("Fenster").Count(t => t.Bezuege.ContainsKey("wand"));
        var raeume = new JsonObject();
        foreach (var g in p.Modell.DerKlasse("Raum").GroupBy(r => p.Modell.Finde(r.Geschoss)?.Kennzeichen ?? "?").OrderBy(x => x.Key)) raeume[g.Key] = g.Count();
        z["raeumeJeGeschoss"] = raeume;
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        z["unterlage"] = ul.Eigenschaft("uebernommen")?.Inhalt?.DeepClone();
        if (ul.Text("netz") is { } n)
        {
            var nd = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, n));
            z["netz"] = new JsonObject
            {
                ["megabyte"] = Math.Round(new FileInfo(nd.Pfad).Length / 1e6, 1), ["produkte"] = nd.Produkte.Count,
                ["uebernommen"] = nd.Produkte.Count(x => x.Uebernommen), ["dreiecke"] = nd.Produkte.Sum(x => (long)x.Dreiecke),
            };
        }
        z["hinweise"] = new JsonArray(e.Hinweise.Select(h => (JsonNode)h).ToArray());
        aus.WriteLine(z.ToJsonString(Json.Eingerueckt));
        var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b1_echt_{Path.GetFileNameWithoutExtension(datei)}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        File.WriteAllText(ziel, z.ToJsonString(Json.Eingerueckt));
        return (p, e, s, z);
    }

    [Fact]
    public void Bernstrasse_Architektur_Uebernehmen_RaeumeAusWaenden_RaumErkennenImEG()
    {
        if (Lauf(@"bernstrasse42\architekt_3d.ifc") is not { } lauf) return;
        var (p, _, s, _) = lauf;
        // Probelauf 17.10.2026: 402 IfcWall (263 aus Teilen), 73 IfcSlab, 57 IfcDoor, 36 IfcWindow, 51 IfcColumn, 73 IfcBeam
        Assert.True(p.Modell.DerKlasse("Wand").Count() >= 350, "Wände");
        Assert.True(p.Modell.DerKlasse("Decke").Count() + p.Modell.DerKlasse("Dach").Count() >= 60, "Decken und Dächer");
        Assert.True(p.Modell.DerKlasse("Stuetze").Count() >= 40, "Stützen");
        Assert.True(p.Modell.DerKlasse("Traeger").Count() >= 40, "Unterzüge");
        Assert.True(p.Modell.DerKlasse("Tuer").Count() >= 40, "Türen in Wänden");
        Assert.All(p.Modell.DerKlasse("Tuer").Concat(p.Modell.DerKlasse("Fenster")), t => Assert.True(t.Bezuege.ContainsKey("wand")));
        var eg = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG");
        var raeumeEg = p.Modell.DerKlasse("Raum").Where(r => r.Geschoss == eg.Id).ToList();
        Assert.NotEmpty(raeumeEg);
        // Abnahmefall: raum_erkennen in einem Raum des EG gelingt (Raum aus den Wänden vorher löschen)
        var r = raeumeEg.OrderByDescending(x => x.Geometrie!.Flaeche()).First();
        var punkt = Polygone.InnererPunkt(r.Geometrie!.Punkte);
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = r.Kennzeichen }));
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = new JsonArray(punkt[0], punkt[1]), ["nummer"] = "0.01" }));
        aus.WriteLine($"Dauer {s:0.0} s (Ziel unter 120 s)");
    }

    [Fact]
    public void Bkw_Architektur_Uebernehmen()
    {
        if (Lauf(@"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc") is not { } lauf) return;
        var (p, _, s, _) = lauf;
        // Probelauf 17.10.2026: 2411 IfcWall, 804 IfcSlab, 621 IfcWindow, 299 IfcDoor, 691 IfcColumn, 396 IfcBeam, 1185 Füllungen
        Assert.True(p.Modell.DerKlasse("Wand").Count() >= 2000, "Wände");
        Assert.True(p.Modell.DerKlasse("Decke").Count() + p.Modell.DerKlasse("Dach").Count() >= 600, "Decken und Dächer");
        Assert.True(p.Modell.DerKlasse("Tuer").Count() >= 250, "Türen in Wänden");
        Assert.True(p.Modell.DerKlasse("Fenster").Count() >= 500, "Fenster in Wänden");
        Assert.NotEmpty(p.Modell.DerKlasse("Raum"));
        aus.WriteLine($"Dauer {s:0.0} s");
    }

    [Fact]
    public void Bernstrasse_HkDatei_NurRaeume_37()
    {
        if (Lauf(@"bernstrasse42\hlks_hk_2026-02-10.ifc", new JsonObject { ["bereiche"] = new JsonArray("raeume") }) is not { } lauf) return;
        var p = lauf.P;
        var raeume = p.Modell.DerKlasse("Raum").ToList();
        Assert.Equal(37, raeume.Count);
        Assert.All(raeume, r => { Assert.NotNull(r.Text("nummer")); Assert.True(r.Geometrie!.Flaeche() > 0); Assert.NotNull(r.Geschoss); });
        Assert.Empty(p.Modell.DerKlasse("Wand"));
    }
}
