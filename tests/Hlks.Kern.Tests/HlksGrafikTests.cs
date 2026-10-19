using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 1a, Teil B2: HLKS-Grafik im Kern (Plangrafik und Körper für Leitungen, Formteile, Armaturen, Apparate,
/// Geräte) und die Darstellungsmasse im Katalog.</summary>
public class HlksGrafikTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static Projekt Beispiel() => Beispielprojekt.Erzeugen(Kat, null);

    static Element E(Projekt p, string kz) => p.Modell.Finde(kz) ?? throw new Xunit.Sdk.XunitException($"Element {kz} fehlt im Beispielprojekt");

    /// <summary>Kontext mit frei wählbarem Detailgrad und Massstab (Grafik.Kontext liest beides aus der Ansicht).</summary>
    static GrafikKontext Ctx(Projekt p, string ansicht, string detail = "mittel", double massstab = 50)
    {
        var a = E(p, ansicht);
        var g = p.Modell.Finde(a.Geschoss)!;
        return new GrafikKontext
        {
            Projekt = p, Ansicht = a, Art = "grundriss", Massstab = massstab, Detailgrad = detail, Geschoss = g,
            Schnitthoehe = (g.Zahl("kote") ?? 0) + 1000,
        };
    }

    static List<Grafikteil> TeileVon(Projekt p, GrafikKontext ctx, string kz) => new HlksGrafik().Plan(ctx, E(p, kz)).ToList();

    static IEnumerable<string?> Farben(Grafikteil t) => t.Primitive.SelectMany(x => new[] { x.Stil.Farbe, x.Stil.Fuellung });

    // ------------------------------------------------------------------ Katalog

    [Fact]
    public void Katalog_JedeApparatUndGeraeteklasseHatDarstellungsmasse()
    {
        var klassen = Kat.Klassen.Values.Where(d => d.Rolle is "verbraucher" or "quelle" or "senke" or "zentrale").ToList();
        Assert.NotEmpty(klassen);
        foreach (var d in klassen)
        {
            Assert.True(d.Darstellung is not null, $"{d.Name}: Klassenattribut «darstellung» fehlt");
            foreach (var k in new[] { "b", "t", "h" })
                Assert.True(d.Darstellung!.TryGetValue(k, out var v) && v > 0, $"{d.Name}: darstellung.{k} fehlt oder ist nicht positiv");
        }
    }

    [Theory]
    [InlineData("WC", 400, 700, 400)]
    [InlineData("Waschtisch", 600, 450, 850)]
    [InlineData("Dusche", 900, 900, 100)]
    [InlineData("Badewanne", 1700, 750, 550)]
    [InlineData("Spueltisch", 1200, 600, 850)]
    [InlineData("Waschmaschine", 600, 600, 850)]
    [InlineData("Bodenablauf", 200, 200, 40)]
    [InlineData("Ausgussbecken", 500, 400, 400)]      // erbt die Vorgabe der Basisklasse Apparat
    [InlineData("Heizkoerper", 500, 400, 400)]        // Vorgabe (sinnbilder.js VORGABE_APPARAT)
    [InlineData("Hausanschluss", 500, 500, 500)]      // Gerät quelle
    [InlineData("Kontrollschacht", 600, 600, 500)]    // Gerät senke
    [InlineData("Wassererwaermer", 700, 700, 700)]    // Gerät zentrale
    [InlineData("Lueftungsgeraet", 700, 700, 700)]
    public void Katalog_MasseWieSinnbilderJs(string klasse, double b, double t, double h)
    {
        var m = Kat.Klassen[klasse].Darstellung!;
        Assert.Equal([b, t, h], [m["b"], m["t"], m["h"]]);
    }

    [Fact]
    public void Katalog_NurHlksKlassenHabenDarstellung()
    {
        foreach (var d in Kat.Klassen.Values.Where(d => d.Darstellung is not null))
            Assert.True(d.Rolle is "verbraucher" or "quelle" or "senke" or "zentrale", $"{d.Name} hat «darstellung», ist aber kein Apparat/Gerät");
    }

    // ------------------------------------------------------------------ Plan

    [Theory]
    [InlineData("AN-GR-UG")]
    [InlineData("AN-GR-EG")]
    [InlineData("AN-GR-1.OG")]
    public void Plan_JedeSichtbareHlksKomponenteHatEinenTeilMitPrimitiven(string ansicht)
    {
        var p = Beispiel();
        var g = Grafik.Plan(p, ansicht);
        var ctx = Grafik.Kontext(p, E(p, ansicht));
        var erwartet = new Dictionary<HlksArt, string> {
            [HlksArt.Leitung] = "leitung", [HlksArt.Formteil] = "formteil", [HlksArt.Armatur] = "armatur",
            [HlksArt.Apparat] = "apparat", [HlksArt.Geraet] = "apparat",
        };
        int gezaehlt = 0;
        foreach (var e in p.Modell.Elemente.Where(e => Grafik.Sichtbar(ctx, e) && Grafik.QuelleFuer(p.Katalog, e) is HlksGrafik))
        {
            var teile = g.Teile.Where(t => t.Id == e.Id).ToList();
            Assert.True(teile.Count >= 1, $"{e.Kennzeichen}: kein Grafikteil im Grundriss {ansicht}");
            Assert.All(teile, t => Assert.NotEmpty(t.Primitive));
            Assert.All(teile, t => Assert.Equal(erwartet.GetValueOrDefault(HlksEinordnung.Art(Kat, e), "sonst"), t.Ebene));
            gezaehlt++;
        }
        Assert.True(gezaehlt > 0, $"{ansicht}: keine HLKS-Elemente gefunden");
        Assert.NotNull(g.Begrenzung);
    }

    [Fact]
    public void Plan_ErstesObergeschossEnthaeltLeitungenUndApparate()
    {
        var p = Beispiel();
        var g = Grafik.Plan(p, "AN-GR-1.OG");
        var ids = g.Teile.Select(t => t.Id).ToHashSet();
        Assert.Contains(E(p, "WC-1.02").Id, ids);
        Assert.Contains(E(p, "BW-1.02").Id, ids);
        Assert.Contains(E(p, "SW-1.02-WC").Id, ids);
        Assert.Contains(E(p, "KW-1.02-WT").Id, ids);
        Assert.Contains(g.Teile, t => t.Ebene == "formteil");
        Assert.DoesNotContain(g.Teile, t => t.Id == E(p, "WC-E.02").Id);   // anderes Geschoss
    }

    [Fact]
    public void Plan_LeitungenTragenDieMedienfarbeAusDemKatalog()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        // SW DN100: breiter als 0.35 mm Papier → Band mit Füllung in Medienfarbe
        var sw = TeileVon(p, ctx, "SW-1.02-WC").Single();
        Assert.Contains(Kat.Medien["SW"].FarbeHex, Farben(sw));
        Assert.All(sw.Primitive, x => Assert.True(x.Art is "polygon" or "kreis"));
        // KW DN15: schmal → Polylinie mit Stift «mittel» in Medienfarbe
        var kw = TeileVon(p, ctx, "KW-1.02-WT").Single();
        var linie = Assert.Single(kw.Primitive);
        Assert.Equal("polylinie", linie.Art);
        Assert.Equal(Kat.Medien["KW"].FarbeHex, linie.Stil.Farbe);
        Assert.Equal(Stifte.Mittel, linie.Stil.Stift);
        Assert.Equal("voll", linie.Stil.Linie);
    }

    [Fact]
    public void Plan_MediumUndLinienartStimmenMitDerDarstellungDesKernsUeberein()
    {
        var p = Beispiel();
        foreach (var e in p.Modell.Elemente)
        {
            var d = Darstellung.Element(p, e);
            var md = HlksEinordnung.Medium(p, e);
            Assert.Equal(d["farbe"]?.GetValue<string>(), md?.FarbeHex);
            Assert.Equal(d["linie"]?.GetValue<string>(), md?.Linie);
        }
    }

    [Fact]
    public void Plan_Leitung_FangpunkteSindEndenUndKnicke()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var t = TeileVon(p, ctx, "SW-1.02-WT").Single();   // 3 Punkte, DN50
        Assert.Equal(3, t.Fangpunkte!.Count);
        Assert.Equal([8500.0, 9600.0], t.Fangpunkte[0]);
        Assert.Equal([8500.0, 9300.0], t.Fangpunkte[1]);
        Assert.Equal([9300.0, 9300.0], t.Fangpunkte[2]);
    }

    [Fact]
    public void Plan_SenkrechteLeitungIstEinRing()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var t = TeileVon(p, ctx, "SW-F1-1").Single();   // DN100 senkrecht
        var ring = Assert.Single(t.Primitive);
        Assert.Equal("kreis", ring.Art);
        Assert.Equal([9000.0, 9800.0], ring.Punkte[0]);
        Assert.Equal(Math.Max(0.6 * 100 * 0.9, 50), ring.Radius!.Value, 6);   // max(0.9 w, DN/2) mit w = 60
        Assert.Equal("papier", ring.Stil.Fuellung);
        Assert.Equal(Kat.Medien["SW"].FarbeHex, ring.Stil.Farbe);
    }

    [Fact]
    public void Plan_DnBreiteLeitungWirdBandMitDnBreite()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG", massstab: 50);
        var t = TeileVon(p, ctx, "SW-1.02-WC").Single();   // (7500,9500) → (8800,9700), DN100: w = 60 mm
        var band = t.Primitive.First(x => x.Art == "polygon");
        var l = Math.Sqrt(1300 * 1300 + 200 * 200);
        // Breite = Abstand der beiden Längsseiten
        var a = band.Punkte[0]; var b = band.Punkte[3];
        Assert.Equal(60, Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2)), 6);
        Assert.Equal(l, Math.Sqrt(Math.Pow(band.Punkte[1][0] - a[0], 2) + Math.Pow(band.Punkte[1][1] - a[1], 2)), 6);
        // gleicher Strang im Massstab 1:200 → schmaler als 0.35 mm Papier → Polylinie
        var klein = TeileVon(p, Ctx(p, "AN-GR-1.OG", massstab: 200), "SW-1.02-WC").Single();
        Assert.Equal("polylinie", Assert.Single(klein.Primitive).Art);
    }

    [Fact]
    public void Plan_GrobZeichnetLeitungenMitStiftMittel()
    {
        var p = Beispiel();
        var t = TeileVon(p, Ctx(p, "AN-GR-1.OG", "grob"), "SW-1.02-WC").Single();
        var l = Assert.Single(t.Primitive);
        Assert.Equal("polylinie", l.Art);
        Assert.Equal(Stifte.Mittel, l.Stil.Stift);
    }

    [Fact]
    public void Plan_DnAngabeNurImFeinenDetailgradUndAufDemPlan()
    {
        var p = Beispiel();
        Assert.DoesNotContain(TeileVon(p, Ctx(p, "AN-GR-1.OG"), "SW-1.02-WC").Single().Primitive, x => x.Art == "text");
        // B9b (Entscheid Christian 19.10.2026): Abwasser trägt den Aussendurchmesser; das Beispielprojekt nennt nur DN → ohne da keine Grösse
        Assert.DoesNotContain(TeileVon(p, Ctx(p, "AN-GR-1.OG", "fein"), "SW-1.02-WC").Single().Primitive, x => x.Art == "text");
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "SW-1.02-WC", ["eigenschaften"] = new JsonObject { ["da"] = 110 } }).Angenommen);
        var t = TeileVon(p, Ctx(p, "AN-GR-1.OG", "fein"), "SW-1.02-WC").Single();
        var text = t.Primitive.Single(x => x.Art == "text");
        Assert.Equal("110", text.Text);   // Abwasser: Aussendurchmesser ohne Ø, ohne NW/DN
        // Wasser und Heizung bleiben NW (Vorgabe, Christian 17.10.2026), DN auf Einstellung
        var kw = p.Modell.Elemente.First(e => e.Klasse == "Rohr" && p.Modell.Finde(e.System)?.Kennzeichen == "KW" && e.Zahl("dn") is not null && e.Geometrie is { Art: "polylinie" });
        Assert.Equal("NW " + kw.Zahl("dn"), Leitungsbezeichnung.Text(p, p.Modell.Finde("AN-GR-1.OG"), kw));
        Assert.False(text.Stil.NurBildschirm);
        Assert.Equal(2.0, text.Groesse);
        // Leserichtung: nie auf dem Kopf
        Assert.InRange(text.Winkel!.Value, -90 + 1e-9, 90);
        // über der Linie: liegt links der Laufrichtung der Linie (Normale), also bei dieser Linie (nach rechts, leicht ansteigend) oberhalb
        var m = new[] { (7500 + 8800) / 2.0, (9500 + 9700) / 2.0 };
        Assert.True(text.Punkte[0][1] > m[1]);
    }

    [Fact]
    public void Plan_GestrichelteMedienWerdenGestrichelt()
    {
        var p = Beispiel();
        // Regenwasser (Katalog: gestrichelt)
        void Ok(BefehlsErgebnis r) => Assert.True(r.Angenommen, r.Fehler + " " + r.Vorschlag);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-RW" }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "RW", ["teilVon"] = "A-RW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Regenwasser" } }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "RW", ["kennzeichen"] = "RW-T1", ["dn"] = 100, ["geschoss"] = "1.OG",
            ["punkte"] = new JsonArray(new JsonArray(0, 0, 3000), new JsonArray(3000, 0, 3000)),
        }));
        Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["system"] = "RW", ["kennzeichen"] = "RW-T2", ["dn"] = 15, ["geschoss"] = "1.OG",
            ["punkte"] = new JsonArray(new JsonArray(0, 500, 3000), new JsonArray(3000, 500, 3000)),
        }));
        var ctx = Ctx(p, "AN-GR-1.OG");
        // schmal: Stift mit Linienart «strich»
        var schmal = TeileVon(p, ctx, "RW-T2").Single();
        Assert.Equal("strich", Assert.Single(schmal.Primitive).Stil.Linie);
        Assert.Equal(Kat.Medien["RW"].FarbeHex, schmal.Primitive[0].Stil.Farbe);
        // breit: Band in Stücken. Strich 3 w = 180 mm, Lücke 90 mm → 3000 / 270 ≈ 11.1 Striche
        var breit = TeileVon(p, ctx, "RW-T1").Single();
        var striche = breit.Primitive.Count(x => x.Art == "polygon");
        Assert.InRange(striche, 11, 12);
        Assert.All(breit.Primitive, x => Assert.Equal(Kat.Medien["RW"].FarbeHex, x.Stil.Fuellung));
        // Lücken: kein Polygon deckt den Punkt (180 + 45, 0) ab
        Assert.DoesNotContain(breit.Primitive.Where(x => x.Art == "polygon"), x => x.Punkte.Min(q => q[0]) < 225 && x.Punkte.Max(q => q[0]) > 225);
    }

    [Fact]
    public void Linien_StrichelungSchneidetUeberKnickeHinweg()
    {
        double[][] z = [[0, 0], [100, 0], [100, 100]];   // Länge 200
        var s = HlksLinien.Striche(z, 130, 30);         // Strich 0–130 (über den Knick), Lücke 130–160, Strich 160–200
        Assert.Equal(2, s.Count);
        Assert.Equal(3, s[0].Count);                    // Anfang, Knick bei (100, 0), Ende bei (100, 30)
        Assert.Equal([100.0, 0.0], s[0][1]);
        Assert.Equal([100.0, 30.0], s[0][2]);
        Assert.Equal([[100.0, 60.0], [100.0, 100.0]], s[1]);
        Assert.Equal(170, s.Sum(HlksLinien.Laenge), 6);
    }

    [Fact]
    public void Plan_Formteil_KreisInMedienfarbeMitHellemRand()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var t = TeileVon(p, ctx, "TS-SW-1.02-2").Single();   // T-Stück DN100: r = max(0.7 M, 0.65 w) = max(35, 39)
        Assert.Equal("formteil", t.Ebene);
        Assert.Equal(2, t.Primitive.Count);
        Assert.Equal("papier", t.Primitive[0].Stil.Fuellung);
        Assert.Equal(Kat.Medien["SW"].FarbeHex, t.Primitive[1].Stil.Fuellung);
        Assert.Equal(39.0, t.Primitive[1].Radius!.Value, 6);
        Assert.Equal([8800.0, 9700.0], t.Fangpunkte![0]);
    }

    [Fact]
    public void Plan_Armatur_RauteMitKreuzUndZaehlerAlsKreis()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-UG");
        var ventil = TeileVon(p, ctx, "MV-1").Single();
        Assert.Equal("armatur", ventil.Ebene);
        Assert.Contains(ventil.Primitive, x => x.Art == "polygon" && x.Punkte.Count == 4);
        Assert.Equal(2, ventil.Primitive.Count(x => x.Art == "linie"));
        Assert.DoesNotContain(ventil.Primitive, x => x.Art == "kreis");
        Assert.All(ventil.Primitive, x => Assert.Equal(Stifte.Mittel, x.Stil.Stift));
        var zaehler = TeileVon(p, ctx, "WZ-1").Single();
        Assert.Contains(zaehler.Primitive, x => x.Art == "kreis" && Math.Abs(x.Radius!.Value - 60) < 1e-9);   // s = 1.2 M
        // Kennzeichen nur im feinen Detailgrad, nur Bildschirm
        Assert.DoesNotContain(ventil.Primitive, x => x.Art == "text");
        var fein = TeileVon(p, Ctx(p, "AN-GR-UG", "fein"), "MV-1").Single();
        var etikett = fein.Primitive.Single(x => x.Art == "text");
        Assert.Equal("MV-1", etikett.Text);
        Assert.True(etikett.Stil.NurBildschirm);
    }

    [Fact]
    public void Plan_Apparat_SinnbildMitKennzeichenNurAmBildschirm()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var wc = TeileVon(p, ctx, "WC-1.02").Single();
        Assert.Equal("apparat", wc.Ebene);
        Assert.Equal(7500.0, wc.Fangpunkte![0][0]);
        Assert.Equal(9500.0, wc.Fangpunkte[0][1]);
        // Kennzeichen-Etikett: nur Bildschirm, alle anderen Primitive (Sinnbild) sind druckbar
        var etiketten = wc.Primitive.Where(x => x.Art == "text").ToList();
        Assert.Equal("WC-1.02", Assert.Single(etiketten).Text);
        Assert.All(etiketten, x => Assert.True(x.Stil.NurBildschirm));
        Assert.All(wc.Primitive.Where(x => x.Art != "text"), x => Assert.False(x.Stil.NurBildschirm));
        // Planfarbe (kein Medium am Apparat) und dünner Stift
        Assert.All(wc.Primitive.Where(x => x.Art != "text"), x => { Assert.Null(x.Stil.Farbe); Assert.Equal(Stifte.Duenn, x.Stil.Stift); });
        // alle Sinnbild-Primitive liegen im Umfeld des Umrisses (400 × 700 mm um die Mitte)
        foreach (var x in wc.Primitive.Where(x => x.Art == "polygon"))
            foreach (var q in x.Punkte)
            {
                Assert.InRange(q[0], 7500 - 200 - 1, 7500 + 200 + 1);
                Assert.InRange(q[1], 9500 - 350 - 1, 9500 + 350 + 1);
            }
        // Spülkasten (SVG oben = Norden) liegt auf der Nordseite
        Assert.True(wc.Primitive[0].Punkte.Average(q => q[1]) > 9500);
    }

    [Fact]
    public void Plan_Apparat_GrobNurUmriss()
    {
        var p = Beispiel();
        var t = TeileVon(p, Ctx(p, "AN-GR-1.OG", "grob"), "BW-1.02").Single();
        var poly = Assert.Single(t.Primitive, x => x.Art == "polygon");
        Assert.Equal(4, poly.Punkte.Count);
        Assert.Equal(1700, poly.Punkte.Max(q => q[0]) - poly.Punkte.Min(q => q[0]), 6);   // Katalogmass b
        Assert.Equal(750, poly.Punkte.Max(q => q[1]) - poly.Punkte.Min(q => q[1]), 6);    // Katalogmass t
    }

    [Fact]
    public void Plan_Geraet_SinnbildNachRolle()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-UG");
        var ha = TeileVon(p, ctx, "HA-1").Single();   // quelle: Kreis mit Dreieck
        Assert.Contains(ha.Primitive, x => x.Art == "kreis" && x.Radius == 250);
        Assert.Contains(ha.Primitive, x => x.Art == "polygon" && x.Punkte.Count == 3);
        // Medienfarbe des Systems KW
        Assert.All(ha.Primitive.Where(x => x.Art != "text"), x => Assert.Equal(Kat.Medien["KW"].FarbeHex, x.Stil.Farbe));
        var ks = TeileVon(p, ctx, "KS-1").Single();   // senke: Doppelkreis mit Schrägstrich
        Assert.Equal(2, ks.Primitive.Count(x => x.Art == "kreis"));
        Assert.Single(ks.Primitive, x => x.Art == "linie");
    }

    [Fact]
    public void Plan_Hindernisse_FuerRaumstempelNachVorbereiten()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var hlks = new HlksGrafik();
        hlks.Vorbereiten(ctx);
        var h = HlksGrafik.Hindernisse(ctx);
        Assert.NotEmpty(h);
        // WC bei (7500, 9500): 400 × 700 samt 80 mm Rand
        Assert.Contains(h, r => r[0] == 7500 - 200 - 80 && r[1] == 9500 - 350 - 80 && r[3] == 9500 + 350 + 80);
    }

    [Fact]
    public void Plan_NurImGrundrissUndNurMitGeometrie()
    {
        var p = Beispiel();
        var ctx = Ctx(p, "AN-GR-1.OG");
        var schnitt = new GrafikKontext { Projekt = p, Ansicht = ctx.Ansicht, Art = "schnitt", Massstab = 50 };
        Assert.Empty(new HlksGrafik().Plan(schnitt, E(p, "WC-1.02")));
        Assert.Empty(new HlksGrafik().Plan(ctx, E(p, "SW")));   // System: keine Geometrie
        Assert.True(new HlksGrafik().Zustaendig(Kat, E(p, "WC-1.02")));
        Assert.IsType<HlksGrafik>(Grafik.Quellen[^1]);   // letzte Quelle (für alles Übrige zuständig)
        Assert.IsType<KollisionGrafik>(Grafik.Quellen[^2]);    // Kollisionsmarken davor (Modul 3b, B4a)
        Assert.IsType<AussparungGrafik>(Grafik.Quellen[^3]);   // Aussparungen davor (Modul 3b, B4a)
        Assert.IsType<HuelleGrafik>(Grafik.Quellen[^4]);       // Darstellung Hülle davor (Modul 1c, C1b; für kein Element zuständig)
        Assert.IsType<GrundlagenGrafik>(Grafik.Quellen[^5]);   // Unterlagen davor (Modul 1b, Befund B3-1)
    }

    // ------------------------------------------------------------------ Körper

    static Koerper K(Projekt p, string kz) => Grafik.Koerper(p, [E(p, kz).Id]).Single();

    [Theory]
    [InlineData("SW-F1-1", 50.0)]     // DN100 → Radius 50
    [InlineData("SW-1.02-WT", 25.0)]  // DN50
    [InlineData("KW-V1-1", 16.0)]     // DN32
    [InlineData("KW-1.02-WT", 15.0)]  // DN15 < 30 → mindestens DN 30, Radius 15
    public void Koerper_RohrIstZylinderMitDnRadius(string kz, double radius)
    {
        var p = Beispiel();
        var k = K(p, kz);
        Assert.Equal("leitung", k.Ebene);
        var zyl = k.Teile.Where(t => t.Art == "zylinder").ToList();
        Assert.NotEmpty(zyl);
        Assert.All(zyl, t => { Assert.Equal(radius, t.Radius!.Value, 9); Assert.Equal("rohr", t.Material); });
    }

    [Fact]
    public void Koerper_RohrJeSegmentEinZylinderUndKugelnAnDenKnicken()
    {
        var p = Beispiel();
        var k = K(p, "SW-1.02-WT");   // (8500,9600,z+344.5) → (8500,9300,z+338.5) → (9300,9300,z+322.5)
        Assert.Equal(2, k.Teile.Count(t => t.Art == "zylinder"));
        var kugel = Assert.Single(k.Teile, t => t.Art == "kugel");
        Assert.Equal([8500.0, 9300.0, 2900 + 338.5], kugel.Mitte!);
        Assert.Equal(25.0, kugel.Radius!.Value);
        var z0 = k.Teile.First(t => t.Art == "zylinder");
        Assert.Equal([8500.0, 9600.0, 2900 + 344.5], z0.Von!);
        Assert.Equal([8500.0, 9300.0, 2900 + 338.5], z0.Nach!);
        // Medienfarbe
        Assert.All(k.Teile, t => Assert.Equal(Kat.Medien["SW"].FarbeHex, t.Farbe));
    }

    [Fact]
    public void Koerper_Formteil_KugelMalEinDreiZehntel()
    {
        var p = Beispiel();
        var k = K(p, "TS-SW-1.02-2");   // DN100 → r = 50 × 1.3
        Assert.Equal("formteil", k.Ebene);
        var t = Assert.Single(k.Teile);
        Assert.Equal("kugel", t.Art);
        Assert.Equal(65.0, t.Radius!.Value, 9);
        Assert.Equal("formteil", t.Material);
        Assert.Equal(Kat.Medien["SW"].FarbeHex, t.Farbe);
    }

    [Fact]
    public void Koerper_Armatur_ZylinderMalEinsSiebenMitAchseInX()
    {
        var p = Beispiel();
        var k = K(p, "MV-1");   // DN32 → r = 16 × 1.7 = 27.2, Länge 1.6 r
        Assert.Equal("armatur", k.Ebene);
        var t = Assert.Single(k.Teile);
        Assert.Equal("zylinder", t.Art);
        Assert.Equal(27.2, t.Radius!.Value, 9);
        Assert.Equal(1500 - 27.2 * 0.8, t.Von![0], 9);
        Assert.Equal(1500 + 27.2 * 0.8, t.Nach![0], 9);
        Assert.Equal([2500.0, -2400.0], [t.Von[1], t.Von[2]]);
        Assert.Equal("armatur", t.Material);
    }

    [Fact]
    public void Koerper_WcIstQuaderMitKatalogmassen()
    {
        var p = Beispiel();
        var k = K(p, "WC-1.02");   // Mitte am Boden bei (7500, 9500, 2900)
        Assert.Equal("apparat", k.Ebene);
        var q = Assert.Single(k.Teile);
        Assert.Equal("quader", q.Art);
        Assert.Equal([400.0, 700.0, 400.0], q.Masse!);
        Assert.Equal([7500.0, 9500.0, 2900 + 200.0], q.Mitte!);
        Assert.Equal(0, q.Drehung ?? 0);
        Assert.Equal("apparat", q.Material);
        Assert.Null(q.Farbe);
    }

    [Fact]
    public void Koerper_Geraet_WuerfelUmDenPunkt()
    {
        var p = Beispiel();
        var q = Assert.Single(K(p, "KS-1").Teile);   // Kontrollschacht (senke) b = 600
        Assert.Equal("quader", q.Art);
        Assert.Equal([600.0, 600.0, 600.0], q.Masse!);
        Assert.Equal([13500.0, 9800.0, -3034.0], q.Mitte!);
        Assert.Equal("geraet", q.Material);
    }

    [Fact]
    public void Koerper_AlleHlksElementeMitGeometrieHabenKoerper()
    {
        var p = Beispiel();
        var koerper = Grafik.Koerper(p).ToDictionary(k => k.Id);
        foreach (var e in p.Modell.Elemente.Where(e => Grafik.IstModellgrafik(Kat, e) && Grafik.QuelleFuer(Kat, e) is HlksGrafik && HlksEinordnung.Punkte(e).Count > 0))
        {
            var art = HlksEinordnung.Art(Kat, e);
            if (art == HlksArt.Leitung && HlksEinordnung.Punkte(e).Count < 2) continue;
            Assert.True(koerper.ContainsKey(e.Id), $"{e.Kennzeichen}: kein Körper");
            Assert.NotEmpty(koerper[e.Id].Teile);
        }
        // Materialien nur aus der vereinbarten Liste
        var erlaubt = new[] { "rohr", "formteil", "armatur", "apparat", "geraet", "sonst" };
        Assert.All(koerper.Values.Where(k => k.Ebene is "leitung" or "formteil" or "armatur" or "apparat"), k => Assert.All(k.Teile, t => Assert.Contains(t.Material, erlaubt)));
    }

    [Fact]
    public void Koerper_SonstigeElemente()
    {
        var p = Beispiel();
        var hlks = new HlksGrafik();
        // Fussbodenheizkreis (rolle verbraucher) wird wie in der Oberfläche als Apparat am ersten Punkt behandelt; ein Klassen-
        // loses Sonst-Element gibt es im Katalog nicht, deshalb hier die Hilfsklasse direkt prüfen
        var e = new Element { Id = "x", Kennzeichen = "X", Klasse = "Unbekannt", Geometrie = new Geometrie { Art = "punkt", Punkte = [[100, 200, 300]] } };
        var k = hlks.Koerper(p, e)!;
        Assert.Equal("sonst", k.Ebene);
        var q = Assert.Single(k.Teile);
        Assert.Equal([200.0, 200.0, 200.0], q.Masse!);
        Assert.Equal([100.0, 200.0, 300.0], q.Mitte!);
        Assert.Equal("sonst", q.Material);
        e.Geometrie = new Geometrie { Art = "polylinie", Punkte = [[0, 0, 0], [1000, 0, 0]] };
        var r = Assert.Single(hlks.Koerper(p, e)!.Teile);
        Assert.Equal("zylinder", r.Art);
        Assert.Equal(15.0, r.Radius);
        e.Geometrie = new Geometrie { Art = "polygon", Punkte = [[0, 0, 100], [1000, 0, 100], [1000, 1000, 100]] };
        var pr = Assert.Single(hlks.Koerper(p, e)!.Teile);
        Assert.Equal("prisma", pr.Art);
        Assert.Equal(100.0, pr.Oben);
        Assert.Equal(80.0, pr.Unten);
        // Plan: Quadrat ±150 mit Kennzeichen am Bildschirm
        e.Geometrie = new Geometrie { Art = "punkt", Punkte = [[100, 200, 300]] };
        var ctx = Ctx(p, "AN-GR-1.OG");
        var t = Assert.Single(hlks.Plan(ctx, e));
        Assert.Equal("sonst", t.Ebene);
        Assert.Equal(300, t.Primitive[0].Punkte.Max(x => x[0]) - t.Primitive[0].Punkte.Min(x => x[0]));
        Assert.True(t.Primitive.Single(x => x.Art == "text").Stil.NurBildschirm);
    }

    // ------------------------------------------------------------------ Einordnung

    [Theory]
    [InlineData("SW-F1-1", HlksArt.Leitung)]
    [InlineData("BO-F1-1", HlksArt.Formteil)]
    [InlineData("TS-F1-EB", HlksArt.Formteil)]
    [InlineData("MV-1", HlksArt.Armatur)]
    [InlineData("WZ-1", HlksArt.Armatur)]
    [InlineData("WC-E.02", HlksArt.Apparat)]
    [InlineData("BA-U.02", HlksArt.Apparat)]
    [InlineData("HA-1", HlksArt.Geraet)]
    [InlineData("KS-1", HlksArt.Geraet)]
    public void Einordnung_WieDieOberflaeche(string kz, HlksArt erwartet)
    {
        var p = Beispiel();
        Assert.Equal(erwartet, HlksEinordnung.Art(Kat, E(p, kz)));
    }
}
