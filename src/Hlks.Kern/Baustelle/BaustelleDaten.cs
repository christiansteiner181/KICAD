// BIM to field Stufe 0 (entwurf/bim-to-field.md, Bestandsaufnahme 02.11.2026): Angaben für Montage und Betrieb aus dem Modell – Montageabschnitte
// (gespeicherte Auswahl), Montagestatus, Artikel (Element vor Familie), Höhenkote je Element. Eine Stelle für Export, Abfragen und später die Plattform.
//
//   Abschnitt   Kriterien { systeme, straenge, klassen, geschosse, raeume, gewerke } – gesetzte Kriterien zusammen (und), Werte darin wahlweise (oder);
//               Klassen schliessen ihre Unterklassen ein (Formteil = Bogen, T-Stück …); Strang = teilVon. Dazu von Hand gewählte Elemente (elemente).
//               Nur Bauteile im Netz (Schicht system mit Rolle), Ausnahme: von Hand gewählte Elemente.
//   Höhe        Punkte der Geometrie (mm über ±0.00); Leitungen: Achse, dazu Unter- und Oberkante mit der halben Höhe des Querschnitts (ohne
//               Dämmung; Serie bzw. Luftleitung HlksMasse.Von, sonst da bzw. durchmesser am Element, nie DN); relativ zum Geschoss über dessen Kote (OK fertig Boden).
//   Artikel     Hersteller und Artikelnummer am Element gehen vor; sonst Kopf der Familiendefinition (hersteller, produkt) und Typparameter
//               «artikelnummer» des Familientyps bzw. der Exemplarwert.

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class BaustelleDaten
{
    public static readonly IReadOnlyList<string> Status = ["geplant", "freigegeben", "montiert", "geprueft", "abgenommen"];

    public static bool IstBauteil(Katalog k, Element e) => k.Klassen.TryGetValue(e.Klasse, out var d) && d.Schicht == "system" && d.Rolle is not null;

    public static string Montagestatus(Element e) => e.Text("montagestatus") ?? "geplant";

    static List<string> Liste(JsonObject? o, string name) =>
        o?[name] is JsonArray a ? a.Select(x => x?.GetValue<string>()).OfType<string>().ToList() : [];

    /// <summary>Elemente eines Montageabschnitts (nach Kennzeichen geordnet).</summary>
    public static List<Element> Abschnitt(Modell m, Katalog k, Element abschnitt)
    {
        var kr = abschnitt.Eigenschaft("kriterien")?.Inhalt as JsonObject;
        string? Kz(string? id) => id is null ? null : m.Finde(id)?.Kennzeichen;
        var systeme = Liste(kr, "systeme"); var straenge = Liste(kr, "straenge"); var klassen = Liste(kr, "klassen");
        var geschosse = Liste(kr, "geschosse"); var raeume = Liste(kr, "raeume"); var gewerke = Liste(kr, "gewerke");
        bool In(List<string> l, string? w) => l.Count == 0 || w is not null && l.Contains(w, StringComparer.OrdinalIgnoreCase);
        var mitKriterien = new[] { systeme, straenge, klassen, geschosse, raeume, gewerke }.Any(l => l.Count > 0);
        var vonHand = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (abschnitt.Eigenschaft("elemente")?.Inhalt is JsonArray hand)
            foreach (var x in hand) if (x?.GetValue<string>() is { } s) vonHand.Add(s);
        return m.Elemente.Where(e =>
                vonHand.Contains(e.Kennzeichen)
                || mitKriterien && IstBauteil(k, e)
                   && In(systeme, Kz(e.System)) && In(straenge, Kz(e.TeilVon)) && In(geschosse, Kz(e.Geschoss)) && In(raeume, Kz(e.Raum))
                   && (klassen.Count == 0 || klassen.Any(c => k.IstArt(e, c)))
                   // Gewerk der Klasse und des Systems bzw. Mediums: Leitungsklassen haben kein eigenes Gewerk (Ableitungen.Gewerke)
                   && (gewerke.Count == 0 || Ableitungen.Gewerke(m, k, e).Any(g => gewerke.Contains(g, StringComparer.OrdinalIgnoreCase))))
            .OrderBy(e => e.Kennzeichen, StringComparer.Ordinal).ToList();
    }

    /// <summary>Hersteller, Produkt, Typ und Artikelnummer mit Herkunft (element | familie); null, wenn nichts bekannt ist.</summary>
    public static JsonObject? Artikel(Projekt p, Element e)
    {
        var m = p.Modell; var k = p.Katalog;
        var fam = Familien.FamilieVon(m, e);
        var def = fam?.Eigenschaft("definition")?.Inhalt as JsonObject;
        string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
        var typ = Familien.Typ(e);
        string? artikelFamilie = null;
        if (fam is not null)
            artikelFamilie = Text(Familien.Exemplarwerte(e)?["artikelnummer"])
                ?? (Familien.Definition(fam, k).Typ(typ)?.Werte.TryGetValue("artikelnummer", out var a) == true ? Text(a) : null);
        var o = new JsonObject();
        void Setze(string name, string? element, string? familie)
        {
            if (element is not null) o[name] = new JsonObject { ["wert"] = element, ["herkunft"] = "element" };
            else if (familie is not null) o[name] = new JsonObject { ["wert"] = familie, ["herkunft"] = "familie" };
        }
        Setze("hersteller", e.Text("hersteller"), Text(def?["hersteller"]));
        Setze("produkt", null, Text(def?["produkt"]));
        Setze("typ", e.Klasse == "Apparat" ? e.Text("typ") : null, typ);
        Setze("artikelnummer", e.Text("artikelnummer"), artikelFamilie);
        if (fam is not null) o["familie"] = fam.Name ?? fam.Kennzeichen;
        return o.Count == 0 ? null : o;
    }

    /// <summary>Höhenkote in mm: achseMin/achseMax (Punkte), bei Leitungen ukMm/okMm, dazu geschossKoteMm. null ohne Punkte mit z.</summary>
    public static JsonObject? Hoehe(Projekt p, Element e)
    {
        if (e.Geometrie?.Punkte is not { Count: > 0 } pk || pk.Any(q => q.Length < 3)) return null;
        double zMin = pk.Min(q => q[2]), zMax = pk.Max(q => q[2]);
        var o = new JsonObject { ["achseMinMm"] = Math.Round(zMin), ["achseMaxMm"] = Math.Round(zMax) };
        // Querschnittshöhe: Serie bzw. Luftleitung (HlksMasse), sonst der Aussendurchmesser am Element (da, durchmesser) – nie die Nennweite
        if (p.Katalog.IstArt(e, "Leitung") && (HlksMasse.Von(p, e)?.Hoehe ?? e.Zahl("da") ?? e.Zahl("durchmesser")) is double hq && hq > 0)
        {
            o["ukMm"] = Math.Round(zMin - hq / 2);
            o["okMm"] = Math.Round(zMax + hq / 2);
        }
        if (p.Modell.Finde(e.Geschoss)?.Zahl("kote") is double kote) o["geschossKoteMm"] = kote;
        return o;
    }

    // ------------------------------------------------------------------ Abfrage und Befehl

    public const string AbschnitteText = "Montageabschnitte (BIM to field): je Abschnitt Kennzeichen, Name, Reihenfolge, Termin, Kolonne, Anzahl Elemente und " +
        "Anzahl je Montagestatus; mit abschnitt zusätzlich die Elemente (Kennzeichen, Klasse, Status). Nur lesend.";
    public const string StatusText = "Montagestatus setzen (BIM to field): status geplant | freigegeben | montiert | geprueft | abgenommen für die " +
        "Elemente (Kennzeichen) und bzw. oder alle Bauteile eines Montageabschnitts. Datum und Person stehen in der Herkunft. Ein Paket, zurücknehmbar.";

    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
        [new("montageabschnitte", AbschnitteText, [new("abschnitt", "text", false, "Kennzeichen eines Montageabschnitts (MA-n) für die Elementliste")])];

    public static object? Tuer(Projekt p, string name, JsonObject par) => name == "montageabschnitte" ? Abschnitte(p, Param.Text(par, "abschnitt")) : null;

    public static void Registrieren(Befehlsregister r) => r.Hinzufuegen(new MontagestatusSetzen());

    public static JsonObject Abschnitte(Projekt p, string? nur)
    {
        var m = p.Modell; var k = p.Katalog;
        var liste = m.DerKlasse("Montageabschnitt")
            .Where(a => nur is null || a.Kennzeichen.Equals(nur, StringComparison.OrdinalIgnoreCase) || a.Id == nur)
            .OrderBy(a => a.Zahl("reihenfolge") ?? double.MaxValue).ThenBy(a => a.Kennzeichen, StringComparer.Ordinal).ToList();
        if (nur is not null && liste.Count == 0) throw new HlksFehler($"Montageabschnitt «{nur}» gibt es nicht.", "Kennzeichen aus montageabschnitte ohne Parameter.");
        return new JsonObject
        {
            ["abschnitte"] = new JsonArray(liste.Select(a =>
            {
                var el = Abschnitt(m, k, a);
                var o = new JsonObject
                {
                    ["kennzeichen"] = a.Kennzeichen, ["name"] = a.Name, ["reihenfolge"] = a.Zahl("reihenfolge"), ["termin"] = a.Text("termin"),
                    ["terminBis"] = a.Text("terminBis"), ["kolonne"] = a.Text("kolonne"), ["elemente"] = el.Count,
                    ["status"] = new JsonObject(Status.Select(s => KeyValuePair.Create(s, (JsonNode?)el.Count(e => Montagestatus(e) == s)))),
                };
                if (nur is not null)
                    o["liste"] = new JsonArray(el.Select(e => (JsonNode)new JsonObject { ["kennzeichen"] = e.Kennzeichen, ["klasse"] = e.Klasse, ["status"] = Montagestatus(e) }).ToArray());
                return (JsonNode)o;
            }).ToArray()),
        };
    }
}

