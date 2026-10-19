// IFC-Übertrag HLKS (Modul 1b, B2): Katalogklasse eines IFC-Produkts.
// 1. Abbildung (ifc.json) über IfcAbbildung.KlasseFuer – Entität, Typobjekt, PredefinedType, ObjectType.
// 2. Mehrdeutig (KlasseFuer = null, aber mehrere Kandidaten mit gleicher bester Güte – typisch Revit-IFC2x3 mit
//    PredefinedType NOTDEFINED): bei Formteilen entscheidet die Lage der Ports (3 und mehr → JUNCTION; 2 geknickt → BEND;
//    2 gerade → TRANSITION, ausser beide Ports nennen dieselbe Nennweite – dann ist es eine Muffe, keine Reduktion);
//    geometrisch, nichts geraten. Sonst eindeutige Namensmuster unter den Kandidaten, sonst das Medium des Systems.
//    Bleibt es mehrdeutig: Hintergrund.
// 3. Ohne Kandidaten: nur eindeutige Namensmuster (WC, Waschtisch, Bogen …) und nur für Produkte mit Ports und passender
//    Entitätsfamilie; sonst Hintergrund (NichtVerstandenZaehlen).
// 4. Luftdurchlässe: Zuluft DIFFUSER / Abluft GRILLE ist eine Annahme der Abbildung (ifc.json → annahmen); widerspricht
//    das Medium des Systems und passt genau die andere Klasse, gilt sie (Revit: Gitter «GRILLE» im Zuluftsystem).

