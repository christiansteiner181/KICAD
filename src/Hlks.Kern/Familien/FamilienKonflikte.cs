// Familien (Modul 2, B4): Quellenprüfung, Konflikte und Abdeckung einer Familiendefinition (entwurf/modul-2.md, «Zusammenführen,
// Konflikte, Abdeckung»). Läuft als IFamilienPruefung «quellen» bei familie_entwurf, familie_aendern, familie_laden und
// konflikt_entscheiden (FamilienPruefung.cs, B1) und auf Abruf bei familie_freigeben.
//
//  1. Jede Befund-Id in quellen (Parameter, Typwerte, Körper, Anschlüsse, Masse) muss es in einer Unterlage des Projekts geben.
//  2. Wert gegen Befund (FamilienQuellen.cs): passt der Wert nicht, Fehler «Wert weicht von der genannten Quelle ab» mit Soll,
//     Ist und Befundstelle. Bild, Linie, Bogen, Masslinie ohne Zahl: «aus Bild gelesen, ungeprüft» (kein Fehler).
//  3. Konflikt: nennen mehrere Quellen eines Werts Zahlen, die über die Toleranz auseinanderliegen, entsteht ein Konflikt mit allen
//     Werten und Quellen; im Entwurf steht der Vorschlag nach Vorrang (bzw. der Entscheid eines Menschen), die Familie ist bis
//     zum Entscheid nicht freigebbar. Die Id ist stabil (aus Parameter, Typ und Quellen); ein Entscheid bleibt, solange sich die
//     Werte der Quellen nicht ändern.
//  4. Abdeckung je Parameter (Vorgabewert) und je «Typ/Parameter»: quellen, status (geprüft, ungeprüft-bild, fehlt, konflikt,
//     mensch), fehlt, pflicht, wert, vermerk.
//
// ANNAHMEN (Prüfliste):
//  - Familien ohne jede Quelle, ohne unterlagen, abdeckung und konflikte (z. B. im Editor gebaut) bleiben unverändert: keine
//    Abdeckung im Speicher (damit «gleiche Definition = keine Änderung» gilt); familie_freigeben rechnet sie bei Bedarf.
//  - Entscheide und frühere Abdeckung gelten nur aus vertrauter Quelle: der gespeicherten Definition, bei familie_laden aus der
//    Bibliotheksdatei, bei konflikt_entscheiden aus dem Befehl. Ein familie_entwurf kann keinen Entscheid und keinen Status
//    «mensch» einschleusen.
//  - Werte ohne Quelle: «mensch», wenn ein Mensch sie gesetzt oder geändert hat, sonst «fehlt»; ein unveränderter Wert behält
//    seinen Status. Pflicht sind alle Werte ausser Gruppe «sonstiges» und Datentyp ja_nein/medium. Ein Typparameter ohne Wert
//    im Typ und ohne Vorgabe ist «fehlt». Wer ändert, sagt die Herkunft des Befehls (FamilienPruefKontext.Herkunft, Nachtrag
//    Hauptlauf 18.10.2026), nicht die Akteur-Art des Projekts.
//  - wert_bestaetigen (nur Mensch, Entscheid Hauptlauf 18.10.2026): ein Wert ohne Quelle oder «ungeprüft-bild» wird ausdrücklich
//    angenommen – Eintrag «bestaetigt» {wert, akteur, zeit, begruendung}, Status «mensch»; gilt, solange der Wert gleich bleibt.
//    Bestätigungen gelten nur aus vertrauter Quelle (wie Entscheide).
//  - Quellen an Formelparametern und Quellen eines Parameters ohne Vorgabewert sind Belege (z. B. die Spalte), keine Werte; sie
//    werden nur auf Existenz geprüft. Quellen an Körpern und Anschlüssen ebenso (Lage prüft das Kontrollbild).
//  - Unterlagen, die das Projekt nicht hat, die aber als Verweis in unterlagen einer vertrauten Definition stehen (Familie aus
//    der Bibliothek), sind kein Fehler: ihre Werte behalten den Status, mit Hinweis.
//  - Konflikte nur zwischen Zahlen; Texte, die nicht passen, sind Fehler.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Ergebnis der Quellenprüfung einer Definition.</summary>
public sealed class QuellenErgebnis
{
    public List<FamFehler> Fehler { get; } = [];
    public List<string> Hinweise { get; } = [];
    public JsonArray Konflikte { get; } = [];
    public JsonObject Abdeckung { get; } = [];
    public JsonArray Unterlagen { get; } = [];
    /// <summary>Werte, die der Kern setzt (Vorschlag oder Entscheid eines Konflikts): Parameter, Typ (null = Vorgabewert), Wert.</summary>
    public List<(string Parameter, string? Typ, JsonNode Wert)> Setzen { get; } = [];
    public int Geprueft, AusBild, Mensch, Fehlt, OffeneKonflikte, Bestaetigt;
    public List<string> FehlendePflicht { get; } = [];
    public bool Gueltig => Fehler.Count == 0;
}

