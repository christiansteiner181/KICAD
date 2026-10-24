// Quest App V1a (Modul XR, XR-E «Problem-Marker mit Nachverfolgung»; entwurf/modul-xr.md: «Vorschlag: als BCF-Themen über den bestehenden BCF-Weg (bcf_export,
// bcf_lesen), damit Architekt und Fachplaner sie sehen»): bcf_export mit klasse = ProblemMarker. Dieselben Bausteine wie die Kollisionen (BcfExport.cs: Version,
// Erweiterungen, Projekt, Koordinaten, Komponenten, Schemaprüfung jeder XML-Datei, Zip), je Marker ein Topic:
//   Guid           fest aus Projekt und Kennung des Markers (bzw. Element-Id) – derselbe Marker bleibt bei jedem Export dasselbe Topic
//   TopicType      «Problem»; TopicStatus offen → «Offen», erledigt → «Erledigt» (ANNAHME der Bezeichnungen, wie bei den Kollisionen)
//   Title          «PM-3 – Kollision mit Lüftung» (erste Zeile der Notiz) bzw. «PM-3 – Problem (ohne Beschreibung)»
//   CreationDate   Zeit aus der Brille (Eigenschaft zeit), sonst das Anlegen; CreationAuthor = benutzer bzw. der Bearbeiter
//   DueDate        frist (JJJJ-MM-TT, 00:00 Ortszeit); AssignedTo = zustaendig (steht auch unter Users in extensions.xml)
//   Description    Notiz, Lage, Geschoss, betroffenes Element, Zuständig, Frist, gesetzt von, Status
//   Viewpoint      PerspectiveCamera am Blickpunkt der Brille (Eigenschaft blickpunkt): Kamera am Kopf, Blick auf die markierte Stelle (sonst in Blickrichtung),
//                  höchstens AbstandMax weg (am Sitzungstisch liegt der Kopf im Massstab weit über dem Modell); ohne Blickpunkt wie bei den Kollisionen schräg
//                  von oben aus Südwest im Abstand AbstandMin. Das betroffene Element ist ausgewählt (Selection).
// Bild: Snapshots entfallen wie bei den Kollisionen (das Bild der 3D-Ansicht vom Blickpunkt ist ein Vorschlag im Bericht V1a).

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Hlks.Kern;

public static partial class BcfExport
{
    public const string MarkerKlasse = "ProblemMarker";
    public const string MarkerTopicTyp = "Problem";
    /// <summary>Status des Markers → TopicStatus (ANNAHME der Bezeichnungen).</summary>
    public static readonly IReadOnlyList<(string Kicad, string Bcf)> MarkerStatus = [("offen", "Offen"), ("erledigt", "Erledigt")];

    /// <summary>Topic-Guid eines Markers: aus der Kennung der Brille, sonst aus der Element-Id.</summary>
    public static Guid MarkerGuid(string projektId, Element e) =>
        BcfFormat.UuidV5(BcfFormat.Namensraum, e.Text("kennung") is { Length: > 0 } k ? $"{projektId}/problemmarker/{k}" : $"{projektId}/problemmarker-id/{e.Id}");

    static string MarkerStatusBcf(string? s) => MarkerStatus.FirstOrDefault(x => x.Kicad == (s ?? "offen")).Bcf ?? "Offen";

