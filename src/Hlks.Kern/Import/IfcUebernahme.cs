// IFC-Übertrag in KICAD (Modul 1b): Befehl ifc_uebernehmen und Abfrage ifc_pruefen.
// Ablauf: Datei als Unterlage ablegen → Bezug (Koordinaten) festlegen → Gebäude und Geschosse zuordnen oder anlegen →
// Übertrager planen Elemente (Architektur, Räume, HLKS; je Bereich eine Datei, per Reflexion gefunden) → Abgleich über
// die GlobalId (neu, geändert, gelöscht) → ein Paket. Nicht Übernommenes kommt als Hintergrund-Netz in die Unterlage.
// Konflikte: Wurde ein übernommenes Element seither in KICAD geändert, gewinnt nach Vorgabe KICAD (Hinweis); mit
// konflikte = ifc gewinnt die neue IFC-Fassung. Siehe entwurf/modul-1b.md.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public static class IfcImport
{
    static readonly Lazy<IReadOnlyList<IIfcUebertrag>> uebertrager = new(() =>
        typeof(IIfcUebertrag).Assembly.GetTypes()
            .Where(t => typeof(IIfcUebertrag).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IIfcUebertrag)Activator.CreateInstance(t)!)
            .OrderBy(u => Array.IndexOf(Bereiche, u.Bereich) is var i && i < 0 ? 99 : i).ThenBy(u => u.GetType().Name).ToList());

    static readonly Lazy<IIfcGrundlage?> grundlage = new(() =>
        typeof(IIfcGrundlage).Assembly.GetTypes()
            .Where(t => typeof(IIfcGrundlage).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IIfcGrundlage)Activator.CreateInstance(t)!).FirstOrDefault());

    /// <summary>Bereiche in der Reihenfolge des Übertrags (Räume nach den Wänden, HLKS zuletzt: braucht Räume).</summary>
    public static readonly string[] Bereiche = ["architektur", "raeume", "hlks"];

    /// <summary>Beschreibung der Abfrage ifc_pruefen und ihres Parameters einpassen (gleich im MCP-Werkzeug).</summary>
    public const string PruefenBeschreibung = "Liest ein IFC (IFC4 oder IFC2x3) ohne etwas zu ändern: Fassung, Programm, Einheit, Geschosse, Räume, welche Katalogklassen " +
        "übernommen würden, was ohne Klasse bliebe, Koordinatenbezug mit Lagebericht (trifft keine Wahl das bestehende Modell, auch das Einpassen über Wände und " +
        "Raumkanten mit Empfehlung). Vor ifc_uebernehmen aufrufen.";
    public const string ParEinpassen = "Einpassen über Wände und Raumkanten immer rechnen und im Lagebericht zeigen (lage.einpassen); Vorgabe false: nur, wenn keine " +
        "andere Wahl das bestehende Modell trifft";

    public static IReadOnlyList<IIfcUebertrag> Uebertrager => uebertrager.Value;
    public static IIfcGrundlage? Grundlage => grundlage.Value;

    /// <summary>Datei eines Parameters: Pfad oder Name einer abgelegten Unterlage. Für die KI (Sicherheitsprüfung 3c, B3a/B4) nur Pfade unter dem
    /// Projektordner und den Wurzeln aus kataloge/buero.json, geprüft vor dem ersten Dateizugriff; ein blosser Name meint den Ordner unterlagen.</summary>
    public static string Dateipfad(Projekt p, string datei)
    {
        if (!KiPfade.IstKi(p)) return File.Exists(datei) ? datei : Unterlagen.Pfad(p, datei);
        if (KiPfade.IstName(datei)) return Unterlagen.Pfad(p, datei);
        var voll = KiPfade.Pruefen(p, datei, "IFC-Datei");
        return File.Exists(voll) ? voll : Unterlagen.Pfad(p, datei);
    }

    /// <summary>Abfrage ifc_pruefen: was steht in der Datei, was würde übernommen – ohne etwas zu ändern. einpassen = das Einpassen über
    /// Wände und Raumkanten immer rechnen (sonst nur, wenn keine andere Wahl das bestehende Modell trifft).</summary>
    public static JsonObject Pruefen(Projekt p, string datei, bool einpassen = false)
    {
        using var d = IfcDatei.Oeffnen(Dateipfad(p, datei));
        var projektBezug = p.Modell.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcBezug")?.Inhalt as JsonObject;
        var frueher = p.Modell.DerKlasse("Unterlage").FirstOrDefault(u => u.Text("art") == "ifc"
            && Unterlagen.OriginalName(u.Text("original") ?? "").Equals(Unterlagen.OriginalName(datei), StringComparison.OrdinalIgnoreCase));
        var aus = IfcAusrichtung.Bestimmen(d, projektBezug, frueher?.Eigenschaft("lage")?.Inhalt as JsonObject, null, null, null,
            IfcAusrichtung.ModellBegrenzung(p.Modell, p.Katalog, frueher?.Id),
            new IfcEinpassQuelle(p, p.Modell, frueher?.Id, einpassen ? IfcEinpassQuelle.Immer : IfcEinpassQuelle.Pruefen));
        d.Bezug = aus.Bezug;
        var abb = p.Katalog.Ifc;
        var zuordnung = new SortedDictionary<string, int>();
        var ohne = new SortedDictionary<string, int>();
        var produkte = d.Modell.Instances.OfType<IIfcProduct>()
            .Where(x => x is not (IIfcSpatialStructureElement or IIfcDistributionPort or IIfcOpeningElement or IIfcVirtualElement or IIfcAnnotation)).ToList();
        foreach (var x in produkte)
        {
            var typ = d.Typ(x);
            var t = abb?.KlasseFuer(d.Schema, IfcDatei.Entitaet(x), typ is null ? null : IfcDatei.Entitaet(typ), d.PredefinedType(x), d.ObjectType(x));
            if (t is null) ohne[IfcDatei.Entitaet(x)] = ohne.GetValueOrDefault(IfcDatei.Entitaet(x)) + 1;
            else zuordnung[t.Klasse] = zuordnung.GetValueOrDefault(t.Klasse) + 1;
        }
        var raeume = d.Modell.Instances.OfType<IIfcSpace>().Count();
        var geschosse = new JsonArray();
        foreach (var g in d.Geschosse())
        {
            int n = produkte.Count(x => d.GeschossVon(x) == g);
            geschosse.Add(new JsonObject { ["name"] = g.Name, ["kote"] = Math.Round(g.KoteMm / 1000, 3), ["produkte"] = n, ["ifc"] = g.Quellen.Count });
        }
        return new JsonObject
        {
            ["datei"] = Path.GetFileName(d.Pfad),
            ["schema"] = d.Schema,
            ["programm"] = d.Programm,
            ["einheit"] = d.MmJeEinheit switch { 1000 => "m", 1 => "mm", 10 => "cm", var f => $"{f} mm" },
            ["geschosse"] = geschosse,
            ["raeume"] = raeume,
            ["zuordnung"] = Json.Knoten(zuordnung),
            ["ohneKlasse"] = Json.Knoten(ohne),
            ["ausrichtung"] = aus.Text,
            ["lage"] = aus.Bericht,
            ["bezugProjekt"] = projektBezug?.DeepClone(),
            ["hinweis"] = raeume == 0 ? "Die Datei hat keine Räume (IfcSpace): Räume nach dem Übertrag mit raum_erkennen aus den Wänden bilden." : null,
        };
    }
}

