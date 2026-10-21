using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.RaumgrenzenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N15 «Raum begehen»: Abfrage raum_begehen (Standpunkt im Rechteck- und im L-Raum, Augenhöhe, Boden, Blick mit Planwinkel ≠ 0,
/// Fehlerfälle, ändert nichts) und die Geometrie dahinter. Testgebäude frei gewählt (keine Normwerte): Rechteck 4 × 3 m, Wände 200 mm.</summary>
public class RaumBegehenTests
{
    static JsonObject Standort(Projekt p, string raum, double? augenhoehe = null)
    {
        var par = new JsonObject { ["raum"] = raum };
        if (augenhoehe is double a) par["augenhoehe"] = a;
        return (JsonObject)Tuer.Abfrage(p, "raum_begehen", par);
    }

    static double Z(JsonNode? n) => Json.Zahl(n!)!.Value;

    static (Projekt P, string Raum) Einfach()
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Waende(p, "EG", 0, 0, 4000, 3000, 200);
        var raum = Raum(p, "EG", 2000, 1500, 2500);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = raum, ["name"] = "Wohnen" }));
        return (p, raum);
    }

    static string LRaum(Projekt p, string kz, string geschoss, double[][] punkte)
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = kz, ["geschoss"] = geschoss,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts(punkte) },
        }));
        return kz;
    }

    // ------------------------------------------------------------------ Standpunkt

    [Fact]
    public void Rechteck_StandpunktInDerMitte_AugenhoeheUeberBoden_BlickNachNord()
    {
        var (p, raum) = Einfach();
        var o = Standort(p, raum);
        var s = (JsonObject)o["standpunkt"]!;
        Assert.Equal(2000, Z(s["x"]), 1);
        Assert.Equal(1500, Z(s["y"]), 1);
        Assert.Equal(1700, Z(s["z"]), 1);                // OK Boden 0 + 1700
        Assert.Equal("schwerpunkt", s["art"]!.GetValue<string>());
        Assert.Equal(1400, Z(s["abstandZumRand"]), 1);   // Innenmass 2800 hoch → 1400 bis zur Wand
        Assert.Equal(0, Z(o["bodenOk"]), 3);
        Assert.Equal(1700, Z(o["augenhoehe"]), 3);
        Assert.Equal("EG", o["geschoss"]!.GetValue<string>());
        Assert.Equal(raum, o["raum"]!.GetValue<string>());
        Assert.Equal("Wohnen", o["name"]!.GetValue<string>());
        var blick = (JsonObject)o["blick"]!;
        Assert.Equal(90, Z(blick["azimutGrad"]), 4);     // ohne Drehung: Projekt-Nord (+y)
        Assert.Equal(0, Z(blick["nickenGrad"]), 6);      // waagrecht
        var r = ((JsonArray)blick["richtung"]!).Select(Z).ToArray();
        Assert.Equal(0, r[0], 5); Assert.Equal(1, r[1], 5); Assert.Equal(0, r[2], 5);
    }

    [Fact]
    public void Augenhoehe_ParameterUebersteuertDieVorgabe_UngueltigeWerteWerdenAbgelehnt()
    {
        var (p, raum) = Einfach();
        Assert.Equal(1200, Z(Standort(p, raum, 1200)["standpunkt"]!["z"]), 1);   // z.B. sitzend
        Assert.Equal(1200, Z(Standort(p, raum, 1200)["augenhoehe"]), 1);
        foreach (var falsch in new[] { 100.0, 0, -1700, 5001 })
        {
            var ex = Assert.Throws<HlksFehler>(() => Standort(p, raum, falsch));
            Assert.Contains("Augenhöhe", ex.Message);
        }
        Assert.Equal(1700, RaumBegehen.AugenhoeheVorgabe);
    }

    [Fact]
    public void ObererStock_AugenhoeheZaehltAbOkBodenDesRaums()
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Geschoss(p, "OG", 3000, 3000);
        Waende(p, "OG", 0, 0, 4000, 3000, 200);
        var raum = Raum(p, "OG", 2000, 1500, 2500);
        var o = Standort(p, raum);
        Assert.Equal(3000, Z(o["bodenOk"]), 1);
        Assert.Equal(4700, Z(o["standpunkt"]!["z"]), 1);
        Assert.Equal("OG", o["geschoss"]!.GetValue<string>());
    }

    [Fact]
    public void Boden_IstDerTiefstePunktDesPolygons_WieBeiRaumImSDZeigen()
    {
        var p = Leer();
        Geschoss(p, "EG", 1000, 3000);
        var raum = LRaum(p, "R-Z", "EG", [[0, 0, 1000], [4000, 0, 950], [4000, 3000, 1000], [0, 3000, 1000]]);
        var o = Standort(p, raum);
        Assert.Equal(950, Z(o["bodenOk"]), 1);                       // tiefster Punkt des Polygons (Schnittbereich.cs, N14)
        Assert.Equal(950 + 1700, Z(o["standpunkt"]!["z"]), 1);
    }

    [Fact]
    public void LRaum_SchwerpunktLiegtAusserhalb_DarumDerInnerstePunkt()
    {
        // L mit Schenkeln von 1 m Breite: 6 × 1 m waagrecht, 1 × 6 m senkrecht. Der Schwerpunkt (1864, 1864) liegt in der Ecke ausserhalb.
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        var raum = LRaum(p, "R-L", "EG", [[0, 0], [6000, 0], [6000, 1000], [1000, 1000], [1000, 6000], [0, 6000]]);
        var ring = new List<double[]> { new double[] { 0, 0 }, new double[] { 6000, 0 }, new double[] { 6000, 1000 }, new double[] { 1000, 1000 }, new double[] { 1000, 6000 }, new double[] { 0, 6000 } };
        var sw = RaumBegehen.Schwerpunkt(ring)!.Value;
        Assert.Equal(1863.6, sw.X, 1);
        Assert.True(RaumBegehen.Abstand(ring, sw.X, sw.Y) < 0, "der Schwerpunkt des L liegt ausserhalb");

        var s = (JsonObject)Standort(p, raum)["standpunkt"]!;
        Assert.Equal("innerster Punkt", s["art"]!.GetValue<string>());
        double x = Z(s["x"]), y = Z(s["y"]);
        Assert.True(RaumBegehen.Abstand(ring, x, y) > 0, $"Standpunkt ({x}, {y}) liegt im Raum");
        // grösster Abstand zum Rand: im Eck, bis Wand und einspringende Ecke (1000, 1000) gleich weit weg: t = √2·(1000 − t) → t ≈ 585.8
        Assert.InRange(Z(s["abstandZumRand"]), 585.8 - 10, 585.8 + 0.5);
        Assert.InRange(x, 585.8 - 30, 585.8 + 30);
        Assert.InRange(y, 585.8 - 30, 585.8 + 30);
        Assert.Equal(1700, Z(s["z"]), 1);
    }

    [Fact]
    public void InnerstePunkt_GenauAufZehnMillimeter_BeiRechteckUndBeiEinerSchmalenRinne()
    {
        var rechteck = new List<double[]> { new double[] { 0, 0 }, new double[] { 4000, 0 }, new double[] { 4000, 2000 }, new double[] { 0, 2000 } };
        var i = RaumBegehen.InnerstePunkt(rechteck);
        Assert.Equal(1000, i.Abstand, 0);                        // in der Mitte der kurzen Seite: Abstand 1000 (ein Streifen von 1000 … 3000 ist gleich gut)
        Assert.InRange(i.X, 1000 - 10, 3000 + 10);
        // U-Form: Schwerpunkt im Ausschnitt; der innerste Punkt liegt im Steg und nicht im Ausschnitt
        var u = new List<double[]> { new double[] { 0, 0 }, new double[] { 5000, 0 }, new double[] { 5000, 4000 }, new double[] { 3500, 4000 }, new double[] { 3500, 1500 }, new double[] { 1500, 1500 }, new double[] { 1500, 4000 }, new double[] { 0, 4000 } };
        var sw = RaumBegehen.Schwerpunkt(u)!.Value;
        var ip = RaumBegehen.InnerstePunkt(u);
        Assert.True(RaumBegehen.Abstand(u, ip.X, ip.Y) > 0);
        Assert.True(ip.Abstand >= RaumBegehen.Abstand(u, sw.X, sw.Y) - 10 || RaumBegehen.Abstand(u, sw.X, sw.Y) < 0);
        // Verfahren stimmt mit einem feinen Raster überein (10 mm Toleranz)
        double beste = 0;
        for (double x = 0; x <= 5000; x += 25) for (double y = 0; y <= 4000; y += 25) beste = Math.Max(beste, RaumBegehen.Abstand(u, x, y));
        Assert.InRange(ip.Abstand, beste - 10 - 25, beste + 1);
    }

    [Fact]
    public void Schwerpunkt_VerschwindendeFlaecheGibtNull()
    {
        Assert.Null(RaumBegehen.Schwerpunkt([[0, 0], [1000, 0], [2000, 0]]));
    }

    // ------------------------------------------------------------------ Blick mit Planwinkel

    [Fact]
    public void GedrehtesGebaeude_BlickFolgtDemPlanwinkel_StandpunktBleibtInDerMitte()
    {
        // Rechteck 10 × 6 m um 37° gedreht → Planwinkel −37; «oben» im ausgerichteten Grundriss zeigt im Modell in Richtung 90° + 37° = 127°
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        double[] D(double x, double y) => Ansichtsausrichtung.Drehen([x, y], 37);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(D(0, 0), D(10000, 0), D(10000, 6000), D(0, 6000)), ["dicke"] = 200, ["geschlossen"] = true,
        }));
        var c = D(5000, 3000);
        var raum = Raum(p, "EG", c[0], c[1], 2600);
        Assert.Equal(-37, p.Modell.Finde("GEB")!.Zahl("planwinkel")!.Value, 6);
        var o = Standort(p, raum);
        var blick = (JsonObject)o["blick"]!;
        Assert.Equal(127, Z(blick["azimutGrad"]), 3);
        Assert.Equal(-37, Z(blick["planwinkel"]), 3);
        var r = ((JsonArray)blick["richtung"]!).Select(Z).ToArray();
        Assert.Equal(Math.Cos(127 * Math.PI / 180), r[0], 5);
        Assert.Equal(Math.Sin(127 * Math.PI / 180), r[1], 5);
        Assert.Equal(0, r[2], 6);
        // senkrecht auf der langen Wand: die lange Achse des Gebäudes liegt bei 37°, der Blick steht 90° dazu
        double lang = 37 * Math.PI / 180;
        Assert.Equal(0, r[0] * Math.Cos(lang) + r[1] * Math.Sin(lang), 5);
        var s = (JsonObject)o["standpunkt"]!;
        Assert.Equal(c[0], Z(s["x"]), 0);
        Assert.Equal(c[1], Z(s["y"]), 0);
    }

    [Fact]
    public void GespeicherterPlanwinkelGilt_AuchWennDieWaendeAnders_Liegen()
    {
        var (p, raum) = Einfach();
        Eigenschaften(p, "GEB", new JsonObject { ["planwinkel"] = 15 });
        var blick = (JsonObject)Standort(p, raum)["blick"]!;
        Assert.Equal(75, Z(blick["azimutGrad"]), 3);      // 90° − 15°
        Assert.Equal(15, Z(blick["planwinkel"]), 3);
    }

    // ------------------------------------------------------------------ Fehlerfälle, Lesen, Katalog

    [Fact]
    public void Fehler_KeinRaum_KeinUmriss_UnbekannterRaum()
    {
        var (p, _) = Einfach();
        var wand = p.Modell.DerKlasse("Wand").First().Kennzeichen;
        var ex = Assert.Throws<HlksFehler>(() => Standort(p, wand));
        Assert.Contains("Raum", ex.Message);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Raum", ["kennzeichen"] = "R-LEER", ["geschoss"] = "EG" }));
        Assert.Contains("Umriss", Assert.Throws<HlksFehler>(() => Standort(p, "R-LEER")).Message);
        Assert.Throws<HlksFehler>(() => Standort(p, "R-GIBTS-NICHT"));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "raum_begehen", new JsonObject()));
    }

    [Fact]
    public void Abfrage_AendertNichts_UndStehtImKatalogDerAbfragen()
    {
        var (p, raum) = Einfach();
        var vorher = p.Modell.Elemente.Count();
        var tx = p.Modell.Finde(raum)!.Geometrie!.Punkte.Select(q => string.Join(",", q)).ToList();
        Standort(p, raum); Standort(p, raum, 1300);
        Assert.Equal(vorher, p.Modell.Elemente.Count());
        Assert.Equal(tx, p.Modell.Finde(raum)!.Geometrie!.Punkte.Select(q => string.Join(",", q)).ToList());
        Assert.Null(p.Modell.Finde(raum)!.Eigenschaft("augenhoehe"));
        var d = Assert.Single(Abfragen.Liste, a => a.Name == "raum_begehen");
        Assert.Contains(d.Parameter, x => x.Name == "raum" && x.Pflicht);
        Assert.Contains(d.Parameter, x => x.Name == "augenhoehe" && x.Typ == "zahl" && !x.Pflicht);
        Assert.DoesNotContain(p.Befehle.Alle, b => b.Name == "raum_begehen");   // eine Abfrage, kein Befehl
    }
}
