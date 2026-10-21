using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N17 mit dem echten Archicad-IFC von Bernstrasse 42 (IFC2X3, Archicad 27.0.0; lokal unter
/// %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\architekt_3d.ifc, nur gelesen, nie im Repository). Fehlt die Datei, überspringt sich der Test.
/// In der Datei stehen 10 IfcStair ohne IfcStairFlight; jede besteht aus IfcBuildingElementPart (Brep) und hat Pset_StairCommon mit
/// NumberOfRiser, NumberOfTreads, RiserHeight (aber ohne TreadLength). Schreibt das Prüfmaterial (Plangrafik als JSON, PDF der Grundrisse)
/// nach %TEMP%\HLKS_CAD\n17\.</summary>
public class TreppeEchtTests(ITestOutputHelper aus)
{
    sealed class EchtDateiFehlt(string grund) : Exception("$XunitDynamicSkip$" + grund);

    static readonly object sperre = new();
    static (Projekt P, BefehlsErgebnis E, string Pfad)? lauf;

    static string Datei() =>
        Echt(@"bernstrasse42\architekt_3d.ifc") ?? Echt(@"bernstrasse42\440_Bernstrasse_42_3D_Modell.ifc")
        ?? throw new EchtDateiFehlt(@"Echte Datei fehlt auf diesem Rechner: %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\architekt_3d.ifc");

    static (Projekt P, BefehlsErgebnis E, string Pfad) Lauf()
    {
        var pfad = Datei();
        lock (sperre)
        {
            if (lauf is { } da) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, pfad));
            lauf = (p, e, pfad);
            return lauf.Value;
        }
    }

    [Fact]
    public void AlleZehnTreppenErkannt_StufenSteigungAusDenAttributen_AuftrittAusDerGeometrie()
    {
        var (p, e, pfad) = Lauf();
        using var d = IfcDatei.Oeffnen(pfad);
        var stairs = d.Modell.Instances.OfType<IIfcStair>().ToList();
        Assert.Equal(10, stairs.Count);
        var treppen = p.Modell.DerKlasse("Treppe").ToList();
        Assert.Equal(10, treppen.Count);
        Assert.DoesNotContain(e.Hinweise, h => h.StartsWith("Treppen:") && h.Contains("→ Hintergrund"));
        foreach (var s in stairs)
        {
            var t = treppen.Single(x => x.Kennungen!["ifc"] == s.GlobalId.ToString());
            var pset = d.Eigenschaften(s);
            double risers = Convert.ToDouble(pset["Pset_StairCommon.NumberOfRiser"].Inhalt), h = d.LaengeMm(pset["Pset_StairCommon.RiserHeight"])!.Value;
            Assert.Equal(risers, t.Zahl("stufen"));
            Assert.Equal(Math.Round(h, 1), t.Zahl("steigung"));
            Assert.EndsWith("Pset_StairCommon.NumberOfRiser", t.Eigenschaft("stufen")!.Herkunft.Quelle);
            Assert.Contains("Geometrie IfcStair #", t.Eigenschaft("auftritt")!.Herkunft.Quelle);   // TreadLength fehlt in der Datei
            // plausible Werte (SIA 358 / Treppenregel: 17 … 19 cm hoch, 26 … 29 cm tief; hier Archicad-Läufe 165 … 185 / 280)
            Assert.InRange(t.Zahl("steigung")!.Value, 150, 200);
            Assert.InRange(t.Zahl("auftritt")!.Value, 250, 300);
            Assert.InRange(t.Zahl("laufbreite")!.Value, 900, 2000);
            // Höhe passt: Oberkante − Unterkante = Stufen · Steigung
            Assert.Equal(t.Zahl("stufen")!.Value * t.Zahl("steigung")!.Value, t.Zahl("oberkante")!.Value - t.Zahl("unterkante")!.Value, 0);
            Assert.Equal("beton", t.Text("material"));
            var l = Treppenlage.Von(t)!;
            Assert.True(l.Kanten.Count >= risers - 1 && l.Kanten.Count <= risers + 2, $"{t.Kennzeichen}: {l.Kanten.Count} Kanten bei {risers} Steigungen");
            Assert.True(l.Lauflinie.Count >= 2);
            Assert.True(l.Austritt[2] > l.Antritt[2]);
            aus.WriteLine($"{t.Kennzeichen} {t.Name} ({p.Modell.Finde(t.Geschoss)?.Kennzeichen}): {t.Zahl("stufen"):0} Stg. {t.Zahl("steigung")}/{t.Zahl("auftritt")} mm, Breite {t.Zahl("laufbreite")}, " +
                          $"z {t.Zahl("unterkante")} … {t.Zahl("oberkante")}, {l.Kanten.Count} Kanten, {l.Podeste.Count} Podest, Lauflinie {l.Lauflinie.Count} Punkte → «{TreppeGrafik.Beschriftung(l)}»");
        }
    }

    [Fact]
    public void OhneAttribute_ZaehltDieGeometrieHoechstensEineSteigungAnders_SteigungUndAuftrittGleich()
    {
        var (p, _, pfad) = Lauf();
        using var d = IfcDatei.Oeffnen(pfad);
        var g = IfcGeometrie.Von(d);
        var teile = d.Modell.Instances.OfType<IIfcRelAggregates>().SelectMany(r => r.RelatedObjects.OfType<IIfcProduct>().Select(k => (r.RelatingObject.EntityLabel, k))).ToLookup(x => x.EntityLabel, x => x.k);
        var koten = d.Geschosse().Select(x => x.KoteMm).ToList();
        int gleich = 0;
        foreach (var s in d.Modell.Instances.OfType<IIfcStair>())
        {
            var netz = new Dreiecksnetz();
            foreach (var t in teile[s.EntityLabel]) if (g.Koerper(t, false) is { } n) netz.Anhaengen(n);
            var form = Treppenanalyse.Aus(netz, koten) ?? throw new Xunit.Sdk.XunitException($"IfcStair #{s.EntityLabel} nicht erkannt");
            var treppe = p.Modell.DerKlasse("Treppe").Single(x => x.Kennungen!["ifc"] == s.GlobalId.ToString());
            Assert.True(Math.Abs(form.Stufen - treppe.Zahl("stufen")!.Value) <= 1, $"#{s.EntityLabel}: Geometrie {form.Stufen}, Attribut {treppe.Zahl("stufen")}");
            if (form.Stufen == treppe.Zahl("stufen")) gleich++;
            Assert.Equal(treppe.Zahl("steigung")!.Value, form.Steigung, 0);   // 165/170/185 mm; Rundung der Archicad-Höhen (171 statt 170) ≤ 1 mm
            Assert.Equal(treppe.Zahl("auftritt")!.Value, form.Auftritt, 0);
        }
        aus.WriteLine($"{gleich} von 10 Treppen zählt die Geometrie ohne Attribut genau wie die Datei, die übrigen um eine Steigung anders (Podest bzw. Boden nicht im Körper)");
        Assert.True(gleich >= 8, $"{gleich} von 10");   // 8 von 10 (Stand 21.10.2026); TR-2 und TR-3 enden an einem Podest bzw. Boden, der in keiner Geschosshöhe liegt
    }

    [Fact]
    public void Grundrisse_ZeigenJedeTreppeInIhremGeschossUndImGeschossDarueber_Pruefbild()
    {
        var (p, _, _) = Lauf();
        var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "n17");
        Directory.CreateDirectory(ordner);
        var geschosse = p.Modell.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote")).ToList();
        foreach (var g in geschosse)
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-" + g.Kennzeichen, ["geschoss"] = g.Kennzeichen,
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
            }));
        var treppen = p.Modell.DerKlasse("Treppe").ToList();
        foreach (var g in geschosse)
        {
            var plan = Grafik.Plan(p, "AN-" + g.Kennzeichen);
            var da = plan.Teile.Where(t => t.Id is not null && treppen.Any(x => x.Id == t.Id)).Select(t => t.Id).Distinct().ToList();
            foreach (var t in treppen.Where(x => x.Geschoss == g.Id))
                Assert.Contains(t.Id, da);   // im eigenen Geschoss
            // von unten kommend (Oberkante im Geschoss, Antritt darunter): von oben sichtbar, alles durchgezogen
            double kote = g.Zahl("kote")!.Value;
            foreach (var t in treppen.Where(x => x.Geschoss != g.Id && x.Zahl("oberkante")! > kote - 50 && x.Zahl("unterkante")! < kote + 1000 && x.Zahl("unterkante")! < kote - 1))
                Assert.Contains(t.Id, da);
            File.WriteAllText(Path.Combine(ordner, $"grundriss_{g.Kennzeichen}.json"), Json.Text(plan));
            aus.WriteLine($"{g.Kennzeichen} (Kote {kote}): {da.Count} Treppen im Grundriss");
        }

        // PDF der Grundrisse EG und 1.OG (A0, 1:50)
        foreach (var g in geschosse.Where(g => g.Kennzeichen is "EG" or "1.OG"))
        {
            var nr = "N17-" + g.Kennzeichen.Replace(".", "");
            Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = nr, ["titel"] = "Treppen " + g.Kennzeichen, ["format"] = "A0" }));
            Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = nr, ["ansicht"] = "AN-" + g.Kennzeichen }));
            File.WriteAllBytes(Path.Combine(ordner, $"grundriss_{g.Kennzeichen}.pdf"), Planausgabe.Pdf(p, nr));
        }
        // Lage der Treppen für den Zuschnitt der Bilder
        File.WriteAllText(Path.Combine(ordner, "treppen.json"), new JsonArray(treppen.OrderBy(t => t.Kennzeichen).Select(t => (JsonNode)new JsonObject
        {
            ["kennzeichen"] = t.Kennzeichen, ["geschoss"] = p.Modell.Finde(t.Geschoss)!.Kennzeichen,
            ["lauflinie"] = Json.Knoten(t.Geometrie!).DeepClone(), ["beschriftung"] = TreppeGrafik.Beschriftung(Treppenlage.Von(t)!),
        }).ToArray()).ToJsonString(Json.Eingerueckt));
    }
}
