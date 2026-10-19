// Schema als verknüpfte Ansicht (Modul 3, Teil B3; entwurf/modul-3.md, Grundsatzentscheid 6): der Schemagraph einer
// Schema-Ansicht. Das Schema ist eine andere Sicht auf dasselbe Modell, kein zweites Modell. Es entsteht bei jedem Lesen
// aus Elementen und Verbindungen (Topologie), nie aus der Geometrie – nicht platzierte Elemente (ohne Geometrie)
// erscheinen genauso wie platzierte.
//
//   Knoten       Netzelemente (Rolle im Katalog) ausser Leitungen, Bögen und Reduktionen: Endgeräte, Geräte (Quelle,
//                Senke, Zentrale), Armaturen, T-Stücke (Knotenpunkt) und übrige Formteile (z. B. Reinigungsöffnung).
//   Schemalinie  Leitungen, Bögen und Reduktionen zwischen zwei Knoten, zu einer Linie zusammengefasst: Element-Ids in
//                Fliessrichtung, Grössen in Fliessrichtung (bei Grössenwechsel alle), Medium. Zwei direkt verbundene
//                Knoten ergeben eine Linie ohne Elemente. Endet ein Zug an einem freien Anschluss oder an einem Element,
//                das die Ansicht nicht zeigt, endet die Linie an einem offenen Ende (Marke). Ein Zug ohne Knoten an beiden
//                Enden ist «frei».
//   Sichtbar     Filter der Ansicht wie im Grundriss: gewerke (Gewerk der Klasse und der Systemtypen des Elements bzw.
//                seiner Nachbarn – sichtbar, wenn eines davon gewählt ist), klassenAusblenden (Klasse und Basisklassen),
//                dazu systeme (Liste von Systemen), sobald der Katalog die Eigenschaft an der Ansicht führt.
//   Fliessrang   Reihenfolge aller sichtbaren Netzelemente in Fliessrichtung (Kahn über die Kanten stromab, bei Gleichstand
//                und in Kreisen nach Kennzeichen) – bestimmt die Reihenfolge der Knoten auf einem Strang.
//
// Deterministisch: alle Aufzählungen laufen nach Kennzeichen (ordinal, ohne Gross/Klein), nie in der Speicherreihenfolge
// des Modells; gleiche Modelle ergeben gleiche Graphen.

namespace Hlks.Kern;

/// <summary>Ein Knoten des Schemas (Netzelement ausser Leitung, Bogen, Reduktion) mit seiner Lage im Schema.</summary>
public sealed class SchemaKnoten
{
    public required Element Element { get; init; }
    /// <summary>endgeraet | geraet | armatur | tstueck | formteil | sonst</summary>
    public required string Art { get; init; }
    /// <summary>Strang über teilVon (nur Leitungsbauteile bzw. ausdrücklich zugeordnete Elemente).</summary>
    public Element? Strang { get; init; }
    /// <summary>Fliessrang (0 = ganz stromauf).</summary>
    public int Rang { get; internal set; }
    public SchemaSpalte? Spalte { get; internal set; }
    public SchemaZeile? Zeile { get; internal set; }
    /// <summary>Lage in Papier-mm der Schema-Ansicht, y nach oben.</summary>
    public double X { get; internal set; }
    public double Y { get; internal set; }
    /// <summary>Lage von Hand gesetzt (Ansicht.lagen).</summary>
    public bool Manuell { get; internal set; }
    /// <summary>senkrecht (auf einer Stranglinie) | waagrecht (auf einer waagrechten Leitung) | etage (auf einer Etagenleitung
    /// bzw. der Anschlusslinie eines Wohnungsblocks) | abgang (auf dem senkrechten Abgang zu einem Endgerät) | frei</summary>
    public string Ausrichtung { get; internal set; } = "frei";
    /// <summary>Wohnungsblock, zu dem der Knoten gehört (Endgerät im Block, Knoten im Block, Armatur am Anschluss des Blocks).</summary>
    public SchemaBlock? Block { get; internal set; }
    /// <summary>Im Block verdeckt (Endgerät oder Knoten im Inneren der Wohnung): kein eigenes Symbol, Linien enden am Blockrand.</summary>
    public bool Verdeckt { get; internal set; }
    /// <summary>Knoten eines waagrechten Strangs, der unter der Stranglinie eines senkrechten Strangs steht (Anker).</summary>
    public SchemaSpalte? Anker { get; internal set; }
    /// <summary>Einlage im Deckenband (Nachtrag B8c): das Band, in dem der Knoten auf seiner Einlage-Spur steht; sonst null.</summary>
    public SchemaBand? Band { get; internal set; }
    /// <summary>Ablauf im Deckenband (Nachtrag B8d): Bodenablauf bzw. Dachablauf, dessen Körper im Band sitzt (Oberkante bündig mit
    /// der Bandoberkante); sonst null.</summary>
    public SchemaBand? Ablaufband { get; internal set; }
    /// <summary>Knoten im Erdreich (Nachtrag B8d): steht auf einer Grundleitungsspur unter dem untersten Band.</summary>
    public bool Untergrund { get; internal set; }
    /// <summary>Schacht im Schnitt unter dem untersten Band (Nachtrag B8d); sonst null.</summary>
    public SchemaSchacht? Schacht { get; internal set; }
    /// <summary>Endgerät bzw. Gerät, das auf dem Boden seiner Geschosszeile steht (Nachtrag B8d); Unterkante seines Symbols.</summary>
    public double? Unterkante { get; internal set; }
    public bool Platziert => Element.Geometrie is not null;
    public string Id => Element.Id;
    public string Kennzeichen => Element.Kennzeichen;
}

