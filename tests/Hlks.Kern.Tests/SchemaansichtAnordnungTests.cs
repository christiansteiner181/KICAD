using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Teil B3 (nachgeführt im Nachtrag B8): Schemagraph und Anordnung des Strangschemas – Lagen von Hand gerechnet
/// (Raster SchemaRaster, Testhaus in SchemaansichtHilfe), Schemalinien, Determinismus, Filter, Zonen.</summary>
public class SchemaansichtAnordnungTests
{
    // ------------------------------------------------------------------ Raster, Spalten, Zeilen

    [Fact]
    public void Testhaus_SpaltenJeStrangUndZeilenJeGeschoss()
    {
        var s = Plan(Testhaus());
        Assert.Equal(["KW-1", "KW-2"], s.Spalten.Select(x => x.Titel));
        Assert.All(s.Spalten, x => Assert.Equal("senkrecht", x.Art));
        Assert.Equal([30.0, 65.0], s.Spalten.Select(x => x.X0));            // Gruppen je 35 breit (Bedarf 30)
        Assert.Equal([40.0, 75.0], s.Spalten.Select(x => x.LinieX));
        Assert.All(s.Spalten, x => Assert.Equal(35, x.Breite));
        Assert.Equal(["KW-1", "KW-2"], s.Gruppen.Select(gr => gr.Bezeichnung));   // ohne Steigzone: je Strang eine Gruppe, nach Lage x
        Assert.Equal([1000.0, 3000.0], s.Gruppen.Select(gr => gr.Lage![0]));
        // Zeilen nach Kote von unten: UG 0, EG 50, OG 100; keine Zone «ohne Geschoss»
        Assert.Equal(["UG", "EG", "OG"], s.Zeilen.Select(z => z.Titel));
        Assert.Equal([0.0, 50.0, 100.0], s.Zeilen.Select(z => z.Y0));
        Assert.DoesNotContain(s.Zeilen, z => z.IstZone);
    }

    [Theory]
    [InlineData("TS-1", 40, 20)]    // KW-1, UG: Stranglinie 30 + 10, y0 0 + 20
    [InlineData("TS-2", 40, 70)]    // KW-1, EG
    [InlineData("V-1", 40, 120)]    // KW-1, OG
    [InlineData("TS-3", 75, 20)]    // KW-2, UG: Stranglinie 65 + 10
    [InlineData("TS-4", 75, 70)]    // KW-2, EG
    [InlineData("HA-1", 55, 26)]    // Gerät über R-1 an KW-1: 40 + 15; B8d auf dem Boden: Etagenleitung 20 + 3 + 3 (Kästchen Gerät ±3)
    [InlineData("WT-E", 55, 75.625)]   // B8d: 70 + 3 + 2.625 (Abfluss des Waschtischs unter dem Knoten)
    [InlineData("WT-O", 55, 125.625)]  // OG: Etagenleitung von V-1 (120) + 3 + 2.625
    [InlineData("WM-U", 90, 26)]    // 75 + 15; 20 + 3 + 3 (Waschmaschine)
    [InlineData("DU-E", 90, 75.5)]     // 70 + 3 + 2.5 (Dusche)
    [InlineData("WC-O", 90, 127.375)] // nicht platziert, Geschoss OG; Boden der Zeile OG (V-1, 120) + 3 + 4.375
    public void Testhaus_KnotenLagen_VonHandGerechnet(string kz, double x, double y)
    {
        var s = Plan(Testhaus());
        Assert.Equal((x, y), Lage(s, kz));
        Assert.False(Knoten(s, kz).Manuell);
    }

    [Fact]
    public void Testhaus_KnotenSindNetzelementeOhneLeitungenBoegenReduktionen()
    {
        var s = Plan(Testhaus());
        Assert.Equal(["DU-E", "HA-1", "TS-1", "TS-2", "TS-3", "TS-4", "V-1", "WC-O", "WM-U", "WT-E", "WT-O"], s.Graph.Knoten.Select(k => k.Kennzeichen));
        Assert.Equal("tstueck", Knoten(s, "TS-1").Art);
        Assert.Equal("armatur", Knoten(s, "V-1").Art);
        Assert.Equal("geraet", Knoten(s, "HA-1").Art);
        Assert.Equal("endgeraet", Knoten(s, "WC-O").Art);
        Assert.Equal("senkrecht", Knoten(s, "TS-1").Ausrichtung);
        Assert.Equal("frei", Knoten(s, "WT-E").Ausrichtung);
        Assert.False(Knoten(s, "WC-O").Platziert);
        Assert.True(Knoten(s, "WT-O").Platziert);
    }

