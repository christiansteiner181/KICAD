// IFC-Export des Modells mit IDS-Prüfung und Exportbericht (Modul 7 Teil B2, entwurf/modul-7.md; Entscheide Christian 20.10.2026:
// A1 Räume nur auf Wunsch, A2 Dämmung als eigene Geometrie, A3 Ablage im Projektordner ausgabe\ifc, A4 eigenes Pset HK_KICAD).
// Abfrage ifc_export – wie plan_pdf, bcf_export und aussparungen_ifc eine Abfrage, die eine Datei schreibt und das Modell nicht ändert.
//
// Ablauf: Parameter prüfen → Beschreibung (IfcExportDaten.cs) → Schreiben in eine Hilfsdatei (IfcExportSchreiber.cs) → die Datei mit dem
// eigenen Leser (IfcDatei) wieder öffnen: Kontrolle (jedes Element mit seiner GlobalId und Entität da), Schemaprüfung (Xbim-Validierung
// jeder Entität: Pflichtattribute, Typen, WHERE-Regeln), IDS (kataloge/ids/hk-hlks-basis.ids und die angegebenen) → Zieldatei ersetzen →
// Bericht daneben (<Name>.bericht.json, <Name>.bericht.md).
// Ziel ohne Angabe: <Projektordner>\ausgabe\ifc\<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc (ohne Ablage %TEMP%\HLKS_CAD\ausgabe\<Projekt-Id>\ifc).
// Koordinaten «gemeinsam» wie die Aussparungsausgabe: hat das Projekt einen IFC-Bezug (Projektinformation.ifcBezug), steht das Gebäude im
// Bezug (Welt der Architekten-IFC), sonst im Ursprung; IFC4 mit IfcMapConversion, wenn der Bezug Landeskoordinaten mit Bezugssystem führt.
// GlobalId: UUID v5 im KICAD-Namensraum aus «<Projekt-Id>/element/<Element-Id>» (Ports «port/<Element-Id>/<Anschluss>», Dämmung
// «daemmung/<Element-Id>», Typobjekte «typ/<Schlüssel>», Beziehungen «rel/…») – gleich über Exporte und Fassungen.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Xbim.Common;
using Xbim.Common.ExpressValidation;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public static class IfcExportTexte
{
    public const string Export =
        "Exportiert das HLKS-Modell als IFC4 (Vorgabe) oder IFC2X3: nur Elemente, die KICAD führt (keine Grundlagen, keine unverändert aus fremden IFC " +
        "übernommenen Elemente), Gliederung Projekt/Gelände/Gebäude/Geschosse (mm, Koten; gemeinsame Koordinaten über den IFC-Bezug, IFC4 mit " +
        "IfcMapConversion), Entität/Typobjekt/PredefinedType nach kataloge/ifc.json, Leitungen als Extrusion mit Aussendurchmesser bzw. b × h, Dämmung als " +
        "IfcCovering, Formteile und Apparate aus den Familienkörpern (sonst Hüllquader), Ports mit Fliessrichtung und Nennweite, Verbindungen, Systeme " +
        "(IfcDistributionSystem), Stränge (IfcDistributionCircuit, nur IFC4), Anlagen als IfcSystem-Gruppe, Psets nach ifc.json, Länge in Qto, " +
        "Klassifikation BKP und Pset HK_KICAD. GlobalIds je Element fest. Danach Schemaprüfung (Xbim) und IDS-Prüfung (kataloge/ids/hk-hlks-basis.ids und " +
        "weitere). Ohne datei: <Projektordner>\\ausgabe\\ifc\\<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc. Bericht daneben (.bericht.json, .bericht.md). " +
        "Ändert das Modell nicht. Antwort: Exportbericht (Zähler je Klasse und Entität, Übersprungene mit Grund, Schemaprüfung, IDS je Regel, Dauer).";
    public const string ParDatei = "Zieldatei (.ifc), eine vorhandene wird ersetzt; leer = <Projektordner>\\ausgabe\\ifc\\<Projekt>_<Fassung>_<JJJJMMTT-hhmm>.ifc";
    public const string ParFassung = "IFC4 (Vorgabe) | IFC2X3 (für ältere Programme)";
    public const string ParUmfang = "Auswahl { gewerke: [heizung, lueftung, sanitaer, …], geschosse: [Kennzeichen oder Name], klassen: [Klasse oder Basisklasse, z. B. Leitung, Apparat] }; leer = alles";
    public const string ParRaeume = "Räume (IfcSpace) und Zonen mit ausgeben (Vorgabe false); gilt auch bei einem Gewerke- oder Klassenfilter, der Geschossfilter bleibt";
    public const string ParIds = "Weitere IDS-Dateien (Pfade, IDS 1.0), z. B. vom Bauherrn oder Architekten; die IDS-Basis von H+K wird immer geprüft";
}

