using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-O: Belastungsbeschriftung im Strangschema («ΣLU 12 / 8», «ΣLU 12», «ΣDU 4.6»). Die Summen kommen aus dem Netz (Teil A-K);
/// geprüft werden Textformat und Rundung, die Beschriftung im Beispielprojekt «MFH Musterweg» (Handrechnung wie SanitaerBeispielTests), das
/// Ein-/Ausschalten (Ansichtseigenschaft belastung, Vorgabe ein; Detailgrad grob ohne) und dass kein Belastungstext einen Grössentext überdeckt.</summary>
public class SchemaBelastungTests
{
    const string Schema = "AN-SCHEMA-SAN";
    static readonly Projekt B = Beispielprojekt.Erzeugen(Kat, null);

    static List<(string Teil, Primitiv T)> Texte(Plangrafik g, Func<string, bool> filter) =>
        g.Teile.SelectMany(t => t.Primitive.Where(x => x.Art == "text" && x.Text is { } s && filter(s)).Select(x => (t.Id ?? "", x))).ToList();

    static bool Belastung(string s) => s.StartsWith("ΣLU") || s.StartsWith("ΣDU");

    [Theory]
    [InlineData(12.0, 8.0, "ΣLU 12 / 8")]
    [InlineData(12.0, 0.0, "ΣLU 12")]
    [InlineData(0.0, 8.0, "ΣLU – / 8")]
    [InlineData(0.0, 0.0, null)]
    [InlineData(80.00000000000001, 52.0, "ΣLU 80 / 52")]
    [InlineData(2.5, 0.0, "ΣLU 2.5")]
    public void TextLu_KaltWarmwasser(double kw, double ww, string? erwartet) => Assert.Equal(erwartet, SchemaBelastung.TextLu(kw, ww));

    [Theory]
    [InlineData(4.6, "ΣDU 4.6")]
    [InlineData(38.800000000000004, "ΣDU 38.8")]   // Fliesskommafehler der Summe verschwindet
    [InlineData(2.0, "ΣDU 2")]
    [InlineData(0.8, "ΣDU 0.8")]
    [InlineData(16.7, "ΣDU 16.7")]
    [InlineData(0.0, null)]
    public void TextDu_EineNachkommastelle(double du, string? erwartet) => Assert.Equal(erwartet, SchemaBelastung.TextDu(du));

    [Fact]
    public void Beispiel_LeitungenTragenIhreSummeAlsText()
    {
        var g = Grafik.Plan(B, Schema);
        TeilTexte(g, "KW-V1-0").ForEach(x => { });
        Assert.Contains("ΣLU 24", TeilTexte(g, "KW-V1-0"));            // Hausanschluss: alle Kaltwasser-Apparate
        Assert.Contains("ΣLU 14", TeilTexte(g, "KW-S1-1"));            // Steigstrang Bad/Küche
        Assert.Contains("ΣDU 16.7", TeilTexte(g, "SW-G1-1"));          // Grundleitung zum Kontrollschacht
        Assert.Contains("ΣDU 14.4", TeilTexte(g, "SW-S1-2"));
        Assert.Contains("ΣDU 8.2", TeilTexte(g, "SW-F1-4"));
        // das Beispiel hat kein Warmwasser: nie «/» in der Belastung
        Assert.All(Texte(g, Belastung), x => Assert.DoesNotContain("/", x.T.Text));
        // jeder Text gehört zu einer Leitung mit Summe
        Assert.NotEmpty(Texte(g, s => s.StartsWith("ΣLU")));
        Assert.NotEmpty(Texte(g, s => s.StartsWith("ΣDU")));
    }

    static List<string> TeilTexte(Plangrafik g, string kz) =>
        g.Teile.Where(t => t.Id == Hole(B, kz).Id).SelectMany(t => t.Primitive).Where(x => x.Art == "text").Select(x => x.Text ?? "").ToList();

