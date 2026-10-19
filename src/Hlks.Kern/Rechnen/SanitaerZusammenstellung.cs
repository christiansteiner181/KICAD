// Modul 4 Teil A-K (entwurf/modul-4.md, Punkt 4): Sanitär-Zusammenstellung je Anlage wie Christians Excel «LU_DU Zusammenstellung_.xlsx».
// Zeilen = vorkommende Apparatearten in Katalogreihenfolge (Excel-Zeile 7–40), Spalten = Geschosse (Gebäude, Kote aufsteigend: UG zuerst –
// Annahme, im Excel nicht übertragen), Anzahl, ΣLU KW, ΣLU WW, ΣDU (Anzahl × Wert; leer, wenn die Art die Spalte nicht hat).
// Darunter, Formeln wörtlich aus dem Katalog (rechnung):
//   Total LU = ΣLU KW + ΣLU WW (B55), Volumenstrom = Total LU × Faktor (B56, Faktor aus der Zellformel des Katalogs),
//   Spitze Kurve «grösster LU 3» = V^Exponent × Faktor (C57) und Kurve «grösster LU 5» (I57), massgebend nach dem grössten Einzel-LU der
//   vorkommenden Arten (> 3 → Kurve 5, sonst Kurve 3; Regel aus entwurf/modul-4.md wie HKAP – die andere steht klein daneben),
//   Spitze gesamt = Spitze + Σ Spezialinstallationen Versorgung (C59 bzw. I59),
//   Q_WW = K × √ΣDU (M66), Q_tot = Q_WW + Σ Spezialinstallationen Entsorgung (M67).
// K ist eine Eigenschaft der Anlage, sonst die Vorgabe der Projektinformation; fehlt sie, gibt es kein Q_WW (Befund P25). Der Excel-Wert 0.5
// gilt nur für Walkringen (Katalog abflusskennzahlBeispiel) und wird nie still eingesetzt (anders als HKAP).
// Zur Anlage gehören die Apparate, die an ein System der Anlage angeschlossen sind (Netz.SystemeVon); Apparate ohne Art zählen nicht
// (Befund P21) und stehen in «ohneArt». Keine Normrechnung je Teilstrecke (Teil B).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public sealed class SanitaerZeile
{
    public required SanitaerArt Art { get; init; }
    /// <summary>Stückzahl je Geschoss in der Reihenfolge von <see cref="SanitaerAuswertung.Geschosse"/>.</summary>
    public required int[] JeGeschoss { get; init; }
    public int Anzahl { get; set; }
    /// <summary>Anzahl × Wert; null, wenn die Art diese Spalte nicht hat.</summary>
    public double? SummeLuKw { get; set; }
    public double? SummeLuWw { get; set; }
    public double? SummeDu { get; set; }
    public List<string> Apparate { get; } = [];
}

/// <summary>Eine Spezialinstallation (von Hand je Anlage, wie Excel Zeilen 46–51), Volumenströme in l/s.</summary>
public sealed record SanitaerSpezial(string Bezeichnung, double Versorgung, double Entsorgung);

/// <summary>Spalte der Zusammenstellung: Geschoss (Kennzeichen null = Apparate ohne Geschoss).</summary>
public sealed record SanitaerGeschoss(string? Kennzeichen, string Name, double? Kote);

public sealed class SanitaerAuswertung
{
    public required SanitaerKatalog Katalog { get; init; }
    public Element? Anlage { get; init; }
    public List<SanitaerGeschoss> Geschosse { get; init; } = [];
    public List<SanitaerZeile> Zeilen { get; init; } = [];
    public double SummeLuKw { get; set; }
    public double SummeLuWw { get; set; }
    public double SummeDu { get; set; }
    public int Apparate { get; set; }
    public double TotalLu { get; set; }
    public double Volumenstrom { get; set; }
    public double SpitzeKurve3 { get; set; }
    public double SpitzeKurve5 { get; set; }
    public double GroessterLu { get; set; }
    /// <summary>3 oder 5 (Nummer der massgebenden Kurve).</summary>
    public int MassgebendeKurve { get; set; }
    public double Spitzendurchfluss { get; set; }
    public List<SanitaerSpezial> Spezialinstallationen { get; init; } = [];
    public double SpezialVersorgung { get; set; }
    public double SpezialEntsorgung { get; set; }
    public double SpitzeGesamtKurve3 { get; set; }
    public double SpitzeGesamtKurve5 { get; set; }
    public double SpitzeGesamt { get; set; }
    public double? Abflusskennzahl { get; set; }
    /// <summary>Woher K kommt: «Anlage A-SAN: mensch, …, Quelle» bzw. «Projektinformation PI-1: …»; null ohne K.</summary>
    public string? AbflusskennzahlHerkunft { get; set; }
    public double? Schmutzwasser { get; set; }
    public double? Gesamtabfluss { get; set; }
    public List<string> OhneArt { get; } = [];
    public List<string> Hinweise { get; } = [];
}

