using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Lagen (Christian 05.11.2026): Abstand UK roh Decke bis Rohrachse je Geschoss, UK roh Decke aus dem Modell oder von Hand,
/// im Grundriss so viele Schrägstriche wie die Lage, einmal bis zum nächsten Wechsel. Werte fiktiv.</summary>
public class LagenTests
{
    static List<Primitiv> Striche(Projekt p, string kz) =>
        Grafik.Plan(p, "AN-EG").Teile.Single(t => t.Id == Hole(p, kz).Id).Primitive.Where(x => x.Art == "linie" && x.Stil.Stift == Stifte.Mittel).ToList();

    [Fact]
    public void Lage_AusDerHoehe_StricheNurBisZumWechsel()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["ukRohdecke"] = 2800, ["lagen"] = "120, 240" } }));
        var l = ((JsonObject)Tuer.Abfrage(p, "lagen", new JsonObject { ["geschoss"] = "EG" }))["geschosse"]![0]!;
        Assert.Equal("eingabe", (string)l["quelle"]!);
        Assert.Equal(2560, (double)l["lagen"]![1]!["hoehe"]!);   // 2800 − 240
        // 2. Lage: zwei Rohre mit Bogen dazwischen → nur das erste trägt das Zeichen (zwei Striche)
        Ok(Zeichnen(p, "DN 20", [[0, 0, 2560], [3000, 0, 2560], [3000, 2000, 2560]]));
        var rohre = p.Modell.DerKlasse("Rohr").OrderBy(r => r.Kennzeichen).ToList();
        Assert.Equal(2, rohre.Count);
        Assert.All(rohre, r => Assert.Equal(2, Lagen.LageVon(p, r)));
        Assert.Equal(2, Striche(p, rohre[0].Kennzeichen).Count);
        Assert.All(Striche(p, rohre[0].Kennzeichen), s => Assert.Equal("#000000", s.Stil.Farbe));   // schwarz (Christian 11.11.2026)
        Assert.Empty(Striche(p, rohre[1].Kennzeichen));
        // keine Lage: kein Zeichen
        Ok(Zeichnen(p, "DN 20", [[0, 1000, 2000], [2000, 1000, 2000]]));
        Assert.Null(Lagen.LageVon(p, Hole(p, "RO-3")));
        Assert.Empty(Striche(p, "RO-3"));
    }

    [Fact]
    public void UkRohdecke_AusDerDeckeDarueber()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "decke", ["dicke"] = 250 }));   // OK 3000 (Kote + Geschosshöhe)
        var l = ((JsonObject)Tuer.Abfrage(p, "lagen", new JsonObject()))["geschosse"]![0]!;
        Assert.Equal("modell", (string)l["quelle"]!);
        Assert.Equal(2750, (double)l["ukRohdecke"]!);
        Assert.Equal(2630, (double)l["lagen"]![0]!["hoehe"]!);   // Vorgabe 1. Lage 120 mm
    }

    static void Decke(Projekt p, string kz, double x0, double y0, double x1, double y1, double ok, double dicke, string? aufbau = null)
    {
        var o = new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = kz, ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["dicke"] = dicke,
                ["punkte"] = new JsonArray(new JsonArray(x0, y0, ok), new JsonArray(x1, y0, ok), new JsonArray(x1, y1, ok), new JsonArray(x0, y1, ok)) },
        };
        if (aufbau is not null) o["eigenschaften"] = new JsonObject { ["aufbau"] = aufbau };
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    static JsonNode Geschoss(Projekt p, string kz) =>
        ((JsonArray)((JsonObject)Tuer.Abfrage(p, "lagen", new JsonObject()))["geschosse"]!).Single(g => (string)g!["geschoss"]! == kz)!;

    /// <summary>Christian 09.11.2026: «uk rohdecke soll aus dem IFC übernommen werden. dies ist im regelfall die betondecke.» Kleine Platten
    /// (Podest, Sturz) verdrängen die grosse Decke nicht mehr; ist eine grosse Decke aus Beton, gilt sie.</summary>
    [Fact]
    public void UkRohdecke_Betondecke_vorKleinenPlatten()
    {
        var p = Neu();
        Decke(p, "D-1", 0, 0, 6000, 4000, 2950, 200);                       // grosse Decke ohne Material
        Decke(p, "D-2", 0, 0, 1000, 1000, 1400, 210, "Beton, Stahlbeton 210");   // zwei kleine Betonplatten (früher: häufigste Unterkante 1190)
        Decke(p, "D-3", 2000, 0, 3000, 1000, 1400, 210, "Beton, Stahlbeton 210");
        var g = Geschoss(p, "EG");
        Assert.Equal(2750, (double)g["ukRohdecke"]!);
        Assert.Equal("D-1", (string)g["decke"]!);
        Decke(p, "D-4", 0, 0, 5000, 4000, 2900, 250, "Beton, Stahlbeton 250");   // grosse Betondecke (Bodenaufbau D-1 darüber ohne Material)
        g = Geschoss(p, "EG");
        Assert.Equal(2650, (double)g["ukRohdecke"]!);
        Assert.Equal("D-4", (string)g["decke"]!);
    }

    /// <summary>Christian 09.11.2026: «lagen gelten default für alle etagen» – Lagen am Gebäude gelten für jedes Geschoss ohne eigene Lagen.</summary>
    [Fact]
    public void Lagen_AmGebaeude_GeltenFuerAlleGeschosse()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000, ["ukRohdecke"] = 2700 } }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["ukRohdecke"] = 2700 } }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB", ["eigenschaften"] = new JsonObject { ["lagen"] = "100, 200" } }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "OG", ["eigenschaften"] = new JsonObject { ["lagen"] = "150" } }));
        var eg = Geschoss(p, "EG"); var og = Geschoss(p, "OG");
        Assert.Equal("gebaeude", (string)eg["lagenQuelle"]!);
        Assert.Equal([100.0, 200.0], ((JsonArray)eg["lagen"]!).Select(l => (double)l!["abstand"]!));
        Assert.Equal("geschoss", (string)og["lagenQuelle"]!);
        Assert.Equal([150.0], ((JsonArray)og["lagen"]!).Select(l => (double)l!["abstand"]!));
    }

    /// <summary>Christian 05.11.2026: «1. Lage UKD −12cm, 2. Lage UKD −24cm» – so geschrieben sind es 120 und 240 mm, nicht 12 und 24 mm,
    /// und das Minus lässt keine Lage wegfallen (Gesamtprüfung 13.11.2026).</summary>
    [Fact]
    public void Lagen_EinheitCmUndMinus_WieGeschrieben()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["ukRohdecke"] = 2700, ["lagen"] = "-12cm, −24 cm, 360, 480mm" } }));
        var eg = Geschoss(p, "EG");
        Assert.Equal([120.0, 240.0, 360.0, 480.0], ((JsonArray)eg["lagen"]!).Select(l => (double)l!["abstand"]!));
        Assert.Equal(2580, (double)eg["lagen"]![0]!["hoehe"]!);   // 2700 − 120
        Assert.Null(eg["hinweis"]);

        // Gegenprüfung B8: Bezeichnungen vor dem Wert zählen nicht als Lagen (vorher [1, 120, 2, 240] bzw. [1, 120, 2, 240] mit «1. Lage»)
        foreach (var text in new[] { "Lage 1: 120, Lage 2: 240", "1. Lage UKD −12cm; 2. Lage UKD −24cm", "0.12 m, 0.24m" })
        {
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["lagen"] = text } }));
            eg = Geschoss(p, "EG");
            Assert.Equal([120.0, 240.0], ((JsonArray)eg["lagen"]!).Select(l => (double)l!["abstand"]!));
            Assert.Null(eg["hinweis"]);
        }
        // Unlesbares wird gemeldet statt als Ziffern mitgezählt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["lagen"] = "120, Lage 2 240, zirka 3x, 360" } }));
        eg = Geschoss(p, "EG");
        Assert.Equal([120.0, 360.0], ((JsonArray)eg["lagen"]!).Select(l => (double)l!["abstand"]!));
        Assert.Contains("«Lage 2 240»", (string)eg["hinweis"]!);
        Assert.Contains("«zirka 3x»", (string)eg["hinweis"]!);
    }
}