public sealed class MontagestatusSetzen : IBefehl
{
    public string Name => "montagestatus_setzen";
    public string Beschreibung => BaustelleDaten.StatusText;
    public IReadOnlyList<ParameterDef> Parameter { get; } =
    [
        new("status", "text", true, "geplant | freigegeben | montiert | geprueft | abgenommen"),
        new("elemente", "liste", false, "Kennzeichen der Elemente"),
        new("abschnitt", "text", false, "Montageabschnitt (MA-n): alle seine Elemente"),
    ];

    public void Ausfuehren(Kontext k, JsonObject p)
    {
        var status = Param.Text(p, "status", true)!.Trim().ToLowerInvariant();
        if (!BaustelleDaten.Status.Contains(status)) throw new HlksFehler($"Montagestatus «{status}» gibt es nicht.", "status: " + string.Join(" | ", BaustelleDaten.Status));
        var m = k.Arbeit;
        var ziel = new List<Element>();
        if (p["elemente"] is JsonArray l)
            foreach (var x in l) ziel.Add(m.Hole(x?.GetValue<string>()));
        if (Param.Text(p, "abschnitt") is { } ab) ziel.AddRange(BaustelleDaten.Abschnitt(m, k.Katalog, m.HoleDerKlasse(ab, "Montageabschnitt")));
        if (ziel.Count == 0) throw new HlksFehler("Keine Elemente angegeben.", "elemente (Kennzeichen) oder abschnitt angeben.");
        var ohne = ziel.Where(e => !BaustelleDaten.IstBauteil(k.Katalog, e)).Select(e => e.Kennzeichen).Distinct().ToList();
        if (ohne.Count > 0) throw new HlksFehler($"Kein Bauteil im Netz: {string.Join(", ", ohne.Take(10))}.", "Montagestatus gilt für Leitungen, Formteile, Armaturen, Apparate und Geräte.");
        var n = 0;
        foreach (var e in ziel.DistinctBy(e => e.Id))
        {
            if (BaustelleDaten.Montagestatus(e) == status && e.Eigenschaften.ContainsKey("montagestatus")) continue;
            var neu = e.Kopie();
            neu.Eigenschaften["montagestatus"] = new Wert { Inhalt = status, Herkunft = k.Herkunft };
            k.Setzen(neu);
            n++;
        }
        k.Hinweise.Add($"Montagestatus «{status}» für {n} Element(e) gesetzt" + (n < ziel.Count ? $", {ziel.Count - n} hatten ihn schon." : "."));
    }
}
