// BCF 3.0 (Modul 3b, B6): Prüfung gegen die offiziellen XML-Schemas in schema/bcf-3.0 (unverändert, CC BY-ND). Der Export prüft
// jede Datei vor dem Schreiben (weicht sie ab, wird nichts geschrieben); der Import meldet Abweichungen fremder Dateien als Hinweis
// und liest tolerant weiter. Fehlt der Schemaordner (z. B. Programm ohne Quellbaum), wird die Prüfung mit Hinweis übersprungen.
// Gesucht wird neben dem Katalogordner (…/kataloge → …/schema/bcf-3.0), sonst aufwärts vom Programm- bzw. Arbeitsordner.

using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Hlks.Kern;

public static class BcfSchema
{
    public static readonly string[] Dateien = ["markup.xsd", "visinfo.xsd", "extensions.xsd", "version.xsd", "project.xsd", "documents.xsd", "shared-types.xsd"];

    static readonly ConcurrentDictionary<string, XmlSchemaSet> saetze = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Schema-Datei zum Wurzelelement einer BCF-Datei; null = unbekannte Wurzel.</summary>
    public static string? XsdZu(string wurzel) => wurzel switch
    {
        "Markup" => "markup.xsd", "VisualizationInfo" => "visinfo.xsd", "Extensions" => "extensions.xsd", "Version" => "version.xsd",
        "ProjectInfo" => "project.xsd", "DocumentInfo" => "documents.xsd", _ => null,
    };

    /// <summary>Ordner mit allen sieben Schemas oder null.</summary>
    public static string? Ordner(Katalog? katalog)
    {
        var kandidaten = new List<string>();
        if (katalog is not null && !string.IsNullOrEmpty(katalog.Ordner))
        {
            try
            {
                var oben = Path.GetDirectoryName(Path.GetFullPath(katalog.Ordner).TrimEnd('\\', '/'));
                if (oben is not null) kandidaten.Add(Path.Combine(oben, "schema", "bcf-3.0"));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                kandidaten.Add(Path.Combine(d.FullName, "schema", "bcf-3.0"));
                kandidaten.Add(Path.Combine(d.FullName, "HLKS_CAD", "schema", "bcf-3.0"));
            }
        return kandidaten.FirstOrDefault(o => Dateien.All(f => File.Exists(Path.Combine(o, f))));
    }

    static XmlSchemaSet Satz(string ordner, string xsd) => saetze.GetOrAdd(Path.Combine(ordner, xsd), pfad =>
    {
        // Nur lokale, unveränderte Schemas des Projekts; include von shared-types.xsd über den Dateipfad
        var s = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        s.Add(null, new Uri(pfad).AbsoluteUri);
        s.Compile();
        return s;
    });

    /// <summary>Prüft ein Dokument gegen sein Schema (nach dem Wurzelelement). Leere Liste = gültig. Warnungen (z. B. Element ohne
    /// Schema-Deklaration) zählen als Abweichung.</summary>
    public static List<string> Pruefen(XDocument doc, string ordner)
    {
        var abw = new List<string>();
        var xsd = doc.Root is null ? null : XsdZu(doc.Root.Name.LocalName);
        if (xsd is null) { abw.Add($"unbekanntes Wurzelelement «{doc.Root?.Name.LocalName}»"); return abw; }
        if (doc.Root!.Name.NamespaceName.Length > 0) abw.Add($"Wurzelelement im Namensraum «{doc.Root.Name.NamespaceName}» (BCF 3.0 hat keinen)");
        lock (saetze)   // XmlSchemaSet ist beim Prüfen nicht für gleichzeitige Nutzung gedacht
        {
            doc.Validate(Satz(ordner, xsd), (_, e) =>
            {
                var zeile = e.Exception?.LineNumber is int z and > 0 ? $" (Zeile {z})" : "";
                abw.Add((e.Severity == XmlSeverityType.Warning ? "Warnung: " : "") + e.Message + zeile);
            });
        }
        return abw;
    }
}
