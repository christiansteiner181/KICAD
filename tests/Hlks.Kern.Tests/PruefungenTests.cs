using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Prüfungen P01–P11. Die Befehle verhindern die meisten Fehlerzustände; die Prüfungen sind aber für kaputte oder
/// widersprüchliche Pakete eines anderen Geräts da. Die Tests bauen deshalb ein gültiges Grundmodell über Befehle und
/// verfälschen eine Kopie davon mit selbst gebauten Operationen (wie ein fremdes Paket über Modell.Anwenden).</summary>
public class PruefungenTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    // ---------------------------------------------------------------- Aufbau des Grundmodells

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl} abgelehnt: {e.Fehler} {e.Vorschlag}");
        return e;
    }

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

    static BefehlsErgebnis Anlegen(Projekt p, string klasse, string kz, JsonObject? eig = null, JsonObject? geo = null,
        string? teilVon = null, string? system = null, JsonObject? bezuege = null, string? geschoss = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (eig is not null) o["eigenschaften"] = eig;
        if (geo is not null) o["geometrie"] = geo;
        if (teilVon is not null) o["teilVon"] = teilVon;
        if (system is not null) o["system"] = system;
        if (bezuege is not null) o["bezuege"] = bezuege;
        if (geschoss is not null) o["geschoss"] = geschoss;
        return Ok(p.Ausfuehren("element_anlegen", o, trockenlauf: false));
    }

    static BefehlsErgebnis Leitung(Projekt p, string strang, string kz, double[][] punkte, string? von = null, string? nach = null)
    {
        var o = new JsonObject
        {
            ["strang"] = strang, ["kennzeichen"] = kz,
            ["punkte"] = new JsonArray(punkte.Select(q => (JsonNode)new JsonArray(q.Select(c => (JsonNode)c).ToArray())).ToArray()),
        };
        if (von is not null) o["von"] = von;
        if (nach is not null) o["nach"] = nach;
        return Ok(p.Ausfuehren("leitung_ziehen", o, trockenlauf: false));
    }

    /// <summary>Gültiges Grundmodell: Gebäude, EG, Raum R.1, Systeme KW und SW mit je einem Strang, Hausanschluss HA-1 →
    /// Rohr RO-KW → WC-1.kw, WC-1.sw → Rohr RO-SW → Kontrollschacht KS-1, Datenpunkt DP-1 an WC-1. Alles verbunden.</summary>
    static Projekt Grund()
    {
        var p = Projekt.Anlegen(Kat, null, "Pruefung", "T", "pc");
        Anlegen(p, "Gebaeude", "GEB-1");
        Anlegen(p, "Geschoss", "EG", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 }, teilVon: "GEB-1");
        Anlegen(p, "Raum", "R.1", geo: Rechteck(0, 0, 5000, 5000), geschoss: "EG");
        Anlegen(p, "System", "KW", new JsonObject { ["systemtyp"] = "Trinkwasser kalt" });
        Anlegen(p, "System", "SW", new JsonObject { ["systemtyp"] = "Schmutzwasser" });
        Anlegen(p, "Strang", "KW-S1", new JsonObject { ["art"] = "steigstrang" }, teilVon: "KW");
        Anlegen(p, "Strang", "SW-S1", new JsonObject { ["art"] = "anschlussleitung" }, teilVon: "SW");
        Anlegen(p, "Hausanschluss", "HA-1", geo: Punkt(500, 500, 0), system: "KW");
        Anlegen(p, "WC", "WC-1", geo: Punkt(1000, 1000, 0));
        Anlegen(p, "Kontrollschacht", "KS-1", geo: Punkt(4000, 4000, 0), system: "SW");
        Leitung(p, "KW-S1", "RO-KW", [[500, 500, 0], [1000, 1000, 0]], von: "HA-1.aus", nach: "WC-1.kw");
        Leitung(p, "SW-S1", "RO-SW", [[1000, 1000, 0], [4000, 4000, 0]], von: "WC-1.sw", nach: "KS-1.ein");
        Anlegen(p, "Datenpunkt", "DP-1", new JsonObject { ["signal"] = "DI", ["art"] = "meldung" }, bezuege: new JsonObject { ["an"] = "WC-1" });
        return p;
    }

    // ---------------------------------------------------------------- Verfälschen über Operationen

    const string Paket = "test-paket";

    /// <summary>Ändert ein vorhandenes Element wie ein fremdes Paket: element.setzen mit Basis = aktuelle Version.</summary>
    static void Aendere(Modell m, string kz, Action<Element> aenderung)
    {
        var alt = m.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt im Modell");
        var neu = alt.Kopie();
        aenderung(neu);
        var konflikte = m.Anwenden([new Operation { Art = "element.setzen", Id = alt.Id, Basis = alt.Version, Element = neu, ElementVorher = alt }], Paket);
        Assert.Empty(konflikte);
    }

    /// <summary>Löscht ein Element ohne Rücksicht auf Verweise und Verbindungen (element.loeschen mit passender Basis).</summary>
    static void Loesche(Modell m, string kz)
    {
        var alt = m.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"{kz} fehlt im Modell");
        var konflikte = m.Anwenden([new Operation { Art = "element.loeschen", Id = alt.Id, Basis = alt.Version, ElementVorher = alt }], Paket);
        Assert.Empty(konflikte);
        Assert.Null(m.Finde(kz));
    }

    /// <summary>Neues Element als Kopie einer Vorlage mit eigener Id und Kennzeichen, ohne Verbindungen.</summary>
    static Element Neues(Modell m, string vorlage, string id, string kz, Action<Element>? aenderung = null)
    {
        var neu = m.Finde(vorlage)!.Kopie();
        neu.Id = id; neu.Kennzeichen = kz; neu.Version = 0; neu.Paket = null;
        aenderung?.Invoke(neu);
        var konflikte = m.Anwenden([new Operation { Art = "element.setzen", Id = id, Basis = 0, Element = neu }], Paket);
        Assert.Empty(konflikte);
        return m.Finde(id)!;
    }

    static Verbindung Verbinde(Modell m, string id, string vonId, string vonAnschluss, string nachId, string nachAnschluss)
    {
        var v = new Verbindung { Id = id, Von = new(vonId, vonAnschluss), Nach = new(nachId, nachAnschluss) };
        var konflikte = m.Anwenden([new Operation { Art = "verbindung.setzen", Id = id, Basis = 0, Verbindung = v }], Paket);
        Assert.Empty(konflikte);
        return m.FindeVerbindung(id)!;
    }

    // ---------------------------------------------------------------- Prüfen

    static List<Befund> Alle(Modell m) => Pruefungen.Alle(m, Kat);

    static List<Befund> Mit(Modell m, string code, string? element = null) =>
        Alle(m).Where(b => b.Code == code && (element is null || b.Element == element)).ToList();

    /// <summary>Erwartet mindestens einen Befund mit Code und Element; prüft Schwere, Elementbezug und Vorschlag.</summary>
    static List<Befund> Erwarte(Modell m, string code, string schwere, string element)
    {
        var treffer = Mit(m, code, element);
        Assert.NotEmpty(treffer);
        foreach (var b in treffer)
        {
            Assert.Equal(schwere, b.Schwere);
            Assert.Equal(element, b.Element);
            Assert.False(string.IsNullOrWhiteSpace(b.Vorschlag), $"{b.Code} {b.Element} ohne Vorschlag");
            Assert.False(string.IsNullOrWhiteSpace(b.Text));
            Assert.True(Pruefungen.Codes.ContainsKey(b.Code));
        }
        return treffer;
    }

    static void Fehlt(Modell m, string code, string? element = null)
    {
        var treffer = Mit(m, code, element);
        Assert.True(treffer.Count == 0, "Unerwartet: " + string.Join("; ", treffer.Select(b => $"{b.Code} {b.Element}: {b.Text}")));
    }

    // ---------------------------------------------------------------- Allgemeines

    [Fact]
    public void P04_NichtPlatziertesEndgeraet_MeldetNurP17()   // Modul 3 (Hauptlauf): ohne Geometrie kein Raum – P17 genügt
    {
        var p = SchemaansichtHilfe.Testhaus();
        var wc = p.Modell.Hole("WC-O");
        Assert.Null(wc.Geometrie);
        var befunde = Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Element == "WC-O").ToList();
        Assert.Contains(befunde, b => b.Code == "P17");
        Assert.DoesNotContain(befunde, b => b.Code == "P04");
    }

    [Fact]
    public void Codes_EnthaltenGenauP01BisP26()
    {
        var erwartet = Enumerable.Range(1, 26).Select(i => $"P{i:D2}").ToList();   // P12 seit Modul 1c, P13–P18 seit Modul 3, P19/P20 seit 3b, P21–P25 seit Modul 4 Teil A, P26 seit Prüfbericht 4A (B2)
        Assert.Equal(erwartet, Pruefungen.Codes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
        Assert.All(Pruefungen.Codes.Values, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.Equal("Apparat ohne Weg im Netz – Belastung fehlt stromab/stromauf", Pruefungen.Codes["P26"]);
    }

    [Fact]
    public void Grundmodell_IstSauber()
    {
        var p = Grund();
        var befunde = Alle(p.Modell).Where(b => b.Code is not ("P18" or "P23")).ToList();   // P18 (Modul 3): Rohre ohne Serie, nur Hinweis; P23 (Modul 4): das Grundmodell hat keine Anlage, nur Hinweis
        Assert.Contains(Alle(p.Modell), b => b.Code == "P23" && b.Element == "WC-1" && b.Schwere == "hinweis");
        Assert.True(befunde.Count == 0, "Befunde im Grundmodell: " + string.Join("; ", befunde.Select(b => $"{b.Code} {b.Element}: {b.Text}")));
        Assert.Equal(["KW-S1", "SW-S1"], Mit(p.Modell, "P18").Select(b => b.Element).Order());   // je Strang zusammengefasst (Christian 19.10.2026)
        Assert.Equal(4, p.Modell.Verbindungen.Count); // HA-1 – RO-KW – WC-1 – RO-SW – KS-1
    }

    [Fact]
    public void Beispielprojekt_HatKeineFehlerOderWarnungen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var befunde = Alle(p.Modell);
        Assert.DoesNotContain(befunde, b => b.Schwere is "fehler" or "warnung");
        // Waschtische usw. haben offene WW-Anschlüsse; das Projekt hat kein WW-System, also kein Befund dazu
        Assert.DoesNotContain(befunde, b => b.Text.Contains("(WW)"));
        Assert.All(befunde, b => Assert.True(Pruefungen.Codes.ContainsKey(b.Code)));
    }

    [Fact]
    public void Sortierung_FehlerVorWarnungVorHinweis()
    {
        var p = Grund();
        Leitung(p, "KW-S1", "RO-OFFEN", [[0, 0, 0], [500, 0, 0]]);                 // hinweis (P01)
        var m = p.Modell.Kopie();
        Aendere(m, "WC-1", e => e.Raum = null);                                     // warnung (P04)
        Aendere(m, "EG", e => e.Eigenschaften.Remove("kote"));                      // fehler (P10)
        var befunde = Alle(m);

        Assert.Equal(["fehler", "warnung", "hinweis"], befunde.Select(b => b.Schwere).Distinct().ToList());
        var rang = befunde.Select(b => b.Schwere switch { "fehler" => 0, "warnung" => 1, _ => 2 }).ToList();
        Assert.Equal(rang.Order().ToList(), rang);
        // innerhalb einer Schwere nach Code
        foreach (var gruppe in befunde.GroupBy(b => b.Schwere))
        {
            var codes = gruppe.Select(b => b.Code).ToList();
            Assert.Equal(codes.Order(StringComparer.Ordinal).ToList(), codes);
        }
    }

    [Fact]
    public void Schwere_ProCode_LautPruefungen()
    {
        // Ein Modell mit möglichst allen Befunden; jede Schwere wird je Code festgehalten.
        var m = Grund().Modell.Kopie();
        Aendere(m, "RO-KW", e => e.System = null);                                  // P02
        Aendere(m, "WC-1", e => { e.Raum = null; e.Geschoss = null; });             // P04, P11
        Aendere(m, "DP-1", e => e.Bezuege.Clear());                                 // P06
        Aendere(m, "KW-S1", e => e.TeilVon = null);                                 // P09
        Aendere(m, "EG", e => e.Eigenschaften.Remove("kote"));                      // P10
        Neues(m, "WC-1", "x-dup", "WC-1");                                          // P05 (und P01 am Duplikat)
        var schwere = Alle(m).GroupBy(b => b.Code).ToDictionary(g => g.Key, g => g.Select(b => b.Schwere).Distinct().Single());
        Assert.Equal("hinweis", schwere["P01"]);
        Assert.Equal("fehler", schwere["P02"]);
        Assert.Equal("warnung", schwere["P04"]);
        Assert.Equal("fehler", schwere["P05"]);
        Assert.Equal("warnung", schwere["P06"]);
        Assert.Equal("fehler", schwere["P09"]);
        Assert.Equal("fehler", schwere["P10"]);
        Assert.Equal("warnung", schwere["P11"]);
    }

    // ---------------------------------------------------------------- P01 Offener Anschluss

    [Fact]
    public void P01_OffenesRohr_Hinweis()
    {
        var p = Grund();
        Leitung(p, "KW-S1", "RO-OFFEN", [[0, 0, 0], [500, 0, 0]]);
        var b = Erwarte(p.Modell, "P01", "hinweis", "RO-OFFEN");
        Assert.Equal(2, b.Count);
        Assert.Contains(b, x => x.Text.Contains("«anfang»") && x.Text.Contains("KW"));
        Assert.Contains(b, x => x.Text.Contains("«ende»"));
        // das verbundene Rohr bleibt ohne Befund
        Fehlt(p.Modell, "P01", "RO-KW");
    }

    [Fact]
    public void P01_AngeschlossenesNetz_HatKeinenOffenenAnschluss()
    {
        var p = Grund();
        Fehlt(p.Modell, "P01");
    }

    [Fact]
    public void P01_GetrenntesGeraet_WirdGemeldet()
    {
        var p = Grund();
        Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "WC-1.kw" }, trockenlauf: false));
        var wc = Erwarte(p.Modell, "P01", "hinweis", "WC-1");
        Assert.Contains(wc, x => x.Text.Contains("«kw»"));
        Assert.DoesNotContain(wc, x => x.Text.Contains("«sw»")); // sw bleibt verbunden
        Erwarte(p.Modell, "P01", "hinweis", "RO-KW");
    }

    [Fact]
    public void P01_OffenerWwAnschlussAmApparat_ErstMitWwSystem()
    {
        var p = Grund();
        Anlegen(p, "Waschtisch", "WT-1", geo: Punkt(2000, 2000, 0));
        // KW und SW gibt es im Projekt: die offenen Anschlüsse sw und kw werden gemeldet, ww noch nicht
        var vorher = Erwarte(p.Modell, "P01", "hinweis", "WT-1");
        Assert.Contains(vorher, x => x.Text.Contains("«kw»"));
        Assert.Contains(vorher, x => x.Text.Contains("«sw»"));
        Assert.DoesNotContain(vorher, x => x.Text.Contains("«ww»"));

        // sobald das Projekt ein System mit Medium WW hat, ist der offene WW-Anschluss ein Befund
        Anlegen(p, "System", "WW", new JsonObject { ["systemtyp"] = "Trinkwasser warm" });
        var nachher = Erwarte(p.Modell, "P01", "hinweis", "WT-1");
        var ww = Assert.Single(nachher, x => x.Text.Contains("«ww»"));
        Assert.Contains("(WW)", ww.Text);
    }

    [Fact]
    public void P01_OffenerAnschlussEinesAnderenMediums_FehltOhneSystemDazu()
    {
        // Grundmodell hat nur KW und SW; ein Heizkörper (HZ-VL/HZ-RL) bleibt ohne Befund, ein Rohr im selben Modell nicht
        var p = Grund();
        Anlegen(p, "Heizkoerper", "HK-1", geo: Punkt(2000, 2000, 0));
        Fehlt(p.Modell, "P01", "HK-1");
        Leitung(p, "KW-S1", "RO-OFFEN", [[0, 0, 0], [500, 0, 0]]);
        Erwarte(p.Modell, "P01", "hinweis", "RO-OFFEN");
    }

    // ---------------------------------------------------------------- P02 Leitungsbauteil ohne System

    [Fact]
    public void P02_RohrOhneSystem_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "RO-KW", e => e.System = null);
        var b = Assert.Single(Erwarte(m, "P02", "fehler", "RO-KW"));
        Assert.Contains("Rohr", b.Text);
    }

    [Fact]
    public void P02_GegentestGrundmodellUndGeraeteOhneSystem()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P02");
        // Apparate und Datenpunkte brauchen kein System
        Assert.Null(m.Finde("WC-1")!.System);
        Assert.Null(m.Finde("DP-1")!.System);
        Fehlt(m, "P02", "WC-1");
        Fehlt(m, "P02", "DP-1");
    }

    [Fact]
    public void P02_Formteil_UndHausanschlussOhneSystem()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "HA-1", e => e.System = null);
        Erwarte(m, "P02", "fehler", "HA-1");
        Aendere(m, "KS-1", e => e.System = null);
        Erwarte(m, "P02", "fehler", "KS-1");
    }

    // ---------------------------------------------------------------- P03 Medien passen nicht

    [Fact]
    public void P03_KwRohrAnSwAnschlussDesWc_Fehler()
    {
        var m = Grund().Modell.Kopie();
        var rohr = Neues(m, "RO-KW", "t-a-rohr", "RO-X");
        var wc = Neues(m, "WC-1", "t-b-wc", "WC-2");
        // Rohr.anfang (KW, rein) mit WC.sw (SW, raus): Fliessrichtung passt, Medien nicht
        Verbinde(m, "t-v1", rohr.Id, "anfang", wc.Id, "sw");

        var b = Assert.Single(Erwarte(m, "P03", "fehler", "RO-X"));
        Assert.Contains("RO-X.anfang (KW)", b.Text);
        Assert.Contains("WC-2.sw (SW)", b.Text);
        Fehlt(m, "P07"); // nur die Medien sind falsch, nicht die Richtung
    }

    [Fact]
    public void P03_GegentestPassendeMedien()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P03");
        // KW-Rohr an kw-Anschluss eines zweiten WC: passt
        var rohr = Neues(m, "RO-KW", "t-a-rohr", "RO-X");
        var wc = Neues(m, "WC-1", "t-b-wc", "WC-2");
        Verbinde(m, "t-v1", rohr.Id, "ende", wc.Id, "kw");
        Fehlt(m, "P03");
        Fehlt(m, "P07");
    }

    [Fact]
    public void P03_UnbestimmtesMediumGiltAlsPassend()
    {
        // Ein Rohr ohne System behält «*» nur, wenn der Anschluss es so führt; hier wird das Medium am Anschluss auf «*» gesetzt
        var m = Grund().Modell.Kopie();
        var rohr = Neues(m, "RO-KW", "t-a-rohr", "RO-X", e => { e.System = null; foreach (var a in e.Anschluesse) a.Medium = "*"; });
        var wc = Neues(m, "WC-1", "t-b-wc", "WC-2");
        Verbinde(m, "t-v1", rohr.Id, "anfang", wc.Id, "sw");
        Fehlt(m, "P03");
    }

    // ---------------------------------------------------------------- P04 Element ohne Raum

    [Fact]
    public void P04_ApparatOhneRaum_Warnung()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "WC-1", e => e.Raum = null);
        var b = Assert.Single(Erwarte(m, "P04", "warnung", "WC-1"));
        Assert.Contains("WC", b.Text);
    }

    [Fact]
    public void P04_Gegentest()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P04");
        // Leitungen, Hausanschluss und Datenpunkte brauchen keinen Raum
        Aendere(m, "RO-KW", e => e.Raum = null);
        Aendere(m, "HA-1", e => e.Raum = null);
        Aendere(m, "DP-1", e => e.Raum = null);
        Fehlt(m, "P04");
    }

    // ---------------------------------------------------------------- P05 Kennzeichen doppelt

    [Fact]
    public void P05_DoppeltesKennzeichen_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Neues(m, "WC-1", "x-dup", "WC-1");
        var b = Assert.Single(Erwarte(m, "P05", "fehler", "WC-1"));
        Assert.Contains("2-mal", b.Text);
        Assert.Equal(2, m.Elemente.Count(e => e.Kennzeichen == "WC-1"));
    }

    [Fact]
    public void P05_GrossKleinschreibungZaehltNicht_UndDreifach()
    {
        var m = Grund().Modell.Kopie();
        Neues(m, "WC-1", "x-dup1", "wc-1");
        Neues(m, "WC-1", "x-dup2", "Wc-1");
        var b = Assert.Single(Mit(m, "P05"));
        Assert.Equal("fehler", b.Schwere);
        Assert.Equal("WC-1", b.Element, ignoreCase: true);
        Assert.Contains("3-mal", b.Text);
        Assert.False(string.IsNullOrWhiteSpace(b.Vorschlag));
    }

    [Fact]
    public void P05_GegentestEindeutigeKennzeichen()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P05");
        Neues(m, "WC-1", "x-neu", "WC-2");
        Fehlt(m, "P05");
    }

    // ---------------------------------------------------------------- P06 Datenpunkt ohne gültigen Bezug

    [Fact]
    public void P06_DatenpunktOhneBezugAn_Warnung()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "DP-1", e => e.Bezuege.Clear());
        var b = Assert.Single(Erwarte(m, "P06", "warnung", "DP-1"));
        Assert.Contains("bezuege.an", b.Text);
        Fehlt(m, "P08"); // kein Verweis ins Leere, nur kein Bezug
    }

    [Fact]
    public void P06_DatenpunktMitAnderemBezugAberOhneAn_Warnung()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "DP-1", e => { e.Bezuege.Clear(); e.Bezuege["station"] = "WC-1"; });
        Erwarte(m, "P06", "warnung", "DP-1");
    }

    [Fact]
    public void P06_BezugAnAufFehlendesElement_AuchP08()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "DP-1", e => e.Bezuege["an"] = "gibt-es-nicht");
        Erwarte(m, "P06", "warnung", "DP-1");
        Erwarte(m, "P08", "fehler", "DP-1");
    }

    [Fact]
    public void P06_GegentestGueltigerBezug()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P06");
        // Bezug auf Id statt Kennzeichen gilt ebenso
        Aendere(m, "DP-1", e => e.Bezuege["an"] = m.Finde("RO-KW")!.Id);
        Fehlt(m, "P06");
        // andere Klassen brauchen keinen Bezug «an»
        Fehlt(m, "P06", "WC-1");
    }

    // ---------------------------------------------------------------- P07 Fliessrichtung widersprüchlich

    [Fact]
    public void P07_RausMitRaus_Fehler()
    {
        var m = Grund().Modell.Kopie();
        var a = Neues(m, "RO-KW", "t-a-rohr", "RO-A");
        var b = Neues(m, "RO-KW", "t-b-rohr", "RO-B");
        Verbinde(m, "t-v1", a.Id, "ende", b.Id, "ende");
        var f = Assert.Single(Erwarte(m, "P07", "fehler", "RO-A"));
        Assert.Contains("RO-A.ende", f.Text);
        Assert.Contains("RO-B.ende", f.Text);
        Assert.Contains("raus", f.Text);
        Fehlt(m, "P03");
    }

    [Fact]
    public void P07_ReinMitRein_Fehler()
    {
        var m = Grund().Modell.Kopie();
        var a = Neues(m, "RO-KW", "t-a-rohr", "RO-A");
        var b = Neues(m, "RO-KW", "t-b-rohr", "RO-B");
        Verbinde(m, "t-v1", a.Id, "anfang", b.Id, "anfang");
        var f = Assert.Single(Erwarte(m, "P07", "fehler", "RO-A"));
        Assert.Contains("rein", f.Text);
    }

    [Fact]
    public void P07_Gegentest_ReinMitRaus_UndFormteileOhneRichtung()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P07");
        var a = Neues(m, "RO-KW", "t-a-rohr", "RO-A");
        var b = Neues(m, "RO-KW", "t-b-rohr", "RO-B");
        Verbinde(m, "t-v1", a.Id, "ende", b.Id, "anfang"); // raus – rein
        Fehlt(m, "P07");

        // «beide» (Formteil) an «raus» ist kein Widerspruch
        var p = Grund();
        Anlegen(p, "Bogen", "BO-1", geo: Punkt(3000, 3000, 0), system: "KW");
        var m2 = p.Modell.Kopie();
        var rohr = Neues(m2, "RO-KW", "t-c-rohr", "RO-C");
        Verbinde(m2, "t-v2", rohr.Id, "ende", m2.Finde("BO-1")!.Id, "a");
        Fehlt(m2, "P07");
    }

    // ---------------------------------------------------------------- P08 Verweis ins Leere

    [Theory]
    [InlineData("KW-S1", "RO-KW", "teilVon")]
    [InlineData("R.1", "WC-1", "raum")]
    [InlineData("KW", "RO-KW", "system")]
    [InlineData("EG", "WC-1", "geschoss")]
    public void P08_VerweisAufGeloeschtesElement_Fehler(string geloescht, string betroffen, string rolle)
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P08");
        Loesche(m, geloescht);
        var treffer = Erwarte(m, "P08", "fehler", betroffen);
        Assert.Contains(treffer, b => b.Text.StartsWith($"{rolle} verweist auf ein fehlendes Element"));
    }

    [Fact]
    public void P08_BezugInsLeere_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Loesche(m, "WC-1"); // DP-1.bezuege.an zeigt jetzt ins Leere
        var treffer = Erwarte(m, "P08", "fehler", "DP-1");
        Assert.Contains(treffer, b => b.Text.Contains("Bezug «an»"));
    }

    [Fact]
    public void P08_VerbindungAufGeloeschtesElement_Fehler()
    {
        var m = Grund().Modell.Kopie();
        var kw = m.VerbindungAn(m.Finde("WC-1")!.Id, "kw")!;
        var sw = m.VerbindungAn(m.Finde("WC-1")!.Id, "sw")!;
        Loesche(m, "WC-1"); // die Verbindungen bleiben stehen
        var b1 = Assert.Single(Erwarte(m, "P08", "fehler", kw.Id));
        Assert.Contains("fehlenden Anschluss", b1.Text);
        Assert.Contains(".kw", b1.Text);
        var b2 = Assert.Single(Erwarte(m, "P08", "fehler", sw.Id));
        Assert.Contains(".sw", b2.Text);
    }

    [Fact]
    public void P08_VerbindungAufFehlendenAnschlussNamen_Fehler()
    {
        var m = Grund().Modell.Kopie();
        var kw = m.VerbindungAn(m.Finde("WC-1")!.Id, "kw")!;
        Aendere(m, "WC-1", e => e.Anschluesse.RemoveAll(a => a.Name == "kw"));
        var b = Assert.Single(Erwarte(m, "P08", "fehler", kw.Id));
        Assert.Contains(".kw", b.Text);
    }

    [Fact]
    public void P08_GegentestAlleVerweiseGueltig()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P08");
        // Id statt Kennzeichen als Verweis ist ebenfalls gültig
        Aendere(m, "WC-1", e => e.Raum = m.Finde("R.1")!.Id);
        Fehlt(m, "P08");
    }

    // ---------------------------------------------------------------- P09 Strang ohne System

    [Fact]
    public void P09_StrangOhneTeilVon_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "KW-S1", e => e.TeilVon = null);
        var b = Assert.Single(Erwarte(m, "P09", "fehler", "KW-S1"));
        Assert.Contains("System", b.Text);
    }

    [Fact]
    public void P09_StrangTeilVonAnderemAlsSystem_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "KW-S1", e => e.TeilVon = m.Finde("GEB-1")!.Id);
        Erwarte(m, "P09", "fehler", "KW-S1");
        Fehlt(m, "P08"); // der Verweis ist gültig, nur die Klasse stimmt nicht
    }

    [Fact]
    public void P09_StrangTeilVonFehlendemElement_AuchP08()
    {
        var m = Grund().Modell.Kopie();
        Loesche(m, "KW");
        Erwarte(m, "P09", "fehler", "KW-S1");
        Erwarte(m, "P08", "fehler", "KW-S1");
    }

    [Fact]
    public void P09_Gegentest()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P09");
        // nur Stränge werden geprüft: ein Rohr ohne teilVon ist kein P09
        Aendere(m, "RO-KW", e => e.TeilVon = null);
        Fehlt(m, "P09");
    }

    // ---------------------------------------------------------------- P10 Pflichteigenschaft fehlt

    [Fact]
    public void P10_GeschossOhneKote_Fehler()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "EG", e => e.Eigenschaften.Remove("kote"));
        var b = Assert.Single(Erwarte(m, "P10", "fehler", "EG"));
        Assert.Contains("«kote»", b.Text);
        Assert.Contains("kote", b.Vorschlag);
    }

    [Fact]
    public void P10_SystemOhneSystemtyp_UndDatenpunktOhneSignal()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "KW", e => e.Eigenschaften.Remove("systemtyp"));
        Aendere(m, "DP-1", e => { e.Eigenschaften.Remove("signal"); e.Eigenschaften.Remove("art"); });
        Assert.Contains("«systemtyp»", Assert.Single(Erwarte(m, "P10", "fehler", "KW")).Text);
        var dp = Erwarte(m, "P10", "fehler", "DP-1");
        Assert.Equal(2, dp.Count);
        Assert.Contains(dp, b => b.Text.Contains("«signal»"));
        Assert.Contains(dp, b => b.Text.Contains("«art»"));
    }

    [Fact]
    public void P10_Gegentest_NichtPflichtfelderDuerfenFehlen()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P10");
        // das Medium des Systems und die Strangart sind keine Pflicht
        Aendere(m, "KW-S1", e => e.Eigenschaften.Remove("art"));
        Aendere(m, "SW", e => e.Eigenschaften.Remove("medium"));
        Fehlt(m, "P10");
    }

    // ---------------------------------------------------------------- P11 Element ohne Geschoss

    [Fact]
    public void P11_ApparatOhneGeschoss_Warnung()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "WC-1", e => e.Geschoss = null);
        var b = Assert.Single(Erwarte(m, "P11", "warnung", "WC-1"));
        Assert.Contains("WC", b.Text);
    }

    [Fact]
    public void P11_RohrUndRaumOhneGeschoss_Warnung()
    {
        var m = Grund().Modell.Kopie();
        Aendere(m, "RO-KW", e => e.Geschoss = null);
        Aendere(m, "R.1", e => e.Geschoss = null);
        Erwarte(m, "P11", "warnung", "RO-KW");
        Erwarte(m, "P11", "warnung", "R.1");
    }

    [Fact]
    public void P11_Gegentest()
    {
        var m = Grund().Modell.Kopie();
        Fehlt(m, "P11");
        // Datenpunkt, Strang, System, Kontrollschacht brauchen kein Geschoss
        Assert.Null(m.Finde("DP-1")!.Geschoss);
        Assert.Null(m.Finde("KW-S1")!.Geschoss);
        Assert.Null(m.Finde("KW")!.Geschoss);
        Fehlt(m, "P11", "DP-1");
        Fehlt(m, "P11", "KW-S1");
        Fehlt(m, "P11", "KS-1");
    }
}
