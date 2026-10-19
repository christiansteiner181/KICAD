// Typenliste «Typname → Material» je Projekt (Modul 3b, Nachtrag B3d, Entscheide Christian 19.10.2026 «Material», entwurf/modul-3b.md):
// Christian legt für Bauteiltypen der Architektur fest, aus welchem Material sie sind; das gilt vor der Materialzuordnung im IFC und vor
// der Wortliste (Vorrang und Einteilung: Aussparungen/AussparungMaterial.cs). Schlüssel ist der Typname – darum gilt die Liste auch nach
// einer neuen Architektenfassung (neue GlobalIds, gleiche Typen).
//
// ABLAGE (Entscheid des Agenten, begründet): je Typname ein Element der Klasse Typmaterial (Kennzeichen TM-n) mit typname und kategorie
//   – nicht eine Eigenschaft «typen» an den Projektvorgaben VG-1:
//   1. Jeder Eintrag hat eine eigene Herkunft (Art, Akteur, Zeit, Quelle) wie jeder andere Wert (Leitprinzip 4); als Teil eines
//      JSON-Objekts an VG-1 hätte nur das ganze Objekt eine Herkunft.
//   2. Synchronisation: Konflikte entscheidet der Kern je Element (die neuere Änderung gewinnt). Zwei Rechner, die verschiedene Typen
//      festlegen – oder einer die Typen, der andere eine Projektvorgabe –, überschreiben sich so nicht.
//   3. Pakete bleiben klein (nur die geänderten Einträge), Rückgängig wirkt je Befehl, und die allgemeinen Werkzeuge (elemente_suchen,
//      element_lesen, element_loeschen) gehen auch.
//   Folge: derselbe Typname kann (auf zwei Rechnern gleichzeitig angelegt) zweimal vorkommen. Es gilt der zuletzt gesetzte (Zeit der
//   Herkunft der kategorie, dann Kennzeichen); die Abfrage nennt die Doppel, typmaterial_setzen räumt sie auf.
//   Ein Eintrag gilt nur mit Quelle an der kategorie (wie die Projektvorgaben: über element_aendern ohne Quelle gesetzt gilt er nicht).
//
// VERGLEICH der Typnamen: ohne Gross-/Kleinschreibung, Leerschläge zusammengefasst. Revit führt den Typnamen als «Familie:Typ» (Befund
//   Bernstrasse 42: «DirectShape:2.3_IW 1.5/8/1.5 cm 110»); ein Eintrag gilt für den ganzen Typnamen und für den Teil nach dem ersten
//   Doppelpunkt (Entscheid Christian 19.10.2026 zu B3d, Fragefenster – die Auslegung aus B3d ist bestätigt). So passt Christians
//   «2.3_IW 1.5/8/1.5 cm 110» und bleibt gültig, wenn eine neue Fassung die Familie anders nennt. Wer nur eine Familie meint, schreibt
//   den ganzen Namen «Familie:Typ».
//
// Kategorien (Katalog Typmaterial.kategorie): beton, mauerwerk (Rohbau, Aussparung), leichtbau (Gips, Ständer), holz, stahl, daemmung
//   (keine Aussparung; eine Durchdringung bleibt eine Kollision, ohne P20), bekleidung (keine Aussparung; Kollision nur im Paar
//   hlks-bekleidung wie IfcCovering – Entscheid Christian «Bekleidungen nicht melden»), installationsvorwand (keine Aussparung; Kollision
//   nur im Paar hlks-vorwand – Entscheid Christian 19.10.2026: Leitungen in bzw. durch die Vorwand sind kein Befund); Nachtrag B3e
//   (Entscheide Christian 19.10.2026 zu B3d): bodenaufbau (Unterlagsboden, Estrich, Trittschalldämmung, Schichten über der Tragdecke;
//   keine Aussparung, Kollision nur im Paar hlks-bekleidung – Leitungen im Bodenaufbau sind kein Befund), luftschicht (weder Kollision
//   noch Aussparung noch P20, in keinem Paar). Unterdecken haben keine eigene Kategorie: «Unterdecken wie Bekleidung» → bekleidung.
//
// Befehl typmaterial_setzen { typen: [{ typname, kategorie, quelle? }], quelle? }: legt Einträge an, ändert sie (kategorie) bzw. entfernt
//   sie (kategorie null oder ""). Jeder Eintrag braucht eine Quelle (eigene oder die gemeinsame); fehlt sie oder ist ein Eintrag falsch,
//   wird der ganze Befehl abgelehnt und alle Fehler werden genannt. Ein Befehl = ein Paket (Trockenlauf, Rückgängig). Die Hinweise
//   nennen je Typ die betroffenen Bauteile, Aussparungen und Durchgänge; die Wirkung kommt mit aussparungen_vorschlagen und
//   kollision_pruefen.
// Abfrage typmaterial { betroffen? }: die Typenliste mit Wirkung und Zahl der Bauteile, Aussparungen und Durchgänge je Eintrag; die
//   unbekannten Typen (Material unbekannt) aus den Netzdateien der IFC-Unterlagen und den KICAD-Bauteilen, ebenso die Typen, deren
//   Material aus dem Typnamen stammt (zum Bestätigen). Durchgänge = Kollisionen typ durchdringung (nicht behoben) des letzten
//   Prüflaufs; Aussparungen = gültige Aussparungen (nicht veraltet) mit Bezug auf das Bauteil. Ohne Geometrie: liest nur die Kopfdaten
//   der Netzdateien.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ein gültiger Eintrag der Typenliste (Element Typmaterial mit Quelle).</summary>
public sealed record Typeneintrag(Element Element, string Typname, string Kategorie, Materialgruppe Gruppe, string Quelle, Herkunft Herkunft)
{
    public string Kennzeichen => Element.Kennzeichen;
    /// <summary>Fundstelle für Materialbefund und Herkunft: «Typenliste TM-1: «Typ» → leichtbau (Quelle: …)».</summary>
    public string Fundstelle => $"Typenliste {Kennzeichen}: «{Typname}» → {Kategorie} (Quelle: {Quelle})";
}

