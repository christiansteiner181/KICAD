// HLKS-CAD: MCP-Server (stdio) und Kommandozeile über dem Kern.
//
// Server:     Hlks.Mcp --projekt <e_KICAD-Ordner> [--katalog <ordner>] [--akteur <name>] [--geraet <name>] [--art mensch|ki]
// Einmalig:   --beispiel <ordner> | --ki-auftrag | --ki-erwartung | --ki-bewerten <antwort.json>
//             | --abfrage <name> [json] | --befehl <name> <json> [--trockenlauf]      (alle ausser --beispiel/--ki-auftrag mit --projekt)
// Exit-Codes: 0 ok, 1 Fehler/abgelehnt, 2 KI-Test nicht bestanden.

using System.Text;
using Hlks.Kern;
using Hlks.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var argumente = new Argumente(args);

if (Kommandozeile.IstEinmalModus(argumente))
{
    var utf8 = new UTF8Encoding(false);
    Console.OutputEncoding = utf8;
    using var aus = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
    return Kommandozeile.Ausfuehren(argumente, aus);
}

if (!argumente.Hat("--projekt"))
{
    Console.Error.WriteLine("Fehler: --projekt <e_KICAD-Ordner> fehlt. Einmal-Modi: --beispiel, --ki-auftrag, --ki-erwartung, --ki-bewerten, --abfrage, --befehl.");
    return 1;
}

Projekt projekt;
try { projekt = Kommandozeile.Oeffnen(argumente); }
catch (Exception ex)
{
    Console.Error.WriteLine("Fehler: " + ex.Message + (ex is HlksFehler { Vorschlag: { } v } ? "\nVorschlag: " + v : ""));
    return 1;
}

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // stdout gehört dem MCP-Protokoll
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.Services.AddSingleton(projekt);
    builder.Services
        .AddMcpServer(o =>
        {
            o.ServerInfo = new() { Name = "hlks", Version = "0.1.0" };
            o.ServerInstructions = Werkzeuge.Anleitung;
        })
        .WithStdioServerTransport()
        .WithTools<Werkzeuge>();
    await builder.Build().RunAsync();
    return 0;
}
finally { projekt.Freigeben(); }