public static class FamilienQuellenAnalyse
{
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    static List<string> Ids(JsonNode? n) => n is JsonArray a ? a.Select(T).Where(x => x is not null).Select(x => x!).ToList() : [];

    /// <summary>Hat die Definition Quellen, Unterlagen, Abdeckung oder Konflikte (= Familie aus Unterlagen)?</summary>
    public static bool AusUnterlagen(JsonObject? def)
    {
        if (def is null) return false;
        if (def["unterlagen"] is JsonArray { Count: > 0 } || def["abdeckung"] is JsonObject { Count: > 0 } || def["konflikte"] is JsonArray { Count: > 0 }) return true;
        return AlleQuellen(def).Any(x => x.Ids.Count > 0);
    }

    /// <summary>Alle Quellenlisten der Definition mit ihrer Stelle.</summary>
    public static IEnumerable<(string Stelle, List<string> Ids)> AlleQuellen(JsonObject def)
    {
        foreach (var o in (def["parameter"] as JsonArray ?? []).OfType<JsonObject>())
            yield return ($"parameter «{T(o["name"])}».quellen", Ids(o["quellen"]));
        foreach (var o in (def["typen"] as JsonArray ?? []).OfType<JsonObject>())
            if (o["quellen"] is JsonObject q)
                foreach (var (pn, l) in q) yield return ($"typ «{T(o["name"])}».quellen.{pn}", Ids(l));
        foreach (var o in (def["koerper"] as JsonArray ?? []).OfType<JsonObject>())
            yield return ($"koerper «{T(o["id"])}».quellen", Ids(o["quellen"]));
        foreach (var o in (def["anschluesse"] as JsonArray ?? []).OfType<JsonObject>())
            yield return ($"anschluss «{T(o["name"])}».quellen", Ids(o["quellen"]));
        foreach (var o in (def["masse"] as JsonArray ?? []).OfType<JsonObject>())
            yield return ($"mass «{T(o["name"])}».quellen", Ids(o["quellen"]));
    }

    public static bool Pflicht(FamParameter p) => p.Gruppe != "sonstiges" && p.Datentyp is not ("ja_nein" or "medium");

    sealed class Eintrag
    {
        public required string Schluessel;
        public required FamParameter P;
        public FamTyp? Typ;
        public JsonNode? Wert;
        public List<string> Quellen = [];
        public required string Ort;
    }