public static class SanitaerZusammenstellung
{
    public const string AbfrageText = "Sanitär-Zusammenstellung einer Anlage wie Christians Excel «LU_DU Zusammenstellung_.xlsx» (Modul 4 Teil A): Zeilen = vorkommende " +
        "Apparatearten in Katalogreihenfolge mit Excel-Zeile, Spalten = Geschosse (Kote aufsteigend); Anzahl, ΣLU KW, ΣLU WW, ΣDU; darunter Total LU, " +
        "Volumenstrom, Spitzendurchfluss nach Kurve «grösster LU 3» und «grösster LU 5» (massgebend aus dem grössten Einzel-LU), Spezialinstallationen " +
        "(Eigenschaft spezialinstallationen der Anlage), Spitze gesamt, ΣDU, Abflusskennzahl K (Anlage bzw. Projektinformation; fehlt sie: kein Q_WW, " +
        "Befund P25), Q_WW = K × √ΣDU und Q_tot. Formeln mit Excel-Zelle und Quelle stehen in der Antwort. Keine Dimensionierung je Teilstrecke.";
    public const string ParAnlage = "Anlage (Kennzeichen); leer = die einzige Anlage mit Sanitärapparaten";

    static SanitaerKatalog KatalogPflicht(Katalog k) => k.Sanitaer ?? throw new HlksFehler($"Katalog {SanitaerKatalog.Datei} fehlt im Katalogordner {k.Ordner}.",
        "Die Datei aus dem Repository (kataloge/) wiederherstellen; Werte nur aus Christians Excel «LU_DU Zusammenstellung_.xlsx».");

    /// <summary>Anlage aus dem Parameter; leer = die einzige Anlage mit Apparaten.</summary>
    public static Element AnlageWaehlen(Modell m, Katalog k, string? bezug)
    {
        if (!string.IsNullOrWhiteSpace(bezug))
        {
            var e = m.Hole(bezug, "Anlage");
            if (!k.IstArt(e, "Anlage")) throw new HlksFehler($"{e.Kennzeichen} ist keine Anlage, sondern {e.Klasse}.",
                "Anlagen: " + string.Join(", ", m.DerKlasse("Anlage").Select(a => a.Kennzeichen).Order()));
            return e;
        }
        var netz = SanitaerBelastung.Ergebnis(m, k);
        var mit = netz.AnlagenJeApparat.Values.SelectMany(x => x).Distinct().Select(id => m.Finde(id)!).OrderBy(a => a.Kennzeichen).ToList();
        if (mit.Count == 1) return mit[0];
        throw new HlksFehler(mit.Count == 0 ? "Keine Anlage hat Sanitärapparate." : $"Mehrere Anlagen haben Sanitärapparate: {string.Join(", ", mit.Select(a => a.Kennzeichen))}.",
            mit.Count == 0 ? "Apparate an ein System anschliessen, das teilVon einer Anlage ist." : "Parameter anlage angeben.");
    }

