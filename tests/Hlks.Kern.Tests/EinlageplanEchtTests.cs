using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B10, Echt-Test Bernstrasse 42 (echte Dateien nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, nie im Repo; fehlen sie,
/// kehrt der Test sofort zurück). Wie VerlegungEchtTests: H+K-IFC (Revit) übernehmen, Typenliste «5.0_Geschossdecke Tragkonstruktion 16/1.5 cm
/// 175» = beton (Entscheid Christian 19.10.2026). Dann einlageplaene_anlegen und die Pläne gegen Christians S06–S09 prüfen:
///   S07 «Einlagen D.ü.2.UG»: zwei Schalungsschoner, zweimal der Bodenablauf («BWA Varino horizontal», im IFC «Bodenablauf horizontal»);
///   S08 «Einlagen Bodenplatte 2.UG»: Bodenablauf vertikal und B.n.o; S09 «Einlagen D.ü.1.UG»: viele B.n.o, Wasser mit Medium (KW:, WW:);
///   S06 «Grundleitungen 2.UG»: B.n.o an den Enden; das H+K-IFC hat keine Rohrdurchmesser → Koten als Achse «A=» mit Hinweis (nie geraten).
/// Im gewöhnlichen Grundriss 2.UG fehlen alle Einlagen und Grundleitungen. PDFs und Zahlen nach %TEMP%\HLKS_CAD\b10_echt_*.</summary>
public sealed class EinlageplanEchtTests(ITestOutputHelper aus)
{
    const string Typ = "5.0_Geschossdecke Tragkonstruktion 16/1.5 cm 175";
    const string Christian = "Christian Steiner, H+K Planungs AG, 19.10.2026";

    [Fact]
    public void Bernstrasse42_Einlageplaene_WieS06BisS09()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            aus.WriteLine("Echte Datei Bernstrasse 42 fehlt auf diesem Rechner – Test ohne Prüfung beendet.");
            return;
        }
        var p = Leer();
        Ok(Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
        Ok(p.Ausfuehren("typmaterial_setzen", new JsonObject
        {
            ["typen"] = new JsonArray(new JsonObject { ["typname"] = Typ, ["kategorie"] = "beton" }), ["quelle"] = Christian,
        }));
        string vorher = p.Modell.Pruefsumme();
        Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject(), trockenlauf: true));
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        var e = Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        foreach (var kz in new[] { "EL-D.ü.2.UG", "EL-D.ü.1.UG", "EL-BP-2.UG", "GL-2.UG" }) Assert.NotNull(p.Modell.Finde(kz));
        Assert.Empty(Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject())).Aenderungen);   // keine Doppel

        var zahlen = new JsonObject { ["hinweise"] = new JsonArray(e.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray()) };
        List<string> Magenta(Plangrafik g) => g.Teile.Where(x => x.Id is null).SelectMany(x => x.Primitive)
            .Where(x => x.Art == "text" && x.Stil.Farbe == EinlageGrafik.Magenta && x.Fett == true).Select(x => x.Text!).ToList();
        List<string> Schwarz(Plangrafik g) => g.Teile.Where(x => x.Id is null).SelectMany(x => x.Primitive)
            .Where(x => x.Art == "text" && x.Stil.Farbe is null && !x.Stil.NurBildschirm).Select(x => x.Text!).ToList();
        var plaene = new Dictionary<string, Plangrafik>();
        foreach (var kz in new[] { "EL-D.ü.2.UG", "EL-D.ü.1.UG", "EL-BP-2.UG", "GL-2.UG" })
        {
            var uhr = Stopwatch.StartNew();
            var g = Grafik.Plan(p, kz);
            plaene[kz] = g;
            var blatt = p.Modell.DerKlasse("Ansichtsfenster").Single(f => f.Bezuege["ansicht"] == Hole(p, kz).Id).TeilVon!;
            var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b10_echt_{kz.Replace('ü', 'u')}.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            File.WriteAllBytes(datei, Planausgabe.Pdf(p, blatt));
            var bausteine = Magenta(g).GroupBy(x => x).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count());
            zahlen[kz] = new JsonObject
            {
                ["ms"] = uhr.ElapsedMilliseconds,
                ["bausteine"] = new JsonObject(bausteine.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
                ["koten"] = Schwarz(g).Count(x => x.StartsWith("S=") || x.StartsWith("A=")),
                ["pdf"] = datei,
            };
        }
        var d2 = Magenta(plaene["EL-D.ü.2.UG"]);
        Assert.Equal(2, d2.Count(x => x == EinlageGrafik.Schalungsschoner));        // S07: Fassade links und Treppenhaus oben
        Assert.Equal(2, d2.Count(x => x.Contains("Bodenablauf horizontal")));         // S07: BWA Varino horizontal (zweimal)
        var bp = Magenta(plaene["EL-BP-2.UG"]);
        Assert.Contains(bp, x => x.Contains("Bodenablauf vertikal"));                 // S08: BWA Varino vertikal
        Assert.Contains(bp, x => x == EinlageGrafik.BogenOben);                       // S08: B.n.o
        var d1 = Magenta(plaene["EL-D.ü.1.UG"]);
        Assert.True(d1.Count(x => x.Contains(EinlageGrafik.BogenOben)) >= 10, string.Join(", ", d1));
        Assert.Contains(d1, x => x.StartsWith("KW: B.n.o"));                          // S09: «KW: B.n.o Ø16»
        Assert.Contains(d1, x => x.StartsWith("WW: B.n.o"));
        var gl = plaene["GL-2.UG"];
        Assert.True(Magenta(gl).Count(x => x == EinlageGrafik.BogenOben) >= 8);       // S06: B.n.o an den Enden
        Assert.True(Schwarz(gl).Count(x => x.StartsWith("A= -6.")) >= 20);            // ohne Durchmesser im IFC: Achse
        Assert.DoesNotContain(Schwarz(gl), x => x.StartsWith("S="));
        Assert.Contains(EinlageGrafik.HinweisAchse, Schwarz(gl));
        // Massketten in ganzen cm
        Assert.All(plaene.Values.SelectMany(g => g.Teile).Where(x => x.Id is null).SelectMany(x => x.Primitive)
            .Where(x => x.Art == "text" && x.Stil.Farbe == AussparungMassketten.Farbe && x.Fett != true), x => Assert.Matches(@"^\d+$", x.Text!));

        // gewöhnlicher Grundriss 2.UG: keine Einlagen und Grundleitungen
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-2.UG", ["geschoss"] = "2.UG", ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 },
        }));
        var standard = Grafik.Plan(p, "AN-2.UG").Teile.Select(x => x.Id).OfType<string>().ToHashSet();
        var vs = Verlegung.Von(p);
        var weg = vs.JeElement.Values.Where(b => b.IstEinlage || b.IstErdreich).Select(b => b.ElementId).ToList();
        Assert.True(weg.Count > 100);
        Assert.DoesNotContain(weg, standard.Contains);
        zahlen["standard2UG"] = new JsonObject { ["teile"] = standard.Count, ["ausgeblendet"] = weg.Count };

        var json = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b10_einlageplaene_echt.json");
        File.WriteAllText(json, zahlen.ToJsonString(Json.Eingerueckt));
        aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
    }

    static Element Hole(Projekt p, string kz) => p.Modell.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt");
}
