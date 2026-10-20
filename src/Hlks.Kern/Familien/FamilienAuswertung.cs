// Familien (Modul 2, B1): Auswertung einer Familie für einen Typ und Exemplarwerte → aufgelöste Werte, Körper (in
// Familienkoordinaten), Anschlüsse mit Lage und Richtung, Sinnbild. Reihenfolge der Werte (Spezifikation):
// Exemplarwert › Typwert › Formel › Vorgabewert (ein Parameter mit Formel hat weder Typ- noch Exemplarwerte).
//
// Zwischenspeicher: je FamilieDef (= je Definitionsstand, siehe Familien.Definition) und Schlüssel «Typ + Exemplarwerte»
// genau eine Auswertung; der Umriss von oben und die Körperteile je Detailgrad werden dort einmal gerechnet. 500 platzierte
// Elemente desselben Typs kosten damit eine Auswertung und 500 Koordinatentransformationen.
//
// Familienkoordinaten (Spezifikation): mm, Ursprung = Einfügepunkt, x nach rechts, y nach hinten (Vorderseite −y), z oben.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Clipper2Lib;

namespace Hlks.Kern;

/// <summary>Ein Anschluss nach der Auswertung (Familienkoordinaten bzw. nach dem Platzieren Weltkoordinaten).</summary>
public sealed record FamAnschlussErgebnis(string Name, string Medium, string Fluss, string? Art, string? Form, double? Dn, double? D, double? B, double? H,
    string? Gewinde, double[] Lage, double[] Richtung, bool Sichtbar, string? Beschreibung);

/// <summary>Ein Körper nach der Auswertung. Teil in Familienkoordinaten (quader | zylinder | koerper) oder null (unsichtbar, Fehler).</summary>
public sealed class FamKoerperErgebnis
{
    public required FamKoerper Def { get; init; }
    public bool Sichtbar { get; init; }
    public Koerperteil? Teil { get; init; }
    public bool ImDetailgrad(string detail) => Sichtbar && Teil is not null && Def.Detailgrad.Contains(detail);
}

/// <summary>Werte eines Typs einer Leitungsserie (Modul 3): Grösse je Schlüssel (dn | d | b | h), Aussen- und Innendurchmesser,
/// Wandstärke, Biegeradius in mm (null = nicht angegeben).</summary>
public sealed record FamLeitungWerte(IReadOnlyDictionary<string, double> Groesse, double? Aussen, double? Innen, double? Wand, double? Biegeradius);

/// <summary>Ein Teil des Sinnbilds nach der Auswertung (Familienkoordinaten).</summary>
public sealed record FamZeichnungErgebnis(FamZeichnung Def, bool Sichtbar, Primitiv Primitiv);

public sealed class FamAufloesung
{
    public required FamilieDef Familie { get; init; }
    public required string Typ { get; init; }
    /// <summary>Werte aller Parameter (null = kein Wert).</summary>
    public Dictionary<string, FWert?> Werte { get; } = new(StringComparer.Ordinal);
    public List<FamFehler> Fehler { get; } = [];
    public bool Gueltig => Fehler.Count == 0;
    public List<FamKoerperErgebnis> Koerper { get; } = [];
    public List<FamAnschlussErgebnis> Anschluesse { get; } = [];
    public List<FamZeichnungErgebnis> Sinnbild { get; } = [];
    /// <summary>Werte einer Leitungsserie für diesen Typ (Modul 3) oder null (keine Leitungsserie bzw. Fehler).</summary>
    public FamLeitungWerte? Leitung { get; internal set; }

    readonly ConcurrentDictionary<string, List<Flaeche>> umrisse = new();
    readonly ConcurrentDictionary<string, List<Koerperteil>> teile = new();

    public IEnumerable<FamKoerperErgebnis> Sichtbare(string detail) => Koerper.Where(k => k.ImDetailgrad(detail));

    /// <summary>Körperteile in Familienkoordinaten für einen Detailgrad (gemeinsam genutzt – nicht ändern).</summary>
    public List<Koerperteil> Teile(string detail) => teile.GetOrAdd(detail, d => Sichtbare(d).Select(k => k.Teil!).ToList());

