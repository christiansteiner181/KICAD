using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Plangrafik (Grundriss) und Körper der Architektur: Wandumriss, Öffnungen, Raum, Achse, Decke.</summary>
public class ArchitekturGrafikTests
{
    // ---------------------------------------------------------------- Wandumriss

    [Fact]
    public void Umriss_VierWaendeGeschlossen_AussenUndInnenringOhneDoppellinien()
    {
        var p = Rechteck();
        var g = Grafik.Plan(p, "AN-EG");
        var umriss = Umriss(g);
        var poly = Assert.Single(umriss);                       // ein zusammenhängender Umriss
        Assert.Equal("polygon", poly.Art);
        Assert.Equal(4, poly.Punkte.Count);                     // nur die vier Aussenecken, keine Zwischenpunkte an den Ecken
        Nahe([-100, -100, 6100, 4100], Box(poly.Punkte));       // Aussenring 6200 × 4200
        var loch = Assert.Single(poly.Loecher!);
        Assert.Equal(4, loch.Count);
        Nahe([100, 100, 5900, 3900], Box(loch));                // Innenring 5800 × 3800
        Assert.True(Polygone.Flaecheninhalt(poly.Punkte) > 0);  // Aussenring im Gegenuhrzeigersinn
        Assert.True(Polygone.Flaecheninhalt(loch) < 0);         // Loch im Uhrzeigersinn
        Assert.Null(poly.Stil.Fuellung);
    }

    [Fact]
    public void Wandteile_JeWandEigeneFlaecheOhneUeberlappung_MitFangkantenUndFangpunkten()
    {
        var p = Rechteck();
        var g = Grafik.Plan(p, "AN-EG");
        double gesamt = PolyFlaeche(Assert.Single(Umriss(g)));
        double summe = 0;
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4" })
        {
            var t = Teil(g, p, kz);
            Assert.Equal("wand", t.Ebene);
            Assert.All(t.Primitive, x => { Assert.Null(x.Stil.Stift); Assert.Equal("schnitt", x.Stil.Fuellung); Assert.Equal(0.25, x.Stil.Deckkraft); });
            summe += t.Primitive.Sum(PolyFlaeche);
            Assert.Equal(2, t.Fangkanten!.Count);               // beide Wandflächen
            var achse = Hole(p, kz).Geometrie!.Punkte;
            Assert.Contains(t.Fangpunkte!, f => Math.Abs(f[0] - achse[0][0]) < 1e-6 && Math.Abs(f[1] - achse[0][1]) < 1e-6);
        }
        Assert.Equal(gesamt, summe, 3);                          // Wandflächen decken den Umriss genau einmal
        Assert.Equal(6200.0 * 4200 - 5800.0 * 3800, gesamt, 3);
        // Fangkanten von W-1: Wandflächen y = −100 und y = +100 über die verlängerte Länge −100 … 6100
        var w1 = Teil(g, p, "W-1").Fangkanten!;
        Assert.Contains(w1, k => Math.Abs(k[0][1] - 100) < 1e-6 && Math.Abs(k[1][1] - 100) < 1e-6 && Math.Abs(Math.Abs(k[1][0] - k[0][0]) - 6200) < 1e-6);
        Assert.Contains(w1, k => Math.Abs(k[0][1] + 100) < 1e-6 && Math.Abs(k[1][1] + 100) < 1e-6);
    }

