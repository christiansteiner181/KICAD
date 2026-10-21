// Feste Kataloge statt freier Texte: Klassen (was ein Element ist, welche Eigenschaften und Anschlüsse es hat),
// Medien (mit Darstellung nach SIA 410) und Systemtypen (Medium, Fliessart, wo die Verbraucher liegen).
// Die Kataloge liegen als JSON in HLKS_CAD/kataloge/ und erklären sich selbst (deutsche Beschreibungen).

using System.Text.Json;

namespace Hlks.Kern;

public sealed class EigenschaftDef
{
    public string Name { get; set; } = "";
    /// <summary>zahl | text | bool | auswahl | liste | objekt</summary>
    public string Typ { get; set; } = "text";
    public string? Einheit { get; set; }
    public string Beschreibung { get; set; } = "";
    public List<string>? Auswahl { get; set; }
    /// <summary>Anzeigenamen der Auswahlwerte (Wert → Name), z. B. Apparateart wc_75 → «Klosettanlage bis 7.5 Liter Spülwassermenge»
    /// (Modul 4, aus kataloge/sanitaer_belastungswerte.json); null = der Wert ist zugleich der Name.</summary>
    public Dictionary<string, string>? AuswahlNamen { get; set; }
    public bool Pflicht { get; set; }
}

public sealed class KlassenDef
{
    public string Name { get; set; } = "";
    public string Kuerzel { get; set; } = "";
    /// <summary>raum | huelle | system | funktion | ansicht | bezug (Achsen) | beschriftung (ansichtsgebunden) | grundlage (Unterlagen) | familie (Modul 2)</summary>
    public string Schicht { get; set; } = "";
    public string? Basis { get; set; }
    public bool Abstrakt { get; set; }
    public string Beschreibung { get; set; } = "";
    /// <summary>sanitaer | heizung | lueftung | elektro | msrl | architektur | allgemein (Kälte gehört zu heizung, Entscheid Christian 20.10.2026)</summary>
    public string? Gewerk { get; set; }
    /// <summary>Rolle im Netz: verbraucher | quelle | senke | zentrale | leitung – oder leer (Gliederung, Raum, Hülle, Funktion).</summary>
    public string? Rolle { get; set; }
    public string? Ifc { get; set; }
    public string? Bkp { get; set; }
    public Dictionary<string, string>? BkpJeGewerk { get; set; }
    /// <summary>punkt | linie | polylinie | polygon – oder leer, wenn das Element keine eigene Geometrie hat.</summary>
    public string? Geometrie { get; set; }
    public bool BrauchtGeschoss { get; set; }
    public bool BrauchtRaum { get; set; }
    public bool BrauchtSystem { get; set; }
    /// <summary>Ansichtsgebunden (Beschriftung): bezuege.ansicht muss auf eine Ansicht zeigen.</summary>
    public bool BrauchtAnsicht { get; set; }
    /// <summary>Darstellungsmasse in mm für Sinnbild und 3D-Körper, z. B. { "b": 400, "t": 600, "h": 400 } (Breite quer,
    /// Tiefe von der Wand weg, Höhe). Vererbt sich; bis Modul 2 (Familien) die einzige Quelle der Apparategrösse.</summary>
    public Dictionary<string, double>? Darstellung { get; set; }
    /// <summary>Familienfähig (Modul 2): Elemente können über bezuege.familie eine Familie tragen; der Katalog gibt ihnen
    /// dann die Eigenschaften familientyp, parameter und drehung (<see cref="Katalog.FamilienEigenschaften"/>). Vererbt sich.</summary>
    public bool Familie { get; set; }
    public List<Anschluss> Anschluesse { get; set; } = [];
    public List<EigenschaftDef> Eigenschaften { get; set; } = [];
    /// <summary>Basisklassen, von nah nach fern (vom Katalog gesetzt).</summary>
    public List<string> Linie { get; set; } = [];

    public bool IstArt(string klasse) => Name == klasse || Linie.Contains(klasse);
}

public sealed class MediumDef
{
    public string Kennung { get; set; } = "";
    public string Name { get; set; } = "";
    public string Gewerk { get; set; } = "";
    public string Farbe { get; set; } = "";
    public string FarbeHex { get; set; } = "";
    /// <summary>voll | gestrichelt | strichpunktiert</summary>
    public string Linie { get; set; } = "voll";
    public string Quelle { get; set; } = "";
}

public sealed class SystemtypDef
{
    public string Name { get; set; } = "";
    public string Medium { get; set; } = "";
    public string Gewerk { get; set; } = "";
    /// <summary>druck | schwerkraft | luft</summary>
    public string Fliessart { get; set; } = "druck";
    /// <summary>stromab: Verbraucher liegen in Fliessrichtung (Versorgung); stromauf: sie speisen ein (Entsorgung, Rücklauf).</summary>
    public string VerbraucherRichtung { get; set; } = "stromab";
    public string Beschreibung { get; set; } = "";
}

