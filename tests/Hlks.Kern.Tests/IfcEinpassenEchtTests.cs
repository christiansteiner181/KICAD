using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>IFC einpassen am echten Fall Bernstrasse 42 (entwurf/ifc-einpassen.md, «Echt-Test»): Architekt (Archicad, IFC2x3, ohne Räume)
/// als erste Datei, dann die H+K-Dateien aus Revit, deren Inhalt um −42.72° um denselben Gebäudeursprung gedreht ist. Referenz (von Hand
/// eingepasst am 24.10.2026): bezug { x: −11.3, y: −32.3, z: 583690, drehung: 42.7182 } (mm, Grad). Läuft nur mit HLKS_ABNAHME=1 (Minuten)
/// und wenn die Dateien in %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42 liegen; sonst kehrt er ohne Prüfung zurück (wie
/// IfcArchitekturEchtTests). Arbeitet in einem neuen Projekt im Temp-Ordner und liest die Testdaten nur; die Zahlen gehen nach
/// %TEMP%\HLKS_CAD\einpassen_echt.json für den Bericht.</summary>
public class IfcEinpassenEchtTests(ITestOutputHelper aus)
{
    static readonly IfcBezug Referenz = new(-11.3, -32.3, 583690, 42.7182);

    static double Z(JsonNode? n) => n!.GetValue<double>();

    [Fact]
    public void Bernstrasse_HkRevit_WirdEindeutigAufDenArchitektenEingepasst()
    {
        if (Environment.GetEnvironmentVariable("HLKS_ABNAHME") != "1") { aus.WriteLine("Nur mit HLKS_ABNAHME=1 (dauert Minuten)."); return; }
        var arch = Echt(@"bernstrasse42\architekt_3d.ifc");
        var hk25 = Echt(@"bernstrasse42\hlks_hk_2025-05-01.ifc");
        var hk26 = Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc");
        if (arch is null || hk25 is null || hk26 is null) { aus.WriteLine("Echte Dateien fehlen (%USERPROFILE%\\HLKS_CAD_testdaten\\bernstrasse42)."); return; }

        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_einpassen_echt_" + Guid.NewGuid().ToString("N")[..8]);
        var bericht = new JsonObject { ["zeit"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm") };
        try
        {
            var p = Projekt.Anlegen(Kat, new OrdnerSpeicher(Path.Combine(wurzel, "e_KICAD")), "Einpassen echt", "T", "pc");
            var uhr = Stopwatch.StartNew();
            Ok(Uebernehmen(p, arch, new JsonObject { ["bereiche"] = new JsonArray("architektur", "raeume") }));
            bericht["architektSekunden"] = Math.Round(uhr.Elapsed.TotalSeconds, 1);

            // 1. 2026-02-10 (mit Wänden) im Trockenlauf: eindeutig, Drehung innert 0.05°
            uhr.Restart();
            var t26 = Ok(Uebernehmen(p, hk26, new JsonObject { ["bereiche"] = new JsonArray("raeume"), ["ausrichtung"] = "einpassen" }, trocken: true));
            bericht["hk26Sekunden"] = Math.Round(uhr.Elapsed.TotalSeconds, 1);
            var e26 = (JsonObject)t26.Lage!["einpassen"]!;
            bericht["hk26"] = e26.DeepClone();
            aus.WriteLine("2026-02-10: " + e26["text"]);
            Assert.Equal("einpassen", t26.Lage["ausrichtung"]!.GetValue<string>());
            Assert.Equal(IfcEinpassen.Eindeutig, e26["ergebnis"]!.GetValue<string>());
            Assert.True(Math.Abs(IfcEinpassen.Winkel(Z(e26["bezug"]!["drehung"]) - Referenz.Drehung)) <= 0.05, $"Drehung {e26["bezug"]!["drehung"]}");

            // 2. ifc_pruefen 2025-05-01: keine Wahl trifft (Bernstrasse: 0 %) → Einpassen gerechnet und empfohlen
            uhr.Restart();
            var pr = (JsonObject)Tuer.Abfrage(p, "ifc_pruefen", new JsonObject { ["datei"] = hk25 });
            bericht["pruefenSekunden"] = Math.Round(uhr.Elapsed.TotalSeconds, 1);
            bericht["pruefen"] = new JsonObject { ["inhaltJeWahl"] = pr["lage"]!["inhaltJeWahl"]?.DeepClone(), ["empfehlung"] = pr["lage"]!["empfehlung"]?.DeepClone(), ["text"] = pr["ausrichtung"]?.DeepClone() };
            aus.WriteLine("ifc_pruefen: " + pr["ausrichtung"]);
            Assert.Equal("einpassen", pr["lage"]!["empfehlung"]?.GetValue<string>());

            // 3. 2025-05-01 (nur IfcSpace) übernehmen mit einpassen: eindeutig, Bezug innert 50 mm / 0.05°, Überdeckung ≥ 0.8
            uhr.Restart();
            var r25 = Ok(Uebernehmen(p, hk25, new JsonObject { ["bereiche"] = new JsonArray("hlks", "raeume"), ["ausrichtung"] = "einpassen" }));
            bericht["hk25Sekunden"] = Math.Round(uhr.Elapsed.TotalSeconds, 1);
            var e25 = (JsonObject)r25.Lage!["einpassen"]!;
            bericht["hk25"] = e25.DeepClone();
            aus.WriteLine("2025-05-01: " + e25["text"]);
            Assert.Equal(IfcEinpassen.Eindeutig, e25["ergebnis"]!.GetValue<string>());
            var b = IfcBezug.Aus(e25["bezug"])!;
            Assert.True(Math.Abs(b.X - Referenz.X) <= 50 && Math.Abs(b.Y - Referenz.Y) <= 50 && Math.Abs(b.Z - Referenz.Z) <= 50, $"Bezug {e25["bezug"]!.ToJsonString()}");
            Assert.True(Math.Abs(IfcEinpassen.Winkel(b.Drehung - Referenz.Drehung)) <= 0.05, $"Drehung {b.Drehung}");
            Assert.True(Z(e25["ueberdeckung"]) >= 0.8, $"Überdeckung {e25["ueberdeckung"]}");
            // die Unterlage trägt den Lagebericht mit der Wahl
            var ul = p.Modell.DerKlasse("Unterlage").Single(u => (u.Text("original") ?? "").EndsWith("hlks_hk_2025-05-01.ifc"));
            Assert.Equal("einpassen", ((JsonObject)ul.Eigenschaft("lage")!.Inhalt!)["ausrichtung"]!.GetValue<string>());
        }
        finally
        {
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "einpassen_echt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllText(ziel, bericht.ToJsonString(Json.Eingerueckt));
            aus.WriteLine(bericht.ToJsonString(Json.Eingerueckt));
            try { Directory.Delete(wurzel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