    /// <summary>Wert der Definition unter einem Abdeckungsschlüssel («p» bzw. «Typ/p»).</summary>
    static JsonNode? WertBei(JsonObject? def, string parameter, string? typ)
    {
        if (def is null) return null;
        if (typ is null)
            return (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"]) == parameter)?["wert"];
        var t = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"]) == typ);
        return (t?["werte"] as JsonObject)?[parameter] ?? (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"]) == parameter)?["wert"];
    }

    static bool Gleich(JsonNode? a, JsonNode? b) => (a?.ToJsonString() ?? "null") == (b?.ToJsonString() ?? "null")
        || (Json.Zahl(a) is double x && Json.Zahl(b) is double y && a is JsonValue av && av.GetValueKind() == JsonValueKind.Number && b is JsonValue bv && bv.GetValueKind() == JsonValueKind.Number && Math.Abs(x - y) < 1e-9);

    /// <summary>Prüft Quellen, bildet Konflikte und Abdeckung. vorher = vertraute frühere Definition (gespeichert), vorherArt = Herkunft
    /// ihrer letzten Änderung (mensch, ki …); eingehendVertrauen = Entscheide, Abdeckung und Unterlagen der Definition selbst gelten
    /// (familie_laden, konflikt_entscheiden, Abruf bei der Freigabe); akteurArt = wer jetzt ändert.</summary>
    public static QuellenErgebnis Rechnen(Projekt projekt, JsonObject def, FamilieDef f, JsonObject? vorher, string? vorherArt, bool eingehendVertrauen,
        string akteurArt, BefundQuelle? quelle = null)
    {
        var e = new QuellenErgebnis();
        var q = quelle ?? new BefundQuelle(projekt);

        // vertraute frühere Stände
        var vorKonflikte = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var k in (vorher?["konflikte"] as JsonArray ?? []).OfType<JsonObject>()) if (T(k["id"]) is { } id) vorKonflikte[id] = k;
        var vorAbdeckung = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (k, v) in vorher?["abdeckung"] as JsonObject ?? []) if (v is JsonObject o) vorAbdeckung[k] = o;
        var vertrauteUnterlagen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var u in (vorher?["unterlagen"] as JsonArray ?? []).OfType<JsonObject>()) if (T(u["pruefsumme"]) is { Length: >= 12 } s) vertrauteUnterlagen[s[..12]] = u;
        if (eingehendVertrauen)
        {
            foreach (var k in (def["konflikte"] as JsonArray ?? []).OfType<JsonObject>()) if (T(k["id"]) is { } id) vorKonflikte[id] = k;
            foreach (var (k, v) in def["abdeckung"] as JsonObject ?? []) if (v is JsonObject o) vorAbdeckung[k] = o;
            foreach (var u in (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>()) if (T(u["pruefsumme"]) is { Length: >= 12 } s) vertrauteUnterlagen[s[..12]] = u;
        }
        else
        {
            if ((def["konflikte"] as JsonArray ?? []).OfType<JsonObject>().Any(k => k["entscheid"] is not null
                    && !(T(k["id"]) is { } id && vorKonflikte.TryGetValue(id, out var vk) && Gleich(vk["entscheid"], k["entscheid"]))))
                e.Hinweise.Add("Entscheide in der übergebenen Definition werden nicht übernommen: Konflikte entscheidet nur ein Mensch mit konflikt_entscheiden.");
            if ((def["abdeckung"] as JsonObject ?? []).Any(kv => kv.Value?["bestaetigt"] is JsonNode b
                    && !(vorAbdeckung.TryGetValue(kv.Key, out var va) && Gleich(va["bestaetigt"], b))))
                e.Hinweise.Add("Bestätigungen in der übergebenen Definition werden nicht übernommen: Werte bestätigt nur ein Mensch mit wert_bestaetigen.");
        }

        // 1. Existenz aller Quellen
        var abwesend = new HashSet<string>(StringComparer.Ordinal);
        var gefunden = new SortedSet<string>(StringComparer.Ordinal);
        // Nachtrag 21.10.2026 (FamilienQuellenVersion): Quellen einer anderen Leserversion, deren Befunde fehlen bzw. die sich nicht
        // umschlüsseln lassen – nicht gegen die falsche Version prüfen; ihre Unterlage behält ihren Eintrag (und leser)
        var uebersprungen = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (stelle, ids) in AlleQuellen(def))
            foreach (var id in ids)
            {
                if (q.Grund(id) is not null) { if (BefundQuelle.Praefix(id) is { } ux) uebersprungen.Add(ux); continue; }
                if (q.Hole(id, out var fehler) is not null) { gefunden.Add(BefundQuelle.Praefix(id)!); continue; }
                var px = BefundQuelle.Praefix(id);
                if (px is not null && !q.UnterlageDa(id) && vertrauteUnterlagen.ContainsKey(px)) { abwesend.Add(px); continue; }
                e.Fehler.Add(new FamFehler(stelle, $"Quelle «{id}»: {fehler}",
                    q.UnterlageDa(id) ? "Befund-Id aus der Abfrage befunde übernehmen (Id und Nummer genau so)."
                                      : "Die Unterlage mit unterlage_einfuegen (zweck «familie») ablegen und mit unterlagen_lesen lesen; Befund-Ids aus befunde übernehmen."));
            }
        foreach (var px in abwesend)
            e.Hinweise.Add($"Unterlage «{T(vertrauteUnterlagen[px]["datei"]) ?? px}» liegt nicht im Projekt: ihre Quellen bleiben als Verweis stehen und werden nicht nachgeprüft.");

