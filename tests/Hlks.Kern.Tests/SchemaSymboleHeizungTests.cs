using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil A-S 2: Schemasymbole Heizung und Kälte aus HKAP (HeizungSymbole). Die Referenz
/// <c>tests/daten/hkap/schema_symbole_heizung.json</c> stammt aus dem HKAP-Quelltext (werkzeuge/hkap_symbole_referenz: SchemaSymbole.Eingebaut,
/// HKAP-Millimeter, y nach unten). Der Test rechnet sie mit eigenem, vom Kern unabhängigem Code ins Zeichenmodell um und vergleicht Primitiv für
/// Primitiv. Umrechnung (Spezifikation, nicht der Kern): Skala 0.625; y gespiegelt; Bogen von = −(start + sweep), bis = von + sweep;
/// Rechteck → Polygon; Strichstärke 0 → duenn, sonst mittel; Fill ohne Farbe → «schnitt» (Planfarbe), «#FFFFFF» → «papier», Fill mit Farbe →
/// diese; Fläche ohne Füllung → durchsichtig, bei Armaturen und Zählern in der Leitung «papier»; Kälte ersetzt #CCFFE0 durch #CCE0FF;
/// Text: Mitte an der HKAP-Stelle, Grundlinie 0.35 · Höhe tiefer. Alle Elemente der Tests sind FIKTIV.
///
/// Handrechnung (Skala 0.625): Pumpe Ø 8 → Ø 5 (r 2.5); Sanduhr b = h = 3.4 → 2.125; Motorquadrat 5.2 → 3.25, Motorkreis r 2.3 → 1.4375;
/// Verteiler 70 × 8 → 43.75 × 5.</summary>
public class SchemaSymboleHeizungTests(ITestOutputHelper aus)
{
    const double Skala = 0.625;
    const double Tol = 1e-3;

    // ------------------------------------------------------------------ Referenz

    static readonly string ReferenzPfad = Path.Combine(AppContext.BaseDirectory, "hkap", "schema_symbole_heizung.json");

    static JsonObject Referenz() => (JsonObject)JsonNode.Parse(File.ReadAllText(ReferenzPfad))!;

    static JsonNode Sym(string key) => Referenz()["symbole"]!.AsArray().Single(s => (string)s!["key"]! == key)!;

    public static IEnumerable<object[]> Schluessel() =>
        Referenz()["symbole"]!.AsArray().Select(s => new object[] { (string)s!["key"]! });

    static readonly string[] ErwarteteKeys =
    [
        "kessel", "kessel_holz", "waermepumpe", "waermepumpe_luft", "fernwaerme", "solarkollektor", "elektroheizung", "bhkw",
        "speicher", "speicher_zylinder", "speicher_register", "speicher_2register", "boiler", "hydr_weiche", "ausdehnungsgefaess", "druckhaltung_kaelte", "druckhaltung",
        "regelventil_2weg", "mischer_3weg", "mischer_4weg", "umschaltventil", "motor", "zonenventil", "thermostatventil", "rl_verschraubung",
        "pumpe", "pumpe_geregelt", "doppelpumpe", "verteiler", "verteiler_klein",
        "absperrventil", "kugelhahn", "schieber", "klappe", "rueckschlagventil", "schmutzfaenger", "sicherheitsventil", "entlueftung", "entleerung",
        "strangregulierventil", "ueberstroemventil", "dd_regler", "druckreduzierventil",
        "fuehler", "aussenfuehler", "thermometer", "manometer", "waermezaehler", "wasserzaehler", "stroemungswaechter", "regler",
        "heizkoerper", "fussbodenheizung", "fbh_verteiler", "lufterhitzer", "heizgruppe", "verbraucher", "wt_platte", "wt_rohr",
    ];

    /// <summary>Armaturen und Zähler, die in einer Leitung sitzen (H+K-Legende): senkrecht gezeichnet bzw. liegend.</summary>
    static readonly string[] Senkrecht =
    [
        "pumpe", "pumpe_geregelt", "doppelpumpe", "absperrventil", "kugelhahn", "schieber", "schmutzfaenger", "strangregulierventil", "ueberstroemventil",
        "dd_regler", "druckreduzierventil", "regelventil_2weg", "mischer_3weg", "mischer_4weg", "umschaltventil", "zonenventil", "thermostatventil", "rl_verschraubung",
    ];
    static readonly string[] Waagrecht = ["klappe", "rueckschlagventil", "waermezaehler", "wasserzaehler"];

    // ------------------------------------------------------------------ Umrechnung aus der Referenz (unabhängig vom Kern)

    sealed record Erw(string Art, double[][] Punkte, double? Radius, double? Von, double? Bis, string? Fuellung, string Stift, string? Linie, string? Farbe,
        string? Text, double? Groesse, bool? Fett);

    static double D(JsonNode? n) => n!.GetValue<double>();