    [Fact]
    public void Wandteil_MaterialBetonGibtSchraffurUeberPapier()
    {
        var p = Rechteck("beton");
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "W-1");
        Assert.All(t.Primitive, x => { Assert.Equal("beton", x.Stil.Muster); Assert.Equal("papier", x.Stil.Fuellung); Assert.Null(x.Stil.Stift); });
        Assert.Equal("schnitt", ArchitekturGrafik.Schnittstil("leichtbau").Fuellung);   // Katalogmaterial ohne Muster
        Assert.Equal("schnitt", ArchitekturGrafik.Schnittstil(null).Fuellung);
    }

    [Fact]
    public void TWand_AnschlussOhneDoppellinie()
    {
        var p = Rechteck();
        // Innenwand von der Südwand zur Nordwand bei x = 3000: Enden in den Achsen der Aussenwände
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([3000, 0], [3000, 4000]), ["dicke"] = 100 }));
        var w5 = Wandgeometrie.MitVerlaengerung(p.Modell, p.Katalog, Hole(p, "W-5"))!;
        Assert.Equal(100, w5.VerlAnfang);                        // halbe Dicke der Südwand
        Assert.Equal(100, w5.VerlEnde);
        var poly = Assert.Single(Umriss(Grafik.Plan(p, "AN-EG")));
        Assert.Equal(2, poly.Loecher!.Count);                    // zwei Räume
        Assert.All(poly.Loecher!, l => Assert.Equal(4, l.Count));
        Assert.Contains(poly.Loecher!, l => Box(l).Zip(new[] { 100.0, 100, 2950, 3900 }).All(z => Math.Abs(z.First - z.Second) < 1e-3));
    }

    [Fact]
    public void NiedrigeWand_SichtkantenDuennNichtImSchnittumriss()
    {
        var p = Neu();
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [2000, 0]), ["dicke"] = 150, ["hoehe"] = 800 }));
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Empty(Umriss(g));
        var sicht = g.Teile.Where(t => t.Id is null && t.Ebene == "wand").SelectMany(t => t.Primitive).ToList();
        var kante = Assert.Single(sicht);
        Assert.Equal(Stifte.Duenn, kante.Stil.Stift);
        Nahe([0, -75, 2000, 75], Box(kante.Punkte));
        var teil = Teil(g, p, "W-1");
        Assert.All(teil.Primitive, x => { Assert.Null(x.Stil.Stift); Assert.Null(x.Stil.Fuellung); });
    }

    // ---------------------------------------------------------------- Tür und Fenster

    [Fact]
    public void Tuer_LueckeImUmriss_BlattUndBogen()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var g = Grafik.Plan(p, "AN-EG");
        var poly = Assert.Single(Umriss(g));
        Assert.True(poly.Loecher is null || poly.Loecher.Count == 0);            // Ring aufgeschnitten
        Assert.False(Polygone.Innen(poly.Punkte, 3000, 0));                         // Lücke 2550 … 3450
        Assert.True(Polygone.Innen(poly.Punkte, 2540, 0));
        Assert.True(Polygone.Innen(poly.Punkte, 3460, 0));
        Assert.Equal(2, poly.Punkte.Count(q => Math.Abs(q[0] - 2550) < 1e-6));     // Leibungen über die ganze Dicke
        Assert.Equal(2, poly.Punkte.Count(q => Math.Abs(q[0] - 3450) < 1e-6));
        Assert.Contains(poly.Punkte, q => Math.Abs(q[0] - 2550) < 1e-6 && Math.Abs(q[1] + 100) < 1e-6);
        Assert.Contains(poly.Punkte, q => Math.Abs(q[0] - 2550) < 1e-6 && Math.Abs(q[1] - 100) < 1e-6);

        var t = Teil(g, p, "T-1");
        Assert.Equal("oeffnung", t.Ebene);
        var blatt = t.Primitive.Single(x => x.Art == "linie");
        Assert.Equal(Stifte.Mittel, blatt.Stil.Stift);
        // seite links (+y), band anfang: Drehpunkt an der Leibung zum Wandanfang auf der Wandfläche links
        Nahe([2550, 100], blatt.Punkte[0]);
        Nahe([2550, 1000], blatt.Punkte[1]);                                        // 90°, Länge = Breite 900
        var bogen = t.Primitive.Single(x => x.Art == "bogen");
        Assert.Equal(Stifte.Duenn, bogen.Stil.Stift);
        Nahe([2550, 100], bogen.Punkte[0]);
        Assert.Equal(900, bogen.Radius!.Value, 6);
        Assert.Equal(0, bogen.Von!.Value, 6);                                       // vom geschlossenen Blatt (+x) …
        Assert.Equal(90, bogen.Bis!.Value, 6);                                      // … zum offenen (+y), Viertelkreis
    }

    [Fact]
    public void Tuer_SeiteRechtsBandEnde()
    {
        var p = Rechteck();
        Ok(Tuer(p, seite: "rechts", band: "ende"));
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "T-1");
        var blatt = t.Primitive.Single(x => x.Art == "linie");
        Nahe([3450, -100], blatt.Punkte[0]);
        Nahe([3450, -1000], blatt.Punkte[1]);
        var bogen = t.Primitive.Single(x => x.Art == "bogen");
        Assert.Equal(180, bogen.Von!.Value, 6);
        Assert.Equal(270, bogen.Bis!.Value, 6);
    }

    [Fact]
    public void Fenster_LueckeNurWennSchnitthoeheImFensterbereich()
    {
        var p = Rechteck();
        Grundriss(p, "AN-TIEF", 500);      // unter der Brüstung
        Ok(Fenster(p));                     // Brüstung 900, Höhe 1200 → 900 … 2100
        var hoch = Grafik.Plan(p, "AN-EG"); // Schnitthöhe 1000
        var poly = Assert.Single(Umriss(hoch));
        Assert.False(Polygone.Innen(poly.Punkte, 3000, 0));
        Assert.True(poly.Loecher is null || poly.Loecher.Count == 0);
        var f = Teil(hoch, p, "F-1");
        Assert.Equal("oeffnung", f.Ebene);
        Assert.All(f.Primitive.Where(x => !x.Stil.NurBildschirm), x => Assert.Equal(Stifte.Duenn, x.Stil.Stift));
        Assert.Contains(f.Primitive, x => x.Art == "polygon" && x.Stil.NurBildschirm);   // Trefferfläche über der Öffnung (nur Bildschirm)
        var bank = f.Primitive.Single(x => x.Art == "polylinie");                   // Fensterbank links der Wandrichtung (+y)
        Assert.All(bank.Punkte, q => Assert.True(q[1] >= 100 - 1e-6));
        Assert.Contains(f.Primitive, x => x.Art == "linie" && x.Punkte.All(q => Math.Abs(q[1]) < 1e-6));   // Glaslinie in der Wandmitte

        var tief = Grafik.Plan(p, "AN-TIEF");
        var ganz = Assert.Single(Umriss(tief));
        Assert.Single(ganz.Loecher!);                                               // Wand durchgehend, keine Lücke
        Assert.True(Polygone.Innen(ganz.Punkte, 3000, 0) && !Polygone.Innen(ganz.Loecher![0], 3000, 0));
        var fTief = Teil(tief, p, "F-1");
        var glas = Assert.Single(fTief.Primitive.Where(x => !x.Stil.NurBildschirm));
        Assert.Equal("strich", glas.Stil.Linie);
    }

    // ---------------------------------------------------------------- Raum, Achse, Decke

    [Fact]
    public void Raum_StempelBegrenzungUndBildschirmfarbe()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nummer"] = "0.01", ["nutzung"] = "Wohnen" }));
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "R-1");
        Assert.Equal("raum", t.Ebene);
        var farbe = t.Primitive.Single(x => x.Art == "polygon" && x.Stil.NurBildschirm);
        Assert.Equal("#fff1b8", farbe.Stil.Fuellung);
        Assert.Equal(0.35, farbe.Stil.Deckkraft);
        Assert.Null(farbe.Stil.Stift);
        var rand = t.Primitive.Single(x => x.Art == "polygon" && !x.Stil.NurBildschirm);
        Assert.Equal(Stifte.Duenn, rand.Stil.Stift);
        var texte = t.Primitive.Where(x => x.Art == "text").ToList();
        Assert.Equal(new[] { "0.01", "Wohnen", "22.04 m²" }, texte.Select(x => x.Text!).ToArray());
        Assert.True(texte[0].Fett);
        Assert.All(texte, x => { Assert.Equal(2.5, x.Groesse); Assert.Equal("mitte", x.Ausrichtung); Assert.Equal(3000, x.Punkte[0][0], 3); });
        Assert.True(texte[0].Punkte[0][1] > texte[1].Punkte[0][1] && texte[1].Punkte[0][1] > texte[2].Punkte[0][1]);
        Assert.Equal("#eeeeee", ArchitekturGrafik.RaumFarbe(null));
        Assert.Equal("#cfe3f7", ArchitekturGrafik.RaumFarbe("Dusche/WC"));
    }

    [Fact]
    public void Raumstempel_WeichtHindernissenAus()
    {
        List<double[]> ring = [[0, 0], [6000, 0], [6000, 4000], [0, 4000]];
        Nahe([3000, 2000], Polygone.StempelLage(ring, 1000, 500, []));
        var lage = Polygone.StempelLage(ring, 1000, 500, [[2000, 1000, 4000, 3000]]);
        Assert.False(lage[0] + 500 > 2000 && lage[0] - 500 < 4000 && lage[1] + 250 > 1000 && lage[1] - 250 < 3000);
        // L-förmiger Raum: Schwerpunkt ausserhalb → Stempel trotzdem innen
        List<double[]> l = [[0, 0], [6000, 0], [6000, 1000], [1000, 1000], [1000, 6000], [0, 6000]];
        var s = Polygone.StempelLage(l, 400, 300, []);
        Assert.True(Polygone.ImRing(l, s[0], s[1]));
    }

    [Fact]
    public void Achse_StrichpunktMitKreisenUndBezeichnungAnBeidenEnden()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Achse", ["kennzeichen"] = "AX-A", ["eigenschaften"] = new JsonObject { ["bezeichnung"] = "A" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([0, -2000], [0, 6000]) },
        }));
        var t = Teil(Grafik.Plan(p, "AN-EG"), p, "AX-A");
        Assert.Equal("achse", t.Ebene);
        var linie = t.Primitive.Single(x => x.Art == "linie");
        Assert.Equal("strichpunkt", linie.Stil.Linie);
        Assert.Equal(Stifte.Duenn, linie.Stil.Stift);
        var kreise = t.Primitive.Where(x => x.Art == "kreis").ToList();
        Assert.Equal(2, kreise.Count);
        Assert.All(kreise, k => Assert.Equal(200, k.Radius!.Value, 6));            // Ø 8 mm bei 1:50
        Assert.Contains(kreise, k => Math.Abs(k.Punkte[0][1] + 2200) < 1e-6);
        Assert.Contains(kreise, k => Math.Abs(k.Punkte[0][1] - 6200) < 1e-6);
        Assert.Equal(2, t.Primitive.Count(x => x.Art == "text" && x.Text == "A"));
        Assert.Null(new ArchitekturGrafik().Koerper(p, Hole(p, "AX-A")));
    }

    [Fact]
    public void Decke_NurWennGeschnittenUmrissDuenn_KoerperPrisma()
    {
        var p = Neu();
        JsonObject Decke(string kz, double z) => new()
        {
            ["klasse"] = "Decke", ["kennzeichen"] = kz, ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, z], [5000, 0, z], [5000, 4000, z], [0, 4000, z]), ["dicke"] = 250 },
        };
        Ok(p.Ausfuehren("element_anlegen", Decke("D-OBEN", 3000)));        // 2750 … 3000: nicht geschnitten
        Ok(p.Ausfuehren("element_anlegen", Decke("D-SCHNITT", 1100)));     // 850 … 1100: geschnitten bei 1000
        var g = Grafik.Plan(p, "AN-EG");
        Assert.DoesNotContain(g.Teile, t => t.Id == Hole(p, "D-OBEN").Id);
        var t = Teil(g, p, "D-SCHNITT");
        Assert.Equal("decke", t.Ebene);
        Assert.Equal(Stifte.Duenn, Assert.Single(t.Primitive).Stil.Stift);
        var k = new ArchitekturGrafik().Koerper(p, Hole(p, "D-OBEN"))!;
        var teil = Assert.Single(k.Teile);
        Assert.Equal("prisma", teil.Art);
        Assert.Equal(2750, teil.Unten);
        Assert.Equal(3000, teil.Oben);
        Assert.Equal("decke", teil.Material);
    }

    // ---------------------------------------------------------------- Körper

    static List<Koerperteil> Quader(Projekt p, string kz) => new ArchitekturGrafik().Koerper(p, Hole(p, kz))!.Teile.OrderBy(t => t.Mitte![0]).ThenBy(t => t.Mitte![2]).ToList();

    [Fact]
    public void Koerper_WandOhneOeffnung_EinQuaderVerlaengert()
    {
        var p = Rechteck();
        var q = Assert.Single(Quader(p, "W-1"));
        Assert.Equal("quader", q.Art);
        Assert.Equal("wand", q.Material);
        Nahe([6200, 200, 3000], q.Masse!);
        Nahe([3000, 0, 1500], q.Mitte!);
        Assert.Equal(0, q.Drehung!.Value, 6);
        var ost = Assert.Single(Quader(p, "W-2"));
        Assert.Equal(90, ost.Drehung!.Value, 6);
        Nahe([6000, 2000, 1500], ost.Mitte!);
    }

    [Fact]
    public void Koerper_WandMitTuer_ZweiQuaderSeitlichUndSturz()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        var q = Quader(p, "W-1");
        Assert.Equal(3, q.Count);
        Nahe([2650, 200, 3000], q[0].Masse!);      // −100 … 2550
        Nahe([1225, 0, 1500], q[0].Mitte!);
        Nahe([900, 200, 900], q[1].Masse!);        // Sturz 2100 … 3000 über der Tür
        Nahe([3000, 0, 2550], q[1].Mitte!);
        Nahe([2650, 200, 3000], q[2].Masse!);      // 3450 … 6100
        var tuer = Assert.Single(new ArchitekturGrafik().Koerper(p, Hole(p, "T-1"))!.Teile);
        Assert.Equal("tuer", tuer.Material);
        Nahe([900, 40, 2100], tuer.Masse!);
        Nahe([3000, 0, 1050], tuer.Mitte!);
    }

    [Fact]
    public void Koerper_WandMitFenster_ZusaetzlichBruestung()
    {
        var p = Rechteck();
        Ok(Fenster(p));
        var q = Quader(p, "W-1");
        Assert.Equal(4, q.Count);
        var mitte = q.Where(x => Math.Abs(x.Mitte![0] - 3000) < 1e-6).OrderBy(x => x.Mitte![2]).ToList();
        Assert.Equal(2, mitte.Count);
        Nahe([1200, 200, 900], mitte[0].Masse!);   // Brüstung 0 … 900
        Nahe([1200, 200, 900], mitte[1].Masse!);   // Sturz 2100 … 3000
        Assert.Equal(450, mitte[0].Mitte![2], 6);
        Assert.Equal(2550, mitte[1].Mitte![2], 6);
        var glas = Assert.Single(new ArchitekturGrafik().Koerper(p, Hole(p, "F-1"))!.Teile);
        Assert.Equal("glas", glas.Material);
        Assert.Equal(0.35, glas.Deckkraft);
        Assert.Equal(1500, glas.Mitte![2], 6);
    }

    [Fact]
    public void Koerper_TuerMitOberlicht_ZweiStuerzeUeberDerTuer()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(Fenster(p, abstand: 3000, breite: 900, hoehe: 400, bruestung: 2200));   // über der Tür, keine Überschneidung
        var mitte = Quader(p, "W-1").Where(x => Math.Abs(x.Mitte![0] - 3000) < 1e-6).OrderBy(x => x.Mitte![2]).ToList();
        Assert.Equal(2, mitte.Count);
        Assert.Equal(100, mitte[0].Masse![2], 6);  // 2100 … 2200
        Assert.Equal(400, mitte[1].Masse![2], 6);  // 2600 … 3000
    }

    [Fact]
    public void Koerper_RaumDurchscheinendNichtGeschnitten()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(1000, 1000) }));
        var t = Assert.Single(new ArchitekturGrafik().Koerper(p, Hole(p, "R-1"))!.Teile);
        Assert.Equal("prisma", t.Art);
        Assert.Equal("raum", t.Material);
        Assert.Equal(0.15, t.Deckkraft);
        Assert.True(t.NichtSchneiden);
        Assert.Equal(0, t.Unten);
        Assert.Equal(3000, t.Oben);                // Geschosshöhe (keine lichte Höhe gesetzt)
    }

    // ---------------------------------------------------------------- Einstieg Grafik

    [Fact]
    public void GrafikPlanUndKoerper_LiefernAlleArchitekturteile()
    {
        var p = Rechteck();
        Ok(Tuer(p));
        Ok(Fenster(p, wand: "W-3"));
        Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = Pt(3000, 2000), ["nummer"] = "0.01" }));
        var g = Grafik.Plan(p, "AN-EG");
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4", "T-1", "F-1", "R-1" })
            Assert.Contains(g.Teile, t => t.Id == Hole(p, kz).Id);
        Assert.Equal(2, Umriss(g).Count);          // Tür im Süden und Fenster im Norden teilen den Wandring in zwei Stücke
        Assert.All(Umriss(g), x => Assert.True(x.Loecher is null || x.Loecher.Count == 0));
        // Grafik.Begrenzung zählt nur den Bogen selbst (behoben 17.10.2026): der Türbogen schlägt nach innen auf, bleibt im Wandring
        Nahe([-100, -100, 6100, 4100], g.Begrenzung!);
        Assert.Equal(p.Modell.Pruefsumme(), g.Stand);

        var koerper = Grafik.Koerper(p);
        foreach (var kz in new[] { "W-1", "W-2", "W-3", "W-4", "T-1", "F-1", "R-1" })
            Assert.Contains(koerper, k => k.Id == Hole(p, kz).Id);
        Assert.Equal("wand", koerper.Single(k => k.Id == Hole(p, "W-1").Id).Ebene);
        Assert.Equal("oeffnung", koerper.Single(k => k.Id == Hole(p, "T-1").Id).Ebene);
        Assert.Equal("raum", koerper.Single(k => k.Id == Hole(p, "R-1").Id).Ebene);
        var nur = Grafik.Koerper(p, ["W-1"]);
        Assert.Equal(3, Assert.Single(nur).Teile.Count);
    }

    [Fact]
    public void Beispielprojekt_GrundrissEG_AussenwaendeVereinigtRaeumeMitStempel()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var g = Grafik.Plan(p, "AN-GR-EG");
        var poly = Assert.Single(Umriss(g));
        Nahe([-175, -175, 12175, 10175], Box(poly.Punkte));
        Nahe([175, 175, 11825, 9825], Box(Assert.Single(poly.Loecher!)));
        foreach (var kz in new[] { "E.01", "E.02", "E.03", "E.04", "E.05" })
        {
            var t = Teil(g, p, kz);
            Assert.Contains(t.Primitive, x => x.Art == "text" && x.Text == kz && x.Fett == true);
            Assert.Contains(t.Primitive, x => x.Art == "text" && x.Text!.EndsWith(" m²"));
        }
        // Grundriss UG zeigt nur die UG-Wände
        Assert.Single(Umriss(Grafik.Plan(p, "AN-GR-UG")));
        Assert.Equal(12, Grafik.Koerper(p).Count(k => k.Ebene == "wand"));
    }
}
