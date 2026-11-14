// Modul 5 Teil A-D (entwurf/modul-5.md, Punkt 7): Abfragen der Heizung über die eine Tür (Tuer.Abfrage, MCP, REST):
//   heizung_lesen    { anlage? }                          Textansicht: Erzeuger → Speicher → Verteiler → Heizgruppen mit Gliedern, Temperaturen und Σ Normleistung
//   heizung_leistung { anlage? | heizgruppe? | raum? | leitung? }  Σ Normleistung der Verbraucher (Angabe) je Anlage, Heizgruppe, Verteiler, Raum bzw. Leitung
// Dazu element_lesen (Block «heizung», abgeleitet spreizung/leistung bzw. summeLeistung) und «warum» für spreizung, leistung (Heizgruppe) und
// summeLeistung (Leitung, Verteiler). system_lesen und strang_text nennen Gegenstück und Heizgruppen (Abfragen.cs).

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class HeizungAbfragen
{
    public const string LesenText = "Heizungs- bzw. Kälteanlage als Textansicht (Modul 5): Systempaare, Erzeuger, Speicher und Ausdehnungsgefässe, Verteiler, dann je Heizgruppe " +
        "Abgang, art, Verbraucherart, Systeme, Auslegungstemperaturen (Eingabe, mit Herkunft) und Spreizung, Glieder in Fliessreihenfolge (Vorlauf, Rücklauf), Äste mit " +
        "Unterverteilern und Verbrauchern je Geschoss, Σ Normleistung der Verbraucher (Angabe, nicht umgerechnet); zuletzt Elemente ohne Heizgruppe und die Befunde P33–P39. " +
        "Ohne anlage: alle Anlagen mit Heiz- bzw. Kältesystemen.";
    public const string LeistungText = "Σ Normleistung der Verbraucher (Angabe) – Summe der Angaben «leistung» (W), ausdrücklich nicht auf Auslegungstemperaturen umgerechnet " +
        "(Teil B): mit heizgruppe die Verbraucher der Gruppe, mit raum die Verbraucher im Raum neben Raum.heizleistung (nur Information), mit leitung die Verbraucher stromab " +
        "(Vorlauf) bzw. stromauf (Rücklauf) mit Rechenweg; mit anlage bzw. ohne Parameter je Anlage, Heizgruppe und Verteiler.";
    public const string ParAnlage = "Nur diese Anlage (Kennzeichen)";
    public const string ParHeizgruppe = "Eine Heizgruppe (Kennzeichen)";
    public const string ParRaum = "Ein Raum (Kennzeichen): Verbraucher im Raum neben Raum.heizleistung";
    public const string ParLeitung = "Eine Leitung (Kennzeichen): Summe mit den Beiträgen der Verbraucher";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("heizung_lesen", LesenText, [new("anlage", "text", false, ParAnlage)]),
        new("heizung_leistung", LeistungText,
            [new("anlage", "text", false, ParAnlage), new("heizgruppe", "text", false, ParHeizgruppe), new("raum", "text", false, ParRaum), new("leitung", "text", false, ParLeitung)]),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "heizung_lesen" => Lesen(p, Param.Text(par, "anlage")),
        "heizung_leistung" => Leistung(p, Param.Text(par, "anlage"), Param.Text(par, "heizgruppe"), Param.Text(par, "raum"), Param.Text(par, "leitung")),
        _ => null,
    };

    static string Z(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    static string W(double x) => x.ToString("0.#", CultureInfo.InvariantCulture) + " W";

    static string Herkunft(Wert w) => $"{w.Herkunft.Art}{(string.IsNullOrWhiteSpace(w.Herkunft.Quelle) ? "" : ", " + w.Herkunft.Quelle)}";

    /// <summary>Anlagen mit Heiz- bzw. Kältesystemen (Systeme mit Medienpaar) oder Heizgruppen.</summary>
    public static List<Element> Anlagen(Modell m, Katalog k) =>
        m.DerKlasse("Anlage").Where(a => m.DerKlasse("System").Any(s => s.TeilVon == a.Id && k.PaarRolle(s.Text("medium")) is not null)
                                         || m.DerKlasse("Heizgruppe").Any(h => h.TeilVon == a.Id))
            .OrderBy(a => a.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();

    static Element AnlageWaehlen(Modell m, Katalog k, string bezug)
    {
        var a = m.Hole(bezug, "Anlage");
        if (!k.IstArt(a, "Anlage")) throw new HlksFehler($"{a.Kennzeichen} ist keine Anlage, sondern {a.Klasse}.", "Anlagen: " + string.Join(", ", Anlagen(m, k).Select(x => x.Kennzeichen)));
        return a;
    }

    // ------------------------------------------------------------------ heizung_lesen

    public static string Lesen(Projekt p, string? anlage)
    {
        var m = p.Modell; var k = p.Katalog;
        var anlagen = anlage is not null ? [AnlageWaehlen(m, k, anlage)] : Anlagen(m, k);
        if (anlagen.Count == 0) return "Keine Heizungs- oder Kälteanlage (kein System mit Vor-/Rücklaufmedium, keine Heizgruppe).";
        var stand = Heizgruppen.Ergebnis(m, k);
        var netz = HeizungLeistung.Ergebnis(m, k);
        var befunde = Pruefungen.Alle(m, k).Where(b => HeizungPruefung.Codes.ContainsKey(b.Code)).ToList();
        var sb = new StringBuilder();
        foreach (var a in anlagen) AnlageText(sb, m, k, stand, netz, a, befunde);
        return sb.ToString().TrimEnd();
    }

    static string Kz(Modell m, string? id) => m.Finde(id)?.Kennzeichen ?? "–";
    static string Gs(Modell m, Element e) => m.Finde(e.Geschoss)?.Kennzeichen ?? "ohne Geschoss";

    static void AnlageText(StringBuilder sb, Modell m, Katalog k, HeizungStand stand, HeizungNetz netz, Element a, List<Befund> befunde)
    {
        var systeme = m.DerKlasse("System").Where(s => s.TeilVon == a.Id).OrderBy(s => s.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        var ids = systeme.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        bool InAnlage(Element e) => (e.System is not null && ids.Contains(e.System)) || Heizgruppen.SystemeVon(m, e).Any(s => ids.Contains(s.Id));
        sb.AppendLine($"Anlage {a.Kennzeichen} «{a.Name}»{(a.Text("beschreibung") is { } bes ? " – " + bes : "")}");
        // Systempaare
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var paare = new List<string>();
        foreach (var s in systeme.Where(s => k.PaarRolle(s.Text("medium")) == "vorlauf"))
        {
            var g = Heizgruppen.Gegenstueck(m, s);
            paare.Add(g is not null && Heizgruppen.IstPaar(m, s, g) ? $"{s.Kennzeichen} ({s.Text("medium")}) ↔ {g.Kennzeichen} ({g.Text("medium")})" : $"{s.Kennzeichen} ({s.Text("medium")}) ohne Gegenstück");
            gesehen.Add(s.Id); if (g is not null) gesehen.Add(g.Id);
        }
        foreach (var s in systeme.Where(s => !gesehen.Contains(s.Id) && k.PaarRolle(s.Text("medium")) == "ruecklauf")) paare.Add($"{s.Kennzeichen} ({s.Text("medium")}) ohne Gegenstück");
        sb.AppendLine($"Systempaare: {(paare.Count == 0 ? "keine" : string.Join("; ", paare))}");
        // Erzeugung, Speicher, Verteiler
        var zentralen = m.Elemente.Where(e => Netz.Rolle(k, e) == "zentrale" && Heizgruppen.HatHeizmedium(m, k, e) && InAnlage(e)).ToList();
        // Ausdehnungsgefässe und Wärmetauscher ohne festes Medium: über ihre Verbindung
        zentralen.AddRange(m.Elemente.Where(e => e.Klasse is "Ausdehnungsgefaess" or "Waermetauscher" && !zentralen.Contains(e) && InAnlage(e)));
        string Zeile(Element e)
        {
            var t = new List<string> { $"{e.Kennzeichen} {e.Klasse}" };
            if (e.Text("art") is { } art) t.Add(art);
            if (e.Name is { Length: > 0 } n) t.Add($"«{n}»");
            foreach (var (eig, einheit) in new[] { ("leistung", "kW"), ("inhalt", "l"), ("vordruck", "bar") })
                if (e.Eigenschaft(eig) is { } w && w.AlsZahl() is double z) t.Add($"{eig} {Z(z)} {w.Einheit ?? einheit} (Angabe: {Herkunft(w)})");
            t.Add($"[{Gs(m, e)}]");
            return string.Join(" ", t);
        }
        void Liste(string titel, IEnumerable<Element> l)
        {
            var x = l.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
            if (x.Count > 0) sb.AppendLine($"{titel}: " + string.Join("; ", x.Select(Zeile)));
        }
        Liste("Erzeugung", zentralen.Where(e => e.Klasse is "Waermeerzeuger" or "Kaelteerzeuger"));
        Liste("Wärmetauscher", zentralen.Where(e => e.Klasse == "Waermetauscher"));
        Liste("Speicher", zentralen.Where(e => e.Klasse is "Speicher"));
        Liste("Ausdehnungsgefässe", zentralen.Where(e => e.Klasse == "Ausdehnungsgefaess"));
        var unterverteiler = stand.Gruppen.Values.SelectMany(w => w.Unterverteiler).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var verteiler = zentralen.Where(e => k.IstArt(e, "Verteiler") && !unterverteiler.Contains(e.Id)).ToList();
        foreach (var v in verteiler.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase))
        {
            var abg = v.Anschluesse.Where(x => k.PaarRolle(Netz.Medium(m, v, x)) == "vorlauf" && x.Fluss != "rein").Select(x => x.Name).ToList();
            var s = netz.Verteiler(v.Id);
            sb.AppendLine($"Verteiler {v.Kennzeichen}{(v.Name is { Length: > 0 } vn ? $" «{vn}»" : "")} [{Gs(m, v)}]: Abgänge {string.Join(", ", abg)}" +
                          (s is null ? "" : $" · {HeizungLeistung.Beschriftung} {W(s.SummeW)} ({s.Anzahl} Verbraucher{(s.OhneAngabe.Count > 0 ? $", {s.OhneAngabe.Count} ohne Angabe" : "")})"));
        }
        // Heizgruppen
        var gruppen = stand.Gruppen.Values.Where(w => w.Gruppe.TeilVon == a.Id || (w.Gruppe.TeilVon is null && w.Vorlauf is not null && ids.Contains(w.Vorlauf.Id)))
            .OrderBy(w => w.Gruppe.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
        sb.AppendLine($"Heizgruppen ({gruppen.Count}):");
        foreach (var w in gruppen) GruppeText(sb, m, k, netz, w);
        // ohne Heizgruppe
        var ohne = m.Elemente.Where(e => InAnlage(e) && !stand.GruppenJeElement.ContainsKey(e.Id)
                                         && (HeizungLeistung.IstHeizverbraucher(m, k, e) || e.Klasse is "Pumpe" or "Mischventil")).ToList();
        if (ohne.Count > 0)
            sb.AppendLine($"Ohne Heizgruppe ({ohne.Count}): {string.Join(", ", ohne.GroupBy(e => e.Klasse).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()} ({string.Join(", ", g.Select(e => e.Kennzeichen).Order(StringComparer.OrdinalIgnoreCase).Take(6))}{(g.Count() > 6 ? " …" : "")})"))}");
        var meine = befunde.Where(b => b.Element == a.Kennzeichen || gruppen.Any(w => w.Gruppe.Kennzeichen == b.Element)
                                       || (m.Finde(b.Element) is { } be && InAnlage(be))).ToList();
        if (meine.Count > 0)
            sb.AppendLine($"Befunde: {string.Join(", ", meine.GroupBy(b => b.Code).OrderBy(g => g.Key).Select(g => $"{g.Key} {HeizungPruefung.Codes[g.Key]} {g.Count()}×"))}");
        sb.AppendLine();
    }

    static void GruppeText(StringBuilder sb, Modell m, Katalog k, HeizungNetz netz, HeizgruppenWeg w)
    {
        var hg = w.Gruppe;
        string Auswahl(string eig)
        {
            var wert = hg.Text(eig);
            if (wert is null) return "–";
            var d = k.Klassen["Heizgruppe"].Eigenschaften.FirstOrDefault(x => x.Name == eig);
            var name = d?.AuswahlNamen?.GetValueOrDefault(wert) ?? wert;
            return hg.Eigenschaft(eig)!.Herkunft.Art == "berechnung" ? $"{name} (abgeleitet)" : name;
        }
        sb.AppendLine($"  {hg.Kennzeichen} «{hg.Name}» · art {Auswahl("art")} · Verbraucher {Auswahl("verbraucherart")} · Abgang {w.AbgangText ?? "fehlt"}" +
                      $" · Vorlauf {w.Vorlauf?.Kennzeichen ?? "–"} / Rücklauf {w.Ruecklauf?.Kennzeichen ?? "–"}{(w.Vorlauf is not null && w.Ruecklauf is not null && !w.Systempaar ? " (kein Systempaar)" : "")}");
        string T(string eig) => hg.Eigenschaft(eig) is { } x && x.AlsZahl() is double z ? $"{Z(z)} °C ({Herkunft(x)})" : "fehlt";
        var spreizung = Heizgruppen.Spreizung(hg);
        sb.AppendLine($"    Temperaturen (Eingabe): Vorlauf {T("vlTemp")}, Rücklauf {T("rlTemp")}{(spreizung is double sp ? $", Spreizung {Z(sp)} K" : "")}");
        string Glied(HeizGlied g)
        {
            var e = g.Element;
            var art = e.Text("art");
            return $"{e.Kennzeichen} {e.Klasse}{(art is null ? "" : " " + art)}{(g.Ast is int a ? $" (Ast {a})" : "")}";
        }
        var vl = w.Glieder.Where(g => g.Weg == "vl").ToList();
        var rl = w.Glieder.Where(g => g.Weg == "rl").ToList();
        sb.AppendLine($"    Vorlauf: {(vl.Count == 0 ? "keine Glieder" : string.Join(" → ", vl.Select(Glied)))}");
        sb.AppendLine($"    Rücklauf: {(rl.Count == 0 ? "keine Glieder" : string.Join(" → ", rl.Select(Glied)))}");
        if (w.Aeste.Count > 0)
        {
            sb.AppendLine($"    Verteilung ab {w.Abzweig?.Kennzeichen} {w.Abzweig?.Klasse} [{(w.Abzweig is null ? "" : Gs(m, w.Abzweig))}]: {w.Aeste.Count} Äste");
            foreach (var ast in w.Aeste)
            {
                var jeGeschoss = ast.Verbraucher.GroupBy(v => m.Finde(v.Geschoss)).OrderBy(g => g.Key?.Zahl("kote") ?? double.MinValue)
                    .Select(g => $"{g.Key?.Kennzeichen ?? "ohne Geschoss"} {g.Count()}");
                sb.AppendLine($"      Ast {ast.Nummer} ab {ast.Anfang.Kennzeichen}: {ast.Verbraucher.Count} Verbraucher ({string.Join(", ", jeGeschoss)})" +
                              (ast.Unterverteiler.Count > 0 ? $"; Unterverteiler {string.Join(", ", ast.Unterverteiler.Select(u => $"{u.Kennzeichen} [{Gs(m, u)}] {UnterverteilerKreise(m, k, w, u)} Kreise"))}" : ""));
            }
        }
        else if (w.Unterverteiler.Count > 0)
            sb.AppendLine($"    Unterverteiler: {string.Join(", ", w.Unterverteiler.Select(u => $"{u.Kennzeichen} [{Gs(m, u)}] {UnterverteilerKreise(m, k, w, u)} Kreise"))}");
        var summe = netz.Summe(w.Verbraucher);
        var klassen = w.Verbraucher.GroupBy(v => v.Klasse).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}");
        sb.AppendLine($"    Verbraucher: {w.Verbraucher.Count}{(w.Verbraucher.Count > 0 ? $" ({string.Join(", ", klassen)})" : "")} · {HeizungLeistung.Beschriftung}: {W(summe.SummeW)}, nicht umgerechnet" +
                      (summe.OhneAngabe.Count > 0 ? $" ({summe.OhneAngabe.Count} ohne Angabe)" : ""));
        if (w.Grenzen.Count > 0) sb.AppendLine($"    Grenzen des Wegs: {string.Join(", ", w.Grenzen.Select(g => $"{g.Kennzeichen} {g.Klasse}"))}");
        foreach (var h in w.Hinweise) sb.AppendLine($"    Hinweis: {h}");
    }

    /// <summary>Kreise eines Unterverteilers: Verbraucher, deren nächster Unterverteiler er ist (Vorlauf, sonst Rücklauf).</summary>
    static int UnterverteilerKreise(Modell m, Katalog k, HeizgruppenWeg w, Element u) => w.KreiseVon(u).Count;

    // ------------------------------------------------------------------ heizung_leistung

    static JsonObject SummeJson(HeizSumme s)
    {
        var o = new JsonObject { ["summeW"] = s.SummeW, ["verbraucher"] = s.Anzahl };
        if (s.OhneAngabe.Count > 0) o["ohneAngabe"] = new JsonArray(s.OhneAngabe.Select(v => (JsonNode)JsonValue.Create(v.Kennzeichen)).ToArray());
        if (s.Masche) o["masche"] = true;
        // Massen- und Volumenstrom (Christian 02.11.2026): Summe der Verbraucher mit allen Angaben; die übrigen stehen in ohneStrom
        o["massenstrom_kgh"] = s.MassenstromKgh;
        o["volumenstrom_lh"] = s.VolumenstromLh;
        if (s.OhneStrom is { Count: > 0 } os) o["ohneStrom"] = new JsonArray(os.Select(v => (JsonNode)JsonValue.Create(v.Kennzeichen)).ToArray());
        return o;
    }

    static JsonObject GruppeJson(Modell m, HeizungNetz netz, HeizgruppenWeg w)
    {
        var o = new JsonObject
        {
            ["heizgruppe"] = w.Gruppe.Kennzeichen, ["name"] = w.Gruppe.Name, ["abgang"] = w.AbgangText,
            ["vlTemp"] = w.Gruppe.Zahl("vlTemp"), ["rlTemp"] = w.Gruppe.Zahl("rlTemp"), ["spreizungK"] = Heizgruppen.Spreizung(w.Gruppe),
        };
        foreach (var (key, v) in SummeJson(netz.Summe(w.Verbraucher))) o[key] = v?.DeepClone();
        return o;
    }

    public static JsonObject Leistung(Projekt p, string? anlage, string? heizgruppe, string? raum, string? leitung)
    {
        var m = p.Modell; var k = p.Katalog;
        var stand = Heizgruppen.Ergebnis(m, k);
        var netz = HeizungLeistung.Ergebnis(m, k);
        JsonObject Fuss(JsonObject o)
        {
            o["beschriftung"] = HeizungLeistung.Beschriftung; o["einheit"] = "W"; o["hinweis"] = HeizungLeistung.NichtUmgerechnet;
            return o;
        }
        JsonArray Liste(IEnumerable<Element> l) => new(l.Select(v => (JsonNode)new JsonObject
        {
            ["verbraucher"] = v.Kennzeichen, ["klasse"] = v.Klasse, ["raum"] = m.Finde(v.Raum)?.Kennzeichen, ["geschoss"] = m.Finde(v.Geschoss)?.Kennzeichen,
            ["leistungW"] = HeizungLeistung.LeistungW(k, v), ["herkunft"] = v.Eigenschaft("leistung") is { } w ? Herkunft(w) : null,
        }).ToArray());
        if (heizgruppe is not null)
        {
            var hg = m.Hole(heizgruppe, "Heizgruppe");
            if (hg.Klasse != "Heizgruppe") throw new HlksFehler($"{hg.Kennzeichen} ist keine Heizgruppe, sondern {hg.Klasse}.");
            var w = stand.Gruppen[hg.Id];
            var o = GruppeJson(m, netz, w);
            o["verbraucherListe"] = Liste(w.Verbraucher);
            o["unterverteiler"] = new JsonArray(w.Unterverteiler.Select(u => (JsonNode)new JsonObject { ["verteiler"] = u.Kennzeichen, ["geschoss"] = m.Finde(u.Geschoss)?.Kennzeichen }).ToArray());
            return Fuss(o);
        }
        if (raum is not null)
        {
            var r = m.HoleDerKlasse(raum, "Raum");
            var v = m.Elemente.Where(e => e.Raum == r.Id && HeizungLeistung.IstHeizverbraucher(m, k, e)).ToList();
            var o = new JsonObject { ["raum"] = r.Kennzeichen, ["name"] = r.Name };
            foreach (var (key, x) in SummeJson(netz.Summe(v))) o[key] = x?.DeepClone();
            o["verbraucherListe"] = Liste(v.OrderBy(e => e.Kennzeichen, StringComparer.OrdinalIgnoreCase));
            o["heizleistungRaum"] = r.Zahl("heizleistung");
            o["heizleistungRaumHerkunft"] = r.Eigenschaft("heizleistung") is { } hw ? Herkunft(hw) : null;
            o["vergleich"] = "Nur Information, kein Befund: Normleistung der Verbraucher und Raumheizlast sind nicht vergleichbar, solange die Umrechnung auf Auslegungstemperaturen (Teil B) fehlt.";
            return Fuss(o);
        }
        if (leitung is not null)
        {
            var l = m.Hole(leitung, "Leitung");
            if (!k.IstArt(l, "Leitung")) throw new HlksFehler($"{l.Kennzeichen} ist keine Leitung, sondern {l.Klasse}.", "Für Verteiler: heizung_leistung ohne Parameter (je Verteiler).");
            if (netz.Leitung(l.Id) is not { } s)
                return Fuss(new JsonObject { ["leitung"] = l.Kennzeichen, ["ausgenommen"] = "Die Leitung liegt in keinem Heiz- bzw. Kältemedium (Vor-/Rücklauf)." });
            var weg = netz.LeitungWeg[l.Id];
            var o = new JsonObject
            {
                ["leitung"] = l.Kennzeichen, ["system"] = m.Finde(l.System)?.Kennzeichen, ["weg"] = weg,
                ["richtung"] = weg == "vl" ? "Verbraucher stromab" : "Verbraucher stromauf",
            };
            foreach (var (key, x) in SummeJson(s)) o[key] = x?.DeepClone();
            o["verbraucherListe"] = Liste(s.Verbraucher);
            o["rechenweg"] = Rechenweg(k, s, weg);
            o["heizgruppen"] = new JsonArray(stand.In(l.Id).Select(w => (JsonNode)JsonValue.Create(w.Gruppe.Kennzeichen)).ToArray());
            return Fuss(o);
        }
        var anlagen = anlage is not null ? [AnlageWaehlen(m, k, anlage)] : Anlagen(m, k);
        var arr = new JsonArray();
        foreach (var a in anlagen)
        {
            var ids = m.DerKlasse("System").Where(s => s.TeilVon == a.Id).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            var verbraucher = m.Elemente.Where(e => HeizungLeistung.IstHeizverbraucher(m, k, e) && Heizgruppen.SystemeVon(m, e).Any(s => ids.Contains(s.Id))).ToList();
            var o = new JsonObject { ["anlage"] = a.Kennzeichen, ["name"] = a.Name };
            foreach (var (key, x) in SummeJson(netz.Summe(verbraucher))) o[key] = x?.DeepClone();
            o["heizgruppen"] = new JsonArray(stand.Gruppen.Values.Where(w => w.Gruppe.TeilVon == a.Id).OrderBy(w => w.Gruppe.Kennzeichen, StringComparer.OrdinalIgnoreCase)
                .Select(w => (JsonNode)GruppeJson(m, netz, w)).ToArray());
            o["verteiler"] = new JsonArray(netz.JeVerteiler.Keys.Select(id => m.Finde(id)!).Where(v => Heizgruppen.SystemeVon(m, v).Any(s => ids.Contains(s.Id)))
                .OrderBy(v => v.Kennzeichen, StringComparer.OrdinalIgnoreCase).Select(v =>
                {
                    var vo = new JsonObject { ["verteiler"] = v.Kennzeichen, ["geschoss"] = m.Finde(v.Geschoss)?.Kennzeichen };
                    foreach (var (key, x) in SummeJson(netz.Verteiler(v.Id)!)) vo[key] = x?.DeepClone();
                    return (JsonNode)vo;
                }).ToArray());
            arr.Add(o);
        }
        var erg = new JsonObject { ["anlagen"] = arr };
        if (netz.Maschen.Count > 0) erg["maschen"] = new JsonArray(netz.Maschen.Keys.Select(id => (JsonNode)JsonValue.Create(Kz(m, id))).ToArray());
        return Fuss(erg);
    }

    static string Rechenweg(Katalog k, HeizSumme s, string weg)
    {
        var teile = s.Verbraucher.Where(v => !s.OhneAngabe.Contains(v)).Select(v => $"{v.Kennzeichen} {Z(HeizungLeistung.LeistungW(k, v) ?? 0)}").ToList();
        return $"{HeizungLeistung.Beschriftung} = Summe der Angaben «leistung» aller Verbraucher {(weg == "vl" ? "stromab (Vorlauf)" : "stromauf (Rücklauf)")}, je Verbraucher einmal: " +
               $"{(teile.Count == 0 ? "keine" : string.Join(" + ", teile))} = {Z(s.SummeW)} W; nicht umgerechnet" + (s.OhneAngabe.Count > 0 ? $"; ohne Angabe: {string.Join(", ", s.OhneAngabe.Select(v => v.Kennzeichen))}" : "");
    }

    // ------------------------------------------------------------------ element_lesen, warum

    /// <summary>Ergänzt element_lesen: Heizgruppe (Block «heizung» mit Abgang, Systemen, Gliedern, Verbrauchern; abgeleitet spreizung, leistung), System
    /// (Gegenstück und Heizgruppen), Leitung im Heiz-/Kältemedium (abgeleitet summeLeistung), Element in einer Gruppe (heizgruppen).</summary>
    public static void ElementErgaenzen(Projekt p, Element e, JsonObject o)
    {
        var m = p.Modell; var k = p.Katalog;
        JsonObject Abg() => o["abgeleitet"] as JsonObject ?? (JsonObject)(o["abgeleitet"] = new JsonObject());
        if (e.Klasse == "Heizgruppe")
        {
            var stand = Heizgruppen.Ergebnis(m, k);
            var w = stand.Gruppen[e.Id];
            var netz = HeizungLeistung.Ergebnis(m, k);
            var s = netz.Summe(w.Verbraucher);
            o["heizung"] = new JsonObject
            {
                ["abgang"] = w.AbgangText, ["abgangRlWoher"] = w.AbgangRlWoher, ["vorlauf"] = w.Vorlauf?.Kennzeichen, ["ruecklauf"] = w.Ruecklauf?.Kennzeichen,
                ["systempaar"] = w.Systempaar, ["mediumVl"] = w.MediumVl, ["mediumRl"] = w.MediumRl,
                ["glieder"] = new JsonArray(w.Glieder.Select(g => (JsonNode)new JsonObject
                {
                    ["reihe"] = g.Reihe, ["element"] = g.Element.Kennzeichen, ["klasse"] = g.Element.Klasse, ["art"] = g.Element.Text("art"), ["weg"] = g.Weg,
                    ["tiefe"] = g.Tiefe, ["ast"] = g.Ast, ["eintritt"] = g.Eintritt,
                }).ToArray()),
                ["abzweig"] = w.Abzweig?.Kennzeichen,
                ["aeste"] = new JsonArray(w.Aeste.Select(a => (JsonNode)new JsonObject
                {
                    ["ast"] = a.Nummer, ["anfang"] = a.Anfang.Kennzeichen,
                    ["unterverteiler"] = new JsonArray(a.Unterverteiler.Select(u => (JsonNode)JsonValue.Create(u.Kennzeichen)).ToArray()),
                    ["verbraucher"] = a.Verbraucher.Count,
                }).ToArray()),
                ["unterverteiler"] = new JsonArray(w.Unterverteiler.Select(u => (JsonNode)JsonValue.Create(u.Kennzeichen)).ToArray()),
                ["verbraucher"] = new JsonArray(w.Verbraucher.Select(v => (JsonNode)JsonValue.Create(v.Kennzeichen)).ToArray()),
                ["grenzen"] = new JsonArray(w.Grenzen.Select(g => (JsonNode)JsonValue.Create(g.Kennzeichen)).ToArray()),
                ["summeLeistungW"] = s.SummeW, ["beschriftung"] = HeizungLeistung.Beschriftung, ["hinweis"] = HeizungLeistung.NichtUmgerechnet,
                ["hinweise"] = new JsonArray(w.Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray()),
            };
            var abg = Abg();
            if (Heizgruppen.Spreizung(e) is double sp) abg["spreizung_K"] = sp;
            abg["leistung_W"] = s.SummeW;
            return;
        }
        if (e.Klasse == "System" && k.PaarRolle(e.Text("medium")) is { } rolle)
        {
            var g = Heizgruppen.Gegenstueck(m, e);
            o["heizung"] = new JsonObject
            {
                ["rolle"] = rolle, ["gegenstueck"] = g?.Kennzeichen, ["systempaar"] = g is not null && Heizgruppen.IstPaar(m, e, g),
                ["gegenstueckMedium"] = k.GegenstueckMedium(e.Text("medium")),
                ["heizgruppen"] = new JsonArray(m.DerKlasse("Heizgruppe").Where(h => h.Bezuege.Values.Contains(e.Id)).Select(h => (JsonNode)JsonValue.Create(h.Kennzeichen)).ToArray()),
            };
            return;
        }
        if (!Heizgruppen.HatHeizmedium(m, k, e) && e.Klasse is not ("Ausdehnungsgefaess" or "Waermetauscher")) return;
        var st = Heizgruppen.Ergebnis(m, k);
        var gruppen = st.In(e.Id).Select(w => w.Gruppe.Kennzeichen).ToList();
        var block = new JsonObject { ["heizgruppen"] = new JsonArray(gruppen.Select(x => (JsonNode)JsonValue.Create(x)).ToArray()) };
        var n = HeizungLeistung.Ergebnis(m, k);
        if (k.IstArt(e, "Leitung") && n.Leitung(e.Id) is { } ls)
        {
            foreach (var (key, x) in SummeJson(ls)) block[key] = x?.DeepClone();
            block["weg"] = n.LeitungWeg[e.Id];
            block["beschriftung"] = HeizungLeistung.Beschriftung;
            Abg()["summeLeistung_W"] = ls.SummeW;
            Abg()["massenstrom_kgh"] = ls.MassenstromKgh;
            Abg()["volumenstrom_lh"] = ls.VolumenstromLh;
        }
        else if (k.IstArt(e, "Verteiler") && n.Verteiler(e.Id) is { } vs)
        {
            foreach (var (key, x) in SummeJson(vs)) block[key] = x?.DeepClone();
            block["beschriftung"] = HeizungLeistung.Beschriftung;
            Abg()["summeLeistung_W"] = vs.SummeW;
            Abg()["massenstrom_kgh"] = vs.MassenstromKgh;
            Abg()["volumenstrom_lh"] = vs.VolumenstromLh;
        }
        else if (n.Verbraucher.TryGetValue(e.Id, out var vw))
        {
            // Verbraucher: Leistung mit Herkunft (Angabe bzw. Raum gleich verteilt), Massen- und Volumenstrom bzw. warum nicht
            if (vw is double w) Abg()["leistung_W"] = w;
            if (n.LeistungWoher.TryGetValue(e.Id, out var woher)) block["leistungWoher"] = woher;
            if (n.Strom.TryGetValue(e.Id, out var strom))
            {
                if (strom.Kgh is double kgh) { Abg()["massenstrom_kgh"] = kgh; Abg()["volumenstrom_lh"] = strom.Lh; }
                if (strom.SpreizungK is double dt) block["spreizungK"] = dt;
                if (strom.Stoff is { } sw) block["stoffwerte"] = new JsonObject { ["cp_kJkgK"] = Math.Round(sw.CpKjKgK, 4), ["rho_kgm3"] = Math.Round(sw.RhoKgM3, 2), ["fluid"] = sw.Fluid, ["quelle"] = sw.Quelle };
                if (strom.Grund is { } grund) block["ohneStrom"] = grund;
                if (strom.Basis is { } basis) { block["stromBasis"] = basis; block["stromBasisW"] = strom.BasisW; }
                if (strom.Basis == "normleistung") block["stromHinweis"] = HeizungLeistung.HinweisNormleistung;
            }
        }
        o["heizung"] = block;
    }

    /// <summary>«warum» für spreizung und leistung einer Heizgruppe sowie summeLeistung einer Leitung bzw. eines Verteilers; null = nicht zuständig.</summary>
    public static JsonObject? Warum(Projekt p, Element e, string eigenschaft)
    {
        var m = p.Modell; var k = p.Katalog;
        var name = eigenschaft.Trim().ToLowerInvariant();
        if (e.Klasse == "Heizgruppe" && name is "spreizung" or "leistung")
        {
            if (name == "spreizung")
                return new JsonObject
                {
                    ["element"] = e.Kennzeichen, ["eigenschaft"] = "spreizung", ["wert"] = Heizgruppen.Spreizung(e), ["einheit"] = "K",
                    ["rechenweg"] = Heizgruppen.Spreizung(e) is double sp
                        ? $"|vlTemp − rlTemp| = |{Z(e.Zahl("vlTemp")!.Value)} − {Z(e.Zahl("rlTemp")!.Value)}| = {Z(sp)} K (abgeleitet, nicht gespeichert)"
                        : "Ohne vlTemp und rlTemp keine Spreizung (Eingabe des Planers, Befund P36).",
                    ["quelle"] = new JsonArray(new[] { "vlTemp", "rlTemp" }.Where(x => e.Eigenschaft(x) is not null).Select(x => (JsonNode)JsonValue.Create($"{x}: {Herkunft(e.Eigenschaft(x)!)}")).ToArray()),
                };
            var w = Heizgruppen.Ergebnis(m, k).Gruppen[e.Id];
            var s = HeizungLeistung.Ergebnis(m, k).Summe(w.Verbraucher);
            return new JsonObject
            {
                ["element"] = e.Kennzeichen, ["eigenschaft"] = "leistung", ["wert"] = s.SummeW, ["einheit"] = "W", ["beschriftung"] = HeizungLeistung.Beschriftung,
                ["rechenweg"] = $"{HeizungLeistung.Beschriftung} = Summe der Angaben «leistung» der {s.Anzahl} Verbraucher der Gruppe (abgeleitet ab {w.AbgangText}): " +
                                $"{string.Join(" + ", s.Verbraucher.Where(v => !s.OhneAngabe.Contains(v)).Take(50).Select(v => $"{v.Kennzeichen} {Z(HeizungLeistung.LeistungW(k, v) ?? 0)}"))} = {Z(s.SummeW)} W; nicht umgerechnet",
                ["ohneAngabe"] = new JsonArray(s.OhneAngabe.Select(v => (JsonNode)JsonValue.Create(v.Kennzeichen)).ToArray()),
                ["hinweis"] = HeizungLeistung.NichtUmgerechnet,
            };
        }
        if (name == "summeleistung" && (k.IstArt(e, "Leitung") || k.IstArt(e, "Verteiler")))
        {
            var n = HeizungLeistung.Ergebnis(m, k);
            var s = k.IstArt(e, "Leitung") ? n.Leitung(e.Id) : n.Verteiler(e.Id);
            if (s is null) throw new HlksFehler($"{e.Kennzeichen} trägt keine Heizungsverbraucher (kein Vor-/Rücklaufmedium bzw. kein Verbraucher dahinter).");
            var weg = k.IstArt(e, "Leitung") ? n.LeitungWeg[e.Id] : "vl";
            return new JsonObject
            {
                ["element"] = e.Kennzeichen, ["eigenschaft"] = "summeLeistung", ["wert"] = s.SummeW, ["einheit"] = "W", ["beschriftung"] = HeizungLeistung.Beschriftung,
                ["rechenweg"] = Rechenweg(k, s, weg), ["masche"] = s.Masche,
            };
        }
        return null;
    }
}
