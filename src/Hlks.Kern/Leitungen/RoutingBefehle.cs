// Leitungen (Modul 3, B2; Abzweigwinkel B7): Befehle des Routings – leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse,
// leitung_serie (registriert in LeitungenBefehle.Registrieren). Geplant wird mit Routing.Planen, angelegt mit Routing.Umsetzen –
// alles in einem Befehl = einem Paket (Trockenlauf = Vorschau mit Übersicht in den Hinweisen, Rückgängig in einem Schritt).
// Der bestehende Befehl leitung_ziehen (Polylinie, Altbestand) bleibt unverändert.
// ANNAHMEN (Prüfliste):
//   - leitung_abzweigen: der Verlauf des Abzweigs (punkte bzw. richtung + laenge) wird vom T-Stück weg angegeben; die
//     Fliessrichtung folgt dem System (Verbraucher stromab: vom T-Stück weg, stromauf wie Schmutzwasser oder Rücklauf: zum
//     T-Stück hin). Ohne Angabe gehören Abzweig und T-Stück zu System und Strang der Leitung. Zwischen T-Stück und den Enden der
//     geteilten Leitung bleiben mindestens 1 mm Gerade.
//   - leitung_abzweigen mit Abzweigwinkel (B7, entwurf/modul-3.md «Lücke Abzweigwinkel»): gemessen wird zwischen der Leitung in
//     Fliessrichtung (anfang → ende) und dem Abzweig in seiner Fliessrichtung (zulaufend: zum T-Stück hin, zuführend: vom T-Stück
//     weg); erlaubt sind die Abzweigwinkel der Serie der Leitung ±Routing.WinkelToleranz, das T-Stück bekommt den Listenwinkel
//     (der Abzweig weicht um die Differenz von der Achse von c ab, verbunden wird bis 1°). Zulaufend liegt das T-Stück mit a→b in
//     Fliessrichtung (c zeigt dagegen, der Zulauf mündet in Fliessrichtung ein); zuführend unter w < 90° wird es gespiegelt
//     (Durchgang b → a), damit der Abzweig in Fliessrichtung abgeht – rechtwinklig wie bisher a → b.
//   - Mit gefaelle und waagrecht gezeichnetem erstem Abzweigstück (Höhe wie der Abzweigpunkt) wird die Abzweigrichtung um
//     arctan(gefaelle/100) geneigt (zulaufend steigend vom T-Stück weg, zuführend fallend) – sie zählt so beim Abzweigwinkel, und das
//     T-Stück wird um die Leitungsachse gedreht, bis c in der Ebene von Leitung und Abzweig liegt (volle Ausrichtung). Die Höhen
//     der Abzweigpunkte gelten dann relativ zum Abzweigpunkt: das erste Stück beginnt auf der Höhe von c, die Höhen folgen aus dem
//     Gefälle ab c. Hauptfall: senkrechter Fallstrang mit Zulauf 2 % → 90° − arctan(0.02) = 88.854° → T-Stück 88.5°.
//   - leitung_groesse / leitung_serie mit leitungen (ohne zug): geändert werden die genannten Geraden mit den Formteilen zwischen
//     ihnen; eine Reduktion direkt daneben gehört dazu (wird ersetzt bzw. entfernt), ein Bogen am Rand bleibt (Reduktion davor).
//     Mit zug: der ganze Leitungszug (Leitungszug.cs), Bögen werden ersetzt, Reduktionen im Zug entfallen (leitung_groesse) bzw.
//     werden durch die der neuen Serie ersetzt (leitung_serie); an den Grenzen (T-Stück, Gerät) werden Reduktionen nach Bedarf
//     eingesetzt. Bestehende Elemente werden wiederverwendet (Kennzeichen bleiben), überzählige entfernt.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Gemeinsame Parameter und Hilfen der Routing-Befehle.</summary>
public static class RoutingBefehle
{
    public const string ParSerie = "Leitungsserie: Familie der Kategorie Rohr bzw. Kanal (Kennzeichen, Name oder id)";
    public const string ParGroesse = "Grösse der Serie: Typname (z. B. \"DN 20\", \"400x200\"), Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { \"dn\": 20 }, { \"d\": 160 } oder { \"b\": 400, \"h\": 200 } (mm); Abwasser-Serien mit Grösse d (leitung.einsatz abwasser) nach Aussendurchmesser, z. B. { \"d\": 110 } oder \"d 110\" (keine DN nötig); bei Kanalserien mit freier Grösse (leitung.frei) jede Grösse innerhalb von Grenzen und Schritt, z. B. { \"b\": 430, \"h\": 270 } oder \"430x270\" (die Typen sind dann Vorschläge, leitungsserie nennt Grenzen und Schritt)";
    public const string ParPunkte = "Verlauf in Fliessrichtung: [[x, y, z], …] in mm (Achse); jeder Knick wird ein Bogen der Serie";
    public const string ParVon = "Anschluss am Anfang: { element, anschluss } oder \"KENNZEICHEN.anschluss\" – der erste Punkt wird seine Lage (ohne Lage: logisch verbunden)";
    public const string ParNach = "Anschluss am Ende: { element, anschluss } oder \"KENNZEICHEN.anschluss\" – der letzte Punkt wird seine Lage (ohne Lage: logisch verbunden)";
    public const string ParGefaelle = "Gefälle in % (in Fliessrichtung fallend): waagrecht gezeichnete Stücke fallen ab dem ersten Punkt, senkrechte behalten ihre Länge";
    public const string ParSystem = "System (Kennzeichen); entfällt mit strang, fehlt beides, gilt das System des Anschlusses";
    public const string ParStrang = "Strang (Kennzeichen), zu dem Geraden und Formteile gehören (teilVon)";
    public const string ParGeschoss = "Geschoss für alle neuen Elemente; fehlt es, aus der Höhe";
    public const string ParPraefix = "Präfix der Kennzeichen der Geraden, z. B. \"KW-V1\" → KW-V1-1, KW-V1-2 …; fehlt es: RO-n bzw. KA-n";
    public const string ParEigenschaften = "Weitere Eigenschaften der Geraden, z. B. { \"material\": \"Edelstahl\", \"daemmdicke\": 20 }";
    public const string ParLeitung = "Leitung (Rohr, Kanal; Kennzeichen oder Id)";
    public const string ParLeitungen = "Leitungen (Kennzeichen oder Ids), z. B. [\"RO-1\", \"RO-2\"]";
    public const string ParZug = "true: der ganze Leitungszug der Leitung (über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät oder offenen Ende)";

    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Grösse einer Serie aus Typname, Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { dn } | { d } |
    /// { b, h }; bei Abwasser-Serien nach Aussendurchmesser auch «d 110»; bei freien Kanalserien auch «430x270» bzw. «Ø 250» und jede Grösse
    /// innerhalb von Grenzen und Schritt (Rastertyp hat Vorrang, sonst virtuelle Grösse). Abgelehnt mit Grund (Grenzen, Schritt) und
    /// Vorschlag (gerundete Grösse, nächste Normgrössen). Eine DN wird nie in einen Aussendurchmesser umgerechnet.</summary>
    public static SerienGroesse Groesse(Leitungsserie s, JsonNode? n, string wer = "groesse")
    {
        if (n is null) throw new HlksFehler($"{wer} fehlt.", $"Grösse angeben: {s.GroessenText} ({ParGroesse}).");
        Leitungsgroesse? ausText = null;
        if (n is JsonValue v && v.TryGetValue<string>(out var t))
        {
            if (s.Groesse(t.Trim()) is { Gueltig: true } sg) return sg;
            if (double.TryParse(t.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl)) n = JsonValue.Create(zahl);
            else if ((s.Frei is not null || s.NachAussendurchmesser) && Leitungsgroesse.AusText(t) is { } gt) ausText = gt;
            else throw new HlksFehler($"Die Serie {s.Bezeichnung} hat keinen Typ «{t}».", $"Typen: {string.Join(", ", s.Groessen.Where(x => x.Gueltig).Select(x => x.Typ))}."
                + (s.Frei is null ? "" : $" Freie Grösse als {s.GroessenForm} bzw. «430x270» – {s.FreiText}.")
                + (s.NachAussendurchmesser ? $" Grösse nach Aussendurchmesser als {s.GroessenForm}." : ""));
        }
        // Zahl: Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d
        var g = s.Norm(ausText ?? (n is JsonValue zv && Json.Zahl(zv) is double z && s.Block.Groesse.ContainsKey("d") ? Leitungsgroesse.Lesen(new JsonObject { ["d"] = z }, wer) : Leitungsgroesse.Lesen(n, wer)));
        if (!s.PasstArt(g))
            throw new HlksFehler($"Die Grösse {g} passt nicht zur Serie {s.Bezeichnung} ({s.Kategorie}, {s.Querschnitt}{(s.NachAussendurchmesser ? ", Abwasser-Serie nach Aussendurchmesser" : "")}).",
                "Grösse angeben als " + s.GroessenForm + "." + (s.NachAussendurchmesser && g.Dn is not null ? " Eine DN wird nie in einen Aussendurchmesser umgerechnet." : ""));
        return s.Typ(g);
    }

