// Drei-Wege-Abgleich KICAD ↔ HKAP (Modul 1d). Je Datensatz und Feld: KICAD-Wert K, HKAP-Wert H, Basis B vom letzten
// Abgleich (siehe HkapBasis). Ergebnis je Feld:
//
//   K = H                                  → gleich (Basis wird nachgeführt)
//   Feld darf in beide Richtungen fliessen  (Katalog: richtung beide, fuehrt zuerst):
//     nur K geändert → senden · nur H geändert → übernehmen · beide geändert → KONFLIKT (beide Fassungen, Mensch entscheidet)
//   Feld darf nur in eine Richtung fliessen (Katalog: richtung oder fuehrt): die führende Seite setzt sich durch, auch
//     wenn die andere geändert hat (dann «ueberschreibt»); nie mit einem leeren Wert, ausser die Leerung ist eine
//     bekannte Änderung. Ein HKAP-geführtes Feld wird so nie gesendet, ein KICAD-geführtes nie übernommen.
//   Feld ohne Ort in KICAD                 → nur lesen (Anzeige; Basis zeigt, ob HKAP seit dem letzten Abgleich geändert hat)
//   Ohne Basis (erster Abgleich) gilt eine leere Seite als unverändert: der Wert der anderen fliesst.
//
// Datensätze: gepaart · neu in HKAP (→ KICAD-Element anlegen; Raum = nicht platzierter Raum) · neu in KICAD (→ in HKAP
// anlegen) · in HKAP gelöscht / in KICAD gelöscht (→ nur Vorschlag, Löschen nie automatisch).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class HkapFeldStand
{
    public required string Feld { get; init; }
    public JsonNode? Kicad { get; init; }
    public JsonNode? Hkap { get; init; }
    public JsonNode? Basis { get; init; }
    public bool BasisBekannt { get; init; }
    /// <summary>gleich | senden | uebernehmen | konflikt | abweichend | nurLesen</summary>
    public required string Aktion { get; init; }
    public bool Ueberschreibt { get; init; }
    public string? Grund { get; init; }
    /// <summary>Schlüssel eines Konflikts für hkap_uebernehmen.konflikte: «raeume/E1-S3/bezeichnung».</summary>
    public string? Schluessel { get; set; }
}

public sealed class HkapPaar
{
    public string? HkapId { get; set; }
    public Element? Element { get; set; }
    public HkapDatensatz? Hkap { get; set; }
    public JsonObject HkapFlach { get; set; } = new();
    public HkapBasisSatz? Basis { get; set; }
    /// <summary>gepaart | neuInHkap | neuInKicad | geloeschtInHkap | geloeschtInKicad</summary>
    public string Art { get; set; } = "gepaart";
    /// <summary>kennung | basis (Kennung fehlt am Element) | name (Geschoss, erster Abgleich) | verknuepfung (Projekt)</summary>
    public string? Zuordnung { get; set; }
    public List<HkapFeldStand> Felder { get; } = [];
    /// <summary>Möglicher Partner mit gleicher Raumnummer (neu auf beiden Seiten): Anlegen unterbleibt, bis zugeordnet.</summary>
    public string? Vorschlag { get; set; }
    /// <summary>Stränge (Modul 3): alle KICAD-Elemente, die ein HKAP-Datensatz bündelt (Element = das erste davon). Sonst leer.</summary>
    public List<Element> Gruppe { get; } = [];
    /// <summary>Stränge: neuer HKAP-Strang → Kennzeichen der Systeme, die zum Medium passen (Platzhalter hängt an genau einem).</summary>
    public List<string> SystemKandidaten { get; } = [];
    /// <summary>Stränge: neuer HKAP-Strang ohne eindeutiges System – die Übernahme verlangt die Wahl «system».</summary>
    public bool SystemWahlNoetig { get; set; }
    /// <summary>Weder anlegen noch senden, bis jemand entschieden hat (Zuordnungsvorschlag oder Systemwahl).</summary>
    public bool Blockiert => Vorschlag is not null || SystemWahlNoetig;
    /// <summary>Kennzeichen aller KICAD-Elemente des Paars (Gruppe oder Element).</summary>
    public string? ElementText => Gruppe.Count > 0 ? string.Join(", ", Gruppe.Select(e => e.Kennzeichen)) : Element?.Kennzeichen;
    public IReadOnlyList<Element> Elemente => Gruppe.Count > 0 ? Gruppe : Element is null ? [] : [Element];
    public string Anzeige => ElementText ?? $"HKAP {HkapId}";
}

/// <summary>Einstellungen für das Planen, die nicht aus den Daten folgen.</summary>
/// <param name="Projekt">Für Ableitungen (Leitungslängen der Stränge); ohne Projekt fehlen die Längen im Block «kicad».</param>
/// <param name="Buendeln">Stränge: wie neue KICAD-Stränge zu einem HKAP-Strang gebündelt werden (steigzone | auto | name | kennzeichen | nein; Vorgabe steigzone).</param>
public sealed record HkapPlanOptionen(Projekt? Projekt = null, string Buendeln = "steigzone");

public sealed class HkapArtPlan
{
    public required string Name { get; init; }
    public HkapDatenartDef? Def { get; init; }
    public HkapPlanOptionen Optionen { get; init; } = new();
    /// <summary>Stränge: mögliche Zuordnungen (HKAP-Strang, KICAD-Stränge, Grund), im Abgleich als Zuordnungsvorschläge.</summary>
    public List<(string HkapId, string Strang, string Grund)> StrangVorschlaege { get; } = [];
    public List<HkapFeld> Abbildung { get; init; } = [];
    public List<HkapPaar> Paare { get; } = [];
    public List<string> Hinweise { get; } = [];
}

public sealed class HkapZaehler
{
    public int Neu, Geaendert, Gleich, Felder, Konflikte, Entschieden, Abgelehnt, Vorschlaege;
}

public static class HkapAbgleich
{
    // ---------------------------------------------------------------- Feldvergleich