/// <summary>Ende einer Schemalinie: ein Knoten mit Anschluss oder ein offenes Ende (letztes Element des Zugs und dessen
/// freier Anschluss).</summary>
public sealed class SchemaEnde
{
    public SchemaKnoten? Knoten { get; init; }
    /// <summary>Anschluss am Knoten bzw. am letzten Element des Zugs (offenes Ende).</summary>
    public required string Anschluss { get; init; }
    /// <summary>Knotenelement bzw. letztes Element des Zugs.</summary>
    public required Element Element { get; init; }
    /// <summary>null = Knoten; offen = freier Anschluss; ausgeblendet = verbunden mit einem Element, das die Ansicht nicht
    /// zeigt; ring = Zug ohne Knoten, der in sich geschlossen ist.</summary>
    public string? Offen { get; init; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
}

/// <summary>Eine Schemalinie: Leitungen, Bögen und Reduktionen zwischen zwei Enden, in Fliessrichtung.</summary>
public sealed class SchemaLinie
{
    public List<Element> Elemente { get; } = [];
    public required SchemaEnde Von { get; set; }
    public required SchemaEnde Nach { get; set; }
    /// <summary>Verbindung bei zwei direkt verbundenen Knoten (Linie ohne Elemente).</summary>
    public string? Verbindung { get; init; }
    /// <summary>Grössen der Leitungen in Fliessrichtung, gleiche aufeinanderfolgende nur einmal («NW 25», «NW 20»).</summary>
    public List<string> Groessen { get; } = [];
    public string? Medium { get; internal set; }
    /// <summary>Kein Knoten an beiden Enden.</summary>
    public bool Frei => Von.Knoten is null && Nach.Knoten is null;
    /// <summary>Orthogonaler Linienzug in Papier-mm (nach der Anordnung).</summary>
    public List<double[]> Punkte { get; internal set; } = [];
    /// <summary>Strang der Linie: der des ersten Linienelements mit Strang, sonst der eines Endknotens.</summary>
    public Element? Strang { get; internal set; }
    /// <summary>Einlage (Nachtrag B8c): die Leitungen sind nach Länge überwiegend im Beton eingelegt – die Linie läuft waagrecht im
    /// Deckenband auf der Einlage-Spur BandY; sonst null.</summary>
    public SchemaBand? Band { get; internal set; }
    public double BandY { get; internal set; }
    /// <summary>Anteil der Leitungslänge (0…1) mit Verlegung einlage im Band (nur gesetzt, wenn Einlagen darin sind).</summary>
    public double Einlageanteil { get; internal set; }
    /// <summary>Linie unter dem untersten Band (Nachtrag B8d): Grundleitung im Erdreich, Linie zu einem Schacht oder zu einem Knoten im
    /// Erdreich – läuft senkrecht hinunter, waagrecht auf ihrer Grundleitungsspur UntergrundY und seitlich in den Schacht.</summary>
    public bool Untergrund { get; internal set; }
    public double UntergrundY { get; internal set; }
    /// <summary>Anteil der Leitungslänge (0…1) mit Verlegung erdreich (nur gesetzt, wenn Leitungen im Erdreich darin sind).</summary>
    public double Erdreichanteil { get; internal set; }
    /// <summary>Anschlusslinie von einem Knoten einer waagrechten Leitung zu einem Endgerät auf dem Boden (Nachtrag B8d): waagrecht auf
    /// dieser Höhe unter den Endgeräten; sonst null.</summary>
    public double? AnschlussY { get; internal set; }
    // Platz eines freien Zugs (nach der Anordnung)
    public SchemaSpalte? Spalte { get; internal set; }
    public SchemaZeile? Zeile { get; internal set; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
}

public sealed class SchemaGraph
{
    /// <summary>Ordnung aller Aufzählungen: Kennzeichen ordinal ohne Gross/Klein, bei Gleichstand die Id.</summary>
    public static readonly IComparer<Element> Ordnung = Comparer<Element>.Create((a, b) =>
    {
        int c = StringComparer.OrdinalIgnoreCase.Compare(a.Kennzeichen, b.Kennzeichen);
        return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
    });

