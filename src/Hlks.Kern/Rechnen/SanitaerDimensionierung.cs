// Modul 4 Teil B – Sanitär dimensionieren je Leitung (Christian 03.11.2026: «Sanitär KI modul weiter bearbeiten … du hast da gestern neue
// daten von mir bekommen»). Grundlage: Summen aus Teil A (SanitaerBelastung) und die Normtabellen im Büroordner (SanitaerNormtabellen;
// SN 592000:2024 bzw. SVGW W3 aus dem Handbuch Gebäudetechnik Sanitär 2025). Rechenweg im Klartext: kataloge/rechenregister.json
// (sanitaer.abwasser_dimensionierung).
// Abwasser je Leitung, Leitungsart aus dem Strang (art anschlussleitung, fallstrang, sammelleitung, grundleitung; im Fallstrang nur die
// steilen Leitungen = Fallleitung, die waagrechten = Anschlussleitung; ohne Strang geschätzt):
//   Anschlussleitung: Tabelle 9 (unbelüftet, ≤ 4 m) – erste Zeile mit ΣDU ≤ höchstzulässige DU und grösster Einzel-DU ≤ Grenze, Fussnoten
//     1 (höchstens ein Gegenstand à 1.5 DU) und 2 (höchstens zwei WC) geprüft.
//   Fallleitung: Tabelle 11 (Hauptlüftung, Winkel-Abzweig) – ΣDU ≤ höchstzulässige DU (gilt für K = 0.5), sonst Q_ww ≤ Q_max; Einzel-DU.
//   Sammel- und Grundleitung: Q = max(K · √ΣDU, grösster Einzel-DU); Tabelle 22 in der Spalte des Gefälles der Leitung (die nächstkleinere
//     Spalte, nie interpoliert) die kleinste DN mit Q_max ≥ Q; Grundleitung nur Zeilen «für Grundleitung»; Mindest-DN Sammelleitung 80, mit WC 100.
//   Vergleich: gezeichneter Innendurchmesser (Serie) ≥ Mindest-Innendurchmesser der Soll-DN (Tabelle 5), sonst DN der Leitung.
// Trinkwasser je Leitung (KW, WW; nicht Zirkulation), SVGW W3 (Handbuch S. 45–70):
//   ΣLU der Spalte (KW bzw. WW) stromab, grösster Einzel-LU; QT = ΣLU · 0.1 l/s; Spitzendurchfluss QD nach Diagramm 1 (grösster LU > 3 →
//     QD = 0.598 · QT^0.257, sonst 0.459 · QT^0.353; unter dem Startpunkt der Kurve QD = QT).
//   Leitungsart A (ein Apparat stromab, Ausstossleitung), sonst V (Strang verteilleitung/steigstrang) bzw. T (T-Stückleitung); abgewickelte Länge = Leitung + längster Weg stromab bis zur Entnahmestelle.
//   Belastungswerttabelle des Systems (Serie der Leitung ↔ Hersteller-System), Leitungsart, Spalte = kleinste Länge ≥ abgewickelte Länge
//     «ohne Wasserzähler», Zeile = kleinste LU-Stufe ≥ ΣLU → Rohr da × s; gezeichneter Aussendurchmesser ≥ da der Tabelle.
//   ANNAHMEN (Fragen an Christian): Stufen aufgerundet; ohne Gruppenwasserzähler; Leitungsart aus Strang bzw. Anzahl Apparate (die
//     Gruppenabsperrung als Schnittstelle kennt das Modell nicht); über dem Tabellenende kein Rohr (Berechnungsmethode, Hinweis).
// ANNAHMEN (Prüfliste, Fragen an Christian): Anschlussleitungen ≤ 4 m (Tabelle 10 belüftet noch nicht); Fallleitung Hauptlüftung mit
//   Winkel-Abzweig (Tabelle 11); Q_c, Q_p, Q_R je Teilstrecke nicht (nur Anlage, Teil A); Grundleitung Mindest-DN bei Fallhöhe ≤ 10 m im
//   Handbuch widersprüchlich (100/150) – nicht angewendet; K ≠ 0.5 bei Fallleitungen über Q_max.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SanitaerDimensionierung
{
    public const string Text = "Sanitär dimensionieren je Leitung (Modul 4 Teil B): Abwasser nach SN 592000:2024 – Leitungsart aus dem Strang, Tabelle 9 " +
        "(Anschlussleitung), 11 (Fallleitung) bzw. 22 (Sammel-/Grundleitung mit Q = max(K·√ΣDU, grösster Einzel-DU) und Gefälle), Soll-DN, gezeichneter " +
        "Innendurchmesser gegen den Mindest-Innendurchmesser (Tabelle 5); Trinkwasser nach SVGW W3 – ΣLU, Spitzendurchfluss (Diagramm 1), Leitungsart V/A/T, " +
        "abgewickelte Länge, Rohr da × s aus der Belastungswerttabelle des Systems der Serie, gezeichneter Aussendurchmesser dagegen; je Ergebnis Tabelle, Zeile, Beleg und Annahmen. Die Normtabellen liegen im " +
        "Büroordner (HLKS_NORMTABELLEN); fehlen sie, sagt die Antwort es. Rechenweg: Abfrage rechenweg (sanitaer.abwasser_dimensionierung, sanitaer.trinkwasser_dimensionierung).";
    public const string ParAnlage = "Anlage (Kennzeichen oder Id); leer = alle";
    public const string ParLeitung = "Nur diese Leitung (Kennzeichen oder Id)";
    public static IReadOnlyList<ParameterDef> Parameter { get; } = [new("anlage", "text", false, ParAnlage), new("leitung", "text", false, ParLeitung)];

    public static JsonObject Abfrage(Projekt p, JsonObject? par)
    {
        par ??= [];
        var m = p.Modell; var k = p.Katalog;
        var netz = SanitaerBelastung.Ergebnis(m, k);
        var abw = SanitaerNormtabellen.Abwasser(k);
        var hinweise = new JsonArray();
        var tw = SanitaerNormtabellen.Trinkwasser(k);
        if (tw is null) hinweise.Add($"Normtabellen Trinkwasser fehlen ({Path.Combine(SanitaerNormtabellen.Ordner(k), "sanitaer", "trinkwasser.json")}) – keine Trinkwasser-Dimensionierung.");
        if (abw is null) hinweise.Add($"Normtabellen Abwasser fehlen ({Path.Combine(SanitaerNormtabellen.Ordner(k), "sanitaer", "abwasser.json")}) – keine Abwasser-Dimensionierung.");
        Element? anlage = Param.Text(par, "anlage") is { Length: > 0 } a ? m.Finde(a) ?? throw new HlksFehler($"Anlage «{a}» gibt es nicht.") : null;
        Element? nur = Param.Text(par, "leitung") is { Length: > 0 } l ? m.Finde(l) ?? throw new HlksFehler($"Leitung «{l}» gibt es nicht.") : null;
        var zeilen = new JsonArray();
        foreach (var (id, medium) in netz.LeitungMedium.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (nur is not null && nur.Id != id) continue;
            if (m.Finde(id) is not { } e) continue;
            var anl = AnlageVon(m, e);
            if (anlage is not null && anl?.Id != anlage.Id) continue;
            if (SanitaerNetz.IstAbwasser(medium) && abw is not null && netz.Leitung(id) is { } s) zeilen.Add(Abwasser(p, e, medium, s, anl, abw));
            else if (SanitaerNetz.IstTrinkwasser(medium) && tw is not null && netz.Leitung(id) is { } t) zeilen.Add(Trinkwasser(p, e, medium, t, tw));
        }
        return new JsonObject { ["normtabellen"] = SanitaerNormtabellen.Ordner(k), ["leitungen"] = zeilen, ["hinweise"] = hinweise };
    }

    static Element? AnlageVon(Modell m, Element e) => e.System is { } s && m.Finde(s) is { } sys && sys.TeilVon is { } a ? m.Finde(a) : null;

    /// <summary>Leitungsart aus dem Strang (art); ohne Strang geschätzt: steil (|Δz| ≥ waagrechte Länge) = Fallleitung, sonst Sammelleitung.</summary>
    public static (string Art, bool Geschaetzt) Leitungsart(Modell m, Element e)
    {
        if (e.TeilVon is { } t && m.Finde(t) is { } st && st.Text("art") is { } art)
            switch (art)
            {
                case "fallstrang": return (Steil(e) ? "fall" : "anschluss", false);   // waagrechte Leitungen im Fallstrang sind Anschlussleitungen
                case "sammelleitung": return ("sammel", false);
                case "grundleitung": return ("grund", false);
                case "anschlussleitung": return ("anschluss", false);
            }
        return (Steil(e) ? "fall" : "sammel", true);
    }

    /// <summary>Steil: der senkrechte Anteil ist mindestens so lang wie der waagrechte.</summary>
    static bool Steil(Element e)
    {
        var p = HlksEinordnung.Punkte(e);
        double dz = 0, xy = 0;
        for (int i = 1; i < p.Count; i++) { dz += Math.Abs(p[i][2] - p[i - 1][2]); xy += Math.Sqrt(Math.Pow(p[i][0] - p[i - 1][0], 2) + Math.Pow(p[i][1] - p[i - 1][1], 2)); }
        return dz >= xy && dz > 0;
    }

    static double D(JsonNode? n) => n?.GetValue<double>() ?? double.NaN;

    /// <summary>Serie der Leitung ↔ System der Belastungswerttabelle (Stichwort im Seriennamen → Stichwort im Systemnamen der Tabelle).</summary>
    static readonly (string Serie, string System)[] Systeme =
    [
        ("Sanipex MT", "Sanipex MT"), ("Sanipex Classic", "Sanipex Classic"), ("iFIT", "iFIT"), ("Instaflex", "Instaflex"),
        ("Mapress Edelstahl", "Geberit Mapress"), ("Optipress", "Optipress"), ("Optiflex", "Optiflex"), ("FlowFit", "FlowFit"), ("PushFit", "PushFit"),
    ];

    /// <summary>Spitzendurchfluss nach Diagramm 1: Gleichung nach dem grössten Einzel-LU (> 3 → zweite), unter dem Startpunkt QD = QT.</summary>
    public static (double Qd, string Kurve, string? Hinweis) Spitze(JsonObject tw, double qt, double groessterLu)
    {
        var gl = (tw["diagramm1"]?["gleichungen"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (gl.Count < 2) throw new HlksFehler("Diagramm 1 fehlt in den Normtabellen Trinkwasser.");
        var g = groessterLu > 3 ? gl[1] : gl[0];
        double a = D(g["a"]), b = D(g["b"]);
        var bereich = g["geltungsbereichQT_ls"] as JsonArray;
        double von = D(bereich?[0]), bis = D(bereich?[1]);
        string kurve = $"QD = {a} · QT^{b}";
        if (qt < von) return (qt, "QD = QT", $"QT {Math.Round(qt, 3)} l/s unter dem Startpunkt {von} l/s der Kurve: QD = QT (Diagramm 1, aus dem Bild).");
        return (a * Math.Pow(qt, b), kurve, qt > bis ? $"QT {Math.Round(qt, 2)} l/s über dem Geltungsbereich {bis} l/s von «{kurve}»." : null);
    }

    static JsonObject Trinkwasser(Projekt p, Element e, string medium, SanitaerSumme s, JsonObject tw)
    {
        var m = p.Modell; var k = p.Katalog;
        var spalte = SanitaerKatalog.SpalteFuer(medium);
        double lu = spalte == "luWw" ? s.SummeLuWw : s.SummeLuKw;
        double luMax = s.Beitraege.Where(b => b.Wert is double && SanitaerKatalog.SpalteFuer(b.Medium) == spalte).Select(b => b.Wert!.Value).DefaultIfEmpty(0).Max();
        double qt = lu * D(tw["diagramm1"]?["einheitLU_ls"]);
        var annahmen = new JsonArray();
        var aus = new JsonObject { ["leitung"] = e.Kennzeichen, ["medium"] = medium, ["summeLu"] = lu, ["groessterLu"] = luMax, ["apparate"] = s.Apparate, ["qt"] = Math.Round(qt, 3) };
        if (s.OhneWert.Count > 0) annahmen.Add($"{s.OhneWert.Count} Apparat(e) ohne LU – Summe zu klein (Teil A, Apparateart setzen).");
        if (lu <= 0) { aus["annahmen"] = annahmen; aus["ok"] = null; return aus; }
        var (qd, kurve, h) = Spitze(tw, qt, luMax);
        aus["qd"] = Math.Round(qd, 3); aus["kurve"] = kurve;
        if (h is not null) annahmen.Add(h);
        // Leitungsart und abgewickelte Länge
        string art = s.Apparate <= 1 ? "A" : e.TeilVon is { } t && m.Finde(t)?.Text("art") is "verteilleitung" or "steigstrang" ? "V" : "T";
        double laenge = (e.Geometrie?.Laenge() ?? 0) + Netz.Folgen(m, k, e, "stromab", medium).Where(x => Netz.IstEndgeraet(k, x.Element)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max();
        laenge /= 1000;
        aus["art"] = art; aus["laenge_m"] = Math.Round(laenge, 2);
        annahmen.Add(art == "A" ? "Leitungsart A: ein Apparat stromab (Ausstossleitung)." : art == "V" ? "Leitungsart V: mehrere Apparate, Strang verteilleitung/steigstrang." : "Leitungsart T: mehrere Apparate ausserhalb Verteil-/Steigstrang (T-Stückleitung); die Gruppenabsperrung als Schnittstelle ist im Modell nicht erfasst.");
        // Tabelle des Systems
        var serie = HlksMasse.Von(p, e)?.Serie?.SerieName ?? "";
        var sys = Systeme.FirstOrDefault(x => serie.Contains(x.Serie, StringComparison.OrdinalIgnoreCase));
        var tab = sys.System is null ? null : (tw["belastungswerttabellen"]?["tabellen"] as JsonArray ?? []).OfType<JsonObject>()
            .FirstOrDefault(x => (x["system"]?.GetValue<string>() ?? "").Contains(sys.System, StringComparison.OrdinalIgnoreCase) && x["leitungsart"]?.GetValue<string>() == art);
        aus["serie"] = serie; aus["tabelle"] = tab is null ? null : $"Belastungswerttabelle {tab["tabelle"]}, S. {tab["seite"]}: {tab["system"]}";
        if (tab is null) { annahmen.Add($"Keine Belastungswerttabelle für die Serie «{serie}» und Leitungsart {art}."); aus["annahmen"] = annahmen; aus["ok"] = null; return aus; }
        var laengen = (tab["spaltenKopf"]?["abgewickelteLaenge_m"] as JsonArray ?? []).Select(D).ToList();
        var zaehler = (tab["spaltenKopf"]?["wasserzaehler"] as JsonArray ?? []).Select(x => x?.GetValue<string>()).ToList();
        int sp = Enumerable.Range(0, laengen.Count).Where(i => laengen[i] + 1e-9 >= laenge && (i >= zaehler.Count || zaehler[i] is null or "ohne")).DefaultIfEmpty(-1).First();
        var zeile = (tab["zeilen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(z => D(z["lu"]) + 1e-9 >= lu);
        annahmen.Add("LU- und Längenstufen aufgerundet, Spalte «ohne Wasserzähler».");
        string? rohr = sp >= 0 && zeile?["werte"] is JsonArray w && sp < w.Count ? w[sp]?.GetValue<string>() : null;
        if (sp < 0) annahmen.Add($"Abgewickelte Länge {Math.Round(laenge, 1)} m über der Tabelle – Berechnungsmethode nötig.");
        if (zeile is null) annahmen.Add($"ΣLU {lu} über der Tabelle – Berechnungsmethode nötig.");
        aus["rohrSoll"] = rohr is { } r && r.Contains('x') ? r : null;
        if (rohr is not null && !rohr.Contains('x')) annahmen.Add($"Tabellenfeld «{rohr}» – kein Rohr für diese Stufe.");
        aus["beleg"] = zeile is null ? null : SanitaerNormtabellen.Beleg(new JsonObject { ["tabelle"] = $"Belastungswerttabelle {tab["tabelle"]}", ["seite"] = tab["seite"]?.DeepClone() }, zeile);
        double? daSoll = aus["rohrSoll"] is JsonValue rv && double.TryParse(rv.GetValue<string>().Split('x')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d0) ? d0 : null;
        double? daIst = e.Zahl("da") ?? HlksMasse.Von(p, e)?.Serie?.Werte.Aussen;
        aus["daSoll"] = daSoll; aus["daIst"] = daIst;
        aus["ok"] = daSoll is double ds && daIst is double di ? di + 1e-6 >= ds : null;
        aus["annahmen"] = annahmen;
        return aus;
    }

    static JsonObject Abwasser(Projekt p, Element e, string medium, SanitaerSumme s, Element? anlage, JsonObject abw)
    {
        var m = p.Modell;
        var (art, geschaetzt) = Leitungsart(m, e);
        bool einzel = s.Apparate == 1 && art != "anschluss";
        if (einzel) art = "anschluss";   // Einzelanschlussleitung: ein Apparat, auch wenn sie am Fall-, Sammel- oder Grundleitungsstrang hängt
        var beitraege = s.Beitraege.Where(b => b.Wert is double).ToList();
        double sDu = s.SummeDu, duMax = beitraege.Count == 0 ? 0 : beitraege.Max(b => b.Wert!.Value);
        int wc = beitraege.Where(b => b.Zuordnung.Schluessel?.StartsWith("wc", StringComparison.OrdinalIgnoreCase) == true).Select(b => b.Apparat.Id).Distinct().Count();
        int anz15 = beitraege.Where(b => b.Wert >= 1.5 - 1e-9).Select(b => b.Apparat.Id).Distinct().Count();
        var (kWert, kHerkunft) = anlage is null ? (null, null) : SanitaerZusammenstellung.Abflusskennzahl(m, anlage);
        var annahmen = new JsonArray();
        var aus = new JsonObject
        {
            ["leitung"] = e.Kennzeichen, ["medium"] = medium, ["art"] = art, ["artGeschaetzt"] = geschaetzt,
            ["summeDu"] = Math.Round(sDu, 3), ["groessterDu"] = duMax, ["wc"] = wc, ["apparate"] = s.Apparate,
        };
        if (einzel) annahmen.Add("Ein Apparat stromauf: als Einzelanschlussleitung gerechnet (Tabelle 9), unabhängig vom Strang.");
        if (geschaetzt) annahmen.Add("Leitungsart ohne Strang aus der Lage geschätzt (steil = Fallleitung, sonst Sammelleitung).");
        if (s.OhneWert.Count > 0) annahmen.Add($"{s.OhneWert.Count} Apparat(e) ohne DU – Summe zu klein (Teil A, Apparateart setzen).");
        int? dnSoll = null; string? beleg = null;
        switch (art)
        {
            case "anschluss":
            {
                var t = abw["anschlussleitungen"]?["tabelle9_unbeluefteteAnschlussleitungen"];
                foreach (var z in (t?["werte"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (sDu > D(z["hoechstDU"]) + 1e-9 || duMax > D(z["groesstEinzelDU"]) + 1e-9) continue;
                    var fn = (z["fussnoten"] as JsonArray ?? []).Select(x => x?.ToString()).ToList();
                    if (fn.Contains("1") && duMax >= 1.5 - 1e-9 && anz15 > 1) continue;
                    if (fn.Contains("2") && wc > 2) continue;
                    dnSoll = (int)D(z["DN"]); beleg = SanitaerNormtabellen.Beleg(t, z);
                    if (fn.Contains("3")) annahmen.Add("Fussnote 3: DN 90 für WC nur über Roh-Boden, höchstens 90° Richtungsänderung, ohne Anschlussbogen – von Hand prüfen.");
                    break;
                }
                annahmen.Add("Anschlussleitung höchstens 4 m abgewickelt angenommen (Tabelle 9 unbelüftet); länger → Tabelle 10, noch nicht gerechnet.");
                break;
            }
            case "fall":
            {
                var t = abw["fallleitungen"]?["tabelle11_hauptlueftung_winkelAbzweig"];
                double qww = (kWert ?? 0.5) * Math.Sqrt(sDu);
                foreach (var z in (t?["werte"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    bool passt = kWert is null or 0.5 ? sDu <= D(z["hoechstDU_K05"]) + 1e-9 : qww <= D(z["Qmax_ls"]) + 1e-9;
                    if (!passt || duMax > D(z["groesstEinzelDU"]) + 1e-9) continue;
                    dnSoll = (int)D(z["DN"]); beleg = SanitaerNormtabellen.Beleg(t, z); break;
                }
                annahmen.Add("Fallleitung mit Hauptlüftung und Winkel-Abzweig angenommen (Tabelle 11).");
                if (kWert is not null and not 0.5) annahmen.Add($"K = {kWert}: Spalte DU gilt für K = 0.5 – gewählt über Q_ww = {Math.Round(qww, 2)} l/s ≤ Q_max (Annahme, Frage an Christian).");
                break;
            }
            default:   // sammel, grund
            {
                if (kWert is not double kk) { annahmen.Add("Abflusskennzahl K fehlt (Anlage bzw. Projektinformation) – keine Bemessung."); break; }
                double qww = kk * Math.Sqrt(sDu), q = Math.Max(qww, duMax);
                aus["k"] = kk; aus["kHerkunft"] = kHerkunft; aus["qww"] = Math.Round(qww, 3); aus["q"] = Math.Round(q, 3);
                if (q > qww) annahmen.Add($"Q_ww {Math.Round(qww, 2)} l/s kleiner als der grösste Einzel-DU {duMax} – dieser gilt.");
                annahmen.Add("Dauer-, Pumpen- und Regenabfluss (Q_c, Q_p, Q_R) je Teilstrecke nicht berücksichtigt.");
                var t = abw["sammelUndGrundleitungen"]?["tabelle22"];
                var spalten = (t?["gefaelleSpalten_prozent"] as JsonArray ?? []).Select(x => D(x)).ToList();
                if (e.Zahl("gefaelle") is not double g) { annahmen.Add("Gefälle der Leitung fehlt – keine Bemessung."); break; }
                int sp = spalten.FindLastIndex(x => x <= g + 1e-9);
                if (sp < 0) { annahmen.Add($"Gefälle {g} % unter der kleinsten Tabellenspalte {spalten.FirstOrDefault()} % – keine Bemessung."); break; }
                if (g > spalten[^1]) annahmen.Add($"Gefälle {g} % über {spalten[^1]} % – mit {spalten[^1]} % gerechnet.");
                aus["gefaelle"] = g; aus["spalte"] = spalten[sp];
                foreach (var z in (t?["werte"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (art == "grund" && z["fuerGrundleitung"]?.GetValue<bool>() == false) continue;
                    if (z["Qmax_ls"] is not JsonArray qs || sp >= qs.Count || D(qs[sp]) + 1e-9 < q) continue;
                    dnSoll = (int)D(z["DN"]); beleg = SanitaerNormtabellen.Beleg(t, z) + $", Spalte {spalten[sp]} %"; break;
                }
                if (art == "sammel" && dnSoll is int d0)
                {
                    int min = wc > 0 ? 100 : 80;
                    if (d0 < min) { dnSoll = min; annahmen.Add($"Mindest-DN Sammelleitung {(wc > 0 ? "mit WC 100" : "ohne WC 80")} (Handbuch S. 29)."); }
                }
                if (art == "grund") annahmen.Add("Mindest-DN der Grundleitung je Fallleitungshöhe nicht angewendet (Handbuch S. 29 widersprüchlich).");
                break;
            }
        }
        aus["dnSoll"] = dnSoll; aus["beleg"] = beleg;
        // Vergleich mit dem Gezeichneten: Innendurchmesser der Serie gegen den Mindest-Innendurchmesser der Soll-DN (Tabelle 5)
        var mass = HlksMasse.Von(p, e);
        double? innen = mass?.Serie?.Werte.Innen;
        aus["innenIst"] = innen; aus["dnIst"] = e.Zahl("dn");
        if (dnSoll is int dn)
        {
            var t5 = abw["mindestInnendurchmesser"];
            var zeile = (t5?["werte"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(z => (int)D(z["DN"]) == dn);
            double? idMin = zeile is null ? null : D(zeile["IDmin_mm"]);
            aus["idMinSoll"] = idMin;
            aus["ok"] = innen is double i && idMin is double im ? i + 1e-6 >= im : e.Zahl("dn") is double di ? di >= dn : null;
            if (innen is null) annahmen.Add("Kein Innendurchmesser aus der Serie – Vergleich über die DN der Leitung.");
        }
        else aus["ok"] = null;
        aus["annahmen"] = annahmen;
        return aus;
    }
}