    public static HkapFeldStand Vergleichen(string feld, bool kicadHat, JsonNode? k, JsonNode? h, bool basisBekannt, JsonNode? b,
        bool darfSenden, bool darfUebernehmen)
    {
        HkapFeldStand Stand(string aktion, bool ueber = false, string? grund = null) => new()
        {
            Feld = feld, Kicad = HkapWerte.Kopie(k), Hkap = HkapWerte.Kopie(h), Basis = HkapWerte.Kopie(b), BasisBekannt = basisBekannt,
            Aktion = aktion, Ueberschreibt = ueber, Grund = grund,
        };
        if (!kicadHat) return Stand("nurLesen", grund: basisBekannt && !HkapWerte.Gleich(h, b) ? "in HKAP geändert" : null);
        if (HkapWerte.Gleich(k, h)) return Stand("gleich");
        bool kLeer = HkapWerte.Normal(k) is null, hLeer = HkapWerte.Normal(h) is null;
        bool kG = basisBekannt ? !HkapWerte.Gleich(k, b) : !kLeer;
        bool hG = basisBekannt ? !HkapWerte.Gleich(h, b) : !hLeer;
        if (darfSenden && darfUebernehmen)
            return kG && hG ? Stand("konflikt") : kG ? Stand("senden") : hG ? Stand("uebernehmen") : Stand("abweichend");
        if (darfSenden)
            return kLeer && !(basisBekannt && kG) ? Stand("abweichend", grund: "KICAD leer, HKAP-Wert bleibt")
                : Stand("senden", hG && !hLeer, "KICAD führt");
        if (darfUebernehmen)
            return hLeer && !(basisBekannt && hG) ? Stand("abweichend", grund: "HKAP leer, KICAD-Wert bleibt")
                : Stand("uebernehmen", kG && !kLeer, "HKAP führt");
        return Stand("abweichend", grund: "darf laut HKAP-Katalog in keine Richtung fliessen");
    }

    // ---------------------------------------------------------------- Planen

    public static HkapArtPlan Planen(Modell m, HkapVerknuepfung link, string datenart, HkapDatenartDef? def, IReadOnlyList<HkapDatensatz> hkap, HkapBasisDaten basis,
        Katalog? katalog = null, HkapPlanOptionen? optionen = null)
    {
        optionen ??= new();
        var plan = new HkapArtPlan { Name = datenart, Def = def, Optionen = optionen, Abbildung = HkapAbbildung.Felder(datenart, link, katalog, optionen.Projekt) };
        if (def is null) plan.Hinweise.Add($"Der HKAP-Katalog kennt die Datenart «{datenart}» nicht – ihre Felder fliessen in keine Richtung.");
        foreach (var f in plan.Abbildung.Where(f => !f.EinheitPasst(def)))
            plan.Hinweise.Add($"{datenart}.{f.Name}: Einheit in HKAP «{string.Join(", ", f.HkapPfade.Select(x => def?.Feld(x)?.Einheit))}», in KICAD «{f.Einheit}» – der Wert fliesst nicht (Katalog prüfen).");
        switch (datenart)
        {
            case "projekt":
            {
                var h = hkap.FirstOrDefault(x => x.Id.Equals(link.Projekt, StringComparison.OrdinalIgnoreCase)) ?? hkap.FirstOrDefault();
                if (h is null) { plan.Hinweise.Add($"HKAP liefert für {link.Projekt} keine Projektdaten."); break; }
                plan.Paare.Add(new HkapPaar
                {
                    HkapId = h.Id, Element = HkapVerknuepfung.Info(m), Hkap = h, HkapFlach = HkapWerte.Flach(h.Felder),
                    Basis = basis.Satz(datenart, h.Id), Zuordnung = "verknuepfung",
                });
                break;
            }
            case "geschosse": Paaren(m, link, "Geschoss", plan, hkap, basis); break;
            case "raeume": Paaren(m, link, "Raum", plan, hkap, basis); break;
            case "straenge": HkapStraenge.Paaren(m, link, plan, hkap, basis, katalog); break;
        }
        foreach (var paar in plan.Paare.Where(p => p.Art == "gepaart")) FelderVergleichen(m, plan, paar);
        if (datenart == "raeume") Zuordnungsvorschlaege(m, link, plan);
        if (datenart == "straenge") HkapStraenge.Vorschlaege(m, plan);
        return plan;
    }

