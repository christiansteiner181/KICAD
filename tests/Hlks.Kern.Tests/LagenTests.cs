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
}
