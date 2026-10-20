using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zum IFC-Export (Modul 7 Teil B2): Export in einen eigenen Temp-Ordner, Rundreise über den eigenen Import,
/// Vergleich der HLKS-Elemente über die GlobalId.</summary>
sealed class IfcExportOrdner : IDisposable
{
    public string Pfad { get; } = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"b2_ifc_{Environment.ProcessId}_{Guid.NewGuid().ToString("N")[..8]}");
    public IfcExportOrdner() => Directory.CreateDirectory(Pfad);
    public string Datei(string name) => Path.Combine(Pfad, name);
    public void Dispose() { try { Directory.Delete(Pfad, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

static class IfcExportHilfe
{
    public static JsonObject Export(Projekt p, string datei, string fassung = "IFC4", JsonObject? weitere = null)
    {
        var par = new JsonObject { ["datei"] = datei, ["fassung"] = fassung };
        foreach (var (k, v) in weitere ?? []) par[k] = v?.DeepClone();
        return (JsonObject)Tuer.Abfrage(p, "ifc_export", par);
    }

    public static string Gid(Projekt p, Element e) => IfcExportAufbau.ElementGlobalId(p.Kopf.Id, e.Id);
    public static string Gid(Projekt p, string kennzeichen) => Gid(p, p.Modell.Hole(kennzeichen));

    /// <summary>HLKS-Elemente (Klassen mit Rolle im Netz), wie sie der Import übernimmt.</summary>
    public static List<Element> HlksElemente(Projekt p) =>
        p.Modell.Elemente.Where(e => IfcHlksKlasse.IstHlks(p.Katalog, e.Klasse)).OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();

    /// <summary>Importiert die Datei in ein neues, leeres Projekt (eigener Import, Modul 1b).</summary>
    public static Projekt Importieren(string datei)
    {
        var ziel = Leer();
        Ok(Uebernehmen(ziel, datei));
        return ziel;
    }

    /// <summary>Element im importierten Projekt mit der GlobalId (Kennung ifc) oder null.</summary>
    public static Element? Importiert(Projekt ziel, string gid) => ziel.Modell.Elemente.FirstOrDefault(e => e.Kennungen?.GetValueOrDefault("ifc") == gid);

    public static int Regeln(JsonObject bericht, string ergebnis) =>
        bericht["ids"]!.AsArray().OfType<JsonObject>().SelectMany(i => i["regeln"]!.AsArray().OfType<JsonObject>()).Count(r => r["ergebnis"]!.GetValue<string>() == ergebnis);

    public static JsonObject Regel(JsonObject bericht, string kennung) =>
        bericht["ids"]!.AsArray().OfType<JsonObject>().SelectMany(i => i["regeln"]!.AsArray().OfType<JsonObject>()).First(r => r["kennung"]?.GetValue<string>() == kennung);

    public static List<string> Befunde(JsonObject regel) =>
        regel["befunde"]?.AsArray().OfType<JsonObject>().Select(b => b["kennzeichen"]!.GetValue<string>()).ToList() ?? [];
}