    public static Element? Geschoss(Kontext k, JsonObject p) =>
        Param.Text(p, "geschoss") is { } g ? Elementpflege.BezugPruefen(k, g, "Geschoss", "Geschoss") : null;

    /// <summary>Ein Punkt [x, y] bzw. [x, y, z] (z fehlt = null).</summary>
    public static (double X, double Y, double? Z)? Punkt(JsonObject p, string name)
    {
        if (p[name] is null) return null;
        if (p[name] is not JsonArray a || a.Count is < 2 or > 3)
            throw new HlksFehler($"Parameter «{name}» ist kein Punkt.", "Punkt als [x, y, z] in mm angeben.");
        var z = a.Select(c => Json.Zahl(c) is double x && double.IsFinite(x) ? x : throw new HlksFehler($"{name}: eine Koordinate ist keine Zahl.", "Punkt als [x, y, z] in mm angeben.")).ToArray();
        return (z[0], z[1], z.Length > 2 ? z[2] : null);
    }

    /// <summary>Ende eines Verlaufs an einem Anschluss (Parameter { element, anschluss } bzw. "KZ.anschluss"); null = nicht angegeben.</summary>
    public static RoutingEnde? Ende(Kontext k, JsonObject p, string name, bool anfang, double[]? naheBei)
    {
        if (Param.Punkt(p, name) is not { } pt) return null;
        return Ende(k, k.Arbeit.Hole(pt.Element, name), pt.Anschluss, anfang, naheBei, name);
    }

    /// <summary>Ende an einem Anschluss eines Elements; ohne Namen der einzige freie, passende Anschluss (bei mehreren der bis 1 mm
    /// bei naheBei). anfang: der Anschluss speist den Verlauf (nicht «rein»), sonst nimmt er ihn auf (nicht «raus»).</summary>
    public static RoutingEnde Ende(Kontext k, Element e, string? anschluss, bool anfang, double[]? naheBei, string wer)
    {
        var stellen = Anschlusslagen.Von(k.Arbeit, k.Katalog, e);
        string Liste() => string.Join(", ", stellen.Select(s => $"{s.Name} ({s.Medium}, {s.Fluss})"));
        AnschlussStelle st;
        if (anschluss is not null)
            st = stellen.FirstOrDefault(s => s.Name.Equals(anschluss, StringComparison.OrdinalIgnoreCase))
                ?? throw new HlksFehler($"{e.Kennzeichen} hat keinen Anschluss «{anschluss}».", "Anschlüsse: " + Liste());
        else
        {
            var frei = stellen.Where(s => k.Arbeit.VerbindungAn(e.Id, s.Name) is null && s.Fluss != (anfang ? "rein" : "raus")).ToList();
            if (frei.Count > 1 && naheBei is not null)
            {
                var nah = frei.Where(s => s.HatLage && Geometrie.Abstand(s.Lage!, naheBei) <= Anschlusslagen.AbstandMax).ToList();
                if (nah.Count == 1) frei = nah;
            }
            if (frei.Count != 1)
                throw new HlksFehler(frei.Count == 0 ? $"{e.Kennzeichen} hat keinen freien, passenden Anschluss für {wer}." : $"{e.Kennzeichen} hat mehrere freie Anschlüsse ({string.Join(", ", frei.Select(s => s.Name))}).",
                    $"Anschluss angeben: {{ \"element\": \"{e.Kennzeichen}\", \"anschluss\": \"…\" }} – Anschlüsse: {Liste()}; anschlusslagen zeigt Lage und Richtung.");
            st = frei[0];
        }
        if (k.Arbeit.VerbindungAn(e.Id, st.Name) is not null)
            throw new HlksFehler($"{e.Kennzeichen}.{st.Name} ist schon verbunden.", "Einen freien Anschluss wählen oder zuerst trennen (trennen).");
        return new RoutingEnde(e, st);
    }

    /// <summary>Zuordnung neuer Elemente: system bzw. strang aus den Parametern, sonst das System von «partner».</summary>
    public static RoutingZiel Ziel(Kontext k, JsonObject p, Element? geschoss, Element? partner, Element? vorlage = null)
    {
        string? strang = Param.Text(p, "strang") is { } st ? Elementpflege.BezugPruefen(k, st, "Strang", "Strang").Id : null;
        string? system = Param.Text(p, "system") is { } sy ? Elementpflege.BezugPruefen(k, sy, "System", "System").Id : null;
        if (strang is not null) system ??= k.Arbeit.Finde(strang)!.System;
        if (system is null && strang is null && vorlage is not null) { system = vorlage.System; strang = vorlage.TeilVon is { } tv && k.Arbeit.Finde(tv)?.Klasse == "Strang" ? tv : null; }
        system ??= partner?.System;
        if (system is null)
            throw new HlksFehler("System fehlt.", "system oder strang angeben. Systeme: " + string.Join(", ", k.Arbeit.DerKlasse("System").Select(x => x.Kennzeichen).Order()));
        var praefix = Param.Text(p, "praefix")?.Trim();
        var gf = Param.Zahl(p, "gefaelle");
        return new RoutingZiel
        {
            System = system, Strang = strang, Geschoss = geschoss?.Id, Praefix = string.IsNullOrEmpty(praefix) ? null : praefix,
            Gefaelle = gf, Eigenschaften = p["eigenschaften"] as JsonObject,
        };
    }

    /// <summary>Leitungen aus «leitung» und «leitungen» (mindestens eine).</summary>
    public static List<Element> Leitungen(Kontext k, JsonObject p)
    {
        var liste = new List<Element>();
        if (Param.Text(p, "leitung") is { } l) liste.Add(Elementpflege.BezugPruefen(k, l, "Leitung", "Leitung"));
        if (p["leitungen"] is JsonNode n)
        {
            if (n is not JsonArray a) throw new HlksFehler("Parameter «leitungen» muss eine Liste sein.", ParLeitungen);
            foreach (var x in a)
                liste.Add(Elementpflege.BezugPruefen(k, x is JsonValue v && v.TryGetValue<string>(out var s) ? s : x?.ToJsonString(), "Leitung", "Leitung"));
        }
        if (liste.Count == 0) throw new HlksFehler("leitung bzw. leitungen fehlt.", "Eine Leitung (leitung) oder eine Liste (leitungen) angeben.");
        return liste.DistinctBy(e => e.Id).ToList();
    }

    /// <summary>Partner am Anschluss einer Leitung als Ende (oder null).</summary>
    public static RoutingEnde? Partner(Modell m, Katalog k, Element e, string anschluss)
    {
        if (m.VerbindungAn(e.Id, anschluss)?.Gegenueber(e.Id, anschluss) is not { } g || m.Finde(g.Element) is not { } x) return null;
        return Anschlusslagen.Stelle(m, k, x, g.Anschluss) is { } s ? new RoutingEnde(x, s) : null;
    }