    /// <summary>Sinnbild für einen Detailgrad; leer, wenn die Familie keines hat oder keines für diesen Detailgrad.</summary>
    public List<Primitiv> SinnbildFuer(string detail) =>
        Sinnbild.Where(s => s.Sichtbar && s.Def.Detailgrad.Contains(detail)).Select(s => s.Primitiv).ToList();

    /// <summary>Umriss der Körper von oben (Vereinigung der Projektionen) für einen Detailgrad, Familienkoordinaten.</summary>
    public List<Flaeche> Umriss(string detail) => umrisse.GetOrAdd(detail, d => FamilienUmriss.Rechnen(Sichtbare(d)));

    readonly ConcurrentDictionary<string, List<Flaeche>> umrisseAusgerichtet = new();

    /// <summary>Umriss von oben der in die Achsen der Einbaulage gedrehten Körper (volle Ausrichtung, Modul 3), Ursprung im
    /// Einfügepunkt (xy relativ, der Aufrufer verschiebt) – zwischengespeichert je Detailgrad und Achsen.</summary>
    public List<Flaeche> UmrissAusgerichtet(string detail, Einbaulage lage)
    {
        var schluessel = detail + "|" + lage.AchsenSchluessel;
        if (umrisseAusgerichtet.TryGetValue(schluessel, out var u)) return u;
        var l0 = lage with { Ursprung = [0, 0, 0] };
        u = FamilienUmriss.Rechnen(Teile(detail).Select(t => FamilienKoerper.Platzieren(t, l0, null)).ToList());
        if (umrisseAusgerichtet.Count > 200) umrisseAusgerichtet.Clear();
        umrisseAusgerichtet[schluessel] = u;
        return u;
    }

    public FWert? Wert(string name) => Werte.GetValueOrDefault(name);
}

public static class FamilienAuswertung
{
    /// <summary>Auswertung für Typ und Exemplarwerte, zwischengespeichert je Definitionsstand.</summary>
    public static FamAufloesung Aufloesen(FamilieDef f, string? typ, JsonObject? exemplar)
    {
        var schluessel = (typ ?? "") + "\u0001" + (exemplar is null || exemplar.Count == 0 ? "" : exemplar.ToJsonString());
        if (f.Aufloesungen.TryGetValue(schluessel, out var a)) return a;
        a = Rechnen(f, typ, exemplar);
        if (f.Aufloesungen.Count > 1000) f.Aufloesungen.Clear();   // Schutz gegen unbegrenztes Wachsen (viele Exemplarwerte)
        f.Aufloesungen[schluessel] = a;
        return a;
    }

    /// <summary>Exemplarwerte eines Elements prüfen: nur bekannte Exemplarparameter ohne Formel, Wert passend zum Datentyp.</summary>
    public static List<FamFehler> ExemplarPruefen(FamilieDef f, JsonObject? exemplar)
    {
        var l = new List<FamFehler>();
        if (exemplar is null) return l;
        var erlaubt = f.Parameter.Where(p => p.Art == "exemplar" && !p.HatFormel).Select(p => p.Name).ToList();
        string Liste() => erlaubt.Count == 0 ? "Die Familie hat keine einstellbaren Exemplarparameter." : "Exemplarparameter: " + string.Join(", ", erlaubt);
        foreach (var (name, wert) in exemplar)
        {
            var ort = $"parameter.{name}";
            if (!f.NachName.TryGetValue(name, out var p)) { l.Add(new(ort, $"Parameter «{name}» gibt es in der Familie nicht", Formel.NamensVorschlag(name, erlaubt))); continue; }
            if (p.HatFormel) { l.Add(new(ort, $"«{name}» hat eine Formel und ist nicht pro Exemplar einstellbar", Liste())); continue; }
            if (p.Art == "typ") { l.Add(new(ort, $"«{name}» ist ein Typparameter und nicht pro Exemplar einstellbar", "Einen anderen Typ wählen (familientyp) oder die Familie ändern. " + Liste())); continue; }
            p.Umwandeln(wert, out var fehler);
            if (fehler is not null) l.Add(new(ort, fehler));
            else if (p.Datentyp == "medium" && wert is JsonValue v && v.TryGetValue<string>(out var s) && s != "*" && f.Katalog is { } k && !k.Medien.ContainsKey(s))
                l.Add(new(ort, $"Medium «{s}» gibt es nicht", "Medien: " + string.Join(", ", k.Medien.Keys)));
        }
        return l;
    }

