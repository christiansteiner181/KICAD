using System.Text.Json;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1, Nachtrag 2 (Befunde der Oberfläche C1, Entscheide Hauptlauf 18.10.2026): Körper-Id je Teil,
/// familie_ansicht «grundriss», Abfrage familien_bibliothek, op parameter_umbenennen, Reihenfolge bei parameter_setzen,
/// familie in der Katalog-Kurzform. Alle Werte fiktiv.</summary>
public sealed class FamilieNachtrag2Tests : IDisposable
{
    readonly List<string> aufraeumen = [];

    public void Dispose()
    {
        foreach (var d in aufraeumen)
            for (int i = 0; i < 3 && Directory.Exists(d); i++)
            {
                try { Directory.Delete(d, true); }
                catch (IOException) { Thread.Sleep(100); }
                catch (UnauthorizedAccessException) { Thread.Sleep(100); }
            }
    }

    static JsonObject Ansicht(Projekt p, string ansicht, string detail = "fein", string typ = "22-600-1000") =>
        (JsonObject)Tuer.Abfrage(p, "familie_ansicht", new JsonObject { ["familie"] = "FAM-1", ["typ"] = typ, ["ansicht"] = ansicht, ["detailgrad"] = detail });

    static double[] Box(IEnumerable<Primitiv> pr) => Grafik.Begrenzung([new Grafikteil { Primitive = pr.ToList() }])!;

    static JsonObject Aendern(params JsonObject[] ops) => new() { ["familie"] = "FAM-1", ["ops"] = new JsonArray(ops) };

