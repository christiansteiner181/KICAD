using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Teil B3: Plangrafik der Schema-Ansicht (SchemaGrafik) – Teile mit Element-Id, Fangpunkte, Linien je
/// Element, Medienfarbe, nicht platzierte Elemente, Symbole (Klasse und Familie), Beschriftung, Beispielprojekt.</summary>
public class SchemaansichtGrafikTests(ITestOutputHelper aus)
{
    static List<Grafikteil> TeileVon(Plangrafik g, Projekt p, string kz) => g.Teile.Where(t => t.Id == Hole(p, kz).Id).ToList();

    static bool Etwa(double[] a, double x, double y) => Math.Abs(a[0] - x) < 1e-6 && Math.Abs(a[1] - y) < 1e-6;

    [Fact]
    public void Plangrafik_ArtSchema_MassstabEins_UeberGrafikPlan()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        Assert.Equal("schema", g.Art);
        Assert.Equal(1, g.Massstab);
        Assert.Equal(Ansicht, g.Ansicht);
        Assert.Equal(p.Modell.Pruefsumme(), g.Stand);
        Assert.NotNull(g.Begrenzung);
        Assert.Equal(Json.Text(SchemaGrafik.Plan(p, Hole(p, Ansicht))), Json.Text(g));
    }

    [Fact]
    public void Plangrafik_JeKnotenEinTeilMitElementIdUndFangpunkt()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        var s = Plan(p);
        foreach (var k in s.Graph.Knoten)
        {
            var t = Assert.Single(g.Teile, x => x.Id == k.Id);
            Assert.NotNull(t.Fangpunkte);
            Assert.Contains(t.Fangpunkte!, f => Etwa(f, k.X, k.Y));
            Assert.NotEmpty(t.Primitive);
        }
        Assert.Equal("formteil", Assert.Single(TeileVon(g, p, "TS-1")).Ebene);
        Assert.Equal("armatur", Assert.Single(TeileVon(g, p, "V-1")).Ebene);
        Assert.Equal("apparat", Assert.Single(TeileVon(g, p, "WT-E")).Ebene);
        // Kennzeichen am Knoten, nicht am T-Stück
        Assert.Contains(TeileVon(g, p, "WT-E")[0].Primitive, x => x.Art == "text" && x.Text == "WT-E");
        Assert.DoesNotContain(TeileVon(g, p, "TS-1")[0].Primitive, x => x.Art == "text");
        // Waschtisch: Sinnbild der HKAP (Linien und gefülltes Polygon), kein Kästchen mit Kürzel mehr (B8b)
        Assert.DoesNotContain(TeileVon(g, p, "WT-E")[0].Primitive, x => x.Art == "text" && x.Text == "WT");
        Assert.Contains(TeileVon(g, p, "WT-E")[0].Primitive, x => x.Art == "polygon" && x.Stil.NurBildschirm == false);
    }

    [Fact]
    public void Plangrafik_JedesLinienelementEinTeilMitId_InFliessrichtungAufgeteilt()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        foreach (var kz in new[] { "R-1", "R-2", "R-3", "R-4", "R-5", "R-6", "R-7", "R-10", "R-11", "R-12", "R-13", "R-14", "BO-1", "RD-1" })
            Assert.Single(TeileVon(g, p, kz));
        // R-2: ganze Linie TS-1 → TS-2 in der Medienfarbe KW, Stift mittel, voll
        var r2 = TeileVon(g, p, "R-2")[0];
        Assert.Equal("leitung", r2.Ebene);
        var linie = r2.Primitive.First(x => x.Art is "linie" or "polylinie");
        Assert.Equal(Gruen, linie.Stil.Farbe);
        Assert.Equal(Stifte.Mittel, linie.Stil.Stift);
        Assert.Equal("voll", linie.Stil.Linie);
        Assert.True(Etwa(linie.Punkte[0], 40, 20) && Etwa(linie.Punkte[^1], 40, 70));
        // Leitung–Bogen–Leitung (40,120) (55,120) (55,125.625) – WT-O steht seit B8d auf dem Boden –, Länge 15 + 5.625 = 20.625: R-6 bis
        // 10.3125 (vor dem Knick, x 50.3125, auf 3 Stellen gerundet 50.312), Bogen dort, R-7 der Rest
        var r6 = TeileVon(g, p, "R-6")[0].Primitive.First(x => x.Art is "linie" or "polylinie");
        Assert.Equal(2, r6.Punkte.Count);
        Assert.True(Etwa(r6.Punkte[0], 40, 120) && Etwa(r6.Punkte[1], 50.312, 120));
        var r7 = TeileVon(g, p, "R-7")[0].Primitive.First(x => x.Art is "linie" or "polylinie");
        Assert.Equal(3, r7.Punkte.Count);
        Assert.True(Etwa(r7.Punkte[0], 50.312, 120) && Etwa(r7.Punkte[1], 55, 120) && Etwa(r7.Punkte[2], 55, 125.625));
        var bogen = TeileVon(g, p, "BO-1")[0];
        Assert.Equal("formteil", bogen.Ebene);
        var kreis = Assert.Single(bogen.Primitive);
        Assert.True(kreis.Stil.NurBildschirm);
        Assert.True(Etwa(kreis.Punkte[0], 50.312, 120));
        // Reduktion in der Mitte von (40,70)–(40,120): Trapez um (40,95), unten breit (gross), oben schmal (klein)
        var rd = TeileVon(g, p, "RD-1")[0].Primitive.Single(x => x.Art == "polygon");
        Assert.False(rd.Stil.NurBildschirm);
        double Breite(double y) => rd.Punkte.Where(q => Math.Abs(q[1] - y) < 1e-6).Select(q => q[0]).DefaultIfEmpty().Max() - rd.Punkte.Where(q => Math.Abs(q[1] - y) < 1e-6).Select(q => q[0]).DefaultIfEmpty().Min();
        Assert.Equal(2.4, Breite(93.8), 6);
        Assert.Equal(1.2, Breite(96.2), 6);
    }

    [Fact]
    public void Plangrafik_GroessenInFliessrichtungAmLaengstenAbschnitt()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        // R-4/RD-1/R-5: senkrecht 50 mm → gedreht links der Linie
        var t = TeileVon(g, p, "R-4")[0].Primitive.Single(x => x.Art == "text");
        Assert.Equal("NW 25 / NW 20", t.Text);
        Assert.Equal(90, t.Winkel);
        Assert.True(Etwa(t.Punkte[0], 39.2, 95));
        // R-1: (55,26) (55,20) (40,20) (B8d: HA-1 auf dem Boden) – längster Abschnitt waagrecht 15 mm → über der Linie, Mitte 47.5
        var t1 = TeileVon(g, p, "R-1")[0].Primitive.Single(x => x.Art == "text");
        Assert.Equal("NW 25", t1.Text);
        Assert.Null(t1.Winkel);
        Assert.True(Etwa(t1.Punkte[0], 47.5, 20.8));
        // grob: keine Grössen
        Ok(p.Ausfuehren("element_aendern", Par(("element", Ansicht), ("eigenschaften", new JsonObject { ["detailgrad"] = "grob" }))));
        Assert.DoesNotContain(TeileVon(Grafik.Plan(p, Ansicht), p, "R-4")[0].Primitive, x => x.Art == "text");
    }

    [Fact]
    public void Plangrafik_NichtPlatziert_GestricheltNurAmBildschirm()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        var rahmen = TeileVon(g, p, "WC-O")[0].Primitive.Where(x => x.Art == "polygon" && x.Stil.Linie == "strich").ToList();
        var r = Assert.Single(rahmen);
        Assert.True(r.Stil.NurBildschirm);
        Assert.DoesNotContain(TeileVon(g, p, "WT-E")[0].Primitive, x => x.Stil.Linie == "strich");
        Assert.Contains(TeileVon(g, p, "R-14")[0].Primitive, x => x.Stil.NurBildschirm && x.Stil.Linie == "punkt");
        Assert.DoesNotContain(TeileVon(g, p, "R-13")[0].Primitive, x => x.Stil.NurBildschirm);
        // auf dem Plan (ohne «nur Bildschirm») sehen nicht platzierte Elemente aus wie die übrigen
        // (Sinnbild des WC plus Kennzeichen)
        var plan = TeileVon(g, p, "WC-O")[0].Primitive.Where(x => !x.Stil.NurBildschirm).Select(x => x.Art).Order();
        var erwartet = SchemaSymbole.Zeichnen(p, Hole(p, "WC-O"), 0, 0, false, null, "mittel").Primitive.Select(x => x.Art).Append("text").Order();
        Assert.Equal(erwartet, plan);
    }

    [Fact]
    public void Plangrafik_RasterMitGeschossenUndStrangkoepfen()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        var eg = Assert.Single(TeileVon(g, p, "EG"));
        Assert.Contains(eg.Primitive, x => x.Art == "text" && x.Text == "EG");
        Assert.Contains(eg.Primitive, x => x.Art == "text" && x.Text == "±0.00");
        // Geschossgrenze als schraffiertes Deckenband (Nachtrag B8) statt Strich-Punkt-Linie: 0 … 100 (zwei Gruppen à 35 ab 30), y 50 ± 1.2
        var band = Assert.Single(eg.Primitive, x => x.Art == "polygon");
        Assert.Equal("beton", band.Stil.Muster);
        Nahe([0.0, 48.8, 100.0, 51.2], Box(band.Punkte), 1e-9);
        Assert.DoesNotContain(g.Teile.SelectMany(t => t.Primitive), x => x.Stil.Linie == "strichpunkt");
        Assert.Contains(TeileVon(g, p, "UG")[0].Primitive, x => x.Text == "−2.70");
        // Kopf: Kreis mit dem Kennzeichen des Strangs über der obersten Zeile (Kreisrand 2 mm über dem Deckenband)
        var kw1 = TeileVon(g, p, "KW-1");
        var kopf = Assert.Single(kw1, t => t.Ebene == "beschriftung");
        var kreis = Assert.Single(kopf.Primitive, x => x.Art == "kreis");
        Assert.Equal(4.5, kreis.Radius);
        Assert.True(Etwa(kreis.Punkte[0], 40, 150 + 1.2 + 2 + 4.5));
        Assert.Contains(kopf.Primitive, x => x.Text == "KW-1" && x.Ausrichtung == "mitte");
        var achse = Assert.Single(kw1, t => t.Ebene == "achse");
        Assert.True(achse.Primitive[0].Stil.NurBildschirm);
        Assert.True(Etwa(achse.Primitive[0].Punkte[0], 40, 0) && Etwa(achse.Primitive[0].Punkte[1], 40, 150));
    }

    [Fact]
    public void Plangrafik_MedienfarbeJeSystem()
    {
        var p = Testhaus(mitHeizung: true);
        var g = Grafik.Plan(p, AnsichtHz);
        var linie = TeileVon(g, p, "R-H1")[0].Primitive.First(x => x.Art is "linie" or "polylinie");
        Assert.Equal("#d0312d", linie.Stil.Farbe);   // HZ-VL im Katalog
        Assert.Empty(TeileVon(g, p, "R-1"));          // anderes Gewerk
    }

    [Fact]
    public void Symbol_VentilAufDerStranglinie_UmNeunzigGradGedreht()
    {
        var p = Testhaus();
        var g = Grafik.Plan(p, Ansicht);
        var dreiecke = TeileVon(g, p, "V-1")[0].Primitive.Where(x => x.Art == "polygon").ToList();
        Assert.Equal(2, dreiecke.Count);
        var x = dreiecke.SelectMany(d => d.Punkte).Select(q => q[0]).ToList();
        var y = dreiecke.SelectMany(d => d.Punkte).Select(q => q[1]).ToList();
        Assert.Equal(3, x.Max() - x.Min(), 6);   // quer 3 mm
        Assert.Equal(4, y.Max() - y.Min(), 6);   // längs der senkrechten Leitung 4 mm
        // auf einer waagrechten Spur bzw. frei: ungedreht (Hand-Lage = frei)
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Ansicht), ("element", "V-1"), ("x", 20), ("y", 120))));
        var frei = TeileVon(Grafik.Plan(p, Ansicht), p, "V-1")[0].Primitive.Where(q => q.Art == "polygon").SelectMany(d => d.Punkte).ToList();
        Assert.Equal(4, frei.Max(q => q[0]) - frei.Min(q => q[0]), 6);
    }

    [Fact]
    public void Symbol_AusDemSchemasymbolDerFamilie()
    {
        var p = Neu();
        var def = FamilieTestHilfe.Testheizkoerper();
        def["schemasymbol"] = JsonNode.Parse("""
            [ { "art": "kreis", "punkte": [[0, 0]], "radius": "laenge / 500" },
              { "art": "linie", "punkte": [[-2, -2], [2, 2]], "stil": { "stift": "mittel" } } ]
            """);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = def }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(5000, 3000, 0), ["kennzeichen"] = "HK-1" }));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-S"), ("eigenschaften", new JsonObject { ["art"] = "schema" }))));
        var s = Plan(p, "AN-S");
        var k = Knoten(s, "HK-1");
        Assert.Equal("familie", SchemaSymbole.Schluessel(p, k.Element));
        Assert.Equal("ohne System", k.Spalte!.Titel);                         // nicht angeschlossen
        var g = Grafik.Plan(p, "AN-S");
        var t = Assert.Single(TeileVon(g, p, "HK-1"));
        var kreis = Assert.Single(t.Primitive, x => x.Art == "kreis");
        Assert.Equal(2, kreis.Radius);                                        // laenge 1000 / 500 (Typ 22-600-1000)
        Assert.True(Etwa(kreis.Punkte[0], k.X, k.Y));
        Assert.Contains(t.Primitive, x => x.Art == "linie" && x.Stil.Stift == Stifte.Mittel && Etwa(x.Punkte[0], k.X - 2, k.Y - 2));
        // anderer Typ, anderer Radius
        Ok(p.Ausfuehren("element_aendern", Par(("element", "HK-1"), ("eigenschaften", new JsonObject { ["familientyp"] = "22-600-1400" }))));
        Assert.Equal(2.8, TeileVon(Grafik.Plan(p, "AN-S"), p, "HK-1")[0].Primitive.Single(x => x.Art == "kreis").Radius!.Value, 6);
    }

    [Fact]
    public void Symbol_OhneSchemasymbol_Klassendarstellung()
    {
        var p = FamilieTestHilfe.MitHeizkoerper();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-S"), ("eigenschaften", new JsonObject { ["art"] = "schema" }))));
        var s = Plan(p, "AN-S");
        Assert.Equal("apparat", SchemaSymbole.Schluessel(p, Knoten(s, "HK-1").Element));
        Assert.Contains(TeileVon(Grafik.Plan(p, "AN-S"), p, "HK-1")[0].Primitive, x => x.Art == "text" && x.Text == "HK");
    }

    [Fact]
    public void Beschriftung_DerSchemaAnsicht_ErscheintNurDort()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", "AN-S2"), ("eigenschaften", new JsonObject { ["art"] = "schema" }))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Textnotiz"), ("kennzeichen", "TX-1"), ("eigenschaften", new JsonObject { ["inhalt"] = "Steigzonen prüfen" }),
            ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([10, 170]) }), ("bezuege", new JsonObject { ["ansicht"] = Ansicht }))));
        var t = Assert.Single(TeileVon(Grafik.Plan(p, Ansicht), p, "TX-1"));
        Assert.Contains(t.Primitive, x => x.Text == "Steigzonen prüfen" && x.Groesse == 2.5);
        Assert.Empty(TeileVon(Grafik.Plan(p, "AN-S2"), p, "TX-1"));
    }

    [Fact]
    public void Planausgabe_SchemaImAnsichtsfensterAlsPdf()
    {
        // ansicht_platzieren nimmt Schemas noch nicht (Befund an den Hauptlauf); die Planausgabe zeichnet sie schon.
        var p = Beispielprojekt.Erzeugen(Kat, null);
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "S-1"), ("titel", "Strangschema Sanitär"), ("format", "A1"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ansichtsfenster"), ("kennzeichen", "AF-S"), ("teilVon", "PB-1"),
            ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([380, 330]) }), ("bezuege", new JsonObject { ["ansicht"] = "AN-SCHEMA-SAN" }))));
        var pdf = Planausgabe.Pdf(p, "PB-1");
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("HLKS_SCHEMA_SKIZZE") is { Length: > 0 } datei)   // Sichtprüfung von Hand
        {
            File.WriteAllBytes(datei, pdf);
            PdfSeite.Rendern(datei, 1, 100, Path.ChangeExtension(datei, ".png"));
        }
    }

    [Fact]
    public void Beispielprojekt_SchemaOhneFehler()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var s = Plan(p, "AN-SCHEMA-SAN");
        var g = Grafik.Plan(p, "AN-SCHEMA-SAN");
        Assert.NotNull(g.Begrenzung);
        Assert.All(g.Begrenzung!, v => Assert.True(double.IsFinite(v)));
        foreach (var h in s.Hinweise) aus.WriteLine(h);
        aus.WriteLine($"{s.Graph.Knoten.Count} Knoten, {s.Graph.Linien.Count} Linien, {s.Spalten.Count} Spalten, {s.Zeilen.Count} Zeilen");
        // jeder Knoten ein Teil mit seiner Id
        foreach (var k in s.Graph.Knoten) Assert.Single(g.Teile, t => t.Id == k.Id);
        // Geschosslinie mit Kennzeichen, Name und Kote
        var eg = Assert.Single(TeileVon(g, p, "EG"));
        Assert.Equal(["EG", "Erdgeschoss", "±0.00"], eg.Primitive.Where(x => x.Art == "text").Select(x => x.Text!).ToArray());
        // alle Sanitär-Netzelemente stehen im Schema: als Knoten oder in genau einer Linie
        var netz = p.Modell.Elemente.Where(e => SchemaGraph.IstNetzelement(Kat, e)).ToList();
        Assert.All(netz, e => Assert.True(s.Graph.NachId.ContainsKey(e.Id) ^ s.Graph.LinieVon.ContainsKey(e.Id), $"{e.Kennzeichen}"));
        var inLinien = s.Graph.Linien.SelectMany(l => l.Elemente).Select(e => e.Id).ToList();
        Assert.Equal(inLinien.Count, inLinien.Distinct().Count());
        Assert.All(s.Graph.Linien, l => Assert.True(l.Punkte.Count >= 2));
        // Linien orthogonal
        Assert.All(s.Graph.Linien, l =>
        {
            for (int i = 1; i < l.Punkte.Count; i++)
                Assert.True(Math.Abs(l.Punkte[i][0] - l.Punkte[i - 1][0]) < 1e-6 || Math.Abs(l.Punkte[i][1] - l.Punkte[i - 1][1]) < 1e-6);
        });
        // das Beispiel ist ganz verbunden: keine offenen Enden; alles platziert
        Assert.DoesNotContain(s.Graph.Linien, l => l.Von.Knoten is null || l.Nach.Knoten is null);
        Assert.All(s.Graph.Knoten, k => Assert.True(k.Platziert));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, Kat), b => b.Code == "P17");
        // Abfrage als JSON
        var o = (JsonObject)Tuer.Abfrage(p, "schema", Par(("ansicht", "AN-SCHEMA-SAN")));
        Assert.Equal(s.Graph.Knoten.Count, o["knoten"]!.AsArray().Count);
    }
}
