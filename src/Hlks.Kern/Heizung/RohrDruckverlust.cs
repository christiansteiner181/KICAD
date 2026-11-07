// Modul 5 Teil B – Druckverlust durch Reibung im Heizungsrohrnetz (Physik, wie KanalDruckverlust für Luft). Grundlagen: Massenstrom je Leitung aus den
// Verbrauchern (HeizungLeistung: Leistung → ṁ mit der Spreizung der Heizgruppe), Viskosität Wasser NIST (kataloge/stroemung.json › wasser), Rauheit
// Tab. 3 und Reibungszahl Tab. 2 (Skript «08 Luftnetzberechnung»; Reibungszahl und Rauheit gelten für jedes Fluid), Innendurchmesser aus der Serie.
//   ṁ je Leitung = Σ ṁ der Verbraucher dahinter; V̇ = ṁ/ρ; w = V̇/A; Re = w·d/ν; λ laminar bzw. Colebrook; R = λ/d·ρ·w²/2; Δp = R·l.
//   Temperatur je Weg: Vorlauf = vlTemp, Rücklauf = rlTemp der Heizgruppe (ohne: Mittel).
// Einzelwiderstände (ζ) fehlen – das Büroblatt «Vorlage Druckverlust Wasser» führt ζ ohne Quelle (Fundstellenbericht); nur Reibung, mit Hinweis.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RohrDruckverlust
{
    public const string Text = "Druckverlust durch Reibung im Heizungsrohrnetz (Modul 5 Teil B, Physik): je Leitung Massenstrom (Summe der Verbraucher), " +
        "Innendurchmesser der Produktlinie, Geschwindigkeit, Re, λ (laminar bzw. Colebrook), R in Pa/m und Δp, dazu die Summe je Verbraucher über Vor- und Rücklauf " +
        "und der massgebende Verbraucher. Wasser nach NIST; ohne Einzelwiderstände (ζ). Rechenweg: Abfrage rechenweg (heizung.rohr_druckverlust).";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [];

    static double D(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
    static double R(double x, int s = 2) => Math.Round(x, s);

    /// <summary>η (Pa·s) und ρ (kg/m³) von Wasser bei t, linear; null ausserhalb der Tabelle.</summary>
    public static (double Eta, double Rho)? Wasser(JsonObject kat, double t)
    {
        var w = (kat["wasser"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().Select(x => (T: D(x["t"]), Eta: D(x["eta"]), Rho: D(x["rho"]))).OrderBy(x => x.T).ToList();
        if (w.Count < 2 || t < w[0].T || t > w[^1].T) return null;
        int i = Math.Max(1, w.FindIndex(x => x.T >= t)); var (a, b) = (w[i - 1], w[i]);
        double f = (t - a.T) / (b.T - a.T);
        return (a.Eta + f * (b.Eta - a.Eta), a.Rho + f * (b.Rho - a.Rho));
    }

    /// <summary>Rauheit k in mm aus dem Seriennamen (Tab. 3, oberer Wert): Kunststoff/Mehrschicht, gezogen (Kupfer), sonst Stahl gewalzt.</summary>
    static (double K, string Text) Rauheit(JsonObject kat, string? serie)
    {
        var w = (kat["rauheit"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        string key = serie is null ? "stahl_gewalzt"
            : System.Text.RegularExpressions.Regex.IsMatch(serie, "pe-?x|pex|pe-rt|pb|sanipex|uni ?pipe|mehrschicht|kunststoff|flowfit|optiflex|pushfit|instaflex", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "kunststoff"
            : System.Text.RegularExpressions.Regex.IsMatch(serie, "kupfer|\\bcu\\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "gezogen"
            : "stahl_gewalzt";
        var z = w.FirstOrDefault(x => x["key"]?.GetValue<string>() == key);
        return z is null ? (0.05, "k fehlt") : (D(z["kBis"]), $"k = {D(z["kBis"])} mm ({z["oberflaeche"]}, {z["zustand"]}; Tab. 3, oberer Wert{(serie is null ? ", ohne Produktlinie wie Stahl" : "")})");
    }

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        var m = p.Modell; var k = p.Katalog;
        var hinweise = new JsonArray("Nur Reibung (Δp_R); Einzelwiderstände (ζ), Armaturen und Verbraucher fehlen – im Büro kein ζ-Katalog mit Quelle.");
        var aus = new JsonObject { ["hinweise"] = hinweise };
        var kat = KanalDruckverlust.Katalog(k);
        if (kat?["wasser"] is null) { hinweise.Add("kataloge/stroemung.json › wasser fehlt – kein Druckverlust."); return aus; }
        var netz = HeizungLeistung.Ergebnis(m, k);
        var stand = Heizgruppen.Ergebnis(m, k);
        double reKrit = D(kat["reibungszahl"]?["reKritisch"]);
        var dpJeLeitung = new Dictionary<string, double>(StringComparer.Ordinal);
        var zeilen = new JsonArray();
        foreach (var (id, beitraege) in netz.JeLeitung.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } e) continue;
            var z = new JsonObject { ["leitung"] = e.Kennzeichen, ["weg"] = netz.LeitungWeg.GetValueOrDefault(id) };
            var strom = beitraege.Select(b => b.Verbraucher.Id).Distinct().Select(v => netz.Strom.GetValueOrDefault(v)).ToList();
            double kgh = strom.Sum(s => s?.Kgh ?? 0);
            z["massenstrom_kgh"] = R(kgh, 1);
            if (strom.Any(s => s?.Kgh is null)) z["hinweis"] = "Verbraucher ohne Massenstrom (Leistung bzw. Spreizung fehlt) – Summe zu klein.";
            var gr = stand.In(id).Select(w => w.Gruppe).FirstOrDefault();
            double? vl = gr?.Zahl("vlTemp"), rl = gr?.Zahl("rlTemp");
            double? t = netz.LeitungWeg.GetValueOrDefault(id) == "rl" ? rl : vl;
            if (t is null && vl is double a0 && rl is double b0) t = (a0 + b0) / 2;
            var medium = Netz.ElementMedium(m, e) ?? "";
            if (medium.Contains("Glyk", StringComparison.OrdinalIgnoreCase)) { z["hinweis"] = "Wasser-Glykol: Viskosität fehlt – kein Druckverlust."; zeilen.Add(z); continue; }
            if (t is not double tt || RohrDruckverlust.Wasser(kat, tt) is not { } wa) { z["hinweis"] = "Temperatur fehlt bzw. ausserhalb 10–80 °C – kein Druckverlust."; zeilen.Add(z); continue; }
            var serie = HlksMasse.Serie(p, e);
            double? di = serie?.Werte.Innen;
            if (di is not double dmm || dmm <= 0 || kgh <= 0) { z["hinweis"] = di is null ? "Innendurchmesser fehlt (Produktlinie) – kein Druckverlust." : z["hinweis"]?.ToString() ?? "kein Massenstrom"; zeilen.Add(z); continue; }
            double d = dmm / 1000, a = Math.PI * d * d / 4, vdot = kgh / 3600 / wa.Rho, w = vdot / a, nu = wa.Eta / wa.Rho, re = w * d / nu;
            var (kmm, kText) = Rauheit(kat, serie?.SerieName);
            double lam = KanalDruckverlust.Lambda(re, kmm / 1000 / d, double.IsNaN(reKrit) ? 2320 : reKrit);
            double rpm = lam / d * wa.Rho * w * w / 2, l = (e.Geometrie?.Laenge() ?? 0) / 1000, dp = rpm * l;
            dpJeLeitung[id] = dp;
            z["t"] = tt; z["di_mm"] = dmm; z["w_ms"] = R(w, 3); z["re"] = R(re, 0); z["lambda"] = R(lam, 4); z["rauheit"] = kText;
            z["r_Pam"] = R(rpm, 1); z["laenge_m"] = R(l, 2); z["dp_Pa"] = R(dp, 1);
            z["formel"] = $"w = ṁ/(ρ·A) = {R(w, 3)} m/s; Re = w·d/ν = {R(re, 0)}; Δp = λ·(l/d)·ρ·w²/2 = {R(lam, 4)}·({R(l, 2)}/{R(d, 4)})·{R(wa.Rho, 1)}·{R(w, 3)}²/2 = {R(dp, 1)} Pa";
            zeilen.Add(z);
        }
        aus["leitungen"] = zeilen;
        // je Verbraucher: Summe aller Leitungen, die ihn führen (Vor- und Rücklauf)
        var jeVerbraucher = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (id, beitraege) in netz.JeLeitung)
            if (dpJeLeitung.TryGetValue(id, out var dp))
                foreach (var v in beitraege.Select(b => b.Verbraucher.Id).Distinct()) jeVerbraucher[v] = jeVerbraucher.GetValueOrDefault(v) + dp;
        aus["verbraucher"] = new JsonArray(jeVerbraucher.OrderByDescending(x => x.Value).Select(x => (JsonNode)new JsonObject { ["verbraucher"] = m.Finde(x.Key)?.Kennzeichen, ["dpReibung_Pa"] = R(x.Value, 0) }).ToArray());
        if (jeVerbraucher.Count > 0) { var mx = jeVerbraucher.MaxBy(x => x.Value); aus["massgebend"] = new JsonObject { ["verbraucher"] = m.Finde(mx.Key)?.Kennzeichen, ["dpReibung_Pa"] = R(mx.Value, 0) }; }
        return aus;
    }
}
