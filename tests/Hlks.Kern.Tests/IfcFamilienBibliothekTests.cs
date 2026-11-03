using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Familien aus dem IFC und die Bürobibliothek (Christian 03.11.2026: «es soll möglich sein, zu wählen, welche importierten bauteile
/// dauerhaft in den standard aufgenommen werden sollen»): der Import schreibt sie nie von selbst in die Bibliothek (Befund 03.11.2026: ein
/// Prüfimport hatte 46 Entwürfe auf W: geschrieben); erst familie_in_bibliothek von Hand – danach lädt ein neuer Import sie von dort.
/// Bibliothek nur unter %TEMP% (BibliothekTestBasis).</summary>
[Collection("Familienbibliothek")]
public sealed class IfcFamilienBibliothekTests : BibliothekTestBasis
{
    static JsonObject NurHlks() => new() { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false };

    string Datei()
    {
        var o = Path.Combine(Wurzel, "ifc");
        Directory.CreateDirectory(o);
        return IfcHlksTestdatei.Schreiben("IFC4", Path.Combine(o, "hlks_test.ifc"), new IfcHlksSpec());
    }

    [Fact]
    public void ImportSchreibtNichtInDieBibliothek_ErstVonHand_DannLaedtDerNaechsteImportSie()
    {
        var datei = Datei();
        var a = NeuesProjekt("Projekt A");
        Ok(Uebernehmen(a, datei, NurHlks()));
        var fam = Familien.FamilieVon(a.Modell, a.Modell.Elemente.Single(e => e.Klasse == "WC"))!;
        Assert.DoesNotContain(Bibliothek(a)["familien"]!.AsArray(), f => f!["id"]!.GetValue<string>().StartsWith("ifc-"));
        Ok(a.Ausfuehren("familie_in_bibliothek", new JsonObject { ["familien"] = new JsonArray(fam.Kennzeichen) }));
        Assert.Contains(Bibliothek(a)["familien"]!.AsArray(), f => f!["id"]!.GetValue<string>() == Familien.DefinitionsId(fam));

        var b = NeuesProjekt("Projekt B");
        var e = Ok(Uebernehmen(b, datei, NurHlks()));
        Assert.Contains(e.Hinweise, h => h.Contains("1 aus der Bürobibliothek"));
        Assert.Equal(Familien.DefinitionsId(fam), Familien.DefinitionsId(Familien.FamilieVon(b.Modell, b.Modell.Elemente.Single(x => x.Klasse == "WC"))!));
    }
}
