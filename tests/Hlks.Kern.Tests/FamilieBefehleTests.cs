using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamilieTestHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B1: Befehle familie_entwurf, familie_aendern, familie_laden, familie_platzieren, Nachführung der
/// Elemente (Anschlüsse, Kennwerte) im selben Paket, Prüfungen bei element_anlegen/element_aendern, Abfragen.
/// Handrechnungen mit der Testfamilie (fiktive Werte).</summary>
public class FamilieBefehleTests
{
    static AnschlussLage Lage(Projekt p, string kz, string anschluss) =>
        Familien.Anschlusslagen(p.Modell, p.Katalog, Hole(p, kz)).Single(a => a.Name == anschluss);

    // ------------------------------------------------------------------ familie_entwurf

    [Fact]
    public void Entwurf_LegtFamilieAnMitVersionUndStatus()
    {
        var p = Neu();
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper() }));
        var f = Hole(p, "FAM-1");
        Assert.Equal("Familie", f.Klasse);
        Assert.Equal(HkName, f.Name);
        Assert.Equal("Heizkoerper", f.Text("kategorie"));
        Assert.Equal("entwurf", f.Text("status"));
        Assert.Equal(1, f.Zahl("version"));
        var def = (JsonObject)f.Eigenschaft("definition")!.Inhalt!;
        Assert.Equal(1, def["version"]!.GetValue<int>());
        Assert.Equal("entwurf", def["status"]!.GetValue<string>());
        Assert.Contains(e.Hinweise, h => h.Contains("11 Parameter, 3 Typ(en), 4 Körper, 2 Anschlüsse"));
        Assert.Contains(e.Aenderungen, a => a.Art == "angelegt" && a.Kennzeichen == "FAM-1");
    }

    [Fact]
    public void Entwurf_TrockenlaufAendertNichtsUndRueckgaengigEntferntDieFamilie()
    {
        using var umgebung = Hlks.Tests.Gemeinsam.TestUmgebung.Ohne();   // T1: kein automatisches Schreiben in eine von aussen gesetzte Bibliothek
        var p = Neu();
        var vorher = p.Modell.Pruefsumme();
        var t = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper() }, trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.Single(t.Aenderungen);
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper() }));
        Assert.NotNull(p.Modell.Finde("FAM-1"));
        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Finde("FAM-1"));
        Assert.Equal(vorher, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Entwurf_ErsetzenErhoehtVersionGleicheDefinitionIstKeineAenderung()
    {
        var p = MitFamilie();
        var gleich = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper(), ["familie"] = "FAM-1" }));
        Assert.Empty(gleich.Aenderungen);
        var d = Testheizkoerper(); d["beschreibung"] = "geändert";
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d, ["familie"] = HkName }));   // über den Namen
        Assert.Equal(2, Hole(p, "FAM-1").Zahl("version"));
        // zweite Familie gleichen Namens bzw. gleicher id wird abgelehnt
        Assert.Contains("gibt es schon", Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Testheizkoerper() })).Fehler);
    }

    [Fact]
    public void Entwurf_OhneIdBekommtFesteKennung()
    {
        var p = Neu();
        var d = Testheizkoerper(); d.Remove("id");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.StartsWith("fam-", Familien.DefinitionsId(Hole(p, "FAM-1")));
    }

    [Fact]
    public void Entwurf_TypfehlerWerdenHinweiseDieFamilieWirdTrotzdemGespeichert()
    {
        // Typ ohne hoehe: Entwurf mit Lücke (wie aus Unterlagen) – speicherbar, aber nicht platzierbar
        var p = Neu();
        var d = Testheizkoerper();
        ((JsonArray)d["typen"]!).Add(new JsonObject { ["name"] = "ohne-hoehe", ["werte"] = new JsonObject { ["laenge"] = 500 } });
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains(e.Hinweise, h => h.Contains("Typ «ohne-hoehe» lässt sich so nicht platzieren") && h.Contains("«hoehe» hat keinen Wert"));
        var n = Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "ohne-hoehe", ["punkt"] = new JsonArray(0, 0) }));
        Assert.Contains("hat keinen Wert", n.Fehler);
    }

    // ------------------------------------------------------------------ familie_platzieren: Handrechnung

    [Fact]
    public void Platzieren_Handrechnung_AnschlusslagenBeiDrehung90()
    {
        // Typ 22-600-1000 bei (5000, 3000, 0), Drehung 90° (Gegenuhrzeigersinn): (x, y) → (−y, x)
        //   vl Familie: [laenge − randabstand, tiefe/2, bodenabstand − stutzen] = [1000 − 50, 100/2, 150 − 30] = [950, 50, 120]
        //      gedreht (−50, 950) → Welt (5000 − 50, 3000 + 950, 0 + 120) = (4950, 3950, 120)
        //   rl Familie: [1000 − 50 − 50, 50, 120] = [900, 50, 120] → (−50, 900) → (4950, 3900, 120)
        //   Richtung [0, 0, −1] bleibt (Drehung um z)
        var p = MitHeizkoerper();
        var hk = Hole(p, "HK-1");
        Assert.Equal("Heizkoerper", hk.Klasse);
        Assert.Equal("22-600-1000", hk.Text("familientyp"));
        Assert.Equal(90, hk.Zahl("drehung"));
        Assert.Equal(Hole(p, "FAM-1").Id, hk.Bezuege["familie"]);
        Assert.Equal("EG", Hole(p, hk.Geschoss!).Kennzeichen);   // Geschoss aus der Höhe abgeleitet wie bei Punkt-Elementen
        var vl = Lage(p, "HK-1", "vl"); var rl = Lage(p, "HK-1", "rl");
        Etwa([4950, 3950, 120], vl.Lage, 1e-9, "vl");
        Etwa([4950, 3900, 120], rl.Lage, 1e-9, "rl");
        Etwa([0, 0, -1], vl.Richtung, 1e-12);
        Assert.Equal(15, vl.Dn);
        Assert.Equal("HZ-VL", vl.Medium);
        Assert.Equal("rein", vl.Fluss);
        Assert.Equal("raus", rl.Fluss);
        // Anschlüsse am Element aus der Familie
        Assert.Equal(["vl", "rl"], hk.Anschluesse.Select(a => a.Name));
        Assert.All(hk.Anschluesse, a => Assert.Equal(15, a.Dn));
        // leistung aus dem Typ, Herkunft familie
        var l = hk.Eigenschaft("leistung")!;
        Assert.Equal(1000, l.AlsZahl());
        Assert.Equal("W", l.Einheit);
        Assert.Equal("familie", l.Herkunft.Art);
        Assert.Equal($"{HkName} Typ 22-600-1000", l.Herkunft.Quelle);
    }

    [Fact]
    public void Platzieren_OhneZAufDerGeschosskote()
    {
        var p = MitFamilie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0), ["geschoss"] = "OG" }));
        var hk = Hole(p, "HK-1");
        Assert.Equal(3000, hk.Geometrie!.Punkte[0][2]);
        Etwa([950, 50, 3120], Lage(p, "HK-1", "vl").Lage, 1e-9);   // Drehung 0: Familienkoordinaten + Kote
    }

    [Fact]
    public void Platzieren_TrockenlaufUndRueckgaengig()
    {
        var p = MitFamilie();
        var vorher = p.Modell.Pruefsumme();
        var t = Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(1, 2, 0) }, trockenlauf: true));
        Assert.Contains(t.Aenderungen, a => a.Art == "angelegt" && a.Klasse == "Heizkoerper");
        Assert.Equal(vorher, p.Modell.Pruefsumme());
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(1, 2, 0) }));
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Platzieren_FehlerMitVorschlag()
    {
        var p = MitFamilie();
        Assert.Contains("22-600-1000", Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "gibts-nicht", ["punkt"] = new JsonArray(0, 0) })).Vorschlag);
        Assert.Contains("Typparameter", Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0), ["parameter"] = new JsonObject { ["hoehe"] = 700 } })).Fehler);
        Assert.Contains("Formel", Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0), ["parameter"] = new JsonObject { ["oberkante"] = 5 } })).Fehler);
        Assert.Contains("FAM-1", Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-9", ["typ"] = "x", ["punkt"] = new JsonArray(0, 0) })).Vorschlag);
        Assert.Contains("punkt", Nein(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = "hier" })).Fehler);
    }

    // ------------------------------------------------------------------ Nachführung

    [Fact]
    public void Typwechsel_FuehrtAnschluesseUndLeistungNach()
    {
        // 22-900-800: laenge 800 → vl Familie [800 − 50, 50, 120] = [750, 50, 120] → Welt (4950, 3750, 120); rl [700, 50, 120] → (4950, 3700, 120)
        var p = MitHeizkoerper();
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "22-900-800" } }));
        Assert.All(e.Aenderungen, a => Assert.Equal("HK-1", a.Kennzeichen));   // ein Paket: Typ, dann Anschlüsse und Kennwert (Nachführung)
        Assert.NotNull(e.PaketId);
        Etwa([4950, 3750, 120], Lage(p, "HK-1", "vl").Lage, 1e-9);
        Etwa([4950, 3700, 120], Lage(p, "HK-1", "rl").Lage, 1e-9);
        var l = Hole(p, "HK-1").Eigenschaft("leistung")!;
        Assert.Equal(1100, l.AlsZahl());
        Assert.Equal($"{HkName} Typ 22-900-800", l.Herkunft.Quelle);
        Ok(p.Rueckgaengig());
        Assert.Equal(1000, Hole(p, "HK-1").Zahl("leistung"));
    }

    [Fact]
    public void Exemplarwert_Bodenabstand_OberkantePerFormel()
    {
        // bodenabstand 300 (statt 150): oberkante = bodenabstand + hoehe = 300 + 600 = 900; Anschlüsse z = 300 − 30 = 270
        var p = MitHeizkoerper();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["parameter"] = new JsonObject { ["bodenabstand"] = 300 } } }));
        var a = Familien.Aufloesung(p.Modell, p.Katalog, Hole(p, "HK-1"), out var grund)!;
        Assert.Null(grund);
        Assert.Equal(900, a.Wert("oberkante")!.Value.Zahl);
        Assert.Equal(270, Lage(p, "HK-1", "vl").Lage[2]);
        // Körper: Platten von z 300 bis 900, Abdeckung 900 … 910
        var platten = a.Teile("mittel")[0];
        Etwa(600, platten.Mitte![2]);
        Etwa(600, platten.Masse![2]);
    }

    [Fact]
    public void ElementAnlegenUndAendern_PruefenFamilieKategorieTyp()
    {
        var p = MitFamilie();
        JsonObject Anlegen(string klasse, string? typ) => new()
        {
            ["klasse"] = klasse, ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([0, 0, 0]) },
            ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["eigenschaften"] = typ is null ? new JsonObject() : new JsonObject { ["familientyp"] = typ },
        };
        Assert.Contains("Kategorie Heizkoerper", Nein(p.Ausfuehren("element_anlegen", Anlegen("WC", "22-600-1000"))).Fehler);
        Assert.Contains("22-600-1000", Nein(p.Ausfuehren("element_anlegen", Anlegen("Heizkoerper", "falsch"))).Vorschlag);
        Assert.Contains("familientyp fehlt", Nein(p.Ausfuehren("element_anlegen", Anlegen("Heizkoerper", null))).Fehler);
        Ok(p.Ausfuehren("element_anlegen", Anlegen("Heizkoerper", "22-600-1400")));
        var hk = p.Modell.DerKlasse("Heizkoerper").Single();
        Assert.Equal(1400, hk.Zahl("leistung"));            // Nachführung auch über element_anlegen
        Assert.Equal(2, hk.Anschluesse.Count);
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = hk.Kennzeichen, ["eigenschaften"] = new JsonObject { ["parameter"] = new JsonObject { ["gibtsnicht"] = 1 } } }));
        Nein(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = hk.Kennzeichen, ["bezuege"] = new JsonObject { ["familie"] = "EG" } }));
    }

    [Fact]
    public void ZielwertVonHand_WirdAusDerFamilieErsetztMitHinweis()
    {
        var p = MitHeizkoerper();
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["leistung"] = 5000 } }));
        Assert.Equal(1000, Hole(p, "HK-1").Zahl("leistung"));
        Assert.Contains(e.Hinweise, h => h.Contains("leistung kommt aus der Familie"));
    }

    [Fact]
    public void FamilieAendern_FuehrtPlatzierteElementeImSelbenPaketNach()
    {
        var p = MitHeizkoerper();
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject
            {
                ["op"] = "typ_setzen", ["typ"] = new JsonObject { ["name"] = "22-600-1000", ["werte"] = new JsonObject { ["hoehe"] = 600, ["laenge"] = 1200, ["leistung"] = 1250 } },
            }),
        }));
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "FAM-1");
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "HK-1");
        Assert.Equal(1250, Hole(p, "HK-1").Zahl("leistung"));
        Etwa([4950, 3000 + 1150, 120], Lage(p, "HK-1", "vl").Lage, 1e-9);   // laenge 1200: vl x_f = 1150
        Assert.Equal(2, Hole(p, "FAM-1").Zahl("version"));
        Ok(p.Rueckgaengig());   // alles in einem Paket
        Assert.Equal(1000, Hole(p, "HK-1").Zahl("leistung"));
        Assert.Equal(1, Hole(p, "FAM-1").Zahl("version"));
    }

    [Fact]
    public void FamilieAendern_AnschlussEntfaelltVerbindungWirdGetrennt()
    {
        var p = MitHeizkoerper();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Verteiler", ["kennzeichen"] = "VT-1", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([1000, 1000, 0]) },
        }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "HK-1.rl", ["nach"] = "VT-1.rl_1" }));
        Assert.NotNull(p.Modell.VerbindungAn(Hole(p, "HK-1").Id, "rl"));
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "anschluss_loeschen", ["name"] = "rl" }),
        }));
        Assert.Contains(e.Hinweise, h => h.Contains("HK-1.rl gibt es in der Familie nicht mehr") && h.Contains("VT-1.rl_1"));
        Assert.Contains(e.Aenderungen, a => a.Art == "getrennt");
        Assert.Equal(["vl"], Hole(p, "HK-1").Anschluesse.Select(a => a.Name));
        Assert.Empty(p.Modell.Verbindungen);
        Ok(p.Rueckgaengig());
        Assert.Single(p.Modell.Verbindungen);
    }

    [Fact]
    public void FamilieAendern_VerwendeterTypDarfNichtVerschwinden()
    {
        var p = MitHeizkoerper();
        var n = Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_loeschen", ["name"] = "22-600-1000" }) }));
        Assert.Contains("wird von HK-1 verwendet", n.Fehler);
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "typ_loeschen", ["name"] = "22-600-1400" }) }));
    }

    [Fact]
    public void FamilieAendern_AlleOps()
    {
        var p = MitHeizkoerper();
        var ops = new JsonArray(
            new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "dicke_abdeckung", ["art"] = "typ", ["datentyp"] = "laenge", ["wert"] = 10 } },
            new JsonObject { ["op"] = "koerper_setzen", ["koerper"] = new JsonObject { ["id"] = "abdeckung", ["name"] = "Abdeckung", ["art"] = "quader", ["material"] = "geraet",
                ["detailgrad"] = new JsonArray("mittel", "fein"), ["sichtbar"] = "mit_abdeckung",
                ["von"] = new JsonArray(-5, -5, "oberkante"), ["bis"] = new JsonArray("laenge + 5", "tiefe + 5", "oberkante + dicke_abdeckung") } },
            new JsonObject { ["op"] = "koerper_setzen", ["koerper"] = new JsonObject { ["id"] = "knopf", ["art"] = "kugel", ["mitte"] = new JsonArray(50, -10, 700), ["r"] = 10, ["detailgrad"] = new JsonArray("fein") } },
            new JsonObject { ["op"] = "anschluss_setzen", ["anschluss"] = new JsonObject { ["name"] = "entl", ["medium"] = "*", ["fluss"] = "beide", ["lage"] = new JsonArray(10, 50, "oberkante"), ["richtung"] = new JsonArray(0, 0, 1) } },
            new JsonObject { ["op"] = "sinnbild_setzen", ["sinnbild"] = new JsonArray(new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(new JsonArray(0, 0), new JsonArray("laenge", 0), new JsonArray("laenge", "tiefe"), new JsonArray(0, "tiefe")) }) },
            new JsonObject { ["op"] = "kopf_setzen", ["hersteller"] = "keiner", ["beschreibung"] = null },
            new JsonObject { ["op"] = "parameter_loeschen", ["name"] = "leistung" });
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = ops }));
        var def = Familien.Definition(Hole(p, "FAM-1"), p.Katalog);
        Assert.True(def.Gueltig);
        Assert.Equal(5, def.Koerper.Count);
        Assert.NotNull(def.Sinnbild);
        Assert.False(def.Quelle.ContainsKey("beschreibung"));
        Assert.Equal("keiner", def.Quelle["hersteller"]!.GetValue<string>());
        Assert.All(def.Typen, t => Assert.False(t.Werte.ContainsKey("leistung")));   // Typwerte des gelöschten Parameters entfallen
        var hk = Hole(p, "HK-1");
        Assert.Equal(["vl", "rl", "entl"], hk.Anschluesse.Select(a => a.Name));
        Assert.Null(hk.Eigenschaft("leistung"));   // Ziel ohne Parameter entfällt (Herkunft familie)
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "HK-1");
        Assert.Contains("op «x» gibt es nicht", Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "x" }) })).Fehler);
        Assert.Contains("gibt es in koerper nicht", Nein(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "koerper_loeschen", ["id"] = "x" }) })).Fehler);
    }

    [Fact]
    public void FamilieAendern_UngueltigeAenderungWirdMitStelleAbgelehnt()
    {
        var p = MitFamilie();
        var n = Nein(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "oberkante", ["art"] = "typ", ["datentyp"] = "laenge", ["formel"] = "bodenabstand + hoehe" } }),
        }));
        Assert.Contains("Typparameter «oberkante» verwendet den Exemplarparameter «bodenabstand»", n.Fehler);
        Assert.Equal(1, Hole(p, "FAM-1").Zahl("version"));
    }

    [Fact]
    public void ExemplarwertNachFamilienaenderungUngueltig_EntfaelltMitHinweis()
    {
        var p = MitHeizkoerper(parameter: new JsonObject { ["bodenabstand"] = 200 });
        // bodenabstand wird Typparameter: Exemplarwert passt nicht mehr – entfällt, das Element bleibt
        var e = Ok(p.Ausfuehren("familie_aendern", new JsonObject
        {
            ["familie"] = "FAM-1",
            ["ops"] = new JsonArray(
                new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "bodenabstand", ["art"] = "typ", ["datentyp"] = "laenge", ["wert"] = 150 } },
                new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "oberkante", ["art"] = "typ", ["datentyp"] = "laenge", ["formel"] = "bodenabstand + hoehe" } }),
        }));
        Assert.Null(Hole(p, "HK-1").Eigenschaft("parameter"));
        Assert.Contains(e.Hinweise, h => h.Contains("Exemplarwerte passen nicht mehr"));
    }

    [Fact]
    public void FamilieGeloescht_ElementeBleibenOhneBezugMitHinweis()
    {
        // element_loeschen lehnt eine verwendete Familie ab (Bezug); die Nachführung deckt andere Wege ab (direkt geprüft)
        var p = MitHeizkoerper();
        Assert.Contains("verwendet", Nein(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "FAM-1" })).Fehler);
        var k = new Kontext(p, p.Modell.Kopie(), new Herkunft { Art = "mensch", Akteur = "T", Zeit = DateTimeOffset.Now });
        k.Loeschen(k.Arbeit.Finde("FAM-1")!);
        Nachfuehrungen.Ausfuehren(k);
        var hk = k.Arbeit.Finde("HK-1")!;
        Assert.False(hk.Bezuege.ContainsKey("familie"));
        Assert.Equal(1000, hk.Zahl("leistung"));              // Werte bleiben
        Assert.Equal(2, hk.Anschluesse.Count);                // Anschlüsse bleiben
        Assert.Contains(k.Hinweise, h => h.Contains("gelöscht") && h.Contains("Klassendarstellung"));
    }

    // ------------------------------------------------------------------ familie_laden

    [Fact]
    public void Laden_AusDateiMitPruefsummeUndAktualisieren()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-familien-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ordner);
        try
        {
            var datei = Path.Combine(ordner, "hk.familie.json");
            File.Copy(TestheizkoerperPfad, datei);
            var p = Neu();
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
            var f = Hole(p, "FAM-1");
            var bib = (JsonObject)f.Eigenschaft("bibliothek")!.Inhalt!;
            Assert.Equal(Path.GetFullPath(datei), bib["datei"]!.GetValue<string>());
            Assert.Equal(64, bib["pruefsumme"]!.GetValue<string>().Length);
            Assert.Equal("entwurf", f.Text("status"));   // Status der Datei bleibt
            Assert.Empty(Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei })).Aenderungen);   // gleiche Datei: keine Änderung
            Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(0, 0), ["kennzeichen"] = "HK-1" }));
            var d = Testheizkoerper();
            ((JsonObject)((JsonArray)d["typen"]!)[0]!)["werte"]!["leistung"] = 999;
            File.WriteAllText(datei, d.ToJsonString());
            var e = Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
            Assert.Single(p.Modell.DerKlasse("Familie"));   // dieselbe Familie (gleiche id) aktualisiert
            Assert.Equal(2, Hole(p, "FAM-1").Zahl("version"));
            Assert.Equal(999, Hole(p, "HK-1").Zahl("leistung"));
            Assert.Contains(e.Hinweise, h => h.Contains("aus der Bibliothek aktualisiert"));
            Assert.Contains("gibt es nicht", Nein(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = "gibt_es_nicht" })).Fehler);
        }
        finally { Directory.Delete(ordner, true); }
    }

    // ------------------------------------------------------------------ Abfragen

    [Fact]
    public void Abfragen_FamilienFamilieAnschlusslagen()
    {
        var p = MitHeizkoerper();
        var liste = (JsonObject)Tuer.Abfrage(p, "familien");
        var f = (JsonObject)liste["familien"]![0]!;
        Assert.Equal("FAM-1", f["kennzeichen"]!.GetValue<string>());
        Assert.Equal(1, f["verwendungen"]!.GetValue<int>());
        Assert.Equal(3, f["typen"]!.AsArray().Count);
        Assert.True(f["gueltig"]!.GetValue<bool>());

        var fam = (JsonObject)Tuer.Abfrage(p, "familie", new JsonObject { ["familie"] = "FAM-1" });
        var typ = fam["typen"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["name"]!.GetValue<string>() == "22-600-1000");
        Assert.Equal(750, typ["werte"]!["oberkante"]!.GetValue<double>());   // 150 + 600
        Assert.True(typ["werte"]!["mit_abdeckung"]!.GetValue<double>() == 1);
        Assert.Equal("HK-1", fam["verwendungen"]![0]!.GetValue<string>());

        var al = (JsonObject)Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "HK-1" });
        var vl = al["anschluesse"]!.AsArray().Select(x => x!.AsObject()).Single(x => x["name"]!.GetValue<string>() == "vl");
        Assert.Equal([4950, 3950, 120], vl["lage"]!.AsArray().Select(x => x!.GetValue<double>()));
        Assert.Equal("HZ-VL", vl["medium"]!.GetValue<string>());
        Assert.Null(vl["verbunden"]);
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "anschlusslagen", new JsonObject { ["element"] = "EG" }));
    }

    [Fact]
    public void Abfragen_StehenInDerListeUndFaehigkeiten()
    {
        foreach (var n in new[] { "familien", "familie", "anschlusslagen", "familie_ansicht" }) Assert.Contains(Abfragen.Liste, a => a.Name == n);
        var p = Neu();
        foreach (var n in new[] { "familie_entwurf", "familie_aendern", "familie_laden", "familie_platzieren" }) Assert.NotNull(p.Befehle.Hole(n));
    }
}
