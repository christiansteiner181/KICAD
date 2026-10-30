using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Befehle des Kerns: element_anlegen, element_aendern, element_loeschen, verbinden, trennen, leitung_ziehen.
/// Alle Tests arbeiten ohne Speicher auf einem frischen Projekt bzw. dem Beispielprojekt.</summary>
public class BefehleTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    // ---------------------------------------------------------------- Hilfen

    static Projekt Neu() => Projekt.Anlegen(Kat, null, "Test", "T", "pc");

    static JsonObject Rechteck(double x0, double y0, double x1, double y1) => new()
    {
        ["art"] = "polygon",
        ["punkte"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)),
    };

    static JsonObject Punkt(double x, double y, double z) => new()
    {
        ["art"] = "punkt",
        ["punkte"] = new JsonArray(new JsonArray(x, y, z)),
    };

    static BefehlsErgebnis Anlegen(Projekt p, string klasse, string? kz = null, JsonObject? eig = null, JsonObject? geo = null,
        string? teilVon = null, string? geschoss = null, string? raum = null, string? system = null)
    {
        var o = new JsonObject { ["klasse"] = klasse };
        if (kz is not null) o["kennzeichen"] = kz;
        if (eig is not null) o["eigenschaften"] = eig;
        if (geo is not null) o["geometrie"] = geo;
        if (teilVon is not null) o["teilVon"] = teilVon;
        if (geschoss is not null) o["geschoss"] = geschoss;
        if (raum is not null) o["raum"] = raum;
        if (system is not null) o["system"] = system;
        return p.Ausfuehren("element_anlegen", o, trockenlauf: false);
    }

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} {e.Vorschlag}");
        return e;
    }

    static BefehlsErgebnis Abgelehnt(BefehlsErgebnis e)
    {
        Assert.False(e.Angenommen, $"{e.Befehl} wurde angenommen, sollte aber abgelehnt werden.");
        Assert.False(string.IsNullOrWhiteSpace(e.Fehler));
        Assert.StartsWith("Abgelehnt", e.Meldung);
        Assert.Null(e.PaketId);
        return e;
    }

    /// <summary>Gebäude GEB-1 mit Geschossen EG (0…2900) und 1.OG (2900…5800), Räume R.1 und R.2 im EG.</summary>
    static Projekt MitGeschossenUndRaeumen()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        Ok(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 }, teilVon: "GEB-1"));
        Ok(Anlegen(p, "Geschoss", "1.OG", new JsonObject { ["kote"] = 2900, ["hoehe"] = 2900 }, teilVon: "GEB-1"));
        Ok(Anlegen(p, "Raum", "R.1", geo: Rechteck(0, 0, 5000, 5000), geschoss: "EG"));
        Ok(Anlegen(p, "Raum", "R.2", geo: Rechteck(5000, 0, 10000, 5000), geschoss: "EG"));
        return p;
    }

    /// <summary>Projekt mit Systemen KW (Trinkwasser kalt) und SW (Schmutzwasser) und einem WC ohne Lage.</summary>
    static Projekt MitSystemen()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        Ok(Anlegen(p, "System", "SW", new JsonObject { ["systemtyp"] = "Schmutzwasser" }));
        Ok(Anlegen(p, "WC", "WC-1"));
        return p;
    }

    static BefehlsErgebnis Rohr(Projekt p, string system, string kz, double laenge = 1000, JsonObject? extra = null)
    {
        var o = new JsonObject
        {
            ["system"] = system, ["kennzeichen"] = kz,
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(laenge, 0, 0)),
        };
        if (extra is not null) foreach (var (k, v) in extra) o[k] = v?.DeepClone();
        return p.Ausfuehren("leitung_ziehen", o, trockenlauf: false);
    }

    static Element Hole(Projekt p, string kz) => p.Modell.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt im Modell");

    // ---------------------------------------------------------------- element_anlegen

    [Fact]
    public void Anlegen_UnbekannteKlasse_AbgelehntMitKlassenliste()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Fantasiegeraet"));
        Assert.Contains("Fantasiegeraet", e.Fehler);
        Assert.NotNull(e.Vorschlag);
        Assert.Contains("WC", e.Vorschlag);
        Assert.Contains("Rohr", e.Vorschlag);
        Assert.Contains("Datenpunkt", e.Vorschlag);
        Assert.DoesNotContain("Apparat,", e.Vorschlag); // abstrakte Basisklassen sind keine Auswahl
        Assert.Empty(p.Modell.Elemente);
        Assert.Empty(p.Verlauf);
    }

    [Fact]
    public void Anlegen_AbstrakteKlasse_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Apparat"));
        Assert.Contains("Basis", e.Fehler);
        Assert.Contains("WC", e.Vorschlag);
    }

    [Fact]
    public void Anlegen_UnbekannteEigenschaft_AbgelehntMitErlaubten()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Gebaeude", "GEB-1", new JsonObject { ["farbe"] = "blau" }));
        Assert.Contains("farbe", e.Fehler);
        Assert.NotNull(e.Vorschlag);
        Assert.Contains("adresse", e.Vorschlag);
        Assert.Contains("egid", e.Vorschlag);
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void Anlegen_UngueltigeAuswahl_AbgelehntMitErlaubten()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Ansicht", "AN-1", new JsonObject { ["art"] = "hologramm" }));
        Assert.Contains("hologramm", e.Fehler);
        Assert.Contains("grundriss", e.Vorschlag);
        Assert.Contains("schema", e.Vorschlag);
    }

    [Fact]
    public void Anlegen_AuswahlOhneBeachtungVonGrossKlein_WirdNormalisiert()
    {
        var p = Neu();
        Ok(Anlegen(p, "Ansicht", "AN-1", new JsonObject { ["art"] = "Grundriss" }));
        Assert.Equal("grundriss", Hole(p, "AN-1").Text("art"));
    }

    [Fact]
    public void Anlegen_ZahlAlsText_WirdAngenommen()
    {
        var p = Neu();
        Ok(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = "1,5", ["hoehe"] = "2900" }));
        var g = Hole(p, "EG");
        Assert.Equal(1.5, g.Zahl("kote"));
        Assert.Equal(2900, g.Zahl("hoehe"));
        Assert.Equal("mm", g.Eigenschaft("kote")!.Einheit);
    }

    [Fact]
    public void Anlegen_KeineZahl_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = "hoch", ["hoehe"] = 2900 }));
        Assert.Contains("kote", e.Fehler);
        Assert.Contains("Zahl", e.Fehler);
    }

    [Fact]
    public void Anlegen_FalscheEinheit_Abgelehnt()
    {
        var p = Neu();
        var kote = new JsonObject { ["wert"] = 1, ["einheit"] = "m" };
        var e = Abgelehnt(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = kote, ["hoehe"] = 2900 }));
        Assert.Contains("mm", e.Fehler);
    }

    [Fact]
    public void Anlegen_PflichteigenschaftFehlt_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "System", "SW"));
        Assert.Contains("systemtyp", e.Fehler);
        var g = Abgelehnt(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = 0 }));
        Assert.Contains("hoehe", g.Fehler);
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void Anlegen_DoppeltesKennzeichen_Abgelehnt()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        var e = Abgelehnt(Anlegen(p, "Gebaeude", "GEB-1"));
        Assert.Contains("GEB-1", e.Fehler);
        Assert.Contains("GEB-2", e.Vorschlag);
        // Gross-/Kleinschreibung zählt nicht
        Abgelehnt(Anlegen(p, "Gebaeude", "geb-1"));
        Assert.Single(p.Modell.Elemente);
    }

    [Fact]
    public void Anlegen_KennzeichenAutomatischAusKuerzel()
    {
        var p = Neu();
        var a = Ok(Anlegen(p, "WC"));
        Ok(Anlegen(p, "WC"));
        Ok(Anlegen(p, "Waschtisch"));
        Assert.Equal("WC-1", Assert.Single(a.Aenderungen).Kennzeichen);
        Assert.NotNull(p.Modell.Finde("WC-1"));
        Assert.NotNull(p.Modell.Finde("WC-2"));
        Assert.NotNull(p.Modell.Finde("WT-1"));
        Assert.Equal("WC", Hole(p, "WC-1").Klasse);
    }

    [Fact]
    public void Anlegen_GeschossAusHoeheUndRaumAusLage()
    {
        var p = MitGeschossenUndRaeumen();
        var e = Ok(Anlegen(p, "Waschtisch", "WT-1", geo: Punkt(1000, 1000, 500)));
        var wt = Hole(p, "WT-1");
        Assert.Equal(Hole(p, "EG").Id, wt.Geschoss);
        Assert.Equal(Hole(p, "R.1").Id, wt.Raum);
        Assert.Contains(e.Hinweise, h => h.Contains("Geschoss EG"));
        Assert.Contains(e.Hinweise, h => h.Contains("Raum R.1"));

        // anderes Geschoss und anderer Raum
        Ok(Anlegen(p, "Raum", "R.3", geo: Rechteck(0, 0, 5000, 5000), geschoss: "1.OG"));
        Ok(Anlegen(p, "WC", "WC-1", geo: Punkt(7000, 1000, 3000)));
        var wc = Hole(p, "WC-1");
        Assert.Equal(Hole(p, "1.OG").Id, wc.Geschoss);
        Assert.Null(wc.Raum); // R.3 liegt bei x 0…5000, die Lage bei x 7000
    }

    [Fact]
    public void Anlegen_LageAusserhalbAllerRaeume_BleibtOhneRaumMitBefund()
    {
        var p = MitGeschossenUndRaeumen();
        var e = Ok(Anlegen(p, "WC", "WC-1", geo: Punkt(50000, 50000, 0)));
        Assert.Null(Hole(p, "WC-1").Raum);
        Assert.Contains(e.NeueBefunde, b => b.Code == "P04" && b.Element == "WC-1");
    }

    [Fact]
    public void Anlegen_AusdruecklichGesetzterRaumGewinntVorLage()
    {
        var p = MitGeschossenUndRaeumen();
        Ok(Anlegen(p, "WC", "WC-1", geo: Punkt(1000, 1000, 0), raum: "R.2"));
        Assert.Equal(Hole(p, "R.2").Id, Hole(p, "WC-1").Raum);
    }

    [Fact]
    public void Anlegen_SystemBekommtMediumAusSystemtypMitHerkunftKatalog()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "SW", new JsonObject { ["systemtyp"] = "Schmutzwasser" }));
        var medium = Hole(p, "SW").Eigenschaft("medium");
        Assert.NotNull(medium);
        Assert.Equal("SW", medium!.AlsText());
        Assert.Equal("katalog", medium.Herkunft.Art);
        Assert.Contains("Schmutzwasser", medium.Herkunft.Quelle);
        // die ausdrücklich gesetzte Eigenschaft hat die Herkunft des Akteurs
        Assert.Equal("mensch", Hole(p, "SW").Eigenschaft("systemtyp")!.Herkunft.Art);
        Assert.Equal("T", Hole(p, "SW").Eigenschaft("systemtyp")!.Herkunft.Akteur);
    }

    [Fact]
    public void Anlegen_AnschluesseKommenAusDemKatalog()
    {
        var p = MitSystemen();
        var wc = Hole(p, "WC-1");
        Assert.Contains(wc.Anschluesse, a => a.Name == "sw" && a.Medium == "SW" && a.Fluss == "raus");
        Assert.Contains(wc.Anschluesse, a => a.Name == "kw" && a.Medium == "KW" && a.Fluss == "rein");
    }

    [Fact]
    public void Anlegen_LeitungsbauteilUebernimmtSystemVomStrang()
    {
        var p = MitSystemen();
        Ok(Anlegen(p, "Strang", "KW-S1", new JsonObject { ["art"] = "steigstrang" }, teilVon: "KW"));
        Assert.Equal(Hole(p, "KW").Id, Hole(p, "KW-S1").System);
        Ok(Anlegen(p, "TStueck", "TS-1", new JsonObject { ["dn"] = 20 }, geo: Punkt(0, 0, 0), teilVon: "KW-S1"));
        var ts = Hole(p, "TS-1");
        Assert.Equal(Hole(p, "KW").Id, ts.System);
        Assert.All(ts.Anschluesse, a => Assert.Equal("KW", a.Medium)); // «*» wird durch das Medium des Systems ersetzt
        Assert.All(ts.Anschluesse, a => Assert.Equal(20, a.Dn));
    }

    [Fact]
    public void Anlegen_LeitungsbauteilOhneSystem_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Bogen", "BO-1", geo: Punkt(0, 0, 0)));
        Assert.Contains("System", e.Fehler);
        Assert.Contains("teilVon", e.Vorschlag);
    }

    [Fact]
    public void Anlegen_FalscheUebergeordneteKlasse_Abgelehnt()
    {
        var p = MitGeschossenUndRaeumen();
        var e = Abgelehnt(Anlegen(p, "Strang", "ST-1", new JsonObject { ["art"] = "fallstrang" }, teilVon: "GEB-1"));
        Assert.Contains("System", e.Fehler);
    }

    [Fact]
    public void Anlegen_GeometrieAlsFalscheArt_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(Anlegen(p, "Raum", "R.1", geo: Punkt(0, 0, 0)));
        Assert.Contains("polygon", e.Fehler);
    }

    [Fact]
    public void Anlegen_PaketUndHerkunft()
    {
        var p = Neu();
        var e = Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        Assert.NotNull(e.PaketId);
        var paket = Assert.Single(p.Verlauf);
        Assert.Equal(e.PaketId, paket.Id);
        Assert.Equal("element_anlegen", paket.Befehl);
        var op = Assert.Single(paket.Operationen);
        Assert.Equal("element.setzen", op.Art);
        Assert.Equal(0, op.Basis);
        Assert.Null(op.ElementVorher);
        var g = Hole(p, "GEB-1");
        Assert.Equal(1, g.Version);
        Assert.Equal("mensch", g.Angelegt!.Art);
        Assert.Equal("T", g.Angelegt.Akteur);
        Assert.Equal("element_anlegen", g.Angelegt.Befehl);
    }

    [Fact]
    public void Ausfuehren_UnbekannterBefehl_AbgelehntMitBefehlsliste()
    {
        var p = Neu();
        var e = Abgelehnt(p.Ausfuehren("element_verschieben", new JsonObject()));
        Assert.Contains("element_anlegen", e.Vorschlag);
        Assert.Contains("verbinden", e.Vorschlag);
    }

    [Fact]
    public void Ausfuehren_FehlenderPflichtparameter_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(p.Ausfuehren("element_anlegen", new JsonObject { ["kennzeichen"] = "X-1" }));
        Assert.Contains("klasse", e.Fehler);
    }

    // ---------------------------------------------------------------- element_aendern

    [Fact]
    public void Aendern_EigenschaftMitNullEntfernen()
    {
        var p = Neu();
        Ok(Anlegen(p, "Waschtisch", "WT-1", new JsonObject { ["hersteller"] = "Muster AG", ["typ"] = "W100" }));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "WT-1",
            ["eigenschaften"] = new JsonObject { ["hersteller"] = null },
        }));
        var wt = Hole(p, "WT-1");
        Assert.Null(wt.Eigenschaft("hersteller"));
        Assert.Equal("W100", wt.Text("typ"));
        Assert.Equal(2, wt.Version);
        var info = Assert.Single(e.Aenderungen);
        Assert.Equal("geaendert", info.Art);
        Assert.Contains("hersteller", info.Detail);
    }

    [Fact]
    public void Aendern_PflichteigenschaftEntfernen_Abgelehnt()
    {
        var p = Neu();
        Ok(Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 }));
        var e = Abgelehnt(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "EG", ["eigenschaften"] = new JsonObject { ["kote"] = null },
        }));
        Assert.Contains("kote", e.Fehler);
        Assert.NotNull(Hole(p, "EG").Eigenschaft("kote"));
    }

    [Fact]
    public void Aendern_EigenschaftSetzen_BekommtNeueHerkunft()
    {
        var p = Neu();
        Ok(Anlegen(p, "Waschtisch", "WT-1", new JsonObject { ["typ"] = "W100" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "WT-1",
            ["eigenschaften"] = new JsonObject { ["typ"] = new JsonObject { ["wert"] = "W200", ["quelle"] = "Datenblatt Muster AG" } },
        }));
        var typ = Hole(p, "WT-1").Eigenschaft("typ")!;
        Assert.Equal("W200", typ.AlsText());
        Assert.Equal("Datenblatt Muster AG", typ.Herkunft.Quelle);
        Assert.Equal("element_aendern", typ.Herkunft.Befehl);
    }

    [Fact]
    public void Aendern_KennzeichenUmbenennen()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        var id = Hole(p, "GEB-1").Id;
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["kennzeichen"] = "HAUS-A" }));
        Assert.Null(p.Modell.Finde("GEB-1"));
        Assert.Equal(id, Hole(p, "HAUS-A").Id);
        Assert.Equal(id, p.Modell.Finde(id)!.Id);
    }

    [Fact]
    public void Aendern_KennzeichenSchonVergeben_Abgelehnt()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        Ok(Anlegen(p, "Gebaeude", "GEB-2"));
        var e = Abgelehnt(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-2", ["kennzeichen"] = "GEB-1" }));
        Assert.Contains("GEB-1", e.Fehler);
        Assert.NotNull(p.Modell.Finde("GEB-2"));
    }

    [Fact]
    public void Aendern_NeueLage_OrdnetRaumNeuZu()
    {
        var p = MitGeschossenUndRaeumen();
        Ok(Anlegen(p, "WC", "WC-1", geo: Punkt(1000, 1000, 0)));
        Assert.Equal(Hole(p, "R.1").Id, Hole(p, "WC-1").Raum);

        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1", ["geometrie"] = Punkt(7000, 1000, 0) }));
        var wc = Hole(p, "WC-1");
        Assert.Equal(Hole(p, "R.2").Id, wc.Raum);
        Assert.Equal(Hole(p, "EG").Id, wc.Geschoss);
        Assert.Equal(7000, wc.Geometrie!.Punkte[0][0]);
        Assert.Contains(e.Hinweise, h => h.Contains("Raum R.2"));
        Assert.Contains("Raum", Assert.Single(e.Aenderungen).Detail);
    }

    [Fact]
    public void Aendern_OhneAenderung_KeinPaket()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1", new JsonObject { ["adresse"] = "Musterweg 1" }));
        int pakete = p.Verlauf.Count;
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB-1", ["name"] = null }));
        Assert.Null(e.PaketId);
        Assert.Empty(e.Aenderungen);
        Assert.Equal(pakete, p.Verlauf.Count);
        Assert.Equal(1, Hole(p, "GEB-1").Version);
    }

    [Fact]
    public void Aendern_UnbekanntesElement_AbgelehntMitAehnlichen()
    {
        var p = Neu();
        Ok(Anlegen(p, "Gebaeude", "GEB-1"));
        var e = Abgelehnt(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "GEB", ["name"] = "x" }));
        Assert.Contains("GEB-1", e.Vorschlag);
    }

    // ---------------------------------------------------------------- element_loeschen

    [Fact]
    public void Loeschen_Referenziert_Abgelehnt()
    {
        var p = MitGeschossenUndRaeumen();
        var e = Abgelehnt(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "GEB-1" }));
        Assert.Contains("EG", e.Fehler);
        Assert.Contains("1.OG", e.Fehler);
        Assert.NotNull(e.Vorschlag);
        Assert.NotNull(p.Modell.Finde("GEB-1"));

        // ein Geschoss mit Räumen ebenfalls
        var g = Abgelehnt(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "EG" }));
        Assert.Contains("R.1", g.Fehler);
    }

    [Fact]
    public void Loeschen_VonBezugAbgelehnt_ErstSpaeterErlaubt()
    {
        var p = Neu();
        Ok(Anlegen(p, "WC", "WC-1"));
        Ok(Anlegen(p, "Datenpunkt", "DP-1", new JsonObject { ["signal"] = "DI", ["art"] = "meldung" }));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "DP-1", ["bezuege"] = new JsonObject { ["an"] = "WC-1" } }));
        var e = Abgelehnt(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WC-1" }));
        Assert.Contains("DP-1", e.Fehler);
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "DP-1" }));
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WC-1" }));
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void Loeschen_NichtReferenziert_SamtVerbindungen()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        Assert.Single(p.Modell.Verbindungen);

        var e = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WC-1" }));
        Assert.Null(p.Modell.Finde("WC-1"));
        Assert.Empty(p.Modell.Verbindungen);
        Assert.Contains(e.Aenderungen, a => a.Art == "getrennt");
        Assert.Contains(e.Aenderungen, a => a.Art == "geloescht" && a.Kennzeichen == "WC-1");
        Assert.NotNull(p.Modell.Finde("RO-KW"));
    }

    [Fact]
    public void Loeschen_Beispielprojekt_EntferntVerbindungenDesApparats()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        int elemente = p.Modell.Elemente.Count, verbindungen = p.Modell.Verbindungen.Count;
        var wt = Hole(p, "WT-1.02");
        Assert.Equal(2, p.Modell.VerbindungenVon(wt.Id).Count()); // sw und kw (ww bleibt offen)

        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "WT-1.02" }));
        Assert.Equal(elemente - 1, p.Modell.Elemente.Count);
        Assert.Equal(verbindungen - 2, p.Modell.Verbindungen.Count);
        Assert.Null(p.Modell.Finde("WT-1.02"));
        Assert.DoesNotContain(p.Modell.Verbindungen, v => v.Von.Element == wt.Id || v.Nach.Element == wt.Id);
    }

    // ---------------------------------------------------------------- verbinden

    [Fact]
    public void Verbinden_WaehltAnschlussAutomatisch()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        var e = Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        var rohr = Hole(p, "RO-KW"); var wc = Hole(p, "WC-1");
        var v = Assert.Single(p.Modell.Verbindungen);
        Assert.NotNull(p.Modell.VerbindungAn(rohr.Id, "ende"));
        Assert.NotNull(p.Modell.VerbindungAn(wc.Id, "kw"));
        Assert.Null(p.Modell.VerbindungAn(rohr.Id, "anfang"));
        Assert.Null(p.Modell.VerbindungAn(wc.Id, "sw"));
        Assert.Equal(new Anschlusspunkt(wc.Id, "kw"), v.Gegenueber(rohr.Id, "ende"));
        Assert.Equal("verbunden", Assert.Single(e.Aenderungen).Art);
        Assert.Equal("RO-KW.ende – WC-1.kw", e.Aenderungen[0].Kennzeichen);
    }

    [Fact]
    public void Verbinden_WaehltAuchSchmutzwasserAnschlussRichtig()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "SW", "RO-SW"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "WC-1", ["nach"] = "RO-SW" }));
        var rohr = Hole(p, "RO-SW"); var wc = Hole(p, "WC-1");
        // WC.sw «raus» passt nur zu Rohr.anfang «rein»
        Assert.Equal(new Anschlusspunkt(rohr.Id, "anfang"), p.Modell.VerbindungAn(wc.Id, "sw")!.Gegenueber(wc.Id, "sw"));
    }

    [Fact]
    public void Verbinden_MedienPassenNicht_Abgelehnt()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "SW", "RO-SW"));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-SW.ende", ["nach"] = "WC-1.kw" }));
        Assert.Contains("Medien passen nicht", e.Fehler);
        Assert.Contains("SW", e.Fehler);
        Assert.Contains("KW", e.Fehler);
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Verbinden_KeinPassendesPaar_AbgelehntMitAnschlussliste()
    {
        var p = MitSystemen();
        Ok(Anlegen(p, "System", "HZ", new JsonObject { ["systemtyp"] = "Heizung Vorlauf" }));
        Ok(Rohr(p, "HZ", "RO-HZ"));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-HZ", ["nach"] = "WC-1" }));
        Assert.Contains("Kein freies, passendes Anschlusspaar", e.Fehler);
        Assert.Contains("kw", e.Vorschlag);
        Assert.Contains("anfang", e.Vorschlag);
    }

    [Fact]
    public void Verbinden_RausMitRaus_Abgelehnt()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "SW", "RO-SW"));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-SW.ende", ["nach"] = "WC-1.sw" }));
        Assert.Contains("Fliessrichtung", e.Fehler);
        Assert.Contains("raus", e.Fehler);
        Assert.NotNull(e.Vorschlag);
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Verbinden_ReinMitRein_Abgelehnt()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW.anfang", ["nach"] = "WC-1.kw" }));
        Assert.Contains("Fliessrichtung", e.Fehler);
        Assert.Contains("rein", e.Fehler);
    }

    [Fact]
    public void Verbinden_BelegterAnschluss_Abgelehnt()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-1"));
        Ok(Rohr(p, "KW", "RO-2"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "WC-1.kw" }));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-2.ende", ["nach"] = "WC-1.kw" }));
        Assert.Contains("schon verbunden", e.Fehler);
        Assert.Contains("trennen", e.Vorschlag, StringComparison.OrdinalIgnoreCase);
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void Verbinden_UnbekannterAnschluss_AbgelehntMitAnschlussliste()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW.mitte", ["nach"] = "WC-1.kw" }));
        Assert.Contains("mitte", e.Fehler);
        Assert.Contains("anfang", e.Vorschlag);
        Assert.Contains("ende", e.Vorschlag);
    }

    [Fact]
    public void Verbinden_MitSichSelbst_Abgelehnt()
    {
        var p = MitSystemen();
        var e = Abgelehnt(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "WC-1.sw", ["nach"] = "WC-1.kw" }));
        Assert.Contains("sich selbst", e.Fehler);
    }

    [Fact]
    public void Verbinden_ObjektformFuerVon_NamenMitAnschluss()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        Ok(Rohr(p, "KW", "RO-KW"));
        Ok(Anlegen(p, "Bogen", "BO-1", geo: Punkt(0, 0, 0), system: "KW"));
        var e = Ok(p.Ausfuehren("verbinden", new JsonObject
        {
            ["von"] = new JsonObject { ["element"] = "RO-KW", ["anschluss"] = "ende" },
            ["nach"] = "BO-1.a",
        }));
        Assert.Equal("RO-KW.ende – BO-1.a", Assert.Single(e.Aenderungen).Kennzeichen);
    }

    // ---------------------------------------------------------------- trennen

    [Fact]
    public void Trennen_UeberAnschluss()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        var e = Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1.kw" }));
        Assert.Empty(p.Modell.Verbindungen);
        Assert.Equal("getrennt", Assert.Single(e.Aenderungen).Art);
        Assert.Contains(e.NeueBefunde, b => b.Code == "P01" && b.Element == "RO-KW");
    }

    [Fact]
    public void Trennen_UeberVerbindungsId()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-KW"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-KW", ["nach"] = "WC-1" }));
        var id = p.Modell.Verbindungen.Single().Id;
        Ok(p.Ausfuehren("trennen", new JsonObject { ["verbindung"] = id }));
        Assert.Empty(p.Modell.Verbindungen);
    }

    [Fact]
    public void Trennen_NichtVerbunden_Abgelehnt()
    {
        var p = MitSystemen();
        var e = Abgelehnt(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1.kw" }));
        Assert.Contains("nicht verbunden", e.Fehler);
        Abgelehnt(p.Ausfuehren("trennen", new JsonObject { ["verbindung"] = "gibt-es-nicht" }));
        Abgelehnt(p.Ausfuehren("trennen", new JsonObject()));
        Abgelehnt(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1" }));
    }

    // ---------------------------------------------------------------- leitung_ziehen

    [Fact]
    public void LeitungZiehen_MitVonUndNach_VerbindetBeideEnden()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        Ok(Anlegen(p, "Hausanschluss", "HA-1", geo: Punkt(0, 0, 0), system: "KW"));
        Ok(Anlegen(p, "WC", "WC-1"));

        var e = Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "KW", ["dn"] = 25, ["material"] = "Mehrschichtverbund",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(3000, 0, 0), new JsonArray(3000, 4000, 0)),
            ["von"] = "HA-1.aus",
            ["nach"] = new JsonObject { ["element"] = "WC-1", ["anschluss"] = "kw" },
        }));

        var rohr = Hole(p, "RO-1");
        Assert.Equal("Rohr", rohr.Klasse);
        Assert.Equal(25, rohr.Zahl("dn"));
        Assert.Equal("Mehrschichtverbund", rohr.Text("material"));
        Assert.Equal(7000, rohr.Geometrie!.Laenge(), 6);
        Assert.Equal("polylinie", rohr.Geometrie.Art);
        Assert.Equal(Hole(p, "KW").Id, rohr.System);
        Assert.All(rohr.Anschluesse, a => Assert.Equal(25, a.Dn));
        Assert.Equal(2, p.Modell.Verbindungen.Count);
        Assert.Equal(1, e.Aenderungen.Count(a => a.Art == "angelegt"));
        Assert.Equal(2, e.Aenderungen.Count(a => a.Art == "verbunden"));

        // Fliessrichtung: Hausanschluss → Rohr → WC
        var folgen = Netz.Folgen(p.Modell, Kat, Hole(p, "HA-1"), "stromab", "KW");
        Assert.Equal(["RO-1", "WC-1"], folgen.Select(s => s.Element.Kennzeichen).ToArray());
    }

    [Fact]
    public void LeitungZiehen_ObjektformFuerVon()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        Ok(Anlegen(p, "Hausanschluss", "HA-1", geo: Punkt(0, 0, 0), system: "KW"));
        Ok(Rohr(p, "KW", "RO-KW", 2000, new JsonObject { ["von"] = new JsonObject { ["element"] = "HA-1", ["anschluss"] = "aus" } }));
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void LeitungZiehen_ImStrang_UebernimmtSystemUndNeuesKennzeichen()
    {
        var p = MitSystemen();
        Ok(Anlegen(p, "Strang", "KW-S1", new JsonObject { ["art"] = "steigstrang" }, teilVon: "KW"));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["strang"] = "KW-S1",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(0, 0, 3000)),
        }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["strang"] = "KW-S1",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 3000), new JsonArray(0, 0, 6000)),
        }));
        var r1 = Hole(p, "RO-1"); var r2 = Hole(p, "RO-2");
        Assert.Equal(Hole(p, "KW-S1").Id, r1.TeilVon);
        Assert.Equal(Hole(p, "KW").Id, r1.System);
        Assert.Equal(Hole(p, "KW").Id, r2.System);
        Assert.Equal(3000, r1.Geometrie!.Laenge(), 6);
    }

    [Fact]
    public void LeitungZiehen_Kanal()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "ZUL", new JsonObject { ["systemtyp"] = "Zuluft" }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "ZUL", ["klasse"] = "Kanal",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 3000), new JsonArray(5000, 0, 3000)),
        }));
        var kanal = Hole(p, "KA-1");
        Assert.Equal("Kanal", kanal.Klasse);
        Assert.All(kanal.Anschluesse, a => Assert.Equal("ZUL", a.Medium));
    }

    [Fact]
    public void LeitungZiehen_OhneSystemOderStrang_Abgelehnt()
    {
        var p = Neu();
        var e = Abgelehnt(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(1000, 0, 0)),
        }));
        Assert.Contains("System", e.Fehler);
        Assert.Contains("teilVon", e.Vorschlag);
        Assert.Empty(p.Modell.Elemente);
    }

    [Fact]
    public void LeitungZiehen_ZuWenigePunkte_Abgelehnt()
    {
        var p = MitSystemen();
        Abgelehnt(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "KW", ["punkte"] = new JsonArray(new JsonArray(0, 0, 0)),
        }));
        var e = Abgelehnt(p.Ausfuehren("leitung_ziehen", new JsonObject { ["system"] = "KW" }));
        Assert.Contains("punkte", e.Fehler);
    }

    [Fact]
    public void LeitungZiehen_ZweiterSchrittScheitert_AllesOderNichts()
    {
        var p = MitSystemen();
        Ok(Rohr(p, "KW", "RO-1"));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "RO-1.ende", ["nach"] = "WC-1.kw" }));
        int elemente = p.Modell.Elemente.Count, verbindungen = p.Modell.Verbindungen.Count, pakete = p.Verlauf.Count;

        // WC-1.kw ist schon belegt: die neue Leitung darf nicht halb entstehen
        var e = Abgelehnt(Rohr(p, "KW", "RO-2", 1000, new JsonObject { ["nach"] = "WC-1.kw" }));
        Assert.Contains("schon verbunden", e.Fehler);
        Assert.Equal(elemente, p.Modell.Elemente.Count);
        Assert.Equal(verbindungen, p.Modell.Verbindungen.Count);
        Assert.Equal(pakete, p.Verlauf.Count);
        Assert.Null(p.Modell.Finde("RO-2"));
    }

    [Fact]
    public void LeitungZiehen_RueckwaertsAngeschlossen_FliessrichtungWiderspruch()
    {
        var p = Neu();
        Ok(Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" }));
        Ok(Anlegen(p, "Hausanschluss", "HA-1", geo: Punkt(0, 0, 0), system: "KW"));
        // Hausanschluss.aus (raus) an das Leitungsende (raus)
        var e = Abgelehnt(Rohr(p, "KW", "RO-1", 1000, new JsonObject { ["nach"] = "HA-1.aus" }));
        Assert.Contains("Fliessrichtung", e.Fehler);
    }

    // ---------------------------------------------------------------- Trockenlauf und unbekannte Parameter (Gesamtschau P7, 30.10.2026)

    [Fact]
    public void Trockenlauf_UnterDenParametern_SpeichertNichts()
    {
        var p = Neu();
        var r = Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB-9", ["trockenlauf"] = true }));
        Assert.True(r.Trockenlauf);
        Assert.Null(p.Modell.Finde("GEB-9"));
    }

    [Fact]
    public void UnbekannterParameter_Abgelehnt_MitGueltigenParametern()
    {
        var p = Neu();
        var r = p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichn"] = "GEB-9" });
        Assert.False(r.Angenommen);
        Assert.Contains("kennzeichn", r.Fehler);
        Assert.Contains("kennzeichen", r.Vorschlag);
        Assert.Null(p.Modell.Finde("GEB-9"));
    }
}