/// <summary>Die Typenliste des Projekts (Regeln im Dateikopf).</summary>
public sealed class Typenliste
{
    public const string Klasse = "Typmaterial";
    public const string Kuerzel = "TM";

    /// <summary>Kategorien (Katalog Typmaterial.kategorie) und ihre Materialgruppe.</summary>
    public static readonly IReadOnlyDictionary<string, Materialgruppe> Kategorien = new Dictionary<string, Materialgruppe>(StringComparer.Ordinal)
    {
        ["beton"] = Materialgruppe.Beton, ["mauerwerk"] = Materialgruppe.Mauerwerk, ["leichtbau"] = Materialgruppe.Leichtbau,
        ["holz"] = Materialgruppe.Holz, ["stahl"] = Materialgruppe.Metall, ["daemmung"] = Materialgruppe.Daemmung,
        ["bekleidung"] = Materialgruppe.Ausbau, ["installationsvorwand"] = Materialgruppe.Installationsvorwand,
        ["bodenaufbau"] = Materialgruppe.Bodenaufbau, ["luftschicht"] = Materialgruppe.Luftschicht,   // Nachtrag B3e
    };

    /// <summary>Kategorie zu einer Materialgruppe (für Vorschläge aus der Wortliste); Unterdecke → bekleidung (Nachtrag B3e: «Unterdecken
    /// wie Bekleidung»); null für ohne Aussage.</summary>
    public static string? KategorieVon(Materialgruppe g) =>
        g == Materialgruppe.Unterdecke ? "bekleidung" : Kategorien.FirstOrDefault(x => x.Value == g).Key;

    /// <summary>Was eine Kategorie bewirkt (Abfrage typmaterial, Hinweise von typmaterial_setzen).</summary>
    public static string Wirkung(string kategorie) => kategorie switch
    {
        "beton" or "mauerwerk" => "Rohbau: Aussparung; eine Durchdringung ohne Aussparung meldet P20",
        "bekleidung" => "keine Aussparung; Kollision nur im Paar hlks-bekleidung (wie IfcCovering, auch Unterdecken)",
        "installationsvorwand" => "keine Aussparung; Kollision nur im Paar hlks-vorwand (Leitungen liegen in der Vorwand)",
        "bodenaufbau" => "keine Aussparung; Kollision nur im Paar hlks-bekleidung (wie IfcCovering; Leitungen im Bodenaufbau sind kein Befund)",
        "luftschicht" => "weder Kollision noch Aussparung noch P20 (in keinem Paar geprüft)",
        _ => "keine Aussparung; eine Durchdringung bleibt eine Kollision (ohne P20)",
    };

    /// <summary>Gültige Einträge, je Schlüssel einer (der zuletzt gesetzte), nach Kennzeichen.</summary>
    public IReadOnlyList<Typeneintrag> Eintraege { get; }
    /// <summary>Einträge ohne Quelle an der kategorie (gelten nicht).</summary>
    public IReadOnlyList<Element> OhneQuelle { get; }
    /// <summary>Einträge ohne typname bzw. mit unbekannter kategorie (gelten nicht).</summary>
    public IReadOnlyList<Element> Ungueltig { get; }
    /// <summary>Typnamen mit mehreren gültigen Einträgen: der geltende und die übrigen.</summary>
    public IReadOnlyList<(Typeneintrag Gilt, IReadOnlyList<Element> Weitere)> Doppelt { get; }

    readonly Dictionary<string, Typeneintrag> nachSchluessel;
    readonly Dictionary<string, List<Element>> elementeJeSchluessel;

    Typenliste(List<Typeneintrag> eintraege, List<Element> ohne, List<Element> ungueltig, List<(Typeneintrag, IReadOnlyList<Element>)> doppelt,
        Dictionary<string, List<Element>> jeSchluessel)
    {
        Eintraege = eintraege; OhneQuelle = ohne; Ungueltig = ungueltig; Doppelt = doppelt; elementeJeSchluessel = jeSchluessel;
        nachSchluessel = eintraege.ToDictionary(e => Schluessel(e.Typname), StringComparer.Ordinal);
    }

