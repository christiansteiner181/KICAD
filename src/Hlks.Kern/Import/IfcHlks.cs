// IFC-Übertrag HLKS (Modul 1b, B2): Leitungen, Formteile, Armaturen, Apparate und Geräte eines Fachplaner-IFC werden
// eigene, bearbeitbare KICAD-Elemente – mit Anschlüssen aus den Ports, Verbindungen aus IfcRelConnectsPorts und Systemen
// aus IfcSystem/IfcDistributionSystem (IfcHlksSysteme). Verbindlich: entwurf/modul-1b.md, Abschnitt B2.
// Grundsätze: Klasse über die Abbildung (ifc.json, IfcHlksKlasse); nichts erfinden – was die Datei nicht sagt, bleibt
// leer (Hinweis); keine Nennweite aus dem Aussendurchmesser (das wäre ein Normwert aus einer Rohrreihe) und keiner umgekehrt.
// Querschnitte von Kanälen sind Geometrie und dürfen aus dem Körper kommen.
// Aussendurchmesser der Rohre (Leitung.da, Modul 3 B9b, Entscheid Christian 19.10.2026: Abwasserrohre werden mit dem Aussendurchmesser
// bezeichnet): zuerst aus einem Pset des Rohrs oder seines Typs (Aussendurchmesser, Outside/Outer Diameter – Fundstelle «Pset.Name»,
// Kandidaten aus ifc.json psets.da, sonst diese Namen), sonst aus dem Rohrkörper (Profil bzw. Punkte quer zur Achse – Geometrie der Datei,
// kein Normwert), auf 0.1 mm gerundet, mit Herkunft. Ein Kürzel («PE-S») übernimmt nur, was die Datei als Material nennt.
// Geprüft an Trimble Nova (IFC4, BKW Hauptsitz) und Revit 2025 (IFC2x3, Bernstrasse 42) – Befunde im Bericht B2.

using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcHlksUebertrag : IIfcUebertrag
{
    static IReadOnlySet<string>? klassen;

    public string Bereich => "hlks";

    /// <summary>Alle konkreten Katalogklassen mit Rolle im Netz sowie System und Strang. Anlagen nicht: eine Anlage je
    /// Gewerk kann Systeme mehrerer Unterlagen tragen und wird darum bei einer neuen Fassung nie gelöscht.</summary>
    public IReadOnlySet<string> Klassen => klassen ??= KlassenAus(Katalog.Laden(Katalog.StandardOrdner()));

    public static IReadOnlySet<string> KlassenAus(Katalog k) =>
        k.Klassen.Values.Where(d => !d.Abstrakt && d.Rolle is not null).Select(d => d.Name).Append("System").Append("Strang")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Uebertragen(IfcLauf lauf)
    {
        klassen = KlassenAus(lauf.Katalog);
        new IfcHlksArbeit(lauf).Ausfuehren();
    }
}

