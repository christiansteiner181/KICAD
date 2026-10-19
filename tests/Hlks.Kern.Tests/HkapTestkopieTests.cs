// Integrationstest gegen die HKAP-Testkopie (Port 5098, _build\hkap_testkopie.ps1; Datenkopie, nie Livedaten).
// Läuft nur mit HLKS_HKAP_TESTKOPIE=1 und erreichbarer Testkopie, sonst kehrt er früh zurück. Projekt und Projektierung
// aus HLKS_HKAP_TESTPROJEKT / HLKS_HKAP_TESTPROJEKTIERUNG, sonst das erste Projekt mit einer Projektierung.
// Schreibt in die Testkopie: die KICAD-Fläche eines Raums (kicad { … }) und eine Pendenz aus einem Prüfbefund.

using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Hlks.Kern.Tests;

public sealed class HkapTestkopieTests(ITestOutputHelper aus)
{
    const string Url = "http://localhost:5098";   // nur die Testkopie – nie 5013
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    BefehlsErgebnis Ok(Projekt p, string befehl, string json = "{}", bool trockenlauf = false)
    {
        var e = p.Ausfuehren(befehl, J(json), trockenlauf);
        aus.WriteLine($"{befehl}{(trockenlauf ? " (Trockenlauf)" : "")}: {e.Meldung} | {string.Join(" | ", e.Hinweise.Take(12))}");
        Assert.True(e.Angenommen, $"{befehl}: {e.Meldung} {e.Fehler} {e.Vorschlag}");
        return e;
    }