    /// <summary>Fehler eines Felds mit Stelle (intern, wird zu FamFehler).</summary>
    sealed class FeldFehler(FamFehler f) : Exception(f.Meldung) { public FamFehler Fehler { get; } = f; }

    static FamAufloesung Rechnen(FamilieDef f, string? typName, JsonObject? exemplar)
    {
        var a = new FamAufloesung { Familie = f, Typ = typName ?? "" };
        if (!f.Gueltig) { a.Fehler.AddRange(f.Fehler); return a; }
        var typ = f.Typ(typName);
        if (typ is null)
        {
            a.Fehler.Add(new("familientyp", typName is null ? "Typ fehlt" : $"Typ «{typName}» gibt es in der Familie «{f.Name}» nicht",
                "Typen: " + string.Join(", ", f.Typen.Select(t => t.Name))));
            return a;
        }
        a.Fehler.AddRange(ExemplarPruefen(f, exemplar));
        var umg = new Umgebung(f, typ, a.Fehler.Count == 0 ? exemplar : null);

        // 1. Parameter
        foreach (var p in f.Parameter)
        {
            try { a.Werte[p.Name] = umg.Hole(p.Name); }
            catch (FormelFehler ff)
            {
                a.Werte[p.Name] = null;
                a.Fehler.Add(p.HatFormel
                    ? new($"parameter «{p.Name}».formel", $"Formel «{p.FormelText}»: {ff.MitStelle}", ff.Vorschlag)
                    : new($"parameter «{p.Name}»", ff.Message, ff.Vorschlag));
            }
        }

        double Z(FamZahl? z)
        {
            if (z is null) throw new FeldFehler(new("", "Wert fehlt"));
            if (z.Konstante is double c) return c;
            try
            {
                var w = z.Formel!.Werten(umg);
                if (w.IstText) throw new FormelFehler($"ergibt den Text «{w.Text}», gebraucht wird eine Zahl", 0, "Formel muss eine Zahl ergeben.");
                return w.Zahl;
            }
            catch (FormelFehler ff) { throw new FeldFehler(new(z.Ort, $"Formel «{z.Text}»: {ff.MitStelle}", ff.Vorschlag)); }
        }
        double[] P(FamZahl[]? p) => p is null ? throw new FeldFehler(new("", "Punkt fehlt")) : p.Select(Z).ToArray();

        // 2. Körper
        foreach (var k in f.Koerper)
        {
            try
            {
                bool sichtbar = k.Sichtbar is null || Z(k.Sichtbar) != 0;
                a.Koerper.Add(new FamKoerperErgebnis { Def = k, Sichtbar = sichtbar, Teil = sichtbar ? FamilienKoerper.Bauen(k, Z, P) : null });
            }
            catch (FeldFehler ff) { a.Fehler.Add(ff.Fehler); a.Koerper.Add(new FamKoerperErgebnis { Def = k, Sichtbar = false }); }
            catch (HlksFehler hf) { a.Fehler.Add(new($"koerper «{k.Id}»", hf.Message, hf.Vorschlag)); a.Koerper.Add(new FamKoerperErgebnis { Def = k, Sichtbar = false }); }
        }

        // 3. Anschlüsse
        foreach (var an in f.Anschluesse)
        {
            try
            {
                bool sichtbar = an.Sichtbar is null || Z(an.Sichtbar) != 0;
                var lage = P(an.Lage);
                var r = new V3(Z(an.Richtung[0]), Z(an.Richtung[1]), Z(an.Richtung[2]));
                if (r.Laenge < 1e-9) throw new FeldFehler(new($"anschluss «{an.Name}».richtung", "Richtung ergibt [0, 0, 0]", "Richtung aus dem Gerät heraus angeben, z. B. [0, 0, -1]."));
                var medium = an.Medium;
                if (medium != "*" && f.Katalog?.Medien.ContainsKey(medium) != true && f.NachName.TryGetValue(medium, out var mp) && mp.Datentyp == "medium")
                {
                    medium = a.Werte.GetValueOrDefault(mp.Name)?.Text ?? "*";
                    if (medium != "*" && f.Katalog is { } kat && !kat.Medien.ContainsKey(medium))
                        throw new FeldFehler(new($"anschluss «{an.Name}».medium", $"Parameter «{mp.Name}» ergibt das Medium «{medium}», das es nicht gibt", "Medien: " + string.Join(", ", kat.Medien.Keys)));
                    // alte Kennung (KUE-VL, SOLE-RL …) in der heutigen Schreibweise (Kälte gehört zur Heizung, Entscheid Christian 20.10.2026)
                    if (medium != "*" && f.Katalog is { } kat2 && kat2.Medien.TryGetValue(medium, out var md)) medium = md.Kennung;
                }
                double? Groesse(FamZahl? z, string feld)
                {
                    if (z is null) return null;
                    var w = Z(z);
                    if (w <= 0) throw new FeldFehler(new($"anschluss «{an.Name}».{feld}", $"{feld} = {w} ist ungültig", $"{feld} > 0 angeben (mm bzw. DN)."));
                    return w;
                }
                // gewinde: genau ein Textparameter → dessen Wert für Typ und Exemplar (Belimo «rp»); sonst der Text, wie er ist («G 1/2»)
                var gewinde = an.Gewinde is { } gw && f.NachName.TryGetValue(gw.Trim(), out var gp) && gp.Datentyp == "text" ? a.Werte.GetValueOrDefault(gp.Name)?.Text : an.Gewinde;
                a.Anschluesse.Add(new FamAnschlussErgebnis(an.Name, medium, an.Fluss, an.Art, an.Form, Groesse(an.Dn, "dn"), Groesse(an.D, "d"),
                    Groesse(an.B, "b"), Groesse(an.H, "h"), gewinde, lage, r.Einheit().Feld(), sichtbar, an.Beschreibung));
            }
            catch (FeldFehler ff) { a.Fehler.Add(ff.Fehler); }
        }

        // 4. Sinnbild
        foreach (var s in f.Sinnbild ?? [])
        {
            try
            {
                bool sichtbar = s.Sichtbar is null || Z(s.Sichtbar) != 0;
                a.Sinnbild.Add(new FamZeichnungErgebnis(s, sichtbar, Zeichnen(s, Z)));
            }
            catch (FeldFehler ff) { a.Fehler.Add(ff.Fehler); }
        }

        // 5. Leitungsserie (Modul 3): Grösse, aussen, innen, wand, Biegeradius dieses Typs
        if (f.Leitung is { } l)
        {
            try
            {
                var g = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (schl, pn) in l.Groesse)
                {
                    var w = a.Werte.GetValueOrDefault(pn);
                    if (w is not { IstText: false } zw)
                        throw new FeldFehler(new($"leitung.groesse.{schl}", $"Die Grösse «{pn}» hat im Typ «{typ.Name}» keinen Wert", $"Wert im Typ angeben (typen.werte.{pn})."));
                    if (zw.Zahl <= 0) throw new FeldFehler(new($"leitung.groesse.{schl}", $"Grösse {pn} = {zw.Zahl} ist ungültig", "Grösse > 0 angeben (DN bzw. mm)."));
                    g[schl] = zw.Zahl;
                }
                double Positiv(FamZahl z, string feld)
                {
                    var v = Z(z);
                    if (v <= 0) throw new FeldFehler(new($"leitung.{feld}", $"{feld} = {v} mm ist ungültig (Typ «{typ.Name}»)", $"{feld} > 0 angeben (mm)."));
                    return v;
                }
                a.Leitung = new FamLeitungWerte(g, l.Aussen is null ? null : Positiv(l.Aussen, "aussen"), l.Innen is null ? null : Positiv(l.Innen, "innen"),
                    l.Wand is null ? null : Positiv(l.Wand, "wand"), l.Biegeradius is null ? null : Positiv(l.Biegeradius, "regeln.biegeradius"));
            }
            catch (FeldFehler ff) { a.Fehler.Add(ff.Fehler); }
        }
        return a;
    }

    static Primitiv Zeichnen(FamZeichnung s, Func<FamZahl?, double> Z)
    {
        var stil = new Stil { Stift = s.Stift, Linie = s.Linie, Fuellung = s.Fuellung };
        var punkte = s.Punkte.Select(p => (U: Z(p.U), V: Z(p.V), W: p.Woelbung is null ? 0 : Z(p.Woelbung))).ToList();
        switch (s.Art)
        {
            case "polygon":
                return new Primitiv { Art = "polygon", Punkte = NetzBau.Facetten(punkte).Select(q => new[] { q.U, q.V }).ToList(), Stil = stil };
            case "linie" or "polylinie":
            {
                // offener Zug: die Wölbung des letzten Punkts gilt nicht
                var offen = new List<double[]>();
                for (int i = 0; i < punkte.Count; i++)
                {
                    if (i == punkte.Count - 1 || Math.Abs(punkte[i].W) < 1e-12) { offen.Add([punkte[i].U, punkte[i].V]); continue; }
                    var bogen = NetzBau.Facetten([punkte[i], (punkte[i + 1].U, punkte[i + 1].V, 0)]);
                    // Facetten liefert den geschlossenen Zug Anfang → Bogen → Ende; ohne den Rückweg
                    int ende = bogen.FindIndex(q => Math.Abs(q.U - punkte[i + 1].U) < 1e-7 && Math.Abs(q.V - punkte[i + 1].V) < 1e-7);
                    foreach (var q in bogen.Take(ende < 0 ? bogen.Count : ende)) offen.Add([q.U, q.V]);
                }
                return new Primitiv { Art = offen.Count == 2 ? "linie" : "polylinie", Punkte = offen, Stil = stil };
            }
            case "kreis":
                return new Primitiv { Art = "kreis", Punkte = [[punkte[0].U, punkte[0].V]], Radius = Z(s.Radius), Stil = stil };
            case "bogen":
                return new Primitiv { Art = "bogen", Punkte = [[punkte[0].U, punkte[0].V]], Radius = Z(s.Radius), Von = Z(s.Von), Bis = Z(s.Bis), Stil = stil };
            default:   // text
                return new Primitiv { Art = "text", Punkte = [[punkte[0].U, punkte[0].V]], Text = s.Text, Groesse = s.Groesse ?? 2.0, Ausrichtung = "links", Stil = stil };
        }
    }

    /// <summary>Werte der Parameter für einen Typ und Exemplarwerte, mit Merkliste gegen Zyklen.</summary>
    sealed class Umgebung(FamilieDef f, FamTyp typ, JsonObject? exemplar) : IFormelUmgebung
    {
        readonly Dictionary<string, FWert?> fertig = new(StringComparer.Ordinal);
        readonly HashSet<string> fehlerhaft = new(StringComparer.Ordinal);
        readonly HashSet<string> inArbeit = new(StringComparer.Ordinal);

        public FWert? Hole(string name)
        {
            if (fertig.TryGetValue(name, out var w)) return w;
            if (fehlerhaft.Contains(name)) throw new FormelFehler($"«{name}» lässt sich selbst nicht rechnen", 0, $"Zuerst den Fehler bei «{name}» beheben.");
            var p = f.NachName[name];
            if (!inArbeit.Add(name)) throw new FormelFehler($"Zirkuläre Formel über «{name}»", 0, "Eine der Formeln durch einen Wert ersetzen.");
            try
            {
                FWert? r;
                if (p.Formel is not null) r = p.Passend(p.Formel.Werten(this), 0);
                else
                {
                    JsonNode? quelle; string woher;
                    if (p.Art == "exemplar" && exemplar is not null && exemplar.TryGetPropertyValue(name, out var en) && en is not null) (quelle, woher) = (en, "Exemplarwert");
                    else if (typ.Werte.TryGetValue(name, out var tn) && tn is not null) (quelle, woher) = (tn, $"Typ «{typ.Name}»");
                    else (quelle, woher) = (p.Wert, "Vorgabewert");
                    r = p.Umwandeln(quelle, out var fehler);
                    if (fehler is not null) throw new FormelFehler($"{woher}: {fehler}", 0);
                }
                fertig[name] = r;
                return r;
            }
            catch (FormelFehler) { fehlerhaft.Add(name); throw; }
            finally { inArbeit.Remove(name); }
        }

        public FWert Wert(string name, int stelle)
        {
            if (!f.NachName.ContainsKey(name)) throw new FormelFehler($"unbekannter Name «{name}»", stelle, Formel.NamensVorschlag(name, f.NachName.Keys));
            FWert? w;
            try { w = Hole(name); }
            catch (FormelFehler ff) { throw new FormelFehler($"«{name}»: {ff.Message}", stelle, ff.Vorschlag); }
            return w ?? throw new FormelFehler($"Parameter «{name}» hat keinen Wert", stelle,
                $"Wert im Typ «{typ.Name}» (typen.werte) oder einen Vorgabewert (wert) angeben.");
        }
    }
}