/// <summary>Ein Gewerk mit Koordinationsfarbe und Pastellton (gewerke.json; Hinterlegung im Projektbrowser). Die Farben stehen nur im Katalog.</summary>
public sealed record GewerkDef(string Id, string Titel, string Koordination, string Pastell);

public sealed class Katalog
{
    public Dictionary<string, KlassenDef> Klassen { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gewerke mit Farben in der Reihenfolge von gewerke.json; leer, wenn die Datei fehlt.</summary>
    public List<GewerkDef> Gewerke { get; } = [];
    /// <summary>Deckkraft des Pastelltons im dunklen Design (gewerke.json: dunkelDeckkraft).</summary>
    public double GewerkeDunkelDeckkraft { get; private set; } = 0.22;
    /// <summary>Medien nach Kennung; alte Kennungen (KUE-VL, SOLE-RL, HZ_VL …) finden den heutigen Eintrag (<see cref="KaelteAlias"/>).</summary>
    public Dictionary<string, MediumDef> Medien { get; } = new(KaelteAlias.MediumVergleich.Instanz);
    /// <summary>Systemtypen nach Name; alte Namen (Kühlwasser Vorlauf, Sole Rücklauf …) finden den heutigen Eintrag (<see cref="KaelteAlias"/>).</summary>
    public Dictionary<string, SystemtypDef> Systemtypen { get; } = new(KaelteAlias.SystemtypVergleich.Instanz);
    /// <summary>openBIM-Abgleich: Abbildung auf IFC4/IFC2x3 (ifc.json, Modul 1b); null, wenn die Datei fehlt.</summary>
    public IfcAbbildung? Ifc { get; private set; }
    /// <summary>Belastungswerte der Sanitärapparate (sanitaer_belastungswerte.json, Modul 4 Teil A); null, wenn die Datei fehlt.</summary>
    public SanitaerKatalog? Sanitaer { get; private set; }
    /// <summary>Schemasymbole aus Christians HKAP-Symbolbibliothek (schemasymbole.json, Modul 5 A-S 2; vom Werkzeug hkap_symbole_uebernehmen erzeugt); null, wenn die Datei fehlt.</summary>
    public SchemaSymbolKatalog? Schemasymbole { get; private set; }
    public string Ordner { get; private set; } = "";

    /// <summary>Lädt klassen.json, medien.json, systemtypen.json (und ifc.json) aus dem Ordner und löst die Basisklassen auf.</summary>
    public static Katalog Laden(string ordner)
    {
        var k = new Katalog { Ordner = ordner };
        foreach (var d in Lesen<List<KlassenDef>>(Path.Combine(ordner, "klassen.json"))) k.Klassen[d.Name] = d;
        foreach (var m in Lesen<List<MediumDef>>(Path.Combine(ordner, "medien.json"))) k.Medien[m.Kennung] = m;
        foreach (var s in Lesen<List<SystemtypDef>>(Path.Combine(ordner, "systemtypen.json"))) k.Systemtypen[s.Name] = s;
        var ifc = Path.Combine(ordner, "ifc.json");
        if (File.Exists(ifc)) k.Ifc = IfcAbbildung.Laden(ifc);
        k.GewerkeLaden(Path.Combine(ordner, "gewerke.json"));
        k.BasisAufloesen();
        var san = Path.Combine(ordner, SanitaerKatalog.Datei);
        if (File.Exists(san)) k.Sanitaer = SanitaerKatalog.Laden(san);
        var sym = Path.Combine(ordner, SchemaSymbolKatalog.Datei);
        if (File.Exists(sym)) k.Schemasymbole = SchemaSymbolKatalog.Laden(sym);
        k.SanitaerAuswahlSetzen();
        k.Pruefen();
        return k;
    }

    /// <summary>Modul 4: Die Auswahl der Eigenschaft «sanitaerArt» (Klasse Apparat) sind die Schlüssel der Apparatearten im Katalog
    /// sanitaer_belastungswerte.json, in Katalogreihenfolge, mit Namen (AuswahlNamen) – die Liste steht nur dort. Ohne die Datei bleibt
    /// die Auswahl leer (jeder Wert wird abgelehnt).</summary>
    void SanitaerAuswahlSetzen()
    {
        var arten = Sanitaer?.Arten ?? [];
        foreach (var d in Klassen.Values)
            foreach (var e in d.Eigenschaften.Where(e => e.Name == "sanitaerArt" && e.Typ == "auswahl"))
            {
                e.Auswahl = arten.Select(a => a.Schluessel).ToList();
                e.AuswahlNamen = arten.ToDictionary(a => a.Schluessel, a => a.Name);
            }
        if (Sanitaer is null) return;
        foreach (var klasse in Sanitaer.VorgabeJeKlasse.Keys)
            if (!Klassen.TryGetValue(klasse, out var kd) || !kd.IstArt("Apparat"))
                throw new HlksFehler($"Katalog {SanitaerKatalog.Datei}, vorgabeJeKlasse: «{klasse}» ist keine Klasse der Apparate in klassen.json.",
                    "Klassennamen wie in klassen.json schreiben (z. B. WC, Waschtisch).");
        // Prüfbericht 4A B5: die Medien der Excel-Spalten (SanitaerKatalog.Spalten) müssen in medien.json stehen, und ihre Systemtypen müssen
        // zur Spalte passen – LU (Versorgung): Verbraucher stromab, DU (Entwässerung): Verbraucher stromauf (systemtypen.json).
        foreach (var (medium, spalte) in SanitaerKatalog.Spalten)
        {
            if (!Medien.ContainsKey(medium))
                throw new HlksFehler($"Katalog {SanitaerKatalog.Datei}: das Medium «{medium}» der Spalte {SanitaerKatalog.SpaltenName(spalte)} fehlt in medien.json.",
                    "medien.json bzw. die Zuordnung SanitaerKatalog.Spalten nachführen.");
            var erwartet = SanitaerKatalog.Abwasser.Contains(medium) ? "stromauf" : "stromab";
            foreach (var st in Systemtypen.Values.Where(s => s.Medium.Equals(medium, StringComparison.OrdinalIgnoreCase) && s.VerbraucherRichtung != erwartet))
                throw new HlksFehler($"Katalog systemtypen.json: Systemtyp «{st.Name}» (Medium {medium}) hat verbraucherRichtung «{st.VerbraucherRichtung}», " +
                    $"die Spalte {SanitaerKatalog.SpaltenName(spalte)} der Sanitär-Belastungswerte verlangt «{erwartet}».",
                    "systemtypen.json prüfen: Trinkwasser versorgt die Apparate (stromab), Schmutzwasser nimmt ihr Abwasser auf (stromauf).");
        }
    }

    /// <summary>Sucht den Katalogordner: HLKS_KATALOG, sonst neben der Programmdatei, sonst aufwärts nach «kataloge».</summary>
    public static string StandardOrdner()
    {
        var env = Environment.GetEnvironmentVariable("HLKS_KATALOG");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                var kandidat = Path.Combine(d.FullName, "kataloge");
                if (File.Exists(Path.Combine(kandidat, "klassen.json"))) return kandidat;
                var imProjekt = Path.Combine(d.FullName, "HLKS_CAD", "kataloge");
                if (File.Exists(Path.Combine(imProjekt, "klassen.json"))) return imProjekt;
            }
        }
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "kataloge");
        if (File.Exists(Path.Combine(bekannt, "klassen.json"))) return bekannt;
        throw new HlksFehler("Katalogordner nicht gefunden.", "HLKS_KATALOG auf den Ordner HLKS_CAD/kataloge setzen.");
    }

    /// <summary>Liest gewerke.json (Objekt mit «gewerke» und «dunkelDeckkraft»); fehlt die Datei, bleibt die Liste leer (die Oberfläche färbt dann nicht).</summary>
    void GewerkeLaden(string datei)
    {
        if (!File.Exists(datei)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(datei, System.Text.Encoding.UTF8));
        var wurzel = doc.RootElement;
        if (wurzel.ValueKind != JsonValueKind.Object) throw new HlksFehler($"Katalog {datei} muss ein Objekt mit «gewerke» sein.");
        if (wurzel.TryGetProperty("dunkelDeckkraft", out var dk) && dk.ValueKind == JsonValueKind.Number) GewerkeDunkelDeckkraft = dk.GetDouble();
        if (!wurzel.TryGetProperty("gewerke", out var liste) || liste.ValueKind != JsonValueKind.Array) return;
        string Text(JsonElement e, string n) => e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
        foreach (var g in liste.EnumerateArray())
        {
            var id = Text(g, "id");
            if (id.Length == 0) continue;
            Gewerke.Add(new GewerkDef(id, Text(g, "titel") is { Length: > 0 } t ? t : id, Text(g, "koordination"), Text(g, "pastell")));
        }
    }

    static T Lesen<T>(string datei) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(datei, System.Text.Encoding.UTF8), Json.Optionen)
        ?? throw new HlksFehler($"Katalog {datei} ist leer oder ungültig.");

    void BasisAufloesen()
    {
        foreach (var d in Klassen.Values)
        {
            var linie = new List<string>();
            var eig = new List<EigenschaftDef>(d.Eigenschaften);
            var b = d.Basis;
            while (b is not null)
            {
                if (!Klassen.TryGetValue(b, out var bd)) throw new HlksFehler($"Klasse {d.Name}: Basis «{b}» fehlt im Katalog.");
                if (linie.Contains(bd.Name)) throw new HlksFehler($"Klasse {d.Name}: Basiskette ist zirkulär.");
                linie.Add(bd.Name);
                foreach (var e in bd.Eigenschaften) if (!eig.Any(x => x.Name == e.Name)) eig.Add(e);
                if (d.Anschluesse.Count == 0 && bd.Anschluesse.Count > 0) d.Anschluesse = bd.Anschluesse.Select(a => a.Kopie()).ToList();
                d.Geometrie ??= bd.Geometrie;
                d.Gewerk ??= bd.Gewerk;
                d.Rolle ??= bd.Rolle;
                d.Ifc ??= bd.Ifc;
                d.Bkp ??= bd.Bkp;
                d.BkpJeGewerk ??= bd.BkpJeGewerk;
                if (string.IsNullOrEmpty(d.Schicht)) d.Schicht = bd.Schicht;
                d.BrauchtGeschoss |= bd.BrauchtGeschoss;
                d.BrauchtRaum |= bd.BrauchtRaum;
                d.BrauchtSystem |= bd.BrauchtSystem;
                d.BrauchtAnsicht |= bd.BrauchtAnsicht;
                d.Darstellung ??= bd.Darstellung;
                d.Familie |= bd.Familie;
                b = bd.Basis;
            }
            if (d.Familie)
                foreach (var e in FamilienEigenschaften) if (!eig.Any(x => x.Name == e.Name)) eig.Add(e);
            d.Linie = linie;
            d.Eigenschaften = eig;
        }
    }

    /// <summary>Eigenschaften jeder familienfähigen Klasse (Modul 2, entwurf/modul-2.md). Der Bezug auf die Familie steht in
    /// bezuege.familie.</summary>
    public static readonly IReadOnlyList<EigenschaftDef> FamilienEigenschaften =
    [
        new() { Name = "familientyp", Typ = "text", Beschreibung = "Typ der Familie (Name aus ihrer Typenliste, wie der Revit-Typkatalog)" },
        new() { Name = "parameter", Typ = "objekt", Beschreibung = "Exemplarwerte der Familienparameter { name: wert } – gehen den Typwerten vor" },
        new() { Name = "drehung", Typ = "zahl", Einheit = "°", Beschreibung = "Drehung um die senkrechte Achse, Gegenuhrzeigersinn; 0 = Vorderseite der Familie zeigt nach Süden (−y)" },
        new() { Name = "ausrichtung", Typ = "objekt", Beschreibung = "Volle Ausrichtung im Raum (Modul 3, z. B. Steig- und Neigungsbögen): { x: [dx, dy, dz], z: [dx, dy, dz] } = lokale x- und z-Achse der Familie in Weltkoordinaten, orthonormal; geht drehung vor" },
    ];

    void Pruefen()
    {
        foreach (var d in Klassen.Values)
            foreach (var a in d.Anschluesse)
                if (a.Medium != "*" && !Medien.ContainsKey(a.Medium))
                    throw new HlksFehler($"Klasse {d.Name}, Anschluss {a.Name}: Medium «{a.Medium}» fehlt in medien.json.");
        foreach (var s in Systemtypen.Values)
            if (!Medien.ContainsKey(s.Medium)) throw new HlksFehler($"Systemtyp {s.Name}: Medium «{s.Medium}» fehlt in medien.json.");
    }

    public KlassenDef Klasse(string name) =>
        Klassen.TryGetValue(name, out var d) && !d.Abstrakt ? d
        : throw new HlksFehler($"Klasse «{name}» gibt es nicht{(d?.Abstrakt == true ? " als konkrete Klasse (nur Basis)" : "")}.",
            "Mögliche Klassen: " + string.Join(", ", Klassen.Values.Where(k => !k.Abstrakt).Select(k => k.Name).Order()));

    public bool IstArt(Element e, string klasse) => Klassen.TryGetValue(e.Klasse, out var d) && d.IstArt(klasse);

    public string? BkpFuer(Element e, string? gewerk)
    {
        if (!Klassen.TryGetValue(e.Klasse, out var d)) return null;
        if (gewerk is not null && d.BkpJeGewerk is not null && d.BkpJeGewerk.TryGetValue(gewerk, out var b)) return b;
        return d.Bkp;
    }
}
