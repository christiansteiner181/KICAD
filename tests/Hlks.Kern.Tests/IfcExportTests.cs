using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.IfcExportHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B2: IFC-Export (Abfrage ifc_export) – Inhalt der Datei je Fassung (Gliederung, Entitäten, Typobjekte, Psets, Qto,
/// HK_KICAD, Ports, Systeme, Stränge, Anlagen, Dämmung als IfcCovering, Klassifikation BKP, Kartenbezug), Schemaprüfung, Auswahl (nur was
/// KICAD führt, Umfang, Räume nur auf Wunsch), Ablage und Bericht, IDS-Befund eines absichtlich unvollständigen Elements.
/// Werte FIKTIV (Beispielprojekt «MFH Musterweg», Testfamilie, Testrohrserie); Handrechnungen in mm.</summary>
public sealed class IfcExportTests(Xunit.Abstractions.ITestOutputHelper aus) : IDisposable
{
    readonly IfcExportOrdner ordner = new();
    public void Dispose() => ordner.Dispose();

    static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>Vielfalt (FIKTIV): Rechteck-Wände 6000 × 4000 (Dicke 200, EG), Tür in W-1, Fenster in W-2, Decke, Heizung (Anlage A-HZG, Systeme
    /// HZ-VL/HZ-RL, Strang HZ-S1) mit Rohr RO-D (DN 25, Dämmung 30 mm, Polylinie), Testheizkörper HK-1 (Familie), Wärmepumpe WP-1, Fussbodenheizkreis
    /// FBH-1, Lüftung (System ZUL) mit Kanal KA-R 400 × 200 (Dämmung 50 mm) und Rundkanal KA-D Ø 250, Lüftungsgerät LG-1, Zuluftdurchlass ZD-1,
    /// Raum R-1 in Zone Z-1, Achse A-1, Datenpunkte DP-V (virtuell) und DP-P (physikalisch, an LG-1).</summary>
    static Projekt Vielfalt()
    {
        var p = Rechteck();
        Ok(Tuer(p, "W-1", 3000));
        Ok(Fenster(p, "W-2", 2000));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Decke","kennzeichen":"D-1","geschoss":"EG","geometrie":{"art":"polygon","punkte":[[0,0,3000],[6000,0,3000],[6000,4000,3000],[0,4000,3000]],"dicke":250}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Anlage","kennzeichen":"A-HZG","name":"Heizungsanlage"}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"System","kennzeichen":"HZ-VL","name":"Heizung Vorlauf","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Vorlauf"}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"System","kennzeichen":"HZ-RL","name":"Heizung Rücklauf","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Rücklauf"}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Strang","kennzeichen":"HZ-S1","name":"Verteilung EG","teilVon":"HZ-VL","eigenschaften":{"art":"verteilleitung"}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Rohr","kennzeichen":"RO-D","teilVon":"HZ-S1","geschoss":"EG","eigenschaften":{"dn":25,"material":"Stahl","daemmdicke":30,"daemmung":"Mineralwolle"},"geometrie":{"art":"polylinie","punkte":[[500,500,2500],[3000,500,2500],[3000,3000,2500]]}}""")));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        Ok(p.Ausfuehren("familie_platzieren", O("""{"familie":"FAM-1","typ":"22-600-1000","punkt":[5000,3000,0],"drehung":90,"geschoss":"EG","kennzeichen":"HK-1"}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Waermeerzeuger","kennzeichen":"WP-1","geschoss":"EG","eigenschaften":{"art":"waermepumpe"},"geometrie":{"art":"punkt","punkte":[[1000,3000,0]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Fussbodenheizkreis","kennzeichen":"FBH-1","geschoss":"EG","geometrie":{"art":"polygon","punkte":[[200,200,0],[2000,200,0],[2000,1500,0],[200,1500,0]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"System","kennzeichen":"ZUL","name":"Zuluft","eigenschaften":{"systemtyp":"Zuluft"}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Kanal","kennzeichen":"KA-R","system":"ZUL","geschoss":"EG","eigenschaften":{"breite":400,"hoehe":200,"daemmdicke":50},"geometrie":{"art":"polylinie","punkte":[[500,2000,2600],[4000,2000,2600]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Kanal","kennzeichen":"KA-D","system":"ZUL","geschoss":"EG","eigenschaften":{"durchmesser":250},"geometrie":{"art":"polylinie","punkte":[[4000,2000,2600],[4000,3500,2600]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Lueftungsgeraet","kennzeichen":"LG-1","geschoss":"EG","geometrie":{"art":"punkt","punkte":[[500,3500,0]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Zuluftdurchlass","kennzeichen":"ZD-1","geschoss":"EG","geometrie":{"art":"punkt","punkte":[[4000,3500,2600]]}}""")));
        Ok(p.Ausfuehren("verbinden", O("""{"von":"KA-D.ende","nach":"ZD-1.zul"}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Zone","kennzeichen":"Z-1","name":"Wohnung"}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Raum","kennzeichen":"R-1","name":"Wohnen","geschoss":"EG","teilVon":"Z-1","eigenschaften":{"nummer":"0.01"},"geometrie":{"art":"polygon","punkte":[[100,100],[5900,100],[5900,3900],[100,3900]],"hoehe":2600}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Achse","kennzeichen":"A-1","eigenschaften":{"bezeichnung":"A"},"geometrie":{"art":"linie","punkte":[[0,-1000,0],[0,5000,0]]}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Datenpunkt","kennzeichen":"DP-V","eigenschaften":{"signal":"Wert","art":"sollwert","physikalisch":false}}""")));
        Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Datenpunkt","kennzeichen":"DP-P","eigenschaften":{"signal":"AI","art":"messung","messgroesse":"Temperatur","physikalisch":true},"bezuege":{"an":"LG-1"}}""")));
        return p;
    }

    static IfcDatei Lesen(string datei) => IfcDatei.Oeffnen(datei);
    static IIfcProduct Produkt(IfcDatei d, string gid) => d.Modell.Instances.OfType<IIfcProduct>().Single(x => x.GlobalId.ToString() == gid);

    // ------------------------------------------------------------------ Schemaprüfung und Inhalt

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Vielfalt_SchemapruefungFehlerfrei_IdsBasisBesteht_AlleKlassenAusgegeben(string fassung)
    {
        var p = Vielfalt();
        var datei = ordner.Datei($"vielfalt_{fassung}.ifc");
        var b = Export(p, datei, fassung, new JsonObject { ["raeume"] = true });
        aus.WriteLine(b["schemapruefung"]!.ToJsonString());
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal(0, b["schemapruefung"]!["meldungen"]!.GetValue<int>());
        Assert.Equal("bestanden", b["kontrolle"]!["ergebnis"]!.GetValue<string>());
        // IDS-Basis: einziger Befund sind die nicht angeschlossenen Geräte ohne System (richtig gemeldet), alles andere besteht
        var nicht = b["ids"]![0]!["regeln"]!.AsArray().OfType<JsonObject>().Where(r => r["ergebnis"]!.GetValue<string>() == "nicht bestanden").ToList();
        aus.WriteLine(string.Join(" | ", nicht.Select(r => $"{r["kennung"]}: {string.Join(", ", Befunde(r))}")));
        var systemregel = Assert.Single(nicht);
        Assert.Equal(fassung == "IFC4" ? "HK-HLKS-03" : "HK-HLKS-04", systemregel["kennung"]!.GetValue<string>());
        Assert.Equal(["FBH-1", "HK-1", "LG-1", "WP-1"], Befunde(systemregel).Order(StringComparer.Ordinal));
        using var d = Lesen(datei);
        Assert.Equal(fassung, d.Schema);
        Assert.Equal(1, d.MmJeEinheit);
        foreach (var kz in new[] { "W-1", "D-1", "RO-D", "HK-1", "WP-1", "FBH-1", "KA-R", "KA-D", "LG-1", "ZD-1", "R-1", "DP-P" })
            Assert.NotNull(Produkt(d, Gid(p, kz)));
        Assert.Equal(2, d.Modell.Instances.OfType<IIfcCovering>().Count());
        Assert.Single(d.Modell.Instances.OfType<IIfcSpace>());
        Assert.Single(d.Modell.Instances.OfType<IIfcZone>());
        Assert.Single(d.Modell.Instances.OfType<IIfcDoor>());
        Assert.Single(d.Modell.Instances.OfType<IIfcWindow>());
        // übersprungen mit Grund: Achse und virtueller Datenpunkt
        var ueb = b["uebersprungen"]!.AsArray().OfType<JsonObject>().ToDictionary(x => x["element"]!.GetValue<string>(), x => x["grund"]!.GetValue<string>());
        Assert.Contains("IfcGrid", ueb["A-1"]);
        Assert.Contains("virtuell", ueb["DP-V"], StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => Produkt(d, Gid(p, "A-1")));
        // Wärmepumpe: USERDEFINED mit ObjectType «Waermepumpe» (ifc.json varianteJe)
        var wp = Produkt(d, Gid(p, "WP-1"));
        Assert.Equal(fassung == "IFC4" ? "IfcUnitaryEquipment" : "IfcEnergyConversionDevice", wp.ExpressType.Name);
        Assert.Equal("Waermepumpe", d.ObjectType(wp));
        Assert.Equal("USERDEFINED", d.PredefinedType(wp));
        // Familienelement mit Familienkörpern (kein Hüllquader-Hinweis), Typobjekt «<Familie> <Typ>»
        Assert.DoesNotContain(b["hinweise"]!.AsArray().Select(h => h!.GetValue<string>()), h => h.Contains("Hüllquader") && h.Contains("HK-1"));
        var hk = Produkt(d, Gid(p, "HK-1"));
        Assert.NotNull(hk.Representation);
        Assert.Equal(FamilieTestHilfe.HkName + " 22-600-1000", d.Typ(hk)!.Name?.ToString());
        Assert.Equal("IfcSpaceHeaterType", d.Typ(hk)!.ExpressType.Name);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Gliederung_ProjektGelaendeGebaeudeGeschosseMitKote_ElementeImGeschoss(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var datei = ordner.Datei($"gl_{fassung}.ifc");
        Export(p, datei, fassung);
        using var d = Lesen(datei);
        Assert.Single(d.Modell.Instances.OfType<IIfcProject>());
        Assert.Single(d.Modell.Instances.OfType<IIfcSite>());
        var geb = Assert.Single(d.Modell.Instances.OfType<IIfcBuilding>());
        Assert.Equal("MFH Musterweg", geb.Name?.ToString());
        var g = d.Geschosse();
        Assert.Equal([-2700.0, 0, 2900], g.Select(x => x.KoteMm));
        Assert.Equal(["UG", "EG", "1.OG"], g.Select(x => x.Quellen[0].Name?.ToString()));
        Assert.Equal("Erdgeschoss", g[1].Quellen[0].LongName?.ToString());
        // Elemente im Geschoss: WC-E.02 im EG; Kontrollschacht KS-1 (ohne Geschoss, unter dem UG) im untersten Geschoss
        Assert.Equal("EG", d.GeschossVon(Produkt(d, Gid(p, "WC-E.02")))!.Quellen[0].Name?.ToString());
        Assert.Equal("UG", d.GeschossVon(Produkt(d, Gid(p, "KS-1")))!.Quellen[0].Name?.ToString());
        // Lage: Einfügepunkt in Projektkoordinaten (Geschoss bei der Kote, Element relativ dazu)
        var lage = d.Lage(Produkt(d, Gid(p, "WC-E.02"))).Ursprung;
        Assert.Equal([7500.0, 9500, 0], lage.Select(x => Math.Round(x, 3)));
    }

    [Theory]
    [InlineData("IFC4", "IfcPipeSegment", "IfcPipeSegmentType")]
    [InlineData("IFC2X3", "IfcFlowSegment", "IfcPipeSegmentType")]
    public void Rohr_EntitaetTypobjektPsetsQtoHkKicadMaterialBkp(string fassung, string entitaet, string typ)
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var datei = ordner.Datei($"rohr_{fassung}.ifc");
        Export(p, datei, fassung);
        using var d = Lesen(datei);
        var ro = Produkt(d, Gid(p, "KW-V1-2"));   // Polylinie (1500,2500,-2400) → (1500,2500,-500) → (8000,2500,-500), DN 32, Mehrschichtverbund
        Assert.Equal(entitaet, ro.ExpressType.Name);
        Assert.Equal(typ, d.Typ(ro)!.ExpressType.Name);
        Assert.Equal("RIGIDSEGMENT", d.PredefinedType(ro));
        Assert.Equal("KW-V1-2", ro.Name?.ToString());
        Assert.Equal("KW-V1-2", ((IIfcElement)ro).Tag?.ToString());
        var eig = d.Eigenschaften(ro);
        Assert.Equal(32, d.LaengeMm(eig["Pset_PipeSegmentTypeCommon.NominalDiameter"]));
        Assert.Equal("IfcPositiveLengthMeasure", eig["Pset_PipeSegmentTypeCommon.NominalDiameter"].Art);
        Assert.Equal(1900 + 6500, d.LaengeMm(eig["Qto_PipeSegmentBaseQuantities.Length"])!.Value, 3);
        Assert.Equal("KW-V1-2", eig["HK_KICAD.Kennzeichen"].Inhalt);
        Assert.Equal("Rohr", eig["HK_KICAD.Klasse"].Inhalt);
        Assert.Equal("KW", eig["HK_KICAD.Medium"].Inhalt);
        Assert.Equal("KW", eig["HK_KICAD.System"].Inhalt);
        Assert.Equal("KW-V1", eig["HK_KICAD.Strang"].Inhalt);
        Assert.Equal(p.Modell.Hole("KW-V1-2").Id, eig["HK_KICAD.ElementId"].Inhalt);
        Assert.Equal("Rohr", eig["HK_KICAD_Typ.Typname"].Inhalt);   // vom Typobjekt
        // Körper: je Teilstück eine Extrusion mit Kreisprofil (DN-gerecht wie das 3D: Ø 32)
        var ex = ro.Representation!.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>().ToList();
        Assert.Equal(2, ex.Count);
        Assert.All(ex, x => Assert.Equal(16, (double)((IIfcCircleProfileDef)x.SweptArea).Radius, 6));
        Assert.Equal([1900.0, 6500], ex.Select(x => Math.Round((double)x.Depth, 3)));
        // Material und BKP (Sanitär 254, klassen.json bkpJeGewerk)
        var mat = d.Modell.Instances.OfType<IIfcRelAssociatesMaterial>().Single(r => r.RelatedObjects.Contains(ro));
        Assert.Equal("Mehrschichtverbund", ((IIfcMaterial)mat.RelatingMaterial).Name.ToString());
        var kl = d.Modell.Instances.OfType<IIfcRelAssociatesClassification>().Single(r => r.RelatedObjects.Contains(ro));
        var refz = (IIfcClassificationReference)kl.RelatingClassification;
        Assert.Equal("254", refz.Identification?.ToString());
        Assert.Equal("BKP", ((IIfcClassification)refz.ReferencedSource!).Name.ToString());
        if (fassung == "IFC2X3") Assert.Equal(IfcExport.KlassifikationOhneAusgabe, ((IIfcClassification)refz.ReferencedSource!).Edition?.ToString());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Ports_FlussNennweiteSystemartVerbindungen(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var datei = ordner.Datei($"ports_{fassung}.ifc");
        var b = Export(p, datei, fassung);
        using var d = Lesen(datei);
        var ports = d.Modell.Instances.OfType<IIfcDistributionPort>().ToList();
        Assert.Equal(p.Modell.Elemente.Where(e => IfcHlksKlasse.IstHlks(p.Katalog, e.Klasse)).Sum(e => e.Anschluesse.Count), ports.Count);
        Assert.Equal(b["anzahl"]!["ports"]!.GetValue<int>(), ports.Count);
        Assert.Equal(p.Modell.Verbindungen.Count(), d.Modell.Instances.OfType<IIfcRelConnectsPorts>().Count());
        var hp = IfcHlksPorts.Lesen(d, IfcHilfe.Kat.Ifc!);
        // Pumpe P-KAE-1: saug rein (SINK), druck raus (SOURCE); WC: sw raus, kw rein
        var pumpe = hp.Von(Produkt(d, Gid(p, "P-KAE-1")));
        Assert.Equal(["SINK", "SOURCE"], pumpe.Select(x => x.Port.FlowDirection.ToString()));
        Assert.Equal(["saug", "druck"], pumpe.Select(x => x.Name));
        // Leitung: Ports an den Enden, Achse nach aussen
        var ro = hp.Von(Produkt(d, Gid(p, "KAE-V1-1")));   // (1500,1000,-2400) → (3500,1000,-2400)
        Assert.Equal([1500.0, 1000, -2400], ro[0].Lage);
        Assert.Equal([3500.0, 1000, -2400], ro[1].Lage);
        Assert.Equal(-1, ro[0].Achse![0], 6);
        Assert.Equal(1, ro[1].Achse![0], 6);
        if (fassung == "IFC4")
        {
            Assert.All(pumpe, x => Assert.Equal("PIPE", x.Port.PredefinedType.ToString()));
            Assert.All(pumpe, x => Assert.Equal("CHILLEDWATER", x.Port.SystemType.ToString()));
            Assert.NotEmpty(d.Modell.Instances.OfType<IIfcRelNests>());
            Assert.Empty(d.Modell.Instances.OfType<IIfcRelConnectsPortToElement>());
            var e = d.Eigenschaften(pumpe[0].Port);
            Assert.Equal(40, d.LaengeMm(e["Pset_DistributionPortTypePipe.NominalDiameter"]));
        }
        else
        {
            Assert.Equal(ports.Count, d.Modell.Instances.OfType<IIfcRelConnectsPortToElement>().Count());
            Assert.Equal(40, d.LaengeMm(d.Eigenschaften(pumpe[0].Port)["HK_KICAD.Nennweite"]));
        }
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Systeme_StraengeNurIfc4_AnlagenAlsGruppeDerSysteme(string fassung)
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var datei = ordner.Datei($"sys_{fassung}.ifc");
        Export(p, datei, fassung);
        using var d = Lesen(datei);
        var gruppen = d.Modell.Instances.OfType<IIfcGroup>().ToDictionary(g => g.GlobalId.ToString());
        var sw = gruppen[Gid(p, "SW")];
        var kaeVl = gruppen[Gid(p, "KAE-VL")];
        var anlage = gruppen[Gid(p, "A-KAE")];
        var mitglieder = (IIfcGroup g) => d.Modell.Instances.OfType<IIfcRelAssignsToGroup>().Where(r => r.RelatingGroup == g).SelectMany(r => r.RelatedObjects).ToList();
        Assert.Equal("IfcSystem", anlage.ExpressType.Name);
        Assert.Equal("Anlage", anlage.ObjectType?.ToString());
        Assert.Equal(new[] { Gid(p, "KAE-VL"), Gid(p, "KAE-RL") }.Order(StringComparer.Ordinal), mitglieder(anlage).Select(x => x.GlobalId.ToString()).Order(StringComparer.Ordinal));
        // Leitungsbauteile und angeschlossene Apparate im System SW (WC-E.02 hängt an SW und KW)
        Assert.Contains(mitglieder(sw), x => x.GlobalId.ToString() == Gid(p, "SW-F1-1"));
        Assert.Contains(mitglieder(sw), x => x.GlobalId.ToString() == Gid(p, "WC-E.02"));
        Assert.Contains(mitglieder(gruppen[Gid(p, "KW")]), x => x.GlobalId.ToString() == Gid(p, "WC-E.02"));
        if (fassung == "IFC4")
        {
            Assert.Equal("IfcDistributionSystem", sw.ExpressType.Name);
            Assert.Equal("WASTEWATER", ((IIfcDistributionSystem)sw).PredefinedType.ToString());
            Assert.Equal("CHILLEDWATER", ((IIfcDistributionSystem)kaeVl).PredefinedType.ToString());
            Assert.Equal("Vorlauf", kaeVl.ObjectType?.ToString());   // ifc.json systemtypen objectType
            var strang = gruppen[Gid(p, "SW-F1")];
            Assert.Equal("IfcDistributionCircuit", strang.ExpressType.Name);
            Assert.Contains(mitglieder(strang), x => x.GlobalId.ToString() == Gid(p, "SW-F1-1"));
            var agg = d.Modell.Instances.OfType<IIfcRelAggregates>().Single(r => r.RelatedObjects.Contains(strang));
            Assert.Equal(sw.GlobalId, agg.RelatingObject.GlobalId);
            var rk = gruppen[Gid(p, "RK-1")];
            Assert.Equal("CONTROL", ((IIfcDistributionSystem)rk).PredefinedType.ToString());
        }
        else
        {
            Assert.Equal("IfcSystem", sw.ExpressType.Name);
            Assert.Equal("Schmutzwasser", sw.ObjectType?.ToString());   // Systemtyp
            Assert.DoesNotContain(gruppen.Values, g => g.GlobalId.ToString() == Gid(p, "SW-F1"));
            Assert.Equal("SW-F1", d.Eigenschaften(Produkt(d, Gid(p, "SW-F1-1")))["HK_KICAD.Strang"].Inhalt);
        }
    }

    // ------------------------------------------------------------------ Dämmung als eigene Geometrie (Entscheid A2)

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Daemmung_AlsIfcCoveringInsulation_ZugeordnetUeberRelCoversBldgElements_Hohlprofil(string fassung)
    {
        var p = Vielfalt();
        var datei = ordner.Datei($"daemm_{fassung}.ifc");
        var b = Export(p, datei, fassung);
        Assert.Equal(2, b["anzahl"]!["daemmungen"]!.GetValue<int>());
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        using var d = Lesen(datei);
        var ro = Produkt(d, Gid(p, "RO-D"));
        var rel = d.Modell.Instances.OfType<IIfcRelCoversBldgElements>().Single(r => r.RelatingBuildingElement.GlobalId == ro.GlobalId);
        var cov = Assert.Single(rel.RelatedCoverings);
        Assert.Equal(IfcCoveringTypeEnum.INSULATION, cov.PredefinedType);
        Assert.Equal("EG", d.GeschossVon(cov)!.Quellen[0].Name?.ToString());
        // Rohr DN 25 ohne Serie: wie das 3D Ø max(DN, 30) = 30, Dämmung 30 mm → Hohlprofil Radius 15 + 30, Wand 30; zwei Teilstücke
        var hohl = cov.Representation!.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>().Select(x => (IIfcCircleHollowProfileDef)x.SweptArea).ToList();
        Assert.Equal(2, hohl.Count);
        Assert.All(hohl, h => { Assert.Equal(45, (double)h.Radius, 6); Assert.Equal(30, (double)h.WallThickness, 6); });
        var rohrkoerper = ro.Representation!.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>().Select(x => (IIfcCircleProfileDef)x.SweptArea).ToList();
        Assert.All(rohrkoerper, k => Assert.Equal(15, (double)k.Radius, 6));
        Assert.Equal(30, d.LaengeMm(d.Eigenschaften(cov)["HK_KICAD.Daemmdicke"]));
        // Kanal 400 × 200 mit 50 mm: Rechteck-Hohlprofil 500 × 300, Wand 50; Kanal selbst 400 × 200, Breite waagrecht
        var ka = Produkt(d, Gid(p, "KA-R"));
        var covK = d.Modell.Instances.OfType<IIfcRelCoversBldgElements>().Single(r => r.RelatingBuildingElement.GlobalId == ka.GlobalId).RelatedCoverings.Single();
        var rh = (IIfcRectangleHollowProfileDef)covK.Representation!.Representations.SelectMany(r => r.Items).OfType<IIfcExtrudedAreaSolid>().Single().SweptArea;
        Assert.Equal([500.0, 300, 50], new[] { (double)rh.XDim, (double)rh.YDim, (double)rh.WallThickness });
        var kq = IfcHlksKoerper.Lesen(d, ka)!.QuerschnittAusProfil()!;
        Assert.Equal((400.0, 200.0), (kq.Breite!.Value, kq.Hoehe!.Value));
        var eig = d.Eigenschaften(ka);
        Assert.Equal(400, d.LaengeMm(eig["Pset_DuctSegmentTypeCommon.NominalDiameterOrWidth"]));
        Assert.Equal(200, d.LaengeMm(eig["Pset_DuctSegmentTypeCommon.NominalHeight"]));
        Assert.Equal(250, d.LaengeMm(d.Eigenschaften(Produkt(d, Gid(p, "KA-D")))["Pset_DuctSegmentTypeCommon.NominalDiameterOrWidth"]));
        // Lüftungsports DUCT (IFC4)
        if (fassung == "IFC4")
            Assert.All(IfcHlksPorts.Lesen(d, IfcHilfe.Kat.Ifc!).Von(ka), x => Assert.Equal("DUCT", x.Port.PredefinedType.ToString()));
    }

    // ------------------------------------------------------------------ Auswahl: nur was KICAD führt

    [Fact]
    public void Grundlagen_UndUnveraendertUebernommeneIfcElemente_WerdenNichtExportiert()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        IfcHilfe.Ok(IfcHilfe.Uebernehmen(p, IfcHilfe.Testdaten("minimal_ifc4.ifc")));   // Wand und Raum aus fremdem IFC, Unterlage
        // Bauprodukte aus dem fremden IFC (Geschosse bleiben Gliederung: ein Geschoss braucht die Ausgabe immer)
        var fremd = p.Modell.Elemente.Where(e => IfcUebernehmen.UnveraendertUebernommen(e) && e.Klasse is not ("Geschoss" or "Gebaeude")).ToList();
        Assert.Contains(fremd, e => e.Klasse == "Wand");
        var datei = ordner.Datei("grundlagen.ifc");
        var b = Export(p, datei, "IFC4", new JsonObject { ["raeume"] = true });
        var ueb = b["uebersprungen"]!.AsArray().OfType<JsonObject>().ToList();
        foreach (var e in fremd)
            Assert.Contains(ueb, u => u["element"]!.GetValue<string>() == e.Kennzeichen && u["grund"]!.GetValue<string>().Contains("fremdem IFC"));
        Assert.Contains(b["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Unterlagen (Grundlagen, Hintergründe) nie exportiert"));
        using var d = Lesen(datei);
        var gids = d.Modell.Instances.OfType<IIfcRoot>().Select(r => r.GlobalId.ToString()).ToHashSet();
        foreach (var e in fremd) Assert.DoesNotContain(e.Kennungen!["ifc"], gids);
        foreach (var e in fremd) Assert.DoesNotContain(Gid(p, e), gids);
        Assert.DoesNotContain(gids, g => p.Modell.DerKlasse("Unterlage").Any(u => Gid(p, u) == g));
        // in KICAD geändert: wird exportiert
        var wand = fremd.First(e => e.Klasse == "Wand");
        IfcHilfe.Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = wand.Kennzeichen, ["eigenschaften"] = new JsonObject { ["tragend"] = true } }));
        var b2 = Export(p, ordner.Datei("grundlagen2.ifc"), "IFC4");
        Assert.DoesNotContain(b2["uebersprungen"]!.AsArray().OfType<JsonObject>(), u => u["element"]!.GetValue<string>() == wand.Kennzeichen);
    }

    [Fact]
    public void Raeume_NurAufWunsch()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var ohne = Export(p, ordner.Datei("ohne.ifc"));
        Assert.Equal(0, ohne["anzahl"]!["raeume"]!.GetValue<int>());
        using (var d = Lesen(ordner.Datei("ohne.ifc"))) Assert.Empty(d.Modell.Instances.OfType<IIfcSpace>());
        var mit = Export(p, ordner.Datei("mit.ifc"), "IFC2X3", new JsonObject { ["raeume"] = true });
        Assert.Equal(13, mit["anzahl"]!["raeume"]!.GetValue<int>());
        Assert.Equal("fehlerfrei", mit["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        using var d2 = Lesen(ordner.Datei("mit.ifc"));
        var raum = d2.Modell.Instances.OfType<IIfcSpace>().Single(s => s.Name?.ToString() == "E.02");
        Assert.Equal("Bad", raum.LongName?.ToString());
        Assert.Equal("EG", d2.GeschossVon(raum)!.Quellen[0].Name?.ToString());
        // ausdrücklich verlangt: auch mit Gewerke- und Klassenfilter; der Geschossfilter gilt
        var gefiltert = Export(p, ordner.Datei("gefiltert.ifc"), "IFC4", O("""{"raeume":true,"umfang":{"gewerke":["sanitaer"],"klassen":["Rohr"],"geschosse":["EG"]}}"""));
        Assert.Equal(5, gefiltert["anzahl"]!["raeume"]!.GetValue<int>());
    }

    [Fact]
    public void Umfang_GewerkeGeschosseKlassen_UndFehler()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        // Kälte gehört zur Heizung (Entscheid Christian 20.10.2026); «kaelte» ist der alte Wert
        var hz = Export(p, ordner.Datei("hz.ifc"), "IFC4", O("""{"umfang":{"gewerke":["kaelte"]}}"""));
        Assert.Equal(["Pumpe", "Rohr", "Ventil"], hz["jeKlasse"]!.AsArray().Select(x => x!["klasse"]!.GetValue<string>()).Distinct().Order());
        Assert.Equal(2, hz["anzahl"]!["systeme"]!.GetValue<int>());
        var eg = Export(p, ordner.Datei("eg.ifc"), "IFC4", O("""{"umfang":{"geschosse":["EG"],"klassen":["Apparat"]}}"""));
        Assert.Equal(7, eg["anzahl"]!["elemente"]!.GetValue<int>());   // 7 Apparate im EG
        Assert.Contains(eg["uebersprungenJeGrund"]!.AsObject(), x => x.Key == "Klasse nicht im Umfang");
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("x.ifc"), "IFC4", O("""{"umfang":{"gewerke":["gibtsnicht"]}}""")));
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("x.ifc"), "IFC4", O("""{"umfang":{"geschosse":["9.OG"]}}""")));
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("x.ifc"), "IFC4", O("""{"umfang":{"klassen":["Rakete"]}}""")));
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("x.ifc"), "IFC4", O("""{"umfang":{"farbe":["rot"]}}""")));
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("x.ifc"), "IFC4", O("""{"umfang":{"klassen":["Achse"]}}""")));   // nichts übrig
    }

    [Fact]
    public void Fehler_FassungEndungIds()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var f = Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("a.ifc"), "IFC4X3"));
        Assert.Contains("IFC2X3", f.Vorschlag);
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("a.txt"), "IFC4"));
        Assert.Throws<HlksFehler>(() => Export(p, ordner.Datei("a.ifc"), "IFC4", new JsonObject { ["ids"] = new JsonArray(ordner.Datei("fehlt.ids")) }));
        Assert.False(File.Exists(ordner.Datei("a.ifc")));
    }

    [Fact]
    public void Export_AendertDasModellNicht_KeineTempReste_BerichtDaneben()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var vorher = p.Modell.Pruefsumme();
        var datei = ordner.Datei("modell.ifc");
        var b = Export(p, datei);
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Assert.Equal(["modell.bericht.json", "modell.bericht.md", "modell.ifc"], Directory.GetFiles(ordner.Pfad).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var json = JsonNode.Parse(File.ReadAllText(ordner.Datei("modell.bericht.json")))!.AsObject();
        Assert.Equal(datei, json["datei"]!.GetValue<string>());
        Assert.Equal(b["bericht"]!["md"]!.GetValue<string>(), ordner.Datei("modell.bericht.md"));
        var md = File.ReadAllText(ordner.Datei("modell.bericht.md"));
        Assert.Contains("# IFC-Exportbericht", md);
        Assert.Contains("| Rohr | IfcPipeSegment | IfcPipeSegmentType | RIGIDSEGMENT | 71 |", md);
        Assert.Contains("H+K HLKS-Basis", md);
        // Zähler je Klasse und Entität, Dauer
        Assert.Equal(71, b["jeKlasse"]!.AsArray().OfType<JsonObject>().Single(x => x["klasse"]!.GetValue<string>() == "Rohr")["anzahl"]!.GetValue<int>());
        Assert.Equal(71, b["jeEntitaet"]!["IfcPipeSegment"]!.GetValue<int>());
        Assert.True(b["dauerSekunden"]!.GetValue<double>() > 0);
        // zweiter Export ersetzt die Datei
        Export(p, datei);
        Assert.Equal(3, Directory.GetFiles(ordner.Pfad).Length);
    }

    [Fact]
    public void Ablage_OhneDatei_ProjektordnerAusgabeIfc_NameProjektFassungZeit()
    {
        var wurzel = ordner.Datei("projekt");
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, new OrdnerSpeicher(wurzel));
        try
        {
            var b = (JsonObject)Tuer.Abfrage(p, "ifc_export", new JsonObject { ["fassung"] = "IFC2X3" });
            var datei = b["datei"]!.GetValue<string>();
            Assert.Equal(Path.Combine(wurzel, "ausgabe", "ifc"), Path.GetDirectoryName(datei));
            Assert.Equal(IfcExport.Ausgabeordner(p), Path.GetDirectoryName(datei));
            Assert.Matches(@"^MFH Musterweg \(Beispiel, fiktiv\)_IFC2X3_\d{8}-\d{4}\.ifc$", Path.GetFileName(datei));
            Assert.True(File.Exists(datei));
            Assert.True(File.Exists(Path.ChangeExtension(datei, null) + ".bericht.json"));
            Assert.True(File.Exists(Path.ChangeExtension(datei, null) + ".bericht.md"));
        }
        finally { p.Freigeben(); }
    }

    // ------------------------------------------------------------------ Koordinaten

    [Fact]
    public void Koordinaten_ProjektbezugMitLandeskoordinaten_Ifc4MitMapConversion_RundreiseDerLandeskoordinaten()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        // FIKTIVER Bezug: Weltpunkt (2'000, 1'000, 500) mm, Drehung 30°, Landeskoordinaten des Projektursprungs 2'600'000 / 1'200'000 / 550 m, Nordwinkel 40°
        var bezug = O("""{"x":2000,"y":1000,"z":500,"drehung":30,"landes":{"ost":2600000,"nord":1200000,"hoehe":550,"nordwinkel":40,"bezugssystem":"EPSG:2056"}}""");
        IfcHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI", ["eigenschaften"] = new JsonObject { ["ifcBezug"] = bezug } }));
        var datei = ordner.Datei("karte.ifc");
        var b = Export(p, datei, "IFC4");
        Assert.Equal("gemeinsam", b["koordinaten"]!["art"]!.GetValue<string>());
        Assert.Equal("EPSG:2056", b["koordinaten"]!["kartenbezug"]!["bezugssystem"]!.GetValue<string>());
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        using var d = Lesen(datei);
        var k = d.Karte()!;
        Assert.Equal("EPSG:2056", k.Bezugssystem);
        Assert.Equal(10, k.Drehung, 6);   // Welt-x gegen Ost: Nordwinkel 40° − Drehung des Bezugs 30°
        var l = k.Landes([2000, 1000, 500]);   // Projektursprung in der Welt der Datei
        Assert.Equal(2600000, l[0], 4); Assert.Equal(1200000, l[1], 4); Assert.Equal(550, l[2], 6);
        // Gebäude im Bezug, Elemente nach dem Rückweg in Projektkoordinaten
        d.Bezug = new IfcBezug(2000, 1000, 500, 30);
        var lage = d.Lage(Produkt(d, Gid(p, "WC-E.02"))).Ursprung;
        Assert.Equal([7500.0, 9500, 0], lage.Select(x => Math.Round(x, 3)));
        // IFC2X3: kein IfcMapConversion, Hinweis
        var b2 = Export(p, ordner.Datei("karte2x3.ifc"), "IFC2X3");
        Assert.Contains(b2["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("nur in IFC4"));
    }

    // ------------------------------------------------------------------ IDS: absichtlich unvollständiges Element

    [Theory]
    [InlineData("IFC4", "HK-HLKS-03")]
    [InlineData("IFC2X3", "HK-HLKS-04")]
    public void Ids_UnvollstaendigeElemente_ErscheinenAlsBefund_MitKennzeichen(string fassung, string systemregel)
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        // absichtlich unvollständig (FIKTIV): Rohr ohne Nennweite und ohne Aussendurchmesser; Waschtisch, an kein System angeschlossen
        IfcHilfe.Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Rohr","kennzeichen":"RO-OHNE","system":"KW","geschoss":"EG","geometrie":{"art":"polylinie","punkte":[[100,100,500],[900,100,500]]}}""")));
        IfcHilfe.Ok(p.Ausfuehren("element_anlegen", O("""{"klasse":"Waschtisch","kennzeichen":"WT-OHNE","geschoss":"EG","geometrie":{"art":"punkt","punkte":[[2000,2000,0]]}}""")));
        var b = Export(p, ordner.Datei($"ids_{fassung}.ifc"), fassung);
        Assert.Equal("fehlerfrei", b["schemapruefung"]!["ergebnis"]!.GetValue<string>());
        Assert.Equal("nicht bestanden", b["ergebnis"]!["ids"]!.GetValue<string>());
        var r1 = Regel(b, "HK-HLKS-01");
        Assert.Equal("nicht bestanden", r1["ergebnis"]!.GetValue<string>());
        Assert.Equal(["RO-OHNE"], Befunde(r1));
        Assert.Equal(72, r1["anwendbar"]!.GetValue<int>());
        Assert.Equal(1, r1["nichtBestandenElemente"]!.GetValue<int>());
        Assert.Contains("fehlt", r1["befunde"]![0]!["gruende"]![0]!.GetValue<string>());
        Assert.Equal(["WT-OHNE"], Befunde(Regel(b, systemregel)));
        Assert.Equal("bestanden", Regel(b, "HK-HLKS-05")["ergebnis"]!.GetValue<string>());
        // Bericht nennt die Regel und die Kennzeichen
        var md = File.ReadAllText(b["bericht"]!["md"]!.GetValue<string>());
        Assert.Contains("RO-OHNE", md);
        Assert.Contains("WT-OHNE", md);
    }

    [Fact]
    public void Ids_FremdeIdsDatei_WirdZusaetzlichGeprueft_UngueltigeWirdGemeldet()
    {
        var p = Beispielprojekt.Erzeugen(IfcHilfe.Kat, null);
        var fremd = ordner.Datei("bauherr.ids");
        File.WriteAllText(fremd, IdsPrueferTests.Ids("""
            <ids:specification name="Pumpen mit Typ" ifcVersion="IFC4 IFC2X3">
              <ids:applicability minOccurs="1" maxOccurs="unbounded"><ids:entity><ids:name><ids:simpleValue>IFCPUMP</ids:simpleValue></ids:name></ids:entity></ids:applicability>
              <ids:requirements><ids:entity><ids:name><ids:simpleValue>IFCPUMP</ids:simpleValue></ids:name><ids:predefinedType><ids:simpleValue>CIRCULATOR</ids:simpleValue></ids:predefinedType></ids:entity></ids:requirements>
            </ids:specification>
            """));
        var kaputt = ordner.Datei("kaputt.ids");
        File.WriteAllText(kaputt, IdsPrueferTests.Ids("""<ids:specification name="x" ifcVersion="IFC5"><ids:applicability/></ids:specification>"""));
        var b = Export(p, ordner.Datei("fremd.ifc"), "IFC2X3", new JsonObject { ["ids"] = new JsonArray(fremd, kaputt) });
        var ids = b["ids"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(3, ids.Count);
        Assert.True(ids[0]["basis"]!.GetValue<bool>());
        Assert.Equal("bestanden", ids[1]["ergebnis"]!.GetValue<string>());
        Assert.Equal(1, ids[1]["regeln"]![0]!["anwendbar"]!.GetValue<int>());
        Assert.Equal("IDS ungültig", ids[2]["ergebnis"]!.GetValue<string>());
        Assert.NotEmpty(ids[2]["xsdMeldungen"]!.AsArray());
    }

    // ------------------------------------------------------------------ Abfrage

    [Fact]
    public void Abfrage_StehtInDerListeMitParametern()
    {
        var a = Assert.Single(Abfragen.Liste, x => x.Name == "ifc_export");
        Assert.Equal(["datei", "fassung", "umfang", "raeume", "ids"], a.Parameter.Select(x => x.Name));
        Assert.All(a.Parameter, x => Assert.False(x.Pflicht));
        Assert.Equal(["text", "text", "objekt", "bool", "liste"], a.Parameter.Select(x => x.Typ));
    }
}
