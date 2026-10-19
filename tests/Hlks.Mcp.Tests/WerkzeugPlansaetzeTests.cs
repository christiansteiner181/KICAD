using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Serien-PDF: MCP-Werkzeuge plansaetze, plansatz_setzen, planblatt_gewerk_setzen, plansatz_pdf. Testprojekt FIKTIV in einer Ablage unter
/// %TEMP% im Aufbau …\Objekt\c_Pläne und Modelle\e_KICAD; Planblätter ohne Ansicht (leere Seiten mit Plankopf).</summary>
public class WerkzeugPlansaetzeTests : IDisposable
{
    readonly string wurzel, objekt;
    readonly Werkzeuge w;
    readonly Projekt p;

    public WerkzeugPlansaetzeTests()
    {
        wurzel = Path.Combine(Path.GetTempPath(), "hlks_mcp_ps_" + Guid.NewGuid().ToString("N")[..8]);
        objekt = Path.Combine(wurzel, "Worb_Test");
        p = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(Path.Combine(objekt, "c_Pläne und Modelle", "e_KICAD")));
        void A(string b, string json) { var e = p.Ausfuehren(b, (JsonObject)JsonNode.Parse(json)!); Assert.True(e.Angenommen, $"{b}: {e.Fehler}"); }
        A("element_anlegen", """{ "klasse": "Projektinformation", "eigenschaften": { "projektnummer": "1-0768" } }""");
        A("planblatt_anlegen", """{ "nummer": "1-0768 S02", "titel": "Sanitär 1.Obergeschoss", "format": "A3" }""");
        A("planblatt_anlegen", """{ "nummer": "1-0768 S01", "titel": "Sanitär 1.Untergeschoss", "format": "A1" }""");
        A("planblatt_anlegen", """{ "nummer": "1-0768 H01", "titel": "Heizung 1.Untergeschoss", "format": "A3" }""");
        w = new Werkzeuge(p);
    }

    public void Dispose() => Aufraeumen(wurzel);

    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Plansaetze_ZeigtAutomatischeSaetzeJeGewerk()
    {
        var a = Json(w.PlansaetzeLesen(datum: "2026-10-19", anlass: "Revisionspläne"));
        var saetze = a["plansaetze"]!.AsArray();
        Assert.Equal(["auto:heizung", "auto:sanitaer"], saetze.Select(s => s!["schluessel"]!.GetValue<string>()).ToArray());
        var san = saetze[1]!;
        Assert.Equal(["1-0768 S01", "1-0768 S02"], san["blaetter"]!.AsArray().Select(b => b!["nummer"]!.GetValue<string>()).ToArray());
        Assert.Equal(Path.Combine(objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026_Revisionspläne", "Sanitär"), san["vorschlag"]!["ordner"]!.GetValue<string>());
        Assert.Equal("1-0768_Plansatz Sanitär_2026-10-19.pdf", san["vorschlag"]!["sammelDatei"]!.GetValue<string>());
        Assert.Empty(p.Modell.DerKlasse("Plansatz"));
        Assert.NotNull(Json(w.PlansaetzeLesen(plansatz: "gibtsnicht"))["fehler"]);
    }

    [Fact]
    public void PlansatzSetzen_TrockenlaufAnlegenUndGewerkSetzen()
    {
        var t = Json(w.PlansatzSetzenWerkzeug(plansatz: "auto:sanitaer", trockenlauf: true));
        Assert.True(t["angenommen"]!.GetValue<bool>(), t.ToJsonString());
        Assert.True(t["trockenlauf"]!.GetValue<bool>());
        Assert.Empty(p.Modell.DerKlasse("Plansatz"));

        var e = Json(w.PlansatzSetzenWerkzeug(plansatz: "auto:sanitaer", blaetter: Elem("""["1-0768 S02","1-0768 S01"]"""), abgewaehlt: Elem("""["1-0768 S02"]""")));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var s = Json(w.PlansaetzeLesen(plansatz: "PS-1"))["plansaetze"]![0]!;
        Assert.Equal(["1-0768 S02", "1-0768 S01"], s["blaetter"]!.AsArray().Select(b => b!["nummer"]!.GetValue<string>()).ToArray());
        Assert.Equal([false, true], s["blaetter"]!.AsArray().Select(b => b!["dabei"]!.GetValue<bool>()).ToArray());

        // Blatt eines anderen Gewerks: abgelehnt mit Vorschlag
        var f = Json(w.PlansatzSetzenWerkzeug(plansatz: "PS-1", blaetter: Elem("""["1-0768 H01"]""")));
        Assert.False(f["angenommen"]!.GetValue<bool>());
        Assert.Contains("planblatt_gewerk_setzen", f["vorschlag"]!.GetValue<string>());

        // Gewerk von Hand: H01 nach Sanitär
        var g = Json(w.PlanblattGewerkSetzenWerkzeug(Elem("""["1-0768 H01"]"""), "sanitaer"));
        Assert.True(g["angenommen"]!.GetValue<bool>(), g.ToJsonString());
        Assert.Contains(g["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Gewerk Sanitär gesetzt"));
        Assert.Equal(3, Json(w.PlansaetzeLesen(plansatz: "PS-1"))["plansaetze"]![0]!["blaetter"]!.AsArray().Count);
    }

    [Fact]
    public void PlansatzPdf_SchreibtSammelUndEinzelPdfs_UeberschreibtNichtOhneErlaubnis()
    {
        var ordner = Path.Combine(wurzel, "Ausgang");
        var a = Json(w.PlansatzPdfSchreiben("sanitaer", ordner: ordner, einzeln: true, datum: "2026-10-19"));
        Assert.True(a["geschrieben"]!.GetValue<bool>(), a.ToJsonString());
        Assert.Equal(2, a["seiten"]!.GetValue<int>());
        Assert.Equal(["1-0768_Plansatz Sanitär_2026-10-19.pdf", "1-0768-S01-Sanitär 1.Untergeschoss-.pdf", "1-0768-S02-Sanitär 1.Obergeschoss-.pdf"],
            a["dateien"]!.AsArray().Select(d => d!["name"]!.GetValue<string>()).ToArray());
        Assert.Equal(3, Directory.GetFiles(ordner, "*.pdf").Length);
        Assert.Contains(a["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("keine Ansicht"));
        Assert.Equal("PS-1", a["plansatz"]!.GetValue<string>());

        var zweites = Json(w.PlansatzPdfSchreiben("PS-1", ordner: ordner, datum: "2026-10-19"));
        Assert.Contains("schon vorhanden", zweites["fehler"]!.GetValue<string>());
        Assert.Contains("ueberschreiben", zweites["vorschlag"]!.GetValue<string>());
        var drittes = Json(w.PlansatzPdfSchreiben("PS-1", ordner: ordner, datum: "2026-10-19", ueberschreiben: true));
        Assert.True(drittes["geschrieben"]!.GetValue<bool>(), drittes.ToJsonString());
        Assert.Single(drittes["ueberschrieben"]!.AsArray());
    }
}
