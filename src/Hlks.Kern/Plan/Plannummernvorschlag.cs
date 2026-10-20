// Plannummern-Vorschlag (Modul 7 Teil A, Punkt 5) nach dem H+K-Schema der echten Pläne (Revisionspläne Bernstrasse 21.01.2026, entwurf/planmuster.md):
//   «<Projektnummer> <Fach><NN>», Fach H Heizung · L Lüftung · S Sanitär · K Koordination; NN je Geschoss von unten (Reihenfolge nach der Kote im Gebäude),
//   bei S ab 00, sonst ab 01 (Bernstrasse: 2.UG 01, 1.UG 02, EG 03 … Attika 06 bzw. S00 … S05).
//   Sonderblätter (H+K-Konvention aus den Bernstrasse-Plänen, keine Norm): H13 ff. FBH (ab EG aufwärts: EG H13, 1.OG H14 …), H21 Steigzonen Heizung,
//   Sanitär: Grundleitungen = Nummer nach dem obersten Geschossblatt (Bernstrasse 6 Geschosse: S06), Einlagen die drei folgenden (S07–S09), Kanalisation S10 (= Geschosse + 4).
//   Ist die berechnete Nummer im Projekt belegt, gilt die nächste freie Nummer des Fachs (mit Hinweis); der Vorschlag ist überschreibbar.
// Annahmen (im Bericht genannt): Reihenfolge nach der Kote der Geschosse desselben Gebäudes (ein Projekt ohne 2.UG beginnt mit dem tiefsten Geschoss bei 01 bzw. 00);
// FBH-Nummern gelten für Geschosse mit Kote ≥ 0; die Sanitär-Sonderblätter hängen an der Zahl der Geschosse.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern;

/// <summary>Ergebnis des Plannummern-Vorschlags.</summary>
public sealed class PlannummerVorschlag
{
    /// <summary>Vorgeschlagene Plannummer (z. B. «1-0768 H03»); null, wenn kein Vorschlag möglich ist (Begründung nennt den Grund).</summary>
    public string? Nummer { get; set; }
    public string? Projektnummer { get; set; }
    public char? Fach { get; set; }
    public string? Gewerk { get; set; }
    public string? Geschoss { get; set; }
    /// <summary>geschoss | fbh | steigzonen | grundleitungen | einlagen | kanalisation | schema</summary>
    public string Art { get; set; } = "geschoss";
    /// <summary>Vorschlag für den Titel («Heizung Erdgeschoss»).</summary>
    public string? Titel { get; set; }
    public string Begruendung { get; set; } = "";
    public List<string> Hinweise { get; set; } = [];

    public JsonObject Json() => new()
    {
        ["nummer"] = Nummer, ["projektnummer"] = Projektnummer, ["fach"] = Fach?.ToString(), ["gewerk"] = Gewerk, ["geschoss"] = Geschoss, ["art"] = Art,
        ["titel"] = Titel, ["begruendung"] = Begruendung, ["hinweise"] = new JsonArray(Hinweise.Select(h => (JsonNode)JsonValue.Create(h)!).ToArray()),
    };
}

public static class Plannummernvorschlag
{
    /// <summary>Sonderblätter: Schlüssel → Beschreibung für Fehlermeldungen.</summary>
    public static readonly string[] Arten = ["geschoss", "fbh", "steigzonen", "grundleitungen", "einlagen", "kanalisation"];

    /// <summary>FBH-Nummer des ersten Geschosses ab EG (H13); Steigzonen Heizung (H21) – H+K-Konvention der Bernstrasse-Pläne.</summary>
    const int FbhAb = 13, Steigzonen = 21;

    static readonly Regex Nr = new(@"^(?<f>[A-Za-z])(?<n>\d+)", RegexOptions.CultureInvariant);

    /// <summary>Fachbuchstabe zum Gewerk: H, L, S und K (Koordination, wie auf den H+K-Plänen); für andere Gewerke gibt es keinen.</summary>
    public static char? FachVon(string? gewerk) => gewerk switch { "heizung" => 'H', "lueftung" => 'L', "sanitaer" => 'S', "koordination" => 'K', _ => null };

