// BCF 3.0 lesen (Modul 3b, B6) – Grundlage für bcf_import und bcf_lesen. Liest nur die XML-Dateien des Zips (bcf.version,
// extensions.xml, project.bcfp, documents.xml, je Topic markup.bcf und die referenzierten *.bcfv); Snapshots, Dokumente und
// Bitmaps werden nicht entpackt. Tolerant gegenüber Abweichungen fremder Programme (Hinweise), streng bei kaputten Dateien.
//
// Schutz wie bei unterlage_aus_url (UnterlagenDownload): Grenzen für Archivgrösse, Anzahl Einträge, Grösse je gelesenem Eintrag und
// gesamt gelesene Menge (Abbruch beim Überschreiten, nicht nach der Angabe im Archiv); Einträge mit Pfaden ausserhalb des Archivs
// (.., absolut, Laufwerk) werden übergangen; verschlüsselte Einträge übergangen. XML ohne DTD und ohne externe Verweise (XXE).
// ANNAHME Grenzen: 1 GB Archiv, 50 000 Einträge (BCF hat je Topic mehrere Dateien – darum mehr als die 200 von unterlage_aus_url),
// 20 MB je XML-Datei, 512 MB gelesen insgesamt. Für Tests als Parameter einstellbar (BcfGrenzen); von aussen nicht zu lockern.
// Gelesen wird nur BCF 3.0 (bcf.version VersionId «3.0»); ältere Fassungen (2.1) brauchten deren Schemas – nicht vorhanden.

using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Hlks.Kern;

public sealed record BcfGrenzen(long MaxDateiBytes, int MaxEintraege, long MaxEintragBytes, long MaxGelesenBytes)
{
    public static readonly BcfGrenzen Standard = new(1L << 30, 50_000, 20L << 20, 512L << 20);
}

/// <summary>Kamera eines Viewpoints, Koordinaten wie in der Datei (m).</summary>
public sealed record BcfKamera(string Art, double[]? Standpunkt, double[]? Richtung, double[]? Oben, double? Sichtfeld, double? Hoehe, double? Seitenverhaeltnis);

/// <summary>Komponente einer Auswahl (Components/Selection/Component).</summary>
public sealed record BcfKomponente(string? IfcGuid, string? Ursprung, string? AuthoringToolId);

public sealed class BcfViewpoint
{
    public Guid Guid { get; init; }
    public string? Datei { get; init; }
    public string? Snapshot { get; init; }
    public int? Index { get; init; }
    public BcfKamera? Kamera { get; set; }
    public List<BcfKomponente> Auswahl { get; } = [];
    public bool? StandardSichtbar { get; set; }
}

public sealed class BcfTopic
{
    public Guid Guid { get; init; }
    public string Ordner { get; init; } = "";
    public string? Typ { get; init; }
    public string? Status { get; init; }
    public string? Titel { get; init; }
    public string? Prioritaet { get; init; }
    public string? Beschreibung { get; init; }
    public DateTimeOffset? Erstellt { get; init; }
    public string? Autor { get; init; }
    public DateTimeOffset? Geaendert { get; init; }
    public string? GeaendertVon { get; init; }
    public DateTimeOffset? Faellig { get; init; }
    public string? Zugewiesen { get; init; }
    public string? Phase { get; init; }
    public List<string> Labels { get; } = [];
    public List<string> Dateien { get; } = [];
    public List<BcfKommentar> Kommentare { get; } = [];
    public List<BcfViewpoint> Viewpoints { get; } = [];
}

public sealed class BcfInhalt
{
    public string Datei { get; init; } = "";
    public string Version { get; set; } = "";
    public string? ProjektId { get; set; }
    public string? ProjektName { get; set; }
    public Dictionary<string, List<string>> Erweiterungen { get; } = new(StringComparer.Ordinal);
    public List<BcfTopic> Topics { get; } = [];
    public List<string> Hinweise { get; } = [];
    /// <summary>Gegen schema/bcf-3.0 geprüft (false: Schemaordner nicht gefunden).</summary>
    public bool SchemaGeprueft { get; set; }
    public int Schemaabweichungen { get; set; }
}

