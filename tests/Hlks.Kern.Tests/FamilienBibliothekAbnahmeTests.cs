using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, Nachtrag B7: «Fertig, wenn …» der Spezifikation als durchgehender Test mit zwei Projekten, dazu parallele
/// Änderungen (Abzweig, beide melden, Entscheid nur Mensch), zwei Rechner am selben Projekt (keine doppelten Versionen) und
/// Unterlagen, die mit der Familie reisen (Befund-Ids gelten im Zielprojekt). Bibliothek nur unter %TEMP%. Alle Werte FIKTIV.</summary>
[Collection("Familienbibliothek")]
public sealed class FamilienBibliothekAbnahmeTests : BibliothekTestBasis
{
    static JsonObject Entwurf(JsonObject? def = null) => new() { ["definition"] = def ?? Testheizkoerper() };

    double VlZ(Projekt p, string el) => ((JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = el }))["anschluesse"]!.AsArray()
        .Single(x => x!["name"]!.GetValue<string>() == "vl")!["lage"]![2]!.GetValue<double>();

    [Fact]
    public void FertigWenn_zwei_Projekte_von_der_Erstellung_bis_zum_Nachschreiben_ohne_NAS()
    {
        var a = NeuesProjekt("Projekt A");
        var b = NeuesProjekt("Projekt B");

        // 1. In A erstellt → ohne weiteres Zutun in der Bibliothek
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        Assert.Equal(["v0001.familie.json"], Versionsdateien());
        Assert.Equal("aktuell", Art(a));

        // 2. B zeigt sie im Explorer, sobald die Auswahl des Projekts sie umfasst, und kann sie platzieren
        Assert.False(BibEintrag(b)!["sichtbar"]!.GetValue<bool>());
        Ok(b.Ausfuehren("familienauswahl_setzen", new JsonObject { ["auswahl"] = new JsonObject { ["kategorien"] = new JsonArray("Heizkoerper") } }));
        var eintrag = Bibliothek(b, new JsonObject { ["auswahl"] = true })["familien"]!.AsArray().Single()!;
        Assert.Equal(HkId, eintrag["id"]!.GetValue<string>());
        Assert.Equal("nicht_im_projekt", eintrag["stand"]!["art"]!.GetValue<string>());
        Assert.Equal("heizung", eintrag["gewerk"]!.GetValue<string>());
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Ok(b.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(5000, 3000, 0), ["kennzeichen"] = "HK-1" }));
        Assert.Equal("aktuell", Art(b));
        Assert.Equal(120, VlZ(b, "HK-1"));

        // 3. Änderung in A erscheint in B als «neuere Version» und wird erst per Knopf mit Vorschau übernommen
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", ParameterWert("stutzen", 42))));
        Assert.Equal(2, Eintrag().Version);
        var s = Stand(b);
        Assert.Equal("neuere_version", s["art"]!.GetValue<string>());
        Assert.Equal(1, s["version"]!.GetValue<int>());
        Assert.Equal(2, s["aktuelleVersion"]!.GetValue<int>());
        Assert.Equal(120, VlZ(b, "HK-1"));   // nie automatisch
        var vorschau = Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }, trockenlauf: true));
        Assert.Contains(vorschau.Hinweise, h => h == "HK-1: Anschluss vl verschiebt sich um 12 mm");
        Assert.Equal(120, VlZ(b, "HK-1"));
        Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Equal(108, VlZ(b, "HK-1"));
        Assert.Equal("aktuell", Art(b));

        // 4. Parallele Änderung bleibt als Abzweig erhalten, beide Projekte melden es
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Fassung A"))));
        var eb = Ok(b.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Fassung B"))));
        Assert.Contains(eb.Hinweise, h => h.Contains("parallel geändert") && h.Contains("als Abzweig v0002-Projekt_B-"));
        Assert.Equal(3, Eintrag().Version);
        var ab = Assert.Single(Eintrag().Abzweige);
        Assert.True(ab.Offen);
        Assert.Equal(2, ab.Basis);
        Assert.Equal("Projekt B", ab.Projekt);
        Assert.True(File.Exists(Path.Combine(FamOrdner(), "abzweige", ab.Abzweig + ".familie.json")));
        Assert.Equal("Fassung A", ((JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(FamOrdner(), "familie.json")))!)["beschreibung"]!.GetValue<string>());
        Assert.Equal("parallel_geaendert", Art(a));
        Assert.Equal("parallel_geaendert", Art(b));
        Assert.Equal(ab.Abzweig, Stand(b)["abzweig"]!.GetValue<string>());
        Assert.Equal(ab.Abzweig, Stand(a)["abzweigeOffen"]![0]!.GetValue<string>());

        // 5. Ohne NAS weitergearbeitet und später nachgeschrieben
        NasWeg();
        var off = Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "A ohne NAS"))));
        Assert.Contains(off.Hinweise, h => h.Contains("Warteschlange"));
        var so = Stand(a);
        Assert.Equal("im_projekt_geaendert", so["art"]!.GetValue<string>());
        Assert.True(so["warteschlange"]!.GetValue<bool>());
        Assert.True(Bibliothek(b)["bibliothek"]!["offline"]!.GetValue<bool>());
        NasDa();
        Assert.Equal(1, Bibliothek(b)["bibliothek"]!["nachgeholt"]!.GetValue<int>());   // nächster Zugriff, auch aus einem anderen Projekt
        Assert.Equal(4, Eintrag().Version);
        Assert.Equal("A ohne NAS", VersionLesen(4)["beschreibung"]!.GetValue<string>());
        Assert.Equal(4, Stand(a)["version"]!.GetValue<int>());
        Assert.Equal("parallel_geaendert", Art(a));   // der Abzweig von B ist noch offen
        Sauber();
    }

    [Fact]
    public void Parallel_geaendert_Mensch_uebernimmt_die_Bibliothek_KI_wird_abgelehnt()
    {
        var (a, b, abzweig) = Parallel();
        // die KI entscheidet nicht
        Assert.Contains("nur ein Mensch", Assert.Throws<HlksFehler>(() => new FamilieAktualisieren().Ausfuehren(Als(b, "ki"), new JsonObject { ["familie"] = "FAM-1" })).Message);
        Assert.Contains("nur ein Mensch", Assert.Throws<HlksFehler>(() => new FamilieInBibliothek().Ausfuehren(Als(b, "ki"), new JsonObject { ["als_aktuell"] = true })).Message);
        // Mensch in B: Bibliothek übernehmen, die eigene Fassung bleibt als Abzweig erhalten und gilt als verworfen
        var e = Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Contains(e.Hinweise, h => h.Contains("als verworfen vermerkt"));
        var entscheid = Log().Eintraege.Last();
        Assert.Equal("entscheid", entscheid.Art);
        Assert.Equal("verworfen", entscheid.Ergebnis);
        Assert.Equal([abzweig], entscheid.Abzweige!);
        Assert.Equal("Projekt B", entscheid.Projekt!.Name);
        Assert.False(Assert.Single(Eintrag().Abzweige).Offen);
        Assert.True(File.Exists(Path.Combine(FamOrdner(), "abzweige", abzweig + ".familie.json")));   // nichts geht verloren
        Assert.Equal("aktuell", Art(a));
        Assert.Equal("aktuell", Art(b));
        Assert.Equal("Fassung A", Definition(b)["beschreibung"]!.GetValue<string>());
        // den Abzweig kann ein Mensch später wieder laden (zum Zusammenführen)
        var c = NeuesProjekt("Projekt C");
        Ok(c.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = abzweig }));
        Assert.Equal("Fassung B", Definition(c)["beschreibung"]!.GetValue<string>());
        Assert.Equal("neuere_version", Art(c));   // entschiedener Abzweig
    }

    [Fact]
    public void Parallel_geaendert_als_aktuell_setzt_die_eigene_Fassung()
    {
        var (a, b, abzweig) = Parallel();
        var t = Ok(b.Ausfuehren("familie_in_bibliothek", new JsonObject { ["familien"] = new JsonArray("FAM-1"), ["als_aktuell"] = true }, trockenlauf: true));
        Assert.Contains(t.Hinweise, h => h.Contains("würde als Version 3 geschrieben"));
        Assert.Equal(2, Eintrag().Version);
        var e = Ok(b.Ausfuehren("familie_in_bibliothek", new JsonObject { ["familien"] = new JsonArray("FAM-1"), ["als_aktuell"] = true }));
        Assert.Contains(e.Hinweise, h => h.Contains("als Version 3 geschrieben") && h.Contains(abzweig));
        Assert.Equal(3, Eintrag().Version);
        Assert.Equal("Fassung B", VersionLesen(3)["beschreibung"]!.GetValue<string>());
        var entscheid = Log().Eintraege.Last();
        Assert.Equal("entscheid", entscheid.Art);
        Assert.Equal("ersetzt", entscheid.Ergebnis);
        Assert.Equal(3, entscheid.Version);
        Assert.Equal("aktuell", Art(b));
        var sa = Stand(a);
        Assert.Equal("neuere_version", sa["art"]!.GetValue<string>());
        Assert.Equal(3, sa["aktuelleVersion"]!.GetValue<int>());
    }

    /// <summary>A erstellt (v1), B lädt; A ändert (v2), B ändert von v1 aus → Abzweig.</summary>
    (Projekt A, Projekt B, string Abzweig) Parallel()
    {
        var a = NeuesProjekt("Projekt A");
        var b = NeuesProjekt("Projekt B");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Fassung A"))));
        Ok(b.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "Fassung B"))));
        Assert.Equal(2, Eintrag().Version);
        var ab = Assert.Single(Eintrag().Abzweige);
        Assert.StartsWith("v0001-Projekt_B-", ab.Abzweig);
        Assert.Equal("parallel_geaendert", Art(a));
        Assert.Equal("parallel_geaendert", Art(b));
        return (a, b, ab.Abzweig);
    }

    [Fact]
    public void Zwei_Rechner_am_selben_Projekt_erzeugen_keine_doppelten_Versionen()
    {
        Rechner("pc1");
        var p1 = NeuesProjekt("Gemeinsam", geraet: "PC1");
        Rechner("pc2");
        var p2 = Oeffnen("Gemeinsam", "PC2");
        Rechner("pc1");
        Ok(p1.Ausfuehren("familie_entwurf", Entwurf()));
        Rechner("pc2");
        p2.Synchronisieren();   // fremdes Paket: PC2 schreibt nichts
        Assert.Single(Versionsdateien());
        var h = Ok(p2.Ausfuehren("familie_in_bibliothek", []));
        Assert.Contains(h.Hinweise, x => x.Contains("keine neue Version"));
        Assert.Single(Versionsdateien());
        Rechner("pc1");
        p1.Synchronisieren();
        Ok(p1.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "zwei"))));
        Assert.Equal(2, Versionsdateien().Length);
        Assert.Equal("aktuell", Art(p1));   // merkt den Index lokal (PC1)

        // PC1 ohne NAS: Änderung in der Warteschlange von PC1; PC2 (mit NAS) schreibt dieselbe Fassung von Hand – kein Doppel
        NasWeg();
        Ok(p1.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "drei"))));
        Assert.Single(FamilienBibliothekDienst.Ausstehend(Ort));
        NasDa();
        Rechner("pc2");
        Assert.Empty(FamilienBibliothekDienst.Ausstehend(Ort));   // die Warteschlange ist je Rechner
        p2.Synchronisieren();
        Assert.Equal("ausstehend", Marker(p2)["art"]!.GetValue<string>());
        var e2 = Ok(p2.Ausfuehren("familie_in_bibliothek", []));
        Assert.Contains(e2.Hinweise, x => x.Contains("als Version 3 geschrieben"));
        Assert.Equal(2, Log().Eintraege.Single(x => x.Version == 3).Basis);
        Rechner("pc1");
        p1.Synchronisieren();
        Assert.Equal(0, Bibliothek(p1)["bibliothek"]!["warteschlange"]!.GetValue<int>());   // nachgeholt: gleiche Prüfsumme = keine neue Version
        Assert.Equal(3, Versionsdateien().Length);
        Assert.Empty(Eintrag().Abzweige);
        Assert.Equal("aktuell", Art(p1));
        Assert.Equal("aktuell", Art(p2));
    }

    [Fact]
    public void Unterlagen_reisen_mit_Befund_Ids_und_Quellen_gelten_im_Zielprojekt()
    {
        using var o = new GrundlagenHilfe.Ordner();
        var a = NeuesProjekt("Projekt A");
        var pdf = Ablegen(a, UnterlagenHilfe.TestPdf(o));
        var zelle = Zelle(pdf, 1, 3);
        Ok(a.Ausfuehren("familie_entwurf", Entwurf(Ventil(pdf))));
        var id = Familien.DefinitionsId(Hole(a, "FAM-1"))!;
        // einmal je Prüfsumme in _unterlagen
        var u = Assert.Single((JsonArray)Definition(a)["unterlagen"]!)!;
        var ps = u["pruefsumme"]!.GetValue<string>();
        var abgelegt = Path.Combine(Bib, "_unterlagen", ps + ".pdf");
        Assert.True(File.Exists(abgelegt));
        Assert.Equal(ps, BefundQuelle.Pruefsumme(abgelegt));
        Assert.Equal(ps, Assert.Single(Log(id).Eintraege).Unterlagen!.Single().Pruefsumme);
        var v1 = VersionLesen(1, id);
        Assert.Null(v1["unterlagen"]![0]!["unterlage"]);   // Element-Id gilt nur im eigenen Projekt
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "zweite Version"))));
        Assert.Single(Directory.GetFiles(Path.Combine(Bib, "_unterlagen")));

        // Trockenlauf kopiert nichts
        var c = NeuesProjekt("Projekt C");
        var t = Ok(c.Ausfuehren("familie_laden", new JsonObject { ["id"] = id }, trockenlauf: true));
        Assert.Contains(t.Hinweise, h => h.Contains("würden ins Projekt kopiert"));
        Assert.Empty(Directory.GetFiles(Unterlagen.Ordner(c), ps[..12] + "_*"));

        // B: Laden kopiert die Unterlage (Prüfsumme bleibt) und fügt sie als Unterlage der Familie ein
        var b = NeuesProjekt("Projekt B");
        Assert.NotEqual(Unterlagen.Ordner(a), Unterlagen.Ordner(b));
        var e = Ok(b.Ausfuehren("familie_laden", new JsonObject { ["id"] = id }));
        Assert.Contains(e.Hinweise, h => h.Contains("1 Unterlage(n) ins Projekt kopiert, 1 als Unterlage eingefügt"));
        var kopie = Path.Combine(Unterlagen.Ordner(b), ps[..12] + "_datenblatt.pdf");
        Assert.True(File.Exists(kopie));
        Assert.Equal(ps, BefundQuelle.Pruefsumme(kopie));
        var ul = Assert.Single(b.Modell.DerKlasse("Unterlage"));
        Assert.Equal("familie", ul.Text("zweck"));
        Assert.Equal(Hole(b, "FAM-1").Id, ul.Bezuege["familie"]);
        // Befund-Ids und Quellen gelten in B
        var q = new BefundQuelle(b);
        Assert.NotNull(q.Hole(zelle, out var fehler));
        Assert.Null(fehler);
        Assert.Equal("geprüft", Abdeckung(b, "V-15/L")["status"]!.GetValue<string>());
        Assert.Equal("aktuell", Art(b));   // gleiche Inhaltsprüfsumme wie in der Bibliothek
        Ok(b.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "in B geändert"))));   // Quellen werden in B geprüft
        var d = (JsonObject)Definition(b).DeepClone();
        TypWert(d, "V-15", "H", 35, "fedcba987654#3");
        Assert.Contains("keine Unterlage", Nein(b.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d, ["familie"] = "FAM-1" })).Fehler);
        // zweites Laden in ein Projekt, das die Unterlage schon hat: nicht doppelt
        Ok(b.Ausfuehren("familie_aktualisieren", new JsonObject { ["familie"] = "FAM-1", ["version"] = 2 }));
        Assert.Single(b.Modell.DerKlasse("Unterlage"));
    }
}
