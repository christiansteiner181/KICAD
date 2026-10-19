// openBIM-Abgleich (Modul 1b): Abbildung des Katalogs auf IFC4 und IFC2x3 aus kataloge/ifc.json – eine Quelle für
// Import und Export. Entitäten und Aufzählungswerte sind gegen die xBIM-Schemaklassen geprüft (IfcAbbildungTests).
// Import: aus Entität, Typobjekt, PredefinedType und ObjectType die Katalogklasse bestimmen (KlasseFuer).

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ein IFC-Ziel: Entität des Exemplars, Typobjekt, PredefinedType, ObjectType (bei USERDEFINED).</summary>
public sealed class IfcZiel
{
    public string Entitaet { get; set; } = "";
    public string? Typ { get; set; }
    public string? PredefinedType { get; set; }
    public string? ObjectType { get; set; }
}

public sealed class IfcVariante
{
    public IfcZiel? Ifc4 { get; set; }
    public IfcZiel? Ifc2x3 { get; set; }
    public IfcZiel? Fuer(string schema) => IstIfc2x3(schema) ? Ifc2x3 : Ifc4;
    public static bool IstIfc2x3(string schema) => schema.Replace(" ", "").StartsWith("IFC2X", StringComparison.OrdinalIgnoreCase);
}

public sealed class IfcWertJe
{
    public string Eigenschaft { get; set; } = "";
    public Dictionary<string, string> Werte { get; set; } = [];
}

public sealed class IfcVarianteJe
{
    public string Eigenschaft { get; set; } = "";
    public Dictionary<string, IfcVariante> Werte { get; set; } = [];
}

/// <summary>Abbildung einer Katalogklasse.</summary>
public sealed class IfcKlasse
{
    public string Klasse { get; set; } = "";
    public IfcZiel? Ifc4 { get; set; }
    public IfcZiel? Ifc2x3 { get; set; }
    /// <summary>Weitere Entitäten, die beim Import zu dieser Klasse werden (z. B. IfcWallStandardCase, IfcRoof).</summary>
    public List<string>? ImportAuch { get; set; }
    /// <summary>PredefinedType-Werte, die beim Import zu dieser Klasse führen (sonst nur der eigene).</summary>
    public List<string>? ImportWerte { get; set; }
    /// <summary>Eigenschaft → Lesekandidaten «Pset.Name» (Import); nicht am Schema geprüft.</summary>
    public Dictionary<string, List<string>>? Psets { get; set; }
    /// <summary>Abweichende Abbildung in der Lüftung (Formteile von Kanälen).</summary>
    public IfcVariante? Lueftung { get; set; }
    public IfcWertJe? PredefinedTypeJe { get; set; }
    public IfcVarianteJe? VarianteJe { get; set; }
    /// <summary>PredefinedType kommt aus dieser Quelle (z. B. «systemtyp» bei System und Strang).</summary>
    public string? PredefinedTypeAus { get; set; }
    public string? OhneExport { get; set; }

    public IfcZiel? Fuer(string schema) => IfcVariante.IstIfc2x3(schema) ? Ifc2x3 : Ifc4;

    /// <summary>Alle Ziele dieser Klasse mit Gewerk-Hinweis (lueftung) bzw. Variante (Wert der Eigenschaft).</summary>
    public IEnumerable<(IfcZiel Ziel, string? Gewerk, string? Variante)> Ziele(string schema)
    {
        if (Fuer(schema) is { } z) yield return (z, null, null);
        if (Lueftung?.Fuer(schema) is { } l) yield return (l, "lueftung", null);
        if (VarianteJe is not null)
            foreach (var (wert, v) in VarianteJe.Werte)
                if (v.Fuer(schema) is { } vz) yield return (vz, null, wert);
    }
}

public sealed class IfcSystemtyp
{
    public string Systemtyp { get; set; } = "";
    public string Ifc4 { get; set; } = "";
    public string? ObjectType { get; set; }
    /// <summary>Reguläre Ausdrücke (ohne Gross-/Kleinschreibung) für Systemnamen aus IFC.</summary>
    public List<string> Namen { get; set; } = [];
}

