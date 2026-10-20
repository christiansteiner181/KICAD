using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Tests zu Modul 4 Teil A (Sanitär-Belastungswerte): kleine Sanitärnetze über die Befehle des Kerns. Alle Lagen und
/// Grössen sind FIKTIV (Testnetze ohne Bezug zu einem Objekt); die Belastungswerte kommen allein aus dem Katalog sanitaer_belastungswerte.json.
/// Abflusskennzahlen in Tests tragen die Quelle «Testwert» – sie sind keine Normwerte.</summary>
static class SanitaerHilfe
{
    public const double Toleranz = 1e-9;

    public static SanitaerKatalog SKat => Kat.Sanitaer ?? throw new Xunit.Sdk.XunitException("Katalog sanitaer_belastungswerte.json fehlt");

    /// <summary>Testkopie des Katalogordners unter %TEMP%\HLKS_CAD mit rechnung.kurvenwahl.bestaetigt = false (ohne «bestaetigung») – der Stand vor
    /// Christians Bestätigung vom 20.10.2026. Damit bleibt der Fall «Kurvenwahl nicht bestätigt → Hinweis» (Prüfbericht 4A B3) geprüft, obwohl der
    /// echte Katalog jetzt bestätigt ist. Sonst unverändert; einmal je Testlauf angelegt und am Ende gelöscht.</summary>
    public static Katalog KatalogUnbestaetigt => unbestaetigt.Value;

