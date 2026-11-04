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
        Assert.Equal([-50, 550, 1122], horiz);
        Assert.Equal(3, Anzahl(sl, "GIS Profil horizontal", 1970));   // Zuschnitt = lichtes Mass minus 3 cm
        // Vertikalprofile an den Enden und beidseits des Elements (Abstand ≤ 250 cm)
        var vert = auf.Teile.Where(t => t.Titel == "GIS Profil vertikal").Select(t => t.S0).Order().ToList();
        Assert.Equal([15, 756, 1212, 1953], vert);   // Profil 32 mm (Datenblatt 461.000.25.1), WC 424 mm breit (Datenblatt 461.313.00.6)
        // Montagewinkel am Boden: erster 10 cm von der Wand, höchstens 100 cm auseinander
        Assert.Equal([100, 1000, 1900], auf.Teile.Where(t => t.Titel == "GIS Montagewinkel (Boden)").Select(t => (t.S0 + t.S1) / 2).Order().ToList());
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
        Assert.Equal(3 * 7, auf.Teile.Count(t => t.Titel == "GIS Profil horizontal"));   // je Segment eigene: 5, 65, 130, 135, 200, 265 cm und an der Decke
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
}
