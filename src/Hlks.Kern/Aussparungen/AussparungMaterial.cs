// Automatische Aussparungen, Nachtrag B3c (Entscheid Christian 19.10.2026, entwurf/modul-3b.md «Entscheide Christian 19.10.2026»):
// «Aussparungen nur in Beton und Mauerwerk» – Aussparungen gehören zum Rohbau. Hier steht an EINER Stelle, woran der Kern das
// Material eines gequerten Bauteils erkennt und wie er es einteilt; Vorschlag (AussparungVorschlag.cs), Kollisionsprüfung (P20,
// Paare) und Listen lesen es von hier.
//
// Nachtrag B3d (Entscheide Christian 19.10.2026 «Material», entwurf/modul-3b.md): Material aus dem Typnamen ist erlaubt, eine
// Typenliste «Typname → Material» je Projekt (Kollision/Typenliste.cs, Klasse Typmaterial), neue Kategorie Installationsvorwand.
//
// VORRANG der Quellen (Entscheid Christian 19.10.2026): Typenliste > Materialzuordnung im IFC > Typname-Wortliste > unbekannt.
//   1. Typenliste des Projekts (Typmaterial-Elemente mit Quelle): passt der Typname des Bauteils (genau oder ohne «Familie:», Regel in
//      Typenliste.Finde), gilt die Kategorie des Eintrags – ohne Wortliste, Herkunft «typenliste», Fundstelle = Eintrag mit Quelle.
//   2. Materialzuordnung: KICAD-Bauteile (Modul 1c) mit material (Wand, Stütze, Träger; «unbestimmt» zählt nicht), Aufbau unter
//      bezuege.aufbau (je Schicht das Material-Element: Name und kategorie) und Kurzbeschreibung aufbau («Beton 150 / …»); bei
//      übernommenen Bauteilen und Hintergrund-Produkten dazu die Materialnamen der Netzdatei Fassung 3 (IfcRelAssociatesMaterial,
//      Exemplar sonst Typ). Ältere Netzdateien (Fassung 1, 2) führen kein Material – dann «unbekannt» mit dem Hinweis, die IFC
//      erneut zu übernehmen.
//   3. Typname (Entscheid Christian 19.10.2026: Material darf aus dem Typnamen gelesen werden, als Rückfall) – nur, wenn die
//      Materialzuordnung nicht aussagekräftig ist, und nur, wenn ein Wort der Liste darin steht (ein unbekannter Typname sagt
//      nichts). Herkunft «typname»; Liste und Element zeigen «aus Typname «…»». Befund Bernstrasse 42: der Revit-Export führt als
//      Material nur Darstellungsmaterialien («Farbe, weiss», «<Unnamed>»), die Bauart steht im Typnamen («Beton, Stahlbeton 250»).
//      «Aussagekräftig» (Entscheid Christian 19.10.2026 zu B3d, Fragefenster – die Auslegung aus B3d ist bestätigt): aussagekräftig
//      ist eine Materialzuordnung, die eine BAUART nennt (Beton, Mauerwerk, Dämmung, Leichtbau, Holz, Stahl/Metall,
//      Installationsvorwand). Putz, Farbe oder Belag allein sind es nicht, ebenso wenig Unterdecke, Bodenaufbau, Luftschicht
//      (Nachtrag B3e) oder unbekannte Namen – dann wird der Typname zusätzlich zu diesen Angaben eingeteilt (so bleibt ein
//      verputztes Mauerwerk «MW, Kalksandstein 150» Rohbau, und ein unbekannter Name hält das Bauteil «unbekannt», wenn der Typname
//      keinen Rohbau nennt).
//   4. Sonst unbekannt.
//
// Einteilung (ANNAHME Prüfliste): jede Angabe bekommt die Gruppe des ersten passenden Eintrags der Wortliste (Reihenfolge der
//   Gruppen unten: Installationsvorwand zuerst – die Funktion zählt, nicht der Baustoff der Beplankung –, Mauerwerk vor Beton –
//   Porenbeton, Betonstein –, Beton vor allem Übrigen – Stahlbeton; Rohbau geht vor, lieber eine Aussparung zu viel –, dann
//   Unterdecke und Bodenaufbau – die Funktion zählt: «Akustikdecke Gips», «Trittschalldämmung» –, Dämmung vor Holz und Glas –
//   Holzfaserdämmung, Glaswolle –, Luftschicht nach dem Ausbau und vor «ohne Aussage»). Ein Bauteil ist
//   - Rohbau (Aussparung), wenn mindestens eine Angabe Beton oder Mauerwerk ist – auch neben Dämmung oder Putz (ein Bauteil ist
//     ein Prüfkörper; die Aussparung geht dann über die ganze Bauteildicke);
//   - unbekannt (Aussparung mit Hinweis, Kennzeichen materialUnbekannt), wenn sonst ein Materialname nicht erkannt wird oder es
//     gar keine erkennbare Angabe gibt. ANNAHME Prüfliste: lieber ein Vorschlag zu viel als ein fehlender Durchbruch im Beton;
//   - kein Rohbau (keine Aussparung): alle Angaben erkannt, keine Beton oder Mauerwerk – Installationsvorwand, Unterdecke,
//     Bodenaufbau, Dämmung, Leichtbau (Gips, Ständer), Holz, Stahl/Metall, Ausbau (Bekleidung, Belag, Putz, Abdichtung, Glas,
//     Kunststoff), Luftschicht.
//   «Ohne Aussage» (Farbe, Anstrich, RAL, Farbwörter, <Unnamed>, Default, Hohlraum, übrige Namen mit «Luft» wie «Luftspalt») zählt
//   weder als erkannt noch als unbekannt – Darstellungsmaterialien sagen nichts über den Baustoff.
//   Hauptgruppe (Zählungen, Paare der Kollisionsprüfung): Beton bzw. Mauerwerk, sonst die erste Bauart, sonst Unterdecke bzw.
//   Bodenaufbau, sonst Ausbau, sonst Luftschicht (eine Luftschicht ist ein Bauteil nur, wenn sonst nichts erkannt ist).
// Installationsvorwand (Nachtrag B3d, Entscheid Christian 19.10.2026 zu «_IW GIS 1.8/3.2 cm 50» = Sanitärvorwand Geberit GIS): keine
//   Aussparung; in der Kollisionsprüfung wie eine Bekleidung nur im zuschaltbaren Paar hlks-vorwand (Kollisionspruefung.cs). Die
//   Wörter «GIS», «Vorwand», «Installationswand» der Wortliste hat Christian am 19.10.2026 bestätigt (Fragefenster zu B3d).
//   Projektbezogene Typen (z. B. «2.3_IW 1.5/8/1.5 cm 110» = Gips/Leichtbau, Christian 19.10.2026) gehören NICHT in die Wortliste,
//   sondern in die Typenliste des Projekts.
//
// Nachtrag B3e (Entscheide Christian 19.10.2026 zu B3d, Fragefenster):
//   - Unterdecke (eigene Gruppe, weil sie vor den Baustoffen geprüft wird und anders wirkt als übriger Ausbau): «Unterdecken wie
//     Bekleidung» – Revit exportiert sie teils als IfcSlab («abgehängte Decke»). Keine Aussparung, Kollision nur im Paar
//     hlks-bekleidung (wie IfcCovering). Wörter: Christians Liste; «Kühldecke» und «Deckenverkleidung» (Ergänzung des Agenten zu
//     «Deckenbekleidung») sind ANNAHME (Prüfliste). Übriger Ausbau aus der Wortliste (Putz, Glas, Belag …) bleibt im Paar
//     hlks-architektur (sonst fielen z. B. Glasfenster und Glasgeländer aus dem Vorgabelauf) – Bekleidung im Paar hlks-bekleidung
//     ist er nur als IfcCovering bzw. über die Typenliste (Kategorie bekleidung).
//   - Bodenaufbau (neue Kategorie bodenaufbau, Entscheid Christian): Unterlagsboden, Estrich, Trittschalldämmung, Schichten über der
//     Tragdecke – keine Aussparung, Kollision nur im Paar hlks-bekleidung (Leitungen im Bodenaufbau sind kein Befund). Die Wörter
//     sind ANNAHME (Prüfliste); «UB» steht bewusst nicht darin (zu kurz – regelt die Typenliste je Projekt). «Estrich» heisst in der
//     Schweiz auch Dachboden (Estrichtür, Estrichtreppe).
//   - Luftschicht (neue Kategorie luftschicht, Entscheid Christian «Luftschichten ignorieren»): weder Kollision noch Aussparung noch
//     P20, in keinem Paar (Kollisionspruefung.cs). Wörter: Christians Liste, nur als ganzes Wort bzw. Schichtname – «Luftkanal»,
//     «Zuluft», «Lüftung», «Luft-Wasser-Wärmepumpe», «cavity wall» treffen nicht.
//   - Unterdecke, Bodenaufbau und Luftschicht aus der WORTLISTE gelten nur für Bauteile, die eine Leitung queren kann
//     (Materialbefund.Querbar: Wand, Decke, Dach, Stütze, Träger, Platte, Teil …). ANNAHME (Prüfliste): Türen, Fenster, Treppen,
//     Geländer bleiben im Paar hlks-architektur, auch wenn ihr Name ein solches Wort trägt («Estrichtür», «Estrichtreppe»). Ein
//     Eintrag der Typenliste gilt immer (ausdrückliche Festlegung mit Quelle).
// Es sind Namen zur Einteilung, keine Kennwerte und keine Normwerte.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Materialgruppe für die Entscheidung «Aussparung ja/nein» (Nachtrag B3c; Installationsvorwand seit Nachtrag B3d; Unterdecke,
/// Bodenaufbau, Luftschicht seit Nachtrag B3e). Die Folge ist die Prüffolge der Wortliste.</summary>
public enum Materialgruppe { Installationsvorwand, Mauerwerk, Beton, Unterdecke, Bodenaufbau, Daemmung, Leichtbau, Holz, Metall, Ausbau, Luftschicht, OhneAussage }