        // 2. Werte sammeln
        var eintraege = new List<Eintrag>();
        var parameterJson = (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(o => T(o["name"])?.Trim() ?? "", o => o);
        var typJson = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().GroupBy(o => T(o["name"])?.Trim() ?? "").ToDictionary(g => g.Key, g => g.First());
        foreach (var p in f.Parameter)
        {
            var pq = parameterJson.TryGetValue(p.Name, out var po) ? Ids(po["quellen"]) : [];
            if (p.HatFormel)
            {
                bool typQuellen = f.Typen.Any(t => typJson.TryGetValue(t.Name, out var to) && Ids((to["quellen"] as JsonObject)?[p.Name]).Count > 0);
                if (pq.Count > 0 || typQuellen)
                    e.Hinweise.Add($"parameter «{p.Name}» hat eine Formel: seine Quellen sind Belege, kein Wert (ein Mass prüft der Massvergleich über masse).");
                continue;
            }
            if (p.Wert is not null) eintraege.Add(new Eintrag { Schluessel = p.Name, P = p, Wert = p.Wert, Quellen = pq, Ort = $"parameter «{p.Name}»" });
            foreach (var t in f.Typen)
            {
                var tq = typJson.TryGetValue(t.Name, out var to) ? Ids((to["quellen"] as JsonObject)?[p.Name]) : [];
                bool hat = t.Werte.TryGetValue(p.Name, out var tw) && tw is not null;
                if (hat || tq.Count > 0)
                {
                    if (!hat && p.Wert is null) e.Hinweise.Add($"typ «{t.Name}».quellen.{p.Name}: Quellen ohne Wert – den Wert aus der Quelle in werte eintragen.");
                    eintraege.Add(new Eintrag { Schluessel = $"{t.Name}/{p.Name}", P = p, Typ = t, Wert = hat ? tw : p.Wert, Quellen = tq, Ort = $"typ «{t.Name}».werte.{p.Name}" });
                }
                else if (p.Wert is null && p.Art == "typ" && Pflicht(p))
                    eintraege.Add(new Eintrag { Schluessel = $"{t.Name}/{p.Name}", P = p, Typ = t, Wert = null, Ort = $"typ «{t.Name}».werte.{p.Name}" });
            }
        }

        // 3. je Wert: Quellen vergleichen, Konflikt, Abdeckung
        var bekannteIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in eintraege)
        {
            var p = x.P;
            var pe = QuellVergleiche.ParameterEinheit(p);
            bool dm = QuellVergleiche.IstDurchmesser(f, p);
            var eintrag = new JsonObject { ["quellen"] = new JsonArray(x.Quellen.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()), ["pflicht"] = Pflicht(p) };
            string status; string? vermerk = null; JsonNode? angewandt = x.Wert;
            var vorEintrag = vorAbdeckung.GetValueOrDefault(x.Schluessel);

            if (x.Wert is null) { status = "fehlt"; vermerk = "Wert fehlt"; }
            else if (x.Quellen.Count == 0)
            {
                bool unveraendert = vorEintrag is not null && Gleich(vorEintrag["wert"], x.Wert) && Ids(vorEintrag["quellen"]).Count == 0;
                if (unveraendert && T(vorEintrag!["status"]) is "mensch" or "fehlt") status = T(vorEintrag["status"])!;
                else if (vorEintrag is null && vorher is not null && vorherArt is not null && Gleich(WertBei(vorher, p.Name, x.Typ?.Name), x.Wert)) status = vorherArt == "mensch" ? "mensch" : "fehlt";
                else status = akteurArt == "mensch" ? "mensch" : "fehlt";
                if (status == "fehlt") vermerk = "ohne Quelle (nicht von einem Menschen gesetzt)";
            }
            else
            {
                var fw = p.Umwandeln(x.Wert, out var umwandelFehler);
                if (fw is null || umwandelFehler is not null) { status = "fehlt"; vermerk = umwandelFehler ?? "kein Wert"; }
                else
                {
                    var erg = x.Quellen.Select(id => q.Grund(id) is { } grund
                        ? new QuellVergleich { Id = id, Status = "abwesend", Meldung = grund, Stelle = id }   // andere Leserversion: Wertprüfung entfällt
                        : QuellVergleiche.Vergleichen(f, p, fw.Value, id, q, () => abwesend.Contains(BefundQuelle.Praefix(id) ?? ""))).ToList();
                    foreach (var r in erg.Where(r => r.Status == "keine"))
                        e.Fehler.Add(new FamFehler($"{x.Ort} (Quelle {r.Id})", $"{r.Meldung} ({r.Stelle})", "Die Zelle (bzw. das Wort, die Masszahl) nennen, die den Wert trägt – Ids aus befunde."));
                    foreach (var r in erg.Where(r => r.Status == "einheit"))
                        e.Fehler.Add(new FamFehler($"{x.Ort} (Quelle {r.Id})", $"Wert weicht von der genannten Quelle ab: {r.Meldung} – {r.Stelle}",
                            $"Einheit des Parameters prüfen ({(pe ?? "ohne")}) oder die passende Quelle nennen."));
                    var zahlen = erg.Where(r => r.Quellwert is not null && r.Status is "passt" or "weicht").ToList();
                    var gruppen = Gruppieren(zahlen);
                    if (fw.Value.IstText || gruppen.Count < 2)
                    {
                        foreach (var r in erg.Where(r => r.Status == "weicht"))
                            e.Fehler.Add(new FamFehler($"{x.Ort} (Quelle {r.Id})",
                                $"Wert weicht von der genannten Quelle ab: «{p.Name}»{(x.Typ is null ? "" : $" (Typ {x.Typ.Name})")} – Soll (Quelle) {r.QuellText ?? "?"}, Ist (Familie) {Darstellen(fw.Value, pe)} – {r.Meldung} – Befundstelle {r.Stelle}",
                                "Den Wert der Quelle übernehmen oder die richtige Quelle nennen. Widersprechen sich zwei Quellen, beide nennen: dann entsteht ein Konflikt, den ein Mensch entscheidet."));
                        int ok = erg.Count(r => r.Status == "passt");
                        bool bild = erg.Any(r => r.Bild);
                        if (ok > 0) status = "geprüft";
                        else if (erg.All(r => r.Status is "ungeprueft" or "abwesend"))
                        {
                            var version = erg.Select(r => q.Grund(r.Id)).FirstOrDefault(g => g is not null);
                            if (erg.All(r => r.Status == "abwesend") && vorEintrag is not null && Gleich(vorEintrag["wert"], x.Wert) && T(vorEintrag["status"]) is { } vs)
                            { status = vs; vermerk = version is null ? "Unterlage nicht im Projekt – Status übernommen" : $"{version} – Status übernommen"; }
                            else if (erg.All(r => r.Status == "abwesend")) { status = "fehlt"; vermerk = version is null ? "Unterlage nicht im Projekt – Quelle nicht prüfbar" : $"{version} – Quelle nicht prüfbar"; }
                            else { status = "ungeprüft-bild"; vermerk = bild ? QuellVergleiche.AusBild : erg.First(r => r.Status == "ungeprueft").Meldung; }
                        }
                        else status = "geprüft";   // abgelehnt wird ohnehin (Fehler)
                    }
                    else
                    {
                        // Konflikt
                        var k = KonfliktBauen(x, gruppen, pe, dm, vorKonflikte, out var vorschlag, out var entscheid);
                        e.Konflikte.Add(k);
                        angewandt = entscheid?["wert"]?.DeepClone() ?? JsonValue.Create(vorschlag);
                        if (!Gleich(angewandt, x.Wert))
                        {
                            e.Setzen.Add((p.Name, x.Typ?.Name, angewandt!.DeepClone()));
                            e.Hinweise.Add($"«{p.Name}»{(x.Typ is null ? "" : $" (Typ {x.Typ.Name})")}: Konflikt {T(k["id"])} – im Entwurf steht {(entscheid is null ? "der Vorschlag" : "der Entscheid")} {angewandt!.ToJsonString()} statt {x.Wert!.ToJsonString()}.");
                        }
                        if (entscheid is null)
                        {
                            status = "konflikt";
                            e.OffeneKonflikte++;
                            e.Hinweise.Add($"Konflikt {T(k["id"])}: «{p.Name}»{(x.Typ is null ? "" : $" (Typ {x.Typ.Name})")} – {string.Join(" / ", gruppen.Select(g => $"{QuellVergleiche.F(g[0].Quellwert!.Value)} ({string.Join(", ", g.Select(r => r.Stelle))})"))}; Vorschlag {QuellVergleiche.F(vorschlag)}. Entscheidet ein Mensch (konflikt_entscheiden); bis dahin nicht freigebbar.");
                        }
                        else { status = "mensch"; vermerk = $"Konflikt entschieden von {T(entscheid["akteur"])}"; }
                        eintrag["konflikt"] = T(k["id"]);
                    }
                }
            }
            // ausdrücklich bestätigt (wert_bestaetigen): gilt, solange der Wert gleich ist und kein Konflikt besteht
            if (vorEintrag?["bestaetigt"] is JsonObject best && x.Wert is not null && Gleich(best["wert"], angewandt) && eintrag["konflikt"] is null
                && status is "fehlt" or "ungeprüft-bild" or "mensch")
            {
                var wer = $"bestätigt von {T(best["akteur"])}";
                vermerk = status == "ungeprüft-bild" && vermerk is not null ? $"{vermerk}; {wer}" : wer;
                status = "mensch";
                eintrag["bestaetigt"] = best.DeepClone();
                e.Bestaetigt++;
            }
            eintrag["status"] = status;
            eintrag["fehlt"] = status == "fehlt" && Pflicht(p);
            eintrag["wert"] = angewandt?.DeepClone();
            if (vermerk is not null) eintrag["vermerk"] = vermerk;
            e.Abdeckung[x.Schluessel] = eintrag;
            switch (status)
            {
                case "geprüft": e.Geprueft++; break;
                case "ungeprüft-bild": e.AusBild++; break;
                case "mensch": e.Mensch++; break;
                case "fehlt": e.Fehlt++; if (Pflicht(p)) e.FehlendePflicht.Add(x.Schluessel); break;
            }
        }

