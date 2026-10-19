using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1: Grafik der Elemente mit Familie – Grundriss (Umriss von oben bzw. Sinnbild, Etikett, Anschlussmarken,
/// Fangpunkte), 3D-Körper, Schnitt und Ansicht (auch durch Netze), familie_ansicht, Rückfall auf die Klassendarstellung.
/// Handrechnungen mit der Testfamilie (fiktive Werte). Leistung: FamilieLeistungTests.</summary>
public class FamilieGrafikTests
{
    static Grafikteil Teil(Plangrafik g, Projekt p, string kz) => g.Teile.Single(t => t.Id == Hole(p, kz).Id);

    static double[] Box(IEnumerable<Primitiv> pr) => Grafik.Begrenzung([new Grafikteil { Primitive = pr.ToList() }])!;

    static void Ansicht(Projekt p, string kz, double[] a, double[] b, string art, string detail = "mittel")
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz,
            ["eigenschaften"] = new JsonObject { ["art"] = art, ["massstab"] = 50, ["detailgrad"] = detail },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts(a, b) },
        }));
    }

    static void Detail(Projekt p, string ansicht, string detail) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ansicht, ["eigenschaften"] = new JsonObject { ["detailgrad"] = detail } }));

    // ------------------------------------------------------------------ Grundriss

    [Fact]
    public void Grundriss_UmrissVonObenGedrehtMitEtikettUndAnschluessen()
    {
        // Detailgrad mittel (Vorgabe): Platten ∪ Abdeckung = Abdeckung [−5, 1005] × [−5, 105] (Familie); gedreht 90°:
        // x' = −y ∈ [−105, 5], y' = x ∈ [−5, 1005] → Welt x 4895 … 5005, y 2995 … 4005 (Ausdehnung 110 × 1010)
        var p = MitHeizkoerper();
        var g = Grafik.Plan(p, "AN-EG");
        var t = Teil(g, p, "HK-1");
        Assert.Equal("apparat", t.Ebene);
        Assert.IsType<FamilienGrafik>(Grafik.QuelleFuer(p.Katalog, Hole(p, "HK-1")));
        var umriss = t.Primitive.Where(x => x.Art == "polygon" && !x.Stil.NurBildschirm).ToList();
        var flaeche = Assert.Single(umriss);
        Assert.Equal("papier", flaeche.Stil.Fuellung);
        Etwa([4895, 2995, 5005, 4005], Box(umriss), 1e-6, "Umriss");
        // Etikett rechts vom Umriss, nur Bildschirm
        var etikett = t.Primitive.Single(x => x.Art == "text" && x.Text == "HK-1");
        Assert.True(etikett.Stil.NurBildschirm);
        Assert.True(etikett.Punkte[0][0] > 5005);
        // Anschlussmarken nur am Bildschirm in der Medienfarbe; Fangpunkte: Einfügepunkt, Anschlüsse, Umrissecken
        var marken = t.Primitive.Where(x => x.Art == "kreis").ToList();
        Assert.Equal(2, marken.Count);
        Assert.All(marken, m => Assert.True(m.Stil.NurBildschirm));
        Assert.Contains(marken, m => m.Stil.Farbe == p.Katalog.Medien["HZ-VL"].FarbeHex && Math.Abs(m.Punkte[0][0] - 4950) < 1e-6 && Math.Abs(m.Punkte[0][1] - 3950) < 1e-6);
        foreach (var f in new double[][] { [5000, 3000], [4950, 3950], [4950, 3900], [4895, 2995], [5005, 4005] })
            Assert.Contains(t.Fangpunkte!, q => Math.Abs(q[0] - f[0]) < 1e-6 && Math.Abs(q[1] - f[1]) < 1e-6);
    }

    [Fact]
    public void Grundriss_DetailgradGrobOhneAbdeckung()
    {
        // grob: nur Platten [0, 1000] × [0, 100] → gedreht x' ∈ [−100, 0], y' ∈ [0, 1000] → Welt 4900 … 5000 × 3000 … 4000
        var p = MitHeizkoerper();
        Detail(p, "AN-EG", "grob");
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "HK-1");
        Etwa([4900, 3000, 5000, 4000], Box(t.Primitive.Where(x => x.Art == "polygon")), 1e-6);
    }

    [Fact]
    public void Grundriss_SinnbildStattUmriss()
    {
        var p = MitHeizkoerper();
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "sinnbild_setzen",
                ["sinnbild"] = new JsonArray(
                    new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(0, 0), new JsonArray("laenge", "tiefe")), ["stil"] = new JsonObject { ["stift"] = "mittel" } },
                    new JsonObject { ["art"] = "text", ["punkte"] = new JsonArray(new JsonArray(0, -100)), ["text"] = "HK", ["detailgrad"] = new JsonArray("fein") }),
            }),
        }));
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "HK-1");
        Assert.DoesNotContain(t.Primitive, x => x.Art == "polygon");   // Sinnbild ersetzt den Umriss
        var linie = t.Primitive.Single(x => x.Art == "linie" && !x.Stil.NurBildschirm);
        Assert.Equal(Stifte.Mittel, linie.Stil.Stift);
        // (0, 0) → (5000, 3000); (1000, 100) → (−100, 1000) → (4900, 4000)
        Etwa([5000, 3000], linie.Punkte[0], 1e-6);
        Etwa([4900, 4000], linie.Punkte[1], 1e-6);
        Assert.DoesNotContain(t.Primitive, x => x.Art == "text" && x.Text == "HK");   // nur fein
        Detail(p, "AN-EG", "fein");
        var text = Teil(Grafik.Plan(p, "AN-EG"), p, "HK-1").Primitive.Single(x => x.Art == "text" && x.Text == "HK");
        Etwa([5100, 3000], text.Punkte[0], 1e-6);   // (0, −100) → (100, 0)
        Assert.Equal(90, text.Winkel);
    }

    [Fact]
    public void Grundriss_UngueltigeOderGeloeschteFamilie_FaelltAufKlassendarstellungZurueck()
    {
        var p = MitHeizkoerper();
        // Familie über ein fremdes Paket gelöscht (ohne die Prüfung von element_loeschen)
        var fam = Hole(p, "FAM-1");
        p.Modell.Anwenden([new Operation { Art = "element.loeschen", Id = fam.Id, Basis = fam.Version, ElementVorher = fam.Kopie() }]);
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "HK-1");
        // HlksGrafik: Apparat-Rechteck 500 × 400 (Katalog «darstellung» Heizkoerper) um den Einfügepunkt
        Etwa([4750, 2800, 5250, 3200], Box(t.Primitive.Where(x => x.Art == "polygon")), 1e-6);
        Assert.Contains(t.Primitive, x => x.Art == "text" && x.Text!.StartsWith("Familie:") && x.Stil.NurBildschirm);
        var k = Assert.Single(Grafik.Koerper(p, [Hole(p, "HK-1").Id]));
        Assert.Equal("quader", Assert.Single(k.Teile).Art);
        Assert.Equal([500.0, 400, 400], k.Teile[0].Masse!);
    }

    // ------------------------------------------------------------------ 3D

    [Fact]
    public void Koerper_3DImDetailgradFeinGedrehtUndVerschoben()
    {
        // Platten: Mitte Familie (500, 50, 450) → (−50, 500) → Welt (4950, 3500, 450), Masse 1000 × 100 × 600, Drehung 90°
        // Ausdehnung fein in z: Stutzen bis bodenabstand − stutzen = 120, Abdeckung bis oberkante + 10 = 760
        var p = MitHeizkoerper();
        var k = Assert.Single(Grafik.Koerper(p, [Hole(p, "HK-1").Id]));
        Assert.Equal("apparat", k.Ebene);
        Assert.Equal(4, k.Teile.Count);
        var platten = k.Teile[0];
        Etwa([4950, 3500, 450], platten.Mitte!, 1e-9);
        Etwa([1000, 100, 600], platten.Masse!);
        Assert.Equal(90, platten.Drehung);
        Assert.Equal("geraet", platten.Material);
        var stutzen = k.Teile.Where(x => x.Art == "zylinder").ToList();
        Assert.Equal(2, stutzen.Count);
        Assert.Equal(120, stutzen.Min(s => Math.Min(s.Von![2], s.Nach![2])), 6);
        Assert.Equal("rohr", stutzen[0].Material);
        var abdeckung = k.Teile[1];
        Assert.Equal(760, abdeckung.Mitte![2] + abdeckung.Masse![2] / 2, 6);
    }

    [Fact]
    public void Koerper_NetzImJsonMitEckenUndDreiecken()
    {
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Netzfamilie() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "A", ["punkt"] = new JsonArray(1000, 0, 0), ["kennzeichen"] = "HK-1" }));
        var k = Assert.Single(Grafik.Koerper(p, [Hole(p, "HK-1").Id]));
        var netz = k.Teile.First(x => x.Art == "koerper");
        var json = Json.Knoten(netz).AsObject();
        Assert.Equal("koerper", json["art"]!.GetValue<string>());
        Assert.True(json["ecken"]!.AsArray().Count > 8);
        Assert.Equal(3, json["dreiecke"]![0]!.AsArray().Count);
        var n = new Netz3(); n.Ecken.AddRange(netz.Ecken!); n.Dreiecke.AddRange(netz.Dreiecke!);
        Assert.Null(n.GeschlossenFehler());
        Etwa([1000, 0, 0, 1400, 400, 300], n.Ausdehnung(), 1e-6);
    }

    /// <summary>Platte 400 × 400 × 300 (xy, z 0 … 300) mit rundem Loch Ø 200 in der Mitte (Halbkreis und zwei Viertelkreise per
    /// Wölbung) – ein nicht konvexes Netz für Schnitt und Ansicht. Fiktiv.</summary>
    static JsonObject Netzfamilie() => Mini(koerper: """
        [{ "id": "platte", "art": "extrusion", "ebene": "xy", "kontur": [[0, 0], [400, 0], [400, 400], [0, 400]],
           "loecher": [[[100, 200, 1], [300, 200, 0.41421356237309503], [200, 300, 0.41421356237309503]]], "von": 0, "bis": 300 }]
        """);

    // ------------------------------------------------------------------ Schnitt und Ansicht

    [Fact]
    public void Schnitt_DurchDenHeizkoerper()
    {
        // Schnittlinie y = 3500 von x 4000 nach 6000, Blick links = nach Norden, u = x − 4000. Heizkörper (gedreht) liegt bei
        // x 4900 … 5000 (Platten, z 150 … 750) bzw. 4895 … 5005 (Abdeckung, z 750 … 760, mittel) → Schnittfläche
        // u 900 … 1000 / 895 … 1005, v 150 … 760.
        var p = MitHeizkoerper();
        Ansicht(p, "S-1", [4000, 3500], [6000, 3500], "schnitt");
        var t = Teil(Grafik.Plan(p, "S-1"), p, "HK-1");
        var flaechen = t.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.NotEmpty(flaechen);
        Etwa([895, 150, 1005, 760], Box(flaechen), 1e-3, "Schnittfläche");
        Assert.All(flaechen, f => Assert.Equal(Stifte.Dick, f.Stil.Stift));
        // grob: nur Platten
        Detail(p, "S-1", "grob");
        Etwa([900, 150, 1000, 750], Box(Teil(Grafik.Plan(p, "S-1"), p, "HK-1").Primitive.Where(x => x.Art == "polygon")), 1e-3);
    }

    [Fact]
    public void Ansicht_VonSuedenAufDenHeizkoerper()
    {
        // Ansicht ohne Schnittflächen: Linie y = 2000 (südlich), Blick nach Norden. Sichtkanten des gedrehten Heizkörpers:
        // seitlich sieht man die Stirnseite x 4895 … 5005 (u 895 … 1005) und z 120 (Stutzen, fein) bzw. 150 … 760
        var p = MitHeizkoerper();
        Ansicht(p, "A-1", [4000, 2000], [6000, 2000], "ansicht", "fein");
        var t = Teil(Grafik.Plan(p, "A-1"), p, "HK-1");
        Assert.DoesNotContain(t.Primitive, x => x.Art == "polygon");
        Etwa([895, 120, 1005, 760], Box(t.Primitive), 1e-3);
    }

    [Fact]
    public void Schnitt_DurchEinNetzMitLoch_ZweiRechteckeUndEckenAufDerEbene()
    {
        // Platte 400 × 400 mit Loch Ø 200 bei (1000, 0) platziert: Schnitt y = 200 (Mitte des Lochs; die Ebene geht genau durch
        // zwei Ecken des 48-Ecks) → Schnittflächen u 0 … 100 und 300 … 400 (u = x − 1000), v 0 … 300; Fläche 2 · 100 · 300.
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Netzfamilie() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "A", ["punkt"] = new JsonArray(1000, 0, 0), ["kennzeichen"] = "HK-1" }));
        Ansicht(p, "S-1", [1000, 200], [2000, 200], "schnitt");
        var t = Teil(Grafik.Plan(p, "S-1"), p, "HK-1");
        var flaechen = t.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, flaechen.Count);
        Etwa(2 * 100 * 300, flaechen.Sum(f => Math.Abs(Polygone.Flaecheninhalt(f.Punkte))), 1e-3);
        Etwa([0, 0, 400, 300], Box(flaechen), 1e-3);
        // dahinter sichtbar: die Lochwand (halber Zylinder hinter der Ebene) zwischen u 100 und 300 – nur Sichtkanten dort
        var kanten = t.Primitive.Where(x => x.Art is "linie" or "polylinie").ToList();
        Assert.NotEmpty(kanten);
        Assert.All(kanten.SelectMany(k => k.Punkte), q => Assert.True(q[0] >= 100 - 1e-3 && q[0] <= 300 + 1e-3, $"Kante ausserhalb des Lochs bei u {q[0]}"));
    }

    [Fact]
    public void Ansicht_NetzMitLoch_VerdecktEigeneKanten()
    {
        // Ansicht von Süden (y = −1000) auf die Platte: sichtbar nur das Rechteck u 0 … 400, v 0 … 300. Die Lochwand liegt
        // hinter der Vorderseite (eigene Verdeckung des Netzes) – keine Kante zwischen u 0 und 400 ausser oben und unten.
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Netzfamilie() }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "A", ["punkt"] = new JsonArray(1000, 0, 0), ["kennzeichen"] = "HK-1" }));
        Ansicht(p, "A-1", [1000, -1000], [2000, -1000], "ansicht");
        var t = Teil(Grafik.Plan(p, "A-1"), p, "HK-1");
        var segmente = t.Primitive.Where(x => x.Art is "linie" or "polylinie").SelectMany(x => x.Punkte.Zip(x.Punkte.Skip(1))).ToList();
        Etwa([0, 0, 400, 300], Box(t.Primitive), 1e-3);
        foreach (var (a, b) in segmente)
        {
            bool waagrecht = Math.Abs(a[1] - b[1]) < 1e-3 && (Math.Abs(a[1]) < 1e-3 || Math.Abs(a[1] - 300) < 1e-3);
            bool senkrecht = Math.Abs(a[0] - b[0]) < 1e-3 && (Math.Abs(a[0]) < 1e-3 || Math.Abs(a[0] - 400) < 1e-3);
            Assert.True(waagrecht || senkrecht, $"unerwartete Kante ({a[0]}, {a[1]}) – ({b[0]}, {b[1]})");
        }
        double Laenge(Func<double[], double[], bool> f) => segmente.Where(s => f(s.First, s.Second)).Sum(s => Math.Abs(s.First[0] - s.Second[0]) + Math.Abs(s.First[1] - s.Second[1]));
        Etwa(400, Laenge((a, b) => Math.Abs(a[1]) < 1e-3 && Math.Abs(b[1]) < 1e-3), 1e-3);         // Unterkante ganz
        Etwa(300, Laenge((a, b) => Math.Abs(a[0] - 400) < 1e-3 && Math.Abs(b[0] - 400) < 1e-3), 1e-3);   // rechte Kante ganz
    }

    // ------------------------------------------------------------------ familie_ansicht

    [Theory]
    [InlineData("vorne", -5, 120, 1005, 760)]    // u = x, v = z (fein: Stutzen bis 120, Abdeckung bis 760)
    [InlineData("hinten", -1005, 120, 5, 760)]   // u = −x
    [InlineData("links", -105, 120, 5, 760)]     // u = −y
    [InlineData("rechts", -5, 120, 105, 760)]    // u = y
    [InlineData("oben", -5, -5, 1005, 105)]      // u = x, v = y
    [InlineData("unten", -1005, -5, 5, 105)]     // u = −x, v = y
    public void FamilieAnsicht_AlleSeitenMitHandrechnung(string ansicht, double u0, double v0, double u1, double v1)
    {
        var p = MitFamilie();
        var o = (JsonObject)Tuer.Abfrage(p, "familie_ansicht", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["ansicht"] = ansicht, ["detailgrad"] = "fein" });
        var g = o["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!;
        Assert.Equal("ansicht", g.Art);
        var modell = g.Teile.Where(t => t.Id is not null).SelectMany(t => t.Primitive).ToList();
        Etwa([u0, v0, u1, v1], Box(modell), 1e-3, ansicht);
        Assert.Contains(g.Teile, t => t.Ebene == "symbol" && t.Primitive.Any(x => x.Art == "text" && x.Text == "vl"));   // Anschlussmarken
    }

    [Fact]
    public void FamilieAnsicht_3dUndFehler()
    {
        var p = MitFamilie();
        var o = (JsonObject)Tuer.Abfrage(p, "familie_ansicht", new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "22-900-800", ["ansicht"] = "3d", ["detailgrad"] = "grob", ["parameter"] = new JsonObject { ["bodenabstand"] = 200 },
        });
        var k = o["koerper"].Deserialize<Koerper>(Json.Optionen)!;
        var platten = Assert.Single(k.Teile);
        Etwa([400, 50, 650], platten.Mitte!);   // laenge 800, hoehe 900, bodenabstand 200 → z 200 … 1100
        Assert.Equal(2, o["anschluesse"]!.AsArray().Count);
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "familie_ansicht", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-900-800", ["ansicht"] = "schräg", ["detailgrad"] = "grob" }));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "familie_ansicht", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "x", ["ansicht"] = "vorne", ["detailgrad"] = "grob" }));
    }
}