/// <summary>Die Wortliste (deutsch, englisch, französisch) – ANNAHME für Christians Prüfliste, soweit nicht als Entscheid vermerkt.</summary>
public static class Materialwortliste
{
    public const string Annahme = "ANNAHME Prüfliste (Nachtrag B3c): Einteilung nach Wortliste deutsch/englisch/französisch";
    /// <summary>Vermerk zur Installationsvorwand aus der Wortliste (Nachtrag B3d; Wörter bestätigt, Nachtrag B3e).</summary>
    public const string EntscheidVorwand = "Entscheid Christian 19.10.2026: «GIS», «Vorwand», «Installationswand» = Installationsvorwand (Nachtrag B3d, bestätigt B3e)";
    /// <summary>Frühere Bezeichnung (B3d: ANNAHME); seit Nachtrag B3e ein Entscheid Christians.</summary>
    [Obsolete("Seit Nachtrag B3e Entscheid Christian – EntscheidVorwand verwenden")]
    public const string AnnahmeVorwand = EntscheidVorwand;
    /// <summary>Vermerk zur Unterdecke aus der Wortliste (Nachtrag B3e).</summary>
    public const string EntscheidUnterdecke = "Unterdecke wie Bekleidung: keine Aussparung, Kollision nur im Paar hlks-bekleidung (Entscheid Christian 19.10.2026, Nachtrag B3e)";
    /// <summary>Wörter der Unterdecke, die Christian nicht genannt hat (Nachtrag B3e).</summary>
    public const string AnnahmeUnterdecke = "ANNAHME Prüfliste (Nachtrag B3e): «Kühldecke» und «Deckenverkleidung» = Unterdecke";
    /// <summary>Vermerk zum Bodenaufbau aus der Wortliste (Nachtrag B3e).</summary>
    public const string AnnahmeBodenaufbau = "ANNAHME Prüfliste (Nachtrag B3e): «Unterlagsboden», «Estrich», «Bodenaufbau», «Trittschall», «screed», «chape» = " +
                                             "Bodenaufbau (Kategorie bodenaufbau: Entscheid Christian 19.10.2026) – «Estrich» heisst in der Schweiz auch Dachboden; " +
                                             "«UB» regelt die Typenliste je Projekt";
    /// <summary>Vermerk zur Luftschicht aus der Wortliste (Nachtrag B3e).</summary>
    public const string EntscheidLuftschicht = "Luftschicht: weder Kollision noch Aussparung noch P20 (Entscheid Christian 19.10.2026 «Luftschichten ignorieren», Nachtrag B3e)";
    /// <summary>Entscheid zum Typnamen als Rückfall (Nachtrag B3d).</summary>
    public const string EntscheidTypname = "Material aus dem Typnamen (Entscheid Christian 19.10.2026, Rückfall ohne aussagekräftige Materialzuordnung)";

