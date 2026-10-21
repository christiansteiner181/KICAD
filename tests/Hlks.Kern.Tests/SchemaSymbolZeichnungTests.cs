using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5, Teil A-S 2: Darstellung der Katalogsymbole im Zeichenmodell des Schemas, nach den Regeln der HKAP (PrinzipschemaPdf.DrawGeo,
/// index.html psGeoZeichnen). Handrechnung: HKAP-Punkt (x, y) mit y nach unten → Schema (x, −y) mal Massstab; um 90° gegen den Uhrzeigersinn gedreht
/// (x, y) → (−y, x). Bogen: HKAP im Uhrzeigersinn ab 3 Uhr, start s, sweep w → y nach oben von −s nach −(s+w): w &gt; 0 → von = −(s+w), bis = −s;
/// w &lt; 0 → von = −s, bis = −s + |w| (jeweils mod 360).</summary>
public class SchemaSymbolZeichnungTests
{
    static KatalogSymbol Sym(string einbau, params KatalogGeo[] geo) => new() { Key = "t", Einbau = einbau, Geo = [.. geo] };
    static KatalogGeo G(string t, double[] p, Action<KatalogGeo>? f = null) { var g = new KatalogGeo { T = t, P = p }; f?.Invoke(g); return g; }

    // ------------------------------------------------------------------ Primitivtypen

    [Fact]
    public void Linie_Y_Gespiegelt_Standardstrich_Mittel_Strichelung()
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest", G("line", [1, 2, 4, -3]), G("line", [0, 0, 5, 0], g => g.Dash = true)), 1);
        Assert.Equal(2, pr.Count);
        Assert.Equal("linie", pr[0].Art);
        Assert.Equal([[1, -2], [4, 3]], pr[0].Punkte);
        Assert.Equal(Stifte.Mittel, pr[0].Stil.Stift);   // Standard 0.35 mm
        Assert.Equal("voll", pr[0].Stil.Linie);
        Assert.Equal("strich", pr[1].Stil.Linie);
        Assert.Null(pr[0].Stil.Farbe);
    }

    [Theory]
    [InlineData(0.0, 1.0, "mittel")]    // Standard 0.35
    [InlineData(0.4, 1.0, "mittel")]    // 0.4 liegt näher an 0.35 als an 0.70 (Apparatumriss einzelner Symbole)
    [InlineData(0.7, 1.0, "dick")]
    [InlineData(0.1, 1.0, "duenn")]
    [InlineData(0.0, 0.5, "duenn")]     // 0.175 mm
    [InlineData(0.0, 2.0, "dick")]      // 0.70 mm
    public void Strichstaerke_Auf_Den_Naechsten_Stift(double w, double massstab, string stift) =>
        Assert.Equal(stift, SchemaSymbolZeichnung.Primitive(Sym("fest", G("line", [0, 0, 1, 0], g => g.W = w)), massstab)[0].Stil.Stift);

    [Fact]
    public void Rechteck_Wird_Polygon_In_Der_Reihenfolge_Der_Quelle_Und_Mit_Massstab()
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest", G("rect", [-2, -3, 4, 6])), 0.5);
        Assert.Single(pr);
        Assert.Equal("polygon", pr[0].Art);
        Assert.Equal([[-1, 1.5], [1, 1.5], [1, -1.5], [-1, -1.5]], pr[0].Punkte);
    }

    [Fact]
    public void Kreis_Radius_Mal_Massstab_Mitte_Gespiegelt()
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest", G("circle", [1, 2, 3])), 2);
        Assert.Equal("kreis", pr[0].Art);
        Assert.Equal([[2, -4]], pr[0].Punkte);
        Assert.Equal(6, pr[0].Radius);
    }

    [Theory]
    [InlineData(0.0, 90.0, 270.0, 360.0)]       // 3 Uhr bis 6 Uhr im Uhrzeigersinn = unten rechts: im Schema von 270° bis 360°
    [InlineData(0.0, -90.0, 0.0, 90.0)]         // 3 Uhr bis 12 Uhr gegen den Uhrzeigersinn = oben rechts: 0° bis 90°
    [InlineData(0.0, 180.0, 180.0, 360.0)]      // Halbkreis von 3 Uhr über 6 Uhr nach 9 Uhr (unten): 180° bis 360°
    [InlineData(180.0, 180.0, 0.0, 180.0)]      // von 9 Uhr über 12 Uhr nach 3 Uhr im Uhrzeigersinn (oben): 0° bis 180°
    [InlineData(270.0, 90.0, 0.0, 90.0)]        // 12 Uhr bis 3 Uhr
    public void Bogen_Winkel_Im_Uhrzeigersinn_Werden_Gegenuhrzeiger(double start, double sweep, double von, double bis)
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest", G("arc", [0, 0, 5, start, sweep])), 1);
        Assert.Single(pr);
        Assert.Equal("bogen", pr[0].Art);
        Assert.Equal(von, pr[0].Von!.Value, 6);
        Assert.Equal(bis, pr[0].Bis!.Value, 6);
        Assert.Equal(5, pr[0].Radius);
    }

    [Fact]
    public void Gefuellter_Bogen_Ist_Ein_Kreissegment_Der_Umriss_Bleibt_Der_Bogen()
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest", G("arc", [0, 0, 5, 0, 180], g => g.Fill = true)), 1);
        Assert.Equal(2, pr.Count);
        Assert.Equal("polygon", pr[0].Art);
        Assert.Null(pr[0].Stil.Stift);                      // Füllung ohne Umriss
        Assert.Equal("schnitt", pr[0].Stil.Fuellung);       // ohne fc: in der Linienfarbe
        Assert.Equal(19, pr[0].Punkte.Count);               // 18 Schritte à 10° + 1
        Assert.Equal([5, 0], pr[0].Punkte[0]);              // Anfang 3 Uhr
        Assert.Equal([-5, 0], pr[0].Punkte[^1]);            // Ende 9 Uhr, über die Sehne geschlossen
        Assert.All(pr[0].Punkte, q => Assert.True(q[1] <= 1e-9));   // Halbkreis unten (HKAP y nach unten = positiv, hier gespiegelt)
        Assert.Equal("bogen", pr[1].Art);
        Assert.Null(pr[1].Stil.Fuellung);
    }

    [Fact]
    public void Polygon_Geschlossen_Und_Offen()
    {
        var pr = SchemaSymbolZeichnung.Primitive(Sym("fest",
            G("poly", [0, 0, 4, 0, 2, 3], g => g.Close = true),
            G("poly", [0, 0, 1, 1, 2, 0, 3, 1], g => g.Close = false)), 1);
        Assert.Equal("polygon", pr[0].Art);
        Assert.Equal([[0, 0], [4, 0], [2, -3]], pr[0].Punkte);
        Assert.Equal("polylinie", pr[1].Art);
        Assert.Equal(4, pr[1].Punkte.Count);
        Assert.Null(pr[1].Stil.Fuellung);
    }

    // ------------------------------------------------------------------ Füllungsfälle

    [Fact]
    public void Fuellung_Fuenf_Faelle_Wie_Die_HKAP()
    {
        var s = Sym("fest",
            G("rect", [0, 0, 1, 1]),                                                          // 0: keine Füllung: durchsichtig
            G("rect", [0, 0, 1, 1], g => g.Fill = true),                                      // 1: fill ohne fc: Linienfarbe
            G("rect", [0, 0, 1, 1], g => { g.Fill = true; g.Fc = "#CCFFE0"; }),               // 2: Füllfarbe, zusätzlich umrandet
            G("circle", [0, 0, 1], g => { g.Fill = true; g.Fc = "#FFFFFF"; }),                // 3: weiss = Papier
            G("rect", [0, 0, 1, 1], g => { g.Fill = true; g.Fc = "#CCFFE0"; g.W = -1; }));    // 4: negative Stärke = ohne Umriss
        var pr = SchemaSymbolZeichnung.Primitive(s, 1);
        Assert.Null(pr[0].Stil.Fuellung);
        Assert.Equal(Stifte.Mittel, pr[0].Stil.Stift);
        Assert.Equal("schnitt", pr[1].Stil.Fuellung);
        Assert.Equal(Stifte.Mittel, pr[1].Stil.Stift);      // Umriss immer, auch bei Füllung in Linienfarbe
        Assert.Equal("#CCFFE0", pr[2].Stil.Fuellung);
        Assert.Equal(Stifte.Mittel, pr[2].Stil.Stift);
        Assert.Equal("papier", pr[3].Stil.Fuellung);
        Assert.Equal("#CCFFE0", pr[4].Stil.Fuellung);
        Assert.Null(pr[4].Stil.Stift);
    }

    [Fact]
    public void Standardfarbe_Des_Symbols_Faerbt_Linien_Und_Fuellung_In_Linienfarbe()
    {
        var s = Sym("fest", G("circle", [0, 0, 1], g => g.Fill = true), G("line", [0, 0, 1, 0]));
        s.StandardFarbe = "#FF00CC";
        var pr = SchemaSymbolZeichnung.Primitive(s, 1);
        Assert.Equal("#FF00CC", pr[0].Stil.Farbe);
        Assert.Equal("#FF00CC", pr[1].Stil.Farbe);
        Assert.Equal("schnitt", pr[0].Stil.Fuellung);   // mit Farbe → gefüllt in dieser Linienfarbe
    }

    [Fact]
    public void Symbole_In_Der_Leitung_Decken_Die_Leitung_Mit_Papierunterlage_Unter_Allen_Linien()
    {
        var sanduhr = G("poly", [-2, -2.5, 2, -2.5, -2, 2.5, 2, 2.5], g => g.Close = true);
        var linie = G("line", [0, 0, -3, 0]);
        var innen = Sym("senkrecht", linie, sanduhr);
        var pr = SchemaSymbolZeichnung.Primitive(innen, 1);
        Assert.Equal(3, pr.Count);
        Assert.Equal("polygon", pr[0].Art);                  // Unterlage zuerst, auch wenn die Fläche nach der Linie kommt
        Assert.Equal("papier", pr[0].Stil.Fuellung);
        Assert.Null(pr[0].Stil.Stift);
        Assert.Equal("linie", pr[1].Art);
        Assert.Equal("polygon", pr[2].Art);
        Assert.Null(pr[2].Stil.Fuellung);                    // die Fläche selbst bleibt durchsichtig, sie liegt auf der Unterlage
        // Apparate (fest) bleiben durchsichtig: keine Unterlage
        var fest = SchemaSymbolZeichnung.Primitive(Sym("fest", linie, sanduhr), 1);
        Assert.Equal(2, fest.Count);
    }

    // ------------------------------------------------------------------ Texte

    [Fact]
    public void Texte_Zuletzt_Mitte_Am_Punkt_Groesse_Mal_Massstab_Ausrichtung_Fett()
    {
        var s = Sym("fest",
            G("text", [2, 1], g => { g.S = "M"; g.Size = 2.4; g.Bold = true; }),
            G("line", [0, 0, 1, 0]),
            G("text", [0, 0], g => { g.S = "AB"; g.Size = 2; g.Align = "links"; }));
        var pr = SchemaSymbolZeichnung.Primitive(s, 0.5);
        Assert.Equal(["linie", "text", "text"], pr.Select(x => x.Art));       // Texte nach allen Flächen und Linien
        var m = pr[1];
        Assert.Equal("M", m.Text);
        Assert.Equal(1.2, m.Groesse!.Value, 6);
        Assert.Equal("mitte", m.Ausrichtung);
        Assert.True(m.Fett);
        Assert.Equal(1.0, m.Punkte[0][0], 6);
        Assert.Equal(-0.5 - 1.2 * 0.35, m.Punkte[0][1], 6);                  // Textmitte (1, −0.5), Grundlinie eine halbe Kapitalhöhe tiefer
        Assert.Null(m.Stil.Stift);
        Assert.Equal("links", pr[2].Ausrichtung);
        Assert.Null(pr[2].Fett);
    }

    [Fact]
    public void Textplatzhalter_Werden_Vom_Aufrufer_Gefuellt()
    {
        var s = Sym("fest", G("text", [0, 0], g => g.S = "$1"), G("text", [0, 1], g => g.S = "$2"), G("text", [0, 2], g => g.S = "$3"), G("text", [0, 3], g => g.S = "$FS"), G("text", [0, 4], g => g.S = "Dp"));
        // ohne Texte: $1/$2/$3 leer (nicht gezeichnet), «$FS» zeigt den Vorgabetext, fester Text steht
        Assert.Equal(["FS", "Dp"], SchemaSymbolZeichnung.Primitive(s, 1).Select(x => x.Text));
        var t = new Dictionary<string, string> { ["1"] = "B1", ["2"] = "DN 25", ["3"] = " 12 l " };
        Assert.Equal(["B1", "DN 25", "12 l", "B1", "Dp"], SchemaSymbolZeichnung.Primitive(s, 1, false, t).Select(x => x.Text));   // «$xy» zeigt die Bezeichnung
    }

    // ------------------------------------------------------------------ Drehung und Anschlüsse

    [Theory]
    [InlineData("senkrecht", false, true)]    // senkrecht gezeichnet: an waagrechter Leitung drehen
    [InlineData("senkrecht", true, false)]
    [InlineData("waagrecht", false, false)]
    [InlineData("waagrecht", true, true)]
    [InlineData("fest", false, false)]
    [InlineData("fest", true, false)]
    public void Einbaulage_Dreht_Je_Nach_Leitungsrichtung(string einbau, bool leitungSenkrecht, bool gedreht) =>
        Assert.Equal(gedreht, SchemaSymbolZeichnung.Gedreht(Sym(einbau), leitungSenkrecht));

    [Fact]
    public void Drehung_Dreht_Punkte_Boegen_Und_Texte_Um_90_Grad_Gegen_Den_Uhrzeigersinn()
    {
        var s = Sym("senkrecht",
            G("line", [1, 2, 3, 2]),
            G("arc", [0, 0, 5, 0, -90]),
            G("text", [2, 1], g => { g.S = "M"; g.Size = 2; }),
            G("text", [2, 1], g => { g.S = "A"; g.Size = 2; g.Aufrecht = true; }));
        var auf = SchemaSymbolZeichnung.Primitive(s, 1, false);
        var ged = SchemaSymbolZeichnung.Primitive(s, 1, true);
        // Linie: Schema (1, −2) → (2, 1); (3, −2) → (2, 3)
        Assert.Equal([[2, 1], [2, 3]], ged.First(x => x.Art == "linie").Punkte);
        // Bogen: Winkel +90°
        var b0 = auf.First(x => x.Art == "bogen"); var b1 = ged.First(x => x.Art == "bogen");
        Assert.Equal(b0.Von!.Value + 90, b1.Von!.Value, 6);
        Assert.Equal(b0.Bis!.Value + 90, b1.Bis!.Value, 6);
        // Text dreht mit (Winkel 90), «aufrecht» bleibt waagrecht; Textmitte (2, −1) → (1, 2)
        var t = ged.Where(x => x.Art == "text").ToList();
        Assert.Equal("M", t[0].Text);
        Assert.Equal(90, t[0].Winkel);
        Assert.Equal(1 + 2 * 0.35, t[0].Punkte[0][0], 6);   // Grundlinie in Textrichtung (nach links unten wird nach rechts): Versatz (0, −0.7) gedreht = (0.7, 0)
        Assert.Equal(2, t[0].Punkte[0][1], 6);
        Assert.Null(t[1].Winkel);
        Assert.Equal(1, t[1].Punkte[0][0], 6);
        Assert.Equal(2 - 0.7, t[1].Punkte[0][1], 6);
    }

    [Fact]
    public void Anschluesse_Drehen_Mit_Richtung()
    {
        var s = Sym("senkrecht");
        s.Anschluesse = [new() { Id = "A1", X = 0, Y = -2.5, Dir = "N" }, new() { Id = "A2", X = 0, Y = 2.5, Dir = "S" }, new() { Id = "A3", X = -7.5, Y = 0, Dir = "W" }, new() { Id = "A4", X = 3, Y = 0, Dir = "E" }];
        var a = SchemaSymbolZeichnung.Anschluesse(s, 1);
        Assert.Equal(new SymbolPunkt("A1", 0, 2.5, "N"), a[0]);    // HKAP y = −2.5 (oben) → Schema y = +2.5
        Assert.Equal(new SymbolPunkt("A3", -7.5, 0, "W"), a[2]);
        var g = SchemaSymbolZeichnung.Anschluesse(s, 1, true);
        // (x, y) → (−y, x): A1 (0, 2.5) → (−2.5, 0), Richtung N → W; A2 (0, −2.5) → (2.5, 0), S → E; A3 (−7.5, 0) → (0, −7.5), W → S; A4 (3, 0) → (0, 3), E → N
        Assert.Equal(new SymbolPunkt("A1", -2.5, 0, "W"), g[0]);
        Assert.Equal(new SymbolPunkt("A2", 2.5, 0, "E"), g[1]);
        Assert.Equal(new SymbolPunkt("A3", 0, -7.5, "S"), g[2]);
        Assert.Equal(new SymbolPunkt("A4", 0, 3, "N"), g[3]);
        // Massstab
        Assert.Equal(1.25, SchemaSymbolZeichnung.Anschluesse(s, 0.5)[0].Y);
    }

    // ------------------------------------------------------------------ Katalogsymbole real

    [Fact]
    public void Kugelhahn_Aus_Dem_Katalog_Sanduhr_Mit_Weissem_Kreis_Gedreht_An_Waagrechter_Leitung()
    {
        var k = Kat.Schemasymbole!.Symbol("kugelhahn")!;
        Assert.Equal("senkrecht", k.Einbau);
        var pr = SchemaSymbolZeichnung.Primitive(k, Kat.Schemasymbole.Massstab);
        // Unterlage (Sanduhr), Sanduhr, weisser Kreis r 0.75
        Assert.Equal(3, pr.Count);
        Assert.Equal("papier", pr[0].Stil.Fuellung);
        var kreis = pr.Single(x => x.Art == "kreis");
        Assert.Equal("papier", kreis.Stil.Fuellung);
        Assert.Equal(0.75 * Kat.Schemasymbole.Massstab, kreis.Radius!.Value, 6);
        var b = Grafik.Begrenzung([new Grafikteil { Primitive = pr }])!;
        Assert.Equal(-2 * Kat.Schemasymbole.Massstab, b[0], 6);   // 4 mm breit, 5 mm hoch
        Assert.Equal(2.5 * Kat.Schemasymbole.Massstab, b[3], 6);
        var ged = SchemaSymbolZeichnung.Primitive(k, Kat.Schemasymbole.Massstab, true);
        var bg = Grafik.Begrenzung([new Grafikteil { Primitive = ged }])!;
        Assert.Equal(-2.5 * Kat.Schemasymbole.Massstab, bg[0], 6);   // gedreht: 5 mm breit, 4 mm hoch
        Assert.Equal(2 * Kat.Schemasymbole.Massstab, bg[3], 6);
    }

    [Fact]
    public void Zeichnen_Im_Schema_Dreht_Die_Armatur_Mit_Der_Leitung_Und_Sanitaer_Bleibt()
    {
        var p = Projekt.Anlegen(Kat, null, "Test Zeichnen (fiktiv)", "T", "pc");
        void Sys(string kz, string typ) => Ok(p.Ausfuehren("element_anlegen", new System.Text.Json.Nodes.JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new System.Text.Json.Nodes.JsonObject { ["systemtyp"] = typ } }));
        void V(string kz, string sys) => Ok(p.Ausfuehren("element_anlegen", new System.Text.Json.Nodes.JsonObject { ["klasse"] = "Ventil", ["kennzeichen"] = kz, ["system"] = sys, ["eigenschaften"] = new System.Text.Json.Nodes.JsonObject { ["art"] = "absperrventil" } }));
        Sys("HZ", "Heizung Vorlauf"); Sys("KW", "Trinkwasser kalt");
        V("V-HZ", "HZ"); V("V-KW", "KW");
        var m = Kat.Schemasymbole!.Massstab;
        // Heizung: Kugelhahn (4 x 5 mm, senkrecht gezeichnet); an waagrechter Leitung gedreht (5 x 4), an senkrechter wie gezeichnet
        var (_, hxw, hyw) = SchemaSymbole.Zeichnen(p, Hole(p, "V-HZ"), 0, 0, false, null, "mittel");
        var (prs, hxs, hys) = SchemaSymbole.Zeichnen(p, Hole(p, "V-HZ"), 0, 0, true, null, "mittel");
        Assert.Equal((2.5 * m, 2 * m), (hxw, hyw));
        Assert.Equal((2 * m, 2.5 * m), (hxs, hys));
        Assert.Contains(prs, x => x.Art == "kreis");
        // Sanitär: bisherige Klassendarstellung (zwei Dreiecke 4 x 3 mm), unverändert und ohne Katalogsymbol
        var (prk, hxk, hyk) = SchemaSymbole.Zeichnen(p, Hole(p, "V-KW"), 0, 0, false, null, "mittel");
        Assert.Equal(2, prk.Count);
        Assert.All(prk, x => { Assert.Equal("polygon", x.Art); Assert.Equal(Stifte.Duenn, x.Stil.Stift); Assert.Equal("papier", x.Stil.Fuellung); });
        Assert.Equal((2.0, 1.5), (hxk, hyk));
    }

    [Fact]
    public void Alle_Sichtbaren_Symbole_Lassen_Sich_Zeichnen_Mit_Gueltigen_Werten()
    {
        var sk = Kat.Schemasymbole!;
        foreach (var s in sk.Symbole)
            foreach (var gedreht in new[] { false, true })
            {
                var pr = SchemaSymbolZeichnung.Primitive(s, sk.Massstab, gedreht);
                foreach (var p in pr)
                {
                    Assert.All(p.Punkte, q => { Assert.True(double.IsFinite(q[0]) && double.IsFinite(q[1]), s.Key); });
                    if (p.Art is "kreis" or "bogen") Assert.True(p.Radius > 0, s.Key);
                    if (p.Art is "polygon" or "polylinie") Assert.True(p.Punkte.Count >= 2, s.Key);
                    if (p.Art == "bogen") Assert.True(p.Bis > p.Von && p.Von >= 0 && p.Von < 360, s.Key);
                }
                // jede nicht-leere Geometrie ergibt Primitive (Beschriftungen mit nur «$»-Texten ausgenommen)
                if (s.Geo.Any(g => g.T != "text")) Assert.NotEmpty(pr);
            }
    }

    [Fact]
    public void Armaturen_Und_Pumpen_Haben_Eine_Einbaulage_Apparate_Stehen_Fest()
    {
        var sk = Kat.Schemasymbole!;
        foreach (var key in new[] { "kugelhahn", "pumpe", "strangregulierventil", "eig-3_wegventil", "eig-absperrventil___kopie", "eig-waermezaehler", "klappe" })
            Assert.Equal("senkrecht", sk.Symbol(key)!.Einbau);
        Assert.Equal("waagrecht", sk.Symbol("schmutzfaenger")!.Einbau);
        foreach (var key in new[] { "kessel", "speicher", "wt_platte", "heizgruppe", "fuehler", "eig-expansionsgefaess_sd", "sicherheitsventil", "entleerung", "manometer" })
            Assert.Equal("fest", sk.Symbol(key)!.Einbau);
    }
}
