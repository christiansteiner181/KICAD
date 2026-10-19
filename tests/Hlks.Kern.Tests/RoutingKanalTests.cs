using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B2: Routing für rechteckige Kanäle mit den FIKTIVEN Testfamilien tests/daten/familien/test_kanalserie,
/// test_kanalbogen (r = b, Einbaulänge r·tan(w/2)), test_kanal_t (l = 300, lc = 250), test_kanalreduktion (l = 200).</summary>
public class RoutingKanalTests
{
    static BefehlsErgebnis Kanal(Projekt p, string groesse, double[][] punkte, JsonObject? extra = null, bool trocken = false) =>
        Zeichnen(p, groesse, punkte, extra, serie: "fam-test-kanalserie", system: "ZUL", trocken: trocken);

    [Fact]
    public void Testfamilien_SindGueltig()
    {
        var p = MitKanal();
        var s = Leitungsserie.Hole(p.Modell, p.Katalog, "fam-test-kanalserie");
        Assert.Equal("rechteck", s.Querschnitt);
        Assert.Equal("Kanal", s.Kategorie);
        var g = Leitungsgroesse.Rechteck(400, 200);
        var bogen = s.Formteil(p.Modell, p.Katalog, "bogen", g, winkel: 90);
        Assert.True(bogen.Ok, bogen.Fehler);
        Etwa(400, bogen.Geometrie!.Einbaulaenge("a"));   // r = b = 400, tan 45° = 1
        Assert.Contains("45°, 90°", s.Formteil(p.Modell, p.Katalog, "bogen", g, winkel: 60).Fehler);
        var t = s.Formteil(p.Modell, p.Katalog, "tstueck", g, Leitungsgroesse.Rechteck(300, 200));
        Assert.Equal("400x200/300x200", t.Typ);
        Etwa(300, t.Geometrie!.Einbaulaenge("a")); Etwa(250, t.Geometrie.Einbaulaenge("c"));
        var r = s.Formteil(p.Modell, p.Katalog, "reduktion", g, Leitungsgroesse.Rechteck(300, 200));
        Assert.Equal("400x200/300x200", r.Typ);
        Etwa(200, r.Geometrie!.Einbaulaenge("b"));
    }

    [Fact]
    public void KanalLZug_400x200_BogenInDerWaagrechten()
    {
        var p = MitKanal();
        var e = Ok(Kanal(p, "400x200", [[0, 0, 2500], [3000, 0, 2500], [3000, 2000, 2500]]));
        Assert.Equal(["KA-1", "KA-2"], Angelegt(e, "Kanal"));
        // Kanalbogen 400x200, 90°: r = b = 400 → La = Lb = 400 mm
        Gerade(p, "KA-1", [0, 0, 2500], [2600, 0, 2500]);       // 3000 − 400 = 2600 mm
        Gerade(p, "KA-2", [3000, 400, 2500], [3000, 2000, 2500]); // 2000 − 400 = 1600 mm
        var ka = Hole(p, "KA-1");
        Assert.Equal(400, ka.Zahl("breite")); Assert.Equal(200, ka.Zahl("hoehe"));
        Assert.Equal("familie", ka.Eigenschaft("breite")!.Herkunft.Art);
        Assert.Null(ka.Zahl("durchmesser"));
        Assert.All(ka.Anschluesse, a => { Assert.Equal(400, a.B); Assert.Equal(200, a.H); });
        // waagrechter Bogen: lokale z senkrecht → h senkrecht, b waagrecht (ANNAHME Querschnittslage)
        var l = Familien.Einbaulage(Hole(p, "BO-1"))!;
        Etwa([0, 0, 1], l.Z.Feld(), 1e-9);
        Assert.Equal(Leitungsgroesse.Rechteck(400, 200), Stelle(p, "BO-1", "a").Groesse);
        IstVerbunden(p, "KA-1", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "KA-2", "anfang");
        Sauber(p);
    }