    static List<Erw> Erwartet(JsonNode sym, bool kaelte, bool gedreht)
    {
        string key = (string)sym["key"]!;
        bool inLeitung = Senkrecht.Contains(key) || Waagrecht.Contains(key);
        string? farbe = (string)sym["standardFarbe"]! is { Length: > 0 } f ? f : null;
        double[] Pt(double x, double y)
        {
            double a = x * Skala, b = -y * Skala;
            return gedreht ? [-b + 0.0, a] : [a, b + 0.0];
        }
        string? Fuell(JsonNode g)
        {
            bool fill = (bool)g["fill"]!; string fc = (string)g["fc"]!;
            if (!fill) return inLeitung ? "papier" : null;
            if (fc == "") return farbe ?? "schnitt";
            if (fc == "#FFFFFF") return "papier";
            return kaelte && fc == "#CCFFE0" ? "#CCE0FF" : fc;
        }
        var aus = new List<Erw>();
        foreach (var g in sym["geo"]!.AsArray())
        {
            var p = g!["p"]!.AsArray().Select(D).ToArray();
            string stift = D(g["w"]) > 0 ? "mittel" : "duenn";
            switch ((string)g["t"]!)
            {
                case "line":
                    aus.Add(new Erw("linie", [Pt(p[0], p[1]), Pt(p[2], p[3])], null, null, null, null, stift, (bool)g["dash"]! ? "strich" : "voll", farbe, null, null, null));
                    break;
                case "rect":
                    aus.Add(new Erw("polygon", [Pt(p[0], p[1]), Pt(p[0] + p[2], p[1]), Pt(p[0] + p[2], p[1] + p[3]), Pt(p[0], p[1] + p[3])], null, null, null, Fuell(g), stift, "voll", farbe, null, null, null));
                    break;
                case "circle":
                    aus.Add(new Erw("kreis", [Pt(p[0], p[1])], p[2] * Skala, null, null, Fuell(g), stift, "voll", farbe, null, null, null));
                    break;
                case "arc":
                {
                    double von = (-(p[3] + p[4]) + (gedreht ? 90 : 0)) % 360;
                    aus.Add(new Erw("bogen", [Pt(p[0], p[1])], p[2] * Skala, von, von + p[4], null, stift, "voll", farbe, null, null, null));
                    break;
                }
                case "poly":
                {
                    var pts = new List<double[]>();
                    for (int i = 0; i + 1 < p.Length; i += 2) pts.Add(Pt(p[i], p[i + 1]));
                    aus.Add((bool)g["close"]!
                        ? new Erw("polygon", [.. pts], null, null, null, Fuell(g), stift, "voll", farbe, null, null, null)
                        : new Erw("polylinie", [.. pts], null, null, null, null, stift, "voll", farbe, null, null, null));
                    break;
                }
                case "text":
                {
                    string s = (string)g["s"]!;
                    if (s.StartsWith('$')) { s = s[1..]; if (s is "1" or "2") continue; }
                    double h = D(g["size"]) * Skala;
                    var m = Pt(p[0], p[1]);
                    aus.Add(new Erw("text", [[m[0], m[1] - h * 0.35]], null, null, null, null, "duenn", "voll", farbe, s, h, (bool)g["bold"]! ? true : null));
                    break;
                }
            }
        }
        return aus;
    }

    static void GleichPunkte(double[][] soll, List<double[]> ist, string was)
    {
        Assert.True(soll.Length == ist.Count, $"{was}: {soll.Length} Punkte erwartet, {ist.Count} gezeichnet");
        for (int i = 0; i < soll.Length; i++)
            for (int j = 0; j < 2; j++)
                Assert.True(Math.Abs(soll[i][j] - ist[i][j]) < Tol, $"{was}: Punkt {i}[{j}] erwartet {soll[i][j]}, ist {ist[i][j]}");
    }

    static double Winkel(double w) => ((w % 360) + 360) % 360;

    static void Vergleichen(string key, List<Erw> soll, List<Primitiv> ist, string variante)
    {
        Assert.True(soll.Count == ist.Count, $"{key} {variante}: {soll.Count} Primitive erwartet, {ist.Count} gezeichnet");
        for (int i = 0; i < soll.Count; i++)
        {
            var e = soll[i]; var p = ist[i]; string was = $"{key} {variante} #{i} ({e.Art})";
            Assert.True(e.Art == p.Art, $"{was}: Art {p.Art}");
            GleichPunkte(e.Punkte, p.Punkte, was);
            if (e.Radius is double r) Assert.True(Math.Abs(r - p.Radius!.Value) < Tol, $"{was}: Radius {p.Radius}, erwartet {r}");
            if (e.Von is double v)
            {
                Assert.True(Math.Abs(Winkel(v) - Winkel(p.Von!.Value)) < Tol, $"{was}: von {p.Von}, erwartet {v}");
                Assert.True(Math.Abs((e.Bis!.Value - v) - (p.Bis!.Value - p.Von!.Value)) < Tol, $"{was}: Öffnungswinkel");
            }
            if (e.Art == "text")
            {
                Assert.Equal(e.Text, p.Text);
                Assert.True(Math.Abs(e.Groesse!.Value - p.Groesse!.Value) < Tol, $"{was}: Schrifthöhe");
                Assert.Equal("mitte", p.Ausrichtung);
                Assert.Equal(e.Fett, p.Fett);
                Assert.Null(p.Stil.Stift);
            }
            else
            {
                Assert.True(e.Stift == p.Stil.Stift, $"{was}: Stift {p.Stil.Stift}, erwartet {e.Stift}");
                Assert.True(e.Linie == p.Stil.Linie, $"{was}: Linie {p.Stil.Linie}, erwartet {e.Linie}");
                Assert.True(e.Fuellung == p.Stil.Fuellung, $"{was}: Füllung {p.Stil.Fuellung ?? "keine"}, erwartet {e.Fuellung ?? "keine"}");
            }
            Assert.True(e.Farbe == p.Stil.Farbe, $"{was}: Farbe {p.Stil.Farbe}, erwartet {e.Farbe}");
        }
    }

    // ------------------------------------------------------------------ Bibliothek gegen Referenz

    [Fact]
    public void Referenz_StammtAusDerQuelleUndHatDieGeforderteListe()
    {
        var r = Referenz();
        Assert.Equal(59, (int)r["anzahl"]!);
        Assert.Matches("^[0-9a-f]{64}$", (string)r["sha256"]!);                       // Herkunft: Hash der HKAP-Quelldatei
        Assert.Equal(ErwarteteKeys.Order(), r["symbole"]!.AsArray().Select(s => (string)s!["key"]!).Order());
        Assert.Equal(59, ErwarteteKeys.Distinct().Count());
        aus.WriteLine($"Referenz: {r["quelle"]}, sha256 {r["sha256"]}, {r["zeilen"]} Zeilen, erzeugt {r["erzeugt"]}");
    }

