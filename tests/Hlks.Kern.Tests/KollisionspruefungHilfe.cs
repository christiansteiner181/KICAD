using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu Modul 3b, B2 (Kollisionsprüfung). Alle Masse FIKTIV aus der Testrohrserie (tests/daten/familien:
/// da = DN + 5, Hauptlauf 18.10.2026); alle Projektvorgaben in den Tests FIKTIV (Quelle «FIKTIV, Test B2») – keine Bürowerte.</summary>
static class KollisionspruefungHilfe
{
    public const string QuelleFiktiv = "FIKTIV, Test B2 – kein Bürostandard";

    /// <summary>Rechteck mit vier Wänden 6000 × 4000, Dicke 200, Höhe 3000 (W-1 Süd: Achse y = 0, also y −100 … 100), System KW
    /// und Testrohrserie (FAM-1).</summary>
    public static Projekt Wandprojekt()
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }));
        return p;
    }

    public static BefehlsErgebnis Pruefen(Projekt p, JsonObject? par = null, bool trocken = false) =>
        p.Ausfuehren("kollision_pruefen", par ?? new JsonObject(), trocken);

    public static List<Element> Kollisionen(Projekt p) => BcfFormat.Kollisionen(p.Modell, p.Katalog);

    public static List<string> Beteiligte(Element k) =>
        k.Eigenschaft("beteiligte")!.Inhalt!.AsArray().Select(x => x!.GetValue<string>()).ToList();

    public static double[] Lage(Element k) => k.Geometrie!.Punkte[0];

    /// <summary>Projektvorgaben setzen, alle mit der FIKTIVEN Testquelle.</summary>
    public static BefehlsErgebnis Vorgaben(Projekt p, params (string Name, JsonNode Wert)[] werte)
    {
        var o = new JsonObject();
        foreach (var (n, w) in werte) o[n] = new JsonObject { ["wert"] = w.DeepClone(), ["quelle"] = QuelleFiktiv };
        return p.Ausfuehren("vorgaben_setzen", new JsonObject { ["werte"] = o });
    }

    public static void Geometrie(Projekt p, string kz, params double[][] punkte) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) } }));

    public static void Daemmung(Projekt p, string kz, double mm) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = new JsonObject { ["daemmdicke"] = mm } }));

    public static BefehlsErgebnis Status(Projekt p, string status, string? kommentar, params string[] kollisionen)
    {
        var par = new JsonObject { ["kollisionen"] = new JsonArray(kollisionen.Select(x => (JsonNode)x).ToArray()), ["status"] = status };
        if (kommentar is not null) par["kommentar"] = kommentar;
        return p.Ausfuehren("kollision_status", par);
    }

    public static string Hinweis(BefehlsErgebnis e, string teil) =>
        e.Hinweise.FirstOrDefault(h => h.Contains(teil)) ?? throw new Xunit.Sdk.XunitException($"Hinweis mit «{teil}» fehlt: {string.Join(" | ", e.Hinweise)}");

    public static JsonArray Par(params string[] werte) => new(werte.Select(x => (JsonNode)x).ToArray());

    /// <summary>Aussparung von Hand im Format von Teil B3 (bezuege.bauteil, leitung1 …; achse = Bauteilnormale), Werte FIKTIV.</summary>
    public static BefehlsErgebnis Aussparung(Projekt p, string kz, string bauteil, double[] mitte, double[] achse, string form, double d = 0,
        double b = 0, double h = 0, double tiefe = 200, string? status = null, params string[] leitungen)
    {
        var eig = new JsonObject { ["form"] = form, ["tiefe"] = tiefe, ["achse"] = new JsonArray(achse.Select(x => (JsonNode)x).ToArray()) };
        if (form == "rund") eig["d"] = d; else { eig["b"] = b; eig["h"] = h; }
        if (status is not null) eig["status"] = status;
        var bez = new JsonObject { ["bauteil"] = bauteil };
        for (int i = 0; i < leitungen.Length; i++) bez[$"leitung{i + 1}"] = leitungen[i];
        return p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aussparung", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["eigenschaften"] = eig, ["bezuege"] = bez,
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(mitte) },
        });
    }
}
