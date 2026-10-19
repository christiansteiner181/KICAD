using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>
/// KI-Aufträge (Modul 2, B5): Ablehnungen und «Keine Änderung» bei familie_entwurf aus dem stream-json-Strom. Die Attrappe schickt
/// familie_entwurf an den echten MCP-Server; der Test legt die Familie «Attrappe-Heizkörper» vorher selbst an (Akteur Test) und
/// reserviert sie bei Bedarf wie ein Mensch im Editor.
/// </summary>
public class AuftragFamilienTests
{
    static string Z(JsonNode? n) => n?.GetValue<string>() ?? "";
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(90);

    /// <summary>Familie wie die Attrappe sie sendet, als Mensch angelegt. Gibt die Element-Id zurück.</summary>
    static async Task<string> FamilieAnlegen(AuftragServer s)
    {
        var def = JsonNode.Parse(File.ReadAllText(AuftragServer.Quelldatei("tests", "daten", "familien", "test_heizkoerper.familie.json")))!.AsObject();
        def["name"] = "Attrappe-Heizkörper";
        def["id"] = "fam-attrappe";
        var (code, j) = await s.Post("/api/befehl/familie_entwurf", new JsonObject { ["definition"] = def });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
        return await s.Dienst.Mit(p => p.Modell.DerKlasse("Familie").Single().Id);
    }

    static async Task<JsonNode> Lauf(AuftragServer s, string modus)
    {
        var (c, j) = await s.Starten(modus);
        Assert.Equal(HttpStatusCode.Accepted, c);
        var ende = await s.Warten(AuftragServer.Id(j), AuftragServer.Beendet, Lang, "Lauf " + modus);
        Assert.Equal("fertig", Z(ende["status"]));
        return ende;
    }

    [Fact]
    public async Task GleicheDefinition_FamilieStehtImErgebnisMitAktionUnveraendert()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        await FamilieAnlegen(s);

        var ende = await Lauf(s, "gleich");

        var erg = ende["ergebnis"]!;
        var f = Assert.Single(erg["familien"]!.AsArray())!;
        Assert.Equal("unveraendert", Z(f["aktion"]));
        Assert.Equal("FAM-1", Z(f["kennzeichen"]));
        Assert.Equal("Attrappe-Heizkörper", Z(f["name"]));
        Assert.Equal(0, f["pakete"]!.GetValue<int>());
        Assert.True(f["imModell"]!.GetValue<bool>());
        Assert.Equal(1, f["version"]!.GetValue<int>());                 // nichts neu geschrieben
        Assert.Equal(0, erg["pakete"]!.GetValue<int>());
        Assert.Empty(erg["abgelehnt"]!.AsArray());
        Assert.Null(erg["warnung"]);
        Assert.Contains(s.Protokoll(AuftragServer.Id(ende)), x => Z(x["text"]).StartsWith("Keine Änderung (familie_entwurf, Attrappe-Heizkörper)"));
    }

    [Fact]
    public async Task FamilieImEditorReserviert_AenderungAbgelehnt_FertigMitWarnungUndAbgelehntListe()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();
        var id = await FamilieAnlegen(s);
        var speicher = new OrdnerSpeicher(s.Ordner);
        Assert.Empty(speicher.Reservieren([id], "Christian", "pc9"));      // ein Mensch hat die Familie im Editor offen

        var ende = await Lauf(s, "aendern");

        var erg = ende["ergebnis"]!;
        Assert.Empty(erg["familien"]!.AsArray());
        Assert.Equal(0, erg["pakete"]!.GetValue<int>());
        var ab = Assert.Single(erg["abgelehnt"]!.AsArray())!;
        Assert.Equal("familie_entwurf", Z(ab["werkzeug"]));
        Assert.Equal("FAM-1", Z(ab["familie"]));
        Assert.Contains("reserviert", Z(ab["meldung"]));
        Assert.Contains("Christian@pc9", Z(ab["meldung"]));
        Assert.False(ab["trockenlauf"]!.GetValue<bool>());
        Assert.StartsWith("Änderung an FAM-1 abgelehnt:", Z(erg["warnung"]));
        Assert.Contains("reserviert", Z(erg["warnung"]));
        Assert.Contains(s.Protokoll(AuftragServer.Id(ende)), x => Z(x["text"]).StartsWith("Änderung abgelehnt (familie_entwurf, Attrappe-Heizkörper)"));

        // nach der Freigabe geht dieselbe Änderung durch: geändert, nichts abgelehnt, keine Warnung
        speicher.Freigeben("Christian", "pc9");
        var ende2 = await Lauf(s, "aendern");
        var erg2 = ende2["ergebnis"]!;
        var f = Assert.Single(erg2["familien"]!.AsArray())!;
        Assert.Equal("geaendert", Z(f["aktion"]));
        Assert.Equal(2, f["version"]!.GetValue<int>());
        Assert.Empty(erg2["abgelehnt"]!.AsArray());
        Assert.Null(erg2["warnung"]);
    }

    [Fact]
    public async Task EntwurfMitFehler_WirdAbgelehntGemeldet_AuchOhneFamilie()
    {
        using var s = new AuftragServer();
        await s.ProjektAnlegen();

        var ende = await Lauf(s, "ungueltig");

        var erg = ende["ergebnis"]!;
        Assert.Empty(erg["familien"]!.AsArray());
        var ab = Assert.Single(erg["abgelehnt"]!.AsArray())!;
        Assert.Equal("familie_entwurf", Z(ab["werkzeug"]));
        Assert.Null(ab["familie"]);
        Assert.False(string.IsNullOrWhiteSpace(Z(ab["meldung"])));
        Assert.StartsWith("Änderung an einer Familie abgelehnt: " + Z(ab["meldung"]), Z(erg["warnung"]));
    }
}
