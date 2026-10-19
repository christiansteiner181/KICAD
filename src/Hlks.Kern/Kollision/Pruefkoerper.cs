// Kollisionsprüfung (Modul 3b, B1): Prüfkörper je Element (Grundsatzentscheid 1, entwurf/modul-3b.md).
//
//   Leitungen (Rohr, Kanal; Geraden und Polylinien des Altbestands je Teilstück): Körper um die Achse – rund als Kapsel
//     (Radius = Aussendurchmesser/2 der Serie, Rundkanal d/2), rechteckig als Quader b × h um die Achse; dazu eine zweite
//     Hülle mit daemmdicke (Achskoerper.Aufgeblasen). Masse wie die Darstellung (HlksMasse.Von, sonst DN-gerecht mit
//     HlksKoerper.MinDurchmesser). Lage des Kanalquerschnitts wie HlksKoerper: b quer und waagrecht, h senkrecht zur Achse in
//     der lotrechten Ebene; senkrechte Teilstücke b in x, h in y.
//   Übernommene IFC-Bauteile, die in KICAD unverändert sind: das exakte Netz aus der Netzdatei ihrer Unterlage (über die
//     GlobalId). «Unverändert» ist die eine Regel aus dem Import (IfcUebernehmen.UnveraendertUebernommen): letzte Änderung
//     durch ifc_uebernehmen bzw. Art «ifc».
//     Ist ein Produkt auf mehrere Elemente verteilt (Wand mit geknickter Achse in Stücke geteilt, Kennung «GlobalId:2» …),
//     trägt das erste Stück das Netz des ganzen Produkts (WeitereElemente nennt die übrigen), solange alle Stücke unverändert
//     sind; sonst haben alle Stücke ihren KICAD-Körper. Fehlt ein Produkt in der Netzdatei, gilt ebenfalls der KICAD-Körper.
//   Alle übrigen grafischen Elemente (Formteile, Armaturen, Geräte, Familienelemente, Wände, Decken, Stützen, Träger, Dächer,
//     Türen, Fenster …): Dreiecksnetz aus ihrem Körper (Grafik.Koerper; Teile quader, prisma, zylinder, kugel, koerper).
//   Hintergrund-Produkte der IFC-Unterlagen (nicht übernommen: Treppen, Geländer, Fremd-IFC …): eigene Prüfkörper aus der
//     Netzdatei mit Unterlage, GlobalId und Entität.
//   Teile (Netzdatei Fassung 2, Rolle teil mit der GlobalId des Ganzen; Entscheid Hauptlauf 19.10.2026 nach Befund B1
//     Bernstrasse 42): hat das Ganze in der Netzdatei ein eigenes Netz, gelten seine Teile nicht als eigene Prüfkörper; hat es
//     keins (steht es nicht mit Netz darin), bilden die Teile zusammen den Prüfkörper des Ganzen unter dessen GlobalId – als
//     exaktes Netz des Elements, das diese GlobalId trägt, sonst als Hintergrund. (Der Erzeuger schreibt Ganze ohne eigenen
//     Körper schon mit dem Netz ihrer Teile; die Regel deckt Dateien ab, in denen das Ganze fehlt.) Selbst übernommene Teile
//     (Platten eines Dachs, je Platte eine Decke) behalten ihr eigenes exaktes Netz.
//     Teile ohne Bezug (Netzdateien Fassung 1 aus Modul 1b; in Fassung 2 IfcBuildingElementPart, für die die IFC-Datei keine
//     Zerlegung nennt – Befund Bernstrasse 42: der Revit-Export der verknüpften Architektur führt 3676 Teile ohne
//     IfcRelAggregates neben ihren Wänden): wie in B1 entfallen die, die ganz im Netz eines Bauteils derselben Unterlage liegen
//     (Lage-Heuristik, Option DoppelteTeileWeglassen); bei Fassung 1 mit Hinweis zum Erneuern.
//   Öffnungen (Netzdatei Fassung 2, IfcOpeningElement mit Wirt und Füllung): keine Prüfkörper, sondern je Wirt abrufbar
//     (Pruefkoerpermenge.OeffnungenVon) – wer prüft, zieht sie logisch vom Netz des Wirts ab (B2: Leitung in einer Öffnung ist
//     keine Kollision; B3: Aussparung angekommen). Ist der Wirt ein Teil, gilt die Öffnung für sein Ganzes. Nennt die IFC-Datei
//     keinen Wirt, wird er aus der Lage bestimmt (ANNAHME, Prüfliste: Bauteile derselben Unterlage, deren Netz die Mitte der
//     Öffnung enthält oder beidseits umgibt – WirtInDerLage), gekennzeichnet mit Pruefoeffnung.WirtAusLage.
//
// ANNAHMEN (Prüfliste):
//   - Kapsel: an freien Enden ragt die Halbkugel um r über den Endpunkt hinaus (Kontakt.AmEnde zeigt, wenn ein Befund nur
//     dort liegt). An Knicken schliessen die Halbkugeln wie die Kugeln der Darstellung.
//   - Rechteckkanal: an inneren Knicken wird jeder Quader um max(b, h)/2 über den Knick verlängert, damit die Aussenecke
//     geschlossen ist (die Darstellung setzt dort einen achsparallelen Quader b × b × h). Geneigte Teilstücke liegen genau
//     um die Achse (die Darstellung nähert sie waagrecht an).
//   - Zylinder und Kugeln der Körper als umschriebenes 24-Eck (Kugel 24 × 12 Streifen): das Netz enthält den wahren Körper,
//     höchstens 0.86 % des Radius zu gross (die Darstellung zeichnet ein eingeschriebenes 48-Eck).
//   - Körperteile mit NichtSchneiden (Räume, Dämmung der Darstellung) zählen nicht.
//   - Geprüft werden Bauteile (Katalog-Basis Bauteil) und Netzelemente (Schicht system mit Rolle); Räume, Zonen,
//     Aussparungen, Kollisionen, Wärmebrücken, Achsen, Beschriftung und Unterlagen nicht.
//
// Nachtrag B3c (Entscheid Christian 19.10.2026):
//   - Bekleidungen (IfcCovering: Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen, Dachhaut) tragen das Kennzeichen Bekleidung:
//     Hintergrund-Produkte und übernommene Elemente nach der Entität der Netzdatei, KICAD-Elemente nach der IFC-Entität ihrer
//     Katalogklasse (heute hat keine Klasse IfcCovering – die Regel steht trotzdem an einer Stelle: IstBekleidung). Der Bereich
//     bleibt architektur; die Kollisionsprüfung prüft sie nur im Paar hlks-bekleidung, Aussparungen bekommen sie nie.
//   - Material und Typname aus der Netzdatei Fassung 3 (Materialien, Typname, Materialquelle, NetzFassung) – für Hintergrund-Produkte
//     und Elemente, die aus einer IFC-Unterlage übernommen sind (auch mit KICAD-Körper). Die Einteilung macht AussparungMaterial.cs.

using System.Globalization;

namespace Hlks.Kern;

/// <summary>Querschnitt eines Achskörpers.</summary>
public enum Querschnittsform { Rund, Rechteck }

/// <summary>Körper um eine gerade Achse A–B: Kapsel (rund, Radius) bzw. Quader (rechteckig, Breite × Höhe quer zur Achse).</summary>
public sealed class Achskoerper
{
    public V3 A { get; }
    public V3 B { get; }
    public Querschnittsform Form { get; }
    /// <summary>Rund: Radius (mm).</summary>
    public double Radius { get; }
    /// <summary>Rechteckig: Breite entlang <see cref="RichtungB"/>, Höhe entlang <see cref="RichtungH"/> (mm).</summary>
    public double Breite { get; }
    public double Hoehe { get; }
    /// <summary>Einheitsvektoren quer zur Achse (auch bei rund gesetzt, für Projektionen).</summary>
    public V3 RichtungB { get; }
    public V3 RichtungH { get; }
    /// <summary>Einheitsvektor A → B.</summary>
    public V3 Richtung { get; }
    public double Laenge { get; }
    public Huelle3 Huelle { get; }

    Achskoerper(V3 a, V3 b, Querschnittsform form, double radius, double breite, double hoehe, V3 rb, V3 rh)
    {
        A = a; B = b; Form = form; Radius = radius; Breite = breite; Hoehe = hoehe; RichtungB = rb; RichtungH = rh;
        var d = b - a;
        Laenge = d.Laenge;
        Richtung = Laenge > 1e-12 ? d * (1 / Laenge) : new V3(1, 0, 0);
        if (form == Querschnittsform.Rund) Huelle = Huelle3.Aus(a, b).Erweitert(radius);
        else
        {
            var h = Huelle3.Leer;
            foreach (var s in new[] { -1.0, 1.0 })
                foreach (var t in new[] { -1.0, 1.0 })
                {
                    var q = rb * (s * breite / 2) + rh * (t * hoehe / 2);
                    h = h.Mit(a + q).Mit(b + q);
                }
            Huelle = h;
        }
    }

