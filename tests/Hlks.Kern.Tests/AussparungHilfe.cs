using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu Modul 3b, B3 (automatische Aussparungen). Alle Werte FIKTIV:
///   Projektvorgaben Spiel 20 mm, Zusammenfassen 100 mm (Testwerte, keine Projektvorgaben – die legt Christian fest); die nicht mehr
///   verwendete Grenze Kernbohrung (Nachtrag B3b) nur auf Wunsch (Grenze 150 mm, für den Hinweis-Test); Testrohrserie
///   tests/daten/familien/test_rohrserie (da = DN + 5, Hauptlauf 18.10.2026); Kanäle mit Eigenschaften breite × hoehe.
///   Handrechnungen in mm.</summary>
static class AussparungHilfe
{
    public const double Spiel = 20, Grenze = 150, Zusammenfassen = 100;
    public const string Quelle = "FIKTIV – Testwert B3, keine Projektvorgabe";

    /// <summary>Vorgaben-Element VG-1 mit den fiktiven Werten (null = Wert weglassen); ohne Quelle, wenn mitQuelle = false.
    /// grenze_kernbohrung nur, wenn angegeben (nicht mehr verwendet, Nachtrag B3b).</summary>
    public static void Vorgaben(Projekt p, double? spiel = Spiel, double? grenze = null, double? zusammenfassen = Zusammenfassen, bool mitQuelle = true)
    {
        var eig = new JsonObject();
        foreach (var (n, w) in new[] { ("spiel_aussparung", spiel), ("grenze_kernbohrung", grenze), ("abstand_zusammenfassen", zusammenfassen) })
            if (w is double d) eig[n] = mitQuelle ? new JsonObject { ["wert"] = d, ["quelle"] = Quelle } : d;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Vorgaben", ["kennzeichen"] = "VG-1", ["eigenschaften"] = eig }));
    }

    /// <summary>System KW (Trinkwasser kalt) und Testrohrserie (FAM-1) laden – für Modul3Hilfe.Rohr.</summary>
    public static void Serie(Projekt p)
    {
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }));
    }

    /// <summary>Vier Wände 6000 × 4000, Dicke 200, Höhe 3000 (W-1 Süd: y −100 … 100), Geschoss OG (Kote 3000), Testserie, Vorgaben.</summary>
    public static Projekt Wandprojekt(bool vorgaben = true)
    {
        var p = Rechteck();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Serie(p);
        if (vorgaben) Vorgaben(p);
        return p;
    }

    /// <summary>Rohr der Testserie (DN 50: da 55, FIKTIV) quer durch W-1 bei x, Höhe z (y −500 … 500).</summary>
    public static void RohrDurchW1(Projekt p, string kz, double x, double z = 1000, string typ = "DN 50", double daemmung = 0)
    {
        Ok(Modul3Hilfe.Rohr(p, kz, typ, [x, -500, z], [x, 500, z]));
        if (daemmung > 0) Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = new JsonObject { ["daemmdicke"] = daemmung } }));
    }

    /// <summary>Leitung verschieben (neue Punkte).</summary>
    public static void Verschieben(Projekt p, string kz, params double[][] punkte) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) } }));

    /// <summary>Kanal mit Eigenschaften breite × hoehe (ohne Serie), System ZUL (Zuluft).</summary>
    public static void Kanal(Projekt p, string kz, double b, double h, params double[][] punkte)
    {
        if (p.Modell.Finde("ZUL") is null)
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["breite"] = b, ["hoehe"] = h },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        }));
    }

    /// <summary>Decke D-1 im OG: 6000 × 4000, Oberkante 3000, Dicke 250 (z 2750 … 3000).</summary>
    public static void Decke(Projekt p) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-1", ["geschoss"] = "OG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 3000], [6000, 0, 3000], [6000, 4000, 3000], [0, 4000, 3000]), ["dicke"] = 250 },
        }));

    public static BefehlsErgebnis Vorschlagen(Projekt p, JsonObject? par = null, bool trocken = false) => p.Ausfuehren("aussparungen_vorschlagen", par ?? new JsonObject(), trocken);

    public static BefehlsErgebnis Status(Projekt p, string status, params string[] aussparungen) =>
        p.Ausfuehren("aussparung_status", new JsonObject { ["aussparungen"] = new JsonArray(aussparungen.Select(a => (JsonNode)a).ToArray()), ["status"] = status });

    /// <summary>aussparung_form (Nachtrag B3b): Form von Hand wählen.</summary>
    public static BefehlsErgebnis Form(Projekt p, string form, bool trocken, params string[] aussparungen) =>
        p.Ausfuehren("aussparung_form", new JsonObject { ["aussparungen"] = new JsonArray(aussparungen.Select(a => (JsonNode)a).ToArray()), ["form"] = form }, trocken);

    public static BefehlsErgebnis Form(Projekt p, string form, params string[] aussparungen) => Form(p, form, false, aussparungen);

    /// <summary>Vorschlagen und die Aussparung der Leitung von Hand rund machen (Kernbohrung) – für Tests von Grafik und Ausgabe
    /// runder Aussparungen.</summary>
    public static Element RundVonHand(Projekt p, string leitung)
    {
        Ok(Form(p, "rund", VonLeitung(p, leitung).Kennzeichen));
        return VonLeitung(p, leitung);
    }

    public static List<Element> Aussparungen(Projekt p) => p.Modell.DerKlasse("Aussparung").OrderBy(a => a.Kennzeichen, StringComparer.Ordinal).ToList();

    /// <summary>Die einzige Aussparung mit Bezug auf die Leitung (Kennzeichen).</summary>
    public static Element VonLeitung(Projekt p, string leitung)
    {
        var id = Hole(p, leitung).Id;
        return Assert.Single(p.Modell.DerKlasse("Aussparung"), a => Aussparungsbezug.Leitungen(a).Contains(id) && AussparungStatus.Von(a) != AussparungStatus.Veraltet);
    }

    public static double[] Mitte(Element a) => a.Geometrie!.Punkte[0];

    public static List<string> LeitungKz(Projekt p, Element a) => Aussparungsbezug.Leitungen(a).Select(id => p.Modell.Finde(id)!.Kennzeichen).ToList();

    public static void Etwa(double erwartet, double? ist, double tol = 1e-6, string? was = null)
    {
        Assert.NotNull(ist);
        Assert.True(Math.Abs(erwartet - ist!.Value) <= tol, $"{was}: erwartet {erwartet}, ist {ist} (Toleranz {tol})");
    }
}
