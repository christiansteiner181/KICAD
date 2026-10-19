// BCF 3.0 schreiben (Modul 3b, B6): Abfrage bcf_export – ändert das Modell nicht, schreibt eine Datei (wie plan_pdf).
//
// Aufbau des Zips (Elemente, Pflichtfelder und Reihenfolgen nach schema/bcf-3.0; die Dateinamen geben die Schemas nicht vor –
// ANNAHME, wie in BCF üblich):
//   bcf.version                      Version VersionId="3.0"                                   (version.xsd)
//   extensions.xml                   TopicTypes, TopicStatuses, Priorities, TopicLabels, Users  (extensions.xsd)
//   project.bcfp                     ProjectInfo/Project ProjectId = KICAD-Projekt-Id, Name     (project.xsd)
//   <Topic-Guid>/markup.bcf          Markup: Header/Files (IFC-Unterlagen der Beteiligten), Topic (markup.xsd)
//   <Topic-Guid>/<Viewpoint-Guid>.bcfv  VisualizationInfo: Components/Selection, PerspectiveCamera (visinfo.xsd)
// documents.xml entfällt (keine Dokumente), Snapshots entfallen (in 3.0 optional; der Kern hat keine Bildfunktion für eine
// perspektivische Modellansicht – Nachtrag, sobald es eine gibt).
//
// ANNAHME Kamera: PerspectiveCamera auf die Lage der Kollision, Blick schräg von oben aus Südwest (Richtung (1, 1, −1)/√3, also
//   35.3° nach unten, Oben-Vektor (1, 1, 2)/√6 senkrecht dazu), Abstand = 10 × Mass, mindestens 3 m, höchstens 30 m; vertikales
//   Sichtfeld 60°, Seitenverhältnis 16:9.
// ANNAHME Koordinaten: in Metern – visinfo.xsd nennt Meter ausdrücklich nur bei ViewToWorldScale («view's visible vertical size in
//   meters»); für Punkte (CameraViewPoint) sagt das Schema nichts, Meter gelten hier durchgehend. koordinaten = ifc: die
//   IFC-Weltkoordinaten über den Projektbezug (Projektinformation.ifcBezug: Welt = DrehungZ(drehung) · Modell + [x, y, z], siehe
//   IfcBezug); koordinaten = projekt: das Projektkoordinatensystem von KICAD (x Ost, y Nord, z über ±0.00). Beide wählbar; ohne
//   Angabe ifc, wenn das Projekt einen Projektbezug hat, sonst projekt mit Hinweis (Entscheid Christian 19.10.2026, wie
//   aussparungen_ifc; B6 hatte projekt als Vorgabe).
// ANNAHME Komponenten: übernommene IFC-Elemente und Produkte einer IFC-Unterlage mit IfcGuid (GlobalId), in KICAD entstandene
//   Elemente mit AuthoringToolId = Element-Id und OriginatingSystem «KICAD» (exklusiv, nicht beides). Alle übrigen Elemente bleiben
//   sichtbar (Visibility DefaultVisibility="true"), die Beteiligten sind ausgewählt (Selection).

using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace Hlks.Kern;

public static class BcfExport
{
    public const double Sichtfeld = 60;                 // Grad, vertikal (ANNAHME)
    public const double Seitenverhaeltnis = 16.0 / 9;   // Breite/Höhe (ANNAHME)
    public const double AbstandMin = 3, AbstandMax = 30, AbstandJeMass = 10;   // m bzw. Faktor (ANNAHME)

    /// <summary>Blickrichtung schräg von oben aus Südwest (Einheitsvektor) und Oben-Vektor senkrecht dazu (ANNAHME).</summary>
    public static readonly double[] Blick = [1 / Math.Sqrt(3), 1 / Math.Sqrt(3), -1 / Math.Sqrt(3)];
    public static readonly double[] Oben = [1 / Math.Sqrt(6), 1 / Math.Sqrt(6), 2 / Math.Sqrt(6)];

