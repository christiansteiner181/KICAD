// IFC-Ausgabe der Aussparungen für Architekt und Bauingenieur (Modul 3b, B4b, Grundsatzentscheid 7): Abfrage aussparungen_ifc –
// ändert das Modell nicht, schreibt eine Datei (wie plan_pdf und bcf_export).
//
// Aufbau der Datei (Schreiber: AussparungIfcSchreiber.cs, Kontrolle: AussparungIfcRundreise.cs):
//   IfcProject (Einheiten mm) → IfcSite → IfcBuilding → IfcBuildingStorey je Geschoss (Name = Kennzeichen, LongName = Name, Elevation =
//   Kote in mm, Property Set «KICAD_Geschoss») → je Aussparung ein IfcBuildingElementProxy mit Körper (IfcExtrudedAreaSolid:
//   Rechteck B × H bzw. Kreis Ø d, Tiefe entlang der Achse, um die Mitte zentriert), Name = Nummer, Description = Form/Masse/Art,
//   Tag = Kennzeichen, Property Set «KICAD_Aussparung».
//
// Schema (am 19.10.2026 an den Klassen von Xbim.Essentials 6.1.605 geprüft, im Test AussparungIfcExportTests festgehalten):
//   IFC4   Xbim.Ifc4.SharedBldgElements.IfcBuildingElementProxy hat PredefinedType (IfcBuildingElementProxyTypeEnum: COMPLEX, ELEMENT,
//          PARTIAL, PROVISIONFORVOID, PROVISIONFORSPACE, USERDEFINED, NOTDEFINED) → PROVISIONFORVOID.
//   IFC2X3 Xbim.Ifc2x3.ProductExtension.IfcBuildingElementProxy hat kein PredefinedType (nur CompositionType: COMPLEX, ELEMENT,
//          PARTIAL); die Aufzählung des Typobjekts (IfcBuildingElementProxyTypeEnum) kennt nur USERDEFINED und NOTDEFINED →
//          ObjectType «ProvisionForVoid» (Vorgabe des Entscheids 7).
//
// GlobalId (stabil): UUID v5 (RFC 4122, wie die BCF-Guids) im KICAD-Namensraum aus «<Projekt-Id>/aussparung/<Element-Id>», als
//   22-stellige IFC-GlobalId (IfcGloballyUniqueId.ConvertToBase64). Dieselbe Aussparung hat in jeder Fassung dieselbe GlobalId – auch
//   nach Umnummerieren, in anderem Paket und auf anderem Rechner; eine neue Datei ersetzt dieselben Objekte. Geschosse und die
//   Gliederung (Projekt, Gelände, Gebäude, Beziehungen) entsprechend aus «<Projekt-Id>/ifc/…» bzw. «…/geschoss/<Element-Id>».
//
// Koordinaten: «projekt» = Projektkoordinaten von KICAD (Gebäude im Ursprung); «ifc» = Welt der Architekten-IFC: das Gebäude steht
//   im Projektbezug (Projektinformation.ifcBezug: Welt = DrehungZ(drehung) · Modell + [x, y, z], siehe IfcBezug), die Geschosse bei
//   ihrer Kote, die Aussparungen in den Koordinaten des Projekts relativ zum Geschoss. Der IFC-Import (IfcDatei) holt sie mit demselben
//   Bezug in die Projektkoordinaten zurück (Rundreise). Vorgabe ifc, wenn das Projekt einen Bezug hat, sonst projekt (mit Warnung).
//
// ANNAHMEN (Prüfliste Christian):
//   – Eigenes Property Set «KICAD_Aussparung» statt «Pset_ProvisionForVoid»: dessen Aufbau liegt im Projekt nicht als Schema vor
//     (nicht aus dem Gedächtnis); es kann nachgeführt werden, sobald die buildingSMART-Definition geprüft ist.
//   – Koten im Property Set «über ±0.00» (Projekt), dazu «über OK Geschoss»; die Lage steht in den Platzierungen.
//   – Körper rechtwinklig zum Bauteil (Achse der Aussparung), Tiefe = Bauteildicke, um die Mitte zentriert.
//   – Nachtrag B3c: Bauteil = das Haupt-Bauteil (das dickste einer durchgehenden Aussparung), BauteileWeitere = die übrigen (GlobalIds
//     bzw. KICAD-Kennzeichen, durch Komma getrennt), Material (Text der Einteilung) und MaterialUnbekannt (ja/nein).
//   – Nachtrag B4d: Kurzzeichen nach SIA 400:2000 Ziff. B.10.2 Tabelle 5 / SIA 410/2 Ziff. 2 1 («S DD», «H WD», allgemein «DD», «FD»;
//     Regeln in AussparungKennzeichnung.cs); fehlt, wenn die Lage des Bauteils nicht bestimmbar ist.

