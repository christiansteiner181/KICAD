using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Gemeinsame Hilfen der Architekturtests: Projekt mit Geschoss EG (Kote 0, Höhe 3000) und Grundriss AN-EG.</summary>
static class ArchitekturHilfe
{
    public static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    public static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} | {e.Vorschlag}");
        return e;
    }

    /// <summary>Abgelehnt mit Fehlertext und Vorschlag.</summary>
    public static BefehlsErgebnis Nein(BefehlsErgebnis e)
    {
        Assert.False(e.Angenommen, $"{e.Befehl} hätte abgelehnt werden sollen: {e.Meldung}");
        Assert.False(string.IsNullOrWhiteSpace(e.Fehler));
        Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag), $"Vorschlag fehlt zu: {e.Fehler}");
        return e;
    }

    public static JsonArray Pts(params double[][] p) =>
        new(p.Select(q => (JsonNode)new JsonArray(q.Select(c => (JsonNode)c).ToArray())).ToArray());

    public static JsonArray Pt(double x, double y) => new(x, y);

    public static Element Hole(Projekt p, string kz) => p.Modell.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt im Modell");

    /// <summary>katalog: abgewandelte Katalogkopie (z. B. SanitaerHilfe.KatalogUnbestaetigt); Vorgabe der Katalog der Arbeitskopie.</summary>
    public static Projekt Neu(double schnitthoehe = 1000, Katalog? katalog = null)
    {
        var p = Projekt.Anlegen(katalog ?? Kat, null, "Test Architektur", "T", "pc");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB",
            ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 3000 },
        }));
        Grundriss(p, "AN-EG", schnitthoehe);
        return p;
    }

    public static void Grundriss(Projekt p, string kz, double schnitthoehe) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = schnitthoehe },
        }));

    /// <summary>Vier Wände um das Rechteck 6000 × 4000 (Achsen), Dicke 200: W-1 Süd (→ +x), W-2 Ost, W-3 Nord, W-4 West.</summary>
    public static Projekt Rechteck(string? material = null, double schnitthoehe = 1000)
    {
        var p = Neu(schnitthoehe);
        var par = new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["geschlossen"] = true,
        };
        if (material is not null) par["material"] = material;
        Ok(p.Ausfuehren("wand_zeichnen", par));
        return p;
    }

    public static BefehlsErgebnis Tuer(Projekt p, string wand = "W-1", double abstand = 3000, double breite = 900, string? seite = null, string? band = null, bool trocken = false)
    {
        var par = new JsonObject { ["klasse"] = "Tuer", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = 2100 };
        if (seite is not null) par["seite"] = seite;
        if (band is not null) par["band"] = band;
        return p.Ausfuehren("oeffnung_setzen", par, trocken);
    }

    public static BefehlsErgebnis Fenster(Projekt p, string wand = "W-1", double abstand = 3000, double breite = 1200, double hoehe = 1200, double bruestung = 900) =>
        p.Ausfuehren("oeffnung_setzen", new JsonObject
        {
            ["klasse"] = "Fenster", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = hoehe, ["bruestung"] = bruestung,
        });

    /// <summary>[minx, miny, maxx, maxy] der Punkte.</summary>
    public static double[] Box(IEnumerable<double[]> pts)
    {
        var l = pts.ToList();
        return [l.Min(q => q[0]), l.Min(q => q[1]), l.Max(q => q[0]), l.Max(q => q[1])];
    }

    public static void Nahe(double[] erwartet, double[] ist, double tol = 1e-3)
    {
        Assert.Equal(erwartet.Length, Math.Min(erwartet.Length, ist.Length));
        for (int i = 0; i < erwartet.Length; i++)
            Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"Stelle {i}: erwartet {string.Join(", ", erwartet)}, ist {string.Join(", ", ist)}");
    }

    public static double PolyFlaeche(Primitiv poly) =>
        Math.Abs(Polygone.Flaecheninhalt(poly.Punkte)) - (poly.Loecher ?? []).Sum(l => Math.Abs(Polygone.Flaecheninhalt(l)));

    /// <summary>Gemeinsamer Wandumriss (Kontur dick) der Plangrafik.</summary>
    public static List<Primitiv> Umriss(Plangrafik g) =>
        g.Teile.Where(t => t.Id is null && t.Ebene == "wand").SelectMany(t => t.Primitive).Where(x => x.Stil.Stift == Stifte.Dick).ToList();

    public static Grafikteil Teil(Plangrafik g, Projekt p, string kz) =>
        g.Teile.Single(t => t.Id == Hole(p, kz).Id);
}
