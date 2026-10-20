using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using static Hlks.Kern.Tests.IfcExportHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B2: IDS 1.0 – Schema (schema/ids-1.0, unverändert), IDS-Basis von H+K (kataloge/ids/hk-hlks-basis.ids) und der eigene
/// Prüfer je Facette (entity mit PredefinedType und USERDEFINED, attribute, property mit Datentyp und Meter-Umrechnung, classification, partOf
/// rekursiv und mit Beziehung, material), Einschränkungen (Aufzählung, Muster, Grenzen, Länge), Kardinalitäten der Spezifikation und der
/// Anforderungen, IFC2X3-Zuordnung über das Typobjekt. Geprüft wird das exportierte Beispielprojekt «MFH Musterweg» (fiktiv).</summary>
public sealed class IdsPrueferTests : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    /// <summary>IDS-Datei mit Kopf und den gegebenen Spezifikationen.</summary>
    public static string Ids(string spezifikationen) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ids:ids xmlns:ids="http://standards.buildingsmart.org/IDS" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <ids:info><ids:title>Test (fiktiv)</ids:title></ids:info>
          <ids:specifications>
        {spezifikationen}
          </ids:specifications>
        </ids:ids>
        """;

    static string Spez(string name, string anwendbarkeit, string? anforderungen = null, string fassung = "IFC2X3 IFC4", string occurs = "minOccurs=\"0\" maxOccurs=\"unbounded\"") =>
        $"""<ids:specification name="{name}" ifcVersion="{fassung}"><ids:applicability {occurs}>{anwendbarkeit}</ids:applicability>{(anforderungen is null ? "" : $"<ids:requirements>{anforderungen}</ids:requirements>")}</ids:specification>""";

    static string Ent(string name, string? pt = null) =>
        $"<ids:entity><ids:name><ids:simpleValue>{name}</ids:simpleValue></ids:name>{(pt is null ? "" : $"<ids:predefinedType><ids:simpleValue>{pt}</ids:simpleValue></ids:predefinedType>")}</ids:entity>";

    static string Eig(string set, string name, string? wert = null, string? typ = null, string kard = "required") =>
        $"<ids:property{(typ is null ? "" : $" dataType=\"{typ}\"")} cardinality=\"{kard}\"><ids:propertySet><ids:simpleValue>{set}</ids:simpleValue></ids:propertySet><ids:baseName><ids:simpleValue>{name}</ids:simpleValue></ids:baseName>{(wert is null ? "" : $"<ids:value>{wert}</ids:value>")}</ids:property>";

    static string EigAnw(string set, string name, string wert) =>
        $"<ids:property><ids:propertySet><ids:simpleValue>{set}</ids:simpleValue></ids:propertySet><ids:baseName><ids:simpleValue>{name}</ids:simpleValue></ids:baseName><ids:value><ids:simpleValue>{wert}</ids:simpleValue></ids:value></ids:property>";

    static readonly Dictionary<string, string> exportiert = [];
    static readonly Lazy<Projekt> beispiel = new(() => Beispielprojekt.Erzeugen(IfcHilfe.Kat, null));

    /// <summary>Beispielprojekt einmal je Fassung exportieren (Temp-Ordner des Prozesses).</summary>
    static string Datei(string fassung)
    {
        lock (exportiert)
        {
            if (exportiert.TryGetValue(fassung, out var d) && File.Exists(d)) return d;
            var o = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"ids_{Environment.ProcessId}");
            Directory.CreateDirectory(o);
            d = Path.Combine(o, $"beispiel_{fassung}.ifc");
            Export(beispiel.Value, d, fassung);
            return exportiert[fassung] = d;
        }
    }

    JsonObject Pruefen(string fassung, string spezifikationen)
    {
        var ids = ordner.Datei(Guid.NewGuid().ToString("N")[..8] + ".ids");
        File.WriteAllText(ids, Ids(spezifikationen));
        var doc = IdsLeser.Lesen(ids, IfcHilfe.Kat);
        Assert.True(doc.Gueltig, string.Join(" | ", doc.XsdMeldungen));
        using var d = IfcDatei.Oeffnen(Datei(fassung));
        return new IdsPruefer(d).Pruefen(doc);
    }

    static JsonObject R(JsonObject e, string name) => e["regeln"]!.AsArray().OfType<JsonObject>().Single(r => r["name"]!.GetValue<string>() == name);
    static string Erg(JsonObject e, string name) => R(e, name)["ergebnis"]!.GetValue<string>();
    static int Anw(JsonObject e, string name) => R(e, name)["anwendbar"]!.GetValue<int>();

    // ------------------------------------------------------------------ Schema und IDS-Basis

    [Fact]
    public void Schema_IdsXsdUnveraendert_PruefsummeWieImReadme()
    {
        var ordnerSchema = IdsSchema.Ordner(IfcHilfe.Kat);
        Assert.NotNull(ordnerSchema);
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(ordnerSchema!, "ids.xsd"))));
        Assert.Equal("8975DC18BD18F08A345A430A22BF317A54D94671B7DB01400042FA43B6C0D1F3", sha);
        Assert.Contains(sha, File.ReadAllText(Path.Combine(ordnerSchema!, "README.md")));
        Assert.Equal("1.0.0", XDocument.Load(Path.Combine(ordnerSchema!, "ids.xsd")).Root!.Attribute("version")!.Value);
        Assert.True(File.Exists(Path.Combine(ordnerSchema!, "LICENSE.buildingSMART.txt")));
    }

    [Fact]
    public void Schema_UngueltigeIdsWirdGemeldet_GueltigeNicht()
    {
        var gut = ordner.Datei("gut.ids");
        File.WriteAllText(gut, Ids(Spez("a", Ent("IFCWALL"))));
        Assert.Empty(IdsLeser.Lesen(gut, IfcHilfe.Kat).XsdMeldungen);
        var schlecht = ordner.Datei("schlecht.ids");
        File.WriteAllText(schlecht, Ids("""<ids:specification name="b" ifcVersion="IFC5"><ids:applicability><ids:bogus/></ids:applicability></ids:specification>"""));
        var doc = IdsLeser.Lesen(schlecht, IfcHilfe.Kat);
        Assert.False(doc.Gueltig);
        Assert.Contains(doc.XsdMeldungen, m => m.Contains("ifcVersion"));
        Assert.Contains(doc.XsdMeldungen, m => m.Contains("bogus"));
        var keinXml = ordner.Datei("kaputt.ids");
        File.WriteAllText(keinXml, "<ids:ids");
        Assert.Throws<HlksFehler>(() => IdsLeser.Lesen(keinXml, IfcHilfe.Kat));
        Assert.Throws<HlksFehler>(() => IdsLeser.Lesen(ordner.Datei("fehlt.ids"), IfcHilfe.Kat));
    }

    [Fact]
    public void Basis_IstGueltigGegenDasXsd_AchtRegeln()
    {
        var pfad = IfcExport.IdsBasisPfad(IfcHilfe.Kat);
        Assert.NotNull(pfad);
        var doc = IdsLeser.Lesen(pfad!, IfcHilfe.Kat);
        Assert.True(doc.XsdGeprueft);
        Assert.True(doc.Gueltig, string.Join(" | ", doc.XsdMeldungen));
        Assert.Equal("H+K HLKS-Basis", doc.Titel);
        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"HK-HLKS-0{i}"), doc.Spezifikationen.Select(s => s.Kennung));
        Assert.All(doc.Spezifikationen, s => Assert.Equal("optional", s.Kardinalitaet));
    }

    /// <summary>Die Entitätslisten der IDS-Basis sind genau die IFC4-Entitäten der HLKS-Klassen in ifc.json (Klassen mit Rolle im Netz, mit
    /// Lüftung und Varianten); die Systemarten genau die der systemtypen plus CONTROL (Regelkreis).</summary>
    [Fact]
    public void Basis_ListenEntsprechenIfcJson()
    {
        var abb = IfcHilfe.Kat.Ifc!;
        var hlks = abb.Klassen.Where(k => IfcHlksKlasse.IstHlks(IfcHilfe.Kat, k.Klasse)).SelectMany(k => k.Ziele("IFC4")).Select(z => z.Ziel.Entitaet.ToUpperInvariant()).ToHashSet();
        var doc = IdsLeser.Lesen(IfcExport.IdsBasisPfad(IfcHilfe.Kat)!, IfcHilfe.Kat);
        foreach (var k in new[] { "HK-HLKS-03", "HK-HLKS-04", "HK-HLKS-05", "HK-HLKS-08" })
        {
            var e = doc.Spezifikationen.Single(s => s.Kennung == k).Anwendbarkeit.OfType<IdsEntitaet>().Single();
            Assert.Equal(hlks.Order(), e.Name.Aufzaehlung.Order());
        }
        var arten = abb.Systemtypen.Select(s => s.Ifc4).Append(abb.Klasse("Regelkreis")!.Ifc4!.PredefinedType!).Distinct().Order();
        var r6 = doc.Spezifikationen.Single(s => s.Kennung == "HK-HLKS-06").Anforderungen!.OfType<IdsEntitaet>().Single();
        Assert.Equal(arten, r6.Vordefiniert!.Aufzaehlung.Order());
        // Apparate: alle Entitäten der Klassen mit Rolle verbraucher, zentrale, quelle oder senke ausser Formteilen (Hausanschluss, Verteiler)
        var geraete = abb.Klassen.Where(k => IfcHilfe.Kat.Klassen.TryGetValue(k.Klasse, out var d) && d.Rolle is "verbraucher" or "zentrale" or "senke")
            .SelectMany(k => k.Ziele("IFC4")).Select(z => z.Ziel.Entitaet.ToUpperInvariant()).Where(n => n != "IFCPIPEFITTING").ToHashSet();
        var r7 = doc.Spezifikationen.Single(s => s.Kennung == "HK-HLKS-07").Anwendbarkeit.OfType<IdsEntitaet>().Single();
        Assert.Equal(geraete.Order(), r7.Name.Aufzaehlung.Order());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Basis_BestehtFuerDasBeispielprojekt(string fassung)
    {
        using var d = IfcDatei.Oeffnen(Datei(fassung));
        var e = new IdsPruefer(d).Pruefen(IdsLeser.Lesen(IfcExport.IdsBasisPfad(IfcHilfe.Kat)!, IfcHilfe.Kat));
        Assert.Equal("bestanden", e["ergebnis"]!.GetValue<string>());
        Assert.Equal(0, e["regelnNichtBestanden"]!.GetValue<int>());
        Assert.Equal(71, e["regeln"]!.AsArray().OfType<JsonObject>().Single(r => r["kennung"]!.GetValue<string>() == "HK-HLKS-01")["anwendbar"]!.GetValue<int>());
        // nicht anwendbar: die Systemregel der anderen Fassung, in IFC2X3 dazu die Systemart (IfcSystem hat dort keinen PredefinedType)
        Assert.Equal(fassung == "IFC4" ? 1 : 2, e["regelnNichtAnwendbar"]!.GetValue<int>());
    }

    // ------------------------------------------------------------------ Facetten

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Entity_PredefinedType_UndUserdefinedUeberObjectType_UndIfc2x3Zuordnung(string fassung)
    {
        var e = Pruefen(fassung,
            Spez("Absperrventile", Ent("IFCVALVE", "ISOLATING")) +
            Spez("Magnetventil", Ent("IFCVALVE", "magnetventil")) +
            Spez("Ventile", Ent("IFCVALVE")) +
            Spez("Rohre", Ent("IFCPIPESEGMENT")) +
            Spez("Apparate", Ent("IFCSANITARYTERMINAL")) +
            Spez("WC", Ent("IFCSANITARYTERMINAL", "TOILETPAN"), Ent("IFCSANITARYTERMINAL", "TOILETPAN")) +
            Spez("Ventile sind Absperrventile", Ent("IFCVALVE"), Ent("IFCVALVE", "ISOLATING")));
        Assert.Equal(3, Anw(e, "Absperrventile"));
        Assert.Equal(1, Anw(e, "Magnetventil"));
        Assert.Equal(4, Anw(e, "Ventile"));
        Assert.Equal(71, Anw(e, "Rohre"));
        Assert.Equal(14, Anw(e, "Apparate"));
        Assert.Equal(4, Anw(e, "WC"));
        Assert.Equal("bestanden", Erg(e, "WC"));
        Assert.Equal("nicht bestanden", Erg(e, "Ventile sind Absperrventile"));
        Assert.Equal(["MV-1"], Befunde(R(e, "Ventile sind Absperrventile")));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Property_DatentypWertInMeternGrenzenUndKardinalitaet(string fassung)
    {
        string anw = Ent("IFCPIPESEGMENT");
        var e = Pruefen(fassung,
            Spez("DN zwischen 15 und 125 mm", anw, Eig("Pset_PipeSegmentTypeCommon", "NominalDiameter", """<xs:restriction base="xs:double"><xs:minInclusive value="0.015"/><xs:maxInclusive value="0.125"/></xs:restriction>""", "IFCPOSITIVELENGTHMEASURE")) +
            Spez("SW-F1-1 DN 100 = 0.1 m", anw + EigAnw("HK_KICAD", "Kennzeichen", "SW-F1-1"), Eig("Pset_PipeSegmentTypeCommon", "NominalDiameter", "<ids:simpleValue>0.1</ids:simpleValue>")) +
            Spez("SW-F1-1 nicht 100 m", anw + EigAnw("HK_KICAD", "Kennzeichen", "SW-F1-1"), Eig("Pset_PipeSegmentTypeCommon", "NominalDiameter", "<ids:simpleValue>100</ids:simpleValue>")) +
            Spez("falscher Datentyp", anw, Eig("Pset_PipeSegmentTypeCommon", "NominalDiameter", typ: "IFCLABEL")) +
            Spez("Familie verboten", anw, Eig("HK_KICAD", "Familie", kard: "prohibited")) +
            Spez("Kennzeichen verboten", anw, Eig("HK_KICAD", "Kennzeichen", kard: "prohibited")) +
            Spez("optional falsch", anw, Eig("Pset_PipeSegmentTypeCommon", "NominalDiameter", "<ids:simpleValue>0.999</ids:simpleValue>", kard: "optional")) +
            Spez("optional fehlt", anw, Eig("Pset_Gibts", "Nicht", "<ids:simpleValue>1</ids:simpleValue>", kard: "optional")) +
            Spez("Länge Qto in m", anw + EigAnw("HK_KICAD", "Kennzeichen", "KW-V1-2"), Eig("Qto_PipeSegmentBaseQuantities", "Length", "<ids:simpleValue>8.4</ids:simpleValue>", "IFCLENGTHMEASURE")) +
            Spez("Muster auf Set und Name", anw, """<ids:property cardinality="required"><ids:propertySet><xs:restriction base="xs:string"><xs:pattern value="Pset_.*SegmentTypeCommon"/></xs:restriction></ids:propertySet><ids:baseName><xs:restriction base="xs:string"><xs:pattern value="Nominal.*"/></xs:restriction></ids:baseName></ids:property>"""));
        Assert.Equal("bestanden", Erg(e, "DN zwischen 15 und 125 mm"));
        Assert.Equal(1, Anw(e, "SW-F1-1 DN 100 = 0.1 m"));
        Assert.Equal("bestanden", Erg(e, "SW-F1-1 DN 100 = 0.1 m"));
        Assert.Equal("nicht bestanden", Erg(e, "SW-F1-1 nicht 100 m"));
        Assert.Equal("nicht bestanden", Erg(e, "falscher Datentyp"));
        Assert.Equal("bestanden", Erg(e, "Familie verboten"));
        Assert.Equal("nicht bestanden", Erg(e, "Kennzeichen verboten"));
        Assert.Equal(71, R(e, "Kennzeichen verboten")["nichtBestandenElemente"]!.GetValue<int>());
        Assert.Equal("nicht bestanden", Erg(e, "optional falsch"));
        Assert.Equal("bestanden", Erg(e, "optional fehlt"));
        Assert.Equal("bestanden", Erg(e, "Länge Qto in m"));   // Polylinie 1900 + 6500 mm
        Assert.Equal("bestanden", Erg(e, "Muster auf Set und Name"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Attribute_Klassifikation_Material(string fassung)
    {
        string sw = Ent("IFCPIPESEGMENT") + EigAnw("HK_KICAD", "System", "SW");
        var e = Pruefen(fassung,
            Spez("Namen KW", Ent("IFCPIPESEGMENT") + EigAnw("HK_KICAD", "System", "KW"), """<ids:attribute><ids:name><ids:simpleValue>Name</ids:simpleValue></ids:name><ids:value><xs:restriction base="xs:string"><xs:pattern value="KW-.*"/></xs:restriction></ids:value></ids:attribute>""") +
            Spez("Beschreibung fehlt", Ent("IFCPIPESEGMENT"), """<ids:attribute><ids:name><ids:simpleValue>Description</ids:simpleValue></ids:name></ids:attribute>""") +
            Spez("Tag Länge", Ent("IFCPUMP"), """<ids:attribute><ids:name><ids:simpleValue>Tag</ids:simpleValue></ids:name><ids:value><xs:restriction base="xs:string"><xs:length value="7"/></xs:restriction></ids:value></ids:attribute>""") +
            Spez("BKP 251 Apparate", Ent("IFCSANITARYTERMINAL"), """<ids:classification><ids:value><ids:simpleValue>251</ids:simpleValue></ids:value><ids:system><ids:simpleValue>BKP</ids:simpleValue></ids:system></ids:classification>""") +
            Spez("BKP 254 Apparate", Ent("IFCSANITARYTERMINAL"), """<ids:classification><ids:value><ids:simpleValue>254</ids:simpleValue></ids:value><ids:system><ids:simpleValue>BKP</ids:simpleValue></ids:system></ids:classification>""") +
            Spez("Uniclass verboten", Ent("IFCSANITARYTERMINAL"), """<ids:classification cardinality="prohibited"><ids:system><ids:simpleValue>Uniclass 2015</ids:simpleValue></ids:system></ids:classification>""") +
            Spez("PE im Schmutzwasser", sw, """<ids:material><ids:value><ids:simpleValue>PE</ids:simpleValue></ids:value></ids:material>""") +
            Spez("Rohre aus PE", Ent("IFCPIPESEGMENT") + """<ids:material><ids:value><ids:simpleValue>PE</ids:simpleValue></ids:value></ids:material>"""));
        Assert.Equal("bestanden", Erg(e, "Namen KW"));
        Assert.True(Anw(e, "Namen KW") > 20);
        Assert.Equal("nicht bestanden", Erg(e, "Beschreibung fehlt"));
        Assert.Equal("bestanden", Erg(e, "Tag Länge"));   // P-KAE-1
        Assert.Equal("bestanden", Erg(e, "BKP 251 Apparate"));
        Assert.Equal("nicht bestanden", Erg(e, "BKP 254 Apparate"));
        Assert.Equal("bestanden", Erg(e, "Uniclass verboten"));
        Assert.Equal("bestanden", Erg(e, "PE im Schmutzwasser"));
        Assert.Equal(Anw(e, "PE im Schmutzwasser"), Anw(e, "Rohre aus PE"));
    }

    [Fact]
    public void PartOf_RekursivUndMitBeziehung_PortsInIfcRelNests()
    {
        var e = Pruefen("IFC4",
            Spez("im Gebäude", Ent("IFCPIPESEGMENT"), """<ids:partOf><ids:entity><ids:name><ids:simpleValue>IFCBUILDING</ids:simpleValue></ids:name></ids:entity></ids:partOf>""", "IFC4") +
            Spez("im Gebäude enthalten", Ent("IFCPIPESEGMENT"), """<ids:partOf relation="IFCRELCONTAINEDINSPATIALSTRUCTURE"><ids:entity><ids:name><ids:simpleValue>IFCBUILDING</ids:simpleValue></ids:name></ids:entity></ids:partOf>""", "IFC4") +
            Spez("Ports am Rohr", Ent("IFCDISTRIBUTIONPORT") + """<ids:partOf relation="IFCRELNESTS"><ids:entity><ids:name><ids:simpleValue>IFCPUMP</ids:simpleValue></ids:name></ids:entity></ids:partOf>""",
                 """<ids:attribute><ids:name><ids:simpleValue>FlowDirection</ids:simpleValue></ids:name><ids:value><xs:restriction base="xs:string"><xs:enumeration value="SINK"/><xs:enumeration value="SOURCE"/></xs:restriction></ids:value></ids:attribute>""", "IFC4") +
            Spez("Systeme in Anlage", Ent("IFCDISTRIBUTIONSYSTEM", "WASTEWATER"), """<ids:partOf relation="IFCRELASSIGNSTOGROUP"><ids:entity><ids:name><ids:simpleValue>IFCSYSTEM</ids:simpleValue></ids:name></ids:entity></ids:partOf>""", "IFC4") +
            Spez("Rohre im Strang", Ent("IFCPIPESEGMENT"), """<ids:partOf relation="IFCRELASSIGNSTOGROUP"><ids:entity><ids:name><ids:simpleValue>IFCDISTRIBUTIONCIRCUIT</ids:simpleValue></ids:name></ids:entity></ids:partOf>""", "IFC4") +
            Spez("nicht im Raum", Ent("IFCPIPESEGMENT"), """<ids:partOf cardinality="prohibited"><ids:entity><ids:name><ids:simpleValue>IFCSPACE</ids:simpleValue></ids:name></ids:entity></ids:partOf>""", "IFC4"));
        Assert.Equal("bestanden", Erg(e, "im Gebäude"));                  // Rohr → Geschoss (enthalten) → Gebäude (Aggregat)
        Assert.Equal("nicht bestanden", Erg(e, "im Gebäude enthalten"));  // nur über Enthaltensein: das Geschoss, nicht das Gebäude
        Assert.Equal(2, Anw(e, "Ports am Rohr"));                          // Pumpe P-KAE-1: saug, druck
        Assert.Equal("bestanden", Erg(e, "Ports am Rohr"));
        Assert.Equal("bestanden", Erg(e, "Systeme in Anlage"));
        Assert.Equal(1, Anw(e, "Systeme in Anlage"));
        Assert.Equal("bestanden", Erg(e, "Rohre im Strang"));
        Assert.Equal("bestanden", Erg(e, "nicht im Raum"));
    }

    [Fact]
    public void Kardinalitaet_DerSpezifikation_RequiredOptionalProhibited_UndFassung()
    {
        var e = Pruefen("IFC4",
            Spez("Kanal verlangt", Ent("IFCDUCTSEGMENT"), occurs: "minOccurs=\"1\" maxOccurs=\"unbounded\"") +
            Spez("Kanal verboten", Ent("IFCDUCTSEGMENT"), occurs: "minOccurs=\"0\" maxOccurs=\"0\"") +
            Spez("Pumpe verboten", Ent("IFCPUMP"), occurs: "minOccurs=\"0\" maxOccurs=\"0\"") +
            Spez("Kanal optional", Ent("IFCDUCTSEGMENT")) +
            Spez("nur IFC2X3", Ent("IFCPUMP"), fassung: "IFC2X3"));
        Assert.Equal("nicht bestanden", Erg(e, "Kanal verlangt"));
        Assert.Equal("bestanden", Erg(e, "Kanal verboten"));
        Assert.Equal("nicht bestanden", Erg(e, "Pumpe verboten"));
        Assert.Equal(["P-KAE-1"], Befunde(R(e, "Pumpe verboten")));
        Assert.Equal("bestanden", Erg(e, "Kanal optional"));
        Assert.Equal("nicht anwendbar (Fassung)", Erg(e, "nur IFC2X3"));
        Assert.Equal("nicht bestanden", e["ergebnis"]!.GetValue<string>());
    }

    [Fact]
    public void Wert_EinschraenkungenUndGenauigkeit()
    {
        Assert.True(new IdsWert { Einfach = "0.1" }.Passt(0.1000000001));
        Assert.True(new IdsWert { Einfach = "0.1" }.Passt(0.1 * (1 + 0.5e-6)));
        Assert.False(new IdsWert { Einfach = "0.1" }.Passt(0.1001));
        Assert.True(new IdsWert { Einfach = "-2" }.Passt(-2.000001));
        Assert.True(new IdsWert { Einfach = "true" }.Passt(true));
        Assert.False(new IdsWert { Einfach = "TRUE" }.Passt(false));
        Assert.False(new IdsWert { Einfach = "Wand" }.Passt("wand"));
        Assert.True(new IdsWert { Einfach = "IFCWALL" }.Passt("IfcWall", grossKlein: false));
        var muster = new IdsWert(); muster.Muster.Add("DT[0-9]{2}");
        Assert.True(muster.Passt("DT01")); Assert.False(muster.Passt("DT1")); Assert.False(muster.Passt("ADT01"));
        var grenzen = new IdsWert { MinExkl = 1, MaxInkl = 5 };
        Assert.False(grenzen.Passt(1.0)); Assert.True(grenzen.Passt(5.0)); Assert.False(grenzen.Passt("abc"));
        var laenge = new IdsWert { MinLaenge = 2, MaxLaenge = 3 };
        Assert.True(laenge.Passt("ab")); Assert.False(laenge.Passt("abcd"));
        var aufz = new IdsWert(); aufz.Aufzaehlung.AddRange(["25", "30"]);
        Assert.True(aufz.Passt(30.0)); Assert.False(aufz.Passt(31.0));
        Assert.False(new IdsWert { Einfach = "x" }.Passt(null));
    }
}