    [Fact]
    public void Testkopie_VerknuepfenUebernehmenFlaecheSendenBefundAlsPendenz()
    {
        if (Environment.GetEnvironmentVariable("HLKS_HKAP_TESTKOPIE") != "1") return;
        var klient = new HkapKlient(Url);
        try { klient.Katalog(); }
        catch (HlksFehler f) { aus.WriteLine("Testkopie nicht erreichbar, übersprungen: " + f.Message); return; }

        var projekt = Environment.GetEnvironmentVariable("HLKS_HKAP_TESTPROJEKT");
        var pj = Environment.GetEnvironmentVariable("HLKS_HKAP_TESTPROJEKTIERUNG");
        if (string.IsNullOrWhiteSpace(projekt) || string.IsNullOrWhiteSpace(pj))
        {
            foreach (var x in klient.Projekte())
            {
                var id = HkapWerte.Text(x?["id"]);
                if (id is null) continue;
                var pjs = klient.Projektierungen(id);
                if (pjs.Count == 0) continue;
                projekt = id; pj = HkapWerte.Text(pjs[0]?["id"]);
                break;
            }
        }
        if (projekt is null || pj is null) { aus.WriteLine("Kein Projekt mit Projektierung in der Testkopie, übersprungen."); return; }
        aus.WriteLine($"Testkopie {Url}: {projekt}/{pj}");

        var p = Projekt.Anlegen(Kat, null, "Testkopie", "hlks-test", "pc-test");
        Ok(p, "hkap_verknuepfen", $$"""{ "projekt": "{{projekt}}", "projektierung": "{{pj}}", "url": "{{Url}}" }""");
        var verbindung = (JsonObject)Tuer.Abfrage(p, "hkap_verbindung", J($$"""{ "projekt": "{{projekt}}" }"""));   // neue Projektliste oder Rückfall
        Assert.Contains(verbindung["projektierungen"]!.AsArray(), x => x!["id"]!.GetValue<string>() == pj);
        var vorschau = (JsonObject)Tuer.Abfrage(p, "hkap_abgleich");
        Assert.True(vorschau["erreichbar"]!.GetValue<bool>(), vorschau.ToJsonString());
        aus.WriteLine("Vorschau: " + vorschau["zusammenfassung"]!.ToJsonString() + " Warnungen: " + vorschau["warnungen"]!.ToJsonString());
        Ok(p, "hkap_uebernehmen", trockenlauf: true);
        Ok(p, "hkap_uebernehmen", """{ "konflikte": { "*": "hkap" } }""");
        Assert.False(string.IsNullOrEmpty(HkapVerknuepfung.Info(p.Modell)!.Text("zeile1")), "Projektdaten im Plankopf");

        // Testdaten anderer (z. B. E1-S3 mit kicad.id «test-1») nicht überschreiben: einen Raum ohne kicad-Werte in HKAP nehmen
        var ohneKicad = klient.Lesen(projekt, "raeume", pj).Where(x => x.Felder["kicad"] is null).Select(x => $"{pj}/{x.Id}").ToHashSet();
        var raum = p.Modell.DerKlasse("Raum").Where(r => r.Geometrie is null && r.Kennungen?.GetValueOrDefault("hkap") is { } k && ohneKicad.Contains(k))
            .OrderBy(r => r.Kennzeichen).FirstOrDefault();
        if (raum is not null)
        {
            var kote = (p.Modell.Finde(raum.Geschoss)?.Zahl("kote") ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Ok(p, "element_aendern", $$"""{ "element": "{{raum.Kennzeichen}}", "geometrie": { "art": "polygon", "punkte": [[0,0,{{kote}}],[2000,0,{{kote}}],[2000,3000,{{kote}}],[0,3000,{{kote}}]], "hoehe": 2400 } }""");
            var id = raum.Kennungen!["hkap"].Split('/', 2)[1];
            Ok(p, "hkap_senden", trockenlauf: true);
            Assert.Null(klient.Lesen(projekt, "raeume", pj).Single(x => x.Id == id).Felder["kicad"]);   // Trockenlauf schreibt nicht
            Ok(p, "hkap_senden");
            var hkap = klient.Lesen(projekt, "raeume", pj).Single(x => x.Id == id);
            aus.WriteLine($"HKAP {id} nach dem Senden: {hkap.Felder["kicad"]?.ToJsonString()}");
            Assert.Equal(6.0, Json.Zahl(HkapWerte.Flach(hkap.Felder)["kicad.flaecheM2"]));
            var nachher = (JsonObject)Tuer.Abfrage(p, "hkap_abgleich", J("""{ "datenarten": ["raeume"] }"""));
            Assert.DoesNotContain(nachher["datenarten"]![0]!["nachHkap"]!["geaendert"]!.AsArray(), x => x!["hkapId"]!.GetValue<string>() == id);
        }
        else aus.WriteLine("Die Projektierung hat keine Räume – Fläche senden übersprungen.");

        Ok(p, "element_anlegen", """{ "klasse": "Strang", "kennzeichen": "ST-HKAP-TEST", "name": "Testbefund (P09)" }""");
        var schluessel = HkapPendenzen.QuelleId(HkapPendenzen.Kurzkennung(p.Kopf), Pruefungen.Alle(p.Modell, Kat).First(b => b.Element == "ST-HKAP-TEST"));
        Ok(p, "hkap_befunde_senden");
        int Anzahl() => klient.Lesen(projekt, "pendenzen", null).Count(x => HkapWerte.Text(x.Felder["quelleId"]) == schluessel);
        Assert.Equal(1, Anzahl());
        Ok(p, "hkap_befunde_senden");
        Assert.Equal(1, Anzahl());                                    // kein Doppel
        Assert.True(p.Rueckgaengig().Angenommen);                     // KICAD-seitig rückgängig machbar
    }

    /// <summary>Modul 3 (Hauptlauf, Phase D): Datenart «straenge» gegen die Testkopie – Heizung Vorlauf und Rücklauf (je ein
    /// KICAD-Strang mit einer Geraden DN 20 der FIKTIVEN Testrohrserie, 3000 mm) werden als ein HKAP-Strang «Heizung» mit zwei
    /// KICAD-Strängen gesendet; zweites Senden ändert nichts.</summary>
    [Fact]
    public void Testkopie_StraengeHeizungVlRlAlsEinHkapStrang()
    {
        if (Environment.GetEnvironmentVariable("HLKS_HKAP_TESTKOPIE") != "1") return;
        var klient = new HkapKlient(Url);
        try { klient.Katalog(); }
        catch (HlksFehler f) { aus.WriteLine("Testkopie nicht erreichbar, übersprungen: " + f.Message); return; }
        string? projekt = null, pj = null;
        foreach (var x in klient.Projekte())
        {
            var id = HkapWerte.Text(x?["id"]);
            if (id is null) continue;
            var pjs = klient.Projektierungen(id);
            if (pjs.Count == 0) continue;
            projekt = id; pj = HkapWerte.Text(pjs[0]?["id"]);
            break;
        }
        if (projekt is null || pj is null) { aus.WriteLine("Kein Projekt mit Projektierung in der Testkopie, übersprungen."); return; }
        var vorher = klient.Lesen(projekt, "straenge", pj).Select(x => x.Id).ToHashSet();
        aus.WriteLine($"Testkopie {Url}: {projekt}/{pj}, {vorher.Count} Stränge vorher");

        var p = Projekt.Anlegen(Kat, null, "Testkopie Stränge", "hlks-test", "pc-test");
        Ok(p, "hkap_verknuepfen", $$"""{ "projekt": "{{projekt}}", "projektierung": "{{pj}}", "url": "{{Url}}" }""");
        Ok(p, "hkap_uebernehmen", """{ "konflikte": { "*": "hkap" }, "datenarten": ["projekt", "geschosse"] }""");
        var name = "Nachttest " + DateTime.Now.ToString("HHmmss");   // eindeutig gegen bestehende HKAP-Stränge (gleiche Bezeichnung wird nicht gesendet)
        Ok(p, "element_anlegen", """{ "klasse": "System", "kennzeichen": "HZ-VL", "eigenschaften": { "systemtyp": "Heizung Vorlauf" } }""");
        Ok(p, "element_anlegen", """{ "klasse": "System", "kennzeichen": "HZ-RL", "eigenschaften": { "systemtyp": "Heizung Rücklauf" } }""");
        Ok(p, "element_anlegen", $$"""{ "klasse": "Strang", "kennzeichen": "HZ-VL-1", "teilVon": "HZ-VL", "name": "{{name}}", "eigenschaften": { "steigzone": "ST 1" } }""");   // Bündelung über die Steigzone (Christian 19.10.2026)
        Ok(p, "element_anlegen", $$"""{ "klasse": "Strang", "kennzeichen": "HZ-RL-1", "teilVon": "HZ-RL", "name": "{{name}}", "eigenschaften": { "steigzone": "ST 1" } }""");
        Ok(p, "familie_laden", new JsonObject { ["datei"] = IfcHilfe.Testdaten(Path.Combine("familien", "test_rohrserie.familie.json")) }.ToJsonString());
        Ok(p, "leitung_zeichnen", """{ "serie": "fam-test-rohrserie", "groesse": "DN 20", "strang": "HZ-VL-1", "punkte": [[0, 0, 500], [3000, 0, 500]] }""");
        Ok(p, "leitung_zeichnen", """{ "serie": "fam-test-rohrserie", "groesse": "DN 20", "strang": "HZ-RL-1", "punkte": [[3000, 200, 500], [0, 200, 500]] }""");

        Ok(p, "hkap_senden", """{ "datenarten": ["straenge"] }""", trockenlauf: true);
        Assert.Equal(vorher.Count, klient.Lesen(projekt, "straenge", pj).Count);   // Trockenlauf schreibt nicht
        Ok(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        var neu = klient.Lesen(projekt, "straenge", pj).Where(x => !vorher.Contains(x.Id)).ToList();
        var s = Assert.Single(neu);                                        // VL und RL gebündelt
        aus.WriteLine($"HKAP {s.Id} nach dem Senden: {s.Felder.ToJsonString()}");
        Assert.Equal("Heizung", HkapWerte.Text(s.Felder["medium"]));
        Assert.Equal(name, HkapWerte.Text(s.Felder["bezeichnung"]));
        Assert.Matches("^H_\\d\\d$", HkapWerte.Text(s.Felder["nummer"]) ?? "");
        var ks = s.Felder["kicad"]!["straenge"]!.AsArray();
        Assert.Equal(["HZ-RL-1", "HZ-VL-1"], ks.Select(x => HkapWerte.Text(x!["kennzeichen"])!).Order().ToArray());
        foreach (var k in ks)   // je Strang eine Gerade DN 20 über 3000 mm = 3.0 m
            Assert.Equal(3.0, Json.Zahl(k!["laengen"]![0]!["laengeM"]));
        Assert.All(p.Modell.DerKlasse("Strang"), x => Assert.Equal($"{pj}/{s.Id}", x.Kennungen?.GetValueOrDefault("hkap")));

        Ok(p, "hkap_senden", """{ "datenarten": ["straenge"] }""");
        Assert.Equal(vorher.Count + 1, klient.Lesen(projekt, "straenge", pj).Count);   // kein zweiter Strang
        var nachher = (JsonObject)Tuer.Abfrage(p, "hkap_abgleich", J("""{ "datenarten": ["straenge"] }"""));
        aus.WriteLine("Abgleich danach: " + nachher["zusammenfassung"]?.ToJsonString());
        Assert.Empty(nachher["datenarten"]![0]!["nachHkap"]!["geaendert"]!.AsArray());
        Assert.Empty(nachher["datenarten"]![0]!["nachHkap"]!["neu"]!.AsArray());
    }
}