    /// <summary>Kameraabstand in m aus dem Mass der Kollision (mm).</summary>
    public static double Abstand(double? massMm) =>
        Math.Clamp(AbstandJeMass * (massMm is double m && double.IsFinite(m) ? Math.Abs(m) : 0) / 1000, AbstandMin, AbstandMax);

    sealed record Koordinaten(string Art, IfcBezug? Bezug)
    {
        /// <summary>Punkt in mm (Projekt) → m im Zielsystem.</summary>
        public double[] Punkt(double[] mm)
        {
            if (Bezug is null) return [mm[0] / 1000, mm[1] / 1000, mm[2] / 1000];
            var r = Richtung(mm);
            return [(r[0] + Bezug.X) / 1000, (r[1] + Bezug.Y) / 1000, (r[2] + Bezug.Z) / 1000];
        }

        public double[] Richtung(double[] v)
        {
            if (Bezug is null || Bezug.Drehung == 0) return [v[0], v[1], v[2]];
            double w = Bezug.Drehung * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
            return [c * v[0] - s * v[1], s * v[0] + c * v[1], v[2]];
        }

        public string Text => Bezug is null
            ? "projekt: Projektkoordinaten KICAD in m (x Ost, y Nord, z über ±0.00)"
            : $"ifc: IFC-Weltkoordinaten in m über den Projektbezug (Weltpunkt {Bezug.X / 1000:0.###} / {Bezug.Y / 1000:0.###} / {Bezug.Z / 1000:0.###} m, Drehung {Bezug.Drehung:0.######}°)";
    }

    /// <summary>bcf_export: wählt die Kollisionen, baut alle XML-Dateien, prüft sie gegen die Schemas und schreibt das Zip.</summary>
    public static JsonObject Schreiben(Projekt p, JsonObject par)
    {
        var kultur = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try { return Schreiben2(p, par); }
        finally { System.Globalization.CultureInfo.CurrentCulture = kultur; }
    }