    static void Paaren(Modell m, HkapVerknuepfung link, string klasse, HkapArtPlan plan, IReadOnlyList<HkapDatensatz> hkap, HkapBasisDaten basis)
    {
        var basisArt = basis.Art(plan.Name);
        var nachElement = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, s) in basisArt) if (s.Element is { } el) nachElement.TryAdd(el, id);
        var hkapNach = new Dictionary<string, HkapDatensatz>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in hkap) hkapNach.TryAdd(h.Id, h);
        var vergeben = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);
        var ohne = new List<Element>();

        foreach (var e in m.DerKlasse(klasse).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            string? id = link.IdVon(e);
            string zuordnung = "kennung";
            if (id is null && e.Kennungen?.ContainsKey("hkap") != true && nachElement.TryGetValue(e.Id, out var bid)) { id = bid; zuordnung = "basis"; }
            if (id is null) { ohne.Add(e); continue; }
            if (vergeben.TryGetValue(id, out var schon))
            {
                plan.Hinweise.Add($"{e.Kennzeichen} und {schon.Kennzeichen} sind beide HKAP {id} zugeordnet – {e.Kennzeichen} wird übergangen (Kennung «hkap» prüfen).");
                continue;
            }
            vergeben[id] = e;
            hkapNach.TryGetValue(id, out var hs);
            plan.Paare.Add(new HkapPaar
            {
                HkapId = id, Element = e, Hkap = hs, HkapFlach = HkapWerte.Flach(hs?.Felder), Basis = basisArt.GetValueOrDefault(id),
                Art = hs is null ? "geloeschtInHkap" : "gepaart", Zuordnung = zuordnung,
            });
        }

        // Geschosse beim ersten Abgleich: eindeutig gleicher Name (Kennzeichen oder Name = HKAP-Name) oder Präfix = kurz.
        // Sonst entstünde jedes Geschoss doppelt; für Räume gibt es dafür hkap_raum_zuordnen (Namen sind dort keine Schlüssel).
        if (klasse == "Geschoss")
        {
            static bool Eq(string? a, string? b) => a is not null && b is not null && a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
            static bool Passt(Element g, HkapDatensatz h)
            {
                var name = HkapWerte.Leer(h.Felder["name"]); var pr = HkapWerte.Leer(h.Felder["praefix"]);
                return (name is not null && (Eq(g.Kennzeichen, name) || Eq(g.Name, name))) || (pr is not null && Eq(g.Text("kurz"), pr));
            }
            var kandidaten = hkap.Where(h => !vergeben.ContainsKey(h.Id) && basisArt.GetValueOrDefault(h.Id) is null).ToList();
            foreach (var h in kandidaten)
            {
                var treffer = ohne.Where(g => Passt(g, h)).ToList();
                if (treffer.Count != 1 || kandidaten.Count(x => Passt(treffer[0], x)) != 1) continue;
                var g = treffer[0];
                ohne.Remove(g);
                vergeben[h.Id] = g;
                plan.Paare.Add(new HkapPaar { HkapId = h.Id, Element = g, Hkap = h, HkapFlach = HkapWerte.Flach(h.Felder), Zuordnung = "name" });
            }
        }

        // Räume ohne Kennung und ohne Basis: HKAP nennt in kicad.id die Element-Id aus einem früheren Senden (z. B. Basis
        // verloren oder Kennungspaket gescheitert) – eine Id, kein Name, darum zuordnen
        if (klasse == "Raum")
        {
            var nachId = ohne.ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (var h in hkap.Where(h => !vergeben.ContainsKey(h.Id) && basisArt.GetValueOrDefault(h.Id) is null))
            {
                var flach = HkapWerte.Flach(h.Felder);
                if (HkapWerte.Leer(flach["kicad.id"]) is not { } kid || !nachId.Remove(kid, out var e)) continue;
                ohne.Remove(e);
                vergeben[h.Id] = e;
                plan.Paare.Add(new HkapPaar { HkapId = h.Id, Element = e, Hkap = h, HkapFlach = flach, Zuordnung = "kicadId" });
            }
        }

        foreach (var h in hkap.Where(h => !vergeben.ContainsKey(h.Id)))
        {
            var b = basisArt.GetValueOrDefault(h.Id);
            plan.Paare.Add(new HkapPaar
            {
                HkapId = h.Id, Hkap = h, HkapFlach = HkapWerte.Flach(h.Felder), Basis = b,
                Art = b?.Element is not null ? "geloeschtInKicad" : "neuInHkap",
            });
        }
        foreach (var e in ohne) plan.Paare.Add(new HkapPaar { Element = e, Art = "neuInKicad" });
    }

    static void FelderVergleichen(Modell m, HkapArtPlan plan, HkapPaar paar)
    {
        var basisFlach = paar.Basis?.Felder;
        var abgedeckt = new HashSet<string>(StringComparer.Ordinal) { "id" };
        foreach (var f in plan.Abbildung)
        {
            foreach (var p in f.HkapPfade) abgedeckt.Add(p);
            var h = f.Hkap(paar.HkapFlach);
            bool bekannt = f.BasisBekannt(basisFlach);
            var b = bekannt ? f.Hkap(basisFlach!) : null;
            bool hat = f.Liest;
            var k = hat && paar.Element is not null ? f.KicadWert(m, paar) : null;
            var fs = Vergleichen(f.Name, hat, k, h, bekannt, b, f.DarfSenden(plan.Def), f.DarfUebernehmen(plan.Def));
            if (f.HkapWertBleibt && fs.Aktion == "senden" && (fs.Ueberschreibt || HkapWerte.Normal(fs.Kicad) is null))
                fs = new HkapFeldStand
                {
                    Feld = fs.Feld, Kicad = fs.Kicad, Hkap = fs.Hkap, Basis = fs.Basis, BasisBekannt = fs.BasisBekannt, Aktion = "abweichend",
                    Grund = HkapWerte.Normal(fs.Kicad) is null ? "KICAD kennt hier kein eindeutiges Medium – der HKAP-Wert bleibt"
                        : "HKAP hat schon einen Wert (wer zuerst setzt, gewinnt) – er bleibt; in HKAP ändern oder den Systemtyp in KICAD angleichen",
                };
            if (fs.Aktion == "konflikt") fs.Schluessel = $"{plan.Name}/{paar.HkapId}/{f.Name}";
            paar.Felder.Add(fs);
        }
        // weitere HKAP-Felder (neu im Vertrag, z. B. kicad.stand): nur lesen
        foreach (var (pfad, wert) in paar.HkapFlach)
        {
            if (abgedeckt.Contains(pfad)) continue;
            bool bekannt = basisFlach?.ContainsKey(pfad) == true;
            paar.Felder.Add(Vergleichen(pfad, false, null, wert, bekannt, bekannt ? basisFlach![pfad] : null, false, false));
        }
    }

    /// <summary>Räume, die auf beiden Seiten neu sind und dieselbe Nummer (auf derselben Etage) haben: wahrscheinlich
    /// derselbe Raum. Weder anlegen noch senden, bis jemand mit hkap_raum_zuordnen entschieden hat.</summary>
    static void Zuordnungsvorschlaege(Modell m, HkapVerknuepfung link, HkapArtPlan plan)
    {
        static string? Nr(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
        string? Etage(Element r) => m.Finde(r.Geschoss) is { } g ? link.IdVon(g) : null;
        var neuK = plan.Paare.Where(p => p.Art == "neuInKicad" && p.Element is not null).ToList();
        foreach (var ph in plan.Paare.Where(p => p.Art == "neuInHkap"))
        {
            var nr = Nr(HkapWerte.Leer(ph.HkapFlach["raumNummer"]));
            if (nr is null) continue;
            var etage = HkapWerte.Leer(ph.HkapFlach["etage"]);
            var kand = neuK.Where(pk => Nr(pk.Element!.Text("nummer")) == nr && (etage is null || Etage(pk.Element!) is not { } ek || ek.Equals(etage, StringComparison.OrdinalIgnoreCase))).ToList();
            if (kand.Count == 0) continue;
            ph.Vorschlag = string.Join(", ", kand.Select(x => x.Element!.Kennzeichen));
            foreach (var pk in kand) pk.Vorschlag ??= ph.HkapId;
        }
    }

    /// <summary>Gegenprüfung der Verknüpfung über Kürzel und Name. hart = beide weichen ab (wiederkehrende Id?).</summary>
    public static (string? Warnung, bool Hart) Gegenpruefung(HkapVerknuepfung link, HkapDatensatz? projekt)
    {
        if (projekt is null) return ($"HKAP liefert für {link.Projekt} keine Projektdaten.", true);
        var kz = HkapWerte.Leer(projekt.Felder["kuerzel"]); var name = HkapWerte.Leer(projekt.Felder["bezeichnung"]);
        bool kzAnders = link.Kuerzel is not null && kz is not null && !link.Kuerzel.Equals(kz, StringComparison.OrdinalIgnoreCase);
        bool nameAnders = link.Name is not null && name is not null && !link.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
        if (kzAnders && nameAnders)
            return ($"HKAP-Projekt {link.Projekt} heisst jetzt «{name}» ({kz}), verknüpft war «{link.Name}» ({link.Kuerzel}) – womöglich eine wiederkehrende Id (HKAP vergibt P-NNN als Maximum + 1).", true);
        if (kzAnders) return ($"HKAP-Projekt {link.Projekt}: Kürzel {link.Kuerzel} → {kz} – Verknüpfung prüfen.", false);
        if (nameAnders) return ($"HKAP-Projekt {link.Projekt}: Name «{link.Name}» → «{name}» – Verknüpfung prüfen.", false);
        return (null, false);
    }

    // ---------------------------------------------------------------- Vorschau

    public static JsonObject Vorschau(Modell m, HkapArtPlan plan)
    {
        JsonArray A() => new();
        var nachKicad = new JsonObject { ["neu"] = A(), ["geaendert"] = A(), ["geloescht"] = A() };
        var nachHkap = new JsonObject { ["neu"] = A(), ["geaendert"] = A(), ["geloescht"] = A() };
        var konflikte = A(); var abweichend = A(); var nurLesen = A(); var zuordnungen = A();
        int unveraendert = 0;
        JsonObject Feld(HkapFeldStand f) => new()
        {
            ["feld"] = f.Feld, ["kicad"] = HkapWerte.Kopie(f.Kicad), ["hkap"] = HkapWerte.Kopie(f.Hkap), ["basis"] = f.BasisBekannt ? HkapWerte.Kopie(f.Basis) : null,
            ["ueberschreibt"] = f.Ueberschreibt ? true : null, ["grund"] = f.Grund,
        };
        JsonObject Werte(HkapPaar p, bool ausKicad)
        {
            var o = new JsonObject();
            foreach (var f in plan.Abbildung)
            {
                var w = ausKicad ? (f.Liest && p.Element is not null ? f.KicadWert(m, p) : null) : f.Hkap(p.HkapFlach);
                if (HkapWerte.Normal(w) is not null) o[f.Name] = HkapWerte.Kopie(w);
            }
            return o;
        }
        foreach (var p in plan.Paare)
        {
            switch (p.Art)
            {
                case "gepaart":
                {
                    var u = p.Felder.Where(f => f.Aktion == "uebernehmen").ToList();
                    var s = p.Felder.Where(f => f.Aktion == "senden").ToList();
                    var kf = p.Felder.Where(f => f.Aktion == "konflikt").ToList();
                    if (u.Count > 0) ((JsonArray)nachKicad["geaendert"]!).Add(new JsonObject { ["hkapId"] = p.HkapId, ["element"] = p.ElementText, ["felder"] = new JsonArray(u.Select(f => (JsonNode)Feld(f)).ToArray()) });
                    if (s.Count > 0) ((JsonArray)nachHkap["geaendert"]!).Add(new JsonObject { ["hkapId"] = p.HkapId, ["element"] = p.ElementText, ["felder"] = new JsonArray(s.Select(f => (JsonNode)Feld(f)).ToArray()) });
                    foreach (var f in kf)
                    {
                        var o = Feld(f); o.Remove("ueberschreibt"); o.Remove("grund");
                        o["schluessel"] = f.Schluessel; o["datenart"] = plan.Name; o["hkapId"] = p.HkapId; o["element"] = p.ElementText;
                        konflikte.Add(o);
                    }
                    foreach (var f in p.Felder.Where(f => f.Aktion == "abweichend"))
                    {
                        var o = Feld(f); o["hkapId"] = p.HkapId; o["element"] = p.ElementText;
                        abweichend.Add(o);
                    }
                    var lesen = p.Felder.Where(f => f.Aktion == "nurLesen" && HkapWerte.Normal(f.Hkap) is not null).ToList();
                    if (lesen.Count > 0)
                    {
                        var werte = new JsonObject();
                        foreach (var f in lesen) werte[f.Feld] = HkapWerte.Kopie(f.Hkap);
                        var geaendert = lesen.Where(f => f.Grund is not null).Select(f => (JsonNode)JsonValue.Create(f.Feld)).ToArray();
                        var eintrag = new JsonObject { ["hkapId"] = p.HkapId, ["element"] = p.ElementText, ["werte"] = werte, ["geaendertInHkap"] = geaendert.Length > 0 ? new JsonArray(geaendert) : null };
                        // Fläche aus dem HKAP-Plan (flaecheHkapM2) neben der KICAD-Fläche zeigen
                        if (plan.Name == "raeume" && p.Element is not null && HkapAbbildung.Flaeche(p.Element) is double fk) eintrag["flaecheKicadM2"] = fk;
                        nurLesen.Add(eintrag);
                    }
                    if (u.Count + s.Count + kf.Count == 0) unveraendert++;
                    if (p.Zuordnung is "name" or "basis" or "kicadId")
                        zuordnungen.Add(new JsonObject
                        {
                            ["element"] = p.ElementText, ["hkapId"] = p.HkapId,
                            ["grund"] = p.Zuordnung switch
                            {
                                "name" => "über den Namen (erster Abgleich), Kennung wird gesetzt",
                                "kicadId" => plan.Name == "straenge" ? "über die Element-Ids in kicad.straenge in HKAP (aus einem früheren Senden), Kennung wird gesetzt"
                                    : "über kicad.id in HKAP (Element-Id aus einem früheren Senden), Kennung wird gesetzt",
                                _ => "über den Basisstand, Kennung fehlt am Element und wird gesetzt",
                            },
                        });
                    break;
                }
                case "neuInHkap":
                    ((JsonArray)nachKicad["neu"]!).Add(new JsonObject
                    {
                        ["hkapId"] = p.HkapId, ["werte"] = Werte(p, false),
                        ["hinweis"] = plan.Name == "straenge" ? HkapStraenge.NeuInHkapHinweis(p)
                            : p.Vorschlag is null ? (plan.Name == "raeume" ? "wird nicht platzierter Raum" : null)
                            : $"nicht angelegt: KICAD-Raum {p.Vorschlag} hat dieselbe Nummer – zuerst hkap_raum_zuordnen",
                    });
                    break;
                case "neuInKicad":
                    ((JsonArray)nachHkap["neu"]!).Add(new JsonObject
                    {
                        ["element"] = p.ElementText, ["werte"] = Werte(p, true),
                        ["hinweis"] = plan.Name == "straenge" ? HkapStraenge.NeuInKicadHinweis(m, p)
                            : p.Vorschlag is not null ? $"nicht gesendet: HKAP-Raum {p.Vorschlag} hat dieselbe Nummer – zuerst hkap_raum_zuordnen"
                            : plan.Name == "raeume" && p.Element is not null && Werte(p, true)["etage"] is null ? "Geschoss ist noch nicht mit HKAP verknüpft – wird nach dem Geschoss gesendet" : null,
                    });
                    break;
                case "geloeschtInKicad":
                    ((JsonArray)nachHkap["geloescht"]!).Add(new JsonObject { ["hkapId"] = p.HkapId, ["hinweis"] = "in KICAD gelöscht – in HKAP löschen nur von Hand (Vorschlag)" });
                    break;
                case "geloeschtInHkap":
                    ((JsonArray)nachKicad["geloescht"]!).Add(new JsonObject { ["hkapId"] = p.HkapId, ["element"] = p.ElementText, ["hinweis"] = "in HKAP gelöscht – KICAD-Element von Hand löschen (Vorschlag)" });
                    break;
            }
        }
        var o2 = new JsonObject
        {
            ["name"] = plan.Name, ["nachKicad"] = nachKicad, ["nachHkap"] = nachHkap, ["konflikte"] = konflikte,
            ["abweichend"] = abweichend, ["nurLesen"] = nurLesen, ["unveraendert"] = unveraendert,
        };
        if (zuordnungen.Count > 0) o2["zuordnungen"] = zuordnungen;
        if (plan.Name == "raeume")
        {
            o2["nichtPlatziert"] = new JsonArray(m.DerKlasse("Raum").Where(r => r.Geometrie is null && r.Kennungen?.ContainsKey("hkap") == true)
                .OrderBy(r => r.Kennzeichen).Select(r => (JsonNode)JsonValue.Create(r.Kennzeichen)).ToArray());
            var vorschlaege = plan.Paare.Where(p => p.Art == "neuInHkap" && p.Vorschlag is not null)
                .Select(p => (JsonNode)new JsonObject { ["hkapId"] = p.HkapId, ["raum"] = p.Vorschlag, ["grund"] = "gleiche Raumnummer" }).ToArray();
            if (vorschlaege.Length > 0) o2["zuordnungsvorschlaege"] = new JsonArray(vorschlaege);
        }
        if (plan.Name == "straenge") HkapStraenge.VorschauErgaenzen(m, plan, o2);
        if (plan.Hinweise.Count > 0) o2["hinweise"] = new JsonArray(plan.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray());
        return o2;
    }

    // ---------------------------------------------------------------- Übernehmen (HKAP → KICAD)

    internal static Herkunft Herkunft(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, HkapPaar paar, HkapFeld f) =>
        k.Herkunft with { Art = "hkap", Quelle = link.Quelle(plan.Name, paar.HkapId ?? "?", string.Join("+", f.HkapPfade), paar.Hkap?.Version) };

    static HkapBasisSatz NeueBasis(HkapPaar paar, string? element, IEnumerable<string> pfadeAusHkap, JsonObject? gesendet = null, string? version = null)
    {
        var felder = paar.Basis?.Felder.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var pfad in pfadeAusHkap.Distinct(StringComparer.Ordinal))
            felder[pfad] = paar.HkapFlach.TryGetPropertyValue(pfad, out var v) ? v?.DeepClone() : null;
        if (gesendet is not null) foreach (var (pfad, v) in gesendet) felder[pfad] = v?.DeepClone();
        return new HkapBasisSatz { Version = version ?? paar.Hkap?.Version ?? paar.Basis?.Version, Element = element, Felder = felder, Zeit = DateTimeOffset.Now };
    }

    internal static void KennungSetzen(Element e, HkapVerknuepfung link, string id)
    {
        e.Kennungen ??= new Dictionary<string, string>();
        e.Kennungen["hkap"] = link.Kennung(id);
    }

    /// <summary>Übernimmt in den Kontext, was nach KICAD fliesst (samt entschiedenen Konflikten), legt neue HKAP-
    /// Datensätze als Elemente an und liefert die Basisänderungen (geschrieben erst mit dem Paket).</summary>
    /// <param name="systemWahl">Stränge: HKAP-Strang-Id → gewählte Systeme (Kennzeichen) für einen neuen, nicht platzierten Strang.</param>
    public static List<HkapBasisAenderung> Uebernehmen(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, IReadOnlyDictionary<string, string> wahl,
        ISet<string> benutzt, HkapZaehler z, IReadOnlyDictionary<string, string[]>? systemWahl = null)
    {
        var aend = new List<HkapBasisAenderung>();
        foreach (var paar in plan.Paare)
        {
            switch (paar.Art)
            {
                case "gepaart":
                {
                    // Stränge: ein HKAP-Datensatz bündelt mehrere KICAD-Elemente (Kopien in es); sonst genau eines (e)
                    var es = paar.Gruppe.Count > 0 ? paar.Gruppe.Select(x => x.Kopie()).ToList() : null;
                    var e = es is not null ? es[0] : paar.Element?.Kopie();
                    bool neu = false;
                    if (e is null)
                    {
                        e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation" };
                        neu = true;
                    }
                    var vorherTexte = (es ?? [e]).Select(x => Json.Text(x)).ToList();
                    var vorher = string.Join("\n", vorherTexte);
                    var pfade = new List<string>();
                    int geaendert = 0;
                    foreach (var fs in paar.Felder)
                    {
                        var f = plan.Abbildung.FirstOrDefault(x => x.Name == fs.Feld);
                        var fp = f?.HkapPfade ?? [fs.Feld];
                        string? w = null;
                        if (fs.Schluessel is not null)
                        {
                            if (wahl.TryGetValue(fs.Schluessel, out var x)) { w = x; benutzt.Add(fs.Schluessel); }
                            else if (wahl.TryGetValue("*", out var alle)) { w = alle; benutzt.Add("*"); }
                        }
                        switch (fs.Aktion)
                        {
                            case "uebernehmen":
                            case "konflikt" when w == "hkap":
                                if (es is not null) f!.GruppeSchreiben!(k, es, fs.Hkap, Herkunft(k, link, plan, paar, f));
                                else f!.KicadSchreiben!(k, e, fs.Hkap, Herkunft(k, link, plan, paar, f));
                                pfade.AddRange(fp);
                                geaendert++;
                                if (fs.Aktion == "konflikt") z.Entschieden++;
                                break;
                            case "konflikt" when w == "kicad":
                                pfade.AddRange(fp);   // HKAP-Fassung gilt als gesehen: der KICAD-Wert geht mit hkap_senden
                                z.Entschieden++;
                                k.Hinweise.Add($"{paar.Anzeige} {fs.Feld}: KICAD-Fassung «{HkapWerte.Text(fs.Kicad)}» bleibt – mit hkap_senden an HKAP schicken.");
                                break;
                            case "konflikt":
                                z.Konflikte++;
                                break;
                            case "gleich" or "nurLesen":
                                pfade.AddRange(fp);
                                break;
                        }
                    }
                    z.Felder += geaendert;
                    if (plan.Name != "projekt")
                        foreach (var x in es ?? [e])
                            if (link.IdVon(x) is null) KennungSetzen(x, link, paar.HkapId!);
                    if (plan.Name == "projekt" && e.Eigenschaft("hkap")?.Inhalt is JsonObject hk)
                    {
                        // Gegenprüfung nachführen: Kürzel und Name wie jetzt in HKAP
                        var kz = HkapWerte.Leer(paar.HkapFlach["kuerzel"]); var name = HkapWerte.Leer(paar.HkapFlach["bezeichnung"]);
                        if ((kz is not null && HkapWerte.Leer(hk["kuerzel"]) != kz) || (name is not null && HkapWerte.Leer(hk["name"]) != name))
                        {
                            var neuHk = (JsonObject)hk.DeepClone();
                            if (kz is not null) neuHk["kuerzel"] = kz;
                            if (name is not null) neuHk["name"] = name;
                            e.Eigenschaften["hkap"] = new Wert { Inhalt = neuHk, Herkunft = k.Herkunft with { Art = "hkap", Quelle = link.Quelle("projekt", paar.HkapId!, "kuerzel+bezeichnung", paar.Hkap?.Version) } };
                        }
                    }
                    var nachher = (es ?? [e]).Select(x => Json.Text(x)).ToList();
                    if (neu || string.Join("\n", nachher) != vorher)
                    {
                        var liste = es ?? [e];
                        for (int i = 0; i < liste.Count; i++)
                            if (neu || nachher[i] != vorherTexte[i]) k.Setzen(liste[i]);
                        if (geaendert > 0) z.Geaendert++;
                    }
                    else z.Gleich++;
                    aend.Add(new HkapBasisAenderung(plan.Name, paar.HkapId!, NeueBasis(paar, plan.Name == "projekt" ? null : e.Id, pfade)));
                    break;
                }
                case "neuInHkap":
                {
                    if (paar.Vorschlag is not null)
                    {
                        k.Hinweise.Add(plan.Name == "straenge"
                            ? $"HKAP-Strang {paar.HkapId} nicht angelegt: KICAD-Strang {paar.Vorschlag} hat dieselbe Bezeichnung – mit hkap_strang_zuordnen zuordnen (oder Namen ändern)."
                            : $"HKAP-Raum {paar.HkapId} nicht angelegt: KICAD-Raum {paar.Vorschlag} hat dieselbe Nummer – mit hkap_raum_zuordnen zuordnen (oder Nummer ändern).");
                        z.Vorschlaege++;
                        break;
                    }
                    Element? e;
                    if (plan.Name == "straenge")
                    {
                        var angelegt = HkapStraenge.PlatzhalterAnlegen(k, link, plan, paar, systemWahl);
                        if (angelegt.Count == 0) { z.Vorschlaege++; break; }
                        e = angelegt[0];
                    }
                    else e = plan.Name switch
                    {
                        "geschosse" => GeschossAnlegen(k, link, plan, paar),
                        "raeume" => RaumAnlegen(k, link, plan, paar),
                        _ => null,
                    };
                    if (e is null) break;
                    z.Neu++;
                    // Basis nur für Felder, die jetzt in KICAD stehen (übernommen) oder nur gelesen werden – nicht für KICAD-geführte,
                    // abgeleitete (kicad.*): sonst gälte «KICAD leer» als Änderung und ein späteres Senden leerte die HKAP-Werte
                    var kicadGefuehrt = plan.Abbildung.Where(f => f.Liest && !f.Schreibt).SelectMany(f => f.HkapPfade).ToHashSet(StringComparer.Ordinal);
                    var pfade = paar.HkapFlach.Select(x => x.Key).Concat(plan.Abbildung.SelectMany(f => f.HkapPfade))
                        .Where(x => !kicadGefuehrt.Contains(x) && !kicadGefuehrt.Any(g => x.StartsWith(g + ".", StringComparison.Ordinal)));
                    aend.Add(new HkapBasisAenderung(plan.Name, paar.HkapId!, NeueBasis(paar, e.Id, pfade)));
                    break;
                }
                case "geloeschtInHkap":
                    k.Hinweise.Add($"{paar.Anzeige}: HKAP {paar.HkapId} gibt es in HKAP nicht mehr – Vorschlag: in KICAD von Hand löschen (nie automatisch).");
                    z.Vorschlaege++;
                    break;
                case "geloeschtInKicad":
                    k.Hinweise.Add($"HKAP {plan.Name} {paar.HkapId}: in KICAD gelöscht – Vorschlag: in HKAP von Hand löschen (nie automatisch).");
                    z.Vorschlaege++;
                    break;
            }
        }
        return aend;
    }

    static Element GeschossAnlegen(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, HkapPaar paar)
    {
        var f = paar.HkapFlach;
        var name = HkapWerte.Leer(f["name"]);
        var kote = Json.Zahl(f["koteMm"]);
        var kz = name is not null && !name.Any(char.IsWhiteSpace) && k.Arbeit.Finde(name) is null ? name : k.NaechstesKennzeichen("G");
        var gebaeude = k.Arbeit.DerKlasse("Gebaeude").OrderBy(g => g.Kennzeichen, StringComparer.Ordinal).FirstOrDefault()
            ?? k.Setzen(new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("GEB"), Klasse = "Gebaeude", Name = "Gebäude" });
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Geschoss", Name = name, TeilVon = gebaeude.Id };
        KennungSetzen(e, link, paar.HkapId!);
        var hName = Herkunft(k, link, plan, paar, plan.Abbildung.First(x => x.Name == "praefix"));
        HkapAbbildung.Eigenschaft(e, "kurz", f["praefix"], hName);
        var hKote = Herkunft(k, link, plan, paar, plan.Abbildung.First(x => x.Name == "koteMm"));
        HkapAbbildung.Eigenschaft(e, "kote", JsonValue.Create(kote ?? 0), kote is null ? hKote with { Quelle = hKote.Quelle + " (in HKAP leer: 0 gesetzt, bitte prüfen)" } : hKote, "zahl", "mm");
        // Geschosshöhe kennt HKAP nicht: Abstand zur nächsthöheren HKAP-Etage, sonst 3000 mm als Platzhalter (wie IFC-Übertrag)
        var hoeher = plan.Paare.Where(p => p.Hkap is not null).Select(p => Json.Zahl(p.HkapFlach["koteMm"])).Where(kk => kk > (kote ?? 0)).Min();
        bool platzhalter = hoeher is null;
        e.Eigenschaften["hoehe"] = new Wert
        {
            Inhalt = JsonValue.Create(platzhalter ? 3000.0 : hoeher!.Value - (kote ?? 0)), Einheit = "mm",
            Herkunft = platzhalter
                ? k.Herkunft with { Quelle = "PLATZHALTER 3000 mm (Annahme, kein Wert aus HKAP: HKAP kennt keine Geschosshöhe) – wirkliche Höhe setzen" }
                : k.Herkunft with { Art = "hkap", Quelle = $"Abstand zur nächsten HKAP-Etage ({link.Quelle(plan.Name, paar.HkapId!, "koteMm", paar.Hkap?.Version)})" },
        };
        if (platzhalter) k.Hinweise.Add($"Geschoss {kz}: Geschosshöhe 3000 mm ist ein PLATZHALTER (Annahme, kein Wert aus HKAP – HKAP kennt keine Geschosshöhe). Wirkliche Höhe in KICAD setzen (element_aendern, Eigenschaft hoehe).");
        if (k.Arbeit.DerKlasse("Geschoss").FirstOrDefault(g => g.Zahl("kote") is double gk && Math.Abs(gk - (kote ?? 0)) < 1) is { } gleich)
            k.Hinweise.Add($"Geschoss {kz}: Kote {HkapAbbildung.Zahltext(kote ?? 0)} mm aus HKAP ist dieselbe wie bei {gleich.Kennzeichen} – Kote in KICAD setzen (KICAD führt die Kote, sie geht dann an HKAP).");
        k.Hinweise.Add($"Geschoss {kz} aus HKAP {paar.HkapId} angelegt.");
        return k.Setzen(e);
    }

    static Element RaumAnlegen(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, HkapPaar paar)
    {
        var nr = HkapWerte.Leer(paar.HkapFlach["raumNummer"]);
        var kz = nr is not null && !nr.Any(char.IsWhiteSpace) && k.Arbeit.Finde(nr) is null ? nr : k.NaechstesKennzeichen("R");
        var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Raum" };
        KennungSetzen(e, link, paar.HkapId!);
        foreach (var f in plan.Abbildung.Where(f => f.KicadSchreiben is not null && f.EinheitPasst(plan.Def)))
            f.KicadSchreiben!(k, e, f.Hkap(paar.HkapFlach), Herkunft(k, link, plan, paar, f));
        if (e.Geschoss is null && HkapWerte.Leer(paar.HkapFlach["etage"]) is null)
            k.Hinweise.Add($"{kz}: HKAP nennt keine Etage – Geschoss bitte setzen.");
        k.Hinweise.Add($"Raum {kz} ({HkapAbbildung.RaumText(e)}) aus HKAP {paar.HkapId} übernommen – noch nicht platziert: Grundriss zeichnen (element_aendern mit geometrie) oder mit hkap_raum_zuordnen einem gezeichneten Raum zuordnen.");
        return k.Setzen(e);
    }

    // ---------------------------------------------------------------- Senden (KICAD → HKAP)

    public sealed record SendeSatz(HkapPaar Paar, HkapAenderung Aenderung, JsonObject Gesendet);

    /// <summary>Was an HKAP geht: geänderte Felder gepaarter Datensätze (mit erwarteter Version) und neue KICAD-Elemente.
    /// Die Felder des Objekts «kicad» gehen immer zusammen. Löschungen nie (nur Vorschlag).</summary>
    public static List<SendeSatz> SendenPlanen(Modell m, HkapArtPlan plan, List<string> hinweise, HkapZaehler z)
    {
        var aus = new List<SendeSatz>();
        foreach (var paar in plan.Paare)
        {
            switch (paar.Art)
            {
                case "gepaart":
                {
                    var senden = paar.Felder.Where(f => f.Aktion == "senden").ToList();
                    if (senden.Count == 0 || paar.Element is null) break;
                    var gesendet = new JsonObject();
                    foreach (var fs in senden) gesendet[fs.Feld] = HkapWerte.Kopie(fs.Kicad);
                    if (gesendet.Any(x => x.Key.StartsWith("kicad.", StringComparison.Ordinal)))
                        foreach (var f in plan.Abbildung.Where(f => f.Name.StartsWith("kicad.", StringComparison.Ordinal) && f.DarfSenden(plan.Def) && f.Liest))
                            gesendet[f.Name] = f.KicadWert(m, paar);
                    aus.Add(new SendeSatz(paar, new HkapAenderung { Id = paar.HkapId, Version = paar.Hkap?.Version, Felder = HkapWerte.Verschachtelt(gesendet) }, gesendet));
                    break;
                }
                case "neuInKicad" when paar.Element is not null:
                {
                    if (paar.Vorschlag is not null)
                    {
                        hinweise.Add(plan.Name == "straenge"
                            ? $"{paar.ElementText} nicht an HKAP gesendet: HKAP-Strang {paar.Vorschlag} hat dieselbe Bezeichnung – zuerst hkap_strang_zuordnen (oder Namen ändern)."
                            : $"{paar.Element.Kennzeichen} nicht an HKAP gesendet: HKAP-Raum {paar.Vorschlag} hat dieselbe Nummer – zuerst hkap_raum_zuordnen (oder Nummer ändern).");
                        z.Vorschlaege++;
                        break;
                    }
                    var gesendet = new JsonObject();
                    foreach (var f in plan.Abbildung.Where(f => f.DarfSenden(plan.Def, neu: true) && f.Liest))
                        if (f.KicadWert(m, paar) is { } w && HkapWerte.Normal(w) is not null) gesendet[f.Name] = w.DeepClone();
                    if (plan.Name == "raeume" && !gesendet.ContainsKey("etage"))
                    {
                        hinweise.Add($"{paar.Element.Kennzeichen}: Geschoss ist nicht mit HKAP verknüpft – Raum geht nach dem Geschoss (hkap_senden mit geschosse).");
                        break;
                    }
                    if (gesendet.Count == 0) break;
                    aus.Add(new SendeSatz(paar, new HkapAenderung { Felder = HkapWerte.Verschachtelt(gesendet) }, gesendet));
                    break;
                }
                case "geloeschtInKicad":
                    hinweise.Add($"HKAP {plan.Name} {paar.HkapId}: in KICAD gelöscht – Vorschlag: in HKAP von Hand löschen (KICAD löscht nie automatisch).");
                    z.Vorschlaege++;
                    break;
                case "geloeschtInHkap":
                    hinweise.Add($"{paar.Anzeige}: HKAP {paar.HkapId} gibt es in HKAP nicht mehr – Vorschlag: in KICAD von Hand löschen.");
                    z.Vorschlaege++;
                    break;
            }
        }
        return aus;
    }

    /// <summary>Wertet die Antwort von HKAP aus: Kennungen für neue Datensätze (in den Kontext), Basis für Angenommenes,
    /// Konflikt und Ablehnung als Hinweis (nichts überschrieben).</summary>
    public static List<HkapBasisAenderung> SendenAuswerten(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, List<SendeSatz> saetze,
        List<HkapErgebnis> ergebnisse, HkapZaehler z)
    {
        var aend = new List<HkapBasisAenderung>();
        static string Liste(JsonObject o) => string.Join(", ", o.Select(x => $"{x.Key} {HkapWerte.Text(x.Value) ?? "–"}"));
        for (int i = 0; i < saetze.Count; i++)
        {
            var s = saetze[i];
            var r = i < ergebnisse.Count ? ergebnisse[i] : null;
            if (s.Paar.HkapId is { } erwartet && (r is null || !r.Id.Equals(erwartet, StringComparison.OrdinalIgnoreCase)))
                r = ergebnisse.FirstOrDefault(x => x.Id.Equals(erwartet, StringComparison.OrdinalIgnoreCase)) ?? r;
            var wer = s.Paar.ElementText ?? s.Paar.HkapId ?? "?";
            if (r is null) { k.Hinweise.Add($"{wer}: keine Antwort von HKAP zu diesem Datensatz – nichts vermerkt, beim nächsten Senden erneut."); continue; }
            var tl = k.Trockenlauf ? " (Trockenlauf)" : "";
            switch (r.Status)
            {
                case "neu":
                    z.Neu++;
                    k.Hinweise.Add($"{wer} → HKAP {plan.Name}{(r.Id.Length > 0 ? " " + r.Id : "")}: neu{tl} ({Liste(s.Gesendet)})");
                    if (k.Trockenlauf || r.Id.Length == 0 || s.Paar.Element is null) break;
                    foreach (var el0 in s.Paar.Elemente)
                    {
                        var c0 = k.Arbeit.Finde(el0.Id)!.Kopie();
                        KennungSetzen(c0, link, r.Id);
                        k.Setzen(c0);
                    }
                    aend.Add(new HkapBasisAenderung(plan.Name, r.Id, new HkapBasisSatz { Version = r.Version, Element = s.Paar.Element.Id, Felder = (JsonObject)s.Gesendet.DeepClone(), Zeit = DateTimeOffset.Now }));
                    break;
                case "geaendert" or "unveraendert":
                {
                    if (r.Status == "geaendert") z.Geaendert++; else z.Gleich++;
                    k.Hinweise.Add($"{wer} → HKAP {s.Paar.HkapId}: {(r.Status == "geaendert" ? "geändert" : "unverändert")}{tl} ({Liste(s.Gesendet)})");
                    if (k.Trockenlauf) break;
                    var gleich = s.Paar.Felder.Where(f => f.Aktion is "gleich" or "nurLesen")
                        .SelectMany(f => plan.Abbildung.FirstOrDefault(x => x.Name == f.Feld)?.HkapPfade ?? [f.Feld]);
                    aend.Add(new HkapBasisAenderung(plan.Name, s.Paar.HkapId!, NeueBasis(s.Paar, s.Paar.Element?.Id, gleich, s.Gesendet, r.Version)));
                    foreach (var el in s.Paar.Elemente)
                        if (link.IdVon(el) is null)
                        {
                            var c = k.Arbeit.Finde(el.Id)!.Kopie();
                            KennungSetzen(c, link, s.Paar.HkapId!);
                            k.Setzen(c);
                        }
                    break;
                }
                case "konflikt":
                    z.Konflikte++;
                    k.Hinweise.Add($"KONFLIKT {wer} (HKAP {s.Paar.HkapId}): HKAP hat den Datensatz inzwischen geändert{(r.Aktuell?.Version is { } v ? $" (jetzt Version {v})" : "")} – nichts überschrieben; hkap_abgleich zeigt den neuen Stand in der Konfliktliste.");
                    break;
                case "abgelehnt":
                    z.Abgelehnt++;
                    k.Hinweise.Add($"{wer}: von HKAP abgelehnt{(r.Grund is null ? "" : $" ({r.Grund})")}.");
                    break;
                default:
                    k.Hinweise.Add($"{wer}: unbekannte Antwort «{r.Status}» von HKAP – nichts vermerkt.");
                    break;
            }
        }
        return aend;
    }
}