    static readonly Lazy<Katalog> unbestaetigt = new(() =>
    {
        var ziel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"kat_kurvenwahl_unbestaetigt_{Environment.ProcessId}");
        if (Directory.Exists(ziel)) Directory.Delete(ziel, true);
        var quelle = Katalog.StandardOrdner();
        foreach (var d in Directory.GetFiles(quelle, "*", SearchOption.AllDirectories))
        {
            var neu = Path.Combine(ziel, Path.GetRelativePath(quelle, d));
            Directory.CreateDirectory(Path.GetDirectoryName(neu)!);
            File.Copy(d, neu);
        }
        var datei = Path.Combine(ziel, SanitaerKatalog.Datei);
        var roh = (JsonObject)JsonNode.Parse(File.ReadAllText(datei))!;
        var kw = (JsonObject)roh["rechnung"]!["kurvenwahl"]!;
        kw["bestaetigt"] = false;
        kw.Remove("bestaetigung");
        File.WriteAllText(datei, roh.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(ziel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
        var kat = Katalog.Laden(ziel);
        Assert.False(kat.Sanitaer!.Kurvenwahl.Bestaetigt);
        return kat;
    });

    static int lauf;

    /// <summary>Projekt mit GEB, EG (Kote 0) und OG (Kote 3000), Anlage A-1 mit den Systemen der Medien (KW, WW, SW, ZIR, RW möglich) und je
    /// einem Strang «<Medium>-1». k = Abflusskennzahl der Anlage (Quelle Testwert) oder null.</summary>
    public static Projekt Netzprojekt(double? k = null, params string[] medien) => NetzprojektMit(Kat, k, medien);

    /// <summary>Wie Netzprojekt, aber mit dem angegebenen Katalog (z. B. KatalogUnbestaetigt).</summary>
    public static Projekt NetzprojektMit(Katalog katalog, double? k = null, params string[] medien)
    {
        var p = Neu(katalog: katalog);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        var an = new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-1", ["name"] = "Testanlage (fiktiv)" };
        if (k is double kw) an["eigenschaften"] = new JsonObject { ["abflusskennzahl"] = new JsonObject { ["wert"] = kw, ["quelle"] = "Testwert (fiktiv)" } };
        Ok(p.Ausfuehren("element_anlegen", an));
        foreach (var m in medien.Length == 0 ? ["KW", "WW", "SW"] : medien) SystemMitStrang(p, m, "A-1");
        return p;
    }

    public static readonly Dictionary<string, string> Systemtyp = new()
    {
        ["KW"] = "Trinkwasser kalt", ["WW"] = "Trinkwasser warm", ["SW"] = "Schmutzwasser", ["ZIR"] = "Zirkulation", ["RW"] = "Regenwasser", ["HZ-VL"] = "Heizung Vorlauf",
    };

    /// <summary>System «<medium>» (teilVon anlage, wenn angegeben) mit Strang «<medium>-1».</summary>
    public static void SystemMitStrang(Projekt p, string medium, string? anlage)
    {
        var s = new JsonObject { ["klasse"] = "System", ["kennzeichen"] = medium, ["eigenschaften"] = new JsonObject { ["systemtyp"] = Systemtyp[medium] } };
        if (anlage is not null) s["teilVon"] = anlage;
        Ok(p.Ausfuehren("element_anlegen", s));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = medium + "-1", ["teilVon"] = medium }));
    }

    /// <summary>Apparat ohne Lage (nur Geschoss); art = sanitaerArt oder null (Vorgabe der Klasse).</summary>
    public static void Apparat(Projekt p, string klasse, string kz, string? art = null, string geschoss = "EG")
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz, ["geschoss"] = geschoss };
        if (art is not null) o["eigenschaften"] = new JsonObject { ["sanitaerArt"] = art };
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    /// <summary>Formteil (Klasse, Vorgabe T-Stück) im Strang, ohne Familie: Anschlüsse ohne Lage, es wird logisch verbunden.</summary>
    public static void Formteil(Projekt p, string kz, string strang, string klasse = "TStueck", JsonArray? anschluesse = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz, ["teilVon"] = strang, ["geschoss"] = "EG" };
        if (anschluesse is not null) o["anschluesse"] = anschluesse;
        Ok(p.Ausfuehren("element_anlegen", o));
    }

    /// <summary>Rohr von «KZ.anschluss» nach «KZ.anschluss» (Fliessrichtung von → nach) im Strang; Verlauf fiktiv.</summary>
    public static void Rohr(Projekt p, string kz, string strang, string? von, string? nach)
    {
        int i = Interlocked.Increment(ref lauf);
        var o = new JsonObject
        {
            ["kennzeichen"] = kz, ["strang"] = strang, ["dn"] = 20,
            ["punkte"] = Pts([i * 10.0, 0, 100], [i * 10.0, 500, 100]),
        };
        if (von is not null) o["von"] = von;
        if (nach is not null) o["nach"] = nach;
        Ok(p.Ausfuehren("leitung_ziehen", o));
    }

    public static void Hausanschluss(Projekt p, string kz = "HA-1") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Hausanschluss", ["kennzeichen"] = kz, ["system"] = "KW", ["geschoss"] = "EG" }));

    public static void Kontrollschacht(Projekt p, string kz = "KS-1") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Kontrollschacht", ["kennzeichen"] = kz, ["system"] = "SW", ["geschoss"] = "EG" }));

    public static void Wassererwaermer(Projekt p, string kz = "WE-1") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Wassererwaermer", ["kennzeichen"] = kz, ["geschoss"] = "EG" }));

    /// <summary>Verteiler als Formteil mit eigenen Anschlüssen: «a» und «v1» … «vn», alle ungerichtet (wie ein Etagenverteiler).</summary>
    public static void Verteiler(Projekt p, string kz, string strang, int n) =>
        Formteil(p, kz, strang, "TStueck", new JsonArray(new[] { "a" }.Concat(Enumerable.Range(1, n).Select(i => $"v{i}"))
            .Select(x => (JsonNode)new JsonObject { ["name"] = x, ["medium"] = "*", ["fluss"] = "beide" }).ToArray()));

    public static JsonObject Abfrage(Projekt p, string name, JsonObject? par = null) => (JsonObject)Tuer.Abfrage(p, name, par ?? []);

    public static JsonObject Belastung(Projekt p, string leitung) => Abfrage(p, "sanitaer_belastung", new JsonObject { ["leitung"] = leitung });

    public static double Zahl(JsonNode? n) => n?.GetValue<double>() ?? throw new Xunit.Sdk.XunitException("Zahl fehlt");

    public static void Etwa(double erwartet, double ist, string was, double tol = Toleranz) =>
        Assert.True(Math.Abs(erwartet - ist) <= tol, $"{was}: erwartet {erwartet:R}, ist {ist:R} (Toleranz {tol})");

    public static List<Befund> Sanitaerbefunde(Projekt p) =>
        Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code is "P21" or "P22" or "P23" or "P24" or "P25" or "P26").ToList();

    public static string Liste(IEnumerable<Befund> b) => string.Join("; ", b.Select(x => $"{x.Code} {x.Element}: {x.Text}"));
}
