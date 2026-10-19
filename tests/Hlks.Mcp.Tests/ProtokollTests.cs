using System.Diagnostics;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Rauchtest über das echte MCP-Protokoll (JSON-RPC, eine Nachricht je Zeile über stdin/stdout).</summary>
public class ProtokollTests
{
    sealed class Gegenstelle(Process prozess)
    {
        int naechsteId;
        readonly List<string> rohzeilen = [];
        public IReadOnlyList<string> Rohzeilen => rohzeilen;

        public void Senden(JsonObject n)
        {
            prozess.StandardInput.WriteLine(n.ToJsonString());
            prozess.StandardInput.Flush();
        }

        public JsonObject Anfrage(string methode, JsonObject? parameter = null)
        {
            int id = ++naechsteId;
            Senden(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = methode, ["params"] = parameter ?? new JsonObject() });
            while (true)
            {
                var lesen = prozess.StandardOutput.ReadLineAsync();
                if (!lesen.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException($"Keine Antwort auf {methode}. Gesehen: {string.Join(" | ", rohzeilen)}");
                var zeile = lesen.Result ?? throw new EndOfStreamException($"Server hat nach {methode} beendet. Gesehen: {string.Join(" | ", rohzeilen)}");
                rohzeilen.Add(zeile);
                var antwort = JsonNode.Parse(zeile)!.AsObject();
                if (antwort["id"] is { } aid && aid.GetValue<int>() == id) return antwort;      // sonst Benachrichtigung/Anfrage des Servers
            }
        }
    }

    [Fact]
    public void Server_ListetAlleWerkzeugeUndBeantwortetProjektUeberblick()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        Process? server = null;
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            server = ServerStarten("--projekt", ordner, "--akteur", "Rauchtest", "--art", "ki", "--geraet", "test");
            var g = new Gegenstelle(server);
            var stderr = server.StandardError.ReadToEndAsync();

            var init = g.Anfrage("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "rauchtest", ["version"] = "1" },
            });
            var info = init["result"]!["serverInfo"]!;
            Assert.Equal("hlks", info["name"]!.GetValue<string>());
            Assert.Equal("0.1.0", info["version"]!.GetValue<string>());
            Assert.Contains("Systemgraph", init["result"]!["instructions"]!.GetValue<string>());
            g.Senden(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });

            var namen = new List<string>();
            string? cursor = null;
            do
            {
                var liste = g.Anfrage("tools/list", cursor is null ? null : new JsonObject { ["cursor"] = cursor });
                namen.AddRange(liste["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()));
                cursor = liste["result"]!["nextCursor"]?.GetValue<string>();
            } while (cursor is not null);
            var erwartet = Abfragen.Liste.Select(a => a.Name).Concat(Projekt.Anlegen(Kat, null, "x", "x", "x").Befehle.Alle.Select(b => b.Name))
                .Concat(["rueckgaengig", "wiederholen", "synchronisieren", "kontext_bild"]).Order().ToList();   // kontext_bild: Sitzungswerkzeug (Modul 3c)
            Assert.Equal(erwartet, namen.Order().ToList());

            var aufruf = g.Anfrage("tools/call", new JsonObject { ["name"] = "projekt_ueberblick", ["arguments"] = new JsonObject() });
            var inhalt = aufruf["result"]!["content"]!.AsArray().Select(c => c!["text"]!.GetValue<string>()).First();
            Assert.Contains("MFH Musterweg", inhalt);
            Assert.NotEqual(true, aufruf["result"]!["isError"]?.GetValue<bool>());

            // Fehler kommen als Text mit «fehler», nicht als Protokollfehler
            var fehler = g.Anfrage("tools/call", new JsonObject
            {
                ["name"] = "element_lesen", ["arguments"] = new JsonObject { ["element"] = "GIBT-ES-NICHT" },
            });
            Assert.Contains("vorschlag", fehler["result"]!["content"]![0]!["text"]!.GetValue<string>());

            // Befehl über das Protokoll mit Objektparameter und Trockenlauf
            var tl = g.Anfrage("tools/call", new JsonObject
            {
                ["name"] = "element_aendern",
                ["arguments"] = new JsonObject
                {
                    ["element"] = "E.02", ["trockenlauf"] = true,
                    ["eigenschaften"] = new JsonObject { ["nutzung"] = "Büro" },
                },
            });
            var tlText = JsonNode.Parse(tl["result"]!["content"]![0]!["text"]!.GetValue<string>())!;
            Assert.True(tlText["trockenlauf"]!.GetValue<bool>(), tlText.ToJsonString());

            // sauber beenden: stdin schliessen
            server.StandardInput.Close();
            Assert.True(server.WaitForExit(20_000), "Server beendet sich nicht nach Schliessen von stdin");
            Assert.Equal(0, server.ExitCode);
            Assert.DoesNotContain(g.Rohzeilen, z => !z.TrimStart().StartsWith('{'));      // stdout nur Protokoll
            _ = stderr;
        }
        finally
        {
            if (server is { HasExited: false }) server.Kill(true);
            server?.Dispose();
            Aufraeumen(wurzel);
        }
    }

    [Fact]
    public void Server_GibtReservierungenBeimBeendenFrei()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        Process? server = null;
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            server = ServerStarten("--projekt", ordner, "--akteur", "Rauchtest", "--art", "ki", "--geraet", "test");
            var g = new Gegenstelle(server);
            _ = server.StandardError.ReadToEndAsync();
            g.Anfrage("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "rauchtest", ["version"] = "1" },
            });
            g.Senden(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
            var echt = g.Anfrage("tools/call", new JsonObject
            {
                ["name"] = "element_aendern", ["arguments"] = new JsonObject { ["element"] = "E.02", ["name"] = "Vom Server" },
            });
            Assert.True(JsonNode.Parse(echt["result"]!["content"]![0]!["text"]!.GetValue<string>())!["angenommen"]!.GetValue<bool>());
            server.StandardInput.Close();
            Assert.True(server.WaitForExit(20_000));
            // nach dem Beenden sind die Sperren frei und die Änderung ist in der Ablage
            Assert.Empty(Directory.GetFiles(Path.Combine(ordner, "sperren")));
            var offen = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Pruefer", "pc");
            Assert.Equal("Vom Server", offen.Modell.Hole("E.02").Name);
        }
        finally
        {
            if (server is { HasExited: false }) server.Kill(true);
            server?.Dispose();
            Aufraeumen(wurzel);
        }
    }
}