/// <summary>Ein Lauf des HLKS-Übertrags (eigener Zustand je Aufruf – der Übertrager selbst wird geteilt).</summary>
sealed class IfcHlksArbeit(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly Katalog kat = lauf.Katalog;
    readonly IfcAbbildung abb = lauf.Abbildung;
    IfcHlksPorts ports = null!;
    IfcHlksSysteme systeme = null!;
    List<Element> raeume = [];

    /// <summary>Geplante Produkte: EntityLabel → Zuordnung Port → Anschluss.</summary>
    readonly Dictionary<int, IfcHlksZuordnung> geplant = [];
    readonly SortedDictionary<string, int> jeKlasse = new(StringComparer.Ordinal);
    readonly Dictionary<string, (int Anzahl, List<string> Beispiele)> notizen = [];
    readonly Dictionary<string, int> nichtUebernommen = [];
    int richtungOffen, verbunden, verbindungenOffen, anschluesse;

    public void Ausfuehren()
    {
        var uhr = Stopwatch.StartNew();
        ports = IfcHlksPorts.Lesen(d, abb);
        systeme = IfcHlksSysteme.Lesen(lauf);
        raeume = Raeume();
        foreach (var x in Produkte()) Planen(x);
        Verbinden();
        Zusammenfassen(uhr.Elapsed);
    }

    /// <summary>Produkte des Bereichs: Verteilelemente (Fluss und Regelung, ausgewählt über den Schemanamen) sowie
    /// Platzhalter (Proxy) mit Ports; nur im Geschossfilter, in Dateireihenfolge.</summary>
    IEnumerable<IIfcProduct> Produkte() =>
        d.Modell.Instances.OfType<IIfcProduct>()
            .Where(x => x is not IIfcDistributionPort
                        && (IfcHlksHilfe.IstArt(x, "IfcDistributionFlowElement") || IfcHlksHilfe.IstArt(x, "IfcDistributionControlElement")
                            || ((IfcHlksHilfe.IstArt(x, "IfcBuildingElementProxy") || IfcHlksHilfe.IstArt(x, "IfcProxy")) && ports.Von(x).Count > 0))
                        && !lauf.Behandelt.Contains(x.EntityLabel) && lauf.ImFilter(x))
            .OrderBy(x => x.EntityLabel).ToList();

    // ------------------------------------------------------------------ Element planen

    void Planen(IIfcProduct x)
    {
        var px = ports.Von(x);
        var eigeneSysteme = systeme.Von(x);
        var medien = Medien(x, px);
        var (wahl, grund) = IfcHlksKlasse.Bestimmen(lauf, x, px, medien, PortDn);
        if (wahl is null) { NichtUebernommen(x, grund!); return; }
        var def = kat.Klassen[wahl.Klasse];
        bool leitungsbauteil = def.IstArt("Leitung") || def.IstArt("Formteil") || def.IstArt("Armatur");
        if (wahl.AusName) Notiz($"Klasse aus dem Namen: {wahl.Klasse} ({IfcDatei.Entitaet(x)})", Kurzname(x));
        if (wahl.AusPorts) Notiz($"Klasse aus der Lage der Ports: {wahl.Klasse} ({IfcDatei.Entitaet(x)} ohne PredefinedType)", Kurzname(x));
        if (medien.Count > 0 && !IfcHlksKlasse.MediumPasst(kat, wahl.Klasse, medien))
            Notiz($"Klasse {wahl.Klasse} passt nicht zum Medium des Systems ({string.Join(", ", medien.Order())})", Kurzname(x));

        // System: eigene Gruppe, sonst die einzige der Ports
        var sysKandidaten = eigeneSysteme.Where(s => s.Systemtyp is not null).ToList();
        if (sysKandidaten.Count == 0)
            sysKandidaten = px.SelectMany(p => systeme.Von(p.Port)).Where(s => s.Systemtyp is not null).Distinct().ToList();
        IfcHlksSystem? sys = sysKandidaten.Count == 1 || (leitungsbauteil && sysKandidaten.Count > 1) ? sysKandidaten.OrderBy(s => s.Basis, StringComparer.Ordinal).First() : null;
        if (leitungsbauteil && sysKandidaten.Count > 1) Notiz("Leitungsbauteile in mehreren Systemen (das erste nach Namen gilt)", Kurzname(x));

        var koerper = new Lazy<IfcHlksKoerper?>(() => IfcHlksKoerper.Lesen(d, x));
        var lage = d.Lage(x).Ursprung;

        // Geometrie (vor dem Planen: ohne Verlauf bzw. ganz ohne Körper und Ports kein Element)
        Geometrie geo;
        IfcHlksZuordnung zu;
        if (def.Geometrie is "polylinie" or "linie")
        {
            zu = IfcHlksAnschluesse.Zuordnen(def, px, PortMedium, null);
            if (Verlauf(x, px, zu, koerper) is not { } v) { NichtUebernommen(x, $"{wahl.Klasse} ohne Verlauf (weder zwei Ports noch eine Achse im Körper)"); return; }
            geo = new Geometrie { Art = "polylinie", Punkte = [Runden(v.A), Runden(v.B)] };
        }
        else
        {
            if (px.Count == 0 && x.Representation is null) { NichtUebernommen(x, $"{wahl.Klasse} ohne Körper und ohne Ports"); return; }
            geo = new Geometrie { Art = "punkt", Punkte = [Runden(Punkt(x, def, px, lage, koerper))] };
            zu = IfcHlksAnschluesse.Zuordnen(def, px, PortMedium, geo.Punkte[0]);
        }

        var e = lauf.Neu(wahl.Klasse, x);
        e.Geometrie = geo;
        e.Geschoss ??= GeschossAusHoehe(geo.Punkte[0][2]);
        jeKlasse[wahl.Klasse] = jeKlasse.GetValueOrDefault(wahl.Klasse) + 1;

        // Zuordnung zu System und Strang
        var strang = leitungsbauteil ? systeme.StrangPlanen(x) : null;
        if (strang is { } st) { e.TeilVon = st.Strang; e.System = st.System; }
        else if (sys is not null) e.System = systeme.Planen(sys);
        if (e.System is null && leitungsbauteil)
        {
            var ohneTyp = eigeneSysteme.Where(s => s.Systemtyp is null).Select(s => s.Basis).Distinct().ToList();
            Notiz(ohneTyp.Count > 0 ? "Leitungsbauteile ohne System (ihr IFC-System hat keinen Systemtyp)" : "Leitungsbauteile ohne System (keinem IFC-System zugeordnet)",
                ohneTyp.Count > 0 ? string.Join("/", ohneTyp) : Kurzname(x));
        }

        Eigenschaften(e, x, def, wahl, px, koerper);
        Anschluesse(e, def, px, zu, leitungsbauteil);
        if (def.BrauchtRaum && geo.Art == "punkt") e.Raum = Raum(x, e);
        geplant[x.EntityLabel] = zu;
    }

    /// <summary>Medien der Systeme des Produkts und seiner Ports; fehlen sie, die der verbundenen Ports und Produkte.</summary>
    HashSet<string> Medien(IIfcProduct x, IReadOnlyList<IfcHlksPort> px)
    {
        var m = systeme.Von(x).Concat(px.SelectMany(p => systeme.Von(p.Port))).Select(s => s.Medium).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (m.Count == 0) foreach (var p in px) if (Gegenmedium(p) is { } g) m.Add(g);
        return m;
    }

    /// <summary>Medium eines Ports: sein eigenes System (Revit ordnet Ports Systemen zu), sonst das gegenüber.</summary>
    string? PortMedium(IfcHlksPort p) => Eindeutig(systeme.Von(p.Port)) ?? Gegenmedium(p);

    string? Gegenmedium(IfcHlksPort p) => p.Gegenueber is { } g ? Eindeutig(systeme.Von(g.Port)) ?? Eindeutig(systeme.Von(g.Element)) : null;

    static string? Eindeutig(IReadOnlyList<IfcHlksSystem> s) =>
        s.Select(x => x.Medium).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList() is { Count: 1 } l ? l[0] : null;

    /// <summary>Verlauf einer Leitung: Lagen der beiden End-Ports (anfang → ende); sonst Achse der Extrusion bzw. Hauptachse
    /// des Körpers, mit dem einen Port als Ende, wenn es ihn gibt.</summary>
    (double[] A, double[] B)? Verlauf(IIfcProduct x, IReadOnlyList<IfcHlksPort> px, IfcHlksZuordnung zu, Lazy<IfcHlksKoerper?> koerper)
    {
        var anfang = zu.PortVon("anfang", px); var ende = zu.PortVon("ende", px);
        if (anfang is not null && ende is not null && Geometrie.Abstand(anfang.Lage, ende.Lage) > 1) return (anfang.Lage, ende.Lage);
        var k = koerper.Value;
        var achse = k?.ExtrusionsAchse() ?? k?.Hauptachse();
        if (achse is not { } a) return null;
        Notiz(k!.Extrusion is not null ? "Verlauf aus der Achse der Extrusion (Ports fehlen)" : "Verlauf aus der Längsachse des Körpers (Ports fehlen)", Kurzname(x));
        var einer = anfang ?? ende;
        if (einer is null) return a;
        // Der vorhandene Port ersetzt das nähere Achsende; das andere Ende bleibt
        bool beiA = Geometrie.Abstand(einer.Lage, a.A) <= Geometrie.Abstand(einer.Lage, a.B);
        var weit = beiA ? a.B : a.A;
        return einer == anfang ? (einer.Lage, weit) : (weit, einer.Lage);
    }

    /// <summary>Punkt eines Formteils, einer Armatur, eines Apparats: Lage des Produkts – ausser sie liegt weit neben dem
    /// Körper (Nova setzt alle Produkte auf den Ursprung des Geschosses): dann Mitte des Körpers, bei Verbrauchern Mitte
    /// unten (Apparate stehen auf dem Boden); ohne Körper die Mitte der Ports.</summary>
    double[] Punkt(IIfcProduct x, KlassenDef def, IReadOnlyList<IfcHlksPort> px, double[] lage, Lazy<IfcHlksKoerper?> koerper)
    {
        // Liegt die Lage bei den Ports (Revit), braucht es den Körper nicht
        if (px.Count > 0)
        {
            double spann = px.Max(a => px.Max(b => Geometrie.Abstand(a.Lage, b.Lage)));
            if (px.Min(p => Geometrie.Abstand(p.Lage, lage)) <= Math.Max(1000, 2 * spann)) return lage;
        }
        if (koerper.Value?.Huelle() is { } h)
        {
            const double rand = 100;
            bool innen = Enumerable.Range(0, 3).All(i => lage[i] >= h.Min[i] - rand && lage[i] <= h.Max[i] + rand);
            if (innen) return lage;
            Notiz("Punkt aus der Mitte des Körpers (die Lage des Produkts liegt ausserhalb)", Kurzname(x));
            double[] m = [(h.Min[0] + h.Max[0]) / 2, (h.Min[1] + h.Max[1]) / 2, (h.Min[2] + h.Max[2]) / 2];
            if (def.Rolle == "verbraucher") m[2] = h.Min[2];
            return m;
        }
        if (px.Count > 0 && px.All(p => Geometrie.Abstand(p.Lage, lage) > 5000))
        {
            Notiz("Punkt aus der Mitte der Ports (kein Körper, Lage weit weg)", Kurzname(x));
            return [px.Average(p => p.Lage[0]), px.Average(p => p.Lage[1]), px.Average(p => p.Lage[2])];
        }
        return lage;
    }

    string? GeschossAusHoehe(double z) =>
        lauf.Kontext.Arbeit.DerKlasse("Geschoss").FirstOrDefault(g => g.Zahl("kote") is double k && g.Zahl("hoehe") is double h && z >= k - 1 && z < k + h)?.Id;

    // ------------------------------------------------------------------ Eigenschaften

    void Eigenschaften(Element e, IIfcProduct x, KlassenDef def, IfcHlksWahl wahl, IReadOnlyList<IfcHlksPort> px, Lazy<IfcHlksKoerper?> koerper)
    {
        var eig = d.Eigenschaften(x);
        var abbKlasse = abb.Klasse(def.Name);
        var psets = abbKlasse?.Psets ?? [];

        if (def.IstArt("Leitung") && def.Name != "Kanal" && def.Eigenschaften.Any(p => p.Name == "da")) Aussendurchmesser(e, x, eig, psets, koerper);

        // Nennweite: nur aus Psets des Produkts oder seiner Ports – nie aus dem Aussendurchmesser
        if (def.Eigenschaften.Any(p => p.Name == "dn"))
        {
            if (psets.TryGetValue("dn", out var kand) && DnPset(eig, kand) is { } fund)
                lauf.Wert(e, "dn", fund.Dn, fund.Name);
            else if (px.Count > 0 && px.Select(PortDn).ToList() is var pdn && pdn.All(v => v is not null) && pdn.Distinct().Count() == 1)
                lauf.Wert(e, "dn", pdn[0]!.Value, $"Port {px[0].Name}, {PortDnFundstelle(px[0])}");
            else if (def.IstArt("Leitung") && def.Name != "Kanal")
            {
                Notiz("Rohre ohne Nennweite (die Datei nennt keine; der Aussendurchmesser steht in da, nie als DN)",
                    e.Zahl("da") is double ad ? $"Ø {ad:0.#} mm" : "Ø ?");
            }
        }

        // Übrige Eigenschaften mit Lesekandidaten in ifc.json
        foreach (var (name, kandidaten) in psets)
        {
            if (name is "dn" or "da" || (def.Name == "Kanal" && name is "breite" or "hoehe" or "durchmesser")) continue;
            var ed = def.Eigenschaften.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (ed is null || Pset(eig, kandidaten) is not { } f) continue;
            if (Wert(ed, f.Wert) is { } w) lauf.Wert(e, ed.Name, w, f.Name);
            else Notiz($"{def.Name}.{ed.Name}: Wert in {f.Name} nicht lesbar (Einheit unklar)", Kurzname(x));
        }

        if (def.Name == "Kanal") Querschnitt(e, x, eig, psets, koerper);
        if (def.IstArt("Leitung") && Material(x) is { } mat) lauf.Wert(e, "material", mat, "IfcRelAssociatesMaterial");
        if (wahl.ArtEigenschaft is { } ae && wahl.ArtWert is { } aw && def.Eigenschaften.Any(p => p.Name == ae))
            lauf.Wert(e, ae, aw, $"PredefinedType {d.PredefinedType(x)} (ifc.json)");
        if (def.Name == "Bogen" && px.Count == 2 && IfcHlksAnschluesse.Umlenkwinkel(px[0], px[1], e.Geometrie!.Punkte[0]) is double w2 && w2 > 0.5)
            lauf.Wert(e, "winkel", w2, $"Lage der Ports {px[0].Name}, {px[1].Name}");
    }

    /// <summary>Namen von Pset-Eigenschaften, die den Aussendurchmesser eines Rohrs meinen (Revit deutsch und englisch, IFC4
    /// Pset_PipeSegmentTypeCommon.OuterDiameter), verglichen ohne Gross-/Kleinschreibung, Leerzeichen und Unterstrich, ß = ss. «Grösse» bzw.
    /// «Size» zählt nicht: Revit nennt damit die Nennweite.</summary>
    static readonly HashSet<string> DaNamen = ["aussendurchmesser", "outsidediameter", "outerdiameter"];

    static string Norm(string s) => s.ToLowerInvariant().Replace("ß", "ss").Replace("ö", "oe").Replace("ä", "ae").Replace("ü", "ue").Replace(" ", "").Replace("_", "");

    /// <summary>Aussendurchmesser (mm) aus einem Pset-Wert: Längenmass, Zahl (nur in einer mm-Datei) oder Text «110», «110 mm»; unplausibel → null.</summary>
    double? DaAus(IfcWert w)
    {
        double? v = w.Inhalt is string t && Regex.Match(t, @"^\s*(\d+(?:[.,]\d+)?)\s*(?:mm)?\s*$", RegexOptions.IgnoreCase) is { Success: true } m
            ? double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture) : MmAus(w);
        return v > 0 && v <= 5000 ? Math.Round(v.Value, 1) : null;
    }

    /// <summary>Aussendurchmesser eines Rohrs: Pset (Rohr samt Typ), sonst Rohrkörper. Setzt Leitung.da mit der Fundstelle als Herkunft;
    /// ohne beides bleibt da leer (die Beschriftung zeigt dann keine Grösse und meldet es).</summary>
    void Aussendurchmesser(Element e, IIfcProduct x, Dictionary<string, IfcWert> eig, Dictionary<string, List<string>> psets, Lazy<IfcHlksKoerper?> koerper)
    {
        var kandidaten = psets.TryGetValue("da", out var k) ? k : [];
        foreach (var (schluessel, wert) in eig.OrderBy(z => z.Key, StringComparer.Ordinal))
        {
            var kurz = schluessel[(schluessel.LastIndexOf('.') + 1)..];
            if (!(kandidaten.Contains(schluessel, StringComparer.OrdinalIgnoreCase) || DaNamen.Contains(Norm(kurz)))) continue;
            if (DaAus(wert) is double da) { lauf.Wert(e, "da", da, schluessel); return; }
            Notiz($"Rohr.da: Wert in {schluessel} nicht lesbar (Einheit unklar)", Kurzname(x));
        }
        var q = koerper.Value?.QuerschnittAusProfil() ?? (e.Geometrie is { Punkte.Count: >= 2 } g ? koerper.Value?.QuerschnittAusPunkten(g.Punkte[0], g.Punkte[1]) : null);
        if (q?.Durchmesser is double d && d > 0)
        {
            lauf.Wert(e, "da", Math.Round(d, 1), q.Herkunft + " (Geometrie der Datei, kein Normwert)");
            Notiz("Aussendurchmesser (da) aus dem Rohrkörper (die Datei nennt ihn in keinem Pset)", Kurzname(x));
        }
        else Notiz("Rohre ohne Aussendurchmesser (weder Pset noch kreisförmiger Körper)", Kurzname(x));
    }

    /// <summary>Kanal: Breite/Höhe bzw. Durchmesser aus dem Profil, sonst aus Psets (nur vollständig), sonst aus dem Körper.</summary>
    void Querschnitt(Element e, IIfcProduct x, Dictionary<string, IfcWert> eig, Dictionary<string, List<string>> psets, Lazy<IfcHlksKoerper?> koerper)
    {
        var q = koerper.Value?.QuerschnittAusProfil();
        if (q is null)
        {
            double? Mm(string n) => psets.TryGetValue(n, out var k) && Pset(eig, k) is { } f ? MmAus(f.Wert) : null;
            string? Fund(string n) => psets.TryGetValue(n, out var k) ? Pset(eig, k)?.Name : null;
            if (Mm("durchmesser") is double dm) q = new IfcHlksQuerschnitt(dm, null, null, Fund("durchmesser")!);
            else if (Mm("breite") is double b && Mm("hoehe") is double h) q = new IfcHlksQuerschnitt(null, b, h, $"{Fund("breite")}, {Fund("hoehe")}");
            else if (Mm("breite") is not null || Mm("hoehe") is not null)
                Notiz("Kanäle: Psets nennen nur Breite oder Höhe – Querschnitt aus dem Körper", Kurzname(x));
        }
        if (q is null && e.Geometrie is { Punkte.Count: >= 2 } g) q = koerper.Value?.QuerschnittAusPunkten(g.Punkte[0], g.Punkte[1]);
        if (q is null) { Notiz("Kanäle ohne Querschnitt (weder Profil noch Psets noch Körper)", Kurzname(x)); return; }
        if (q.Durchmesser is double dd) lauf.Wert(e, "durchmesser", dd, q.Herkunft);
        else { lauf.Wert(e, "breite", q.Breite!.Value, q.Herkunft); lauf.Wert(e, "hoehe", q.Hoehe!.Value, q.Herkunft); }
    }

    /// <summary>Erster vorhandener Pset-Wert aus den Kandidaten «Pset.Name» (mit Fundstelle).</summary>
    static (string Name, IfcWert Wert)? Pset(Dictionary<string, IfcWert> eig, IEnumerable<string> kandidaten)
    {
        foreach (var k in kandidaten) if (eig.TryGetValue(k, out var w) && w.Inhalt is not null) return (k, w);
        return null;
    }

    /// <summary>Nennweite aus dem ersten Kandidaten, dessen Wert eine Nennweite ist (Nachbearbeitung Ventil-Pset 20.10.2026): ein Wert, der keine
    /// ist (z. B. Text «15 mm-15 mm»), blockiert die weiteren Kandidaten nicht mehr – so verdrängt Pset_ValveTypeCommon.Size an erster Stelle
    /// (Exportziel) Pset MEP.Geom-DN bzw. Tech-DN nur, wenn Size eine Zahl ist.</summary>
    (string Name, double Dn)? DnPset(Dictionary<string, IfcWert> eig, IEnumerable<string> kandidaten)
    {
        foreach (var k in kandidaten)
            if (eig.TryGetValue(k, out var w) && w.Inhalt is not null && DnAus(w) is double dn) return (k, dn);
        return null;
    }

    /// <summary>Nennweite eines Ports aus seinen Psets (Kandidaten der Rohr-Nennweite in ifc.json).</summary>
    double? PortDn(IfcHlksPort p) => DnKandidaten().Count == 0 ? null : Pset(d.Eigenschaften(p.Port), DnKandidaten()) is { } f ? DnAus(f.Wert) : null;
    string PortDnFundstelle(IfcHlksPort p) => Pset(d.Eigenschaften(p.Port), DnKandidaten())?.Name ?? "";

    List<string>? dnKandidaten;
    /// <summary>Kandidaten der Port-Nennweite: die der Rohr-Nennweite und (Modul 7 Teil B2) die Port-Psets aus ifc.json ports.dn
    /// (IFC4 Pset_DistributionPortTypePipe.NominalDiameter, IFC2x3 HK_KICAD.Nennweite – so schreibt sie der eigene Export).</summary>
    List<string> DnKandidaten() => dnKandidaten ??= abb.Klassen.Where(k => k.Psets?.ContainsKey("dn") == true).SelectMany(k => k.Psets!["dn"])
        .Concat(abb.Ports?.Kandidaten("dn") ?? []).Distinct().ToList();

    /// <summary>Nennweite aus einem Pset-Wert: Längenmass → mm der Datei; reine Zahl oder «DN 100» → wie geschrieben.
    /// Unplausibel (≤ 0 oder über 5000) → leer.</summary>
    double? DnAus(IfcWert w)
    {
        double? v = w.Inhalt switch
        {
            double x => x, long l => l, int i => i,
            string s when Regex.Match(s, @"^\s*(?:DN|NW)?\s*(\d+(?:[.,]\d+)?)\s*$", RegexOptions.IgnoreCase) is { Success: true } m
                => double.Parse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        if (v is null) return null;
        if (IstLaenge(w)) v *= d.MmJeEinheit;
        return v > 0 && v <= 5000 ? Math.Round(v.Value, 1) : null;
    }

    /// <summary>Mass in mm: Längenmass → × Einheit der Datei; reine Zahl nur in einer mm-Datei (sonst ist die Einheit unklar:
    /// Nova schreibt mm als IfcReal in eine Meter-Datei).</summary>
    double? MmAus(IfcWert w)
    {
        double? v = w.Inhalt switch { double x => x, long l => l, int i => i, _ => null };
        if (v is null || v <= 0) return null;
        if (IstLaenge(w)) return Math.Round(v.Value * d.MmJeEinheit, 1);
        return Math.Abs(d.MmJeEinheit - 1) < 1e-9 ? Math.Round(v.Value, 1) : null;
    }

    static bool IstLaenge(IfcWert w) => w.Art.Contains("LengthMeasure", StringComparison.OrdinalIgnoreCase);

    JsonNode? Wert(EigenschaftDef ed, IfcWert w) => ed.Typ switch
    {
        "zahl" when ed.Einheit == "mm" => MmAus(w) is double mm ? JsonValue.Create(mm) : null,
        "zahl" => w.Inhalt switch { double x => JsonValue.Create(x), long l => JsonValue.Create(l), int i => JsonValue.Create(i), _ => null },
        "bool" => w.Inhalt is bool b ? JsonValue.Create(b) : null,
        "auswahl" => w.Inhalt?.ToString() is { } s && ed.Auswahl?.FirstOrDefault(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)) is { } a ? JsonValue.Create(a) : null,
        _ => w.Inhalt?.ToString() is { Length: > 0 } t ? JsonValue.Create(t) : null,
    };

    Dictionary<int, string>? materialien;
    /// <summary>Werkstoff: genau ein IfcMaterial am Produkt (IfcRelAssociatesMaterial).</summary>
    string? Material(IIfcProduct x)
    {
        if (materialien is null)
        {
            materialien = [];
            foreach (var r in d.Modell.Instances.OfType<IIfcRelAssociatesMaterial>())
            {
                var name = r.RelatingMaterial switch
                {
                    IIfcMaterial m => IfcLauf.Text(m.Name),
                    IIfcMaterialList l when l.Materials.Count == 1 => IfcLauf.Text(l.Materials[0].Name),
                    _ => null,
                };
                if (name is null) continue;
                foreach (var o in r.RelatedObjects) materialien[o.EntityLabel] = name;
            }
        }
        return materialien.GetValueOrDefault(x.EntityLabel);
    }

    // ------------------------------------------------------------------ Anschlüsse und Verbindungen

    void Anschluesse(Element e, KlassenDef def, IReadOnlyList<IfcHlksPort> px, IfcHlksZuordnung zu, bool leitungsbauteil)
    {
        var sysMedium = e.System is { } s ? systeme.Systeme.FirstOrDefault(x => x.Ref == s)?.Medium : null;
        var elementDn = e.Zahl("dn");
        foreach (var a in zu.Anschluesse)
        {
            var p = zu.PortVon(a.Name, px);
            // Medium «*» übernimmt das System (wie element_anlegen); das eigene System des Ports geht vor
            if (a.Medium == "*" && (p is null ? null : Eindeutig(systeme.Von(p.Port))) is { } pm) a.Medium = pm;
            else if (a.Medium == "*" && sysMedium is not null) a.Medium = sysMedium;
            var dn = p is null ? null : PortDn(p);
            dn ??= leitungsbauteil ? elementDn : null;
            if (dn is double v) a.Dn = (int)Math.Round(v);
        }
        e.Anschluesse = zu.Anschluesse;
        anschluesse += zu.Anschluesse.Count;
        richtungOffen += zu.RichtungOffen;
        if (zu.Ohne.Count > 0)
            Notiz($"{def.Name}: Ports ohne Anschluss (Klasse hat {string.Join(", ", def.Anschluesse.Select(a => a.Name))})", $"{Kurzname(e)} {zu.Ohne.Count}×");
    }

    void Verbinden()
    {
        foreach (var p in ports.Alle.OrderBy(p => p.Port.EntityLabel))
        {
            if (p.Gegenueber is not { } g || p.Port.EntityLabel > g.Port.EntityLabel) continue;   // jede Verbindung einmal
            if (!geplant.TryGetValue(p.Element.EntityLabel, out var za) || !geplant.TryGetValue(g.Element.EntityLabel, out var zb)
                || !za.Name.TryGetValue(p.Port.EntityLabel, out var na) || !zb.Name.TryGetValue(g.Port.EntityLabel, out var nb)
                || p.Element.EntityLabel == g.Element.EntityLabel)
            { verbindungenOffen++; continue; }
            lauf.Verbinden(p.Element, na, g.Element, nb);
            verbunden++;
        }
    }

    // ------------------------------------------------------------------ Raum

    /// <summary>Räume dieses Laufs (geplant, vorläufige Id) und bestehende des Projekts (ohne die, die neu geplant sind).</summary>
    List<Element> Raeume()
    {
        var geplanteRaeume = lauf.Geplant.Values.Where(r => r.Klasse == "Raum" && r.Geometrie?.Art == "polygon").ToList();
        var gids = geplanteRaeume.Select(r => r.Kennungen?.GetValueOrDefault("ifc")).OfType<string>().ToHashSet();
        return geplanteRaeume.Concat(lauf.Kontext.Arbeit.DerKlasse("Raum")
            .Where(r => r.Geometrie?.Art == "polygon" && !(r.Kennungen?.GetValueOrDefault("ifc") is { } g && gids.Contains(g)))).ToList();
    }

    /// <summary>Raum: der IfcSpace, in dem die Datei das Produkt führt (Revit), sonst Punkt-in-Polygon im selben Geschoss.</summary>
    string? Raum(IIfcProduct x, Element e)
    {
        for (var o = d.Eltern(x); o is not null; o = d.Eltern(o))
        {
            if (o is IIfcBuildingStorey) break;
            if (o is not IIfcSpace sp) continue;
            var gid = sp.GlobalId.ToString();
            if (raeume.FirstOrDefault(r => r.Kennungen?.GetValueOrDefault("ifc") == gid) is { } r) return r.Id;
        }
        var p = e.Geometrie!.Punkte[0];
        var drin = raeume.Where(r => (e.Geschoss is null || r.Geschoss == e.Geschoss) && r.Geometrie!.Enthaelt(p[0], p[1])).ToList();
        if (drin.Count == 1) return drin[0].Id;
        if (drin.Count > 1) Notiz("Lage in mehreren Räumen – Raum offen", Kurzname(e));
        return null;
    }

    // ------------------------------------------------------------------ Hinweise

    void NichtUebernommen(IIfcProduct x, string grund)
    {
        lauf.NichtVerstandenZaehlen(x);
        nichtUebernommen[grund] = nichtUebernommen.GetValueOrDefault(grund) + 1;
    }

    void Notiz(string kategorie, string? beispiel)
    {
        var (n, l) = notizen.TryGetValue(kategorie, out var v) ? v : (0, new List<string>());
        if (beispiel is not null && l.Count < 4 && !l.Contains(beispiel)) l.Add(beispiel);
        notizen[kategorie] = (n + 1, l);
    }

    static string Kurzname(IIfcProduct x) => $"#{x.EntityLabel} «{Kuerzen(IfcLauf.Text(x.Name) ?? IfcDatei.Entitaet(x))}»";
    static string Kurzname(Element e) => $"«{Kuerzen(e.Name ?? e.Klasse)}»";
    static string Kuerzen(string s) => s.Length > 40 ? s[..40] + "…" : s;

    static double[] Runden(double[] p) => [Math.Round(p[0], 1), Math.Round(p[1], 1), Math.Round(p.Length > 2 ? p[2] : 0, 1)];

    void Zusammenfassen(TimeSpan dauer)
    {
        int n(params string[] basen) => jeKlasse.Where(k => basen.Any(b => kat.Klassen[k.Key].IstArt(b))).Sum(k => k.Value);
        int leitungen = n("Leitung"), formteile = n("Formteil"), armaturen = n("Armatur");
        int uebrige = jeKlasse.Values.Sum() - leitungen - formteile - armaturen;
        var sysMitTyp = systeme.Systeme.Where(s => s.Systemtyp is not null).ToList();
        int teileMitTyp = sysMitTyp.Sum(s => s.Teile.Count);
        lauf.Hinweise.Add($"HLKS: {leitungen} Leitungen, {formteile} Formteile, {armaturen} Armaturen, {uebrige} Apparate/Geräte " +
                          $"({string.Join(", ", jeKlasse.Select(k => $"{k.Key} {k.Value}"))}); {nichtUebernommen.Values.Sum()} nicht übernommen. " +
                          $"Verbindungen: {verbunden} von {ports.Verbindungen} der Datei geplant; Anschlüsse {anschluesse}, Richtung offen {richtungOffen}. " +
                          $"Systeme: {systeme.IfcSysteme} in der Datei, {teileMitTyp} mit Systemtyp → {sysMitTyp.Count(s => s.Ref is not null)} KICAD-Systeme. " +
                          $"Dauer HLKS {dauer.TotalSeconds:0.0} s.");
        foreach (var s in sysMitTyp.Where(s => s.Ref is not null).OrderBy(s => s.Basis).Take(20))
            lauf.Hinweise.Add($"System «{s.Basis}»{(s.Teile.Count > 1 ? $" ({s.Teile.Count} Teile zusammengefasst)" : "")} → {s.Systemtyp} ({s.Grund}).");
        var ohneTyp = systeme.Systeme.Where(s => s.Systemtyp is null).ToList();
        if (ohneTyp.Count > 0)
            lauf.Hinweise.Add($"Ohne Systemtyp, darum kein System angelegt ({ohneTyp.Count}): {string.Join(", ", ohneTyp.Take(12).Select(s => $"«{s.Basis}»"))}{(ohneTyp.Count > 12 ? " …" : "")} " +
                              "– Systemtyp im IFC-Namen ergänzen oder das Namensmuster in ifc.json erweitern lassen.");
        if (systeme.StraengeOhneSystem > 0) lauf.Hinweise.Add($"{systeme.StraengeOhneSystem} Stränge (IfcDistributionCircuit) ohne System mit Systemtyp – nicht angelegt.");
        if (richtungOffen > 0)
            lauf.Hinweise.Add($"Fliessrichtung offen an {richtungOffen} Anschlüssen (die Datei nennt keine FlowDirection): als «beide» übernommen, bitte prüfen.");
        foreach (var (grund, anzahl) in nichtUebernommen.OrderByDescending(x => x.Value).Take(10))
            lauf.Hinweise.Add($"Nicht übernommen ({anzahl}×, im Hintergrund): {grund}.");
        foreach (var (kategorie, (anzahl, beispiele)) in notizen.OrderByDescending(x => x.Value.Anzahl))
            lauf.Hinweise.Add($"{kategorie}: {anzahl}×{(beispiele.Count > 0 ? " – z. B. " + string.Join(", ", beispiele) : "")}.");
        if (ports.MehrfachVerbunden > 0) lauf.Hinweise.Add($"{ports.MehrfachVerbunden} IfcRelConnectsPorts an schon verbundenen Ports übergangen.");
    }
}
