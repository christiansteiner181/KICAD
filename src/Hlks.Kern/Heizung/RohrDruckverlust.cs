// Modul 5 Teil B – Druckverlust durch Reibung im Heizungsrohrnetz (Physik, wie KanalDruckverlust für Luft). Grundlagen: Massenstrom je Leitung aus den
// Verbrauchern (HeizungLeistung: Leistung → ṁ mit der Spreizung der Heizgruppe), Viskosität Wasser NIST (kataloge/stroemung.json › wasser), Rauheit
// Tab. 3 und Reibungszahl Tab. 2 (Skript «08 Luftnetzberechnung»; Reibungszahl und Rauheit gelten für jedes Fluid), Innendurchmesser aus der Serie.
//   ṁ je Leitung = Σ ṁ der Verbraucher dahinter; V̇ = ṁ/ρ; w = V̇/A; Re = w·d/ν; λ laminar bzw. Colebrook; R = λ/d·ρ·w²/2; Δp = R·l.
//   Temperatur je Weg: Vorlauf = vlTemp, Rücklauf = rlTemp der Heizgruppe (ohne: Mittel); Hauptleitungen ausserhalb einer Gruppe: höchste Gruppe dahinter,
//   Vorlauf + Anlage.vlHysterese (Frage 21). Massenstrom aus dem Bedarf des Raums, ohne Raumwert aus der Normleistung mit Hinweis (Frage 20).
// Rauheit: ein Standardwert k = 0.045 mm (stroemung.json › rohrRauheitStandard; Christian 13.11.2026, N26) statt Raten aus dem Seriennamen.
// Einzelwiderstände (N27, Christian 13.11.2026 «aus den HFGT16-Unterlagen»): ζ nach Skript C2 (Betschart) S. 13–15 (stroemung.json › zetaWasser):
//   Bogen 0.5 (90°, r = 1,5 d; auch flachere Bögen – sichere Seite, Annahme), T-Stück «Abzweig rechtwinklig» je Verbraucher: Trennung/Vereinigung aus der
//   Fliessrichtung der drei Leitungen (sonst Vorlauf = Trennung, Rücklauf = Vereinigung), Abzweig = Weg des Verbrauchers über c, Gegenlauf = einzelner
//   Zu- bzw. Ablauf am Abzweig c; Absperrventil = Durchgangsventil nach DN.
//   Bezugsgeschwindigkeit: die der Leitung am Anschluss auf der Seite des Verbrauchers (Annahme, im Skript nicht genannt). Δp = ζ·ρ·w²/2.
//   Regulier-/Thermostatventile laufen über kVS (Skript S. 27), übrige Armaturen ohne ζ in der Quelle – beide mit Hinweis, nicht gerechnet.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RohrDruckverlust
{
    public const string Text = "Druckverlust durch Reibung im Heizungsrohrnetz (Modul 5 Teil B, Physik): je Leitung Massenstrom (Summe der Verbraucher), " +
        "Innendurchmesser der Produktlinie, Geschwindigkeit, Re, λ (laminar bzw. Colebrook), R in Pa/m und Δp, dazu die Summe je Verbraucher über Vor- und Rücklauf " +
        "und der massgebende Verbraucher. Wasser nach NIST; Einzelwiderstände (ζ) für Bögen, T-Stücke und Absperrventile nach Skript C2. Rechenweg: Abfrage rechenweg (heizung.rohr_druckverlust).";
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

    /// <summary>Rauheit k in mm: Standardwert (N26) aus stroemung.json › rohrRauheitStandard; ohne Eintrag wie bisher aus dem Seriennamen.</summary>
    static (double K, string Text) Rauheit(JsonObject kat, string? serie)
    {
        if (kat["rohrRauheitStandard"] is JsonObject std && D(std["k"]) is var ks && !double.IsNaN(ks))
            return (ks, $"k = {ks} mm (Standardwert, Christian 13.11.2026; {std["quelle"]})");
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
        var hinweise = new JsonArray("Reibung (Δp_R) und Einzelwiderstände (Δp_Z) von Bögen, T-Stücken und Absperrventilen; Verbraucher, Regulier-/Thermostatventile (kVS) und Wärmeerzeuger fehlen.");
        var aus = new JsonObject { ["hinweise"] = hinweise };
        var kat = KanalDruckverlust.Katalog(k);
        if (kat?["wasser"] is null) { hinweise.Add("kataloge/stroemung.json › wasser fehlt – kein Druckverlust."); return aus; }
        var netz = HeizungLeistung.Ergebnis(m, k);
        var stand = Heizgruppen.Ergebnis(m, k);
        double reKrit = D(kat["reibungszahl"]?["reKritisch"]);
        var dpJeLeitung = new Dictionary<string, double>(StringComparer.Ordinal);
        var wJeLeitung = new Dictionary<string, (double W, double Rho)>(StringComparer.Ordinal);
        var zeilen = new JsonArray();
        foreach (var (id, beitraege) in netz.JeLeitung.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (m.Finde(id) is not { } e) continue;
            var z = new JsonObject { ["leitung"] = e.Kennzeichen, ["weg"] = netz.LeitungWeg.GetValueOrDefault(id) };
            var strom = beitraege.Select(b => b.Verbraucher.Id).Distinct().Select(v => netz.Strom.GetValueOrDefault(v)).ToList();
            double kgh = strom.Sum(s => s?.Kgh ?? 0);
            z["massenstrom_kgh"] = R(kgh, 1);
            var hw = new List<string>();
            if (strom.Any(s => s?.Kgh is null)) hw.Add("Verbraucher ohne Massenstrom (Leistung bzw. Spreizung fehlt) – Summe zu klein.");
            if (strom.Any(s => s?.Basis == "normleistung")) hw.Add(HeizungLeistung.HinweisNormleistung);
            var gr = stand.In(id).Select(w => w.Gruppe).FirstOrDefault();
            double? vl = gr?.Zahl("vlTemp"), rl = gr?.Zahl("rlTemp");
            bool rlWeg = netz.LeitungWeg.GetValueOrDefault(id) == "rl";
            double? t = rlWeg ? rl : vl;
            if (t is null && vl is double a0 && rl is double b0) t = (a0 + b0) / 2;
            if (gr is null)
            {
                // Hauptleitung ausserhalb einer Heizgruppe (Erzeuger bis Verteiler; Frage 21, Christian 13.11.2026 «Temperatur der höchsten Gruppe, mit
                // einstellbarer Hysterese»): Gruppe mit der höchsten Vorlauftemperatur unter den Verbrauchern dahinter; Vorlauf + Hysterese der Anlage
                // (vlHysterese, ohne Wert 0 K), Rücklauf = Rücklauf dieser Gruppe (Annahme: keine Mischtemperatur der Gruppen)
                var hoechste = beitraege.Select(b => netz.GruppeJeVerbraucher.GetValueOrDefault(b.Verbraucher.Id)).OfType<Element>()
                    .Where(g => g.Zahl("vlTemp") is not null && g.Zahl("rlTemp") is not null).DistinctBy(g => g.Id).MaxBy(g => g.Zahl("vlTemp")!.Value);
                if (hoechste is not null)
                {
                    var anlage = Heizgruppen.SystemeVon(m, e).Select(s => m.Finde(s.TeilVon)).FirstOrDefault(a => a?.Klasse == "Anlage");
                    double hy = anlage?.Zahl("vlHysterese") ?? 0;
                    t = rlWeg ? hoechste.Zahl("rlTemp") : hoechste.Zahl("vlTemp") + hy;
                    z["temperaturAus"] = rlWeg
                        ? $"Hauptleitung: Rücklauf der Gruppe {hoechste.Kennzeichen} mit der höchsten Vorlauftemperatur (Annahme, keine Mischtemperatur)"
                        : $"Hauptleitung: Vorlauf der Gruppe {hoechste.Kennzeichen} {hoechste.Zahl("vlTemp")} °C + Hysterese {hy} K{(anlage is null ? "" : $" (Anlage {anlage.Kennzeichen} vlHysterese)")}";
                }
            }
            if (hw.Count > 0) z["hinweis"] = string.Join(" ", hw);
            var medium = Netz.ElementMedium(m, e) ?? "";
            if (medium.Contains("Glyk", StringComparison.OrdinalIgnoreCase)) { z["hinweis"] = "Wasser-Glykol: Viskosität fehlt – kein Druckverlust."; zeilen.Add(z); continue; }
            if (t is not double tt || RohrDruckverlust.Wasser(kat, tt) is not { } wa) { z["hinweis"] = "Temperatur fehlt bzw. ausserhalb 10–80 °C – kein Druckverlust."; zeilen.Add(z); continue; }
            z["t"] = tt;
            var serie = HlksMasse.Serie(p, e);
            double? di = serie?.Werte.Innen;
            if (di is not double dmm || dmm <= 0 || kgh <= 0) { z["hinweis"] = di is null ? "Innendurchmesser fehlt (Produktlinie) – kein Druckverlust." : z["hinweis"]?.ToString() ?? "kein Massenstrom"; zeilen.Add(z); continue; }
            double d = dmm / 1000, a = Math.PI * d * d / 4, vdot = kgh / 3600 / wa.Rho, w = vdot / a, nu = wa.Eta / wa.Rho, re = w * d / nu;
            var (kmm, kText) = Rauheit(kat, serie?.SerieName);
            double lam = KanalDruckverlust.Lambda(re, kmm / 1000 / d, double.IsNaN(reKrit) ? 2320 : reKrit);
            double rpm = lam / d * wa.Rho * w * w / 2, l = (e.Geometrie?.Laenge() ?? 0) / 1000, dp = rpm * l;
            dpJeLeitung[id] = dp;
            wJeLeitung[id] = (w, wa.Rho);
            z["di_mm"] = dmm; z["w_ms"] = R(w, 3); z["re"] = R(re, 0); z["lambda"] = R(lam, 4); z["rauheit"] = kText;
            z["r_Pam"] = R(rpm, 1); z["laenge_m"] = R(l, 2); z["dp_Pa"] = R(dp, 1);
            z["formel"] = $"w = ṁ/(ρ·A) = {R(w, 3)} m/s; Re = w·d/ν = {R(re, 0)}; Δp = λ·(l/d)·ρ·w²/2 = {R(lam, 4)}·({R(l, 2)}/{R(d, 4)})·{R(wa.Rho, 1)}·{R(w, 3)}²/2 = {R(dp, 1)} Pa";
            zeilen.Add(z);
        }
        aus["leitungen"] = zeilen;
        // Einzelwiderstände (N27): je Bauteil und Verbraucher ζ nach Skript C2, Geschwindigkeit der Leitung auf der Seite des Verbrauchers
        var dpZeta = new Dictionary<string, double>(StringComparer.Ordinal);
        var ohneZeta = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int ohneGeschwindigkeit = 0;
        var zetaZeilen = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (kat["zetaWasser"] is JsonObject zk)
            foreach (var (bid, liste) in netz.JeBauteil.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
            {
                if (m.Finde(bid) is not { } b) continue;
                foreach (var (v, ein, weg) in liste)
                {
                    var (zeta, art, seite) = Zeta(m, k, zk, b, ein, weg);
                    if (zeta is not double zz || double.IsNaN(zz)) { if (art is not null) ohneZeta.Add($"{b.Kennzeichen} ({art})"); continue; }
                    var nb = Gegenueber(m, b, ein);
                    if (nb is null || !wJeLeitung.TryGetValue(nb.Value.Element.Id, out var wr)) { ohneGeschwindigkeit++; continue; }
                    double dpz = zz * wr.Rho * wr.W * wr.W / 2;
                    dpZeta[v] = dpZeta.GetValueOrDefault(v) + dpz;
                    var schl = $"{bid}|{art}|{weg}";
                    if (!zetaZeilen.TryGetValue(schl, out var zr))
                        zetaZeilen[schl] = zr = new JsonObject { ["bauteil"] = b.Kennzeichen, ["weg"] = weg, ["art"] = art, ["zeta"] = zz, ["quelle"] = $"Skript C2 (Betschart) Seite {seite}", ["verbraucher"] = 0, ["w_ms"] = R(wr.W, 3), ["dp_Pa"] = R(dpz, 1) };
                    zr["verbraucher"] = zr["verbraucher"]!.GetValue<int>() + 1;
                }
            }
        aus["einzelwiderstaende"] = new JsonArray(zetaZeilen.Values.Select(x => (JsonNode)x).ToArray());
        if (ohneZeta.Count > 0) hinweise.Add($"Ohne ζ (nicht gerechnet): {string.Join(", ", ohneZeta.Take(15))}{(ohneZeta.Count > 15 ? $" … ({ohneZeta.Count})" : "")} – Regulier-/Thermostatventile über kVS, übrige ohne Wert in Skript C2.");
        if (ohneGeschwindigkeit > 0) hinweise.Add($"{ohneGeschwindigkeit} Einzelwiderstände ohne Geschwindigkeit (keine gerechnete Leitung am Anschluss auf der Seite des Verbrauchers) – nicht gerechnet.");
        // je Verbraucher: Summe aller Leitungen, die ihn führen (Vor- und Rücklauf)
        // Leitungen ohne Druckverlust (Temperatur, Innendurchmesser bzw. Massenstrom fehlt) und Maschen werden je Verbraucher genannt – die Summe
        // ist dann zu klein bzw. nicht eindeutig (in einer Masche trägt jeder der parallelen Wege den vollen Strom und zählt mit).
        var jeVerbraucher = new Dictionary<string, double>(StringComparer.Ordinal);
        double Gesamt(string v) => jeVerbraucher.GetValueOrDefault(v) + dpZeta.GetValueOrDefault(v);
        var ohneDp = new Dictionary<string, int>(StringComparer.Ordinal);
        var inMasche = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, beitraege) in netz.JeLeitung)
            foreach (var v in beitraege.Select(b => b.Verbraucher.Id).Distinct())
            {
                if (dpJeLeitung.TryGetValue(id, out var dp)) jeVerbraucher[v] = jeVerbraucher.GetValueOrDefault(v) + dp;
                else ohneDp[v] = ohneDp.GetValueOrDefault(v) + 1;
                if (netz.MaschenLeitungen.Contains(id)) inMasche.Add(v);
            }
        JsonObject Zeile(string v, double dp)
        {
            var o = new JsonObject { ["verbraucher"] = m.Finde(v)?.Kennzeichen, ["dpReibung_Pa"] = R(dp, 0), ["dpEinzel_Pa"] = R(dpZeta.GetValueOrDefault(v), 0), ["dp_Pa"] = R(Gesamt(v), 0) };
            var hw = new List<string>();
            if (ohneDp.TryGetValue(v, out var n)) { o["leitungenOhneDp"] = n; hw.Add($"{n} Leitung(en) auf dem Weg ohne Druckverlust – Summe zu klein."); }
            if (inMasche.Contains(v)) hw.Add("Weg mit Masche (Ring, Tichelmann): parallele Leitungen mit vollem Massenstrom mitgezählt – Summe nicht eindeutig (Befund P39).");
            if (hw.Count > 0) o["hinweis"] = string.Join(" ", hw);
            return o;
        }
        aus["verbraucher"] = new JsonArray(jeVerbraucher.OrderByDescending(x => Gesamt(x.Key)).Select(x => (JsonNode)Zeile(x.Key, x.Value)).ToArray());
        if (jeVerbraucher.Count > 0)
        {
            var mx = jeVerbraucher.MaxBy(x => Gesamt(x.Key));
            aus["massgebend"] = new JsonObject { ["verbraucher"] = m.Finde(mx.Key)?.Kennzeichen, ["dpReibung_Pa"] = R(mx.Value, 0), ["dpEinzel_Pa"] = R(dpZeta.GetValueOrDefault(mx.Key), 0), ["dp_Pa"] = R(Gesamt(mx.Key), 0) };
        }
        return aus;
    }

    static (Element Element, Anschluss Anschluss)? Gegenueber(Modell m, Element e, string anschluss)
    {
        var g = m.VerbindungAn(e.Id, anschluss)?.Gegenueber(e.Id, anschluss);
        var nb = m.Finde(g?.Element);
        var na = nb?.AnschlussNamens(g!.Anschluss);
        return nb is null || na is null ? null : (nb, na);
    }

    /// <summary>ζ eines Bauteils für einen Verbraucher (N27, Skript C2): Wert, Art, Seite. Wert null mit Art = in der Quelle kein ζ (Hinweis);
    /// beides null = kein Widerstand im Sinne der Tabelle (Pumpe und andere).</summary>
    public static (double? Zeta, string? Art, int Seite) Zeta(Modell m, Katalog k, JsonObject zk, Element b, string ein, string weg)
    {
        static double Z(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
        if (k.IstArt(b, "Bogen"))
            return (Z(zk["bogen"]?["werte"]?[0]?["zeta"]), "Bogen 90°, r = 1,5 d", (int)Z(zk["bogen"]?["seite"]));
        if (k.IstArt(b, "TStueck"))
        {
            var t = zk["tstueck"]?["werte"]; int s = (int)Z(zk["tstueck"]?["seite"]);
            // Fluss je Anschluss aus der angeschlossenen Leitung (am «ende» = hinein, am «anfang» = hinaus): 1 hinein + 2 hinaus = Stromtrennung,
            // 2 hinein + 1 hinaus = Stromvereinigung; unbekannt: Vorlauf = Trennung, Rücklauf = Vereinigung (Annahme)
            int? Fluss(string an) => Gegenueber(m, b, an) is { } g && k.IstArt(g.Element, "Leitung") ? g.Anschluss.Name switch { "ende" => 1, "anfang" => -1, _ => null } : null;
            var fl = new[] { "a", "b", "c" }.ToDictionary(x => x, Fluss);
            int hinein = fl.Values.Count(x => x == 1), hinaus = fl.Values.Count(x => x == -1);
            bool trennung = hinein == 1 && hinaus == 2 ? true : hinein == 2 && hinaus == 1 ? false : weg == "vl";
            bool abzweig = ein.Equals("c", StringComparison.OrdinalIgnoreCase);
            if (trennung)
                return fl["c"] == 1 ? (Z(t?["gegenlaufTrennung"]), "T-Stück Gegenlauf, Stromtrennung", s)
                    : abzweig ? (Z(t?["abzweigTrennung"]), "T-Stück Abzweig, Stromtrennung", s) : (Z(t?["durchgangTrennung"]), "T-Stück Durchgang, Stromtrennung", s);
            return fl["c"] == -1 ? (Z(t?["gegenlaufVereinigung"]), "T-Stück Gegenlauf, Stromvereinigung", s)
                : abzweig ? (Z(t?["abzweigVereinigung"]), "T-Stück Abzweig, Stromvereinigung", s) : (Z(t?["durchgangVereinigung"]), "T-Stück Durchgang, Stromvereinigung", s);
        }
        if (k.IstArt(b, "Ventil"))
        {
            var art = b.Text("art");
            if (art == "absperrventil")
            {
                double? dn = b.Zahl("dn") ?? (Gegenueber(m, b, ein) is { } g ? g.Element.Zahl("dn") : null);
                if (dn is not double dd) return (null, "Absperrventil ohne DN", 15);
                var zeile = (zk["durchgangsventil"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => Z(x["dnBis"]) >= dd);
                return zeile is null ? (null, $"Absperrventil DN {dd} ausserhalb der Tabelle", 15) : (Z(zeile["zeta"]), $"Durchgangsventil DN {dd}", (int)Z(zk["durchgangsventil"]?["seite"]));
            }
            return (null, art is "regulierventil" or "thermostatventil" or "strangregulierventil" or "regelventil" or "zonenventil" ? $"{art}: kVS statt ζ" : art ?? "Ventil", 0);
        }
        if (k.IstArt(b, "Armatur") || k.IstArt(b, "Reduktion")) return (null, b.Klasse, 0);
        return (null, null, 0);
    }
}
