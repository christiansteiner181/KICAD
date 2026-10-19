using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HuelleTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Fensterkennwerte der Hülle (Modul 1c, B2): Zahlen nur mit Quelle, Uw-Formel, ausdrückliches uw, Nachführung.
/// Alle Zahlen sind frei gewählte Testwerte (keine Norm, keine Herstellerwerte).</summary>
public class HuelleFensterTests
{
    static BefehlsErgebnis Kennwerte(Projekt p, JsonObject par, bool trocken = false) => p.Ausfuehren("fenster_kennwerte", par, trocken);

    static JsonObject Par(string[] elemente, double? ug = null, double? uf = null, double? psiG = null, double? rahmen = null, string? quelle = TestQuelle)
    {
        var o = new JsonObject { ["elemente"] = new JsonArray(elemente.Select(x => (JsonNode)x).ToArray()) };
        if (ug is not null) o["ug"] = ug;
        if (uf is not null) o["uf"] = uf;
        if (psiG is not null) o["psiG"] = psiG;
        if (rahmen is not null) o["rahmenanteil"] = rahmen;
        if (quelle is not null) o["quelle"] = quelle;
        return o;
    }

    /// <summary>Fenster F-1 in W-1: lichte Breite und Höhe in mm (Testmasse).</summary>
    static Projekt MitFenster(double breite = 1000, double hoehe = 1000)
    {
        var p = Rechteck();
        Ok(Fenster(p, "W-1", 3000, breite, hoehe));
        return p;
    }

    // ---------------------------------------------------------------- Uw-Formel

