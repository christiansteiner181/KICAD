// HKAP-Datenart «straenge» (Modul 3, Teil B5): KICAD-Seite. Ein HKAP-Strang bündelt mehrere KICAD-Stränge, weil KICAD je System
// (= je Medium) einen Strang führt: «Heizung H_01» = Heizung Vorlauf + Rücklauf, «Sanitär S_01» = Trinkwasser kalt, warm,
// Zirkulation, Schmutzwasser. Die Gruppe wird über die Kennung «hkap» am KICAD-Element Strang gebildet («PJ-…/ST1»); mehrere
// KICAD-Stränge dürfen dieselbe HKAP-Id tragen.
//
//   bezeichnung  ↔ Name der Gruppe (gemeinsamer Name der Stränge, sonst der häufigste, sonst der erste). Beide Richtungen, wer zuerst setzt.
//   medium       ← Systemtyp des Systems (Tabelle MediumJeSystemtyp, ANNAHME bis zu Christians Prüfung). Nur Senden; ein Wert, den
//                  HKAP schon hat, wird nie überschrieben. Verschiedene HKAP-Medien in einer Gruppe: kein Medium (Hinweis).
//   kicad.straenge → HKAP: je KICAD-Strang { id, kennzeichen, name, system, systemtyp, geschosse [HKAP-Etagen-Ids], endgeraete,
//                  laengen [{ serie, groesse, laengeM }] } (Ableitungen.Leitungslaengen). HKAP ersetzt den Block ganz.
//   nummer, tempVL, tempRL, anschluss, anschlussEtageId, etagen: nur anzeigen (KICAD speichert sie nicht – ANNAHME). Die Nummer
//                  gibt HKAP nach Gewerk (H_, K_, S_, L_); sie wird nur für einen nicht platzierten Strang als Kennzeichen benutzt.
//
// Neue KICAD-Stränge ohne Kennung werden beim Senden zu einem neuen HKAP-Strang, Stränge desselben «Strangs im Gebäude» gebündelt
// (Parameter buendeln, siehe Buendeln). Vorgabe seit 19.10.2026 (Entscheid Christian, Abnahme Modul 3, Prüfliste 25): über die von
// Hand gesetzte Steigzone (Strang.steigzone, wie im Strangschema); ohne Steigzone wird jeder KICAD-Strang ein eigener HKAP-Strang. Ein neuer HKAP-Strang kommt als nicht platzierter Strang nach KICAD (Element Strang ohne
// Leitungen im passenden System; gibt es kein oder mehrere passende Systeme, verlangt die Übernahme die Wahl «system»).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

public static class HkapStraenge
{
    /// <summary>Die Medien eines HKAP-Strangs (wie im HKAP-Editor und im Austauschvertrag).</summary>
    public static readonly string[] HkapMedien = ["Zuluft", "Abluft", "Zu-/Abluft", "Fortluft", "Aussenluft", "Heizung", "Kälte", "Sanitär"];
    public static readonly string[] BuendelnArten = ["steigzone", "auto", "name", "kennzeichen", "nein"];

