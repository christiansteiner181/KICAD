using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B10: Grundriss-Filter, Darstellungen «einlagen» und «grundleitungen» (Grafik/EinlageGrafik.cs) und Befehl
/// einlageplaene_anlegen (Plan/EinlagePlaene.cs). Testhaus FIKTIV wie VerlegungTests.Haus (Handrechnungen in mm):
///   Geschosse UG (−3000), EG (0), OG (3000); D-EG Decke über UG 10000 × 8000, z −300 … 0, Beton; D-BP Bodenplatte z −3400 … −3000, Beton;
///   W-1 Betonwand im EG y 3850 … 4150. Dazu Betonwände im UG um das Rechteck 0 … 10000 × 0 … 8000 (Achse), Dicke 200 → Innenflächen
///   x = 100 und 9900, y = 100 und 7900 (Fixpunkte der Massketten auf Schnitthöhe UG −2000). Systeme KW (Trinkwasser kalt) und SW
///   (Schmutzwasser). Leitungen in der Decke auf z −150 (Einlagen D.ü.UG), unter der Bodenplatte auf z −3600 … −3650 (Grundleitungen UG).
///   Familien: Testabwasserserie (FIKTIV, da = DN + 10, s = 3, di = da − 2·s: DN 100 → da 110, di 104) und Testrohrserie (DN 15: da 20).</summary>
public sealed class EinlageplanTests
{
    const string Quelle = "Test B10, Werte fiktiv";

