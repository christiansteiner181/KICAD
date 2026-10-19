using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B9b, Echt-Test Bernstrasse 42 (echte Datei nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo; fehlt sie,
/// kehrt der Test sofort zurück): das Revit-IFC nennt in keinem Pset der Rohre oder ihrer Typen einen Aussendurchmesser (die Psets der
/// Rohre sind Pset_DistributionFlowElementCommon.Reference, Pset_FlowSegmentPipeSegment.Length, Pset_QuantityTakeOff.Reference; die
/// Typobjekte haben keine) – da kommt also aus dem Rohrkörper (Kreisprofil, Geometrie der Datei, kein Normwert). Geprüft wird der Anteil
/// der Abwasserrohre mit da und die Verteilung der Werte; Zahlen nach %TEMP%\HLKS_CAD\b9b_echt_zahlen.json.</summary>
public sealed class RohrgroesseEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_Abwasserrohre_AussendurchmesserAusDemRohrkoerper_MitHerkunft()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Datei Bernstrasse 42 fehlt auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
        var rohre = p.Modell.Elemente.Where(e => e.Klasse == "Rohr").ToList();
        var abwasser = rohre.Where(e => Rohrgroessen.IstAbwasser(p, e)).ToList();
        var uebrige = rohre.Except(abwasser).ToList();
        Assert.NotEmpty(abwasser);

        var mitDa = abwasser.Where(e => e.Zahl("da") is > 0).ToList();
        var verteilung = mitDa.GroupBy(e => e.Zahl("da")!.Value).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        var quellen = rohre.Where(e => e.Eigenschaft("da") is not null).GroupBy(e => e.Eigenschaft("da")!.Herkunft.Quelle).ToDictionary(g => g.Key, g => g.Count());
        var beschriftung = abwasser.Select(e => Rohrgroessen.Von(p, null, e).Beschriftung).GroupBy(t => t ?? "(keine)").OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count());
        // jedes da hat eine Herkunft aus der Datei (Art ifc) mit Fundstelle; nirgends aus DN gerechnet
        Assert.All(rohre.Where(e => e.Eigenschaft("da") is not null), e =>
        {
            var h = e.Eigenschaft("da")!.Herkunft;
            Assert.Equal("ifc", h.Art);
            Assert.False(string.IsNullOrWhiteSpace(h.Quelle));
            Assert.DoesNotContain("DN", h.Quelle);
        });
        Assert.All(mitDa, e => Assert.Equal(Math.Round(e.Zahl("da")!.Value, 1), e.Zahl("da")!.Value));   // auf 0.1 mm gerundet
        // Beschriftung der Abwasserrohre = Zahl ohne Ø und ohne NW/DN (Kürzel nur, wenn die Datei es nennt: hier keines)
        Assert.All(abwasser.Where(e => e.Zahl("da") is > 0), e =>
        {
            var t = Rohrgroessen.Von(p, null, e).Beschriftung!;
            Assert.Matches(@"^\d+(\.\d)?$", t);
        });
        var zahlen = new JsonObject
        {
            ["rohre"] = rohre.Count, ["abwasser"] = abwasser.Count, ["abwasserMitDa"] = mitDa.Count,
            ["anteilAbwasserMitDa"] = abwasser.Count == 0 ? 0 : Math.Round(100.0 * mitDa.Count / abwasser.Count, 1),
            ["verteilungDa"] = new JsonObject(verteilung.Select(x => KeyValuePair.Create(x.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), (JsonNode?)JsonValue.Create(x.Value)))),
            ["quellenDa"] = new JsonObject(quellen.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
            ["beschriftungAbwasser"] = new JsonObject(beschriftung.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
            ["uebrigeRohre"] = uebrige.Count, ["uebrigeMitDn"] = uebrige.Count(e => e.Zahl("dn") is > 0),
            ["abwasserMitDn"] = abwasser.Count(e => e.Zahl("dn") is > 0),
            ["materialAbwasser"] = new JsonObject(abwasser.GroupBy(e => e.Text("material") ?? "(keines)").ToDictionary(g => g.Key, g => g.Count())
                .Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
        };
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b9b_echt_zahlen.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
        aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
    }

    /// <summary>Bild für die Sichtprüfung (nur lokal): mit der Umgebungsvariable HLKS_B9B_BERN = Kopie des Projektordners e_KICAD (Bernstrasse,
    /// IFC mit da neu übernommen) werden die Einlagepläne angelegt und «Einlagen D.ü.2.UG» als PDF nach %TEMP%HLKS_CAD9b geschrieben;
    /// geprüft wird, dass die Abwasserleitungen im Plan die Aussendurchmesser tragen (wie in Christians S07: «PE-S 110» bzw. «110»).
    /// Ohne die Variable kehrt der Test sofort zurück.</summary>
    [Fact]
    public void Bernstrasse42_EinlageplanD2UG_TraegtAussendurchmesser()
    {
        if (Environment.GetEnvironmentVariable("HLKS_B9B_BERN") is not { Length: > 0 } ordner || !Directory.Exists(ordner))
        {
            aus.WriteLine("HLKS_B9B_BERN nicht gesetzt – Test ohne Prüfung beendet.");
            return;
        }
        var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B9b", "pc", "ki");
        Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        var g = Grafik.Plan(p, "EL-D.ü.2.UG");
        var texte = g.Teile.Where(t => t.Id is null).SelectMany(t => t.Primitive).Where(x => x.Art == "text" && x.Stil.Farbe is null && !x.Stil.NurBildschirm).Select(x => x.Text!).ToList();
        var groessen = texte.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^(\S+ )?\d+(\.\d)?$")).GroupBy(t => t).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count());
        aus.WriteLine("Beschriftungen D.ü.2.UG: " + string.Join(", ", groessen.Select(x => $"{x.Key} ×{x.Value}")));
        aus.WriteLine("alle Texte (Auszug): " + string.Join(" | ", texte.GroupBy(t => t).OrderByDescending(x => x.Count()).Take(30).Select(x => $"{x.Key} x{x.Count()}")));
        Assert.NotEmpty(groessen);
        Assert.DoesNotContain(texte, t => t.Contains("Ø") || t.StartsWith("NW ") || t.StartsWith("DN "));
        var blatt = p.Modell.DerKlasse("Ansichtsfenster").Single(f => f.Bezuege["ansicht"] == p.Modell.Finde("EL-D.ü.2.UG")!.Id).TeilVon!;
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b9b", "einlageplan-d2ug.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllBytes(datei, Planausgabe.Pdf(p, blatt));
    }
}