    static JsonObject Schreiben2(Projekt p, JsonObject par)
    {
        var datei = (Param.Text(par, "datei", true) ?? "").Trim().Trim('"');
        var endung = Path.GetExtension(datei).ToLowerInvariant();
        if (endung is not (".bcf" or ".bcfzip"))
            throw new HlksFehler($"Die Zieldatei «{datei}» braucht die Endung .bcf oder .bcfzip.", "datei z. B. «…\\Kollisionen_2026-10-19.bcf» angeben.");
        string pfad;
        try { pfad = Path.GetFullPath(datei); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Pfad «{datei}» ist ungültig: {ex.Message}", "Vollständigen Pfad mit Endung .bcf angeben."); }

        var m = p.Modell; var kat = p.Katalog;
        var auswahl = Auswahl(p, par);
        var hinweise = new List<string>();
        var koord = KoordinatenLesen(p, Param.Text(par, "koordinaten"), hinweise);
        var autor = BcfFormat.NichtLeer(Param.Text(par, "autor"))?.Trim();
        var guids = BcfFormat.TopicGuids(p.Kopf.Id, m, kat);
        var aufl = new BcfAufloesung(p, m);
        var nutzer = new SortedSet<string>(StringComparer.Ordinal);
        var labelsBenutzt = new HashSet<string>(StringComparer.Ordinal);
        var dokumente = new List<(string Name, XDocument Doc)>();
        var liste = new JsonArray();
        int hart = 0, weich = 0;

        foreach (var e in auswahl)
        {
            var tg = guids[e.Id];
            var bet = aufl.Beteiligte(e, hinweise);
            var art = e.Text("art") == "weich" ? "weich" : "hart";
            if (art == "hart") hart++; else weich++;
            var vp = Viewpoint(e, tg, bet, koord);
            if (vp is null) hinweise.Add($"{e.Kennzeichen}: keine Lage (Geometrie punkt) – Topic ohne Viewpoint.");
            var markup = Markup(p, e, tg, art, bet, autor, vp is not null, nutzer, labelsBenutzt);
            dokumente.Add(($"{BcfFormat.Text(tg)}/markup.bcf", markup));
            if (vp is not null) dokumente.Add(($"{BcfFormat.Text(tg)}/{BcfFormat.Text(BcfFormat.ViewpointGuid(tg))}.bcfv", vp));
            liste.Add(new JsonObject
            {
                ["kollision"] = e.Kennzeichen, ["topic"] = BcfFormat.Text(tg), ["art"] = art,
                ["status"] = BcfFormat.StatusBcf(e.Text("status")),
                ["titel"] = markup.Root!.Element("Topic")!.Element("Title")!.Value,
                ["komponenten"] = new JsonArray(bet.Select(b => (JsonNode)JsonValue.Create(b.IfcGuid is not null ? "IfcGuid " + b.IfcGuid : $"{b.Ursprung} {b.AuthoringToolId}")!).ToArray()),
                ["viewpoint"] = vp is not null,
            });
        }

        dokumente.Insert(0, ("bcf.version", Dokument(new XElement("Version", new XAttribute("VersionId", BcfFormat.Version)))));
        dokumente.Insert(1, ("extensions.xml", Erweiterungen(labelsBenutzt, nutzer)));
        dokumente.Insert(2, ("project.bcfp", Dokument(new XElement("ProjectInfo",
            new XElement("Project", new XAttribute("ProjectId", BcfFormat.NichtLeer(p.Kopf.Id) ?? "KICAD"),
                BcfFormat.NichtLeer(p.Kopf.Name) is { } pn ? new XElement("Name", pn) : null)))));

        // Jede Datei gegen ihr Schema prüfen – weicht eine ab, wird nichts geschrieben
        string pruefung;
        var ordner = BcfSchema.Ordner(kat);
        if (ordner is null)
        {
            pruefung = "übersprungen: Ordner schema/bcf-3.0 nicht gefunden";
            hinweise.Add("Schemaprüfung übersprungen: Ordner schema/bcf-3.0 nicht gefunden (Datei trotzdem nach den Schemas aufgebaut).");
        }
        else
        {
            var abw = dokumente.SelectMany(d => BcfSchema.Pruefen(d.Doc, ordner).Select(f => $"{d.Name}: {f}")).ToList();
            if (abw.Count > 0)
                throw new HlksFehler($"Der BCF-Export weicht vom Schema BCF 3.0 ab, es wurde nichts geschrieben: {string.Join(" | ", abw.Take(5))}{(abw.Count > 5 ? $" (… {abw.Count} Abweichungen)" : "")}",
                    "Interner Fehler im BCF-Export – bitte mit der Meldung melden; bis dahin die betroffene Kollision mit dem Filter kollisionen auslassen.");
            pruefung = $"bestanden: {dokumente.Count} XML-Dateien gegen schema/bcf-3.0 geprüft";
        }

        var bytes = ZipSchreiben(pfad, dokumente);
        return new JsonObject
        {
            ["datei"] = pfad,
            ["bcfVersion"] = BcfFormat.Version,
            ["groesseBytes"] = bytes,
            ["topics"] = auswahl.Count,
            ["hart"] = hart,
            ["weich"] = weich,
            ["koordinaten"] = koord.Text,
            ["schemapruefung"] = pruefung,
            ["kollisionen"] = liste,
            ["hinweise"] = new JsonArray(hinweise.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        };
    }

    // ------------------------------------------------------------------ Auswahl und Koordinaten

    static List<Element> Auswahl(Projekt p, JsonObject par)
    {
        var m = p.Modell; var kat = p.Katalog;
        var def = kat.Klasse("Kollision");
        var erlaubtStatus = def.Eigenschaften.FirstOrDefault(x => x.Name == "status")?.Auswahl ?? [];
        var erlaubtArt = def.Eigenschaften.FirstOrDefault(x => x.Name == "art")?.Auswahl ?? [];

        var statusFilter = Liste(par, "status")?.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
        if (statusFilter is not null)
            foreach (var s in statusFilter.Where(s => !erlaubtStatus.Contains(s)))
                throw new HlksFehler($"Status «{s}» gibt es für Kollisionen nicht.", "status: " + string.Join(", ", erlaubtStatus));
        var art = Param.Text(par, "art")?.Trim().ToLowerInvariant();
        if (art is { Length: 0 }) art = null;
        if (art is not null && !erlaubtArt.Contains(art))
            throw new HlksFehler($"Art «{art}» gibt es für Kollisionen nicht.", "art: " + string.Join(" | ", erlaubtArt));

        IEnumerable<Element> kandidaten;
        if (Liste(par, "kollisionen") is { } namen)
        {
            var gewaehlt = new List<Element>();
            foreach (var n in namen)
            {
                var e = m.Hole(n, "Kollision");
                if (!kat.IstArt(e, "Kollision"))
                    throw new HlksFehler($"{e.Kennzeichen} ist keine Kollision, sondern {e.Klasse}.", "In kollisionen nur Kollisionen angeben (elemente_suchen mit klasse Kollision listet sie).");
                if (!gewaehlt.Contains(e)) gewaehlt.Add(e);
            }
            kandidaten = gewaehlt.OrderBy(e => e.Kennzeichen, BcfFormat.Natuerlich);
        }
        else kandidaten = BcfFormat.Kollisionen(m, kat);

        var auswahl = kandidaten.Where(e => (statusFilter is null || statusFilter.Contains(e.Text("status") ?? "neu"))
                                            && (art is null || (e.Text("art") ?? "hart") == art)).ToList();
        if (auswahl.Count == 0)
            throw new HlksFehler(BcfFormat.Kollisionen(m, kat).Count == 0 ? "Das Projekt hat keine Kollisionen – nichts für den BCF-Export." : "Keine Kollision passt zum Filter – nichts für den BCF-Export.",
                "Zuerst einen Prüflauf machen (kollision_pruefen) bzw. den Filter (kollisionen, status, art) lockern; elemente_suchen mit klasse Kollision listet die vorhandenen.");
        return auswahl;
    }

    /// <summary>Liste aus einem JSON-Array oder einem Text (durch Komma getrennt); null = Parameter fehlt.</summary>
    static List<string>? Liste(JsonObject par, string name)
    {
        var n = par[name];
        if (n is null) return null;
        if (n is JsonArray a)
            return a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler($"Parameter «{name}» muss eine Liste von Texten sein."))
                .Where(s => s.Trim().Length > 0).Select(s => s.Trim()).ToList();
        if (n is JsonValue t && t.TryGetValue<string>(out var text))
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        throw new HlksFehler($"Parameter «{name}» muss eine Liste sein, z. B. [\"KOL-1\"].");
    }

    /// <summary>Koordinaten: ausdrücklich gewählt (projekt | ifc) oder – ohne Angabe – ifc, wenn das Projekt einen Projektbezug hat,
    /// sonst projekt mit Hinweis (Entscheid Christian 19.10.2026: wie aussparungen_ifc).</summary>
    static Koordinaten KoordinatenLesen(Projekt p, string? art, List<string> hinweise)
    {
        art = string.IsNullOrWhiteSpace(art) ? null : art.Trim().ToLowerInvariant();
        if (art is not (null or "projekt" or "ifc"))
            throw new HlksFehler($"koordinaten «{art}» gibt es nicht.", "koordinaten: ifc (Vorgabe mit Projektbezug) | projekt (Vorgabe ohne Projektbezug)");
        if (art == "projekt") return new("projekt", null);
        var bezug = IfcBezug.Aus(p.Modell.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcBezug")?.Inhalt);
        if (bezug is not null) return new("ifc", bezug);
        if (art == "ifc")
            throw new HlksFehler("Das Projekt hat keinen IFC-Bezug (Projektinformation.ifcBezug) – IFC-Weltkoordinaten sind nicht bestimmbar.",
                "koordinaten «projekt» wählen bzw. weglassen oder zuerst ein IFC übernehmen (ifc_uebernehmen setzt den Projektbezug).");
        hinweise.Add("Das Projekt hat keinen IFC-Bezug (noch keine IFC-Unterlage übernommen): die Viewpoints stehen in Projektkoordinaten von KICAD.");
        return new("projekt", null);
    }

    // ------------------------------------------------------------------ Dokumente

    static XDocument Dokument(XElement wurzel) => new(new XDeclaration("1.0", "utf-8", null), wurzel);

    static XDocument Erweiterungen(HashSet<string> labelsBenutzt, SortedSet<string> nutzer)
    {
        var labels = BcfFormat.Labels.Concat(labelsBenutzt.Where(l => !BcfFormat.Labels.Contains(l)).Order(StringComparer.Ordinal)).ToList();
        var ext = new XElement("Extensions",
            new XElement("TopicTypes", BcfFormat.TopicTypen.Select(t => new XElement("TopicType", t))),
            new XElement("TopicStatuses", BcfFormat.StatusAusgabe.Select(s => new XElement("TopicStatus", s.Bcf))),
            new XElement("Priorities", BcfFormat.Prioritaeten.Select(t => new XElement("Priority", t))),
            new XElement("TopicLabels", labels.Select(l => new XElement("TopicLabel", BcfFormat.Sauber(l)))));
        if (nutzer.Count > 0) ext.Add(new XElement("Users", nutzer.Select(u => new XElement("User", u))));
        return Dokument(ext);
    }

    static XDocument Markup(Projekt p, Element e, Guid tg, string art, List<BcfBeteiligter> bet, string? autor, bool mitViewpoint,
        SortedSet<string> nutzer, HashSet<string> labelsBenutzt)
    {
        var m = p.Modell;
        var status = e.Text("status") ?? "neu";
        var erstellt = e.Angelegt?.Zeit ?? e.Geaendert?.Zeit ?? DateTimeOffset.Now;
        var erstellAutor = autor ?? BcfFormat.NichtLeer(e.Angelegt?.Akteur)?.Trim() ?? BcfFormat.NichtLeer(p.Akteur)?.Trim() ?? BcfFormat.Ursprungssystem;
        nutzer.Add(erstellAutor);

        var topic = new XElement("Topic",
            new XAttribute("Guid", BcfFormat.Text(tg)),
            new XAttribute("TopicType", BcfFormat.TopicTyp(art)),
            new XAttribute("TopicStatus", BcfFormat.StatusBcf(status)));
        topic.Add(new XElement("Title", Titel(e, art, bet)));
        topic.Add(new XElement("Priority", BcfFormat.Prioritaet(art)));
        var labels = bet.Select(b => b.Label).Distinct().Order(StringComparer.Ordinal).ToList();
        foreach (var l in labels) labelsBenutzt.Add(l);
        if (labels.Count > 0) topic.Add(new XElement("Labels", labels.Select(l => new XElement("Label", BcfFormat.Sauber(l)))));
        topic.Add(new XElement("CreationDate", BcfFormat.Zeit(erstellt)));
        topic.Add(new XElement("CreationAuthor", erstellAutor));
        if (e.Geaendert is { } g && g.Zeit > erstellt)
        {
            var aendAutor = autor ?? BcfFormat.NichtLeer(g.Akteur)?.Trim() ?? erstellAutor;
            nutzer.Add(aendAutor);
            topic.Add(new XElement("ModifiedDate", BcfFormat.Zeit(g.Zeit)));
            topic.Add(new XElement("ModifiedAuthor", aendAutor));
        }
        topic.Add(new XElement("Description", Beschreibung(p, e, art, bet)));

        // Kommentare: eigener Text (feste Guid) und übernommene BCF-Kommentare mit ihrer ursprünglichen Guid
        var kom = e.Eigenschaft("kommentar");
        var zer = BcfKommentare.Zerlegen(kom?.AlsText());
        var comments = new List<(DateTimeOffset Datum, XElement El)>();
        if (BcfFormat.NichtLeer(zer.Eigen) is { } eigen)
        {
            var datum = kom?.Herkunft.Zeit is { } z && z != default ? z : erstellt;
            var kAutor = autor ?? BcfFormat.NichtLeer(kom?.Herkunft.Akteur)?.Trim() ?? erstellAutor;
            nutzer.Add(kAutor);
            comments.Add((datum, Comment(BcfFormat.KommentarGuid(tg), datum, kAutor, eigen)));
        }
        foreach (var c in zer.Uebernommen)
        {
            var cAutor = BcfFormat.NichtLeer(c.Autor)?.Trim() ?? "unbekannt";
            var datum = c.Datum ?? erstellt;
            comments.Add((datum, Comment(c.Guid, datum, cAutor, BcfFormat.NichtLeer(c.Text))));
        }
        if (comments.Count > 0) topic.Add(new XElement("Comments", comments.OrderBy(c => c.Datum).Select(c => c.El)));
        if (mitViewpoint)
        {
            var vg = BcfFormat.ViewpointGuid(tg);
            topic.Add(new XElement("Viewpoints", new XElement("ViewPoint", new XAttribute("Guid", BcfFormat.Text(vg)),
                new XElement("Viewpoint", BcfFormat.Text(vg) + ".bcfv"), new XElement("Index", 0))));
        }

        var markup = new XElement("Markup");
        var dateien = bet.Where(b => b.UnterlageId is not null).Select(b => BcfFormat.UnterlageName(m.Finde(b.UnterlageId)))
            .Where(n => n is not null).Select(n => BcfFormat.Sauber(n)).Where(n => n.Trim().Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (dateien.Count > 0) markup.Add(new XElement("Header", new XElement("Files", dateien.Select(d => new XElement("File", new XElement("Filename", d))))));
        markup.Add(topic);
        return Dokument(markup);
    }

    static XElement Comment(Guid g, DateTimeOffset datum, string autor, string? text)
    {
        var c = new XElement("Comment", new XAttribute("Guid", BcfFormat.Text(g)),
            new XElement("Date", BcfFormat.Zeit(datum)), new XElement("Author", BcfFormat.Sauber(autor)));
        if (text is not null) c.Add(new XElement("Comment", text));
        return c;
    }

    static string Titel(Element e, string art, List<BcfBeteiligter> bet)
    {
        var wer = bet.Count == 0 ? "(ohne Beteiligte)" : string.Join(" ↔ ", bet.Select(b => b.Kurz));
        return BcfFormat.NichtLeer($"{e.Kennzeichen} – {art}: {wer}") ?? e.Kennzeichen;
    }

    static string Beschreibung(Projekt p, Element e, string art, List<BcfBeteiligter> bet)
    {
        static string Z(double x) => x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append($"Kollision {e.Kennzeichen}: {art} ({(art == "weich" ? "Abstand unterschritten" : "Durchdringung")})");
        if (e.Zahl("mass") is double mass)
            sb.Append($"\nMass: {mass.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} mm ({(art == "weich" ? "fehlender Abstand" : "Tiefe der Überschneidung")})");
        if (e.Geometrie?.Punkte.FirstOrDefault() is { Length: >= 3 } pt)
            sb.Append($"\nLage: x {Z(pt[0] / 1000)} m, y {Z(pt[1] / 1000)} m, z {Z(pt[2] / 1000)} m (Projektkoordinaten KICAD, x Ost, y Nord, z über ±0.00)");
        if (p.Modell.Finde(e.Geschoss) is { } gs) sb.Append($"\nGeschoss: {gs.Kennzeichen}{(string.IsNullOrWhiteSpace(gs.Name) ? "" : " " + gs.Name)}");
        if (bet.Count > 0)
        {
            sb.Append("\nBeteiligte:");
            foreach (var b in bet) sb.Append("\n- ").Append(b.Anzeige);
        }
        if (e.Text("pruefung") is { Length: > 0 } pl) sb.Append($"\nPrüflauf: {pl}");
        if (e.Text("kennung") is { Length: > 0 } kg) sb.Append($"\nKennung: {kg}");
        sb.Append($"\nStatus in KICAD: {e.Text("status") ?? "neu"}");
        return BcfFormat.Sauber(sb.ToString());
    }

    static XDocument? Viewpoint(Element e, Guid tg, List<BcfBeteiligter> bet, Koordinaten koord)
    {
        if (e.Geometrie?.Punkte.FirstOrDefault() is not { Length: >= 3 } pt || !pt.Take(3).All(double.IsFinite)) return null;
        var ziel = koord.Punkt(pt);
        var blick = koord.Richtung(Blick);
        var oben = koord.Richtung(Oben);
        var d = Abstand(e.Zahl("mass"));
        double[] stand = [ziel[0] - d * blick[0], ziel[1] - d * blick[1], ziel[2] - d * blick[2]];

        static XElement Punkt(string name, double[] v) =>
            new(name, new XElement("X", BcfFormat.Zahl(v[0])), new XElement("Y", BcfFormat.Zahl(v[1])), new XElement("Z", BcfFormat.Zahl(v[2])));

        var comps = new XElement("Components");
        if (bet.Count > 0) comps.Add(new XElement("Selection", bet.Select(Komponente)));
        comps.Add(new XElement("Visibility", new XAttribute("DefaultVisibility", "true")));
        var vi = new XElement("VisualizationInfo", new XAttribute("Guid", BcfFormat.Text(BcfFormat.ViewpointGuid(tg))),
            comps,
            new XElement("PerspectiveCamera",
                Punkt("CameraViewPoint", stand), Punkt("CameraDirection", blick), Punkt("CameraUpVector", oben),
                new XElement("FieldOfView", BcfFormat.Zahl(Sichtfeld)), new XElement("AspectRatio", BcfFormat.Zahl(Seitenverhaeltnis))));
        return Dokument(vi);
    }

    static XElement Komponente(BcfBeteiligter b)
    {
        var c = new XElement("Component");
        if (b.IfcGuid is not null) c.Add(new XAttribute("IfcGuid", b.IfcGuid));
        else
        {
            if (BcfFormat.NichtLeer(b.Ursprung) is { } u) c.Add(new XElement("OriginatingSystem", u));
            if (BcfFormat.NichtLeer(b.AuthoringToolId) is { } a) c.Add(new XElement("AuthoringToolId", a));
        }
        return c;
    }

    // ------------------------------------------------------------------ Zip

    static long ZipSchreiben(string pfad, List<(string Name, XDocument Doc)> dokumente)
    {
        string? temp = null;
        try
        {
            var ordner = Path.GetDirectoryName(pfad)!;
            Directory.CreateDirectory(ordner);
            temp = Path.Combine(ordner, "." + Path.GetFileName(pfad) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var einst = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n" };
                foreach (var (name, doc) in dokumente)
                {
                    var eintrag = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var s = eintrag.Open();
                    using var w = XmlWriter.Create(s, einst);
                    doc.Save(w);
                }
            }
            File.Move(temp, pfad, overwrite: true);
            temp = null;
            return new FileInfo(pfad).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new HlksFehler($"Die Datei «{pfad}» kann nicht geschrieben werden: {ex.Message}", "Anderen Pfad angeben bzw. die Datei in anderen Programmen schliessen.");
        }
        finally
        {
            if (temp is not null) try { File.Delete(temp); } catch (Exception) { }
        }
    }
}
