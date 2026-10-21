// Leitungen (Modul 3, B1): Leitungsserie = Familie der Kategorie Rohr bzw. Kanal mit Block «leitung» (entwurf/modul-3.md,
// Grundsatzentscheid 2). Die Typen sind die Grössen. Diese Datei liest eine Serie aus einem Familie-Element (Querschnitt, Grösse,
// aussen, innen, wand je Typ, Winkelregel, Biegeradius), findet den Typ zu einer Grösse und wählt das Formteil (Formteilwahl) –
// die Grundlage für das Routing (B2: leitung_zeichnen), die Abfrage «leitungsserie» und die Prüfungen.
//
// Formteilwahl (Entscheid Hauptlauf 18.10.2026): Die Formteil-Familie wird im Projekt über ihre Definitions-id gefunden. Ohne
// Typregel gilt der Typ, dessen AUSGEWERTETE Anschlussgrössen passen – Bogen a = b = Grösse, T-Stück a = b = Durchgang und
// c = Abzweig, Reduktion a = gross und b = klein, Reinigungsöffnung a = b = Grösse (Annahme wie Bogen) –, nicht über
// Parameternamen. Mit Typregel (Formel) ergibt diese den Typnamen; Namen der Formel sind die Grössenparameter der Serie, für
// T-Stück-Abzweig bzw. Reduktion klein mit der Endung «_2». Danach wird die lokale Lage der Anschlüsse geprüft
// (Formteilgeometrie, Grundsatzentscheid 3) – eine abweichende Formteil-Familie wird abgelehnt.
// ANNAHMEN (Prüfliste): passen mehrere Typen, gilt der erste der Typenliste (Hinweis); Grössen gelten als gleich bis 1e-6;
//   ein Bogenwinkel wird mit dem Winkel aus der Richtung von Anschluss b verglichen (bis 1e-6°); fehlt regeln.winkel, ist
//   jeder Winkel erlaubt (wie «frei»); die Reduktion wird nach der Grösse geordnet (a = grössere), egal in welcher Reihenfolge
//   die Grössen kommen.
//
// Abzweigwinkel (Nachtrag B7, Entscheid Hauptlauf 19.10.2026, entwurf/modul-3.md «Lücke Abzweigwinkel»): T-Stück-Familien dürfen
// einen Parameter «winkel» haben (Abzweigwinkel w, 0 < w ≤ 90; ohne Parameter 90). Lokale Lage des Abzweigs c bei
// Lc·(−cos w, sin w, 0), Richtung (−cos w, sin w, 0) – w = 90 ergibt die bisherige Lage (0, Lc, 0)/(0, 1, 0); bei w < 90 zeigt c gegen
// die Durchgangsrichtung a→b (Zulauf in Fliessrichtung). Die Serie nennt die erlaubten Winkel in regeln.abzweigwinkel (fehlt = [90]).
// Formteilwahl «tstueck» mit Winkel: der Winkel der Liste bis ±Routing.WinkelToleranz (ohne Angabe 90), sonst abgelehnt mit den
// erlaubten Winkeln; der Typ muss c unter genau diesem Winkel haben (aus der Richtung von c, bis 1e-6°); Exemplar { winkel }, wenn
// die Familie winkel als Exemplarparameter (ohne Formel) hat.
// ANNAHMEN (Prüfliste, B7): der Abzweigwinkel-Parameter heisst «winkel» (Klasse TStueck hat keine Eigenschaft winkel, ein ziel ist
//   dort nicht möglich; ziel «winkel» würde auch gelten); er darf Typ- oder Exemplarparameter sein, auch mit Formel.
//
// Freie Grösse (Nachtrag Modul 3, entwurf/freie-groesse-kanal.md; Entscheid Christian 20.10.2026: «Rechteckkanäle mit freier Grösse
// b × h (Normraster nur als Vorschlag); Formteile rechnen ihre Masse aus der Formel»): Kanalserien mit leitung.frei nehmen jede Grösse
// innerhalb von Grenzen und Schritt. TypFuer: Rastertyp hat Vorrang, sonst eine virtuelle Grösse – Typ = Vorlage, Masse aus der
// Vorlage mit ersetzten Grössenparametern (FamilienAuswertung.Aufloesen mit typwerte). Formteilwahl: passt kein Typ, wird der
// Vorlage-Typ der Formteil-Familie (formteile.<art>.vorlage, fehlt = erster Typ; mit Typregel deren Typ) mit Exemplarwerten
// ausgewertet – jeder Grössenwert eines Anschlusses, der direkt ein Exemplarparameter ohne Formel ist, wird die Soll-Grösse; danach
// Passt, Lage und Winkel wie bisher. Die Leitung trägt familientyp = Vorlage und breite/hoehe bzw. durchmesser = tatsächliche Grösse
// (FreieGroesse.cs), das Formteil familientyp = Vorlage und die Exemplarwerte in parameter.
// ANNAHMEN (Prüfliste, freie Grösse): Schritt = Vielfache des Schritts ab 0 (ganze mm, weil Anschlussgrössen ganze mm sind); ein
//   Formteil-Grössenwert als Konstante muss schon stimmen; die Probe der Abfrage nimmt je Schlüssel die Mitte der Grenzen (gerundet
//   auf den Schritt), ohne beide Grenzen 430 × 270 bzw. d 430.
//
// Abwasser-Serie (Nachtrag Modul 3, entwurf/abwasser-serie.md; Entscheid Christian 20.10.2026: «Geberit PE ist immer Abwasser»): Rohrserien
// mit leitung.einsatz = abwasser werden immer nach Aussendurchmesser beschriftet (Rohrgroessen.Von, unabhängig vom Medium; P30 meldet sie in
// einem anderen Medium) und dürfen die Grösse d = Aussendurchmesser haben (groesse { d }, keine DN nötig – d32 wird zeichenbar). Dann ist d die
// Grösse für Typwahl, Formteilwahl (Anschlüsse der Formteile mit d), Routing und Befehle ({ "d": 110 }, Meldungen «d 110»:
// Leitungsgroesse.Aussendurchmesser); das Rohr trägt durchmesser (Aussendurchmesser), dn nur als Herstellerangabe aus einem Typparameter mit
// ziel dn. Regel Christian: DN wird nie aus dem Aussendurchmesser abgeleitet (und nie umgekehrt). Eine Leitung mit DN bzw. ohne Grösse kommt
// über ihren Aussendurchmesser (Typ ihrer Serie bzw. Leitung.da, z. B. aus dem IFC-Import) in eine solche Serie (GroesseFuer).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Grösse einer Leitung bzw. eines Anschlusses: Rohr DN, Kanal rund d (mm), Kanal rechteckig b × h (mm); Rohr einer
/// Abwasser-Serie nach Aussendurchmesser d (mm, <see cref="Aussendurchmesser"/>).</summary>
public readonly record struct Leitungsgroesse(double? Dn, double? D, double? B, double? H)
{
    public const double Toleranz = 1e-6;

    public static Leitungsgroesse Rohr(double dn) => new(dn, null, null, null);
    public static Leitungsgroesse Rund(double d) => new(null, d, null, null);
    public static Leitungsgroesse Rechteck(double b, double h) => new(null, null, b, h);
    /// <summary>Aussendurchmesser eines Rohrs einer Abwasser-Serie (Nachtrag, entwurf/abwasser-serie.md): d in mm, Text «d 110».</summary>
    public static Leitungsgroesse Aussen(double d) => new(null, d, null, null) { Aussendurchmesser = true };

    /// <summary>D ist der Aussendurchmesser eines Rohrs (Abwasser-Serie nach Aussendurchmesser, Rohranschluss mit d) – nur für Texte
    /// («d 110» statt «Ø 110 mm»); Vergleiche (Gleich, Equals) ignorieren das Kennzeichen, die Zahl ist dieselbe.</summary>
    public bool Aussendurchmesser { get; init; }

    public bool Equals(Leitungsgroesse o) => Dn == o.Dn && D == o.D && B == o.B && H == o.H;
    public override int GetHashCode() => HashCode.Combine(Dn, D, B, H);

    /// <summary>Keine Grösse (weder dn noch d noch b und h).</summary>
    public bool Leer => Dn is null && D is null && (B is null || H is null);

    static bool Gl(double? x, double? y) => x is null ? y is null : y is not null && Math.Abs(x.Value - y.Value) <= Toleranz;

    /// <summary>Gleiche Grösse (gleiche Art, Werte bis 1e-6).</summary>
    public bool Gleich(Leitungsgroesse o) => Gl(Dn, o.Dn) && Gl(D, o.D) && Gl(B, o.B) && Gl(H, o.H);

    /// <summary>Zum Ordnen (Reduktion: grössere zuerst): DN, d bzw. Fläche b·h.</summary>
    public double Mass => Dn ?? D ?? (B is double b && H is double h ? b * h : 0);

    public static string Zahl(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    public override string ToString() =>
        Dn is double dn ? $"DN {Zahl(dn)}" : D is double d ? (Aussendurchmesser ? $"d {Zahl(d)}" : $"Ø {Zahl(d)} mm") : B is double b && H is double h ? $"{Zahl(b)} × {Zahl(h)} mm" : "ohne Grösse";

    /// <summary>Kurz ohne Einheit: «430 × 270», «Ø 250», «DN 20», «d 110» (Abwasser-Serie) – Name einer freien Grösse in Listen, Mengen und
    /// im IFC-Typobjekt.</summary>
    public string Kurz =>
        Dn is double dn ? $"DN {Zahl(dn)}" : D is double d ? (Aussendurchmesser ? $"d {Zahl(d)}" : $"Ø {Zahl(d)}") : B is double b && H is double h ? $"{Zahl(b)} × {Zahl(h)}" : "ohne Grösse";

    /// <summary>Wert je Grössenschlüssel (dn | d | b | h) oder null.</summary>
    public double? Wert(string schluessel) => schluessel switch { "dn" => Dn, "d" => D, "b" => B, "h" => H, _ => null };

    /// <summary>Grösse aus einem Text «430x270», «430 × 270» bzw. «Ø 250» (Kanäle, freie Grösse) bzw. «d 110» (Aussendurchmesser einer
    /// Abwasser-Serie, kleines d) oder null.</summary>
    public static Leitungsgroesse? AusText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace(',', '.');
        if (t.EndsWith("mm", StringComparison.OrdinalIgnoreCase)) t = t[..^2].Trim();
        static double? Z(string s) => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && x > 0 && double.IsFinite(x) ? x : null;
        var teile = t.Split(['x', 'X', '×', '*'], StringSplitOptions.TrimEntries);
        if (teile.Length == 2 && Z(teile[0]) is double b && Z(teile[1]) is double h) return Rechteck(b, h);
        if ((t.StartsWith('Ø') || t.StartsWith('ø')) && Z(t[1..]) is double d) return Rund(d);
        if (t.StartsWith('d') && Z(t[1..]) is double da) return Aussen(da);   // «d 110» – nicht «DN 100» (gross, mit N)
        return null;
    }

    /// <summary>{ dn } | { d } | { b, h } (leer = null).</summary>
    public JsonObject? AlsJson()
    {
        if (Leer) return null;
        var o = new JsonObject();
        if (Dn is double dn) o["dn"] = dn;
        if (D is double d) o["d"] = d;
        if (B is double b && H is double h) { o["b"] = b; o["h"] = h; }
        return o;
    }

    /// <summary>Aus { dn } | { d } | { b, h } bzw. einer Zahl (DN) – für Abfragen und Befehle.</summary>
    public static Leitungsgroesse Lesen(JsonNode? n, string wer = "groesse")
    {
        const string Form = "Grösse als { \"dn\": 20 } (Rohr), { \"d\": 110 } (Aussendurchmesser einer Abwasser-Serie bzw. Kanal rund, mm) bzw. { \"b\": 400, \"h\": 200 } (Kanal rechteckig) bzw. als Zahl (DN).";
        if (n is JsonValue v && Json.Zahl(v) is double z) return Rohr(z);
        if (n is not JsonObject o) throw new HlksFehler($"{wer} fehlt oder ist keine Grösse.", Form);
        foreach (var (feld, _) in o)
            if (feld is not ("dn" or "d" or "b" or "h")) throw new HlksFehler($"{wer}: Feld «{feld}» gibt es nicht.", Form);
        double? W(string f) => o[f] is null ? null : Json.Zahl(o[f]) is double x && x > 0 && double.IsFinite(x) ? x : throw new HlksFehler($"{wer}.{f} muss eine Zahl > 0 sein.", Form);
        var g = new Leitungsgroesse(W("dn"), W("d"), W("b"), W("h"));
        int arten = (g.Dn is null ? 0 : 1) + (g.D is null ? 0 : 1) + (g.B is null && g.H is null ? 0 : 1);
        if (arten != 1 || (g.B is null) != (g.H is null)) throw new HlksFehler($"{wer}: genau eine Grösse angeben.", Form);
        return g;
    }

    /// <summary>Grösse aus den Werten eines Formteil- bzw. Familienanschlusses; ein Rohranschluss mit d (ohne dn) trägt den
    /// Aussendurchmesser (Formteil einer Abwasser-Serie nach Aussendurchmesser).</summary>
    public static Leitungsgroesse Von(FamAnschlussErgebnis a) =>
        new(a.Dn, a.D, a.B, a.H) { Aussendurchmesser = a.Art == "rohr" && a.D is not null && a.Dn is null };
}

