// Modul 5 Teil B – Heizkörper auslegen: Normleistung des Herstellers auf die Betriebstemperaturen umrechnen und gegen den Bedarf des Raums stellen.
// Grundlagen (kataloge/grundlagen.json, Rangfolge Norm › Hersteller › Büro › Physik): SN EN 442 fehlt im Normenordner – es gelten die Herstellerangaben
// (Normleistung Φn bei normVl/normRl/normRaum, Exponent n) und die Physik (logarithmische Übertemperatur, Wärmestrom des Mediums). Gegenprobe ist das
// Büroblatt «Auslegung Heizkörper» (Christian 04.11.2026 «zur prüfung»), dessen Korrekturen (Luftdruck, Anschlussart, Strahlungsanteil, senkrechter
// Einbau) ohne Quelle sind – sie werden genannt, nicht angewendet.
//   Bedarf je Heizkörper: Raum.heizleistung (z. B. aus heizlast_uebernehmen), auf die Heizkörper des Raums nach Normleistung verteilt (ohne Normleistung gleich).
//   Betrieb: vlTemp/rlTemp der Heizgruppe des Heizkörpers, θi des Raums wie die Heizlast (Raumwert › Bürostandard › ND.2 › SIA 2024).
//   ΔT = (VL − RL) / ln((VL − θi)/(RL − θi)) (logarithmisch), ΔTn = (normVl + normRl)/2 − normRaum (Übertemperatur des Herstellers, wie im Büroblatt);
//   Φ = Φn · (ΔT/ΔTn)^n; erforderliche Normleistung = Bedarf / (ΔT/ΔTn)^n; Deckung = Φ / Bedarf; ṁ = Bedarf / (c_p·(VL − RL)) mit Stoffwerten (stoffwerte.json).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class HeizkoerperAuslegung
{
    public const string Text = "Heizkörper auslegen (Modul 5 Teil B): je Heizkörper Bedarf aus der Heizleistung des Raums (nach Normleistung verteilt), Betriebstemperaturen " +
        "der Heizgruppe und Innentemperatur des Raums, Umrechnung der Normleistung des Herstellers Φ = Φn·(ΔT/ΔTn)^n mit logarithmischer Übertemperatur, Deckung, " +
        "erforderliche Normleistung und Massenstrom. Herstellerangaben am Heizkörper: leistung (Φn), normVl, normRl, normRaum, exponent. Rechenweg: Abfrage rechenweg (heizung.heizkoerper).";
    public const string ParRaum = "Nur Heizkörper in diesem Raum (Kennzeichen oder Id); leer = alle";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("raum", "text", false, ParRaum)];

    static double R(double x, int s = 2) => Math.Round(x, s);

    /// <summary>Logarithmische Übertemperatur; null, wenn VL ≤ RL oder RL ≤ θi.</summary>
    public static double? DeltaTLog(double vl, double rl, double ti) =>
        vl > rl + 1e-9 && rl > ti + 1e-9 ? (vl - rl) / Math.Log((vl - ti) / (rl - ti)) : vl > ti && Math.Abs(vl - rl) < 1e-9 ? vl - ti : null;

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell; var k = p.Katalog;
        Element? nur = Param.Text(par, "raum") is { Length: > 0 } rz ? m.Finde(rz) ?? throw new HlksFehler($"Raum «{rz}» gibt es nicht.") : null;
        var hk = m.DerKlasse("Heizkoerper").Where(e => nur is null || e.Raum == nur.Id).OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var stand = Heizgruppen.Ergebnis(m, k);
        var hl = SanitaerNormtabellen.Laden(k, "heizlast", "heizung");
        var n2021 = SanitaerNormtabellen.Laden(k, "raumnutzung_sia2024_2021", "heizung");
        var n2015 = SanitaerNormtabellen.Laden(k, "raumnutzung_sia2024", "heizung");
        var zu = Heizlast.Zuordnung(k);
        // Anteil je Heizkörper am Raumbedarf: nach Normleistung, ohne Normleistung gleich
        var jeRaum = m.DerKlasse("Heizkoerper").Where(e => e.Raum is not null).GroupBy(e => e.Raum!).ToDictionary(g => g.Key, g => g.ToList());
        var zeilen = new JsonArray();
        foreach (var e in hk)
        {
            var ann = new JsonArray();
            var z = new JsonObject { ["heizkoerper"] = e.Kennzeichen, ["name"] = e.Name };
            var raum = e.Raum is null ? null : m.Finde(e.Raum);
            z["raum"] = raum?.Kennzeichen;
            double? phiN = e.Zahl("leistung"), nVl = e.Zahl("normVl"), nRl = e.Zahl("normRl"), nRaum = e.Zahl("normRaum"), n = e.Zahl("exponent");
            z["normleistung_W"] = phiN; z["normVl"] = nVl; z["normRl"] = nRl; z["normRaum"] = nRaum; z["exponent"] = n;
            // Bedarf
            double? bedarf = null;
            if (raum?.Zahl("heizleistung") is double qr && jeRaum.TryGetValue(raum.Id, out var im))
            {
                bool alleMitN = im.All(x => x.Zahl("leistung") is double v && v > 0);
                double anteil = alleMitN ? phiN!.Value / im.Sum(x => x.Zahl("leistung")!.Value) : 1.0 / im.Count;
                bedarf = qr * anteil;
                z["bedarfText"] = $"Raum {raum.Kennzeichen} heizleistung {qr} W · Anteil {R(anteil, 3)} ({(alleMitN ? "nach Normleistung" : "gleich verteilt")}; {raum.Eigenschaft("heizleistung")?.Herkunft.Quelle ?? raum.Eigenschaft("heizleistung")?.Herkunft.Art})";
            }
            else ann.Add(raum is null ? "Heizkörper ohne Raum – kein Bedarf." : $"Raum {raum.Kennzeichen} ohne heizleistung – zuerst heizlast_uebernehmen bzw. Heizleistung setzen.");
            z["bedarf_W"] = bedarf is double b0 ? R(b0, 0) : null;
            // Betrieb
            var gr = stand.In(e.Id).Select(w => w.Gruppe).FirstOrDefault();
            double? vl = gr?.Zahl("vlTemp"), rl = gr?.Zahl("rlTemp");
            var (ti, tiQ) = raum is null ? (null, "kein Raum") : Heizlast.Innentemperatur(raum, hl, n2021, n2015, zu);
            z["heizgruppe"] = gr?.Kennzeichen; z["vl"] = vl; z["rl"] = rl; z["thetaInt"] = ti; z["thetaIntQuelle"] = tiQ;
            if (gr is null) ann.Add("Keine Heizgruppe – Betriebstemperaturen fehlen (heizgruppen_vorschlagen bzw. heizgruppe_anlegen).");
            else if (vl is null || rl is null) ann.Add($"Heizgruppe {gr.Kennzeichen} ohne vlTemp/rlTemp.");
            // Umrechnung
            var fehlt = new List<string>();
            if (phiN is null) fehlt.Add("leistung (Φn)"); if (nVl is null) fehlt.Add("normVl"); if (nRl is null) fehlt.Add("normRl"); if (nRaum is null) fehlt.Add("normRaum"); if (n is null) fehlt.Add("exponent");
            if (fehlt.Count > 0) ann.Add($"Herstellerangaben fehlen: {string.Join(", ", fehlt)} (Datenblatt mit Quelle; SN EN 442 liegt nicht vor).");
            double? dtLog = vl is double v1 && rl is double r1 && ti is double t1 ? DeltaTLog(v1, r1, t1) : null;
            if (vl is not null && rl is not null && ti is not null && dtLog is null) ann.Add($"Betrieb {vl}/{rl} °C bei θi {ti} °C: keine Übertemperatur (VL > RL > θi verlangt).");
            // Herstellerangabe ohne Übertemperatur (ΔTn ≤ 0, z. B. normRaum über der mittleren Normtemperatur): nicht umrechnen statt durch 0 teilen
            bool normOk = nVl is not double nv || nRl is not double nr || nRaum is not double nm || (nv + nr) / 2 - nm > 1e-9;
            if (!normOk) ann.Add($"Herstellerangabe {nVl}/{nRl}/{nRaum} °C ohne Übertemperatur (ΔTn ≤ 0) – keine Umrechnung; normVl, normRl und normRaum prüfen.");
            if (normOk && dtLog is double dt && nVl is double a && nRl is double bb && nRaum is double c && n is double ex && phiN is double pn)
            {
                double dtN = (a + bb) / 2 - c;
                double f = Math.Pow(dt / dtN, ex);
                double phi = pn * f;
                z["deltaT_K"] = R(dt, 2); z["deltaTn_K"] = R(dtN, 2); z["faktor"] = R(f, 4); z["leistungBetrieb_W"] = R(phi, 0);
                z["formel"] = $"ΔT = ({vl} − {rl}) / ln(({vl} − {ti})/({rl} − {ti})) = {R(dt, 2)} K; ΔTn = ({a} + {bb})/2 − {c} = {R(dtN, 2)} K; Φ = {pn}·({R(dt, 2)}/{R(dtN, 2)})^{ex} = {R(phi, 0)} W";
                if (bedarf is double bd && bd > 0)
                {
                    z["normleistungErforderlich_W"] = R(bd / f, 0);
                    z["deckung"] = R(phi / bd, 3);
                    z["ok"] = phi + 1e-6 >= bd;
                }
            }
            // Massenstrom
            if (bedarf is double bm && vl is double v2 && rl is double r2 && v2 > r2)
            {
                var medium = e.Anschluesse.Select(x => Netz.Medium(m, e, x)).FirstOrDefault(x => Heizgruppen.IstHeizmedium(k, x));
                if (k.Stoffwerte?.Wert(medium, (v2 + r2) / 2) is { } st)
                {
                    double kgh = bm / (st.CpKjKgK * 1000 * (v2 - r2)) * 3600;
                    z["massenstrom_kgh"] = R(kgh, 1);
                    z["massenstromText"] = $"ṁ = Q/(c_p·ΔT) = {R(bm, 0)} W / ({st.CpKjKgK} kJ/(kg·K) · {v2 - r2} K) = {R(kgh, 1)} kg/h ({st.Fluid}, {st.Quelle})";
                }
                else ann.Add($"Stoffwerte fehlen für {medium ?? "?"} – kein Massenstrom.");
            }
            ann.Add("ΔTn als arithmetische Übertemperatur des Herstellers (wie Büroblatt «Auslegung Heizkörper»); Büro-Korrekturen (Luftdruck, Anschlussart, Strahlungsanteil, senkrechter Einbau) ohne Quelle – nicht angewendet.");
            z["annahmen"] = ann;
            zeilen.Add(z);
        }
        return new JsonObject { ["heizkoerper"] = zeilen, ["anzahl"] = zeilen.Count };
    }
}