    /// <summary>Zusammenstellung einer Anlage aus dem Modell.</summary>
    public static SanitaerAuswertung Rechnen(Modell m, Katalog k, Element anlage)
    {
        var kat = KatalogPflicht(k);
        var netz = SanitaerBelastung.Ergebnis(m, k);
        var apparate = netz.AnlagenJeApparat.Where(x => x.Value.Contains(anlage.Id)).Select(x => m.Finde(x.Key)!)
            .OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();

        // Spalten: Geschosse der Gebäude, in denen Apparate der Anlage stehen, je Gebäude nach Kote aufsteigend
        var gebaeude = apparate.Select(a => m.Finde(a.Geschoss)).Where(g => g is not null).Select(g => g!.TeilVon).ToHashSet();
        var geschosse = m.DerKlasse("Geschoss").Where(g => gebaeude.Contains(g.TeilVon))
            .OrderBy(g => m.Finde(g.TeilVon)?.Kennzeichen ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Zahl("kote") ?? 0).ThenBy(g => g.Kennzeichen)
            .Select(g => (Id: (string?)g.Id, Spalte: new SanitaerGeschoss(g.Kennzeichen, g.Name ?? g.Kennzeichen, g.Zahl("kote")))).ToList();
        if (apparate.Any(a => m.Finde(a.Geschoss) is null)) geschosse.Add((null, new SanitaerGeschoss(null, "ohne Geschoss", null)));
        var spalteVon = geschosse.Select((g, i) => (g.Id, i)).Where(x => x.Id is not null).ToDictionary(x => x.Id!, x => x.i);
        int ohneSpalte = geschosse.FindIndex(g => g.Id is null);

        var proArt = new Dictionary<string, SanitaerZeile>(StringComparer.Ordinal);
        var ohneArt = new List<string>();
        foreach (var a in apparate)
        {
            var z = netz.Apparate[a.Id];
            if (z.Art is null) { ohneArt.Add(a.Kennzeichen); continue; }
            if (!proArt.TryGetValue(z.Art.Schluessel, out var zeile))
                proArt[z.Art.Schluessel] = zeile = new SanitaerZeile { Art = z.Art, JeGeschoss = new int[geschosse.Count] };
            int spalte = a.Geschoss is not null && spalteVon.TryGetValue(a.Geschoss, out var s) ? s : ohneSpalte;
            if (spalte >= 0) zeile.JeGeschoss[spalte]++;
            zeile.Anzahl++;
            zeile.Apparate.Add(a.Kennzeichen);
        }
        var (spezial, hinweise) = Spezialinstallationen(anlage);
        var (kWert, kHerkunft) = Abflusskennzahl(m, anlage);
        var aus = Auswerten(kat, proArt.Values, spezial, kWert, kHerkunft, anlage, geschosse.Select(g => g.Spalte).ToList());
        aus.OhneArt.AddRange(ohneArt);
        aus.Hinweise.AddRange(hinweise);
        if (ohneArt.Count > 0)
            aus.Hinweise.Add($"{ohneArt.Count} Apparat(e) ohne Apparateart zählen nicht: {string.Join(", ", ohneArt.Take(10))}{(ohneArt.Count > 10 ? " …" : "")} (Befund P21).");
        if (kWert is null)
            aus.Hinweise.Add("Abflusskennzahl K fehlt (Anlage und Projektinformation): kein Q_WW und kein Q_tot (Befund P25).");
        var mehrere = apparate.Where(a => netz.AnlagenJeApparat[a.Id].Count > 1).Select(a => a.Kennzeichen).ToList();
        if (mehrere.Count > 0)
            aus.Hinweise.Add($"{mehrere.Count} Apparat(e) hängen auch an Systemen einer anderen Anlage und zählen dort ebenfalls: {string.Join(", ", mehrere.Take(10))}.");
        return aus;
    }

    /// <summary>Rechnung der Zusammenstellung aus Zeilen (Stückzahlen je Art) – Formeln wie im Excel, Werte aus dem Katalog. Auch für den
    /// Referenzfall ohne Netz (Stückzahlen des Excel).</summary>
    public static SanitaerAuswertung Auswerten(SanitaerKatalog kat, IEnumerable<SanitaerZeile> zeilen, IReadOnlyList<SanitaerSpezial> spezial,
        double? abflusskennzahl, string? kHerkunft, Element? anlage = null, List<SanitaerGeschoss>? geschosse = null)
    {
        var a = new SanitaerAuswertung
        {
            Katalog = kat, Anlage = anlage, Geschosse = geschosse ?? [],
            Zeilen = zeilen.Where(z => z.Anzahl > 0).OrderBy(z => z.Art.Reihenfolge).ToList(),
            Spezialinstallationen = spezial.ToList(),
        };
        foreach (var z in a.Zeilen)
        {
            if (z.Art.LuKw is double kw) { z.SummeLuKw = z.Anzahl * kw; a.SummeLuKw += z.SummeLuKw.Value; }
            if (z.Art.LuWw is double ww) { z.SummeLuWw = z.Anzahl * ww; a.SummeLuWw += z.SummeLuWw.Value; }
            if (z.Art.Du is double du) { z.SummeDu = z.Anzahl * du; a.SummeDu += z.SummeDu.Value; }
            a.Apparate += z.Anzahl;
            a.GroessterLu = Math.Max(a.GroessterLu, z.Art.GroessterLu);
        }
        a.TotalLu = a.SummeLuKw + a.SummeLuWw;                       // B55 = M43+N43
        a.Volumenstrom = a.TotalLu * kat.VolumenstromFaktor;         // B56 = B55*0.1
        a.SpitzeKurve3 = kat.Kurve3.Spitze(a.Volumenstrom);          // C57 = B56^0.353*0.459
        a.SpitzeKurve5 = kat.Kurve5.Spitze(a.Volumenstrom);          // I57 = B56^0.257*0.598
        var massgebend = a.GroessterLu > kat.Kurve3.Nummer ? kat.Kurve5 : kat.Kurve3;   // grösster Einzel-LU > 3 → Kurve «grösster LU 5»
        a.MassgebendeKurve = massgebend.Nummer;
        a.Spitzendurchfluss = massgebend.Nummer == 5 ? a.SpitzeKurve5 : a.SpitzeKurve3;
        a.SpezialVersorgung = a.Spezialinstallationen.Sum(s => s.Versorgung);   // I51
        a.SpezialEntsorgung = a.Spezialinstallationen.Sum(s => s.Entsorgung);   // N51
        a.SpitzeGesamtKurve3 = a.SpitzeKurve3 + a.SpezialVersorgung;            // C59 = C57+I51
        a.SpitzeGesamtKurve5 = a.SpitzeKurve5 + a.SpezialVersorgung;            // I59 = I57+I51
        a.SpitzeGesamt = massgebend.Nummer == 5 ? a.SpitzeGesamtKurve5 : a.SpitzeGesamtKurve3;
        a.Abflusskennzahl = abflusskennzahl;
        a.AbflusskennzahlHerkunft = abflusskennzahl is null ? null : kHerkunft;
        if (abflusskennzahl is double k)
        {
            a.Schmutzwasser = k * Math.Sqrt(a.SummeDu);              // M66 = M65*((M64)^(1/2))
            a.Gesamtabfluss = a.Schmutzwasser + a.SpezialEntsorgung; // M67 = M66+N51
        }
        return a;
    }

