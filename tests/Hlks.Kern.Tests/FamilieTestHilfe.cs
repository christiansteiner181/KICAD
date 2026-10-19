using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Familientests (Modul 2, B1). Alle Werte sind FIKTIV (Testfamilie tests/daten/familien/
/// test_heizkoerper.familie.json, Hauptlauf 18.10.2026) – keine Herstellerdaten, keine Normwerte.</summary>
static class FamilieTestHilfe
{
    public const string HkName = "Testheizkörper Typ 22 (fiktiv)";

    /// <summary>Die Testfamilie als neues JSON-Objekt.</summary>
    public static JsonObject Testheizkoerper() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Path.Combine("familien", "test_heizkoerper.familie.json"))))!;

    public static string TestheizkoerperPfad => IfcHilfe.Testdaten(Path.Combine("familien", "test_heizkoerper.familie.json"));

    /// <summary>Gelesene Definition gegen den Katalog.</summary>
    public static FamilieDef Def(JsonObject d) => FamilieDef.Lesen(d, Kat);

    /// <summary>Projekt mit Geschoss EG (Kote 0, Höhe 3000), Grundriss AN-EG und der Testfamilie als FAM-1.</summary>
    public static Projekt MitFamilie()
    {
        var p = Neu();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper() }));
        return p;
    }

    /// <summary>Typ 22-600-1000 bei [5000, 3000, 0], Drehung 90° (die Handrechnung der Spezifikation) als HK-1.</summary>
    public static Projekt MitHeizkoerper(string typ = "22-600-1000", double drehung = 90, JsonObject? parameter = null)
    {
        var p = MitFamilie();
        var par = new JsonObject { ["familie"] = "FAM-1", ["typ"] = typ, ["punkt"] = new JsonArray(5000, 3000, 0), ["drehung"] = drehung, ["kennzeichen"] = "HK-1" };
        if (parameter is not null) par["parameter"] = parameter;
        Ok(p.Ausfuehren("familie_platzieren", par));
        return p;
    }

    public static void Etwa(double erwartet, double ist, double tol = 1e-6, string? was = null) =>
        Assert.True(Math.Abs(erwartet - ist) <= tol, $"{was}: erwartet {erwartet}, ist {ist} (Toleranz {tol})");

    public static void Etwa(double[] erwartet, double[] ist, double tol = 1e-6, string? was = null)
    {
        Assert.Equal(erwartet.Length, ist.Length);
        for (int i = 0; i < erwartet.Length; i++) Etwa(erwartet[i], ist[i], tol, $"{was}[{i}]");
    }

    /// <summary>Fläche eines regelmässigen n-Ecks mit Umkreisradius r (Facettierung der Kreise: n = 48).</summary>
    public static double NEck(double r, int n = 48) => 0.5 * n * r * r * Math.Sin(2 * Math.PI / n);

    /// <summary>Definition aus JSON-Text (für kleine Testfamilien).</summary>
    public static JsonObject JsonAus(string text) => (JsonObject)JsonNode.Parse(text)!;

    /// <summary>Kleinste gültige Familie (Kategorie Heizkoerper) mit den gegebenen Parametern, Typen, Körpern, Anschlüssen.</summary>
    public static JsonObject Mini(string parameter = "[]", string typen = "[{ \"name\": \"A\", \"werte\": {} }]", string koerper = "[]", string anschluesse = "[]", string mehr = "") =>
        JsonAus($$"""{ "format": 1, "name": "Mini", "kategorie": "Heizkoerper", "parameter": {{parameter}}, "typen": {{typen}}, "koerper": {{koerper}}, "anschluesse": {{anschluesse}} {{mehr}} }""");
}

/// <summary>Zusätzliche Prüfung nur für den Test des Einhängepunkts (B4): wirkt nur auf Familien namens «Prüfhaken».</summary>
public sealed class TestFamilienPruefung : IFamilienPruefung
{
    public string Name => "test";

    public void Pruefen(FamilienPruefKontext ctx)
    {
        if (ctx.Familie.Name != "Prüfhaken") return;
        ctx.Definition["abdeckung"] = new JsonObject { ["hoehe"] = new JsonObject { ["fehlt"] = true } };
        ctx.Hinweise.Add("Testprüfung gelaufen");
        if (ctx.Familie.Typen.Any(t => t.Name == "verboten")) ctx.Fehler.Add(new FamFehler("typ «verboten»", "Testfehler aus der Zusatzprüfung", "Typ umbenennen"));
    }
}
