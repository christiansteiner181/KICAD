using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu Modul 5 Teil A-D (Heizung): kleine Heiznetze über die Befehle des Kerns. Alle Lagen, Leistungen und Temperaturen sind
/// FIKTIV (Testnetze ohne Bezug zu einem Objekt) und tragen die Quelle «Testwert (fiktiv)» – keine Normwerte.</summary>
static class HeizungHilfe
{
    public const string Testwert = "Testwert (fiktiv)";

    public static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    public static BefehlsErgebnis Gut(Projekt p, string befehl, JsonObject par, bool trocken = false)
    {
        var e = p.Ausfuehren(befehl, par, trocken);
        Assert.True(e.Angenommen, $"{befehl} abgelehnt: {e.Fehler} | {e.Vorschlag}");
        return e;
    }

    public static BefehlsErgebnis Gut(Projekt p, string befehl, string json, bool trocken = false) => Gut(p, befehl, O(json), trocken);

    /// <summary>Abgelehnt mit Fehlertext (Vorschlag, wo der Kern einen nennt).</summary>
    public static BefehlsErgebnis Abgelehnt(Projekt p, string befehl, string json)
    {
        var e = p.Ausfuehren(befehl, O(json));
        Assert.False(e.Angenommen, $"{befehl} hätte abgelehnt werden sollen: {e.Meldung}");
        Assert.False(string.IsNullOrWhiteSpace(e.Fehler));
        return e;
    }

