// Kollisionsprüfung (Modul 3b, B2): Prüflauf kollision_pruefen über dem Geometrie-Kern aus B1/B1b (Prüfkörper, Hüllquader-Baum,
// Schmalphase, Durchgänge, Öffnungen der Netzdatei Fassung 2). entwurf/modul-3b.md, Grundsatzentscheide 2–5.
//
// Paare: hlks-architektur (HLKS-Elemente gegen Bauteile – KICAD-Elemente, übernommene IFC-Bauteile, Hintergrund-Produkte mit
//   Architektur-Entität – ohne Bekleidungen), hlks-bekleidung (HLKS-Elemente gegen Bekleidungen IfcCovering, Nachtrag B3c),
//   hlks-hlks (HLKS-Elemente untereinander), hlks-fremd (HLKS-Elemente gegen Hintergrund-Produkte, die keine Architektur sind –
//   fremde Fachplaner-IFC; ANNAHME: ohne Produkte aus derselben IFC-Datei wie das HLKS-Element). Vorgabe: hlks-architektur und
//   hlks-hlks. «unterlagen» beschränkt Architektur, Bekleidung und Fremd auf diese IFC-Unterlagen (in KICAD gezeichnete Bauteile
//   zählen immer), «geschoss» die HLKS-Seite auf ein Geschoss.
//
// Nachtrag B3c (Entscheid Christian 19.10.2026):
//   - Bekleidungen nicht melden: Bekleidungen (Pruefkoerper.Bekleidung: IfcCovering – Unterdecken, Verkleidungen, Bodenbeläge,
//     Dämmungen, Dachhaut; Bernstrasse 42: 429 von 2964 Kollisionen, alle Kontakt) gehören nicht mehr zu hlks-architektur, sondern
//     zum zuschaltbaren Paar hlks-bekleidung (nicht in der Vorgabe). Bestehende Kollisionen mit Bekleidungen (ältere Projekte)
//     behandelt ein Lauf ohne hlks-bekleidung wie jede Kollision eines nicht gewählten Paars: «ausserhalb dieses Laufs», unverändert
//     (Status, Kommentar, BCF-Bezug bleiben; wer das Paar wieder zuschaltet, findet sie mit ihrem Status wieder). Die Antwort nennt
//     ihre Zahl; die Kollisionsliste zeigt je Zeile das Paar (filterbar).
//   - Keine Abstandsprüfung: ohne abstand_weich und abstand_leitungen bleibt die weiche Prüfung still aus (in der Zusammenfassung
//     nur «Abstandsprüfung aus»); sind Werte gesetzt, prüft sie wie bisher.
//   - Durchdringungen tragen das Material des gequerten Bauteils (Kollision.material, rohbau ja/nein/unbekannt nach der Wortliste,
//     AussparungMaterial.cs) – P20 meldet eine fehlende Aussparung nur, wenn das Bauteil eine braucht (Beton, Mauerwerk, unbekannt).
//
// Nachtrag B3d (Entscheide Christian 19.10.2026 «Material»; Typenliste: Typenliste.cs, Vorrang: AussparungMaterial.cs):
//   - Installationsvorwände (Typenliste «installationsvorwand» bzw. Typname nach der Wortliste, ANNAHME) zählen wie eine Bekleidung:
//     Leitungen in bzw. durch die Vorwand sind kein Befund (dort liegen die Installationen). Sie gehören nicht zu hlks-architektur,
//     sondern zum eigenen, zuschaltbaren Paar hlks-vorwand (nicht in der Vorgabe). Begründung für ein eigenes Paar statt
//     hlks-bekleidung: die beiden werden aus verschiedenen Gründen zugeschaltet – Bekleidungen, um Leitungen gegen Unterdecken und
//     Verkleidungen zu prüfen (echte Konflikte mit dem Ausbau), Vorwände nur, um zu sehen, was in der Vorwand liegt (dort sind Leitungen
//     gewollt); zusammen würde das eine das andere zuschütten. Mit dem Paar: Durchdringung mit rohbau nein (kein P20).
//   - Typenliste «bekleidung»: das Bauteil zählt wie IfcCovering zum Paar hlks-bekleidung (Entscheid Christian «Bekleidungen nicht melden»;
//     ein Ausbau-Material allein aus der Wortliste macht kein Bauteil zur Bekleidung).
//   - Alte Kollisionen mit Vorwänden bzw. Bekleidungen bleiben bei einem Lauf ohne das Paar «ausserhalb» und unverändert (wie B3c);
//     die Antwort nennt ihre Zahl, die Kollisionsliste zeigt das Paar je Zeile.
//   - Material und rohbau der Durchdringungen werden bei jedem Lauf auch AUSSERHALB seines Umfangs nachgeführt (Status bleibt), wenn das
//     Bauteil einen Prüfkörper hat: das Material gehört zum Bauteil, nicht zum Lauf – sonst meldete P20 nach einer neuen Typenliste
//     weiter Durchdringungen von Vorwänden, die kein Lauf mehr anfasst.
//
// Nachtrag B3e (Entscheide Christian 19.10.2026 zu B3d, Fragefenster; Einteilung: AussparungMaterial.cs):
//   - Unterdecken (Wortliste, z. B. Revit-IfcSlab «abgehängte Decke») und Bodenaufbau (Typenliste bodenaufbau bzw. Wortliste: Unterlags-
//     boden, Estrich, Trittschalldämmung) zählen wie Bekleidungen (Materialbefund.AlsBekleidung): nur im Paar hlks-bekleidung; alte
//     Kollisionen damit bleiben bei einem Lauf ohne das Paar «ausserhalb» und unverändert (wie B3c).
//   - Luftschichten (Typenliste luftschicht bzw. Wortliste: «Luft, horizontal (Teil)» …) gehören in KEIN Paar: weder Kollision noch
//     Aussparung noch P20. Ältere Kollisionen mit einer Luftschicht (vor B3e im Paar hlks-architektur gefunden) gelten weiter als
//     hlks-architektur (PaarVon, Kollisionsliste): ein Lauf mit hlks-architektur findet sie nicht mehr und setzt sie «behoben» (Grund in
//     der Herkunft des Status, Zahl in den Hinweisen); akzeptierte bleiben. Begründung: «ausserhalb» blieben sie für immer «neu» in der
//     Liste, obwohl kein Lauf sie je wieder prüft.
//   - Wortliste nur für querbare Bauteile (Materialbefund.Querbar, QuerbarNach): Türen, Fenster, Treppen, Geländer bleiben in
//     hlks-architektur, auch wenn ihr Name ein Wort der Unterdecke, des Bodenaufbaus bzw. der Luftschicht trägt (ANNAHME Prüfliste).
//
// REGELN je Paar (Breitphase: Hüllquader mit Dämmung, um den grössten weichen Abstand erweitert):
//   Keine Kollision:
//     - Teile desselben Elements (dasselbe Element, Stücke desselben IFC-Produkts).
//     - Verbundene Elemente: direkt verbunden (Verbindung des Modells), oder beide an demselben Formteil bzw. derselben Armatur
//       (Rohr – Bogen – Rohr, Rohr – Ventil – Heizkörper); zwei Elemente an demselben weiteren Element (z. B. zwei Leitungen am
//       selben Gerät), wenn der Befund am Leitungsende liegt (Kontakt.AmEnde).
//     - Leitung in einer Architektenöffnung bzw. zugeordneten Aussparung: alle Durchgänge ihrer Achse durch das Bauteil sind mit
//       dem ganzen Querschnitt samt Dämmung gedeckt (Oeffnungsdeckung). Dann auch keine weiche Kollision.
//   Hart: Kontakt ohne Dämmung (Schnitt, Enthalten, Umschliesst) mit Eindringtiefe > toleranz_hart. Fehlt toleranz_hart, zählt jede
//     Durchdringung (Hinweis). Schnitt ohne messbare Tiefe (ein Netz ohne geschlossene Teile) zählt immer, Mass 0. Schnitt mit
//     Tiefe 0 zwischen geschlossenen Netzen ist eine streifende Berührung: Mass 0, mit toleranz_hart nicht gemeldet (Bernstrasse:
//     394 Formteil–Decke-Fälle, in keinem liegt eine Ecke im anderen Netz).
//     Durchdringung: Leitung quert ein Bauteil (Wand, Decke, Dach, Stütze, Träger bzw. IfcWall, IfcSlab, IfcRoof, IfcColumn,
//     IfcBeam, IfcMember, IfcPlate, IfcFooting, IfcPile, IfcChimney, IfcBuildingElementPart) – je nicht gedecktem Durchgang mit
//     Ein- oder Austrittsfläche eine Kollision typ durchdringung, bauteil = Kennung des Bauteils, Lage = Mitte des Durchgangs
//     (ANNAHME: statt Kontakt.Ort, damit die Lage die Achse im Bauteil ist – Grundlage für B3 und P20). Netze, die nicht
//     wasserdicht sind (Revit-Verknüpfung der Bernstrasse): Querung über die Durchstosspunkte der Achse (OffeneQuerungen).
//     Alles andere typ kontakt, Lage = Kontakt.Ort (Leitung ganz im Bauteil, Formteil/Gerät im Bauteil, Leitung an der
//     Oberfläche, HLKS gegen HLKS, Bekleidungen IfcCovering, Geländer, Treppen, Fenster, Türen).
//   Weich (wenn nicht hart): Abstand MIT Dämmungshülle der Leitungen kleiner als die Vorgabe – Leitung–Leitung abstand_leitungen,
//     sonst abstand_weich je Gewerkpaar (Projektvorgaben.AbstandWeichFuer; Bekleidungen wie Architektur). Mass = Vorgabe − Abstand;
//     überschneiden sich die Dämmungshüllen, ist der Abstand −Eindringtiefe (Mass = Vorgabe + Überschneidung). Fehlt die Vorgabe,
//     entfällt die weiche Prüfung für dieses Paar; sind einzelne Werte gesetzt, nennt die Antwort die fehlenden.
//
// Kollision-Elemente (Grundsatzentscheid 4): beteiligte = Kennungen der Prüfkörper (Element-Id bzw. «Unterlage-Id:GlobalId»),
//   sortiert; kennung = «<beteiligte mit | verbunden>@<x>,<y>,<z>» mit der Lage auf 100 mm gerundet (mm). Ein Befund gehört zu
//   einer bestehenden Kollision mit gleicher Kennung oder – bei denselben Beteiligten – mit gespeicherter Lage näher als 50 mm
//   (halbes Raster; sonst würde eine kleine Verschiebung über eine Rastergrenze eine neue Kennung ergeben); sie behält ihre Kennung.
//   Statuslogik: neu gefunden → neu; wieder gefunden → Status bleibt (behoben → neu, Hinweis); nicht mehr gefunden → behoben (nicht
//   gelöscht), ausser akzeptiert (bleibt). «Nicht mehr gefunden» gilt nur im Umfang des Laufs (Paar, Geschoss, Unterlagen, weiche
//   Prüfung mit Vorgabe); Kollisionen ohne kennung (von Hand angelegt) führt der Lauf nicht nach.
//   pruefung = der Lauf, der die Kollision ZUERST gefunden hat: «JJJJ-MM-TT hh:mm · Lauf n · Paare». Ergänzt wird sie nur bei einem
//   Statuswechsel durch einen Lauf: «· nicht mehr gefunden in Lauf n (JJJJ-MM-TT hh:mm)» (→ behoben) bzw. «· wieder gefunden in
//   Lauf n (JJJJ-MM-TT hh:mm)» (behoben → neu). Ein wiedergefundener Befund mit unverändertem Status und unveränderten Daten wird
//   nicht neu geschrieben (Massstab: Json.Text des Elements vor und nach dem Nachführen) – vorher trug pruefung den letzten Lauf und
//   jeder Lauf schrieb alle Kollisionen neu (Bernstrasse: ~3400 Operationen je Paket ohne Änderung). Ändern sich Daten (Mass, Lage,
//   Art, Typ, Name, Geschoss), wird die Kollision geschrieben, pruefung bleibt. Fehlt pruefung bei einer wiedergefundenen Kollision,
//   setzt der Lauf sie auf sich selbst (ANNAHME: der früheste bekannte Lauf).
//   Zähler: Projektinformation.kollisionslauf (erstes Element der Klasse nach Kennzeichen; fehlt es, legt der Lauf eines an wie die
//   übrigen Befehle) = «JJJJ-MM-TT hh:mm · Lauf n · Paare» des letzten Laufs. Jeder Lauf setzt sie – eine Operation; ein Lauf ohne
//   Änderung an Kollisionen schreibt nur sie. Lauf-Nummer = grösste Nummer aus kollisionslauf und den pruefung-Texten + 1 (zählt also
//   weiter, auch wenn ein Lauf keine Kollision schreibt; ältere Projekte ohne kollisionslauf zählen aus pruefung weiter). Hinweis: die
//   geänderte Projektinformation löst die Nachführung der Hülle aus (HuelleNachfuehren: R/U aller Aufbauten werden nachgerechnet,
//   geschrieben nur bei Abweichung). Geschoss nach der Höhe der Lage (wie Elementpflege.Zuordnen), sonst das Geschoss des
//   HLKS-Elements. Ein Lauf ist ein Befehl und damit ein Paket.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static class KollisionTexte
{
    public const string HlksArchitektur = "hlks-architektur";
    /// <summary>HLKS gegen Bekleidungen (IfcCovering) – zuschaltbar, nicht in der Vorgabe (Nachtrag B3c, Entscheid Christian 19.10.2026).</summary>
    public const string HlksBekleidung = "hlks-bekleidung";
    /// <summary>HLKS gegen Installationsvorwände (Typenliste bzw. Typname) – zuschaltbar, nicht in der Vorgabe (Nachtrag B3d, Entscheid
    /// Christian 19.10.2026: Leitungen in bzw. durch die Vorwand sind kein Befund).</summary>
    public const string HlksVorwand = "hlks-vorwand";
    public const string HlksHlks = "hlks-hlks";
    public const string HlksFremd = "hlks-fremd";
    public static readonly string[] Paare = [HlksArchitektur, HlksBekleidung, HlksVorwand, HlksHlks, HlksFremd];
    public static readonly string[] PaareVorgabe = [HlksArchitektur, HlksHlks];

    public const string Pruefen = "Prüflauf der Kollisionsprüfung: HLKS gegen Architektur/Tragwerk, HLKS untereinander, auf Wunsch gegen Bekleidungen (hlks-bekleidung: " +
        "IfcCovering – Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen, Dachhaut –, Typen der Typenliste mit Kategorie bekleidung bzw. bodenaufbau, Unterdecken und " +
        "Bodenaufbau nach dem Typnamen, z. B. «abgehängte Decke», Unterlagsboden, Estrich, Trittschalldämmung; ohne dieses Paar nicht gemeldet), " +
        "gegen Installationsvorwände (hlks-vorwand: Typenliste installationsvorwand bzw. Typname mit GIS/Vorwand; ohne dieses Paar nicht gemeldet, weil dort die " +
        "Leitungen liegen) und fremde IFC-Modelle (hlks-fremd). Luftschichten (Typenliste luftschicht bzw. «Luft», «Luftschicht», «Hinterlüftung») werden nie geprüft. " +
        "Hart = Durchdringung ohne Dämmung über toleranz_hart (fehlt sie: jede); Durchdringung einer Wand/Decke/Stütze/eines Trägers durch eine Leitung = typ durchdringung " +
        "mit dem Material des Bauteils (ausser die Leitung liegt mit Dämmung ganz in einer Architektenöffnung oder einer ihr zugeordneten Aussparung). Keine Abstandsprüfung " +
        "(Entscheid Christian 19.10.2026) – nur wenn abstand_leitungen bzw. abstand_weich gesetzt sind: weich = Abstand mit Dämmung darunter. Verbundene Elemente sind keine " +
        "Kollision. Jede Kollision wird ein Element KOL-n mit stabiler Kennung (Beteiligte + Lage auf 100 mm): neu gefunden «neu», wieder gefunden Status bleibt, nicht mehr " +
        "gefunden «behoben» (akzeptiert bleibt; Kollisionen nicht gewählter Paare bleiben unverändert). Ein Lauf = ein Paket, rückgängig machbar; zuerst als Trockenlauf " +
        "ansehen (Zahlen je Art und Paar in den Hinweisen).";
    public const string ParPaare = "Liste aus hlks-architektur, hlks-bekleidung, hlks-vorwand, hlks-hlks, hlks-fremd; Vorgabe [\"hlks-architektur\", \"hlks-hlks\"] – " +
        "Bekleidungen (IfcCovering, Typenliste bekleidung bzw. bodenaufbau, Unterdecken und Bodenaufbau nach Typname) nur mit hlks-bekleidung, Installationsvorwände " +
        "nur mit hlks-vorwand, Luftschichten nie";
    public const string ParUnterlagen = "Nur diese IFC-Unterlagen (Kennzeichen oder Id) als Architektur, Bekleidung, Vorwand bzw. Fremd; in KICAD gezeichnete Bauteile zählen immer; Vorgabe alle";
    public const string ParGeschoss = "Nur HLKS-Elemente dieses Geschosses (Kennzeichen); Vorgabe alle";
    public const string ParWeich = "Weiche Prüfung (Abstände aus den Projektvorgaben); Vorgabe true – ohne abstand_leitungen und abstand_weich ist die Abstandsprüfung aus " +
        "(Entscheid Christian 19.10.2026)";

    public const string Status = "Setzt den Status von Kollisionen (neu, bestaetigt, akzeptiert, erledigt, behoben) und auf Wunsch den eigenen Kommentar " +
        "(ersetzt ihn; aus BCF übernommene Kommentare bleiben). akzeptiert bleibt über Prüfläufe, alles andere wird «behoben», wenn der Lauf die Kollision nicht mehr findet.";
    public const string ParKollisionen = "Kollisionen (Kennzeichen oder Id), z. B. [\"KOL-1\", \"KOL-4\"]";
    public const string ParStatus = "neu | bestaetigt | akzeptiert | erledigt | behoben";
    public const string ParKommentar = "Eigener Kommentar (ersetzt den bisherigen eigenen Text; leer entfernt ihn)";

    public const string Liste = "Kollisionsliste (auch als Ansicht liste = kollisionen): je Kollision Kennzeichen, Art (hart/weich), Typ (kontakt/durchdringung), Paar " +
        "(hlks-architektur, hlks-bekleidung, hlks-vorwand, hlks-hlks, hlks-fremd), Beteiligte mit Kennzeichen bzw. IFC-Entität, Lage (mm), Geschoss, Mass (mm), Status, Kennung, " +
        "Prüflauf, bei Durchdringungen das Material des Bauteils mit Herkunft (Typenliste, aus Typname) und rohbau ja/nein/unbekannt; dazu Zahlen je Status, Art, Typ, Paar und Geschoss.";
    public const string ParListePaar = "Nur diese Paare (auch mehrere durch Komma getrennt): hlks-architektur, hlks-bekleidung, hlks-vorwand, hlks-hlks, hlks-fremd";
    public const string ParListeStatus = "Nur dieser Status (auch mehrere durch Komma getrennt)";
    public const string ParListeArt = "hart | weich";
    public const string ParListeTyp = "kontakt | durchdringung";
    public const string ParListeGeschoss = "Nur dieses Geschoss (Kennzeichen)";
    public const string ParListeElement = "Nur Kollisionen mit diesem Beteiligten (Kennzeichen, Id oder IFC-GlobalId)";
}

