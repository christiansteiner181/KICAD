using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Nachtrag B7 (entwurf/modul-2-bibliothek.md): projektübergreifende Familienbibliothek – Ort und Schutzregel,
/// Ablage (Index, Versionen, Log), automatisches Schreiben (auch KI, nie im Trockenlauf), Rückgängig, Befehle und Abfragen,
/// Sperre, Warteschlange, Index-Zwischenspeicher. Bibliothek nur unter %TEMP% (BibliothekTestBasis). Alle Werte FIKTIV.</summary>
[Collection("Familienbibliothek")]
public sealed class FamilienBibliothekTests : BibliothekTestBasis
{
    static JsonObject Entwurf(JsonObject? def = null) => new() { ["definition"] = def ?? Testheizkoerper() };

    // ------------------------------------------------------------------ 1. Ort und Schutzregel

    [Fact]
    public void Ort_Umgebungsvariable_vor_bueroJson_vor_Rueckfall()
    {
        var kat = Path.Combine(Wurzel, "kataloge");
        Directory.CreateDirectory(kat);
        File.Copy(Path.Combine(Kat.Ordner, "buero.json"), Path.Combine(kat, "buero.json"));
        var env = Path.Combine(Wurzel, "eigene");
        var o1 = FamilienBibliothekDienst.Ermitteln(kat, env, testlauf: false);
        Assert.Equal(env, o1.Pfad);
        Assert.Contains(FamilienBibliothekDienst.Umgebungsvariable, o1.Quelle);
        Assert.Null(o1.Sperre);
        // ohne Variable: kataloge/buero.json (nur gelesen, kein Zugriff auf W:)
        var o2 = FamilienBibliothekDienst.Ermitteln(kat, null, testlauf: false);
        Assert.Equal(@"W:\03_CAD Bibliotheken\d_KICAD Familien", o2.Pfad);
        Assert.Equal("kataloge/buero.json", o2.Quelle);
        // ohne buero.json: Rückfall kataloge/familien
        File.Delete(Path.Combine(kat, "buero.json"));
        var o3 = FamilienBibliothekDienst.Ermitteln(kat, null, testlauf: false);
        Assert.Equal(Path.Combine(kat, "familien"), o3.Pfad);
        Assert.Contains("Rückfall", o3.Quelle);
    }

    [Fact]
    public void Schutzregel_im_Testlauf_ohne_Variable_bzw_auf_dem_NAS_gesperrt()
    {
        Assert.True(FamilienBibliothekDienst.Testlauf());
        var kat = Kat.Ordner;
        Assert.Contains("Testlauf", FamilienBibliothekDienst.Ermitteln(kat, null, testlauf: true).Sperre);
        Assert.NotNull(FamilienBibliothekDienst.Ermitteln(kat, @"W:\03_CAD Bibliotheken\d_KICAD Familien", testlauf: true).Sperre);
        Assert.NotNull(FamilienBibliothekDienst.Ermitteln(kat, @"\\HK-NAS02\WorkShare_NAS\bib", testlauf: true).Sperre);
        Assert.NotNull(FamilienBibliothekDienst.Ermitteln(kat, @"C:\HLKS_nicht_temp\bib", testlauf: true).Sperre);
        Assert.Null(FamilienBibliothekDienst.Ermitteln(kat, Path.Combine(Path.GetTempPath(), "x"), testlauf: true).Sperre);
        Assert.True(FamilienBibliothekDienst.UnterTemp(FamilienBibliothekDienst.WarteschlangeWurzel()));
        // Bürobibliothek (buero.json bzw. Rückfall): Projekte ohne Ablage oder unter %TEMP% schreiben nicht automatisch (Browser-Tests, Versuche)
        var buero = FamilienBibliothekDienst.Ermitteln(kat, null, testlauf: false);
        Assert.True(buero.Aktiv);
        Assert.Contains("%TEMP%", FamilienBibliothekDienst.AutomatischGesperrt(buero, NeuesProjekt("Versuch")));
        Assert.Contains("ohne Ablage", FamilienBibliothekDienst.AutomatischGesperrt(buero, Projekt.Anlegen(Kat, null, "ohne Ablage", "T", "pc")));
        Assert.Null(FamilienBibliothekDienst.AutomatischGesperrt(Ort, NeuesProjekt("Mit Variable")));   // eigene Bibliothek über die Variable: gilt nicht
    }

