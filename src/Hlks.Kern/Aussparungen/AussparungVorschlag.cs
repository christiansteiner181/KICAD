// Automatische Aussparungen (Modul 3b, B3, entwurf/modul-3b.md, Grundsatzentscheide 1, 3, 5, 6): Befehl aussparungen_vorschlagen.
//
// Nachtrag B3b (Entscheid Christian 19.10.2026, «Rund (Kernbohrung) nur durch manuelle Auswahl»): der Vorschlag ist immer
// rechteckig (art aussparung), auch für runde Rohre; Kernbohrungen entstehen nur von Hand (aussparung_form, AussparungPflege.cs;
// Regel «von Hand» in AussparungForm, AussparungDaten.cs). grenze_kernbohrung wird nicht mehr verlangt.
//
// Ablauf (ein Befehl = ein Paket; Trockenlauf = Vorschau mit Liste in den Hinweisen):
//   1. Vorgaben spiel_aussparung, abstand_zusammenfassen aus dem Element Vorgaben (mit Quelle) – fehlt eine, wird abgelehnt. Ist die
//      nicht mehr verwendete grenze_kernbohrung gesetzt: Hinweis.
//   2. Prüfkörper (B1): Leitungen als Kapsel bzw. Quader um die Achse mit Dämmung; Bauteile: Wand, Decke, Dach, Stütze, Träger in
//      KICAD (gezeichnet oder übernommen, dann mit dem exakten Netz) und nicht übernommene Produkte der IFC-Unterlagen mit
//      Bereich architektur und Entität Wand, Decke, Dach, Träger, Stütze (Teile, Türen, Fenster, Treppen … nicht).
//   3. Bedarf je Durchgang (Durchgaenge.Finden; an einem Knick im Bauteil werden die beiden Teilstücke zu einem Durchgang
//      zusammengesetzt). Beginnt oder endet die Leitung im Bauteil, gibt es keinen Vorschlag (Hinweis); ebenso, wenn sie längs im
//      Bauteil liegt (Wand über Ober-/Unterseite bzw. Stirnseite, Decke über die Stirnseite betreten, flacher als 30° zur Fläche –
//      Laengs). Liegt der
//      Querschnitt mit Dämmung ganz in einer Öffnung der Netzdatei (IfcOpeningElement, B1b) → keine Aussparung (Hinweis).
//      Mitte = Durchgangsmitte, Achse = Bauteilnormale (kanonisch), Tiefe = Bauteildicke (Durchgang.Dicke).
//      Grösse = Projektion des Querschnitts mit Dämmung über die ganze Tiefe (Durchgaenge.Projektion, Durchbruch*) + 2 × Spiel,
//      in der Bauteilebene gemessen (ANNAHME Prüfliste: das Spiel wird zur Projektion addiert, nicht vor der Projektion zum
//      Querschnitt – bei senkrechter Querung gleich). Immer rechteckig, art aussparung: b × h = umschliessendes Rechteck in den
//      Ebenenachsen U, V + 2 × Spiel – für Rohr, Rund- und Rechteckkanal (Rohr senkrecht: (D + 2s) im Quadrat; schräg unter θ durch
//      die Dicke t: D/cos θ + t · tan θ + 2s in Neigungsrichtung, quer D + 2s). Masse b, h, d auf ganze cm aufgerundet, nie kleiner
//      als gerechnet (Entscheid Christian 19.10.2026, Nachtrag B4e – vorher 0.1 mm); die Tiefe (Bauteildicke) auf 0.1 mm.
//      Von Hand gewählte Kernbohrung (nur über aussparung_form): rund, art kernbohrung, d = umschliessender Kreis + 2 × Spiel.
//      Koten: rechteckig UK/OK des Aussparungskörpers über ±0.00, rund die Achse (kote_uk).
//   4. Bestehende Aussparungen ab freigegeben (freigegeben, versandt, angekommen, ausgeführt, fehlt) werden nicht verändert. Sie
//      gelten, solange jede ihrer Leitungen das Bauteil noch quert und ihr Querschnitt mit Dämmung (ohne Spiel) ganz in der
//      Aussparung liegt; dann decken sie den Bedarf ab (eine weitere Leitung, die ganz hindurchgeht, wird als Bezug ergänzt –
//      Geometrie und Status bleiben). Sonst → «veraltet», und ihr Bedarf wird neu vorgeschlagen. Feste ohne lesbare Lage, Achse
//      oder Masse (von Hand angelegt) bleiben unberührt (Hinweis).
//   5. Von Hand gewählte Kernbohrungen (Status vorgeschlagen, form rund von Hand, eine Leitung): der Bedarf ihrer Leitung im selben
//      Bauteil (bei mehreren der Mitte am nächsten) bleibt ihr vorbehalten – eigene runde Gruppe, d neu gerechnet, Form und Herkunft
//      der Wahl bleiben; nicht zusammengefasst. Liegt ein Nachbar im selben Bauteil und derselben Ebene unter abstand_zusammenfassen
//      (bzw. überschneiden sie sich) → Hinweis. Ohne Bedarf (Leitung quert nicht mehr) gilt Punkt 6 (veraltet bzw. aufgegangen).
//      Zusammenfassen: der übrige Bedarf je Bauteil und Ebene; Randabstand (lichter Abstand der Rechtecke in der Bauteilebene)
//      < abstand_zusammenfassen oder Überschneidung → eine rechteckige Sammelaussparung (umschliessendes Rechteck, Leitungen
//      vereinigt, Tiefe = grösste Tiefe), wiederholt bis nichts mehr zusammenkommt.
//   6. Wiedererkennen der Vorschläge (Status vorgeschlagen): gleiches Bauteil und mindestens eine gemeinsame Leitung; bei
//      mehreren Möglichkeiten zuerst die meisten gemeinsamen Leitungen, dann der kleinste Abstand der Mitten. Keine feste
//      Abstandsgrenze (eine Zahl wäre eine Vorgabe) – ein Vorschlag folgt seiner Leitung im selben Bauteil. Erkannt → Geometrie,
//      Grösse, Form, Art, Koten und Leitungen nachgeführt (Kennzeichen, Nummer und Status bleiben; eine automatisch runde
//      Aussparung aus der Zeit vor B3b wird dabei rechteckig). Nicht erkannte Vorschläge: ist ihre
//      Leitung im Bauteil anderweitig versorgt (in einer Sammelaussparung oder einer festen Aussparung aufgegangen) → gelöscht,
//      sonst (Leitung quert nicht mehr, gelöscht, Bauteil fehlt) → «veraltet».
//   Ein Bauteil wird immer ganz behandelt (alle Leitungen, die es queren), auch wenn nur Leitungen gewählt sind – sonst stimmten
//   Sammelaussparungen nicht. Bezüge auf gelöschte Leitungen bzw. Bauteile entfernt der Befehl (Hinweis), damit P08 nicht meldet.
//
// Nachtrag B3c (Entscheid Christian 19.10.2026, «Aussparungen nur in Beton und Mauerwerk», «Bekleidungen nicht melden»):
//   a. Material: jedes Bauteil wird nach AussparungMaterial.cs eingeteilt. Neue Vorschläge nur in Rohbau (Beton, Mauerwerk) und bei
//      unbekanntem Material (mit Kennzeichen materialUnbekannt und Hinweis – ANNAHME Prüfliste: lieber einer zu viel). Dämmung,
//      Leichtbau, Holz, Stahl/Metall, Ausbau bekommen keine; Bekleidungen (IfcCovering) sind nie aussparungsfähig.
//   b. Durchgehende Aussparung: Durchgänge derselben Leitung durch solche Bauteile, die DIREKT hintereinander liegen, werden eine Kette
//      und eine Aussparung (Tiefe über alle, eine Nummer, Bezüge Haupt-Bauteil + weitere, Format AussparungDaten.cs). «Direkt» heisst:
//      Normalen parallel (Aussparungsgeometrie.EbenenWinkel) und Lücke senkrecht zu den Schichten ≤ Kettenluecke = 1 mm (Überlappung
//      zählt mit). Begründung der 1 mm (technische Toleranz, keine Projektvorgabe): Revit führt Wandschichten als eigene, aneinander
//      gezeichnete Wände; im float32-Netz der Netzdatei und durch die Tessellierung bleiben Spalte im Zehntelmillimeter-Bereich
//      (Geometrie3.Toleranz 0.1 mm ist dafür zu knapp), während eine echte Zwischenschicht (Dämmung, Luftschicht, Trennlage als
//      eigenes Bauteil) mehrere Millimeter dick ist. Liegt eine Schicht ohne Aussparung dazwischen (Dämmung, Leichtbau …), ist die
//      Lücke ihre Dicke – es entstehen getrennte Aussparungen. Mitte, Achse, Masse aus dem zusammengesetzten Durchgang (Eintritt
//      des ersten, Austritt des letzten Bauteils) – dieselbe Rechnung wie für ein Bauteil.
//   c. Feste Aussparungen decken ihren Bedarf je Bauteil (jedes ihrer Bauteile, jede ihrer Leitungen) – eine ältere feste Aussparung
//      in nur einer Schicht gilt weiter; für die übrigen Schichten wird neu vorgeschlagen. Feste in Bauteilen ohne Aussparung (kein
//      Rohbau) bleiben, solange sie decken (abgegeben ist abgegeben), mit Hinweis.
//   d. Vorschläge (Status vorgeschlagen) in Bauteilen, die keine Aussparung mehr bekommen (kein Rohbau), werden gelöscht (Hinweis).
//      Wiedererkennen: ein gemeinsames Bauteil und eine gemeinsame Leitung (zuerst die meisten Leitungen, dann die meisten Bauteile,
//      dann die Nähe). Sind Bauteile oder Leitungen gewählt, gehören die Ketten-Nachbarn der gewählten Bauteile mit zum Bereich.
//
// Nachtrag B3d (Entscheide Christian 19.10.2026 «Material»): das Material kommt mit der Typenliste des Projekts (Kollision/Typenliste.cs)
//   und dem Vorrang Typenliste > Materialzuordnung im IFC > Typname > unbekannt (AussparungMaterial.cs). Installationsvorwand, Leichtbau
//   usw. aus der Typenliste bekommen keine Aussparung (Vorschläge darin werden gelöscht wie bei d.). Die Hinweise nennen, wie viele
//   gequerte Bauteile ihr Material aus der Typenliste bzw. aus dem Typnamen haben; die Herkunft des material steht je Bauteil darin.
// Nachtrag B3e (19.10.2026): Unterdecke, Bodenaufbau und Luftschicht bekommen keine Aussparung (AussparungMaterial.cs). Die Hinweise
//   nennen die Zahl der Einzelzeilen je Grund immer vollständig («gelöscht: kein Beton bzw. Mauerwerk – 21»), die Einzelzeilen höchstens 100.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Was ein Durchgang einer Leitung durch ein Bauteil – bzw. durch eine Kette direkt hintereinander liegender Bauteile
/// (Nachtrag B3c) – an Aussparung braucht (vor dem Zusammenfassen).</summary>
sealed class Aussparungsbedarf
{
    /// <summary>Das (Haupt-)Bauteil: das einzige bzw. das dickste der Kette (bei gleicher Dicke der kleinere Schlüssel).</summary>
    public required Pruefkoerper Bauteil { get; init; }
    /// <summary>Alle Bauteile in der Folge entlang der Achse (ein Bauteil: nur dieses).</summary>
    public IReadOnlyList<Pruefkoerper> Bauteile { get => bauteile ?? [Bauteil]; init => bauteile = value; }
    IReadOnlyList<Pruefkoerper>? bauteile;
    /// <summary>Dicke je Bauteil (Kennung → mm) – für das Haupt-Bauteil einer Gruppe.</summary>
    public IReadOnlyDictionary<string, double> Dicken { get => dicken ?? new Dictionary<string, double> { [Bauteil.Kennung] = Tiefe }; init => dicken = value; }
    IReadOnlyDictionary<string, double>? dicken;
    public required Pruefkoerper Leitung { get; init; }
    public required Durchgang Durchgang { get; init; }
    public required V3 Mitte { get; init; }
    /// <summary>Bauteilnormale, kanonisch (Aussparungsgeometrie.Kanonisch).</summary>
    public required V3 Achse { get; init; }
    public required V3 U { get; init; }
    public required V3 V { get; init; }
    public required double Tiefe { get; init; }
    /// <summary>Querschnitt der Leitung rund (Rohr, Rundkanal) – nicht die Form der Aussparung.</summary>
    public required bool Rund { get; init; }
    /// <summary>Rechteckige Aussparung mit Spiel (die Regel): B entlang U, H entlang V – umschliessendes Rechteck + 2 × Spiel.</summary>
    public required double B { get; init; }
    public required double H { get; init; }
    /// <summary>Kernbohrung mit Spiel (nur von Hand gewählt): umschliessender Kreis + 2 × Spiel.</summary>
    public required double D { get; init; }
    /// <summary>Querschnitt mit Dämmung über die ganze Tiefe, ohne Spiel: umschliessendes Rechteck (QB × QH) und Kreis (QD).</summary>
    public required double QB { get; init; }
    public required double QH { get; init; }
    public required double QD { get; init; }
    /// <summary>Schlüssel zum Zusammenfassen und Wiedererkennen: Kennung des Bauteils bzw. der Bauteile der Kette (sortiert, «|»).</summary>
    public string Schluessel => Aussparungsplanung.Kettenschluessel(Bauteile.Select(b => b.Kennung));
    public string LeitungId => Leitung.ElementId!;
    public bool Kette => Bauteile.Count > 1;
}