    /// <summary>Vergleichsschlüssel eines Typnamens: ohne Gross-/Kleinschreibung, Leerschläge zusammengefasst.</summary>
    public static string Schluessel(string typname) => Regex.Replace(typname.Trim(), @"\s+", " ").ToLowerInvariant();

    /// <summary>Typname ohne «Familie:» (Teil nach dem ersten Doppelpunkt, Revit), sonst null.</summary>
    public static string? OhneFamilie(string typname)
    {
        var i = typname.IndexOf(':');
        return i >= 0 && typname[(i + 1)..].Trim() is { Length: > 0 } rest ? rest : null;
    }

    /// <summary>Typname, wie ihn die Typenliste vorschlägt: ohne «Familie:», sonst der ganze (getrimmt).</summary>
    public static string Kern(string typname) => OhneFamilie(typname) ?? typname.Trim();

    /// <summary>Eintrag für den Typnamen eines Bauteils: genau, sonst für den Teil nach «Familie:» (Regel im Dateikopf); null = keiner.</summary>
    public Typeneintrag? Finde(string? typname)
    {
        if (string.IsNullOrWhiteSpace(typname) || nachSchluessel.Count == 0) return null;
        if (nachSchluessel.TryGetValue(Schluessel(typname), out var e)) return e;
        return OhneFamilie(typname) is { } kern && nachSchluessel.TryGetValue(Schluessel(kern), out var e2) ? e2 : null;
    }

    /// <summary>Der gültige Eintrag mit genau diesem Typnamen (ohne die Familien-Regel) oder null.</summary>
    public Typeneintrag? Genau(string typname) => nachSchluessel.GetValueOrDefault(Schluessel(typname));

    /// <summary>Alle Elemente mit genau diesem Typnamen (gültig, ohne Quelle, ungültig).</summary>
    public IReadOnlyList<Element> ElementeZu(string typname) => elementeJeSchluessel.GetValueOrDefault(Schluessel(typname)) ?? [];

    /// <summary>Liest die Typenliste aus dem Modell.</summary>
    public static Typenliste Lesen(Modell m)
    {
        var alle = m.DerKlasse(Klasse).OrderBy(e => e.Kennzeichen, Aussparungsbezug.Natuerlich).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        var ohne = new List<Element>(); var ungueltig = new List<Element>();
        var jeSchluessel = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        var kandidaten = new Dictionary<string, List<Typeneintrag>>(StringComparer.Ordinal);
        foreach (var e in alle)
        {
            var typname = e.Text("typname")?.Trim();
            var w = e.Eigenschaft("kategorie");
            var kat = w?.AlsText()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(typname) || kat is null || !Kategorien.TryGetValue(kat, out var g)) { ungueltig.Add(e); continue; }
            var s = Schluessel(typname);
            if (!jeSchluessel.TryGetValue(s, out var l)) jeSchluessel[s] = l = [];
            l.Add(e);
            if (string.IsNullOrWhiteSpace(w!.Herkunft.Quelle)) { ohne.Add(e); continue; }
            if (!kandidaten.TryGetValue(s, out var k)) kandidaten[s] = k = [];
            k.Add(new Typeneintrag(e, typname, kat, g, w.Herkunft.Quelle!.Trim(), w.Herkunft));
        }
        var eintraege = new List<Typeneintrag>();
        var doppelt = new List<(Typeneintrag, IReadOnlyList<Element>)>();
        foreach (var k in kandidaten.Values)
        {
            // zuletzt gesetzt gilt (Zeit der Herkunft), bei gleicher Zeit das kleinere Kennzeichen
            var sortiert = k.OrderByDescending(x => x.Herkunft.Zeit).ThenBy(x => x.Kennzeichen, Aussparungsbezug.Natuerlich).ToList();
            eintraege.Add(sortiert[0]);
            if (sortiert.Count > 1) doppelt.Add((sortiert[0], sortiert.Skip(1).Select(x => x.Element).ToList()));
        }
        eintraege.Sort((a, b) => Aussparungsbezug.Natuerlich.Compare(a.Kennzeichen, b.Kennzeichen));
        return new Typenliste(eintraege, ohne, ungueltig, doppelt, jeSchluessel);
    }

    sealed record Zwischen(long Zaehler, Typenliste Liste);
    static readonly ConditionalWeakTable<Modell, Zwischen> Speicher = new();

    /// <summary>Die Typenliste des Modells, zwischengespeichert, solange sich das Modell nicht ändert (Änderungszähler wie die Prüfsumme).</summary>
    public static Typenliste Von(Modell m)
    {
        var zaehler = m.Aenderungszaehler;
        if (Speicher.TryGetValue(m, out var z) && z.Zaehler == zaehler) return z.Liste;
        var l = Lesen(m);
        Speicher.AddOrUpdate(m, new Zwischen(zaehler, l));
        return l;
    }
}

/// <summary>Kopfdaten der Netzdatei einer IFC-Unterlage (ohne Dreiecke), je Datei und Stand zwischengespeichert – für Kollisionsliste und
/// Typenliste.</summary>
public static class Netzkoepfe
{
    static readonly ConcurrentDictionary<(string Pfad, long Laenge, DateTime Zeit), IfcNetzdatei> Speicher = new();

