using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;
using static Hlks.Kern.Tests.SchemaStrangschemaHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8c: Einlagen im Deckenband des Strangschemas (SchemaAnordnung.cs «Einlagen», SchemaGrafik.cs). Testhaus
/// «MFH Testweg» (SchemaStrangschemaHilfe, FIKTIV) mit Decke über UG D-EG: 12000 × 10000, Oberkante 0, Dicke 300 (z −300 … 0), aufbau
/// «Beton 300» (FIKTIV). Mit Einlagen liegen die Etagenleitungen Schmutzwasser des EG beider Wohnungen (SW-E1-*, SW-E2-*) und ihre
/// T-Stücke statt auf z +150 auf z −150 in der Decke; SW-E{s}-1 läuft zuletzt lotrecht auf +150 zum Steig-T (700 + 150 von 1000 mm
/// im Beton = 85 % → Einlage, teils frei). Handrechnung Raster: zwei Spuren (SW-1, SW-2) im Band 1 (Decke über UG) → Höhe
/// 2 · 1.2 + 1 · 1.5 = 3.9 mm, Zuwachs 1.5 mm; Oberkante bleibt bei EG.y0 + 1.2, die Zeilen EG, 1.OG, 2.OG rücken um 1.5 mm nach oben.</summary>
public class SchemaEinlagenTests(ITestOutputHelper aus)
{
    static void A(Projekt p, string befehl, JsonObject par) => Ok(p.Ausfuehren(befehl, par));

