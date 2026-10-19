// Befehle und Abfragen der HKAP-Universalschnittstelle (Modul 1d) gegen die Attrappe (HkapAttrappe): verknüpfen,
// übernehmen (nicht platzierte Räume), zuordnen, Fläche senden, Konflikte, führende Seite, veraltete Version,
// HKAP nicht erreichbar, Prüfbefunde → Pendenzen, Rückgängig, Basis nur nach Erfolg, Trockenlauf.

using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

public sealed class HkapBefehleTests : IDisposable
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks-hkap-" + Guid.NewGuid().ToString("N")[..10]);
    readonly HkapAttrappe a = new();

    public void Dispose()
    {
        a.Dispose();
        for (int i = 0; i < 3 && Directory.Exists(wurzel); i++)
            try { Directory.Delete(wurzel, true); } catch (IOException) { Thread.Sleep(100); } catch (UnauthorizedAccessException) { Thread.Sleep(100); }
    }

    // ---------------------------------------------------------------- Hilfen

    static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    static BefehlsErgebnis Ok(Projekt p, string befehl, string json = "{}", bool trockenlauf = false)
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

    /// <summary>KICAD-Projekt: Gebäude, Geschoss EG, gezeichneter Raum R-E02 (Nummer E.02, 3 × 4 m, Höhe 2.5 m).</summary>
    static Projekt NeuesProjekt(string? ordner = null, string geraet = "pc-test")
    {
        var p = Projekt.Anlegen(Kat, ordner is null ? null : new OrdnerSpeicher(ordner), "HKAP-Test", "Tester", geraet);
        Ok(p, "element_anlegen", """{ "klasse": "Gebaeude", "kennzeichen": "GEB-1", "name": "MFH Musterweg" }""");
        Ok(p, "element_anlegen", """{ "klasse": "Geschoss", "kennzeichen": "EG", "name": "Erdgeschoss", "teilVon": "GEB-1", "eigenschaften": { "kote": 0, "hoehe": 3000 } }""");
        Ok(p, "element_anlegen", """{ "klasse": "Raum", "kennzeichen": "R-E02", "name": "Bad", "geschoss": "EG", "eigenschaften": { "nummer": "E.02", "nutzung": "Bad" }, "geometrie": { "art": "polygon", "punkte": [[0,0,0],[3000,0,0],[3000,4000,0],[0,4000,0]], "hoehe": 2500 } }""");
        return p;
    }

    void Verknuepfen(Projekt p) => Ok(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""");

    /// <summary>Verknüpft, übernimmt und ordnet R-E02 dem HKAP-Raum E1-S2 zu: Ausgangslage für Senden und Konflikte.</summary>
    Projekt Abgeglichen(string? ordner = null)
    {
        var p = NeuesProjekt(ordner);
        Verknuepfen(p);
        Ok(p, "hkap_uebernehmen", """{ "konflikte": { "geschosse/E1/name": "hkap" } }""");
        Ok(p, "hkap_raum_zuordnen", """{ "raum": "R-E02", "hkapRaum": "E1-S2" }""");
        return p;
    }

    static JsonObject Abgleich(Projekt p) => (JsonObject)Tuer.Abfrage(p, "hkap_abgleich");
    static JsonObject Art(JsonObject abgleich, string name) => abgleich["datenarten"]!.AsArray().Single(d => d!["name"]!.GetValue<string>() == name)!.AsObject();
    static IEnumerable<JsonObject> Eintraege(JsonObject art, string richtung, string was) => art[richtung]![was]!.AsArray().Select(x => x!.AsObject());
    static IEnumerable<string> Felder(JsonObject art, string richtung, string hkapId) =>
        Eintraege(art, richtung, "geaendert").Where(x => x["hkapId"]?.GetValue<string>() == hkapId).SelectMany(x => x["felder"]!.AsArray()).Select(f => f!["feld"]!.GetValue<string>());
    static string Kennung(Projekt p, string kz) => p.Modell.Hole(kz).Kennungen?.GetValueOrDefault("hkap") ?? "";

    static string TotesHkap()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return $"http://127.0.0.1:{port}";
    }

    // ---------------------------------------------------------------- verknüpfen und Verbindung

    [Fact]
    public void Verknuepfen_PrueftProjektUndProjektierungUndSchreibtProjektinformation()
    {
        var p = NeuesProjekt();
        var tl = Ok(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-003", "url": "{{a.Url}}" }""", trockenlauf: true);
        Assert.True(tl.Trockenlauf);
        Assert.Null(HkapVerknuepfung.Lesen(p.Modell));
        Verknuepfen(p);
        var v = HkapVerknuepfung.Lesen(p.Modell)!;
        Assert.Equal(("P-005", "PJ-2026-003", "MUS", "MFH Musterweg", a.Url), (v.Projekt, v.Projektierung, v.Kuerzel, v.Name, v.Url));
        var w = HkapVerknuepfung.Info(p.Modell)!.Eigenschaft("hkap")!;
        Assert.Equal("hkap", w.Herkunft.Art);
        Assert.StartsWith("HKAP P-005/PJ-2026-003", w.Herkunft.Quelle);

        var f = Nein(p, "hkap_verknuepfen", $$"""{ "projekt": "P-999", "url": "{{a.Url}}" }""");
        Assert.Contains("P-999", f.Fehler);
        Assert.Contains("P-005", f.Vorschlag);          // Liste der HKAP-Projekte als Weg zur Lösung
        var g = Nein(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-999", "url": "{{a.Url}}" }""");
        Assert.Contains("PJ-2026-999", g.Fehler);
        Assert.Contains("PJ-2026-003", g.Vorschlag);
        Assert.Equal("PJ-2026-003", HkapVerknuepfung.Lesen(p.Modell)!.Projektierung);   // abgelehnt ändert nichts
    }

    [Fact]
    public void Verbindung_ZeigtFassungDatenartenProjekteUndProjektierungen()
    {
        var p = NeuesProjekt();
        var o = (JsonObject)Tuer.Abfrage(p, "hkap_verbindung", J($$"""{ "url": "{{a.Url}}", "projekt": "P-005" }"""));
        Assert.True(o["erreichbar"]!.GetValue<bool>());
        Assert.Equal("1", o["fassung"]!.GetValue<string>());
        Assert.Equal(5, o["datenarten"]!.AsArray().Count);   // projekt, geschosse, raeume, straenge (Modul 3), pendenzen
        var pr = o["projekte"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == "P-005")!;
        Assert.Equal("MUS", pr["kuerzel"]!.GetValue<string>());                         // aus /api/austausch/projekte
        var pj = o["projektierungen"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == "PJ-2026-003")!;
        Assert.Equal((2, 3), (pj["etagen"]!.GetValue<int>(), pj["raeume"]!.GetValue<int>()));
        Assert.DoesNotContain(a.Anfragen, x => x.Pfad == "/api/projects");              // kein Rückfall nötig
    }

    [Fact]
    public void Verbindung_AeltereHkapOhneProjektlisteFaelltAufAlteStellenZurueck()
    {
        a.OhneProjekteListe = true;
        var p = NeuesProjekt();
        var o = (JsonObject)Tuer.Abfrage(p, "hkap_verbindung", J($$"""{ "url": "{{a.Url}}", "projekt": "P-005" }"""));
        Assert.True(o["erreichbar"]!.GetValue<bool>());
        Assert.Contains(o["projekte"]!.AsArray(), x => x!["id"]!.GetValue<string>() == "P-005");
        Assert.Contains(o["projektierungen"]!.AsArray(), x => x!["id"]!.GetValue<string>() == "PJ-2026-003");
        Assert.Contains(a.Anfragen, x => x.Pfad == "/api/projects");
        Assert.Contains(a.Anfragen, x => x.Pfad == "/api/projects/P-005/projektierungen");
        Assert.Single(a.Anfragen, x => x.Pfad == "/api/austausch/projekte");           // einmal versucht, dann gemerkt
        var f = Nein(p, "hkap_verknuepfen", $$"""{ "projekt": "P-005", "projektierung": "PJ-2026-999", "url": "{{a.Url}}" }""");
        Assert.Contains("PJ-2026-003", f.Vorschlag);
    }

    // ---------------------------------------------------------------- übernehmen

    [Fact]
    public void Uebernehmen_ProjektdatenGeschosseUndNichtPlatzierteRaeume()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        var e = Ok(p, "hkap_uebernehmen");
        var m = p.Modell;

        // Projektdaten in die Projektinformation (Plankopf)
        var pi = HkapVerknuepfung.Info(m)!;
        Assert.Equal("MFH Musterweg", pi.Text("zeile1"));
        Assert.Equal("Musterweg 1, 8000 Zürich, Muster AG", pi.Text("zeile2"));
        Assert.Equal("MUS", pi.Text("projektnummer"));
        Assert.Equal("Bauprojekt", pi.Text("phase"));

        // Geschosse: EG über den eindeutigen Namen zugeordnet, 1.OG neu
        Assert.Equal("PJ-2026-003/E1", Kennung(p, "EG"));
        Assert.Equal("E", m.Hole("EG").Text("kurz"));
        Assert.Equal("Erdgeschoss", m.Hole("EG").Name);              // Name auf beiden Seiten verschieden: Konflikt, nichts überschrieben
        var og = m.Hole("1.OG");
        Assert.Equal("PJ-2026-003/E2", Kennung(p, "1.OG"));
        Assert.Equal(3000, og.Zahl("hoehe"));                        // Platzhalter, HKAP kennt keine Geschosshöhe
        Assert.Contains(e.Hinweise, h => h.Contains("1.OG") && h.Contains("PLATZHALTER") && h.Contains("Annahme"));
        Assert.StartsWith("PLATZHALTER", og.Eigenschaft("hoehe")!.Herkunft.Quelle);
        Assert.NotEqual("hkap", og.Eigenschaft("hoehe")!.Herkunft.Art);   // kein Wert aus HKAP

        // Räume ohne Gegenstück → nicht platziert; E1-S2 hat einen KICAD-Raum mit gleicher Nummer → nicht angelegt
        var r = m.Hole("E.01");
        Assert.Null(r.Geometrie);
        Assert.Equal("PJ-2026-003/E1-S1", Kennung(p, "E.01"));
        Assert.Equal(("Wohnen", "E.01", "Wohnen"), (r.Name, r.Text("nummer"), r.Text("nutzung")));
        Assert.Equal(m.Hole("EG").Id, r.Geschoss);
        Assert.Equal(og.Id, m.Hole("1.01").Geschoss);
        Assert.Equal("", Kennung(p, "R-E02"));
        Assert.Equal(2, m.DerKlasse("Raum").Count(x => x.Kennungen?.ContainsKey("hkap") == true));
        Assert.Contains(e.Hinweise, h => h.Contains("E1-S2") && h.Contains("hkap_raum_zuordnen"));
        Assert.Contains(e.Hinweise, h => h.Contains("noch nicht platziert"));
        Assert.Equal(4, r.Zahl("personen"));                        // Anforderungen übernommen (HKAP führt)

        var ab = Abgleich(p);
        var raeume = Art(ab, "raeume");
        Assert.Equal(new[] { "1.01", "E.01" }, raeume["nichtPlatziert"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Contains(raeume["zuordnungsvorschlaege"]!.AsArray(), x => x!["hkapId"]!.GetValue<string>() == "E1-S2" && x["raum"]!.GetValue<string>() == "R-E02");
        Assert.Contains(ab["konflikte"]!.AsArray(), k => k!["schluessel"]!.GetValue<string>() == "geschosse/E1/name");
        Assert.DoesNotContain(Eintraege(raeume, "nachKicad", "neu"), x => x["hinweis"]?.GetValue<string>()?.Contains("nicht angelegt") != true);
    }

    [Fact]
    public void Uebernehmen_HerkunftHkapMitDatensatzFeldUndStand()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "hkap_uebernehmen");
        var stand = new HkapAttrappe.Satz { Felder = a.Projekte["P-005"] }.Version;
        var z1 = HkapVerknuepfung.Info(p.Modell)!.Eigenschaft("zeile1")!;
        Assert.Equal("hkap", z1.Herkunft.Art);
        Assert.Equal($"HKAP P-005, Feld bezeichnung, Stand {stand}", z1.Herkunft.Quelle);
        Assert.Equal("hkap_uebernehmen", z1.Herkunft.Befehl);
        var nr = p.Modell.Hole("E.01").Eigenschaft("nummer")!;
        Assert.Equal("hkap", nr.Herkunft.Art);
        Assert.StartsWith("HKAP P-005/PJ-2026-003/E1-S1, Feld raumNummer, Stand ", nr.Herkunft.Quelle);
        var warum = (JsonObject)Tuer.Abfrage(p, "warum", J("""{ "element": "E.01", "eigenschaft": "nummer" }"""));
        Assert.Contains("E1-S1", warum.ToJsonString());
    }

    [Fact]
    public void Anforderungen_HkapFuehrtUndFliesstNurNachKicad()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        var vor = Abgleich(p);
        Assert.True(vor["erreichbar"]!.GetValue<bool>());
        var raeume = Art(vor, "raeume");
        Assert.Equal(2, Eintraege(raeume, "nachKicad", "neu").Count(x => x["hinweis"]?.GetValue<string>() == "wird nicht platzierter Raum"));
        Assert.Contains(Eintraege(raeume, "nachHkap", "neu"), x => x["element"]!.GetValue<string>() == "R-E02");
        Ok(p, "hkap_uebernehmen");

        // übernommen mit Einheit aus dem Katalog und Herkunft hkap; tempWinter auf tempWinter, nicht auf solltemperatur
        var r = p.Modell.Hole("E.01");
        Assert.Equal((4.0, 120.0, 0.0, 1500.0, 21.0, 26.0, 1000.0), (r.Zahl("personen")!.Value, r.Zahl("zuluft")!.Value, r.Zahl("abluft")!.Value,
            r.Zahl("heizleistung")!.Value, r.Zahl("tempWinter")!.Value, r.Zahl("tempSommer")!.Value, r.Zahl("co2Max")!.Value));
        Assert.Equal("m³/h", r.Eigenschaft("zuluft")!.Einheit);
        Assert.Equal("W", r.Eigenschaft("heizleistung")!.Einheit);
        Assert.Equal("°C", r.Eigenschaft("tempWinter")!.Einheit);
        Assert.Null(r.Eigenschaft("solltemperatur"));
        var h = r.Eigenschaft("personen")!.Herkunft;
        Assert.Equal("hkap", h.Art);
        Assert.StartsWith("HKAP P-005/PJ-2026-003/E1-S1, Feld personen, Stand ", h.Quelle);
        Assert.Contains("führt HKAP", Abgleich(p)["hinweis"]!.GetValue<string>());

        // in HKAP geändert → übernehmen
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S1", f => f["personen"] = 5);
        Assert.Contains("personen", Felder(Art(Abgleich(p), "raeume"), "nachKicad", "E1-S1"));
        Ok(p, "hkap_uebernehmen");
        Assert.Equal(5, p.Modell.Hole("E.01").Zahl("personen"));

        // in KICAD geändert → nie gesendet, HKAP setzt sich beim Übernehmen durch (Einbahn, HKAP führt)
        Ok(p, "element_aendern", """{ "element": "E.01", "eigenschaften": { "personen": 9, "tempWinter": 18 } }""");
        raeume = Art(Abgleich(p), "raeume");
        Assert.DoesNotContain("personen", Felder(raeume, "nachHkap", "E1-S1"));
        var u = Eintraege(raeume, "nachKicad", "geaendert").Single(x => x["hkapId"]!.GetValue<string>() == "E1-S1")["felder"]!.AsArray();
        Assert.True(u.Single(f => f!["feld"]!.GetValue<string>() == "personen")!["ueberschreibt"]!.GetValue<bool>());
        Ok(p, "hkap_senden");
        Assert.Equal(5, a.Raum("E1-S1")["personen"]!.GetValue<int>());
        Assert.All(a.Posts("raeume").Select(x => x.Body!.ToJsonString()), b => Assert.DoesNotContain("\"tempWinter\"", b));
        Ok(p, "hkap_uebernehmen");
        Assert.Equal((5.0, 21.0), (p.Modell.Hole("E.01").Zahl("personen")!.Value, p.Modell.Hole("E.01").Zahl("tempWinter")!.Value));
    }

    [Fact]
    public void Anforderungen_AndereEinheitInHkapFliesstNicht()
    {
        lock (a.Sperre)
        {
            var raeume = a.Katalog["datenarten"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "raeume")!;
            raeume["felder"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "zuluft")!["einheit"] = "l/s";
        }
        var p = NeuesProjekt();
        Verknuepfen(p);
        var e = Ok(p, "hkap_uebernehmen");
        Assert.Contains(e.Hinweise, h => h.Contains("zuluft") && h.Contains("l/s") && h.Contains("m³/h"));
        Assert.Null(p.Modell.Hole("E.01").Eigenschaft("zuluft"));
        Assert.Equal(4, p.Modell.Hole("E.01").Zahl("personen"));
    }

    // ---------------------------------------------------------------- zuordnen und senden

    [Fact]
    public void RaumZuordnen_UndFlaecheSenden()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "hkap_uebernehmen");
        Ok(p, "hkap_raum_zuordnen", """{ "raum": "R-E02", "hkapRaum": "PJ-2026-003/E1-S2" }""");
        Assert.Equal("PJ-2026-003/E1-S2", Kennung(p, "R-E02"));
        Nein(p, "hkap_raum_zuordnen", """{ "raum": "E.01", "hkapRaum": "E1-S2" }""");      // E.01 ist schon E1-S1
        Nein(p, "hkap_raum_zuordnen", """{ "raum": "R-E02", "hkapRaum": "E1-S1" }""");     // R-E02 ist schon E1-S2

        var raeume = Art(Abgleich(p), "raeume");
        Assert.Equal(new[] { "kicad.flaecheM2", "kicad.hoeheMm", "kicad.id" }, Felder(raeume, "nachHkap", "E1-S2").Order().ToArray());
        Assert.Contains(raeume["konflikte"]!.AsArray(), k => k!["schluessel"]!.GetValue<string>() == "raeume/E1-S2/bezeichnung");   // Bad ↔ Bad/WC: erste Begegnung

        var e = Ok(p, "hkap_senden");
        Assert.Contains(e.Hinweise, h => h.Contains("R-E02 → HKAP E1-S2: geändert"));
        var kicad = a.Raum("E1-S2")["kicad"]!;
        Assert.Equal(12.0, kicad["flaecheM2"]!.GetValue<double>());
        Assert.Equal(2500.0, kicad["hoeheMm"]!.GetValue<double>());
        Assert.Equal(p.Modell.Hole("R-E02").Id, kicad["id"]!.GetValue<string>());
        Assert.Equal("Bad/WC", a.Raum("E1-S2")["bezeichnung"]!.GetValue<string>());        // Konflikt: nicht gesendet
        var post = a.Posts("raeume").Single();
        Assert.Equal("false", post.Query["trockenlauf"]);
        Assert.Equal(HkapAttrappe.Pj, post.Query["projektierung"]);
        Assert.Null(post.Body!["datensaetze"]![0]!["felder"]!["bezeichnung"]);
        Assert.NotNull(post.Body["datensaetze"]![0]!["version"]);                           // mit erwarteter Version
        Assert.Empty(Felder(Art(Abgleich(p), "raeume"), "nachHkap", "E1-S2"));               // Basis nachgeführt

        // Fläche in KICAD geändert → nur die geht (mit dem ganzen kicad-Objekt)
        Ok(p, "element_aendern", """{ "element": "R-E02", "geometrie": { "art": "polygon", "punkte": [[0,0,0],[4000,0,0],[4000,4000,0],[0,4000,0]], "hoehe": 2500 } }""");
        Assert.Equal(new[] { "kicad.flaecheM2" }, Felder(Art(Abgleich(p), "raeume"), "nachHkap", "E1-S2").ToArray());
        Ok(p, "hkap_senden");
        Assert.Equal(16.0, a.Raum("E1-S2")["kicad"]!["flaecheM2"]!.GetValue<double>());
    }

    [Fact]
    public void RaumZuordnen_NichtPlatzierterRaumGehtImGezeichnetenAuf()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "hkap_uebernehmen");
        Ok(p, "element_anlegen", """{ "klasse": "Raum", "kennzeichen": "R-W", "name": "Wohnen", "geschoss": "EG", "geometrie": { "art": "polygon", "punkte": [[3000,0,0],[8000,0,0],[8000,4000,0],[3000,4000,0]], "hoehe": 2500 } }""");
        var e = Ok(p, "hkap_raum_zuordnen", """{ "raum": "R-W", "hkapRaum": "E1-S1" }""");
        Assert.Null(p.Modell.Finde("E.01"));                                       // Platzhalter aufgegangen
        Assert.Equal("PJ-2026-003/E1-S1", Kennung(p, "R-W"));
        Assert.Contains(e.Hinweise, h => h.Contains("E.01") && h.Contains("R-W"));
        var raeume = Art(Abgleich(p), "raeume");
        Assert.Contains("raumNummer", Felder(raeume, "nachKicad", "E1-S1"));       // KICAD leer, HKAP E.01 → übernehmen
        Ok(p, "hkap_uebernehmen");
        Assert.Equal("E.01", p.Modell.Hole("R-W").Text("nummer"));
    }

    // ---------------------------------------------------------------- Konflikte

    [Fact]
    public void Konflikt_BeidseitigGeaendertLandetMitBeidenFassungenInDerListe()
    {
        var p = Abgeglichen();
        Ok(p, "element_aendern", """{ "element": "E.01", "name": "Wohnen/Essen" }""");
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S1", f => f["bezeichnung"] = "Wohnzimmer");
        var k = Abgleich(p)["konflikte"]!.AsArray().Single(x => x!["schluessel"]!.GetValue<string>() == "raeume/E1-S1/bezeichnung")!;
        Assert.Equal("Wohnen/Essen", k["kicad"]!.GetValue<string>());
        Assert.Equal("Wohnzimmer", k["hkap"]!.GetValue<string>());
        Assert.Equal("Wohnen", k["basis"]!.GetValue<string>());
        Assert.Equal("E.01", k["element"]!.GetValue<string>());
        // ohne Wahl: weder übernommen noch gesendet
        Ok(p, "hkap_uebernehmen");
        Ok(p, "hkap_senden");
        Assert.Equal("Wohnen/Essen", p.Modell.Hole("E.01").Name);
        Assert.Equal("Wohnzimmer", a.Raum("E1-S1")["bezeichnung"]!.GetValue<string>());
    }

    [Fact]
    public void Konflikt_EntschiedenFuerHkapOderKicad()
    {
        var p = Abgeglichen();
        Ok(p, "element_aendern", """{ "element": "E.01", "name": "Wohnen/Essen" }""");
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S1", f => f["bezeichnung"] = "Wohnzimmer");
        Ok(p, "hkap_uebernehmen", """{ "konflikte": { "raeume/E1-S1/bezeichnung": "hkap" } }""");
        Assert.Equal("Wohnzimmer", p.Modell.Hole("E.01").Name);
        Assert.DoesNotContain(Abgleich(p)["konflikte"]!.AsArray(), x => x!["schluessel"]!.GetValue<string>() == "raeume/E1-S1/bezeichnung");

        Ok(p, "element_aendern", """{ "element": "E.01", "name": "Wohnküche" }""");
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S1", f => f["bezeichnung"] = "Stube");
        var e = Ok(p, "hkap_uebernehmen", """{ "konflikte": { "raeume/E1-S1/bezeichnung": "kicad", "raeume/E9-S9/name": "hkap" } }""");
        Assert.Contains(e.Hinweise, h => h.Contains("E9-S9") && h.Contains("nicht (mehr)"));
        Assert.Equal("Wohnküche", p.Modell.Hole("E.01").Name);
        Assert.Contains("bezeichnung", Felder(Art(Abgleich(p), "raeume"), "nachHkap", "E1-S1"));
        Ok(p, "hkap_senden");
        Assert.Equal("Wohnküche", a.Raum("E1-S1")["bezeichnung"]!.GetValue<string>());
        Assert.DoesNotContain(Abgleich(p)["konflikte"]!.AsArray(), x => x!["schluessel"]!.GetValue<string>().StartsWith("raeume/E1-S1/"));
        Assert.Contains("gibt es nicht", Nein(p, "hkap_uebernehmen", """{ "konflikte": { "raeume/E1-S1/bezeichnung": "beide" } }""").Fehler);
    }

    // ---------------------------------------------------------------- führende Seite

    [Fact]
    public void Senden_HkapGefuehrtesFeldWirdNieGesendet()
    {
        a.KatalogFeld("raeume", "nutzung", "beide", "hkap");
        var p = Abgeglichen();
        Ok(p, "element_aendern", """{ "element": "R-E02", "eigenschaften": { "nutzung": "Dusche" } }""");
        var raeume = Art(Abgleich(p), "raeume");
        Assert.DoesNotContain("nutzung", Felder(raeume, "nachHkap", "E1-S2"));
        var u = Eintraege(raeume, "nachKicad", "geaendert").Single(x => x["hkapId"]!.GetValue<string>() == "E1-S2")["felder"]!.AsArray().Single(f => f!["feld"]!.GetValue<string>() == "nutzung")!;
        Assert.True(u["ueberschreibt"]!.GetValue<bool>());

        Ok(p, "hkap_senden");
        var bodies = a.Posts("raeume").Select(x => x.Body!.ToJsonString()).ToList();
        Assert.NotEmpty(bodies);                                             // die Fläche ging …
        Assert.All(bodies, b => Assert.DoesNotContain("\"nutzung\"", b));   // … die HKAP-geführte Nutzung nie
        Assert.All(bodies, b => Assert.DoesNotContain("\"personen\"", b));  // Anforderungen ebenso
        Assert.Equal("Bad", a.Raum("E1-S2")["nutzung"]!.GetValue<string>());

        // Projektdaten führt HKAP: eine Plankopf-Änderung in KICAD wird übernommen statt gesendet
        Ok(p, "element_aendern", $$"""{ "element": "{{HkapVerknuepfung.Info(p.Modell)!.Kennzeichen}}", "eigenschaften": { "zeile1": "Eigener Titel" } }""");
        var projekt = Art(Abgleich(p), "projekt");
        Assert.Contains("zeile1", Felder(projekt, "nachKicad", "P-005"));
        var e = Ok(p, "hkap_senden", """{ "datenarten": ["projekt"] }""");
        Assert.Contains(e.Hinweise, h => h.Contains("führt HKAP"));
        Assert.Empty(a.Posts("projekt"));
        Ok(p, "hkap_uebernehmen", """{ "datenarten": ["projekt"] }""");
        Assert.Equal("MFH Musterweg", HkapVerknuepfung.Info(p.Modell)!.Text("zeile1"));
    }

    [Fact]
    public void Senden_VeralteteVersionGibtKonfliktUndUeberschreibtNichts()
    {
        var p = Abgeglichen();
        a.VorPost = (att, art) => { if (art == "raeume") { att.Raeume.Single(x => x.Id == "E1-S2").Felder["personen"] = 1; att.VorPost = null; } };
        var e = Ok(p, "hkap_senden");
        Assert.Contains(e.Hinweise, h => h.StartsWith("KONFLIKT R-E02"));
        Assert.Contains(e.Hinweise, h => h.Contains("1 Konflikte"));
        Assert.Null(a.Raum("E1-S2")["kicad"]);                                    // nichts überschrieben
        Assert.Contains("kicad.flaecheM2", Felder(Art(Abgleich(p), "raeume"), "nachHkap", "E1-S2"));   // Basis nicht nachgeführt
        Ok(p, "hkap_senden");                                                     // mit frischer Version
        Assert.Equal(12.0, a.Raum("E1-S2")["kicad"]!["flaecheM2"]!.GetValue<double>());
        Assert.Equal(1, a.Raum("E1-S2")["personen"]!.GetValue<int>());           // die HKAP-Änderung bleibt
    }

    [Fact]
    public void Senden_NeuesGeschossUndNeuerRaumBekommenHkapIdsAlsKennung()
    {
        var p = Abgeglichen();
        Ok(p, "element_anlegen", """{ "klasse": "Geschoss", "kennzeichen": "2.OG", "name": "2. Obergeschoss", "teilVon": "GEB-1", "eigenschaften": { "kote": 6000, "hoehe": 3000, "kurz": "2" } }""");
        Ok(p, "element_anlegen", """{ "klasse": "Raum", "kennzeichen": "R-201", "name": "Büro", "geschoss": "2.OG", "eigenschaften": { "nummer": "2.01", "nutzung": "Büro" }, "geometrie": { "art": "polygon", "punkte": [[0,0,6000],[5000,0,6000],[5000,4000,6000],[0,4000,6000]], "hoehe": 2600 } }""");
        var e = Ok(p, "hkap_senden");
        Assert.NotNull(e.PaketId);                                                // Kennungen → ein Paket
        Assert.Equal("PJ-2026-003/E3", Kennung(p, "2.OG"));
        Assert.Equal("PJ-2026-003/E3-S1", Kennung(p, "R-201"));
        var neu = a.Raum("E3-S1");
        Assert.Equal(("2.01", "Büro", "E3"), (neu["raumNummer"]!.GetValue<string>(), neu["bezeichnung"]!.GetValue<string>(), neu["etage"]!.GetValue<string>()));
        Assert.Equal(20.0, neu["kicad"]!["flaecheM2"]!.GetValue<double>());
        Assert.Equal(6000, a.Geschosse.Single(x => x.Id == "E3").Felder["koteMm"]!.GetValue<double>());
        var ab = Abgleich(p);
        Assert.Empty(Eintraege(Art(ab, "geschosse"), "nachHkap", "neu"));
        Assert.Empty(Eintraege(Art(ab, "raeume"), "nachHkap", "neu"));
    }

    [Fact]
    public void Senden_NichtPlatzierterRaumLeertKicadWerteInHkapNie()
    {
        // HKAP-Raum trägt kicad-Werte von anderswo (wie E1-S3 «test-1» in der Testkopie): als nicht platzierter Raum übernommen,
        // darf KICAD sie nicht mit «leer» überschreiben – erst ein gezeichneter Grundriss setzt sich durch (KICAD führt)
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S1", f => f["kicad"] = JsonNode.Parse("""{ "id": "test-1", "flaecheM2": 20.5, "hoeheMm": 2400 }"""));
        var p = Abgeglichen();
        Assert.Null(p.Modell.Hole("E.01").Geometrie);
        var raeume = Art(Abgleich(p), "raeume");
        Assert.Empty(Felder(raeume, "nachHkap", "E1-S1"));
        Assert.Contains(raeume["abweichend"]!.AsArray(), x => x!["hkapId"]!.GetValue<string>() == "E1-S1" && x["feld"]!.GetValue<string>() == "kicad.flaecheM2");
        Ok(p, "hkap_senden");
        Assert.Equal("test-1", a.Raum("E1-S1")["kicad"]!["id"]!.GetValue<string>());

        Ok(p, "element_aendern", """{ "element": "E.01", "geometrie": { "art": "polygon", "punkte": [[3000,0,0],[8000,0,0],[8000,4000,0],[3000,4000,0]], "hoehe": 2500 } }""");
        var s = Eintraege(Art(Abgleich(p), "raeume"), "nachHkap", "geaendert").Single(x => x["hkapId"]!.GetValue<string>() == "E1-S1")["felder"]!.AsArray();
        Assert.True(s.Single(f => f!["feld"]!.GetValue<string>() == "kicad.flaecheM2")!["ueberschreibt"]!.GetValue<bool>());
        Ok(p, "hkap_senden");
        Assert.Equal(20.0, a.Raum("E1-S1")["kicad"]!["flaecheM2"]!.GetValue<double>());
        Assert.Equal(p.Modell.Hole("E.01").Id, a.Raum("E1-S1")["kicad"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Zuordnung_UeberKicadIdOhneBasisUndKennung()
    {
        var p = NeuesProjekt();
        var id = p.Modell.Hole("R-E02").Id;
        a.Aendern($"{HkapAttrappe.Pj}|raeume", "E1-S2", f => f["kicad"] = new JsonObject { ["id"] = id, ["flaecheM2"] = 12.0, ["hoeheMm"] = 2500.0 });
        Verknuepfen(p);
        var raeume = Art(Abgleich(p), "raeume");
        Assert.Contains(raeume["zuordnungen"]!.AsArray(), x => x!["element"]!.GetValue<string>() == "R-E02" && x["hkapId"]!.GetValue<string>() == "E1-S2");
        Assert.Null(raeume["zuordnungsvorschlaege"]);
        Ok(p, "hkap_uebernehmen", """{ "konflikte": { "raeume/E1-S2/bezeichnung": "hkap" } }""");
        Assert.Equal("PJ-2026-003/E1-S2", Kennung(p, "R-E02"));
        Assert.Equal("Bad/WC", p.Modell.Hole("R-E02").Name);
        Assert.Null(p.Modell.Finde("E.02"));                                       // kein Platzhalter daneben
    }

    [Fact]
    public void Vorschau_ZeigtHkapPlanflaecheNebenKicadFlaeche()
    {
        var p = Abgeglichen();
        var lesen = Art(Abgleich(p), "raeume")["nurLesen"]!.AsArray().Single(x => x!["hkapId"]!.GetValue<string>() == "E1-S2")!;
        Assert.Equal(11.8, lesen["werte"]!["flaecheHkapM2"]!.GetValue<double>());
        Assert.Equal(12.0, lesen["flaecheKicadM2"]!.GetValue<double>());
        Assert.Null(p.Modell.Hole("R-E02").Eigenschaft("flaecheHkapM2"));          // nie übernommen
        Assert.All(a.Posts("raeume").Select(x => x.Body!.ToJsonString()), b => Assert.DoesNotContain("flaecheHkapM2", b));
    }

    // ---------------------------------------------------------------- HKAP nicht erreichbar

    [Fact]
    public void NichtErreichbar_KlareMeldungUndNichtsGeaendert()
    {
        var p = Abgeglichen();
        var summe = p.Modell.Pruefsumme();
        var basisVorher = Json.Text(HkapZustand.Von(p).Lesen(HkapVerknuepfung.Lesen(p.Modell)!));
        a.Dispose();
        var ab = Abgleich(p);
        Assert.False(ab["erreichbar"]!.GetValue<bool>());
        Assert.Contains("nicht erreichbar", ab["meldung"]!.GetValue<string>());
        foreach (var befehl in new[] { "hkap_uebernehmen", "hkap_senden", "hkap_befunde_senden" })
        {
            var e = Nein(p, befehl);
            Assert.Contains("nicht erreichbar", e.Fehler);
            Assert.Contains("HKAP starten", e.Vorschlag);
        }
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Equal(basisVorher, Json.Text(HkapZustand.Von(p).Lesen(HkapVerknuepfung.Lesen(p.Modell)!)));
        var v = (JsonObject)Tuer.Abfrage(p, "hkap_verbindung");
        Assert.False(v["erreichbar"]!.GetValue<bool>());
        Assert.Equal(a.Url, v["url"]!.GetValue<string>());
    }

    [Fact]
    public void Klient_ZeitlimitFehlendeAustauschstelleUndUngueltigeAdresse()
    {
        a.Verzoegerung = TimeSpan.FromSeconds(2);
        var k = new HkapKlient(a.Url, TimeSpan.FromMilliseconds(300));
        var f = Assert.Throws<HlksFehler>(() => k.Katalog());
        Assert.Contains("antwortet nicht", f.Message);
        Assert.False(k.Erreichbar);
        a.Verzoegerung = TimeSpan.Zero;
        a.OhneAustauschstelle = true;
        var k2 = new HkapKlient(a.Url);
        Assert.Contains("keine Austauschstelle", Assert.Throws<HlksFehler>(() => k2.Katalog()).Vorschlag);
        Assert.True(k2.Erreichbar);
        Assert.Throws<HlksFehler>(() => new HkapKlient("ftp://localhost:5013"));
        var tot = new HkapKlient(TotesHkap());
        Assert.Contains("nicht erreichbar", Assert.Throws<HlksFehler>(() => tot.Katalog()).Message);
        Assert.Equal("http://x:1", new HkapKlient(HkapKlient.UrlFuer("http://x:1/", "http://y:2")).Url);
        Assert.Equal("http://y:2", HkapKlient.UrlFuer(null, "http://y:2"));
    }

    [Fact]
    public void NichtVerknuepft_AbgleichSagtWieWeiter()
    {
        var p = NeuesProjekt();
        var o = Abgleich(p);
        Assert.False(o["verknuepft"]!.GetValue<bool>());
        Assert.Contains("hkap_verknuepfen", o["vorschlag"]!.GetValue<string>());
        Assert.Contains("hkap_verknuepfen", Nein(p, "hkap_uebernehmen").Vorschlag);
        Assert.Empty(a.Anfragen);                                             // ohne Verknüpfung kein Aufruf
    }

    // ---------------------------------------------------------------- Prüfbefunde → Pendenzen

    [Fact]
    public void Befunde_WerdenPendenzenErneutOhneDoppelBehobeneMitVorschlag()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "element_anlegen", """{ "klasse": "Strang", "kennzeichen": "ST-1", "name": "Strang ohne System" }""");   // P09 fehler
        var befund = Pruefungen.Alle(p.Modell, Kat).Single(b => b.Schwere == "fehler");
        var tl = Ok(p, "hkap_befunde_senden", trockenlauf: true);
        Assert.Single(a.Pendenzen);
        Assert.Contains(tl.Hinweise, h => h.Contains("Trockenlauf"));

        Ok(p, "hkap_befunde_senden");
        var eigene = a.Pendenzen.Where(x => x.Felder["quelle"]?.GetValue<string>() == "KICAD").ToList();
        var pd = Assert.Single(eigene).Felder;
        var kennung = HkapPendenzen.Kurzkennung(p.Kopf);
        Assert.Equal(8, kennung.Length);
        Assert.StartsWith(kennung, p.Kopf.Id.Replace("-", ""));             // stabil aus der Projekt-Id (projekt.json)
        Assert.Equal($"{kennung}|{befund.Schluessel}", pd["quelleId"]!.GetValue<string>());
        Assert.Equal("hoch", pd["prioritaet"]!.GetValue<string>());
        Assert.Equal("offen", pd["status"]!.GetValue<string>());
        Assert.Contains("ST-1", pd["aufgabe"]!.GetValue<string>());

        var zwei = Ok(p, "hkap_befunde_senden");                             // erneut: kein Doppel
        Assert.Equal(2, a.Pendenzen.Count);
        Assert.Contains("1 schon aktuell", zwei.Hinweise[0]);

        Ok(p, "element_loeschen", """{ "element": "ST-1" }""");              // behoben
        var drei = Ok(p, "hkap_befunde_senden");
        Assert.Equal(2, a.Pendenzen.Count);                                  // nie gelöscht
        pd = a.Pendenzen.Single(x => x.Felder["quelle"]?.GetValue<string>() == "KICAD").Felder;
        Assert.StartsWith(HkapPendenzen.Marke, pd["bemerkung"]!.GetValue<string>());
        Assert.Equal("offen", pd["status"]!.GetValue<string>());            // Status führt HKAP
        Assert.Contains("1 behoben", drei.Hinweise[0]);
        var bemerkung = pd["bemerkung"]!.GetValue<string>();
        Ok(p, "hkap_befunde_senden");
        Assert.Equal(bemerkung, a.Pendenzen.Single(x => x.Felder["quelle"]?.GetValue<string>() == "KICAD").Felder["bemerkung"]!.GetValue<string>());
        Assert.Empty(a.Posts("pendenzen").Skip(3));                          // viertes Senden: nichts mehr zu schreiben
        var vorschau = Art(Abgleich(p), "pendenzen");
        Assert.Single(vorschau["erledigtVorschlag"]!.AsArray());
    }

    [Fact]
    public void Befunde_PendenzenEinesAnderenKicadProjektsBleibenUnberuehrt()
    {
        const string strang = "P09|ST-1|Strang ist nicht teilVon eines Systems";
        lock (a.Sperre)
        {
            // wie in der Testkopie: ohne Projektkennung, nicht von hier gesendet …
            a.Pendenzen.Add(new HkapAttrappe.Satz { Id = "PD-077", Felder = J("""{ "id": "PD-077", "aufgabe": "P02 Leitungsbauteil ohne System R-14", "status": "offen", "bemerkung": "", "quelle": "KICAD", "quelleId": "P02|R-14" }""") });
            // … und mit der Kennung eines anderen KICAD-Projekts zum selben Befund, den auch dieses Projekt hat
            a.Pendenzen.Add(new HkapAttrappe.Satz { Id = "PD-078", Felder = J($$"""{ "id": "PD-078", "aufgabe": "fremd", "status": "offen", "bemerkung": "", "quelle": "KICAD", "quelleId": "abcdef12|{{strang}}" }""") });
        }
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "element_anlegen", """{ "klasse": "Strang", "kennzeichen": "ST-1" }""");
        var e = Ok(p, "hkap_befunde_senden");
        Assert.Equal("", a.Pendenzen.Single(x => x.Id == "PD-077").Felder["bemerkung"]!.GetValue<string>());
        Assert.Equal("fremd", a.Pendenzen.Single(x => x.Id == "PD-078").Felder["aufgabe"]!.GetValue<string>());
        Assert.Contains(a.Pendenzen, x => x.Felder["quelleId"]?.GetValue<string>() == $"{HkapPendenzen.Kurzkennung(p.Kopf)}|{strang}");   // eigene, nicht die fremde
        Assert.Contains(e.Hinweise, h => h.Contains("2 KICAD-Pendenz(en) gehören nicht zu diesem Projekt"));
        Ok(p, "element_loeschen", """{ "element": "ST-1" }""");
        Ok(p, "hkap_befunde_senden");                                         // eigene behoben, fremde nie
        Assert.Equal("", a.Pendenzen.Single(x => x.Id == "PD-078").Felder["bemerkung"]!.GetValue<string>());
        Assert.Equal(2, Art(Abgleich(p), "pendenzen")["fremd"]!.GetValue<int>());
    }

    [Fact]
    public void Befunde_UebergangEigenePendenzOhneKennungBekommtSie()
    {
        const string strang = "P09|ST-1|Strang ist nicht teilVon eines Systems";
        lock (a.Sperre) a.Pendenzen.Add(new HkapAttrappe.Satz { Id = "PD-050", Felder = J($$"""{ "id": "PD-050", "aufgabe": "P09 ST-1: Strang ist nicht teilVon eines Systems", "status": "offen", "bemerkung": "teilVon auf das System setzen", "raum": "", "quelle": "KICAD", "quelleId": "{{strang}}" }""") });
        var p = NeuesProjekt();
        Verknuepfen(p);
        // früher von diesem Projekt gesendet: steht im eigenen Basisstand
        HkapZustand.Von(p).Anwenden(HkapVerknuepfung.Lesen(p.Modell)!, [new HkapBasisAenderung("pendenzen", "PD-050", new HkapBasisSatz
            { Version = "alt", Felder = J("""{ "aufgabe": "P09 ST-1: Strang ist nicht teilVon eines Systems", "bemerkung": "teilVon auf das System setzen", "raum": "" }""") })]);
        Ok(p, "element_anlegen", """{ "klasse": "Strang", "kennzeichen": "ST-1" }""");
        var e = Ok(p, "hkap_befunde_senden");
        Assert.Equal(2, a.Pendenzen.Count);                                   // kein Doppel
        Assert.Equal($"{HkapPendenzen.Kurzkennung(p.Kopf)}|{strang}", a.Pendenzen.Single(x => x.Id == "PD-050").Felder["quelleId"]!.GetValue<string>());
        Assert.Contains(e.Hinweise, h => h.Contains("Übergang"));
        Ok(p, "element_loeschen", """{ "element": "ST-1" }""");
        Ok(p, "hkap_befunde_senden");
        Assert.StartsWith(HkapPendenzen.Marke, a.Pendenzen.Single(x => x.Id == "PD-050").Felder["bemerkung"]!.GetValue<string>());
    }

    [Fact]
    public void Befunde_AbSchwereWarnungMitRaum()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "element_anlegen", """{ "klasse": "Raum", "kennzeichen": "R-X", "name": "Abstellraum", "eigenschaften": { "nummer": "9.99" } }""");   // P11 warnung
        Ok(p, "hkap_befunde_senden");
        Assert.Single(a.Pendenzen);                                          // Vorgabe fehler: Warnung geht nicht
        Ok(p, "hkap_befunde_senden", """{ "schwere": "warnung" }""");
        var pd = a.Pendenzen.Single(x => x.Felder["quelle"]?.GetValue<string>() == "KICAD").Felder;
        Assert.Equal("9.99 Abstellraum", pd["raum"]!.GetValue<string>());
        Assert.Equal("mittel", pd["prioritaet"]!.GetValue<string>());
        Assert.Contains("Schwere", Nein(p, "hkap_befunde_senden", """{ "schwere": "kritisch" }""").Fehler);
    }

    // ---------------------------------------------------------------- Rückgängig, Basis, Trockenlauf

    [Fact]
    public void Rueckgaengig_DerUebernahmeStelltModellUndBasisZurueck()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        Ok(p, "hkap_uebernehmen");
        Assert.NotNull(p.Modell.Finde("E.01"));
        Assert.DoesNotContain(Eintraege(Art(Abgleich(p), "raeume"), "nachKicad", "neu"), x => x["hkapId"]!.GetValue<string>() == "E1-S1");

        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Null(p.Modell.Finde("E.01"));
        Assert.Null(p.Modell.Finde("1.OG"));
        Assert.Null(HkapVerknuepfung.Info(p.Modell)!.Text("zeile1"));
        Assert.Contains(Eintraege(Art(Abgleich(p), "raeume"), "nachKicad", "neu"), x => x["hkapId"]!.GetValue<string>() == "E1-S1");   // Basis zurück

        Assert.True(p.Wiederholen().Angenommen);
        Assert.NotNull(p.Modell.Finde("E.01"));
        Assert.DoesNotContain(Eintraege(Art(Abgleich(p), "raeume"), "nachKicad", "neu"), x => x["hkapId"]!.GetValue<string>() == "E1-S1");
    }

    [Fact]
    public void Basis_NachErfolgGeschriebenNachFehlerNicht()
    {
        var ordner = Path.Combine(wurzel, "Projekt Müller", "c_Pläne und Modelle", "e_KICAD");
        var p = NeuesProjekt(ordner, "pc-a");
        Verknuepfen(p);
        var datei = Path.Combine(ordner, "austausch", "hkap.json");
        Assert.False(File.Exists(datei));

        // Paket scheitert (EG wird gerade von jemand anderem bearbeitet) → keine Basis
        var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Andere", "pc-b");
        Ok(b, "element_aendern", """{ "element": "EG", "eigenschaften": { "hoehe": 3100 } }""");
        var gesperrt = Nein(p, "hkap_uebernehmen");
        Assert.NotEmpty(gesperrt.Gesperrt);
        Assert.False(File.Exists(datei));

        // nach der Freigabe: Paket gespeichert → Basis geschrieben
        b.Freigeben();
        p.Synchronisieren();
        Ok(p, "hkap_uebernehmen");
        Assert.True(File.Exists(datei));
        var inhalt = File.ReadAllText(datei);
        Assert.Contains("E1-S1", inhalt);
        Assert.Contains("PJ-2026-003", inhalt);

        // HKAP fällt aus → Befehle abgelehnt, Basis unverändert
        var vorher = File.ReadAllBytes(datei);
        a.Dispose();
        Nein(p, "hkap_uebernehmen");
        Nein(p, "hkap_senden");
        Assert.Equal(vorher, File.ReadAllBytes(datei));
        p.Freigeben();
    }

    [Fact]
    public void Trockenlauf_UebernehmenUndSendenAendernWederKicadNochHkapNochBasis()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        var summe = p.Modell.Pruefsumme();
        var tl = Ok(p, "hkap_uebernehmen", trockenlauf: true);
        Assert.True(tl.Trockenlauf);
        Assert.Contains(tl.Aenderungen, x => x.Art == "angelegt" && x.Kennzeichen == "E.01");
        Assert.Equal(summe, p.Modell.Pruefsumme());
        Assert.Contains(Eintraege(Art(Abgleich(p), "raeume"), "nachKicad", "neu"), x => x["hkapId"]!.GetValue<string>() == "E1-S1");

        Ok(p, "hkap_uebernehmen");
        Ok(p, "hkap_raum_zuordnen", """{ "raum": "R-E02", "hkapRaum": "E1-S2" }""");
        var vorher = a.Raum("E1-S2").ToJsonString();
        var s = Ok(p, "hkap_senden", trockenlauf: true);
        Assert.Contains(s.Hinweise, h => h.StartsWith("Trockenlauf"));
        Assert.Equal("true", a.Posts("raeume").Single().Query["trockenlauf"]);
        Assert.Equal(vorher, a.Raum("E1-S2").ToJsonString());
        Assert.Contains("kicad.flaecheM2", Felder(Art(Abgleich(p), "raeume"), "nachHkap", "E1-S2"));   // Basis unverändert
    }

    [Fact]
    public void Gegenpruefung_WiederkehrendeIdWirdAbgelehnt_UmbenennungNurGewarnt()
    {
        var p = NeuesProjekt();
        Verknuepfen(p);
        lock (a.Sperre) { a.Projekte["P-005"]["kuerzel"] = "NEU"; a.Projekte["P-005"]["bezeichnung"] = "Ganz anderes Projekt"; }
        var f = Nein(p, "hkap_uebernehmen");
        Assert.Contains("wiederkehrende Id", f.Fehler);
        Assert.NotEmpty(Abgleich(p)["warnungen"]!.AsArray());

        lock (a.Sperre) a.Projekte["P-005"]["kuerzel"] = "MUS";             // nur umbenannt
        var e = Ok(p, "hkap_uebernehmen");
        Assert.Contains(e.Hinweise, h => h.Contains("Verknüpfung prüfen"));
        Assert.Equal("Ganz anderes Projekt", HkapVerknuepfung.Lesen(p.Modell)!.Name);   // Gegenprüfung nachgeführt
        Assert.Equal("Ganz anderes Projekt", HkapVerknuepfung.Info(p.Modell)!.Text("zeile1"));
    }
}
