// Kennzeichnung der Aussparungen nach SIA (Modul 3b, Nachtrag B4d; Befund Christian 19.10.2026 «Bei der Darstellung der Aussparungen
// wurde nicht der SIA-Standard verwendet»): Kurzzeichen, Lage der Bauteile (horizontal/vertikal) und Gewerk an EINER Stelle – für die
// Grafik (Sinnbild, Beschriftung, Legende, AussparungGrafik.cs), die Aussparungsliste (Feld kurzzeichen, AussparungAbgleich.cs) und die
// IFC-Ausgabe (Property Set KICAD_Aussparung, Eigenschaft Kurzzeichen, AussparungIfcExport.cs).
//
// QUELLE: SIA 400:2000 Ziff. B.10 «Darstellung von Aussparungen» (S. 41–42; Quelle dort: Empfehlung SIA 410/2) und SIA 410/2
// Ziff. 2 1 «Bezeichnungen, Kurzzeichen, Sinnbilder» (S. 12) – zusammengefasst in entwurf/modul-3b.md, Abschnitt «SIA 400 / SIA 410/2»:
//   - Durchbrüche (SIA 400 B.10.2 Tabelle 5): WD Wanddurchbruch, DD Deckendurchbruch, BD Bodendurchbruch, FD Fundamentdurchbruch.
//     (Futterrohre WFR/DFR/BFR/FFR, Schlitze WS/DS/BS/FS, VM, AS erzeugt KICAD heute nicht.)
//   - Zusätzliches Kurzzeichen der Arbeitsgattung davor: E Elektrische Installationen, H Heizung, L Lüftung und Klima, K Kälte,
//     S Sanitär – «S DD»; ohne Arbeitsgattung allgemein («DD», Figur 49: «DD Allgemeiner Deckendurchbruch»).
//   - Zusätzliche Kennzeichnung nach der Lage des Bauteils (Tabelle 5): Decken und Bodenkonstruktionen (horizontal), Wände und
//     Pfeiler (vertikal), beides (Aussparungen, die gleichzeitig durch horizontale und vertikale Bauteile gehen).
//
// REGELN DES KERNS (Auftrag B4d):
//   Lage je Bauteil der Aussparung (bauteil, bauteil2 … – durchgehende Aussparung, Nachtrag B3c): KICAD Decke, Dach → horizontal;
//     Wand, Stütze, Träger → vertikal (Träger: ANNAHME vertikal – der Durchbruch geht quer durch den Steg); Hintergrund nach der
//     IFC-Entität (IfcSlab…, IfcRoof → horizontal; IfcWall…, IfcColumn…, IfcBeam… → vertikal). Ist kein Bauteil bestimmbar, gilt die
//     Achse (|z| ≥ 0.5 → horizontal, wie Aussparungsgeometrie.Kanonisch). Horizontale und vertikale zusammen → «beides».
//   Art aus dem Haupt-Bauteil (bauteil; ohne Lage das erste mit Lage, sonst die Achse): vertikal → WD; horizontal → DD; horizontal
//     ohne Geschoss unter der Unterkante (Bodenplatte bzw. Fundament) → FD – dieselbe Höhenband-Regel wie die Geschoss-Regel im
//     Vorschlag (AussparungVorschlag.GeschossVon, Hauptlauf 19.10.2026: Höhe Unterkante − 1 mm im Band kote − 1 ≤ z < kote + hoehe
//     eines Geschosses). BD (Bodenkonstruktion = nichttragende Überkonstruktion, SIA 410/2 Ziff. 3 5) erkennt der Kern nicht:
//     Aussparungen gibt es nur im Rohbau (Nachtrag B3c).
//   Gewerk aus den Leitungen (Ableitungen.Gewerk: Systemtyp des Systems, sonst Katalog): sanitaer S, heizung H, lueftung L,
//     kaelte K, elektro E; msrl → E (ANNAHME Prüfliste: MSRL-Leitungen sind elektrische Installationen). Haben alle Leitungen dasselbe
//     Kurzzeichen, steht es davor; mehrere Gewerke, ein unbekanntes Gewerk oder keine Leitung → kein Gewerk-Kurzzeichen (allgemein).
//   Kernbohrung (rund, nur von Hand): ANNAHME runder Durchbruch – SIA 400/410 regeln Kernbohrungen nicht eigens.

namespace Hlks.Kern;

/// <summary>Lage der Bauteile einer Aussparung nach SIA 400:2000 B.10.2 Tabelle 5 «zusätzliche Kennzeichnungen».</summary>
public enum Bauteillage { Horizontal, Vertikal, Beides }

