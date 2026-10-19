using System.Reflection;
using System.Text.RegularExpressions;
using Xbim.Common;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>openBIM-Abbildung «am Schema geprüft» (kataloge/ifc.json): jede Entität, jedes Typobjekt und jeder Aufzählungswert
/// muss als Klasse bzw. Enum-Wert in den xBIM-Schemaassemblies (IFC4 und IFC2x3) vorkommen. Dazu der Abgleich mit
/// klassen.json und systemtypen.json und der Rückweg KlasseFuer.</summary>
public class IfcAbbildungTests
{
    static readonly Assembly Ifc4 = typeof(Xbim.Ifc4.Kernel.IfcProduct).Assembly;
    static readonly Assembly Ifc2x3 = typeof(Xbim.Ifc2x3.Kernel.IfcProduct).Assembly;
    static readonly IfcAbbildung Abb = Kat.Ifc ?? throw new InvalidOperationException("kataloge/ifc.json fehlt");

    static readonly Dictionary<Assembly, Type[]> Typen = new()
    {
        [Ifc4] = SicherTypen(Ifc4),
        [Ifc2x3] = SicherTypen(Ifc2x3),
    };

    static Type[] SicherTypen(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null).ToArray()!; }
    }

    static Assembly Asm(string schema) => schema == "IFC4" ? Ifc4 : Ifc2x3;

    /// <summary>Entitätsklasse (nicht Interface, nicht Enum) mit genau diesem Namen in irgendeinem Namespace der Assembly.</summary>
    static Type? EntitaetsKlasse(Assembly a, string name) =>
        Typen[a].FirstOrDefault(t => t.IsClass && t.Name == name && typeof(IPersistEntity).IsAssignableFrom(t));

    static bool Erbt(Type t, string basisName)
    {
        for (var x = t; x is not null; x = x.BaseType) if (x.Name == basisName) return true;
        return false;
    }

    /// <summary>Werte der Aufzählung hinter der Eigenschaft PredefinedType der Klasse; null, wenn die Klasse sie nicht hat.</summary>
    static HashSet<string>? PredefinedWerte(Type? klasse)
    {
        var eig = klasse?.GetProperty("PredefinedType", BindingFlags.Public | BindingFlags.Instance);
        if (eig is null) return null;
        var t = Nullable.GetUnderlyingType(eig.PropertyType) ?? eig.PropertyType;
        return t.IsEnum ? Enum.GetNames(t).ToHashSet(StringComparer.Ordinal) : null;
    }

    /// <summary>PredefinedType-Werte, die für ein Ziel in dieser Fassung gelten: die des Typobjekts; fehlt die Eigenschaft
    /// dort, die des Exemplars.</summary>
    static HashSet<string>? WerteFuer(string schema, IfcZiel z)
    {
        var a = Asm(schema);
        var typ = z.Typ is null ? null : EntitaetsKlasse(a, z.Typ);
        return PredefinedWerte(typ) ?? PredefinedWerte(EntitaetsKlasse(a, z.Entitaet));
    }

    static HashSet<string> EnumWerte(Assembly a, string enumName)
    {
        var treffer = Typen[a].Where(t => t.IsEnum && t.Name == enumName).ToList();
        Assert.True(treffer.Count > 0, $"Aufzählung {enumName} fehlt in {a.GetName().Name}");
        return treffer.SelectMany(t => Enum.GetNames(t)).ToHashSet(StringComparer.Ordinal);
    }

    // ------------------------------------------------------------------ Datenquellen

    static string Schluessel(string schema, string klasse, string? gewerk, string? variante) => $"{schema}|{klasse}|{gewerk}|{variante}";

    public static IEnumerable<object[]> Ziele()
    {
        foreach (var schema in new[] { "IFC4", "IFC2X3" })
            foreach (var k in Abb.Klassen)
                foreach (var (_, gewerk, variante) in k.Ziele(schema))
                    yield return [schema, k.Klasse, gewerk ?? "", variante ?? ""];
    }

    public static IEnumerable<object[]> KlassenNamen() => Abb.Klassen.Select(k => new object[] { k.Klasse });

    static (IfcKlasse K, IfcZiel Z) Ziel(string schema, string klasse, string gewerk, string variante)
    {
        var k = Abb.Klasse(klasse)!;
        var z = k.Ziele(schema).First(x => (x.Gewerk ?? "") == gewerk && (x.Variante ?? "") == variante).Ziel;
        return (k, z);
    }

    [Fact]
    public void Datei_IstGeladen_UndEnthaeltAlleKlassenUndBeideFassungen()
    {
        Assert.True(Abb.Klassen.Count >= 40);
        Assert.NotEmpty(Abb.Systemtypen);
        Assert.Equal(3, Abb.Fluss.Count);
        // Jede Klasse hat beide Fassungen (Entscheid 17.10.2026: «IFC 4 und alle zuvor»)
        Assert.All(Abb.Klassen, k => { Assert.NotNull(k.Ifc4); Assert.NotNull(k.Ifc2x3); });
        // Lüftung und Varianten: auch deren Ziele sind in beiden Fassungen da
        foreach (var k in Abb.Klassen)
        {
            if (k.Lueftung is not null) { Assert.NotNull(k.Lueftung.Ifc4); Assert.NotNull(k.Lueftung.Ifc2x3); }
            if (k.VarianteJe is not null) Assert.All(k.VarianteJe.Werte, v => { Assert.NotNull(v.Value.Ifc4); Assert.NotNull(v.Value.Ifc2x3); });
        }
    }

    [Fact]
    public void Ziele_UmfassenLueftungUndVarianten()
    {
        var bogen = Abb.Klasse("Bogen")!;
        Assert.Equal(2, bogen.Ziele("IFC4").Count());
        Assert.Contains(bogen.Ziele("IFC4"), z => z.Gewerk == "lueftung" && z.Ziel.Entitaet == "IfcDuctFitting");
        var wez = Abb.Klasse("Waermeerzeuger")!;
        Assert.Equal(3, wez.Ziele("IFC2X3").Count());
        Assert.Contains(wez.Ziele("IFC4"), z => z.Variante == "waermepumpe" && z.Ziel.Entitaet == "IfcUnitaryEquipment");
        Assert.Contains(wez.Ziele("IFC4"), z => z.Variante == "fernwaerme" && z.Ziel.Entitaet == "IfcHeatExchanger");
    }

    // ------------------------------------------------------------------ am Schema geprüft

    [Theory]
    [MemberData(nameof(Ziele))]
    public void Entitaet_GibtEsAlsKlasseImSchema(string schema, string klasse, string gewerk, string variante)
    {
        var (_, z) = Ziel(schema, klasse, gewerk, variante);
        var t = EntitaetsKlasse(Asm(schema), z.Entitaet);
        Assert.True(t is not null, $"{Schluessel(schema, klasse, gewerk, variante)}: Entität {z.Entitaet} gibt es in {Asm(schema).GetName().Name} nicht");
        Assert.True(Erbt(t!, "IfcObjectDefinition"), $"{z.Entitaet} ist kein IfcObjectDefinition");
    }

    [Theory]
    [MemberData(nameof(Ziele))]
    public void Typobjekt_GibtEsAlsTypklasseImSchema(string schema, string klasse, string gewerk, string variante)
    {
        var (_, z) = Ziel(schema, klasse, gewerk, variante);
        if (z.Typ is null) return;   // Gliederung, Raum, Achse, Systeme: kein Typobjekt
        var t = EntitaetsKlasse(Asm(schema), z.Typ);
        Assert.True(t is not null, $"{Schluessel(schema, klasse, gewerk, variante)}: Typobjekt {z.Typ} gibt es in {Asm(schema).GetName().Name} nicht");
        Assert.True(Erbt(t!, "IfcTypeObject"), $"{z.Typ} ist kein IfcTypeObject");
    }

    [Theory]
    [MemberData(nameof(Ziele))]
    public void TypobjektUndEntitaet_PassenZusammen(string schema, string klasse, string gewerk, string variante)
    {
        var (_, z) = Ziel(schema, klasse, gewerk, variante);
        if (z.Typ is null) return;
        if (schema == "IFC4")
            // IFC4: das Typobjekt heisst wie das Exemplar plus «Type» (IfcPipeFitting ↔ IfcPipeFittingType)
            Assert.Equal(z.Entitaet + "Type", z.Typ);
        else
            // IFC2x3: Exemplar ist die allgemeine Entität (IfcFlowSegment, IfcWallStandardCase …), die Art steckt im Typobjekt;
            // Tür und Fenster haben «Style» statt «Type».
            Assert.True(z.Typ.EndsWith("Type") || z.Typ.EndsWith("Style"), $"{z.Typ} ist weder …Type noch …Style");
    }

    [Theory]
    [MemberData(nameof(Ziele))]
    public void PredefinedType_IstWertDerAufzaehlungDesTypobjekts(string schema, string klasse, string gewerk, string variante)
    {
        var (_, z) = Ziel(schema, klasse, gewerk, variante);
        if (z.PredefinedType is null) return;
        var werte = WerteFuer(schema, z);
        Assert.True(werte is not null, $"{Schluessel(schema, klasse, gewerk, variante)}: weder {z.Typ} noch {z.Entitaet} hat eine Eigenschaft PredefinedType");
        Assert.True(werte!.Contains(z.PredefinedType), $"{Schluessel(schema, klasse, gewerk, variante)}: PredefinedType {z.PredefinedType} ist kein Wert von {z.Typ ?? z.Entitaet} ({string.Join(", ", werte.Order())})");
    }

    [Theory]
    [MemberData(nameof(Ziele))]
    public void ObjectType_NurNeben_PredefinedType(string schema, string klasse, string gewerk, string variante)
    {
        var (_, z) = Ziel(schema, klasse, gewerk, variante);
        if (z.ObjectType is null) return;
        Assert.False(string.IsNullOrWhiteSpace(z.ObjectType));
        Assert.NotNull(z.PredefinedType);   // ein ObjectType unterscheidet innerhalb eines Werts bzw. bei USERDEFINED
    }

    [Theory]
    [MemberData(nameof(KlassenNamen))]
    public void PredefinedTypeJe_UndImportWerte_SindWerteDerAufzaehlung(string klasse)
    {
        var k = Abb.Klasse(klasse)!;
        foreach (var schema in new[] { "IFC4", "IFC2X3" })
        {
            var z = k.Fuer(schema)!;
            var werte = WerteFuer(schema, z);
            if (k.PredefinedTypeJe is { } je)
            {
                Assert.True(werte is not null, $"{klasse} {schema}: keine PredefinedType-Aufzählung für predefinedTypeJe");
                foreach (var (auswahl, wert) in je.Werte)
                    Assert.True(werte!.Contains(wert), $"{klasse} {schema}: predefinedTypeJe[{auswahl}] = {wert} ist kein Wert von {z.Typ ?? z.Entitaet}");
            }
            if (k.ImportWerte is { } iw && schema == "IFC4")
            {
                Assert.True(werte is not null, $"{klasse} {schema}: keine PredefinedType-Aufzählung für importWerte");
                foreach (var wert in iw)
                    Assert.True(werte!.Contains(wert), $"{klasse} {schema}: importWerte {wert} ist kein Wert von {z.Typ ?? z.Entitaet}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(KlassenNamen))]
    public void ImportWerte_Ifc2x3_SindWerteDerAufzaehlung_oderNurInIfc4(string klasse)
    {
        // importWerte gelten für beide Fassungen. Werte, die es nur in IFC4 gibt (z. B. Unterzug HOLLOWCORE, SPANDREL), können
        // in einer IFC2x3-Datei nicht vorkommen – sie sind dort harmlos, müssen aber IFC4-Werte sein (oben geprüft).
        var k = Abb.Klasse(klasse)!;
        if (k.ImportWerte is not { } iw) return;
        var z3 = k.Fuer("IFC2X3")!;
        // Fenster/Tür: in IFC2x3 haben weder Exemplar noch Stil (IfcWindowStyle/IfcDoorStyle) einen PredefinedType – dort
        // gibt es keinen Typwert, die importWerte gelten nur für IFC4
        if (WerteFuer("IFC2X3", z3) is not { } w3) return;
        var nurIfc4 = iw.Where(w => !w3.Contains(w)).ToList();
        var z4 = k.Fuer("IFC4")!;
        var w4 = WerteFuer("IFC4", z4)!;
        Assert.All(nurIfc4, w => Assert.Contains(w, w4));
        // Wichtig ist nur, dass IFC2x3 mindestens einen der Werte kennt
        Assert.Contains(iw, w3.Contains);
    }

    [Theory]
    [MemberData(nameof(KlassenNamen))]
    public void ImportAuch_SindEntitaetenImSchema(string klasse)
    {
        var k = Abb.Klasse(klasse)!;
        if (k.ImportAuch is not { } ia) return;
        // IfcRoof, …StandardCase, …ElementedCase: Jede Entität muss es in mindestens einer Fassung geben; in IFC4 kennt man
        // …StandardCase und …ElementedCase, in IFC2x3 …StandardCase
        foreach (var e in ia)
            Assert.True(EntitaetsKlasse(Ifc4, e) is not null || EntitaetsKlasse(Ifc2x3, e) is not null, $"{klasse}: importAuch {e} gibt es in keiner Fassung");
    }

    [Fact]
    public void Systemtypen_Ifc4_SindWerteVonIfcDistributionSystemEnum()
    {
        var werte = EnumWerte(Ifc4, "IfcDistributionSystemEnum");
        foreach (var s in Abb.Systemtypen)
            Assert.True(werte.Contains(s.Ifc4), $"Systemtyp «{s.Systemtyp}»: {s.Ifc4} ist kein Wert von IfcDistributionSystemEnum ({string.Join(", ", werte.Order())})");
    }

    [Fact]
    public void Systemtypen_IFC2x3_HabenKeinePredefinedType_DarumLaeuftDerImportUeberDenNamen()
    {
        // IfcSystem (IFC2x3) hat keine Eigenschaft PredefinedType – die Zuordnung geht dort nur über Namensmuster
        var t = EntitaetsKlasse(Ifc2x3, "IfcSystem")!;
        Assert.Null(t.GetProperty("PredefinedType"));
        Assert.All(Abb.Systemtypen, s => Assert.NotEmpty(s.Namen));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Fluss_WerteSindWerteVonIfcFlowDirectionEnum(string schema)
    {
        var werte = EnumWerte(Asm(schema), "IfcFlowDirectionEnum");
        Assert.All(Abb.Fluss, f => Assert.True(werte.Contains(f.Value), $"fluss[{f.Key}] = {f.Value} ist kein Wert von IfcFlowDirectionEnum ({schema})"));
        Assert.Equal(["beide", "raus", "rein"], Abb.Fluss.Keys.Order());
    }

    [Fact]
    public void Namensmuster_SindGueltigeRegulaereAusdruecke()
    {
        foreach (var s in Abb.Systemtypen)
            foreach (var muster in s.Namen)
            {
                var ex = Record.Exception(() => new Regex(muster, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
                Assert.True(ex is null, $"Systemtyp «{s.Systemtyp}»: Muster «{muster}» ist kein gültiger regulärer Ausdruck: {ex?.Message}");
                Assert.False(string.IsNullOrWhiteSpace(muster));
            }
    }

    // ------------------------------------------------------------------ Abgleich mit den Katalogen

    [Fact]
    public void JedeKlasseInIfcJson_GibtEsInKlassenJson_MitGleicherEntitaet()
    {
        foreach (var k in Abb.Klassen)
        {
            Assert.True(Kat.Klassen.TryGetValue(k.Klasse, out var def), $"Klasse {k.Klasse} aus ifc.json fehlt in klassen.json");
            Assert.True(string.Equals(def!.Ifc, k.Ifc4!.Entitaet, StringComparison.Ordinal),
                $"Klasse {k.Klasse}: klassen.json ifc = «{def.Ifc}», ifc.json ifc4.entitaet = «{k.Ifc4.Entitaet}»");
        }
    }

    [Fact]
    public void JedeKonkreteKatalogklasse_IstAbgebildetOderOhneIfc()
    {
        foreach (var d in Kat.Klassen.Values.Where(c => !c.Abstrakt))
        {
            bool abgebildet = Abb.Klasse(d.Name) is not null;
            bool ohne = Abb.OhneIfc.Contains(d.Name, StringComparer.OrdinalIgnoreCase);
            Assert.True(abgebildet || ohne, $"Konkrete Klasse {d.Name} steht weder in ifc.json klassen noch in ohneIfc");
            Assert.False(abgebildet && ohne, $"Klasse {d.Name} steht in klassen UND in ohneIfc");
        }
    }

    [Fact]
    public void OhneIfc_NenntNurKlassenDesKatalogs()
    {
        Assert.All(Abb.OhneIfc, n => Assert.True(Kat.Klassen.ContainsKey(n), $"ohneIfc: «{n}» gibt es im Katalog nicht"));
        Assert.Equal(Abb.OhneIfc.Count, Abb.OhneIfc.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void KlassenJson_IfcEntitaeten_GibtEsImIfc4Schema()
    {
        foreach (var d in Kat.Klassen.Values.Where(c => !string.IsNullOrEmpty(c.Ifc)))
            Assert.True(EntitaetsKlasse(Ifc4, d.Ifc!) is not null, $"Klasse {d.Name}: klassen.json ifc «{d.Ifc}» gibt es in IFC4 nicht");
    }

    [Fact]
    public void JederSystemtyp_GibtEsInSystemtypenJson_UndUmgekehrt()
    {
        foreach (var s in Abb.Systemtypen) Assert.True(Kat.Systemtypen.ContainsKey(s.Systemtyp), $"Systemtyp «{s.Systemtyp}» aus ifc.json fehlt in systemtypen.json");
        foreach (var n in Kat.Systemtypen.Keys)
            Assert.True(Abb.Systemtypen.Any(s => s.Systemtyp.Equals(n, StringComparison.OrdinalIgnoreCase)), $"Systemtyp «{n}» aus systemtypen.json fehlt in ifc.json");
        Assert.Equal(Abb.Systemtypen.Count, Abb.Systemtypen.Select(s => s.Systemtyp).Distinct().Count());
    }

    // ------------------------------------------------------------------ KlasseFuer: Rückweg

    [Theory]
    [MemberData(nameof(Ziele))]
    public void KlasseFuer_Rueckweg_AusDemZielWirdWiederDieKlasse(string schema, string klasse, string gewerk, string variante)
    {
        var (k, z) = Ziel(schema, klasse, gewerk, variante);
        if (schema == "IFC2X3" && z.Entitaet == "IfcSystem")
        {
            // Absichtlich mehrdeutig: IFC2x3 kennt nur IfcSystem (kein PredefinedType, keine Untertypen). Anlage, System, Strang und
            // Regelkreis sind dort allein über Name/Gruppierung unterscheidbar (SystemtypFuer, Übertrager); seit B4-6 ist KlasseFuer
            // bei gleichrangigen Klassen null, Kandidaten nennt alle vier.
            Assert.Null(Abb.KlasseFuer(schema, z.Entitaet, z.Typ, z.PredefinedType, z.ObjectType));
            Assert.Contains(Abb.Kandidaten(schema, z.Entitaet, z.Typ, z.PredefinedType, z.ObjectType), t => t.Klasse == "Anlage");
            return;
        }
        // Das Ziel unverändert (Entität, Typobjekt, PredefinedType, ObjectType) muss genau auf seine Klasse zurückführen. Wo die
        // Abbildung absichtlich mehrdeutig ist (Verteiler/TStueck beide JUNCTION; Speicher/Wassererwaermer; Spueltisch/Ausgussbecken),
        // entscheidet der ObjectType – er steht im Ziel der spezifischeren Klasse und wird hier mitgegeben.
        var t = Abb.KlasseFuer(schema, z.Entitaet, z.Typ, z.PredefinedType, z.ObjectType);
        Assert.True(t is not null, $"{Schluessel(schema, klasse, gewerk, variante)}: {z.Entitaet}/{z.Typ}/{z.PredefinedType}/{z.ObjectType} ergibt keine Klasse");
        Assert.True(t!.Klasse == k.Klasse, $"{Schluessel(schema, klasse, gewerk, variante)}: {z.Entitaet}/{z.Typ}/{z.PredefinedType}/{z.ObjectType} ergibt «{t.Klasse}» statt «{k.Klasse}» ({t.Grund})");
        if (gewerk == "lueftung") Assert.Equal("lueftung", t.Gewerk);
        else Assert.Null(t.Gewerk);
        // Varianten: «stellung» und «schaltung» haben dasselbe Ziel (IfcActuator) – dort ist nur die Klasse eindeutig
        bool eindeutig = variante.Length == 0 || k.VarianteJe!.Werte.Count(v => v.Value.Fuer(schema)!.Entitaet == z.Entitaet && v.Value.Fuer(schema)!.PredefinedType == z.PredefinedType) == 1;
        if (eindeutig) Assert.Equal(variante.Length == 0 ? null : variante, t.Variante);
    }

    [Theory]
    [MemberData(nameof(Ziele))]
    public void KlasseFuer_Ifc4_AuchOhneTypobjekt(string schema, string klasse, string gewerk, string variante)
    {
        if (schema != "IFC4") return;
        var (k, z) = Ziel(schema, klasse, gewerk, variante);
        var t = Abb.KlasseFuer(schema, z.Entitaet, null, z.PredefinedType, z.ObjectType);
        Assert.True(t is not null && t.Klasse == k.Klasse, $"{Schluessel(schema, klasse, gewerk, variante)}: ohne Typobjekt ergibt {z.Entitaet}/{z.PredefinedType}/{z.ObjectType} «{t?.Klasse}» statt «{k.Klasse}»");
    }

    [Theory]
    [InlineData("IFC2X3", "IfcFlowSegment")]
    [InlineData("IFC2X3", "IfcFlowFitting")]
    [InlineData("IFC2X3", "IfcFlowController")]
    [InlineData("IFC2X3", "IfcFlowTerminal")]
    [InlineData("IFC2X3", "IfcFlowMovingDevice")]
    [InlineData("IFC2X3", "IfcFlowStorageDevice")]
    [InlineData("IFC2X3", "IfcEnergyConversionDevice")]
    [InlineData("IFC2X3", "IfcDistributionControlElement")]
    public void KlasseFuer_Ifc2x3_AllgemeineEntitaetOhneTypobjekt_IstNichtEntscheidbar(string schema, string entitaet)
    {
        Assert.Null(Abb.KlasseFuer(schema, entitaet, null, null, null));
        Assert.Null(Abb.KlasseFuer(schema, entitaet, null, "NOTDEFINED", null));
    }

    [Fact]
    public void KlasseFuer_MehrdeutigeAbbildung_EntscheidetDerObjectType()
    {
        // Verteiler und T-Stück sind beide IfcPipeFitting JUNCTION
        Assert.Equal("TStueck", Abb.KlasseFuer("IFC4", "IfcPipeFitting", null, "JUNCTION", null)!.Klasse);
        Assert.Equal("Verteiler", Abb.KlasseFuer("IFC4", "IfcPipeFitting", null, "JUNCTION", "Verteiler HK 2.OG")!.Klasse);
        Assert.Equal("Verteiler", Abb.KlasseFuer("IFC4", "IfcPipeFitting", null, "JUNCTION", "VERTEILER")!.Klasse);   // Gross-/Kleinschreibung egal
        Assert.Equal("TStueck", Abb.KlasseFuer("IFC2X3", "IfcFlowFitting", "IfcPipeFittingType", "JUNCTION", null)!.Klasse);
        Assert.Equal("Verteiler", Abb.KlasseFuer("IFC2X3", "IfcFlowFitting", "IfcPipeFittingType", "JUNCTION", "Verteiler")!.Klasse);
        // Lüftungs-T-Stück bleibt TStueck, Gewerk lueftung (Verteiler gibt es nur als Rohrformteil)
        var l = Abb.KlasseFuer("IFC4", "IfcDuctFitting", null, "JUNCTION", "Verteiler")!;
        Assert.Equal("TStueck", l.Klasse); Assert.Equal("lueftung", l.Gewerk);
        // Speicher und Wassererwärmer: beide IfcTank STORAGE
        Assert.Equal("Speicher", Abb.KlasseFuer("IFC4", "IfcTank", null, "STORAGE", null)!.Klasse);
        Assert.Equal("Wassererwaermer", Abb.KlasseFuer("IFC4", "IfcTank", null, "STORAGE", "Wassererwaermer 800 l")!.Klasse);
        Assert.Equal("Speicher", Abb.KlasseFuer("IFC2X3", "IfcFlowStorageDevice", "IfcTankType", "PRESSUREVESSEL", null)!.Klasse);
        Assert.Equal("Wassererwaermer", Abb.KlasseFuer("IFC2X3", "IfcFlowStorageDevice", "IfcTankType", "PRESSUREVESSEL", "Wassererwaermer")!.Klasse);
        // Spültisch und Ausgussbecken: beide SINK
        Assert.Equal("Spueltisch", Abb.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, "SINK", null)!.Klasse);
        Assert.Equal("Ausgussbecken", Abb.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, "SINK", "Ausgussbecken")!.Klasse);
    }

    [Fact]
    public void KlasseFuer_Guete_ObjectType3_PredefinedType2_NurEntitaet1()
    {
        Assert.Equal(3, Abb.KlasseFuer("IFC4", "IfcPipeFitting", null, "JUNCTION", "Verteiler")!.Guete);
        Assert.Equal(2, Abb.KlasseFuer("IFC4", "IfcPipeFitting", null, "JUNCTION", null)!.Guete);
        // Rohr nennt NOTDEFINED in importWerte: ohne Typwert regulär (Güte 1)
        Assert.Equal(1, Abb.KlasseFuer("IFC4", "IfcPipeSegment", null, null, null)!.Guete);
        Assert.Equal(1, Abb.KlasseFuer("IFC4", "IfcPipeSegment", null, "NOTDEFINED", null)!.Guete);
        // Ziel verlangt einen Typwert, die Datei nennt keinen: Güte 0 für alle Sanitärapparate (auch WC trotz importWerte) → mehrdeutig
        var san = Abb.Kandidaten("IFC4", "IfcSanitaryTerminal", null, "NOTDEFINED", null);
        Assert.Contains(san, t => t.Klasse == "WC");
        Assert.All(san, t => Assert.Equal(0, t.Guete));
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcSanitaryTerminal", null, null, null));
        Assert.Equal(1, Abb.KlasseFuer("IFC4", "IfcDistributionSystem", null, null, null)!.Guete);   // System, Ziel ohne Typwert
    }

    [Fact]
    public void KlasseFuer_Architektur_ImportAuchUndImportWerte()
    {
        Assert.Equal("Wand", Abb.KlasseFuer("IFC4", "IfcWall", null, "STANDARD", null)!.Klasse);
        // Befund B1 vom 17.10.2026: andere Typwerte fielen durch
        foreach (var w in new[] { "SOLIDWALL", "PARTITIONING", "PARAPET", "NOTDEFINED" })
            Assert.Equal("Wand", Abb.KlasseFuer("IFC4", "IfcWall", null, w, null)!.Klasse);
        Assert.Equal("Wand", Abb.KlasseFuer("IFC4", "IfcWall", null, null, null)!.Klasse);
        Assert.Equal("Stuetze", Abb.KlasseFuer("IFC4", "IfcColumn", null, "PILASTER", null)!.Klasse);
        Assert.Equal("Wand", Abb.KlasseFuer("IFC4", "IfcWallStandardCase", null, null, null)!.Klasse);
        Assert.Equal("Wand", Abb.KlasseFuer("IFC4", "IfcWallElementedCase", null, null, null)!.Klasse);
        Assert.Equal("Wand", Abb.KlasseFuer("IFC2X3", "IfcWallStandardCase", null, null, null)!.Klasse);
        foreach (var w in new[] { "FLOOR", "BASESLAB", "LANDING", "NOTDEFINED", "USERDEFINED" })
        {
            Assert.Equal("Decke", Abb.KlasseFuer("IFC4", "IfcSlab", null, w, null)!.Klasse);
            Assert.Equal("Decke", Abb.KlasseFuer("IFC2X3", "IfcSlab", null, w, null)!.Klasse);
        }
        Assert.Equal("Decke", Abb.KlasseFuer("IFC4", "IfcSlabStandardCase", null, null, null)!.Klasse);
        Assert.Equal("Dach", Abb.KlasseFuer("IFC4", "IfcSlab", null, "ROOF", null)!.Klasse);
        Assert.Equal("Dach", Abb.KlasseFuer("IFC2X3", "IfcSlab", null, "ROOF", null)!.Klasse);
        Assert.Equal("Dach", Abb.KlasseFuer("IFC4", "IfcRoof", null, null, null)!.Klasse);
        Assert.Equal("Dach", Abb.KlasseFuer("IFC2X3", "IfcRoof", null, null, null)!.Klasse);
        Assert.Equal("Stuetze", Abb.KlasseFuer("IFC4", "IfcColumnStandardCase", null, null, null)!.Klasse);
        foreach (var w in new[] { "BEAM", "JOIST", "LINTEL", "T_BEAM", "NOTDEFINED", "USERDEFINED" })
            Assert.Equal("Traeger", Abb.KlasseFuer("IFC2X3", "IfcBeam", null, w, null)!.Klasse);
        foreach (var w in new[] { "HOLLOWCORE", "SPANDREL" })
            Assert.Equal("Traeger", Abb.KlasseFuer("IFC4", "IfcBeam", null, w, null)!.Klasse);
        Assert.Equal("Tuer", Abb.KlasseFuer("IFC2X3", "IfcDoor", null, null, null)!.Klasse);
        Assert.Equal("Fenster", Abb.KlasseFuer("IFC2X3", "IfcWindow", "IfcWindowStyle", null, null)!.Klasse);
        Assert.Equal("Fenster", Abb.KlasseFuer("IFC4", "IfcWindowStandardCase", null, null, null)!.Klasse);
    }

    [Fact]   // B4-1 behoben am 17.10.2026 (Hauptlauf)
    public void KlasseFuer_Ifc2x3_EinfachesIfcWall_IstWand()
    {
        Assert.Equal("Wand", Abb.KlasseFuer("IFC2X3", "IfcWall", null, null, null)?.Klasse);
        Assert.Equal("Wand", Abb.KlasseFuer("IFC2X3", "IfcWall", "IfcWallType", "STANDARD", null)?.Klasse);
    }

    [Fact]
    public void KlasseFuer_UnbekannteEntitaet_ErgibtNull_FalscherPredefinedType_AndereKlasseOderNull()
    {
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcStair", null, null, null));
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcFurniture", null, "NOTDEFINED", null));
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcBuildingElementProxy", null, null, null));
        Assert.Null(Abb.KlasseFuer("IFC2X3", "IfcBuildingElementProxy", null, null, null));
        // Ein Rohr-Typobjekt an einer Kanal-Entität (IFC2x3) passt zu keiner Klasse
        Assert.Null(Abb.KlasseFuer("IFC2X3", "IfcFlowSegment", "IfcWallType", null, null));
        // IfcValve: seit B4-5 sind alle Werte von IfcValveTypeEnum ein Ventil; ein Wert ausserhalb der Aufzählung nicht
        Assert.Equal("Ventil", Abb.KlasseFuer("IFC4", "IfcValve", null, "FLUSHING", null)?.Klasse);
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcValve", null, "BEND", null));
        // Gross-/Kleinschreibung egal
        Assert.Equal("Rohr", Abb.KlasseFuer("IFC4", "ifcpipesegment", null, "rigidsegment", null)!.Klasse);
    }

    [Fact]
    public void KlasseFuer_Ventil_PredefinedTypeJe()
    {
        foreach (var (art, wert) in Abb.Klasse("Ventil")!.PredefinedTypeJe!.Werte)
        {
            var t4 = Abb.KlasseFuer("IFC4", "IfcValve", null, wert, null);
            Assert.True(t4 is not null && t4.Klasse == "Ventil", $"Ventil art={art} ({wert}) IFC4 → {t4?.Klasse}");
            var t3 = Abb.KlasseFuer("IFC2X3", "IfcFlowController", "IfcValveType", wert, null);
            Assert.True(t3 is not null && t3.Klasse == "Ventil", $"Ventil art={art} ({wert}) IFC2x3 → {t3?.Klasse}");
        }
    }

    [Fact]
    public void KlasseFuer_Waermeerzeuger_Varianten()
    {
        var wp = Abb.KlasseFuer("IFC4", "IfcUnitaryEquipment", null, "USERDEFINED", "Waermepumpe")!;
        Assert.Equal(("Waermeerzeuger", "waermepumpe"), (wp.Klasse, wp.Variante));
        var lg = Abb.KlasseFuer("IFC4", "IfcUnitaryEquipment", null, "AIRHANDLER", null)!;
        Assert.Equal("Lueftungsgeraet", lg.Klasse);
        var fw = Abb.KlasseFuer("IFC4", "IfcHeatExchanger", null, "PLATE", null)!;
        Assert.Equal(("Waermeerzeuger", "fernwaerme"), (fw.Klasse, fw.Variante));
        var kessel = Abb.KlasseFuer("IFC4", "IfcBoiler", null, "WATER", null)!;
        Assert.Equal(("Waermeerzeuger", (string?)null), (kessel.Klasse, kessel.Variante));
        // Wärmepumpe ohne ObjectType: USERDEFINED verlangt den ObjectType → nicht als Wärmepumpe raten
        Assert.NotEqual("Waermeerzeuger", Abb.KlasseFuer("IFC4", "IfcUnitaryEquipment", null, "USERDEFINED", null)?.Klasse);
    }

    // ------------------------------------------------------------------ SystemtypFuer und FlussAus

    [Fact]
    public void SystemtypFuer_JederSystemtypErkenntSichAmEigenenNamen()
    {
        foreach (var s in Abb.Systemtypen)
        {
            Assert.Equal(s.Systemtyp, Abb.SystemtypFuer(s.Ifc4, s.Systemtyp, null));
            Assert.Equal(s.Systemtyp, Abb.SystemtypFuer(null, s.Systemtyp, s.ObjectType));
        }
    }

    [Fact]
    public void SystemtypFuer_PredefinedTypeAllein_NurWennEindeutig()
    {
        foreach (var gruppe in Abb.Systemtypen.GroupBy(s => s.Ifc4))
        {
            var erwartet = gruppe.Count() == 1 ? gruppe.First().Systemtyp : null;
            Assert.Equal(erwartet, Abb.SystemtypFuer(gruppe.Key, null, null));
        }
        Assert.Null(Abb.SystemtypFuer("HEATING", null, null));
        Assert.Null(Abb.SystemtypFuer("VENTILATION", "", ""));
        Assert.Equal("Trinkwasser kalt", Abb.SystemtypFuer("DOMESTICCOLDWATER", null, null));
        Assert.Equal("Schmutzwasser", Abb.SystemtypFuer("WASTEWATER", null, null));
        Assert.Null(Abb.SystemtypFuer(null, null, null));
        Assert.Null(Abb.SystemtypFuer("NOTDEFINED", "Gebäude", null));
    }

    [Theory]
    [InlineData("H VL NT 6", "Heizung Vorlauf")]
    [InlineData("H RL NT 6", "Heizung Rücklauf")]
    [InlineData("Kühlwasser Vorlauf 1", "Kühlwasser Vorlauf")]
    [InlineData("K RL", "Kühlwasser Rücklauf")]
    [InlineData("Sole Vorlauf Erdsonden", "Sole Vorlauf")]
    [InlineData("Sole RL", "Sole Rücklauf")]
    [InlineData("S WW Zirk", "Zirkulation")]
    [InlineData("PWH-C", "Zirkulation")]
    [InlineData("Kaltwasser", "Trinkwasser kalt")]
    [InlineData("PWC", "Trinkwasser kalt")]
    [InlineData("Warmwasser", "Trinkwasser warm")]
    [InlineData("PWH", "Trinkwasser warm")]
    [InlineData("Schmutzwasser", "Schmutzwasser")]
    [InlineData("Regenwasser Dach", "Regenwasser")]
    [InlineData("Aussenluft", "Aussenluft")]
    [InlineData("Zuluft 3", "Zuluft")]
    [InlineData("Abluft WC", "Abluft")]
    [InlineData("Fortluft", "Fortluft")]
    public void SystemtypFuer_Namensbeispiele(string name, string erwartet) => Assert.Equal(erwartet, Abb.SystemtypFuer(null, name, null));

    [Fact]
    public void SystemtypFuer_UnbekannterName_ErgibtNull_GrossKleinEgal()
    {
        Assert.Null(Abb.SystemtypFuer(null, "Brandschutzklappen", null));
        Assert.Equal("Zirkulation", Abb.SystemtypFuer(null, "ZIRKULATION", null));
        Assert.Equal("Abluft", Abb.SystemtypFuer(null, "abluft", null));
    }

    [Theory]
    [InlineData("SINK", "rein")]
    [InlineData("SOURCE", "raus")]
    [InlineData("SOURCEANDSINK", "beide")]
    [InlineData("sink", "rein")]
    [InlineData("NOTDEFINED", "beide")]
    [InlineData(null, "beide")]
    [InlineData("", "beide")]
    public void FlussAus_WerteVonIfcFlowDirectionEnum(string? ifc, string erwartet) => Assert.Equal(erwartet, Abb.FlussAus(ifc));

    // ------------------------------------------------------------------ Befunde aus den echten Dateien (Skip bis zur Korrektur im Produktcode)

    [Fact]   // B4-3 behoben am 17.10.2026 (Hauptlauf)
    public void Befund_B43_IfcRoofMitTypobjekt_IstDach()
    {
        Assert.Equal("Dach", Abb.KlasseFuer("IFC4", "IfcRoof", "IfcRoofType", "NOTDEFINED", null)?.Klasse);
        Assert.Equal("Dach", Abb.KlasseFuer("IFC2X3", "IfcRoof", "IfcTypeProduct", null, null)?.Klasse);
    }

    [Fact]   // B4-5 behoben am 17.10.2026 (Hauptlauf)
    public void Befund_B45_VentilMitAnderemPredefinedType_IstVentil()
    {
        foreach (var w in new[] { "DIVERTING", "PRESSURERELIEF", "FLUSHING", "SAFETYCUTOFF", "COMMISSIONING", "DOUBLECHECK", "DRAWOFFCOCK" })
            if (Enum.IsDefined(typeof(Xbim.Ifc4.Interfaces.IfcValveTypeEnum), w))
                Assert.Equal("Ventil", Abb.KlasseFuer("IFC4", "IfcValve", "IfcValveType", w, null)?.Klasse);
    }

    [Fact]   // B4-6 behoben am 17.10.2026 (Hauptlauf)
    public void Befund_B46_MehrdeutigeTypobjekteOhnePredefinedType_ErgebenKeineKlasse()
    {
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcSanitaryTerminal", "IfcSanitaryTerminalType", "NOTDEFINED", null));
        Assert.Null(Abb.KlasseFuer("IFC2X3", "IfcFlowFitting", "IfcPipeFittingType", "NOTDEFINED", null));
        Assert.Null(Abb.KlasseFuer("IFC2X3", "IfcFlowFitting", "IfcPipeFittingType", null, null));
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcPipeFitting", "IfcPipeFittingType", "NOTDEFINED", null));
        Assert.Null(Abb.KlasseFuer("IFC4", "IfcFlowMeter", "IfcFlowMeterType", "NOTDEFINED", null));
    }

    [Fact]   // B4-7 behoben am 17.10.2026 (Hauptlauf)
    public void Befund_B47_FensterMitSkylight_IstFenster()
    {
        Assert.Equal("Fenster", Abb.KlasseFuer("IFC4", "IfcWindow", "IfcWindowType", "SKYLIGHT", null)?.Klasse);
        Assert.Equal("Fenster", Abb.KlasseFuer("IFC4", "IfcWindow", "IfcWindowType", "LIGHTDOME", null)?.Klasse);
    }
}
