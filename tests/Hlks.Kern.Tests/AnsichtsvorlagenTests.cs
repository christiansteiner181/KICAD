using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Gesamtprüfung Frage 49 (Christian 13.11.2026: «Verknüpfen wie Revit; einzelne Einstellung lösbar») und N33 («Wahl beim Duplizieren: nur Ansicht
/// oder mit Beschriftungen»). Fiktive Ansichten.</summary>
public class AnsichtsvorlagenTests
{
    static double? Zahl(Projekt p, string kz, string n) => p.Modell.Finde(kz)!.Zahl(n);
    static string? Text(Projekt p, string kz, string n) => p.Modell.Finde(kz)!.Text(n);
    static BefehlsErgebnis Aendern(Projekt p, string kz, JsonObject eig) => Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }));

    static Projekt MitVorlage()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "VL-1", ["name"] = "Koordination 1:100",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["vorlage"] = true, ["massstab"] = 100, ["detailgrad"] = "grob" },
        }));
        return p;
    }

    [Fact]
    public void Verknuepft_Vorlage_wirkt_nach_Einstellung_einzeln_loesbar_Umbenennen_und_Loeschen()
    {
        var p = MitVorlage();
        Aendern(p, "AN-EG", new JsonObject { ["ansichtsvorlage"] = "VL-1" });
        Assert.Equal((100.0, "grob"), (Zahl(p, "AN-EG", "massstab")!.Value, Text(p, "AN-EG", "detailgrad")));
        // Änderung der Vorlage erreicht die Ansicht
        var r = Aendern(p, "VL-1", new JsonObject { ["massstab"] = 200 });
        Assert.Equal(200, Zahl(p, "AN-EG", "massstab"));
        Assert.Contains(r.Hinweise, h => h.Contains("verknüpfte Ansicht"));
        // an der Ansicht geändert → nur diese Einstellung gelöst; die übrigen folgen weiter der Vorlage
        var g = Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "fein" });
        Assert.Contains(g.Hinweise, h => h.Contains("detailgrad von der Ansichtsvorlage VL-1 gelöst"));
        Aendern(p, "VL-1", new JsonObject { ["detailgrad"] = "mittel", ["massstab"] = 50 });
        Assert.Equal(("fein", 50.0), (Text(p, "AN-EG", "detailgrad"), Zahl(p, "AN-EG", "massstab")!.Value));
        // wieder anbinden: gelöste Liste leeren → Wert der Vorlage gilt
        Aendern(p, "AN-EG", new JsonObject { ["vorlageGeloest"] = null });
        Assert.Equal("mittel", Text(p, "AN-EG", "detailgrad"));
        // Umbenennen der Vorlage reisst die Verknüpfung nicht ab
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "VL-1", ["kennzeichen"] = "VL-KOORD" }));
        Assert.Equal("VL-KOORD", Text(p, "AN-EG", "ansichtsvorlage"));
        // Vorlage gelöscht: Verknüpfung weg, Werte bleiben
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "VL-KOORD" }));
        Assert.Null(Text(p, "AN-EG", "ansichtsvorlage"));
        Assert.Equal(50, Zahl(p, "AN-EG", "massstab"));
    }

    [Fact]
    public void Duplizieren_nur_Ansicht_oder_mit_Beschriftungen()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Textnotiz", ["kennzeichen"] = "TX-1", ["eigenschaften"] = new JsonObject { ["inhalt"] = "Notiz" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 0]) }, ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG" },
        }));
        int Notizen(string ansichtId) => p.Modell.Elemente.Count(e => e.Klasse == "Textnotiz" && e.Bezuege.GetValueOrDefault("ansicht") == ansichtId);
        Ok(p.Ausfuehren("ansicht_duplizieren", new JsonObject { ["ansicht"] = "AN-EG", ["kennzeichen"] = "AN-K1" }));
        Assert.Equal(0, Notizen(p.Modell.Finde("AN-K1")!.Id));
        var r = Ok(p.Ausfuehren("ansicht_duplizieren", new JsonObject { ["ansicht"] = "AN-EG", ["kennzeichen"] = "AN-K2", ["mitBeschriftungen"] = true }));
        var k2 = p.Modell.Finde("AN-K2")!;
        Assert.Equal(1, Notizen(k2.Id));
        Assert.Equal(1, Notizen(p.Modell.Finde("AN-EG")!.Id));   // Original unverändert
        Assert.Equal(p.Modell.Finde("AN-EG")!.Text("art"), k2.Text("art"));
        Assert.Contains(r.Hinweise, h => h.Contains("mit 1 Beschriftung"));
    }
}