    /// <summary>Kapsel um die Strecke a–b mit Radius r.</summary>
    public static Achskoerper Kapsel(V3 a, V3 b, double radius)
    {
        var (rb, rh) = KanalLage(b - a);
        return new(a, b, Querschnittsform.Rund, radius, 2 * radius, 2 * radius, rb, rh);
    }

    /// <summary>Quader b × h um die Strecke a–b; Lage des Querschnitts wie HlksKoerper (siehe <see cref="KanalLage"/>).</summary>
    public static Achskoerper Quader(V3 a, V3 b, double breite, double hoehe)
    {
        var (rb, rh) = KanalLage(b - a);
        return new(a, b, Querschnittsform.Rechteck, Math.Sqrt(breite * breite + hoehe * hoehe) / 2, breite, hoehe, rb, rh);
    }

    /// <summary>Richtungen von Breite und Höhe eines Kanals wie HlksKoerper.Kanal: waagrechte und geneigte Teilstücke b quer und
    /// waagrecht (z × Achse), h = Achse × b (senkrecht zur Achse, nach oben); senkrechte Teilstücke (Grundrisslänge &lt; 0.001 mm)
    /// b in x, h in y.</summary>
    public static (V3 B, V3 H) KanalLage(V3 achse)
    {
        double lxy = Math.Sqrt(achse.X * achse.X + achse.Y * achse.Y);
        if (lxy < 1e-3) return (new V3(1, 0, 0), new V3(0, 1, 0));
        var d = achse.Einheit();
        var b = new V3(-d.Y, d.X, 0).Einheit();
        return (b, d.Kreuz(b));
    }

    /// <summary>Derselbe Körper, ringsum um <paramref name="zuschlag"/> grösser (Dämmung, Spiel): Radius + z, b + 2z, h + 2z.</summary>
    public Achskoerper Aufgeblasen(double zuschlag) => zuschlag == 0 ? this : Form == Querschnittsform.Rund
        ? new(A, B, Form, Radius + zuschlag, 2 * (Radius + zuschlag), 2 * (Radius + zuschlag), RichtungB, RichtungH)
        : new(A, B, Form, Math.Sqrt(Math.Pow(Breite + 2 * zuschlag, 2) + Math.Pow(Hoehe + 2 * zuschlag, 2)) / 2, Breite + 2 * zuschlag, Hoehe + 2 * zuschlag, RichtungB, RichtungH);

    /// <summary>Halbe Ausdehnung des Querschnitts in Richtung n (Einheitsvektor): rund r, rechteckig |n·b|·B/2 + |n·h|·H/2.</summary>
    public double Ausdehnung(V3 n) => Form == Querschnittsform.Rund ? Radius
        : Math.Abs(n.Mal(RichtungB)) * Breite / 2 + Math.Abs(n.Mal(RichtungH)) * Hoehe / 2;

    /// <summary>Mitte des Quaders.</summary>
    public V3 Mitte => (A + B) * 0.5;

    /// <summary>Liegt q im Körper, mindestens <paramref name="rand"/> von seiner Oberfläche entfernt?</summary>
    public bool Enthaelt(V3 q, double rand = 0)
    {
        if (Form == Querschnittsform.Rund) return Geometrie3.AbstandPunktSegment(q, A, B) < Radius - rand;
        var w = q - Mitte;
        return Math.Abs(w.Mal(Richtung)) < Laenge / 2 - rand && Math.Abs(w.Mal(RichtungB)) < Breite / 2 - rand && Math.Abs(w.Mal(RichtungH)) < Hoehe / 2 - rand;
    }

    /// <summary>Rechteckig: der Quader als geschlossenes Netz (12 Dreiecke).</summary>
    public Pruefnetz AlsNetz() => Pruefnetz.Quader(Mitte, Richtung, RichtungB, RichtungH, Laenge / 2, Breite / 2, Hoehe / 2);

    public override string ToString() => Form == Querschnittsform.Rund
        ? $"Kapsel r {Radius.ToString("0.###", CultureInfo.InvariantCulture)} mm, l {Laenge.ToString("0.#", CultureInfo.InvariantCulture)} mm"
        : $"Quader {Breite.ToString("0.###", CultureInfo.InvariantCulture)} × {Hoehe.ToString("0.###", CultureInfo.InvariantCulture)} mm, l {Laenge.ToString("0.#", CultureInfo.InvariantCulture)} mm";
}

/// <summary>Prüfkörper eines Elements bzw. eines Hintergrund-Produkts einer IFC-Unterlage.</summary>
public sealed class Pruefkoerper
{
    /// <summary>Stabile Kennung: Element-Id bzw. «Unterlage-Id:GlobalId» (Hintergrund).</summary>
    public required string Kennung { get; init; }
    public string? ElementId { get; init; }
    public string? Kennzeichen { get; init; }
    /// <summary>Katalogklasse (Elemente) – null bei Hintergrund-Produkten.</summary>
    public string? Klasse { get; init; }
    /// <summary>Unterlage, aus der das Netz stammt bzw. aus der das Element übernommen wurde.</summary>
    public string? UnterlageId { get; init; }
    public string? GlobalId { get; init; }
    /// <summary>IFC-Entität aus der Netzdatei (Hintergrund und exakte Netze), sonst null.</summary>
    public string? Entitaet { get; init; }
    /// <summary>Gewerk (Elemente: über das System, sonst Katalog – Ableitungen.Gewerk); Hintergrund null.</summary>
    public string? Gewerk { get; init; }
    /// <summary>hlks | architektur | sonst (Elemente nach Katalog, Hintergrund nach Entität).</summary>
    public required string Bereich { get; init; }
    /// <summary>achse (Kapsel/Quader um die Achse) | koerper (Netz aus Grafik.Koerper) | netzdatei (exaktes Netz eines
    /// übernommenen Elements) | hintergrund (Netz eines nicht übernommenen Produkts).</summary>
    public required string Quelle { get; init; }
    /// <summary>Herkunft der Masse (Leitungen: Serie und Grösse, Eigenschaften oder Darstellung DN-gerecht).</summary>
    public string? Massquelle { get; init; }
    /// <summary>Element-Id des Geschosses (Hintergrund: nach Kote zugeordnet, ±50 mm wie ifc_uebernehmen).</summary>
    public string? Geschoss { get; init; }
    /// <summary>Kote des Geschosses (mm über ±0.00).</summary>
    public double? GeschossKote { get; init; }
    /// <summary>Leitungen: Achse als Polylinie (Punkte ohne Doppel).</summary>
    public IReadOnlyList<V3> Achse { get; init; } = [];
    /// <summary>Leitungen: Körper je Teilstück, ohne Dämmung.</summary>
    public IReadOnlyList<Achskoerper> Achsen { get; init; } = [];
    /// <summary>Dämmdicke (mm) für die zweite Hülle; 0 = keine.</summary>
    public double Daemmdicke { get; init; }
    /// <summary>Alle anderen: Dreiecksnetz.</summary>
    public Pruefnetz? Netz { get; init; }
    /// <summary>Weitere Elemente desselben IFC-Produkts, die dieses exakte Netz mit abdeckt (Wand mit geknickter Achse, beim
    /// Übertrag in Stücke geteilt); sonst leer.</summary>
    public IReadOnlyList<string> WeitereElemente { get; init; } = [];
    /// <summary>Hüllquader aussen (bei Leitungen mit Dämmung).</summary>
    public Huelle3 Huelle { get; init; }
    /// <summary>Hüllquader ohne Dämmung.</summary>
    public Huelle3 HuelleOhneDaemmung { get; init; }
    /// <summary>Bekleidung (IfcCovering; Nachtrag B3c): nur im Paar hlks-bekleidung geprüft, nie eine Aussparung.</summary>
    public bool Bekleidung { get; init; }
    /// <summary>Materialnamen aus der Netzdatei (Fassung 3; IfcRelAssociatesMaterial), sonst leer.</summary>
    public IReadOnlyList<string> Materialien { get; init; } = [];
    /// <summary>Typname aus der Netzdatei (Fassung 3), sonst null.</summary>
    public string? Typname { get; init; }
    /// <summary>Fundstelle des Materials in der IFC-Datei (Fassung 3), sonst null.</summary>
    public string? Materialquelle { get; init; }
    /// <summary>Fassung der Netzdatei, aus der Material und Typname stammen (bzw. stammen könnten); null = Element ohne IFC-Herkunft.</summary>
    public int? NetzFassung { get; init; }