/// <summary>Kennzeichnung einer Aussparung: Art (WD, DD, FD), Gewerk-Kurzzeichen (E, H, L, K, S oder null = allgemein) und Lage.</summary>
public sealed record AussparungKennung(string Art, string? Gewerk, Bauteillage Lage)
{
    /// <summary>Kurzzeichen, z. B. «S DD», «H WD», allgemein «DD».</summary>
    public string Kurzzeichen => Gewerk is null ? Art : $"{Gewerk} {Art}";

    /// <summary>Ausgeschrieben, z. B. «Sanitär Deckendurchbruch», allgemein «allgemeiner Deckendurchbruch».</summary>
    public string Text => Gewerk is { } g && AussparungKennzeichnung.GewerkTexte.TryGetValue(g, out var gt)
        ? $"{gt} {AussparungKennzeichnung.ArtTexte.GetValueOrDefault(Art, Art)}"
        : $"allgemeiner {AussparungKennzeichnung.ArtTexte.GetValueOrDefault(Art, Art)}";

    /// <summary>Gehört die Aussparung zu einem horizontalen Bauteil (allein oder zusammen mit einem vertikalen)?</summary>
    public bool Horizontal => Lage != Bauteillage.Vertikal;
}

public static class AussparungKennzeichnung
{
    /// <summary>Fundstelle für Dateiköpfe, Legende und Herkunft.</summary>
    public const string Quelle = "SIA 400:2000 Ziff. B.10 (S. 41–42) / SIA 410/2 Ziff. 2 1 (S. 12) und 3 2–3 4 (S. 15)";

    public const string WD = "WD", DD = "DD", BD = "BD", FD = "FD";

    /// <summary>Durchbrüche nach SIA 400:2000 B.10.2 Tabelle 5.</summary>
    public static readonly IReadOnlyDictionary<string, string> ArtTexte = new Dictionary<string, string>
    {
        [WD] = "Wanddurchbruch", [DD] = "Deckendurchbruch", [BD] = "Bodendurchbruch", [FD] = "Fundamentdurchbruch",
    };

    /// <summary>Kurzzeichen der Arbeitsgattung nach SIA 400:2000 B.10.2 (unter Tabelle 5).</summary>
    public static readonly IReadOnlyDictionary<string, string> GewerkTexte = new Dictionary<string, string>
    {
        ["E"] = "Elektro", ["H"] = "Heizung", ["L"] = "Lüftung/Klima", ["K"] = "Kälte", ["S"] = "Sanitär",
    };

    /// <summary>Gewerk des Katalogs → Kurzzeichen der Arbeitsgattung. msrl → E ist eine ANNAHME (Dateikopf).</summary>
    public static readonly IReadOnlyDictionary<string, string> GewerkKurz = new Dictionary<string, string>
    {
        ["sanitaer"] = "S", ["heizung"] = "H", ["lueftung"] = "L", ["kaelte"] = "K", ["elektro"] = "E", ["msrl"] = "E",
    };

    /// <summary>Kennzeichnung der Aussparung (Regeln im Dateikopf); null, wenn weder ein Bauteil noch die Achse die Lage sagen.</summary>
    public static AussparungKennung? Von(Modell m, Katalog k, Element a)
    {
        var lage = Aussparungslage.Von(a);
        var lagen = BauteilLagen(m, k, a);
        Bauteillage? haupt = lagen.Count > 0 && lagen[0] is { } h0 ? h0 : lagen.FirstOrDefault(x => x is not null);
        haupt ??= AchsLage(a, lage);
        if (haupt is not { } hl) return null;
        var bekannt = lagen.OfType<Bauteillage>().ToList();
        if (bekannt.Count == 0) bekannt.Add(hl);
        var gesamt = bekannt.Contains(Bauteillage.Horizontal) && bekannt.Contains(Bauteillage.Vertikal) ? Bauteillage.Beides : hl;
        string art = hl == Bauteillage.Vertikal ? WD : OhneGeschossDarunter(m, a, lage) ? FD : DD;
        return new AussparungKennung(art, Gewerk(m, k, a), gesamt);
    }

    /// <summary>Kurzzeichen («S DD», «H WD», «DD») oder null (Lage nicht bestimmbar).</summary>
    public static string? Kurzzeichen(Modell m, Katalog k, Element a) => Von(m, k, a)?.Kurzzeichen;

