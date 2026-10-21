using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der IFC-Tests (Modul 1b): Testdateien, echte Dateien (nur wenn vorhanden), leeres Projekt.</summary>
static class IfcHilfe
{
    public static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    /// <summary>Kleine Testdatei aus HLKS_CAD/tests/daten (minimal_ifc4.ifc, minimal_ifc2x3.ifc: Projekt in mm, Gebäude,
    /// Erdgeschoss Kote 0, 1. Obergeschoss Kote 3000, Wand 5 m × 200 mm × 2.6 m mit Achse (0,0)→(5000,0) im EG,
    /// Raum 1.01 «Wohnen» 4 × 3 m, 2.5 m hoch).</summary>
    public static string Testdaten(string name)
    {
        // Arbeitskopie zuerst (Agent-Worktree, Prüfstand stand_pruefen.ps1): HLKS_KATALOG zeigt auf <Arbeitskopie>\kataloge – deren
        // tests\daten gilt vor dem OneDrive-Hauptordner, sonst prüfen die Tests neue Testdateien einer Arbeitskopie nicht
        if (Arbeitskopie() is { } ak && Path.Combine(ak, "tests", "daten", name) is var pa && File.Exists(pa)) return pa;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            foreach (var p in new[] { Path.Combine(d.FullName, "tests", "daten", name), Path.Combine(d.FullName, "HLKS_CAD", "tests", "daten", name) })
                if (File.Exists(p)) return p;
        }
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "tests", "daten", name);
        return File.Exists(bekannt) ? bekannt : throw new FileNotFoundException("Testdatei nicht gefunden: " + name);
    }

    /// <summary>Wurzel der Arbeitskopie aus HLKS_KATALOG (= «&lt;Arbeitskopie&gt;\kataloge», gesetzt für Läufe in einer eigenen
    /// Arbeitskopie) oder null.</summary>
    public static string? Arbeitskopie()
    {
        var kat = Environment.GetEnvironmentVariable("HLKS_KATALOG");
        if (string.IsNullOrWhiteSpace(kat)) return null;
        var voll = Path.GetFullPath(kat).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetDirectoryName(voll) is { } w && Directory.Exists(Path.Combine(w, "tests", "daten")) ? w : null;
    }

    /// <summary>Echte Projektdatei aus %USERPROFILE%\HLKS_CAD_testdaten (Bernstrasse 42, BKW Hauptsitz) oder null, wenn
    /// sie auf diesem Rechner fehlt – Tests damit kehren dann früh zurück (xunit 2 kennt kein Überspringen zur Laufzeit).</summary>
    public static string? Echt(string relativ)
    {
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", relativ);
        return File.Exists(p) ? p : null;
    }

    public static Projekt Leer() => Projekt.Anlegen(Kat, null, "Test IFC", "T", "pc");

    public static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} | {e.Vorschlag}");
        return e;
    }

    public static BefehlsErgebnis Uebernehmen(Projekt p, string datei, JsonObject? weitere = null, bool trocken = false)
    {
        var par = new JsonObject { ["datei"] = datei };
        foreach (var (k, v) in weitere ?? []) par[k] = v?.DeepClone();
        return p.Ausfuehren("ifc_uebernehmen", par, trocken);
    }
}