    static void Trennen(Kontext k, Element e)
    {
        foreach (var v in k.Arbeit.VerbindungenVon(e.Id).ToList()) k.Trennen(v);
    }

    // ------------------------------------------------------------------ Neuaufbau eines Zugs (leitung_groesse, leitung_serie)

    sealed record Lauf(List<ZugGlied> Glieder, ZugGrenze? Anfang, ZugGrenze? Ende);

    /// <summary>Die zu ändernden Läufe: mit zug der ganze Leitungszug, sonst die genannten Geraden (siehe Dateikopf).</summary>
    static List<Lauf> Laeufe(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var leitungen = Leitungen(k, p);
        bool zug = Param.Bool(p, "zug");
        var gewaehlt = leitungen.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var erledigt = new HashSet<string>(StringComparer.Ordinal);
        var laeufe = new List<Lauf>();
        bool IstLeitung(ZugGlied g) => kat.IstArt(g.Element, "Leitung");
        foreach (var e in leitungen)
        {
            if (erledigt.Contains(e.Id)) continue;
            var z = Leitungszug.Finden(m, kat, e);
            if (z.Ring)
                throw new HlksFehler($"Der Leitungszug von {e.Kennzeichen} ist ein Ring ohne Ende.", "Die Leitungen einzeln (leitungen, ohne zug) angeben.");
            foreach (var g in z.Glieder) erledigt.Add(g.Element.Id);
            if (zug) { laeufe.Add(new Lauf(z.Glieder, z.Anfang, z.Ende)); continue; }
            var gl = z.Glieder;
            for (int i = 0; i < gl.Count;)
            {
                if (!(IstLeitung(gl[i]) && gewaehlt.Contains(gl[i].Element.Id))) { i++; continue; }
                int a = i, b = i;
                for (int j = i + 1; j < gl.Count && !(IstLeitung(gl[j]) && !gewaehlt.Contains(gl[j].Element.Id)); j++)
                    if (IstLeitung(gl[j])) b = j;
                while (a > 0 && kat.IstArt(gl[a - 1].Element, "Reduktion")) a--;
                while (b < gl.Count - 1 && kat.IstArt(gl[b + 1].Element, "Reduktion")) b++;
                var anfang = a > 0 ? new ZugGrenze(gl[a - 1].Element, gl[a - 1].Aus) : z.Anfang;
                var ende = b < gl.Count - 1 ? new ZugGrenze(gl[b + 1].Element, gl[b + 1].Ein) : z.Ende;
                laeufe.Add(new Lauf(gl.GetRange(a, b - a + 1), anfang, ende));
                i = b + 1;
            }
        }
        return laeufe;
    }

    /// <summary>Baut einen Lauf mit neuer Serie (leitung_serie) bzw. neuer Grösse (leitung_groesse) neu auf: Knoten = Bögen, Lage der
    /// Grenzen bleibt; Formteile werden ersetzt, Geraden neu gekürzt.</summary>
    static void Neuaufbau(Kontext k, string befehl, Lauf lauf, Leitungsserie? neueSerie, JsonNode? neueGroesse)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var leitungen = lauf.Glieder.Where(g => kat.IstArt(g.Element, "Leitung")).Select(g => g.Element).ToList();
        if (leitungen.Count == 0) throw new HlksFehler("Im Lauf gibt es keine Leitung.", "Eine Leitung angeben.");
        foreach (var l in leitungen)
            if (Leitungspruefungen.Knicke(l).Anzahl > 0 || l.Geometrie is not { Punkte.Count: >= 2 })
                throw new HlksFehler($"{l.Kennzeichen} ist keine Gerade (Polylinie mit Knick bzw. ohne Verlauf).", $"Zuerst leitung_aufteilen {{ \"leitung\": \"{l.Kennzeichen}\" }} – dann die Grösse bzw. Serie ändern.");
        Leitungsserie s;
        if (neueSerie is not null) s = neueSerie;
        else
        {
            var serien = leitungen.Select(l => (L: l, S: Leitungsserie.VonLeitung(m, kat, l))).ToList();
            if (serien.FirstOrDefault(x => x.S is null) is { L: { } ohne })
                throw new HlksFehler($"{ohne.Kennzeichen} hat keine Serie.", $"Zuerst eine Serie zuweisen: leitung_aufteilen {{ \"leitung\": \"{ohne.Kennzeichen}\", \"serie\": … }} bzw. leitung_serie.");
            if (serien.Select(x => x.S!.Familie.Id).Distinct().Count() > 1)
                throw new HlksFehler($"Im Zug gibt es verschiedene Serien ({string.Join(", ", serien.Select(x => x.S!.Familie.Kennzeichen).Distinct())}).", "Zuerst mit leitung_serie eine Serie für den ganzen Zug setzen.");
            s = serien[0].S!;
        }
        foreach (var l in leitungen)
            if (!kat.IstArt(l, s.Kategorie))
                throw new HlksFehler($"{l.Kennzeichen} ist ein {l.Klasse}, die Serie {s.Bezeichnung} gehört zu {s.Kategorie}.", $"Eine Serie der Kategorie {l.Klasse} wählen.");
        Leitungsgroesse? gNeu = neueGroesse is null ? null : Groesse(s, neueGroesse).Groesse;

