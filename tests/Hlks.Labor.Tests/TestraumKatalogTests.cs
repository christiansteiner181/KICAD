// XR-Labor L1: Katalog des Testraums (kataloge/labor_testraum.json) – gültig, Herkunft der Masse und Farben wie angegeben, Ziele für alle Prüfaufgaben, Fehler mit Ort.

using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Labor.Tests;

public class TestraumKatalogTests
{
    static TestraumKatalog Katalog() => TestraumKatalog.Laden(TestraumHilfe.KatalogOrdner());

    [Fact]
    public void Der_Katalog_im_Repository_ist_gueltig_mit_zwei_Etagen_und_einem_Raum_je_Etage()
    {
        var k = Katalog();
        Assert.Equal("labor-testraum", k.Projekt);
        Assert.Equal(["EG", "OG"], k.Geschosse.Select(g => g.Id).ToArray());
        foreach (var g in k.Geschosse)
        {
            var raeume = k.Elemente.Where(e => e.IstRaum && e.Geschoss == g.Id).ToList();
            Assert.Single(raeume);
            Assert.Equal(4, k.Elemente.Count(e => e.IstWand && e.Geschoss == g.Id));
            Assert.Single(k.Elemente, e => e.Art == TestraumKatalog.ArtBoden && e.Geschoss == g.Id);
            Assert.Single(k.Elemente, e => e.Art == TestraumKatalog.ArtDecke && e.Geschoss == g.Id);
            // Annahme L1: Raum innen 8.0 × 6.0 m, lichte Höhe = Geschosshöhe − Platte
            var r = raeume[0];
            Assert.Equal(8000, r.Umriss!.Max(p => p[0]) - r.Umriss!.Min(p => p[0]));
            Assert.Equal(6000, r.Umriss!.Max(p => p[1]) - r.Umriss!.Min(p => p[1]));
            Assert.Equal(g.KoteMm, r.UntenMm);
            var decke = k.Elemente.Single(e => e.Art == TestraumKatalog.ArtDecke && e.Geschoss == g.Id);
            Assert.Equal(g.KoteMm + g.HoeheMm, decke.ObenMm);
            Assert.Equal(decke.UntenMm, r.ObenMm);
            // Wände von OK Boden bis UK Decke: berühren die Platten, ohne sie zu überlappen
            Assert.All(k.Elemente.Where(e => e.IstWand && e.Geschoss == g.Id), w => { Assert.Equal(g.KoteMm, w.UntenMm); Assert.Equal(decke.UntenMm, w.ObenMm); });
        }
        // Platte zwischen EG und OG: Decke des EG und Boden des OG haben dieselbe Lage (siehe Beschreibung des Katalogs)
        var d = k.Element("decke-eg")!;
        var b = k.Element("boden-og")!;
        Assert.Equal((d.UntenMm, d.ObenMm), (b.UntenMm, b.ObenMm));
        Assert.Equal(d.Umriss!.SelectMany(p => p), b.Umriss!.SelectMany(p => p));
        Assert.Equal(24, k.Schluessel.Length);
    }

    [Fact]
    public void Geschosshoehe_wie_im_Beispielprojekt()
    {
        // Herkunft im Katalog: «wie im Beispielprojekt MFH Musterweg» – EG Kote 0, Höhe 2900; 1.OG Kote 2900, Höhe 2900
        var p = Beispielprojekt.Erzeugen(Hlks.Kern.Katalog.Laden(TestraumHilfe.KatalogOrdner()), null);
        var bsp = p.Modell.DerKlasse("Geschoss").ToDictionary(g => g.Kennzeichen, g => (Kote: g.Zahl("kote"), Hoehe: g.Zahl("hoehe")));
        var k = Katalog();
        Assert.Equal((bsp["EG"].Kote, bsp["EG"].Hoehe), (k.Geschoss("EG")!.KoteMm, k.Geschoss("EG")!.HoeheMm));
        Assert.Equal((bsp["1.OG"].Kote, bsp["1.OG"].Hoehe), (k.Geschoss("OG")!.KoteMm, k.Geschoss("OG")!.HoeheMm));
    }

