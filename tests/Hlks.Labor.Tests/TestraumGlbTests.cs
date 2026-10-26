// XR-Labor L1: der Testraum als GLB je Etage – mit einem unabhängigen Leser geprüft (TestraumGlb): Format wie KICAD (XrModell), Koordinaten, Gruppen, Farben,
// Zuordnung der Dreiecke, Versionsschlüssel und die Ziele der Prüfaufgaben in scene.extras.kicad.labor.

using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Labor.Tests;

public class TestraumGlbTests
{
    static readonly TestraumStand Stand = TestraumModell.Bauen(TestraumKatalog.Laden(TestraumHilfe.KatalogOrdner()));
    static TestraumGlb Glb(string geschoss) => TestraumGlb.Lesen(Stand.Etage(geschoss)!.Glb);

    static void Nah(double soll, double ist, double tol = 1e-4) => Assert.True(Math.Abs(soll - ist) <= tol, $"{ist} statt {soll}");

    [Fact]
    public void Format_wie_KICAD_Szene_Asset_und_Netze()
    {
        Assert.Equal(XrModell.Fassung, TestraumModell.Fassung);   // dieselbe Fassung des Formats wie KICAD
        foreach (var e in Stand.Etagen)
        {
            var g = TestraumGlb.Lesen(e.Glb);
            Assert.Equal("kicad-xr-glb", (string)g.Gltf["asset"]!["extras"]!["kicad"]!["format"]!);
            Assert.Equal($"{e.Geschoss.Kennzeichen} modell", (string)g.Gltf["scenes"]![0]!["name"]!);
            var s = g.Szene;
            // dieselben Felder wie XrModell.Szene, dazu «labor»
            Assert.Equal(["fassung", "teil", "schluessel", "projekt", "geschoss", "bezugspunktMm", "achsen", "netze", "dreiecke", "elemente", "hinweise", "labor"], s.Select(p => p.Key).ToArray());
            Assert.Equal("modell", (string)s["teil"]!);
            Assert.Equal(e.Schluessel, (string)s["schluessel"]!);
            Assert.Equal("labor-testraum", (string)s["projekt"]!);
            Assert.Equal(e.Geschoss.Id, (string)s["geschoss"]!["id"]!);
            Assert.Equal(e.Geschoss.KoteMm, (double)s["geschoss"]!["koteMm"]!);
            Assert.Equal(e.Geschoss.HoeheMm, (double)s["geschoss"]!["hoeheMm"]!);
            Assert.Equal([0.0, 0.0, 0.0], s["bezugspunktMm"]!.AsArray().Select(x => (double)x!).ToArray());   // Nullpunkt (Testraum liegt nah)
            Assert.Equal(XrKoordinaten.Achsen, (string)s["achsen"]!);
            Assert.Equal(g.Netze.Count, (int)s["netze"]!);
            Assert.Equal(g.Netze.Sum(n => n.Dreiecke), (long)s["dreiecke"]!);
            Assert.Equal(e.Elemente, (int)s["elemente"]!);
            Assert.Empty(s["hinweise"]!.AsArray());
            // Netze je (Gruppe, Farbe, Deckkraft): undurchsichtige zuerst, Bereiche der Elemente lückenlos und ohne Überlappung
            var deckkraft = g.Netze.Select(n => (double)n.Kicad["deckkraft"]!).ToList();
            Assert.Equal(deckkraft.OrderBy(d => d < 1 ? 1 : 0).ToList(), deckkraft);
            foreach (var n in g.Netze)
            {
                var k = n.Kicad;
                Assert.Equal(["gruppe", "farbe", "deckkraft", "deckkraft3d", "dreiecke", "elemente"], k.Select(p => p.Key).ToArray());
                Assert.Equal(n.Dreiecke, (int)k["dreiecke"]!);
                var el = (JsonObject)k["elemente"]!;
                Assert.Equal(["ids", "ab", "anzahl", "ebenen"], el.Select(p => p.Key).ToArray());
                var ab = el["ab"]!.AsArray().Select(x => (int)x!).ToList();
                var anzahl = el["anzahl"]!.AsArray().Select(x => (int)x!).ToList();
                Assert.Equal(0, ab[0]);
                for (int i = 1; i < ab.Count; i++) Assert.Equal(ab[i - 1] + anzahl[i - 1], ab[i]);
                Assert.Equal(n.Dreiecke, ab[^1] + anzahl[^1]);
                // Material wie GlbSchreiber: Farbe linear, Deckkraft in Alpha, BLEND unter 1
                var farbe = GlbSchreiber.Linear((string)k["farbe"]!);
                var bc = n.Material["pbrMetallicRoughness"]!["baseColorFactor"]!.AsArray().Select(x => (double)x!).ToArray();
                for (int c = 0; c < 3; c++) Nah(farbe[c], bc[c], 1e-6);
                Nah((double)k["deckkraft"]!, bc[3], 1e-6);
                Assert.Equal((double)k["deckkraft"]! < 1 ? "BLEND" : "OPAQUE", (string)n.Material["alphaMode"]!);
                Assert.StartsWith($"{(string)k["gruppe"]!} {(string)k["farbe"]!} ", n.Name);
            }
        }
    }