    public bool IstLeitung => Achsen.Count > 0;

    /// <summary>Körper um die Achse, mit oder ohne Dämmung.</summary>
    public IReadOnlyList<Achskoerper> Huellkoerper(bool mitDaemmung) =>
        mitDaemmung && Daemmdicke > 0 ? Achsen.Select(a => a.Aufgeblasen(Daemmdicke)).ToList() : Achsen;

    public override string ToString() => $"{Kennzeichen ?? GlobalId ?? Kennung} ({Klasse ?? Entitaet}, {Quelle})";
}

/// <summary>Auswahl für <see cref="Pruefkoerperbau.Erzeugen"/>.</summary>
public sealed class PruefkoerperOptionen
{
    /// <summary>Nur diese Elemente (Ids oder Kennzeichen); null = alle prüfbaren.</summary>
    public IReadOnlyCollection<string>? Elemente { get; init; }
    /// <summary>Hintergrund-Produkte der IFC-Unterlagen als eigene Prüfkörper.</summary>
    public bool Hintergrund { get; init; } = true;
    /// <summary>Hintergrund nur aus diesen Unterlagen (Ids oder Kennzeichen); null = alle IFC-Unterlagen mit Netzdatei.</summary>
    public IReadOnlyCollection<string>? Unterlagen { get; init; }
    /// <summary>Unverändert übernommene IFC-Bauteile mit dem exakten Netz der Netzdatei (sonst mit dem KICAD-Körper).</summary>
    public bool ExakteNetze { get; init; } = true;
    /// <summary>Teile ohne Bezug zu ihrem Ganzen – Netzdatei Fassung 1, oder Fassung 2, wenn die IFC-Datei keine Zerlegung nennt
    /// (Revit-Export einer verknüpften Architektur): Hintergrund-Teile (IfcBuildingElementPart), die ganz im Netz eines Bauteils
    /// derselben Unterlage liegen, weglassen (Heuristik aus B1). Teile mit Bezug (Fassung 2) folgen immer der Beziehung.</summary>
    public bool DoppelteTeileWeglassen { get; init; } = true;
    /// <summary>Öffnungen der Netzdateien (Fassung 2) laden und je Wirt bereitstellen (<see cref="Pruefkoerpermenge.OeffnungenVon"/>).</summary>
    public bool Oeffnungen { get; init; } = true;
}

/// <summary>Eine Öffnung (IfcOpeningElement) aus der Netzdatei einer IFC-Unterlage mit ihrem Wirt – kein Prüfkörper, sondern
/// logisch vom Netz des Wirts abzuziehen (die Bauteilnetze der Netzdatei sind nicht ausgeschnitten).</summary>
public sealed class Pruefoeffnung
{
    public required string UnterlageId { get; init; }
    /// <summary>GlobalId der Öffnung.</summary>
    public required string GlobalId { get; init; }
    /// <summary>IfcOpeningElement bzw. IfcOpeningStandardCase.</summary>
    public required string Entitaet { get; init; }
    /// <summary>GlobalId des Wirts, wie in der Netzdatei (Produkt der Datei, das die Öffnung durchbricht); null, wenn die IFC-Datei
    /// keinen Wirt nennt (IfcRelVoidsElement fehlt).</summary>
    public string? WirtGlobalId { get; init; }
    /// <summary>GlobalId des Wirts nach Auflösen der Teile (Wirt ein Teil → sein Ganzes); meist = WirtGlobalId; null ohne Wirt.</summary>
    public string? BauteilGlobalId { get; init; }
    /// <summary>Kennungen der Prüfkörper des Wirts (Element-Id bzw. «Unterlage-Id:GlobalId»; mehrere bei geteilten Wänden mit
    /// KICAD-Körpern); leer, wenn der Wirt keinen Prüfkörper hat (nicht ausgewählt, Element gelöscht …).</summary>
    public IReadOnlyList<string> WirtKennungen { get; init; } = [];
    /// <summary>Wirt aus der Lage bestimmt (die IFC-Datei nennt keinen): ANNAHME (Prüfliste) – Prüfkörper derselben Unterlage
    /// (nicht HLKS), deren Netz die Mitte der Öffnung enthält oder umgibt (<see cref="Pruefkoerperbau.WirtInDerLage"/>).</summary>
    public bool WirtAusLage { get; init; }
    /// <summary>GlobalIds der Füllungen (IfcRelFillsElement: Tür, Fenster).</summary>
    public IReadOnlyList<string> Fuellungen { get; init; } = [];
    /// <summary>Hat die Öffnung eine Füllung (Tür, Fenster)?</summary>
    public bool Gefuellt => Fuellungen.Count > 0;
    /// <summary>Erste Füllung oder null.</summary>
    public string? Fuellung => Fuellungen.Count > 0 ? Fuellungen[0] : null;
    public double? GeschossKote { get; init; }
    /// <summary>Netz der Öffnung (Projekt-mm; Extrusionen sind geschlossen, Lage/InnenAbschnitte gehen).</summary>
    public required Pruefnetz Netz { get; init; }
    public Huelle3 Huelle => Netz.Huelle;

    /// <summary>Liegt q in der Öffnung (innen oder auf ihrer Fläche)?</summary>
    public bool Enthaelt(V3 q) => Netz.Lage(q) != Punktlage.Aussen;

    public override string ToString() =>
        $"Öffnung {GlobalId} in {BauteilGlobalId ?? (WirtKennungen.Count > 0 ? string.Join(", ", WirtKennungen) + " (aus der Lage)" : "?")}{(Gefuellt ? $", gefüllt ({string.Join(", ", Fuellungen)})" : "")}";
}

/// <summary>Ergebnis: Prüfkörper und Hinweise (Elemente ohne Körper, fehlende Netzdateien …), dazu die Öffnungen der
/// IFC-Unterlagen je Wirt.</summary>
public sealed record Pruefkoerpermenge(IReadOnlyList<Pruefkoerper> Koerper, IReadOnlyList<string> Hinweise)
{
    readonly IReadOnlyList<Pruefoeffnung> oeffnungen = [];
    Dictionary<string, List<Pruefoeffnung>>? jeWirt;
    Dictionary<string, List<Pruefoeffnung>>? jeGid;

    /// <summary>Alle Öffnungen der Netzdateien Fassung 2 (auch die, deren Wirt keinen Prüfkörper hat).</summary>
    public IReadOnlyList<Pruefoeffnung> Oeffnungen
    {
        get => oeffnungen;
        init { oeffnungen = value; jeWirt = null; jeGid = null; }   // «with» kopiert sonst die Zwischenspeicher
    }

    /// <summary>Räumlicher Index über die äusseren Hüllquader.</summary>
    public Raumindex<Pruefkoerper> Index() => new(Koerper, k => k.Huelle);

    public IEnumerable<Pruefkoerper> ImBereich(string bereich) => Koerper.Where(k => k.Bereich == bereich);

    /// <summary>Öffnungen im Prüfkörper mit dieser Kennung (Element-Id bzw. «Unterlage-Id:GlobalId»); leer, wenn er keine hat.</summary>
    public IReadOnlyList<Pruefoeffnung> OeffnungenVon(string kennung)
    {
        jeWirt ??= Gruppieren(Oeffnungen.SelectMany(o => o.WirtKennungen.Select(k => (k, o))));
        return jeWirt.TryGetValue(kennung, out var l) ? l : [];
    }

    /// <summary>Öffnungen eines Prüfkörpers.</summary>
    public IReadOnlyList<Pruefoeffnung> OeffnungenVon(Pruefkoerper k) => OeffnungenVon(k.Kennung);

    /// <summary>Öffnungen im IFC-Bauteil mit dieser GlobalId (Wirt oder sein Ganzes; über alle Unterlagen) – auch ohne Prüfkörper,
    /// z. B. für den Abgleich einer neuen Architekten-Fassung.</summary>
    public IReadOnlyList<Pruefoeffnung> OeffnungenVonGlobalId(string globalId)
    {
        jeGid ??= Gruppieren(Oeffnungen.SelectMany(o => new[] { o.WirtGlobalId, o.BauteilGlobalId }.OfType<string>().Distinct().Select(g => (g, o))));
        return jeGid.TryGetValue(globalId, out var l) ? l : [];
    }

    static Dictionary<string, List<Pruefoeffnung>> Gruppieren(IEnumerable<(string Schluessel, Pruefoeffnung O)> paare)
    {
        var d = new Dictionary<string, List<Pruefoeffnung>>(StringComparer.Ordinal);
        foreach (var (s, o) in paare)
        {
            if (!d.TryGetValue(s, out var l)) d[s] = l = [];
            if (!l.Contains(o)) l.Add(o);
        }
        return d;
    }
}

