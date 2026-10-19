using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Hülletests (Modul 1c, B2). Alle Zahlen sind frei gewählte Testwerte und stammen aus keiner Norm.</summary>
static class HuelleTestHilfe
{
    public const string TestQuelle = "Testwert (frei gewählt, keine Norm)";

    public static JsonObject Kw(double wert, string? quelle = TestQuelle)
    {
        var o = new JsonObject { ["wert"] = wert };
        if (quelle is not null) o["quelle"] = quelle;
        return o;
    }

    public static BefehlsErgebnis Material(Projekt p, string name, double? lambda, bool trocken = false)
    {
        var par = new JsonObject { ["name"] = name };
        if (lambda is double l) par["werte"] = new JsonObject { ["lambda"] = Kw(l) };
        return p.Ausfuehren("material_anlegen", par, trocken);
    }

    public static JsonArray Schichten(params (string Material, double Dicke)[] s) =>
        new(s.Select(x => (JsonNode)new JsonObject { ["material"] = x.Material, ["dicke"] = x.Dicke }).ToArray());

    public static BefehlsErgebnis Aufbau(Projekt p, string name, string art, JsonArray schichten, bool trocken = false) =>
        p.Ausfuehren("aufbau_anlegen", new JsonObject { ["name"] = name, ["art"] = art, ["schichten"] = schichten }, trocken);

    public static BefehlsErgebnis Uebergang(Projekt p, string gruppe, double rsi, double rse, string? quelle = TestQuelle)
    {
        var par = new JsonObject { [gruppe] = new JsonObject { ["rsi"] = rsi, ["rse"] = rse } };
        if (quelle is not null) par["quelle"] = quelle;
        return p.Ausfuehren("uebergang_setzen", par);
    }

    /// <summary>Rechteck mit vier Wänden, Material MAT-1 (λ = 0.5) und AUF-1 (wand, 200 mm MAT-1). Testwerte.</summary>
    public static Projekt MitAufbau()
    {
        var p = Rechteck();
        Ok(Material(p, "Testmaterial A", 0.5));
        Ok(Aufbau(p, "Testaufbau A", "wand", Schichten(("MAT-1", 200))));
        return p;
    }

    public static double? Zahl(Projekt p, string kz, string eigenschaft) => Hole(p, kz).Zahl(eigenschaft);

    public static void Gleich(double erwartet, double? ist, double tol = 1e-9)
    {
        Assert.NotNull(ist);
        Assert.True(Math.Abs(erwartet - ist!.Value) <= tol, $"erwartet {erwartet}, ist {ist}");
    }

    /// <summary>Geschossdecke als Polygon (Testgeometrie) über element_anlegen.</summary>
    public static BefehlsErgebnis Decke(Projekt p, string klasse = "Decke", string? art = null)
    {
        var eig = new JsonObject();
        if (art is not null) eig["art"] = art;
        return p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = klasse, ["geschoss"] = "EG", ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject
            {
                ["art"] = "polygon", ["punkte"] = Pts([0, 0, 3000], [6000, 0, 3000], [6000, 4000, 3000], [0, 4000, 3000]), ["dicke"] = 250,
            },
        });
    }
}
