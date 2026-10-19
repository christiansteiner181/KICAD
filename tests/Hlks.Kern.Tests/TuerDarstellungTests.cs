using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Die Kern-Tür (Tuer.Abfrage, Tuer.Befehl) und die Darstellungsdaten für Ansichten – ohne Server.</summary>
public class TuerDarstellungTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static Projekt Beispiel() => Beispielprojekt.Erzeugen(Kat, null);

    static JsonObject Par(string json) => JsonNode.Parse(json)!.AsObject();

    /// <summary>Sinnvolle Parameter je Abfrage des Beispielprojekts.</summary>
    // Vor der Tabelle deklarieren: statische Felder werden in Textreihenfolge belegt (sonst steht in plan_pdf ein leerer Pfad)
    static readonly string PlanPdfDatei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "tuer-test-" + Guid.NewGuid().ToString("N")[..8] + ".pdf").Replace('\\', '/');
    static readonly string PlansatzOrdner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "tuer-plansatz-" + Guid.NewGuid().ToString("N")[..8]).Replace('\\', '/');
    static readonly Dictionary<string, string> AbfrageParameter = new()
    {
        ["faehigkeiten"] = "{}",
        ["projekt_ueberblick"] = "{}",
        ["element_lesen"] = """{"element":"WT-E.02"}""",
        ["elemente_suchen"] = """{"klasse":"Apparat","geschoss":"EG","seite":1,"proSeite":5}""",
        ["system_lesen"] = """{"system":"SW"}""",
        ["strang_text"] = """{"strang":"SW-F2"}""",
        ["netz_folgen"] = """{"von":"MV-1","richtung":"stromab","medium":"KW"}""",
        ["raum_inhalt"] = """{"raum":"E.02"}""",
        ["mengen"] = """{"system":"SW"}""",
        ["raumliste"] = """{"geschoss":"1.OG"}""",
        ["strangliste"] = "{}",
        ["pruefen"] = "{}",
        ["warum"] = """{"element":"WT-E.02","eigenschaft":"raum"}""",
        ["aenderungen"] = """{"anzahl":5}""",
        ["katalog"] = "{}",
        ["datenpunkte"] = "{}",
        ["zeitreihe"] = """{"datenpunkt":"DP-WZ1-VOL","von":"2026-10-19T00:00","bis":"2026-10-20T00:00","aufloesungMinuten":120}""",
        ["plan_pdf"] = "{\"planblatt\":\"PB-1\",\"datei\":\"" + PlanPdfDatei + "\"}",   // Planblatt legt der Test vorher an
        ["ifc_pruefen"] = "{\"datei\":\"" + IfcHilfe.Testdaten("minimal_ifc4.ifc").Replace('\\', '/') + "\"}",
        ["aufbau_rechnen"] = """{"aufbau":"AUF-1"}""",   // Aufbau legt der Test vorher an (das Beispielprojekt hat keinen)
        ["materialien"] = "{}",
        ["huelle_pruefen"] = "{}",
        ["hkap_verbindung"] = """{"url":"http://127.0.0.1:9"}""",   // Modul 1d: nie die echte HKAP (nicht erreichbar = Antwort)
        ["hkap_abgleich"] = "{}",                                    // nicht verknüpft = Antwort
        ["raumgrenzen"] = """{"geschoss":"EG"}""",                   // Modul 1c, B1
        ["huellbauteile"] = "{}",
        ["familien"] = "{}",                                         // Modul 2, B1: Testfamilie (fiktiv) legt der Test vorher an
        ["familie"] = """{"familie":"FAM-1"}""",
        ["anschlusslagen"] = """{"element":"HK-T1"}""",
        ["familie_ansicht"] = """{"familie":"FAM-1","typ":"22-600-1000","ansicht":"vorne","detailgrad":"mittel"}""",
        ["familien_bibliothek"] = "{}",
        ["unterlagen_lesen"] = "{\"unterlagen\":[\"" + IfcHilfe.Testdaten("unterlagen/beispiel.pdf").Replace("\\", "/") + "\"]}",   // Modul 2, B2: kleines Beispiel-PDF
        ["befunde"] = "{\"unterlage\":\"" + IfcHilfe.Testdaten("unterlagen/beispiel.pdf").Replace("\\", "/") + "\",\"art\":\"wort\"}",
        ["unterlage_bild"] = "{\"unterlage\":\"" + IfcHilfe.Testdaten("unterlagen/beispiel.pdf").Replace("\\", "/") + "\",\"breite\":200}",
        ["familie_masse"] = """{"familie":"FAM-1"}""",                    // Modul 2, B4: Testfamilie ohne Massbezüge = leere Liste mit Hinweis
        ["familie_vergleichen"] = "{\"familie\":\"FAM-1\",\"typ\":\"22-600-1000\",\"ansicht\":\"vorne\",\"breite\":300,\"unterlage\":\"" + IfcHilfe.Testdaten("unterlagen/beispiel.pdf").Replace("\\", "/")
            + "\",\"passpunkte\":[{\"familie\":[0,0],\"unterlage\":[100,100]},{\"familie\":[1000,0],\"unterlage\":[300,100]}]}",
        ["leitungsserie"] = """{"familie":"fam-test-rohrserie"}""",   // Modul 3, B1: fiktive Testrohrserie legt der Test vorher an
        ["schema"] = """{"ansicht":"AN-SCHEMA-SAN"}""",                  // Modul 3, B3: Schema-Ansicht des Beispielprojekts
        ["materialliste"] = """{"system":"SW"}""",                      // Modul 3, B4
        ["leitungszug"] = """{"leitung":"KW-V1-3"}""",                  // Modul 3, B2: Zug einer Polylinie des Beispielprojekts
        ["verlegung"] = """{"art":"einlage"}""",                         // Modul 3, Nachtrag B8c: Beispielprojekt ohne Decken = keine Einlagen
        ["bcf_export"] = "{\"datei\":\"" + BcfDatei() + "\"}",          // Modul 3b, B6: Kollision legt der Test vorher an
        ["bcf_lesen"] = "{\"datei\":\"" + BcfDatei() + "\"}",           // Modul 3b, B6: Datei schreibt der Test vorher mit bcf_export
        ["vorgaben"] = "{}",                                            // Modul 3b, B2: ohne Vorgaben = Antwort mit fehlenden Werten
        ["kollisionen"] = """{"status":"neu"}""",                       // Modul 3b, B2
        ["typmaterial"] = """{"betroffen":false}""",                    // Modul 3b, Nachtrag B3d: leere Typenliste = Antwort mit Hinweis
        ["aussparungsliste"] = """{"geschoss":"EG"}""",                 // Modul 3b, B3 (der Test legt vorher eine Aussparung an: siehe aussparungen_ifc)
        ["aussparungen_ifc"] = "{\"datei\":\"" + AussparungIfcDatei() + "\"}",   // Modul 3b, B4b: freigegebene Testaussparung (fiktiv) legt der Test vorher an
        ["plansaetze"] = "{}",                                          // Serien-PDF
        ["plansatz_pdf"] = "{\"plansatz\":\"allgemein\",\"ordner\":\"" + PlansatzOrdner + "\",\"ueberschreiben\":true}",   // Planblatt T-1 (ohne Fachbuchstabe = allgemein) legt der Test vorher an
    };

    static string AussparungIfcDatei() => Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"tuer-test-{Environment.ProcessId}-aussparungen.ifc").Replace('\\', '/');

    static string BcfDatei() => Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"tuer-test-{Environment.ProcessId}.bcf").Replace('\\', '/');

    // ------------------------------------------------------------------ Abfragen

    [Fact]
    public void Abfragen_ParameterTabelleDecktGenauDieListeAb()
    {
        Assert.Equal(Abfragen.Liste.Select(a => a.Name).Order(), AbfrageParameter.Keys.Order());
    }

    [Fact]
    public void Abfragen_JedeAusDerListeLaesstSichUeberDieTuerAufrufen()
    {
        var p = Beispiel();
        Assert.True(p.Ausfuehren("planblatt_anlegen", Par("""{"nummer":"T-1","titel":"Test"}""")).Angenommen);   // für plan_pdf
        Assert.True(p.Ausfuehren("material_anlegen", Par("""{"name":"Testmaterial","werte":{"lambda":{"wert":0.5,"quelle":"Testwert"}}}""")).Angenommen);   // für aufbau_rechnen (Testwert)
        Assert.True(p.Ausfuehren("aufbau_anlegen", Par("""{"name":"Testaufbau","art":"wand","schichten":[{"material":"MAT-1","dicke":200}]}""")).Angenommen);
        Assert.True(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }).Angenommen);   // für die Familienabfragen (Modul 2)
        Assert.True(p.Ausfuehren("familie_platzieren", Par("""{"familie":"FAM-1","typ":"22-600-1000","punkt":[5000,3000],"geschoss":"EG","kennzeichen":"HK-T1"}""")).Angenommen);
        Assert.True(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Modul3Hilfe.Datei("test_rohrserie") }).Angenommen);   // für leitungsserie (Modul 3)
        Assert.True(p.Ausfuehren("element_anlegen", Par("""{"klasse":"Kollision","kennzeichen":"KOL-T1","eigenschaften":{"art":"hart","beteiligte":["WT-E.02"],"mass":10},"geometrie":{"art":"punkt","punkte":[[1000,1000,500]]}}""")).Angenommen);   // für bcf_export, bcf_lesen (Modul 3b, B6)
        Tuer.Abfrage(p, "bcf_export", Par(AbfrageParameter["bcf_export"]));
        Assert.True(p.Ausfuehren("element_anlegen", Par("""{"klasse":"Aussparung","kennzeichen":"AUS-T1","geschoss":"EG","eigenschaften":{"form":"rund","d":95,"tiefe":200,"achse":[0,1,0],"art":"kernbohrung","status":"freigegeben","nummer":"A-EG-001"},"geometrie":{"art":"punkt","punkte":[[1000,1000,1000]]}}""")).Angenommen);   // für aussparungen_ifc (Modul 3b, B4b; Testwerte)
        try
        {
        foreach (var a in Abfragen.Liste)
        {
            var par = Par(AbfrageParameter[a.Name]);
            object ergebnis;
            try { ergebnis = Tuer.Abfrage(p, a.Name, par); }
            catch (HlksFehler f) when (f.Message.Contains("gibt es nicht")) { throw new Xunit.Sdk.XunitException($"{a.Name} fehlt im switch der Tür: {f.Message}"); }
            Assert.NotNull(ergebnis);
            if (ergebnis is string text) Assert.False(string.IsNullOrWhiteSpace(text), $"{a.Name}: leerer Text");
            else Assert.False(string.IsNullOrWhiteSpace(Hlks.Kern.Json.Text(ergebnis)), $"{a.Name}: leeres Ergebnis");
        }
        }
        finally { File.Delete(PlanPdfDatei); File.Delete(BcfDatei()); File.Delete(AussparungIfcDatei()); try { Directory.Delete(PlansatzOrdner, true); } catch (IOException) { } }   // Ausgabe von plan_pdf, bcf_export, aussparungen_ifc und plansatz_pdf aufräumen
    }

    [Fact]
    public void Abfragen_TextansichtenKommenAlsText()
    {
        var p = Beispiel();
        Assert.IsType<string>(Tuer.Abfrage(p, "system_lesen", Par("""{"system":"SW"}""")));
        Assert.IsType<string>(Tuer.Abfrage(p, "strang_text", Par("""{"strang":"SW-F2"}""")));
        Assert.IsNotType<string>(Tuer.Abfrage(p, "mengen", null));
    }

    [Fact]
    public void Abfragen_OhneParameterObjektGenuegtWennNichtsPflicht()
    {
        var p = Beispiel();
        Assert.NotNull(Tuer.Abfrage(p, "projekt_ueberblick"));
        Assert.NotNull(Tuer.Abfrage(p, "pruefen", null));
    }

    [Fact]
    public void Abfragen_FehlenderPflichtparameterGibtHlksFehler()
    {
        var p = Beispiel();
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "element_lesen", new JsonObject()));
        Assert.Contains("element", f.Message);
    }

    [Fact]
    public void Abfragen_UnbekannterNameGibtFehlerMitListeImVorschlag()
    {
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(Beispiel(), "gibt_es_nicht"));
        Assert.Contains("gibt_es_nicht", f.Message);
        Assert.NotNull(f.Vorschlag);
        foreach (var a in Abfragen.Liste) Assert.Contains(a.Name, f.Vorschlag!);
    }

    // ------------------------------------------------------------------ Befehle

    [Fact]
    public void Befehl_ElementAendernUndTrockenlauf()
    {
        var p = Beispiel();
        var start = p.Modell.Pruefsumme();
        var par = Par("""{"element":"E.02","name":"Neuer Name"}""");

        var tl = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "element_aendern", par, trockenlauf: true));
        Assert.True(tl.Angenommen, tl.Fehler);
        Assert.True(tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());

        var echt = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "element_aendern", par));
        Assert.True(echt.Angenommen, echt.Fehler);
        Assert.False(echt.Trockenlauf);
        Assert.NotNull(echt.PaketId);
        Assert.Equal("Neuer Name", p.Modell.Hole("E.02").Name);
        Assert.NotEqual(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Befehl_RueckgaengigUndWiederholen()
    {
        var p = Beispiel();
        Tuer.Befehl(p, "element_aendern", Par("""{"element":"E.02","name":"Neuer Name"}"""));
        var geaendert = p.Modell.Pruefsumme();

        var tl = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "rueckgaengig", null, trockenlauf: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(geaendert, p.Modell.Pruefsumme());

        var zurueck = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "rueckgaengig"));
        Assert.True(zurueck.Angenommen, zurueck.Fehler);
        Assert.NotEqual("Neuer Name", p.Modell.Hole("E.02").Name);

        var wieder = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "wiederholen"));
        Assert.True(wieder.Angenommen, wieder.Fehler);
        Assert.Equal("Neuer Name", p.Modell.Hole("E.02").Name);
    }

    [Fact]
    public void Befehl_AbgelehnterBefehlIstErgebnisMitVorschlagKeineAusnahme()
    {
        var p = Beispiel();
        var r = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "element_aendern", Par("""{"element":"E.02","eigenschaften":{"gibtEsNicht":1}}""")));
        Assert.False(r.Angenommen);
        Assert.False(string.IsNullOrWhiteSpace(r.Fehler));
        Assert.False(string.IsNullOrWhiteSpace(r.Vorschlag));
        var unbekannt = Assert.IsType<BefehlsErgebnis>(Tuer.Befehl(p, "gibt_es_nicht", new JsonObject()));
        Assert.False(unbekannt.Angenommen);
    }

    [Fact]
    public void Befehl_SynchronisierenOhneSpeicherHatNullPakete()
    {
        var s = Assert.IsType<SyncErgebnis>(Tuer.Befehl(Beispiel(), "synchronisieren"));
        Assert.Equal(0, s.NeuePakete);
        Assert.Empty(s.Konflikte);
    }

    // ------------------------------------------------------------------ Darstellung

    static JsonObject Dar(Projekt p, string kennzeichen) => Darstellung.Element(p, p.Modell.Hole(kennzeichen));

    [Fact]
    public void Darstellung_RohrHatMediumFarbeLinieKennzeichenUndWerte()
    {
        var p = Beispiel();
        var rohr = p.Modell.DerKlasse("Rohr").First(r => r.System is not null && r.TeilVon is not null && Netz.SystemMedium(p.Modell, r.System) == "SW" && r.Zahl("dn") is not null);
        var o = Darstellung.Element(p, rohr);
        Assert.Equal("Rohr", o["klasse"]!.GetValue<string>());
        Assert.Equal("SW", o["medium"]!.GetValue<string>());
        var md = Kat.Medien["SW"];
        Assert.Equal(md.FarbeHex, o["farbe"]!.GetValue<string>());
        Assert.Matches("^#[0-9a-fA-F]{6}$", o["farbe"]!.GetValue<string>());
        Assert.Equal(md.Linie, o["linie"]!.GetValue<string>());
        Assert.Equal("leitung", o["rolle"]!.GetValue<string>());
        Assert.Equal("SW", o["system"]!.GetValue<string>());                    // Kennzeichen, nicht Id
        Assert.Equal(p.Modell.Finde(rohr.TeilVon!)!.Kennzeichen, o["teilVon"]!.GetValue<string>());
        Assert.NotEqual(rohr.TeilVon, o["teilVon"]!.GetValue<string>());
        Assert.Equal(rohr.Zahl("dn"), o["werte"]!["dn"]!.GetValue<double>());
        Assert.Equal("polylinie", o["geometrie"]!["art"]!.GetValue<string>());
    }

    [Fact]
    public void Darstellung_WcIstVerbraucherMitRaumUndGeschoss()
    {
        var o = Dar(Beispiel(), "WC-E.02");
        Assert.Equal("verbraucher", o["rolle"]!.GetValue<string>());
        Assert.Equal("E.02", o["raum"]!.GetValue<string>());
        Assert.Equal("EG", o["geschoss"]!.GetValue<string>());
        Assert.Equal("WC", o["klasse"]!.GetValue<string>());
        Assert.Equal("sanitaer", o["gewerk"]!.GetValue<string>());
    }

    [Fact]
    public void Darstellung_RaumHatSchichtRaum()
    {
        var o = Dar(Beispiel(), "E.02");
        Assert.Equal("raum", o["schicht"]!.GetValue<string>());
        Assert.Equal("polygon", o["geometrie"]!["art"]!.GetValue<string>());
        Assert.Equal("EG", o["geschoss"]!.GetValue<string>());
    }

    [Fact]
    public void Darstellung_AnsichtHatWerteArt()
    {
        var p = Beispiel();
        var ansichten = p.Modell.DerKlasse("Ansicht").ToList();
        Assert.NotEmpty(ansichten);
        foreach (var a in ansichten)
        {
            var o = Darstellung.Element(p, a);
            Assert.Equal("ansicht", o["schicht"]!.GetValue<string>());
            Assert.False(string.IsNullOrEmpty(o["werte"]?["art"]?.GetValue<string>()), a.Kennzeichen);
        }
    }

    [Fact]
    public void Darstellung_DatenpunktHatBezuegeAlsKennzeichen()
    {
        var p = Beispiel();
        var o = Dar(p, "DP-MV1");
        Assert.Equal("funktion", o["schicht"]!.GetValue<string>());
        var bez = o["bezuege"]!.AsObject();
        Assert.True(bez.Count > 0);
        foreach (var (name, wert) in bez)
        {
            var kz = wert!.GetValue<string>();
            Assert.NotNull(p.Modell.Finde(kz));
            Assert.Equal(kz, p.Modell.Finde(kz)!.Kennzeichen);               // Kennzeichen, keine Id
        }
        Assert.Equal("MV-1", bez["an"]!.GetValue<string>());
    }

    [Fact]
    public void Darstellung_ModellHat175ElementeUnd138VerbindungenMitKennzeichen()
    {
        var p = Beispiel();
        var o = Darstellung.Modell(p);
        Assert.Equal(p.Kopf.Name, o["projekt"]!.GetValue<string>());
        Assert.Equal(175, o["elemente"]!.AsArray().Count);
        var v = o["verbindungen"]!.AsArray();
        Assert.Equal(138, v.Count);
        foreach (var x in v)
        {
            foreach (var feld in new[] { "von", "nach" })
            {
                var kz = x![feld]!.GetValue<string>();
                Assert.False(string.IsNullOrEmpty(kz));
                Assert.NotNull(p.Modell.Finde(kz));
            }
            Assert.False(string.IsNullOrEmpty(x!["vonAnschluss"]!.GetValue<string>()));
        }
        Assert.Equal(175, o["stand"]!["elemente"]!.GetValue<int>());
        Assert.Equal(138, o["stand"]!["verbindungen"]!.GetValue<int>());
    }

    [Fact]
    public void Darstellung_ElementeGeloeschteIdErscheintUnterGeloescht()
    {
        var p = Beispiel();
        var r = p.Ausfuehren("element_anlegen", Par("""{"klasse":"Raum","kennzeichen":"R-TEST","geschoss":"EG","eigenschaften":{"nummer":"T.01"},"geometrie":{"art":"polygon","punkte":[[0,0],[3000,0],[3000,3000],[0,3000]],"hoehe":2500}}"""));
        Assert.True(r.Angenommen, r.Fehler);
        var id = p.Modell.Hole("R-TEST").Id;
        var wc = p.Modell.Hole("WC-E.02").Id;

        var vorher = Darstellung.Elemente(p, [id, wc]);
        Assert.Equal(2, vorher["elemente"]!.AsArray().Count);
        Assert.Empty(vorher["geloescht"]!.AsArray());

        Assert.True(p.Ausfuehren("element_loeschen", Par("""{"element":"R-TEST"}""")).Angenommen);
        var nachher = Darstellung.Elemente(p, [id, wc, wc, "gibt-es-nicht"]);
        Assert.Single(nachher["elemente"]!.AsArray());
        Assert.Equal("WC-E.02", nachher["elemente"]![0]!["kennzeichen"]!.GetValue<string>());
        var geloescht = nachher["geloescht"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Equal([id, "gibt-es-nicht"], geloescht);
        Assert.NotNull(nachher["stand"]);
    }
}