public static class Pruefkoerperbau
{
    /// <summary>Grenze, bis zu der eine Geschosskote der Netzdatei einem Geschoss des Projekts zugeordnet wird (wie
    /// ifc_uebernehmen, IfcUebernahme.GeschosseZuordnen).</summary>
    public const double GeschossToleranz = 50;

    /// <summary>Wird das Element geprüft? Grafische Elemente der Katalog-Basis Bauteil und Netzelemente (Schicht system mit
    /// Rolle).</summary>
    public static bool IstPruefbar(Katalog k, Element e) =>
        Grafik.IstModellgrafik(k, e) && k.Klassen.TryGetValue(e.Klasse, out var d)
        && (d.IstArt("Bauteil") || (d.Schicht == "system" && d.Rolle is not null));

    /// <summary>hlks | architektur | sonst nach dem Katalog.</summary>
    public static string Bereich(Katalog k, Element e) =>
        !k.Klassen.TryGetValue(e.Klasse, out var d) ? "sonst"
        : d.IstArt("Bauteil") ? "architektur"
        : d.Schicht == "system" && d.Rolle is not null ? "hlks" : "sonst";

    /// <summary>Übernommenes IFC-Element, seit dem letzten Übertrag nicht in KICAD geändert – die Regel steht an einer Stelle im
    /// Import (<see cref="IfcUebernehmen.UnveraendertUebernommen"/>); hier nur weitergereicht.</summary>
    public static bool UnveraendertUebernommen(Element e) => IfcUebernehmen.UnveraendertUebernommen(e);

