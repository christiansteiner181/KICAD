using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.PlansatzProjekt;

namespace Hlks.Kern.Tests;

/// <summary>Serien-PDF und Plansätze (entwurf/serien-pdf.md): Gewerk-Vorschlag, automatische Sätze je Gewerk, Auswahl und Reihenfolge bleiben,
/// Sammel-PDF mit Seiten, Formaten und Lesezeichen, Einzel-PDFs mit H+K-Namen, Ordner-Vorschlag, nicht überschreiben, Trockenlauf und Rückgängig.</summary>
public class PlansatzTests
{
    // ------------------------------------------------------------------ Plannummern und Gewerk-Vorschlag

    [Theory]
    [InlineData("1-0768 H01", null, "1-0768", "H01", 'H')]
    [InlineData("1-0768-S01", null, "1-0768", "S01", 'S')]
    [InlineData("1-0768_L12", null, "1-0768", "L12", 'L')]
    [InlineData("1-0768.K3", null, "1-0768", "K3", 'K')]
    [InlineData("H01", null, "", "H01", 'H')]
    [InlineData("h01", "1-0768", "1-0768", "h01", 'H')]
    [InlineData("1-0768 S01", "1-0768", "1-0768", "S01", 'S')]
    [InlineData("0000-101", "0000", "0000", "101", null)]
    [InlineData("2317-101", null, "", "2317-101", null)]
    [InlineData("PB-1", null, "", "PB-1", null)]
    [InlineData("1-0768 E01", null, "1-0768", "E01", 'E')]   // E wird erkannt, aber kein Gewerk zugeordnet (Spezifikation: nur H, L, S, K)
    [InlineData("1-0768 S01.1", null, "1-0768", "S01.1", 'S')]
    [InlineData("", "1-0768", "1-0768", "", null)]
    public void Plannummer_Teilen_UndFachbuchstabe(string nummer, string? projekt, string pn, string rest, char? fach)
    {
        var (p, r) = Plannummern.Teilen(nummer, projekt);
        Assert.Equal(pn, p);
        Assert.Equal(rest, r);
        Assert.Equal(fach, Plannummern.Fachbuchstabe(nummer, projekt));
    }

    [Fact]
    public void Plannummer_NatuerlicheReihenfolge()
    {
        var l = new[] { "1-0768 S10", "1-0768 S2", "1-0768 H01", "1-0768 S01", "1-0768 L01" };
        Assert.Equal(["1-0768 H01", "1-0768 L01", "1-0768 S01", "1-0768 S2", "1-0768 S10"], l.OrderBy(x => x, Comparer<string>.Create(Plannummern.Vergleichen)).ToArray());
        Assert.True(Plannummern.Vergleichen("h1", "H2") < 0);
    }

    [Fact]
    public void Gewerke_StimmenMitDemKatalogUeberein()
    {
        var def = Kat.Klasse("Planblatt").Eigenschaften.First(e => e.Name == "gewerk");
        Assert.Equal(def.Auswahl!, PlanGewerk.Alle.Select(g => g.Schluessel).ToList());
        Assert.Equal("Sanitär", PlanGewerk.Name("sanitaer"));
        Assert.Throws<HlksFehler>(() => PlanGewerk.Hole("gas"));
        Assert.Contains("Plansatz", Kat.Klassen.Keys);
    }

