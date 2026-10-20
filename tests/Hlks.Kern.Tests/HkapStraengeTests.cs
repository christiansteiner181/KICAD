// HKAP-Datenart «straenge» (Modul 3, Teil B5) gegen die Attrappe (HkapAttrappe, Verhalten wie AustauschService des Hauptlaufs):
// Abbildung (Medium aus dem Systemtyp, Block kicad mit Geschossen, Endgeräten und Leitungslängen), Bündeln mehrerer KICAD-Stränge zu
// einem HKAP-Strang, Senden/Übernehmen/Konflikte, nicht platzierte Stränge aus HKAP, hkap_strang_zuordnen, Trockenlauf, Rückgängig.
// Alle Werte FIKTIV (Testrohrserie da = DN + 5, Längen von Hand gerechnet). Nie die echte HKAP.

using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

public sealed class HkapStraengeTests : IDisposable
{
    readonly HkapAttrappe a = new();
    public void Dispose() => a.Dispose();

    const string Serie = "Testrohrserie (fiktiv)";

    // ---------------------------------------------------------------- Hilfen

    static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    static BefehlsErgebnis Gut(Projekt p, string befehl, string json = "{}", bool trockenlauf = false)
    {
        var e = p.Ausfuehren(befehl, J(json), trockenlauf);
        Assert.True(e.Angenommen, $"{befehl}: {e.Meldung} {e.Fehler} {e.Vorschlag} | {string.Join(" | ", e.Hinweise)}");
        return e;
    }

    static BefehlsErgebnis Nein(Projekt p, string befehl, string json = "{}")
    {
        var e = p.Ausfuehren(befehl, J(json));
        Assert.False(e.Angenommen, $"{befehl} hätte abgelehnt werden sollen: {e.Meldung} | {string.Join(" | ", e.Hinweise)}");
        return e;
    }

