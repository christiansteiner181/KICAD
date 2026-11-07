// Modul 4 Teil B – Sanitär dimensionieren je Leitung (Christian 03.11.2026: «Sanitär KI modul weiter bearbeiten … du hast da gestern neue
// daten von mir bekommen»). Grundlage: Summen aus Teil A (SanitaerBelastung) und die Normtabellen im Büroordner (SanitaerNormtabellen;
// SN 592000:2024 bzw. SVGW W3 aus dem Handbuch Gebäudetechnik Sanitär 2025). Rechenweg im Klartext: kataloge/rechenregister.json
// (sanitaer.abwasser_dimensionierung, sanitaer.trinkwasser_dimensionierung). Prüfbericht hlks-pruefer 03.11.2026 (F1–F3, L1–L8) eingearbeitet.
// Abwasser je Leitung, Leitungsart aus dem Strang (art anschlussleitung, fallstrang, sammelleitung, grundleitung; im Fallstrang nur die
// steilen Leitungen = Fallleitung, die waagrechten = Anschlussleitung; ohne Strang geschätzt, dann ein Apparat = Einzelanschlussleitung):
//   Anschlussleitung: abgewickelte Länge bis zur entferntesten Anschluss-Verbindung (Handbuch S. 7); ≤ 4 m Tabelle 9 (unbelüftet), ein Apparat
//     ohne WC bis 10 m Tabelle 9 eine Nennweite grösser, sonst Tabelle 10 (belüftet, mit Umlüftungs-DN). Erste Zeile mit ΣDU ≤ höchstzulässige
//     DU und grösster Einzel-DU ≤ Grenze; Fussnoten (Anzahl Gegenstände je DU, Anzahl WC) aus der Tabelle geprüft.
//   Fallleitung: Tabelle 11 (Hauptlüftung, Winkel-Abzweig) – ΣDU ≤ höchstzulässige DU (gilt für K = 0.5; ohne K angenommen), sonst Q_ww ≤ Q_max.
//   Sammel- und Grundleitung: Q = max(K · √ΣDU, grösster Einzel-DU); Tabelle 22 in der Spalte des Gefälles der Leitung (die nächstkleinere
//     Spalte, nie interpoliert) die kleinste DN mit Q_max ≥ Q; Grundleitung nur Zeilen «für Grundleitung»; Mindest-DN Sammelleitung 80, mit WC 100;
//     Gefälle gegen Tabelle 6 (min/max).
//   Nicht kleiner als eine zufliessende Leitung (Nennweite in Abflussrichtung nie reduziert, Handbuch S. 14 und 26).
//   Vergleich: gezeichneter Innendurchmesser (Serie) ≥ Mindest-Innendurchmesser der Soll-DN (Tabelle 5), sonst DN der Leitung.
// Trinkwasser je Leitung (KW, WW; nicht Zirkulation), SVGW W3 (Handbuch S. 45–70):
//   ΣLU stromab: WW-Leitung ΣLU WW; KW-Leitung ΣLU KW + ΣLU WW (das Kaltwasser vor dem Wassererwärmer trägt dessen Warmwasser-Apparate, wie die
//     Gesamtsumme am Hausanschluss, Teil A); grösster Einzel-LU; QT = ΣLU · 0.1 l/s; Spitzendurchfluss QD nach Diagramm 1 (grösster LU > 3 →
//     QD = 0.598 · QT^0.257, sonst 0.459 · QT^0.353; unter dem Startpunkt der Kurve QD = QT).
//   Leitungsart V (Strang verteilleitung/steigstrang), A (ein Apparat, gespeist aus einem Verteiler), sonst T.
//   Abgewickelte Länge je Abschnitt: V vom Netzanfang bis zum Ende des Verteil-/Steigstrangs (Schnittstelle); A/T von der Leitung bis zur
//     entferntesten Entnahmestelle.
//   Belastungswerttabelle des Systems (Serie der Leitung ↔ Hersteller-System), Leitungsart, Spalte = kleinste Länge ≥ abgewickelte Länge
//     «ohne Wasserzähler», Zeile = kleinste LU-Stufe ≥ ΣLU → Rohr da × s; gezeichneter Aussendurchmesser ≥ da der Tabelle.
// ANNAHMEN (Fragen an Christian): Stufen aufgerundet; ohne Gruppenwasserzähler; Schnittstelle = Ende des Verteil-/Steigstrangs (die
//   Gruppenabsperrung kennt das Modell nicht); Fallleitung Hauptlüftung mit Winkel-Abzweig (Tabelle 11); Schleifungen nicht erkannt; Q_c, Q_p, Q_R
//   je Teilstrecke nicht (nur Anlage, Teil A); Grundleitung Mindest-DN bei Fallhöhe ≤ 10 m im Handbuch widersprüchlich (100/150) – nicht angewendet;
//   K ≠ 0.5 bei Fallleitungen über Q_max; über dem Tabellenende keine Bemessung (Hinweis).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SanitaerDimensionierung
{
    public const string Text = "Sanitär dimensionieren je Leitung (Modul 4 Teil B): Abwasser nach SN 592000:2024 – Leitungsart aus dem Strang, Tabelle 9/10 " +
        "(Anschlussleitung nach abgewickelter Länge), 11 (Fallleitung) bzw. 22 (Sammel-/Grundleitung mit Q = max(K·√ΣDU, grösster Einzel-DU) und Gefälle), Soll-DN " +
        "(nie kleiner als zufliessend), gezeichneter Innendurchmesser gegen den Mindest-Innendurchmesser (Tabelle 5); Trinkwasser nach SVGW W3 – ΣLU (KW vor dem " +
        "Wassererwärmer mit WW), Spitzendurchfluss (Diagramm 1), Leitungsart V/A/T, abgewickelte Länge je Abschnitt, Rohr da × s aus der Belastungswerttabelle des " +
        "Systems der Produktlinie, gezeichneter Aussendurchmesser dagegen; je Ergebnis Tabelle, Zeile, Beleg und Annahmen. Die Normtabellen liegen im " +
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
        // Abwasser je Leitung vor der Regel «nicht kleiner als zufliessend» (auch für die Leitungen stromauf einer gefragten Leitung)
        var roh = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject Roh(Element x) => roh.TryGetValue(x.Id, out var r) ? r : roh[x.Id] = Abwasser(p, x, netz.LeitungMedium[x.Id], netz.Leitung(x.Id)!, AnlageVon(m, x), abw!);
        var zeilen = new JsonArray();
        foreach (var (id, medium) in netz.LeitungMedium.OrderBy(x => m.Finde(x.Key)?.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            if (nur is not null && nur.Id != id) continue;
            if (m.Finde(id) is not { } e) continue;
            if (anlage is not null && AnlageVon(m, e)?.Id != anlage.Id) continue;
            if (SanitaerNetz.IstAbwasser(medium) && abw is not null)
            {
                var aus = Roh(e).DeepClone().AsObject();
                NichtKleiner(m, k, netz, e, aus, abw, Roh);
                Vergleich(p, e, aus, abw);
                zeilen.Add(aus);
            }
            else if (SanitaerNetz.IstTrinkwasser(medium) && tw is not null && netz.Leitung(id) is { } t) zeilen.Add(Trinkwasser(p, netz, e, medium, t, tw));
        }
        return new JsonObject { ["normtabellen"] = SanitaerNormtabellen.Ordner(k), ["leitungen"] = zeilen, ["hinweise"] = hinweise };
    }

    static Element? AnlageVon(Modell m, Element e) => e.System is { } s && m.Finde(s) is { } sys && sys.TeilVon is { } a ? m.Finde(a) : null;

    static string? StrangArt(Modell m, Element e) => e.TeilVon is { } t && m.Finde(t) is { } st ? st.Text("art") : null;

    /// <summary>Leitungsart aus dem Strang (art); ohne Strang geschätzt: steil (|Δz| ≥ waagrechte Länge) = Fallleitung, sonst Sammelleitung.
    /// Im Fallstrang (importierte Stränge fassen Fallleitung, Anschlussleitungen und Fuss zusammen, Probelauf Praxisprojekt 03.11.2026):
    /// waagrecht mit einer Fallleitung stromab = Anschlussleitung; waagrecht ohne Fallleitung stromab = Fuss des Strangs, Sammelleitung;
    /// steil, aber stromab in eine Anschlussleitung mündend = Fallstrecke der Anschlussleitung (Handbuch S. 7: die abgewickelte Länge zählt
    /// Fall- und Sturzstrecken mit); sonst Fallleitung.</summary>
    public static (string Art, bool Geschaetzt) Leitungsart(Modell m, Katalog k, Element e)
    {
        switch (StrangArt(m, e))
        {
            case "fallstrang": return (Steil(e) ? (MuendetInAnschluss(m, k, e) ? "anschluss" : "fall") : SteilStromab(m, k, e) ? "anschluss" : "sammel", false);
            case "sammelleitung": return ("sammel", false);
            case "grundleitung": return ("grund", false);
            case "anschlussleitung": return ("anschluss", false);
        }
        return (Steil(e) ? "fall" : "sammel", true);
    }

    /// <summary>Stromab (über Leitungsbauteile) liegt eine steile Leitung im Fallstrang.</summary>
    static bool SteilStromab(Modell m, Katalog k, Element e) =>
        Netz.Folgen(m, k, e, "stromab", Netz.ElementMedium(m, e), stopp: x => !Netz.IstLeitungsbauteil(k, x))
            .Any(x => k.IstArt(x.Element, "Leitung") && StrangArt(m, x.Element) == "fallstrang" && Steil(x.Element));

    /// <summary>Anschlussleitung ohne Umweg über steile Stücke: Strang «anschlussleitung» bzw. waagrecht im Fallstrang mit Fallleitung stromab.</summary>
    static bool DirektAnschluss(Modell m, Katalog k, Element e) => StrangArt(m, e) switch
    {
        "fallstrang" => !Steil(e) && SteilStromab(m, k, e),
        "anschlussleitung" => true,
        _ => false,
    };

    /// <summary>Stromab (über Leitungsbauteile, bis zur nächsten Leitung) folgt eine Anschlussleitung – auch über weitere steile Stücke im Fallstrang.</summary>
    static bool MuendetInAnschluss(Modell m, Katalog k, Element e, int tiefe = 0) =>
        tiefe < 50 && Netz.Folgen(m, k, e, "stromab", Netz.ElementMedium(m, e), stopp: x => !Netz.IstLeitungsbauteil(k, x) || k.IstArt(x, "Leitung"))
            .Any(x => k.IstArt(x.Element, "Leitung") && (DirektAnschluss(m, k, x.Element) || StrangArt(m, x.Element) == "fallstrang" && Steil(x.Element) && MuendetInAnschluss(m, k, x.Element, tiefe + 1)));

    /// <summary>Steil: der senkrechte Anteil ist mindestens so lang wie der waagrechte.</summary>
    static bool Steil(Element e)
    {
        var p = HlksEinordnung.Punkte(e);
        double dz = 0, xy = 0;
        for (int i = 1; i < p.Count; i++) { dz += Math.Abs(p[i][2] - p[i - 1][2]); xy += Math.Sqrt(Math.Pow(p[i][0] - p[i - 1][0], 2) + Math.Pow(p[i][1] - p[i - 1][1], 2)); }
        return dz >= xy && dz > 0;
    }

    static double D(JsonNode? n) => n?.GetValue<double>() ?? double.NaN;

    /// <summary>Grenze einer Tabellenzeile: Zahl, oder {"groesserAls": x} (letzte Zeile ohne obere Grenze) = unbegrenzt.</summary>
    static double Grenze(JsonNode? n) => n is JsonObject ? double.PositiveInfinity : D(n);

    static double Laenge(Element e) => e.Geometrie is { Art: "linie" or "polylinie" } g ? g.Laenge() : 0;

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

    /// <summary>Leitungsart nach W3. Ein Verteiler stromauf (über Leitungsbauteile) ist die Schnittstelle: dahinter A, wenn ein einziger Apparat
    /// direkt ab dem Verteiler gespeist wird, sonst T – auch wenn die Leitung im Verteil-/Steigstrang liegt (importierte Stränge fassen die
    /// Zuleitungen ab dem Etagenverteiler mit, Probelauf Praxisprojekt 03.11.2026). Ohne Verteiler stromauf: V aus dem Strang
    /// (verteilleitung/steigstrang), sonst T.</summary>
    public static string TrinkwasserArt(Modell m, Katalog k, SanitaerNetz netz, Element e, string medium, SanitaerSumme s)
    {
        bool Mehr(Element x) => netz.LeitungMedium.ContainsKey(x.Id) && (netz.Leitung(x.Id)?.Apparate ?? 0) > 1;
        var weg = Netz.Folgen(m, k, e, "stromauf", medium, stopp: x => !Netz.IstLeitungsbauteil(k, x));
        if (weg.Where(x => k.IstArt(x.Element, "Verteiler")).OrderBy(x => x.Tiefe).FirstOrDefault() is { } vt)
            return s.Apparate <= 1 && !weg.Any(y => y.Tiefe < vt.Tiefe && Mehr(y.Element)) ? "A" : "T";
        return StrangArt(m, e) is "verteilleitung" or "steigstrang" ? "V" : "T";
    }

    /// <summary>Abgewickelte Länge in m je Abschnitt (W3): V vom Netzanfang bis zum Ende des Verteil-/Steigstrangs (Schnittstelle),
    /// A/T von der Leitung bis zur entferntesten Entnahmestelle.</summary>
    public static double AbgewickelteLaenge(Modell m, Katalog k, Element e, string medium, string art)
    {
        double eigen = Laenge(e);
        if (art != "V")
            return (eigen + Netz.Folgen(m, k, e, "stromab", medium).Where(x => Netz.IstEndgeraet(k, x.Element)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max()) / 1000;
        bool V(Element x) => k.IstArt(x, "Leitung") && StrangArt(m, x) is "verteilleitung" or "steigstrang";
        double auf = Netz.Folgen(m, k, e, "stromauf", medium, stopp: x => !Netz.IstLeitungsbauteil(k, x)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max();
        double ab = Netz.Folgen(m, k, e, "stromab", medium, stopp: x => !Netz.IstLeitungsbauteil(k, x) || k.IstArt(x, "Leitung") && !V(x))
            .Where(x => V(x.Element)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max();
        return (auf + eigen + ab) / 1000;
    }

    /// <summary>Belastungswerttabellen zum System der Serie und zur Leitungsart; mehr als eine = Zuordnung mehrdeutig (die erste gilt, Hinweis).</summary>
    public static List<JsonObject> Tabellen(JsonObject tw, string serie, string art)
    {
        var systeme = Systeme.Where(x => serie.Contains(x.Serie, StringComparison.OrdinalIgnoreCase)).Select(x => x.System).Distinct().ToList();
        return (tw["belastungswerttabellen"]?["tabellen"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(x => systeme.Any(sy => (x["system"]?.GetValue<string>() ?? "").Contains(sy, StringComparison.OrdinalIgnoreCase)) && x["leitungsart"]?.GetValue<string>() == art).ToList();
    }

    /// <summary>Feld der Belastungswerttabelle: Spalte = kleinste Länge ≥ abgewickelte Länge «ohne Wasserzähler» (-1 = über der Tabelle),
    /// Zeile = kleinste LU-Stufe ≥ ΣLU (null = über der Tabelle), Rohr = Tabellenfeld.</summary>
    public static (int Spalte, JsonObject? Zeile, string? Rohr) Feld(JsonObject tab, double laenge, double lu)
    {
        var laengen = (tab["spaltenKopf"]?["abgewickelteLaenge_m"] as JsonArray ?? []).Select(D).ToList();
        var zaehler = (tab["spaltenKopf"]?["wasserzaehler"] as JsonArray ?? []).Select(x => x?.GetValue<string>()).ToList();
        int sp = Enumerable.Range(0, laengen.Count).Where(i => laengen[i] + 1e-9 >= laenge && (i >= zaehler.Count || zaehler[i] is null or "ohne")).DefaultIfEmpty(-1).First();
        var zeile = (tab["zeilen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(z => D(z["lu"]) + 1e-9 >= lu);
        string? rohr = sp >= 0 && zeile?["werte"] is JsonArray w && sp < w.Count ? w[sp]?.GetValue<string>() : null;
        return (sp, zeile, rohr);
    }

    static JsonObject Trinkwasser(Projekt p, SanitaerNetz netz, Element e, string medium, SanitaerSumme s, JsonObject tw)
    {
        var m = p.Modell; var k = p.Katalog;
        bool ww = SanitaerKatalog.SpalteFuer(medium) == "luWw";
        double lu = ww ? s.SummeLuWw : s.SummeLuKw + s.SummeLuWw, luMax = s.GroessterLu;
        double qt = lu * D(tw["diagramm1"]?["einheitLU_ls"]);
        var annahmen = new JsonArray();
        var aus = new JsonObject { ["leitung"] = e.Kennzeichen, ["medium"] = medium, ["summeLu"] = lu, ["groessterLu"] = luMax, ["apparate"] = s.Apparate, ["qt"] = Math.Round(qt, 3) };
        if (!ww && s.SummeLuWw > 0) annahmen.Add($"Kaltwasser vor dem Wassererwärmer: ΣLU = ΣLU KW {s.SummeLuKw} + ΣLU WW {s.SummeLuWw} der Warmwasser-Apparate dahinter.");
        if (s.OhneWert.Count > 0) annahmen.Add($"{s.OhneWert.Count} Apparat(e) ohne LU – Summe zu klein (Teil A, Apparateart setzen).");
        if (lu <= 0) { aus["annahmen"] = annahmen; aus["ok"] = null; return aus; }
        var (qd, kurve, h) = Spitze(tw, qt, luMax);
        aus["qd"] = Math.Round(qd, 3); aus["kurve"] = kurve;
        if (h is not null) annahmen.Add(h);
        // Leitungsart und abgewickelte Länge je Abschnitt
        string art = TrinkwasserArt(m, k, netz, e, medium, s);
        double laenge = AbgewickelteLaenge(m, k, e, medium, art);
        aus["art"] = art; aus["laenge_m"] = Math.Round(laenge, 2);
        annahmen.Add(art switch
        {
            "V" => "Leitungsart V aus dem Strang (verteilleitung/steigstrang); Länge vom Netzanfang bis zum Ende des Strangs (= Schnittstelle, die Gruppenabsperrung ist im Modell nicht erfasst).",
            "A" => "Leitungsart A: ein Apparat, gespeist aus einem Verteiler; Länge bis zur Entnahmestelle.",
            _ => "Leitungsart T (ausserhalb Verteil-/Steigstrang, nicht einzeln ab Verteiler); Länge bis zur entferntesten Entnahmestelle.",
        });
        // Tabelle des Systems
        var serie = HlksMasse.Von(p, e)?.Serie?.SerieName ?? "";
        var passend = Tabellen(tw, serie, art);
        var tab = passend.FirstOrDefault();
        aus["serie"] = serie; aus["tabelle"] = tab is null ? null : $"Belastungswerttabelle {tab["tabelle"]}, S. {tab["seite"]}: {tab["system"]}";
        if (passend.Count > 1)
            annahmen.Add($"Produktlinie «{serie}» passt zu {passend.Count} Tabellen ({string.Join("; ", passend.Select(x => $"{x["tabelle"]} {x["system"]}"))}) – die erste gewählt; Zuordnung im Katalog schärfen (Frage an Christian).");
        if (tab is null) { annahmen.Add($"Keine Belastungswerttabelle für die Produktlinie «{serie}» und Leitungsart {art}."); aus["annahmen"] = annahmen; aus["ok"] = null; return aus; }
        var (sp, zeile, rohr) = Feld(tab, laenge, lu);
        annahmen.Add("LU- und Längenstufen aufgerundet, Spalte «ohne Wasserzähler».");
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

    /// <summary>Abgewickelte Länge einer Anschlussleitung in m (Handbuch S. 7: inkl. Fall- und Sturzstrecken bis zur entferntesten
    /// Anschluss-Verbindung): stromauf bis zum entferntesten Apparat, stromab durch die Anschlussleitung bis zur Fall-, Sammel- bzw. Grundleitung.</summary>
    public static double AnschlussLaenge(Modell m, Katalog k, Element e, string medium)
    {
        bool Anschluss(Element x) => k.IstArt(x, "Leitung") && Leitungsart(m, k, x).Art == "anschluss";
        double auf = Netz.Folgen(m, k, e, "stromauf", medium).Where(x => Netz.IstEndgeraet(k, x.Element)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max();
        double ab = Netz.Folgen(m, k, e, "stromab", medium, stopp: x => !Netz.IstLeitungsbauteil(k, x) || k.IstArt(x, "Leitung") && !Anschluss(x))
            .Where(x => Anschluss(x.Element)).Select(x => x.LaengeBisHier).DefaultIfEmpty(0).Max();
        return (auf + Laenge(e) + ab) / 1000;
    }

    /// <summary>Erste Zeile einer Anschlussleitungstabelle (9 oder 10) mit ΣDU und grösstem Einzel-DU unter den Grenzen; Fussnoten mit
    /// maxApparate/duJeApparat bzw. maxWC aus der Tabelle geprüft. Liefert den Index der Zeile oder -1.</summary>
    static int AnschlussZeile(JsonNode? t, double sDu, double duMax, int wc, IReadOnlyList<double> du)
    {
        var werte = (t?["werte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        for (int i = 0; i < werte.Count; i++)
        {
            var z = werte[i];
            if (sDu > Grenze(z["hoechstDU"]) + 1e-9 || duMax > D(z["groesstEinzelDU"]) + 1e-9) continue;
            bool passt = true;
            foreach (var f in (z["fussnoten"] as JsonArray ?? []).Select(x => t?["fussnoten"]?[x!.ToString()]).OfType<JsonObject>())
            {
                if (f["maxApparate"] is { } ma && f["duJeApparat"] is { } dj && du.Count(x => x >= D(dj) - 1e-9) > D(ma)) passt = false;
                if (f["maxWC"] is { } mw && wc > D(mw)) passt = false;
            }
            if (passt) return i;
        }
        return -1;
    }

    static JsonObject Abwasser(Projekt p, Element e, string medium, SanitaerSumme s, Element? anlage, JsonObject abw)
    {
        var m = p.Modell; var k = p.Katalog;
        var (art, geschaetzt) = Leitungsart(m, k, e);
        bool einzel = geschaetzt && s.Apparate == 1 && art != "anschluss";
        if (einzel) art = "anschluss";   // ohne Strang: ein Apparat stromauf = Einzelanschlussleitung (mit Strang gilt der Strang, Prüfbericht L1)
        var beitraege = s.Beitraege.Where(b => b.Wert is double).ToList();
        double sDu = s.SummeDu, duMax = beitraege.Count == 0 ? 0 : beitraege.Max(b => b.Wert!.Value);
        int wc = beitraege.Where(b => b.Zuordnung.Schluessel?.StartsWith("wc", StringComparison.OrdinalIgnoreCase) == true).Select(b => b.Apparat.Id).Distinct().Count();
        var duJeApparat = beitraege.GroupBy(b => b.Apparat.Id).Select(g => g.Max(b => b.Wert!.Value)).ToList();
        var (kWert, kHerkunft) = SanitaerZusammenstellung.Abflusskennzahl(m, anlage);
        var annahmen = new JsonArray();
        var aus = new JsonObject
        {
            ["leitung"] = e.Kennzeichen, ["medium"] = medium, ["art"] = art, ["artGeschaetzt"] = geschaetzt,
            ["summeDu"] = Math.Round(sDu, 3), ["groessterDu"] = duMax, ["wc"] = wc, ["apparate"] = s.Apparate,
        };
        if (einzel) annahmen.Add("Ohne Strang, ein Apparat stromauf: als Einzelanschlussleitung gerechnet.");
        else if (geschaetzt) annahmen.Add("Leitungsart ohne Strang aus der Lage geschätzt (steil = Fallleitung, sonst Sammelleitung).");
        if (StrangArt(m, e) == "fallstrang" && art == "anschluss" && Steil(e))
            annahmen.Add("Steile Leitung im Fallstrang, die in eine Anschlussleitung mündet: als Fallstrecke der Anschlussleitung gerechnet (Handbuch S. 7).");
        else if (StrangArt(m, e) == "fallstrang" && art == "sammel")
            annahmen.Add("Waagrechte Leitung im Fallstrang ohne Fallleitung stromab (Fuss des Strangs): als Sammelleitung gerechnet.");
        else if (StrangArt(m, e) == "fallstrang" && art == "anschluss")
            annahmen.Add("Waagrechte Leitung im Fallstrang als Anschlussleitung gerechnet; eine Schleifung (liegende Verbindung zweier Fallleitungsteile, Handbuch S. 15: nach Tabelle 22) wird nicht erkannt.");
        if (s.OhneWert.Count > 0) annahmen.Add($"{s.OhneWert.Count} Apparat(e) ohne DU – Summe zu klein (Teil A, Apparateart setzen).");
        int? dnSoll = null; string? beleg = null;
        if (s.Apparate == 0) { annahmen.Add("Kein Apparat stromauf (z. B. Lüftungsteil über dem Fallstrang oder offenes Ende) – keine Bemessung."); art = "ohne"; }
        switch (art)
        {
            case "ohne": break;
            case "anschluss":
            {
                double l = AnschlussLaenge(m, k, e, medium);
                aus["laenge_m"] = Math.Round(l, 2);
                var t9 = abw["anschlussleitungen"]?["tabelle9_unbeluefteteAnschlussleitungen"];
                var t10 = abw["anschlussleitungen"]?["tabelle10_beluefteteAnschlussleitungen"];
                var regel = abw["anschlussleitungen"]?["auswahl"];
                bool einzelnOhneWc = s.Apparate == 1 && wc == 0;
                var t = l <= 4 + 1e-9 || einzelnOhneWc && l <= 10 + 1e-9 ? t9 : t10;
                int i = AnschlussZeile(t, sDu, duMax, wc, duJeApparat);
                var werte = (t?["werte"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                if (i >= 0 && t == t9 && l > 4 + 1e-9)
                {
                    // ein Apparat ohne WC, 4–10 m: Tabelle 9 eine Nennweite grösser
                    if (i + 1 < werte.Count) { annahmen.Add($"Abgewickelte Länge {Math.Round(l, 1)} m (4–10 m, ein Apparat ohne WC): Tabelle 9 eine Nennweite grösser ({SanitaerNormtabellen.Beleg(new JsonObject { ["tabelle"] = "Ablaufschema Anschlussleitungen" }, regel)}); Sonderbedingungen S. 9 von Hand prüfen."); i++; }
                    else { annahmen.Add("Tabelle 9 hat keine grössere Nennweite – keine Bemessung."); i = -1; }
                }
                if (i >= 0)
                {
                    var z = werte[i];
                    dnSoll = (int)D(z["DN"]); beleg = SanitaerNormtabellen.Beleg(t, z);
                    var fn = (z["fussnoten"] as JsonArray ?? []).Select(x => t?["fussnoten"]?[x!.ToString()]).OfType<JsonObject>();
                    if (wc > 0 && fn.Any(f => f["nurRohboden"]?.GetValue<bool>() == true)) annahmen.Add($"Fussnote: {fn.First(f => f["nurRohboden"] is not null)["bez"]} – von Hand prüfen.");
                    if (z["umlueftungDN"] is { } ul) { aus["umlueftungDn"] = (int)D(ul); annahmen.Add($"Belüftete Anschlussleitung (Tabelle 10): Umlüftung DN {(int)D(ul)} vorsehen."); }
                }
                else if (!annahmen.Any(x => x?.ToString().StartsWith("Tabelle 9 hat") == true))
                    annahmen.Add($"ΣDU {Math.Round(sDu, 2)} bzw. Einzel-DU {duMax} über der {t?["tabelle"]} – keine Bemessung.");
                if (t == t10) annahmen.Add($"Abgewickelte Länge {Math.Round(l, 1)} m über {(einzelnOhneWc ? "10" : "4")} m: belüftet nach Tabelle 10 ({SanitaerNormtabellen.Beleg(new JsonObject { ["tabelle"] = "Ablaufschema Anschlussleitungen" }, regel)}).");
                break;
            }
            case "fall":
            {
                var t = abw["fallleitungen"]?["tabelle11_hauptlueftung_winkelAbzweig"];
                double qww = (kWert ?? 0.5) * Math.Sqrt(sDu);
                foreach (var z in (t?["werte"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    bool passt = kWert is null or 0.5 ? sDu <= Grenze(z["hoechstDU_K05"]) + 1e-9 : qww <= D(z["Qmax_ls"]) + 1e-9;
                    if (!passt || duMax > D(z["groesstEinzelDU"]) + 1e-9) continue;
                    dnSoll = (int)D(z["DN"]); beleg = SanitaerNormtabellen.Beleg(t, z); break;
                }
                annahmen.Add("Fallleitung mit Hauptlüftung und Winkel-Abzweig angenommen (Tabelle 11).");
                if (kWert is null) annahmen.Add("Abflusskennzahl K fehlt (Anlage bzw. Projektinformation) – Spalte für K = 0.5 angenommen.");
                else { aus["k"] = kWert; aus["kHerkunft"] = kHerkunft; }
                if (kWert is not null and not 0.5) annahmen.Add($"K = {kWert}: Spalte DU gilt für K = 0.5 – gewählt über Q_ww = {Math.Round(qww, 2)} l/s ≤ Q_max (Annahme, Frage an Christian).");
                if (dnSoll is null) annahmen.Add($"ΣDU {Math.Round(sDu, 2)} bzw. Einzel-DU {duMax} über der {t?["tabelle"]} – keine Bemessung.");
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
                if (dnSoll is null) annahmen.Add($"Q {Math.Round(q, 2)} l/s über der {t?["tabelle"]} (Spalte {spalten[sp]} %) – keine Bemessung.");
                if (art == "sammel" && dnSoll is int d0)
                {
                    int min = wc > 0 ? 100 : 80;
                    if (d0 < min) { dnSoll = min; annahmen.Add($"Mindest-DN Sammelleitung {(wc > 0 ? "mit WC 100" : "ohne WC 80")} (Handbuch S. 29)."); }
                }
                if (art == "grund") annahmen.Add("Mindest-DN der Grundleitung je Fallleitungshöhe nicht angewendet (Handbuch S. 29 widersprüchlich).");
                break;
            }
        }
        GefaellePruefen(e, art, dnSoll, abw, aus, annahmen);
        aus["dnSoll"] = dnSoll; aus["beleg"] = beleg;
        aus["annahmen"] = annahmen;
        return aus;
    }

    /// <summary>Gefälle der Leitung gegen Tabelle 6 (min/max je Leitungsart); nur wenn die Leitung ein Gefälle hat.</summary>
    static void GefaellePruefen(Element e, string art, int? dn, JsonObject abw, JsonObject aus, JsonArray annahmen)
    {
        if (e.Zahl("gefaelle") is not double g) return;
        string? key = art switch
        {
            "sammel" => "sammelleitung",
            "anschluss" => "anschlussleitung_sw",
            "grund" => dn > 200 ? "grundleitung_sw_ueber_dn200" : "grundleitung_sw_bis_dn200",
            _ => null,
        };
        var t6 = abw["gefaelle"];
        if (key is null || (t6?["werte"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(z => z["key"]?.GetValue<string>() == key) is not { } z) return;
        double min = D(z["gefaelle_prozent"]?["min"]), max = D(z["gefaelle_prozent"]?["max"]);
        aus["gefaelleMin"] = min; aus["gefaelleMax"] = max;
        aus["gefaelleOk"] = g + 1e-9 >= min && g <= max + 1e-9;
        if (g + 1e-9 < min) annahmen.Add($"Gefälle {g} % unter dem Mindestgefälle {min} % ({z["leitungsart"]}, {SanitaerNormtabellen.Beleg(t6, z)}).");
        else if (g > max + 1e-9) annahmen.Add($"Gefälle {g} % über dem Höchstgefälle {max} % ({z["leitungsart"]}, {SanitaerNormtabellen.Beleg(t6, z)}).");
    }

    /// <summary>Nennweite in Abflussrichtung nie reduziert (Handbuch S. 26, Fallleitung S. 14): Soll-DN mindestens die grösste Soll-DN der
    /// zufliessenden Abwasserleitungen.</summary>
    static void NichtKleiner(Modell m, Katalog k, SanitaerNetz netz, Element e, JsonObject aus, JsonObject abw, Func<Element, JsonObject> roh)
    {
        if (aus["dnSoll"]?.GetValue<int>() is not int dn) return;
        (int Dn, string Kz)? groesste = null;
        foreach (var x in Netz.Folgen(m, k, e, "stromauf", netz.LeitungMedium[e.Id]))
            if (netz.LeitungMedium.TryGetValue(x.Element.Id, out var mx) && SanitaerNetz.IstAbwasser(mx) && roh(x.Element)["dnSoll"]?.GetValue<int>() is int d && d > (groesste?.Dn ?? dn))
                groesste = (d, x.Element.Kennzeichen);
        if (groesste is not { } g) return;
        aus["dnSoll"] = g.Dn; aus["dnTabelle"] = dn;
        var regel = abw["sammelUndGrundleitungen"]?["regeln"]?["dimensionswechsel"];
        (aus["annahmen"] as JsonArray)?.Add($"DN {g.Dn} statt {dn}: nicht kleiner als die zufliessende Leitung {g.Kz} ({SanitaerNormtabellen.Beleg(new JsonObject { ["tabelle"] = "Dimensionswechsel" }, regel)}).");
    }

    /// <summary>Vergleich mit dem Gezeichneten: Innendurchmesser der Serie gegen den Mindest-Innendurchmesser der Soll-DN (Tabelle 5).</summary>
    static void Vergleich(Projekt p, Element e, JsonObject aus, JsonObject abw)
    {
        var annahmen = aus["annahmen"] as JsonArray ?? [];
        double? innen = HlksMasse.Von(p, e)?.Serie?.Werte.Innen;
        aus["innenIst"] = innen; aus["dnIst"] = e.Zahl("dn");
        if (aus["dnSoll"]?.GetValue<int>() is not int dn) { aus["ok"] = null; return; }
        var zeile = (abw["mindestInnendurchmesser"]?["werte"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(z => (int)D(z["DN"]) == dn);
        double? idMin = zeile is null ? null : D(zeile["IDmin_mm"]);
        aus["idMinSoll"] = idMin;
        aus["ok"] = innen is double i && idMin is double im ? i + 1e-6 >= im : e.Zahl("dn") is double di ? di >= dn : null;
        if (innen is null) annahmen.Add("Kein Innendurchmesser aus der Produktlinie – Vergleich über die DN der Leitung.");
    }
}