using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Gewählte Klasse mit Begründung. ArtEigenschaft/ArtWert: Auswahl-Eigenschaft aus der Abbildung (Variante,
/// predefinedTypeJe). AusName: Klasse nur aus einem Namensmuster.</summary>
public sealed record IfcHlksWahl(string Klasse, string Grund, string? ArtEigenschaft = null, string? ArtWert = null, bool AusName = false, bool AusPorts = false);

public static class IfcHlksKlasse
{
    // Entitätsfamilien (Vorfahren im Schema), in denen ein Namensmuster gelten darf
    static readonly string[] Formteile = ["IfcFlowFitting", "IfcFlowTreatmentDevice", "IfcBuildingElementProxy", "IfcProxy"];
    static readonly string[] Armaturen = ["IfcFlowController", "IfcFlowMovingDevice", "IfcBuildingElementProxy", "IfcProxy"];
    static readonly string[] Geraete = ["IfcFlowTerminal", "IfcFlowStorageDevice", "IfcEnergyConversionDevice", "IfcFlowMovingDevice", "IfcBuildingElementProxy", "IfcProxy"];
    static readonly string[] Verteiler = ["IfcFlowFitting", "IfcFlowStorageDevice", "IfcBuildingElementProxy", "IfcProxy"];

    /// <summary>Eindeutige Namensmuster (Name, ObjectType, Name des Typobjekts; ohne Gross-/Kleinschreibung) – nur, wenn
    /// die Abbildung keine eindeutige Klasse ergibt. Reihenfolge zählt (spezifischere zuerst). Annahme für Christians
    /// Prüfliste; Vorschlag an den Hauptlauf: als «namen» je Klasse nach ifc.json übernehmen.</summary>
    public static readonly IReadOnlyList<(string Klasse, string Muster, string[] Familien)> Namensmuster =
    [
        ("WC", @"\bwc\b|klosett|toilette", Geraete),
        ("Waschtisch", @"waschtisch|lavabo|handwaschbecken", Geraete),
        ("Dusche", @"dusche|duschwanne|duschtasse", Geraete),
        ("Badewanne", @"badewanne", Geraete),
        ("Spueltisch", @"sp(ü|ue)ltisch|sp(ü|ue)lbecken|k(ü|ue)chensp(ü|ue)le", Geraete),
        ("Ausgussbecken", @"ausguss|waschtrog", Geraete),
        ("Waschmaschine", @"waschmaschine", Geraete),
        ("Bodenablauf", @"bodenablauf", Geraete),
        ("Dachablauf", @"dachablauf|dachwassereinlauf|dacheinlauf", Geraete),
        ("Wassererwaermer", @"wassererw(ä|ae)rmer|warmwasserspeicher|boiler", Geraete),
        ("Verteiler", @"verteiler", Verteiler),
        ("Speicher", @"speicher", Geraete),
        ("Heizkoerper", @"heizk(ö|oe)rper|radiator", Geraete),
        ("Lueftungsgeraet", @"l(ü|ue)ftungsger(ä|ae)t|monobloc", Geraete),
        ("Pumpe", @"pumpe(?!n)", Armaturen),
        ("Wasserzaehler", @"wasserz(ä|ae)hler", Armaturen),
        ("Waermezaehler", @"w(ä|ae)rmez(ä|ae)hler", Armaturen),
        ("Ventil", @"ventil(?!ator)|hahn|schieber|absperrklappe|absperrarmatur", Armaturen),
        ("Reinigungsoeffnung", @"reinigung", Formteile),
        ("TStueck", @"t-st(ü|ue)ck|tst(ü|ue)ck|abzweig", Formteile),
        ("Reduktion", @"reduktion|reduzier|(ü|ue)bergang", Formteile),
        ("Bogen", @"bogen", Formteile),
    ];

    /// <summary>Gehört die Klasse zum Bereich HLKS (konkret, mit Rolle im Netz)?</summary>
    public static bool IstHlks(Katalog kat, string klasse) => kat.Klassen.TryGetValue(klasse, out var d) && !d.Abstrakt && d.Rolle is not null;

    /// <summary>Bestimmt die Klasse; null mit Grund, wenn es keine eindeutige gibt.</summary>
    /// <param name="medien">Medien der Systeme des Produkts und seiner Ports (leer = unbekannt).</param>
    /// <param name="portDn">Nennweite eines Ports aus seinen Psets (null = unbekannt).</param>
    public static (IfcHlksWahl? Wahl, string? Grund) Bestimmen(IfcLauf lauf, IIfcProduct x, IReadOnlyList<IfcHlksPort> ports,
        IReadOnlySet<string> medien, Func<IfcHlksPort, double?> portDn)
    {
        var d = lauf.Datei; var abb = lauf.Abbildung; var kat = lauf.Katalog;
        var ent = IfcDatei.Entitaet(x);
        var typ = d.Typ(x) is { } t ? IfcDatei.Entitaet(t) : null;
        var pt = d.PredefinedType(x);
        var ot = d.ObjectType(x);
        var treffer = abb.KlasseFuer(d.Schema, ent, typ, pt, ot);   // null = keine oder mehrdeutig
        var alle = abb.Kandidaten(d.Schema, ent, typ, pt, ot);
        IfcHlksWahl? wahl;

        if (treffer is not null)
        {
            if (!IstHlks(kat, treffer.Klasse)) return (null, $"{treffer.Klasse} gehört nicht zum Bereich HLKS");
            // Nur die Entität passt, obwohl der Typwert die Klasse unterscheiden würde (Güte 0), und keine Ports: unsicher
            // (Revit exportiert Architektur-Objekte als IfcFlowTerminal/IfcDistributionChamberElement) – nur mit passendem Namen
            if (treffer.Guete <= 1 && ports.Count == 0 && TypwertUnterscheidet(lauf, treffer.Klasse) && NamePasst(x, d, treffer.Klasse) is null)
                return (null, $"{treffer.Klasse} nur über die Entität erkannt ({ent}{(typ is null ? "" : " " + typ)}{(pt is null ? "" : " " + pt)}), ohne Ports und ohne passenden Namen");
            wahl = MitArt(lauf, new IfcHlksWahl(treffer.Klasse, treffer.Grund), treffer, pt);
        }
        else if (alle.Count > 0)
        {
            var beste = alle.Where(k => k.Guete == alle[0].Guete).Select(k => k.Klasse).Distinct().ToList();
            var hlks = beste.Where(k => IstHlks(kat, k)).ToList();
            if (hlks.Count == 0) return (null, $"{string.Join(", ", beste)} gehört nicht zum Bereich HLKS");
            wahl = hlks.Count == 1 ? new IfcHlksWahl(hlks[0], alle.First(k => k.Klasse == hlks[0]).Grund)
                 : AusPorts(lauf, ent, typ, ot, ports, hlks, portDn)
                   ?? AusNamen(x, d, ports, hlks.ToHashSet())
                   ?? AusMedium(lauf, hlks, medien);
            if (wahl is null)
                return (null, $"mehrdeutig ({string.Join(", ", hlks)}): {ent}{(typ is null ? "" : " " + typ)}{(pt is null ? "" : " " + pt)}"
                              + (ports.Count == 0 ? ", ohne Ports" : ""));
            if (alle.FirstOrDefault(k => k.Klasse == wahl.Klasse) is { } tw) wahl = MitArt(lauf, wahl, tw, pt);
        }
        else
        {
            wahl = AusNamen(x, d, ports, null);
            if (wahl is null)
                return (null, $"keine Klasse für {ent}{(typ is null ? "" : " " + typ)}{(pt is null or "NOTDEFINED" ? "" : " " + pt)}");
        }

        // Luftdurchlässe: Medium des Systems vor der Annahme DIFFUSER/GRILLE
        if (medien.Count > 0 && !MediumPasst(kat, wahl.Klasse, medien) && kat.Klassen[wahl.Klasse].Gewerk == "lueftung")
        {
            var andere = abb.Kandidaten(d.Schema, ent, typ, null, null).Select(k => k.Klasse).Distinct()
                .Where(k => k != wahl.Klasse && IstHlks(kat, k) && kat.Klassen[k].Gewerk == "lueftung" && FesteMedien(kat, k).Count > 0 && MediumPasst(kat, k, medien)).ToList();
            if (andere.Count == 1)
                wahl = new IfcHlksWahl(andere[0], $"{wahl.Grund}; Medium des Systems ({string.Join(", ", medien.Order())}) passt zu {andere[0]}, nicht zu {wahl.Klasse}");
        }
        return (wahl, null);
    }

    /// <summary>Formteile: PredefinedType aus der Lage der Ports ableiten und die Abbildung erneut fragen.</summary>
    static IfcHlksWahl? AusPorts(IfcLauf lauf, string ent, string? typ, string? ot, IReadOnlyList<IfcHlksPort> ports, List<string> kandidaten,
        Func<IfcHlksPort, double?> portDn)
    {
        if (!kandidaten.All(k => lauf.Katalog.Klassen[k].IstArt("Formteil") || k == "Hausanschluss" || k == "Verteiler")) return null;
        string? pt = null, warum = null;
        if (ports.Count >= 3) { pt = "JUNCTION"; warum = $"{ports.Count} Ports"; }
        else if (ports.Count == 2 && IfcHlksAnschluesse.Umlenkwinkel(ports[0], ports[1], null) is double w)
        {
            if (w > 5) { pt = "BEND"; warum = $"2 Ports, Umlenkwinkel {w:0.#}°"; }
            else if (portDn(ports[0]) is double a && portDn(ports[1]) is double b && Math.Abs(a - b) < 0.5) return null;   // Muffe: gleiche Nennweite
            else { pt = "TRANSITION"; warum = "2 Ports in einer Achse"; }
        }
        if (pt is null) return null;
        var t = lauf.Abbildung.KlasseFuer(lauf.Datei.Schema, ent, typ, pt, ot);
        return t is null || !kandidaten.Contains(t.Klasse) ? null : new IfcHlksWahl(t.Klasse, $"{t.Grund} (abgeleitet: {warum})", AusPorts: true);
    }

    /// <summary>Eindeutiges Namensmuster (nur Produkte mit Ports, nur passende Entitätsfamilie, ggf. nur unter Kandidaten).</summary>
    static IfcHlksWahl? AusNamen(IIfcProduct x, IfcDatei d, IReadOnlyList<IfcHlksPort> ports, IReadOnlySet<string>? nurUnter)
    {
        if (ports.Count == 0) return null;
        foreach (var (klasse, _, _) in Namensmuster)
        {
            if (nurUnter is not null && !nurUnter.Contains(klasse)) continue;
            if (NamePasst(x, d, klasse) is { } treffer) return new IfcHlksWahl(klasse, $"Name «{treffer}» ({IfcDatei.Entitaet(x)})", AusName: true);
        }
        return null;
    }

    /// <summary>Passendes Namensmuster einer Klasse (Name, ObjectType, Typobjekt; Entitätsfamilie muss passen) oder null.</summary>
    static string? NamePasst(IIfcProduct x, IfcDatei d, string klasse)
    {
        var text = string.Join(" | ", new[] { IfcLauf.Text(x.Name), IfcLauf.Text(x.ObjectType), d.Typ(x) is { } t ? IfcLauf.Text(t.Name) : null }.Where(s => s is not null));
        if (text.Length == 0) return null;
        foreach (var (k, muster, familien) in Namensmuster)
        {
            if (k != klasse || !familien.Any(f => IfcHlksHilfe.IstArt(x, f))) continue;
            var m = Regex.Match(text, muster, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success) return m.Value;
        }
        return null;
    }

    /// <summary>Unterscheidet der PredefinedType die Klasse (Ziel mit Typwert, keine abschliessende importWerte-Liste)?</summary>
    static bool TypwertUnterscheidet(IfcLauf lauf, string klasse) =>
        lauf.Abbildung.Klasse(klasse) is { } k && !(k.ImportWerte?.Count > 0) && k.Ziele(lauf.Datei.Schema).Any(z => z.Ziel.PredefinedType is not null);

    /// <summary>Unter mehrdeutigen Kandidaten die einzige, deren feste Anschlussmedien zum System passen.</summary>
    static IfcHlksWahl? AusMedium(IfcLauf lauf, List<string> kandidaten, IReadOnlySet<string> medien)
    {
        if (medien.Count == 0) return null;
        var passend = kandidaten.Where(k => FesteMedien(lauf.Katalog, k).Count > 0 && MediumPasst(lauf.Katalog, k, medien)).ToList();
        return passend.Count == 1 ? new IfcHlksWahl(passend[0], $"Medium des Systems ({string.Join(", ", medien.Order())})") : null;
    }

    /// <summary>Feste Anschlussmedien einer Klasse (ohne «*»).</summary>
    public static HashSet<string> FesteMedien(Katalog kat, string klasse) =>
        kat.Klassen.TryGetValue(klasse, out var d) ? d.Anschluesse.Where(a => a.Medium != "*").Select(a => a.Medium).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];

    /// <summary>Passt die Klasse zu den Medien? Ohne feste Medien immer.</summary>
    public static bool MediumPasst(Katalog kat, string klasse, IReadOnlySet<string> medien)
    {
        var f = FesteMedien(kat, klasse);
        return f.Count == 0 || f.Overlaps(medien);
    }

    /// <summary>Auswahl-Eigenschaft aus der Abbildung: Variante (z. B. Wärmeerzeuger art = fernwaerme), wenn der
    /// PredefinedType sie bestätigt; bei Ventilen art aus predefinedTypeJe (nur eindeutige, nicht USERDEFINED).</summary>
    static IfcHlksWahl MitArt(IfcLauf lauf, IfcHlksWahl wahl, IfcTreffer treffer, string? pt)
    {
        var k = lauf.Abbildung.Klasse(wahl.Klasse);
        if (k is null) return wahl;
        if (treffer.Klasse == wahl.Klasse && treffer.Variante is not null && treffer.Guete >= 2 && k.VarianteJe is not null)
            return wahl with { ArtEigenschaft = k.VarianteJe.Eigenschaft, ArtWert = treffer.Variante };
        var p = pt?.ToUpperInvariant();
        if (k.PredefinedTypeJe is { } je && p is not null and not ("NOTDEFINED" or "USERDEFINED"))
        {
            var werte = je.Werte.Where(w => w.Value.Equals(p, StringComparison.OrdinalIgnoreCase)).Select(w => w.Key).ToList();
            if (werte.Count == 1) return wahl with { ArtEigenschaft = je.Eigenschaft, ArtWert = werte[0] };
        }
        return wahl;
    }
}

/// <summary>Kleine Hilfen des HLKS-Übertrags.</summary>
public static class IfcHlksHilfe
{
    /// <summary>Ist die Entität (oder einer ihrer Vorfahren im Schema) die genannte? Bewusst über den Schemanamen und nicht
    /// über die IFC4-Schnittstellen: in IFC2x3-Dateien liefert xBIM z. B. IfcWall auch für IIfcDistributionElement.</summary>
    public static bool IstArt(IPersistEntity e, string entitaet)
    {
        for (var t = e.ExpressType; t is not null; t = t.SuperType)
            if (t.Name.Equals(entitaet, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
