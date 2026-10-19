using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2: harte Kollisionen mit Handrechnung – Rohr quer durch eine KICAD-Wand (Durchdringung), mit zugeordneter
/// Aussparung (keine), toleranz_hart, verbundene Leitungen, Formteile und Geräte (keine), Geschossfilter. Masse FIKTIV
/// (Testrohrserie da = DN + 5), Vorgaben FIKTIV.</summary>
public class KollisionspruefungHartTests
{
    [Fact]
    public void RohrQuerDurchKicadWand_DurchdringungHart_Handrechnung()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));   // quer durch W-1 (y −100 … 100)
        var e = Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        var ro = Hole(p, "RO-1"); var w1 = Hole(p, "W-1");
        Assert.Equal("KOL-1", k.Kennzeichen);
        Assert.Equal("hart", k.Text("art"));
        Assert.Equal("durchdringung", k.Text("typ"));
        Assert.Equal(w1.Id, k.Text("bauteil"));
        Assert.Equal("neu", k.Text("status"));
        // Eindringtiefe (Näherung B1): r + halbe Wanddicke = 25/2 + 200/2 = 112.5 mm
        Assert.Equal(112.5, k.Zahl("mass"));
        Assert.Equal("mm", k.Eigenschaft("mass")!.Einheit);
        Assert.Equal("berechnung", k.Eigenschaft("mass")!.Herkunft.Art);
        Assert.Contains("Eindringtiefe", k.Eigenschaft("mass")!.Herkunft.Quelle);
        // Lage = Mitte des Durchgangs auf der Achse in der Wandmitte
        Assert.Equal([3000, 0, 1000], Lage(k));
        var bet = new[] { ro.Id, w1.Id }.Order(StringComparer.Ordinal).ToList();
        Assert.Equal(bet, Beteiligte(k));
        Assert.Equal($"{bet[0]}|{bet[1]}@3000,0,1000", k.Text("kennung"));
        Assert.Equal(Hole(p, "EG").Id, k.Geschoss);                      // nach z (1000 in 0 … 3000)
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2} · Lauf 1 · hlks-architektur, hlks-hlks$", k.Text("pruefung"));
        Assert.Equal("RO-1 ↔ W-1", k.Name);
        Assert.Contains("jede Durchdringung", Hinweis(e, "toleranz_hart fehlt"));
        Assert.Contains("1 Kollisionen – hart 1 (durchdringung 1, kontakt 0), weich 0", Hinweis(e, "Prüflauf 1"));
        Assert.Contains("hart 1 (durchdringung 1", Hinweis(e, "  hlks-architektur:"));
        Assert.Contains("1 neu, 0 wieder gefunden", Hinweis(e, "Status:"));
        // ein Lauf = ein Paket
        Assert.Equal("kollision_pruefen", p.Verlauf[^1].Befehl);
    }

    [Fact]
    public void MitZugeordneterAussparung_KeineKollision_OhneDaemmungRundUndRechteckig()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        // rund d 60 um die Achse (FIKTIV): Ø 25 liegt ganz darin
        Ok(Aussparung(p, "AUS-1", "W-1", [3000, 0, 1000], [0, 1, 0], "rund", d: 60, leitungen: "RO-1"));
        var e = Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        Assert.Contains("1 in Aussparung", Hinweis(e, "Keine Kollision:"));
        // mit Dämmung 20: Ø 25 + 2·20 = 65 > 60 – der Querschnitt mit Dämmung ragt hinaus → Durchdringung
        Daemmung(p, "RO-1", 20);
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("durchdringung", k.Text("typ"));
        // grössere Aussparung (Ø 66 ≥ 65): wieder gedeckt; die Kollision ist behoben, nicht gelöscht
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AUS-1", ["eigenschaften"] = new JsonObject { ["d"] = 66 } }));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
    }

    [Fact]
    public void Aussparung_NurMitBezugAufDieseLeitung_NichtVeraltet_QuerschnittInnerhalb()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [5000, 3000, 2000], [5500, 3000, 2000]));   // weit weg, nur als Bezug
        // Aussparung an der richtigen Stelle, aber für eine andere Leitung → Durchdringung bleibt
        Ok(Aussparung(p, "AUS-1", "W-1", [3000, 0, 1000], [0, 1, 0], "rund", d: 60, leitungen: "RO-2"));
        Ok(Pruefen(p));
        Assert.Equal("durchdringung", Assert.Single(Kollisionen(p)).Text("typ"));
        // Status veraltet zählt nicht (Teil B3: AussparungStatus.Gueltig)
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "AUS-1" }));
        Ok(Aussparung(p, "AUS-2", "W-1", [3000, 0, 1000], [0, 1, 0], "rund", d: 60, status: "veraltet", leitungen: "RO-1"));
        Ok(Pruefen(p));
        Assert.Equal("neu", Assert.Single(Kollisionen(p)).Text("status"));
        // rechteckig b 100 (waagrecht) × h 40 (senkrecht) um die Achse: Ø 25 in der Mitte gedeckt …
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "AUS-2" }));
        Ok(Aussparung(p, "AUS-3", "W-1", [3000, 0, 1000], [0, 1, 0], "rechteckig", b: 100, h: 40, leitungen: "RO-1"));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        // … 10 mm höher: Oberkante Rohr 1010 + 12.5 = 1022.5 > 1000 + 20 → ragt hinaus → Durchdringung (wieder aufgetreten)
        Geometrie(p, "RO-1", [3000, -500, 1010], [3000, 500, 1010]);
        var e = Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("neu", k.Text("status"));
        Assert.Contains("KOL-1", Hinweis(e, "Wieder aufgetreten"));
    }

    [Fact]
    public void ToleranzHart_HalbmillimeterAuflage_NichtGemeldet_OhneToleranzGemeldet()
    {
        var p = Wandprojekt();
        // Rohr DN 20 (r 12.5) entlang der Innenseite von W-1 (y = 100): Achse bei y = 112 → 0.5 mm in der Wand
        Ok(Rohr(p, "RO-1", "DN 20", [1000, 112, 1000], [2000, 112, 1000]));
        var ohne = Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("kontakt", k.Text("typ"));                           // Achse nicht in der Wand: keine Querung
        Assert.Equal(0.5, k.Zahl("mass"));
        Assert.Null(k.Text("bauteil"));
        Assert.Contains("jede Durchdringung", Hinweis(ohne, "toleranz_hart fehlt"));
        // toleranz_hart 1 mm (FIKTIV): Auflage bis 1 mm ist Berührung → nicht mehr gemeldet, KOL-1 behoben
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 1)));
        var mit = Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        Assert.Contains("1 innerhalb toleranz_hart", Hinweis(mit, "Keine Kollision:"));
        Assert.Contains("toleranz_hart 1 mm (Quelle: FIKTIV", Hinweis(mit, "toleranz_hart 1"));
        // toleranz_hart 0.4 mm: 0.5 > 0.4 → wieder hart
        Ok(Vorgaben(p, (Projektvorgaben.ToleranzHart, 0.4)));
        Ok(Pruefen(p));
        Assert.Equal("neu", Assert.Single(Kollisionen(p)).Text("status"));
    }

    [Fact]
    public void Verbunden_LeitungFormteilLeitung_KeineKollision_GetrenntSchon()
    {
        var p = MitSerie();
        // L-Zug DN 20: RO-1 (0 → 1950), Bogen BO-1, RO-2 (50 → 1500) – alle verbunden
        Ok(RoutingHilfe.Zeichnen(p, "DN 20", [[0, 0, 1000], [2000, 0, 1000], [2000, 1500, 1000]]));
        // FIKTIV 50 mm: ohne die Formteil-Regel wären RO-1 und RO-2 weich (Enden 70.7 mm auseinander, 70.7 − 25 = 45.7 < 50)
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50), (Projektvorgaben.AbstandWeich, new JsonObject { ["hlks-hlks"] = 50 })));
        var e = Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        Assert.Contains("3 verbunden", Hinweis(e, "Keine Kollision:"));     // RO-1–BO-1, BO-1–RO-2 direkt, RO-1–RO-2 über den Bogen
        // RO-1 vom Bogen trennen: Rohrende im Bogenkörper (hart) und RO-1–RO-2 weich: 50 − (√(50² + 50²) − 25) = 4.29 → 4.3 mm
        Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "RO-1.ende" }));
        Ok(Pruefen(p));
        var ks = Kollisionen(p);
        var hart = Assert.Single(ks, x => x.Text("art") == "hart");
        Assert.Equal("RO-1 ↔ BO-1", hart.Name);
        var weich = Assert.Single(ks, x => x.Text("art") == "weich");
        Assert.Equal("RO-1 ↔ RO-2", weich.Name);
        Assert.Equal(4.3, weich.Zahl("mass"));
        Assert.Equal(2, ks.Count);
    }

    [Fact]
    public void Verbunden_LeitungAmGeraet_KeineKollision_OhneVerbindungHart()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([3000, 3000, 0]) },
        }));
        // Rohr endet im Körper des WC (x 2800 … 3200, y 2650 … 3350, z 0 … 400)
        Ok(Rohr(p, "RO-1", "DN 20", [3000, 2000, 200], [3000, 3000, 200]));
        Ok(Pruefen(p));
        var k = Assert.Single(Kollisionen(p));
        Assert.Equal("hart", k.Text("art"));
        Assert.Equal("kontakt", k.Text("typ"));
        Assert.Equal("RO-1 ↔ WC-1", k.Name);
        // verbunden (logisch, das WC hat keine Anschlusslagen) → keine Kollision mehr
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "WC-1.kw" }));
        var e = Ok(Pruefen(p));
        Assert.Equal("behoben", Assert.Single(Kollisionen(p)).Text("status"));
        Assert.Contains("1 verbunden", Hinweis(e, "Keine Kollision:"));
    }

    [Fact]
    public void Geschossfilter_NurDiesesGeschoss_AndereBleibenUnveraendert()
    {
        var p = Wandprojekt();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));    // EG: durch W-1
        // OG: zwei sich kreuzende Rohre (hart, HLKS ↔ HLKS)
        foreach (var (kz, a, b) in new[] { ("RO-2", new double[] { 1000, 2000, 4000 }, new double[] { 2000, 2000, 4000 }), ("RO-3", new double[] { 1500, 1500, 4000 }, new double[] { 1500, 2500, 4000 }) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "OG", ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" },
                ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" }, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(a, b) },
            }));
        Ok(Pruefen(p, new JsonObject { ["geschoss"] = "EG" }));
        Assert.Equal("RO-1 ↔ W-1", Assert.Single(Kollisionen(p)).Name);
        Ok(Pruefen(p));
        var og = Assert.Single(Kollisionen(p), x => x.Name == "RO-2 ↔ RO-3");
        Assert.Equal(Hole(p, "OG").Id, og.Geschoss);
        Assert.Equal(25, og.Zahl("mass"));                                  // Kapsel–Kapsel exakt: r + r − 0 = 25
        // OG-Kreuzung beheben; Lauf nur im EG lässt die OG-Kollision unverändert, der volle Lauf setzt sie behoben
        Geometrie(p, "RO-3", [1500, 2500, 4000], [1500, 3000, 4000]);
        var eg = Ok(Pruefen(p, new JsonObject { ["geschoss"] = "EG" }));
        Assert.Equal("neu", Hole(p, og.Kennzeichen).Text("status"));
        Assert.Contains("1 ausserhalb dieses Laufs", Hinweis(eg, "Status:"));
        Ok(Pruefen(p));
        Assert.Equal("behoben", Hole(p, og.Kennzeichen).Text("status"));
    }

    [Fact]
    public void Paare_Und_Unterlagen_Pruefen_Abgelehnt()
    {
        var p = Wandprojekt();
        Nein(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-mond") }));
        Nein(Pruefen(p, new JsonObject { ["paare"] = new JsonArray() }));
        Nein(Pruefen(p, new JsonObject { ["unterlagen"] = Par("W-1") }));   // keine IFC-Unterlage
        Nein(Pruefen(p, new JsonObject { ["geschoss"] = "W-1" }));
        // architektur-hlks = hlks-architektur; Text mit Komma geht auch
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        Ok(Pruefen(p, new JsonObject { ["paare"] = "architektur-hlks" }));
        Assert.EndsWith("Lauf 1 · hlks-architektur", Assert.Single(Kollisionen(p)).Text("pruefung"));
        // nur hlks-hlks: die Wandkollision liegt ausserhalb des Laufs und bleibt
        Ok(Pruefen(p, new JsonObject { ["paare"] = Par("hlks-hlks") }));
        Assert.Equal("neu", Assert.Single(Kollisionen(p)).Text("status"));
    }
}