/// <summary>Grundkörper → Körperteil in Familienkoordinaten.</summary>
public static class FamilienKoerper
{
    public static Koerperteil Bauen(FamKoerper k, Func<FamZahl?, double> Z, Func<FamZahl[]?, double[]> P)
    {
        var ort = $"koerper «{k.Id}»";
        switch (k.Art)
        {
            case "quader":
            {
                var a = P(k.Von); var b = P(k.Bis);
                var masse = Enumerable.Range(0, 3).Select(i => Math.Abs(b[i] - a[i])).ToArray();
                for (int i = 0; i < 3; i++)
                    if (masse[i] < 1e-6) throw new HlksFehler($"{ort}: Quader hat keine Ausdehnung in {"xyz"[i]} (von = bis = {a[i]:0.###}).", "von und bis in jeder Richtung verschieden wählen.");
                return new Koerperteil
                {
                    Art = "quader", Mitte = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2], Masse = masse, Drehung = 0, Material = k.Material, Teil = k.Id,
                };
            }
            case "zylinder":
            {
                var von = P(k.Von); var nach = P(k.Nach); var r = Z(k.R);
                if (r <= 0) throw new HlksFehler($"{ort}: Radius {r:0.###} ist ungültig.", "Radius > 0 angeben.");
                if (Geometrie.Abstand(von, nach) < 1e-6) throw new HlksFehler($"{ort}: von und nach liegen aufeinander.", "Zwei verschiedene Punkte angeben.");
                return new Koerperteil { Art = "zylinder", Von = von, Nach = nach, Radius = r, Material = k.Material, Teil = k.Id };
            }
            case "kegel": return Netz(NetzBau.Kegel(P(k.Von), P(k.Nach), Z(k.R1), Z(k.R2)), k);
            case "kugel": return Netz(NetzBau.Kugel(P(k.Mitte), Z(k.R)), k);
            case "extrusion":
            {
                var kontur = NetzBau.Facetten(k.Kontur!.Select(p => (Z(p.U), Z(p.V), p.Woelbung is null ? 0 : Z(p.Woelbung))).ToList());
                var loecher = (k.Loecher ?? []).Select(l => NetzBau.Facetten(l.Select(p => (Z(p.U), Z(p.V), p.Woelbung is null ? 0 : Z(p.Woelbung))).ToList())).ToList();
                return Netz(NetzBau.Extrusion(k.Ebene!, kontur, loecher, Z(k.ExVon), Z(k.ExBis)), k);
            }
            case "rotation":
            {
                var kontur = NetzBau.Facetten(k.Kontur!.Select(p => (Z(p.U), Z(p.V), p.Woelbung is null ? 0 : Z(p.Woelbung))).ToList());
                var ap = P(k.AchsePunkt);
                return Netz(NetzBau.Rotation(k.Ebene!, kontur, (ap[0], ap[1]), k.AchseRichtung!, k.Winkel is null ? 360 : Z(k.Winkel)), k);
            }
            case "sweep":
            {
                var pfad = k.Pfad!.Select(P).ToList();
                (double, double)? rechteck = k.ProfilRechteck is null ? null : (Z(k.ProfilRechteck[0]), Z(k.ProfilRechteck[1]));
                return Netz(NetzBau.Sweep(pfad, k.ProfilKreis is null ? null : Z(k.ProfilKreis), rechteck, k.Biegeradius is null ? null : Z(k.Biegeradius)), k);
            }
            default: throw new HlksFehler($"{ort}: Körperart «{k.Art}» gibt es nicht.");
        }
    }

    static Koerperteil Netz(Netz3 n, FamKoerper k) => new() { Art = "koerper", Ecken = n.Ecken, Dreiecke = n.Dreiecke, Material = k.Material, Teil = k.Id };

    /// <summary>Jeder Körperteil als Netz (für Ansichten von oben/unten, die nur Netze kennen).</summary>
    public static Netz3 AlsNetz(Koerperteil t)
    {
        switch (t.Art)
        {
            case "quader":
            {
                var m = t.Mitte!; var s = t.Masse!;
                var n = NetzBau.Quader([-s[0] / 2, -s[1] / 2, -s[2] / 2], [s[0] / 2, s[1] / 2, s[2] / 2]);
                double w = (t.Drehung ?? 0) * Math.PI / 180, c = Math.Cos(w), sn = Math.Sin(w);
                return n.Transformiert(p => [m[0] + p[0] * c - p[1] * sn, m[1] + p[0] * sn + p[1] * c, m[2] + p[2]]);
            }
            case "zylinder": return NetzBau.Kegel(t.Von!, t.Nach!, t.Radius!.Value, t.Radius!.Value);
            case "kugel": return NetzBau.Kugel(t.Mitte!, t.Radius!.Value);
            case "koerper":
            {
                var n = new Netz3();
                foreach (var p in t.Ecken!) n.Ecken.Add(p);
                foreach (var d in t.Dreiecke!) n.Dreiecke.Add(d);
                return n;
            }
            default: throw new HlksFehler($"Körperteil «{t.Art}» lässt sich nicht als Netz darstellen.");
        }
    }

    /// <summary>Körperteil an den Einfügepunkt gesetzt und um z gedreht (Grad, Gegenuhrzeigersinn).</summary>
    public static Koerperteil Platzieren(Koerperteil t, double[] ein, double drehung, string? farbe)
    {
        double w = drehung * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        double[] W(double[] p) => [ein[0] + p[0] * c - p[1] * s, ein[1] + p[0] * s + p[1] * c, ein[2] + p[2]];
        var k = new Koerperteil { Art = t.Art, Material = t.Material, Farbe = farbe ?? t.Farbe, Deckkraft = t.Deckkraft, NichtSchneiden = t.NichtSchneiden, Teil = t.Teil };
        switch (t.Art)
        {
            case "quader": k.Mitte = W(t.Mitte!); k.Masse = t.Masse; k.Drehung = (t.Drehung ?? 0) + drehung; break;
            case "zylinder": k.Von = W(t.Von!); k.Nach = W(t.Nach!); k.Radius = t.Radius; break;
            case "kugel": k.Mitte = W(t.Mitte!); k.Radius = t.Radius; break;
            case "koerper":
                var ecken = new List<double[]>(t.Ecken!.Count);
                foreach (var p in t.Ecken!) ecken.Add(W(p));
                k.Ecken = ecken; k.Dreiecke = t.Dreiecke;   // Dreiecke werden geteilt (unveränderlich)
                break;
        }
        return k;
    }

    /// <summary>Körperteil in die Einbaulage gesetzt (volle Ausrichtung, Modul 3). Ist die Lage eine Drehung um z, gilt die Drehung
    /// wie bisher (Quader bleiben Quader); sonst werden Quader zum Netz, Zylinder, Kugeln und Netze werden transformiert.</summary>
    public static Koerperteil Platzieren(Koerperteil t, Einbaulage l, string? farbe)
    {
        if (l.Senkrecht) return Platzieren(t, l.Ursprung, l.DrehungZ, farbe);
        var k = new Koerperteil { Art = t.Art, Material = t.Material, Farbe = farbe ?? t.Farbe, Deckkraft = t.Deckkraft, NichtSchneiden = t.NichtSchneiden, Teil = t.Teil };
        switch (t.Art)
        {
            case "quader":
            {
                var n = AlsNetz(t);
                k.Art = "koerper";
                k.Ecken = n.Ecken.Select(l.Welt).ToList();
                k.Dreiecke = n.Dreiecke;
                break;
            }
            case "zylinder": k.Von = l.Welt(t.Von!); k.Nach = l.Welt(t.Nach!); k.Radius = t.Radius; break;
            case "kugel": k.Mitte = l.Welt(t.Mitte!); k.Radius = t.Radius; break;
            case "koerper":
                var ecken = new List<double[]>(t.Ecken!.Count);
                foreach (var p in t.Ecken!) ecken.Add(l.Welt(p));
                k.Ecken = ecken; k.Dreiecke = t.Dreiecke;   // Dreiecke werden geteilt (unveränderlich); Lage ist rechtshändig, die Orientierung bleibt
                break;
        }
        return k;
    }

    /// <summary>Punkt aus Familien- in Weltkoordinaten.</summary>
    public static double[] Welt(double[] p, double[] ein, double drehung)
    {
        double w = drehung * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return [ein[0] + p[0] * c - p[1] * s, ein[1] + p[0] * s + p[1] * c, ein[2] + (p.Length > 2 ? p[2] : 0)];
    }

    /// <summary>Richtung aus Familien- in Weltkoordinaten (nur gedreht).</summary>
    public static double[] Richtung(double[] d, double drehung)
    {
        double w = drehung * Math.PI / 180, c = Math.Cos(w), s = Math.Sin(w);
        return [d[0] * c - d[1] * s, d[0] * s + d[1] * c, d[2]];
    }
}