    static void A(Projekt p, string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));

    static Projekt Haus()
    {
        var p = VerlegungTests.Haus();
        A(p, "wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "UG", ["punkte"] = Pts([0, 0], [10000, 0], [10000, 8000], [0, 8000]), ["dicke"] = 200, ["geschlossen"] = true, ["material"] = "beton",
        });
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } });
        Ansicht(p, "AN-UG", "UG");
        return p;
    }

    static void Ansicht(Projekt p, string kz, string geschoss, string? darstellung = null, string? einlagen = null, string? detail = null)
    {
        var eig = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 };
        if (darstellung is not null) eig["darstellung"] = darstellung;
        if (einlagen is not null) eig["einlagen"] = einlagen;
        if (detail is not null) eig["detailgrad"] = detail;
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = geschoss, ["eigenschaften"] = eig });
    }

    static void Rohr(Projekt p, string kz, string system, params double[][] punkte) => RohrDa(p, kz, system, 110, punkte);

    /// <summary>Rohr ohne Serie mit dn 100 und wahlweise Aussendurchmesser da (FIKTIV: Abwasser wird mit dem Aussendurchmesser bezeichnet, B9b).</summary>
    static void RohrDa(Projekt p, string kz, string system, double? da, params double[][] punkte)
    {
        var eig = new JsonObject { ["dn"] = 100 };
        if (da is not null) eig["da"] = da;
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "UG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        });
    }

    static void RohrSerie(Projekt p, string kz, string system, string familie, string typ, params double[][] punkte) =>
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "UG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = familie },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        });

    /// <summary>Bogen ohne Familie (nur logisch verbindbar) am Punkt, verbunden mit dem Ende von «von» (a) und dem Anfang von «nach» (b).</summary>
    static void Bogen(Projekt p, string kz, string system, double[] punkt, string von, string vonAnschluss, string nach, string nachAnschluss)
    {
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "UG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) },
        });
        Verbinden(p, von, vonAnschluss, kz, "a");
        Verbinden(p, kz, "b", nach, nachAnschluss);
    }

    static void Verbinden(Projekt p, string a, string an, string b, string bn) =>
        A(p, "verbinden", new JsonObject { ["von"] = new JsonObject { ["element"] = a, ["anschluss"] = an }, ["nach"] = new JsonObject { ["element"] = b, ["anschluss"] = bn } });

    static Plangrafik Plan(Projekt p, string ansicht) => Grafik.Plan(p, ansicht);

    static bool Hat(Plangrafik g, Projekt p, string kz) => g.Teile.Any(t => t.Id == Hole(p, kz).Id);

    static List<Primitiv> Texte(Plangrafik g) => g.Teile.Where(t => t.Id is null).SelectMany(t => t.Primitive).Where(x => x.Art == "text" && !x.Stil.NurBildschirm).ToList();

    static List<string> Magenta(Plangrafik g) => Texte(g).Where(x => x.Stil.Farbe == EinlageGrafik.Magenta).Select(x => x.Text!).ToList();

    /// <summary>Haus mit Einlagen in der Decke: R-1 frei endend im Bauteil; R-1 → BO-1 → R-V senkrecht hoch (B.n.o); R-6 → BO-6 → R-V6
    /// hinunter (B.n.u); R-2 → BO-2 (Knick 90°) → R-3 bis an den Plattenrand y = 8000 (Schalungsschoner); R-4/R-5 mit zwei Steigleitungen
    /// 300 mm auseinander (2x B.n.o); alle SW (DN 100 ohne Serie).</summary>
    static Projekt MitEinlagen()
    {
        var p = Haus();
        Rohr(p, "R-1", "SW", [1234.4, 2000, -150], [5000, 2000, -150]);
        Rohr(p, "R-V", "SW", [5000, 2000, -150], [5000, 2000, 900]);
        Bogen(p, "BO-1", "SW", [5000, 2000, -150], "R-1", "ende", "R-V", "anfang");
        Rohr(p, "R-6", "SW", [2000, 4000, -150], [3000, 4000, -150]);
        Rohr(p, "R-V6", "SW", [3000, 4000, -150], [3000, 4000, -1500]);
        Bogen(p, "BO-6", "SW", [3000, 4000, -150], "R-6", "ende", "R-V6", "anfang");
        Rohr(p, "R-2", "SW", [2000, 6000, -150], [4000, 6000, -150]);
        Rohr(p, "R-3", "SW", [4000, 6000, -150], [4000, 8000, -150]);
        Bogen(p, "BO-2", "SW", [4000, 6000, -150], "R-2", "ende", "R-3", "anfang");
        Rohr(p, "R-4", "SW", [6000, 2000, -150], [7000, 2000, -150]);
        Rohr(p, "R-V4", "SW", [7000, 2000, -150], [7000, 2000, 900]);
        Bogen(p, "BO-4", "SW", [7000, 2000, -150], "R-4", "ende", "R-V4", "anfang");
        Rohr(p, "R-5", "SW", [6000, 2300, -150], [7000, 2300, -150]);
        Rohr(p, "R-V5", "SW", [7000, 2300, -150], [7000, 2300, 900]);
        Bogen(p, "BO-5", "SW", [7000, 2300, -150], "R-5", "ende", "R-V5", "anfang");
        Rohr(p, "R-F", "SW", [1000, 7000, -2500], [6000, 7000, -2500]);   // frei im UG
        Rohr(p, "R-G", "SW", [2000, 3000, -3600], [6000, 3000, -3600]);   // im Erdreich unter der Bodenplatte
        return p;
    }

    // ------------------------------------------------------------------ 1. Grundriss-Filter

    [Fact]
    public void Filter_Standard_OhneEinlagenUndErdreich_SchnittUnd3DMit()
    {
        var p = MitEinlagen();
        var vs = Verlegung.Von(p);
        Assert.Equal(Verlegungsart.Einlage, vs.Wirksam(Hole(p, "R-1").Id));
        Assert.Equal(Verlegungsart.Einlage, vs.Wirksam(Hole(p, "BO-2").Id));
        Assert.Equal(Verlegungsart.Erdreich, vs.Wirksam(Hole(p, "R-G").Id));
        Assert.Equal(Verlegungsart.Frei, vs.Wirksam(Hole(p, "R-V").Id));   // 150 von 1050 mm im Beton

        foreach (var darstellung in new string?[] { null, "standard", "huelle" })
        {
            var kz = "AN-T-" + (darstellung ?? "ohne");
            Ansicht(p, kz, "UG", darstellung, detail: "fein");
            var g = Plan(p, kz);
            foreach (var weg in new[] { "R-1", "R-2", "R-3", "BO-2", "R-G" }) Assert.False(Hat(g, p, weg), $"{darstellung}: {weg} im Grundriss");
            Assert.True(Hat(g, p, "R-F"), $"{darstellung}: R-F fehlt");
            Assert.True(Hat(g, p, "R-V"), $"{darstellung}: R-V fehlt");
            // keine Fangpunkte und kein Leitungstext der Einlagen (Detailgrad fein beschriftet R-F mit «110», B9b)
            Assert.DoesNotContain(g.Teile.SelectMany(t => t.Fangpunkte ?? []), q => Math.Abs(q[0] - 1234.4) < 1e-6 && Math.Abs(q[1] - 2000) < 1e-6);
            Assert.Single(g.Teile.SelectMany(t => t.Primitive), x => x.Art == "text" && x.Text == "110");
        }
        // Schnitt und 3D zeigen sie weiter
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "SN-1", ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["blick"] = "links", ["tiefe"] = 1000 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([3000, -1000], [3000, 9000]) },
        });
        var schnitt = Grafik.Kontext(p, Hole(p, "SN-1"));
        Assert.True(Grafik.Sichtbar(schnitt, Hole(p, "R-1")));
        Assert.True(Hat(Plan(p, "SN-1"), p, "R-1"));
        Assert.Contains(Grafik.Koerper(p), k => k.Id == Hole(p, "R-1").Id);
        Assert.Contains(Grafik.Koerper(p), k => k.Id == Hole(p, "R-G").Id);
    }

    [Fact]
    public void Filter_VonHandFrei_ErscheintWiederImGrundriss_UndNichtMehrImEinlageplan()
    {
        var p = MitEinlagen();
        Ansicht(p, "EL", "UG", "einlagen");
        Assert.False(Hat(Plan(p, "AN-UG"), p, "R-1"));
        Assert.True(Hat(Plan(p, "EL"), p, "R-1"));
        A(p, "element_aendern", new JsonObject { ["element"] = "R-1", ["eigenschaften"] = new JsonObject { ["verlegung"] = new JsonObject { ["wert"] = "frei", ["quelle"] = Quelle } } });
        Assert.True(Hat(Plan(p, "AN-UG"), p, "R-1"));
        Assert.False(Hat(Plan(p, "EL"), p, "R-1"));
    }

    // ------------------------------------------------------------------ 2. Darstellung «einlagen»

    [Fact]
    public void Einlagen_Decke_NurPlanElementeKraeftig_HintergrundGrau_OhneLegende()
    {
        var p = MitEinlagen();
        Ansicht(p, "EL", "UG", "einlagen");   // einlagen ohne Angabe = decke
        var g = Plan(p, "EL");
        foreach (var kz in new[] { "R-1", "R-2", "R-3", "R-4", "R-5", "R-6", "BO-1", "BO-2", "BO-4", "BO-5", "BO-6" }) Assert.True(Hat(g, p, kz), kz + " fehlt");
        foreach (var kz in new[] { "R-V", "R-V4", "R-V5", "R-V6", "R-F", "R-G" }) Assert.False(Hat(g, p, kz), kz + " gehört nicht in den Plan");
        // kräftig: Leitung ohne Mass mit dem dicken Stift in der Medienfarbe (SW), nie gestrichelt
        var r1 = g.Teile.Single(t => t.Id == Hole(p, "R-1").Id).Primitive.Where(x => !x.Stil.NurBildschirm).ToList();
        // DN 100 ohne Serie: Band 0.6 × DN = 60 mm in der Medienfarbe (breiter als jeder Stift), nie gestrichelt
        var sw = p.Katalog.Medien["SW"].FarbeHex;
        Assert.Contains(r1, x => x.Art == "polygon" && x.Stil.Fuellung == sw);
        Assert.DoesNotContain(r1, x => x.Stil.Linie == "strich");
        Assert.All(r1.Where(x => x.Art is "linie" or "polylinie"), x => Assert.Equal(Stifte.Dick, x.Stil.Stift));
        // Hintergrund grau: Wände (gemeinsamer Umriss) grau
        var waende = g.Teile.Where(t => t.Ebene == "wand").SelectMany(t => t.Primitive).Where(x => !x.Stil.NurBildschirm).ToList();
        Assert.NotEmpty(waende);
        Assert.All(waende, x => Assert.Equal(EinlageGrafik.Grau, x.Stil.Farbe));
        Assert.DoesNotContain(waende, x => x.Stil.Fuellung == "schnitt");
        // keine Legende
        Assert.DoesNotContain(Texte(g), x => x.Text!.Contains("Legende") || x.Text.Contains("SIA 400"));
    }

    [Fact]
    public void Einlagen_JeBauart_DeckeBodenplatteWand()
    {
        var p = MitEinlagen();
        Rohr(p, "R-BP", "SW", [1000, 7500, -3200], [3000, 7500, -3200]);   // in der Bodenplatte
        Rohr(p, "R-W", "SW", [2000, 4000, 1500], [6000, 4000, 1500]);      // längs in der Betonwand W-1 (EG)
        Ansicht(p, "EL-D", "UG", "einlagen", "decke");
        Ansicht(p, "EL-BP", "UG", "einlagen", "bodenplatte");
        Ansicht(p, "EL-W", "EG", "einlagen", "wand");
        var d = Plan(p, "EL-D"); var bp = Plan(p, "EL-BP"); var w = Plan(p, "EL-W");
        Assert.True(Hat(d, p, "R-1")); Assert.False(Hat(d, p, "R-BP")); Assert.False(Hat(d, p, "R-W"));
        Assert.True(Hat(bp, p, "R-BP")); Assert.False(Hat(bp, p, "R-1")); Assert.False(Hat(bp, p, "R-W"));
        Assert.True(Hat(w, p, "R-W")); Assert.False(Hat(w, p, "R-1")); Assert.False(Hat(w, p, "R-BP"));
        Assert.Equal("Einlagen Bodenplatte UG", EinlageGrafik.Schluessel(p.Modell, Hole(p, "EL-BP")));
        Assert.Equal("Einlagen Wände EG", EinlageGrafik.Schluessel(p.Modell, Hole(p, "EL-W")));
        // die Bodenplatten-Einlage liegt unter dem Boden des UG und ist trotzdem nicht gestrichelt
        Assert.All(bp.Teile.Single(t => t.Id == Hole(p, "R-BP").Id).Primitive.Where(x => x.Art is "linie" or "polylinie"), x => Assert.Equal("voll", x.Stil.Linie));
    }

    [Fact]
    public void Einlagen_AussparungenNurDesBetroffenenBauteils_GrauOhneKennfarbe()
    {
        var p = MitEinlagen();
        void Aus(string kz, string bauteil, double[] punkt) => A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = kz, ["geschoss"] = "UG",
            ["eigenschaften"] = new JsonObject { ["form"] = "rechteckig", ["b"] = 200, ["h"] = 200, ["tiefe"] = 300, ["achse"] = new JsonArray(0, 0, 1), ["status"] = "vorgeschlagen" },
            ["bezuege"] = new JsonObject { ["bauteil"] = Hole(p, bauteil).Id },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) },
        });
        Aus("AUS-D", "D-EG", [8000, 6000, -150]);
        Aus("AUS-B", "D-BP", [8000, 4000, -3200]);
        Ansicht(p, "EL-D", "UG", "einlagen", "decke");
        Ansicht(p, "EL-BP", "UG", "einlagen", "bodenplatte");
        Ansicht(p, "GL", "UG", "grundleitungen");
        var d = Plan(p, "EL-D");
        Assert.True(Hat(d, p, "AUS-D")); Assert.False(Hat(d, p, "AUS-B"));
        var bp = Plan(p, "EL-BP");
        Assert.True(Hat(bp, p, "AUS-B")); Assert.False(Hat(bp, p, "AUS-D"));
        var gl = Plan(p, "GL");
        Assert.False(Hat(gl, p, "AUS-D")); Assert.False(Hat(gl, p, "AUS-B"));
        // grau ohne Kennfarbe (gedruckt); im gewöhnlichen Grundriss mit Kennfarbe gelb
        var gedruckt = d.Teile.Where(t => t.Id == Hole(p, "AUS-D").Id).SelectMany(t => t.Primitive).Where(x => !x.Stil.NurBildschirm).ToList();
        Assert.NotEmpty(gedruckt);
        Assert.DoesNotContain(gedruckt, x => x.Stil.Fuellung is AussparungGrafik.Gelb or AussparungGrafik.Braun);
        Assert.All(gedruckt, x => Assert.Equal(EinlageGrafik.Grau, x.Stil.Farbe));
        Assert.Contains(Plan(p, "AN-UG").Teile.Where(t => t.Id == Hole(p, "AUS-D").Id).SelectMany(t => t.Primitive), x => x.Stil.Fuellung == AussparungGrafik.Gelb);
    }

    // ------------------------------------------------------------------ Bausteine

    [Fact]
    public void Bausteine_BnoBnu_2x_Schalungsschoner_OffenesEndeOhneBaustein()
    {
        var p = MitEinlagen();
        Ansicht(p, "EL", "UG", "einlagen");
        var d = EinlageGrafik.Daten(Grafik.Kontext(p, Hole(p, "EL")));
        string? B(string kz, string an) => d.Enden.Single(x => x.Element.Kennzeichen == kz && x.Anschluss == an).Baustein;
        Assert.Equal(EinlageGrafik.BogenOben, B("BO-1", "b"));                // R-V steigt senkrecht
        Assert.Equal(EinlageGrafik.BogenUnten, B("BO-6", "b"));               // R-V6 fällt
        Assert.Equal(EinlageGrafik.Schalungsschoner, B("R-3", "ende"));       // offen am Plattenrand y = 8000
        Assert.Null(B("R-1", "anfang"));                                      // offen 1234 mm vom Rand, waagrecht: kein Baustein
        Assert.Null(B("R-2", "anfang"));
        var texte = Magenta(Plan(p, "EL"));
        Assert.Contains("2x B.n.o", texte);                                   // BO-4 und BO-5, 300 mm auseinander
        Assert.Single(texte, t => t == "B.n.o");                              // BO-1 allein (2000 mm von BO-4)
        Assert.Contains("B.n.u", texte);
        Assert.Contains("Schalungsschoner", texte);
        // je Ziel ein Pfeil: «2x B.n.o» hat zwei Führungslinien mit Pfeilspitze in BO-4 und BO-5
        var teil = Plan(p, "EL").Teile.Single(t => t.Primitive.Any(x => x.Text == "2x B.n.o"));
        foreach (var y in new[] { 2000.0, 2300.0 })
            Assert.True(teil.Primitive.Count(x => x.Art == "linie" && x.Punkte.Any(q => Math.Abs(q[0] - 7000) < 1e-6 && Math.Abs(q[1] - y) < 1e-6)) >= 3, $"Pfeil auf (7000, {y})");
    }

    [Fact]
    public void Bausteine_Wasser_MediumUndAussendurchmesser_AusSerieSonstLeitungDa()
    {
        var p = Haus();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }));   // FAM-1, DN 15: da 20 (FIKTIV)
        RohrSerie(p, "R-K", "KW", "FAM-1", "DN 15", [1000, 1000, -150], [2500, 1000, -150]);
        Rohr(p, "R-KV", "KW", [2500, 1000, -150], [2500, 1000, 900]);
        Bogen(p, "BO-K", "KW", [2500, 1000, -150], "R-K", "ende", "R-KV", "anfang");
        RohrDa(p, "R-K2", "KW", 16, [1000, 5000, -150], [2500, 5000, -150]);   // ohne Serie, aber mit Leitung.da (FIKTIV 16)
        RohrDa(p, "R-K2V", "KW", 16, [2500, 5000, -150], [2500, 5000, 900]);
        Bogen(p, "BO-K2", "KW", [2500, 5000, -150], "R-K2", "ende", "R-K2V", "anfang");
        RohrDa(p, "R-K3", "KW", null, [1000, 7000, -150], [2500, 7000, -150]);   // weder Serie noch da
        RohrDa(p, "R-K3V", "KW", null, [2500, 7000, -150], [2500, 7000, 900]);
        Bogen(p, "BO-K3", "KW", [2500, 7000, -150], "R-K3", "ende", "R-K3V", "anfang");
        Ansicht(p, "EL", "UG", "einlagen");
        var texte = Magenta(Plan(p, "EL"));
        // gemeinsame da-Quelle (B9b, Zusatz Hauptlauf 19.10.2026): Serie, sonst Leitung.da; das Ø bleibt in den Bausteinen (Christians S09)
        Assert.Contains("KW: B.n.o Ø20", texte);   // Serie FAM-1, DN 15
        Assert.Contains("KW: B.n.o Ø16", texte);   // Leitung.da
        Assert.Contains("KW: B.n.o", texte);       // weder Serie noch da: ohne Ø
    }

    [Fact]
    public void Bausteine_Endgeraet_VerbundenOderOffenInDerNaehe_MitNamen()
    {
        var p = Haus();
        void Ablauf(string kz, string name, double[] punkt) => A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Bodenablauf", ["kennzeichen"] = kz, ["name"] = name, ["system"] = "SW", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) },
        });
        Ablauf("BA-1", "Bodenablauf:BWA Test horizontal:12345", [8500, 5000, 0]);   // Revit-Name: der Typ zählt
        Ablauf("BA-2", "BWA nahe", [8000, 7000, 0]);
        Ablauf("BA-3", "BWA weit", [3000, 7000, 0]);
        Rohr(p, "R-7", "SW", [8500, 5000, -150], [8500, 3000, -150]);
        Verbinden(p, "BA-1", "sw", "R-7", "anfang");
        Rohr(p, "R-8", "SW", [6000, 7000, -150], [7800, 7000, -150]);   // offenes Ende 200 mm waagrecht und 150 mm unter BA-2
        Rohr(p, "R-9", "SW", [1000, 7000, -150], [2600, 7000, -150]);   // offenes Ende 400 mm von BA-3: zu weit
        Ansicht(p, "EL", "UG", "einlagen");
        var g = Plan(p, "EL");
        var texte = Magenta(g);
        Assert.Contains("BWA Test horizontal", texte);
        Assert.Contains("BWA nahe", texte);
        Assert.DoesNotContain("BWA weit", texte);
        // die Endgeräte an den Enden erscheinen (Geschoss EG, trotzdem im Plan des UG), andere nicht
        Assert.True(Hat(g, p, "BA-1")); Assert.True(Hat(g, p, "BA-2")); Assert.False(Hat(g, p, "BA-3"));
        Assert.Equal("BWA Test horizontal", EinlageGrafik.Anzeigename(p.Modell, Hole(p, "BA-1")));
    }

    // ------------------------------------------------------------------ Massketten

    [Fact]
    public void Massketten_MagentaGanzeCm_ZuEndenUndKnicken()
    {
        var p = MitEinlagen();
        Ansicht(p, "EL", "UG", "einlagen");
        var g = Plan(p, "EL");
        // Zahlen der Massketten: magenta, nicht fett (die Bausteine sind fett)
        var zahlen = Texte(g).Where(x => x.Stil.Farbe == AussparungMassketten.Farbe && x.Fett != true).Select(x => x.Text!).ToList();
        Assert.NotEmpty(zahlen);
        Assert.All(zahlen, z => Assert.Matches(new Regex(@"^\d+$"), z));       // ganze cm
        // Handrechnung (Fixpunkte Innenflächen x = 100 / 9900, y = 100 / 7900, Aussenflächen −100 / 10100 bzw. −100 / 8100):
        // x ab 100, Punkte auf y = 2000: R-1 Anfang 1234.4 und BO-1 5000 → eine Kette, Lagen ab dem Fixpunkt gerundet 113 und 490 cm →
        // Felder «113» und «377» (die Kette geht auf); x ab 9900: BO-4/R-4 7000 und 6000 → Felder «290» und «100»; R-3 Ende (4000, 8000)
        // liegt in der Nordwand: x ab der Aussenfläche −100 → «410»; y ab 100 bei x 5000 … 7000: 2000 und 2300 → «190» und «30».
        Assert.Contains(g.Teile, t => t.Primitive.Any(x => x.Text == "113") && t.Primitive.Any(x => x.Text == "377"));
        foreach (var z in new[] { "290", "100", "410", "190", "30" }) Assert.Contains(z, zahlen);
        Assert.DoesNotContain("390", zahlen);   // der Knick BO-2 (4000, 6000) wird ohne masseKnicke nicht bemasst (B10b)
        var d = EinlageGrafik.Daten(Grafik.Kontext(p, Hole(p, "EL")));
        Assert.Single(d.Knicke, k => k.Element.Kennzeichen == "BO-2");
        Assert.DoesNotContain(d.Knicke, k => k.Element.Kennzeichen is "BO-1" or "BO-4");   // Bogen in die Senkrechte: kein Knick im Grundriss
        Assert.All(d.Enden, en => Assert.True(en.Austritt, en.Element.Kennzeichen));       // hier ist jedes Ende ein Austritt (Baustein oder offen)
    }

    /// <summary>Kopie der Kataloge mit dem Ansicht-Feld masseKnicke (bool) – den Eintrag im echten Katalog setzt der Hauptlauf (B10b).</summary>
    static Katalog KatalogMitMasseKnicke()
    {
        var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b10b_kataloge_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ziel);
        foreach (var f in Directory.GetFiles(Katalog.StandardOrdner())) File.Copy(f, Path.Combine(ziel, Path.GetFileName(f)));
        var klassen = Path.Combine(ziel, "klassen.json");
        var text = File.ReadAllText(klassen);
        if (!text.Contains("\"masseKnicke\""))
        {
            const string anker = "{ \"name\": \"legende\", \"typ\": \"bool\"";
            Assert.Contains(anker, text);
            text = text.Replace(anker, "{ \"name\": \"masseKnicke\", \"typ\": \"bool\", \"beschreibung\": \"Test B10b: Massketten auch zu Knicken\" }, " + anker);
            File.WriteAllText(klassen, text);
        }
        try { return Katalog.Laden(ziel); }
        finally { Directory.Delete(ziel, true); }
    }

    [Fact]
    public void Massketten_NurZuAustritten_KnickeNurMitOptionMasseKnicke()
    {
        // FIKTIV: Decke über UG wie das Testhaus, Betonwände im UG (Innenflächen x = 100 / 9900, y = 100 / 7900); R-A von (2345, 2000) nach
        // (4950, 2000), Bogen BO-K ohne Familie in (5000, 2000) (Knick 90°), R-B von (5000, 2050) nach (5000, 5678); beide Enden offen.
        var kat = KatalogMitMasseKnicke();
        var p = Projekt.Anlegen(kat, null, "B10b masseKnicke (fiktiv)", "T", "pc");
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" });
        foreach (var (kz, kote) in new[] { ("UG", -3000.0), ("EG", 0.0) })
            A(p, "element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = 3000 } });
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } });
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-EG", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 300" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 0], [10000, 0, 0], [10000, 8000, 0], [0, 8000, 0]), ["dicke"] = 300 },
        });
        A(p, "wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "UG", ["punkte"] = Pts([0, 0], [10000, 0], [10000, 8000], [0, 8000]), ["dicke"] = 200, ["geschlossen"] = true, ["material"] = "beton",
        });
        Rohr(p, "R-A", "SW", [2345, 2000, -150], [4950, 2000, -150]);
        Rohr(p, "R-B", "SW", [5000, 2050, -150], [5000, 5678, -150]);
        Bogen(p, "BO-K", "SW", [5000, 2000, -150], "R-A", "ende", "R-B", "anfang");
        Ansicht(p, "EL", "UG", "einlagen");
        static List<string> Zahlen(Plangrafik g) => g.Teile.Where(t => t.Id is null).SelectMany(t => t.Primitive)
            .Where(x => x.Art == "text" && x.Stil.Farbe == AussparungMassketten.Farbe && x.Fett != true).Select(x => x.Text!).ToList();
        // ohne Option: R-A Anfang x ab 100 → «225» (224.5 aufgerundet); R-B Ende x 5000 (gleich weit von 100 und 9900 → 100) «490»,
        // y bis 7900 → «222»; y ab 100 bei 2000 → «190»
        var ohne = Zahlen(Plan(p, "EL"));
        foreach (var z in new[] { "225", "490", "222", "190" }) Assert.Contains(z, ohne);
        Assert.DoesNotContain("265", ohne);
        Assert.False(EinlageGrafik.MasseKnicke(Hole(p, "EL")));
        // mit masseKnicke: der Knick (5000, 2000) kommt in die Kette ab x = 100 auf y = 2000 → Felder «225» und «265» (490 − 225)
        A(p, "element_aendern", new JsonObject { ["element"] = "EL", ["eigenschaften"] = new JsonObject { [EinlageGrafik.OptionMasseKnicke] = true } });
        Assert.True(EinlageGrafik.MasseKnicke(Hole(p, "EL")));
        var mit = Zahlen(Plan(p, "EL"));
        Assert.Contains("265", mit);
        Assert.Contains("225", mit);
    }

    [Fact]
    public void Einlagen_AusgezogenUnabhaengigVomMedium_FormteileLueckenlos()
    {
        // FIKTIV: Regenwasser (Linienart des Mediums gestrichelt) als Einlage; die Leitungen enden 100 mm vor dem Bogen BO-L (wie im IFC
        // mit Einbaulänge), BO-L ohne Familie in (3000, 1000); R-V senkrecht hoch (B.n.o-Marke).
        var p = Haus();
        A(p, "element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "RW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Regenwasser" } });
        Assert.Equal("gestrichelt", p.Katalog.Medien["RW"].Linie);
        Rohr(p, "R-L1", "RW", [1000, 1000, -150], [2900, 1000, -150]);
        Rohr(p, "R-L2", "RW", [3000, 1100, -150], [3000, 3000, -150]);
        Bogen(p, "BO-L", "RW", [3000, 1000, -150], "R-L1", "ende", "R-L2", "anfang");
        Rohr(p, "R-V", "RW", [3000, 3000, -150], [3000, 3000, 900]);
        Bogen(p, "BO-V", "RW", [3000, 3000, -150], "R-L2", "ende", "R-V", "anfang");
        Ansicht(p, "EL", "UG", "einlagen");
        Ansicht(p, "AN-T", "UG");
        var g = Plan(p, "EL");
        var rw = p.Katalog.Medien["RW"].FarbeHex;
        // Leitung: ein Band je Gerade (DN 100 ohne Serie: 60 mm breit), nicht in Striche zerlegt, keine Strichlinie
        var l1 = g.Teile.Single(t => t.Id == Hole(p, "R-L1").Id).Primitive;
        Assert.Single(l1, x => x.Art == "polygon" && x.Stil.Fuellung == rw);
        Assert.DoesNotContain(g.Teile.SelectMany(t => t.Primitive), x => x.Stil.Linie == "strich" && x.Stil.Farbe == rw);
        // Bogen ohne Familie: Verbinder (3000, 1000) → (2900, 1000) und → (3000, 1100) in 60 mm Breite, runde Ecke, kein Papierrand
        var bo = g.Teile.Single(t => t.Id == Hole(p, "BO-L").Id).Primitive;
        Assert.Equal(2, bo.Count(x => x.Art == "polygon" && x.Stil.Fuellung == rw));
        var ecke = Assert.Single(bo, x => x.Art == "kreis");
        Assert.Equal((30.0, rw), (ecke.Radius!.Value, ecke.Stil.Fuellung));
        Assert.DoesNotContain(bo, x => x.Stil.Fuellung == "papier");
        var box = Box(bo.Where(x => x.Art == "polygon").SelectMany(x => x.Punkte));
        Nahe([2900, 970, 3030, 1100], box);   // lückenlos bis an beide Leitungsenden
        // Austritt nach oben: Marke in der Medienfarbe, Radius 30 mm (Breite der Leitung), dünner Rand
        Assert.Contains(g.Teile.Where(t => t.Id is null && t.Ebene == "formteil").SelectMany(t => t.Primitive),
            x => x.Art == "kreis" && x.Stil.Fuellung == rw && x.Stil.Stift == Stifte.Duenn && Math.Abs(x.Radius!.Value - 30) < 1e-9
                && Math.Abs(x.Punkte[0][0] - 3000) < 1e-6 && Math.Abs(x.Punkte[0][1] - 3000) < 1e-6);
        // im gewöhnlichen Grundriss fehlen sie (Filter)
        Assert.False(Hat(Plan(p, "AN-T"), p, "R-L1"));
    }

    // ------------------------------------------------------------------ Beschriftung

    [Fact]
    public void Beschriftung_SchwarzMitFuehrungslinie_JeLaufEinmal()
    {
        var p = MitEinlagen();
        Ansicht(p, "EL", "UG", "einlagen");
        var g = Plan(p, "EL");
        var nw = g.Teile.Where(t => t.Id is null && t.Primitive.Any(x => x.Text == "110")).ToList();
        // Läufe: R-1, R-6, R-2+R-3 (über BO-2), R-4, R-5 → fünf Beschriftungen
        Assert.Equal(5, nw.Count);
        Assert.All(nw, t =>
        {
            Assert.Null(t.Primitive.Single(x => x.Art == "text").Stil.Farbe);   // schwarz (Planfarbe)
            Assert.Single(t.Primitive, x => x.Art == "linie");                  // Führungslinie
        });
    }

    // ------------------------------------------------------------------ 3. Darstellung «grundleitungen»

    [Fact]
    public void Grundleitungen_SohleMitInnendurchmesser_AchseOhne_MitHinweis()
    {
        var p = Haus();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_abwasserserie") }));   // FAM-1
        // dieselbe Serie ohne Innendurchmesser (FIKTIV): leitung.innen entfernt
        var ohne = Modul3Hilfe.Familie("test_abwasserserie");
        ohne["id"] = "fam-test-abwasserserie-ohne-innen";
        ohne["name"] = "Testabwasserserie ohne Innendurchmesser (fiktiv)";
        ((JsonObject)ohne["leitung"]!).Remove("innen");
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b10_test_" + Guid.NewGuid().ToString("N")[..8] + ".familie.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllText(datei, ohne.ToJsonString());
        try { Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei })); }   // FAM-2
        finally { File.Delete(datei); }
        RohrSerie(p, "G-1", "SW", "FAM-1", "DN 100", [2000, 2000, -3650], [6000, 2000, -3650]);   // S = −3650 − 104/2 = −3702 → «S= -3.70 m ab 0.0»
        RohrSerie(p, "G-2", "SW", "FAM-2", "DN 100", [2000, 4000, -3620], [6000, 4000, -3620]);   // Serie ohne di → «A= -3.62 m ab 0.0»
        Rohr(p, "G-3", "SW", [2000, 6000, -3600], [6000, 6000, -3600]);                            // ohne Serie → «A= -3.60 m ab 0.0»
        Ansicht(p, "GL", "UG", "grundleitungen");
        var g = Plan(p, "GL");
        foreach (var kz in new[] { "G-1", "G-2", "G-3" }) Assert.True(Hat(g, p, kz));
        var texte = Texte(g).Select(x => x.Text!).ToList();
        Assert.Equal(2, texte.Count(t => t == "S= -3.70 m ab 0.0"));   // beide Enden von G-1
        Assert.Equal(2, texte.Count(t => t == "A= -3.62 m ab 0.0"));
        Assert.Equal(2, texte.Count(t => t == "A= -3.60 m ab 0.0"));
        Assert.DoesNotContain(texte, t => t.StartsWith("S= -3.6"));      // nie aus dem Aussendurchmesser geraten
        Assert.Contains(EinlageGrafik.HinweisAchse, texte);
        // Rohrbeschriftung immer aus der gemeinsamen Funktion (B9/B9b), hier nicht zusammengesetzt (B10b)
        foreach (var kz in new[] { "G-1", "G-2", "G-3" }) Assert.Contains(Leitungsbezeichnung.Text(p, Hole(p, "GL"), Hole(p, kz))!, texte);
        Assert.Contains("110", texte);                                   // G-3 ohne Serie: Abwasser mit Aussendurchmesser (B9b, Entscheid Christian 19.10.2026)
        // Sohlenkote schwarz mit gefülltem Pfeil
        var kote = g.Teile.First(t => t.Primitive.Any(x => x.Text == "S= -3.70 m ab 0.0"));
        Assert.Contains(kote.Primitive, x => x.Art == "polygon" && x.Stil.Fuellung == "schnitt");
        // Format und Rundung (Handrechnung)
        Assert.Equal("S= -6.37 m ab 0.0", EinlageGrafik.KotenText("S", -6370.4));
        Assert.Equal("S= +0.12 m ab 0.0", EinlageGrafik.KotenText("S", 115));
        Assert.Equal("A= ±0.00 m ab 0.0", EinlageGrafik.KotenText("A", -4));
        // ohne Innendurchmesser keine Sohle: auch nicht im Plan ohne Hinweis
        Assert.Equal("A= -3.62 m ab 0.0", EinlageGrafik.Sohlentext(p, Hole(p, "G-2"), -3620, out var achse));
        Assert.True(achse);
        // im gewöhnlichen Grundriss fehlen die Grundleitungen
        Assert.False(Hat(Plan(p, "AN-UG"), p, "G-1"));
    }

    [Fact]
    public void Grundleitungen_OhneInnendurchmesserLos_KeinHinweis()
    {
        var p = Haus();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_abwasserserie") }));
        RohrSerie(p, "G-1", "SW", "FAM-1", "DN 100", [2000, 2000, -3650], [6000, 2000, -3650]);
        Ansicht(p, "GL", "UG", "grundleitungen");
        Assert.DoesNotContain(EinlageGrafik.HinweisAchse, Texte(Plan(p, "GL")).Select(x => x.Text));
    }

    // ------------------------------------------------------------------ 4. Befehl einlageplaene_anlegen

    [Fact]
    public void Befehl_Trockenlauf_Anlegen_KeineDoppel_Rueckgaengig()
    {
        var p = MitEinlagen();
        string vorher = p.Modell.Pruefsumme();
        var t = Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject(), trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Assert.Contains(t.Hinweise, h => h.StartsWith("Einlagen D.ü.UG: Ansicht EL-D.ü.UG angelegt"));
        Assert.Contains(t.Hinweise, h => h.StartsWith("Grundleitungen UG: Ansicht GL-UG angelegt"));

        var e = Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        var a = Hole(p, "EL-D.ü.UG");
        Assert.Equal(("Ansicht", "grundriss", "einlagen", "decke", 50.0), (a.Klasse, a.Text("art"), a.Text("darstellung"), a.Text("einlagen"), a.Zahl("massstab")));
        Assert.Equal(Hole(p, "UG").Id, a.Geschoss);
        Assert.Equal("Sanitär Einlagen D.ü.UG", a.Name);
        var gl = Hole(p, "GL-UG");
        Assert.Equal(("grundleitungen", "Grundleitungen UG"), (gl.Text("darstellung"), gl.Name));
        var blaetter = p.Modell.DerKlasse("Planblatt").ToList();
        Assert.Equal(2, blaetter.Count);
        var b = blaetter.Single(x => x.Text("titel") == "Sanitär Einlagen D.ü.UG");
        Assert.Equal(("A1", false), (b.Text("format"), Grafik.Bool(b, "hochformat")));
        Assert.Null(b.Text("nummer"));   // kein Muster vorhanden: frei
        Assert.Single(blaetter, x => x.Text("titel") == "Grundleitungen UG");
        Assert.Single(p.Modell.DerKlasse("Ansichtsfenster"), f => f.Bezuege["ansicht"] == a.Id && f.TeilVon == b.Id);
        Assert.Equal("cm", AussparungGrafik.Einheit(p.Modell, a));   // Masse wie die Massketten

        // nochmals: nichts doppelt
        var zwei = Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        Assert.Empty(zwei.Aenderungen);
        Assert.Contains(zwei.Hinweise, h => h.StartsWith("Einlagen D.ü.UG: Ansicht EL-D.ü.UG vorhanden, steht schon auf"));
        Assert.Equal(2, p.Modell.DerKlasse("Planblatt").Count());

        // rückgängig: alles weg
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("EL-D.ü.UG"));
        Assert.Empty(p.Modell.DerKlasse("Planblatt"));
        Assert.Equal(vorher, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Befehl_PlannummerNachMuster_MassstabFormat_Filter_Fehler()
    {
        var p = MitEinlagen();
        A(p, "planblatt_anlegen", new JsonObject { ["nummer"] = "2317 S05", ["titel"] = "Sanitär Grundriss UG" });
        Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["massstab"] = 100, ["format"] = "a0", ["art"] = "einlage" }));
        Assert.Null(p.Modell.Finde("GL-UG"));   // nur Einlagen
        var b = p.Modell.DerKlasse("Planblatt").Single(x => x.Text("titel") == "Sanitär Einlagen D.ü.UG");
        Assert.Equal(("2317 S06", "A0"), (b.Text("nummer"), b.Text("format")));
        Assert.Equal(100.0, Hole(p, "EL-D.ü.UG").Zahl("massstab"));
        Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["geschoss"] = "UG" }));
        Assert.Equal("2317 S07", p.Modell.DerKlasse("Planblatt").Single(x => x.Text("titel") == "Grundleitungen UG").Text("nummer"));
        Assert.Equal("2317 S08", EinlageplaeneAnlegen.Nummer(p.Modell, "S"));
        Assert.Null(EinlageplaeneAnlegen.Nummer(p.Modell, "H"));
        // vorhandene Ansicht mit anderem Kennzeichen wird weiterverwendet
        var q = MitEinlagen();
        Ansicht(q, "MEIN-EL", "UG", "einlagen", "decke");
        Ok(q.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["art"] = "einlage" }));
        Assert.Null(q.Modell.Finde("EL-D.ü.UG"));
        Assert.Single(q.Modell.DerKlasse("Ansichtsfenster"), f => f.Bezuege["ansicht"] == Hole(q, "MEIN-EL").Id);
        // Fehler
        Nein(p.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["format"] = "B5" }));
        Nein(p.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["massstab"] = 0 }));
        Nein(p.Ausfuehren("einlageplaene_anlegen", new JsonObject { ["art"] = "beton" }));
        // ohne Einlagen: Hinweis, keine Änderung
        var leer = Haus();
        var r = Ok(leer.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        Assert.Empty(r.Aenderungen);
        Assert.Contains(r.Hinweise, h => h.StartsWith("Keine Einlagen bzw. Grundleitungen erkannt"));
    }

    [Fact]
    public void Befehl_PlanAlsPdf_MasseInCm()
    {
        var p = MitEinlagen();
        Ok(p.Ausfuehren("einlageplaene_anlegen", new JsonObject()));
        var b = p.Modell.DerKlasse("Planblatt").Single(x => x.Text("titel") == "Sanitär Einlagen D.ü.UG");
        var pdf = Planausgabe.Pdf(p, b.Kennzeichen, unkomprimiert: true);
        Assert.True(pdf.Length > 1000);
        Assert.Contains("Masse in cm", System.Text.Encoding.Latin1.GetString(pdf));
        // zum Ansehen (nur lokal, Temp): beide Blätter des Testhauses
        var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD");
        Directory.CreateDirectory(ordner);
        File.WriteAllBytes(Path.Combine(ordner, "b10_test_einlagen.pdf"), Planausgabe.Pdf(p, b.Kennzeichen));
        File.WriteAllBytes(Path.Combine(ordner, "b10_test_grundleitungen.pdf"), Planausgabe.Pdf(p, p.Modell.DerKlasse("Planblatt").Single(x => x.Text("titel") == "Grundleitungen UG").Kennzeichen));
    }
}