    [Fact]
    public void GewerkVorschlag_NachPlannummer_Ansichten_Allgemein_UndGesetztGewinnt()
    {
        using var t = new PlansatzProjekt(false);
        t.Blatt("1-0768 H05", "Nummer H", "A3", "EG", ["sanitaer"]);              // Nummer vor Ansichten
        t.Blatt("1-0768-S06", "Nummer S mit Bindestrich", "A3", "EG", ["heizung"]);
        t.Blatt("K07", "Nummer ohne Projektnummer", "A3", "EG", []);
        t.Blatt("A-100", "Ansicht Lüftung", "A3", "EG", ["lueftung", "architektur"]);   // architektur zählt nicht
        t.Blatt("A-101", "Zwei Gewerke", "A3", "UG", ["sanitaer", "heizung"]);
        t.Blatt("A-102", "Ohne alles", "A3", "1.OG", [], mitAnsicht: false);
        t.Blatt("1-0768 E01", "Elektro hat keinen Fachbuchstaben", "A3", "1.OG", [], mitAnsicht: false);
        var m = t.P.Modell;
        BlattGewerk G(string nr) => Plaene.GewerkVon(m, Planausgabe.Blatt(m, nr), "1-0768");
        Assert.Equal(new BlattGewerk("heizung", false, "Fachbuchstabe H der Plannummer"), G("1-0768 H05"));
        Assert.Equal("sanitaer", G("1-0768-S06").Gewerk);
        Assert.Equal("koordination", G("K07").Gewerk);   // Fach K = Koordination (Entscheid Christian 20.10.2026)
        Assert.Equal("lueftung", G("A-100").Gewerk);
        Assert.Contains("Ansichten", G("A-100").Quelle);
        Assert.Equal("koordination", G("A-101").Gewerk);
        Assert.Contains("Sanitär", G("A-101").Quelle);
        Assert.Equal("allgemein", G("A-102").Gewerk);
        Assert.Equal("allgemein", G("1-0768 E01").Gewerk);
        Assert.All(new[] { "1-0768 H05", "K07", "A-100", "A-101", "A-102" }, n => Assert.False(G(n).Gesetzt));

        // von Hand gesetzt gewinnt und trägt die Herkunft «gesetzt»
        Ok(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 H05"],"gewerk":"elektro"}""")));
        Assert.Equal(new BlattGewerk("elektro", true, "am Planblatt gesetzt"), G("1-0768 H05"));
        // ohne Projektinformation gilt dasselbe Muster
        Assert.Equal('S', Plannummern.Fachbuchstabe("1-0768-S06", null));
    }

    // ------------------------------------------------------------------ Abfrage plansaetze: automatische Sätze

    [Fact]
    public void Plansaetze_AutomatischJeGewerkAngeboten_NachPlannummer_NochNichtAngelegt()
    {
        using var t = new PlansatzProjekt();
        var a = t.Saetze("""{"datum":"2026-10-19","anlass":"Revisionspläne"}""");
        var saetze = ((JsonArray)a["plansaetze"]!).Select(s => s!.AsObject()).ToList();
        Assert.Equal(["heizung", "lueftung", "sanitaer"], saetze.Select(s => s["gewerk"]!.GetValue<string>()).ToArray());   // Reihenfolge des Katalogs
        Assert.All(saetze, s => { Assert.Null(s["plansatz"]); Assert.False(s["angelegt"]!.GetValue<bool>()); Assert.True(s["automatisch"]!.GetValue<bool>()); });
        Assert.Equal(["auto:heizung", "auto:lueftung", "auto:sanitaer"], saetze.Select(s => s["schluessel"]!.GetValue<string>()).ToArray());
        Assert.Equal(["H01", "H02"], Nummern(saetze[0]));
        Assert.Equal(["L01"], Nummern(saetze[1]));
        Assert.Equal(["S01", "S02"], Nummern(saetze[2]));   // angelegt als S02, S01 – sortiert nach Plannummer
        Assert.Equal("Plansatz Sanitär", saetze[2]["name"]!.GetValue<string>());
        Assert.Empty(t.P.Modell.DerKlasse("Plansatz"));     // die Abfrage legt nichts an

        var s2 = saetze[2];
        var s01 = ((JsonArray)s2["blaetter"]!)[0]!.AsObject();
        Assert.Equal("1-0768 S01", s01["nummer"]!.GetValue<string>());
        Assert.Equal("Sanitär 1.Untergeschoss", s01["titel"]!.GetValue<string>());
        Assert.Equal("A1", s01["format"]!.GetValue<string>());
        Assert.Equal("vorschlag", s01["gewerkHerkunft"]!.GetValue<string>());
        Assert.Contains("Fachbuchstabe S", s01["gewerkQuelle"]!.GetValue<string>());
        Assert.True(s01["dabei"]!.GetValue<bool>());
        Assert.Equal("1-0768-S01-Sanitär 1.Untergeschoss-.pdf", s01["einzelDatei"]!.GetValue<string>());
        Assert.Equal("S01 Sanitär 1.Untergeschoss", s01["lesezeichen"]!.GetValue<string>());
        Assert.Equal("1-0768_Plansatz Sanitär_2026-10-19.pdf", s2["vorschlag"]!["sammelDatei"]!.GetValue<string>());
        Assert.Equal("1-0768", a["projektnummer"]!.GetValue<string>());
    }

    [Fact]
    public void Plansaetze_OrdnerVorschlag_AusDemObjektordnerUndAnlass()
    {
        using var t = new PlansatzProjekt();
        var s = t.Satz("auto:sanitaer", "Revisionspläne");
        var soll = Path.Combine(t.Objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026_Revisionspläne", "Sanitär");
        Assert.Equal(soll, s["vorschlag"]!["ordner"]!.GetValue<string>());
        Assert.Equal(t.Objekt, ((JsonObject)Tuer.Abfrage(t.P, "plansaetze", Par("{}")))["objektordner"]!.GetValue<string>());
        Assert.Equal(Path.Combine(t.Objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026", "Heizung"), t.Satz("heizung")["vorschlag"]!["ordner"]!.GetValue<string>());   // ohne Anlass nur das Datum
        Assert.Equal(Path.Combine(t.Objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026_Rev", "Lüftung"), t.Satz("Lüftung", "Rev")["vorschlag"]!["ordner"]!.GetValue<string>());
    }

    [Fact]
    public void OrdnerVorschlag_LiegtDasProjektAnders_ProjektordnerAusgabe()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "hlks_p1_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var p = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(Path.Combine(wurzel, "irgendwo")));
            Ok(p.Ausfuehren("planblatt_anlegen", Par("""{"nummer":"S01","titel":"Test"}""")));
            var a = (JsonObject)Tuer.Abfrage(p, "plansaetze", Par("""{"datum":"2026-10-19","anlass":"Test"}"""));
            Assert.Null(a["objektordner"]);
            Assert.Equal(Path.Combine(wurzel, "irgendwo", "ausgabe", "19.10.2026_Test", "Sanitär"), a["plansaetze"]![0]!["vorschlag"]!["ordner"]!.GetValue<string>());
        }
        finally { try { Directory.Delete(wurzel, true); } catch (IOException) { } }
    }

    [Fact]
    public void Plansaetze_OhnePlanblaetter_KeineSaetzeMitHinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var a = (JsonObject)Tuer.Abfrage(p, "plansaetze", Par("{}"));
        Assert.Empty((JsonArray)a["plansaetze"]!);
        Assert.Contains("planblatt_anlegen", a["ohneBlaetter"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "plansatz_pdf", Par("""{"plansatz":"sanitaer"}""")));
    }

    // ------------------------------------------------------------------ Befehle: plansatz_setzen, planblatt_gewerk_setzen

    [Fact]
    public void PlansatzSetzen_LegtAutomatischenSatzAn_TrockenlaufRueckgaengigUndNachfuehren()
    {
        using var t = new PlansatzProjekt();
        var vorher = t.P.Modell.Elemente.Count;
        var trocken = t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:sanitaer"}"""), trockenlauf: true);
        Assert.True(trocken.Angenommen, trocken.Fehler);
        Assert.Contains(trocken.Aenderungen, a => a.Klasse == "Plansatz" && a.Art == "angelegt");
        Assert.Equal(vorher, t.P.Modell.Elemente.Count);   // nichts gespeichert

        var e = Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:sanitaer"}""")));
        Assert.Contains(e.Hinweise, h => h.Contains("Plansatz PS-1") && h.Contains("angelegt") && h.Contains("2 Blätter"));
        var ps = t.P.Modell.Hole("PS-1");
        Assert.Equal("Plansatz Sanitär", ps.Text("name"));
        Assert.Equal("Plansatz Sanitär", ps.Name);
        Assert.Equal("sanitaer", ps.Text("gewerk"));
        Assert.True(Plaene.AlsBool(ps, "automatisch"));
        Assert.True(t.Satz("PS-1")["angelegt"]!.GetValue<bool>());
        Assert.Equal(["auto:heizung", "auto:lueftung", "PS-1"], ((JsonArray)t.Saetze()["plansaetze"]!).Select(s => s!["schluessel"]!.GetValue<string>()).ToArray());   // angelegt ersetzt das Angebot

        // Rückgängig: wieder nur angeboten
        Assert.True(t.P.Rueckgaengig().Angenommen);
        Assert.Empty(t.P.Modell.DerKlasse("Plansatz"));
        Assert.Contains("auto:sanitaer", ((JsonArray)t.Saetze()["plansaetze"]!).Select(s => s!["schluessel"]!.GetValue<string>()));

        // ein zweiter Satz mit demselben Namen wird abgelehnt
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:sanitaer"}""")));
        var doppelt = t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"Plansatz Sanitär"}"""));
        Assert.False(doppelt.Angenommen);
    }

    [Fact]
    public void PlansatzSetzen_AuswahlBleibt_NeueBlaetterKommenDazu_MoeglicheReihenfolgeBleibt()
    {
        using var t = new PlansatzProjekt();
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:sanitaer","blaetter":["1-0768 S02","1-0768 S01"],"abgewaehlt":["1-0768 S02"]}""")));
        var s = t.Satz("PS-1");
        Assert.Equal(["S02", "S01"], Nummern(s));              // von Hand verschoben: bleibt
        Assert.Equal(["S01"], Nummern(s, nurDabei: true));     // S02 abgewählt, bleibt gespeichert
        Assert.Equal(["PB-1", "PB-4"], ((JsonArray)t.P.Modell.Hole("PS-1").Eigenschaft("blaetter")!.Inhalt!).Select(x => x!.GetValue<string>()).Order().ToArray());

        // ein neues Blatt S03 kommt von selbst dazu, an seiner Stelle nach Plannummer – nach S01 (S02 steht schon davor)
        t.Blatt("1-0768 S03", "Sanitär 2.Obergeschoss", "A3", "1.OG", ["sanitaer"], mitAnsicht: false);
        s = t.Satz("PS-1");
        Assert.Equal(["S02", "S01", "S03"], Nummern(s));
        Assert.True(((JsonArray)s["blaetter"]!)[2]!["dabei"]!.GetValue<bool>());   // neu = dabei (nicht abgewählt)

        // ein Blatt, das das Gewerk wechselt, verlässt den Satz; ein gelöschtes ebenfalls
        Ok(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S03"],"gewerk":"koordination"}""")));
        Assert.Equal(["S02", "S01"], Nummern(t.Satz("PS-1")));
        Assert.Equal(["S03"], Nummern(t.Satz("auto:koordination")));
        t.Blatt("1-0768 S04", "Wird gelöscht", "A3", "EG", ["sanitaer"], mitAnsicht: false);
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","blaetter":["1-0768 S02","1-0768 S01","1-0768 S04"]}""")));
        Assert.Equal(["S02", "S01", "S04"], Nummern(t.Satz("PS-1")));
        Ok(t.P.Ausfuehren("element_loeschen", Par("""{"element":"1-0768 S04"}""".Replace("1-0768 S04", t.P.Modell.DerKlasse("Planblatt").First(b => b.Text("nummer") == "1-0768 S04").Kennzeichen))));
        Assert.Equal(["S02", "S01"], Nummern(t.Satz("PS-1")));
    }

    [Fact]
    public void PlansatzSetzen_Fehler_UndHandsatz()
    {
        using var t = new PlansatzProjekt();
        // automatisch ohne Gewerk
        var a = t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"X","automatisch":true}"""));
        Assert.False(a.Angenommen);
        Assert.Contains("Gewerk", a.Fehler);
        // unbekanntes Gewerk, unbekanntes Blatt, Blatt eines anderen Gewerks im automatischen Satz
        Assert.Contains("gas", t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"X","gewerk":"gas"}""")).Fehler);
        Assert.False(t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"X","blaetter":["gibtsnicht"]}""")).Angenommen);
        var fremd = t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:sanitaer","blaetter":["1-0768 H01"]}"""));
        Assert.False(fremd.Angenommen);
        Assert.Contains("planblatt_gewerk_setzen", fremd.Vorschlag);
        Assert.False(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"gibtsnicht","name":"Y"}""")).Angenommen);
        Assert.False(t.P.Ausfuehren("plansatz_setzen", Par("""{"ausgabe":{"unsinn":1},"name":"Y"}""")).Angenommen);
        Assert.False(t.P.Ausfuehren("plansatz_setzen", Par("{}")).Angenommen);   // Name fehlt
        Assert.Empty(t.P.Modell.DerKlasse("Plansatz"));

        // Satz von Hand: nur die genannten Blätter, beliebige Gewerke, Reihenfolge wie genannt
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"Alles für den Bauherrn","blaetter":["1-0768 L01","1-0768 H01","1-0768 S01"]}""")));
        var s = t.Satz("Alles für den Bauherrn");
        Assert.False(s["automatisch"]!.GetValue<bool>());
        Assert.Equal(["L01", "H01", "S01"], Nummern(s));
        Assert.Equal(["H02", "S02"], ((JsonArray)s["weitere"]!).Select(b => Plannummern.Teilen(b!["nummer"]!.GetValue<string>(), "1-0768").Nummer).Order().ToArray());
        Assert.Equal("Alles für den Bauherrn", s["vorschlag"]!["ordner"]!.GetValue<string>().Split(Path.DirectorySeparatorChar)[^1]);   // ohne Gewerk der Name
        // ändern: Name, Blatt dazu
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","name":"Bauherr","blaetter":["1-0768 L01","1-0768 H01","1-0768 S01","1-0768 S02"]}""")));
        Assert.Equal(["L01", "H01", "S01", "S02"], Nummern(t.Satz("Bauherr")));
        // ohne Änderung: keine Änderung
        var gleich = t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","name":"Bauherr"}"""));
        Assert.True(gleich.Angenommen);
        Assert.Empty(gleich.Aenderungen);
    }

    [Fact]
    public void PlansatzSetzen_AusgabeFelderWerdenZusammengefuehrt()
    {
        using var t = new PlansatzProjekt();
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"auto:heizung","ausgabe":{"anlass":"Revisionspläne","einzeln":true}}""")));
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","ausgabe":{"ordner":"D:\\Test"}}""")));
        var o = (JsonObject)t.P.Modell.Hole("PS-1").Eigenschaft("ausgabe")!.Inhalt!;
        Assert.Equal("Revisionspläne", o["anlass"]!.GetValue<string>());
        Assert.True(o["einzeln"]!.GetValue<bool>());
        Assert.Equal("D:\\Test", o["ordner"]!.GetValue<string>());
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","ausgabe":{"ordner":null}}""")));
        Assert.Null(((JsonObject)t.P.Modell.Hole("PS-1").Eigenschaft("ausgabe")!.Inhalt!)["ordner"]);
        Assert.False(t.P.Ausfuehren("plansatz_setzen", Par("""{"plansatz":"PS-1","ausgabe":{"einzeln":"ja"}}""")).Angenommen);
    }

    [Fact]
    public void PlanblattGewerkSetzen_BestaetigenUmstellenZurueck_TrockenlaufUndRueckgaengig()
    {
        using var t = new PlansatzProjekt();
        var m = t.P.Modell;
        // Vorschlag bestätigen
        var e = Ok(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S01"],"gewerk":"sanitaer"}""")));
        Assert.Contains(e.Hinweise, h => h.Contains("Vorschlag bestätigt"));
        Assert.Equal("sanitaer", Planausgabe.Blatt(m, "1-0768 S01").Text("gewerk"));
        Assert.Equal("gesetzt", t.Satz("auto:sanitaer")["blaetter"]![0]!["gewerkHerkunft"]!.GetValue<string>());
        // umstellen (Trockenlauf ändert nichts)
        var trocken = t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S01","1-0768 S02"],"gewerk":"Heizung"}"""), trockenlauf: true);
        Assert.True(trocken.Angenommen);
        Assert.Equal(["S01", "S02"], Nummern(t.Satz("auto:sanitaer")));
        Ok(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S01","1-0768 S02"],"gewerk":"Heizung"}""")));
        Assert.Equal(["H01", "H02", "S01", "S02"], Nummern(t.Satz("auto:heizung")));
        Assert.Throws<HlksFehler>(() => t.Satz("auto:sanitaer"));   // kein Blatt mehr im Gewerk: kein Satz
        // Rückgängig
        Assert.True(t.P.Rueckgaengig().Angenommen);
        Assert.Equal(["S01", "S02"], Nummern(t.Satz("auto:sanitaer")));
        // Angabe zurücknehmen: wieder Vorschlag
        var z = Ok(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S01"],"gewerk":"vorschlag"}""")));
        Assert.Null(Planausgabe.Blatt(m, "1-0768 S01").Text("gewerk"));
        Assert.Contains(z.Hinweise, h => h.Contains("wieder Vorschlag"));
        // Fehler
        Assert.False(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["1-0768 S01"],"gewerk":"gas"}""")).Angenommen);
        Assert.False(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":[],"gewerk":"heizung"}""")).Angenommen);
        Assert.False(t.P.Ausfuehren("planblatt_gewerk_setzen", Par("""{"planblaetter":["gibtsnicht"],"gewerk":"heizung"}""")).Angenommen);
    }

    // ------------------------------------------------------------------ Abfrage plansatz_pdf

    [Fact]
    public void PlansatzPdf_SammelPdf_SeitenFormateLesezeichen_ImVorgeschlagenenOrdner()
    {
        using var t = new PlansatzProjekt();
        var a = t.Pdf(Par("""{"plansatz":"auto:sanitaer","anlass":"Revisionspläne","datum":"2026-10-19"}"""));
        var ordner = Path.Combine(t.Objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "19.10.2026_Revisionspläne", "Sanitär");
        Assert.Equal(ordner, a["ordner"]!.GetValue<string>());
        Assert.True(a["geschrieben"]!.GetValue<bool>());
        Assert.Equal(2, a["seiten"]!.GetValue<int>());
        var dateien = ((JsonArray)a["dateien"]!).Select(d => d!.AsObject()).ToList();
        Assert.Single(dateien);
        var pfad = Path.Combine(ordner, "1-0768_Plansatz Sanitär_2026-10-19.pdf");
        Assert.Equal(pfad, dateien[0]["datei"]!.GetValue<string>());
        Assert.True(File.Exists(pfad));
        Assert.Equal(new FileInfo(pfad).Length, dateien[0]["groesseBytes"]!.GetValue<long>());
        Assert.Equal(["PB-4", "PB-1"], ((JsonArray)a["blaetter"]!).Select(b => b!.GetValue<string>()).ToArray());

        var (formate, lesezeichen) = Lesen(pfad);
        Assert.Equal(new[] { (841.0, 594.0), (420.0, 297.0) }, formate.ToArray());   // S01 A1 quer, S02 A3 quer – jede Seite im Format ihres Blatts
        Assert.Equal(["S01 Sanitär 1.Untergeschoss", "S02 Sanitär 1.Obergeschoss"], lesezeichen);
        Assert.Equal("PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(pfad), 1, 3));
        Assert.Empty(Directory.GetFiles(ordner).Where(f => f != pfad));   // ohne Wunsch keine Einzel-PDFs
    }

    [Fact]
    public void PlansatzPdf_EinzelPdfsMitHKNamen_UndHochformat()
    {
        using var t = new PlansatzProjekt();
        t.Blatt("1-0768 S03", "Sanitär 2/3: Dach", "A3", "1.OG", ["sanitaer"], hoch: true);
        var a = t.Pdf(Par("""{"plansatz":"sanitaer","einzeln":true,"datum":"2026-10-19"}"""));
        var namen = ((JsonArray)a["dateien"]!).Select(d => d!["name"]!.GetValue<string>()).ToList();
        Assert.Equal(["1-0768_Plansatz Sanitär_2026-10-19.pdf", "1-0768-S01-Sanitär 1.Untergeschoss-.pdf", "1-0768-S02-Sanitär 1.Obergeschoss-.pdf", "1-0768-S03-Sanitär 2_3_ Dach-.pdf"], namen);
        var ordner = a["ordner"]!.GetValue<string>();
        Assert.All(namen, n => Assert.True(File.Exists(Path.Combine(ordner, n))));
        Assert.Equal(3, ((JsonArray)a["dateien"]!).Count(d => d!["art"]!.GetValue<string>() == "einzeln"));
        // Seitengrösse der Einzel-PDFs und Hochformat
        Assert.Equal(new[] { (841.0, 594.0) }, Lesen(Path.Combine(ordner, namen[1])).Formate.ToArray());
        Assert.Equal(new[] { (297.0, 420.0) }, Lesen(Path.Combine(ordner, namen[3])).Formate.ToArray());
        Assert.Equal(3, Lesen(Path.Combine(ordner, namen[0])).Formate.Count);
        Assert.Equal((297.0, 420.0), Lesen(Path.Combine(ordner, namen[0])).Formate[2]);
    }

    [Fact]
    public void PlansatzPdf_GleicheEinzelnamenWerdenUnterschieden()
    {
        using var t = new PlansatzProjekt(false);
        t.Blatt("1-0768 S01", "Gleicher Titel", "A3", "EG", ["sanitaer"], mitAnsicht: false);
        t.Blatt("1-0768-S01", "Gleicher Titel", "A3", "EG", ["sanitaer"], mitAnsicht: false);   // andere Schreibweise der Nummer, derselbe H+K-Name
        var a = t.Pdf(Par("""{"plansatz":"sanitaer","einzeln":true,"datum":"2026-10-19"}"""));
        var namen = ((JsonArray)a["dateien"]!).Select(d => d!["name"]!.GetValue<string>()).Skip(1).ToList();
        Assert.Equal(["1-0768-S01-Gleicher Titel-.pdf", "1-0768-S01-Gleicher Titel- (2).pdf"], namen);
        Assert.All(namen, n => Assert.True(File.Exists(Path.Combine(a["ordner"]!.GetValue<string>(), n))));
    }

    [Fact]
    public void Dateinamen_Sammel_Einzel_OhneProjektnummer()
    {
        Assert.Equal("1-0768_Plansatz Sanitär_2026-10-19.pdf", Plaene.Sammelname("1-0768", "Plansatz Sanitär", new DateOnly(2026, 10, 19)));
        Assert.Equal("Plansatz Heizung_2026-01-02.pdf", Plaene.Sammelname(null, "Plansatz Heizung", new DateOnly(2026, 1, 2)));
        Assert.Equal("1-0768_Plan_ A_B_2026-10-19.pdf", Plaene.Sammelname("1-0768", "Plan: A/B", new DateOnly(2026, 10, 19)));   // ungültige Zeichen ersetzt
        using var t = new PlansatzProjekt(false);
        t.Blatt("H01", "Ohne Projektnummer in der Nummer", "A3", "EG", ["heizung"], mitAnsicht: false);
        t.Blatt("PB-7", "Weder Nummer noch Fach", "A3", "EG", ["heizung"], mitAnsicht: false);
        Ok(t.P.Ausfuehren("planblatt_anlegen", Par("""{"nummer":"2317-101","titel":"Andere Nummer"}""")));
        var m = t.P.Modell;
        Assert.Equal("1-0768-H01-Ohne Projektnummer in der Nummer-.pdf", Plaene.Einzelname(Planausgabe.Blatt(m, "H01"), "1-0768"));   // Projektnummer aus der Projektinformation
        Assert.Equal("H01-Ohne Projektnummer in der Nummer-.pdf", Plaene.Einzelname(Planausgabe.Blatt(m, "H01"), null));
        Assert.Equal("1-0768-PB-7-Weder Nummer noch Fach-.pdf", Plaene.Einzelname(Planausgabe.Blatt(m, "PB-7"), "1-0768"));
        Assert.Equal("1-0768-2317-101-Andere Nummer-.pdf", Plaene.Einzelname(Planausgabe.Blatt(m, "2317-101"), "1-0768"));
        Assert.Equal("H01 Ohne Projektnummer in der Nummer", Plaene.Lesezeichen(Planausgabe.Blatt(m, "H01"), "1-0768"));
    }

    [Fact]
    public void PlansatzPdf_NichtUeberschreibenOhneErlaubnis_FehlerMitVorschlag()
    {
        using var t = new PlansatzProjekt();
        var par = """{"plansatz":"auto:sanitaer","anlass":"Test","datum":"2026-10-19"}""";
        var a = t.Pdf(Par(par));
        var datei = ((JsonArray)a["dateien"]!)[0]!["datei"]!.GetValue<string>();
        File.AppendAllText(datei, "% Marke");
        var laenge = new FileInfo(datei).Length;

        var f = Assert.Throws<HlksFehler>(() => t.Pdf(Par(par)));
        Assert.Contains("schon vorhanden", f.Message);
        Assert.Contains("Nichts wurde geschrieben", f.Message);
        Assert.Contains("ueberschreiben = true", f.Vorschlag);
        Assert.Equal(laenge, new FileInfo(datei).Length);            // unberührt

        // mit Einzel-PDFs: schon die eine vorhandene Datei verhindert alles, es entstehen keine neuen
        Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"auto:sanitaer","anlass":"Test","datum":"2026-10-19","einzeln":true}""")));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(datei)!));

        // pruefen schreibt nichts und nennt die vorhandenen Dateien
        var pr = t.Pdf(Par("""{"plansatz":"auto:sanitaer","anlass":"Test","datum":"2026-10-19","einzeln":true,"pruefen":true}"""));
        Assert.False(pr["geschrieben"]!.GetValue<bool>());
        Assert.Single((JsonArray)pr["vorhanden"]!);
        Assert.Equal(3, ((JsonArray)pr["dateien"]!).Count);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(datei)!));

        // mit ueberschreiben: ersetzt, meldet es
        var b = t.Pdf(Par("""{"plansatz":"auto:sanitaer","anlass":"Test","datum":"2026-10-19","ueberschreiben":true}"""));
        Assert.Contains(datei, ((JsonArray)b["ueberschrieben"]!).Select(x => x!.GetValue<string>()));
        Assert.NotEqual(laenge, new FileInfo(datei).Length);
    }

    [Fact]
    public void PlansatzPdf_SpeichertEinstellungAuswahlUndReihenfolge_BeimNaechstenMalWiederDa()
    {
        using var t = new PlansatzProjekt();
        // Satz wird angeboten, noch nicht angelegt; der Export legt ihn an
        Assert.Empty(t.P.Modell.DerKlasse("Plansatz"));
        var a = t.Pdf(Par("""{"plansatz":"auto:heizung","anlass":"Revisionspläne","einzeln":true,"datum":"2026-10-19","blaetter":["1-0768 H02","1-0768 H01"],"abgewaehlt":["1-0768 H01"]}"""));
        Assert.True(a["angelegt"]!.GetValue<bool>());
        Assert.Equal("PS-1", a["plansatz"]!.GetValue<string>());
        Assert.True(a["gespeichert"]!.GetValue<bool>());
        Assert.Equal(1, a["seiten"]!.GetValue<int>());                           // nur H02 dabei
        Assert.Equal(["PB-5"], ((JsonArray)a["blaetter"]!).Select(b => b!.GetValue<string>()).ToArray());
        Assert.Equal(2, ((JsonArray)a["dateien"]!).Count);                       // Sammel-PDF und ein Einzel-PDF
        var ps = t.P.Modell.Hole("PS-1");
        Assert.Equal("heizung", ps.Text("gewerk"));
        var o = (JsonObject)ps.Eigenschaft("ausgabe")!.Inhalt!;
        Assert.Equal("Revisionspläne", o["anlass"]!.GetValue<string>());
        Assert.True(o["einzeln"]!.GetValue<bool>());
        Assert.Null(o["ordner"]);                                                // Vorschlag, nicht festgeschrieben
        Assert.NotNull(o["zeit"]);

        // beim nächsten Mal: Reihenfolge H02, H01 und H01 abgewählt – ohne dass man es noch einmal sagt
        var s = t.Satz("PS-1");
        Assert.Equal(["H02", "H01"], Nummern(s));
        Assert.Equal(["H02"], Nummern(s, nurDabei: true));
        Assert.Equal("Revisionspläne", s["vorschlag"]!["anlass"]!.GetValue<string>());   // letzter Anlass kommt wieder
        Assert.True(s["ausgabe"]!["einzeln"]!.GetValue<bool>());
        var b = t.Pdf(Par("""{"plansatz":"PS-1","datum":"2026-10-20","einzeln":true}"""));   // anderer Tag = neuer Ordner, keine Überschneidung
        Assert.Equal(["PB-5"], ((JsonArray)b["blaetter"]!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Contains("20.10.2026_Revisionspläne", b["ordner"]!.GetValue<string>());

        // wieder zuwählen: nur blaetter ohne abgewaehlt = nur diese; mit abgewaehlt leer = alle
        var c = t.Pdf(Par("""{"plansatz":"PS-1","datum":"2026-10-21","abgewaehlt":[]}"""));
        Assert.Equal(["PB-5", "PB-2"], ((JsonArray)c["blaetter"]!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(["H02", "H01"], Nummern(t.Satz("PS-1"), nurDabei: true));

        // der gespeicherte Stand lässt sich zurücknehmen (eigener Befehl im selben Ablauf = eigenes Paket)
        Assert.True(t.P.Rueckgaengig().Angenommen);
        Assert.Equal(["H02"], Nummern(t.Satz("PS-1"), nurDabei: true));
    }

    [Fact]
    public void PlansatzPdf_EigenerOrdner_WirdGespeichertUndWiederVerwendet()
    {
        using var t = new PlansatzProjekt();
        var eigen = Path.Combine(t.Wurzel, "frei gewählt", "Sanitär");
        var a = t.Pdf(Par($$"""{"plansatz":"sanitaer","ordner":{{System.Text.Json.JsonSerializer.Serialize(eigen)}},"datum":"2026-10-19"}"""));
        Assert.Equal(eigen, a["ordner"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(eigen, "1-0768_Plansatz Sanitär_2026-10-19.pdf")));
        Assert.Equal(eigen, t.Satz("PS-1")["vorschlag"]!["ordnerWirksam"]!.GetValue<string>());
        // der Vorschlag selbst als ordner wird nicht festgeschrieben
        var vorschlag = t.Satz("PS-1", "A")["vorschlag"]!["ordner"]!.GetValue<string>();
        var b = t.Pdf(Par($$"""{"plansatz":"PS-1","anlass":"A","ordner":{{System.Text.Json.JsonSerializer.Serialize(vorschlag)}},"datum":"2026-10-19"}"""));
        Assert.Equal(vorschlag, b["ordner"]!.GetValue<string>());
        Assert.Null(((JsonObject)t.P.Modell.Hole("PS-1").Eigenschaft("ausgabe")!.Inhalt!)["ordner"]);
        // ein relativer Pfad wird abgelehnt
        Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"PS-1","ordner":"relativ\\pfad"}""")));
    }

    [Fact]
    public void PlansatzPdf_Fehler_KeinBlattGewaehlt_FremdesBlatt_UnbekannterSatz()
    {
        using var t = new PlansatzProjekt();
        var f = Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"sanitaer","abgewaehlt":["1-0768 S01","1-0768 S02"]}""")));
        Assert.Contains("kein Planblatt gewählt", f.Message);
        var g = Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"sanitaer","blaetter":["1-0768 H01"]}""")));
        Assert.Contains("gehört nicht zum Plansatz", g.Message);
        Assert.Contains("planblatt_gewerk_setzen", g.Vorschlag);
        Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"gibtsnicht"}""")));
        Assert.Throws<HlksFehler>(() => t.Pdf(Par("""{"plansatz":"sanitaer","datum":"gestern"}""")));
        Assert.Throws<HlksFehler>(() => t.Pdf(Par("{}")));
        Assert.Empty(t.P.Modell.DerKlasse("Plansatz"));   // gescheitert = nichts angelegt
    }

    [Fact]
    public void PlansatzPdf_BlattOhneAnsicht_LeereSeiteMitHinweis_UndFehlerImBlattSchreibtNichts()
    {
        using var t = new PlansatzProjekt(false);
        t.Blatt("1-0768 S01", "Mit Ansicht", "A3", "EG", ["sanitaer"]);
        t.Blatt("1-0768 S02", "Ohne Ansicht", "A3", "EG", ["sanitaer"], mitAnsicht: false);
        t.Blatt("1-0768 S03", "Ohne Nummernbuchstabe", "A3", "EG", ["sanitaer"], mitAnsicht: false);
        var a = t.Pdf(Par("""{"plansatz":"sanitaer","datum":"2026-10-19"}"""));
        Assert.Equal(3, a["seiten"]!.GetValue<int>());
        var hinweise = ((JsonArray)a["hinweise"]!).Select(h => h!.GetValue<string>()).ToList();
        Assert.Contains(hinweise, h => h.Contains("1-0768 S02") && h.Contains("keine Ansicht"));
        Assert.Contains(hinweise, h => h.Contains("1-0768 S03") && h.Contains("keine Ansicht"));
        Assert.DoesNotContain(hinweise, h => h.Contains("1-0768 S01"));

        // Plangrafik eines Blatts scheitert: Fehler nennt das Blatt, im Ordner liegt nichts Neues
        var ordner = a["ordner"]!.GetValue<string>();
        var fehler = Assert.Throws<HlksFehler>(() => Plansatzausgabe.Schreiben(t.P, Par("""{"plansatz":"sanitaer","datum":"2026-10-22","anlass":"Fehler"}"""),
            (_, ansicht) => ansicht.EndsWith("S01") ? throw new HlksFehler("Grafik nicht berechenbar.", "Ansicht prüfen.") : Fake(null!, ansicht)));
        Assert.Contains("PB-1", fehler.Message);
        Assert.Contains("Grafik nicht berechenbar", fehler.Message);
        Assert.False(Directory.Exists(Path.Combine(t.Objekt, "c_Pläne und Modelle", "d_Ausgang", "b_Pläne", "22.10.2026_Fehler")));
        Assert.Single(Directory.GetFiles(ordner));
    }

    [Fact]
    public void PlansatzPdf_Handsatz_MitGewerkOderOhne_UndEchteGrafik()
    {
        using var t = new PlansatzProjekt();
        Ok(t.P.Ausfuehren("plansatz_setzen", Par("""{"name":"Koordination Technik","gewerk":"koordination","blaetter":["1-0768 L01","1-0768 H01","1-0768 S01"]}""")));
        var a = t.Pdf(Par("""{"plansatz":"Koordination Technik","anlass":"Koordination","datum":"2026-10-19"}"""), fake: false);   // echte Plangrafik des Kerns
        Assert.Equal(3, a["seiten"]!.GetValue<int>());
        Assert.Equal("Koordination", Path.GetFileName(a["ordner"]!.GetValue<string>()));
        var pfad = ((JsonArray)a["dateien"]!)[0]!["datei"]!.GetValue<string>();
        Assert.Equal("1-0768_Koordination Technik_2026-10-19.pdf", Path.GetFileName(pfad));
        Assert.Equal(["L01 Lüftung Erdgeschoss", "H01 Heizung 1.Untergeschoss", "S01 Sanitär 1.Untergeschoss"], Lesen(pfad).Lesezeichen);
        Assert.Equal(new[] { (841.0, 594.0), (841.0, 594.0), (841.0, 594.0) }, Lesen(pfad).Formate.ToArray());
    }
}
