using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;
using static Hlks.Web.Tests.GespraechServer;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Palette (Modul 3c, Teil K): Änderungen der KI in der Nachricht und «Zurücknehmen» (ok und 409) über POST
/// /api/ki/aenderungen/{paket}/zuruecknehmen. Die Attrappe ändert über den echten MCP-Server (Akteur KI, Art ki) das Element WT-E.02 des
/// Beispielprojekts; der Server sieht das Paket über den Abgleich.
/// </summary>
public class GespraechZuruecknehmenTests
{
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    static async Task<(GespraechServer S, string Id, JsonObject Antwort, string Paket, string? Vorher)> KiAendert(string modus = "aendern")
    {
        var s = new GespraechServer();
        await s.ProjektOeffnen();
        var vorher = await s.ElementName("WT-E.02");   // im Beispielprojekt ohne Namen (null)
        var id = await s.GespraechAnlegen();
        var antwort = await s.FragenUndWarten(id, $"Bitte WT-E.02 umbenennen [attrappe:{modus}=WT-E.02]", frist: Lang);
        Assert.Equal("fertig", Z(antwort["status"]));
        var paket = Z(antwort["aenderungen"]!.AsArray()[^1]!["paket"]);
        await s.AufPaketWarten(paket);
        return (s, id, antwort, paket, vorher);
    }

    [Fact]
    public async Task Aenderung_InDerNachricht_Zuruecknehmen_Ok()
    {
        var (s, id, antwort, paket, vorher) = await KiAendert();
        using var _ = s;
        var a = Assert.Single(antwort["aenderungen"]!.AsArray())!;
        Assert.Equal("element_aendern", Z(a["befehl"]));
        Assert.Equal("element_aendern: WT-E.02 geändert", Z(a["beschreibung"]));
        Assert.Equal(["WT-E.02"], a["elemente"]!.AsArray().Select(x => Z(x)));
        Assert.Single(a["ids"]!.AsArray());
        Assert.False(a["zurueckgenommen"]!.GetValue<bool>());
        var w = Assert.Single(antwort["werkzeuge"]!.AsArray())!;
        Assert.Equal("element_aendern", Z(w["name"]));
        Assert.True(w["ok"]!.GetValue<bool>());
        Assert.Equal("Von der Attrappe geändert", await s.ElementName("WT-E.02"));
        var paketDatei = new OrdnerSpeicher(s.Ordner).PaketLesen(paket);
        Assert.Equal("KI", paketDatei.Akteur);
        Assert.Equal("ki", paketDatei.AkteurArt);
        Assert.DoesNotContain(new OrdnerSpeicher(s.Ordner).Reservierungen(), r => r.Akteur == "KI");   // nach dem Lauf freigegeben

        var (code, j) = await s.Post($"/api/ki/aenderungen/{paket}/zuruecknehmen");

        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        Assert.True(j!["zurueckgenommen"]!.GetValue<bool>());
        Assert.Equal(paket, Z(j["paket"]));
        Assert.Equal(id, Z(j["gespraech"]));
        var neu = Z(j["rueckgaengig"]);
        Assert.Equal(vorher, await s.ElementName("WT-E.02"));
        var undo = new OrdnerSpeicher(s.Ordner).PaketLesen(neu);
        Assert.Equal(paket, undo.RueckgaengigVon);
        Assert.Equal("Test", undo.Akteur);                                // der Mensch nimmt zurück
        Assert.Equal("mensch", undo.AkteurArt);
        var gespeichert = (await s.Gespraech(id))["nachrichten"]!.AsArray()[^1]!["aenderungen"]![0]!;
        Assert.True(gespeichert["zurueckgenommen"]!.GetValue<bool>());
        Assert.Equal(neu, Z(gespeichert["zurueckgenommenPaket"]));
        Assert.Contains(s.Ereignisse, e => Z(e["art"]) == "ki" && Z(e["typ"]) == "zurueckgenommen" && Z(e["paket"]) == paket && Z(e["rueckgaengig"]) == neu && Z(e["gespraech"]) == id);

        // zweites Mal: 409, nichts geändert
        var (c2, j2) = await s.Post($"/api/ki/aenderungen/{paket}/zuruecknehmen");
        Assert.Equal(HttpStatusCode.Conflict, c2);
        Assert.Equal("schon_zurueckgenommen", Z(j2!["ablehnung"]));
    }

