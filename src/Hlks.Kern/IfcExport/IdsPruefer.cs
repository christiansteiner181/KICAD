// IDS 1.0 (Information Delivery Specification, buildingSMART, Release v1.0.0 vom 03.06.2024): IDS-Dateien lesen und eine IFC-Datei
// dagegen prüfen (Modul 7 Teil B2). Eigener Prüfer statt Paket – Begründung (Prüfung 20.10.2026, nuget.org):
//   Xbim.IDS.Validator.Core 1.0.187 passt technisch zu Xbim.Essentials 6.1.605 (verlangt ≥ 6.0.578), steht aber unter AGPL-3.0-only:
//   KICAD wäre damit als Ganzes AGPL (Weitergabe und Netzzugriff mit Quellcode) – das entscheidet Christian, nicht ein Agent. Ohne
//   Paket genügt ein eigener Prüfer für die genutzten Facetten.
// Grundlage (nicht aus dem Gedächtnis): schema/ids-1.0/ids.xsd (unverändert) und die Dokumentation im Repository
// github.com/buildingSMART/IDS, Tag v1.0.0, Ordner Documentation (entity-, attribute-, property-, classification-, partof-facet.md,
// restrictions.md, developer-guide.md «Precision», units.md, ifc2x3-occurrence-type-mapping-table.md):
//   – entity: Name der IFC-Klasse genau (ohne Untertypen), PredefinedType am Exemplar, sonst am Typobjekt; USERDEFINED → ObjectType bzw.
//     ElementType. IFC2X3: Namen der IFC4-Konvention (IFCPIPESEGMENT …) über die Zuordnungstabelle Exemplar/Typobjekt.
//   – attribute: Attribut muss existieren und einen nicht leeren Wert haben.
//   – property: Property Set und Name (auch mit Einschränkungen), Datentyp, Wert; Werte des Typobjekts gelten, das Exemplar geht vor;
//     Mengen (IfcElementQuantity) zählen wie Eigenschaften. Längen in Metern (units.md: IFCLENGTHMEASURE m), die Datei wird umgerechnet.
//   – classification: System (Name der IfcClassification) und Wert (Identification bzw. ItemReference), auch über das Typobjekt.
//   – partOf: rekursiv über IfcRelAggregates, IfcRelAssignsToGroup, IfcRelContainedInSpatialStructure, IfcRelNests, IfcRelVoidsElement/
//     IfcRelFillsElement; mit relation nur über diese.
//   – material (einfach): Name oder Kategorie eines zugeordneten Materials.
//   – Kardinalität der Spezifikation (minOccurs/maxOccurs: required, optional, prohibited) und der Anforderungen (required, optional,
//     prohibited); Zahlen gleich innerhalb x·(1 ± 1e-6) ± 1e-6 (developer-guide.md).
// Nicht unterstützt (gemeldet statt geraten): xs:totalDigits/fractionDigits (IDS 1.0 sieht sie nicht vor), Einheiten ausser Länge.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Ein Wert einer Facette: einfacher Wert oder xs:restriction.</summary>
public sealed class IdsWert
{
    public string? Einfach { get; init; }
    public string? Basis { get; init; }
    public List<string> Aufzaehlung { get; } = [];
    public List<string> Muster { get; } = [];
    public double? MinInkl { get; set; }
    public double? MaxInkl { get; set; }
    public double? MinExkl { get; set; }
    public double? MaxExkl { get; set; }
    public int? Laenge { get; set; }
    public int? MinLaenge { get; set; }
    public int? MaxLaenge { get; set; }
    public List<string> NichtUnterstuetzt { get; } = [];

    static readonly ConcurrentDictionary<string, Regex> regeln = new(StringComparer.Ordinal);

    /// <summary>Passt der Wert (Text, Zahl, Wahrheitswert)? grossKlein = false: Texte ohne Gross-/Kleinschreibung (Klassennamen).</summary>
    public bool Passt(object? wert, bool grossKlein = true)
    {
        if (wert is null) return false;
        if (Einfach is not null) return Gleich(wert, Einfach, grossKlein);
        if (Aufzaehlung.Count > 0 && !Aufzaehlung.Any(a => Gleich(wert, a, grossKlein))) return false;
        var text = Text(wert);
        foreach (var m in Muster)
        {
            var r = regeln.GetOrAdd((grossKlein ? "" : "i:") + m, _ => new Regex("^(?:" + m + ")$", RegexOptions.CultureInvariant | (grossKlein ? RegexOptions.None : RegexOptions.IgnoreCase)));
            if (!r.IsMatch(text)) return false;
        }
        if (MinInkl is not null || MaxInkl is not null || MinExkl is not null || MaxExkl is not null)
        {
            if (Zahl(wert) is not double z) return false;
            if (MinInkl is double a && z < a - Toleranz(a)) return false;
            if (MaxInkl is double b && z > b + Toleranz(b)) return false;
            if (MinExkl is double c && z <= c) return false;
            if (MaxExkl is double d && z >= d) return false;
        }
        if (Laenge is int l && text.Length != l) return false;
        if (MinLaenge is int l0 && text.Length < l0) return false;
        if (MaxLaenge is int l1 && text.Length > l1) return false;
        return true;
    }

    static double Toleranz(double x) => Math.Abs(x) * 1e-6 + 1e-6;