    /// <summary>Die Wortliste: Gruppe und Muster (auf den normalisierten Text: klein, ohne Akzente, ß → ss, _ → Leerschlag), in dieser
    /// Reihenfolge geprüft; der erste Treffer gilt. ANNAHME (Prüfliste Christian), soweit nicht als Entscheid vermerkt.</summary>
    public static readonly IReadOnlyList<(Materialgruppe Gruppe, string Muster)> Woerter =
    [
        // Installationsvorwand (Nachtrag B3d; Wörter bestätigt von Christian 19.10.2026, Nachtrag B3e; vor allen Baustoffen, weil die
        // Funktion zählt – Leitungen liegen darin, auch wenn sie beplankt oder gemauert ist)
        (Materialgruppe.Installationsvorwand, @"\bgis\b|vorwand|installationswand"),
        // Mauerwerk (vor Beton: Porenbeton, Betonstein, Kalksandstein sind Steine des Mauerwerks)
        (Materialgruppe.Mauerwerk,
            @"mauerwerk|backstein|(?<!dach)ziegel|kalksand|porenbeton|gasbeton|ytong|siporex|betonstein|zementstein|klinker|bruchstein|lehmstein" +   // de
            @"|masonry|brick|blockwork|calcium[- ]?silicate|sand[- ]?lime|aerated concrete|concrete block" +                                          // en
            @"|maconnerie|brique|silico[- ]?calcaire|beton cellulaire|parpaing|moellon|terre cuite"),                                                // fr
        // Beton (vor Stahl: Stahlbeton)
        (Materialgruppe.Beton, @"beton|concrete"),
        // Unterdecke (Nachtrag B3e, Entscheid Christian 19.10.2026 «Unterdecken wie Bekleidung»; Befund Bernstrasse: als IfcSlab
        // «abgehängte Decke» exportiert): nach Beton und Mauerwerk (Rohbau geht vor), vor den übrigen Baustoffen – die Funktion zählt
        // («Akustikdecke Gips», «Metallkühldecke»). «Kühldecke» und «Deckenverkleidung» ANNAHME (AnnahmeUnterdecke)
        (Materialgruppe.Unterdecke,
            @"unterdecke|abgeh(a|ae)ngt|akustikdecke|decken(be|ver)kleidung|rasterdecke|k(u|ue)hldecke" +
            @"|suspended ceiling|false ceiling" +
            @"|faux[- ]plafond|plafond suspendu"),
        // Bodenaufbau (Nachtrag B3e; Kategorie Entscheid Christian 19.10.2026, Wörter ANNAHME): Schichten über der Tragdecke; nach Beton
        // (Rohbau geht vor), vor der Dämmung (Trittschalldämmung). «UB» bewusst nicht (zu kurz – Typenliste je Projekt)
        (Materialgruppe.Bodenaufbau,
            @"unterlagsboden|estrich|bodenaufbau|trittschall" +
            @"|\bscreed" +
            @"|\bchapes?\b"),
        // Dämmung (vor Holz und Glas: Holzfaserdämmung, Glaswolle, Schaumglas); «ohne Dämmung» zählt nicht (Befund Bernstrasse:
        // Typname «5.1.1_Geschossdecke UB ohne Dämmung»); Trittschalldämmung ist seit Nachtrag B3e Bodenaufbau
        (Materialgruppe.Daemmung,
            @"(?<!ohne )d(a|ae)mm|isolier|mineralwolle|steinwolle|glaswolle|polystyrol|\beps\b|\bxps\b|\bpur\b|\bpir\b|schaumglas|kautschuk|elastomer|schaum" +
            @"|insulation|insulating|mineral ?wool|rock ?wool|glass ?wool|polystyrene|foam" +
            @"|isolant|isolation|laine de roche|laine de verre|laine minerale|mousse"),
        // Leichtbau (Gips, Ständer, Trockenbau)
        (Materialgruppe.Leichtbau,
            @"gips|\bgk[bfi]{0,2}\b|trockenbau|leichtbau|st(a|ae)nder" +
            @"|plasterboard|gypsum|drywall|\bstud|dry ?lining" +
            @"|platre|placo|cloison seche"),
        // Holz
        (Materialgruppe.Holz,
            @"holz|brettschicht|brettsperr|\bbsh\b|\bclt\b|\bosb\b|spanplatte|sperrholz|\bmdf\b|dreischicht" +
            @"|wood|timber|plywood|glulam|lumber" +
            @"|\bbois\b|contreplaque|lamelle[- ]colle"),
        // Stahl und Metall (nach Beton: Stahlbeton)
        (Materialgruppe.Metall,
            @"stahl|metall|alumin|kupfer|\bzink|messing|guss|blech|\bs(235|275|355)\b|\bh-?tr(a|ae)ger" +
            @"|steel|metal|copper|\biron\b" +
            @"|acier|fonte|cuivre"),
        // Ausbau: Bekleidung, Belag, Putz, Abdichtung, Glas, Kunststoff (Unterdecke und Bodenaufbau seit Nachtrag B3e eigene Gruppen)
        (Materialgruppe.Ausbau,
            @"putz|fliese|keramik|parkett|laminat|linoleum|teppich|belag|abdichtung|bitumen|folie|dampfbremse|dampfsperre|membran" +
            @"|glas|kunststoff|\bpvc\b|tapete|verkleidung|bekleidung|dachziegel" +
            @"|plaster|render|\btile|parquet|carpet|flooring|cladding|covering|waterproof|vapou?r|glazing|plastic" +
            @"|enduit|crepi|carrelage|moquette|revetement|etancheite|pare[- ]vapeur|verre|vitrage|plastique|tuile"),
        // Luftschicht (Nachtrag B3e, Entscheid Christian 19.10.2026 «Luftschichten ignorieren», Wörter von Christian): nur als ganzes Wort
        // bzw. Schichtname – «Luft, horizontal (Teil)» ja; «Luftkanal», «Zuluft», «Lüftung», «Luft-Wasser-Wärmepumpe», «Wasser/Luft»,
        // «cavity wall» (Zweischalenmauerwerk) nein
        (Materialgruppe.Luftschicht,
            @"(?<![\w/-])luft(?![\w/-])|\bluftschicht(en)?\b|\bhinterluftung\b" +
            @"|\bair[- ](gap|space)s?\b|\bcavity\b(?![- ]?wall)" +
            @"|\blame d.air\b"),
        // ohne Aussage: Darstellungsmaterialien, Farben, leere Namen, Hohlraum, übrige Namen mit «Luft» (Luftspalt …)
        (Materialgruppe.OhneAussage,
            @"farbe|anstrich|\black|paint|peinture|\bral\b|unnamed|unbenannt|^default$|^standard$|^vorgabe$|nach kategorie|by category|generic|allgemein" +
            @"|luft|hohlraum|\bair\b|cavity|lame d.air|\bvide\b" +
            @"|\b(weiss|schwarz|grau|blau|rot|gelb|grun|braun|white|black|grey|gray|blue|red|yellow|green|brown|blanc|noir|gris|bleu|rouge|jaune|vert)\b"),
    ];