    [Theory]
    [InlineData("R-1", "HA-1", "TS-1", "(55,26) (55,20) (40,20)")]           // frei → Strang, gleiche Zeile: Knick unter dem Gerät
    [InlineData("R-2", "TS-1", "TS-2", "(40,20) (40,70)")]                   // auf der Stranglinie
    [InlineData("R-10", "TS-1", "TS-3", "(40,20) (75,20)")]                  // Strang zu Strang, gleiche Höhe
    [InlineData("R-3", "TS-2", "WT-E", "(40,70) (55,70) (55,75.63)")]          // Strang → Endgerät gleiche Zeile: zuerst waagrecht
    [InlineData("R-4", "TS-2", "V-1", "(40,70) (40,120)")]                   // Leitung–Reduktion–Leitung
    [InlineData("R-6", "V-1", "WT-O", "(40,120) (55,120) (55,125.63)")]        // Leitung–Bogen–Leitung
    [InlineData("R-11", "TS-3", "WM-U", "(75,20) (90,20) (90,26)")]
    [InlineData("R-12", "TS-3", "TS-4", "(75,20) (75,70)")]
    [InlineData("R-13", "TS-4", "DU-E", "(75,70) (90,70) (90,75.5)")]
    [InlineData("R-14", "TS-4", "WC-O", "(75,70) (75,127.38) (90,127.38)")]       // andere Zeile: zuerst senkrecht am Strang
    public void Testhaus_Linien_VonHandGerechnet(string element, string von, string nach, string zug)
    {
        var l = LinieMit(Plan(Testhaus()), element);
        Assert.Equal(von, Ende(l.Von));
        Assert.Equal(nach, Ende(l.Nach));
        Assert.Equal(zug, Zug(l));
        Assert.Equal("KW", l.Medium);
    }