/// <summary>Eine Grösse (= ein Typ) einer Leitungsserie mit ihren Massen in mm (null = nicht angegeben). Fehler = der Typ lässt
/// sich nicht auswerten (dann sind die Masse null). Frei = virtuelle Grösse einer freien Kanalserie (leitung.frei): Typ ist dann
/// die Vorlage, Grösse die gewünschte, die Masse aus der Vorlage mit ersetzten Grössenparametern.</summary>
public sealed record SerienGroesse(string Typ, Leitungsgroesse Groesse, double? Aussen, double? Innen, double? Wand, double? Biegeradius, string? Fehler, bool Frei = false)
{
    public bool Gueltig => Fehler is null;
    /// <summary>Für Meldungen: Typname bzw. «430 × 270 (frei, Vorlage 400x200)».</summary>
    public string Text => Frei ? $"{Groesse.Kurz} (frei, Vorlage {Typ})" : Typ;
}

/// <summary>Eine Leitungsserie (Familie der Kategorie Rohr bzw. Kanal) mit ihren Grössen, Regeln und der Formteilwahl.</summary>
public sealed class Leitungsserie
{
    public Element Familie { get; }
    public FamilieDef Def { get; }
    public FamLeitung Block => Def.Leitung!;
    /// <summary>Kategorie der Serie (Rohr bzw. Kanal).</summary>
    public string Kategorie => Def.Kategorie;
    /// <summary>rund | rechteck</summary>
    public string Querschnitt => Block.Querschnitt;
    /// <summary>Alle Grössen in der Reihenfolge der Typenliste (auch nicht auswertbare, mit Fehler).</summary>
    public IReadOnlyList<SerienGroesse> Groessen { get; }

    Leitungsserie(Element familie, FamilieDef def, IReadOnlyList<SerienGroesse> groessen) { Familie = familie; Def = def; Groessen = groessen; }

    /// <summary>Bezeichnung für Meldungen: «FAM-1 «Name»».</summary>
    public string Bezeichnung => $"{Familie.Kennzeichen} «{Familie.Name ?? Def.Name}»";

