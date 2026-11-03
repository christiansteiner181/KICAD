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

    [Fact]
    public void FamilieAusIfc_EinlesenTeileWaehlen_KoerperUndAnschluesse()
    {
        // Flow (Christian 03.11.2026): IFC einlesen (bereiche keine) › Teile wählen (ifc_teile) › Familie (familie_netz_uebernehmen mit objekte, anschluesse)
        var p = Leer();
        Ok(Uebernehmen(p, Datei(), new JsonObject { ["bereiche"] = new JsonArray("keine") }));
        Assert.Empty(p.Modell.DerKlasse("WC"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var liste = (JsonObject)Tuer.Abfrage(p, "ifc_teile", new JsonObject { ["unterlage"] = ul.Kennzeichen });
        var wc = liste["teile"]!.AsArray().Single(t => t!["groesse"]!.AsArray().Select(x => x!.GetValue<double>()).SequenceEqual([400.0, 700, 400]))!;
        Ok(p.Ausfuehren("familie_netz_uebernehmen", new JsonObject
        {
            ["unterlage"] = ul.Kennzeichen, ["objekte"] = new JsonArray(wc["globalId"]!.GetValue<string>()), ["anschluesse"] = true,
            ["kategorie"] = "WC", ["name"] = "WC aus IFC",
        }));
        var def = (JsonObject)p.Modell.DerKlasse(Familien.Klasse).Single().Eigenschaft("definition")!.Inhalt!;
        var ecken = def["koerper"]![0]!["ecken"]!.AsArray().Select(q => q!.AsArray().Select(c => c!.GetValue<double>()).ToArray()).ToList();
        var box = Box(ecken);
        Assert.Equal(400, box[3] - box[0], 1); Assert.Equal(700, box[4] - box[1], 1); Assert.Equal(400, box[5] - box[2], 1);
        var an = def["anschluesse"]!.AsArray();
        Assert.NotEmpty(an);
        Assert.All(an, x => Assert.NotNull(x!["richtung"]));
    }

    [Fact]
    public void Architektur_TuerUndFenster_FamilieAusDemIfc_KoerperDecktSich_ZweiteUebernahmeOhneAenderung()
    {
        // Christian 03.11.2026: «auch bei der Architektur sollte das so sein» – Körper im System der Öffnung, geht mit der Wand mit
        var p = Leer();
        var datei = IfcArchitekturTestdatei.Datei("IFC4", false);
        Ok(Uebernehmen(p, datei));
        foreach (var kl in new[] { "Tuer", "Fenster" })
        {
            var e = p.Modell.DerKlasse(kl).Single();
            Assert.EndsWith("(IFC)", Familien.FamilieVon(p.Modell, e)!.Name);
        }
        var u = IfcDeckung.Pruefen(p, new JsonObject())["unterlagen"]!.AsArray().Single()!;
        foreach (var kl in new[] { "Tuer", "Fenster" })
            Assert.Equal(u["jeKlasse"]![kl]!["anzahl"]!.GetValue<int>(), u["jeKlasse"]![kl]!["deckungsgleich"]!.GetValue<int>());
        Assert.Null(Ok(Uebernehmen(p, datei)).PaketId);
    }

    static double[] Box(List<double[]> ecken) =>
        [ecken.Min(q => q[0]), ecken.Min(q => q[1]), ecken.Min(q => q[2]), ecken.Max(q => q[0]), ecken.Max(q => q[1]), ecken.Max(q => q[2])];
}