    static List<string> Parameternamen(Projekt p) =>
        ((JsonArray)((JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!)["parameter"]!).Select(x => x!["name"]!.GetValue<string>()).ToList();

    // ------------------------------------------------------------------ 1. Körper-Id je Teil

    [Fact]
    public void KoerperId_In3DUndJeGrafikteilDerSeitenansicht()
    {
        var p = MitHeizkoerper();
        var k = Assert.Single(Grafik.Koerper(p, [Hole(p, "HK-1").Id]));
        Assert.Equal(["platten", "abdeckung", "stutzen_vl", "stutzen_rl"], k.Teile.Select(t => t.Teil));
        var k3 = Ansicht(p, "3d")["koerper"].Deserialize<Koerper>(Json.Optionen)!;
        Assert.Equal(["platten", "abdeckung", "stutzen_vl", "stutzen_rl"], k3.Teile.Select(t => t.Teil));
        Assert.Equal("platten", Ansicht(p, "3d")["koerper"]!["teile"]![0]!["teil"]!.GetValue<string>());   // im JSON «teil»
        foreach (var seite in new[] { "vorne", "oben", "links" })
        {
            var g = Ansicht(p, seite)["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!;
            var modell = g.Teile.Where(t => t.Id is not null).ToList();
            Assert.NotEmpty(modell);
            Assert.All(modell, t => Assert.NotNull(t.Teil));
            Assert.Equal(modell.Count, modell.Select(t => t.Teil).Distinct().Count());   // je Körper ein Grafikteil
        }
        // vorne sieht man Platten, Abdeckung und Stutzen; von oben deckt die Abdeckung alles (gemeinsame Verdeckung)
        Assert.Equal(["platten", "abdeckung", "stutzen_vl", "stutzen_rl"],
            Ansicht(p, "vorne")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!.Teile.Where(t => t.Id is not null).Select(t => t.Teil));
        Assert.Equal(["abdeckung"], Ansicht(p, "oben")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!.Teile.Where(t => t.Id is not null).Select(t => t.Teil));
        // vorne: die Platten allein liegen bei u 0 … 1000, v 150 … 750 (teilweise von der Abdeckung verdeckt, nicht ganz)
        var vorne = Ansicht(p, "vorne")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!;
        var platten = Box(vorne.Teile.Single(t => t.Teil == "platten").Primitive);
        Etwa(0, platten[0], 1e-3); Etwa(1000, platten[2], 1e-3); Etwa(150, platten[1], 1e-3);
    }

    // ------------------------------------------------------------------ 2. familie_ansicht grundriss

    [Fact]
    public void Grundriss_WieImProjekt_SinnbildSonstUmriss()
    {
        // mittel: Umriss = Abdeckung [−5, 1005] × [−5, 105]; grob: Platten [0, 1000] × [0, 100] (Familienkoordinaten, Drehung 0)
        var p = MitFamilie();
        var g = Ansicht(p, "grundriss", "mittel")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!;
        Assert.Equal("grundriss", g.Art);
        var t = Assert.Single(g.Teile);
        Etwa([-5, -5, 1005, 105], Box(t.Primitive.Where(x => x.Art == "polygon")), 1e-6);
        Assert.Equal(2, t.Primitive.Count(x => x.Art == "kreis" && x.Stil.NurBildschirm));   // Anschlussmarken
        Assert.DoesNotContain(t.Primitive, x => x.Art == "text");                             // kein Etikett
        Assert.Contains(t.Fangpunkte!, q => Math.Abs(q[0] - 950) < 1e-6 && Math.Abs(q[1] - 50) < 1e-6);   // vl
        Etwa([0, 0, 1000, 100], Box(Ansicht(p, "grundriss", "grob")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!.Teile[0].Primitive.Where(x => x.Art == "polygon")), 1e-6);
        // oben bleibt die reine Körperansicht (Sichtkanten, kein gefülltes Umrisspolygon)
        Assert.DoesNotContain(Ansicht(p, "oben", "mittel")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!.Teile.SelectMany(x => x.Primitive), x => x.Art == "polygon");
        // mit Sinnbild: das Sinnbild statt des Umrisses
        Ok(p.Ausfuehren("familie_aendern", Aendern(new JsonObject
        {
            ["op"] = "sinnbild_setzen",
            ["sinnbild"] = new JsonArray(new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(0, 0), new JsonArray("laenge", "tiefe")) }),
        })));
        var s = Ansicht(p, "grundriss", "mittel")["plangrafik"].Deserialize<Plangrafik>(Json.Optionen)!.Teile[0];
        Assert.DoesNotContain(s.Primitive, x => x.Art == "polygon");
        var linie = s.Primitive.Single(x => x.Art == "linie" && !x.Stil.NurBildschirm);
        Etwa([1000, 100], linie.Punkte[1], 1e-6);
    }

    // ------------------------------------------------------------------ 3. familien_bibliothek

