using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HeizungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Lüftung (Christian 02.11.2026): Durchlässe bestimmen die Luftmenge, summiert zum Gerät; Fliessrichtung auf Befehl aus dem Lüftungsgerät.
/// Netz und Luftmengen FIKTIV: LG-1.zul – K-0 – Bogen BO-1 – K-1 – TS-1 – K-2 – ZD-1 (120 m³/h) bzw. K-3 – ZD-2 (ohne Angabe, Raum R-1 Zuluft 300 m³/h).</summary>
public class LueftungMengenTests
{
    static Projekt Netz(bool k1Verkehrt)
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Raum","kennzeichen":"R-1","name":"Büro","geschoss":"EG","eigenschaften":{"nummer":"R-1","nutzung":"Büro","lichteHoehe":2500,"zuluft":300},"geometrie":{"art":"polygon","punkte":[[0,-1000],[8000,-1000],[8000,5000],[0,5000]]}}""");
        Punkt(p, "Lueftungsgeraet", "LG-1", null, 0, 0, 2500);
        Punkt(p, "Bogen", "BO-1", null, 1000, 0, 2500);
        Punkt(p, "TStueck", "TS-1", null, 2000, 0, 2500);
        Punkt(p, "Zuluftdurchlass", "ZD-1", null, 4000, 0, 2500, O("""{"volumenstrom":120}"""), "R-1");
        Punkt(p, "Zuluftdurchlass", "ZD-2", null, 2000, 2000, 2500, null, "R-1");
        void Kanal(string kz, string von, string nach, double[] a, double[] b) => Gut(p, "leitung_ziehen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["durchmesser"] = 200 },
            ["punkte"] = new JsonArray(new JsonArray(a[0], a[1], a[2]), new JsonArray(b[0], b[1], b[2])),
            ["von"] = von, ["nach"] = nach,
        });
        Kanal("K-0", "LG-1.zul", "BO-1.a", [0, 0, 2500], [1000, 0, 2500]);
        // verkehrt wie nach einem Import: zwischen zwei Formteilen (ungerichtet) nimmt Verbinden die Leitung auch gegen den Fluss
        if (k1Verkehrt) Kanal("K-1", "TS-1.a", "BO-1.b", [2000, 0, 2500], [1000, 0, 2500]);
        else Kanal("K-1", "BO-1.b", "TS-1.a", [1000, 0, 2500], [2000, 0, 2500]);
        Kanal("K-2", "TS-1.b", "ZD-1.zul", [2000, 0, 2500], [4000, 0, 2500]);
        Kanal("K-3", "TS-1.c", "ZD-2.zul", [2000, 0, 2500], [2000, 2000, 2500]);
        return p;
    }

    static double? M3h(Projekt p, string kz) => LueftungMengen.Ergebnis(p.Modell, p.Katalog) is var n && p.Modell.Hole(kz) is var e
        && n.Durchlaesse.TryGetValue(e.Id, out var d) ? d.M3h : n.Kanal(e.Id)?.M3h;

    [Fact]
    public void Volumenstrom_AngabeUndRaumrest_ZumGeraetSummiert()
    {
        var p = Netz(k1Verkehrt: false);
        Assert.Equal(120, M3h(p, "ZD-1"));
        Assert.Equal(180, M3h(p, "ZD-2"));   // Raum 300 − Angabe 120, auf den einen Durchlass ohne Angabe
        Assert.Equal(120, M3h(p, "K-2"));
        Assert.Equal(180, M3h(p, "K-3"));
        Assert.Equal(300, M3h(p, "K-1"));
        Assert.Equal(300, M3h(p, "K-0"));
    }

    [Fact]
    public void Druckverlust_Reibung_je_Kanal_und_Weg_zum_Durchlass_wie_von_Hand()
    {
        // Modul 6 Teil B (Physik, kataloge/stroemung.json): K-0 Ø 200 mm, 1 m, 300 m³/h, Luft 20 °C (ν = 14.9e-6, ρ = η/ν = 17.9/14.9), Blechkanal k = 0.15 mm
        var p = Netz(k1Verkehrt: false);
        var r = (JsonObject)Tuer.Abfrage(p, "kanal_druckverlust", new JsonObject());
        var k0 = r["kanaele"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["kanal"] == "K-0");
        double a = Math.PI * 0.2 * 0.2 / 4, w = 300 / 3600.0 / a, nu = 14.9e-6, rho = 17.9 / 14.9, re = w * 0.2 / nu;
        double lam = k0["lambda"]!.GetValue<double>();
        // Colebrook erfüllt (Tab. 2): 1/√λ = −2·lg((k/d)/3,71 + 2,51/(Re·√λ))
        Assert.True(Math.Abs(1 / Math.Sqrt(lam) + 2 * Math.Log10(0.15 / 200 / 3.71 + 2.51 / (re * Math.Sqrt(lam)))) < 0.01);
        Assert.Equal(lam / 0.2 * rho * w * w / 2 * 1.0, k0["dp_Pa"]!.GetValue<double>(), 2);
        Assert.Equal(64 / 1000.0, KanalDruckverlust.Lambda(1000, 0.001), 9);   // laminar
        // ZD-1: Summe K-0 + K-1 + K-2 bis zum Gerät
        double Dp(string kz) => r["kanaele"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["kanal"] == kz)["dp_Pa"]!.GetValue<double>();
        var zd1 = r["durchlaesse"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["durchlass"] == "ZD-1");
        Assert.Equal(Dp("K-0") + Dp("K-1") + Dp("K-2"), zd1["dpReibung_Pa"]!.GetValue<double>(), 1);
    }

    [Fact]
    public void Fliessrichtung_AusLueftungsgeraet_KehrtVerkehrtenKanalUm()
    {
        var p = Netz(k1Verkehrt: true);
        Assert.Null(M3h(p, "K-1"));   // gegen den Fluss gezeichnet: der Weg vom Durchlass zum Gerät bricht ab
        var e = Gut(p, "fliessrichtung_aus_quelle", """{"quelle":"LG-1"}""");
        var k1 = p.Modell.Hole("K-1");
        Assert.Equal([1000, 0, 2500], k1.Geometrie!.Punkte[0]);
        Assert.Equal("BO-1", p.Modell.Finde(p.Modell.VerbindungAn(k1.Id, "anfang")!.Gegenueber(k1.Id, "anfang")!.Element)!.Kennzeichen);
        Assert.Equal("TS-1", p.Modell.Finde(p.Modell.VerbindungAn(k1.Id, "ende")!.Gegenueber(k1.Id, "ende")!.Element)!.Kennzeichen);
        Assert.Equal(300, M3h(p, "K-1"));
        Assert.Contains(e.Hinweise, h => h.Contains("4 Leitung(en) erreicht, 1 umgekehrt"));
        // zweiter Lauf ändert nichts mehr
        Assert.Contains(Gut(p, "fliessrichtung_aus_quelle", """{"quelle":"LG-1"}""").Hinweise, h => h.Contains("0 umgekehrt"));
    }

    [Fact]
    public void Druckverlust_Weg_endet_am_Geraet_und_Kanal_ohne_Volumenstrom_bleibt_ohne_NaN()
    {
        // Gesamtprüfung 13.11.2026: Zu- und Abluft am selben Gerät. Der Weg des Zuluftdurchlasses darf nicht durch das Gerät in die Abluftkanäle laufen.
        var p = Netz(k1Verkehrt: false);
        Punkt(p, "Abluftdurchlass", "AD-1", null, 0, 3000, 2500, O("""{"volumenstrom":100}"""), "R-1");
        Gut(p, "leitung_ziehen", new JsonObject
        {
            ["klasse"] = "Kanal", ["kennzeichen"] = "K-4", ["eigenschaften"] = new JsonObject { ["durchmesser"] = 200 },
            ["punkte"] = new JsonArray(new JsonArray(0, 3000, 2500), new JsonArray(0, 0, 2500)), ["von"] = "AD-1.abl", ["nach"] = "LG-1.abl",
        });
        // ZD-1 trägt den ganzen Raum (300 m³/h): für ZD-2 bleibt 0 – K-3 ohne Strömung
        Gut(p, "element_aendern", """{"element":"ZD-1","eigenschaften":{"volumenstrom":300}}""");
        var r = (JsonObject)Tuer.Abfrage(p, "kanal_druckverlust", new JsonObject());
        r.ToJsonString();   // keine NaN-Werte (warf vorher bei Re = 0)
        JsonObject Kanal(string kz) => r["kanaele"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["kanal"] == kz);
        JsonObject Durchlass(string kz) => r["durchlaesse"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["durchlass"] == kz);
        double Dp(string kz) => Kanal(kz)["dp_Pa"]!.GetValue<double>();
        Assert.Null(Kanal("K-3")["dp_Pa"]);
        Assert.Contains("kein Druckverlust", (string?)Kanal("K-3")["hinweis"]);
        Assert.Equal(Dp("K-0") + Dp("K-1") + Dp("K-2"), Durchlass("ZD-1")["dpReibung_Pa"]!.GetValue<double>(), 1);
        Assert.Equal(3, Durchlass("ZD-1")["kanaele"]!.GetValue<int>());
        Assert.Equal(Dp("K-4"), Durchlass("AD-1")["dpReibung_Pa"]!.GetValue<double>(), 1);
        Assert.Equal(1, Durchlass("ZD-2")["kanaeleOhneDp"]!.GetValue<int>());   // K-3 fehlt in der Summe und wird genannt
    }

    [Fact]
    public void Druckverlust_Querschnitt_Rohr_mit_Innendurchmesser_und_Kunststoff_nur_als_Wort()
    {
        // Rohr einer Produktlinie: Mass der Darstellung ist aussen (75), durchströmt ist innen (63) – FIKTIVE Masse
        LeitungsMass Rohr(double? innen) => new(new LeitungsSerieInfo(null!, "PE-Rohr", "Rohr", "rund", "75",
            new FamLeitungWerte(new Dictionary<string, double>(), 75, innen, null, null)), false, 75, 75, 0);
        var q = KanalDruckverlust.Querschnitt(Rohr(63))!.Value;
        Assert.Equal(0.063, q.Dh, 9);
        Assert.Equal(Math.PI * 0.063 * 0.063 / 4, q.Flaeche, 9);
        Assert.NotNull(KanalDruckverlust.Querschnitt(Rohr(null))!.Value.Hinweis);
        Assert.Equal(2 * 0.4 * 0.2 / 0.6, KanalDruckverlust.Querschnitt(new LeitungsMass(null, true, 400, 200, 0))!.Value.Dh, 9);
        Assert.Null(KanalDruckverlust.Querschnitt(new LeitungsMass(null, true, 400, 0, 0)));
        Assert.True(KanalDruckverlust.IstKunststoff("PE-HD Lüftungsrohr"));
        Assert.True(KanalDruckverlust.IstKunststoff("Kunststoffkanal PP"));
        Assert.False(KanalDruckverlust.IstKunststoff("Spezialkanal doppelwandig"));
        Assert.False(KanalDruckverlust.IstKunststoff("Spiralrohr verzinkt"));
    }
}
