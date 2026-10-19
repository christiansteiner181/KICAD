// Abfragen raumgrenzen und huellbauteile (Modul 1c, Auftrag B1): Ableitungen wie die Raumliste, aus dem Rechenkern
// Raumgrenzen.cs. Flächen in m², Azimut/Neigung in Grad, U in W/(m²·K), U·A in W/K. Keine Normfaktoren.
//
// Hüllbauteile (Spezifikation «Masse»): je Bauteil mit mindestens einer Raumgrenze eines beheizten Raums gegen aussen,
// Erdreich oder unbeheizt (Annahme: Raumgrenzen unbeheizter Räume gehören nicht zur Hülle des beheizten Volumens).
// Fläche innen = Summe dieser Raumgrenzen. Aussenmass-Fläche: Wand = Länge an der Aussenseite samt Verlängerung an den
// Ecken × Wandhöhe − Öffnungen (lichte Masse, auf die Wand begrenzt), für die ganze Wand (Hinweis, wenn sie nur zum Teil
// Hülle ist); Fenster/Tür = Breite × Höhe; Decke/Dach = Polygonfläche. Welcher Massbezug für welches Normverfahren gilt,
// legt Christian fest (entwurf/modul-1c.md, Prüfliste 1).

using System.Text.Json.Nodes;

namespace Hlks.Kern;

public static class RaumgrenzenAbfragen
{
    public const string BeschreibungRaumgrenzen = "Raumgrenzen (Ableitung): je Raum die Flächen gegen Nachbarraum (beheizt/unbeheizt), aussen und Erdreich – Wand, Fenster, Tür, Boden, Decke, Dach mit Bauteil, Aufbau, Fläche (Innenmass, m²), Azimut, Neigung, U und U·A, falls U bekannt; Summen je Raum und Angrenzung. Ohne Normfaktoren.";
    public const string BeschreibungHuellbauteile = "Hüllbauteile (Ableitung): je Bauteil mit Raumgrenzen eines beheizten Raums gegen aussen, Erdreich oder unbeheizt – Aufbau, U, Fläche innen (Summe der Raumgrenzen) und Aussenmass-Fläche, Orientierung, Angrenzung.";

    /// <summary>Einträge für Abfragen.Liste (die Tür ruft Raumgrenzen bzw. Huellbauteile auf).</summary>
    public static readonly IReadOnlyList<AbfrageDef> Definitionen =
    [
        new("raumgrenzen", BeschreibungRaumgrenzen,
            [new("raum", "text", false, "Nur dieser Raum (Kennzeichen oder Id)"), new("geschoss", "text", false, "Nur Räume dieses Geschosses"),
             new("geometrie", "bool", false, GeometrieBeschreibung)]),
        new("huellbauteile", BeschreibungHuellbauteile, [new("geschoss", "text", false, "Nur Bauteile dieses Geschosses")]),
    ];

    static double R(double x, int stellen = 2) => Math.Round(x, stellen, MidpointRounding.AwayFromZero);
    static JsonNode? R(double? x, int stellen = 2) => x is double d ? JsonValue.Create(R(d, stellen)) : null;

    static List<Element> RaeumeAuswahl(Modell m, string? raum, string? geschoss)
    {
        var r = raum is null ? null : m.HoleDerKlasse(raum, "Raum");
        var g = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        if (r is not null)
        {
            if (g is not null && r.Geschoss != g)
                throw new HlksFehler($"Raum {r.Kennzeichen} liegt nicht im Geschoss {m.Finde(g)!.Kennzeichen}.", "raum oder geschoss weglassen.");
            return [r];
        }
        return m.DerKlasse("Raum").Where(x => g is null || x.Geschoss == g).ToList();
    }

    static string Kz(Element? e) => e?.Kennzeichen ?? "";