        // 4. Unterlagen als Verweis (Datei, Prüfsumme, Leserversion), ohne Befunde. Nachtrag 21.10.2026: leser = «<name>-<version>» des Lesers,
        // dessen Befund-Ids die Quellen nennen (auch die Quellen des Blocks leitung); Unterlagen mit übersprungenen Quellen (andere Leserversion)
        // behalten ihren Eintrag samt leser.
        var rollen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var u in (def["unterlagen"] as JsonArray ?? []).OfType<JsonObject>()) if (T(u["pruefsumme"]) is { Length: >= 12 } s) rollen[s[..12]] = u;
        foreach (var id in FamilienQuellenVersion.LeitungQuellen(def))
            if (q.Grund(id) is null && q.Hole(id, out _) is not null) gefunden.Add(BefundQuelle.Praefix(id)!);
        foreach (var px in gefunden.Union(uebersprungen).Distinct().Order(StringComparer.Ordinal))
        {
            bool behalten = uebersprungen.Contains(px);
            if (behalten && rollen.TryGetValue(px, out var bisher)) { e.Unterlagen.Add(bisher.DeepClone()); continue; }
            var u = q.Unterlage(px);
            if (u is null) continue;
            var o = new JsonObject();
            if (u.Id is not null) o["unterlage"] = u.Id;
            o["datei"] = u.Original;
            o["pruefsumme"] = BefundQuelle.Pruefsumme(u.Pfad);
            o["art"] = u.Art;
            if (!behalten && UnterlagenLeser.Fuer(u.Art) is { } leser) o["leser"] = FamilienQuellenVersion.Text(leser);
            if (rollen.TryGetValue(px, out var r) && T(r["rolle"]) is { } rolle) o["rolle"] = rolle;
            e.Unterlagen.Add(o);
        }
        foreach (var px in abwesend) e.Unterlagen.Add(vertrauteUnterlagen[px].DeepClone());

        if (e.Fehler.Count == 0)
            e.Hinweise.Add($"Quellen: {e.Geprueft} Wert(e) geprüft, {e.AusBild} aus Bild bzw. ungeprüft, {e.Mensch} von einem Menschen{(e.Bestaetigt > 0 ? $" (davon {e.Bestaetigt} ausdrücklich bestätigt)" : "")}, {e.Fehlt} ohne Quelle" +
                (e.FehlendePflicht.Count > 0 ? $" (Pflicht: {string.Join(", ", e.FehlendePflicht.Take(8))}{(e.FehlendePflicht.Count > 8 ? " …" : "")})" : "") +
                (e.Konflikte.Count > 0 ? $"; {e.Konflikte.Count} Konflikt(e), davon {e.OffeneKonflikte} offen" : "") + ".");
        return e;
    }

    static string Darstellen(FWert w, string? einheit) => w.IstText ? $"«{w.Text}»" : QuellVergleiche.F(w.Zahl) + (einheit is null ? "" : " " + einheit);

    /// <summary>Zahlen der Quellen in Gruppen, die innerhalb der Toleranz liegen (sortiert nach Wert).</summary>
    static List<List<QuellVergleich>> Gruppieren(List<QuellVergleich> zahlen)
    {
        var g = new List<List<QuellVergleich>>();
        foreach (var r in zahlen.OrderBy(r => r.Quellwert))
        {
            var letzte = g.LastOrDefault();
            if (letzte is not null && Math.Abs(r.Quellwert!.Value - letzte[0].Quellwert!.Value) <= Math.Max(r.Toleranz ?? 0, letzte[0].Toleranz ?? 0) + 1e-9) letzte.Add(r);
            else g.Add([r]);
        }
        return g;
    }

    /// <summary>Stabile Id eines Konflikts: aus Parameter, Typ und den Quellen.</summary>
    public static string KonfliktId(string parameter, string? typ, IEnumerable<string> quellen)
    {
        var text = $"{parameter}\u0001{typ}\u0001{string.Join(",", quellen.Order(StringComparer.Ordinal))}";
        return "k-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..10].ToLowerInvariant();
    }

    static string WerteSchluessel(JsonArray werte) => string.Join(";", werte.OfType<JsonObject>()
        .Select(w => $"{QuellVergleiche.F(Json.Zahl(w["wert"]) ?? double.NaN)}:{string.Join(",", Ids(w["quellen"]).Order(StringComparer.Ordinal))}").Order(StringComparer.Ordinal));

    static JsonObject KonfliktBauen(Eintrag x, List<List<QuellVergleich>> gruppen, string? einheit, bool dm, Dictionary<string, JsonObject> vor,
        out double vorschlag, out JsonObject? entscheid)
    {
        bool geometrie = x.P.Datentyp is "laenge" or "winkel";
        // je Gruppe: Wert der stärksten Quelle, Vorrang = stärkste Quelle
        var sortiert = gruppen.Select(g => (G: g, Best: g.OrderByDescending(r => r.Vorrang).First()))
            .OrderByDescending(t => t.Best.Vorrang).ThenByDescending(t => t.G.Count).ThenBy(t => t.Best.Quellwert).ToList();
        vorschlag = Math.Round(sortiert[0].Best.Quellwert!.Value, 6);
        var werte = new JsonArray(sortiert.Select(t => (JsonNode)new JsonObject
        {
            ["wert"] = Math.Round(t.Best.Quellwert!.Value, 6),
            ["quellen"] = new JsonArray(t.G.Select(r => (JsonNode)JsonValue.Create(r.Id)).ToArray()),
            ["stellen"] = new JsonArray(t.G.Select(r => (JsonNode)JsonValue.Create(r.Stelle)).ToArray()),
            ["text"] = new JsonArray(t.G.Select(r => (JsonNode?)JsonValue.Create(r.QuellText)).ToArray()),
            ["vorrang"] = QuellVergleiche.VorrangText(t.Best.Vorrang, geometrie),
        }).ToArray());
        var id = KonfliktId(x.P.Name, x.Typ?.Name, gruppen.SelectMany(g => g).Select(r => r.Id));
        double tol = gruppen.SelectMany(g => g).Max(r => r.Toleranz ?? 0);
        var k = new JsonObject
        {
            ["id"] = id, ["parameter"] = x.P.Name,
        };
        if (x.Typ is not null) k["typ"] = x.Typ.Name;
        k["werte"] = werte;
        if (einheit is not null) k["einheit"] = einheit;
        k["toleranz"] = QuellVergleiche.ToleranzText(x.P.Datentyp, dm, tol, einheit);
        k["vorschlag"] = vorschlag;
        k["vorrang"] = $"{QuellVergleiche.VorrangText(sortiert[0].Best.Vorrang, geometrie)} vor {QuellVergleiche.VorrangText(sortiert[1].Best.Vorrang, geometrie)}" +
            (sortiert[0].Best.Vorrang == sortiert[1].Best.Vorrang ? " (gleicher Vorrang: Vorschlag nach Anzahl Quellen)" : "");
        entscheid = null;
        if (vor.TryGetValue(id, out var alt) && alt["entscheid"] is JsonObject ae && alt["werte"] is JsonArray aw && WerteSchluessel(aw) == WerteSchluessel(werte))
        {
            entscheid = (JsonObject)ae.DeepClone();
            k["entscheid"] = entscheid;
        }
        return k;
    }

    /// <summary>Schreibt das Ergebnis in die Definition: Werte aus Konflikten, konflikte, abdeckung, unterlagen.</summary>
    public static void Anwenden(JsonObject def, QuellenErgebnis e)
    {
        foreach (var (p, typ, wert) in e.Setzen)
        {
            if (typ is null)
            {
                var po = (def["parameter"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"])?.Trim() == p);
                if (po is not null) po["wert"] = wert.DeepClone();
            }
            else
            {
                var to = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"])?.Trim() == typ);
                if (to is null) continue;
                if (to["werte"] is not JsonObject w) to["werte"] = w = [];
                w[p] = wert.DeepClone();
            }
        }
        if (e.Konflikte.Count > 0) def["konflikte"] = e.Konflikte.DeepClone(); else def.Remove("konflikte");
        def["abdeckung"] = e.Abdeckung.DeepClone();
        if (e.Unterlagen.Count > 0) def["unterlagen"] = e.Unterlagen.DeepClone(); else def.Remove("unterlagen");
    }

    /// <summary>Kurzbericht zur Familie für Abfragen und Freigabe: Konflikte offen, Pflichtwerte ohne Quelle, Werte aus Bildern.</summary>
    public static JsonObject Zusammenfassung(QuellenErgebnis e) => new()
    {
        ["geprueft"] = e.Geprueft, ["ausBild"] = e.AusBild, ["mensch"] = e.Mensch, ["bestaetigt"] = e.Bestaetigt, ["ohneQuelle"] = e.Fehlt,
        ["konflikte"] = e.Konflikte.Count, ["offeneKonflikte"] = e.OffeneKonflikte,
        ["fehlendePflicht"] = new JsonArray(e.FehlendePflicht.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
        ["fehler"] = new JsonArray(e.Fehler.Select(x => (JsonNode)x.AlsJson()).ToArray()),
    };
}

/// <summary>Die Quellenprüfung als zusätzliche Prüfung beim Speichern einer Definition (Einhängepunkt von B1).</summary>
public sealed class FamilienQuellenPruefung : IFamilienPruefung
{
    public string Name => "quellen";

    public void Pruefen(FamilienPruefKontext ctx)
    {
        if (ctx.Projekt is null) return;
        var vorherWert = ctx.Bisher?.Eigenschaft("definition");
        // Kopie: die gespeicherte Definition gehört dem Modell; sie wird unten umgeschlüsselt (Konflikte, Abdeckung)
        var vorher = (vorherWert?.Inhalt as JsonObject)?.DeepClone() as JsonObject;
        if (!FamilienQuellenAnalyse.AusUnterlagen(ctx.Definition) && !FamilienQuellenAnalyse.AusUnterlagen(vorher)) return;
        bool vertrauen = ctx.Befehl is "familie_laden" or "konflikt_entscheiden" or "wert_bestaetigen";
        var q = new BefundQuelle(ctx.Projekt);
        // Nachtrag 21.10.2026: Quellen einer älteren Leserversion auf die aktuelle umschlüsseln (FamilienQuellenVersion). Bei familie_aendern
        // gelten nur die Ids als alt, die schon in der gespeicherten Definition stehen (neu genannte kommen aus der aktuellen Abfrage befunde).
        var n = FamilienQuellenVersion.Nachfuehren(ctx.Projekt, ctx.Definition, ctx.Befehl == "familie_aendern" ? vorher : null, q);
        if (vorher is not null) FamilienQuellenVersion.Anwenden(vorher, n.Zuordnung);
        foreach (var x in n.NichtZuordenbar)
            ctx.Fehler.Add(new FamFehler(x.Stelle, $"Quelle «{x.Id}» (Befund-Id nach Leser {x.Von}) lässt sich nicht eindeutig auf Leser {x.Nach} umschlüsseln: {x.Grund}",
                "Den Befund in der aktuellen Fassung suchen (Abfrage befunde der Unterlage) und die Quelle mit seiner Id neu nennen."));
        ctx.Hinweise.AddRange(n.Hinweise);
        ctx.Hinweise.AddRange(n.Warnungen);
        // wer ändert: die Herkunft des Befehls (Nachtrag Hauptlauf 18.10.2026), ausserhalb eines Befehls die Akteur-Art des Projekts
        var akteurArt = ctx.Herkunft?.Art ?? ctx.Projekt.AkteurArt;
        var e = FamilienQuellenAnalyse.Rechnen(ctx.Projekt, ctx.Definition, ctx.Familie, vorher, vorherWert?.Herkunft.Art, vertrauen, akteurArt, q);
        ctx.Fehler.AddRange(e.Fehler);
        ctx.Hinweise.AddRange(e.Hinweise);
        if (ctx.Fehler.Count > 0) return;
        FamilienQuellenAnalyse.Anwenden(ctx.Definition, e);
        // Massvergleich je Typ als Hinweis (die Freigabe verlangt ihn bestanden)
        if (ctx.Familie.Masse.Count == 0) return;
        var f = FamilieDef.Lesen(ctx.Definition, ctx.Katalog);
        if (!f.Gueltig) return;
        var masse = FamilienMasse.Vergleichen(f, ctx.Definition, q, null);
        var schlecht = masse.Where(r => r.Bestanden == false).ToList();
        ctx.Hinweise.Add($"Massvergleich: {masse.Count(r => r.Bestanden == true)} von {masse.Count(r => r.Bestanden is not null)} Massen je Typ innerhalb der Toleranz" +
            (schlecht.Count > 0 ? $"; ausserhalb: {string.Join(", ", schlecht.Take(6).Select(r => $"{r.Mass} Typ {r.Typ} (Soll {QuellVergleiche.F(r.Soll!.Value)}, Ist {QuellVergleiche.F(r.Ist!.Value)})"))}{(schlecht.Count > 6 ? " …" : "")} – Abfrage familie_masse" : "") + ".");
        // Masse ohne Soll bzw. mit unklarer Tabellenzeile: schon im Trockenlauf melden, damit der Ersteller es selbst behebt
        foreach (var g in masse.Where(r => r.Soll is null && !r.Mehrdeutig).GroupBy(r => r.Mass))
            ctx.Hinweise.Add($"Mass {g.Key} ohne Sollwert: parameter (Typparameter mit dem Tabellenwert) angeben ({g.Count()} von {f.Typen.Count} Typen).");
        foreach (var g in masse.Where(r => r.Mehrdeutig).GroupBy(r => r.Mass))
            ctx.Hinweise.Add($"Mass {g.Key}: Tabellenzeile für Typ {string.Join(", ", g.Take(6).Select(r => r.Typ))}{(g.Count() > 6 ? " …" : "")} nicht eindeutig – " +
                "der Typ soll in quellen nur Zellen seiner eigenen Tabellenzeile nennen.");
    }
}
