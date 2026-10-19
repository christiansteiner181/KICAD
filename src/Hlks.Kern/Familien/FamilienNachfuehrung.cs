// Familien (Modul 2, B1): Nachführung im selben Paket (Nachfuehrungen.Liste). Ändert ein Befehl
//   - die Definition einer Familie → alle Elemente mit dieser Familie,
//   - an einem Element bezuege.familie, familientyp oder die Exemplarwerte (parameter) → dieses Element,
// werden Anschlüsse (Name, Medium, Fluss, DN bzw. Kanal d, b, h aus der Familie) und die Eigenschaften der Parameter mit «ziel» (Herkunft
// familie, Quelle «<Familie> Typ <typ>») nachgeführt. Fällt ein verbundener Anschluss weg, wird die Verbindung getrennt
// (Hinweis). Wird eine Familie gelöscht, bleiben ihre Elemente: der Bezug entfällt (Hinweis), sie zeigen die
// Klassendarstellung; Werte und Anschlüsse bleiben, wie sie sind.
//
// Prüfungen (Befehl wird abgelehnt): Familie vorhanden, Klasse des Elements IstArt Kategorie, Typ vorhanden, Exemplarwerte
// gültig, Typ auswertbar. Ändert der Befehl nur die Familie, entfallen ungültig gewordene Exemplarwerte mit Hinweis
// (Annahme); ein verwendeter Typ darf nicht verschwinden.
// Modul 3 (B1): Leitungen (Rohr, Kanal) mit Serie bekommen ihre Grösse aus dem Typ (Rohr dn, Kanal rund durchmesser, Kanal
// rechteckig breite und hoehe; Herkunft familie) und die Ziel-Eigenschaften; ihre Anschlüsse anfang/ende bleiben (Leitungsserien
// haben keine Anschlüsse – nichts wird gelöscht oder getrennt), nur deren Grösse folgt. Eine neue bzw. geänderte ausrichtung
// wird geprüft (Familien.AusrichtungLesen). Elemente ohne Geometrie («nicht platziert») werden nachgeführt wie platzierte.
// ANNAHMEN (Prüfliste): Eigenschaften mit «ziel» gehören der Familie – ein Wert von Hand wird mit Hinweis ersetzt (wie Revit:
// Typparameter sind am Exemplar nicht änderbar); Eigenschaften mit Herkunft familie, deren Parameter kein «ziel» mehr hat,
// entfallen; fehlt familientyp und hat die Familie genau einen Typ, wird dieser gesetzt (Hinweis).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class FamilienNachfuehren : INachfuehrung
{
    public string Name => "familien";

    const int EinzelnBis = 10;

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        var m = k.Arbeit; var kat = k.Katalog;

        // 1. Geänderte Familien: Definition gültig, Kategorie stimmt
        var familien = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in geaendert)
        {
            if (m.Finde(id) is not { Klasse: Familien.Klasse } f) continue;
            var def = Familien.Definition(f, kat);
            if (!def.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {f.Kennzeichen} «{f.Name}»", def.Fehler);
            if (!string.Equals(f.Text("kategorie"), def.Kategorie, StringComparison.Ordinal))
                throw new HlksFehler($"kategorie von {f.Kennzeichen} ({f.Text("kategorie")}) weicht von der Definition ab ({def.Kategorie}).",
                    "Die Kategorie steht in der Definition: familie_aendern mit { op: kopf_setzen, kategorie: … }.");
            familien.Add(f.Id);
        }

        // 2. Gelöschte Familien: Elemente bleiben, Bezug entfällt
        foreach (var id in geloescht)
        {
            var vorher = k.Operationen.LastOrDefault(o => o.Art == "element.loeschen" && o.Id == id)?.ElementVorher;
            if (vorher?.Klasse != Familien.Klasse) continue;
            var nutzer = m.Elemente.Where(x => x.Bezuege.GetValueOrDefault(Familien.Bezug) == id).ToList();
            foreach (var x in nutzer)
            {
                var c = x.Kopie();
                c.Bezuege.Remove(Familien.Bezug);
                k.Setzen(c);
            }
            if (nutzer.Count > 0)
                Hinweis(k, $"Familie {vorher.Kennzeichen} «{vorher.Name}» gelöscht: {nutzer.Count} Element(e) bleiben und zeigen die Klassendarstellung " +
                    $"({string.Join(", ", nutzer.Take(5).Select(x => x.Kennzeichen))}{(nutzer.Count > 5 ? " …" : "")}); Werte und Anschlüsse bleiben.");
        }

        // 3. Betroffene Elemente
        var selbst = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in geaendert)
            if (m.Finde(id) is { } e && e.Klasse != Familien.Klasse && !IstUnterlage(kat, e))
            {
                // ausrichtung { x, z } nur an familienfähigen Klassen (andere Klassen haben eigene Eigenschaften dieses Namens, z. B. Textnotiz)
                bool familienfaehig = kat.Klassen.TryGetValue(e.Klasse, out var kd) && kd.Familie;
                if (familienfaehig) AusrichtungPruefen(k, e, Herkunft(k, id));
                if (e.Bezuege.ContainsKey(Familien.Bezug)) selbst.Add(id);
                else if (familienfaehig && (e.Eigenschaft("familientyp") is not null || e.Eigenschaft("ausrichtung") is not null) && Herkunft(k, id) is { } alt && !alt.Bezuege.ContainsKey(Familien.Bezug) && GeaendertFamilienfeld(alt, e))
                    Hinweis(k, $"{e.Kennzeichen}: familientyp, parameter und ausrichtung wirken nur mit einer Familie (bezuege.familie).");
            }
        var betroffen = new List<(Element E, bool Selbst)>();
        foreach (var id in selbst) betroffen.Add((m.Finde(id)!, true));
        if (familien.Count > 0)
            foreach (var e in m.Elemente.ToList())
                if (!selbst.Contains(e.Id) && !IstUnterlage(kat, e) && e.Bezuege.TryGetValue(Familien.Bezug, out var fid) && familien.Contains(fid)) betroffen.Add((e, false));

        var nachgefuehrt = new List<string>();
        foreach (var (e, s) in betroffen.OrderBy(x => x.E.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            if (Element(k, e, s)) nachgefuehrt.Add(e.Kennzeichen);
        if (nachgefuehrt.Count > 0 && familien.Count > 0)
            Hinweis(k, nachgefuehrt.Count <= EinzelnBis
                ? $"Aus der Familie nachgeführt (Anschlüsse, Kennwerte): {string.Join(", ", nachgefuehrt)}"
                : $"Aus der Familie nachgeführt (Anschlüsse, Kennwerte): {nachgefuehrt.Count} Elemente ({string.Join(", ", nachgefuehrt.Take(3))} …)");
    }

    /// <summary>Eine Unterlage mit bezuege.familie ist eine Herstellerunterlage dieser Familie (Modul 2, zweck familie), kein
    /// platziertes Familienelement – sie wird nicht nachgeführt (Befund C2, Hauptlauf 18.10.2026).</summary>
    static bool IstUnterlage(Katalog kat, Element e) => kat.IstArt(e, "Unterlage");

    static Element? Herkunft(Kontext k, string id) => k.Operationen.FirstOrDefault(o => o.Id == id && o.Art == "element.setzen")?.ElementVorher;

    static bool GeaendertFamilienfeld(Element alt, Element neu) =>
        alt.Eigenschaft("familientyp")?.Inhalt?.ToJsonString() != neu.Eigenschaft("familientyp")?.Inhalt?.ToJsonString()
        || alt.Eigenschaft("parameter")?.Inhalt?.ToJsonString() != neu.Eigenschaft("parameter")?.Inhalt?.ToJsonString()
        || alt.Eigenschaft("ausrichtung")?.Inhalt?.ToJsonString() != neu.Eigenschaft("ausrichtung")?.Inhalt?.ToJsonString();

    /// <summary>Volle Ausrichtung (Modul 3): neu gesetzt bzw. geändert → prüfen (Längen &gt; 0, rechtwinklig); lehnt den Befehl ab.</summary>
    static void AusrichtungPruefen(Kontext k, Element e, Element? vorher)
    {
        if (e.Eigenschaft("ausrichtung")?.Inhalt is not { } n) return;
        if (vorher?.Eigenschaft("ausrichtung")?.Inhalt?.ToJsonString() == n.ToJsonString()) return;
        Familien.AusrichtungLesen(n, $"{e.Kennzeichen}: ausrichtung");
        if (k.Katalog.IstArt(e, "Leitung"))
            Hinweis(k, $"{e.Kennzeichen}: ausrichtung wirkt bei Leitungen nicht – ihre Lage kommt aus dem Verlauf (geometrie).");
    }

    static void Hinweis(Kontext k, string text) { if (!k.Hinweise.Contains(text)) k.Hinweise.Add(text); }

    /// <summary>Ein Element nachführen; true, wenn es geändert wurde. selbst = der Befehl hat das Element geändert (strenge Prüfung).</summary>
    static bool Element(Kontext k, Element e, bool selbst)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        if (!kat.Klassen.TryGetValue(e.Klasse, out var d) || !d.Familie)
            throw new HlksFehler($"{e.Kennzeichen} ({e.Klasse}) kann keine Familie tragen.",
                "Familienfähige Klassen: " + string.Join(", ", kat.Klassen.Values.Where(x => x.Familie && !x.Abstrakt).Select(x => x.Name).Order()));
        var f = m.Finde(e.Bezuege[Familien.Bezug]);
        if (f is null || f.Klasse != Familien.Klasse)
            throw new HlksFehler($"bezuege.familie von {e.Kennzeichen} zeigt nicht auf eine Familie{(f is null ? "" : $" ({f.Kennzeichen} ist ein {f.Klasse})")}.",
                "Eine Familie angeben (Kennzeichen FAM-n); die Abfrage familien listet sie.");
        var def = Familien.Definition(f, kat);
        if (!def.Gueltig)
        {
            // nur erreichbar, wenn die Familie schon vorher ungültig war (z. B. aus einem fremden Paket): Element nicht blockieren
            Hinweis(k, $"{e.Kennzeichen}: Familie {f.Kennzeichen} ist ungültig ({def.Fehler[0]}) – das Element zeigt die Klassendarstellung.");
            return false;
        }
        if (!kat.IstArt(e, def.Kategorie))
            throw new HlksFehler($"{e.Kennzeichen} ist ein {e.Klasse}, die Familie {f.Kennzeichen} «{f.Name}» gehört zur Kategorie {def.Kategorie}.",
                $"Eine Familie der Kategorie {e.Klasse} wählen oder das Element als {def.Kategorie} anlegen.");
        var neu = e.Kopie();
        bool geaendert = false;
        var typ = Familien.Typ(e);
        var typen = string.Join(", ", def.Typen.Select(t => t.Name));
        if (typ is null)
        {
            if (def.Typen.Count != 1) throw new HlksFehler($"{e.Kennzeichen}: familientyp fehlt.", $"Typ der Familie {f.Kennzeichen} angeben (eigenschaften.familientyp): {typen}");
            typ = def.Typen[0].Name;
            neu.Eigenschaften["familientyp"] = new Wert { Inhalt = JsonValue.Create(typ), Herkunft = k.Herkunft };
            geaendert = true;
            Hinweis(k, $"{e.Kennzeichen}: Typ «{typ}» gesetzt (einziger Typ der Familie {f.Kennzeichen}).");
        }
        else if (def.Typ(typ) is null)
            throw selbst
                ? new HlksFehler($"Typ «{typ}» gibt es in der Familie {f.Kennzeichen} «{f.Name}» nicht.", $"Typen: {typen}")
                : new HlksFehler($"Typ «{typ}» wird von {e.Kennzeichen} verwendet und fehlt in der neuen Definition von {f.Kennzeichen}.",
                    $"Den Typ behalten oder {e.Kennzeichen} zuerst auf einen anderen Typ umstellen (element_aendern eigenschaften.familientyp).");

        var ex = Familien.Exemplarwerte(e);
        var exFehler = FamilienAuswertung.ExemplarPruefen(def, ex);
        if (exFehler.Count > 0)
        {
            if (selbst) throw FamilienPruefung.Fehlermeldung($"Exemplarwerte von {e.Kennzeichen}", exFehler);
            var bereinigt = (JsonObject)ex!.DeepClone();
            foreach (var x in exFehler) bereinigt.Remove(x.Stelle["parameter.".Length..]);
            if (bereinigt.Count == 0) neu.Eigenschaften.Remove("parameter");
            else neu.Eigenschaften["parameter"] = new Wert { Inhalt = bereinigt, Herkunft = k.Herkunft };
            ex = bereinigt.Count == 0 ? null : bereinigt;
            geaendert = true;
            Hinweis(k, $"{e.Kennzeichen}: Exemplarwerte passen nicht mehr zur Familie und entfallen – {string.Join("; ", exFehler.Select(x => x.Meldung))}.");
        }
        // Ohne Geometrie = «nicht platziert» (Modul 3: Elemente zuerst nur im Schema) – Anschlüsse und Kennwerte gelten trotzdem,
        // nur Lagen gibt es keine (Anschlusslagen null).
        var a = FamilienAuswertung.Aufloesen(def, typ, ex);
        if (!a.Gueltig) throw FamilienPruefung.Fehlermeldung($"{e.Kennzeichen} (Familie {f.Kennzeichen}, Typ «{typ}»)", a.Fehler);

        // Leitung mit Serie (Modul 3): Grösse aus dem Typ; die Anschlüsse anfang/ende bleiben (Leitungsserien haben keine)
        var leitung = kat.IstArt(e, "Leitung");
        if (leitung && !def.IstLeitungsserie)
            throw new HlksFehler($"{e.Kennzeichen} ist eine Leitung, die Familie {f.Kennzeichen} «{f.Name}» ist keine Leitungsserie.", "Eine Leitungsserie (Kategorie Rohr bzw. Kanal mit Block leitung) angeben; familien listet sie.");
        if (leitung) geaendert |= LeitungsgroesseAnschluesse(e, neu, def, a);

        // Anschlüsse aus der Familie
        var sysMedium = Netz.SystemMedium(m, e.System);
        var soll = a.Anschluesse.Where(x => x.Sichtbar).Select(x => new Anschluss
        {
            Name = x.Name, Medium = x.Medium == "*" ? sysMedium ?? "*" : x.Medium, Fluss = x.Fluss, Dn = Familien.Ganz(x.Dn), D = Familien.Ganz(x.D), B = Familien.Ganz(x.B), H = Familien.Ganz(x.H), Gewinde = x.Gewinde,
        }).ToList();
        if (!leitung && Json.Text(soll) != Json.Text(neu.Anschluesse))
        {
            foreach (var alt in e.Anschluesse.Where(alt => !soll.Any(s => s.Name.Equals(alt.Name, StringComparison.OrdinalIgnoreCase))))
                if (m.VerbindungAn(e.Id, alt.Name) is { } v)
                {
                    var gegen = v.Gegenueber(e.Id, alt.Name);
                    k.Trennen(v);
                    Hinweis(k, $"{e.Kennzeichen}.{alt.Name} gibt es in der Familie nicht mehr: Verbindung mit {m.Finde(gegen?.Element)?.Kennzeichen}.{gegen?.Anschluss} getrennt.");
                }
            neu.Anschluesse = soll;
            geaendert = true;
        }

        // Eigenschaften der Parameter mit «ziel»
        var quelle = $"{f.Name ?? def.Name} Typ {typ}";
        var ziele = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in def.Parameter.Where(p => p.Ziel is not null))
        {
            var ed = d.Eigenschaften.First(x => x.Name.Equals(p.Ziel, StringComparison.OrdinalIgnoreCase));
            ziele.Add(ed.Name);
            var w = a.Wert(p.Name);
            var bisher = neu.Eigenschaft(ed.Name);
            if (w is not { } wert)
            {
                if (bisher?.Herkunft.Art == "familie") { neu.Eigenschaften.Remove(ed.Name); geaendert = true; }
                continue;
            }
            JsonNode inhalt;
            switch (ed.Typ)
            {
                case "zahl": inhalt = JsonValue.Create(wert.Zahl); break;
                case "bool": inhalt = JsonValue.Create(wert.Zahl != 0); break;
                case "auswahl":
                    var treffer = ed.Auswahl?.FirstOrDefault(x => x.Equals(wert.Text, StringComparison.OrdinalIgnoreCase))
                        ?? throw new HlksFehler($"{e.Kennzeichen}: «{wert.Text}» aus «{p.Name}» ist für {ed.Name} nicht erlaubt.", "Erlaubt: " + string.Join(", ", ed.Auswahl ?? []));
                    inhalt = JsonValue.Create(treffer);
                    break;
                default: inhalt = JsonValue.Create(wert.Text ?? wert.ToString()); break;
            }
            if (bisher is not null && bisher.Herkunft.Art == "familie" && bisher.Herkunft.Quelle == quelle && bisher.Inhalt?.ToJsonString() == inhalt.ToJsonString()) continue;
            if (bisher is not null && bisher.Herkunft.Art != "familie" && bisher.Inhalt?.ToJsonString() != inhalt.ToJsonString())
                Hinweis(k, $"{e.Kennzeichen}: {ed.Name} kommt aus der Familie ({inhalt.ToJsonString()}{(ed.Einheit is null ? "" : " " + ed.Einheit)}); der bisherige Wert {bisher} wird ersetzt.");
            neu.Eigenschaften[ed.Name] = new Wert { Inhalt = inhalt, Einheit = ed.Einheit ?? p.Einheit, Herkunft = k.Herkunft with { Art = "familie", Quelle = quelle } };
            geaendert = true;
        }

        // Grösse einer Leitung mit Serie (Modul 3): Rohr dn, Kanal rund durchmesser, Kanal rechteckig breite und hoehe – aus dem Typ,
        // Herkunft familie wie die Ziel-Eigenschaften (ein Wert von Hand wird mit Hinweis ersetzt)
        if (leitung && a.Leitung is { } lw)
            foreach (var (schl, wert) in lw.Groesse)
            {
                var name = schl switch { "dn" => "dn", "d" => "durchmesser", "b" => "breite", "h" => "hoehe", _ => schl };
                var ed = d.Eigenschaften.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (ed is null) { Hinweis(k, $"{e.Kennzeichen}: {e.Klasse} hat keine Eigenschaft «{name}» für die Grösse {schl} der Serie."); continue; }
                ziele.Add(ed.Name);
                JsonNode inhalt = JsonValue.Create(wert);
                var bisher = neu.Eigenschaft(ed.Name);
                if (bisher is not null && bisher.Herkunft.Art == "familie" && bisher.Herkunft.Quelle == quelle && bisher.Inhalt?.ToJsonString() == inhalt.ToJsonString()) continue;
                if (bisher is not null && bisher.Herkunft.Art != "familie" && bisher.Inhalt?.ToJsonString() != inhalt.ToJsonString())
                    Hinweis(k, $"{e.Kennzeichen}: {ed.Name} kommt aus der Serie ({inhalt.ToJsonString()}{(ed.Einheit is null ? "" : " " + ed.Einheit)}, Typ «{typ}»); der bisherige Wert {bisher} wird ersetzt.");
                neu.Eigenschaften[ed.Name] = new Wert { Inhalt = inhalt, Einheit = ed.Einheit, Herkunft = k.Herkunft with { Art = "familie", Quelle = quelle } };
                geaendert = true;
            }
        foreach (var (name, w) in neu.Eigenschaften.ToList())
            if (w.Herkunft.Art == "familie" && !ziele.Contains(name)) { neu.Eigenschaften.Remove(name); geaendert = true; }

        if (!geaendert) return false;
        k.Setzen(neu);
        return true;
    }

    /// <summary>Leitung mit Serie: die Anschlüsse anfang/ende bleiben (Name, Medium, Fluss, Verbindungen) – nur ihre Grösse folgt dem
    /// Typ (Rohr Dn; Kanal rund D; Kanal rechteckig B und H), wie es element_anlegen für dn schon tut. true = geändert.</summary>
    static bool LeitungsgroesseAnschluesse(Element e, Element neu, FamilieDef def, FamAufloesung a)
    {
        if (a.Leitung is not { } lw) return false;
        int? G(string s) => lw.Groesse.TryGetValue(s, out var x) ? Familien.Ganz(x) : null;
        bool geaendert = false;
        foreach (var an in neu.Anschluesse.Where(x => x.Name.Equals("anfang", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("ende", StringComparison.OrdinalIgnoreCase)))
        {
            int? dn = def.Leitung!.Groesse.ContainsKey("dn") ? G("dn") : an.Dn, dd = G("d"), b = G("b"), h = G("h");
            if (an.Dn == dn && an.D == dd && an.B == b && an.H == h) continue;
            an.Dn = dn; an.D = dd; an.B = b; an.H = h;
            geaendert = true;
        }
        return geaendert;
    }
}
