using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu Modul 3, B1 (Leitungsserien, Formteil-Familien, Ausrichtung, Anschlusslagen, verbinden, P13–P18).
/// Alle Werte FIKTIV: die Testfamilien tests/daten/familien/test_rohrserie|test_bogen|test_tstueck|test_reduktion (Hauptlauf,
/// 18.10.2026: da = DN + 5, Bogen r = 2·da und Einbaulänge r·tan(w/2), T-Stück l = lc = da, Reduktion l = 40) und eigene,
/// im Test als FIKTIV markierte Mini-Familien – keine Herstellerdaten, keine Normmasse.</summary>
static class Modul3Hilfe
{
    public static string Datei(string name) => IfcHilfe.Testdaten(Path.Combine("familien", name + ".familie.json"));

    public static JsonObject Familie(string name) => (JsonObject)JsonNode.Parse(File.ReadAllText(Datei(name)))!;

    /// <summary>Projekt mit Geschoss EG (Kote 0), System KW (Trinkwasser kalt) und der Testrohrserie (FAM-1);
    /// mit formteile auch Bogen (FAM-2), T-Stück (FAM-3), Reduktion (FAM-4).</summary>
    public static Projekt MitSerie(bool formteile = true)
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_rohrserie") }));
        if (formteile)
            foreach (var f in new[] { "test_bogen", "test_tstueck", "test_reduktion" })
                Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    /// <summary>Rohr mit Serie (fam-test-rohrserie) und Grösse (Typ) entlang der Punkte, System KW.</summary>
    public static BefehlsErgebnis Rohr(Projekt p, string kz, string typ, params double[][] punkte) =>
        p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        });

    /// <summary>Rohr ohne Serie (nur dn) entlang der Punkte, System KW.</summary>
    public static BefehlsErgebnis RohrOhneSerie(Projekt p, string kz, double dn, params double[][] punkte) =>
        p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = dn },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        });

    /// <summary>Formteil der Testfamilien platzieren (System KW); ausrichtung { x, z } bzw. drehung.</summary>
    public static BefehlsErgebnis Formteil(Projekt p, string familie, string typ, string kz, double[] punkt, double? winkel = null,
        double[]? x = null, double[]? z = null, double? drehung = null, bool trocken = false)
    {
        var par = new JsonObject
        {
            ["familie"] = familie, ["typ"] = typ, ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "EG",
            ["punkt"] = new JsonArray(punkt.Select(c => (JsonNode)c).ToArray()),
        };
        if (winkel is double w) par["parameter"] = new JsonObject { ["winkel"] = w };
        if (x is not null && z is not null) par["ausrichtung"] = Ausrichtung(x, z);
        if (drehung is double d) par["drehung"] = d;
        return p.Ausfuehren("familie_platzieren", par, trocken);
    }

    public static JsonObject Ausrichtung(double[] x, double[] z) => new()
    {
        ["x"] = new JsonArray(x.Select(c => (JsonNode)c).ToArray()), ["z"] = new JsonArray(z.Select(c => (JsonNode)c).ToArray()),
    };

    public static AnschlussStelle Stelle(Projekt p, string kz, string anschluss) =>
        Anschlusslagen.Stelle(p.Modell, p.Katalog, Hole(p, kz), anschluss) ?? throw new Xunit.Sdk.XunitException($"{kz}.{anschluss} fehlt");

    public static Leitungsserie Serie(Projekt p) => Leitungsserie.Hole(p.Modell, p.Katalog, "fam-test-rohrserie");

    public static void Etwa(double erwartet, double ist, double tol = 1e-6, string? was = null) =>
        Assert.True(Math.Abs(erwartet - ist) <= tol, $"{was}: erwartet {erwartet}, ist {ist} (Toleranz {tol})");

    public static void Etwa(double[] erwartet, double[]? ist, double tol = 1e-6, string? was = null)
    {
        Assert.NotNull(ist);
        Assert.Equal(erwartet.Length, ist!.Length);
        for (int i = 0; i < erwartet.Length; i++) Etwa(erwartet[i], ist[i], tol, $"{was}[{i}]");
    }

    /// <summary>Befunde eines Codes.</summary>
    public static List<Befund> Befunde(Projekt p, string code) => Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == code).ToList();
}