    static bool Gleich(object wert, string soll, bool grossKlein)
    {
        switch (wert)
        {
            case bool b:
                return soll.Trim().ToLowerInvariant() switch { "true" or "1" => b, "false" or "0" => !b, _ => false };
            case double or float or int or long or decimal:
            {
                if (!double.TryParse(soll, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return false;
                var z = Convert.ToDouble(wert, CultureInfo.InvariantCulture);
                return Math.Abs(z - s) < Toleranz(s);   // developer-guide.md «Precision»: x·(1 ± 1e-6) ± 1e-6
            }
            default:
                return string.Equals(Text(wert), soll, grossKlein ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }
    }

    static string Text(object wert) => wert switch
    {
        bool b => b ? "true" : "false",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => wert.ToString() ?? "",
    };

    static double? Zahl(object wert) => wert switch
    {
        double or float or int or long or decimal => Convert.ToDouble(wert, CultureInfo.InvariantCulture),
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };

    public override string ToString()
    {
        if (Einfach is not null) return $"«{Einfach}»";
        var teile = new List<string>();
        if (Aufzaehlung.Count > 0) teile.Add("eines von " + string.Join(", ", Aufzaehlung.Select(a => $"«{a}»")));
        foreach (var m in Muster) teile.Add($"Muster «{m}»");
        if (MinInkl is double a) teile.Add($"≥ {a.ToString(CultureInfo.InvariantCulture)}");
        if (MinExkl is double b) teile.Add($"> {b.ToString(CultureInfo.InvariantCulture)}");
        if (MaxInkl is double c) teile.Add($"≤ {c.ToString(CultureInfo.InvariantCulture)}");
        if (MaxExkl is double d) teile.Add($"< {d.ToString(CultureInfo.InvariantCulture)}");
        if (Laenge is int l) teile.Add($"Länge {l}");
        if (MinLaenge is int l0) teile.Add($"Länge ≥ {l0}");
        if (MaxLaenge is int l1) teile.Add($"Länge ≤ {l1}");
        return teile.Count == 0 ? "beliebig" : string.Join(", ", teile);
    }
}

/// <summary>Eine Facette (Anwendbarkeit oder Anforderung).</summary>
public abstract class IdsFacette
{
    /// <summary>required | optional | prohibited (nur Anforderungen).</summary>
    public string Kardinalitaet { get; init; } = "required";
    public string? Anweisung { get; init; }
    public abstract string Art { get; }
    public abstract string Text();
}

public sealed class IdsEntitaet : IdsFacette
{
    public required IdsWert Name { get; init; }
    public IdsWert? Vordefiniert { get; init; }
    public override string Art => "entity";
    public override string Text() => $"Entität {Name}{(Vordefiniert is null ? "" : $" mit PredefinedType {Vordefiniert}")}";
}

public sealed class IdsAttribut : IdsFacette
{
    public required IdsWert Name { get; init; }
    public IdsWert? Wert { get; init; }
    public override string Art => "attribute";
    public override string Text() => $"Attribut {Name}{(Wert is null ? "" : $" = {Wert}")}";
}

public sealed class IdsEigenschaft : IdsFacette
{
    public required IdsWert Set { get; init; }
    public required IdsWert Name { get; init; }
    public IdsWert? Wert { get; init; }
    public string? Datentyp { get; init; }
    public override string Art => "property";
    public override string Text() => $"Eigenschaft {Set}.{Name}{(Datentyp is null ? "" : $" ({Datentyp})")}{(Wert is null ? "" : $" = {Wert}")}";
}

public sealed class IdsKlassifikation : IdsFacette
{
    public IdsWert? System { get; init; }
    public IdsWert? Wert { get; init; }
    public override string Art => "classification";
    public override string Text() => $"Klassifikation{(System is null ? "" : $" {System}")}{(Wert is null ? "" : $" Wert {Wert}")}";
}

public sealed class IdsTeilVon : IdsFacette
{
    public string? Beziehung { get; init; }
    public required IdsEntitaet Entitaet { get; init; }
    public override string Art => "partOf";
    public override string Text() => $"Teil von {Entitaet.Text()}{(Beziehung is null ? "" : $" über {Beziehung}")}";
}

public sealed class IdsMaterial : IdsFacette
{
    public IdsWert? Wert { get; init; }
    public override string Art => "material";
    public override string Text() => $"Material{(Wert is null ? "" : $" {Wert}")}";
}

public sealed class IdsSpezifikation
{
    public required string Name { get; init; }
    public string? Kennung { get; init; }
    public string? Beschreibung { get; init; }
    public string? Anweisung { get; init; }
    public required List<string> Fassungen { get; init; }
    public int MinOccurs { get; init; }
    /// <summary>null = unbounded.</summary>
    public int? MaxOccurs { get; init; }
    public List<IdsFacette> Anwendbarkeit { get; } = [];
    /// <summary>null = keine Anforderungen.</summary>
    public List<IdsFacette>? Anforderungen { get; set; }

    public string Kardinalitaet => MaxOccurs == 0 ? "prohibited" : MinOccurs >= 1 ? "required" : "optional";
}

public sealed class IdsDokument
{
    public required string Pfad { get; init; }
    public string Titel { get; set; } = "";
    public string? Version { get; set; }
    public string? Beschreibung { get; set; }
    public List<IdsSpezifikation> Spezifikationen { get; } = [];
    /// <summary>Meldungen der Prüfung gegen schema/ids-1.0/ids.xsd (leer = gültig).</summary>
    public List<string> XsdMeldungen { get; } = [];
    public bool XsdGeprueft { get; set; }
    public bool Gueltig => XsdMeldungen.Count == 0;
}

/// <summary>Schema der IDS 1.0 aus schema/ids-1.0 (ids.xsd unverändert; Auflösung der W3C-Importe lokal, siehe README dort).</summary>
public static class IdsSchema
{
    public const string Namensraum = "http://standards.buildingsmart.org/IDS";
    public const string XsNamensraum = "http://www.w3.org/2001/XMLSchema";
    public static readonly string[] Dateien = ["ids.xsd", "xml.xsd", "XMLSchema-teil.xsd"];

    static readonly ConcurrentDictionary<string, XmlSchemaSet> saetze = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ordner schema/ids-1.0 (neben dem Katalogordner, sonst aufwärts) oder null.</summary>
    public static string? Ordner(Katalog? katalog)
    {
        var kandidaten = new List<string>();
        if (katalog is not null && !string.IsNullOrEmpty(katalog.Ordner))
        {
            try
            {
                var oben = Path.GetDirectoryName(Path.GetFullPath(katalog.Ordner).TrimEnd('\\', '/'));
                if (oben is not null) kandidaten.Add(Path.Combine(oben, "schema", "ids-1.0"));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                kandidaten.Add(Path.Combine(d.FullName, "schema", "ids-1.0"));
                kandidaten.Add(Path.Combine(d.FullName, "HLKS_CAD", "schema", "ids-1.0"));
            }
        return kandidaten.FirstOrDefault(o => Dateien.All(f => File.Exists(Path.Combine(o, f))));
    }

    /// <summary>Löst die drei W3C-Importe von ids.xsd lokal auf: xml.xsd (W3C, unverändert), XMLSchema.xsd → XMLSchema-teil.xsd (die beiden
    /// Bestandteile, die ids.xsd braucht – .NET kann das Metaschema selbst nicht übersetzen), XMLSchema-instance → leer (in .NET eingebaut).
    /// Nichts wird aus dem Internet geladen.</summary>
    sealed class LokalAufloesen(string ordner) : XmlResolver
    {
        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (absoluteUri.IsFile) return File.OpenRead(absoluteUri.LocalPath);
            var name = Path.GetFileName(absoluteUri.AbsolutePath);
            if (absoluteUri.Host.EndsWith("w3.org", StringComparison.OrdinalIgnoreCase))
                return name switch
                {
                    "xml.xsd" => File.OpenRead(Path.Combine(ordner, "xml.xsd")),
                    "XMLSchema.xsd" => File.OpenRead(Path.Combine(ordner, "XMLSchema-teil.xsd")),
                    "XMLSchema-instance" => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                        "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" targetNamespace=\"http://www.w3.org/2001/XMLSchema-instance\"/>")),
                    _ => throw new XmlException($"Schema {absoluteUri} wird nicht geladen (nur lokale Schemas)."),
                };
            throw new XmlException($"Schema {absoluteUri} wird nicht geladen (nur lokale Schemas).");
        }
    }

    public static XmlSchemaSet Satz(string ordner) => saetze.GetOrAdd(ordner, o =>
    {
        var aufloeser = new LokalAufloesen(o);
        var s = new XmlSchemaSet { XmlResolver = aufloeser };
        var meldungen = new List<string>();
        s.ValidationEventHandler += (_, e) => meldungen.Add(e.Message);
        using (var r = XmlReader.Create(Path.Combine(o, "ids.xsd"), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = aufloeser }))
            s.Add(null, r);
        s.Compile();
        if (meldungen.Count > 0) throw new HlksFehler($"schema/ids-1.0 lässt sich nicht übersetzen: {string.Join(" | ", meldungen.Take(3))}", "schema/ids-1.0 aus dem Repository wiederherstellen.");
        return s;
    });

