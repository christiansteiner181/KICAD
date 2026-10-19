using System.Reflection;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.UtilityResource;
using static Hlks.Kern.Tests.AussparungHilfe;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using N = Hlks.Kern.Tests.IfcNetzFassung2Testdatei;
using X = Hlks.Kern.Tests.AussparungIfcTestdatei;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B4b: IFC-Ausgabe der Aussparungen (Abfrage aussparungen_ifc). Prüft das Schema der Xbim-Klassen (IFC4
/// PredefinedType, IFC2X3 ObjectType), die Rundreise (die geschriebene Datei mit dem eigenen IFC-Leser wieder öffnen: Masse, Lage
/// auf 0.1 mm, Geschoss, Property Set), die Koordinaten «ifc» gegen «projekt» mit einem Projektbezug, die stabile GlobalId, die Filter
/// und den Exportbericht. Werte FIKTIV (siehe AussparungHilfe); Handrechnungen in mm.
/// Fall(): Wand W-1 (y −100 … 100, EG), Decke D-1 (OG, z 2750 … 3000), Testrohrserie.
///   RO-1  DN 50 (da 55 → Ø 95 mit Spiel 20 → ganze cm Ø 100, Nachtrag B4e) quer durch W-1 bei x 1500, z 1000: Kernbohrung (von Hand,
///         aussparung_form – Nachtrag B3b),
///         Tiefe 200, Achse (0, 1, 0), Mitte (1500, 0, 1000)
///   KA-1  Kanal 400 × 200 quer durch W-1 bei x 3000, z 1800: Aussparung 440 × 240, Tiefe 200, Mitte (3000, 0, 1800), UK 1680, OK 1920
///   KA-2  Kanal 400 × 200 senkrecht durch D-1 bei (3000, 2000): Aussparung 440 × 240, Tiefe 250, Achse (0, 0, 1), Mitte (3000, 2000, 2875), UK 2750, OK 3000
///         – Geschoss EG: Deckenaussparungen gehören ins Geschoss UNTER der Decke (Hauptlauf 19.10.2026, SIA 400:2000 B.10.3 / SIA 410/2
///         Ziff. 2.3; Unterkante 2750 − 1 = 2749 im Band des EG 0 … 3000). Damit liegen alle drei im EG: Nummern nach x, dann y –
///         RO-1 (1500, 0) A-EG-001, KA-1 (3000, 0) A-EG-002, KA-2 (3000, 2000) A-EG-003; die Datei führt nur das gebrauchte Geschoss EG.
///         Kurzzeichen (Nachtrag B4d): RO-1 im System KW (sanitaer) «S WD», KA-1 und KA-2 im System ZUL (lueftung) «L WD» bzw. «L DD».</summary>
public sealed class AussparungIfcExportTests : IDisposable
{
    readonly Xunit.Abstractions.ITestOutputHelper aus;

    readonly string ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b4b_{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..8]}");
    readonly List<Projekt> projekte = [];

    public AussparungIfcExportTests(Xunit.Abstractions.ITestOutputHelper aus)
    {
        this.aus = aus;
        Directory.CreateDirectory(ordner);
    }

    public void Dispose()
    {
        try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (HlksFehler) { }
    }

    string Pfad(string name) => Path.Combine(ordner, name);

    /// <summary>Wandprojekt mit RO-1, KA-1, KA-2, vorgeschlagen, RO-1 von Hand rund, nummeriert (alle vorgeschlagen).</summary>
    Projekt Fall()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1500);
        Kanal(p, "KA-1", 400, 200, [3000, -500, 1800], [3000, 500, 1800]);
        Decke(p);
        Kanal(p, "KA-2", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);
        Ok(Vorschlagen(p));
        RundVonHand(p, "RO-1");   // B3b: Kernbohrung nur von Hand
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Assert.Equal(3, Aussparungen(p).Count);
        return p;
    }

    /// <summary>Fall(), alle drei freigegeben.</summary>
    Projekt FallFrei()
    {
        var p = Fall();
        Ok(Status(p, "freigegeben", Aussparungen(p).Select(a => a.Kennzeichen).ToArray()));
        return p;
    }

    static string Kz(Projekt p, string leitung) => VonLeitung(p, leitung).Kennzeichen;

    static JsonObject Export(Projekt p, string datei, string? fassung = null, string? koordinaten = null, string? geschoss = null, JsonNode? status = null)
    {
        var par = new JsonObject { ["datei"] = datei };
        if (fassung is not null) par["fassung"] = fassung;
        if (koordinaten is not null) par["koordinaten"] = koordinaten;
        if (geschoss is not null) par["geschoss"] = geschoss;
        if (status is not null) par["status"] = status;
        return (JsonObject)Tuer.Abfrage(p, "aussparungen_ifc", par);
    }

    static void Bezug(Projekt p, double x, double y, double z, double drehung)
    {
        var bezug = new JsonObject { ["x"] = x, ["y"] = y, ["z"] = z, ["drehung"] = drehung };
        if (p.Modell.DerKlasse("Projektinformation").Any())
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = p.Modell.DerKlasse("Projektinformation").First().Kennzeichen, ["eigenschaften"] = new JsonObject { ["ifcBezug"] = bezug } }));
        else
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI", ["eigenschaften"] = new JsonObject { ["ifcBezug"] = bezug } }));
    }

    static IIfcBuildingElementProxy Proxy(IfcDatei d, string gid) =>
        d.Modell.Instances.OfType<IIfcBuildingElementProxy>().Single(x => x.GlobalId.ToString() == gid);

    static string Gid(Projekt p, string leitung) => AussparungIfcExport.AussparungGlobalId(p.Kopf.Id, VonLeitung(p, leitung).Id);

    static void Nahe(double[] erwartet, double[] ist, double tol = 0.1, string? was = null)
    {
        for (int i = 0; i < erwartet.Length; i++)
            Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"{was}: [{string.Join(", ", ist.Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))}] statt [{string.Join(", ", erwartet)}] (Toleranz {tol})");
    }

    static double Laenge(IfcDatei d, Dictionary<string, IfcWert> eig, string name) =>
        d.LaengeMm(eig[$"{AussparungIfcExport.PsetAussparung}.{name}"]) ?? throw new Xunit.Sdk.XunitException($"{name} ist keine Länge");

    static string Text(Dictionary<string, IfcWert> eig, string name) => eig[$"{AussparungIfcExport.PsetAussparung}.{name}"].Inhalt!.ToString()!;

    // ------------------------------------------------------------------ Schema (Xbim-Klassen)

    [Fact]
    public void Schema_Ifc4_ProxyHatPredefinedTypeProvisionForVoid()
    {
        var klasse = typeof(Xbim.Ifc4.SharedBldgElements.IfcBuildingElementProxy);
        var eig = klasse.GetProperty("PredefinedType", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(eig);
        var aufzaehlung = Nullable.GetUnderlyingType(eig!.PropertyType);
        Assert.Equal(typeof(IfcBuildingElementProxyTypeEnum), aufzaehlung);
        Assert.Contains(AussparungIfcExport.PredefinedType4, Enum.GetNames(typeof(IfcBuildingElementProxyTypeEnum)));
        Assert.Equal(IfcBuildingElementProxyTypeEnum.PROVISIONFORVOID, Enum.Parse<IfcBuildingElementProxyTypeEnum>(AussparungIfcExport.PredefinedType4));
        // dieselbe Entität, mit der der Rest von KICAD IFC4 liest
        Assert.True(typeof(IIfcBuildingElementProxy).IsAssignableFrom(klasse));
    }

    [Fact]
    public void Schema_Ifc2x3_ProxyHatKeinPredefinedType_ObjectTypeIstDerWeg()
    {
        var klasse = typeof(Xbim.Ifc2x3.ProductExtension.IfcBuildingElementProxy);
        // kein öffentliches PredefinedType am Proxy (nur über die IFC4-Schnittstelle explizit), dafür ObjectType und CompositionType
        Assert.Null(klasse.GetProperty("PredefinedType", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(klasse.GetProperty("ObjectType", BindingFlags.Public | BindingFlags.Instance));
        var komposition = klasse.GetProperty("CompositionType", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(komposition);
        Assert.Equal(["COMPLEX", "ELEMENT", "PARTIAL"], Enum.GetNames(Nullable.GetUnderlyingType(komposition!.PropertyType)!).Order());
        // auch die Aufzählung des Typobjekts kennt PROVISIONFORVOID nicht → ObjectType «ProvisionForVoid»
        var typEnum = typeof(Xbim.Ifc2x3.ProductExtension.IfcBuildingElementProxyTypeEnum);
        Assert.DoesNotContain(AussparungIfcExport.PredefinedType4, Enum.GetNames(typEnum));
        Assert.Equal(["NOTDEFINED", "USERDEFINED"], Enum.GetNames(typEnum).Order());
        Assert.Equal("ProvisionForVoid", AussparungIfcExport.ObjectType2x3);
    }

    [Fact]
    public void Schema_AlleVerwendetenEntitaetenGibtEsInBeidenFassungen()
    {
        string[] namen = ["IfcProject", "IfcSite", "IfcBuilding", "IfcBuildingStorey", "IfcBuildingElementProxy", "IfcExtrudedAreaSolid", "IfcRectangleProfileDef",
            "IfcCircleProfileDef", "IfcPropertySet", "IfcPropertySingleValue", "IfcRelDefinesByProperties", "IfcRelAggregates", "IfcRelContainedInSpatialStructure"];
        var t4 = typeof(Xbim.Ifc4.Kernel.IfcProject).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        var t2 = typeof(Xbim.Ifc2x3.Kernel.IfcProject).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        foreach (var n in namen)
        {
            Assert.Contains(n, t4);
            Assert.Contains(n, t2);
        }
    }

    // ------------------------------------------------------------------ Rundreise

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Rundreise_MasseLageGeschossPropertySet(string fassung)
    {
        var p = FallFrei();
        var pfad = Pfad($"a_{fassung}.ifc");
        var b = Export(p, pfad, fassung, "projekt");
        Assert.Equal(fassung, b["fassung"]!.GetValue<string>());
        Assert.Equal("bestanden", b["rundreise"]!["ergebnis"]!.GetValue<string>());
        Assert.True(File.Exists(pfad));

        using var d = IfcDatei.Oeffnen(pfad);   // wie ein Empfänger: eigener Leser, unabhängig von der Prüfung im Export
        Assert.Equal(fassung, d.Schema);
        Assert.Equal(1, d.MmJeEinheit);
        var proxys = d.Modell.Instances.OfType<IIfcBuildingElementProxy>().ToList();
        Assert.Equal(3, proxys.Count);
        Assert.Equal("IfcProject", IfcDatei.Entitaet(d.Modell.Instances.OfType<IIfcProject>().Single()));
        Assert.Single(d.Modell.Instances.OfType<IIfcSite>());
        Assert.Single(d.Modell.Instances.OfType<IIfcBuilding>());
        Assert.Equal(["EG"], d.Geschosse().Select(g => g.Name));        // nur das gebrauchte Geschoss (alle drei im EG, Kopf)
        Assert.Equal([0], d.Geschosse().Select(g => g.KoteMm));

        // (Leitung, Nummer, Beschreibung, Mitte, Achse, rund, B, H bzw. D, Tiefe, Geschoss, Bauteil, Kurzzeichen)
        var soll = new (string Leitung, string Nummer, string Beschreibung, double[] Mitte, double[] Achse, bool Rund, double B, double H, double Tiefe, string Geschoss, string Bauteil, string Kurz)[]
        {
            ("RO-1", "A-EG-001", "Kernbohrung rund Ø 100 mm, Tiefe 200 mm", [1500, 0, 1000], [0, 1, 0], true, 100, 100, 200, "EG", "W-1", "S WD"),
            ("KA-1", "A-EG-002", "Aussparung rechteckig 440 × 240 mm, Tiefe 200 mm", [3000, 0, 1800], [0, 1, 0], false, 440, 240, 200, "EG", "W-1", "L WD"),
            ("KA-2", "A-EG-003", "Aussparung rechteckig 440 × 240 mm, Tiefe 250 mm", [3000, 2000, 2875], [0, 0, 1], false, 440, 240, 250, "EG", "D-1", "L DD"),
        };
        foreach (var s in soll)
        {
            var a = VonLeitung(p, s.Leitung);
            Assert.Equal(s.Nummer, a.Text("nummer"));
            var px = Proxy(d, Gid(p, s.Leitung));
            Assert.Equal(s.Nummer, px.Name?.ToString());
            Assert.Equal(s.Beschreibung, px.Description?.ToString());
            Assert.Equal(a.Kennzeichen, px.Tag?.ToString());
            Assert.Equal("IfcBuildingElementProxy", IfcDatei.Entitaet(px));
            if (fassung == "IFC4") Assert.Equal("PROVISIONFORVOID", d.PredefinedType(px));
            else Assert.Equal("ProvisionForVoid", px.ObjectType?.ToString());
            Assert.Equal(s.Geschoss, d.GeschossVon(px)!.Name);

            // Lage: Platzierung in der Mitte, auf 0.1 mm (Projektkoordinaten, Bezug = Ursprung)
            var lage = d.Lage(px);
            Nahe(s.Mitte, lage.Ursprung, was: $"{s.Leitung} Mitte");
            Nahe(s.Achse, lage.AchseZ, 1e-9, $"{s.Leitung} Achse");
            // Körper
            var k = Assert.Single(px.Representation!.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>());
            Assert.Equal(s.Tiefe, (double)k.Depth, 0.1);
            Nahe([0, 0, -s.Tiefe / 2], d.Punkt(k.Position.Location), 0.1, $"{s.Leitung} Körper um die Mitte zentriert");
            if (s.Rund)
            {
                var kr = Assert.IsAssignableFrom<IIfcCircleProfileDef>(k.SweptArea);
                Assert.Equal(s.B, (double)kr.Radius * 2, 0.1);
            }
            else
            {
                var re = Assert.IsAssignableFrom<IIfcRectangleProfileDef>(k.SweptArea);
                Assert.Equal(s.B, (double)re.XDim, 0.1);
                Assert.Equal(s.H, (double)re.YDim, 0.1);
                Assert.True(Math.Abs(V3.Aus(lage.AchseX).Mal(Aussparungslage.Von(a)!.U)) > 1 - 1e-9, "Breite entlang U der Bauteilebene");
            }

            // Property Set
            var eig = d.Eigenschaften(px);
            Assert.Equal(s.Nummer, Text(eig, "Nummer"));
            Assert.Equal(a.Kennzeichen, Text(eig, "Kennzeichen"));
            Assert.Equal(a.Id, Text(eig, "KicadId"));
            Assert.Equal(s.Rund ? "Kernbohrung" : "Aussparung", Text(eig, "Art"));
            Assert.Equal("freigegeben", Text(eig, "Status"));
            Assert.Equal(s.Rund ? "rund" : "rechteckig", Text(eig, "Form"));
            Assert.Equal(s.Geschoss, Text(eig, "Geschoss"));
            Assert.Equal(s.Leitung, Text(eig, "Leitungen"));
            Assert.Equal("projekt", Text(eig, "Koordinatenbezug"));
            Assert.Equal(s.Tiefe, Laenge(d, eig, "Tiefe"), 0.1);
            if (s.Rund) { Assert.Equal(s.B, Laenge(d, eig, "Durchmesser"), 0.1); Assert.Equal(s.Mitte[2], Laenge(d, eig, "KoteAchse"), 0.1); }
            else { Assert.Equal(s.B, Laenge(d, eig, "Breite"), 0.1); Assert.Equal(s.H, Laenge(d, eig, "Hoehe"), 0.1); }
            Assert.Equal(s.Rund ? s.Mitte[2] : a.Zahl("kote_uk")!.Value, Laenge(d, eig, s.Rund ? "KoteAchse" : "KoteUK"), 0.1);
            var bauteil = Text(eig, "Bauteil");
            Assert.Equal(s.Bauteil, bauteil);   // in KICAD gezeichnet: das Kennzeichen (kein IFC-Produkt)
            Assert.Equal(s.Kurz, Text(eig, "Kurzzeichen"));   // Nachtrag B4d, SIA 400 B.10.2 Tabelle 5
        }
        // genaue Koten des Kanals in der Wand und in der Decke (Handrechnung)
        var e1 = d.Eigenschaften(Proxy(d, Gid(p, "KA-1")));
        Assert.Equal(1680, Laenge(d, e1, "KoteUK"), 0.1); Assert.Equal(1920, Laenge(d, e1, "KoteOK"), 0.1);
        Assert.Equal(1680, Laenge(d, e1, "KoteUKGeschoss"), 0.1);
        var e2 = d.Eigenschaften(Proxy(d, Gid(p, "KA-2")));
        Assert.Equal(2750, Laenge(d, e2, "KoteUK"), 0.1); Assert.Equal(3000, Laenge(d, e2, "KoteOK"), 0.1);
        Assert.Equal(2750, Laenge(d, e2, "KoteUKGeschoss"), 0.1); Assert.Equal(3000, Laenge(d, e2, "KoteOKGeschoss"), 0.1);   // über OK EG (Kote 0)

        // der Import-Prüfer liest die Datei (ifc_pruefen): Fassung, Einheit, Geschosse
        var pr = (JsonObject)Tuer.Abfrage(p, "ifc_pruefen", new JsonObject { ["datei"] = pfad });
        Assert.Equal(fassung, pr["schema"]!.GetValue<string>());
        Assert.Equal("mm", pr["einheit"]!.GetValue<string>());
        Assert.Single(pr["geschosse"]!.AsArray());
    }

    [Fact]
    public void Rundreise_XbimSchemaregelnOhneMeldung_BeideFassungen()
    {
        var p = FallFrei();
        foreach (var f in new[] { "IFC4", "IFC2X3" })
        {
            var b = Export(p, Pfad($"v_{f}.ifc"), f);
            var v = b["rundreise"]!["validierung"]!;
            Assert.Equal(0, v["meldungen"]!.GetValue<int>());
            Assert.True(v["entitaetenGeprueft"]!.GetValue<int>() > 50);
        }
    }

    [Fact]
    public void Rundreise_AbweichungVerhindertDasSchreiben()
    {
        // Eine Aussparung mit nicht schreibbarer Tiefe wird übersprungen (nicht still falsch geschrieben)
        var p = FallFrei();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Kz(p, "RO-1"), ["eigenschaften"] = new JsonObject { ["tiefe"] = 0 } }));
        var b = Export(p, Pfad("t.ifc"));
        Assert.Equal(2, b["anzahl"]!.GetValue<int>());
        Assert.Contains(b["warnungen"]!.AsArray(), w => w!.GetValue<string>().Contains($"{Kz(p, "RO-1")}: Tiefe fehlen – nicht ausgegeben"));
    }

    // ------------------------------------------------------------------ Koordinaten

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Koordinaten_IfcUeberProjektbezug_ProjektOhne_Rundreise(string fassung)
    {
        var p = FallFrei();
        Bezug(p, 1250, -500, 300, 90);
        var pfadIfc = Pfad($"ifc_{fassung}.ifc");
        var pfadProjekt = Pfad($"projekt_{fassung}.ifc");
        var b = Export(p, pfadIfc, fassung);   // Vorgabe: ifc, weil das Projekt einen Bezug hat
        Assert.Equal("ifc", b["koordinaten"]!["art"]!.GetValue<string>());
        Assert.Equal(1250, b["koordinaten"]!["bezug"]!["x"]!.GetValue<double>());
        Assert.Equal(90, b["koordinaten"]!["bezug"]!["drehung"]!.GetValue<double>());
        Assert.StartsWith("ifc:", b["koordinaten"]!["text"]!.GetValue<string>());
        var bp = Export(p, pfadProjekt, fassung, "projekt");
        Assert.Equal("projekt", bp["koordinaten"]!["art"]!.GetValue<string>());
        Assert.Null(bp["koordinaten"]!["bezug"]);

        // Welt = DrehungZ(90°) · Projekt + (1250, −500, 300): RO-1 (1500, 0, 1000) → (−0, 1500) + … = (1250, 1000, 1300); Achse (0, 1, 0) → (−1, 0, 0)
        // KA-2 (3000, 2000, 2875) → (−2000, 3000, 2875) + … = (−750, 2500, 3175); Achse (0, 0, 1) bleibt
        using (var d = IfcDatei.Oeffnen(pfadIfc))
        {
            Assert.Equal(fassung, d.Schema);
            var r1 = Proxy(d, Gid(p, "RO-1")); var k2 = Proxy(d, Gid(p, "KA-2"));
            Nahe([1250, 1000, 1300], d.Welt(r1.ObjectPlacement).Ursprung, 0.1, "RO-1 Welt");
            Nahe([-1, 0, 0], d.Welt(r1.ObjectPlacement).AchseZ, 1e-9, "RO-1 Achse Welt");
            Nahe([-750, 2500, 3175], d.Welt(k2.ObjectPlacement).Ursprung, 0.1, "KA-2 Welt");
            Nahe([0, 0, 1], d.Welt(k2.ObjectPlacement).AchseZ, 1e-9, "KA-2 Achse Welt");
            // wie der Import: Gebäudeursprung = Bezug, damit zurück in die Projektkoordinaten
            var v = d.BezugVorschlag();
            Nahe([1250, -500, 300], [v.X, v.Y, v.Z], 0.1, "Gebäudeursprung");
            Assert.Equal(90, v.Drehung, 6);
            d.Bezug = v;
            Nahe([1500, 0, 1000], d.Lage(r1).Ursprung, 0.1, "RO-1 zurück in Projektkoordinaten");
            Nahe([3000, 2000, 2875], d.Lage(k2).Ursprung, 0.1, "KA-2 zurück in Projektkoordinaten");
            Assert.Equal([0], d.Geschosse().Select(g => g.KoteMm));        // alle drei im EG (Geschoss unter der Decke)
            Assert.Equal("ifc", Text(d.Eigenschaften(r1), "Koordinatenbezug"));
        }
        // ohne Bezug: Welt = Projekt
        using (var d = IfcDatei.Oeffnen(pfadProjekt))
        {
            var r1 = Proxy(d, Gid(p, "RO-1"));
            Nahe([1500, 0, 1000], d.Welt(r1.ObjectPlacement).Ursprung, 0.1, "RO-1 projekt");
            Nahe([0, 1, 0], d.Welt(r1.ObjectPlacement).AchseZ, 1e-9, "RO-1 Achse projekt");
            Assert.Equal("projekt", Text(d.Eigenschaften(r1), "Koordinatenbezug"));
        }
    }

    [Fact]
    public void Koordinaten_OhneBezug_VorgabeProjektMitWarnung_AusdruecklichIfcLehntAb()
    {
        var p = FallFrei();
        var b = Export(p, Pfad("a.ifc"));
        Assert.Equal("projekt", b["koordinaten"]!["art"]!.GetValue<string>());
        Assert.Contains(b["warnungen"]!.AsArray(), w => w!.GetValue<string>().Contains("keinen IFC-Bezug"));
        var f = Assert.Throws<HlksFehler>(() => Export(p, Pfad("b.ifc"), koordinaten: "ifc"));
        Assert.Contains("ifcBezug", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.False(File.Exists(Pfad("b.ifc")));
        var u = Assert.Throws<HlksFehler>(() => Export(p, Pfad("c.ifc"), koordinaten: "lv95"));
        Assert.Contains("lv95", u.Message);
    }

    [Fact]
    public void Koordinaten_ImportierteArchitekten_IfcIstVorgabe_BauteilMitGlobalId_GeschossAusDerIfc()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, X.Datei("ohne")));
        Serie(p); Vorgaben(p);
        var wand = p.Modell.Elemente.Single(e => e.Kennungen?.GetValueOrDefault("ifc") == X.Gid("W-X"));
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 50", [1500, -500, 1200], [1500, 700, 1200]));
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal(wand.Id, a.Bezuege["bauteil"]);
        Ok(Status(p, "freigegeben", a.Kennzeichen));
        var pfad = Pfad("arch.ifc");
        var b = Export(p, pfad);
        Assert.Equal("ifc", b["koordinaten"]!["art"]!.GetValue<string>());   // die Architekten-IFC hat den Projektbezug gesetzt
        using var d = IfcDatei.Oeffnen(pfad);
        var px = Proxy(d, Gid(p, "RO-1"));
        Assert.Equal(X.Gid("W-X"), Text(d.Eigenschaften(px), "Bauteil"));       // GlobalId des gequerten Bauteils
        Assert.Equal(wand.Kennzeichen, Text(d.Eigenschaften(px), "BauteilKennzeichen"));
        var g = p.Modell.Finde(a.Geschoss)!;
        Assert.Equal(g.Kennzeichen, d.GeschossVon(px)!.Quellen[0].Name?.ToString());
        Assert.Equal(g.Zahl("kote")!.Value, d.Geschosse().Single().KoteMm, 0.1);
        // Lage in der Welt der Architekten-IFC = wie im Modell der Architekten (Gebäude liegt dort im Ursprung)
        Nahe([1500, 100, 1200], d.Welt(px.ObjectPlacement).Ursprung, 0.1, "Welt");
    }

    [Fact]
    public void Bauteil_Hintergrundprodukt_GlobalIdAusDemBezug()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, N.Datei("IFC4"), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));   // Architektur bleibt Hintergrund
        Serie(p); Vorgaben(p);
        Ok(Modul3Hilfe.Rohr(p, "RO-2", "DN 20", [2500, -500, 1300], [2500, 700, 1300]));
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-2");
        Assert.False(a.Bezuege.ContainsKey("bauteil"));
        Ok(Status(p, "freigegeben", a.Kennzeichen));
        var pfad = Pfad("hg.ifc");
        Export(p, pfad, "IFC2X3");
        using var d = IfcDatei.Oeffnen(pfad);
        var eig = d.Eigenschaften(Proxy(d, Gid(p, "RO-2")));
        Assert.Equal(N.Gid("W-A"), Text(eig, "Bauteil"));
        Assert.Equal("IfcWall", Text(eig, "BauteilEntitaet"));
    }

    // ------------------------------------------------------------------ GlobalId

    [Fact]
    public void GlobalId_StabilUeberFassungenNummernUndMasse_UndDokumentierteRegel()
    {
        var p = FallFrei();
        var b1 = Export(p, Pfad("1.ifc"), "IFC4", "projekt");
        var ids1 = b1["aussparungen"]!.AsArray().ToDictionary(x => x!["aussparung"]!.GetValue<string>(), x => x!["globalId"]!.GetValue<string>());
        Assert.Equal(3, ids1.Count);
        Assert.Equal(3, ids1.Values.Distinct().Count());
        foreach (var (kz, gid) in ids1)
        {
            Assert.Equal(22, gid.Length);
            Assert.Equal(AussparungIfcExport.AussparungGlobalId(p.Kopf.Id, Hole(p, kz).Id), gid);
            // Regel: UUID v5 aus «<Projekt-Id>/aussparung/<Element-Id>» im KICAD-Namensraum, als IFC-GlobalId
            var uuid = BcfFormat.UuidV5(AussparungIfcExport.Namensraum, $"{p.Kopf.Id}/aussparung/{Hole(p, kz).Id}");
            Assert.Equal(IfcGloballyUniqueId.ConvertToBase64(uuid), gid);
            Assert.Equal(uuid, IfcGloballyUniqueId.ConvertFromBase64(gid));
            Assert.Equal('5', uuid.ToString()[14]);
        }
        // neue Nummern, geänderte Masse, andere Fassung, anderer Dateiname: dieselben GlobalIds (die neue Datei ersetzt dieselben Objekte)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = Kz(p, "RO-1"), ["eigenschaften"] = new JsonObject { ["nummer"] = "X-9", ["tiefe"] = 250, ["d"] = 100 } }));
        var b2 = Export(p, Pfad("2.ifc"), "IFC2X3", "projekt");
        var ids2 = b2["aussparungen"]!.AsArray().ToDictionary(x => x!["aussparung"]!.GetValue<string>(), x => x!["globalId"]!.GetValue<string>());
        Assert.Equal(ids1, ids2);
        using var d = IfcDatei.Oeffnen(Pfad("2.ifc"));
        var px = Proxy(d, ids1[Kz(p, "RO-1")]);
        Assert.Equal("X-9", px.Name?.ToString());
        Assert.Equal(100, Laenge(d, d.Eigenschaften(px), "Durchmesser"), 0.1);
        // GlobalIds der Gliederung sind ebenfalls fest (zweimal geschrieben = gleich)
        string[] gliederung = [.. d.Modell.Instances.OfType<IIfcProject>().Select(x => x.GlobalId.ToString()), .. d.Modell.Instances.OfType<IIfcBuildingStorey>().Select(x => x.GlobalId.ToString()).Order()];
        Export(p, Pfad("3.ifc"), "IFC4", "projekt");
        using var d3 = IfcDatei.Oeffnen(Pfad("3.ifc"));
        string[] gliederung3 = [.. d3.Modell.Instances.OfType<IIfcProject>().Select(x => x.GlobalId.ToString()), .. d3.Modell.Instances.OfType<IIfcBuildingStorey>().Select(x => x.GlobalId.ToString()).Order()];
        Assert.Equal(gliederung, gliederung3);
        // anderes Projekt, gleiche Elemente: andere GlobalIds
        var q = FallFrei();
        Assert.NotEqual(Gid(p, "RO-1"), Gid(q, "RO-1"));
    }

    // ------------------------------------------------------------------ Filter

    [Fact]
    public void Filter_StatusVorgabeFreigegebenUndVersandt_Geschoss_Liste()
    {
        var p = Fall();   // alle vorgeschlagen
        var f = Assert.Throws<HlksFehler>(() => Export(p, Pfad("a.ifc")));
        Assert.Contains("Keine Aussparung passt zum Filter", f.Message);
        Assert.Contains("vorgeschlagen 3", f.Message);
        Assert.Contains("aussparung_status", f.Vorschlag);
        Assert.False(File.Exists(Pfad("a.ifc")));

        Ok(Status(p, "freigegeben", Kz(p, "RO-1"), Kz(p, "KA-1"), Kz(p, "KA-2")));
        Ok(Status(p, "versandt", Kz(p, "KA-2")));
        var alle = Export(p, Pfad("b.ifc"));                                       // Vorgabe: freigegeben und versandt
        Assert.Equal(3, alle["anzahl"]!.GetValue<int>());
        Assert.Equal(["freigegeben", "versandt"], alle["filter"]!["status"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(1, Export(p, Pfad("c.ifc"), status: JsonValue.Create("versandt"))["anzahl"]!.GetValue<int>());
        Assert.Equal(2, Export(p, Pfad("d.ifc"), status: new JsonArray("freigegeben"))["anzahl"]!.GetValue<int>());
        // Geschoss: KA-2 (Decke D-1) liegt im EG (Geschoss unter der Decke) – im OG ist nichts, im EG alle drei
        // (die Meldung lautet heute «Das Projekt hat keine Aussparungen …», weil im OG gar keine liegt – offener Punkt im Bericht B4d)
        var og = Assert.Throws<HlksFehler>(() => Export(p, Pfad("e.ifc"), geschoss: "OG"));
        Assert.Contains("nichts für die IFC-Ausgabe", og.Message);
        Assert.False(File.Exists(Pfad("e.ifc")));
        var eg = Export(p, Pfad("f.ifc"), geschoss: "EG");
        Assert.Equal(3, eg["anzahl"]!.GetValue<int>());
        Assert.Equal("EG", eg["filter"]!["geschoss"]!.GetValue<string>());
        Assert.Single(eg["geschosse"]!.AsArray());
        using (var d = IfcDatei.Oeffnen(Pfad("f.ifc")))
        {
            Assert.Equal(3, d.Modell.Instances.OfType<IIfcBuildingElementProxy>().Count());
            Assert.Single(d.Geschosse());   // nur das gebrauchte Geschoss
            Assert.Contains(d.Modell.Instances.OfType<IIfcBuildingElementProxy>(), x => x.GlobalId.ToString() == Gid(p, "KA-2"));
        }
        // ein anderer Status ausdrücklich
        Ok(Status(p, "vorgeschlagen", Kz(p, "RO-1")));
        var vg = Export(p, Pfad("g.ifc"), status: JsonValue.Create("vorgeschlagen, veraltet"));
        Assert.Equal(1, vg["anzahl"]!.GetValue<int>());
        Assert.Equal("vorgeschlagen", vg["aussparungen"]![0]!["status"]!.GetValue<string>());

        var u = Assert.Throws<HlksFehler>(() => Export(p, Pfad("h.ifc"), status: new JsonArray("fertig")));
        Assert.Contains("«fertig»", u.Message);
        Assert.Contains("freigegeben", u.Vorschlag);
        Assert.Throws<HlksFehler>(() => Export(p, Pfad("i.ifc"), geschoss: "GIBT-ES-NICHT"));
    }

    [Fact]
    public void Filter_VeralteteAusgelassenMitWarnung_FreigegebeneAufVersandtHingewiesen()
    {
        var p = FallFrei();
        var ka1 = Kz(p, "KA-1");
        Ok(Status(p, "veraltet", ka1));
        var b = Export(p, Pfad("a.ifc"));
        Assert.Equal(2, b["anzahl"]!.GetValue<int>());
        var w = b["warnungen"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains(w, t => t.Contains("1 veraltete Aussparung(en) nicht ausgegeben") && t.Contains(ka1));
        Assert.Equal(1, b["filter"]!["nichtAusgegeben"]!.GetValue<int>());
        Assert.Contains(b["hinweise"]!.AsArray(), x => x!.GetValue<string>().Contains("2 Aussparung(en) stehen noch auf «freigegeben»") && x.GetValue<string>().Contains("aussparung_status"));
        // ausdrücklich veraltet verlangt: wird ausgegeben
        var v = Export(p, Pfad("b.ifc"), status: new JsonArray("veraltet"));
        Assert.Equal(1, v["anzahl"]!.GetValue<int>());
        Assert.Equal("veraltet", v["jeStatus"]!.AsObject().Single().Key);
    }

    // ------------------------------------------------------------------ Bericht und Nebenwirkungen

    [Fact]
    public void Bericht_AnzahlJeArtUndGeschoss_Fassung_Koordinaten_Rundreise()
    {
        var p = FallFrei();
        var b = Export(p, Pfad("a.ifc"), "ifc2x3", "projekt");   // Schreibweise egal
        Assert.Equal(Pfad("a.ifc"), b["datei"]!.GetValue<string>());
        Assert.Equal("IFC2X3", b["fassung"]!.GetValue<string>());
        Assert.Equal("IfcBuildingElementProxy", b["entitaet"]!.GetValue<string>());
        Assert.Equal("ObjectType «ProvisionForVoid»", b["kennzeichnung"]!.GetValue<string>());
        Assert.Equal("mm", b["einheit"]!.GetValue<string>());
        Assert.True(b["groesseBytes"]!.GetValue<long>() > 1000);
        Assert.Equal(3, b["anzahl"]!.GetValue<int>());
        Assert.Equal(1, b["jeArt"]!["kernbohrung"]!.GetValue<int>());
        Assert.Equal(2, b["jeArt"]!["aussparung"]!.GetValue<int>());
        Assert.Equal(3, b["jeGeschoss"]!["EG"]!.GetValue<int>());           // KA-2 (Decke D-1) im Geschoss unter der Decke
        Assert.Null(b["jeGeschoss"]!["OG"]);
        Assert.Equal(3, b["jeStatus"]!["freigegeben"]!.GetValue<int>());
        Assert.Equal(["EG"], b["geschosse"]!.AsArray().Select(x => x!["geschoss"]!.GetValue<string>()));
        Assert.Equal([0.0], b["geschosse"]!.AsArray().Select(x => x!["kote"]!.GetValue<double>()));
        Assert.Contains("UUID v5", b["globalId"]!.GetValue<string>());
        var r = b["rundreise"]!;
        Assert.Equal("bestanden", r["ergebnis"]!.GetValue<string>());
        Assert.Equal("IFC2X3", r["fassungGelesen"]!.GetValue<string>());
        Assert.Equal("mm", r["einheitGelesen"]!.GetValue<string>());
        Assert.Equal(3, r["aussparungenGeprueft"]!.GetValue<int>());
        Assert.Equal(1, r["geschosseGeprueft"]!.GetValue<int>());
        Assert.True(r["eigenschaftenGeprueft"]!.GetValue<int>() > 30);
        Assert.Empty(r["abweichungen"]!.AsArray());
        Assert.Empty(b["warnungen"]!.AsArray().Where(x => !x!.GetValue<string>().Contains("IFC-Bezug")));
        var ia = (JsonObject)Tuer.Abfrage(p, "aussparungen_ifc", new JsonObject { ["datei"] = Pfad("a.ifc"), ["fassung"] = "IFC4" });
        Assert.Equal("PredefinedType PROVISIONFORVOID", ia["kennzeichnung"]!.GetValue<string>());
    }

    [Fact]
    public void Warnungen_OhneNummer_NameIstDasKennzeichen()
    {
        var p = Wandprojekt();
        RohrDurchW1(p, "RO-1", 1500);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Ok(Status(p, "freigegeben", a.Kennzeichen));
        var pfad = Pfad("a.ifc");
        var b = Export(p, pfad);
        Assert.Contains(b["warnungen"]!.AsArray(), w => w!.GetValue<string>().Contains($"{a.Kennzeichen} hat keine Nummer"));
        using var d = IfcDatei.Oeffnen(pfad);
        var px = Proxy(d, Gid(p, "RO-1"));
        Assert.Equal(a.Kennzeichen, px.Name?.ToString());
        Assert.False(d.Eigenschaften(px).ContainsKey("KICAD_Aussparung.Nummer"));
    }

    [Fact]
    public void Abfrage_AendertDasModellNicht_ErsetztDieDatei_KeineTempReste()
    {
        var p = FallFrei();
        int pakete = p.Verlauf.Count;
        var vorher = p.Modell.Elemente.Select(e => (e.Id, e.Version)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var pfad = Pfad("sub\\ordner\\a.ifc");
        Export(p, pfad);
        var gross = new FileInfo(pfad).Length;
        File.WriteAllText(pfad, "alte Datei");
        Export(p, pfad);
        Assert.Equal(gross, new FileInfo(pfad).Length);   // ersetzt
        Assert.Equal(pakete, p.Verlauf.Count);
        Assert.Equal(vorher, p.Modell.Elemente.Select(e => (e.Id, e.Version)).OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
        Assert.Equal([pfad], Directory.GetFiles(Path.GetDirectoryName(pfad)!));
        Assert.Equal("IFC4", IfcDatei.FassungAusKopf(pfad));
    }

    [Fact]
    public void Fehler_EndungFassungDatei()
    {
        var p = FallFrei();
        var f1 = Assert.Throws<HlksFehler>(() => Export(p, Pfad("a.txt")));
        Assert.Contains(".ifc", f1.Message);
        var f2 = Assert.Throws<HlksFehler>(() => Export(p, Pfad("a.ifc"), "IFC4X3"));
        Assert.Contains("IFC4X3", f2.Message);
        Assert.Contains("IFC2X3", f2.Vorschlag);
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "aussparungen_ifc", new JsonObject()));   // datei ist Pflicht
        var leer = Wandprojekt();
        var f3 = Assert.Throws<HlksFehler>(() => Export(leer, Pfad("b.ifc")));
        Assert.Contains("keine Aussparungen", f3.Message);
        Assert.Empty(Directory.GetFiles(ordner, "*.ifc"));
    }

    [Fact]
    public void Abfrage_StehtInDerListeMitParametern()
    {
        var a = Assert.Single(Abfragen.Liste, x => x.Name == "aussparungen_ifc");
        Assert.Equal(["datei", "fassung", "geschoss", "status", "koordinaten"], a.Parameter.Select(x => x.Name));
        Assert.True(a.Parameter[0].Pflicht);
        Assert.All(a.Parameter.Skip(1), x => Assert.False(x.Pflicht));
        Assert.Equal(AussparungIfcTexte.Export, a.Beschreibung);
        // kein eigener Befehl zum Versenden nötig: aussparung_status setzt freigegeben → versandt
        Assert.DoesNotContain(Projekt.Anlegen(Kat, null, "x", "x", "x").Befehle.Alle, b => b.Name == "aussparungen_versenden");
    }

    // ------------------------------------------------------------------ Echt-Test Bernstrasse 42 (lokal)

    [Fact]
    public void Bernstrasse42_Aussparungen_AlsIfc4UndIfc2x3_Rundreise()
    {
        if (IfcHilfe.Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } hlks)
        {
            return;   // echte Datei fehlt auf diesem Rechner: Test ohne Prüfung beendet (Muster der Echt-Tests)
        }
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, hlks, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
        Vorgaben(p);
        Ok(Vorschlagen(p));
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        var alle = Aussparungen(p);
        if (alle.Count == 0) return;
        Ok(Status(p, "freigegeben", alle.Select(a => a.Kennzeichen).ToArray()));
        var zahlen = new JsonObject { ["aussparungen"] = alle.Count };
        foreach (var f in new[] { "IFC4", "IFC2X3" })
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var b = Export(p, Pfad($"bern_{f}.ifc"), f);
            double s = uhr.Elapsed.TotalSeconds;
            Assert.Equal("bestanden", b["rundreise"]!["ergebnis"]!.GetValue<string>());
            Assert.Equal(alle.Count(a => Aussparungslage.Von(a) is { Tiefe: > 0 }), b["anzahl"]!.GetValue<int>());
            Assert.Equal("ifc", b["koordinaten"]!["art"]!.GetValue<string>());
            Assert.True(s < 120, $"{f}: {s:0.0} s");
            zahlen[f] = new JsonObject
            {
                ["sekunden"] = Math.Round(s, 2), ["bytes"] = b["groesseBytes"]!.DeepClone(), ["anzahl"] = b["anzahl"]!.DeepClone(), ["jeArt"] = b["jeArt"]!.DeepClone(),
                ["jeGeschoss"] = b["jeGeschoss"]!.DeepClone(), ["koordinaten"] = b["koordinaten"]!["bezug"]!.DeepClone(), ["entitaeten"] = b["rundreise"]!["entitaeten"]!.DeepClone(),
                ["warnungen"] = b["warnungen"]!.DeepClone(),
            };
        }
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "b4b_aussparungen_ifc_echt.json");
        File.WriteAllText(datei, zahlen.ToJsonString(Json.Eingerueckt));
        aus.WriteLine(zahlen.ToJsonString(Json.Eingerueckt));
    }
}
