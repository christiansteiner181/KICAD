using System.Diagnostics;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;
using static Hlks.Kern.Tests.RoutingHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachträge zu Leitungsserien (Modul 3, 21.10.2026, hlks-kern): (1) Abfrage leitungsserie schnell, ohne Sperre auf einem festen Stand und
/// abbrechbar – Reduktionen nach der Regel der Reduktionsfamilie, Stichproben voll gerechnet; (2) Formteile freier Serien tragen ihre Grösse selbst
/// (auch in der Vorgabegrösse), bestehende über die Nachführung und P45. Alle Werte FIKTIV aus den Testfamilien tests/daten/familien
/// (test_kanalfrei mit Raster 500x300, 400x200, 300x200, 200x200 und Formteilen mit Exemplar-Grössen; Handrechnungen dort). Keine Normwerte.</summary>
public class LeitungsserieNachtraegeTests
{
    const string Frei = "fam-test-kanalfrei";
    static readonly string[] Familien4 = ["test_kanalfrei", "test_kanalbogen_frei", "test_kanal_t_frei", "test_kanalreduktion_frei"];

    /// <summary>Projekt mit System ZUL, der freien Testkanalserie (FAM-1) und ihren Formteilen (FAM-2 Bogen, FAM-3 T, FAM-4 Reduktion).</summary>
    static Projekt MitFrei()
    {
        var p = Neu();
        MitSystem(p, "ZUL", "Zuluft");
        foreach (var f in Familien4) Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static Leitungsserie Serie(Projekt p, string id = Frei) => Leitungsserie.Hole(p.Modell, p.Katalog, id);

    static JsonObject G(double b, double h) => new() { ["b"] = b, ["h"] = h };

    static BefehlsErgebnis Kanal(Projekt p, JsonNode groesse, double[][] punkte, string serie = Frei) =>
        p.Ausfuehren("leitung_zeichnen", new JsonObject { ["serie"] = serie, ["groesse"] = groesse.DeepClone(), ["punkte"] = Pts(punkte), ["system"] = "ZUL" });

    /// <summary>Variante einer Testfamilie (neue id und neuer Name) als Entwurf.</summary>
    static BefehlsErgebnis Variante(Projekt p, string datei, string id, Action<JsonObject> aendern)
    {
        var d = Familie(datei);
        d["id"] = id; d["name"] = $"{d["name"]!.GetValue<string>()} – Variante {id}";
        aendern(d);
        return p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d });
    }

    static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Jeder Reduktionseintrag der Abfrage (ohne «gezaehlt») muss genau das Ergebnis der Formteilwahl sein (Felder wie bisher).</summary>
    static void ReduktionenWieFormteilwahl(Projekt p, JsonObject o, Leitungsserie s)
    {
        var gueltig = s.Groessen.Where(g => g.Gueltig).ToList();
        foreach (var zeile in o["groessen"]!.AsArray())
        {
            if (zeile!["gueltig"]!.GetValue<bool>() == false) continue;
            var g = s.Groesse(zeile["typ"]!.GetValue<string>())!.Groesse;
            var red = zeile["formteile"]!["reduktion"]!.AsArray();
            var erwartet = gueltig.Where(x => x.Groesse.Mass < g.Mass).ToList();
            Assert.Equal(erwartet.Count, red.Count);
            for (int i = 0; i < red.Count; i++)
            {
                var r = red[i]!.AsObject();
                Assert.Equal(erwartet[i].Typ, S(r["auf"]));
                var w = s.Formteil(p.Modell, p.Katalog, "reduktion", g, erwartet[i].Groesse);
                var was = $"{zeile["typ"]} → {erwartet[i].Typ}";
                Assert.Equal(w.Ok, r["verfuegbar"]!.GetValue<bool>());
                if (!w.Ok)
                {
                    Assert.Equal(w.Fehler, S(r["grund"]));
                    Assert.Equal(w.Vorschlag, S(r["vorschlag"]));
                    continue;
                }
                Assert.Equal(w.Familie!.Kennzeichen, S(r["familie"]));
                Assert.Equal(w.Typ, S(r["typ"]));
                Assert.Equal(w.Hinweis, S(r["hinweis"]));
                Assert.Equal(w.Frei, r["frei"]?.GetValue<bool>() == true);
                if (w.Frei) Assert.True(JsonNode.DeepEquals(w.Exemplar, r["parameter"]), was + ": parameter");
            }
        }
    }

    static List<JsonObject> Reduktionen(JsonObject o) =>
        o["groessen"]!.AsArray().Where(g => g!["gueltig"]!.GetValue<bool>()).SelectMany(g => g!["formteile"]!["reduktion"]!.AsArray()).Select(r => r!.AsObject()).ToList();

    // ------------------------------------------------------------------ (1) Reduktionen nach der Regel, Stichproben, gleich wie gerechnet

    /// <summary>Freie Testserie: Reduktionen 500x300→400x200 und 400x200→300x200 sind Typen (gerechnet), die übrigen 4 aus der Vorlage
    /// «400x200/300x200» mit Exemplar-Grössen. Ecken des Rasters: 200x200 (kleinstes b und kleinste Fläche), 500x300 (grösstes b, h, Fläche),
    /// 400x200 (erstes kleinstes h) → Stichproben 500x300→400x200, 500x300→200x200, 400x200→200x200 und das erste gezählte Paar 500x300→300x200;
    /// gezählt bleibt 300x200→200x200.</summary>
    [Fact]
    public void Abfrage_FreieSerie_ReduktionenGezaehlt_GleichWieDieFormteilwahl()
    {
        var p = MitFrei();
        var o = Leitungsserie.Abfrage(p, Frei);
        ReduktionenWieFormteilwahl(p, o, Serie(p));
        var kr = o["kontrolle"]!["reduktionen"]!;
        Assert.Equal(6, kr["paare"]!.GetValue<int>());
        Assert.Equal(6, kr["verfuegbar"]!.GetValue<int>());
        Assert.Equal(1, kr["gezaehlt"]!.GetValue<int>());
        Assert.Equal(5, kr["gerechnet"]!.GetValue<int>());
        Assert.Equal(4, kr["stichproben"]!.GetValue<int>());
        Assert.Equal("gezaehlt", S(kr["ermittlung"]));
        Assert.Null(kr["abweichung"]);
        Assert.Contains("Vorlage «400x200/300x200» mit Exemplar-Grössen", S(kr["regel"]));
        var gezaehlt = Assert.Single(Reduktionen(o), r => r["gezaehlt"]?.GetValue<bool>() == true);
        Assert.Equal("200x200", S(gezaehlt["auf"]));
        Assert.True(gezaehlt["frei"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["breite1"] = 300.0, ["hoehe1"] = 200.0, ["breite2"] = 200.0, ["hoehe2"] = 200.0 }, gezaehlt["parameter"]));
        Assert.True(o["kontrolle"]!["dauerMs"]!.GetValue<long>() >= 0);
        Assert.Contains("gezaehlt: true", Leitungsserie.AbfrageText);
    }

    /// <summary>Serien ohne freie Grösse (Testkanalserie, Testrohrserie, Abwasser d): jedes Paar über die Typen der Reduktion – genau wie die
    /// Formteilwahl, nichts gezählt (auch nicht verfügbare Paare mit demselben Grund und Vorschlag).</summary>
    [Theory]
    [InlineData("kanal")]
    [InlineData("rohr")]
    [InlineData("abwasser_d")]
    public void Abfrage_SerieOhneFreieGroesse_AllesGerechnet_GleichWieDieFormteilwahl(string art)
    {
        Projekt p;
        string id;
        switch (art)
        {
            case "kanal": p = MitKanal(); id = "fam-test-kanalserie"; break;
            case "rohr": p = MitSerie(); id = "fam-test-rohrserie"; break;
            default:
                p = Neu();
                foreach (var f in new[] { "test_abwasser_d_serie", "test_abwasser_d_bogen", "test_abwasser_d_t", "test_abwasser_d_reduktion" })
                    Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
                id = "fam-test-abwasser-d";
                break;
        }
        var o = Leitungsserie.Abfrage(p, id);
        ReduktionenWieFormteilwahl(p, o, Serie(p, id));
        var kr = o["kontrolle"]!["reduktionen"]!;
        Assert.Equal(0, kr["gezaehlt"]!.GetValue<int>());
        Assert.Equal("gerechnet", S(kr["ermittlung"]));
        Assert.True(kr["paare"]!.GetValue<int>() > 0);
        Assert.DoesNotContain(Reduktionen(o), r => r["gezaehlt"] is not null);
    }

    /// <summary>Freie Serie, deren Reduktion sich nicht einstellen lässt (a.b ist eine Formel bzw. die Vorlage fehlt bzw. die Familie fehlt):
    /// der Grund ist je Paar derselbe wie in der Formteilwahl – ohne Auswertung, nichts gezählt.</summary>
    [Fact]
    public void Abfrage_FreieSerie_NichtEinstellbar_GrundWieDieFormteilwahl()
    {
        var p = MitFrei();
        Ok(Variante(p, "test_kanalreduktion_frei", "fam-test-red-formel", d =>
        {
            foreach (var an in d["anschluesse"]!.AsArray()) if (S(an!["name"]) == "a") an["b"] = "breite1 + 0";
        }));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-red-formel", d => d["leitung"]!["formteile"]!["reduktion"]!["familie"] = "fam-test-red-formel"));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-red-vorlage", d => d["leitung"]!["formteile"]!["reduktion"]!["vorlage"] = "gibt-es-nicht"));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-red-fehlt", d => d["leitung"]!["formteile"]!["reduktion"]!["familie"] = "fam-gibt-es-nicht"));
        foreach (var id in new[] { "fam-test-frei-red-formel", "fam-test-frei-red-vorlage", "fam-test-frei-red-fehlt" })
        {
            var o = Leitungsserie.Abfrage(p, id);
            ReduktionenWieFormteilwahl(p, o, Serie(p, id));
            Assert.Contains(Reduktionen(o), r => !r["verfuegbar"]!.GetValue<bool>());
            Assert.Equal(0, o["kontrolle"]!["reduktionen"]!["gezaehlt"]!.GetValue<int>());
        }
    }

    /// <summary>Weicht eine Stichprobe von der Regel ab, wird jedes Paar gerechnet: FIKTIVE Reduktion mit l = sqrt(breite2 − 250) + 100 – die Regel
    /// hält jede Kombination für verfügbar, die Auswertung scheitert aber für klein = 200 × 200 (Wurzel einer negativen Zahl). Die Stichprobe
    /// 500x300→200x200 (Ecken) zeigt das; danach stimmt jeder Eintrag mit der Formteilwahl überein.</summary>
    [Fact]
    public void Abfrage_StichprobeWeichtAb_JedesPaarGerechnet()
    {
        var p = MitFrei();
        Ok(Variante(p, "test_kanalreduktion_frei", "fam-test-red-wurzel", d =>
        {
            foreach (var par in d["parameter"]!.AsArray()) if (S(par!["name"]) == "l") par["formel"] = "sqrt(breite2 - 250) + 100";
        }));
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-red-wurzel", d => d["leitung"]!["formteile"]!["reduktion"]!["familie"] = "fam-test-red-wurzel"));
        var o = Leitungsserie.Abfrage(p, "fam-test-frei-red-wurzel");
        var kr = o["kontrolle"]!["reduktionen"]!;
        Assert.Equal("gerechnet", S(kr["ermittlung"]));
        Assert.Contains("Stichprobe", S(kr["abweichung"]));
        Assert.Contains("200 × 200", S(kr["abweichung"]));
        Assert.Equal(0, kr["gezaehlt"]!.GetValue<int>());
        Assert.Contains(Reduktionen(o), r => !r["verfuegbar"]!.GetValue<bool>() && S(r["grund"])!.Contains("sqrt"));
        ReduktionenWieFormteilwahl(p, o, Serie(p, "fam-test-frei-red-wurzel"));
    }

    // ------------------------------------------------------------------ (1) fester Stand, ohne Sperre, abbrechbar, schnell

    /// <summary>Schritt 1 kopiert nur den Lesestand (Serie, Formteil-Familien, Projektinformation); was danach im Projekt geschieht, sieht die
    /// Rechnung nicht (fester Stand) – hier: die Reduktionsfamilie wird gelöscht, die Rechnung zeigt sie noch.</summary>
    [Fact]
    public void AbfrageVorbereiten_FesterStand_NurDieNoetigenFamilien()
    {
        var p = MitFrei();
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei("test_rohrserie") }));   // gehört nicht zur Kanalserie
        var stand = Leitungsserie.Lesestand(p.Modell, Hole(p, "FAM-1"));
        Assert.Equal(["FAM-1", "FAM-2", "FAM-3", "FAM-4"], stand.Elemente.Where(e => e.Klasse == Familien.Klasse).Select(e => e.Kennzeichen).Order().ToArray());
        Assert.All(stand.Elemente, e => Assert.True(e.Klasse is "Familie" or "Projektinformation", e.Kennzeichen));
        Assert.NotSame(Hole(p, "FAM-1"), stand.Finde("FAM-1"));   // Kopie

        var rechnen = Leitungsserie.AbfrageVorbereiten(p, Frei);
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "FAM-4" }));
        var o = rechnen(CancellationToken.None);
        Assert.Equal("FAM-4", S(o["formteilFamilien"]!["reduktion"]!["imProjekt"]));
        Assert.All(Reduktionen(o), r => Assert.True(r["verfuegbar"]!.GetValue<bool>()));
        // neu gefragt: die Familie fehlt
        var neu = Leitungsserie.Abfrage(p, Frei);
        Assert.Null(neu["formteilFamilien"]!["reduktion"]!["imProjekt"]);
        Assert.All(Reduktionen(neu), r => Assert.Contains("fehlt im Projekt", S(r["grund"])));
    }

    [Fact]
    public void Tuer_AbfrageVorbereiten_LeitungsserieSpaeter_AndereSofort_Abbruch()
    {
        var p = MitFrei();
        var par = new JsonObject { ["familie"] = Frei };
        var rechnen = Tuer.AbfrageVorbereiten(p, "leitungsserie", par);
        Assert.Equal("FAM-1", S(((JsonObject)rechnen(CancellationToken.None))["familie"]));
        // abgebrochen, bevor gerechnet wird
        using var weg = new CancellationTokenSource();
        weg.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => rechnen(weg.Token));
        // Familie fehlt: der Fehler kommt schon im Schritt 1 (unter der Sperre)
        Assert.Throws<HlksFehler>(() => Tuer.AbfrageVorbereiten(p, "leitungsserie", new JsonObject { ["familie"] = "FAM-99" }));
        // andere Abfragen rechnen sofort (wie Tuer.Abfrage) und geben nur zurück
        var andere = Tuer.AbfrageVorbereiten(p, "familien");
        Assert.Same(andere(CancellationToken.None), andere(weg.Token));
    }

    /// <summary>Grosse freie Serie wie K2 (FIKTIV: 400 Rastertypen b, h = 150, 200 … 1100 aus der Testkanalserie, rund 79 000 Reduktionen): die
    /// Abfrage zählt die Reduktionen nach der Regel und antwortet schnell. Zeitgrenze grosszügig (Ziel echt K2 &lt; 5 s; bisher wurden alle Paare
    /// einzeln gerechnet, 48 s bzw. 155 s); scheitert der Test unter fremder Last, einzeln wiederholen. Abbruch mitten in der Rechnung.</summary>
    [Fact]
    public void Abfrage_GrosseFreieSerie_SchnellGezaehlt_UndAbbrechbar()
    {
        var p = MitFrei();
        var masse = Enumerable.Range(0, 20).Select(i => 150.0 + 50 * i).ToList();
        Ok(Variante(p, "test_kanalfrei", "fam-test-frei-400", d =>
        {
            d["typen"] = new JsonArray(masse.SelectMany(b => masse.Select(h => (JsonNode)new JsonObject
            {
                ["name"] = $"{b}x{h}", ["werte"] = new JsonObject { ["b"] = b, ["h"] = h },
            })).ToArray());
        }));
        var uhr = Stopwatch.StartNew();
        var o = Leitungsserie.Abfrage(p, "fam-test-frei-400");
        var dauer = uhr.Elapsed;
        var kr = o["kontrolle"]!["reduktionen"]!;
        Assert.Equal(400, o["groessen"]!.AsArray().Count);
        Assert.True(kr["paare"]!.GetValue<int>() > 70_000, kr.ToJsonString());
        Assert.Equal("gezaehlt", S(kr["ermittlung"]));
        Assert.Equal(kr["paare"]!.GetValue<int>(), kr["verfuegbar"]!.GetValue<int>());
        Assert.True(kr["gezaehlt"]!.GetValue<int>() > 70_000);
        Assert.True(dauer < TimeSpan.FromSeconds(30), $"leitungsserie mit 400 Typen dauerte {dauer.TotalSeconds:0.0} s (Grenze 30 s, Ziel K2 < 5 s)");

        // Abbruch mitten in der Rechnung: endet bald mit OperationCanceledException statt zu Ende zu rechnen
        using var abbruch = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var rechnen = Leitungsserie.AbfrageVorbereiten(p, "fam-test-frei-400");
        uhr.Restart();
        Assert.ThrowsAny<OperationCanceledException>(() => rechnen(abbruch.Token));
        Assert.True(uhr.Elapsed < dauer + TimeSpan.FromSeconds(1), $"Abbruch nach {uhr.Elapsed.TotalSeconds:0.0} s, ganze Rechnung {dauer.TotalSeconds:0.0} s");
    }

    // ------------------------------------------------------------------ (2) Formteile freier Serien tragen ihre Grösse selbst

    /// <summary>Bogen eines Kanals 400 × 200 (= Typ «400x200» der Bogenfamilie, Typwerte breite 400, hoehe 200): die Formteilwahl setzt die
    /// Grössen als Exemplarwerte. Danach wird die Familie geändert (Typwerte 450 × 250, Vorgabe 450) – der Bogen bleibt 400 × 200 (Anschlüsse,
    /// Lage), ohne Befund P14 bzw. P45.</summary>
    [Fact]
    public void Formteil_InVorgabegroesse_TraegtSeineGroesse_FamilieAendernAendertEsNicht()
    {
        var p = MitFrei();
        Ok(Kanal(p, G(400, 200), [[0, 0, 2500], [3000, 0, 2500], [3000, 3000, 2500]]));
        var bo = Hole(p, "BO-1");
        Assert.Equal("400x200", Familien.Typ(bo));
        var ex = Familien.Exemplarwerte(bo)!;
        Assert.Equal(400, ex["breite"]!.GetValue<double>());
        Assert.Equal(200, ex["hoehe"]!.GetValue<double>());
        var vorher = Stelle(p, "BO-1", "b");
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-2",
            ["ops"] = new JsonArray(
                new JsonObject { ["op"] = "typ_setzen", ["typ"] = new JsonObject { ["name"] = "400x200", ["werte"] = new JsonObject { ["breite"] = 450, ["hoehe"] = 250 } } },
                new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "breite", ["art"] = "exemplar", ["datentyp"] = "laenge", ["gruppe"] = "anschluss", ["wert"] = 450 } }),
        }));
        var nachher = Stelle(p, "BO-1", "b");
        Assert.Equal(Leitungsgroesse.Rechteck(400, 200), nachher.Groesse);
        Etwa(vorher.Lage!, nachher.Lage, 1e-6);
        Assert.Empty(Befunde(p, "P14"));
        Assert.Empty(Befunde(p, "P45"));
        // T-Stück und Reduktion in Normgrösse tragen ihre Grösse ebenso
        var s = Serie(p);
        var t = s.Formteil(p.Modell, p.Katalog, "tstueck", Leitungsgroesse.Rechteck(400, 200), Leitungsgroesse.Rechteck(300, 200));
        Assert.Equal("400x200/300x200", t.Typ);
        Assert.False(t.Frei);
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["b_a"] = 400.0, ["h_a"] = 200.0, ["b_c"] = 300.0, ["h_c"] = 200.0 }, t.Exemplar));
        var r = s.Formteil(p.Modell, p.Katalog, "reduktion", Leitungsgroesse.Rechteck(500, 300), Leitungsgroesse.Rechteck(400, 200));
        Assert.Equal("500x300/400x200", r.Typ);
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["breite1"] = 500.0, ["hoehe1"] = 300.0, ["breite2"] = 400.0, ["hoehe2"] = 200.0 }, r.Exemplar));
        // Serien ohne freie Grösse: unverändert (Testkanalserie mit Typparametern; nur der Winkel)
        var k = MitKanal();
        var kb = Leitungsserie.Hole(k.Modell, k.Katalog, "fam-test-kanalserie").Formteil(k.Modell, k.Katalog, "bogen", Leitungsgroesse.Rechteck(400, 200), winkel: 90);
        Assert.True(kb.Ok, kb.Fehler);
        Assert.DoesNotContain("breite", kb.Exemplar?.Select(x => x.Key) ?? []);
    }

    /// <summary>Formteil aus einem bestehenden Projekt ohne Exemplar-Grössen (hier im Modell nachgestellt, wie aus einem alten Paket geladen):
    /// beim Laden wird nichts umgeschrieben, P45 meldet es mit Vorschlag; ändert sich die Familie, hält die Nachführung im selben Paket die
    /// Grösse von vorher fest (400 × 200 statt der neuen Vorgabe 450 × 250); ändert ein Befehl das Formteil selbst, ebenso.</summary>
    [Fact]
    public void BestehendesFormteil_OhneGroesse_P45_NachfuehrungHaeltFest()
    {
        var p = MitFrei();
        Ok(Kanal(p, G(400, 200), [[0, 0, 2500], [3000, 0, 2500], [3000, 3000, 2500]]));
        void OhneGroesse()
        {
            var e = Hole(p, "BO-1").Kopie();
            e.Eigenschaften["parameter"] = new Wert { Inhalt = new JsonObject { ["winkel"] = 90 }, Herkunft = e.Eigenschaft("parameter")!.Herkunft };
            p.Modell.Anwenden([new Operation { Art = "element.setzen", Id = e.Id, Basis = Hole(p, "BO-1").Version, Element = e }]);
        }
        OhneGroesse();
        var befund = Assert.Single(Befunde(p, "P45"));
        Assert.Equal("BO-1", befund.Element);
        Assert.Equal("hinweis", befund.Schwere);
        Assert.Contains("breite = 400, hoehe = 200", befund.Text);
        Assert.Contains("FAM-1", befund.Text);
        Assert.Contains("element_aendern { \"element\": \"BO-1\", \"eigenschaften\": { \"parameter\": {\"winkel\":90,\"breite\":400,\"hoehe\":200} } }", befund.Vorschlag);
        Assert.Contains("leitung_groesse { \"leitungen\": [\"KA-1\", \"KA-2\"], \"groesse\": \"400 × 200\" }", befund.Vorschlag);

        // Familie ändern: die Grösse von vorher bleibt (Nachführung im selben Paket)
        var e1 = Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-2",
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_setzen", ["typ"] = new JsonObject { ["name"] = "400x200", ["werte"] = new JsonObject { ["breite"] = 450, ["hoehe"] = 250 } } }),
        }));
        Assert.Contains(e1.Hinweise, h => h.Contains("festgehalten: BO-1 (breite = 400, hoehe = 200)"));
        Assert.Equal(400, Familien.Exemplarwerte(Hole(p, "BO-1"))!["breite"]!.GetValue<double>());
        Assert.Equal(Leitungsgroesse.Rechteck(400, 200), Stelle(p, "BO-1", "a").Groesse);
        Assert.Empty(Befunde(p, "P45"));
        Assert.Empty(Befunde(p, "P14"));
        Ok(p.Rueckgaengig());   // ein Paket: Familie und Bogen zurück
        Assert.Null(Familien.Exemplarwerte(Hole(p, "BO-1"))!["breite"]);

        // das Formteil selbst ändern (Name): die Grösse danach wird festgehalten
        var e2 = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BO-1", ["name"] = "Bogen Zuluft" }));
        Assert.Contains(e2.Hinweise, h => h.Contains("BO-1 (breite = 400, hoehe = 200)"));
        Assert.Equal(200, Familien.Exemplarwerte(Hole(p, "BO-1"))!["hoehe"]!.GetValue<double>());

        // Vorschlag von P45 ausgeführt: Befund weg, Lage gleich
        OhneGroesse();
        var lage = Stelle(p, "BO-1", "b").Lage!;
        var vorschlag = Assert.Single(Befunde(p, "P45")).Vorschlag!;
        var json = vorschlag[(vorschlag.IndexOf("element_aendern ", StringComparison.Ordinal) + "element_aendern ".Length)..];
        json = json[..(json.IndexOf(" } }", StringComparison.Ordinal) + 4)];
        Ok(p.Ausfuehren("element_aendern", (JsonObject)JsonNode.Parse(json)!));
        Assert.Empty(Befunde(p, "P45"));
        Etwa(lage, Stelle(p, "BO-1", "b").Lage, 1e-9);
    }
}