    [Fact]
    public async Task SpaetereAenderungDesMenschen_409_NichtsGeaendert()
    {
        var (s, id, _, paket, _) = await KiAendert();
        using var _s = s;
        var (cb, jb) = await s.Post("/api/befehl/element_aendern", new { element = "WT-E.02", name = "Vom Menschen" });
        Assert.True(cb == HttpStatusCode.OK && jb!["angenommen"]!.GetValue<bool>(), jb?.ToJsonString());
        var pakete = new OrdnerSpeicher(s.Ordner).PaketListe().Count;

        var (code, j) = await s.Post($"/api/ki/aenderungen/{paket}/zuruecknehmen");

        Assert.Equal(HttpStatusCode.Conflict, code);
        Assert.Equal("spaeter_geaendert", Z(j!["ablehnung"]));
        Assert.Contains("Später hat Test (mensch) mit element_aendern WT-E.02 geändert", Z(j["fehler"]));
        Assert.Contains("Zuerst die späteren Änderungen zurücknehmen", Z(j["vorschlag"]));
        var sp = Assert.Single(j["spaeter"]!.AsArray())!;
        Assert.Equal("Test", Z(sp["akteur"]));
        Assert.Equal(["WT-E.02"], sp["elemente"]!.AsArray().Select(x => Z(x)));
        Assert.Equal("Vom Menschen", await s.ElementName("WT-E.02"));
        Assert.Equal(pakete, new OrdnerSpeicher(s.Ordner).PaketListe().Count);
        Assert.False((await s.Gespraech(id))["nachrichten"]!.AsArray()[^1]!["aenderungen"]![0]!["zurueckgenommen"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ZweiKiPakete_VonHintenNachVorn()
    {
        var (s, id, antwort, _, vorher) = await KiAendert("zweimal");
        using var _s = s;
        var pakete = antwort["aenderungen"]!.AsArray().Select(a => Z(a!["paket"])).ToList();
        Assert.Equal(2, pakete.Count);
        Assert.Equal(pakete.Order(StringComparer.Ordinal), pakete);       // in der Reihenfolge der Pakete
        await s.AufPaketWarten(pakete[1]);

        Assert.Equal(HttpStatusCode.Conflict, (await s.Post($"/api/ki/aenderungen/{pakete[0]}/zuruecknehmen")).Code);
        Assert.Equal(HttpStatusCode.OK, (await s.Post($"/api/ki/aenderungen/{pakete[1]}/zuruecknehmen")).Code);
        Assert.Equal("Von der Attrappe geändert 1", await s.ElementName("WT-E.02"));
        Assert.Equal(HttpStatusCode.OK, (await s.Post($"/api/ki/aenderungen/{pakete[0]}/zuruecknehmen")).Code);
        Assert.Equal(vorher, await s.ElementName("WT-E.02"));
        Assert.All((await s.Gespraech(id))["nachrichten"]!.AsArray()[^1]!["aenderungen"]!.AsArray(), a => Assert.True(a!["zurueckgenommen"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task UnbekanntesPaket_404_PaketDesMenschen_400_WaehrendEinesLaufs_409()
    {
        using var s = new GespraechServer();
        await s.ProjektOeffnen();
        Assert.Equal(HttpStatusCode.NotFound, (await s.Post("/api/ki/aenderungen/20260101T000000000Z_x_y_000001/zuruecknehmen")).Code);
        var (_, jb) = await s.Post("/api/befehl/element_aendern", new { element = "WT-E.02", name = "Vom Menschen" });
        var (c, j) = await s.Post($"/api/ki/aenderungen/{Z(jb!["paketId"])}/zuruecknehmen");
        Assert.Equal(HttpStatusCode.BadRequest, c);
        Assert.Equal("fremd", Z(j!["ablehnung"]));

        var id = await s.GespraechAnlegen();
        await s.Fragen(id, "[attrappe:haengt]");
        await s.AufProzesseWarten(id, Lang);
        var (c2, j2) = await s.Post($"/api/ki/aenderungen/{Z(jb["paketId"])}/zuruecknehmen");
        Assert.Equal(HttpStatusCode.Conflict, c2);
        Assert.Contains("Zurücknehmen erst nach dem Lauf", Z(j2!["fehler"]));
        await s.Post($"/api/ki/gespraeche/{id}/abbrechen");
    }

    [Fact]
    public async Task AnderswoZurueckgenommen_ErscheintAlsZurueckgenommen()
    {
        var (s, id, _, paket, vorher) = await KiAendert();
        using var _s = s;
        var (c, j) = await s.Post("/api/befehl/paket_zuruecknehmen", new { paket, akteurArt = "ki" });   // derselbe Befehl über die Tür
        Assert.True(c == HttpStatusCode.OK && j!["angenommen"]!.GetValue<bool>(), j?.ToJsonString());
        Assert.Equal(vorher, await s.ElementName("WT-E.02"));
        Assert.True((await s.Gespraech(id))["nachrichten"]!.AsArray()[^1]!["aenderungen"]![0]!["zurueckgenommen"]!.GetValue<bool>());
    }
}

/// <summary>Modul 3c, Teil K: Normensuche ohne Normenordner (HLKS_NORMEN nicht gesetzt) – im Testlauf gesperrt, nie W:. Allein, weil andere
/// Tests HLKS_NORMEN kurz setzen (ApiTests: Abdeckung aller Abfragen).</summary>
[CollectionDefinition("NormenUmgebungWeb", DisableParallelization = true)]
public sealed class NormenUmgebungWebSammlung { }

[Collection("NormenUmgebungWeb")]
public class GespraechNormenOhneOrdnerTests
{
    [Fact]
    public async Task Normen_OhneOrdner_ImTestlaufGesperrt_NieW()
    {
        using var u = Hlks.Tests.Gemeinsam.TestUmgebung.Setzen(("HLKS_NORMEN", null));
        using var s = new GespraechServer();
        var j = (await s.Get("/api/normen/suche?q=Gefälle")).Json!;
        Assert.Empty(j["treffer"]!.AsArray());
        Assert.Contains("Testlauf", Z(j["hinweis"]));
        Assert.False((await s.Get("/api/normen")).Json!["verfuegbar"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Post("/api/normen/aufbauen")).Code);
    }
}

/// <summary>Modul 3c, Teil K: Verbotsliste der Werkzeuge und Schalter von Claude Code (ohne Lauf).</summary>
public class GespraechVerbotslisteTests
{
    static HashSet<string> McpWerkzeuge() =>
        new[] { typeof(Hlks.Mcp.Werkzeuge), typeof(Hlks.Mcp.SitzungWerkzeuge) }
            .SelectMany(t => t.GetMethods()).Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name).Where(n => n is not null).Select(n => n!).ToHashSet();

    [Fact]
    public void Verbotsliste_GibtEsImMcpServer_JederEintragBegruendet()
    {
        var vorhanden = McpWerkzeuge();
        Assert.Contains("kontext_bild", vorhanden);
        foreach (var v in GespraechDienst.Verbote)
        {
            Assert.Contains(v.Werkzeug, vorhanden);
            Assert.True(v.Grund.Length >= 20, $"{v.Werkzeug}: Begründung fehlt");
        }
        Assert.Equal(GespraechDienst.Verbotsliste.Count, GespraechDienst.Verbotsliste.Distinct().Count());
    }

    [Fact]
    public void Verbotsliste_EnthaeltDieVorgabenDerSpezifikation()
    {
        var v = GespraechDienst.Verbotsliste;
        foreach (var w in new[] { "familie_freigeben", "konflikt_entscheiden", "wert_bestaetigen", "familie_in_bibliothek", "familie_aktualisieren",
                     "familienauswahl_setzen", "aussparung_status", "kollision_status", "unterlage_aus_url" })
            Assert.Contains(w, v);
        // alles hkap_* (HKAP im Betrieb) – auch künftige Werkzeuge: jedes hkap_-Werkzeug des Servers muss in der Liste stehen
        var hkap = McpWerkzeuge().Where(n => n.StartsWith("hkap_")).ToList();
        Assert.Equal(8, hkap.Count);
        Assert.All(hkap, h => Assert.Contains(h, v));
        // nicht verboten: lesen, ändern über den Kern, Normen, Bild
        foreach (var w in new[] { "projekt_ueberblick", "element_lesen", "element_aendern", "leitung_ziehen", "rueckgaengig", "normen_suchen", "norm_lesen",
                     "kontext_bild", "datenpunkte", "zeitreihe", "familie_entwurf", "unterlagen_lesen" })
            Assert.DoesNotContain(w, v);
    }

    [Fact]
    public void ClaudeArgumente_OhneUndMitInternet()
    {
        var e = new GespraechEinstellungen();
        var ohne = GespraechDienst.ClaudeArgumente(e, @"C:\x\mcp.json", "sonnet", false, "11111111-1111-1111-1111-111111111111", false);
        string? W(List<string> a, string s) { var i = a.IndexOf(s); return i < 0 ? null : a[i + 1]; }
        Assert.Equal("", W(ohne, "--tools"));
        Assert.Equal("mcp__hlks", W(ohne, "--allowedTools"));
        Assert.Equal(GespraechDienst.Verbotsliste.Count, W(ohne, "--disallowedTools")!.Split(',').Length);
        Assert.All(W(ohne, "--disallowedTools")!.Split(','), x => Assert.StartsWith("mcp__hlks__", x));
        Assert.Equal("11111111-1111-1111-1111-111111111111", W(ohne, "--session-id"));
        Assert.DoesNotContain("--resume", ohne);
        Assert.DoesNotContain("--no-session-persistence", ohne);
        Assert.DoesNotContain("--effort", ohne);
        Assert.Equal(@"C:\x\mcp.json", W(ohne, "--mcp-config"));
        Assert.Contains("--strict-mcp-config", ohne);
        Assert.Contains("--include-partial-messages", ohne);
        Assert.Contains("--disable-slash-commands", ohne);

        var mit = GespraechDienst.ClaudeArgumente(e with { Aufwand = "high" }, "m.json", "haiku", true, "abc", true);
        Assert.Equal("WebSearch", W(mit, "--tools"));
        Assert.Equal("abc", W(mit, "--resume"));
        Assert.DoesNotContain("--session-id", mit);
        Assert.Equal("haiku", W(mit, "--model"));
        Assert.Equal("high", W(mit, "--effort"));
    }

    /// <summary>Entscheid Hauptlauf 20.10.2026: mit Internet ohne Projektzugriff – erlaubt sind nur WebSearch, unterlage_aus_url, die Normenwerkzeuge,
    /// kontext_bild und die Familien-/Unterlagen-Werkzeuge der Internetliste der Aufträge; jedes andere Werkzeug des Servers steht in --disallowedTools.</summary>
    [Fact]
    public void Internet_OhneProjektzugriff_NurDieEngeListe_AllesAndereVerboten()
    {
        Assert.Equal(AuftragDienst.InternetWerkzeuge.Concat(["normen_suchen", "norm_lesen", "kontext_bild"]).Order(), GespraechDienst.InternetWerkzeuge.Order());
        var server = McpWerkzeuge();
        Assert.All(GespraechDienst.InternetWerkzeuge, w => Assert.Contains(w, server));
        // keine Projektabfragen und keine ändernden Projektbefehle in der Liste
        foreach (var w in new[] { "projekt_ueberblick", "element_lesen", "elemente_suchen", "system_lesen", "strang_text", "raum_inhalt", "aenderungen", "warum",
                     "pruefen", "mengen", "datenpunkte", "zeitreihe", "element_anlegen", "element_aendern", "element_loeschen", "leitung_ziehen", "verbinden",
                     "rueckgaengig", "wiederholen", "ifc_uebernehmen", "unterlage_einfuegen", "familie_platzieren" })
            Assert.DoesNotContain(w, GespraechDienst.InternetWerkzeuge);

        var a = GespraechDienst.ClaudeArgumente(new GespraechEinstellungen(), "m.json", "sonnet", true, "abc", false);
        string W(string s) => a[a.IndexOf(s) + 1];
        var erlaubt = W("--allowedTools").Split(',');
        Assert.Equal(GespraechDienst.InternetWerkzeuge.Select(w => "mcp__hlks__" + w).Prepend("WebSearch").Order(), erlaubt.Order());
        Assert.DoesNotContain("mcp__hlks", erlaubt);                       // nicht mehr der ganze Server
        var verboten = W("--disallowedTools").Split(',').ToHashSet();
        foreach (var w in server)
            Assert.True(GespraechDienst.InternetWerkzeuge.Contains(w) ? !verboten.Contains("mcp__hlks__" + w) : verboten.Contains("mcp__hlks__" + w),
                $"{w}: mit Internet falsch eingeordnet");
        Assert.Contains("OHNE Projektzugriff", new GespraechDienst(null!, new GespraechEinstellungen(), new KiLaeufe()).Systemteil(true));
        Assert.DoesNotContain("OHNE Projektzugriff", new GespraechDienst(null!, new GespraechEinstellungen(), new KiLaeufe()).Systemteil(false));
    }

    [Fact]
    public void AlleWerkzeuge_AusDemKern_SindGenauDieDesMcpServers()
    {
        // Die Liste für --disallowedTools mit Internet stammt aus dem Kern (Abfragen, Befehle, Verlauf, kontext_bild) – sie muss vollständig sein
        Assert.Equal(McpWerkzeuge().Order(StringComparer.Ordinal), GespraechDienst.AlleWerkzeuge());
    }

    [Fact]
    public void ClaudeArgumente_NurSchalterDieDieAttrappeKennt()
    {
        // Die Attrappe kennt genau die Schalter aus «claude --help» (2.1.299), die die Aufträge und Gespräche brauchen; ein unbekannter endet mit Code 2.
        var a = GespraechDienst.ClaudeArgumente(new GespraechEinstellungen { Aufwand = "low" }, "m.json", "sonnet", true, "abc", true);
        var bekannt = new HashSet<string> { "-p", "--output-format", "--verbose", "--include-partial-messages", "--mcp-config", "--strict-mcp-config", "--tools",
            "--allowedTools", "--disallowedTools", "--permission-mode", "--disable-slash-commands", "--setting-sources", "--model", "--resume", "--session-id", "--effort" };
        Assert.All(a.Where(x => x.StartsWith('-')), x => Assert.Contains(x, bekannt));
    }
}
