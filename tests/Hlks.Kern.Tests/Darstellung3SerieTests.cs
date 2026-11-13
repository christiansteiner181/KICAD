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
        Grafik.Plan(p, ansicht).Teile.Single(t => t.Id == Hole(p, kz).Id && t.Teil != DaemmGrafik.Kennung);

    /// <summary>Vereinigte Dämmfläche des Grundrisses (DaemmGrafik, 09.11.2026) oder null.</summary>
    static Grafikteil? Daemmung(Projekt p, string ansicht = "AN-EG") => DaemmTeil(Grafik.Plan(p, ansicht));
    static (double Breite, double Hoehe) Ausdehnung(Primitiv x) => (x.Punkte.Max(q => q[0]) - x.Punkte.Min(q => q[0]), x.Punkte.Max(q => q[1]) - x.Punkte.Min(q => q[1]));

    static void Aendern(Projekt p, string kz, JsonObject eig) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }));

    static double Abstand(double[] a, double[] b) => Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));

    static Primitiv Flaeche(Grafikteil t) => Assert.Single(t.Primitive, x => x.Art == "polygon");   // Dämmfläche (DaemmGrafik), Ränder als Linien
    static IEnumerable<Primitiv> Strichlinien(Grafikteil t) => t.Primitive.Where(x => x.Art == "polylinie" && x.Stil.Linie is "strich" or DaemmGrafik.Verdeckt);

    // ------------------------------------------------------------------ Grundriss massstäblich

    [Fact]
    public void Grundriss_Einlinig_SteigleitungMitKreisUndRichtung()   // Christian 05.11.2026: «darstellung mit kreis für n.o und n.u.»
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-A", 20, [0, 0, 500], [1000, 0, 500], [1000, 0, 4000]));    // waagrecht, dann aus dem Geschoss nach oben
        Ok(RohrOhneSerie(p, "RO-B", 20, [0, 500, -800], [0, 500, 500], [1000, 500, 500]));  // von unten herauf, dann waagrecht
        var a = Teil(p, "RO-A"); var b = Teil(p, "RO-B");
        Assert.Contains(a.Primitive, x => x.Art == "kreis" && Abstand(x.Punkte[0], [1000, 0]) < 1e-6);
        Assert.DoesNotContain(a.Primitive, x => x.Art == "text" && x.Text is "n.o." or "n.u.");   // keine Beschriftung (Christian 05.11.2026)
        Assert.Contains(b.Primitive, x => x.Art is "kreis" or "bogen" && Abstand(x.Punkte[0], [0, 500]) < 1e-6);
        Assert.DoesNotContain(b.Primitive, x => x.Art == "text" && x.Text is "n.o." or "n.u.");
        // Ausschnitt des Kreises auf der höheren Leitung: RO-A geht nach oben weiter → geschlossen; RO-B kommt von unten, oben schliesst das waagrechte Stück nach +x an
        Assert.DoesNotContain(a.Primitive, x => x.Art == "bogen");
        // Skizze Christian 05.11.2026: n.o. ohne höhere Leitung mit Kreuz; die tiefere (waagrechte) Leitung endet am Kreisrand, die höhere läuft in die Mitte
        var ring = Assert.Single(a.Primitive, x => x.Art == "kreis" && Abstand(x.Punkte[0], [1000, 0]) < 1e-6);
        // n.o.: Kreuz durch die Mitte (Christian 05.11.2026: «bei nach oben ein kreuz nicht ein punkt»)
        Assert.Equal(2, a.Primitive.Count(x => x.Art == "linie" && Abstand([(x.Punkte[0][0] + x.Punkte[1][0]) / 2, (x.Punkte[0][1] + x.Punkte[1][1]) / 2], [1000, 0]) < 1e-6));
        Assert.Equal(1000 - ring.Radius!.Value, a.Primitive.Where(x => x.Art == "polylinie").Max(x => x.Punkte.Max(q => q[0])), 6);
        Assert.Equal(1000, b.Primitive.Where(x => x.Art == "polylinie").Max(x => x.Punkte.Max(q => q[0])), 6);
        var bo = Assert.Single(b.Primitive, x => x.Art == "bogen");
        Assert.Equal(30, bo.Von!.Value, 6); Assert.Equal(330, bo.Bis!.Value, 6);
    }

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
        Assert.DoesNotContain(t.Primitive, x => x.Stil.Linie is not ("voll" or "achse"));
        // Achslinie (Christian 05.11.2026): anthrazit (09.11.2026); endet am Rohr (06.11.2026 «die achse der rohre darf nicht länger sein, als das rohr selbst»)
        var achse = Assert.Single(t.Primitive, x => x.Stil.Linie == "achse");
        Assert.Equal("#383e42", achse.Stil.Farbe);
        Assert.Equal(0, achse.Punkte[0][0], 6); Assert.Equal(3000, achse.Punkte[^1][0], 6);
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
    public void Grundriss_Grob_EinfacheLinie_MitGelbemDaemmpunkt()
    {
        // Christian 09.11.2026: «bei grob, gibt es nur einen gelben punkt auf der leitungslinie»
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        var pr = Teil(p, "RO-1").Primitive;
        var punkt = Assert.Single(pr, x => x.Art == "kreis");
        Assert.Equal(HlksGrafik.DaemmGelb, punkt.Stil.Fuellung);
        Etwa([1500, 0], punkt.Punkte[0], 1e-9);   // Mitte der Leitung
        var l = Assert.Single(pr, x => x.Art == "polylinie");
        Assert.Equal("voll", l.Stil.Linie);
        Assert.Equal(Stifte.Mittel, l.Stil.Stift);
    }

    [Fact]
    public void Grundriss_Daemmung_BandInRohrfarbeHinterDemRohr()
    {
        // Christian 09.11.2026: «darstellung dämmung … schwarze linien, gelb transparent hinterlegt», «auch auf mittel anwenden»; seit 12.11.2026 in der
        // Rohrfarbe transparent («wird die Dämmung gezeigt, so soll sie die Rohrfarbe mit transparenz erhalten»)
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        // Handrechnung: 25 + 2 · 20 = 65 mm Dämmbreite; eine vereinigte Fläche mit dünnem schwarzem Rand, vor den Leitungen gezeichnet (darunter)
        var plan = Grafik.Plan(p, "AN-EG");
        var dm = DaemmTeil(plan)!;
        // einzeln wählbar (12.11.2026): die gemeinsame Fläche ohne Id, Ränder und Trefferfläche mit der Id der Leitung
        Assert.Single(plan.Teile, x => x.Teil == DaemmGrafik.Kennung && x.Id is null);
        Assert.Single(plan.Teile, x => x.Teil == DaemmGrafik.Kennung && x.Id == Hole(p, "RO-1").Id);
        var b = Flaeche(dm);
        var rohrFarbe = HlksEinordnung.Farbe(HlksEinordnung.Medium(p, Hole(p, "RO-1")));
        Assert.Equal(HlksGrafik.DaemmDeckkraft, b.Stil.Deckkraft);
        Assert.Null(b.Stil.Stift);   // Fläche ohne Rand; die Ränder sind dünne schwarze Linien
        Assert.All(dm.Primitive.Where(x => x.Art == "polylinie"), x => { Assert.Equal(rohrFarbe, x.Stil.Farbe); Assert.Equal(Stifte.Fein, x.Stil.Stift); Assert.Equal("voll", x.Stil.Linie); });   // Rand in Rohrfarbe, fein (12.11.2026)
        Assert.Equal((3000.0, 65.0), Ausdehnung(b));
        Assert.True(plan.Teile.FindIndex(x => x.Id is null && x.Teil == DaemmGrafik.Kennung) < plan.Teile.FindIndex(x => x.Id == Hole(p, "RO-1").Id && x.Teil is null));   // hinter dem Rohr
        var t = Teil(p, "RO-1");
        Assert.DoesNotContain(t.Primitive, x => x.Stil.Fuellung == HlksGrafik.DaemmGelb);
        var rohr = t.Primitive.First(x => x.Art == "polygon");
        Assert.Equal(25, Abstand(rohr.Punkte[0], rohr.Punkte[3]), 6);   // das Rohr selbst bleibt 25 mm breit
        Assert.Equal(rohr.Stil.Fuellung, b.Stil.Fuellung);              // Dämmung in der Rohrfarbe
        // ohne Dämmdicke keine Dämmfläche
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 0 });
        Assert.Null(Daemmung(p));
    }

    [Fact]
    public void Grundriss_Daemmung_AnKnickenMitGehrung()
    {
        // Polylinie (Altbestand mit Serie): das Dämmband folgt dem Knick, je Teilstück 65 mm breit
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [1000, 0, 500], [1000, 1000, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var b = Flaeche(Daemmung(p)!);   // eine Fläche über den Knick, ohne Schleife am Innenradius
        Assert.Contains(b.Punkte, q => Math.Abs(q[0] - 967.5) < 0.5 && Math.Abs(q[1] - 32.5) < 0.5);   // innen eine scharfe Ecke
        Assert.DoesNotContain(b.Punkte, q => Math.Abs(q[0] - 1032.5) < 0.5 && Math.Abs(q[1] + 32.5) < 0.5);   // aussen gerundet (keine Gehrungsecke)
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
        BefehlsErgebnis Rohr1OG(string kz, double z, double y) => p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "1.OG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 20" }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, y, z], [3000, y, z]) },
        });
        Ok(Rohr1OG("RO-2", 2800, 0));      // nebeneinander, damit keine die andere verdeckt (LeitungVerdeckung)
        Ok(Rohr1OG("RO-3", 3500, 1000));
        Ok(Rohr1OG("RO-4", 4500, 2000));
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
        // Christian 06.11.2026: Steig- und Fallleitungen nach dem Farbmuster der Leitungen – gefüllt in der Leitungsfarbe (ohne Farbe «schnitt»),
        // Achsenkreuz; unterhalb der Schnitthöhe gleich, darüber und verdeckt wie bisher
        var sp = Teil(p, "RO-S").Primitive;
        var s = Assert.Single(sp, x => x.Art == "kreis");
        var kreuz = sp.Where(x => x.Stil.Linie == "achse").ToList();
        Assert.Equal(2, kreuz.Count);
        // anthrazit mit Überstand (Christian 06.11.2026 «hier können die achskreuze verlängert werden», «achskreuze in antrazith»): Ø 25 + 2 × 12.5
        Assert.All(kreuz, x => { Assert.Equal("#383e42", x.Stil.Farbe); Assert.Equal(50, Math.Abs(x.Punkte[0][0] - x.Punkte[1][0]) + Math.Abs(x.Punkte[0][1] - x.Punkte[1][1]), 9); });
        Assert.Equal(12.5, s.Radius!.Value, 9);                                // Aussendurchmesser/2
        Assert.Equal(s.Stil.Farbe, s.Stil.Fuellung);   // in der Leitungsfarbe (KW)
        Assert.Equal([1000.0, 1000.0], s.Punkte[0]);
        var u = Assert.Single(Teil(p, "RO-U").Primitive, x => x.Art == "kreis");
        Assert.Equal(s.Stil.Fuellung, u.Stil.Fuellung);
        Assert.Equal("voll", u.Stil.Linie);
        var o = Assert.Single(Teil(p, "RO-O").Primitive);
        Assert.Equal("voll", o.Stil.Linie);
        Assert.Null(o.Stil.Fuellung);
        var v = Assert.Single(Teil(p, "RO-V").Primitive);
        Assert.Equal("strich", v.Stil.Linie);
        Etwa([1000, 1000], Assert.Single(Teil(p, "RO-S").Fangpunkte!));
    }

    [Fact]
    public void Daemmung_Lagewechsel_VerdeckteKantenGestrichelt()
    {
        // Christian 09.11.2026: «bei dämmungen passt die darstellung bei bogen nach oben/bogen nach unten. hier die unsichtbaren dämmkanten gestrichelt
        // darstellen» – oben 2500, Steigleitung hinunter, unten 2200 weiter: Ring unter dem oberen Band und das untere Band unter dem Ring gestrichelt
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-O", "DN 20", [0, 0, 2500], [1000, 0, 2500]));
        Ok(Rohr(p, "RO-S", "DN 20", [1000, 0, 2500], [1000, 0, 2200]));
        Ok(Rohr(p, "RO-U", "DN 20", [1000, 0, 2200], [1000, 1000, 2200]));
        foreach (var kz in new[] { "RO-O", "RO-S", "RO-U" }) Aendern(p, kz, new JsonObject { ["daemmdicke"] = 20 });
        var dm = Daemmung(p)!;
        Flaeche(dm);
        var strich = Strichlinien(dm).ToList();
        Assert.NotEmpty(strich);
        // gestrichelt nur am Lagewechsel: im Ring (r = 32.5) bzw. unter dem oberen Band (y ±32.5 bis x 1000), ausgezogen der Rest
        Assert.All(strich.SelectMany(x => x.Punkte), q => Assert.True(Math.Sqrt((q[0] - 1000) * (q[0] - 1000) + q[1] * q[1]) <= 33 || (q[0] <= 1000.5 && Math.Abs(q[1]) <= 33), $"{q[0]}/{q[1]}"));
        Assert.DoesNotContain(strich, x => x.Punkte.Any(q => q[0] < 960));   // das obere Band selbst ist nirgends verdeckt
        Assert.Contains(dm.Primitive, x => x.Art == "polylinie" && x.Stil.Linie == "voll" && x.Punkte.Any(q => q[1] > 900));
        // Ring über dem unteren Band ausgezogen (Teil des Rings bei y > 0 ausserhalb des oberen Bands)
        Assert.Contains(dm.Primitive, x => x.Stil.Linie == "voll" && x.Punkte.Any(q => q[0] > 1020 && q[1] > 20));
    }

    [Fact]
    public void Steigleitung_MitDaemmungUndInPolylinie()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-S", "DN 20", [1000, 1000, 0], [1000, 1000, 3000]));
        Aendern(p, "RO-S", new JsonObject { ["daemmdicke"] = 20 });
        var t = Teil(p, "RO-S");
        Assert.Equal(3, t.Primitive.Count);   // Kreis, Achsenkreuz (2 Linien); der Dämmring steht in der Dämmfläche (DaemmGrafik)
        var dr = Flaeche(Daemmung(p)!);
        Assert.Equal(65, Ausdehnung(dr).Breite, 1);                          // 2 · (25 / 2 + 20)
        Assert.Equal(HlksGrafik.DaemmFuellung(HlksEinordnung.Farbe(HlksEinordnung.Medium(p, Hole(p, "RO-S")))), dr.Stil.Fuellung);   // Rohrfarbe (12.11.2026)
        // Polylinie mit senkrechtem Teilstück durch die Schnittebene: dort erscheint der Kreis zusätzlich
        Ok(Rohr(p, "RO-P", "DN 20", [0, 0, 500], [2000, 0, 500], [2000, 0, 2500], [4000, 0, 2500]));
        var pl = Teil(p, "RO-P");
        var ring = Assert.Single(pl.Primitive, x => x.Art == "kreis" && x.Stil.Fuellung is not null && x.Stil.Fuellung == x.Stil.Farbe);   // Ring der Steigleitung (das Band läuft durch, rund verbunden)
        Assert.Equal([2000.0, 0.0], ring.Punkte[0]);
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
        var eckig = Assert.Single(Teil(p, "KA-2").Primitive, x => x.Art == "polygon" && x.Stil.Deckkraft != 0.35);   // ohne die Rahmen (KanalRahmenTests)
        Assert.Equal(400, Abstand(eckig.Punkte[0], eckig.Punkte[3]), 6);   // b waagrecht
        // 3D: Rechteckkanal b quer, h senkrecht (ANNAHME senkrechte Kanäle: b in x, h in y)
        var q = Assert.Single(Grafik.Koerper(p, [Hole(p, "KA-2").Id]).Single().Teile, t => t.Masse![0] == 2000);   // der Kanal; dazu die Rahmen
        Assert.Equal([2000.0, 400.0, 200.0], q.Masse!);
        var z = Assert.Single(Grafik.Koerper(p, [Hole(p, "KA-1").Id]).Single().Teile);
        Assert.Equal("zylinder", z.Art);
        Assert.Equal(80, z.Radius!.Value, 9);
        // Kanal als Steigleitung: Rechteck b × h leicht in der Leitungsfarbe gefüllt (Christian 06.11.2026, wie Leitungen), ohne Achsenkreuz; Rundkanal als Kreis mit Kreuz
        Kanal(p, "KA-3", "FAM-3", "400x200", [5000, 1000, 0], [5000, 1000, 3000]);
        Kanal(p, "KA-4", "FAM-2", "125", [6000, 1000, 0], [6000, 1000, 3000]);
        var r = Assert.Single(Teil(p, "KA-3").Primitive);
        Assert.Equal("polygon", r.Art);
        Assert.Equal(r.Stil.Farbe ?? "schnitt", r.Stil.Fuellung);
        Assert.Equal(0.2, r.Stil.Deckkraft);
        Etwa([400, 200], [r.Punkte.Max(a => a[0]) - r.Punkte.Min(a => a[0]), r.Punkte.Max(a => a[1]) - r.Punkte.Min(a => a[1])]);
        Assert.Equal(62.5, Assert.Single(Teil(p, "KA-4").Primitive, x => x.Art == "kreis").Radius!.Value, 9);
        Assert.Equal(2, Teil(p, "KA-4").Primitive.Count(x => x.Stil.Linie == "achse"));   // runde Lüftungsrohre mit Achsenkreuz
    }

    [Fact]
    public void Kanal_Daemmung_GelbesBandUndHellerKoerper()
    {
        var p = MitKanaelen();
        Aendern(p, "KA-2", new JsonObject { ["daemmdicke"] = 30 });
        var band = Flaeche(Daemmung(p)!);
        Assert.Equal(460, Math.Min(Ausdehnung(band).Breite, Ausdehnung(band).Hoehe), 6);   // 400 + 2 · 30
        var teile = Grafik.Koerper(p, [Hole(p, "KA-2").Id]).Single().Teile.Where(t => t.Masse![0] == 2000).ToList();   // ohne die Rahmen
        Assert.Equal(2, teile.Count);
        Assert.Equal([2000.0, 400.0, 200.0], teile[0].Masse!);
        Assert.Equal([2000.0, 460.0, 260.0], teile[1].Masse!);
        Assert.True(teile[1].NichtSchneiden);
    }

    [Fact]
    public void Kreuzung_TiefereLeitungVerdeckt_GrobUnterbrochen()
    {
        // Christian 12.11.2026: «leitungen die in einem Grundriss unter einer anderen Leitung durchlaufen (inkl. Dämmung) müssen verdeckt dargestellt
        // werden», «bei grob wird die verdeckte leitung unterbrochen»
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));          // tiefer
        Ok(Rohr(p, "RO-2", "DN 20", [1500, -1000, 1500], [1500, 1000, 1500]));   // höher, kreuzt bei x = 1500
        Aendern(p, "RO-2", new JsonObject { ["daemmdicke"] = 20 });          // Deckfläche 65 mm breit
        static bool Bei(Primitiv x, double xx) => x.Punkte.Any(q => Math.Abs(q[0] - xx) < 40) || (x.Punkte.Count >= 2 && x.Punkte.Min(q => q[0]) < xx && x.Punkte.Max(q => q[0]) > xx);
        var t = Teil(p, "RO-1");
        Assert.Contains(t.Primitive, x => x.Art == "polylinie" && x.Stil.Linie == "strich" && Bei(x, 1500));    // verdeckte Ränder gestrichelt
        Assert.DoesNotContain(t.Primitive, x => x.Art == "polygon" && Polygone.Innen(x.Punkte, 1500, 0));    // keine Füllung unter RO-2
        Assert.DoesNotContain(Teil(p, "RO-2").Primitive, x => x.Stil.Linie == "strich");                       // die höhere bleibt ausgezogen
        // grob: unterbrochen – keine Linie von RO-1 über x = 1500
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        var g = Teil(p, "RO-1");
        Assert.DoesNotContain(g.Primitive, x => x.Art == "polylinie" && x.Punkte.Min(q => q[0]) < 1500 && x.Punkte.Max(q => q[0]) > 1500);
        Assert.True(g.Primitive.Count(x => x.Art == "polylinie") >= 2);
    }

    [Fact]
    public void Kreuzung_SteigleitungVerdecktMitIhremOberenEnde()
    {
        // Entscheid Christian 13.11.2026 (Fragen 5 und 13): Steigleitungen verdecken ebenfalls, massgebend ist ihr oberes Ende. Steigleitung
        // 20 mm neben RO-1 (z 500), Dämmung 20 mm: Ring r 32.5 mm überlappt das Band von RO-1 (12.5 mm)
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [1500, 20, 0], [1500, 20, 3000]));   // reicht bis 3000 über RO-1 hinaus
        Aendern(p, "RO-2", new JsonObject { ["daemmdicke"] = 20 });
        static bool Bei(Primitiv x) => x.Punkte.Any(q => Math.Abs(q[0] - 1500) < 40);
        Assert.Contains(Teil(p, "RO-1").Primitive, x => x.Art == "polylinie" && x.Stil.Linie == "strich" && Bei(x));
        // endet unter RO-1 (oberes Ende 400 < 500): verdeckt nicht
        var q = MitSerie(false);
        Ok(Rohr(q, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(q, "RO-2", "DN 20", [1500, 20, 0], [1500, 20, 400]));
        Aendern(q, "RO-2", new JsonObject { ["daemmdicke"] = 20 });
        Assert.DoesNotContain(Teil(q, "RO-1").Primitive, x => x.Art == "polylinie" && x.Stil.Linie == "strich" && Bei(x));
    }

    [Fact]
    public void Kreuzung_KanalMitKontur_UmrissBleibtAusserhalbDerDeckflaeche()
    {
        // Gesamtprüfung 13.11.2026 (Restliste): Kanal 400 × 200 (Kontur 0.5 mm) von x = 0 bis 2000 bei y = 1000, ein höheres Rohr kreuzt bei
        // x = 1000 (Band 25 mm). Der Umriss des Kanals bleibt mit seinem Stift stehen – samt Stirnkanten bei x = 0 und 2000 – und ist nur unter
        // dem Rohr (987.5 … 1012.5) unterbrochen. Vorher: Umriss durch Ersatzränder ohne Stirnkanten ersetzt.
        var p = MitKanaelen();
        Ok(Rohr(p, "RO-H", "DN 20", [1000, 500, 1500], [1000, 1500, 1500]));
        var t = Teil(p, "KA-2");
        var umriss = t.Primitive.Where(x => x.Art == "polylinie" && x.Stil.Stift == Stifte.Kontur && x.Stil.Linie == "voll").ToList();
        static bool Kante(Primitiv x, double xx) => x.Punkte.Zip(x.Punkte.Skip(1)).Any(s =>
            Math.Abs(s.First[0] - xx) < 0.01 && Math.Abs(s.Second[0] - xx) < 0.01 && Math.Abs(Math.Abs(s.First[1] - s.Second[1]) - 400) < 0.01);
        Assert.Contains(umriss, x => Kante(x, 0)); Assert.Contains(umriss, x => Kante(x, 2000));   // Stirnkanten
        Assert.All(umriss, x => Assert.Null(x.Stil.Fuellung));
        Assert.DoesNotContain(umriss, x => x.Punkte.Zip(x.Punkte.Skip(1)).Any(s => Math.Min(s.First[0], s.Second[0]) < 999 && Math.Max(s.First[0], s.Second[0]) > 1001));
        Assert.Equal(987.5, umriss.SelectMany(x => x.Punkte).Where(q => q[0] < 1000).Max(q => q[0]), 1);
        Assert.Contains(t.Primitive, x => x.Art == "polylinie" && x.Stil.Linie == "strich" && x.Stil.Stift == Stifte.Hilf);   // verdeckte Ränder
    }

    [Fact]
    public void Kreuzung_Grob_LueckeAbRandDesGezeichnetenStrichs()
    {
        // Handrechnung: grob wird mit Stift «mittel» 0.35 mm gezeichnet, Lücke je Seite 0.5 mm Papier → Unterbruch 0.35 + 2 × 0.5 = 1.35 mm Papier,
        // im Massstab 1:50 67.5 mm (vorher aus 0.25 mm gerechnet: 62.5 mm, sichtbare Lücke nur 0.45 mm)
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [1500, -1000, 1500], [1500, 1000, 1500]));
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        var m = Grafik.Plan(p, "AN-EG").Massstab;
        var x = Teil(p, "RO-1").Primitive.Where(q => q.Art == "polylinie").SelectMany(q => q.Punkte).Select(q => q[0]).ToList();
        Assert.Equal(1.35 * m, x.Where(v => v > 1500).Min() - x.Where(v => v < 1500).Max(), 1);
    }

    [Fact]
    public void Kreuzung_PunktImLochDerDeckflaeche_BleibtSichtbar()
    {
        // Gesamtprüfung 13.11.2026: die höhere Leitung als geschlossener Ring – der Dämmpunkt (grob) der tieferen Leitung liegt im Loch der
        // Deckfläche (Ring 1000…2000, Punkt bei x = 1500) und ist nicht verdeckt; die Linie ist nur unter dem Ring unterbrochen
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        Ok(Rohr(p, "RO-2", "DN 20", [1000, -500, 1500], [2000, -500, 1500], [2000, 500, 1500], [1000, 500, 1500], [1000, -500, 1500]));
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        var t = Teil(p, "RO-1");
        Assert.Contains(t.Primitive, x => x.Art == "kreis" && Abstand(x.Punkte[0], [1500, 0]) < 1e-6);
        Assert.Equal(3, t.Primitive.Count(x => x.Art == "polylinie"));   // vor dem Ring, im Ring, nach dem Ring
    }

    [Fact]
    public void Kreuzung_Gefaelleleitung_HoeheAnDerKreuzungEntscheidet()
    {
        // Gesamtprüfung 13.11.2026, Handbeispiel: RO-G 10 m mit 2 % Gefälle (2600 → 2400), drei Leitungen auf 2500 kreuzen bei x = 500 (RO-G dort
        // 2590: liegt darüber), x = 5000 (2500: gleich hoch) und x = 9500 (2410: liegt darunter). Vorher (Höhenspanne der ganzen Leitung): nichts verdeckt.
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-G", "DN 20", [0, 0, 2600], [10000, 0, 2400]));
        Ok(Rohr(p, "RO-A", "DN 20", [500, -1000, 2500], [500, 1000, 2500]));
        Ok(Rohr(p, "RO-B", "DN 20", [5000, -1000, 2500], [5000, 1000, 2500]));
        Ok(Rohr(p, "RO-C", "DN 20", [9500, -1000, 2500], [9500, 1000, 2500]));
        static bool Gefuellt(Grafikteil t, double x) => t.Primitive.Any(q => q.Art == "polygon" && Polygone.Innen(q.Punkte, x, 0));
        var g = Teil(p, "RO-G");
        Assert.True(Gefuellt(g, 500)); Assert.True(Gefuellt(g, 5000)); Assert.False(Gefuellt(g, 9500));
        Assert.False(Gefuellt(Teil(p, "RO-A"), 500));     // unter RO-G
        Assert.True(Gefuellt(Teil(p, "RO-B"), 5000));     // gleich hoch: keine verdeckt die andere
        Assert.True(Gefuellt(Teil(p, "RO-C"), 9500));     // über RO-G
        Assert.DoesNotContain(Teil(p, "RO-B").Primitive, x => x.Stil.Linie == "strich");
        Assert.DoesNotContain(Teil(p, "RO-C").Primitive, x => x.Stil.Linie == "strich");
        // verdeckte Ränder von RO-G nur bei x = 9500 (Band von RO-C 25 mm breit)
        var strich = g.Primitive.Where(x => x.Stil.Linie == "strich").ToList();
        Assert.NotEmpty(strich);
        Assert.All(strich.SelectMany(x => x.Punkte), q => Assert.InRange(q[0], 9500 - 13, 9500 + 13));

        // Dämmkanten (DaemmGrafik) nach derselben Regel: gestrichelt die Kanten von RO-A unter RO-G (x = 500 ± 32.5, |y| ≤ 32.5) und die von RO-G
        // unter RO-C (y = ±32.5, x = 9500 ± 32.5); bei x = 5000 gleiche Lage – keine gestrichelte Kante
        foreach (var kz in new[] { "RO-G", "RO-A", "RO-B", "RO-C" }) Aendern(p, kz, new JsonObject { ["daemmdicke"] = 20 });
        var kanten = Strichlinien(Daemmung(p)!).ToList();
        Assert.Contains(kanten, x => x.Punkte.All(q => Math.Abs(Math.Abs(q[0] - 500) - 32.5) < 0.5 && Math.Abs(q[1]) <= 33));
        Assert.Contains(kanten, x => x.Punkte.All(q => Math.Abs(Math.Abs(q[1]) - 32.5) < 0.5 && Math.Abs(q[0] - 9500) <= 33));
        Assert.All(kanten.SelectMany(x => x.Punkte), q => Assert.True(Math.Abs(q[0] - 500) <= 33 || Math.Abs(q[0] - 9500) <= 33, $"{q[0]}/{q[1]}"));
    }

    [Fact]
    public void Kreuzung_LeitungMitEtage_VerdecktObenUndIstUntenVerdeckt()
    {
        // Gesamtprüfung 13.11.2026, Handbeispiel: RO-E in einem Element 500 → Etage → 2500; RO-U auf 1500 kreuzt sie zweimal (x = 1000 über dem
        // tiefen, x = 5000 unter dem hohen Teil). Vorher: Spannen 500…2500 und 1500 überlappen – nichts verdeckt.
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-E", "DN 20", [0, 0, 500], [2000, 0, 500], [3000, 0, 2500], [6000, 0, 2500]));
        Ok(Rohr(p, "RO-U", "DN 20", [1000, -1000, 1500], [1000, 1000, 1500], [5000, 1000, 1500], [5000, -1000, 1500]));
        static bool Gefuellt(Grafikteil t, double x) => t.Primitive.Any(q => q.Art == "polygon" && Polygone.Innen(q.Punkte, x, 0));
        var e = Teil(p, "RO-E"); var u = Teil(p, "RO-U");
        Assert.False(Gefuellt(e, 1000)); Assert.True(Gefuellt(e, 5000));
        Assert.True(Gefuellt(u, 1000)); Assert.False(Gefuellt(u, 5000));
        // grob: unterbrochen ist je die tiefere Linie
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "grob" });
        static bool Durch(Grafikteil t, double x) => t.Primitive.Any(q => q.Art == "polylinie" && q.Punkte.Zip(q.Punkte.Skip(1)).Any(s =>
            Math.Min(s.First[1], s.Second[1]) <= 0 && Math.Max(s.First[1], s.Second[1]) >= 0 && Math.Min(s.First[0], s.Second[0]) <= x && Math.Max(s.First[0], s.Second[0]) >= x));
        e = Teil(p, "RO-E"); u = Teil(p, "RO-U");
        Assert.False(Durch(e, 1000)); Assert.True(Durch(e, 5000));
        Assert.True(Durch(u, 1000)); Assert.False(Durch(u, 5000));
    }

    [Fact]
    public void Verdeckt_Daemmkanten_InRohrfarbeFein()
    {
        // Christian 12.11.2026: «die aussenkontur in Rohrfarbe jedoch wesentlich feiner» – auch die gestrichelten Kanten unter dem Geschossboden
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-BODEN", "DN 20", [0, 2000, -300], [3000, 2000, -300]));
        Aendern(p, "RO-BODEN", new JsonObject { ["verlegung"] = "frei", ["daemmdicke"] = 20 });
        var raender = Strichlinien(Teil(p, "RO-BODEN")).ToList();
        Assert.Equal(4, raender.Count);
        var daemm = raender.Where(x => Math.Abs(Math.Abs(x.Punkte[0][1] - 2000) - 32.5) < 1e-6).ToList();   // 25 / 2 + 20
        Assert.Equal(2, daemm.Count);
        Assert.All(daemm, x => { Assert.Equal(Stifte.Fein, x.Stil.Stift); Assert.Equal(Kat.Medien["KW"].FarbeHex, x.Stil.Farbe); });
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
        Assert.NotNull(k.Teile[0].Achse); Assert.Null(d.Achse);              // eine Achslinie je Leitung, nicht noch eine für die Dämmung
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
    public void Beschriftung_GedaemmtesRohr_StehtAusserhalbDerDaemmung()
    {
        // Christian 09.11.2026: «beschriftungsposition mit der Dämmung abgleichen»
        var p = MitSerie(false);
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "fein" });
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        double ohne = Teil(p, "RO-1").Primitive.Single(x => x.Art == "text").Punkte[0][1];
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 40 });
        double mit = Teil(p, "RO-1").Primitive.Single(x => x.Art == "text").Punkte[0][1];
        Assert.Equal(40, mit - ohne, 1e-6);   // um die Dämmdicke weiter weg, der Abstand zum Rand bleibt gleich
    }

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
