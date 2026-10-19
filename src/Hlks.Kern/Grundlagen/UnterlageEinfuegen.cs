// Befehl unterlage_einfuegen (Modul 1b, B3): DWG, DXF, PDF oder Bild je Geschoss als Unterlage einfügen.
// Ablauf: Datei ablegen (Unterlagen.Ablegen: Prüfsumme + Name, nie überschreiben) → Unterlage-Element (Kennzeichen UL-n,
// Geometrie = Umriss im Modell) → erste Lage: DWG/DXF nach INSUNITS in mm, PDF/Bild nach Massstab und Auflösung,
// Ursprung (0, 0) – danach mit unterlage_kalibrieren auf bekannte Punkte ausrichten. Dieselbe Datei im selben Geschoss
// (bei PDF: derselben Seite) erneut eingefügt = neue Fassung der Unterlage, die Lage bleibt.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class UnterlageEinfuegen : IBefehl
{
    public string Name => "unterlage_einfuegen";
    public const string BeschreibungText = "Fügt eine Unterlage in ein Geschoss ein: DWG/DXF (Linien, Polylinien, Bögen, Kreise, Text, Bemassungen, Blöcke, Schraffurumrisse), PDF (eine Seite) oder Bild (PNG, JPEG). " +
        "Die Datei wird unverändert im Ordner unterlagen abgelegt; die Unterlage erscheint grau im Grundriss des Geschosses. Erste Lage: DWG/DXF nach der Einheit der Datei, PDF/Bild nach massstab, " +
        "Ursprung (0, 0). Danach mit unterlage_kalibrieren auf bekannte Punkte ausrichten. Layer ausblenden: element_aendern mit eigenschaften { ebenenAus: [Layer, …] }. " +
        "Mit zweck «familie» (Modul 2) wird eine Herstellerunterlage für eine Familie abgelegt – ohne Geschoss und ohne Lage, in Plänen nicht gezeichnet: dann auch step, iges, stl, obj, xlsx, csv, typkatalog (Revit-Typkatalog .txt) und ifc; " +
        "gelesen wird sie mit unterlagen_lesen, optional mit bezuege { familie } der Familie, zu der sie gehört.";
    public string Beschreibung => BeschreibungText;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("datei", "text", true, "Pfad der Datei (lokal oder W:) oder Name einer Unterlage im Ordner unterlagen"),
        new("geschoss", "text", false, "Geschoss (Kennzeichen oder Id), in dessen Grundriss die Unterlage erscheint; entfällt bei zweck «familie»"),
        new("art", "text", false, "dwg | dxf | pdf | bild (Vorgabe aus der Dateiendung); bei zweck «familie» auch step | iges | stl | obj | xlsx | csv | typkatalog | ifc"),
        new("seite", "zahl", false, "PDF: Seite (ab 1, Vorgabe 1)"),
        new("massstab", "zahl", false, "PDF/Bild: Massstabsnenner der Vorlage (100 = 1:100), bestimmt die erste Lage; ohne Angabe wird die Papiergrösse (1:1) eingesetzt"),
        new("dpi", "zahl", false, "PDF/Bild: Auflösung in Bildpunkten je Zoll (PDF: Vorgabe 150 beim Rendern; Bild: aus der Datei, sonst 150)"),
        new("zweck", "text", false, "plan (Vorgabe: Grundlage im Grundriss eines Geschosses) | familie (Herstellerunterlage für eine Familie, ohne Geschoss und Lage)"),
        new("bezuege", "objekt", false, "Nur bei zweck «familie»: { familie: Familie (Kennzeichen oder Id) } – die Familie, zu der die Unterlage gehört (kann später gesetzt werden)"),
    ];

    /// <summary>Länge der Prüfsumme + «_» vor dem ursprünglichen Namen (siehe <see cref="Unterlagen.Ablegen"/>).</summary>
    const int Praefix = 13;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        // Zahlen in den Hinweisen immer mit Punkt, unabhängig von der Spracheinstellung des Rechners
        var kultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { Ausfuehren2(k, p); }
        finally { CultureInfo.CurrentCulture = kultur; }
    }

    void Ausfuehren2(Kontext k, JsonObject p)
    {
        var quelle = Param.Text(p, "datei", true)!.Trim();
        var zweck = (Param.Text(p, "zweck") ?? "plan").Trim().ToLowerInvariant();
        if (zweck is not ("plan" or "familie")) throw new HlksFehler($"zweck «{zweck}» gibt es nicht.", "Erlaubt: plan (Grundlage im Grundriss) oder familie (Herstellerunterlage für eine Familie).");
        bool fuerFamilie = zweck == "familie";
        var art = ArtBestimmen(quelle, Param.Text(p, "art"), fuerFamilie);
        if (fuerFamilie) { FamilienUnterlage(k, p, quelle, art); return; }
        if (p["bezuege"] is not null) k.Hinweise.Add("bezuege gilt nur bei zweck «familie» und wurde ignoriert.");
        if (Param.Text(p, "geschoss") is null)
            throw new HlksFehler("Parameter «geschoss» fehlt.", "Das Geschoss angeben, in dessen Grundriss die Unterlage erscheint – oder zweck «familie» für eine Herstellerunterlage ohne Geschoss.");
        var geschoss = Elementpflege.BezugPruefen(k, Param.Text(p, "geschoss", true), "Geschoss", "Geschoss");

        double seite = Param.Zahl(p, "seite") ?? 1;
        if (art == "pdf" && (seite < 1 || seite != Math.Floor(seite)))
            throw new HlksFehler($"seite = {seite:0.##} ist keine Seitennummer.", "Eine ganze Zahl ab 1 angeben.");
        double? massstab = Param.Zahl(p, "massstab");
        if (massstab is <= 0) throw new HlksFehler("massstab muss grösser als 0 sein.", "Den Nenner angeben, z. B. 100 für 1:100.");
        double? dpi = Param.Zahl(p, "dpi");
        if (dpi is < 20 or > 2400) throw new HlksFehler($"dpi = {dpi:0.##} ist nicht sinnvoll.", "Bildpunkte je Zoll zwischen 20 und 2400, z. B. 150.");

        var (name, stand) = Unterlagen.Ablegen(k.Projekt, quelle);
        var original = name.Length > Praefix ? name[Praefix..] : name;
        var herkunft = k.Herkunft with { Art = "import", Befehl = Name, Quelle = original };
        var hinweise = new List<string>();
        if (art is "dwg" or "dxf")
        {
            if (massstab is not null || dpi is not null || p.ContainsKey("seite")) hinweise.Add("massstab, dpi und seite gelten nur für PDF und Bild – bei DWG/DXF bestimmt die Einheit der Datei die Grösse.");
            massstab = null; dpi = null;
        }
        else if (art == "bild" && p.ContainsKey("seite")) hinweise.Add("seite gilt nur für PDF.");

        // vorhandene Unterlage derselben Datei im selben Geschoss = neue Fassung
        var vorhanden = k.Arbeit.DerKlasse("Unterlage").FirstOrDefault(u => u.Geschoss == geschoss.Id && u.Text("art") == art
            && string.Equals(OriginalVon(u), original, StringComparison.OrdinalIgnoreCase)
            && (art != "pdf" || (u.Zahl("seite") ?? 1) == seite));
        var ul = vorhanden?.Kopie() ?? new Element
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("UL"), Klasse = "Unterlage", Name = original, Geschoss = geschoss.Id,
        };
        bool neu = vorhanden is null;
        var bisher = vorhanden?.Eigenschaft("transformation")?.Inhalt as JsonObject;
        // Raster: ändern sich Massstab oder Auflösung, passt die alte Lage nicht mehr (mm je Bildpunkt) – sie wird neu berechnet
        double? altDpi = Json.Zahl(bisher?["dpi"]), altMassstab = vorhanden?.Zahl("massstab");
        bool dpiGeaendert = dpi is not null && (altDpi is null || Math.Abs(dpi.Value - altDpi.Value) > 0.5);
        bool massstabGeaendert = massstab is not null && (altMassstab is null || Math.Abs(massstab.Value - altMassstab.Value) > 1e-9);
        if (!neu && vorhanden!.Text("stand") == stand && !dpiGeaendert && !massstabGeaendert)
        {
            k.Hinweise.Add($"{ul.Kennzeichen}: «{original}» ist unverändert schon als Unterlage eingefügt – nichts geändert.");
            return;
        }
        void E(string n, JsonNode? w) { if (w is not null) ul.Eigenschaften[n] = new Wert { Inhalt = w, Herkunft = herkunft }; }
        E("datei", name); E("original", quelle); E("art", art); E("stand", stand);
        if (neu) { E("anzeigen", true); E("deckkraft", 0.5); }
        if (art == "pdf") E("seite", seite);
        if (massstab is not null) E("massstab", massstab);

        var alt = vorhanden is null ? (UnterlagenLage?)null : UnterlagenLage.Lesen(vorhanden);
        if (alt is not null && (dpiGeaendert || massstabGeaendert))
        {
            alt = null;
            hinweise.Add("Massstab oder Auflösung haben sich geändert: die Lage ist neu aus beiden berechnet, die frühere Kalibrierung ist verworfen.");
        }
        (UnterlagenLage Lage, JsonObject Extras, List<double[]> Ecken) ergebnis = art is "dwg" or "dxf"
            ? Cad(k, ul, art, hinweise, alt)
            : Raster(k, ul, art, name, (int)seite, massstab ?? (vorhanden?.Zahl("massstab")), dpi, hinweise, alt);
        var transformation = ergebnis.Lage.Zu(MitExtras(bisher, ergebnis.Extras));
        E("transformation", transformation);

        double z0 = geschoss.Zahl("kote") ?? 0;
        ul.Geometrie = new Geometrie { Art = "polygon", Punkte = ergebnis.Ecken.Select(q => new[] { q[0], q[1], z0 }).ToList() };
        ul.Angelegt ??= herkunft;
        k.Setzen(ul);

        if (!neu) hinweise.Insert(0, $"{ul.Kennzeichen}: neue Fassung von «{original}» (Lage und Kalibrierung bleiben, bitte prüfen).");
        k.Hinweise.AddRange(hinweise);
    }

    // ---------- Herstellerunterlage einer Familie (Modul 2) ----------

    /// <summary>Unterlage für eine Familie: abgelegt wie jede Unterlage (Prüfsumme + Name, nie überschreiben), aber ohne Geschoss, ohne
    /// Lage und ohne Geometrie – sie wird deshalb in keinem Plan und in keiner 3D-Ansicht gezeichnet (Grafik.Sichtbar verlangt eine
    /// Geometrie); zusätzlich anzeigen = false und zweck = familie. Gelesen wird sie mit unterlagen_lesen (Befunde).</summary>
    void FamilienUnterlage(Kontext k, JsonObject p, string quelle, string art)
    {
        var (name, stand) = Unterlagen.Ablegen(k.Projekt, quelle);
        var original = name.Length > Praefix ? name[Praefix..] : name;
        var herkunft = k.Herkunft with { Art = "import", Befehl = Name, Quelle = original };
        var hinweise = new List<string>();
        if (p.ContainsKey("geschoss")) hinweise.Add("geschoss gilt nicht bei zweck «familie» (die Unterlage hat kein Geschoss) und wurde ignoriert.");
        if (p.ContainsKey("massstab") || p.ContainsKey("dpi") || p.ContainsKey("seite")) hinweise.Add("massstab, dpi und seite gelten nur für Grundlagen im Plan; bei zweck «familie» liest unterlagen_lesen die ganze Datei und unterlage_bild zeigt jede Seite.");

        Element? familie = null;
        if (p["bezuege"] is JsonObject bz)
        {
            foreach (var rolle in bz.Select(x => x.Key).Where(r => r != "familie"))
                throw new HlksFehler($"bezuege.{rolle} gibt es bei einer Herstellerunterlage nicht.", "Erlaubt: bezuege { familie: Kennzeichen oder Id der Familie }.");
            if (bz["familie"] is { } f) familie = Elementpflege.BezugPruefen(k, f.GetValue<string>(), "Familie", "Familie");
        }
        else if (p["bezuege"] is not null) throw new HlksFehler("bezuege muss ein Objekt sein.", "{ familie: Kennzeichen oder Id der Familie }");

        var vorhanden = k.Arbeit.DerKlasse("Unterlage").FirstOrDefault(u => u.Geschoss is null && u.Text("zweck") == "familie" && u.Text("art") == art
            && string.Equals(OriginalVon(u), original, StringComparison.OrdinalIgnoreCase));
        var ul = vorhanden?.Kopie() ?? new Element
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("UL"), Klasse = "Unterlage", Name = original,
        };
        bool neu = vorhanden is null;
        if (!neu && vorhanden!.Text("stand") == stand && (familie is null || ul.Bezuege.GetValueOrDefault("familie") == familie.Id))
        {
            k.Hinweise.Add($"{ul.Kennzeichen}: «{original}» ist unverändert schon als Unterlage für Familien eingefügt – nichts geändert.");
            return;
        }
        void E(string n, JsonNode? w) { if (w is not null) ul.Eigenschaften[n] = new Wert { Inhalt = w, Herkunft = herkunft }; }
        E("datei", name); E("original", quelle); E("art", art); E("stand", stand); E("zweck", "familie");
        if (neu) E("anzeigen", false);
        ul.Geometrie = null;
        if (familie is not null) ul.Bezuege["familie"] = familie.Id;
        ul.Angelegt ??= herkunft;
        k.Setzen(ul);

        hinweise.Insert(0, neu
            ? $"{ul.Kennzeichen}: «{original}» ({art}) als Herstellerunterlage abgelegt – ohne Geschoss und Lage, in Plänen nicht gezeichnet. Als Nächstes: unterlagen_lesen {{ unterlagen: [\"{ul.Kennzeichen}\"] }}."
            : $"{ul.Kennzeichen}: neue Fassung von «{original}» (die Befunde der alten Fassung stimmen nicht mehr; neu lesen).");
        if (art is "step" or "iges") hinweise.Add("STEP und IGES liest der STEP-Leser (occt-import-js, Node); ist er nicht eingebaut, bleibt die Datei als Referenz abgelegt.");
        k.Hinweise.AddRange(hinweise);
    }

    static JsonObject MitExtras(JsonObject? bisher, JsonObject neu)
    {
        var o = bisher?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (key, wert) in neu) o[key] = wert?.DeepClone();
        return o;
    }

    /// <summary>Ursprünglicher Dateiname einer abgelegten Unterlage (ohne Prüfsumme).</summary>
    static string OriginalVon(Element u)
    {
        var d = u.Text("datei") ?? "";
        return d.Length > Praefix && d[Praefix - 1] == '_' ? d[Praefix..] : d;
    }

    static string ArtBestimmen(string datei, string? art, bool fuerFamilie = false)
    {
        const string NurFamilie = "step, iges, stl, obj, xlsx, csv, typkatalog, html und ifc gibt es nur mit zweck «familie» (Herstellerunterlage ohne Geschoss und Lage).";
        if (art is not null)
        {
            var a = art.Trim().ToLowerInvariant();
            if (fuerFamilie)
            {
                if (!UnterlagenArten.FuerFamilie.Contains(a)) throw new HlksFehler($"Art «{art}» gibt es nicht.", "Erlaubt: " + string.Join(", ", UnterlagenArten.FuerFamilie) + ".");
                return a;
            }
            if (a == "ifc") throw new HlksFehler("IFC-Dateien werden nicht mit unterlage_einfuegen eingefügt.", "ifc_pruefen und ifc_uebernehmen verwenden (als Herstellerunterlage einer Familie: zweck «familie»).");
            if (UnterlagenArten.Alle.Contains(a) && a is not ("dwg" or "dxf" or "pdf" or "bild")) throw new HlksFehler($"Art «{art}» ist nur eine Herstellerunterlage.", NurFamilie);
            if (a is not ("dwg" or "dxf" or "pdf" or "bild")) throw new HlksFehler($"Art «{art}» gibt es nicht.", "Erlaubt: dwg, dxf, pdf, bild.");
            return a;
        }
        var ext = Path.GetExtension(datei).ToLowerInvariant();
        if (fuerFamilie)
        {
            return UnterlagenArten.AusEndung(datei, File.Exists(datei) ? datei : null)
                ?? throw new HlksFehler($"Aus der Endung «{ext}» lässt sich die Art der Datei nicht bestimmen.",
                    "art angeben: " + string.Join(", ", UnterlagenArten.FuerFamilie) + ". TIFF und RFA werden nicht gelesen.");
        }
        return ext switch
        {
            ".dwg" => "dwg",
            ".dxf" => "dxf",
            ".pdf" => "pdf",
            ".png" or ".jpg" or ".jpeg" => "bild",
            ".ifc" => throw new HlksFehler("IFC-Dateien werden nicht mit unterlage_einfuegen eingefügt.", "ifc_pruefen und ifc_uebernehmen verwenden (als Herstellerunterlage einer Familie: zweck «familie»)."),
            ".step" or ".stp" or ".iges" or ".igs" or ".stl" or ".obj" or ".xlsx" or ".xlsm" or ".csv" or ".html" or ".htm" => throw new HlksFehler($"Dateien mit der Endung «{ext}» sind Herstellerunterlagen.", NurFamilie),
            _ => throw new HlksFehler($"Aus der Endung «{ext}» lässt sich die Art der Datei nicht bestimmen.", "art angeben: dwg, dxf, pdf oder bild (Bilder als PNG oder JPEG)."),
        };
    }

    // ---------- DWG/DXF ----------

    static (UnterlagenLage, JsonObject, List<double[]>) Cad(Kontext k, Element ul, string art, List<string> hinweise, UnterlagenLage? alt)
    {
        var z = CadZeichnung.Laden(k.Projekt, ul);
        if (z.Umriss is null)
            throw new HlksFehler($"«{ul.Text("datei")}» enthält im Modellbereich nichts, das sich darstellen lässt.",
                "Prüfen, ob die Zeichnung im Modellbereich (nicht nur in Layouts) liegt und die Layer eingeschaltet sind." +
                (z.NichtDargestellt.Count > 0 ? " Nicht darstellbar: " + string.Join(", ", z.NichtDargestellt.Select(x => $"{x.Key} {x.Value}")) + "." : ""));
        var lage = alt ?? new UnterlagenLage(z.MmJeEinheit, 0, 0, 0);
        var extras = new JsonObject
        {
            ["erwartung"] = z.MmJeEinheit,
            ["grundlage"] = z.EinheitBekannt ? $"Einheit der Datei (INSUNITS {z.Einheit}): {Zahl(z.MmJeEinheit)} mm je Zeichnungseinheit"
                                              : "Annahme: Millimeter (die Datei nennt keine Einheit)",
        };
        var u = z.Umriss;
        double breite = (u[2] - u[0]) * z.MmJeEinheit / 1000, hoehe = (u[3] - u[1]) * z.MmJeEinheit / 1000;
        hinweise.Insert(0, $"{ul.Kennzeichen}: «{ul.Name}» ({art.ToUpperInvariant()} {z.Version}) mit {z.Primitive.Count} Elementen in {z.Layer.Count} Layern; " +
                           $"Hauptteil {breite:0.0} × {hoehe:0.0} m, Einheit {(z.EinheitBekannt ? z.Einheit : "unbekannt")}.");
        if (alt is null)
            hinweise.Add(z.EinheitBekannt
                ? $"Erste Lage: Einheit der Datei ({z.Einheit}), {Zahl(z.MmJeEinheit)} mm je Zeichnungseinheit, Zeichnungsursprung auf (0, 0) des Modells."
                : "Die Datei nennt keine Zeichnungseinheit (INSUNITS fehlt): Annahme Millimeter. Stimmt das nicht, mit unterlage_kalibrieren zwei bekannte Punkte setzen – der Faktor wird dann berechnet.");
        if (alt is null) hinweise.Add("Zum Ausrichten: unterlage_kalibrieren mit zwei Punkten der Unterlage (von) und ihren Sollpunkten im Modell (nach).");
        var a = z.Ausdehnung!;
        if (a[2] - a[0] > 3 * (u[2] - u[0]) || a[3] - a[1] > 3 * (u[3] - u[1]))
            hinweise.Add($"Weit abgelegene Teile: die Zeichnung reicht über den Hauptteil hinaus bis {(a[2] - a[0]) * z.MmJeEinheit / 1000:0} × {(a[3] - a[1]) * z.MmJeEinheit / 1000:0} m " +
                         "(Legenden, Planköpfe?). Der Umriss der Unterlage zeigt nur den Hauptteil; unerwünschte Layer mit ebenenAus ausblenden.");
        double mx = (u[0] + u[2]) / 2 * z.MmJeEinheit, my = (u[1] + u[3]) / 2 * z.MmJeEinheit;
        if (alt is null && Math.Max(Math.Abs(mx), Math.Abs(my)) > 1_000_000)
            hinweise.Add($"Die Zeichnung liegt weit vom Nullpunkt ({mx / 1000:0} m, {my / 1000:0} m) – vermutlich Landeskoordinaten; mit unterlage_kalibrieren auf das Projekt legen.");
        hinweise.Add("Layer (ebenenAus zum Ausblenden): " + Layerliste(z));
        if (z.LayerAus.Count > 0) hinweise.Add($"In der Datei ausgeschaltet oder gefroren, nicht gezeichnet: {string.Join(", ", z.LayerAus.Take(20))}{(z.LayerAus.Count > 20 ? " …" : "")}.");
        if (z.NichtDargestellt.Count > 0)
            hinweise.Add("Nicht dargestellt: " + string.Join(", ", z.NichtDargestellt.Select(x => $"{x.Key} {x.Value}")) + ".");
        if (z.Gekappt > 0) hinweise.Add($"Die Zeichnung ist sehr gross: {z.Gekappt} Elemente über der Obergrenze fehlen.");
        return (lage, extras, Ecken(lage, [(u[0], u[1]), (u[2], u[1]), (u[2], u[3]), (u[0], u[3])]));
    }

    static string Layerliste(CadZeichnung z)
    {
        const int Max = 60;
        var s = string.Join(", ", z.Layer.Take(Max));
        return z.Layer.Count > Max ? $"{s} … ({z.Layer.Count - Max} weitere)" : s;
    }

    static List<double[]> Ecken(UnterlagenLage lage, (double U, double V)[] ecken) =>
        ecken.Select(e => { var (x, y) = lage.Auf(e.U, e.V); return new[] { x, y }; }).ToList();

    // ---------- PDF und Bild ----------

    static (UnterlagenLage, JsonObject, List<double[]>) Raster(Kontext k, Element ul, string art, string name, int seite, double? massstab, double? dpiParam, List<string> hinweise, UnterlagenLage? alt)
    {
        var pfad = Unterlagen.Pfad(k.Projekt, name);
        int breite, hoehe;
        double dpi, mmProPixelPapier;
        string dpiQuelle;
        if (art == "pdf")
        {
            var (seiten, bw, bh) = PdfSeite.Mass(pfad, seite);
            if (seiten < 1) throw new HlksFehler($"Das PDF «{ul.Name}» hat keine Seiten.", "Eine andere Datei wählen.");
            if (seite > seiten) throw new HlksFehler($"Das PDF hat {seiten} Seite(n), Seite {seite} gibt es nicht.", $"seite zwischen 1 und {seiten} wählen.");
            dpi = dpiParam ?? UnterlagenBild.VorgabeDpi;
            dpiQuelle = dpiParam is null ? "Vorgabe" : "angegeben";
            // Seite rendern (Ableitung) und die tatsächliche Grösse lesen
            ul.Eigenschaften["transformation"] = new Wert { Inhalt = new JsonObject { ["dpi"] = dpi }, Herkunft = k.Herkunft };
            var png = UnterlagenBild.Pfad(k.Projekt, ul);
            var info = BildDatei.Lesen(png);
            breite = info.Breite; hoehe = info.Hoehe;
            mmProPixelPapier = bw * 25.4 / 72.0 / breite;   // exakt aus der Seitengrösse, nicht aus dem gerundeten dpi
            hinweise.Add($"{ul.Kennzeichen}: PDF-Seite {seite} von {seiten} ({bw * 25.4 / 72:0} × {bh * 25.4 / 72:0} mm) mit {dpi:0} dpi als Bild {breite} × {hoehe} Bildpunkte.");
        }
        else
        {
            var info = BildDatei.Lesen(pfad);
            breite = info.Breite; hoehe = info.Hoehe;
            dpi = dpiParam ?? info.Dpi ?? UnterlagenBild.VorgabeDpi;
            dpiQuelle = dpiParam is not null ? "angegeben" : info.Dpi is not null ? "aus der Datei" : "Vorgabe, die Datei nennt keine";
            mmProPixelPapier = 25.4 / dpi;
            hinweise.Add($"{ul.Kennzeichen}: Bild «{ul.Name}» {breite} × {hoehe} Bildpunkte, {dpi:0.#} dpi ({dpiQuelle}).");
        }
        double m = massstab ?? 1;
        var extras = new JsonObject { ["dpi"] = Math.Round(dpi, 3) };
        if (massstab is not null)
        {
            extras["erwartung"] = Math.Round(mmProPixelPapier * m, 9);
            extras["grundlage"] = $"Massstab 1:{Zahl(m)} bei {dpi:0.#} dpi: {Zahl(mmProPixelPapier * m)} mm je Bildpunkt";
        }
        else
        {
            extras.Remove("erwartung");
            hinweise.Add("Kein massstab angegeben: Das Bild steht in Papiergrösse (1:1) im Modell. Mit massstab (z. B. 100 für 1:100) einfügen oder mit unterlage_kalibrieren auf zwei bekannte Punkte bringen.");
        }
        var lage = alt ?? new UnterlagenLage(mmProPixelPapier * m, 0, 0, mmProPixelPapier * m * hoehe);   // unten links auf (0, 0)
        if (alt is null)
        {
            hinweise.Add(massstab is not null
                ? $"Erste Lage: Massstab 1:{Zahl(m)}, {Zahl(mmProPixelPapier * m)} mm je Bildpunkt, ergibt {breite * mmProPixelPapier * m / 1000:0.0} × {hoehe * mmProPixelPapier * m / 1000:0.0} m; Ecke unten links auf (0, 0). " +
                  "Wurde die Vorlage nicht im angegebenen Massstab gedruckt (z. B. «auf Seite einpassen»), stimmt die Grösse nicht – kalibrieren."
                : $"Erste Lage: {breite * mmProPixelPapier / 1000:0.00} × {hoehe * mmProPixelPapier / 1000:0.00} m; Ecke unten links auf (0, 0).");
            hinweise.Add("Zum Ausrichten: unterlage_kalibrieren mit zwei Punkten der Unterlage in Bildpunkten (von: [x, y], y zählt von oben) und ihren Sollpunkten im Modell in mm (nach).");
        }
        double b = breite, h = hoehe;
        var ecken = new[] { lage.AufPixel(0, h), lage.AufPixel(b, h), lage.AufPixel(b, 0), lage.AufPixel(0, 0) }.Select(q => new[] { q.X, q.Y }).ToList();
        return (lage, extras, ecken);
    }

    static string Zahl(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);
}