public sealed class IfcUebernehmen : IBefehl
{
    /// <summary>Beschreibung des Parameters ausrichtung (gleich im MCP-Werkzeug).</summary>
    public const string ParAusrichtung = "Nur nötig, wenn die Datei nicht zum Projektbezug passt (ifc_pruefen zeigt die Abweichung): projekt = Weltkoordinaten der Datei wie sie sind | " +
        "gebaeude = Gebäudeursprung der Datei auf den Projektursprung | koordinationskoerper = Koordinationskörper aufeinander | einpassen = Drehung und Verschiebung " +
        "automatisch über Wände und Raumkanten (übernommen nur bei eindeutiger Lage, sonst Abbruch mit Lagebericht; auch für die erste Datei in einem Projekt mit Inhalt)";

    public string Name => "ifc_uebernehmen";
    public string Beschreibung => "Übernimmt ein IFC (IFC4 oder IFC2x3) als eigene, bearbeitbare Elemente: Geschosse, Räume, Wände, Decken, Türen, Fenster, " +
        "Leitungen, Formteile, Armaturen, Apparate und Systeme mit Anschlüssen und Verbindungen. Jedes Element behält die GlobalId; dieselbe Datei " +
        "erneut übernehmen gleicht ab (neu, geändert, gelöscht). Was sich nicht als Element fassen lässt, bleibt als Hintergrund (Unterlage). " +
        "Ein Paket je Übernahme – zuerst als Trockenlauf ansehen.";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("datei", "text", true, "Pfad der IFC-Datei (lokal oder W:) oder Name einer Unterlage im Ordner unterlagen"),
        new("bereiche", "liste", false, "architektur | raeume | hlks (Vorgabe alle)"),
        new("geschosse", "liste", false, "Nur diese Geschosse (Kennzeichen oder Name des Geschosses im Projekt bzw. im IFC, oder Kote in m wie \"3.06\"); Vorgabe alle"),
        new("grundlage", "bool", false, "Nicht übernommene Objekte als Hintergrund zeigen (Vorgabe true)"),
        new("bezug", "objekt", false, "IFC-Weltpunkt { x, y, z } in mm und drehung in Grad, der im Projekt Ursprung und ±0.00 ist; Vorgabe: Bezug des Projekts, beim ersten IFC aus dem Gebäude der Datei"),
        new("ausrichtung", "text", false, ParAusrichtung),
        new("koordinationskoerper", "text", false, "Name des Koordinationskörpers, falls er nicht «Koordinationskörper» heisst"),
        new("konflikte", "text", false, "kicad (Vorgabe): seit dem letzten Übertrag in KICAD geänderte Elemente behalten | ifc: neue IFC-Fassung übernehmen"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var abb = k.Katalog.Ifc ?? throw new HlksFehler("Die IFC-Abbildung (kataloge/ifc.json) fehlt.");
        var quelle = Param.Text(p, "datei", true)!;
        var (name, stand) = Unterlagen.Ablegen(k.Projekt, quelle);
        using var d = IfcDatei.Oeffnen(Unterlagen.Pfad(k.Projekt, name));
        var bereiche = Liste(p, "bereiche") is { Count: > 0 } b ? b.Select(x => x.ToLowerInvariant()).ToHashSet() : IfcImport.Bereiche.ToHashSet();
        foreach (var x in bereiche)
            if (!IfcImport.Bereiche.Contains(x)) throw new HlksFehler($"Bereich «{x}» gibt es nicht.", "Bereiche: " + string.Join(", ", IfcImport.Bereiche));
        var konfliktIfc = (Param.Text(p, "konflikte") ?? "kicad").Equals("ifc", StringComparison.OrdinalIgnoreCase);
        var herkunft = k.Herkunft with { Art = "ifc", Befehl = Name };

        // ---- Unterlage (gleicher Dateiname = neue Fassung derselben Unterlage; Prüfsummen-Präfix der Ablage zählt nicht)
        var original = Unterlagen.OriginalName(quelle);
        var unterlage = k.Arbeit.DerKlasse("Unterlage").FirstOrDefault(u => u.Text("art") == "ifc"
                            && Unterlagen.OriginalName(u.Text("original") ?? "").Equals(original, StringComparison.OrdinalIgnoreCase))?.Kopie()
                        ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("UL"), Klasse = "Unterlage", Name = Path.GetFileNameWithoutExtension(original) };
        bool unterlageNeu = k.Arbeit.Finde(unterlage.Id) is null;

