// Modul 4 Teil A-K (entwurf/modul-4.md, Punkt 4): Sanitär-Zusammenstellung je Anlage wie Christians Excel «LU_DU Zusammenstellung_.xlsx».
// Zeilen = vorkommende Apparatearten in Katalogreihenfolge (Excel-Zeile 7–40), Spalten = Geschosse (Gebäude, Kote aufsteigend: UG zuerst –
// Annahme, im Excel nicht übertragen), Anzahl, ΣLU KW, ΣLU WW, ΣDU (Anzahl × Wert; leer, wenn die Art die Spalte nicht hat).
// Darunter, Formeln wörtlich aus dem Katalog (rechnung):
//   Total LU = ΣLU KW + ΣLU WW (B55), Volumenstrom = Total LU × Faktor (B56, Faktor aus der Zellformel des Katalogs),
//   Spitze Kurve «grösster LU 3» = V^Exponent × Faktor (C57) und Kurve «grösster LU 5» (I57) – Kurvennummern («grösster LU»), Faktoren,
//   Exponenten und Gültigkeitsbereiche aus dem Katalog –, massgebend nach der Regel rechnung.kurvenwahl des Katalogs (grösster Einzel-LU der
//   vorkommenden Arten über dem «grössten LU» der kleineren Kurve → die grössere Kurve; NICHT im Excel, aus HKAP; ob bestätigt, steht im
//   Katalog; die andere Kurve steht klein daneben). Prüfbericht 4A B3: Herkunft der Kurvenwahl in formeln und – solange nicht bestätigt – in
//   hinweise; Volumenstrom ausserhalb des Bereichs einer Kurve (bereichLs) bzw. grösster Einzel-LU über der grösseren Kurve → Hinweis (kein Befund).
//   Spitze gesamt = Spitze + Σ Spezialinstallationen Versorgung (C59 bzw. I59),
//   Q_WW = K × √ΣDU (M66), Q_tot = Q_WW + Σ Spezialinstallationen Entsorgung (M67).
// K ist eine Eigenschaft der Anlage, sonst die Vorgabe der Projektinformation; fehlt sie, gibt es kein Q_WW (Befund P25). Der Excel-Wert (Katalog
// abflusskennzahlBeispiel, Zelle M65) gilt nur für Walkringen und wird nie still eingesetzt (anders als HKAP).
// Zur Anlage gehören die Apparate, die an ein System der Anlage angeschlossen sind (Netz.SystemeVon); Apparate ohne Art zählen nicht
// (Befund P21) und stehen in «ohneArt». Die Stückzahl-Summen zählen wie das Excel jeden Apparat der Anlage, auch mit einem Anschluss ohne
// Netzverbindung bzw. ohne Weg im Netz (P26) – das sagt dann ein Hinweis (Prüfbericht 4A B7), weil die Netzsummen ihn nicht enthalten.
// Keine Normrechnung je Teilstrecke (Teil B).

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
        "Volumenstrom, Spitzendurchfluss nach Kurve «grösster LU 3» und «grösster LU 5» (massgebend aus dem grössten Einzel-LU nach der Regel " +
        "kurvenwahl des Katalogs – nicht im Excel, aus HKAP; Herkunft und Bestätigung in kurvenwahl), Spezialinstallationen " +
        "(Eigenschaft spezialinstallationen der Anlage), Spitze gesamt, ΣDU, Abflusskennzahl K (Anlage bzw. Projektinformation; fehlt sie: kein Q_WW, " +
        "Befund P25), Q_WW = K × √ΣDU und Q_tot. Formeln mit Excel-Zelle und Quelle stehen in der Antwort; hinweise nennt u. a. Volumenstrom ausserhalb " +
        "des Bereichs einer Kurve und Apparate, die in den Stückzahl-Summen, aber nicht in den Netzsummen stehen. Keine Dimensionierung je Teilstrecke.";
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
        if (ohneArt.Count > 0)
            hinweise.Add($"{ohneArt.Count} Apparat(e) ohne Apparateart zählen nicht: {Aufzaehlen(ohneArt)} (Befund P21).");
        if (kWert is null)
            hinweise.Add("Abflusskennzahl K fehlt (Anlage und Projektinformation): kein Q_WW und kein Q_tot (Befund P25).");
        var mehrere = apparate.Where(a => netz.AnlagenJeApparat[a.Id].Count > 1).Select(a => a.Kennzeichen).ToList();
        if (mehrere.Count > 0)
            hinweise.Add($"{mehrere.Count} Apparat(e) hängen auch an Systemen einer anderen Anlage und zählen dort ebenfalls: {string.Join(", ", mehrere.Take(10))}.");
        hinweise.AddRange(Netzabweichungen(m, netz, anlage, apparate));
        aus.Hinweise.InsertRange(0, hinweise);   // vor den Hinweisen der Rechnung (Kurven)
        return aus;
    }

    static string Aufzaehlen(IReadOnlyList<string> kz) => string.Join(", ", kz.Take(10)) + (kz.Count > 10 ? " …" : "");

    static string Stueck(int n) => n == 1 ? "1 Apparat" : $"{n} Apparate";

    /// <summary>Prüfbericht 4A B7 (und B2): die Stückzahl-Summen der Zusammenstellung zählen jeden Apparat der Anlage (wie das Excel), die
    /// Netzsummen nur über verbundene Wege. Je Spalte ein Hinweis, wenn Apparate mit einem Wert dieser Spalte keinen verbundenen Anschluss des
    /// Mediums haben («ΣLU WW enthält n Apparate ohne WW-Verbindung») bzw. ihr Anschluss keinen Weg im Netz hat (Befund P26).</summary>
    static IEnumerable<string> Netzabweichungen(Modell m, SanitaerNetz netz, Element anlage, IReadOnlyList<Element> apparate)
    {
        foreach (var (medium, spalte) in SanitaerKatalog.Spalten)
        {
            var summe = "Σ" + SanitaerKatalog.SpaltenName(spalte);
            var mitWert = apparate.Where(a => netz.Apparate[a.Id].Art?.WertFuer(medium) is not null).ToList();
            var mitWertIds = mitWert.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
            var ohneWegIds = netz.OhneWegIn(anlage.Id).Where(o => o.Medium.Equals(medium, StringComparison.OrdinalIgnoreCase) && mitWertIds.Contains(o.Apparat.Id))
                .Select(o => o.Apparat.Id).ToHashSet(StringComparer.Ordinal);
            var ohneVerbindung = mitWert.Where(a => !ohneWegIds.Contains(a.Id) && !a.Anschluesse.Any(an => Netz.Medium(m, a, an).Equals(medium, StringComparison.OrdinalIgnoreCase)
                    && m.VerbindungAn(a.Id, an.Name) is not null)).Select(a => a.Kennzeichen).ToList();
            if (ohneVerbindung.Count > 0)
                yield return $"{summe} enthält {Stueck(ohneVerbindung.Count)} ohne {medium}-Verbindung: {Aufzaehlen(ohneVerbindung)} – die Zusammenstellung zählt " +
                    "Stückzahl × Wert wie das Excel, die Netzsummen (sanitaer_belastung) zählen sie nicht.";
            var ohneWeg = mitWert.Where(a => ohneWegIds.Contains(a.Id)).Select(a => a.Kennzeichen).ToList();
            if (ohneWeg.Count > 0)
                yield return $"{summe} enthält {Stueck(ohneWeg.Count)} ohne Weg im {medium}-Netz: {Aufzaehlen(ohneWeg)} – in den Netzsummen fehlen sie " +
                    $"{(SanitaerNetz.IstTrinkwasser(medium) ? "stromauf" : "stromab")} (Befund P26).";
        }
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
        a.Volumenstrom = a.TotalLu * kat.VolumenstromFaktor;         // B56 = B55*Faktor (Katalog)
        a.SpitzeKurve3 = kat.Kurve3.Spitze(a.Volumenstrom);          // C57 = B56^Exponent*Faktor (Katalog spitzeKurve3)
        a.SpitzeKurve5 = kat.Kurve5.Spitze(a.Volumenstrom);          // I57 = B56^Exponent*Faktor (Katalog spitzeKurve5)
        var massgebend = kat.MassgebendeKurve(a.GroessterLu);        // Regel rechnung.kurvenwahl, Schwelle = grosserLu der kleineren Kurve (Katalog)
        bool fuenf = ReferenceEquals(massgebend, kat.Kurve5);
        a.MassgebendeKurve = massgebend.Nummer;
        a.Spitzendurchfluss = fuenf ? a.SpitzeKurve5 : a.SpitzeKurve3;
        a.SpezialVersorgung = a.Spezialinstallationen.Sum(s => s.Versorgung);   // I51
        a.SpezialEntsorgung = a.Spezialinstallationen.Sum(s => s.Entsorgung);   // N51
        a.SpitzeGesamtKurve3 = a.SpitzeKurve3 + a.SpezialVersorgung;            // C59 = C57+I51
        a.SpitzeGesamtKurve5 = a.SpitzeKurve5 + a.SpezialVersorgung;            // I59 = I57+I51
        a.SpitzeGesamt = fuenf ? a.SpitzeGesamtKurve5 : a.SpitzeGesamtKurve3;
        a.Hinweise.AddRange(KurvenHinweise(kat, a, massgebend));
        a.Abflusskennzahl = abflusskennzahl;
        a.AbflusskennzahlHerkunft = abflusskennzahl is null ? null : kHerkunft;
        if (abflusskennzahl is double k)
        {
            a.Schmutzwasser = k * Math.Sqrt(a.SummeDu);              // M66 = M65*((M64)^(1/2))
            a.Gesamtabfluss = a.Schmutzwasser + a.SpezialEntsorgung; // M67 = M66+N51
        }
        return a;
    }

    static string Z(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Prüfbericht 4A B3: Hinweise zur Kurvenwahl (nur mit LU, d. h. Volumenstrom > 0). Herkunft der Regel, solange sie im Katalog nicht
    /// bestätigt ist; Volumenstrom ausserhalb des Gültigkeitsbereichs einer Kurve (Katalog bereichLs, Excel C55 bzw. I55); grösster Einzel-LU über
    /// dem «grössten LU» der grösseren Kurve (die Regel deckt ihn nicht ab). Hinweise, keine Befunde – das Excel prüft das ebenfalls nicht.</summary>
    static IEnumerable<string> KurvenHinweise(SanitaerKatalog kat, SanitaerAuswertung a, SanitaerKurve massgebend)
    {
        if (a.Volumenstrom <= 0) yield break;
        foreach (var k in new[] { kat.Kurve3, kat.Kurve5 })
            if (!k.ImBereich(a.Volumenstrom))
                yield return $"Volumenstrom {Z(a.Volumenstrom)} l/s liegt ausserhalb des Bereichs der {k.Name}{(ReferenceEquals(k, massgebend) ? " (massgebend)" : "")}: " +
                    $"{Z(k.BereichVon)} bis {Z(k.BereichBis)} l/s laut Katalog ({k.BereichZelle}) – Spitze dieser Kurve prüfen.";
        if (a.GroessterLu > kat.Kurve5.Nummer)
            yield return $"Grösster Einzel-LU {Z(a.GroessterLu)} liegt über dem «grössten LU» der {kat.Kurve5.Name} – die Regel der Kurvenwahl deckt ihn nicht ab " +
                "(Katalog rechnung.kurvenwahl), Kurve prüfen.";
        if (!kat.Kurvenwahl.Bestaetigt)
            yield return $"Kurvenwahl nicht im Excel, aus HKAP, nicht bestätigt: massgebend ist die {massgebend.Name} nach der Regel «{kat.Kurvenwahl.Regel}» " +
                "(Katalog rechnung.kurvenwahl); beide Kurven sind ausgewiesen.";
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
