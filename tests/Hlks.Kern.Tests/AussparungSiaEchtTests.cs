using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, Nachtrag B4d, Echt-Test Bernstrasse 42 (echte Datei nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo;
/// fehlt sie, kehrt der Test sofort zurück): H+K-IFC übernehmen (Architektur als Hintergrund), mit FIKTIVEN Vorgaben (AussparungHilfe)
/// vorschlagen und nummerieren, Aussparungsplan EG 1:50 nach H+K-Vorlage und SIA 400 B.10 / SIA 410/2 (Sinnbild, Massketten in ganzen cm,
/// ab Fixpunkten des Hintergrunds) als PDF nach %TEMP%\HLKS_CAD\b4d_aussparungsplan_eg.pdf, Zahlen nach …\b4d_aussparungsplan_eg.json.
/// Das Bild dazu entsteht ausserhalb (werkzeuge/pdf_bild.ps1).</summary>
public sealed class AussparungSiaEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_AussparungsplanEG_NachSia_AlsPdf()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Datei Bernstrasse 42 fehlt auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        try
        {
            Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
            AussparungHilfe.Vorgaben(p);
            Ok(AussparungHilfe.Vorschlagen(p));
            Ok(p.Ausfuehren("aussparungen_nummerieren"));
            var eg = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG");
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = "AP-EG", ["name"] = "Aussparungsplan EG", ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000, ["darstellung"] = "aussparungen" },
            }));
            var uhr = Stopwatch.StartNew();
            var g1 = Grafik.Plan(p, "AP-EG");
            double sErster = uhr.Elapsed.TotalSeconds;
            uhr.Restart();
            Grafik.Plan(p, "AP-EG");
            double sZweiter = uhr.Elapsed.TotalSeconds;   // Fixpunktkanten aus dem Zwischenspeicher
            // Nachtrag B4e: eine Leitungsänderung rechnet die Fixpunkte nicht neu (Schlüssel nur aus der Architektur)
            long berechnungen = AussparungMassketten.BerechnungenVon(p);
            var leitung = p.Modell.Elemente.First(e => p.Katalog.IstArt(e, "Leitung"));
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = leitung.Kennzeichen, ["eigenschaften"] = new JsonObject { ["material"] = "FIKTIV B4e" } }));
            uhr.Restart();
            Grafik.Plan(p, "AP-EG");
            double sNachLeitung = uhr.Elapsed.TotalSeconds;
            long berechnungenNachLeitung = AussparungMassketten.BerechnungenVon(p);

            var ids = p.Modell.DerKlasse("Aussparung").Where(a => a.Geschoss == eg.Id).ToList();
            var texte = g1.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text").Select(x => x.Text!).ToList();
            int ketten = g1.Teile.Count(t => t.Id is null && t.Ebene == "beschriftung" && t.Primitive.Count(x => x.Art == "linie") >= 3
                                              && t.Primitive.Any(x => x.Art == "text") && t.Primitive.Where(x => x.Art == "text").All(x => double.TryParse(x.Text, System.Globalization.CultureInfo.InvariantCulture, out _)));
            var kurz = ids.Select(a => AussparungKennzeichnung.Kurzzeichen(p.Modell, p.Katalog, a) ?? "?").GroupBy(x => x).OrderByDescending(x => x.Count())
                .Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count()));

            Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "AP-101", ["titel"] = "Aussparungsplan EG nach SIA 400 B.10 (B4d)", ["format"] = "A1" }));
            Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AP-EG", ["lage"] = new JsonArray(420, 297) }));
            var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD");
            Directory.CreateDirectory(ordner);
            var pdf = Path.Combine(ordner, "b4d_aussparungsplan_eg.pdf");
            File.WriteAllBytes(pdf, Planausgabe.Pdf(p, "PB-1"));

            var zahlen = new JsonObject
            {
                ["aussparungenEG"] = ids.Count,
                ["kurzzeichen"] = new JsonObject(kurz),
                ["ketten"] = ketten,
                ["keinFixpunkt"] = texte.Count(t => t.Contains(AussparungMassketten.KeinFixpunkt)),
                ["sekundenPlan"] = Math.Round(sErster, 2),
                ["sekundenPlanZweiter"] = Math.Round(sZweiter, 2),
                ["sekundenPlanNachLeitungsaenderung"] = Math.Round(sNachLeitung, 2),
                ["fixpunkteNeuGerechnetNachLeitungsaenderung"] = berechnungenNachLeitung - berechnungen,
                ["pdf"] = pdf,
            };
            File.WriteAllText(Path.Combine(ordner, "b4d_aussparungsplan_eg.json"), zahlen.ToJsonString(Json.Eingerueckt));
            aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));

            Assert.NotEmpty(ids);
            Assert.Equal(berechnungen, berechnungenNachLeitung);
            Assert.True(new FileInfo(pdf).Length > 5000);
            Assert.True(sErster < 120, $"Plan {sErster:0.0} s");
        }
        finally { try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