public static class IfcExportAbfragen
{
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("ifc_export", IfcExportTexte.Export,
        [
            new("datei", "text", false, IfcExportTexte.ParDatei),
            new("fassung", "text", false, IfcExportTexte.ParFassung),
            new("umfang", "objekt", false, IfcExportTexte.ParUmfang),
            new("raeume", "bool", false, IfcExportTexte.ParRaeume),
            new("ids", "liste", false, IfcExportTexte.ParIds),
        ]),
    ];

    /// <summary>Beantwortet die Abfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "ifc_export" ? IfcExport.Schreiben(p, par) : null;
}

public static class IfcExport
{
    /// <summary>Eigenes Pset (Entscheid A4; kein «Pset_» – dieser Vorsatz gehört buildingSMART).</summary>
    public const string PsetKicad = "HK_KICAD";
    /// <summary>Pset am Typobjekt (Typname, Klasse, Familie, Typ) – zeigt das Typobjekt auch in der IDS-Prüfung.</summary>
    public const string PsetTyp = "HK_KICAD_Typ";
    /// <summary>Ausgabe des BKP: steht nicht in ifc.json – IFC2X3 verlangt das Attribut Edition.</summary>
    public const string KlassifikationOhneAusgabe = "ohne Angabe";
    /// <summary>Die eigene IDS-Basis im Katalog.</summary>
    public const string IdsBasis = "hk-hlks-basis.ids";

    /// <summary>Name der Mengen nach IFC4: Qto_&lt;Entität ohne Ifc&gt;BaseQuantities (IfcPipeSegment → Qto_PipeSegmentBaseQuantities).</summary>
    public static string QtoName(string entitaetIfc4) => "Qto_" + (entitaetIfc4.StartsWith("Ifc", StringComparison.Ordinal) ? entitaetIfc4[3..] : entitaetIfc4) + "BaseQuantities";

    /// <summary>Ordner der IFC-Ausgabe des Projekts: &lt;Projektordner&gt;\ausgabe\ifc (Entscheid A3), ohne Ablage im Temp-Ordner.</summary>
    public static string Ausgabeordner(Projekt p) =>
        p.Speicher?.Ort is { Length: > 0 } ort ? Path.Combine(ort, "ausgabe", "ifc") : Path.Combine(Path.GetTempPath(), "HLKS_CAD", "ausgabe", p.Kopf.Id, "ifc");

    /// <summary>Vorgeschlagener Dateiname &lt;Projekt&gt;_&lt;Fassung&gt;_&lt;JJJJMMTT-hhmm&gt;.ifc.</summary>
    public static string Dateiname(Projekt p, string fassung, DateTimeOffset zeit) =>
        Planausgabe.DateinameSicher($"{(string.IsNullOrWhiteSpace(p.Kopf.Name) ? "KICAD" : p.Kopf.Name.Trim())}_{fassung}_{zeit.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}") + ".ifc";

    /// <summary>Pfad der IDS-Basis (kataloge/ids/hk-hlks-basis.ids) oder null.</summary>
    public static string? IdsBasisPfad(Katalog k)
    {
        var p = Path.Combine(k.Ordner, "ids", IdsBasis);
        return File.Exists(p) ? p : null;
    }