    /// <summary>Netzdatei der Unterlage oder null (keine, fehlt, nicht lesbar).</summary>
    public static IfcNetzdatei? Von(Projekt p, Element unterlage)
    {
        if (unterlage.Text("netz") is not { Length: > 0 } name) return null;
        try
        {
            var fi = new FileInfo(Unterlagen.Pfad(p, name));
            if (!fi.Exists) return null;
            if (Speicher.Count > 32) Speicher.Clear();   // Tests legen viele kleine Projekte an
            return Speicher.GetOrAdd((fi.FullName, fi.Length, fi.LastWriteTimeUtc), _ => IfcNetzdatei.Oeffnen(fi.FullName));
        }
        catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    /// <summary>GlobalId ohne den Zusatz geteilter Stücke («GlobalId:2» → «GlobalId»).</summary>
    public static string Basis(string kennung) => kennung.IndexOf(':') is var i and > 0 ? kennung[..i] : kennung;
}

/// <summary>Bauteile des Projekts mit Typname und Material – Grundlage der Abfrage typmaterial und der Hinweise von typmaterial_setzen.
/// Bauteile = KICAD-Elemente der Klassen Bauteil (ohne Fenster, Tür, Bekleidung) und Produkte der Netzdateien mit querbarer Entität
/// (Kollisionssuche.QuerbareEntitaeten, ohne IfcCovering), die nicht übernommen sind und kein Teil eines Ganzen (Rolle teil mit
/// Bezug).</summary>
sealed class Typenbestand
{
    public sealed record Bauteil(string Kennung, string Name, string? Entitaet, Bauteilangaben Angaben, Typeneintrag? Eintrag, Materialbefund? Befund,
        int Aussparungen, int Durchgaenge);

    public List<Bauteil> Bauteile { get; } = [];
    public List<string> Hinweise { get; } = [];

    public static Typenbestand Lesen(Projekt p, Modell m, Typenliste liste, bool mitBefund)
    {
        var k = p.Katalog;
        var b = new Typenbestand();
        var aussparungen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in m.DerKlasse("Aussparung").Where(a => AussparungStatus.Gueltig(AussparungStatus.Von(a))))
            foreach (var s in Aussparungsbezug.BauteilSchluesselAlle(a)) aussparungen[s] = aussparungen.GetValueOrDefault(s) + 1;
        var durchgaenge = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kol in m.DerKlasse("Kollision").Where(x => x.Text("typ") == "durchdringung" && x.Text("status") != "behoben"))
            if (kol.Text("bauteil") is { Length: > 0 } bt) durchgaenge[bt] = durchgaenge.GetValueOrDefault(bt) + 1;

        var koepfe = new Dictionary<string, IfcNetzdatei>(StringComparer.Ordinal);
        foreach (var ul in m.DerKlasse("Unterlage").Where(u => u.Text("netz") is { Length: > 0 }))
        {
            if (Netzkoepfe.Von(p, ul) is { } nd) koepfe[ul.Id] = nd;
            else b.Hinweise.Add($"Unterlage {ul.Kennzeichen}: Netzdatei fehlt bzw. ist nicht lesbar – ihre Typen fehlen hier.");
        }
        var befunde = new Dictionary<string, Materialbefund>(StringComparer.Ordinal);   // Hintergrund: je gleiche Angaben einmal einteilen
        Materialbefund? Befund(Bauteilangaben a, bool element)
        {
            if (!mitBefund) return null;
            if (element) return Bauteilmaterial.Von(m, k, a, liste);
            var sig = $"{a.NetzFassung}\u0001{a.Typname}\u0001{a.Bekleidung}\u0001{string.Join("\u0002", a.Materialien)}";
            if (!befunde.TryGetValue(sig, out var mb)) befunde[sig] = mb = Bauteilmaterial.Von(m, k, a, liste);
            return mb;
        }