    public required Projekt Projekt { get; init; }
    public required Element Ansicht { get; init; }
    /// <summary>Modell, aus dem das Schema entsteht: das des Projekts bzw. im Befehl die Arbeitskopie.</summary>
    public required Modell Modell { get; init; }
    public Katalog Katalog => Projekt.Katalog;
    /// <summary>Gewählte Gewerke; null = alle.</summary>
    public IReadOnlySet<string>? Gewerke { get; private init; }
    public IReadOnlySet<string> KlassenAus { get; private init; } = new HashSet<string>();
    /// <summary>Gewählte Systeme (Ids); null = alle.</summary>
    public IReadOnlySet<string>? Systeme { get; private init; }

    /// <summary>Knoten nach Kennzeichen.</summary>
    public List<SchemaKnoten> Knoten { get; } = [];
    public Dictionary<string, SchemaKnoten> NachId { get; } = new(StringComparer.Ordinal);
    public List<SchemaLinie> Linien { get; } = [];
    public List<string> Hinweise { get; } = [];
    /// <summary>Linie, zu der ein Linienelement (Leitung, Bogen, Reduktion) gehört.</summary>
    public Dictionary<string, SchemaLinie> LinieVon { get; } = new(StringComparer.Ordinal);
    /// <summary>Alle sichtbaren Netzelemente (Knoten und Linienelemente) nach Kennzeichen.</summary>
    public List<Element> Sichtbare { get; } = [];

    readonly HashSet<string> sichtbar = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> rang = new(StringComparer.Ordinal);

    public bool IstSichtbar(Element e) => sichtbar.Contains(e.Id);
    public int RangVon(Element e) => rang.GetValueOrDefault(e.Id, int.MaxValue);

    /// <summary>Leitung, Bogen oder Reduktion mit genau zwei Anschlüssen: Teil einer Schemalinie, kein Knoten.</summary>
    public static bool IstLinienelement(Katalog k, Element e) =>
        (k.IstArt(e, "Leitung") || k.IstArt(e, "Bogen") || k.IstArt(e, "Reduktion")) && e.Anschluesse.Count == 2;

    /// <summary>Netzelement = Klasse mit Rolle im Netz (verbraucher, quelle, senke, zentrale, leitung).</summary>
    public static bool IstNetzelement(Katalog k, Element e) => Netz.Rolle(k, e) is not null;

    public static string KnotenArt(Katalog k, Element e) => Netz.Rolle(k, e) switch
    {
        "verbraucher" => "endgeraet",
        "quelle" or "senke" or "zentrale" => "geraet",
        _ => k.IstArt(e, "TStueck") ? "tstueck" : k.IstArt(e, "Armatur") ? "armatur" : k.IstArt(e, "Formteil") ? "formteil" : "sonst",
    };