    /// <summary>IFC-Entitäten der Bekleidungen (Nachtrag B3c): IfcCovering – in IFC4 und IFC2X3 die einzige Entität dafür (Unterdecke
    /// CEILING, Belag FLOORING, Verkleidung CLADDING, Dachhaut ROOFING, Dämmung INSULATION …; am Xbim-Schema geprüft, Test
    /// KollisionBekleidungTests.Bekleidung_GibtEsImXbimSchema).</summary>
    public static readonly IReadOnlySet<string> BekleidungEntitaeten = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "IfcCovering" };

    /// <summary>Ist die IFC-Entität eine Bekleidung?</summary>
    public static bool IstBekleidung(string? entitaet) => entitaet is not null && BekleidungEntitaeten.Contains(entitaet);

    /// <summary>Ist das KICAD-Element eine Bekleidung (IFC-Entität seiner Katalogklasse bzw. einer Basisklasse IfcCovering)?</summary>
    public static bool IstBekleidung(Katalog k, Element e) =>
        k.Klassen.TryGetValue(e.Klasse, out var d) && (IstBekleidung(d.Ifc) || d.Linie.Any(b => k.Klassen.TryGetValue(b, out var bd) && IstBekleidung(bd.Ifc)));

    /// <summary>Bereich eines Hintergrund-Produkts nach seiner IFC-Entität (technische Zuordnung der IFC-Klassen, ANNAHME
    /// Prüfliste): Bauteile der Hülle und des Tragwerks → architektur; Verteilelemente (IfcFlow…, IfcDistribution…, Rohre,
    /// Kanäle, Geräte der Gebäudetechnik) → hlks; Proxy, Möbel, Elektro und Unbekanntes → sonst.</summary>
    public static string BereichVonEntitaet(string? entitaet)
    {
        if (string.IsNullOrEmpty(entitaet)) return "sonst";
        var e = entitaet.ToUpperInvariant();
        string[] architektur =
        [
            "IFCWALL", "IFCWALLSTANDARDCASE", "IFCWALLELEMENTEDCASE", "IFCSLAB", "IFCSLABSTANDARDCASE", "IFCSLABELEMENTEDCASE", "IFCROOF",
            "IFCBEAM", "IFCBEAMSTANDARDCASE", "IFCCOLUMN", "IFCCOLUMNSTANDARDCASE", "IFCSTAIR", "IFCSTAIRFLIGHT", "IFCRAMP", "IFCRAMPFLIGHT",
            "IFCRAILING", "IFCCOVERING", "IFCCURTAINWALL", "IFCPLATE", "IFCPLATESTANDARDCASE", "IFCMEMBER", "IFCMEMBERSTANDARDCASE",
            "IFCFOOTING", "IFCPILE", "IFCDOOR", "IFCDOORSTANDARDCASE", "IFCWINDOW", "IFCWINDOWSTANDARDCASE", "IFCBUILDINGELEMENTPART",
            "IFCCHIMNEY", "IFCSHADINGDEVICE",
        ];
        if (architektur.Contains(e)) return "architektur";
        string[] hlksVorsilben = ["IFCFLOW", "IFCDISTRIBUTIONFLOW", "IFCDISTRIBUTIONELEMENT", "IFCENERGYCONVERSION", "IFCPIPE", "IFCDUCT"];
        string[] hlks =
        [
            "IFCAIRTERMINAL", "IFCAIRTERMINALBOX", "IFCAIRTOAIRHEATRECOVERY", "IFCBOILER", "IFCBURNER", "IFCCHILLER", "IFCCOIL",
            "IFCCOMPRESSOR", "IFCCONDENSER", "IFCCOOLEDBEAM", "IFCCOOLINGTOWER", "IFCDAMPER", "IFCEVAPORATIVECOOLER", "IFCEVAPORATOR",
            "IFCFAN", "IFCFILTER", "IFCFIRESUPPRESSIONTERMINAL", "IFCFLOWMETER", "IFCHEATEXCHANGER", "IFCHUMIDIFIER", "IFCINTERCEPTOR",
            "IFCPUMP", "IFCSANITARYTERMINAL", "IFCSPACEHEATER", "IFCSTACKTERMINAL", "IFCTANK", "IFCTUBEBUNDLE", "IFCUNITARYEQUIPMENT",
            "IFCVALVE", "IFCWASTETERMINAL", "IFCMEDICALDEVICE",
        ];
        if (hlks.Contains(e) || hlksVorsilben.Any(v => e.StartsWith(v, StringComparison.Ordinal))) return "hlks";
        return "sonst";
    }

    sealed class Netzdaten
    {
        public required Element Unterlage { get; init; }
        public required IfcNetzdatei Datei { get; init; }
        /// <summary>GlobalId → exaktes Netz für Elemente (übernommene Produkte; Ganze aus ihren Teilen).</summary>
        public Dictionary<string, (IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke)> Uebernommen { get; } = new(StringComparer.Ordinal);
        public List<(IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke)> Hintergrund { get; } = [];
        /// <summary>Fassung 2: GlobalId eines Teils → GlobalId seines Ganzen.</summary>
        public Dictionary<string, string> GanzesVon { get; } = new(StringComparer.Ordinal);
        /// <summary>Fassung 2: Hintergrund-Teile, die nicht geprüft werden, weil ihr Ganzes ein eigenes Netz hat.</summary>
        public int TeileImGanzen;
        /// <summary>Fassung 2: Ganze ohne eigenes Netz in der Datei, aus ihren Teilen zusammengesetzt.</summary>
        public int GanzeAusTeilen;
        /// <summary>Fassung 2: GlobalIds der aus Teilen zusammengesetzten Hintergrund-Ganzen.</summary>
        public HashSet<string> AusTeilen { get; } = new(StringComparer.Ordinal);

        /// <summary>Teil ohne Bezug zu einem Ganzen (Fassung 1: jedes IfcBuildingElementPart; Fassung 2: IfcBuildingElementPart mit
        /// Rolle bauteil, die IFC-Datei nennt kein Ganzes) – für die Lage-Heuristik.</summary>
        public bool TeilOhneBezug(IfcNetzProdukt pr) => pr.Entitaet == Teilentitaet
            && (Datei.Fassung < 2 || (pr.Rolle == IfcNetzRolle.Bauteil && !AusTeilen.Contains(pr.GlobalId)));
    }

    /// <summary>Teile eines Ganzen zu einem Datensatz unter der GlobalId des Ganzen vereinen (Punkte hintereinander, Indizes
    /// verschoben, Begrenzung vereint; Entität und Geschoss vom ersten Teil).</summary>
    static (IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke) Vereinen(string ganz, List<(IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke)> teile)
    {
        var pu = new float[teile.Sum(t => t.Punkte.Length)];
        var dr = new int[teile.Sum(t => t.Dreiecke.Length)];
        float[] bb = [float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue];
        int op = 0, od = 0;
        foreach (var (pr, p, d) in teile)
        {
            Array.Copy(p, 0, pu, op, p.Length);
            int basis = op / 3;
            for (int i = 0; i < d.Length; i++) dr[od + i] = d[i] + basis;
            op += p.Length; od += d.Length;
            for (int i = 0; i < 3; i++) { bb[i] = Math.Min(bb[i], pr.Begrenzung[i]); bb[i + 3] = Math.Max(bb[i + 3], pr.Begrenzung[i + 3]); }
        }
        var k = teile[0].Produkt;
        var materialien = teile.SelectMany(t => t.Produkt.Materialien).Distinct(StringComparer.Ordinal).ToList();   // Fassung 3: Material aller Teile
        return (k with
        {
            EntityLabel = 0, GlobalId = ganz, Begrenzung = bb, Punkte = pu.Length / 3, Dreiecke = dr.Length / 3, Lage = -1, Rolle = IfcNetzRolle.Bauteil, Bezug = null,
            Materialien = materialien, Materialquelle = materialien.Count > 0 ? "Teile des Ganzen" : null,
        }, pu, dr);
    }

    /// <summary>Datensätze einer Netzdatei Fassung 2 einordnen: übernommene → exakte Netze, nicht übernommene → Hintergrund; Teile
    /// nach ihrem Ganzen (eigenes Netz → nicht einzeln; sonst zusammen als Prüfkörper des Ganzen). elementGids = GlobalIds der
    /// Elemente aus dieser Unterlage.</summary>
    static void Einordnen(Netzdaten nd, IReadOnlySet<string> elementGids, bool exakt, bool hintergrund)
    {
        var mitNetz = nd.Datei.Produkte.Where(x => x.Dreiecke > 0 && x.Rolle != IfcNetzRolle.Oeffnung).Select(x => x.GlobalId).ToHashSet(StringComparer.Ordinal);
        var ohneGanzes = new Dictionary<string, List<(IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke)>>(StringComparer.Ordinal);
        foreach (var x in nd.Datei.Alle(nurHintergrund: false))
        {
            var pr = x.Produkt;
            if (pr.Rolle == IfcNetzRolle.Teil && pr.Bezug is { } gz) nd.GanzesVon[pr.GlobalId] = gz;
            if (x.Dreiecke.Length == 0) continue;
            if (pr.Rolle == IfcNetzRolle.Teil && pr.Bezug is { } ganz)
            {
                if (pr.Uebernommen && exakt) nd.Uebernommen.TryAdd(pr.GlobalId, x);   // selbst übernommenes Teil (Dachplatte als Decke)
                if (mitNetz.Contains(ganz)) { if (!pr.Uebernommen) nd.TeileImGanzen++; }
                else
                {
                    if (!ohneGanzes.TryGetValue(ganz, out var l)) ohneGanzes[ganz] = l = [];
                    l.Add(x);
                }
                continue;
            }
            if (pr.Uebernommen) { if (exakt) nd.Uebernommen.TryAdd(pr.GlobalId, x); }
            else if (hintergrund) nd.Hintergrund.Add(x);
        }
        // Ganze ohne eigenes Netz in der Datei: ihre Teile zusammen (ohne Teile, die selbst Elemente sind) – als exaktes Netz des
        // Elements mit der GlobalId des Ganzen, sonst als Hintergrund aus den nicht übernommenen Teilen
        foreach (var (ganz, teile) in ohneGanzes)
        {
            bool element = elementGids.Contains(ganz);
            var auswahl = teile.Where(t => t.Dreiecke.Length > 0 && !elementGids.Contains(t.Produkt.GlobalId) && (element || !t.Produkt.Uebernommen)).ToList();
            if (auswahl.Count == 0) continue;
            var vereint = Vereinen(ganz, auswahl);
            if (element) { if (!exakt || !nd.Uebernommen.TryAdd(ganz, vereint)) continue; }
            else if (hintergrund) { nd.Hintergrund.Add((vereint.Produkt with { Uebernommen = false }, vereint.Punkte, vereint.Dreiecke)); nd.AusTeilen.Add(ganz); }
            else continue;
            nd.GanzeAusTeilen++;
        }
    }

    /// <summary>Prüfkörper aller prüfbaren Elemente (bzw. der gewählten) und – mit Hintergrund – der nicht übernommenen
    /// Produkte der IFC-Unterlagen.</summary>
    public static Pruefkoerpermenge Erzeugen(Projekt p, PruefkoerperOptionen? o = null)
    {
        o ??= new PruefkoerperOptionen();
        var m = p.Modell; var k = p.Katalog;
        var hinweise = new List<string>();
        var aus = new List<Pruefkoerper>();
        HashSet<string>? auswahl = null;
        if (o.Elemente is not null)
        {
            auswahl = [];
            foreach (var x in o.Elemente)
                if (m.Finde(x) is { } e) auswahl.Add(e.Id); else hinweise.Add($"{x}: Element nicht gefunden.");
        }

        // Netzdateien der IFC-Unterlagen (einmal je Datei gelesen)
        var netze = new Dictionary<string, Netzdaten>();
        HashSet<string>? unterlagen = o.Unterlagen?.Select(x => m.Finde(x)?.Id).OfType<string>().ToHashSet();
        var elementGids = m.Elemente.Where(e => e.Kennungen is { } kn && kn.ContainsKey("ifc") && kn.ContainsKey("quelle"))
            .GroupBy(e => e.Kennungen!["quelle"]).ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(e => Basis(e.Kennungen!["ifc"])).ToHashSet(StringComparer.Ordinal));
        foreach (var ul in m.DerKlasse("Unterlage"))
        {
            if (!string.Equals(ul.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase) || ul.Text("netz") is not { Length: > 0 } name) continue;
            string pfad;
            try { pfad = Unterlagen.Pfad(p, name); }
            catch (HlksFehler) { hinweise.Add($"Unterlage {ul.Kennzeichen}: Netzdatei {name} fehlt – ohne exakte Netze und Hintergrund."); continue; }
            if (!File.Exists(pfad)) { hinweise.Add($"Unterlage {ul.Kennzeichen}: Netzdatei {name} fehlt – ohne exakte Netze und Hintergrund."); continue; }
            var nd = new Netzdaten { Unterlage = ul, Datei = IfcNetzdatei.Oeffnen(pfad) };
            bool hintergrund = o.Hintergrund && (unterlagen is null || unterlagen.Contains(ul.Id));
            if (nd.Datei.Fassung >= 2)
            {
                Einordnen(nd, elementGids.GetValueOrDefault(ul.Id) ?? new HashSet<string>(), o.ExakteNetze, hintergrund);
                if (nd.TeileImGanzen > 0)
                    hinweise.Add($"Unterlage {ul.Kennzeichen}: {nd.TeileImGanzen} Teile nicht einzeln geprüft – ihr Ganzes hat einen eigenen Körper.");
                if (nd.GanzeAusTeilen > 0)
                    hinweise.Add($"Unterlage {ul.Kennzeichen}: {nd.GanzeAusTeilen} Bauteile ohne eigenen Körper aus ihren Teilen zusammengesetzt.");
            }
            else
            {
                hinweise.Add($"Unterlage {ul.Kennzeichen}: Netzdatei in Fassung {nd.Datei.Fassung} (ohne Öffnungen und ohne Bezug Teil → Ganzes) – " +
                             "Teile nach ihrer Lage erkannt, Öffnungen fehlen. Die IFC-Datei erneut übernehmen, damit die Netzdatei in Fassung " +
                             $"{IfcNetzErzeuger.Fassung} entsteht.");
                foreach (var (pr, pu, dr) in nd.Datei.Alle(nurHintergrund: false))
                {
                    if (pr.Uebernommen) { if (o.ExakteNetze) nd.Uebernommen.TryAdd(pr.GlobalId, (pr, pu, dr)); }
                    else if (hintergrund) nd.Hintergrund.Add((pr, pu, dr));
                }
            }
            netze[ul.Id] = nd;
        }

        // Elemente. Ein IFC-Produkt kann auf mehrere Elemente verteilt sein (Wand mit geknickter Achse: Kennung ifc «GlobalId»,
        // «GlobalId:2» …, IfcBauteile.NeuAbgeleitet). Sein Netz gilt dann für alle Stücke zusammen: sind alle unverändert, trägt
        // das erste Stück das exakte Netz (die übrigen stehen in WeitereElemente), sonst haben alle ihren KICAD-Körper.
        var pruefbar = m.Elemente.Where(e => (auswahl is null || auswahl.Contains(e.Id)) && IstPruefbar(k, e)).ToList();
        var stuecke = m.Elemente.Where(e => e.Kennungen is { } kn && kn.ContainsKey("ifc") && kn.ContainsKey("quelle") && IstPruefbar(k, e))
            .GroupBy(e => (e.Kennungen!["quelle"], Basis(e.Kennungen["ifc"]))).Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.ToList());
        var mitKoerper = new List<Element>();
        foreach (var e in pruefbar)
        {
            if (HlksEinordnung.Art(k, e) == HlksArt.Leitung)
            {
                if (Leitung(p, e) is { } pk) aus.Add(pk);
                else hinweise.Add($"{e.Kennzeichen}: Leitung ohne zwei verschiedene Punkte – kein Prüfkörper.");
                continue;
            }
            if (o.ExakteNetze && UnveraendertUebernommen(e) && netze.TryGetValue(e.Kennungen!["quelle"], out var nd))
            {
                var gid = e.Kennungen["ifc"];
                var basis = Basis(gid);
                stuecke.TryGetValue((e.Kennungen["quelle"], basis), out var gruppe);
                bool alleUnveraendert = gruppe is null || gruppe.All(UnveraendertUebernommen);
                bool erstes = gid == basis;
                bool erstesDa = gruppe is null || gruppe.Any(x => x.Kennungen!["ifc"] == basis);
                if (alleUnveraendert && erstesDa && nd.Uebernommen.TryGetValue(basis, out var prod))
                {
                    if (!erstes) continue;   // vom ersten Stück mit dem Netz des ganzen Produkts abgedeckt
                    var netz = Pruefnetz.Aus(prod.Punkte, prod.Dreiecke);
                    if (netz.AnzahlDreiecke > 0)
                    {
                        var weitere = gruppe?.Where(x => x.Id != e.Id).Select(x => x.Id).ToList() ?? [];
                        aus.Add(ElementKoerper(p, e, netz, "netzdatei", nd.Unterlage.Id, prod.Produkt.Entitaet, weitere, prod.Produkt, nd.Datei.Fassung));
                        continue;
                    }
                }
            }
            mitKoerper.Add(e);
        }
        if (mitKoerper.Count > 0)
        {
            var koerper = Grafik.Koerper(p, mitKoerper.Select(e => e.Id)).ToDictionary(x => x.Id);
            foreach (var e in mitKoerper)
            {
                if (!koerper.TryGetValue(e.Id, out var kp)) { hinweise.Add($"{e.Kennzeichen} ({e.Klasse}): kein Körper – kein Prüfkörper."); continue; }
                var netz = Pruefnetzbau.AusKoerper(kp, hinweise, e.Kennzeichen);
                if (netz is null || netz.AnzahlDreiecke == 0) { hinweise.Add($"{e.Kennzeichen} ({e.Klasse}): Körper ohne Dreiecke – kein Prüfkörper."); continue; }
                // übernommenes Element mit KICAD-Körper: Material und Typname seines Produkts aus der Netzdatei (Nachtrag B3c)
                IfcNetzProdukt? produkt = null; int? fassung = null;
                if (e.Kennungen is { } kn && kn.TryGetValue("quelle", out var q) && kn.TryGetValue("ifc", out var g0) && netze.TryGetValue(q, out var ndk))
                {
                    produkt = ndk.Datei.Produkt(Basis(g0));
                    fassung = ndk.Datei.Fassung;
                }
                aus.Add(ElementKoerper(p, e, netz, "koerper", e.Kennungen?.GetValueOrDefault("quelle"), null, null, produkt, fassung));
            }
        }

        // Hintergrund
        var geschosse = m.DerKlasse("Geschoss").Select(g => (g.Id, Kote: g.Zahl("kote"))).Where(g => g.Kote is not null).ToList();
        var teileOhneBezug = new List<Pruefkoerper>();
        foreach (var nd in netze.Values)
            foreach (var (pr, pu, dr) in nd.Hintergrund)
            {
                var netz = Pruefnetz.Aus(pu, dr);
                if (netz.AnzahlDreiecke == 0) continue;
                string? geschoss = null;
                if (pr.GeschossKote is double kote)
                    geschoss = geschosse.Where(g => Math.Abs(g.Kote!.Value - kote) <= GeschossToleranz).OrderBy(g => Math.Abs(g.Kote!.Value - kote)).Select(g => g.Id).FirstOrDefault();
                var hk = new Pruefkoerper
                {
                    Kennung = $"{nd.Unterlage.Id}:{pr.GlobalId}", UnterlageId = nd.Unterlage.Id, GlobalId = pr.GlobalId, Entitaet = pr.Entitaet,
                    Bereich = BereichVonEntitaet(pr.Entitaet), Quelle = "hintergrund", Geschoss = geschoss, GeschossKote = pr.GeschossKote,
                    Netz = netz, Huelle = netz.Huelle, HuelleOhneDaemmung = netz.Huelle,
                    Bekleidung = IstBekleidung(pr.Entitaet), Materialien = pr.Materialien, Typname = pr.Typname, Materialquelle = pr.Materialquelle,
                    NetzFassung = nd.Datei.Fassung,
                };
                aus.Add(hk);
                if (nd.TeilOhneBezug(pr)) teileOhneBezug.Add(hk);
            }
        // Teile ohne Bezug zu ihrem Ganzen (Fassung 1; Fassung 2, wenn die IFC-Datei keine Zerlegung nennt – Revit-Export der
        // verknüpften Architektur der Bernstrasse): Lage-Heuristik aus B1
        if (o.DoppelteTeileWeglassen && teileOhneBezug.Count > 0) Weglassen(aus, hinweise, teileOhneBezug);
        var oeffnungen = o.Oeffnungen ? OeffnungenLaden(netze.Values, aus) : [];
        return new Pruefkoerpermenge(aus, hinweise) { Oeffnungen = oeffnungen };
    }

    /// <summary>Öffnungen aller Netzdateien Fassung 2 mit den Kennungen der Prüfkörper ihres Wirts (über Unterlage und GlobalId;
    /// ist der Wirt ein Teil, über sein Ganzes; geteilte Wände: alle Stücke mit eigenem Prüfkörper).</summary>
    static List<Pruefoeffnung> OeffnungenLaden(IEnumerable<Netzdaten> netze, List<Pruefkoerper> koerper)
    {
        var aus = new List<Pruefoeffnung>();
        var nachGid = new Dictionary<(string, string), List<string>>();
        foreach (var k in koerper)
        {
            if (k.UnterlageId is null || k.GlobalId is null) continue;
            var s = (k.UnterlageId, Basis(k.GlobalId));
            if (!nachGid.TryGetValue(s, out var l)) nachGid[s] = l = [];
            l.Add(k.Kennung);
        }
        Raumindex<Pruefkoerper>? index = null;   // für Öffnungen ohne Wirt: Bauteile mit geschlossenem Netz
        foreach (var nd in netze)
        {
            if (nd.Datei.Fassung < 2 || nd.Datei.Oeffnungen.Count == 0) continue;
            foreach (var (op, pu, dr) in nd.Datei.AlleOeffnungen())
            {
                if (dr.Length == 0) continue;
                var netz = Pruefnetz.Aus(pu, dr);
                if (op.Bezug is not { } wirt)
                {
                    // ohne Wirt in der IFC-Datei: aus der Lage (siehe WirtInDerLage)
                    index ??= new Raumindex<Pruefkoerper>(koerper.Where(k => k.Netz is not null && k.Bereich != "hlks" && k.UnterlageId is not null).ToList(), k => k.Huelle);
                    var mitte = (netz.Huelle.Min + netz.Huelle.Max) * 0.5;
                    var wirte = index.Kandidaten(new Huelle3(mitte, mitte))
                        .Where(k => k.UnterlageId == nd.Unterlage.Id && WirtInDerLage(k, netz.Huelle))
                        .Select(k => k.Kennung).Distinct().ToList();
                    aus.Add(new Pruefoeffnung
                    {
                        UnterlageId = nd.Unterlage.Id, GlobalId = op.GlobalId, Entitaet = op.Entitaet, WirtKennungen = wirte, WirtAusLage = wirte.Count > 0,
                        Fuellungen = op.Fuellungen, GeschossKote = op.GeschossKote, Netz = netz,
                    });
                    continue;
                }
                var bauteil = wirt;
                for (int i = 0; i < 8 && nd.GanzesVon.TryGetValue(bauteil, out var g); i++) bauteil = g;
                var kennungen = nachGid.GetValueOrDefault((nd.Unterlage.Id, bauteil)) ?? nachGid.GetValueOrDefault((nd.Unterlage.Id, wirt)) ?? [];
                aus.Add(new Pruefoeffnung
                {
                    UnterlageId = nd.Unterlage.Id, GlobalId = op.GlobalId, Entitaet = op.Entitaet, WirtGlobalId = wirt, BauteilGlobalId = bauteil,
                    WirtKennungen = kennungen.ToList(), Fuellungen = op.Fuellungen, GeschossKote = op.GeschossKote, Netz = netz,
                });
            }
        }
        return aus;
    }

    /// <summary>Ist der Prüfkörper k (Netz) der Wirt einer Öffnung mit dem Hüllquader o, für die die IFC-Datei keinen Wirt nennt?
    /// ANNAHME (Prüfliste): die Mitte der Öffnung liegt in seinem Hüllquader und entweder in seinem geschlossenen Netz (Netz nicht
    /// ausgeschnitten, wie bei Archicad) oder das Netz umgibt die Mitte: in einer von acht waagrechten Richtungen (je 22.5°) oder
    /// senkrecht treffen die Strahlen von der Mitte nach beiden Seiten sein Netz innerhalb der halben Diagonale + 100 mm (Netz schon
    /// ausgeschnitten, wie bei Revit: die Leibung liegt beidseits der Öffnung). Ein Bauteil, das die Öffnung nur von einer Seite
    /// berührt (Decke unter einer Türöffnung, anstossende Wand), zählt nicht.</summary>
    public static bool WirtInDerLage(Pruefkoerper k, Huelle3 o)
    {
        if (k.Netz is not { } n) return false;
        var mitte = (o.Min + o.Max) * 0.5;
        if (!k.Huelle.Enthaelt(mitte)) return false;
        if (n.HatGeschlosseneTeile && n.Lage(mitte) != Punktlage.Aussen) return true;
        double l = (o.Max - o.Min).Laenge / 2 + 100;
        for (int i = 0; i <= 8; i++)
        {
            var d = i < 8 ? new V3(Math.Cos(i * Math.PI / 8), Math.Sin(i * Math.PI / 8), 0) : new V3(0, 0, 1);
            if (n.Treffer(mitte, mitte + d * l).Count > 0 && n.Treffer(mitte, mitte - d * l).Count > 0) return true;
        }
        return false;
    }

    /// <summary>Entität der Bauteil-Teile (Schichten, Zerlegung) in der Netzdatei.</summary>
    public const string Teilentitaet = "IfcBuildingElementPart";

    /// <summary>Für Teile ohne Bezug zu ihrem Ganzen (Netzdatei Fassung 1; Fassung 2, wenn die IFC-Datei keine Zerlegung nennt):
    /// Hintergrund-Teile weglassen, die ganz in einem geschlossenen Netz eines anderen Prüfkörpers derselben Unterlage liegen
    /// (Hüllquader darin, bis zu 16 Ecken innen oder auf der Fläche). ANNAHME (Prüfliste, B1): ein solches Teil ist eine Schicht
    /// dieses Bauteils, das mit eigenem Körper schon geprüft wird. nurUnterlagen: nur Teile aus diesen Unterlagen (null = alle).
    /// Rückgabe: Anzahl weggelassener Teile.</summary>
    public static int TeileImGanzenWeglassen(List<Pruefkoerper> aus, List<string> hinweise, IReadOnlySet<string>? nurUnterlagen = null) =>
        Weglassen(aus, hinweise, aus.Where(k => k.Quelle == "hintergrund" && k.Entitaet == Teilentitaet && k.Netz is not null
                                                && (nurUnterlagen is null || (k.UnterlageId is { } u && nurUnterlagen.Contains(u)))).ToList());

    static int Weglassen(List<Pruefkoerper> aus, List<string> hinweise, List<Pruefkoerper> teile)
    {
        if (teile.Count == 0) return 0;
        var ganze = aus.Where(k => k.Netz is not null && k.Entitaet != Teilentitaet && k.UnterlageId is not null && k.Bereich != "hlks").ToList();
        var index = new Raumindex<Pruefkoerper>(ganze, k => k.Huelle);
        var weg = new HashSet<Pruefkoerper>();
        foreach (var t in teile)
            foreach (var g in index.Kandidaten(t.Huelle))
            {
                if (g.UnterlageId != t.UnterlageId || !g.Huelle.Umfasst(t.Huelle, 1) || !g.Netz!.HatGeschlosseneTeile) continue;
                if (!EckenInnen(t.Netz!, g.Netz)) continue;
                weg.Add(t);
                break;
            }
        if (weg.Count == 0) return 0;
        aus.RemoveAll(weg.Contains);
        hinweise.Add($"{weg.Count} Hintergrund-Teile ({Teilentitaet}) weggelassen: sie liegen im Netz eines Bauteils derselben Unterlage, das schon geprüft wird.");
        return weg.Count;
    }

    static bool EckenInnen(Pruefnetz teil, Pruefnetz ganz)
    {
        int n = teil.AnzahlEcken, schritt = Math.Max(1, n / 16);
        for (int i = 0; i < n; i += schritt)
            if (ganz.Lage(teil.Ecke(i)) == Punktlage.Aussen) return false;
        return n > 0;
    }

    static Pruefkoerper ElementKoerper(Projekt p, Element e, Pruefnetz netz, string quelle, string? unterlage, string? entitaet, IReadOnlyList<string>? weitere = null,
        IfcNetzProdukt? produkt = null, int? netzFassung = null) => new()
    {
        Kennung = e.Id, ElementId = e.Id, Kennzeichen = e.Kennzeichen, Klasse = e.Klasse, UnterlageId = unterlage,
        GlobalId = e.Kennungen?.GetValueOrDefault("ifc"), Entitaet = entitaet, Gewerk = Ableitungen.Gewerk(p.Modell, p.Katalog, e),
        Bereich = Bereich(p.Katalog, e), Quelle = quelle, Geschoss = e.Geschoss, GeschossKote = p.Modell.Finde(e.Geschoss)?.Zahl("kote"),
        Netz = netz, Huelle = netz.Huelle, HuelleOhneDaemmung = netz.Huelle, WeitereElemente = weitere ?? [],
        Bekleidung = IstBekleidung(p.Katalog, e) || IstBekleidung(entitaet ?? produkt?.Entitaet),
        Materialien = produkt?.Materialien ?? [], Typname = produkt?.Typname, Materialquelle = produkt?.Materialquelle, NetzFassung = netzFassung,
    };

    /// <summary>GlobalId ohne den Zusatz geteilter Stücke («GlobalId:2» → «GlobalId»; GlobalIds enthalten keinen Doppelpunkt).</summary>
    static string Basis(string kennung) => kennung.IndexOf(':') is var i and > 0 ? kennung[..i] : kennung;

    /// <summary>Prüfkörper einer Leitung: Kapsel bzw. Quader je Teilstück, Dämmdicke für die zweite Hülle; null ohne zwei
    /// verschiedene Punkte.</summary>
    public static Pruefkoerper? Leitung(Projekt p, Element e)
    {
        var punkte = new List<V3>();
        foreach (var q in HlksEinordnung.Punkte(e))
        {
            var v = new V3(q[0], q[1], q[2]);
            if (punkte.Count == 0 || (v - punkte[^1]).Laenge >= 1e-3) punkte.Add(v);
        }
        if (punkte.Count < 2) return null;
        var mass = HlksMasse.Von(p, e);
        double daemm = mass?.Daemmdicke ?? (e.Zahl("daemmdicke") is double dd && dd > 0 && double.IsFinite(dd) ? dd : 0);
        string Z(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
        var achsen = new List<Achskoerper>();
        string massquelle;
        if (mass is { Rechteck: true })
        {
            double verl = Math.Max(mass.Breite, mass.Hoehe) / 2;
            for (int i = 1; i < punkte.Count; i++)
            {
                var a = punkte[i - 1]; var b = punkte[i];
                var d = (b - a).Einheit();
                achsen.Add(Achskoerper.Quader(i > 1 ? a - d * verl : a, i < punkte.Count - 1 ? b + d * verl : b, mass.Breite, mass.Hoehe));
            }
            massquelle = mass.Serie is { } s ? $"Serie {s.SerieName}, {s.Typ}: {Z(mass.Breite)} × {Z(mass.Hoehe)} mm" : $"Eigenschaften breite × hoehe: {Z(mass.Breite)} × {Z(mass.Hoehe)} mm";
        }
        else
        {
            double durchmesser = mass?.Durchmesser ?? Math.Max(HlksEinordnung.Dn(e) ?? 0, HlksKoerper.MinDurchmesser);
            for (int i = 1; i < punkte.Count; i++) achsen.Add(Achskoerper.Kapsel(punkte[i - 1], punkte[i], durchmesser / 2));
            massquelle = mass?.Serie is { } s ? $"Serie {s.SerieName}, {s.Typ}: Ø {Z(durchmesser)} mm"
                : mass is not null ? $"Eigenschaft durchmesser: Ø {Z(durchmesser)} mm"
                : $"Darstellung DN-gerecht: Ø max(dn, {Z(HlksKoerper.MinDurchmesser)}) = {Z(durchmesser)} mm";
        }
        if (daemm > 0) massquelle += $", Dämmung {Z(daemm)} mm";
        var innen = achsen.Aggregate(Huelle3.Leer, (h, a) => h.Mit(a.Huelle));
        var aussen = daemm > 0 ? achsen.Aggregate(Huelle3.Leer, (h, a) => h.Mit(a.Aufgeblasen(daemm).Huelle)) : innen;
        return new Pruefkoerper
        {
            Kennung = e.Id, ElementId = e.Id, Kennzeichen = e.Kennzeichen, Klasse = e.Klasse, UnterlageId = e.Kennungen?.GetValueOrDefault("quelle"),
            GlobalId = e.Kennungen?.GetValueOrDefault("ifc"), Gewerk = Ableitungen.Gewerk(p.Modell, p.Katalog, e), Bereich = Bereich(p.Katalog, e),
            Quelle = "achse", Massquelle = massquelle, Geschoss = e.Geschoss, GeschossKote = p.Modell.Finde(e.Geschoss)?.Zahl("kote"),
            Achse = punkte, Achsen = achsen, Daemmdicke = daemm, Huelle = aussen, HuelleOhneDaemmung = innen,
        };
    }
}

