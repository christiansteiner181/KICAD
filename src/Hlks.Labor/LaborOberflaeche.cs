// Oberfläche des XR-Labors (L0): eigene Seite aus src/Hlks.Labor/wwwroot, dazu Stil, Symbole und der Teil «Quest App» aus der KICAD-Oberfläche – als zweiter
// Wurzelordner, nicht als Kopie (entwurf/labor-app.md, Leitlinie 4 «Nichts doppelt bauen»).
//
// Labor-Wurzel: HLKS_LABOR_WEBROOT (der Desktop startet aus einer Arbeitskopie), sonst der Quellordner beim Bau (Entwicklung, Änderungen ohne Neubau), sonst die
// Kopie neben dem Programm (labor_wwwroot).
// KICAD-Wurzel: Hlks.Web\wwwroot DERSELBEN Arbeitskopie wie die Labor-Wurzel (…\src\Hlks.Labor\wwwroot → …\src\Hlks.Web\wwwroot; so passen beide Stände
// zusammen), sonst der Quellordner von Hlks.Web beim Bau, sonst die Kopie neben dem Programm (wwwroot; der Verweis auf Hlks.Web legt sie dorthin).
// Aus der KICAD-Wurzel gibt es nur die Ordner css/ und js/; KICADs Seiten (index.html, vr.html, Symbole der VR-Seite, Bibliotheken) liefert das Labor nicht aus.
// Was in der Labor-Wurzel liegt, geht vor. Nur gelesen, keine Dateiwächter (Befund R 19.10.2026, Hlks.Web/Program.cs).

using System.Reflection;
using Hlks.Web;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Hlks.Labor;

public static class LaborOberflaeche
{
    /// <summary>Umgebungsvariable mit dem Ordner der Labor-Oberfläche (muss index.html enthalten).</summary>
    public const string WebRootVariable = "HLKS_LABOR_WEBROOT";
    /// <summary>Ordnername der Kopie der Labor-Oberfläche neben dem Programm (Hlks.Labor.csproj).</summary>
    public const string KopieOrdner = "labor_wwwroot";
    /// <summary>Diese Ordner der KICAD-Oberfläche liefert das Labor mit aus (Stil, Symbole, Teil «Quest App» mit seinen Modulen).</summary>
    public static readonly IReadOnlyList<string> KicadOrdner = ["css", "js"];

    /// <summary>Ordner der Labor-Oberfläche für diesen Lauf.</summary>
    public static string LaborWurzel() =>
        LaborWurzel(Environment.GetEnvironmentVariable(WebRootVariable), Quelle(typeof(LaborOberflaeche).Assembly), AppContext.BaseDirectory);

    public static string LaborWurzel(string? umgebung, string? quelle, string programmOrdner)
    {
        if (!string.IsNullOrWhiteSpace(umgebung) && File.Exists(Path.Combine(umgebung, "index.html"))) return Path.GetFullPath(umgebung);
        if (!string.IsNullOrWhiteSpace(quelle) && File.Exists(Path.Combine(quelle, "index.html"))) return Path.GetFullPath(quelle);
        return Path.Combine(programmOrdner, KopieOrdner);
    }

    /// <summary>Ordner der KICAD-Oberfläche, aus dem css/ und js/ kommen (siehe Kopf).</summary>
    public static string KicadWurzel(string laborWurzel) => KicadWurzel(laborWurzel, Quelle(typeof(XrNetzDienst).Assembly), AppContext.BaseDirectory);

    public static string KicadWurzel(string laborWurzel, string? kicadQuelle, string programmOrdner)
    {
        var geschwister = Path.GetFullPath(Path.Combine(laborWurzel, "..", "..", "Hlks.Web", "wwwroot"));
        if (File.Exists(Path.Combine(geschwister, "index.html"))) return geschwister;
        if (!string.IsNullOrWhiteSpace(kicadQuelle) && File.Exists(Path.Combine(kicadQuelle, "index.html"))) return Path.GetFullPath(kicadQuelle);
        return Path.Combine(programmOrdner, "wwwroot");
    }

    static string? Quelle(Assembly a) =>
        a.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == "QuellWebRoot")?.Value;

    /// <summary>Startseite und statische Dateien: zuerst die Labor-Wurzel, dann css/ und js/ der KICAD-Wurzel. Wie KICAD ohne Zwischenspeicher im Browser (no-cache).</summary>
    public static void Ausliefern(WebApplication app, string laborWurzel)
    {
        var anbieter = new List<IFileProvider>();
        PhysicalFileProvider? labor = Directory.Exists(laborWurzel) ? new PhysicalFileProvider(laborWurzel) : null;
        if (labor is not null) anbieter.Add(labor);
        var kicadOrdner = KicadWurzel(laborWurzel);
        PhysicalFileProvider? kicad = Directory.Exists(kicadOrdner) ? new PhysicalFileProvider(kicadOrdner) : null;
        if (kicad is not null) anbieter.Add(new NurOrdner(kicad, KicadOrdner));
        if (anbieter.Count == 0)
        {
            Console.Error.WriteLine($"XR-Labor: keine Oberfläche gefunden ({laborWurzel}).");
            return;
        }
        app.Lifetime.ApplicationStopped.Register(() => { labor?.Dispose(); kicad?.Dispose(); });
        if (labor is not null) app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = labor });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new CompositeFileProvider(anbieter),
            OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache",
        });
    }
}

/// <summary>Gibt aus einem Anbieter nur Dateien in den genannten obersten Ordnern heraus (keine Verzeichnislisten, kein «..», kein «\»).</summary>
public sealed class NurOrdner(IFileProvider innen, IReadOnlyList<string> ordner) : IFileProvider
{
    /// <summary>Liegt der Pfad (z. B. «/css/hlks.css») in einem der Ordner? Pfade mit «..», «\» oder «:» gelten nie.</summary>
    public static bool Erlaubt(string? pfad, IReadOnlyList<string> ordner)
    {
        if (string.IsNullOrEmpty(pfad) || pfad.Contains("..", StringComparison.Ordinal) || pfad.Contains('\\') || pfad.Contains(':')) return false;
        var p = pfad.TrimStart('/');
        var schnitt = p.IndexOf('/');
        if (schnitt <= 0 || schnitt == p.Length - 1) return false;
        return ordner.Contains(p[..schnitt], StringComparer.OrdinalIgnoreCase);
    }

    public IFileInfo GetFileInfo(string subpath) => Erlaubt(subpath, ordner) ? innen.GetFileInfo(subpath) : new NotFoundFileInfo(subpath);
    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
}
