// BIM to field (entwurf/bim-to-field.md, Stufe 1): das Modell als Webseite für die Baustelle. Eine einzige HTML-Datei, die alles enthält –
// läuft offline vom USB-Stick, als Mailanhang und von jedem Webspace (Browser blockieren lokale Nachbardateien, darum nicht als Ordner).
// Inhalt: Kopf mit Stand und Freigabe, Elemente mit Angaben für die Montage, Grundrisse/Schnitte/Schemas als SVG (PlanSvg), 3D je Geschoss als
// GLB mit Architekten-IFC als Hintergrund (XrModell, Teile «modell» und «unterlage»), Material je Element für den Auszug nach beliebiger Auswahl.
// Entscheide Christian 02.11.2026: Monteur nur freigegebene Stände, Bauleitung (Projekt- und Bauleiter) auch Arbeitsstände mit Vermerk;
// Architekten-IFC immer dabei. Freigeben tut der Mensch: die KI darf nicht als «freigegeben» exportieren (dazu Verbotsliste der KI-Palette).
// Ein Export ist eine Ausgabe an Dritte und kommt ins Ausgabeprotokoll.

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class BaustelleExport
{
    public const string AbfrageText = "BIM to field (Stufe 1): schreibt das Modell als eine Webseite (HTML-Datei, offline lauffähig) für die Baustelle – 3D je " +
        "Geschoss mit Architekten-IFC, Grundrisse und Schemas, Elementinfo (Dimension, Material, Höhe, System, Artikel), Suche und Filter, Materialauszug " +
        "nach Auswahl; Kopf mit Projekt, Rolle, Stand und Freigabe. rolle monteur (Vorgabe, nur freigegebener Stand) | bauleitung (auch Arbeitsstand). " +
        "Freigeben tut der Mensch. Kommt ins Ausgabeprotokoll.";
    public const string ParDatei = "Zieldatei (.html); ohne: Projektordner ausgabe\\baustelle\\<Projekt>_<rolle>_<Zeit>.html";
    public const string ParRolle = "monteur (Vorgabe) | bauleitung";
    public const string ParStand = "freigegeben (Vorgabe) | arbeitsstand (nur Rolle bauleitung)";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("baustelle_export", AbfrageText,
            [new("datei", "text", false, ParDatei), new("rolle", "text", false, ParRolle), new("stand", "text", false, ParStand)]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "baustelle_export" ? Schreiben(p, par) : null;

    static readonly string[] Rollen = ["monteur", "bauleitung"];
    static readonly HashSet<string> OhneElement = ["Ansicht", "Planblatt", "Projektinformation"];
    static readonly HashSet<string> Planarten = ["grundriss", "schnitt", "ansicht", "schema"];

    public static JsonObject Schreiben(Projekt p, JsonObject par)
    {
        var rolle = (Param.Text(par, "rolle")?.Trim().ToLowerInvariant()) ?? "monteur";
        if (!Rollen.Contains(rolle)) throw new HlksFehler($"Rolle «{rolle}» gibt es nicht.", "rolle: " + string.Join(" | ", Rollen));
        var stand = (Param.Text(par, "stand")?.Trim().ToLowerInvariant()) ?? "freigegeben";
        if (stand is not ("freigegeben" or "arbeitsstand")) throw new HlksFehler($"Stand «{stand}» gibt es nicht.", "stand: freigegeben | arbeitsstand");
        if (rolle == "monteur" && stand == "arbeitsstand")
            throw new HlksFehler("Der Monteur erhält nur freigegebene Stände (Entscheid Christian 02.11.2026).", "stand: freigegeben oder rolle: bauleitung.");
        if (stand == "freigegeben" && p.AkteurArt == "ki")
            throw new HlksFehler("Freigeben für die Baustelle tut der Mensch.", "Als Arbeitsstand für die Bauleitung exportieren oder den Menschen bitten.");

        var zeit = DateTimeOffset.Now;
        string pfad;
        if (Param.Text(par, "datei") is { } roh && roh.Trim().Trim('"') is { Length: > 0 } datei)
        {
            if (!string.Equals(Path.GetExtension(datei), ".html", StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"Die Zieldatei «{datei}» braucht die Endung .html.", "datei z. B. «…\\Baustelle.html» angeben oder leer lassen.");
            pfad = Path.GetFullPath(datei);
        }
        else if (p.Speicher is not null)
            pfad = Path.Combine(p.Speicher.Ort, "ausgabe", "baustelle", $"{Sauber(p.Kopf.Name)}_{rolle}_{zeit:yyyyMMdd-HHmm}.html");
        else throw new HlksFehler("Projekt ohne Ablage – Zieldatei fehlt.", "datei angeben (vollständiger Pfad mit .html).");

        var hinweise = new List<string>();
        var daten = Daten(p, rolle, stand, zeit, hinweise);
        var html = Html(daten);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, html, new UTF8Encoding(false));
        if (Ausgabeprotokoll.Vermerken(p, "baustelle_export", pfad) is { } h) hinweise.Add(h);
        return new JsonObject
        {
            ["datei"] = pfad, ["groesseBytes"] = new FileInfo(pfad).Length, ["rolle"] = rolle, ["stand"] = stand,
            ["elemente"] = daten["elemente"]!.AsArray().Count, ["ansichten"] = daten["ansichten"]!.AsArray().Count,
            ["geschosse3d"] = daten["modell3d"]!["geschosse"]!.AsArray().Count,
            ["hinweise"] = new JsonArray(hinweise.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
        };
    }

    static string Sauber(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')).Trim('_') is { Length: > 0 } x ? x : "Projekt";

    // ------------------------------------------------------------------ Daten

    public static JsonObject Daten(Projekt p, string rolle, string stand, DateTimeOffset zeit, List<string> hinweise)
    {
        var m = p.Modell; var k = p.Katalog;
        var st = p.Stand;
        var kopf = new JsonObject
        {
            ["projekt"] = p.Kopf.Name, ["rolle"] = rolle, ["stand"] = stand,
            ["freigegebenVon"] = stand == "freigegeben" ? p.Akteur : null,
            ["erstellt"] = zeit.ToString("dd.MM.yyyy HH:mm"), ["modellPruefsumme"] = st.Pruefsumme[..Math.Min(12, st.Pruefsumme.Length)], ["letztesPaket"] = st.LetztesPaket,
        };
        string? Kz(string? id) => id is null ? null : m.Finde(id)?.Kennzeichen;
        var geschosse = m.DerKlasse("Geschoss").OrderBy(g => g.Zahl("kote") ?? 0).ToList();

        var elemente = new JsonArray();
        foreach (var e in m.Elemente.OrderBy(e => e.Kennzeichen, StringComparer.Ordinal))
        {
            if (OhneElement.Contains(e.Klasse) || k.Klassen.TryGetValue(e.Klasse, out var d) && d.BrauchtAnsicht) continue;
            var eig = new JsonObject();
            foreach (var (name, w) in e.Eigenschaften.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (w.Inhalt is null) continue;
                var text = w.Inhalt.ToJsonString();
                if (text.Length > 400) continue;   // grosse Strukturen (Lagen, Listen) nicht in die Elementinfo
                eig[name] = new JsonObject { ["wert"] = w.Inhalt.DeepClone(), ["einheit"] = w.Einheit };
            }
            var o = new JsonObject
            {
                ["id"] = e.Id, ["kz"] = e.Kennzeichen, ["klasse"] = e.Klasse, ["name"] = e.Name, ["gewerk"] = Gewerk(k, e),
                ["geschoss"] = Kz(e.Geschoss), ["raum"] = Kz(e.Raum), ["system"] = Kz(e.System), ["teilVon"] = Kz(e.TeilVon), ["eig"] = eig,
            };
            if (Familien.FamilieVon(m, e) is { } fam) { o["familie"] = fam.Name ?? fam.Kennzeichen; o["typ"] = Familien.Typ(e); }
            if (e.Geometrie?.Punkte is { Count: > 0 } pk && pk.All(q => q.Length >= 3))
            {
                o["zMin"] = Math.Round(pk.Min(q => q[2])); o["zMax"] = Math.Round(pk.Max(q => q[2]));
                if (m.Finde(e.Geschoss)?.Zahl("kote") is double kote) o["kote"] = kote;
            }
            if (Material(p, e) is { } mat) o["mat"] = mat;
            elemente.Add(o);
        }

        var ansichten = new JsonArray();
        foreach (var a in m.DerKlasse("Ansicht").Where(a => Planarten.Contains(a.Text("art") ?? "grundriss") && !(a.Eigenschaft("vorlage")?.Inhalt is JsonValue vw && vw.TryGetValue<bool>(out var vb) && vb))
                     .OrderBy(a => a.Kennzeichen, StringComparer.Ordinal))
        {
            try
            {
                var g = Grafik.Plan(p, a.Kennzeichen);
                if (g.Teile.Count == 0) continue;
                ansichten.Add(new JsonObject { ["kz"] = a.Kennzeichen, ["name"] = a.Name ?? a.Kennzeichen, ["art"] = g.Art, ["svg"] = PlanSvg.Svg(g) });
            }
            catch (Exception ex) when (ex is HlksFehler or InvalidOperationException) { hinweise.Add($"Ansicht {a.Kennzeichen} nicht exportiert: {ex.Message}"); }
        }

        // 3D: dieselben GLB wie die Quest App, je Geschoss Modell und Unterlage (Architekten-IFC)
        var xr = XrModell.Stand(p);
        var geschosse3d = new JsonArray();
        foreach (var gs in xr.Geschosse)
        {
            var g = new JsonObject
            {
                ["kz"] = gs.Geschoss.Kennzeichen, ["name"] = gs.Geschoss.Name, ["koteMm"] = gs.Geschoss.KoteMm,
                ["modell"] = Convert.ToBase64String(XrModell.Glb(xr, gs.Geschoss.Id, XrModell.TeilModell)),
            };
            if (gs.SchluesselUnterlage is not null) g["unterlage"] = Convert.ToBase64String(XrModell.Glb(xr, gs.Geschoss.Id, XrModell.TeilUnterlage));
            geschosse3d.Add(g);
        }
        hinweise.AddRange(xr.Hinweise.Take(10));
        var modell3d = XrModell.Liste(xr, (_, _) => "");
        modell3d["geschosse"] = geschosse3d;

        return new JsonObject
        {
            ["kopf"] = kopf,
            ["geschosse"] = new JsonArray(geschosse.Select(g => (JsonNode)new JsonObject { ["kz"] = g.Kennzeichen, ["name"] = g.Name, ["kote"] = g.Zahl("kote") }).ToArray()),
            ["elemente"] = elemente, ["ansichten"] = ansichten, ["modell3d"] = modell3d,
        };
    }

    static string? Gewerk(Katalog k, Element e) => k.Klassen.TryGetValue(e.Klasse, out var d) ? d.Gewerk : null;

    /// <summary>Angaben für den Materialauszug wie die Materialliste (Modul 3 B4): Leitung = Serie, Grösse, Länge; Formteil und übrige
    /// Bauteile mit Familie = Familie, Typ, Stück; Apparate ohne Familie = Klasse, Stück. Räume, Architektur und Gliederung: keine.</summary>
    static JsonObject? Material(Projekt p, Element e)
    {
        var k = p.Katalog;
        if (k.IstArt(e, "Leitung"))
        {
            var b = HlksMasse.Bezeichnung(p, e);
            return new JsonObject { ["art"] = "leitung", ["serie"] = b.Serie, ["groesse"] = b.Groesse, ["laengeM"] = Math.Round((e.Geometrie?.Laenge() ?? 0) / 1000, 3) };
        }
        if (!k.Klassen.TryGetValue(e.Klasse, out var d) || d.Schicht != "system" || e.Geometrie is null) return null;
        var fam = Familien.FamilieVon(p.Modell, e);
        return new JsonObject
        {
            ["art"] = k.IstArt(e, "Formteil") ? "formteil" : "stueck", ["familie"] = fam?.Name ?? fam?.Kennzeichen ?? e.Klasse,
            ["typ"] = fam is null ? (HlksEinordnung.Dn(e) is double dn ? $"DN {dn:0}" : null) : FreieGroesse.Text(p, e) ?? Familien.Typ(e),
        };
    }

    // ------------------------------------------------------------------ HTML

    static string Ressource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hlks.Kern.Baustelle." + name)
            ?? throw new InvalidOperationException($"Ressource {name} fehlt im Kern.");
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    /// <summary>Die Webseite: Vorlage mit three.js (MIT), OrbitControls, Betrachter und Daten in einem Modul-Skript (inline, damit es auch von
    /// file:// läuft). OrbitControls importiert aus «three» – im gemeinsamen Modul sind die Namen schon da, die Import-Zeilen fallen weg.</summary>
    public static string Html(JsonObject daten)
    {
        var three = Ressource("three.module.js");
        var orbit = Ressource("OrbitControls.js");
        var importEnde = orbit.IndexOf("} from 'three';", StringComparison.Ordinal);
        if (orbit.StartsWith("import {", StringComparison.Ordinal) && importEnde > 0) orbit = orbit[(importEnde + "} from 'three';".Length)..];
        orbit = orbit.Replace("export { OrbitControls };", "");
        three = three[..three.LastIndexOf("export {", StringComparison.Ordinal)];
        // OrbitControls in einem eigenen Gültigkeitsbereich: seine Hilfsnamen (_ray, _plane …) gibt es in three.js schon
        var skript = three + "\nconst OrbitControls = (() => {\n" + orbit + "\nreturn OrbitControls;\n})();\n" + Ressource("betrachter.js");
        // JSON mit escapten < > & (Standard-Encoder) – sicher im <script>
        return Ressource("betrachter.html")
            .Replace("/*KICAD_STIL*/", Ressource("betrachter.css"))
            .Replace("/*KICAD_DATEN*/", daten.ToJsonString())
            .Replace("/*KICAD_SKRIPT*/", skript.Replace("</script", "<\\/script"))
            .Replace("<!--KICAD_LIZENZ-->", "<!-- three.js r160, MIT-Lizenz: " + Ressource("LICENSE").Replace("--", "- -") + " -->");
    }
}
