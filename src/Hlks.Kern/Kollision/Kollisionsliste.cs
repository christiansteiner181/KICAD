// Kollisionsprüfung (Modul 3b, B2): Befehl kollision_status und Abfrage kollisionen (Kollisionsliste, auch Ansicht liste =
// kollisionen). Die Liste hat das Format der Ableitungen (Kopf mit Modellstand, «zeilen» mit dem Kennzeichen als Schlüssel für den
// Sprung in die Ansichten, Zusammenfassung) – wie raumgrenzen/huellbauteile in einer eigenen Datei, Api/Ableitungen.cs bleibt
// unverändert (nur Ableitungen.Kopf wird benutzt).
// Beteiligte: Elemente mit Kennzeichen und Klasse; Produkte einer IFC-Unterlage («Unterlage-Id:GlobalId») mit Entität aus der
// Netzdatei der Unterlage (Kopfdaten, zwischengespeichert je Datei und Stand).
// Nachtrag B3c: je Zeile das Paar (hlks-architektur, hlks-bekleidung, hlks-hlks, hlks-fremd – wie der Prüflauf es einteilt; Bekleidung =
// IfcCovering) und bei Durchdringungen Material und rohbau des Bauteils; Filter paar, Zahlen jePaar. Ältere Kollisionen mit Bekleidungen
// bleiben in der Liste (Status unverändert) und lassen sich mit paar ausblenden.
// Nachtrag B3d: Paar hlks-vorwand (Installationsvorwand nach Typenliste bzw. Typname) und hlks-bekleidung auch für Typen der Typenliste mit
// Kategorie bekleidung – dieselbe Regel wie der Prüflauf (Kollisionspaar.Architektur, Material aus AussparungMaterial.cs mit der
// Typenliste); das Material einer Durchdringung zeigt seine Herkunft im Text («Typenliste «…»», «aus Typname «…»»).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Hlks.Kern;

/// <summary>Befehl kollision_status.</summary>
public sealed class KollisionStatusSetzen : IBefehl
{
    public string Name => "kollision_status";
    public string Beschreibung => KollisionTexte.Status;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("kollisionen", "liste", true, KollisionTexte.ParKollisionen),
        new("status", "text", true, KollisionTexte.ParStatus),
        new("kommentar", "text", false, KollisionTexte.ParKommentar),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var namen = Kollisionsauftrag.Liste(p, "kollisionen") ?? throw new HlksFehler("Parameter «kollisionen» fehlt.", "kollisionen: [\"KOL-1\", …]");
        if (namen.Count == 0) throw new HlksFehler("Parameter «kollisionen» ist leer.", "kollisionen: [\"KOL-1\", …] (Abfrage kollisionen listet sie)");
        var erlaubt = k.Katalog.Klasse("Kollision").Eigenschaften.First(x => x.Name == "status").Auswahl ?? [];
        var status = (Param.Text(p, "status", true) ?? "").Trim().ToLowerInvariant();
        if (!erlaubt.Contains(status)) throw new HlksFehler($"Status «{status}» gibt es für Kollisionen nicht.", "status: " + string.Join(" | ", erlaubt));
        bool mitKommentar = p.ContainsKey("kommentar");
        var kommentar = Param.Text(p, "kommentar")?.Trim() ?? "";
        var geaendert = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in namen)
        {
            var alt = k.Arbeit.Hole(n, "Kollision");
            if (!k.Katalog.IstArt(alt, "Kollision"))
                throw new HlksFehler($"{alt.Kennzeichen} ist keine Kollision, sondern {alt.Klasse}.", "In kollisionen nur Kollisionen angeben (Abfrage kollisionen listet sie).");
            if (!gesehen.Add(alt.Id)) continue;
            var e = alt.Kopie();
            var was = new List<string>();
            var vorher = alt.Text("status") ?? "neu";
            if (alt.Text("status") != status)
            {
                e.Eigenschaften["status"] = new Wert { Inhalt = JsonValue.Create(status), Herkunft = k.Herkunft };
                was.Add($"Status {vorher} → {status}");
            }
            if (mitKommentar)
            {
                // eigener Text ersetzt, übernommene BCF-Kommentare bleiben (Format BcfKommentare)
                var zer = BcfKommentare.Zerlegen(alt.Text("kommentar"));
                var text = zer.Uebernommen.Count == 0 ? kommentar : BcfKommentare.Anhaengen(kommentar, zer.Uebernommen, k.Herkunft.Zeit);
                if (text != (alt.Text("kommentar") ?? ""))
                {
                    if (text.Length == 0) e.Eigenschaften.Remove("kommentar");
                    else e.Eigenschaften["kommentar"] = new Wert { Inhalt = JsonValue.Create(text), Herkunft = k.Herkunft };
                    was.Add(kommentar.Length == 0 ? "Kommentar entfernt" : "Kommentar gesetzt");
                }
            }
            if (was.Count == 0) continue;
            k.Setzen(e);
            geaendert.Add($"{alt.Kennzeichen}: {string.Join(", ", was)}");
        }
        if (geaendert.Count == 0) { k.Hinweise.Add("Keine Änderung."); return; }
        k.Hinweise.AddRange(geaendert);
        if (status == "akzeptiert") k.Hinweise.Add("akzeptiert bleibt auch, wenn ein Prüflauf die Kollision nicht mehr findet.");
    }
}