    static void Sys(Projekt p, string kz, string typ) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = kz, ["eigenschaften"] = new JsonObject { ["systemtyp"] = typ } }));

    /// <summary>Strang mit optionaler Steigzone (von Hand; seit 19.10.2026 Vorgabe der HKAP-Bündelung, Entscheid Christian).</summary>
    static void Strang(Projekt p, string kz, string system, string? name, string? steigzone = null)
    {
        var eig = new JsonObject { ["art"] = "steigstrang" };
        if (steigzone is not null) eig["steigzone"] = steigzone;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = kz, ["name"] = name, ["teilVon"] = system, ["eigenschaften"] = eig }));
    }

    /// <summary>Rohr der Testserie (Typ = Grösse) im Strang, Geschoss und System des Strangs.</summary>
    static void RohrSt(Projekt p, string kz, string typ, string strang, string geschoss, params double[][] punkte) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["teilVon"] = strang, ["geschoss"] = geschoss,
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        }));

    /// <summary>Grundprojekt: Gebäude GEB, Geschosse EG (Kote 0) und 1.OG (Kote 3000), Testrohrserie (FAM-1), System KW.
    /// Heizung: HZ-VL/HZ-RL mit den Strängen HZ-VL-1/HZ-RL-1 (gleicher Name); Leitungen von Hand gerechnet:
    /// HZ-VL-1: DN 20 EG 3.000 + 1.450 m, DN 25 1.000 m; HZ-RL-1: DN 20 2.000 m.</summary>
    static Projekt Heizung()
    {
        var p = MitSerie(formteile: false);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "1.OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 } }));
        Sys(p, "HZ-VL", "Heizung Vorlauf");
        Sys(p, "HZ-RL", "Heizung Rücklauf");
        Strang(p, "HZ-VL-1", "HZ-VL", "Heizung Steigstrang 1", "ST 1");
        Strang(p, "HZ-RL-1", "HZ-RL", "Heizung Steigstrang 1", "ST 1");
        RohrSt(p, "RO-V1", "DN 20", "HZ-VL-1", "EG", [0, 0, 500], [3000, 0, 500]);
        RohrSt(p, "RO-V2", "DN 20", "HZ-VL-1", "EG", [3000, 0, 500], [3000, 1450, 500]);
        RohrSt(p, "RO-V3", "DN 25", "HZ-VL-1", "EG", [0, 500, 500], [0, 1500, 500]);
        RohrSt(p, "RO-R1", "DN 20", "HZ-RL-1", "EG", [0, 100, 400], [2000, 100, 400]);
        return p;
    }

    void Verknuepfen(Projekt p) => Gut(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""");

    /// <summary>Verknüpft und ordnet die Geschosse EG/1.OG den HKAP-Etagen E1/E2 zu.</summary>
    Projekt Bereit(Projekt? p = null)
    {
        p ??= Heizung();
        Verknuepfen(p);
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["geschosse"] }""");
        Assert.Equal("PJ-2026-003/E1", Kennung(p, "EG"));
        Assert.Equal("PJ-2026-003/E2", Kennung(p, "1.OG"));
        return p;
    }

    static string Kennung(Projekt p, string kz) => p.Modell.Hole(kz).Kennungen?.GetValueOrDefault("hkap") ?? "";
    static JsonObject Abgleich(Projekt p, string json = "{}") => (JsonObject)Tuer.Abfrage(p, "hkap_abgleich", J(json));
    static JsonObject Art(JsonObject abgleich, string name) => abgleich["datenarten"]!.AsArray().Single(d => d!["name"]!.GetValue<string>() == name)!.AsObject();
    static JsonObject Straenge(Projekt p, string json = """{ "datenarten": ["straenge"] }""") => Art(Abgleich(p, json), "straenge");
    static IEnumerable<JsonObject> Eintraege(JsonObject art, string richtung, string was) => art[richtung]![was]!.AsArray().Select(x => x!.AsObject());
    static IEnumerable<string> Felder(JsonObject art, string richtung, string hkapId) =>
        Eintraege(art, richtung, "geaendert").Where(x => x["hkapId"]?.GetValue<string>() == hkapId).SelectMany(x => x["felder"]!.AsArray()).Select(f => f!["feld"]!.GetValue<string>());

    HkapAttrappe.Satz Strang1 => a.Straenge.Single();
    static JsonArray Block(HkapAttrappe.Satz s) => (JsonArray)s.Felder["kicad"]!["straenge"]!;
    static JsonObject Eintrag(JsonArray block, string kennzeichen) => block.Single(x => x!["kennzeichen"]!.GetValue<string>() == kennzeichen)!.AsObject();
    static string[] Kennzeichen(JsonArray block) => block.Select(x => x!["kennzeichen"]!.GetValue<string>()).ToArray();
    static string[] Texte(JsonNode? n) => ((JsonArray)n!).Select(x => x!.GetValue<string>()).ToArray();

    void HkapStrang(string id, string nummer, string bezeichnung, string medium) =>
        a.Straenge.Add(new HkapAttrappe.Satz
        {
            Id = id,
            Felder = J($$"""{ "id": "{{id}}", "nummer": "{{nummer}}", "bezeichnung": "{{bezeichnung}}", "medium": "{{medium}}", "tempVL": 70, "tempRL": 50, "anschluss": "Technik", "anschlussEtageId": "E1", "etagen": ["E1", "E2"], "kicad": null }"""),
        });

    // ---------------------------------------------------------------- Medium aus dem Systemtyp

    [Fact]
    public void Medium_AusDemSystemtyp_NachTabelle()
    {
        var erwartet = new Dictionary<string, string?>
        {
            ["Trinkwasser kalt"] = "Sanitär", ["Trinkwasser warm"] = "Sanitär", ["Zirkulation"] = "Sanitär", ["Schmutzwasser"] = "Sanitär", ["Regenwasser"] = "Sanitär",
            ["Heizung Vorlauf"] = "Heizung", ["Heizung Rücklauf"] = "Heizung", ["Kälte Vorlauf"] = "Kälte", ["Kälte Rücklauf"] = "Kälte",
            ["Zuluft"] = "Zuluft", ["Abluft"] = "Abluft", ["Fortluft"] = "Fortluft", ["Aussenluft"] = "Aussenluft",
            ["Heizung Vorlauf Glykol"] = null, ["Heizung Rücklauf Glykol"] = null, ["Kälte Vorlauf Glykol"] = "Kälte", ["Kälte Rücklauf Glykol"] = "Kälte",   // Heizung Glykol (früher Sole): kein HKAP-Medium
        };
        Assert.Equal(Kat.Systemtypen.Keys.Order(), erwartet.Keys.Order());              // jeder Systemtyp des Katalogs ist entschieden
        foreach (var (typ, medium) in erwartet) Assert.Equal(medium, HkapStraenge.MediumVon(typ));
        Assert.All(erwartet.Values.Where(v => v is not null), v => Assert.Contains(v!, HkapStraenge.HkapMedien));   // nur Medien, die HKAP kennt
    }

    // ---------------------------------------------------------------- Senden: Bündeln, Block kicad

    [Fact]
    public void Senden_HeizungVorlaufUndRuecklauf_EinHkapStrangMitZweiKicadStraengen()
    {
        var p = Bereit();
        var tl = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""", trockenlauf: true);
        Assert.Contains(tl.Hinweise, h => h.Contains("Trockenlauf") && h.Contains("1 neu"));
        Assert.Empty(a.Straenge);                                           // Trockenlauf: nichts geschrieben
        Assert.Equal("", Kennung(p, "HZ-VL-1"));

        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Contains(e.Hinweise, h => h.Contains("1 neu"));
        var s = Strang1;
        Assert.Equal(("ST1", "Heizung", "H_01", "Heizung Steigstrang 1"),
            (s.Id, s.Felder["medium"]!.GetValue<string>(), s.Felder["nummer"]!.GetValue<string>(), s.Felder["bezeichnung"]!.GetValue<string>()));   // Nummer nach Gewerk H_
        var block = Block(s);
        Assert.Equal(["HZ-RL-1", "HZ-VL-1"], Kennzeichen(block));
        var vl = Eintrag(block, "HZ-VL-1");
        Assert.Equal(p.Modell.Hole("HZ-VL-1").Id, vl["id"]!.GetValue<string>());
        Assert.Equal(("HZ-VL", "Heizung Vorlauf", "Heizung Steigstrang 1"), (vl["system"]!.GetValue<string>(), vl["systemtyp"]!.GetValue<string>(), vl["name"]!.GetValue<string>()));
        Assert.Equal("Heizung Rücklauf", Eintrag(block, "HZ-RL-1")["systemtyp"]!.GetValue<string>());
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "HZ-VL-1"));              // beide Stränge tragen dieselbe HKAP-Id
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "HZ-RL-1"));
    }

    [Fact]
    public void Senden_SanitaerKaltWarmSchmutzwasser_EinStrang()
    {
        var p = MitSerie(formteile: false);
        Sys(p, "WW", "Trinkwasser warm"); Sys(p, "SW", "Schmutzwasser");
        Strang(p, "KW-1", "KW", "Sanitär Strang 1", "PW 1"); Strang(p, "WW-1", "WW", "Sanitär Strang 1", "PW 1"); Strang(p, "SW-1", "SW", "Sanitär Strang 1", "PW 1");
        RohrSt(p, "RO-K1", "DN 25", "KW-1", "EG", [0, 0, 0], [0, 0, 2500]);
        Bereit(p);
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var s = Strang1;
        Assert.Equal(("Sanitär", "S_01"), (s.Felder["medium"]!.GetValue<string>(), s.Felder["nummer"]!.GetValue<string>()));
        var block = Block(s);
        Assert.Equal(["KW-1", "SW-1", "WW-1"], Kennzeichen(block));
        Assert.Equal(["Trinkwasser kalt", "Schmutzwasser", "Trinkwasser warm"], block.Select(x => x!["systemtyp"]!.GetValue<string>()).ToArray());
        Assert.Equal(2.5, Eintrag(block, "KW-1")["laengen"]![0]!["laengeM"]!.GetValue<double>(), 9);     // senkrecht 0 → 2500 mm
        Assert.All(new[] { "KW-1", "WW-1", "SW-1" }, kz => Assert.Equal("PJ-2026-003/ST1", Kennung(p, kz)));
    }

    [Fact]
    public void Laengen_JeSerieUndGroesse_HandrechnungMitDerTestserie()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var block = Block(Strang1);
        // HZ-VL-1: DN 20 = 3000 + 1450 mm (zwei Geraden) = 4.45 m, DN 25 = 1000 mm = 1 m; HZ-RL-1: DN 20 = 2000 mm = 2 m
        // B9b (Entscheid Christian 19.10.2026): Grösse der Rohre ausser Abwasser «NW 20» (Vorgabe) statt Typname «DN 20»
        Assert.Equal([(Serie, "NW 20", 4.45), (Serie, "NW 25", 1.0)],
            ((JsonArray)Eintrag(block, "HZ-VL-1")["laengen"]!).Select(l => (l!["serie"]!.GetValue<string>(), l["groesse"]!.GetValue<string>(), l["laengeM"]!.GetValue<double>())).ToArray());
        Assert.Equal([(Serie, "NW 20", 2.0)],
            ((JsonArray)Eintrag(block, "HZ-RL-1")["laengen"]!).Select(l => (l!["serie"]!.GetValue<string>(), l["groesse"]!.GetValue<string>(), l["laengeM"]!.GetValue<double>())).ToArray());
        // dieselben Zahlen wie die Ableitung des Kerns
        var ab = Ableitungen.Leitungslaengen(p, strangId: "HZ-VL-1");
        Assert.Equal([("NW 20", 4.45), ("NW 25", 1.0)], ab.Select(l => (l.Groesse, l.LaengeM)).ToArray());
        // groesse geht als Text (HKAP liest ProjKicadLaenge.Groesse als string) – sonst ignorierte HKAP den ganzen Block
        var gesendet = a.Posts("straenge").Single().Body!["datensaetze"]![0]!["felder"]!["kicad"]!["straenge"]!.AsArray();
        Assert.All(gesendet.SelectMany(x => x!["laengen"]!.AsArray()), l => Assert.Equal(System.Text.Json.JsonValueKind.String, l!["groesse"]!.GetValueKind()));
    }

    [Fact]
    public void Endgeraete_WerdenImNetzDesStrangsGezaehlt()
    {
        var p = Heizung();
        foreach (var (kz, x) in new[] { ("HK-1", 3000.0), ("HK-2", 0.0) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Heizkoerper", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([x, x == 0 ? 1500 : 1450, 500]) },
            }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-V2.ende", ["nach"] = "HK-1.vl" }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-V3.ende", ["nach"] = "HK-2.vl" }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "HK-1.rl", ["nach"] = "RO-R1.anfang" }));
        Bereit(p);
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var block = Block(Strang1);
        Assert.Equal(2, Eintrag(block, "HZ-VL-1")["endgeraete"]!.GetValue<int>());     // HK-1 und HK-2 am Vorlauf
        Assert.Equal(1, Eintrag(block, "HZ-RL-1")["endgeraete"]!.GetValue<int>());     // nur HK-1 am Rücklauf
        // wie die Strangliste zählt (direkt angeschlossen)
        var zeilen = Ableitungen.Strangliste(p)["zeilen"]!.AsArray();
        Assert.Equal(2, zeilen.Single(z => z!["strang"]!.GetValue<string>() == "HZ-VL-1")!["anzahlDirekt"]!.GetValue<int>());
    }

    [Fact]
    public void Geschosse_AlsHkapEtagenIds_VonObenNachUnten_OhneZuordnungMitHinweis()
    {
        var p = Heizung();
        RohrSt(p, "RO-V4", "DN 20", "HZ-VL-1", "1.OG", [0, 0, 3500], [2000, 0, 3500]);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "DG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 6000, ["hoehe"] = 3000 } }));
        RohrSt(p, "RO-V5", "DN 20", "HZ-VL-1", "DG", [0, 0, 6500], [1000, 0, 6500]);
        Bereit(p);
        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var block = Block(Strang1);
        Assert.Equal(["E2", "E1"], Texte(Eintrag(block, "HZ-VL-1")["geschosse"]));   // 1.OG (E2) vor EG (E1); DG hat keine HKAP-Etage
        Assert.Equal(["E1"], Texte(Eintrag(block, "HZ-RL-1")["geschosse"]));
        Assert.Contains(e.Hinweise, h => h.Contains("HZ-VL-1") && h.Contains("DG") && h.Contains("ohne HKAP-Zuordnung"));
    }

    [Fact]
    public void Senden_ZweitesMalOhneAenderung_Unveraendert_UndAenderungWirdGesendet()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        int posts = a.Posts("straenge").Count;
        var version = Strang1.Version;
        var vor = Straenge(p);
        Assert.Equal(1, vor["unveraendert"]!.GetValue<int>());
        Assert.Empty(Eintraege(vor, "nachHkap", "geaendert"));
        Assert.Empty(Eintraege(vor, "nachKicad", "geaendert"));
        Assert.Empty(vor["konflikte"]!.AsArray());
        var zweites = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Contains(zweites.Hinweise, h => h.Contains("Nichts zu senden"));
        Assert.Equal(posts, a.Posts("straenge").Count);                      // kein zweiter Aufruf an HKAP
        Assert.Equal(version, Strang1.Version);

        // neue Leitung → nur der Block ändert sich, wird gesendet; HKAP ersetzt ihn ganz, danach wieder gleich
        RohrSt(p, "RO-V6", "DN 25", "HZ-VL-1", "EG", [0, 2000, 500], [500, 2000, 500]);
        Assert.Equal(["kicad.straenge"], Felder(Straenge(p), "nachHkap", "ST1").ToArray());
        var drittes = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Contains(drittes.Hinweise, h => h.Contains("1 geändert"));
        Assert.Equal(1.5, Eintrag(Block(Strang1), "HZ-VL-1")["laengen"]!.AsArray().Single(l => l!["groesse"]!.GetValue<string>() == "NW 25")!["laengeM"]!.GetValue<double>(), 9);
        Assert.Empty(Eintraege(Straenge(p), "nachHkap", "geaendert"));
    }

    [Fact]
    public void NurLesenFelderInHkap_ErzeugenKeinSenden_AberDieAnzeige()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var version = Strang1.Version;
        a.Aendern($"{HkapAttrappe.Pj}|straenge", "ST1", f => f["tempVL"] = 70);   // HKAP-Seite ändert ein nur-lesen-Feld
        var s = Straenge(p);
        Assert.Empty(Eintraege(s, "nachHkap", "geaendert"));
        Assert.Equal(1, s["nurLesen"]!.AsArray().Count);
        Assert.NotEqual(version, Strang1.Version);
        Assert.Contains("tempVL", s["nurLesen"]![0]!["werte"]!.AsObject().Select(x => x.Key));
    }

    // ---------------------------------------------------------------- Bündeln

    [Fact]
    public void Buendeln_NachNameKennzeichenNummerUndNein()
    {
        var p = MitSerie(formteile: false);
        Sys(p, "HZ-VL", "Heizung Vorlauf"); Sys(p, "HZ-RL", "Heizung Rücklauf"); Sys(p, "KAE-VL", "Kälte Vorlauf");
        Strang(p, "HZ-VL-1", "HZ-VL", "Steigstrang A"); Strang(p, "HZ-RL-1", "HZ-RL", "Steigstrang B");     // verschiedene Namen, gleiche Nummer
        Strang(p, "HZ-VL-2", "HZ-VL", "Steigstrang 2"); Strang(p, "HZ-RL-2", "HZ-RL", "Steigstrang 2");     // gleicher Name
        Strang(p, "KAE-VL-1", "KAE-VL", "Steigstrang A");                                                    // anderes Medium: bündelt nie mit Heizung
        var alle = p.Modell.DerKlasse("Strang").ToList();
        static string[][] Namen(List<List<Element>> g) => g.Select(x => x.Select(e => e.Kennzeichen).ToArray()).ToArray();

        Assert.Equal([["HZ-RL-1", "HZ-VL-1"], ["HZ-RL-2", "HZ-VL-2"], ["KAE-VL-1"]], Namen(HkapStraenge.Buendeln(p.Modell, alle, "auto")));
        Assert.Equal([["HZ-RL-1"], ["HZ-RL-2", "HZ-VL-2"], ["HZ-VL-1"], ["KAE-VL-1"]], Namen(HkapStraenge.Buendeln(p.Modell, alle, "name")));   // nur gleiche Namen, nach Kennzeichen sortiert
        Assert.Equal([["HZ-RL-1", "HZ-VL-1"], ["HZ-RL-2", "HZ-VL-2"], ["KAE-VL-1"]], Namen(HkapStraenge.Buendeln(p.Modell, alle, "kennzeichen")));
        Assert.Equal(5, HkapStraenge.Buendeln(p.Modell, alle, "nein").Count);
        // steigzone (Vorgabe seit 19.10.2026): ohne Steigzone je Strang eine Gruppe; gleiche Steigzone im selben Medium bündelt,
        // auch bei verschiedenen Namen und Nummern; ein anderes Medium bündelt nie mit
        Assert.Equal(5, HkapStraenge.Buendeln(p.Modell, alle, "steigzone").Count);
        Assert.Equal("steigzone", HkapStraenge.BuendelnPruefen(null));
        foreach (var kz in new[] { "HZ-VL-1", "HZ-RL-2", "KAE-VL-1" })
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["eigenschaften"] = new JsonObject { ["steigzone"] = "Schacht Nord" } }));
        Assert.Equal([["HZ-RL-1"], ["HZ-RL-2", "HZ-VL-1"], ["HZ-VL-2"], ["KAE-VL-1"]], Namen(HkapStraenge.Buendeln(p.Modell, p.Modell.DerKlasse("Strang").ToList(), "steigzone")));
        // höchstens ein Strang je Systemtyp in einer Gruppe: ein zweiter Vorlauf mit derselben Nummer beginnt eine neue Gruppe
        Sys(p, "HZ-VL-B", "Heizung Vorlauf"); Strang(p, "HB-VL-1", "HZ-VL-B", "Steigstrang A");
        var g = HkapStraenge.Buendeln(p.Modell, p.Modell.DerKlasse("Strang").ToList(), "kennzeichen");
        Assert.DoesNotContain(g, x => x.Count(e => HkapStraenge.SystemtypVon(p.Modell, e) == "Heizung Vorlauf") > 1);
        Assert.Throws<HlksFehler>(() => HkapStraenge.BuendelnPruefen("alles"));
    }

    [Fact]
    public void Buendeln_ParameterSteuertDenVorschlag_UndGiltFuerAbgleichUndSenden()
    {
        var p = Heizung();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HZ-RL-1", ["name"] = "Rücklauf Steigstrang 1" }));   // verschiedene Namen
        Bereit(p);
        var auto = Straenge(p);
        Assert.Equal(1, Eintraege(auto, "nachHkap", "neu").Count());                                   // Nummer 1 + gleiches Medium → ein Strang
        Assert.Equal("HZ-RL-1, HZ-VL-1", Eintraege(auto, "nachHkap", "neu").Single()["element"]!.GetValue<string>());
        Assert.Equal(2, Eintraege(Straenge(p, """{ "datenarten": ["straenge"], "buendeln": "name" }"""), "nachHkap", "neu").Count());
        Assert.Equal(2, Eintraege(Straenge(p, """{ "datenarten": ["straenge"], "buendeln": "nein" }"""), "nachHkap", "neu").Count());
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"], "buendeln": "nein" }""");
        Assert.Equal(2, a.Straenge.Count);                                                             // je KICAD-Strang ein HKAP-Strang
        Assert.Equal(["H_01", "H_02"], a.Straenge.Select(s => s.Felder["nummer"]!.GetValue<string>()).ToArray());
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "HZ-RL-1"));
        Assert.Equal("PJ-2026-003/ST2", Kennung(p, "HZ-VL-1"));
        var e = Nein(p, "hkap_senden", """{ "buendeln": "alles" }""");
        Assert.Contains("auto", e.Vorschlag);
    }

    // ---------------------------------------------------------------- Medium

    [Fact]
    public void Medium_GemischteGruppeUndSole_ohneMediumMitHinweis_UndHkapMediumBleibt()
    {
        var p = MitSerie(formteile: false);
        Sys(p, "HZ-VL", "Heizung Vorlauf"); Sys(p, "KAE-VL", "Kälte Vorlauf"); Sys(p, "HZ-VL-Glyk", "Heizung Vorlauf Glykol");
        Strang(p, "HZ-VL-1", "HZ-VL", "Mischstrang"); Strang(p, "KAE-VL-1", "KAE-VL", "Mischstrang"); Strang(p, "HZ-VL-Glyk-1", "HZ-VL-Glyk", "Sole 1");
        Bereit(p);
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(3, a.Straenge.Count);                                       // gleicher Name, aber verschiedene Medien: nie in einer Gruppe
        var sole = a.Straenge.Single(s => s.Felder["bezeichnung"]!.GetValue<string>() == "Sole 1");
        Assert.Equal("", sole.Felder["medium"]!.GetValue<string>());            // Sole: kein Medium
        Assert.StartsWith("L_", sole.Felder["nummer"]!.GetValue<string>());     // ohne Medium: HKAP nummeriert wie Lüftung
        Assert.Contains(Straenge(p)["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Heizung Vorlauf Glykol") && h.GetValue<string>().Contains("kein HKAP-Medium"));

        // Kühlwasser und ein Heizungsstrang in einer HKAP-Gruppe: verschiedene Medien → Medium nicht schreiben, Hinweis
        var kue = a.Straenge.Single(s => s.Felder["medium"]!.GetValue<string>() == "Kälte");
        Sys(p, "HZ-RL", "Heizung Rücklauf"); Strang(p, "HZ-RL-9", "HZ-RL", "Rest");
        Gut(p, "hkap_strang_zuordnen", $$"""{ "strang": "HZ-RL-9", "hkap": "{{kue.Id}}" }""");
        var ab = Straenge(p);
        Assert.Contains(ab["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("verschiedenen HKAP-Medien") && h.GetValue<string>().Contains("Heizung") && h.GetValue<string>().Contains("Kälte"));
        Assert.DoesNotContain("medium", Felder(ab, "nachHkap", kue.Id));       // Medium bleibt, wie HKAP es hat (kein Überschreiben mit leer)
        Assert.Equal("Kälte", kue.Felder["medium"]!.GetValue<string>());
    }

    [Fact]
    public void Medium_EinWertDenHkapSchonHat_WirdNieUeberschrieben()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        a.Aendern($"{HkapAttrappe.Pj}|straenge", "ST1", f => f["medium"] = "Kälte");      // HKAP setzt von Hand ein anderes Medium
        var ab = Straenge(p);
        Assert.DoesNotContain("medium", Felder(ab, "nachHkap", "ST1"));
        Assert.Contains(ab["abweichend"]!.AsArray(), x => x!["feld"]!.GetValue<string>() == "medium" && x["kicad"]!.GetValue<string>() == "Heizung" && x["hkap"]!.GetValue<string>() == "Kälte");
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("Kälte", Strang1.Felder["medium"]!.GetValue<string>());
    }

    // ---------------------------------------------------------------- HKAP ändert, Konflikte

    [Fact]
    public void HkapAendertBezeichnung_AbgleichZeigtEs_UebernehmenSetztDenNamenAllerStraenge()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        a.Aendern($"{HkapAttrappe.Pj}|straenge", "ST1", f => f["bezeichnung"] = "Heizung Nord");
        var ab = Straenge(p);
        Assert.Equal(["bezeichnung"], Felder(ab, "nachKicad", "ST1").ToArray());
        var f = Eintraege(ab, "nachKicad", "geaendert").Single()["felder"]![0]!;
        Assert.Equal(("Heizung Steigstrang 1", "Heizung Nord"), (f["kicad"]!.GetValue<string>(), f["hkap"]!.GetValue<string>()));
        Assert.Empty(ab["konflikte"]!.AsArray());

        var tl = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""", trockenlauf: true);
        Assert.Equal("Heizung Steigstrang 1", p.Modell.Hole("HZ-VL-1").Name);          // Trockenlauf ändert nichts
        var e = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("Heizung Nord", p.Modell.Hole("HZ-VL-1").Name);
        Assert.Equal("Heizung Nord", p.Modell.Hole("HZ-RL-1").Name);
        Assert.Contains(e.Hinweise, h => h.Contains("1 geändert"));
        Assert.Empty(Eintraege(Straenge(p), "nachKicad", "geaendert"));                 // Basis nachgeführt
        // der neue Name steht im Block, der nächste Abgleich schlägt ihn zum Senden vor (HKAP ersetzt den Block ganz)
        Assert.Equal(["kicad.straenge"], Felder(Straenge(p), "nachHkap", "ST1").ToArray());
    }

    [Fact]
    public void BeideGeaendert_Konflikt_NurMitAusdruecklicherWahl()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        a.Aendern($"{HkapAttrappe.Pj}|straenge", "ST1", f => f["bezeichnung"] = "Heizung Nord");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HZ-VL-1", ["name"] = "Heizung Süd" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HZ-RL-1", ["name"] = "Heizung Süd" }));
        var ab = Abgleich(p, """{ "datenarten": ["straenge"] }""");
        var k = ab["konflikte"]!.AsArray().Single()!;
        Assert.Equal(("straenge/ST1/bezeichnung", "Heizung Süd", "Heizung Nord", "Heizung Steigstrang 1"),
            (k["schluessel"]!.GetValue<string>(), k["kicad"]!.GetValue<string>(), k["hkap"]!.GetValue<string>(), k["basis"]!.GetValue<string>()));

        var ohne = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("Heizung Süd", p.Modell.Hole("HZ-VL-1").Name);                      // ohne Wahl bleibt der Konflikt offen
        Assert.Contains("1 offen", ohne.Hinweise[0]);
        Assert.Single(Abgleich(p, """{ "datenarten": ["straenge"] }""")["konflikte"]!.AsArray());

        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"], "konflikte": { "straenge/ST1/bezeichnung": "kicad" } }""");
        Assert.Equal("Heizung Süd", p.Modell.Hole("HZ-VL-1").Name);
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("Heizung Süd", Strang1.Felder["bezeichnung"]!.GetValue<string>());   // KICAD-Fassung gewählt → HKAP bekommt sie

        a.Aendern($"{HkapAttrappe.Pj}|straenge", "ST1", f => f["bezeichnung"] = "Heizung Ost");
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HZ-VL-1", ["name"] = "Heizung West" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HZ-RL-1", ["name"] = "Heizung West" }));
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"], "konflikte": { "*": "hkap" } }""");
        Assert.Equal(("Heizung Ost", "Heizung Ost"), (p.Modell.Hole("HZ-VL-1").Name, p.Modell.Hole("HZ-RL-1").Name));
    }

    [Fact]
    public void Senden_HkapHatInzwischenGeaendert_KonfliktNichtsUeberschrieben()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        RohrSt(p, "RO-V6", "DN 25", "HZ-VL-1", "EG", [0, 2000, 500], [500, 2000, 500]);
        a.VorPost = (att, art) => { if (art == "straenge") att.Straenge[0].Felder["tempVL"] = 65; };   // jemand ändert in HKAP, während KICAD sendet
        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        a.VorPost = null;
        Assert.Contains(e.Hinweise, h => h.StartsWith("KONFLIKT") && h.Contains("HZ-RL-1, HZ-VL-1"));
        Assert.Equal(1.0, Eintrag(Block(Strang1), "HZ-VL-1")["laengen"]!.AsArray().Single(l => l!["groesse"]!.GetValue<string>() == "NW 25")!["laengeM"]!.GetValue<double>(), 9);   // alter Stand
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");                                   // neue Version gelesen → geht durch
        Assert.Equal(1.5, Eintrag(Block(Strang1), "HZ-VL-1")["laengen"]!.AsArray().Single(l => l!["groesse"]!.GetValue<string>() == "NW 25")!["laengeM"]!.GetValue<double>(), 9);
    }

    // ---------------------------------------------------------------- Neuer HKAP-Strang → nicht platzierter Strang

    [Fact]
    public void NeuerHkapStrang_EindeutigesSystem_WirdNichtPlatzierterStrang()
    {
        var p = Bereit(MitSerie(formteile: false));
        Sys(p, "ZUL", "Zuluft");
        HkapStrang("ST1", "L_01", "Zuluft Nord", "Zuluft");
        var vor = Straenge(p);
        var neu = Eintraege(vor, "nachKicad", "neu").Single();
        Assert.Equal("ST1", neu["hkapId"]!.GetValue<string>());
        Assert.Contains("nicht platzierter Strang im System ZUL", neu["hinweis"]!.GetValue<string>());

        var tl = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""", trockenlauf: true);
        Assert.Empty(p.Modell.DerKlasse("Strang"));                                       // Trockenlauf ändert nichts
        var e = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        var s = p.Modell.Hole("L_01");                                                    // Kennzeichen aus nummer
        Assert.Equal(("Strang", "Zuluft Nord", p.Modell.Hole("ZUL").Id), (s.Klasse, s.Name, s.TeilVon));
        Assert.Equal(s.TeilVon, s.System);
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "L_01"));
        Assert.Empty(p.Modell.Elemente.Where(x => x.TeilVon == s.Id));                    // ohne Leitungen = nicht platziert
        Assert.Contains(e.Hinweise, h => h.Contains("L_01") && h.Contains("nicht platziert") && h.Contains("ZUL"));
        Assert.Equal(["L_01"], Straenge(p)["nichtPlatziert"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        // beim nächsten Abgleich gepaart: Name aus bezeichnung gleich, nichts zu übernehmen; der leere Block geht als KICAD-Stand an HKAP
        var danach = Straenge(p);
        Assert.Empty(Eintraege(danach, "nachKicad", "neu"));
        Assert.Empty(Eintraege(danach, "nachKicad", "geaendert"));
        Assert.Empty(danach["konflikte"]!.AsArray());
    }

    [Fact]
    public void NeuerHkapStrang_KennzeichenWirdEindeutig()
    {
        var p = Bereit(MitSerie(formteile: false));
        Sys(p, "ZUL", "Zuluft");
        Strang(p, "L_01", "ZUL", "anderer Strang");                                       // Kennzeichen schon vergeben
        HkapStrang("ST1", "L_01", "Zuluft Nord", "Zuluft");
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "L_01-2"));
        Assert.Equal("anderer Strang", p.Modell.Hole("L_01").Name);
    }

    [Fact]
    public void NeuerHkapStrang_MehrereOderKeinPassendesSystem_VerlangtWahl()
    {
        var p = Bereit(MitSerie(formteile: false));
        Sys(p, "HZ-VL", "Heizung Vorlauf"); Sys(p, "HZ-RL", "Heizung Rücklauf");
        HkapStrang("ST1", "H_01", "Heizung Haus", "Heizung");
        HkapStrang("ST2", "K_01", "Kühlung Haus", "Kälte");
        var vor = Straenge(p);
        var hinweise = Eintraege(vor, "nachKicad", "neu").ToDictionary(x => x["hkapId"]!.GetValue<string>(), x => x["hinweis"]!.GetValue<string>());
        Assert.Contains("mehrere passende Systeme (HZ-RL, HZ-VL)", hinweise["ST1"]);
        Assert.Contains("kein passendes System", hinweise["ST2"]);
        var wahl = vor["systemWahl"]!.AsArray().Select(x => x!.AsObject()).ToList();
        Assert.Equal(["ST1", "ST2"], wahl.Select(w => w["hkapId"]!.GetValue<string>()).ToArray());
        Assert.Equal(["HZ-RL", "HZ-VL"], Texte(wahl[0]["kandidaten"]));
        Assert.Empty(Texte(wahl[1]["kandidaten"]));
        Assert.Equal(3, vor["systemWahl"]![0]!["systeme"]!.AsArray().Count);   // alle Systeme des Projekts (KW, HZ-RL, HZ-VL) zur freien Wahl

        var ohne = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.Empty(p.Modell.DerKlasse("Strang"));                                       // ohne Wahl nichts angelegt, mit Hinweis
        Assert.Contains(ohne.Hinweise, h => h.Contains("ST1") && h.Contains("mehrere passende Systeme") && h.Contains("system"));
        Assert.Contains(ohne.Hinweise, h => h.Contains("ST2") && h.Contains("kein passendes System"));
        Assert.Contains("2 Vorschläge", ohne.Hinweise[0]);

        // Wahl eines Systems: Kennzeichen = nummer; Wahl mehrerer: je System ein Strang mit derselben HKAP-Id
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"], "system": { "ST1": ["HZ-VL", "HZ-RL"] } }""");
        Assert.Equal(["H_01-HZ-RL", "H_01-HZ-VL"], p.Modell.DerKlasse("Strang").Select(s => s.Kennzeichen).Order().ToArray());
        Assert.All(p.Modell.DerKlasse("Strang"), s => Assert.Equal("PJ-2026-003/ST1", s.Kennungen!["hkap"]));
        Assert.Equal(p.Modell.Hole("HZ-VL").Id, p.Modell.Hole("H_01-HZ-VL").TeilVon);
        Assert.Throws<HlksFehler>(() => HkapStraenge.BuendelnPruefen("x"));
        var falsch = Nein(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"], "system": { "ST2": "gibt-es-nicht" } }""");
        Assert.Contains("gibt-es-nicht", falsch.Fehler);
        Sys(p, "KAE-VL", "Kälte Vorlauf");
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");                  // jetzt eindeutig: Kälte → KAE-VL
        Assert.Equal(p.Modell.Hole("KAE-VL").Id, p.Modell.Hole("K_01").TeilVon);
    }

    [Fact]
    public void NeuerHkapStrang_ZuordnenMitGleicherBezeichnung_BlockiertAnlegenUndSenden()
    {
        var p = Bereit();
        HkapStrang("ST1", "H_01", "Heizung Steigstrang 1", "Heizung");                    // gleiche Bezeichnung wie die KICAD-Stränge, noch nicht zugeordnet
        var ab = Straenge(p);
        var v = ab["zuordnungsvorschlaege"]!.AsArray().Single()!;
        Assert.Equal(("ST1", "HZ-RL-1, HZ-VL-1"), (v["hkapId"]!.GetValue<string>(), v["strang"]!.GetValue<string>()));
        Assert.Contains("nicht angelegt", Eintraege(ab, "nachKicad", "neu").Single()["hinweis"]!.GetValue<string>());
        Assert.Contains("nicht gesendet", Eintraege(ab, "nachHkap", "neu").Single()["hinweis"]!.GetValue<string>());
        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(1, a.Straenge.Count);                                                // kein zweiter HKAP-Strang
        Assert.Contains(e.Hinweise, h => h.Contains("hkap_strang_zuordnen"));
        var u = Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.Empty(p.Modell.DerKlasse("Strang").Where(s => s.Kennzeichen == "H_01"));
        Assert.Contains(u.Hinweise, h => h.Contains("hkap_strang_zuordnen"));
        Assert.Equal(2, Abgleich(p, """{ "datenarten": ["straenge"] }""")["zusammenfassung"]!["vorschlaege"]!.GetValue<int>());   // beide Seiten warten auf die Entscheidung

        Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-1, HZ-RL-1", "hkap": "ST1" }""");
        var danach = Straenge(p);
        Assert.Empty(Eintraege(danach, "nachKicad", "neu"));
        Assert.Empty(Eintraege(danach, "nachHkap", "neu"));
        Assert.Contains("kicad.straenge", Felder(danach, "nachHkap", "ST1"));                // die Gruppe ist gepaart; der Block geht zum Senden
    }

    [Fact]
    public void NeuerKicadStrangMitDerBezeichnungEinerBestehendenGruppe_WirdVorgeschlagenUndTrittMitZuordnenBei()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Sys(p, "HZ-VL2", "Heizung Vorlauf");
        Strang(p, "HZ-VL-2", "HZ-VL2", "Heizung Steigstrang 1");                         // gleicher Name, gehört wohl zu ST1
        var ab = Straenge(p);
        Assert.Equal(("ST1", "HZ-VL-2"), (ab["zuordnungsvorschlaege"]![0]!["hkapId"]!.GetValue<string>(), ab["zuordnungsvorschlaege"]![0]!["strang"]!.GetValue<string>()));
        Assert.Contains("nicht gesendet", Eintraege(ab, "nachHkap", "neu").Single()["hinweis"]!.GetValue<string>());
        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(1, a.Straenge.Count);
        Assert.Equal("", Kennung(p, "HZ-VL-2"));
        Assert.Contains(e.Hinweise, h => h.Contains("hkap_strang_zuordnen"));

        Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-2", "hkap": "ST1" }""");
        var danach = Straenge(p);
        Assert.Empty(danach["konflikte"]!.AsArray());                                      // bestehende Gruppe: Basis bleibt, kein Scheinkonflikt
        Assert.Equal(["kicad.straenge"], Felder(danach, "nachHkap", "ST1").ToArray());     // nur der Block wächst
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(["HZ-RL-1", "HZ-VL-1", "HZ-VL-2"], Kennzeichen(Block(Strang1)));
        Assert.Equal(1, a.Straenge.Count);
        Assert.Empty(Eintraege(Straenge(p), "nachHkap", "geaendert"));
    }

    // ---------------------------------------------------------------- hkap_strang_zuordnen

    [Fact]
    public void Zuordnen_MehrereKicadStraengeAufDieselbeHkapId_UndPruefungen()
    {
        var p = Bereit();
        HkapStrang("ST1", "H_01", "Heizung Haus", "Heizung");
        var tl = Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-1", "hkap": "ST1" }""", trockenlauf: true);
        Assert.Equal("", Kennung(p, "HZ-VL-1"));                                           // Trockenlauf ändert nichts
        Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-1", "hkap": "PJ-2026-003/ST1" }""");
        Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-RL-1", "hkap": "ST1" }""");          // zweiter Strang auf dieselbe Id
        Assert.Equal(("PJ-2026-003/ST1", "PJ-2026-003/ST1"), (Kennung(p, "HZ-VL-1"), Kennung(p, "HZ-RL-1")));
        Assert.Contains(Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-RL-1", "hkap": "ST1" }""").Hinweise, h => h.Contains("schon HKAP-Strang ST1"));   // erneut: kein Fehler

        var anders = Nein(p, "hkap_strang_zuordnen", """{ "strang": "HZ-RL-1", "hkap": "ST2" }""");
        Assert.Contains("schon HKAP-Strang ST1", anders.Fehler);
        var fremd = Nein(p, "hkap_strang_zuordnen", """{ "strang": "RO-V1", "hkap": "ST1" }""");           // kein Strang
        Assert.Contains("Strang", fremd.Fehler + fremd.Vorschlag);
        var pj = Nein(p, "hkap_strang_zuordnen", """{ "strang": "HZ-RL-1", "hkap": "PJ-2026-999/ST1" }""");
        Assert.Contains("PJ-2026-999", pj.Fehler);

        // Abweichung der Bezeichnung zeigt der Abgleich als Konflikt (Basis der KICAD-Felder vergessen), nichts wird still gesendet
        var ab = Abgleich(p, """{ "datenarten": ["straenge"] }""");
        Assert.Equal("straenge/ST1/bezeichnung", ab["konflikte"]![0]!["schluessel"]!.GetValue<string>());
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal("Heizung Haus", Strang1.Felder["bezeichnung"]!.GetValue<string>());
        Assert.Equal(["HZ-RL-1", "HZ-VL-1"], Kennzeichen(Block(Strang1)));                                 // der Block kommt trotzdem
    }

    [Fact]
    public void Zuordnen_UnbekannterHkapStrang_ZeigtDieStraengeInHkap()
    {
        var p = Bereit();
        HkapStrang("ST1", "H_01", "Heizung Haus", "Heizung");
        var e = Nein(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-1", "hkap": "ST9" }""");
        Assert.Contains("ST9", e.Fehler);
        Assert.Contains("ST1 H_01 Heizung Haus", e.Vorschlag);
        Assert.Equal("", Kennung(p, "HZ-VL-1"));
    }

    [Fact]
    public void Zuordnen_GezeichneterStrangLoestDenNichtPlatziertenPlatzhalterImSelbenSystemAuf()
    {
        var p = Bereit();
        HkapStrang("ST1", "H_01", "Heizung Haus", "Heizung");
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"], "system": { "ST1": ["HZ-VL", "HZ-RL"] } }""");
        Assert.Equal(["H_01-HZ-RL", "H_01-HZ-VL"], p.Modell.DerKlasse("Strang").Select(s => s.Kennzeichen).Where(k => k.StartsWith("H_01")).Order().ToArray());
        // HZ-VL-1 (gezeichnet) ersetzt den Platzhalter im System HZ-VL; der im System HZ-RL bleibt
        var e = Gut(p, "hkap_strang_zuordnen", """{ "strang": "HZ-VL-1", "hkap": "ST1" }""");
        Assert.Null(p.Modell.Finde("H_01-HZ-VL"));
        Assert.NotNull(p.Modell.Finde("H_01-HZ-RL"));
        Assert.Contains(e.Hinweise, h => h.Contains("H_01-HZ-VL") && h.Contains("HZ-VL-1") && h.Contains("auf"));
        Assert.Equal(["H_01-HZ-RL"], Straenge(p)["nichtPlatziert"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        // Rückgängig stellt Platzhalter und Zuordnung wieder her
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.NotNull(p.Modell.Finde("H_01-HZ-VL"));
        Assert.Equal("", Kennung(p, "HZ-VL-1"));
    }

    // ---------------------------------------------------------------- Trockenlauf, Rückgängig, Rückfall

    [Fact]
    public void Uebernehmen_RueckgaengigEntferntDenPlatzhalterUndStelltDieBasisZurueck()
    {
        var p = Bereit(MitSerie(formteile: false));
        Sys(p, "ZUL", "Zuluft");
        HkapStrang("ST1", "L_01", "Zuluft Nord", "Zuluft");
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["straenge"] }""");
        Assert.NotNull(p.Modell.Finde("L_01"));
        Assert.Empty(Eintraege(Straenge(p), "nachKicad", "neu"));
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("L_01"));
        var ab = Straenge(p);
        Assert.Single(Eintraege(ab, "nachKicad", "neu"));                                  // wieder neu in HKAP (Basis zurückgestellt)
        Assert.True(p.Wiederholen().Angenommen);
        Assert.NotNull(p.Modell.Finde("L_01"));
        Assert.Empty(Eintraege(Straenge(p), "nachKicad", "neu"));
    }

    [Fact]
    public void Senden_KennungenRueckgaengig_HkapNenntDieElementIds_ZuordnungUeberKicadId()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.True(p.Rueckgaengig().Angenommen);                                                            // Kennungspaket zurück: HKAP hat den Strang, KICAD kennt die Id nicht mehr
        Assert.Equal("", Kennung(p, "HZ-VL-1"));
        var ab = Straenge(p);
        Assert.Empty(Eintraege(ab, "nachHkap", "neu"));                                   // kein zweiter HKAP-Strang
        Assert.Empty(Eintraege(ab, "nachKicad", "neu"));
        Assert.Contains(ab["zuordnungen"]!.AsArray(), z => z!["hkapId"]!.GetValue<string>() == "ST1" && z["grund"]!.GetValue<string>().Contains("kicad.straenge"));
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(1, a.Straenge.Count);
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "HZ-VL-1"));                            // Kennung wieder gesetzt
        Assert.Equal("PJ-2026-003/ST1", Kennung(p, "HZ-RL-1"));
    }

    [Fact]
    public void KicadStrangGeloescht_NurVorschlag_NieAutomatisch()
    {
        var p = Bereit();
        Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        foreach (var kz in new[] { "RO-V1", "RO-V2", "RO-V3", "RO-R1", "HZ-VL-1", "HZ-RL-1" }) Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = kz }));
        var ab = Straenge(p);
        Assert.Contains(Eintraege(ab, "nachHkap", "geloescht"), x => x["hkapId"]!.GetValue<string>() == "ST1");
        var e = Gut(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Contains(e.Hinweise, h => h.Contains("in KICAD gelöscht"));
        Assert.Equal(1, a.Straenge.Count);                                                 // HKAP behält den Strang
        a.Straenge.Clear();                                                               // in HKAP gelöscht: KICAD-Strang bleibt (nur Vorschlag)
    }

    [Fact]
    public void HkapOhneStraenge_Datenart_WirdUebergangen_BestehendeDatenartenBleiben()
    {
        lock (a.Sperre)
        {
            var arten = a.Katalog["datenarten"]!.AsArray();
            arten.Remove(arten.Single(x => x!["name"]!.GetValue<string>() == "straenge"));
        }
        var p = Bereit();
        var ab = Straenge(p);
        Assert.Contains(ab["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("kennt die Datenart «straenge» nicht"));
        Assert.DoesNotContain(a.Anfragen, x => x.Pfad.EndsWith("/straenge"));              // weder gelesen noch geschrieben
        var e = Gut(p, "hkap_senden");                                                    // Vorgabe: alle Datenarten, ohne Fehler
        Assert.DoesNotContain(a.Anfragen, x => x.Pfad.EndsWith("/straenge"));
        Assert.Contains(e.Hinweise, h => h.Contains("kennt die Datenart «straenge» nicht"));
        Gut(p, "hkap_uebernehmen", """{ "datenarten": ["geschosse"] }""");
    }

    // ---------------------------------------------------------------- Attrappe = Verhalten des HKAP-Austauschdienstes (Gegenprobe)

    [Fact]
    public void Attrappe_StrangVerhaltenWieAustauschService_NeuVersionKonfliktBlockGanzErsetztMediumAusDerListe()
    {
        var klient = new HkapKlient(a.Url);
        const string pj = HkapAttrappe.Pj;
        HkapAenderung Neu(string medium, string bez, JsonObject? kicad = null) =>
            new() { Felder = J($$"""{ "bezeichnung": "{{bez}}", "medium": "{{medium}}" }""").Also(o => { if (kicad is not null) o["kicad"] = kicad; }) };
        var block = J("""{ "straenge": [ { "id": "x1", "kennzeichen": "HZ-VL-1", "name": "n", "system": "HZ-VL", "systemtyp": "Heizung Vorlauf", "geschosse": ["E1"], "endgeraete": 2, "laengen": [ { "serie": "S", "groesse": "DN 20", "laengeM": 1.5 } ] } ] }""");

        // neu ohne id: ST<n>, Nummer nach Gewerk (H_, K_, S_, L_), je Gewerk hochgezählt
        var r = klient.Schreiben("P-005", "straenge", pj, false, [Neu("Heizung", "A", block), Neu("Heizung", "B"), Neu("Kälte", "C"), Neu("Sanitär", "D"), Neu("Zuluft", "E"), Neu("Heizung", "F")]);
        Assert.Equal(["ST1", "ST2", "ST3", "ST4", "ST5", "ST6"], r.Select(x => x.Id).ToArray());
        Assert.All(r, x => Assert.Equal("neu", x.Status));
        Assert.Equal(["H_01", "H_02", "K_01", "S_01", "L_01", "H_03"], a.Straenge.Select(s => s.Felder["nummer"]!.GetValue<string>()).ToArray());

        // medium nur aus der Liste: sonst wird es nicht geschrieben (mit Grund), der Rest schon
        var falsch = klient.Schreiben("P-005", "straenge", pj, false, [Neu("Wasser", "G")]).Single();
        Assert.Equal("neu", falsch.Status);
        Assert.Contains("medium", falsch.Grund);
        Assert.Equal("", a.Straenge.Last().Felder["medium"]!.GetValue<string>());

        // kicad ganz ersetzt; gleicher Inhalt = unverändert; anderer Inhalt = geändert mit neuer Version
        var s1 = a.Straenge[0];
        var v1 = s1.Version;
        var gleich = klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST1", Version = v1, Felder = J("{}").Also(o => o["kicad"] = block.DeepClone()) }]).Single();
        Assert.Equal("unveraendert", gleich.Status);
        Assert.Equal(v1, s1.Version);
        var anders = (JsonObject)block.DeepClone();
        anders["straenge"]![0]!["endgeraete"] = 3;
        var geaendert = klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST1", Version = v1, Felder = J("{}").Also(o => o["kicad"] = anders) }]).Single();
        Assert.Equal("geaendert", geaendert.Status);
        Assert.Equal(3, Block(s1)[0]!["endgeraete"]!.GetValue<int>());
        Assert.NotEqual(v1, s1.Version);

        // veraltete Version → Konflikt mit aktuellem Stand, nichts überschrieben
        var konflikt = klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST1", Version = v1, Felder = J("""{ "bezeichnung": "neu" }""") }]).Single();
        Assert.Equal("konflikt", konflikt.Status);
        Assert.Equal("A", s1.Felder["bezeichnung"]!.GetValue<string>());
        // Löschen abgelehnt; unbekannte Id abgelehnt; groesse als Zahl kann HKAP nicht lesen → Block bleibt
        Assert.Equal("abgelehnt", klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST1", Version = s1.Version, Loeschen = true }]).Single().Status);
        Assert.Equal("abgelehnt", klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST99", Felder = J("{}") }]).Single().Status);
        var zahl = (JsonObject)block.DeepClone();
        zahl["straenge"]![0]!["laengen"]![0]!["groesse"] = 20;
        var abgelehnt = klient.Schreiben("P-005", "straenge", pj, false, [new HkapAenderung { Id = "ST1", Version = s1.Version, Felder = J("{}").Also(o => o["kicad"] = zahl) }]).Single();
        Assert.Equal("unveraendert", abgelehnt.Status);
        Assert.Contains("kicad", abgelehnt.Grund);
        Assert.Equal(3, Block(s1)[0]!["endgeraete"]!.GetValue<int>());
        // Trockenlauf schreibt nichts
        int n = a.Straenge.Count;
        Assert.Equal("neu", klient.Schreiben("P-005", "straenge", pj, true, [Neu("Heizung", "Z")]).Single().Status);
        Assert.Equal(n, a.Straenge.Count);
    }

    [Fact]
    public void Vertrag_AbgleichUndWerkzeugdefinitionenKennenStraengeUndBuendeln()
    {
        Assert.Contains("straenge", HkapAbbildung.Datenarten);
        Assert.Contains(Abfragen.Liste, d => d.Name == "hkap_abgleich" && d.Parameter.Any(x => x.Name == "buendeln"));
        var p = Heizung();
        Assert.Contains(p.Befehle.Alle, b => b.Name == "hkap_strang_zuordnen" && b.Parameter.Select(x => x.Name).SequenceEqual(["strang", "hkap"]));
        Assert.Contains(p.Befehle.Alle, b => b.Name == "hkap_uebernehmen" && b.Parameter.Any(x => x.Name == "system"));
        Assert.Contains(p.Befehle.Alle, b => b.Name == "hkap_senden" && b.Parameter.Any(x => x.Name == "buendeln"));
        // nicht verknüpft = Antwort, kein Fehler (auch mit den neuen Parametern)
        Assert.False(Abgleich(p, """{ "buendeln": "nein" }""")["verknuepft"]!.GetValue<bool>());
    }
}

static class HkapStraengeTestHilfe
{
    /// <summary>Objekt ändern und zurückgeben (für Aufrufe in einem Ausdruck).</summary>
    public static JsonObject Also(this JsonObject o, Action<JsonObject> aenderung) { aenderung(o); return o; }
}