/// <summary>Umriss von oben: Vereinigung der Projektionen der Körper auf xy (Clipper2).</summary>
public static class FamilienUmriss
{
    public static List<Flaeche> Rechnen(IEnumerable<FamKoerperErgebnis> koerper) => Rechnen(koerper.Select(k => k.Teil!));

    /// <summary>Umriss von oben aus Körperteilen in beliebigen Koordinaten (z. B. schon in die Einbaulage gesetzt, Modul 3).</summary>
    public static List<Flaeche> Rechnen(IEnumerable<Koerperteil> teile)
    {
        var pfade = new PathsD();
        foreach (var t in teile)
        {
            switch (t.Art)
            {
                case "quader":
                {
                    var m = t.Mitte!; var s = t.Masse!;
                    double w = (t.Drehung ?? 0) * Math.PI / 180, c = Math.Cos(w), sn = Math.Sin(w);
                    var ring = new PathD();
                    foreach (var (px, py) in new[] { (-s[0] / 2, -s[1] / 2), (s[0] / 2, -s[1] / 2), (s[0] / 2, s[1] / 2), (-s[0] / 2, s[1] / 2) })
                        ring.Add(new PointD(m[0] + px * c - py * sn, m[1] + px * sn + py * c));
                    pfade.Add(Polygone.Ccw(ring));
                    break;
                }
                case "zylinder":
                {
                    // Projektion = konvexe Hülle der beiden Endkreise
                    var a = V3.Aus(t.Von!); var b = V3.Aus(t.Nach!);
                    var (e1, e2) = (b - a).Einheit().Senkrechte();
                    var pts = new List<PointD>();
                    for (int i = 0; i < NetzBau.Teilung; i++)
                    {
                        double w = 2 * Math.PI * i / NetzBau.Teilung;
                        var q = e1 * (t.Radius!.Value * Math.Cos(w)) + e2 * (t.Radius!.Value * Math.Sin(w));
                        pts.Add(new PointD(a.X + q.X, a.Y + q.Y)); pts.Add(new PointD(b.X + q.X, b.Y + q.Y));
                    }
                    var huelle = Huelle(pts);
                    if (huelle.Count >= 3 && Math.Abs(Clipper.Area(huelle)) > 1e-6) pfade.Add(Polygone.Ccw(huelle));
                    break;
                }
                case "koerper":
                {
                    var e = t.Ecken!;
                    foreach (var d in t.Dreiecke!)
                    {
                        var p0 = e[d[0]]; var p1 = e[d[1]]; var p2 = e[d[2]];
                        double flaeche = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p1[1] - p0[1]) * (p2[0] - p0[0]);
                        if (flaeche <= 1e-6) continue;   // nur nach oben zeigende Dreiecke (geschlossenes Netz: decken die Projektion ganz)
                        pfade.Add([new PointD(p0[0], p0[1]), new PointD(p1[0], p1[1]), new PointD(p2[0], p2[1])]);
                    }
                    break;
                }
            }
        }
        return pfade.Count == 0 ? [] : Polygone.Flaechen(pfade);
    }

    static PathD Huelle(List<PointD> p)
    {
        var s = p.OrderBy(q => q.x).ThenBy(q => q.y).ToList();
        if (s.Count < 3) return new PathD(s);
        double Kreuz(PointD o, PointD a, PointD b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<PointD>();
        foreach (var q in s) { while (h.Count >= 2 && Kreuz(h[^2], h[^1], q) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(q); }
        int unten = h.Count + 1;
        for (int i = s.Count - 2; i >= 0; i--) { var q = s[i]; while (h.Count >= unten && Kreuz(h[^2], h[^1], q) <= 1e-9) h.RemoveAt(h.Count - 1); h.Add(q); }
        h.RemoveAt(h.Count - 1);
        return new PathD(h);
    }
}