        AnschlussStelle Stelle(Element e, string an) => Anschlusslagen.Stelle(m, kat, e, an)
            ?? throw new HlksFehler($"{e.Kennzeichen} hat keinen Anschluss «{an}».", "Verbindungen prüfen (P08).");
        V3 Aussen(ZugGlied g, bool amAnfang)
        {
            var e = g.Element;
            if (kat.IstArt(e, "Leitung"))
            {
                var pts = e.Geometrie!.Punkte;
                bool einIstAnfang = g.Ein.Equals("anfang", StringComparison.OrdinalIgnoreCase);
                return V3.Aus(amAnfang == einIstAnfang ? pts[0] : pts[^1]);
            }
            if (Stelle(e, amAnfang ? g.Ein : g.Aus) is { HatLage: true } st) return V3.Aus(st.Lage!);
            return V3.Aus(Familien.Einfuegepunkt(e) ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Lage.", "Formteil platzieren (familie_platzieren)."));
        }
        RoutingEnde? Grenze(ZugGrenze? z) => z is null ? null : new RoutingEnde(z.Element, Stelle(z.Element, z.Anschluss));
        var von = Grenze(lauf.Anfang); var nach = Grenze(lauf.Ende);

        var pkt = new List<V3> { von is { HatLage: true } ? von.Lage : Aussen(lauf.Glieder[0], true) };
        var wechsel = new List<Groessenwechsel>();
        var geraden = new List<(Element, int)>();
        var formteile = new List<Element>();
        int abschnitt = 0;
        Leitungsgroesse? g0 = gNeu;
        // Grösse in der Art der Serie: eine Abwasser-Serie nach Aussendurchmesser nimmt Leitungen mit DN über ihren Aussendurchmesser
        // (Typ ihrer bisherigen Serie bzw. Leitung.da) – nie aus der DN gerechnet; sonst wie bisher
        Leitungsgroesse GroesseIn(Element leitung) => s.GroesseFuer(m, kat, leitung);
        // eine Reduktion der bisherigen Serie mit DN-Grössen: die Grösse danach ist die der nächsten Leitung im Lauf
        Leitungsgroesse NachReduktion(int i, Leitungsgroesse aus) =>
            s.PasstArt(aus) || lauf.Glieder.Skip(i + 1).FirstOrDefault(x => kat.IstArt(x.Element, "Leitung")) is not { } n ? aus : GroesseIn(n.Element);
        for (int gi = 0; gi < lauf.Glieder.Count; gi++)
        {
            var gl = lauf.Glieder[gi];
            var e = gl.Element;
            if (kat.IstArt(e, "Leitung"))
            {
                geraden.Add((e, abschnitt));
                if (g0 is null && GroesseIn(e) is { Leer: false } lg) g0 = lg;
            }
            else if (kat.IstArt(e, "Bogen"))
            {
                pkt.Add(V3.Aus(Familien.Einfuegepunkt(e) ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Lage.", "Bogen platzieren bzw. entfernen.")));
                formteile.Add(e);
                if (g0 is null && Stelle(e, gl.Ein) is { HatGroesse: true } bs && s.PasstArt(bs.Groesse)) g0 = bs.Groesse;
                abschnitt++;
            }
            else
            {
                formteile.Add(e);
                if (gNeu is not null) continue;   // leitung_groesse: Reduktionen im Lauf entfallen
                var ein = Stelle(e, gl.Ein); var aus = Stelle(e, gl.Aus);
                if (!ein.HatLage || !ein.HatGroesse || !aus.HatGroesse)
                    throw new HlksFehler($"Die Reduktion {e.Kennzeichen} hat keine Familie (keine Lage bzw. Grösse) – ihr Ort im Zug ist unklar.", "Reduktion aus einer Formteil-Familie setzen oder den Zug mit leitung_groesse auf eine Grösse bringen.");
                if (s.PasstArt(ein.Groesse)) g0 ??= ein.Groesse;
                wechsel.Add(new Groessenwechsel(abschnitt, (V3.Aus(ein.Lage!) - pkt[^1]).Laenge, NachReduktion(gi, aus.Groesse)));
            }
        }
        pkt.Add(nach is { HatLage: true } ? nach.Lage : Aussen(lauf.Glieder[^1], false));
        if (g0 is not { } start) throw new HlksFehler("Die Grösse am Anfang des Zugs ist unbekannt.", "Grösse angeben (leitung_groesse).");
        // ein Bogen am offenen Ende (bzw. vor einem Gerät ohne Lage) bleibt ohne Gerade davor bzw. danach
        bool IstBogen(ZugGlied g) => kat.IstArt(g.Element, "Bogen");
        var plan = Routing.Planen(m, kat, s, start, pkt, von, nach, new PlanOptionen
        {
            Wechsel = wechsel,
            FreiAnfang = IstBogen(lauf.Glieder[0]) && von is not { HatLage: true },
            FreiEnde = IstBogen(lauf.Glieder[^1]) && nach is not { HatLage: true },
        });
        foreach (var gl in lauf.Glieder) Trennen(k, gl.Element);
        var erste = leitungen[0];
        var liste = Routing.Umsetzen(k, plan, new RoutingZiel { Vorlage = erste, Praefix = erste.Kennzeichen, AbZwei = true },
            new Wiederverwendung { Geraden = geraden, Formteile = formteile }, nachsichtigAnGrenzen: true);
        Routing.Uebersicht(k, befehl, plan, liste);
    }

    internal static void ZugAendern(Kontext k, JsonObject p, string befehl, Leitungsserie? serie, JsonNode? groesse)
    {
        var laeufe = Laeufe(k, p);
        if (laeufe.Count == 0) throw new HlksFehler("Keine Leitung zu ändern.", "leitung bzw. leitungen angeben.");
        foreach (var l in laeufe) Neuaufbau(k, befehl, l, serie, groesse);
    }
}

public sealed class LeitungZeichnen : IBefehl
{
    public string Name => "leitung_zeichnen";
    public const string Text = "Zeichnet eine Leitung wie in Revit (Modul 3): Serie (Leitungsserie = Familie Rohr bzw. Kanal) und Grösse wählen, Verlauf in Fliessrichtung angeben – je Gerade entsteht ein Rohr bzw. Kanal (zwei Punkte, Achse), am Knick ein Bogen der Serie (Winkel nach regeln.winkel, Toleranz ±0.5°; Steig- und Fallbögen voll ausgerichtet), an einem Anschluss anderer Grösse eine Reduktion; die Geraden enden an den Formteilen (um die Einbaulängen gekürzt) und alles wird streng verbunden. Mit von bzw. nach beginnt bzw. endet der Verlauf an diesem Anschluss (seine Lage wird übernommen; ohne Lage wird logisch verbunden). Mit gefaelle fallen waagrecht gezeichnete Stücke in Fliessrichtung, senkrechte behalten ihre Länge; endet bzw. beginnt der Verlauf an einem Anschluss, dessen Achse dem Gefälle folgt (z. B. am schrägen Abzweig c eines T-Stücks am Fallstrang), gilt das Stück daran als waagrecht gezeichnet, wenn sein Punkt zwischen der Waagrechten durch den Anschluss und dessen Achse liegt. Abgelehnt mit Vorschlag: Winkel nicht in der Serie, Gerade kürzer als die Einbaulängen, Formteil fehlt. Der Trockenlauf zeigt in den Hinweisen die Geraden mit Längen und die Formteile mit Typ und Winkel. Kanalserien mit freier Grösse (leitung.frei, z. B. Rechteckkanal nach Mass): jede Grösse b × h bzw. d innerhalb von Grenzen und Schritt (Normgrössen = Typen haben Vorrang, sonst familientyp = Vorlage und breite/hoehe = tatsächliche Grösse); die Formteile werden dann aus der Vorlage ihrer Familie mit den Grössen als Exemplarwerte gerechnet (Einbaulängen aus deren Formeln). Ausserhalb der Grenzen oder nicht im Schritt: abgelehnt mit gerundetem Vorschlag und Normgrössen. Abwasser-Serien mit Grösse d (leitung.einsatz abwasser): Grösse als Aussendurchmesser { \"d\": 110 } bzw. \"d 110\", keine DN nötig; die Formteile werden über d gewählt.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("serie", "text", true, RoutingBefehle.ParSerie),
        new("groesse", "objekt", true, RoutingBefehle.ParGroesse),
        new("punkte", "liste", true, RoutingBefehle.ParPunkte),
        new("von", "objekt", false, RoutingBefehle.ParVon),
        new("nach", "objekt", false, RoutingBefehle.ParNach),
        new("gefaelle", "zahl", false, RoutingBefehle.ParGefaelle),
        new("system", "text", false, RoutingBefehle.ParSystem),
        new("strang", "text", false, RoutingBefehle.ParStrang),
        new("geschoss", "text", false, RoutingBefehle.ParGeschoss),
        new("praefix", "text", false, RoutingBefehle.ParPraefix),
        new("eigenschaften", "objekt", false, RoutingBefehle.ParEigenschaften),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var s = Leitungsserie.Hole(k.Arbeit, k.Katalog, Param.Text(p, "serie", true));
        var sg = RoutingBefehle.Groesse(s, p["groesse"]);
        var roh = Param.Punkte(p, "punkte");
        var geschoss = RoutingBefehle.Geschoss(k, p);
        var von = RoutingBefehle.Ende(k, p, "von", true, roh.FirstOrDefault());
        var nach = RoutingBefehle.Ende(k, p, "nach", false, roh.LastOrDefault());
        if (von is not null && nach is not null && von.Element.Id == nach.Element.Id && von.Anschluss.Equals(nach.Anschluss, StringComparison.OrdinalIgnoreCase))
            throw new HlksFehler("von und nach sind derselbe Anschluss.", "Zwei verschiedene Anschlüsse angeben.");
        var ziel = RoutingBefehle.Ziel(k, p, geschoss, von?.Element ?? nach?.Element);
        var hinweise = new List<string>();
        var pkt = Routing.Vorbereiten(roh, geschoss?.Zahl("kote") ?? 0, von, nach, Param.Zahl(p, "gefaelle"), hinweise);
        var plan = Routing.Planen(k.Arbeit, k.Katalog, s, sg.Groesse, pkt, von, nach);
        var liste = Routing.Umsetzen(k, plan, ziel);
        k.Hinweise.AddRange(hinweise);
        Routing.Uebersicht(k, Name, plan, liste);
    }
}

public sealed class LeitungAbzweigen : IBefehl
{
    public string Name => "leitung_abzweigen";
    public const string Text = "Abzweig an einer bestehenden Geraden (Modul 3): die Gerade wird am Punkt geteilt (Punkt auf die Achse projiziert), ein T-Stück der Hauptgrösse eingesetzt (beide Teilstücke um seine Einbaulängen gekürzt, die Verbindung am Ende umgehängt) und der Abzweig ab Anschluss c wie leitung_zeichnen gezeichnet – kleinerer Abzweig mit Reduktion an c, wenn die Serie kein passendes T-Stück hat. Der Abzweig wird vom T-Stück weg angegeben: punkte, oder richtung mit laenge (ohne laenge nur das T-Stück). Abzweigwinkel = Winkel zwischen der Leitung in Fliessrichtung und dem Abzweig in seiner Fliessrichtung; erlaubt sind die Abzweigwinkel der Serie (regeln.abzweigwinkel, ohne Angabe nur 90°) ±0.5°, das T-Stück mit diesem Winkel wird gewählt. Fliessrichtung nach dem System: Entsorgung und Rücklauf laufen dem T-Stück zu (ein schräger Zulauf mündet in Fliessrichtung ein, der Abzweig zeigt also gegen die Fliessrichtung der Leitung), Versorgung und Vorlauf gehen vom T-Stück weg (schräg: T-Stück gespiegelt, Abzweig in Fliessrichtung). Mit gefaelle fällt ein waagrecht gezeichneter Abzweig in seiner Fliessrichtung (Abwasser: zum T-Stück hin); seine Neigung zählt beim Abzweigwinkel mit und das T-Stück wird um die Leitungsachse gedreht, bis c in der Ebene des Abzweigs liegt – z. B. Zulauf mit 2 % an einen senkrechten Fallstrang: 90° − arctan(0.02) = 88.85° → T-Stück 88.5°. Zu nah an einem Ende (T-Stück passt nicht) oder nicht erlaubter Winkel wird abgelehnt (mit den erlaubten Winkeln). Bei Kanalserien mit freier Grösse (leitung.frei) darf der Abzweig jede Grösse innerhalb von Grenzen und Schritt haben; passt kein T-Stück-Typ, wird die Vorlage der T-Stück-Familie mit Durchgang und Abzweig als Exemplarwerte gerechnet.";
    public string Beschreibung => Text;
    public const string ParPunkt = "Punkt des Abzweigs [x, y, z] in mm auf der Achse der Leitung (wird projiziert)";
    public const string ParRichtung = "Richtung des Abzweigs vom T-Stück weg [dx, dy, dz], unter einem erlaubten Abzweigwinkel der Serie (Vorgabe rechtwinklig), statt punkte";
    public const string ParLaenge = "Länge des Abzweigs ab der Achse der Leitung in mm (mit richtung); ohne laenge nur das T-Stück";
    public const string ParAbzweigPunkte = "Verlauf des Abzweigs vom T-Stück weg: [[x, y, z], …] in mm (der erste Punkt unter einem erlaubten Abzweigwinkel neben der Leitung, Vorgabe rechtwinklig; mit gefaelle waagrecht auf der Höhe des Abzweigpunkts)";
    public const string ParAbzweigGroesse = "Grösse des Abzweigs (Vorgabe: Grösse der Leitung) – Typname, Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { dn } | { d } | { b, h }; bei freien Kanalserien jede Grösse innerhalb von Grenzen und Schritt, z. B. { \"b\": 250, \"h\": 200 }";
    public const string ParAbzweigSerie = "Serie des Abzweigs (Vorgabe: Serie der Leitung)";
    public const string ParEnde = "Anschluss am anderen Ende des Abzweigs: { element, anschluss } oder \"KENNZEICHEN.anschluss\"";
    public const string ParHaupt = "Gerade, an der abgezweigt wird (Kennzeichen oder Id)";
    public const string ParSystemAbzweig = "System des Abzweigs (Kennzeichen); Vorgabe: System der Leitung";
    public const string ParStrangAbzweig = "Strang des Abzweigs (Kennzeichen); Vorgabe: Strang der Leitung";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", true, ParHaupt),
        new("punkt", "liste", true, ParPunkt),
        new("richtung", "liste", false, ParRichtung),
        new("laenge", "zahl", false, ParLaenge),
        new("punkte", "liste", false, ParAbzweigPunkte),
        new("groesse", "objekt", false, ParAbzweigGroesse),
        new("serie", "text", false, ParAbzweigSerie),
        new("ende", "objekt", false, ParEnde),
        new("gefaelle", "zahl", false, RoutingBefehle.ParGefaelle),
        new("system", "text", false, ParSystemAbzweig),
        new("strang", "text", false, ParStrangAbzweig),
        new("geschoss", "text", false, RoutingBefehle.ParGeschoss),
        new("praefix", "text", false, RoutingBefehle.ParPraefix),
    ];

