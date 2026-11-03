// Standardkomponenten aus dem IFC mit unseren Serien nachzeichnen (Christian 03.11.2026: «ist ein ablaufrohr gezeichnet, so wird dies beim
// import mit unserer silent familie nachgezeichnet»; «Serien-Zuordnung»). Beim HLKS-Import:
//   1. Leitungen: Regel IFC-Typ (Name bzw. ObjectType, regulärer Ausdruck, wahlweise nur für bestimmte Medien) → Serie der Bürobibliothek; die
//      Serie samt ihren Formteil-Familien (und mischbaren Serien) wird ins Projekt geladen, die Leitung bekommt Serie und Typ nach ihrem
//      Aussendurchmesser (Rohr) bzw. Durchmesser oder Breite × Höhe (Kanal). Regeln: Projektinformation.ifcSerien zuerst, dann die Vorgabe.
//   2. Formteile (Bogen, T-Stück, Reduktion), deren Anschlüsse alle an Leitungen derselben Serie hängen: das Formteil der Serie mit Lage im
//      Schnittpunkt der Leitungsachsen und Ausrichtung aus deren Richtungen – der Verlauf bleibt wie im IFC. Ein Knick, den die Serie nicht hat,
//      nimmt den nächsten erlaubten Winkel (Abwasser-Serien bis 5°, sonst 2°; Revit zeichnet 90°, Geberit Silent hat 88.5° – Christian: «die bogen sind 88.5° zum
//      kompensieren des gefälles von der vertikalen in die Horizontale»), mit Hinweis.
// ANNAHMEN (Vorgabe-Regeln, an Bernstrasse 42 H+K-Revit-Typen geprüft; wählbar): Mapress ohne «Edelstahl» im Namen: Trinkwasser (KW, WW, ZIR)
//   Edelstahl, sonst C-Stahl; «Stahl» = Stahlrohr geschweisst; Rund-Kanäle = Wickelfalzrohr rund. Entscheide Christian 03.11.2026: Flexkanäle =
//   Depping Aludec 112 («flexkanäle von depping ag»), Uponor-Metallverbundrohr = Uponor Uni Pipe PLUS FBH («uponor FBH Rohre in Standard»;
//   ohne Formteile, ab Rolle, Biegeradius 100 mm). Aufrunden (Regel «aufrunden»): fehlt die Grösse in der Serie, nimmt die Leitung die
//   nächstgrössere (zuerst die übrigen passenden Regeln: Flexkanal Ø 90 → Schmidlin KSR, Christian 03.11.2026: «nehme auch noch den 90er
//   kunststoffschlauch von schmidlin»; vorher «Auf Ø 100»); die Leitung trägt dann die Grösse der Serie, das IFC-Mass steht
//   in der Kennung «ifcGroesse» (Deckung zählt sie eigens).

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

static class IfcSerien
{
    public sealed record Regel(string Muster, string? Serie, string[]? Medien = null, bool Aufrunden = false);

    public static readonly Regel[] Vorgabe =
    [
        new("Silent-db20|Silent db20", "Geberit Silent-db20"),
        new(@"Geberit[ _]PE|PE[ _-]Rohr|_PE\b", "Geberit PE"),
        new(@"Mapress.*Edelstahl|1\.4401", "Geberit Mapress Edelstahl"),
        new("Mapress", "Geberit Mapress Edelstahl", ["KW", "WW", "ZIR"]),
        new("Mapress", "Geberit Mapress C-Stahl"),
        new("Sani[Pp]ex", "JRG Sanipex MT"),
        new("Flexkanal", "Flexkanal Depping Aludec 112", Aufrunden: true),
        new("Flexkanal", "Flexrohr Schmidlin KSR"),
        new("Uponor", "Uponor Uni Pipe PLUS FBH"),
        new("Stahl", "Stahlrohr geschweisst"),
        new("^Rechteck", "Rechteckkanal"),
        new("^Rund", "Wickelfalzrohr rund"),
    ];

