using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Bögen mit Radius (30.10.2026, Christian: «alle ansichten von oben zeichnen keine bogenradien»; «rohrbogen sind meist ein
/// rotierter kreis mit einsteckmuffen»; «ausnahme geberit-pe dort sind es segmente»): Bogen-Familien aus zwei Schenkelzylindern werden
/// bei der Auswertung ein Sweep mit Radius; Geberit PE und Silent-db20 bleiben Segment.</summary>
public class BogenRadiusTests
{
    static JsonObject ZweiZylinder(string name)
    {
        var d = (JsonObject)JsonNode.Parse(File.ReadAllText(Modul3Hilfe.Datei("test_bogen")))!;
        d["name"] = name;
        d["koerper"] = JsonNode.Parse("""
            [{"id":"schenkel_a","art":"zylinder","material":"rohr","von":["-la",0,0],"nach":[0,0,0],"r":"da / 2"},
             {"id":"schenkel_b","art":"zylinder","material":"rohr","von":[0,0,0],"nach":["lb * cos(winkel)","lb * sin(winkel)",0],"r":"da / 2"}]
            """);
        return d;
    }

    [Fact]
    public void Zwei_Schenkelzylinder_werden_ein_Bogen_mit_Radius()
    {
        var f = FamilieTestHilfe.Def(ZweiZylinder("Testbogen Zylinder (fiktiv)"));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler.Select(x => x.Meldung)));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.True(a.Gueltig, string.Join("; ", a.Fehler.Select(x => x.Meldung)));
        var t = Assert.Single(a.Koerper.Where(k => k.Teil is not null)).Teil!;
        Assert.Equal("koerper", t.Art);
        // die äussere Ecke des Knicks ist weg: kein Eckpunkt weiter als der Rohrradius vom Achsschnittpunkt entfernt in der «Ecke» (x > 0, y < 0)
        var da = a.Werte["da"]!.Value.Zahl;
        Assert.DoesNotContain(t.Ecken!, p => p[0] > da * 0.3 && p[1] < -da * 0.3);
    }

    /// <summary>Befund Christian 14.11.2026 («bei rohrbogen haben wir die Radien verloren»): die Bibliotheksbögen haben seit 09.11.2026 eine Kugel im
    /// Achsschnittpunkt («knick»); sie wird mit den Schenkeln durch den Bogen ersetzt, statt den Radius ganz zu verhindern.</summary>
    [Fact]
    public void Kugel_im_Knick_wird_mit_ersetzt()
    {
        var d = ZweiZylinder("Testbogen Zylinder mit Knickkugel (fiktiv)");
        ((JsonArray)d["koerper"]!).Add(JsonNode.Parse("""{"id":"knick","art":"kugel","material":"rohr","mitte":[0,0,0],"r":"da / 2"}"""));
        var f = FamilieTestHilfe.Def(d);
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.True(a.Gueltig, string.Join("; ", a.Fehler.Select(x => x.Meldung)));
        var t = Assert.Single(a.Koerper.Where(k => k.Teil is not null)).Teil!;
        Assert.Equal("koerper", t.Art);
        var da = a.Werte["da"]!.Value.Zahl;
        Assert.DoesNotContain(t.Ecken!, p => p[0] > da * 0.3 && p[1] < -da * 0.3);
    }

    [Theory]
    [InlineData("Geberit PE Bogen")]
    [InlineData("Geberit Silent-db20 Bogen")]
    public void Geberit_PE_und_Silent_bleiben_Segment(string name)
    {
        var f = FamilieTestHilfe.Def(ZweiZylinder(name));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.Equal(2, a.Koerper.Count(k => k.Teil is { Art: "zylinder" }));
        // grob einlinig mit Gehrung: Anschluss a → Achsschnittpunkt → Anschluss b
        Assert.Equal(3, Assert.Single(a.Achsen).Count);
    }

    /// <summary>Befund Christian 02.11.2026 («Ansichten der Familien, fehlende Radien … muss auch beim Konstruieren korrekt aussehen»): im Detailgrad
    /// grob zeichnet das Formteil einlinig entlang der Achse mit dem Bogen – nicht den Hüllquader.</summary>
    [Fact]
    public void Grob_einlinig_als_Ecke_Achse_mit_Radius()
    {
        var f = FamilieTestHilfe.Def(ZweiZylinder("Testbogen Zylinder (fiktiv)"));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        var achse = Assert.Single(a.Achsen);
        Assert.True(achse.Count > 5, "Bogen in Stücken, nicht nur Gehrung");
        // alle Bogenpunkte liegen auf der Innenseite des Knicks (keiner im Achsschnittpunkt, ausser bei Gehrung)
        Assert.DoesNotContain(achse, p => Math.Abs(p[0]) < 1e-6 && Math.Abs(p[1]) < 1e-6);
        // einlinig (grob) ohne Radius: Ecke am Achsschnittpunkt (Christian 05.11.2026: «nicht die bögen weglassen sondern den radius weglassen»)
        var g = FamilienGrafik.Grundriss(a, [0, 0, 0], 0, "grob", 50, null, ArchitekturHilfe.Kat, null, null, "apparat", null);
        var linie = Assert.Single(g.Primitive, p => p.Art == "polylinie");
        Assert.Equal(3, linie.Punkte.Count);
        Assert.Equal(0, linie.Punkte[1][0], 6); Assert.Equal(0, linie.Punkte[1][1], 6);
        Assert.DoesNotContain(g.Primitive, p => p.Art == "polygon");
    }

    /// <summary>«es muss in allen ansichten korrekt sein» (Christian 02.11.2026): ein Hüllquader für grob weicht in Schnitt, Ansicht und 3D dem
    /// Bogen mit Radius (Körper von mittel).</summary>
    [Fact]
    public void Grob_Koerper_sind_die_von_mittel_statt_Huellquader()
    {
        var d = ZweiZylinder("Testbogen Zylinder (fiktiv)");
        foreach (var k in d["koerper"]!.AsArray()) k!["detailgrad"] = new JsonArray("mittel", "fein");
        d["koerper"]!.AsArray().Add(JsonNode.Parse("""{"id":"huelle","art":"quader","detailgrad":["grob"],"material":"rohr","von":["-la","-da / 2","-da / 2"],"bis":["da","lb","da / 2"]}"""));
        var f = FamilieTestHilfe.Def(d);
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler.Select(x => x.Meldung)));
        var a = FamilienAuswertung.Aufloesen(f, f.Typen[0].Name, null);
        Assert.DoesNotContain(a.Teile("grob"), t => t.Art == "quader");
        Assert.Equal(a.Teile("mittel").Count, a.Teile("grob").Count);
    }

    /// <summary>Gesamtprüfung 13.11.2026: die Achse des schrägen Abzweigs (45°) endet im Achsschnittpunkt – vorher im Lot vom Anschluss auf den
    /// Durchgang (hier an dessen Ende), die einlinige Darstellung und die Dämmung liefen damit quer statt schräg.</summary>
    [Fact]
    public void Achse_des_schraegen_Abzweigs_endet_im_Achsschnittpunkt()
    {
        var f = FamilieTestHilfe.Def(FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Abzweig 45 (fiktiv)", "kategorie": "TStueck", "parameter": [ { "name": "winkel", "art": "typ", "datentyp": "winkel", "wert": 45 } ],
              "typen": [{ "name": "A", "werte": {} }],
              "koerper": [ { "id": "d", "art": "zylinder", "von": [-100, 0, 0], "nach": [100, 0, 0], "r": 55 },
                           { "id": "c", "art": "zylinder", "von": [0, 0, 0], "nach": ["-200 * cos(winkel)", "200 * sin(winkel)", 0], "r": 55 } ],
              "anschluesse": [
                { "name": "a", "medium": "*", "fluss": "beide", "art": "rohr", "form": "rund", "dn": 100, "lage": [-100, 0, 0], "richtung": [-1, 0, 0] },
                { "name": "b", "medium": "*", "fluss": "beide", "art": "rohr", "form": "rund", "dn": 100, "lage": [100, 0, 0], "richtung": [1, 0, 0] },
                { "name": "c", "medium": "*", "fluss": "beide", "art": "rohr", "form": "rund", "dn": 100, "lage": ["-200 * cos(winkel)", "200 * sin(winkel)", 0], "richtung": ["-cos(winkel)", "sin(winkel)", 0] } ] }
            """));
        Assert.True(f.Gueltig, string.Join("; ", f.Fehler.Select(x => x.Meldung)));
        var a = FamilienAuswertung.Aufloesen(f, "A", null);
        Assert.Equal(2, a.Achsen.Count);
        var abzweig = a.Achsen[1];
        Assert.Equal(0, abzweig[0][0], 6); Assert.Equal(0, abzweig[0][1], 6);                      // Fuss im Achsschnittpunkt
        Assert.Equal(-141.421356, abzweig[1][0], 4); Assert.Equal(141.421356, abzweig[1][1], 4);   // Anschluss c: 200·(−cos 45°, sin 45°)
    }
}