    [Fact]
    public void Schemalinie_FasstLeitungBogenLeitungZuEinerLinieZusammen()
    {
        var s = Plan(Testhaus());
        Assert.Equal(10, s.Graph.Linien.Count);
        var l = LinieMit(s, "BO-1");
        Assert.Equal(["R-6", "BO-1", "R-7"], l.Elemente.Select(e => e.Kennzeichen));   // in Fliessrichtung
        Assert.Same(l, LinieMit(s, "R-6"));
        Assert.Same(l, LinieMit(s, "R-7"));
        Assert.Equal(["NW 15"], l.Groessen);
        Assert.Equal("aus", l.Von.Anschluss);
        Assert.Equal("kw", l.Nach.Anschluss);
        // jede Leitung, jeder Bogen und jede Reduktion in genau einer Linie
        var inLinien = s.Graph.Linien.SelectMany(x => x.Elemente).Select(e => e.Kennzeichen).ToList();
        Assert.Equal(inLinien.Count, inLinien.Distinct().Count());
        Assert.Equal(["BO-1", "R-1", "R-10", "R-11", "R-12", "R-13", "R-14", "R-2", "R-3", "R-4", "R-5", "R-6", "R-7", "RD-1"], inLinien.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Schemalinie_GroessenwechselNennAlleGroessenInFliessrichtung()
    {
        var l = LinieMit(Plan(Testhaus()), "RD-1");
        Assert.Equal(["R-4", "RD-1", "R-5"], l.Elemente.Select(e => e.Kennzeichen));
        Assert.Equal(["NW 25", "NW 20"], l.Groessen);
    }

    [Fact]
    public void TStueck_IstKnotenpunktMitDreiLinien()
    {
        var s = Plan(Testhaus());
        var an = s.Graph.Linien.Where(l => l.Von.Knoten?.Kennzeichen == "TS-1" || l.Nach.Knoten?.Kennzeichen == "TS-1").ToList();
        Assert.Equal(3, an.Count);
        Assert.Equal(["a", "b", "c"], an.Select(l => l.Von.Knoten?.Kennzeichen == "TS-1" ? l.Von.Anschluss : l.Nach.Anschluss).Order());
    }

    [Fact]
    public void Linie_ZweiDirektVerbundeneKnoten_OhneElemente()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ventil"), ("kennzeichen", "V-2"), ("teilVon", "KW-2"), ("geschoss", "UG"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ventil"), ("kennzeichen", "V-3"), ("teilVon", "KW-2"), ("geschoss", "UG"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "V-2.aus"), ("nach", "V-3.ein"))));
        var s = Plan(p);
        var l = s.Graph.Linien.Single(x => x.Elemente.Count == 0);
        Assert.Equal("V-2", Ende(l.Von));
        Assert.Equal("V-3", Ende(l.Nach));
        Assert.NotNull(l.Verbindung);
        Assert.Equal("KW", l.Medium);
        // V-2 und V-3 sind von TS-3 aus nicht erreichbar (kein Etagenweg) und stehen deshalb auf der Stranglinie KW-2, UG: nach
        // Fliessrang (beide Anschlüsse «beide»: Kennzeichen) aufwärts – TS-3 (Rang vor V-2, V-3) bleibt unten; die Ventile folgen
        // im Knotenschritt 6
        Assert.Equal((75.0, 20.0), Lage(s, "TS-3"));
        Assert.Equal(75, Lage(s, "V-2").X);
        Assert.Equal(75, Lage(s, "V-3").X);
        Assert.Equal([26.0, 32.0], new[] { Lage(s, "V-2").Y, Lage(s, "V-3").Y }.Order());
    }

    // ------------------------------------------------------------------ Determinismus

    [Fact]
    public void Determinismus_ZweimalGerechnet_Gleich()
    {
        var p = Testhaus();
        Assert.Equal(Plan(p).AlsJson().ToJsonString(), Plan(p).AlsJson().ToJsonString());
        Assert.Equal(Json.Text(Grafik.Plan(p, Ansicht)), Json.Text(Grafik.Plan(p, Ansicht)));
    }

    [Fact]
    public void Determinismus_ReihenfolgeImModellVertauscht_Gleich()
    {
        var p = Testhaus();
        var vorher = Plan(p).AlsJson().ToJsonString();
        var grafik = Json.Text(Grafik.Plan(p, Ansicht));
        var abbild = p.Modell.Abbild([]);
        abbild.Elemente.Reverse();
        abbild.Verbindungen.Reverse();
        p.Modell.Laden(abbild);
        Assert.Equal(p.Modell.Elemente.First().Id, abbild.Elemente[0].Id);   // die Speicherreihenfolge ist wirklich eine andere
        Assert.Equal(vorher, Plan(p).AlsJson().ToJsonString());
        Assert.Equal(grafik, Json.Text(Grafik.Plan(p, Ansicht)));
    }

    [Fact]
    public void Determinismus_AndereAnlegereihenfolge_GleicheLagenUndLinien()
    {
        var a = Plan(Testhaus());
        var b = Plan(Testhaus(umgekehrt: true));
        Assert.Equal(a.Graph.Knoten.Select(k => (k.Kennzeichen, k.X, k.Y)), b.Graph.Knoten.Select(k => (k.Kennzeichen, k.X, k.Y)));
        string Linien(Schemaplan s) => string.Join("\n", s.Graph.Linien.Select(l => $"{string.Join(",", l.Elemente.Select(e => e.Kennzeichen))} {Ende(l.Von)}→{Ende(l.Nach)} {Zug(l)}").Order(StringComparer.Ordinal));
        Assert.Equal(Linien(a), Linien(b));
    }

    [Fact]
    public void KleineAenderung_EinEndgeraetMehr_UebrigeLagenBleiben()
    {
        var p = Testhaus();
        var vorher = Plan(p).Graph.Knoten.ToDictionary(k => k.Kennzeichen, k => (k.X, k.Y));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-15"), ("teilVon", "KW-2"), ("geschoss", "EG"), ("eigenschaften", new JsonObject { ["dn"] = 15 }))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Waschtisch"), ("kennzeichen", "WT-E2"), ("geschoss", "EG"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "R-15.ende"), ("nach", "WT-E2.kw"))));
        var s = Plan(p);
        foreach (var (kz, lage) in vorher) Assert.Equal(lage, Lage(s, kz));
        // WT-E2 am Strang KW-2 (über R-15), EG; vom Steigknoten aus nicht erreichbar (R-15 am Anfang offen) → nach DU-E: 90 + 13.
        // KW-2 wird dadurch 70 breit (Bedarf 10 + 15 + 13 + 5 = 43) – KW-2 ist die letzte Gruppe, die übrigen Lagen bleiben.
        Assert.Equal((103.0, 75.625), Lage(s, "WT-E2"));   // B8d: auf dem Boden der Zeile EG (70 + 3 + 2.625)
        Assert.Equal(70, s.Spalten.Single(x => x.Titel == "KW-2").Breite);
        // R-15 ist am Anfang offen: offenes Ende über WT-E2 (Anschluss kw = Nr. 1 → nach oben, Offenlänge 8)
        var l = LinieMit(s, "R-15");
        Assert.Equal("offen:R-15.anfang", Ende(l.Von));
        Assert.Equal("offen", l.Von.Offen);
        Assert.Equal("(103,83.63) (103,75.63)", Zug(l));
    }

    // ------------------------------------------------------------------ Filter

    [Fact]
    public void Filter_Gewerke_ZeigtNurDieGewaehltenSysteme()
    {
        var p = Testhaus(mitHeizung: true);
        var san = Plan(p);
        Assert.DoesNotContain(san.Graph.Knoten, k => k.Kennzeichen is "HK-E" or "TS-H1");
        Assert.Equal(["KW-1", "KW-2"], san.Spalten.Select(x => x.Titel));
        Assert.DoesNotContain(san.Graph.Linien, l => l.Elemente.Any(e => e.Kennzeichen == "R-H1"));

        var hz = Plan(p, AnsichtHz);
        Assert.Equal(["HK-E", "TS-H1"], hz.Graph.Knoten.Select(k => k.Kennzeichen));
        Assert.Equal(["HZ-1"], hz.Spalten.Select(x => x.Titel));
        Assert.Equal((40.0, 70.0), Lage(hz, "TS-H1"));
        Assert.Equal((55.0, 75.25), Lage(hz, "HK-E"));   // B8d: 70 + 3 + 2.25 (Kästchen Apparat)
        Assert.Equal("HZ-VL", LinieMit(hz, "R-H1").Medium);
    }

    [Fact]
    public void Filter_OhneGewerke_ZeigtAlles()
    {
        var p = Testhaus(mitHeizung: true);
        Ok(p.Ausfuehren("element_aendern", Par(("element", Ansicht), ("eigenschaften", new JsonObject { ["gewerke"] = null }))));
        var s = Plan(p);
        // Gruppen nach Lage im Gebäude (von Süden: x), nicht mehr nach System: KW-1 (x 1000), KW-2 (3000), HZ-1 (Mittel der
        // waagrechten Leitung R-H1: (5250 · 500 + 5500 · 500) / 1000 = 5375); keine Lücke zwischen den Systemen
        Assert.Equal(["KW-1", "KW-2", "HZ-1"], s.Spalten.Select(x => x.Titel));
        Assert.Equal(5375, s.Spalten[2].Lage![0], 6);
        Assert.Contains(s.Graph.Knoten, k => k.Kennzeichen == "HK-E");
        Assert.Equal([30.0, 65.0, 100.0], s.Spalten.Select(x => x.X0));
    }

    [Fact]
    public void Filter_KlassenAusblenden_LinieEndetOffenAmAusgeblendetenElement()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_aendern", Par(("element", Ansicht), ("eigenschaften", new JsonObject { ["klassenAusblenden"] = new JsonArray("Ventil") }))));
        var s = Plan(p);
        Assert.DoesNotContain(s.Graph.Knoten, k => k.Kennzeichen == "V-1");
        var unten = LinieMit(s, "RD-1");
        Assert.Equal("TS-2", Ende(unten.Von));
        Assert.Equal("ausgeblendet", unten.Nach.Offen);
        Assert.Equal("(40,70) (40,78)", Zug(unten));   // TS-2.b = Anschluss Nr. 1 auf der Stranglinie: nach oben
        var oben = LinieMit(s, "BO-1");
        Assert.Equal("ausgeblendet", oben.Von.Offen);
        Assert.Equal("WT-O", Ende(oben.Nach));
        Assert.Equal("(55,114.83) (55,106.83)", Zug(oben));   // WT-O.kw = Nr. 1, frei: nach oben; B8d: ohne Etagenleitung im OG auf der
                                                            // Bandoberkante 101.2 + 3 + 2.625 = 106.825
    }

    [Fact]
    public void Filter_Systeme_WennDieAnsichtSieFuehrt()
    {
        // Der Katalog führt «systeme» an der Ansicht noch nicht (Befund); die Anordnung liest sie schon. Testweise direkt im Modell.
        var p = Testhaus(mitHeizung: true);
        var a = p.Modell.Finde(Ansicht)!.Kopie();
        a.Eigenschaften.Remove("gewerke");
        a.Eigenschaften["systeme"] = new Wert { Inhalt = new JsonArray("HZ") };
        p.Modell.Anwenden([new Operation { Art = "element.setzen", Id = a.Id, Basis = p.Modell.Finde(Ansicht)!.Version, Element = a }]);
        var s = Plan(p);
        Assert.Equal(["HK-E", "TS-H1"], s.Graph.Knoten.Select(k => k.Kennzeichen));
        Assert.Equal(["HZ-1"], s.Spalten.Select(x => x.Titel));
    }

    // ------------------------------------------------------------------ Zonen

    [Fact]
    public void Zonen_OhneStrangUndOhneGeschoss_MitHinweis()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Ventil"), ("kennzeichen", "V-9"), ("system", "KW"))));                        // ohne Strang, ohne Geschoss
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-99"), ("system", "KW"), ("geschoss", "EG"))));      // freier Zug ohne Strang
        var s = Plan(p);
        // Zonen am Schluss nach der Zonenlücke: 30 + 35 + 35 + 10 = 110
        Assert.Equal(["KW-1", "KW-2", "KW ohne Strang"], s.Spalten.Select(x => x.Titel));
        Assert.Equal([30.0, 65.0, 110.0], s.Spalten.Select(x => x.X0));
        var zone = s.Zeilen.Single(z => z.IstZone);
        Assert.Equal(-60, zone.Y0);
        Assert.Equal((120.0, -18.0), Lage(s, "V-9"));               // Zone: 110 + 10, −60 + 42
        Assert.Equal((40.0, 20.0), Lage(s, "TS-1"));                // KW-1 bleibt links
        var frei = LinieMit(s, "R-99");
        Assert.True(frei.Frei);
        Assert.Equal("(115,92) (125,92)", Zug(frei));               // EG-Zelle der Zone, Platz 0: 120 ± 5
        Assert.Contains(s.Hinweise, h => h.StartsWith("Ohne Geschoss") && h.Contains("V-9"));
        Assert.Contains(s.Hinweise, h => h.StartsWith("Ohne Strang") && h.Contains("V-9"));
    }

    [Fact]
    public void FreierZug_RingOhneKnoten_NachDenEndgeraetenDerZelle()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-A"), ("teilVon", "KW-1"), ("geschoss", "EG"))));
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", "R-B"), ("teilVon", "KW-1"), ("geschoss", "EG"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "R-A.ende"), ("nach", "R-B.anfang"))));
        Ok(p.Ausfuehren("verbinden", Par(("von", "R-B.ende"), ("nach", "R-A.anfang"))));
        var s = Plan(p);
        var l = LinieMit(s, "R-A");
        Assert.True(l.Frei);
        Assert.Equal(2, l.Elemente.Count);
        Assert.Same(l, LinieMit(s, "R-B"));
        Assert.Equal("ring", l.Von.Offen);
        Assert.Equal("ring", l.Nach.Offen);
        Assert.Equal("(63,92) (73,92)", Zug(l));   // KW-1, EG: nach WT-E (Platz 0) auf Platz 1: 55 + 13 ± 5
    }

    [Fact]
    public void Zonen_EndgeraetOhneAnschluss_InZoneOhneSystem()
    {
        var p = Testhaus();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "WC"), ("kennzeichen", "WC-9"), ("geschoss", "EG"))));
        var s = Plan(p);
        Assert.Equal("ohne System", s.Spalten[^1].Titel);
        Assert.Equal(110, s.Spalten[^1].X0);                        // nach KW-2 (65 + 35) und der Zonenlücke 10 …
        Assert.Equal((120.0, 77.375), Lage(s, "WC-9"));            // … Zone: 110 + 10; B8d auf dem Boden der Zeile EG: 70 + 3 + 4.375
        Assert.Contains(s.Hinweise, h => h.StartsWith("An keinen Strang angeschlossen") && h.Contains("WC-9"));
    }

    [Fact]
    public void Zonen_SpalteWaechstInGanzenBreiten()
    {
        var p = Testhaus();
        foreach (var i in Enumerable.Range(1, 4))
        {
            Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", $"R-2{i}"), ("teilVon", "KW-1"), ("geschoss", "EG"))));
            Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Waschtisch"), ("kennzeichen", $"WT-E{i}"), ("geschoss", "EG"))));
            Ok(p.Ausfuehren("verbinden", Par(("von", $"R-2{i}.ende"), ("nach", $"WT-E{i}.kw"))));
        }
        var s = Plan(p);
        // KW-1, EG: 5 Endgeräte – 10 + 15 + 4 · 13 + 5 = 82 > 70 → 105 (3 × 35); KW-2 rückt um 70. WT-E ist vom Steigknoten aus
        // erreichbar und steht zuerst, WT-E1…WT-E4 (an offenen Leitungen) folgen nach Kennzeichen
        Assert.Equal(105, s.Spalten[0].Breite);
        Assert.Equal(135, s.Spalten[1].X0);
        Assert.Equal((55.0, 75.625), Lage(s, "WT-E"));             // B8d: y auf dem Boden der Zeile EG
        Assert.Equal((55.0 + 4 * 13, 75.625), Lage(s, "WT-E4"));
    }

    // ------------------------------------------------------------------ Beispielprojekt

    [Fact]
    public void Beispielprojekt_EndgeraetAnZweiSystemen_StehtAmErstenStrang()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var s = Plan(p, "AN-SCHEMA-SAN");
        // Gruppen nach Lage im Gebäude von Süden (x): Steigachsen SW-F1 9000, KW-S1 9100, SW-F2 10200, KW-S2 10300 (Beispielprojekt)
        Assert.Equal(7, s.Spalten.Count);
        var x = s.Spalten.Select(sp => sp.Lage![0]).ToList();
        Assert.Equal(x.Order(), x);
        Assert.Equal([9000.0, 9800.0], s.Spalten.Single(sp => sp.Titel == "SW-F1").Lage!);
        Assert.Equal([9100.0, 9700.0], s.Spalten.Single(sp => sp.Titel == "KW-S1").Lage!);   // Steigachse, nicht die Fallstücke zu den Apparaten
        var reihe = s.Spalten.Select(sp => sp.Titel).ToList();
        Assert.True(reihe.IndexOf("SW-F1") < reihe.IndexOf("KW-S1") && reihe.IndexOf("KW-S1") < reihe.IndexOf("SW-F2") && reihe.IndexOf("SW-F2") < reihe.IndexOf("KW-S2"));
        Assert.Equal("SW-F1", Knoten(s, "WC-E.02").Spalte!.Titel);   // an SW-F1 und KW-S1: die erste Stranglinie (SW-F1 liegt westlich)
        Assert.Equal("KW-V1", Knoten(s, "WM-U.02").Spalte!.Titel);
        Assert.Equal("KW-V1", Knoten(s, "HA-1").Spalte!.Titel);      // Quelle am nächsten Strang
        Assert.Equal("SW-G1", Knoten(s, "KS-1").Spalte!.Titel);      // Senke am nächsten Strang
    }

    [Fact]
    public void Beispielprojekt_WaagrechteLeitungen_AufSpurenUndUnterDenStraengen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var s = Plan(p, "AN-SCHEMA-SAN");
        SchemaSpalte Spalte(string t) => s.Spalten.Single(x => x.Titel == t);
        Assert.Equal("waagrecht", Spalte("KW-V1").Art);
        Assert.Equal("senkrecht", Spalte("KW-S1").Art);
        // Spuren im UG nach Art: Grundleitung, Sammelleitung, Verteilleitung ab 4 im Abstand 4. Nachtrag B8d: die Grundleitung SW-G1 liegt
        // im Erdreich (ohne Bodenplatte unter der Kote des UG), ihre T-Stücke stehen unter dem untersten Band und belegen im UG keine Spur
        // mehr (bis B8c: Grundleitung 4, Sammelleitung 8, Verteilleitung 12)
        Assert.True(Knoten(s, "TS-G1").Untergrund);
        Assert.True(Lage(s, "TS-G1").Y < s.Baender[0].Unten);
        Assert.Equal(4, Lage(s, "TS-S1-F2").Y);
        Assert.Equal(8, Lage(s, "TS-KW-V1-S").Y);
        // T-Stücke, die einen senkrechten Strang speisen, stehen unter dessen Stranglinie (höchstens 1000 mm daneben im Grundriss)
        Assert.Equal(Spalte("KW-S1").LinieX, Lage(s, "TS-KW-V1-S").X);
        Assert.Equal(Spalte("SW-F2").LinieX, Lage(s, "TS-S1-F2").X);   // a von SW-F1 (1200 mm entfernt), c von SW-F2 (darunter)
        // die übrigen in der eigenen Spalte in Fliessrichtung im Waagrechtschritt
        Assert.Equal([Spalte("KW-V1").LinieX, Spalte("KW-V1").LinieX + 10, Spalte("KW-V1").LinieX + 20],
            new[] { "WZ-1", "MV-1", "TS-KW-V1-WM" }.Select(k => Lage(s, k).X));
        Assert.All(new[] { "WZ-1", "MV-1", "TS-KW-V1-WM", "TS-KW-V1-S" }, k => Assert.Equal("waagrecht", Knoten(s, k).Ausrichtung));
        // Linien: zur Stranglinie entlang der Spur, dann hinauf; zum Gerät senkrecht von der Spur weg
        string P(double x, double y) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({x:0.##},{y:0.##})");
        var (sx, sy) = Lage(s, "TS-KW-V1-S"); var (ex, ey) = Lage(s, "TS-KW-S2-E");
        Assert.Equal($"{P(sx, sy)} {P(ex, sy)} {P(ex, ey)}", Zug(LinieMit(s, "KW-V1-4")));
        // B8d: zum Gerät auf dem Boden als Anschlusslinie über der obersten Spur (8) und unter dem Gerät
        var (hx, hy) = Lage(s, "HA-1"); var (wx, wy) = Lage(s, "WZ-1");
        var l0 = LinieMit(s, "KW-V1-0");
        double ya = l0.AnschlussY!.Value;
        Assert.InRange(ya, 8 + SchemaRaster.Anschlussstufe - 1e-9, Knoten(s, "HA-1").Unterkante!.Value - SchemaRaster.Bodenabstand + 1e-9);
        Assert.Equal($"{P(hx, hy)} {P(hx, ya)} {P(wx, ya)} {P(wx, wy)}", Zug(l0));
    }

    [Fact]
    public void Beispielprojekt_Fallstrang_FliesstAbwaerts()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var s = Plan(p, "AN-SCHEMA-SAN");
        // SW-F1 im EG: das untere T (TS-F1-EK) liegt unter dem oberen (TS-F1-EB), obwohl das Wasser von oben kommt
        Assert.True(Lage(s, "TS-F1-EK").Y < Lage(s, "TS-F1-EB").Y);
        // KW-S1 im EG (Steigstrang): Fliessrichtung aufwärts, TS-KW-S1-EK unter TS-KW-S1-EB
        Assert.True(Lage(s, "TS-KW-S1-EK").Y < Lage(s, "TS-KW-S1-EB").Y);
    }
}