        var erledigt = new HashSet<(string, string)>();
        foreach (var e in m.Elemente.Where(e => k.IstArt(e, "Bauteil") && !k.IstArt(e, "Fenster") && !k.IstArt(e, "Tuer") && !Pruefkoerperbau.IstBekleidung(k, e))
                     .OrderBy(e => e.Kennzeichen, Aussparungsbezug.Natuerlich))
        {
            IfcNetzProdukt? pr = null; int? fassung = null;
            if (e.Kennungen is { } kn && kn.TryGetValue("quelle", out var q) && kn.TryGetValue("ifc", out var g) && koepfe.TryGetValue(q, out var nd))
            {
                pr = nd.Produkt(Netzkoepfe.Basis(g));
                fassung = nd.Fassung;
                erledigt.Add((q, Netzkoepfe.Basis(g)));
            }
            var an = Bauteilangaben.Von(k, pr, fassung, e);
            if (an.Bekleidung) continue;
            b.Bauteile.Add(new Bauteil(e.Id, e.Kennzeichen, pr?.Entitaet ?? e.Klasse, an, liste.Finde(an.Typname), Befund(an, true),
                aussparungen.GetValueOrDefault(e.Id), durchgaenge.GetValueOrDefault(e.Id)));
        }
        foreach (var (ulId, nd) in koepfe)
        {
            var ul = m.Finde(ulId)!;
            foreach (var pr in nd.Produkte)
            {
                if (pr.Uebernommen || erledigt.Contains((ulId, pr.GlobalId))) continue;
                if (pr.Rolle == IfcNetzRolle.Teil && pr.Bezug is not null) continue;   // im Ganzen geprüft
                if (!Kollisionssuche.QuerbareEntitaeten.Contains(pr.Entitaet) || Pruefkoerperbau.IstBekleidung(pr.Entitaet)) continue;
                var an = Bauteilangaben.Von(k, pr, nd.Fassung);
                var kennung = $"{ulId}:{pr.GlobalId}";
                b.Bauteile.Add(new Bauteil(kennung, $"{pr.Entitaet} {pr.GlobalId} ({ul.Kennzeichen})", pr.Entitaet, an, liste.Finde(an.Typname), Befund(an, false),
                    aussparungen.GetValueOrDefault(kennung), durchgaenge.GetValueOrDefault(kennung)));
            }
        }
        return b;
    }
}

public static class TypmaterialTexte
{
    public const string Setzen =
        "Setzt die Typenliste «Typname → Material» des Projekts (Entscheid Christian 19.10.2026): je Bauteiltyp der Architektur die Kategorie beton, mauerwerk " +
        "(Rohbau: Aussparung), leichtbau (Gips, Ständer), holz, stahl, daemmung (keine Aussparung), bekleidung (auch Unterdecken; keine Aussparung, Kollision nur " +
        "im Paar hlks-bekleidung), installationsvorwand (z. B. Sanitärvorwand; keine Aussparung, Kollision nur im Paar hlks-vorwand), bodenaufbau (Unterlagsboden, " +
        "Estrich, Trittschalldämmung über der Tragdecke; keine Aussparung, Kollision nur im Paar hlks-bekleidung) oder luftschicht (weder Kollision noch Aussparung " +
        "noch P20). Die Liste hat Vorrang vor der " +
        "Materialzuordnung im IFC und dem Typnamen; Schlüssel ist der Typname (gilt auch nach einer neuen Architektenfassung; ohne Doppelpunkt auch für " +
        "«Familie:Typname»). Jeder Eintrag braucht eine Quelle; kategorie null entfernt einen Eintrag. Die Abfrage typmaterial nennt die unbekannten Typen. " +
        "Wirkung mit aussparungen_vorschlagen und kollision_pruefen. Ein Paket, rückgängig machbar; zuerst als Trockenlauf ansehen.";
    public const string ParTypen = "[{ \"typname\": \"Typname wie im IFC\", \"kategorie\": \"beton | mauerwerk | leichtbau | holz | stahl | daemmung | bekleidung | " +
                                   "installationsvorwand | bodenaufbau | luftschicht | null (entfernen)\", \"quelle\": \"optional, sonst die gemeinsame\" }]";
    public const string ParQuelle = "Gemeinsame Quelle für alle Einträge ohne eigene (z. B. «Entscheid Christian 19.10.2026» oder die Projektfestlegung mit Datum)";
    public const string Abfrage =
        "Typenliste «Typname → Material» (Entscheid Christian 19.10.2026): die Einträge mit Kategorie, Wirkung, Quelle und Zahl der Bauteile, Aussparungen und " +
        "Durchgänge (Kollisionen typ durchdringung), die sie treffen; die unbekannten Typen des Projekts (Material unbekannt, aus den Netzdateien der IFC-Unterlagen " +
        "und den KICAD-Bauteilen) mit Anzahl Bauteile, Aussparungen und Durchgänge zum Festlegen (schluessel = Typname ohne «Familie:»); die Typen, deren Material aus " +
        "dem Typnamen stammt (zum Bestätigen); Einträge ohne Quelle und doppelte.";
    public const string ParBetroffen = "Nur Typen mit Aussparungen oder Durchgängen; Vorgabe false (alle)";
}