    [Fact]
    public void Bibliothek_HatGenauDieSymboleDerReferenz_MitBenennungDerLegende()
    {
        var ref0 = Referenz()["symbole"]!.AsArray();
        Assert.Equal(ref0.Select(s => (string)s!["key"]!), HeizungSymbole.Alle.Select(s => s.Key));          // Reihenfolge wie die HKAP
        Assert.Equal(ErwarteteKeys.Order(), HeizungSymbole.Bibliothek.Keys.Order());
        foreach (var r in ref0)
        {
            var s = HeizungSymbole.Bibliothek[(string)r!["key"]!];
            Assert.Equal((string)r["gruppe"]!, s.Gruppe);
            Assert.Equal((string)r["label"]!, s.Label);
            Assert.Equal(D(r["w"]), s.W);
            Assert.Equal(D(r["h"]), s.H);
            Assert.Equal((string)r["labelPos"]!, s.LabelPos);
            Assert.Equal((string)r["standardFarbe"]!, s.StandardFarbe);
            Assert.Equal(r["geo"]!.AsArray().Count, s.Geo.Count);
            Assert.False(string.IsNullOrWhiteSpace(s.Label));                  // Benennung für die Legende
        }
        Assert.Equal(9, HeizungSymbole.Alle.Select(s => s.Gruppe).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Geometrie_JedesSymbol_GegenDieHkapQuelle(string key)
    {
        var sym = Sym(key);
        var s = HeizungSymbole.Bibliothek[key];
        foreach (var kaelte in new[] { false, true })
            foreach (var gedreht in new[] { false, true })
                Vergleichen(key, Erwartet(sym, kaelte, gedreht), HeizungSymbole.Primitive(s, kaelte, gedreht), $"(Kälte {kaelte}, gedreht {gedreht})");
    }

    [Theory]
    [MemberData(nameof(Schluessel))]
    public void Anschluesse_JedesSymbol_NamenLagenRichtungen(string key)
    {
        var sym = Sym(key);
        var s = HeizungSymbole.Bibliothek[key];
        foreach (var gedreht in new[] { false, true })
        {
            var soll = sym["anschluesse"]!.AsArray().Select(a =>
            {
                double x = D(a!["x"]) * Skala, y = -D(a["y"]) * Skala;
                string dir = (string)a["dir"]!;
                return gedreht
                    ? ((string)a["id"]!, -y + 0.0, x, dir switch { "N" => "W", "W" => "S", "S" => "E", _ => "N" })
                    : ((string)a["id"]!, x, y + 0.0, dir);
            }).ToList();
            var ist = HeizungSymbole.Anschluesse(s, gedreht);
            Assert.Equal(soll.Count, ist.Count);
            for (int i = 0; i < soll.Count; i++)
            {
                Assert.Equal(soll[i].Item1, ist[i].Id);
                Assert.True(Math.Abs(soll[i].Item2 - ist[i].X) < Tol && Math.Abs(soll[i].Item3 - ist[i].Y) < Tol, $"{key} {ist[i].Id}: ({ist[i].X}, {ist[i].Y}) statt ({soll[i].Item2}, {soll[i].Item3})");
                Assert.Equal(soll[i].Item4, ist[i].Dir);
            }
        }
    }

    [Fact]
    public void Einbaulage_SenkrechteUndLiegendeArmaturen_HabenPassendeAnschluesse()
    {
        foreach (var s in HeizungSymbole.Alle)
        {
            var dirs = s.Anschluesse.Select(a => a.Dir).ToHashSet();
            if (Senkrecht.Contains(s.Key)) { Assert.Equal(HzLage.Senkrecht, s.Lage); Assert.True(dirs.Contains("N") && dirs.Contains("S"), $"{s.Key}: N und S fehlen"); }
            else if (Waagrecht.Contains(s.Key)) { Assert.Equal(HzLage.Waagrecht, s.Lage); Assert.True(dirs.Contains("W") && dirs.Contains("E"), $"{s.Key}: W und E fehlen"); }
            else Assert.Equal(HzLage.Fest, s.Lage);
        }
    }

    // ------------------------------------------------------------------ Regeln der H+K-Legende (von Hand geprüft)

    [Fact]
    public void Legende_Pumpe_KreisMitDurchmesserUndGefuelltemDreieck()
    {
        var pr = HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["pumpe"]);
        var kreis = Assert.Single(pr, x => x.Art == "kreis");
        Assert.Equal(2.5, kreis.Radius!.Value, 4);                       // Ø 8 → Ø 5
        var dreieck = Assert.Single(pr, x => x.Art == "polygon");
        Assert.Equal("schnitt", dreieck.Stil.Fuellung);                  // ausgefüllt
        Assert.Contains(dreieck.Punkte, q => Math.Abs(q[0]) < 1e-9 && Math.Abs(q[1] - 2.5) < 1e-9);   // Spitze am oberen Kreisrand (y nach oben)
        Assert.Contains(dreieck.Punkte, q => Math.Abs(q[0] + 2.5) < 1e-9 && Math.Abs(q[1]) < 1e-9);   // Basis = waagrechter Durchmesser
        var durchmesser = Assert.Single(pr, x => x.Art == "linie");
        Assert.Equal([[-2.5, 0.0], [2.5, 0.0]], durchmesser.Punkte);
        var an = HeizungSymbole.Anschluesse(HeizungSymbole.Bibliothek["pumpe"]);
        Assert.Equal([new HzPunkt("1", 0, 2.5, "N"), new HzPunkt("2", 0, -2.5, "S")], an);
    }

    [Fact]
    public void Legende_DreiWegVentil_ObererWegUndRechterFluegelGefuellt()
    {
        var pr = HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["mischer_3weg"]);
        var polys = pr.Where(x => x.Art == "polygon" && x.Punkte.Count == 3).ToList();
        Assert.Equal(3, polys.Count);
        var oben = polys.Single(x => x.Punkte.All(q => q[1] >= 0) && x.Punkte.Any(q => q[1] > 1));     // Basis oben (y > 0)
        var unten = polys.Single(x => x.Punkte.All(q => q[1] <= 0) && x.Punkte.Any(q => q[1] < -1));
        var rechts = polys.Single(x => x.Punkte.All(q => q[0] >= 0) && x.Punkte.Any(q => q[0] > 2.5));
        Assert.Equal("schnitt", oben.Stil.Fuellung);
        Assert.Equal("papier", unten.Stil.Fuellung);                      // Armatur in der Leitung: nicht ausgefüllt, aber deckend
        Assert.Equal("schnitt", rechts.Stil.Fuellung);
        Assert.Equal(2.875, rechts.Punkte.Max(q => q[0]), 4);             // Flügel ragt 4.6 · 0.625 hinaus, weiter als die Sanduhr (2.125)
        var an = HeizungSymbole.Anschluesse(HeizungSymbole.Bibliothek["mischer_3weg"]);
        Assert.Equal(["A", "B", "AB"], an.Select(a => a.Id));
        Assert.Equal(["N", "S", "E"], an.Select(a => a.Dir));
    }