    /// <summary>Strang, zu dem ein Element über teilVon gehört (oder null).</summary>
    public Element? StrangVon(Element e) => Modell.Finde(e.TeilVon) is { Klasse: "Strang" } s ? s : null;

    /// <summary>Nachbarn über die Verbindungen, in der Reihenfolge der eigenen Anschlüsse.</summary>
    public IEnumerable<(Anschluss Eigener, Element Nachbar, string NachbarAnschluss, Verbindung Verbindung)> Nachbarn(Element e)
    {
        foreach (var a in e.Anschluesse)
        {
            var v = Modell.VerbindungAn(e.Id, a.Name);
            if (v?.Gegenueber(e.Id, a.Name) is not { } g || Modell.Finde(g.Element) is not { } n || n.Id == e.Id) continue;
            yield return (a, n, g.Anschluss, v);
        }
    }

    /// <summary>Systeme eines Elements: das eigene, sonst die der direkt verbundenen Elemente (Endgeräte), nach Kennzeichen.</summary>
    public List<Element> SystemeVon(Element e)
    {
        if (e.System is not null) return Modell.Finde(e.System) is { Klasse: "System" } s ? [s] : [];
        return Nachbarn(e).Select(x => Modell.Finde(x.Nachbar.System)).OfType<Element>().Where(s => s.Klasse == "System")
            .DistinctBy(s => s.Id).OrderBy(s => s, Ordnung).ToList();
    }

    public string? SystemGewerk(Element system) =>
        system.Text("systemtyp") is { } typ && Katalog.Systemtypen.TryGetValue(typ, out var st) ? st.Gewerk : null;

    /// <summary>Gewerke eines Elements: Gewerk der Klasse und der Systemtypen seiner Systeme.</summary>
    public HashSet<string> GewerkeVon(Element e)
    {
        var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Katalog.Klassen.TryGetValue(e.Klasse, out var d) && d.Gewerk is { } g) s.Add(g);
        foreach (var sys in SystemeVon(e))
            if (SystemGewerk(sys) is { } sg) s.Add(sg);
        return s;
    }

    /// <summary>Ist ein System in dieser Ansicht gewählt (Gewerk seines Systemtyps, Systemfilter)?</summary>
    public bool SystemSichtbar(Element system)
    {
        if (Systeme is not null && !Systeme.Contains(system.Id)) return false;
        if (Gewerke is not null && SystemGewerk(system) is { } g && g != "allgemein" && !Gewerke.Contains(g)) return false;
        return true;
    }

    /// <summary>Warum ein Netzelement nicht im Schema steht (null = sichtbar).</summary>
    public string? Ausgeblendet(Element e)
    {
        if (!Katalog.Klassen.TryGetValue(e.Klasse, out var d) || d.Rolle is null)
            return $"{e.Kennzeichen} ist kein Netzelement ({e.Klasse}); im Schema stehen Endgeräte, Geräte, Armaturen, Formteile und Leitungen";
        if (KlassenAus.Contains(e.Klasse) || d.Linie.Any(KlassenAus.Contains))
            return $"Klasse {e.Klasse} ist in der Ansicht {Ansicht.Kennzeichen} ausgeblendet (klassenAusblenden)";
        if (Gewerke is not null)
        {
            var gw = GewerkeVon(e);
            if (gw.Count > 0 && !gw.Any(g => g.Equals("allgemein", StringComparison.OrdinalIgnoreCase) || Gewerke.Contains(g)))
                return $"Gewerk {string.Join("/", gw.Order(StringComparer.OrdinalIgnoreCase))} ist in der Ansicht {Ansicht.Kennzeichen} nicht gewählt (gewerke: {string.Join(", ", Gewerke.Order(StringComparer.OrdinalIgnoreCase))})";
        }
        if (Systeme is not null && !SystemeVon(e).Any(s => Systeme.Contains(s.Id)))
            return $"{e.Kennzeichen} gehört zu keinem der gewählten Systeme der Ansicht {Ansicht.Kennzeichen}";
        return null;
    }

