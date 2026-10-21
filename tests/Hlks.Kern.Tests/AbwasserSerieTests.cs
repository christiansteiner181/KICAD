using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.IfcExportHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag Modul 3: Abwasser-Serien nach Aussendurchmesser (entwurf/abwasser-serie.md; Entscheid Christian 20.10.2026: «Geberit PE
/// ist immer Abwasser» – Grösse und Beschriftung nach Aussendurchmesser, keine DN nötig, Prüfbefund in Heizung, Kälte, Trinkwasser; Regel
/// Christian: DN nie aus dem Aussendurchmesser abgeleitet, die DN des Herstellers darf als Angabe stehen). Handrechnungen aus den FIKTIVEN
/// Testfamilien tests/daten/familien/test_abwasser_d_*: Serie d 32 | 56 | 75 | 110 (DN des Herstellers FIKTIV nur bei d 56 = DN 50 und
/// d 110 = DN 100), Kürzel «PE», Bogen- und Abzweigwinkel 45 und 88.5; Bogen la = lb = z45 bzw. z88 (d 110: 50 bzw. 100, d 32: 20 bzw. 40);
/// T-Stück unter 60° l = 1.5·d1, lc = 2·d1, sonst l = lc = d1 (d 110, 45°: 165 / 220); Reduktion l = 60. Keine Normwerte.</summary>
public class AbwasserSerieTests
{
    const string Pe = "fam-test-abwasser-d";
    const string PeName = "Testabwasser PE nach Aussendurchmesser (fiktiv)";
    static readonly string[] PeFamilien = ["test_abwasser_d_serie", "test_abwasser_d_bogen", "test_abwasser_d_t", "test_abwasser_d_reduktion"];
    static readonly double Cos45 = Math.Cos(Math.PI / 4);

    /// <summary>Projekt mit EG, Systemen SW, RW, HZ (Heizung Vorlauf), KW (Trinkwasser kalt), KAE (Kälte Vorlauf) und der PE-Serie (FAM-1)
    /// samt Bogen (FAM-2), T-Stück (FAM-3), Reduktion (FAM-4).</summary>
    static Projekt MitPe(IProjektSpeicher? speicher = null)
    {
        var p = Neu(speicher: speicher);
        foreach (var (kz, typ) in new[] { ("SW", "Schmutzwasser"), ("RW", "Regenwasser"), ("HZ", "Heizung Vorlauf"), ("KW", "Trinkwasser kalt"), ("KAE", "Kälte Vorlauf") })
            MitSystem(p, kz, typ);
        foreach (var f in PeFamilien) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static BefehlsErgebnis PeZeichnen(Projekt p, string groesse, double[][] punkte, string system = "SW", JsonObject? extra = null) =>
        Zeichnen(p, groesse, punkte, extra, serie: Pe, system: system);

    static Leitungsserie PeSerie(Projekt p) => Leitungsserie.Hole(p.Modell, p.Katalog, Pe);

    static string? Beschriftung(Projekt p, string kz) => Leitungsbezeichnung.Text(p, Hole(p, "AN-EG"), Hole(p, kz));

    static FamilieDef Lesen(JsonObject d) => FamilieDef.Lesen(d, Kat);

    static JsonObject L(JsonObject d) => d["leitung"]!.AsObject();

    static void HatFehler(FamilieDef d, string stelle, string text)
    {
        Assert.False(d.Gueltig, "hätte ungültig sein sollen");
        Assert.Contains(d.Fehler, f => f.Stelle.StartsWith(stelle, StringComparison.Ordinal) && f.Meldung.Contains(text, StringComparison.Ordinal));
        Assert.All(d.Fehler.Where(f => f.Stelle.StartsWith(stelle, StringComparison.Ordinal)), f => Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag), $"Vorschlag fehlt: {f}"));
    }

    static List<Befund> P30(Projekt p) => Befunde(p, "P30");

    // ------------------------------------------------------------------ Datenmodell: einsatz, Grösse d

    [Theory]
    [InlineData("test_abwasser_d_serie", "Rohr", "fam-test-abwasser-d", 4)]
    [InlineData("test_abwasser_d_bogen", "Bogen", "fam-test-abwasser-d-bogen", 4)]
    [InlineData("test_abwasser_d_t", "TStueck", "fam-test-abwasser-d-t", 6)]
    [InlineData("test_abwasser_d_reduktion", "Reduktion", "fam-test-abwasser-d-reduktion", 4)]
    public void Testfamilien_SindGueltigUndFiktiv_FormteileOhneHinweisZurDn(string datei, string kategorie, string id, int typen)
    {
        var d = Familie(datei);
        Assert.Equal(id, d["id"]!.GetValue<string>());
        Assert.Equal(kategorie, d["kategorie"]!.GetValue<string>());
        Assert.Contains("FIKTIV", d["beschreibung"]!.GetValue<string>());
        var def = Lesen(d);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.Equal(typen, def.Typen.Count);
        // Rohranschlüsse mit d (Aussendurchmesser) statt dn: kein Hinweis «ohne dn»
        Assert.DoesNotContain(def.Hinweise, h => h.Meldung.Contains("ohne dn", StringComparison.Ordinal));
    }

    [Fact]
    public void Serie_EinsatzAbwasser_GroesseD_IstNachAussendurchmesser()
    {
        var l = Lesen(Familie("test_abwasser_d_serie")).Leitung!;
        Assert.Equal("abwasser", l.Einsatz);
        Assert.True(l.Abwasser);
        Assert.True(l.NachAussendurchmesser);
        Assert.Equal(["d"], l.Groesse.Keys);
        Assert.Equal("PE", l.Kuerzel);
        // ohne einsatz bleibt alles wie bisher
        var alt = Lesen(Familie("test_abwasserserie")).Leitung!;
        Assert.Null(alt.Einsatz);
        Assert.False(alt.Abwasser);
        Assert.False(alt.NachAussendurchmesser);
    }

    [Fact]
    public void Definition_RohrOhneEinsatz_GroesseD_IstFehler_MitHinweisAufEinsatz()
    {
        var d = Familie("test_abwasser_d_serie");
        L(d).Remove("einsatz");
        var def = Lesen(d);
        HatFehler(def, "leitung.groesse", "passt nicht zu Rohr");
        Assert.Contains(def.Fehler, f => f.Stelle == "leitung.groesse" && f.Vorschlag!.Contains("einsatz", StringComparison.Ordinal));
    }

    [Fact]
    public void Definition_EinsatzAbwasser_MitDn_BleibtErlaubt_AberNichtDUndDnZugleich()
    {
        var d = Familie("test_abwasserserie");
        L(d)["einsatz"] = "abwasser";
        var def = Lesen(d);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.True(def.Leitung!.Abwasser);
        Assert.False(def.Leitung.NachAussendurchmesser);
        // beide Grössen: Fehler
        var beide = Familie("test_abwasser_d_serie");
        L(beide)["groesse"]!["dn"] = "dn_hersteller";
        HatFehler(Lesen(beide), "leitung.groesse", "Grössenschlüssel «dn»");
    }

    [Fact]
    public void Definition_EinsatzNurAbwasserUndNurBeiRohren()
    {
        var d = Familie("test_abwasser_d_serie");
        L(d)["einsatz"] = "heizung";
        HatFehler(Lesen(d), "leitung.einsatz", "gibt es nicht");
        var k = Familie("test_kanalserie");
        L(k)["einsatz"] = "abwasser";
        HatFehler(Lesen(k), "leitung.einsatz", "nur bei Rohrserien");
    }

    [Fact]
    public void Definition_DnNurAlsHerstellerangabe_NieAusD()
    {
        // der Aussendurchmesser selbst mit ziel dn
        var d1 = Familie("test_abwasser_d_serie");
        foreach (var p in d1["parameter"]!.AsArray()) if (p!["name"]!.GetValue<string>() == "d") p["ziel"] = "dn";
        foreach (var p in d1["parameter"]!.AsArray()) if (p!["name"]!.GetValue<string>() == "dn_hersteller") p.AsObject().Remove("ziel");
        HatFehler(Lesen(d1), "parameter «d».ziel", "keine DN");
        // eine DN, die aus d gerechnet wird
        var d2 = Familie("test_abwasser_d_serie");
        foreach (var p in d2["parameter"]!.AsArray()) if (p!["name"]!.GetValue<string>() == "dn_hersteller") p["formel"] = "d - 10";
        foreach (var t in d2["typen"]!.AsArray()) t!["werte"]!.AsObject().Remove("dn_hersteller");
        HatFehler(Lesen(d2), "parameter «dn_hersteller».ziel", "hat eine Formel");
        // als Exemplarparameter
        var d3 = Familie("test_abwasser_d_serie");
        foreach (var p in d3["parameter"]!.AsArray()) if (p!["name"]!.GetValue<string>() == "dn_hersteller") p["art"] = "exemplar";
        HatFehler(Lesen(d3), "parameter «dn_hersteller».ziel", "Exemplarparameter");
    }

    [Fact]
    public void Definition_RohranschlussMitDnUndD_IstHinweis()
    {
        var d = Familie("test_abwasser_d_bogen");
        d["anschluesse"]![0]!["dn"] = 100;
        var def = Lesen(d);
        Assert.True(def.Gueltig, string.Join(" | ", def.Fehler));
        Assert.Contains(def.Hinweise, h => h.Meldung.Contains("mit dn und d", StringComparison.Ordinal));
    }

    [Fact]
    public void Schema_KenntEinsatzAbwasser_NichtPflicht()
    {
        var schema = JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Path.Combine("..", "..", "schema", "hlks-familie.schema.json"))))!;
        var e = schema["properties"]!["leitung"]!["properties"]!["einsatz"]!.AsObject();
        Assert.Equal(["abwasser"], e["enum"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("nie aus d gerechnet", e["description"]!.GetValue<string>());
        Assert.DoesNotContain("einsatz", schema["properties"]!["leitung"]!["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("Abwasser-Serie", schema["properties"]!["leitung"]!["properties"]!["groesse"]!["description"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Grösse nach Aussendurchmesser

    [Fact]
    public void Leitungsgroesse_Aussendurchmesser_TextD110_VergleichWieDurchmesser()
    {
        var a = Leitungsgroesse.Aussen(110);
        Assert.Equal("d 110", a.ToString());
        Assert.Equal("d 110", a.Kurz);
        Assert.Equal("Ø 110 mm", Leitungsgroesse.Rund(110).ToString());   // Kanal rund unverändert
        Assert.True(a.Gleich(Leitungsgroesse.Rund(110)));
        Assert.Equal(a, Leitungsgroesse.Rund(110));                       // das Kennzeichen ändert die Gleichheit nicht
        Assert.Equal(a.GetHashCode(), Leitungsgroesse.Rund(110).GetHashCode());
        Assert.False(a.Gleich(Leitungsgroesse.Rohr(110)));                // DN 110 ist nie d 110
        Assert.True(Leitungsgroesse.AusText("d 110") is { Aussendurchmesser: true, D: 110 });
        Assert.Null(Leitungsgroesse.AusText("DN 100"));
        Assert.Equal("{\"d\":110}", a.AlsJson()!.ToJsonString());
    }

    [Fact]
    public void Serie_GroessenSindAussendurchmesser_TypFuer_KeinDnUmrechnen()
    {
        var p = MitPe();
        var s = PeSerie(p);
        Assert.True(s.Abwasser);
        Assert.True(s.NachAussendurchmesser);
        Assert.Equal("d 32, d 56, d 75, d 110", s.GroessenText);
        Assert.All(s.Groessen, g => Assert.True(g.Groesse.Aussendurchmesser));
        Assert.Equal("d 110", s.TypFuer(Leitungsgroesse.Rund(110))!.Typ);   // { "d": 110 } aus einem Befehl
        Assert.Equal("d 32", s.TypFuer(Leitungsgroesse.Aussen(32))!.Typ);
        Assert.Null(s.TypFuer(Leitungsgroesse.Rohr(100), out var grund));    // DN 100 wird nicht zu d 110
        Assert.Contains("passt nicht", grund);
        Assert.Contains("nie in einen Aussendurchmesser umgerechnet", s.GroessenVorschlag(Leitungsgroesse.Rohr(100)));
        Assert.Null(s.TypFuer(Leitungsgroesse.Rund(100), out var grund2));
        Assert.Contains("keine Grösse d 100", grund2);
        // DN des Herstellers: nur, wo die Unterlage sie nennt (FIKTIV d 56 → 50, d 110 → 100), nie gerechnet
        Assert.Equal(100, s.DnHersteller(s.Groesse("d 110")!));
        Assert.Equal(50, s.DnHersteller(s.Groesse("d 56")!));
        Assert.Null(s.DnHersteller(s.Groesse("d 32")!));
        Assert.Null(s.DnHersteller(s.Groesse("d 75")!));
    }

    [Fact]
    public void Befehlsgroesse_TypnameZahlObjektUndText_DnAbgelehnt()
    {
        var p = MitPe();
        var s = PeSerie(p);
        Assert.Equal("d 110", RoutingBefehle.Groesse(s, JsonValue.Create("d 110")).Typ);
        Assert.Equal("d 110", RoutingBefehle.Groesse(s, JsonValue.Create(110)).Typ);
        Assert.Equal("d 110", RoutingBefehle.Groesse(s, JsonValue.Create("110")).Typ);
        Assert.Equal("d 110", RoutingBefehle.Groesse(s, new JsonObject { ["d"] = 110 }).Typ);
        Assert.Equal("d 56", RoutingBefehle.Groesse(s, JsonValue.Create("d56")).Typ);
        var dn = Assert.Throws<HlksFehler>(() => RoutingBefehle.Groesse(s, new JsonObject { ["dn"] = 100 }));
        Assert.Contains("passt nicht", dn.Message);
        Assert.Contains("nie in einen Aussendurchmesser umgerechnet", dn.Vorschlag);
        Assert.Contains("hat keinen Typ «DN 100»", Assert.Throws<HlksFehler>(() => RoutingBefehle.Groesse(s, JsonValue.Create("DN 100"))).Message);
        Assert.Contains("d 100", Assert.Throws<HlksFehler>(() => RoutingBefehle.Groesse(s, JsonValue.Create(100))).Message);
    }

    [Fact]
    public void Formteilwahl_UeberD_BogenTStueckReduktion()
    {
        var p = MitPe();
        var s = PeSerie(p);
        var m = p.Modell; var k = p.Katalog;
        var b = s.Formteil(m, k, "bogen", Leitungsgroesse.Rund(110), winkel: 45);
        Assert.True(b.Ok, b.Fehler);
        Assert.Equal("d 110", b.Typ);
        Assert.Equal(50, b.Geometrie!.Einbaulaenge("a"), 6);   // z45 = 50 (FIKTIV)
        var b88 = s.Formteil(m, k, "bogen", Leitungsgroesse.Aussen(32), winkel: 88.5);
        Assert.True(b88.Ok, b88.Fehler);
        Assert.Equal(40, b88.Geometrie!.Einbaulaenge("b"), 6);  // d 32: z88 = 40
        var t = s.Formteil(m, k, "tstueck", Leitungsgroesse.Aussen(110), Leitungsgroesse.Aussen(56), 45);
        Assert.True(t.Ok, t.Fehler);
        Assert.Equal("d 110/56", t.Typ);
        Assert.Equal(165, t.Geometrie!.Einbaulaenge("a"), 6);   // 1.5 · 110
        Assert.Equal(220, t.Geometrie.Einbaulaenge("c"), 6);    // 2 · 110
        Assert.Equal("d 110/d 56", t.GroessenText);
        var r = s.Formteil(m, k, "reduktion", Leitungsgroesse.Aussen(56), Leitungsgroesse.Aussen(110));
        Assert.True(r.Ok, r.Fehler);
        Assert.Equal("d 110/56", r.Typ);
        Assert.True(r.Vertauscht);
        // DN passt nicht zur Serie nach Aussendurchmesser, ein fehlender Typ nennt die Grössen mit d
        Assert.Contains("passt nicht", s.Formteil(m, k, "bogen", Leitungsgroesse.Rohr(100)).Fehler);
        var nein = s.Formteil(m, k, "reduktion", Leitungsgroesse.Aussen(110), Leitungsgroesse.Aussen(32));
        Assert.False(nein.Ok);
        Assert.Contains("a = d 110, b = d 32", nein.Fehler);
    }

    // ------------------------------------------------------------------ Routing und Befehle

    [Fact]
    public void Zeichnen_D110_MitBogen45_RohrTraegtDurchmesserUndDnDesHerstellers()
    {
        var p = MitPe();
        var e = Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [3000, 0, 500], [5000, 2000, 500]]));
        // Bogen 45° (z45 = 50): Geraden um 50 mm gekürzt
        Gerade(p, "RO-1", [0, 0, 500], [2950, 0, 500]);
        Gerade(p, "RO-2", [3000 + 50 * Cos45, 50 * Cos45, 500], [5000, 2000, 500]);
        var bo = Hole(p, "BO-1");
        Assert.Equal("d 110", Familien.Typ(bo));
        Assert.Equal(45, Familien.Exemplarwerte(bo)!["winkel"]!.GetValue<double>());
        foreach (var kz in new[] { "RO-1", "RO-2" })
        {
            var r = Hole(p, kz);
            Assert.Equal("d 110", Familien.Typ(r));
            Assert.Equal(110, r.Zahl("durchmesser"));
            Assert.Equal("familie", r.Eigenschaft("durchmesser")!.Herkunft.Art);
            Assert.Equal(100, r.Zahl("dn"));                                   // DN des Herstellers (ziel dn, FIKTIV), nicht aus d gerechnet
            Assert.Equal($"{PeName} Typ d 110", r.Eigenschaft("dn")!.Herkunft.Quelle);
            Assert.All(r.Anschluesse, a => { Assert.Null(a.Dn); Assert.Equal(110, a.D); });
            var st = Stelle(p, kz, "ende");
            Assert.Equal("d 110", st.Groesse.ToString());                     // die DN des Herstellers ist nicht die Grösse
        }
        Assert.Equal("d 110", Stelle(p, "BO-1", "a").Groesse.ToString());
        Assert.Contains(e.Hinweise, h => h.StartsWith("Gerade RO-1 d 110: 2950 mm", StringComparison.Ordinal));
        Sauber(p);
        Assert.Empty(P30(p));
    }

    [Fact]
    public void Abzweigen_D110MitAbzweigD56_TStueck110Auf56()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [3000, 0, 500]]));
        // Schmutzwasser läuft dem T-Stück zu: Abzweig unter 45° gegen die Fliessrichtung der Leitung
        var e = Ok(p.Ausfuehren("leitung_abzweigen", new JsonObject
        {
            ["leitung"] = "RO-1", ["punkt"] = new JsonArray(1500, 0, 500), ["richtung"] = new JsonArray(-1, -1, 0), ["laenge"] = 1000, ["groesse"] = "d 56",
        }));
        var ts = Hole(p, "TS-1");
        Assert.Equal("d 110/56", Familien.Typ(ts));
        Assert.Equal(45, Familien.Exemplarwerte(ts)!["winkel"]!.GetValue<double>());
        Gerade(p, "RO-1", [0, 0, 500], [1500 - 165, 0, 500]);
        Gerade(p, "RO-1-2", [1500 + 165, 0, 500], [3000, 0, 500]);
        var ab = p.Modell.DerKlasse("Rohr").Single(r => r.Zahl("durchmesser") == 56);
        Assert.Equal("d 56", Familien.Typ(ab));
        Assert.Equal(50, ab.Zahl("dn"));                                       // DN des Herstellers (FIKTIV)
        Assert.Equal("d 56", Stelle(p, "TS-1", "c").Groesse.ToString());
        Assert.Contains(e.Hinweise, h => h.Contains("Typ «d 110/56»", StringComparison.Ordinal));
        Sauber(p);
        Assert.Equal("PE 56", Beschriftung(p, ab.Kennzeichen));
    }

    [Fact]
    public void D32_OhneDn_IstZeichenbar_MitBogen88Komma5()
    {
        var p = MitPe();
        double w = 88.5 * Math.PI / 180;
        Ok(PeZeichnen(p, "d 32", [[0, 5000, 500], [2000, 5000, 500], [2000 + 1000 * Math.Cos(w), 5000 + 1000 * Math.Sin(w), 500]]));
        var r = Hole(p, "RO-1");
        Assert.Equal("d 32", Familien.Typ(r));
        Assert.Equal(32, r.Zahl("durchmesser"));
        Assert.Null(r.Eigenschaft("dn"));                                    // keine DN – und keine erfunden
        Assert.Equal(1960, Laenge(p, "RO-1"), 3);                            // z88 = 40 (FIKTIV)
        Assert.Equal("d 32", Familien.Typ(Hole(p, "BO-1")));
        Assert.Equal("PE 32", Beschriftung(p, "RO-1"));
        Assert.Empty(Rohrgroessen.Hinweise(p, p.Modell.DerKlasse("Rohr")));   // keine «Rohr ohne Nennweite»
        Sauber(p);
    }

    [Fact]
    public void Zeichnen_AnD110Ende_MitD56_SetztReduktion110Auf56()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [2000, 0, 500]]));
        Ok(PeZeichnen(p, "d 56", [[2000, 0, 500], [4000, 0, 500]], extra: new JsonObject { ["von"] = new JsonObject { ["element"] = "RO-1", ["anschluss"] = "ende" } }));
        var rd = Assert.Single(p.Modell.DerKlasse("Reduktion"));
        Assert.Equal("d 110/56", Familien.Typ(rd));
        Assert.Equal("d 110", Anschlusslagen.Stelle(p.Modell, p.Katalog, rd, "a")!.Groesse.ToString());
        var neu = p.Modell.DerKlasse("Rohr").Single(r => r.Zahl("durchmesser") == 56);
        Gerade(p, neu.Kennzeichen, [2060, 0, 500], [4000, 0, 500]);           // Reduktion l = 60 (FIKTIV)
        Sauber(p);
    }

    [Fact]
    public void LeitungGroesse_Zug_AufD75_FormteileUeberD()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [3000, 0, 500], [5000, 2000, 500]]));
        Ok(p.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["groesse"] = new JsonObject { ["d"] = 75 } }));
        Assert.Equal("d 75", Familien.Typ(Hole(p, "RO-1")));
        Assert.Equal("d 75", Familien.Typ(Hole(p, "RO-2")));
        Assert.Equal("d 75", Familien.Typ(Hole(p, "BO-1")));
        Assert.Equal(75, Hole(p, "RO-2").Zahl("durchmesser"));
        Assert.Null(Hole(p, "RO-2").Eigenschaft("dn"));                       // d 75 hat keine DN des Herstellers: die alte DN 100 entfällt
        Gerade(p, "RO-1", [0, 0, 500], [2960, 0, 500]);                         // d 75: z45 = 40
        Sauber(p);
    }

    [Fact]
    public void LeitungSerie_VonDnSerieInPeSerie_UeberDenAussendurchmesser()
    {
        // Testabwasserserie (DN 100, da 110 FIKTIV) → PE-Serie: d 110 über den Aussendurchmesser des Typs, nie aus der DN gerechnet
        var p = MitPe();
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t", "test_abwasserreduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        Ok(Zeichnen(p, "DN 100", [[0, 0, 500], [3000, 0, 500], [5000, 2000, 500]], serie: "fam-test-abwasserserie", system: "SW"));
        Assert.Equal("DN 100", Stelle(p, "RO-1", "ende").Groesse.ToString());
        Ok(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["serie"] = Pe }));
        foreach (var kz in new[] { "RO-1", "RO-2" })
        {
            var r = Hole(p, kz);
            Assert.Equal(Familien.DefinitionsId(Familien.FamilieVon(p.Modell, r)!), Pe);
            Assert.Equal("d 110", Familien.Typ(r));
            Assert.Equal(110, r.Zahl("durchmesser"));
        }
        var bo = Assert.Single(p.Modell.DerKlasse("Bogen"));
        Assert.Equal("d 110", Familien.Typ(bo));
        Assert.Equal(Pe + "-bogen", Familien.DefinitionsId(Familien.FamilieVon(p.Modell, bo)!));
        Gerade(p, "RO-1", [0, 0, 500], [2950, 0, 500]);
        Sauber(p);
        // eine DN-Grösse ohne passenden Aussendurchmesser wird abgelehnt (DN 50: da 60 FIKTIV – die PE-Serie hat kein d 60)
        Ok(Zeichnen(p, "DN 50", [[0, 5000, 500], [2000, 5000, 500]], serie: "fam-test-abwasserserie", system: "SW"));
        var kz50 = p.Modell.DerKlasse("Rohr").Single(r => r.Zahl("dn") == 50 && r.Zahl("durchmesser") is null).Kennzeichen;
        var nein = Nein(p.Ausfuehren("leitung_serie", new JsonObject { ["leitung"] = kz50, ["serie"] = Pe }));
        Assert.Contains("d 60", nein.Fehler);
    }

    [Fact]
    public void Datenumstellung_DnSerieAufGroesseD_LeitungenFolgen_DnBleibtAlsHerstellerangabe()
    {
        // R6/R7-Muster: eine bestehende DN-Serie wird auf einsatz abwasser mit Grösse d umgestellt (familie_aendern leitung_setzen)
        var p = MitPe();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_abwasserserie") }));          // FAM-5
        var fam = p.Modell.DerKlasse(Familien.Klasse).Single(f => Familien.DefinitionsId(f) == "fam-test-abwasserserie").Kennzeichen;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "R-ALT", ["system"] = "SW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 100" }, ["bezuege"] = new JsonObject { ["familie"] = fam },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 0, 500], [2000, 0, 500]) },
        }));
        Assert.Equal("110", Beschriftung(p, "R-ALT"));                          // Abwasser über das Medium (B9b), Grösse DN 100
        Assert.Equal("DN 100", Stelle(p, "R-ALT", "anfang").Groesse.ToString());
        var block = (JsonObject)Leitungsserie.Hole(p.Modell, p.Katalog, fam).Def.Quelle["leitung"]!.DeepClone();
        block["groesse"] = new JsonObject { ["d"] = "da" };
        block["einsatz"] = "abwasser";
        block["kuerzel"] = "PE-S";
        block.Remove("formteile"); block.Remove("regeln");
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = fam, ["ops"] = new JsonArray(new JsonObject { ["op"] = "leitung_setzen", ["leitung"] = block }) }));
        var r = Hole(p, "R-ALT");
        Assert.Equal("DN 100", Familien.Typ(r));                              // der Typname bleibt (Umbenennen ist Sache der Daten)
        Assert.Equal(110, r.Zahl("durchmesser"));                             // aus da des Typs
        Assert.Equal(100, r.Zahl("dn"));                                      // der Parameter dn (ziel dn) ist jetzt die Herstellerangabe
        Assert.Equal("d 110", Stelle(p, "R-ALT", "anfang").Groesse.ToString());
        Assert.Equal("PE-S 110", Beschriftung(p, "R-ALT"));
    }

    // ------------------------------------------------------------------ Beschriftung

    [Fact]
    public void Beschriftung_PE110_ImmerNachAussendurchmesser_AuchInHeizung()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [2000, 0, 500]]));
        Ok(PeZeichnen(p, "d 110", [[0, 3000, 500], [2000, 3000, 500]], system: "HZ"));
        Assert.Equal("PE 110", Beschriftung(p, "RO-1"));
        Assert.Equal("PE 110", Beschriftung(p, "RO-2"));                       // unabhängig vom Medium
        var g = Rohrgroessen.Von(p, null, Hole(p, "RO-2"));
        Assert.True(g.Abwasser);
        Assert.Equal("110", g.Groesse);
        Assert.Equal(110, Rohrgroessen.Aussendurchmesser(p, Hole(p, "RO-2"))!.Value.Da);
        // Einstellung NW/DN gilt für Abwasser-Serien nicht
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["rohrbezeichnung"] = "DN" } }));
        Assert.Equal("PE 110", Beschriftung(p, "RO-2"));
        // ohne Kürzel nur der Aussendurchmesser
        var block = (JsonObject)PeSerie(p).Def.Quelle["leitung"]!.DeepClone();
        block.Remove("kuerzel");
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "leitung_setzen", ["leitung"] = block }) }));
        Assert.Equal("110", Beschriftung(p, "RO-1"));
    }

    [Fact]
    public void Beschriftung_EinsatzAbwasserMitDn_AuchInHeizungNachAussendurchmesser_OhneEinsatzWieBisher()
    {
        var p = MitPe();
        var mit = Familie("test_abwasserserie");
        mit["id"] = "fam-test-abwasserserie-einsatz"; mit["name"] = "Testabwasserserie mit Einsatz (fiktiv)";
        L(mit)["einsatz"] = "abwasser"; L(mit)["kuerzel"] = "PE-S"; L(mit).Remove("formteile"); L(mit).Remove("regeln");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = mit }));          // FAM-5
        var ohne = Familie("test_abwasserserie");
        L(ohne).Remove("formteile"); L(ohne).Remove("regeln");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = ohne }));         // FAM-6
        foreach (var (kz, fam, y) in new[] { ("R-MIT", "FAM-5", 0.0), ("R-OHNE", "FAM-6", 1000.0) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = "HZ", ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["familientyp"] = "DN 100" }, ["bezuege"] = new JsonObject { ["familie"] = fam },
                ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, y, 500], [2000, y, 500]) },
            }));
        Assert.Equal("PE-S 110", Beschriftung(p, "R-MIT"));                     // Abwasser-Serie: Aussendurchmesser (da 110 FIKTIV)
        Assert.Equal("NW 100", Beschriftung(p, "R-OHNE"));                      // ohne einsatz: wie bisher nach Medium
        Assert.Equal("DN 100", Stelle(p, "R-MIT", "anfang").Groesse.ToString()); // Grösse bleibt die DN
        Assert.Single(P30(p), b => b.Element == "R-MIT");
    }

    [Fact]
    public void Abfrage_Leitungsserie_EinsatzGroessenDBeschriftungUndDnDesHerstellers()
    {
        var p = MitPe();
        var o = Leitungsserie.Abfrage(p, Pe);
        Assert.Equal("abwasser", o["einsatz"]!.GetValue<string>());
        Assert.True(o["nachAussendurchmesser"]!.GetValue<bool>());
        Assert.Equal("d", o["groessenparameter"]!["d"]!.GetValue<string>());
        var g = o["groessen"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["typ"]!.GetValue<string>());
        Assert.Equal(["d 32", "d 56", "d 75", "d 110"], g.Keys);
        var d110 = g["d 110"];
        Assert.Equal(110, d110["d"]!.GetValue<double>());
        Assert.Null(d110["dn"]);
        Assert.Equal("PE 110", d110["beschriftung"]!.GetValue<string>());
        Assert.Equal("PE 110", d110["beschriftungAbwasser"]!.GetValue<string>());
        Assert.Equal(100, d110["dnHersteller"]!.GetValue<double>());
        Assert.Null(g["d 32"]["dnHersteller"]);
        Assert.Equal("PE 32", g["d 32"]["beschriftung"]!.GetValue<string>());
        Assert.True(d110["formteile"]!["bogen"]!["verfuegbar"]!.GetValue<bool>());
        Assert.True(d110["formteile"]!["tstueck"]!["verfuegbar"]!.GetValue<bool>());
        var red = d110["formteile"]!["reduktion"]!.AsArray().Select(x => (x!["auf"]!.GetValue<string>(), x["verfuegbar"]!.GetValue<bool>())).ToList();
        Assert.Equal([("d 32", false), ("d 56", true), ("d 75", true)], red);
        // DN-Serie unverändert: kein einsatz, beschriftung NW
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_abwasserserie") }));
        var alt = Leitungsserie.Abfrage(p, "fam-test-abwasserserie");
        Assert.Null(alt["einsatz"]);
        Assert.False(alt["nachAussendurchmesser"]!.GetValue<bool>());
        var dn100 = alt["groessen"]!.AsArray().Single(x => x!["typ"]!.GetValue<string>() == "DN 100")!;
        Assert.Equal("NW 100", dn100["beschriftung"]!.GetValue<string>());
        Assert.Equal("110", dn100["beschriftungAbwasser"]!.GetValue<string>());
        Assert.Null(dn100["dnHersteller"]);
    }

    // ------------------------------------------------------------------ Prüfung P30

    [Fact]
    public void P30_Code()
    {
        Assert.Equal("Abwasser-Serie für ein anderes Medium als Abwasser", Pruefungen.Codes["P30"]);
    }

    [Fact]
    public void P30_HeizungTrinkwasserKaelte_Fehler_SchmutzUndRegenwasserNicht()
    {
        var p = MitPe();
        string[] systeme = ["SW", "RW", "HZ", "KW", "KAE"];
        for (int i = 0; i < systeme.Length; i++) Ok(PeZeichnen(p, "d 110", [[0, i * 1000.0, 500], [2000, i * 1000.0, 500]], system: systeme[i]));
        var b = P30(p);
        Assert.Equal(["RO-3", "RO-4", "RO-5"], b.Select(x => x.Element).Order(StringComparer.Ordinal));
        Assert.All(b, x => Assert.Equal("fehler", x.Schwere));
        var hz = b.Single(x => x.Element == "RO-3");
        Assert.Contains($"Abwasser-Serie FAM-1 «{PeName}» für Heizung Vorlauf (HZ-VL)", hz.Text);
        Assert.Contains("fliessart druck", hz.Text);
        Assert.Contains("leitung_serie", hz.Vorschlag);
        Assert.Contains("Trinkwasser kalt (KW)", b.Single(x => x.Element == "RO-4").Text);
        Assert.Contains("Kälte Vorlauf (KAE-VL)", b.Single(x => x.Element == "RO-5").Text);
    }

    [Fact]
    public void P30_OhneMedium_Hinweis_MediumAuchUeberDenStrang()
    {
        // Ohne System gibt es kein Rohr über die Befehle (Rohr braucht ein System); ohne Medium ist eine Leitung nur nach einem Fremdbestand
        // bzw. Altstand – hier nachgestellt, indem das System im Modell entfernt wird
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [2000, 0, 500]]));
        var r = Hole(p, "RO-1");
        r.System = null;
        r.TeilVon = null;
        var b = Assert.Single(P30(p));
        Assert.Equal("hinweis", b.Schwere);
        Assert.Contains("ohne Medium", b.Text);
        Assert.Contains("System bzw. Strang zuordnen", b.Vorschlag);
        // Medium über den Strang eines Heizungssystems: fehler
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "HZ-1", ["teilVon"] = "HZ" }));
        Hole(p, "RO-1").TeilVon = Hole(p, "HZ-1").Id;
        var f = Assert.Single(P30(p));
        Assert.Equal("fehler", f.Schwere);
        Assert.Contains("Heizung Vorlauf (HZ-VL)", f.Text);
    }

    [Fact]
    public void P30_DnSerieOhneEinsatz_InHeizung_KeinBefund()
    {
        var p = MitPe();
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen" }) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        Ok(Zeichnen(p, "DN 100", [[0, 0, 500], [2000, 0, 500]], serie: "fam-test-abwasserserie", system: "HZ"));
        Assert.Empty(P30(p));
    }

    // ------------------------------------------------------------------ Listen, Sanitär-Belastung (Modul 4A)

    [Fact]
    public void Leitungslaengen_NachAussendurchmesserGruppiert()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [3000, 0, 500]]));
        Ok(PeZeichnen(p, "d 110", [[0, 1000, 500], [1000, 1000, 500]]));
        Ok(PeZeichnen(p, "d 32", [[0, 2000, 500], [500, 2000, 500]]));
        var l = Ableitungen.Leitungslaengen(p).OrderBy(x => x.Groesse, StringComparer.Ordinal).ToList();
        Assert.Equal([(PeName, "110", 4.0), (PeName, "32", 0.5)], l.Select(x => (x.Serie, x.Groesse, Math.Round(x.LaengeM, 6))));
    }

    [Fact]
    public void SanitaerBelastung_MitLeitungenNachAussendurchmesser()
    {
        // wie SanitaerNetzTests.Verzweigung (Schmutzwasser), die Leitungen aber mit Serie und Grösse d (leitung_ziehen); Werte aus dem
        // Katalog sanitaer_belastungswerte.json: Waschtisch DU 0.5, WC wc_75 DU 2, Badewanne DU 0.8
        var p = SanitaerHilfe.Netzprojekt(0.5, "SW");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_abwasser_d_serie") }));
        SanitaerHilfe.Apparat(p, "Waschtisch", "WT-1"); SanitaerHilfe.Apparat(p, "WC", "WC-1"); SanitaerHilfe.Apparat(p, "Badewanne", "BW-1", geschoss: "OG");
        SanitaerHilfe.Kontrollschacht(p);
        foreach (var t in new[] { "T3", "T4" }) SanitaerHilfe.Formteil(p, t, "SW-1");
        int n = 0;
        void Rohr(string kz, string groesse, string von, string nach) => Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["kennzeichen"] = kz, ["strang"] = "SW-1", ["serie"] = Pe, ["groesse"] = groesse, ["von"] = von, ["nach"] = nach,
            ["punkte"] = Pts([++n * 100.0, 0, 100], [n * 100.0, 500, 100]),
        }));
        Rohr("S1", "d 56", "WT-1.sw", "T3.c");
        Rohr("S2", "d 110", "WC-1.sw", "T3.a");
        Rohr("S3", "d 110", "T3.b", "T4.a");
        Rohr("S4", "d 56", "BW-1.sw", "T4.c");
        Rohr("S5", "d 110", "T4.b", "KS-1.ein");
        Assert.Equal("d 56", Familien.Typ(Hole(p, "S1")));
        Assert.Equal(56, Hole(p, "S1").Zahl("durchmesser"));
        foreach (var (kz, du, apparate) in new[] { ("S1", 0.5, 1), ("S2", 2.0, 1), ("S3", 2.5, 2), ("S4", 0.8, 1), ("S5", 3.3, 3) })
        {
            var b = SanitaerHilfe.Belastung(p, kz);
            Assert.Equal("abwasser", b["netz"]!.GetValue<string>());
            SanitaerHilfe.Etwa(du, SanitaerHilfe.Zahl(b["summeDu"]), $"{kz} ΣDU");
            Assert.Equal(apparate, b["apparate"]!.GetValue<int>());
        }
        Assert.Empty(P30(p));
    }

    // ------------------------------------------------------------------ IFC

    [Fact]
    public void IfcExport_OuterDiameter_NominalDiameterNurAusHerstellerangabe()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [2000, 0, 500]]));
        Ok(PeZeichnen(p, "d 32", [[0, 1000, 500], [2000, 1000, 500]]));
        var (aus, _) = IfcExportAufbau.Erstellen(p, "IFC4", new IfcExUmfang(), null, null);
        IfcExProdukt X(string kz) => aus.Produkte.Single(x => x.Element.Kennzeichen == kz);
        double? W(IfcExProdukt x, string set, string name) => x.Psets.FirstOrDefault(s => s.Name == set)?.Werte.FirstOrDefault(w => w.Name == name)?.Zahl;
        Assert.Equal(110, W(X("RO-1"), "Pset_PipeSegmentTypeCommon", "OuterDiameter"));
        Assert.Equal(100, W(X("RO-1"), "Pset_PipeSegmentTypeCommon", "NominalDiameter"));   // DN des Herstellers (FIKTIV)
        Assert.Equal(100, W(X("RO-1"), IfcExport.PsetKicad, "Nennweite"));
        Assert.Equal(32, W(X("RO-2"), "Pset_PipeSegmentTypeCommon", "OuterDiameter"));
        Assert.Null(W(X("RO-2"), "Pset_PipeSegmentTypeCommon", "NominalDiameter"));          // d 32 ohne DN: keine, nie aus d gerechnet
        Assert.Null(W(X("RO-2"), IfcExport.PsetKicad, "Nennweite"));
        Assert.All(X("RO-2").Ports, pt => Assert.Null(pt.Nennweite));
        Assert.All(X("RO-1").Ports, pt => Assert.Equal(100, pt.Nennweite));
        Assert.Equal($"{PeName} d 110", X("RO-1").Typ!.Name);
    }

    [Fact]
    public void IfcRundreise_ImportUeberDenAussendurchmesserInDieSerie()
    {
        var p = MitPe();
        Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [2000, 0, 500]]));
        Ok(PeZeichnen(p, "d 32", [[0, 1000, 500], [2000, 1000, 500]]));
        using var o = new IfcExportOrdner();
        var datei = o.Datei("pe.ifc");
        var bericht = Export(p, datei);
        Assert.True(File.Exists(datei), bericht.ToJsonString());
        var ziel = Importieren(datei);
        var r110 = Importiert(ziel, Gid(p, "RO-1"))!;
        var r32 = Importiert(ziel, Gid(p, "RO-2"))!;
        Assert.Equal("Rohr", r110.Klasse);
        Assert.Equal(110, r110.Zahl("da"));                     // OuterDiameter
        Assert.Equal(100, r110.Zahl("dn"));                     // NominalDiameter = DN des Herstellers
        Assert.Equal(32, r32.Zahl("da"));
        Assert.Null(r32.Zahl("dn"));                            // keine DN – auch nicht aus dem Aussendurchmesser
        // in die PE-Serie über den Aussendurchmesser
        foreach (var f in PeFamilien) Ok(ziel.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        foreach (var r in new[] { r110, r32 })
            Ok(ziel.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = r.Kennzeichen, ["serie"] = Pe }));
        var n110 = ziel.Modell.Finde(r110.Id)!; var n32 = ziel.Modell.Finde(r32.Id)!;
        Assert.Equal("d 110", Familien.Typ(n110));
        Assert.Equal(110, n110.Zahl("durchmesser"));
        Assert.Equal("d 32", Familien.Typ(n32));
        Assert.Equal(32, n32.Zahl("durchmesser"));
        Assert.Null(n32.Zahl("dn"));
        Assert.Equal("PE 32", Leitungsbezeichnung.Text(ziel, null, n32));
        // ohne Aussendurchmesser keine Grösse in der Serie (eine DN wird nicht umgerechnet)
        var nurDn = new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = "R-DN", ["system"] = ziel.Modell.Finde(n110.System)!.Kennzeichen, ["eigenschaften"] = new JsonObject { ["dn"] = 100 },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, 5000, 500], [2000, 5000, 500]) },
        };
        Ok(ziel.Ausfuehren("element_anlegen", nurDn));
        var nein = Nein(ziel.Ausfuehren("leitung_aufteilen", new JsonObject { ["leitung"] = "R-DN", ["serie"] = Pe }));
        Assert.Contains("DN 100", nein.Fehler);
    }

    // ------------------------------------------------------------------ Rundreise: bestehende Projekte unverändert, PE-Projekt bleibt

    [Fact]
    public void Rundreise_Beispielprojekt_DnGefuehrtUnveraendert()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "abwasser_bsp_" + Guid.NewGuid().ToString("N")[..8]);
        var ordner = Path.Combine(wurzel, "e_KICAD");
        try
        {
            new OrdnerSpeicher(ordner).Anlegen();
            var p = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner));
            var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B", "pc-b");
            Assert.Equal(Inhalt(p), Inhalt(b));
            var rohre = b.Modell.DerKlasse("Rohr").ToList();
            Assert.NotEmpty(rohre);
            foreach (var r in rohre)
            {
                Assert.Null(r.Eigenschaft("durchmesser"));
                foreach (var s in Anschlusslagen.Von(b.Modell, b.Katalog, r).Where(s => s.HatGroesse))
                {
                    Assert.False(s.Groesse.Aussendurchmesser, $"{r.Kennzeichen}.{s.Name}");
                    Assert.NotNull(s.Groesse.Dn);
                }
                Assert.Equal(Leitungsbezeichnung.Text(p, null, p.Modell.Hole(r.Id)), Leitungsbezeichnung.Text(b, null, r));
            }
            Assert.Empty(P30(b));
            Assert.Equal(Pruefungen.Alle(p.Modell, p.Katalog).Select(x => x.Schluessel), Pruefungen.Alle(b.Modell, b.Katalog).Select(x => x.Schluessel));
        }
        finally
        {
            try { Directory.Delete(wurzel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Rundreise_DnSerieGezeichnet_GroessenUndBeschriftungWieBisher()
    {
        // Testabwasserserie (DN-geführt, ohne einsatz): Grösse DN, Beschriftung nach Medium – Schmutzwasser «110», Heizung «NW 100»
        var p = Neu();
        MitSystem(p, "SW", "Schmutzwasser"); MitSystem(p, "HZ", "Heizung Vorlauf");
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t", "test_abwasserreduktion" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        Ok(Zeichnen(p, "DN 100", [[0, 0, 500], [3000, 0, 500], [5000, 2000, 500]], serie: "fam-test-abwasserserie", system: "SW"));
        Ok(Zeichnen(p, "DN 100", [[0, 5000, 500], [3000, 5000, 500]], serie: "fam-test-abwasserserie", system: "HZ"));
        Assert.Equal("DN 100", Stelle(p, "RO-1", "ende").Groesse.ToString());
        Assert.Equal("DN 100", Stelle(p, "BO-1", "a").Groesse.ToString());
        Assert.Equal(100, Hole(p, "RO-1").Zahl("dn"));
        Assert.Null(Hole(p, "RO-1").Eigenschaft("durchmesser"));
        Assert.Equal("110", Leitungsbezeichnung.Text(p, null, Hole(p, "RO-1")));
        Assert.Equal("NW 100", Leitungsbezeichnung.Text(p, null, Hole(p, "RO-3")));
        Assert.All(Hole(p, "RO-1").Anschluesse, a => Assert.Equal(100, a.Dn));
        Sauber(p);
        Assert.Empty(P30(p));
    }

    [Fact]
    public void SpeichernUndLaden_PeSerieBleibt()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "abwasser_pe_" + Guid.NewGuid().ToString("N")[..8]);
        var ordner = Path.Combine(wurzel, "e_KICAD");
        try
        {
            new OrdnerSpeicher(ordner).Anlegen();
            var p = MitPe(new OrdnerSpeicher(ordner));
            Ok(PeZeichnen(p, "d 110", [[0, 0, 500], [3000, 0, 500], [5000, 2000, 500]]));
            var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B", "pc-b");
            Assert.Equal(Inhalt(p), Inhalt(b));
            Assert.Equal("d 110", Stelle(b, "RO-1", "ende").Groesse.ToString());
            Assert.Equal("PE 110", Leitungsbezeichnung.Text(b, null, Hole(b, "RO-1")));
            Sauber(b);
            // weiterarbeiten im geöffneten Projekt
            Ok(b.Ausfuehren("leitung_groesse", new JsonObject { ["leitung"] = "RO-1", ["zug"] = true, ["groesse"] = "d 56" }));
            Assert.Equal(56, Hole(b, "RO-2").Zahl("durchmesser"));
            Assert.Equal(50, Hole(b, "RO-2").Zahl("dn"));
            Sauber(b);
        }
        finally
        {
            try { Directory.Delete(wurzel, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