    /// <summary>Prüft eine IDS-Datei gegen ids.xsd. Leere Liste = gültig.</summary>
    public static List<string> Pruefen(string datei, string ordner)
    {
        var aus = new List<string>();
        var satz = Satz(ordner);
        lock (saetze)
        {
            var e = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = satz, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            e.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            e.ValidationEventHandler += (_, a) =>
            {
                var zeile = a.Exception?.LineNumber is int z and > 0 ? $" (Zeile {z})" : "";
                aus.Add((a.Severity == XmlSeverityType.Warning ? "Warnung: " : "") + a.Message + zeile);
            };
            try
            {
                using var r = XmlReader.Create(datei, e);
                while (r.Read()) { }
            }
            catch (XmlException ex) { aus.Add($"kein gültiges XML: {ex.Message}"); }
        }
        return aus;
    }
}

public static class IdsLeser
{
    static readonly XNamespace I = IdsSchema.Namensraum, Xs = IdsSchema.XsNamensraum;

    /// <summary>Liest eine IDS-Datei; prüft sie zuerst gegen ids.xsd (Meldungen im Dokument). Nicht lesbar = HlksFehler.</summary>
    public static IdsDokument Lesen(string pfad, Katalog? katalog)
    {
        if (!File.Exists(pfad)) throw new HlksFehler($"Die IDS-Datei «{pfad}» gibt es nicht.", "Vollständigen Pfad einer .ids-Datei angeben.");
        var doc = new IdsDokument { Pfad = pfad };
        if (IdsSchema.Ordner(katalog) is { } ordner)
        {
            doc.XsdMeldungen.AddRange(IdsSchema.Pruefen(pfad, ordner));
            doc.XsdGeprueft = true;
        }
        XDocument x;
        try { x = XDocument.Load(pfad, LoadOptions.SetLineInfo); }
        catch (XmlException ex) { throw new HlksFehler($"Die IDS-Datei «{Path.GetFileName(pfad)}» ist kein gültiges XML: {ex.Message}", "Datei mit einem IDS-Editor prüfen."); }
        var wurzel = x.Root;
        if (wurzel is null || wurzel.Name != I + "ids")
            throw new HlksFehler($"«{Path.GetFileName(pfad)}» ist keine IDS-Datei (Wurzel {wurzel?.Name}).", $"IDS 1.0: Wurzelelement ids im Namensraum {IdsSchema.Namensraum}.");
        var info = wurzel.Element(I + "info");
        doc.Titel = info?.Element(I + "title")?.Value ?? Path.GetFileNameWithoutExtension(pfad);
        doc.Version = info?.Element(I + "version")?.Value;
        doc.Beschreibung = info?.Element(I + "description")?.Value;
        foreach (var s in wurzel.Element(I + "specifications")?.Elements(I + "specification") ?? [])
        {
            var anw = s.Element(I + "applicability");
            var max = anw?.Attribute("maxOccurs")?.Value;
            var spez = new IdsSpezifikation
            {
                Name = s.Attribute("name")?.Value ?? "(ohne Name)", Kennung = s.Attribute("identifier")?.Value,
                Beschreibung = s.Attribute("description")?.Value, Anweisung = s.Attribute("instructions")?.Value,
                Fassungen = (s.Attribute("ifcVersion")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => v.ToUpperInvariant()).ToList(),
                MinOccurs = int.TryParse(anw?.Attribute("minOccurs")?.Value, out var mi) ? mi : 1,
                MaxOccurs = max is null || max == "unbounded" ? null : int.TryParse(max, out var ma) ? ma : null,
            };
            foreach (var f in anw?.Elements() ?? []) if (Facette(f, false) is { } fa) spez.Anwendbarkeit.Add(fa);
            if (s.Element(I + "requirements") is { } anf)
            {
                spez.Anforderungen = [];
                foreach (var f in anf.Elements()) if (Facette(f, true) is { } fa) spez.Anforderungen.Add(fa);
            }
            doc.Spezifikationen.Add(spez);
        }
        return doc;
    }

    static IdsFacette? Facette(XElement f, bool anforderung)
    {
        var kard = anforderung ? f.Attribute("cardinality")?.Value ?? "required" : "required";
        var anw = f.Attribute("instructions")?.Value;
        return f.Name.LocalName switch
        {
            "entity" => Entitaet(f, kard, anw),
            "attribute" => new IdsAttribut { Name = Wert(f.Element(I + "name")) ?? new IdsWert(), Wert = Wert(f.Element(I + "value")), Kardinalitaet = kard, Anweisung = anw },
            "property" => new IdsEigenschaft
            {
                Set = Wert(f.Element(I + "propertySet")) ?? new IdsWert(), Name = Wert(f.Element(I + "baseName")) ?? new IdsWert(),
                Wert = Wert(f.Element(I + "value")), Datentyp = f.Attribute("dataType")?.Value?.ToUpperInvariant(), Kardinalitaet = kard, Anweisung = anw,
            },
            "classification" => new IdsKlassifikation { System = Wert(f.Element(I + "system")), Wert = Wert(f.Element(I + "value")), Kardinalitaet = kard, Anweisung = anw },
            "partOf" => new IdsTeilVon
            {
                Beziehung = f.Attribute("relation")?.Value, Kardinalitaet = kard, Anweisung = anw,
                Entitaet = f.Element(I + "entity") is { } e ? Entitaet(e, "required", null) : new IdsEntitaet { Name = new IdsWert() },
            },
            "material" => new IdsMaterial { Wert = Wert(f.Element(I + "value")), Kardinalitaet = kard, Anweisung = anw },
            _ => null,
        };
    }

    static IdsEntitaet Entitaet(XElement f, string kard, string? anw) =>
        new() { Name = Wert(f.Element(I + "name")) ?? new IdsWert(), Vordefiniert = Wert(f.Element(I + "predefinedType")), Kardinalitaet = kard, Anweisung = anw };