    /// <summary>Prüft, dass die Ansicht ein Schema ist.</summary>
    public static void SchemaAnsichtPruefen(Modell m, Element ansicht)
    {
        if (ansicht.Klasse != "Ansicht")
            throw new HlksFehler($"{ansicht.Kennzeichen} ist keine Ansicht, sondern {ansicht.Klasse}.", Vorschlag(m));
        if (ansicht.Text("art") != "schema")
            throw new HlksFehler($"Ansicht {ansicht.Kennzeichen} ist kein Schema (art {ansicht.Text("art") ?? "–"}).", Vorschlag(m));
    }

    static string Vorschlag(Modell m)
    {
        var liste = m.DerKlasse("Ansicht").Where(a => a.Text("art") == "schema").Select(a => a.Kennzeichen).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return liste.Count > 0 ? "Eine Ansicht mit art schema angeben: " + string.Join(", ", liste)
            : "Zuerst eine Schema-Ansicht anlegen: element_anlegen { klasse: Ansicht, eigenschaften: { art: schema, schemaart: strangschema } }.";
    }

    /// <summary>Bildet den Schemagraphen einer Schema-Ansicht.</summary>
    public static SchemaGraph Bilden(Projekt p, Element ansicht, Modell? modell = null)
    {
        var m = modell ?? p.Modell;
        SchemaAnsichtPruefen(m, ansicht);
        var gewerke = Grafik.Liste(ansicht, "gewerke");
        var hinweise = new List<string>();
        HashSet<string>? systeme = null;
        var systemListe = Grafik.Liste(ansicht, "systeme");
        if (systemListe.Count > 0)
        {
            systeme = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in systemListe)
                if (m.Finde(s) is { Klasse: "System" } sys) systeme.Add(sys.Id);
                else hinweise.Add($"System «{s}» in systeme der Ansicht gibt es nicht.");
        }
        var g = new SchemaGraph
        {
            Projekt = p,
            Modell = m,
            Ansicht = ansicht,
            Gewerke = gewerke.Count > 0 ? new HashSet<string>(gewerke, StringComparer.OrdinalIgnoreCase) : null,
            KlassenAus = new HashSet<string>(Grafik.Liste(ansicht, "klassenAusblenden"), StringComparer.OrdinalIgnoreCase),
            Systeme = systeme,
        };
        g.Hinweise.AddRange(hinweise);
        var k = p.Katalog;
        var netz = m.Elemente.Where(e => IstNetzelement(k, e)).OrderBy(e => e, Ordnung).ToList();
        foreach (var e in netz)
        {
            if (g.Ausgeblendet(e) is not null) continue;
            g.sichtbar.Add(e.Id);
            g.Sichtbare.Add(e);
            if (IstLinienelement(k, e)) continue;
            var kn = new SchemaKnoten { Element = e, Art = KnotenArt(k, e), Strang = g.StrangVon(e) };
            g.Knoten.Add(kn);
            g.NachId[e.Id] = kn;
        }
        g.RaengeRechnen(g.Sichtbare);
        foreach (var kn in g.Knoten) kn.Rang = g.RangVon(kn.Element);
        g.LinienBilden(g.Sichtbare.Where(e => IstLinienelement(k, e)).ToList());
        return g;
    }

    // ------------------------------------------------------------------ Fliessrang

    /// <summary>Kahn über die Kanten stromab (Netz.Nachbarn) der sichtbaren Elemente; bereit ist immer das kleinste
    /// Kennzeichen. Bleiben Elemente in Kreisen übrig, gilt das kleinste davon als nächster Anfang.</summary>
    void RaengeRechnen(List<Element> elemente)
    {
        var ein = new Dictionary<string, int>(StringComparer.Ordinal);
        var aus = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        foreach (var e in elemente) { ein[e.Id] = 0; aus[e.Id] = []; }
        foreach (var e in elemente)
            foreach (var (n, _, _) in Netz.Nachbarn(Modell, e, null, "stromab", null))
            {
                if (n.Id == e.Id || !ein.ContainsKey(n.Id)) continue;
                aus[e.Id].Add(n);
                ein[n.Id]++;
            }
        var rest = new SortedSet<Element>(elemente, Ordnung);
        var bereit = new SortedSet<Element>(elemente.Where(e => ein[e.Id] == 0), Ordnung);
        int r = 0;
        while (rest.Count > 0)
        {
            if (bereit.Count == 0) bereit.Add(rest.Min!);
            var e = bereit.Min!;
            bereit.Remove(e);
            if (!rest.Remove(e)) continue;
            rang[e.Id] = r++;
            foreach (var n in aus[e.Id])
            {
                if (!rest.Contains(n)) continue;
                if (--ein[n.Id] <= 0) bereit.Add(n);
            }
        }
    }

