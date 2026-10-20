using System.Diagnostics;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Sicherheitsprüfung 3c (berichte/modul-3c-sicherheitspruefung.md) im MCP-Server: B3(c) --werkzeuge registriert nur die genannten
/// Werkzeuge, B3(b) --nur-familienunterlagen setzt die Lesegrenze, B15 Kopfzeile vor inhaltstragenden Ergebnissen. Über das echte Protokoll.</summary>
public class SicherheitTests
{
    sealed class Gegenstelle(Process prozess)
    {
        int naechsteId;
        public void Senden(JsonObject n) { prozess.StandardInput.WriteLine(n.ToJsonString()); prozess.StandardInput.Flush(); }

        public JsonObject Anfrage(string methode, JsonObject? parameter = null)
        {
            int id = ++naechsteId;
            Senden(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = methode, ["params"] = parameter ?? new JsonObject() });
            while (true)
            {
                var lesen = prozess.StandardOutput.ReadLineAsync();
                if (!lesen.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("Keine Antwort auf " + methode);
                var antwort = JsonNode.Parse(lesen.Result ?? throw new EndOfStreamException("Server beendet nach " + methode))!.AsObject();
                if (antwort["id"] is { } aid && aid.GetValue<int>() == id) return antwort;
            }
        }

        public void Bereit()
        {
            Anfrage("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(), ["clientInfo"] = new JsonObject { ["name"] = "sicherheit", ["version"] = "1" },
            });
            Senden(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
        }

        public List<string> Werkzeuge()
        {
            var namen = new List<string>();
            string? cursor = null;
            do
            {
                var r = Anfrage("tools/list", cursor is null ? null : new JsonObject { ["cursor"] = cursor });
                namen.AddRange(r["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()));
                cursor = r["result"]!["nextCursor"]?.GetValue<string>();
            } while (cursor is not null);
            return namen;
        }

        public JsonObject Aufruf(string name, JsonObject? argumente = null) =>
            Anfrage("tools/call", new JsonObject { ["name"] = name, ["arguments"] = argumente ?? new JsonObject() });
    }

    /// <summary>Server auf einem neuen Beispielprojekt (Akteur KI, Art ki) mit weiteren Argumenten; stderr wird gesammelt.</summary>
    static void MitServer(string[] weitere, Action<Gegenstelle, Func<string>, string> pruefen, Action<string>? vorbereiten = null)
    {
        var (wurzel, ordner) = TempProjektOrdner();
        Process? server = null;
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            vorbereiten?.Invoke(ordner);
            server = ServerStarten(["--projekt", ordner, "--akteur", "KI", "--art", "ki", "--geraet", "test", .. weitere]);
            var stderr = server.StandardError.ReadToEndAsync();
            var g = new Gegenstelle(server);
            g.Bereit();
            pruefen(g, () => { server.StandardInput.Close(); server.WaitForExit(20_000); return stderr.Result; }, ordner);
        }
        finally
        {
            if (server is { HasExited: false }) server.Kill(true);
            server?.Dispose();
            Aufraeumen(wurzel);
        }
    }

    static string Text(JsonObject antwort, int i = 0) => antwort["result"]!["content"]![i]!["text"]!.GetValue<string>();

    [Fact]
    public void Werkzeuge_NurDieDerListe_RestUnbekannt_UnbekannteNamenMitWarnung()
    {
        MitServer(["--werkzeuge", "projekt_ueberblick,unterlagen_lesen,kontext_bild,gibt_es_nicht"], (g, ende, ordner) =>
        {
            Assert.Equal(["kontext_bild", "projekt_ueberblick", "unterlagen_lesen"], g.Werkzeuge().Order(StringComparer.Ordinal).ToList());
            Assert.Contains("MFH Musterweg", Text(g.Aufruf("projekt_ueberblick")));
            // ein nicht registriertes Werkzeug ist für den Server unbekannt – nichts geändert
            var nein = g.Aufruf("element_aendern", new JsonObject { ["element"] = "E.02", ["name"] = "Darf nicht" });
            Assert.True(nein["error"] is not null || nein["result"]?["isError"]?.GetValue<bool>() == true, nein.ToJsonString());
            var err = ende();
            Assert.Contains("Warnung: --werkzeuge nennt Werkzeuge, die dieser Server nicht hat: gibt_es_nicht", err);
            var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Pruefer", "pc");
            Assert.NotEqual("Darf nicht", p.Modell.Hole("E.02").Name);
        });
    }

