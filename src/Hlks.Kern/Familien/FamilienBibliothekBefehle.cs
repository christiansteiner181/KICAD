// Familien (Modul 2, Nachtrag B7, entwurf/modul-2-bibliothek.md): automatisches Schreiben in die Familienbibliothek und die
// Befehle familie_in_bibliothek, familie_aktualisieren, familienauswahl_setzen (familie_laden: FamilienBefehle.cs).
//
// ENTSCHEID (B7, begründet im Bericht): Marker im selben Paket, Schreiben als Nachlauf.
//   - Familie.bibliothek { id, version, pruefsumme, datei, zeit, art } führt die Nachführung FamilienBibliothekNachfuehren im SELBEN
//     Paket nach (Rückgängig nimmt Definition und Marker gemeinsam zurück, kein zweites Paket, kein Eintrag im Rückgängig-Stapel).
//     version ist die beim Befehl vorgesehene Version (Planung wie beim Schreiben, aus dem Index); massgebend ist der Stand aus der
//     Bibliothek über die Inhaltsprüfsumme (Abfragen familien und familien_bibliothek), der Marker ist der Stand beim Befehl.
//   - Geschrieben wird erst NACH dem gespeicherten Paket (Projekt.Uebernehmen ruft NachPaket): ein abgelehnter Befehl (Reservierung,
//     Ablage nicht erreichbar) oder ein Trockenlauf schreibt nichts; ein fremdes Paket (Synchronisieren) schreibt nie – es schreibt nur
//     der Rechner, der den Befehl ausgeführt hat. Doppel verhindert zusätzlich die Planung (gleiche Prüfsumme = keine neue Version).
//   - Rückgängig und Wiederholen sind neue Übergänge und schreiben eine weitere Version (Annahme 2); ausgenommen ist das Rückgängig
//     einer Übernahme (familie_laden, familie_aktualisieren) – die Bibliothek bleibt dann, wie sie ist.
//   - Kein Befehl scheitert am NAS: nicht erreichbar → Warteschlange (FamilienBibliothekDienst), Hinweis im Ergebnis.
// Nicht automatisch geschrieben: Akteur-Art «beispiel» (fiktive Werte des Beispielprojekts) und die Übernahmen selbst.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class FamilienBibliothekSchreiben
{
    /// <summary>Befehle, die eine Fassung aus der Bibliothek übernehmen (schreiben nicht zurück).</summary>
    public static readonly string[] Uebernahmen = ["familie_laden", "familie_aktualisieren"];
    /// <summary>Befehle, die den Marker selbst setzen (die Nachführung lässt sie aus).</summary>
    static readonly string[] MarkerSelbst = ["familie_laden", "familie_aktualisieren", "familie_in_bibliothek", "familie_speichern"];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    public static Element? Projektinformation(Modell m) => m.DerKlasse("Projektinformation").OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Projekt für log.json: Id, Name, Nummer (Projektinformation.projektnummer), Ordner der Ablage.</summary>
    public static BibProjekt ProjektVon(Projekt p, Modell? m = null) => new()
    {
        Id = p.Kopf.Id, Name = p.Kopf.Name, Nummer = Projektinformation(m ?? p.Modell)?.Text("projektnummer"), Ordner = p.Speicher?.Ort,
    };

    /// <summary>Unterlagen der Definition mit ihrer Datei im Projekt (Quelle; null, wenn sie dort fehlt).</summary>
    public static List<BibUnterlage> UnterlagenVon(Projekt p, JsonObject def)
    {
        var liste = new List<BibUnterlage>();
        BefundQuelle? q = null;
        foreach (var u in (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var ps = T(u["pruefsumme"]);
            if (ps is not { Length: >= 12 }) continue;
            var b = new BibUnterlage { Pruefsumme = ps, Datei = T(u["datei"]), Art = T(u["art"]) };
            try { b.Quelle = (q ??= new BefundQuelle(p)).Finden(ps[..12])?.Pfad; }
            catch (Exception ex) when (ex is HlksFehler or IOException or UnauthorizedAccessException) { }
            liste.Add(b);
        }
        return liste;
    }

    /// <summary>Auftrag für eine Fassung: neu = Stand nach dem Befehl, alt = vorher (Basis), marker = Marker der Basis.</summary>
    public static BibAuftrag Auftrag(Projekt p, Element neu, string? vorher, JsonObject? marker, string befehl, string akteur, string akteurArt, string geraet,
        DateTimeOffset zeit, bool alsAktuell, Modell? m = null, bool mitUnterlagen = true) => new()
    {
        Id = Familien.DefinitionsId(neu) ?? "",
        Definition = (neu.Eigenschaft("definition")?.Inhalt as JsonObject)?.DeepClone() as JsonObject,
        Pruefsumme = FamilienBibliothekLogik.Inhaltspruefsumme(neu),
        Vorher = vorher,
        MarkerVersion = FamilienBibliothekLogik.MarkerVersion(marker),
        MarkerAbzweig = T(marker?["abzweig"]),
        MarkerInhalt = T(marker?["inhalt"]) ?? T(marker?["pruefsumme"]),
        AlsAktuell = alsAktuell,
        Projekt = ProjektVon(p, m),
        Akteur = akteur, AkteurArt = akteurArt, Geraet = geraet,
        Zeit = zeit.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        Befehl = befehl,
        Freigabe = neu.Text("status") == "freigegeben" ? neu.Eigenschaft("freigabe")?.Inhalt?.DeepClone() as JsonObject : null,
        Unterlagen = mitUnterlagen && neu.Eigenschaft("definition")?.Inhalt is JsonObject d ? UnterlagenVon(p, d) : [],
    };

    /// <summary>Auftrag für ein Schreiben von Hand (familie_in_bibliothek, familie_speichern): die Basis kommt aus dem Marker. Beschreibt er
    /// die heutige Fassung (z. B. «ausstehend» aus dem Paket eines anderen Rechners), gilt seine Basis (basis, basisPruefsumme) – so wird
    /// derselbe Übergang nicht doppelt geschrieben; sonst gilt die Fassung des Markers als Basis.</summary>
    public static BibAuftrag AuftragVonHand(Kontext k, Element f, string befehl, bool alsAktuell)
    {
        var a = Auftrag(k.Projekt, f, null, null, befehl, k.Herkunft.Akteur, k.Herkunft.Art, k.Projekt.Geraet, k.Herkunft.Zeit, alsAktuell, k.Arbeit);
        var m = FamilienBibliothekLogik.Marker(f);
        if (m is null) return a;
        var markerInhalt = T(m["inhalt"]) ?? T(m["pruefsumme"]);
        if (markerInhalt == a.Pruefsumme)
        {
            a.Vorher = T(m["basisPruefsumme"]);
            a.MarkerVersion = Json.Zahl(m["basis"]) is double b ? (int)b : null;
            a.MarkerAbzweig = T(m["basisAbzweig"]);
            a.MarkerInhalt = a.Vorher;
        }
        else
        {
            a.Vorher = markerInhalt;
            a.MarkerVersion = FamilienBibliothekLogik.MarkerVersion(m);
            a.MarkerAbzweig = T(m["abzweig"]);
            a.MarkerInhalt = markerInhalt;
        }
        return a;
    }

    /// <summary>Marker Familie.bibliothek aus Plan bzw. Ergebnis.</summary>
    public static JsonObject Marker(string id, BibPlan plan, string ordner, string pruefsumme, DateTimeOffset zeit, string? art = null, string? vorher = null)
    {
        var m = new JsonObject
        {
            ["id"] = id,
            ["version"] = plan.Art == "abzweig" || plan.Abzweig is not null ? null : plan.Version,
            ["pruefsumme"] = pruefsumme,
            ["datei"] = ordner.Length > 0 ? ordner : null,
            ["zeit"] = zeit.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["art"] = art ?? plan.Art switch { "neu" or "gleich" => plan.Abzweig is null ? "geschrieben" : "abzweig", "abzweig" => "abzweig", _ => "ausstehend" },
        };
        if (plan.Abzweig is not null) m["abzweig"] = plan.Abzweig;
        if (plan.Basis is int b) m["basis"] = b;
        if (plan.BasisAbzweig is not null) m["basisAbzweig"] = plan.BasisAbzweig;
        if (vorher is not null && vorher != pruefsumme) m["basisPruefsumme"] = vorher;   // Inhalt, von dem die Fassung ausgeht (für ein späteres Schreiben von Hand)
        foreach (var leer in m.Where(x => x.Value is null).Select(x => x.Key).ToList()) m.Remove(leer);
        return m;
    }

    /// <summary>Gleicher Marker (ohne Zeit)?</summary>
    public static bool GleicherMarker(JsonObject? a, JsonObject? b)
    {
        if (a is null || b is null) return a is null && b is null;
        var x = (JsonObject)a.DeepClone(); var y = (JsonObject)b.DeepClone();
        x.Remove("zeit"); y.Remove("zeit");
        return JsonNode.DeepEquals(x, y);
    }

    /// <summary>Setzt den Marker einer Familie (im selben Paket), wenn er sich ändert.</summary>
    public static Element MarkerSetzen(Kontext k, Element fam, JsonObject marker)
    {
        var aktuell = k.Arbeit.Finde(fam.Id) ?? fam;
        if (GleicherMarker(FamilienBibliothekLogik.Marker(aktuell), marker)) return aktuell;
        var e = aktuell.Kopie();
        e.Eigenschaften["bibliothek"] = new Wert { Inhalt = marker, Herkunft = k.Herkunft };
        return k.Setzen(e);
    }

    /// <summary>Kurztext des Plans für Trockenlauf und Hinweise.</summary>
    public static string Vorschau(string name, BibPlan plan, BibliotheksOrt ort) => plan.Art switch
    {
        "neu" => $"Familienbibliothek: «{name}» würde als Version {plan.Version} geschrieben.",
        "gleich" => $"Familienbibliothek: «{name}» ist dort schon {(plan.Abzweig is null ? "als Version " + plan.Version : "als Abzweig " + plan.Abzweig)} – keine neue Version.",
        "abzweig" => $"Familienbibliothek: «{name}» würde als Abzweig {plan.Abzweig} gespeichert (parallel geändert: Version {plan.Aktuell} ist neuer als die Grundlage).",
        "gesperrt" => "Familienbibliothek: nicht geschrieben – " + (ort.Sperre ?? "gesperrt."),
        _ => $"Familienbibliothek nicht erreichbar ({ort.Pfad}): «{name}» würde in die Warteschlange gehen und beim nächsten Zugriff nachgeschrieben.",
    };

    /// <summary>Nachlauf nach einem gespeicherten eigenen Paket (Projekt.Uebernehmen; nie für fremde Pakete): jede Familie, deren
    /// Inhalt sich geändert hat, als neue Version in die Bibliothek (bzw. Warteschlange). Wirft nie; Rückgabe = Hinweise.</summary>
    public static List<string> NachPaket(Projekt p, Paket paket)
    {
        var hinweise = new List<string>();
        try
        {
            var ops = paket.Operationen.Where(o => o.Art == "element.setzen" && o.Element?.Klasse == Familien.Klasse).ToList();
            if (ops.Count == 0 || p.AkteurArt == "beispiel") return hinweise;
            var ursprung = paket.Befehl is "rueckgaengig" or "wiederholen" ? T(paket.Parameter?["befehl"]) ?? "" : paket.Befehl;
            var ort = FamilienBibliothekDienst.Ort(p.Katalog);
            foreach (var g in ops.GroupBy(o => o.Id))
            try
            {
                var alt = g.First().ElementVorher;
                var neu = g.Last().Element!;
                if (Familien.DefinitionsId(neu) is null) continue;
                // Übernahme: Entscheid über eigene Abzweige nachtragen (nur beim Befehl selbst, nicht bei rückgängig/wiederholen)
                if (paket.Befehl == "familie_aktualisieren")
                {
                    if (ort.Aktiv && FamilienBibliothekLogik.Marker(neu)?["verwirft"] is JsonArray vw && vw.Count > 0)
                    {
                        var a = new BibAuftrag
                        {
                            Art = "entscheid", Id = Familien.DefinitionsId(neu)!, Abzweige = vw.Select(x => T(x)).Where(x => x is not null).Select(x => x!).ToList(),
                            Ergebnis = "verworfen", Projekt = ProjektVon(p), Akteur = paket.Akteur, AkteurArt = paket.AkteurArt, Geraet = paket.Geraet, Befehl = paket.Befehl,
                            Zeit = paket.Zeit.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                            Definition = new JsonObject { ["name"] = neu.Name },
                        };
                        var r = FamilienBibliothekDienst.SchreibenOderEinreihen(ort, a);
                        if (r.Hinweis.Length > 0) hinweise.Add(r.Hinweis);
                    }
                    continue;
                }
                if (MarkerSelbst.Contains(ursprung)) continue;
                var neuP = FamilienBibliothekLogik.Inhaltspruefsumme(neu);
                var altP = FamilienBibliothekLogik.Inhaltspruefsumme(alt);
                if (neuP is null || neuP == altP) continue;
                if (FamilienBibliothekDienst.AutomatischGesperrt(ort, p) is { } grund)
                {
                    var t = "Familienbibliothek: nicht geschrieben – " + grund;
                    if (!hinweise.Contains(t)) hinweise.Add(t);
                    continue;
                }
                var auftrag = Auftrag(p, neu, altP, FamilienBibliothekLogik.Marker(alt), paket.Befehl == ursprung ? paket.Befehl : $"{paket.Befehl} ({ursprung})",
                    paket.Akteur, paket.AkteurArt, paket.Geraet, paket.Zeit, false);
                hinweise.Add(FamilienBibliothekDienst.SchreibenOderEinreihen(ort, auftrag).Hinweis);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or HlksFehler or InvalidOperationException or ArgumentException)
            {
                hinweise.Add($"Familienbibliothek: {g.Last().Element?.Name} nicht geschrieben ({ex.Message}) – mit familie_in_bibliothek nachholen.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or HlksFehler or InvalidOperationException or ArgumentException)
        {
            hinweise.Add($"Familienbibliothek: nicht geschrieben ({ex.Message}) – mit familie_in_bibliothek nachholen.");
        }
        return hinweise;
    }

    /// <summary>Eine Herstellerunterlage, die im Projekt liegt bzw. dorthin kopiert wurde.</summary>
    public sealed record UnterlageGeholt(string Praefix, string Name, string Original, string? Art, bool Kopiert);

    /// <summary>Herstellerunterlagen einer Fassung ins Projekt holen: fehlende Dateien aus _unterlagen nach unterlagen\ kopieren
    /// (Name «&lt;Prüfsumme 12&gt;_&lt;Original&gt;», Prüfsumme bleibt – so gelten die Befund-Ids «&lt;Prüfsumme 12&gt;#n» im Zielprojekt; die Befunde
    /// liest der Leser dort neu). Im Trockenlauf wird nichts kopiert. Vor dem Speichern der Definition aufrufen (die Prüfung der Quellen
    /// sucht die Dateien).</summary>
    public static (List<UnterlageGeholt> Geholt, List<string> Fehlend) DateienHolen(Kontext k, BibliotheksOrt ort, JsonObject def)
    {
        var geholt = new List<UnterlageGeholt>();
        var fehlend = new List<string>();
        string? ordner = null;
        foreach (var u in (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var ps = T(u["pruefsumme"]);
            if (ps is not { Length: >= 12 }) continue;
            var praefix = ps[..12].ToLowerInvariant();
            var original = T(u["datei"]) ?? (praefix + ".bin");
            var art = T(u["art"]) ?? UnterlagenArten.AusEndung(original);
            ordner ??= Unterlagen.Ordner(k.Projekt);
            var vorhanden = Directory.EnumerateFiles(ordner, praefix + "_*").Where(f => !Path.GetFileName(f).StartsWith('.'))
                .Where(f => UnterlagenArten.AusEndung(Path.GetFileName(f), f) is not null).OrderBy(f => f.Length).FirstOrDefault();
            if (vorhanden is not null) { geholt.Add(new UnterlageGeholt(praefix, Path.GetFileName(vorhanden), original, art, false)); continue; }
            var quelle = FamilienBibliothekDienst.UnterlageInBibliothek(ort, new BibUnterlage { Pruefsumme = ps, Datei = original, Art = art });
            if (quelle is null) { fehlend.Add(original); continue; }
            var name = $"{praefix}_{DateiSauber(original)}";
            if (!k.Trockenlauf)
            {
                var ziel = Path.Combine(ordner, name);
                if (!File.Exists(ziel))
                {
                    var temp = Path.Combine(ordner, $".{name}.{Guid.NewGuid():N}.tmp");
                    try
                    {
                        File.Copy(quelle, temp);
                        try { File.Move(temp, ziel, overwrite: false); }
                        catch (IOException) when (File.Exists(ziel)) { }
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
            }
            geholt.Add(new UnterlageGeholt(praefix, name, original, art, true));
        }
        return (geholt, fehlend);
    }

    /// <summary>Unterlagen als Element (Unterlage, zweck familie, Bezug auf die Familie) einfügen, wenn es zur Datei noch keines gibt.</summary>
    public static int ElementeEinfuegen(Kontext k, Element familie, IEnumerable<UnterlageGeholt> geholt, string befehl)
    {
        int n = 0;
        foreach (var g in geholt)
        {
            if (k.Arbeit.DerKlasse("Unterlage").Any(x => x.Text("datei") is { } d && d.StartsWith(g.Praefix + "_", StringComparison.OrdinalIgnoreCase))) continue;
            var herkunft = k.Herkunft with { Art = "import", Befehl = befehl, Quelle = $"Familienbibliothek: {g.Original}" };
            var ul = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("UL"), Klasse = "Unterlage", Name = g.Original, Angelegt = herkunft };
            void E(string name, JsonNode? w) { if (w is not null) ul.Eigenschaften[name] = new Wert { Inhalt = w, Herkunft = herkunft }; }
            E("datei", g.Name); E("original", "Familienbibliothek " + g.Original); E("art", g.Art); E("stand", g.Praefix); E("zweck", "familie"); E("anzeigen", false);
            ul.Bezuege[Familien.Bezug] = familie.Id;
            k.Setzen(ul);
            n++;
        }
        return n;
    }

    static string DateiSauber(string name)
    {
        var ungueltig = Path.GetInvalidFileNameChars();
        var s = new string(Path.GetFileName(name).Select(c => ungueltig.Contains(c) ? '_' : c).ToArray()).Trim().TrimStart('.');
        return s.Length == 0 ? "unterlage" : s;
    }

    /// <summary>Version bzw. Abzweig aus dem Parameter version («5», «v0005», Abzweigname); null = aktuelle Version.</summary>
    public static (int? Version, string? Abzweig) VersionLesen(BibIndexEintrag e, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return (e.Version, null);
        var v = version.Trim();
        if (v.EndsWith(".familie.json", StringComparison.OrdinalIgnoreCase)) v = v[..^".familie.json".Length];
        var zahl = v.StartsWith('v') || v.StartsWith('V') ? v[1..] : v;
        if (int.TryParse(zahl, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || (double.TryParse(zahl, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == Math.Floor(d) && (n = (int)d) == d))
        {
            if (e.Versionen.Any(x => x.Version == n)) return (n, null);
            throw new HlksFehler($"Version {n} von «{e.Name}» gibt es in der Bibliothek nicht.", $"Versionen: {string.Join(", ", e.Versionen.Select(x => x.Version))} (aktuell {e.Version}).");
        }
        if (e.Abzweige.FirstOrDefault(a => string.Equals(a.Abzweig, v, StringComparison.OrdinalIgnoreCase)) is { } ab) return (null, ab.Abzweig);
        throw new HlksFehler($"«{version}» ist weder eine Version noch ein Abzweig von «{e.Name}».",
            $"Versionen: {string.Join(", ", e.Versionen.Select(x => x.Version))}{(e.Abzweige.Count > 0 ? "; Abzweige: " + string.Join(", ", e.Abzweige.Select(a => a.Abzweig)) : "")}.");
    }

    /// <summary>Freigabe der Fassung aus dem Log (wer, wann) – damit ein anderes Projekt sieht, wer freigegeben hat.</summary>
    public static JsonObject? FreigabeAusLog(BibliotheksOrt ort, BibIndexEintrag e, int? version)
    {
        if (version is null) return null;
        var log = FamilienBibliothekDienst.Log(ort, e);
        var x = log?.Eintraege.LastOrDefault(l => l.Art == "version" && l.Version == version);
        if (x?.Freigabe is not JsonObject f) return null;
        var o = (JsonObject)f.DeepClone();
        o["bibliothek"] = new JsonObject { ["version"] = version, ["projekt"] = x.Projekt?.Name };
        return o;
    }

    /// <summary>Übernimmt eine Fassung aus der Bibliothek ins Projekt (familie_laden bzw. familie_aktualisieren): Unterlagen holen,
    /// Definition speichern (vertraute Quelle wie familie_laden), Marker, Freigabe aus dem Log. bisher = null: neu im Projekt.</summary>
    public static Element Uebernehmen(Kontext k, BibliotheksOrt ort, BibIndexEintrag e, int? version, string? abzweig, JsonObject def, Element? bisher,
        string befehl, IReadOnlyList<string> verwirft)
    {
        var libP = FamilienBibliothekLogik.Inhaltspruefsumme(def);
        var status = T(def["status"]) ?? "entwurf";
        int projektVersion = bisher is null ? (Json.Zahl(def["version"]) is double v && v >= 1 ? (int)v : 1) : FamilienBefehle.Version(bisher) + 1;
        var marker = Marker(e.Id, new BibPlan(abzweig is null ? "gleich" : "abzweig", version, abzweig, null, null, e.Version, []), e.Ordner, libP, k.Herkunft.Zeit, "geladen");
        if (verwirft.Count > 0) marker["verwirft"] = new JsonArray(verwirft.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        // Unterlagen zuerst (Dateien), damit die Prüfung der Quellen sie findet; Elemente danach mit Bezug auf die Familie.
        // Gespeichert wird als «familie_laden»: die Bibliothek ist eine vertraute Quelle (Entscheide und Abdeckung bleiben).
        var (geholt, fehlend) = DateienHolen(k, ort, def);
        var fam = FamilienBefehle.Speichern(k, bisher is null ? null : k.Arbeit.Finde(bisher.Id) ?? bisher, StatusPruefen(def), "familie_laden", status, projektVersion, marker);
        int kopiert = geholt.Count(g => g.Kopiert);
        int eingefuegt = ElementeEinfuegen(k, fam, geholt, befehl);
        fam = k.Arbeit.Finde(fam.Id)!;
        // die Prüfung kann die Definition ergänzen (Abdeckung, Unterlagen): der Marker merkt sich den Inhalt im Projekt
        var h = FamilienBibliothekLogik.Inhaltspruefsumme(fam);
        if (h is not null && h != libP)
        {
            var m2 = (JsonObject)marker.DeepClone();
            m2["inhalt"] = h;
            fam = MarkerSetzen(k, fam, m2);
        }
        if (status == "freigegeben" && FreigabeAusLog(ort, e, version) is { } fr)
        {
            var c = fam.Kopie();
            c.Eigenschaften["freigabe"] = new Wert { Inhalt = fr, Herkunft = k.Herkunft };
            fam = k.Setzen(c);
        }
        k.Hinweise.Add($"{fam.Kennzeichen} «{fam.Name}» aus der Familienbibliothek {(bisher is null ? "geladen" : "übernommen")}: " +
            $"{(abzweig is null ? $"Version {version}" : $"Abzweig {abzweig}")} ({e.Ordner}), Status {status}" +
            (kopiert + eingefuegt > 0 ? $"; {kopiert} Unterlage(n) {(k.Trockenlauf ? "würden ins Projekt kopiert" : "ins Projekt kopiert")}, {eingefuegt} als Unterlage eingefügt" : "") + "." +
            (fehlend.Count > 0 ? $" Nicht in der Bibliothek: {string.Join(", ", fehlend)} – Quellen dieser Unterlagen bleiben Verweis." : "") +
            (verwirft.Count > 0 ? $" Parallele Fassung(en) {string.Join(", ", verwirft)} werden als verworfen vermerkt (bleiben in der Bibliothek erhalten)." : ""));
        return fam;
    }

    static JsonObject StatusPruefen(JsonObject def)
    {
        var d = (JsonObject)def.DeepClone();
        if (T(d["status"]) is not ("entwurf" or "freigegeben")) d["status"] = "entwurf";
        return d;
    }
}

/// <summary>Nachführung (Nachfuehrungen.Liste): Familie.bibliothek im selben Paket, wenn ein Befehl den Inhalt einer Familie ändert.</summary>
public sealed class FamilienBibliothekNachfuehren : INachfuehrung
{
    public string Name => "familienbibliothek";

    static readonly string[] MarkerSelbst = ["familie_laden", "familie_aktualisieren", "familie_in_bibliothek", "familie_speichern"];

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        if (k.Projekt.AkteurArt == "beispiel" || MarkerSelbst.Contains(k.Herkunft.Befehl)) return;
        BibliotheksOrt? ort = null;
        foreach (var id in geaendert)
        {
            if (k.Arbeit.Finde(id) is not { Klasse: Familien.Klasse } f) continue;
            var famId = Familien.DefinitionsId(f);
            if (famId is null) continue;
            var vorher = k.Projekt.Modell.Finde(id);
            var neuP = FamilienBibliothekLogik.Inhaltspruefsumme(f);
            var altP = FamilienBibliothekLogik.Inhaltspruefsumme(vorher);
            if (neuP is null || neuP == altP) continue;
            ort ??= FamilienBibliothekDienst.Ort(k.Katalog);
            if (FamilienBibliothekDienst.AutomatischGesperrt(ort, k.Projekt) is not null) return;
            var a = FamilienBibliothekSchreiben.Auftrag(k.Projekt, f, altP, FamilienBibliothekLogik.Marker(vorher), k.Herkunft.Befehl ?? "", k.Herkunft.Akteur,
                k.Herkunft.Art, k.Projekt.Geraet, k.Herkunft.Zeit, false, k.Arbeit, mitUnterlagen: false);   // nur Vorhersage
            var plan = FamilienBibliothekDienst.Vorhersage(ort, a, out var ordner);
            var marker = FamilienBibliothekSchreiben.Marker(famId, plan, ordner, neuP, k.Herkunft.Zeit, null, altP);
            if (FamilienBibliothekSchreiben.GleicherMarker(FamilienBibliothekLogik.Marker(f), marker)) continue;
            FamilienBibliothekSchreiben.MarkerSetzen(k, f, marker);
            if (k.Trockenlauf) k.Hinweise.Add(FamilienBibliothekSchreiben.Vorschau(f.Name ?? famId, plan, ort));
        }
    }
}

public sealed class FamilieInBibliothek : IBefehl
{
    public string Name => "familie_in_bibliothek";
    public const string Text = "Schreibt Familien des Projekts von Hand in die Familienbibliothek (Bürobibliothek: kataloge/buero.json bzw. HLKS_FAMILIENBIBLIOTHEK) – z. B. die " +
        "Erstbefüllung aus bestehenden Projekten. Jede wirksame Änderung einer Familie schreibt der Kern ohnehin nach dem Befehl als neue Version (auch Entwürfe der KI). " +
        "Gleicher Inhalt = keine neue Version; wurde die Familie inzwischen anderswo geändert, wird die Fassung ein Abzweig («parallel geändert»). als_aktuell (nur ein Mensch): " +
        "die Fassung des Projekts wird die neue aktuelle Version, offene Abzweige gelten damit als entschieden. Ohne NAS: Warteschlange, nachgeschrieben beim nächsten Zugriff. " +
        "Familie.bibliothek wird nachgeführt; der Trockenlauf zeigt, was geschrieben würde.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familien", "liste", false, "Familien (Kennzeichen, Name oder id); leer = alle Familien des Projekts"),
        new("als_aktuell", "bool", false, "Die Fassung des Projekts als neue aktuelle Version setzen, auch wenn die Familie parallel geändert wurde (nur ein Mensch)"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        bool alsAktuell = Param.Bool(p, "als_aktuell");
        if (alsAktuell) FamilienFreigabe.NurMensch(k, "Eine Fassung als aktuelle Version der Familienbibliothek setzen");
        List<Element> liste = p["familien"] switch
        {
            null => k.Arbeit.DerKlasse(Familien.Klasse).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList(),
            JsonArray a => a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? Familien.Hole(k.Arbeit, s)
                : throw new HlksFehler("familien: jeder Eintrag muss Text sein (Kennzeichen, Name oder id).", "z. B. [\"FAM-1\", \"FAM-2\"]")).DistinctBy(f => f.Id).ToList(),
            JsonValue v when v.TryGetValue<string>(out var s) => [Familien.Hole(k.Arbeit, s)],
            _ => throw new HlksFehler("Parameter «familien» muss eine Liste sein.", "z. B. [\"FAM-1\"] oder weglassen (alle Familien)."),
        };
        if (liste.Count == 0) throw new HlksFehler("Das Projekt hat keine Familien.", "Familie mit familie_entwurf anlegen oder mit familie_laden laden.");
        Schreiben(k, liste, alsAktuell, Name);
    }

    /// <summary>Schreibt die Familien (auch für familie_speichern ohne datei).</summary>
    public static void Schreiben(Kontext k, IEnumerable<Element> familien, bool alsAktuell, string befehl)
    {
        var ort = FamilienBibliothekDienst.Ort(k.Katalog);
        if (!ort.Aktiv)
            throw new HlksFehler(ort.Sperre ?? "Die Familienbibliothek ist nicht eingestellt.",
                $"{FamilienBibliothekDienst.Umgebungsvariable} auf den Ordner der Bibliothek setzen (in Tests einen Ordner unter %TEMP%); der Pfad des Büros steht in kataloge/buero.json.");
        foreach (var f in familien)
        {
            var id = Familien.DefinitionsId(f)
                ?? throw new HlksFehler($"{f.Kennzeichen} «{f.Name}» hat keine feste Kennung (definition.id).", $"Mit familie_aendern speichern (der Kern vergibt dann eine id) und erneut versuchen.");
            var a = FamilienBibliothekSchreiben.AuftragVonHand(k, f, befehl, alsAktuell);
            BibPlan plan;
            string ordner;
            if (k.Trockenlauf)
            {
                plan = FamilienBibliothekDienst.Vorhersage(ort, a, out ordner);
                k.Hinweise.Add(FamilienBibliothekSchreiben.Vorschau(f.Name ?? id, plan, ort) +
                    (alsAktuell && plan.Erledigt.Count > 0 ? $" Offene Abzweige {string.Join(", ", plan.Erledigt)} gälten damit als entschieden." : ""));
            }
            else
            {
                var r = FamilienBibliothekDienst.SchreibenOderEinreihen(ort, a);
                plan = r.Plan; ordner = r.Ordner;
                k.Hinweise.Add(r.Hinweis);
            }
            FamilienBibliothekSchreiben.MarkerSetzen(k, f, FamilienBibliothekSchreiben.Marker(id, plan, ordner, a.Pruefsumme!, k.Herkunft.Zeit));
        }
    }
}

public sealed class FamilieAktualisieren : IBefehl
{
    public string Name => "familie_aktualisieren";
    public const string Text = "Übernimmt eine Fassung der Familienbibliothek in die Familie des Projekts (wie Revit «Familie neu laden», nie automatisch): version = Nummer oder Abzweig, " +
        "Vorgabe die aktuelle Version. Der Trockenlauf ist die Vorschau: geänderte Kopfdaten, Parameter, Typen, Körper und Anschlüsse sowie betroffene Elemente und Anschlüsse " +
        "(z. B. «HK-1: Anschluss VL verschiebt sich um 12 mm», «Typ «22-600-1000» entfällt – 3 Elemente»). Herstellerunterlagen der Fassung werden ins Projekt kopiert, platzierte " +
        "Elemente nachgeführt (Version + 1, rückgängig möglich). Ist die Familie parallel geändert (eigener Abzweig), entscheidet nur ein Mensch: die eigene Fassung bleibt als " +
        "Abzweig in der Bibliothek erhalten und gilt als verworfen. Strukturierte Vorschau: Abfrage familien_bibliothek { familie, version }.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("familie", "text", true, "Familie des Projekts (Kennzeichen, Name oder id)"),
        new("version", "text", false, "Version der Bibliothek (Nummer, z. B. 5) oder Name eines Abzweigs; leer = aktuelle Version"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var fam = Familien.Hole(k.Arbeit, Param.Text(p, "familie", true));
        Aktualisieren(k, fam, Param.Text(p, "version"), Name);
    }

    /// <summary>Übernimmt eine Fassung in eine vorhandene Familie (auch für familie_laden, wenn die Familie schon im Projekt ist).</summary>
    public static void Aktualisieren(Kontext k, Element fam, string? version, string befehl)
    {
        var id = Familien.DefinitionsId(fam)
            ?? throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» hat keine feste Kennung (definition.id) und ist darum nicht in der Bibliothek.", "Zuerst familie_in_bibliothek.");
        var ort = FamilienBibliothekDienst.Ort(k.Katalog);
        if (!ort.Aktiv)
            throw new HlksFehler(ort.Sperre ?? "Die Familienbibliothek ist nicht eingestellt.", $"{FamilienBibliothekDienst.Umgebungsvariable} setzen (in Tests einen Ordner unter %TEMP%).");
        var bib = FamilienBibliothekDienst.Lesen(ort, !k.Trockenlauf);
        if (bib.Offline)
            throw new HlksFehler($"Familienbibliothek nicht erreichbar ({ort.Pfad}) – eine Fassung lässt sich nur aus der erreichbaren Bibliothek übernehmen.",
                "Netzwerk/VPN prüfen und erneut versuchen; im Projekt kann bis dahin weitergearbeitet werden.");
        var e = bib.Index!.Familie(id)
            ?? throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» ist nicht in der Familienbibliothek.",
                "Mit familie_in_bibliothek schreiben (jede Änderung schreibt die Familie ohnehin automatisch).");
        var (v, ab) = FamilienBibliothekSchreiben.VersionLesen(e, version);
        var def = FamilienBibliothekDienst.FassungLesen(ort, e, v, ab);
        var libP = FamilienBibliothekLogik.Inhaltspruefsumme(def);
        var h = FamilienBibliothekLogik.Inhaltspruefsumme(fam)!;
        var lage = FamilienBibliothekLogik.Lage(e, h, FamilienBibliothekLogik.Marker(fam));
        // eigene offene Abzweige: diese Übernahme entscheidet über sie (nur ein Mensch)
        var verwirft = e.OffeneAbzweige.Where(a => a.Abzweig != ab && (a.ProjektId == k.Projekt.Kopf.Id || a.Pruefsumme == h)).Select(a => a.Abzweig).ToList();
        if (verwirft.Count > 0)
            FamilienFreigabe.NurMensch(k, $"Über die parallele Änderung von «{fam.Name}» entscheiden (Bibliothek übernehmen, eigene Fassung {string.Join(", ", verwirft)} verwerfen)");
        var titel = $"{(lage.Version is int lv ? $"Projekt = Version {lv}" : lage.Abzweig is { } la ? $"Projekt = Abzweig {la}" : "Projekt mit eigener Fassung")} → " +
                    (ab is null ? $"Bibliothek Version {v}" : $"Abzweig {ab}");
        bool schonDa = h == libP || (ab is null ? lage.Version == v && lage.Abzweig is null : lage.Abzweig == ab);
        if (schonDa)
        {
            k.Hinweise.Add($"Keine Änderung: {fam.Kennzeichen} «{fam.Name}» entspricht schon {(ab is null ? $"Version {v}" : $"Abzweig {ab}")} der Bibliothek.");
            if (verwirft.Count > 0)
            {
                var marker = FamilienBibliothekSchreiben.Marker(e.Id, new BibPlan(ab is null ? "gleich" : "abzweig", v, ab, null, null, e.Version, []), e.Ordner, libP, k.Herkunft.Zeit, "geladen");
                if (h != libP) marker["inhalt"] = h;
                marker["verwirft"] = new JsonArray(verwirft.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
                FamilienBibliothekSchreiben.MarkerSetzen(k, fam, marker);
            }
            return;
        }
        var alt = (JsonObject)fam.Eigenschaft("definition")!.Inhalt!;
        var vs = FamilienVorschau.Vergleichen(k.Arbeit, k.Katalog, fam, alt, def, titel);
        k.Hinweise.AddRange(vs.Zeilen);
        if (lage.Version is null && lage.Abzweig is null && !FamilienBibliothekDienst.Ausstehend(ort).Any(a => a.Id == id && a.Pruefsumme == h))
            k.Hinweise.Add($"Achtung: die Fassung von {fam.Kennzeichen} im Projekt ist nicht in der Bibliothek – sie wird ersetzt (rückgängig holt sie zurück; vorher sichern mit familie_in_bibliothek).");
        if (vs.FehlendeTypen.Count > 0)
            throw new HlksFehler($"{fam.Kennzeichen} «{fam.Name}» lässt sich nicht aktualisieren: " +
                string.Join("; ", vs.FehlendeTypen.Select(x => $"Typ «{x.Typ}» entfällt – {x.Elemente.Count} Element(e) ({string.Join(", ", x.Elemente.Take(8))}{(x.Elemente.Count > 8 ? " …" : "")})")) + ".",
                "Die Elemente zuerst auf einen anderen Typ umstellen (element_aendern mit eigenschaften.familientyp) oder löschen – oder eine andere Version wählen (version).");
        FamilienBibliothekSchreiben.Uebernehmen(k, ort, e, v, ab, def, fam, befehl, verwirft);
    }
}

public sealed class FamilienauswahlSetzen : IBefehl
{
    public string Name => "familienauswahl_setzen";
    public const string Text = "Setzt die Auswahl der Bibliotheksfamilien, die der Projektbrowser dieses Projekts zeigt (Projektinformation.familienAuswahl, gilt für alle am Projekt): " +
        "{ gewerke[], kategorien[], hersteller[], status[] (entwurf, freigegeben), ein[] (Familien-Id immer zeigen), aus[] (Familien-Id nie zeigen) }; eine leere Liste schränkt nicht ein. " +
        "Ohne auswahl (null) zeigt der Browser nur die Familien des Projekts. Im Projekt vorhandene Familien stehen immer im Browser. Sichtbar: Abfrage familien_bibliothek mit auswahl = true.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("auswahl", "objekt", false, "{ gewerke, kategorien, hersteller, status, ein, aus } – je eine Liste von Texten; null bzw. weglassen = nur die Familien des Projekts"),
    ];

    static readonly string[] Felder = ["gewerke", "kategorien", "hersteller", "status", "ein", "aus"];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        JsonObject? neu = null;
        if (p["auswahl"] is JsonObject a)
        {
            neu = new JsonObject();
            foreach (var (feld, wert) in a)
            {
                if (!Felder.Contains(feld)) throw new HlksFehler($"auswahl.{feld} gibt es nicht.", "Felder: " + string.Join(", ", Felder) + " (je eine Liste von Texten).");
                if (wert is null) continue;
                if (wert is not JsonArray liste) throw new HlksFehler($"auswahl.{feld} muss eine Liste sein.", $"z. B. \"{feld}\": [\"…\"] – leere Liste = keine Einschränkung.");
                var texte = new List<string>();
                foreach (var x in liste)
                {
                    if (x is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s))
                        throw new HlksFehler($"auswahl.{feld}: jeder Eintrag muss ein nicht leerer Text sein.", $"z. B. \"{feld}\": [\"…\"]");
                    texte.Add(s.Trim());
                }
                texte = texte.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (feld == "gewerke" && texte.Any(KaelteAlias.IstAltesGewerk))
                {
                    // Kälte gehört zur Heizung (Entscheid Christian 20.10.2026): der alte Wert «kaelte» wird als «heizung» gelesen, kein Fehler
                    texte = texte.Select(KaelteAlias.Gewerk).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    k.Hinweise.Add(KaelteAlias.HinweisGewerk);
                }
                Pruefen(k, feld, texte);
                neu[feld] = new JsonArray(texte.Select(t => (JsonNode)JsonValue.Create(t)).ToArray());
            }
        }
        else if (p["auswahl"] is not null) throw new HlksFehler("Parameter «auswahl» muss ein Objekt oder null sein.", "{ \"kategorien\": [\"Heizkoerper\"], \"status\": [\"freigegeben\"] }");

        var info = FamilienBibliothekSchreiben.Projektinformation(k.Arbeit)?.Kopie()
            ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation" };
        var bisher = info.Eigenschaft("familienAuswahl")?.Inhalt;
        if (JsonNode.DeepEquals(bisher, neu))
        {
            k.Hinweise.Add("Keine Änderung: die Familienauswahl ist schon so gesetzt.");
            return;
        }
        if (neu is null) info.Eigenschaften.Remove("familienAuswahl");
        else info.Eigenschaften["familienAuswahl"] = new Wert { Inhalt = neu, Herkunft = k.Herkunft };
        k.Setzen(info);
        var ort = FamilienBibliothekDienst.Ort(k.Katalog);
        var idx = FamilienBibliothekDienst.IndexOhneSchreiben(ort);
        if (neu is null) k.Hinweise.Add("Familienauswahl entfernt: der Projektbrowser zeigt nur die Familien des Projekts.");
        else if (idx is null) k.Hinweise.Add("Familienauswahl gesetzt" + (ort.Aktiv ? " (Bibliothek nicht lesbar – Anzahl unbekannt)." : $" ({ort.Sperre})"));
        else
        {
            int n = idx.Familien.Count(f => FamilienBibliothek.Sichtbar(neu, f.Id, f.Kategorie, FamilienBibliothek.Gewerk(k.Katalog, f.Kategorie), f.Hersteller, f.Status));
            k.Hinweise.Add($"Familienauswahl gesetzt: {n} von {idx.Familien.Count} Bibliotheksfamilien sichtbar (dazu immer die Familien des Projekts).");
        }
    }

    static void Pruefen(Kontext k, string feld, List<string> texte)
    {
        switch (feld)
        {
            case "status":
                foreach (var s in texte.Where(s => s is not ("entwurf" or "freigegeben")))
                    throw new HlksFehler($"auswahl.status «{s}» gibt es nicht.", "Erlaubt: entwurf, freigegeben.");
                break;
            case "kategorien":
            {
                var erlaubt = k.Katalog.Klassen.Values.Where(c => c.Familie).Select(c => c.Name).Order(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var s in texte.Where(s => !erlaubt.Contains(s, StringComparer.OrdinalIgnoreCase)))
                    throw new HlksFehler($"auswahl.kategorien «{s}» ist keine familienfähige Klasse.", "Erlaubt: " + string.Join(", ", erlaubt) + ".");
                break;
            }
            case "gewerke":
            {
                var erlaubt = k.Katalog.Klassen.Values.Select(c => c.Gewerk).Where(g => !string.IsNullOrEmpty(g)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var s in texte.Where(s => !erlaubt.Contains(s, StringComparer.OrdinalIgnoreCase)))
                    throw new HlksFehler($"auswahl.gewerke «{s}» gibt es nicht.", "Erlaubt: " + string.Join(", ", erlaubt) + ".");
                break;
            }
        }
    }
}