    [Fact]
    public void Beispiel_BelastungSchaltbar_VorgabeEin_GrobOhne()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Assert.NotEmpty(Texte(Grafik.Plan(p, Schema), Belastung));                    // Vorgabe ein, Eigenschaft nicht gesetzt
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Schema, ["eigenschaften"] = new JsonObject { ["belastung"] = false } }));
        Assert.Empty(Texte(Grafik.Plan(p, Schema), Belastung));                       // ausgeschaltet
        Assert.NotEmpty(Texte(Grafik.Plan(p, Schema), s => s.StartsWith("NW")));      // die Grössen bleiben
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Schema, ["eigenschaften"] = new JsonObject { ["belastung"] = true } }));
        Assert.NotEmpty(Texte(Grafik.Plan(p, Schema), Belastung));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Schema, ["eigenschaften"] = new JsonObject { ["belastung"] = null } }));
        Assert.NotEmpty(Texte(Grafik.Plan(p, Schema), Belastung));                    // zurück zur Vorgabe
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Schema, ["eigenschaften"] = new JsonObject { ["detailgrad"] = "grob" } }));
        Assert.Empty(Texte(Grafik.Plan(p, Schema), s => Belastung(s) || s.StartsWith("NW")));   // grob: weder Grössen noch Belastung
    }

    [Fact]
    public void Beispiel_AendertSichDieApparateart_AendertSichDieBeschriftung()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        // Waschtisch (1 LU KW) → Badewanne (3): die Summe am Hausanschluss wächst um 2
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WT-E.02", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "badewanne" } }));
        var g = Grafik.Plan(p, Schema);
        Assert.Contains("ΣLU 26", g.Teile.Where(t => t.Id == Hole(p, "KW-V1-0").Id).SelectMany(t => t.Primitive).Select(x => x.Text ?? ""));
    }

    static double[] Rahmen(Primitiv t)
    {
        double b = SchemaRaster.TextBreite(t.Text!, t.Groesse ?? 1.6), h = 0.75 * (t.Groesse ?? 1.6), x = t.Punkte[0][0], y = t.Punkte[0][1];
        return t.Winkel == 90 ? [x - h, y - b / 2, x, y + b / 2] : [x - b / 2, y, x + b / 2, y + h];
    }

    [Fact]
    public void Beispiel_KeinBelastungstextUeberdecktEinenAnderenGroessenOderBelastungstext()
    {
        var g = Grafik.Plan(B, Schema);
        var alle = Texte(g, s => Belastung(s) || s.StartsWith("NW") || s.StartsWith("PE")).Select(x => x.T).ToList();
        Assert.True(alle.Count > 20);
        var ueberdeckt = new List<string>();
        for (int i = 0; i < alle.Count; i++)
            for (int j = i + 1; j < alle.Count; j++)
            {
                var a = Rahmen(alle[i]); var c = Rahmen(alle[j]);
                if (a[0] < c[2] && c[0] < a[2] && a[1] < c[3] && c[1] < a[3])
                    ueberdeckt.Add($"«{alle[i].Text}» ({string.Join(",", a)}) überdeckt «{alle[j].Text}» ({string.Join(",", c)})");
            }
        Assert.True(ueberdeckt.Count == 0, string.Join("\n", ueberdeckt));
    }

    [Fact]
    public void Katalog_ListeSanitaerZusammenstellung_MitAnlage_UndSchalterBelastung()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-LI-SAN",
            ["eigenschaften"] = new JsonObject { ["art"] = "liste", ["liste"] = "sanitaer_zusammenstellung", ["anlage"] = "A-SAN" },
        }));
        var a = Hole(p, "AN-LI-SAN");
        Assert.Equal("sanitaer_zusammenstellung", a.Text("liste"));
        Assert.Equal("A-SAN", a.Text("anlage"));
        // die Liste lässt sich mit den Zahlen der Abfrage füllen (die Oberfläche fragt mit der Anlage der Ansicht)
        Assert.Equal(16, Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = a.Text("anlage") })["apparate"]!.GetValue<int>());
        // der Schalter ist ein bool; eine andere Liste als im Katalog lehnt der Kern ab
        var schalter = Kat.Klasse("Ansicht").Eigenschaften.Single(e => e.Name == "belastung");
        Assert.Equal("bool", schalter.Typ);
        Nein(p.Ausfuehren("element_aendern", new JsonObject
        { ["element"] = "AN-LI-SAN", ["eigenschaften"] = new JsonObject { ["liste"] = "gibt_es_nicht" } }));
    }

    [Fact]
    public void WarmwasserUeberDenWassererwaermer_KaltUndWarmInEinemText()
    {
        // Modul 3 Abnahmeszenario: das Kaltwasser am Hausanschluss trägt auch die Warmwasser-Apparate (ΣKW 24, ΣWW 6)
        var s = new Abnahme3Szenario();
        var netz = SanitaerBelastung.Ergebnis(s.P.Modell, s.P.Katalog);
        var ha = netz.Leitung(Hole(s.P, "KW-V1-0").Id)!;
        Assert.Equal("ΣLU 24 / 6", SchemaBelastung.TextLu(ha.SummeLuKw, ha.SummeLuWw));
        var warm = netz.Ueber(netz.LeitungMedium.Where(x => x.Value == "WW").Select(x => x.Key));
        Assert.Equal("ΣLU – / 6", SchemaBelastung.TextLu(warm.SummeLuKw, warm.SummeLuWw));
    }
}
