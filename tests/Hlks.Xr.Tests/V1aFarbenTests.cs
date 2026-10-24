// Tests Quest App V1a, Vorgaben Christian 24.10.2026 nach den Bildern 02: «das grau ist nicht so ansprechend. verwende unsere pastelfarben» (App-Pastellreihe im
// eigenen Katalog kataloge/xr_farben.json, nicht die Gewerkfarben) und «nutze die gleiche symbolsprache wie im cad» (Texturen aus symbole.js, Assets/symbole).

using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Bedienung;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class V1aFarbenTests
{
    static string KatalogText() => File.ReadAllText(Path.Combine(Repo(), "kataloge", "xr_farben.json"));
    static AppFarben Katalog() => AppFarben.Lesen(KatalogText());

    [Fact]
    public void Katalog_hat_die_App_Pastellreihe_von_Christian_mit_Quelle_und_alle_Apps()
    {
        var k = Katalog();
        Assert.All(AppFarben.Pflicht, id => Assert.True(k.Apps.ContainsKey(id), id));
        // Christians Startwerte (Fläche, helle Fassung) genau so
        foreach (var (id, hex) in new[] { ("etagen", "#D6E6F5"), ("messen", "#D9F0E6"), ("marker", "#F8E0D2"), ("tisch", "#E6DDF3"), ("ort", "#F3ECD6"), ("status", "#DDE7EE") })
        {
            Assert.Equal(AppFarben.Hex(hex), k.Apps[id].Hell.Ton);
            Assert.Equal("Christian 24.10.2026, App-Pastellreihe", k.Apps[id].Quelle);
        }
        Assert.Equal(AppFarben.Hex("#1F1F1F"), k.SchriftHell);   // Schrift dunkel
        // keine Gewerkfarbe (kataloge/gewerke.json) in der App
        var gewerke = JsonNode.Parse(File.ReadAllText(Path.Combine(Repo(), "kataloge", "gewerke.json")))!["gewerke"]!.AsArray()
            .SelectMany(g => new[] { (string)g!["pastell"]!, (string)g["koordination"]! }).Select(h => AppFarben.Hex(h)).ToHashSet();
        Assert.All(k.Apps.Values, a => Assert.DoesNotContain(a.Hell.Ton, gewerke));
    }

    [Fact]
    public void Akzent_ist_eine_dunklere_Stufe_desselben_Tons_und_jede_Schrift_ist_lesbar()
    {
        var k = Katalog();
        foreach (var a in k.Apps.Values)
        {
            // hell: Akzent dunkler als der Ton, gleicher Farbton (ungefähr), weisse bzw. dunkle Schrift darauf mit Kontrast ≥ 4.5 (WCAG 2.x, normaler Text)
            Assert.True(AppFarben.Leuchtdichte(a.Hell.Akzent) < AppFarben.Leuchtdichte(a.Hell.Ton) / 3, a.Id);
            Assert.True(Math.Max(AppFarben.Kontrast(a.Hell.Akzent, k.SchriftHell), AppFarben.Kontrast(a.Hell.Akzent, k.SchriftDunkel)) >= 4.5, a.Id);
            Assert.True(AppFarben.Kontrast(a.Hell.Ton, k.SchriftHell) >= 7, a.Id);       // dunkle Schrift auf dem Pastell
            Assert.True(AppFarben.Kontrast(a.Dunkel.Ton, k.SchriftDunkel) >= 4.5, a.Id);  // helle Schrift auf der dunklen Fassung
            Assert.True(AppFarben.Leuchtdichte(a.Dunkel.Ton) < 0.2, a.Id);                // die dunkle Fassung ist dunkel (Tönung über dunklem Grund)
        }
    }

    [Fact]
    public void Fassung_und_Deckkraft_nach_der_Umgebung_Schrift_überall_lesbar()
    {
        var k = Katalog();
        Assert.True(k.IstHell(GlasStil.HintergrundHell) && k.IstHell(GlasStil.HintergrundModell) && k.IstHell(GlasStil.HintergrundEcht));
        Assert.False(k.IstHell(GlasStil.HintergrundDunkel));   // Sitzungsraum «tron»: dunkle Fassung
        foreach (var bg in new[] { GlasStil.HintergrundHell, GlasStil.HintergrundModell, GlasStil.HintergrundEcht, GlasStil.HintergrundDunkel, 0.15 })
            foreach (var id in AppFarben.Pflicht)
            {
                var s = k.Satz(id, bg, 0.75, 0.42);
                Assert.Equal(k.IstHell(bg), s.Hell);
                Assert.InRange(s.Glas.A, 0.75 - 1e-12, AppFarben.DeckkraftMax);
                // das Glas über dem Hintergrund: die Schrift bleibt lesbar (≥ 4.5)
                var wirkt = AppFarben.Ueber(s.Glas, bg);
                Assert.True(AppFarben.Kontrast(wirkt, s.Text) >= 4.5, $"{id} vor {bg}: {AppFarben.Kontrast(wirkt, s.Text):0.0}");
                Assert.True(AppFarben.Kontrast(s.Ein, s.TextAufEin) >= 4.5, id);
            }
        // die helle Fassung über einem dunklen Grund deckt mehr; die dunkle über einem hellen Grund ebenso
        var ton = k.Fuer("etagen").Hell.Ton;
        Assert.True(AppFarben.Deckkraft(ton, true, 0.05, 0.5, 0.42) > 0.75);
        Nah(AppFarben.Deckkraft(ton, true, 0.88, 0.5, 0.42), 0.5, 1e-12);
        var dunkel = k.Fuer("etagen").Dunkel.Ton;
        Assert.True(AppFarben.Deckkraft(dunkel, false, 0.9, 0.5, 0.42) > 0.5);
        Assert.True(AppFarben.Deckkraft(ton, true, 0, 0.1, 0.42) <= AppFarben.DeckkraftMax);
        // unbekannte App: die Vorgabe
        Assert.Equal(k.Apps[AppFarben.VorgabeId], k.Fuer("gibt-es-nicht"));
        Assert.Equal(k.Apps[AppFarben.VorgabeId], k.Fuer(null));
    }

    [Fact]
    public void Katalog_wird_streng_gelesen()
    {
        var o = JsonNode.Parse(KatalogText())!.AsObject();
        string Mit(Action<JsonObject> aendern) { var c = (JsonObject)o.DeepClone(); aendern(c); return c.ToJsonString(); }
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["art"] = "anders")));
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["fassung"] = 2)));
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["hellAb"] = 3)));
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["apps"]![0]!["hell"]!["ton"] = "rot")));
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["apps"]!.AsArray().RemoveAt(0))));                    // «status» fehlt
        Assert.Throws<FormatException>(() => AppFarben.Lesen(Mit(c => c["apps"]!.AsArray().Add(c["apps"]![1]!.DeepClone()))));   // doppelt
        Assert.Throws<FormatException>(() => AppFarben.Hex("#12345"));
        Assert.Equal(new GlasFarbe(1, 0, 0, 1), AppFarben.Hex("#FF0000"));
        Nah(AppFarben.Kontrast(AppFarben.Hex("#000000"), AppFarben.Hex("#FFFFFF")), 21, 1e-9);
    }

    [Fact]
    public void Symbole_der_App_sind_die_von_KICAD_als_Texturen_in_drei_Sätzen()
    {
        var ordner = Path.Combine(Repo(), "src", "Hlks.Quest", "Assets", "symbole");
        using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(ordner, "symbole.json")));
        var namen = d.RootElement.GetProperty("symbole").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.All(GlasSymbole.Alle, n => Assert.Contains(n, namen));
        foreach (var n in GlasSymbole.Alle)
            foreach (var satz in new[] { GlasSymbole.SatzStrich, GlasSymbole.SatzRevitHell, GlasSymbole.SatzRevitDunkel })
                Assert.True(File.Exists(Path.Combine(ordner, GlasSymbole.Datei(n, satz)["symbole/".Length..].Replace('/', Path.DirectorySeparatorChar))), $"{satz}/{n}");
        // bestehende Schlüssel von KICAD, wo es passt (Auftrag: geschoss, info …), neu: sitzungstisch, gesten, haken
        Assert.Equal("geschoss", GlasSymbole.Etagen);
        Assert.Equal("info", GlasSymbole.Status);
        Assert.Equal("problemMarker", GlasSymbole.Marker);
        Assert.Equal("raumBegehen", GlasSymbole.Begehen);
    }
}
