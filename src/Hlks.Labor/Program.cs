// XR-Labor (Labor-App), PC-Teil: lokaler Web-Server (nur localhost) mit der Labor-Seite im Browser (entwurf/labor-app.md, Stufe L0).
// Aufruf: Hlks.Labor [--port 5430] [--katalog <ordner>]
// Läuft neben KICAD (5130) und ohne KICAD-Projekt. Dienste, Ports, Ablage und Endpunkte: LaborDienste.cs; Oberfläche: LaborOberflaeche.cs.
// Der Einstiegspunkt heisst LaborProgramm (nicht Program): Hlks.Web.dll, auf die das Labor verweist, bringt schon «Program» mit (Entscheid in LaborDienste.cs).

using Hlks.Kern;
using Hlks.Web;

namespace Hlks.Labor;

public sealed class LaborProgramm
{
    /// <summary>Port der Labor-Seite, wenn --port fehlt (frei laut Suche im Repository am 26.10.2026; KICAD belegt 5130–5182).</summary>
    public const int PortVorgabe = LaborXr.DesktopPort;

    public static async Task Main(string[] args)
    {
        string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

        int port = int.TryParse(Arg("--port"), out var pt) && pt is >= 1 and <= 65535 ? pt : PortVorgabe;
        // Akteur und Gerät für die Benutzereinstellungen (gelesen: quest.adb, quest.app_ablage, Symbolsatz); Kataloge wie KICAD (--katalog, sonst HLKS_KATALOG, …)
        var einstellungen = new Einstellungen(Akteur: Environment.UserName, Geraet: Environment.MachineName, KatalogOrdner: Arg("--katalog") ?? Katalog.StandardOrdner(), StartProjekt: null);

        // Wie Hlks.Web/Program.cs: keine Dateiwächter auf Quell- bzw. OneDrive-Ordnern (Befund R 19.10.2026) – Konfiguration ohne Neuladen bei Änderung,
        // Inhaltswurzel im Programmordner statt im Arbeitsverzeichnis; die Oberfläche wird nur gelesen.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = LaborOberflaeche.LaborWurzel(), ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSingleton(einstellungen);
        builder.Services.AddLabor(port);
        var app = builder.Build();
        app.MapLabor();

        Console.WriteLine($"XR-Labor läuft auf http://localhost:{port}  (Netzzugang der Labor-Brille {LaborXr.ZugangPort}/{LaborXr.SuchPort}, wenn eingeschaltet)");
        await app.RunAsync();
    }
}