/// <summary>Abfrage kollisionen und Abfrage-Definitionen von Teil B2.</summary>
public static class Kollisionsliste
{
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("kollisionen", KollisionTexte.Liste,
        [
            new("status", "text", false, KollisionTexte.ParListeStatus),
            new("art", "text", false, KollisionTexte.ParListeArt),
            new("typ", "text", false, KollisionTexte.ParListeTyp),
            new("geschoss", "text", false, KollisionTexte.ParListeGeschoss),
            new("element", "text", false, KollisionTexte.ParListeElement),
            new("paar", "text", false, KollisionTexte.ParListePaar),
        ]),
        new("vorgaben", VorgabenTexte.Abfrage, []),
    ];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name switch
    {
        "kollisionen" => Lesen(p, Param.Text(par, "status"), Param.Text(par, "art"), Param.Text(par, "typ"), Param.Text(par, "geschoss"), Param.Text(par, "element"), Param.Text(par, "paar")),
        "vorgaben" => VorgabenAbfrage.Lesen(p),
        _ => null,
    };

    static List<string>? Auswahl(Katalog k, string eigenschaft, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var erlaubt = k.Klasse("Kollision").Eigenschaften.First(x => x.Name == eigenschaft).Auswahl ?? [];
        var l = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).ToList();
        foreach (var x in l.Where(x => !erlaubt.Contains(x)))
            throw new HlksFehler($"{eigenschaft} «{x}» gibt es für Kollisionen nicht.", $"{eigenschaft}: {string.Join(" | ", erlaubt)}");
        return l;
    }

    public static JsonObject Lesen(Projekt p, string? status = null, string? art = null, string? typ = null, string? geschoss = null, string? element = null, string? paar = null)
    {
        var m = p.Modell; var k = p.Katalog;
        List<string>? fp = null;
        if (!string.IsNullOrWhiteSpace(paar))
        {
            fp = paar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).ToList();
            foreach (var x in fp.Where(x => !KollisionTexte.Paare.Contains(x)))
                throw new HlksFehler($"Paar «{x}» gibt es nicht.", "paar: " + string.Join(" | ", KollisionTexte.Paare));
        }
        var fs = Auswahl(k, "status", status);
        var fa = Auswahl(k, "art", art);
        var ft = Auswahl(k, "typ", typ);
        var fg = string.IsNullOrWhiteSpace(geschoss) ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        string? elementId = null, elementText = null;
        if (!string.IsNullOrWhiteSpace(element))
        {
            elementText = element.Trim();
            elementId = m.Finde(elementText)?.Id;
        }
        var entitaeten = new Entitaeten(p);
        var typen = Typenliste.Von(m);   // Nachtrag B3d: Paar hlks-vorwand bzw. hlks-bekleidung nach der Typenliste
        var alle = BcfFormat.Kollisionen(m, k);
        var zeilen = new JsonArray();
        var treffer = new List<Element>();
        var paare = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in alle)
        {
            var st = e.Text("status") ?? "neu";
            var ar = e.Text("art") ?? "hart";
            var ty = e.Text("typ") ?? "kontakt";
            if (fs is not null && !fs.Contains(st)) continue;
            if (fa is not null && !fa.Contains(ar)) continue;
            if (ft is not null && !ft.Contains(ty)) continue;
            if (fg is not null && e.Geschoss != fg) continue;
            var bet = KollisionPruefen.Beteiligte(e);
            if (elementText is not null)
            {
                bool Passt(string b) => b == elementId || b == elementText || b.EndsWith(":" + elementText, StringComparison.Ordinal);
                if (!bet.Any(Passt) && !(e.Text("bauteil") is { } bt && Passt(bt))) continue;
            }
            var pa = Paar(m, k, entitaeten, typen, bet);
            if (fp is not null && !fp.Contains(pa)) continue;
            treffer.Add(e);
            paare[e.Id] = pa;
            var details = bet.Select(b => Beteiligter(m, k, entitaeten, b)).ToList();
            var lage = KollisionPruefen.Lage(e);
            zeilen.Add(new JsonObject
            {
                ["kollision"] = e.Kennzeichen,
                ["art"] = ar,
                ["typ"] = ty,
                ["paar"] = pa,
                ["status"] = st,
                ["mass_mm"] = e.Zahl("mass"),
                ["geschoss"] = m.Finde(e.Geschoss)?.Kennzeichen,
                ["beteiligte"] = string.Join(" ↔ ", details.Select(d => d["text"]!.GetValue<string>())),
                ["bauteil"] = e.Text("bauteil") is { } b2 ? Beteiligter(m, k, entitaeten, b2)["text"]!.GetValue<string>() : null,
                ["lage_mm"] = lage is V3 l ? new JsonArray(JsonValue.Create(Math.Round(l.X, 1)), JsonValue.Create(Math.Round(l.Y, 1)), JsonValue.Create(Math.Round(l.Z, 1))) : null,
                ["material"] = e.Text("material"),
                ["rohbau"] = e.Text("rohbau"),
                ["kommentar"] = e.Text("kommentar"),
                ["pruefung"] = e.Text("pruefung"),
                ["kennung"] = e.Text("kennung"),
                ["beteiligteListe"] = new JsonArray(details.Select(d => (JsonNode)d).ToArray()),
            });
        }
        static JsonObject Je(IEnumerable<string> werte) =>
            new(werte.GroupBy(x => x).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count())));
        var o = Ableitungen.Kopf(p, "Kollisionsliste");
        o["filter"] = new JsonObject { ["status"] = status, ["art"] = art, ["typ"] = typ, ["geschoss"] = geschoss, ["element"] = element, ["paar"] = paar };
        o["anzahl"] = treffer.Count;
        // letzter Prüflauf (B2b): je Kollision steht in pruefung der Lauf, der sie zuerst gefunden hat
        o["kollisionslauf"] = HuelleHilfe.Projektinfo(m)?.Text("kollisionslauf");
        o["zusammenfassung"] = new JsonObject
        {
            ["jeStatus"] = Je(treffer.Select(e => e.Text("status") ?? "neu")),
            ["jeArt"] = Je(treffer.Select(e => e.Text("art") ?? "hart")),
            ["jeTyp"] = Je(treffer.Select(e => e.Text("typ") ?? "kontakt")),
            ["jeArtTyp"] = Je(treffer.Select(e => $"{e.Text("art") ?? "hart"} {e.Text("typ") ?? "kontakt"}")),
            ["jePaar"] = Je(treffer.Select(e => paare[e.Id])),
            ["jeGeschoss"] = Je(treffer.Select(e => m.Finde(e.Geschoss)?.Kennzeichen ?? "ohne Geschoss")),
        };
        o["zeilen"] = zeilen;
        if (alle.Count == 0) o["hinweis"] = "Noch keine Kollisionen – Prüflauf mit kollision_pruefen (zuerst als Trockenlauf).";
        return o;
    }

    /// <summary>Paar einer Kollision aus ihren Beteiligten (wie der Prüflauf): zwei HLKS-Elemente → hlks-hlks; sonst nach dem anderen
    /// Beteiligten: Bekleidung (IfcCovering bzw. Bekleidungsklasse, Typenliste bekleidung) → hlks-bekleidung, Installationsvorwand
    /// (Typenliste bzw. Typname, Nachtrag B3d) → hlks-vorwand, Architektur/Tragwerk → hlks-architektur, übrige Produkte einer IFC-Unterlage
    /// → hlks-fremd. Unbekannt (nicht im Modell): hlks-architektur.</summary>
    static string Paar(Modell m, Katalog k, Entitaeten ent, Typenliste typen, List<string> bet)
    {
        int hlks = 0; string? anderes = null;
        foreach (var b in bet)
        {
            if (m.Finde(b) is { } e)
            {
                var bereich = Pruefkoerperbau.Bereich(k, e);
                if (bereich == "hlks") { hlks++; continue; }
                if (anderes is not null) continue;
                if (Pruefkoerperbau.IstBekleidung(k, e)) { anderes = KollisionTexte.HlksBekleidung; continue; }
                var (pr, fassung) = e.Kennungen is { } kn && kn.TryGetValue("quelle", out var q) && kn.TryGetValue("ifc", out var g) && m.Finde(q) is { } eul
                    ? ent.Produkt(eul, Netzkoepfe.Basis(g)) : (null, null);
                anderes = Kollisionspaar.Architektur(false, Bauteilmaterial.Von(m, k, Bauteilangaben.Von(k, pr, fassung, e), typen));
                continue;
            }
            var i = b.IndexOf(':');
            if (i <= 0 || m.Finde(b[..i]) is not { } ul) continue;
            var (p2, f2) = ent.Produkt(ul, b[(i + 1)..]);
            var en = p2?.Entitaet;
            if (Pruefkoerperbau.IstBekleidung(en)) anderes ??= KollisionTexte.HlksBekleidung;
            else if (Pruefkoerperbau.BereichVonEntitaet(en) is var be && be == "hlks") hlks++;   // Leitung einer Unterlage (selten)
            else anderes ??= be == "architektur" ? Kollisionspaar.Architektur(false, Bauteilmaterial.Von(m, k, Bauteilangaben.Von(k, p2, f2), typen))
                : KollisionTexte.HlksFremd;
        }
        return hlks >= 2 ? KollisionTexte.HlksHlks : anderes ?? KollisionTexte.HlksArchitektur;
    }

    static JsonObject Beteiligter(Modell m, Katalog k, Entitaeten ent, string b)
    {
        if (m.Finde(b) is { } e)
            return new JsonObject
            {
                ["kennung"] = b, ["kennzeichen"] = e.Kennzeichen, ["klasse"] = e.Klasse, ["globalId"] = BcfFormat.GlobalIdBasis(e.Kennungen?.GetValueOrDefault("ifc")),
                ["text"] = $"{e.Kennzeichen} ({e.Klasse})",
            };
        var i = b.IndexOf(':');
        if (i > 0 && i < b.Length - 1)
        {
            var ul = m.Finde(b[..i]);
            var gid = b[(i + 1)..];
            var en = ul is null ? null : ent.Von(ul, gid);
            return new JsonObject
            {
                ["kennung"] = b, ["entitaet"] = en, ["globalId"] = gid, ["unterlage"] = ul?.Kennzeichen ?? b[..i],
                ["text"] = $"{en ?? "IFC-Produkt"} {gid} ({ul?.Kennzeichen ?? b[..i]})",
            };
        }
        return new JsonObject { ["kennung"] = b, ["text"] = $"{b} (nicht im Modell)" };
    }

    /// <summary>Produkte einer IFC-Unterlage aus den Kopfdaten ihrer Netzdatei (Netzkoepfe, je Datei und Stand zwischengespeichert): Entität
    /// und – seit Nachtrag B3d – Material und Typname für das Paar.</summary>
    sealed class Entitaeten(Projekt p)
    {
        readonly Dictionary<string, IfcNetzdatei?> jeUnterlage = new(StringComparer.Ordinal);

        public string? Von(Element unterlage, string gid) => Produkt(unterlage, gid).Produkt?.Entitaet;

        /// <summary>Produkt (Bauteile, Teile und Öffnungen) und Fassung der Netzdatei, sonst (null, null).</summary>
        public (IfcNetzProdukt? Produkt, int? Fassung) Produkt(Element unterlage, string gid)
        {
            if (!jeUnterlage.TryGetValue(unterlage.Id, out var nd)) jeUnterlage[unterlage.Id] = nd = Netzkoepfe.Von(p, unterlage);
            if (nd is null) return (null, null);
            return (nd.Produkt(gid) ?? nd.Oeffnungen.FirstOrDefault(o => o.GlobalId == gid), nd.Fassung);
        }
    }
}