/// <summary>Befehl typmaterial_setzen (Regeln im Dateikopf).</summary>
public sealed class TypmaterialSetzen : IBefehl
{
    public string Name => "typmaterial_setzen";
    public string Beschreibung => TypmaterialTexte.Setzen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("typen", "liste", true, TypmaterialTexte.ParTypen),
        new("quelle", "text", false, TypmaterialTexte.ParQuelle),
    ];

    const string Beispiel = "typen: [{ \"typname\": \"2.3_IW 1.5/8/1.5 cm 110\", \"kategorie\": \"leichtbau\" }], quelle: \"Entscheid Christian 19.10.2026\"";

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var typen = p["typen"] as JsonArray ?? throw new HlksFehler("Parameter «typen» fehlt oder ist keine Liste.", Beispiel);
        if (typen.Count == 0) throw new HlksFehler("Parameter «typen» ist leer.", Beispiel);
        var gemeinsam = Param.Text(p, "quelle")?.Trim();
        var erlaubt = string.Join(", ", Typenliste.Kategorien.Keys);
        var fehler = new List<string>();
        var auftraege = new List<(string Typname, string? Kategorie, string? Quelle)>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        foreach (var x in typen)
        {
            i++;
            if (x is not JsonObject o) { fehler.Add($"Eintrag {i}: Form {{ \"typname\": …, \"kategorie\": … }}"); continue; }
            var typname = Text(o["typname"])?.Trim();
            if (string.IsNullOrEmpty(typname)) { fehler.Add($"Eintrag {i}: typname fehlt"); continue; }
            if (!gesehen.Add(Typenliste.Schluessel(typname))) { fehler.Add($"«{typname}» doppelt (Gross-/Kleinschreibung und Leerschläge zählen nicht)"); continue; }
            if (!o.ContainsKey("kategorie")) { fehler.Add($"«{typname}»: kategorie fehlt (null entfernt den Eintrag)"); continue; }
            if (o["kategorie"] is not null && Text(o["kategorie"]) is null) { fehler.Add($"«{typname}»: kategorie muss ein Text sein (erlaubt: {erlaubt})"); continue; }
            var kat = Text(o["kategorie"])?.Trim().ToLowerInvariant().Replace("ä", "ae");
            if (string.IsNullOrEmpty(kat)) { auftraege.Add((typname, null, null)); continue; }
            if (!Typenliste.Kategorien.ContainsKey(kat)) { fehler.Add($"«{typname}»: Kategorie «{kat}» gibt es nicht (erlaubt: {erlaubt})"); continue; }
            var quelle = Text(o["quelle"])?.Trim() is { Length: > 0 } q ? q : gemeinsam;
            if (string.IsNullOrEmpty(quelle)) { fehler.Add($"«{typname}»: Quelle fehlt (eigene «quelle» oder die gemeinsame)"); continue; }
            auftraege.Add((typname, kat, quelle));
        }
        if (fehler.Count > 0)
            throw new HlksFehler($"Typenliste nicht gesetzt: {string.Join("; ", fehler)}.",
                $"Je Eintrag typname und kategorie ({erlaubt}) mit Quelle – {Beispiel}. Keine Festlegung aus dem Gedächtnis; fehlt sie, den Typ weglassen.");

        var liste = Typenliste.Lesen(k.Arbeit);
        var meldungen = new List<string>();
        var gesetzt = new List<(string Typname, string Kategorie)>();
        foreach (var (typname, kat, quelle) in auftraege)
        {
            var vorhanden = liste.ElementeZu(typname);
            if (kat is null)
            {
                if (vorhanden.Count == 0) { meldungen.Add($"«{typname}»: nicht in der Typenliste – nichts zu entfernen."); continue; }
                foreach (var e in vorhanden) k.Loeschen(e);
                meldungen.Add($"{string.Join(", ", vorhanden.Select(e => e.Kennzeichen))}: «{typname}» aus der Typenliste entfernt – es gilt wieder Materialzuordnung bzw. Typname.");
                continue;
            }
            var haupt = liste.Genau(typname)?.Element ?? vorhanden.FirstOrDefault();
            var weitere = vorhanden.Where(x => x.Id != haupt?.Id).ToList();
            var alteKat = haupt?.Text("kategorie");
            bool gleich = haupt is not null && alteKat == kat && haupt.Eigenschaft("kategorie")?.Herkunft.Quelle?.Trim() == quelle && haupt.Text("typname") == typname;
            if (gleich && weitere.Count == 0) { meldungen.Add($"{haupt!.Kennzeichen}: «{typname}» → {kat} unverändert."); continue; }
            var e2 = haupt?.Kopie() ?? new Element
            {
                Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen(Typenliste.Kuerzel), Klasse = Typenliste.Klasse, Angelegt = k.Herkunft,
            };
            if (!gleich)
            {
                var h = k.Herkunft with { Quelle = quelle };
                e2.Name = typname;
                e2.Eigenschaften["typname"] = new Wert { Inhalt = JsonValue.Create(typname), Herkunft = h };
                e2.Eigenschaften["kategorie"] = new Wert { Inhalt = JsonValue.Create(kat), Herkunft = h };
                e2 = k.Setzen(e2);
            }
            foreach (var w in weitere) k.Loeschen(w);
            var was = haupt is null ? "neu" : alteKat != kat ? $"war {alteKat ?? "ohne Kategorie"}" : gleich ? "unverändert" : "Quelle bzw. Schreibweise geändert";
            meldungen.Add($"{e2.Kennzeichen}: «{typname}» → {kat} (Quelle: {quelle}; {was}) – {Typenliste.Wirkung(kat)}." +
                          (weitere.Count > 0 ? $" Doppelte Einträge {string.Join(", ", weitere.Select(x => x.Kennzeichen))} gelöscht." : ""));
            gesetzt.Add((typname, kat));
        }
        if (k.Operationen.Count == 0) { k.Hinweise.Add("Keine Änderung."); k.Hinweise.AddRange(meldungen); return; }
        k.Hinweise.AddRange(meldungen);

        // betroffene Bauteile je gesetztem Typ (nach der neuen Liste)
        if (gesetzt.Count > 0)
        {
            var neu = Typenliste.Lesen(k.Arbeit);
            var bestand = Typenbestand.Lesen(k.Projekt, k.Arbeit, neu, mitBefund: false);
            foreach (var (typname, kat) in gesetzt)
            {
                var eintrag = neu.Genau(typname);
                var treffer = bestand.Bauteile.Where(b => eintrag is not null && b.Eintrag?.Element.Id == eintrag.Element.Id).ToList();
                if (treffer.Count == 0)
                    k.Hinweise.Add($"«{typname}» passt auf kein Bauteil der Unterlagen bzw. von KICAD – Schreibweise prüfen (Abfrage typmaterial zeigt die Typnamen).");
                else
                    k.Hinweise.Add($"«{typname}» ({kat}): {treffer.Count} Bauteil(e), {treffer.Sum(b => b.Aussparungen)} Aussparung(en), {treffer.Sum(b => b.Durchgaenge)} Durchgang/Durchgänge " +
                                   $"({string.Join(", ", treffer.Select(b => b.Angaben.Typname).Distinct().Take(3).Select(t => $"«{t}»"))}).");
            }
            k.Hinweise.AddRange(bestand.Hinweise);
        }
        k.Hinweise.Add("Wirkung mit aussparungen_vorschlagen (Vorschläge in Bauteilen ohne Aussparung werden gelöscht, freigegebene bleiben) und kollision_pruefen " +
                       "(Material, rohbau und Paar der Kollisionen; P20 folgt).");
    }
}