    [Fact]
    public void Uw_Formel_QuadratischesFenster()
    {
        var p = MitFenster(1000, 1000);                                         // A = 1 m²
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25))); // Testwerte
        // Ag = 0.75, Af = 0.25, lg = 2·(1 + 1)·√0.75; Uw = (0.75·1 + 0.25·2 + lg·0.1)/1
        double lg = 2 * (1.0 + 1.0) * Math.Sqrt(0.75);
        Gleich(0.75 * 1.0 + 0.25 * 2.0 + lg * 0.1, Zahl(p, "F-1", "uw"), 1e-9);
        var uw = Hole(p, "F-1").Eigenschaft("uw")!;
        Assert.Equal("berechnung", uw.Herkunft.Art);
        Assert.Equal("W/(m²·K)", uw.Einheit);
        Assert.Contains("Ag·Ug + Af·Uf + lg·ψg", uw.Herkunft.Quelle);          // Formel
        Assert.Contains("Annahme", uw.Herkunft.Quelle);                         // Annahme «Glas ähnlich zum Fenster» benannt
        Assert.Contains(TestQuelle, uw.Herkunft.Quelle);                        // Quelle der Eingaben
    }

    [Fact]
    public void Uw_Formel_RechteckigesFensterRahmenanteilNull()
    {
        var p = MitFenster(1200, 1500);                                         // A = 1.8 m²
        Ok(Kennwerte(p, Par(["F-1"], ug: 0.8, uf: 1.5, psiG: 0.05, rahmen: 0)));
        // ohne Rahmen: Af = 0, Ag = A, lg = 2·(b + h) = 5.4 m; Uw = (1.8·0.8 + 5.4·0.05)/1.8
        Gleich((1.8 * 0.8 + 5.4 * 0.05) / 1.8, Zahl(p, "F-1", "uw"), 1e-9);
    }

    [Fact]
    public void Uw_Formel_NurRahmen()
    {
        var p = MitFenster(1000, 1000);
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 1)));
        Gleich(2.0, Zahl(p, "F-1", "uw"), 1e-9);                                // Ag = 0, lg = 0 → Uw = Uf
    }

    [Fact]
    public void Uw_EntstehtErstWennAlleEingabenDaSind_UndNenntWasFehlt()
    {
        var p = MitFenster();
        var e1 = Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0)));
        Assert.Null(Hole(p, "F-1").Eigenschaft("uw"));
        Assert.Contains(e1.Hinweise, h => h.Contains("kein Uw") && h.Contains("ψg") && h.Contains("Rahmenanteil"));
        var e2 = Ok(Kennwerte(p, Par(["F-1"], psiG: 0.1)));
        Assert.Null(Hole(p, "F-1").Eigenschaft("uw"));
        Assert.Contains(e2.Hinweise, h => h.Contains("Rahmenanteil"));
        Ok(Kennwerte(p, Par(["F-1"], rahmen: 0.3)));
        Assert.NotNull(Hole(p, "F-1").Eigenschaft("uw"));
    }

    // ---------------------------------------------------------------- Quelle Pflicht

    [Fact]
    public void Kennwerte_OhneQuelleWerdenAbgelehnt()
    {
        var p = MitFenster();
        var start = p.Modell.Pruefsumme();
        var n = Nein(Kennwerte(p, Par(["F-1"], ug: 1.0, quelle: null)));
        Assert.Contains("Quelle", n.Fehler);
        Assert.Contains("ug", n.Fehler);
        Nein(Kennwerte(p, Par(["F-1"], ug: 1.0, quelle: "  ")));
        var uw = new JsonObject { ["elemente"] = new JsonArray("F-1"), ["uw"] = 1.4 };
        Nein(Kennwerte(p, uw));                                                  // auch ein ausdrückliches uw braucht eine Quelle
        Assert.Equal(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Kennwerte_UngueltigeWerteUndElemente()
    {
        var p = MitFenster();
        Ok(Tuer(p, "W-2"));                                                      // T-1
        Nein(Kennwerte(p, Par(["F-1"], ug: 0)));
        Nein(Kennwerte(p, Par(["F-1"], ug: -1)));
        Nein(Kennwerte(p, Par(["F-1"], rahmen: 1.5)));
        Nein(Kennwerte(p, Par(["F-1"], rahmen: -0.1)));
        Nein(Kennwerte(p, Par(["F-1"], psiG: -0.1)));
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["g"] = 1.2, ["quelle"] = TestQuelle }));
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["fc"] = 1.2, ["quelle"] = TestQuelle }));
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["sonnenschutz"] = "vorhang" }));
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1") }));                                  // nichts angegeben
        Nein(Kennwerte(p, Par(["W-1"], ug: 1.0)));                                                                   // Wand
        var fuerTuer = Nein(Kennwerte(p, Par(["T-1"], ug: 1.0)));                                                    // ug gibt es bei Türen nicht
        Assert.Contains("ud", fuerTuer.Vorschlag);
        var fuerFenster = Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["ud"] = 1.0, ["quelle"] = TestQuelle }));
        Assert.Contains("Türen", fuerFenster.Vorschlag);
        // alles oder nichts: Fenster und Tür gemischt mit ug lehnt ab, auch das Fenster bleibt unverändert
        Nein(Kennwerte(p, Par(["F-1", "T-1"], ug: 1.0)));
        Assert.Null(Hole(p, "F-1").Eigenschaft("ug"));
    }

    [Fact]
    public void Kennwerte_SonnenschutzBrauchtKeineQuelle_GUndFcMitQuelle()
    {
        var p = MitFenster();
        Ok(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["sonnenschutz"] = "aussen" }));
        Assert.Equal("aussen", Hole(p, "F-1").Text("sonnenschutz"));
        Ok(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["g"] = 0.5, ["fc"] = 0.2, ["quelle"] = "Testdatenblatt" }));   // Testwerte
        var g = Hole(p, "F-1").Eigenschaft("g")!;
        Assert.Equal(0.5, g.AlsZahl());
        Assert.Equal("Testdatenblatt", g.Herkunft.Quelle);
        Assert.Equal(0.2, Zahl(p, "F-1", "fc"));
    }

    [Fact]
    public void Kennwerte_HerkunftMenschUndKi()
    {
        var p = Projekt.Anlegen(Kat, null, "Test KI", "Claude", "pc", "ki");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 3000 } }));
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0]), ["dicke"] = 200 }));
        Ok(Fenster(p));
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0)));
        var h = Hole(p, "F-1").Eigenschaft("ug")!.Herkunft;
        Assert.Equal("ki", h.Art);
        Assert.Equal(TestQuelle, h.Quelle);
    }

    // ---------------------------------------------------------------- ausdrückliches uw geht vor

    [Fact]
    public void AusdruecklichesUw_GehtVorUndBleibtBeiAenderungen()
    {
        var p = MitFenster();
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25)));
        var gerechnet = Zahl(p, "F-1", "uw");
        Assert.Equal("berechnung", Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Art);

        var e = Ok(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1"), ["uw"] = 1.7, ["quelle"] = "Testdatenblatt Fenster" }));   // Testwert
        Assert.Equal(1.7, Zahl(p, "F-1", "uw"));
        Assert.Equal("mensch", Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Art);
        Assert.Equal("Testdatenblatt Fenster", Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.Contains("geht vor"));

        // Eingaben ändern: ausdrückliches uw bleibt
        Ok(Kennwerte(p, Par(["F-1"], ug: 0.5)));
        Assert.Equal(1.7, Zahl(p, "F-1", "uw"));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "F-1", ["eigenschaften"] = new JsonObject { ["hoehe"] = 1100 } }));
        Assert.Equal(1.7, Zahl(p, "F-1", "uw"));

        // ausdrücklichen Wert entfernen → es wird wieder gerechnet (mit dem neuen Ug und der neuen Höhe)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "F-1", ["eigenschaften"] = new JsonObject { ["uw"] = null } }));
        var neu = Zahl(p, "F-1", "uw");
        Assert.NotNull(neu);
        Assert.NotEqual(1.7, neu);
        Assert.NotEqual(gerechnet, neu);
        Assert.Equal("berechnung", Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Art);
    }

    // ---------------------------------------------------------------- Nachführung

    [Fact]
    public void Nachfuehrung_KennwertOderMassAendertUwImSelbenPaket()
    {
        var p = MitFenster(1000, 1000);
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25)));
        var vorher = Zahl(p, "F-1", "uw")!.Value;

        // Ug ändern
        var e = Ok(Kennwerte(p, Par(["F-1"], ug: 0.8)));
        double lg = 2 * (1.0 + 1.0) * Math.Sqrt(0.75);
        Gleich(0.75 * 0.8 + 0.25 * 2.0 + lg * 0.1, Zahl(p, "F-1", "uw"), 1e-9);
        Assert.Contains(e.Hinweise, h => h.Contains("Uw neu gerechnet") && h.Contains("F-1"));

        // Mass ändern: Höhe 1000 → 2000 mm, A = 2 m², lg = 2·(1 + 2)·√0.75
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "F-1", ["eigenschaften"] = new JsonObject { ["hoehe"] = 2000 } }));
        double lg2 = 2 * (1.0 + 2.0) * Math.Sqrt(0.75);
        Gleich((1.5 * 0.8 + 0.5 * 2.0 + lg2 * 0.1) / 2.0, Zahl(p, "F-1", "uw"), 1e-9);

        // Quelle der Eingabe entfernen (am Befehl vorbei): ohne Quelle gilt der Wert nicht, das berechnete Uw fällt weg
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "F-1", ["eigenschaften"] = new JsonObject { ["psiG"] = 0.1 } }));
        Assert.Null(Hole(p, "F-1").Eigenschaft("uw"));
        Assert.True(vorher > 0);
    }

    [Fact]
    public void Nachfuehrung_FensterMitDerWandVerschiebenLaesstUwStehen()
    {
        var p = MitFenster();
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25)));
        var uw = Zahl(p, "F-1", "uw");
        var paketVorher = Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Zeit;
        Ok(p.Ausfuehren("elemente_verschieben", new JsonObject { ["elemente"] = new JsonArray("W-1"), ["dx"] = 100 }));
        Assert.Equal(uw, Zahl(p, "F-1", "uw"));
        Assert.Equal(paketVorher, Hole(p, "F-1").Eigenschaft("uw")!.Herkunft.Zeit);       // unverändert, nicht neu geschrieben
    }

    [Fact]
    public void Kennwerte_ZweitesMalGleicheWerteKeineAenderung()
    {
        var p = MitFenster();
        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0)));
        var nochmal = Kennwerte(p, Par(["F-1"], ug: 1.0));
        Assert.True(nochmal.Angenommen);
        Assert.Null(nochmal.PaketId);
        Assert.Contains(nochmal.Hinweise, h => h.Contains("Keine Änderung"));
    }

    // ---------------------------------------------------------------- Türen

    [Fact]
    public void Tuer_UdMitQuelle()
    {
        var p = MitFenster();
        Ok(Tuer(p, "W-2"));
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("T-1"), ["ud"] = 1.2 }));          // Quelle fehlt
        Ok(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("T-1"), ["ud"] = 1.2, ["quelle"] = "Testdatenblatt Tür" }));
        var ud = Hole(p, "T-1").Eigenschaft("ud")!;
        Assert.Equal(1.2, ud.AlsZahl());
        Assert.Equal("W/(m²·K)", ud.Einheit);
        Assert.Equal("Testdatenblatt Tür", ud.Herkunft.Quelle);
        Assert.Equal("mensch", ud.Herkunft.Art);
        // Fenster und Tür gemeinsam: Quelle gilt für beide, wenn beide die Eigenschaft haben (hier keine)
        Nein(Kennwerte(p, new JsonObject { ["elemente"] = new JsonArray("F-1", "T-1"), ["ud"] = 1.0, ["quelle"] = TestQuelle }));
    }

    // ---------------------------------------------------------------- Trockenlauf, Rückgängig

    [Fact]
    public void Kennwerte_TrockenlaufRueckgaengig()
    {
        var p = MitFenster();
        var start = p.Modell.Pruefsumme();
        var tl = Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25), trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Null(Hole(p, "F-1").Eigenschaft("ug"));

        Ok(Kennwerte(p, Par(["F-1"], ug: 1.0, uf: 2.0, psiG: 0.1, rahmen: 0.25)));
        Assert.NotNull(Hole(p, "F-1").Eigenschaft("uw"));
        Ok(p.Rueckgaengig());
        Assert.Null(Hole(p, "F-1").Eigenschaft("ug"));
        Assert.Null(Hole(p, "F-1").Eigenschaft("uw"));                              // gerechnetes Uw geht im selben Paket mit zurück
        Ok(p.Wiederholen());
        Assert.NotNull(Hole(p, "F-1").Eigenschaft("uw"));
    }
}