    [Fact]
    public void KanalSteigbogen_Und45Grad()
    {
        var p = MitKanal();
        Ok(Kanal(p, "300x200", [[0, 0, 2500], [2000, 0, 2500], [2000, 0, 4500]]));
        // Kanalbogen 300x200, 90°: La = Lb = 300; senkrechte Biegeebene → lokale z waagrecht (0, −1, 0)
        Gerade(p, "KA-1", [0, 0, 2500], [1700, 0, 2500]);
        Gerade(p, "KA-2", [2000, 0, 2800], [2000, 0, 4500]);
        Etwa([0, -1, 0], Familien.Einbaulage(Hole(p, "BO-1"))!.Z.Feld(), 1e-9);
        // 45°: La = 300·tan 22.5° = 124.264 mm
        Ok(Kanal(p, "300x200", [[0, 5000, 2500], [1000, 5000, 2500], [2000, 6000, 2500]]));
        double la = 300 * Tan(22.5);
        Etwa(124.264, la, 1e-3);
        Gerade(p, "KA-3", [0, 5000, 2500], [1000 - la, 5000, 2500]);
        Sauber(p);
        // 60° gibt es in der Kanalserie nicht
        Assert.Contains("45°, 90°", Nein(Kanal(p, "300x200", [[0, 9000, 2500], [1000, 9000, 2500], [1500, 9866.0254, 2500]])).Fehler);
    }

    [Fact]
    public void KanalAbzweig_TMitKleineremAbzweig_UndReduktionImZug()
    {
        var p = MitKanal();
        Ok(Kanal(p, "400x200", [[0, 0, 2500], [4000, 0, 2500]]));
        Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "KA-1", ["punkt"] = new JsonArray(2000, 0, 2500), ["richtung"] = new JsonArray(0, 1, 0), ["laenge"] = 1500, ["groesse"] = "300x200",
        }));
        // T «400x200/300x200» (l = 300, lc = 250) – passt direkt, keine Reduktion
        Assert.Equal("400x200/300x200", Familien.Typ(Hole(p, "TS-1")));
        Gerade(p, "KA-1", [0, 0, 2500], [1700, 0, 2500]);        // 2000 − 300
        Gerade(p, "KA-1-2", [2300, 0, 2500], [4000, 0, 2500]);   // 2000 + 300
        Gerade(p, "KA-2", [2000, 250, 2500], [2000, 1500, 2500]); // 1500 − 250 = 1250 mm
        Assert.Equal(300, Hole(p, "KA-2").Zahl("breite"));
        Assert.Null(p.Modell.Finde("RD-1"));
        Sauber(p);
        // 200x200 am T: kein T mit diesem Abzweig, Reduktion 400x200/200x200 gibt es nicht → abgelehnt mit Vorschlag
        var nein = Nein(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "KA-1", ["punkt"] = new JsonArray(800, 0, 2500), ["richtung"] = new JsonArray(0, -1, 0), ["laenge"] = 1500, ["groesse"] = "200x200",
        }));
        Assert.Contains("zwei Schritten", nein.Vorschlag);
        // Grössenwechsel der letzten Geraden: Reduktion 400x200/300x200 (l = 200) hinter dem Bogen (Lb = 400)
        Ok(Kanal(p, "400x200", [[0, 5000, 2500], [3000, 5000, 2500], [3000, 7000, 2500]]));
        var letzte = Angelegt(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("KA-4"), ["groesse"] = new JsonObject { ["b"] = 300, ["h"] = 200 } }), "Reduktion").Single();
        Etwa([3000, 5400, 2500], Stelle(p, letzte, "a").Lage);
        Etwa([3000, 5600, 2500], Stelle(p, letzte, "b").Lage);
        Gerade(p, "KA-4", [3000, 5600, 2500], [3000, 7000, 2500]);   // 2000 − 400 − 200 = 1400 mm
        Assert.Equal(300, Hole(p, "KA-4").Zahl("breite"));
        Sauber(p);
    }

    [Fact]
    public void KanalGroesse_FalscheArt_Abgelehnt()
    {
        var p = MitKanal();
        Assert.Contains("passt nicht zur Serie", Nein(Kanal(p, "20", [[0, 0, 0], [1000, 0, 0]])).Fehler);
        Assert.Contains("keine Grösse 450 × 200 mm", Nein(Kanal(p, "x", [[0, 0, 0], [1000, 0, 0]], new JsonObject { ["groesse"] = new JsonObject { ["b"] = 450, ["h"] = 200 } })).Fehler);
    }
}