/// <summary>Paar eines Architektur-Bauteils (Nachtrag B3d) – eine Regel für Prüflauf und Kollisionsliste.</summary>
public static class Kollisionspaar
{
    /// <summary>Bekleidung (IfcCovering bzw. Bekleidungsklasse, Typenliste «bekleidung»; Unterdecke, Bodenaufbau – Nachtrag B3e) →
    /// hlks-bekleidung; Installationsvorwand (Typenliste bzw. Typname) → hlks-vorwand; sonst hlks-architektur – auch eine Luftschicht
    /// (Nachtrag B3e: sie wird in keinem Paar geprüft; ältere Kollisionen damit stammen aus hlks-architektur).</summary>
    public static string Architektur(bool bekleidung, Materialbefund? mb) =>
        bekleidung || mb?.AlsBekleidung == true ? KollisionTexte.HlksBekleidung
        : mb?.Installationsvorwand == true ? KollisionTexte.HlksVorwand
        : KollisionTexte.HlksArchitektur;
}

/// <summary>Kennung einer Kollision aus Beteiligten und gerundeter Lage.</summary>
public static class Kollisionskennung
{
    /// <summary>Rundungsraster der Lage (mm).</summary>
    public const double Raster = 100;
    /// <summary>Fang: ein Befund mit denselben Beteiligten näher als dieser Abstand an der gespeicherten Lage ist dieselbe Kollision.</summary>
    public const double Fang = Raster / 2;

