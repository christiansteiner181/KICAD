using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Übertrag der Hülle aus IFC (Modul 1c, B4) an der synthetischen Datei IfcHuelleTestdatei, je IFC4 und IFC2x3:
/// Aufbauten aus Materialschichten (Reihenfolge innen → aussen), Materialien mit Kennwerten der Datei, U-Werte aus den Psets,
/// Fenster- und Türkennwerte, Nordwinkel aus TrueNorth, Abgleich. Alle Zahlen der Testdatei sind Testwerte.</summary>
public class IfcHuelleTests
{
    static readonly Dictionary<string, (Projekt P, BefehlsErgebnis E)> laeufe = [];

    /// <summary>Ein Übertrag je Fassung und Variante (für die lesenden Tests geteilt).</summary>
    internal static (Projekt P, BefehlsErgebnis E) Uebernommen(string schema, HuelleVariante? v = null)
    {
        v ??= new HuelleVariante();
        lock (laeufe)
        {
            var k = schema + v.Kuerzel;
            if (laeufe.TryGetValue(k, out var da)) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, IfcHuelleTestdatei.Datei(schema, v)));
            return laeufe[k] = (p, e);
        }
    }

    static Element Aufbau(Projekt p, string name) =>
        p.Modell.DerKlasse("Aufbau").SingleOrDefault(e => e.Name == name) ?? throw new Xunit.Sdk.XunitException($"Aufbau «{name}» fehlt: {string.Join(" | ", p.Modell.DerKlasse("Aufbau").Select(e => e.Name))}");

    static Element Material(Projekt p, string name) =>
        p.Modell.DerKlasse("Material").SingleOrDefault(e => e.Name == name) ?? throw new Xunit.Sdk.XunitException($"Material «{name}» fehlt: {string.Join(" | ", p.Modell.DerKlasse("Material").Select(e => e.Name))}");

    static Element Wand(Projekt p, string name) => IfcArchitekturTests.Namens(p, "Wand", name);

    static List<(string Material, double Dicke)> Schichten(Element aufbau) =>
        ((JsonArray)aufbau.Eigenschaft("schichten")!.Inhalt!).Select(s => (s!["material"]!.GetValue<string>(), s["dicke"]!.GetValue<double>())).ToList();

    /// <summary>Material einer Schicht: die Id des Materials oder (solange IfcUebernahme Verweise in Listen nicht auflöst, Befund
    /// B4-1) die vorläufige Id «ifc:&lt;Kennung&gt;».</summary>
    static Element MaterialDerSchicht(Projekt p, string verweis) =>
        verweis.StartsWith("ifc:") ? p.Modell.Elemente.Single(e => e.Kennungen?.GetValueOrDefault("ifc") == verweis[4..])
        : p.Modell.Finde(verweis) ?? throw new Xunit.Sdk.XunitException("Material " + verweis + " fehlt");

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Aufbauten_InnenNachAussen_GleicheFolgeDerselbeAufbau(string schema)
    {
        var (p, _) = Uebernommen(schema);
        Assert.Equal(3, p.Modell.DerKlasse("Aufbau").Count());
        // W-Sued und W-Nord: Datei [Wärmedämmung, Beton], Dämmung aussen → innen → aussen = Beton, Wärmedämmung; ein Aufbau
        var a = Aufbau(p, "Beton 150 / Wärmedämmung 50");
        Assert.Equal(a.Id, Wand(p, "W-Sued").Bezuege["aufbau"]);
        Assert.Equal(a.Id, Wand(p, "W-Nord").Bezuege["aufbau"]);
        Assert.Equal("wand", a.Text("art"));
        var s = Schichten(a);
        Assert.Equal([150.0, 50.0], s.Select(x => x.Dicke));
        Assert.Equal(["Beton", "Wärmedämmung"], s.Select(x => MaterialDerSchicht(p, x.Material).Name));
        // W-Ost: Schichtfolge läuft entgegen (NEGATIVE ab Offset 200): Dämmung innen
        var o = Aufbau(p, "Wärmedämmung 50 / Beton 150");
        Assert.Equal(o.Id, Wand(p, "W-Ost").Bezuege["aufbau"]);
        Assert.NotEqual(a.Id, o.Id);
        // W-West hat nur ein Material, keinen Schichtaufbau
        Assert.False(Wand(p, "W-West").Bezuege.ContainsKey("aufbau"));
        // Text am Bauteil bleibt wie IfcArchitektur ihn schreibt (Reihenfolge der Datei)
        Assert.Equal("Wärmedämmung 50 / Beton 150", Wand(p, "W-Sued").Text("aufbau"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Aufbau_Herkunft_Kennung_Fundstelle(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var a = Aufbau(p, "Beton 150 / Wärmedämmung 50");
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Matches(@"^aufbau:[0-9a-f]{12}$", a.Kennungen!["ifc"]);
        Assert.Equal(ul.Id, a.Kennungen["quelle"]);
        Assert.Equal("ifc", a.Angelegt!.Art);
        Assert.Contains("IfcMaterialLayerSet #", a.Angelegt.Quelle);
        Assert.Equal("ifc", a.Eigenschaft("schichten")!.Herkunft.Art);
        Assert.Contains("IfcMaterialLayerSet #", a.Eigenschaft("art")!.Herkunft.Quelle);
        Assert.Contains("b4_huelle", a.Eigenschaft("art")!.Herkunft.Quelle);
        // Aufbau und Material gehören keinem Geschoss
        Assert.Null(a.Geschoss);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Bodenplatte_Baseslab_IstBoden_SchichtenVonObenNachUnten(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var platte = IfcArchitekturTests.Namens(p, "Decke", "Bodenplatte");
        Assert.Equal("boden", platte.Text("art"));
        Assert.Contains("BASESLAB", platte.Eigenschaft("art")!.Herkunft.Quelle);
        var a = Aufbau(p, "Estrich 60 / Wärmedämmung 100 / Beton 200");
        Assert.Equal("boden", a.Text("art"));
        Assert.Equal(a.Id, platte.Bezuege["aufbau"]);
        Assert.Equal([60.0, 100.0, 200.0], Schichten(a).Select(x => x.Dicke));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Materialien_KennwerteNurAusDerDatei_MitFundstelle(string schema)
    {
        var (p, _) = Uebernommen(schema);
        Assert.Equal(["Beton", "Estrich", "Gipsplatte", "Wärmedämmung"], p.Modell.DerKlasse("Material").Select(m => m.Name!).Order());
        var beton = Material(p, "Beton");
        Assert.Equal("material:Beton", beton.Kennungen!["ifc"]);
        Assert.Equal(0.123, beton.Zahl("lambda"));       // Testwerte der Testdatei
        Assert.Equal(456, beton.Zahl("waermekapazitaet"));
        Assert.Equal(789, beton.Zahl("rohdichte"));
        Assert.Equal("ifc", beton.Eigenschaft("lambda")!.Herkunft.Art);
        Assert.Equal("W/(m·K)", beton.Eigenschaft("lambda")!.Einheit);
        Assert.Contains("ThermalConductivity", beton.Eigenschaft("lambda")!.Herkunft.Quelle);
        Assert.Contains("IfcMaterial #", beton.Eigenschaft("lambda")!.Herkunft.Quelle);
        Assert.Contains("b4_huelle", beton.Eigenschaft("lambda")!.Herkunft.Quelle);
        Assert.Contains("MassDensity", beton.Eigenschaft("rohdichte")!.Herkunft.Quelle);
        if (schema == "IFC4") Assert.Contains("Pset_MaterialThermal", beton.Eigenschaft("lambda")!.Herkunft.Quelle);
        else Assert.Contains("IfcThermalMaterialProperties", beton.Eigenschaft("lambda")!.Herkunft.Quelle);
        // Wärmedämmung: nur λ (IFC2x3 über IfcExtendedMaterialProperties); die zweite IfcMaterial-Instanz «Beton» der Nordwand ohne Kennwerte stört nicht
        var daemmung = Material(p, "Wärmedämmung");
        Assert.Equal(0.321, daemmung.Zahl("lambda"));
        Assert.Null(daemmung.Eigenschaft("rohdichte"));
        Assert.Null(daemmung.Eigenschaft("waermekapazitaet"));
        // ohne Kennwerte in der Datei keine Kennwerte
        foreach (var n in new[] { "Estrich", "Gipsplatte" })
            Assert.All(new[] { "lambda", "rohdichte", "waermekapazitaet", "emission", "absorption", "mu" }, e => Assert.Null(Material(p, n).Eigenschaft(e)));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void UWerte_AmAufbau_Ausdruecklich_MitQuelle(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var a = Aufbau(p, "Beton 150 / Wärmedämmung 50");
        Assert.Equal(0.25, a.Zahl("uWert"));            // Testwert
        Assert.Equal("ifc", a.Eigenschaft("uWert")!.Herkunft.Art);
        Assert.Contains("Pset_WallCommon.ThermalTransmittance", a.Eigenschaft("uWert")!.Herkunft.Quelle);
        Assert.Contains("b4_huelle", a.Eigenschaft("uWert")!.Herkunft.Quelle);
        Assert.Equal(0.35, Aufbau(p, "Wärmedämmung 50 / Beton 150").Zahl("uWert"));
        var boden = Aufbau(p, "Estrich 60 / Wärmedämmung 100 / Beton 200");
        Assert.Equal(0.45, boden.Zahl("uWert"));
        Assert.Contains("Pset_SlabCommon.ThermalTransmittance", boden.Eigenschaft("uWert")!.Herkunft.Quelle);
        // rWert kommt nicht aus der Datei: die Nachführung der Hülle rechnet ihn aus den Schichten (hier beide λ bekannt: 0.15/0.123 + 0.05/0.321)
        Assert.Equal("berechnung", a.Eigenschaft("rWert")!.Herkunft.Art);
        Assert.Equal(0.15 / 0.123 + 0.05 / 0.321, a.Zahl("rWert")!.Value, 6);
    }
    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void OhneSchichten_UAusDerDatei_AufbauNurMitU(string schema)
    {
        var (p, e) = Uebernommen(schema, new HuelleVariante(OhneSchichten: true));
        // Süd und Nord (U 0.25) teilen einen Aufbau, Ost (0.35) und die Bodenplatte (0.45) haben je einen; Testwerte
        Assert.Equal(3, p.Modell.DerKlasse("Aufbau").Count());
        var a = Aufbau(p, "U aus Datei 0.25 (wand)");
        Assert.Equal("aufbau:u:wand:0.250", a.Kennungen!["ifc"]);
        Assert.Equal("wand", a.Text("art"));
        Assert.Equal(0.25, a.Zahl("uWert"));
        Assert.Equal("ifc", a.Eigenschaft("uWert")!.Herkunft.Art);
        Assert.Contains("Pset_WallCommon.ThermalTransmittance", a.Eigenschaft("uWert")!.Herkunft.Quelle);
        Assert.Contains("b4_huelle", a.Eigenschaft("uWert")!.Herkunft.Quelle);
        Assert.Null(a.Eigenschaft("schichten"));
        Assert.Equal(a.Id, Wand(p, "W-Sued").Bezuege["aufbau"]);
        Assert.Equal(a.Id, Wand(p, "W-Nord").Bezuege["aufbau"]);
        Assert.Equal(Aufbau(p, "U aus Datei 0.35 (wand)").Id, Wand(p, "W-Ost").Bezuege["aufbau"]);
        Assert.False(Wand(p, "W-West").Bezuege.ContainsKey("aufbau"));   // kein U in der Datei
        var boden = IfcArchitekturTests.Namens(p, "Decke", "Bodenplatte");
        var ab = Aufbau(p, "U aus Datei 0.45 (boden)");
        Assert.Equal("boden", ab.Text("art"));
        Assert.Equal(ab.Id, boden.Bezuege["aufbau"]);
        // Fenster und Tür tragen ihr U weiter direkt
        Assert.Equal(1.3, IfcArchitekturTests.Namens(p, "Fenster", "F 0.01").Zahl("uw"));
        Assert.Contains("Hülle: 3 Aufbauten, 2 Materialien (0 mit λ), 6 Bauteile mit U aus der Datei, davon 4 nur mit U aus der Datei (3 Aufbauten ohne Schichten)", e.Hinweise);
    }

    [Fact]
    public void OhneSchichten_ZweiteUebernahme_KeinPaket()
    {
        var p = Leer();
        var datei = IfcHuelleTestdatei.Datei("IFC4", new HuelleVariante(OhneSchichten: true));
        Ok(Uebernehmen(p, datei));
        Assert.Null(Ok(Uebernehmen(p, datei)).PaketId);
        Assert.Equal(3, p.Modell.DerKlasse("Aufbau").Count());
    }


    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void IsExternal_NurHinweis_KeineAngrenzung(string schema)
    {
        var (p, e) = Uebernommen(schema);
        Assert.All(p.Modell.DerKlasse("Wand"), w => Assert.Null(w.Eigenschaft("angrenzung")));
        Assert.Contains(e.Hinweise, h => h.Contains("IsExternal") && h.Contains("3 Bauteile aussen"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void FensterUndTuer_Kennwerte_RahmenanteilEinsMinusGlasanteil(string schema)
    {
        var (p, _) = Uebernommen(schema);
        var f = IfcArchitekturTests.Namens(p, "Fenster", "F 0.01");
        Assert.Equal(1.3, f.Zahl("uw"));                 // Testwert
        Assert.Equal(0.3, f.Zahl("rahmenanteil"));       // 1 − 0.7
        Assert.Equal("ifc", f.Eigenschaft("uw")!.Herkunft.Art);
        Assert.Contains("Pset_WindowCommon.ThermalTransmittance", f.Eigenschaft("uw")!.Herkunft.Quelle);
        Assert.Contains("Pset_WindowCommon.GlazingAreaFraction", f.Eigenschaft("rahmenanteil")!.Herkunft.Quelle);
        Assert.Null(f.Eigenschaft("ug"));                // nicht in der Datei, nicht erfunden
        var t = IfcArchitekturTests.Namens(p, "Tuer", "T 0.01");
        Assert.Equal(1.8, t.Zahl("ud"));                 // Testwert
        Assert.Contains("Pset_DoorCommon.ThermalTransmittance", t.Eigenschaft("ud")!.Herkunft.Quelle);
        Assert.False(f.Bezuege.ContainsKey("aufbau"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Zusammenfassung_InDenHinweisen(string schema)
    {
        var (_, e) = Uebernommen(schema);
        // 3 Aufbauten; Beton, Estrich, Gipsplatte, Wärmedämmung (2 mit λ); U aus der Datei: Süd, Ost, Nord, Bodenplatte, Fenster, Tür
        Assert.Contains("Hülle: 3 Aufbauten, 4 Materialien (2 mit λ), 6 Bauteile mit U aus der Datei, davon 0 nur mit U aus der Datei (0 Aufbauten ohne Schichten)", e.Hinweise);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void UWert_Widerspruch_BeiGleichemAufbau_WirdNichtGesetzt(string schema)
    {
        var (p, e) = Uebernommen(schema, new HuelleVariante(Konflikt: true));
        var a = Aufbau(p, "Beton 150 / Wärmedämmung 50");
        Assert.Null(a.Eigenschaft("uWert"));
        Assert.Contains(e.Hinweise, h => h.Contains("verschiedene U-Werte") && h.Contains("0.25") && h.Contains("0.28"));
        Assert.Equal(0.35, Aufbau(p, "Wärmedämmung 50 / Beton 150").Zahl("uWert"));
        Assert.Contains("4 Bauteile mit U", string.Join("\n", e.Hinweise));   // Ost, Boden, Fenster, Tür
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Einheit_KiloJoule_WirdUmgerechnet_UndGenannt(string schema)
    {
        var (p, e) = Uebernommen(schema, new HuelleVariante(Einheit: true));
        Assert.Equal(456, Material(p, "Beton").Zahl("waermekapazitaet"));   // 0.456 kJ/(kg·K)
        Assert.Contains(e.Hinweise, h => h.Contains("SPECIFICHEATCAPACITYUNIT") && h.Contains("1000"));
        Assert.Equal(0.123, Material(p, "Beton").Zahl("lambda"));            // ohne abgeleitete Einheit gilt SI
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void TrueNorth_30Grad_NordwinkelGesetzt(string schema)
    {
        var (p, e) = Uebernommen(schema);
        var pi = p.Modell.DerKlasse("Projektinformation").Single();
        Assert.Equal(30, pi.Zahl("nordwinkel")!.Value, 2);
        Assert.Equal("ifc", pi.Eigenschaft("nordwinkel")!.Herkunft.Art);
        Assert.Contains("TrueNorth", pi.Eigenschaft("nordwinkel")!.Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.StartsWith("Hülle: nordwinkel aus der Datei: 30°"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void TrueNorth_MitGedrehtemGebaeude_NordwinkelBerücksichtigtAusrichtung(string schema)
    {
        // Gebäude 20° gedreht → Projekt-+y liegt 20° gegen die Datei-+y; Nord bei 30° im Uhrzeigersinn von Datei-+y
        // → von Projekt-+y aus 30° + 20° = 50° im Uhrzeigersinn
        var (p, _) = Uebernommen(schema, new HuelleVariante(Gedreht: true));
        Assert.Equal(50, p.Modell.DerKlasse("Projektinformation").Single().Zahl("nordwinkel")!.Value, 2);
        Assert.Contains("Ausrichtung der Datei 20°", p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("nordwinkel")!.Herkunft.Quelle);
        // die Aufbauten kommen trotz der Drehung gleich heraus
        Assert.Equal(3, p.Modell.DerKlasse("Aufbau").Count());
        Aufbau(p, "Beton 150 / Wärmedämmung 50");
        Aufbau(p, "Wärmedämmung 50 / Beton 150");
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void OhneTrueNorth_KeinNordwinkel_Hinweis(string schema)
    {
        var (p, e) = Uebernommen(schema, new HuelleVariante(OhneNord: true));
        Assert.Null(p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("nordwinkel"));
        Assert.Contains(e.Hinweise, h => h.Contains("keine Nordrichtung"));
    }

    [Fact]
    public void Nordwinkel_BereitsGesetzt_BleibtUndWirdGemeldet()
    {
        var p = Leer();
        var datei = IfcHuelleTestdatei.Datei("IFC4", new HuelleVariante());
        Ok(Uebernehmen(p, datei));
        var pi = p.Modell.DerKlasse("Projektinformation").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = pi.Kennzeichen, ["eigenschaften"] = new JsonObject { ["nordwinkel"] = 45 } }));
        var zweites = Ok(Uebernehmen(p, datei));
        Assert.Equal(45, p.Modell.DerKlasse("Projektinformation").Single().Zahl("nordwinkel"));
        Assert.Contains(zweites.Hinweise, h => h.Contains("30°") && h.Contains("weicht vom Projekt (45°) ab"));
        Assert.Null(zweites.PaketId);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void ZweiteUebernahme_DerselbenDatei_KeinPaket(string schema)
    {
        var p = Leer();
        var datei = IfcHuelleTestdatei.Datei(schema, new HuelleVariante());
        Ok(Uebernehmen(p, datei));
        var zweites = Ok(Uebernehmen(p, datei));
        Assert.True(zweites.PaketId is null, string.Join(" ¦ ", zweites.Hinweise.Take(4)));
        Assert.Contains(zweites.Hinweise, h => h.Contains("0 neu, 0 geändert, 0 gelöscht"));
        Assert.Equal(3, p.Modell.DerKlasse("Aufbau").Count());
        Assert.Equal(4, p.Modell.DerKlasse("Material").Count());
    }

    [Fact]
    public void ZweiteDatei_GleicheMaterialnamen_NimmtDieErsteNichtWeg()
    {
        var p = Leer();
        var erste = IfcHuelleTestdatei.Datei("IFC4", new HuelleVariante());
        var zweite = Path.Combine(Path.GetDirectoryName(erste)!, "b4_zweite_unterlage.ifc");
        File.Copy(erste, zweite, true);
        Ok(Uebernehmen(p, erste));
        var ul1 = p.Modell.DerKlasse("Unterlage").Single().Id;
        Ok(Uebernehmen(p, zweite));
        // gleiche Namen, aber je Unterlage ein eigenes Material (Kennung der zweiten mit Anhang)
        var betons = p.Modell.DerKlasse("Material").Where(m => m.Name == "Beton").ToList();
        Assert.Equal(2, betons.Count);
        Assert.Single(betons, m => m.Kennungen!["ifc"] == "material:Beton" && m.Kennungen["quelle"] == ul1);
        Assert.Single(betons, m => m.Kennungen!["ifc"].StartsWith("material:Beton@") && m.Kennungen["quelle"] != ul1);
    }

    [Fact]
    public void Trockenlauf_ZeigtAufbauUndMaterial_OhneSieAnzulegen()
    {
        var p = Leer();
        var e = Ok(Uebernehmen(p, IfcHuelleTestdatei.Datei("IFC4", new HuelleVariante()), trocken: true));
        Assert.Contains(e.Aenderungen, a => a.Klasse == "Aufbau" && a.Art == "angelegt");
        Assert.Contains(e.Aenderungen, a => a.Klasse == "Material" && a.Art == "angelegt");
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void NurRaeume_ErzeugtKeineHuelle()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcHuelleTestdatei.Datei("IFC4", new HuelleVariante()), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        Assert.Empty(p.Modell.DerKlasse("Aufbau"));
        Assert.Empty(p.Modell.DerKlasse("Material"));
    }

    /// <summary>Befund B4-1: IfcUebernahme.Abgleichen löst «ifc:…» nur in Bezügen, Geschoss, Raum, System und teilVon auf, nicht
    /// in Listen wie Aufbau.schichten[].material. Der Test beschreibt den gewünschten Zustand und ist ausgesetzt, bis der
    /// Abgleich dort auflöst (Änderung in IfcUebernahme.cs, nicht Teil von B4).</summary>
    [Fact]   // Befund B4-1 vom Hauptlauf in IfcUebernahme.Abgleichen behoben (18.10.2026)
    public void Schichten_MaterialVerweise_SindEchteIds()
    {
        var (p, _) = Uebernommen("IFC4");
        foreach (var a in p.Modell.DerKlasse("Aufbau"))
            Assert.All(Schichten(a), s => Assert.NotNull(p.Modell.Finde(s.Material)));
    }

    // ------------------------------------------------------------------ echte Dateien (nur mit HLKS_ABNAHME=1, je bis zu zwei Minuten)

    public class Echt(ITestOutputHelper aus)
    {
        void Lauf(string relativ)
        {
            if (Environment.GetEnvironmentVariable("HLKS_ABNAHME") != "1") { aus.WriteLine("Nur mit HLKS_ABNAHME=1 (dauert Minuten)."); return; }
            var datei = IfcHilfe.Echt(relativ);
            if (datei is null) { aus.WriteLine($"Echte Datei fehlt: %USERPROFILE%\\HLKS_CAD_testdaten\\{relativ}"); return; }
            var p = Leer();
            var sw = Stopwatch.StartNew();
            var e = Ok(Uebernehmen(p, datei));
            var aufbauten = p.Modell.DerKlasse("Aufbau").ToList();
            var materialien = p.Modell.DerKlasse("Material").ToList();
            var z = new JsonObject
            {
                ["datei"] = Path.GetFileName(datei), ["dauerSekunden"] = Math.Round(sw.Elapsed.TotalSeconds, 1),
                ["aufbauten"] = aufbauten.Count, ["aufbautenMitU"] = aufbauten.Count(a => a.Eigenschaft("uWert") is not null),
                ["materialien"] = materialien.Count, ["materialienMitLambda"] = materialien.Count(m => m.Eigenschaft("lambda") is not null),
                ["materialienMitRohdichte"] = materialien.Count(m => m.Eigenschaft("rohdichte") is not null),
                ["materialienMitWaermekapazitaet"] = materialien.Count(m => m.Eigenschaft("waermekapazitaet") is not null),
                ["aufbautenNurU"] = aufbauten.Count(a => a.Eigenschaft("schichten") is null && a.Eigenschaft("uWert") is not null),
                ["bauteileMitNurU"] = p.Modell.Elemente.Count(x => x.Bezuege.TryGetValue("aufbau", out var ai) && p.Modell.Finde(ai) is { } af && af.Eigenschaft("schichten") is null),
                ["bauteileMitAufbau"] = p.Modell.Elemente.Count(x => x.Bezuege.ContainsKey("aufbau")),
                ["fensterMitUw"] = p.Modell.DerKlasse("Fenster").Count(f => f.Eigenschaft("uw") is not null),
                ["fensterMitRahmenanteil"] = p.Modell.DerKlasse("Fenster").Count(f => f.Eigenschaft("rahmenanteil") is not null),
                ["tuerenMitUd"] = p.Modell.DerKlasse("Tuer").Count(f => f.Eigenschaft("ud") is not null),
                ["nordwinkel"] = p.Modell.DerKlasse("Projektinformation").FirstOrDefault()?.Zahl("nordwinkel"),
                ["aufbautenListe"] = new JsonArray(aufbauten.OrderBy(a => a.Name).Select(a => (JsonNode)$"{a.Text("art")}: {a.Name}").ToArray()),
                ["huelleHinweise"] = new JsonArray(e.Hinweise.Where(h => h.StartsWith("Hülle")).Select(h => (JsonNode)h).ToArray()),
            };
            aus.WriteLine(z.ToJsonString(Json.Eingerueckt));
            var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b4_echt_{Path.GetFileNameWithoutExtension(datei)}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
            File.WriteAllText(ziel, z.ToJsonString(Json.Eingerueckt));
            // jede Schicht verweist auf ein Material des Übertrags (echte Id oder vorläufige Id «ifc:…»)
            foreach (var a in aufbauten.Where(x => x.Eigenschaft("schichten") is not null))
                Assert.All(Schichten(a), s => Assert.NotNull(MaterialDerSchicht(p, s.Material)));
            Assert.Contains(e.Hinweise, h => h.StartsWith("Hülle: ") && h.Contains("Aufbauten"));
        }

        [Fact] public void Bernstrasse_Architektur() => Lauf(@"bernstrasse42\architekt_3d.ifc");
        [Fact] public void Bkw_Architektur() => Lauf(@"bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc");
    }
}
