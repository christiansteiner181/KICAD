using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>HLKS-Übertrag (Modul 1b, B2) mit synthetischen Dateien in IFC4 und IFC2x3 (IfcHlksTestdatei): Klassen,
/// Verlauf, Nennweite nur aus Psets, Anschlüsse aus Ports, Verbindungen, Systeme (auch zerlegte Revit-Systeme),
/// Stränge, Räume, Abgleich einer neuen Fassung.</summary>
public sealed class IfcHlksTests : IDisposable
{
    readonly List<string> ordner = [];
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var o in ordner) try { Directory.Delete(o, true); } catch (IOException) { }
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { }
    }

    string Datei(string schema, IfcHlksSpec spec, string name = "hlks_test.ifc")
    {
        var o = IfcHlksTestdatei.Ordner();
        ordner.Add(o);
        return IfcHlksTestdatei.Schreiben(schema, Path.Combine(o, name), spec);
    }

    static JsonObject NurHlks() => new() { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false };

    (Projekt P, BefehlsErgebnis E) Laden(string schema, IfcHlksSpec spec)
    {
        var p = Leer();
        projekte.Add(p);
        var e = Ok(Uebernehmen(p, Datei(schema, spec), NurHlks()));
        return (p, e);
    }

    static Element Namens(Projekt p, string name) => Assert.Single(p.Modell.Elemente, e => e.Name == name);

    static bool Verbunden(Projekt p, Element a, string an, Element b, string bn) =>
        p.Modell.VerbindungAn(a.Id, an) is { } v && v.Gegenueber(a.Id, an) is { } g && g.Element == b.Id && g.Anschluss.Equals(bn, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ Grundnetz

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Grundnetz_KlassenVerlaufUndVerbindungen(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec());
        Assert.Equal(2, p.Modell.DerKlasse("Rohr").Count());
        Assert.Single(p.Modell.DerKlasse("Bogen"));
        Assert.Single(p.Modell.DerKlasse("WC"));
        Assert.Single(p.Modell.DerKlasse("Kanal"));

        var r1 = Namens(p, "Rohr 1"); var r2 = Namens(p, "Rohr 2"); var bo = Namens(p, "Rohrbogen 90"); var wc = Namens(p, "WC Vorwand");
        // Verlauf aus den Lagen der Ports, anfang (SINK) → ende (SOURCE)
        Assert.Equal("polylinie", r1.Geometrie!.Art);
        Assert.Equal([1900, 0, 500], r1.Geometrie.Punkte[0]);
        Assert.Equal([0, 0, 500], r1.Geometrie.Punkte[1]);
        Assert.Equal([2000, 2000, 500], r2.Geometrie!.Punkte[0]);
        Assert.Equal("rein", r1.AnschlussNamens("anfang")!.Fluss);
        Assert.Equal("raus", r1.AnschlussNamens("ende")!.Fluss);
        // Formteile und Apparate: Punkt = Lage des Produkts
        Assert.Equal([2000, 0, 500], bo.Geometrie!.Punkte[0]);
        Assert.Equal([2000, 2500, 0], wc.Geometrie!.Punkte[0]);
        Assert.Equal(90, bo.Zahl("winkel")!.Value, 1);
        // Verbindungen aus IfcRelConnectsPorts
        Assert.True(Verbunden(p, wc, "sw", r2, "anfang"));
        Assert.Equal(3, p.Modell.Verbindungen.Count);
        Assert.Null(p.Modell.VerbindungAn(wc.Id, "kw"));
        // Fliessrichtung durchgehend: vom WC stromab über Rohr 2 und Bogen in Rohr 1
        var weg = Netz.Folgen(p.Modell, p.Katalog, wc, "stromab").Select(s => s.Element.Name).ToList();
        Assert.Equal(["Rohr 2", "Rohrbogen 90", "Rohr 1"], weg);
        // Kennungen, Geschoss, Herkunft
        var eg = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG");
        Assert.All(new[] { r1, r2, bo, wc }, x => Assert.Equal(eg.Id, x.Geschoss));
        Assert.Equal(22, r1.Kennungen!["ifc"].Length);
        Assert.Equal(p.Modell.DerKlasse("Unterlage").Single().Id, r1.Kennungen["quelle"]);
        Assert.Equal("ifc", r1.Angelegt!.Art);
        Assert.Contains(schema == "IFC4" ? "IfcPipeSegment" : "IfcFlowSegment", r1.Angelegt.Quelle);
        Assert.Contains(e.Hinweise, h => h.StartsWith("HLKS: 3 Leitungen, 1 Formteile, 0 Armaturen, 1 Apparate/Geräte"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Nennweite_NurAusPsets_NieAusDemAussendurchmesser(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec());
        var r1 = Namens(p, "Rohr 1"); var r2 = Namens(p, "Rohr 2");
        Assert.Equal(100, r1.Zahl("dn"));
        Assert.Equal("ifc", r1.Eigenschaft("dn")!.Herkunft.Art);
        Assert.Contains("Pset_PipeSegmentTypeCommon.NominalDiameter", r1.Eigenschaft("dn")!.Herkunft.Quelle);
        Assert.All(r1.Anschluesse, a => Assert.Equal(100, a.Dn));
        // Rohr 2 hat nur ein Kreisprofil r 55: keine Nennweite; der Aussendurchmesser steht (B9b) in da, nie als DN
        Assert.Null(r2.Eigenschaft("dn"));
        Assert.All(r2.Anschluesse, a => Assert.Null(a.Dn));
        Assert.Equal(110, r2.Zahl("da"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Rohre ohne Nennweite") && h.Contains("Ø 110 mm") && h.Contains("nie als DN"));
    }

    // ------------------------------------------------------------------ Aussendurchmesser da (B9b, Entscheid Christian 19.10.2026)

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Aussendurchmesser_AusDemRohrkoerper_MitHerkunft_NieAlsDn(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec());
        var r1 = Namens(p, "Rohr 1"); var r2 = Namens(p, "Rohr 2");
        // kein Pset nennt den Aussendurchmesser → Geometrie der Datei (Kreisprofil r 55 = Ø 110), mit Fundstelle
        Assert.Equal(110, r2.Zahl("da"));
        Assert.Equal("ifc", r2.Eigenschaft("da")!.Herkunft.Art);
        Assert.Contains("IfcCircleProfileDef", r2.Eigenschaft("da")!.Herkunft.Quelle);
        Assert.Contains("kein Normwert", r2.Eigenschaft("da")!.Herkunft.Quelle);
        // Rohr 1 hat DN 100 im Pset und Ø 110 im Körper: beide Werte stehen nebeneinander, keiner wird aus dem anderen gerechnet
        Assert.Equal(100, r1.Zahl("dn"));
        Assert.Equal(110, r1.Zahl("da"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Aussendurchmesser (da) aus dem Rohrkörper"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Aussendurchmesser_AusPsetVorDemKoerper_MitFundstelle(string schema)
    {
        var (p, _) = Laden(schema, new IfcHlksSpec { DaPset = 100 });   // Pset weicht vom Körper (110) ab, damit klar ist, welche Quelle gilt
        var r2 = Namens(p, "Rohr 2");
        Assert.Equal(100, r2.Zahl("da"));
        Assert.Contains("Abmessungen.Aussendurchmesser", r2.Eigenschaft("da")!.Herkunft.Quelle);
        Assert.Null(r2.Eigenschaft("dn"));   // auch aus dem Pset wird keine Nennweite
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Kanal_QuerschnittAusDemProfil(string schema)
    {
        var (p, _) = Laden(schema, new IfcHlksSpec());
        var ka = Assert.Single(p.Modell.DerKlasse("Kanal"));
        Assert.Equal(400, ka.Zahl("breite"));
        Assert.Equal(200, ka.Zahl("hoehe"));
        Assert.Null(ka.Eigenschaft("durchmesser"));
        Assert.Contains("IfcRectangleProfileDef", ka.Eigenschaft("breite")!.Herkunft.Quelle);
        var sys = p.Modell.Finde(ka.System)!;
        Assert.Equal("Zuluft", sys.Text("systemtyp"));
        Assert.All(ka.Anschluesse, a => Assert.Equal("ZUL", a.Medium));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Systeme_SystemtypMediumAnlageJeGewerk(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec());
        var sw = p.Modell.DerKlasse("System").Single(s => s.Text("systemtyp") == "Schmutzwasser");
        Assert.Equal("SW", sw.Text("medium"));
        Assert.Equal("katalog", sw.Eigenschaft("medium")!.Herkunft.Art);
        Assert.Contains("Namensmuster", sw.Eigenschaft("systemtyp")!.Herkunft.Quelle);
        Assert.Equal("SW-Schmutzwasser", sw.Kennzeichen);
        var anlage = p.Modell.Finde(sw.TeilVon)!;
        Assert.Equal("Anlage", anlage.Klasse);
        Assert.Equal("A-SAN", anlage.Kennzeichen);
        Assert.Equal("anlage:sanitaer", anlage.Kennungen!["ifc"]);
        Assert.Equal(2, p.Modell.DerKlasse("Anlage").Count());   // sanitaer und lueftung
        foreach (var n in new[] { "Rohr 1", "Rohr 2", "Rohrbogen 90" })
        {
            var x = Namens(p, n);
            Assert.Equal(sw.Id, x.System);
            Assert.All(x.Anschluesse, a => Assert.Equal("SW", a.Medium));
        }
        // WC: Apparat in genau einem System → System gesetzt; Anschlussmedien bleiben aus dem Katalog
        var wc = Namens(p, "WC Vorwand");
        Assert.Equal(sw.Id, wc.System);
        Assert.Equal("KW", wc.AnschlussNamens("kw")!.Medium);
        Assert.Contains(e.Hinweise, h => h.StartsWith("System «SW Schmutzwasser»") && h.Contains("Schmutzwasser"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code is "P02" or "P03" or "P07" or "P08" or "P10");
    }

    [Fact]
    public void RevitTeile_GleicherBasisname_WerdenEinSystem()
    {
        var (p, e) = Laden("IFC2X3", new IfcHlksSpec { SwSysteme = ["S SW 1", "S SW 2"] });
        var sw = p.Modell.DerKlasse("System").Single(s => s.Text("systemtyp") == "Schmutzwasser");
        Assert.Equal("S SW", sw.Name);
        Assert.Equal("S-SW", sw.Kennzeichen);
        Assert.Equal("system:S SW", sw.Kennungen!["ifc"]);
        Assert.Contains("2 Teile", sw.Angelegt!.Quelle);
        foreach (var n in new[] { "Rohr 1", "Rohr 2", "Rohrbogen 90", "WC Vorwand" }) Assert.Equal(sw.Id, Namens(p, n).System);
        Assert.Contains(e.Hinweise, h => h.StartsWith("System «S SW» (2 Teile zusammengefasst) → Schmutzwasser"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void OhneSystemtyp_KeinSystem_MitgliederOhneSystem_Hinweis(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec { MitAbgas = true });
        Assert.DoesNotContain(p.Modell.DerKlasse("System"), s => s.Name == "Abgas");
        var ab = Namens(p, "Abgasrohr");
        Assert.Null(ab.System);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Ohne Systemtyp, darum kein System angelegt") && h.Contains("«Abgas»"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Leitungsbauteile ohne System (ihr IFC-System hat keinen Systemtyp)"));
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P02" && b.Element == ab.Kennzeichen);
    }

    [Fact]
    public void Strang_AusIfcDistributionCircuit_TeilVonSystem()
    {
        var (p, _) = Laden("IFC4", new IfcHlksSpec { MitStrang = true });
        var st = Assert.Single(p.Modell.DerKlasse("Strang"));
        var sw = p.Modell.DerKlasse("System").Single(s => s.Text("systemtyp") == "Schmutzwasser");
        Assert.Equal("Strang SW 1", st.Name);
        Assert.Equal(sw.Id, st.TeilVon);
        foreach (var n in new[] { "Rohr 1", "Rohrbogen 90" })
        {
            var x = Namens(p, n);
            Assert.Equal(st.Id, x.TeilVon);
            Assert.Equal(sw.Id, x.System);
        }
        Assert.Null(Namens(p, "Rohr 2").TeilVon);
        Assert.Single(Netz.Verbraucher(p.Modell, p.Katalog, st).Gesamt, x => x.Klasse == "WC");
    }

    // ------------------------------------------------------------------ Klassen ohne PredefinedType, Namensmuster

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Formteile_OhnePredefinedType_KlasseAusDenPorts(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec { MitFormteilen = true });
        var t = Namens(p, "Fitting A"); var g = Namens(p, "Fitting B"); var k = Namens(p, "Fitting C");
        Assert.Equal("TStueck", t.Klasse);
        Assert.Equal("Reduktion", g.Klasse);
        Assert.Equal("Bogen", k.Klasse);
        Assert.Equal(90, k.Zahl("winkel")!.Value, 1);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Klasse aus der Lage der Ports: TStueck"));
        // Abzweig «c» aus der Lage: der Port quer zu den beiden anderen
        Assert.True(Verbunden(p, t, "c", Namens(p, "Abzweigrohr"), "anfang"));
        // ohne FlowDirection bleibt die Richtung offen
        Assert.All(Namens(p, "Abzweigrohr").Anschluesse, a => Assert.Equal("beide", a.Fluss));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Apparat_OhnePortsUndOhneTypwert_MehrdeutigImHintergrund(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec { WcOhnePorts = true });
        Assert.Empty(p.Modell.DerKlasse("WC"));
        Assert.DoesNotContain(p.Modell.Elemente, x => x.Name == "Objekt-109");
        Assert.Contains(e.Hinweise, h => h.StartsWith("Nicht übernommen (1×, im Hintergrund): mehrdeutig (WC, Waschtisch"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(1, ul.Eigenschaft("uebernommen")!.Inhalt!["nichtVerstanden"]![schema == "IFC4" ? "IfcSanitaryTerminal" : "IfcFlowTerminal"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Verteiler_AusDemNamen_ZusaetzlicheAnschluesse(string schema)
    {
        var (p, e) = Laden(schema, new IfcHlksSpec { MitVerteiler = true });
        var v = Assert.Single(p.Modell.DerKlasse("Verteiler"));
        Assert.Equal(8, v.Anschluesse.Count);
        Assert.Equal(["vl_ein", "rl_aus", "vl_1", "rl_1", "vl_2", "rl_2", "abgang_1", "abgang_2"], v.Anschluesse.Select(a => a.Name));
        // Ohne FlowDirection und Medium: Richtung offen
        Assert.All(v.Anschluesse, a => Assert.Equal("beide", a.Fluss));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Klasse aus dem Namen: Verteiler (IfcBuildingElementProxy)"));
    }

    [Fact]
    public void NovaStil_PortsOhneRichtung_LageAusDemKoerper()
    {
        var (p, e) = Laden("IFC4", new IfcHlksSpec { PortsUeberNests = false, MitRichtung = false, NovaLagen = true });
        var r1 = Namens(p, "Rohr 1"); var bo = Namens(p, "Rohrbogen 90"); var wc = Namens(p, "WC Vorwand");
        Assert.Equal(3, p.Modell.Verbindungen.Count);
        // Alle Produkte liegen auf dem Geschossursprung: Punkt aus dem Körper (Verbraucher: Mitte unten)
        Assert.Equal([1950, -50, 500], bo.Geometrie!.Punkte[0]);
        Assert.Equal([2000, 2500, 0], wc.Geometrie!.Punkte[0]);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Punkt aus der Mitte des Körpers"));
        // Ohne FlowDirection: Leitungsenden «beide», Hinweis
        Assert.All(r1.Anschluesse, a => Assert.Equal("beide", a.Fluss));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Fliessrichtung offen an"));
        // Das WC findet seinen Abwasser-Anschluss über das Medium des Rohrs gegenüber
        Assert.True(Verbunden(p, wc, "sw", Namens(p, "Rohr 2"), "anfang"));
        Assert.Equal("raus", wc.AnschlussNamens("sw")!.Fluss);
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code is "P03" or "P07");
    }

    // ------------------------------------------------------------------ Raum

    [Fact]
    public void Raum_AusBestehendemRaumPerLage()
    {
        var p = Leer();
        projekte.Add(p);
        var datei = Datei("IFC2X3", new IfcHlksSpec());
        Ok(Uebernehmen(p, datei, NurHlks()));
        Assert.Null(Namens(p, "WC Vorwand").Raum);
        var eg = p.Modell.DerKlasse("Geschoss").Single(g => g.Kennzeichen == "EG");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-BAD", ["geschoss"] = eg.Kennzeichen,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(new JsonArray(1500, 1800), new JsonArray(2600, 1800), new JsonArray(2600, 3500), new JsonArray(1500, 3500)) },
        }));
        var zweites = Ok(Uebernehmen(p, datei, NurHlks()));
        var wc = Namens(p, "WC Vorwand");
        Assert.Equal(p.Modell.Finde("R-BAD")!.Id, wc.Raum);
        Assert.Contains("1 geändert", zweites.Hinweise[0]);
        Assert.Null(Namens(p, "Rohr 1").Raum);   // Leitungen brauchen keinen Raum
    }

    [Fact]
    public void Raum_AusGeplantemRaumDesselbenLaufs()
    {
        var p = Leer();
        projekte.Add(p);
        var datei = Datei("IFC4", new IfcHlksSpec());
        Ok(Uebernehmen(p, datei, NurHlks()));   // Geschosse anlegen

        using var d = IfcDatei.Oeffnen(datei);
        d.Bezug = IfcBezug.Aus(p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("ifcBezug")!.Inhalt)!;
        var ids = d.Geschosse().ToDictionary(g => g, g => p.Modell.DerKlasse("Geschoss").Single(x => Math.Abs(x.Zahl("kote")!.Value - g.KoteMm) < 1).Id);
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "ifc", Akteur = "T" });
        var lauf = new IfcLauf
        {
            Datei = d, Kontext = k, UnterlageId = "UL-test", Quelle = "hlks_test.ifc", Herkunft = new Herkunft { Art = "ifc" }, GeschossIds = ids,
        };
        var eg = ids.Single(x => x.Key.KoteMm == 0).Value;
        // Ein Raum, den der Raum-Übertrager im selben Lauf geplant hätte (vorläufige Id)
        lauf.Geplant["ifc:RAUM-TEST"] = new Element
        {
            Id = "ifc:RAUM-TEST", Klasse = "Raum", Geschoss = eg, Kennungen = new() { ["ifc"] = "RAUM-TEST" },
            Geometrie = new Geometrie { Art = "polygon", Punkte = [[1500, 1800, 0], [2600, 1800, 0], [2600, 3500, 0], [1500, 3500, 0]] },
        };
        new IfcHlksUebertrag().Uebertragen(lauf);
        var wc = lauf.Geplant.Values.Single(x => x.Klasse == "WC");
        Assert.Equal("ifc:RAUM-TEST", wc.Raum);
    }

    // ------------------------------------------------------------------ Abgleich, Wiederholung, Trockenlauf

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void GleicheDatei_ZweitesMal_KeinPaket(string schema)
    {
        var p = Leer();
        projekte.Add(p);
        var datei = Datei(schema, new IfcHlksSpec { MitFormteilen = true, MitVerteiler = true, MitAbgas = true, SwSysteme = ["S SW 1", "S SW 2"] });
        Ok(Uebernehmen(p, datei, NurHlks()));
        var zweites = Ok(Uebernehmen(p, datei, NurHlks()));
        Assert.Null(zweites.PaketId);
    }

    [Fact]
    public void Trockenlauf_AendertNichts_ZeigtAberDieZahlen()
    {
        var p = Leer();
        projekte.Add(p);
        var e = Ok(Uebernehmen(p, Datei("IFC4", new IfcHlksSpec()), NurHlks(), trocken: true));
        Assert.True(e.Trockenlauf);
        Assert.Empty(p.Modell.Elemente);
        Assert.Contains(e.Hinweise, h => h.StartsWith("HLKS: 3 Leitungen"));
        Assert.Contains(e.Aenderungen, a => a.Klasse == "Rohr");
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void NeueFassung_GleichtUeberDieGlobalIdAb(string schema)
    {
        var p = Leer();
        projekte.Add(p);
        Ok(Uebernehmen(p, Datei(schema, new IfcHlksSpec { MitAbgas = true }, "hlks.ifc"), NurHlks()));
        var r1Id = Namens(p, "Rohr 1").Id;
        var boId = Namens(p, "Rohrbogen 90").Id;
        // Fassung 2: Rohr 1 länger, Abgasrohr weg, Ventil neu
        var e = Ok(Uebernehmen(p, Datei(schema, new IfcHlksSpec { Rohr1Ende = -500, MitVentil = true }, "hlks.ifc"), NurHlks()));
        var m = Regex.Match(e.Hinweise[0], @"(\d+) neu, (\d+) geändert, (\d+) gelöscht, (\d+) unverändert");
        Assert.True(m.Success, e.Hinweise[0]);
        Assert.Equal(1, int.Parse(m.Groups[1].Value));   // Ventil
        Assert.True(int.Parse(m.Groups[2].Value) >= 1);   // Rohr 1
        Assert.Equal(1, int.Parse(m.Groups[3].Value));   // Abgasrohr
        var r1 = Namens(p, "Rohr 1");
        Assert.Equal(r1Id, r1.Id);
        Assert.Equal([-500, 0, 500], r1.Geometrie!.Punkte[1]);
        Assert.DoesNotContain(p.Modell.Elemente, x => x.Name == "Abgasrohr");
        Assert.Single(p.Modell.DerKlasse("Ventil"));
        Assert.Equal(boId, p.Modell.VerbindungAn(r1.Id, "anfang")!.Gegenueber(r1.Id, "anfang")!.Element);
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
    }

    // ------------------------------------------------------------------ Bausteine

    [Theory]
    [InlineData("H VL NT 6", "H VL NT")]
    [InlineData("S WASH 13", "S WASH")]
    [InlineData("Kälte VL", "Kälte VL")]
    [InlineData("ZU_ZUL_2", "ZU_ZUL")]
    [InlineData("12", "12")]
    public void Basisname_OhneNummerAmEnde(string name, string basis) => Assert.Equal(basis, IfcHlksSysteme.Basisname(name));

    [Fact]
    public void Klassen_AlleMitRolleSowieSystemUndStrang_OhneAnlage()
    {
        var k = new IfcHlksUebertrag().Klassen;
        Assert.Contains("Rohr", k); Assert.Contains("Kanal", k); Assert.Contains("Bogen", k); Assert.Contains("WC", k);
        Assert.Contains("Waermeerzeuger", k); Assert.Contains("System", k); Assert.Contains("Strang", k);
        Assert.DoesNotContain("Anlage", k); Assert.DoesNotContain("Wand", k); Assert.DoesNotContain("Leitung", k);
        Assert.Equal("hlks", new IfcHlksUebertrag().Bereich);
        Assert.Contains(IfcImport.Uebertrager, u => u is IfcHlksUebertrag);
    }

    [Fact]
    public void Namensmuster_NurBekannteKlassen_SpezifischeVorAllgemeinen()
    {
        foreach (var (klasse, muster, _) in IfcHlksKlasse.Namensmuster)
        {
            Assert.True(IfcHlksKlasse.IstHlks(Kat, klasse), klasse);
            _ = new Regex(muster);
        }
        int Stelle(string k) => IfcHlksKlasse.Namensmuster.ToList().FindIndex(x => x.Klasse == k);
        Assert.True(Stelle("Wassererwaermer") < Stelle("Speicher"));   // «Warmwasserspeicher»
        Assert.True(Stelle("TStueck") < Stelle("Bogen"));
        Assert.Matches(IfcHlksKlasse.Namensmuster[Stelle("Ventil")].Muster, "Dreiwegeventil");
        Assert.DoesNotMatch(IfcHlksKlasse.Namensmuster[Stelle("Ventil")].Muster, "ventilator");
        Assert.DoesNotMatch(IfcHlksKlasse.Namensmuster[Stelle("Pumpe")].Muster, "pumpenschacht");
    }
}
