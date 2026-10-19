// Familien (Modul 2, B4): Massvergleich je Typ (entwurf/modul-2.md, «Massbilder in Datenblättern sind schematisch» und
// «Massbezüge in der Familie»). Je Massbezug (masse) und Typ wird das Mass an der Familie gemessen und mit dem Tabellenwert
// verglichen → Abweichungsliste (Mass, Soll, Ist, Toleranz, bestanden). Abfrage familie_masse {familie, typ?}.
//
// Ist (an der Familie):  von/bis → Abstand (richtung x, y, z: entlang der Achse; abstand: in der Ebene der Ansicht, ohne
//                        Ansicht räumlich) · nur parameter → Wert des Parameters.
// Soll (aus der Unterlage): der Befund in quellen des Masses, der zum Typ gehört. Zum Typ gehören nur Befunde, die der Typ selbst in
// typen[].quellen nennt, und Zellen derselben Tabellenzeile wie diese (Entscheid Hauptlauf 18.10.2026 nach Abnahmelauf 1: ULSA/ULDA
// mit gleichen Grössen auf einer Seite wurden verwechselt; nie eine Zeile nach Name oder Grösse suchen):
//   - zelle: die Zelle derselben Spalte in der Zeile des Typs dieser Tabelle (Massbezug zeigt auf die Spalte);
//   - tabelle: die Spalte, deren Kopf wie das Mass heisst («L [mm]» für L), in der Zeile des Typs;
//   - typ (Typkatalog), DXF-Bemassung, Masszahl, Wort, Zeile, Stutzen, Hülle: nur, wenn der Typ den Befund selbst nennt.
//   Nennt der Typ mehrere Zeilen derselben Tabelle oder ergeben Zellen verschiedener Zeilen verschiedene Werte: kein Soll, Hinweis
//   «Mass L: Tabellenzeile für Typ … nicht eindeutig».
//   Ausnahme (Entscheid Hauptlauf 18.10.2026): Nennt der Typ in den Unterlagen der Massquellen keine eigene Zelle, gilt die Zeile, in
//   der genau der Typname steht – nur wenn er in genau einer Zelle aller Tabellen dieser Unterlagen steht (Vermerk «Zeile über
//   Typname T1 (eindeutig)»); steht er mehrfach (z. B. Typenübersicht und Masstabelle), kein Soll und «nicht eindeutig».
//   Ohne solchen Befund: der Typwert des Parameters (bei von/bis mit parameter) bzw. die Quelle des Typwerts (nur parameter). Fehlt
//   parameter, gilt ein Typparameter mit dem Namen des Masses (vorgestelltes Ø entfällt: «ØD» → «D»), Vermerk «Soll aus Parameter L
//   (gleicher Name)», mit den Quellen seines Typwerts. Bleibt ein Mass ohne Soll, nennt die Prüfung beim Speichern (auch im
//   Trockenlauf) «Mass … ohne Sollwert: parameter (Typparameter mit dem Tabellenwert) angeben».
// ANNAHMEN (Prüfliste): Masse sind Längen in mm; Toleranz max(1 mm; 0.5 % des Solls), Durchmesser (Name beginnt mit Ø, ⌀, Φ
//   oder der Parameter ist ein Durchmesser) 1 mm; widersprechen sich mehrere Befunde eines Typs, gilt der mit dem höchsten
//   Vorrang als Soll und die Zeile nennt die übrigen (Hinweis). Ein Mass ohne Soll ist «ohne Sollwert» (kein Fehler).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Eine Zeile der Abweichungsliste.</summary>
public sealed class MassZeile
{
    public required string Mass { get; init; }
    public required string Typ { get; init; }
    public string? Ansicht { get; init; }
    public double? Soll { get; set; }
    public double? Ist { get; set; }
    public double? Toleranz { get; set; }
    /// <summary>null = nicht vergleichbar (kein Soll oder kein Ist).</summary>
    public bool? Bestanden { get; set; }
    /// <summary>Woher das Soll kommt: Befund (Id, Stelle) oder «Typwert …».</summary>
    public string? SollQuelle { get; set; }
    public string? QuelleId { get; set; }
    /// <summary>Befund-Ids hinter dem Soll, wenn es aus einem Typwert kommt (dessen quellen).</summary>
    public List<string> Quellen { get; } = [];
    /// <summary>Die Tabellenzeile des Typs ist nicht eindeutig: kein Soll.</summary>
    public bool Mehrdeutig { get; set; }
    public List<string> Hinweise { get; } = [];
    public List<(double Wert, string Stelle)> Weitere { get; } = [];