/// <summary>Körperteile (Grafik.Koerper) als Dreiecksnetze für die Prüfung.</summary>
public static class Pruefnetzbau
{
    /// <summary>Strecken je Vollkreis für Zylinder und Kugeln im Prüfnetz (umschrieben, siehe Dateikopf).</summary>
    public const int Teilung = 24;

    /// <summary>Alle Teile des Körpers (ohne NichtSchneiden und ohne «netz» der Unterlagen) als ein Prüfnetz; null ohne Teile.
    /// Teile, die sich nicht zerlegen lassen, ergeben einen Hinweis.</summary>
    public static Pruefnetz? AusKoerper(Koerper k, List<string>? hinweise = null, string? wer = null)
    {
        var netze = new List<Netz3>();
        foreach (var t in k.Teile)
        {
            if (t.NichtSchneiden || t.Art == "netz") continue;
            try
            {
                if (Teil(t) is { } n && n.Dreiecke.Count > 0) netze.Add(n);
                else hinweise?.Add($"{wer ?? k.Id}: Körperteil «{t.Art}» unvollständig – übergangen.");
            }
            catch (HlksFehler f) { hinweise?.Add($"{wer ?? k.Id}: Körperteil «{t.Art}» nicht zerlegbar ({f.Message}) – übergangen."); }
        }
        return netze.Count == 0 ? null : Pruefnetz.Aus(netze);
    }

