using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Routing-Tests (Modul 3, B2). Alle Werte FIKTIV aus den Testfamilien tests/daten/familien:
/// Testrohrserie da = DN + 5; Testbogen r = 2·da, Einbaulänge r·tan(w/2); Test-T-Stück l = lc = da (nur gleiche Grössen);
/// Testreduktion l = 40; Testkanalserie b × h mit Kanalbogen r = b, Kanal-T l = 300 / lc = 250, Kanalreduktion l = 200.</summary>
static class RoutingHilfe
{
    public static JsonArray Punkte(params double[][] p) => Pts(p);

    /// <summary>leitung_zeichnen mit der Testrohrserie (Vorgabe System KW).</summary>
    public static BefehlsErgebnis Zeichnen(Projekt p, string groesse, double[][] punkte, JsonObject? extra = null, string serie = "fam-test-rohrserie",
        string? system = "KW", bool trocken = false)
    {
        var par = new JsonObject { ["serie"] = serie, ["groesse"] = groesse, ["punkte"] = Pts(punkte) };
        if (system is not null) par["system"] = system;
        foreach (var (k, v) in extra ?? []) par[k] = v?.DeepClone();
        return p.Ausfuehren("leitung_zeichnen", par, trocken);
    }

    public static List<double[]> Pkt(Projekt p, string kz) => Hole(p, kz).Geometrie!.Punkte;

    /// <summary>Länge einer Leitung (mm).</summary>
    public static double Laenge(Projekt p, string kz) => Hole(p, kz).Geometrie!.Laenge();

    /// <summary>Gerade von a nach b (je auf 0.001 mm).</summary>
    public static void Gerade(Projekt p, string kz, double[] a, double[] b, double tol = 1e-3)
    {
        var pt = Pkt(p, kz);
        Assert.Equal(2, pt.Count);
        Etwa(a, pt[0], tol, kz + " Anfang");
        Etwa(b, pt[1], tol, kz + " Ende");
    }

    /// <summary>Verbindung zwischen zwei Anschlüssen (Richtung egal).</summary>
    public static bool Verbunden(Projekt p, string a, string an, string b, string bn) =>
        p.Modell.VerbindungAn(Hole(p, a).Id, an)?.Gegenueber(Hole(p, a).Id, an) is { } g && g.Element == Hole(p, b).Id && g.Anschluss.Equals(bn, StringComparison.OrdinalIgnoreCase);

    public static void IstVerbunden(Projekt p, string a, string an, string b, string bn) =>
        Assert.True(Verbunden(p, a, an, b, bn), $"{a}.{an} – {b}.{bn} ist nicht verbunden");

    /// <summary>Kennzeichen der im Ergebnis angelegten Elemente einer Klasse.</summary>
    public static List<string> Angelegt(BefehlsErgebnis e, string klasse) =>
        e.Aenderungen.Where(a => a.Art == "angelegt" && a.Klasse == klasse).Select(a => a.Kennzeichen).ToList();

    /// <summary>Keine Befunde P13, P14, P15, P18 (P16 nur, wenn ausdrücklich geprüft).</summary>
    public static void Sauber(Projekt p)
    {
        var b = Pruefungen.Alle(p.Modell, p.Katalog).Where(x => x.Code is "P13" or "P14" or "P15" or "P18").ToList();
        Assert.True(b.Count == 0, "Befunde: " + string.Join("; ", b.Select(x => $"{x.Code} {x.Element}: {x.Text}")));
    }

    public static Projekt MitSystem(Projekt p, string kz, string systemtyp)
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = systemtyp } }));
        return p;
    }

    /// <summary>Projekt mit Testkanalserie und ihren Formteilen (FAM-1 … FAM-4) und System ZUL (Zuluft).</summary>
    public static Projekt MitKanal()
    {
        var p = Neu();
        MitSystem(p, "ZUL", "Zuluft");
        foreach (var f in new[] { "test_kanalserie", "test_kanalbogen", "test_kanal_t", "test_kanalreduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    /// <summary>Inhalt des Modells ohne Versionen und Paketvermerk (für Trockenlauf und Rückgängig).</summary>
    public static string Inhalt(Projekt p) => p.Modell.Pruefsumme();

    public static double Tan(double grad) => Math.Tan(grad * Math.PI / 180);
}