    [Fact]
    public void Gruppen_Ebenen_Farben_wie_die_Koerper_von_KICAD()
    {
        var g = Glb("EG");
        var k = Stand.Katalog;
        (string Gruppe, string Farbe, double Vr, double D3, string? Ebene) Art(string id)
        {
            var (n, _, _, ebene) = g.Element(id)!.Value;
            return ((string)n.Kicad["gruppe"]!, (string)n.Kicad["farbe"]!, (double)n.Kicad["deckkraft"]!, (double)n.Kicad["deckkraft3d"]!, ebene);
        }
        Assert.Equal(("architektur", k.Farben["wand"], 1.0, XrFarben.WandDeckkraft3d, "wand"), Art("wand-eg-sued"));   // Wand in VR undurchsichtig, 3D 0.28
        Assert.Equal(("architektur", k.Farben["platte"], 1.0, 1.0, "decke"), Art("boden-eg"));
        Assert.Equal(("architektur", k.Farben["platte"], 1.0, 1.0, "decke"), Art("decke-eg"));
        Assert.Equal(("raum", XrFarben.Hell["raum"], 0.15, 0.15, "raum"), Art("testraum-eg"));
        Assert.Equal(("sonst", k.Farben["markierung"], 1.0, 1.0, "sonst"), Art("markierung-eg-sw"));
        // nur die Elemente dieser Etage
        Assert.Null(g.Element("wand-og-sued"));
        Assert.Equal(k.Elemente.Count(e => e.Geschoss == "EG"), g.Netze.Sum(n => n.Kicad["elemente"]!["ids"]!.AsArray().Count));
    }

    [Fact]
    public void Koordinaten_wie_KICAD_x_Ost_y_oben_z_minus_Nord_in_Metern()
    {
        var eg = Glb("EG");
        // Raum EG: x 0 … 8 m, y 0 … 2.65 m, z −6 … 0 m (Nord = −z)
        var r = eg.Box("testraum-eg");
        double[] soll = [0, 0, -6, 8, 2.65, 0];
        for (int i = 0; i < 6; i++) Nah(soll[i], r[i]);
        // Wand Süd: Achse y = −100 mm, Dicke 200 → z 0 … 0.2 m, über die ganze Länge −0.2 … 8.2 m
        var w = eg.Box("wand-eg-sued");
        soll = [-0.2, 0, 0, 8.2, 2.65, 0.2];
        for (int i = 0; i < 6; i++) Nah(soll[i], w[i]);
        // Markierung Südwest: Ring um (2, −1.5) mit 0.3 m, 1 … 6 mm über dem Boden
        var m = eg.Box("markierung-eg-sw");
        Nah(1.7, m[0]); Nah(2.3, m[3]); Nah(-1.8, m[2]); Nah(-1.2, m[5]); Nah(0.001, m[1]); Nah(0.006, m[4]);
        // OG eine Etage höher; der Boden des OG und die Decke des EG liegen gleich
        var og = Glb("OG");
        Nah(2.9, og.Box("testraum-og")[1]);
        Assert.Equal(eg.Box("decke-eg"), og.Box("boden-og"));
        Nah(5.8, og.Box("decke-og")[4]);
    }

