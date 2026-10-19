using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Befund C2 (Hauptlauf, 18.10.2026): Eine Herstellerunterlage mit bezuege.familie ist ein Verweis auf ihre Familie,
/// kein platziertes Familienelement – die Familien-Nachführung lässt sie in Ruhe.</summary>
public class FamilieUnterlageBezugTests
{
    [Fact]
    public void Unterlage_mit_Bezug_auf_Familie_wird_angenommen_und_nicht_nachgefuehrt()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        var fam = p.Modell.DerKlasse("Familie").Single();

        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject
        {
            ["datei"] = TestPdf(o), ["zweck"] = "familie", ["bezuege"] = new JsonObject { ["familie"] = fam.Kennzeichen },
        }));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(fam.Id, ul.Bezuege["familie"]);
        Assert.Null(ul.Eigenschaft("familientyp"));
        Assert.Empty(ul.Anschluesse);

        // Eine neue Fassung der Familie führt platzierte Elemente nach – die Unterlage bleibt, wie sie ist
        var version = ul.Version;
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = fam.Kennzeichen,
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "Beschreibung geändert (Test)" }),
        }));
        var nachher = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(version, nachher.Version);
        Assert.Empty(nachher.Anschluesse);
    }
}
