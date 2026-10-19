// Prüfbefunde → HKAP-Pendenzen (Modul 1d, hkap_befunde_senden). Jeder Befund ab der gewählten Schwere (Vorgabe
// «fehler») wird eine Pendenz mit quelle «KICAD», quelleId = «<Projektkennung>|<Befund-Schlüssel Code|Element|Text>»
// und raum. Die Projektkennung (8 Zeichen aus der Projekt-Id in projekt.json) trennt die Pendenzen mehrerer KICAD-Projekte
// am selben HKAP-Projekt. Abgleich über quelleId: erneutes Senden trifft dieselbe Pendenz, nie ein Doppel. Den Status
// führt HKAP; KICAD ändert nur den Inhalt der eigenen Pendenzen (aufgabe, bemerkung, raum – Drei-Wege wie bei den
// Räumen, eine Änderung in HKAP bleibt). Behobener Befund: nie löschen, nie den Status setzen – Vorschlag «erledigt» in
// der Bemerkung und als Hinweis, nur für eigene Pendenzen.
// Übergang: Pendenzen ohne Projektkennung (quelleId beginnt mit dem Befund-Code, vor dem 18.10.2026) gelten als eigene,
// wenn sie im eigenen Basisstand stehen; ihre quelleId bekommt beim nächsten Senden die Kennung. Sonst sind sie fremd.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class HkapPendenzen
{
    public const string Quelle = "KICAD";
    public const string Kategorie = "KICAD-Prüfung";
    public const string Marke = "Befund in KICAD behoben";
    static readonly string[] Inhalt = ["aufgabe", "bemerkung", "raum"];
    public static readonly string[] Schweren = ["fehler", "warnung", "hinweis"];

    public sealed record Soll(Befund Befund, JsonObject Felder);
    public sealed record Aenderung(HkapDatensatz Pendenz, JsonObject Felder, string Art);

    public sealed class Plan
    {
        public List<Soll> Neu { get; } = [];
        /// <summary>Art: inhalt (Befund-Inhalt geändert) | behoben (Vorschlag erledigt in der Bemerkung).</summary>
        public List<Aenderung> Aendern { get; } = [];
        /// <summary>Befund behoben, Vorschlag steht schon in der Bemerkung bzw. die Bemerkung darf KICAD nicht ändern.</summary>
        public List<HkapDatensatz> BehobenOhneAenderung { get; } = [];
        /// <summary>Befund besteht weiter, Pendenz ist in HKAP erledigt (Status führt HKAP: nur Hinweis).</summary>
        public List<(HkapDatensatz Pendenz, Befund Befund)> ErledigtAberOffen { get; } = [];
        public int Unveraendert { get; set; }
        /// <summary>Pendenzen mit quelle KICAD eines anderen KICAD-Projekts (oder ohne Kennung und nicht im eigenen
        /// Basisstand): bleiben unberührt.</summary>
        public int Fremd { get; set; }
        /// <summary>Eigene Pendenzen ohne Projektkennung, deren quelleId beim Senden die Kennung bekommt (Übergang).</summary>
        public int Umbenannt { get; set; }
        public List<string> Hinweise { get; } = [];
    }

    public static string SchwerePruefen(string? schwere)
    {
        var s = (schwere ?? "fehler").Trim().ToLowerInvariant();
        return Schweren.Contains(s) ? s : throw new HlksFehler($"Schwere «{schwere}» gibt es nicht.", "schwere: fehler (Vorgabe) | warnung (fehler und warnung) | hinweis (alle)");
    }

    static bool Eq(string? a, string? b) => a is not null && b is not null && a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Stabile Kurzkennung des KICAD-Projekts: die ersten 8 Zeichen der Projekt-Id (projekt.json); ist die Id keine
    /// GUID, die ersten 8 Hex-Zeichen ihrer SHA-256-Prüfsumme.</summary>
    public static string Kurzkennung(ProjektKopf kopf) =>
        Guid.TryParse(kopf.Id, out var g) ? g.ToString("N")[..8]
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kopf.Id ?? "")))[..8].ToLowerInvariant();

    public static string QuelleId(string kennung, Befund b) => $"{kennung}|{b.Schluessel}";

    /// <summary>Befund-Schlüssel einer quelleId: eigene Kennung → Rest; ohne Kennung (beginnt mit einem Befund-Code) → ganze
    /// quelleId mit alt = true; Kennung eines anderen KICAD-Projekts → null.</summary>
    public static (string? Schluessel, bool Alt) Zerlegen(string quelleId, string kennung)
    {
        if (quelleId.StartsWith(kennung + "|", StringComparison.Ordinal)) return (quelleId[(kennung.Length + 1)..], false);
        return Pruefungen.Codes.ContainsKey(quelleId.Split('|', 2)[0]) ? (quelleId, true) : (null, false);
    }

    public static JsonObject SollFelder(Modell m, Befund b, string kennung)
    {
        var e = m.Finde(b.Element);
        var r = e is null ? null : e.Klasse == "Raum" ? e : m.Finde(e.Raum);
        return new JsonObject
        {
            ["aufgabe"] = $"{b.Code} {b.Element}: {b.Text}",
            ["kategorie"] = Kategorie,
            ["prioritaet"] = b.Schwere switch { "fehler" => "hoch", "warnung" => "mittel", _ => "tief" },
            ["bemerkung"] = b.Vorschlag ?? "",
            ["quelle"] = Quelle,
            ["quelleId"] = QuelleId(kennung, b),
            ["raum"] = r is null ? "" : HkapAbbildung.RaumText(r),
        };
    }

    /// <param name="kennung">Kurzkennung dieses KICAD-Projekts (Kurzkennung(Projekt.Kopf)).</param>
    public static Plan Planen(Modell m, Katalog kat, HkapDatenartDef? def, IReadOnlyList<HkapDatensatz> pendenzen, HkapBasisDaten basis, string schwere,
        DateTimeOffset jetzt, string kennung)
    {
        if (def is null) throw new HlksFehler("Der HKAP-Katalog kennt keine Pendenzen (Datenart «pendenzen»).", "HKAP-Fassung mit Austauschstelle Modul 1d starten.");
        bool Darf(string feld, bool neu) => def.Feld(feld) is { } f && f.NachHkap && (neu || f.Fuehrt != "hkap");
        if (!Darf("quelle", true) || !Darf("quelleId", true))
            throw new HlksFehler("Der HKAP-Katalog lässt quelle/quelleId für Pendenzen nicht zu – ohne sie entstünden bei jedem Senden Doppel.", "HKAP-Austauschkatalog prüfen.");
        var plan = new Plan();
        var alle = Pruefungen.Alle(m, kat);
        var aktuell = alle.Select(b => b.Schluessel).ToHashSet(StringComparer.Ordinal);
        // eigene Pendenzen nach Befund-Schlüssel; mit Kennung vor solchen ohne (Übergang)
        var eigene = new Dictionary<string, (HkapDatensatz P, bool Alt)>(StringComparer.Ordinal);
        foreach (var p in pendenzen.Where(p => Eq(HkapWerte.Text(p.Felder["quelle"]), Quelle)))
        {
            var qid = HkapWerte.Text(p.Felder["quelleId"]);
            if (string.IsNullOrEmpty(qid)) continue;
            var (schluessel, alt) = Zerlegen(qid, kennung);
            // anderes KICAD-Projekt, oder ohne Kennung und nicht von hier gesendet: unberührt
            if (schluessel is null || (alt && basis.Satz("pendenzen", p.Id) is null)) { plan.Fremd++; continue; }
            if (eigene.TryGetValue(schluessel, out var da))
            {
                if (da.Alt && !alt) eigene[schluessel] = (p, alt);
                plan.Hinweise.Add($"HKAP-Pendenzen {da.P.Id} und {p.Id} gehören zum selben Befund – {(da.Alt && !alt ? da.P.Id : p.Id)} übergangen.");
                continue;
            }
            eigene[schluessel] = (p, alt);
        }
        int rang = Array.IndexOf(Schweren, schwere);
        foreach (var b in alle.Where(b => Array.IndexOf(Schweren, b.Schwere) is var r && r >= 0 && r <= rang))
        {
            var soll = new Soll(b, SollFelder(m, b, kennung));
            if (!eigene.TryGetValue(b.Schluessel, out var e)) { plan.Neu.Add(soll); continue; }
            var p = e.P;
            if (Eq(HkapWerte.Text(p.Felder["status"]), "erledigt")) plan.ErledigtAberOffen.Add((p, b));
            var bs = basis.Satz("pendenzen", p.Id);
            var aend = new JsonObject();
            foreach (var f in Inhalt)
            {
                bool bekannt = bs?.Felder.ContainsKey(f) == true;
                var fs = HkapAbgleich.Vergleichen(f, true, soll.Felder[f], p.Felder[f], bekannt, bekannt ? bs!.Felder[f] : null, Darf(f, false), false);
                if (fs.Aktion == "senden") aend[f] = soll.Felder[f]?.DeepClone();
            }
            if (e.Alt && Darf("quelleId", false)) { aend["quelleId"] = soll.Felder["quelleId"]?.DeepClone(); plan.Umbenannt++; }
            if (aend.Count > 0) plan.Aendern.Add(new Aenderung(p, aend, "inhalt")); else plan.Unveraendert++;
        }
        foreach (var (schluessel, (p, alt)) in eigene)
        {
            if (aktuell.Contains(schluessel) || Eq(HkapWerte.Text(p.Felder["status"]), "erledigt")) continue;
            var bem = HkapWerte.Text(p.Felder["bemerkung"]) ?? "";
            if (bem.Contains(Marke, StringComparison.Ordinal) || !Darf("bemerkung", false)) { plan.BehobenOhneAenderung.Add(p); continue; }
            var behoben = new JsonObject { ["bemerkung"] = $"{Marke} am {jetzt:dd.MM.yyyy} – Vorschlag: erledigt.{(bem.Length > 0 ? " " + bem : "")}" };
            if (alt && Darf("quelleId", false)) { behoben["quelleId"] = $"{kennung}|{schluessel}"; plan.Umbenannt++; }
            plan.Aendern.Add(new Aenderung(p, behoben, "behoben"));
        }
        return plan;
    }

    /// <summary>Zu sendende Datensätze: neue Pendenzen (nur Felder, die der Katalog von KICAD annimmt) und Änderungen.</summary>
    public static List<(HkapAenderung Satz, string? PendenzId, JsonObject Gesendet, string Text)> Saetze(Plan plan, HkapDatenartDef def)
    {
        var aus = new List<(HkapAenderung, string?, JsonObject, string)>();
        foreach (var s in plan.Neu)
        {
            var felder = new JsonObject();
            foreach (var (k, v) in s.Felder) if (def.Feld(k) is { NachHkap: true }) felder[k] = v?.DeepClone();
            aus.Add((new HkapAenderung { Felder = felder }, null, felder, $"Befund {s.Befund.Code} {s.Befund.Element}"));
        }
        foreach (var a in plan.Aendern)
            aus.Add((new HkapAenderung { Id = a.Pendenz.Id, Version = a.Pendenz.Version, Felder = (JsonObject)a.Felder.DeepClone() }, a.Pendenz.Id, a.Felder,
                a.Art == "behoben" ? $"Pendenz {a.Pendenz.Id} (Befund behoben: Vorschlag erledigt)" : $"Pendenz {a.Pendenz.Id}"));
        return aus;
    }

    public static JsonObject Vorschau(Plan plan) => new()
    {
        ["neu"] = new JsonArray(plan.Neu.Select(s => (JsonNode)new JsonObject
        {
            ["quelleId"] = s.Felder["quelleId"]?.DeepClone(), ["aufgabe"] = s.Felder["aufgabe"]?.DeepClone(), ["raum"] = s.Felder["raum"]?.DeepClone(),
            ["prioritaet"] = s.Felder["prioritaet"]?.DeepClone(),
        }).ToArray()),
        ["geaendert"] = new JsonArray(plan.Aendern.Where(a => a.Art == "inhalt").Select(a => (JsonNode)new JsonObject { ["id"] = a.Pendenz.Id, ["felder"] = a.Felder.DeepClone() }).ToArray()),
        ["erledigtVorschlag"] = new JsonArray(plan.Aendern.Where(a => a.Art == "behoben").Select(a => a.Pendenz).Concat(plan.BehobenOhneAenderung)
            .Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["aufgabe"] = HkapWerte.Kopie(p.Felder["aufgabe"]), ["status"] = HkapWerte.Kopie(p.Felder["status"]) }).ToArray()),
        ["erledigtInHkap"] = new JsonArray(plan.ErledigtAberOffen.Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Pendenz.Id, ["aufgabe"] = HkapWerte.Kopie(x.Pendenz.Felder["aufgabe"]), ["hinweis"] = "Befund besteht in KICAD weiter – Pendenz in HKAP wieder öffnen?",
        }).ToArray()),
        ["unveraendert"] = plan.Unveraendert,
        ["fremd"] = plan.Fremd,
        ["umbenannt"] = plan.Umbenannt,
    };
}