/// <summary>Eine vorzuschlagende Aussparung: ein Bedarf oder eine Sammelaussparung aus mehreren.</summary>
sealed class Aussparungsgruppe
{
    public required List<Aussparungsbedarf> Teile { get; init; }
    public required V3 Mitte { get; init; }
    public required V3 Achse { get; init; }
    public required V3 U { get; init; }
    public required V3 V { get; init; }
    public required double Tiefe { get; init; }
    /// <summary>Form der Aussparung rund (nur eine von Hand gewählte Kernbohrung); sonst rechteckig.</summary>
    public required bool Rund { get; init; }
    /// <summary>Mass mit Spiel: rechteckig B × H, rund B = H = d.</summary>
    public required double B { get; init; }
    public required double H { get; init; }
    /// <summary>Haupt-Bauteil: das dickste aller Teile (grösste Dicke je Bauteil), bei gleicher Dicke der kleinere Schlüssel.</summary>
    public Pruefkoerper Bauteil => Teile[0].Kette || Teile.Any(t => t.Bauteil != Teile[0].Bauteil)
        ? Bauteile.OrderByDescending(b => Teile.Max(t => t.Dicken.GetValueOrDefault(b.Kennung))).ThenBy(b => b.Kennung, StringComparer.Ordinal).First()
        : Teile[0].Bauteil;
    /// <summary>Alle Bauteile der Gruppe (Folge entlang der Achse wie im ersten Teil).</summary>
    public IReadOnlyList<Pruefkoerper> Bauteile => Teile[0].Bauteile;
    /// <summary>Bauteile für die Bezüge: das Haupt-Bauteil, dann die übrigen in der Folge entlang der Achse.</summary>
    public IReadOnlyList<Pruefkoerper> BauteileFuerBezug => [Bauteil, .. Bauteile.Where(b => b != Bauteil)];
    public bool Sammel => Teile.Count > 1;
    public bool Kette => Bauteile.Count > 1;
    public IEnumerable<string> LeitungIds => Teile.Select(t => t.LeitungId).Distinct();
}

/// <summary>Regeln, welche Bauteile Aussparungen bekommen.</summary>
public static class Aussparungsregeln
{
    /// <summary>Katalogklassen (mit Basisklassen), in denen Aussparungen vorgeschlagen werden.</summary>
    public static readonly string[] Klassen = ["Wand", "Decke", "Dach", "Stuetze", "Traeger"];

    /// <summary>IFC-Entitäten der nicht übernommenen Produkte (Hintergrund), in denen Aussparungen vorgeschlagen werden – am
    /// Xbim-Schema geprüft (Test AussparungVorschlagTests.Entitaeten_GibtEsImXbimSchema).</summary>
    public static readonly IReadOnlySet<string> Entitaeten = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "IfcWall", "IfcWallStandardCase", "IfcWallElementedCase", "IfcSlab", "IfcSlabStandardCase", "IfcSlabElementedCase", "IfcRoof",
        "IfcBeam", "IfcBeamStandardCase", "IfcColumn", "IfcColumnStandardCase",
    };

    /// <summary>Element einer der <see cref="Klassen"/> und keine Bekleidung (Nachtrag B3c). Das Material entscheidet erst der Vorschlag
    /// (<see cref="Bauteilmaterial"/>): ein Bauteil ohne Beton bzw. Mauerwerk ist aussparungsfähig, bekommt aber keine.</summary>
    public static bool Aussparungsfaehig(Katalog k, Element e) => Klassen.Any(c => k.IstArt(e, c)) && !Pruefkoerperbau.IstBekleidung(k, e);

    /// <summary>Prüfkörper mit Netz: Element einer der <see cref="Klassen"/> bzw. Hintergrund-Produkt einer der <see cref="Entitaeten"/>;
    /// nie eine Bekleidung (IfcCovering, Nachtrag B3c).</summary>
    public static bool Aussparungsfaehig(Katalog k, Modell m, Pruefkoerper pk) =>
        pk.Netz is not null && !pk.IstLeitung && !pk.Bekleidung
        && (pk.ElementId is { } id ? m.Finde(id) is { } e && Aussparungsfaehig(k, e)
            : pk.Bereich == "architektur" && pk.Entitaet is { } en && Entitaeten.Contains(en) && pk.UnterlageId is not null && pk.GlobalId is not null);
}

public sealed class AussparungenVorschlagen : IBefehl
{
    public string Name => "aussparungen_vorschlagen";
    public string Beschreibung => AussparungTexte.Vorschlagen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("geschoss", "text", false, AussparungTexte.ParGeschossVorschlagen),
        new("bauteile", "liste", false, AussparungTexte.ParBauteile),
        new("leitungen", "liste", false, AussparungTexte.ParLeitungen),
    ];

    public void Ausfuehren(Kontext k, JsonObject p) => new Aussparungsplanung(k).Ausfuehren(p);
}

/// <summary>Rechnung und Nachführung eines Laufs von aussparungen_vorschlagen (Regeln im Dateikopf).</summary>
sealed class Aussparungsplanung(Kontext k)
{
    const double Tol = Geometrie3.Toleranz;
    const int ListeMax = 100;
    /// <summary>Grösste Lücke (mm, senkrecht zu den Schichten) zwischen zwei Bauteilen, die noch «direkt hintereinander» liegen und eine
    /// durchgehende Aussparung bekommen (Nachtrag B3c; technische Toleranz, Begründung im Dateikopf).</summary>
    public const double Kettenluecke = 1.0;
    readonly Modell m = k.Arbeit;
    readonly Katalog kat = k.Katalog;
    AussparungVorgaben v = null!;
    readonly List<string> liste = [];
    /// <summary>Zahl der Einzelzeilen je Grund, in der Folge des ersten Auftretens (Nachtrag B3e: die Zusammenfassung ist immer vollständig,
    /// die Einzelzeilen höchstens <see cref="ListeMax"/>).</summary>
    readonly Dictionary<string, int> jeGrund = new(StringComparer.Ordinal);
    readonly List<string> gruende = [];
    readonly Dictionary<string, Materialbefund> material = new(StringComparer.Ordinal);
    int nNeu, nNach, nVeraltet, nGeloescht, nGleich, nOeffnung, nErgaenzt, nSammel, nFest, nHand, nKette, nUnbekannt, nKeinRohbauGeloescht, nFestKeinRohbau;
    readonly SortedDictionary<string, int> keinRohbau = new(StringComparer.Ordinal);   // Durchgänge ohne Aussparung je Materialgruppe

    static string Z(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);
    static double Auf(double x) => Math.Ceiling(x * 10 - 1e-6) / 10;
    /// <summary>Aussparungsmass (b, h, d) auf ganze cm aufgerundet, nie kleiner als gerechnet (Entscheid Christian 19.10.2026, Nachtrag
    /// B4e); 1e-6 mm Toleranz gegen Gleitkommareste (95.0000000001 bleibt 100, 100.0000000001 bleibt 100).</summary>
    internal static double AufCm(double x) => Math.Ceiling(x / 10 - 1e-7) * 10;
    static double R1(double x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);
    static double R6(double x) { var r = Math.Round(x, 6, MidpointRounding.AwayFromZero); return r == 0 ? 0 : r; }
    static string Masse(bool rund, double b, double h) => rund ? $"Ø {Z(b)} mm" : $"{Z(b)} × {Z(h)} mm";
    string Kz(string? id) => m.Finde(id)?.Kennzeichen ?? id ?? "?";
    static string Name(Pruefkoerper b) => b.Kennzeichen ?? $"{b.Entitaet} {b.GlobalId}";
    static string Namen(IEnumerable<Pruefkoerper> b) => string.Join(", ", b.Select(Name));

