// KI-Verständnistest (Abnahme Modul 0a): Eine KI ohne Vorwissen bekommt nur die Werkzeuge des Kerns und
// beantwortet Fragen zum Beispielprojekt. Die richtigen Antworten rechnet der Kern selbst; Bewerten vergleicht.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class KiVerstaendnistest
{
    public sealed record Frage(string Id, string Text, string Format);

    public static readonly IReadOnlyList<Frage> Fragen =
    [
        new("f1", "Welche Endgeräte entwässern über den Fallstrang SW-F2?", "Liste von Kennzeichen"),
        new("f2", "In welchem Raum steht WT-E.02, und an welche Systeme ist er angeschlossen?", "{ \"raum\": Kennzeichen, \"systeme\": [Kennzeichen] }"),
        new("f3", "Welche Endgeräte haben kein Kaltwasser mehr, wenn das Magnetventil MV-1 schliesst?", "Liste von Kennzeichen"),
        new("f4", "Über welche Stränge fliesst das Abwasser von WC-1.02 bis zum Kontrollschacht (in Fliessrichtung), und welches Kennzeichen hat der Kontrollschacht?", "{ \"straenge\": [Kennzeichen in Reihenfolge], \"ende\": Kennzeichen }"),
        new("f5", "Wie viele Meter Schmutzwasserleitung DN 100 enthält das Modell (auf 0.1 m genau)?", "Zahl in Metern"),
        new("f6", "Welcher Datenpunkt löst die Absperrung der Kaltwasser-Hauptleitung aus, und in welchem Raum wird dafür gemessen?", "{ \"datenpunkt\": Kennzeichen, \"raum\": Kennzeichen }"),
        new("f7", "Nur prüfen, nichts ändern: Welche Leitungen hätten einen offenen Anschluss, wenn der Waschtisch WT-1.03 gelöscht würde?", "Liste von Kennzeichen"),
        new("f8", "Erstelle die Raumliste des 1.OG: je Raum Kennzeichen, Fläche in m² und Anzahl Endgeräte.", "[ { \"raum\": Kennzeichen, \"flaeche_m2\": Zahl, \"endgeraete\": Zahl } ]"),
        new("f9", "Erstelle die Strangliste: je Strang Kennzeichen, System und Anzahl direkt angeschlossener Endgeräte.", "[ { \"strang\": Kennzeichen, \"system\": Kennzeichen, \"direkt\": Zahl } ]"),
        new("f10", "Welche Räume im EG haben keinen Sanitäranschluss (kein Endgerät)?", "Liste von Kennzeichen"),
    ];

    /// <summary>Auftrag an die KI: Fragen und Antwortformat. Die KI darf nur die Werkzeuge benutzen.</summary>
    public static string Auftrag()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Du hast Zugriff auf ein Gebäudetechnik-Modell nur über die Werkzeuge des Servers «hlks». Du kennst das Projekt nicht.");
        sb.AppendLine("Ermittle die Antworten ausschliesslich mit diesen Werkzeugen (Tipp: zuerst faehigkeiten und projekt_ueberblick). Ändere nichts am Modell; zum Prüfen von Auswirkungen gibt es den Trockenlauf.");
        sb.AppendLine("Antworte am Schluss mit genau einem JSON-Objekt in einem ```json-Block mit den Schlüsseln f1 bis f10, sonst nichts dahinter.");
        sb.AppendLine();
        foreach (var f in Fragen) sb.AppendLine($"{f.Id}: {f.Text}  Format: {f.Format}");
        return sb.ToString();
    }

    static JsonArray Arr(IEnumerable<string> s) => new(s.Order(StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)JsonValue.Create(x)).ToArray());

    /// <summary>Richtige Antworten, aus dem Modell abgeleitet.</summary>
    public static JsonObject Erwartung(Projekt p)
    {
        var m = p.Modell; var k = p.Katalog;
        var o = new JsonObject();
        o["f1"] = Arr(Netz.Verbraucher(m, k, m.Hole("SW-F2")).Gesamt.Select(e => e.Kennzeichen));
        var wt = m.Hole("WT-E.02");
        o["f2"] = new JsonObject { ["raum"] = m.Finde(wt.Raum)?.Kennzeichen, ["systeme"] = Arr(Netz.SystemeVon(m, wt).Select(s => s.Kennzeichen)) };
        o["f3"] = Arr(Netz.Folgen(m, k, m.Hole("MV-1"), "stromab", "KW").Where(s => Netz.IstEndgeraet(k, s.Element)).Select(s => s.Element.Kennzeichen));
        var weg = Netz.Folgen(m, k, m.Hole("WC-1.02"), "stromab", null, "Kontrollschacht");
        var straenge = new List<string>();
        foreach (var s in weg)
            if (s.Element.TeilVon is { } t && m.Finde(t) is { Klasse: "Strang" } st && (straenge.Count == 0 || straenge[^1] != st.Kennzeichen)) straenge.Add(st.Kennzeichen);
        o["f4"] = new JsonObject
        {
            ["straenge"] = new JsonArray(straenge.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
            ["ende"] = weg.LastOrDefault(s => Netz.Rolle(k, s.Element) == "senke")?.Element.Kennzeichen,
        };
        o["f5"] = Math.Round(m.Elemente.Where(e => k.IstArt(e, "Leitung") && m.Finde(e.System)?.Kennzeichen == "SW" && e.Zahl("dn") == 100)
            .Sum(e => e.Geometrie?.Laenge() ?? 0) / 1000, 2);
        var dpMv = m.DerKlasse("Datenpunkt").First(d => d.Bezuege.GetValueOrDefault("an") == m.Hole("MV-1").Id);
        var rk = m.DerKlasse("Regelkreis").First(r => r.Bezuege.GetValueOrDefault("stellung") == dpMv.Id);
        var ausl = m.Finde(rk.Bezuege["ausloeser"])!;
        var an = m.Finde(ausl.Bezuege["an"])!;
        o["f6"] = new JsonObject { ["datenpunkt"] = ausl.Kennzeichen, ["raum"] = an.Klasse == "Raum" ? an.Kennzeichen : m.Finde(an.Raum)?.Kennzeichen };
        var tl = p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WT-1.03" }, trockenlauf: true);
        o["f7"] = Arr(tl.NeueBefunde.Where(b => b.Code == "P01").Select(b => b.Element).Distinct());
        o["f8"] = new JsonArray(Ableitungen.Raumliste(p, "1.OG")["zeilen"]!.AsArray().Select(z => (JsonNode)new JsonObject
        {
            ["raum"] = z!["raum"]!.GetValue<string>(), ["flaeche_m2"] = z["flaeche_m2"]!.DeepClone(), ["endgeraete"] = z["anzahlEndgeraete"]!.DeepClone(),
        }).ToArray());
        o["f9"] = new JsonArray(Ableitungen.Strangliste(p)["zeilen"]!.AsArray().Select(z => (JsonNode)new JsonObject
        {
            ["strang"] = z!["strang"]!.GetValue<string>(), ["system"] = z["system"]!.GetValue<string>(), ["direkt"] = z["anzahlDirekt"]!.DeepClone(),
        }).ToArray());
        var eg = m.Hole("EG").Id;
        o["f10"] = Arr(m.DerKlasse("Raum").Where(r => r.Geschoss == eg && !m.Elemente.Any(e => e.Raum == r.Id && Netz.IstEndgeraet(k, e))).Select(r => r.Kennzeichen));
        return o;
    }

    static HashSet<string> Menge(JsonNode? n) =>
        n is JsonArray a ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : x?.ToJsonString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase) : [];

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;

    /// <summary>Vergleicht eine KI-Antwort mit der Erwartung. Ergebnis je Frage: richtig/falsch mit Erwartet und Erhalten.</summary>
    public static JsonObject Bewerten(Projekt p, JsonObject antwort)
    {
        var e = Erwartung(p);
        var ergebnisse = new JsonArray();
        int richtig = 0;
        foreach (var f in Fragen)
        {
            var soll = e[f.Id]; var ist = antwort[f.Id];
            bool ok;
            string? hinweis = null;
            switch (f.Id)
            {
                case "f1" or "f3" or "f7" or "f10":
                    ok = Menge(soll).SetEquals(Menge(ist));
                    break;
                case "f2":
                    ok = string.Equals(T(soll?["raum"]), T(ist?["raum"]), StringComparison.OrdinalIgnoreCase) && Menge(soll?["systeme"]).SetEquals(Menge(ist?["systeme"]));
                    break;
                case "f4":
                {
                    var a = soll?["straenge"]?.AsArray().Select(x => T(x)).ToList() ?? [];
                    var b = (ist?["straenge"] as JsonArray)?.Select(x => T(x)).ToList() ?? [];
                    ok = a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase) && string.Equals(T(soll?["ende"]), T(ist?["ende"]), StringComparison.OrdinalIgnoreCase);
                    break;
                }
                case "f5":
                    ok = Json.Zahl(ist) is double z && Math.Abs(z - Json.Zahl(soll)!.Value) <= 0.1;
                    hinweis = "Toleranz ±0.1 m";
                    break;
                case "f6":
                    ok = string.Equals(T(soll?["datenpunkt"]), T(ist?["datenpunkt"]), StringComparison.OrdinalIgnoreCase)
                         && string.Equals(T(soll?["raum"]), T(ist?["raum"]), StringComparison.OrdinalIgnoreCase);
                    break;
                case "f8" or "f9":
                {
                    string schluessel = f.Id == "f8" ? "raum" : "strang";
                    var sollZ = soll!.AsArray().ToDictionary(x => T(x![schluessel])!, x => x!.AsObject(), StringComparer.OrdinalIgnoreCase);
                    var istZ = (ist as JsonArray)?.Where(x => x is JsonObject).ToDictionary(x => T(x![schluessel]) ?? "", x => x!.AsObject(), StringComparer.OrdinalIgnoreCase) ?? [];
                    ok = sollZ.Count == istZ.Count && sollZ.All(kv => istZ.TryGetValue(kv.Key, out var z) && kv.Value.All(feld =>
                        feld.Key == schluessel ||
                        (Json.Zahl(feld.Value) is double sz ? Json.Zahl(z[feld.Key]) is double iz && Math.Abs(sz - iz) <= (feld.Key == "flaeche_m2" ? 0.05 : 0)
                                                            : string.Equals(T(feld.Value), T(z[feld.Key]), StringComparison.OrdinalIgnoreCase))));
                    hinweis = f.Id == "f8" ? "Fläche ±0.05 m²" : null;
                    break;
                }
                default: ok = false; break;
            }
            if (ok) richtig++;
            ergebnisse.Add(new JsonObject
            {
                ["frage"] = f.Id, ["text"] = f.Text, ["richtig"] = ok, ["hinweis"] = hinweis,
                ["erwartet"] = soll?.DeepClone(), ["erhalten"] = ist?.DeepClone(),
            });
        }
        return new JsonObject
        {
            ["richtig"] = richtig, ["fragen"] = Fragen.Count, ["bestanden"] = richtig == Fragen.Count,
            ["modellStand"] = p.Modell.Pruefsumme(), ["ergebnisse"] = ergebnisse,
        };
    }
}