    /// <summary>Ein Körperteil als Netz (quader, prisma, zylinder, kugel, koerper); null bei fehlenden Angaben.</summary>
    public static Netz3? Teil(Koerperteil t)
    {
        switch (t.Art)
        {
            case "quader":
            {
                if (t.Mitte is not { Length: >= 3 } m || t.Masse is not { Length: >= 3 } s || s[0] <= 0 || s[1] <= 0 || s[2] <= 0) return null;
                var n = NetzBau.Quader([-s[0] / 2, -s[1] / 2, -s[2] / 2], [s[0] / 2, s[1] / 2, s[2] / 2]);
                double w = (t.Drehung ?? 0) * Math.PI / 180, c = Math.Cos(w), sn = Math.Sin(w);
                return n.Transformiert(q => [m[0] + q[0] * c - q[1] * sn, m[1] + q[0] * sn + q[1] * c, m[2] + q[2]]);
            }
            case "prisma":
            {
                if (t.Grundriss is not { Count: >= 3 } g) return null;
                double unten = t.Unten ?? 0, oben = t.Oben ?? unten;
                var kontur = g.Where(q => q.Length >= 2).Select(q => (q[0], q[1])).ToList();
                var loecher = (t.Loecher ?? []).Where(l => l.Count >= 3).Select(l => l.Select(q => (q[0], q[1])).ToList()).ToList();
                return NetzBau.Extrusion("xy", kontur, loecher, unten, oben);
            }
            case "zylinder":
                if (t.Von is not { Length: >= 3 } von || t.Nach is not { Length: >= 3 } nach || t.Radius is not double r || r <= 0) return null;
                return Zylinder(V3.Aus(von), V3.Aus(nach), r);
            case "kugel":
                if (t.Mitte is not { Length: >= 3 } mk || t.Radius is not double rk || rk <= 0) return null;
                return Kugel(V3.Aus(mk), rk);
            case "koerper":
            {
                if (t.Ecken is not { Count: >= 3 } ecken || t.Dreiecke is not { Count: >= 1 } dr) return null;
                var n = new Netz3();
                foreach (var q in ecken) n.Ecken.Add(q.Length >= 3 ? q : [q[0], q.Length > 1 ? q[1] : 0, 0]);
                foreach (var x in dr) if (x is { Length: 3 }) n.Dreiecke.Add(x);
                return n;
            }
            default: return null;
        }
    }