    static readonly (Materialgruppe Gruppe, Regex Muster)[] Kompiliert =
        Woerter.Select(w => (w.Gruppe, new Regex(w.Muster, RegexOptions.CultureInvariant | RegexOptions.Compiled))).ToArray();

    /// <summary>Text für den Vergleich: klein, ohne Akzente (ä → a, é → e), ß → ss, _ → Leerschlag, Leerschläge zusammengefasst.</summary>
    public static string Normalisieren(string text)
    {
        var t = text.Trim().ToLowerInvariant().Replace("ß", "ss").Replace('_', ' ').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var c in t)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }

    /// <summary>Gruppe des ersten passenden Eintrags; null = unbekannt (kein Wort der Liste).</summary>
    public static Materialgruppe? Gruppe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Materialgruppe.OhneAussage;
        var n = Normalisieren(text);
        foreach (var (g, m) in Kompiliert)
            if (m.IsMatch(n)) return g;
        return null;
    }

    /// <summary>Beton oder Mauerwerk – bekommt Aussparungen.</summary>
    public static bool Rohbau(Materialgruppe g) => g is Materialgruppe.Beton or Materialgruppe.Mauerwerk;

    /// <summary>Nennt die Gruppe eine Bauart (Nachtrag B3d, «aussagekräftig», bestätigt B3e)? Alles ausser Ausbau (Oberfläche), ohne
    /// Aussage und – Nachtrag B3e – Unterdecke, Bodenaufbau, Luftschicht (dann zählt der Typname mit: nennt er Beton, gibt es eine
    /// Aussparung).</summary>
    public static bool Bauart(Materialgruppe g) =>
        g is not (Materialgruppe.Ausbau or Materialgruppe.OhneAussage or Materialgruppe.Unterdecke or Materialgruppe.Bodenaufbau or Materialgruppe.Luftschicht);

    static readonly Regex KuehldeckeMuster = new(@"k(u|ue)hldecke|deckenverkleidung", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Trifft der Text ein Wort der Unterdecke, das Christian nicht genannt hat («Kühldecke», «Deckenverkleidung» – ANNAHME)?</summary>
    public static bool UnterdeckeAnnahme(string text) => KuehldeckeMuster.IsMatch(Normalisieren(text));

    public static string Name(Materialgruppe g) => g switch
    {
        Materialgruppe.Installationsvorwand => "Installationsvorwand",
        Materialgruppe.Mauerwerk => "Mauerwerk",
        Materialgruppe.Beton => "Beton",
        Materialgruppe.Unterdecke => "Unterdecke",
        Materialgruppe.Bodenaufbau => "Bodenaufbau",
        Materialgruppe.Luftschicht => "Luftschicht",
        Materialgruppe.Daemmung => "Dämmung",
        Materialgruppe.Leichtbau => "Leichtbau",
        Materialgruppe.Holz => "Holz",
        Materialgruppe.Metall => "Stahl/Metall",
        Materialgruppe.Ausbau => "Bekleidung/Ausbau",
        _ => "ohne Aussage",
    };
}

