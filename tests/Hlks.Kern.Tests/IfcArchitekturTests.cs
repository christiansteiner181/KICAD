using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Übertrag der Architektur (Modul 1b, B1) an der synthetischen Datei IfcArchitekturTestdatei, je IFC4 und IFC2x3:
/// Wände (Achse in der Mitte, Ecken auf den Achsen, Dicke, Höhe, Material, Aufbau), Tür und Fenster in ihrer Wand, Decke,
/// Dach, Stütze, Unterzug, Achsen, Hintergrund und Abgleich.</summary>
public class IfcArchitekturTests
{
    static readonly Dictionary<string, (Projekt P, BefehlsErgebnis E)> laeufe = [];

    /// <summary>Ein Übertrag je Fassung und Variante (für die lesenden Tests geteilt).</summary>
    internal static (Projekt P, BefehlsErgebnis E) Uebernommen(string schema, bool mitRaum = false)
    {
        lock (laeufe)
        {
            var k = schema + mitRaum;
            if (laeufe.TryGetValue(k, out var da)) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei(schema, mitRaum)));
            return laeufe[k] = (p, e);
        }
    }

    internal static Element Namens(Projekt p, string klasse, string name) =>
        p.Modell.DerKlasse(klasse).SingleOrDefault(e => e.Name == name) ?? throw new Xunit.Sdk.XunitException($"{klasse} «{name}» fehlt: {string.Join(", ", p.Modell.DerKlasse(klasse).Select(e => e.Name))}");

    static void Nahe(double[] erwartet, double[] ist, double tol = 0.5)
    {
        for (int i = 0; i < erwartet.Length; i++)
            Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"Stelle {i}: erwartet [{string.Join(", ", erwartet)}], ist [{string.Join(", ", ist)}]");
    }

    static double[] Ab(Element e, int i) => e.Geometrie!.Punkte[i];

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Waende_AchseInDerMitte_EckenAufDenAchsen_DickeHoehe(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var eg = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG");
        var sued = Namens(p, "Wand", "W-Sued");
        Assert.Equal("linie", sued.Geometrie!.Art);
        // Achse an der Aussenfläche y = 0, Körper y 0 … 200 → Wandmitte y = 100; Enden auf den Achsen von W-West und W-Ost
        Nahe([100, 100, 0], Ab(sued, 0));
        Nahe([5900, 100, 0], Ab(sued, 1));
        Assert.Equal(200, sued.Geometrie.Dicke);
        Assert.Equal(2600, sued.Geometrie.Hoehe);
        Assert.Equal(eg.Id, sued.Geschoss);
        Nahe([5900, 100], Ab(Namens(p, "Wand", "W-Ost"), 0)); Nahe([5900, 3900], Ab(Namens(p, "Wand", "W-Ost"), 1));
        Nahe([5900, 3900], Ab(Namens(p, "Wand", "W-Nord"), 0)); Nahe([100, 3900], Ab(Namens(p, "Wand", "W-Nord"), 1));
        Nahe([100, 3900], Ab(Namens(p, "Wand", "W-West"), 0)); Nahe([100, 100], Ab(Namens(p, "Wand", "W-West"), 1));
        // Der Kern verlängert an den Ecken um die halbe Dicke: Aussenmass wie im IFC 6000 × 4000
        var lage = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, sued)!;
        Assert.Equal(100, lage.VerlAnfang);
        Assert.Equal(100, lage.VerlEnde);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Waende_MaterialAufbauTragend_MitFundstelle(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var sued = Namens(p, "Wand", "W-Sued");
        Assert.Equal("beton", sued.Text("material"));
        Assert.Equal("Beton 150 / Wärmedämmung 50", sued.Text("aufbau"));
        Assert.Equal("true", sued.Text("tragend"));
        Assert.Equal("ifc", sued.Eigenschaft("material")!.Herkunft.Art);
        Assert.Contains("IfcMaterialLayerSetUsage", sued.Eigenschaft("material")!.Herkunft.Quelle);
        Assert.Contains("Pset_WallCommon.LoadBearing", sued.Eigenschaft("tragend")!.Herkunft.Quelle);
        Assert.Equal("kalksandstein", Namens(p, "Wand", "W-Ost").Text("material"));
        Assert.Equal("beton", Namens(p, "Wand", "W-Nord").Text("material"));
        Assert.Equal("Beton, Stahlbeton 200", Namens(p, "Wand", "W-Nord").Text("aufbau"));
        Assert.Equal("leichtbau", Namens(p, "Wand", "W-West").Text("material"));
        // Kennungen: GlobalId und Unterlage; Herkunft mit Entität und Nummer
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(22, sued.Kennungen!["ifc"].Length);
        Assert.Equal(ul.Id, sued.Kennungen["quelle"]);
        Assert.Equal("ifc", sued.Angelegt!.Art);
        Assert.Matches(@"IfcWall(StandardCase)? #\d+", sued.Angelegt.Quelle);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Tuer_InIhrerWand_AbstandBreiteHoeheSeiteBand(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var t = Namens(p, "Tuer", "T 0.01");
        var sued = Namens(p, "Wand", "W-Sued");
        Assert.Equal(sued.Id, t.Bezuege["wand"]);
        Assert.Equal(sued.Geschoss, t.Geschoss);
        Assert.Equal(2900, t.Zahl("abstand"));           // Mitte x = 3000, Achse beginnt bei x = 100
        Assert.Equal(900, t.Zahl("breite"));
        Assert.Equal(2100, t.Zahl("hoehe"));
        Assert.Contains("OverallWidth", t.Eigenschaft("breite")!.Herkunft.Quelle);
        Assert.Equal("links", t.Text("seite"));           // öffnet in +y = links der Wandrichtung
        Assert.Equal("anfang", t.Text("band"));           // SINGLE_SWING_LEFT: Band bei −x der Tür = zum Wandanfang
        Assert.Contains("SINGLE_SWING_LEFT", t.Eigenschaft("band")!.Herkunft.Quelle);
        Nahe([3000, 100, 0], Ab(t, 0));
        // Wandgeometrie findet die Öffnung: passt in die Wand
        var w = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, sued)!;
        var o = Wandgeometrie.Oeffnung(p.Katalog, t, w)!;
        Assert.Null(Wandgeometrie.PasstNicht(w, o, []));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Fenster_BruestungUeberGeschosskote(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var f = Namens(p, "Fenster", "F 0.01");
        var nord = Namens(p, "Wand", "W-Nord");
        Assert.Equal(nord.Id, f.Bezuege["wand"]);
        Assert.Equal(3900, f.Zahl("abstand"));           // Achse von x = 5900 nach x = 100, Mitte x = 2000
        Assert.Equal(1200, f.Zahl("breite"));
        Assert.Equal(1200, f.Zahl("hoehe"));
        Assert.Equal(900, f.Zahl("bruestung"));
        Assert.Contains("Geschosskote", f.Eigenschaft("bruestung")!.Herkunft.Quelle);
        Nahe([2000, 3900, 0], Ab(f, 0));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void DeckeUndDach_UmrissOberkanteDicke_LochAlsHinweis(string schema)
    {
        var (p, e) = Uebernommen(schema);
        var og = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "1.OG");
        var d = Namens(p, "Decke", "Decke 1.OG");
        Assert.Equal("polygon", d.Geometrie!.Art);
        Assert.Equal(4, d.Geometrie.Punkte.Count);
        Assert.All(d.Geometrie.Punkte, q => Assert.Equal(3000, q[2]));
        Assert.Equal(250, d.Geometrie.Dicke);
        Assert.Equal(24e6, d.Geometrie.Flaeche(), 0);
        Assert.Equal(og.Id, d.Geschoss);
        Assert.Contains(e.Hinweise, h => h.Contains("Decke mit Aussparungen"));
        Assert.Equal("Beton 250", d.Text("aufbau"));
        var dach = Namens(p, "Dach", "Dach");
        Assert.All(dach.Geometrie!.Punkte, q => Assert.Equal(6000, q[2]));
        Assert.Equal(250, dach.Geometrie.Dicke);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void StuetzeUndUnterzug_QuerschnittAchseMasse(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var s = Namens(p, "Stuetze", "Stuetze 1");
        Assert.Equal("polygon", s.Geometrie!.Art);
        Nahe([2850, 1850, 3150, 2150], ArchitekturHilfe.Box(s.Geometrie.Punkte));
        Assert.All(s.Geometrie.Punkte, q => Assert.Equal(0, q[2]));
        Assert.Equal(2600, s.Geometrie.Hoehe);
        Assert.Equal("beton", s.Text("material"));
        var u = Namens(p, "Traeger", "UZ 1");
        Assert.Equal("linie", u.Geometrie!.Art);
        Nahe([1000, 3000, 2400], Ab(u, 0));
        Nahe([5000, 3000, 2400], Ab(u, 1));
        Assert.Equal(200, u.Geometrie.Dicke);
        Assert.Equal(200, u.Geometrie.Hoehe);
        Assert.Equal("stahl", u.Text("material"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Achsen_AusDemRaster_MitBezeichnungAlsKennzeichen(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var achsen = p.Modell.DerKlasse("Achse").ToList();
        Assert.Equal(["1", "2", "A", "B"], achsen.Select(a => a.Text("bezeichnung")).Order());
        var a = achsen.Single(x => x.Text("bezeichnung") == "A");
        Assert.Equal("A", a.Kennzeichen);
        Nahe([0, -1000], Ab(a, 0)); Nahe([0, 5000], Ab(a, 1));
        Assert.EndsWith(":U1", a.Kennungen!["ifc"]);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Rundwand_Hintergrund_GezaehltUndImNetz(string schema)
    {
        var (p, e) = Uebernommen(schema);
        Assert.DoesNotContain(p.Modell.DerKlasse("Wand"), w => (w.Name ?? "").StartsWith("Rundwand"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var nv = ul.Eigenschaft("uebernommen")!.Inhalt!["nichtVerstanden"]!;
        Assert.Equal(schema == "IFC4" ? 2 : 1, nv["IfcWall"]!.GetValue<int>());
        Assert.Contains(e.Hinweise, h => h.Contains("Gekrümmte Wand"));
        var netz = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
        Assert.Contains(netz.Produkte, x => x.Entitaet == "IfcWall" && !x.Uebernommen);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Anzahlen_JeKlasse(string schema)
    {
        var (p, _) = Uebernommen(schema);
        Assert.Equal(schema == "IFC4" ? 6 : 5, p.Modell.DerKlasse("Wand").Count());
        Assert.Single(p.Modell.DerKlasse("Tuer"));
        Assert.Single(p.Modell.DerKlasse("Fenster"));
        Assert.Single(p.Modell.DerKlasse("Decke"));
        Assert.Single(p.Modell.DerKlasse("Dach"));
        Assert.Single(p.Modell.DerKlasse("Stuetze"));
        Assert.Single(p.Modell.DerKlasse("Traeger"));
        Assert.Equal(4, p.Modell.DerKlasse("Achse").Count());
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(schema == "IFC4" ? 6 : 5, ul.Eigenschaft("uebernommen")!.Inhalt!["Wand"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void WandAusTeilen_DickeAusMengen_AufbauAusTeilen_TeileNichtEinzeln(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var w = Namens(p, "Wand", "W-Teile");
        // Achse an der Aussenfläche, Teile y 0 … 150 (Beton) und 150 … 200 (Dämmung) → Mitte y = −3000 + 100
        Nahe([0, -2900, 3000], Ab(w, 0));
        Nahe([4000, -2900, 3000], Ab(w, 1));
        Assert.Equal(200, w.Geometrie!.Dicke);
        Assert.Equal(2600, w.Geometrie.Hoehe);
        Assert.Equal("Beton 150 / Dämmung 50", w.Text("aufbau"));
        Assert.Equal("beton", w.Text("material"));
        Assert.Contains("IfcBuildingElementPart", w.Eigenschaft("material")!.Herkunft.Quelle);
        // Netzdatei: die Wand trägt das Netz ihrer Teile (übernommen), die Teile stehen nicht einzeln darin
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var n = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
        var np = n.Produkt(w.Kennungen!["ifc"])!;
        Assert.True(np.Uebernommen);
        Assert.Equal(24, np.Dreiecke);
        Assert.DoesNotContain(n.Produkte, x => x.Entitaet == "IfcBuildingElementPart");
    }

    [Fact]
    public void Ifc4_WandMitIndexedPolyCurveUndPolygonalFaceSet()
    {
        var (p, _) = Uebernommen("IFC4");
        var w = Namens(p, "Wand", "W-OG");
        Nahe([0, 6000, 3000], Ab(w, 0));
        Nahe([3000, 6000, 3000], Ab(w, 1));
        Assert.Equal(150, w.Geometrie!.Dicke);
        Assert.Equal(2800, w.Geometrie.Hoehe);
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "1.OG").Id, w.Geschoss);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void ZweiteUebernahme_DerselbenDatei_KeineAenderung(string schema)
    {
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei(schema, false);
        Ok(Uebernehmen(p, datei));
        var zweites = Ok(Uebernehmen(p, datei));
        Assert.Null(zweites.PaketId);
        Assert.Contains(zweites.Hinweise, h => h.Contains("0 neu, 0 geändert, 0 gelöscht"));
    }

    [Fact]
    public void Trockenlauf_ZeigtElemente_OhneNetzdatei()
    {
        var p = Leer();
        var e = Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false), trocken: true));
        Assert.True(e.Trockenlauf);
        Assert.Contains(e.Aenderungen, a => a.Klasse == "Wand" && a.Art == "angelegt");
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void NurArchitektur_DanachRaumErkennenImRaum_Gelingt()
    {
        // Abnahmefall der Spezifikation: Architekten-IFC übernehmen, danach raum_erkennen in einem Raum des EG
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC2X3", false), new JsonObject { ["bereiche"] = new JsonArray("architektur") }));
        Assert.Empty(p.Modell.DerKlasse("Raum"));
        var r = Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = new JsonArray(1500, 1500), ["nummer"] = "0.01" }));
        var raum = p.Modell.DerKlasse("Raum").Single();
        Assert.Equal(5600.0 * 3600, raum.Geometrie!.Flaeche(), 0);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Grafik_StuetzeGeschnittenDick_UnterzugUeberDerSchnitthoeheGestrichelt_Koerper(string schema)
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei(schema, false)));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "GR-EG", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        var g = Grafik.Plan(p, "GR-EG");
        var s = Namens(p, "Stuetze", "Stuetze 1");
        var ts = g.Teile.Single(t => t.Id == s.Id);
        Assert.Equal(Stifte.Dick, ts.Primitive[0].Stil.Stift);
        Assert.Equal("beton", ts.Primitive[0].Stil.Muster);
        var u = Namens(p, "Traeger", "UZ 1");
        var tu = g.Teile.Single(t => t.Id == u.Id);
        Assert.Equal("strich", tu.Primitive[0].Stil.Linie);
        Assert.Equal(Stifte.Duenn, tu.Primitive[0].Stil.Stift);
        Nahe([1000, 2900, 5000, 3100], ArchitekturHilfe.Box(tu.Primitive[0].Punkte));
        // Körper: Stütze Prisma 0 … 2600, Unterzug Quader 4000 × 200 × 200 in der Mitte (3000, 3000, 2500)
        var ks = Grafik.Koerper(p, [s.Id]).Single().Teile.Single();
        Assert.Equal("prisma", ks.Art); Assert.Equal(0, ks.Unten); Assert.Equal(2600, ks.Oben);
        var ku = Grafik.Koerper(p, [u.Id]).Single().Teile.Single();
        Assert.Equal("quader", ku.Art);
        Nahe([3000, 3000, 2500], ku.Mitte!);
        Nahe([4000, 200, 200], ku.Masse!);
    }

    [Fact]
    public void Grafik_UnterzugGeschnitten_SchraffurDick_StuetzeUnterDerSchnitthoehe_Sichtkante()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        // Schnitthöhe 2500 im EG: Unterzug (2400 … 2600) geschnitten; zweite Ansicht 2700: Stütze (0 … 2600) darunter
        foreach (var (kz, h) in new[] { ("GR-2500", 2500), ("GR-2700", 2700) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = h, ["schnitthoeheNachSia"] = false },
            }));
        var u = Namens(p, "Traeger", "UZ 1");
        var tu = Grafik.Plan(p, "GR-2500").Teile.Single(t => t.Id == u.Id).Primitive[0];
        Assert.Equal(Stifte.Dick, tu.Stil.Stift);
        Assert.Equal("schnitt", tu.Stil.Fuellung);   // Stahl hat (noch) kein Muster: Füllung «schnitt»
        var s = Namens(p, "Stuetze", "Stuetze 1");
        var ts = Grafik.Plan(p, "GR-2700").Teile.Single(t => t.Id == s.Id).Primitive[0];
        Assert.Equal(Stifte.Duenn, ts.Stil.Stift);
        Assert.Equal("voll", ts.Stil.Linie);
        Assert.Null(ts.Stil.Fuellung);
    }
}
