// Modul 5 Teil B-H – Norm-Heizlast je Raum nach SN EN 12831:2003 / SIA 384.201 mit Nationalem Anhang ND (Entscheide Christian 03.11.2026:
// «384.201:2003 jetzt», Innentemperatur «Raumwert, sonst SIA 2024», Aussentemperatur «SIA 2028 Tab. 6 übertragen»; Grundsatz «letzt endlich
// [beruht alles] auf physik... nach norm ist nur die beweislast umgedreht»). Spezifikation: entwurf/modul-5.md, Teil B-H; Rechenweg im
// Klartext: kataloge/rechenregister.json (heizung.heizlast). Gleichungsnummern wie in der Norm.
// Werte NICHT im Repository: HLKS_NORMTABELLEN\heizung\heizlast.json (ND, Uequiv Tab. 4–7), sia2028_auslegung.json (Tab. 6, 1a, 2),
// raumnutzung_sia2024.json (Tab. 9). Fehlt eine Datei, rechnet die Abfrage nicht und sagt es.
//   θe   = SIA 2028 Tab. 6 der Klimastation (Gebaeude.thetaE geht vor) + Höhenkorrektur ND.1.1.1 + τ-Korrektur Tab. ND.1; θm,e = Tab. 2 «Jahr».
//   θint = Raum.tempWinter, sonst SIA 2024 Tab. 9 «Winter» nach Raum.nutzung (Nr. oder Name).
//   ΦT   = (HT,ie + HT,iue + HT,ig + HT,ij)·(θint − θe) (2) aus den Raumgrenzen (Modul 1c, Innenmasse – Annahme V1) und den Wärmebrücken
//          mit ψ bzw. χ und Quelle (Klasse Waermebruecke, ek = el = 1,0 ND.4.1); bu (5)/(6); Erdreich (8) mit fg1 ND.5, fg2, Uequiv Tab. 4–7, B' (9),
//          GW = 1,0; HT,ij (10) mit fij ≥ 0.
//   ΦV   = HV·(θint − θe) (11), HV = V̇·ρc (12) mit ρc nach (37) bzw. 0,34 (13); ohne Anlage V̇ = max(V̇inf, V̇min) (14), V̇min = 0,3·V (ND.5.2),
//          V̇inf = 2·V·n50·e·ε (17) mit ND.7–ND.9; mit Anlage V̇ = V̇inf + V̇su·fV (15), V̇inf = 0,1·V (ND.5.1), fV = 0 (Zuluft mit Raumtemperatur, Annahme).
//   ΦRH  = 0 (ND.6); ΦHL = ΦT + ΦV (21); Gebäude Σ ΦT + f·Σ ΦV (22) mit f nach ND.5.7.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class Heizlast
{
    public const string Text = "Norm-Heizlast je Raum und Gebäude nach SN EN 12831:2003 / SIA 384.201 mit Nationalem Anhang ND (Modul 5 Teil B): Aussentemperatur aus " +
        "SIA 2028 Tab. 6 der Klimastation des Gebäudes mit Höhen- und Zeitkonstanten-Korrektur, Innentemperatur aus dem Raum bzw. SIA 2024, Transmission aus den " +
        "Raumgrenzen (aussen, unbeheizt mit bu, Erdreich mit Uequiv, beheizt mit anderer Temperatur) und den Wärmebrücken mit ψ, Lüftung aus Raumvolumen, n50, " +
        "Mindestluftwechsel bzw. Lüftungsanlage. Je Raum alle H-Werte mit Beiträgen, ΦT, ΦV, ΦHL und Annahmen; dazu die Gebäudesumme. Normtabellen im Büroordner " +
        "(HLKS_NORMTABELLEN); fehlen sie, sagt die Antwort es. Rechenweg: Abfrage rechenweg (heizung.heizlast).";
    public const string ParRaum = "Nur dieser Raum (Kennzeichen oder Id); leer = alle beheizten Räume";
    public const string ParGebaeude = "Gebäude (Kennzeichen oder Id); leer = das einzige bzw. erste Gebäude";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("raum", "text", false, ParRaum), new("gebaeude", "text", false, ParGebaeude)];

    public const string Norm = "SN EN 12831:2003 / SIA 384.201 mit Nationalem Anhang ND";

    static double D(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static double R(double x, int s = 2) => Math.Round(x, s);
    static IEnumerable<JsonObject> Zeilen(JsonNode? t) => (t?["werte"] as JsonArray ?? []).OfType<JsonObject>();

    /// <summary>Aussenklima des Gebäudes: θe mit Herleitung, θm,e, ρ·c der Luft.</summary>
    public sealed record Aussen(double ThetaE, double? ThetaMe, double RhoC, string Herleitung, List<string> Annahmen);

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell; var k = p.Katalog;
        var hinweise = new JsonArray();
        var hl = SanitaerNormtabellen.Laden(k, "heizlast", "heizung");
        var klima = SanitaerNormtabellen.Laden(k, "sia2028_auslegung", "heizung");
        var nutz = SanitaerNormtabellen.Laden(k, "raumnutzung_sia2024", "heizung");
        var ordner = Path.Combine(SanitaerNormtabellen.Ordner(k), "heizung");
        var aus = new JsonObject { ["norm"] = Norm, ["normtabellen"] = ordner, ["hinweise"] = hinweise };
        if (hl is null) { hinweise.Add($"Normtabellen Heizlast fehlen ({Path.Combine(ordner, "heizlast.json")}) – keine Heizlast."); return aus; }
        var geb = Param.Text(par, "gebaeude") is { Length: > 0 } g ? m.Finde(g) ?? throw new HlksFehler($"Gebäude «{g}» gibt es nicht.")
                  : m.DerKlasse("Gebaeude").OrderBy(x => x.Kennzeichen).FirstOrDefault();
        if (geb is null) { hinweise.Add("Kein Gebäude im Modell – Klimastation und Terrain fehlen."); return aus; }
        var a = AussenKlima(geb, hl, klima, hinweise);
        if (a is null) return aus;
        aus["aussen"] = new JsonObject
        {
            ["thetaE"] = a.ThetaE, ["thetaMe"] = a.ThetaMe, ["rhoC_Wh_m3K"] = R(a.RhoC, 4), ["herleitung"] = a.Herleitung,
            ["annahmen"] = new JsonArray(a.Annahmen.Select(x => (JsonNode)x).ToArray()),
        };
        Element? nur = Param.Text(par, "raum") is { Length: > 0 } rz ? m.Finde(rz) ?? throw new HlksFehler($"Raum «{rz}» gibt es nicht.") : null;
        var alle = m.DerKlasse("Raum").ToList();
        var rg = Raumgrenzen.Rechnen(m, k, alle);
        foreach (var h in rg.Hinweise.Take(5)) hinweise.Add("Raumgrenzen: " + h);
        double terrain = geb.Zahl("terrain") ?? 0;
        // θint aller Räume (auch der Nachbarn)
        var theta = new Dictionary<string, (double? Wert, string Quelle)>(StringComparer.Ordinal);
        var zuordnung = Zuordnung(k);
        foreach (var r in alle) theta[r.Id] = Innentemperatur(r, nutz, zuordnung);
        double bStrich = BStrich(rg, hl, out var bText);
        var wb = Waermebruecken(m, rg);
        var zeilen = new JsonArray();
        double sT = 0, sV = 0; bool mech = false;
        foreach (var rm in rg.Raeume.OrderBy(x => x.Raum.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var r = rm.Raum;
            if (!Raumgrenzen.Beheizt(r)) continue;
            if (nur is not null && nur.Id != r.Id) continue;
            var (z, rohT, rohV) = Raum(r, rm, rg.Grenzen.Where(x => x.Raum.Id == r.Id).ToList(), theta, a, hl, geb, terrain, bStrich, bText, wb.GetValueOrDefault(r.Id) ?? []);
            zeilen.Add(z);
            if (z["phiT"] is JsonValue) { sT += rohT; sV += rohV; mech |= z["lueftungsanlage"]?.GetValue<bool>() == true; }
        }
        aus["raeume"] = zeilen;
        if (nur is null)
        {
            var ew = (hl["einzelwerte"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(x => T(x["id"]) ?? "", x => x);
            double f = mech ? D(ew.GetValueOrDefault("ND.5.7_faktorMechanisch")?["wert"]) : D(ew.GetValueOrDefault("ND.5.7_faktorNatuerlich")?["wert"]);
            if (double.IsNaN(f)) f = 1;
            aus["gebaeude"] = new JsonObject
            {
                ["gebaeude"] = geb.Kennzeichen, ["summePhiT"] = R(sT, 0), ["summePhiV"] = R(sV, 0), ["faktorLueftung"] = f,
                ["phiHL"] = R(sT + f * sV, 0),
                ["formel"] = $"ΦHL = Σ ΦT,i + f·Σ ΦV,i (22) = {R(sT, 0)} + {f}·{R(sV, 0)} W; f nach ND.5.7 ({(mech ? "mechanische Lüftung, höchstens 1,0" : "natürliche Lüftung, höchstens 0,8")})",
            };
        }
        return aus;
    }

    /// <summary>θe aus Gebaeude.thetaE (mit Quelle) bzw. SIA 2028 Tab. 6 der Klimastation, Korrekturen ND.1.1.1 und Tab. ND.1; θm,e aus Tab. 2; ρc (37).</summary>
    public static Aussen? AussenKlima(Element geb, JsonObject hl, JsonObject? klima, JsonArray hinweise)
    {
        var ann = new List<string>();
        double? hGeb = geb.Zahl("hoeheUeberMeer");
        double rhoC = hGeb is double h0 ? (1220 - 0.14 * h0) / 3600 : 0.34;
        if (hGeb is null) ann.Add("Höhe über Meer fehlt (Gebaeude.hoeheUeberMeer): ρ·c = 0,34 Wh/(m³K) nach (13), keine Höhenkorrektur von θe.");
        string? station = geb.Text("klimastation");
        JsonObject? t6 = station is null || klima is null ? null : Zeilen(klima["tabelle6"]).FirstOrDefault(x => string.Equals(T(x["station"]), station, StringComparison.OrdinalIgnoreCase));
        JsonObject? t2 = station is null || klima is null ? null : Zeilen(klima["tabelle2"]).FirstOrDefault(x => string.Equals(T(x["station"]), station, StringComparison.OrdinalIgnoreCase));
        double? thetaMe = t2 is null ? null : D(t2["jahr"]);
        if (t2 is not null) ann.Add($"θm,e = {thetaMe} °C aus SIA 2028:2010 Tab. 2 «Jahr» ({station}, S. {t2["seite"]}); ND.1.2 nennt SIA 381/2 – gleiche Quelle wie θe gewählt.");
        if (geb.Zahl("thetaE") is double te)
            return new Aussen(te, thetaMe, rhoC, $"θe = {te} °C ausdrücklich am Gebäude {geb.Kennzeichen} ({geb.Eigenschaft("thetaE")?.Herkunft.Quelle ?? "ohne Quelle"})", ann);
        if (klima is null) { hinweise.Add("Normtabelle SIA 2028 fehlt (heizung\\sia2028_auslegung.json) – kein θe."); return null; }
        if (station is null) { hinweise.Add($"Gebäude {geb.Kennzeichen}: Klimastation fehlt (Eigenschaft klimastation, Name nach SIA 2028 Tab. 6) – kein θe."); return null; }
        if (t6 is null) { hinweise.Add($"Klimastation «{station}» steht nicht in SIA 2028 Tab. 6 – kein θe."); return null; }
        double basis = D(t6["thetaE"]), hSt = D(t6["hoehe"]);
        var teile = new List<string> { $"θe,Station = {basis} °C (SIA 2028:2010 Tab. 6, {T(t6["station"])}, S. {t6["seite"]})" };
        double korrH = 0;
        if (hGeb is double hg && !double.IsNaN(hSt))
        {
            double dh = hg - hSt;
            double proM = Zeilen(new JsonObject { ["werte"] = hl["einzelwerte"]?.DeepClone() }).FirstOrDefault(x => T(x["id"]) == "ND.1.1.1_hoehe") is { } e ? D(e["wert"]) : double.NaN;
            if (double.IsNaN(proM)) hinweise.Add("ND.1.1.1 (Korrektur je Meter) fehlt in heizlast.json – keine Höhenkorrektur.");
            else if (dh > 0) { korrH = Math.Round(proM * dh, 0, MidpointRounding.AwayFromZero) + 0.0; teile.Add($"Höhe: Gebäude {hg} m über Station {hSt} m → {proM} K/m · {R(dh, 0)} m = {R(proM * dh)} K, gerundet {korrH} K (ND.1.1.1)"); }
            else teile.Add($"Höhe: Gebäude {hg} m nicht höher als Station {hSt} m → keine Korrektur (ND.1.1.1)");
        }
        double korrTau = 0;
        if (geb.Zahl("zeitkonstante") is double tau)
        {
            var z = Zeilen(hl["tabellen"]?["ND1"]).FirstOrDefault(x => tau > (D(x["tauVon_h"]) is var v && double.IsNaN(v) ? double.NegativeInfinity : v) - 1e-9 &&
                                                                       (x["tauBis_h"] is null || tau <= D(x["tauBis_h"]) + 1e-9));
            korrTau = z is null ? 0 : D(z["korrektur_K"]);
            teile.Add($"Zeitkonstante τ = {tau} h → {korrTau} K (Tab. ND.1)");
        }
        else ann.Add("Zeitkonstante τ fehlt (Gebaeude.zeitkonstante): τ > 200 h angenommen, keine Korrektur (Tab. ND.1) – bei leichter Bauweise zu prüfen.");
        double thetaE = basis + korrH + korrTau;
        teile.Add($"θe = {thetaE} °C");
        return new Aussen(thetaE, thetaMe, rhoC, string.Join("; ", teile), ann);
    }

    /// <summary>θint: Raum.tempWinter, sonst SIA 2024:2015 Tab. 9 «Winter» nach Raum.nutzung (Nummer oder Name, gross/klein egal).</summary>
    /// <summary>Zuordnung freier Nutzungen zu SIA-2024-Nummern aus kataloge/raumnutzungen.json (Muster, erste passende gewinnt).</summary>
    public static List<(System.Text.RegularExpressions.Regex Muster, string Nr)> Zuordnung(Katalog k)
    {
        var pfad = Path.Combine(k.Ordner, "raumnutzungen.json");
        if (!File.Exists(pfad)) return [];
        return (JsonNode.Parse(File.ReadAllText(pfad))?["zuordnung"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => T(x["muster"]) is { Length: > 0 } && T(x["sia2024"]) is { Length: > 0 })
            .Select(x => (new System.Text.RegularExpressions.Regex(T(x["muster"])!, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant), T(x["sia2024"])!)).ToList();
    }

    public static (double? Wert, string Quelle) Innentemperatur(Element r, JsonObject? nutz, List<(System.Text.RegularExpressions.Regex Muster, string Nr)>? zuordnung = null)
    {
        if (r.Zahl("tempWinter") is double t) return (t, $"Raum {r.Kennzeichen}.tempWinter ({r.Eigenschaft("tempWinter")?.Herkunft.Quelle ?? r.Eigenschaft("tempWinter")?.Herkunft.Art})");
        var n = r.Text("nutzung");
        if (n is { Length: > 0 } && nutz is not null &&
            Zeilen(nutz["tabelle"]).FirstOrDefault(x => string.Equals(T(x["nr"]), n.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(T(x["nutzung"]), n.Trim(), StringComparison.OrdinalIgnoreCase)) is { } z
            && z["raumtempWinter_C"] is JsonValue)
            return (D(z["raumtempWinter_C"]), $"SIA 2024:2015 Tab. 9, {T(z["nr"])} {T(z["nutzung"])}, Winter (S. {z["seite"]})");
        if (n is { Length: > 0 } && nutz is not null && zuordnung?.FirstOrDefault(x => x.Muster.IsMatch(n.Trim())) is { Nr: { } nr }
            && Zeilen(nutz["tabelle"]).FirstOrDefault(x => T(x["nr"]) == nr) is { } z2)
            return z2["raumtempWinter_C"] is JsonValue
                ? (D(z2["raumtempWinter_C"]), $"SIA 2024:2015 Tab. 9, {nr} {T(z2["nutzung"])}, Winter (S. {z2["seite"]}) – Nutzung «{n}» zugeordnet nach kataloge/raumnutzungen.json (Annahme Hauptlauf, von Christian zu bestätigen)")
                : (null, $"Nutzung «{n}» → SIA 2024 {nr} {T(z2["nutzung"])}: kein Winterwert in Tab. 9 (unbeheizt?) – tempWinter setzen");
        return (null, n is { Length: > 0 } ? $"Nutzung «{n}» nicht in SIA 2024 Tab. 9 und kein tempWinter" : "weder tempWinter noch Nutzung");
    }

    /// <summary>B' = AG / (0,5·P) (9) des Gebäudes: Bodenflächen gegen Erdreich und Umfang = Aussen- bzw. Erdreichwände dieser Räume.</summary>
    static double BStrich(RaumgrenzenErgebnis rg, JsonObject hl, out string text)
    {
        var raeume = rg.Grenzen.Where(g => g.Art == "boden" && g.Angrenzung == "erdreich").Select(g => g.Raum.Id).ToHashSet();
        double ag = rg.Grenzen.Where(g => g.Art == "boden" && g.Angrenzung == "erdreich").Sum(g => g.Flaeche);
        double pu = rg.Grenzen.Where(g => raeume.Contains(g.Raum.Id) && g.Art is "wand" or "fenster" or "tuer" && g.Angrenzung is "aussen" or "erdreich" && g.Von is not null)
            .GroupBy(g => (g.Raum.Id, g.Von![0], g.Von[1], g.Bis![0], g.Bis[1])).Sum(x => x.Max(g => g.Laenge)) / 1000;
        if (ag <= 0) { text = "keine Bodenfläche gegen Erdreich"; return double.NaN; }
        double b = pu > 0 ? ag / (0.5 * pu) : 20;
        text = pu > 0 ? $"B' = AG/(0,5·P) = {R(ag, 1)} m² / (0,5·{R(pu, 1)} m) = {R(b, 2)} m (9; Innenmasse)" : $"B' ohne Umfang gegen aussen: {R(b)} m (grösster Tabellenwert, Annahme)";
        return b;
    }

    /// <summary>Wärmebrücken mit ψ bzw. χ je Raum: mit Öffnung dem Raum der Öffnung, sonst nach Länge auf die Räume an derselben Wand verteilt.</summary>
    static Dictionary<string, List<(Element Wb, double Anteil)>> Waermebruecken(Modell m, RaumgrenzenErgebnis rg)
    {
        var aus = new Dictionary<string, List<(Element, double)>>(StringComparer.Ordinal);
        foreach (var wb in m.DerKlasse("Waermebruecke"))
        {
            List<(string Raum, double Laenge)> ziele;
            if (wb.Bezuege.TryGetValue("oeffnung", out var oe))
                ziele = rg.Grenzen.Where(g => g.Bauteil?.Id == oe && g.Angrenzung != "beheizt").Select(g => (g.Raum.Id, 1.0)).DistinctBy(x => x.Item1).ToList();
            else if (wb.Bezuege.TryGetValue("wand", out var wa))
                ziele = rg.Grenzen.Where(g => g.Bauteil?.Id == wa && g.Art == "wand" && g.Angrenzung != "beheizt").GroupBy(g => g.Raum.Id).Select(x => (x.Key, x.Sum(g => g.Laenge))).ToList();
            else continue;
            double summe = ziele.Sum(x => x.Item2);
            foreach (var (raum, l) in ziele)
            {
                if (!aus.TryGetValue(raum, out var liste)) aus[raum] = liste = [];
                liste.Add((wb, summe > 0 ? l / summe : 1.0 / ziele.Count));
            }
        }
        return aus;
    }

    /// <summary>Lineare Interpolation in einer Stützstellenreihe (x aufsteigend); ausserhalb der Rand (geklemmt).</summary>
    static double Linear(IReadOnlyList<(double X, double Y)> p, double x)
    {
        if (p.Count == 0) return double.NaN;
        if (x <= p[0].X) return p[0].Y;
        if (x >= p[^1].X) return p[^1].Y;
        for (int i = 1; i < p.Count; i++)
            if (x <= p[i].X) return p[i - 1].Y + (p[i].Y - p[i - 1].Y) * (x - p[i - 1].X) / (p[i].X - p[i - 1].X);
        return p[^1].Y;
    }

    /// <summary>Uequiv,bf eines Bodens aus einer der Tab. 4–6: in B' und U linear (Spalten 2,0/1,0/0,5/0,25; U über 2,0 → «keine Dämmung», unter 0,25 → 0,25, Annahme).</summary>
    static double UequivBoden(JsonObject tab, double bStrich, double u, List<string> ann)
    {
        var w = Zeilen(tab).ToList();
        double Spalte(string s) => Linear(w.Select(z => (D(z["bPrime_m"]), D(z[s]))).ToList(), bStrich);
        var punkte = new List<(double X, double Y)> { (0.25, Spalte("uBoden_0_25")), (0.5, Spalte("uBoden_0_5")), (1.0, Spalte("uBoden_1_0")), (2.0, Spalte("uBoden_2_0")) };
        if (u > 2.0 + 1e-9) { ann.Add($"U Boden {R(u)} > 2,0 W/(m²K): Spalte «keine Dämmung» (Annahme)."); return Spalte("keineDaemmung"); }
        if (u < 0.25 - 1e-9) ann.Add($"U Boden {R(u)} < 0,25 W/(m²K): mit der Spalte 0,25 gerechnet (sichere Seite, Annahme).");
        return Linear(punkte, u);
    }

    static (JsonObject Zeile, double PhiT, double PhiV) Raum(Element r, RaumMasse rm, List<Raumgrenze> grenzen, Dictionary<string, (double? Wert, string Quelle)> theta, Aussen a,
        JsonObject hl, Element geb, double terrain, double bStrich, string bText, List<(Element Wb, double Anteil)> wbs)
    {
        var ann = new JsonArray();
        var (ti, tiQ) = theta[r.Id];
        var aus = new JsonObject { ["raum"] = r.Kennzeichen, ["nummer"] = r.Text("nummer"), ["name"] = r.Name, ["thetaInt"] = ti, ["thetaIntQuelle"] = tiQ, ["thetaE"] = a.ThetaE };
        if (ti is not double tint) { ann.Add($"Innentemperatur fehlt ({tiQ}) – keine Heizlast."); aus["annahmen"] = ann; return (aus, 0, 0); }
        double dt = tint - a.ThetaE;
        double vol = rm.FlaecheM2 * rm.HoeheMm / 1000;
        aus["flaeche_m2"] = R(rm.FlaecheM2); aus["volumen_m3"] = R(vol);
        var beitraege = new JsonArray();
        double hie = 0, hiue = 0, hig = 0, hij = 0; int ohneU = 0;
        var tabellen = hl["tabellen"]; var ueq = hl["uequiv"];
        double zBoden = Math.Max(0, (terrain - rm.UntenMm) / 1000);
        foreach (var g in grenzen)
        {
            if (g.U is not double u) { if (g.Angrenzung != "beheizt") ohneU++; continue; }
            double faktor = 1; string text;
            switch (g.Angrenzung)
            {
                case "aussen":
                    faktor = 1; text = "ek = 1,0 (ND.4.1)"; hie += g.Flaeche * u; break;
                case "unbeheizt":
                {
                    var nb = g.NachbarRaum;
                    if (nb?.Zahl("bu") is double bu) { faktor = bu; text = $"bu = {bu} ({nb.Kennzeichen}.bu, {nb.Eigenschaft("bu")?.Herkunft.Quelle ?? "ohne Quelle"})"; }
                    else if (nb is not null && theta.GetValueOrDefault(nb.Id).Wert is double tu) { faktor = Math.Clamp((tint - tu) / dt, 0, 1); text = $"bu = (θint − θu)/(θint − θe) = ({tint} − {tu})/{R(dt, 1)} = {R(faktor)} (6)"; }
                    else { faktor = 1; text = "bu = 1 (weder bu noch Temperatur des Nachbarraums – sichere Seite, Annahme)"; }
                    hiue += g.Flaeche * u * faktor; break;
                }
                case "erdreich":
                {
                    if (g.Art == "boden")
                    {
                        var tab = zBoden < 0.75 ? ueq?["tabelle4_z0m"] : zBoden < 2.25 ? ueq?["tabelle5_z1_5m"] : ueq?["tabelle6_z3_0m"];
                        var tabAnn = new List<string>();
                        double ue = tab is JsonObject to && !double.IsNaN(bStrich) ? UequivBoden(to, bStrich, u, tabAnn) : double.NaN;
                        foreach (var x in tabAnn) ann.Add(x);
                        text = $"Uequiv,bf = {R(ue, 3)} ({T(tab?["tabelle"])?.Split(' ')[0]} {T(tab?["tabelle"])?.Split(' ')[1]}, z ≈ {R(zBoden, 1)} m → Tabelle z = {tab?["tiefeZ_m"]} m, {bText})";
                        u = ue;
                    }
                    else
                    {
                        var w7 = Zeilen(ueq?["tabelle7_Uequiv_bw"]).ToList();
                        double Sp(string s) => Linear(w7.Select(z => (D(z["uWand_W_m2K"]), D(z[s]))).ToList(), u);
                        var zp = new List<(double, double)> { (0, Sp("z0m")), (1, Sp("z1m")), (2, Sp("z2m")), (3, Sp("z3m")) };
                        double ue = Linear(zp, zBoden);
                        text = $"Uequiv,bw = {R(ue, 3)} (Tab. 7, U = {R(u)}, z = {R(zBoden, 1)} m)";
                        u = ue;
                    }
                    if (double.IsNaN(u)) { ann.Add("Uequiv nicht bestimmbar (Tabellen bzw. B' fehlen) – Erdreichfläche nicht gerechnet."); continue; }
                    hig += g.Flaeche * u; break;
                }
                default:   // beheizt
                {
                    if (g.NachbarRaum is not { } nb || theta.GetValueOrDefault(nb.Id).Wert is not double tj || Math.Abs(tj - tint) < 1e-9) continue;
                    faktor = Math.Max(0, (tint - tj) / dt);
                    text = $"fij = (θint,i − θj)/(θint,i − θe) = ({tint} − {tj})/{R(dt, 1)} = {R((tint - tj) / dt, 3)}{(tint < tj ? " → 0 (Wärmegewinn nicht angerechnet, Annahme)" : "")} (10)";
                    hij += g.Flaeche * u * faktor; break;
                }
            }
            beitraege.Add(new JsonObject
            {
                ["art"] = g.Art, ["angrenzung"] = g.Angrenzung, ["nachbar"] = g.NachbarRaum?.Kennzeichen ?? g.Nachbar, ["bauteil"] = g.Bauteil?.Kennzeichen,
                ["flaeche_m2"] = R(g.Flaeche, 3), ["u"] = R(u, 3), ["faktor"] = R(faktor, 3), ["h_WK"] = R(g.Flaeche * u * faktor, 3), ["text"] = text,
            });
        }
        // Erdreich: fg1 (Tab. ND.5), fg2, GW
        if (hig > 0)
        {
            var z = Zeilen(tabellen?["ND5"]).FirstOrDefault(x => zBoden <= (x["zBis_m"] is null ? double.PositiveInfinity : D(x["zBis_m"])) + 1e-9);
            double fg1 = z is null ? double.NaN : D(z["fg1"]);
            double? fg2 = a.ThetaMe is double tme ? (tint - tme) / dt : null;
            if (fg2 is null) ann.Add("θm,e fehlt (SIA 2028 Tab. 2) – fg2 = 1 (sichere Seite, Annahme).");
            if (geb.Text("grundwasserNah") == "true" || geb.Eigenschaft("grundwasserNah")?.Inhalt is JsonValue gv && gv.TryGetValue<bool>(out var gb) && gb)
                ann.Add("Grundwasser näher als 1 m: GW nach EN 13370 Anhang H nicht gerechnet – GW = 1,0 eingesetzt (ND.4.3, Hinweis).");
            double roh = hig;
            hig = fg1 * (fg2 ?? 1) * roh;
            aus["erdreich"] = new JsonObject { ["summeAUequiv_WK"] = R(roh, 3), ["fg1"] = fg1, ["fg2"] = fg2 is double f2 ? R(f2, 3) : null, ["gw"] = 1.0, ["z_m"] = R(zBoden, 2),
                ["formel"] = $"HT,ig = fg1·fg2·Σ A·Uequiv·GW = {fg1}·{R(fg2 ?? 1, 3)}·{R(roh, 3)}·1,0 = {R(hig, 3)} W/K (8)" };
        }
        // Wärmebrücken mit ψ bzw. χ
        double hwb = 0; int wbOhne = 0;
        foreach (var (wb, anteil) in wbs)
        {
            double? psi = wb.Zahl("psi"), chi = wb.Zahl("chi"), l = wb.Zahl("laenge");
            if (psi is double ps && l is double ll) { double h = ps * ll * anteil; hwb += h; beitraege.Add(new JsonObject { ["art"] = "waermebruecke", ["bauteil"] = wb.Kennzeichen, ["psi"] = ps, ["laenge_m"] = R(ll * anteil, 3), ["h_WK"] = R(h, 3), ["text"] = $"ψ·l·el = {ps}·{R(ll * anteil, 2)}·1,0 ({wb.Eigenschaft("psi")?.Herkunft.Quelle ?? "ohne Quelle"})" }); }
            else if (chi is double c) { double h = c * anteil; hwb += h; beitraege.Add(new JsonObject { ["art"] = "waermebruecke", ["bauteil"] = wb.Kennzeichen, ["chi"] = c, ["h_WK"] = R(h, 3), ["text"] = "χ (punktförmig)" }); }
            else wbOhne++;
        }
        hie += hwb;
        if (wbOhne > 0) ann.Add($"{wbOhne} Wärmebrücke(n) ohne ψ bzw. χ – nicht gerechnet (ND.4.1: effektive ψ-Werte einsetzen).");
        if (wbs.Count == 0) ann.Add("Keine Wärmebrücken am Raum erfasst (waermebruecken_vorschlagen, ψ mit Quelle) – Wärmebrückenverluste fehlen.");
        if (ohneU > 0) ann.Add($"{ohneU} Raumgrenze(n) ohne U-Wert – nicht gerechnet (Aufbau bzw. Fenster-Uw fehlt).");
        double phiT = (hie + hiue + hig + hij) * dt;
        // Lüftung
        var ew = (hl["einzelwerte"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(x => T(x["id"]) ?? "", x => D(x["wert"]));
        double zu = r.Zahl("zuluft") ?? 0, ab = r.Zahl("abluft") ?? 0;
        bool mech = zu > 0 || ab > 0;
        double vMin = 0, vInf, vSu = 0, v;
        string vText;
        int ausr = grenzen.Where(g => g.Angrenzung == "aussen" && g.Art is "wand" or "fenster" or "tuer" && g.Azimut is double).Select(g => (int)Math.Round(g.Azimut!.Value / 45.0) % 8).Distinct().Count();
        if (mech)
        {
            vInf = ew.GetValueOrDefault("ND.5.1_nInfMech", double.NaN) * vol; vSu = zu; v = vInf;
            vText = $"mit Lüftungsanlage: V̇ = V̇inf + V̇su·fV = {R(vInf, 1)} + {R(zu, 1)}·0 = {R(v, 1)} m³/h (15; V̇inf = 0,1·V nach ND.5.1)";
            ann.Add("Lüftungsanlage: Zulufttemperatur unbekannt – fV = 0 (Zuluft mit Raumtemperatur, Annahme); Abluftüberschuss (18) nicht je Raum.");
        }
        else
        {
            vMin = ew.GetValueOrDefault("ND.5.2_nmin", double.NaN) * vol;
            double n50 = geb.Zahl("n50") ?? 0;
            if (geb.Zahl("n50") is null) ann.Add("n50 fehlt (Gebaeude.n50): 0 angenommen – Hülle luftdicht nach SIA 180 (ND.5.3); bei Altbau Tab. ND.7.");
            var abs = geb.Text("abschirmung") ?? "moderat";
            if (geb.Text("abschirmung") is null) ann.Add("Abschirmungsklasse fehlt (Gebaeude.abschirmung): «moderat» angenommen (Tab. ND.8).");
            var z8 = Zeilen(tabellen?["ND8"]).FirstOrDefault(x => (T(x["klasse"]) ?? "").StartsWith(abs switch { "keine" => "Keine", "gut" => "Gute", _ => "Moderate" }, StringComparison.OrdinalIgnoreCase));
            double e = z8 is null ? double.NaN : D(z8[ausr == 0 ? "e_ohneAussenwand" : ausr == 1 ? "e_eineAusrichtung" : "e_mehrereAusrichtungen"]);
            double hBoden = Math.Max(0, (rm.UntenMm + rm.HoeheMm / 2 - terrain) / 1000);
            var z9 = Zeilen(tabellen?["ND9"]).FirstOrDefault(x => hBoden <= (x["hoeheBis_m"] is null ? double.PositiveInfinity : D(x["hoeheBis_m"])) + 1e-9);
            double eps = z9 is null ? double.NaN : D(z9["epsilon"]);
            vInf = 2 * vol * n50 * e * eps;
            v = Math.Max(vInf, vMin);
            vText = $"ohne Lüftungsanlage: V̇ = max(V̇inf, V̇min) = max({R(vInf, 1)}, {R(vMin, 1)}) = {R(v, 1)} m³/h (14); V̇min = 0,3·V (ND.5.2), V̇inf = 2·V·n50·e·ε = 2·{R(vol, 1)}·{n50}·{e}·{eps} (17; e Tab. ND.8 «{abs}», {ausr} Ausrichtung(en); ε Tab. ND.9, Raummitte {R(hBoden, 1)} m über Terrain)";
        }
        double hv = v * a.RhoC, phiV = hv * dt;
        aus["lueftungsanlage"] = mech;
        aus["hTie_WK"] = R(hie, 3); aus["hTiue_WK"] = R(hiue, 3); aus["hTig_WK"] = R(hig, 3); aus["hTij_WK"] = R(hij, 3); aus["hWaermebruecken_WK"] = R(hwb, 3);
        aus["phiT"] = R(phiT, 0);
        aus["phiTFormel"] = $"ΦT = (HT,ie + HT,iue + HT,ig + HT,ij)·(θint − θe) = ({R(hie, 2)} + {R(hiue, 2)} + {R(hig, 2)} + {R(hij, 2)})·({tint} − {a.ThetaE}) = {R(phiT, 0)} W (2)";
        aus["vPunkt_m3h"] = R(v, 2); aus["vMin_m3h"] = R(vMin, 2); aus["vInf_m3h"] = R(vInf, 2); aus["vSu_m3h"] = R(vSu, 2); aus["lueftungText"] = vText;
        aus["hV_WK"] = R(hv, 3); aus["phiV"] = R(phiV, 0);
        aus["phiVFormel"] = $"ΦV = V̇·ρc·(θint − θe) = {R(v, 1)}·{R(a.RhoC, 4)}·{R(dt, 1)} = {R(phiV, 0)} W (11, 12; ρc nach {(geb.Zahl("hoeheUeberMeer") is null ? "(13)" : "(37)")})";
        aus["phiRH"] = 0;
        aus["phiHL"] = R(phiT + phiV, 0);
        aus["phiHL_Wm2"] = rm.FlaecheM2 > 0 ? R((phiT + phiV) / rm.FlaecheM2, 1) : null;
        aus["beitraege"] = beitraege;
        ann.Add("Masse: Innenmasse der Raumgrenzen (ND.3 verlangt Aussenmasse für den Wärmedämmperimeter) – Heizlast etwas zu klein (Annahme V1).");
        ann.Add("ΦRH = 0 (ND.6).");
        aus["annahmen"] = ann;
        return (aus, phiT, phiV);
    }
}
