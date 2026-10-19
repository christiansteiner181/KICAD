using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>
/// Prüfstand der Bedienungsanleitung (Häkchen und Notizen, AnleitungDienst.cs › Pruefstand; Entscheid Christian Steiner 19.10.2026: «Häkchen und
/// Notizen speichert KICAD je Benutzer über OneDrive»): Grenzen, Zusammenführen der Rechnerdateien, Testlauf-Sperre und die Endpunkte
/// GET /api/hilfe/pruefung und PUT /api/hilfe/pruefung/{id}. Jeder Test hat seinen Ordner unter %TEMP% – nie die echten Dateien des Benutzers.
/// </summary>
[Collection("AnleitungUmgebung")]
public class PruefstandTests : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_pruefstand_" + Guid.NewGuid().ToString("N")[..10]);
    public PruefstandTests() { Directory.CreateDirectory(wurzel); }
    public void Dispose()
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    static readonly DateTimeOffset T0 = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    static DateTimeOffset T(int minuten) => T0.AddMinutes(minuten);

    Pruefstand Rechner(string rechner, DateTimeOffset zeit, string? ordner = null) =>
        new(new RechnerAblage(ordner ?? Path.Combine(wurzel, "pruefung"), rechner, true, "anleitung.json", "Umleitung")) { Uhr = () => zeit };

    static JsonObject Koerper(string notiz = "", string? punkte = null) =>
        new() { ["punkte"] = JsonNode.Parse(punkte ?? "{}"), ["notiz"] = notiz, ["titel"] = "Serien-PDF öffnen", ["stand"] = "abcd1234" };

    // ---------------------------------------------------------------- Speichern und Lesen

    [Fact]
    public void Setzen_und_Alle_Rundlauf_mit_Umlauten_in_der_Datei_des_Rechners()
    {
        var p = Rechner("PC-A", T(1));
        var e = p.Setzen("zb-spdf-oeffnen", new JsonObject
        {
            ["punkte"] = new JsonObject { ["0"] = "ok", ["2"] = "mangel" }, ["notiz"] = "Dialog öffnet «langsam» – Größe?",
            ["titel"] = "Serien-PDF öffnen", ["stand"] = "abcd1234", ["texte"] = new JsonObject { ["0"] = "Knopf gesperrt ohne Planblätter", ["2"] = "Auch über Rechtsklick" },
        });
        Assert.Equal(T(1).ToString("o"), (string?)e["geaendert"]);
        var datei = Path.Combine(wurzel, "pruefung", "PC-A.json");
        Assert.True(File.Exists(datei));
        Assert.Contains("«langsam»", File.ReadAllText(datei));   // nicht als « maskiert
        Assert.Empty(Directory.GetFiles(Path.Combine(wurzel, "pruefung"), "*.tmp"));

        var alle = p.Alle();
        var a = alle["zb-spdf-oeffnen"]!.AsObject();
        Assert.Equal("ok", (string?)a["punkte"]!["0"]);
        Assert.Equal("mangel", (string?)a["punkte"]!["2"]);
        Assert.Equal("Dialog öffnet «langsam» – Größe?", (string?)a["notiz"]);
        Assert.Equal("Serien-PDF öffnen", (string?)a["titel"]);
        Assert.Equal("abcd1234", (string?)a["stand"]);
        Assert.Equal("Auch über Rechtsklick", (string?)a["texte"]!["2"]);
        Assert.Equal("PC-A", (string?)a["rechner"]);
        Assert.Equal(T(1).ToString("o"), (string?)a["geaendert"]);
    }

    [Fact]
    public void Setzen_ersetzt_den_Eintrag_derselben_Funktion_und_laesst_andere_stehen()
    {
        var p = Rechner("PC-A", T(1));
        p.Setzen("f1", Koerper(punkte: """{"0":"ok"}"""));
        p.Setzen("f2", Koerper(punkte: """{"1":"mangel"}"""));
        p.Uhr = () => T(5);
        p.Setzen("f1", Koerper("neu", """{"0":"mangel"}"""));
        var alle = p.Alle();
        Assert.Equal(2, alle.Count);
        Assert.Equal("mangel", (string?)alle["f1"]!["punkte"]!["0"]);
        Assert.Equal("neu", (string?)alle["f1"]!["notiz"]);
        Assert.Equal("mangel", (string?)alle["f2"]!["punkte"]!["1"]);
        Assert.Equal(T(1).ToString("o"), (string?)alle["f2"]!["geaendert"]);
    }

    [Fact]
    public void Leerer_Eintrag_ist_erlaubt_und_hebt_Haken_auf()
    {
        var p = Rechner("PC-A", T(1));
        p.Setzen("f1", Koerper(punkte: """{"0":"ok"}"""));
        p.Uhr = () => T(2);
        p.Setzen("f1", new JsonObject());
        var e = p.Alle()["f1"]!;
        Assert.Empty(e["punkte"]!.AsObject());
        Assert.Equal("", (string?)e["notiz"]);
    }

    [Fact]
    public void Alle_ohne_Verzeichnis_ist_leer()
    {
        Assert.Empty(Rechner("PC-A", T(1), Path.Combine(wurzel, "gibt-es-nicht")).Alle());
    }

    [Fact]
    public void Kaputte_Datei_eines_anderen_Rechners_wird_uebersprungen_die_eigene_neu_geschrieben()
    {
        var ordner = Path.Combine(wurzel, "pruefung");
        Directory.CreateDirectory(ordner);
        File.WriteAllText(Path.Combine(ordner, "PC-B.json"), "{ kaputt");
        File.WriteAllText(Path.Combine(ordner, "PC-A.json"), "nicht json");
        var p = Rechner("PC-A", T(1));
        Assert.Empty(p.Alle());
        p.Setzen("f1", Koerper(punkte: """{"0":"ok"}"""));
        Assert.Equal("ok", (string?)p.Alle()["f1"]!["punkte"]!["0"]);
        Assert.Equal("{ kaputt", File.ReadAllText(Path.Combine(ordner, "PC-B.json")));   // fremde Datei nie angefasst
    }

    [Fact]
    public void Ein_Rechner_schreibt_nur_seine_Datei()
    {
        var a = Rechner("PC-A", T(1));
        var b = Rechner("PC-B", T(2));
        a.Setzen("f1", Koerper(punkte: """{"0":"ok"}"""));
        var fremd = File.ReadAllText(Path.Combine(wurzel, "pruefung", "PC-A.json"));
        b.Setzen("f1", Koerper(punkte: """{"0":"mangel"}"""));
        Assert.Equal(fremd, File.ReadAllText(Path.Combine(wurzel, "pruefung", "PC-A.json")));
        Assert.Equal(["PC-A.json", "PC-B.json"], Directory.GetFiles(Path.Combine(wurzel, "pruefung")).Select(Path.GetFileName).OrderBy(x => x).ToArray());
    }

    // ---------------------------------------------------------------- Zusammenführen

    [Fact]
    public void Zwei_Rechner_je_Funktion_gewinnt_der_juengste_geaendert()
    {
        var a = Rechner("PC-A", T(1));
        var b = Rechner("PC-B", T(3));
        a.Setzen("f1", Koerper("von A", """{"0":"ok"}"""));
        a.Uhr = () => T(5);
        a.Setzen("f2", Koerper("von A", """{"0":"ok"}"""));   // neuer als B
        b.Setzen("f1", Koerper("von B", """{"0":"mangel"}"""));   // neuer als A
        b.Uhr = () => T(2);
        b.Setzen("f2", Koerper("von B", """{"0":"mangel"}"""));   // älter als A
        b.Setzen("f3", Koerper("nur B"));
        a.Setzen("f4", Koerper("nur A"));

        foreach (var p in new[] { a, b, Rechner("PC-C", T(9)) })   // jeder Rechner sieht dasselbe
        {
            var alle = p.Alle();
            Assert.Equal(["f1", "f2", "f3", "f4"], alle.Select(x => x.Key).ToArray());
            Assert.Equal("von B", (string?)alle["f1"]!["notiz"]);
            Assert.Equal("PC-B", (string?)alle["f1"]!["rechner"]);
            Assert.Equal("von A", (string?)alle["f2"]!["notiz"]);
            Assert.Equal("PC-A", (string?)alle["f2"]!["rechner"]);
            Assert.Equal("nur B", (string?)alle["f3"]!["notiz"]);
            Assert.Equal("nur A", (string?)alle["f4"]!["notiz"]);
        }
    }

    [Fact]
    public void Gleiche_Zeit_gewinnt_der_groessere_Rechnername_in_jeder_Reihenfolge()
    {
        JsonObject Datei(string notiz) => new() { ["eintraege"] = new JsonObject { ["f"] = new JsonObject { ["notiz"] = notiz, ["geaendert"] = T(1).ToString("o"), ["punkte"] = new JsonObject() } } };
        Assert.Equal("b", (string?)Pruefstand.Vereinigen([("PC-A", Datei("a")), ("PC-B", Datei("b"))])["f"]!["notiz"]);
        Assert.Equal("b", (string?)Pruefstand.Vereinigen([("PC-B", Datei("b")), ("PC-A", Datei("a"))])["f"]!["notiz"]);
    }

    [Fact]
    public void Vereinigen_ohne_oder_mit_unbrauchbaren_Eintraegen()
    {
        Assert.Empty(Pruefstand.Vereinigen([]));
        Assert.Empty(Pruefstand.Vereinigen([("PC-A", null), ("PC-B", new JsonObject()), ("PC-C", new JsonObject { ["eintraege"] = "text" })]));
        var d = new JsonObject { ["eintraege"] = new JsonObject { ["ok"] = new JsonObject { ["notiz"] = "x" }, ["kaputt"] = 12, [new string('x', 121)] = new JsonObject() } };
        var erg = Pruefstand.Vereinigen([("PC-A", d)]);
        Assert.Equal(["ok"], erg.Select(x => x.Key).ToArray());   // ohne «geaendert» zählt der Eintrag als ältester, bleibt aber erhalten
    }

    [Fact]
    public void Eintrag_mit_geaendert_schlaegt_Eintrag_ohne_geaendert()
    {
        JsonObject Datei(string notiz, string? zeit) { var e = new JsonObject { ["notiz"] = notiz }; if (zeit is not null) e["geaendert"] = zeit; return new JsonObject { ["eintraege"] = new JsonObject { ["f"] = e } }; }
        Assert.Equal("mit", (string?)Pruefstand.Vereinigen([("PC-Z", Datei("ohne", null)), ("PC-A", Datei("mit", T(1).ToString("o")))])["f"]!["notiz"]);
        Assert.Equal("mit", (string?)Pruefstand.Vereinigen([("PC-Z", Datei("ohne", "kein datum")), ("PC-A", Datei("mit", T(1).ToString("o")))])["f"]!["notiz"]);
    }

    // ---------------------------------------------------------------- Grenzen

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mit leerzeichen")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("ä")]
    [InlineData("a?b")]
    [InlineData("a#b")]
    [InlineData("a%b")]
    [InlineData("a*b")]
    public void Id_ungueltig_wird_abgelehnt_mit_Vorschlag(string? id)
    {
        var f = Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen(id, Koerper()));
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Fact]
    public void Id_Grenze_120_Zeichen()
    {
        Pruefstand.Pruefen(new string('a', 120), Koerper());
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen(new string('a', 121), Koerper()));
        Pruefstand.Pruefen("A-z_0.9~:@+-", Koerper());   // alle erlaubten Zeichenarten
    }

    [Fact]
    public void Punkte_Grenze_64_und_Nummern_0_bis_63()
    {
        var voll = new JsonObject(); for (int i = 0; i < 64; i++) voll[i.ToString()] = i % 2 == 0 ? "ok" : "mangel";
        Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = voll });
        var zuviel = new JsonObject(); for (int i = 0; i < 65; i++) zuviel[i.ToString()] = "ok";
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = zuviel }));
        foreach (var k in new[] { "64", "-1", "a", "01", "1.5", "", "100", "1000" })
            Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = new JsonObject { [k] = "ok" } }));
    }

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("\"OK\"")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Punktwert_nur_ok_oder_mangel(string wert)
    {
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = new JsonObject { ["0"] = JsonNode.Parse(wert) } }));
    }

    [Fact]
    public void Punkte_muss_ein_Objekt_sein()
    {
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = new JsonArray("ok") }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = "ok" }));
        Pruefstand.Pruefen("f", new JsonObject { ["punkte"] = null });   // fehlend wie null = keine Punkte
    }

    [Fact]
    public void Notiz_Grenze_4000_Zeichen()
    {
        Pruefstand.Pruefen("f", new JsonObject { ["notiz"] = new string('n', 4000) });
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["notiz"] = new string('n', 4001) }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["notiz"] = 5 }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["notiz"] = new JsonObject() }));
    }

    [Fact]
    public void Titel_Stand_und_Texte_haben_Grenzen()
    {
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["titel"] = new string('t', 301) }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["stand"] = new string('s', 65) }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["texte"] = new JsonObject { ["0"] = new string('x', 1001) } }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["texte"] = new JsonObject { ["64"] = "x" } }));
        Assert.Throws<HlksFehler>(() => Pruefstand.Pruefen("f", new JsonObject { ["texte"] = new JsonArray() }));
        Pruefstand.Pruefen("f", new JsonObject { ["titel"] = new string('t', 300), ["stand"] = new string('s', 64), ["texte"] = new JsonObject { ["63"] = new string('x', 1000) } });
    }

    [Fact]
    public void Unbekannte_Felder_im_Koerper_werden_nicht_gespeichert()
    {
        var e = Pruefstand.Pruefen("f", new JsonObject { ["notiz"] = "n", ["id"] = "anderes", ["rechner"] = "PC-X", ["boese"] = new JsonObject { ["a"] = 1 } });
        Assert.Equal(["titel", "punkte", "notiz", "stand", "texte"], e.Select(x => x.Key).ToArray());
    }

    [Fact]
    public void Setzen_lehnt_ungueltigen_Eintrag_ab_ohne_Datei_zu_schreiben()
    {
        var p = Rechner("PC-A", T(1));
        Assert.Throws<HlksFehler>(() => p.Setzen("a b", Koerper()));
        Assert.Throws<HlksFehler>(() => p.Setzen("f", new JsonObject { ["notiz"] = new string('n', 4001) }));
        Assert.False(Directory.Exists(Path.Combine(wurzel, "pruefung")));
    }

    // ---------------------------------------------------------------- Testlauf-Sperre

    [Fact]
    public void Testlauf_ohne_Umleitung_nach_Temp_weder_lesen_noch_schreiben()
    {
        // Ablage wie bei OneDrive/lokal (kein %TEMP%): im Testlauf (HLKS_TEST=1) gesperrt
        var echt = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "HLKS_TEST_NIE_ANLEGEN", "anleitung");
        var vorher = Directory.Exists(echt);
        var p = new Pruefstand(new RechnerAblage(echt, "PC-A", true, "anleitung.json", "OneDrive")) { Uhr = () => T(1) };
        Assert.False(p.Erlaubt);
        Assert.Empty(p.Alle());
        var f = Assert.Throws<HlksFehler>(() => p.Setzen("f1", Koerper()));
        Assert.Contains("Testlauf", f.Message);
        Assert.Equal(vorher, Directory.Exists(echt));   // nichts angelegt
        Assert.False(File.Exists(Path.Combine(echt, "PC-A.json")));
    }

    [Fact]
    public void Testlauf_erlaubt_jede_Ablage_unter_Temp_auch_ohne_Umleitung_als_Quelle()
    {
        var unterTemp = new Pruefstand(new RechnerAblage(Path.Combine(wurzel, "pruefung"), "PC-A", true, "anleitung.json", "OneDrive")) { Uhr = () => T(1) };
        Assert.True(unterTemp.Erlaubt);
        unterTemp.Setzen("f1", Koerper(punkte: """{"0":"ok"}"""));
        Assert.Single(unterTemp.Alle());
    }

    [Fact]
    public void Aus_Einstellungen_waehlt_Umleitung_vor_OneDrive()
    {
        var e = new Einstellungen("Test", "pc:1/x", "k", null) { AnleitungPruefung = Path.Combine(wurzel, "u") };
        var p = Pruefstand.Aus(e);
        Assert.Equal("Umleitung", p.Quelle);
        Assert.Equal(Path.Combine(wurzel, "u"), p.Verzeichnis);
        Assert.Equal("pc_1_x", p.Rechner);   // ungültige Dateinamenzeichen ersetzt
    }

    // ---------------------------------------------------------------- Endpunkte

    static StringContent Json(string s) => new(s, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Endpunkte_PUT_dann_GET_Rundlauf()
    {
        using var u = new AnleitungUmgebung().Starten();
        var put = await u.Http.PutAsync("/api/hilfe/pruefung/zb-spdf-oeffnen", Json("""
            { "titel": "Serien-PDF öffnen", "punkte": { "0": "ok", "1": "mangel" }, "notiz": "Dialog «Serien-PDF» zu klein", "stand": "72b16dc", "texte": { "0": "Knopf gesperrt", "1": "Rechtsklick" } }
            """));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var pj = JsonNode.Parse(await put.Content.ReadAsStringAsync())!;
        Assert.True((bool?)pj["gespeichert"]);
        Assert.Equal("zb-spdf-oeffnen", (string?)pj["id"]);
        Assert.Equal("testpc", (string?)pj["rechner"]);
        Assert.Equal("Umleitung", (string?)pj["quelle"]);
        Assert.NotNull((string?)pj["geaendert"]);

        var get = await u.Http.GetAsync("/api/hilfe/pruefung");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var g = JsonNode.Parse(await get.Content.ReadAsStringAsync())!;
        Assert.Equal("Umleitung", (string?)g["quelle"]);
        Assert.Equal(u.Pruefung, (string?)g["ort"]);
        Assert.Null(g["hinweis"]);
        var e = g["eintraege"]!["zb-spdf-oeffnen"]!;
        Assert.Equal("ok", (string?)e["punkte"]!["0"]);
        Assert.Equal("mangel", (string?)e["punkte"]!["1"]);
        Assert.Equal("Dialog «Serien-PDF» zu klein", (string?)e["notiz"]);
        Assert.Equal("72b16dc", (string?)e["stand"]);
        Assert.Equal("Serien-PDF öffnen", (string?)e["titel"]);
        Assert.Equal("Rechtsklick", (string?)e["texte"]!["1"]);
        Assert.Equal((string?)pj["geaendert"], (string?)e["geaendert"]);
        Assert.True(File.Exists(Path.Combine(u.Pruefung, "testpc.json")));
        Assert.Empty(Directory.GetFiles(u.Pruefung, "*.tmp"));
    }

    [Fact]
    public async Task Endpunkte_GET_fasst_Dateien_anderer_Rechner_zusammen()
    {
        using var u = new AnleitungUmgebung().Starten();
        Directory.CreateDirectory(u.Pruefung);
        File.WriteAllText(Path.Combine(u.Pruefung, "laptop.json"), new JsonObject
        {
            ["format"] = 1, ["rechner"] = "laptop", ["eintraege"] = new JsonObject
            {
                ["nur-laptop"] = new JsonObject { ["punkte"] = new JsonObject { ["0"] = "ok" }, ["notiz"] = "vom Laptop", ["geaendert"] = "2020-01-01T00:00:00.0000000Z" },
                ["beide"] = new JsonObject { ["punkte"] = new JsonObject { ["0"] = "ok" }, ["notiz"] = "alt vom Laptop", ["geaendert"] = "2020-01-01T00:00:00.0000000Z" },
                ["beide-neu"] = new JsonObject { ["punkte"] = new JsonObject { ["0"] = "ok" }, ["notiz"] = "neu vom Laptop", ["geaendert"] = "2099-01-01T00:00:00.0000000Z" },
            },
        }.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, (await u.Http.PutAsync("/api/hilfe/pruefung/beide", Json("""{ "notiz": "jetzt vom Server" }"""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await u.Http.PutAsync("/api/hilfe/pruefung/beide-neu", Json("""{ "notiz": "jetzt vom Server" }"""))).StatusCode);

        var g = JsonNode.Parse(await u.Http.GetStringAsync("/api/hilfe/pruefung"))!["eintraege"]!;
        Assert.Equal("vom Laptop", (string?)g["nur-laptop"]!["notiz"]);
        Assert.Equal("laptop", (string?)g["nur-laptop"]!["rechner"]);
        Assert.Equal("jetzt vom Server", (string?)g["beide"]!["notiz"]);
        Assert.Equal("testpc", (string?)g["beide"]!["rechner"]);
        Assert.Equal("neu vom Laptop", (string?)g["beide-neu"]!["notiz"]);   // der Laptop war später dran
        Assert.False(File.ReadAllText(Path.Combine(u.Pruefung, "laptop.json")).Contains("jetzt vom Server"));   // fremde Datei nie angefasst
    }

    [Fact]
    public async Task Endpunkte_GET_ohne_Eintraege_ist_leer()
    {
        using var u = new AnleitungUmgebung().Starten();
        var g = JsonNode.Parse(await u.Http.GetStringAsync("/api/hilfe/pruefung"))!;
        Assert.Empty(g["eintraege"]!.AsObject());
        Assert.Equal("testpc", (string?)g["rechner"]);
    }

    [Theory]
    [InlineData("a%20b")]
    [InlineData("%C3%A4")]
    [InlineData("a%2Fb")]
    [InlineData("a%5Cb")]
    [InlineData("a%3Fb")]
    [InlineData("a*b")]
    public async Task Endpunkte_PUT_ungueltige_Id_ist_400(string id)
    {
        using var u = new AnleitungUmgebung().Starten();
        var a = await u.Http.PutAsync("/api/hilfe/pruefung/" + id, Json("""{ "notiz": "x" }"""));
        Assert.True(a.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, a.StatusCode.ToString());
        if (a.StatusCode == HttpStatusCode.BadRequest) Assert.NotNull((string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        Assert.False(Directory.Exists(u.Pruefung) && Directory.GetFiles(u.Pruefung).Length > 0);
    }

    [Fact]
    public async Task Endpunkte_PUT_Id_Grenze_120()
    {
        using var u = new AnleitungUmgebung().Starten();
        Assert.Equal(HttpStatusCode.OK, (await u.Http.PutAsync("/api/hilfe/pruefung/" + new string('a', 120), Json("{}"))).StatusCode);
        var a = await u.Http.PutAsync("/api/hilfe/pruefung/" + new string('a', 121), Json("{}"));
        Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
        Assert.Contains("ungültig", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
    }

    [Fact]
    public async Task Endpunkte_PUT_Grenzen_Punkte_Notiz_Koerper_ist_400_und_schreibt_nichts()
    {
        using var u = new AnleitungUmgebung().Starten();
        var punkte65 = string.Join(",", Enumerable.Range(0, 65).Select(i => $"\"{i}\":\"ok\""));
        foreach (var koerper in new[]
        {
            "{ \"punkte\": {" + punkte65 + "} }",
            "{ \"punkte\": { \"0\": \"vielleicht\" } }",
            "{ \"punkte\": { \"64\": \"ok\" } }",
            "{ \"notiz\": \"" + new string('n', 4001) + "\" }",
            "{ \"notiz\": 5 }",
            "{ kaputt",
            "[1,2]",
            "\"text\"",
        })
        {
            var a = await u.Http.PutAsync("/api/hilfe/pruefung/f1", Json(koerper));
            Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
            var j = JsonNode.Parse(await a.Content.ReadAsStringAsync())!;
            Assert.False(string.IsNullOrWhiteSpace((string?)j["fehler"]));
            Assert.False(string.IsNullOrWhiteSpace((string?)j["vorschlag"]));
        }
        Assert.False(Directory.Exists(u.Pruefung) && Directory.GetFiles(u.Pruefung).Length > 0);
        // genau 4000 Zeichen und 64 Punkte gehen
        var punkte64 = string.Join(",", Enumerable.Range(0, 64).Select(i => $"\"{i}\":\"mangel\""));
        Assert.Equal(HttpStatusCode.OK, (await u.Http.PutAsync("/api/hilfe/pruefung/f1", Json("{ \"punkte\": {" + punkte64 + "}, \"notiz\": \"" + new string('n', 4000) + "\" }"))).StatusCode);
    }

    [Fact]
    public async Task Endpunkte_PUT_Koerper_ueber_64_KB_ist_400()
    {
        using var u = new AnleitungUmgebung().Starten();
        var gross = "{ \"notiz\": \"x\", \"unbekannt\": \"" + new string('y', 70 * 1024) + "\" }";
        var a = await u.Http.PutAsync("/api/hilfe/pruefung/f1", Json(gross));
        Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
        Assert.Contains("zu gross", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        // ohne Content-Length (gestückelt) ebenso
        var gestueckelt = new HttpRequestMessage(HttpMethod.Put, "/api/hilfe/pruefung/f1") { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(gross))) };
        gestueckelt.Headers.TransferEncodingChunked = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await u.Http.SendAsync(gestueckelt)).StatusCode);
        Assert.False(Directory.Exists(u.Pruefung) && Directory.GetFiles(u.Pruefung).Length > 0);
    }

    [Fact]
    public async Task Endpunkte_PUT_leerer_Koerper_ist_ein_leerer_Eintrag()
    {
        using var u = new AnleitungUmgebung().Starten();
        Assert.Equal(HttpStatusCode.OK, (await u.Http.PutAsync("/api/hilfe/pruefung/f1", new StringContent(""))).StatusCode);
        Assert.Single(JsonNode.Parse(await u.Http.GetStringAsync("/api/hilfe/pruefung"))!["eintraege"]!.AsObject());
    }

    [Fact]
    public async Task Endpunkte_gleichzeitige_PUT_verschiedener_Funktionen_gehen_alle_durch()
    {
        using var u = new AnleitungUmgebung().Starten();
        var aufgaben = Enumerable.Range(0, 24).Select(i => u.Http.PutAsync($"/api/hilfe/pruefung/f{i}", Json($$"""{ "punkte": { "0": "ok" }, "notiz": "n{{i}}" }"""))).ToArray();
        var antworten = await Task.WhenAll(aufgaben);
        Assert.All(antworten, a => Assert.Equal(HttpStatusCode.OK, a.StatusCode));
        var g = JsonNode.Parse(await u.Http.GetStringAsync("/api/hilfe/pruefung"))!["eintraege"]!.AsObject();
        Assert.Equal(24, g.Count);
        for (int i = 0; i < 24; i++) Assert.Equal($"n{i}", (string?)g[$"f{i}"]!["notiz"]);
        Assert.Empty(Directory.GetFiles(u.Pruefung, "*.tmp"));
    }

    [Fact]
    public async Task Endpunkte_Testlauf_ohne_Umleitung_GET_leer_mit_Hinweis_PUT_400()
    {
        using var u = new AnleitungUmgebung().Starten(pruefung: null);   // keine Umleitung: OneDrive bzw. %APPDATA% – im Testlauf gesperrt
        var g = JsonNode.Parse(await u.Http.GetStringAsync("/api/hilfe/pruefung"))!;
        Assert.Empty(g["eintraege"]!.AsObject());
        Assert.Contains("Testlauf", (string?)g["hinweis"]);
        var a = await u.Http.PutAsync("/api/hilfe/pruefung/f1", Json("""{ "notiz": "x" }"""));
        Assert.Equal(HttpStatusCode.BadRequest, a.StatusCode);
        Assert.Contains("Testlauf", (string?)JsonNode.Parse(await a.Content.ReadAsStringAsync())!["fehler"]);
        Assert.False(File.Exists(Path.Combine((string?)g["ort"]!, "testpc.json")));   // nichts in die echte Ablage geschrieben
    }

    [Fact]
    public async Task Endpunkte_nur_GET_und_PUT()
    {
        using var u = new AnleitungUmgebung().Starten();
        var a = await u.Http.PostAsync("/api/hilfe/pruefung", Json("{}"));
        Assert.True(a.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        var b = await u.Http.DeleteAsync("/api/hilfe/pruefung/f1");
        Assert.True(b.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
        var c = await u.Http.PostAsync("/api/hilfe/pruefung/f1", Json("{}"));
        Assert.True(c.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound);
    }
}
