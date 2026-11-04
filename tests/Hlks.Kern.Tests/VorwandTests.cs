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
        Assert.Equal([15, 700, 1250, 1935], vert);
        // Montagewinkel am Boden: erster 10 cm von der Wand, höchstens 100 cm auseinander
        Assert.Equal([100, 1000, 1900], auf.Teile.Where(t => t.Titel == "GIS Montagewinkel (Boden)").Select(t => (t.S0 + t.S1) / 2).Order().ToList());
        Assert.Equal(8, Anzahl(sl, "GIS Wandanbindung"));
        Assert.Contains(sl["zeilen"]!.AsArray().OfType<JsonObject>(), z => (string?)z["artikel"] == "461.313.00.6" && (int)z["anzahl"]! == 1);
        Assert.Contains(sl["annahmen"]!.AsArray(), a => ((string?)a)!.Contains("Querschnitt des GIS Profils"));

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
}