    /// <summary>Schlüssel einer Kette (Bauteil-Kennungen sortiert, «|»); ein Bauteil: seine Kennung.</summary>
    internal static string Kettenschluessel(IEnumerable<string> kennungen) => string.Join("|", kennungen.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>Typenliste des Projekts (Nachtrag B3d), einmal je Lauf gelesen.</summary>
    Typenliste typen = null!;

    /// <summary>Material eines Bauteils (einmal je Lauf eingeteilt, AussparungMaterial.cs; Vorrang der Typenliste, Nachtrag B3d).</summary>
    Materialbefund Material(Pruefkoerper b)
    {
        if (!material.TryGetValue(b.Kennung, out var mb)) material[b.Kennung] = mb = Bauteilmaterial.Von(m, kat, b, typen);
        return mb;
    }

    public void Ausfuehren(JsonObject p)
    {
        v = AussparungVorgaben.Lesen(m, k.Hinweise);
        typen = Typenliste.Lesen(m);

        // ---- Auswahl
        var geschoss = Param.Text(p, "geschoss") is { } gtext ? m.HoleDerKlasse(gtext, "Geschoss") : null;
        var bauteilWahl = AussparungParam.Liste(p, "bauteile");
        var leitungWahl = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in AussparungParam.Liste(p, "leitungen"))
        {
            var e = m.Hole(x, "Leitung");
            if (!kat.IstArt(e, "Leitung"))
                throw new HlksFehler($"{e.Kennzeichen} ist keine Leitung ({e.Klasse}).", "Rohre oder Kanäle angeben (elemente_suchen mit klasse Leitung).");
            leitungWahl.Add(e.Id);
        }
        var bauteilSchluessel = new List<(string Angabe, string Schluessel)>();
        foreach (var x in bauteilWahl)
        {
            if (m.Finde(x) is { } e)
            {
                if (!Aussparungsregeln.Aussparungsfaehig(kat, e))
                    throw new HlksFehler($"{e.Kennzeichen} ({e.Klasse}) bekommt keine Aussparungen.",
                        "Wände, Decken, Dächer, Stützen oder Träger angeben bzw. GlobalId (mit Unterlage: «UL-1:GlobalId») eines Produkts der IFC-Unterlage.");
                bauteilSchluessel.Add((x, e.Id));
            }
            else bauteilSchluessel.Add((x, Aussparungsbezug.Normalisieren(m, x)!));
        }
        bool voll = geschoss is null && bauteilWahl.Count == 0 && leitungWahl.Count == 0;

        // ---- Prüfkörper: Leitungen und Bauteile (KICAD), Hintergrund der IFC-Unterlagen mit Öffnungen. Teile (IfcBuildingElementPart)
        //      bekommen keine Aussparung – die Lage-Heuristik für Teile ohne Bezug (B1) braucht es hier nicht (spart Zeit, Bernstrasse).
        var ids = m.Elemente.Where(e => kat.IstArt(e, "Leitung") || Aussparungsregeln.Aussparungsfaehig(kat, e)).Select(e => e.Id).ToList();
        var menge = Pruefkoerperbau.Erzeugen(k.Projekt, new PruefkoerperOptionen { Elemente = ids, DoppelteTeileWeglassen = false });
        foreach (var h in menge.Hinweise.Where(h => h.Contains("Netzdatei") || h.Contains("Leitung ohne")).Take(20)) k.Hinweise.Add(h);
        var leitungen = menge.Koerper.Where(x => x.IstLeitung && x.ElementId is not null).OrderBy(x => x.Kennzeichen, StringComparer.Ordinal).ToList();
        var bauteile = menge.Koerper.Where(x => Aussparungsregeln.Aussparungsfaehig(kat, m, x)).OrderBy(x => x.Kennung, StringComparer.Ordinal).ToList();
        var nachSchluessel = new Dictionary<string, Pruefkoerper>(StringComparer.Ordinal);
        foreach (var b in bauteile)
        {
            nachSchluessel.TryAdd(b.Kennung, b);
            foreach (var w in b.WeitereElemente) nachSchluessel.TryAdd(w, b);
        }

        // ---- Bereich: welche Bauteile werden behandelt
        var bereich = bauteile;
        if (bauteilSchluessel.Count > 0)
        {
            var gewaehlt = new HashSet<Pruefkoerper>();
            foreach (var (angabe, s) in bauteilSchluessel)
            {
                var treffer = bauteile.Where(b => Aussparungsbezug.Passt(s, b.Kennung) || b.WeitereElemente.Contains(s)).ToList();
                if (treffer.Count == 0)
                    throw new HlksFehler($"Bauteil «{angabe}» gibt es nicht bzw. es hat keinen Prüfkörper.",
                        "Kennzeichen einer Wand, Decke, Stütze oder eines Trägers bzw. «Unterlage:GlobalId» eines Produkts der IFC-Unterlage angeben (aussparungsliste und elemente_suchen helfen).");
                gewaehlt.UnionWith(treffer);
            }
            bereich = bereich.Where(gewaehlt.Contains).ToList();
        }
        if (geschoss is not null) bereich = bereich.Where(b => GeschossVon(b, b.Huelle.Mitte) == geschoss.Id).ToList();
        if (leitungWahl.Count > 0)
        {
            var aus = m.DerKlasse("Aussparung").Where(a => Aussparungsbezug.Leitungen(a).Any(leitungWahl.Contains))
                .SelectMany(Aussparungsbezug.BauteilSchluesselAlle).ToHashSet(StringComparer.Ordinal);
            var idx = new Raumindex<Pruefkoerper>(bereich, b => b.Huelle);
            var gequert = new HashSet<Pruefkoerper>();
            foreach (var l in leitungen.Where(l => leitungWahl.Contains(l.ElementId!)))
                foreach (var b in idx.Kandidaten(l.Huelle))
                    if (Durchgaenge.Finden(l, b).Count > 0) gequert.Add(b);
            bereich = bereich.Where(b => gequert.Contains(b) || aus.Contains(b.Kennung) || b.WeitereElemente.Any(aus.Contains)).ToList();
        }
        var bereichSet = bereich.ToHashSet();

        // ---- Bedarf je Bauteil (alle Materialien – feste Aussparungen decken auch ohne Rohbau), bestehende Aussparungen. Mit Auswahl
        //      gehören die Ketten-Nachbarn dazu (Nachtrag B3c: eine durchgehende Aussparung wird immer über alle Schichten gerechnet)
        var bedarf = BedarfRechnen(leitungen, bereich, menge);
        if (!voll)
        {
            var alleIdx = new Raumindex<Pruefkoerper>(bauteile, b => b.Huelle);
            for (int runde = 0; runde < 8; runde++)
            {
                var nachbarn = KettenNachbarn(bedarf, bereichSet, alleIdx);
                if (nachbarn.Count == 0) break;
                bereichSet.UnionWith(nachbarn);
                bedarf.AddRange(BedarfRechnen(leitungen, nachbarn, menge));
            }
            bereich = bauteile.Where(bereichSet.Contains).ToList();
        }
        var bestehend = m.DerKlasse("Aussparung")
            .Where(a => voll || Aussparungsbezug.BauteilSchluesselAlle(a).Any(s => nachSchluessel.TryGetValue(s, out var pk) && bereichSet.Contains(pk)))
            .OrderBy(a => a.Kennzeichen, StringComparer.Ordinal).ToList();
        var beurteilbar = new List<Element>();
        foreach (var a in bestehend)
        {
            if (Beurteilbar(a, nachSchluessel) is { } grund) { k.Hinweise.Add($"{a.Kennzeichen}: {grund} – nicht nachgeführt."); continue; }
            beurteilbar.Add(a);
        }

        // ---- feste Aussparungen: gelten sie noch? (je Bauteil und Leitung, Nachtrag B3c)
        var verbraucht = new HashSet<Aussparungsbedarf>();
        var aenderungen = new Dictionary<string, Element>(StringComparer.Ordinal);   // Id → neuer Stand
        var veraltetGrund = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in beurteilbar.Where(a => AussparungStatus.Fest(AussparungStatus.Von(a))))
        {
            var schluessel = SchluesselAlle(a, nachSchluessel);
            if (Aussparungslage.Von(a) is not { } lage)
            {
                k.Hinweise.Add($"{a.Kennzeichen}: Lage, Achse oder Masse fehlen (z. B. von Hand angelegt) – nicht nachgeführt.");
                continue;
            }
            var eigene = Aussparungsbezug.Leitungen(a);
            var gedeckt = bedarf.Where(b => schluessel.Contains(b.Bauteil.Kennung) && Deckt(lage, b)).ToList();
            bool Ganz(string l) => schluessel.All(s => gedeckt.Any(b => b.LeitungId == l && b.Bauteil.Kennung == s));
            var fehlend = eigene.Where(l => !Ganz(l)).ToList();
            if (schluessel.Any(s => !nachSchluessel.ContainsKey(s)) || fehlend.Count > 0 || (eigene.Count == 0 && gedeckt.Count == 0))
            {
                string grund = schluessel.Any(s => !nachSchluessel.ContainsKey(s)) ? "Bauteil gelöscht bzw. nicht mehr in der Unterlage"
                    : fehlend.Count > 0 ? string.Join("; ", fehlend.Select(l => LeitungGrund(l, schluessel, bedarf)))
                    : "keine Leitung geht mehr hindurch";
                veraltetGrund[a.Id] = grund;
                continue;
            }
            nFest++;
            verbraucht.UnionWith(gedeckt);
            var ohneRohbau = schluessel.Select(s => nachSchluessel[s]).Where(b => Material(b).KeinRohbau).ToList();
            if (ohneRohbau.Count > 0)
            {
                nFestKeinRohbau++;
                Liste("fest, kein Beton bzw. Mauerwerk: bleibt (abgegeben)",
                      $"{a.Kennzeichen}{Nr(a)} ({AussparungStatus.Von(a)}): {string.Join("; ", ohneRohbau.Select(b => $"{Name(b)} ist kein Beton bzw. Mauerwerk ({Material(b).Text})"))} " +
                      "– bleibt, weil schon abgegeben; nach der Regel nicht mehr nötig (Entscheid Christian 19.10.2026), bei Bedarf zurückziehen.");
            }
            var dazu = gedeckt.Select(b => b.LeitungId).Distinct().Where(l => !eigene.Contains(l) && Ganz(l)).ToList();
            if (dazu.Count > 0)
            {
                var e = a.Kopie();
                Aussparungsbezug.LeitungenSetzen(e, eigene.Concat(dazu.OrderBy(Kz, StringComparer.Ordinal)));
                aenderungen[e.Id] = e;
                nErgaenzt++;
                Liste("fest: Leitung ergänzt", $"{a.Kennzeichen}{Nr(a)} ({AussparungStatus.Von(a)}): {string.Join(", ", dazu.Select(Kz))} geht ganz hindurch – als Leitung ergänzt, Geometrie und Status bleiben.");
            }
        }

        // ---- Material (Nachtrag B3c): neue Aussparungen nur in Beton, Mauerwerk bzw. bei unbekanntem Material; dann zu Ketten
        //      direkt hintereinander liegender Bauteile zusammensetzen
        var relevant = new List<Aussparungsbedarf>();
        foreach (var b in bedarf.Where(b => !verbraucht.Contains(b)))
        {
            var mb = Material(b.Bauteil);
            if (mb.Aussparung) { relevant.Add(b); continue; }
            var gruppe = mb.Gruppe is { } g ? Materialwortliste.Name(g) : "?";
            keinRohbau[gruppe] = keinRohbau.GetValueOrDefault(gruppe) + 1;
        }
        var ketten = Ketten(relevant);

        // ---- von Hand gewählte Kernbohrungen: ihr Bedarf bleibt ihnen vorbehalten (eigene runde Gruppe, nicht zusammengefasst)
        var vorschlaege = beurteilbar.Where(a => AussparungStatus.Von(a) == AussparungStatus.Vorgeschlagen).ToList();
        var handGruppen = new Dictionary<Element, Aussparungsgruppe>();
        var vorbehalten = new HashSet<Aussparungsbedarf>();
        foreach (var a in vorschlaege.Where(AussparungForm.KernbohrungVonHand).OrderBy(a => a.Kennzeichen, StringComparer.Ordinal))
        {
            var eigene = Aussparungsbezug.Leitungen(a);
            if (eigene.Count != 1) continue;   // mehrere Leitungen können nicht rund sein – Schreiben hebt die Wahl auf (Liste)
            var s = SchluesselAlle(a, nachSchluessel);
            var alt = a.Geometrie?.Punkte is { Count: > 0 } pk && pk[0].Length >= 3 ? V3.Aus(pk[0]) : (V3?)null;
            var b = ketten.Where(x => !vorbehalten.Contains(x) && x.Bauteile.Any(bt => s.Contains(bt.Kennung)) && x.LeitungId == eigene[0])
                .OrderBy(x => alt is V3 am ? (x.Mitte - am).Laenge : 0).FirstOrDefault();
            if (b is null) continue;   // Leitung quert nicht mehr bzw. ist anderweitig versorgt → veraltet bzw. aufgegangen (unten)
            vorbehalten.Add(b);
            handGruppen[a] = Rundgruppe(b);
        }

        // ---- übriger Bedarf zusammenfassen, Vorschläge wiedererkennen
        var gruppen = Zusammenfassen(ketten.Where(b => !vorbehalten.Contains(b)).ToList(), v.Zusammenfassen);
        nSammel = gruppen.Count(g => g.Sammel);
        var zuordnung = Wiedererkennen(vorschlaege.Where(a => !handGruppen.ContainsKey(a)).ToList(), gruppen, nachSchluessel);
        foreach (var (a, g) in handGruppen) { zuordnung[a] = g; gruppen.Add(g); }
        nHand = handGruppen.Count;
        nKette = gruppen.Count(g => g.Kette);
        nUnbekannt = gruppen.Count(g => g.Bauteile.Any(b => Material(b).Unbekannt));
        var gruppeVon = zuordnung.ToDictionary(x => x.Value, x => x.Key);   // Gruppe → Element

        // nicht erkannte Vorschläge: kein Rohbau mehr (löschen), aufgegangen (löschen) oder veraltet
        var loeschen = new List<(Element A, string Grund)>();
        foreach (var a in vorschlaege.Where(a => !zuordnung.ContainsKey(a)))
        {
            var s = SchluesselAlle(a, nachSchluessel);
            var eigene = Aussparungsbezug.Leitungen(a);
            var vorhanden = s.Where(nachSchluessel.ContainsKey).Select(x => nachSchluessel[x]).ToList();
            if (vorhanden.Count > 0 && vorhanden.Count == s.Count && vorhanden.All(b => Material(b).KeinRohbau))
                loeschen.Add((a, $"kein Beton bzw. Mauerwerk: {string.Join("; ", vorhanden.Select(b => $"{Name(b)} ({Material(b).Text})"))} – keine Aussparung (Entscheid Christian 19.10.2026)"));
            else if (bedarf.Any(b => s.Contains(b.Bauteil.Kennung) && eigene.Contains(b.LeitungId))) loeschen.Add((a, ""));
            else veraltetGrund[a.Id] = s.Count == 0 || s.Any(x => !nachSchluessel.ContainsKey(x)) ? "Bauteil gelöscht bzw. nicht mehr in der Unterlage"
                : eigene.Count == 0 ? "ohne Leitung" : string.Join("; ", eigene.Select(l => LeitungGrund(l, s, bedarf)));
        }

