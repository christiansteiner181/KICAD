using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit.Abstractions;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Nachbearbeitung N5 (20.10.2026): Lehnt der Kern einen Werkzeugaufruf ab, trägt das MCP-Ergebnis isError – für jedes Werkzeug gleich
/// (Filter Fehlerkennung, Regel Werkzeugergebnis.MeldetFehler). Befund Abnahmelauf B6: unterlagen_lesen erschien abgelehnt als «ok».
/// Über das echte Protokoll, Akteur KI; der Server läuft auf einem Beispielprojekt unter %TEMP%, HKAP zeigt auf einen freien eigenen Port.</summary>
public class FehlerkennungTests(ITestOutputHelper aus)
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
                if (!lesen.Wait(TimeSpan.FromSeconds(90))) throw new TimeoutException("Keine Antwort auf " + methode + " " + parameter?.ToJsonString());
                var antwort = JsonNode.Parse(lesen.Result ?? throw new EndOfStreamException("Server beendet nach " + methode))!.AsObject();
                if (antwort["id"] is { } aid && aid.GetValue<int>() == id) return antwort;
            }
        }

        public void Bereit()
        {
            Anfrage("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject(), ["clientInfo"] = new JsonObject { ["name"] = "n5", ["version"] = "1" },
            });
            Senden(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
        }

        public JsonObject Aufruf(string name, JsonObject? argumente = null) =>
            Anfrage("tools/call", new JsonObject { ["name"] = name, ["arguments"] = argumente ?? new JsonObject() })["result"]!.AsObject();
    }

    /// <summary>Ablagen und HKAP nur für den Serverprozess: alles unter %TEMP%, HKAP auf 127.0.0.1:5318 (dort lauscht nichts; nie 5013).</summary>
    static Dictionary<string, string?> Umgebung(string wurzel) => new()
    {
        ["HLKS_TEST"] = "1",
        ["HLKS_HKAP_URL"] = "http://127.0.0.1:5318",
        ["HLKS_PROJEKTABLAGE"] = Path.Combine(wurzel, "ablage"),
        ["HLKS_FAMILIENBIBLIOTHEK"] = Path.Combine(wurzel, "bibliothek"),
        ["HLKS_BIBLIOTHEK_AUSSTEHEND"] = Path.Combine(wurzel, "ausstehend"),
        ["HLKS_NORMEN"] = Path.Combine(wurzel, "normen"),
    };

    static void MitServer(string[] weitere, Action<Gegenstelle, string> pruefen, Action<string>? vorbereiten = null)
    {
        var (wurzel, ordner) = TempProjektOrdner();
        Process? server = null;
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            vorbereiten?.Invoke(ordner);
            foreach (var u in new[] { "ablage", "bibliothek", "ausstehend", "normen" }) Directory.CreateDirectory(Path.Combine(wurzel, u));
            server = ServerStarten(Umgebung(wurzel), ["--projekt", ordner, "--akteur", "KI", "--art", "ki", "--geraet", "test", .. weitere]);
            _ = server.StandardError.ReadToEndAsync();
            var g = new Gegenstelle(server);
            g.Bereit();
            pruefen(g, wurzel);
            server.StandardInput.Close();
            server.WaitForExit(20_000);
        }
        finally
        {
            if (server is { HasExited: false }) server.Kill(true);
            server?.Dispose();
            Aufraeumen(wurzel);
        }
    }

    static string Text(JsonObject ergebnis) => string.Join("\n", ergebnis["content"]!.AsArray().OfType<JsonObject>().Select(c => c["text"]?.GetValue<string>()).Where(t => t is not null && t != KiInhalt.Kopfzeile));
    static bool IstFehler(JsonObject ergebnis) => ergebnis["isError"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>Werkzeuge mit Pfad-Parameter: die erzwungene Ablehnung kommt aus der Pfadprüfung der KI (KiPfade). bcf_export und aussparungen_ifc
    /// lehnen den Pfad «….pdf» schon an der Endung ab (vor der Pfadprüfung, ohne Dateizugriff) – auch abgelehnt, nur mit anderer Meldung.</summary>
    static readonly string[] Pfadwerkzeuge =
        ["unterlagen_lesen", "befunde", "unterlage_bild", "ifc_pruefen", "ifc_uebernehmen", "bcf_lesen", "bcf_import", "unterlage_einfuegen"];

    [Fact]
    public void Alle_Werkzeuge_Ablehnung_des_Kerns_traegt_isError()
    {
        // Alle Werkzeuge mit Parametern aus ihrer Methode (Name und Typ wie im Schema); Pflichtparameter erzwingen die Ablehnung:
        // Text = ein Pfad ausserhalb der erlaubten Wurzeln (für Pfade: Pfadprüfung; für Elemente, Klassen, Namen: «gibt es nicht»), Zahl 1,
        // JSON = derselbe Pfad als Text (Liste bei unterlagen). Wo es trockenlauf gibt, true – nichts wird gespeichert.
        var werkzeuge = new[] { typeof(Werkzeuge), typeof(SitzungWerkzeuge) }.SelectMany(t => t.GetMethods())
            .Select(m => (Name: m.GetCustomAttribute<McpServerToolAttribute>()?.Name, Methode: m)).Where(x => x.Name is not null)
            .OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(Werkzeugauswahl.Alle(), werkzeuge.Select(x => x.Name!).ToList());
        var fremd = Path.Combine(Path.GetTempPath(), "hlks_n5_fremd_" + Guid.NewGuid().ToString("N")[..8], "fremd.pdf");   // nie angelegt

        MitServer([], (g, _) =>
        {
            var ergebnisse = new List<(string Name, bool Pflicht, bool IsError, string Text)>();
            foreach (var (name, m) in werkzeuge)
            {
                var arg = new JsonObject();
                bool pflicht = false;
                foreach (var p in m.GetParameters())
                {
                    var typ = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
                    if (p.Name == "trockenlauf" && typ == typeof(bool)) { arg["trockenlauf"] = true; continue; }
                    if (p.HasDefaultValue) continue;
                    pflicht = true;
                    arg[p.Name!] = typ == typeof(double) ? 1
                        : typ == typeof(bool) ? false
                        : typ == typeof(JsonElement) && p.Name == "unterlagen" ? new JsonArray(JsonValue.Create(fremd))
                        : JsonValue.Create(fremd);
                }
                var r = g.Aufruf(name!, arg);
                ergebnisse.Add((name!, pflicht, IstFehler(r), Text(r)));
            }
            foreach (var e in ergebnisse) aus.WriteLine($"{(e.IsError ? "abgelehnt" : "ok       ")} {e.Name}{(e.Pflicht ? "" : " (ohne Pflichtparameter)")}: {Kurz(e.Text)}");

            // 1. Einheitlich: isError genau dann, wenn das Ergebnis eine Ablehnung meldet (dieselbe Regel wie Palette und Auftragsprotokoll)
            var ungleich = ergebnisse.Where(e => e.IsError != Werkzeugergebnis.MeldetFehler(e.Text)).ToList();
            Assert.True(ungleich.Count == 0, "isError passt nicht zum Inhalt: " + string.Join(" | ", ungleich.Select(e => $"{e.Name} isError={e.IsError}: {Kurz(e.Text)}")));
            // 2. Jede erzwungene Ablehnung (Pflichtparameter mit fremdem Wert) kommt als isError an. Ausgenommen, weil der Wert dort gültig ist:
            //    material_anlegen (ein Material darf jeden Namen tragen; Trockenlauf angenommen), normen_suchen (eine Suche ohne Treffer ist ein Ergebnis)
            string[] nehmenAn = ["material_anlegen", "normen_suchen"];
            Assert.All(nehmenAn, w => Assert.False(ergebnisse.Single(x => x.Name == w).IsError, w));
            var nichtAbgelehnt = ergebnisse.Where(e => e.Pflicht && !e.IsError && !nehmenAn.Contains(e.Name)).ToList();
            Assert.True(nichtAbgelehnt.Count == 0, "Nicht abgelehnt: " + string.Join(" | ", nichtAbgelehnt.Select(e => $"{e.Name}: {Kurz(e.Text)}")));
            Assert.True(ergebnisse.Count(e => e.Pflicht) >= 75, $"nur {ergebnisse.Count(e => e.Pflicht)} Werkzeuge mit Pflichtparametern");   // Stand 20.10.2026: 81 von 130
            // 3. Werkzeuge mit Pfad: die Pfadprüfung der KI lehnt ab – unterlagen_lesen eingeschlossen (Befund B6)
            foreach (var w in Pfadwerkzeuge)
            {
                var e = ergebnisse.Single(x => x.Name == w);
                Assert.True(e.IsError, w);
                Assert.Contains(KiPfade.Meldung, e.Text);
            }
            // 4. Erfolgreiche Abfragen bleiben ohne Kennzeichen, auch mit verschachteltem «fehler» (projekt_ueberblick: befunde.fehler = Anzahl)
            foreach (var w in new[] { "projekt_ueberblick", "faehigkeiten", "katalog", "kontext_bild", "synchronisieren" })
                Assert.False(ergebnisse.Single(x => x.Name == w).IsError, w);
            Assert.NotNull(JsonNode.Parse(ergebnisse.Single(x => x.Name == "projekt_ueberblick").Text)!["befunde"]!["fehler"]);
        });
    }

    [Fact]
    public void Lesegrenze_mit_Internet_abgelehnte_Unterlagen_tragen_isError()
    {
        static void Vorbereiten(string ordner)
        {
            File.WriteAllText(Path.Combine(ordner, "unterlagen", "plan.csv"), "Raum;Fläche\nE.01;20\n");
            File.WriteAllText(Path.Combine(ordner, "unterlagen", "liste.csv"), "Typ;L\nA;1\n");
        }
        MitServer(["--nur-familienunterlagen", "--unterlagen-erlaubt", "liste.csv"], (g, _) =>
        {
            // eine Unterlage, abgelehnt (KiUnterlagen): isError, Meldung oben und je Unterlage
            var a = g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("plan.csv") });
            Assert.True(IstFehler(a), Text(a));
            var ja = JsonNode.Parse(Text(a))!;
            Assert.StartsWith("Mit Internet liest die KI nur Herstellerunterlagen", ja["fehler"]!.GetValue<string>());
            Assert.NotNull(ja["vorschlag"]);
            Assert.StartsWith("Mit Internet liest die KI nur Herstellerunterlagen", ja["unterlagen"]![0]!["fehler"]!.GetValue<string>());
            // eine von zwei abgelehnt: der Aufruf gilt als abgelehnt, die gelesene steht trotzdem da
            var b = g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("plan.csv", "liste.csv") });
            Assert.True(IstFehler(b), Text(b));
            var jb = JsonNode.Parse(Text(b))!;
            Assert.StartsWith("1 von 2 Unterlagen nicht gelesen: «plan.csv»: Mit Internet", jb["fehler"]!.GetValue<string>());
            Assert.Equal(1, jb["gelesen"]!.GetValue<int>());
            Assert.Null(jb["unterlagen"]![1]!["fehler"]);
            // nur die erlaubte: kein Fehler
            var c = g.Aufruf("unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray("liste.csv") });
            Assert.False(IstFehler(c), Text(c));
            Assert.Null(JsonNode.Parse(Text(c))!["fehler"]);
            // befunde und unterlage_bild derselben Unterlage: ebenso abgelehnt
            Assert.True(IstFehler(g.Aufruf("befunde", new JsonObject { ["unterlage"] = "plan.csv" })));
            Assert.True(IstFehler(g.Aufruf("unterlage_bild", new JsonObject { ["unterlage"] = "plan.csv" })));
        }, Vorbereiten);
    }

    [Fact]
    public void Kennzeichnen_nur_bei_Ablehnung_Kopfzeile_und_Bild_uebergangen()
    {
        static CallToolResult R(params ContentBlock[] bloecke) => new() { Content = [.. bloecke] };
        static TextContentBlock T(string t) => new() { Text = t };
        Assert.True(Fehlerkennung.Kennzeichnen(R(T(KiInhalt.Kopfzeile), T("{\"fehler\":\"nein\",\"vorschlag\":\"so\"}"))).IsError);
        Assert.True(Fehlerkennung.Kennzeichnen(R(T("{\"angenommen\":false,\"meldung\":\"Abgelehnt: x\"}"))).IsError);
        Assert.True(Fehlerkennung.Kennzeichnen(R(T("{\"unterlagen\":[{\"fehler\":\"x\"}],\"fehler\":\"x\"}"))).IsError);
        Assert.NotEqual(true, Fehlerkennung.Kennzeichnen(R(T("{\"angenommen\":true,\"paketId\":\"p\"}"))).IsError);
        Assert.NotEqual(true, Fehlerkennung.Kennzeichnen(R(T("{\"befunde\":{\"fehler\":2}}"))).IsError);
        Assert.NotEqual(true, Fehlerkennung.Kennzeichnen(R(T("{\"fehler\":null}"))).IsError);
        Assert.NotEqual(true, Fehlerkennung.Kennzeichnen(R(T("Strang S1: fehler im Text ist keine Ablehnung"))).IsError);
        Assert.NotEqual(true, Fehlerkennung.Kennzeichnen(R(T("{\"bereich\":[0,0,1,1]}"), ImageContentBlock.FromBytes(new byte[] { 0x89, 0x50 }, "image/png"))).IsError);
        // ein selbst gesetztes Kennzeichen bleibt (kontext_bild: Text statt JSON)
        var k = R(T("Das Bild zur Frage ist weder PNG noch JPEG.")); k.IsError = true;
        Assert.True(Fehlerkennung.Kennzeichnen(k).IsError);
    }

    static string Kurz(string s) { s = s.Replace('\n', ' ').Replace('\r', ' '); return s.Length <= 160 ? s : s[..160] + " …"; }
}