/// <summary>Herkunft eines Materialbefunds (Nachtrag B3d): woraus die Einteilung stammt.</summary>
public static class Materialherkunft
{
    /// <summary>Typenliste des Projekts (Typmaterial, mit Quelle).</summary>
    public const string Typenliste = "typenliste";
    /// <summary>Materialzuordnung im IFC bzw. material/Aufbau des KICAD-Bauteils.</summary>
    public const string Material = "material";
    /// <summary>Aus dem Typnamen nach der Wortliste (Entscheid Christian 19.10.2026, Rückfall).</summary>
    public const string Typname = "typname";
    /// <summary>Bekleidung nach der IFC-Entität (IfcCovering).</summary>
    public const string Entitaet = "entitaet";
    /// <summary>Material unbekannt.</summary>
    public const string Keine = "keine";
}

/// <summary>Eine Angabe zum Material eines Bauteils: Text (Materialname, Kategorie, Typname) und Fundstelle. NurWennErkannt: zählt nur,
/// wenn ein Wort der Liste darin steht (Typname). Festgelegt: Gruppe aus der Typenliste (keine Wortliste; Nachtrag B3d).</summary>
public sealed record Materialangabe(string Text, string Quelle, bool NurWennErkannt = false, Materialgruppe? Festgelegt = null)
{
    /// <summary>Angabe aus dem Typnamen nach der Wortliste.</summary>
    public bool AusTypname => NurWennErkannt && Festgelegt is null;
    /// <summary>Angabe aus der Typenliste des Projekts.</summary>
    public bool AusTypenliste => Festgelegt is not null;
}

/// <summary>Ergebnis der Einteilung eines Bauteils (Regeln im Dateikopf).</summary>
public sealed class Materialbefund
{
    /// <summary>Angaben mit ihrer Gruppe (null = unbekannt); Typnamen ohne Treffer sind weggelassen.</summary>
    public required IReadOnlyList<(Materialangabe Angabe, Materialgruppe? Gruppe)> Angaben { get; init; }
    /// <summary>Beton oder Mauerwerk erkannt.</summary>
    public bool Rohbau { get; init; }
    /// <summary>Material unbekannt (kein Rohbau erkannt, aber auch nicht alles erkannt bzw. gar nichts erkennbar).</summary>
    public bool Unbekannt { get; init; }
    /// <summary>Hinweis, warum nichts bekannt ist (z. B. Netzdatei ohne Material), sonst null.</summary>
    public string? Hinweis { get; init; }
    /// <summary>Woraus die Einteilung stammt (Materialherkunft: typenliste, material, typname, entitaet, keine).</summary>
    public string Herkunft { get; init; } = Materialherkunft.Material;
    /// <summary>Bekommt das Bauteil Aussparungen (Rohbau oder unbekannt)?</summary>
    public bool Aussparung => Rohbau || Unbekannt;
    /// <summary>Erkannt: wenigstens eine Angabe einer Gruppe mit Aussage.</summary>
    public bool Erkannt => Angaben.Any(a => a.Gruppe is { } g && g != Materialgruppe.OhneAussage);
    /// <summary>Aussagekräftig (Nachtrag B3d): wenigstens eine Angabe nennt eine Bauart (nicht nur Oberfläche bzw. Darstellung).</summary>
    public bool BauartErkannt => Angaben.Any(a => a.Gruppe is { } g && Materialwortliste.Bauart(g));
    /// <summary>Kein Rohbau: keine Aussparung.</summary>
    public bool KeinRohbau => !Aussparung;

    /// <summary>Kann eine Leitung das Bauteil queren (Kollisionssuche.QuerbarNach: Wand, Decke, Dach, Stütze, Träger, Platte, Teil …; nicht
    /// Tür, Fenster, Treppe, Geländer)? Nachtrag B3e: nur dann machen die Wörter der Wortliste es zur Unterdecke, zum Bodenaufbau bzw. zur
    /// Luftschicht (Regel im Dateikopf). Ohne Bauteil (Einteilen direkt): ja.</summary>
    public bool Querbar { get; init; } = true;

    /// <summary>Rang für die Hauptgruppe ohne Rohbau (Nachtrag B3e): Bauart, dann Unterdecke bzw. Bodenaufbau, dann Ausbau, dann Luftschicht.</summary>
    static int Rang(Materialgruppe g) =>
        Materialwortliste.Bauart(g) ? 0 : g is Materialgruppe.Unterdecke or Materialgruppe.Bodenaufbau ? 1 : g == Materialgruppe.Ausbau ? 2 : 3;

    /// <summary>Hauptgruppe für Zählungen und Paare: Beton bzw. Mauerwerk, sonst die erste Bauart, sonst die erste Unterdecke bzw. der
    /// erste Bodenaufbau, sonst Ausbau, sonst Luftschicht (Nachtrag B3e), sonst null (unbekannt).</summary>
    public Materialgruppe? Gruppe => Rohbau
        ? Angaben.Select(a => a.Gruppe).First(g => g is { } x && Materialwortliste.Rohbau(x))
        : Unbekannt ? null
        : Angaben.Select(a => a.Gruppe).Where(g => g is { } x && x != Materialgruppe.OhneAussage).OrderBy(g => Rang(g!.Value)).First();