    /// <summary>Regeln: Projektinformation.ifcSerien [{ muster, serie, medien }] (serie leer = keine Serie), danach die Vorgabe.</summary>
    public static List<Regel> Regeln(Modell m)
    {
        var aus = new List<Regel>();
        if (m.DerKlasse("Projektinformation").FirstOrDefault()?.Eigenschaft("ifcSerien")?.Inhalt is JsonArray a)
            foreach (var r in a.OfType<JsonObject>())
                if (Wert(r["muster"]) is { Length: > 0 } mu)
                    aus.Add(new Regel(mu, Wert(r["serie"]) is { Length: > 0 } s ? s : null,
                        r["medien"] is JsonArray me ? me.Select(x => Wert(x)).OfType<string>().ToArray() : null,
                        r["aufrunden"] is JsonValue au && au.TryGetValue<bool>(out var ja) && ja));
        aus.AddRange(Vorgabe);
        return aus;
    }

    public static IEnumerable<string> Zuordnen(IfcLauf lauf, IEnumerable<(IIfcProduct X, Element E)> leitungen, IEnumerable<(IIfcProduct X, Element E)> formteile)
    {
        var k = lauf.Kontext;
        var kat = lauf.Katalog;
        var regeln = Regeln(k.Arbeit);
        var ort = FamilienBibliothekDienst.Ort(kat);
        var hinweise = new List<string>();
        var ohne = new Dictionary<string, int>();
        var fehler = new Dictionary<string, int>();
        var aufgerundet = new Dictionary<string, int>();
        var serien = new Dictionary<string, Leitungsserie?>(StringComparer.OrdinalIgnoreCase);
        var vonLeitung = new Dictionary<string, (Leitungsserie S, Leitungsgroesse G)>();
        int zugeordnet = 0;

        foreach (var (x, e) in leitungen)
        {
            var typname = Typname(x) ?? e.Name ?? "";
            var medium = e.Anschluesse.Select(a => a.Medium).FirstOrDefault(md => md is { Length: > 0 } && md != "*") ?? Netz.ElementMedium(k.Arbeit, e);
            // passende Regeln der Reihe nach: die erste, deren Serie die Grösse hat (Flexkanal Ø 90 → Schmidlin KSR, sonst Depping Aludec);
            // eine Regel ohne Serie beendet die Suche; hat keine die Grösse, rundet die erste Regel mit «aufrunden» auf
            var passend = regeln.Where(r => Regex.IsMatch(typname, r.Muster, RegexOptions.IgnoreCase) && (r.Medien is null || medium is not null && r.Medien.Contains(medium, StringComparer.OrdinalIgnoreCase)))
                .TakeWhile(r => r.Serie is not null).ToList();
            if (passend.Count == 0) { Zaehlen(ohne, typname); continue; }
            Regel regel = passend[0];
            Leitungsserie? s = null;
            SerienGroesse? g = null;
            foreach (var r in passend)
            {
                if (!serien.TryGetValue(r.Serie!, out var rs)) serien[r.Serie!] = rs = Laden(k, kat, ort, r.Serie!, fehler);
                if (rs is null) continue;
                s ??= rs;
                if (Groesse(rs, e) is { } rg) { (regel, s, g) = (r, rs, rg); break; }
            }
            if (s is null) continue;
            var name = regel.Serie!;
            if (g is null && passend.FirstOrDefault(r => r.Aufrunden) is { } ra && serien.GetValueOrDefault(ra.Serie!) is { } sa && Naechstgroesser(sa, e) is { } gr)
            {
                (regel, s, name) = (ra, sa, ra.Serie!);
                // nächstgrössere Grösse der Serie; das IFC-Mass bleibt als Kennung (Deckung)
                (e.Kennungen ??= [])[IfcGroesse] = Kurz(e);
                if (e.Zahl("da") is not null && gr.Aussen is double ga) lauf.Wert(e, "da", ga, $"aufgerundet auf {s.Familie.Name} {gr.Typ} (IFC {Kurz(e)})");
                else if (e.Zahl("durchmesser") is not null && gr.Groesse.D is double gd) lauf.Wert(e, "durchmesser", gd, $"aufgerundet auf {s.Familie.Name} {gr.Typ} (IFC {Kurz(e)})");
                Zaehlen(aufgerundet, $"{name}: {e.Kennungen[IfcGroesse]} → {gr.Typ}");
                g = gr;
            }
            if (g is null) { Zaehlen(fehler, $"{name}: Grösse {Kurz(e)} nicht in der Serie"); continue; }
            e.Bezuege[Familien.Bezug] = s.Familie.Id;
            lauf.Wert(e, "familientyp", g.Typ, $"Serie {s.Familie.Name} nach IFC-Typ «{typname}» (Regel «{regel.Muster}»)");
            vonLeitung[e.Id] = (s, g.Groesse);
            zugeordnet++;
        }

        // Leitungen ab Rolle: Bögen ohne Formteil der Serie gehen in der Leitung auf
        int biegungen = Zusammenfuehren(lauf, formteile.Select(f => f.E).Where(e => kat.IstArt(e, "Bogen")).ToList(), vonLeitung);

        // Formteile zwischen Leitungen derselben Serie
        var anschluss = new Dictionary<(string, string), (string Id, string Name)>();
        foreach (var v in lauf.Verbindungen) { anschluss[(v.VonRef, v.VonAnschluss)] = (v.NachRef, v.NachAnschluss); anschluss[(v.NachRef, v.NachAnschluss)] = (v.VonRef, v.VonAnschluss); }
        int formteileOk = 0, winkelAngepasst = 0;
        foreach (var (_, e) in formteile)
        {
            if (!lauf.Geplant.ContainsKey(e.Id)) continue;   // in einer Leitung aufgegangen
            string? art = kat.IstArt(e, "Bogen") ? "bogen" : kat.IstArt(e, "TStueck") ? "tstueck" : kat.IstArt(e, "Reduktion") ? "reduktion" : null;
            if (art is null) continue;
            var enden = new Dictionary<string, (V3 Punkt, V3 Raus, Leitungsserie S, Leitungsgroesse G)>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in e.Anschluesse)
            {
                if (!anschluss.TryGetValue((e.Id, a.Name), out var gg) || !vonLeitung.TryGetValue(gg.Id, out var sl) || !lauf.Geplant.TryGetValue(gg.Id, out var l)) break;
                var p = l.Geometrie?.Punkte;
                if (p is not { Count: >= 2 }) break;
                bool amAnfang = gg.Name.Equals("anfang", StringComparison.OrdinalIgnoreCase);
                var ende = V3.Aus(amAnfang ? p[0] : p[^1]); var weiter = V3.Aus(amAnfang ? p[1] : p[^2]);
                enden[a.Name] = (ende, (weiter - ende).Einheit(), sl.S, sl.G);
            }
            if (enden.Count != e.Anschluesse.Count || enden.Values.Select(v => v.S.Familie.Id).Distinct().Count() != 1) continue;
            var s = enden.Values.First().S;
            try
            {
                var (wahl, ursprung, x, z, angepasst) = Formteil(k.Arbeit, kat, s, art, enden);
                if (wahl is null) { Zaehlen(fehler, $"{Leitungsserie.Artname(art)} in {s.Familie.Name}: kein passendes Formteil"); continue; }
                e.Bezuege[Familien.Bezug] = wahl.Familie!.Id;
                lauf.Wert(e, "familientyp", wahl.Typ, $"Formteil der Serie {s.Familie.Name}");
                if (wahl.Exemplar is { Count: > 0 } ex) lauf.Wert(e, "parameter", ex.DeepClone(), $"Formteil der Serie {s.Familie.Name}");
                lauf.Wert(e, "ausrichtung", Familien.AusrichtungJson(x, z), "Richtungen der angeschlossenen Leitungen");
                e.Geometrie = new Geometrie { Art = "punkt", Punkte = [[Math.Round(ursprung.X, 1), Math.Round(ursprung.Y, 1), Math.Round(ursprung.Z, 1)]] };
                formteileOk++;
                if (angepasst) winkelAngepasst++;
            }
            catch (HlksFehler f) { Zaehlen(fehler, f.Message.Length > 80 ? f.Message[..80] + "…" : f.Message); }
        }