    public static List<string> Sortiert(IEnumerable<string> beteiligte) => beteiligte.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    public static string Schluessel(IEnumerable<string> beteiligte) => string.Join("|", Sortiert(beteiligte));

    public static string Bilden(IEnumerable<string> beteiligte, V3 lage)
    {
        static long R(double x) => (long)Math.Round(x / Raster, MidpointRounding.AwayFromZero) * (long)Raster;
        return string.Create(CultureInfo.InvariantCulture, $"{Schluessel(beteiligte)}@{R(lage.X)},{R(lage.Y)},{R(lage.Z)}");
    }
}

/// <summary>Ein Befund des Prüflaufs (noch kein Element).</summary>
public sealed record Kollisionsbefund(Pruefkoerper Hlks, Pruefkoerper Gegen, string Paar, string Art, string Typ, string? Bauteil, V3 Ort,
    double Mass, string Massquelle)
{
    public List<string> Beteiligte => Kollisionskennung.Sortiert([Hlks.Kennung, Gegen.Kennung]);
    public string Schluessel => Kollisionskennung.Schluessel([Hlks.Kennung, Gegen.Kennung]);
    public string Kennung => Kollisionskennung.Bilden([Hlks.Kennung, Gegen.Kennung], Ort);
}

/// <summary>Auftrag eines Prüflaufs.</summary>
public sealed record Kollisionsauftrag(IReadOnlyList<string> Paare, IReadOnlySet<string>? Unterlagen, string? Geschoss, bool Weich)
{
    public static Kollisionsauftrag Lesen(Modell m, Katalog k, JsonObject p)
    {
        var paare = new List<string>();
        foreach (var x in Liste(p, "paare") ?? [.. KollisionTexte.PaareVorgabe])
        {
            var n = x.Trim().ToLowerInvariant();
            var t = n.Split('-', StringSplitOptions.TrimEntries);
            if (t.Length == 2 && t[1] == "hlks" && t[0] != "hlks") n = $"hlks-{t[0]}";   // architektur-hlks = hlks-architektur
            if (!KollisionTexte.Paare.Contains(n))
                throw new HlksFehler($"Paar «{x}» gibt es nicht.", "paare: " + string.Join(", ", KollisionTexte.Paare));
            if (!paare.Contains(n)) paare.Add(n);
        }
        if (paare.Count == 0) throw new HlksFehler("Parameter «paare» ist leer.", "paare z. B. [\"hlks-architektur\", \"hlks-hlks\"] oder weglassen (Vorgabe).");
        HashSet<string>? unterlagen = null;
        if (Liste(p, "unterlagen") is { } ul)
        {
            unterlagen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var u in ul)
            {
                var e = m.Hole(u, "Unterlage");
                if (!k.IstArt(e, "Unterlage") || !string.Equals(e.Text("art"), "ifc", StringComparison.OrdinalIgnoreCase))
                    throw new HlksFehler($"{e.Kennzeichen} ist keine IFC-Unterlage.", "In unterlagen nur IFC-Unterlagen angeben (elemente_suchen mit klasse Unterlage).");
                unterlagen.Add(e.Id);
            }
        }
        var geschoss = Param.Text(p, "geschoss") is { Length: > 0 } g ? m.HoleDerKlasse(g, "Geschoss").Id : null;
        return new([.. KollisionTexte.Paare.Where(paare.Contains)], unterlagen, geschoss, Param.Bool(p, "weich", true));
    }

    /// <summary>Liste aus einem JSON-Array oder einem Text (durch Komma getrennt); null = Parameter fehlt.</summary>
    public static List<string>? Liste(JsonObject par, string name)
    {
        var n = par[name];
        if (n is null) return null;
        if (n is JsonArray a)
            return a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new HlksFehler($"Parameter «{name}» muss eine Liste von Texten sein."))
                .Where(s => s.Trim().Length > 0).Select(s => s.Trim()).ToList();
        if (n is JsonValue t && t.TryGetValue<string>(out var text))
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        throw new HlksFehler($"Parameter «{name}» muss eine Liste sein.");
    }
}

/// <summary>Der geometrische Teil eines Prüflaufs: Prüfkörper, Paare, Befunde und Zähler – ändert das Modell nicht.</summary>
public sealed class Kollisionssuche
{
    const double Tol = Geometrie3.Toleranz;

