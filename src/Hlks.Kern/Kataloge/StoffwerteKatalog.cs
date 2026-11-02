// Stoffwerte (kataloge/stoffwerte.json, Modul 5, Christian 02.11.2026): spezifische Wärmekapazität c_p und Dichte ρ der Heiz- und Kältemedien,
// je Fluid als Tabelle mit Quelle; zwischen den Temperaturpunkten linear, ausserhalb kein Wert (der Aufrufer meldet es). Gemische (z. B.
// Ethylenglykol 35 %) als Mittel zweier Fluide je Temperatur – so entschieden und im Katalog als Rechenwert gekennzeichnet.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed record Stoffwert(double CpKjKgK, double RhoKgM3, string Fluid, string Quelle);

public sealed class StoffwerteKatalog
{
    public const string Datei = "stoffwerte.json";

    sealed record Punkt(double T, double? Cp, double? Rho);
    sealed record Fluid(string Name, string Quelle, List<Punkt> Punkte);

    readonly Dictionary<string, Fluid> fluide = new(StringComparer.Ordinal);
    readonly Dictionary<string, (string Name, string[] Mittel, string Quelle)> gemische = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> medien = new(StringComparer.OrdinalIgnoreCase);

    public static StoffwerteKatalog Laden(string pfad)
    {
        var w = JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject ?? throw new HlksFehler($"{Datei} ist leer oder ungültig.");
        var k = new StoffwerteKatalog();
        foreach (var (schluessel, f) in w["fluide"] as JsonObject ?? [])
        {
            var punkte = (f!["punkte"] as JsonArray ?? []).Select(p => new Punkt(Json.Zahl(p!["t"])!.Value, Json.Zahl(p["cp"]), Json.Zahl(p["rho"]))).OrderBy(p => p.T).ToList();
            k.fluide[schluessel] = new Fluid(f["name"]?.GetValue<string>() ?? schluessel, f["quelle"]?.GetValue<string>() ?? throw new HlksFehler($"{Datei}: Fluid {schluessel} ohne Quelle."), punkte);
        }
        foreach (var (schluessel, g) in w["gemische"] as JsonObject ?? [])
            k.gemische[schluessel] = (g!["name"]?.GetValue<string>() ?? schluessel, (g["mittel"] as JsonArray ?? []).Select(x => x!.GetValue<string>()).ToArray(),
                g["quelle"]?.GetValue<string>() ?? throw new HlksFehler($"{Datei}: Gemisch {schluessel} ohne Quelle."));
        foreach (var (medium, fluid) in w["medien"] as JsonObject ?? []) k.medien[medium] = fluid!.GetValue<string>();
        return k;
    }

    static double? Linear(List<Punkt> p, double t, Func<Punkt, double?> wert)
    {
        var mit = p.Where(x => wert(x) is not null).ToList();
        if (mit.Count == 0 || t < mit[0].T - 1e-9 || t > mit[^1].T + 1e-9) return null;
        for (int i = 0; i < mit.Count; i++)
        {
            if (Math.Abs(mit[i].T - t) < 1e-9) return wert(mit[i]);
            if (i + 1 < mit.Count && t > mit[i].T && t < mit[i + 1].T)
            {
                double a = wert(mit[i])!.Value, b = wert(mit[i + 1])!.Value;
                return a + (b - a) * (t - mit[i].T) / (mit[i + 1].T - mit[i].T);
            }
        }
        return null;
    }

    /// <summary>Stoffwerte eines Fluids bzw. Gemischs bei <paramref name="t"/> °C; null ausserhalb der Tabelle.</summary>
    public Stoffwert? Fluidwert(string fluid, double t)
    {
        if (fluide.TryGetValue(fluid, out var f))
            return Linear(f.Punkte, t, p => p.Cp) is double cp && Linear(f.Punkte, t, p => p.Rho) is double rho ? new Stoffwert(cp, rho, f.Name, f.Quelle) : null;
        if (gemische.TryGetValue(fluid, out var g) && g.Mittel.Length > 0)
        {
            var teile = g.Mittel.Select(x => Fluidwert(x, t)).ToList();
            if (teile.Any(x => x is null)) return null;
            return new Stoffwert(teile.Average(x => x!.CpKjKgK), teile.Average(x => x!.RhoKgM3), g.Name, g.Quelle + "; " + string.Join("; ", teile.Select(x => x!.Quelle).Distinct()));
        }
        return null;
    }

    /// <summary>Stoffwerte des Mediums (Kennung, z. B. HZ-VL) bei <paramref name="t"/> °C; null ohne Zuordnung bzw. ausserhalb der Tabelle.</summary>
    public Stoffwert? Wert(string? medium, double t) => medium is not null && medien.TryGetValue(medium, out var fluid) ? Fluidwert(fluid, t) : null;

    public bool KenntMedium(string? medium) => medium is not null && medien.ContainsKey(medium);
}