        // ---- Ausrichtung (transparent: nie stillschweigend verschieben, siehe IfcAusrichtung)
        var info = k.Arbeit.DerKlasse("Projektinformation").FirstOrDefault();
        var aus = IfcAusrichtung.Bestimmen(d, info?.Eigenschaft("ifcBezug")?.Inhalt as JsonObject, unterlage.Eigenschaft("lage")?.Inhalt as JsonObject,
            Param.Text(p, "ausrichtung")?.ToLowerInvariant(), p["bezug"], Param.Text(p, "koordinationskoerper"),
            IfcAusrichtung.ModellBegrenzung(k.Arbeit, k.Katalog, unterlage.Id),
            new IfcEinpassQuelle(k.Projekt, k.Arbeit, unterlage.Id));
        k.Lage = (JsonObject)aus.Bericht.DeepClone();   // Lagebericht ins Ergebnis des Befehls (auch Trockenlauf und Ablehnung)
        if (aus.Bewertung == IfcAusrichtung.NichtEindeutig) throw new HlksFehler(aus.Text, IfcAusrichtung.VorschlagEinpassen);   // auch im Trockenlauf
        if (aus.Bewertung == "abweichung")
        {
            if (!k.Trockenlauf)
                throw new HlksFehler(aus.Text,
                    "Ausrichtung ausdrücklich wählen: ausrichtung = projekt (Weltkoordinaten der Datei wie sie sind), gebaeude (Gebäudeursprung der Datei auf den " +
                    "Projektursprung), koordinationskoerper (Koordinationskörper aufeinander) oder einpassen (automatisch über Wände und Raumkanten, nur bei " +
                    "eindeutiger Lage). ifc_pruefen zeigt alle Lagen. Revit-Export: «Gemeinsame Koordinaten» prüfen.");
            k.Hinweise.Add(aus.Text + " (Trockenlauf mit dem Projektbezug gerechnet.)");
        }
        d.Bezug = aus.Bezug;
        if (aus.Neu && aus.Bewertung != "abweichung")
        {
            bool eingepasst = aus.Bericht["ausrichtung"]?.GetValue<string>() == "einpassen";
            var e = info?.Kopie() ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation" };
            var bezugJson = IfcAusrichtung.ProjektBezug(d, aus.Bezug, Param.Text(p, "koordinationskoerper"));
            if (eingepasst) { bezugJson["quelle"] = bezugJson["quelle"]!.GetValue<string>() + ", eingepasst"; bezugJson["eingepasst"] = true; }
            e.Eigenschaften["ifcBezug"] = new Wert
            {
                Inhalt = bezugJson,
                Herkunft = k.Herkunft with { Art = "ifc", Befehl = Name, Quelle = eingepasst ? $"{original}: eingepasst über Wände und Raumkanten" : $"{original}: Gebäudeursprung und ±0.00" },
            };
            k.Setzen(e);
        }
        k.Hinweise.Add(aus.Text);

        // ---- Gebäude und Geschosse
        var geschossIds = GeschosseZuordnen(k, d, unterlage.Id);
        var filter = GeschossFilter(k, d, geschossIds, Liste(p, "geschosse"));

        var lauf = new IfcLauf
        {
            Datei = d, Kontext = k, UnterlageId = unterlage.Id,
            Quelle = $"{original} ({d.Schema}{(d.Programm.Length > 0 ? ", " + d.Programm : "")})",
            Herkunft = herkunft, GeschossIds = geschossIds, GeschossFilter = filter,
        };
        var ausgefuehrt = IfcImport.Uebertrager.Where(u => bereiche.Contains(u.Bereich)).ToList();
        if (ausgefuehrt.Count == 0) k.Hinweise.Add("Für die gewählten Bereiche gibt es noch keinen Übertrager; nur Geschosse und Hintergrund.");
        foreach (var u in ausgefuehrt) u.Uebertragen(lauf);