    [Fact]
    public void Farben_aus_xr_farben_wie_angegeben_und_keine_Gewerkfarbe()
    {
        var farben = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(TestraumHilfe.KatalogOrdner(), "xr_farben.json")))!;
        string App(string id, string feld) => ((string)farben["apps"]!.AsArray().Single(a => (string)a!["id"]! == id)!["hell"]![feld]!).ToLowerInvariant();
        var k = Katalog();
        var j = TestraumHilfe.KatalogJson();
        Assert.Equal(App("vorgabe", "ton"), k.Farben["wand"]);
        Assert.Contains("App «vorgabe», hell.ton", (string)j["farben"]!["wand"]!["quelle"]!);
        Assert.Equal(App("labor", "akzent"), k.Farben["markierung"]);
        Assert.Contains("App «labor», hell.akzent", (string)j["farben"]!["markierung"]!["quelle"]!);
        // nie Gewerkfarben (Christian 24.10.2026: «gewerke werden nur für gewerkeunterschiede gebraucht»)
        var gewerke = File.ReadAllText(Path.Combine(TestraumHilfe.KatalogOrdner(), "gewerke.json")).ToLowerInvariant();
        Assert.All(k.Farben.Values, f => Assert.DoesNotContain(f, gewerke));
    }

    [Fact]
    public void Ziele_fuer_alle_Pruefaufgaben_und_nur_fuer_bekannte()
    {
        var aufgaben = ((JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(TestraumHilfe.KatalogOrdner(), "xr_pruefaufgaben.json")))!)["aufgaben"]!.AsArray()
            .Select(a => (string)a!["id"]!).Order().ToArray();
        var k = Katalog();
        Assert.Equal(aufgaben, k.AufgabenMitEintrag.Order().ToArray());
        // die Aufgaben mit Zielen haben welche in jeder Etage (bis auf den einen Raum für «raum-ar-oeffnen»)
        foreach (var g in k.Geschosse)
        {
            Assert.Contains(k.Markierungen, id => k.Element(id)!.Geschoss == g.Id);
            Assert.Contains(k.WandPaare, p => k.Element(p.Raum)!.Geschoss == g.Id);
            Assert.Contains(k.Bauteile, b => k.Element(b.Bauteil)!.Geschoss == g.Id);
        }
        Assert.Equal("testraum-eg", k.RaumArOeffnenRaum);
        // jede Markierung liegt im Raum ihrer Etage, mindestens 1 m von den Wänden (Springen ohne an die Wand zu stossen)
        foreach (var m in k.Elemente.Where(e => e.IstMarkierung))
        {
            var r = k.Elemente.Single(e => e.IstRaum && e.Geschoss == m.Geschoss);
            Assert.True(m.Mitte![0] - r.Umriss!.Min(p => p[0]) >= 1000 && r.Umriss!.Max(p => p[0]) - m.Mitte[0] >= 1000, m.Id);
            Assert.True(m.Mitte[1] - r.Umriss!.Min(p => p[1]) >= 1000 && r.Umriss!.Max(p => p[1]) - m.Mitte[1] >= 1000, m.Id);
        }
    }

    [Fact]
    public void Sollmass_der_Wandpaare_ist_der_Abstand_der_Innenflaechen()
    {
        var k = Katalog();
        var eg = k.WandPaare.Single(p => p.Id == "wandpaar-eg");
        Assert.Equal(6000, eg.SollMm);
        var (a, b) = TestraumKatalog.Flaechen(k.Element(eg.A)!, k.Element(eg.B)!, k.Element(eg.Raum)!)!.Value;
        Assert.Equal(new double[] { 0, 0, 0 }, a.Von);           // Wand Süd: Achse y = −100, Dicke 200 → Innenfläche y = 0, über die Länge des Raums
        Assert.Equal(new double[] { 8000, 0, 0 }, a.Bis);
        Assert.Equal(new double[] { 0, 1, 0 }, a.Normale);       // in den Raum (nach Norden)
        Assert.Equal(new double[] { 0, 6000, 0 }, b.Von);
        Assert.Equal(new double[] { 0, -1, 0 }, b.Normale);
        Assert.Equal(6000, TestraumKatalog.Abstand(a, b));
        var og = k.WandPaare.Single(p => p.Id == "wandpaar-og");
        var (c, d) = TestraumKatalog.Flaechen(k.Element(og.A)!, k.Element(og.B)!, k.Element(og.Raum)!)!.Value;
        Assert.Equal(8000, TestraumKatalog.Abstand(c, d));
        Assert.Equal(2900, c.Von[2]);                             // am Boden des OG
        Assert.Equal(new double[] { 1, 0, 0 }, c.Normale);
        // nicht gegenüber: Süd und West
        Assert.Null(TestraumKatalog.Flaechen(k.Element("wand-eg-sued")!, k.Element("wand-eg-west")!, k.Element("testraum-eg")!));
        // Bauteil des Tablets: Höhe der Wand = lichte Höhe
        Assert.All(k.Bauteile, x => Assert.Equal(2650, x.HoeheMm));
    }

    static void Fehler(Action<JsonObject> aendern, string teil)
    {
        var j = TestraumHilfe.KatalogJson();
        aendern(j);
        var f = Assert.Throws<HlksFehler>(() => TestraumKatalog.Lesen(j.ToJsonString()));
        Assert.Contains(teil, f.Message);
        Assert.Contains("labor_testraum.json", f.Vorschlag);
    }

    static JsonObject El(JsonObject j, string id) => (JsonObject)j["elemente"]!.AsArray().Single(e => (string)e!["id"]! == id)!;

    [Fact]
    public void Fehler_nennen_den_Ort_im_Katalog()
    {
        Fehler(j => j["art"] = "hlks-xr-farben", "art");
        Fehler(j => j["fassung"] = 2, "fassung");
        Fehler(j => j["farben"]!["wand"]!["farbe"] = "weiss", "farben.wand.farbe");
        Fehler(j => j["geschosse"]![1]!["koteMm"] = 1000, "beginnt, bevor");
        Fehler(j => El(j, "wand-eg-ost")["geschoss"] = "UG", "«wand-eg-ost».geschoss");
        Fehler(j => El(j, "wand-eg-ost")["id"] = "wand-eg-west", "Id doppelt");
        Fehler(j => El(j, "wand-eg-ost")["id"] = "wand eg", "Id nur aus");
        Fehler(j => El(j, "wand-eg-ost")["art"] = "stuetze", "unbekannt");
        Fehler(j => El(j, "wand-eg-ost")["dickeMm"] = 0, "dickeMm");
        Fehler(j => El(j, "boden-eg")["obenMm"] = -300, "obenMm muss über untenMm");
        Fehler(j => El(j, "markierung-eg-sw")["mitteMm"] = new JsonArray(-1000, 1500), "in keinem Raum");
        Fehler(j => j["ziele"]!["messen-wand-wand"]!["wandpaare"]![0]!["sollMm"] = 6020, "die Geometrie ergibt 6000");
        Fehler(j => j["ziele"]!["messen-wand-wand"]!["wandpaare"]![0]!["b"] = "wand-eg-west", "nicht parallel");
        Fehler(j => j["ziele"]!["messen-wand-wand"]!["wandpaare"]![0]!["raum"] = "testraum-og", "derselben Etage");
        Fehler(j => j["ziele"]!["teleport-markierung"]!["markierungen"]![0] = "wand-eg-ost", "kein Element der Art «markierung»");
        Fehler(j => j["ziele"]!["raum-ar-oeffnen"]!["raum"] = "fehlt", "«fehlt» gibt es nicht");
        Fehler(j => j["ziele"]!["tablet-wert-ablesen"]!["bauteile"]![0]!["hoeheMm"] = 2900, "die Geometrie ergibt 2650");
        Fehler(j => j["ziele"]!["tablet-wert-ablesen"]!["bauteile"]![0]!["bauteil"] = "markierung-eg-sw", "nur Wände und Platten");
        Assert.Throws<HlksFehler>(() => TestraumKatalog.Lesen("{ kein json"));
        var weg = Path.Combine(Path.GetTempPath(), "hlks_testraum_leer_" + Guid.NewGuid().ToString("N")[..8]);
        var f = Assert.Throws<HlksFehler>(() => TestraumKatalog.Laden(weg));
        Assert.Contains("fehlt", f.Message);
    }
}