    static string Z(double x, int stellen = 1) => Math.Round(x, stellen).ToString("0.###", CultureInfo.InvariantCulture);

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var haupt = Elementpflege.BezugPruefen(k, Param.Text(p, "leitung", true), "Leitung", "Leitung");
        var hp = haupt.Geometrie?.Punkte;
        if (hp is null || hp.Count < 2) throw new HlksFehler($"{haupt.Kennzeichen} hat keinen Verlauf.", "Eine gezeichnete Leitung angeben.");
        if (hp.Count > 2)
            throw new HlksFehler($"{haupt.Kennzeichen} ist eine Polylinie mit {hp.Count} Punkten – abgezweigt wird an einer Geraden.", $"Zuerst leitung_aufteilen {{ \"leitung\": \"{haupt.Kennzeichen}\" }}, dann an der passenden Geraden abzweigen.");
        var sm = Leitungsserie.VonLeitung(m, kat, haupt)
            ?? throw new HlksFehler($"{haupt.Kennzeichen} hat keine Serie – das T-Stück kommt aus der Serie der Leitung.", $"Zuerst eine Serie zuweisen: leitung_aufteilen {{ \"leitung\": \"{haupt.Kennzeichen}\", \"serie\": … }}.");
        var gm = Anschlusslagen.LeitungGroesse(m, kat, haupt);
        if (sm.TypFuer(gm, out var grundHaupt) is null)
            throw new HlksFehler(sm.Frei is null ? $"{haupt.Kennzeichen} hat die Grösse {gm}, die es in der Serie {sm.Bezeichnung} nicht gibt." : $"{haupt.Kennzeichen}: {grundHaupt}",
                $"Grösse angleichen (leitung_groesse); Grössen: {sm.GroessenText}.");
        var A = V3.Aus(hp[0]); var B = V3.Aus(hp[1]);
        double L = (B - A).Laenge;
        if (L <= Formteilgeometrie.LageToleranz) throw new HlksFehler($"{haupt.Kennzeichen} hat die Länge 0.", "Leitung neu zeichnen.");
        var d = (B - A).Einheit();

        // Punkt auf die Achse projizieren (ohne z über die Lage im Grundriss)
        var pt = RoutingBefehle.Punkt(p, "punkt") ?? throw new HlksFehler("Parameter «punkt» fehlt.", ParPunkt);
        double t;
        if (pt.Z is double qz) t = (new V3(pt.X, pt.Y, qz) - A).Mal(d);
        else
        {
            double dxy = d.X * d.X + d.Y * d.Y;
            if (dxy < 1e-12) throw new HlksFehler($"{haupt.Kennzeichen} ist senkrecht – der Punkt braucht z.", "Punkt als [x, y, z] angeben.");
            t = ((pt.X - A.X) * d.X + (pt.Y - A.Y) * d.Y) / dxy;
        }
        var q = A + d * t;
        var qEin = new V3(pt.X, pt.Y, pt.Z ?? q.Z);
        if (t <= 0 || t >= L)
            throw new HlksFehler($"Der Punkt liegt nicht auf {haupt.Kennzeichen} (Projektion {Z(t)} mm ab dem Anfang, Länge {Z(L)} mm).", $"Einen Punkt zwischen {Routing.P(A)} und {Routing.P(B)} angeben.");
        var hinweise = new List<string>();
        if ((qEin - q).Laenge > Anschlusslagen.AbstandMax) hinweise.Add($"Punkt auf die Achse von {haupt.Kennzeichen} projiziert ({Z((qEin - q).Laenge)} mm daneben): {Routing.P(q)}.");