using System.Globalization;
using System.Text.Json.Nodes;
using Xbim.Ifc4.UtilityResource;

namespace Hlks.Kern;

public static class AussparungIfcTexte
{
    public const string Export =
        "Schreibt die Aussparungen und Kernbohrungen als schlanke IFC-Datei für Architekt und Bauingenieur (IFC4 oder IFC2X3): IfcProject (mm), " +
        "IfcSite, IfcBuilding, IfcBuildingStorey (Geschosse mit Kote), je Aussparung ein IfcBuildingElementProxy (IFC4: PredefinedType PROVISIONFORVOID, " +
        "IFC2X3: ObjectType «ProvisionForVoid») mit Körper (Quader bzw. Zylinder, Tiefe entlang der Achse), Name = Nummer, Beschreibung = Form, Masse, Art und " +
        "Property Set «KICAD_Aussparung» (Nummer, Art, Status, Masse, Koten, Bauteil, Leitungen). Die GlobalId ist je Aussparung fest (aus Projekt- und " +
        "Element-Id): eine neue Fassung ersetzt dieselben Objekte. Vorgabe: freigegebene und versandte Aussparungen, Koordinaten der Architekten-IFC " +
        "(über den Projektbezug, sonst Projektkoordinaten). Die geschriebene Datei wird zur Kontrolle wieder gelesen (Rundreise); stimmt etwas nicht, wird nichts " +
        "geschrieben. Ändert das Modell nicht – den Status «versandt» setzt aussparung_status nach dem Versand. Antwort: Exportbericht (Anzahl je Art und " +
        "Geschoss, Koordinatenbezug, Fassung, Rundreise, Warnungen).";
    public const string ParDatei = "Zieldatei (.ifc); eine vorhandene Datei wird ersetzt";
    public const string ParFassung = "IFC4 (Vorgabe) | IFC2X3 (für ältere Programme)";
    public const string ParGeschoss = "Nur Aussparungen dieses Geschosses (Kennzeichen); Vorgabe alle";
    public const string ParStatus = "Nur Aussparungen mit diesem Status (vorgeschlagen, freigegeben, versandt, angekommen, ausgefuehrt, veraltet, fehlt) als Liste oder Text; Vorgabe freigegeben und versandt";
    public const string ParKoordinaten = "ifc (Vorgabe: Welt der Architekten-IFC über den Projektbezug, wie der Import sie hereingeholt hat; ohne Projektbezug projekt) | projekt (Projektkoordinaten von KICAD)";
}

public static class AussparungIfcAbfrage
{
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("aussparungen_ifc", AussparungIfcTexte.Export,
        [
            new("datei", "text", true, AussparungIfcTexte.ParDatei),
            new("fassung", "text", false, AussparungIfcTexte.ParFassung),
            new("geschoss", "text", false, AussparungIfcTexte.ParGeschoss),
            new("status", "liste", false, AussparungIfcTexte.ParStatus),
            new("koordinaten", "text", false, AussparungIfcTexte.ParKoordinaten),
        ]),
    ];
}

/// <summary>Ein Wert des Property Sets: Art label (IfcLabel), text (IfcText), laenge (IfcLengthMeasure, mm), kennung (IfcIdentifier).</summary>
public sealed record IfcEigenschaft(string Name, string Art, string? Text = null, double? Laenge = null);

/// <summary>Ein Geschoss der Ausgabe.</summary>
public sealed record IfcGeschossDaten(string Id, string GlobalId, string Kennzeichen, string? Name, double Kote, double? Hoehe);