/// <summary>Ergebnis der Klassenbestimmung beim Import.</summary>
public sealed record IfcTreffer(string Klasse, string? Gewerk, string? Variante, int Guete, string Grund);

public sealed class IfcAbbildung
{
    public string Beschreibung { get; set; } = "";
    public string Geprueft { get; set; } = "";
    public List<string> Annahmen { get; set; } = [];
    public List<IfcKlasse> Klassen { get; set; } = [];
    public List<string> OhneIfc { get; set; } = [];
    public List<IfcSystemtyp> Systemtypen { get; set; } = [];
    public Dictionary<string, string> Fluss { get; set; } = [];

    public static IfcAbbildung Laden(string datei) =>
        JsonSerializer.Deserialize<IfcAbbildung>(File.ReadAllText(datei, System.Text.Encoding.UTF8), Json.Optionen)
        ?? throw new HlksFehler($"IFC-Abbildung {datei} ist leer oder ungültig.");

    public IfcKlasse? Klasse(string klasse) => Klassen.FirstOrDefault(k => k.Klasse.Equals(klasse, StringComparison.OrdinalIgnoreCase));

    /// <summary>Katalogklasse zu einem IFC-Exemplar. entitaet = Exemplar (z. B. IfcFlowSegment), typEntitaet = Entität des
    /// Typobjekts (z. B. IfcPipeSegmentType) oder null, predefinedType vom Exemplar, sonst vom Typ; objectType bei USERDEFINED.
    /// Güte: 3 = ObjectType passt, 2 = PredefinedType (oder Entität aus importAuch) passt, 1 = nur Entität/Typ.
    /// null = keine Klasse (→ Grundlage) oder mehrdeutig: kommen mehrere Klassen auf dieselbe beste Güte (z. B. Revit-Formteil
    /// ohne PredefinedType: Bogen, T-Stück, Reduktion …), entscheidet der Übertrager selbst (Kandidaten, Ports, Name).
    /// Befund B4-6 vom 17.10.2026: vorher gewann die erste Klasse der Liste.</summary>
    public IfcTreffer? KlasseFuer(string schema, string entitaet, string? typEntitaet, string? predefinedType, string? objectType)
    {
        var k = Kandidaten(schema, entitaet, typEntitaet, predefinedType, objectType);
        if (k.Count == 0) return null;
        return k.Count(x => x.Guete == k[0].Guete && x.Klasse != k[0].Klasse) > 0 ? null : k[0];
    }