    /// <summary>Bauteil-Entitäten, deren Querung durch eine Leitung typ durchdringung ist (Hintergrund-Produkte; Elemente nach Klasse).</summary>
    public static readonly IReadOnlySet<string> QuerbareEntitaeten = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "IfcWall", "IfcWallStandardCase", "IfcWallElementedCase", "IfcSlab", "IfcSlabStandardCase", "IfcSlabElementedCase", "IfcRoof",
        "IfcColumn", "IfcColumnStandardCase", "IfcBeam", "IfcBeamStandardCase", "IfcMember", "IfcMemberStandardCase", "IfcPlate",
        "IfcPlateStandardCase", "IfcFooting", "IfcPile", "IfcChimney", Pruefkoerperbau.Teilentitaet,
    };

    public Kollisionsauftrag Auftrag { get; }
    public Projektvorgaben Vorgaben { get; }
    public Pruefkoerpermenge Menge { get; }
    public List<Kollisionsbefund> Befunde { get; } = [];
    /// <summary>Geprüfte Paare (nach der Breitphase) je Paarart.</summary>
    public Dictionary<string, int> PaareJeArt { get; } = [];
    /// <summary>Paare ohne weiche Vorgabe: Name der fehlenden Vorgabe → Anzahl Paare.</summary>
    public SortedDictionary<string, int> WeichFehlt { get; } = new(StringComparer.Ordinal);
    public int Teile, Verbunden, AmEndeGemeinsam, InOeffnung, InAussparung, UnterToleranz, TiefeUnbestimmt;
    public double SekundenPruefkoerper, SekundenSchmalphase;
    public List<string> Hinweise { get; } = [];
    public int HlksKoerper { get; private set; }

    readonly Projekt projekt;
    readonly Modell modell;
    readonly Dictionary<string, Pruefkoerper> nachKennung;
    readonly Dictionary<string, HashSet<string>> nachbarn = new(StringComparer.Ordinal);
    readonly HashSet<string> formteile = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> stueck = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<Aussparungsform>> aussparungenJeLeitung = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, List<Deckung>> oeffnungenJeBauteil = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Materialbefund> material = new(StringComparer.Ordinal);
    readonly double? toleranz;

    /// <summary>Typenliste des Projekts (Nachtrag B3d), einmal je Lauf gelesen.</summary>
    public Typenliste Typen { get; }
    /// <summary>Architektur-Prüfkörper im Paar hlks-vorwand (Installationsvorwände) bzw. hlks-bekleidung über die Typenliste (Kategorie
    /// bekleidung), als Unterdecke bzw. als Bodenaufbau (Nachtrag B3e).</summary>
    public int Vorwaende { get; private set; }
    public int BekleidungenTypenliste { get; private set; }
    public int Unterdecken { get; private set; }
    public int Bodenaufbauten { get; private set; }
    /// <summary>Architektur-Prüfkörper, die als Luftschicht in keinem Paar geprüft werden (Nachtrag B3e).</summary>
    public int Luftschichten { get; private set; }

    Kollisionssuche(Projekt p, Modell arbeit, Kollisionsauftrag a, Projektvorgaben v, Pruefkoerpermenge menge)
    {
        projekt = p; modell = arbeit; Auftrag = a; Vorgaben = v; Menge = menge;
        nachKennung = menge.Koerper.GroupBy(x => x.Kennung, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        toleranz = v.Zahl(Projektvorgaben.ToleranzHart);
        Typen = Typenliste.Lesen(arbeit);
    }

    /// <summary>Prüfkörper mit dieser Kennung (Element-Id bzw. «Unterlage-Id:GlobalId») oder null.</summary>
    public Pruefkoerper? Koerper(string kennung) => nachKennung.GetValueOrDefault(kennung);

    /// <summary>Material eines Bauteils (AussparungMaterial.cs; Typenliste dieses Laufs), je Prüfkörper einmal eingeteilt.</summary>
    public Materialbefund Material(Pruefkoerper b) => material.GetOrAdd(b.Kennung, _ => Bauteilmaterial.Von(modell, projekt.Katalog, b, Typen));

    /// <summary>Paar eines Prüfkörpers, der nicht HLKS ist (Nachtrag B3d): Architektur nach Kollisionspaar.Architektur, übrige
    /// Hintergrund-Produkte hlks-fremd, Bekleidungen immer hlks-bekleidung.</summary>
    public string PaarVon(Pruefkoerper b) =>
        b.Bekleidung ? KollisionTexte.HlksBekleidung
        : b.Bereich == "architektur" ? Kollisionspaar.Architektur(false, Material(b))
        : KollisionTexte.HlksFremd;

    /// <summary>Sucht alle Befunde. p: Projekt (Prüfkörper aus seinem Modell); arbeit: Modell mit Verbindungen und Aussparungen
    /// (die Arbeitskopie des Befehls).</summary>
    public static Kollisionssuche Suchen(Projekt p, Modell arbeit, Kollisionsauftrag a, Projektvorgaben v)
    {
        var uhr = Stopwatch.StartNew();
        var menge = Pruefkoerperbau.Erzeugen(p, new PruefkoerperOptionen { Unterlagen = a.Unterlagen?.ToList() });
        var s = new Kollisionssuche(p, arbeit, a, v, menge) { SekundenPruefkoerper = uhr.Elapsed.TotalSeconds };
        s.Laufen();
        s.SekundenSchmalphase = uhr.Elapsed.TotalSeconds - s.SekundenPruefkoerper;
        return s;
    }

    bool UnterlageGilt(Pruefkoerper k) => Auftrag.Unterlagen is null || k.UnterlageId is null || Auftrag.Unterlagen.Contains(k.UnterlageId);

    void Laufen()
    {
        var k = projekt.Katalog;
        Hinweise.AddRange(Menge.Hinweise);
        // Verbindungen, Formteile, Stücke desselben IFC-Produkts, Aussparungen
        foreach (var v in modell.Verbindungen)
        {
            Nachbar(v.Von.Element, v.Nach.Element);
            Nachbar(v.Nach.Element, v.Von.Element);
        }
        foreach (var e in modell.Elemente)
        {
            var art = HlksEinordnung.Art(k, e);
            if (art is HlksArt.Formteil or HlksArt.Armatur) formteile.Add(e.Id);
            if (e.Kennungen is { } kn && kn.TryGetValue("ifc", out var gid) && kn.TryGetValue("quelle", out var q))
                stueck[e.Id] = q + "|" + BcfFormat.GlobalIdBasis(gid);
        }
        foreach (var f in Aussparungsform.Alle(modell, k, Hinweise))
            foreach (var l in f.Leitungen)
            {
                if (!aussparungenJeLeitung.TryGetValue(l, out var liste)) aussparungenJeLeitung[l] = liste = [];
                liste.Add(f);
            }
        _ = Menge.OeffnungenVon("");   // Verzeichnis je Wirt vor dem parallelen Teil bauen

        var hlks = Menge.Koerper.Where(x => x.Bereich == "hlks" && x.ElementId is not null
                                            && (Auftrag.Geschoss is null || x.Geschoss == Auftrag.Geschoss)).ToList();
        HlksKoerper = hlks.Count;
        double rand = Auftrag.Weich ? new[] { Vorgaben.Zahl(Projektvorgaben.AbstandLeitungen) ?? 0 }.Concat(Vorgaben.AbstaendeWeich.Values).Max() : 0;
        var ih = new Raumindex<Pruefkoerper>(hlks, x => x.Huelle);
        var auftrag = new List<(Pruefkoerper A, Pruefkoerper B, string Paar)>();
        // Architektur-Bauteile nach Paar (Nachtrag B3c/B3d/B3e): Bekleidungen (IfcCovering, Typenliste bekleidung, Unterdecken, Bodenaufbau)
        // nur im Paar hlks-bekleidung, Installationsvorwände nur im Paar hlks-vorwand, Luftschichten in keinem, alle übrigen in
        // hlks-architektur
        var archAlle = Menge.Koerper.Where(x => x.Bereich == "architektur" && !x.Bekleidung && UnterlageGilt(x)).ToList();
        Luftschichten = archAlle.RemoveAll(x => Material(x).Luftschicht);
        var seite = archAlle.ToDictionary(x => x, PaarVon);
        Vorwaende = seite.Values.Count(x => x == KollisionTexte.HlksVorwand);
        var bekleidung = seite.Where(x => x.Value == KollisionTexte.HlksBekleidung).Select(x => Material(x.Key)).ToList();
        Unterdecken = bekleidung.Count(x => x.Unterdecke);
        Bodenaufbauten = bekleidung.Count(x => x.Bodenaufbau);
        BekleidungenTypenliste = bekleidung.Count - Unterdecken - Bodenaufbauten;
        if (Auftrag.Paare.Contains(KollisionTexte.HlksArchitektur))
        {
            var arch = archAlle.Where(x => seite[x] == KollisionTexte.HlksArchitektur).ToList();
            auftrag.AddRange(ih.Paare(new Raumindex<Pruefkoerper>(arch, x => x.Huelle), rand + Tol).Select(x => (x.A, x.B, KollisionTexte.HlksArchitektur)));
        }
        if (Auftrag.Paare.Contains(KollisionTexte.HlksBekleidung))
        {
            var bek = Menge.Koerper.Where(x => x.Bekleidung && x.Bereich != "hlks" && UnterlageGilt(x))
                .Concat(archAlle.Where(x => seite[x] == KollisionTexte.HlksBekleidung)).ToList();
            auftrag.AddRange(ih.Paare(new Raumindex<Pruefkoerper>(bek, x => x.Huelle), rand + Tol).Select(x => (x.A, x.B, KollisionTexte.HlksBekleidung)));
        }
        if (Auftrag.Paare.Contains(KollisionTexte.HlksVorwand))
        {
            var vw = archAlle.Where(x => seite[x] == KollisionTexte.HlksVorwand).ToList();
            auftrag.AddRange(ih.Paare(new Raumindex<Pruefkoerper>(vw, x => x.Huelle), rand + Tol).Select(x => (x.A, x.B, KollisionTexte.HlksVorwand)));
        }
        if (Auftrag.Paare.Contains(KollisionTexte.HlksHlks))
            auftrag.AddRange(ih.Paare(rand + Tol).Select(x => (x.A, x.B, KollisionTexte.HlksHlks)));
        if (Auftrag.Paare.Contains(KollisionTexte.HlksFremd))
        {
            var fremd = Menge.Koerper.Where(x => x.ElementId is null && x.Bereich != "architektur" && !x.Bekleidung && UnterlageGilt(x)).ToList();
            auftrag.AddRange(ih.Paare(new Raumindex<Pruefkoerper>(fremd, x => x.Huelle), rand + Tol)
                .Where(x => x.A.UnterlageId is null || x.A.UnterlageId != x.B.UnterlageId)
                .Select(x => (x.A, x.B, KollisionTexte.HlksFremd)));
        }
        foreach (var g in auftrag.GroupBy(x => x.Paar)) PaareJeArt[g.Key] = g.Count();

        var ergebnis = new List<Kollisionsbefund>?[auftrag.Count];
        var fehlt = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        try { Parallel.For(0, auftrag.Count, i => ergebnis[i] = Pruefen(auftrag[i].A, auftrag[i].B, auftrag[i].Paar, fehlt)); }
        catch (AggregateException ae) when (ae.InnerExceptions.FirstOrDefault(x => x is HlksFehler) is HlksFehler f) { throw f; }
        foreach (var (n, z) in fehlt) WeichFehlt[n] = z;
        Befunde.AddRange(ergebnis.Where(x => x is not null).SelectMany(x => x!)
            .OrderBy(b => b.Hlks.Kennzeichen ?? b.Hlks.Kennung, BcfFormat.Natuerlich).ThenBy(b => b.Kennung, StringComparer.Ordinal));
    }

    void Nachbar(string a, string b)
    {
        if (!nachbarn.TryGetValue(a, out var s)) nachbarn[a] = s = new HashSet<string>(StringComparer.Ordinal);
        s.Add(b);
    }

    /// <summary>Teile desselben Elements (dasselbe Element, mitabgedeckte Stücke, Stücke desselben IFC-Produkts).</summary>
    public bool Gleich(Pruefkoerper a, Pruefkoerper b)
    {
        if (a.ElementId is null || b.ElementId is null) return false;
        if (a.ElementId == b.ElementId || a.WeitereElemente.Contains(b.ElementId) || b.WeitereElemente.Contains(a.ElementId)) return true;
        return stueck.TryGetValue(a.ElementId, out var x) && stueck.TryGetValue(b.ElementId, out var y) && x == y;
    }

    /// <summary>Direkt verbunden oder beide an demselben Formteil bzw. derselben Armatur.</summary>
    public bool Verbundene(Pruefkoerper a, Pruefkoerper b)
    {
        if (a.ElementId is null || b.ElementId is null) return false;
        if (!nachbarn.TryGetValue(a.ElementId, out var na) || !nachbarn.TryGetValue(b.ElementId, out var nb)) return false;
        if (na.Contains(b.ElementId)) return true;
        foreach (var x in na) if (nb.Contains(x) && formteile.Contains(x)) return true;
        return false;
    }

    bool GemeinsamerNachbar(Pruefkoerper a, Pruefkoerper b)
    {
        if (a.ElementId is null || b.ElementId is null) return false;
        return nachbarn.TryGetValue(a.ElementId, out var na) && nachbarn.TryGetValue(b.ElementId, out var nb) && na.Overlaps(nb);
    }

    static string Gewerk(Pruefkoerper k) => (k.Gewerk ?? "allgemein").ToLowerInvariant();

    /// <summary>Kann die Schmalphase die Eindringtiefe messen? Leitungen (Achskörper) immer, Netze nur mit geschlossenen Teilen.</summary>
    static bool TiefeMessbar(Pruefkoerper k) => k.IstLeitung || (k.Netz?.HatGeschlosseneTeile ?? false);

    /// <summary>Querungen einer Leitungsachse durch ein Bauteil, dessen Netz nicht wasserdicht ist (Durchgaenge.Finden findet dann
    /// kein «innen»): die Durchstosspunkte der Achse (auf die Toleranz zusammengefasst) bilden der Reihe nach Paare Eintritt–Austritt,
    /// ein übrig bleibender Punkt einen Durchgang ohne Tiefe. ANNAHME (Prüfliste): so wird eine Leitung durch eine offen modellierte
    /// Decke (nur Ober- und Unterseite) als Durchdringung erkannt; die Deckungsprüfung kann das Material dann nicht abtasten und
    /// prüft nur die Mitte des Durchgangs.</summary>
    public static List<Durchgang> OffeneQuerungen(Pruefkoerper leitung, Pruefnetz netz)
    {
        var aus = new List<Durchgang>();
        for (int i = 1; i < leitung.Achse.Count; i++)
        {
            var a = leitung.Achse[i - 1]; var b = leitung.Achse[i];
            var r = b - a; double len = r.Laenge;
            if (len < 1e-9 || !Huelle3.Aus(a, b).Schneidet(netz.Huelle, Tol)) continue;
            var d = r * (1 / len);
            var punkte = new List<Treffer3>();
            foreach (var t in netz.Treffer(a, b))
                if (punkte.Count == 0 || (t.T - punkte[^1].T) * len > Tol) punkte.Add(t);
            for (int k = 0; k < punkte.Count; k += 2)
            {
                var ein = punkte[k]; var aus2 = k + 1 < punkte.Count ? punkte[k + 1] : ein;
                var ne = ein.Normale.Mal(d) > 0 ? -ein.Normale : ein.Normale;
                var na = aus2.Normale.Mal(d) < 0 ? -aus2.Normale : aus2.Normale;
                double tiefe = (aus2.T - ein.T) * len, cos = Math.Abs(ne.Mal(d));
                double winkel = Math.Asin(Math.Clamp(cos, 0, 1)) * 180 / Math.PI;
                aus.Add(new Durchgang(i - 1, ein.T, aus2.T, ein.Punkt, aus2.Punkt, ne, na, false, false, tiefe, tiefe * cos, winkel,
                    (ein.Punkt + aus2.Punkt) * 0.5, d));
            }
        }
        return aus;
    }

    /// <summary>Weiche Vorgabe eines Paars (Wert, Name mit Quelle) oder null; fehlend: Name der fehlenden Vorgabe.</summary>
    public (double Wert, string Name, string Quelle)? WeichVorgabe(string paar, Pruefkoerper a, Pruefkoerper b, out string? fehlend)
    {
        fehlend = null;
        if (!Auftrag.Weich) return null;
        if (paar == KollisionTexte.HlksHlks && a.IstLeitung && b.IstLeitung)
        {
            if (Vorgaben.Zahl(Projektvorgaben.AbstandLeitungen) is double d)
                return (d, Projektvorgaben.AbstandLeitungen, Vorgaben.Quelle(Projektvorgaben.AbstandLeitungen)!);
            fehlend = $"{Projektvorgaben.AbstandLeitungen} (Leitung–Leitung)";
            return null;
        }
        string ga = Gewerk(a);
        string gb = paar is KollisionTexte.HlksArchitektur or KollisionTexte.HlksBekleidung or KollisionTexte.HlksVorwand ? Projektvorgaben.SeiteArchitektur   // Bekleidungen, Vorwände wie Architektur
            : paar == KollisionTexte.HlksFremd ? Projektvorgaben.SeiteFremd : Gewerk(b);
        if (Vorgaben.AbstandWeichFuer(ga, true, gb, paar == KollisionTexte.HlksHlks) is { } t)
            return (t.Wert, $"{Projektvorgaben.AbstandWeich} «{t.Schluessel}»", Vorgaben.Quelle(Projektvorgaben.AbstandWeich)!);
        fehlend = $"{Projektvorgaben.AbstandWeich} «{Projektvorgaben.Schluessel($"{ga}-{gb}")}»";
        return null;
    }

    /// <summary>Durchdringung möglich: Leitung quert ein Bauteil dieser Klasse bzw. Entität.</summary>
    public bool Querbar(Pruefkoerper b) => QuerbarNach(projekt.Katalog, b.Klasse, b.Entitaet);

    /// <summary>Durchdringung möglich (eine Regel für Prüflauf und Material, Nachtrag B3e): mit Katalogklasse ein Bauteil, aber nicht
    /// Fenster bzw. Tür; sonst eine der <see cref="QuerbareEntitaeten"/>.</summary>
    public static bool QuerbarNach(Katalog k, string? klasse, string? entitaet)
    {
        if (klasse is { } kl)
            return k.Klassen.TryGetValue(kl, out var d) && d.IstArt("Bauteil") && !d.IstArt("Fenster") && !d.IstArt("Tuer");
        return entitaet is { } en && QuerbareEntitaeten.Contains(en);
    }

    List<Deckung> Deckungen(Pruefkoerper bauteil, Pruefkoerper leitung)
    {
        var oe = oeffnungenJeBauteil.GetOrAdd(bauteil.Kennung, kn => Menge.OeffnungenVon(kn).Select(Deckung.Aus).ToList());
        if (leitung.ElementId is null || !aussparungenJeLeitung.TryGetValue(leitung.ElementId, out var au)) return oe;
        var zu = au.Where(x => x.BauteilIst(bauteil)).Select(x => x.AlsDeckung()).ToList();
        return zu.Count == 0 ? oe : [.. oe, .. zu];
    }

    List<Kollisionsbefund>? Pruefen(Pruefkoerper a, Pruefkoerper b, string paar, ConcurrentDictionary<string, int> fehlt)
    {
        if (Gleich(a, b)) { Interlocked.Increment(ref Teile); return null; }
        if (Verbundene(a, b)) { Interlocked.Increment(ref Verbunden); return null; }
        var w = WeichVorgabe(paar, a, b, out var fehlend);
        if (fehlend is not null) fehlt.AddOrUpdate(fehlend, 1, (_, n) => n + 1);
        bool daemmung = (a.IstLeitung && a.Daemmdicke > 0) || (b.IstLeitung && b.Daemmdicke > 0);
        var kh = Schmalphase.Pruefen(a, b, daemmung ? 0 : w?.Wert ?? 0);
        // Tiefe unbestimmt nur, wenn ein Netz keine geschlossenen Teile hat (Schmalphase.TiefeNetze kann dann nicht messen); bei
        // geschlossenen Netzen ist Tiefe 0 eine streifende Berührung (Bernstrasse: 394 Fälle, keine Ecke im anderen Netz)
        bool unbestimmt = kh.Art == Kontaktart.Schnitt && kh.Eindringtiefe <= 0 && !(TiefeMessbar(a) && TiefeMessbar(b));
        bool hart = kh.Hart && (toleranz is not double t || kh.Eindringtiefe > t || unbestimmt);
        if (kh.Hart && !hart) Interlocked.Increment(ref UnterToleranz);
        if (hart)
        {
            if (kh.AmEnde && GemeinsamerNachbar(a, b)) { Interlocked.Increment(ref AmEndeGemeinsam); return null; }
            if (unbestimmt) Interlocked.Increment(ref TiefeUnbestimmt);
            string quelle = unbestimmt ? "Tiefe unbestimmt (Netze nicht geschlossen), Mass 0"
                : kh.Eindringtiefe <= 0 ? "Tiefe 0: streifende Berührung der Oberflächen (keine Ecke im anderen Körper)"
                : "Eindringtiefe ohne Dämmung (Schmalphase, Näherung)";
            if (a.IstLeitung && b.Netz is { } netz)
            {
                var gs = Durchgaenge.Finden(a, b);
                if (gs.Count == 0 && !netz.Geschlossen) gs = OffeneQuerungen(a, netz);
                if (gs.Count > 0)
                {
                    var deck = Deckungen(b, a);
                    var offen = new List<Durchgang>();
                    string? artDeckung = null;
                    foreach (var g in gs)
                        if (Oeffnungsdeckung.Gedeckt(a, g, netz, deck, out var x)) artDeckung ??= x; else offen.Add(g);
                    if (offen.Count == 0)
                    {
                        if (artDeckung == "aussparung") Interlocked.Increment(ref InAussparung); else Interlocked.Increment(ref InOeffnung);
                        return null;
                    }
                    var quer = Querbar(b) ? offen.Where(g => !(g.BeginntInnen && g.EndetInnen)).ToList() : [];
                    if (quer.Count > 0)
                        return quer.Select(g => new Kollisionsbefund(a, b, paar, "hart", "durchdringung", b.Kennung, g.Mitte, kh.Eindringtiefe, quelle)).ToList();
                }
            }
            return [new Kollisionsbefund(a, b, paar, "hart", "kontakt", null, kh.Ort, unbestimmt ? 0 : kh.Eindringtiefe, quelle)];
        }
        if (w is not { } v) return null;
        var kw = daemmung ? Schmalphase.Pruefen(a, b, v.Wert, a.IstLeitung, b.IstLeitung) : kh;
        double abstand = kw.Hart ? -kw.Eindringtiefe : kw.Abstand;
        if (!(abstand < v.Wert - 1e-9)) return null;
        if (kw.AmEnde && GemeinsamerNachbar(a, b)) { Interlocked.Increment(ref AmEndeGemeinsam); return null; }
        static string Z(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);
        var mq = $"{v.Name} {Z(v.Wert)} mm (Quelle: {v.Quelle}) − Abstand {(daemmung ? "mit Dämmung " : "")}{Z(abstand)} mm";
        return [new Kollisionsbefund(a, b, paar, "weich", "kontakt", null, kw.Ort, v.Wert - abstand, mq)];
    }
}