        if (zugeordnet > 0) hinweise.Add($"Serien aus dem Standard: {zugeordnet} Leitungen und {formteileOk} Formteile nachgezeichnet ({string.Join(", ", serien.Where(s => s.Value is not null).Select(s => s.Key))})"
            + (winkelAngepasst > 0 ? $"; {winkelAngepasst} Bögen auf den nächsten Winkel der Serie (Spielraum Abwasser 5°, sonst 2°; z. B. 90° → 88.5° für das Gefälle)" : "") + ".");
        foreach (var (was, n) in aufgerundet) hinweise.Add($"Serien aus dem Standard: {n}× aufgerundet – {was}.");
        if (biegungen > 0) hinweise.Add($"Serien aus dem Standard: {biegungen} Bögen ab Rolle in der Leitung aufgegangen (Serie ohne Bogen-Formteil, Winkel frei; Biegung mit dem Biegeradius der Serie).");
        if (ohne.Count > 0) hinweise.Add("Ohne Serie (keine Regel, Projektinformation.ifcSerien): " + string.Join(", ", ohne.OrderByDescending(o => o.Value).Take(8).Select(o => $"«{o.Key}» {o.Value}×")) + ".");
        foreach (var (grund, n) in fehler) hinweise.Add($"Serien aus dem Standard: {n}× {grund}.");
        return hinweise;
    }

    /// <summary>Serie (Name oder Kennung) aus dem Projekt bzw. der Bürobibliothek, samt ihren Formteil-Familien und mischbaren Serien.</summary>
    static Leitungsserie? Laden(Kontext k, Katalog kat, BibliotheksOrt ort, string name, Dictionary<string, int> fehler)
    {
        Element? Finden(string bezug) => k.Arbeit.DerKlasse(Familien.Klasse).FirstOrDefault(f => Familien.DefinitionsId(f) == bezug || string.Equals(f.Name?.Trim(), bezug, StringComparison.OrdinalIgnoreCase));
        Element? Holen(string bezug)
        {
            if (Finden(bezug) is { } da) return da;
            if (!ort.Aktiv) { Zaehlen(fehler, $"«{bezug}»: Familienbibliothek nicht erreichbar"); return null; }
            try
            {
                int n = k.Hinweise.Count;
                FamilieLaden.AusBibliothek(k, ort, bezug, null, null);
                if (k.Hinweise.Count > n) k.Hinweise.RemoveRange(n, k.Hinweise.Count - n);
            }
            catch (HlksFehler f) { Zaehlen(fehler, $"«{bezug}» nicht geladen: {(f.Message.Length > 60 ? f.Message[..60] + "…" : f.Message)}"); return null; }
            return Finden(bezug);
        }
        if (Holen(name) is not { } fam) return null;
        if (fam.Eigenschaft("definition")?.Inhalt is JsonObject def && def["leitung"] is JsonObject l)
        {
            if (l["formteile"] is JsonObject ft) foreach (var (_, f) in ft) if (Wert(f?["familie"]) is { } id) Holen(id);
            if (l["regeln"]?["mischbar"] is JsonArray mi) foreach (var m in mi) if (Wert(m) is { } id) Holen(id);
        }
        try { return Leitungsserie.Von(k.Arbeit, kat, Finden(name)!); }
        catch (HlksFehler f) { Zaehlen(fehler, $"«{name}» ist keine Leitungsserie: {f.Message}"); return null; }
    }

    /// <summary>Typ der Serie für die Grösse der Leitung: Rohr über den Aussendurchmesser (±1 mm), sonst DN; Kanal rund d, eckig b × h
    /// (auch als freie Grösse innerhalb der Grenzen der Serie).</summary>
    static SerienGroesse? Groesse(Leitungsserie s, Element e)
    {
        if (e.Zahl("da") is double da)
            return s.Groessen.Where(g => g.Gueltig && g.Aussen is double a && Math.Abs(a - da) <= 1).MinBy(g => Math.Abs(g.Aussen!.Value - da));
        Leitungsgroesse? lg = e.Zahl("durchmesser") is double d ? Leitungsgroesse.Rund(d)
            : e.Zahl("breite") is double b && e.Zahl("hoehe") is double h ? Leitungsgroesse.Rechteck(b, h)
            : e.Zahl("dn") is double dn ? Leitungsgroesse.Rohr(dn) : null;
        if (lg is not { } g1 || !s.PasstArt(g1)) return null;
        if (s.TypFuer(g1) is { } t) return t;
        return s.Frei is not null && s.Grenzverletzung(g1) is null && s.ImSchritt(g1) ? s.Virtuell(g1) : null;
    }

    static (FormteilWahl? Wahl, V3 Ursprung, V3 X, V3 Z, bool Angepasst) Formteil(Modell m, Katalog kat, Leitungsserie s, string art,
        Dictionary<string, (V3 Punkt, V3 Raus, Leitungsserie S, Leitungsgroesse G)> enden)
    {
        var a = enden["a"]; var b = enden["b"];
        var durch = a.Raus * -1;
        if (art == "reduktion")
        {
            var w = s.Formteil(m, kat, art, a.G, b.G);
            if (!w.Ok || w.Vertauscht) return (null, a.Punkt, default, default, false);
            var (rx, rz) = Formteilgeometrie.Ausrichtung(durch);
            return (w, a.Punkt, rx, rz, false);
        }
        var seite = art == "tstueck" ? enden["c"] : b;
        var ursprung = Schnitt(a.Punkt, a.Raus, seite.Punkt, seite.Raus);
        double winkel = Math.Acos(Math.Clamp(durch.Mal(seite.Raus), -1, 1)) * 180 / Math.PI;
        var kandidaten = new List<(double W, bool Angepasst)> { (winkel, false) };
        // Spielraum (Christian 03.11.2026: «dies ist nicht exakt, und kann nachgearbeitet werden. es braucht also einen gewissen spielraum bei PE und
        // Silent»): Abwasser-Serien bis 5°, die übrigen bis 2°
        double spiel = s.Abwasser ? 5 : 2;
        var erlaubt = art == "tstueck" ? s.Block.Abzweigwinkel ?? [90] : s.Block.Winkel;
        if (erlaubt is not null)
            kandidaten.AddRange(erlaubt.Where(x => Math.Abs(x - winkel) <= spiel && Math.Abs(x - winkel) > 1e-6).OrderBy(x => Math.Abs(x - winkel)).Select(x => (x, true)));
        foreach (var (wi, angepasst) in kandidaten)
        {
            var w = art == "tstueck" ? s.Formteil(m, kat, art, a.G, seite.G, wi) : s.Formteil(m, kat, art, a.G, null, wi);
            if (!w.Ok) continue;
            var (x, z) = Formteilgeometrie.Ausrichtung(durch, seite.Raus);
            return (w, ursprung, x, z, angepasst);
        }
        return (null, ursprung, default, default, false);
    }

    /// <summary>Leitungen ab Rolle (Christian 03.11.2026: «bei fbh rohren gibt es keine fittinge. diese rohre werden ab rolle verlegt. minimaler
    /// biegeradius 10cm»): ein Bogen aus dem IFC zwischen zwei Leitungen derselben Serie, die kein Bogen-Formteil hat und deren Winkel frei ist, geht
    /// in einer Leitung auf – Knick im Schnittpunkt der Achsen; die Darstellung rundet ihn mit dem Biegeradius der Serie (HlksKoerper). Die erste
    /// Leitung bleibt (ihre GlobalId), die zweite und der Bogen entfallen; ihre GlobalIds stehen in der Kennung «ifcMit» (Deckung). Nicht bei
    /// Umkehr (180°) und nicht, wenn der Knick weiter als 1 m vom Leitungsende liegt (dann stimmt die Verbindung nicht).</summary>
    static int Zusammenfuehren(IfcLauf lauf, List<Element> boegen, Dictionary<string, (Leitungsserie S, Leitungsgroesse G)> vonLeitung)
    {
        int n = 0;
        var jeElement = new Dictionary<string, List<IfcVerbindung>>();
        void Index(IfcVerbindung v) { foreach (var id in new[] { v.VonRef, v.NachRef }) (jeElement.TryGetValue(id, out var l) ? l : jeElement[id] = []).Add(v); }
        foreach (var v in lauf.Verbindungen) Index(v);
        void Entfernen(IfcVerbindung v) { lauf.Verbindungen.Remove(v); foreach (var id in new[] { v.VonRef, v.NachRef }) jeElement.GetValueOrDefault(id)?.Remove(v); }
        static bool Gleich(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
        // Verbindungen eines Leitungsendes auf ein anderes Element bzw. Ende umhängen
        void Umhaengen(string id, string ende, string neuId, string neuEnde)
        {
            foreach (var v in (jeElement.GetValueOrDefault(id) ?? []).ToList())
            {
                bool von = v.VonRef == id && Gleich(v.VonAnschluss, ende), nach = v.NachRef == id && Gleich(v.NachAnschluss, ende);
                if (!von && !nach) continue;
                Entfernen(v);
                var w = von ? v with { VonRef = neuId, VonAnschluss = neuEnde } : v with { NachRef = neuId, NachAnschluss = neuEnde };
                lauf.Verbindungen.Add(w);
                Index(w);
            }
        }
        foreach (var bogen in boegen)
        {
            var vb = jeElement.GetValueOrDefault(bogen.Id)?.ToList() ?? [];
            if (vb.Count != 2) continue;
            (string Id, string Ende) Gegen(IfcVerbindung v) => v.VonRef == bogen.Id ? (v.NachRef, v.NachAnschluss) : (v.VonRef, v.VonAnschluss);
            var (ia, ea) = Gegen(vb[0]); var (ib, eb) = Gegen(vb[1]);
            if (ia == ib || !vonLeitung.TryGetValue(ia, out var sa) || !vonLeitung.TryGetValue(ib, out var sb) || sa.S.Familie.Id != sb.S.Familie.Id) continue;
            if (!sa.S.Block.WinkelFrei || sa.S.Block.Formteile.ContainsKey("bogen")) continue;
            if (!lauf.Geplant.TryGetValue(ia, out var a) || !lauf.Geplant.TryGetValue(ib, out var b)) continue;
            if (a.Geometrie?.Punkte is not { Count: >= 2 } pa || b.Geometrie?.Punkte is not { Count: >= 2 } pb) continue;
            bool aAmAnfang = Gleich(ea, "anfang"), bAmEnde = Gleich(eb, "ende");
            var la = pa.Select(p => (double[])p.Clone()).ToList(); if (aAmAnfang) la.Reverse();
            var lb = pb.Select(p => (double[])p.Clone()).ToList(); if (bAmEnde) lb.Reverse();
            V3 a1 = V3.Aus(la[^1]), b0 = V3.Aus(lb[0]);
            var u = (a1 - V3.Aus(la[^2])).Einheit(); var w = (V3.Aus(lb[1]) - b0).Einheit();
            if (u.Mal(w) < -0.999) continue;   // Umkehr: kein Knick bestimmbar
            var knick = u.Mal(w) > 0.9999 ? (a1 + b0) * 0.5 : Schnitt(a1, u, b0, w);
            if ((knick - a1).Laenge > 1000 || (knick - b0).Laenge > 1000) continue;
            var punkte = la.Take(la.Count - 1).Append(knick.Feld()).Concat(lb.Skip(1)).Select(p => p.Select(x => Math.Round(x, 1)).ToArray()).ToList();
            // a: die Bogenseite wird «ende»; die Verbindungen am fernen Ende heissen nach dem Umdrehen «anfang»
            if (aAmAnfang)
            {
                Umhaengen(a.Id, "ende", a.Id, "#anfang");
                foreach (var x in a.Anschluesse) x.Name = Gleich(x.Name, "anfang") ? "ende" : Gleich(x.Name, "ende") ? "anfang" : x.Name;
                Umhaengen(a.Id, "#anfang", a.Id, "anfang");
            }
            string bFern = bAmEnde ? "anfang" : "ende";
            int ie = a.Anschluesse.FindIndex(x => Gleich(x.Name, "ende"));
            if (ie >= 0 && b.Anschluesse.FirstOrDefault(x => Gleich(x.Name, bFern))?.Kopie() is { } fern) { fern.Name = "ende"; a.Anschluesse[ie] = fern; }
            foreach (var v in vb) Entfernen(v);
            foreach (var v in (jeElement.GetValueOrDefault(b.Id) ?? []).ToList())
                if (!Gleich(v.VonRef == b.Id ? v.VonAnschluss : v.NachAnschluss, bFern)) Entfernen(v);
            Umhaengen(b.Id, bFern, a.Id, "ende");
            a.Geometrie = new Geometrie { Art = a.Geometrie.Art, Punkte = punkte };
            var mit = new[] { a.Kennungen?.GetValueOrDefault(IfcMit), b.Kennungen?.GetValueOrDefault("ifc"), b.Kennungen?.GetValueOrDefault(IfcMit), bogen.Kennungen?.GetValueOrDefault("ifc") }
                .Where(x => x is { Length: > 0 }).SelectMany(x => x!.Split(',')).Distinct();
            (a.Kennungen ??= [])[IfcMit] = string.Join(",", mit);
            lauf.Geplant.Remove(b.Id);
            lauf.Geplant.Remove(bogen.Id);
            vonLeitung.Remove(b.Id);
            n++;
        }
        return n;
    }

    /// <summary>Kennung einer aufgerundeten Leitung: Grösse laut IFC (z. B. «d 90»).</summary>
    public const string IfcGroesse = "ifcGroesse";

    /// <summary>Nächstgrössere gültige Grösse der Serie: Rohr nach Aussendurchmesser, Kanal rund nach d; eckig und ohne Mass null.</summary>
    static SerienGroesse? Naechstgroesser(Leitungsserie s, Element e)
    {
        if (e.Zahl("da") is double da) return s.Groessen.Where(g => g.Gueltig && g.Aussen is double a && a > da).MinBy(g => g.Aussen);
        if (e.Zahl("durchmesser") is double d) return s.Groessen.Where(g => g.Gueltig && g.Groesse.D is double x && x > d).MinBy(g => g.Groesse.D);
        return null;
    }

    /// <summary>Kennung einer Leitung ab Rolle: GlobalIds der IFC-Teile (Leitungen, Bögen), die in ihr aufgegangen sind, mit Komma.</summary>
    public const string IfcMit = "ifcMit";

    /// <summary>Mitte der kürzesten Verbindung zweier Geraden p + t·u und q + v·w (parallel: Mitte der Punkte).</summary>
    static V3 Schnitt(V3 p, V3 u, V3 q, V3 w)
    {
        var r = p - q;
        double a = u.Mal(u), b = u.Mal(w), c = w.Mal(w), d = u.Mal(r), e = w.Mal(r), n = a * c - b * b;
        if (Math.Abs(n) < 1e-9) return (p + q) * 0.5;
        double t = (b * e - c * d) / n, v = (a * e - b * d) / n;
        return (p + u * t + q + w * v) * 0.5;
    }

    static string Kurz(Element e) => e.Zahl("da") is double da ? $"da {da:0.#}" : e.Zahl("durchmesser") is double d ? $"d {d:0.#}"
        : e.Zahl("breite") is double b ? $"{b:0} × {e.Zahl("hoehe"):0}" : "?";

    static string? Wert(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;

    static void Zaehlen(Dictionary<string, int> d, string grund) => d[grund] = d.GetValueOrDefault(grund) + 1;

    static string? Typname(IIfcProduct x)
    {
        if (x is IIfcObject o)
        {
            if (o.IsTypedBy.FirstOrDefault()?.RelatingType?.Name is { } tn && IfcLauf.Text(tn) is { Length: > 0 } t) return t;
            if (IfcLauf.Text(o.ObjectType) is { Length: > 0 } ot) return ot;
        }
        return IfcLauf.Text(x.Name);
    }
}