    /// <summary>Alle passenden Klassen, beste Güte zuerst (je Klasse der beste Treffer).</summary>
    public List<IfcTreffer> Kandidaten(string schema, string entitaet, string? typEntitaet, string? predefinedType, string? objectType)
    {
        var alle = new List<IfcTreffer>();
        var pt = predefinedType?.ToUpperInvariant();
        bool echterWert = pt is not null and not ("NOTDEFINED" or "USERDEFINED");
        foreach (var k in Klassen)
            foreach (var (z, gewerk, variante) in k.Ziele(schema))
            {
                bool eigene = z.Entitaet.Equals(entitaet, StringComparison.OrdinalIgnoreCase);
                bool ueberImportAuch = !eigene && (k.ImportAuch?.Any(a => a.Equals(entitaet, StringComparison.OrdinalIgnoreCase)) ?? false);
                if (!eigene && !ueberImportAuch) continue;
                int guete; string grund;
                if (ueberImportAuch)
                {
                    // Eigene Entität (IfcRoof → Dach, IfcWall in IFC2x3 → Wand): Typobjekt und Typwert gehören zu ihr, nicht prüfen
                    guete = 2; grund = $"Entität {entitaet} (importAuch)";
                }
                else
                {
                    // Bei allgemeinen Entitäten (IFC2x3: IfcFlowSegment …) entscheidet das Typobjekt; sonst die Entität selbst
                    if (IstAllgemein(z.Entitaet) && z.Typ is not null
                        && (typEntitaet is null || !z.Typ.Equals(typEntitaet, StringComparison.OrdinalIgnoreCase))) continue;
                    guete = 1; grund = $"Entität {entitaet}";
                    if (k.ImportWerte is { Count: > 0 } iw)
                    {
                        // Abschliessende Liste für echte Typwerte. Ohne Typwert (fehlt, NOTDEFINED, USERDEFINED) gilt die Klasse nur
                        // dann mit Güte 1, wenn die Liste NOTDEFINED bzw. den Wert ausdrücklich nennt (Decke, Rohr, Ventil …); sonst
                        // Güte 0 wie ohne Liste – ein Sanitärapparat ohne Typwert bleibt so mehrdeutig (WC, Waschtisch …),
                        // statt dass WC verschwindet oder gewinnt (Befund aus B2 vom 17.10.2026).
                        if (echterWert)
                        {
                            if (!iw.Contains(pt!) && z.PredefinedType != pt && !(k.PredefinedTypeJe?.Werte.ContainsValue(pt!) ?? false)) continue;
                            guete = 2; grund += $", PredefinedType {pt}";
                        }
                        else if (!iw.Contains("NOTDEFINED") && !(pt is not null && iw.Contains(pt)) && z.PredefinedType is not null)
                            guete = 0;
                    }
                    else if (echterWert)
                    {
                        bool ptPasst = z.PredefinedType == pt || (k.PredefinedTypeJe?.Werte.ContainsValue(pt!) ?? false);
                        if (ptPasst) { guete = 2; grund += $", PredefinedType {pt}"; }
                        else if (z.PredefinedType is not null) continue;   // anderer Typwert → andere Klasse
                    }
                    else if (z.PredefinedType is not null)
                        guete = 0;   // das Ziel verlangt einen Typwert, die Datei nennt keinen: schwächer als ein Ziel ohne Typwert
                                     // (IfcDistributionSystem ohne Wert → System, nicht Regelkreis CONTROL)
                }
                if (z.ObjectType is not null)
                {
                    if (objectType is not null && objectType.Contains(z.ObjectType, StringComparison.OrdinalIgnoreCase)) { guete = 3; grund += $", ObjectType {z.ObjectType}"; }
                    else if (pt == "USERDEFINED") continue;
                    else guete = Math.Min(guete, 1);
                }
                alle.Add(new IfcTreffer(k.Klasse, gewerk, variante, guete, grund));
            }
        return alle.GroupBy(t => t.Klasse).Select(g => g.OrderByDescending(t => t.Guete).First())
            .OrderByDescending(t => t.Guete).ToList();
    }

    static bool IstAllgemein(string entitaet) => entitaet is "IfcFlowSegment" or "IfcFlowFitting" or "IfcFlowController" or "IfcFlowTerminal"
        or "IfcFlowMovingDevice" or "IfcFlowStorageDevice" or "IfcFlowTreatmentDevice" or "IfcEnergyConversionDevice" or "IfcDistributionControlElement";

    /// <summary>Systemtyp aus IFC: zuerst über ObjectType und Name (Namensmuster), dann über den PredefinedType (IFC4),
    /// falls er eindeutig ist. null = unbekannt.</summary>
    public string? SystemtypFuer(string? predefinedType, string? name, string? objectType)
    {
        var text = $"{name} {objectType}".Trim();
        if (text.Length > 0)
            foreach (var s in Systemtypen)
                foreach (var muster in s.Namen)
                    if (Regex.IsMatch(text, muster, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return s.Systemtyp;
        if (predefinedType is not null)
        {
            var kandidaten = Systemtypen.Where(s => s.Ifc4.Equals(predefinedType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (kandidaten.Count == 1) return kandidaten[0].Systemtyp;
        }
        return null;
    }

    /// <summary>KICAD-Fluss (rein/raus/beide) aus IfcFlowDirectionEnum.</summary>
    public string FlussAus(string? flowDirection) =>
        Fluss.FirstOrDefault(f => f.Value.Equals(flowDirection, StringComparison.OrdinalIgnoreCase)).Key ?? "beide";
}