        // ---- Abgleich und Übernahme
        var zaehler = Abgleichen(k, lauf, unterlage.Id, ausgefuehrt.SelectMany(u => u.Klassen).ToHashSet(StringComparer.OrdinalIgnoreCase), konfliktIfc);

        // ---- Netzdatei: exakte Geometrie aller Produkte (Kollision, Aussparungen); die nicht übernommenen = Hintergrund
        double[]? umriss = null; int imNetz = 0;
        var mitGeometrie = d.Modell.Instances.OfType<IIfcProduct>()
            .Where(x => x.Representation is not null
                        && x is not (IIfcSpatialStructureElement or IIfcDistributionPort or IIfcOpeningElement or IIfcVirtualElement or IIfcAnnotation)
                        && lauf.ImFilter(x)).ToList();
        int rest = mitGeometrie.Count(x => !lauf.Behandelt.Contains(x.EntityLabel));
        if (Param.Bool(p, "grundlage", true) && IfcImport.Grundlage is { } gl && mitGeometrie.Count > 0)
        {
            if (k.Trockenlauf)
            {
                imNetz = rest;
                if (!unterlageNeu && unterlage.Geometrie is { Punkte.Count: >= 3 } g)   // Trockenlauf rechnet kein Netz: Umriss bleibt
                    umriss = [g.Punkte.Min(q => q[0]), g.Punkte.Min(q => q[1]), g.Punkte.Max(q => q[0]), g.Punkte.Max(q => q[1])];
            }
            else
            {
                var schluessel = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    $"f{IfcNetzErzeuger.Fassung}|{d.Bezug}|{string.Join(",", mitGeometrie.Select(x => lauf.Behandelt.Contains(x.EntityLabel) ? $"u{x.EntityLabel}" : $"{x.EntityLabel}"))}")))[..10].ToLowerInvariant();   // Fassung im Schlüssel (Modul 3b): eine neue Netzdatei-Fassung wird neu erzeugt
                (int, double[]?) ergebnis = (0, null);
                bool erzeugt = false;
                var netz = Unterlagen.Ableitung(k.Projekt, name, $".netz-{schluessel}.bin", ziel => { ergebnis = gl.Erzeugen(d, mitGeometrie, lauf.Behandelt, ziel); erzeugt = true; });
                umriss = ergebnis.Item2;
                imNetz = rest;
                // Netz schon vorhanden (gleiche Datei, gleicher Bezug, gleiche Übernahme): Umriss der Unterlage bleibt wie er ist
                if (!erzeugt && !unterlageNeu && unterlage.Text("netz") == Path.GetFileName(netz) && unterlage.Geometrie is { Punkte.Count: >= 3 } g)
                    umriss = [g.Punkte.Min(q => q[0]), g.Punkte.Min(q => q[1]), g.Punkte.Max(q => q[0]), g.Punkte.Max(q => q[1])];
                unterlage.Eigenschaften["netz"] = W(Path.GetFileName(netz), herkunft);
            }
        }
        else if (rest > 0) k.Hinweise.Add($"{rest} Objekte nicht übernommen und ohne Hintergrund (grundlage = false oder noch kein Netz-Erzeuger).");

        // ---- Unterlage schreiben
        void E(string n, JsonNode? w) { if (w is not null) unterlage.Eigenschaften[n] = W(w, herkunft); }
        // original: ein fremder Pfad bleibt (Herkunft), ein Name aus der Ablage ohne Prüfsumme
        E("datei", name); E("original", File.Exists(quelle) && !Path.GetFullPath(quelle).StartsWith(Unterlagen.Ordner(k.Projekt), StringComparison.OrdinalIgnoreCase) ? quelle : original); E("art", "ifc"); E("stand", stand); E("ifcSchema", d.Schema); E("ifcProgramm", d.Programm);
        E("lage", aus.Bericht);
        if (unterlageNeu) { E("anzeigen", true); E("deckkraft", 0.5); }
        var zusammen = new JsonObject();
        foreach (var (kl, n) in zaehler.Uebernommen.OrderBy(x => x.Key)) zusammen[kl] = n;
        zusammen["grundlage"] = imNetz;
        if (lauf.NichtVerstanden.Count > 0) zusammen["nichtVerstanden"] = Json.Knoten(lauf.NichtVerstanden.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value));
        E("uebernommen", zusammen);
        umriss ??= Umriss(lauf.Geplant.Values.Select(e => k.Arbeit.Finde(ZielId(zaehler, e)) ?? e));
        unterlage.Geometrie = new Geometrie
        {
            Art = "polygon",
            Punkte = umriss is { } um ? [[um[0], um[1], 0], [um[2], um[1], 0], [um[2], um[3], 0], [um[0], um[3], 0]] : [[0, 0, 0], [1000, 0, 0], [1000, 1000, 0], [0, 1000, 0]],
        };
        unterlage.Angelegt ??= herkunft with { Quelle = lauf.Quelle };
        if (k.Arbeit.Finde(unterlage.Id) is not { } bisher || !GleicherInhalt(bisher, unterlage)) k.Setzen(unterlage);

        k.Hinweise.Insert(0, $"{original}: {d.Schema}, {d.Programm}. Übernommen: {zaehler.Neu} neu, {zaehler.Geaendert} geändert, {zaehler.Geloescht} gelöscht, " +
                             $"{zaehler.Gleich} unverändert; {imNetz} Objekte als Hintergrund.");
        if (lauf.NichtVerstanden.Count > 0)
            k.Hinweise.Add("Nicht verstanden (im Hintergrund): " + string.Join(", ", lauf.NichtVerstanden.OrderByDescending(x => x.Value).Take(12).Select(x => $"{x.Key} {x.Value}")));
        k.Hinweise.AddRange(lauf.Hinweise.Take(50));
        if (lauf.Hinweise.Count > 50) k.Hinweise.Add($"… und {lauf.Hinweise.Count - 50} weitere Hinweise.");
    }

    static string ZielId(Zaehler z, Element e) => z.Ids.GetValueOrDefault(e.Id, e.Id);

    static Wert W(JsonNode wert, Herkunft h) => new() { Inhalt = wert, Herkunft = h };

    static List<string> Liste(JsonObject p, string n) =>
        p[n] is JsonArray a ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : x?.ToJsonString() ?? "").Where(s => s.Length > 0).ToList()
        : Param.Text(p, n) is { Length: > 0 } t ? t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() : [];

    /// <summary>Ordnet die IFC-Geschosse den Geschossen des Projekts zu (gleiche Kote ± 50 mm) oder legt sie an.</summary>
    static Dictionary<IfcGeschoss, string> GeschosseZuordnen(Kontext k, IfcDatei d, string unterlageId)
    {
        var aus = new Dictionary<IfcGeschoss, string>();
        var ifcG = d.Geschosse();
        if (ifcG.Count == 0) return aus;
        var gebaeude = k.Arbeit.DerKlasse("Gebaeude").FirstOrDefault();
        if (gebaeude is null)
        {
            var b = d.Modell.Instances.FirstOrDefault<IIfcBuilding>();
            gebaeude = k.Setzen(new Element
            {
                Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("GEB"), Klasse = "Gebaeude",
                Name = IfcLauf.Text(b?.LongName) ?? IfcLauf.Text(b?.Name) ?? "Gebäude",
                Kennungen = b is null ? null : new() { ["ifc"] = b.GlobalId.ToString(), ["quelle"] = unterlageId },
            });
        }
        var kurz = Kurznamen(ifcG);
        var vergeben = k.Arbeit.Elemente.Select(e => e.Kennzeichen).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ifcG.Count; i++)
        {
            var g = ifcG[i];
            var da = k.Arbeit.DerKlasse("Geschoss").FirstOrDefault(x => x.Zahl("kote") is double kote && Math.Abs(kote - g.KoteMm) <= 50);
            if (da is not null) { aus[g] = da.Id; continue; }
            double hoehe = i + 1 < ifcG.Count ? ifcG[i + 1].KoteMm - g.KoteMm : (i > 0 ? g.KoteMm - ifcG[i - 1].KoteMm : 3000);
            var kz = vergeben.Contains(kurz[i]) ? k.NaechstesKennzeichen("G") : kurz[i];
            vergeben.Add(kz);
            var e = new Element
            {
                Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Geschoss", Name = g.Name, TeilVon = gebaeude.Id,
                Kennungen = new() { ["ifc"] = g.GlobalId, ["quelle"] = unterlageId },
            };
            var h = k.Herkunft with { Art = "ifc", Quelle = $"{Path.GetFileName(d.Pfad)}: IfcBuildingStorey {g.Name}" };
            e.Eigenschaften["kote"] = new Wert { Inhalt = Math.Round(g.KoteMm, 1), Einheit = "mm", Herkunft = h };
            e.Eigenschaften["hoehe"] = new Wert { Inhalt = Math.Round(Math.Max(hoehe, 1), 1), Einheit = "mm", Herkunft = h };
            e.Eigenschaften["kurz"] = new Wert { Inhalt = kz, Herkunft = h };
            aus[g] = k.Setzen(e).Id;
        }
        return aus;
    }

    /// <summary>Kurznamen nach Lage zum Erdgeschoss (Kote am nächsten bei 0): 2.UG, 1.UG, EG, 1.OG …; Attika und Dach
    /// nach Namen.</summary>
    static List<string> Kurznamen(IReadOnlyList<IfcGeschoss> g)
    {
        int eg = 0;
        for (int i = 1; i < g.Count; i++) if (Math.Abs(g[i].KoteMm) < Math.Abs(g[eg].KoteMm)) eg = i;
        var aus = new List<string>();
        for (int i = 0; i < g.Count; i++)
        {
            var n = g[i].Name.ToLowerInvariant();
            aus.Add(i == eg ? "EG"
                : n.Contains("attika") ? "AT"
                : n.Contains("dach") ? "DA"
                : i < eg ? $"{eg - i}.UG" : $"{i - eg}.OG");
        }
        return aus;
    }

    static IReadOnlySet<string>? GeschossFilter(Kontext k, IfcDatei d, Dictionary<IfcGeschoss, string> ids, List<string> wahl)
    {
        if (wahl.Count == 0) return null;
        var aus = new HashSet<string>();
        foreach (var w in wahl)
        {
            // Kote in m (z. B. «3.06») – nötig, wenn Namen doppelt vorkommen (Archicad: zwei «OG», zwei «UG»; Befund C1-2)
            if (double.TryParse(w.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m))
            {
                var nachKote = ids.Where(x => Math.Abs(x.Key.KoteMm - m * 1000) <= 50).Select(x => x.Value).FirstOrDefault()
                    ?? throw new HlksFehler($"Kein Geschoss mit Kote {w} m im IFC.", "Koten im IFC: " + string.Join(", ", d.Geschosse().Select(g => $"{g.KoteMm / 1000:0.00}")));
                aus.Add(nachKote);
                continue;
            }
            var proj = k.Arbeit.DerKlasse("Geschoss").FirstOrDefault(g => g.Kennzeichen.Equals(w, StringComparison.OrdinalIgnoreCase) || (g.Name ?? "").Equals(w, StringComparison.OrdinalIgnoreCase));
            var ifc = ids.Where(x => x.Key.Name.Equals(w, StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).ToList();
            if (proj is null && ifc.Count > 1)
                throw new HlksFehler($"Geschoss «{w}» kommt im IFC mehrfach vor.", "Geschoss über die Kote in m wählen: " +
                    string.Join(", ", ids.Where(x => x.Key.Name.Equals(w, StringComparison.OrdinalIgnoreCase)).Select(x => $"{x.Key.KoteMm / 1000:0.00}")));
            var id = proj?.Id ?? ifc.FirstOrDefault() ?? throw new HlksFehler($"Geschoss «{w}» gibt es weder im Projekt noch im IFC.",
                "Geschosse im IFC: " + string.Join(", ", d.Geschosse().Select(g => $"{g.Name} ({g.KoteMm / 1000:0.00})")));
            aus.Add(id);
        }
        return aus;
    }

    sealed class Zaehler
    {
        public int Neu, Geaendert, Geloescht, Gleich;
        public Dictionary<string, int> Uebernommen { get; } = [];
        /// <summary>Vorläufige Id → echte Id.</summary>
        public Dictionary<string, string> Ids { get; } = [];
    }

    /// <summary>Gleicht die geplanten Elemente mit dem Bestand ab (Schlüssel GlobalId) und übernimmt sie in den Kontext.</summary>
    static Zaehler Abgleichen(Kontext k, IfcLauf lauf, string unterlageId, IReadOnlySet<string> klassen, bool konfliktIfc)
    {
        var z = new Zaehler();
        // Bestand nach GlobalId; Elemente aus derselben Unterlage zuerst
        var bestand = new Dictionary<string, Element>();
        foreach (var e in k.Arbeit.Elemente.Where(e => e.Kennungen?.ContainsKey("ifc") == true)
                     .OrderBy(e => e.Kennungen!.GetValueOrDefault("quelle") == unterlageId ? 0 : 1))
            bestand.TryAdd(e.Kennungen!["ifc"], e);

        // Übernahme eines bestehenden Elements (Raum aus IfcSpace statt Raum aus Wänden): nur ohne Treffer im Bestand, sonst wird
        // das bestehende ersetzt (Verweise umhängen, löschen)
        var uebernahmen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (vorl, e) in lauf.Geplant)
        {
            bool imBestand = bestand.TryGetValue(e.Kennungen!["ifc"], out var alt);
            if (lauf.Uebernimmt.TryGetValue(vorl, out var ziel) && k.Arbeit.Finde(ziel) is not null)
            {
                if (!imBestand) { z.Ids[vorl] = ziel; uebernahmen.Add(ziel); continue; }
                lauf.Ersetzt[ziel] = vorl;
            }
            z.Ids[vorl] = imBestand ? alt!.Id : Guid.NewGuid().ToString();
        }

        string? Aufloesen(string? r, Element e, string rolle)
        {
            if (r is null || !r.StartsWith("ifc:")) return r;
            if (z.Ids.TryGetValue(r, out var id)) return id;
            var gid = r[4..];
            if (bestand.TryGetValue(gid, out var b)) return b.Id;
            lauf.Hinweise.Add($"{e.Klasse} {e.Name}: Bezug «{rolle}» auf ein nicht übernommenes IFC-Objekt ({gid}) weggelassen.");
            return null;
        }

        var kennzeichen = k.Arbeit.Elemente.Select(e => e.Kennzeichen).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var naechste = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string NeuesKennzeichen(string kuerzel)
        {
            if (!naechste.TryGetValue(kuerzel, out var n))
            {
                n = 0;
                foreach (var kz in kennzeichen)
                    if (kz.StartsWith(kuerzel + "-", StringComparison.OrdinalIgnoreCase) && int.TryParse(kz[(kuerzel.Length + 1)..], out var m) && m > n) n = m;
            }
            naechste[kuerzel] = ++n;
            return $"{kuerzel}-{n}";
        }

        var gesehen = new HashSet<string>();
        foreach (var (vorl, plan) in lauf.Geplant)
        {
            var e = plan.Kopie();
            e.Id = z.Ids[vorl];
            e.Geschoss = Aufloesen(e.Geschoss, e, "geschoss");
            e.TeilVon = Aufloesen(e.TeilVon, e, "teilVon");
            e.System = Aufloesen(e.System, e, "system");
            e.Raum = Aufloesen(e.Raum, e, "raum");
            foreach (var rolle in e.Bezuege.Keys.ToList())
                if (Aufloesen(e.Bezuege[rolle], e, rolle) is { } id) e.Bezuege[rolle] = id; else e.Bezuege.Remove(rolle);
            // Verweise in den Schichten eines Aufbaus (Modul 1c, IfcHuelle): «ifc:material:…» auf die echte Id (Befund B4-1)
            if (e.Eigenschaften.TryGetValue("schichten", out var sw) && sw.Inhalt is JsonArray schichten)
                foreach (var s in schichten.OfType<JsonObject>())
                    if (s["material"] is JsonValue mv && mv.TryGetValue<string>(out var m) && m.StartsWith("ifc:") && Aufloesen(m, e, "material") is { } mid)
                        s["material"] = mid;
            gesehen.Add(e.Id);
            var alt = k.Arbeit.Finde(e.Id);
            if (alt is null)
            {
                var kuerzel = k.Katalog.Klassen.TryGetValue(e.Klasse, out var d) && d.Kuerzel.Length > 0 ? d.Kuerzel : "E";
                e.Kennzeichen = e.Kennzeichen.Length > 0 && !kennzeichen.Contains(e.Kennzeichen) ? e.Kennzeichen : NeuesKennzeichen(kuerzel);
                kennzeichen.Add(e.Kennzeichen);
                k.Setzen(e);
                z.Neu++;
            }
            else if (uebernahmen.Contains(e.Id))
            {
                // übernommenes Element: Id bleibt, Kennzeichen aus dem IFC (Raumnummer), wenn frei
                e.Kennzeichen = e.Kennzeichen.Length > 0 && !kennzeichen.Contains(e.Kennzeichen) ? e.Kennzeichen : alt.Kennzeichen;
                kennzeichen.Add(e.Kennzeichen);
                e.Angelegt = alt.Angelegt;
                e.Paket = alt.Paket; e.Version = alt.Version;
                k.Setzen(e);
                z.Geaendert++;
            }
            else
            {
                e.Kennzeichen = alt.Kennzeichen;
                e.Angelegt = alt.Angelegt;
                e.Paket = alt.Paket; e.Version = alt.Version; e.Geaendert = alt.Geaendert;
                if (GleicherInhalt(alt, e)) { z.Gleich++; }
                else if (!konfliktIfc && InKicadGeaendert(alt))
                {
                    lauf.Hinweise.Add($"{alt.Kennzeichen} ({alt.Klasse}) wurde in KICAD geändert – die neue IFC-Fassung ist nicht übernommen (konflikte = ifc übernimmt sie).");
                    z.Gleich++;
                }
                else { k.Setzen(e); z.Geaendert++; }
            }
            z.Uebernommen[e.Klasse] = z.Uebernommen.GetValueOrDefault(e.Klasse) + 1;
        }

        // Ersetzte Elemente: Verweise auf das neue umhängen, dann löschen
        foreach (var (altId, vorl) in lauf.Ersetzt)
        {
            if (k.Arbeit.Finde(altId) is not { } weg || !z.Ids.TryGetValue(vorl, out var neuId) || neuId == altId) continue;
            foreach (var x in k.Arbeit.Elemente.Where(x => x.Raum == altId || x.TeilVon == altId || x.System == altId || x.Bezuege.ContainsValue(altId)).ToList())
            {
                var c = x.Kopie();
                if (c.Raum == altId) c.Raum = neuId;
                if (c.TeilVon == altId) c.TeilVon = neuId;
                if (c.System == altId) c.System = neuId;
                foreach (var rolle in c.Bezuege.Where(b => b.Value == altId).Select(b => b.Key).ToList()) c.Bezuege[rolle] = neuId;
                k.Setzen(c);
            }
            k.Loeschen(weg);
            z.Geloescht++;
        }

        // In der neuen Fassung fehlende Elemente derselben Unterlage löschen (nur Klassen der ausgeführten Übertrager)
        foreach (var e in k.Arbeit.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == unterlageId && klassen.Contains(e.Klasse) && !gesehen.Contains(e.Id)).ToList())
        {
            if (lauf.GeschossFilter is { } f && (e.Geschoss is null || !f.Contains(e.Geschoss))) continue;
            if (!konfliktIfc && InKicadGeaendert(e)) { lauf.Hinweise.Add($"{e.Kennzeichen} fehlt in der neuen IFC-Fassung, bleibt aber (in KICAD geändert)."); continue; }
            k.Loeschen(e);
            z.Geloescht++;
        }

        // Verbindungen (Befund B2-1): zuerst die Soll-Liste, dann alte IFC-Verbindungen dieser Unterlage trennen, die nicht
        // mehr darin stehen (auch auf weggefallene Anschlussnamen), erst dann neue anlegen – sonst scheitern sie an «belegt».
        // IFC-Verbindungen erkennt man am Befehl (Kontext.Verbinden setzt die Herkunft des Akteurs, nicht «ifc»); von Hand
        // gezogene Verbindungen zwischen übernommenen Elementen bleiben.
        static string N(string s) => s.ToLowerInvariant();
        var soll = new HashSet<(string, string, string, string)>();
        var neu = new List<(string A, string Pa, string B, string Pb, Element Ea, Element Eb)>();
        foreach (var v in lauf.Verbindungen)
        {
            if (!z.Ids.TryGetValue(v.VonRef, out var a) || !z.Ids.TryGetValue(v.NachRef, out var b)) continue;
            if (k.Arbeit.Finde(a) is not { } ea || k.Arbeit.Finde(b) is not { } eb) continue;
            if (ea.AnschlussNamens(v.VonAnschluss) is null || eb.AnschlussNamens(v.NachAnschluss) is null) continue;
            soll.Add((a, N(v.VonAnschluss), b, N(v.NachAnschluss)));
            soll.Add((b, N(v.NachAnschluss), a, N(v.VonAnschluss)));
            neu.Add((a, v.VonAnschluss, b, v.NachAnschluss, ea, eb));
        }
        var dieseUnterlage = k.Arbeit.Elemente.Where(e => e.Kennungen?.GetValueOrDefault("quelle") == unterlageId).Select(e => e.Id).ToHashSet();
        foreach (var v in k.Arbeit.Verbindungen.ToList())
        {
            if (!dieseUnterlage.Contains(v.Von.Element) || !dieseUnterlage.Contains(v.Nach.Element)) continue;
            if (v.Angelegt?.Befehl != "ifc_uebernehmen" && v.Angelegt?.Art != "ifc") continue;
            bool anschluesseDa = k.Arbeit.Finde(v.Von.Element)?.AnschlussNamens(v.Von.Anschluss) is not null
                              && k.Arbeit.Finde(v.Nach.Element)?.AnschlussNamens(v.Nach.Anschluss) is not null;
            if (!anschluesseDa || !soll.Contains((v.Von.Element, N(v.Von.Anschluss), v.Nach.Element, N(v.Nach.Anschluss)))) k.Trennen(v);
        }
        foreach (var (a, pa, b, pb, ea, eb) in neu)
        {
            // über den Anschluss-Index des Modells (VerbindungenVon durchsucht alle Verbindungen)
            var anA = k.Arbeit.VerbindungAn(a, pa);
            if (anA?.Gegenueber(a, pa) is { } g && g.Element == b && g.Anschluss.Equals(pb, StringComparison.OrdinalIgnoreCase)) continue;
            bool besetzt = anA is not null || k.Arbeit.VerbindungAn(b, pb) is not null;
            if (besetzt) { lauf.Hinweise.Add($"Verbindung {ea.Kennzeichen}.{pa} – {eb.Kennzeichen}.{pb}: Anschluss schon belegt, nicht verbunden."); continue; }
            k.Verbinden(new Anschlusspunkt(a, pa), new Anschlusspunkt(b, pb));
        }
        return z;
    }

    /// <summary>Wurde das Element nach seinem letzten IFC-Übertrag mit einem anderen Befehl geändert (letzte Änderung weder durch
    /// ifc_uebernehmen noch mit Herkunftsart ifc)? Die eine Regel «in KICAD geändert» – für den Abgleich einer neuen Fassung
    /// (konflikte = kicad) und für die Kollisionsprüfung (exaktes Netz der Netzdatei nur für unveränderte, Pruefkoerperbau).</summary>
    public static bool InKicadGeaendert(Element e) => e.Geaendert is { } g && g.Befehl != "ifc_uebernehmen" && g.Art != "ifc";

    /// <summary>Übernommenes IFC-Element (Kennungen ifc und quelle), das seit dem letzten Übertrag nicht in KICAD geändert wurde:
    /// für es gilt die exakte Geometrie der Netzdatei (entwurf/modul-1b.md: «Wird ein übernommenes Element in KICAD geändert, gilt
    /// für dieses Element nur noch die KICAD-Geometrie»).</summary>
    public static bool UnveraendertUebernommen(Element e) =>
        e.Kennungen is { } kn && kn.ContainsKey("ifc") && kn.ContainsKey("quelle") && !InKicadGeaendert(e);

    static bool GleicherInhalt(Element a, Element b)
    {
        static string Inhalt(Element e)
        {
            var x = e.Kopie();
            x.Version = 0; x.Paket = null; x.Geaendert = null; x.Angelegt = null;
            foreach (var w in x.Eigenschaften.Values) w.Herkunft = new Herkunft();
            return Json.Text(x);
        }
        return Inhalt(a) == Inhalt(b);
    }

    static double[]? Umriss(IEnumerable<Element> elemente)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var e in elemente)
            foreach (var pt in e.Geometrie?.Punkte ?? [])
            { x0 = Math.Min(x0, pt[0]); y0 = Math.Min(y0, pt[1]); x1 = Math.Max(x1, pt[0]); y1 = Math.Max(y1, pt[1]); }
        return x0 == double.MaxValue ? null : [x0, y0, Math.Max(x1, x0 + 1), Math.Max(y1, y0 + 1)];
    }
}