    [Fact]
    public void Werkzeuge_AusDatei_UndMitLeerzeichen()
    {
        var liste = Path.Combine(Path.GetTempPath(), "hlks_wz_" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        File.WriteAllText(liste, "katalog\r\nfamilien\r\n\r\nnormen_suchen\n");
        try
        {
            MitServer(["--werkzeuge", "@" + liste], (g, _, _) =>
                Assert.Equal(["familien", "katalog", "normen_suchen"], g.Werkzeuge().Order(StringComparer.Ordinal).ToList()));
            MitServer(["--werkzeuge", "katalog,", "familien"], (g, _, _) =>
                Assert.Equal(["familien", "katalog"], g.Werkzeuge().Order(StringComparer.Ordinal).ToList()));
        }
        finally { File.Delete(liste); }
        // fehlende Datei: Server startet nicht
        var (code, _, fehler) = Lauf("--projekt", Path.GetTempPath(), "--werkzeuge", "@" + liste);
        Assert.NotEqual(0, code);
        Assert.Contains("Werkzeugliste", fehler);
    }

    [Fact]
    public void Kopfzeile_VorInhaltstragendenErgebnissen_NichtBeiProjektabfragen()
    {
        MitServer([], (g, _, _) =>
        {
            var a = g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("gibt-es-nicht.pdf") });
            Assert.Equal(KiInhalt.Kopfzeile, Text(a, 0));
            Assert.NotNull(JsonNode.Parse(Text(a, 1))!["unterlagen"]);      // das Ergebnis selbst bleibt JSON im nächsten Block
            var b = g.Aufruf("befunde", new JsonObject { ["unterlage"] = "gibt-es-nicht.pdf" });
            Assert.Equal(KiInhalt.Kopfzeile, Text(b, 0));
            var u = g.Aufruf("projekt_ueberblick");
            Assert.NotEqual(KiInhalt.Kopfzeile, Text(u, 0));
            Assert.Contains("MFH Musterweg", Text(u, 0));
        });
    }

    [Fact]
    public void NurFamilienunterlagen_LesegrenzeImServer_GenannteErlaubt()
    {
        static void Vorbereiten(string ordner)
        {
            File.WriteAllText(Path.Combine(ordner, "unterlagen", "plan.csv"), "Raum;Fläche\nE.01;20\n");
            File.WriteAllText(Path.Combine(ordner, "unterlagen", "liste.csv"), "Typ;L\nA;1\n");
        }
        MitServer(["--nur-familienunterlagen", "--unterlagen-erlaubt", "liste.csv"], (g, _, _) =>
        {
            var a = JsonNode.Parse(KiInhalt.OhneKopf(string.Concat(g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("plan.csv", "liste.csv") })
                ["result"]!["content"]!.AsArray().Select(c => c!["text"]!.GetValue<string>()))))!;
            Assert.StartsWith("Mit Internet liest die KI nur Herstellerunterlagen", a["unterlagen"]![0]!["fehler"]!.GetValue<string>());
            Assert.Null(a["unterlagen"]![1]!["fehler"]);
        }, Vorbereiten);
        // ohne den Schalter liest die KI beide (ohne Internet)
        MitServer([], (g, _, _) =>
        {
            var a = JsonNode.Parse(KiInhalt.OhneKopf(string.Concat(g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("plan.csv") })
                ["result"]!["content"]!.AsArray().Select(c => c!["text"]!.GetValue<string>()))))!;
            Assert.Null(a["unterlagen"]![0]!["fehler"]);
        }, Vorbereiten);
    }

    [Fact]
    public void Werkzeugauswahl_Lesen_UndAlle()
    {
        Assert.Null(Werkzeugauswahl.Lesen(new Argumente(["--projekt", "x"])));
        Assert.Equal(["a", "b", "c"], Werkzeugauswahl.Lesen(new Argumente(["--werkzeuge", "a, b;c"]))!.Order().ToList());
        Assert.Empty(Werkzeugauswahl.Lesen(new Argumente(["--werkzeuge"]))!);   // leere Liste = kein Werkzeug
        var alle = Werkzeugauswahl.Alle();
        Assert.Contains("kontext_bild", alle);
        Assert.Contains("element_aendern", alle);
        var erwartet = Abfragen.Liste.Select(a => a.Name).Concat(Projekt.Anlegen(Kat, null, "x", "x", "x").Befehle.Alle.Select(b => b.Name))
            .Concat(["rueckgaengig", "wiederholen", "synchronisieren", "kontext_bild"]).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(erwartet, alle);
        Assert.All(KiInhalt.Werkzeuge, w => Assert.Contains(w, alle));   // jedes Werkzeug mit Kopfzeile gibt es
    }
}
