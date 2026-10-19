using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B4: Leitungen mit Serie massstäblich (Grundriss: Band mit Aussendurchmesser, 3D: Zylinder aussen/2, Kanalmass),
/// Dämmung als zweite Kontur bzw. zweiter heller Körper, Grundriss ausgezogen und nur Verdecktes gestrichelt (Christian 19.10.2026),
/// Steigleitungen mit Füllung «schnitt» an der Schnitthöhe, Beschriftung mit dem Typnamen der Serie. Werte FIKTIV (Testrohrserie: da = DN + 5; Kanalserien als FIKTIVE Mini-
/// Familien im Test). Ausgaben ohne Serie bleiben unverändert.</summary>
public class Darstellung3SerieTests
{
    static Grafikteil Teil(Projekt p, string kz, string ansicht = "AN-EG") =>
        Grafik.Plan(p, ansicht).Teile.Single(t => t.Id == Hole(p, kz).Id);

    static void Aendern(Projekt p, string kz, JsonObject eig) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }));

    static double Abstand(double[] a, double[] b) => Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));

    static IEnumerable<Primitiv> Strichlinien(Grafikteil t) => t.Primitive.Where(x => x.Art == "polylinie" && x.Stil.Linie == "strich");

    // ------------------------------------------------------------------ Grundriss massstäblich

    [Fact]
    public void Grundriss_RohrMitSerie_BandMitAussendurchmesser()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        var t = Teil(p, "RO-1");
        var band = Assert.Single(t.Primitive, x => x.Art == "polygon");
        // Handrechnung: DN 20 → da = 20 + 5 = 25 mm (FIKTIV)
        Assert.Equal(25, Abstand(band.Punkte[0], band.Punkte[3]), 6);
        Assert.Equal(3000, Abstand(band.Punkte[0], band.Punkte[1]), 6);
        Assert.Equal(Kat.Medien["KW"].FarbeHex, band.Stil.Fuellung);
        Assert.Null(band.Stil.Stift);
        Assert.DoesNotContain(t.Primitive, x => x.Stil.Linie != "voll");
        Assert.Equal(2, t.Fangpunkte!.Count);
        Etwa([3000, 0], t.Fangpunkte[1]);
        // andere Grösse: DN 50 → 55 mm
        Ok(Rohr(p, "RO-2", "DN 50", [0, 1000, 500], [3000, 1000, 500]));
        var b2 = Assert.Single(Teil(p, "RO-2").Primitive, x => x.Art == "polygon");
        Assert.Equal(55, Abstand(b2.Punkte[0], b2.Punkte[3]), 6);
    }

    [Fact]
    public void Grundriss_RohrOhneSerie_WieBisher()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-1", 20, [0, 0, 500], [3000, 0, 500]));
        var l = Assert.Single(Teil(p, "RO-1").Primitive);   // DN 20: 0.6 · 20 = 12 < 0.35 mm Papier (17.5) → Linie
        Assert.Equal("polylinie", l.Art);
        Assert.Equal(Stifte.Mittel, l.Stil.Stift);
        Assert.Equal("voll", l.Stil.Linie);
        // ohne Serie wirkt die Schnitthöhe nicht (Altbestand bleibt voll)
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 500, 2500], [3000, 500, 2500]));
        Assert.Equal("voll", Assert.Single(Teil(p, "RO-2").Primitive).Stil.Linie);
    }

    [Fact]
    public void Grundriss_Grob_BleibtEinfacheLinie_OhneDaemmung()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        var l = Assert.Single(Teil(p, "RO-1").Primitive);
        Assert.Equal("polylinie", l.Art);
        Assert.Equal("voll", l.Stil.Linie);
        Assert.Equal(Stifte.Mittel, l.Stil.Stift);
    }

    [Fact]
    public void Grundriss_Daemmung_ZweiteKonturDuennGestrichelt()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var t = Teil(p, "RO-1");
        // Handrechnung: 25 + 2 · 20 = 65 mm äussere Kontur → Ränder bei y = ±32.5
        var kontur = Strichlinien(t).ToList();
        Assert.Equal(2, kontur.Count);
        Assert.All(kontur, k => Assert.Equal(Stifte.Duenn, k.Stil.Stift));
        Assert.Equal(65, Math.Abs(kontur[0].Punkte[0][1] - kontur[1].Punkte[0][1]), 6);
        Assert.Equal([32.5, -32.5], kontur.Select(k => k.Punkte[0][1]).ToArray());
        Assert.Equal([0.0, 3000.0], kontur[0].Punkte.Select(q => q[0]).ToArray());
        // das Rohr selbst bleibt 25 mm breit und voll
        Assert.Equal(25, Abstand(t.Primitive.First(x => x.Art == "polygon").Punkte[0], t.Primitive.First(x => x.Art == "polygon").Punkte[3]), 6);
        // ohne Dämmdicke keine zweite Kontur
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 0 });
        Assert.Empty(Strichlinien(Teil(p, "RO-1")));
    }

    [Fact]
    public void Grundriss_Daemmung_AnKnickenMitGehrung()
    {
        // Polylinie (Altbestand mit Serie): zweite Kontur folgt dem Knick; Abstand der Ränder bleibt 65 mm
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500], [1000, 1000, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var kontur = Strichlinien(Teil(p, "RO-1")).ToList();
        Assert.Equal(2, kontur.Count);
        Assert.All(kontur, k => Assert.Equal(3, k.Punkte.Count));
        Etwa([1032.5, -32.5], kontur[1].Punkte[1], 1e-9);   // rechts der Laufrichtung: Gehrungspunkt
        Etwa([967.5, 32.5], kontur[0].Punkte[1], 1e-9);
    }

    [Fact]
    public void Versatz_ParallelkurveLinksPositiv()
    {
        var v = HlksLinien.Versatz([[0, 0], [100, 0], [100, 100]], 10);
        Etwa([0, 10], v[0], 1e-9);
        Etwa([90, 10], v[1], 1e-9);    // Gehrung innen
        Etwa([90, 100], v[2], 1e-9);
        var r = HlksLinien.Versatz([[0, 0], [100, 0], [100, 100]], -10);
        Etwa([110, -10], r[1], 1e-9);
        Assert.Empty(HlksLinien.Versatz([[5, 5]], 3));
    }

    // ------------------------------------------------------------------ Schnitthöhe

    // Entscheid Christian 19.10.2026 (Abnahme Modul 3, Prüfliste 10): über der Schnitthöhe ausgezogen, gestrichelt nur Verdecktes
    // (Auslegung Hauptlauf: ganz unterhalb der Kote des dargestellten Geschosses, also in bzw. unter Boden/Decke/Bodenplatte).

    [Fact]
    public void UeberDerSchnitthoehe_Ausgezogen_UnterDemBoden_Gestrichelt()
    {
        var p = MitSerie(false);   // Geschoss EG Kote 0, Schnitthöhe 1000
        Ok(Rohr(p, "RO-OBEN", "DN 20", [0, 0, 2500], [3000, 0, 2500]));
        Ok(Rohr(p, "RO-UNTEN", "DN 20", [0, 1000, 500], [3000, 1000, 500]));
        Ok(Rohr(p, "RO-BODEN", "DN 20", [0, 2000, -300], [3000, 2000, -300]));
        // B10: unter dem untersten Geschoss ohne Bodenplatte = Erdreich (Grundleitung) → nur im Grundleitungsplan; von Hand frei, damit
        // hier die verdeckte Darstellung im gewöhnlichen Grundriss geprüft wird
        Aendern(p, "RO-BODEN", new JsonObject { ["verlegung"] = "frei" });
        var oben = Teil(p, "RO-OBEN");
        Assert.Single(oben.Primitive, x => x.Art == "polygon");                 // Band wie unterhalb
        Assert.Empty(Strichlinien(oben));
        Assert.Single(Teil(p, "RO-UNTEN").Primitive, x => x.Art == "polygon");
        var boden = Teil(p, "RO-BODEN");
        Assert.DoesNotContain(boden.Primitive, x => x.Art == "polygon");         // verdeckt: ohne Füllung
        var raender = Strichlinien(boden).ToList();
        Assert.Equal(2, raender.Count);
        Assert.Equal(25, Math.Abs(raender[0].Punkte[0][1] - raender[1].Punkte[0][1]), 6);   // weiter Aussendurchmesser breit
    }

    [Fact]
    public void Verdeckt_RichtetSichNachDerGeschosskote_AuchOhneSerie()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 0], [3000, 0, 0]));                 // genau auf dem Boden = nicht verdeckt
        Assert.Empty(Strichlinien(Teil(p, "RO-1")));
        // ohne Serie (Altbestand): verdeckt ebenfalls gestrichelt, darüber voll
        Ok(RohrOhneSerie(p, "RO-A", 20, [0, 500, -200], [3000, 500, -200]));
        Aendern(p, "RO-A", new JsonObject { ["verlegung"] = "frei" });   // B10: sonst Erdreich (siehe oben) und nur im Grundleitungsplan
        Assert.Equal("strich", Assert.Single(Teil(p, "RO-A").Primitive).Stil.Linie);
        // Geschoss mit Kote 3000: ein Rohr des 1.OG auf z 2800 liegt unter dessen Boden, auf 3500 und 4500 nicht
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "1.OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-1OG", ["geschoss"] = "1.OG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        BefehlsErgebnis Rohr1OG(string kz, double z) => p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "1.OG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 0, z], [3000, 0, z]) },
        });
        Ok(Rohr1OG("RO-2", 2800));
        Ok(Rohr1OG("RO-3", 3500));
        Ok(Rohr1OG("RO-4", 4500));
        Assert.Equal(2, Strichlinien(Teil(p, "RO-2", "AN-1OG")).Count());       // 2800 < 3000
        Assert.Empty(Strichlinien(Teil(p, "RO-3", "AN-1OG")));
        Assert.Empty(Strichlinien(Teil(p, "RO-4", "AN-1OG")));                 // über der Schnitthöhe 4000: ausgezogen
    }

    [Fact]
    public void Verdeckt_PolylinieJeTeilstueckNachSeinemZ()
    {
        var p = MitSerie(false);
        // 0–1000 auf z −300 (verdeckt), 1000–2000 steigend von −300 nach 500 (nicht ganz unter dem Boden = voll), 2000–3000 auf z 500
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, -300], [1000, 0, -300], [2000, 0, 500], [3000, 0, 500]));
        var t = Teil(p, "RO-1");
        var raender = Strichlinien(t).ToList();
        Assert.Equal(2, raender.Count);
        Assert.All(raender, r => Assert.Equal([0.0, 1000.0], r.Punkte.Select(q => q[0]).ToArray()));
        var voll = t.Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, voll.Count);   // zwei Segmente als Band
        Assert.Equal(1000, voll.SelectMany(v => v.Punkte).Min(q => q[0]), 6);
        Assert.Equal(4, t.Fangpunkte!.Count);   // Fangpunkte unverändert: Enden und Knicke
    }

    [Fact]
    public void Steigleitung_FuellungNachSchnitthoehe_KonturAusgezogen_VerdecktGestrichelt()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-S", "DN 20", [1000, 1000, 0], [1000, 1000, 3000]));    // durch die Schnittebene (1000)
        Ok(Rohr(p, "RO-U", "DN 20", [2000, 1000, 0], [2000, 1000, 800]));      // ganz darunter
        Ok(Rohr(p, "RO-O", "DN 20", [3000, 1000, 1500], [3000, 1000, 3000]));  // ganz darüber
        Ok(Rohr(p, "RO-V", "DN 20", [4000, 1000, -2000], [4000, 1000, -500])); // ganz unter dem Boden
        Aendern(p, "RO-V", new JsonObject { ["verlegung"] = "frei" });   // B10: sonst Erdreich und nur im Grundleitungsplan
        var s = Assert.Single(Teil(p, "RO-S").Primitive);
        Assert.Equal("kreis", s.Art);
        Assert.Equal(12.5, s.Radius!.Value, 9);                                // Aussendurchmesser/2
        Assert.Equal("schnitt", s.Stil.Fuellung);
        Assert.Equal([1000.0, 1000.0], s.Punkte[0]);
        var u = Assert.Single(Teil(p, "RO-U").Primitive);
        Assert.Equal("papier", u.Stil.Fuellung);
        Assert.Equal("voll", u.Stil.Linie);
        var o = Assert.Single(Teil(p, "RO-O").Primitive);
        Assert.Equal("voll", o.Stil.Linie);
        Assert.Null(o.Stil.Fuellung);
        var v = Assert.Single(Teil(p, "RO-V").Primitive);
        Assert.Equal("strich", v.Stil.Linie);
        Etwa([1000, 1000], Assert.Single(Teil(p, "RO-S").Fangpunkte!));
    }

    [Fact]
    public void Steigleitung_MitDaemmungUndInPolylinie()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-S", "DN 20", [1000, 1000, 0], [1000, 1000, 3000]));
        Aendern(p, "RO-S", new JsonObject { ["daemmdicke"] = 20 });
        var t = Teil(p, "RO-S");
        Assert.Equal(2, t.Primitive.Count);
        Assert.Equal(32.5, t.Primitive[1].Radius!.Value, 9);                  // 25 / 2 + 20
        Assert.Equal("strich", t.Primitive[1].Stil.Linie);                     // Dämmkontur bleibt dünn gestrichelt
        // Polylinie mit senkrechtem Teilstück durch die Schnittebene: dort erscheint der Kreis zusätzlich
        Ok(Rohr(p, "RO-P", "DN 20", [0, 0, 500], [2000, 0, 500], [2000, 0, 2500], [4000, 0, 2500]));
        var pl = Teil(p, "RO-P");
        var ring = Assert.Single(pl.Primitive, x => x.Art == "kreis" && x.Stil.Fuellung == "schnitt");   // Ring der Steigleitung (das Band läuft durch, rund verbunden)
        Assert.Equal([2000.0, 0.0], ring.Punkte[0]);
        Assert.Equal("schnitt", ring.Stil.Fuellung);
        Assert.Empty(Strichlinien(pl));                                        // das obere Stück ist ausgezogen
        Assert.Contains(pl.Primitive, x => x.Art == "polygon");
    }
    // ------------------------------------------------------------------ Kanäle

    static JsonObject Kanalserie(bool rund) => JsonNode.Parse(rund
        ? """
          { "format": 1, "id": "fam-test-kanal-rund", "name": "Kanal rund (fiktiv)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "d", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "125", "werte": { "d": 125 } }, { "name": "160", "werte": { "d": 160 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rund", "groesse": { "d": "d" }, "aussen": "d + 1", "regeln": { "winkel": "frei" } } }
          """
        : """
          { "format": 1, "id": "fam-test-kanal-eckig", "name": "Kanal eckig (fiktiv)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "b", "art": "typ", "datentyp": "laenge" }, { "name": "h", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "400x200", "werte": { "b": 400, "h": 200 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rechteck", "groesse": { "b": "b", "h": "h" }, "aussen": 0.001 } }
          """)!.AsObject();

    static Projekt MitKanaelen()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(true) }));    // FAM-2
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(false) }));   // FAM-3
        Kanal(p, "KA-1", "FAM-2", "160", [0, 0, 500], [2000, 0, 500]);
        Kanal(p, "KA-2", "FAM-3", "400x200", [0, 1000, 500], [2000, 1000, 500]);
        return p;
    }

    static void Kanal(Projekt p, string kz, string serie, string typ, params double[][] punkte) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = serie },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        }));

    [Fact]
    public void Kanal_MitSerie_GrundrissUndKoerperMitKanalmass()
    {
        var p = MitKanaelen();
        var rund = Assert.Single(Teil(p, "KA-1").Primitive, x => x.Art == "polygon");
        Assert.Equal(160, Abstand(rund.Punkte[0], rund.Punkte[3]), 6);     // Ø d (nominal), nicht d + 1
        Assert.Equal(0.2, rund.Stil.Deckkraft);
        var eckig = Assert.Single(Teil(p, "KA-2").Primitive, x => x.Art == "polygon");
        Assert.Equal(400, Abstand(eckig.Punkte[0], eckig.Punkte[3]), 6);   // b waagrecht
        // 3D: Rechteckkanal b quer, h senkrecht (ANNAHME senkrechte Kanäle: b in x, h in y)
        var q = Assert.Single(Grafik.Koerper(p, [Hole(p, "KA-2").Id]).Single().Teile);
        Assert.Equal([2000.0, 400.0, 200.0], q.Masse!);
        var z = Assert.Single(Grafik.Koerper(p, [Hole(p, "KA-1").Id]).Single().Teile);
        Assert.Equal("zylinder", z.Art);
        Assert.Equal(80, z.Radius!.Value, 9);
        // Kanal als Steigleitung: Rechteck b × h mit Füllung «schnitt», Rundkanal als Kreis
        Kanal(p, "KA-3", "FAM-3", "400x200", [5000, 1000, 0], [5000, 1000, 3000]);
        Kanal(p, "KA-4", "FAM-2", "125", [6000, 1000, 0], [6000, 1000, 3000]);
        var r = Assert.Single(Teil(p, "KA-3").Primitive);
        Assert.Equal("polygon", r.Art);
        Assert.Equal("schnitt", r.Stil.Fuellung);
        Etwa([400, 200], [r.Punkte.Max(a => a[0]) - r.Punkte.Min(a => a[0]), r.Punkte.Max(a => a[1]) - r.Punkte.Min(a => a[1])]);
        Assert.Equal(62.5, Assert.Single(Teil(p, "KA-4").Primitive).Radius!.Value, 9);
    }

    [Fact]
    public void Kanal_Daemmung_ZweiteKonturUndHellerKoerper()
    {
        var p = MitKanaelen();
        Aendern(p, "KA-2", new JsonObject { ["daemmdicke"] = 30 });
        var kontur = Strichlinien(Teil(p, "KA-2")).ToList();
        Assert.Equal(2, kontur.Count);
        Assert.Equal(460, Math.Abs(kontur[0].Punkte[0][1] - kontur[1].Punkte[0][1]), 6);   // 400 + 2 · 30
        var teile = Grafik.Koerper(p, [Hole(p, "KA-2").Id]).Single().Teile;
        Assert.Equal(2, teile.Count);
        Assert.Equal([2000.0, 400.0, 200.0], teile[0].Masse!);
        Assert.Equal([2000.0, 460.0, 260.0], teile[1].Masse!);
        Assert.True(teile[1].NichtSchneiden);
    }

    // ------------------------------------------------------------------ 3D

    [Fact]
    public void Koerper_RohrMitSerie_ZylinderMitRadiusAussenDurchZwei()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        var k = Grafik.Koerper(p, [Hole(p, "RO-1").Id]).Single();
        var z = Assert.Single(k.Teile);
        Assert.Equal("zylinder", z.Art);
        Assert.Equal(12.5, z.Radius!.Value, 9);                              // da = 25 (FIKTIV) → 12.5 mm
        Assert.Equal("rohr", z.Material);
        Assert.Equal([0.0, 0.0, 500.0], z.Von!);
        Assert.Equal([3000.0, 0.0, 500.0], z.Nach!);
        // ohne Serie: DN-gerecht mit Mindestdurchmesser 30 mm wie bisher
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 500, 500], [3000, 500, 500]));
        Assert.Equal(15, Assert.Single(Grafik.Koerper(p, [Hole(p, "RO-2").Id]).Single().Teile).Radius!.Value, 9);
    }

    [Fact]
    public void Koerper_Daemmung_ZweiterHellerKoerperNurImDetailgradFein()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var k = Grafik.Koerper(p, [Hole(p, "RO-1").Id]).Single();           // 3D ohne Ansicht = fein
        Assert.Equal(2, k.Teile.Count);
        Assert.Equal(12.5, k.Teile[0].Radius!.Value, 9);
        var d = k.Teile[1];
        Assert.Equal(32.5, d.Radius!.Value, 9);                              // 12.5 + 20
        Assert.Equal(0.35, d.Deckkraft);
        Assert.True(d.NichtSchneiden);                                       // nicht im Schnitt
        Assert.NotNull(d.Farbe);
        Assert.NotEqual(k.Teile[0].Farbe, d.Farbe);
        // mittel: nur das Rohr
        var e = Hole(p, "RO-1");
        Assert.Single(new HlksGrafik().Koerper(p, e, "mittel")!.Teile);
        Assert.Equal(2, new HlksGrafik().Koerper(p, e, "fein")!.Teile.Count);
        // heller als die Medienfarbe: jeder Kanal näher an Weiss
        static int Summe(string f) => Convert.ToInt32(f[1..3], 16) + Convert.ToInt32(f[3..5], 16) + Convert.ToInt32(f[5..7], 16);
        Assert.True(Summe(d.Farbe!) > Summe(k.Teile[0].Farbe!));
    }

    [Fact]
    public void Koerper_DaemmungAnKnickenSchliesstMitKugeln()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500], [1000, 1000, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var t = Grafik.Koerper(p, [Hole(p, "RO-1").Id]).Single().Teile;
        Assert.Equal(2, t.Count(x => x.Art == "kugel"));
        Assert.Contains(t, x => x.Art == "kugel" && Math.Abs(x.Radius!.Value - 12.5) < 1e-9);
        Assert.Contains(t, x => x.Art == "kugel" && Math.Abs(x.Radius!.Value - 32.5) < 1e-9);
    }

    [Fact]
    public void Schnitt_DaemmungWirdNichtGeschnitten()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [1000, -1000, 500], [1000, 1000, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-SCHNITT", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, 0], [2000, 0]) },
        }));
        var g = Grafik.Plan(p, "AN-SCHNITT");   // darf mit Serie und Dämmung nicht scheitern
        Assert.NotNull(g);
    }

    // ------------------------------------------------------------------ Beschriftung

    [Fact]
    public void Beschriftung_RohreMitUndOhneSerie_NwOderDnNachEinstellung()
    {
        var p = MitSerie(false);
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "fein" });
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 1000, 500], [3000, 1000, 500]));
        // Entscheid Christian 19.10.2026 (B9b): alle Rohre ausser Abwasser mit NW bzw. DN – auch Rohre mit Serie (vorher der Typname)
        Assert.Equal("NW 20", Teil(p, "RO-1").Primitive.Single(x => x.Art == "text").Text);   // Vorgabe NW
        Assert.Equal("NW 20", Teil(p, "RO-2").Primitive.Single(x => x.Art == "text").Text);
        // die Einstellung DN gilt für beide
        Aendern(p, "AN-EG", new JsonObject { ["rohrbezeichnung"] = "DN" });
        Assert.Equal("DN 20", Teil(p, "RO-2").Primitive.Single(x => x.Art == "text").Text);
        Assert.Equal("DN 20", Leitungsbezeichnung.Text(p, Hole(p, "AN-EG"), Hole(p, "RO-1")));
        // Kanäle behalten «b×h» bzw. «Ø d»
        var k = MitKanaelen();
        Aendern(k, "AN-EG", new JsonObject { ["detailgrad"] = "fein" });
        Assert.Equal("400×200", Leitungsbezeichnung.Text(k, Hole(k, "AN-EG"), Hole(k, "KA-2")));
        Assert.Equal("Ø 160", Leitungsbezeichnung.Text(k, Hole(k, "AN-EG"), Hole(k, "KA-1")));
    }

    // ------------------------------------------------------------------ Zusammenspiel

    [Fact]
    public void Plan_MitSerieUndFormteilen_ZeichnetAlles()
    {
        var p = MitSerie();   // mit Bogen, T-Stück, Reduktion
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [950, 0, 500]));
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-1", [1000, 0, 500], 90));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 50, 500], [1000, 2000, 500]));
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Contains(g.Teile, t => t.Id == Hole(p, "RO-1").Id);
        Assert.Contains(g.Teile, t => t.Id == Hole(p, "BO-1").Id);
        Assert.NotEmpty(Grafik.Koerper(p));
    }
}
