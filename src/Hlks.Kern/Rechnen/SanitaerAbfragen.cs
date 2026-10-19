// Modul 4 Teil A-K (entwurf/modul-4.md, Punkt 6): Abfragen der Sanitär-Belastungswerte über die eine Tür (Tuer.Abfrage, MCP, REST):
//   sanitaer_belastung { anlage? | strang? | leitung? }  Belastungssummen je Leitung, Strang, System, Anlage
//   sanitaer_zusammenstellung { anlage? }               Zusammenstellung je Anlage wie das Excel
//   sanitaer_katalog {}                                 Apparatearten, Vorgabe je Klasse, Formeln, Quelle
// Dazu die abgeleiteten Werte in element_lesen (abgeleitet + Block «sanitaer») und «warum» für luKw, luWw, du, sanitaerArt (Apparat) bzw.
// summeLuKw, summeLuWw, groessterLu, summeDu (Leitung). Die Eigenschaft sanitaerArt ändert element_aendern wie jede andere.
// SanitaerNachfuehren prüft die Eingaben der Anlage bzw. Projektinformation (abflusskennzahl > 0, spezialinstallationen) und lehnt ab.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class SanitaerAbfragen
{
    public const string BelastungText = "Belastungssummen Sanitär (Modul 4 Teil A, Werte aus Christians Excel «LU_DU Zusammenstellung_.xlsx» über den Katalog " +
        "sanitaer_belastungswerte.json): je Leitung des Trinkwassernetzes (KW, WW) ΣLU KW, ΣLU WW und grösster Einzel-LU der Apparate stromab, je Leitung des " +
        "Schmutzwassernetzes (SW) ΣDU der Apparate stromauf; dazu je Strang, System und Anlage. Jeder Apparat zählt je Leitung einmal (Masche: Befund P24). " +
        "Zirkulation und Regenwasser sind ausgenommen. Keine Dimensionierung (kein Spitzendurchfluss, keine DN, kein Q_WW je Teilstrecke). Mit leitung: eine " +
        "Leitung mit den Beiträgen der Apparate; mit strang bzw. anlage: nur diese; ohne Parameter: alle Anlagen.";
    public const string KatalogText = "Katalog der Sanitär-Apparatearten (kataloge/sanitaer_belastungswerte.json, aus Christians Excel «LU_DU Zusammenstellung_.xlsx»): " +
        "Schlüssel, Name, LU KW, LU WW, DU (null = Anschluss fehlt) und Excel-Zeile in Katalogreihenfolge; Vorgabe je Klasse; Rechenformeln mit Excel-Zelle; Quelle.";
    public const string ParAnlage = "Nur diese Anlage (Kennzeichen)";
    public const string ParStrang = "Nur dieser Strang (Kennzeichen)";
    public const string ParLeitung = "Eine Leitung (Kennzeichen): Summen mit den Beiträgen der Apparate";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("sanitaer_belastung", BelastungText,
            [new("anlage", "text", false, ParAnlage), new("strang", "text", false, ParStrang), new("leitung", "text", false, ParLeitung)]),
        new("sanitaer_zusammenstellung", SanitaerZusammenstellung.AbfrageText, [new("anlage", "text", false, SanitaerZusammenstellung.ParAnlage)]),
        new("sanitaer_katalog", KatalogText, []),
    ];

    /// <summary>Beantwortet eine Sanitär-Abfrage; null = nicht zuständig.</summary>
    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "sanitaer_belastung" => Belastung(p, Param.Text(par, "anlage"), Param.Text(par, "strang"), Param.Text(par, "leitung")),
        "sanitaer_zusammenstellung" => Zusammenstellung(p, Param.Text(par, "anlage")),
        "sanitaer_katalog" => KatalogLesen(p),
        _ => null,
    };

    static SanitaerKatalog Kat(Katalog k) => k.Sanitaer ?? throw new HlksFehler($"Katalog {SanitaerKatalog.Datei} fehlt im Katalogordner {k.Ordner}.",
        "Die Datei aus dem Repository (kataloge/) wiederherstellen; Werte nur aus Christians Excel «LU_DU Zusammenstellung_.xlsx».");

    const string KeineDimensionierung = "Summen der Belastungswerte, keine Dimensionierung: Spitzendurchfluss, DN und Q_WW je Teilstrecke folgen in Teil B (Normen SN 592000, SN EN 12056, SVGW W3 fehlen).";

    // ------------------------------------------------------------------ sanitaer_katalog

    public static JsonObject KatalogLesen(Projekt p)
    {
        var kat = Kat(p.Katalog);
        return new JsonObject
        {
            ["quelle"] = new JsonObject
            {
                ["katalog"] = "kataloge/" + SanitaerKatalog.Datei, ["dokument"] = kat.Dokument, ["objekt"] = kat.Objekt, ["stand"] = kat.Stand,
                ["verfasser"] = kat.Verfasser, ["bereich"] = kat.Bereich,
            },
            ["arten"] = new JsonArray(kat.Arten.Select(a => (JsonNode)new JsonObject
            {
                ["schluessel"] = a.Schluessel, ["name"] = a.Name, ["luKw"] = a.LuKw, ["luWw"] = a.LuWw, ["du"] = a.Du, ["zeile"] = a.Zeile,
            }).ToArray()),
            ["vorgabeJeKlasse"] = new JsonObject(kat.VorgabeJeKlasse.OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Value))),
            ["ohneVorgabe"] = new JsonArray(p.Katalog.Klassen.Values.Where(d => !d.Abstrakt && d.IstArt("Apparat") && kat.Vorgabe(p.Katalog, d.Name, out _) is null)
                .Select(d => (JsonNode)JsonValue.Create(d.Name)).ToArray()),
            ["formeln"] = Formeln(kat),
            ["hinweis"] = "Werte nur aus dem Katalog (Christians Excel); fehlt eine Art, im Excel bzw. Katalog nachführen. " + KeineDimensionierung,
        };
    }

    static JsonObject Formeln(SanitaerKatalog kat) => new()
    {
        ["totalLu"] = kat.TotalLuText,
        ["volumenstrom"] = kat.VolumenstromText,
        ["spitzeKurve3"] = $"{kat.Kurve3.Zelle} – {kat.Kurve3.Text}",
        ["spitzeKurve5"] = $"{kat.Kurve5.Zelle} – {kat.Kurve5.Text}",
        ["massgebendeKurve"] = $"grösster Einzel-LU der vorkommenden Arten > {kat.Kurve3.Nummer} → Kurve «grösster LU {kat.Kurve5.Nummer}», sonst Kurve «grösster LU {kat.Kurve3.Nummer}» (entwurf/modul-4.md, wie HKAP)",
        ["spitzeGesamt"] = kat.SpitzeGesamtText,
        ["schmutzwasser"] = $"{kat.SchmutzwasserText} ({kat.SchmutzwasserZelle})",
        ["gesamtabfluss"] = kat.GesamtabflussText,
    };

    // ------------------------------------------------------------------ sanitaer_belastung

    static string Kz(Modell m, string? id) => m.Finde(id)?.Kennzeichen ?? "";

    static JsonObject Summen(SanitaerSumme s, bool trink, bool abwasser)
    {
        var o = new JsonObject();
        if (trink) { o["summeLuKw"] = s.SummeLuKw; o["summeLuWw"] = s.SummeLuWw; o["groessterLu"] = s.GroessterLu; }
        if (abwasser) o["summeDu"] = s.SummeDu;
        o["apparate"] = s.Apparate;
        var ohne = s.OhneWert;
        if (ohne.Count > 0) o["ohneWert"] = new JsonArray(ohne.Select(b => (JsonNode)JsonValue.Create(OhneWertText(b))).ToArray());
        if (s.Masche) o["masche"] = true;
        return o;
    }

    static string OhneWertText(SanitaerBeitrag b) =>
        $"{b.Apparat.Kennzeichen}.{b.Anschluss} ({(b.Zuordnung.Art is null ? "ohne Apparateart" : $"Art {b.Zuordnung.Art.Schluessel} ohne {SanitaerKatalog.SpaltenName(SanitaerKatalog.SpalteFuer(b.Medium)!)}")})";

    static string NetzArt(string medium) => SanitaerNetz.IstTrinkwasser(medium) ? "trinkwasser" : "abwasser";

    static JsonObject LeitungKurz(Modell m, SanitaerNetz n, Element l)
    {
        var med = n.LeitungMedium[l.Id];
        var o = new JsonObject { ["leitung"] = l.Kennzeichen, ["strang"] = m.Finde(l.TeilVon) is { Klasse: "Strang" } st ? st.Kennzeichen : null, ["medium"] = med };
        foreach (var (k, v) in Summen(n.Leitung(l.Id)!, SanitaerNetz.IstTrinkwasser(med), SanitaerNetz.IstAbwasser(med))) o[k] = v?.DeepClone();
        return o;
    }

    static JsonObject StrangKurz(Modell m, SanitaerNetz n, Element st, string medium)
    {
        var leitungen = n.LeitungMedium.Keys.Where(id => m.Finde(id)?.TeilVon == st.Id).ToList();
        var o = new JsonObject { ["strang"] = st.Kennzeichen, ["name"] = st.Name, ["art"] = st.Text("art"), ["system"] = Kz(m, st.TeilVon), ["medium"] = medium, ["leitungen"] = leitungen.Count };
        foreach (var (k, v) in Summen(n.Ueber(leitungen), SanitaerNetz.IstTrinkwasser(medium), SanitaerNetz.IstAbwasser(medium))) o[k] = v?.DeepClone();
        return o;
    }

    static JsonObject SystemBlock(Modell m, SanitaerNetz n, Element sys)
    {
        var medium = (sys.Text("medium") ?? "").ToUpperInvariant();
        var leitungen = n.LeitungMedium.Keys.Select(id => m.Finde(id)!).Where(l => l.System == sys.Id)
            .OrderBy(l => l.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var o = new JsonObject
        {
            ["system"] = sys.Kennzeichen, ["name"] = sys.Name, ["systemtyp"] = sys.Text("systemtyp"), ["medium"] = medium, ["netz"] = NetzArt(medium),
            ["richtung"] = SanitaerNetz.IstTrinkwasser(medium) ? "Apparate stromab" : "Apparate stromauf",
        };
        foreach (var (k, v) in Summen(n.Ueber(leitungen.Select(l => l.Id)), SanitaerNetz.IstTrinkwasser(medium), SanitaerNetz.IstAbwasser(medium))) o[k] = v?.DeepClone();
        o["straenge"] = new JsonArray(m.DerKlasse("Strang").Where(s => s.TeilVon == sys.Id).OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase)
            .Select(s => (JsonNode)StrangKurz(m, n, s, medium)).ToArray());
        o["leitungen"] = new JsonArray(leitungen.Select(l => (JsonNode)LeitungKurz(m, n, l)).ToArray());
        return o;
    }

    static bool SanitaerSystem(Element sys) => sys.Text("medium") is { } med && SanitaerKatalog.SpalteFuer(med) is not null;

    static JsonObject AnlageBlock(Modell m, SanitaerNetz n, Element anlage)
    {
        var systeme = m.DerKlasse("System").Where(s => s.TeilVon == anlage.Id && SanitaerSystem(s)).OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        IEnumerable<string> Leitungen(Func<string, bool> medium) => n.LeitungMedium.Where(x => medium(x.Value) && systeme.Any(s => s.Id == m.Finde(x.Key)?.System)).Select(x => x.Key);
        return new JsonObject
        {
            ["anlage"] = anlage.Kennzeichen, ["name"] = anlage.Name,
            ["trinkwasser"] = Summen(n.Ueber(Leitungen(SanitaerNetz.IstTrinkwasser)), true, false),
            ["abwasser"] = Summen(n.Ueber(Leitungen(SanitaerNetz.IstAbwasser)), false, true),
            ["systeme"] = new JsonArray(systeme.Select(s => (JsonNode)SystemBlock(m, n, s)).ToArray()),
        };
    }

    public static JsonObject Belastung(Projekt p, string? anlage, string? strang, string? leitung)
    {
        var kat = Kat(p.Katalog);
        var m = p.Modell; var k = p.Katalog;
        var n = SanitaerBelastung.Ergebnis(m, k);
        if (leitung is not null) return LeitungDetail(m, k, kat, n, m.Hole(leitung, "Leitung"));
        if (strang is not null)
        {
            var st = m.HoleDerKlasse(strang, "Strang");
            var sys = m.Finde(st.TeilVon);
            var medium = (sys?.Text("medium") ?? "").ToUpperInvariant();
            if (SanitaerKatalog.SpalteFuer(medium) is null)
                throw new HlksFehler($"Strang {st.Kennzeichen} gehört nicht zum Trinkwasser- oder Schmutzwassernetz (Medium {(medium.Length == 0 ? "unbekannt" : medium)}).",
                    "Belastungssummen gibt es für Stränge der Systeme Trinkwasser kalt, Trinkwasser warm und Schmutzwasser; Zirkulation und Regenwasser sind ausgenommen.");
            var o = StrangKurz(m, n, st, medium);
            o["leitungen"] = new JsonArray(n.LeitungMedium.Keys.Select(id => m.Finde(id)!).Where(l => l.TeilVon == st.Id)
                .OrderBy(l => l.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(l => (JsonNode)LeitungKurz(m, n, l)).ToArray());
            o["quelle"] = kat.QuelleKurz; o["hinweis"] = KeineDimensionierung;
            return o;
        }
        List<Element> anlagen = anlage is not null ? [SanitaerZusammenstellung.AnlageWaehlen(m, k, anlage)]
            : m.DerKlasse("Anlage").Where(a => m.DerKlasse("System").Any(s => s.TeilVon == a.Id && SanitaerSystem(s)) || n.AnlagenJeApparat.Values.Any(v => v.Contains(a.Id)))
                .OrderBy(a => a.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var ergebnis = new JsonObject
        {
            ["anlagen"] = new JsonArray(anlagen.Select(a => (JsonNode)AnlageBlock(m, n, a)).ToArray()),
        };
        if (anlage is null)
        {
            var ohne = m.DerKlasse("System").Where(s => SanitaerSystem(s) && (m.Finde(s.TeilVon) is not { } an || !k.IstArt(an, "Anlage")))
                .OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            if (ohne.Count > 0) ergebnis["systemeOhneAnlage"] = new JsonArray(ohne.Select(s => (JsonNode)SystemBlock(m, n, s)).ToArray());
        }
        if (n.Maschen.Count > 0) ergebnis["maschen"] = new JsonArray(n.Maschen.Keys.Select(id => Kz(m, id)).Order(StringComparer.OrdinalIgnoreCase).Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        ergebnis["quelle"] = kat.QuelleKurz;
        ergebnis["hinweis"] = KeineDimensionierung + " Zirkulation (ZIR) und Regenwasser (RW) sind ausgenommen.";
        return ergebnis;
    }

    static JsonObject LeitungDetail(Modell m, Katalog k, SanitaerKatalog kat, SanitaerNetz n, Element l)
    {
        if (!k.IstArt(l, "Leitung"))
            throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung, sondern {l.Klasse}.", "Belastungssummen gibt es je Leitung (Rohr); für Formteile die angeschlossene Leitung abfragen.");
        if (n.Leitung(l.Id) is not { } s)
            return new JsonObject
            {
                ["leitung"] = l.Kennzeichen, ["medium"] = Netz.SystemMedium(m, l.System),
                ["ausgenommen"] = "Diese Leitung gehört nicht zum Trinkwasser- (KW, WW) oder Schmutzwassernetz (SW); Zirkulation und Regenwasser sind von den Belastungssummen ausgenommen.",
            };
        var med = n.LeitungMedium[l.Id];
        bool trink = SanitaerNetz.IstTrinkwasser(med);
        var sys = m.Finde(l.System);
        var o = new JsonObject
        {
            ["leitung"] = l.Kennzeichen, ["klasse"] = l.Klasse, ["medium"] = med, ["netz"] = NetzArt(med), ["richtung"] = trink ? "Apparate stromab" : "Apparate stromauf",
            ["system"] = sys?.Kennzeichen, ["strang"] = m.Finde(l.TeilVon) is { Klasse: "Strang" } st ? st.Kennzeichen : null,
            ["anlage"] = m.Finde(sys?.TeilVon) is { } an && k.IstArt(an, "Anlage") ? an.Kennzeichen : null,
        };
        foreach (var (key, v) in Summen(s, trink, !trink)) o[key] = v?.DeepClone();
        o["masche"] = s.Masche;
        o["beitraege"] = new JsonArray(s.Beitraege.Select(b => (JsonNode)BeitragJson(kat, b)).ToArray());
        o["rechenweg"] = Rechenweg(s, trink);
        o["quelle"] = kat.QuelleKurz;
        o["hinweis"] = KeineDimensionierung;
        return o;
    }

    static JsonObject BeitragJson(SanitaerKatalog kat, SanitaerBeitrag b)
    {
        var spalte = SanitaerKatalog.SpalteFuer(b.Medium)!;
        return new JsonObject
        {
            ["apparat"] = b.Apparat.Kennzeichen, ["klasse"] = b.Apparat.Klasse, ["anschluss"] = b.Anschluss, ["medium"] = b.Medium,
            ["spalte"] = SanitaerKatalog.SpaltenName(spalte), ["wert"] = b.Wert,
            ["art"] = b.Zuordnung.Art?.Schluessel, ["artName"] = b.Zuordnung.Art?.Name, ["artWoher"] = b.Zuordnung.Woher,
            ["zeile"] = b.Zuordnung.Art?.Zeile,
            ["quelle"] = b.Zuordnung.Art is { } a ? kat.Quelle(a.Zeile) : null,
        };
    }

    static string Rechenweg(SanitaerSumme s, bool trink)
    {
        static string Z(double x) => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        string Liste(string medium) => string.Join(" + ", s.Beitraege.Where(b => b.Medium == medium && b.Wert is not null).Select(b => $"{b.Apparat.Kennzeichen} {Z(b.Wert!.Value)}"));
        if (!trink)
            return $"ΣDU = Summe der DU aller Apparate stromauf (je Apparat einmal): {(Liste("SW") is { Length: > 0 } l ? l : "keine")} = {Z(s.SummeDu)}";
        return $"ΣLU KW = Summe der LU KW aller Apparate stromab über ihren KW-Anschluss: {(Liste("KW") is { Length: > 0 } kw ? kw : "keine")} = {Z(s.SummeLuKw)}; " +
               $"ΣLU WW = über den WW-Anschluss: {(Liste("WW") is { Length: > 0 } ww ? ww : "keine")} = {Z(s.SummeLuWw)}; grösster Einzel-LU = {Z(s.GroessterLu)} " +
               "(je Apparat und Medium einmal; das Kaltwasser vor dem Wassererwärmer trägt dessen Warmwasser-Apparate mit)";
    }

    // ------------------------------------------------------------------ sanitaer_zusammenstellung

    public static JsonObject Zusammenstellung(Projekt p, string? anlage)
    {
        var kat = Kat(p.Katalog);
        var an = SanitaerZusammenstellung.AnlageWaehlen(p.Modell, p.Katalog, anlage);
        return ZusammenstellungJson(kat, SanitaerZusammenstellung.Rechnen(p.Modell, p.Katalog, an));
    }

    public static JsonObject ZusammenstellungJson(SanitaerKatalog kat, SanitaerAuswertung a)
    {
        static JsonArray Texte(IEnumerable<string> t) => new(t.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        var f = Formeln(kat);
        return new JsonObject
        {
            ["anlage"] = a.Anlage?.Kennzeichen, ["name"] = a.Anlage?.Name,
            ["quelle"] = new JsonObject
            {
                ["katalog"] = "kataloge/" + SanitaerKatalog.Datei, ["dokument"] = kat.Dokument, ["objekt"] = kat.Objekt, ["stand"] = kat.Stand, ["bereich"] = kat.Bereich,
            },
            ["geschosse"] = new JsonArray(a.Geschosse.Select(g => (JsonNode)new JsonObject { ["kennzeichen"] = g.Kennzeichen, ["name"] = g.Name, ["kote"] = g.Kote }).ToArray()),
            ["zeilen"] = new JsonArray(a.Zeilen.Select(z => (JsonNode)new JsonObject
            {
                ["art"] = z.Art.Schluessel, ["name"] = z.Art.Name, ["zeile"] = z.Art.Zeile,
                ["luKw"] = z.Art.LuKw, ["luWw"] = z.Art.LuWw, ["du"] = z.Art.Du,
                ["jeGeschoss"] = new JsonArray(z.JeGeschoss.Select(x => (JsonNode)x).ToArray()),
                ["anzahl"] = z.Anzahl, ["summeLuKw"] = z.SummeLuKw, ["summeLuWw"] = z.SummeLuWw, ["summeDu"] = z.SummeDu,
                ["apparate"] = Texte(z.Apparate),
                ["quelle"] = kat.Quelle(z.Art.Zeile),
            }).ToArray()),
            ["apparate"] = a.Apparate,
            ["summeLuKw"] = a.SummeLuKw, ["summeLuWw"] = a.SummeLuWw, ["summeDu"] = a.SummeDu,
            ["totalLu"] = a.TotalLu,
            ["volumenstrom"] = a.Volumenstrom,
            ["spitzeKurve3"] = a.SpitzeKurve3, ["spitzeKurve5"] = a.SpitzeKurve5,
            ["groessterLu"] = a.GroessterLu, ["massgebendeKurve"] = a.MassgebendeKurve,
            ["spitzendurchfluss"] = a.Spitzendurchfluss,
            ["spezialinstallationen"] = new JsonArray(a.Spezialinstallationen.Select(s => (JsonNode)new JsonObject
            { ["bezeichnung"] = s.Bezeichnung, ["versorgung"] = s.Versorgung, ["entsorgung"] = s.Entsorgung }).ToArray()),
            ["spezialVersorgung"] = a.SpezialVersorgung, ["spezialEntsorgung"] = a.SpezialEntsorgung,
            ["spitzeGesamtKurve3"] = a.SpitzeGesamtKurve3, ["spitzeGesamtKurve5"] = a.SpitzeGesamtKurve5, ["spitzeGesamt"] = a.SpitzeGesamt,
            ["abflusskennzahl"] = a.Abflusskennzahl, ["abflusskennzahlHerkunft"] = a.AbflusskennzahlHerkunft,
            ["schmutzwasser"] = a.Schmutzwasser, ["gesamtabfluss"] = a.Gesamtabfluss,
            ["einheiten"] = new JsonObject
            {
                ["luKw"] = "LU", ["luWw"] = "LU", ["du"] = "DU", ["volumenstrom"] = "l/s", ["spitze"] = "l/s", ["spezialinstallationen"] = "l/s",
                ["schmutzwasser"] = "l/s", ["gesamtabfluss"] = "l/s",
            },
            ["formeln"] = f,
            ["ohneArt"] = Texte(a.OhneArt),
            ["hinweise"] = Texte(a.Hinweise),
            ["hinweis"] = KeineDimensionierung,
        };
    }

    // ------------------------------------------------------------------ element_lesen, warum

    /// <summary>Ergänzt element_lesen: Apparat → Block «sanitaer» (Art, Herkunft, LU KW, LU WW, DU, Quelle) und abgeleitet luKw, luWw, du;
    /// Leitung des Trinkwasser- bzw. Schmutzwassernetzes → Block «sanitaer» und abgeleitet summeLuKw, summeLuWw, groessterLu bzw. summeDu.</summary>
    public static void ElementErgaenzen(Projekt p, Element e, JsonObject o)
    {
        var kat = p.Katalog.Sanitaer;
        if (kat is null) return;
        var m = p.Modell; var k = p.Katalog;
        bool apparat = SanitaerArten.IstApparat(k, e), leitung = k.IstArt(e, "Leitung");
        if (!apparat && !leitung) return;
        var n = SanitaerBelastung.Ergebnis(m, k);
        JsonObject Abg() => o["abgeleitet"] as JsonObject ?? (JsonObject)(o["abgeleitet"] = new JsonObject());
        if (apparat && n.Apparate.TryGetValue(e.Id, out var z))
        {
            o["sanitaer"] = new JsonObject
            {
                ["art"] = z.Art?.Schluessel ?? z.Schluessel, ["name"] = z.Art?.Name, ["woher"] = z.Woher, ["herkunft"] = z.Herkunft,
                ["luKw"] = z.Art?.LuKw, ["luWw"] = z.Art?.LuWw, ["du"] = z.Art?.Du, ["zeile"] = z.Art?.Zeile,
                ["quelle"] = z.Art is { } a ? kat.Quelle(a.Zeile) : null,
                ["anlagen"] = new JsonArray(n.AnlagenJeApparat[e.Id].Select(id => (JsonNode)JsonValue.Create(Kz(m, id))).ToArray()),
            };
            if (z.Art is { } art)
            {
                var abg = Abg();
                if (art.LuKw is double kw) abg["luKw"] = kw;
                if (art.LuWw is double ww) abg["luWw"] = ww;
                if (art.Du is double du) abg["du"] = du;
            }
        }
        else if (leitung && n.Leitung(e.Id) is { } s)
        {
            var med = n.LeitungMedium[e.Id];
            bool trink = SanitaerNetz.IstTrinkwasser(med);
            var block = new JsonObject { ["netz"] = NetzArt(med), ["medium"] = med, ["richtung"] = trink ? "Apparate stromab" : "Apparate stromauf" };
            foreach (var (key, v) in Summen(s, trink, !trink)) block[key] = v?.DeepClone();
            block["quelle"] = kat.QuelleKurz;
            o["sanitaer"] = block;
            var abg = Abg();
            if (trink) { abg["summeLuKw"] = s.SummeLuKw; abg["summeLuWw"] = s.SummeLuWw; abg["groessterLu"] = s.GroessterLu; }
            else abg["summeDu"] = s.SummeDu;
        }
    }

    static readonly string[] ApparatWerte = ["luKw", "luWw", "du"];
    static readonly string[] LeitungWerte = ["summeLuKw", "summeLuWw", "groessterLu", "summeDu"];

    /// <summary>«warum» für die abgeleiteten Sanitärwerte; null = nicht zuständig (dann gilt die allgemeine Herkunft).</summary>
    public static JsonObject? Warum(Projekt p, Element e, string eigenschaft)
    {
        var kat = p.Katalog.Sanitaer;
        if (kat is null) return null;
        var m = p.Modell; var k = p.Katalog;
        var name = eigenschaft.Trim();
        if (SanitaerArten.IstApparat(k, e))
        {
            bool art = name.Equals("sanitaerArt", StringComparison.OrdinalIgnoreCase);
            var wert = ApparatWerte.FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!art && wert is null) return null;
            if (art && e.Eigenschaft("sanitaerArt") is not null && SanitaerArten.Von(m, k, e).Art is not null) return null;   // gesetzter Wert: allgemeine Herkunft mit Verlauf
            var z = SanitaerBelastung.Ergebnis(m, k).Apparate[e.Id];
            var o = new JsonObject
            {
                ["element"] = e.Kennzeichen, ["eigenschaft"] = art ? "sanitaerArt" : wert,
                ["apparateart"] = new JsonObject { ["schluessel"] = z.Art?.Schluessel ?? z.Schluessel, ["name"] = z.Art?.Name, ["woher"] = z.Woher, ["herkunft"] = z.Herkunft },
            };
            if (art)
            {
                o["wert"] = z.Art?.Schluessel ?? z.Schluessel;
                o["begruendung"] = z.Woher switch
                {
                    "vorgabe" => $"Nicht gesetzt – es gilt die {z.Herkunft}.",
                    "familie" => $"Aus der Familie: {z.Herkunft}.",
                    "unbekannt" => $"«{z.Schluessel}» gibt es im Katalog nicht ({z.Herkunft}) – Befund P21.",
                    _ => $"Keine Apparateart: {z.Herkunft} – Befund P21.",
                };
            }
            else
            {
                double? v = wert switch { "luKw" => z.Art?.LuKw, "luWw" => z.Art?.LuWw, _ => z.Art?.Du };
                o["wert"] = v;
                o["einheit"] = wert == "du" ? "DU" : "LU";
                o["rechenweg"] = z.Art is null ? "Ohne Apparateart kein Wert (Befund P21)."
                    : v is null ? $"Die Apparateart «{z.Art.Name}» hat keinen Wert {SanitaerKatalog.SpaltenName(wert!)} (Excel-Zelle leer: der Apparat hat diesen Anschluss nicht)."
                    : $"Wert der Spalte {SanitaerKatalog.SpaltenName(wert!)} der Apparateart «{z.Art.Name}» ({z.Art.Schluessel}) im Katalog, Excel-Zeile {z.Art.Zeile}; abgeleitet, nicht gespeichert.";
                o["quelle"] = z.Art is { } a ? kat.Quelle(a.Zeile) : null;
            }
            return o;
        }
        if (k.IstArt(e, "Leitung"))
        {
            var wert = LeitungWerte.FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (wert is null) return null;
            var n = SanitaerBelastung.Ergebnis(m, k);
            if (n.Leitung(e.Id) is not { } s)
                throw new HlksFehler($"{e.Kennzeichen} gehört nicht zum Trinkwasser- oder Schmutzwassernetz – {wert} gibt es nicht.",
                    "Belastungssummen gibt es für Leitungen der Medien KW, WW und SW; Zirkulation und Regenwasser sind ausgenommen.");
            var med = n.LeitungMedium[e.Id];
            bool trink = SanitaerNetz.IstTrinkwasser(med);
            if (trink == (wert == "summeDu"))
                throw new HlksFehler($"{e.Kennzeichen} ({med}) hat keinen Wert {wert}.", trink ? "Trinkwasser: summeLuKw, summeLuWw, groessterLu." : "Schmutzwasser: summeDu.");
            return new JsonObject
            {
                ["element"] = e.Kennzeichen, ["eigenschaft"] = wert,
                ["wert"] = wert switch { "summeLuKw" => s.SummeLuKw, "summeLuWw" => s.SummeLuWw, "groessterLu" => s.GroessterLu, _ => s.SummeDu },
                ["einheit"] = wert == "summeDu" ? "DU" : "LU",
                ["rechenweg"] = Rechenweg(s, trink),
                ["masche"] = s.Masche,
                ["beitraege"] = new JsonArray(s.Beitraege.Select(b => (JsonNode)BeitragJson(kat, b)).ToArray()),
                ["quelle"] = kat.QuelleKurz,
            };
        }
        return null;
    }
}

/// <summary>Modul 4: prüft die Eingaben der Sanitär-Zusammenstellung an Anlage und Projektinformation im selben Befehl und lehnt ab:
/// abflusskennzahl muss grösser als 0 sein, spezialinstallationen eine Liste von { bezeichnung, versorgung, entsorgung } (l/s, ≥ 0).</summary>
public sealed class SanitaerNachfuehren : INachfuehrung
{
    public string Name => "sanitaer";

    public void Nachfuehren(Kontext k, IReadOnlySet<string> geaendert, IReadOnlySet<string> geloescht)
    {
        foreach (var id in geaendert)
        {
            if (k.Arbeit.Finde(id) is not { } e || !(k.Katalog.IstArt(e, "Anlage") || e.Klasse == "Projektinformation")) continue;
            if (e.Eigenschaft("abflusskennzahl")?.AlsZahl() is double kz && (!double.IsFinite(kz) || kz <= 0))
                throw new HlksFehler($"{e.Kennzeichen}: Abflusskennzahl {kz.ToString(System.Globalization.CultureInfo.InvariantCulture)} ist nicht möglich – K muss grösser als 0 sein.",
                    "Wert aus der Norm mit Quelle angeben (SN 592000, Gebäudeart) oder die Eigenschaft entfernen (null).");
            if (e.Eigenschaft("spezialinstallationen")?.Inhalt is JsonArray liste)
                foreach (var (n, i) in liste.Select((n, i) => (n, i)))
                    if (SanitaerZusammenstellung.SpezialFehler(n) is { } f)
                        throw new HlksFehler($"{e.Kennzeichen}: Spezialinstallation {i + 1} ungültig – {f}.",
                            "Form: [{ \"bezeichnung\": \"Getränkeautomat\", \"versorgung\": 0.1, \"entsorgung\": 0.1 }] (l/s, wie Excel Zeilen 46–51).");
        }
    }
}