    static IdsWert? Wert(XElement? e)
    {
        if (e is null) return null;
        if (e.Element(I + "simpleValue") is { } s) return new IdsWert { Einfach = s.Value };
        if (e.Element(Xs + "restriction") is not { } r) return new IdsWert { Einfach = e.Value };
        var w = new IdsWert { Basis = r.Attribute("base")?.Value };
        static double? D(XElement x) => double.TryParse(x.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        static int? N(XElement x) => int.TryParse(x.Attribute("value")?.Value, out var n) ? n : null;
        foreach (var x in r.Elements())
        {
            var v = x.Attribute("value")?.Value ?? "";
            switch (x.Name.LocalName)
            {
                case "enumeration": w.Aufzaehlung.Add(v); break;
                case "pattern": w.Muster.Add(v); break;
                case "minInclusive": w.MinInkl = D(x); break;
                case "maxInclusive": w.MaxInkl = D(x); break;
                case "minExclusive": w.MinExkl = D(x); break;
                case "maxExclusive": w.MaxExkl = D(x); break;
                case "length": w.Laenge = N(x); break;
                case "minLength": w.MinLaenge = N(x); break;
                case "maxLength": w.MaxLaenge = N(x); break;
                default: w.NichtUnterstuetzt.Add(x.Name.LocalName); break;
            }
        }
        return w;
    }
}

/// <summary>Prüft eine geöffnete IFC-Datei gegen IDS-Dokumente.</summary>
public sealed class IdsPruefer
{
    /// <summary>IFC2X3: Name im IDS (IFC4-Konvention) → (Exemplar-Entität, Typobjekt) – Tabelle aus
    /// Documentation/ifc2x3-occurrence-type-mapping-table.md (IDS v1.0.0). Geprüft wird über das Typobjekt.</summary>
    public static readonly IReadOnlyDictionary<string, (string Exemplar, string Typ)> Ifc2x3Zuordnung = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["IfcFurniture"] = ("IfcFurnishingElement", "IfcFurnitureType"), ["IfcSystemFurnitureElement"] = ("IfcFurnishingElement", "IfcSystemFurnitureElementType"),
        ["IfcActuator"] = ("IfcDistributionControlElement", "IfcActuatorType"), ["IfcAlarm"] = ("IfcDistributionControlElement", "IfcAlarmType"),
        ["IfcController"] = ("IfcDistributionControlElement", "IfcControllerType"), ["IfcFlowInstrument"] = ("IfcDistributionControlElement", "IfcFlowInstrumentType"),
        ["IfcSensor"] = ("IfcDistributionControlElement", "IfcSensorType"), ["IfcAirToAirHeatRecovery"] = ("IfcEnergyConversionDevice", "IfcAirToAirHeatRecoveryType"),
        ["IfcBoiler"] = ("IfcEnergyConversionDevice", "IfcBoilerType"), ["IfcChiller"] = ("IfcEnergyConversionDevice", "IfcChillerType"),
        ["IfcCoil"] = ("IfcEnergyConversionDevice", "IfcCoilType"), ["IfcCondenser"] = ("IfcEnergyConversionDevice", "IfcCondenserType"),
        ["IfcCooledBeam"] = ("IfcEnergyConversionDevice", "IfcCooledBeamType"), ["IfcCoolingTower"] = ("IfcEnergyConversionDevice", "IfcCoolingTowerType"),
        ["IfcElectricGenerator"] = ("IfcEnergyConversionDevice", "IfcElectricGeneratorType"), ["IfcElectricMotor"] = ("IfcEnergyConversionDevice", "IfcElectricMotorType"),
        ["IfcEvaporativeCooler"] = ("IfcEnergyConversionDevice", "IfcEvaporativeCoolerType"), ["IfcEvaporator"] = ("IfcEnergyConversionDevice", "IfcEvaporatorType"),
        ["IfcHeatExchanger"] = ("IfcEnergyConversionDevice", "IfcHeatExchangerType"), ["IfcHumidifier"] = ("IfcEnergyConversionDevice", "IfcHumidifierType"),
        ["IfcMotorConnection"] = ("IfcEnergyConversionDevice", "IfcMotorConnectionType"), ["IfcTransformer"] = ("IfcEnergyConversionDevice", "IfcTransformerType"),
        ["IfcTubeBundle"] = ("IfcEnergyConversionDevice", "IfcTubeBundleType"), ["IfcUnitaryEquipment"] = ("IfcEnergyConversionDevice", "IfcUnitaryEquipmentType"),
        ["IfcAirTerminalBox"] = ("IfcFlowController", "IfcAirTerminalBoxType"), ["IfcDamper"] = ("IfcFlowController", "IfcDamperType"),
        ["IfcElectricTimeControl"] = ("IfcFlowController", "IfcElectricTimeControlType"), ["IfcFlowMeter"] = ("IfcFlowController", "IfcFlowMeterType"),
        ["IfcProtectiveDevice"] = ("IfcFlowController", "IfcProtectiveDeviceType"), ["IfcSwitchingDevice"] = ("IfcFlowController", "IfcSwitchingDeviceType"),
        ["IfcValve"] = ("IfcFlowController", "IfcValveType"), ["IfcCableCarrierFitting"] = ("IfcFlowFitting", "IfcCableCarrierFittingType"),
        ["IfcDuctFitting"] = ("IfcFlowFitting", "IfcDuctFittingType"), ["IfcJunctionBox"] = ("IfcFlowFitting", "IfcJunctionBoxType"),
        ["IfcPipeFitting"] = ("IfcFlowFitting", "IfcPipeFittingType"), ["IfcCompressor"] = ("IfcFlowMovingDevice", "IfcCompressorType"),
        ["IfcFan"] = ("IfcFlowMovingDevice", "IfcFanType"), ["IfcPump"] = ("IfcFlowMovingDevice", "IfcPumpType"),
        ["IfcCableCarrierSegment"] = ("IfcFlowSegment", "IfcCableCarrierSegmentType"), ["IfcCableSegment"] = ("IfcFlowSegment", "IfcCableSegmentType"),
        ["IfcDuctSegment"] = ("IfcFlowSegment", "IfcDuctSegmentType"), ["IfcPipeSegment"] = ("IfcFlowSegment", "IfcPipeSegmentType"),
        ["IfcElectricFlowStorageDevice"] = ("IfcFlowStorageDevice", "IfcElectricFlowStorageDeviceType"), ["IfcTank"] = ("IfcFlowStorageDevice", "IfcTankType"),
        ["IfcAirTerminal"] = ("IfcFlowTerminal", "IfcAirTerminalType"), ["IfcElectricAppliance"] = ("IfcFlowTerminal", "IfcElectricApplianceType"),
        ["IfcFireSuppressionTerminal"] = ("IfcFlowTerminal", "IfcFireSuppressionTerminalType"), ["IfcLamp"] = ("IfcFlowTerminal", "IfcLampType"),
        ["IfcLightFixture"] = ("IfcFlowTerminal", "IfcLightFixtureType"), ["IfcOutlet"] = ("IfcFlowTerminal", "IfcOutletType"),
        ["IfcSanitaryTerminal"] = ("IfcFlowTerminal", "IfcSanitaryTerminalType"), ["IfcSpaceHeater"] = ("IfcFlowTerminal", "IfcSpaceHeaterType"),
        ["IfcStackTerminal"] = ("IfcFlowTerminal", "IfcStackTerminalType"), ["IfcWasteTerminal"] = ("IfcFlowTerminal", "IfcWasteTerminalType"),
        ["IfcDuctSilencer"] = ("IfcFlowTreatmentDevice", "IfcDuctSilencerType"), ["IfcFilter"] = ("IfcFlowTreatmentDevice", "IfcFilterType"),
        ["IfcVibrationIsolator"] = ("IfcElementComponent", "IfcVibrationIsolatorType"),
    };