    /// <summary>Installationsvorwand (Nachtrag B3d): keine Aussparung, Kollision nur im Paar hlks-vorwand.</summary>
    public bool Installationsvorwand => !Rohbau && !Unbekannt && Gruppe == Materialgruppe.Installationsvorwand;

    /// <summary>Gilt eine Unterdecke, ein Bodenaufbau bzw. eine Luftschicht (Nachtrag B3e)? Aus der Typenliste immer, aus der Wortliste nur
    /// für querbare Bauteile.</summary>
    bool Gilt(Materialgruppe g) => !Rohbau && !Unbekannt && Gruppe == g && (Querbar || Herkunft == Materialherkunft.Typenliste);

    /// <summary>Unterdecke (Nachtrag B3e, Entscheid Christian 19.10.2026 «Unterdecken wie Bekleidung»).</summary>
    public bool Unterdecke => Gilt(Materialgruppe.Unterdecke);
    /// <summary>Bodenaufbau (Nachtrag B3e, Kategorie bodenaufbau): keine Aussparung, Kollision nur im Paar hlks-bekleidung.</summary>
    public bool Bodenaufbau => Gilt(Materialgruppe.Bodenaufbau);
    /// <summary>Luftschicht (Nachtrag B3e, Entscheid Christian 19.10.2026 «Luftschichten ignorieren»): weder Kollision noch Aussparung noch
    /// P20, in keinem Paar.</summary>
    public bool Luftschicht => Gilt(Materialgruppe.Luftschicht);

    /// <summary>Zählt in der Kollisionsprüfung als Bekleidung (Paar hlks-bekleidung): IfcCovering bzw. Typenliste «bekleidung» (Nachtrag
    /// B3d), Unterdecke und Bodenaufbau (Nachtrag B3e). Übriges Ausbau-Material allein aus der Wortliste (Putz, Glas …) macht kein Bauteil
    /// zur Bekleidung.</summary>
    public bool AlsBekleidung => !Rohbau && !Unbekannt
                                 && ((Gruppe == Materialgruppe.Ausbau && Herkunft is Materialherkunft.Typenliste or Materialherkunft.Entitaet) || Unterdecke || Bodenaufbau);

    /// <summary>Vermerk zur Wortliste der Hauptgruppe (Nachtrag B3e): Entscheid Christians bzw. ANNAHME für Installationsvorwand, Unterdecke,
    /// Bodenaufbau, Luftschicht; null ohne besonderen Vermerk, bei Rohbau, unbekannt und aus der Typenliste (Festlegung mit Quelle).</summary>
    public string? Vermerk
    {
        get
        {
            if (Herkunft == Materialherkunft.Typenliste || Rohbau || Unbekannt) return null;
            if (Installationsvorwand) return Materialwortliste.EntscheidVorwand;
            if (Unterdecke)
                return Angaben.Any(a => a.Gruppe == Materialgruppe.Unterdecke && Materialwortliste.UnterdeckeAnnahme(a.Angabe.Text))
                    ? $"{Materialwortliste.EntscheidUnterdecke}; {Materialwortliste.AnnahmeUnterdecke}" : Materialwortliste.EntscheidUnterdecke;
            if (Bodenaufbau) return Materialwortliste.AnnahmeBodenaufbau;
            if (Luftschicht) return Materialwortliste.EntscheidLuftschicht;
            return null;
        }
    }

    static string Kurz(Materialangabe a) => a.AusTypenliste ? $"Typenliste «{a.Text}»" : a.AusTypname ? $"aus Typname «{a.Text}»" : a.Text;

    /// <summary>Text für Liste und Element: «Beton: Beton Sichtbeton, hellgrau», «Beton: aus Typname «…»», «Leichtbau: Typenliste «…»»,
    /// «unbekannt: <Unnamed>».</summary>
    public string Text
    {
        get
        {
            if (Rohbau)
                return string.Join(" | ", Angaben.Where(a => a.Gruppe is { } g && Materialwortliste.Rohbau(g))
                    .Select(a => $"{Materialwortliste.Name(a.Gruppe!.Value)}: {Kurz(a.Angabe)}").Distinct());
            if (Unbekannt)
            {
                var namen = Angaben.Where(a => !a.Angabe.NurWennErkannt).Select(a => a.Angabe.Text).Distinct().ToList();
                return namen.Count == 0 ? $"unbekannt: {Hinweis ?? "keine Materialangabe"}" : $"unbekannt: {string.Join(" | ", namen)}";
            }
            return string.Join(" | ", Angaben.Where(a => a.Gruppe is { } g && g != Materialgruppe.OhneAussage)
                .Select(a => $"{Materialwortliste.Name(a.Gruppe!.Value)}: {Kurz(a.Angabe)}").Distinct());
        }
    }

    /// <summary>Fundstellen der Angaben (ohne den Hinweis auf die Wortliste).</summary>
    public string Fundstellen
    {
        get
        {
            var q = Angaben.Select(a => a.Angabe.Quelle).Where(x => x.Length > 0).Distinct().ToList();
            return q.Count == 0 ? (Hinweis ?? "keine Materialangabe") : string.Join("; ", q.Take(6)) + (q.Count > 6 ? $" … ({q.Count})" : "");
        }
    }

