using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HeizungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Lüftung (Christian 02.11.2026): Durchlässe bestimmen die Luftmenge, summiert zum Gerät; Fliessrichtung auf Befehl aus dem Lüftungsgerät.
/// Netz und Luftmengen FIKTIV: LG-1.zul – K-0 – Bogen BO-1 – K-1 – TS-1 – K-2 – ZD-1 (120 m³/h) bzw. K-3 – ZD-2 (ohne Angabe, Raum R-1 Zuluft 300 m³/h).</summary>
public class LueftungMengenTests
{
    static Projekt Netz(bool k1Verkehrt)
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Raum","kennzeichen":"R-1","name":"Büro","geschoss":"EG","eigenschaften":{"nummer":"R-1","nutzung":"Büro","lichteHoehe":2500,"zuluft":300},"geometrie":{"art":"polygon","punkte":[[0,-1000],[8000,-1000],[8000,5000],[0,5000]]}}""");
        Punkt(p, "Lueftungsgeraet", "LG-1", null, 0, 0, 2500);
        Punkt(p, "Bogen", "BO-1", null, 1000, 0, 2500);
        Punkt(p, "TStueck", "TS-1", null, 2000, 0, 2500);
        Punkt(p, "Zuluftdurchlass", "ZD-1", null, 4000, 0, 2500, O("""{"volumenstrom":120}"""), "R-1");
        Punkt(p, "Zuluftdurchlass", "ZD-2", null, 2000, 2000, 2500, null, "R-1");
        void Kanal(string kz, string von, string nach, double[] a, double[] b) => Gut(p, "leitung_ziehen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["durchmesser"] = 200 },
            ["punkte"] = new JsonArray(new JsonArray(a[0], a[1], a[2]), new JsonArray(b[0], b[1], b[2])),
            ["von"] = von, ["nach"] = nach,
        });
        Kanal("K-0", "LG-1.zul", "BO-1.a", [0, 0, 2500], [1000, 0, 2500]);
        // verkehrt wie nach einem Import: zwischen zwei Formteilen (ungerichtet) nimmt Verbinden die Leitung auch gegen den Fluss
        if (k1Verkehrt) Kanal("K-1", "TS-1.a", "BO-1.b", [2000, 0, 2500], [1000, 0, 2500]);
        else Kanal("K-1", "BO-1.b", "TS-1.a", [1000, 0, 2500], [2000, 0, 2500]);
        Kanal("K-2", "TS-1.b", "ZD-1.zul", [2000, 0, 2500], [4000, 0, 2500]);
        Kanal("K-3", "TS-1.c", "ZD-2.zul", [2000, 0, 2500], [2000, 2000, 2500]);
        return p;
    }

    static double? M3h(Projekt p, string kz) => LueftungMengen.Ergebnis(p.Modell, p.Katalog) is var n && p.Modell.Hole(kz) is var e
        && n.Durchlaesse.TryGetValue(e.Id, out var d) ? d.M3h : n.Kanal(e.Id)?.M3h;

    [Fact]
    public void Volumenstrom_AngabeUndRaumrest_ZumGeraetSummiert()
    {
        var p = Netz(k1Verkehrt: false);
        Assert.Equal(120, M3h(p, "ZD-1"));
        Assert.Equal(180, M3h(p, "ZD-2"));   // Raum 300 − Angabe 120, auf den einen Durchlass ohne Angabe
        Assert.Equal(120, M3h(p, "K-2"));
        Assert.Equal(180, M3h(p, "K-3"));
        Assert.Equal(300, M3h(p, "K-1"));
        Assert.Equal(300, M3h(p, "K-0"));
    }

    [Fact]
    public void Fliessrichtung_AusLueftungsgeraet_KehrtVerkehrtenKanalUm()
    {
        var p = Netz(k1Verkehrt: true);
        Assert.Null(M3h(p, "K-1"));   // gegen den Fluss gezeichnet: der Weg vom Durchlass zum Gerät bricht ab
        var e = Gut(p, "fliessrichtung_aus_quelle", """{"quelle":"LG-1"}""");
        var k1 = p.Modell.Hole("K-1");
        Assert.Equal([1000, 0, 2500], k1.Geometrie!.Punkte[0]);
        Assert.Equal("BO-1", p.Modell.Finde(p.Modell.VerbindungAn(k1.Id, "anfang")!.Gegenueber(k1.Id, "anfang")!.Element)!.Kennzeichen);
        Assert.Equal("TS-1", p.Modell.Finde(p.Modell.VerbindungAn(k1.Id, "ende")!.Gegenueber(k1.Id, "ende")!.Element)!.Kennzeichen);
        Assert.Equal(300, M3h(p, "K-1"));
        Assert.Contains(e.Hinweise, h => h.Contains("4 Leitung(en) erreicht, 1 umgekehrt"));
        // zweiter Lauf ändert nichts mehr
        Assert.Contains(Gut(p, "fliessrichtung_aus_quelle", """{"quelle":"LG-1"}""").Hinweise, h => h.Contains("0 umgekehrt"));
    }
}