    /// <summary>Lage je Bauteil der Aussparung in der Folge Haupt-Bauteil, bauteil2 …; null = nicht bestimmbar (gelöscht, unbekannte
    /// Klasse bzw. Entität).</summary>
    public static List<Bauteillage?> BauteilLagen(Modell m, Katalog k, Element a)
    {
        var aus = new List<Bauteillage?>();
        var nummern = new List<int> { 1 };
        nummern.AddRange(Aussparungsbezug.WeitereBauteile(a).Select(x => x.Nummer));
        foreach (var i in nummern.Distinct())
        {
            var schluessel = Aussparungsbezug.BauteilSchluessel(a, i);
            if (schluessel is null) { if (i == 1) aus.Add(null); continue; }
            var endung = i <= 1 ? "" : i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            aus.Add(m.Finde(schluessel) is { } e ? LageVonElement(k, e) : LageVonEntitaet(a.Kennungen?.GetValueOrDefault("bauteilEntitaet" + endung)));
        }
        return aus;
    }

    /// <summary>Lage eines KICAD-Bauteils: Decke, Dach horizontal; Wand, Stütze, Träger vertikal (Träger: ANNAHME).</summary>
    public static Bauteillage? LageVonElement(Katalog k, Element e) =>
        k.IstArt(e, "Decke") || k.IstArt(e, "Dach") ? Bauteillage.Horizontal
        : k.IstArt(e, "Wand") || k.IstArt(e, "Stuetze") || k.IstArt(e, "Traeger") ? Bauteillage.Vertikal
        : null;

    /// <summary>Lage eines Hintergrund-Produkts nach seiner IFC-Entität.</summary>
    public static Bauteillage? LageVonEntitaet(string? entitaet)
    {
        if (string.IsNullOrEmpty(entitaet)) return null;
        var en = entitaet.ToUpperInvariant();
        if (en.StartsWith("IFCSLAB", StringComparison.Ordinal) || en == "IFCROOF") return Bauteillage.Horizontal;
        if (en.StartsWith("IFCWALL", StringComparison.Ordinal) || en.StartsWith("IFCCOLUMN", StringComparison.Ordinal) || en.StartsWith("IFCBEAM", StringComparison.Ordinal))
            return Bauteillage.Vertikal;
        return null;
    }

    /// <summary>Lage aus der Achse (ohne bestimmbares Bauteil): steile Achse = Decke.</summary>
    static Bauteillage? AchsLage(Element a, Aussparungslage? lage)
    {
        if (lage is not null) return Math.Abs(lage.Achse.Z) >= 0.5 ? Bauteillage.Horizontal : Bauteillage.Vertikal;
        if (a.Eigenschaft("achse")?.Inhalt is System.Text.Json.Nodes.JsonArray ar && ar.Count >= 3)
        {
            var v = new V3(Json.Zahl(ar[0]) ?? 0, Json.Zahl(ar[1]) ?? 0, Json.Zahl(ar[2]) ?? 0);
            if (v.Laenge > 1e-9) return Math.Abs(v.Einheit().Z) >= 0.5 ? Bauteillage.Horizontal : Bauteillage.Vertikal;
        }
        return null;
    }

    /// <summary>Gewerk-Kurzzeichen aus den Leitungen (Dateikopf) oder null (allgemein).</summary>
    public static string? Gewerk(Modell m, Katalog k, Element a)
    {
        var kurz = new HashSet<string>(StringComparer.Ordinal);
        int n = 0;
        foreach (var id in Aussparungsbezug.Leitungen(a))
        {
            n++;
            if (m.Finde(id) is not { } l || Ableitungen.Gewerk(m, k, l) is not { } g || !GewerkKurz.TryGetValue(g, out var kz)) return null;   // unbekannt
            kurz.Add(kz);
        }
        return n > 0 && kurz.Count == 1 ? kurz.First() : null;
    }

    /// <summary>Liegt unter der Unterkante der Aussparung kein Geschoss (Bodenplatte bzw. Fundament → FD)? Höhenband wie die
    /// Geschoss-Regel des Vorschlags: z = Unterkante − 1 mm, Geschoss mit kote − 1 ≤ z &lt; kote + hoehe. Ohne Unterkante: false.</summary>
    public static bool OhneGeschossDarunter(Modell m, Element a, Aussparungslage? lage = null)
    {
        lage ??= Aussparungslage.Von(a);
        double? uk = lage is not null ? lage.Koten().Uk : a.Text("form") == "rund" ? null : a.Zahl("kote_uk");
        if (uk is not double u) return false;
        double z = u - 1;
        return !m.DerKlasse("Geschoss").Any(g => g.Zahl("kote") is double kt && g.Zahl("hoehe") is double h && z >= kt - 1 && z < kt + h);
    }
}
