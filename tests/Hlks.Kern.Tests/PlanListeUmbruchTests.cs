using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hlks.Kern.Tests;

/// <summary>Modul 7 Teil B3: Listen auf Planblättern brechen in Blöcken rechts daneben um (Option umbruch am Ansichtsfenster: spalten | aus), Kopfzeile in jedem Block
/// wiederholt, Summenzeile nur am Ende; reicht die Blattbreite nicht, meldet der Befund P28 «Liste passt nicht aufs Blatt» mit Vorschlag. Die lange Raumliste
/// entsteht im Beispielprojekt «MFH Musterweg» (13 Räume) mit zusätzlichen Testräumen. Alles fiktiv.</summary>
public class PlanListeUmbruchTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static BefehlsErgebnis Ok(BefehlsErgebnis e) { Assert.True(e.Angenommen, e.Meldung + " " + e.Fehler + " " + e.Vorschlag); return e; }
    static BefehlsErgebnis Nein(BefehlsErgebnis e) { Assert.False(e.Angenommen); return e; }
    static JsonObject Par(params (string, object?)[] kv) => new(kv.Select(x => KeyValuePair.Create(x.Item1, x.Item2 is null ? null : x.Item2 is JsonNode n ? n : JsonValue.Create(x.Item2))));

    /// <summary>Beispielprojekt mit Projektinformation und <paramref name="zusaetzlich"/> Testräumen im EG (die Raumliste hat dann 13 + n Zeilen), Raumliste AN-L-1,
    /// Planblatt PB-1 im gewählten Format. Die Liste ist noch nicht platziert.</summary>
    static Projekt Haus(int zusaetzlich, string format = "A3", bool hoch = false)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Projektinformation"),
            ("eigenschaften", new JsonObject { ["phase"] = "Bauprojekt", ["zeile1"] = "MFH Musterweg fiktiv", ["zeile2"] = "Musterweg 1, 3000 Bern", ["projektnummer"] = "1-0768" }))));
        for (int i = 1; i <= zusaetzlich; i++)
            Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Raum"), ("kennzeichen", $"T.{i:00}"), ("name", $"Testraum {i}"), ("geschoss", "EG"),
                ("eigenschaften", new JsonObject { ["nummer"] = $"T.{i:00}", ["nutzung"] = "Büro", ["lichteHoehe"] = 2500 }),
                ("geometrie", new JsonObject { ["art"] = "polygon", ["punkte"] = new JsonArray(new JsonArray(0, 0), new JsonArray(3000, 0), new JsonArray(3000, 3000), new JsonArray(0, 3000)) }))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-L-1"), ("name", "Raumliste"),
            ("eigenschaften", new JsonObject { ["art"] = "liste", ["liste"] = "raumliste" }))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "1-0768 L99"), ("titel", "Listen"), ("format", format), ("hochformat", hoch))));
        return p;
    }

    static BefehlsErgebnis Platzieren(Projekt p, string? umbruch = null, double? schrift = null, string ansicht = "AN-L-1") =>
        p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", ansicht), ("umbruch", umbruch), ("schrift", schrift)));

    static List<Primitiv> Prim(Projekt p, string ansicht = "AN-L-1") => Hlks.Kern.Grafik.Plan(p, ansicht).Teile.SelectMany(t => t.Primitive).ToList();
    static List<Primitiv> Texte(IEnumerable<Primitiv> prim) => prim.Where(x => x.Art == "text").ToList();
    static int Anzahl(IEnumerable<Primitiv> prim, string text, bool? fett = null) => Texte(prim).Count(x => x.Text == text && (fett is null || (x.Fett ?? false) == fett));
    static IEnumerable<Befund> P28(Projekt p) => Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == "P28");

    static ListeTabelle Tabelle(int zeilen, string titel = "", params string[][] fuss) => new()
    {
        Titel = titel,
        Spalten = [new("a", "Name", false), new("b", "Wert", true)],
        Zeilen = Enumerable.Range(1, zeilen).Select(i => new[] { "zeile" + i, i.ToString() }).ToList(),
        Fuss = [.. fuss],
    };

    // ================================================================== 1. Umbruch in Blöcken

    [Fact]
    public void Raumliste_93Zeilen_AufA3_ZweiBloeckeKopfWiederholt_NichtsFehlt()
    {
        var p = Haus(80);
        var e = Ok(Platzieren(p));
        Assert.DoesNotContain(e.NeueBefunde, b => b.Code == "P28");                             // die Platzsuche misst die Liste so, wie sie steht
        var prim = Prim(p);
        Assert.Equal(2, Anzahl(prim, "Raum", fett: true));                                       // Kopfzeile in jedem Block
        Assert.Equal(2, Anzahl(prim, "Nutzung", fett: true));
        Assert.Equal(2, Anzahl(prim, "Systeme", fett: true));
        for (int i = 1; i <= 80; i++) Assert.Equal(1, Anzahl(prim, $"Testraum {i}"));            // jede Zeile genau einmal
        Assert.Equal(1, Anzahl(prim, "U.01"));
        Assert.Equal(1, Anzahl(prim, "1.05"));                                                    // 1.OG-Räume sind auch da
        Assert.DoesNotContain(Texte(prim), t => t.Text!.Contains("weitere"));                   // nichts abgeschnitten
        Assert.Equal(1, Texte(prim).Count(t => t.Text!.StartsWith("93 Räume")));               // die Zusammenfassung («Total») steht nur einmal, oben
        // zwei Blöcke nebeneinander: die zweite Kopfzeile steht rechts der ersten, auf gleicher Höhe
        var koepfe = Texte(prim).Where(t => t.Text == "Raum" && t.Fett == true).OrderBy(t => t.Punkte[0][0]).ToList();
        Assert.True(koepfe[1].Punkte[0][0] > koepfe[0].Punkte[0][0] + 150);
        Assert.Equal(koepfe[0].Punkte[0][1], koepfe[1].Punkte[0][1], 3);
        // das Fenster liegt im Rahmen und über dem Plankopf
        var bereich = Planausgabe.ListenBereich(p, p.Modell.Hole("PB-1"));
        var g = Hlks.Kern.Grafik.Plan(p, "AN-L-1");
        Assert.True(g.Begrenzung![2] - g.Begrenzung[0] <= bereich.Breite + 4);
        Assert.True(g.Begrenzung[3] - g.Begrenzung[1] <= bereich.Hoehe + 8);
        Assert.Empty(P28(p));
    }

    [Fact]
    public void Raumliste_PdfZeigtDieKopfzeileZweimal_UndAlleZeilen()
    {
        var p = Haus(80);
        Ok(Platzieren(p));
        var pdf = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        var texte = Regex.Matches(pdf, @"\(((?:[^()\\]|\\.)*)\)").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, texte.Count(t => t == "Nutzung"));
        Assert.Contains("Testraum 80", texte);
        Assert.Contains("Testraum 1", texte);
        Assert.DoesNotContain(texte, t => t.Contains("weitere"));
    }

    [Fact]
    public void KurzeListe_BleibtEineSpalte_KeinUmbruchKeinBefund()
    {
        var p = Haus(0);
        Ok(Platzieren(p));
        var prim = Prim(p);
        Assert.Equal(1, Anzahl(prim, "Raum", fett: true));
        Assert.Empty(P28(p));
        // ohne Umbruch wäre das Bild dasselbe: Zeichnen ohne Bereich liefert die Grafik der alten Fassung
        var alt = Listentabelle.Zeichnen(Listentabelle.Fuer(p, p.Modell.Hole("AN-L-1")));
        Assert.Equal(Json.Text(alt), Json.Text(prim));
    }

    [Fact]
    public void Umbruch_SummenzeileNurAmEnde_ZusammenMitDerLetztenZeile_TitelMitFortsetzung()
    {
        var inhalt = new ListeInhalt { Titel = "Test", Tabellen = [Tabelle(120, "Mengen", new[] { "Total", "7260" })] };
        var lay = Listentabelle.Anordnen(inhalt, new ListeBereich(400, 100));
        Assert.True(lay.Bloecke >= 3);
        Assert.True(lay.Passt);
        var t = Texte(lay.Primitive);
        Assert.Single(t, x => x.Text == "Total");
        Assert.Equal(lay.Bloecke, t.Count(x => x.Text == "Name" && x.Fett == true));            // Kopfzeile in jedem Block
        Assert.Equal(lay.Bloecke - 1, t.Count(x => x.Text == "Mengen (Fortsetzung)"));
        Assert.Equal(1, t.Count(x => x.Text == "Mengen"));
        for (int i = 1; i <= 120; i++) Assert.Single(t, x => x.Text == "zeile" + i);
        // Total steht im letzten Block, in einem Block mit mindestens einer Zeile über sich
        double letzterX = t.Where(x => x.Text == "Name").Max(x => x.Punkte[0][0]);
        var total = t.Single(x => x.Text == "Total");
        Assert.True(total.Punkte[0][0] >= letzterX - 0.01);
        Assert.Contains(t, x => x.Text == "zeile120" && Math.Abs(x.Punkte[0][0] - total.Punkte[0][0]) < 0.01);
        // Alle Blöcke ausser dem letzten sind bis zur Höhe gefüllt, kein Block ist höher als der Bereich
        Assert.True(lay.Hoehe <= 100 + 0.01);
    }

    [Fact]
    public void Umbruch_SanitaerZusammenstellung_TotalNurEinmal_BlockweiseWeiter()
    {
        var p = Haus(0);
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-L-S"), ("name", "Sanitär"),
            ("eigenschaften", new JsonObject { ["art"] = "liste", ["liste"] = "sanitaer_zusammenstellung", ["anlage"] = "A-SAN" }))));
        var inhalt = Listentabelle.Fuer(p, p.Modell.Hole("AN-L-S"));
        var einspaltig = Listentabelle.Anordnen(inhalt, new ListeBereich(2000, 2000));
        var lay = Listentabelle.Anordnen(inhalt, new ListeBereich(2000, 45));                    // sehr niedrig: viele Blöcke
        Assert.True(lay.Bloecke > einspaltig.Bloecke);
        Assert.Equal(1, Anzahl(lay.Primitive, "Total"));                                          // die Totalzeile der Apparate steht einmal
        Assert.Equal(Anzahl(einspaltig.Primitive, "Total LU"), Anzahl(lay.Primitive, "Total LU"));
        Assert.Equal(Anzahl(einspaltig.Primitive, "ΣDU", fett: false), Anzahl(lay.Primitive, "ΣDU", fett: false));
        Assert.True(Anzahl(lay.Primitive, "Grösse", fett: true) >= 2);                            // Kopf der Tabellen Trinkwasser und Entwässerung steht in jedem ihrer Stücke
    }

    [Fact]
    public void Umbruch_MehrereTabellenFliessenDurchDieBloecke_EineTabelleBeginntNieMitNurKopfzeile()
    {
        var inhalt = new ListeInhalt { Titel = "Mengen", Tabellen = [Tabelle(20, "Leitungen"), Tabelle(20, "Bauteile", new[] { "Total", "1" })] };
        var lay = Listentabelle.Anordnen(inhalt, new ListeBereich(400, 60));
        Assert.True(lay.Passt);
        var t = Texte(lay.Primitive);
        for (int i = 1; i <= 20; i++) Assert.Equal(2, t.Count(x => x.Text == "zeile" + i));
        Assert.Single(t, x => x.Text == "Total");
        // jede Kopfzeile hat eine Zeile unter sich: kein Block endet mit einer Kopfzeile
        foreach (var kopf in t.Where(x => x.Text == "Name" && x.Fett == true))
            Assert.Contains(t, x => x.Text!.StartsWith("zeile") && Math.Abs(x.Punkte[0][0] - kopf.Punkte[0][0]) < 0.01 && Math.Abs(x.Punkte[0][1] - (kopf.Punkte[0][1] - 3.4)) < 0.01);
    }

    [Fact]
    public void Umbruch_WinzigerBereich_ZeigtMindestensEineZeileProBlock_Endet_UndPasstNicht()
    {
        var inhalt = new ListeInhalt { Titel = "Test", Tabellen = [Tabelle(6, "T", new[] { "Total", "21" }), new ListeTabelle { Titel = "Leer", Spalten = [new("a", "A", false)], Fuss = [new[] { "Summe" }] }] };
        var lay = Listentabelle.Anordnen(inhalt, new ListeBereich(2000, 3));
        Assert.False(lay.Passt);                                                                 // zu hoch, aber nichts bleibt aus
        Assert.True(lay.Hoehe > 3);
        var t = Texte(lay.Primitive);
        for (int i = 1; i <= 6; i++) Assert.Single(t, x => x.Text == "zeile" + i);
        Assert.Single(t, x => x.Text == "Total");
        Assert.Single(t, x => x.Text == "Summe");                                                 // Tabelle ohne Zeilen, nur Summe
    }

    [Fact]
    public void Schrift_KleinereSchriftBrauchtWenigerPlatz_UndSetztDieSchriftgroesse()
    {
        var inhalt = new ListeInhalt { Titel = "Test", Tabellen = [Tabelle(40, "T")] };
        var gross = Listentabelle.Anordnen(inhalt, null, Listentabelle.UmbruchSpalten, 2.2);
        var klein = Listentabelle.Anordnen(inhalt, null, Listentabelle.UmbruchSpalten, 1.6);
        Assert.True(klein.Hoehe < gross.Hoehe * 0.8);
        Assert.True(klein.Breite < gross.Breite);
        Assert.All(Texte(klein.Primitive).Where(x => x.Text!.StartsWith("zeile")), x => Assert.Equal(1.6, x.Groesse!.Value));
        Assert.All(Texte(gross.Primitive).Where(x => x.Text!.StartsWith("zeile")), x => Assert.Equal(2.2, x.Groesse!.Value));
        // ausserhalb der Grenzen wird begrenzt, nicht abgelehnt
        Assert.Equal(Listentabelle.SchriftMax, Listentabelle.SchriftWert(9));
        Assert.Equal(Listentabelle.SchriftMin, Listentabelle.SchriftWert(0.2));
        Assert.Equal(Listentabelle.SchriftVorgabe, Listentabelle.SchriftWert(null));
        Assert.Equal(Listentabelle.SchriftVorgabe, Listentabelle.SchriftWert(-1));
    }

    // ================================================================== 2. Befund P28

    [Fact]
    public void ZuBreit_Befund_P28_Warnung_MitVorschlag_UndEineMarkierungSagtWasFehlt()
    {
        var p = Haus(90);                                                                         // 103 Zeilen: 3 Blöcke, aber nur 2 passen auf die Breite von A3
        var e = Ok(Platzieren(p));
        var b = Assert.Single(e.NeueBefunde, x => x.Code == "P28");                               // der Befehl meldet den neuen Befund
        Assert.Equal("warnung", b.Schwere);
        Assert.Equal("AF-1", b.Element);
        Assert.Contains("Liste «Raumliste» (AN-L-1) passt nicht aufs Blatt PB-1", b.Text);
        Assert.Contains("3 Blöcke", b.Text);
        Assert.Contains("nicht gezeichnet", b.Text);
        Assert.Contains("Schrift auf", b.Vorschlag);                                              // kleinere Schrift reicht
        Assert.Equal(b.Schluessel, Assert.Single(P28(p)).Schluessel);

        // nichts wird ohne Befund abgeschnitten: gezeichnete Zeilen + die in der Markierung genannten ergeben alle
        var prim = Prim(p);
        var marker = Texte(prim).Single(t => t.Text!.StartsWith("… ") && t.Text.Contains("Liste passt nicht aufs Blatt"));
        int fehlen = int.Parse(Regex.Match(marker.Text!, @"… (\d+) weitere").Groups[1].Value);
        int gezeigt = Texte(prim).Count(t => t.Text!.StartsWith("Testraum ")) + new[] { "U.01", "U.02", "U.03", "E.01", "E.02", "E.03", "E.04", "E.05", "1.01", "1.02", "1.03", "1.04", "1.05" }.Count(k => Anzahl(prim, k) == 1);
        Assert.Equal(103, gezeigt + fehlen);
        Assert.Equal(2, Anzahl(prim, "Raum", fett: true));                                       // zwei Blöcke gezeichnet
        Assert.Contains($"{fehlen}", b.Text);

        // der Vorschlag hilft wirklich: die genannte Schrift setzen, danach ist der Befund weg und die Liste ganz da
        var schrift = double.Parse(Regex.Match(b.Vorschlag!, @"Schrift auf ([\d.]+) mm").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(schrift, 1.5, 2.1);
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AF-1"), ("eigenschaften", new JsonObject { ["schrift"] = schrift }))));
        Assert.Empty(P28(p));
        var prim2 = Prim(p);
        Assert.DoesNotContain(Texte(prim2), t => t.Text!.Contains("weitere"));
        Assert.Equal(1, Anzahl(prim2, "Testraum 90"));
    }

    [Fact]
    public void ZuBreit_AufA4Hochformat_AuchMitKleinsterSchriftNicht_VorschlagFolgeblatt()
    {
        var p = Haus(90, "A4", hoch: true);
        Ok(Platzieren(p));
        var b = Assert.Single(P28(p));
        Assert.Contains("Folgeblatt", b.Vorschlag);
        Assert.Contains("grösseres Blattformat", b.Vorschlag);
        Assert.DoesNotContain("Schrift auf", b.Vorschlag);
        // Ein Folgeblatt hilft: die Liste mit Geschossfilter (nur EG) auf einem zweiten Blatt
        Assert.Contains(P28(p), x => x.Text.Contains("Blöcke"));
    }

    [Fact]
    public void Umbruch_Aus_BisherigesVerhalten_45Zeilen_UndBefundMitVorschlagUmbruchEinschalten()
    {
        var p = Haus(80);
        var e = Ok(Platzieren(p, umbruch: "aus"));
        Assert.Equal("aus", p.Modell.DerKlasse("Ansichtsfenster").Single().Text("umbruch"));
        var b = Assert.Single(e.NeueBefunde, x => x.Code == "P28");
        Assert.Contains("höchstens 45 Zeilen", b.Text);
        Assert.Contains("Umbruch einschalten", b.Vorschlag);
        var prim = Prim(p);
        Assert.Equal(1, Anzahl(prim, "Raum", fett: true));                                       // eine Spalte
        Assert.Contains(Texte(prim), t => t.Text == "… 48 weitere Zeilen (Umbruch aus)");        // 93 - 45
        // identisch mit der Fassung ohne Bereich (bisheriges Verhalten), nur der Hinweis nennt den Grund
        var alt = Listentabelle.Zeichnen(Listentabelle.Fuer(p, p.Modell.Hole("AN-L-1")));
        Assert.Equal(alt.Count, prim.Count);
        Assert.Equal(Json.Text(alt.Where(x => x.Art != "text")), Json.Text(prim.Where(x => x.Art != "text")));

        // Umbruch einschalten behebt es
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AF-1"), ("eigenschaften", new JsonObject { ["umbruch"] = "spalten" }))));
        Assert.Empty(P28(p));
        Assert.Equal(2, Anzahl(Prim(p), "Raum", fett: true));
    }

    [Fact]
    public void Umbruch_Aus_ListeHoeherAlsDerBereichOhneZeilengrenze_BefundNenntDieHoehe()
    {
        var inhalt = new ListeInhalt { Titel = "Test", Tabellen = [Tabelle(40, "T")] };
        var lay = Listentabelle.Anordnen(inhalt, new ListeBereich(300, 80), Listentabelle.UmbruchAus);
        Assert.Equal(1, lay.Bloecke);
        Assert.Equal(0, lay.Verborgen);
        Assert.True(lay.Hoehe > 80);
        Assert.False(lay.Passt);
        Assert.True(Listentabelle.Anordnen(inhalt, new ListeBereich(300, 80)).Passt);             // mit Umbruch passt sie
    }

    [Fact]
    public void ListeOhneBlatt_KeinUmbruch_45Zeilen_UndKeinBefund()
    {
        var p = Haus(80);
        var prim = Prim(p);                                                                       // nicht platziert
        Assert.Equal(1, Anzahl(prim, "Raum", fett: true));
        Assert.Contains(Texte(prim), t => t.Text == "… 48 weitere Zeilen (höchstens 45 Zeilen)");
        Assert.Empty(P28(p));
    }

    [Fact]
    public void Befund_NichtBerechenbareListeVerhindertKeinenBefehl()
    {
        var p = Haus(0);
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-L-S"), ("name", "Sanitär"),
            ("eigenschaften", new JsonObject { ["art"] = "liste", ["liste"] = "sanitaer_zusammenstellung", ["anlage"] = "A-GIBTS-NICHT" }))));
        Ok(Platzieren(p, ansicht: "AN-L-S"));
        Assert.Empty(P28(p));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AN-L-S"), ("name", "Sanitär neu"))));
    }

    // ================================================================== 3. Option am Ansichtsfenster

    [Fact]
    public void AnsichtPlatzieren_UmbruchUndSchriftWerdenAmFensterGespeichert_UngueltigesWirdAbgelehnt()
    {
        var p = Haus(0);
        Ok(Platzieren(p, umbruch: "spalten", schrift: 1.8));
        var af = p.Modell.DerKlasse("Ansichtsfenster").Single();
        Assert.Equal("spalten", af.Text("umbruch"));
        Assert.Equal(1.8, af.Zahl("schrift"));
        Assert.All(Texte(Prim(p)).Where(t => t.Text == "Technik"), t => Assert.Equal(1.8, t.Groesse!.Value));

        var q = Haus(0);
        Assert.Contains("Umbruch «quer»", Nein(Platzieren(q, umbruch: "quer")).Fehler);
        Assert.Contains("Schriftgrösse", Nein(Platzieren(q, schrift: 9)).Fehler);
        Assert.Contains("Schriftgrösse", Nein(Platzieren(q, schrift: 0.5)).Fehler);
        Assert.Empty(q.Modell.DerKlasse("Ansichtsfenster"));
        Ok(Platzieren(q));                                                                         // ohne Angabe: keine Eigenschaft, es gilt die Vorgabe
        Assert.Null(q.Modell.DerKlasse("Ansichtsfenster").Single().Eigenschaft("umbruch"));
        Assert.Null(q.Modell.DerKlasse("Ansichtsfenster").Single().Eigenschaft("schrift"));
    }

    [Fact]
    public void AnsichtPlatzieren_UmbruchGiltNurFuerListen()
    {
        var p = Haus(0);
        var n = Nein(Platzieren(p, umbruch: "aus", ansicht: "AN-GR-EG"));
        Assert.Contains("nur für Listen", n.Fehler);
        Ok(Platzieren(p, ansicht: "AN-GR-EG"));
    }

    [Fact]
    public void Eigenschaften_UmbruchIstEineAuswahl_SchriftEineZahl_ImKatalog()
    {
        var d = Kat.Klasse("Ansichtsfenster").Eigenschaften;
        Assert.Equal("auswahl", d.Single(x => x.Name == "umbruch").Typ);
        Assert.Equal(["spalten", "aus"], d.Single(x => x.Name == "umbruch").Auswahl);
        Assert.Equal("zahl", d.Single(x => x.Name == "schrift").Typ);
        var p = Haus(0);
        Ok(Platzieren(p));
        Nein(p.Ausfuehren("element_aendern", Par(("element", "AF-1"), ("eigenschaften", new JsonObject { ["umbruch"] = "quer" }))));
        Ok(p.Ausfuehren("element_aendern", Par(("element", "AF-1"), ("eigenschaften", new JsonObject { ["umbruch"] = "aus", ["schrift"] = 2.0 }))));
        Assert.Equal("aus", p.Modell.Hole("AF-1").Text("umbruch"));
        // Rückgängig wie alle Befehle
        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Hole("AF-1").Eigenschaft("umbruch"));
    }

    [Fact]
    public void FreieLage_DasFensterDerUmbrochenenListeLiegtImRahmenUeberDemKopf()
    {
        var p = Haus(80);
        Ok(Platzieren(p));
        var l = p.Modell.DerKlasse("Ansichtsfenster").Single().Geometrie!.Punkte[0];
        var g = Hlks.Kern.Grafik.Plan(p, "AN-L-1");
        double w = g.Begrenzung![2] - g.Begrenzung[0] + 2, h = g.Begrenzung[3] - g.Begrenzung[1] + 2;   // wie Planausgabe.Gebiet (1 mm Zugabe je Seite)
        var blatt = p.Modell.Hole("PB-1");
        var km = Planausgabe.KopfMasse(p, blatt);
        double kh = Blattformate.KopfHoehe("A3", km.Art, km.Zeilen, km.Titelband);
        Assert.True(l[0] - w / 2 >= Blattformate.Rand - 0.01, "links im Rahmen");
        Assert.True(l[0] + w / 2 <= 420 - Blattformate.Rand + 0.01, "rechts im Rahmen");
        Assert.True(l[1] + h / 2 <= 297 - Blattformate.Rand + 0.01, "oben im Rahmen");
        Assert.True(l[1] - h / 2 >= Blattformate.Rand + kh + Blattformate.TitelHoehe - 0.01, "über Plankopf und Titel");
    }

    [Fact]
    public void Pdf_ZuBreiteListe_ZeichnetMarkierungUndBleibtImRahmen()
    {
        var p = Haus(90);
        Ok(Platzieren(p));
        var pdf = Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.Contains("Liste passt nicht aufs Blatt", pdf);
        Assert.Equal(2, Regex.Matches(pdf, @"\(Nutzung\)").Count);
    }
}
