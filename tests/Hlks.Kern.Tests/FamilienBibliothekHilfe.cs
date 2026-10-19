using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Nachtrag B7: Tests der Familienbibliothek setzen HLKS_FAMILIENBIBLIOTHEK (und die Warteschlange) auf einen
/// eigenen Ordner unter %TEMP% – darum laufen sie nicht parallel zu anderen Tests (Umgebungsvariablen gelten im ganzen Prozess).</summary>
[CollectionDefinition("Familienbibliothek", DisableParallelization = true)]
public sealed class FamilienBibliothekSammlung;

/// <summary>Grundlage der Bibliothekstests: je Test ein eigener Ordner %TEMP%\hlks-b7-…\ mit nas\bibliothek (Bibliothek), lokal\
/// (Warteschlange) und projekte\ (Projektablagen). Zeigt die Bibliothek nicht dorthin, bricht der Test ab, statt auf W: zu
/// schreiben. Am Ende werden die Umgebungsvariablen zurückgesetzt und der Ordner gelöscht. Alle Familien sind FIKTIV.</summary>
public abstract class BibliothekTestBasis : IDisposable
{
    public const string HkId = "fam-test-heizkoerper";

    protected readonly string Wurzel;
    protected readonly string NasOrdner;
    protected readonly string Bib;
    readonly string? altBib, altQ;