        // Abzweig: Serie, Grösse, Verlauf
        var sb = Param.Text(p, "serie") is { } sbz ? Leitungsserie.Hole(m, kat, sbz) : sm;
        var gb = p["groesse"] is { } gn ? RoutingBefehle.Groesse(sb, gn) : sb.TypFuer(gm, out var grundAbzweig)
            ?? throw new HlksFehler(grundAbzweig ?? $"Die Serie {sb.Bezeichnung} hat keine Grösse {gm}.", $"groesse angeben; Grössen: {sb.GroessenText}.");
        var geschoss = RoutingBefehle.Geschoss(k, p);
        var gefaelle = Param.Zahl(p, "gefaelle");
        if (gefaelle is double gf0 && (!double.IsFinite(gf0) || gf0 < 0))
            throw new HlksFehler($"gefaelle {Z(gf0, 3)} % ist ungültig.", "Gefälle in % ≥ 0 angeben (in Fliessrichtung fallend), z. B. 2.");

        // Fliessrichtung des Abzweigs (nach dem System): hin = der Abzweig läuft dem T-Stück zu (Schmutzwasser, Rücklauf)
        var ziel = RoutingBefehle.Ziel(k, p, geschoss, null, haupt);
        bool hin = m.Finde(ziel.System) is { } sys && Netz.VerbraucherRichtung(m, kat, sys) == "stromauf";

        var kurs = new List<V3>();
        V3 u;
        bool waagrecht;   // erstes Stück des Abzweigs waagrecht gezeichnet (für das Gefälle)
        double[]? fernNahe = null;
        if (p["punkte"] is not null)
        {
            kurs = Param.Punkte(p, "punkte").Select(x => new V3(x[0], x[1], x.Length > 2 ? x[2] : q.Z)).ToList();
            if (kurs.Count > 0 && (kurs[0] - q).Laenge <= Anschlusslagen.AbstandMax) kurs.RemoveAt(0);
            if (kurs.Count == 0) throw new HlksFehler("punkte des Abzweigs fehlen.", ParAbzweigPunkte);
            u = (kurs[0] - q).Einheit();
            waagrecht = Math.Abs(kurs[0].Z - q.Z) <= Anschlusslagen.AbstandMax && !Routing.Senkrecht(q, kurs[0]);
            fernNahe = kurs[^1].Feld();
        }
        else if (RoutingBefehle.Punkt(p, "richtung") is { } r)
        {
            u = new V3(r.X, r.Y, r.Z ?? 0);
            if (u.Laenge < 1e-9) throw new HlksFehler("richtung ist [0, 0, 0].", ParRichtung);
            u = u.Einheit();
            waagrecht = Math.Abs(u.Z) <= 1e-9;
            if (Param.Zahl(p, "laenge") is double lg)
            {
                if (!(lg > 0)) throw new HlksFehler($"laenge {Z(lg)} mm ist ungültig.", ParLaenge);
                kurs.Add(q + u * lg);
                fernNahe = kurs[^1].Feld();
            }
        }
        else throw new HlksFehler("Richtung des Abzweigs fehlt.", "punkte (Verlauf vom T-Stück weg) oder richtung (mit laenge) angeben.");

        // Mit Gefälle: ein waagrecht gezeichneter Abzweig fällt in seiner Fliessrichtung – zum T-Stück hin (zulaufend) bzw. vom
        // T-Stück weg (zuführend); seine Richtung u (vom T-Stück weg) wird um arctan(gefaelle/100) geneigt.
        bool geneigt = gefaelle is double gfn && gfn > 0 && waagrecht;
        if (geneigt)
        {
            var h = new V3(u.X, u.Y, 0).Einheit();
            double alpha = Math.Atan(gefaelle!.Value / 100);
            u = h * Math.Cos(alpha) + new V3(0, 0, hin ? Math.Sin(alpha) : -Math.Sin(alpha));
        }

        // Abzweigwinkel = Winkel zwischen dem Durchgang in Fliessrichtung (d) und dem Abzweig in seiner Fliessrichtung
        // (zulaufend −u, zuführend u); erlaubt sind die Winkel der Serie der Leitung (regeln.abzweigwinkel, fehlt = 90°) ±0.5°
        double gemessen = Math.Acos(Math.Clamp(d.Mal(hin ? -u : u), -1, 1)) * 180 / Math.PI;
        if (sm.AbzweigwinkelZu(gemessen) is not double w)
        {
            string richtungHinweis = gemessen > 90 + Routing.WinkelToleranz
                ? (hin ? " Ein Zulauf mündet in Fliessrichtung ein: der Abzweig muss gegen die Fliessrichtung der Leitung (anfang → ende) zeigen."
                       : " Ein Abzweig zweigt in Fliessrichtung ab: er muss in Fliessrichtung der Leitung (anfang → ende) zeigen.")
                : "";
            throw new HlksFehler($"Der Abzweig steht unter {Z(gemessen, 2)}° zu {haupt.Kennzeichen} (Winkel zwischen der Leitung in Fliessrichtung und dem Abzweig, der {(hin ? "zuläuft" : "abgeht")}{(geneigt ? $", mit {Z(gefaelle!.Value, 3)} % Gefälle" : "")}) – die Serie {sm.Bezeichnung} erlaubt Abzweigwinkel {sm.AbzweigwinkelText} (Toleranz ±{Z(Routing.WinkelToleranz)}°).{richtungHinweis}",
                "Den ersten Punkt des Abzweigs bzw. die Richtung so legen, dass der Winkel einem erlaubten Abzweigwinkel entspricht (mit gefaelle zählt die Neigung des Abzweigs mit); für einen anderen Verlauf danach einen Bogen setzen (weiterer Punkt).");
        }
        // Zuführend und schräg: Spiegelbild – das T-Stück wird b → a durchflossen, damit c in Fliessrichtung abgeht
        bool spiegel = !hin && w < 90 - 1e-9;
        var tx = spiegel ? -d : d;

        // T-Stück: zuerst mit Abzweiggrösse, sonst der Hauptgrösse (Reduktion an c)
        var tw = sm.Formteil(m, kat, "tstueck", gm, gb.Groesse, w);
        if (!tw.Ok && !gb.Groesse.Gleich(gm) && sm.Formteil(m, kat, "tstueck", gm, gm, w) is { Ok: true } egal) tw = egal;
        if (!tw.Ok) throw new HlksFehler(tw.Fehler!, tw.Vorschlag);
        double la = tw.Geometrie!.Einbaulaenge("a"), lb = tw.Geometrie.Einbaulaenge("b");
        double lv = spiegel ? lb : la, ln = spiegel ? la : lb;   // vor (stromauf) bzw. nach (stromab) dem Abzweig
        string ein = spiegel ? "b" : "a", aus = spiegel ? "a" : "b";
        if (t - lv <= Anschlusslagen.AbstandMax || L - t - ln <= Anschlusslagen.AbstandMax)
            throw new HlksFehler($"Das T-Stück «{tw.Typ}» braucht {Z(lv)} mm vor und {Z(ln)} mm nach dem Abzweig; bei {Z(t)} mm ab dem Anfang von {haupt.Kennzeichen} (Länge {Z(L)} mm) passt es nicht.",
                $"Den Abzweig zwischen {Z(lv + Anschlusslagen.AbstandMax)} und {Z(L - ln - Anschlusslagen.AbstandMax)} mm ab dem Anfang setzen (z. B. bei {Routing.P(A + d * Math.Clamp(t, lv + 2, Math.Max(lv + 2, L - ln - 2)))}).");

        var fern = RoutingBefehle.Ende(k, p, "ende", hin, fernNahe);
        if (kurs.Count == 0 && fern is { HatLage: true }) kurs.Add(fern.Lage);

