// Modul 6 Teil B – Druckverlust durch Reibung in Lüftungskanälen (Physik, Christian 03.11.2026: «fehlt eine normelle regulierung gilt die phisik»).
// Grundlagen in kataloge/stroemung.json (Skript «08 Luftnetzberechnung», HFGT16): Viskosität der Luft (Tab. 1, ρ = η/ν), Rauheit (Tab. 3, oberer Wert),
// Reibungszahl (Tab. 2: laminar 64/Re unter Re 2320, sonst Colebrook – deckt glatt, Übergang und rauh ab), Δp_R = λ·(l/d)·ρ·w²/2 (1-12), d_h = 2ab/(a+b).
// Volumenstrom je Kanal aus den Durchlässen (LueftungMengen); Querschnitt aus der Serie bzw. den Eigenschaften (HlksMasse). Einzelwiderstände (ζ) fehlen:
// im Büro kein ζ-Katalog für Luft (Fundstellenbericht) – nur Reibung, mit Hinweis. Massgebend je Durchlass: Summe der Kanäle bis zum Gerät.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class KanalDruckverlust
{
    public const string Datei = "stroemung.json";
    public const string Text = "Druckverlust durch Reibung im Lüftungskanalnetz (Modul 6 Teil B, Physik): je Kanal Volumenstrom (aus den Durchlässen), Querschnitt, " +
        "hydraulischer Durchmesser, Geschwindigkeit, Re, Reibungszahl λ (laminar bzw. Colebrook), R in Pa/m und Δp; je Durchlass die Summe bis zum Gerät, " +
        "dazu der massgebende Weg. Ohne Einzelwiderstände (ζ-Katalog fehlt). Rechenweg: Abfrage rechenweg (lueftung.kanal_druckverlust).";
    public const string ParTemperatur = "Lufttemperatur in °C (Vorgabe 20)";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("temperatur", "zahl", false, ParTemperatur)];

    static double D(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static double R(double x, int s = 2) => Math.Round(x, s);

    public static JsonObject? Katalog(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, Datei);
        return File.Exists(pfad) ? JsonNode.Parse(File.ReadAllText(pfad)) as JsonObject : null;
    }

    /// <summary>η (Pa·s) und ν (m²/s) der Luft bei t, linear zwischen den Tabellenpunkten; ρ = η/ν.</summary>
    public static (double Eta, double Nu, double Rho)? Luft(JsonObject kat, double t)
    {
        var w = (kat["luft"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().Select(x => (T: D(x["t"]), Eta: D(x["eta"]), Nu: D(x["nu"]))).OrderBy(x => x.T).ToList();
        if (w.Count < 2 || t < w[0].T || t > w[^1].T) return null;
        int i = Math.Max(1, w.FindIndex(x => x.T >= t)); var (a, b) = (w[i - 1], w[i]);
        double f = (t - a.T) / (b.T - a.T), eta = (a.Eta + f * (b.Eta - a.Eta)) * 1e-6, nu = (a.Nu + f * (b.Nu - a.Nu)) * 1e-6;
        return (eta, nu, eta / nu);
    }

    /// <summary>Reibungszahl: laminar λ = 64/Re (Re &lt; reKrit), sonst Colebrook 1/√λ = −2·lg((k/d)/3,71 + 2,51/(Re·√λ)), iteriert.</summary>
    public static double Lambda(double re, double kd, double reKrit = 2320)
    {
        if (re <= 0) return double.NaN;
        if (re < reKrit) return 64 / re;
        double x = 0.02;
        for (int i = 0; i < 50; i++)
        {
            double neu = Math.Pow(-2 * Math.Log10(kd / 3.71 + 2.51 / (re * Math.Sqrt(x))), -2);
            if (Math.Abs(neu - x) < 1e-12) return neu;
            x = neu;
        }
        return x;
    }

    /// <summary>Rauheit k in mm aus der Serie (Stichwort im Namen), sonst Blechkanal gefalzt; oberer Wert der Tabelle.</summary>
    static (double K, string Text) Rauheit(JsonObject kat, string? serie)
    {
        var w = (kat["rauheit"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        string key = serie is null ? "blechkanal"
            : System.Text.RegularExpressions.Regex.IsMatch(serie, "pe|pvc|pp|kunststoff", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "kunststoff"
            : "blechkanal";
        var z = w.FirstOrDefault(x => T(x["key"]) == key) ?? w.FirstOrDefault();
        return z is null ? (0.15, "k fehlt") : (D(z["kBis"]), $"k = {D(z["kBis"])} mm ({T(z["oberflaeche"])}, {T(z["zustand"])}; Tab. 3 S. {kat["rauheit"]?["seite"]}, oberer Wert)" +
            (serie is not null && System.Text.RegularExpressions.Regex.IsMatch(serie, "flex|aludec", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? " – Flexrohr: Herstellerangabe fehlt, wie Blechkanal gerechnet (Annahme)" : ""));
    }

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell; var k = p.Katalog;
        var hinweise = new JsonArray();
        var kat = Katalog(k);
        var aus = new JsonObject { ["hinweise"] = hinweise };
        if (kat is null) { hinweise.Add($"kataloge/{Datei} fehlt – kein Druckverlust."); return aus; }
        double t = Param.Zahl(par, "temperatur") ?? 20;
        if (Luft(kat, t) is not { } luft) { hinweise.Add($"Lufttemperatur {t} °C ausserhalb Tab. 1 – kein Druckverlust."); return aus; }
        double reKrit = D(kat["reibungszahl"]?["reKritisch"]);
        aus["luft"] = new JsonObject { ["t"] = t, ["eta_Pas"] = luft.Eta, ["nu_m2s"] = luft.Nu, ["rho_kgm3"] = R(luft.Rho, 4), ["quelle"] = $"{T(kat["luft"]?["tabelle"])}, S. {kat["luft"]?["seite"]} (ρ = η/ν)" };
        if (par["temperatur"] is null) hinweise.Add("Lufttemperatur 20 °C angenommen (Parameter temperatur).");
        hinweise.Add("Nur Reibung (Δp_R); Einzelwiderstände (ζ) fehlen – im Büro kein ζ-Katalog für Luftkanäle.");
        var netz = LueftungMengen.Ergebnis(m, k);
        var dpJeKanal = new Dictionary<string, double>(StringComparer.Ordinal);
        var zeilen = new JsonArray();
        foreach (var (id, _) in netz.JeKanal.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } e || netz.Kanal(id) is not { } q) continue;
            var z = new JsonObject { ["kanal"] = e.Kennzeichen, ["volumenstrom_m3h"] = q.M3h };
            var mass = HlksMasse.Von(p, e);
            double l = (e.Geometrie?.Laenge() ?? 0) / 1000;
            z["laenge_m"] = R(l, 2);
            if (mass is null) { z["hinweis"] = "Querschnitt unbekannt (Serie bzw. Eigenschaften) – kein Druckverlust."; zeilen.Add(z); continue; }
            double a = mass.Breite / 1000, b = mass.Hoehe / 1000;
            double flaeche = mass.Rechteck ? a * b : Math.PI * a * a / 4, dh = mass.Rechteck ? 2 * a * b / (a + b) : a;
            double w = q.M3h / 3600 / flaeche, re = w * dh / luft.Nu;
            var (kmm, kText) = Rauheit(kat, mass.Serie?.SerieName);
            double lam = Lambda(re, kmm / 1000 / dh, double.IsNaN(reKrit) ? 2320 : reKrit);
            double rpm = lam / dh * luft.Rho * w * w / 2, dp = rpm * l;
            dpJeKanal[id] = dp;
            z["querschnitt"] = mass.Rechteck ? $"{mass.Breite}×{mass.Hoehe} mm" : $"Ø {mass.Breite} mm";
            z["dh_mm"] = R(dh * 1000, 1); z["w_ms"] = R(w, 2); z["re"] = R(re, 0); z["lambda"] = R(lam, 4); z["rauheit"] = kText;
            z["r_Pam"] = R(rpm, 3); z["dp_Pa"] = R(dp, 2);
            z["formel"] = $"Δp = λ·(l/d_h)·ρ·w²/2 = {R(lam, 4)}·({R(l, 2)}/{R(dh, 3)})·{R(luft.Rho, 3)}·{R(w, 2)}²/2 = {R(dp, 2)} Pa (1-12); Re = w·d_h/ν = {R(re, 0)} (1-13)";
            if (q.OhneAngabe.Count > 0) z["hinweis"] = $"{q.OhneAngabe.Count} Durchlass/Durchlässe ohne Volumenstrom – Summe zu klein.";
            zeilen.Add(z);
        }
        aus["kanaele"] = zeilen;
        // je Durchlass: Summe der Kanäle bis zum Gerät (Zuluft stromauf, Abluft stromab)
        var wege = new JsonArray(); (string Kz, double Dp)? max = null;
        foreach (var (did, _) in netz.Durchlaesse)
        {
            if (m.Finde(did) is not { } d) continue;
            bool zu = k.IstArt(d, "Zuluftdurchlass");
            var weg = Netz.Folgen(m, k, d, zu ? "stromauf" : "stromab").Where(x => dpJeKanal.ContainsKey(x.Element.Id)).ToList();
            double s = weg.Sum(x => dpJeKanal[x.Element.Id]);
            wege.Add(new JsonObject { ["durchlass"] = d.Kennzeichen, ["dpReibung_Pa"] = R(s, 1), ["kanaele"] = weg.Count });
            if (max is null || s > max.Value.Dp) max = (d.Kennzeichen, s);
        }
        aus["durchlaesse"] = wege;
        if (max is { } mx) aus["massgebend"] = new JsonObject { ["durchlass"] = mx.Kz, ["dpReibung_Pa"] = R(mx.Dp, 1) };
        return aus;
    }
}