    /// <summary>Serie aus einem Familie-Element; HlksFehler, wenn es keine gültige Leitungsserie ist.</summary>
    public static Leitungsserie Von(Modell m, Katalog k, Element familie)
    {
        if (familie.Klasse != Familien.Klasse)
            throw new HlksFehler($"{familie.Kennzeichen} ist keine Familie, sondern {familie.Klasse}.", "Eine Leitungsserie angeben (Familie der Kategorie Rohr bzw. Kanal); familien listet sie.");
        var def = Familien.Definition(familie, k);
        if (!def.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {familie.Kennzeichen} «{familie.Name}»", def.Fehler);
        if (!def.IstLeitungsserie)
            throw new HlksFehler($"{familie.Kennzeichen} «{familie.Name}» ist keine Leitungsserie (Kategorie {def.Kategorie}).",
                "Eine Familie der Kategorie Rohr bzw. Kanal mit Block «leitung» angeben: " + Serien(m, k));
        var groessen = new List<SerienGroesse>();
        foreach (var t in def.Typen)
        {
            var a = FamilienAuswertung.Aufloesen(def, t.Name, null);
            if (!a.Gueltig || a.Leitung is null)
            {
                groessen.Add(new SerienGroesse(t.Name, default, null, null, null, null, a.Fehler.Count > 0 ? $"{a.Fehler[0].Stelle}: {a.Fehler[0].Meldung}" : "nicht auswertbar"));
                continue;
            }
            var w = a.Leitung;
            groessen.Add(new SerienGroesse(t.Name, AusSchluesseln(w.Groesse, def.Leitung), w.Aussen, w.Innen, w.Wand, w.Biegeradius, null));
        }
        return new Leitungsserie(familie, def, groessen);
    }

    /// <summary>Serie über Kennzeichen, Name oder id der Familie.</summary>
    public static Leitungsserie Hole(Modell m, Katalog k, string? bezug) => Von(m, k, Familien.Hole(m, bezug));

    /// <summary>Serie einer Leitung (bezuege.familie) oder null (keine Serie, Familie fehlt oder ist keine gültige Serie).</summary>
    public static Leitungsserie? VonLeitung(Modell m, Katalog k, Element leitung)
    {
        if (Familien.FamilieVon(m, leitung) is not { } f) return null;
        var def = Familien.Definition(f, k);
        if (!def.Gueltig || !def.IstLeitungsserie) return null;
        try { return Von(m, k, f); }
        catch (HlksFehler) { return null; }
    }

    /// <summary>Leitungsserien des Projekts als Text für Vorschläge.</summary>
    public static string Serien(Modell m, Katalog k)
    {
        var l = m.DerKlasse(Familien.Klasse).Where(f => Familien.Definition(f, k) is { Gueltig: true, IstLeitungsserie: true })
            .OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(f => $"{f.Kennzeichen} «{f.Name}»").ToList();
        return l.Count > 0 ? string.Join(", ", l) : "im Projekt gibt es noch keine – mit familie_laden bzw. familie_entwurf anlegen.";
    }

    /// <summary>Grösse aus den Schlüsseln dn | d | b, h; bei einer Abwasser-Serie nach Aussendurchmesser (l.NachAussendurchmesser) ist
    /// d der Aussendurchmesser («d 110»).</summary>
    public static Leitungsgroesse AusSchluesseln(IReadOnlyDictionary<string, double> g, FamLeitung? l = null) =>
        new(g.TryGetValue("dn", out var dn) ? dn : null, g.TryGetValue("d", out var d) ? d : null, g.TryGetValue("b", out var b) ? b : null, g.TryGetValue("h", out var h) ? h : null)
        { Aussendurchmesser = l?.NachAussendurchmesser == true && g.ContainsKey("d") };

    /// <summary>Die Grösse hat die Art dieser Serie (Rohr: DN; Abwasser-Serie nach Aussendurchmesser bzw. Kanal rund: d; Kanal rechteckig: b und h).</summary>
    public bool PasstArt(Leitungsgroesse g) => Block.Groesse.Keys.All(s => s switch { "dn" => g.Dn is not null, "d" => g.D is not null, "b" => g.B is not null, "h" => g.H is not null, _ => false })
        && (Block.Groesse.ContainsKey("dn") || g.Dn is null) && (Block.Groesse.ContainsKey("d") || g.D is null) && (Block.Groesse.ContainsKey("b") || g.B is null);

    /// <summary>Abwasser-Serie (leitung.einsatz abwasser, Nachtrag entwurf/abwasser-serie.md): Beschriftung immer nach Aussendurchmesser.</summary>
    public bool Abwasser => Block.Abwasser;
    /// <summary>Abwasser-Serie mit Grösse d = Aussendurchmesser (keine DN nötig).</summary>
    public bool NachAussendurchmesser => Block.NachAussendurchmesser;

    /// <summary>Die Grösse in der Schreibweise dieser Serie: bei einer Abwasser-Serie nach Aussendurchmesser ist d der Aussendurchmesser
    /// («d 110»), bei allen anderen Serien nie (Kanal rund «Ø 250 mm», auch wenn die Eingabe «d 250» lautete). Für Eingaben aus Befehlen
    /// ({ "d": 110 }) vor Meldungen; die Zahl bleibt dieselbe.</summary>
    public Leitungsgroesse Norm(Leitungsgroesse g) =>
        g.D is null ? g : NachAussendurchmesser != g.Aussendurchmesser ? g with { Aussendurchmesser = NachAussendurchmesser } : g;

    /// <summary>Wie eine Grösse dieser Serie angegeben wird.</summary>
    public string GroessenForm => Block.Groesse.ContainsKey("dn") ? "{ \"dn\": … } bzw. eine Zahl (DN)"
        : NachAussendurchmesser ? "{ \"d\": … } (Aussendurchmesser in mm) bzw. eine Zahl oder «d 110»"
        : Block.Groesse.ContainsKey("d") ? "{ \"d\": … } (mm)" : "{ \"b\": …, \"h\": … } (mm)";

    /// <summary>Typ (Grösse) der Serie zu einer Grösse oder null – siehe <see cref="TypFuer(Leitungsgroesse, out string?)"/>.</summary>
    public SerienGroesse? TypFuer(Leitungsgroesse g) => TypFuer(g, out _);

    /// <summary>Typ zu einer Grösse: der Rastertyp mit genau dieser Grösse (er hat Vorrang); sonst bei einer freien Serie
    /// (leitung.frei) innerhalb von Grenzen und Schritt die virtuelle Grösse (Typ = Vorlage, Frei = true, Masse aus der Vorlage mit
    /// ersetzten Grössenparametern); sonst null mit Grund (bei freien Serien mit Grenzen, Schritt und Vorschlag).</summary>
    public SerienGroesse? TypFuer(Leitungsgroesse g, out string? grund)
    {
        grund = null;
        g = Norm(g);
        if (Groessen.FirstOrDefault(x => x.Gueltig && x.Groesse.Gleich(g)) is { } t) return t;
        if (Frei is null || !PasstArt(g))
        {
            grund = PasstArt(g) || g.Leer ? $"Die Serie {Bezeichnung} hat keine Grösse {g}." : $"Die Grösse {g} passt nicht zur Serie {Bezeichnung} ({Kategorie}, {Querschnitt}).";
            return null;
        }
        if (Grenzverletzung(g) is { } ausser) { grund = $"Die Grösse {g} liegt ausserhalb der Grenzen der Serie {Bezeichnung}: {ausser}."; return null; }
        if (!ImSchritt(g)) { grund = $"Die Grösse {g} liegt nicht im Schritt der Serie {Bezeichnung} ({FreiText}) – gerundet {Gerundet(g)}."; return null; }
        var v = Virtuell(g);
        if (!v.Gueltig) { grund = $"Die freie Grösse {g} der Serie {Bezeichnung} lässt sich aus der Vorlage «{Frei.Vorlage}» nicht rechnen: {v.Fehler}"; return null; }
        return v;
    }

    /// <summary>Wie <see cref="TypFuer(Leitungsgroesse, out string?)"/>, wirft sonst HlksFehler mit Grund und Vorschlag (ort = Präfix der Meldung).</summary>
    public SerienGroesse Typ(Leitungsgroesse g, string ort = "") => TypFuer(g, out var grund) ?? throw new HlksFehler(ort + grund, GroessenVorschlag(g));

    /// <summary>Vorschlag zu einer Grösse, die es nicht gibt: ohne freie Grösse die Liste der Grössen; mit freier Grösse die gerundete
    /// Grösse innerhalb der Grenzen und die nächsten Normgrössen.</summary>
    public string GroessenVorschlag(Leitungsgroesse g)
    {
        g = Norm(g);
        if (Frei is null || !PasstArt(g))
            return $"Grössen: {GroessenText} (leitungsserie zeigt sie mit ihren Formteilen)."
                + (NachAussendurchmesser && g.Dn is not null ? " Die Serie ist nach Aussendurchmesser geführt (Abwasser): Grösse als { \"d\": … } angeben – eine DN wird nie in einen Aussendurchmesser umgerechnet." : "");
        var naechste = Groessen.Where(x => x.Gueltig).OrderBy(x => Abstand(x.Groesse, g)).Take(3).Select(x => x.Groesse.Kurz).ToList();
        return $"Vorschlag: {Gerundet(g).Kurz} (innerhalb der Grenzen, im Schritt){(naechste.Count > 0 ? " oder die Normgrösse " + string.Join(", ", naechste) : "")} – {FreiText}.";
    }

    /// <summary>Grösse über den Typnamen oder null.</summary>
    public SerienGroesse? Groesse(string typ) => Groessen.FirstOrDefault(x => x.Typ == typ);

    // ------------------------------------------------------------------ Abwasser-Serie (Nachtrag, entwurf/abwasser-serie.md)

    /// <summary>DN des Herstellers zu einer Grösse einer Abwasser-Serie nach Aussendurchmesser: Wert des Typparameters mit ziel dn
    /// (Herstellerangabe, nie abgeleitet); null = keiner bzw. ohne Wert. Bei DN-geführten Serien null (dort ist die DN die Grösse).</summary>
    public double? DnHersteller(SerienGroesse g)
    {
        if (!NachAussendurchmesser || !g.Gueltig) return null;
        var p = Def.Parameter.FirstOrDefault(x => string.Equals(x.Ziel, "dn", StringComparison.OrdinalIgnoreCase));
        if (p is null) return null;
        var a = FamilienAuswertung.Aufloesen(Def, g.Typ, null);
        return a.Gueltig && a.Wert(p.Name) is { IstText: false } w && w.Zahl > 0 ? w.Zahl : null;
    }

    /// <summary>Aussendurchmesser einer Leitung (mm) ohne Umrechnung: aus dem Typ ihrer Serie (d einer Abwasser-Serie nach
    /// Aussendurchmesser, sonst aussen), sonst Leitung.da (z. B. IFC-Import: OuterDiameter), sonst Rohr.durchmesser; null = unbekannt.
    /// Nie aus einer DN gerechnet.</summary>
    public static double? AussendurchmesserVon(Modell m, Katalog k, Element leitung)
    {
        if (VonLeitung(m, k, leitung) is { } s && s.GroesseVon(leitung) is { Gueltig: true } sg)
        {
            if (sg.Groesse.Aussendurchmesser && sg.Groesse.D is double d && d > 0) return d;
            if (sg.Aussen is double a && a > 0 && s.Kategorie == "Rohr") return a;
        }
        if (leitung.Zahl("da") is double da && da > 0 && double.IsFinite(da)) return da;
        if (!k.IstArt(leitung, "Kanal") && leitung.Zahl("durchmesser") is double dm && dm > 0 && double.IsFinite(dm)) return dm;
        return null;
    }

    /// <summary>Grösse einer bestehenden Leitung in der Art dieser Serie (leitung_aufteilen, leitung_serie): ihre Grösse (Anschlusslagen);
    /// ist diese Serie nach Aussendurchmesser geführt und die Grösse eine DN bzw. keine, ihr Aussendurchmesser (<see cref="AussendurchmesserVon"/> –
    /// so kommt eine importierte Abwasserleitung über den Aussendurchmesser in die Serie). Nie aus einer DN gerechnet; ohne Aussendurchmesser
    /// bleibt die Grösse, wie sie ist (der Aufrufer lehnt mit Grund ab).</summary>
    public Leitungsgroesse GroesseFuer(Modell m, Katalog k, Element leitung)
    {
        var g = Anschlusslagen.LeitungGroesse(m, k, leitung);
        if (!NachAussendurchmesser || PasstArt(g)) return Norm(g);
        return AussendurchmesserVon(m, k, leitung) is double da ? Leitungsgroesse.Aussen(da) : g;
    }

    /// <summary>Grösse einer Leitung dieser Serie: freie Grösse (familientyp = Vorlage, Grösse aus den Eigenschaften, siehe
    /// FreieGroesse.Von) bzw. der Typ nach familientyp; null = ohne bzw. unbekannter Typ.</summary>
    public SerienGroesse? GroesseVon(Element leitung) =>
        FreieGroesse.Von(Def, leitung, Familie.Name ?? Def.Name) is { } g ? Virtuell(g) : Familien.Typ(leitung) is { } typ ? Groesse(typ) : null;

    /// <summary>Gültige Grössen als Text, z. B. «DN 15, DN 20»; bei freien Serien «frei b × h (145 … 2500 mm, Schritt 1 mm), Vorschlag:
    /// 500 × 300, 400 × 200 …» (gekürzt auf 8 Normgrössen).</summary>
    public string GroessenText
    {
        get
        {
            var gueltig = Groessen.Where(x => x.Gueltig).ToList();
            if (Frei is null) return string.Join(", ", gueltig.Select(x => x.Groesse.ToString()));
            const int Max = 8;
            var raster = string.Join(", ", gueltig.Take(Max).Select(x => x.Groesse.Kurz)) + (gueltig.Count > Max ? " …" : "");
            return gueltig.Count == 0 ? FreiText! : $"{FreiText}, Vorschlag: {raster}";
        }
    }

    // ------------------------------------------------------------------ Freie Grösse (Nachtrag, entwurf/freie-groesse-kanal.md)

    /// <summary>Block leitung.frei oder null (nur Typen).</summary>
    public FamFrei? Frei => Block.Frei;

    /// <summary>«frei b × h (145 … 2500 mm, Schritt 1 mm)» bzw. null ohne freie Grösse.</summary>
    public string? FreiText
    {
        get
        {
            if (Frei is not { } fr) return null;
            static string Bereich(FamGrenze? g) => g switch
            {
                { Min: double a, Max: double b } => $"{Leitungsgroesse.Zahl(a)} … {Leitungsgroesse.Zahl(b)} mm",
                { Min: double a } => $"ab {Leitungsgroesse.Zahl(a)} mm",
                { Max: double b } => $"bis {Leitungsgroesse.Zahl(b)} mm",
                _ => "jede Grösse > 0",
            };
            var je = Block.Groesse.Keys.Select(k => (K: k, G: fr.Grenzen.GetValueOrDefault(k))).ToList();
            var bereich = je.Select(x => x.G).Distinct().Count() == 1 ? Bereich(je[0].G) : string.Join(", ", je.Select(x => $"{x.K} {Bereich(x.G)}"));
            return $"frei {(Querschnitt == "rechteck" ? "b × h" : "d")} ({bereich}, Schritt {Leitungsgroesse.Zahl(fr.Schritt)} mm)";
        }
    }

    /// <summary>Verletzte Grenze als Text («b = 120 mm, erlaubt 145 … 2500 mm») oder null (innerhalb bzw. ohne freie Grösse).</summary>
    public string? Grenzverletzung(Leitungsgroesse g)
    {
        if (Frei is not { } fr) return null;
        var l = new List<string>();
        foreach (var k in Block.Groesse.Keys)
        {
            if (g.Wert(k) is not double x || !fr.Grenzen.TryGetValue(k, out var gr)) continue;
            if ((gr.Min is double a && x < a - Leitungsgroesse.Toleranz) || (gr.Max is double b && x > b + Leitungsgroesse.Toleranz))
                l.Add($"{k} = {Leitungsgroesse.Zahl(x)} mm, erlaubt {(gr.Min is double mn ? Leitungsgroesse.Zahl(mn) : "0")} … {(gr.Max is double mx ? Leitungsgroesse.Zahl(mx) : "∞")} mm");
        }
        return l.Count == 0 ? null : string.Join("; ", l);
    }

    /// <summary>Jeder Grössenwert ist ein Vielfaches des Schritts (bis 1e-6 mm); ohne freie Grösse true.</summary>
    public bool ImSchritt(Leitungsgroesse g)
    {
        if (Frei is not { } fr) return true;
        foreach (var k in Block.Groesse.Keys)
            if (g.Wert(k) is double x && Math.Abs(x / fr.Schritt - Math.Round(x / fr.Schritt)) * fr.Schritt > Leitungsgroesse.Toleranz) return false;
        return true;
    }

    /// <summary>Die Grösse auf den Schritt gerundet und in die Grenzen gelegt (Vorschlag, nie still übernommen).</summary>
    public Leitungsgroesse Gerundet(Leitungsgroesse g)
    {
        if (Frei is not { } fr) return g;
        double R(string k, double? x)
        {
            if (x is not double v) return double.NaN;
            double s = fr.Schritt;
            var gr = fr.Grenzen.GetValueOrDefault(k);
            double lo = gr?.Min is double mn ? Math.Ceiling(mn / s - 1e-9) * s : s;
            double hi = gr?.Max is double mx ? Math.Floor(mx / s + 1e-9) * s : double.MaxValue;
            double r = Math.Round(v / s, MidpointRounding.AwayFromZero) * s;
            return lo <= hi ? Math.Clamp(r, lo, hi) : r;
        }
        return new Leitungsgroesse(g.Dn, g.D is null ? null : R("d", g.D), g.B is null ? null : R("b", g.B), g.H is null ? null : R("h", g.H));
    }

    /// <summary>Probegrösse für die Abfrage: je Schlüssel die Mitte der Grenzen, sonst 430 × 270 (rund 430), gerundet und begrenzt.</summary>
    public Leitungsgroesse Probe()
    {
        double? W(string k, double vorgabe) => !Block.Groesse.ContainsKey(k) ? null
            : Frei?.Grenzen.GetValueOrDefault(k) is { Min: double a, Max: double b } ? (a + b) / 2 : vorgabe;
        return Gerundet(new Leitungsgroesse(null, W("d", 430), W("b", 430), W("h", 270)));
    }

    static double Abstand(Leitungsgroesse a, Leitungsgroesse b)
    {
        double s = 0;
        foreach (var k in new[] { "dn", "d", "b", "h" })
            if (a.Wert(k) is double x && b.Wert(k) is double y) s += (x - y) * (x - y);
        return Math.Sqrt(s);
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, SerienGroesse> virtuelle = new();

    /// <summary>Virtuelle Grösse aus der Vorlage mit ersetzten Grössenparametern (ohne Prüfung von Grenzen und Schritt – für bestehende
    /// Leitungen; Neues geht über TypFuer). Fehler, wenn die Vorlage damit nicht auswertbar ist.</summary>
    public SerienGroesse Virtuell(Leitungsgroesse g)
    {
        if (Frei is not { } fr) throw new HlksFehler($"Die Serie {Bezeichnung} hat keine freie Grösse.", "leitung.frei in der Serie ergänzen oder eine Grösse der Serie wählen.");
        return virtuelle.GetOrAdd(g.Kurz, _ =>
        {
            var a = FamilienAuswertung.Aufloesen(Def, fr.Vorlage, null, FreieGroesse.Typwerte(Block, g));
            if (!a.Gueltig || a.Leitung is null)
                return new SerienGroesse(fr.Vorlage, g, null, null, null, null, a.Fehler.Count > 0 ? $"{a.Fehler[0].Stelle}: {a.Fehler[0].Meldung}" : "nicht auswertbar", true);
            var w = a.Leitung;
            return new SerienGroesse(fr.Vorlage, AusSchluesseln(w.Groesse, Block), w.Aussen, w.Innen, w.Wand, w.Biegeradius, null, true);
        });
    }

    /// <summary>Bogenwinkel erlaubt (Liste bis 1e-6°; «frei» oder ohne Regel: 0 &lt; w ≤ 180).</summary>
    public bool WinkelErlaubt(double w) =>
        Block.Winkel is { } l ? l.Any(x => Math.Abs(x - w) <= 1e-6) : w > 0 && w <= 180 + 1e-9;

    /// <summary>«15°, 30°, 45°, 90°» bzw. «frei».</summary>
    public string WinkelText => Block.Winkel is { } l ? string.Join(", ", l.Select(x => Leitungsgroesse.Zahl(x) + "°")) : "frei";

    /// <summary>Erlaubte Abzweigwinkel der T-Stücke in Grad (regeln.abzweigwinkel; fehlt = [90]).</summary>
    public IReadOnlyList<double> Abzweigwinkel => Block.Abzweigwinkel ?? [90.0];

    /// <summary>Abzweigwinkel erlaubt (Liste bis 1e-6°).</summary>
    public bool AbzweigwinkelErlaubt(double w) => Abzweigwinkel.Any(x => Math.Abs(x - w) <= 1e-6);

    /// <summary>«45°, 88.5°» bzw. «90°».</summary>
    public string AbzweigwinkelText => string.Join(", ", Abzweigwinkel.Select(x => Leitungsgroesse.Zahl(x) + "°"));

    /// <summary>Der erlaubte Abzweigwinkel zu einem gemessenen Winkel (der nächste der Liste bis ±Routing.WinkelToleranz) oder null.</summary>
    public double? AbzweigwinkelZu(double gemessen)
    {
        if (!double.IsFinite(gemessen)) return null;
        var naechster = Abzweigwinkel.OrderBy(x => Math.Abs(x - gemessen)).First();
        return Math.Abs(naechster - gemessen) <= Routing.WinkelToleranz + 1e-9 ? naechster : null;
    }

    // ------------------------------------------------------------------ Formteilwahl

    /// <summary>Formteil-Familien im Projekt über die Definitions-id.</summary>
    public static List<Element> FamilienMitId(Modell m, string id) =>
        m.DerKlasse(Familien.Klasse).Where(f => Familien.DefinitionsId(f) == id).OrderBy(f => f.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Wählt das Formteil der Serie für eine Art (bogen | tstueck | reduktion | reinigungsoeffnung) und Grösse(n):
    /// Bogen g1 (winkel = Umlenkwinkel in Grad, optional), T-Stück g1 = Durchgang und g2 = Abzweig (Vorgabe g1; winkel =
    /// Abzweigwinkel, ohne Angabe 90 – der nächste erlaubte Winkel der Serie bis ±Routing.WinkelToleranz), Reduktion
    /// g1 und g2 (geordnet: a = gross), Reinigungsöffnung g1. Ergebnis mit Familie, Typ, Exemplarwerten und lokaler Geometrie
    /// oder abgelehnt mit Grund und Vorschlag – wirft nie für fachliche Gründe.</summary>
    public FormteilWahl Formteil(Modell m, Katalog k, string art, Leitungsgroesse g1, Leitungsgroesse? g2 = null, double? winkel = null)
    {
        FormteilWahl Nein(string grund, string vorschlag) => new() { Art = art, Fehler = grund, Vorschlag = vorschlag };
        g1 = Norm(g1);
        if (g2 is { } gn) g2 = Norm(gn);
        if (!FamLeitung.Formteilarten.Contains(art))
            return Nein($"Formteilart «{art}» gibt es nicht.", "Formteilarten: " + string.Join(", ", FamLeitung.Formteilarten));
        if (!PasstArt(g1) || (g2 is { } gx && !PasstArt(gx)))
            return Nein($"Die Grösse {g1}{(g2 is { } gy ? " bzw. " + gy : "")} passt nicht zur Serie {Bezeichnung} ({Kategorie}, {Querschnitt}).", "Grösse angeben als " + GroessenForm + ".");
        Leitungsgroesse a1 = g1, a2 = g2 ?? g1;
        bool vertauscht = false;
        double abzweigwinkel = 90;   // T-Stück: gewählter Abzweigwinkel der Serie
        switch (art)
        {
            case "bogen":
                if (winkel is double w && !WinkelErlaubt(w))
                    return Nein($"Ein Bogen mit {Leitungsgroesse.Zahl(w)}° ist in der Serie {Bezeichnung} nicht erlaubt (erlaubt: {WinkelText}).",
                        "Den Verlauf so legen, dass die Umlenkung einem erlaubten Winkel entspricht (auch zwei Bögen nacheinander), oder eine Serie mit diesem Winkel wählen.");
                if (winkel is double w0 && (w0 <= 0 || w0 > 180))
                    return Nein($"Bogenwinkel {Leitungsgroesse.Zahl(w0)}° liegt nicht in 0 < w ≤ 180.", "Umlenkwinkel in Grad angeben.");
                break;
            case "tstueck":
            {
                double gemessen = winkel ?? 90;
                if (AbzweigwinkelZu(gemessen) is not double wl)
                    return Nein($"Ein Abzweig unter {Leitungsgroesse.Zahl(Math.Round(gemessen, 3))}° ist in der Serie {Bezeichnung} nicht erlaubt (Abzweigwinkel: {AbzweigwinkelText}, Toleranz ±{Leitungsgroesse.Zahl(Routing.WinkelToleranz)}°).",
                        "Den Abzweig unter einem erlaubten Abzweigwinkel führen (Winkel zwischen dem Durchgang in Fliessrichtung und dem Abzweig) oder eine Serie mit diesem Abzweigwinkel wählen (leitung.regeln.abzweigwinkel).");
                abzweigwinkel = wl;
                break;
            }
            case "reduktion":
                if (g2 is null) return Nein("Für eine Reduktion braucht es zwei Grössen.", "Grösse gross und klein angeben.");
                if (a1.Gleich(a2)) return Nein($"Gleiche Grösse {a1}: dafür braucht es keine Reduktion.", "Die Leitungen direkt verbinden.");
                if (a2.Mass > a1.Mass) { (a1, a2) = (a2, a1); vertauscht = true; }
                break;
        }
        if (!Block.Formteile.TryGetValue(art, out var wahl))
            return Nein($"Die Serie {Bezeichnung} nennt keine Formteil-Familie für {Artname(art)}.",
                $"In der Serie leitung.formteile.{art} = {{ \"familie\": \"<id der {FamLeitung.KlasseVon(art)}-Familie>\" }} ergänzen (familie_aendern mit op leitung_setzen).");
        var kandidaten = FamilienMitId(m, wahl.Familie);
        if (kandidaten.Count == 0)
            return Nein($"Die Formteil-Familie «{wahl.Familie}» ({Artname(art)} der Serie {Bezeichnung}) fehlt im Projekt.",
                $"familie_laden mit der Datei der Formteil-Familie (familien_bibliothek listet die Bibliothek), danach erneut versuchen.");
        var fam = kandidaten[0];
        var def = Familien.Definition(fam, k);
        if (!def.Gueltig)
            return Nein($"Die Formteil-Familie {fam.Kennzeichen} «{fam.Name}» ist ungültig: {def.Fehler[0]}", "Familie korrigieren (familie abfragen zeigt alle Fehler).");
        var klasse = FamLeitung.KlasseVon(art);
        if (!k.Klassen.TryGetValue(def.Kategorie, out var kd) || !kd.IstArt(klasse))
            return Nein($"Die Formteil-Familie {fam.Kennzeichen} «{fam.Name}» hat die Kategorie {def.Kategorie}, für {Artname(art)} braucht es {klasse}.",
                $"In der Serie leitung.formteile.{art} eine Familie der Kategorie {klasse} nennen.");

        // Exemplarwerte: Bogenwinkel bzw. Abzweigwinkel (T-Stück) in den Exemplarparameter mit ziel «winkel» (bzw. namens winkel)
        JsonObject? Exemplar(FamilieDef d)
        {
            double w;
            if (art == "bogen" && winkel is double wb) w = wb;
            else if (art == "tstueck") w = abzweigwinkel;
            else return null;
            var p = d.Parameter.FirstOrDefault(x => x.Art == "exemplar" && !x.HatFormel && string.Equals(x.Ziel, "winkel", StringComparison.OrdinalIgnoreCase))
                    ?? d.Parameter.FirstOrDefault(x => x.Art == "exemplar" && !x.HatFormel && x.Name.Equals("winkel", StringComparison.OrdinalIgnoreCase));
            return p is null ? null : new JsonObject { [p.Name] = w };
        }
        var ex = Exemplar(def);

        // Soll-Grössen je Anschluss
        var soll = art switch
        {
            "tstueck" => new (string Name, Leitungsgroesse G)[] { ("a", a1), ("b", a1), ("c", a2) },
            "reduktion" => [("a", a1), ("b", a2)],
            _ => [("a", a1), ("b", a1)],
        };
        string SollText() => string.Join(", ", soll.Select(s => $"{s.Name} = {s.G}"));

        string? Passt(FamAufloesung a)
        {
            foreach (var (name, g) in soll)
            {
                var an = a.Anschluesse.FirstOrDefault(x => x.Sichtbar && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (an is null) return $"Anschluss «{name}» fehlt";
                var ist = Leitungsgroesse.Von(an);
                if (!ist.Gleich(g)) return $"Anschluss {name} ist {ist}";
            }
            if (art == "bogen" && winkel is double w)
            {
                var b = a.Anschluesse.First(x => x.Name.Equals("b", StringComparison.OrdinalIgnoreCase));
                var wb = Formteilgeometrie.Bogenwinkel(b.Richtung);
                if (Math.Abs(wb - w) > 1e-6) return $"Bogenwinkel {Leitungsgroesse.Zahl(wb)}°";
            }
            if (art == "tstueck")
            {
                var c = a.Anschluesse.First(x => x.Name.Equals("c", StringComparison.OrdinalIgnoreCase));
                var wc = Formteilgeometrie.Abzweigwinkel(c.Richtung);
                if (Math.Abs(wc - abzweigwinkel) > 1e-6) return $"Abzweigwinkel {Leitungsgroesse.Zahl(Math.Round(wc, 6))}°";
            }
            return null;
        }
        string WinkelZusatz() => art == "bogen" && winkel is double wz ? $", {Leitungsgroesse.Zahl(wz)}°"
            : art == "tstueck" && Math.Abs(abzweigwinkel - 90) > 1e-9 ? $", Abzweig {Leitungsgroesse.Zahl(abzweigwinkel)}°" : "";

        string typ;
        string? hinweis = null;
        bool frei = false;
        // Freie Grösse (Nachtrag, entwurf/freie-groesse-kanal.md): passt kein Typ und ist die Serie frei, wird der Typ mit Exemplarwerten
        // für die Anschlussgrössen ausgewertet (FreiExemplar) und wie bisher geprüft (Passt, Lage, Winkel)
        string ExText(JsonObject? x) => x is null ? "" : string.Join(", ", x.Select(kv => $"{kv.Key} = {kv.Value?.ToJsonString()}"));
        FormteilWahl? FreiEinstellen(string t, string grundOhne, out JsonObject? exFrei)
        {
            exFrei = null;
            var (exf, grund) = FreiExemplar(def, soll, ex);
            if (grund is not null)
                return Nein($"{grundOhne}; für die freie Grösse lässt sich Typ «{t}» nicht einstellen: {grund}.", FreiVorschlag(art));
            var af = FamilienAuswertung.Aufloesen(def, t, exf);
            if (!af.Gueltig)
                return Nein($"Typ «{t}» von {fam.Kennzeichen} «{fam.Name}» lässt sich mit {ExText(exf)} nicht auswerten: {af.Fehler[0]}", af.Fehler[0].Vorschlag ?? "Formteil-Familie korrigieren.");
            if (Passt(af) is { } abw)
                return Nein($"Typ «{t}» von {fam.Kennzeichen} «{fam.Name}» passt auch mit {ExText(exf)} nicht zu {Artname(art)} {SollText()}{WinkelZusatz()}: {abw}.", FreiVorschlag(art));
            exFrei = exf;
            return null;
        }
        if (wahl.TypFormel is { } formel)
        {
            var umg = new TypregelUmgebung(Block.Groesse, a1, a2);
            FWert erg;
            try { erg = formel.Werten(umg); }
            catch (FormelFehler ff)
            {
                return Nein($"Typregel «{wahl.TypText}» der Serie {Bezeichnung} ({Artname(art)}): {ff.MitStelle}", ff.Vorschlag ?? "Typregel in leitung.formteile korrigieren.");
            }
            if (!erg.IstText)
                return Nein($"Typregel «{wahl.TypText}» ergibt die Zahl {erg}, gebraucht wird ein Typname (Text).", "Formel so schreiben, dass sie den Typnamen ergibt, z. B. if(dn = 15, \"DN 15\", \"DN 20\").");
            typ = erg.Text!;
            if (def.Typ(typ) is null)
                return Nein($"Die Typregel ergibt «{typ}» für {SollText()}, den Typ gibt es in {fam.Kennzeichen} «{fam.Name}» nicht.",
                    $"Typen: {string.Join(", ", def.Typen.Select(t => t.Name))} – Typ ergänzen (familie_aendern typ_setzen) oder die Typregel anpassen.");
            var a = FamilienAuswertung.Aufloesen(def, typ, ex);
            if (!a.Gueltig) return Nein($"Typ «{typ}» von {fam.Kennzeichen} lässt sich nicht auswerten: {a.Fehler[0]}", a.Fehler[0].Vorschlag ?? "Familie korrigieren.");
            if (Passt(a) is { } abw)
            {
                var grundOhne = $"Typ «{typ}» (aus der Typregel) von {fam.Kennzeichen} passt nicht zu {SollText()}{WinkelZusatz()}: {abw}";
                if (Frei is null) return Nein(grundOhne + ".", "Typregel bzw. Typwerte der Formteil-Familie korrigieren.");
                if (FreiEinstellen(typ, grundOhne, out var exf) is { } nein) return nein;
                ex = exf; frei = true;
            }
        }
        else
        {
            var treffer = new List<string>();
            foreach (var t in def.Typen)
            {
                var a = FamilienAuswertung.Aufloesen(def, t.Name, ex);
                if (a.Gueltig && Passt(a) is null) treffer.Add(t.Name);
            }
            if (treffer.Count == 0)
            {
                var vorhanden = string.Join(", ", def.Typen.Select(t => t.Name));
                var grundOhne = $"Kein Typ der Formteil-Familie {fam.Kennzeichen} «{fam.Name}» passt zu {Artname(art)} {SollText()}{WinkelZusatz()}";
                if (Frei is null)
                    return Nein(grundOhne + ".",
                        $"Vorhandene Typen: {vorhanden}. Typ ergänzen (familie_aendern typ_setzen) oder eine andere Grösse bzw. Folge wählen" + (art == "reduktion" ? " (z. B. in zwei Schritten reduzieren)." : "."));
                // freie Serie: Vorlage-Typ (leitung.formteile.<art>.vorlage, fehlt = erster Typ) mit Exemplarwerten
                var vorlage = wahl.Vorlage ?? def.Typen[0].Name;
                if (def.Typ(vorlage) is null)
                    return Nein($"{grundOhne}, und die Vorlage «{vorlage}» (leitung.formteile.{art}.vorlage der Serie {Bezeichnung}) gibt es in {fam.Kennzeichen} «{fam.Name}» nicht.",
                        $"Typen: {vorhanden} – vorlage korrigieren (familie_aendern mit op leitung_setzen).");
                if (FreiEinstellen(vorlage, grundOhne, out var exf) is { } nein) return nein;
                typ = vorlage; ex = exf; frei = true;
            }
            else
            {
                typ = treffer[0];
                if (treffer.Count > 1) hinweis = $"Mehrere Typen passen ({string.Join(", ", treffer)}), gewählt: {typ}.";
            }
        }

        Formteilgeometrie geo;
        try { geo = Formteilgeometrie.Lokal(def, typ, ex, art); }
        catch (HlksFehler f)
        {
            return Nein($"Formteil-Familie {fam.Kennzeichen} «{fam.Name}», Typ «{typ}»{(frei ? $" mit {ExText(ex)}" : "")}: {f.Message}", f.Vorschlag ?? Formteilgeometrie.LageVorschlag);
        }
        return new FormteilWahl
        {
            Art = art, Familie = fam, Def = def, Typ = typ, Exemplar = ex, Geometrie = geo, Hinweis = hinweis, Vertauscht = vertauscht,
            Groesse1 = a1, Groesse2 = art is "tstueck" or "reduktion" ? a2 : null, Abzweigwinkel = art == "tstueck" ? abzweigwinkel : null, Frei = frei,
        };
    }

    /// <summary>Vorschlag, wenn sich ein Formteil-Typ für eine freie Grösse nicht einstellen lässt.</summary>
    static string FreiVorschlag(string art) =>
        $"In der {FamLeitung.KlasseVon(art)}-Familie die Anschlussgrössen (d bzw. b und h) direkt als Exemplarparameter ohne Formel führen – " +
        (art switch
        {
            "tstueck" => "Durchgang a und b mit denselben, Abzweig c mit eigenen Parametern (z. B. b_c, h_c)",
            "reduktion" => "a und b mit je eigenen Parametern (z. B. breite1/hoehe1 und breite2/hoehe2)",
            _ => "a und b mit denselben Parametern (z. B. breite, hoehe)",
        }) + "; Einbaulängen als Formel daraus, Formeln darauf ebenfalls als Exemplarparameter – oder einen Typ mit dieser Grösse ergänzen (familie_aendern typ_setzen).";

    /// <summary>Exemplarwerte für eine freie Grösse (Nachtrag, entwurf/freie-groesse-kanal.md): ist der Grössenwert (d, b, h) eines
    /// Anschlusses direkt der Name eines Exemplarparameters ohne Formel, wird dieser auf die Soll-Grösse gesetzt; eine Konstante muss
    /// schon stimmen. Abgelehnt mit Grund: Grössenwert ist eine Formel, ein Typparameter, ein Parameter mit Formel, oder derselbe
    /// Parameter müsste zwei Werte haben. Die übrigen Exemplarwerte (winkel) bleiben.</summary>
    public static (JsonObject? Exemplar, string? Grund) FreiExemplar(FamilieDef def, IReadOnlyList<(string Name, Leitungsgroesse G)> soll, JsonObject? exemplar)
    {
        var ex = exemplar?.DeepClone() as JsonObject ?? new JsonObject();
        var gesetzt = new Dictionary<string, (double Wert, string Wo)>(StringComparer.Ordinal);
        foreach (var (name, g) in soll)
        {
            var an = def.Anschluesse.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (an is null) return (null, $"Anschluss «{name}» fehlt");
            foreach (var (schl, feld, wert) in new[] { ("d", an.D, g.D), ("b", an.B, g.B), ("h", an.H, g.H) })
            {
                if (wert is not double w) continue;
                var wo = $"{an.Name}.{schl}";
                if (feld is null) return (null, $"Anschluss {an.Name} hat keine Grösse {schl}");
                if (feld.Konstante is double c)
                {
                    if (Math.Abs(c - w) > Leitungsgroesse.Toleranz) return (null, $"{wo} ist fest {Leitungsgroesse.Zahl(c)} mm");
                    continue;
                }
                var pn = feld.Text?.Trim();
                if (pn is null || !def.NachName.TryGetValue(pn, out var p)) return (null, $"{wo} ist die Formel «{feld.Text}», kein einzelner Parameter");
                if (p.HatFormel) return (null, $"{wo} = «{pn}» hat eine Formel");
                if (p.Art != "exemplar") return (null, $"{wo} = «{pn}» ist ein Typparameter (pro Exemplar nicht einstellbar)");
                if (gesetzt.TryGetValue(pn, out var vorher) && Math.Abs(vorher.Wert - w) > Leitungsgroesse.Toleranz)
                    return (null, $"«{pn}» müsste zugleich {Leitungsgroesse.Zahl(vorher.Wert)} mm ({vorher.Wo}) und {Leitungsgroesse.Zahl(w)} mm ({wo}) sein");
                gesetzt.TryAdd(pn, (w, wo));   // erste Stelle bleibt für die Meldung
                ex[pn] = w;
            }
        }
        return (ex, null);
    }

    public static string Artname(string art) => art switch
    {
        "bogen" => "Bogen", "tstueck" => "T-Stück", "reduktion" => "Reduktion", "reinigungsoeffnung" => "Reinigungsöffnung", _ => art,
    };

    /// <summary>Namen der Typregel: Grössenparameter der Serie = erste Grösse, mit Endung «_2» = zweite Grösse.</summary>
    sealed class TypregelUmgebung(IReadOnlyDictionary<string, string> groesse, Leitungsgroesse g1, Leitungsgroesse g2) : IFormelUmgebung
    {
        static double? Wert(Leitungsgroesse g, string schluessel) => schluessel switch { "dn" => g.Dn, "d" => g.D, "b" => g.B, "h" => g.H, _ => null };

        public FWert Wert(string name, int stelle)
        {
            foreach (var (schl, pn) in groesse)
            {
                if (name == pn && Wert(g1, schl) is double x) return FWert.Z(x);
                if (name == pn + "_2" && Wert(g2, schl) is double y) return FWert.Z(y);
            }
            throw new FormelFehler($"unbekannter Name «{name}»", stelle, "In der Typregel gibt es nur die Grössen der Serie: " + string.Join(", ", FamilieDef.TypregelNamen(groesse.Values, "reduktion")));
        }
    }

    // ------------------------------------------------------------------ Abfrage «leitungsserie»

    public const string AbfrageText = "Eine Leitungsserie (Familie der Kategorie Rohr bzw. Kanal, Modul 3): Querschnitt, Werkstoff-Kürzel (leitung.kuerzel, falls angegeben), Grössen mit aussen, innen, wand und Biegeradius (mm) und der Beschriftung der Rohre dieser Grösse (Rohre ausser Abwasser: beschriftung mit NW/DN, z. B. «CNS NW 20»; Abwasser: beschriftungAbwasser mit dem Aussendurchmesser, z. B. «PE-S 110»; ohne Kürzel nur die Grösse; ohne Zahl null). Abwasser-Serien (leitung.einsatz abwasser, einsatz und nachAussendurchmesser): beschriftung ist immer die Abwasserform mit dem Aussendurchmesser, unabhängig vom Medium (z. B. «PE 110»); bei Grösse d (nachAussendurchmesser: true) heissen die Grössen { d } («d 110»), keine DN nötig, dnHersteller = DN der Unterlage (Typparameter mit ziel dn, nie abgeleitet) bzw. null. Erlaubte Bogenwinkel und Abzweigwinkel der T-Stücke (regeln.abzweigwinkel, fehlt = 90°), Formteil-Familien je Art (im Projekt vorhanden?) und je Grösse, ob Bogen, T-Stück (gleiche Grösse, erster Abzweigwinkel; tstueckJeWinkel je erlaubtem Abzweigwinkel), Reduktion (auf jede kleinere Grösse) und Reinigungsöffnung verfügbar sind – mit Grund und Vorschlag, wenn nicht. Kanalserien mit freier Grösse (leitung.frei): frei mit Vorlage, Grenzen, Schritt und Text (die Typen sind dann das Vorschlagsraster) und eine probe mit einer freien Grösse (Mitte der Grenzen, sonst 430 × 270) samt Massen und Formteilverfügbarkeit (frei: true und parameter = Exemplarwerte, wenn ein Formteil aus seiner Vorlage gerechnet wird).";

    public static JsonObject Abfrage(Projekt p, string bezug)
    {
        var m = p.Modell; var k = p.Katalog;
        var s = Hole(m, k, bezug);
        static JsonNode? Z(double? x) => x is double d ? JsonValue.Create(Math.Round(d, 6)) : null;
        JsonObject Verfuegbar(FormteilWahl w)
        {
            if (!w.Ok) return new JsonObject { ["verfuegbar"] = false, ["grund"] = w.Fehler, ["vorschlag"] = w.Vorschlag };
            var o = new JsonObject { ["verfuegbar"] = true, ["familie"] = w.Familie!.Kennzeichen, ["typ"] = w.Typ, ["hinweis"] = w.Hinweis };
            if (w.Frei) { o["frei"] = true; o["parameter"] = w.Exemplar?.DeepClone(); }
            return o;
        }
        var gueltig = s.Groessen.Where(g => g.Gueltig).ToList();
        var formteile = new JsonObject();
        foreach (var art in FamLeitung.Formteilarten)
        {
            if (!s.Block.Formteile.TryGetValue(art, out var w)) { formteile[art] = null; continue; }
            var im = FamilienMitId(m, w.Familie).FirstOrDefault();
            formteile[art] = new JsonObject { ["familie"] = w.Familie, ["typregel"] = w.TypText, ["imProjekt"] = im?.Kennzeichen };
            if (w.Vorlage is not null) formteile[art]!["vorlage"] = w.Vorlage;
        }
        var groessen = new JsonArray();
        foreach (var g in s.Groessen)
        {
            var o = new JsonObject { ["typ"] = g.Typ, ["gueltig"] = g.Gueltig };
            if (!g.Gueltig) { o["fehler"] = g.Fehler; groessen.Add(o); continue; }
            foreach (var (schl, wert) in g.Groesse.AlsJson()!) o[schl] = wert?.DeepClone();
            o["aussen"] = Z(g.Aussen); o["innen"] = Z(g.Innen); o["wand"] = Z(g.Wand); o["biegeradius"] = Z(g.Biegeradius);
            if (s.Kategorie == "Rohr")
            {
                // B9b (Entscheide Christian 19.10.2026): Rohre ausser Abwasser NW/DN (Einstellung des Projekts), Abwasser Aussendurchmesser; nie umgerechnet
                var art = Leitungsbezeichnung.Art(m, null).Art;
                // Abwasser-Serie (Nachtrag, Entscheid Christian 20.10.2026): immer der Aussendurchmesser (d bzw. aussen), unabhängig vom Medium
                double? da = g.Groesse.Aussendurchmesser ? g.Groesse.D : g.Aussen;
                var abwasserform = da is double daw && daw > 0 ? Rohrgroessen.Beschriftung(true, "da", s.Block.Kuerzel, daw) : null;
                o["beschriftung"] = s.Abwasser ? abwasserform
                    : g.Groesse.Dn is double dnw && dnw > 0 ? Rohrgroessen.Beschriftung(false, art, s.Block.Kuerzel, dnw) : null;
                o["beschriftungAbwasser"] = abwasserform;
                if (s.NachAussendurchmesser) o["dnHersteller"] = s.DnHersteller(g);
            }
            var ft = new JsonObject
            {
                ["bogen"] = Verfuegbar(s.Formteil(m, k, "bogen", g.Groesse)),
                ["tstueck"] = Verfuegbar(s.Formteil(m, k, "tstueck", g.Groesse, g.Groesse, s.Abzweigwinkel[0])),
                ["reinigungsoeffnung"] = Verfuegbar(s.Formteil(m, k, "reinigungsoeffnung", g.Groesse)),
            };
            if (s.Block.Abzweigwinkel is not null)
            {
                var jeWinkel = new JsonArray();
                foreach (var aw in s.Abzweigwinkel)
                {
                    var t = Verfuegbar(s.Formteil(m, k, "tstueck", g.Groesse, g.Groesse, aw));
                    t["winkel"] = aw;
                    jeWinkel.Add(t);
                }
                ft["tstueckJeWinkel"] = jeWinkel;
            }
            var red = new JsonArray();
            foreach (var kl in gueltig.Where(x => x.Groesse.Mass < g.Groesse.Mass))
            {
                var r = Verfuegbar(s.Formteil(m, k, "reduktion", g.Groesse, kl.Groesse));
                r["auf"] = kl.Typ;
                red.Add(r);
            }
            ft["reduktion"] = red;
            o["formteile"] = ft;
            groessen.Add(o);
        }

        // Freie Grösse (Nachtrag): Block frei und eine Probe mit einer freien Grösse samt Formteilverfügbarkeit
        JsonObject? frei = null, probe = null;
        if (s.Frei is { } fr)
        {
            frei = new JsonObject
            {
                ["vorlage"] = fr.Vorlage,
                ["grenzen"] = new JsonObject(fr.Grenzen.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)new JsonObject { ["min"] = x.Value.Min, ["max"] = x.Value.Max }))),
                ["schritt"] = fr.Schritt, ["text"] = s.FreiText,
            };
            var pg = s.Probe();
            probe = new JsonObject { ["groesse"] = pg.AlsJson(), ["text"] = pg.Kurz };
            if (s.TypFuer(pg, out var grund) is not { } sg) { probe["gueltig"] = false; probe["grund"] = grund; }
            else
            {
                probe["gueltig"] = true; probe["typ"] = sg.Typ; probe["frei"] = sg.Frei;
                probe["aussen"] = Z(sg.Aussen); probe["innen"] = Z(sg.Innen); probe["wand"] = Z(sg.Wand); probe["biegeradius"] = Z(sg.Biegeradius);
                var ft = new JsonObject
                {
                    ["bogen"] = Verfuegbar(s.Formteil(m, k, "bogen", pg)),
                    ["tstueck"] = Verfuegbar(s.Formteil(m, k, "tstueck", pg, pg, s.Abzweigwinkel[0])),
                    ["reinigungsoeffnung"] = Verfuegbar(s.Formteil(m, k, "reinigungsoeffnung", pg)),
                };
                if (gueltig.Where(x => x.Groesse.Mass < pg.Mass).OrderByDescending(x => x.Groesse.Mass).FirstOrDefault() is { } kl)
                {
                    var r = Verfuegbar(s.Formteil(m, k, "reduktion", pg, kl.Groesse));
                    r["auf"] = kl.Typ;
                    ft["reduktion"] = new JsonArray(r);
                }
                else ft["reduktion"] = new JsonArray();
                probe["formteile"] = ft;
            }
        }
        return new JsonObject
        {
            ["familie"] = s.Familie.Kennzeichen, ["name"] = s.Familie.Name, ["id"] = Familien.DefinitionsId(s.Familie), ["kategorie"] = s.Kategorie,
            ["querschnitt"] = s.Querschnitt, ["kuerzel"] = s.Block.Kuerzel,
            ["einsatz"] = s.Block.Einsatz, ["nachAussendurchmesser"] = s.NachAussendurchmesser,
            ["groessenparameter"] = new JsonObject(s.Block.Groesse.Select(x => KeyValuePair.Create(x.Key, (JsonNode?)JsonValue.Create(x.Value)))),
            ["winkel"] = s.Block.Winkel is { } wl ? (JsonNode)new JsonArray(wl.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) : JsonValue.Create("frei"),
            ["winkelRegel"] = s.Block.Winkel is not null ? "liste" : s.Block.WinkelFrei ? "frei" : "keine",
            ["abzweig"] = s.Block.Abzweig,
            ["abzweigwinkel"] = new JsonArray(s.Abzweigwinkel.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()),
            ["formteilFamilien"] = formteile,
            ["groessen"] = groessen,
            ["frei"] = frei,
            ["probe"] = probe,
        };
    }
}

/// <summary>Ergebnis der Formteilwahl: gewählt (Familie, Typ, Exemplarwerte, lokale Geometrie) oder abgelehnt (Fehler, Vorschlag).</summary>
public sealed class FormteilWahl
{
    public bool Ok => Fehler is null;
    /// <summary>bogen | tstueck | reduktion | reinigungsoeffnung</summary>
    public string Art { get; init; } = "";
    /// <summary>Formteil-Familie im Projekt (Element der Klasse Familie).</summary>
    public Element? Familie { get; init; }
    public FamilieDef? Def { get; init; }
    public string? Typ { get; init; }
    /// <summary>Exemplarwerte für familie_platzieren (Bogen: { winkel } = Umlenkwinkel; T-Stück mit Exemplarparameter winkel:
    /// { winkel } = Abzweigwinkel, B7; freie Grösse: dazu die Grössenparameter der Anschlüsse), sonst null.</summary>
    public JsonObject? Exemplar { get; init; }
    /// <summary>Freie Grösse (Nachtrag): kein Typ passte, Typ = Vorlage mit den Grössen als Exemplarwerte.</summary>
    public bool Frei { get; init; }
    /// <summary>Grössen für Meldungen: «430 × 270» bzw. «430 × 270/250 × 200».</summary>
    public string GroessenText => Groesse1.Kurz + (Groesse2 is { } g2 && !g2.Gleich(Groesse1) ? "/" + g2.Kurz : "");
    /// <summary>Lokale Anschlusslagen und Einbaulängen des gewählten Typs.</summary>
    public Formteilgeometrie? Geometrie { get; init; }
    /// <summary>Grösse an a (Bogen, T-Durchgang, Reduktion gross) bzw. an c / b (T-Abzweig, Reduktion klein).</summary>
    public Leitungsgroesse Groesse1 { get; init; }
    public Leitungsgroesse? Groesse2 { get; init; }
    /// <summary>Reduktion: die Grössen kamen klein → gross und wurden geordnet (a = gross).</summary>
    public bool Vertauscht { get; init; }
    /// <summary>T-Stück: gewählter Abzweigwinkel der Serie (Grad, B7); sonst null.</summary>
    public double? Abzweigwinkel { get; init; }
    public string? Hinweis { get; init; }
    public string? Fehler { get; init; }
    public string? Vorschlag { get; init; }
}

/// <summary>Ein Anschluss eines Formteils in Familienkoordinaten (Ursprung = Achsschnittpunkt) mit Einbaulänge (Abstand vom
/// Ursprung, mm).</summary>
public sealed record FormteilAnschluss(string Name, double[] Lage, double[] Richtung, Leitungsgroesse Groesse, double Einbaulaenge);

/// <summary>Lokale Geometrie eines Formteiltyps (entwurf/modul-3.md, Grundsatzentscheid 3): Ursprung = Schnittpunkt der Achsen.
/// Bogen: a bei (−La, 0, 0) Richtung (−1, 0, 0), b bei Lb·(cos w, sin w, 0) Richtung (cos w, sin w, 0). T-Stück: a (−L, 0, 0)/(−1, 0, 0),
/// b (L, 0, 0)/(1, 0, 0), c Lc·(−cos w, sin w, 0)/(−cos w, sin w, 0) mit w = Parameter winkel (Abzweigwinkel, B7; ohne Parameter 90 →
/// c (0, Lc, 0)/(0, 1, 0)). Reduktion: a im Ursprung/(−1, 0, 0), b (L, 0, 0)/(1, 0, 0). Reinigungsöffnung
/// (Annahme, wie ein gerades Stück): a (−La, 0, 0)/(−1, 0, 0), b (Lb, 0, 0)/(1, 0, 0).</summary>
public sealed class Formteilgeometrie
{
    public const double LageToleranz = 1e-3;      // mm
    public const double RichtungToleranz = 1e-6;
    public const string LageVorschlag = "Formteil-Familie nach entwurf/modul-3.md (Grundsatzentscheid 3) aufbauen: Ursprung im Schnittpunkt der Achsen; Bogen a (−La, 0, 0)/(−1, 0, 0), b Lb·(cos w, sin w, 0); T-Stück a (−L, 0, 0)/(−1, 0, 0), b (L, 0, 0)/(1, 0, 0), c Lc·(−cos w, sin w, 0)/(−cos w, sin w, 0) mit w = Parameter winkel (Abzweigwinkel 0 < w ≤ 90; ohne Parameter 90, also c (0, Lc, 0)/(0, 1, 0)); Reduktion a (0, 0, 0)/(−1, 0, 0), b (L, 0, 0)/(1, 0, 0).";

