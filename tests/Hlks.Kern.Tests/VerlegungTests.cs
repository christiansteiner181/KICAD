using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8c: Erkennung der Verlegung (Leitungen/Verlegung.cs). Testhaus FIKTIV, Handrechnungen in mm:
///   Geschosse UG (Kote −3000, Höhe 3000), EG (0, 3000), OG (3000, 3000).
///   D-EG  Decke über UG: 10000 × 8000, Oberkante 0, Dicke 300 (z −300 … 0), aufbau «Beton 300» → Beton, Unterkante − 1 = −301 im
///         Höhenband des UG → Einlageplan «Einlagen D.ü.UG».
///   D-BP  Bodenplatte: gleicher Umriss, Oberkante −3000, Dicke 400 (z −3400 … −3000), «Beton 400»; −3401 liegt in keinem Höhenband
///         und unter dem UG → bodenplatte, Geschoss UG.
///   W-1   Betonwand im EG von (0, 4000) nach (10000, 4000), Dicke 300 (y 3850 … 4150), z 0 … 3000.</summary>
public sealed class VerlegungTests
{
    const string Quelle = "Test B8c, Werte fiktiv";

    static void A(Projekt p, string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));

    static void Platte(Projekt p, string kz, string geschoss, double ok, double dicke, string? aufbau, string? art = null)
    {
        var eig = new JsonObject();
        if (aufbau is not null) eig["aufbau"] = aufbau;
        if (art is not null) eig["art"] = art;
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = kz, ["geschoss"] = geschoss, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, ok], [10000, 0, ok], [10000, 8000, ok], [0, 8000, ok]), ["dicke"] = dicke },
        });
    }

    public static Projekt Haus(string? deckenAufbau = "Beton 300", bool bodenplatte = true, bool wand = true)
    {
        var p = Projekt.Anlegen(Kat, null, "B8c Verlegung (fiktiv)", "T", "pc");
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" });
        foreach (var (kz, kote) in new[] { ("UG", -3000.0), ("EG", 0.0), ("OG", 3000.0) })
            A(p, "element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = 3000 } });
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } });
        Platte(p, "D-EG", "EG", 0, 300, deckenAufbau);
        if (bodenplatte) Platte(p, "D-BP", "UG", -3000, 400, "Beton 400", "boden");
        if (wand) A(p, "wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 4000], [10000, 4000]), ["dicke"] = 300, ["material"] = "beton" });
        return p;
    }

    public static void Rohr(Projekt p, string kz, string geschoss, params double[][] punkte) =>
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = geschoss, ["eigenschaften"] = new JsonObject { ["dn"] = 50 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        });

    static Verlegungsbefund B(Projekt p, string kz) => Verlegung.Von(p).Befund(Hole(p, kz).Id) ?? throw new Xunit.Sdk.XunitException($"{kz} ohne Befund");

    static void Setzen(Projekt p, string kz, string wert, string? quelle = Quelle) =>
        A(p, "element_aendern", new JsonObject
        {
            ["element"] = kz,
            ["eigenschaften"] = new JsonObject { ["verlegung"] = quelle is null ? wert : new JsonObject { ["wert"] = wert, ["quelle"] = quelle } },
        });

    // ------------------------------------------------------------------ Decke, Durchgang, 80 %

    [Fact]
    public void DeckeAusBeton_LeitungDarin_Einlage_MitBauteilUndEinlageplan()
    {
        var p = Haus();
        Rohr(p, "R-1", "UG", [1000, 2000, -150], [5000, 2000, -150]);
        var b = B(p, "R-1");
        Assert.Equal(("einlage", "auto", "einlage"), (b.Verlegung, b.Herkunft, b.Erkannt));
        Assert.Equal(1.0, b.AnteilBeton, 3);
        Assert.Equal(4000, b.Laenge, 1);
        Assert.Equal(("D-EG", "decke"), (b.BauteilName, b.Bauteilart));
        Assert.Equal(Hole(p, "D-EG").Id, b.BauteilElement);
        Assert.Equal(Hole(p, "D-EG").Id, b.BauteilKennung);
        Assert.Equal(("UG", "Einlagen D.ü.UG"), (b.GeschossKennzeichen, b.Plan));   // Geschoss unter der Decke (SIA 410/2 Ziff. 2.3)
        Assert.Contains("Beton", b.Material);
        Assert.Contains("100 % der Achse", b.Begruendung);
        Assert.Null(b.Hinweis);
    }

    [Fact]
    public void Grenze80Prozent_Handrechnung()
    {
        var p = Haus();
        // 4000 von 4500 mm in der Decke (x 6000 … 10000, danach ausserhalb des Umrisses) = 88.9 % → einlage
        Rohr(p, "R-3", "UG", [6000, 1000, -150], [10500, 1000, -150]);
        // 2000 von 2600 mm (x 8000 … 10000) = 76.9 % → frei
        Rohr(p, "R-2", "UG", [8000, 1500, -150], [10600, 1500, -150]);
        Assert.Equal("einlage", B(p, "R-3").Verlegung);
        Assert.Equal(4000.0 / 4500, B(p, "R-3").AnteilBeton, 3);
        Assert.Equal("frei", B(p, "R-2").Verlegung);
        Assert.Equal(2000.0 / 2600, B(p, "R-2").AnteilBeton, 3);
        Assert.Contains("77 % der Achse im Beton (Einlage ab 80 %)", B(p, "R-2").Begruendung);
        Assert.Null(B(p, "R-2").Plan);
    }

    [Fact]
    public void SenkrechtDurchDecke_Durchgang_KeineEinlage()
    {
        var p = Haus();
        // 300 von 320 mm in der Decke (93.75 %), aber quer hindurch (ein und aus, senkrecht) → Durchgang mit Aussparung, frei
        Rohr(p, "R-4", "UG", [2000, 6000, -310], [2000, 6000, 10]);
        // lange Steigleitung UG → EG
        Rohr(p, "R-5", "UG", [3000, 6000, -2500], [3000, 6000, 2500]);
        // waagrecht quer durch die Betonwand W-1 (ein und aus unter 90°) → frei
        Rohr(p, "R-6", "EG", [3000, 3000, 1000], [3000, 5000, 1000]);
        foreach (var kz in new[] { "R-4", "R-5", "R-6" })
        {
            Assert.Equal("frei", B(p, kz).Verlegung);
            Assert.Equal(0, B(p, kz).AnteilBeton, 6);
        }
        Assert.Contains("quer durch D-EG (Durchgang, keine Einlage)", B(p, "R-4").Begruendung);
        Assert.Contains("quer durch W-1", B(p, "R-6").Begruendung);
    }

    [Fact]
    public void Bodenplatte_Wand_Einlagen_UndErdreich()
    {
        var p = Haus();
        Rohr(p, "R-8", "UG", [1000, 7500, -3200], [3000, 7500, -3200]);           // in der Bodenplatte
        Rohr(p, "R-9", "EG", [5000, 4000, 500], [5000, 4000, 2500]);              // längs in der Betonwand (beginnt und endet darin)
        Rohr(p, "R-10", "UG", [1000, 1000, -3700], [6000, 1000, -3800]);          // unter der Bodenplatte
        Rohr(p, "R-11", "UG", [12000, 1000, -3600], [15000, 1000, -3600]);        // ausserhalb, tiefer als die Unterkante −3400
        Rohr(p, "R-12", "UG", [1000, 7000, -2500], [6000, 7000, -2500]);          // im UG frei
        Assert.Equal(("einlage", "bodenplatte", "UG", "Einlagen Bodenplatte UG"), (B(p, "R-8").Verlegung, B(p, "R-8").Bauteilart, B(p, "R-8").GeschossKennzeichen, B(p, "R-8").Plan));
        Assert.Equal(("einlage", "wand", "EG", "Einlagen Wände EG"), (B(p, "R-9").Verlegung, B(p, "R-9").Bauteilart, B(p, "R-9").GeschossKennzeichen, B(p, "R-9").Plan));
        Assert.Equal("W-1", B(p, "R-9").BauteilName);
        foreach (var kz in new[] { "R-10", "R-11" })
        {
            var b = B(p, kz);
            Assert.Equal(("erdreich", "UG", "Grundleitungen UG"), (b.Verlegung, b.GeschossKennzeichen, b.Plan));
            Assert.Equal(1.0, b.AnteilErdreich, 3);
        }
        Assert.Contains("unter der Bodenplatte", B(p, "R-10").Begruendung);
        Assert.Equal("frei", B(p, "R-12").Verlegung);
        Assert.Contains("in keinem Bauteil", B(p, "R-12").Begruendung);
        var vs = Verlegung.Von(p);
        Assert.Equal(1, vs.Bodenplatten);
        var plaene = vs.Plaene(p.Modell).Select(x => x.Schluessel).ToList();
        Assert.Equal(["Grundleitungen UG", "Einlagen Bodenplatte UG", "Einlagen Wände EG"], plaene);
    }

    [Fact]
    public void OhneBodenplatte_ErdreichUnterDerKoteDesUntersten_MitHinweis()
    {
        var p = Haus(bodenplatte: false);
        Rohr(p, "R-10", "UG", [1000, 1000, -3700], [6000, 1000, -3800]);
        Rohr(p, "R-12", "UG", [1000, 7000, -2500], [6000, 7000, -2500]);
        Assert.Equal("erdreich", B(p, "R-10").Verlegung);
        Assert.Contains("keine Bodenplatte bekannt", B(p, "R-10").Begruendung);
        Assert.Equal("frei", B(p, "R-12").Verlegung);
        Assert.Contains(Verlegung.Von(p).Hinweise, h => h.StartsWith("Keine Bodenplatte bekannt") && h.Contains("UG"));
    }

    [Fact]
    public void UnbekanntesMaterial_KeineEinlage_HinweisMoeglicherweiseEinlage()
    {
        var p = Haus(deckenAufbau: null);   // D-EG ohne material und Aufbau
        Rohr(p, "R-1", "UG", [1000, 2000, -150], [5000, 2000, -150]);
        var b = B(p, "R-1");
        Assert.Equal("frei", b.Verlegung);
        Assert.Equal(0, b.AnteilBeton, 6);
        Assert.Equal("möglicherweise Einlage – Material von «D-EG» unbekannt (Typenliste: typmaterial_setzen)", b.Hinweis);
        Assert.Contains(Verlegung.Von(p).Hinweise, h => h.StartsWith("R-1: möglicherweise Einlage"));
        var o = (JsonObject)Tuer.Abfrage(p, "verlegung");
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Material von «D-EG» unbekannt"));
    }

    // ------------------------------------------------------------------ von Hand

    [Fact]
    public void VonHand_GiltUndWirdNieUeberschrieben_MitQuelle_AutoWiederErkannt()
    {
        var p = Haus();
        Rohr(p, "R-1", "UG", [1000, 2000, -150], [5000, 2000, -150]);
        Rohr(p, "R-4", "UG", [2000, 6000, -310], [2000, 6000, 10]);
        Rohr(p, "R-12", "UG", [1000, 7000, -2500], [6000, 7000, -2500]);
        Setzen(p, "R-1", "frei");
        var e = Hole(p, "R-1");
        Assert.Equal("frei", e.Text("verlegung"));
        Assert.Equal(Quelle, e.Eigenschaft("verlegung")!.Herkunft.Quelle);   // element_aendern trägt die Quelle in die Herkunft
        var b = B(p, "R-1");
        Assert.Equal(("frei", "hand", "einlage"), (b.Verlegung, b.Herkunft, b.Erkannt));
        Assert.Null(b.Plan);
        Assert.StartsWith("von Hand gesetzt: frei (mensch T, Quelle: Test B8c, Werte fiktiv); erkannt: einlage", b.Begruendung);
        // Durchgang von Hand zur Einlage: Bauteil aus dem Durchgang, Geschoss unter der Decke
        Setzen(p, "R-4", "einlage");
        Assert.Equal(("einlage", "hand", "frei", "D-EG", "Einlagen D.ü.UG"), (B(p, "R-4").Verlegung, B(p, "R-4").Herkunft, B(p, "R-4").Erkannt, B(p, "R-4").BauteilName, B(p, "R-4").Plan));
        // ohne Bauteil: Geschoss des Elements, Hinweis
        Setzen(p, "R-12", "einlage", quelle: null);
        Assert.Equal("Einlagen UG (Bauteil unbekannt)", B(p, "R-12").Plan);
        Assert.Contains("in keinem Bauteil", B(p, "R-12").Hinweis);
        // eine Änderung der Geometrie überschreibt den Hand-Wert nicht
        A(p, "element_aendern", new JsonObject { ["element"] = "R-1", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([1000, 2500, -150], [5000, 2500, -150]) } });
        Assert.Equal(("frei", "hand"), (B(p, "R-1").Verlegung, B(p, "R-1").Herkunft));
        Assert.Equal("frei", Hole(p, "R-1").Text("verlegung"));
        // auto = wieder erkennen
        Setzen(p, "R-1", "auto");
        Assert.Equal(("einlage", "auto"), (B(p, "R-1").Verlegung, B(p, "R-1").Herkunft));
        // nur die Werte des Katalogs
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "R-1", ["eigenschaften"] = new JsonObject { ["verlegung"] = "beton" } }));
    }

    // ------------------------------------------------------------------ Formteile

    static void Bogen(Projekt p, string kz, double[] punkt) =>
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "UG", ["eigenschaften"] = new JsonObject { ["winkel"] = 90 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) },
        });

    static void Ziehen(Projekt p, string kz, string? von, string? nach, params double[][] punkte)
    {
        var o = new JsonObject { ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "UG", ["dn"] = 50, ["punkte"] = Pts(punkte) };
        if (von is not null) o["von"] = von;
        if (nach is not null) o["nach"] = nach;
        A(p, "leitung_ziehen", o);
    }

    [Fact]
    public void Formteil_WieDieLeitungen_SonstNachEinfuegepunkt()
    {
        var p = Haus();
        // BO-1: beide Leitungen Einlage → einlage (wie die Leitungen)
        Bogen(p, "BO-1", [5000, 2000, -150]);
        Ziehen(p, "R-A", null, "BO-1.a", [1000, 2000, -150], [5000, 2000, -150]);
        Ziehen(p, "R-B", "BO-1.b", null, [5000, 2000, -150], [5000, 3500, -150]);
        // BO-2: senkrechte Leitung von unten (frei, 150 von 1850 mm in der Decke) und Einlage – uneinig; Einfügepunkt im Beton → einlage
        Bogen(p, "BO-2", [2000, 6000, -150]);
        Ziehen(p, "R-C", null, "BO-2.a", [2000, 6000, -2000], [2000, 6000, -150]);
        Ziehen(p, "R-D", "BO-2.b", null, [2000, 6000, -150], [6000, 6000, -150]);
        // BO-3: uneinig, Einfügepunkt in der Luft des UG → frei
        Bogen(p, "BO-3", [8000, 7000, -1000]);
        Ziehen(p, "R-E", null, "BO-3.a", [6000, 7000, -150], [8000, 7000, -150]);
        Ziehen(p, "R-F", "BO-3.b", null, [8000, 7000, -1000], [8000, 7000, -2000]);
        // ohne Leitung: nach dem Einfügepunkt (in der Decke → einlage, unter der Bodenplatte → erdreich)
        Bogen(p, "BO-4", [9000, 7500, -150]);
        Bogen(p, "BO-5", [9000, 7500, -3700]);
        Assert.Equal(("einlage", "einlage", "frei"), (B(p, "R-A").Verlegung, B(p, "R-D").Verlegung, B(p, "R-C").Verlegung));
        var b1 = B(p, "BO-1");
        Assert.Equal(("einlage", "Einlagen D.ü.UG", "D-EG"), (b1.Verlegung, b1.Plan, b1.BauteilName));
        Assert.StartsWith("wie die angeschlossenen Leitungen R-A, R-B (einlage)", b1.Begruendung);
        Assert.Equal(0, b1.Laenge);
        var b2 = B(p, "BO-2");
        Assert.Equal(("einlage", "D-EG", "Einlagen D.ü.UG"), (b2.Verlegung, b2.BauteilName, b2.Plan));
        Assert.Contains("Leitungen uneinig", b2.Begruendung);
        Assert.Contains("Einfügepunkt im Beton von D-EG", b2.Begruendung);
        Assert.Equal("frei", B(p, "BO-3").Verlegung);
        Assert.Equal("einlage", B(p, "BO-4").Verlegung);
        Assert.Contains("ohne angeschlossene Leitung", B(p, "BO-4").Begruendung);
        Assert.Equal(("erdreich", "Grundleitungen UG"), (B(p, "BO-5").Verlegung, B(p, "BO-5").Plan));
        // Formteil folgt der Hand-Verlegung seiner Leitungen; von Hand gesetzt gilt der eigene Wert
        Setzen(p, "R-A", "frei"); Setzen(p, "R-B", "frei");
        Assert.Equal("frei", B(p, "BO-1").Verlegung);
        Setzen(p, "BO-1", "einlage");
        Assert.Equal(("einlage", "hand"), (B(p, "BO-1").Verlegung, B(p, "BO-1").Herkunft));
    }

    // ------------------------------------------------------------------ Zwischenspeicher

    [Fact]
    public void Zwischenspeicher_JeModellstand_KontextBleibtBeiLeitungen_NeuBeiBauteilen()
    {
        var p = Haus();
        Rohr(p, "R-1", "UG", [1000, 2000, -150], [5000, 2000, -150]);
        Rohr(p, "R-3", "UG", [6000, 1000, -150], [10500, 1000, -150]);
        var a = Verlegung.Von(p);
        Assert.Same(a, Verlegung.Von(p));   // gleicher Modellstand: dasselbe Ergebnis
        // Leitung verschoben: neues Ergebnis, Bauteilkontext (Prüfkörper, Material) wiederverwendet
        A(p, "element_aendern", new JsonObject { ["element"] = "R-1", ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([1000, 2000, -1000], [5000, 2000, -1000]) } });
        var c = Verlegung.Von(p);
        Assert.NotSame(a, c);
        Assert.Equal("frei", c.Befund(Hole(p, "R-1").Id)!.Verlegung);
        Assert.Equal("einlage", c.Befund(Hole(p, "R-3").Id)!.Verlegung);
        Assert.Same(a.Kontext, c.Kontext);
        // Hand-Wert: ebenso nur neu zusammengesetzt
        Setzen(p, "R-3", "frei");
        var d = Verlegung.Von(p);
        Assert.Same(c.Kontext, d.Kontext);
        Assert.Equal("hand", d.Befund(Hole(p, "R-3").Id)!.Herkunft);
        // Material der Decke geändert: neuer Bauteilkontext, R-3 nicht mehr im Beton
        A(p, "element_aendern", new JsonObject { ["element"] = "D-EG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Holz 300" } });
        var e = Verlegung.Von(p);
        Assert.NotSame(d.Kontext, e.Kontext);
        Assert.Equal("frei", e.Befund(Hole(p, "R-3").Id)!.Erkannt);
        // Rückgängig: wieder Beton
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Equal("einlage", Verlegung.Von(p).Befund(Hole(p, "R-3").Id)!.Erkannt);
    }

    // ------------------------------------------------------------------ Abfrage

    [Fact]
    public void Abfrage_Verlegung_PlaeneFilterUndFehler()
    {
        var p = Haus();
        Rohr(p, "R-1", "UG", [1000, 2000, -150], [5000, 2000, -150]);
        Rohr(p, "R-8", "UG", [1000, 7500, -3200], [3000, 7500, -3200]);
        Rohr(p, "R-9", "EG", [5000, 4000, 500], [5000, 4000, 2500]);
        Rohr(p, "R-10", "UG", [1000, 1000, -3700], [6000, 1000, -3800]);
        Rohr(p, "R-12", "UG", [1000, 7000, -2500], [6000, 7000, -2500]);
        Assert.Contains(Abfragen.Liste, a => a.Name == "verlegung");
        var o = (JsonObject)Tuer.Abfrage(p, "verlegung");
        Assert.Equal(p.Modell.Pruefsumme(), o["stand"]!.GetValue<string>());
        Assert.Equal(5, o["zaehlung"]!["leitungen"]!.GetValue<int>());
        Assert.Equal((3, 1, 1), (o["zaehlung"]!["einlage"]!.GetValue<int>(), o["zaehlung"]!["erdreich"]!.GetValue<int>(), o["zaehlung"]!["frei"]!.GetValue<int>()));
        var plaene = o["plaene"]!.AsArray();
        Assert.Equal(["Grundleitungen UG", "Einlagen Bodenplatte UG", "Einlagen D.ü.UG", "Einlagen Wände EG"], plaene.Select(x => x!["plan"]!.GetValue<string>()).ToArray());
        var dUg = plaene.Single(x => x!["plan"]!.GetValue<string>() == "Einlagen D.ü.UG")!;
        Assert.Equal(("einlage", "decke", "UG", 1, 4.0), (dUg["art"]!.GetValue<string>(), dUg["bauteilart"]!.GetValue<string>(), dUg["geschoss"]!.GetValue<string>(),
            dUg["leitungen"]!.GetValue<int>(), dUg["laenge_m"]!.GetValue<double>()));
        var r1 = dUg["elemente"]!.AsArray().Single()!;
        Assert.Equal(("R-1", "Rohr", "auto", 100.0, "D-EG"), (r1["kennzeichen"]!.GetValue<string>(), r1["klasse"]!.GetValue<string>(), r1["herkunft"]!.GetValue<string>(),
            r1["anteilBeton"]!.GetValue<double>(), r1["bauteil"]!.GetValue<string>()));
        Assert.Equal(100.0, plaene[0]!["elemente"]![0]!["anteilErdreich"]!.GetValue<double>());
        // Filter
        var eg = (JsonObject)Tuer.Abfrage(p, "verlegung", new JsonObject { ["geschoss"] = "EG" });
        Assert.Equal(["Einlagen Wände EG"], eg["plaene"]!.AsArray().Select(x => x!["plan"]!.GetValue<string>()).ToArray());
        var erd = (JsonObject)Tuer.Abfrage(p, "verlegung", new JsonObject { ["art"] = "erdreich" });
        Assert.Equal(["Grundleitungen UG"], erd["plaene"]!.AsArray().Select(x => x!["plan"]!.GetValue<string>()).ToArray());
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "verlegung", new JsonObject { ["art"] = "beton" }));
        Assert.Contains("einlage oder erdreich", f.Vorschlag);
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "verlegung", new JsonObject { ["geschoss"] = "XX" }));
    }

    [Fact]
    public void OhneBauteile_AlleFrei_NurHinweisBodenplatte()
    {
        var p = Neu();   // nur Geschoss EG (Kote 0)
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } });
        Rohr(p, "R-1", "EG", [0, 0, 500], [1000, 0, 500]);
        var vs = Verlegung.Von(p);
        Assert.Equal(("frei", "auto"), (vs.Befund(Hole(p, "R-1").Id)!.Verlegung, vs.Befund(Hole(p, "R-1").Id)!.Herkunft));
        Assert.Equal(0, vs.Bauteile);
        Assert.StartsWith("Keine Bodenplatte bekannt", Assert.Single(vs.Hinweise));
    }

    [Fact]
    public void PlatteMitOffenemNetz_LotrechterParitaetstest()
    {
        // Netz einer Platte 1000 × 1000, z 0 … 300, nur Ober- und Unterseite (offen, ohne Seitenflächen; Befund Bernstrasse 42)
        double[] pu = [0, 0, 0, 1000, 0, 0, 1000, 1000, 0, 0, 1000, 0, 0, 0, 300, 1000, 0, 300, 1000, 1000, 300, 0, 1000, 300];
        int[] dr = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7];
        var n = new Pruefnetz(pu, dr);
        Assert.False(n.HatGeschlosseneTeile);
        Assert.True(Verlegung.InnenLotrecht(n, new V3(500, 500, 150)));
        Assert.False(Verlegung.InnenLotrecht(n, new V3(500, 500, 400)));
        Assert.False(Verlegung.InnenLotrecht(n, new V3(1500, 500, 150)));
        // waagrechte Achse x −500 … 600 in der Platte: innen 0 … 600 (Teilung 50 mm und Durchstosspunkte)
        var g = Assert.Single(Verlegung.DurchgaengeLotrecht(n, new V3(-500, 500, 150), new V3(600, 500, 150)));
        Assert.Equal(600, g.Tiefe, 0);
        Assert.False(g.BeginntInnen);
        Assert.True(g.EndetInnen);
    }
}