        // Leitung teilen
        ZugGrenze? endePartner = null;
        if (m.VerbindungAn(haupt.Id, "ende") is { } ve && ve.Gegenueber(haupt.Id, "ende") is { } gp && m.Finde(gp.Element) is { } pe)
        {
            endePartner = new ZugGrenze(pe, gp.Anschluss);
            k.Trennen(ve);
        }
        var h1 = haupt.Kopie();
        h1.Geometrie = new Geometrie { Art = "polylinie", Punkte = [Familien.Runden(A.Feld(), 6), Familien.Runden((A + d * (t - lv)).Feld(), 6)] };
        h1 = k.Setzen(h1);
        var h2 = haupt.Kopie();
        h2.Id = Guid.NewGuid().ToString(); h2.Kennzeichen = Routing.Teilstueck(k, haupt.Kennzeichen);
        h2.Version = 0; h2.Paket = null; h2.Angelegt = null; h2.Geaendert = null; h2.Kennungen = null;
        h2.Geometrie = new Geometrie { Art = "polylinie", Punkte = [Familien.Runden((A + d * (t + ln)).Feld(), 6), Familien.Runden(B.Feld(), 6)] };
        h2 = k.Setzen(h2);

        // T-Stück: x = Durchgang a→b (Leitung in Fliessrichtung, gespiegelt dagegen), y = Abzweig – um die Durchgangsachse so
        // gedreht, dass c in der Ebene von Leitung und Abzweig liegt: c = −cos w·x + sin w·y
        var (ax, az) = Formteilgeometrie.Ausrichtung(tx, u);
        var tPlan = new PlanFormteil { Wahl = tw, Ursprung = q, X = ax, Z = az, EinName = ein, AusName = aus, EinLage = q - d * lv, AusLage = q + d * ln };
        var tEl = Routing.FormteilSetzen(k, tPlan, new RoutingZiel { Vorlage = haupt, Geschoss = geschoss?.Id }, null);
        Routing.Anschliessen(k, h1, "ende", tEl, ein, false);
        Routing.Anschliessen(k, tEl, aus, h2, "anfang", false);
        if (endePartner is not null) Routing.Anschliessen(k, h2, "ende", endePartner.Element, endePartner.Anschluss, true);
        string winkelText = Math.Abs(w - 90) > 1e-9 || Math.Abs(gemessen - w) > 1e-3
            ? $", Abzweigwinkel {Z(w, 3)}°" + (Math.Abs(gemessen - w) > 1e-3 ? $" (Verlauf {Z(gemessen, 3)}°)" : "") + (spiegel ? ", gespiegelt (Durchgang b → a, Abzweig in Fliessrichtung)" : "")
            : "";
        k.Hinweise.Add($"leitung_abzweigen: {haupt.Kennzeichen} geteilt in {h1.Kennzeichen} ({Z(t - lv)} mm) und {h2.Kennzeichen} ({Z(L - t - ln)} mm); T-Stück {tEl.Kennzeichen} Typ «{tw.Typ}»{(tw.Frei ? $" frei {tw.GroessenText}" : "")} bei {Routing.P(q)}{winkelText}, Einbaulängen a {Z(la)} mm, b {Z(lb)} mm, c {Z(tw.Geometrie.Einbaulaenge("c"))} mm.");

        // Abzweig ab c
        var c = Anschlusslagen.Stelle(m, kat, tEl, "c") ?? throw new HlksFehler("Das T-Stück hat keinen Anschluss c.", Formteilgeometrie.LageVorschlag);
        var tEnde = new RoutingEnde(tEl, c);
        if (geneigt)
        {
            var cr = tEnde.Richtung;
            double crh = Math.Sqrt(cr.X * cr.X + cr.Y * cr.Y);
            if (crh > 1e-9)
                hinweise.Add($"Abzweig {tEl.Kennzeichen}.c: Achse {Z(Math.Abs(cr.Z) / crh * 100, 3)} % gegen die Waagrechte geneigt, Abzweigleitung {Z(gefaelle!.Value, 3)} % (Abweichung {Z(Math.Abs(gemessen - w), 3)}°, verbunden wird bis {Z(Anschlusslagen.WinkelMax)}°).");
        }
        if (kurs.Count > 0)
        {
            // Mit Gefälle beziehen sich die Höhen der Abzweigpunkte auf den Abzweigpunkt q auf der Achse: ein waagrecht gezeichnetes
            // erstes Stück beginnt auf der Höhe von c (dort ist der Abzweig gebunden) und fällt bzw. steigt von dort mit dem Gefälle
            // (ein gebundenes Ende bleibt, wo es ist)
            if (geneigt)
            {
                double dz = c.Lage![2] - q.Z;
                kurs = kurs.Select(x => fern is { HatLage: true } && (x - fern.Lage).Laenge <= Anschlusslagen.AbstandMax ? x : new V3(x.X, x.Y, x.Z + dz)).ToList();
            }
            var roh = new List<double[]> { c.Lage! };
            roh.AddRange(kurs.Select(x => x.Feld()));
            RoutingEnde? von = tEnde, nach = fern;
            if (hin) { roh.Reverse(); (von, nach) = (fern, tEnde); }
            var pkt = Routing.Vorbereiten(roh, q.Z, von, nach, gefaelle, hinweise);
            var plan = Routing.Planen(m, kat, sb, gb.Groesse, pkt, von, nach);
            var liste = Routing.Umsetzen(k, plan, ziel);
            k.Hinweise.AddRange(hinweise);
            Routing.Uebersicht(k, "Abzweig", plan, liste);
        }
        else
        {
            k.Hinweise.AddRange(hinweise);
            if (!gb.Groesse.Gleich(c.Groesse) && c.HatGroesse)
            {
                var red = sm.Formteil(m, kat, "reduktion", c.Groesse, gb.Groesse);
                if (!red.Ok) throw new HlksFehler(red.Fehler!, red.Vorschlag);
                var cl = V3.Aus(c.Lage!); var cr = V3.Aus(c.Richtung!).Einheit();
                var plan = Routing.Planen(m, kat, sb, gb.Groesse, [cl, cl + cr * red.Geometrie!.Einbaulaenge("b")], tEnde, null);
                var liste = Routing.Umsetzen(k, plan, ziel);
                Routing.Uebersicht(k, "Abzweig (nur Reduktion an c)", plan, liste);
            }
            else k.Hinweise.Add($"Anschluss {tEl.Kennzeichen}.c ist frei – der Abzweig wird mit leitung_zeichnen von {{ \"element\": \"{tEl.Kennzeichen}\", \"anschluss\": \"c\" }} gezeichnet.");
        }
    }
}