    /// <summary>Katalog-Kopie in einem Temp-Ordner (die echte Bibliothek kataloge/familien bleibt unberührt).</summary>
    Katalog KatalogMitBibliothek(bool mitBibliothek)
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-bibliothek-" + Guid.NewGuid().ToString("N")[..8]);
        aufraeumen.Add(ordner);
        Directory.CreateDirectory(ordner);
        foreach (var f in Directory.GetFiles(Kat.Ordner, "*.json")) File.Copy(f, Path.Combine(ordner, Path.GetFileName(f)));
        if (mitBibliothek)
        {
            var bib = Path.Combine(ordner, "familien");
            Directory.CreateDirectory(bib);
            File.Copy(TestheizkoerperPfad, Path.Combine(bib, "test_heizkoerper.familie.json"));
            File.WriteAllText(Path.Combine(bib, "kaputt.familie.json"), "{ nicht json");
        }
        return Katalog.Laden(ordner);
    }

    [Fact]
    public void Bibliothek_ListetDateienUndOhneOrdnerEinenHinweis()
    {
        using var umgebung = Hlks.Tests.Gemeinsam.TestUmgebung.Ohne();   // T1: unabhängig von HLKS_FAMILIENBIBLIOTHEK von aussen
        var kat = KatalogMitBibliothek(true);
        var p = Projekt.Anlegen(kat, null, "Test Bibliothek", "T", "pc");
        var l = (JsonObject)Tuer.Abfrage(p, "familien_bibliothek");
        var eintraege = l["familien"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(2, eintraege.Count);
        var hk = eintraege.Single(x => x["datei"]!.GetValue<string>() == "test_heizkoerper.familie.json");
        Assert.Equal(HkName, hk["name"]!.GetValue<string>());
        Assert.Equal("fam-test-heizkoerper", hk["id"]!.GetValue<string>());
        Assert.Equal("Heizkoerper", hk["kategorie"]!.GetValue<string>());
        Assert.Equal("keiner – Testfamilie", hk["hersteller"]!.GetValue<string>());
        Assert.Equal(1, hk["version"]!.GetValue<double>());
        Assert.Equal("entwurf", hk["status"]!.GetValue<string>());
        Assert.Equal(3, hk["typen"]!.GetValue<int>());
        Assert.Equal(64, hk["pruefsumme"]!.GetValue<string>().Length);
        Assert.Null(hk["imProjekt"]);
        Assert.NotNull(eintraege.Single(x => x["datei"]!.GetValue<string>() == "kaputt.familie.json")["fehler"]);
        // nach dem Laden: im Projekt (gleiche id) und aktuell (gleiche Prüfsumme)
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = "test_heizkoerper" }));
        hk = ((JsonObject)Tuer.Abfrage(p, "familien_bibliothek"))["familien"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["datei"]!.GetValue<string>() == "test_heizkoerper.familie.json");
        Assert.Equal("FAM-1", hk["imProjekt"]!.GetValue<string>());
        Assert.True(hk["aktuell"]!.GetValue<bool>());
        Assert.Equal("test_heizkoerper.familie.json", ((JsonObject)Hole(p, "FAM-1").Eigenschaft("bibliothek")!.Inhalt!)["datei"]!.GetValue<string>());
        // ohne Ordner: leere Liste mit Hinweis
        var leer = (JsonObject)Tuer.Abfrage(Projekt.Anlegen(KatalogMitBibliothek(false), null, "Leer", "T", "pc"), "familien_bibliothek");
        Assert.Empty(leer["familien"]!.AsArray());
        Assert.Contains("gibt es noch nicht", leer["hinweis"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ 4. parameter_umbenennen

    /// <summary>Testfamilie mit Mass und Sinnbild – damit die Fundstellen eines Namens vorkommen (Quellen prüft B4 gegen echte
    /// Unterlagen; sie stehen im Test Umbenennen_QuellenKonflikteAbdeckung direkt an der Definition).</summary>
    static JsonObject MitAllenFundstellen()
    {
        var d = Testheizkoerper();
        d["masse"] = JsonNode.Parse("""[{ "name": "H", "ansicht": "vorne", "parameter": "hoehe", "von": [0, 0, "bodenabstand"], "bis": [0, 0, "oberkante"], "richtung": "z" }]""");
        d["sinnbild"] = JsonNode.Parse("""[{ "art": "linie", "punkte": [[0, "bodenabstand / 10"], ["laenge", "tiefe"]], "sichtbar": "bodenabstand > 0" }]""");
        return d;
    }

    [Fact]
    public void Umbenennen_UeberallAuchInDenExemplarwertenDerElemente()
    {
        var p = MitFamilie();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MitAllenFundstellen(), ["familie"] = "FAM-1" }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject
        {
            ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0, 0), ["kennzeichen"] = "HK-1", ["parameter"] = new JsonObject { ["bodenabstand"] = 200 },
        }));
        var vorher = Parameternamen(p);
        var e = Ok(p.Ausfuehren("familie_aendern", Aendern(
            new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = "bodenabstand", ["neu"] = "unterkante" },
            new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = "hoehe", ["neu"] = "bauhöhe" })));
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "HK-1");   // Exemplarwerte im selben Paket
        Assert.Contains(e.Hinweise, h => h.Contains("bodenabstand → unterkante") && h.Contains("HK-1"));

        // Reihenfolge der Parameter bleibt
        Assert.Equal(vorher.Select(n => n switch { "bodenabstand" => "unterkante", "hoehe" => "bauhöhe", _ => n }), Parameternamen(p));
        var def = (JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!;
        string F(JsonNode? n) => n!.GetValue<string>();
        Assert.Equal("unterkante + bauhöhe", F(def["parameter"]!.AsArray().Single(x => F(x!["name"]) == "oberkante")!["formel"]));
        Assert.Equal("unterkante", F(def["koerper"]![0]!["von"]![2]));                                  // Körper
        Assert.Equal("unterkante - stutzen", F(def["anschluesse"]![0]!["lage"]![2]));                    // Anschluss
        Assert.Equal("bauhöhe", F(def["masse"]![0]!["parameter"]));                                      // masse[].parameter
        Assert.Equal("unterkante", F(def["masse"]![0]!["von"]![2]));                                     // Mass-Formel
        Assert.Equal("unterkante / 10", F(def["sinnbild"]![0]!["punkte"]![0]![1]));                      // Sinnbild
        Assert.Equal("unterkante > 0", F(def["sinnbild"]![0]!["sichtbar"]));
        var typ = def["typen"]![0]!.AsObject();
        Assert.Equal(["bauhöhe", "laenge", "leistung"], typ["werte"]!.AsObject().Select(x => x.Key));     // Typwerte, Reihenfolge bleibt
        // Element: Exemplarwert umbenannt, Wirkung unverändert (Anschlüsse z = 200 − 30 = 170)
        var hk = Hole(p, "HK-1");
        Assert.Equal(200, ((JsonObject)hk.Eigenschaft("parameter")!.Inhalt!)["unterkante"]!.GetValue<double>());
        Assert.False(((JsonObject)hk.Eigenschaft("parameter")!.Inhalt!).ContainsKey("bodenabstand"));
        Etwa(170, Familien.Anschlusslagen(p.Modell, p.Katalog, hk).Single(a => a.Name == "vl").Lage[2]);
        // rückgängig: Familie und Element zusammen
        Ok(p.Rueckgaengig());
        Assert.Contains("bodenabstand", Parameternamen(p));
        Assert.True(((JsonObject)Hole(p, "HK-1").Eigenschaft("parameter")!.Inhalt!).ContainsKey("bodenabstand"));
    }

    [Fact]
    public void Umbenennen_QuellenKonflikteAbdeckungUndMediumparameter()
    {
        var d = JsonAus("""
            { "parameter": [{ "name": "a", "art": "typ", "datentyp": "laenge" }, { "name": "med", "art": "typ", "datentyp": "medium", "wert": "KW" }],
              "typen": [{ "name": "T", "werte": { "a": 1, "med": "WW" }, "quellen": { "a": ["0123456789ab#1"], "med": ["0123456789ab#2"] } }],
              "anschluesse": [{ "name": "x", "medium": "med", "fluss": "rein", "lage": [0, 0, "a"], "richtung": [0, 0, 1] }],
              "konflikte": [{ "id": "k1", "parameter": "a", "werte": [] }],
              "abdeckung": { "a": { "fehlt": true }, "T/a": { "fehlt": false }, "T/med": { "fehlt": false } } }
            """);
        Assert.Equal(("a", "b"), FamilienUmbenennen.Anwenden(d, new JsonObject { ["alt"] = "a", ["neu"] = "b" }, 1));
        Assert.Equal(["b", "med"], d["typen"]![0]!["quellen"]!.AsObject().Select(x => x.Key));
        Assert.Equal("b", d["konflikte"]![0]!["parameter"]!.GetValue<string>());
        Assert.Equal(["b", "T/b", "T/med"], d["abdeckung"]!.AsObject().Select(x => x.Key));
        Assert.Equal("b", d["anschluesse"]![0]!["lage"]![2]!.GetValue<string>());
        FamilienUmbenennen.Anwenden(d, new JsonObject { ["alt"] = "med", ["neu"] = "medium_vl" }, 2);
        Assert.Equal("medium_vl", d["anschluesse"]![0]!["medium"]!.GetValue<string>());   // Medium aus dem Parameter
    }

    [Theory]
    [InlineData("bodenabstand", "hoehe", "gibt es schon")]
    [InlineData("bodenabstand", "Hoehe", "Gross/Kleinschreibung")]
    [InlineData("gibtsnicht", "x", "gibt es nicht")]
    [InlineData("bodenabstand", "1x", "ungültig")]
    [InlineData("bodenabstand", "bodenabstand", "gleich")]
    public void Umbenennen_FehlerMitVorschlag(string alt, string neu, string teil)
    {
        var p = MitFamilie();
        var n = Nein(p.Ausfuehren("familie_aendern", Aendern(new JsonObject { ["op"] = "parameter_umbenennen", ["alt"] = alt, ["neu"] = neu })));
        Assert.Contains(teil, n.Fehler);
        Assert.Equal(1, Hole(p, "FAM-1").Zahl("version"));
    }

    [Fact]
    public void Umbenennen_ImFormeltextNurNamen()
    {
        Assert.Equal("max(m, 2) + 3 * m - \"max\" + max2", FamilienUmbenennen.Formel("max(max, 2) + 3 * max - \"max\" + max2", "max", "m"));
        Assert.Equal("if(b>1,b,1.5e3)", FamilienUmbenennen.Formel("if(a>1,a,1.5e3)", "a", "b"));
        Assert.Equal("e + 2e3", FamilienUmbenennen.Formel("x + 2e3", "x", "e"));
        Assert.Equal("Hoehe + hoehe", FamilienUmbenennen.Formel("Hoehe + h", "h", "hoehe"));   // gross/klein genau
    }

    // ------------------------------------------------------------------ 5. Reihenfolge bei parameter_setzen

    [Fact]
    public void ParameterSetzen_BestehenderBleibtAnSeinerStelleNeuerAmEnde()
    {
        var p = MitFamilie();
        var vorher = Parameternamen(p);
        Assert.Equal(2, vorher.IndexOf("tiefe"));
        Ok(p.Ausfuehren("familie_aendern", Aendern(
            new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "tiefe", ["art"] = "typ", ["datentyp"] = "laenge", ["wert"] = 120 } },
            new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "farbe", ["art"] = "typ", ["datentyp"] = "text", ["wert"] = "weiss" } })));
        var nachher = Parameternamen(p);
        Assert.Equal([.. vorher, "farbe"], nachher);
        var def = (JsonObject)Hole(p, "FAM-1").Eigenschaft("definition")!.Inhalt!;
        Assert.Equal(120, def["parameter"]![2]!["wert"]!.GetValue<double>());
    }

    // ------------------------------------------------------------------ 6. Katalog-Kurzform

    [Fact]
    public void Katalog_KurzformNenntFamilienfaehigkeit()
    {
        var p = Neu();
        var klassen = ((JsonObject)Tuer.Abfrage(p, "katalog"))["klassen"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["name"]!.GetValue<string>());
        Assert.True(klassen["Heizkoerper"]["familie"]!.GetValue<bool>());
        Assert.True(klassen["Ventil"]["familie"]!.GetValue<bool>());          // geerbt von Armatur
        Assert.True(klassen["Rohr"]["familie"]!.GetValue<bool>());           // Modul 3: Leitungsserie
        Assert.False(klassen["Raum"]["familie"]!.GetValue<bool>());
        Assert.False(klassen["Familie"]["familie"]!.GetValue<bool>());
        Assert.All(klassen.Values, k => Assert.NotNull(k["familie"]));
        Assert.Equal(p.Katalog.Klassen.Values.Count(k => !k.Abstrakt && k.Familie), klassen.Values.Count(k => k["familie"]!.GetValue<bool>()));
    }
}