    protected BibliothekTestBasis()
    {
        Wurzel = Path.Combine(Path.GetTempPath(), "hlks-b7-" + Guid.NewGuid().ToString("N")[..10]);
        NasOrdner = Path.Combine(Wurzel, "nas");
        Bib = Path.Combine(NasOrdner, "bibliothek");
        Directory.CreateDirectory(NasOrdner);
        altBib = Environment.GetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable);
        altQ = Environment.GetEnvironmentVariable(FamilienBibliothekDienst.WarteschlangeVariable);
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, Bib);
        Rechner("pc-a");
        FamilienBibliothekDienst.SperreWarten = TimeSpan.FromSeconds(10);
        // Abbruch statt Schreiben auf W: bzw. in die echte Bibliothek
        var ort = FamilienBibliothekDienst.Ort(Kat);
        if (!ort.Aktiv || !string.Equals(ort.Pfad, Bib, StringComparison.OrdinalIgnoreCase) || FamilienBibliothekDienst.AufNetz(ort.Pfad!)
            || !FamilienBibliothekDienst.UnterTemp(ort.Pfad!) || !FamilienBibliothekDienst.UnterTemp(FamilienBibliothekDienst.WarteschlangeWurzel()))
        {
            Dispose();
            throw new InvalidOperationException($"Abbruch: die Familienbibliothek zeigt nicht auf den Testordner ({ort.Pfad}, {ort.Sperre}) – es wird nie auf W: geschrieben.");
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, altBib);
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.WarteschlangeVariable, altQ);
        FamilienBibliothekDienst.SperreWarten = TimeSpan.FromSeconds(10);
        if (Directory.Exists(NasOrdner + "_weg") && !Directory.Exists(NasOrdner)) Directory.Move(NasOrdner + "_weg", NasOrdner);
        for (int i = 0; i < 10 && Directory.Exists(Wurzel); i++)
        {
            try { Directory.Delete(Wurzel, true); }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { Thread.Sleep(100); }
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Warteschlange je «Rechner» (lokal je Gerät): eigener Ordner unter Wurzel\lokal.</summary>
    protected void Rechner(string name) =>
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.WarteschlangeVariable, Path.Combine(Wurzel, "lokal", name));

    protected BibliotheksOrt Ort => FamilienBibliothekDienst.Ort(Kat);

    /// <summary>Neues Projekt mit Ablage unter Wurzel\projekte, Gebäude GEB und Geschoss EG.</summary>
    protected Projekt NeuesProjekt(string name, string akteurArt = "mensch", string geraet = "PC-A")
    {
        var p = Hlks.Kern.Projekt.Anlegen(Kat, new OrdnerSpeicher(Path.Combine(Wurzel, "projekte", Kurz(name)), TimeSpan.FromMilliseconds(1)), name,
            akteurArt == "ki" ? "KI" : "Christian", geraet, akteurArt);
        if (akteurArt is "mensch" or "ki")
        {
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" }));
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 3000 },
            }));
        }
        return p;
    }

    /// <summary>Dasselbe Projekt auf einem zweiten Rechner öffnen (gleiche Ablage).</summary>
    protected Projekt Oeffnen(string name, string geraet, string akteur = "Martin") =>
        Hlks.Kern.Projekt.Oeffnen(Kat, new OrdnerSpeicher(Path.Combine(Wurzel, "projekte", Kurz(name)), TimeSpan.FromMilliseconds(1)), akteur, geraet);

    static string Kurz(string name) => FamilienBibliothekLogik.Sauber(name, 30);

    protected BibIndex Index() => JsonSerializer.Deserialize<BibIndex>(File.ReadAllBytes(Path.Combine(Bib, "index.json")), Json.Optionen)!;

    protected BibIndexEintrag Eintrag(string id = HkId) => Index().Familie(id) ?? throw new Xunit.Sdk.XunitException($"{id} fehlt im Index");

    protected string FamOrdner(string id = HkId) => Path.Combine(Bib, Eintrag(id).Ordner.Replace('/', Path.DirectorySeparatorChar));

    protected BibLog Log(string id = HkId) => FamilienBibliothekDienst.LogLesen(FamOrdner(id))!;

    protected string[] Versionsdateien(string id = HkId) =>
        Directory.GetFiles(Path.Combine(FamOrdner(id), "versionen"), "*.familie.json").Select(Path.GetFileName).Order().ToArray()!;

    protected JsonObject VersionLesen(int n, string id = HkId) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(FamOrdner(id), "versionen", $"v{n:D4}.familie.json")))!;

    /// <summary>bibliothekStand einer Familie aus der Abfrage familien.</summary>
    protected static JsonObject Stand(Projekt p, string kz = "FAM-1") =>
        ((JsonObject)Tuer.Abfrage(p, "familien"))["familien"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["kennzeichen"]!.GetValue<string>() == kz)["bibliothekStand"]!.AsObject();

    protected static string Art(Projekt p, string kz = "FAM-1") => Stand(p, kz)["art"]!.GetValue<string>();

    protected static JsonObject Bibliothek(Projekt p, JsonObject? par = null) => (JsonObject)Tuer.Abfrage(p, "familien_bibliothek", par ?? []);

    protected static JsonObject? BibEintrag(Projekt p, string id = HkId, JsonObject? par = null) =>
        Bibliothek(p, par)["familien"]!.AsArray().Select(x => x!.AsObject()).FirstOrDefault(x => x["id"]?.GetValue<string>() == id && x["quelle"]!.GetValue<string>() == "bibliothek");

    protected static JsonObject Marker(Projekt p, string kz = "FAM-1") => (JsonObject)Hole(p, kz).Eigenschaft("bibliothek")!.Inhalt!;

    protected static JsonObject Aendern(string familie, params JsonObject[] ops) => new() { ["familie"] = familie, ["ops"] = new JsonArray(ops) };

    protected static JsonObject Kopf(string feld, string wert) => new() { ["op"] = "kopf_setzen", [feld] = wert };

    protected static JsonObject ParameterWert(string name, double wert, string gruppe = "anschluss") => new()
    {
        ["op"] = "parameter_setzen",
        ["parameter"] = new JsonObject { ["name"] = name, ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = gruppe, ["wert"] = wert },
    };

    protected void NasWeg() => Directory.Move(NasOrdner, NasOrdner + "_weg");
    protected void NasDa() => Directory.Move(NasOrdner + "_weg", NasOrdner);

    /// <summary>Keine temporären Dateien und keine Sperren bleiben liegen.</summary>
    protected void Sauber()
    {
        Assert.Empty(Directory.GetFiles(Bib, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Bib, ".sperre", SearchOption.AllDirectories));
    }
}
