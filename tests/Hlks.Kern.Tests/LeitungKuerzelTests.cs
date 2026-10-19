using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B9 (Entscheid Christian Steiner 19.10.2026): Rohre mit Serie werden mit Werkstoff-Kürzel und
/// Grösse beschriftet. Nachgeführt mit B9b (Entscheide Christian 19.10.2026): Abwasser trägt den Aussendurchmesser («PE-S 110»), alle
/// anderen Rohre NW bzw. DN («CNS NW 20»); das «CNS Ø22» aus B9 gilt nicht mehr. Das Kürzel ist ein Feld des Blocks «leitung» der Serie (leitung.kuerzel). Alle Kürzel
/// und Werte in den Tests sind FIKTIV («XY», «ZW»; Testrohrserie da = DN + 5) – keine Herstellerwerte, keine Normmasse.</summary>
public class LeitungKuerzelTests
{
    // ------------------------------------------------------------------ Hilfen

    static FamilieDef Lesen(JsonObject d) => FamilieDef.Lesen(d, Kat);

    static JsonObject L(JsonObject d) => d["leitung"]!.AsObject();

    static JsonObject RohrDef(string? kuerzel)
    {
        var d = Familie("test_rohrserie");
        if (kuerzel is not null) L(d)["kuerzel"] = kuerzel;
        return d;
    }

    /// <summary>Projekt mit der Testrohrserie als FAM-1; mit kuerzel über den Befehl familie_aendern (op leitung_setzen).</summary>
    static Projekt MitKuerzel(string? kuerzel)
    {
        var p = MitSerie(false);
        if (kuerzel is not null) KuerzelSetzen(p, kuerzel);
        return p;
    }