/// <summary>Eine Aussparung der Ausgabe: alles, was der Schreiber braucht (unabhängig vom Modell und von der IFC-Fassung).</summary>
public sealed record IfcAussparungDaten(string Id, string Kennzeichen, string GlobalId, string Name, string Beschreibung, Aussparungslage Lage,
    string? GeschossId, string Art, string Status, IReadOnlyList<IfcEigenschaft> Eigenschaften);

public static class AussparungIfcExport
{
    public const string Fassung4 = "IFC4", Fassung2x3 = "IFC2X3";
    /// <summary>Name des Property Sets der Aussparung (eigenes Set, kein «Pset_» – dieser Vorsatz gehört buildingSMART).</summary>
    public const string PsetAussparung = "KICAD_Aussparung", PsetGeschoss = "KICAD_Geschoss";
    /// <summary>IFC2X3: ObjectType der Aussparung (IfcBuildingElementProxy hat dort kein PredefinedType).</summary>
    public const string ObjectType2x3 = "ProvisionForVoid";
    /// <summary>IFC4: PredefinedType (Name des Wertes in Xbim.Ifc4.Interfaces.IfcBuildingElementProxyTypeEnum).</summary>
    public const string PredefinedType4 = "PROVISIONFORVOID";

    /// <summary>KICAD-Namensraum der IFC-GlobalIds (UUID v5 aus dem URL-Namensraum und «https://hkplanung.ch/kicad/ifc»).</summary>
    public static readonly Guid Namensraum = BcfFormat.UuidV5(BcfFormat.NamensraumUrl, "https://hkplanung.ch/kicad/ifc");

    /// <summary>Stabile GlobalId (22 Zeichen) aus Projekt-Id und Schlüssel.</summary>
    public static string GlobalId(string projektId, string schluessel) =>
        IfcGloballyUniqueId.ConvertToBase64(BcfFormat.UuidV5(Namensraum, $"{projektId}/{schluessel}"));

    /// <summary>GlobalId der Aussparung: aus «<Projekt-Id>/aussparung/<Element-Id>».</summary>
    public static string AussparungGlobalId(string projektId, string elementId) => GlobalId(projektId, "aussparung/" + elementId);