    /// <summary>Zylinder von → nach mit Radius r als umschriebenes Vieleck-Prisma (Teilung Strecken), mit Deckeln.</summary>
    public static Netz3 Zylinder(V3 von, V3 nach, double r)
    {
        var d = nach - von;
        if (d.Laenge < 1e-9) throw new HlksFehler("Zylinder ohne Länge.", "Zwei verschiedene Punkte angeben.");
        var (e1, e2) = d.Einheit().Senkrechte();
        double ru = r / Math.Cos(Math.PI / Teilung);
        var n = new Netz3();
        var unten = new int[Teilung]; var oben = new int[Teilung];
        for (int k = 0; k < Teilung; k++)
        {
            double w = 2 * Math.PI * (k + 0.5) / Teilung;
            var q = e1 * (ru * Math.Cos(w)) + e2 * (ru * Math.Sin(w));
            unten[k] = n.Ecke(von + q); oben[k] = n.Ecke(nach + q);
        }
        int mu = n.Ecke(von), mo = n.Ecke(nach);
        for (int k = 0; k < Teilung; k++)
        {
            int k1 = (k + 1) % Teilung;
            n.Viereck(unten[k], unten[k1], oben[k1], oben[k]);
            n.Dreieck(mu, unten[k1], unten[k]);
            n.Dreieck(mo, oben[k], oben[k1]);
        }
        n.Orientieren();
        return n;
    }

    static readonly Lazy<Netz3> Einheitskugel = new(() =>
    {
        const int breiten = Teilung / 2;
        var n = new Netz3();
        int sued = n.Ecke(0, 0, -1);
        var ringe = new int[breiten - 1][];
        for (int j = 1; j < breiten; j++)
        {
            double phi = -Math.PI / 2 + Math.PI * j / breiten;
            ringe[j - 1] = new int[Teilung];
            for (int k = 0; k < Teilung; k++)
            {
                double l = 2 * Math.PI * k / Teilung;
                ringe[j - 1][k] = n.Ecke(Math.Cos(phi) * Math.Cos(l), Math.Cos(phi) * Math.Sin(l), Math.Sin(phi));
            }
        }
        int nord = n.Ecke(0, 0, 1);
        for (int k = 0; k < Teilung; k++)
        {
            int k1 = (k + 1) % Teilung;
            n.Dreieck(sued, ringe[0][k1], ringe[0][k]);
            for (int j = 0; j < breiten - 2; j++) n.Viereck(ringe[j][k], ringe[j][k1], ringe[j + 1][k1], ringe[j + 1][k]);
            n.Dreieck(nord, ringe[^1][k], ringe[^1][k1]);
        }
        n.Orientieren();
        // umschreiben: kleinster Abstand einer Fläche (Ebene durch das Dreieck) zur Mitte wird 1
        double min = double.MaxValue;
        foreach (var t in n.Dreiecke)
        {
            var a = V3.Aus(n.Ecken[t[0]]); var b = V3.Aus(n.Ecken[t[1]]); var c = V3.Aus(n.Ecken[t[2]]);
            var nn = (b - a).Kreuz(c - a).Einheit();
            min = Math.Min(min, Math.Abs(nn.Mal(a)));
        }
        return n.Transformiert(q => [q[0] / min, q[1] / min, q[2] / min]);
    });

    /// <summary>Kugel als umschriebenes Netz (Teilung Längen-, Teilung/2 Breitenstreifen).</summary>
    public static Netz3 Kugel(V3 mitte, double r) =>
        Einheitskugel.Value.Transformiert(q => [mitte.X + r * q[0], mitte.Y + r * q[1], mitte.Z + r * q[2]]);
}