/// <summary>Abfrage typmaterial (Regeln im Dateikopf).</summary>
public static class TypmaterialAbfrage
{
    public static JsonObject Lesen(Projekt p, bool betroffen = false)
    {
        var m = p.Modell;
        var liste = Typenliste.Lesen(m);
        var bestand = Typenbestand.Lesen(p, m, liste, mitBefund: true);
        var bauteile = bestand.Bauteile;
        static string Zeit(Herkunft h) => $"{h.Art}, {h.Akteur}, {h.Zeit.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
        static JsonArray Texte(IEnumerable<string> l) => new(l.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
        static string Je(IEnumerable<string?> werte) => string.Join(", ", werte.GroupBy(x => x ?? "?").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {g.Count()}"));
        static int Gewicht(IEnumerable<Typenbestand.Bauteil> l) => l.Sum(b => b.Aussparungen + b.Durchgaenge);

        // Einträge der Typenliste mit ihrer Wirkung im Projekt
        var typen = new JsonArray();
        var hinweise = new List<string>();
        foreach (var t in liste.Eintraege)
        {
            var treffer = bauteile.Where(b => b.Eintrag?.Element.Id == t.Element.Id).ToList();
            typen.Add(new JsonObject
            {
                ["element"] = t.Kennzeichen, ["typname"] = t.Typname, ["kategorie"] = t.Kategorie, ["gruppe"] = Materialwortliste.Name(t.Gruppe),
                ["aussparung"] = Materialwortliste.Rohbau(t.Gruppe), ["wirkung"] = Typenliste.Wirkung(t.Kategorie), ["quelle"] = t.Quelle, ["gesetzt"] = Zeit(t.Herkunft),
                ["bauteile"] = treffer.Count, ["aussparungen"] = treffer.Sum(b => b.Aussparungen), ["durchgaenge"] = treffer.Sum(b => b.Durchgaenge),
                ["typnamen"] = Texte(treffer.Select(b => b.Angaben.Typname!).Distinct().Order(StringComparer.Ordinal)),
                ["entitaeten"] = Je(treffer.Select(b => b.Entitaet)),
            });
            if (treffer.Count == 0) hinweise.Add($"{t.Kennzeichen} «{t.Typname}» passt auf kein Bauteil der Unterlagen bzw. von KICAD – Schreibweise prüfen.");
        }

        // unbekannte Typen und Typen mit Material aus dem Typnamen, je Schlüssel (Typname ohne «Familie:»)
        JsonArray Gruppen(IEnumerable<Typenbestand.Bauteil> auswahl, bool unbekannt)
        {
            var a = new JsonArray();
            foreach (var g in auswahl.GroupBy(b => Typenliste.Schluessel(Typenliste.Kern(b.Angaben.Typname!)))
                         .Where(g => !betroffen || Gewicht(g) > 0)
                         .OrderByDescending(Gewicht).ThenByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                var o = new JsonObject
                {
                    ["schluessel"] = Typenliste.Kern(g.First().Angaben.Typname!),
                    ["typnamen"] = Texte(g.Select(b => b.Angaben.Typname!).Distinct().Order(StringComparer.Ordinal)),
                    ["entitaeten"] = Je(g.Select(b => b.Entitaet)),
                    ["bauteile"] = g.Count(), ["aussparungen"] = g.Sum(b => b.Aussparungen), ["durchgaenge"] = g.Sum(b => b.Durchgaenge),
                };
                if (unbekannt)
                    o["material"] = Je(g.Select(b => b.Angaben.Materialien.Count == 0 ? "(keine Zuordnung)" : string.Join(" | ", b.Angaben.Materialien)));
                else
                {
                    var gr = g.Select(b => b.Befund!.Gruppe).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
                    o["gruppe"] = string.Join(", ", gr.Select(Materialwortliste.Name));
                    o["kategorie"] = gr.Count == 1 ? Typenliste.KategorieVon(gr[0]) : null;
                    o["aussparung"] = g.Any(b => b.Befund!.Aussparung);
                    // Vermerk der Wortliste (Nachtrag B3e): ANNAHME (Bodenaufbau, Kühldecke …) zum Prüfen; die bestätigten Wörter
                    // (Installationsvorwand, Unterdecke, Luftschicht) sind Entscheide Christians und stehen in «vermerk»
                    var vermerke = g.Select(b => b.Befund!.Vermerk).OfType<string>().Distinct().ToList();
                    if (vermerke.Count > 0) o["vermerk"] = string.Join("; ", vermerke);
                    if (vermerke.Any(v => v.Contains("ANNAHME", StringComparison.Ordinal))) o["annahme"] = string.Join("; ", vermerke.Where(v => v.Contains("ANNAHME", StringComparison.Ordinal)));
                }
                a.Add(o);
            }
            return a;
        }
        var mitTyp = bauteile.Where(b => b.Angaben.Typname is { Length: > 0 }).ToList();
        var unbekannt = Gruppen(mitTyp.Where(b => b.Befund!.Unbekannt), true);
        var ausTypname = Gruppen(mitTyp.Where(b => b.Befund!.Herkunft == Materialherkunft.Typname), false);
        int ohneTyp = bauteile.Count(b => b.Befund!.Unbekannt && b.Angaben.Typname is not { Length: > 0 });

        if (liste.Eintraege.Count == 0) hinweise.Insert(0, "Die Typenliste ist leer – mit typmaterial_setzen festlegen (je Typ Kategorie und Quelle).");
        foreach (var e in liste.OhneQuelle) hinweise.Add($"{e.Kennzeichen} «{e.Text("typname")}» ohne Quelle – gilt nicht; mit typmaterial_setzen und Quelle neu setzen.");
        foreach (var e in liste.Ungueltig) hinweise.Add($"{e.Kennzeichen}: typname bzw. kategorie fehlt oder ist unbekannt – gilt nicht.");
        foreach (var (gilt, weitere) in liste.Doppelt)
            hinweise.Add($"«{gilt.Typname}» steht mehrfach ({gilt.Kennzeichen}, {string.Join(", ", weitere.Select(x => x.Kennzeichen))}) – es gilt {gilt.Kennzeichen} " +
                         "(zuletzt gesetzt); typmaterial_setzen für diesen Typ räumt auf.");
        if (ohneTyp > 0) hinweise.Add($"{ohneTyp} Bauteil(e) mit unbekanntem Material ohne Typname – Material am Bauteil setzen (material bzw. Aufbau).");
        hinweise.AddRange(bestand.Hinweise);
        hinweise.Add("Durchgänge = Kollisionen typ durchdringung des letzten Prüflaufs (kollision_pruefen); Aussparungen = gültige Aussparungen (aussparungen_vorschlagen).");

        var o2 = Ableitungen.Kopf(p, "Typenliste");
        o2["filter"] = new JsonObject { ["betroffen"] = betroffen };
        o2["typen"] = typen;
        o2["unbekannt"] = unbekannt;
        o2["ausTypname"] = ausTypname;
        o2["zusammenfassung"] = new JsonObject
        {
            ["eintraege"] = liste.Eintraege.Count, ["bauteile"] = bauteile.Count, ["mitTypname"] = mitTyp.Count,
            ["jeHerkunft"] = new JsonObject(bauteile.GroupBy(b => b.Befund!.Herkunft).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))),
            ["unbekannt"] = bauteile.Count(b => b.Befund!.Unbekannt), ["unbekannteTypen"] = mitTyp.Where(b => b.Befund!.Unbekannt)
                .Select(b => Typenliste.Schluessel(Typenliste.Kern(b.Angaben.Typname!))).Distinct().Count(),
            ["unbekanntOhneTypname"] = ohneTyp,
        };
        o2["kategorien"] = new JsonArray(Typenliste.Kategorien.Select(x => (JsonNode)new JsonObject
        {
            ["kategorie"] = x.Key, ["gruppe"] = Materialwortliste.Name(x.Value), ["aussparung"] = Materialwortliste.Rohbau(x.Value), ["wirkung"] = Typenliste.Wirkung(x.Key),
        }).ToArray());
        o2["ohneQuelle"] = Texte(liste.OhneQuelle.Select(e => e.Kennzeichen));
        o2["doppelt"] = new JsonArray(liste.Doppelt.Select(d => (JsonNode)new JsonObject
        {
            ["typname"] = d.Gilt.Typname, ["gilt"] = d.Gilt.Kennzeichen, ["weitere"] = Texte(d.Weitere.Select(x => x.Kennzeichen)),
        }).ToArray());
        o2["hinweise"] = Texte(hinweise);
        return o2;
    }
}