    readonly IfcDatei d;
    readonly Func<IPersistEntity, string?> kennzeichen;
    readonly bool ifc2x3;
    Dictionary<string, List<IPersistEntity>>? jeEntitaet;
    Dictionary<int, IIfcTypeObject>? typen;
    Dictionary<int, List<IIfcPropertySetDefinition>>? psets;
    Dictionary<int, List<(string Beziehung, IPersistEntity Eltern)>>? eltern;
    Dictionary<int, List<IIfcRelAssociates>>? zuordnungen;

    public IdsPruefer(IfcDatei d, Func<IPersistEntity, string?>? kennzeichen = null)
    {
        this.d = d;
        this.kennzeichen = kennzeichen ?? (_ => null);
        ifc2x3 = d.Schema == "IFC2X3";
    }

    /// <summary>Prüft ein Dokument; Ergebnis je Spezifikation.</summary>
    public JsonObject Pruefen(IdsDokument doc)
    {
        var regeln = new JsonArray();
        int bestanden = 0, nicht = 0, ohne = 0;
        foreach (var s in doc.Spezifikationen)
        {
            var r = Spezifikation(s);
            var erg = r["ergebnis"]!.GetValue<string>();
            if (erg == "bestanden") bestanden++; else if (erg == "nicht bestanden") nicht++; else ohne++;
            regeln.Add(r);
        }
        return new JsonObject
        {
            ["datei"] = doc.Pfad, ["titel"] = doc.Titel, ["version"] = doc.Version,
            ["xsd"] = doc.XsdGeprueft ? (doc.Gueltig ? "gültig (schema/ids-1.0/ids.xsd)" : "ungültig") : "nicht geprüft (schema/ids-1.0 nicht gefunden)",
            ["xsdMeldungen"] = new JsonArray(doc.XsdMeldungen.Take(20).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["ergebnis"] = !doc.Gueltig ? "IDS ungültig" : nicht > 0 ? "nicht bestanden" : "bestanden",
            ["regelnBestanden"] = bestanden, ["regelnNichtBestanden"] = nicht, ["regelnNichtAnwendbar"] = ohne,
            ["regeln"] = regeln,
        };
    }

    JsonObject Spezifikation(IdsSpezifikation s)
    {
        var o = new JsonObject
        {
            ["name"] = s.Name, ["kennung"] = s.Kennung, ["beschreibung"] = s.Beschreibung, ["ifcVersion"] = string.Join(" ", s.Fassungen),
            ["kardinalitaet"] = s.Kardinalitaet,
            ["anwendbarkeit"] = new JsonArray(s.Anwendbarkeit.Select(f => (JsonNode)JsonValue.Create(f.Text())!).ToArray()),
            ["anforderungen"] = s.Anforderungen is null ? null : new JsonArray(s.Anforderungen.Select(f => (JsonNode)JsonValue.Create($"{f.Text()} [{f.Kardinalitaet}]")!).ToArray()),
        };
        if (!s.Fassungen.Contains(d.Schema))
        {
            o["ergebnis"] = "nicht anwendbar (Fassung)";
            o["grund"] = $"gilt für {string.Join(", ", s.Fassungen)}, die Datei ist {d.Schema}";
            return o;
        }
        var unbekannt = s.Anwendbarkeit.Concat(s.Anforderungen ?? []).SelectMany(Werte).SelectMany(w => w.NichtUnterstuetzt).Distinct().ToList();
        var anwendbar = Kandidaten(s).Where(x => s.Anwendbarkeit.All(f => Trifft(f, x))).ToList();
        var befunde = new JsonArray();
        int fehler = 0;
        if (s.Anforderungen is { Count: > 0 } anf && s.MaxOccurs != 0)
            foreach (var x in anwendbar)
            {
                var gruende = new List<string>();
                foreach (var f in anf)
                    if (Anforderung(f, x) is { } grund) gruende.Add(grund);
                if (gruende.Count == 0) continue;
                fehler++;
                if (befunde.Count < 200)
                    befunde.Add(new JsonObject
                    {
                        ["kennzeichen"] = Kennzeichen(x), ["globalId"] = (x as IIfcRoot)?.GlobalId.ToString(), ["entitaet"] = x.ExpressType.Name,
                        ["gruende"] = new JsonArray(gruende.Select(g => (JsonNode)JsonValue.Create(g)!).ToArray()),
                    });
            }
        string ergebnis; string? grundText = null;
        if (s.MaxOccurs == 0 && anwendbar.Count > 0) { ergebnis = "nicht bestanden"; grundText = $"{anwendbar.Count} Elemente, die es nicht geben darf (prohibited)"; }
        else if (s.MinOccurs >= 1 && anwendbar.Count == 0) { ergebnis = "nicht bestanden"; grundText = "kein anwendbares Element, mindestens eines verlangt (required)"; }
        else if (fehler > 0) ergebnis = "nicht bestanden";
        else ergebnis = anwendbar.Count == 0 ? "bestanden (kein anwendbares Element)" : "bestanden";
        if (s.MaxOccurs == 0)
            foreach (var x in anwendbar.Take(200))
                befunde.Add(new JsonObject { ["kennzeichen"] = Kennzeichen(x), ["globalId"] = (x as IIfcRoot)?.GlobalId.ToString(), ["entitaet"] = x.ExpressType.Name, ["gruende"] = new JsonArray("darf nicht vorkommen") });
        o["ergebnis"] = ergebnis.StartsWith("bestanden") ? "bestanden" : ergebnis;
        if (ergebnis == "bestanden (kein anwendbares Element)") o["hinweis"] = "kein anwendbares Element";
        o["grund"] = grundText;
        o["anwendbar"] = anwendbar.Count;
        o["bestandenElemente"] = s.MaxOccurs == 0 ? 0 : anwendbar.Count - fehler;
        o["nichtBestandenElemente"] = s.MaxOccurs == 0 ? anwendbar.Count : fehler;
        if (unbekannt.Count > 0) o["nichtUnterstuetzt"] = new JsonArray(unbekannt.Select(u => (JsonNode)JsonValue.Create($"xs:{u} (IDS 1.0 sieht es nicht vor) – ignoriert")!).ToArray());
        o["befunde"] = befunde;
        return o;
    }

    static IEnumerable<IdsWert> Werte(IdsFacette f)
    {
        IdsWert?[] w = f switch
        {
            IdsEntitaet e => [e.Name, e.Vordefiniert],
            IdsAttribut a => [a.Name, a.Wert],
            IdsEigenschaft p => [p.Set, p.Name, p.Wert],
            IdsKlassifikation k => [k.System, k.Wert],
            IdsTeilVon t => [t.Entitaet.Name, t.Entitaet.Vordefiniert],
            IdsMaterial m => [m.Wert],
            _ => [],
        };
        return w.OfType<IdsWert>();
    }

    string Kennzeichen(IPersistEntity x) =>
        kennzeichen(x) ?? (x as IIfcElement)?.Tag?.ToString() ?? (x as IIfcRoot)?.Name?.ToString() ?? $"#{x.EntityLabel}";

    // ------------------------------------------------------------------ Kandidaten

    IEnumerable<IPersistEntity> Kandidaten(IdsSpezifikation s)
    {
        if (s.Anwendbarkeit.OfType<IdsEntitaet>().FirstOrDefault() is not { } ent) return d.Modell.Instances;
        Index();
        var namen = jeEntitaet!.Keys.Where(n => ent.Name.Passt(n, false)).ToList();
        var aus = namen.SelectMany(n => jeEntitaet[n]);
        return ifc2x3 ? aus.Concat(Zugeordnet(ent)).Distinct() : aus;
    }

    /// <summary>IFC2X3: Exemplare, deren Typobjekt nach der Zuordnungstabelle zu einem Namen der Facette gehört. Entschieden wird über das
    /// Typobjekt allein: die Tabelle nennt für IfcSpaceHeater das Exemplar IfcFlowTerminal, im Schema IFC2X3 ist IfcSpaceHeaterType aber ein
    /// IfcEnergyConversionDeviceType (Xbim.Ifc2x3 6.1.605, geprüft 20.10.2026) – ein strenger Abgleich des Exemplars fände dort nichts.</summary>
    IEnumerable<IPersistEntity> Zugeordnet(IdsEntitaet ent)
    {
        Typ(null);
        foreach (var (ids, (_, typ)) in Ifc2x3Zuordnung)
            if (ent.Name.Passt(ids.ToUpperInvariant(), false) && jeTyp!.TryGetValue(typ.ToUpperInvariant(), out var l))
                foreach (var x in l) yield return x;
    }

    Dictionary<string, List<IPersistEntity>>? jeTyp;

    void Index()
    {
        if (jeEntitaet is not null) return;
        jeEntitaet = new Dictionary<string, List<IPersistEntity>>(StringComparer.Ordinal);
        foreach (var x in d.Modell.Instances)
        {
            var n = x.ExpressType.ExpressNameUpper;
            if (!jeEntitaet.TryGetValue(n, out var l)) jeEntitaet[n] = l = [];
            l.Add(x);
        }
    }

    // ------------------------------------------------------------------ Anwendbarkeit und Anforderungen

    bool Trifft(IdsFacette f, IPersistEntity x) => f switch
    {
        IdsEntitaet e => EntitaetTrifft(e, x),
        IdsAttribut a => Attribute(a, x).Any(w => a.Wert is null || a.Wert.Passt(w.Wert)),
        IdsEigenschaft p => Eigenschaften(p, x).Any(w => DatentypPasst(p, w.Typ) && (p.Wert is null || p.Wert.Passt(w.Wert))),
        IdsKlassifikation k => Klassen(x).Any(c => KlasseTrifft(k, c)),
        IdsTeilVon t => Vorfahren(x, t.Beziehung).Any(v => EntitaetTrifft(t.Entitaet, v)),
        IdsMaterial m => Materialien(x).Any(n => m.Wert is null || m.Wert.Passt(n)),
        _ => false,
    };

    /// <summary>null = erfüllt, sonst der Grund.</summary>
    string? Anforderung(IdsFacette f, IPersistEntity x)
    {
        var k = f.Kardinalitaet;
        switch (f)
        {
            case IdsEntitaet e:
                return EntitaetTrifft(e, x) ? null : $"ist {x.ExpressType.Name}{(Vordefiniert(x) is { } pt ? $" ({pt})" : "")}, verlangt {e.Text()}";
            case IdsAttribut a:
            {
                var gef = Attribute(a, x).ToList();
                var falsch = a.Wert is null ? [] : gef.Where(w => !a.Wert.Passt(w.Wert)).ToList();
                return k switch
                {
                    "prohibited" => gef.Any(w => a.Wert is null || a.Wert.Passt(w.Wert)) ? $"{a.Text()} darf nicht gesetzt sein" : null,
                    "optional" => falsch.Count > 0 ? $"{a.Text()}: Wert {Liste(falsch.Select(w => w.Wert))}" : null,
                    _ => gef.Count == 0 ? $"{a.Text()} fehlt" : falsch.Count > 0 ? $"{a.Text()}: Wert {Liste(falsch.Select(w => w.Wert))}" : null,
                };
            }
            case IdsEigenschaft p:
            {
                var gef = Eigenschaften(p, x).ToList();
                bool Gut((string Set, string Name, object Wert, string Typ) w) => DatentypPasst(p, w.Typ) && (p.Wert is null || p.Wert.Passt(w.Wert));
                var falsch = gef.Where(w => !Gut(w)).ToList();
                string Fehltext() => $"{p.Text()}: {string.Join(", ", falsch.Take(3).Select(w => $"{w.Set}.{w.Name} = {Text(w.Wert)} ({w.Typ})"))}";
                return k switch
                {
                    "prohibited" => gef.Any(Gut) ? $"{p.Text()} darf nicht vorhanden sein" : null,
                    "optional" => falsch.Count > 0 ? Fehltext() : null,
                    _ => gef.Count == 0 ? $"{p.Text()} fehlt" : falsch.Count > 0 ? Fehltext() : null,
                };
            }
            case IdsKlassifikation kl:
            {
                bool da = Klassen(x).Any(c => KlasseTrifft(kl, c));
                return k == "prohibited" ? (da ? $"{kl.Text()} darf nicht zugeordnet sein" : null)
                    : k == "optional" ? null
                    : da ? null : $"{kl.Text()} fehlt";
            }
            case IdsTeilVon t:
            {
                bool da = Vorfahren(x, t.Beziehung).Any(v => EntitaetTrifft(t.Entitaet, v));
                return k == "prohibited" ? (da ? $"darf nicht {t.Text()} sein" : null) : da ? null : $"nicht {t.Text()}";
            }
            case IdsMaterial m:
            {
                var namen = Materialien(x).ToList();
                bool da = namen.Any(n => m.Wert is null || m.Wert.Passt(n));
                return k == "prohibited" ? (da ? $"{m.Text()} darf nicht zugeordnet sein" : null)
                    : k == "optional" ? (namen.Count > 0 && !da ? $"{m.Text()}: {string.Join(", ", namen)}" : null)
                    : da ? null : namen.Count == 0 ? $"{m.Text()} fehlt" : $"{m.Text()}: {string.Join(", ", namen)}";
            }
        }
        return null;
    }

    static string Liste(IEnumerable<object> w) => string.Join(", ", w.Take(3).Select(Text));
    static string Text(object? w) => w switch { null => "–", double z => z.ToString("0.######", CultureInfo.InvariantCulture), bool b => b ? "true" : "false", _ => w.ToString() ?? "" };

    // ------------------------------------------------------------------ entity

    bool EntitaetTrifft(IdsEntitaet e, IPersistEntity x)
    {
        bool name = e.Name.Passt(x.ExpressType.ExpressNameUpper, false);
        if (!name && ifc2x3 && x is IIfcObject o && Typ(o) is { } t)
            name = Ifc2x3Zuordnung.Any(z => z.Value.Typ.Equals(t.ExpressType.Name, StringComparison.OrdinalIgnoreCase) && e.Name.Passt(z.Key.ToUpperInvariant(), false));
        if (!name) return false;
        return e.Vordefiniert is null || (Vordefiniert(x) is { } pt && e.Vordefiniert.Passt(pt));
    }

    static string? Aufz(object o) =>
        o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(p => p.Name == "PredefinedType")?.GetValue(o)?.ToString();

    /// <summary>PredefinedType: Exemplar, sonst Typobjekt; USERDEFINED → ObjectType bzw. ElementType.</summary>
    string? Vordefiniert(IPersistEntity x)
    {
        var w = Aufz(x);
        string? userdefined = null;
        if (x is IIfcObject o)
        {
            if (w == "USERDEFINED") userdefined = o.ObjectType?.ToString();
            var t = Typ(o);
            if ((w is null or "NOTDEFINED") && t is not null)
            {
                var tw = Aufz(t);
                if (tw is not null) w = tw;
                if (tw == "USERDEFINED") userdefined = (t as IIfcElementType)?.ElementType?.ToString() ?? o.ObjectType?.ToString();
            }
        }
        else if (x is IIfcElementType et && w == "USERDEFINED") userdefined = et.ElementType?.ToString();
        return w == "USERDEFINED" ? (string.IsNullOrEmpty(userdefined) ? w : userdefined) : w;
    }

    IIfcTypeObject? Typ(IIfcObject? o)
    {
        if (typen is null)
        {
            typen = [];
            jeTyp = new Dictionary<string, List<IPersistEntity>>(StringComparer.Ordinal);
            foreach (var r in d.Modell.Instances.OfType<IIfcRelDefinesByType>())
            {
                var n = r.RelatingType.ExpressType.ExpressNameUpper;
                if (!jeTyp.TryGetValue(n, out var l)) jeTyp[n] = l = [];
                foreach (var k in r.RelatedObjects) { typen[k.EntityLabel] = r.RelatingType; l.Add(k); }
            }
        }
        return o is null ? null : typen.GetValueOrDefault(o.EntityLabel);
    }

    // ------------------------------------------------------------------ attribute

    IEnumerable<(string Name, object Wert)> Attribute(IdsAttribut a, IPersistEntity x)
    {
        foreach (var mp in x.ExpressType.Properties.Values)
        {
            if (!a.Name.Passt(mp.Name)) continue;
            object? roh;
            try { roh = mp.PropertyInfo.GetValue(x); } catch (TargetInvocationException) { continue; }
            if (Attributwert(roh, mp.PropertyInfo.PropertyType) is { } w) yield return (mp.Name, w);
        }
    }

    object? Attributwert(object? roh, Type typ)
    {
        switch (roh)
        {
            case null: return null;
            case string s: return s.Length == 0 ? null : s;
            case Enum e: return e.ToString();
            case IExpressValueType v:
            {
                var inner = v.Value;
                if (inner is string s2) return s2.Length == 0 ? null : s2;
                if (inner is double z && v.GetType().Name.Contains("LengthMeasure", StringComparison.Ordinal)) return z * d.MmJeEinheit / 1000;
                return inner;
            }
            case System.Collections.IEnumerable l when roh is not string: return l.Cast<object>().Any() ? "(Liste)" : null;
            case IPersistEntity: return "(Objekt)";
            default: return roh;
        }
    }

    // ------------------------------------------------------------------ property

    bool DatentypPasst(IdsEigenschaft p, string typ) => p.Datentyp is null || typ.Equals(p.Datentyp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Eigenschaften (Exemplar vor Typobjekt), die zu Set und Name passen; Werte nicht leer, Längen in m.</summary>
    IEnumerable<(string Set, string Name, object Wert, string Typ)> Eigenschaften(IdsEigenschaft p, IPersistEntity x)
    {
        var gesehen = new HashSet<(string, string)>();
        foreach (var def in PsetDefinitionen(x))
        {
            var set = def.Name?.ToString() ?? "";
            if (!p.Set.Passt(set)) continue;
            if (def is IIfcPropertySet ps)
                foreach (var pr in ps.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    var n = pr.Name.ToString();
                    if (!p.Name.Passt(n) || !gesehen.Add((set, n))) continue;
                    if (pr.NominalValue is not { } nv) continue;
                    var w = nv.Value;
                    if (w is string s && s.Length == 0) continue;
                    var typName = nv.GetType().Name;
                    if (w is double z && typName.Contains("LengthMeasure", StringComparison.Ordinal)) w = z * d.MmJeEinheit / 1000;
                    yield return (set, n, w, typName.ToUpperInvariant());
                }
            else if (def is IIfcElementQuantity q)
                foreach (var m in q.Quantities)
                {
                    var n = m.Name.ToString();
                    if (!p.Name.Passt(n) || !gesehen.Add((set, n))) continue;
                    (object? w, string t) = m switch
                    {
                        IIfcQuantityLength l => ((object?)((double)l.LengthValue * d.MmJeEinheit / 1000), "IFCLENGTHMEASURE"),
                        IIfcQuantityArea a => ((double)a.AreaValue, "IFCAREAMEASURE"),
                        IIfcQuantityVolume v => ((double)v.VolumeValue, "IFCVOLUMEMEASURE"),
                        IIfcQuantityCount c => ((double)c.CountValue, "IFCCOUNTMEASURE"),
                        IIfcQuantityWeight g => ((double)g.WeightValue, "IFCMASSMEASURE"),
                        _ => (null, ""),
                    };
                    if (w is not null) yield return (set, n, w, t);
                }
        }
    }

    /// <summary>Property-Set-Definitionen des Objekts, dann die seines Typobjekts.</summary>
    IEnumerable<IIfcPropertySetDefinition> PsetDefinitionen(IPersistEntity x)
    {
        if (psets is null)
        {
            psets = [];
            foreach (var r in d.Modell.Instances.OfType<IIfcRelDefinesByProperties>())
            {
                var defs = r.RelatingPropertyDefinition switch
                {
                    IIfcPropertySetDefinition def => [def],
                    { } menge when menge.GetType().GetProperty("Value")?.GetValue(menge) is System.Collections.IEnumerable liste => liste.OfType<IIfcPropertySetDefinition>().ToList(),
                    _ => new List<IIfcPropertySetDefinition>(),
                };
                foreach (var k in r.RelatedObjects)
                {
                    if (!psets.TryGetValue(k.EntityLabel, out var l)) psets[k.EntityLabel] = l = [];
                    l.AddRange(defs);
                }
            }
        }
        if (psets.TryGetValue(x.EntityLabel, out var eigene)) foreach (var def in eigene) yield return def;
        var typ = x as IIfcTypeObject ?? (x is IIfcObject o ? Typ(o) : null);
        if (typ?.HasPropertySets is { } tps) foreach (var def in tps) yield return def;
    }

    // ------------------------------------------------------------------ classification

    sealed record Referenz(string? System, string? Wert);

    IEnumerable<Referenz> Klassen(IPersistEntity x)
    {
        foreach (var r in Zuordnungen(x).OfType<IIfcRelAssociatesClassification>())
            switch (r.RelatingClassification)
            {
                case IIfcClassificationReference cr:
                {
                    string? system = null;
                    IIfcClassificationReferenceSelect? q = cr.ReferencedSource;
                    for (int i = 0; i < 20 && q is not null; i++)
                    {
                        if (q is IIfcClassification c) { system = c.Name.ToString(); break; }
                        q = (q as IIfcClassificationReference)?.ReferencedSource;
                    }
                    yield return new Referenz(system, cr.Identification?.ToString());
                    break;
                }
                case IIfcClassification c:
                    yield return new Referenz(c.Name.ToString(), null);
                    break;
            }
    }

    static bool KlasseTrifft(IdsKlassifikation k, Referenz r) =>
        (k.System is null || (r.System is not null && k.System.Passt(r.System))) && (k.Wert is null || (r.Wert is not null && k.Wert.Passt(r.Wert)));

    IEnumerable<IIfcRelAssociates> Zuordnungen(IPersistEntity x)
    {
        if (zuordnungen is null)
        {
            zuordnungen = [];
            foreach (var r in d.Modell.Instances.OfType<IIfcRelAssociates>())
                foreach (var o in r.RelatedObjects)
                {
                    if (o is not IPersistEntity pe) continue;
                    if (!zuordnungen.TryGetValue(pe.EntityLabel, out var l)) zuordnungen[pe.EntityLabel] = l = [];
                    l.Add(r);
                }
        }
        if (zuordnungen.TryGetValue(x.EntityLabel, out var eigene)) foreach (var r in eigene) yield return r;
        if (x is IIfcObject o2 && Typ(o2) is { } t && zuordnungen.TryGetValue(t.EntityLabel, out var vomTyp)) foreach (var r in vomTyp) yield return r;
    }

    // ------------------------------------------------------------------ material

    IEnumerable<string> Materialien(IPersistEntity x)
    {
        foreach (var r in Zuordnungen(x).OfType<IIfcRelAssociatesMaterial>())
            foreach (var m in MaterialNamen(r.RelatingMaterial)) yield return m;
    }

    static IEnumerable<string> MaterialNamen(object? m)
    {
        switch (m)
        {
            case IIfcMaterial mt:
                yield return mt.Name.ToString();
                if (mt.Category?.ToString() is { Length: > 0 } kat) yield return kat;
                break;
            case IIfcMaterialList l:
                foreach (var x in l.Materials) foreach (var n in MaterialNamen(x)) yield return n;
                break;
            case IIfcMaterialLayerSetUsage u:
                foreach (var n in MaterialNamen(u.ForLayerSet)) yield return n;
                break;
            case IIfcMaterialLayerSet ls:
                if (ls.LayerSetName?.ToString() is { Length: > 0 } lsn) yield return lsn;
                foreach (var la in ls.MaterialLayers) if (la.Material is { } lm) foreach (var n in MaterialNamen(lm)) yield return n;
                break;
            case IIfcMaterialProfileSetUsage pu:
                foreach (var n in MaterialNamen(pu.ForProfileSet)) yield return n;
                break;
            case IIfcMaterialProfileSet ps:
                foreach (var pr in ps.MaterialProfiles) if (pr.Material is { } pm) foreach (var n in MaterialNamen(pm)) yield return n;
                break;
            case IIfcMaterialConstituentSet cs:
                foreach (var co in cs.MaterialConstituents) if (co.Material is { } cm) foreach (var n in MaterialNamen(cm)) yield return n;
                break;
        }
    }

    // ------------------------------------------------------------------ partOf

    void ElternAufbauen()
    {
        if (eltern is not null) return;
        eltern = [];
        void Dazu(IPersistEntity kind, string beziehung, IPersistEntity e)
        {
            if (!eltern.TryGetValue(kind.EntityLabel, out var l)) eltern[kind.EntityLabel] = l = [];
            l.Add((beziehung, e));
        }
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAggregates>()) foreach (var k in r.RelatedObjects) Dazu(k, "IFCRELAGGREGATES", r.RelatingObject);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAssignsToGroup>()) foreach (var k in r.RelatedObjects) Dazu(k, "IFCRELASSIGNSTOGROUP", r.RelatingGroup);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelContainedInSpatialStructure>()) foreach (var k in r.RelatedElements) Dazu(k, "IFCRELCONTAINEDINSPATIALSTRUCTURE", r.RelatingStructure);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelNests>()) foreach (var k in r.RelatedObjects) Dazu(k, "IFCRELNESTS", r.RelatingObject);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelVoidsElement>()) Dazu(r.RelatedOpeningElement, "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT", r.RelatingBuildingElement);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelFillsElement>()) Dazu(r.RelatedBuildingElement, "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT", r.RelatingOpeningElement);
    }

    /// <summary>Alle Vorfahren über die Beziehungen (alle bzw. nur die genannte), rekursiv.</summary>
    IEnumerable<IPersistEntity> Vorfahren(IPersistEntity x, string? beziehung)
    {
        ElternAufbauen();
        var gesehen = new HashSet<int> { x.EntityLabel };
        var offen = new Queue<IPersistEntity>();
        offen.Enqueue(x);
        while (offen.Count > 0)
        {
            var a = offen.Dequeue();
            if (!eltern!.TryGetValue(a.EntityLabel, out var l)) continue;
            foreach (var (b, e) in l)
            {
                if (beziehung is not null && !b.Equals(beziehung, StringComparison.OrdinalIgnoreCase)) continue;
                if (!gesehen.Add(e.EntityLabel)) continue;
                yield return e;
                offen.Enqueue(e);
            }
        }
    }
}
