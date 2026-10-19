using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B2: leitung_aufteilen (Polylinie → Geraden mit Bögen), leitung_groesse und leitung_serie (Zug neu aufbauen,
/// Reduktionen an den Grenzen) und die Abfrage leitungszug. Handrechnungen aus den FIKTIVEN Testfamilien.</summary>
public class RoutingZugTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static JsonNode Abfrage(Projekt p, string leitung) => (JsonNode)Tuer.Abfrage(p, "leitungszug", new JsonObject { ["leitung"] = leitung });

    // ------------------------------------------------------------------ leitung_aufteilen

    [Fact]
    public void Aufteilen_PolylineDesBeispielprojekts_MitDerTestserie()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        foreach (var f in new[] { "test_rohrserie", "test_bogen", "test_tstueck", "test_reduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        // KW-V1-3 (DN 32): (8000, 2500, −500) → (9100, 2500, −500) → (9100, 9700, −500), Anfang an TS-KW-V1-WM.b, Ende an TS-KW-V1-S.a
        var verbraucherVorher = Netz.Folgen(p.Modell, p.Katalog, Hole(p, "MV-1"), "stromab", "KW").Where(s => Netz.IstEndgeraet(p.Katalog, s.Element)).Select(s => s.Element.Kennzeichen).Order().ToList();
        Assert.Equal(3, Pkt(p, "KW-V1-3").Count);
        var e = Ok(p.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = "KW-V1-3", ["serie"] = "fam-test-rohrserie" }));
        // DN 32: da = 37, r = 74, 90° → La = Lb = 74 mm
        Gerade(p, "KW-V1-3", [8000, 2500, -500], [9026, 2500, -500]);       // 9100 − 74 = 9026 → 1026 mm
        Gerade(p, "KW-V1-3-2", [9100, 2574, -500], [9100, 9700, -500]);     // 9700 − 2574 = 7126 mm
        var bo = Angelegt(e, "Bogen").Single();
        Etwa([9100, 2500, -500], Familien.Einfuegepunkt(Hole(p, bo)));
        Assert.Equal("DN 32", Familien.Typ(Hole(p, bo)));
        Assert.Equal("DN 32", Familien.Typ(Hole(p, "KW-V1-3")));
        Assert.Equal("DN 32", Familien.Typ(Hole(p, "KW-V1-3-2")));
        // Verbindungen an Anfang und Ende bleiben (logisch: die T-Stücke des Beispielprojekts haben keine Familie)
        IstVerbunden(p, "TS-KW-V1-WM", "b", "KW-V1-3", "anfang");
        IstVerbunden(p, "KW-V1-3", "ende", bo, "a");
        IstVerbunden(p, bo, "b", "KW-V1-3-2", "anfang");
        IstVerbunden(p, "KW-V1-3-2", "ende", "TS-KW-V1-S", "a");
        // Strang, System und Material übernommen
        var neu = Hole(p, "KW-V1-3-2");
        Assert.Equal(Hole(p, "KW-V1").Id, neu.TeilVon);
        Assert.Equal(Hole(p, "KW-V1-3").Text("material"), neu.Text("material"));
        Assert.Equal(Hole(p, "KW-V1").Id, Hole(p, bo).TeilVon);
        // Netz unverändert: dieselben Verbraucher stromab
        var verbraucherNachher = Netz.Folgen(p.Modell, p.Katalog, Hole(p, "MV-1"), "stromab", "KW").Where(s => Netz.IstEndgeraet(p.Katalog, s.Element)).Select(s => s.Element.Kennzeichen).Order().ToList();
        Assert.Equal(verbraucherVorher, verbraucherNachher);
        var befunde = Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Element is "KW-V1-3" or "KW-V1-3-2" || b.Element == bo).ToList();
        Assert.DoesNotContain(befunde, b => b.Code is "P13" or "P14" or "P15" or "P18");
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Schwere is "fehler" or "warnung");
        Ok(p.Rueckgaengig());
        Assert.Equal(3, Pkt(p, "KW-V1-3").Count);
        Assert.Null(p.Modell.Finde("KW-V1-3-2"));
    }

    [Fact]
    public void Aufteilen_SteigleitungMitGefaelleDesAltbestands_GroesseAusDn()
    {
        var p = MitSerie();
        // Polylinie ohne Serie (dn 20): waagrecht, Steigung, waagrecht
        Ok(RohrOhneSerie(p, "RO-A", 20, [0, 0, 500], [1000, 0, 500], [1000, 0, 3500], [2000, 0, 3500]));
        Ok(p.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitungen"] = new JsonArray("RO-A"), ["serie"] = "FAM-1" }));
        Gerade(p, "RO-A", [0, 0, 500], [950, 0, 500]);
        Gerade(p, "RO-A-2", [1000, 0, 550], [1000, 0, 3450]);
        Gerade(p, "RO-A-3", [1050, 0, 3500], [2000, 0, 3500]);
        Sauber(p);
        // schon Geraden: keine Änderung
        var e = Ok(p.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = "RO-A" }));
        Assert.Contains(e.Hinweise, h => h.Contains("schon eine Gerade"));
        // ohne Serie und ohne Parameter: abgelehnt
        Ok(RohrOhneSerie(p, "RO-B", 20, [0, 5000, 0], [1000, 5000, 0], [1000, 6000, 0]));
        Assert.Contains("keine Serie", Nein(p.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = "RO-B" })).Fehler);
    }

    // ------------------------------------------------------------------ leitung_groesse

    /// <summary>Hauptleitung DN 25 (0..4000, z 1000), Abzweig bei x = 1500 nach +y bis (1500, 1000) und weiter bis (2500, 1000).
    /// DN 25: da = 30 → T l = lc = 30, Bogen r = 60 → La = Lb = 60.</summary>
    static Projekt MitAbzweigZug()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [4000, 0, 1000]]));
        Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 1000), ["punkte"] = Punkte([1500, 1000, 1000], [2500, 1000, 1000]),
        }));
        Gerade(p, "RO-2", [1500, 30, 1000], [1500, 940, 1000]);   // c bei 30, Bogen bei 1000 − 60
        Gerade(p, "RO-3", [1560, 1000, 1000], [2500, 1000, 1000]);
        return p;
    }

    [Fact]
    public void Groesse_Zug_ReduktionAnDerGrenzeZumTStueck_UndZurueck()
    {
        var p = MitAbzweigZug();
        var bogenId = Hole(p, "BO-1").Id;
        var e = Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-2", ["zug"] = true, ["groesse"] = "DN 20" }));
        // Zug RO-2 – BO-1 – RO-3 (vom T-Stück bis zum offenen Ende) auf DN 20; an TS-1.c (DN 25) Reduktion 25/20 (l = 40)
        var rd = Angelegt(e, "Reduktion").Single();
        Assert.Equal("RD-1", rd);
        Assert.Equal("DN 25/20", Familien.Typ(Hole(p, "RD-1")));
        Etwa([1500, 30, 1000], Stelle(p, "RD-1", "a").Lage);
        Etwa([1500, 70, 1000], Stelle(p, "RD-1", "b").Lage);              // 30 + 40
        Gerade(p, "RO-2", [1500, 70, 1000], [1500, 950, 1000]);          // Bogen DN 20: La = 50 → 1000 − 50 → 880 mm
        Gerade(p, "RO-3", [1550, 1000, 1000], [2500, 1000, 1000]);       // 950 mm
        Assert.Equal(bogenId, Hole(p, "BO-1").Id);                       // Bogen wiederverwendet, Typ ersetzt
        Assert.Equal("DN 20", Familien.Typ(Hole(p, "BO-1")));
        Etwa([1500, 1000, 1000], Familien.Einfuegepunkt(Hole(p, "BO-1")));
        Assert.Equal("DN 20", Familien.Typ(Hole(p, "RO-2")));
        Assert.Equal(20, Hole(p, "RO-3").Zahl("dn"));
        IstVerbunden(p, "TS-1", "c", "RD-1", "a");
        IstVerbunden(p, "RD-1", "b", "RO-2", "anfang");
        IstVerbunden(p, "RO-2", "ende", "BO-1", "a");
        IstVerbunden(p, "BO-1", "b", "RO-3", "anfang");
        Sauber(p);
        // zurück auf DN 25: die Reduktion entfällt
        var z = Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-3", ["zug"] = true, ["groesse"] = 25 }));
        Assert.Null(p.Modell.Finde("RD-1"));
        Assert.Contains(z.Hinweise, h => h.Contains("Entfernt: RD-1"));
        Gerade(p, "RO-2", [1500, 30, 1000], [1500, 940, 1000]);
        IstVerbunden(p, "TS-1", "c", "RO-2", "anfang");
        Sauber(p);
    }

    [Fact]
    public void Groesse_EinzelneGerade_ReduktionAmBogen()
    {
        var p = MitAbzweigZug();
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("RO-3"), ["groesse"] = "DN 20" }));
        // nur RO-3: der Bogen bleibt DN 25 (Lb = 60), dahinter Reduktion 25/20 (40 mm), RO-3 ab 1560 + 40 = 1600
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "BO-1")));
        Etwa([1560, 1000, 1000], Stelle(p, "RD-1", "a").Lage);
        Gerade(p, "RO-3", [1600, 1000, 1000], [2500, 1000, 1000]);   // 900 mm
        IstVerbunden(p, "BO-1", "b", "RD-1", "a");
        IstVerbunden(p, "RD-1", "b", "RO-3", "anfang");
        Sauber(p);
        // Trockenlauf ändert nichts, Rückgängig stellt her
        var vorher = Inhalt(p);
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-2", ["zug"] = true, ["groesse"] = "DN 32" }, trockenlauf: true));
        Assert.Equal(vorher, Inhalt(p));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-2", ["zug"] = true, ["groesse"] = "DN 32" }));
        // ganzer Zug TS-1.c – RO-2 – BO-1 – RD-1 – RO-3 auf DN 32: die Reduktion im Zug entfällt dort und wird an der Grenze
        // TS-1.c (DN 25) wiederverwendet – als 32/25 mit der kleinen Seite b an c (stromauf klein → a zeigt stromab)
        Assert.Equal("DN 32", Familien.Typ(Hole(p, "RO-3")));
        Assert.Equal("DN 32/25", Familien.Typ(Hole(p, "RD-1")));
        Etwa([1500, 30, 1000], Stelle(p, "RD-1", "b").Lage);
        Etwa([1500, 70, 1000], Stelle(p, "RD-1", "a").Lage);             // 30 + 40
        IstVerbunden(p, "TS-1", "c", "RD-1", "b");
        Gerade(p, "RO-2", [1500, 70, 1000], [1500, 926, 1000]);          // Bogen DN 32: La = 2·37 = 74 → 1000 − 74
        Gerade(p, "RO-3", [1574, 1000, 1000], [2500, 1000, 1000]);
        Sauber(p);
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, Inhalt(p));
    }

    [Fact]
    public void Groesse_Zug_OffenerBogenAmEnde_BleibtOhneGerade()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "RO-2" }));   // BO-1.b ist jetzt offen
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["groesse"] = "DN 20" }));
        // Bogen DN 20: La = 50 → RO-1 bis 2000 − 50 = 1950; hinter dem offenen Bogen entsteht keine Gerade
        Gerade(p, "RO-1", [0, 0, 1000], [1950, 0, 1000]);
        Assert.Equal("DN 20", Familien.Typ(Hole(p, "BO-1")));
        Assert.Single(p.Modell.DerKlasse("Rohr"));
        Etwa([2000, 50, 1000], Stelle(p, "BO-1", "b").Lage);
        // spiegelbildlich: offener Bogen am Anfang
        p = MitSerie();
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "RO-1" }));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-2", ["zug"] = true, ["groesse"] = "DN 20" }));
        Gerade(p, "RO-2", [2000, 50, 1000], [2000, 1500, 1000]);
        Assert.Single(p.Modell.DerKlasse("Rohr"));
        IstVerbunden(p, "BO-1", "b", "RO-2", "anfang");
    }

    [Fact]
    public void Groesse_ZuKurz_AbgelehntUndUnveraendert()
    {
        var p = MitSerie();
        Ok(Zeichnen(p, "DN 15", [[0, 0, 0], [1000, 0, 0], [1000, 90, 0], [2000, 90, 0]]));   // DN 15: La = 40, 90 ≥ 80
        var vorher = Inhalt(p);
        // DN 50: La = 110 → 220 mm nötig zwischen den Bögen, vorhanden 90
        var e = Nein(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["groesse"] = "DN 50" }));
        Assert.Contains("mindestens 220 mm", e.Fehler);
        Assert.Equal(vorher, Inhalt(p));
    }

    // ------------------------------------------------------------------ leitung_serie

    /// <summary>Zweite FIKTIVE Rohrserie mit Bogen r = 3·da (Kopie der Testfamilien, andere id).</summary>
    static void ZweiteSerie(Projekt p)
    {
        var bogen = Familie("test_bogen");
        bogen["id"] = "fam-test-bogen-r3"; bogen["name"] = "Testbogen r = 3 da (fiktiv)";
        foreach (var x in bogen["parameter"]!.AsArray()) if (x!["name"]!.GetValue<string>() == "r") x["formel"] = "3 * da";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = bogen }));
        var serie = Familie("test_rohrserie");
        serie["id"] = "fam-test-rohrserie-r3"; serie["name"] = "Testrohrserie r3 (fiktiv)";
        serie["leitung"]!["formteile"]!["bogen"]!["familie"] = "fam-test-bogen-r3";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = serie }));
    }

    [Fact]
    public void Serie_ZugMitReduktion_FormteileErsetzt_ReduktionRueckt()
    {
        var p = MitSerie();
        ZweiteSerie(p);
        Ok(Zeichnen(p, "DN 25", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("RO-2"), ["groesse"] = "DN 20" }));
        // vorher: Bogen DN 25 La = Lb = 60, RD-1 25/20 von 60 bis 100, RO-2 ab 100
        Etwa([2000, 60, 1000], Stelle(p, "RD-1", "a").Lage);
        var e = Ok(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["serie"] = "fam-test-rohrserie-r3" }));
        // r3: DN 25 r = 3·30 = 90 → La = Lb = 90; die Reduktion rückt hinter den Bogen (90 bis 130)
        var s2 = Familien.Hole(p.Modell, "fam-test-rohrserie-r3");
        Assert.Equal(s2.Id, Hole(p, "RO-1").Bezuege["familie"]);
        Assert.Equal(s2.Id, Hole(p, "RO-2").Bezuege["familie"]);
        Assert.Equal("DN 25", Familien.Typ(Hole(p, "RO-1")));
        Assert.Equal("DN 20", Familien.Typ(Hole(p, "RO-2")));            // Grössen bleiben
        Assert.Equal(Familien.Hole(p.Modell, "fam-test-bogen-r3").Id, Hole(p, "BO-1").Bezuege["familie"]);
        Gerade(p, "RO-1", [0, 0, 1000], [1910, 0, 1000]);                // 2000 − 90
        Etwa([2000, 90, 1000], Stelle(p, "RD-1", "a").Lage);             // gleiche Reduktion (Kennzeichen bleibt)
        Gerade(p, "RO-2", [2000, 130, 1000], [2000, 1500, 1000]);        // 90 + 40 = 130 → 1370 mm
        Sauber(p);
        Assert.Contains(e.Hinweise, h => h.StartsWith("leitung_serie:"));
        // Serie anderer Kategorie: abgelehnt
        var k = MitKanalAuch(p);
        Assert.Contains("Kanal", Nein(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["serie"] = k })).Fehler);
    }

    [Fact]
    public void Serie_ReduktionDirektNachBogenMitListenwinkel_LiegtAufDerGeraden()
    {
        var p = MitSerie();
        ZweiteSerie(p);
        MitSystem(p, "SW", "Schmutzwasser");
        // senkrecht, dann 0.8 % fallend: Knick 90° − atan(0.008) = 89.54° → Bogen 90° (Liste), Abweichung 0.46°
        Ok(Zeichnen(p, "DN 50", [[0, 0, 3000], [0, 0, 1000], [3000, 0, 1000]], new JsonObject { ["gefaelle"] = 0.8 }, system: "SW"));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitungen"] = new JsonArray("RO-2"), ["groesse"] = "DN 40" }));
        Ok(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["serie"] = "fam-test-rohrserie-r3" }));
        // r3: Lb = 3·55 = 165 → die Reduktion rückt an den Bogen und liegt auf der Geraden, nicht auf der Knotenachse
        // (dort wäre sie um 165·sin 0.46° = 1.3 mm versetzt – mehr als verbinden erlaubt)
        Etwa(Stelle(p, "BO-1", "b").Lage!, Stelle(p, "RD-1", "a").Lage, 1e-3);
        Etwa([0, 0, 1165], Pkt(p, "RO-1")[1], 1e-6);   // senkrecht: 1000 + 165
        Sauber(p);
        Assert.Empty(Befunde(p, "P16"));
    }

    static string MitKanalAuch(Projekt p)
    {
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_kanalserie") }));
        return "fam-test-kanalserie";
    }

    // ------------------------------------------------------------------ Abfrage leitungszug

    [Fact]
    public void Leitungszug_AbfrageInFliessrichtungMitSummen()
    {
        var p = MitAbzweigZug();
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-2", ["zug"] = true, ["groesse"] = "DN 20" }));
        var z = Abfrage(p, "RO-3");
        Assert.Equal("TS-1", z["anfang"]!["element"]!.GetValue<string>());
        Assert.Equal("c", z["anfang"]!["anschluss"]!.GetValue<string>());
        Assert.True(z["endeOffen"]!.GetValue<bool>());
        var gl = z["glieder"]!.AsArray().Select(x => x!["kennzeichen"]!.GetValue<string>()).ToList();
        Assert.Equal(["RD-1", "RO-2", "BO-1", "RO-3"], gl);
        // Längen: RO-2 880 mm, RO-3 950 mm → 1830 mm = 1.83 m
        Assert.Equal(880, z["glieder"]![1]!["laenge"]!.GetValue<double>(), 6);
        Assert.Equal(1830, z["summe"]!["laengeMm"]!.GetValue<double>(), 6);
        Assert.Equal(1.83, z["summe"]!["laengeM"]!.GetValue<double>(), 6);
        Assert.Equal(90, z["glieder"]![2]!["winkel"]!.GetValue<double>(), 6);
        var dn20 = z["jeGroesse"]!.AsArray().Single(x => x!["groesse"]!.GetValue<string>() == "DN 20")!;
        Assert.Equal(2, dn20["anzahl"]!.GetValue<int>());
        // Hauptleitung: Zug endet beidseits (T-Stück bzw. offen)
        var h = Abfrage(p, "RO-1");
        Assert.True(h["anfangOffen"]!.GetValue<bool>());
        Assert.Equal("TS-1", h["ende"]!["element"]!.GetValue<string>());
        Assert.Single(h["glieder"]!.AsArray());
        Assert.Throws<HlksFehler>(() => Abfrage(p, "BO-1"));   // keine Leitung
    }

    [Fact]
    public void Leitungszug_Beispielprojekt_ZugDesAltbestands()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var z = Abfrage(p, "KW-V1-4");
        // KW-V1-4 (DN 25) → BO-KW-V1 → … ; Anfang am T-Stück TS-KW-V1-S
        Assert.Equal("TS-KW-V1-S", z["anfang"]!["element"]!.GetValue<string>());
        Assert.Contains(z["glieder"]!.AsArray(), x => x!["kennzeichen"]!.GetValue<string>() == "BO-KW-V1");
        Assert.True(z["summe"]!["laengeMm"]!.GetValue<double>() > 0);
    }
}
