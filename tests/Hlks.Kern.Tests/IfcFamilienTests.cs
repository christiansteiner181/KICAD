using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Familie aus dem IFC (Christian 03.11.2026): Bauteile mit eigener Form werden als Familie mit ihrem IFC-Netz übernommen,
/// Standardkomponenten (Vorgabe Leitung, Formteil, Verteiler, Armatur; wählbar mit Projektinformation.ifcStandard) nicht. Testdatei
/// IfcHlksTestdatei: «WC Vorwand» als Quader 400 × 700 × 400, «Rohrbogen 90».</summary>
public sealed class IfcFamilienTests : IDisposable
{
    readonly List<string> ordner = [];
    public void Dispose() { foreach (var o in ordner) try { Directory.Delete(o, true); } catch (IOException) { } }

    string Datei()
    {
        var o = IfcHlksTestdatei.Ordner();
        ordner.Add(o);
        return IfcHlksTestdatei.Schreiben("IFC4", Path.Combine(o, "hlks_test.ifc"), new IfcHlksSpec());
    }

    static JsonObject NurHlks() => new() { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false };

    [Fact]
    public void WcAlsFamilieAusDemIfc_KoerperWieImIfc_FormteilNicht_ZweiterImportOhneDoppel()
    {
        var p = Leer();
        var datei = Datei();
        Ok(Uebernehmen(p, datei, NurHlks()));
        var wc = p.Modell.Elemente.Single(e => e.Klasse == "WC");
        var fam = Familien.FamilieVon(p.Modell, wc)!;
        Assert.Equal("WC Vorwand (IFC)", fam.Name);
        Assert.StartsWith("ifc-", Familien.DefinitionsId(fam));
        // Körper der Familie, platziert = Quader des IFC (400 × 700 × 400)
        var k = Grafik.QuelleFuer(p.Katalog, wc)!.Koerper(p, wc)!;
        var box = IfcDeckung.TeilBox(k.Teile.Single()) ?? Box(k.Teile.Single().Ecken!);
        Assert.Equal(400, box[3] - box[0], 1); Assert.Equal(700, box[4] - box[1], 1); Assert.Equal(400, box[5] - box[2], 1);
        // Anschlüsse bleiben (aus den Ports der Datei, jetzt über die Familie)
        Assert.NotEmpty(wc.Anschluesse);
        Assert.Null(Familien.FamilieVon(p.Modell, p.Modell.Elemente.Single(e => e.Name == "Rohrbogen 90")));   // Formteil: Standard
        int familien = p.Modell.DerKlasse(Familien.Klasse).Count();
        Ok(Uebernehmen(p, datei, NurHlks()));
        Assert.Equal(familien, p.Modell.DerKlasse(Familien.Klasse).Count());
    }

    [Fact]
    public void Standard_WaehlbarJeProjekt_ApparatMitStandard_KeineFamilie()
    {
        var p = Leer();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["eigenschaften"] = new JsonObject { ["ifcStandard"] = new JsonArray("Leitung", "Formteil", "Apparat") } }));
        Ok(Uebernehmen(p, Datei(), NurHlks()));
        Assert.Null(Familien.FamilieVon(p.Modell, p.Modell.Elemente.Single(e => e.Klasse == "WC")));
    }

    static double[] Box(List<double[]> ecken) =>
        [ecken.Min(q => q[0]), ecken.Min(q => q[1]), ecken.Min(q => q[2]), ecken.Max(q => q[0]), ecken.Max(q => q[1]), ecken.Max(q => q[2])];
}