    public JsonObject AlsJson()
    {
        var o = new JsonObject { ["mass"] = Mass, ["typ"] = Typ };
        if (Ansicht is not null) o["ansicht"] = Ansicht;
        o["soll"] = Soll is double s ? Math.Round(s, 3) : null;
        o["ist"] = Ist is double i ? Math.Round(i, 3) : null;
        o["abweichung"] = Soll is double s2 && Ist is double i2 ? Math.Round(i2 - s2, 3) : null;
        o["toleranz"] = Toleranz is double t ? Math.Round(t, 3) : null;
        o["bestanden"] = Bestanden;
        o["einheit"] = "mm";
        if (SollQuelle is not null) o["quelle"] = SollQuelle;
        if (QuelleId is not null) o["befund"] = QuelleId;
        if (Quellen.Count > 0) o["quellen"] = new JsonArray(Quellen.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        if (Mehrdeutig) o["mehrdeutig"] = true;
        if (Weitere.Count > 0) o["weitereQuellen"] = new JsonArray(Weitere.Select(w => (JsonNode)new JsonObject { ["wert"] = Math.Round(w.Wert, 3), ["stelle"] = w.Stelle }).ToArray());
        if (Hinweise.Count > 0) o["hinweise"] = new JsonArray(Hinweise.Select(h => (JsonNode)JsonValue.Create(h)).ToArray());
        return o;
    }
}

public static class FamilienMasse
{
    public const string AbfrageText = "Massvergleich je Typ (Abweichungsliste): je Massbezug der Familie (masse) und Typ das Mass an der Familie (Abstand von/bis, " +
        "entlang einer Achse oder der Parameter) und der Tabellenwert der Unterlage (Befund in quellen des Masses, der zum Typ gehört; sonst der Typwert) – " +
        "Soll, Ist, Abweichung, Toleranz (Länge max(1 mm; 0.5 %), Durchmesser 1 mm), bestanden. Massbilder in Datenblättern sind schematisch: verglichen wird über die Werte.";

    sealed class WerteUmgebung(FamAufloesung a) : IFormelUmgebung
    {
        public FWert Wert(string name, int stelle) =>
            a.Werte.TryGetValue(name, out var w) && w is { } x ? x : throw new FormelFehler($"Parameter «{name}» hat keinen Wert", stelle);
    }

    /// <summary>Punkt (Formeln) mit den aufgelösten Werten eines Typs; wirft FormelFehler.</summary>
    public static double[] Auswerten(FamZahl[] punkt, FamAufloesung a)
    {
        var u = new WerteUmgebung(a);
        return punkt.Select(x => Z(x, u)).ToArray();
    }

    static double Z(FamZahl z, IFormelUmgebung u)
    {
        if (z.Konstante is double c) return c;
        var w = z.Formel!.Werten(u);
        if (w.IstText) throw new FormelFehler($"«{z.Text}» ergibt einen Text", 0);
        return w.Zahl;
    }