    [Fact]
    public void Ziele_der_Pruefaufgaben_in_den_extras_je_Etage()
    {
        var eg = Glb("EG").Labor;
        Assert.Equal(["art", "fassung", "geschoss", "raeume", "markierungen", "wandpaare", "bauteile"], eg.Select(p => p.Key).ToArray());
        Assert.Equal("hlks-labor-testraum", (string)eg["art"]!);
        Assert.Equal(1, (int)eg["fassung"]!);
        Assert.Equal("EG", (string)eg["geschoss"]!);
        var raum = eg["raeume"]!.AsArray().Single()!;
        Assert.Equal(("testraum-eg", "T.EG", "Testraum EG", 0.0, true), ((string)raum["id"]!, (string)raum["kennzeichen"]!, (string)raum["name"]!, (double)raum["bodenMm"]!, (bool)raum["raumArOeffnen"]!));
        Assert.Equal(["markierung-eg-sw", "markierung-eg-so", "markierung-eg-nw", "markierung-eg-no"], eg["markierungen"]!.AsArray().Select(x => (string)x!["id"]!).ToArray());
        var m = eg["markierungen"]![0]!;
        Assert.Equal([2000.0, 1500.0, 0.0], m["mitteMm"]!.AsArray().Select(x => (double)x!).ToArray());
        Assert.Equal(300, (double)m["radiusMm"]!);
        var p = eg["wandpaare"]!.AsArray().Single()!;
        Assert.Equal(("wandpaar-eg", "testraum-eg", 6000.0), ((string)p["id"]!, (string)p["raum"]!, (double)p["sollMm"]!));
        Assert.Equal("wand-eg-sued", (string)p["a"]!["wand"]!);
        Assert.Equal([0.0, 0.0, 0.0], p["a"]!["vonMm"]!.AsArray().Select(x => (double)x!).ToArray());
        Assert.Equal([8000.0, 0.0, 0.0], p["a"]!["bisMm"]!.AsArray().Select(x => (double)x!).ToArray());
        Assert.Equal([0.0, 1.0, 0.0], p["a"]!["normale"]!.AsArray().Select(x => (double)x!).ToArray());
        Assert.Equal([0.0, 6000.0, 0.0], p["b"]!["vonMm"]!.AsArray().Select(x => (double)x!).ToArray());
        var b = eg["bauteile"]!.AsArray().Single()!;
        Assert.Equal(("wand-eg-ost", "tablet-wert-ablesen", 2650.0), ((string)b["id"]!, (string)b["aufgabe"]!, (double)b["hoeheMm"]!));

        // OG: eigene Markierungen und das Wandpaar West–Ost; der Raum ist nicht der für «raum-ar-oeffnen»
        var og = Glb("OG").Labor;
        Assert.Equal(3, og["markierungen"]!.AsArray().Count);
        Assert.All(og["markierungen"]!.AsArray(), x => Assert.Equal(2900, (double)x!["mitteMm"]![2]!));
        Assert.Equal(8000, (double)og["wandpaare"]![0]!["sollMm"]!);
        Assert.False((bool)og["raeume"]![0]!["raumArOeffnen"]!);
        // die Fläche des Paars liegt dort, wo die Wand in der GLB ist (Innenfläche der Wand West: x = 0)
        Nah(0, Glb("OG").Box("wand-og-west")[3]);
        Assert.Equal(0, (double)og["wandpaare"]![0]!["a"]!["vonMm"]![0]!);
    }

    [Fact]
    public void Liste_wie_KICAD_mit_Schluessel_und_Adresse_je_Etage_ohne_Unterlage()
    {
        var l = TestraumModell.Liste(Stand);
        Assert.Equal(["fassung", "projekt", "bezugspunktMm", "achsen", "geschosse", "hinweise"], l.Select(p => p.Key).ToArray());
        var g = l["geschosse"]!.AsArray();
        Assert.Equal(["EG", "OG"], g.Select(x => (string)x!["id"]!).ToArray());
        foreach (var x in g)
        {
            Assert.Equal(["id", "kennzeichen", "name", "koteMm", "hoeheMm", "modell", "unterlage"], x!.AsObject().Select(p => p.Key).ToArray());
            Assert.Null(x["unterlage"]);
            var e = Stand.Etage((string)x["id"]!)!;
            Assert.Equal(e.Schluessel, (string)x["modell"]!["schluessel"]!);
            Assert.Matches("^[0-9a-f]{24}$", e.Schluessel);
            Assert.Equal($"/api/xr/geschoss/{e.Geschoss.Id}.glb?teil=modell", (string)x["modell"]!["url"]!);
            Assert.Equal(e.Elemente, (int)x["modell"]!["elemente"]!);
        }
        Assert.Same(Stand.Etage("EG"), Stand.Etage("eg"));   // auch nach Kennzeichen, wie XrStand.Geschoss
    }

    [Fact]
    public void Schluessel_bleibt_beim_Neubau_und_aendert_sich_nur_in_der_geaenderten_Etage()
    {
        var neu = TestraumModell.Bauen(TestraumKatalog.Laden(TestraumHilfe.KatalogOrdner()));
        Assert.Equal(Stand.Etagen.Select(e => e.Schluessel), neu.Etagen.Select(e => e.Schluessel));
        Assert.Equal(Stand.Etage("EG")!.Glb, neu.Etage("EG")!.Glb);   // gleiche Bytes
        var j = TestraumHilfe.KatalogJson();
        j["elemente"]!.AsArray().Single(e => (string)e!["id"]! == "markierung-og-nord")!["mitteMm"] = new JsonArray(4000, 4500);
        var anders = TestraumModell.Bauen(TestraumKatalog.Lesen(j.ToJsonString()));
        Assert.Equal(Stand.Etage("EG")!.Schluessel, anders.Etage("EG")!.Schluessel);
        Assert.NotEqual(Stand.Etage("OG")!.Schluessel, anders.Etage("OG")!.Schluessel);
        // Farbe der Wände: alle Etagen
        j["farben"]!["wand"]!["farbe"] = "#E0E0E0";
        var farbe = TestraumModell.Bauen(TestraumKatalog.Lesen(j.ToJsonString()));
        Assert.All(farbe.Etagen, e => Assert.NotEqual(anders.Etage(e.Geschoss.Id)!.Schluessel, e.Schluessel));
    }
}