    public string Art { get; init; } = "";
    public string Typ { get; init; } = "";
    /// <summary>Bogen: Umlenkwinkel aus der Richtung von b (Grad); T-Stück: Abzweigwinkel w (Parameter winkel, ohne Parameter 90);
    /// sonst null.</summary>
    public double? Winkel { get; init; }
    public IReadOnlyList<FormteilAnschluss> Anschluesse { get; init; } = [];

    public FormteilAnschluss Anschluss(string name) => Anschluesse.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new HlksFehler($"Das Formteil hat keinen Anschluss «{name}».", "Anschlüsse: " + string.Join(", ", Anschluesse.Select(a => a.Name)));

    /// <summary>Einbaulänge eines Anschlusses (Abstand Achsschnittpunkt – Anschluss, mm).</summary>
    public double Einbaulaenge(string name) => Anschluss(name).Einbaulaenge;

    /// <summary>Ausrichtung eines Formteils aus Richtungen (für das Routing, B2): durch = Richtung, in der der Durchgang durchlaufen
    /// wird (von a zum Achsschnittpunkt; a zeigt nach −durch), seitlich = Richtung der Umlenkung bzw. des Abzweigs (Bogen: Richtung
    /// nach b, T-Stück: nach c). Ohne seitlich (gerade Formteile) eine waagrechte Senkrechte, damit z möglichst nach oben zeigt.
    /// Ergebnis (x, z) für Familien.AusrichtungJson bzw. Einbaulage.Achsen. Beispiel Steigbogen: durch (1, 0, 0), seitlich (0, 0, 1)
    /// → x = (1, 0, 0), z = (0, −1, 0).</summary>
    public static (V3 X, V3 Z) Ausrichtung(V3 durch, V3? seitlich = null)
    {
        if (durch.Laenge < 1e-9) throw new HlksFehler("Richtung des Durchgangs ist [0, 0, 0].", "Zwei verschiedene Punkte des Verlaufs angeben.");
        var x = durch.Einheit();
        V3 y;
        if (seitlich is { } s && (s - x * x.Mal(s)).Laenge > 1e-9) y = (s - x * x.Mal(s)).Einheit();
        else
        {
            if (seitlich is not null) throw new HlksFehler("Die seitliche Richtung liegt in der Achse des Durchgangs – daraus ergibt sich keine Umlenkung.", "Für eine Umlenkung zwei nicht parallele Richtungen angeben.");
            var waagrecht = new V3(0, 0, 1).Kreuz(x);
            y = waagrecht.Laenge > 1e-9 ? waagrecht.Einheit() : new V3(1, 0, 0);
        }
        return (x, x.Kreuz(y));
    }