    static JsonObject Zeile(Raumgrenze g, bool geometrie)
    {
        var o = new JsonObject
        {
            ["raum"] = g.Raum.Kennzeichen,
            ["nachbar"] = g.NachbarRaum?.Kennzeichen ?? g.Nachbar,
            ["angrenzung"] = g.Angrenzung,
            ["art"] = g.Art,
            ["bauteil"] = g.Bauteil?.Kennzeichen,
            ["aufbau"] = g.Aufbau?.Kennzeichen,
            ["flaeche_m2"] = R(g.Flaeche),
            ["azimut"] = R(g.Azimut, 1),
            ["neigung"] = g.Neigung,
            ["u"] = g.U,
            ["ua_WK"] = R(g.UA),
        };
        if (g.Art is "wand" or "fenster" or "tuer")
        {
            o["laenge_m"] = R(g.Laenge / 1000, 3);
            o["hoehe_m"] = R((g.Oben - g.Unten) / 1000, 3);
        }
        if (g.Offen) o["offen"] = true;
        if (g.Gerechnet is not null) { o["vorgabe"] = true; o["gerechnet"] = g.Gerechnet; }
        if (g.UQuelle is not null) o["uQuelle"] = g.UQuelle;
        if (geometrie)
        {
            static JsonArray P(double[] q) => new(R(q[0], 1), R(q[1], 1));
            if (g.Von is not null && g.Bis is not null)
            {
                o["punkte"] = new JsonArray(P(g.Von), P(g.Bis));
                o["unten"] = R(g.Unten, 1);
                o["oben"] = R(g.Oben, 1);
            }
            else if (g.Ring is not null)
            {
                o["polygon"] = new JsonArray(g.Ring.Select(q => (JsonNode)P(q)).ToArray());
                o["z"] = R(g.Unten, 1);
            }
        }
        return o;
    }

    public const string GeometrieBeschreibung = "true = je Raumgrenze die Geometrie in mm: Seiten punkte [[x,y],[x,y]] auf der Raumkante mit unten/oben (z), Boden/Decke/Dach polygon [[x,y],…] mit z (Vorgabe false: schlanke Liste)";

    /// <summary>Summen je Angrenzung: Fläche, U·A (wo U bekannt) und Fläche ohne U.</summary>
    static JsonObject JeAngrenzung(IEnumerable<Raumgrenze> grenzen)
    {
        var o = new JsonObject();
        foreach (var gr in grenzen.GroupBy(g => g.Angrenzung).OrderBy(g => Array.IndexOf(Hlks.Kern.Raumgrenzen.Angrenzungen, g.Key)))
            o[gr.Key] = new JsonObject
            {
                ["flaeche_m2"] = R(gr.Sum(g => g.Flaeche)),
                ["ua_WK"] = R(gr.Where(g => g.UA is not null).Sum(g => g.UA!.Value)),
                ["ohneU_m2"] = R(gr.Where(g => g.U is null).Sum(g => g.Flaeche)),
            };
        return o;
    }

    static JsonObject JeArt(IEnumerable<Raumgrenze> grenzen) =>
        new(grenzen.GroupBy(g => g.Art).OrderBy(g => Array.IndexOf(Hlks.Kern.Raumgrenzen.Arten, g.Key))
            .Select(g => KeyValuePair.Create(g.Key, (JsonNode?)R(g.Sum(x => x.Flaeche)))));

    /// <summary>Senkrechte Flächen gegen aussen je Azimut (auf ganze Grad gerundet).</summary>
    static JsonArray JeOrientierung(IEnumerable<Raumgrenze> grenzen) =>
        new(grenzen.Where(g => g.Angrenzung == "aussen" && g.Azimut is not null)
            .GroupBy(g => (Azimut: Math.Round(g.Azimut!.Value) % 360, g.Art))
            .OrderBy(g => g.Key.Azimut).ThenBy(g => Array.IndexOf(Hlks.Kern.Raumgrenzen.Arten, g.Key.Art))
            .Select(g => (JsonNode)new JsonObject { ["azimut"] = g.Key.Azimut, ["art"] = g.Key.Art, ["flaeche_m2"] = R(g.Sum(x => x.Flaeche)) }).ToArray());

    public static JsonObject Raumgrenzen(Projekt p, string? raum = null, string? geschoss = null, bool geometrie = false)
    {
        var m = p.Modell;
        var auswahl = RaeumeAuswahl(m, raum, geschoss);
        var erg = Hlks.Kern.Raumgrenzen.Rechnen(m, p.Katalog, auswahl);
        var o = Ableitungen.Kopf(p, "Raumgrenzen");
        o["filter"] = new JsonObject { ["raum"] = raum, ["geschoss"] = geschoss };
        o["nordwinkel"] = erg.Nordwinkel;
        o["anzahlRaeume"] = erg.Raeume.Count;
        o["anzahl"] = erg.Grenzen.Count;
        var jeRaum = erg.Grenzen.ToLookup(g => g.Raum.Id);
        o["summenJeRaum"] = new JsonArray(erg.Raeume.Select(rm =>
        {
            var gs = jeRaum[rm.Raum.Id].ToList();
            return (JsonNode)new JsonObject
            {
                ["raum"] = rm.Raum.Kennzeichen,
                ["nummer"] = rm.Raum.Text("nummer"),
                ["name"] = rm.Raum.Name,
                ["geschoss"] = Kz(m.Finde(rm.Raum.Geschoss)),
                ["beheizt"] = Hlks.Kern.Raumgrenzen.Beheizt(rm.Raum),
                ["flaeche_m2"] = R(rm.FlaecheM2),
                ["hoehe_m"] = R(rm.HoeheMm / 1000),
                ["grenzen"] = gs.Count,
                ["jeAngrenzung"] = JeAngrenzung(gs),
                ["jeArt"] = JeArt(gs),
                ["offen_m2"] = R(gs.Where(g => g.Offen).Sum(g => g.Flaeche)),
            };
        }).ToArray());
        o["summen"] = new JsonObject
        {
            ["jeAngrenzung"] = JeAngrenzung(erg.Grenzen),
            ["jeArt"] = JeArt(erg.Grenzen),
            ["jeOrientierung"] = JeOrientierung(erg.Grenzen),
        };
        o["hinweise"] = new JsonArray(erg.Hinweise.Select(h => (JsonNode)h).ToArray());
        o["zeilen"] = new JsonArray(erg.Grenzen.Select(g => (JsonNode)Zeile(g, geometrie)).ToArray());
        return o;
    }