public static class BcfLeser
{
    const int MaxSchemaHinweise = 20;

    public static BcfInhalt Lesen(string datei, Katalog? katalog) => Lesen(datei, katalog, BcfGrenzen.Standard);

    internal static BcfInhalt Lesen(string datei, Katalog? katalog, BcfGrenzen g)
    {
        string pfad;
        try { pfad = Path.GetFullPath(datei.Trim().Trim('"')); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Pfad «{datei}» ist ungültig: {ex.Message}", "Vollständigen Pfad der .bcf- bzw. .bcfzip-Datei angeben."); }
        if (!File.Exists(pfad))
            throw new HlksFehler($"Die BCF-Datei «{datei}» gibt es nicht.", "Pfad prüfen (vollständiger Pfad der .bcf- bzw. .bcfzip-Datei).");
        var name = Path.GetFileName(pfad);
        var laenge = new FileInfo(pfad).Length;
        if (laenge > g.MaxDateiBytes)
            throw new HlksFehler($"«{name}» ist {laenge / 1_048_576.0:0.#} MB gross (Grenze {g.MaxDateiBytes / 1_048_576} MB).", "Beim Absender ein kleineres BCF (weniger Topics, kleinere Snapshots) exportieren lassen.");

        ZipArchive zip;
        try { zip = ZipFile.OpenRead(pfad); }
        catch (InvalidDataException ex)
        { throw new HlksFehler($"«{name}» ist kein gültiges BCF-Zip ({ex.Message}).", KeinBcf); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new HlksFehler($"«{name}» kann nicht gelesen werden: {ex.Message}", "Datei schliessen (anderes Programm?) und Zugriffsrechte prüfen."); }

        using (zip)
        {
            int anzahl;
            try { anzahl = zip.Entries.Count; }
            catch (InvalidDataException ex) { throw new HlksFehler($"«{name}» ist kein gültiges BCF-Zip ({ex.Message}).", KeinBcf); }
            if (anzahl > g.MaxEintraege)
                throw new HlksFehler($"Das BCF-Zip «{name}» hat {anzahl} Einträge (Grenze {g.MaxEintraege}).", "Beim Absender in mehrere kleinere BCF-Dateien aufteilen lassen.");

            var schema = BcfSchema.Ordner(katalog);
            var inhalt = new BcfInhalt { Datei = pfad, Version = "" };
            var hinweise = inhalt.Hinweise;
            var eintraege = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in zip.Entries)
            {
                var n = e.FullName.Replace('\\', '/');
                if (n.EndsWith('/')) continue;   // Ordner
                if (!SicherPfad(n)) { hinweise.Add($"Eintrag «{e.FullName}» übergangen: Pfad ausserhalb des Archivs (.., absoluter Pfad oder Laufwerk)."); continue; }
                eintraege.TryAdd(n, e);
            }
            long gelesen = 0;
            int schemaHinweise = 0;

            XDocument? Xml(string eintrag)
            {
                if (!eintraege.TryGetValue(eintrag, out var e)) return null;
                bool verschluesselt = false;
                try { verschluesselt = e.IsEncrypted; } catch (Exception) { }
                if (verschluesselt) { hinweise.Add($"«{eintrag}» übergangen: verschlüsselt."); return null; }
                if (e.Length > g.MaxEintragBytes) { hinweise.Add($"«{eintrag}» übergangen: grösser als {g.MaxEintragBytes / 1_048_576.0:0.#} MB."); return null; }
                byte[] daten;
                try
                {
                    using var quelle = e.Open();
                    using var ms = new MemoryStream();
                    var puffer = new byte[81920];
                    long l = 0; int r;
                    while ((r = quelle.Read(puffer, 0, puffer.Length)) > 0)
                    {
                        l += r; gelesen += r;
                        if (gelesen > g.MaxGelesenBytes)
                            throw new HlksFehler($"Das BCF-Zip «{name}» wird beim Entpacken grösser als {g.MaxGelesenBytes / 1_048_576.0:0.#} MB (Zip-Bombe?): abgebrochen.",
                                "Nur BCF-Dateien aus bekannter Quelle lesen; beim Absender neu exportieren lassen.");
                        if (l > g.MaxEintragBytes) { hinweise.Add($"«{eintrag}» übergangen: entpackt grösser als {g.MaxEintragBytes / 1_048_576.0:0.#} MB (Angabe im Archiv stimmt nicht)."); return null; }
                        ms.Write(puffer, 0, r);
                    }
                    daten = ms.ToArray();
                }
                catch (InvalidDataException ex) { throw new HlksFehler($"«{eintrag}» in «{name}» ist beschädigt ({ex.Message}).", KeinBcf); }
                XDocument doc;
                try
                {
                    var einst = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true,
                        MaxCharactersInDocument = g.MaxEintragBytes,
                    };
                    using var leser = XmlReader.Create(new MemoryStream(daten), einst);
                    doc = XDocument.Load(leser, LoadOptions.SetLineInfo);
                }
                catch (XmlException ex)
                {
                    throw new HlksFehler($"«{eintrag}» in «{name}» ist kein gültiges XML: {ex.Message}",
                        "Die Datei beim Absender neu als BCF 3.0 exportieren lassen; von Hand geänderte BCF-Dateien prüfen.");
                }
                if (schema is not null)
                {
                    var abw = BcfSchema.Pruefen(doc, schema);
                    inhalt.Schemaabweichungen += abw.Count;
                    foreach (var a in abw)
                        if (schemaHinweise++ < MaxSchemaHinweise) hinweise.Add($"Schema BCF 3.0, «{eintrag}»: {a}");
                }
                return doc;
            }

            // bcf.version
            var ver = Xml("bcf.version") ?? throw new HlksFehler($"«{name}» ist keine BCF-Datei: bcf.version fehlt.", KeinBcf);
            var vid = ver.Root?.Name.LocalName == "Version" ? Attr(ver.Root, "VersionId") : null;
            if (vid is null) throw new HlksFehler($"«{name}»: bcf.version hat keine VersionId.", KeinBcf);
            if (!vid.StartsWith("3.0", StringComparison.Ordinal))
                throw new HlksFehler($"«{name}» ist BCF {vid}; gelesen wird nur BCF 3.0.", "Beim Absender als BCF 3.0 exportieren lassen.");
            inhalt.Version = vid;
            inhalt.SchemaGeprueft = schema is not null;
            if (schema is null) hinweise.Add("Schemaprüfung übersprungen: Ordner schema/bcf-3.0 nicht gefunden.");

            // extensions.xml, project.bcfp, documents.xml (nur Prüfung)
            if (Xml("extensions.xml")?.Root is { } ext && ext.Name.LocalName == "Extensions")
                foreach (var liste in ext.Elements())
                    inhalt.Erweiterungen[liste.Name.LocalName] = liste.Elements().Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToList();
            if (Xml("project.bcfp")?.Root is { } pi && Kind(pi, "Project") is { } pr)
            {
                inhalt.ProjektId = Attr(pr, "ProjectId");
                inhalt.ProjektName = Text(pr, "Name");
            }
            _ = Xml("documents.xml");

            // Topics: je Ordner eine markup.bcf
            var gesehen = new HashSet<Guid>();
            foreach (var eintrag in eintraege.Keys.Where(k => k.EndsWith("/markup.bcf", StringComparison.OrdinalIgnoreCase) && k.Count(c => c == '/') == 1)
                         .Order(StringComparer.OrdinalIgnoreCase).ToList())
            {
                var ordner = eintrag[..eintrag.IndexOf('/')];
                var doc = Xml(eintrag);
                if (doc is null) continue;
                var markup = doc.Root;
                var topicEl = markup?.Name.LocalName == "Markup" ? Kind(markup, "Topic") : null;
                if (topicEl is null) { hinweise.Add($"«{eintrag}»: kein Topic – übergangen."); continue; }
                if (!Guid.TryParse(Attr(topicEl, "Guid"), out var tg)) { hinweise.Add($"«{eintrag}»: Topic ohne gültige Guid – übergangen."); continue; }
                if (!gesehen.Add(tg)) { hinweise.Add($"Topic {BcfFormat.Text(tg)} steht zweimal im Zip («{eintrag}») – das zweite übergangen."); continue; }
                if (!string.Equals(ordner, BcfFormat.Text(tg), StringComparison.OrdinalIgnoreCase))
                    hinweise.Add($"Topic {BcfFormat.Text(tg)}: Ordner «{ordner}» heisst nicht wie die Guid (gelesen wird die Guid aus markup.bcf).");
                var t = new BcfTopic
                {
                    Guid = tg, Ordner = ordner, Typ = Attr(topicEl, "TopicType"), Status = Attr(topicEl, "TopicStatus"),
                    Titel = Text(topicEl, "Title"), Prioritaet = Text(topicEl, "Priority"), Beschreibung = Text(topicEl, "Description"),
                    Erstellt = Zeit(Text(topicEl, "CreationDate")), Autor = Text(topicEl, "CreationAuthor"),
                    Geaendert = Zeit(Text(topicEl, "ModifiedDate")), GeaendertVon = Text(topicEl, "ModifiedAuthor"),
                    Faellig = Zeit(Text(topicEl, "DueDate")), Zugewiesen = Text(topicEl, "AssignedTo"), Phase = Text(topicEl, "Stage"),
                };
                if (Kind(topicEl, "Labels") is { } lab) t.Labels.AddRange(Kinder(lab, "Label").Select(x => x.Value.Trim()).Where(x => x.Length > 0));
                if (Kind(markup!, "Header") is { } kopf && Kind(kopf, "Files") is { } files)
                    foreach (var f in Kinder(files, "File"))
                        if ((Text(f, "Filename") ?? Text(f, "Reference")) is { } fn) t.Dateien.Add(fn);
                if (Kind(topicEl, "Comments") is { } cs)
                    foreach (var c in Kinder(cs, "Comment"))
                    {
                        if (!Guid.TryParse(Attr(c, "Guid"), out var cg)) { hinweise.Add($"Topic {BcfFormat.Text(tg)}: Kommentar ohne gültige Guid – übergangen."); continue; }
                        Guid? cv = Kind(c, "Viewpoint") is { } cvp && Guid.TryParse(Attr(cvp, "Guid"), out var cvg) ? cvg : null;
                        t.Kommentare.Add(new BcfKommentar(cg, Zeit(Text(c, "Date")), Text(c, "Author"), Text(c, "Comment"), cv,
                            Zeit(Text(c, "ModifiedDate")), Text(c, "ModifiedAuthor")));
                    }
                if (Kind(topicEl, "Viewpoints") is { } vps)
                    foreach (var vp in Kinder(vps, "ViewPoint"))
                    {
                        if (!Guid.TryParse(Attr(vp, "Guid"), out var vg)) { hinweise.Add($"Topic {BcfFormat.Text(tg)}: Viewpoint ohne gültige Guid – übergangen."); continue; }
                        var v = new BcfViewpoint
                        {
                            Guid = vg, Datei = Text(vp, "Viewpoint"), Snapshot = Text(vp, "Snapshot"),
                            Index = int.TryParse(Text(vp, "Index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ix) ? ix : null,
                        };
                        t.Viewpoints.Add(v);
                        if (v.Datei is null) continue;
                        if (v.Datei.Contains('/') || v.Datei.Contains('\\') || v.Datei.Contains(':') || v.Datei.Contains(".."))
                        { hinweise.Add($"Topic {BcfFormat.Text(tg)}: Viewpoint-Datei «{v.Datei}» liegt nicht im Topic-Ordner – übergangen."); continue; }
                        var vdoc = Xml($"{ordner}/{v.Datei}");
                        if (vdoc?.Root is not { } vi || vi.Name.LocalName != "VisualizationInfo")
                        { if (vdoc is null && !eintraege.ContainsKey($"{ordner}/{v.Datei}")) hinweise.Add($"Topic {BcfFormat.Text(tg)}: Viewpoint-Datei «{v.Datei}» fehlt im Zip."); continue; }
                        Visinfo(vi, v);
                    }
                inhalt.Topics.Add(t);
            }
            if (inhalt.Schemaabweichungen > MaxSchemaHinweise)
                hinweise.Add($"… insgesamt {inhalt.Schemaabweichungen} Abweichungen vom Schema BCF 3.0 (die ersten {MaxSchemaHinweise} oben).");
            return inhalt;
        }
    }

    const string KeinBcf = "Eine BCF-3.0-Datei wählen (.bcf/.bcfzip: Zip mit bcf.version und je Topic einem Ordner mit markup.bcf) bzw. beim Absender neu als BCF 3.0 exportieren lassen.";

    static void Visinfo(XElement vi, BcfViewpoint v)
    {
        var pc = Kind(vi, "PerspectiveCamera");
        var oc = Kind(vi, "OrthogonalCamera");
        if ((pc ?? oc) is { } cam)
        {
            double[]? P(string n) => Kind(cam, n) is { } x && Zahl(Text(x, "X")) is double a && Zahl(Text(x, "Y")) is double b && Zahl(Text(x, "Z")) is double c ? [a, b, c] : null;
            v.Kamera = new BcfKamera(pc is not null ? "perspektivisch" : "orthogonal", P("CameraViewPoint"), P("CameraDirection"), P("CameraUpVector"),
                pc is not null ? Zahl(Text(cam, "FieldOfView")) : null, oc is not null ? Zahl(Text(cam, "ViewToWorldScale")) : null, Zahl(Text(cam, "AspectRatio")));
        }
        if (Kind(vi, "Components") is { } comps)
        {
            if (Kind(comps, "Selection") is { } sel)
                foreach (var c in Kinder(sel, "Component"))
                    v.Auswahl.Add(new BcfKomponente(Attr(c, "IfcGuid"), Text(c, "OriginatingSystem"), Text(c, "AuthoringToolId")));
            if (Kind(comps, "Visibility") is { } vis)
                v.StandardSichtbar = Attr(vis, "DefaultVisibility") is { } dv ? dv is "true" or "1" : false;
        }
    }

    static bool SicherPfad(string name)
    {
        if (name.Length == 0 || name.StartsWith('/') || name.Contains(':')) return false;
        return !name.Split('/').Any(t => t == "..");
    }

    // Elemente nach lokalem Namen (BCF 3.0 hat keinen Namensraum; fremde Programme schreiben manchmal einen)
    static XElement? Kind(XElement e, string name) => e.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    static IEnumerable<XElement> Kinder(XElement e, string name) => e.Elements().Where(x => x.Name.LocalName == name);
    static string? Text(XElement e, string name) => Kind(e, name)?.Value is { } s && s.Trim().Length > 0 ? s.Trim() : null;
    static string? Attr(XElement e, string name) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value is { } s && s.Trim().Length > 0 ? s.Trim() : null;

    static DateTimeOffset? Zeit(string? s)
    {
        if (s is null) return null;
        try { return XmlConvert.ToDateTimeOffset(s); }
        catch (FormatException) { }
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
    }

    static double? Zahl(string? s) =>
        s is not null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
}