    static void DeckeUeberUg(Projekt p) =>
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-EG", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 300" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, 0], [12000, 0, 0], [12000, 10000, 0], [0, 10000, 0]), ["dicke"] = 300 },
        });

    static void Linie(Projekt p, string kz, params double[][] punkte) =>
        A(p, "element_aendern", new JsonObject { ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) } });

    static void Punkt(Projekt p, string kz, double[] punkt) =>
        A(p, "element_aendern", new JsonObject { ["element"] = kz, ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts(punkt) } });

    /// <summary>MFH mit Decke über UG; mit einlagen die EG-Etagenleitungen Schmutzwasser in der Decke (z −150); mit sammelleitung dazu
    /// der Anfang der Sammelleitung: Bogen BO-SS-1 auf z −150, SW-1-E von +150 lotrecht hinunter (150 von 300 mm im Beton = 50 % →
    /// frei), SW-S-1 waagrecht in der Decke bis zum T-Stück TS-SS-WM (z −150; seine Leitungen uneinig, Einfügepunkt im Beton → Einlage).
    /// Die Schemalinie TS-S1-E → TS-SS-WM ist dann teils frei (300 mm), überwiegend Einlage (4000 mm) und überdeckt in x den Zug der
    /// Etagenleitungen von SW-1 → zweite Spur.</summary>
    public static Projekt MfhMitEinlagen(bool einlagen = true, bool sammelleitung = true)
    {
        var p = Mfh();
        DeckeUeberUg(p);
        if (!einlagen) return p;
        const double z = -150;
        if (sammelleitung)
        {
            Punkt(p, "BO-SS-1", [2300, 5200, z]);
            Punkt(p, "TS-SS-WM", [6300, 5200, z]);
            Linie(p, "SW-1-E", [2300, 5200, 150], [2300, 5200, z]);
            Linie(p, "SW-S-1", [2300, 5200, z], [6300, 5200, z]);
        }
        foreach (var s in new[] { 1, 2 })
        {
            double xk = s == 1 ? 2000 : 9000, xs = xk + 300;
            string w = $"E{s}";
            for (int t = 1; t <= 3; t++) Punkt(p, $"TS-S{w}-{t}", [xs + (t == 1 ? 700 : t == 2 ? 1200 : 1900), 5200, z]);
            Linie(p, $"SW-{w}-DU", [xk + 2800, 6500, z], [xk + 2800, 5200, z], [xs + 1900, 5200, z]);
            Linie(p, $"SW-{w}-SP", [xk + 2200, 3000, z], [xs + 1900, 3000, z], [xs + 1900, 5200, z]);
            Linie(p, $"SW-{w}-3", [xs + 1900, 5200, z], [xs + 1200, 5200, z]);
            Linie(p, $"SW-{w}-WT", [xk + 1500, 6200, z], [xs + 1200, 6200, z], [xs + 1200, 5200, z]);
            Linie(p, $"SW-{w}-2", [xs + 1200, 5200, z], [xs + 700, 5200, z]);
            Linie(p, $"SW-{w}-WC", [xk + 1000, 6000, z], [xs + 700, 6000, z], [xs + 700, 5200, z]);
            Linie(p, $"SW-{w}-1", [xs + 700, 5200, z], [xs, 5200, z], [xs, 5200, 150]);
        }
        return p;
    }

    static readonly string[] EgSw = ["SW-E1-DU", "SW-E1-SP", "SW-E1-3", "SW-E1-WT", "SW-E1-2", "SW-E1-WC", "SW-E1-1"];

    [Fact]
    public void Einlage_LiegtImDeckenbandIhrerDecke_LinienUndKnoten()
    {
        var p = MfhMitEinlagen();
        var vs = Verlegung.Von(p);
        foreach (var kz in EgSw) Assert.Equal(("einlage", "Einlagen D.ü.UG"), (vs.Befund(Hole(p, kz).Id)!.Verlegung, vs.Befund(Hole(p, kz).Id)!.Plan));
        Assert.Equal(0.85, vs.Befund(Hole(p, "SW-E1-1").Id)!.AnteilBeton, 3);
        var s = Plan(p, Abwasser);
        var ug = s.Zeilen.Single(z => z.Titel == "UG"); var eg = s.Zeilen.Single(z => z.Titel == "EG");
        var band = s.Baender[eg.Index];
        Assert.Same(ug, band.Unter); Assert.Same(eg, band.Ueber);
        Assert.Equal("Decke über UG", band.Titel);
        // Spur 0: die Etagenleitungen von SW-1 und (weiter rechts, ohne Überdeckung) von SW-2; Spur 1: die Sammelleitung ab SW-1, die
        // den Zug von SW-1 in x überdeckt
        Assert.Equal(["SW-1, SW-2", "SW-1"], band.Spuren);
        double y1 = band.SpurY(0), y2 = band.SpurY(1);
        Assert.Equal((band.Mitte + 0.75, band.Mitte - 0.75), (y1, y2));
        foreach (var kz in EgSw.Concat(EgSw.Select(x => x.Replace("E1", "E2"))))
        {
            var l = LinieMit(s, kz);
            Assert.Same(band, l.Band);
            Assert.Equal(y1, l.BandY);
            Assert.True(l.Punkte.Any(q => Math.Abs(q[1] - y1) < 1e-6), $"{kz}: {Zug(l)} läuft nicht auf der Spur {y1}");
            // jedes waagrechte Stück der Linie liegt im Band (auf der Spur), senkrechte Stücke laufen ins Band
            for (int i = 1; i < l.Punkte.Count; i++)
                if (Math.Abs(l.Punkte[i - 1][1] - l.Punkte[i][1]) < 1e-6) Assert.Equal(y1, l.Punkte[i][1], 6);
        }
        foreach (var t in new[] { "TS-SE1-1", "TS-SE1-2", "TS-SE1-3", "TS-SE2-1" })
        {
            var k = Knoten(s, t);
            Assert.Same(band, k.Band);
            Assert.Equal(y1, k.Y);
            Assert.InRange(k.Y, band.Unten, band.Oben);
        }
        // Sammelleitung: vom Steig-T senkrecht ins Band, auf Spur 1 zum T-Stück TS-SS-WM (im Band), von dort die freien Leitungen
        // senkrecht aus dem Band hinunter zur Spur der Sammelleitung im UG (Versatz am Übergang)
        var la = LinieMit(s, "SW-S-1");
        Assert.Same(la, LinieMit(s, "SW-1-E"));
        Assert.Equal(y2, la.BandY);
        var tsWm = Knoten(s, "TS-SS-WM");
        Assert.Equal((band, y2), (tsWm.Band, tsWm.Y));
        var sw2 = LinieMit(s, "SW-S-2");
        Assert.Null(sw2.Band);
        var ts2 = Knoten(s, "TS-SS-2");
        Assert.Equal(new[] { tsWm.X, tsWm.Y, tsWm.X, ts2.Y, ts2.X, ts2.Y }, sw2.Punkte.SelectMany(q => q).ToArray());
        // die Endgeräte bleiben an ihrem Platz in der Zeile EG, die Senkrechte läuft vom Band zum Gerät; seit B8d stehen sie auf dem
        // Boden – die Etagenleitung ist eine Einlage, also Unterkante = Bandoberkante + Bodenabstand (statt y0 + Endgerätehöhe)
        var du = Knoten(s, "DU-E1");
        Assert.Null(du.Band);
        Assert.Equal(band.Oben + SchemaRaster.Bodenabstand, du.Unterkante!.Value, 3);
        Assert.Contains(LinieMit(s, "SW-E1-DU").Punkte, q => Math.Abs(q[0] - du.X) < 1e-6 && Math.Abs(q[1] - y1) < 1e-6);
        // JSON
        var o = s.AlsJson();
        var bj = o["baender"]!.AsArray()[eg.Index]!;
        Assert.Equal(["SW-1, SW-2", "SW-1"], bj["spuren"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(3.9, bj["hoehe"]!.GetValue<double>());
        var lj = o["linien"]!.AsArray().First(x => x!["kennzeichen"]!.AsArray().Any(k => k!.GetValue<string>() == "SW-E1-1"))!;
        // anteil = Anteil der Leitungslänge der Linie mit Verlegung einlage (SW-E1-1 ist als Ganzes Einlage, 85 % im Beton)
        Assert.Equal((eg.Index, 100.0), (lj["einlage"]!["band"]!.GetValue<int>(), lj["einlage"]!["anteil"]!.GetValue<double>()));
        Assert.Equal(eg.Index, o["knoten"]!.AsArray().First(x => x!["kennzeichen"]!.GetValue<string>() == "TS-SE1-2")!["einlage"]!.GetValue<int>());
        Assert.Contains(s.Hinweise, h => h.StartsWith("Einlagen im Deckenband: 15 Linie(n) – Decke über UG (2 Spuren)"));
        Assert.Contains(s.Hinweise, h => h == "Teils Einlage, teils frei – nach der Mehrheit der Länge gezeigt: SW-1-E.");
        // im Wasserschema keine Einlagen: Bänder unverändert
        Assert.All(Plan(p, Wasser).Baender, b => Assert.Equal((SchemaRaster.Deckenband, 0), (b.Hoehe, b.Spuren.Count)));
    }

    [Fact]
    public void Band_WaechstNachBedarf_ZeilenRueckenNach_LayoutSonstGleich()
    {
        var mit = Plan(MfhMitEinlagen(), Abwasser);
        var ohne = Plan(MfhMitEinlagen(einlagen: false), Abwasser);
        double D = SchemaRaster.Deckenband, zuwachs = 2 * SchemaRaster.Bandrand + SchemaRaster.Einlagespur - D;   // 3.9 − 2.4 = 1.5
        Assert.Equal(1.5, zuwachs, 9);
        var y0Ohne = ohne.Zeilen.ToDictionary(z => z.Titel, z => z.Y0);
        Assert.Equal((0, 50, 100, 150), (y0Ohne["UG"], y0Ohne["EG"], y0Ohne["1.OG"], y0Ohne["2.OG"]));
        var y0 = mit.Zeilen.ToDictionary(z => z.Titel, z => z.Y0);
        Assert.Equal((0, 51.5, 101.5, 151.5), (y0["UG"], y0["EG"], y0["1.OG"], y0["2.OG"]));
        var b = mit.Baender[1];
        Assert.Equal((3.9, 52.7, 48.8), (b.Hoehe, Math.Round(b.Oben, 3), Math.Round(b.Unten, 3)));   // Oberkante EG.y0 + 1.2, Unterkante UG.y0 + 50 − 1.2
        Assert.All(mit.Baender.Where(x => x != b), x => Assert.Equal(D, x.Hoehe));
        Assert.Equal(201.5, mit.Baender[^1].Mitte);
        // Kreise und Kopf rücken um den Zuwachs, x bleibt
        foreach (var g in ohne.Gruppen.Where(g => g.MitKreis))
        {
            var gm = mit.Gruppen.Single(x => x.Bezeichnung == g.Bezeichnung);
            Assert.Equal((g.KreisX, g.KreisY + 1.5, g.X0, g.Breite), (gm.KreisX, gm.KreisY, gm.X0, gm.Breite));
        }
        // alle Knoten ausser den Einlage-Knoten: gleiches x, gleiche Höhe in ihrer Zeile
        foreach (var k in ohne.Graph.Knoten)
        {
            var km = mit.Graph.Knoten.Single(x => x.Kennzeichen == k.Kennzeichen);
            if (km.Band is not null) continue;
            // B8d: die Endgeräte im EG stehen mit Einlagen über dem Band statt über der Etagenleitung (Test oben); WM-U hängt mit Einlagen an
            // TS-SS-WM im Band statt über eine Anschlusslinie unter dem Gerät an der Sammelleitung, steht also tiefer (Unterkante 4 + 3)
            if (km.Unterkante is not null && (km.Zeile!.Titel == "EG" || km.Kennzeichen == "WM-U")) continue;
            Assert.Equal((k.X, Math.Round(k.Y - k.Zeile!.Y0, 3)), (km.X, Math.Round(km.Y - km.Zeile!.Y0, 3)));
        }
        // Rahmen und Grafik: Band 1 mit 3.9 mm, Kopf um 1.5 mm höher
        var (_, u0, _, o0) = SchemaGrafik.Rahmen(ohne);
        var (_, u1, _, o1) = SchemaGrafik.Rahmen(mit);
        Assert.Equal((u0, o0 + 1.5), (u1, o1));
        var p = MfhMitEinlagen();
        var bandPoly = Grafik.Plan(p, Abwasser).Teile.Where(t => t.Ebene == "decke").SelectMany(t => t.Primitive).Where(x => x.Art == "polygon" && x.Stil.Muster == "beton")
            .Select(x => Math.Round(x.Punkte.Max(q => q[1]) - x.Punkte.Min(q => q[1]), 3)).ToList();
        Assert.Equal(5, bandPoly.Count);
        Assert.Equal(1, bandPoly.Count(h => Math.Abs(h - 3.9) < 1e-6));
    }

    [Fact]
    public void ZuegeOhneUeberdeckung_TeilenEineSpur_BandWaechstNicht()
    {
        // nur die Etagenleitungen: der Zug von SW-1 und der von SW-2 liegen in x nebeneinander → eine Spur, Band 2.4, Zeilen wie ohne
        var p = MfhMitEinlagen(sammelleitung: false);
        var s = Plan(p, Abwasser);
        var eg = s.Zeilen.Single(z => z.Titel == "EG");
        var band = s.Baender[eg.Index];
        Assert.Equal(["SW-1, SW-2"], band.Spuren);
        Assert.Equal((SchemaRaster.Deckenband, 50.0, 50.0), (band.Hoehe, band.Mitte, eg.Y0));
        Assert.Equal(band.Mitte, LinieMit(s, "SW-E2-DU").BandY);
        Assert.Equal(band.Mitte, Knoten(s, "TS-SE1-2").Y);
        Assert.Contains(s.Hinweise, h => h.StartsWith("Einlagen im Deckenband: 14 Linie(n) – Decke über UG (1 Spur)"));
        Assert.DoesNotContain(s.Hinweise, h => h.StartsWith("Teils Einlage"));
    }

    [Fact]
    public void OhneEinlagen_Unveraendert_AuchMitBetondecke()
    {
        // Testhaus ohne Decke und mit Betondecke (keine Leitung liegt darin): gleiches Schema, Bänder 2.4 mm auf den Geschossgrenzen
        var a = Plan(Mfh(), Abwasser);
        var b = Plan(MfhMitEinlagen(einlagen: false), Abwasser);
        static string Bild(Schemaplan s) => string.Join("\n",
            s.Graph.Knoten.OrderBy(k => k.Kennzeichen, StringComparer.Ordinal).Select(k => $"{k.Kennzeichen} {k.X} {k.Y} {k.Ausrichtung}")
            .Concat(s.Graph.Linien.Select(l => $"{string.Join(",", l.Elemente.Select(e => e.Kennzeichen))} {Zug(l)}").Order(StringComparer.Ordinal))
            .Concat(s.Zeilen.Select(z => $"{z.Titel} {z.Y0}")).Concat(s.Baender.Select(x => $"band {x.Index} {x.Mitte} {x.Hoehe}"))
            .Append($"kopf {s.KopfOben}"));
        Assert.Equal(Bild(a), Bild(b));
        for (int i = 0; i < a.Baender.Count - 1; i++) Assert.Equal((a.Zeilen[i].Y0, SchemaRaster.Deckenband), (a.Baender[i].Mitte, a.Baender[i].Hoehe));
        Assert.DoesNotContain(b.Hinweise, h => h.Contains("Einlage"));
        Assert.All(b.Graph.Linien, l => Assert.Null(l.Band));
    }

    [Fact]
    public void Beschriftung_AusserhalbDesBandes_MitFuehrungsstrich_Freigestellt()
    {
        var p = MfhMitEinlagen();
        var s = Plan(p, Abwasser);
        var band = s.Baender[s.Zeilen.Single(z => z.Titel == "EG").Index];
        var g = SchemaGrafik.Plan(p, s);
        var einlageIds = s.Graph.Linien.Where(l => l.Band is not null).SelectMany(l => l.Elemente).Select(e => e.Id).ToHashSet();
        var teile = g.Teile.Where(t => t.Id is not null && einlageIds.Contains(t.Id)).ToList();
        // waagrechte Grössentexte (die einzigen waagrechten Stücke einer Einlage-Linie liegen auf ihrer Spur im Band)
        var texte = teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text" && x.Groesse == 1.6 && x.Winkel is null).ToList();
        Assert.True(texte.Count >= 8, $"nur {texte.Count} Grössentexte");
        var rahmen = new List<double[]>();
        foreach (var t in texte)
        {
            // Grundlinie über dem Band (Ausgangsseite) bzw. Oberkante unter dem Band (andere Seite)
            double y = t.Punkte[0][1], h = 0.75 * t.Groesse!.Value, w = SchemaRaster.TextBreite(t.Text!, t.Groesse.Value);
            Assert.True(y >= band.Oben + GroessenTexte.Linienabstand - 1e-6 || y + h <= band.Unten - GroessenTexte.Linienabstand + 1e-6, $"{t.Text} bei y {y} im Band {band.Unten}…{band.Oben}");
            rahmen.Add([t.Punkte[0][0] - w / 2, y, t.Punkte[0][0] + w / 2, y + h]);
        }
        for (int i = 0; i < rahmen.Count; i++)
            for (int j = i + 1; j < rahmen.Count; j++)
                Assert.False(GroessenTexte.Schneidet(rahmen[i], rahmen[j]), $"Texte {texte[i].Text} und {texte[j].Text} überdecken sich");
        // je Text ein Führungsstrich: Punkt auf der Spur, Strich lotrecht vom Band zum Text, Strich unter dem Text
        var punkte = teile.SelectMany(t => t.Primitive).Where(x => x.Art == "kreis" && x.Radius == 0.3).ToList();
        Assert.Equal(texte.Count, punkte.Count);
        Assert.All(punkte, x => Assert.True(Math.Abs(x.Punkte[0][1] - band.SpurY(0)) < 1e-6 || Math.Abs(x.Punkte[0][1] - band.SpurY(1)) < 1e-6));
        var lot = teile.SelectMany(t => t.Primitive).Where(x => x.Art == "linie" && Math.Abs(x.Punkte[0][0] - x.Punkte[1][0]) < 1e-9 && x.Stil.Stift == Stifte.Duenn).ToList();
        Assert.Equal(texte.Count, lot.Count);
        Assert.All(lot, x => Assert.True(x.Punkte[1][1] > band.Oben || x.Punkte[1][1] < band.Unten));
        foreach (var t in texte) aus.WriteLine($"{t.Text} bei ({t.Punkte[0][0]}, {t.Punkte[0][1]})");
    }

    [Fact]
    public void HandLage_KnotenImBand_GiltAlsFrei_LinieBleibtImBand()
    {
        var p = MfhMitEinlagen();
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Abwasser), ("element", "TS-SE1-2"), ("x", 70.0), ("y", 70.0))));
        var s = Plan(p, Abwasser);
        var k = Knoten(s, "TS-SE1-2");
        Assert.True(k.Manuell);
        Assert.Null(k.Band);
        Assert.Equal((70.0, 70.0), (k.X, k.Y));
        var l = LinieMit(s, "SW-E1-2");   // T-2 → T-1, Einlage: senkrecht vom Hand-Knoten ins Band, auf der Spur weiter
        Assert.NotNull(l.Band);
        Assert.Contains(l.Punkte, q => Math.Abs(q[0] - 70) < 1e-6 && Math.Abs(q[1] - l.BandY) < 1e-6);
    }

    [Fact]
    public void Bodenplatte_UnterstesBand_EinlageInBand0()
    {
        // Bodenplatte unter dem UG (Oberkante −2700, Dicke 300) mit zwei Leitungen der Grundleitung RW-G darin: Band 0
        var p = MfhMitEinlagen(einlagen: false);
        A(p, "element_anlegen", new JsonObject
        {
            ["klasse"] = "Decke", ["kennzeichen"] = "D-BP", ["geschoss"] = "UG", ["eigenschaften"] = new JsonObject { ["aufbau"] = "Beton 300", ["art"] = "boden" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0, -2700], [13000, 0, -2700], [13000, 11000, -2700], [0, 11000, -2700]), ["dicke"] = 300 },
        });
        Punkt(p, "BO-RG", [6000, 9800, -2850]);
        Linie(p, "RW-1-U", [6000, 9800, -300], [6000, 9800, -2850]);
        Linie(p, "RW-G-1", [6000, 9800, -2850], [12500, 9800, -2900]);
        var vs = Verlegung.Von(p);
        Assert.Equal(("einlage", "bodenplatte", "Einlagen Bodenplatte UG"), (vs.Befund(Hole(p, "RW-G-1").Id)!.Verlegung, vs.Befund(Hole(p, "RW-G-1").Id)!.Bauteilart, vs.Befund(Hole(p, "RW-G-1").Id)!.Plan));
        var s = Plan(p, Abwasser);
        // eine Schemalinie RE-R1-U → KS-2 aus RW-1-U (frei, 2550 mm), BO-RG und RW-G-1 (Einlage, 6500 mm): 72 % Einlage → im Band 0
        var l = LinieMit(s, "RW-G-1");
        Assert.Same(l, LinieMit(s, "RW-1-U"));
        Assert.Equal(6500.0 / 9050, l.Einlageanteil, 3);
        Assert.Contains(s.Hinweise, h => h.StartsWith("Teils Einlage, teils frei – nach der Mehrheit der Länge gezeigt: RW-1-U"));
        Assert.Same(s.Baender[0], l.Band);
        Assert.Equal(s.Baender[0].Mitte, l.BandY);   // eine Spur: Mitte, das Band wächst nicht
        Assert.Equal(SchemaRaster.Deckenband, s.Baender[0].Hoehe);
        Assert.Equal("Bodenplatte", s.Baender[0].Titel);
    }

    // ------------------------------------------------------------------ Bild (Sichtprüfung)

    [Fact]
    public void Bild_MfhAbwasserMitEinlagen_AlsPlanPdf()
    {
        var p = MfhMitEinlagen();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8c-1"), ("titel", "Schema Abwasser mit Einlagen (fiktiv)"), ("format", "A2"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", Abwasser))));
        var pdf = Planausgabe.Pdf(p, "PB-1");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("HLKS_SCHEMA_BILD") is { Length: > 0 } ordner)   // Sichtprüfung von Hand
            File.WriteAllBytes(Path.Combine(ordner, "b8c_schema_abwasser_einlagen.pdf"), pdf);
        foreach (var h in Plan(p, Abwasser).Hinweise) aus.WriteLine("Abwasser: " + h);
    }
}