    /// <summary>Umlenkwinkel aus der Richtung von b (Grad, 0..180 bei Richtung in der xy-Ebene).</summary>
    public static double Bogenwinkel(double[] richtungB)
    {
        var w = Math.Atan2(richtungB[1], richtungB[0]) * 180 / Math.PI;
        return w < -1e-9 ? w + 360 : Math.Abs(w) <= 1e-9 ? 0 : w;
    }

    /// <summary>Abzweigwinkel eines T-Stücks aus der lokalen Richtung von c (Grad): c = (−cos w, sin w, 0) → w; (0, 1, 0) → 90.
    /// Zeigt c in Durchgangsrichtung a→b, wird der Winkel grösser als 90.</summary>
    public static double Abzweigwinkel(double[] richtungC)
    {
        var w = Math.Atan2(richtungC[1], -richtungC[0]) * 180 / Math.PI;
        return w < -1e-9 ? w + 360 : Math.Abs(w) <= 1e-9 ? 0 : w;
    }

    /// <summary>Wert des Abzweigwinkel-Parameters eines T-Stücks (Parameter mit ziel «winkel» bzw. namens «winkel», Typ- oder
    /// Exemplarparameter) in einer Auswertung; null = die Familie hat keinen (dann gilt 90).</summary>
    public static double? WinkelParameter(FamAufloesung a)
    {
        var p = a.Familie.Parameter.FirstOrDefault(x => string.Equals(x.Ziel, "winkel", StringComparison.OrdinalIgnoreCase))
                ?? a.Familie.Parameter.FirstOrDefault(x => x.Name.Equals("winkel", StringComparison.OrdinalIgnoreCase));
        return p is not null && a.Wert(p.Name) is { IstText: false } w ? w.Zahl : null;
    }

