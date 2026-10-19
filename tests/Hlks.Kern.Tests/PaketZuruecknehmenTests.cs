using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3c, Teil K: ein bestimmtes Paket zurücknehmen (Projekt.PaketZuruecknehmen, Tür «paket_zuruecknehmen») – die Grundlage von
/// «Zurücknehmen» in der KI-Palette. Zwei Projekte auf derselben Ablage wie im Betrieb: die KI (MCP-Server, Akteur KI, Art ki) und der
/// Mensch (Arbeitsplatz-Server). Regel: Rückgängig überschreibt nie eine spätere Änderung.</summary>
public sealed class PaketZuruecknehmenTests : IDisposable
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_zur_" + Guid.NewGuid().ToString("N")[..10]);
    string Ordner => Path.Combine(wurzel, "e_KICAD");

    public PaketZuruecknehmenTests()
    {
        var basis = Projekt.Anlegen(Kat, new OrdnerSpeicher(Ordner), "Zurücknehmen", "Anlage", "pc");
        Ok(basis.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-1" }));
        Ok(basis.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB-1", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 } }));
        Ok(basis.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "WC", ["kennzeichen"] = "WC-1", ["name"] = "Original 1" }));
        Ok(basis.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "WC", ["kennzeichen"] = "WC-2", ["name"] = "Original 2" }));
        basis.Freigeben();
    }

    public void Dispose()
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }
        }
    }

    Projekt Ki() => Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), "KI", "pc", "ki");
    Projekt Mensch(string akteur = "Christian") => Projekt.Oeffnen(Kat, new OrdnerSpeicher(Ordner), akteur, "pc", "mensch");

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} {e.Vorschlag}");
        return e;
    }

    static string Umbenennen(Projekt p, string kz, string name)
    {
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["name"] = name }));
        p.Freigeben();   // wie der MCP-Server beim Ende bzw. der Arbeitsplatz-Server nach einem KI-Lauf
        return e.PaketId!;
    }

    static string? Name(Projekt p, string kz) => p.Modell.Finde(kz)?.Name;
    int Pakete() => new OrdnerSpeicher(Ordner).PaketListe().Count;

    [Fact]
    public void KiAenderung_WirdAlsNeuesPaketZurueckgenommen()
    {
        var ki = Ki();
        var p1 = Umbenennen(ki, "WC-1", "Von der KI");
        var mensch = Mensch();
        Assert.Equal("Von der KI", Name(mensch, "WC-1"));
        var vorher = Pakete();

        var e = Ok(mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki"));

        Assert.Equal("Original 1", Name(mensch, "WC-1"));
        Assert.NotNull(e.PaketId);
        Assert.Equal(vorher + 1, Pakete());                       // ein neues Paket, nichts gelöscht
        var paket = new OrdnerSpeicher(Ordner).PaketLesen(e.PaketId!);
        Assert.Equal("rueckgaengig", paket.Befehl);
        Assert.Equal(p1, paket.RueckgaengigVon);
        Assert.Equal("Christian", paket.Akteur);
        Assert.Equal("mensch", paket.AkteurArt);
        Assert.Equal(p1, paket.Parameter?["paket"]?.GetValue<string>());
        Assert.True(paket.Parameter?["gezielt"]?.GetValue<bool>());
        Assert.Null(e.Ablehnung);
        // die KI sieht es nach dem Abgleich ebenso
        ki.Synchronisieren();
        Assert.Equal("Original 1", Name(ki, "WC-1"));
    }

    [Fact]
    public void SpaeteresPaketAmSelbenElement_Abgelehnt_NichtsGeaendert()
    {
        var ki = Ki();
        var p1 = Umbenennen(ki, "WC-1", "Von der KI");
        var mensch = Mensch();
        var p2 = Umbenennen(mensch, "WC-1", "Vom Menschen");
        var vorher = Pakete();

        var e = mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki");

        Assert.False(e.Angenommen);
        Assert.Equal("spaeter_geaendert", e.Ablehnung);
        Assert.StartsWith("Abgelehnt", e.Meldung);
        Assert.Contains("Christian", e.Fehler);
        Assert.Contains("WC-1", e.Fehler);
        var s = Assert.Single(e.SpaeterePakete!);
        Assert.Equal(p2, s.Paket);
        Assert.Equal("element_aendern", s.Befehl);
        Assert.Equal(["WC-1"], s.Elemente);
        Assert.Equal("Vom Menschen", Name(mensch, "WC-1"));    // die spätere Änderung bleibt
        Assert.Equal(vorher, Pakete());                         // nichts geschrieben
        Assert.Null(e.PaketId);
    }

    [Fact]
    public void SpaeteresPaketEinesAnderenBearbeiters_AusDerAblage_WirdVorherAbgeglichen()
    {
        var ki = Ki();
        var p1 = Umbenennen(ki, "WC-1", "Von der KI");
        var mensch = Mensch();                                   // kennt p1
        var anna = Mensch("Anna");
        Umbenennen(anna, "WC-1", "Von Anna");                     // mensch hat noch nicht abgeglichen
        Assert.Equal("Von der KI", Name(mensch, "WC-1"));

        var e = mensch.PaketZuruecknehmen(p1);

        Assert.False(e.Angenommen);
        Assert.Equal("spaeter_geaendert", e.Ablehnung);
        Assert.Equal("Anna", Assert.Single(e.SpaeterePakete!).Akteur);
        Assert.Equal("Von Anna", Name(mensch, "WC-1"));          // abgeglichen, nicht überschrieben
    }

    [Fact]
    public void SpaeteresPaketAnEinemAnderenElement_HindertNicht()
    {
        var ki = Ki();
        var p1 = Umbenennen(ki, "WC-1", "Von der KI");
        var mensch = Mensch();
        Umbenennen(mensch, "WC-2", "Vom Menschen");

        Ok(mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki"));

        Assert.Equal("Original 1", Name(mensch, "WC-1"));
        Assert.Equal("Vom Menschen", Name(mensch, "WC-2"));
    }

    [Fact]
    public void ZweiKiPakete_VonHintenNachVornZuruecknehmen()
    {
        var ki = Ki();
        var p1 = Umbenennen(ki, "WC-1", "KI eins");
        var p2 = Umbenennen(ki, "WC-1", "KI zwei");
        var mensch = Mensch();

        var zuerst = mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki");
        Assert.False(zuerst.Angenommen);
        Assert.Equal(p2, Assert.Single(zuerst.SpaeterePakete!).Paket);

        Ok(mensch.PaketZuruecknehmen(p2, nurAkteurArt: "ki"));
        Assert.Equal("KI eins", Name(mensch, "WC-1"));
        // p2 und sein Zurücknehmen heben sich auf: jetzt geht p1
        Ok(mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki"));
        Assert.Equal("Original 1", Name(mensch, "WC-1"));
    }

    [Fact]
    public void SchonZurueckgenommen_Abgelehnt()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var mensch = Mensch();
        var z = Ok(mensch.PaketZuruecknehmen(p1));

        var e = mensch.PaketZuruecknehmen(p1);

        Assert.False(e.Angenommen);
        Assert.Equal("schon_zurueckgenommen", e.Ablehnung);
        Assert.Contains(z.PaketId!, e.Fehler);
    }

    [Fact]
    public void ZuruecknehmenDesZuruecknehmens_GiltWiederAlsWirksam()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var mensch = Mensch();
        var z = Ok(mensch.PaketZuruecknehmen(p1));
        mensch.Freigeben();
        Ok(mensch.PaketZuruecknehmen(z.PaketId!));               // das Zurücknehmen selbst zurücknehmen
        Assert.Equal("Von der KI", Name(mensch, "WC-1"));
        mensch.Freigeben();

        Ok(mensch.PaketZuruecknehmen(p1));                        // p1 ist wieder wirksam: geht erneut
        Assert.Equal("Original 1", Name(mensch, "WC-1"));
    }

    [Fact]
    public void NurAkteurArtKi_PaketEinesMenschen_Abgelehnt()
    {
        var mensch = Mensch();
        var p = Umbenennen(mensch, "WC-1", "Vom Menschen");

        var e = mensch.PaketZuruecknehmen(p, nurAkteurArt: "ki");

        Assert.False(e.Angenommen);
        Assert.Equal("fremd", e.Ablehnung);
        Assert.Equal("Vom Menschen", Name(mensch, "WC-1"));
    }

    [Fact]
    public void UnbekanntesPaket_Abgelehnt()
    {
        var e = Mensch().PaketZuruecknehmen("20260101T000000000Z_x_y_000001");
        Assert.False(e.Angenommen);
        Assert.Equal("unbekannt", e.Ablehnung);
        Assert.Contains("aenderungen", e.Vorschlag);
    }

    [Fact]
    public void ReservierungDerKi_Abgelehnt_AlsGesperrt()
    {
        var ki = Ki();
        var p1 = Ok(ki.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["name"] = "Von der KI" })).PaketId!;   // ohne Freigeben
        var mensch = Mensch();

        var e = mensch.PaketZuruecknehmen(p1);

        Assert.False(e.Angenommen);
        Assert.Equal("gesperrt", e.Ablehnung);
        Assert.Contains(e.Gesperrt, g => g.Akteur == "KI");
        ki.Freigeben();
        Ok(mensch.PaketZuruecknehmen(p1));
    }

    [Fact]
    public void AngelegtesElement_InzwischenVerwendet_Abgelehnt()
    {
        var ki = Ki();
        var p1 = Ok(ki.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "KW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser kalt" } })).PaketId!;
        ki.Freigeben();
        var mensch = Mensch();
        Ok(mensch.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-2", ["system"] = "KW" }));   // berührt nur WC-2, verweist aber auf KW
        mensch.Freigeben();

        var e = mensch.PaketZuruecknehmen(p1);

        Assert.False(e.Angenommen);
        Assert.Equal("verwendet", e.Ablehnung);
        Assert.Contains("WC-2", e.Fehler);
        Assert.NotNull(mensch.Modell.Finde("KW"));
    }

    [Fact]
    public void AngelegtesElement_UnbenutztZurueckgenommen_IstWeg()
    {
        var ki = Ki();
        var p1 = Ok(ki.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "WC", ["kennzeichen"] = "WC-KI" })).PaketId!;
        ki.Freigeben();
        var mensch = Mensch();
        Assert.NotNull(mensch.Modell.Finde("WC-KI"));

        Ok(mensch.PaketZuruecknehmen(p1));

        Assert.Null(mensch.Modell.Finde("WC-KI"));
    }

    [Fact]
    public void Trockenlauf_SpeichertNichts()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var mensch = Mensch();
        var vorher = Pakete();

        var e = Ok(mensch.PaketZuruecknehmen(p1, trockenlauf: true));

        Assert.True(e.Trockenlauf);
        Assert.Null(e.PaketId);
        Assert.Equal(vorher, Pakete());
        Assert.Equal("Von der KI", Name(mensch, "WC-1"));
    }

    [Fact]
    public void PaketImStandabbild_WirdAusDerAblageGelesen()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var erst = Mensch();
        erst.StandSichern();                                     // p1 steckt jetzt im Standabbild
        var mensch = Mensch();
        Assert.DoesNotContain(mensch.Verlauf, x => x.Id == p1);
        Assert.Contains(p1, mensch.BekanntePakete);

        Ok(mensch.PaketZuruecknehmen(p1, nurAkteurArt: "ki"));

        Assert.Equal("Original 1", Name(mensch, "WC-1"));
    }

    [Fact]
    public void PaketImStandabbild_SpaeteresPaketAusDemStand_Abgelehnt()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var erst = Mensch();
        Umbenennen(erst, "WC-1", "Vom Menschen");
        erst.StandSichern();
        var mensch = Mensch();

        var e = mensch.PaketZuruecknehmen(p1);

        Assert.False(e.Angenommen);
        Assert.Equal("spaeter_geaendert", e.Ablehnung);
        Assert.Equal("Vom Menschen", Name(mensch, "WC-1"));
    }

    [Fact]
    public void UeberDieTuer_paket_zuruecknehmen()
    {
        var p1 = Umbenennen(Ki(), "WC-1", "Von der KI");
        var mensch = Mensch();

        var e = (BefehlsErgebnis)Tuer.Befehl(mensch, "paket_zuruecknehmen", new JsonObject { ["paket"] = p1, ["akteurArt"] = "ki" });

        Assert.True(e.Angenommen, e.Fehler);
        Assert.Equal("Original 1", Name(mensch, "WC-1"));
        Assert.Throws<HlksFehler>(() => Tuer.Befehl(mensch, "paket_zuruecknehmen", new JsonObject()));
        // Ausgabe: Ablehnung und spätere Pakete erscheinen nur, wenn gesetzt
        var json = Json.Text(e);
        Assert.DoesNotContain("ablehnung", json);
        Assert.DoesNotContain("spaeterePakete", json);
    }
}