    [Fact]
    public void Ohne_Variable_Hinweis_statt_Schreiben_und_klare_Fehler()
    {
        var p = NeuesProjekt("Ohne Variable");
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, null);
        Assert.False(Ort.Aktiv);
        var e = Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Familienbibliothek: nicht geschrieben") && h.Contains("Testlauf ohne HLKS_FAMILIENBIBLIOTHEK"));
        Assert.Null(Hole(p, "FAM-1").Eigenschaft("bibliothek"));
        Assert.Equal("gesperrt", Art(p));
        var l = Bibliothek(p);
        Assert.False(l["bibliothek"]!["aktiv"]!.GetValue<bool>());
        Assert.Contains("Testlauf", l["bibliothek"]!["gesperrt"]!.GetValue<string>());
        var n = Nein(p.Ausfuehren("familie_in_bibliothek", []));
        Assert.Contains("Testlauf", n.Fehler);
        Assert.Contains(FamilienBibliothekDienst.Umgebungsvariable, n.Vorschlag);
        Assert.Contains("Testlauf", Nein(p.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId })).Fehler);
        // Variable auf das NAS: ebenso gesperrt, kein Zugriff
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, @"W:\03_CAD Bibliotheken\d_KICAD Familien");
        Assert.NotNull(Ort.Sperre);
        var e2 = Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "anders"))));
        Assert.Contains(e2.Hinweise, h => h.StartsWith("Familienbibliothek: nicht geschrieben"));
        Assert.False(Directory.Exists(Bib));
    }

    // ------------------------------------------------------------------ 2. Ablage und automatisches Schreiben

    [Fact]
    public void Entwurf_liegt_ohne_Zutun_als_Version1_in_der_Bibliothek()
    {
        var p = NeuesProjekt("Projekt A");
        var e = Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.Contains(e.Hinweise, h => h.Contains("als Version 1 geschrieben"));
        var x = Eintrag();
        Assert.Equal("Heizkoerper/Testheizkoerper_Typ_22_fiktiv__fam-test", x.Ordner);
        Assert.Equal(1, x.Version);
        Assert.Equal(HkName, x.Name);
        Assert.Equal("entwurf", x.Status);
        Assert.Equal(3, x.Typen);
        Assert.Equal("Projekt A", x.Projekt);
        var ordner = FamOrdner();
        Assert.True(File.Exists(Path.Combine(ordner, "familie.json")));
        Assert.Equal(["v0001.familie.json"], Versionsdateien());
        var v1 = VersionLesen(1);
        Assert.Equal(1, v1["version"]!.GetValue<int>());
        Assert.Equal(HkId, v1["id"]!.GetValue<string>());
        Assert.Equal(File.ReadAllText(Path.Combine(ordner, "familie.json")), File.ReadAllText(Path.Combine(ordner, "versionen", "v0001.familie.json")));
        // Log: Projekt, Akteur, Art, Zeit, Befehl, Status, Prüfsumme, Basis
        var log = Assert.Single(Log().Eintraege);
        Assert.Equal("version", log.Art);
        Assert.Equal("Projekt A", log.Projekt!.Name);
        Assert.Equal(p.Kopf.Id, log.Projekt.Id);
        Assert.NotNull(log.Projekt.Ordner);
        Assert.Equal("Christian", log.Akteur);
        Assert.Equal("mensch", log.AkteurArt);
        Assert.Equal("familie_entwurf", log.Befehl);
        Assert.Equal("entwurf", log.Status);
        Assert.Null(log.Basis);
        Assert.False(string.IsNullOrEmpty(log.Zeit));
        // eine Prüfsumme für Projekt, Datei und Log
        var ps = FamilienBibliothekLogik.Inhaltspruefsumme(Hole(p, "FAM-1"))!;
        Assert.Equal(ps, log.Pruefsumme);
        Assert.Equal(ps, FamilienBibliothekLogik.Inhaltspruefsumme(v1));
        Assert.Equal(ps, x.Pruefsumme);
        // Marker im selben Paket
        var m = Marker(p);
        Assert.Equal(HkId, m["id"]!.GetValue<string>());
        Assert.Equal(1, m["version"]!.GetValue<int>());
        Assert.Equal(ps, m["pruefsumme"]!.GetValue<string>());
        Assert.Equal(x.Ordner, m["datei"]!.GetValue<string>());
        Assert.Equal("geschrieben", m["art"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(m["zeit"]!.GetValue<string>()));
        Assert.Single(p.Verlauf, v => v.Befehl == "familie_entwurf");   // kein zweites Paket für den Marker
        Assert.Equal("aktuell", Art(p));
        Sauber();
    }

    [Fact]
    public void Trockenlauf_und_abgelehnter_Befehl_schreiben_nichts()
    {
        var p = NeuesProjekt("Projekt A");
        var t = Ok(p.Ausfuehren("familie_entwurf", Entwurf(), trockenlauf: true));
        Assert.Contains(t.Hinweise, h => h.Contains("würde als Version 1 geschrieben"));
        Assert.False(Directory.Exists(Bib) && Directory.EnumerateFileSystemEntries(Bib).Any(e => !Path.GetFileName(e).Equals("index.json")));
        var kaputt = Testheizkoerper();
        kaputt["typen"] = new JsonArray();
        Nein(p.Ausfuehren("familie_entwurf", Entwurf(kaputt)));
        Assert.Empty(p.Modell.DerKlasse("Familie"));
        Assert.False(File.Exists(Path.Combine(Bib, "index.json")) && Index().Familien.Count > 0);
        // Änderung im Trockenlauf: keine Version 2
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        var t2 = Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "neu")), trockenlauf: true));
        Assert.Contains(t2.Hinweise, h => h.Contains("würde als Version 2 geschrieben"));
        Assert.Equal(["v0001.familie.json"], Versionsdateien());
    }

    [Fact]
    public void Jede_wirksame_Aenderung_eine_Version_gleicher_Inhalt_keine()
    {
        var p = NeuesProjekt("Projekt A");
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Version 2"))));
        Assert.Equal(["v0001.familie.json", "v0002.familie.json"], Versionsdateien());
        Assert.Equal(2, Eintrag().Version);
        Assert.Equal(1, Log().Eintraege[1].Basis);
        Assert.Equal("Version 2", ((JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(FamOrdner(), "familie.json")))!)["beschreibung"]!.GetValue<string>());
        // gleicher Inhalt: kein Paket, keine Version
        Assert.Contains("Keine Änderung", Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Version 2")))).Meldung);
        // von Hand: gleiche Prüfsumme = keine neue Version
        var h = Ok(p.Ausfuehren("familie_in_bibliothek", new JsonObject { ["familien"] = new JsonArray("FAM-1") }));
        Assert.Contains(h.Hinweise, x => x.Contains("schon als Version 2") && x.Contains("keine neue Version"));
        Assert.Equal(2, Versionsdateien().Length);
        // Freigabe = neue Version mit Status und Freigabe im Log
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Equal(3, Eintrag().Version);
        Assert.Equal("freigegeben", Eintrag().Status);
        Assert.Equal("Christian", Log().Eintraege[2].Freigabe!["akteur"]!.GetValue<string>());
        Assert.Equal("freigegeben", VersionLesen(3)["status"]!.GetValue<string>());
        Assert.Equal("aktuell", Art(p));
        Sauber();
    }

    [Fact]
    public void KI_Entwurf_wird_geschrieben_Freigabe_und_als_aktuell_bleiben_Mensch()
    {
        var ki = NeuesProjekt("Projekt KI", "ki");
        Ok(ki.Ausfuehren("familie_entwurf", Entwurf()));
        var log = Assert.Single(Log().Eintraege);
        Assert.Equal("ki", log.AkteurArt);
        Assert.Equal("KI", log.Akteur);
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_in_bibliothek", new JsonObject { ["als_aktuell"] = true })).Fehler);
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
        // ohne als_aktuell darf die KI von Hand schreiben (gleicher Inhalt = nichts Neues)
        Ok(ki.Ausfuehren("familie_in_bibliothek", []));
        Assert.Single(Versionsdateien());
    }

    [Fact]
    public void Rueckgaengig_und_Wiederholen_sind_weitere_Versionen_Uebernahme_nicht()
    {
        var p = NeuesProjekt("Projekt A");
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        var p1 = FamilienBibliothekLogik.Inhaltspruefsumme(Hole(p, "FAM-1"));
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "B"))));
        var p2 = FamilienBibliothekLogik.Inhaltspruefsumme(Hole(p, "FAM-1"));
        var r = Ok(p.Rueckgaengig());
        Assert.Contains(r.Hinweise, h => h.Contains("als Version 3 geschrieben"));
        Assert.Equal(p1, FamilienBibliothekLogik.Inhaltspruefsumme(Hole(p, "FAM-1")));
        Assert.Equal(3, Eintrag().Version);
        Assert.Equal(p1, Eintrag().Pruefsumme);
        Assert.Equal(2, Log().Eintraege[2].Basis);
        Assert.Equal("rueckgaengig (familie_aendern)", Log().Eintraege[2].Befehl);
        Assert.Equal("aktuell", Art(p));
        Ok(p.Wiederholen());
        Assert.Equal(4, Eintrag().Version);
        Assert.Equal(p2, Eintrag().Pruefsumme);
        Assert.Equal("aktuell", Art(p));
        // eine weitere Änderung geht von der aktuellen Version aus (kein Abzweig)
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "C"))));
        Assert.Equal(5, Eintrag().Version);
        Assert.Empty(Eintrag().Abzweige);

        // Übernahme rückgängig: die Bibliothek bleibt
        var b = NeuesProjekt("Projekt B");
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = 2 }));
        Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }));
        Ok(b.Rueckgaengig());
        Assert.Equal(5, Eintrag().Version);
        Assert.Equal("neuere_version", Art(b));
    }

    [Fact]
    public void Beispielprojekt_schreibt_nicht()
    {
        var p = NeuesProjekt("Beispiel", "beispiel");
        var e = Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.DoesNotContain(e.Hinweise, h => h.StartsWith("Familienbibliothek"));
        Assert.False(File.Exists(Path.Combine(Bib, "index.json")) && Index().Familien.Count > 0);
    }

    // ------------------------------------------------------------------ 3. Befehle und Abfragen

    [Fact]
    public void Laden_aus_Index_mit_Version_und_klare_Fehler()
    {
        var a = NeuesProjekt("Projekt A");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "V2"))));
        var b = NeuesProjekt("Projekt B");
        var vor = BibEintrag(b)!;
        Assert.Equal("nicht_im_projekt", vor["stand"]!["art"]!.GetValue<string>());
        Assert.Null(vor["imProjekt"]);
        // Trockenlauf lädt nichts
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }, trockenlauf: true));
        Assert.Empty(b.Modell.DerKlasse("Familie"));
        // ältere Version über den Namen
        var e = Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkName, ["version"] = "1" }));
        Assert.Contains(e.Hinweise, h => h.Contains("aus der Familienbibliothek geladen") && h.Contains("Version 1"));
        var m = Marker(b);
        Assert.Equal(1, m["version"]!.GetValue<int>());
        Assert.Equal("geladen", m["art"]!.GetValue<string>());
        Assert.Equal(Eintrag().Ordner, m["datei"]!.GetValue<string>());
        var s = Stand(b);
        Assert.Equal("neuere_version", s["art"]!.GetValue<string>());
        Assert.Equal(1, s["version"]!.GetValue<int>());
        Assert.Equal(2, s["aktuelleVersion"]!.GetValue<int>());
        Assert.Equal("FAM-1", BibEintrag(b)!["imProjekt"]!.GetValue<string>());
        Assert.Single(Versionsdateien(), x => x == "v0002.familie.json");   // laden schreibt nichts zurück
        Assert.Equal(2, Versionsdateien().Length);
        // Fehler mit Vorschlag
        var n = Nein(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = "fam-gibt-es-nicht" }));
        Assert.Contains("gibt es in der Familienbibliothek nicht", n.Fehler);
        Assert.Contains(HkId, n.Vorschlag);
        var n2 = Nein(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = 7 }));
        Assert.Contains("Version 7", n2.Fehler);
        Assert.Contains("Versionen: 1, 2", n2.Vorschlag);
        Assert.Contains("fehlt", Nein(b.Ausfuehren("familie_laden", [])).Fehler);
        Assert.Contains("zugleich", Nein(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["datei"] = "x" })).Fehler);
        // über den Ordner bzw. die Datei der Bibliothek
        var c = NeuesProjekt("Projekt C");
        Ok(c.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Eintrag().Ordner + "/versionen/v0002.familie.json" }));
        Assert.Equal("aktuell", Art(c));
        // schon im Projekt: wie familie_aktualisieren
        var e3 = Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Assert.Contains(e3.Hinweise, h => h.Contains("schon im Projekt"));
        Assert.Equal("aktuell", Art(b));
        Assert.Single(b.Modell.DerKlasse("Familie"));
    }

    [Fact]
    public void Aktualisieren_mit_Vorschau_und_betroffenen_Elementen()
    {
        var a = NeuesProjekt("Projekt A");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        var b = NeuesProjekt("Projekt B");
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Ok(b.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(5000, 3000, 0), ["kennzeichen"] = "HK-1" }));
        Ok(b.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1400", ["punkt"] = new JsonArray(8000, 3000, 0), ["kennzeichen"] = "HK-2" }));
        double Z(string el) => ((JsonObject)Tuer.Abfrage(b, "anschlusslagen", new JsonObject { ["element"] = el }))["anschluesse"]!.AsArray()
            .Single(x => x!["name"]!.GetValue<string>() == "vl")!["lage"]![2]!.GetValue<double>();
        Assert.Equal(120, Z("HK-1"));   // bodenabstand 150 − stutzen 30
        // A: Stutzen 42 mm statt 30 mm, Leistung eines Typs anders
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", ParameterWert("stutzen", 42),
            new JsonObject { ["op"] = "typ_setzen", ["typ"] = new JsonObject { ["name"] = "22-600-1000", ["werte"] = new JsonObject { ["hoehe"] = 600, ["laenge"] = 1000, ["leistung"] = 1050 } } })));
        Assert.Equal("neuere_version", Art(b));
        Assert.Equal(120, Z("HK-1"));   // nie automatisch
        // Vorschau (Trockenlauf)
        var v = Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }, trockenlauf: true));
        Assert.StartsWith("Vorschau FAM-1", v.Hinweise[0]);
        Assert.Contains("2 von 2 Element(en) betroffen", v.Hinweise[0]);
        Assert.Contains(v.Hinweise, h => h == "HK-1: Anschluss vl verschiebt sich um 12 mm");
        Assert.Contains(v.Hinweise, h => h == "HK-2: Anschluss rl verschiebt sich um 12 mm");
        Assert.Contains(v.Hinweise, h => h.StartsWith("Parameter «stutzen»: wert 30 → 42"));
        Assert.Contains(v.Hinweise, h => h.StartsWith("Typ «22-600-1000»: leistung 1000 → 1050"));
        Assert.Equal(120, Z("HK-1"));
        Assert.Equal("neuere_version", Art(b));
        // strukturierte Vorschau für die Oberfläche
        var d = Bibliothek(b, new JsonObject { ["familie"] = "FAM-1" })["familie"]!;
        Assert.Equal(2, d["versionen"]!.AsArray().Count);
        Assert.Equal("HK-1", d["vorschau"]!["elemente"]![0]!["element"]!.GetValue<string>());
        Assert.Contains("verschiebt sich um 12 mm", d["vorschau"]!["elemente"]![0]!["meldungen"]![0]!.GetValue<string>());
        Assert.Equal(2, d["log"]!.AsArray().Count);
        // Übernahme
        var e = Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Contains(e.Hinweise, h => h.Contains("aus der Familienbibliothek übernommen") && h.Contains("Version 2"));
        Assert.Equal(108, Z("HK-1"));
        Assert.Equal(1050, Hole(b, "HK-1").Zahl("leistung"));
        Assert.Equal("aktuell", Art(b));
        Assert.Equal(2, Versionsdateien().Length);   // übernehmen schreibt nicht zurück
        Assert.Contains("Keine Änderung", Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" })).Hinweise[0]);
    }

    [Fact]
    public void Aktualisieren_Typ_entfaellt_klarer_Fehler_mit_Vorschlag()
    {
        var a = NeuesProjekt("Projekt A");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        var b = NeuesProjekt("Projekt B");
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Ok(b.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1400", ["punkt"] = new JsonArray(5000, 3000, 0), ["kennzeichen"] = "HK-1" }));
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", new JsonObject { ["op"] = "typ_loeschen", ["name"] = "22-600-1400" })));
        var n = Nein(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }, trockenlauf: true));
        Assert.Contains("Typ «22-600-1400» entfällt – 1 Element(e) (HK-1)", n.Fehler);
        Assert.Contains("andere Version", n.Vorschlag);
        var d = Bibliothek(b, new JsonObject { ["familie"] = HkId })["familie"]!;
        Assert.Equal("22-600-1400", d["vorschau"]!["fehlendeTypen"]![0]!["typ"]!.GetValue<string>());
        // Version bzw. Abzweig unbekannt
        var n9 = Nein(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1", ["version"] = "v9" }));
        Assert.Contains("Version 9", n9.Fehler);
        Assert.Contains("Versionen: 1, 2", n9.Vorschlag);
        Assert.Contains("weder eine Version noch ein Abzweig", Nein(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1", ["version"] = "quatsch" })).Fehler);
    }

    [Fact]
    public void Familienauswahl_je_Projekt_und_Sichtbarkeit()
    {
        var a = NeuesProjekt("Projekt A");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        var ts = (JsonObject)JsonNode.Parse(File.ReadAllText(Modul3Hilfe.Datei("test_tstueck")))!;
        Ok(a.Ausfuehren("familie_entwurf", Entwurf(ts)));
        var tsId = ts["id"]!.GetValue<string>();
        var b = NeuesProjekt("Projekt B");
        // ohne Auswahl: nur die Familien des Projekts
        Assert.False(BibEintrag(b)!["sichtbar"]!.GetValue<bool>());
        Assert.Empty(Bibliothek(b, new JsonObject { ["auswahl"] = true })["familien"]!.AsArray());
        // Kategorie
        var e = Ok(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["kategorien"] = new JsonArray("Heizkoerper") } }));
        Assert.Contains(e.Hinweise, h => h.Contains("1 von 2 Bibliotheksfamilien sichtbar"));
        var sichtbar = Bibliothek(b, new JsonObject { ["auswahl"] = true })["familien"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()).ToList();
        Assert.Equal([HkId], sichtbar);
        Assert.Equal("Heizkoerper", ((JsonObject)Hole(b, "PI-1").Eigenschaft("familienAuswahl")!.Inhalt!)["kategorien"]![0]!.GetValue<string>());
        // aus geht vor, ein zeigt immer
        Ok(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject
        {
            ["kategorien"] = new JsonArray("Heizkoerper"), ["aus"] = new JsonArray(HkId), ["ein"] = new JsonArray(tsId), ["status"] = new JsonArray("freigegeben"),
        } }));
        sichtbar = Bibliothek(b, new JsonObject { ["auswahl"] = true })["familien"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()).ToList();
        Assert.Equal([tsId], sichtbar);
        // im Projekt immer sichtbar
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Assert.True(BibEintrag(b)!["sichtbar"]!.GetValue<bool>());
        // Gewerk, Hersteller
        Ok(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["gewerke"] = new JsonArray("heizung"), ["hersteller"] = new JsonArray("niemand") } }));
        Assert.False(BibEintrag(b, tsId)!["sichtbar"]!.GetValue<bool>());
        // Schnellsuche
        Assert.Equal([tsId], Bibliothek(b, new JsonObject { ["filter"] = "T-STÜCK" })["familien"]!.AsArray().Where(x => x!["quelle"]!.GetValue<string>() == "bibliothek").Select(x => x!["id"]!.GetValue<string>()).ToList());
        // Fehler mit Vorschlag
        Assert.Contains("Felder", Nein(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["farbe"] = new JsonArray("rot") } })).Vorschlag);
        Assert.Contains("entwurf, freigegeben", Nein(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["status"] = new JsonArray("alt") } })).Vorschlag);
        Assert.Contains("Heizkoerper", Nein(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["kategorien"] = new JsonArray("Wand") } })).Vorschlag);
        Nein(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["kategorien"] = "Heizkoerper" } }));
        // entfernen: wieder nur die Familien des Projekts
        var r = Ok(b.Ausfuehren("familienauswahl_setzen", []));
        Assert.Contains(r.Hinweise, h => h.Contains("entfernt"));
        Assert.Null(Hole(b, "PI-1").Eigenschaft("familienAuswahl"));
        Assert.False(BibEintrag(b, tsId)!["sichtbar"]!.GetValue<bool>());
    }

    [Fact]
    public void Doppel_gleicher_Hersteller_und_Produkt_werden_gemeldet()
    {
        var a = NeuesProjekt("Projekt A");
        var d1 = Testheizkoerper(); d1["produkt"] = "Kompakt 22";
        var d2 = Testheizkoerper(); d2["produkt"] = " kompakt 22 "; d2["id"] = "fam-test-heizkoerper-zwei"; d2["name"] = "Testheizkörper zwei (fiktiv)";
        Ok(a.Ausfuehren("familie_entwurf", Entwurf(d1)));
        Ok(a.Ausfuehren("familie_entwurf", Entwurf(d2)));
        var l = Bibliothek(a);
        var dop = Assert.Single(l["doppel"]!.AsArray())!;
        Assert.Equal(2, dop["familien"]!.AsArray().Count);
        Assert.Equal("fam-test-heizkoerper-zwei", BibEintrag(a)!["doppel"]![0]!.GetValue<string>());
        Assert.Equal(2, Index().Familien.Count);   // gemeldet, nicht zusammengeführt
    }

    [Fact]
    public void Speichern_ohne_datei_ist_der_Weg_in_die_Bibliothek()
    {
        var p = NeuesProjekt("Projekt A");
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, null);
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));   // gesperrt: nicht geschrieben
        Environment.SetEnvironmentVariable(FamilienBibliothekDienst.Umgebungsvariable, Bib);
        Assert.Equal("nicht_in_bibliothek", Art(p));
        var e = Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1" }));   // Entwurf: erlaubt (alle Familien)
        Assert.Contains(e.Hinweise, h => h.Contains("als Version 1 geschrieben"));
        Assert.Equal("familie_speichern", Assert.Single(Log().Eintraege).Befehl);
        Assert.Equal(1, Marker(p)["version"]!.GetValue<int>());
        Assert.Equal("aktuell", Art(p));
        Assert.Contains("Das Projekt hat keine Familien", Nein(NeuesProjekt("Leer").Ausfuehren("familie_in_bibliothek", [])).Fehler);
        Assert.Contains("gibt es nicht", Nein(p.Ausfuehren("familie_in_bibliothek", new JsonObject { ["familien"] = new JsonArray("FAM-9") })).Fehler);
    }

    // ------------------------------------------------------------------ Sperre, Warteschlange, Index

    [Fact]
    public void Sperre_eines_anderen_Rechners_Warteschlange_abgelaufene_Sperre_wird_uebernommen()
    {
        var p = NeuesProjekt("Projekt A");
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        var sperre = Path.Combine(FamOrdner(), ".sperre");
        File.WriteAllText(sperre, new JsonObject { ["rechner"] = "ANDERER-PC", ["prozess"] = 1, ["zeit"] = DateTimeOffset.UtcNow.ToString("o") }.ToJsonString());
        FamilienBibliothekDienst.SperreWarten = TimeSpan.FromMilliseconds(300);
        var e = Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "gesperrt"))));
        Assert.Contains(e.Hinweise, h => h.Contains("Warteschlange") && h.Contains("gesperrt"));
        Assert.Single(FamilienBibliothekDienst.Ausstehend(Ort));
        Assert.Single(Versionsdateien());
        Assert.True(Stand(p)["warteschlange"]!.GetValue<bool>());
        Assert.Equal("im_projekt_geaendert", Art(p));
        File.Delete(sperre);
        var l = Bibliothek(p);
        Assert.Equal(1, l["bibliothek"]!["nachgeholt"]!.GetValue<int>());
        Assert.Equal(2, Versionsdateien().Length);
        Assert.Empty(FamilienBibliothekDienst.Ausstehend(Ort));
        Assert.Equal("aktuell", Art(p));
        // abgelaufene Sperre (älter als 2 min) wird übernommen
        File.WriteAllText(sperre, new JsonObject { ["rechner"] = "ANDERER-PC", ["prozess"] = 1, ["zeit"] = DateTimeOffset.UtcNow.AddMinutes(-3).ToString("o") }.ToJsonString());
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "abgelaufen"))));
        Assert.Equal(3, Versionsdateien().Length);
        Sauber();
    }

    [Fact]
    public void Ohne_NAS_laedt_ein_anderes_Projekt_die_Familie_aus_dem_Spiegel()
    {
        var a = NeuesProjekt("Projekt A");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        var b = NeuesProjekt("Projekt B");
        Assert.NotNull(BibEintrag(b));   // liest den Index online und spiegelt Version 1
        NasWeg();
        var e = Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Assert.Contains(e.Aenderungen, x => x.Klasse == "Familie");
        Assert.Equal("FAM-1", BibEintrag(b)!["imProjekt"]!.GetValue<string>());
        NasDa();
    }

    [Fact]
    public void Ohne_NAS_Warteschlange_Lesen_aus_dem_gemerkten_Stand_nachgeschrieben_beim_naechsten_Zugriff()
    {
        var p = NeuesProjekt("Projekt A");
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.Equal("aktuell", Art(p));   // liest und merkt den Index lokal
        NasWeg();
        var e = Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "offline 1"))));
        Assert.Contains(e.Hinweise, h => h.Contains("nicht erreichbar") && h.Contains("Warteschlange"));
        Assert.Equal("ausstehend", Marker(p)["art"]!.GetValue<string>());
        Assert.Equal(1, Marker(p)["basis"]!.GetValue<int>());
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "offline 2"))));
        Assert.Equal(2, FamilienBibliothekDienst.Ausstehend(Ort).Count);
        var s = Stand(p);
        Assert.Equal("im_projekt_geaendert", s["art"]!.GetValue<string>());
        Assert.True(s["warteschlange"]!.GetValue<bool>());
        Assert.True(s["offline"]!.GetValue<bool>());
        var l = Bibliothek(p);
        Assert.True(l["bibliothek"]!["offline"]!.GetValue<bool>());
        Assert.Contains("nicht erreichbar", l["bibliothek"]!["hinweis"]!.GetValue<string>());
        Assert.Equal(2, l["bibliothek"]!["warteschlange"]!.GetValue<int>());
        Assert.NotNull(BibEintrag(p));   // letzter Stand
        Assert.Contains("nicht erreichbar", Nein(p.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" })).Fehler);
        NasDa();
        Assert.Equal(2, Bibliothek(p)["bibliothek"]!["nachgeholt"]!.GetValue<int>());
        Assert.Equal(3, Eintrag().Version);
        Assert.Equal(["v0001.familie.json", "v0002.familie.json", "v0003.familie.json"], Versionsdateien());
        Assert.Equal(2, Log().Eintraege[2].Basis);
        Assert.Equal("offline 2", VersionLesen(3)["beschreibung"]!.GetValue<string>());
        Assert.Equal("aktuell", Art(p));
        Assert.Empty(Eintrag().Abzweige);
    }

    [Fact]
    public void Index_zwischengespeichert_nach_Aenderungszeit_und_aus_den_Ordnern_neu_aufgebaut()
    {
        var p = NeuesProjekt("Projekt A");
        Ok(p.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.Equal(HkName, BibEintrag(p)!["name"]!.GetValue<string>());
        // anderer Rechner schreibt den Index (simuliert): neue Änderungszeit → neu gelesen
        var pfad = Path.Combine(Bib, "index.json");
        var text = File.ReadAllText(pfad).Replace(HkName, "Umbenannt (fiktiv)");
        File.WriteAllText(pfad, text);
        File.SetLastWriteTimeUtc(pfad, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal("Umbenannt (fiktiv)", BibEintrag(p)!["name"]!.GetValue<string>());
        // Index fehlt: aus den Familienordnern neu
        File.Delete(pfad);
        Assert.Equal(HkName, BibEintrag(p)!["name"]!.GetValue<string>());
        Assert.True(File.Exists(pfad));
        // unlesbarer bzw. als veraltet markierter Index: neu aufgebaut
        File.WriteAllText(pfad, "{ kaputt");
        Assert.Equal(HkName, BibEintrag(p)!["name"]!.GetValue<string>());
        File.WriteAllText(Path.Combine(Bib, "index.veraltet"), "x");
        Assert.Equal(HkName, BibEintrag(p)!["name"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(Bib, "index.veraltet")));
        // Abbruch zwischen Versionsdatei und Log (simuliert): die Version wird beim nächsten Schreiben aus dem Ordner nachgetragen
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "zwei"))));
        var ordner = FamOrdner();
        var log = Log();
        log.Eintraege.RemoveAll(x => x.Version == 2);
        File.WriteAllText(Path.Combine(ordner, "log.json"), System.Text.Json.JsonSerializer.Serialize(log, Json.Optionen));
        File.Delete(pfad);
        Ok(p.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "drei"))));
        Assert.Equal(3, Eintrag().Version);
        Assert.Equal("wiederhergestellt", Log().Eintraege.Single(x => x.Version == 2).Befehl);
        Assert.Equal(2, Log().Eintraege.Single(x => x.Version == 3).Basis);
        Assert.Empty(Eintrag().Abzweige);
    }
}