        // ---- anwenden: löschen, veraltet, nachführen, neu
        foreach (var (a, _) in loeschen) k.Loeschen(m.Finde(a.Id)!);
        foreach (var (id, grund) in veraltetGrund.OrderBy(x => Kz(x.Key), StringComparer.Ordinal))
        {
            var a = m.Finde(id)!;
            var e = a.Kopie();
            e.Eigenschaften["status"] = new Wert { Inhalt = AussparungStatus.Veraltet, Herkunft = k.Herkunft with { Art = "berechnung", Quelle = $"aussparungen_vorschlagen: {grund}" } };
            aenderungen[e.Id] = e;
            nVeraltet++;
            Liste(VeraltetArt(grund), $"{a.Kennzeichen}{Nr(a)} ({AussparungStatus.Von(a)} → veraltet): {grund}.");
        }
        foreach (var a in bestehend.Where(a => !aenderungen.ContainsKey(a.Id) && AussparungStatus.Von(a) == AussparungStatus.Veraltet))
            aenderungen[a.Id] = a.Kopie();   // nur für das Aufräumen der Bezüge
        foreach (var e in aenderungen.Values.OrderBy(e => e.Kennzeichen, StringComparer.Ordinal))
        {
            BezuegeAufraeumen(e);
            if (Json.Text(e) != Json.Text(m.Finde(e.Id)!)) k.Setzen(e);
        }
        var neue = new List<(Aussparungsgruppe G, Element E)>();
        foreach (var g in gruppen)
        {
            if (gruppeVon.TryGetValue(g, out var alt))
            {
                var e = Schreiben(m.Finde(alt.Id)!, g);
                if (Json.Text(e) != Json.Text(m.Finde(alt.Id)!)) { k.Setzen(e); nNach++; Liste("nachgeführt", $"{e.Kennzeichen}{Nr(e)} nachgeführt: {Beschreibung(e, g)}."); }
                else nGleich++;
            }
        }
        foreach (var g in gruppen.Where(g => !gruppeVon.ContainsKey(g))
                     .OrderBy(g => Name(g.Bauteil), StringComparer.Ordinal).ThenBy(g => R1(g.Mitte.X)).ThenBy(g => R1(g.Mitte.Y)).ThenBy(g => R1(g.Mitte.Z)))
        {
            var e = k.Setzen(Schreiben(null, g));
            neue.Add((g, e));
            nNeu++;
            Liste("neu", $"{e.Kennzeichen} neu: {Beschreibung(e, g)}.");
        }
        foreach (var (a, grund) in loeschen)
        {
            nGeloescht++;
            if (grund.Length > 0) { nKeinRohbauGeloescht++; Liste("gelöscht: kein Beton bzw. Mauerwerk", $"{a.Kennzeichen}{Nr(a)} gelöscht: {grund}."); continue; }
            var s = SchluesselAlle(a, nachSchluessel);
            var eigene = Aussparungsbezug.Leitungen(a);
            var ziel = gruppen.FirstOrDefault(g => g.Bauteile.Any(b => s.Contains(b.Kennung)) && g.LeitungIds.Any(eigene.Contains)) is { } zg
                ? (gruppeVon.TryGetValue(zg, out var ze) ? ze.Kennzeichen : neue.First(x => x.G == zg).E.Kennzeichen)
                : beurteilbar.FirstOrDefault(f => AussparungStatus.Fest(AussparungStatus.Von(f)) && !veraltetGrund.ContainsKey(f.Id)
                      && SchluesselAlle(f, nachSchluessel).Any(s.Contains) && Aussparungsbezug.Leitungen(aenderungen.GetValueOrDefault(f.Id) ?? f).Any(eigene.Contains))?.Kennzeichen ?? "?";
            Liste("gelöscht: aufgegangen", $"{a.Kennzeichen}{Nr(a)} gelöscht: aufgegangen in {ziel}.");
        }

        // ---- von Hand gewählte Kernbohrungen: Nachbarn unter abstand_zusammenfassen nennen (nicht zusammengefasst)
        foreach (var a in handGruppen.Keys.OrderBy(a => a.Kennzeichen, StringComparer.Ordinal))
            if (m.Finde(a.Id) is { } jetzt && AussparungForm.Nachbarn(m, jetzt, v.Zusammenfassen) is { Count: > 0 } nb)
                Liste("Kernbohrung von Hand: Nachbar unter abstand_zusammenfassen",
                      $"{jetzt.Kennzeichen}{Nr(jetzt)} (Kernbohrung von Hand): {string.Join(", ", nb.Select(x => AussparungForm.NachbarText(x.Nachbar, x.Abstand)))} " +
                      $"– unter abstand_zusammenfassen {Z(v.Zusammenfassen)} mm, wird nicht automatisch zusammengefasst (zusammenfassen: aussparung_form rechteckig, dann aussparungen_vorschlagen).");

