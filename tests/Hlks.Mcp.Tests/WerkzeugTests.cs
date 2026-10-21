using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Kern;
using ModelContextProtocol.Server;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

public class WerkzeugTests
{
    static readonly string[] Verlauf = ["rueckgaengig", "wiederholen", "synchronisieren"];

    static List<(MethodInfo Methode, string Name)> AlleWerkzeuge() =>
        typeof(Werkzeuge).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (Methode: m, Attr: m.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(x => x.Attr is not null).Select(x => (x.Methode, x.Attr!.Name!)).ToList();

    static JsonElement Elem(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static readonly string PlansatzOrdner = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-plansatz-{Environment.ProcessId}");

    // ------------------------------------------------------------------ 1. Abdeckung

    [Fact]
    public void Abdeckung_WerkzeugnamenSindGenauAbfragenBefehleUndVerlauf()
    {
        var p = Beispiel();
        var erwartet = Abfragen.Liste.Select(a => a.Name).Concat(p.Befehle.Alle.Select(b => b.Name)).Concat(Verlauf).Order().ToList();
        var ist = AlleWerkzeuge().Select(w => w.Name).Order().ToList();
        Assert.Equal(erwartet, ist);                       // keine fehlt, keine zu viel
        Assert.Equal(ist.Count, ist.Distinct().Count());
        Assert.Equal(132, ist.Count);   // + abzweigwinkel (Nachtrag Modul 3, 21.10.2026) + 2 Leserversion der Quellen (Nachtrag 21.10.2026: familie_pruefen, familien_quellen_nachfuehren) + ifc_export (Modul 7 Teil B2) + 3 Planblatt Modul 7 Teil A (plannummer_vorschlag, revision_hinzufuegen, plansatz_revision_hinzufuegen) + 3 Sanitär (Modul 4 Teil A: sanitaer_belastung, sanitaer_zusammenstellung, sanitaer_katalog) + 2 Normensuche (Modul 3c, Teil K: normen_suchen, norm_lesen; kontext_bild steht in SitzungWerkzeuge) + 4 Serien-PDF (plansaetze, plansatz_pdf, plansatz_setzen, planblatt_gewerk_setzen) + 3 Familienbibliothek (Modul 2, Nachtrag B7: familie_in_bibliothek, familie_aktualisieren, familienauswahl_setzen) + einlageplaene_anlegen (Modul 3, B10) + verlegung (Modul 3, Nachtrag B8c) + 2 Typenliste (Modul 3b, Nachtrag B3d: typmaterial_setzen; typmaterial) + schema_anlegen (Modul 3, Nachtrag B8) + aussparung_form (Modul 3b, Nachtrag B3b) + 1 Aussparungs-IFC (Modul 3b, B4b: aussparungen_ifc) + 5 Aussparungen (Modul 3b, B3: aussparungen_vorschlagen, aussparungen_nummerieren, aussparung_status, aussparungen_abgleichen; aussparungsliste) + 5 Kollisionsprüfung (Modul 3b, B2: vorgaben_setzen, kollision_pruefen, kollision_status; vorgaben, kollisionen) + 3 BCF (Modul 3b, B6: bcf_export, bcf_lesen, bcf_import) + 6 Routing (Modul 3, B2: leitung_zeichnen, leitung_abzweigen, leitung_aufteilen, leitung_groesse, leitung_serie; leitungszug) + hkap_strang_zuordnen (Modul 3, B5) + materialliste (Modul 3, B4) + 3 Schema (Modul 3, B3: schema, schema_lage, schema_anordnen) + leitungsserie (Modul 3, B1) + 6 Familienprüfung (Modul 2, B4: konflikt_entscheiden, wert_bestaetigen, familie_freigeben, familie_speichern; familie_masse, familie_vergleichen) + familien_bibliothek (Modul 2, B1, Nachtrag 2); 26 aus 0a + 7 Architektur + 3 Planblatt (Modul 1a) + 2 IFC + 2 Unterlagen (Modul 1b) + 11 Hülle (Modul 1c, B2: 8 Befehle, 3 Abfragen) + decke_erzeugen (B3) + 3 Raumgrenzen (B1: 2 Abfragen, 1 Befehl) + 7 HKAP (Modul 1d: 5 Befehle, 2 Abfragen) + 8 Familien (Modul 2, B1: 4 Befehle, 4 Abfragen)
    }

    [Fact]
    public void Abdeckung_JedesWerkzeugHatBeschreibung()
    {
        foreach (var (m, name) in AlleWerkzeuge())
        {
            Assert.False(string.IsNullOrWhiteSpace(m.GetCustomAttribute<DescriptionAttribute>()?.Description), $"{name}: Beschreibung fehlt");
            // CancellationToken bindet der MCP-Server selbst an den Abbruch des Aufrufs (kein Werkzeugparameter; leitungsserie seit 21.10.2026)
            foreach (var par in m.GetParameters().Where(x => x.ParameterType != typeof(CancellationToken)))
                Assert.False(string.IsNullOrWhiteSpace(par.GetCustomAttribute<DescriptionAttribute>()?.Description), $"{name}.{par.Name}: Beschreibung fehlt");
        }
    }

    static void ParameterPruefen(string name, MethodInfo m, IReadOnlyList<ParameterDef> defs, bool mitTrockenlauf)
    {
        var ps = m.GetParameters().Where(x => x.ParameterType != typeof(CancellationToken)).ToDictionary(x => x.Name!);   // CancellationToken: kein Werkzeugparameter
        var erwartet = defs.Select(d => d.Name).ToList();
        if (mitTrockenlauf) erwartet.Add("trockenlauf");
        Assert.Equal(erwartet.Order(), ps.Keys.Order());
        foreach (var d in defs)
        {
            var par = ps[d.Name];
            Assert.Equal(d.Beschreibung, par.GetCustomAttribute<DescriptionAttribute>()?.Description);
            Assert.True(!d.Pflicht == par.HasDefaultValue, $"{name}.{d.Name}: Pflicht/optional passt nicht");
            var typ = par.ParameterType;
            switch (d.Typ)
            {
                case "text": Assert.Equal(typeof(string), typ); break;
                case "zahl": Assert.Equal(d.Pflicht ? typeof(double) : typeof(double?), typ); break;
                case "objekt" or "liste": Assert.Equal(d.Pflicht ? typeof(JsonElement) : typeof(JsonElement?), typ); break;
                case "bool": Assert.Equal(typeof(bool), typ); break;
                default: Assert.Fail($"{name}.{d.Name}: Typ {d.Typ} nicht abgedeckt"); break;
            }
        }
        if (mitTrockenlauf)
        {
            Assert.Equal(typeof(bool), ps["trockenlauf"].ParameterType);
            Assert.True(ps["trockenlauf"].HasDefaultValue);
            Assert.Equal(false, ps["trockenlauf"].DefaultValue);
        }
    }

    [Fact]
    public void Abdeckung_AbfrageParameterUndBeschreibungenStimmenMitDerListe()
    {
        var werkzeuge = AlleWerkzeuge().ToDictionary(w => w.Name, w => w.Methode);
        foreach (var a in Abfragen.Liste)
        {
            var m = werkzeuge[a.Name];
            Assert.Equal(a.Beschreibung, m.GetCustomAttribute<DescriptionAttribute>()!.Description);
            ParameterPruefen(a.Name, m, a.Parameter, false);
        }
    }

    [Fact]
    public void Abdeckung_BefehlParameterUndBeschreibungenStimmenMitDemRegister()
    {
        var werkzeuge = AlleWerkzeuge().ToDictionary(w => w.Name, w => w.Methode);
        foreach (var b in Beispiel().Befehle.Alle)
        {
            var m = werkzeuge[b.Name];
            Assert.Equal(b.Beschreibung, m.GetCustomAttribute<DescriptionAttribute>()!.Description);
            ParameterPruefen(b.Name, m, b.Parameter, true);
        }
        foreach (var n in new[] { "rueckgaengig", "wiederholen" })
            Assert.Contains(werkzeuge[n].GetParameters(), x => x.Name == "trockenlauf" && x.HasDefaultValue);
    }

    // ------------------------------------------------------------------ 2. Abfragen

    [Fact]
    public void Abfragen_JedeLiefertNichtLeerOhneFehler()
    {
        var w = new Werkzeuge(Beispiel());
        var aufrufe = new Dictionary<string, Func<string>>
        {
            ["faehigkeiten"] = () => w.Faehigkeiten(),
            ["projekt_ueberblick"] = () => w.ProjektUeberblick(),
            ["element_lesen"] = () => w.ElementLesen("WT-E.02"),
            ["elemente_suchen"] = () => w.ElementeSuchen(klasse: "Apparat", geschoss: "EG", seite: 1, proSeite: 5),
            ["system_lesen"] = () => w.SystemLesen("SW"),
            ["strang_text"] = () => w.StrangText("SW-F2"),
            ["netz_folgen"] = () => w.NetzFolgen("MV-1", "stromab", medium: "KW"),
            ["raum_inhalt"] = () => w.RaumInhalt("E.02"),
            ["mengen"] = () => w.Mengen(system: "SW"),
            ["raumliste"] = () => w.Raumliste("1.OG"),
            ["strangliste"] = () => w.Strangliste(),
            ["pruefen"] = () => w.Pruefen(),
            ["warum"] = () => w.Warum("WT-E.02", "raum"),
            ["aenderungen"] = () => w.Aenderungen(anzahl: 5),
            ["katalog"] = () => w.Katalog(),
            ["datenpunkte"] = () => w.Datenpunkte(),
            ["zeitreihe"] = () => w.Zeitreihe("DP-WZ1-VOL", "2026-10-19T00:00", "2026-10-20T00:00", 120),
            ["plan_pdf"] = () => { w.PlanblattAnlegen("T-1", "Testplan"); return w.PlanPdf("PB-1"); },   // das Beispielprojekt hat noch kein Planblatt
            ["ifc_pruefen"] = () => w.IfcPruefen(Testdaten("minimal_ifc4.ifc")),
            ["aufbau_rechnen"] = () =>   // das Beispielprojekt hat noch keinen Aufbau (Testwerte, nicht aus einer Quelle)
            {
                w.MaterialAnlegen("Testmaterial", werte: Elem("""{ "lambda": { "wert": 0.5, "quelle": "Testwert" } }"""));
                w.AufbauAnlegen("Testaufbau", "wand", Elem("""[{ "material": "MAT-1", "dicke": 200 }]"""));
                return w.AufbauRechnen("AUF-1");
            },
            ["materialien"] = () => w.Materialien(),
            ["huelle_pruefen"] = () => w.HuellePruefen(),
            ["hkap_verbindung"] = () => w.HkapVerbindung(url: "http://127.0.0.1:9"),   // Modul 1d: nie die echte HKAP; nicht erreichbar = Antwort, kein Fehler
            ["hkap_abgleich"] = () => w.HkapAbgleich(),                                // nicht verknüpft = Antwort, kein Fehler
            ["raumgrenzen"] = () => w.RaumgrenzenLesen(geschoss: "EG"),                 // Modul 1c, B1
            ["huellbauteile"] = () => w.Huellbauteile(),
            ["familien"] = () => { FamilieAnlegen(w); return w.FamilienLesen(); },   // Modul 2, B1: Testfamilie (fiktiv), im Beispielprojekt gibt es keine
            ["familie"] = () => w.FamilieLesen("FAM-1"),
            ["anschlusslagen"] = () => w.Anschlusslagen("HK-T1"),
            ["familie_ansicht"] = () => w.FamilieAnsicht("FAM-1", "22-600-1000", "oben", "mittel"),
            ["familien_bibliothek"] = () => w.FamilienBibliothekLesen(),
            ["unterlagen_lesen"] = () => w.UnterlagenLesen(Elem($"[\"{Testdaten("unterlagen/beispiel.pdf").Replace("\\", "/")}\"]")),   // Modul 2, B2: kleines Beispiel-PDF
            ["befunde"] = () => w.Befunde(Testdaten("unterlagen/beispiel.pdf"), art: "wort"),
            ["unterlage_bild"] = () => w.UnterlageBild(Testdaten("unterlagen/beispiel.pdf"), breite: 200).Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().First().Text,
            ["familie_pruefen"] = () => w.FamiliePruefenWerkzeug("FAM-1"),   // Nachtrag 21.10.2026: Prüfbefunde der Testfamilie
            ["familie_masse"] = () => w.FamilieMasse("FAM-1"),   // Modul 2, B4: Testfamilie ohne Massbezüge = leere Liste mit Hinweis
            ["familie_vergleichen"] = () => w.FamilieVergleichen("FAM-1", "22-600-1000", Testdaten("unterlagen/beispiel.pdf"), "vorne", breite: 300,
                passpunkte: Elem("""[{ "familie": [0, 0], "unterlage": [100, 100] }, { "familie": [1000, 0], "unterlage": [300, 100] }]""")).Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().First().Text,
            ["leitungsserie"] = () =>   // Modul 3, B1: fiktive Testrohrserie (im Beispielprojekt gibt es keine)
            {
                var l = Json(w.FamilieLadenWerkzeug(Testdaten(Path.Combine("familien", "test_rohrserie.familie.json"))));
                Assert.True(l["angenommen"]!.GetValue<bool>(), l.ToJsonString());
                return w.LeitungsserieLesen("fam-test-rohrserie");
            },
            ["abzweigwinkel"] = () => w.AbzweigwinkelLesen(familie: "fam-test-rohrserie", groesse: Elem("\"DN 20\"")),   // Nachtrag Modul 3, 21.10.2026 (Testrohrserie lädt leitungsserie oben)
            ["schema"] = () => w.SchemaLesen("AN-SCHEMA-SAN"),   // Modul 3, B3
            ["materialliste"] = () => w.MateriallisteLesen(system: "SW"),   // Modul 3, B4
            ["leitungszug"] = () => w.LeitungszugLesen("KW-V1-3"),          // Modul 3, B2: Zug einer Polylinie des Beispielprojekts
            ["verlegung"] = () => w.VerlegungLesen(),                        // Modul 3, Nachtrag B8c: Beispielprojekt ohne Decken = alles frei
            ["bcf_export"] = () => w.BcfExportieren(BcfTestdatei(w)),      // Modul 3b, B6: Testkollision (fiktiv) legt BcfTestdatei an
            ["bcf_lesen"] = () => w.BcfLesenWerkzeug(BcfTestdatei(w)),
            ["vorgaben"] = () => w.VorgabenLesen(),                          // Modul 3b, B2: ohne Vorgaben = Antwort mit fehlenden Werten
            ["kollisionen"] = () => w.KollisionenLesen(status: "neu"),       // Modul 3b, B2
            ["typmaterial"] = () => w.TypmaterialLesen(),                    // Modul 3b, Nachtrag B3d: leere Typenliste = Antwort mit Hinweis
            ["aussparungsliste"] = () => w.AussparungslisteLesen(geschoss: "EG"),   // Modul 3b, B3: im Beispielprojekt ohne Aussparungen = leere Liste
            ["aussparungen_ifc"] = () => w.AussparungenIfcSchreiben(AussparungIfcTestdatei(w)),   // Modul 3b, B4b: freigegebene Testaussparung (fiktiv) legt AussparungIfcTestdatei an
            ["plannummer_vorschlag"] = () => w.PlannummerVorschlag(gewerk: "heizung", geschoss: "EG"),   // Modul 7 Teil A
            ["planwinkel"] = () => w.PlanwinkelLesen(ansicht: "AN-GR-EG"),   // N9: Planwinkel GEB-1 und Ausrichtung des Grundrisses EG
            ["plansaetze"] = () => w.PlansaetzeLesen(),   // Serien-PDF: Planblatt T-1 (plan_pdf oben) ohne Fachbuchstabe = Plansatz «allgemein» angeboten
            ["plansatz_pdf"] = () => w.PlansatzPdfSchreiben("allgemein", ordner: PlansatzOrdner, ueberschreiben: true),
            ["normen_suchen"] = () => NormenTestdaten.Mit(() => w.NormenSuchen("Mindestgefälle Prüfleitung")),   // Modul 3c: eigene Test-PDF, nie W:
            ["norm_lesen"] = () => NormenTestdaten.Mit(() => w.NormLesen("SIA 9999:2026", 3)),
            ["sanitaer_belastung"] = () => w.SanitaerBelastungLesen(anlage: "A-SAN"),            // Modul 4 Teil A
            ["sanitaer_zusammenstellung"] = () => w.SanitaerZusammenstellungLesen("A-SAN"),
            ["sanitaer_katalog"] = () => w.SanitaerKatalogLesen(),
            ["ifc_export"] = () => w.IfcExportieren(IfcExportDatei, "IFC2X3"),   // Modul 7 Teil B2: Export des Beispielprojekts in den Temp-Ordner
        };
        Assert.Equal(Abfragen.Liste.Select(a => a.Name).Order(), aufrufe.Keys.Order());   // jede Abfrage ist getestet
        foreach (var (name, f) in aufrufe)
        {
            var text = f();
            Assert.False(string.IsNullOrWhiteSpace(text), $"{name}: leer");
            Assert.False(text.StartsWith("{\"fehler\""), $"{name}: {text}");
        }
        File.Delete(Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-test-{Environment.ProcessId}.bcf"));   // Ausgabe von bcf_export aufräumen
        File.Delete(Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-test-{Environment.ProcessId}-aussparungen.ifc"));   // Ausgabe von aussparungen_ifc aufräumen
        try { Directory.Delete(PlansatzOrdner, true); } catch (IOException) { }   // Ausgabe von plansatz_pdf aufräumen
        foreach (var x in new[] { IfcExportDatei, IfcExportDatei[..^4] + ".bericht.json", IfcExportDatei[..^4] + ".bericht.md" }) File.Delete(x);   // Ausgabe von ifc_export aufräumen
    }

    static readonly string IfcExportDatei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-test-{Environment.ProcessId}-export.ifc");

    /// <summary>Modul 3b, B4b: Testaussparung AUS-T1 (fiktiv, freigegeben) anlegen, falls nötig – über die Werkzeuge. Gibt den Pfad der IFC-Datei zurück.</summary>
    static string AussparungIfcTestdatei(Werkzeuge w)
    {
        if (Json(w.ElementLesen("AUS-T1")).AsObject().ContainsKey("fehler"))
        {
            var a = Json(w.ElementAnlegen("Aussparung", kennzeichen: "AUS-T1", geschoss: "EG",
                eigenschaften: Elem("""{ "form": "rund", "d": 95, "tiefe": 200, "achse": [0, 1, 0], "art": "kernbohrung", "status": "freigegeben", "nummer": "A-EG-001" }"""),
                geometrie: Elem("""{ "art": "punkt", "punkte": [[1000, 1000, 1000]] }""")));
            Assert.True(a["angenommen"]!.GetValue<bool>(), a.ToJsonString());
        }
        return Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-test-{Environment.ProcessId}-aussparungen.ifc");
    }

    /// <summary>Modul 3b, B6: Testkollision KOL-T1 (fiktiv) anlegen, falls nötig, und als BCF in den Temp-Ordner schreiben – über die
    /// Werkzeuge. Gibt den Pfad zurück (eine Datei je Testlauf).</summary>
    static string BcfTestdatei(Werkzeuge w)
    {
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", $"mcp-test-{Environment.ProcessId}.bcf");
        if (Json(w.ElementLesen("KOL-T1")).AsObject().ContainsKey("fehler"))
        {
            var k = Json(w.ElementAnlegen("Kollision", kennzeichen: "KOL-T1", eigenschaften: Elem("""{ "art": "hart", "beteiligte": ["WT-E.02"], "mass": 10 }"""),
                geometrie: Elem("""{ "art": "punkt", "punkte": [[1000, 1000, 500]] }""")));
            Assert.True(k["angenommen"]!.GetValue<bool>(), k.ToJsonString());
        }
        var a = w.BcfExportieren(datei);
        Assert.False(a.StartsWith("{\"fehler\""), a);
        return datei;
    }

    /// <summary>Testfamilie (tests/daten/familien, fiktive Werte) anlegen und als HK-T1 platzieren – über die Werkzeuge.</summary>
    static void FamilieAnlegen(Werkzeuge w)
    {
        var e = Json(w.FamilieEntwurfen(Elem(File.ReadAllText(Testdaten(Path.Combine("familien", "test_heizkoerper.familie.json"))))));
        Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
        var p = Json(w.FamiliePlatzierenWerkzeug("FAM-1", "22-600-1000", Elem("[5000, 3000]"), drehung: 90, geschoss: "EG", kennzeichen: "HK-T1"));
        Assert.True(p["angenommen"]!.GetValue<bool>(), p.ToJsonString());
    }

    [Fact]
    public void Abfragen_ErgebnisseEntsprechenDemKern()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        Assert.Equal(9, Json(w.Strangliste())["zeilen"]!.AsArray().Count);   // 7 Sanitär-Stränge + 2 Kälte-Stränge (Beispielprojekt, Entscheid Christian 20.10.2026)
        Assert.Contains("MFH Musterweg", w.ProjektUeberblick());
        Assert.Contains("SW-F2", w.StrangText("SW-F2"));          // Textansicht bleibt Text
        Assert.StartsWith("System SW", w.SystemLesen("SW"));
        Assert.Equal(p.Modell.Pruefsumme(), Json(w.ProjektUeberblick())["modellStand"]!["pruefsumme"]!.GetValue<string>());
        Assert.DoesNotContain("\\u", w.Raumliste());              // Umlaute nicht maskiert
    }

    // ------------------------------------------------------------------ 3. Fehlerfall

    [Fact]
    public void Fehler_UnbekanntesKennzeichenGibtFehlerUndVorschlag()
    {
        var w = new Werkzeuge(Beispiel());
        var o = Json(w.ElementLesen("GIBT-ES-NICHT")).AsObject();
        Assert.Contains("GIBT-ES-NICHT", o["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(o["vorschlag"]?.GetValue<string>()));
    }

    [Fact]
    public void Fehler_WerfenNieAusnahmen()
    {
        var w = new Werkzeuge(Beispiel());
        Assert.Contains("fehler", w.Zeitreihe("DP-WZ1-VOL", "keine Zeit", "auch nicht"));
        Assert.Contains("fehler", w.NetzFolgen("MV-1", "seitwaerts"));
        Assert.Contains("fehler", w.StrangText("WT-E.02"));                    // kein Strang
        Assert.Contains("fehler", w.Katalog("Gibtsnicht"));
        // Befehl abgelehnt: kein Wurf, sondern Ergebnis mit angenommen=false, Fehler und Vorschlag
        var abgelehnt = Json(w.ElementLoeschen("SW-F2")).AsObject();
        Assert.False(abgelehnt["angenommen"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(abgelehnt["fehler"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(abgelehnt["vorschlag"]?.GetValue<string>()));
    }

    [Fact]
    public void Fehler_InternerFehlerWirdAlsJsonGemeldet()
    {
        var w = new Werkzeuge(Beispiel());
        var o = Json(w.Lauf(() => throw new InvalidOperationException("kaputt"))).AsObject();
        Assert.Equal("Interner Fehler: kaputt", o["fehler"]!.GetValue<string>());
        var f = Json(w.Lauf(() => throw new HlksFehler("so nicht", "so schon"))).AsObject();
        Assert.Equal("so nicht", f["fehler"]!.GetValue<string>());
        Assert.Equal("so schon", f["vorschlag"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ 4. Befehle, Trockenlauf, Rückgängig

    /// <summary>Inhaltliche Prüfsumme: wie Modell.Pruefsumme, aber ohne Version und Paketvermerk (letzte Änderung).</summary>
    static string Inhalt(Projekt p)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in p.Modell.Elemente.OrderBy(e => e.Id, StringComparer.Ordinal)) { var k = e.Kopie(); k.Version = 0; k.Paket = null; sb.Append(Hlks.Kern.Json.Text(k)).Append('\n'); }
        foreach (var v in p.Modell.Verbindungen.OrderBy(v => v.Id, StringComparer.Ordinal)) { var k = v.Kopie(); k.Version = 0; k.Paket = null; sb.Append(Hlks.Kern.Json.Text(k)).Append('\n'); }
        return sb.ToString();
    }

    [Fact]
    public void Befehl_AnlegenUndRueckgaengigStelltPruefsummeExaktHer()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var start = p.Modell.Pruefsumme();
        Assert.True(Json(w.ElementAnlegen("Raum", kennzeichen: "R-TEST", geschoss: "EG",
            eigenschaften: Elem("""{ "nummer": "T.01" }"""),
            geometrie: Elem("""{ "art": "polygon", "punkte": [[0,0],[3000,0],[3000,3000],[0,3000]], "hoehe": 2500 }""")))["angenommen"]!.GetValue<bool>());
        Assert.NotEqual(start, p.Modell.Pruefsumme());
        Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
        Assert.Equal(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Befund_RueckgaengigEinerAenderungStelltPruefsummeExaktHer()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var start = p.Modell.Pruefsumme();
        Assert.True(Json(w.ElementAendern("E.02", name: "Neuer Name"))["angenommen"]!.GetValue<bool>());
        Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
        Assert.Equal(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Befehl_TrockenlaufAendertNichtsEchtSchonRueckgaengigStelltInhaltHer()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var start = p.Modell.Pruefsumme();
        var startInhalt = Inhalt(p);

        var tl = Json(w.ElementAendern("E.02", name: "Neuer Name", trockenlauf: true)).AsObject();
        Assert.True(tl["angenommen"]!.GetValue<bool>());
        Assert.True(tl["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(start, p.Modell.Pruefsumme());

        var echt = Json(w.ElementAendern("E.02", name: "Neuer Name")).AsObject();
        Assert.True(echt["angenommen"]!.GetValue<bool>(), echt.ToJsonString());
        Assert.NotNull(echt["paketId"]);
        var geaendert = p.Modell.Pruefsumme();
        var geaendertInhalt = Inhalt(p);
        Assert.NotEqual(start, geaendert);
        Assert.Equal("Neuer Name", p.Modell.Hole("E.02").Name);

        var trockenZurueck = Json(w.Rueckgaengig(trockenlauf: true)).AsObject();
        Assert.True(trockenZurueck["trockenlauf"]!.GetValue<bool>());
        Assert.Equal(geaendert, p.Modell.Pruefsumme());

        Assert.True(Json(w.Rueckgaengig())["angenommen"]!.GetValue<bool>());
        Assert.Equal(startInhalt, Inhalt(p));                  // Inhalt wie vorher (zum Paketvermerk siehe Skip-Test)
        Assert.NotEqual("Neuer Name", p.Modell.Hole("E.02").Name);

        Assert.True(Json(w.Wiederholen())["angenommen"]!.GetValue<bool>());
        Assert.Equal(geaendertInhalt, Inhalt(p));
    }

    [Fact]
    public void Befehl_AnlegenMitJsonParameternUndTrockenlauf()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var vorher = p.Modell.Elemente.Count;
        string Anlegen(bool trocken) => w.ElementAnlegen("Raum", kennzeichen: "R-TEST", geschoss: "EG",
            eigenschaften: Elem("""{ "nummer": "T.01", "nutzung": "Büro" }"""),
            geometrie: Elem("""{ "art": "polygon", "punkte": [[0,0],[3000,0],[3000,3000],[0,3000]], "hoehe": 2500 }"""), trockenlauf: trocken);
        var tl = Anlegen(true);
        Assert.True(Json(tl)["angenommen"]!.GetValue<bool>(), tl);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        var echt = Anlegen(false);
        Assert.True(Json(echt)["angenommen"]!.GetValue<bool>(), echt);
        Assert.Equal(vorher + 1, p.Modell.Elemente.Count);
        Assert.Equal("Büro", p.Modell.Hole("R-TEST").Text("nutzung"));
        Assert.Contains("R-TEST", w.ElementLesen("R-TEST"));
    }

    [Fact]
    public void Befehl_TrennenUndVerbindenUeberWerkzeuge()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var v = p.Modell.Verbindungen.First();
        string Kz(string id) => p.Modell.Finde(id)!.Kennzeichen;
        var von = $"{Kz(v.Von.Element)}.{v.Von.Anschluss}";
        var nach = $"{Kz(v.Nach.Element)}.{v.Nach.Anschluss}";
        var anzahl = p.Modell.Verbindungen.Count;

        var tl = Json(w.Trennen(anschluss: Elem($"\"{von}\""), trockenlauf: true));
        Assert.True(tl["angenommen"]!.GetValue<bool>(), tl.ToJsonString());
        Assert.Equal(anzahl, p.Modell.Verbindungen.Count);

        Assert.True(Json(w.Trennen(anschluss: Elem($"\"{von}\"")))["angenommen"]!.GetValue<bool>());
        Assert.Equal(anzahl - 1, p.Modell.Verbindungen.Count);

        var verbunden = Json(w.Verbinden(Elem($"\"{von}\""), Elem($"\"{nach}\"")));
        Assert.True(verbunden["angenommen"]!.GetValue<bool>(), verbunden.ToJsonString());
        Assert.Equal(anzahl, p.Modell.Verbindungen.Count);
    }

    [Fact]
    public void Befehl_LeitungZiehenUndLoeschen()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var antwort = Json(w.LeitungZiehen(Elem("[[0,0,0],[1000,0,0]]"), strang: "SW-S1", kennzeichen: "RO-TEST", dn: 100, material: "PE"));
        Assert.True(antwort["angenommen"]!.GetValue<bool>(), antwort.ToJsonString());
        Assert.Equal(100, p.Modell.Hole("RO-TEST").Zahl("dn"));
        Assert.True(Json(w.ElementLoeschen("RO-TEST"))["angenommen"]!.GetValue<bool>());
        Assert.Null(p.Modell.Finde("RO-TEST"));
    }

    [Fact]
    public void Befehl_DeckeErzeugenUeberWerkzeug()   // Modul 1c, B3
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var tl = Json(w.DeckeErzeugen("EG", "decke", 250, trockenlauf: true)).AsObject();
        Assert.True(tl["angenommen"]!.GetValue<bool>(), tl.ToJsonString());
        Assert.Contains(tl["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("127.82 m²"));   // 12350 × 10350 (Aussenwände 350)
        Assert.Null(p.Modell.Finde("D-1"));
        Assert.True(Json(w.DeckeErzeugen("EG", "decke", 250))["angenommen"]!.GetValue<bool>());
        Assert.Equal(p.Modell.Hole("1.OG").Id, p.Modell.Hole("D-1").Geschoss);
        var doppelt = Json(w.DeckeErzeugen("EG", "decke", 250)).AsObject();
        Assert.False(doppelt["angenommen"]!.GetValue<bool>());
        Assert.Contains("element_loeschen", doppelt["vorschlag"]!.GetValue<string>());
    }

    [Fact]
    public void Befehl_UnterlageEinfuegenUndKalibrierenUeberWerkzeuge()
    {
        var p = Beispiel();
        var w = new Werkzeuge(p);
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_mcp_ul_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        try
        {
            // Bild 20 × 10 Bildpunkte, 1 mm Papier je Bildpunkt (25.4 dpi), 1:50 → 50 mm je Bildpunkt im Modell
            var png = Path.Combine(ordner, "skizze.png");
            PngSchreiber.Schreiben(png, 20, 10, new byte[20 * 10 * 4], 25.4);
            var vorher = p.Modell.Elemente.Count;
            var tl = Json(w.UnterlageEinfuegen(png, "EG", massstab: 50, dpi: 25.4, trockenlauf: true)).AsObject();
            Assert.True(tl["angenommen"]!.GetValue<bool>(), tl.ToJsonString());
            Assert.Equal(vorher, p.Modell.Elemente.Count);

            var e = Json(w.UnterlageEinfuegen(png, "EG", massstab: 50, dpi: 25.4)).AsObject();
            Assert.True(e["angenommen"]!.GetValue<bool>(), e.ToJsonString());
            Assert.Contains("kalibrieren", e["hinweise"]!.ToJsonString());
            var ul = p.Modell.Hole("UL-1");
            Assert.Equal("bild", ul.Text("art"));
            Assert.Equal(50, UnterlagenLage.Lesen(ul).Faktor, 6);

            // zwei Punkte der Unterlage (Bildpunkte, y von oben) auf ihre Sollpunkte: doppelt so gross
            var k = Json(w.UnterlageKalibrieren("UL-1", Elem("[[0, 10], [20, 10]]"), Elem("[[0, 0], [2000, 0]]"))).AsObject();
            Assert.True(k["angenommen"]!.GetValue<bool>(), k.ToJsonString());
            Assert.Equal(100, UnterlagenLage.Lesen(p.Modell.Hole("UL-1")).Faktor, 6);
            Assert.Contains("ACHTUNG", k["hinweise"]!.ToJsonString());   // 100 statt 50 erwartet
            var v = Json(w.UnterlageKalibrieren("UL-1", Elem("[[0, 0]]"), Elem("[[500, 500]]"), nurVerschieben: true)).AsObject();
            Assert.True(v["angenommen"]!.GetValue<bool>(), v.ToJsonString());

            var nein = Json(w.UnterlageEinfuegen(Path.Combine(ordner, "gibtsnicht.pdf"), "EG")).AsObject();
            Assert.False(nein["angenommen"]!.GetValue<bool>());
            Assert.False(string.IsNullOrWhiteSpace(nein["vorschlag"]?.GetValue<string>()));
            Assert.Single(p.Modell.DerKlasse("Unterlage"));
        }
        finally { Aufraeumen(ordner); }
    }

    [Fact]
    public void Befehl_UnbekannteEigenschaftWirdAbgelehntMitErlaubtenWerten()
    {
        var w = new Werkzeuge(Beispiel());
        var o = Json(w.ElementAendern("E.02", eigenschaften: Elem("""{ "gibtEsNicht": 1 }"""))).AsObject();
        Assert.False(o["angenommen"]!.GetValue<bool>());
        Assert.Contains("Erlaubt", o["vorschlag"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Synchronisation vor jedem Aufruf

    [Fact]
    public void Synchronisieren_FremdeAenderungSichtbarVorJedemWerkzeugaufruf()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        try
        {
            Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner)).Freigeben();
            var a = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "KI", "pc-a", "ki");
            var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Mensch", "pc-b");
            var w = new Werkzeuge(a);
            Assert.DoesNotContain("Fremdname", w.ElementLesen("E.02"));

            Assert.True(b.Ausfuehren("element_aendern", new JsonObject { ["element"] = "E.02", ["name"] = "Fremdname" }).Angenommen);
            Assert.Contains("Fremdname", w.ElementLesen("E.02"));                 // ohne eigenes synchronisieren
            Assert.Equal(b.Modell.Pruefsumme(), a.Modell.Pruefsumme());

            var s = Json(w.Synchronisieren()).AsObject();                         // nichts Neues mehr
            Assert.Equal(0, s["neuePakete"]!.GetValue<int>());
            a.Freigeben(); b.Freigeben();
        }
        finally { Aufraeumen(wurzel); }
    }
}
