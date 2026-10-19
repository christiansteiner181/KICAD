using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der BCF-Tests (Modul 3b, B6): Projekt mit Kollisionen (fiktiv, über element_anlegen – die Prüfung selbst baut
/// B2), Zip lesen/schreiben, unabhängige Prüfung jeder XML-Datei gegen die offiziellen Schemas in schema/bcf-3.0
/// (System.Xml.Schema, include von shared-types.xsd über den Dateipfad; Warnungen zählen als Fehler).</summary>
static class BcfHilfe
{
    /// <summary>Temporärer Ordner, beim Dispose gelöscht.</summary>
    public sealed class Temp : IDisposable
    {
        public string Pfad { get; } = Path.Combine(Path.GetTempPath(), "hlks_bcf_" + Guid.NewGuid().ToString("N")[..10]);
        public Temp() => Directory.CreateDirectory(Pfad);
        public string Datei(string name) => Path.Combine(Pfad, name);
        public void Dispose() { try { Directory.Delete(Pfad, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>Projekt mit Geschoss EG, vier Wänden W-1…W-4 (Rechteck 6 × 4 m), System KW und Rohr R-1 (DN 20, z 2500) quer durch W-1;
    /// Kollisionen KOL-1 (hart, R-1 ↔ W-1) und KOL-2 (weich, R-1 ↔ IFC-Produkt einer Unterlage). Alle Werte FIKTIV.</summary>
    public static Projekt MitKollisionen()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "R-1", ["system"] = "KW", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["dn"] = 20 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([2500, -1000, 2500], [2500, 1000, 2500]) },
        }));
        var r1 = Hole(p, "R-1").Id; var w1 = Hole(p, "W-1").Id;
        Kollision(p, "KOL-1", "hart", "neu", $"{r1}|{w1}|25:0:25", new JsonArray(r1, w1), 35, [2500, 0, 2500], pruefung: "2026-10-19 Lauf 1 (sanitaer-architektur)");
        Kollision(p, "KOL-2", "weich", "bestaetigt", $"{r1}|UL-X:3vB2YO$MX4xv5uCqZZG05x|25:10:25", new JsonArray(r1, "UL-X:3vB2YO$MX4xv5uCqZZG05x"), 20, [2500, 1000, 2500]);
        return p;
    }

    public static BefehlsErgebnis Kollision(Projekt p, string kz, string art, string? status, string? kennung, JsonArray beteiligte, double? mass, double[]? lage,
        string? kommentar = null, string? pruefung = null)
    {
        var eig = new JsonObject { ["art"] = art, ["beteiligte"] = beteiligte };
        if (status is not null) eig["status"] = status;
        if (kennung is not null) eig["kennung"] = kennung;
        if (mass is double m) eig["mass"] = m;
        if (kommentar is not null) eig["kommentar"] = kommentar;
        if (pruefung is not null) eig["pruefung"] = pruefung;
        var par = new JsonObject { ["klasse"] = "Kollision", ["kennzeichen"] = kz, ["eigenschaften"] = eig };
        if (lage is not null) par["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(lage) };
        return Ok(p.Ausfuehren("element_anlegen", par));
    }

    public static JsonObject Export(Projekt p, string datei, JsonObject? weitere = null)
    {
        var par = weitere?.DeepClone().AsObject() ?? new JsonObject();
        par["datei"] = datei;
        return (JsonObject)Tuer.Abfrage(p, "bcf_export", par);
    }

    public static JsonObject Lesen(Projekt p, string datei) => (JsonObject)Tuer.Abfrage(p, "bcf_lesen", new JsonObject { ["datei"] = datei });

    public static BefehlsErgebnis Import(Projekt p, string datei, bool trocken = false) =>
        p.Ausfuehren("bcf_import", new JsonObject { ["datei"] = datei }, trocken);

    // ------------------------------------------------------------------ Zip

    /// <summary>Alle Einträge als UTF-8-Text (Name → Inhalt), Reihenfolge des Archivs.</summary>
    public static Dictionary<string, string> ZipLesen(string pfad)
    {
        using var z = ZipFile.OpenRead(pfad);
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in z.Entries)
        {
            using var s = e.Open();
            using var r = new StreamReader(s, new UTF8Encoding(false));
            d[e.FullName] = r.ReadToEnd();
        }
        return d;
    }

    public static void ZipSchreiben(string pfad, IEnumerable<KeyValuePair<string, string>> eintraege)
    {
        if (File.Exists(pfad)) File.Delete(pfad);
        using var fs = File.Create(pfad);
        using var z = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (name, text) in eintraege)
        {
            var e = z.CreateEntry(name);
            using var s = e.Open();
            var b = new UTF8Encoding(false).GetBytes(text);
            s.Write(b, 0, b.Length);
        }
    }

    /// <summary>Kopie des Zips mit geändertem Topic (markup.bcf des Topics mit dieser Guid).</summary>
    public static void TopicAendern(string quelle, string ziel, string topicGuid, Action<XElement> aenderung)
    {
        var d = ZipLesen(quelle);
        var name = topicGuid + "/markup.bcf";
        Assert.True(d.ContainsKey(name), $"{name} fehlt im Zip");
        var doc = XDocument.Parse(d[name]);
        aenderung(doc.Root!.Element("Topic")!);
        d[name] = doc.Declaration + "\n" + doc.Root.ToString();
        ZipSchreiben(ziel, d);
    }

    /// <summary>Fügt einem Topic einen Kommentar hinzu (Reihenfolge nach Schema: Comments vor Viewpoints).</summary>
    public static void KommentarDazu(XElement topic, Guid guid, string autor, DateTimeOffset datum, string text)
    {
        var c = new XElement("Comment", new XAttribute("Guid", guid.ToString("D")),
            new XElement("Date", datum.ToString("yyyy-MM-dd'T'HH:mm:ssK", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement("Author", autor), new XElement("Comment", text));
        var comments = topic.Element("Comments");
        if (comments is null)
        {
            comments = new XElement("Comments");
            if (topic.Element("Viewpoints") is { } vps) vps.AddBeforeSelf(comments); else topic.Add(comments);
        }
        comments.Add(c);
    }

    // ------------------------------------------------------------------ Schema (unabhängig vom Kern)

    public static string SchemaOrdner()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            foreach (var o in new[] { Path.Combine(d.FullName, "schema", "bcf-3.0"), Path.Combine(d.FullName, "HLKS_CAD", "schema", "bcf-3.0") })
                if (File.Exists(Path.Combine(o, "markup.xsd"))) return o;
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "schema", "bcf-3.0");
        return File.Exists(Path.Combine(bekannt, "markup.xsd")) ? bekannt : throw new DirectoryNotFoundException("schema/bcf-3.0 nicht gefunden");
    }

    static readonly Dictionary<string, XmlSchemaSet> saetze = [];

    static XmlSchemaSet Satz(string xsd)
    {
        lock (saetze)
        {
            if (saetze.TryGetValue(xsd, out var s)) return s;
            s = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            s.Add(null, Path.Combine(SchemaOrdner(), xsd));
            s.Compile();
            return saetze[xsd] = s;
        }
    }

    /// <summary>Schema-Datei nach dem Namen des Zip-Eintrags (BCF 3.0).</summary>
    public static string XsdFuer(string eintrag) => eintrag switch
    {
        "bcf.version" => "version.xsd",
        "extensions.xml" => "extensions.xsd",
        "project.bcfp" => "project.xsd",
        "documents.xml" => "documents.xsd",
        _ when eintrag.EndsWith("/markup.bcf", StringComparison.Ordinal) => "markup.xsd",
        _ when eintrag.EndsWith(".bcfv", StringComparison.Ordinal) => "visinfo.xsd",
        _ => throw new Xunit.Sdk.XunitException("Unerwarteter Eintrag im BCF-Zip: " + eintrag),
    };

    /// <summary>Prüft ein XML gegen ein Schema; Fehler und Warnungen als Liste (leer = gültig).</summary>
    public static List<string> XsdPruefen(string xml, string xsd)
    {
        var fehler = new List<string>();
        var einst = new XmlReaderSettings { ValidationType = ValidationType.Schema, Schemas = Satz(xsd), DtdProcessing = DtdProcessing.Prohibit };
        einst.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        einst.ValidationEventHandler += (_, e) => { lock (fehler) fehler.Add($"{e.Severity}: {e.Message} (Zeile {e.Exception?.LineNumber})"); };
        using var r = XmlReader.Create(new StringReader(xml), einst);
        while (r.Read()) { }
        return fehler;
    }

    /// <summary>Jede Datei des Zips gegen ihr Schema; gibt die geprüften Einträge zurück.</summary>
    public static List<string> ZipGegenSchemaPruefen(string pfad)
    {
        var d = ZipLesen(pfad);
        foreach (var (name, xml) in d)
        {
            var f = XsdPruefen(xml, XsdFuer(name));
            Assert.True(f.Count == 0, $"{name} weicht von {XsdFuer(name)} ab:\n{string.Join("\n", f)}\n---\n{xml}");
        }
        return d.Keys.ToList();
    }
}
