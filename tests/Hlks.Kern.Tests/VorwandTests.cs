using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Geberit Vorwand (Duofix, GIS): Gestell nach den Montageregeln des Planungshandbuchs, Bestückung, Befunde.</summary>
public class VorwandTests
{
    static JsonObject Stueckliste(Projekt p, string vw) =>
        ((JsonObject)Tuer.Abfrage(p, "vorwand_stueckliste", new JsonObject { ["vorwand"] = vw }))["vorwaende"]!.AsArray().OfType<JsonObject>().Single();

    static int Anzahl(JsonObject sl, string titel, double? zuschnitt = null) =>
        sl["zeilen"]!.AsArray().OfType<JsonObject>().Where(z => (string?)z["titel"] == titel && (zuschnitt is null || (double?)z["zuschnitt"] == zuschnitt))
            .Sum(z => (int)z["anzahl"]!);

    /// <summary>Christian 06.11.2026: «1. aussenkannte der vorwand zeichnen, 2. tiefe am plan abgreiffen» und «wird das vorwand system geändert,
    /// müssen die gewählten einbauten auch ändern».</summary>
    [Fact]
    public void Aussenkante_UndSystemwechsel_ZiehtDieMontageelementeMit()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(1100, 600, 0)) },
        }));
        // Aussenkante bei y = 300, Rohbauwand (Fläche y = 100) rechts davon
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 300], [2100, 300]), ["aussenkante"] = "rechts", ["system"] = "gis", ["tiefe"] = 200, ["hoehe"] = 1140, ["bestuecken"] = true,
        }));
        var vw = Hole(p, "VW-1");
        Assert.Equal(200, vw.Geometrie!.Punkte[0][1], 6);
        Assert.Equal("rechts", vw.Text("rueckseite"));
        Assert.Equal(300, Hole(p, "ME-1").Geometrie!.Punkte[0][1], 6);
        Assert.Equal("gis.wc.sigma12.114", Hole(p, "ME-1").Text("element"));

        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "VW-1", ["eigenschaften"] = new JsonObject { ["system"] = "duofix" } }));
        var me = Hole(p, "ME-1");
        Assert.Equal("duofix.wc.sigma12.112", me.Text("element"));
        Assert.Equal("111.309.00.6", me.Text("artikel"));
        Assert.Equal(Hole(p, "WC-1").Id, me.Bezuege["apparat"]);
        Assert.Contains(e.Hinweise, h => h.Contains("1 Montageelement(e) auf duofix umgestellt"));
    }

    /// <summary>Christian 06.11.2026 mit der Masskizze MA 970.704 S. 2: «das WC muss so ausgerichtet sein, dass der Anschluss +23 … ab Fertig Boden liegt»;
    /// GIS-Rahmen 100 cm hoch, Oberkante 104.5 cm über OKFF, ohne Füsse. Bodenaufbau 100 mm, OKFF = Geschosskote 0.</summary>
    [Theory]
    [InlineData("gis", "gis.wc.sigma12.114")]
    [InlineData("duofix", "duofix.wc.sigma12.112")]
    public void Wc_AbwasseranschlussAuf23cmUeberFertigboden(string system, string element)
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [2100, 100]), ["system"] = system, ["tiefe"] = 200, ["hoehe"] = 1140, ["bezugslinie"] = "rechts", ["bodenaufbau"] = 100,
        }));
        Ok(p.Ausfuehren("montageelement_setzen", new JsonObject { ["vorwand"] = "VW-1", ["element"] = element, ["abstand"] = 1000 }));
        var me = Hole(p, "ME-1");
        var k = Grafik.QuelleFuer(p.Katalog, me)!.Koerper(p, me)!;
        var sw = k.Teile.Single(t => t.Art == "zylinder" && t.Radius == 45);   // WC-Abgang d90
        Assert.Equal(230, sw.Von![2], 6);
        Assert.Equal(230, sw.Nach![2], 6);
        var quader = k.Teile.Where(t => t.Art == "quader").ToList();
        double unten = quader.Min(t => t.Mitte![2] - t.Masse![2] / 2), oben = quader.Max(t => t.Mitte![2] + t.Masse![2] / 2);
        if (system == "gis") { Assert.Equal(45, unten, 6); Assert.Equal(1045, oben, 6); }   // hängt an den Profilen, keine Füsse
        else Assert.Equal(-100 + 34, unten, 6);   // Duofix: Füsse in der Systemschiene (OK Schiene)
    }

    [Fact]
    public void Gis_ProfileNachHandbuch_BestueckungHinterDemWc()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["einbringlaenge"] = 2500 } }));   // länger als die Vorwand: ein Segment
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nutzung"] = "Bad" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geschoss"] = "EG", ["raum"] = "R-1",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(1100, 600, 0)) },
        }));
        // Vorwand an der Innenfläche der Wand y = 0 (Fläche bei y = 100), Wand rechts in Zeichenrichtung
        var e = Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [2100, 100]), ["system"] = "gis", ["tiefe"] = 200, ["hoehe"] = 1140,
            ["bezugslinie"] = "rechts", ["bodenaufbau"] = 100, ["bestuecken"] = true,
        }));
        var vw = Hole(p, "VW-1");
        Assert.Equal(200, vw.Geometrie!.Punkte[0][1], 6);   // Achse = Mitte, 100 mm vor der Wandfläche
        var me = Hole(p, "ME-1");
        Assert.Equal(vw.Id, me.TeilVon);
        Assert.Equal("gis.wc.sigma12.114", me.Text("element"));
        Assert.Equal("461.313.00.6", me.Text("artikel"));
        Assert.Equal(Hole(p, "WC-1").Id, me.Bezuege["apparat"]);
        Assert.Equal(300, me.Geometrie!.Punkte[0][1], 6);   // Vorderseite der Vorwand
        Assert.Equal(1100, me.Geometrie.Punkte[0][0], 6);

        var sl = Stueckliste(p, "VW-1");
        Assert.Empty(sl["befunde"]!.AsArray());
        // S. 251: Oberkanten 5 und 65 cm über OK Rohboden (−100) liegen unter der Ablage, 130/135 nicht mehr; dazu das oberste unter der Ablage
        var auf = VorwandGestell.Ableiten(p.Modell, p.Katalog, vw)!;
        var horiz = auf.Teile.Where(t => t.Titel == "GIS Profil horizontal").Select(t => t.Z1).Order().ToList();
        Assert.Equal([-50, 550, 1122], horiz.Distinct());
        // Nie durch den Spülkasten: auf 65 cm endet das Profil am WC; das unterste (5 cm) läuft durch (Rahmen bis 104.5 cm), oben laufen sie durch (Zuschnitt = lichtes Mass minus 3 cm)
        Assert.Equal(2, Anzahl(sl, "GIS Profil horizontal", 1970));   // unten und oben durchgehend
        Assert.Equal(4, horiz.Count);
        Assert.Contains(auf.Teile, t => t.Titel == "GIS Elementbefestigung T1");   // Wandtiefe 200 mm: T1 (MA 970.704 S. 3)
        // Vertikalprofile an den Enden und beidseits des Elements (Abstand ≤ 250 cm)
        var vert = auf.Teile.Where(t => t.Titel == "GIS Profil vertikal").Select(t => t.S0).Order().ToList();
        Assert.Equal([15, 756, 1212, 1953], vert);   // Profil 32 mm (Datenblatt 461.000.25.1), WC 424 mm breit (Datenblatt 461.313.00.6)
        // Montagewinkel am Boden: erster 10 cm von der Wand, höchstens 100 cm auseinander
        Assert.Equal([100, 1233, 1900], auf.Teile.Where(t => t.Titel == "GIS Montagewinkel (Boden)").Select(t => (t.S0 + t.S1) / 2).Order().ToList());
        Assert.All(auf.Teile.Where(t => t.Titel == "GIS Montagewinkel (Boden)"), t => Assert.Equal("461.185.00.1", t.Artikel));   // Entscheid Christian 04.11.2026
        Assert.Equal(8, Anzahl(sl, "GIS Wandanbindung (Verbindungsstück)"));
        Assert.Contains(sl["zeilen"]!.AsArray().OfType<JsonObject>(), z => (string?)z["artikel"] == "461.313.00.6" && (int)z["anzahl"]! == 1);
        Assert.Contains(sl["annahmen"]!.AsArray(), a => ((string?)a)!.Contains("Breite des Montagewinkels"));

        // Grundriss: Vorwand mit Beplankung als Schnittfläche, Element mit Kurzbezeichnung
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Contains(Teil(g, p, "VW-1").Primitive, x => x.Stil.Fuellung == "schnitt");
        Assert.Contains(Teil(g, p, "ME-1").Primitive, x => x.Text == "WC 114");
        Assert.NotEmpty(Grafik.Koerper(p, [vw.Id, me.Id]));
    }

    [Fact]
    public void Duofix_Raumhoch_StaenderAbstandUndArtikel_TiefeAusserhalb()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [2100, 100]), ["system"] = "duofix", ["tiefe"] = 150, ["hoehe"] = 3000, ["bezugslinie"] = "rechts",
        }));
        var auf = VorwandGestell.Ableiten(p.Modell, p.Katalog, Hole(p, "VW-1"))!;
        Assert.True(auf.Raumhoch);
        var st = auf.Teile.Where(t => t.Art == "staender").ToList();
        Assert.Equal(5, st.Count);   // 2000 mm Länge, Ständer höchstens 55 cm auseinander (S. 191)
        Assert.All(st.Zip(st.Skip(1)), x => Assert.True(x.Second.S0 - x.First.S0 <= 550 + 1e-6));
        Assert.All(st, t => Assert.Equal("111.872.00.2", t.Artikel));   // 300 cm: Ständer 260–320 cm (S. 196–197)
        Assert.Equal(5, auf.Teile.Count(t => t.Titel == "Befestigung Schiene (Boden)"));   // höchstens 50 cm
        Assert.Contains(auf.Annahmen, a => a.Contains("Bodenaufbau fehlt"));
        Assert.Empty(auf.Befunde);
        // Tiefe ausserhalb Tab. 49 (S. 175)
        Nein(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 3900], [2100, 3900]), ["system"] = "duofix", ["tiefe"] = 500, ["hoehe"] = 1140,
        }));
    }

    [Fact]
    public void Gis_Vorgefertigt_InSegmenteGeteilt_StossNieImElement_TuerBegrenzt()
    {
        // Christian 04.11.2026: GIS-Elemente werden in der Werkstatt vorgefertigt und müssen sich einbringen lassen
        var p = Rechteck();
        Ok(Tuer(p, "W-2", 2000, 900));   // lichte Tür 900 × 2100 mm
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["einbringlaenge"] = 3000 } }));
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [4100, 100]), ["system"] = "gis", ["tiefe"] = 200, ["hoehe"] = 3000, ["bezugslinie"] = "rechts",
        }));
        Ok(p.Ausfuehren("montageelement_setzen", new JsonObject { ["vorwand"] = "VW-1", ["element"] = "gis.wc.sigma12.114", ["abstand"] = 2000 }));
        var auf = VorwandGestell.Ableiten(p.Modell, p.Katalog, Hole(p, "VW-1"))!;
        Assert.True(auf.Vorgefertigt && auf.Raumhoch);
        // raumhoch 3000 mm höher als die Tür (2100): Segmente höchstens 2100 lang; Teilung in der Mitte träfe das WC → drei Segmente
        Assert.Equal(3, auf.Segmente.Count);
        Assert.All(auf.Segmente, x => Assert.True(x.S1 - x.S0 <= 2100 + 1e-6));
        var wc = auf.Elemente.Single();
        Assert.Contains(auf.Segmente, x => x.S0 <= wc.S0 && wc.S1 <= x.S1);   // Stoss nie durch das Element
        Assert.Contains("Tür", auf.Einbringgrenze);
        Assert.NotEmpty(auf.Teile.Where(t => t.Titel == "GIS Verbindungslasche (Stoss)"));
        Assert.Equal(3 * 7 + 1, auf.Teile.Count(t => t.Titel == "GIS Profil horizontal"));   // je Segment 5, 65, 130, 135, 200, 265 cm und Decke; 65 cm am WC unterbrochen (+1 Stück)
        Assert.Empty(auf.Befunde);
        // Duofix bleibt vor Ort gebaut: nicht geteilt
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 3900], [4100, 3900]), ["system"] = "duofix", ["tiefe"] = 150, ["hoehe"] = 1140, ["bezugslinie"] = "links",
        }));
        var df = VorwandGestell.Ableiten(p.Modell, p.Katalog, Hole(p, "VW-2"))!;
        Assert.False(df.Vorgefertigt);
        Assert.Empty(df.Segmente);
    }

    /// <summary>Kein Teil des Gestells liegt im Raum eines Montageelements (Spülkasten, Traverse, Füsse) – ausser der Duofix-Bodenschiene,
    /// in die die Füsse einrasten (Handbuch S. 189), und den Befestigungen am Elementrahmen. Christian 04.11.2026: «nicht durch die spühlkasten»,
    /// «prüfe das auch bei duofix».</summary>
    [Theory]
    [InlineData("gis", 1140, 200)]
    [InlineData("gis", 3000, 200)]
    [InlineData("duofix", 1140, 200)]
    [InlineData("duofix", 3000, 220)]
    public void Gestell_NieDurchEinElement(string system, double hoehe, double tiefe)
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["einbringlaenge"] = 1500 } }));
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [3100, 100]), ["system"] = system, ["tiefe"] = tiefe, ["hoehe"] = hoehe, ["bezugslinie"] = "rechts", ["bodenaufbau"] = 100,
        }));
        string wc = system == "gis" ? "gis.wc.sigma12.114" : "duofix.wc.sigma12.112", wt = system == "gis" ? "gis.wt.set" : "duofix.wt.112";
        Ok(p.Ausfuehren("montageelement_setzen", new JsonObject { ["vorwand"] = "VW-1", ["element"] = wc, ["abstand"] = 700 }));
        Ok(p.Ausfuehren("montageelement_setzen", new JsonObject { ["vorwand"] = "VW-1", ["element"] = wt, ["abstand"] = 1900 }));
        var auf = VorwandGestell.Ableiten(p.Modell, p.Katalog, Hole(p, "VW-1"))!;
        Assert.Equal(2, auf.Elemente.Count);
        // Vertikale Streben direkt beidseits jedes Einbaus (S. 186–188; Christian 04.11.2026), Duofix-Ständer höchstens 55 cm auseinander
        string art = system == "gis" ? "profil" : "staender";
        var senkrecht = auf.Teile.Where(t => t.Art == art && t.Z1 - t.Z0 > t.S1 - t.S0 && t.Z1 - t.Z0 > 500).OrderBy(t => t.S0).ToList();
        foreach (var el in auf.Elemente)
        {
            Assert.Contains(senkrecht, t => Math.Abs(t.S1 - el.S0) < 1);
            Assert.Contains(senkrecht, t => Math.Abs(t.S0 - el.S1) < 1);
        }
        if (system == "duofix")
            foreach (var (x, y) in senkrecht.Zip(senkrecht.Skip(1)))
                Assert.True(y.S0 - x.S0 <= 550 + 1e-6 || auf.Elemente.Any(e => e.S0 >= x.S1 - 1 && e.S1 <= y.S0 + 1), $"Ständer {x.S0:0} und {y.S0:0} zu weit auseinander");
        // Christian 06.11.2026 «die unterste schiene schneidet die Einbaukasten … wie im montagehandbuch beschrieben versetzt»: GIS-Elemente hinter den
        // Profilen (MA 970.704 S. 2: 1.8–6 cm hinter der Oberfläche), Duofix-Rahmen auf der Systemschiene (Fuss eingerastet, Planungshandbuch S. 188)
        Assert.Equal(system == "gis" ? auf.Okff : auf.Rohboden + 34, auf.ElementUnterkante);   // GIS hängt an den Profilen (keine Füsse), Duofix auf der Schiene
        double q1 = auf.ElementVorne;
        if (system == "gis") Assert.InRange(auf.Tiefe - q1, 18, 60);
        foreach (var el in auf.Elemente)
        {
            double qe = q1 - el.Typ!.Rahmentiefe, ze = auf.Okff + el.Typ.Rahmenhoehe;
            foreach (var t in auf.Teile)
            {
                if (t.Titel.StartsWith("GIS Elementbefestigung") || t.Art == "beplankung") continue;   // die Befestigung verbindet Element und Profil
                bool schneidet = t.S0 < el.S1 - 0.5 && t.S1 > el.S0 + 0.5 && t.Q0 < q1 - 0.5 && t.Q1 > qe + 0.5 && t.Z0 < ze - 0.5 && t.Z1 > auf.ElementUnterkante + 0.5;
                Assert.False(schneidet, $"{t.Titel} (s {t.S0:0}–{t.S1:0}, q {t.Q0:0}–{t.Q1:0}, z {t.Z0:0}–{t.Z1:0}) liegt im {el.Typ.Titel}");
            }
        }
    }

    [Fact]
    public void Raumhoch_OhneHoehe_NimmtDieRaumhoehe_TeilhochBrauchtHoehe()
    {
        // Christian 04.11.2026: «es muss auch möglich sein raumhohe und teilraumhohe elemente zu erstellen»
        var p = Rechteck();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nutzung"] = "Bad" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "R-1", ["eigenschaften"] = new JsonObject { ["lichteHoehe"] = 2600 } }));
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [2100, 100]), ["system"] = "gis", ["tiefe"] = 200, ["ausfuehrung"] = "raumhoch", ["bezugslinie"] = "rechts",
        }));
        var vw = Hole(p, "VW-1");
        Assert.Equal(2600, vw.Geometrie!.Hoehe);
        Assert.Equal("raumhoch", vw.Text("ausfuehrung"));
        var auf = VorwandGestell.Ableiten(p.Modell, p.Katalog, vw)!;
        Assert.True(auf.Raumhoch);   // obwohl tiefer als die Geschosshöhe 3000: die Ausführung entscheidet
        Assert.Contains(auf.Teile, t => t.Titel == "GIS Montagewinkel (Decke)");
        Nein(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 3900], [2100, 3900]), ["system"] = "gis", ["tiefe"] = 200, ["ausfuehrung"] = "teilhoch",
        }));
    }

    [Fact]
    public void Schnitt_ZeigtGestellInSeinerFarbe_NichtInDerFarbeDerBeplankung()
    {
        // Befund 04.11.2026: Schnitt hinter der Beplankung einer raumhohen Duofix-Vorwand zeigte Ständer und Schienen grün wie die Beplankung
        var p = Rechteck();
        Ok(p.Ausfuehren("vorwand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([100, 100], [2100, 100]), ["system"] = "duofix", ["tiefe"] = 150, ["hoehe"] = 2600, ["ausfuehrung"] = "raumhoch", ["bezugslinie"] = "rechts",
        }));
        // Schnittlinie in der Ebene des Gestells (Beplankung 232–250, Ständer 182–232), Blick zur Rohbauwand
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-S", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["blick"] = "rechts", ["tiefe"] = 300 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(0, 220, 0), new JsonArray(2200, 220, 0)) },
        }));
        var flaechen = Teil(Grafik.Plan(p, "AN-S"), p, "VW-1").Primitive.Where(x => x.Art == "polygon").Select(x => x.Stil.Fuellung).ToList();
        Assert.Contains("#8c96a0", flaechen);              // Ständer und Schienen grau
        Assert.DoesNotContain("#9fd3c0", flaechen);        // keine Beplankung geschnitten
    }
}