    /// <summary>Punkt der Familie in Ansichtskoordinaten (u, v) – wie FamilienAnsicht (vorne u = x, v = z …).</summary>
    public static double[] InAnsicht(string ansicht, double[] p) => ansicht switch
    {
        "hinten" => [-p[0], p[2]],
        "links" => [-p[1], p[2]],
        "rechts" => [p[1], p[2]],
        "oben" => [p[0], p[1]],
        "unten" => [-p[0], p[1]],
        _ => [p[0], p[2]],
    };

    static string? T(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Befund-Ids der quellen eines Massbezugs (Reihenfolge wie f.Masse).</summary>
    public static List<List<string>> MassQuellen(JsonObject def) =>
        (def["masse"] as JsonArray ?? []).OfType<JsonObject>().Select(o => (o["quellen"] as JsonArray ?? []).Select(T).Where(x => x is not null).Select(x => x!).ToList()).ToList();

    /// <summary>Alle Befund-Ids, die ein Typ in seinen quellen nennt.</summary>
    public static HashSet<string> TypQuellen(JsonObject def, string typ)
    {
        var to = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => T(o["name"])?.Trim() == typ);
        var s = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, l) in to?["quellen"] as JsonObject ?? []) foreach (var x in (l as JsonArray ?? [])) if (T(x) is { } id) s.Add(id);
        return s;
    }

    /// <summary>Zeilen einer Tabelle, deren Zellen der Typ selbst in seinen quellen nennt (Entscheid Hauptlauf 18.10.2026: nie eine
    /// Zeile nach Name oder Grösse suchen – zwei Tabellen mit denselben Grössen, z. B. ULSA/ULDA, würden verwechselt).</summary>
    public static List<int> ZeilenVonTyp(HashSet<string> typQuellen, string tabelle, BefundQuelle q) =>
        typQuellen.Select(id => q.Hole(id, out _)).Where(b => b?.Art == "zelle" && T(b.Zusatz?["tabelle"]) == tabelle)
            .Select(b => (int)(Json.Zahl(b!.Zusatz!["zeile"]) ?? 0)).Where(z => z > 0).Distinct().Order().ToList();

    /// <summary>Typparameter mit dem Namen des Masses (ohne vorgestelltes Ø: «ØD» → «D»), für Masse ohne parameter.</summary>
    public static FamParameter? Gleichnamig(FamilieDef f, string massName)
    {
        var n = massName.Trim().TrimStart('Ø', '⌀', 'ø', 'Φ').Trim();
        if (n.Length == 0) return null;
        var p = f.NachName.GetValueOrDefault(n) ?? f.Parameter.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
        return p is { Art: "typ", IstText: false } ? p : null;
    }

    /// <summary>Abweichungsliste: je Typ (oder nur <paramref name="nurTyp"/>) und Massbezug eine Zeile.</summary>
    public static List<MassZeile> Vergleichen(FamilieDef f, JsonObject def, BefundQuelle q, string? nurTyp)
    {
        var aus = new List<MassZeile>();
        var quellen = MassQuellen(def);
        foreach (var t in f.Typen)
        {
            if (nurTyp is not null && t.Name != nurTyp) continue;
            var a = FamilienAuswertung.Aufloesen(f, t.Name, null);
            var typQ = TypQuellen(def, t.Name);
            for (int i = 0; i < f.Masse.Count; i++)
            {
                var m = f.Masse[i];
                var z = new MassZeile { Mass = m.Name, Typ = t.Name, Ansicht = m.Ansicht };
                aus.Add(z);
                // Ist
                try
                {
                    var u = new WerteUmgebung(a);
                    if (m.Von is not null && m.Bis is not null)
                    {
                        var von = m.Von.Select(x => Z(x, u)).ToArray(); var bis = m.Bis.Select(x => Z(x, u)).ToArray();
                        z.Ist = m.Richtung switch
                        {
                            "x" => Math.Abs(bis[0] - von[0]), "y" => Math.Abs(bis[1] - von[1]), "z" => Math.Abs(bis[2] - von[2]),
                            _ when m.Ansicht is not null => Laenge(InAnsicht(m.Ansicht, von), InAnsicht(m.Ansicht, bis)),
                            _ => Math.Sqrt(Enumerable.Range(0, 3).Sum(k => (bis[k] - von[k]) * (bis[k] - von[k]))),
                        };
                    }
                    else if ((m.Parameter ?? Gleichnamig(f, m.Name)?.Name) is { } mpn)
                    {
                        var w = a.Wert(mpn);
                        if (w is { IstText: false } wz) z.Ist = wz.Zahl; else z.Hinweise.Add($"Parameter «{mpn}» hat im Typ keinen Zahlwert");
                    }
                    else z.Hinweise.Add("Massbezug ohne von/bis und ohne parameter – nichts zu messen");
                }
                catch (FormelFehler ff) { z.Hinweise.Add($"an der Familie nicht messbar: {ff.MitStelle}"); }

                // Soll
                var kandidaten = new List<(double Wert, int Vorrang, string Stelle, string Id, string? Zeile)>();
                bool nurBild = false;
                var mehrdeutig = new List<string>();
                var nameZeile = NameZeile(t.Name, typQ, i < quellen.Count ? quellen[i] : [], q);
                foreach (var id in i < quellen.Count ? quellen[i] : [])
                {
                    var b = q.Hole(id, out _);
                    if (b is null) continue;
                    var gehoert = SollAusBefund(m, t.Name, typQ, nameZeile, b, q, z.Ist, out var md, out var vermerk);
                    if (md is not null) mehrdeutig.Add(md);
                    if (vermerk is not null && !z.Hinweise.Contains(vermerk)) z.Hinweise.Add(vermerk);
                    if (gehoert is { } g)
                        kandidaten.Add((g.Wert, QuellVergleiche.Vorrang(g.Befund, q.ArtVon(g.Befund.Id), true), q.Stelle(g.Befund), g.Befund.Id,
                            g.Befund.Art == "zelle" ? $"{T(g.Befund.Zusatz?["tabelle"])}:{g.Befund.Zusatz?["zeile"]}" : null));
                    else if (b.Art is "bild" or "linie" or "bogen" or "kreis" or "polylinie" || (b.Art == "mass" && b.Wert is JsonValue mv && mv.GetValueKind() != System.Text.Json.JsonValueKind.Number && b.Zusatz?["zahl"] is null)) nurBild = true;
                }
                // Zellen aus verschiedenen Tabellenzeilen mit verschiedenen Werten: welche Zeile die des Typs ist, ist offen
                var zeilenWerte = kandidaten.Where(k => k.Zeile is not null).GroupBy(k => k.Zeile).Select(g => g.First().Wert).ToList();
                if (zeilenWerte.Count > 1 && zeilenWerte.Any(w => Math.Abs(w - zeilenWerte[0]) > Toleranz(m, f, zeilenWerte[0])))
                    mehrdeutig.Add($"Zellen aus {zeilenWerte.Count} Tabellenzeilen mit {string.Join(" / ", zeilenWerte.Select(QuellVergleiche.F))} mm");
                var mp = m.Parameter is not null ? f.NachName.GetValueOrDefault(m.Parameter) : Gleichnamig(f, m.Name);
                bool gleichnamig = m.Parameter is null && mp is not null;
                if (mehrdeutig.Count > 0)
                {
                    z.Mehrdeutig = true;
                    z.Hinweise.Add($"Mass {m.Name}: Tabellenzeile für Typ {t.Name} nicht eindeutig ({string.Join("; ", mehrdeutig.Distinct())}) – kein Soll; " +
                        "der Typ soll in quellen nur die Zellen seiner eigenen Tabellenzeile nennen");
                }
                else if (kandidaten.Count > 0)
                {
                    var best = kandidaten.OrderByDescending(k => k.Vorrang).First();
                    z.Soll = best.Wert; z.SollQuelle = best.Stelle; z.QuelleId = best.Id;
                    foreach (var k in kandidaten.Where(k => k != best)) z.Weitere.Add((k.Wert, k.Stelle));
                    if (kandidaten.Any(k => Math.Abs(k.Wert - best.Wert) > Toleranz(m, f, best.Wert)))
                        z.Hinweise.Add("Die Quellen dieses Masses widersprechen sich – Soll aus der Quelle mit dem höchsten Vorrang");
                }
                else if (mp is not null)
                {
                    // ohne Befund des Masses: der Parameter (angegeben oder gleichnamig, «ØD» → «D»), samt den Quellen seines Typwerts
                    var p = mp;
                    var ids = (def["typen"] as JsonArray ?? []).OfType<JsonObject>().Where(o => T(o["name"])?.Trim() == t.Name)
                        .SelectMany(o => (o["quellen"] as JsonObject)?[p.Name] as JsonArray ?? []).Select(T).Where(x => x is not null).Select(x => x!).ToList();
                    string Herkunft(string? stelle) => (gleichnamig ? $"Soll aus Parameter {p.Name} (gleicher Name)" : $"Typwert «{p.Name}»") + (stelle is null ? "" : $" – {stelle}");
                    if (m.Von is not null && m.Bis is not null && a.Wert(p.Name) is { IstText: false } tw)
                    {
                        z.Soll = tw.Zahl;
                        var erste = ids.Select(id => q.Hole(id, out _)).FirstOrDefault(b => b is not null);
                        z.SollQuelle = Herkunft(erste is null ? null : q.Stelle(erste));
                        z.QuelleId = erste?.Id;
                        z.Quellen.AddRange(ids);
                        if (nurBild) z.Hinweise.Add($"Quelle des Masses ist ein Bild bzw. eine Zeichnung ohne Zahl: Soll aus dem Typwert ({QuellVergleiche.AusBild})");
                    }
                    else if (a.Wert(p.Name) is { IstText: false } pw)
                    {
                        // nur parameter: Soll aus der Quelle des Typwerts
                        var beste = ids.Select(id => QuellVergleiche.Vergleichen(f, p, pw, id, q)).Where(r => r.Quellwert is not null)
                            .OrderByDescending(r => r.Vorrang).FirstOrDefault();
                        if (beste is not null) { z.Soll = beste.Quellwert; z.SollQuelle = Herkunft(beste.Stelle); z.QuelleId = beste.Id; z.Quellen.AddRange(ids); }
                        else z.Hinweise.Add(nurBild ? $"Quelle ist ein Bild bzw. eine Zeichnung ohne Zahl ({QuellVergleiche.AusBild})" : "ohne Sollwert (keine Quelle mit Zahl für diesen Typ)");
                    }
                    else z.Hinweise.Add($"ohne Sollwert (Parameter «{p.Name}» hat im Typ keinen Zahlwert)");
                }
                else z.Hinweise.Add(nurBild ? $"Quelle ist ein Bild bzw. eine Zeichnung ohne Zahl ({QuellVergleiche.AusBild})" : "ohne Sollwert (keine Quelle mit Zahl für diesen Typ)");

                if (z.Soll is double s && z.Ist is double ist)
                {
                    z.Toleranz = Toleranz(m, f, s);
                    z.Bestanden = Math.Abs(ist - s) <= z.Toleranz + 1e-9;
                }
            }
        }
        return aus;
    }

    static double Laenge(double[] a, double[] b) => Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));

    public static bool IstDurchmesser(FamMass m, FamilieDef f) =>
        m.Name.TrimStart().StartsWith('Ø') || m.Name.TrimStart().StartsWith('⌀') || m.Name.TrimStart().StartsWith('ø') || m.Name.TrimStart().StartsWith('Φ')
        || (m.Parameter is not null && f.NachName.TryGetValue(m.Parameter, out var p) && QuellVergleiche.IstDurchmesser(f, p));

    static double Toleranz(FamMass m, FamilieDef f, double soll) => IstDurchmesser(m, f) ? 1.0 : Math.Max(1.0, 0.005 * Math.Abs(soll));

    /// <summary>Zeile des Typs über seinen Namen (Ausnahme, Entscheid Hauptlauf 18.10.2026): nur wenn der Typ in den Unterlagen der
    /// Massquellen keine eigene Zelle nennt. Ergebnis: null = Rückfall gilt nicht; sonst die Zellen aller Tabellen dieser Unterlagen,
    /// deren Text genau der Typname ist (eindeutig nur bei genau einer).</summary>
    static List<(string Tab, int Zeile)>? NameZeile(string typ, HashSet<string> typQ, List<string> massQuellen, BefundQuelle q)
    {
        var praefixe = massQuellen.Select(BefundQuelle.Praefix).Where(x => x is not null).Select(x => x!).ToHashSet(StringComparer.Ordinal);
        if (praefixe.Count == 0) return null;
        if (typQ.Any(id => BefundQuelle.Praefix(id) is { } px && praefixe.Contains(px) && q.Hole(id, out _)?.Art == "zelle")) return null;   // eigene Zeile hat Vorrang
        var name = QuellZahlen.TextNormal(typ);
        return praefixe.SelectMany(q.Alle).Where(b => b.Art == "zelle" && QuellZahlen.TextNormal(T(b.Zusatz?["text"]) ?? T(b.Wert)) == name)
            .Select(b => (T(b.Zusatz!["tabelle"]) ?? "", (int)(Json.Zahl(b.Zusatz["zeile"]) ?? 0))).ToList();
    }

    /// <summary>Soll eines Befunds für einen Typ (in mm) oder null, wenn er nicht zum Typ gehört bzw. keine Länge trägt. Zum Typ gehören
    /// nur Befunde, die der Typ selbst in seinen quellen nennt, und Zellen derselben Tabellenzeile wie diese (Spalte wie der Befund
    /// des Masses). Nennt der Typ mehrere Zeilen derselben Tabelle, ist die Zeile nicht eindeutig (<paramref name="mehrdeutig"/>).
    /// Nennt der Typ keine eigene Zelle, gilt die Zeile, in der der Typname steht – nur wenn er in genau einer Zelle steht.</summary>
    static (double Wert, UnterlagenBefund Befund)? SollAusBefund(FamMass m, string typName, HashSet<string> typQ, List<(string Tab, int Zeile)>? nameZeile, UnterlagenBefund b,
        BefundQuelle q, double? ist, out string? mehrdeutig, out string? vermerk)
    {
        mehrdeutig = null; vermerk = null;
        UnterlagenBefund? zelle = null;
        (string Tab, int Spalte)? zelleIn = b.Art switch
        {
            "zelle" when T(b.Zusatz?["tabelle"]) is { } tab && Json.Zahl(b.Zusatz?["spalte"]) is double sp && sp > 0 => (tab, (int)sp),
            "tabelle" when b.Wert?["kopf"] is JsonArray kopf && Enumerable.Range(0, kopf.Count).FirstOrDefault(k => QuellVergleiche.NamePasst(T(kopf[k]), m.Name, m.Parameter), -1) is int sk && sk >= 0 => (b.Id, sk + 1),
            _ => null,
        };
        if (b.Art is "zelle" or "tabelle")
        {
            if (zelleIn is not { } zi) return null;
            var zeilen = ZeilenVonTyp(typQ, zi.Tab, q);
            if (zeilen.Count == 0)
            {
                // Rückfall über den Typnamen: nur eindeutig (genau eine Zelle in allen Tabellen der Unterlagen des Masses)
                if (nameZeile is null || nameZeile.Count == 0) return null;   // der Typ nennt keine Zelle dieser Tabelle: sie gilt nicht für ihn
                if (nameZeile.Count > 1) { mehrdeutig = $"Typname steht in {nameZeile.Count} Zellen"; return null; }
                if (nameZeile[0].Tab != zi.Tab) return null;
                zeilen = [nameZeile[0].Zeile];
                vermerk = $"Zeile über Typname {typName} (eindeutig)";
            }
            if (zeilen.Count > 1) { mehrdeutig = $"der Typ nennt die Zeilen {string.Join(", ", zeilen)} derselben Tabelle"; return null; }
            zelle = q.Zelle(zi.Tab, zeilen[0], zi.Spalte);
            if (zelle is null) return null;
        }
        else if (!typQ.Contains(b.Id)) return null;   // Typzeile, Bemassung, Masszahl …: nur, wenn der Typ sie selbst nennt
        var befund = zelle ?? b;
        var kand = QuellVergleiche.Kandidaten(befund, out var nicht, out var keine);
        if (nicht is not null || keine is not null) return null;
        var zahlen = kand.Where(k => k.Zahl is not null).ToList();
        var benannt = zahlen.Where(k => QuellVergleiche.NamePasst(k.Name, m.Name, m.Parameter)).ToList();
        if (benannt.Count > 0) zahlen = benannt;
        else if (befund.Art is "typ" or "pset" or "port") return null;
        var mm = new List<double>();
        foreach (var k in zahlen)
        {
            var zq = k.Zahl!;
            if (zq.Bruch && zq.Einheit is null) continue;   // Zoll-Bruch (Gewinde) ist keine Länge
            if (zq.Einheit is null) mm.Add(zq.Wert);
            else if (QuellEinheiten.Faktor(zq.Einheit, "mm") is double fk) mm.Add(zq.Wert * fk);
        }
        if (mm.Count == 0) return null;
        double wert = mm.Count == 1 || ist is null ? mm[0] : mm.OrderBy(x => Math.Abs(x - ist.Value)).First();
        return (wert, befund);
    }

    /// <summary>Abfrage familie_masse {familie, typ?}.</summary>
    public static JsonObject Abfrage(Projekt p, string familie, string? typ)
    {
        var fam = Familien.Hole(p.Modell, familie);
        var f = Familien.Definition(fam, p.Katalog);
        if (!f.Gueltig) throw FamilienPruefung.Fehlermeldung($"Familie {fam.Kennzeichen} «{fam.Name}»", f.Fehler);
        if (typ is not null && f.Typ(typ) is null)
            throw new HlksFehler($"Typ «{typ}» gibt es in der Familie {fam.Kennzeichen} nicht.", "Typen: " + string.Join(", ", f.Typen.Select(t => t.Name)));
        var def = (JsonObject)fam.Eigenschaft("definition")!.Inhalt!;
        var zeilen = Vergleichen(f, def, new BefundQuelle(p), typ);
        var o = new JsonObject
        {
            ["familie"] = fam.Kennzeichen, ["name"] = fam.Name, ["masse"] = f.Masse.Count,
            ["bestanden"] = zeilen.Count(z => z.Bestanden == true), ["ausserhalb"] = zeilen.Count(z => z.Bestanden == false), ["ohneVergleich"] = zeilen.Count(z => z.Bestanden is null),
            ["typen"] = new JsonArray(zeilen.GroupBy(z => z.Typ).Select(g => (JsonNode)new JsonObject
            {
                ["typ"] = g.Key, ["bestanden"] = g.All(z => z.Bestanden != false),
                ["abweichungen"] = new JsonArray(g.Select(z => (JsonNode)z.AlsJson()).ToArray()),
            }).ToArray()),
        };
        if (f.Masse.Count == 0) o["hinweis"] = "Die Familie hat keine Massbezüge (masse): je Massbuchstabe der Unterlage einen Eintrag mit von/bis bzw. parameter und quellen ergänzen.";
        return o;
    }
}