    public static List<Befund> Befunde(Projekt p, string code) => Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == code).ToList();

    public static HeizgruppenWeg Weg(Projekt p, string heizgruppe) => Heizgruppen.Ergebnis(p.Modell, p.Katalog).Gruppen[p.Modell.Hole(heizgruppe).Id];

    public static void Strang(Projekt p, string kz, string system) =>
        Gut(p, "element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = kz, ["teilVon"] = system, ["eigenschaften"] = new JsonObject { ["art"] = "verteilleitung" } });

    public static void Punkt(Projekt p, string klasse, string kz, string? strang, double x, double y, double z, JsonObject? eig = null, string? raum = null)
    {
        var o = new JsonObject
        {
            ["klasse"] = klasse, ["kennzeichen"] = kz, ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = new JsonArray(new JsonArray(x, y, z)) },
        };
        if (strang is not null) o["teilVon"] = strang;
        if (eig is not null) o["eigenschaften"] = eig;
        if (raum is not null) o["raum"] = raum;
        Gut(p, "element_anlegen", o);
    }

    static int rohre;

    /// <summary>Rohr DN 25 Stahl von «Element.anschluss» nach «Element.anschluss» (null = offenes Ende); Lage fiktiv auf Höhe 500.</summary>
    public static void Rohr(Projekt p, string kz, string strang, string? von, string? nach)
    {
        double y = 100 * Interlocked.Increment(ref rohre) % 4000;
        var o = new JsonObject
        {
            ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = 25, ["material"] = "Stahl",
            ["punkte"] = new JsonArray(new JsonArray(0, y, 500), new JsonArray(500, y, 500)),
        };
        if (von is not null) o["von"] = von;
        if (nach is not null) o["nach"] = nach;
        Gut(p, "leitung_ziehen", o);
    }

    public static JsonObject Leistung(double w) => new() { ["leistung"] = new JsonObject { ["wert"] = w, ["quelle"] = Testwert } };

    /// <summary>Kleines Heiznetz (FIKTIV): Gebäude, EG, Raum R-1, Anlage A-HZG mit dem Systempaar HZ-VL/HZ-RL (paar = false: zwei Systeme ohne Paar),
    /// Stränge HZ-V/HZ-R, Verteiler VT-1 (ohne Familie: Abgänge vl_1/rl_1, vl_2/rl_2).
    ///   vl_1: R-V1 → Absperrventil VE-1 → R-V2 → Mischventil MV-1 (a → ab; vertauscht: ab ← VE-1, a → P-1) → R-V3 → Pumpe P-1 → R-V4 → Heizkörper HK-1 (800 W);
    ///         Rücklauf HK-1.rl → R-R1 → T-Stück TS-1 → R-R3 → VT-1.rl_1, Bypass TS-1.c → R-R2 → MV-1.b (bypass = false: R-R2 ohne MV-1, b offen)
    ///   vl_2: R-V5 → Pumpe P-2 (pumpe2 = false: ohne) → R-V6 → Heizkörper HK-2 (600 W) → R-R4 → VT-1.rl_2
    /// gruppen: HG-1 (vl_1) und HG-2 (vl_2) mit 55/45 °C (Testwert) über heizgruppe_anlegen.</summary>
    public static Projekt Netz(bool gruppen = true, bool paar = true, bool vertauscht = false, bool bypass = true, bool pumpe2 = true)
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Raum","kennzeichen":"R-1","name":"Wohnen","geschoss":"EG","eigenschaften":{"nummer":"R-1","nutzung":"Wohnen","lichteHoehe":2500},"geometrie":{"art":"polygon","punkte":[[0,-1000],[8000,-1000],[8000,5000],[0,5000]]}}""");
        Gut(p, "element_anlegen", """{"klasse":"Anlage","kennzeichen":"A-HZG","name":"Heizungsanlage (Test)"}""");
        if (paar) Gut(p, "system_paar_anlegen", """{"systemtyp":"Heizung Vorlauf","anlage":"A-HZG"}""");
        else
        {
            Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-VL","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Vorlauf"}}""");
            Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-RL","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Rücklauf"}}""");
        }
        Strang(p, "HZ-V", "HZ-VL");
        Strang(p, "HZ-R", "HZ-RL");
        Punkt(p, "Verteiler", "VT-1", null, 0, 0, 0);
        Punkt(p, "Ventil", "VE-1", "HZ-V", 1000, 0, 500, O("""{"art":"absperrventil","dn":25}"""));
        Punkt(p, "Mischventil", "MV-1", "HZ-V", 2000, 0, 500, O("""{"art":"3weg","dn":25}"""));
        Punkt(p, "Pumpe", "P-1", "HZ-V", 3000, 0, 500, O("""{"dn":25}"""));
        Punkt(p, "Heizkoerper", "HK-1", null, 5000, 0, 500, Leistung(800), "R-1");
        Punkt(p, "TStueck", "TS-1", "HZ-R", 2000, 500, 500, O("""{"dn":25}"""));
        Rohr(p, "R-V1", "HZ-V", "VT-1.vl_1", "VE-1.ein");
        if (vertauscht)
        {
            // wie im Revit-Modell mit verkehrter Fliessrichtung: Leitungen gegen den Fluss gezeichnet, ab zeigt zum Abgang, a zu den Verbrauchern
            Punkt(p, "Bogen", "BO-1", "HZ-V", 2500, 0, 500);
            Rohr(p, "R-V2", "HZ-V", "MV-1.ab", "VE-1.aus");
            Rohr(p, "R-V3a", "HZ-V", "BO-1.a", "MV-1.a");
            Rohr(p, "R-V3", "HZ-V", "BO-1.b", "P-1.saug");
        }
        else
        {
            Rohr(p, "R-V2", "HZ-V", "VE-1.aus", "MV-1.a");
            Rohr(p, "R-V3", "HZ-V", "MV-1.ab", "P-1.saug");
        }
        Rohr(p, "R-V4", "HZ-V", "P-1.druck", "HK-1.vl");
        Rohr(p, "R-R1", "HZ-R", "HK-1.rl", "TS-1.a");
        Rohr(p, "R-R2", "HZ-R", "TS-1.c", bypass ? "MV-1.b" : null);
        Rohr(p, "R-R3", "HZ-R", "TS-1.b", "VT-1.rl_1");
        Punkt(p, "Heizkoerper", "HK-2", null, 5000, 2000, 500, Leistung(600), "R-1");
        if (pumpe2)
        {
            Punkt(p, "Pumpe", "P-2", "HZ-V", 1000, 2000, 500, O("""{"dn":20}"""));
            Rohr(p, "R-V5", "HZ-V", "VT-1.vl_2", "P-2.saug");
            Rohr(p, "R-V6", "HZ-V", "P-2.druck", "HK-2.vl");
        }
        else Rohr(p, "R-V6", "HZ-V", "VT-1.vl_2", "HK-2.vl");
        Rohr(p, "R-R4", "HZ-R", "HK-2.rl", "VT-1.rl_2");
        if (gruppen)
        {
            Gut(p, "heizgruppe_anlegen", $$"""{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1","vlTemp":55,"rlTemp":45,"quelle":"{{Testwert}}"}""");
            Gut(p, "heizgruppe_anlegen", $$"""{"kennzeichen":"HG-2","verteiler":"VT-1","abgangVl":"vl_2","vlTemp":55,"rlTemp":45,"quelle":"{{Testwert}}"}""");
        }
        return p;
    }

    /// <summary>FBH-Verteiler als Unterverteiler (FIKTIV): VT-1.vl_1 → Pumpe P-1 → Absperrventil VE-1 → T-Stück TS-V; Ast über b: Strangregulierventil
    /// VE-2 → FBH-Verteiler VT-2 mit Kreis FBH-1 (500 W); Ast über c: FBH-Verteiler VT-3 mit Kreis FBH-2 (500 W); Rücklauf über TS-R zu VT-1.rl_1.
    /// Ohne Heizgruppe.</summary>
    public static Projekt NetzMitUnterverteilern()
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Anlage","kennzeichen":"A-HZG"}""");
        Gut(p, "system_paar_anlegen", """{"systemtyp":"Heizung Vorlauf","anlage":"A-HZG"}""");
        Strang(p, "HZ-V", "HZ-VL");
        Strang(p, "HZ-R", "HZ-RL");
        Punkt(p, "Verteiler", "VT-1", null, 0, 0, 0);
        Punkt(p, "Pumpe", "P-1", "HZ-V", 500, 0, 500);
        Punkt(p, "Ventil", "VE-1", "HZ-V", 900, 0, 500, O("""{"art":"absperrventil"}"""));
        Punkt(p, "TStueck", "TS-V", "HZ-V", 1000, 0, 500);
        Punkt(p, "TStueck", "TS-R", "HZ-R", 1000, 300, 500);
        Punkt(p, "Ventil", "VE-2", "HZ-V", 1500, 0, 500, O("""{"art":"strangregulierventil"}"""));
        foreach (var (vt, fbh, x) in new[] { ("VT-2", "FBH-1", 2000.0), ("VT-3", "FBH-2", 3000.0) })
        {
            Punkt(p, "Verteiler", vt, null, x, 0, 0);
            Gut(p, "element_anlegen", new JsonObject
            {
                ["klasse"] = "Fussbodenheizkreis", ["kennzeichen"] = fbh, ["geschoss"] = "EG", ["eigenschaften"] = Leistung(500),
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(new JsonArray(x, 0), new JsonArray(x + 500, 0), new JsonArray(x + 500, 500), new JsonArray(x, 500)) },
            });
            Rohr(p, $"R-{fbh}-V", "HZ-V", $"{vt}.vl_1", $"{fbh}.vl");
            Rohr(p, $"R-{fbh}-R", "HZ-R", $"{fbh}.rl", $"{vt}.rl_1");
        }
        Rohr(p, "R-1", "HZ-V", "VT-1.vl_1", "P-1.saug");
        Rohr(p, "R-2", "HZ-V", "P-1.druck", "VE-1.ein");
        Rohr(p, "R-3", "HZ-V", "VE-1.aus", "TS-V.a");
        Rohr(p, "R-4", "HZ-V", "TS-V.b", "VE-2.ein");
        Rohr(p, "R-5", "HZ-V", "VE-2.aus", "VT-2.vl_ein");
        Rohr(p, "R-6", "HZ-V", "TS-V.c", "VT-3.vl_ein");
        Rohr(p, "R-7", "HZ-R", "VT-2.rl_aus", "TS-R.b");
        Rohr(p, "R-8", "HZ-R", "VT-3.rl_aus", "TS-R.c");
        Rohr(p, "R-9", "HZ-R", "TS-R.a", "VT-1.rl_1");
        return p;
    }

    /// <summary>Netz mit Masche (FIKTIV): VT-1.vl_1 → TS-A, von TS-A zwei parallele Rohre nach TS-B, weiter zu Pumpe P-1 und Heizkörper HK-1 (700 W),
    /// Rücklauf direkt zu VT-1.rl_1; Heizgruppe HG-1 (vl_1).</summary>
    public static Projekt NetzMitMasche()
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Anlage","kennzeichen":"A-HZG"}""");
        Gut(p, "system_paar_anlegen", """{"systemtyp":"Heizung Vorlauf","anlage":"A-HZG"}""");
        Strang(p, "HZ-V", "HZ-VL");
        Strang(p, "HZ-R", "HZ-RL");
        Punkt(p, "Verteiler", "VT-1", null, 0, 0, 0);
        Punkt(p, "TStueck", "TS-A", "HZ-V", 1000, 0, 500);
        Punkt(p, "TStueck", "TS-B", "HZ-V", 2000, 0, 500);
        Punkt(p, "Pumpe", "P-1", "HZ-V", 3000, 0, 500);
        Punkt(p, "Heizkoerper", "HK-1", null, 4000, 0, 500, Leistung(700));
        Rohr(p, "R-1", "HZ-V", "VT-1.vl_1", "TS-A.a");
        Rohr(p, "R-2", "HZ-V", "TS-A.b", "TS-B.a");
        Rohr(p, "R-3", "HZ-V", "TS-A.c", "TS-B.c");
        Rohr(p, "R-4", "HZ-V", "TS-B.b", "P-1.saug");
        Rohr(p, "R-5", "HZ-V", "P-1.druck", "HK-1.vl");
        Rohr(p, "R-6", "HZ-R", "HK-1.rl", "VT-1.rl_1");
        Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1"}""");
        return p;
    }
}