    /// <summary>Belegte Nummern je Fach (grossgeschrieben): Fach → NN → Kennzeichen des Blatts.</summary>
    public static Dictionary<(char Fach, int Nr), string> Belegt(Modell m, string? projektnummer)
    {
        var d = new Dictionary<(char, int), string>();
        foreach (var b in m.DerKlasse("Planblatt").OrderBy(x => x.Kennzeichen, StringComparer.Ordinal))
        {
            var (_, rest) = Plannummern.Teilen(b.Text("nummer"), projektnummer);
            var t = Nr.Match(rest);
            if (t.Success) d.TryAdd((char.ToUpperInvariant(t.Groups["f"].Value[0]), int.Parse(t.Groups["n"].Value)), b.Kennzeichen);
        }
        return d;
    }

    /// <summary>Berechnet den Vorschlag. Alle Angaben sind optional; die Ansicht (Kennzeichen) liefert Geschoss, Gewerk und Art, wenn sie nicht ausdrücklich genannt sind.</summary>
    public static PlannummerVorschlag Berechnen(Projekt p, string? gewerk, string? geschoss, string? ansicht, string? art)
    {
        var m = p.Modell;
        var v = new PlannummerVorschlag();
        var pn = Plaene.Projektnummer(m);
        v.Projektnummer = pn;

        // ---- Angaben aus der Ansicht
        Element? an = string.IsNullOrWhiteSpace(ansicht) ? null : m.HoleDerKlasse(ansicht, "Ansicht");
        string? anArt = an?.Text("art");
        if (an is not null)
        {
            if (string.IsNullOrWhiteSpace(geschoss) && an.Geschoss is not null) geschoss = m.Finde(an.Geschoss)?.Kennzeichen;
            if (string.IsNullOrWhiteSpace(gewerk))
            {
                var gs = (an.Eigenschaft("gewerke")?.Inhalt as JsonArray)?.Select(x => x is JsonValue j && j.TryGetValue<string>(out var s) ? s : null).OfType<string>()
                    .Select(PlanGewerk.Finde).OfType<PlanGewerk.Eintrag>().Where(g => g.Schluessel != "allgemein").Select(g => g.Schluessel).Distinct().ToList() ?? [];
                if (gs.Count == 1) gewerk = gs[0];
                else if (gs.Count > 1) gewerk = "koordination";
            }
            if (string.IsNullOrWhiteSpace(art))
            {
                var dar = an.Text("darstellung");
                if (dar == EinlageGrafik.Grundleitungen) art = "grundleitungen";
                else if (dar == EinlageGrafik.Einlagen) art = "einlagen";
            }
        }
        art = string.IsNullOrWhiteSpace(art) ? "geschoss" : art.Trim().ToLowerInvariant();
        if (!Arten.Contains(art))
            throw new HlksFehler($"Art «{art}» gibt es nicht.", "Erlaubt: " + string.Join(", ", Arten));
        v.Art = art;

        // ---- Gewerk und Fach
        var g0 = string.IsNullOrWhiteSpace(gewerk) ? null : PlanGewerk.Hole(gewerk);
        v.Gewerk = g0?.Schluessel;
        if (anArt == "schema")
        {
            v.Art = "schema";
            v.Begruendung = "Schemas tragen bei H+K nur die Projektnummer (Strangschemas Wasser/Abwasser), keine Fach- und Blattnummer: Plannummer von Hand eintragen.";
            if (pn is not null) v.Hinweise.Add($"Projektnummer des Projekts: {pn}.");
            return v;
        }
        if (g0 is null)
        {
            v.Begruendung = "Das Gewerk fehlt (Heizung, Lüftung, Sanitär oder Koordination): ohne Gewerk gibt es keinen Fachbuchstaben.";
            return v;
        }
        if (FachVon(g0.Schluessel) is not { } fach)
        {
            v.Begruendung = $"Für das Gewerk {g0.Name} kennt das H+K-Plannummernschema keinen Fachbuchstaben (nur H, L, S, K): Plannummer von Hand eintragen.";
            return v;
        }
        v.Fach = fach;

        // ---- Geschosse des Gebäudes nach Kote
        var g = string.IsNullOrWhiteSpace(geschoss) ? null : m.HoleDerKlasse(geschoss, "Geschoss");
        v.Geschoss = g?.Kennzeichen;
        List<Element> reihe = [.. m.DerKlasse("Geschoss").Where(x => g is null || x.TeilVon == g.TeilVon)
            .OrderBy(x => x.Zahl("kote") ?? 0).ThenBy(x => x.Kennzeichen, StringComparer.Ordinal)];
        var belegt = Belegt(m, pn);
        int start = fach == 'S' ? 0 : 1;
        int basis;
        string beschreibung;

        switch (art)
        {
            case "fbh":
                if (fach != 'H') { v.Begruendung = "FBH-Pläne gibt es im Plannummernschema nur bei Heizung (H13 ff.)."; return v; }
                if (g is null) { v.Begruendung = "Das Geschoss fehlt: die FBH-Nummer folgt dem Geschoss (EG H13, 1.OG H14 …)."; return v; }
                var ab = reihe.Where(x => (x.Zahl("kote") ?? 0) >= 0).ToList();
                if (!ab.Contains(g)) { v.Begruendung = $"{g.Kennzeichen} liegt unter ±0.00: FBH-Nummern (H13 ff.) laufen ab dem Erdgeschoss aufwärts."; return v; }
                basis = FbhAb + ab.IndexOf(g);
                beschreibung = $"FBH {g.Kennzeichen}: H{FbhAb} ab Erdgeschoss, Geschoss {ab.IndexOf(g) + 1} von {ab.Count} ab ±0.00";
                v.Titel = $"FBH {g.Name ?? g.Kennzeichen}";
                break;
            case "steigzonen":
                if (fach != 'H') { v.Begruendung = "Steigzonenpläne gibt es im Plannummernschema nur bei Heizung (H21)."; return v; }
                basis = Steigzonen;
                beschreibung = $"Steigzonen Heizung: H{Steigzonen}";
                v.Titel = "Steigzonen Heizung";
                break;
            case "grundleitungen" or "einlagen" or "kanalisation":
                if (fach != 'S') { v.Begruendung = "Grundleitungen, Einlagen und Kanalisation haben im Plannummernschema Sanitär-Nummern (S06–S10)."; return v; }
                int n = reihe.Count;
                if (n == 0) { v.Begruendung = "Es gibt kein Geschoss: die Sonderblätter hängen an der Zahl der Geschosse (Grundleitungen = Geschosse, Einlagen = Geschosse + 1 …)."; return v; }
                basis = art switch { "grundleitungen" => n, "einlagen" => n + 1, _ => n + 4 };   // Bernstrasse 6 Geschosse: S06, S07–S09, S10
                beschreibung = $"{art} nach den {n} Geschossblättern S{start:00}–S{start + n - 1:00}: S{basis:00}" + (art == "einlagen" ? " (erste freie von drei: " + $"S{basis:00}–S{basis + 2:00})" : "");
                v.Titel = art switch
                {
                    "grundleitungen" => g is null ? "Grundleitungen" : $"Grundleitungen {g.Kennzeichen}",
                    "einlagen" => $"{g0.Name} Einlagen" + (g is null ? "" : $" D.ü.{g.Kennzeichen}"),
                    _ => "Kanalisation",
                };
                break;
            default:   // geschoss
                if (g is null) { v.Begruendung = "Das Geschoss fehlt: die Nummer folgt dem Geschoss (von unten: Bernstrasse 2.UG 01, 1.UG 02, EG 03 …)."; return v; }
                basis = start + reihe.IndexOf(g);
                beschreibung = $"{g0.Name} {g.Kennzeichen}: Geschoss {reihe.IndexOf(g) + 1} von {reihe.Count} (von unten, nach Kote) – {fach}{basis:00} (Zählung ab {start:00})";
                v.Titel = $"{g0.Name} {g.Name ?? g.Kennzeichen}";
                break;
        }

        // ---- Belegt? nächste freie Nummer
        int nr = basis;
        string? wer = null;
        while (belegt.TryGetValue((fach, nr), out var kz)) { wer ??= kz; nr++; }
        v.Nummer = (pn is null ? "" : pn + " ") + fach + nr.ToString("00");
        v.Begruendung = beschreibung;
        if (nr != basis) v.Hinweise.Add($"{fach}{basis:00} ist schon vergeben ({wer}) – die nächste freie Nummer ist {fach}{nr:00}.");
        if (pn is null) v.Hinweise.Add("Die Projektnummer fehlt (Projektinformation › projektnummer): die Nummer steht ohne Projektnummer.");
        return v;
    }
}