    static JsonObject MarkerSchreiben(Projekt p, JsonObject par, string pfad)
    {
        var m = p.Modell; var kat = p.Katalog;
        var def = kat.Klasse(MarkerKlasse);
        var erlaubt = def.Eigenschaften.FirstOrDefault(x => x.Name == "status")?.Auswahl ?? [];
        var statusFilter = Liste(par, "status")?.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
        if (statusFilter is not null)
            foreach (var s in statusFilter.Where(s => !erlaubt.Contains(s)))
                throw new HlksFehler($"Status «{s}» gibt es für Problem-Marker nicht.", "status: " + string.Join(", ", erlaubt));
        IEnumerable<Element> kandidaten;
        if (Liste(par, "marker") is { } namen)
        {
            var gewaehlt = new List<Element>();
            foreach (var n in namen)
            {
                var e = m.Hole(n, MarkerKlasse);
                if (!kat.IstArt(e, MarkerKlasse)) throw new HlksFehler($"{e.Kennzeichen} ist kein Problem-Marker, sondern {e.Klasse}.", "In marker nur Problem-Marker angeben.");
                if (!gewaehlt.Contains(e)) gewaehlt.Add(e);
            }
            kandidaten = gewaehlt;
        }
        else kandidaten = m.Elemente.Where(e => kat.IstArt(e, MarkerKlasse));
        var auswahl = kandidaten.Where(e => statusFilter is null || statusFilter.Contains(e.Text("status") ?? "offen"))
            .OrderBy(e => e.Kennzeichen, BcfFormat.Natuerlich).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        if (auswahl.Count == 0)
            throw new HlksFehler(m.Elemente.Any(e => kat.IstArt(e, MarkerKlasse)) ? "Kein Problem-Marker passt zum Filter – nichts für den BCF-Export." : "Das Projekt hat keine Problem-Marker – nichts für den BCF-Export.",
                "Marker in der Quest App setzen (App «Marker» › «Problem markieren») bzw. den Filter (marker, status) lockern.");

        var hinweise = new List<string>();
        var koord = KoordinatenLesen(p, Param.Text(par, "koordinaten"), hinweise);
        var autor = BcfFormat.NichtLeer(Param.Text(par, "autor"))?.Trim();
        var aufl = new BcfAufloesung(p, m);
        var nutzer = new SortedSet<string>(StringComparer.Ordinal);
        var labelsBenutzt = new HashSet<string>(StringComparer.Ordinal);
        var dokumente = new List<(string Name, XDocument Doc)>();
        var liste = new JsonArray();
        var vergeben = new HashSet<Guid>();
        foreach (var e in auswahl)
        {
            var tg = MarkerGuid(p.Kopf.Id, e);
            if (!vergeben.Add(tg)) { tg = BcfFormat.UuidV5(BcfFormat.Namensraum, $"{p.Kopf.Id}/problemmarker-id/{e.Id}"); vergeben.Add(tg); }
            var bet = aufl.Beteiligte(e, hinweise);
            var vp = MarkerViewpoint(e, tg, bet, koord);
            if (vp is null) hinweise.Add($"{e.Kennzeichen}: keine Lage (Geometrie punkt) – Topic ohne Viewpoint.");
            var markup = MarkerMarkup(p, e, tg, bet, autor, vp is not null, nutzer, labelsBenutzt);
            dokumente.Add(($"{BcfFormat.Text(tg)}/markup.bcf", markup));
            if (vp is not null) dokumente.Add(($"{BcfFormat.Text(tg)}/{BcfFormat.Text(BcfFormat.ViewpointGuid(tg))}.bcfv", vp));
            liste.Add(new JsonObject
            {
                ["marker"] = e.Kennzeichen, ["topic"] = BcfFormat.Text(tg), ["status"] = MarkerStatusBcf(e.Text("status")),
                ["titel"] = markup.Root!.Element("Topic")!.Element("Title")!.Value, ["viewpoint"] = vp is not null,
            });
        }
        dokumente.Insert(0, ("bcf.version", Dokument(new XElement("Version", new XAttribute("VersionId", BcfFormat.Version)))));
        var labels = BcfFormat.Labels.Concat(labelsBenutzt.Where(l => !BcfFormat.Labels.Contains(l)).Order(StringComparer.Ordinal)).ToList();
        var ext = new XElement("Extensions",
            new XElement("TopicTypes", new XElement("TopicType", MarkerTopicTyp)),
            new XElement("TopicStatuses", MarkerStatus.Select(s => new XElement("TopicStatus", s.Bcf))),
            new XElement("Priorities", BcfFormat.Prioritaeten.Select(t => new XElement("Priority", t))),
            new XElement("TopicLabels", labels.Select(l => new XElement("TopicLabel", BcfFormat.Sauber(l)))));
        if (nutzer.Count > 0) ext.Add(new XElement("Users", nutzer.Select(u => new XElement("User", u))));
        dokumente.Insert(1, ("extensions.xml", Dokument(ext)));
        dokumente.Insert(2, ("project.bcfp", Dokument(new XElement("ProjectInfo",
            new XElement("Project", new XAttribute("ProjectId", BcfFormat.NichtLeer(p.Kopf.Id) ?? "KICAD"),
                BcfFormat.NichtLeer(p.Kopf.Name) is { } pn ? new XElement("Name", pn) : null)))));

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
                    "Interner Fehler im BCF-Export – bitte mit der Meldung melden; bis dahin den betroffenen Marker mit dem Filter marker auslassen.");
            pruefung = $"bestanden: {dokumente.Count} XML-Dateien gegen schema/bcf-3.0 geprüft";
        }
        var bytes = ZipSchreiben(pfad, dokumente);
        return new JsonObject
        {
            ["datei"] = pfad, ["bcfVersion"] = BcfFormat.Version, ["groesseBytes"] = bytes, ["klasse"] = MarkerKlasse, ["topics"] = auswahl.Count,
            ["offen"] = auswahl.Count(e => (e.Text("status") ?? "offen") == "offen"), ["koordinaten"] = koord.Text, ["schemapruefung"] = pruefung,
            ["marker"] = liste, ["hinweise"] = new JsonArray(hinweise.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
        };
    }

    static XDocument MarkerMarkup(Projekt p, Element e, Guid tg, List<BcfBeteiligter> bet, string? autor, bool mitViewpoint, SortedSet<string> nutzer,
        HashSet<string> labelsBenutzt)
    {
        var erstellt = DateTimeOffset.TryParse(e.Text("zeit"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var z) ? z : e.Angelegt?.Zeit ?? DateTimeOffset.Now;
        var erstellAutor = autor ?? BcfFormat.NichtLeer(e.Text("benutzer"))?.Trim() ?? BcfFormat.NichtLeer(e.Angelegt?.Akteur)?.Trim() ?? BcfFormat.Ursprungssystem;
        nutzer.Add(BcfFormat.Sauber(erstellAutor));
        var notiz = BcfFormat.NichtLeer(e.Text("notiz"))?.Trim();
        var titel = $"{e.Kennzeichen} – {(notiz is null ? "Problem (ohne Beschreibung)" : notiz.Split('\n')[0].Trim() is var t && t.Length > 80 ? t[..79] + "…" : t)}";
        var topic = new XElement("Topic", new XAttribute("Guid", BcfFormat.Text(tg)), new XAttribute("TopicType", MarkerTopicTyp),
            new XAttribute("TopicStatus", MarkerStatusBcf(e.Text("status"))));
        topic.Add(new XElement("Title", BcfFormat.Sauber(titel)));
        topic.Add(new XElement("Priority", "Normal"));
        var labels = bet.Select(b => b.Label).Distinct().Order(StringComparer.Ordinal).ToList();
        foreach (var l in labels) labelsBenutzt.Add(l);
        if (labels.Count > 0) topic.Add(new XElement("Labels", labels.Select(l => new XElement("Label", BcfFormat.Sauber(l)))));
        topic.Add(new XElement("CreationDate", BcfFormat.Zeit(erstellt)));
        topic.Add(new XElement("CreationAuthor", BcfFormat.Sauber(erstellAutor)));
        if (e.Geaendert is { } g && g.Zeit > erstellt)
        {
            var aendAutor = autor ?? BcfFormat.NichtLeer(g.Akteur)?.Trim() ?? erstellAutor;
            nutzer.Add(BcfFormat.Sauber(aendAutor));
            topic.Add(new XElement("ModifiedDate", BcfFormat.Zeit(g.Zeit)));
            topic.Add(new XElement("ModifiedAuthor", BcfFormat.Sauber(aendAutor)));
        }
        if (DateTime.TryParseExact(e.Text("frist")?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var frist))
            topic.Add(new XElement("DueDate", BcfFormat.Zeit(new DateTimeOffset(frist, TimeZoneInfo.Local.GetUtcOffset(frist)))));
        if (BcfFormat.NichtLeer(e.Text("zustaendig"))?.Trim() is { } zu)
        {
            nutzer.Add(BcfFormat.Sauber(zu));
            topic.Add(new XElement("AssignedTo", BcfFormat.Sauber(zu)));
        }
        topic.Add(new XElement("Description", MarkerBeschreibung(p, e, bet, notiz)));
        if (mitViewpoint)
        {
            var vg = BcfFormat.ViewpointGuid(tg);
            topic.Add(new XElement("Viewpoints", new XElement("ViewPoint", new XAttribute("Guid", BcfFormat.Text(vg)),
                new XElement("Viewpoint", BcfFormat.Text(vg) + ".bcfv"), new XElement("Index", 0))));
        }
        return Dokument(new XElement("Markup", topic));
    }

    static string MarkerBeschreibung(Projekt p, Element e, List<BcfBeteiligter> bet, string? notiz)
    {
        static string Z(double x) => x.ToString("0.000", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append($"Problem-Marker {e.Kennzeichen}: {notiz ?? "(ohne Beschreibung)"}");
        if (e.Geometrie?.Punkte.FirstOrDefault() is { Length: >= 3 } pt)
            sb.Append($"\nLage: x {Z(pt[0] / 1000)} m, y {Z(pt[1] / 1000)} m, z {Z(pt[2] / 1000)} m (Projektkoordinaten KICAD, x Ost, y Nord, z über ±0.00)");
        if (p.Modell.Finde(e.Geschoss) is { } gs) sb.Append($"\nGeschoss: {gs.Kennzeichen}{(string.IsNullOrWhiteSpace(gs.Name) ? "" : " " + gs.Name)}");
        foreach (var b in bet) sb.Append($"\nBetroffen: {b.Anzeige}");
        if (e.Text("zustaendig") is { Length: > 0 } zu) sb.Append($"\nZuständig: {zu}");
        if (e.Text("frist") is { Length: > 0 } fr) sb.Append($"\nFrist: {fr}");
        if (e.Text("benutzer") is { Length: > 0 } be) sb.Append($"\nGesetzt von: {be}{(e.Text("quelle") == "brille" ? " (Quest App)" : "")}");
        sb.Append($"\nStatus in KICAD: {e.Text("status") ?? "offen"}");
        return BcfFormat.Sauber(sb.ToString());
    }

    /// <summary>Kamera aus dem Blickpunkt (siehe Dateikopf): Kopf (mm, Projekt), Blick auf die Stelle, Oben senkrecht dazu in der Lotebene; null ohne Lage.</summary>
    static XDocument? MarkerViewpoint(Element e, Guid tg, List<BcfBeteiligter> bet, Koordinaten koord)
    {
        if (e.Geometrie?.Punkte.FirstOrDefault() is not { Length: >= 3 } pt || !pt.Take(3).All(double.IsFinite)) return null;
        var (kopfMm, blick) = MarkerKamera(e, pt);
        var stand = koord.Punkt(kopfMm);
        var r = koord.Richtung(blick);
        var oben = koord.Richtung(ObenZu(blick));
        static XElement P(string name, double[] v) =>
            new(name, new XElement("X", BcfFormat.Zahl(v[0])), new XElement("Y", BcfFormat.Zahl(v[1])), new XElement("Z", BcfFormat.Zahl(v[2])));
        var comps = new XElement("Components");
        if (bet.Count > 0) comps.Add(new XElement("Selection", bet.Select(Komponente)));
        comps.Add(new XElement("Visibility", new XAttribute("DefaultVisibility", "true")));
        return Dokument(new XElement("VisualizationInfo", new XAttribute("Guid", BcfFormat.Text(BcfFormat.ViewpointGuid(tg))), comps,
            new XElement("PerspectiveCamera", P("CameraViewPoint", stand), P("CameraDirection", r), P("CameraUpVector", oben),
                new XElement("FieldOfView", BcfFormat.Zahl(Sichtfeld)), new XElement("AspectRatio", BcfFormat.Zahl(Seitenverhaeltnis)))));
    }

    /// <summary>
    /// Kamera eines Markers in Projekt-mm: am Kopf des Blickpunkts mit Blick auf die Stelle (höchstens AbstandMax m davor; näher als 0.1 m: Blickrichtung); ohne
    /// Blickpunkt schräg von oben aus Südwest im Abstand AbstandMin (wie die Kollisionen). Rückgabe: Standort (mm) und Blick (Einheit).
    /// </summary>
    public static (double[] Kopf, double[] Blick) MarkerKamera(Element e, double[] ort)
    {
        double[]? V(JsonNode? n) => n is JsonArray a && a.Count == 3 && a.All(x => x is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d))
            ? [.. a.Select(x => x!.GetValue<double>())] : null;
        var bp = e.Eigenschaft("blickpunkt")?.Inhalt as JsonObject;
        if (V(bp?["kopf"]) is { } kopf)
        {
            double[] d = [ort[0] - kopf[0], ort[1] - kopf[1], ort[2] - kopf[2]];
            var l = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (l >= 100)
            {
                double[] r = [d[0] / l, d[1] / l, d[2] / l];
                var abstand = Math.Min(l, AbstandMax * 1000);
                return ([ort[0] - r[0] * abstand, ort[1] - r[1] * abstand, ort[2] - r[2] * abstand], r);
            }
            if (V(bp?["richtung"]) is { } rr && Math.Sqrt(rr[0] * rr[0] + rr[1] * rr[1] + rr[2] * rr[2]) is var lr && lr > 1e-9)
                return (kopf, [rr[0] / lr, rr[1] / lr, rr[2] / lr]);
        }
        var a = AbstandMin * 1000;
        return ([ort[0] - Blick[0] * a, ort[1] - Blick[1] * a, ort[2] - Blick[2] * a], [.. Blick]);
    }

    /// <summary>Oben-Vektor senkrecht zum Blick in der Lotebene (z oben); bei senkrechtem Blick die y-Achse.</summary>
    public static double[] ObenZu(double[] r)
    {
        double[] z = [0, 0, 1];
        var s = r[2];
        double[] o = [z[0] - s * r[0], z[1] - s * r[1], z[2] - s * r[2]];
        var l = Math.Sqrt(o[0] * o[0] + o[1] * o[1] + o[2] * o[2]);
        return l < 1e-6 ? [0, 1, 0] : [o[0] / l, o[1] / l, o[2] / l];
    }
}