        // ---- Hinweise: Zusammenfassung und Liste (Vorschau)
        var kopf = $"Aussparungen: {nNeu} neu, {nNach} nachgeführt, {nGleich} unverändert, {nVeraltet} veraltet, {nGeloescht} gelöscht (aufgegangen); " +
                   $"{nFest} feste gelten weiter{(nErgaenzt > 0 ? $" ({nErgaenzt} mit ergänzter Leitung)" : "")}; {nSammel} Sammelaussparung(en); " +
                   $"{nHand} Kernbohrung(en) von Hand; {nOeffnung} Durchgang/Durchgänge in Architektenöffnungen. Vorschläge rechteckig, Kernbohrungen nur von Hand " +
                   $"(aussparung_form, {AussparungForm.Entscheid}). Vorgaben {v.Element}: Spiel {Z(v.Spiel)} mm, Zusammenfassen unter {Z(v.Zusammenfassen)} mm. " +
                   $"Nur in Beton und Mauerwerk ({AussparungForm.Entscheid}): {nKette} durchgehend über mehrere Bauteile, {nUnbekannt} mit unbekanntem Material " +
                   $"(trotzdem vorgeschlagen, materialUnbekannt – prüfen), {keinRohbau.Values.Sum()} Durchgang/Durchgänge ohne Aussparung (kein Rohbau" +
                   $"{(keinRohbau.Count > 0 ? ": " + string.Join(", ", keinRohbau.Select(x => $"{x.Key} {x.Value}")) : "")})" +
                   $"{(nKeinRohbauGeloescht > 0 ? $", {nKeinRohbauGeloescht} Vorschlag/Vorschläge darin gelöscht" : "")}" +
                   $"{(nFestKeinRohbau > 0 ? $", {nFestKeinRohbau} feste darin bleiben (abgegeben)" : "")}.";
        k.Hinweise.Insert(0, kopf);
        // Nachtrag B3d: woher das Material der gequerten Bauteile stammt (Typenliste, aus dem Typnamen) – sichtbar in den Hinweisen
        var gequerteMat = bedarf.Select(b => b.Bauteil).Distinct().Select(b => Material(b)).ToList();
        int ausListe = gequerteMat.Count(x => x.Herkunft == Materialherkunft.Typenliste), ausTyp = gequerteMat.Count(x => x.Herkunft == Materialherkunft.Typname);
        if (ausListe > 0 || ausTyp > 0)
            k.Hinweise.Insert(1, $"Material der {gequerteMat.Count} gequerten Bauteile: {ausListe} über die Typenliste ({typen.Eintraege.Count} Einträge), {ausTyp} aus dem Typnamen " +
                                 $"({Materialwortliste.EntscheidTypname}); {gequerteMat.Count(x => x.Unbekannt)} unbekannt – typmaterial zeigt die Typen zum Festlegen.");
        // Nachtrag B3e: die Zahl je Grund immer vollständig, die Einzelzeilen höchstens ListeMax
        if (liste.Count > 0)
        {
            k.Hinweise.Add($"Liste je Grund ({liste.Count} Zeile(n), vollständig; Einzelzeilen darunter höchstens {ListeMax}):");
            k.Hinweise.AddRange(gruende.Select(g => $"  {g} – {jeGrund[g]}"));
        }
        k.Hinweise.AddRange(liste.Take(ListeMax));
        if (liste.Count > ListeMax) k.Hinweise.Add($"… und {liste.Count - ListeMax} weitere (Zahlen je Grund oben; aussparungsliste zeigt alle Aussparungen).");
    }

    void Liste(string grund, string text)
    {
        liste.Add(text);
        if (!jeGrund.TryGetValue(grund, out var n)) gruende.Add(grund);
        jeGrund[grund] = n + 1;
    }

    /// <summary>Art eines Grunds «veraltet» für die Zusammenfassung (Nachtrag B3e) – aus den Texten oben und von <see cref="LeitungGrund"/>;
    /// bei mehreren Leitungen zählt der erste passende.</summary>
    static string VeraltetArt(string grund) =>
        grund.StartsWith("Bauteil gelöscht", StringComparison.Ordinal) ? "veraltet: Bauteil gelöscht bzw. nicht mehr in der Unterlage"
        : grund.Contains("Leitung gelöscht", StringComparison.Ordinal) ? "veraltet: Leitung gelöscht"
        : grund.Contains("passt nicht mehr hindurch", StringComparison.Ordinal) ? "veraltet: Leitung passt nicht mehr hindurch"
        : grund.Contains("quert", StringComparison.Ordinal) ? "veraltet: Leitung quert nicht mehr"
        : "veraltet: keine Leitung mehr";

    static string Nr(Element a) => a.Text("nummer") is { Length: > 0 } n ? $" ({n})" : "";

    // ------------------------------------------------------------------ aussparung_form (Nachtrag B3b)

    /// <summary>aussparung_form: setzt die Form der Ziele (geprüft: Aussparungen mit Status vorgeschlagen oder veraltet und genau einer
    /// Leitung). Das Mass wird aus dem aktuellen Durchgang der Leitung durch das Bauteil neu gerechnet – dieselbe Rechnung wie
    /// aussparungen_vorschlagen: rund d = umschliessender Kreis der Projektion + 2 × Spiel, rechteckig b × h = umschliessendes Rechteck
    /// + 2 × Spiel. Lage, Achse, Tiefe, Nummer und Status bleiben. Der Durchgang muss noch durch die Mitte der Aussparung gehen
    /// (Abstand ≤ 2 × Geometrie3.Toleranz, Rundung der gespeicherten Mitte auf 0.1 mm) – sonst wird der ganze Befehl abgelehnt
    /// (zuerst nachführen). rund: Herkunft der Form = die Wahl (mensch bzw. ki); rechteckig: zurück zur Regel (berechnung).</summary>
    internal void FormWaehlen(List<Element> ziele, string form)
    {
        v = AussparungVorgaben.Lesen(m);
        bool rund = form == AussparungForm.Rund;
        var leitIds = ziele.SelectMany(Aussparungsbezug.Leitungen).Where(id => m.Finde(id) is not null).ToHashSet(StringComparer.Ordinal);
        var unterlagen = ziele.SelectMany(a => a.Bezuege.Where(x => x.Key.StartsWith("unterlage", StringComparison.Ordinal) && Aussparungsbezug.IstBauteilrolle(x.Key)).Select(x => x.Value))
            .Distinct().ToList();
        var ids = leitIds.Concat(m.Elemente.Where(e => Aussparungsregeln.Aussparungsfaehig(kat, e)).Select(e => e.Id)).ToList();
        var menge = Pruefkoerperbau.Erzeugen(k.Projekt, new PruefkoerperOptionen
        {
            Elemente = ids, DoppelteTeileWeglassen = false, Hintergrund = unterlagen.Count > 0, Unterlagen = unterlagen.Count > 0 ? unterlagen : null,
        });
        var leitungen = menge.Koerper.Where(x => x.IstLeitung && x.ElementId is not null && leitIds.Contains(x.ElementId)).OrderBy(x => x.Kennzeichen, StringComparer.Ordinal).ToList();
        var bauteile = menge.Koerper.Where(x => Aussparungsregeln.Aussparungsfaehig(kat, m, x)).OrderBy(x => x.Kennung, StringComparer.Ordinal).ToList();
        var nachSchluessel = new Dictionary<string, Pruefkoerper>(StringComparer.Ordinal);
        foreach (var b in bauteile)
        {
            nachSchluessel.TryAdd(b.Kennung, b);
            foreach (var w in b.WeitereElemente) nachSchluessel.TryAdd(w, b);
        }
        var schluessel = ziele.SelectMany(a => SchluesselAlle(a, nachSchluessel)).ToHashSet(StringComparer.Ordinal);
        // dieselbe Rechnung wie aussparungen_vorschlagen: je Bauteil, nur Rohbau bzw. unbekanntes Material, zu Ketten zusammengesetzt
        var bedarf = Ketten(BedarfRechnen(leitungen, bauteile.Where(b => schluessel.Contains(b.Kennung)).ToList(), menge).Where(b => Material(b.Bauteil).Aussparung).ToList());

        var fehler = new List<string>();
        var treffer = new List<(Element A, Aussparungslage Lage, Aussparungsbedarf B)>();
        double cosEbene = Math.Cos(Aussparungsgeometrie.EbenenWinkel * Math.PI / 180);
        foreach (var a in ziele)
        {
            if (Beurteilbar(a, nachSchluessel) is { } grund) { fehler.Add($"{a.Kennzeichen}: {grund}"); continue; }
            var sa = SchluesselAlle(a, nachSchluessel);
            if (sa.Count == 0 || sa.Any(x => !nachSchluessel.ContainsKey(x))) { fehler.Add($"{a.Kennzeichen}: Bauteil gelöscht bzw. nicht mehr in der Unterlage"); continue; }
            var s = Kettenschluessel(sa);
            if (Aussparungslage.Von(a) is not { } lage) { fehler.Add($"{a.Kennzeichen}: Lage, Achse oder Masse fehlen"); continue; }
            var l = Aussparungsbezug.Leitungen(a)[0];
            if (m.Finde(l) is null) { fehler.Add($"{a.Kennzeichen}: Leitung gelöscht"); continue; }
            var b = bedarf.Where(x => x.Schluessel == s && x.LeitungId == l).OrderBy(x => (x.Mitte - lage.Mitte).Laenge).FirstOrDefault();
            if (b is null)
            {
                var ohne = sa.Select(x => nachSchluessel[x]).Where(x => Material(x).KeinRohbau).ToList();
                fehler.Add(ohne.Count > 0
                    ? $"{a.Kennzeichen}: {Namen(ohne)} ist kein Beton bzw. Mauerwerk ({string.Join("; ", ohne.Select(x => Material(x).Text))}) – keine Aussparung (Entscheid Christian 19.10.2026)"
                    : $"{a.Kennzeichen}: {Kz(l)} quert {Namen(sa.Select(x => nachSchluessel[x]))} nicht (mehr) als Durchgang, der eine Aussparung braucht");
                continue;
            }
            if ((b.Mitte - lage.Mitte).Laenge > 2 * Tol || Math.Abs(b.Achse.Mal(lage.Achse)) < cosEbene)
            { fehler.Add($"{a.Kennzeichen}: {Kz(l)} geht nicht mehr durch die Mitte der Aussparung (verschoben bzw. geändert)"); continue; }
            treffer.Add((a, lage, b));
        }
        if (fehler.Count > 0)
            throw new HlksFehler($"Form nicht gesetzt – das Mass ist nicht rechenbar: {string.Join("; ", fehler)}.",
                "Vorschläge zuerst mit aussparungen_vorschlagen nachführen, dann aussparung_form. Veraltete Aussparungen werden nicht verschoben: " +
                "für eine verschobene Leitung legt aussparungen_vorschlagen einen neuen Vorschlag an – dort die Form wählen.");

        foreach (var (a, lage, b) in treffer)
        {
            var e = FormSchreiben(a, lage, b, rund);
            k.Setzen(e);
            var vorher = a.Text("form") == AussparungForm.Rund ? Masse(true, a.Zahl("d") ?? 0, 0) : Masse(false, a.Zahl("b") ?? 0, a.Zahl("h") ?? 0);
            k.Hinweise.Add($"{a.Kennzeichen}{Nr(a)}: {a.Text("form")} {vorher} → " +
                           (rund ? $"rund {Masse(true, e.Zahl("d") ?? 0, 0)}, Kernbohrung (von Hand gewählt)" : $"rechteckig {Masse(false, e.Zahl("b") ?? 0, e.Zahl("h") ?? 0)}, Aussparung (Regel, Handwahl aufgehoben)") +
                           (AussparungStatus.Von(a) == AussparungStatus.Veraltet ? "; Status bleibt veraltet" : "") + ".");
        }
        foreach (var (a, _, _) in treffer)
        {
            var jetzt = m.Finde(a.Id)!;
            var nb = AussparungForm.Nachbarn(m, jetzt, v.Zusammenfassen);
            if (rund && nb.Count > 0)
                k.Hinweise.Add($"{jetzt.Kennzeichen}{Nr(jetzt)}: {string.Join(", ", nb.Select(x => AussparungForm.NachbarText(x.Nachbar, x.Abstand)))} – unter abstand_zusammenfassen " +
                               $"{Z(v.Zusammenfassen)} mm; eine Kernbohrung von Hand wird nicht automatisch zusammengefasst.");
            var mit = nb.Where(x => AussparungStatus.Von(x.Nachbar) == AussparungStatus.Vorgeschlagen && !AussparungForm.KernbohrungVonHand(x.Nachbar)).ToList();
            if (!rund && AussparungStatus.Von(jetzt) == AussparungStatus.Vorgeschlagen && mit.Count > 0)
                k.Hinweise.Add($"{jetzt.Kennzeichen}{Nr(jetzt)}: {string.Join(", ", mit.Select(x => AussparungForm.NachbarText(x.Nachbar, x.Abstand)))} – unter abstand_zusammenfassen " +
                               $"{Z(v.Zusammenfassen)} mm; aussparungen_vorschlagen fasst sie zu einer Sammelaussparung zusammen.");
        }
    }

    /// <summary>Element mit der gewählten Form und dem neu gerechneten Mass (Lage, Achse, Tiefe bleiben).</summary>
    Element FormSchreiben(Element a, Aussparungslage lage, Aussparungsbedarf b, bool rund)
    {
        var e = a.Kopie();
        var bt = Name(b.Bauteil);
        var hM = k.Herkunft with
        {
            Art = "berechnung",
            Quelle = $"{(rund ? "umschliessender Kreis" : "umschliessendes Rechteck")} der Projektion des Querschnitts von {b.Leitung.Kennzeichen} ({b.Leitung.Massquelle}) " +
                     $"auf {bt} über die ganze Tiefe + 2 × Spiel {Z(v.Spiel)} mm ({v.SpielQuelle})",
        };
        var hF = rund ? k.Herkunft with { Quelle = $"von Hand gewählt: Kernbohrung (aussparung_form, {AussparungForm.Entscheid})" }
                      : k.Herkunft with { Art = "berechnung", Quelle = FormRegelQuelle + " – Handwahl aufgehoben (aussparung_form)" };
        var hL = k.Herkunft with { Art = "berechnung", Quelle = $"Lage aus dem Durchgang durch {bt}" };
        e.Eigenschaften["form"] = new Wert { Inhalt = JsonValue.Create(rund ? AussparungForm.Rund : AussparungForm.Rechteckig), Herkunft = hF };   // Herkunft = die Wahl
        e.Eigenschaften["art"] = new Wert { Inhalt = JsonValue.Create(rund ? "kernbohrung" : "aussparung"), Herkunft = hF };
        // Koten wie Schreiben aus dem Durchgang (liegt höchstens 2 × Toleranz neben der gespeicherten Mitte) – ein späterer
        // aussparungen_vorschlagen sieht dann keine Rundungsdifferenz
        if (rund)
        {
            Zahl(e, "d", AufCm(b.D), "mm", hM); Weg(e, "b"); Weg(e, "h");
            Zahl(e, "kote_uk", R1(b.Mitte.Z), "mm", hL); Weg(e, "kote_ok");
        }
        else
        {
            double bb = AufCm(b.B), hh = AufCm(b.H);
            Zahl(e, "b", bb, "mm", hM); Zahl(e, "h", hh, "mm", hM); Weg(e, "d");
            var (uk, ok) = new Aussparungslage(b.Mitte, b.Achse, b.U, b.V, false, bb, hh, lage.Tiefe).Koten();
            Zahl(e, "kote_uk", R1(uk), "mm", hL); Zahl(e, "kote_ok", R1(ok), "mm", hL);
        }
        return e;
    }

    string Beschreibung(Element e, Aussparungsgruppe g)
    {
        bool rund = e.Text("form") == "rund";
        var masse = Masse(rund, rund ? e.Zahl("d") ?? 0 : e.Zahl("b") ?? 0, e.Zahl("h") ?? 0);
        return $"{(rund ? "rund" : "rechteckig")} {masse}, {e.Text("art")}, Tiefe {Z(e.Zahl("tiefe") ?? 0)} mm, in {Name(g.Bauteil)}" +
               (g.Kette ? $" (durchgehend über {g.Bauteile.Count} Bauteile: {Namen(g.Bauteile)})" : "") +
               $", Leitungen {string.Join(", ", g.LeitungIds.Select(Kz).Order(StringComparer.Ordinal))}" +
               (g.Sammel ? $" (Sammelaussparung aus {g.Teile.Count})" : "") +
               $", Mitte ({Z(g.Mitte.X)}, {Z(g.Mitte.Y)}, {Z(g.Mitte.Z)}); Material {e.Text("material") ?? "?"}" +
               (e.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue jv && jv.TryGetValue<bool>(out var u) && u ? " – unbekannt, prüfen" : "");
    }

    /// <summary>Warum eine Leitung nicht (mehr) durch die Aussparung geht (schluessel: Bauteile der Aussparung, auf Prüfkörper umgelegt).</summary>
    string LeitungGrund(string leitungId, IReadOnlyCollection<string> schluessel, List<Aussparungsbedarf> bedarf)
    {
        if (m.Finde(leitungId) is null) return "Leitung gelöscht";
        var quert = schluessel.Where(s => bedarf.Any(b => b.Bauteil.Kennung == s && b.LeitungId == leitungId)).ToList();
        if (quert.Count == schluessel.Count && quert.Count > 0) return $"{Kz(leitungId)} passt nicht mehr hindurch (Querschnitt ausserhalb)";
        return quert.Count == 0 || schluessel.Count == 1 ? $"{Kz(leitungId)} quert das Bauteil nicht mehr"
            : $"{Kz(leitungId)} quert nicht mehr alle Bauteile der durchgehenden Aussparung";
    }

    /// <summary>Bauteilschlüssel der Aussparung (Haupt zuerst, dann die weiteren), auf den Prüfkörper umgelegt (Stück einer geteilten
    /// Wand → Träger des Netzes).</summary>
    static List<string> SchluesselAlle(Element a, Dictionary<string, Pruefkoerper> nachSchluessel) =>
        Aussparungsbezug.BauteilSchluesselAlle(a).Select(s => nachSchluessel.TryGetValue(s, out var pk) ? pk.Kennung : s).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>null = beurteilbar; sonst der Grund, warum die Aussparung nicht nachgeführt wird (Bauteil da, aber ohne Prüfkörper;
    /// Netzdatei der Unterlage fehlt) – für jedes ihrer Bauteile.</summary>
    string? Beurteilbar(Element a, Dictionary<string, Pruefkoerper> nachSchluessel)
    {
        if (Aussparungsbezug.BauteilSchluessel(a) is null) return "ohne Bauteilbezug";
        foreach (var s in Aussparungsbezug.BauteilSchluesselAlle(a))
        {
            if (nachSchluessel.ContainsKey(s)) continue;
            if (m.Finde(s) is { } e) return $"Bauteil {e.Kennzeichen} hat keinen Prüfkörper";   // Element da, aber ohne Prüfkörper
            // Hintergrund «Unterlage-Id:GlobalId»: ohne Netzdatei nicht beurteilbar
            if (s.IndexOf(':') is var j and > 0 && m.Finde(s[..j]) is { } u && !NetzDa(u)) return $"Netzdatei der Unterlage {u.Kennzeichen} fehlt";
        }
        return null;   // gelöscht bzw. nicht mehr in der Unterlage → veraltet
    }

    bool NetzDa(Element unterlage)
    {
        if (unterlage.Text("netz") is not { Length: > 0 } name) return false;
        try { return File.Exists(Unterlagen.Pfad(k.Projekt, name)); }
        catch (HlksFehler) { return false; }
    }

    /// <summary>Bezüge auf gelöschte Leitungen bzw. ein gelöschtes Bauteil entfernen (sonst meldet P08).</summary>
    void BezuegeAufraeumen(Element e)
    {
        var weg = e.Bezuege.Where(x => (Aussparungsbezug.IstLeitungsrolle(x.Key) || Aussparungsbezug.IstBauteilrolle(x.Key)) && m.Finde(x.Value) is null).ToList();
        if (weg.Count == 0) return;
        var leitungen = Aussparungsbezug.Leitungen(e).Where(l => m.Finde(l) is not null).ToList();
        foreach (var (rolle, _) in weg.Where(x => !Aussparungsbezug.IstLeitungsrolle(x.Key))) e.Bezuege.Remove(rolle);
        Aussparungsbezug.LeitungenSetzen(e, leitungen);
        Liste("Bezug auf gelöschte Leitung bzw. gelöschtes Bauteil entfernt",
              $"{e.Kennzeichen}: Bezug auf gelöschte{(weg.Any(x => Aussparungsbezug.IstBauteilrolle(x.Key)) ? "s Bauteil bzw." : "")} Leitung(en) entfernt ({string.Join(", ", weg.Select(x => x.Key))}).");
    }

    /// <summary>Geschoss der Aussparung: das des Bauteils (Element bzw. Hintergrund nach Kote), sonst aus der Höhe der Mitte.
    /// Hauptlauf 19.10.2026 (Befund Bernstrasse 42, Aussparungsplan EG mit Aussparungen auf +5.47): Wände, Stützen und Träger
    /// reichen in Revit/Archicad oft über mehrere Geschosse – liegt die Mitte nicht im Höhenband ihres Geschosses, gilt das Geschoss
    /// aus der Höhe.
    /// Decken und Dächer (Hauptlauf 19.10.2026, SIA 400:2000 B.10.3 und SIA 410/2 Ziff. 2.3 – Christian: «Darstellung nicht nach
    /// SIA»): Deckenaussparungen werden im Grundriss des Geschosses **direkt unter der Decke** eingetragen → Geschoss, in dessen
    /// Höhenband die Unterkante der Decke (1 mm darunter) liegt; gibt es darunter keines (z. B. Bodenplatte), das des Bauteils.</summary>
    string? GeschossVon(Pruefkoerper b, V3 mitte, double? unterkante = null)
    {
        var g = b.ElementId is { } id && m.Finde(id)?.Geschoss is { } eg ? eg : b.Geschoss;
        if (IstPlatte(b))
        {
            var uk = unterkante ?? b.Huelle.Min.Z;
            return NachHoehe(new V3(mitte.X, mitte.Y, uk - 1)) ?? g ?? NachHoehe(mitte);
        }
        if (g is null) return NachHoehe(mitte);
        if (m.Finde(g) is not { } gs || gs.Zahl("kote") is not double kt || gs.Zahl("hoehe") is not double h) return g;
        return InBand(mitte.Z, kt, h) ? g : NachHoehe(mitte) ?? g;
    }

    static bool InBand(double z, double kote, double hoehe) => z >= kote - 1 && z < kote + hoehe;

    string? NachHoehe(V3 mitte) =>
        m.DerKlasse("Geschoss").Where(x => x.Zahl("kote") is double kt && x.Zahl("hoehe") is double h && InBand(mitte.Z, kt, h))
            .OrderByDescending(x => x.Zahl("kote")).Select(x => x.Id).FirstOrDefault();

    bool IstPlatte(Pruefkoerper b)
    {
        if (b.ElementId is { } id && m.Finde(id) is { } e) return kat.IstArt(e, "Decke") || kat.IstArt(e, "Dach");
        var en = (b.Entitaet ?? "").ToUpperInvariant();
        return en.StartsWith("IFCSLAB", StringComparison.Ordinal) || en == "IFCROOF";
    }

    // ------------------------------------------------------------------ Bedarf

    List<Aussparungsbedarf> BedarfRechnen(List<Pruefkoerper> leitungen, List<Pruefkoerper> bereich, Pruefkoerpermenge menge)
    {
        var aus = new List<Aussparungsbedarf>();
        if (bereich.Count == 0) return aus;
        var idx = new Raumindex<Pruefkoerper>(bereich, b => b.Huelle);
        foreach (var l in leitungen)
            foreach (var b in idx.Kandidaten(l.Huelle).OrderBy(x => x.Kennung, StringComparer.Ordinal))
                foreach (var g in Zusammensetzen(Durchgaenge.Finden(l, b)))
                {
                    var wer = $"{l.Kennzeichen} durch {Name(b)}";
                    if (g.BeginntInnen || g.EndetInnen)
                    {
                        Liste("keine Aussparung: Leitung beginnt bzw. endet im Bauteil",
                              $"{wer}: die Leitung {(g.BeginntInnen ? "beginnt" : "endet")} im Bauteil – keine Aussparung vorgeschlagen (Schlitz bzw. Anschluss von Hand).");
                        continue;
                    }
                    if (g.Tiefe <= Tol) continue;
                    var normale = g.NormaleEin ?? (g.NormaleAus is V3 na ? -na : -g.Richtung);
                    if (Laengs(b, normale, g.Richtung) is { } laengs)
                    {
                        Liste("keine Aussparung: Leitung längs im Bauteil", $"{wer}: {laengs} – keine Aussparung (Schlitz bzw. Führung von Hand).");
                        continue;
                    }
                    int ts = Math.Clamp(g.Teilstueck, 0, l.Achsen.Count - 1);
                    var (ganz, teil) = InOeffnung(menge.OeffnungenVon(b), l.Huellkoerper(true)[ts], g);
                    if (ganz is not null)
                    {
                        nOeffnung++;
                        Liste("keine Aussparung: in einer Architektenöffnung",
                              $"{wer}: liegt in der Öffnung {ganz.GlobalId}{(ganz.Gefuellt ? $" (gefüllt: {ganz.Fuellung})" : "")} der Architektur – keine Aussparung.");
                        continue;
                    }
                    if (teil is not null) Liste("Öffnung zu klein: Aussparung vorgeschlagen", $"{wer}: Achse in der Öffnung {teil.GlobalId}, der Querschnitt passt aber nicht hinein – Aussparung vorgeschlagen.");
                    aus.Add(Bedarf(b, l, g));
                }
        return aus;
    }

    /// <summary>Bedarf aus einem Durchgang (eines Bauteils bzw. einer Kette): Projektion des Querschnitts mit Dämmung über die ganze
    /// Tiefe + 2 × Spiel (Regel im Dateikopf, Punkt 3).</summary>
    Aussparungsbedarf Bedarf(Pruefkoerper b, Pruefkoerper l, Durchgang g, IReadOnlyList<Pruefkoerper>? bauteile = null, IReadOnlyDictionary<string, double>? dicken = null)
    {
        int ts = Math.Clamp(g.Teilstueck, 0, l.Achsen.Count - 1);
        var q = l.Achsen[ts];
        var pr = Durchgaenge.Projektion(g, q, l.Daemmdicke);
        double spiel2 = 2 * v.Spiel;
        var x = new Aussparungsbedarf
        {
            Bauteil = b, Leitung = l, Durchgang = g, Mitte = g.Mitte, Achse = Aussparungsgeometrie.Kanonisch(pr.Normale), U = pr.AchseU, V = pr.AchseV,
            Tiefe = g.Dicke, Rund = q.Form == Querschnittsform.Rund,
            B = pr.DurchbruchBreite + spiel2, H = pr.DurchbruchHoehe + spiel2,   // Regel: immer das umschliessende Rechteck (B3b)
            D = pr.DurchbruchKreis + spiel2,                                     // nur für eine von Hand gewählte Kernbohrung
            QB = pr.DurchbruchBreite, QH = pr.DurchbruchHoehe, QD = pr.DurchbruchKreis,
        };
        return bauteile is null ? x : new Aussparungsbedarf
        {
            Bauteil = x.Bauteil, Bauteile = bauteile, Dicken = dicken!, Leitung = x.Leitung, Durchgang = x.Durchgang, Mitte = x.Mitte, Achse = x.Achse, U = x.U, V = x.V,
            Tiefe = x.Tiefe, Rund = x.Rund, B = x.B, H = x.H, D = x.D, QB = x.QB, QH = x.QH, QD = x.QD,
        };
    }

    // ------------------------------------------------------------------ Ketten (Nachtrag B3c)

    /// <summary>Lücke zwischen dem Ende eines Durchgangs a und dem Anfang des nächsten b derselben Leitung, senkrecht zu den Schichten
    /// (mm; negativ = Überlappung): im selben Teilstück entlang der Achse gemessen, sonst als Abstand der Punkte; mal |cos| zwischen
    /// Leitungsachse und Bauteilnormale von a.</summary>
    internal static double Luecke(Aussparungsbedarf a, Aussparungsbedarf b)
    {
        var d = a.Durchgang.Richtung;
        double axial = a.Durchgang.Teilstueck == b.Durchgang.Teilstueck
            ? (b.Durchgang.Eintritt - a.Durchgang.Austritt).Mal(d)
            : (b.Durchgang.Eintritt - a.Durchgang.Austritt).Laenge;
        return axial * Math.Max(Math.Abs(a.Achse.Mal(d)), 1e-6);
    }

    /// <summary>Liegen zwei Durchgänge derselben Leitung direkt hintereinander (Normalen parallel, Lücke ≤ Kettenluecke)?</summary>
    static bool Direkt(Aussparungsbedarf a, Aussparungsbedarf b) =>
        Math.Abs(a.Achse.Mal(b.Achse)) >= Math.Cos(Aussparungsgeometrie.EbenenWinkel * Math.PI / 180) && Luecke(a, b) <= Kettenluecke;

    /// <summary>Durchgänge je Leitung entlang ihrer Achse zu Ketten direkt hintereinander liegender Bauteile zusammensetzen (Regel im
    /// Dateikopf, Nachtrag B3c b). Ein Durchgang ohne Nachbarn bleibt, wie er ist; eine Kette wird ein Bedarf über alle Bauteile:
    /// Eintritt des ersten, Austritt des letzten, Masse aus diesem Durchgang, Haupt-Bauteil = das dickste. Dasselbe Bauteil zweimal
    /// (Leitung tritt wieder ein) beginnt eine neue Kette.</summary>
    internal List<Aussparungsbedarf> Ketten(List<Aussparungsbedarf> einzeln)
    {
        var aus = new List<Aussparungsbedarf>();
        foreach (var jeLeitung in einzeln.GroupBy(b => b.LeitungId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var folge = jeLeitung.OrderBy(b => b.Durchgang.Teilstueck).ThenBy(b => b.Durchgang.T0).ThenBy(b => b.Bauteil.Kennung, StringComparer.Ordinal).ToList();
            var kette = new List<Aussparungsbedarf>();
            Aussparungsbedarf? ende = null;   // Glied mit dem am weitesten reichenden Austritt
            void Abschliessen()
            {
                if (kette.Count == 1) aus.Add(kette[0]);
                else if (kette.Count > 1) aus.Add(Kettenbedarf(kette, ende!));
                kette.Clear(); ende = null;
            }
            foreach (var b in folge)
            {
                bool dazu = ende is not null && !kette.Any(x => x.Bauteil == b.Bauteil) && Direkt(ende, b);
                if (!dazu) Abschliessen();
                kette.Add(b);
                if (ende is null || Weiter(b, ende)) ende = b;
            }
            Abschliessen();
        }
        return aus;
    }

    /// <summary>Reicht der Durchgang b weiter entlang der Leitung als a?</summary>
    static bool Weiter(Aussparungsbedarf b, Aussparungsbedarf a) =>
        b.Durchgang.Teilstueck > a.Durchgang.Teilstueck || (b.Durchgang.Teilstueck == a.Durchgang.Teilstueck && b.Durchgang.T1 > a.Durchgang.T1);

    /// <summary>Bedarf einer Kette: zusammengesetzter Durchgang (Eintritt des ersten, Austritt des am weitesten reichenden Glieds),
    /// Bauteile in der Folge entlang der Aussparungsachse, Haupt-Bauteil = grösste Dicke (bei gleicher Dicke der kleinere Schlüssel).</summary>
    Aussparungsbedarf Kettenbedarf(List<Aussparungsbedarf> kette, Aussparungsbedarf ende)
    {
        var a = kette[0];
        var ein = a.Durchgang; var aus = ende.Durchgang;
        var r = aus.Austritt - ein.Eintritt;
        double l = r.Laenge;
        var dir = ein.Teilstueck == aus.Teilstueck || l < 1e-9 ? ein.Richtung : r * (1 / l);
        V3? n = ein.NormaleEin ?? (aus.NormaleAus is V3 na ? -na : null);
        double cos = n is V3 nn ? Math.Abs(nn.Mal(dir)) : 1;
        var g = new Durchgang(ein.Teilstueck, ein.T0, aus.T1, ein.Eintritt, aus.Austritt, ein.NormaleEin, aus.NormaleAus, false, false,
            l, l * cos, Math.Asin(Math.Clamp(cos, 0, 1)) * 180 / Math.PI, (ein.Eintritt + aus.Austritt) * 0.5, dir);
        var dicken = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var x in kette) dicken[x.Bauteil.Kennung] = Math.Max(dicken.GetValueOrDefault(x.Bauteil.Kennung), x.Tiefe);
        var achse = a.Achse;
        var bauteile = kette.GroupBy(x => x.Bauteil).Select(x => (B: x.Key, W: x.Average(y => (y.Mitte - g.Mitte).Mal(achse))))
            .OrderBy(x => x.W).ThenBy(x => x.B.Kennung, StringComparer.Ordinal).Select(x => x.B).ToList();
        var haupt = bauteile.OrderByDescending(b => dicken[b.Kennung]).ThenBy(b => b.Kennung, StringComparer.Ordinal).First();
        return Bedarf(haupt, a.Leitung, g, bauteile, dicken);
    }

    /// <summary>Bauteile ausserhalb des Bereichs, die eine Leitung direkt vor oder hinter einem Durchgang quert (mögliche Ketten-
    /// Nachbarn, grosszügig: Ein- bzw. Austritt bis 10 × Kettenluecke neben dem Ende des Durchgangs) – damit eine Auswahl die Kette
    /// ganz rechnet.</summary>
    static List<Pruefkoerper> KettenNachbarn(List<Aussparungsbedarf> bedarf, HashSet<Pruefkoerper> bereich, Raumindex<Pruefkoerper> idx)
    {
        const double Nah = 10 * Kettenluecke;
        var neu = new HashSet<Pruefkoerper>();
        foreach (var b in bedarf)
            foreach (var p in new[] { b.Durchgang.Eintritt, b.Durchgang.Austritt })
                foreach (var x in idx.Kandidaten(new Huelle3(p, p), Nah + Tol))
                {
                    if (bereich.Contains(x) || neu.Contains(x)) continue;
                    if (Durchgaenge.Finden(b.Leitung, x).Any(g => (g.Eintritt - p).Laenge <= Nah + Tol || (g.Austritt - p).Laenge <= Nah + Tol
                                                                   || Geometrie3.AbstandPunktSegment(p, g.Eintritt, g.Austritt) <= Tol))
                        neu.Add(x);
                }
        return neu.OrderBy(x => x.Kennung, StringComparer.Ordinal).ToList();
    }

    /// <summary>Liegt die Leitung längs im Bauteil statt es zu durchqueren? Wände (Klasse Wand bzw. IfcWall…) werden nur über eine
    /// steile Fläche gequert (|n_z| &lt; 0.5, dieselbe Einteilung wie Durchgaenge.Ebenenachsen) und – bei bekannter Wandachse – nicht
    /// über die Stirnseite (|n·Achse| ≤ 0.5); Decken und Dächer (Decke, Dach, IfcSlab…, IfcRoof) nur über eine flache Fläche. Stützen
    /// und Träger ohne Einschränkung. Bei allen: die Leitung quert nicht flacher als 30° zur Fläche (|n·Richtung| ≥ 0.5, dieselbe
    /// Einteilung). Sonst Text mit dem Grund (Leitung in der Wand hochgeführt, in der Decke verlegt, flach durch eine dünne Schicht –
    /// Befund Bernstrasse 42: Tiefen bis 3.6 m, eine Sammelaussparung 3.4 × 3.6 m in einer 3 mm dicken Platte), sonst null.
    /// ANNAHME (Prüfliste): solche Stellen brauchen einen Schlitz bzw. eine Aussparung von Hand.</summary>
    string? Laengs(Pruefkoerper b, V3 n, V3 richtung)
    {
        string art;
        V3? achse = null;
        if (b.ElementId is { } id && m.Finde(id) is { } e)
        {
            art = kat.IstArt(e, "Wand") ? "wand" : kat.IstArt(e, "Decke") || kat.IstArt(e, "Dach") ? "decke" : "stab";
            if (art == "wand" && e.Geometrie?.Punkte is { Count: >= 2 } pk && pk[0].Length >= 2 && pk[^1].Length >= 2)
            {
                var d = new V3(pk[^1][0] - pk[0][0], pk[^1][1] - pk[0][1], 0);
                if (d.Laenge > 1e-6) achse = d.Einheit();
            }
        }
        else
        {
            var en = (b.Entitaet ?? "").ToUpperInvariant();
            art = en.StartsWith("IFCWALL", StringComparison.Ordinal) ? "wand" : en.StartsWith("IFCSLAB", StringComparison.Ordinal) || en == "IFCROOF" ? "decke" : "stab";
        }
        n = n.Einheit();
        bool steil = Math.Abs(n.Z) < 0.5;
        if (art == "wand" && !steil) return "die Leitung liegt längs in der Wand (Ein- bzw. Austritt über Ober- oder Unterseite)";
        if (art == "wand" && achse is V3 a && Math.Abs(n.Mal(a)) > 0.5) return "die Leitung liegt längs in der Wand (Ein- bzw. Austritt über die Stirnseite)";
        if (art == "decke" && steil) return "die Leitung liegt längs in der Decke (Ein- bzw. Austritt über die Stirnseite)";
        if (Math.Abs(n.Mal(richtung.Einheit())) < 0.5) return "die Leitung quert flacher als 30° zur Bauteilfläche (längs)";
        return null;
    }

    /// <summary>Teilstücke, die an einem Knick im Bauteil aneinanderstossen (das erste endet innen, das nächste beginnt dort), zu
    /// einem Durchgang zusammensetzen: Ein- und Austritt der beiden, Richtung und Tiefe der Verbindung.</summary>
    internal static List<Durchgang> Zusammensetzen(List<Durchgang> roh)
    {
        var aus = new List<Durchgang>();
        foreach (var d in roh.OrderBy(x => x.Teilstueck).ThenBy(x => x.T0))
        {
            if (aus.Count > 0 && aus[^1] is { EndetInnen: true } a && d.BeginntInnen && d.Teilstueck == a.Teilstueck + 1 && (d.Eintritt - a.Austritt).Laenge <= 1)
            {
                var r = d.Austritt - a.Eintritt;
                double l = r.Laenge;
                var dir = l > 1e-9 ? r * (1 / l) : a.Richtung;
                V3? n = a.NormaleEin ?? (d.NormaleAus is V3 na ? -na : null);
                double cos = n is V3 nn ? Math.Abs(nn.Mal(dir)) : 1;
                aus[^1] = new Durchgang(a.Teilstueck, a.T0, d.T1, a.Eintritt, d.Austritt, a.NormaleEin, d.NormaleAus, a.BeginntInnen, d.EndetInnen,
                    l, l * cos, Math.Asin(Math.Clamp(cos, 0, 1)) * 180 / Math.PI, (a.Eintritt + d.Austritt) * 0.5, dir);
                continue;
            }
            aus.Add(d);
        }
        return aus;
    }

    /// <summary>Liegt der Querschnitt (mit Dämmung) auf der Länge des Durchgangs ganz in einer Öffnung (Ganz)? Sonst die erste
    /// Öffnung, in der wenigstens die Mitte der Achse liegt (Teil).</summary>
    static (Pruefoeffnung? Ganz, Pruefoeffnung? Teil) InOeffnung(IReadOnlyList<Pruefoeffnung> oeffnungen, Achskoerper q, Durchgang g)
    {
        if (oeffnungen.Count == 0) return (null, null);
        var punkte = Querschnittspunkte(q, g);
        Pruefoeffnung? teil = null;
        foreach (var o in oeffnungen)
        {
            if (!o.Huelle.Enthaelt(g.Mitte, Tol) || !o.Enthaelt(g.Mitte)) continue;
            if (punkte.All(x => o.Huelle.Enthaelt(x, Tol) && o.Enthaelt(x))) return (o, null);
            teil ??= o;
        }
        return (null, teil);
    }

    /// <summary>Randpunkte des Querschnitts (um die Toleranz nach innen) und Achspunkt an drei Stellen des Durchgangs (1/6, 1/2, 5/6).</summary>
    static List<V3> Querschnittspunkte(Achskoerper q, Durchgang g)
    {
        var aus = new List<V3>();
        foreach (var s in new[] { 1 / 6.0, 0.5, 5 / 6.0 })
        {
            var c = g.Eintritt + (g.Austritt - g.Eintritt) * s;
            aus.Add(c);
            if (q.Form == Querschnittsform.Rund)
            {
                double r = Math.Max(q.Radius - Tol, 0);
                for (int i = 0; i < 8; i++)
                {
                    double w = i * Math.PI / 4;
                    aus.Add(c + q.RichtungB * (r * Math.Cos(w)) + q.RichtungH * (r * Math.Sin(w)));
                }
            }
            else
            {
                double hb = Math.Max(q.Breite / 2 - Tol, 0), hh = Math.Max(q.Hoehe / 2 - Tol, 0);
                foreach (var (x, y) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (0, -1), (1, 0), (0, 1), (-1, 0) })
                    aus.Add(c + q.RichtungB * (x * hb) + q.RichtungH * (y * hh));
            }
        }
        return aus;
    }

    /// <summary>Liegt der Querschnitt eines Bedarfs (mit Dämmung, ohne Spiel) ganz in der Aussparung (gleiche Ebene)?</summary>
    internal static bool Deckt(Aussparungslage a, Aussparungsbedarf b)
    {
        if (!Aussparungsgeometrie.GleicheEbene(a.Mitte, a.Achse, a.Tiefe, b.Mitte, b.Achse, b.Tiefe)) return false;
        var d = b.Mitte - a.Mitte;
        double cu = Math.Abs(d.Mal(a.U)), cv = Math.Abs(d.Mal(a.V));
        if (a.Rund)
        {
            double r = a.D / 2 + Tol;
            return (b.Rund && Math.Sqrt(cu * cu + cv * cv) + b.QD / 2 <= r)
                   || Math.Sqrt(Math.Pow(cu + b.QB / 2, 2) + Math.Pow(cv + b.QH / 2, 2)) <= r;
        }
        bool rechteck = cu + b.QB / 2 <= a.B / 2 + Tol && cv + b.QH / 2 <= a.H / 2 + Tol;
        bool kreis = b.Rund && cu + b.QD / 2 <= a.B / 2 + Tol && cv + b.QD / 2 <= a.H / 2 + Tol;
        return rechteck || kreis;
    }

    // ------------------------------------------------------------------ Zusammenfassen

    /// <summary>Gruppen je Bauteil und Ebene; zusammengefasst wird bei Randabstand &lt; abstand bzw. Berührung/Überschneidung.</summary>
    internal static List<Aussparungsgruppe> Zusammenfassen(List<Aussparungsbedarf> bedarf, double abstand)
    {
        var aus = new List<Aussparungsgruppe>();
        foreach (var jeBauteil in bedarf.GroupBy(b => b.Schluessel).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var cluster = jeBauteil.OrderBy(b => b.Leitung.Kennzeichen, StringComparer.Ordinal).ThenBy(b => b.Mitte.X).ThenBy(b => b.Mitte.Y).ThenBy(b => b.Mitte.Z)
                .Select(b => new List<Aussparungsbedarf> { b }).ToList();
            for (bool weiter = true; weiter;)
            {
                weiter = false;
                for (int i = 0; i < cluster.Count && !weiter; i++)
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        var a = cluster[i][0]; var b = cluster[j][0];
                        if (!Aussparungsgeometrie.GleicheEbene(a.Mitte, a.Achse, cluster[i].Max(x => x.Tiefe), b.Mitte, b.Achse, cluster[j].Max(x => x.Tiefe))) continue;
                        double d = Aussparungsgeometrie.Randabstand(Umriss(cluster[i], a), Umriss(cluster[j], a));
                        if (d < abstand || d <= Tol)
                        {
                            cluster[i].AddRange(cluster[j]);
                            cluster.RemoveAt(j);
                            weiter = true;
                            break;
                        }
                    }
            }
            aus.AddRange(cluster.Select(Gruppe));
        }
        return aus;
    }

    /// <summary>Umriss in der Ebene des Bezugs r (u, v ab r.Mitte): das umschliessende Rechteck der Teile (die Vorschläge sind
    /// rechteckig, B3b).</summary>
    static Aussparungsgeometrie.Umrissform Umriss(List<Aussparungsbedarf> teile, Aussparungsbedarf r)
    {
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var t in teile)
        {
            var d = t.Mitte - r.Mitte;
            double cu = d.Mal(r.U), cv = d.Mal(r.V);
            u0 = Math.Min(u0, cu - t.B / 2); u1 = Math.Max(u1, cu + t.B / 2);
            v0 = Math.Min(v0, cv - t.H / 2); v1 = Math.Max(v1, cv + t.H / 2);
        }
        return new Aussparungsgeometrie.Umrissform(false, u0, u1, v0, v1);
    }

    /// <summary>Gruppe einer von Hand gewählten Kernbohrung: ein Bedarf, rund, d = umschliessender Kreis + 2 × Spiel.</summary>
    static Aussparungsgruppe Rundgruppe(Aussparungsbedarf r) =>
        new() { Teile = [r], Mitte = r.Mitte, Achse = r.Achse, U = r.U, V = r.V, Tiefe = r.Tiefe, Rund = true, B = r.D, H = r.D };

    static Aussparungsgruppe Gruppe(List<Aussparungsbedarf> teile)
    {
        var r = teile[0];
        if (teile.Count == 1)
            return new Aussparungsgruppe { Teile = teile, Mitte = r.Mitte, Achse = r.Achse, U = r.U, V = r.V, Tiefe = r.Tiefe, Rund = false, B = r.B, H = r.H };
        var f = Umriss(teile, r);
        double w = teile.Average(t => (t.Mitte - r.Mitte).Mal(r.Achse));
        var mitte = r.Mitte + r.U * f.Cu + r.V * f.Cv + r.Achse * w;
        return new Aussparungsgruppe { Teile = teile, Mitte = mitte, Achse = r.Achse, U = r.U, V = r.V, Tiefe = teile.Max(t => t.Tiefe), Rund = false, B = f.U1 - f.U0, H = f.V1 - f.V0 };
    }

    // ------------------------------------------------------------------ Wiedererkennen

    /// <summary>Vorschläge (Status vorgeschlagen) den Gruppen zuordnen: gleiches Bauteil, gemeinsame Leitung; zuerst die meisten
    /// gemeinsamen Leitungen, dann der kleinste Abstand der Mitten (Regel im Dateikopf).</summary>
    Dictionary<Element, Aussparungsgruppe> Wiedererkennen(List<Element> vorschlaege, List<Aussparungsgruppe> gruppen, Dictionary<string, Pruefkoerper> nachSchluessel)
    {
        // gemeinsames Bauteil (durchgehende Aussparungen: eines der Bauteile) und gemeinsame Leitung; zuerst die meisten Leitungen,
        // dann die meisten Bauteile, dann die Nähe (Nachtrag B3c)
        var paare = new List<(Element E, Aussparungsgruppe G, int Gemeinsam, int Bauteile, double Abstand, int Nr)>();
        var schluessel = vorschlaege.ToDictionary(e => e, e => SchluesselAlle(e, nachSchluessel));
        for (int gi = 0; gi < gruppen.Count; gi++)
        {
            var g = gruppen[gi];
            var lg = g.LeitungIds.ToHashSet(StringComparer.Ordinal);
            var bg = g.Bauteile.Select(b => b.Kennung).ToHashSet(StringComparer.Ordinal);
            foreach (var e in vorschlaege)
            {
                int cb = schluessel[e].Count(bg.Contains);
                if (cb == 0) continue;
                int c = Aussparungsbezug.Leitungen(e).Count(lg.Contains);
                if (c == 0) continue;
                double d = e.Geometrie?.Punkte is { Count: > 0 } pk && pk[0].Length >= 3 ? (V3.Aus(pk[0]) - g.Mitte).Laenge : double.MaxValue;
                paare.Add((e, g, c, cb, d, gi));
            }
        }
        var aus = new Dictionary<Element, Aussparungsgruppe>();
        var vergeben = new HashSet<Aussparungsgruppe>();
        foreach (var x in paare.OrderByDescending(x => x.Gemeinsam).ThenByDescending(x => x.Bauteile).ThenBy(x => x.Abstand)
                     .ThenBy(x => x.E.Kennzeichen, StringComparer.Ordinal).ThenBy(x => x.Nr))
        {
            if (aus.ContainsKey(x.E) || vergeben.Contains(x.G)) continue;
            aus[x.E] = x.G;
            vergeben.Add(x.G);
        }
        return aus;
    }

    // ------------------------------------------------------------------ Schreiben

    /// <summary>Element einer Gruppe: neu (alt = null, Status vorgeschlagen) bzw. nachgeführt (Kennzeichen, Nummer, Status bleiben;
    /// unveränderte Werte behalten ihre Herkunft).</summary>
    Element Schreiben(Element? alt, Aussparungsgruppe g)
    {
        var e = alt?.Kopie() ?? new Element
        {
            Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(kat.Klasse("Aussparung").Kuerzel), Klasse = "Aussparung", Angelegt = k.Herkunft,
        };
        double b = AufCm(g.B), h = AufCm(g.H), t = Auf(g.Tiefe);
        // durchgehend über mehrere Bauteile (Nachtrag B3c): alle nennen, Haupt-Bauteil zuerst
        var bt = g.Kette ? $"{Namen(g.BauteileFuerBezug)} (durchgehend, direkt hintereinander)" : Name(g.Bauteil);
        var masseQuelle = g.Sammel
            ? $"umschliessendes Rechteck von {g.Teile.Count} Aussparungen in {bt} (Randabstand < {Z(v.Zusammenfassen)} mm, {v.ZusammenfassenQuelle}); je Querschnitt mit Dämmung projiziert + 2 × Spiel {Z(v.Spiel)} mm ({v.SpielQuelle})"
            : $"{(g.Rund ? "umschliessender Kreis" : "umschliessendes Rechteck")} der Projektion des Querschnitts von {g.Teile[0].Leitung.Kennzeichen} " +
              $"({g.Teile[0].Leitung.Massquelle}) auf {bt} über die ganze Tiefe + 2 × Spiel {Z(v.Spiel)} mm ({v.SpielQuelle})";
        var hM = k.Herkunft with { Art = "berechnung", Quelle = masseQuelle };
        var hT = k.Herkunft with
        {
            Art = "berechnung",
            Quelle = g.Kette ? $"Dicke über {g.Bauteile.Count} Bauteile direkt hintereinander (Lücke ≤ {Z(Kettenluecke)} mm, Nachtrag B3c): {Namen(g.Bauteile)} am Durchgang"
                : $"Bauteildicke von {bt} am Durchgang",
        };
        var hL = k.Herkunft with { Art = "berechnung", Quelle = $"Lage aus dem Durchgang durch {bt}" };
        var hF = k.Herkunft with { Art = "berechnung", Quelle = g.Rund ? "folgt der von Hand gewählten Kernbohrung (aussparung_form)" : FormRegelQuelle };

        if (!g.Rund && alt is not null && AussparungForm.KernbohrungVonHand(alt))   // nur bei mehreren Leitungen (von Hand ergänzt) – sonst vorbehalten
            Liste("Kernbohrung von Hand aufgehoben (mehrere Leitungen)", $"{alt.Kennzeichen}{Nr(alt)}: Kernbohrung von Hand aufgehoben – die Aussparung hat mehrere Leitungen " +
                  $"({string.Join(", ", Aussparungsbezug.Leitungen(alt).Select(Kz).Order(StringComparer.Ordinal))}); rechteckig nach der Regel.");
        e.Geschoss = GeschossVon(g.Bauteil, g.Mitte, g.Mitte.Z - g.Tiefe / 2);   // Decke: Unterkante am Durchgang (Achse senkrecht)
        double[] punkt = [R1(g.Mitte.X), R1(g.Mitte.Y), R1(g.Mitte.Z)];
        if (e.Geometrie is not { Art: "punkt", Punkte.Count: 1 } geo || geo.Punkte[0].Length < 3 || !geo.Punkte[0].Zip(punkt).All(x => Math.Abs(x.First - x.Second) < 1e-9))
            e.Geometrie = new Geometrie { Art = "punkt", Punkte = [punkt] };
        Text(e, "form", g.Rund ? AussparungForm.Rund : AussparungForm.Rechteckig, hF);   // unverändert → Herkunft (Wahl von Hand) bleibt
        if (g.Rund) { Zahl(e, "d", b, "mm", hM); Weg(e, "b"); Weg(e, "h"); }
        else { Zahl(e, "b", b, "mm", hM); Zahl(e, "h", h, "mm", hM); Weg(e, "d"); }
        Zahl(e, "tiefe", t, "mm", hT);
        Achse(e, [R6(g.Achse.X), R6(g.Achse.Y), R6(g.Achse.Z)], hL);
        Text(e, "art", g.Rund ? "kernbohrung" : "aussparung", hF);
        if (g.Rund) { Zahl(e, "kote_uk", R1(g.Mitte.Z), "mm", hL); Weg(e, "kote_ok"); }
        else
        {
            var (uk, ok) = new Aussparungslage(g.Mitte, g.Achse, g.U, g.V, false, b, h, t).Koten();
            Zahl(e, "kote_uk", R1(uk), "mm", hL); Zahl(e, "kote_ok", R1(ok), "mm", hL);
        }
        if (alt is null) e.Eigenschaften["status"] = new Wert { Inhalt = AussparungStatus.Vorgeschlagen, Herkunft = k.Herkunft with { Quelle = "aussparungen_vorschlagen" } };
        Aussparungsbezug.BauteileSetzen(e, g.BauteileFuerBezug);
        Aussparungsbezug.LeitungenSetzen(e, g.LeitungIds.OrderBy(Kz, StringComparer.Ordinal));
        // Material der Bauteile (Nachtrag B3c): erkannt nach der Wortliste, unbekannt → Kennzeichen materialUnbekannt; Nachtrag B3d: Typenliste
        // (Festlegung mit Quelle) bzw. aus dem Typnamen (Entscheid Christian) – je Bauteil die Fundstelle mit Entscheid bzw. Annahme
        var befunde = g.Bauteile.Select(x => (B: x, M: Material(x))).ToList();
        var hMat = k.Herkunft with
        {
            Art = "berechnung",
            Quelle = $"Material von {string.Join("; ", befunde.Select(x => $"{Name(x.B)}: {x.M.Quelle}"))}",
        };
        Text(e, "material", string.Join(" / ", befunde.Select(x => x.M.Text).Distinct()), hMat);
        Bool(e, "materialUnbekannt", befunde.Any(x => x.M.Unbekannt), hMat);
        return e;
    }

    /// <summary>Quelle der Form rechteckig und der Art aussparung nach der Regel.</summary>
    internal const string FormRegelQuelle = "Regel: Vorschlag rechteckig (umschliessendes Rechteck der Projektion), Kernbohrungen nur von Hand mit aussparung_form (" +
                                            AussparungForm.Entscheid + ")";

    static void Zahl(Element e, string name, double wert, string einheit, Herkunft h)
    {
        if (e.Zahl(name) is double alt && Math.Abs(alt - wert) < 1e-9) return;
        e.Eigenschaften[name] = new Wert { Inhalt = JsonValue.Create(wert), Einheit = einheit, Herkunft = h };
    }

    static void Text(Element e, string name, string wert, Herkunft h)
    {
        if (e.Text(name) == wert) return;
        e.Eigenschaften[name] = new Wert { Inhalt = JsonValue.Create(wert), Herkunft = h };
    }

    static void Achse(Element e, double[] wert, Herkunft h)
    {
        if (e.Eigenschaft("achse")?.Inhalt is JsonArray a && a.Count == 3 && a.Select(Json.Zahl).Zip(wert).All(x => x.First is double d && Math.Abs(d - x.Second) < 1e-9)) return;
        e.Eigenschaften["achse"] = new Wert { Inhalt = new JsonArray(wert.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()), Herkunft = h };
    }

    static void Bool(Element e, string name, bool wert, Herkunft h)
    {
        if (e.Eigenschaft(name)?.Inhalt is JsonValue jv && jv.TryGetValue<bool>(out var alt) && alt == wert) return;
        e.Eigenschaften[name] = new Wert { Inhalt = JsonValue.Create(wert), Herkunft = h };
    }

    static void Weg(Element e, string name) => e.Eigenschaften.Remove(name);
}

/// <summary>Lesen der Listenparameter (Liste oder Text mit Kommas).</summary>
static class AussparungParam
{
    public static List<string> Liste(JsonObject p, string name) => p[name] switch
    {
        null => [],
        JsonArray a => a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : x?.ToJsonString() ?? "").Where(s => s.Length > 0).ToList(),
        JsonValue v when v.TryGetValue<string>(out var s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        _ => throw new HlksFehler($"Parameter «{name}» muss eine Liste sein.", "Form: [\"W-1\", \"W-2\"] (Kennzeichen)"),
    };
}