    /// <summary>Systemtyp (KICAD) → HKAP-Medium. ANNAHME (Auftrag Modul 3 B5, bis Christian prüft). Heizung Glykol (früher Sole) fehlt absichtlich: kein Medium.</summary>
    static readonly Dictionary<string, string> MediumJeSystemtyp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trinkwasser kalt"] = "Sanitär", ["Trinkwasser warm"] = "Sanitär", ["Zirkulation"] = "Sanitär",
        ["Schmutzwasser"] = "Sanitär", ["Regenwasser"] = "Sanitär",
        ["Heizung Vorlauf"] = "Heizung", ["Heizung Rücklauf"] = "Heizung",
        // Kälte ist im KICAD ein Medium der Heizung (Entscheid Christian 20.10.2026), im HKAP bleibt «Kälte» ein eigenes Medium des Strangs.
        ["Kälte Vorlauf"] = "Kälte", ["Kälte Rücklauf"] = "Kälte", ["Kälte Vorlauf Glykol"] = "Kälte", ["Kälte Rücklauf Glykol"] = "Kälte",
        ["Zuluft"] = "Zuluft", ["Abluft"] = "Abluft", ["Fortluft"] = "Fortluft", ["Aussenluft"] = "Aussenluft",
    };

    static JsonNode? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : JsonValue.Create(s);

    // ---------------------------------------------------------------- Medium

    /// <summary>HKAP-Medium zum Systemtyp; alte Namen (Kühlwasser …, Sole …) werden zuerst umgeschrieben (<see cref="KaelteAlias"/>).</summary>
    public static string? MediumVon(string? systemtyp) => systemtyp is not null && MediumJeSystemtyp.TryGetValue(KaelteAlias.Systemtyp(systemtyp.Trim()), out var m) ? m : null;

    public static Element? SystemVon(Modell m, Element strang) =>
        m.Finde(strang.TeilVon) is { Klasse: "System" } s ? s : m.Finde(strang.System) is { Klasse: "System" } s2 ? s2 : null;

    public static string? SystemtypVon(Modell m, Element strang) => SystemVon(m, strang)?.Text("systemtyp");

    public static string? MediumVonStrang(Modell m, Element strang) => MediumVon(SystemtypVon(m, strang));

    /// <summary>Medien der Stränge einer Gruppe (ohne Systemtypen ohne Medium).</summary>
    public static List<string> MedienVon(Modell m, IEnumerable<Element> gruppe) =>
        gruppe.Select(e => MediumVonStrang(m, e)).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Medium der Gruppe: das gemeinsame HKAP-Medium, sonst null (kein Medium oder verschiedene).</summary>
    public static string? MediumVonGruppe(Modell m, IEnumerable<Element> gruppe) => MedienVon(m, gruppe) is { Count: 1 } l ? l[0] : null;

    /// <summary>HKAP-Medium und KICAD-Medium vertragen sich: eine Seite leer, gleich, oder HKAP «Zu-/Abluft» gegen Zuluft/Abluft.</summary>
    public static bool MediumPasst(string? hkapMedium, string? kicadMedium)
    {
        if (string.IsNullOrWhiteSpace(hkapMedium) || string.IsNullOrWhiteSpace(kicadMedium)) return true;
        if (hkapMedium.Trim().Equals(kicadMedium.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return hkapMedium.Trim().Equals("Zu-/Abluft", StringComparison.OrdinalIgnoreCase)
               && kicadMedium.Trim() is var k && (k.Equals("Zuluft", StringComparison.OrdinalIgnoreCase) || k.Equals("Abluft", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Systeme in KICAD, die zu einem HKAP-Medium passen (Systemtyp bildet auf das Medium ab; «Zu-/Abluft» = Zuluft und Abluft).</summary>
    public static List<Element> SystemeFuer(Modell m, string? hkapMedium)
    {
        if (string.IsNullOrWhiteSpace(hkapMedium)) return [];
        return m.DerKlasse("System").Where(s => MediumVon(s.Text("systemtyp")) is { } km && MediumPasst(hkapMedium, km))
            .OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------------------------------------------------------------- Block «kicad» je KICAD-Strang

    /// <summary>Geschosse, die ein Strang berührt (Geschosse seiner Bauteile und direkten Endgeräte, wie die Strangliste), als
    /// HKAP-Etagen-Ids von oben nach unten; Geschosse ohne HKAP-Zuordnung stehen in ohneHkap (Kennzeichen).</summary>
    static (List<string> Ids, List<string> OhneHkap) Geschosse(Modell m, Katalog? k, HkapVerknuepfung link, Element strang, List<Element>? direkt = null)
    {
        var eigene = m.Elemente.Where(e => e.TeilVon == strang.Id).ToList();
        direkt ??= k is null ? [] : Netz.Verbraucher(m, k, strang).Direkt;
        var gs = eigene.Concat(direkt).Select(e => e.Geschoss).Where(g => g is not null).Distinct()
            .Select(g => m.Finde(g)).Where(g => g is not null).Select(g => g!)
            .OrderByDescending(g => g.Zahl("kote") ?? 0).ThenBy(g => g.Kennzeichen, StringComparer.Ordinal).ToList();
        var ids = new List<string>(); var ohne = new List<string>();
        foreach (var g in gs)
        {
            if (link.IdVon(g) is { } id) { if (!ids.Contains(id)) ids.Add(id); }
            else ohne.Add(g.Kennzeichen);
        }
        return (ids, ohne);
    }

    /// <summary>Eintrag eines KICAD-Strangs im Block «kicad.straenge» (Schlüssel in der Reihenfolge, in der HKAP sie schreibt).</summary>
    public static JsonObject Block(Modell m, Katalog? k, Projekt? p, HkapVerknuepfung link, Element s)
    {
        var sys = SystemVon(m, s);
        var direkt = k is null ? [] : Netz.Verbraucher(m, k, s).Direkt;
        var (geschosse, _) = Geschosse(m, k, link, s, direkt);
        int endgeraete = direkt.Count;
        // Leitungslängen kommen aus den Ableitungen des Projekts; ein Strang, der nur in der Arbeitskopie steht (eben angelegt), hat keine
        var laengen = p is not null && p.Modell.Finde(s.Id) is not null ? Ableitungen.Leitungslaengen(p, strangId: s.Id) : [];
        return new JsonObject
        {
            ["id"] = s.Id, ["kennzeichen"] = s.Kennzeichen, ["name"] = s.Name?.Trim() ?? "",
            ["system"] = sys?.Kennzeichen ?? "", ["systemtyp"] = sys?.Text("systemtyp") ?? "",
            ["geschosse"] = new JsonArray(geschosse.Select(g => (JsonNode)JsonValue.Create(g)!).ToArray()),
            ["endgeraete"] = endgeraete,
            ["laengen"] = new JsonArray(laengen.Select(l => (JsonNode)new JsonObject
            {
                ["serie"] = l.Serie, ["groesse"] = l.Groesse, ["laengeM"] = Math.Round(l.LaengeM, 3),
            }).ToArray()),
        };
    }

    /// <summary>Block in feste Form bringen (Schlüsselfolge, Typen, 3 Stellen), damit KICAD- und HKAP-Fassung buchstabengleich vergleichen.</summary>
    public static JsonNode? Normalisiere(JsonNode? n)
    {
        if (n is not JsonArray a) return n;
        static string S(JsonNode? x) => HkapWerte.Text(x) ?? "";
        static JsonArray Liste(JsonNode? x) => new((x as JsonArray ?? new JsonArray()).Select(y => (JsonNode)JsonValue.Create(S(y))!).ToArray());
        var aus = new JsonArray();
        foreach (var x in a.OfType<JsonObject>())
            aus.Add(new JsonObject
            {
                ["id"] = S(x["id"]), ["kennzeichen"] = S(x["kennzeichen"]), ["name"] = S(x["name"]), ["system"] = S(x["system"]), ["systemtyp"] = S(x["systemtyp"]),
                ["geschosse"] = Liste(x["geschosse"]),
                ["endgeraete"] = (int)Math.Round(Json.Zahl(x["endgeraete"]) ?? 0),
                ["laengen"] = new JsonArray((x["laengen"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(l => (JsonNode)new JsonObject
                {
                    ["serie"] = S(l["serie"]), ["groesse"] = S(l["groesse"]), ["laengeM"] = Math.Round(Json.Zahl(l["laengeM"]) ?? 0, 3),
                }).ToArray()),
            });
        return aus;
    }

    // ---------------------------------------------------------------- Felder

    /// <summary>Bezeichnung einer Gruppe: der häufigste nicht leere Name (bei Gleichstand der erste in Kennzeichenfolge), sonst null.</summary>
    public static string? Bezeichnung(IEnumerable<Element> gruppe)
    {
        var namen = gruppe.Select(e => e.Name?.Trim()).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList();
        return namen.Count == 0 ? null : namen.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).First().First();
    }

    public static List<HkapFeld> Felder(HkapVerknuepfung link, Katalog? katalog, Projekt? projekt)
    {
        var cache = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject BlockVon(Modell m, Element s) => cache.TryGetValue(s.Id, out var b) ? b : cache[s.Id] = Block(m, katalog, projekt, link, s);
        HkapFeld Lesen(string name) => new() { Name = name, HkapPfade = [name] };   // nur anzeigen
        return
        [
            // wird nur für einen nicht platzierten Strang als Kennzeichen benutzt; die Kennzeichen der KICAD-Stränge bleiben, wie sie sind
            Lesen("nummer"),
            new HkapFeld
            {
                Name = "bezeichnung", HkapPfade = ["bezeichnung"], Sendbar = true,
                GruppeLesen = (_, g) => T(Bezeichnung(g)),
                GruppeSchreiben = (k, g, w, _) =>
                {
                    var neu = HkapWerte.Leer(w);
                    var alt = Bezeichnung(g);
                    foreach (var e in g)
                    {
                        var n = e.Name?.Trim();
                        if (string.IsNullOrEmpty(n) || (alt is not null && n.Equals(alt, StringComparison.OrdinalIgnoreCase))) e.Name = neu;
                        else k.Hinweise.Add($"{e.Kennzeichen}: Name «{e.Name}» bleibt (weicht von der Bezeichnung «{alt}» der Gruppe ab).");
                    }
                },
            },
            new HkapFeld
            {
                Name = "medium", HkapPfade = ["medium"], Sendbar = true, HkapWertBleibt = true,
                GruppeLesen = (m, g) => T(MediumVonGruppe(m, g)),
            },
            new HkapFeld
            {
                Name = "kicad.straenge", HkapPfade = ["kicad.straenge"], Sendbar = true,
                AusHkap = f => Normalisiere(f.TryGetPropertyValue("kicad.straenge", out var v) ? v : null),
                GruppeLesen = (m, g) => Normalisiere(new JsonArray(g.Select(s => (JsonNode)BlockVon(m, s).DeepClone()).ToArray())),
            },
            Lesen("tempVL"), Lesen("tempRL"), Lesen("anschluss"), Lesen("anschlussEtageId"), Lesen("etagen"),
        ];
    }

    // ---------------------------------------------------------------- Gruppen bilden

    static string? NameKey(Modell m, Element e) =>
        e.Name?.Trim() is { Length: > 0 } n ? $"{MediumVonStrang(m, e)}|{n.ToLowerInvariant()}" : null;

    static readonly Regex NummerEnde = new(@"(\d+(?:\.\d+)*)$", RegexOptions.Compiled);

    /// <summary>Steigzone (von Hand, Strang.steigzone), mit dem Medium: Stränge verschiedener Gewerke bilden keine Gruppe.</summary>
    static string? SteigzoneKey(Modell m, Element e) =>
        e.Text("steigzone")?.Trim() is { Length: > 0 } sz ? $"{MediumVonStrang(m, e)}|sz:{sz.ToLowerInvariant()}" : null;

    /// <summary>Nummer am Ende des Kennzeichens («HZ-VL-1» → 1), mit dem Medium: Stränge verschiedener Gewerke bilden keine Gruppe.</summary>
    static string? NummerKey(Modell m, Element e) =>
        NummerEnde.Match(e.Kennzeichen) is { Success: true } x ? $"{MediumVonStrang(m, e)}|#{x.Groups[1].Value}" : null;

    /// <summary>Bündelt KICAD-Stränge ohne HKAP-Kennung zu Gruppen (je ein neuer HKAP-Strang). Vorschlag, im Trockenlauf sichtbar:
    /// steigzone (Vorgabe) = gleiche Steigzone (im selben Medium); auto = gleicher Name, danach gleiche Kennzeichen-Nummer; name;
    /// kennzeichen; nein = je Strang ein HKAP-Strang.
    /// Eine Gruppe hat höchstens einen Strang je Systemtyp (ein zweiter Heizung Vorlauf mit derselben Nummer beginnt eine neue Gruppe).</summary>
    public static List<List<Element>> Buendeln(Modell m, IReadOnlyList<Element> straenge, string modus)
    {
        var uebrig = straenge.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var gruppen = new List<List<Element>>();
        void Durchgang(Func<Element, string?> schluessel)
        {
            var eimer = new Dictionary<string, List<List<Element>>>(StringComparer.Ordinal);
            foreach (var e in uebrig)
            {
                if (schluessel(e) is not { } key) continue;
                if (!eimer.TryGetValue(key, out var liste)) eimer[key] = liste = [];
                var typ = SystemtypVon(m, e) ?? "";
                var g = liste.FirstOrDefault(x => x.All(y => !(SystemtypVon(m, y) ?? "").Equals(typ, StringComparison.OrdinalIgnoreCase)));
                if (g is null) liste.Add([e]); else g.Add(e);
            }
            foreach (var g in eimer.Values.SelectMany(x => x).Where(x => x.Count >= 2))
            {
                gruppen.Add(g);
                foreach (var e in g) uebrig.Remove(e);
            }
        }
        if (modus is "steigzone") Durchgang(e => SteigzoneKey(m, e));
        if (modus is "auto" or "name") Durchgang(e => NameKey(m, e));
        if (modus is "auto" or "kennzeichen") Durchgang(e => NummerKey(m, e));
        gruppen.AddRange(uebrig.Select(e => new List<Element> { e }));
        return gruppen.OrderBy(g => g[0].Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string BuendelnPruefen(string? wert)
    {
        var w = string.IsNullOrWhiteSpace(wert) ? "steigzone" : wert.Trim().ToLowerInvariant();
        if (!BuendelnArten.Contains(w)) throw new HlksFehler($"buendeln «{wert}» gibt es nicht.", "Erlaubt: " + string.Join(", ", BuendelnArten) + ".");
        return w;
    }

    static List<string> KicadIdsAusHkap(HkapDatensatz h) =>
        (h.Felder["kicad"]?["straenge"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(x => HkapWerte.Leer(x["id"])).Where(x => x is not null).Select(x => x!).ToList();

    /// <summary>Paarung der HKAP-Stränge mit Gruppen von KICAD-Strängen über die Kennung «hkap» (Art, Gruppe, Basis wie bei Räumen).</summary>
    internal static void Paaren(Modell m, HkapVerknuepfung link, HkapArtPlan plan, IReadOnlyList<HkapDatensatz> hkap, HkapBasisDaten basis, Katalog? katalog)
    {
        var basisArt = basis.Art(plan.Name);
        var hkapNach = new Dictionary<string, HkapDatensatz>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in hkap) hkapNach.TryAdd(h.Id, h);
        var jeId = new Dictionary<string, List<Element>>(StringComparer.OrdinalIgnoreCase);
        var ohne = new List<Element>();
        foreach (var e in m.DerKlasse("Strang").OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (link.IdVon(e) is not { } id) { ohne.Add(e); continue; }
            if (!jeId.TryGetValue(id, out var l)) jeId[id] = l = [];
            l.Add(e);
        }

        var vergeben = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HkapPaar Neu(string id, HkapDatensatz? hs, List<Element> gruppe, string art, string zuordnung)
        {
            var p = new HkapPaar
            {
                HkapId = id, Element = gruppe[0], Hkap = hs, HkapFlach = HkapWerte.Flach(hs?.Felder), Basis = basisArt.GetValueOrDefault(id),
                Art = art, Zuordnung = zuordnung,
            };
            p.Gruppe.AddRange(gruppe);
            plan.Paare.Add(p);
            vergeben.Add(id);
            return p;
        }
        foreach (var (id, g) in jeId.OrderBy(kv => kv.Value[0].Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            hkapNach.TryGetValue(id, out var hs);
            Neu(id, hs, g, hs is null ? "geloeschtInHkap" : "gepaart", "kennung");
        }

        // Ohne Kennung, aber HKAP nennt die Element-Id in kicad.straenge (Kennungspaket eines früheren Sendens gescheitert): zuordnen
        foreach (var h in hkap.Where(h => !vergeben.Contains(h.Id)))
        {
            var ids = KicadIdsAusHkap(h);
            if (ids.Count == 0) continue;
            var treffer = ohne.Where(e => ids.Contains(e.Id)).ToList();
            if (treffer.Count == 0) continue;
            foreach (var e in treffer) ohne.Remove(e);
            Neu(h.Id, h, treffer, "gepaart", "kicadId");
        }

        foreach (var h in hkap.Where(h => !vergeben.Contains(h.Id)))
        {
            var b = basisArt.GetValueOrDefault(h.Id);
            var p = new HkapPaar
            {
                HkapId = h.Id, Hkap = h, HkapFlach = HkapWerte.Flach(h.Felder), Basis = b,
                Art = b?.Element is not null ? "geloeschtInKicad" : "neuInHkap",
            };
            plan.Paare.Add(p);
            if (p.Art == "neuInHkap")
            {
                var kand = SystemeFuer(m, HkapWerte.Leer(p.HkapFlach["medium"]));
                p.SystemKandidaten.AddRange(kand.Select(s => s.Kennzeichen));
                p.SystemWahlNoetig = kand.Count != 1;
            }
        }
        foreach (var g in Buendeln(m, ohne, plan.Optionen.Buendeln))
        {
            var p = new HkapPaar { Element = g[0], Art = "neuInKicad" };
            p.Gruppe.AddRange(g);
            plan.Paare.Add(p);
        }

        // Hinweise zu Medium und Geschossen der Gruppen
        foreach (var p in plan.Paare.Where(p => p.Gruppe.Count > 0 && p.Art is "gepaart" or "neuInKicad"))
        {
            var medien = MedienVon(m, p.Gruppe);
            if (medien.Count > 1)
                plan.Hinweise.Add($"{p.ElementText}: Stränge mit verschiedenen HKAP-Medien ({string.Join(", ", medien)}) – das Medium wird nicht geschrieben.");
            else if (medien.Count == 0)
            {
                var typen = p.Gruppe.Select(e => SystemtypVon(m, e)).Where(x => x is not null).Distinct().ToList();
                plan.Hinweise.Add($"{p.ElementText}: der Systemtyp {(typen.Count > 0 ? "«" + string.Join("», «", typen) + "»" : "(keiner)")} hat kein HKAP-Medium (Annahme) – das Medium bleibt leer.");
            }
            foreach (var e in p.Gruppe)
                if (Geschosse(m, katalog, link, e).OhneHkap is { Count: > 0 } fehlt)
                    plan.Hinweise.Add($"{e.Kennzeichen}: berührt {(fehlt.Count == 1 ? "das Geschoss" : "die Geschosse")} {string.Join(", ", fehlt)} ohne HKAP-Zuordnung – fehlt in den Geschossen des Strangs (Geschosse zuerst abgleichen).");
        }
    }

    /// <summary>Gruppen, die erst über die Element-Ids in HKAP (kicad.straenge) zugeordnet wurden, bekommen ihre Kennung auch dann, wenn
    /// beim Senden nichts zu senden ist (sonst bliebe die Zuordnung nur flüchtig).</summary>
    internal static void KennungenNachfuehren(Kontext k, HkapVerknuepfung link, HkapArtPlan plan)
    {
        foreach (var p in plan.Paare.Where(p => p.Art == "gepaart" && p.Zuordnung == "kicadId"))
            foreach (var e in p.Gruppe)
                if (link.IdVon(e) is null && k.Arbeit.Finde(e.Id) is { } aktuell)
                {
                    var c = aktuell.Kopie();
                    HkapAbgleich.KennungSetzen(c, link, p.HkapId!);
                    k.Setzen(c);
                }
    }

    /// <summary>Strang ohne Leitungen und ohne Verweise anderer Elemente: ein nicht platzierter Platzhalter aus HKAP.</summary>
    public static bool OhneInhalt(Modell m, Element strang) =>
        !m.Elemente.Any(e => e.Id != strang.Id && (e.TeilVon == strang.Id || e.Bezuege.ContainsValue(strang.Id)));

    // ---------------------------------------------------------------- Vorschläge, Hinweise, Vorschau

    /// <summary>Neue Stränge auf beiden Seiten mit gleicher Bezeichnung (und verträglichem Medium) sind wahrscheinlich derselbe Strang:
    /// weder anlegen noch senden, bis jemand mit hkap_strang_zuordnen entschieden hat. Gilt auch für einen neuen KICAD-Strang, der
    /// die Bezeichnung eines schon gepaarten HKAP-Strangs trägt (er gehört wohl dazu).</summary>
    internal static void Vorschlaege(Modell m, HkapArtPlan plan)
    {
        static string? Nr(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
        var hkapSeite = plan.Paare.Where(p => p.Hkap is not null && p.Art is "neuInHkap" or "gepaart").ToList();
        foreach (var pk in plan.Paare.Where(p => p.Art == "neuInKicad" && p.Gruppe.Count > 0))
        {
            var name = Nr(Bezeichnung(pk.Gruppe));
            if (name is null) continue;
            var medium = MediumVonGruppe(m, pk.Gruppe);
            var kand = hkapSeite.Where(ph => Nr(HkapWerte.Leer(ph.HkapFlach["bezeichnung"])) == name && MediumPasst(HkapWerte.Leer(ph.HkapFlach["medium"]), medium)).ToList();
            if (kand.Count == 0) continue;
            pk.Vorschlag = string.Join(", ", kand.Select(x => x.HkapId));
            foreach (var ph in kand)
            {
                plan.StrangVorschlaege.Add((ph.HkapId!, pk.ElementText ?? "", "gleiche Bezeichnung"));
                if (ph.Art == "neuInHkap") ph.Vorschlag = ph.Vorschlag is null ? pk.ElementText : ph.Vorschlag + ", " + pk.ElementText;
            }
        }
    }

    internal static string? NeuInHkapHinweis(HkapPaar p)
    {
        if (p.Vorschlag is not null) return $"nicht angelegt: KICAD-Strang {p.Vorschlag} hat dieselbe Bezeichnung – zuerst hkap_strang_zuordnen";
        if (p.SystemWahlNoetig)
            return p.SystemKandidaten.Count == 0
                ? $"nicht angelegt: kein passendes System in KICAD (Medium «{HkapWerte.Leer(p.HkapFlach["medium"]) ?? "–"}») – Wahl «system» angeben oder zuerst das System anlegen"
                : $"nicht angelegt: mehrere passende Systeme ({string.Join(", ", p.SystemKandidaten)}) – Wahl «system» angeben";
        return $"wird nicht platzierter Strang im System {p.SystemKandidaten[0]}";
    }

    internal static string? NeuInKicadHinweis(Modell m, HkapPaar p)
    {
        if (p.Vorschlag is not null) return $"nicht gesendet: HKAP-Strang {p.Vorschlag} hat dieselbe Bezeichnung – zuerst hkap_strang_zuordnen";
        var teile = new List<string>();
        if (p.Gruppe.Count > 1) teile.Add($"{p.Gruppe.Count} KICAD-Stränge in einem neuen HKAP-Strang gebündelt");
        else teile.Add("legt in HKAP einen neuen Strang an");
        if (MediumVonGruppe(m, p.Gruppe) is null) teile.Add("ohne Medium");
        return string.Join(", ", teile);
    }

    internal static void VorschauErgaenzen(Modell m, HkapArtPlan plan, JsonObject o)
    {
        o["nichtPlatziert"] = new JsonArray(m.DerKlasse("Strang").Where(s => s.Kennungen?.ContainsKey("hkap") == true && OhneInhalt(m, s))
            .OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(s => (JsonNode)JsonValue.Create(s.Kennzeichen)!).ToArray());
        o["buendeln"] = plan.Optionen.Buendeln;
        if (plan.StrangVorschlaege.Count > 0)
            o["zuordnungsvorschlaege"] = new JsonArray(plan.StrangVorschlaege.Distinct()
                .Select(v => (JsonNode)new JsonObject { ["hkapId"] = v.HkapId, ["strang"] = v.Strang, ["grund"] = v.Grund }).ToArray());
        var wahl = plan.Paare.Where(p => p is { Art: "neuInHkap", SystemWahlNoetig: true, Vorschlag: null }).ToList();
        if (wahl.Count > 0)
        {
            var alle = m.DerKlasse("System").OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase)
                .Select(s => (JsonNode)new JsonObject { ["kennzeichen"] = s.Kennzeichen, ["systemtyp"] = s.Text("systemtyp") }).ToArray();
            o["systemWahl"] = new JsonArray(wahl.Select(p => (JsonNode)new JsonObject
            {
                ["hkapId"] = p.HkapId, ["medium"] = HkapWerte.Leer(p.HkapFlach["medium"]), ["bezeichnung"] = HkapWerte.Leer(p.HkapFlach["bezeichnung"]),
                ["kandidaten"] = new JsonArray(p.SystemKandidaten.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
                ["systeme"] = new JsonArray(alle.Select(x => x.DeepClone()).ToArray()),
            }).ToArray());
        }
    }

    // ---------------------------------------------------------------- Nicht platzierter Strang aus HKAP

    /// <summary>Legt zu einem neuen HKAP-Strang je gewähltem System einen nicht platzierten Strang an (Element Strang ohne Leitungen,
    /// teilVon dem System). Eindeutiges System ohne Wahl; sonst verlangt die Übernahme die Wahl «system», ohne sie unterbleibt das
    /// Anlegen mit Hinweis. Name aus bezeichnung, Kennzeichen aus nummer (eindeutig gemacht).</summary>
    internal static List<Element> PlatzhalterAnlegen(Kontext k, HkapVerknuepfung link, HkapArtPlan plan, HkapPaar paar, IReadOnlyDictionary<string, string[]>? wahl)
    {
        var f = paar.HkapFlach;
        var medium = HkapWerte.Leer(f["medium"]); var nummer = HkapWerte.Leer(f["nummer"]); var bez = HkapWerte.Leer(f["bezeichnung"]);
        var was = $"HKAP-Strang {paar.HkapId}{(bez is null ? "" : " «" + bez + "»")}";
        List<Element> systeme;
        if (wahl is not null && wahl.TryGetValue(paar.HkapId!, out var namen) && namen.Length > 0)
        {
            systeme = namen.Select(n => k.Arbeit.HoleDerKlasse(n, "System")).GroupBy(s => s.Id).Select(g => g.First()).ToList();
            foreach (var s in systeme.Where(s => !MediumPasst(medium, MediumVon(s.Text("systemtyp")))))
                k.Hinweise.Add($"{was}: das gewählte System {s.Kennzeichen} ({s.Text("systemtyp")}) passt nicht zum Medium «{medium}».");
        }
        else
        {
            var kand = SystemeFuer(k.Arbeit, medium);
            if (kand.Count != 1)
            {
                k.Hinweise.Add(kand.Count == 0
                    ? $"{was} nicht angelegt: kein passendes System in KICAD (Medium «{medium ?? "–"}»). Wahl: hkap_uebernehmen mit system {{ \"{paar.HkapId}\": \"<System>\" }} oder zuerst das System anlegen."
                    : $"{was} nicht angelegt: mehrere passende Systeme ({string.Join(", ", kand.Select(s => s.Kennzeichen))}). Wahl: hkap_uebernehmen mit system {{ \"{paar.HkapId}\": \"<System>\" }} (mehrere Systeme: Liste).");
                return [];
            }
            systeme = kand;
        }
        var angelegt = new List<Element>();
        foreach (var sys in systeme)
        {
            var basisKz = nummer is not null && !nummer.Any(char.IsWhiteSpace) ? nummer : null;
            string kz;
            if (basisKz is null) kz = k.NaechstesKennzeichen("ST");
            else
            {
                kz = systeme.Count > 1 ? $"{basisKz}-{sys.Kennzeichen}" : basisKz;
                for (int n = 2; k.Arbeit.Finde(kz) is not null; n++) kz = $"{(systeme.Count > 1 ? $"{basisKz}-{sys.Kennzeichen}" : basisKz)}-{n}";
            }
            var e = new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = kz, Klasse = "Strang", Name = bez, TeilVon = sys.Id, System = sys.Id };
            HkapAbgleich.KennungSetzen(e, link, paar.HkapId!);
            angelegt.Add(k.Setzen(e));
            k.Hinweise.Add($"Strang {kz} aus {was} angelegt – nicht platziert (ohne Leitungen) im System {sys.Kennzeichen}: Leitungen zeichnen und dem Strang zuordnen, oder mit hkap_strang_zuordnen einem gezeichneten Strang zuordnen.");
        }
        return angelegt;
    }
}

/// <summary>Zuordnung gezeichneter KICAD-Stränge zu einem HKAP-Strang von Hand (analog hkap_raum_zuordnen): mehrere KICAD-Stränge
/// dürfen dieselbe HKAP-Id tragen. Ein nicht platzierter Platzhalter desselben HKAP-Strangs im selben System geht dabei auf.</summary>
public sealed class HkapStrangZuordnen : IBefehl
{
    public string Name => "hkap_strang_zuordnen";
    public string Beschreibung => HkapTexte.StrangZuordnen;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("strang", "text", true, HkapTexte.ParStrang),
        new("hkap", "text", true, HkapTexte.ParHkapStrang),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var link = HkapVerknuepfung.Pflicht(k.Arbeit);
        var pj = link.PjPflicht;
        var namen = (Param.Text(p, "strang", true) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (namen.Length == 0) throw new HlksFehler("Parameter «strang» ist leer.", "Kennzeichen des KICAD-Strangs angeben, mehrere durch Komma getrennt.");
        var id = Param.Text(p, "hkap", true)!.Trim();
        if (id.Contains('/'))
        {
            var teile = id.Split('/', 2);
            if (!teile[0].Equals(pj, StringComparison.OrdinalIgnoreCase))
                throw new HlksFehler($"HKAP-Strang {id} gehört zur Projektierung {teile[0]}, verknüpft ist {pj}.", "Strang-Id der verknüpften Projektierung angeben, z. B. ST1.");
            id = teile[1];
        }
        var straenge = namen.Select(n => k.Arbeit.HoleDerKlasse(n, "Strang")).GroupBy(s => s.Id).Select(g => g.First()).ToList();
        var neue = new List<Element>();
        foreach (var s in straenge)
        {
            if (link.IdVon(s) is { } schon)
            {
                if (schon.Equals(id, StringComparison.OrdinalIgnoreCase)) { k.Hinweise.Add($"{s.Kennzeichen} ist schon HKAP-Strang {id}."); continue; }
                throw new HlksFehler($"{s.Kennzeichen} ist schon HKAP-Strang {schon} zugeordnet.", "Eine bestehende Zuordnung wird nicht stillschweigend umgehängt.");
            }
            neue.Add(s);
        }
        if (neue.Count == 0) return;

        var zustand = HkapZustand.Von(k.Projekt);
        var basis = zustand.Lesen(link);
        var bisher = k.Arbeit.DerKlasse("Strang").Where(s => string.Equals(link.IdVon(s), id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (bisher.Count == 0 && basis.Satz("straenge", id) is null)
        {
            // weder Platzhalter noch Basisstand: in HKAP nachsehen
            var klient = new HkapKlient(HkapKlient.UrlFuer(null, link.Url));
            var hkap = klient.Lesen(link.Projekt, "straenge", pj);
            var gefunden = hkap.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"HKAP-Strang {id} gibt es in {pj} nicht.", "Stränge in HKAP: " + string.Join(", ", hkap.Take(40).Select(x => $"{x.Id} {HkapWerte.Text(x.Felder["nummer"])} {HkapWerte.Text(x.Felder["bezeichnung"])}".Trim())));
            id = gefunden.Id;
            foreach (var s in neue.Where(s => !HkapStraenge.MediumPasst(HkapWerte.Leer(gefunden.Felder["medium"]), HkapStraenge.MediumVonStrang(k.Arbeit, s))))
                k.Hinweise.Add($"{s.Kennzeichen} ({HkapStraenge.SystemtypVon(k.Arbeit, s)}) passt nicht zum Medium «{HkapWerte.Leer(gefunden.Felder["medium"])}» des HKAP-Strangs {id}.");
        }

        foreach (var s in neue)
        {
            var e = s.Kopie();
            e.Kennungen ??= new Dictionary<string, string>();
            e.Kennungen["hkap"] = link.Kennung(id);
            k.Setzen(e);
        }
        // Platzhalter (nicht platzierter Strang) desselben HKAP-Strangs im selben System geht im gezeichneten Strang auf
        var platzhalter = bisher.Where(s => HkapStraenge.OhneInhalt(k.Arbeit, s) && neue.Any(n => n.TeilVon == s.TeilVon)).ToList();
        foreach (var ph in platzhalter)
        {
            k.Loeschen(k.Arbeit.Finde(ph.Id)!);
            k.Hinweise.Add($"Nicht platzierter Strang {ph.Kennzeichen} geht in {string.Join(", ", neue.Where(n => n.TeilVon == ph.TeilVon).Select(n => n.Kennzeichen))} auf.");
        }
        // Erste Begegnung der Gruppe (nur Platzhalter oder noch nichts): Basis der KICAD-Felder vergessen – abweichende Werte werden
        // Konflikte statt stillschweigend gesendet. Kommt der Strang zu einer Gruppe, die es schon gibt, bleibt die Basis.
        if (bisher.All(s => platzhalter.Contains(s)))
        {
            var alt = basis.Satz("straenge", id);
            var felder = (JsonObject?)alt?.Felder.DeepClone() ?? new JsonObject();
            foreach (var f in HkapAbbildung.Felder("straenge", link, k.Katalog, k.Projekt).Where(f => f.Liest))
                foreach (var pfad in f.HkapPfade) felder.Remove(pfad);
            zustand.Vormerken(k, link, [new HkapBasisAenderung("straenge", id, new HkapBasisSatz { Version = alt?.Version, Element = neue[0].Id, Felder = felder, Zeit = DateTimeOffset.Now })]);
        }
        k.Hinweise.Add($"{string.Join(", ", neue.Select(n => n.Kennzeichen))} {(neue.Count == 1 ? "ist" : "sind")} jetzt HKAP-Strang {id}; hkap_abgleich zeigt, welche Werte abweichen.");
    }
}
