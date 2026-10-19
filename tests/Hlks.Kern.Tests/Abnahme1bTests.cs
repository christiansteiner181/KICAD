using System.Diagnostics;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Abnahme Modul 1b mit den echten Dateien (Bernstrasse 42, BKW Hauptsitz). Läuft nur mit HLKS_ABNAHME=1
/// (dauert Minuten) und wenn die Dateien in %USERPROFILE%\HLKS_CAD_testdaten liegen. Schreibt die Zahlen nach
/// %TEMP%\HLKS_CAD\abnahme1b.json für den Testbericht.</summary>
public class Abnahme1bTests
{
    static bool Aktiv => Environment.GetEnvironmentVariable("HLKS_ABNAHME") == "1";

    static readonly string Bericht = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "abnahme1b.json");

    static Projekt NeuesProjekt(out string wurzel)
    {
        wurzel = Path.Combine(Path.GetTempPath(), "hlks_abnahme1b_" + Guid.NewGuid().ToString("N")[..8]);
        var s = new OrdnerSpeicher(Path.Combine(wurzel, "c_Pläne und Modelle", "e_KICAD"));
        return Projekt.Anlegen(Kat, s, "Abnahme 1b", "CS", "abnahme");
    }

    static JsonObject Lauf(Projekt p, string datei, JsonObject? weitere = null)
    {
        var sw = Stopwatch.StartNew();
        var vorher = p.Modell.Elemente.GroupBy(e => e.Klasse).ToDictionary(g => g.Key, g => g.Count());
        var vorherV = p.Modell.Verbindungen.Count;
        var e = Uebernehmen(p, datei, weitere);
        var dauer = sw.Elapsed.TotalSeconds;
        var nachher = p.Modell.Elemente.GroupBy(x => x.Klasse).ToDictionary(g => g.Key, g => g.Count());
        var klassen = new JsonObject();
        foreach (var (k, n) in nachher.OrderBy(x => x.Key)) if (n != vorher.GetValueOrDefault(k)) klassen[k] = n - vorher.GetValueOrDefault(k);
        var ul = p.Modell.DerKlasse("Unterlage").FirstOrDefault(u => (u.Text("original") ?? "").EndsWith(Path.GetFileName(datei), StringComparison.OrdinalIgnoreCase));
        return new JsonObject
        {
            ["datei"] = Path.GetFileName(datei),
            ["angenommen"] = e.Angenommen,
            ["fehler"] = e.Fehler,
            ["vorschlag"] = e.Vorschlag,
            ["dauerSekunden"] = Math.Round(dauer, 1),
            ["aenderungen"] = e.Aenderungen.Count,
            ["neueElemente"] = klassen,
            ["neueVerbindungen"] = p.Modell.Verbindungen.Count - vorherV,
            ["hinweise"] = new JsonArray(e.Hinweise.Take(40).Select(h => (JsonNode)h).ToArray()),
            ["lage"] = ul?.Eigenschaft("lage")?.Inhalt?.DeepClone(),
            ["ausdehnungM"] = Ausdehnung(p, ul?.Id),
            ["uebernommen"] = ul?.Eigenschaft("uebernommen")?.Inhalt?.DeepClone(),
        };
    }

    /// <summary>Ausdehnung der aus einer Unterlage übernommenen Elemente im Modell [minX, minY, maxX, maxY] in m (Median-nahe
    /// 2 %/98 %-Werte, damit Ausreisser nicht zählen) – zum Vergleich der Lage von Architektur und Fachplaner.</summary>
    static JsonArray? Ausdehnung(Projekt p, string? unterlage)
    {
        if (unterlage is null) return null;
        var pts = p.Modell.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == unterlage && e.Klasse is not ("Unterlage" or "Geschoss" or "Gebaeude"))
            .SelectMany(e => e.Geometrie?.Punkte ?? []).Where(q => q.Length >= 2).ToList();
        if (pts.Count == 0) return null;
        double Q(IEnumerable<double> w, double anteil) { var l = w.Order().ToList(); return Math.Round(l[(int)Math.Clamp(anteil * (l.Count - 1), 0, l.Count - 1)] / 1000, 1); }
        return new JsonArray(Q(pts.Select(q => q[0]), 0.02), Q(pts.Select(q => q[1]), 0.02), Q(pts.Select(q => q[0]), 0.98), Q(pts.Select(q => q[1]), 0.98));
    }

    /// <summary>Dauer der 3D-Körper (alle Elemente) und eines Grundrisses je Geschoss (Ansicht wird angelegt).</summary>
    static JsonObject Grafikdauer(Projekt p)
    {
        var sw = Stopwatch.StartNew();
        int koerper = Grafik.Koerper(p).Count;
        var aus = new JsonObject { ["koerper"] = koerper, ["koerperSekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 1) };
        var plaene = new JsonObject();
        foreach (var g in p.Modell.DerKlasse("Geschoss").OrderBy(x => x.Zahl("kote")).Take(4))
        {
            var kz = "AB-" + g.Kennzeichen;
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = g.Kennzeichen,
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 100, ["schnitthoehe"] = 1000 },
            }));
            sw.Restart();
            int teile = Grafik.Plan(p, kz).Teile.Count;
            plaene[g.Kennzeichen!] = new JsonObject { ["teile"] = teile, ["sekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 1) };
        }
        aus["grundrisse"] = plaene;
        return aus;
    }

    [Fact]
    public void Abnahme_EchteDateien_ZahlenUndDauer()
    {
        if (!Aktiv) return;
        var bericht = new JsonObject { ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm") };

        // Bernstrasse 42: Architektur (Archicad IFC2x3, ohne Räume) als Grundlage, dann HLKS von H+K (Revit IFC2x3)
        if (Echt(@"bernstrasse42\architekt_3d.ifc") is { } arch && Echt(@"bernstrasse42\hlks_hk_2024-09-17.ifc") is { } alt
            && Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is { } neu)
        {
            var p = NeuesProjekt(out var w);
            var b = new JsonArray { Lauf(p, arch) };
            // HLKS: zweimal dieselbe Datei unter gleichem Namen (alte, dann neue Fassung) → Abgleich über die GlobalId
            var ordner = Path.Combine(w, "eingang"); Directory.CreateDirectory(ordner);
            var gleich = Path.Combine(ordner, "1-0768_MFH Bernstrasse 42, Worb.ifc");
            File.Copy(alt, gleich, true);
            var r1 = Lauf(p, gleich, new JsonObject { ["bereiche"] = new JsonArray("hlks", "raeume") });
            if (r1["angenommen"]!.GetValue<bool>() == false && (r1["fehler"]?.GetValue<string>() ?? "").Contains("ACHTUNG"))
            {
                b.Add(r1);   // abgelehnt mit Lagebericht – für den Testbericht
                r1 = Lauf(p, gleich, new JsonObject { ["bereiche"] = new JsonArray("hlks", "raeume"), ["ausrichtung"] = "projekt" });
            }
            b.Add(r1);
            File.Copy(neu, gleich, true);
            b.Add(Lauf(p, gleich, new JsonObject { ["bereiche"] = new JsonArray("hlks", "raeume") }));
            // Unterlage DWG des Architekten im EG
            JsonObject? dwg = null;
            if (Echt(@"bernstrasse42\architekt_eg.dwg") is { } d && p.Modell.DerKlasse("Geschoss").FirstOrDefault(g => g.Kennzeichen == "EG") is { } eg)
            {
                var sw = Stopwatch.StartNew();
                var r = p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = d, ["geschoss"] = eg.Kennzeichen });
                dwg = new JsonObject { ["angenommen"] = r.Angenommen, ["fehler"] = r.Fehler, ["dauerSekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 1) };
            }
            bericht["bernstrasse42"] = new JsonObject
            {
                ["laeufe"] = b,
                ["dwg"] = dwg,
                ["grafik"] = Grafikdauer(p),
                ["raeume"] = p.Modell.DerKlasse("Raum").Count(),
                ["elemente"] = p.Modell.Elemente.Count,
                ["verbindungen"] = p.Modell.Verbindungen.Count,
                ["projektordner"] = w,
            };
        }

        // BKW Hauptsitz: Architektur (Archicad IFC4), dann Nova HK, SA, LU (IFC4)
        if (Echt(@"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc") is { } bArch)
        {
            var p = NeuesProjekt(out var w);
            var b = new JsonArray { Lauf(p, bArch) };
            foreach (var n in new[] { "BKW-HS_1_HK-GEBT_NOV.ifc", "BKW-HS_1_SA-GEBT_NOV.ifc", "BKW-HS_1_LU-GEBT_NOV.ifc" })
                if (Echt(@"bkw_hauptsitz\" + n) is { } f)
                {
                    var r = Lauf(p, f, new JsonObject { ["bereiche"] = new JsonArray("hlks") });
                    if (r["angenommen"]!.GetValue<bool>() == false && (r["fehler"]?.GetValue<string>() ?? "").Contains("ACHTUNG Lage"))
                    {
                        b.Add(r);
                        r = Lauf(p, f, new JsonObject { ["bereiche"] = new JsonArray("hlks"), ["ausrichtung"] = "projekt" });
                    }
                    b.Add(r);
                }
            bericht["bkwHauptsitz"] = new JsonObject
            {
                ["laeufe"] = b,
                ["grafik"] = Grafikdauer(p),
                ["raeume"] = p.Modell.DerKlasse("Raum").Count(),
                ["elemente"] = p.Modell.Elemente.Count,
                ["verbindungen"] = p.Modell.Verbindungen.Count,
                ["projektordner"] = w,
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Bericht)!);
        File.WriteAllText(Bericht, bericht.ToJsonString(Json.Eingerueckt));
    }
}