/// <summary>Befehl kollision_pruefen: Prüflauf und Nachführen der Kollision-Elemente.</summary>
public sealed class KollisionPruefen : IBefehl
{
    public string Name => "kollision_pruefen";
    public string Beschreibung => KollisionTexte.Pruefen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("paare", "liste", false, KollisionTexte.ParPaare),
        new("unterlagen", "liste", false, KollisionTexte.ParUnterlagen),
        new("geschoss", "text", false, KollisionTexte.ParGeschoss),
        new("weich", "bool", false, KollisionTexte.ParWeich),
    ];

    static readonly Regex LaufNr = new(@"Lauf (\d+)", RegexOptions.CultureInvariant);

    /// <summary>Nächste Lauf-Nummer: grösste Nummer aus Projektinformation.kollisionslauf und den pruefung-Texten der Kollisionen + 1.</summary>
    public static int NaechsterLauf(Modell m, Katalog k)
    {
        int max = 0;
        void Lies(string? t)
        {
            if (t is null) return;
            foreach (Match x in LaufNr.Matches(t))
                if (int.TryParse(x.Groups[1].Value, out var n)) max = Math.Max(max, n);
        }
        foreach (var pi in m.Elemente.Where(x => k.IstArt(x, "Projektinformation"))) Lies(pi.Text("kollisionslauf"));
        foreach (var e in m.Elemente.Where(x => k.IstArt(x, "Kollision"))) Lies(e.Text("pruefung"));
        return max + 1;
    }

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var uhr = Stopwatch.StartNew();
        var auftrag = Kollisionsauftrag.Lesen(k.Arbeit, k.Katalog, p);
        var vorgaben = Projektvorgaben.Lesen(k.Arbeit);
        var suche = Kollisionssuche.Suchen(k.Projekt, k.Arbeit, auftrag, vorgaben);
        Nachfuehren(k, suche, uhr);
    }

    static string Z(double x, string f = "0.#") => x.ToString(f, CultureInfo.InvariantCulture);

    static void Nachfuehren(Kontext k, Kollisionssuche s, Stopwatch uhr)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var a = s.Auftrag;
        int lauf = NaechsterLauf(m, kat);
        var zeit = k.Herkunft.Zeit;
        var zeitText = zeit.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var pruefung = $"{zeitText} · Lauf {lauf} · {string.Join(", ", a.Paare)}";
        var berechnet = k.Herkunft with { Art = "berechnung", Quelle = $"kollision_pruefen, Lauf {lauf}" };
        var geschosse = m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).OrderBy(g => g.Zahl("kote")).ToList();
        var vorhanden = m.Elemente.Where(e => kat.IstArt(e, "Kollision") && e.Text("kennung") is { Length: > 0 })
            .OrderBy(e => e.Kennzeichen, BcfFormat.Natuerlich).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        var nachKennung = new Dictionary<string, Element>(StringComparer.Ordinal);
        var nachSchluessel = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        foreach (var e in vorhanden)
        {
            nachKennung.TryAdd(e.Text("kennung")!, e);
            var sl = Kollisionskennung.Schluessel(Beteiligte(e));
            if (!nachSchluessel.TryGetValue(sl, out var l)) nachSchluessel[sl] = l = [];
            l.Add(e);
        }
        int nr = m.Elemente.Select(e => e.Kennzeichen).Where(kz => kz.StartsWith("KOL-", StringComparison.OrdinalIgnoreCase))
            .Select(kz => int.TryParse(kz[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        var benutzt = new HashSet<string>(StringComparer.Ordinal);
        var kennungenLauf = new HashSet<string>(StringComparer.Ordinal);
        int neu = 0, wieder = 0, wiederNeu = 0, behoben = 0, akzeptiertBleibt = 0, ausserhalb = 0, ausserhalbBekleidung = 0, ausserhalbVorwand = 0,
            materialNachgefuehrt = 0, geschrieben = 0, unveraendert = 0, luftBehoben = 0;
        var wiederNeuListe = new List<string>(); var erledigtGefunden = new List<string>();
        // Material der gequerten Bauteile (Nachtrag B3c; Typenliste Nachtrag B3d), je Prüfkörper einmal eingeteilt
        Materialbefund MaterialVon(Pruefkoerper b) => s.Material(b);

        foreach (var b in s.Befunde)
        {
            var ort = new V3(Math.Round(b.Ort.X, 1), Math.Round(b.Ort.Y, 1), Math.Round(b.Ort.Z, 1));
            var befund = b with { Ort = ort };
            var kennung = befund.Kennung;
            Element? alt = nachKennung.TryGetValue(kennung, out var x) && !benutzt.Contains(x.Id) ? x : null;
            if (alt is null && nachSchluessel.TryGetValue(befund.Schluessel, out var kand))
                alt = kand.Where(e => !benutzt.Contains(e.Id) && Lage(e) is V3 l && (l - ort).Laenge < Kollisionskennung.Fang)
                    .OrderBy(e => (Lage(e)!.Value - ort).Laenge).FirstOrDefault();
            string? geschoss = GeschossNachZ(geschosse, ort.Z) ?? b.Hlks.Geschoss;
            Element e2;
            if (alt is not null)
            {
                benutzt.Add(alt.Id);
                kennung = alt.Text("kennung")!;
                e2 = alt.Kopie();
                var st = alt.Text("status") ?? "neu";
                var bisher = alt.Text("pruefung") is { Length: > 0 } t ? t : null;
                if (st == "behoben")
                {
                    Setze(e2, "status", JsonValue.Create("neu"), berechnet with { Quelle = $"kollision_pruefen, Lauf {lauf}: wieder gefunden (war behoben)" });
                    // Statuswechsel durch den Lauf: pruefung ergänzen (der erste Lauf bleibt vorne)
                    Setze(e2, "pruefung", JsonValue.Create(bisher is null ? pruefung : $"{bisher} · wieder gefunden in Lauf {lauf} ({zeitText})"), berechnet);
                    wiederNeu++; wiederNeuListe.Add(alt.Kennzeichen);
                }
                else
                {
                    wieder++;
                    if (alt.Text("status") is null) Setze(e2, "status", JsonValue.Create("neu"), berechnet);
                    if (bisher is null) Setze(e2, "pruefung", JsonValue.Create(pruefung), berechnet);   // ANNAHME: frühester bekannter Lauf
                    if (st == "erledigt") erledigtGefunden.Add(alt.Kennzeichen);
                }
            }
            else
            {
                if (!kennungenLauf.Add(kennung)) { int i = 2; while (!kennungenLauf.Add($"{befund.Kennung}#{i}")) i++; kennung = $"{befund.Kennung}#{i}"; }
                e2 = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = $"KOL-{++nr}", Klasse = "Kollision", Angelegt = k.Herkunft };
                Setze(e2, "status", JsonValue.Create("neu"), berechnet);
                Setze(e2, "kennung", JsonValue.Create(kennung), berechnet);
                Setze(e2, "pruefung", JsonValue.Create(pruefung), berechnet);   // der Lauf, der sie zuerst gefunden hat
                neu++;
            }
            kennungenLauf.Add(kennung);
            Setze(e2, "art", JsonValue.Create(befund.Art), berechnet);
            Setze(e2, "typ", JsonValue.Create(befund.Typ), berechnet);
            if (befund.Bauteil is { } bt) Setze(e2, "bauteil", JsonValue.Create(bt), berechnet); else e2.Eigenschaften.Remove("bauteil");
            // Durchdringung: Material des Bauteils (P20 erwartet eine Aussparung nur in Beton, Mauerwerk bzw. bei unbekanntem Material)
            if (befund.Typ == "durchdringung")
            {
                var mb = MaterialVon(befund.Gegen);
                var hm = berechnet with { Quelle = $"kollision_pruefen, Lauf {lauf}: Material von {Kurz(m, befund.Gegen)} – {mb.Quelle}" };
                Setze(e2, "material", JsonValue.Create(mb.Text), hm);
                Setze(e2, "rohbau", JsonValue.Create(mb.Rohbau ? "ja" : mb.Unbekannt ? "unbekannt" : "nein"), hm);
            }
            else { e2.Eigenschaften.Remove("material"); e2.Eigenschaften.Remove("rohbau"); }
            Setze(e2, "beteiligte", new JsonArray(befund.Beteiligte.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()), berechnet);
            Setze(e2, "mass", JsonValue.Create(Math.Round(befund.Mass, 1, MidpointRounding.AwayFromZero)), berechnet with { Quelle = $"kollision_pruefen, Lauf {lauf}: {befund.Massquelle}" }, "mm");
            e2.Name = $"{Kurz(m, befund.Hlks)} ↔ {Kurz(m, befund.Gegen)}";
            e2.Geschoss = geschoss;
            e2.Geometrie = new Geometrie { Art = "punkt", Punkte = [[ort.X, ort.Y, ort.Z]] };
            // unverändert wiedergefunden (Status und Daten gleich): keine Operation
            if (alt is null || Json.Text(e2) != Json.Text(alt)) { k.Setzen(e2); geschrieben++; }
            else unveraendert++;
        }

        // nicht mehr gefunden
        foreach (var e in vorhanden.Where(e => !benutzt.Contains(e.Id)))
        {
            if (!ImUmfang(m, kat, s, e))
            {
                ausserhalb++;
                if (!a.Paare.Contains(KollisionTexte.HlksBekleidung) && MitBekleidung(m, kat, s, e)) ausserhalbBekleidung++;
                else if (!a.Paare.Contains(KollisionTexte.HlksVorwand) && MitVorwand(s, e)) ausserhalbVorwand++;
                // Nachtrag B3d: Material und rohbau gehören zum Bauteil, nicht zum Lauf – auch ausserhalb nachführen (Status bleibt)
                if (e.Text("typ") == "durchdringung" && e.Text("bauteil") is { Length: > 0 } bt && s.Koerper(bt) is { IstLeitung: false } bk)
                {
                    var mb = MaterialVon(bk);
                    var rohbau = mb.Rohbau ? "ja" : mb.Unbekannt ? "unbekannt" : "nein";
                    if (e.Text("material") != mb.Text || e.Text("rohbau") != rohbau)
                    {
                        var e3 = e.Kopie();
                        var hm = berechnet with { Quelle = $"kollision_pruefen, Lauf {lauf}: Material von {Kurz(m, bk)} (ausserhalb des Laufs nachgeführt) – {mb.Quelle}" };
                        Setze(e3, "material", JsonValue.Create(mb.Text), hm);
                        Setze(e3, "rohbau", JsonValue.Create(rohbau), hm);
                        k.Setzen(e3);
                        materialNachgefuehrt++; geschrieben++;
                    }
                }
                continue;
            }
            var st = e.Text("status") ?? "neu";
            if (st == "akzeptiert") { akzeptiertBleibt++; continue; }
            if (st == "behoben") continue;
            var e2 = e.Kopie();
            // Nachtrag B3e: eine ältere Kollision mit einer Luftschicht gehört zum Paar, in dem sie gefunden wurde (hlks-architektur), wird aber
            // nicht mehr geprüft – behoben mit dem Grund in der Herkunft
            bool luft = MitLuftschicht(s, e);
            if (luft) luftBehoben++;
            Setze(e2, "status", JsonValue.Create("behoben"), berechnet with
            {
                Quelle = luft ? $"kollision_pruefen, Lauf {lauf}: nicht mehr gefunden – Luftschicht, wird nicht geprüft ({Materialwortliste.EntscheidLuftschicht})"
                    : $"kollision_pruefen, Lauf {lauf}: nicht mehr gefunden",
            });
            Setze(e2, "pruefung", JsonValue.Create($"{e.Text("pruefung")} · nicht mehr gefunden in Lauf {lauf} ({zeitText})".TrimStart(' ', '·')), berechnet);
            k.Setzen(e2);
            behoben++; geschrieben++;
        }

        // Zähler: Projektinformation.kollisionslauf (erstes Element nach Kennzeichen; fehlt es, wie die übrigen Befehle anlegen)
        var info = HuelleHilfe.Projektinfo(m)?.Kopie()
                   ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation", Angelegt = k.Herkunft };
        Setze(info, "kollisionslauf", JsonValue.Create(pruefung), berechnet);
        info = k.Setzen(info);

        // Hinweise: Zahlen je Art und Paar
        var bf = s.Befunde;
        string Je(IEnumerable<Kollisionsbefund> l) =>
            $"hart {l.Count(x => x.Art == "hart")} (durchdringung {l.Count(x => x.Art == "hart" && x.Typ == "durchdringung")}, kontakt {l.Count(x => x.Art == "hart" && x.Typ == "kontakt")}), weich {l.Count(x => x.Art == "weich")}";
        // Abstandsprüfung aus (Entscheid Christian 19.10.2026): keine Abstände gesetzt – nur knapp in der Zusammenfassung
        bool abstandAus = a.Weich && s.Vorgaben.Zahl(Projektvorgaben.AbstandLeitungen) is null && s.Vorgaben.AbstaendeWeich.Count == 0;
        k.Hinweise.Add($"Prüflauf {lauf} ({string.Join(", ", a.Paare)}{(a.Geschoss is null ? "" : $", Geschoss {m.Finde(a.Geschoss)?.Kennzeichen}")}{(a.Unterlagen is null ? "" : $", Unterlagen {string.Join(", ", a.Unterlagen.Select(u => m.Finde(u)?.Kennzeichen ?? u))}")}): " +
                       $"{s.HlksKoerper} HLKS-Prüfkörper, {s.PaareJeArt.Values.Sum()} Paare geprüft; {bf.Count} Kollisionen – {Je(bf)}.{(abstandAus ? " Abstandsprüfung aus." : "")}");
        foreach (var paar in a.Paare)
            k.Hinweise.Add($"  {paar}: {s.PaareJeArt.GetValueOrDefault(paar)} Paare, {Je(bf.Where(x => x.Paar == paar))}.");
        k.Hinweise.Add($"Status: {neu} neu, {wieder} wieder gefunden (Status bleibt), {wiederNeu} wieder aufgetreten (behoben → neu), {behoben} nicht mehr gefunden → behoben, " +
                       $"{akzeptiertBleibt} akzeptiert und nicht mehr gefunden (bleibt), {ausserhalb} ausserhalb dieses Laufs (unverändert).");
        if (ausserhalbBekleidung > 0)
            k.Hinweise.Add($"Davon {ausserhalbBekleidung} mit Bekleidungen (IfcCovering) bzw. Unterdecken und Bodenaufbau: Paar {KollisionTexte.HlksBekleidung} nicht gewählt – " +
                           "unverändert (Entscheid Christian 19.10.2026: Bekleidungen nur auf Wunsch prüfen).");
        if (ausserhalbVorwand > 0)
            k.Hinweise.Add($"Davon {ausserhalbVorwand} mit Installationsvorwänden: Paar {KollisionTexte.HlksVorwand} nicht gewählt – unverändert " +
                           "(Entscheid Christian 19.10.2026: Leitungen in bzw. durch die Vorwand sind kein Befund).");
        if (luftBehoben > 0)
            k.Hinweise.Add($"Davon {luftBehoben} mit Luftschichten → behoben: Luftschichten werden nicht mehr geprüft (Entscheid Christian 19.10.2026, «Luftschichten ignorieren»).");
        if (materialNachgefuehrt > 0)
            k.Hinweise.Add($"{materialNachgefuehrt} Durchdringung(en) ausserhalb dieses Laufs: Material und rohbau nachgeführt (z. B. nach typmaterial_setzen), Status bleibt.");
        if (s.Vorwaende > 0 || s.BekleidungenTypenliste > 0 || s.Unterdecken > 0 || s.Bodenaufbauten > 0 || s.Luftschichten > 0 || s.Typen.Eintraege.Count > 0)
            k.Hinweise.Add($"Typenliste ({s.Typen.Eintraege.Count} Einträge) und Typnamen: {s.Vorwaende} Installationsvorwand/-vorwände " +
                           $"(nur im Paar {KollisionTexte.HlksVorwand}{(a.Paare.Contains(KollisionTexte.HlksVorwand) ? ", gewählt" : "")}), " +
                           $"{s.BekleidungenTypenliste} Bekleidung(en) laut Typenliste, {s.Unterdecken} Unterdecke(n), {s.Bodenaufbauten} Bodenaufbau-Schicht(en) " +
                           $"(nur im Paar {KollisionTexte.HlksBekleidung}{(a.Paare.Contains(KollisionTexte.HlksBekleidung) ? ", gewählt" : "")}), " +
                           $"{s.Luftschichten} Luftschicht(en) (in keinem Paar geprüft).");
        k.Hinweise.Add($"Geschrieben: {geschrieben} Kollision(en) neu oder geändert, {unveraendert} unverändert wiedergefunden (nicht geschrieben); " +
                       $"Lauf-Nummer in {info.Kennzeichen}.kollisionslauf.");
        if (wiederNeuListe.Count > 0) k.Hinweise.Add($"Wieder aufgetreten (waren behoben): {string.Join(", ", wiederNeuListe.Take(20))}{(wiederNeuListe.Count > 20 ? " …" : "")}.");
        if (erledigtGefunden.Count > 0) k.Hinweise.Add($"Als erledigt markiert, aber weiterhin gefunden: {string.Join(", ", erledigtGefunden.Take(20))}{(erledigtGefunden.Count > 20 ? " …" : "")}.");
        k.Hinweise.Add($"Keine Kollision: {s.Verbunden} verbunden (direkt bzw. über Formteil/Armatur), {s.AmEndeGemeinsam} am Leitungsende am gemeinsamen Element, " +
                       $"{s.InOeffnung} in Architektenöffnung, {s.InAussparung} in Aussparung, {s.Teile} Teile desselben Elements, {s.UnterToleranz} innerhalb toleranz_hart.");
        if (s.Vorgaben.Zahl(Projektvorgaben.ToleranzHart) is double tol)
            k.Hinweise.Add($"toleranz_hart {Z(tol)} mm (Quelle: {s.Vorgaben.Quelle(Projektvorgaben.ToleranzHart)}).");
        else k.Hinweise.Add("toleranz_hart fehlt in den Projektvorgaben – jede Durchdringung wird als harte Kollision gemeldet (vorgaben_setzen).");
        if (s.TiefeUnbestimmt > 0) k.Hinweise.Add($"{s.TiefeUnbestimmt} harte Kollision(en) mit unbestimmter Tiefe (Netze nicht geschlossen) – Mass 0, unabhängig von toleranz_hart gemeldet.");
        // Abstandsprüfung (Entscheid Christian 19.10.2026: keine): ohne Werte kein eigener Hinweis (nur «Abstandsprüfung aus» oben);
        // sind Werte gesetzt, werden fehlende Gewerkpaare genannt
        if (!a.Weich) k.Hinweise.Add("Weiche Prüfung ausgeschaltet (weich = false); weiche Kollisionen bleiben unverändert.");
        else if (!abstandAus && s.WeichFehlt.Count > 0)
            k.Hinweise.Add($"Weiche Prüfung übersprungen (Vorgabe fehlt, vorgaben_setzen): {string.Join(", ", s.WeichFehlt.Select(x => $"{x.Key} – {x.Value} Paar(e)"))}.");
        if (s.Vorgaben.Element is null) k.Hinweise.Add("Keine Projektvorgaben (VG-1): ohne toleranz_hart wird jede Durchdringung gemeldet (Abfrage vorgaben).");
        k.Hinweise.Add($"Dauer {Z(uhr.Elapsed.TotalSeconds, "0.0")} s (Prüfkörper {Z(s.SekundenPruefkoerper, "0.0")} s, Paare und Schmalphase {Z(s.SekundenSchmalphase, "0.0")} s).");
        foreach (var h in s.Hinweise.Take(8)) k.Hinweise.Add("Prüfkörper: " + h);
        if (s.Hinweise.Count > 8) k.Hinweise.Add($"Prüfkörper: … {s.Hinweise.Count - 8} weitere Hinweise.");
    }

    static void Setze(Element e, string name, JsonNode wert, Herkunft h, string? einheit = null)
    {
        if (e.Eigenschaft(name) is { } alt && alt.Inhalt?.ToJsonString() == wert.ToJsonString()) return;
        e.Eigenschaften[name] = new Wert { Inhalt = wert, Einheit = einheit, Herkunft = h };
    }

    internal static List<string> Beteiligte(Element kollision) =>
        kollision.Eigenschaft("beteiligte")?.Inhalt is JsonArray a
            ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s : x?.ToJsonString() ?? "").Where(s => s.Length > 0).ToList()
            : [];

    internal static V3? Lage(Element e) => e.Geometrie?.Punkte.FirstOrDefault() is { Length: >= 3 } p ? new V3(p[0], p[1], p[2]) : null;

    static string? GeschossNachZ(List<Element> geschosse, double z)
    {
        Element? best = null;
        foreach (var g in geschosse)
            if (g.Zahl("kote") is double kote && g.Zahl("hoehe") is double h && z >= kote - 1 && z < kote + h) best = g;
        return best?.Id;
    }

    static string Kurz(Modell m, Pruefkoerper k) =>
        k.ElementId is not null ? (m.Finde(k.ElementId)?.Kennzeichen ?? k.Kennzeichen ?? k.Kennung) : $"{k.Entitaet ?? "IFC-Produkt"} {k.GlobalId}";

    /// <summary>Ist an der Kollision eine Bekleidung beteiligt (Prüfkörper bzw. Element einer Bekleidungsklasse)?</summary>
    static bool MitBekleidung(Modell m, Katalog kat, Kollisionssuche s, Element kol) =>
        Beteiligte(kol).Any(x => s.Koerper(x) is { } kb && kb.Bereich != "hlks" && s.PaarVon(kb) == KollisionTexte.HlksBekleidung
                                 || (m.Finde(x) is { } e && Pruefkoerperbau.IstBekleidung(kat, e)));

    /// <summary>Ist an der Kollision eine Installationsvorwand beteiligt (Nachtrag B3d)?</summary>
    static bool MitVorwand(Kollisionssuche s, Element kol) =>
        Beteiligte(kol).Any(x => s.Koerper(x) is { } kb && kb.Bereich != "hlks" && s.PaarVon(kb) == KollisionTexte.HlksVorwand);

    /// <summary>Ist an der Kollision eine Luftschicht beteiligt (Nachtrag B3e)?</summary>
    static bool MitLuftschicht(Kollisionssuche s, Element kol) =>
        Beteiligte(kol).Any(x => s.Koerper(x) is { } kb && kb.Bereich == "architektur" && !kb.Bekleidung && s.Material(kb).Luftschicht);

    /// <summary>Gehört eine nicht wieder gefundene Kollision zum Umfang dieses Laufs (Paar, Geschoss, Unterlagen, weich mit
    /// Vorgabe)? Sonst bleibt sie unverändert.</summary>
    static bool ImUmfang(Modell m, Katalog kat, Kollisionssuche s, Element kol)
    {
        var a = s.Auftrag;
        var bet = Beteiligte(kol);
        if (bet.Count == 0) return false;
        var seiten = bet.Select(x => (Kennung: x, Element: m.Finde(x), Koerper: s.Koerper(x))).ToList();
        bool IstHlks((string Kennung, Element? Element, Pruefkoerper? Koerper) x) =>
            x.Element is { } e ? Pruefkoerperbau.Bereich(kat, e) == "hlks" : x.Koerper?.Bereich == "hlks" && x.Koerper.ElementId is not null;
        var hlks = seiten.Where(IstHlks).ToList();
        // Geschoss
        if (a.Geschoss is not null)
        {
            var g = hlks.Select(x => x.Element?.Geschoss).FirstOrDefault(x => x is not null) ?? kol.Geschoss;
            if (g != a.Geschoss) return false;
        }
        // Paar
        string? paar = null;
        var andere = seiten.Where(x => !hlks.Contains(x)).ToList();
        if (hlks.Count >= 2) paar = KollisionTexte.HlksHlks;
        else if (andere.Count > 0)
        {
            var o = andere[0];
            string? unterlage = o.Element?.Kennungen?.GetValueOrDefault("quelle") ?? o.Koerper?.UnterlageId
                                ?? (o.Kennung.IndexOf(':') is var i and > 0 ? o.Kennung[..i] : null);
            if (a.Unterlagen is not null && unterlage is not null && !a.Unterlagen.Contains(unterlage)) return false;
            // Bekleidungen (Nachtrag B3c) gehören zum Paar hlks-bekleidung, Installationsvorwände (Nachtrag B3d) zu hlks-vorwand – ein Lauf
            // ohne dieses Paar lässt sie unverändert
            if (o.Element is { } oe)
                paar = Pruefkoerperbau.Bereich(kat, oe) != "architektur" ? null
                    : Pruefkoerperbau.IstBekleidung(kat, oe) || o.Koerper?.Bekleidung == true ? KollisionTexte.HlksBekleidung
                    : o.Koerper is { } oek ? s.PaarVon(oek) : KollisionTexte.HlksArchitektur;
            else if (o.Koerper is { } ok)
                paar = s.PaarVon(ok);
        }
        if (paar is not null && !a.Paare.Contains(paar)) return false;
        if (paar is null && !a.Paare.Contains(KollisionTexte.HlksArchitektur) && !a.Paare.Contains(KollisionTexte.HlksBekleidung) && !a.Paare.Contains(KollisionTexte.HlksVorwand)
            && !a.Paare.Contains(KollisionTexte.HlksFremd) && seiten.Any(x => x.Element is null && x.Kennung.Contains(':'))) return false;
        // weich nur, wenn die weiche Prüfung für dieses Paar lief
        if (kol.Text("art") == "weich")
        {
            if (!a.Weich) return false;
            var ks = seiten.Select(x => x.Koerper).ToList();
            if (paar is not null && ks.All(x => x is not null) && ks.Count == 2)
            {
                var h = ks.FirstOrDefault(x => x!.Bereich == "hlks" && x.ElementId is not null) ?? ks[0]!;
                var g = ks.First(x => !ReferenceEquals(x, h))!;
                if (s.WeichVorgabe(paar, h, g, out _) is null) return false;
            }
        }
        return true;
    }
}