    /// <summary>Lokale Geometrie eines Typs für Exemplarwerte (z. B. { winkel: 45 }). art (bogen | tstueck | reduktion |
    /// reinigungsoeffnung) prüft die Lage nach der Spezifikation; null = ohne Lageprüfung. Wirft HlksFehler.</summary>
    public static Formteilgeometrie Lokal(FamilieDef def, string typ, JsonObject? exemplar, string? art = null)
    {
        var a = FamilienAuswertung.Aufloesen(def, typ, exemplar);
        if (!a.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie «{def.Name}», Typ «{typ}»", a.Fehler);
        var liste = a.Anschluesse.Where(x => x.Sichtbar).Select(x => new FormteilAnschluss(x.Name, Familien.Runden(x.Lage, 6), Familien.Runden(x.Richtung, 12),
            Leitungsgroesse.Von(x), Math.Sqrt(x.Lage.Sum(c => c * c)))).ToList();
        double? winkel = null;
        if (art is not null)
        {
            string[] namen = art == "tstueck" ? ["a", "b", "c"] : ["a", "b"];
            foreach (var n in namen)
                if (!liste.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                    throw new HlksFehler($"Anschluss «{n}» fehlt ({Leitungsserie.Artname(art)} braucht {string.Join(", ", namen)}).", LageVorschlag);
            FormteilAnschluss A(string n) => liste.First(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
            switch (art)
            {
                case "bogen":
                {
                    AufAchse(A("a"), new V3(-1, 0, 0));
                    var b = A("b");
                    var rb = V3.Aus(b.Richtung);
                    if (Math.Abs(rb.Z) > RichtungToleranz)
                        throw new HlksFehler($"Anschluss b zeigt aus der xy-Ebene (Richtung {Text(b.Richtung)}) – der Bogen biegt in der Familie um z.", LageVorschlag);
                    AufAchse(b, rb.Einheit());
                    winkel = Bogenwinkel(b.Richtung);
                    if (winkel <= 1e-9 || winkel > 180 + 1e-9)
                        throw new HlksFehler($"Umlenkwinkel {Leitungsgroesse.Zahl(winkel.Value)}° aus der Richtung von b liegt nicht in 0 < w ≤ 180.", LageVorschlag);
                    break;
                }
                case "tstueck":
                {
                    AufAchse(A("a"), new V3(-1, 0, 0)); AufAchse(A("b"), new V3(1, 0, 0));
                    // Abzweig c nach dem Abzweigwinkel (B7): Lc·(−cos w, sin w, 0), ohne Parameter w = 90 → (0, Lc, 0)/(0, 1, 0)
                    double w = WinkelParameter(a) ?? 90;
                    if (!(w > 1e-9) || w > 90 + 1e-9)
                        throw new HlksFehler($"Abzweigwinkel winkel = {Leitungsgroesse.Zahl(w)}° liegt nicht in 0 < w ≤ 90.", LageVorschlag);
                    double r = w * Math.PI / 180;
                    AufAchse(A("c"), Math.Abs(w - 90) <= 1e-12 ? new V3(0, 1, 0) : new V3(-Math.Cos(r), Math.Sin(r), 0));
                    winkel = w;
                    break;
                }
                case "reduktion":
                    AufAchse(A("a"), new V3(-1, 0, 0));
                    if (A("a").Einbaulaenge > LageToleranz)
                        throw new HlksFehler($"Anschluss a liegt bei {Text(A("a").Lage)} – bei der Reduktion liegt a im Ursprung.", LageVorschlag);
                    AufAchse(A("b"), new V3(1, 0, 0));
                    break;
                default:   // reinigungsoeffnung
                    AufAchse(A("a"), new V3(-1, 0, 0)); AufAchse(A("b"), new V3(1, 0, 0));
                    break;
            }
        }
        return new Formteilgeometrie { Art = art ?? def.Kategorie, Typ = typ, Winkel = winkel, Anschluesse = liste };
    }

    static string Text(double[] p) => "(" + string.Join(", ", p.Select(Leitungsgroesse.Zahl)) + ")";

    /// <summary>Richtung = soll und Lage auf der Achse in Richtung soll (Abstand ≥ 0).</summary>
    static void AufAchse(FormteilAnschluss x, V3 soll)
    {
        var r = V3.Aus(x.Richtung);
        if ((r - soll).Laenge > RichtungToleranz)
            throw new HlksFehler($"Anschluss {x.Name} zeigt nach {Text(x.Richtung)}, verlangt ist {Text(soll.Feld())}.", LageVorschlag);
        var l = V3.Aus(x.Lage);
        var quer = l - soll * l.Mal(soll);
        if (quer.Laenge > LageToleranz || l.Mal(soll) < -LageToleranz)
            throw new HlksFehler($"Anschluss {x.Name} liegt bei {Text(x.Lage)} – nicht auf der Achse in Richtung {Text(soll.Feld())} durch den Ursprung.", LageVorschlag);
    }
}
