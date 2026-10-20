using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Q1 (Befund C3): familie_laden einer Familie, die (gleiche id) schon im Projekt ist, wirkt wie familie_aktualisieren –
/// eine Versionsübernahme. Die KI darf das nicht ohne Mensch: der Kern lehnt es für den Akteur «ki» ab. Neue Familien darf die KI laden,
/// ein Mensch darf beides. Bibliothek nur unter %TEMP% (BibliothekTestBasis). Alle Familien FIKTIV.</summary>
[Collection("Familienbibliothek")]
public sealed class FamilieLadenKiTests : BibliothekTestBasis
{
    static JsonObject Entwurf() => new() { ["definition"] = Testheizkoerper() };

    /// <summary>Bibliothek mit dem Testheizkörper in den Versionen 1 und 2 (geschrieben von einem Menschen).</summary>
    void BibliothekFuellen()
    {
        var a = NeuesProjekt("Quelle");
        Ok(a.Ausfuehren("familie_entwurf", Entwurf()));
        Ok(a.Ausfuehren("familie_aendern", Aendern("FAM-1", Kopf("beschreibung", "V2"))));
        Assert.Equal(2, Eintrag().Version);
    }

    [Fact]
    public void KI_laedt_neue_Familie_aus_der_Bibliothek()
    {
        BibliothekFuellen();
        var ki = NeuesProjekt("KI neu", "ki");
        Ok(ki.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Assert.Single(ki.Modell.DerKlasse("Familie"));
        Assert.Equal("aktuell", Art(ki));
    }

    [Fact]
    public void KI_laedt_neue_Familie_aus_einer_Datei()
    {
        BibliothekFuellen();
        var ki = NeuesProjekt("KI Datei", "ki");
        // Sicherheitsprüfung 3c, B3a: die KI lädt Dateien nur unter dem Projektordner bzw. den Wurzeln aus kataloge/buero.json
        var datei = Path.Combine(ki.Speicher!.Ort, "heizkoerper.familie.json");
        File.WriteAllText(datei, VersionLesen(2).ToJsonString());
        Ok(ki.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
        Assert.Single(ki.Modell.DerKlasse("Familie"));
        // ausserhalb (Testwurzel neben Projekt und Bibliothek): abgelehnt
        var aussen = Path.Combine(Wurzel, "heizkoerper.familie.json");
        File.WriteAllText(aussen, VersionLesen(2).ToJsonString());
        Assert.StartsWith(KiPfade.Meldung, Nein(NeuesProjekt("KI Datei aussen", "ki").Ausfuehren("familie_laden", new JsonObject { ["datei"] = aussen })).Fehler);
    }

    [Fact]
    public void KI_mit_vorhandener_Familie_wird_abgelehnt_Bibliothek()
    {
        BibliothekFuellen();
        var ki = NeuesProjekt("KI vorhanden", "ki");
        Ok(ki.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = 1 }));
        var vorher = Hole(ki, "FAM-1");
        var version = FamilienBefehle.Version(vorher);

        // gleiche id, neuere Version in der Bibliothek: die KI darf sie nicht übernehmen (auch nicht als Trockenlauf)
        foreach (var trocken in new[] { false, true })
        {
            var n = Nein(ki.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }, trockenlauf: trocken));
            Assert.Contains("Eine neuere Fassung übernimmt nur ein Mensch", n.Fehler);
            Assert.Contains("FAM-1", n.Fehler);
            Assert.Contains("familie_aktualisieren", n.Vorschlag);
        }
        // über den Namen und über die Datei der Bibliothek ebenso
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkName })).Fehler);
        Assert.Contains("nur ein Mensch", Nein(ki.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Eintrag().Ordner + "/versionen/v0002.familie.json" })).Fehler);

        // nichts hat sich geändert
        Assert.Single(ki.Modell.DerKlasse("Familie"));
        Assert.Equal(version, FamilienBefehle.Version(Hole(ki, "FAM-1")));
        Assert.Equal("neuere_version", Art(ki));
    }

    [Fact]
    public void KI_mit_vorhandener_Familie_wird_abgelehnt_Datei()
    {
        BibliothekFuellen();
        var ki = NeuesProjekt("KI Datei vorhanden", "ki");
        var datei = Path.Combine(ki.Speicher!.Ort, "heizkoerper.familie.json");   // unter dem Projektordner (Sicherheitsprüfung 3c, B3a)
        File.WriteAllText(datei, VersionLesen(1).ToJsonString());
        Ok(ki.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
        var version = FamilienBefehle.Version(Hole(ki, "FAM-1"));

        File.WriteAllText(datei, VersionLesen(2).ToJsonString());
        var n = Nein(ki.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
        Assert.Contains("Eine neuere Fassung übernimmt nur ein Mensch", n.Fehler);
        Assert.Equal(version, FamilienBefehle.Version(Hole(ki, "FAM-1")));
        Assert.Single(ki.Modell.DerKlasse("Familie"));
    }

    [Fact]
    public void Mensch_uebernimmt_neuere_Fassung_einer_vorhandenen_Familie()
    {
        BibliothekFuellen();
        var mensch = NeuesProjekt("Mensch vorhanden");
        Ok(mensch.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = 1 }));
        Assert.Equal("neuere_version", Art(mensch));
        var e = Ok(mensch.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId }));
        Assert.Contains(e.Hinweise, h => h.Contains("schon im Projekt"));
        Assert.Equal("aktuell", Art(mensch));
        Assert.Single(mensch.Modell.DerKlasse("Familie"));
    }

    [Fact]
    public void Akteur_ki_im_Kern_gleich_ob_Projekt_Mensch_ist()
    {
        // Die Prüfung hängt am Akteur des Befehls (Herkunft.Art), nicht am Projekt: dasselbe Projekt, Befehl als KI bzw. als Mensch
        BibliothekFuellen();
        var p = NeuesProjekt("Akteur wechselt");
        Ok(p.Ausfuehren("familie_laden", new JsonObject { ["id"] = HkId, ["version"] = 1 }));
        var par = new JsonObject { ["id"] = HkId };
        var abgelehnt = Assert.Throws<HlksFehler>(() => new FamilieLaden().Ausfuehren(FamiliePruefungHilfe.Als(p, "ki"), par));
        Assert.Contains("nur ein Mensch", abgelehnt.Message);
        new FamilieLaden().Ausfuehren(FamiliePruefungHilfe.Als(p, "mensch"), par);   // wirft nicht
    }
}
