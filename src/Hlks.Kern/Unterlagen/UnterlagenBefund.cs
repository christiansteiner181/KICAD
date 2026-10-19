// Herstellerunterlagen lesen (Modul 2, entwurf/modul-2.md, Abschnitt «Unterlagen lesen → Befunde»): Jede Datei eines
// Unterlagensatzes (PDF, DWG/DXF, STEP/IGES, STL/OBJ, IFC-Typ, XLSX/CSV/Typkatalog, Bild) wird in Befunde übersetzt –
// kleine, überprüfbare Aussagen mit genauer Stelle. Aus ihnen baut die KI (oder ein Mensch) eine Familie; jeder Wert der
// Familie nennt seine Befunde, und der Kern prüft Wert gegen Befund (Modul 2, Teil B4).
//
// Name UnterlagenBefund, weil «Befund» der Prüfbefund (Pruefungen.cs) ist.
// Schnittstelle (Hauptlauf, Phase A): Leser implementieren IUnterlagenLeser und werden über Reflexion gefunden – so können
// mehrere Bauaufträge Leser beisteuern, ohne eine gemeinsame Liste zu ändern.

using System.Reflection;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Aussage aus einer Unterlage, mit Stelle und Sicherheit.</summary>
public sealed class UnterlagenBefund
{
    /// <summary>«&lt;Prüfsumme der Datei, 12 Zeichen&gt;#&lt;laufende Nummer ab 1&gt;» – stabil, solange Datei und Leserversion gleich sind.</summary>
    public string Id { get; set; } = "";
    /// <summary>Dateiname im Ordner unterlagen des Projekts.</summary>
    public string Datei { get; set; } = "";
    /// <summary>seite | text | wort | zeile | tabelle | zelle | linie | polylinie | bogen | kreis | mass | block | bild | form3d |
    /// flaeche | stutzen | ansicht | port | pset | typ</summary>
    public string Art { get; set; } = "";
    /// <summary>Inhalt: Zahl, Text oder Objekt (z. B. Tabelle mit Zeilen, Netz mit Ecken und Dreiecken, Stutzen mit Mitte,
    /// Normale, Durchmesser).</summary>
    public JsonNode? Wert { get; set; }
    /// <summary>Einheit des Werts, wenn die Unterlage sie nennt bzw. die Datei sie festlegt (mm, m, W, kPa …).</summary>
    public string? Einheit { get; set; }
    /// <summary>PDF/Bild: Seite ab 1.</summary>
    public int? Seite { get; set; }
    /// <summary>DWG/DXF: Layout («Model» für den Modellbereich).</summary>
    public string? Layout { get; set; }
    public string? Layer { get; set; }
    /// <summary>Lage in Unterlagen-Koordinaten: PDF in pt (y nach oben, Ursprung unten links der Seite), DWG/DXF in
    /// Zeichnungseinheiten, Bild in Pixeln (y nach unten), STEP/STL/IFC in mm des Modells. Punkte [x, y] bzw. [x, y, z].</summary>
    public List<double[]>? Lage { get; set; }
    /// <summary>Umgebendes Rechteck [minx, miny, maxx, maxy] in denselben Koordinaten.</summary>
    public double[]? Box { get; set; }
    /// <summary>1 = exakt gelesen (DXF-Bemassung, Tabellenzelle); kleiner = gedeutet (Zahl neben einer Masslinie).</summary>
    public double Sicherheit { get; set; } = 1;
    /// <summary>Name und Version des Lesers, z. B. «pdf 1».</summary>
    public string Leser { get; set; } = "";
    /// <summary>Zusätzliche Angaben des Lesers (z. B. Zeile/Spalte einer Zelle, Bezug einer Masslinie).</summary>
    public JsonObject? Zusatz { get; set; }
}

/// <summary>Ergebnis eines Lesers für eine Datei.</summary>
public sealed class UnterlagenBefunde
{
    public string Datei { get; set; } = "";
    public string Pruefsumme { get; set; } = "";
    /// <summary>Art der Unterlage (pdf, dwg, dxf, step, iges, stl, obj, ifc, xlsx, csv, typkatalog, bild).</summary>
    public string Art { get; set; } = "";
    public string Leser { get; set; } = "";
    /// <summary>Seiten (PDF), Layouts (DWG) bzw. 1.</summary>
    public int Seiten { get; set; } = 1;
    public List<UnterlagenBefund> Befunde { get; set; } = [];
    /// <summary>Was der Leser nicht verstanden hat oder auslassen musste – nie still.</summary>
    public List<string> Hinweise { get; set; } = [];
}

/// <summary>Ein Leser für eine oder mehrere Dateiarten. Implementierungen brauchen einen parameterlosen Konstruktor;
/// <see cref="UnterlagenLeser"/> findet sie über Reflexion.</summary>
public interface IUnterlagenLeser
{
    /// <summary>Kurzname, z. B. «pdf», «dxf», «step».</summary>
    string Name { get; }
    /// <summary>Version des Lesers; eine neue Version verwirft zwischengespeicherte Befunde.</summary>
    int Version { get; }
    /// <summary>Dateiarten (Unterlage.art), die dieser Leser liest.</summary>
    IReadOnlyCollection<string> Arten { get; }
    /// <summary>Liest die Datei. Ids der Befunde: «&lt;pruefsumme[..12]&gt;#&lt;n&gt;», n ab 1 in fester Reihenfolge.</summary>
    UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art);
}

/// <summary>Findet die Leser (Reflexion über die geladenen Hlks-Assemblies).</summary>
public static class UnterlagenLeser
{
    static readonly Lazy<IReadOnlyList<IUnterlagenLeser>> alle = new(() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Hlks", StringComparison.Ordinal) == true)
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.OfType<Type>(); } })
            .Where(t => typeof(IUnterlagenLeser).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false } && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IUnterlagenLeser)Activator.CreateInstance(t)!)
            .OrderBy(l => l.Name, StringComparer.Ordinal)
            .ToList());

    public static IReadOnlyList<IUnterlagenLeser> Alle => alle.Value;

    /// <summary>Leser für eine Dateiart oder null (dann: Meldung, Datei bleibt als Referenz).</summary>
    public static IUnterlagenLeser? Fuer(string art) =>
        Alle.FirstOrDefault(l => l.Arten.Contains(art, StringComparer.OrdinalIgnoreCase));
}
