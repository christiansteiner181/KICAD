using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Alle Geometrien in Familien (30.10.2026, Christian: «es müssen alle geometrien abgebildet werden können»; «es muss auch möglich
/// sein fehlende familien aus einem ifc geometrisch abzubilden»): Körper uebergang und netz, Netz aus IFC bzw. STL übernehmen,
/// Masslinien in den Ansichten.</summary>
public class FamilieGeometrieTests
{
    static FamAufloesung Aufloesen(string koerper, string mehr = "")
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.JsonAus($$"""
            { "format": 1, "name": "Geometrie (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [{ "name": "A", "werte": {} }],
              "koerper": [{{koerper}}], "anschluesse": [] {{mehr}} }
            """));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler.Select(x => x.Meldung)));
        var a = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.True(a.Gueltig, string.Join("; ", a.Fehler.Select(x => x.Meldung)));
        return a;
    }

    static (double[] Min, double[] Max) Huelle(IEnumerable<double[]> ecken) =>
        ([.. Enumerable.Range(0, 3).Select(i => ecken.Min(e => e[i]))], [.. Enumerable.Range(0, 3).Select(i => ecken.Max(e => e[i]))]);

    static void Nahe(double[] soll, double[] ist, double tol = 0.5)
    {
        for (int i = 0; i < soll.Length; i++) Assert.True(Math.Abs(soll[i] - ist[i]) <= tol, $"[{i}] soll {soll[i]}, ist {ist[i]}");
    }

    [Fact]
    public void Uebergang_von_rund_auf_rechteckig()
    {
        var a = Aufloesen("""{ "id": "u", "art": "uebergang", "von": [0, 0, 0], "nach": [0, 0, 100], "profil": { "kreis": 50 }, "profil_nach": { "rechteck": [200, 60] } }""");
        var t = Assert.Single(a.Koerper.Where(k => k.Teil is not null)).Teil!;
        var (min, max) = Huelle(t.Ecken!);
        Nahe([-100, -50, 0], min);   // senkrecht: Breite in x, Höhe in y – unten Kreis r 50, oben Rechteck 200 × 60
        Nahe([100, 50, 100], max);
    }

    [Fact]
    public void Netz_wird_gedreht_und_verschoben()
    {
        var a = Aufloesen("""
            { "id": "n", "art": "netz", "ecken": [[0,0,0],[100,0,0],[0,50,0],[0,0,20]], "dreiecke": [[0,2,1],[0,1,3],[0,3,2],[1,2,3]],
              "drehung": 90, "verschiebung": [1000, 0, 5], "herkunft": "fiktiv" }
            """);
        var t = Assert.Single(a.Koerper.Where(k => k.Teil is not null)).Teil!;
        var (min, max) = Huelle(t.Ecken!);
        Nahe([950, 0, 5], min);   // x 0..100 → y 0..100, y 0..50 → x −50..0, dann + 1000 / + 5
        Nahe([1000, 100, 25], max);
    }

    [Fact]
    public void Netz_mit_Index_ausserhalb_ist_ungueltig()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Geometrie (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [{ "name": "A", "werte": {} }],
              "koerper": [{ "id": "n", "art": "netz", "ecken": [[0,0,0],[1,0,0],[0,1,0]], "dreiecke": [[0,1,3]] }], "anschluesse": [] }
            """));
        Assert.Contains(f.Fehler, x => x.Meldung.Contains("Indizes in ecken"));
    }

    [Fact]
    public void Masslinie_in_der_Ansicht_vorne()
    {
        var p = ProjektB4();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Mass (fiktiv)", "kategorie": "Ventil", "parameter": [{ "name": "b", "art": "typ", "datentyp": "laenge" }],
              "typen": [{ "name": "A", "werte": { "b": 400 } }],
              "koerper": [{ "id": "k", "art": "quader", "von": [0, 0, 0], "bis": ["b", 100, 300] }], "anschluesse": [],
              "masse": [{ "name": "B", "von": [0, 0, 0], "bis": ["b", 0, 0], "richtung": "x" }] }
            """) }));
        var r = FamilienAnsicht.Erzeugen(p, "FAM-1", "A", "vorne", "mittel", null).ToJsonString();
        Assert.Contains("bemassung", r);
        Assert.Contains("B 400", r);
    }

    [Fact]
    public void Fehlende_Familie_aus_IFC_Objekt()
    {
        var p = IfcHilfe.Leer();
        Ok(IfcHilfe.Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        // Objekt (IfcBuildingElementProxy, FacetedBrep 1000 × 1000 × 2000 bei 8000..9000 / 0..1000) liegt im Hintergrund – keine Familie
        var netz = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
        var pr = netz.Produkte.Single(x => x.Entitaet == "IfcBuildingElementProxy");
        var e = Ok(p.Ausfuehren("familie_netz_uebernehmen", new JsonObject
        {
            ["objekt"] = pr.GlobalId, ["unterlage"] = ul.Kennzeichen, ["name"] = "Objekt aus IFC (fiktiv)", ["kategorie"] = "Ventil",
        }));
        Assert.Contains(e.Hinweise, h => h.Contains("1000 × 1000 × 2000 mm") && h.Contains("Nullpunkt unten_mitte"));
        var fam = p.Modell.DerKlasse(Familien.Klasse).Single();
        var k = (JsonObject)((JsonObject)fam.Eigenschaft("definition")!.Inhalt!)["koerper"]![0]!;
        Assert.Equal("netz", k["art"]!.GetValue<string>());
        Assert.Contains(pr.GlobalId, k["herkunft"]!.GetValue<string>());
        var ecken = k["ecken"]!.AsArray().Select(x => x!.AsArray().Select(c => c!.GetValue<double>()).ToArray()).ToList();
        Assert.Equal(8, ecken.Count);   // doppelte Punkte der Flächen zusammengelegt
        var (min, max) = Huelle(ecken);
        Nahe([-500, -500, 0], min);
        Nahe([500, 500, 2000], max);
    }

    [Fact]
    public void Herstellernetz_aus_STL_Befund_in_bestehende_Familie()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var b = Ablegen(p, UnterlagenHilfe.StlBinaer(o));   // Quader 10 × 20 × 30, Ecke 0/0/0
        var f3 = b.Befunde.Single(x => x.Art == "form3d");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Quader (fiktiv)", "kategorie": "Ventil", "parameter": [], "typen": [{ "name": "A", "werte": {} }],
              "koerper": [{ "id": "netz", "art": "quader", "von": [0, 0, 0], "bis": [1, 1, 1] }], "anschluesse": [] }
            """) }));
        Ok(p.Ausfuehren("familie_netz_uebernehmen", new JsonObject { ["objekt"] = f3.Id, ["unterlage"] = "quader.stl", ["familie"] = "FAM-1" }));
        var def = (JsonObject)p.Modell.Finde("FAM-1")!.Eigenschaft("definition")!.Inhalt!;
        var k = (JsonObject)Assert.Single(def["koerper"]!.AsArray())!;   // Körper gleicher id ersetzt
        Assert.Equal("netz", k["art"]!.GetValue<string>());
        var a = FamilienAuswertung.Aufloesen(FamilieTestHilfe.Def(def), "A", null);
        var t = Assert.Single(a.Koerper.Where(x => x.Teil is not null)).Teil!;
        var (min, max) = Huelle(t.Ecken!);
        Nahe([0, 0, 0], min, 0.05);   // Herstellermodell: Ursprung bleibt
        Nahe([10, 20, 30], max, 0.05);
    }

    [Theory]
    [InlineData(47.3)]
    [InlineData(-20)]
    [InlineData(0)]
    public void Ausrichtung_aus_kleinstem_Rechteck(double grad)
    {
        double w = grad * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        var ecken = new List<double[]>();
        foreach (var (x, y) in new[] { (0.0, 0.0), (400, 0), (400, 600), (0, 600), (200, 300) })
            ecken.Add([x * c - y * s + 10000, x * s + y * c - 47000, 0]);
        Assert.Equal(((grad + 45) % 90 + 90) % 90 - 45, FamilieNetzUebernehmen.Ausrichtung(ecken), 1);   // Vielfache von 90° offen (Vorderseite unbekannt)
    }
}