    public static JsonObject Schreiben(Projekt p, JsonObject par)
    {
        var uhr = Stopwatch.StartNew();
        var fassung = AussparungIfcExport.FassungLesen(Param.Text(par, "fassung"));
        var umfang = Umfang(p, par);
        var idsDateien = Liste(par, "ids") ?? [];
        foreach (var f in idsDateien)
            if (!File.Exists(f)) throw new HlksFehler($"Die IDS-Datei «{f}» gibt es nicht.", "Vollständigen Pfad einer IDS-Datei (IDS 1.0, .ids) angeben.");
        var zeit = DateTimeOffset.Now;
        string pfad;
        if (Param.Text(par, "datei") is { } roh && roh.Trim().Trim('"') is { Length: > 0 } datei)
        {
            if (!string.Equals(Path.GetExtension(datei), ".ifc", StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"Die Zieldatei «{datei}» braucht die Endung .ifc.", "datei z. B. «…\\HLKS_IFC4.ifc» angeben oder leer lassen (Projektordner ausgabe\\ifc).");
            try { pfad = Path.GetFullPath(datei); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { throw new HlksFehler($"Der Pfad «{datei}» ist ungültig: {ex.Message}", "Vollständigen Pfad mit Endung .ifc angeben."); }
        }
        else pfad = Path.Combine(Ausgabeordner(p), Dateiname(p, fassung, zeit));

        // Koordinaten: Projektbezug (gemeinsame Koordinaten), IFC4 dazu der Kartenbezug
        var bezugJson = p.Modell.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcBezug")?.Inhalt as JsonObject;
        var bezug = IfcBezug.Aus(bezugJson);
        var hinweise = new List<string>();
        IfcExKarte? karte = null;
        if (bezug is not null && bezugJson?["landes"] is JsonObject landes)
        {
            if (Json.Zahl(landes["ost"]) is double ost && Json.Zahl(landes["nord"]) is double nord && Json.Zahl(landes["hoehe"]) is double hoehe
                && landes["bezugssystem"]?.GetValue<string>() is { Length: > 0 } crs)
            {
                double theta = (Json.Zahl(landes["nordwinkel"]) ?? 0) - bezug.Drehung, w = theta * Math.PI / 180;
                karte = new IfcExKarte(crs, ost * 1000 - (bezug.X * Math.Cos(w) - bezug.Y * Math.Sin(w)), nord * 1000 - (bezug.X * Math.Sin(w) + bezug.Y * Math.Cos(w)),
                    hoehe * 1000 - bezug.Z, theta);
                if (fassung == AussparungIfcExport.Fassung2x3) hinweise.Add($"Landeskoordinaten ({crs}) nur in IFC4 (IfcMapConversion) – IFC2X3 kennt sie nicht.");
            }
            else hinweise.Add("Der Projektbezug nennt Landeskoordinaten ohne Bezugssystem – kein IfcMapConversion (IfcProjectedCRS braucht einen Namen).");
        }

        var (aus, aufbau) = IfcExportAufbau.Erstellen(p, fassung, umfang, bezug, karte);
        if (aus.Produkte.Count == 0)
            throw new HlksFehler(aufbau.Uebersprungen.Count > 0
                    ? $"Nichts zu exportieren: alle {aufbau.Uebersprungen.Count} Elemente sind ausgelassen ({string.Join("; ", aufbau.Uebersprungen.GroupBy(u => u.Grund).Take(3).Select(g => $"{g.Key}: {g.Count()}"))})."
                    : "Das Projekt hat keine Elemente für den IFC-Export.",
                "Umfang (gewerke, geschosse, klassen) lockern bzw. zuerst Elemente anlegen.");

        // schreiben, zurücklesen, prüfen, dann an den Zielort
        var ordner = Path.GetDirectoryName(pfad)!;
        string? temp = null;
        int entitaeten;
        JsonObject kontrolle, schema;
        var idsErgebnisse = new JsonArray();
        var zeiten = new JsonObject();
        long bytes;
        double sekundenSchreiben, sekundenPruefen;
        try
        {
            Directory.CreateDirectory(ordner);
            temp = Path.Combine(ordner, "." + Path.GetFileNameWithoutExtension(pfad) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp.ifc");
            entitaeten = IfcExportSchreiber.Schreiben(aus, temp, Path.GetFileName(pfad));
            sekundenSchreiben = uhr.Elapsed.TotalSeconds;
            using (var d = IfcDatei.Oeffnen(temp))
            {
                zeiten["oeffnen"] = Math.Round(uhr.Elapsed.TotalSeconds - sekundenSchreiben, 2);
                kontrolle = Kontrolle(aus, d);
                var t0 = uhr.Elapsed.TotalSeconds;
                schema = Validieren(d);
                zeiten["schemapruefung"] = Math.Round(uhr.Elapsed.TotalSeconds - t0, 2);
                t0 = uhr.Elapsed.TotalSeconds;
                var namen = Kennzeichen(aus);
                var pruefer = new IdsPruefer(d, x => x is IIfcRoot r ? namen.GetValueOrDefault(r.GlobalId.ToString()) : null);
                var liste = new List<string>();
                if (IdsBasisPfad(p.Katalog) is { } basis) liste.Add(basis);
                else hinweise.Add($"IDS-Basis kataloge/ids/{IdsBasis} nicht gefunden – nur die angegebenen IDS geprüft.");
                liste.AddRange(idsDateien.Select(Path.GetFullPath));
                foreach (var ids in liste)
                {
                    var doc = IdsLeser.Lesen(ids, p.Katalog);
                    var erg = doc.Gueltig ? pruefer.Pruefen(doc) : new JsonObject
                    {
                        ["datei"] = ids, ["titel"] = doc.Titel, ["xsd"] = "ungültig", ["ergebnis"] = "IDS ungültig",
                        ["xsdMeldungen"] = new JsonArray(doc.XsdMeldungen.Take(20).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
                        ["regeln"] = new JsonArray(),
                    };
                    erg["basis"] = ids == liste[0] && IdsBasisPfad(p.Katalog) is not null;
                    idsErgebnisse.Add(erg);
                }
                zeiten["ids"] = Math.Round(uhr.Elapsed.TotalSeconds - t0, 2);
            }
            sekundenPruefen = uhr.Elapsed.TotalSeconds - sekundenSchreiben;
            File.Move(temp, pfad, overwrite: true);
            temp = null;
            bytes = new FileInfo(pfad).Length;
            Ausgabeprotokoll.Vermerken(p, "ifc_export", pfad);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new HlksFehler($"Die Datei «{pfad}» kann nicht geschrieben werden: {ex.Message}", "Anderen Pfad angeben bzw. die Datei in anderen Programmen schliessen.");
        }
        finally
        {
            if (temp is not null) try { File.Delete(temp); } catch (Exception) { }
        }

        hinweise.AddRange(aufbau.Hinweise);
        var bericht = IfcExportBericht.Json(p, aus, aufbau, umfang, pfad, bytes, entitaeten, kontrolle, schema, idsErgebnisse, hinweise, aufbau.Warnungen,
            uhr.Elapsed.TotalSeconds, sekundenSchreiben, sekundenPruefen);
        foreach (var (k, v) in zeiten) bericht["dauer"]![k] = v?.DeepClone();
        var basisName = Path.Combine(ordner, Path.GetFileNameWithoutExtension(pfad));
        var json = basisName + ".bericht.json"; var md = basisName + ".bericht.md";
        bericht["bericht"] = new JsonObject { ["json"] = json, ["md"] = md };
        try
        {
            File.WriteAllText(json, bericht.ToJsonString(Json.Eingerueckt), new System.Text.UTF8Encoding(false));
            File.WriteAllText(md, IfcExportBericht.Markdown(bericht), new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HlksFehler($"Der Bericht neben «{pfad}» kann nicht geschrieben werden: {ex.Message}", "Ordner und Rechte prüfen; die IFC-Datei ist geschrieben.");
        }
        return bericht;
    }

    // ------------------------------------------------------------------ Parameter

    static IfcExUmfang Umfang(Projekt p, JsonObject par)
    {
        var raeume = Param.Bool(par, "raeume");
        if (par["umfang"] is null) return new IfcExUmfang { Raeume = raeume };
        if (par["umfang"] is not JsonObject u)
            throw new HlksFehler("Parameter «umfang» muss ein Objekt sein.", "Beispiel: { \"gewerke\": [\"sanitaer\"], \"geschosse\": [\"EG\"], \"klassen\": [\"Leitung\"] }");
        foreach (var (k, _) in u)
            if (k is not ("gewerke" or "geschosse" or "klassen"))
                throw new HlksFehler($"umfang.{k} gibt es nicht.", "Erlaubt: gewerke, geschosse, klassen");
        HashSet<string>? gewerke = null;
        if (Liste(u, "gewerke") is { Count: > 0 } gw)
        {
            var bekannt = p.Katalog.Klassen.Values.Select(d => d.Gewerk).OfType<string>().Concat(p.Katalog.Systemtypen.Values.Select(s => s.Gewerk))
                .Concat(p.Katalog.Gewerke.Select(g => g.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            gewerke = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in gw)
            {
                var n = KaelteAlias.Gewerk(g.Trim().ToLowerInvariant());
                if (!bekannt.Contains(n)) throw new HlksFehler($"Gewerk «{g}» gibt es nicht.", "Gewerke: " + string.Join(", ", bekannt.Order()));
                gewerke.Add(n);
            }
        }
        HashSet<string>? geschosse = null;
        if (Liste(u, "geschosse") is { Count: > 0 } gs)
        {
            geschosse = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in gs)
            {
                var e = p.Modell.DerKlasse("Geschoss").FirstOrDefault(x => x.Kennzeichen.Equals(g, StringComparison.OrdinalIgnoreCase) || (x.Name ?? "").Equals(g, StringComparison.OrdinalIgnoreCase))
                        ?? throw new HlksFehler($"Geschoss «{g}» gibt es nicht.", "Geschosse: " + string.Join(", ", p.Modell.DerKlasse("Geschoss").Select(x => x.Kennzeichen)));
                geschosse.Add(e.Id);
            }
        }
        List<string>? klassen = null;
        if (Liste(u, "klassen") is { Count: > 0 } kl)
        {
            klassen = [];
            foreach (var k in kl)
            {
                if (!p.Katalog.Klassen.TryGetValue(k.Trim(), out var d))
                    throw new HlksFehler($"Klasse «{k}» gibt es nicht.", "Klassen oder Basisklassen aus dem Katalog, z. B. Leitung, Rohr, Formteil, Armatur, Apparat, Wand.");
                klassen.Add(d.Name);
            }
        }
        return new IfcExUmfang { Gewerke = gewerke, GeschossIds = geschosse, Klassen = klassen, Raeume = raeume };
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
            return text.Split(name == "ids" ? ';' : ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        throw new HlksFehler($"Parameter «{name}» muss eine Liste sein.");
    }

    // ------------------------------------------------------------------ Prüfungen

    /// <summary>GlobalId → Kennzeichen (Produkte, Gruppen, Ports «Element.Anschluss», Dämmung).</summary>
    static Dictionary<string, string> Kennzeichen(IfcExAusgabe aus)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var x in aus.Produkte)
        {
            d[x.GlobalId] = x.Element.Kennzeichen;
            foreach (var pt in x.Ports) d[pt.GlobalId] = $"{x.Element.Kennzeichen}.{pt.Name}";
            if (x.Daemmung is { } dm) d[dm.GlobalId] = $"Dämmung {x.Element.Kennzeichen}";
        }
        foreach (var g in aus.Gruppen) d[g.GlobalId] = g.Element.Kennzeichen;
        foreach (var g in aus.Geschosse) d[g.GlobalId] = g.Kennzeichen;
        return d;
    }

    /// <summary>Jedes Produkt (GlobalId, Entität) und jede Gruppe in der geschriebenen Datei; Fassung und Einheit mm.</summary>
    static JsonObject Kontrolle(IfcExAusgabe aus, IfcDatei d)
    {
        var abw = new List<string>();
        if (d.Schema != aus.Fassung) abw.Add($"Fassung {d.Schema} statt {aus.Fassung}");
        if (Math.Abs(d.MmJeEinheit - 1) > 1e-9) abw.Add($"Längeneinheit {d.MmJeEinheit} mm statt Millimeter");
        var jeGid = new Dictionary<string, IPersistEntity>(StringComparer.Ordinal);
        foreach (var r in d.Modell.Instances.OfType<IIfcRoot>())
            if (!jeGid.TryAdd(r.GlobalId.ToString(), r)) abw.Add($"GlobalId {r.GlobalId} doppelt (#{r.EntityLabel})");
        int produkte = 0, ports = 0, gruppen = 0;
        foreach (var x in aus.Produkte)
        {
            if (!jeGid.TryGetValue(x.GlobalId, out var e)) { abw.Add($"{x.Element.Kennzeichen}: fehlt in der Datei"); continue; }
            if (!e.ExpressType.Name.Equals(x.Entitaet, StringComparison.OrdinalIgnoreCase)) abw.Add($"{x.Element.Kennzeichen}: {e.ExpressType.Name} statt {x.Entitaet}");
            produkte++;
            foreach (var pt in x.Ports) if (jeGid.ContainsKey(pt.GlobalId)) ports++; else abw.Add($"{x.Element.Kennzeichen}.{pt.Name}: Port fehlt");
        }
        foreach (var g in aus.Gruppen) if (jeGid.ContainsKey(g.GlobalId)) gruppen++; else abw.Add($"{g.Element.Kennzeichen}: Gruppe fehlt");
        return new JsonObject
        {
            ["ergebnis"] = abw.Count == 0 ? "bestanden" : "abgewichen",
            ["fassungGelesen"] = d.Schema, ["einheitGelesen"] = Math.Abs(d.MmJeEinheit - 1) < 1e-9 ? "mm" : $"{d.MmJeEinheit} mm je Einheit",
            ["produkte"] = produkte, ["ports"] = ports, ["gruppen"] = gruppen,
            ["abweichungen"] = new JsonArray(abw.Take(50).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        };
    }

    /// <summary>Schemaprüfung (Xbim ExpressValidation) jeder Entität: Pflichtattribute, Typen, Mengen, WHERE-Regeln.</summary>
    static JsonObject Validieren(IfcDatei d)
    {
        int geprueft = 0, fehler = 0;
        var beispiele = new JsonArray();
        var jeArt = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var validator = new Validator { CreateEntityHierarchy = false, ValidateLevel = Xbim.Common.Enumerations.ValidationFlags.All };
        // Inverse Beziehungen (WHERE-Regeln, Inverse-Prüfung) einmal indexieren statt je Entität das ganze Modell zu durchsuchen
        using var inverse = d.Modell.BeginInverseCaching();
        foreach (var e in d.Modell.Instances)
        {
            geprueft++;
            foreach (var r in validator.Validate(e))
            {
                fehler++;
                var schluessel = $"{e.ExpressType.Name}: {r.Message}";
                jeArt[schluessel] = jeArt.GetValueOrDefault(schluessel) + 1;
                if (beispiele.Count < 20) beispiele.Add($"#{e.EntityLabel} {e.ExpressType.Name}: {r.Message} ({r.IssueSource})");
            }
        }
        return new JsonObject
        {
            ["ergebnis"] = fehler == 0 ? "fehlerfrei" : "Fehler",
            ["werkzeug"] = "Xbim.Common.ExpressValidation.Validator (Xbim.Essentials 6.1.605, ValidationFlags.All)",
            ["entitaetenGeprueft"] = geprueft, ["meldungen"] = fehler,
            ["jeArt"] = new JsonObject(jeArt.Take(30).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
            ["beispiele"] = beispiele,
        };
    }
}