public sealed class LeitungAufteilen : IBefehl
{
    public string Name => "leitung_aufteilen";
    public const string Text = "Zerlegt bestehende Leitungen (Polylinien des Altbestands) in Geraden mit Bögen der Serie (Modul 3): je Gerade ein Element (die Leitung selbst bleibt das erste Stück, weitere heissen «<kennzeichen>-2», «-3» …), am Knick ein Bogen nach regeln.winkel (Toleranz ±0.5°). Serie und Grösse aus der Leitung (bezuege.familie, familientyp bzw. dn) oder aus den Parametern. Die Verbindungen an Anfang und Ende bleiben (an die neuen Enden umgehängt, Lage geprüft; passt sie nicht, bleibt sie logisch und P13/P14 melden es). Eine Leitung mit freier Grösse (Kanalserie mit leitung.frei) behält ihre tatsächliche Grösse (breite/hoehe bzw. durchmesser).";
    public string Beschreibung => Text;
    public const string ParSerie = "Leitungsserie (Kennzeichen, Name oder id); Vorgabe: die Serie der Leitung";
    public const string ParGroesse = "Grösse der Serie (Vorgabe: Grösse der Leitung – familientyp bzw. bei freier Grösse breite/hoehe bzw. durchmesser, sonst dn; in eine Abwasser-Serie nach Aussendurchmesser über den Aussendurchmesser der Leitung, z. B. da aus dem IFC-Import, nie aus der DN gerechnet) – Typname, Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { dn } | { d } | { b, h }; bei freien Kanalserien jede Grösse innerhalb von Grenzen und Schritt";
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", false, RoutingBefehle.ParLeitung),
        new("leitungen", "liste", false, RoutingBefehle.ParLeitungen),
        new("serie", "text", false, ParSerie),
        new("groesse", "objekt", false, ParGroesse),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var m = k.Arbeit; var kat = k.Katalog;
        var serieParam = Param.Text(p, "serie") is { } sp ? Leitungsserie.Hole(m, kat, sp) : null;
        foreach (var alt in RoutingBefehle.Leitungen(k, p))
        {
            var e = m.Finde(alt.Id)!;
            var pts = e.Geometrie?.Punkte;
            if (pts is null || pts.Count < 2) throw new HlksFehler($"{e.Kennzeichen} hat keinen Verlauf.", "Eine gezeichnete Leitung angeben.");
            var eigene = Leitungsserie.VonLeitung(m, kat, e);
            var s = serieParam ?? eigene
                ?? throw new HlksFehler($"{e.Kennzeichen} hat keine Serie.", "serie angeben (familien listet die Leitungsserien): " + Leitungsserie.Serien(m, kat));
            if (!kat.IstArt(e, s.Kategorie))
                throw new HlksFehler($"{e.Kennzeichen} ist ein {e.Klasse}, die Serie {s.Bezeichnung} gehört zu {s.Kategorie}.", $"Eine Serie der Kategorie {e.Klasse} wählen.");
            SerienGroesse sg;
            if (p["groesse"] is { } gn) sg = RoutingBefehle.Groesse(s, gn);
            else if (eigene?.Familie.Id == s.Familie.Id && s.GroesseVon(e) is { Gueltig: true } eg) sg = eg;   // Typ bzw. freie Grösse der Leitung
            else
            {
                // Abwasser-Serie nach Aussendurchmesser: eine Leitung ohne d (z. B. aus dem IFC-Import mit da, oder mit DN) über ihren
                // Aussendurchmesser – nie aus der DN gerechnet (Leitungsserie.GroesseFuer)
                var g = s.GroesseFuer(m, kat, e);
                if (g.Leer) throw new HlksFehler($"{e.Kennzeichen} hat keine Grösse.", $"groesse angeben; Grössen der Serie: {s.GroessenText}.");
                sg = s.TypFuer(g, out var grund) ?? throw new HlksFehler(s.Frei is null && !s.NachAussendurchmesser ? $"Die Serie {s.Bezeichnung} hat keine Grösse {g} (wie {e.Kennzeichen})." : $"{grund} (wie {e.Kennzeichen})",
                    s.NachAussendurchmesser && g.Dn is not null
                        ? $"groesse angeben ({s.GroessenForm}) oder den Aussendurchmesser an der Leitung nachtragen (da) – eine DN wird nie in einen Aussendurchmesser umgerechnet; Grössen: {s.GroessenText}."
                        : $"groesse angeben; Grössen: {s.GroessenText}.");
            }
            var hinweise = new List<string>();
            var pkt = pts.Select(V3.Aus).ToList();
            Routing.Doppelte(pkt);
            Routing.OhneGeradePunkte(pkt, hinweise);
            if (pkt.Count == 2 && eigene?.Familie.Id == s.Familie.Id && Familien.Typ(e) == sg.Typ && (s.Frei is null || Anschlusslagen.LeitungGroesse(m, kat, e).Gleich(sg.Groesse)) && pts.Count == 2)
            {
                k.Hinweise.Add($"{e.Kennzeichen} ist schon eine Gerade der Serie {s.Bezeichnung} ({sg.Text}).");
                continue;
            }
            var von = RoutingBefehle.Partner(m, kat, e, "anfang");
            var nach = RoutingBefehle.Partner(m, kat, e, "ende");
            var plan = Routing.Planen(m, kat, s, sg.Groesse, pkt, von, nach, new PlanOptionen { ReduktionAnGrenzen = false });
            var erste = plan.Geraden.FirstOrDefault()
                ?? throw new HlksFehler($"{e.Kennzeichen} ist zu kurz für Geraden zwischen den Bögen.", "Den Verlauf mit leitung_zeichnen neu legen.");
            foreach (var v in m.VerbindungenVon(e.Id).ToList()) k.Trennen(v);
            var liste = Routing.Umsetzen(k, plan, new RoutingZiel { Vorlage = e, Praefix = e.Kennzeichen, AbZwei = true },
                new Wiederverwendung { Geraden = [(e, erste.Abschnitt)] }, nachsichtigAnGrenzen: true);
            k.Hinweise.AddRange(hinweise);
            Routing.Uebersicht(k, $"{Name} {e.Kennzeichen}", plan, liste);
        }
    }
}

public sealed class LeitungGroesseAendern : IBefehl
{
    public string Name => "leitung_groesse";
    public const string Text = "Ändert die Grösse von Geraden bzw. eines ganzen Leitungszugs (Modul 3): Bögen im Zug werden durch den passenden Typ ersetzt (Lage im Achsschnittpunkt bleibt), die Geraden neu gekürzt, Reduktionen im Zug entfallen, an den Grenzen zu anderen Grössen (T-Stück, Gerät, andere Gerade, Bogen am Rand) werden Reduktionen eingesetzt bzw. ersetzt. Leitungszug = zusammenhängende Geraden über Bögen und Reduktionen bis zum nächsten T-Stück, Gerät oder offenen Ende. Kennzeichen bleiben; nicht mehr gebrauchte Formteile werden entfernt. Bei Kanalserien mit freier Grösse (leitung.frei) jede Grösse innerhalb von Grenzen und Schritt (sonst abgelehnt mit gerundetem Vorschlag und Normgrössen); Formteile dann aus der Vorlage ihrer Familie.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", false, RoutingBefehle.ParLeitung),
        new("leitungen", "liste", false, RoutingBefehle.ParLeitungen),
        new("zug", "bool", false, RoutingBefehle.ParZug),
        new("groesse", "objekt", true, ParGroesse),
    ];
    public const string ParGroesse = "Neue Grösse der Serie: Typname, Zahl (Rohr DN, Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund d) bzw. { dn } | { d } | { b, h }; bei freien Kanalserien jede Grösse innerhalb von Grenzen und Schritt, z. B. { \"b\": 430, \"h\": 270 } oder \"430x270\"";

    public void Ausfuehren(Kontext k, JsonObject p) => RoutingBefehle.ZugAendern(k, p, Name, null, p["groesse"] ?? throw new HlksFehler("Parameter «groesse» fehlt.", RoutingBefehle.ParGroesse));
}

public sealed class LeitungSerieAendern : IBefehl
{
    public string Name => "leitung_serie";
    public const string Text = "Ändert die Serie von Geraden bzw. eines ganzen Leitungszugs (Modul 3): die Grössen bleiben (müssen in der neuen Serie vorkommen; in eine Abwasser-Serie nach Aussendurchmesser kommen Leitungen mit DN über ihren Aussendurchmesser – Typ ihrer bisherigen Serie bzw. da –, nie aus der DN gerechnet), Bögen und Reduktionen werden durch die Formteile der neuen Serie ersetzt (Lage neu, Geraden neu gekürzt), Winkel müssen in der neuen Serie erlaubt sein; an den Grenzen werden Reduktionen nach Bedarf eingesetzt. Leitungszug wie bei leitung_groesse. Eine freie Grösse (Kanalserie mit leitung.frei) bleibt nur, wenn die neue Serie sie als Typ oder als freie Grösse innerhalb ihrer Grenzen und ihres Schritts hat.";
    public string Beschreibung => Text;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("leitung", "text", false, RoutingBefehle.ParLeitung),
        new("leitungen", "liste", false, RoutingBefehle.ParLeitungen),
        new("zug", "bool", false, RoutingBefehle.ParZug),
        new("serie", "text", true, ParSerie),
    ];
    public const string ParSerie = "Neue Leitungsserie (Kennzeichen, Name oder id), gleiche Kategorie (Rohr bzw. Kanal)";

    public void Ausfuehren(Kontext k, JsonObject p) =>
        RoutingBefehle.ZugAendern(k, p, Name, Leitungsserie.Hole(k.Arbeit, k.Katalog, Param.Text(p, "serie", true)), null);
}
