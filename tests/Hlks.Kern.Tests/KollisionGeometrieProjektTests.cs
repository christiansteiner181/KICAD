using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B1: Prüfkörper aus dem Projekt – Leitungen als Kapsel bzw. Quader um die Achse mit Massen der Serie
/// (Testrohrserie FIKTIV: da = DN + 5) und zweiter Hülle mit daemmdicke, ohne Serie wie die Darstellung; Kanäle mit FIKTIVER
/// Mini-Serie; Bauteile, Apparate und Formteile als Netz aus ihrem Körper; Auswahl; nicht prüfbare Klassen; Leitung in der
/// Wand und durch die Wand.</summary>
public class KollisionGeometrieProjektTests
{
    static void Aendern(Projekt p, string kz, JsonObject eig) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }));

    static Pruefkoerper Von(Pruefkoerpermenge m, Projekt p, string kz) => m.Koerper.Single(k => k.ElementId == Hole(p, kz).Id);

    // ------------------------------------------------------------------ Leitungen

    [Fact]
    public void RohrDN20_TestserieDa25_Daemmung20_HuellenR12_5Und32_5()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var k = Pruefkoerperbau.Leitung(p, Hole(p, "RO-1"))!;
        Assert.True(k.IstLeitung);
        Assert.Equal("achse", k.Quelle);
        Assert.Equal("hlks", k.Bereich);
        Assert.Equal("sanitaer", k.Gewerk);   // System KW: Trinkwasser kalt
        Assert.Equal("Rohr", k.Klasse); Assert.Equal("RO-1", k.Kennzeichen);
        Assert.Equal(0, k.GeschossKote);
        var a = Assert.Single(k.Achsen);
        Assert.Equal(Querschnittsform.Rund, a.Form);
        Assert.Equal(12.5, a.Radius, 12);                                 // da 25 / 2 (FIKTIV)
        Assert.Equal(20, k.Daemmdicke);
        Assert.Equal(32.5, Assert.Single(k.Huellkoerper(true)).Radius, 12);   // 12.5 + 20
        Assert.Equal(12.5, Assert.Single(k.Huellkoerper(false)).Radius, 12);
        Assert.Equal(new Huelle3(new V3(-12.5, -12.5, 487.5), new V3(3012.5, 12.5, 512.5)), k.HuelleOhneDaemmung);
        Assert.Equal(new Huelle3(new V3(-32.5, -32.5, 467.5), new V3(3032.5, 32.5, 532.5)), k.Huelle);
        Assert.Contains("Testrohrserie", k.Massquelle);
        Assert.Contains("DN 20", k.Massquelle);
        Assert.Contains("Dämmung 20", k.Massquelle);
        Assert.Equal([new V3(0, 0, 500), new V3(3000, 0, 500)], k.Achse);
    }

    [Fact]
    public void Rohr_OhneSerie_WieDarstellung_PolylinieJeTeilstueck()
    {
        var p = MitSerie(false);
        Ok(RohrOhneSerie(p, "RO-2", 20, [0, 1000, 500], [3000, 1000, 500]));
        var k2 = Pruefkoerperbau.Leitung(p, Hole(p, "RO-2"))!;
        Assert.Equal(15, k2.Achsen[0].Radius, 12);               // Ø max(DN 20, 30) wie HlksKoerper.MinDurchmesser
        Assert.Contains("DN-gerecht", k2.Massquelle);
        Ok(RohrOhneSerie(p, "RO-3", 50, [0, 2000, 500], [1000, 2000, 500], [1000, 2000, 500], [1000, 3000, 500]));
        var k3 = Pruefkoerperbau.Leitung(p, Hole(p, "RO-3"))!;
        Assert.Equal(2, k3.Achsen.Count);                        // doppelter Punkt entfällt
        Assert.Equal(3, k3.Achse.Count);
        Assert.All(k3.Achsen, a => Assert.Equal(25, a.Radius, 12));
        // Leitung mit Serie und Knick: zwei Kapseln da 55/2
        Ok(Rohr(p, "RO-4", "DN 50", [0, 4000, 500], [1000, 4000, 500], [1000, 5000, 500]));
        Assert.All(Pruefkoerperbau.Leitung(p, Hole(p, "RO-4"))!.Achsen, a => Assert.Equal(27.5, a.Radius, 12));
    }

    static JsonObject Kanalserie(bool rund) => JsonNode.Parse(rund
        ? """
          { "format": 1, "id": "fam-b1-kanal-rund", "name": "Kanal rund (fiktiv B1)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "d", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "160", "werte": { "d": 160 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rund", "groesse": { "d": "d" }, "aussen": "d + 1", "regeln": { "winkel": "frei" } } }
          """
        : """
          { "format": 1, "id": "fam-b1-kanal-eckig", "name": "Kanal eckig (fiktiv B1)", "kategorie": "Kanal",
            "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
            "parameter": [ { "name": "b", "art": "typ", "datentyp": "laenge" }, { "name": "h", "art": "typ", "datentyp": "laenge" } ],
            "typen": [ { "name": "400x200", "werte": { "b": 400, "h": 200 } } ],
            "koerper": [], "anschluesse": [],
            "leitung": { "querschnitt": "rechteck", "groesse": { "b": "b", "h": "h" }, "aussen": 0.001 } }
          """)!.AsObject();

    static void Kanal(Projekt p, string kz, string serie, string typ, params double[][] punkte) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = serie },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        }));

    [Fact]
    public void Kanal_Rund_Und_Rechteckig_LageWieHlksKoerper_KnickVerlaengert()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(true) }));    // FAM-2
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Kanalserie(false) }));   // FAM-3
        Kanal(p, "KA-1", "FAM-2", "160", [0, 0, 2500], [2000, 0, 2500]);
        Kanal(p, "KA-2", "FAM-3", "400x200", [0, 1000, 2500], [2000, 1000, 2500]);
        Kanal(p, "KA-3", "FAM-3", "400x200", [5000, 1000, 0], [5000, 1000, 3000]);
        Kanal(p, "KA-4", "FAM-3", "400x200", [0, 3000, 2500], [1000, 3000, 2500], [1000, 4000, 2500]);
        var rund = Pruefkoerperbau.Leitung(p, Hole(p, "KA-1"))!;
        Assert.Equal(80, Assert.Single(rund.Achsen).Radius, 12);      // Ø d nominal wie die Darstellung
        Assert.Equal("lueftung", rund.Gewerk);
        var eckig = Assert.Single(Pruefkoerperbau.Leitung(p, Hole(p, "KA-2"))!.Achsen);
        Assert.Equal(Querschnittsform.Rechteck, eckig.Form);
        Assert.Equal(400, eckig.Breite); Assert.Equal(200, eckig.Hoehe);
        Assert.Equal(new Huelle3(new V3(0, 800, 2400), new V3(2000, 1200, 2600)), eckig.Huelle);
        var steig = Assert.Single(Pruefkoerperbau.Leitung(p, Hole(p, "KA-3"))!.Achsen);
        Assert.Equal(new Huelle3(new V3(4800, 900, 0), new V3(5200, 1100, 3000)), steig.Huelle);   // b in x, h in y
        var knick = Pruefkoerperbau.Leitung(p, Hole(p, "KA-4"))!;
        Assert.Equal(2, knick.Achsen.Count);
        // innere Enden um max(b, h)/2 = 200 über den Knick verlängert, äussere nicht
        Assert.Equal(new V3(0, 3000, 2500), knick.Achsen[0].A);
        Assert.Equal(new V3(1200, 3000, 2500), knick.Achsen[0].B);
        Assert.Equal(new V3(1000, 2800, 2500), knick.Achsen[1].A);
        Assert.Equal(new V3(1000, 4000, 2500), knick.Achsen[1].B);
        Assert.Equal(new[] { new V3(0, 3000, 2500), new V3(1000, 3000, 2500), new V3(1000, 4000, 2500) }, knick.Achse);
        // Dämmung 30: zweite Hülle 460 × 260
        Aendern(p, "KA-2", new JsonObject { ["daemmdicke"] = 30 });
        var gedaemmt = Assert.Single(Pruefkoerperbau.Leitung(p, Hole(p, "KA-2"))!.Huellkoerper(true));
        Assert.Equal(460, gedaemmt.Breite); Assert.Equal(260, gedaemmt.Hoehe);
    }

    [Fact]
    public void ZweiRohre_Abstand_MitUndOhneDaemmung()
    {
        var p = MitSerie(false);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [0, 100, 500], [3000, 100, 500]));
        Aendern(p, "RO-1", new JsonObject { ["daemmdicke"] = 20 });
        var a = Pruefkoerperbau.Leitung(p, Hole(p, "RO-1"))!; var b = Pruefkoerperbau.Leitung(p, Hole(p, "RO-2"))!;
        // Achsabstand 100: Rohr–Rohr 100 − 25 = 75, mit Dämmung von RO-1 75 − 20 = 55
        Assert.Equal(75, Schmalphase.Pruefen(a, b, maxAbstand: 100).Abstand, 9);
        Assert.Equal(55, Schmalphase.Pruefen(a, b, maxAbstand: 100, mitDaemmungA: true).Abstand, 9);
        Assert.Equal(55, Schmalphase.Pruefen(b, a, maxAbstand: 100, mitDaemmungB: true).Abstand, 9);
        Assert.Same(Kontakt.Fern, Schmalphase.Pruefen(a, b));
    }

    // ------------------------------------------------------------------ Bauteile, Apparate, Formteile

    [Fact]
    public void Waende_AlsNetz_LeitungInDerWandUndDurchDieWand()
    {
        var p = Rechteck();                       // W-1 Süd (0,0)→(6000,0), Dicke 200, Höhe 3000 (Geschoss)
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([100, 100], [5900, 100], [5900, 3900], [100, 3900]) },
        }));
        Ok(RohrOhneSerie(p, "RO-D", 20, [3000, -500, 1000], [3000, 500, 1000]));      // quer durch W-1
        Ok(RohrOhneSerie(p, "RO-W", 20, [1000, 0, 1500], [5000, 0, 1500]));           // in W-1 verlegt
        var m = Pruefkoerperbau.Erzeugen(p);
        Assert.DoesNotContain(m.Koerper, k => k.Klasse == "Raum");                 // Räume sind keine Prüfkörper
        var w1 = Von(m, p, "W-1");
        Assert.Equal("koerper", w1.Quelle);
        Assert.Equal("architektur", w1.Bereich);
        Assert.NotNull(w1.Netz);
        Assert.True(w1.Netz!.Geschlossen);
        Assert.Equal(-100, w1.Huelle.Min.Y, 6); Assert.Equal(100, w1.Huelle.Max.Y, 6);
        Assert.Equal(0, w1.Huelle.Min.Z, 6); Assert.Equal(3000, w1.Huelle.Max.Z, 6);
        var quer = Von(m, p, "RO-D"); var innen = Von(m, p, "RO-W");
        var s = Schmalphase.Pruefen(quer, w1);
        Assert.Equal(Kontaktart.Schnitt, s.Art);
        Assert.Equal(15 + 100, s.Eindringtiefe, 6);                               // r + halbe Wanddicke (Näherung)
        var g = Assert.Single(Durchgaenge.Finden(quer, w1));
        Assert.Equal(200, g.Tiefe, 6);
        Assert.Equal(1000, g.Mitte.Z, 6); Assert.Equal(0, g.Mitte.Y, 6);
        var e = Schmalphase.Pruefen(innen, w1);
        Assert.Equal(Kontaktart.Enthalten, e.Art);                                 // ganz in der Wand: harte Kollision
        Assert.True(e.Hart);
        // Index: Kandidaten der Leitung RO-D sind die Wand W-1 (und die Leitung RO-W in 500 mm Höhe darüber nicht)
        var idx = m.Index();
        var kandidaten = idx.Kandidaten(quer.Huelle).Where(k => k != quer).ToList();
        Assert.Contains(w1, kandidaten);
        Assert.DoesNotContain(innen, kandidaten);
    }

    [Fact]
    public void Apparat_Formteil_AlsNetz_Auswahl_UndHinweise()
    {
        var p = MitSerie(true);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 1000, 0]) },
        }));
        Ok(Formteil(p, "fam-test-bogen", "DN 20", "BO-1", [3000, 2000, 500], winkel: 90));
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [2000, 0, 500]));
        var m = Pruefkoerperbau.Erzeugen(p);
        var wc = Von(m, p, "WC-1");
        Assert.Equal("koerper", wc.Quelle); Assert.Equal("hlks", wc.Bereich);
        Assert.Equal(12, wc.Netz!.AnzahlDreiecke);                                 // Quader b × t × h
        Assert.True(wc.Netz.Geschlossen);
        var (b, t, h) = HlksEinordnung.Mass(p.Katalog, Hole(p, "WC-1"));
        Assert.Equal(new Huelle3(new V3(1000 - b / 2, 1000 - t / 2, 0), new V3(1000 + b / 2, 1000 + t / 2, h)), wc.Huelle);
        var bo = Von(m, p, "BO-1");
        Assert.Equal("koerper", bo.Quelle);
        Assert.NotNull(bo.Netz);
        Assert.True(bo.Netz!.AnzahlDreiecke > 0);
        Assert.True(bo.Huelle.Enthaelt(new V3(3000, 2000, 500), 100), $"Bogen-Hülle {bo.Huelle}");
        // Auswahl: nur WC-1
        var nur = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Elemente = ["WC-1", "GIBTSNICHT"] });
        Assert.Equal("WC-1", Assert.Single(nur.Koerper).Kennzeichen);
        Assert.Contains(nur.Hinweise, x => x.Contains("GIBTSNICHT"));
        Assert.Equal(3, m.Koerper.Count);
        Assert.Equal(3, m.ImBereich("hlks").Count());
    }

    [Fact]
    public void Pruefnetzbau_ZylinderUndKugelUmschrieben_PrismaMitLoch()
    {
        // umschriebenes 24-Eck: Abstand jeder Fläche zur Achse ≥ r, Ecken ≤ r / cos(7.5°)
        var z = Pruefnetz.Aus(Pruefnetzbau.Zylinder(new V3(0, 0, 0), new V3(0, 0, 1000), 100));
        Assert.True(z.Geschlossen);
        Assert.Equal(100, z.Naechster(new V3(0, 0, 500)).Abstand, 6);
        Assert.InRange(z.Huelle.Max.X, 100, 100 / Math.Cos(Math.PI / 24) + 1e-9);
        var k = Pruefnetz.Aus(Pruefnetzbau.Kugel(new V3(10, 20, 30), 50));
        Assert.True(k.Geschlossen);
        Assert.Equal(50, k.Naechster(new V3(10, 20, 30)).Abstand, 6);
        Assert.Equal(Punktlage.Innen, k.Lage(new V3(10, 20, 79)));
        // Prisma mit Loch (Decke) über NetzBau.Extrusion
        var prisma = new Koerperteil
        {
            Art = "prisma", Grundriss = [[0, 0], [1000, 0], [1000, 1000], [0, 1000]], Loecher = [[[400, 400], [600, 400], [600, 600], [400, 600]]],
            Unten = 0, Oben = 250,
        };
        var n = Pruefnetz.Aus(Pruefnetzbau.Teil(prisma)!);
        Assert.True(n.Geschlossen);
        Assert.Equal(Punktlage.Aussen, n.Lage(new V3(500, 500, 100)));   // im Loch
        Assert.Equal(Punktlage.Innen, n.Lage(new V3(200, 200, 100)));
        // Teile mit NichtSchneiden und Netz-Verweise zählen nicht
        var leer = new Koerper { Id = "x", Teile = [new Koerperteil { Art = "netz", Quelle = "u" }, new Koerperteil { Art = "quader", Mitte = [0, 0, 0], Masse = [1, 1, 1], NichtSchneiden = true }] };
        Assert.Null(Pruefnetzbau.AusKoerper(leer));
    }
}
