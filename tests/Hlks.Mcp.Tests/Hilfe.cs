using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Mcp.Tests;

/// <summary>Gemeinsame Hilfen: Katalog, Beispielprojekt, temporäre Ordner, Prozessstart der gebauten Hlks.Mcp.dll.</summary>
static class Hilfe
{
    public static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    public const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";

    public static Projekt Beispiel() => Beispielprojekt.Erzeugen(Kat, null);

    /// <summary>Datei aus HLKS_CAD/tests/daten (kleine IFC-Testdateien, Modul 1b).</summary>
    public static string Testdaten(string name)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "tests", "daten", name);
            if (File.Exists(p)) return p;
            var q = Path.Combine(d.FullName, "HLKS_CAD", "tests", "daten", name);
            if (File.Exists(q)) return q;
        }
        var bekannt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Claude_Code", "HLKS_CAD", "tests", "daten", name);
        return File.Exists(bekannt) ? bekannt : throw new FileNotFoundException("Testdatei nicht gefunden: " + name);
    }

    public static JsonNode Json(string text) => JsonNode.Parse(text) ?? throw new Xunit.Sdk.XunitException("Antwort ist kein JSON: " + text);

    /// <summary>Neuer Temp-Ordner im Stil der NAS-Ablage (mit Umlaut und Leerzeichen); Wurzel zum Aufräumen.</summary>
    public static (string Wurzel, string Projekt) TempProjektOrdner()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_mcp_" + Guid.NewGuid().ToString("N")[..10]);
        return (wurzel, Path.Combine(wurzel, "c_Pläne und Modelle", "e_KICAD"));
    }

    public static void Aufraeumen(string wurzel)
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    /// <summary>Pfad der gebauten Hlks.Mcp.dll (liegt mit runtimeconfig neben den Testdateien).</summary>
    public static string Dll()
    {
        var neben = Path.Combine(AppContext.BaseDirectory, "Hlks.Mcp.dll");
        if (File.Exists(neben) && File.Exists(Path.ChangeExtension(neben, ".runtimeconfig.json"))) return neben;
        // Ausgabe des Hlks.Mcp-Projekts: …\Hlks.Mcp.Tests\bin\<Konfig>\net10.0\ -> …\Hlks.Mcp\bin\<Konfig>\net10.0\
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('\\', '/'));
        var konfig = tfm.Parent!;
        var bin = konfig.Parent!.Parent!.Parent!;
        var kandidat = Path.Combine(bin.FullName, "Hlks.Mcp", "bin", konfig.Name, tfm.Name, "Hlks.Mcp.dll");
        return File.Exists(kandidat) ? kandidat : throw new FileNotFoundException("Hlks.Mcp.dll nicht gefunden: " + kandidat);
    }

    static ProcessStartInfo Start(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(Dotnet)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, StandardInputEncoding = new UTF8Encoding(false),
            UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Dll());
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    /// <summary>Einmal-Modus: Prozess laufen lassen, stdout (UTF-8) und Exit-Code liefern.</summary>
    public static (int Code, string Aus, string Fehler) Lauf(params string[] args)
    {
        using var p = Process.Start(Start(args))!;
        p.StandardInput.Close();
        var aus = p.StandardOutput.ReadToEndAsync();
        var fehler = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(120_000)) { p.Kill(true); throw new TimeoutException("Hlks.Mcp " + string.Join(' ', args)); }
        return (p.ExitCode, aus.Result, fehler.Result);
    }

    public static Process ServerStarten(params string[] args) => Process.Start(Start(args))!;

    /// <summary>Server mit zusätzlichen bzw. gelöschten (null) Umgebungsvariablen nur für diesen Prozess (z. B. HLKS_HKAP_URL auf einen freien Port).</summary>
    public static Process ServerStarten(IReadOnlyDictionary<string, string?> umgebung, params string[] args)
    {
        var psi = Start(args);
        foreach (var (k, v) in umgebung) { if (v is null) psi.Environment.Remove(k); else psi.Environment[k] = v; }
        return Process.Start(psi)!;
    }
}