    static string Z(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    static double R1(double x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);

    /// <summary>Normalisiert die Fassung: IFC4 | IFC2X3 (Gross-/Kleinschreibung, «IFC2x3», «2x3» …); sonst HlksFehler.</summary>
    public static string FassungLesen(string? roh)
    {
        if (string.IsNullOrWhiteSpace(roh)) return Fassung4;
        var s = new string(roh.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (s is "IFC4" or "4") return Fassung4;
        if (s is "IFC2X3" or "2X3") return Fassung2x3;
        throw new HlksFehler($"Die IFC-Fassung «{roh}» gibt es für die Ausgabe nicht (IFC 4.3 und IFC2x2 sind nicht vorgesehen, Entscheid 17.10.2026).", "fassung: IFC4 (Vorgabe) | IFC2X3");
    }

    /// <summary>aussparungen_ifc: wählt die Aussparungen, schreibt die Datei, liest sie zur Kontrolle wieder (Rundreise) und liefert den
    /// Exportbericht. Bei einer Abweichung wird nichts geschrieben.</summary>
    public static JsonObject Schreiben(Projekt p, JsonObject par)
    {
        var datei = (Param.Text(par, "datei", true) ?? "").Trim().Trim('"');
        if (!string.Equals(Path.GetExtension(datei), ".ifc", StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler($"Die Zieldatei «{datei}» braucht die Endung .ifc.", "datei z. B. «…\\Aussparungen_EG_2026-10-19.ifc» angeben.");
        datei = KiPfade.Pruefen(p, datei, "aussparungen_ifc");   // Sicherheitsprüfung 3c, B3a/B4: KI nur unter den Wurzeln aus kataloge/buero.json
        string pfad;
        try { pfad = Path.GetFullPath(datei); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new HlksFehler($"Der Pfad «{datei}» ist ungültig: {ex.Message}", "Vollständigen Pfad mit Endung .ifc angeben."); }
        var fassung = FassungLesen(Param.Text(par, "fassung"));

        var m = p.Modell;
        var warnungen = new List<string>();
        var hinweise = new List<string>();

        // Koordinaten
        var koordArt = Param.Text(par, "koordinaten") is { } kr && !string.IsNullOrWhiteSpace(kr) ? kr.Trim().ToLowerInvariant() : null;
        if (koordArt is not (null or "ifc" or "projekt"))
            throw new HlksFehler($"koordinaten «{koordArt}» gibt es nicht.", "koordinaten: ifc (Vorgabe) | projekt");
        var bezug = IfcBezug.Aus(m.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcBezug")?.Inhalt);
        if (koordArt == "ifc" && bezug is null)
            throw new HlksFehler("Das Projekt hat keinen IFC-Bezug (Projektinformation.ifcBezug) – die Koordinaten der Architekten-IFC sind nicht bestimmbar.",
                "koordinaten «projekt» wählen oder zuerst die Architekten-IFC übernehmen (ifc_uebernehmen setzt den Projektbezug).");
        if (koordArt is null && bezug is null)
            warnungen.Add("Das Projekt hat keinen IFC-Bezug (noch keine IFC-Unterlage übernommen): die Aussparungen stehen in Projektkoordinaten. " +
                          "Für die Architekten-IFC zuerst ifc_uebernehmen, dann die Datei neu schreiben.");
        var koordinaten = koordArt == "projekt" || bezug is null ? "projekt" : "ifc";
        var verwendeterBezug = koordinaten == "ifc" ? bezug : null;

        // Auswahl
        var auswahl = Auswahl(p, par, out var geschossFilter, out var statusFilter, out var ausgelassen);
        if (auswahl.Count == 0)
            throw new HlksFehler(ausgelassen.Count == 0 ? "Das Projekt hat keine Aussparungen – nichts für die IFC-Ausgabe."
                    : $"Keine Aussparung passt zum Filter (Status {string.Join(", ", statusFilter)}{(geschossFilter is null ? "" : $", Geschoss {geschossFilter.Kennzeichen}")}) – nichts für die IFC-Ausgabe. Vorhanden: {Zaehlung(ausgelassen)}.",
                "Zuerst aussparungen_vorschlagen, dann freigeben (aussparung_status freigegeben) bzw. den Filter lockern (status, geschoss).");

        // Daten
        var geschosse = new Dictionary<string, IfcGeschossDaten>(StringComparer.Ordinal);
        var daten = new List<IfcAussparungDaten>();
        foreach (var a in auswahl)
        {
            var lage = Aussparungslage.Von(a);
            if (lage is null || lage.Tiefe <= 0)
            {
                warnungen.Add($"{a.Kennzeichen}: {(lage is null ? "Lage oder Masse" : "Tiefe")} fehlen – nicht ausgegeben.");
                continue;
            }
            IfcGeschossDaten? g = null;
            if (m.Finde(a.Geschoss) is { } ge)
            {
                if (!geschosse.TryGetValue(ge.Id, out g))
                {
                    if (ge.Zahl("kote") is not double kote)
                    {
                        warnungen.Add($"Geschoss {ge.Kennzeichen} hat keine Kote – {a.Kennzeichen} steht ohne Geschoss im Gebäude.");
                        g = null;
                    }
                    else
                        geschosse[ge.Id] = g = new IfcGeschossDaten(ge.Id, GlobalId(p.Kopf.Id, "geschoss/" + ge.Id), ge.Kennzeichen,
                            string.IsNullOrWhiteSpace(ge.Name) ? null : ge.Name, kote, ge.Zahl("hoehe"));
                }
            }
            else warnungen.Add($"{a.Kennzeichen} hat kein Geschoss – steht ohne Geschoss im Gebäude.");
            daten.Add(Daten(p, a, lage, g, koordinaten, warnungen));
        }
        if (daten.Count == 0)
            throw new HlksFehler("Keine der gewählten Aussparungen hat eine gültige Lage – nichts für die IFC-Ausgabe.", "Aussparungen mit aussparungen_vorschlagen nachführen.");
        foreach (var d in daten.GroupBy(x => x.Name).Where(x => x.Count() > 1))
            warnungen.Add($"Nummer «{d.Key}» kommt {d.Count()}-mal vor ({string.Join(", ", d.Select(x => x.Kennzeichen))}) – im IFC unterscheidet nur die GlobalId.");
        if (ausgelassen.Count > 0)
        {
            var veraltet = ausgelassen.Count(x => AussparungStatus.Von(x) == AussparungStatus.Veraltet);
            if (veraltet > 0)
                warnungen.Add($"{veraltet} veraltete Aussparung(en) nicht ausgegeben ({string.Join(", ", ausgelassen.Where(x => AussparungStatus.Von(x) == AussparungStatus.Veraltet).Select(x => x.Kennzeichen).Take(10))}" +
                              $"{(veraltet > 10 ? ", …" : "")}) – beim Architekten zurückziehen, aussparungen_vorschlagen führt den neuen Vorschlag nach.");
            hinweise.Add($"Nicht ausgegeben wegen Status-/Geschossfilter: {Zaehlung(ausgelassen)}.");
        }
        var nochNichtVersandt = daten.Where(x => x.Status == AussparungStatus.Freigegeben).Select(x => x.Name).ToList();
        if (nochNichtVersandt.Count > 0)
            hinweise.Add($"{nochNichtVersandt.Count} Aussparung(en) stehen noch auf «freigegeben»: nach dem Versand mit aussparung_status auf «versandt» setzen ({string.Join(", ", nochNichtVersandt.Take(10))}{(nochNichtVersandt.Count > 10 ? ", …" : "")}).");

        var geschossListe = geschosse.Values.OrderBy(x => x.Kote).ThenBy(x => x.Kennzeichen, StringComparer.Ordinal).ToList();
        var ctx = new IfcAusgabe(fassung, pfad, p.Kopf.Id, string.IsNullOrWhiteSpace(p.Kopf.Name) ? "KICAD" : p.Kopf.Name!, p.Akteur, koordinaten, verwendeterBezug,
            geschossListe, daten, DateTimeOffset.Now);

        // schreiben in eine Hilfsdatei, zurücklesen, prüfen, dann an den Zielort
        var ordner = Path.GetDirectoryName(pfad)!;
        string? temp = null;
        RundreiseErgebnis rr;
        long bytes;
        try
        {
            Directory.CreateDirectory(ordner);
            temp = Path.Combine(ordner, "." + Path.GetFileNameWithoutExtension(pfad) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp.ifc");
            AussparungIfcSchreiber.Schreiben(ctx, temp);
            rr = AussparungIfcRundreise.Pruefen(ctx, temp);
            if (rr.Abweichungen.Count > 0)
                throw new HlksFehler($"Die IFC-Ausgabe weicht beim Zurücklesen ab, es wurde nichts geschrieben: {string.Join(" | ", rr.Abweichungen.Take(5))}" +
                                     $"{(rr.Abweichungen.Count > 5 ? $" (… {rr.Abweichungen.Count} Abweichungen)" : "")}",
                    "Interner Fehler der IFC-Ausgabe – bitte mit der Meldung melden; bis dahin die betroffene Aussparung mit dem Filter (status, geschoss) auslassen.");
            File.Move(temp, pfad, overwrite: true);
            temp = null;
            bytes = new FileInfo(pfad).Length;
            Ausgabeprotokoll.Vermerken(p, "aussparungen_ifc", pfad);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new HlksFehler($"Die Datei «{pfad}» kann nicht geschrieben werden: {ex.Message}", "Anderen Pfad angeben bzw. die Datei in anderen Programmen schliessen.");
        }
        finally
        {
            if (temp is not null) try { File.Delete(temp); } catch (Exception) { }
        }
        foreach (var h in rr.Hinweise) hinweise.Add(h);

        return Bericht(p, ctx, pfad, bytes, rr, geschossFilter, statusFilter, ausgelassen, warnungen, hinweise);
    }

    // ------------------------------------------------------------------ Auswahl

    static List<Element> Auswahl(Projekt p, JsonObject par, out Element? geschossFilter, out List<string> statusFilter, out List<Element> ausgelassen)
    {
        var m = p.Modell;
        var def = p.Katalog.Klasse("Aussparung");
        var erlaubt = def.Eigenschaften.FirstOrDefault(x => x.Name == "status")?.Auswahl ?? [];
        var roh = Liste(par, "status")?.Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().ToList();
        if (roh is { Count: 0 }) roh = null;
        if (roh is not null)
            foreach (var s in roh.Where(s => !erlaubt.Contains(s)))
                throw new HlksFehler($"Status «{s}» gibt es für Aussparungen nicht.", "status: " + string.Join(", ", erlaubt));
        var filter = roh ?? [AussparungStatus.Freigegeben, AussparungStatus.Versandt];
        statusFilter = filter;
        geschossFilter = Param.Text(par, "geschoss") is { Length: > 0 } g ? m.HoleDerKlasse(g, "Geschoss") : null;
        var gf = geschossFilter;
        double Kote(string? id) => m.Finde(id)?.Zahl("kote") ?? double.MinValue;
        var imGeschoss = m.DerKlasse("Aussparung").Where(a => gf is null || a.Geschoss == gf.Id).ToList();
        var gewaehlt = imGeschoss.Where(a => filter.Contains(AussparungStatus.Von(a)))
            .OrderBy(a => Kote(a.Geschoss)).ThenBy(a => a.Text("nummer") is { Length: > 0 } ? 0 : 1)
            .ThenBy(a => a.Text("nummer") ?? "", Aussparungsbezug.Natuerlich).ThenBy(a => a.Kennzeichen, Aussparungsbezug.Natuerlich).ToList();
        ausgelassen = imGeschoss.Except(gewaehlt).OrderBy(a => a.Kennzeichen, Aussparungsbezug.Natuerlich).ToList();
        return gewaehlt;
    }

    static string Zaehlung(List<Element> liste) =>
        string.Join(", ", liste.GroupBy(AussparungStatus.Von).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {x.Count()}"));

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
        throw new HlksFehler($"Parameter «{name}» muss eine Liste sein, z. B. [\"freigegeben\", \"versandt\"].");
    }

    // ------------------------------------------------------------------ Daten einer Aussparung

    static IfcAussparungDaten Daten(Projekt p, Element a, Aussparungslage lage, IfcGeschossDaten? g, string koordinaten, List<string> warnungen)
    {
        var m = p.Modell;
        var nummer = a.Text("nummer") is { Length: > 0 } n ? n : null;
        if (nummer is null) warnungen.Add($"{a.Kennzeichen} hat keine Nummer – der Name im IFC ist das Kennzeichen (aussparungen_nummerieren vergibt Nummern).");
        var name = nummer ?? a.Kennzeichen;
        var art = a.Text("art") is { Length: > 0 } ar ? ar : (lage.Rund ? "kernbohrung" : "aussparung");
        var artText = art == "kernbohrung" ? "Kernbohrung" : art == "aussparung" ? "Aussparung" : art;
        var form = lage.Rund ? "rund" : "rechteckig";
        var mass = lage.Rund ? $"Ø {Z(lage.D)}" : $"{Z(lage.B)} × {Z(lage.H)}";
        var beschreibung = $"{artText} {form} {mass} mm, Tiefe {Z(lage.Tiefe)} mm";
        var status = AussparungStatus.Von(a);

        var eig = new List<IfcEigenschaft>
        {
            new("Nummer", "label", Text: nummer),
            new("Kennzeichen", "label", Text: a.Kennzeichen),
            new("KicadId", "kennung", Text: a.Id),
            new("Art", "label", Text: artText),
            new("Status", "label", Text: status),
            new("Form", "label", Text: form),
        };
        // Nachtrag B4d: Kurzzeichen nach SIA 400:2000 B.10.2 Tabelle 5 («S DD», «H WD», allgemein «DD»; AussparungKennzeichnung.cs)
        if (AussparungKennzeichnung.Kurzzeichen(m, p.Katalog, a) is { } kurz) eig.Add(new("Kurzzeichen", "label", Text: kurz));
        if (lage.Rund) eig.Add(new("Durchmesser", "laenge", Laenge: lage.D));
        else { eig.Add(new("Breite", "laenge", Laenge: lage.B)); eig.Add(new("Hoehe", "laenge", Laenge: lage.H)); }
        eig.Add(new("Tiefe", "laenge", Laenge: lage.Tiefe));
        var (ukL, okL) = lage.Koten();
        double uk = a.Zahl("kote_uk") ?? ukL, ok = a.Zahl("kote_ok") ?? okL;
        if (lage.Rund) eig.Add(new("KoteAchse", "laenge", Laenge: R1(a.Zahl("kote_uk") ?? lage.Mitte.Z)));
        else { eig.Add(new("KoteUK", "laenge", Laenge: R1(uk))); eig.Add(new("KoteOK", "laenge", Laenge: R1(ok))); }
        if (g is not null)
        {
            if (lage.Rund) eig.Add(new("KoteAchseGeschoss", "laenge", Laenge: R1((a.Zahl("kote_uk") ?? lage.Mitte.Z) - g.Kote)));
            else { eig.Add(new("KoteUKGeschoss", "laenge", Laenge: R1(uk - g.Kote))); eig.Add(new("KoteOKGeschoss", "laenge", Laenge: R1(ok - g.Kote))); }
            eig.Add(new("Geschoss", "label", Text: g.Kennzeichen));
        }
        // Bauteil: GlobalId (übernommenes Element bzw. Hintergrundprodukt), sonst das KICAD-Kennzeichen
        var bauteilEl = m.Finde(a.Bezuege.GetValueOrDefault("bauteil"));
        var gid = Aussparungsbezug.BauteilGlobalId(m, a);
        var bauteil = gid ?? bauteilEl?.Kennzeichen;
        if (bauteil is null) warnungen.Add($"{a.Kennzeichen}: das gequerte Bauteil ist nicht bekannt (Bezug fehlt oder gelöscht).");
        else eig.Add(new("Bauteil", "label", Text: bauteil));
        if (bauteilEl is not null) eig.Add(new("BauteilKennzeichen", "label", Text: bauteilEl.Kennzeichen));
        if (bauteilEl?.Klasse is { Length: > 0 } bk) eig.Add(new("BauteilKlasse", "label", Text: bk));
        else if (a.Kennungen?.GetValueOrDefault("bauteilEntitaet") is { Length: > 0 } be) eig.Add(new("BauteilEntitaet", "label", Text: be));
        // Nachtrag B3c: durchgehende Aussparung – die weiteren Bauteile (GlobalId, sonst KICAD-Kennzeichen) in der gespeicherten Folge;
        // Material der Bauteile und ob es unbekannt ist
        var weitere = Aussparungsbezug.WeitereBauteile(a)
            .Select(x => Aussparungsbezug.BauteilGlobalId(m, a, x.Nummer) ?? m.Finde(x.Schluessel)?.Kennzeichen ?? x.Schluessel).ToList();
        if (weitere.Count > 0) eig.Add(new("BauteileWeitere", "text", Text: string.Join(", ", weitere)));
        if (a.Text("material") is { Length: > 0 } mat) eig.Add(new("Material", "text", Text: mat));
        if (a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue mu && mu.TryGetValue<bool>(out var unbekannt))
            eig.Add(new("MaterialUnbekannt", "label", Text: unbekannt ? "ja" : "nein"));
        var leitungen = new List<string>();
        foreach (var id in Aussparungsbezug.Leitungen(a))
        {
            if (m.Finde(id) is { } le) leitungen.Add(le.Kennzeichen);
            else warnungen.Add($"{a.Kennzeichen}: Leitung {id} gibt es nicht mehr (gelöscht).");
        }
        if (leitungen.Count > 0) eig.Add(new("Leitungen", "text", Text: string.Join(", ", leitungen)));
        eig.Add(new("Koordinatenbezug", "label", Text: koordinaten));
        return new IfcAussparungDaten(a.Id, a.Kennzeichen, AussparungGlobalId(p.Kopf.Id, a.Id), name, beschreibung, lage, g?.Id, art, status, eig);
    }

    // ------------------------------------------------------------------ Bericht

    static JsonObject Bericht(Projekt p, IfcAusgabe ctx, string pfad, long bytes, RundreiseErgebnis rr, Element? geschossFilter, List<string> statusFilter,
        List<Element> ausgelassen, List<string> warnungen, List<string> hinweise)
    {
        JsonObject Zaehl<T>(IEnumerable<T> quelle, Func<T, string> schluessel) =>
            new(quelle.GroupBy(schluessel).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count())));
        var gk = ctx.Geschosse.ToDictionary(g => g.Id, g => g.Kennzeichen);
        var arten = Zaehl(ctx.Aussparungen, a => a.Art);
        var objekttyp = ctx.Fassung == Fassung4 ? $"PredefinedType {PredefinedType4}" : $"ObjectType «{ObjectType2x3}»";
        return new JsonObject
        {
            ["datei"] = pfad,
            ["fassung"] = ctx.Fassung,
            ["entitaet"] = "IfcBuildingElementProxy",
            ["kennzeichnung"] = objekttyp,
            ["einheit"] = "mm",
            ["groesseBytes"] = bytes,
            ["stand"] = ctx.Zeit.ToString("o"),
            ["anzahl"] = ctx.Aussparungen.Count,
            ["jeArt"] = arten,
            ["jeGeschoss"] = Zaehl(ctx.Aussparungen, a => a.GeschossId is { } id ? gk[id] : "ohne Geschoss"),
            ["jeStatus"] = Zaehl(ctx.Aussparungen, a => a.Status),
            ["koordinaten"] = new JsonObject
            {
                ["art"] = ctx.Koordinaten,
                ["text"] = ctx.Koordinaten == "ifc"
                    ? $"ifc: Welt der Architekten-IFC über den Projektbezug (Weltpunkt {Z(ctx.Bezug!.X / 1000)} / {Z(ctx.Bezug.Y / 1000)} / {Z(ctx.Bezug.Z / 1000)} m, Drehung {ctx.Bezug.Drehung.ToString("0.######", CultureInfo.InvariantCulture)}°); " +
                      "Gebäude im Bezug, Geschosse bei ihrer Kote, Aussparungen in Projektkoordinaten relativ zum Geschoss"
                    : "projekt: Projektkoordinaten von KICAD (x Ost, y Nord, z über ±0.00), Gebäude im Ursprung",
                ["bezug"] = ctx.Bezug?.AlsJson(),
            },
            ["filter"] = new JsonObject
            {
                ["geschoss"] = geschossFilter?.Kennzeichen,
                ["status"] = new JsonArray(statusFilter.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                ["nichtAusgegeben"] = ausgelassen.Count,
            },
            ["globalId"] = "UUID v5 (RFC 4122) im KICAD-Namensraum aus «<Projekt-Id>/aussparung/<Element-Id>», als 22-stellige IFC-GlobalId – je Aussparung fest über Fassungen, Nummern und Rechner",
            ["geschosse"] = new JsonArray(ctx.Geschosse.Select(g => (JsonNode)new JsonObject
            {
                ["geschoss"] = g.Kennzeichen, ["name"] = g.Name, ["kote"] = g.Kote, ["hoehe"] = g.Hoehe, ["globalId"] = g.GlobalId,
                ["anzahl"] = ctx.Aussparungen.Count(a => a.GeschossId == g.Id),
            }).ToArray()),
            ["aussparungen"] = new JsonArray(ctx.Aussparungen.Select(a => (JsonNode)new JsonObject
            {
                ["aussparung"] = a.Kennzeichen, ["nummer"] = a.Name, ["globalId"] = a.GlobalId,
                ["geschoss"] = a.GeschossId is { } id ? gk[id] : null, ["art"] = a.Art, ["status"] = a.Status, ["beschreibung"] = a.Beschreibung,
            }).ToArray()),
            ["rundreise"] = new JsonObject
            {
                ["ergebnis"] = rr.Abweichungen.Count == 0 ? "bestanden" : "abgewichen",
                ["fassungGelesen"] = rr.Fassung,
                ["einheitGelesen"] = rr.MmJeEinheit == 1 ? "mm" : $"{rr.MmJeEinheit} mm je Einheit",
                ["aussparungenGeprueft"] = rr.Geprueft,
                ["geschosseGeprueft"] = rr.GeschosseGeprueft,
                ["eigenschaftenGeprueft"] = rr.EigenschaftenGeprueft,
                ["entitaeten"] = rr.Entitaeten,
                ["validierung"] = rr.Validierung,
                ["abweichungen"] = new JsonArray(rr.Abweichungen.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            },
            ["warnungen"] = new JsonArray(warnungen.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["hinweise"] = new JsonArray(hinweise.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
        };
    }
}

/// <summary>Alles, was der Schreiber und die Rundreise brauchen.</summary>
public sealed record IfcAusgabe(string Fassung, string Pfad, string ProjektId, string ProjektName, string Akteur, string Koordinaten, IfcBezug? Bezug,
    IReadOnlyList<IfcGeschossDaten> Geschosse, IReadOnlyList<IfcAussparungDaten> Aussparungen, DateTimeOffset Zeit);