    /// <summary>Fundstellen mit Entscheid bzw. Annahme (für die Herkunft): Typenliste = Festlegung mit Quelle (keine Annahme), aus dem
    /// Typnamen = Entscheid Christian und Wortliste, sonst Wortliste; Installationsvorwand, Unterdecke, Bodenaufbau, Luftschicht aus der
    /// Wortliste mit ihrem Vermerk (Entscheid bzw. ANNAHME, Nachtrag B3e).</summary>
    public string Quelle
    {
        get
        {
            if (Herkunft == Materialherkunft.Typenliste) return Fundstellen;
            var s = Herkunft == Materialherkunft.Typname ? $"{Fundstellen} – {Materialwortliste.EntscheidTypname}; {Materialwortliste.Annahme}"
                : $"{Fundstellen} – {Materialwortliste.Annahme}";
            return Vermerk is { } v ? $"{s}; {v}" : s;
        }
    }

    /// <summary>Derselbe Befund mit der Angabe, ob das Bauteil querbar ist (Bauteilmaterial.Von).</summary>
    internal Materialbefund MitQuerbar(bool querbar) => querbar == Querbar ? this
        : new Materialbefund { Angaben = Angaben, Rohbau = Rohbau, Unbekannt = Unbekannt, Hinweis = Hinweis, Herkunft = Herkunft, Querbar = querbar };

    /// <summary>Einteilen (Regeln im Dateikopf). hinweis: Grund, wenn es keine Angaben gibt; herkunft: nur für Sonderfälle (Bekleidung
    /// nach Entität), sonst aus den Angaben bestimmt.</summary>
    public static Materialbefund Einteilen(IEnumerable<Materialangabe> angaben, string? hinweis = null, string? herkunft = null)
    {
        var liste = new List<(Materialangabe, Materialgruppe?)>();
        foreach (var a in angaben)
        {
            var g = a.Festgelegt ?? Materialwortliste.Gruppe(a.Text);
            if (a.AusTypname && (g is null || g == Materialgruppe.OhneAussage)) continue;
            liste.Add((a, g));
        }
        bool rohbau = liste.Any(x => x.Item2 is { } g && Materialwortliste.Rohbau(g));
        bool erkannt = liste.Any(x => x.Item2 is { } g && g != Materialgruppe.OhneAussage);
        bool unbekannt = !rohbau && (!erkannt || liste.Any(x => x.Item2 is null));
        var b = new Materialbefund { Angaben = liste, Rohbau = rohbau, Unbekannt = unbekannt, Hinweis = hinweis };
        return new Materialbefund
        {
            Angaben = liste, Rohbau = rohbau, Unbekannt = unbekannt, Hinweis = hinweis,
            Herkunft = herkunft ?? (liste.Any(x => x.Item1.AusTypenliste) ? Materialherkunft.Typenliste
                : unbekannt ? Materialherkunft.Keine
                // die entscheidende Gruppe nennt nur der Typname → aus dem Typnamen
                : liste.Any(x => !x.Item1.AusTypname && x.Item2 == b.Gruppe) ? Materialherkunft.Material : Materialherkunft.Typname),
        };
    }

    /// <summary>Befund aus einem Eintrag der Typenliste (Nachtrag B3d): die Kategorie gilt, ohne Wortliste.</summary>
    public static Materialbefund AusTypenliste(Typeneintrag eintrag, string typname) =>
        Einteilen([new Materialangabe(typname, eintrag.Fundstelle, Festgelegt: eintrag.Gruppe)]);
}

/// <summary>Was über ein Bauteil zum Material bekannt ist (Prüfkörper, Produkt einer Netzdatei, KICAD-Element) – Nachtrag B3d, damit
/// Prüfkörper, Kollisionsliste und Typenliste dieselbe Einteilung bekommen.</summary>
public sealed record Bauteilangaben(Element? Element, IReadOnlyList<string> Materialien, string? Typname, string? Materialquelle, int? NetzFassung,
    bool Bekleidung, string? Entitaet)
{
    /// <summary>Angaben eines Prüfkörpers (Element, sonst Hintergrund-Produkt).</summary>
    public static Bauteilangaben Von(Modell m, Pruefkoerper pk) =>
        new(pk.ElementId is { } id ? m.Finde(id) : null, pk.Materialien, pk.Typname, pk.Materialquelle, pk.NetzFassung, pk.Bekleidung, pk.Entitaet);

    /// <summary>Angaben eines Produkts der Netzdatei (Hintergrund) bzw. – mit element – eines daraus übernommenen Elements.</summary>
    public static Bauteilangaben Von(Katalog k, IfcNetzProdukt? produkt, int? fassung, Element? element = null) =>
        new(element, produkt?.Materialien ?? [], produkt?.Typname, produkt?.Materialquelle, produkt is null ? null : fassung,
            (element is not null && Pruefkoerperbau.IstBekleidung(k, element)) || Pruefkoerperbau.IstBekleidung(produkt?.Entitaet), produkt?.Entitaet);
}

/// <summary>Material eines gequerten Bauteils (Prüfkörper) aus Typenliste, KICAD bzw. der Netzdatei (Regeln im Dateikopf).</summary>
public static class Bauteilmaterial
{
    /// <summary>Material eines Bauteils (Element oder Hintergrund-Produkt). Bekleidungen (IfcCovering) sind Ausbau. liste: Typenliste
    /// (null = die des Modells, zwischengespeichert).</summary>
    public static Materialbefund Von(Modell m, Katalog k, Pruefkoerper pk, Typenliste? liste = null) => Von(m, k, Bauteilangaben.Von(m, pk), liste);