    [Fact]
    public void Legende_Motor_QuadratGrosserKreisUndM()
    {
        var pr = HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["motor"]);
        var quadrat = Assert.Single(pr, x => x.Art == "polygon");
        Assert.Equal(3.25, quadrat.Punkte.Max(q => q[0]) - quadrat.Punkte.Min(q => q[0]), 4);
        Assert.Equal(1.4375, Assert.Single(pr, x => x.Art == "kreis").Radius!.Value, 4);
        Assert.Equal("M", Assert.Single(pr, x => x.Art == "text").Text);
        Assert.Null(quadrat.Stil.Fuellung);                               // Apparat ausserhalb der Leitung: durchsichtig
    }

    [Fact]
    public void Legende_Kugelhahn_KreisWeissAusgespart()
    {
        var pr = HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["kugelhahn"]);
        Assert.Equal("papier", Assert.Single(pr, x => x.Art == "kreis").Stil.Fuellung);
    }

    [Fact]
    public void Legende_Schmutzfaenger_GestrichelteMittellinie()
    {
        var pr = HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["schmutzfaenger"]);
        Assert.Equal("strich", Assert.Single(pr, x => x.Art == "linie").Stil.Linie);
    }

    [Fact]
    public void Texte_PlatzhalterBleibenLeer_BisDerAufruferSieFuellt()
    {
        var sp = HeizungSymbole.Bibliothek["speicher"];
        Assert.DoesNotContain(HeizungSymbole.Primitive(sp), x => x.Art == "text");
        var mit = HeizungSymbole.Primitive(sp, texte: new Dictionary<string, string> { ["1"] = "SP 1000", ["2"] = "Σ 24 kW" });
        Assert.Equal(["SP 1000", "Σ 24 kW"], mit.Where(x => x.Art == "text").Select(x => x.Text));
        Assert.Equal("MSR", Assert.Single(HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["regler"]), x => x.Art == "text").Text);   // $MSR: Vorgabewert
        Assert.Equal("FS", Assert.Single(HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["stroemungswaechter"]), x => x.Art == "text").Text);
    }

    // ------------------------------------------------------------------ Zuordnung Klasse / Art → Symbol

    static Projekt Leer() => Projekt.Anlegen(Kat, null, "Test Heizungssymbole (fiktiv)", "T", "pc");

    static void System(Projekt p, string kz, string systemtyp) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = systemtyp } }));

    static Element Anlegen(Projekt p, string klasse, string kz, string? art = null, string? system = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (art is not null) o["eigenschaften"] = new JsonObject { ["art"] = art };
        if (system is not null) o["system"] = system;
        Ok(p.Ausfuehren("element_anlegen", o));
        return Hole(p, kz);
    }

    [Theory]
    [InlineData("Waermeerzeuger", "waermepumpe", "waermepumpe")]
    [InlineData("Waermeerzeuger", "gas", "kessel")]
    [InlineData("Waermeerzeuger", "oel", "kessel")]
    [InlineData("Waermeerzeuger", "holz", "kessel_holz")]
    [InlineData("Waermeerzeuger", "fernwaerme", "fernwaerme")]
    [InlineData("Speicher", null, "speicher")]
    [InlineData("Verteiler", null, "verteiler")]
    [InlineData("Heizkoerper", null, "heizkoerper")]
    [InlineData("Fussbodenheizkreis", null, "fussbodenheizung")]
    [InlineData("Pumpe", null, "pumpe")]
    [InlineData("Waermezaehler", null, "waermezaehler")]
    [InlineData("Ventil", "absperrventil", "absperrventil")]
    [InlineData("Ventil", "regulierventil", "regelventil_2weg")]
    [InlineData("Ventil", "rueckflussverhinderer", "rueckschlagventil")]
    [InlineData("Ventil", "druckreduzierventil", "druckreduzierventil")]
    [InlineData("Ventil", "mischventil", "mischer_3weg")]
    public void Zuordnung_BestehendeKlassen(string klasse, string? art, string key)
    {
        var p = Leer();
        System(p, "HZ", "Heizung Vorlauf");
        var e = Anlegen(p, klasse, "X-1", art, Kat.Klassen[klasse].IstArt("Armatur") ? "HZ" : null);
        var s = HeizungSymbole.SymbolVon(p.Katalog, e);
        Assert.NotNull(s);
        Assert.Equal(key, s!.Key);
        Assert.Equal("heizsymbol", SchemaSymbole.Schluessel(p, e));
        var (prims, hx, hy) = SchemaSymbole.Zeichnen(p, e, 100, 50, true, "#8b5a2b", "mittel");
        Assert.NotEmpty(prims);
        Assert.All(prims.Where(x => x.Stil.Farbe is not null), x => Assert.NotEqual("#8b5a2b", x.Stil.Farbe));   // Planfarbe der H+K-Symbole, nicht die Medienfarbe
        Assert.True(hx > 0 && hy > 0);
    }

    [Theory]
    [InlineData("Ventil", "magnetventil")]            // kein Symbol in der HKAP-Liste: bleibt beim Ersatzbild
    [InlineData("Waermeerzeuger", null)]            // Wärmeerzeuger ohne art: keine Vermutung, Ersatzbild
    public void Zuordnung_OhneSymbol_BleibtBeimErsatzbild(string klasse, string? art)
    {
        var p = Leer();
        System(p, "HZ", "Heizung Vorlauf");
        var e = Anlegen(p, klasse, "X-1", art, klasse == "Ventil" ? "HZ" : null);
        Assert.Null(HeizungSymbole.SymbolVon(p.Katalog, e));
        Assert.NotEqual("heizsymbol", SchemaSymbole.Schluessel(p, e));
        Assert.NotEmpty(SchemaSymbole.Zeichnen(p, e, 0, 0, false, null, "mittel").Primitive);
    }

    [Fact]
    public void Zuordnung_Tabelle_JederSchluesselExistiert_KeineDoppelteZeile()
    {
        Assert.All(HeizungSymbole.Zuordnung, z => Assert.True(HeizungSymbole.Bibliothek.ContainsKey(z.Key), $"{z.Klasse}/{z.Art}: {z.Key} fehlt in der Bibliothek"));
        Assert.Equal(HeizungSymbole.Zuordnung.Count, HeizungSymbole.Zuordnung.Select(z => (z.Klasse, z.Art)).Distinct().Count());
        Assert.All(HeizungSymbole.KaelteVariante, kv => { Assert.True(HeizungSymbole.Bibliothek.ContainsKey(kv.Key)); Assert.True(HeizungSymbole.Bibliothek.ContainsKey(kv.Value)); });
        // vorbereitet für Modul 5 A-D (Klassen und Arten stehen schon in der Tabelle)
        foreach (var (klasse, art, key) in new[]
        {
            ("Mischventil", "3weg", "mischer_3weg"), ("Mischventil", "4weg", "mischer_4weg"), ("Mischventil", "umschalt", "umschaltventil"),
            ("Ausdehnungsgefaess", null, "ausdehnungsgefaess"), ("Waermetauscher", "platte", "wt_platte"), ("Waermetauscher", "rohr", "wt_rohr"),
            ("Fuehler", "temperatur", "fuehler"), ("Fuehler", "thermometer", "thermometer"), ("Fuehler", "manometer", "manometer"), ("Fuehler", "stroemung", "stroemungswaechter"),
            ("Kaelteerzeuger", null, "waermepumpe"),
            ("Ventil", "strangregulierventil", "strangregulierventil"), ("Ventil", "sicherheitsventil", "sicherheitsventil"), ("Ventil", "ueberstroemventil", "ueberstroemventil"),
            ("Ventil", "thermostatventil", "thermostatventil"), ("Ventil", "zonenventil", "zonenventil"), ("Ventil", "entlueftung", "entlueftung"),
            ("Ventil", "entleerung", "entleerung"), ("Ventil", "schmutzfaenger", "schmutzfaenger"),
        })
            Assert.Contains(HeizungSymbole.Zuordnung, z => z.Klasse == klasse && z.Art == art && z.Key == key);
    }

    [Fact]
    public void Zuordnung_KlasseFehltImKatalog_GreiftNichtUndStuerztNicht()
    {
        var p = Leer();
        var e = Anlegen(p, "Heizkoerper", "HK-1");
        Assert.NotNull(HeizungSymbole.SymbolVon(p.Katalog, e));
        var ohne = Katalog.Laden(Katalog.StandardOrdner());
        ohne.Klassen.Remove("Heizkoerper");
        Assert.Null(HeizungSymbole.SymbolVon(ohne, e));                                     // Zeile bleibt in der Tabelle, greift aber nicht
        Assert.Equal(Kat.Klassen.ContainsKey("Mischventil"), HeizungSymbole.SymbolVon(Kat, MitArt(p, "Mischventil", "3weg")) is not null);
    }

    /// <summary>Element der Klasse (auch wenn sie dem Katalog fehlt) nur zum Befragen der Zuordnung, nicht im Modell.</summary>
    static Element MitArt(Projekt p, string klasse, string art) =>
        Kat.Klassen.ContainsKey(klasse) ? Anlegen(p, klasse, "Z-" + klasse, art) : new Element { Klasse = klasse, Kennzeichen = "Z-" + klasse };

    // ------------------------------------------------------------------ Hinterlegung

    static Primitiv ErstesPolygon(IEnumerable<Primitiv> pr) => pr.First(x => x.Art == "polygon");

    [Fact]
    public void Hinterlegung_ApparateDerHeizung_Gruen()
    {
        var p = Leer();
        foreach (var (klasse, art) in new (string, string?)[] { ("Heizkoerper", null), ("Speicher", null), ("Fussbodenheizkreis", null), ("Waermeerzeuger", "gas"), ("Waermeerzeuger", "waermepumpe") })
        {
            var e = Anlegen(p, klasse, $"{klasse}-{art}", art);
            var (prims, _, _) = SchemaSymbole.Zeichnen(p, e, 0, 0, false, null, "mittel");
            Assert.Contains(prims, x => x.Stil.Fuellung == "#CCFFE0");
            Assert.DoesNotContain(prims, x => x.Stil.Fuellung == "#CCE0FF");
        }
        Assert.Equal("#CCFFE0", HeizungSymbole.FlaecheApparat);
        Assert.Equal("#E4D6EA", HeizungSymbole.FlaecheGefaess);
        Assert.Equal("#CCE0FF", HeizungSymbole.FlaecheApparatKaelte);
    }

    [Fact]
    public void Hinterlegung_ApparatMitKaeltemedium_Blau()
    {
        var p = Leer();
        System(p, "KAE", "Kälte Vorlauf");
        System(p, "HZ", "Heizung Vorlauf");
        var kalt = Anlegen(p, "Heizkoerper", "HK-K", null, "KAE");
        var warm = Anlegen(p, "Heizkoerper", "HK-W", null, "HZ");
        Assert.True(HeizungSymbole.IstKaelte(p, kalt));
        Assert.False(HeizungSymbole.IstKaelte(p, warm));
        var polK = ErstesPolygon(SchemaSymbole.Zeichnen(p, kalt, 0, 0, false, null, "mittel").Primitive);
        var polW = ErstesPolygon(SchemaSymbole.Zeichnen(p, warm, 0, 0, false, null, "mittel").Primitive);
        Assert.Equal("#CCE0FF", polK.Stil.Fuellung);
        Assert.Equal("#CCFFE0", polW.Stil.Fuellung);
        // alte Schreibweise des Mediums (KUE-VL) gilt ebenfalls als Kälte
        Assert.Equal("KAE-VL", KaelteAlias.Medium("KUE-VL"));
    }

    [Fact]
    public void Hinterlegung_Gefaess_Violett_AuchInDerKaelte_UndKaelteVariante()
    {
        foreach (var key in new[] { "ausdehnungsgefaess", "druckhaltung_kaelte", "druckhaltung" })
            foreach (var kaelte in new[] { false, true })
                Assert.Equal("#E4D6EA", ErstesPolygon(HeizungSymbole.Primitive(HeizungSymbole.Bibliothek[key], kaelte)).Stil.Fuellung);
        Assert.Equal("#FF0000", HeizungSymbole.Bibliothek["ausdehnungsgefaess"].StandardFarbe);          // an der Heizung rot (HKAP)
        Assert.Equal("#FF00CC", HeizungSymbole.Bibliothek["druckhaltung_kaelte"].StandardFarbe);        // in der Kälte magenta
        Assert.All(HeizungSymbole.Primitive(HeizungSymbole.Bibliothek["druckhaltung_kaelte"]).Where(x => x.Art != "text"), x => Assert.Equal("#FF00CC", x.Stil.Farbe));
        Assert.Equal("druckhaltung_kaelte", HeizungSymbole.KaelteVariante["ausdehnungsgefaess"]);
    }

    [Fact]
    public void Hinterlegung_NurDieGruenfuellungWirdInDerKaelteBlau()
    {
        foreach (var s in HeizungSymbole.Alle)
        {
            var warm = HeizungSymbole.Primitive(s, false).Select(x => x.Stil.Fuellung).ToList();
            var kalt = HeizungSymbole.Primitive(s, true).Select(x => x.Stil.Fuellung).ToList();
            for (int i = 0; i < warm.Count; i++)
                Assert.Equal(warm[i] == "#CCFFE0" ? "#CCE0FF" : warm[i], kalt[i]);
        }
    }

    // ------------------------------------------------------------------ Familie hat Vorrang; Lage in der Leitung

    [Fact]
    public void Familie_MitSchemasymbol_GehtDemHeizungssymbolVor()
    {
        var p = Leer();
        var def = FamilieTestHilfe.Testheizkoerper();
        def["schemasymbol"] = JsonNode.Parse("""[ { "art": "kreis", "punkte": [[0, 0]], "radius": 1.5 } ]""");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = def }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(1000, 1000, 0), ["kennzeichen"] = "HK-F" }));
        var e = Hole(p, "HK-F");
        Assert.NotNull(HeizungSymbole.SymbolVon(p.Katalog, e));            // die Klasse hätte ein Heizungssymbol …
        Assert.Equal("familie", SchemaSymbole.Schluessel(p, e));           // … die Familie geht vor
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, e, 10, 20, false, null, "mittel");
        var kreis = Assert.Single(prims);
        Assert.Equal("kreis", kreis.Art);
        Assert.DoesNotContain(prims, x => x.Stil.Fuellung == "#CCFFE0");
    }

    [Fact]
    public void Zeichnen_ArmaturenDrehenMitDerLeitung_ApparateStehenAufrecht()
    {
        var p = Leer();
        System(p, "HZ", "Heizung Vorlauf");
        var mischer = Anlegen(p, "Ventil", "MV-1", "mischventil", "HZ");        // senkrecht gezeichnet, Motor links
        var (_, sx, sy) = SchemaSymbole.Zeichnen(p, mischer, 0, 0, true, null, "mittel");    // senkrechte Leitung: Symbol wie gezeichnet
        var (_, wx, wy) = SchemaSymbole.Zeichnen(p, mischer, 0, 0, false, null, "mittel");   // waagrechte Leitung: um 90° gedreht
        Assert.True(sx > sy, $"senkrechte Leitung: Motor liegt seitlich, hx {sx} hy {sy}");
        Assert.True(wy > wx, $"waagrechte Leitung: gedreht, hx {wx} hy {wy}");
        Assert.Equal(sx, wy, 3);
        Assert.Equal(sy, wx, 3);
        // liegender Wärmezähler: waagrecht gezeichnet, an senkrechter Leitung gedreht
        var wmz = Anlegen(p, "Waermezaehler", "WMZ-1", null, "HZ");
        var (_, ax, ay) = SchemaSymbole.Zeichnen(p, wmz, 0, 0, false, null, "mittel");
        var (_, bx, by) = SchemaSymbole.Zeichnen(p, wmz, 0, 0, true, null, "mittel");
        Assert.True(ax > ay);
        Assert.True(by > bx);
        // Heizkörper: nie gedreht
        var hk = Anlegen(p, "Heizkoerper", "HK-1");
        Assert.Equal(SchemaSymbole.Zeichnen(p, hk, 0, 0, false, null, "mittel").HalbX, SchemaSymbole.Zeichnen(p, hk, 0, 0, true, null, "mittel").HalbX);
    }

    [Fact]
    public void Heizkoerper_ZeigtDasSymbolStattDesKaestchensHK()
    {
        // vorher: Kästchen «HK» (Klassendarstellung «apparat»); jetzt das Heizkörpersymbol der H+K-Legende (Umriss, drei Rippen), ohne Kürzel
        var p = Leer();
        var e = Anlegen(p, "Heizkoerper", "HK-1");
        Assert.Equal("heizsymbol", SchemaSymbole.Schluessel(p, e));
        Assert.Null(SchemaSymbole.SinnbildVon(p.Katalog, e));
        var (prims, hx, hy) = SchemaSymbole.Zeichnen(p, e, 0, 0, false, null, "mittel");
        Assert.DoesNotContain(prims, x => x.Art == "text");
        Assert.Equal(6.25, hx, 4);                    // 20 · 0.625 / 2
        Assert.Equal(3.4375, hy, 4);                  // 11 · 0.625 / 2
        Assert.Equal(1 + 3, prims.Count);             // Rahmen + drei Rippen
    }

    // ------------------------------------------------------------------ Übersichtsbild

    [Fact]
    public void Uebersicht_AllerSymbole_AlsSvg()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "symbole_heizung");
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, "uebersicht.svg");
        File.WriteAllText(pfad, Svg(), new UTF8Encoding(false));
        aus.WriteLine($"Übersicht: {pfad}");
        var svg = File.ReadAllText(pfad);
        foreach (var s in HeizungSymbole.Alle) Assert.Contains(s.Key, svg);
        Assert.StartsWith("<svg", svg);
    }

    const double Px = 6;          // Pixel je mm der Übersicht
    const double Zoom = 2;        // Vergrösserung der Symbole gegenüber dem Papier (nur in der Übersicht)
    const double ZelleB = 96, ZelleH = 68;
    static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;");

    string Svg()
    {
        var sb = new StringBuilder();
        const int Spalten = 5;
        var zellen = new List<(string Titel, List<Primitiv> Prims, List<HzPunkt> Ports, string Gruppe)>();
        foreach (var s in HeizungSymbole.Alle) zellen.Add(($"{s.Key} - {s.Label}", HeizungSymbole.Primitive(s), HeizungSymbole.Anschluesse(s), s.Gruppe));
        foreach (var key in new[] { "kessel", "waermepumpe", "speicher", "heizkoerper", "fussbodenheizung", "wt_platte", "lufterhitzer" })
        {
            var s = HeizungSymbole.Bibliothek[key];
            zellen.Add(($"{key} - Kaelte (#CCE0FF)", HeizungSymbole.Primitive(s, true), HeizungSymbole.Anschluesse(s), "Kälte-Hinterlegung"));
        }
        foreach (var key in new[] { "absperrventil", "regelventil_2weg", "mischer_3weg", "pumpe", "rueckschlagventil", "waermezaehler", "strangregulierventil" })
        {
            var s = HeizungSymbole.Bibliothek[key];
            bool gedreht = HeizungSymbole.Gedreht(s, s.Lage == HzLage.Waagrecht);   // Senkrecht-Armatur an waagrechter Leitung bzw. Waagrecht-Armatur an senkrechter
            zellen.Add(($"{key} - gedreht", HeizungSymbole.Primitive(s, false, gedreht), HeizungSymbole.Anschluesse(s, gedreht), "Waagrechter Einbau (um 90 Grad gedreht)"));
        }
        // Gruppenköpfe: jede Gruppe beginnt in einer neuen Zeile
        var zeilen = new List<(string Kopf, List<(string Titel, List<Primitiv> Prims, List<HzPunkt> Ports, string Gruppe)> Z)>();
        foreach (var g in zellen.GroupBy(z => z.Gruppe)) zeilen.Add((g.Key, g.ToList()));
        double kopfH = 8, y = 4;
        var teile = new StringBuilder();
        foreach (var (kopf, z) in zeilen)
        {
            teile.Append($"<text x=\"{N(2 * Px)}\" y=\"{N((y + 5) * Px)}\" font-family=\"Segoe UI, Arial\" font-size=\"{N(4 * Px)}\" font-weight=\"bold\">{Esc(kopf)}</text>\n");
            y += kopfH;
            for (int i = 0; i < z.Count; i++)
            {
                int sp = i % Spalten, zl = i / Spalten;
                double ox = 2 + sp * ZelleB, oy = y + zl * ZelleH;
                teile.Append($"<rect x=\"{N(ox * Px)}\" y=\"{N(oy * Px)}\" width=\"{N((ZelleB - 2) * Px)}\" height=\"{N((ZelleH - 2) * Px)}\" fill=\"none\" stroke=\"#ccc\" stroke-width=\"1\"/>\n");
                teile.Append($"<text x=\"{N((ox + 1) * Px)}\" y=\"{N((oy + 3.2) * Px)}\" font-family=\"Segoe UI, Arial\" font-size=\"{N(2.4 * Px)}\" fill=\"#555\">{Esc(z[i].Titel)}</text>\n");
                var b = Grafik.Begrenzung([new Grafikteil { Primitive = z[i].Prims }]) ?? [0, 0, 0, 0];
                double cx = ox + (ZelleB - 2) / 2 - Zoom * (b[0] + b[2]) / 2, cy = oy + 3 + (ZelleH - 5) / 2 + Zoom * (b[1] + b[3]) / 2;   // y nach unten im Bild
                teile.Append($"<g transform=\"translate({N(cx * Px)} {N(cy * Px)}) scale({N(Px * Zoom)} {N(-Px * Zoom)})\">\n");
                foreach (var pr in z[i].Prims) teile.Append(SvgPrimitiv(pr));
                teile.Append("</g>\n");
                foreach (var a in z[i].Ports)
                {
                    teile.Append($"<circle cx=\"{N((cx + a.X * Zoom) * Px)}\" cy=\"{N((cy - a.Y * Zoom) * Px)}\" r=\"{N(0.45 * Px)}\" fill=\"#d00\"/>");
                    teile.Append($"<text x=\"{N((cx + a.X * Zoom + 0.7) * Px)}\" y=\"{N((cy - a.Y * Zoom - 0.5) * Px)}\" font-family=\"Segoe UI, Arial\" font-size=\"{N(1.4 * Px)}\" fill=\"#d00\">{Esc(a.Id)}</text>\n");
                }
            }
            y += Math.Ceiling(z.Count / (double)Spalten) * ZelleH + 2;
        }
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N((2 + Spalten * ZelleB) * Px)}\" height=\"{N((y + 2) * Px)}\" viewBox=\"0 0 {N((2 + Spalten * ZelleB) * Px)} {N((y + 2) * Px)}\">\n");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>\n");
        sb.Append("<!-- HLKS-CAD Modul 5 A-S 2: Heizungs- und Kaeltesymbole (Papier-mm, Skala 0.625 gegenueber HKAP, hier zusaetzlich 2fach vergroessert). Rot: Anschlusspunkte. Nicht im Repository. -->\n");
        sb.Append(teile);
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // Das Symbol wird in einer Gruppe mit y-Spiegelung gezeichnet (Kern: y nach oben); Texte stehen deshalb in einer eigenen Umkehrung.
    static string SvgPrimitiv(Primitiv pr)
    {
        string Farbe(string? f) => f ?? "#000";
        string Fill(string? f) => f switch { null => "none", "papier" => "#fff", "schnitt" => "#000", _ => f };
        double w = Stifte.Mm(pr.Stil.Stift);
        string Strich() => $"stroke=\"{Farbe(pr.Stil.Farbe)}\" stroke-width=\"{N(w)}\" stroke-linejoin=\"round\"{(pr.Stil.Linie == "strich" ? " stroke-dasharray=\"0.8 0.5\"" : "")}";
        string Pkte(IEnumerable<double[]> q) => string.Join(" ", q.Select(t => $"{N(t[0])},{N(t[1])}"));
        switch (pr.Art)
        {
            case "linie": return $"<line x1=\"{N(pr.Punkte[0][0])}\" y1=\"{N(pr.Punkte[0][1])}\" x2=\"{N(pr.Punkte[1][0])}\" y2=\"{N(pr.Punkte[1][1])}\" {Strich()}/>\n";
            case "polylinie": return $"<polyline points=\"{Pkte(pr.Punkte)}\" fill=\"none\" {Strich()}/>\n";
            case "polygon": return $"<polygon points=\"{Pkte(pr.Punkte)}\" fill=\"{Fill(pr.Stil.Fuellung)}\" {Strich()}/>\n";
            case "kreis": return $"<circle cx=\"{N(pr.Punkte[0][0])}\" cy=\"{N(pr.Punkte[0][1])}\" r=\"{N(pr.Radius!.Value)}\" fill=\"{Fill(pr.Stil.Fuellung)}\" {Strich()}/>\n";
            case "bogen":
            {
                double r = pr.Radius!.Value, cx = pr.Punkte[0][0], cy = pr.Punkte[0][1], von = pr.Von!.Value * Math.PI / 180, bis = pr.Bis!.Value * Math.PI / 180;
                double x0 = cx + r * Math.Cos(von), y0 = cy + r * Math.Sin(von), x1 = cx + r * Math.Cos(bis), y1 = cy + r * Math.Sin(bis);
                int gross = pr.Bis!.Value - pr.Von!.Value > 180 ? 1 : 0;
                return $"<path d=\"M {N(x0)} {N(y0)} A {N(r)} {N(r)} 0 {gross} 1 {N(x1)} {N(y1)}\" fill=\"none\" {Strich()}/>\n";   // Gegenuhrzeigersinn bei y nach oben = sweep 1 in der gespiegelten Gruppe
            }
            case "text":
                return $"<text transform=\"translate({N(pr.Punkte[0][0])} {N(pr.Punkte[0][1])}) scale(1 -1)\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial\" font-size=\"{N(pr.Groesse!.Value)}\"{(pr.Fett == true ? " font-weight=\"bold\"" : "")} fill=\"{Farbe(pr.Stil.Farbe)}\">{Esc(pr.Text!)}</text>\n";
            default: return "";
        }
    }
}