    // ------------------------------------------------------------------ Hüllbauteile

    /// <summary>Ein Bauteil der Hülle mit seinen Flächen.</summary>
    public sealed record Huellbauteil(Element Bauteil, Element? Aufbau, double? U, string? UQuelle, double FlaecheInnen, double? FlaecheAussen,
        double? Azimut, double Neigung, string Angrenzung, IReadOnlyDictionary<string, double> Angrenzungen, IReadOnlyList<Element> Raeume, string? Hinweis);

    static readonly string[] HuelleAngrenzung = ["aussen", "erdreich", "unbeheizt"];

    /// <summary>Raumgrenzen beheizter Räume gegen aussen, Erdreich oder unbeheizt (Hülle des beheizten Volumens).</summary>
    public static bool IstHuelle(Raumgrenze g) => HuelleAngrenzung.Contains(g.Angrenzung) && Hlks.Kern.Raumgrenzen.Beheizt(g.Raum);

    /// <summary>Hüllbauteile aus einem Ergebnis der Raumgrenzen (alle Räume gerechnet).</summary>
    public static List<Huellbauteil> HuellbauteileAus(Modell m, Katalog k, RaumgrenzenErgebnis erg, string? geschossId)
    {
        var liste = new List<Huellbauteil>();
        var alle = erg.Grenzen.Where(g => g.Bauteil is not null).ToLookup(g => g.Bauteil!.Id);
        using var _ = Wandgeometrie.Zwischenspeichern(m, k);
        foreach (var gruppe in erg.Grenzen.Where(g => g.Bauteil is not null && IstHuelle(g)).GroupBy(g => g.Bauteil!.Id))
        {
            var b = gruppe.First().Bauteil!;
            if (geschossId is not null && b.Geschoss != geschossId) continue;
            var gs = gruppe.ToList();
            var groesste = gs.MaxBy(g => g.Flaeche)!;
            var jeAngrenzung = gs.GroupBy(g => g.Angrenzung).ToDictionary(g => g.Key, g => g.Sum(x => x.Flaeche));
            string? hinweis = null;
            double? aussen = null;
            if (k.IstArt(b, "Wand"))
            {
                if (Wandgeometrie.MitVerlaengerung(m, k, b) is { } w && w.Hoehe is double h)
                {
                    double laenge = w.Bis - w.Von, oeff = 0;
                    foreach (var o in Wandgeometrie.Oeffnungen(k, w, Wandgeometrie.OeffnungenVon(m, k, b.Id)))
                    {
                        double z0 = Math.Max(0, o.Unten), z1 = Math.Min(h, o.Oben ?? h);
                        double s0 = Math.Max(w.Von, o.Von), s1 = Math.Min(w.Bis, o.Bis);
                        if (z1 > z0 && s1 > s0) oeff += (s1 - s0) * (z1 - z0);
                    }
                    aussen = Math.Max(0, laenge * h - oeff) / 1e6;
                }
                else hinweis = "Aussenmass fehlt: Wand ohne Achse oder Höhe";
                if (alle[b.Id].Any(g => !IstHuelle(g))) hinweis = Anhaengen(hinweis, "nur zum Teil Hülle (Aussenmass für die ganze Wand)");
            }
            else if (k.IstArt(b, "Fenster") || k.IstArt(b, "Tuer"))
            {
                if (b.Zahl("breite") is double br && b.Zahl("hoehe") is double ho) aussen = br * ho / 1e6;
                else hinweis = "Aussenmass fehlt: Breite oder Höhe fehlt";
            }
            else if (k.IstArt(b, "Decke") || k.IstArt(b, "Dach"))
            {
                aussen = (b.Geometrie?.Flaeche() ?? 0) / 1e6;
                if (alle[b.Id].Any(g => !IstHuelle(g))) hinweis = Anhaengen(hinweis, "nur zum Teil Hülle (Aussenmass = ganze Polygonfläche)");
            }
            var (u, quelle) = Hlks.Kern.Raumgrenzen.UWert(m, k, b);
            liste.Add(new Huellbauteil(b, Hlks.Kern.Raumgrenzen.AufbauVon(m, k, b), u, quelle, gs.Sum(g => g.Flaeche), aussen,
                groesste.Azimut, groesste.Neigung, jeAngrenzung.MaxBy(x => x.Value).Key, jeAngrenzung,
                gs.Select(g => g.Raum).DistinctBy(r => r.Id).ToList(), hinweis));
        }
        return liste.OrderBy(x => Array.IndexOf(Klassenfolge, x.Bauteil.Klasse) is var i && i < 0 ? 99 : i)
            .ThenBy(x => x.Bauteil.Kennzeichen, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static readonly string[] Klassenfolge = ["Wand", "Fenster", "Tuer", "Decke", "Dach"];

    static string Anhaengen(string? a, string b) => a is null ? b : a + "; " + b;

    public static JsonObject Huellbauteile(Projekt p, string? geschoss = null)
    {
        var m = p.Modell; var k = p.Katalog;
        var g = geschoss is null ? null : m.HoleDerKlasse(geschoss, "Geschoss").Id;
        var erg = Hlks.Kern.Raumgrenzen.Rechnen(m, k);
        var liste = HuellbauteileAus(m, k, erg, g);
        var o = Ableitungen.Kopf(p, "Hüllbauteile");
        o["filter"] = new JsonObject { ["geschoss"] = geschoss };
        o["anzahl"] = liste.Count;
        o["summen"] = new JsonObject(liste.GroupBy(x => x.Angrenzung).OrderBy(x => Array.IndexOf(Hlks.Kern.Raumgrenzen.Angrenzungen, x.Key))
            .Select(x => KeyValuePair.Create(x.Key, (JsonNode?)new JsonObject
            {
                ["flaecheInnen_m2"] = R(x.Sum(y => y.FlaecheInnen)),
                ["flaecheAussen_m2"] = R(x.Sum(y => y.FlaecheAussen ?? 0)),
                ["bauteile"] = x.Count(),
            })));
        var ohneBauteil = erg.Grenzen.Where(gr => gr.Bauteil is null && IstHuelle(gr) && (g is null || gr.Raum.Geschoss == g)).ToList();
        o["ohneBauteil_m2"] = R(ohneBauteil.Sum(x => x.Flaeche));
        var hinweise = new List<string>();
        if (ohneBauteil.Count > 0)
            hinweise.Add($"{ohneBauteil.Count} Raumgrenzen der Hülle ohne Bauteil ({Grafik.Format(ohneBauteil.Sum(x => x.Flaeche), 2)} m²: offene Kanten, Boden oder Decke ohne Platte) – in der Liste nicht enthalten.");
        hinweise.Add("Fläche innen = Summe der Raumgrenzen (Innenmass); Aussenmass-Fläche als zweite Grösse – Massbezug je Normverfahren legt Christian fest.");
        o["hinweise"] = new JsonArray(hinweise.Select(h => (JsonNode)h).ToArray());
        o["zeilen"] = new JsonArray(liste.Select(x => (JsonNode)new JsonObject
        {
            ["bauteil"] = x.Bauteil.Kennzeichen,
            ["klasse"] = x.Bauteil.Klasse,
            ["geschoss"] = Kz(m.Finde(x.Bauteil.Geschoss)),
            ["aufbau"] = x.Aufbau?.Kennzeichen,
            ["u"] = x.U,
            ["uQuelle"] = x.UQuelle,
            ["flaecheInnen_m2"] = R(x.FlaecheInnen),
            ["flaecheAussen_m2"] = R(x.FlaecheAussen),
            ["azimut"] = R(x.Azimut, 1),
            ["neigung"] = x.Neigung,
            ["angrenzung"] = x.Angrenzung,
            ["angrenzungen"] = new JsonObject(x.Angrenzungen.OrderBy(a => Array.IndexOf(Hlks.Kern.Raumgrenzen.Angrenzungen, a.Key))
                .Select(a => KeyValuePair.Create(a.Key, (JsonNode?)R(a.Value)))),
            ["raeume"] = new JsonArray(x.Raeume.Select(r => (JsonNode)r.Kennzeichen).ToArray()),
            ["hinweis"] = x.Hinweis,
        }).ToArray());
        return o;
    }
}
