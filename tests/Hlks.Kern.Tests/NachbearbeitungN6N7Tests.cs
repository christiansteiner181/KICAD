using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N6 (Tür- und Fensterhöhe aus dem Körper, wenn OverallHeight deutlich abweicht) und N7 (Unterzug mit
/// lotrechtem Profil weit vom Ursprung liegt dort, wo das IFC ihn zeichnet) an der synthetischen Datei
/// NachbearbeitungN6N7Testdatei, je IFC4 und IFC2x3.</summary>
public class NachbearbeitungN6N7Tests
{
    static readonly Dictionary<string, (Projekt P, BefehlsErgebnis E)> laeufe = [];

    static (Projekt P, BefehlsErgebnis E) Uebernommen(string schema)
    {
        lock (laeufe)
        {
            if (laeufe.TryGetValue(schema, out var da)) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, NachbearbeitungN6N7Testdatei.Datei(schema)));
            return laeufe[schema] = (p, e);
        }
    }

    static Element Namens(Projekt p, string klasse, string name) => IfcArchitekturTests.Namens(p, klasse, name);

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N6_Tuer_HoeheAusDemKoerper_HerkunftNenntBeideWerte(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var t = Namens(p, "Tuer", "T-Koerper");
        Assert.Equal(2150, t.Zahl("hoehe"));
        Assert.Equal(940, t.Zahl("breite"));
        var q = t.Eigenschaft("hoehe")!.Herkunft.Quelle!;
        Assert.Equal("ifc", t.Eigenschaft("hoehe")!.Herkunft.Art);
        Assert.Contains("Körper IfcDoor #", q);
        Assert.Contains("2150 mm", q);
        Assert.Contains("OverallHeight 5190 mm", q);
        Assert.Contains("3040 mm", q);
        // Lage: Mitte der Öffnung auf der Wandachse (W-Sued, Achse in Wandmitte y = 100), Tür in der Wand
        Assert.Equal(1470, t.Zahl("abstand"));
        Assert.Equal(Namens(p, "Wand", "W-Sued").Id, t.Bezuege["wand"]);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N6_TuerOhneKoerper_UndRahmenUeberDerOeffnung_BehaltenOverallHeight(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var ohne = Namens(p, "Tuer", "T-OhneKoerper");
        Assert.Equal(5190, ohne.Zahl("hoehe"));
        Assert.EndsWith("IfcDoor.OverallHeight", ohne.Eigenschaft("hoehe")!.Herkunft.Quelle);
        // Körper 150 mm (7 %) höher als OverallHeight: unter der Schwelle (400 mm und 15 %) – OverallHeight gilt
        var rahmen = Namens(p, "Tuer", "T-Rahmen");
        Assert.Equal(2100, rahmen.Zahl("hoehe"));
        Assert.EndsWith("IfcDoor.OverallHeight", rahmen.Eigenschaft("hoehe")!.Herkunft.Quelle);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N6_Fenster_GleicherFall_HoeheUndBruestungAusDemKoerper(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var f = Namens(p, "Fenster", "F-Koerper");
        Assert.Equal(1200, f.Zahl("hoehe"));
        Assert.Equal(900, f.Zahl("bruestung"));
        Assert.Contains("OverallHeight 2700 mm", f.Eigenschaft("hoehe")!.Herkunft.Quelle);
        Assert.Contains("Körper IfcWindow #", f.Eigenschaft("bruestung")!.Herkunft.Quelle);
        Assert.Equal(1200, f.Zahl("breite"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N6_Hinweis_NenntJedeTuerUndJedesFenster(string schema)
    {
        var (_, e) = Uebernommen(schema);
        var tuer = Assert.Single(e.Hinweise, h => h.StartsWith("Architektur: Tür: Höhe aus dem Körper statt OverallHeight"));
        Assert.Contains("über 400 mm und über 15 %", tuer);
        Assert.Contains("– 1: IfcDoor #", tuer);
        Assert.Contains("«T-Koerper» 2150 statt 5190 mm", tuer);
        Assert.DoesNotContain("T-OhneKoerper", tuer);
        Assert.DoesNotContain("T-Rahmen", tuer);
        var fenster = Assert.Single(e.Hinweise, h => h.StartsWith("Architektur: Fenster: Höhe aus dem Körper statt OverallHeight"));
        Assert.Contains("«F-Koerper» 1200 statt 2700 mm", fenster);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N6_Wanddarstellung_SchneidetNurDieKoerperhoeheAus(string schema)
    {
        // Der KICAD-Körper der Wand spart über der Tür ab 2150 nichts mehr aus; die Tür ragt nicht über die Wand (2600)
        var (p, _) = Uebernommen(schema);
        var t = Namens(p, "Tuer", "T-Koerper");
        var w = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, p.Modell.Finde(t.Bezuege["wand"])!)!;
        var o = Wandgeometrie.Oeffnung(p.Katalog, t, w)!;
        Assert.Equal(2150, o.Oben);
        Assert.Null(Wandgeometrie.PasstNicht(w, o, []));
        var k = new ArchitekturGrafik().Koerper(p, t)!;
        var teil = Assert.Single(k.Teile);
        Assert.Equal(2150, teil.Masse![2], 3);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void N7_Unterzug_LotrechtesProfilWeitVomUrsprung_AchseUndLaengeWieImIfc(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var uz = Namens(p, "Traeger", "UZ-Stahl");
        var (an, en) = NachbearbeitungN6N7Testdatei.UzErwartet();
        var g = uz.Geometrie!;
        Assert.Equal("linie", g.Art);
        Assert.Equal(2, g.Punkte.Count);
        for (int k = 0; k < 2; k++)
        {
            Assert.True(Math.Abs(an[k] - g.Punkte[0][k]) <= 0.5, $"Anfang [{g.Punkte[0][0]}, {g.Punkte[0][1]}], erwartet [{an[0]:0.0}, {an[1]:0.0}]");
            Assert.True(Math.Abs(en[k] - g.Punkte[1][k]) <= 0.5, $"Ende [{g.Punkte[1][0]}, {g.Punkte[1][1]}], erwartet [{en[0]:0.0}, {en[1]:0.0}]");
        }
        double laenge = Math.Sqrt(Math.Pow(g.Punkte[1][0] - g.Punkte[0][0], 2) + Math.Pow(g.Punkte[1][1] - g.Punkte[0][1], 2));
        Assert.Equal(NachbearbeitungN6N7Testdatei.UzLaenge, laenge, 0);
        // Querschnitt 220 × 220, Unterkante = Lage − 220 (Profil hängt unter der Lage)
        Assert.Equal(220, g.Dicke);
        Assert.Equal(220, g.Hoehe);
        Assert.Equal(Math.Round(NachbearbeitungN6N7Testdatei.UzZ - 220, 1), g.Punkte[0][2], 0);
        Assert.Equal("stahl", uz.Text("material"));
    }

    [Fact]
    public void N7_Schwerpunkt_RingOhneFlaecheWeitVomUrsprung_IstMittelwert()
    {
        // Lotrechtes Profil im Grundriss: alle Punkte auf einer Strecke quer zur Achse, 50 km vom Ursprung (Rundungsrest ≠ 0)
        double ux = -0.6779209389291859, uy = -0.735134817949314, nx = -uy, ny = ux;
        double ox = 14660.97, oy = -54130.13;
        double[] s = [0, -110.0002712680377, -110.000135633996, -4.999999999999695, -5.000000000001399, 110.000135633996, 5.00000000000031, 110.0002712680377];
        var ring = s.Select(t => new[] { ox + t * nx, oy + t * ny, 5000.0 }).ToList();
        var c = Polygone.Schwerpunkt(ring);
        Assert.Equal(ring.Average(q => q[0]), c[0], 6);
        Assert.Equal(ring.Average(q => q[1]), c[1], 6);
        // Längs der Achse liegt der Punkt auf der Strecke (vorher bis 15.9 m daneben)
        Assert.True(Math.Abs((c[0] - ox) * ux + (c[1] - oy) * uy) < 1e-6);
    }

    [Fact]
    public void N7_Schwerpunkt_EchteFlaeche_UnveraendertAuchWeitVomUrsprung()
    {
        // Rechteck 4 × 2 m mit Mitte (2000, 1000) bzw. verschoben um 2 600 000 / 1 200 000 m (Landeskoordinaten in mm)
        List<double[]> R(double dx, double dy) => [[dx, dy], [dx + 4000, dy], [dx + 4000, dy + 2000], [dx, dy + 2000]];
        var a = Polygone.Schwerpunkt(R(0, 0));
        Assert.Equal(2000, a[0], 9); Assert.Equal(1000, a[1], 9);
        var b = Polygone.Schwerpunkt(R(2.6e9, 1.2e9));
        Assert.Equal(2.6e9 + 2000, b[0], 3); Assert.Equal(1.2e9 + 1000, b[1], 3);
        // L-Form: Schwerpunkt nicht der Mittelwert der Punkte
        var l = Polygone.Schwerpunkt([[0, 0], [3000, 0], [3000, 1000], [1000, 1000], [1000, 3000], [0, 3000]]);
        Assert.Equal(1100, l[0], 6); Assert.Equal(1100, l[1], 6);
    }
}