    /// <summary>Material eines Bauteils aus seinen Angaben (Vorrang: Typenliste > Materialzuordnung > Typname > unbekannt), mit der Angabe,
    /// ob das Bauteil querbar ist (Nachtrag B3e: Element nach seiner Katalogklasse, sonst nach der IFC-Entität; ohne beides: ja).</summary>
    public static Materialbefund Von(Modell m, Katalog k, Bauteilangaben b, Typenliste? liste = null)
    {
        bool querbar = b.Element is { } e ? Kollisionssuche.QuerbarNach(k, e.Klasse, null)
            : b.Entitaet is not { Length: > 0 } || Kollisionssuche.QuerbarNach(k, null, b.Entitaet);
        return Bestimmen(m, k, b, liste).MitQuerbar(querbar);
    }

    static Materialbefund Bestimmen(Modell m, Katalog k, Bauteilangaben b, Typenliste? liste)
    {
        if (b.Bekleidung)
            return Materialbefund.Einteilen([new Materialangabe("Bekleidung", b.Entitaet ?? "Bekleidung")], herkunft: Materialherkunft.Entitaet);
        // 1. Typenliste des Projekts
        liste ??= Typenliste.Von(m);
        if (b.Typname is { Length: > 0 } typ && liste.Finde(typ) is { } eintrag) return Materialbefund.AusTypenliste(eintrag, typ);
        // 2. Materialzuordnung (KICAD, Netzdatei)
        var angaben = new List<Materialangabe>();
        string hinweis;
        if (b.Element is { } e)
        {
            var kicad = AusElement(m, k, e);
            var befund = Materialbefund.Einteilen(kicad, kicad.Count == 0 ? $"{e.Kennzeichen} ohne material und Aufbau" : null);
            if (befund.BauartErkannt || b.NetzFassung is null) return befund;
            // übernommenes Bauteil ohne erkennbare Bauart in KICAD: dazu das Material der Netzdatei
            angaben.AddRange(kicad);
            hinweis = NetzHinweis(b.NetzFassung) ?? $"{e.Kennzeichen} ohne material und Aufbau, keine Materialzuordnung im IFC";
        }
        else hinweis = NetzHinweis(b.NetzFassung) ?? "keine Materialzuordnung im IFC";
        angaben.AddRange(AusNetzMaterial(b));
        var ifc = Materialbefund.Einteilen(angaben, hinweis);
        if (ifc.BauartErkannt || b.Typname is not { Length: > 0 } t) return ifc;
        // 3. Rückfall Typname (Entscheid Christian 19.10.2026) – zählt nur mit einem Wort der Liste
        return Materialbefund.Einteilen([.. angaben, new Materialangabe(t, "IFC Typname", NurWennErkannt: true)], hinweis);
    }

    /// <summary>Netzdatei älter als Fassung 3: kein Material darin.</summary>
    static string? NetzHinweis(int? fassung) => fassung is int f && f < IfcNetzErzeuger.FassungMaterial
        ? $"Netzdatei Fassung {f} ohne Material – die IFC-Datei erneut übernehmen (Fassung {IfcNetzErzeuger.Fassung})"
        : null;

    /// <summary>Angaben der Netzdatei: Materialnamen und (nur wenn erkannt) der Typname.</summary>
    public static List<Materialangabe> AusNetz(Pruefkoerper pk)
    {
        var aus = AusNetzMaterial(new Bauteilangaben(null, pk.Materialien, pk.Typname, pk.Materialquelle, pk.NetzFassung, pk.Bekleidung, pk.Entitaet));
        if (pk.Typname is { Length: > 0 } t) aus.Add(new Materialangabe(t, "IFC Typname", NurWennErkannt: true));
        return aus;
    }

    /// <summary>Materialnamen der Netzdatei (ohne Typname).</summary>
    static List<Materialangabe> AusNetzMaterial(Bauteilangaben b)
    {
        var q = b.Materialquelle is { Length: > 0 } mq ? $"IFC {mq}" : "IFC";
        return b.Materialien.Select(n => new Materialangabe(n, q)).ToList();
    }

    /// <summary>Angaben eines KICAD-Bauteils (Modul 1c): material, Aufbau (Material-Elemente: Name und kategorie), Kurzbeschreibung aufbau.</summary>
    public static List<Materialangabe> AusElement(Modell m, Katalog k, Element e)
    {
        var aus = new List<Materialangabe>();
        if (e.Text("material") is { Length: > 0 } mat && !mat.Equals("unbestimmt", StringComparison.OrdinalIgnoreCase))
            aus.Add(new Materialangabe(mat, $"{e.Kennzeichen}.material"));
        if (e.Bezuege.GetValueOrDefault("aufbau") is { } aid && m.Finde(aid) is { } auf && k.IstArt(auf, "Aufbau"))
        {
            int i = 0;
            foreach (var s in HuelleHilfe.Schichten(auf))
            {
                i++;
                if (s.Material is null || m.Finde(s.Material) is not { } me) continue;
                var text = me.Name is { Length: > 0 } n ? (me.Text("kategorie") is { Length: > 0 } kat && !n.Contains(kat, StringComparison.OrdinalIgnoreCase) ? $"{n} ({kat})" : n)
                    : me.Text("kategorie") ?? me.Kennzeichen;
                aus.Add(new Materialangabe(text, $"Aufbau {auf.Kennzeichen}, Schicht {i}: {me.Kennzeichen}"));
            }
        }
        if (e.Text("aufbau") is { Length: > 0 } kurz)   // «Beton 150 / Wärmedämmung 50» (Trennzeichen « / » wie der IFC-Import)
            foreach (var teil in kurz.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                aus.Add(new Materialangabe(teil, $"{e.Kennzeichen}.aufbau"));
        return aus;
    }
}