    // ------------------------------------------------------------------ Schemalinien

    void LinienBilden(List<Element> linienelemente)
    {
        var direkt = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kn in Knoten)
            foreach (var (a, n, na, v) in Nachbarn(kn.Element))
            {
                if (!IstSichtbar(n)) continue;
                if (NachId.TryGetValue(n.Id, out var gegen))
                {
                    if (direkt.Add(v.Id)) Direkt(kn, a, gegen, na, v);
                }
                else if (!LinieVon.ContainsKey(n.Id)) Kette(new SchemaEnde { Knoten = kn, Element = kn.Element, Anschluss = a.Name }, n, na);
            }
        // freie Züge: Linienelemente ohne Knoten an beiden Enden
        foreach (var e in linienelemente)
        {
            if (LinieVon.ContainsKey(e.Id)) continue;
            var a0 = e.Anschluesse[0].Name;
            // rückwärts bis zum Anfang des Zugs, dann vorwärts wie von einem Knoten aus
            var (anfang, anfangAnschluss, grund) = Rueckwaerts(e, a0);
            Kette(new SchemaEnde { Element = anfang, Anschluss = anfangAnschluss, Offen = grund }, anfang, anfangAnschluss);
        }
    }

    /// <summary>Vom Linienelement e über den Anschluss <paramref name="ueber"/> zurück bis zum letzten Linienelement des Zugs;
    /// liefert dieses Element, seinen freien Anschluss und den Grund (offen, ausgeblendet, ring).</summary>
    (Element Element, string Anschluss, string Grund) Rueckwaerts(Element e, string ueber)
    {
        var besucht = new HashSet<string>(StringComparer.Ordinal) { e.Id };
        var cur = e; var aus = ueber;
        while (true)
        {
            var v = Modell.VerbindungAn(cur.Id, aus);
            var g = v?.Gegenueber(cur.Id, aus);
            var n = g is null ? null : Modell.Finde(g.Element);
            if (n is null) return (cur, aus, "offen");
            if (!IstSichtbar(n) || NachId.ContainsKey(n.Id)) return (cur, aus, IstSichtbar(n) ? "offen" : "ausgeblendet");
            if (!besucht.Add(n.Id)) return (cur, aus, "ring");
            cur = n;
            aus = AndererAnschluss(n, g!.Anschluss);
        }
    }

    static string AndererAnschluss(Element e, string anschluss) =>
        e.Anschluesse.First(x => !x.Name.Equals(anschluss, StringComparison.OrdinalIgnoreCase)).Name;

    void Direkt(SchemaKnoten a, Anschluss aa, SchemaKnoten b, string bAnschluss, Verbindung v)
    {
        var ba = b.Element.AnschlussNamens(bAnschluss);
        var von = new SchemaEnde { Knoten = a, Element = a.Element, Anschluss = aa.Name };
        var nach = new SchemaEnde { Knoten = b, Element = b.Element, Anschluss = ba?.Name ?? bAnschluss };
        bool umkehren = !(aa.Fluss == "raus" || ba?.Fluss == "rein") && (aa.Fluss == "rein" || ba?.Fluss == "raus");
        var l = umkehren ? new SchemaLinie { Von = nach, Nach = von, Verbindung = v.Id } : new SchemaLinie { Von = von, Nach = nach, Verbindung = v.Id };
        l.Medium = MediumVon(l);
        l.Strang = l.Elemente.Select(StrangVon).FirstOrDefault(s => s is not null) ?? l.Von.Knoten?.Strang ?? l.Nach.Knoten?.Strang;
        Linien.Add(l);
    }

    /// <summary>Folgt einem Zug ab dem Ende <paramref name="start"/> über das erste Linienelement <paramref name="erstes"/>
    /// (betreten über <paramref name="eintritt"/>) bis zum nächsten Knoten oder offenen Ende.</summary>
    void Kette(SchemaEnde start, Element erstes, string eintritt)
    {
        var schritte = new List<(Element E, string Ein, string Aus)>();
        var cur = erstes; var ein = eintritt;
        SchemaEnde? ende = null;
        while (true)
        {
            if (LinieVon.ContainsKey(cur.Id) || schritte.Any(s => s.E.Id == cur.Id))
            {
                ende = new SchemaEnde { Element = schritte[^1].E, Anschluss = schritte[^1].Aus, Offen = "ring" };
                break;
            }
            var aus = AndererAnschluss(cur, ein);
            schritte.Add((cur, cur.AnschlussNamens(ein)?.Name ?? ein, aus));
            var v = Modell.VerbindungAn(cur.Id, aus);
            var g = v?.Gegenueber(cur.Id, aus);
            var n = g is null ? null : Modell.Finde(g.Element);
            if (n is null) { ende = new SchemaEnde { Element = cur, Anschluss = aus, Offen = "offen" }; break; }
            if (!IstSichtbar(n)) { ende = new SchemaEnde { Element = cur, Anschluss = aus, Offen = "ausgeblendet" }; break; }
            if (NachId.TryGetValue(n.Id, out var kn))
            {
                ende = new SchemaEnde { Knoten = kn, Element = n, Anschluss = n.AnschlussNamens(g!.Anschluss)?.Name ?? g!.Anschluss };
                break;
            }
            cur = n; ein = g!.Anschluss;
        }
        if (schritte.Count == 0) return;
        // Fliessrichtung: Eintritt über einen Anschluss «rein» = in Fliessrichtung gegangen, über «raus» = dagegen
        int vor = 0, zurueck = 0;
        foreach (var (e, ei, _) in schritte)
            switch (e.AnschlussNamens(ei)?.Fluss)
            {
                case "rein": vor++; break;
                case "raus": zurueck++; break;
            }
        if (vor == zurueck)
        {
            var fs = start.Knoten?.Element.AnschlussNamens(start.Anschluss)?.Fluss;
            var fe = ende.Knoten?.Element.AnschlussNamens(ende.Anschluss)?.Fluss;
            if (fs == "rein" || fe == "raus") zurueck++;
            else if (fs == "raus" || fe == "rein") vor++;
        }
        var l = new SchemaLinie { Von = start, Nach = ende };
        l.Elemente.AddRange(schritte.Select(s => s.E));
        if (zurueck > vor)
        {
            l.Von = ende; l.Nach = start;
            l.Elemente.Reverse();
        }
        foreach (var e in l.Elemente) LinieVon[e.Id] = l;
        l.Medium = MediumVon(l);
        l.Strang = l.Elemente.Select(StrangVon).FirstOrDefault(s => s is not null) ?? l.Von.Knoten?.Strang ?? l.Nach.Knoten?.Strang;
        foreach (var e in l.Elemente)
        {
            if (!Katalog.IstArt(e, "Leitung")) continue;
            // Modul 3, B9b: nur die gemeinsame Rohrbeschriftung (Abwasser «PE-S 110», übrige «CNS NW 20»); ohne Zahl keine Grösse, nie der Typname
            var t = Leitungsbezeichnung.Text(Projekt, Ansicht, e);
            if (t is not null && (l.Groessen.Count == 0 || l.Groessen[^1] != t)) l.Groessen.Add(t);
        }
        Linien.Add(l);
    }

    string? MediumVon(SchemaLinie l)
    {
        foreach (var e in l.Elemente)
            if (Netz.SystemMedium(Modell, e.System) is { } m) return m;
        foreach (var en in new[] { l.Von, l.Nach })
            if (en.Knoten is not null && en.Element.AnschlussNamens(en.Anschluss) is { } a && Netz.Medium(Modell, en.Element, a) is { } m && m != "*")
                return m;
        return null;
    }
}