    /// <summary>Abflusskennzahl der Anlage, sonst die Vorgabe der Projektinformation (mit Herkunft); null, wenn keine gesetzt ist.</summary>
    public static (double? Wert, string? Herkunft) Abflusskennzahl(Modell m, Element anlage)
    {
        static string H(Element e, Wert w) => $"{e.Klasse} {e.Kennzeichen}: {w.Herkunft.Art}, {w.Herkunft.Akteur}, {w.Herkunft.Zeit:yyyy-MM-dd HH:mm}" +
            (string.IsNullOrWhiteSpace(w.Herkunft.Quelle) ? " (ohne Quelle)" : ", " + w.Herkunft.Quelle);
        if (anlage.Eigenschaft("abflusskennzahl") is { } w && w.AlsZahl() is double k) return (k, H(anlage, w));
        foreach (var pi in m.DerKlasse("Projektinformation").OrderBy(x => x.Kennzeichen))
            if (pi.Eigenschaft("abflusskennzahl") is { } wp && wp.AlsZahl() is double kp) return (kp, H(pi, wp) + " (Vorgabe der Projektinformation)");
        return (null, null);
    }

    /// <summary>Spezialinstallationen der Anlage (Eigenschaft spezialinstallationen); ungültige Einträge zählen nicht (Hinweis).</summary>
    public static (List<SanitaerSpezial> Liste, List<string> Hinweise) Spezialinstallationen(Element anlage)
    {
        var l = new List<SanitaerSpezial>(); var h = new List<string>();
        if (anlage.Eigenschaft("spezialinstallationen")?.Inhalt is not JsonArray a) return (l, h);
        foreach (var (n, i) in a.Select((n, i) => (n, i)))
        {
            var fehler = SpezialFehler(n);
            if (fehler is not null) { h.Add($"Spezialinstallation {i + 1} zählt nicht: {fehler}"); continue; }
            var o = (JsonObject)n!;
            l.Add(new SanitaerSpezial(o["bezeichnung"]!.GetValue<string>().Trim(), Json.Zahl(o["versorgung"]) ?? 0, Json.Zahl(o["entsorgung"]) ?? 0));
        }
        return (l, h);
    }

    static readonly string[] SpezialFelder = ["bezeichnung", "versorgung", "entsorgung"];

    /// <summary>Fehlertext eines Eintrags der Spezialinstallationen oder null (gültig).</summary>
    public static string? SpezialFehler(JsonNode? n)
    {
        if (n is not JsonObject o) return "Eintrag ist kein Objekt { bezeichnung, versorgung, entsorgung }";
        if (o.Select(x => x.Key).FirstOrDefault(x => !SpezialFelder.Contains(x)) is { } fremd) return $"Feld «{fremd}» gibt es nicht (erlaubt: {string.Join(", ", SpezialFelder)})";
        if (o["bezeichnung"] is not JsonValue b || !b.TryGetValue<string>(out var t) || string.IsNullOrWhiteSpace(t)) return "bezeichnung fehlt";
        if (o["versorgung"] is null && o["entsorgung"] is null) return $"«{t}»: versorgung oder entsorgung (l/s) angeben";
        foreach (var f in new[] { "versorgung", "entsorgung" })
            if (o[f] is { } z && (z is not JsonValue zv || zv.GetValueKind() != System.Text.Json.JsonValueKind.Number || Json.Zahl(z) is not double d || d < 0 || !double.IsFinite(d)))
                return $"«{t}»: {f} muss eine Zahl ≥ 0 in l/s sein";
        return null;
    }
}
