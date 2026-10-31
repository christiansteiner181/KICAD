// Bewertung von Varianten nach gewichteten Kriterien (entwurf/modul-6.md A-6; gewerkneutral, auch für Modul 12 E). Rein rechnerisch:
//   - Jedes Kriterium fasst eine oder mehrere Kennzahlen zusammen (kleiner ist besser). Jede Kennzahl wird über die Varianten auf 0..1 normiert
//     (0 = beste, 1 = schlechteste; alle gleich = 0), das Kriterium ist der Mittelwert seiner Kennzahlen.
//   - Fehlt eine Kennzahl bei einer Variante, zählt das Kriterium für alle nicht («fehlt»); die übrigen Gewichte werden neu verteilt.
//   - Punkte = Σ Gewicht × (1 − Kriterium) ÷ Σ Gewicht, 0..100. Varianten mit Ausschlussbefund werden ausgewiesen, aber nicht empfohlen.
// Die Gewichte kommen vom Menschen (Katalog bzw. Projekt), nie vom Kern; jedes Ergebnis nennt sie mit Herkunft.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Bewertung
{
    public sealed record Kriterium(string Schluessel, string Name, double Gewicht, IReadOnlyList<string> Kennzahlen, string Herkunft);

    public sealed record Variante(string Schluessel, string Name, IReadOnlyDictionary<string, double?> Kennzahlen, IReadOnlyList<string> Ausschluss);

    public static JsonObject Rechnen(IReadOnlyList<Variante> alle, IReadOnlyList<Kriterium> kriterien)
    {
        var befunde = new JsonArray();
        // Normiert und verrechnet werden nur die Varianten ohne Ausschlussbefund; die übrigen stehen ohne Punkte in der Rangfolge.
        var varianten = alle.Where(v => v.Ausschluss.Count == 0).ToList();
        var gilt = new List<Kriterium>();
        var fehlt = new JsonArray();
        foreach (var k in kriterien)
        {
            var luecke = varianten.Where(v => k.Kennzahlen.Any(z => v.Kennzahlen.GetValueOrDefault(z) is null)).Select(v => v.Schluessel).ToList();
            if (k.Gewicht <= 0) continue;
            if (luecke.Count > 0)
            {
                fehlt.Add(k.Schluessel);
                befunde.Add($"Kriterium «{k.Name}» zählt nicht: Kennzahl fehlt bei {string.Join(", ", luecke)}.");
            }
            else gilt.Add(k);
        }
        var summe = gilt.Sum(k => k.Gewicht);

        // Kriterium je Variante: Mittel der normierten Kennzahlen
        var wert = new Dictionary<(string V, string K), double>();
        foreach (var k in varianten.Count == 0 ? [] : gilt)
        {
            var norm = new Dictionary<string, double>();
            foreach (var z in k.Kennzahlen)
            {
                var werte = varianten.Select(v => v.Kennzahlen[z]!.Value).ToList();
                double min = werte.Min(), max = werte.Max();
                foreach (var v in varianten)
                    norm[v.Schluessel] = norm.GetValueOrDefault(v.Schluessel) + (max > min ? (v.Kennzahlen[z]!.Value - min) / (max - min) : 0);
            }
            foreach (var v in varianten) wert[(v.Schluessel, k.Schluessel)] = norm[v.Schluessel] / k.Kennzahlen.Count;
        }
        double Punkte(Variante v) => summe > 0 ? 100 * gilt.Sum(k => k.Gewicht * (1 - wert[(v.Schluessel, k.Schluessel)])) / summe : 0;

        var liste = varianten.Select(v => (V: v, P: Punkte(v))).OrderByDescending(x => x.P).ThenBy(x => x.V.Schluessel, StringComparer.Ordinal).ToList();
        var waehlbar = liste;
        JsonObject? empfehlung = null;
        if (summe <= 0) befunde.Add("Kein Kriterium zählt – keine Empfehlung.");
        else if (waehlbar.Count == 0) befunde.Add(alle.Count == 0 ? "Keine Variante – keine Empfehlung." : "Alle Varianten haben einen Ausschlussbefund – keine Empfehlung.");
        else
        {
            var beste = waehlbar[0];
            var andere = varianten.Where(v => v != beste.V).ToList();
            // entscheidend: grösster gewichteter Vorsprung gegenüber dem Mittel der übrigen Varianten
            var vorsprung = gilt.Select(k => (k, d: andere.Count == 0 ? 0
                    : k.Gewicht * (andere.Average(v => wert[(v.Schluessel, k.Schluessel)]) - wert[(beste.V.Schluessel, k.Schluessel)])))
                .Where(x => x.d > 0).OrderByDescending(x => x.d).Take(2).Select(x => (JsonNode)JsonValue.Create(x.k.Name)).ToArray();
            empfehlung = new JsonObject
            {
                ["variante"] = beste.V.Schluessel, ["name"] = beste.V.Name, ["punkte"] = Math.Round(beste.P, 1), ["entscheidend"] = new JsonArray(vorsprung),
                ["hinweis"] = "Empfehlung des Kerns nach den Gewichten unten; entscheiden und freigeben tut der Mensch.",
            };
            if (waehlbar.Count > 1 && Math.Abs(waehlbar[0].P - waehlbar[1].P) < 1e-9)
                befunde.Add($"Gleichstand zwischen {waehlbar[0].V.Schluessel} und {waehlbar[1].V.Schluessel} – Wahl nach Reihenfolge.");
        }

        return new JsonObject
        {
            ["gewichte"] = new JsonArray(kriterien.Select(k => (JsonNode)new JsonObject
            {
                ["kriterium"] = k.Schluessel, ["name"] = k.Name, ["gewicht"] = k.Gewicht, ["kennzahlen"] = new JsonArray(k.Kennzahlen.Select(z => (JsonNode)JsonValue.Create(z)).ToArray()),
                ["herkunft"] = k.Herkunft, ["zaehlt"] = gilt.Contains(k),
            }).ToArray()),
            ["rangfolge"] = new JsonArray(liste.Select((x, i) =>
            {
                var o = new JsonObject
                {
                    ["rang"] = i + 1, ["variante"] = x.V.Schluessel, ["name"] = x.V.Name, ["punkte"] = Math.Round(x.P, 1),
                    ["kriterien"] = new JsonObject(gilt.Select(k => KeyValuePair.Create(k.Schluessel, (JsonNode?)Math.Round(wert[(x.V.Schluessel, k.Schluessel)], 3)))),
                };
                return (JsonNode)o;
            }).Concat(alle.Where(v => v.Ausschluss.Count > 0).Select(v => (JsonNode)new JsonObject
            {
                ["rang"] = null, ["variante"] = v.Schluessel, ["name"] = v.Name, ["punkte"] = null,
                ["ausschluss"] = new JsonArray(v.Ausschluss.Select(a => (JsonNode)JsonValue.Create(a)).ToArray()),
            })).ToArray()),
            ["empfehlung"] = empfehlung,
            ["kriterienFehlen"] = fehlt,
            ["befunde"] = befunde,
            ["verfahren"] = "Je Kennzahl über die Varianten normiert (0 = beste, 1 = schlechteste), Kriterium = Mittel seiner Kennzahlen, Punkte = Σ Gewicht × (1 − Kriterium) ÷ Σ Gewicht × 100. " +
                            "Varianten mit Ausschlussbefund werden nicht empfohlen und nicht verrechnet.",
        };
    }
}