    static void KuerzelSetzen(Projekt p, string? kuerzel)
    {
        var block = (JsonObject)Serie(p).Def.Quelle["leitung"]!.DeepClone();
        if (kuerzel is null) block.Remove("kuerzel"); else block["kuerzel"] = kuerzel;
        Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "leitung_setzen", ["leitung"] = block }),
        }));
    }

    static void Aendern(Projekt p, string kz, JsonObject eig) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = eig }));

    static string Text(Projekt p, string kz) => Leitungsbezeichnung.Text(p, Hole(p, "AN-EG"), Hole(p, kz))!;

    /// <summary>FIKTIVE Mini-Rohrserie mit Aussendurchmessern, die ganzzahlig sind oder Nachkommastellen haben.</summary>
    static JsonObject MiniRohr(string kuerzel) => JsonNode.Parse($$"""
        { "format": 1, "id": "fam-test-minirohr", "name": "Minirohr (fiktiv)", "kategorie": "Rohr",
          "beschreibung": "FIKTIVE Mini-Serie für Tests, keine Herstellerdaten",
          "parameter": [ { "name": "dn", "art": "typ", "datentyp": "dn", "gruppe": "abmessung", "ziel": "dn" },
                         { "name": "da", "art": "typ", "datentyp": "laenge", "gruppe": "abmessung" } ],
          "typen": [ { "name": "A", "werte": { "dn": 20, "da": 22 } }, { "name": "B", "werte": { "dn": 25, "da": 22.5 } },
                     { "name": "C", "werte": { "dn": 32, "da": 22.04 } }, { "name": "D", "werte": { "dn": 40, "da": 22.46 } },
                     { "name": "E", "werte": { "dn": 50, "da": 110 } } ],
          "koerper": [], "anschluesse": [],
          "leitung": { "querschnitt": "rund", "groesse": { "dn": "dn" }, "aussen": "da", "kuerzel": "{{kuerzel}}" } }
        """)!.AsObject();

    static void RohrMitTyp(Projekt p, string kz, string serie, string typ, double y, string system = "KW") =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = serie },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, y, 500], [3000, y, 500]) },
        }));

    // ------------------------------------------------------------------ Bezeichnung

    [Fact]
    public void Text_RohrMitSerieUndKuerzel_KuerzelUndAussendurchmesser()
    {
        var p = MitKuerzel("XY");
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 50", [0, 1000, 500], [3000, 1000, 500]));
        // Entscheid Christian 19.10.2026 (B9b): Rohre ausser Abwasser mit NW (Vorgabe), nicht mit dem Aussendurchmesser (vorher «XY Ø25»)
        Assert.Equal("XY NW 20", Text(p, "RO-1"));
        Assert.Equal("XY NW 50", Text(p, "RO-2"));
        // die Einstellung NW/DN gilt jetzt auch für Rohre mit Serie
        Aendern(p, "AN-EG", new JsonObject { ["rohrbezeichnung"] = "DN" });
        Assert.Equal("XY DN 20", Text(p, "RO-1"));
    }

    [Fact]
    public void Text_RohrMitSerieOhneKuerzel_NurDieGroesse()
    {
        var p = MitKuerzel(null);
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Assert.Equal("NW 20", Text(p, "RO-1"));
        // Kürzel setzen und wieder entfernen: die Beschriftung folgt
        KuerzelSetzen(p, "XY");
        Assert.Equal("XY NW 20", Text(p, "RO-1"));
        KuerzelSetzen(p, null);
        Assert.Equal("NW 20", Text(p, "RO-1"));
    }

    [Fact]
    public void Text_RohrOhneSerie_NwOderDnWieBisher()
    {
        var p = MitKuerzel("XY");   // die Serie mit Kürzel ändert nichts an Rohren ohne Serie
        Ok(RohrOhneSerie(p, "RO-9", 20, [0, 0, 500], [3000, 0, 500]));
        Assert.Equal("NW 20", Text(p, "RO-9"));
        Aendern(p, "AN-EG", new JsonObject { ["rohrbezeichnung"] = "DN" });
        Assert.Equal("DN 20", Text(p, "RO-9"));
    }

    [Fact]
    public void Text_Kanal_BleibtBreiteMalHoeheBzwDurchmesser_AuchMitKuerzelInDerSerie()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "ZUL", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Zuluft" } }));
        var rund = JsonNode.Parse("""
            { "format": 1, "id": "fam-test-kanal-xy", "name": "Kanal rund mit Kürzel (fiktiv)", "kategorie": "Kanal",
              "parameter": [ { "name": "d", "art": "typ", "datentyp": "laenge" } ],
              "typen": [ { "name": "160", "werte": { "d": 160 } } ], "koerper": [], "anschluesse": [],
              "leitung": { "querschnitt": "rund", "groesse": { "d": "d" }, "aussen": "d + 1", "kuerzel": "XY" } }
            """)!.AsObject();
        var eckig = JsonNode.Parse("""
            { "format": 1, "id": "fam-test-kanal-xy-eckig", "name": "Kanal eckig mit Kürzel (fiktiv)", "kategorie": "Kanal",
              "parameter": [ { "name": "b", "art": "typ", "datentyp": "laenge" }, { "name": "h", "art": "typ", "datentyp": "laenge" } ],
              "typen": [ { "name": "400x200", "werte": { "b": 400, "h": 200 } } ], "koerper": [], "anschluesse": [],
              "leitung": { "querschnitt": "rechteck", "groesse": { "b": "b", "h": "h" }, "aussen": 0.001, "kuerzel": "XY" } }
            """)!.AsObject();
        var r = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = rund }));   // FAM-2
        Assert.Contains(r.Hinweise, h => h.Contains("kuerzel wirkt nur bei Rohren"));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = eckig }));            // FAM-3
        foreach (var (kz, serie, typ, y) in new[] { ("KA-1", "FAM-2", "160", 0.0), ("KA-2", "FAM-3", "400x200", 1000.0) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Kanal", ["kennzeichen"] = kz, ["system"] = "ZUL", ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = serie },
                ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts([0, y, 500], [2000, y, 500]) },
            }));
        Assert.Equal("Ø 160", Text(p, "KA-1"));
        Assert.Equal("400×200", Text(p, "KA-2"));
    }

    [Fact]
    public void Text_Aussendurchmesser_GanzzahligOhneNachkommaSonstHoechstensEine()
    {
        var p = MitSerie(false);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = MiniRohr("ZW") }));   // FAM-2
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } }));
        // Abwasser (B9b): Aussendurchmesser ohne Ø und ohne NW/DN
        RohrMitTyp(p, "RO-A", "FAM-2", "A", 0, "SW");
        RohrMitTyp(p, "RO-B", "FAM-2", "B", 500, "SW");
        RohrMitTyp(p, "RO-C", "FAM-2", "C", 1000, "SW");
        RohrMitTyp(p, "RO-D", "FAM-2", "D", 1500, "SW");
        RohrMitTyp(p, "RO-E", "FAM-2", "E", 2000, "SW");
        Assert.Equal("ZW 22", Text(p, "RO-A"));
        Assert.Equal("ZW 22.5", Text(p, "RO-B"));    // eine Nachkommastelle, Punkt
        Assert.Equal("ZW 22", Text(p, "RO-C"));      // 22.04 → auf eine Stelle 22.0 → ganzzahlig
        Assert.Equal("ZW 22.5", Text(p, "RO-D"));    // 22.46 → 22.5
        Assert.Equal("ZW 110", Text(p, "RO-E"));
        // dasselbe Rohr im Trinkwasser: NW, nie der Aussendurchmesser
        RohrMitTyp(p, "RO-W", "FAM-2", "E", 2500);
        Assert.Equal("ZW NW 50", Text(p, "RO-W"));
        Assert.Equal("22", Leitungsbezeichnung.Aussendurchmesser(22.0));
        Assert.Equal("22.5", Leitungsbezeichnung.Aussendurchmesser(22.5));
        Assert.Equal("0.3", Leitungsbezeichnung.Aussendurchmesser(0.25));
    }

    [Fact]
    public void Text_GanzUnabhaengigVomDeutschenZahlenformat()
    {
        var alt = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-CH");
            Assert.Equal("22.5", Leitungsbezeichnung.Aussendurchmesser(22.5));   // Punkt, nie Komma
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = alt; }
    }

    [Fact]
    public void Grundriss_Fein_ZeigtDieNeueBeschriftung_GrobNichts()
    {
        var p = MitKuerzel("XY");
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "fein" });
        var t = Grafik.Plan(p, "AN-EG").Teile.Single(x => x.Id == Hole(p, "RO-1").Id);
        Assert.Equal("XY NW 20", t.Primitive.Single(x => x.Art == "text").Text);
        // ohne Kürzel nur die Grösse
        KuerzelSetzen(p, null);
        var t2 = Grafik.Plan(p, "AN-EG").Teile.Single(x => x.Id == Hole(p, "RO-1").Id);
        Assert.Equal("NW 20", t2.Primitive.Single(x => x.Art == "text").Text);
        // Detailgrad mittel: keine Beschriftung (wie bisher)
        KuerzelSetzen(p, "XY");
        Aendern(p, "AN-EG", new JsonObject { ["detailgrad"] = "mittel" });
        Assert.DoesNotContain(Grafik.Plan(p, "AN-EG").Teile.Single(x => x.Id == Hole(p, "RO-1").Id).Primitive, x => x.Art == "text");
    }

    // ------------------------------------------------------------------ Familienformat

    [Fact]
    public void Format_KuerzelWirdGelesen_UndFehltOhneAngabe()
    {
        var f = Lesen(RohrDef("PE-S"));
        Assert.True(f.Gueltig, string.Join(" | ", f.Fehler));
        Assert.Equal("PE-S", f.Leitung!.Kuerzel);
        Assert.Null(Lesen(RohrDef(null)).Leitung!.Kuerzel);
        // Leerzeichen am Rand fallen weg, genau 12 Zeichen gehen
        Assert.Equal("XY", Lesen(RohrDef("  XY ")).Leitung!.Kuerzel);
        var zwoelf = new string('X', FamilieDef.KuerzelMax);
        Assert.Equal(12, FamilieDef.KuerzelMax);
        Assert.True(Lesen(RohrDef(zwoelf)).Gueltig);
        // Sonderzeichen, Umlaute und Bindestrich sind erlaubt (Werkstoffkürzel wie «PE-S», «PE-Xa/Al»)
        Assert.True(Lesen(RohrDef("PE-Xa/Al")).Gueltig);
    }

    static void Fehler(JsonObject d, string text)
    {
        var f = Lesen(d);
        Assert.False(f.Gueltig, "hätte ungültig sein sollen");
        var e = Assert.Single(f.Fehler, x => x.Stelle == "leitung.kuerzel");
        Assert.Contains(text, e.Meldung);
        Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag), "Vorschlag fehlt");
        Assert.Null(f.Leitung!.Kuerzel);   // ein ungültiges Kürzel wirkt nie
    }

    [Fact]
    public void Validierung_ZuLangMitZeilenumbruchLeerOderKeinText_WirdAbgelehnt()
    {
        Fehler(RohrDef(new string('X', FamilieDef.KuerzelMax + 1)), "zu lang");
        Fehler(RohrDef("AB\nCD"), "Zeilenumbruch");
        Fehler(RohrDef("AB\r\nCD"), "Zeilenumbruch");
        Fehler(RohrDef("AB\tCD"), "Steuerzeichen");
        Fehler(RohrDef(""), "leer");
        Fehler(RohrDef("   "), "leer");
        var zahl = RohrDef(null); L(zahl)["kuerzel"] = 5;
        Fehler(zahl, "muss ein Text sein");
        var obj = RohrDef(null); L(obj)["kuerzel"] = new JsonObject();
        Fehler(obj, "muss ein Text sein");
        // ein Entwurf mit ungültigem Kürzel wird abgelehnt (Befehl familie_entwurf)
        var p = MitSerie(false);
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = RohrDef(new string('X', 13)) }));
        Assert.Contains("kuerzel", n.Fehler);
    }

    [Fact]
    public void Schema_KennztKuerzelMitGrenzen()
    {
        var schema = JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Path.Combine("..", "..", "schema", "hlks-familie.schema.json"))))!;
        var k = schema["properties"]!["leitung"]!["properties"]!["kuerzel"]!.AsObject();
        Assert.Equal("string", k["type"]!.GetValue<string>());
        Assert.Equal(FamilieDef.KuerzelMax, k["maxLength"]!.GetValue<int>());
        Assert.Equal(1, k["minLength"]!.GetValue<int>());
        Assert.DoesNotContain("kuerzel", schema["properties"]!["leitung"]!["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        var muster = new System.Text.RegularExpressions.Regex(k["pattern"]!.GetValue<string>());
        Assert.Matches(muster, "PE-S");
        Assert.DoesNotMatch(muster, "A\nB");
        Assert.DoesNotMatch(muster, "A\tB");
    }

    // ------------------------------------------------------------------ Rundreise

    [Fact]
    public void Rundreise_FamilieMitKuerzel_AbfrageAendernRueckgaengigUndDatei()
    {
        var p2 = MitKuerzel("XY");
        var def = (JsonObject)((JsonObject)Tuer.Abfrage(p2, "familie", new JsonObject { ["familie"] = "FAM-1" }))["definition"]!;
        Assert.Equal("XY", def["leitung"]!["kuerzel"]!.GetValue<string>());
        // der übrige Block bleibt unverändert
        Assert.Equal("da", def["leitung"]!["aussen"]!.GetValue<string>());
        Assert.Equal("fam-test-bogen", def["leitung"]!["formteile"]!["bogen"]!["familie"]!.GetValue<string>());
        // Abfrage leitungsserie nennt Kürzel und Beschriftung je Grösse
        var s = (JsonObject)Tuer.Abfrage(p2, "leitungsserie", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Equal("XY", s["kuerzel"]!.GetValue<string>());
        var g20 = s["groessen"]!.AsArray().Single(g => g!["typ"]!.GetValue<string>() == "DN 20")!;
        Assert.Equal("XY NW 20", g20["beschriftung"]!.GetValue<string>());                 // Rohre ausser Abwasser
        Assert.Equal("XY 25", g20["beschriftungAbwasser"]!.GetValue<string>());           // Abwasser: Aussendurchmesser (FIKTIV da = DN + 5)
        // ändern und rückgängig
        KuerzelSetzen(p2, "ZW");
        Assert.Equal("ZW", Serie(p2).Block.Kuerzel);
        Ok(p2.Rueckgaengig());
        Assert.Equal("XY", Serie(p2).Block.Kuerzel);
        // entfernen
        KuerzelSetzen(p2, null);
        var s2 = (JsonObject)Tuer.Abfrage(p2, "leitungsserie", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Null(s2["kuerzel"]);
        var o20 = s2["groessen"]!.AsArray().Single(g => g!["typ"]!.GetValue<string>() == "DN 20")!;
        Assert.Equal("NW 20", o20["beschriftung"]!.GetValue<string>());
        Assert.Equal("25", o20["beschriftungAbwasser"]!.GetValue<string>());
    }

    [Fact]
    public void Rundreise_GespeicherteDateiTraegtDasKuerzel_UndLaedtUnveraendert()
    {
        var p = MitKuerzel("XY");
        var ordner = Directory.CreateTempSubdirectory("hlks_kuerzel_").FullName;
        try
        {
            var datei = Path.Combine(ordner, "testrohr.familie.json");
            Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
            Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));
            var gespeichert = (JsonObject)JsonNode.Parse(File.ReadAllText(datei))!;
            Assert.Equal("XY", gespeichert["leitung"]!["kuerzel"]!.GetValue<string>());
            // laden: gleiche Familie, keine Änderung
            Assert.Empty(Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei })).Aenderungen);
            // in ein neues Projekt laden: das Kürzel wirkt dort
            var q = Neu();
            Ok(q.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
            Ok(q.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
            Assert.Equal("XY", Serie(q).Block.Kuerzel);
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public void Rundreise_FamilieLadenAusDateiMitKuerzel()
    {
        var ordner = Directory.CreateTempSubdirectory("hlks_kuerzel_").FullName;
        try
        {
            var datei = Path.Combine(ordner, "rohr_xy.familie.json");
            File.WriteAllText(datei, RohrDef("PE-S").ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            var p = Neu();
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } }));
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
            Assert.Equal("PE-S", Serie(p).Block.Kuerzel);
            Ok(Rohr(p, "RO-1", "DN 40", [0, 0, 500], [3000, 0, 500]));
            Assert.Equal("PE-S NW 40", Text(p, "RO-1"));   // Trinkwasser: NW (B9b); FIKTIV: da = 40 + 5
        }
        finally { Directory.Delete(ordner, true); }
    }
}
