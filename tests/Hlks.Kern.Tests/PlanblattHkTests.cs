using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil A: Planblatt wie H+K – Plankopf mit zwei Standorten (aus kataloge/buero.json), Revisionen und Planänderungen, Plannummern-Vorschlag,
/// Befund P27, Revisionsstempel, Schema-Kopf, Listen auf Planblättern. Alle Projekte fiktiv (Beispielprojekt «MFH Musterweg»); Werte der Plannummern
/// folgen der H+K-Konvention der Bernstrasse-Pläne (H01–H06 über sechs Geschosse, H13–H16 FBH, S06–S10 Sonderblätter).</summary>
public class PlanblattHkTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static BefehlsErgebnis Ok(BefehlsErgebnis e) { Assert.True(e.Angenommen, e.Fehler + " " + e.Vorschlag); return e; }
    static BefehlsErgebnis Nein(BefehlsErgebnis e) { Assert.False(e.Angenommen); return e; }
    static JsonObject Par(params (string, object?)[] kv) => new(kv.Select(x => KeyValuePair.Create(x.Item1, x.Item2 is null ? null : x.Item2 is JsonNode n ? n : JsonValue.Create(x.Item2))));

    /// <summary>Beispielprojekt mit Projektinformation (Projektnummer 1-0768, Projekt «MFH Bernstrasse 42» – so heisst nur der Prüftext, nicht das Projekt).</summary>
    static Projekt Haus(bool sechsGeschosse = false)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Projektinformation"),
            ("eigenschaften", new JsonObject { ["phase"] = "Revision", ["zeile1"] = "MFH Musterweg fiktiv", ["zeile2"] = "Musterweg 1, 3000 Bern", ["projektnummer"] = "1-0768" }))));
        if (sechsGeschosse)
        {
            // wie Bernstrasse: 2.UG, 1.UG, EG, 1.OG, 2.OG, Attika (UG des Beispielprojekts gilt hier als 1.UG)
            foreach (var (kz, name, kote) in new[] { ("2.UG", "2. Untergeschoss", -5400), ("2.OG", "2. Obergeschoss", 5800), ("AT", "Attika", 8700) })
                Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", kz), ("name", name), ("teilVon", "GEB-1"),
                    ("eigenschaften", new JsonObject { ["kote"] = kote, ["hoehe"] = 2900 }))));
        }
        return p;
    }

    static JsonObject Vorschlag(Projekt p, string? gewerk = null, string? geschoss = null, string? ansicht = null, string? art = null) =>
        (JsonObject)Tuer.Abfrage(p, "plannummer_vorschlag", Par(("gewerk", gewerk), ("geschoss", geschoss), ("ansicht", ansicht), ("art", art)));

    static string Nr(JsonObject v) => v["nummer"]?.GetValue<string>() ?? "";

    static string Pdf(Projekt p, string blatt = "PB-1", Func<Projekt, string, Plangrafik>? grafik = null) =>
        Encoding.Latin1.GetString(Planausgabe.Pdf(p, blatt, unkomprimiert: true, grafik));

    /// <summary>Alle Zeichenketten in Klammern (PDF-Textoperatoren).</summary>
    static List<string> Texte(string pdf) => Regex.Matches(pdf, @"\(((?:[^()\\]|\\.)*)\)").Select(m => m.Groups[1].Value).ToList();

    static Plangrafik Leer(Projekt _, string ansicht)
    {
        var g = new Plangrafik { Ansicht = ansicht, Art = "grundriss", Massstab = 50 };
        g.Teile.Add(new Grafikteil { Ebene = "wand", Primitive = [new Primitiv { Art = "linie", Punkte = [[0, 0], [8000, 0]], Stil = new Stil { Stift = Stifte.Dick } }] });
        g.Begrenzung = Hlks.Kern.Grafik.Begrenzung(g.Teile);
        return g;
    }

    // ================================================================== 1. Absender aus dem Bürokatalog

    [Fact]
    public void Standorte_AusBueroJson_BolligenUndThunMitWerten()
    {
        var s = Buero.Standorte(Kat.Ordner);
        Assert.Equal(["bolligen", "thun"], s.Select(x => x.Schluessel).ToArray());
        var b = s[0]; var t = s[1];
        Assert.Equal(("H+K Planungs AG, Bolligen", "Dorfmärit 9", "3065 Bolligen", "031 / 924 25 35", "infob@hkplanung.ch", "www.hkplanung.ch"),
            (b.Firma, b.Strasse, b.PlzOrt, b.Telefon, b.Mail, b.Web));
        Assert.Equal(("H+K Planungs AG, Thun", "Kasernenstrasse 17", "3600 Thun", "033 / 225 25 45", "infot@hkplanung.ch", null),
            (t.Firma, t.Strasse, t.PlzOrt, t.Telefon, t.Mail, t.Web));   // Web für Thun steht auf keinem gelesenen Plan: nichts geraten
        Assert.All(s, x => Assert.Contains("PdfPig", x.Quelle ?? "") );  // Quelle: Textauszug der echten Plan-PDF
    }

    [Fact]
    public void Standorte_FehlendeDatei_GibtLeereListeStattRaten()
    {
        var leer = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "kat-leer-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(leer);
        try { Assert.Empty(Buero.Standorte(leer)); }
        finally { Directory.Delete(leer, true); }
    }

    [Fact]
    public void Plankopf_ZeigtBeideStandorteMitAdresseTelefonMail()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "Heizung Erdgeschoss"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-GR-EG"))));
        var t = Texte(Pdf(p, grafik: Leer));
        foreach (var erwartet in new[] { "H+K Planungs AG, Bolligen", "H+K Planungs AG, Thun", "Kasernenstrasse 17", "3065 Bolligen", "3600 Thun", "Tel: 031 / 924 25 35",
                     "Tel: 033 / 225 25 45", "Mail: infob@hkplanung.ch", "Mail: infot@hkplanung.ch" })
            Assert.Contains(erwartet, t);
        Assert.Contains(t, x => x.StartsWith("Dorfm"));                                   // «Dorfmärit 9» (Umlaut als Escape)
        foreach (var feld in new[] { "Projektphase:", "Projekt:", "Plannummer:", "Erstelldatum:", "Rev. Index:", "Rev. Datum:", "Kontrolliert:", "Massstab:" })
            Assert.Contains(feld, t);
        Assert.Contains("1-0768 H02", t);
        Assert.Contains("1 : 50", t);                                                      // Massstab im H+K-Format
    }

    [Fact]
    public void Plankopf_ProjektZweizeilig_ObjektUndPlantitel()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "Heizung Erdgeschoss"))));
        var t = Texte(Pdf(p, grafik: Leer));
        Assert.Contains("MFH Musterweg fiktiv", t);                                      // Zeile 1: Objekt (Projektinformation)
        Assert.Contains("Heizung Erdgeschoss", t);                                         // Zeile 2: Plantitel (Planblatt)
        Assert.Equal(1, t.Count(x => x == "Heizung Erdgeschoss"));                         // ohne Titelband nur einmal
    }

    [Fact]
    public void Titelband_VorgabeAus_EinschaltbarAmBlattUndInDerProjektinformation()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "Heizung Erdgeschoss"))));
        var aus = Texte(Pdf(p, grafik: Leer));
        Assert.DoesNotContain("Planinhalt", aus);                                          // Vorgabe aus (H+K-Pläne haben kein Band)
        Assert.DoesNotContain(aus, x => x.StartsWith("Masse in"));

        // Projekteinstellung
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PI-1"), ("eigenschaften", new JsonObject { ["titelband"] = true }))));
        var an = Texte(Pdf(p, grafik: Leer));
        Assert.Contains("Planinhalt", an);
        Assert.Contains(an, x => x.StartsWith("Masse in"));
        Assert.Equal(2, an.Count(x => x == "Heizung Erdgeschoss"));                        // Band und Projektfeld

        // das Blatt kann abweichen
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["titelband"] = false }))));
        Assert.DoesNotContain("Planinhalt", Texte(Pdf(p, grafik: Leer)));
    }

    [Fact]
    public void Plankopf_MitTitelbandIstHoeherAlsOhne()
    {
        Assert.True(Blattformate.KopfHoehe("A3", HkPlankopf.Art.Grundriss, 1, true) > Blattformate.KopfHoehe("A3", HkPlankopf.Art.Grundriss, 1, false));
        Assert.Equal(10, Blattformate.KopfHoehe("A3", HkPlankopf.Art.Grundriss, 1, true) - Blattformate.KopfHoehe("A3", HkPlankopf.Art.Grundriss, 1, false), 0.01);
    }

    // ================================================================== 2. Revisionen und Planänderungen

    static Projekt MitBlatt()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "Heizung Erdgeschoss"))));
        return p;
    }

    static List<(string, string, string)> Revs(Projekt p, string blatt = "PB-1") =>
        Revisionen.Lesen(p.Modell.Hole(blatt)).Select(r => (r.Index, r.Datum, r.Beschreibung)).ToList();

    [Fact]
    public void Revision_NaechsterBuchstabeDatumHeuteBeschreibungPflicht()
    {
        var p = MitBlatt();
        var e = Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Anpassungen Beschriftungen"))));
        var heute = DateTime.Now.ToString("dd.MM.yyyy");
        Assert.Equal([("A", heute, "Anpassungen Beschriftungen")], Revs(p));
        Assert.Contains(e.Hinweise, h => h.Contains("Revision A") && h.Contains("PB-1"));
        Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "1-0768 H02"), ("beschreibung", "Beschriftungen ergänzt"), ("datum", "11.06.2025"))));   // über die Plannummer, Datum angegeben
        Assert.Equal([("A", heute, "Anpassungen Beschriftungen"), ("B", "11.06.2025", "Beschriftungen ergänzt")], Revs(p));

        var leer = Nein(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "  "))));
        Assert.Contains("leer", leer.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(leer.Vorschlag));
        Nein(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"))));            // Beschreibung ist Pflicht
        Nein(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-9"), ("beschreibung", "x"))));
        Nein(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "x"), ("datum", "gestern"))));
        Assert.Equal(2, Revs(p).Count);
    }

    [Theory]
    [InlineData(new string[0], "A")]
    [InlineData(new[] { "A" }, "B")]
    [InlineData(new[] { "A", "B", "C" }, "D")]
    [InlineData(new[] { "B", "A" }, "C")]            // der höchste zählt, nicht der letzte
    [InlineData(new[] { "Y" }, "Z")]
    [InlineData(new[] { "Z" }, "AA")]
    [InlineData(new[] { "AZ" }, "BA")]
    [InlineData(new[] { "1", "-" }, "A")]            // Ziffern und Striche zählen nicht
    public void Revision_IndexReihe(string[] bestehende, string erwartet) =>
        Assert.Equal(erwartet, Revisionen.NaechsterIndex(bestehende.Select(i => new Revision(i, "", ""))));

    [Fact]
    public void Revision_TrockenlaufAendertNichts_RueckgaengigNimmtSieZurueck_HerkunftStehtDran()
    {
        var p = MitBlatt();
        var start = p.Modell.Pruefsumme();
        var trocken = Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Test")), trockenlauf: true));
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.NotEmpty(trocken.Aenderungen);

        Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Test"))));
        var w = p.Modell.Hole("PB-1").Eigenschaft("revisionen")!;
        Assert.False(string.IsNullOrWhiteSpace(w.Herkunft.Akteur));                        // Herkunft: wer, wann
        Assert.Equal("revision_hinzufuegen", w.Herkunft.Befehl);
        Ok(p.Rueckgaengig());
        Assert.Empty(Revs(p));
        Assert.Equal(start, p.Modell.Pruefsumme());
    }

    [Fact]
    public void Plankopf_RevIndexDatumUndPlanaenderungstabelle()
    {
        var p = MitBlatt();
        Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "div. Anpassungen Beschriftungen"), ("datum", "20.05.2025"))));
        Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Beschriftungen ergänzt"), ("datum", "11.06.2025"))));
        var t = Texte(Pdf(p, grafik: Leer));
        Assert.Contains("Rev. Index:", t);
        Assert.Contains("B", t);                                                           // Index der letzten Revision
        Assert.Contains("11.06.2025", t);                                                  // Datum der letzten Revision (Rev. Datum und Zeile B)
        Assert.Equal(2, t.Count(x => x == "11.06.2025"));
        Assert.Contains("20.05.2025", t);
        Assert.Contains("div. Anpassungen Beschriftungen", t);
        Assert.Contains("Beschreibung", t);
        Assert.Contains("Index", t);
        Assert.Contains("A", t);
    }

    [Fact]
    public void Plankopf_WaechstNachObenMitJederZeile_UndZeigtNurDieLetztenZwoelf()
    {
        double H(int zeilen) => Blattformate.KopfHoehe("A3", HkPlankopf.Art.Grundriss, zeilen, false);
        Assert.Equal(H(1), H(0));                                                          // keine Revision: eine Strichzeile
        Assert.Equal(3.4, H(2) - H(1), 0.001);                                             // je Zeile 3.4 mm (Referenzbreite 180 mm)
        Assert.True(H(5) > H(4) && H(12) > H(11));
        Assert.Equal(H(12), H(40));                                                        // der Kopf wächst nicht über zwölf Zeilen

        var p = MitBlatt();
        for (int i = 1; i <= 14; i++) Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Änderung Nr " + i), ("datum", "01.01.2026"))));
        var t = Texte(Pdf(p, grafik: Leer));
        Assert.DoesNotContain("Änderung Nr 1", t);
        Assert.DoesNotContain(t, x => x.StartsWith("nderung Nr 2") || x == "Änderung Nr 2");
        Assert.Contains(t, x => x.EndsWith("derung Nr 14"));
        Assert.Contains("N", t);                                                           // letzter Index (14. Revision = N)
    }

    [Fact]
    public void Plankopf_OhneRevisionEineStrichzeile_AlteAenderungslisteBleibtLesbar()
    {
        var p = MitBlatt();
        var t = Texte(Pdf(p, grafik: Leer));
        Assert.True(t.Count(x => x == "-") >= 3);                                          // Index, Datum, Beschreibung
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject
        {
            ["aenderungen"] = new JsonArray(new JsonObject { ["index"] = "a", ["datum"] = "18.10.2026", ["text"] = "Fenster verschoben" }),
        }))));
        Assert.Contains("Fenster verschoben", Texte(Pdf(p, grafik: Leer)));                // ohne revisionen gilt die ältere Liste
        Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "Neu"))));
        var nach = Texte(Pdf(p, grafik: Leer));
        Assert.DoesNotContain("Fenster verschoben", nach);                                 // mit revisionen gelten nur diese
        Assert.Contains("Neu", nach);
    }

    [Fact]
    public void PlansatzRevision_AlleGewaehltenBlaetterMitGleicherBeschreibung()
    {
        using var t = new PlansatzProjekt();
        // H01 hat schon eine Revision A: bekommt B, H02 bekommt A; abgewählte Blätter bleiben unverändert
        PlansatzProjekt.Ok(t.P.Ausfuehren("revision_hinzufuegen", PlansatzProjekt.Par("""{"planblatt":"1-0768 H01","beschreibung":"Erste Revision","datum":"01.01.2026"}""")));
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_setzen", PlansatzProjekt.Par("""{"plansatz":"auto:heizung","abgewaehlt":[]}""")));
        var e = PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"heizung","beschreibung":"Beschriftungen ergänzt","datum":"11.06.2025"}""")));
        Assert.Contains(e.Hinweise, h => h.Contains("2 Blättern") && h.Contains("H01 B") && h.Contains("H02 A"));
        var h01 = Revisionen.Lesen(t.P.Modell.Hole("PB-2")).Select(r => r.Index).ToArray();
        Assert.Equal(["A", "B"], h01);
        Assert.Equal([("A", "11.06.2025", "Beschriftungen ergänzt")], Revisionen.Lesen(t.P.Modell.Hole("PB-5")).Select(r => (r.Index, r.Datum, r.Beschreibung)).ToList());
        Assert.Empty(Revisionen.Lesen(t.P.Modell.Hole("PB-1")));                           // Sanitär S02: nicht im Satz Heizung

        // ein Blatt abgewählt: bleibt unverändert; ausdrückliche Auswahl (blaetter) hat Vorrang
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_setzen", PlansatzProjekt.Par("""{"plansatz":"heizung","abgewaehlt":["PB-5"]}""")));
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"heizung","beschreibung":"Nur H01"}""")));
        Assert.Equal(3, Revisionen.Lesen(t.P.Modell.Hole("PB-2")).Count);
        Assert.Single(Revisionen.Lesen(t.P.Modell.Hole("PB-5")));
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"heizung","beschreibung":"Nur H02","blaetter":["1-0768 H02"]}""")));
        Assert.Equal(2, Revisionen.Lesen(t.P.Modell.Hole("PB-5")).Count);
        Assert.Equal(3, Revisionen.Lesen(t.P.Modell.Hole("PB-2")).Count);
        var fremd = Nein(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"heizung","beschreibung":"x","blaetter":["1-0768 S01"]}""")));
        Assert.Contains("gehört nicht zum Plansatz", fremd.Fehler);
    }

    [Fact]
    public void PlansatzRevision_TrockenlaufRueckgaengigEinPaket()
    {
        using var t = new PlansatzProjekt();
        var start = t.P.Modell.Pruefsumme();
        var tr = PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"sanitaer","beschreibung":"Test"}"""), trockenlauf: true));
        Assert.Equal(start, t.P.Modell.Pruefsumme());
        Assert.Equal(2, tr.Aenderungen.Count);                                             // S01, S02
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"sanitaer","beschreibung":"Test"}""")));
        PlansatzProjekt.Ok(t.P.Rueckgaengig());                                            // ein Schritt macht alle zurück
        Assert.Equal(start, t.P.Modell.Pruefsumme());
        Nein(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"sanitaer","beschreibung":""}""")));
        Nein(t.P.Ausfuehren("plansatz_revision_hinzufuegen", PlansatzProjekt.Par("""{"plansatz":"kaelte","beschreibung":"x"}""")));   // Satz ohne Blätter
    }

    // ================================================================== 3. Plannummern-Vorschlag

    [Fact]
    public void Vorschlag_NachGeschossVonUntenHeizungAbEinsSanitaerAbNull()
    {
        var p = Haus(sechsGeschosse: true);                                                // 2.UG, UG, EG, 1.OG, 2.OG, AT nach Kote
        var h = new[] { "2.UG", "UG", "EG", "1.OG", "2.OG", "AT" }.Select(g => Nr(Vorschlag(p, "heizung", g))).ToArray();
        Assert.Equal(["1-0768 H01", "1-0768 H02", "1-0768 H03", "1-0768 H04", "1-0768 H05", "1-0768 H06"], h);   // Bernstrasse H01 … H06
        var s = new[] { "2.UG", "UG", "EG", "1.OG", "2.OG", "AT" }.Select(g => Nr(Vorschlag(p, "sanitaer", g))).ToArray();
        Assert.Equal(["1-0768 S00", "1-0768 S01", "1-0768 S02", "1-0768 S03", "1-0768 S04", "1-0768 S05"], s);   // Bernstrasse S00 … S05
        Assert.Equal("1-0768 L03", Nr(Vorschlag(p, "lueftung", "EG")));
        Assert.Equal("1-0768 K03", Nr(Vorschlag(p, "koordination", "EG")));               // Koordination K01–K06 wie auf den H+K-Plänen
    }

    [Fact]
    public void Vorschlag_GeschossreihenfolgeNachKoteNichtNachNameOderReihenfolgeDerAnlage()
    {
        var p = Haus();                                                                     // UG -2700, EG 0, 1.OG 2900 (angelegt in dieser Reihenfolge)
        Ok(p.Ausfuehren("element_aendern", Par(("element", "EG"), ("eigenschaften", new JsonObject { ["kote"] = 5000 }))));   // EG liegt jetzt über 1.OG? nein: 5000 > 2900
        // Reihenfolge nach Kote: UG (-2700), 1.OG (2900), EG (5000)
        Assert.Equal("1-0768 H01", Nr(Vorschlag(p, "heizung", "UG")));
        Assert.Equal("1-0768 H02", Nr(Vorschlag(p, "heizung", "1.OG")));
        Assert.Equal("1-0768 H03", Nr(Vorschlag(p, "heizung", "EG")));
    }

    [Fact]
    public void Vorschlag_TitelUndBegruendung()
    {
        var p = Haus();
        var v = Vorschlag(p, "heizung", "EG");
        Assert.Equal("Heizung Erdgeschoss", v["titel"]!.GetValue<string>());
        Assert.Equal("H", v["fach"]!.GetValue<string>());
        Assert.Equal("1-0768", v["projektnummer"]!.GetValue<string>());
        Assert.Contains("Geschoss 2 von 3", v["begruendung"]!.GetValue<string>());
        Assert.Equal("Sanitär Untergeschoss", Vorschlag(p, "sanitaer", "UG")["titel"]!.GetValue<string>());
    }

    [Fact]
    public void Vorschlag_AusAnsichtGeschossGewerkUndArt()
    {
        var p = Haus();
        // AN-GR-EG: Gewerke sanitaer + architektur → Sanitär, Geschoss EG
        Assert.Equal("1-0768 S01", Nr(Vorschlag(p, ansicht: "AN-GR-EG")));
        // mehrere HLKS-Gewerke → Koordination
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AN-GR-1.OG"), ("eigenschaften", new JsonObject { ["gewerke"] = new JsonArray("heizung", "lueftung") }))));
        var k = Vorschlag(p, ansicht: "AN-GR-1.OG");
        Assert.Equal("1-0768 K03", Nr(k));
        Assert.Equal("koordination", k["gewerk"]!.GetValue<string>());
        // ausdrückliche Angabe hat Vorrang vor der Ansicht
        Assert.Equal("1-0768 H01", Nr(Vorschlag(p, "heizung", "UG", "AN-GR-EG")));
    }

    [Fact]
    public void Vorschlag_Sonderblaetter_FbhSteigzonenGrundleitungenEinlagenKanalisation()
    {
        var p = Haus(sechsGeschosse: true);
        // FBH ab EG aufwärts: H13 … H16 (Bernstrasse)
        Assert.Equal(["1-0768 H13", "1-0768 H14", "1-0768 H15", "1-0768 H16"], new[] { "EG", "1.OG", "2.OG", "AT" }.Select(g => Nr(Vorschlag(p, "heizung", g, art: "fbh"))).ToArray());
        Assert.Equal("FBH Erdgeschoss", Vorschlag(p, "heizung", "EG", art: "fbh")["titel"]!.GetValue<string>());
        Assert.Null(Vorschlag(p, "heizung", "UG", art: "fbh")["nummer"]);                  // unter ±0.00 keine FBH-Nummer
        Assert.Equal("1-0768 H21", Nr(Vorschlag(p, "heizung", art: "steigzonen")));
        // Sanitär: sechs Geschosse S00–S05, Grundleitungen S06, Einlagen S07, Kanalisation S10
        Assert.Equal("1-0768 S06", Nr(Vorschlag(p, "sanitaer", "2.UG", art: "grundleitungen")));
        Assert.Equal("1-0768 S07", Nr(Vorschlag(p, "sanitaer", "2.UG", art: "einlagen")));
        Assert.Equal("1-0768 S10", Nr(Vorschlag(p, "sanitaer", art: "kanalisation")));
        Assert.Equal("Sanitär Einlagen D.ü.2.UG", Vorschlag(p, "sanitaer", "2.UG", art: "einlagen")["titel"]!.GetValue<string>());
        Assert.Equal("Grundleitungen 2.UG", Vorschlag(p, "sanitaer", "2.UG", art: "grundleitungen")["titel"]!.GetValue<string>());
        // falsches Fach für die Sonderart
        Assert.Null(Vorschlag(p, "lueftung", "EG", art: "fbh")["nummer"]);
        Assert.Null(Vorschlag(p, "heizung", "EG", art: "einlagen")["nummer"]);
        Assert.Contains("Sanitär", Vorschlag(p, "heizung", "EG", art: "einlagen")["begruendung"]!.GetValue<string>());
        Assert.Contains("xyz", Assert.Throws<HlksFehler>(() => Vorschlag(p, "heizung", art: "xyz")).Message);
    }

    [Fact]
    public void Vorschlag_EinlagenErsteFreieNummer_BelegtGehtWeiter()
    {
        var p = Haus(sechsGeschosse: true);
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 S07"), ("titel", "Sanitär Einlagen D.ü.2.UG"))));
        var v = Vorschlag(p, "sanitaer", "UG", art: "einlagen");
        Assert.Equal("1-0768 S08", Nr(v));                                                 // S07 ist von PB-1 belegt
        Assert.Contains(v["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("S07") && h.GetValue<string>().Contains("PB-1"));
        // normales Blatt: belegte Nummer → nächste freie des Fachs
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H03"), ("titel", "Heizung Erdgeschoss"))));
        var h3 = Vorschlag(p, "heizung", "EG");
        Assert.Equal("1-0768 H04", Nr(h3));
        Assert.Contains(h3["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("H03"));
    }

    [Fact]
    public void Vorschlag_KeinVorschlagOhneGewerkGeschossOderFachbuchstabe_UndSchemaVonHand()
    {
        var p = Haus();
        Assert.Null(Vorschlag(p)["nummer"]);
        Assert.Contains("Gewerk", Vorschlag(p)["begruendung"]!.GetValue<string>());
        Assert.Null(Vorschlag(p, "heizung")["nummer"]);                                    // Geschoss fehlt
        Assert.Contains("Geschoss", Vorschlag(p, "heizung")["begruendung"]!.GetValue<string>());
        var kaelte = Vorschlag(p, "kaelte", "EG");
        Assert.Null(kaelte["nummer"]);                                                     // Kälte und Elektro: kein Fachbuchstabe, nichts geraten
        Assert.Contains("Fachbuchstaben", kaelte["begruendung"]!.GetValue<string>());
        Assert.Null(Vorschlag(p, "elektro", "EG")["nummer"]);
        var schema = Vorschlag(p, ansicht: "AN-SCHEMA-SAN");
        Assert.Null(schema["nummer"]);                                                     // Schemas tragen nur die Projektnummer
        Assert.Equal("schema", schema["art"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => Vorschlag(p, "heizung", "XYZ"));                   // Geschoss gibt es nicht
    }

    [Fact]
    public void Vorschlag_OhneProjektnummerNurFachUndNummerMitHinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var v = Vorschlag(p, "heizung", "EG");
        Assert.Equal("H02", Nr(v));
        Assert.Contains(v["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("Projektnummer"));
    }

    [Fact]
    public void PlanblattAnlegen_OhneNummerUndTitelGiltDerVorschlag_Gewerk_Wird_Gesetzt()
    {
        var p = Haus();
        var e = Ok(p.Ausfuehren("planblatt_anlegen", Par(("gewerk", "heizung"), ("geschoss", "EG"))));
        var b = p.Modell.Hole("PB-1");
        Assert.Equal("1-0768 H02", b.Text("nummer"));
        Assert.Equal("Heizung Erdgeschoss", b.Text("titel"));
        Assert.Equal("heizung", b.Text("gewerk"));
        Assert.Contains(e.Hinweise, h => h.Contains("1-0768 H02") && h.Contains("vorgeschlagen"));

        // zweites Blatt gleicher Art: nächste freie Nummer, kein Doppel
        var zwei = Ok(p.Ausfuehren("planblatt_anlegen", Par(("gewerk", "heizung"), ("geschoss", "EG"))));
        Assert.Equal("1-0768 H03", p.Modell.Hole("PB-2").Text("nummer"));
        Assert.Contains(zwei.Hinweise, h => h.Contains("schon vergeben"));

        // Nummer angegeben, Titel fehlt: Titelvorschlag aus Gewerk und Geschoss; angegebene Nummer bleibt
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "X-1"), ("gewerk", "sanitaer"), ("geschoss", "UG"))));
        Assert.Equal(("X-1", "Sanitär Untergeschoss"), (p.Modell.Hole("PB-3").Text("nummer"), p.Modell.Hole("PB-3").Text("titel")));

        // über die Ansicht
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("ansicht", "AN-GR-1.OG"))));
        Assert.Equal("1-0768 S02", p.Modell.Hole("PB-4").Text("nummer"));
        Assert.Equal("Sanitär 1. Obergeschoss", p.Modell.Hole("PB-4").Text("titel"));
    }

    [Fact]
    public void PlanblattAnlegen_OhneNummerUndOhneVorschlagMoeglichIstEinFehlerMitWeg()
    {
        var p = Haus();
        var e = Nein(p.Ausfuehren("planblatt_anlegen", Par(("titel", "Irgendwas"))));
        Assert.Contains("Plannummer", e.Fehler);
        Assert.Contains("plannummer_vorschlag", e.Vorschlag);
        Assert.Empty(p.Modell.DerKlasse("Planblatt"));
        Nein(p.Ausfuehren("planblatt_anlegen", Par(("gewerk", "kaelte"), ("geschoss", "EG"))));
        // Trockenlauf legt nichts an, nennt aber den Vorschlag
        var tr = Ok(p.Ausfuehren("planblatt_anlegen", Par(("gewerk", "heizung"), ("geschoss", "EG")), trockenlauf: true));
        Assert.Contains(tr.Hinweise, h => h.Contains("1-0768 H02"));
        Assert.Empty(p.Modell.DerKlasse("Planblatt"));
        // doppelte Nummer bleibt abgelehnt
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "a"))));
        Assert.Contains("PB-1", Nein(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "b")))).Fehler);
    }

    // ================================================================== 4. Befund P27

    [Fact]
    public void P27_DoppelteNummerNenntNaechsteFreie_NormaleProjekteHabenKeinen()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H01"), ("titel", "a"))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H02"), ("titel", "b"))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 H03"), ("titel", "c"))));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P27");

        // element_aendern lässt die Doppelung zu; der Befund steht am späteren Blatt
        var e = Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-3"), ("eigenschaften", new JsonObject { ["nummer"] = "1-0768 h01" }))));   // Gross-/Kleinschreibung zählt nicht
        var b = Assert.Single(Pruefungen.Alle(p.Modell, p.Katalog), x => x.Code == "P27");
        Assert.Equal(("PB-3", "warnung"), (b.Element, b.Schwere));
        Assert.Contains("PB-1", b.Text);
        Assert.Contains("1-0768 H03", b.Vorschlag);                                        // nächste freie: eine über der höchsten des Fachs (H02; H03 ist durch die Umnummerierung frei)
        Assert.Contains(e.NeueBefunde, x => x.Code == "P27");                              // der Befehl meldet den neuen Befund

        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-3"), ("eigenschaften", new JsonObject { ["nummer"] = "1-0768 H04" }))));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), x => x.Code == "P27");
        Assert.Equal("Plannummer doppelt im Projekt", Pruefungen.Codes["P27"]);
    }

    [Fact]
    public void P27_DreifachUndOhneFachbuchstabe()
    {
        var p = Haus();
        foreach (var kz in new[] { "A", "B", "C" }) Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "N" + kz), ("titel", kz))));
        foreach (var kz in new[] { "PB-2", "PB-3" }) Ok(p.Ausfuehren("element_aendern", Par(("element", kz), ("eigenschaften", new JsonObject { ["nummer"] = "NA" }))));
        var befunde = Pruefungen.Alle(p.Modell, p.Katalog).Where(x => x.Code == "P27").ToList();
        Assert.Equal(["PB-2", "PB-3"], befunde.Select(x => x.Element).Order().ToArray());
        Assert.All(befunde, x => Assert.Contains("PB-1", x.Text));
        Assert.All(befunde, x => Assert.DoesNotContain("Nächste freie", x.Vorschlag ?? ""));   // «NA» hat kein Muster Fach+Zahl: nichts geraten
    }

    // ================================================================== 5. Revisionsstempel

    [Fact]
    public void Stempel_AmPlanblattRotSchraegUeberDemKopf_VorgabeKeiner()
    {
        var p = MitBlatt();
        Assert.DoesNotContain("Revisionsplan", Pdf(p, grafik: Leer));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["stempel"] = "Revisionsplan 21.01.2026" }))));
        var s = Pdf(p, grafik: Leer);
        Assert.Contains("(Revisionsplan 21.01.2026)", s);
        Assert.Contains("0.91 0.125 0.165 rg", s);                                         // #E8202A
        // gedreht um 12° (Drehmatrix cos 0.978, sin 0.208)
        Assert.Matches(@"0\.978\d* -?0\.20\d* -?0\.20\d* 0\.978", s);
        // leer = kein Stempel
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["stempel"] = null }))));
        Assert.DoesNotContain("Revisionsplan", Pdf(p, grafik: Leer));
    }

    [Fact]
    public void Stempel_SerienPdf_AngabeAmSatzDemBlattUndDerAusgabeMitVorrang()
    {
        using var t = new PlansatzProjekt();
        string Lies(JsonObject antwort)
        {
            var datei = antwort["dateien"]![0]!["datei"]!.GetValue<string>();
            using var doc = UglyToad.PdfPig.PdfDocument.Open(datei);
            return string.Join("\n", doc.GetPages().Select(pg => pg.Text));
        }
        JsonObject Satz(string json) => t.Pdf(PlansatzProjekt.Par(json));

        var ohne = Lies(Satz($$$"""{"plansatz":"heizung","ordner":"{{{t.Wurzel.Replace("\\", "/")}}}/a","ueberschreiben":true}"""));
        Assert.DoesNotContain("Revisionsplan", ohne);
        // Angabe der Ausgabe: alle Blätter
        var mit = Lies(Satz($$$"""{"plansatz":"heizung","ordner":"{{{t.Wurzel.Replace("\\", "/")}}}/b","stempel":"Revisionsplan 21.01.2026","ueberschreiben":true}"""));
        Assert.Equal(2, Regex.Matches(mit, "Revisionsplan 21.01.2026").Count);             // H01 und H02
        // Stempel am Plansatz
        PlansatzProjekt.Ok(t.P.Ausfuehren("plansatz_setzen", PlansatzProjekt.Par("""{"plansatz":"auto:heizung"}""")));
        var satz = t.P.Modell.DerKlasse("Plansatz").Single();
        PlansatzProjekt.Ok(t.P.Ausfuehren("element_aendern", PlansatzProjekt.Par($$$"""{"element":"{{{satz.Kennzeichen}}}","eigenschaften":{"stempel":"Satzstempel"}}""")));
        Assert.Equal(2, Regex.Matches(Lies(Satz($$$"""{"plansatz":"heizung","ordner":"{{{t.Wurzel.Replace("\\", "/")}}}/c","ueberschreiben":true}""")), "Satzstempel").Count);
        // Stempel am Blatt geht vor dem des Satzes; die Angabe der Ausgabe (auch leer) geht vor beiden
        PlansatzProjekt.Ok(t.P.Ausfuehren("element_aendern", PlansatzProjekt.Par("""{"element":"PB-2","eigenschaften":{"stempel":"Blattstempel"}}""")));
        var gemischt = Lies(Satz($$$"""{"plansatz":"heizung","ordner":"{{{t.Wurzel.Replace("\\", "/")}}}/d","ueberschreiben":true}"""));
        Assert.Contains("Blattstempel", gemischt);
        Assert.Equal(1, Regex.Matches(gemischt, "Satzstempel").Count);
        var keiner = Lies(Satz($$$"""{"plansatz":"heizung","ordner":"{{{t.Wurzel.Replace("\\", "/")}}}/e","stempel":"","ueberschreiben":true}"""));
        Assert.DoesNotContain("stempel", keiner);
    }

    // ================================================================== 6. Schema-Kopf

    [Fact]
    public void SchemaKopf_NurSchemaBlatt_SchmalUntenLinks_NurBolligenMitWeb_OhneMassstab()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768"), ("titel", "Schema Wasser"), ("format", "A1"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-SCHEMA-SAN"), ("lage", new JsonArray(400, 330)))));
        var t = Texte(Pdf(p));
        Assert.Contains("H+K Planungs AG, Bolligen", t);
        Assert.DoesNotContain("H+K Planungs AG, Thun", t);                                 // nur Bolligen
        Assert.Contains("www.hkplanung.ch", t);                                            // Web statt Mail
        Assert.DoesNotContain(t, x => x.StartsWith("Mail:"));
        Assert.DoesNotContain("Massstab:", t);                                             // ohne Massstab
        Assert.DoesNotContain("Rev. Index:", t);
        Assert.Contains("Plannummer", t);
        Assert.Contains("Projektphase", t);
        Assert.Contains(t, x => Regex.IsMatch(x, "^Plan.*nderungen$"));                    // «Planänderungen» (Umlaut als Escape im PDF-Text)

        // kopfart am Blatt übersteuert die Vorgabe aus dem Inhalt
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["kopfart"] = "grundriss" }))));
        var gr = Texte(Pdf(p));
        Assert.Contains("Rev. Index:", gr);
        Assert.Contains("H+K Planungs AG, Thun", gr);
        Ok(p.Ausfuehren("element_aendern", Par(("element", "PB-1"), ("eigenschaften", new JsonObject { ["kopfart"] = null }))));
        Assert.DoesNotContain("Rev. Index:", Texte(Pdf(p)));                               // Vorgabe wieder aus dem Inhalt: Schema-Kopf
    }
    [Fact]
    public void SchemaKopf_PositionLinksGegenGrundrissKopfRechts()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 S01"), ("titel", "Schema"), ("format", "A1"))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 S02"), ("titel", "Grundriss"), ("format", "A1"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-SCHEMA-SAN"), ("lage", new JsonArray(400, 330)))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-2"), ("ansicht", "AN-GR-EG"), ("lage", new JsonArray(400, 330)))));
        double X(string blatt, string wort, Func<Projekt, string, Plangrafik>? g = null)
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(Planausgabe.Pdf(p, blatt, false, g ?? Leer));
            return doc.GetPage(1).GetWords().First(w => w.Text == wort).BoundingBox.Left / doc.GetPage(1).Width;
        }
        Assert.True(X("PB-1", "Plannummer", (pp, a) => Hlks.Kern.Grafik.Plan(pp, a)) < 0.25);   // Schema-Kopf links
        Assert.True(X("PB-2", "Plannummer:") > 0.6);                                             // Grundriss-Kopf rechts
    }

    [Fact]
    public void FreieLage_RechnetMitDerKopfgroesse_SchemaKopfLinks()
    {
        var p = Haus();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 S01"), ("titel", "Schema"), ("format", "A2"))));
        // viele Revisionen: der Kopf wächst; die Ansicht (Mitte des freien Bereichs) steht über dem Kopf, im Blatt
        for (int i = 0; i < 8; i++) Ok(p.Ausfuehren("revision_hinzufuegen", Par(("planblatt", "PB-1"), ("beschreibung", "r" + i))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-SCHEMA-SAN"))));
        var l = p.Modell.DerKlasse("Ansichtsfenster").Single().Geometrie!.Punkte[0];
        var kh = Blattformate.KopfHoehe("A2", HkPlankopf.Art.Schema, 8, false);
        Assert.True(l[1] > Blattformate.Rand + kh);                                        // Mitte über dem Kopf
        Assert.InRange(l[0], 0, 594);
    }

    // ================================================================== 7. Listen auf Planblättern

    static Projekt MitListe(string liste, string? anlage = null, string? geschoss = null, string name = "Raumliste")
    {
        var p = Haus();
        var eig = new JsonObject { ["art"] = "liste", ["liste"] = liste };
        if (anlage is not null) eig["anlage"] = anlage;
        var par = Par(("klasse", "Ansicht"), ("kennzeichen", "AN-L-1"), ("name", name), ("eigenschaften", eig));
        if (geschoss is not null) par["geschoss"] = geschoss;
        Ok(p.Ausfuehren("element_anlegen", par));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 L99"), ("titel", "Listen"), ("format", "A3"))));
        return p;
    }

    [Fact]
    public void Liste_AnsichtPlatzierenNimmtListenAn_UndZeigtSieAlsTabelleImPdf()
    {
        var p = MitListe("raumliste");
        var e = Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-L-1"))));
        Assert.Contains(e.Hinweise, h => h.Contains("AN-L-1") && h.Contains("PB-1"));
        var t = Texte(Pdf(p, grafik: (pp, a) => Hlks.Kern.Grafik.Plan(pp, a)));
        Assert.Contains("Raumliste", t);                                                   // Titel unter der Tabelle
        Assert.Contains("Raum", t);                                                        // Spaltenköpfe
        Assert.Contains("Nutzung", t);
        Assert.Contains("Technik", t);                                                     // Zeilen aus den Räumen des Beispielprojekts
        Assert.Contains("U.01", t);
        Assert.Contains("E.02", t);
        Assert.Contains(t, x => x.StartsWith("Waschk"));
        Assert.Contains("25,00", t);                                                       // Fläche m² mit Dezimalkomma, 2 Stellen
        Assert.DoesNotContain(t, x => Regex.IsMatch(x, @"^1:\d+$"));                       // eine Liste hat keinen Massstab im Titel
        Assert.DoesNotContain(t, x => Regex.IsMatch(x, @"^\d+(\.\d+)? m$"));               // und keinen grafischen Massstab
        Assert.Contains("-", t);                                                           // Massstab im Kopf: «-» (nur Listen)
        Assert.DoesNotContain("1 : xx", t);
    }

    [Fact]
    public void Liste_GeschossFilterWieDieListenansicht()
    {
        var p = MitListe("raumliste", geschoss: "EG");
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-L-1"))));
        var t = Texte(Pdf(p, grafik: (pp, a) => Hlks.Kern.Grafik.Plan(pp, a)));
        Assert.Contains("E.02", t);
        Assert.DoesNotContain("U.01", t);                                                  // nur EG
    }

    [Theory]
    [InlineData("raumliste", "Raumliste")]
    [InlineData("strangliste", "Strangliste")]
    [InlineData("mengen", "Mengen")]
    [InlineData("raumgrenzen", "Raumgrenzen")]
    [InlineData("huellbauteile", "Hüllbauteile")]
    [InlineData("materialliste", "Materialliste")]
    [InlineData("kollisionen", "Kollisionsliste")]
    [InlineData("aussparungsliste", "Aussparungsliste")]
    [InlineData("sanitaer_zusammenstellung", "Sanitär-Zusammenstellung (W3)")]
    public void Liste_AlleListenartenErgebenEineTabelleMitTitelUndLinien(string liste, string titel)
    {
        var p = MitListe(liste, anlage: liste == "sanitaer_zusammenstellung" ? "A-SAN" : null, name: titel);
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-L-1"))));
        var g = Hlks.Kern.Grafik.Plan(p, "AN-L-1");
        Assert.Equal("liste", g.Art);
        Assert.Equal(1, g.Massstab);                                                       // Papier-mm wie ein Schema
        Assert.NotNull(g.Begrenzung);
        var prim = g.Teile.SelectMany(x => x.Primitive).ToList();
        Assert.Contains(prim, x => x.Art == "linie");                                      // Tabellenlinien
        Assert.Contains(prim, x => x.Art == "text" && x.Fett == true);                     // fette Kopfzeile
        var pdf = Pdf(p, grafik: (pp, a) => Hlks.Kern.Grafik.Plan(pp, a));
        Assert.Contains(titel.Substring(0, Math.Min(6, titel.Length)), pdf.Replace("\\374", "ü").Replace("\\344", "ä"));   // Titel steht im PDF
    }

    [Fact]
    public void Liste_SanitaerZusammenstellungHatDieDreiBloeckeMitAbgerundetenZahlen()
    {
        var p = MitListe("sanitaer_zusammenstellung", "A-SAN");
        var d = (JsonObject)Tuer.Abfrage(p, "sanitaer_zusammenstellung", Par(("anlage", "A-SAN")));
        var inhalt = Listentabelle.Fuer(p, p.Modell.Hole("AN-L-1"));
        Assert.Equal("Sanitär-Zusammenstellung (W3)", inhalt.Titel);
        Assert.Equal(["Apparate (LU und DU je Stück, Stück je Geschoss, Summen)", "Trinkwasser", "Entwässerung"], inhalt.Tabellen.Take(3).Select(x => x.Titel).ToArray());
        var apparate = inhalt.Tabellen[0];
        Assert.Equal("Total", apparate.Fuss.Single()[0]);
        Assert.Equal("Apparateart", apparate.Spalten[0].Titel);
        Assert.Contains(inhalt.Tabellen[1].Zeilen, z => z[0] == "Total LU");
        Assert.Contains(inhalt.Tabellen[2].Zeilen, z => z[0] == "ΣDU");
        // die Zahl der Apparate steht wie im Kern
        Assert.Equal(Hlks.Kern.Json.Zahl(d["apparate"])!.Value.ToString("0"), apparate.Fuss.Single()[apparate.Spalten.Count - 4]);
    }

    [Fact]
    public void Liste_UnbekannteListeUndZuVieleZeilenWerdenBegrenzt()
    {
        var p = Haus();
        var e = Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-L-2"), ("eigenschaften", new JsonObject { ["art"] = "liste", ["liste"] = "raumliste" }))));
        var sp = Listentabelle.Zeichnen(new ListeInhalt
        {
            Titel = "Test", Tabellen =
            [
                new ListeTabelle { Spalten = [new("a", "A", false), new("b", "B", true)], Zeilen = Enumerable.Range(1, 100).Select(i => new[] { "z" + i, i.ToString() }).ToList() },
            ],
        });
        var texte = sp.Where(x => x.Art == "text").Select(x => x.Text!).ToList();
        Assert.Contains("z45", texte);
        Assert.DoesNotContain("z46", texte);                                               // höchstens 45 Zeilen
        Assert.Contains(texte, x => x.Contains("55 weitere Zeilen"));                      // Umbruch folgt in Teil B
    }

    [Fact]
    public void Liste_LangeTexteWerdenGekuerztSpaltenbreiteAusDemInhalt()
    {
        var lang = new string('x', 400);
        var sp = Listentabelle.Zeichnen(new ListeInhalt
        {
            Titel = "Test", Tabellen = [new ListeTabelle { Spalten = [new("a", "A", false), new("b", "B", true)], Zeilen = [[lang, "1"], ["kurz", "2"]] }],
        });
        var texte = sp.Where(x => x.Art == "text").Select(x => x.Text!).ToList();
        Assert.Contains(texte, x => x.EndsWith("…") && x.Length < 100);
        Assert.Contains("kurz", texte);
        var breite = sp.Where(x => x.Art == "linie").SelectMany(x => x.Punkte).Max(q => q[0]);
        Assert.InRange(breite, 20, 100);                                                   // eine Spalte höchstens 60 mm plus die schmale zweite
    }

    [Fact]
    public void Liste_AndereAnsichtsartenBleibenAbgelehnt_ListeAufEinemBlattNurEinmal()
    {
        var p = MitListe("raumliste");
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-L-1"))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 L98"), ("titel", "Zweites"))));
        Assert.Contains("PB-1", Nein(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-2"), ("ansicht", "AN-L-1")))).Fehler);
        Assert.Contains("liste", Nein(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-2"), ("ansicht", "AN-3D")))).Vorschlag ?? "");
    }

    [Fact]
    public void Liste_FreieLageUeberDemKopf_FensterWirdNichtUeberlagert()
    {
        var p = MitListe("raumliste");
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", "AN-L-1"))));
        var l = p.Modell.DerKlasse("Ansichtsfenster").Single().Geometrie!.Punkte[0];
        Assert.InRange(l[0], 0, 420);
        Assert.InRange(l[1], 0, 297);
    }
}
